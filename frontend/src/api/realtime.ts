import * as signalR from "@microsoft/signalr"
import { getAuthToken } from "./client"

const BACKEND_URL = (import.meta.env.VITE_API_URL as string | undefined)?.replace(/\/$/, '') || ''

let activeConnection: signalR.HubConnection | null = null
const activeSubscribers = new Map<string, Set<() => void>>()

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
          const callbacks = activeSubscribers.get(runId)
          callbacks?.forEach(cb => cb())
        } catch {}
      }
    })
  }

  return activeConnection
}

// Real-time run updates over persistent SignalR WebSockets; no polling.
export function subscribeToRun(runId: string, onChanged: () => void): () => void {
  const conn = getHubConnection()

  if (!activeSubscribers.has(runId)) {
    activeSubscribers.set(runId, new Set())
  }
  activeSubscribers.get(runId)!.add(onChanged)

  const join = async () => {
    try {
      if (conn.state === signalR.HubConnectionState.Disconnected) {
        await conn.start()
      }
      if (conn.state === signalR.HubConnectionState.Connected) {
        await conn.invoke("JoinRun", runId)
        onChanged()
      }
    } catch (err) {
      console.warn("SignalR JoinRun error:", err)
    }
  }

  if (conn.state === signalR.HubConnectionState.Connected) {
    conn.invoke("JoinRun", runId).then(() => onChanged()).catch(() => {})
  } else if (conn.state === signalR.HubConnectionState.Disconnected) {
    join()
  }

  return () => {
    const set = activeSubscribers.get(runId)
    if (set) {
      set.delete(onChanged)
      if (set.size === 0) {
        activeSubscribers.delete(runId)
        if (conn.state === signalR.HubConnectionState.Connected) {
          conn.invoke("LeaveRun", runId).catch(() => {})
        }
      }
    }
  }
}
