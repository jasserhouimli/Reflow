import * as signalR from "@microsoft/signalr"
import { getAuthToken, type RunSnapshot } from "./client"

const BACKEND_URL = (import.meta.env.VITE_API_URL as string | undefined)?.replace(/\/$/, '') || ''

let activeConnection: signalR.HubConnection | null = null
const activeSubscribers = new Map<string, Set<(snapshot?: RunSnapshot) => void>>()

function getHubConnection(): signalR.HubConnection {
  if (!activeConnection) {
    activeConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${BACKEND_URL}/hubs/runs`, {
        accessTokenFactory: () => getAuthToken() ?? "",
        transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect([0, 500, 1000, 2000, 5000])
      .configureLogging(signalR.LogLevel.None)
      .build()

    activeConnection.on("runSnapshot", (snapshot: RunSnapshot) => {
      const callbacks = activeSubscribers.get(snapshot.runId)
      if (callbacks) {
        callbacks.forEach(cb => cb(snapshot))
      }
    })

    activeConnection.on("runChanged", (changedRunId: string) => {
      const callbacks = activeSubscribers.get(changedRunId)
      if (callbacks) {
        callbacks.forEach(cb => cb())
      }
    })

    activeConnection.onreconnected(async () => {
      for (const runId of activeSubscribers.keys()) {
        try {
          await activeConnection?.invoke("JoinRun", runId)
        } catch {}
      }
    })
  }

  return activeConnection
}

// Real-time run updates streamed over persistent SignalR WebSockets; zero polling.
export function subscribeToRun(
  runId: string,
  onSnapshot: (snapshot?: RunSnapshot) => void
): () => void {
  const conn = getHubConnection()

  if (!activeSubscribers.has(runId)) {
    activeSubscribers.set(runId, new Set())
  }
  activeSubscribers.get(runId)!.add(onSnapshot)

  const join = async () => {
    try {
      if (conn.state === signalR.HubConnectionState.Disconnected) {
        await conn.start()
      }
      if (conn.state === signalR.HubConnectionState.Connected) {
        await conn.invoke("JoinRun", runId)
      }
    } catch (err) {
      console.warn("SignalR JoinRun error:", err)
    }
  }

  if (conn.state === signalR.HubConnectionState.Connected) {
    conn.invoke("JoinRun", runId).catch(() => {})
  } else if (conn.state === signalR.HubConnectionState.Disconnected) {
    join()
  }

  return () => {
    const set = activeSubscribers.get(runId)
    if (set) {
      set.delete(onSnapshot)
      if (set.size === 0) {
        activeSubscribers.delete(runId)
        if (conn.state === signalR.HubConnectionState.Connected) {
          conn.invoke("LeaveRun", runId).catch(() => {})
        }
      }
    }
  }
}
