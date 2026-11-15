import * as signalR from "@microsoft/signalr"

function getCookie(name: string): string | null {
  const match = document.cookie.match(new RegExp('(^| )' + name + '=([^;]+)'));
  return match ? match[2] : null;
}

const BACKEND_URL = (import.meta.env.VITE_API_URL as string | undefined)?.replace(/\/$/, '') || '';

// Real-time run updates over SignalR; triggers a REST refetch on changes.
export function subscribeToRun(runId: string, onChanged: () => void): () => void {
  const connection = new signalR.HubConnectionBuilder()
    .withUrl(`${BACKEND_URL}/hubs/runs`, {
      accessTokenFactory: () => getCookie("Reflow.Token") ?? "",
    })
    .withAutomaticReconnect()
    .build()

  let stopped = false
  connection.on("runChanged", () => { if (!stopped) onChanged() })
  connection.start()
    .then(() => connection.invoke("JoinRun", runId))
    .catch(() => { /* polling fallback covers outages */ })

  return () => {
    stopped = true
    connection.stop().catch(() => undefined)
  }
}
