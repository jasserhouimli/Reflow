import { useState, useEffect } from "react"
import { pipelines, PIPELINE_STATUSES, type Pipeline } from "@/api/client"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Skeleton } from "@/components/ui/skeleton"
import { EditorAiDialog } from "@/components/EditorAiDialog"
import {
  Workflow,
  Plus,
  Search,
  Sparkles,
  Play,
  Trash2,
  LogOut,
  Clock,
  Layers,
  ChevronRight,
  Loader2
} from "lucide-react"

interface DashboardPageProps {
  user: { id: string; email: string; displayName: string }
  onSelectPipeline: (id: string) => void
  onLogout: () => void
}

export function DashboardPage({ user, onSelectPipeline, onLogout }: DashboardPageProps) {
  const [list, setList] = useState<Pipeline[]>([])
  const [newName, setNewName] = useState("")
  const [creating, setCreating] = useState(false)
  const [isCreating, setIsCreating] = useState(false)
  const [query, setQuery] = useState("")
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState("")
  const [busy, setBusy] = useState(false)
  const [showAi, setShowAi] = useState(false)

  useEffect(() => {
    pipelines.list().then(setList).catch(e => setError(e.message)).finally(() => setLoading(false))
  }, [])

  const create = async () => {
    if (!newName.trim() || isCreating) return
    setIsCreating(true)
    try {
      const id = await pipelines.create({ name: newName.trim() })
      setList(prev => [{ id, name: newName.trim(), status: 0, currentVersion: 0, updatedAt: new Date().toISOString() }, ...prev])
      setNewName("")
      setCreating(false)
      onSelectPipeline(id)
    } catch (e) {
      setError(e instanceof Error ? e.message : "Create failed")
    } finally {
      setIsCreating(false)
    }
  }

  const remove = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation()
    if (!confirm("Are you sure you want to delete this pipeline?")) return
    await pipelines.remove(id)
    setList(prev => prev.filter(p => p.id !== id))
  }

  const createSample = async () => {
    setBusy(true)
    try {
      const id = await pipelines.create({ name: "Sample Sales Analysis" })
      await pipelines.update(id, {
        nodes: [
          { nodeId: "orders", nodeType: "csv.read", configJson: "{\"csvText\":\"country,amount\\nFR,120\\nDE,35\\nFR,200\"}", label: "Orders Data", positionX: 60, positionY: 120 },
          { nodeId: "revenue", nodeType: "data.sql", configJson: "{\"query\":\"SELECT country, SUM(CAST(amount AS DOUBLE)) AS total_rev FROM orders GROUP BY country ORDER BY total_rev DESC\"}", label: "Revenue Aggregation", positionX: 400, positionY: 120 },
        ],
        edges: [{ sourceNodeId: "orders", targetNodeId: "revenue" }],
      })
      await pipelines.publish(id)
      onSelectPipeline(id)
    } catch (e) {
      setError(e instanceof Error ? e.message : "Sample creation failed")
    } finally {
      setBusy(false)
    }
  }

  const shown = query.trim()
    ? list.filter(p => p.name.toLowerCase().includes(query.trim().toLowerCase()))
    : list

  return (
    <div className="min-h-screen bg-background flex flex-col">
      {/* Top Navigation */}
      <header className="border-b bg-card px-6 py-3 flex items-center justify-between sticky top-0 z-10 shadow-2xs">
        <div className="flex items-center gap-3">
          <div className="h-7 w-7 rounded-md bg-foreground text-background flex items-center justify-center font-bold">
            <Workflow className="h-4 w-4" />
          </div>
          <div>
            <span className="font-semibold text-sm tracking-tight">Reflow</span>
            <span className="text-[10px] text-muted-foreground ml-2 px-1.5 py-0.5 rounded bg-muted font-mono">v1.0</span>
          </div>
        </div>

        <div className="flex items-center gap-3 text-xs">
          <div className="hidden sm:flex items-center gap-2 px-2.5 py-1 rounded-md bg-muted/50 border text-muted-foreground font-mono">
            <span>{user.email}</span>
          </div>
          <Button variant="ghost" size="sm" onClick={onLogout} className="h-7 text-xs text-muted-foreground hover:text-foreground">
            <LogOut className="h-3.5 w-3.5 mr-1" />
            Logout
          </Button>
        </div>
      </header>

      {/* Main Content */}
      <main className="flex-1 max-w-5xl w-full mx-auto px-6 py-8 space-y-6">
        {/* Banner / Actions Header */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div>
            <h1 className="text-xl font-bold tracking-tight">Data Pipelines</h1>
            <p className="text-xs text-muted-foreground mt-0.5">
              Build, schedule, and orchestrate reactive data workflows.
            </p>
          </div>

          <div className="flex items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={createSample}
              disabled={busy}
              className="h-8 text-xs gap-1.5"
            >
              {busy ? (
                <>
                  <Loader2 className="h-3.5 w-3.5 animate-spin text-emerald-600" />
                  Deploying...
                </>
              ) : (
                <>
                  <Play className="h-3.5 w-3.5 text-emerald-600" />
                  Try Sample
                </>
              )}
            </Button>
            <Button
              variant="outline"
              size="sm"
              onClick={() => setShowAi(true)}
              className="h-8 text-xs gap-1.5"
            >
              <Sparkles className="h-3.5 w-3.5 text-blue-500" />
              AI Prompt
            </Button>
            <Button
              size="sm"
              onClick={() => setCreating(true)}
              className="h-8 text-xs gap-1.5 font-medium"
            >
              <Plus className="h-3.5 w-3.5" />
              New Pipeline
            </Button>
          </div>
        </div>

        {/* Quick inline creator */}
        {creating && (
          <div className="p-3 rounded-lg border bg-card shadow-xs flex items-center gap-2">
            <Input
              autoFocus
              placeholder="Pipeline name (e.g., Daily Customer Sync)..."
              value={newName}
              onChange={e => setNewName(e.target.value)}
              onKeyDown={e => {
                if (e.key === "Enter") create()
                if (e.key === "Escape") setCreating(false)
              }}
              className="h-8 text-xs flex-1"
            />
            <Button size="sm" className="h-8 text-xs gap-1.5" onClick={create} disabled={!newName.trim() || isCreating}>
              {isCreating ? (
                <>
                  <Loader2 className="h-3.5 w-3.5 animate-spin" />
                  Creating...
                </>
              ) : (
                "Create & Open"
              )}
            </Button>
            <Button size="sm" variant="ghost" className="h-8 text-xs" onClick={() => setCreating(false)}>
              Cancel
            </Button>
          </div>
        )}

        {/* Filter Bar */}
        <div className="flex items-center justify-between gap-4">
          <div className="relative flex-1 max-w-sm">
            <Search className="h-3.5 w-3.5 absolute left-2.5 top-1/2 -translate-y-1/2 text-muted-foreground" />
            <Input
              placeholder="Filter pipelines..."
              value={query}
              onChange={e => setQuery(e.target.value)}
              className="h-8 pl-8 text-xs"
            />
          </div>
          <span className="text-xs text-muted-foreground font-mono">
            {shown.length} {shown.length === 1 ? "pipeline" : "pipelines"}
          </span>
        </div>

        {error && (
          <p className="text-xs text-destructive bg-destructive/10 border border-destructive/20 p-2.5 rounded-lg">
            {error}
          </p>
        )}

        {/* Pipeline List */}
        {loading ? (
          <div className="border rounded-lg bg-card divide-y overflow-hidden shadow-2xs">
            {[...Array(4)].map((_, i) => (
              <div key={i} className="px-4 py-3 flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <Skeleton className="h-8 w-8 rounded-md" />
                  <div className="space-y-1.5">
                    <Skeleton className="h-4 w-36" />
                    <Skeleton className="h-3 w-20" />
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  <Skeleton className="h-5 w-14 rounded-full" />
                  <Skeleton className="h-4 w-4 rounded" />
                </div>
              </div>
            ))}
          </div>
        ) : shown.length === 0 ? (
          <div className="border border-dashed rounded-xl p-12 text-center space-y-3 bg-muted/10">
            <div className="h-10 w-10 rounded-full bg-muted flex items-center justify-center mx-auto text-muted-foreground">
              <Layers className="h-5 w-5" />
            </div>
            <div>
              <p className="text-sm font-semibold">
                {list.length === 0 ? "No pipelines created yet" : "No pipelines match your search"}
              </p>
              <p className="text-xs text-muted-foreground mt-1 max-w-sm mx-auto">
                {list.length === 0
                  ? "Get started by building a custom data flow, generating one with AI, or trying the sample sales workflow."
                  : "Try clearing your search query to see all available pipelines."}
              </p>
            </div>
            {list.length === 0 && (
              <div className="flex justify-center gap-2 pt-2">
                <Button size="sm" onClick={() => setCreating(true)} className="h-8 text-xs gap-1.5">
                  <Plus className="h-3.5 w-3.5" />
                  Create First Pipeline
                </Button>
                <Button size="sm" variant="outline" onClick={createSample} disabled={busy} className="h-8 text-xs gap-1.5">
                  <Play className="h-3.5 w-3.5 text-emerald-600" />
                  Try Sample Sales Flow
                </Button>
              </div>
            )}
          </div>
        ) : (
          <div className="border rounded-lg bg-card divide-y overflow-hidden shadow-2xs">
            {shown.map(p => {
              const statusName = PIPELINE_STATUSES[p.status] ?? "Draft"
              const isPublished = p.status === 1 || p.currentVersion > 0
              return (
                <div
                  key={p.id}
                  onClick={() => onSelectPipeline(p.id)}
                  className="px-4 py-3 flex items-center justify-between hover:bg-muted/40 cursor-pointer transition-colors group"
                >
                  <div className="flex items-center gap-3 min-w-0">
                    <div className="h-8 w-8 rounded-md bg-muted/60 flex items-center justify-center text-muted-foreground shrink-0 group-hover:text-primary transition-colors">
                      <Workflow className="h-4 w-4" />
                    </div>
                    <div className="min-w-0">
                      <div className="flex items-center gap-2">
                        <span className="text-xs font-semibold text-foreground group-hover:text-primary transition-colors truncate">
                          {p.name}
                        </span>
                        <span className={`text-[10px] px-1.5 py-0.2 rounded font-medium ${
                          isPublished
                            ? "bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400"
                            : "bg-muted text-muted-foreground"
                        }`}>
                          {statusName}
                        </span>
                      </div>
                      <div className="flex items-center gap-3 text-[11px] text-muted-foreground font-mono mt-0.5">
                        <span>v{p.currentVersion}</span>
                        {p.updatedAt && (
                          <span className="flex items-center gap-1 text-[10px]">
                            <Clock className="h-2.5 w-2.5" />
                            {new Date(p.updatedAt).toLocaleDateString()}
                          </span>
                        )}
                      </div>
                    </div>
                  </div>

                  <div className="flex items-center gap-2">
                    <Button
                      variant="ghost"
                      size="sm"
                      className="h-7 w-7 p-0 text-muted-foreground hover:text-destructive opacity-0 group-hover:opacity-100 transition-opacity"
                      onClick={(e) => remove(p.id, e)}
                      title="Delete pipeline"
                    >
                      <Trash2 className="h-3.5 w-3.5" />
                    </Button>
                    <ChevronRight className="h-4 w-4 text-muted-foreground/50 group-hover:text-foreground transition-colors" />
                  </div>
                </div>
              )
            })}
          </div>
        )}
      </main>

      {/* AI Generator Modal */}
      {showAi && (
        <EditorAiDialog
          isOpen={showAi}
          onClose={() => setShowAi(false)}
          existingNodesCount={0}
          defs={[]}
          onApply={async (nodes, edges) => {
            try {
              const id = await pipelines.create({ name: "AI Generated Pipeline" })
              await pipelines.update(id, {
                nodes: nodes.map(n => ({
                  nodeId: n.id,
                  nodeType: (n.data as { nodeType: string }).nodeType,
                  configJson: (n.data as { configJson: string }).configJson || "{}",
                  label: (n.data as { label?: string }).label ?? null,
                  positionX: Math.round(n.position.x),
                  positionY: Math.round(n.position.y),
                })),
                edges: edges.map(e => ({
                  sourceNodeId: e.source,
                  targetNodeId: e.target,
                })),
              })
              setShowAi(false)
              onSelectPipeline(id)
            } catch (err) {
              setError(err instanceof Error ? err.message : "Failed to create pipeline from AI draft")
            }
          }}
        />
      )}
    </div>
  )
}
