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
  Activity,
  Cpu,
  Database,
} from "lucide-react"

interface DashboardPageProps {
  user: { id: string; email: string; displayName: string }
  onSelectPipeline: (id: string) => void
  onLogout: () => void
}

function DashboardLoadingSkeleton() {
  return (
    <div className="space-y-3">
      {[...Array(4)].map((_, i) => (
        <div
          key={i}
          className="p-4 rounded-lg border border-border/70 bg-card/70 flex items-center justify-between"
        >
          <div className="space-y-2.5">
            <div className="flex items-center gap-2.5">
              <Skeleton className="h-4 w-44 rounded" />
              <Skeleton className="h-4 w-16 rounded-full" />
            </div>
            <div className="flex items-center gap-3">
              <Skeleton className="h-3 w-16 rounded" />
              <Skeleton className="h-3 w-28 rounded" />
            </div>
          </div>
          <div className="flex items-center gap-3">
            <Skeleton className="h-7 w-20 rounded-md" />
            <Skeleton className="h-5 w-5 rounded" />
          </div>
        </div>
      ))}
    </div>
  )
}

export function DashboardPage({ user, onSelectPipeline, onLogout }: DashboardPageProps) {
  const [list, setList] = useState<Pipeline[]>([])
  const [newName, setNewName] = useState("")
  const [creating, setCreating] = useState(false)
  const [query, setQuery] = useState("")
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState("")
  const [busy, setBusy] = useState(false)
  const [showAi, setShowAi] = useState(false)

  useEffect(() => {
    pipelines.list().then(setList).catch(e => setError(e.message)).finally(() => setLoading(false))
  }, [])

  const create = async () => {
    if (!newName.trim()) return
    try {
      const id = await pipelines.create({ name: newName.trim() })
      setList(prev => [{ id, name: newName.trim(), status: 0, currentVersion: 0, updatedAt: new Date().toISOString() }, ...prev])
      setNewName("")
      setCreating(false)
      onSelectPipeline(id)
    } catch (e) {
      setError(e instanceof Error ? e.message : "Create failed")
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

  const publishedCount = list.filter(p => p.status === 1 || p.currentVersion > 0).length

  return (
    <div className="min-h-screen bg-background flex flex-col bg-dot-grid">
      {/* Top Navigation */}
      <header className="border-b bg-card/85 backdrop-blur-md px-6 py-3 flex items-center justify-between sticky top-0 z-10 shadow-xs">
        <div className="flex items-center gap-3">
          <div className="h-7 w-7 rounded-md bg-primary text-primary-foreground flex items-center justify-center font-bold shadow-xs">
            <Workflow className="h-4 w-4" />
          </div>
          <div className="flex items-center gap-2">
            <span className="font-semibold text-sm tracking-tight text-foreground">Reflow</span>
            <span className="text-[10px] text-muted-foreground px-1.5 py-0.5 rounded bg-muted/80 border border-border/80 font-mono">
              Workspace
            </span>
          </div>
        </div>

        <div className="flex items-center gap-3 text-xs">
          <div className="hidden sm:flex items-center gap-2 px-2.5 py-1 rounded-md bg-muted/40 border border-border/80 text-muted-foreground font-mono">
            <div className="h-1.5 w-1.5 rounded-full bg-emerald-500 animate-pulse" />
            <span>{user.email}</span>
          </div>

          <Button
            variant="ghost"
            size="sm"
            onClick={onLogout}
            className="h-7 text-xs text-muted-foreground hover:text-foreground cursor-pointer"
          >
            <LogOut className="h-3.5 w-3.5 mr-1" />
            Sign Out
          </Button>
        </div>
      </header>

      {/* Main Content */}
      <main className="flex-1 max-w-5xl w-full mx-auto px-6 py-8 space-y-6">
        {/* Banner / Actions Header */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div>
            <h1 className="text-xl font-bold tracking-tight text-foreground">Data Pipelines</h1>
            <p className="text-xs text-muted-foreground mt-0.5">
              Build, schedule, and orchestrate reactive data workflows with DuckDB and AI.
            </p>
          </div>

          <div className="flex items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={createSample}
              disabled={busy}
              className="h-8 text-xs gap-1.5 border-border/80 bg-card/80 hover:bg-muted cursor-pointer"
            >
              <Play className="h-3.5 w-3.5 text-emerald-500" />
              {busy ? "Deploying..." : "Try Sample"}
            </Button>
            <Button
              variant="outline"
              size="sm"
              onClick={() => setShowAi(true)}
              className="h-8 text-xs gap-1.5 border-border/80 bg-card/80 hover:bg-muted cursor-pointer"
            >
              <Sparkles className="h-3.5 w-3.5 text-primary" />
              AI Prompt
            </Button>
            <Button
              size="sm"
              onClick={() => setCreating(true)}
              className="h-8 text-xs gap-1.5 font-medium shadow-xs cursor-pointer active:scale-[0.985] transition-all"
            >
              <Plus className="h-3.5 w-3.5" />
              New Pipeline
            </Button>
          </div>
        </div>

        {/* Databricks KPI Strip */}
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-3.5">
          <div className="p-3.5 rounded-lg border border-border/80 bg-card/75 backdrop-blur-xs flex items-center justify-between">
            <div className="space-y-0.5">
              <span className="text-[11px] font-medium text-muted-foreground uppercase tracking-wider font-mono">
                Pipelines
              </span>
              <div className="flex items-baseline gap-2">
                <span className="text-lg font-bold text-foreground">{list.length}</span>
                <span className="text-[11px] text-muted-foreground font-mono">
                  ({publishedCount} live)
                </span>
              </div>
            </div>
            <div className="h-8 w-8 rounded-md bg-muted/60 flex items-center justify-center text-muted-foreground">
              <Activity className="h-4 w-4 text-primary" />
            </div>
          </div>

          <div className="p-3.5 rounded-lg border border-border/80 bg-card/75 backdrop-blur-xs flex items-center justify-between">
            <div className="space-y-0.5">
              <span className="text-[11px] font-medium text-muted-foreground uppercase tracking-wider font-mono">
                Engine Runtime
              </span>
              <div className="flex items-center gap-1.5">
                <span className="text-xs font-semibold text-emerald-400">Operational</span>
                <div className="h-1.5 w-1.5 rounded-full bg-emerald-500 animate-pulse" />
              </div>
            </div>
            <div className="h-8 w-8 rounded-md bg-muted/60 flex items-center justify-center text-muted-foreground">
              <Cpu className="h-4 w-4 text-emerald-500" />
            </div>
          </div>

          <div className="p-3.5 rounded-lg border border-border/80 bg-card/75 backdrop-blur-xs flex items-center justify-between">
            <div className="space-y-0.5">
              <span className="text-[11px] font-medium text-muted-foreground uppercase tracking-wider font-mono">
                Data Backend
              </span>
              <div className="flex items-center gap-1.5">
                <span className="text-xs font-semibold text-foreground">In-Memory DuckDB</span>
              </div>
            </div>
            <div className="h-8 w-8 rounded-md bg-muted/60 flex items-center justify-center text-muted-foreground">
              <Database className="h-4 w-4 text-cyan-400" />
            </div>
          </div>
        </div>

        {/* Quick inline creator */}
        {creating && (
          <div className="p-3 rounded-lg border border-primary/40 bg-card/90 shadow-sm flex items-center gap-2 animate-dialog-pop">
            <Input
              autoFocus
              placeholder="Pipeline name (e.g., Daily Customer Sync)..."
              value={newName}
              onChange={e => setNewName(e.target.value)}
              onKeyDown={e => {
                if (e.key === "Enter") create()
                if (e.key === "Escape") setCreating(false)
              }}
              className="h-8 text-xs flex-1 bg-background/80"
            />
            <Button size="sm" className="h-8 text-xs cursor-pointer" onClick={create} disabled={!newName.trim()}>
              Create & Open
            </Button>
            <Button size="sm" variant="ghost" className="h-8 text-xs cursor-pointer" onClick={() => setCreating(false)}>
              Cancel
            </Button>
          </div>
        )}

        {/* Filter Bar */}
        <div className="flex items-center justify-between gap-4">
          <div className="relative flex-1 max-w-sm">
            <Search className="h-3.5 w-3.5 absolute left-2.5 top-1/2 -translate-y-1/2 text-muted-foreground" />
            <Input
              placeholder="Filter pipelines by name..."
              value={query}
              onChange={e => setQuery(e.target.value)}
              className="h-8 pl-8 text-xs bg-card/60"
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
          <DashboardLoadingSkeleton />
        ) : shown.length === 0 ? (
          <div className="border border-dashed border-border/80 rounded-xl p-12 text-center space-y-3 bg-card/30">
            <div className="h-10 w-10 rounded-full bg-muted/60 flex items-center justify-center mx-auto text-muted-foreground">
              <Layers className="h-5 w-5" />
            </div>
            <div>
              <p className="text-sm font-semibold text-foreground">
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
                <Button size="sm" onClick={() => setCreating(true)} className="h-8 text-xs gap-1.5 cursor-pointer">
                  <Plus className="h-3.5 w-3.5" />
                  Create First Pipeline
                </Button>
                <Button size="sm" variant="outline" onClick={createSample} disabled={busy} className="h-8 text-xs gap-1.5 cursor-pointer">
                  <Play className="h-3.5 w-3.5 text-emerald-500" />
                  Try Sample Sales Flow
                </Button>
              </div>
            )}
          </div>
        ) : (
          <div className="border border-border/80 rounded-lg bg-card/85 divide-y divide-border/60 overflow-hidden shadow-xs">
            {shown.map(p => {
              const statusName = PIPELINE_STATUSES[p.status] ?? "Draft"
              const isPublished = p.status === 1 || p.currentVersion > 0
              return (
                <div
                  key={p.id}
                  onClick={() => onSelectPipeline(p.id)}
                  className="px-4 py-3.5 flex items-center justify-between hover:bg-muted/40 cursor-pointer transition-all duration-150 group"
                >
                  <div className="flex items-center gap-3.5 min-w-0">
                    <div className="h-8 w-8 rounded-md bg-muted/60 border border-border/60 flex items-center justify-center text-muted-foreground shrink-0 group-hover:text-primary group-hover:border-primary/40 transition-colors">
                      <Workflow className="h-4 w-4" />
                    </div>
                    <div className="min-w-0">
                      <div className="flex items-center gap-2.5">
                        <span className="text-xs font-semibold text-foreground group-hover:text-primary transition-colors truncate">
                          {p.name}
                        </span>
                        <span
                          className={`text-[10px] px-2 py-0.5 rounded-full font-medium inline-flex items-center gap-1.5 border ${
                            isPublished
                              ? "bg-emerald-500/10 text-emerald-400 border-emerald-500/30"
                              : "bg-muted/60 text-muted-foreground border-border/80"
                          }`}
                        >
                          <span
                            className={`h-1.5 w-1.5 rounded-full ${
                              isPublished ? "bg-emerald-400 animate-pulse" : "bg-muted-foreground"
                            }`}
                          />
                          {statusName}
                        </span>
                      </div>
                      <div className="flex items-center gap-3 text-[11px] text-muted-foreground font-mono mt-1">
                        <span className="px-1.5 py-0.2 rounded bg-muted/60 border border-border/60">
                          v{p.currentVersion}
                        </span>
                        {p.updatedAt && (
                          <span className="flex items-center gap-1 text-[10px]">
                            <Clock className="h-3 w-3" />
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
                      className="h-7 w-7 p-0 text-muted-foreground hover:text-destructive hover:bg-destructive/10 opacity-0 group-hover:opacity-100 transition-all cursor-pointer"
                      onClick={(e) => remove(p.id, e)}
                      title="Delete pipeline"
                    >
                      <Trash2 className="h-3.5 w-3.5" />
                    </Button>
                    <ChevronRight className="h-4 w-4 text-muted-foreground/40 group-hover:text-foreground group-hover:translate-x-0.5 transition-all" />
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
