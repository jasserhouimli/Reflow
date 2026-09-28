import { useState, useEffect } from "react"
import { pipelines, PIPELINE_STATUSES, type Pipeline } from "@/api/client"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Card, CardHeader, CardTitle } from "@/components/ui/card"

interface DashboardPageProps {
  user: { id: string; email: string; displayName: string }
  onSelectPipeline: (id: string) => void
  onLogout: () => void
}

export function DashboardPage({ user, onSelectPipeline, onLogout }: DashboardPageProps) {
  const [list, setList] = useState<Pipeline[]>([])
  const [newName, setNewName] = useState("")
  const [query, setQuery] = useState("")
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState("")
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    pipelines.list().then(setList).catch(e => setError(e.message)).finally(() => setLoading(false))
  }, [])

  const create = async () => {
    if (!newName.trim()) return
    try {
      const id = await pipelines.create({ name: newName.trim() })
      setList(prev => [{ id, name: newName.trim(), status: 0, currentVersion: 0, updatedAt: new Date().toISOString() }, ...prev])
      setNewName("")
    } catch (e) {
      setError(e instanceof Error ? e.message : "Create failed")
    }
  }

  const remove = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation()
    await pipelines.remove(id)
    setList(prev => prev.filter(p => p.id !== id))
  }

  const createSample = async () => {
    setBusy(true)
    try {
      const id = await pipelines.create({ name: "Sample sales" })
      await pipelines.update(id, {
        nodes: [
          { nodeId: "orders", nodeType: "csv.read", configJson: "{\"csvText\":\"country,amount\\nFR,120\\nDE,35\\nFR,200\"}", label: "Orders", positionX: 60, positionY: 120 },
          { nodeId: "report", nodeType: "data.sql", configJson: "{\"query\":\"SELECT country, SUM(CAST(amount AS DOUBLE)) AS revenue FROM orders GROUP BY country ORDER BY revenue DESC\"}", label: "Report", positionX: 380, positionY: 120 },
        ],
        edges: [{ sourceNodeId: "orders", targetNodeId: "report" }],
      })
      await pipelines.publish(id)
      onSelectPipeline(id)
    } catch (e) {
      setError(e instanceof Error ? e.message : "Sample failed")
    } finally {
      setBusy(false)
    }
  }

  const shown = query.trim()
    ? list.filter(p => p.name.toLowerCase().includes(query.trim().toLowerCase()))
    : list

  return (
    <div className="max-w-4xl mx-auto p-8">
      <div className="flex items-center justify-between mb-8">
        <h1 className="text-2xl font-bold">Reflow</h1>
        <div className="flex items-center gap-4">
          <span className="text-sm text-muted-foreground">{user.email}</span>
          <Button variant="ghost" size="sm" onClick={onLogout}>Logout</Button>
        </div>
      </div>
      <div className="flex gap-2 mb-4">
        <Input placeholder="New pipeline name" value={newName} onChange={e => setNewName(e.target.value)} onKeyDown={e => e.key === 'Enter' && create()} />
        <Button onClick={create}>Create</Button>
        <Button variant="outline" onClick={createSample} disabled={busy}>{busy ? "…" : "Try a sample"}</Button>
      </div>
      <div className="mb-6">
        <Input placeholder="Search pipelines…" value={query} onChange={e => setQuery(e.target.value)} />
      </div>
      {error && <p className="text-sm text-destructive mb-4">{error}</p>}
      {loading ? <p>Loading...</p> : (
        <div className="grid gap-4">
          {shown.map(p => (
            <Card key={p.id} className="cursor-pointer hover:bg-accent/50 transition-colors" onClick={() => onSelectPipeline(p.id)}>
              <CardHeader className="py-3">
                <div className="flex items-center justify-between">
                  <CardTitle className="text-lg">{p.name}</CardTitle>
                  <div className="flex items-center gap-2">
                    <span className="text-xs px-2 py-0.5 rounded bg-blue-100 text-blue-700">
                      {PIPELINE_STATUSES[p.status] ?? p.status} · v{p.currentVersion}
                    </span>
                    <Button variant="ghost" size="sm" className="text-destructive h-7" onClick={(e) => remove(p.id, e)}>x</Button>
                  </div>
                </div>
              </CardHeader>
            </Card>
          ))}
          {shown.length === 0 && <p className="text-muted-foreground">{list.length === 0 ? "No pipelines yet. Create one — or try a sample." : "No pipelines match."}</p>}
        </div>
      )}
    </div>
  )
}
