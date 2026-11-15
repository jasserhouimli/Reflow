import * as signalR from "@microsoft/signalr"
import { getAuthToken, type RunSnapshot, runs } from "./client"

const BACKEND_URL = (import.meta.env.VITE_API_URL as string | undefined)?.replace(/\/$/, '') || ''

let activeConnection: signalR.HubConnection | null = null
let startPromise: Promise<void> | null = null
const activeSubscribers = new Map<string, Set<(snapshot?: RunSnapshot) => void>>()

function norm(id: string): string {
  return id.trim().toLowerCase()
}

function getHubConnection(): signalR.HubConnection {
  if (!activeConnection) {
    activeConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${BACKEND_URL}/hubs/runs`, {
        accessTokenFactory: () => getAuthToken() ?? "",
        transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect([0, 500, 1000, 2000, 5000])
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    activeConnection.on("runSnapshot", (snapshot: RunSnapshot) => {
      const targetId = norm(snapshot.runId)
      const callbacks = activeSubscribers.get(targetId)
      if (callbacks) {
        callbacks.forEach(cb => cb(snapshot))
      }
    })

    activeConnection.on("runChanged", (changedRunId: string) => {
      const targetId = norm(changedRunId)
      const callbacks = activeSubscribers.get(targetId)
      if (callbacks) {
        callbacks.forEach(cb => cb())
      }
    })

    activeConnection.onreconnected(async () => {
      for (const runId of activeSubscribers.keys()) {
        try {
          await activeConnection?.invoke("JoinRun", runId)
        } catch (err) {
          console.warn("SignalR re-join failed:", err)
        }
      }
    })
  }

  return activeConnection
}

async function ensureConnected(conn: signalR.HubConnection): Promise<boolean> {
  if (conn.state === signalR.HubConnectionState.Connected) return true

  if (!startPromise) {
    startPromise = (async () => {
      try {
        if (conn.state === signalR.HubConnectionState.Disconnected) {
          await conn.start()
        }
      } catch (err) {
        console.warn("SignalR start error:", err)
      } finally {
        startPromise = null
      }
    })()
  }

  await startPromise
  return (conn.state as signalR.HubConnectionState) === signalR.HubConnectionState.Connected
}

// Real-time run updates streamed over persistent SignalR WebSockets with instant initial sync.
export function subscribeToRun(
  runId: string,
  onSnapshot: (snapshot?: RunSnapshot) => void
): () => void {
  const normalizedId = norm(runId)
  const conn = getHubConnection()

  if (!activeSubscribers.has(normalizedId)) {
    activeSubscribers.set(normalizedId, new Set())
  }
  activeSubscribers.get(normalizedId)!.add(onSnapshot)

  // 1. Immediate HTTP baseline fetch so the UI is never blank while socket connects
  Promise.all([
    runs.get(runId).catch(() => null),
    runs.tasks(runId).catch(() => []),
    runs.logs(runId).catch(() => [])
  ]).then(([run, tasks, logs]) => {
    if (run && activeSubscribers.has(normalizedId)) {
      onSnapshot({
        runId,
        run,
        tasks,
        logs,
      })
    }
  }).catch(() => {})

  // 2. Connect and join SignalR group for real-time live events
  ensureConnected(conn).then(connected => {
    if (connected && activeSubscribers.has(normalizedId)) {
      conn.invoke("JoinRun", runId).catch(err => {
        console.warn("SignalR JoinRun error:", err)
      })
    }
  })

  // 3. Safety interval: ONLY while run is running (every 2s), poll to ensure zero lost updates
  let timer: ReturnType<typeof setTimeout> | null = null
  let running = true

  const checkStatus = async () => {
    if (!running || !activeSubscribers.has(normalizedId)) return
    try {
      const [r, t, l] = await Promise.all([
        runs.get(runId),
        runs.tasks(runId),
        runs.logs(runId)
      ])
      if (activeSubscribers.has(normalizedId)) {
        onSnapshot({ runId, run: r, tasks: t, logs: l })
        if (r.status < 2 && running) {
          timer = setTimeout(checkStatus, 2000)
        }
      }
    } catch {
      if (running && activeSubscribers.has(normalizedId)) {
        timer = setTimeout(checkStatus, 3000)
      }
    }
  }

  // Start safety check after 1.5s if socket hasn't completed it
  timer = setTimeout(checkStatus, 1500)

  return () => {
    running = false
    if (timer) clearTimeout(timer)

    const set = activeSubscribers.get(normalizedId)
    if (set) {
      set.delete(onSnapshot)
      if (set.size === 0) {
        activeSubscribers.delete(normalizedId)
        if (conn.state === signalR.HubConnectionState.Connected) {
          conn.invoke("LeaveRun", runId).catch(() => {})
        }
      }
    }
  }
}
