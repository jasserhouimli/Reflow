import { useState, useEffect, useCallback, useRef, useMemo } from "react"
import {
  ReactFlow, ReactFlowProvider, useReactFlow, Background, Controls, MiniMap, Handle, Position,
  applyNodeChanges, applyEdgeChanges, addEdge, MarkerType,
  type Node, type Edge, type NodeChange, type EdgeChange, type Connection,
  type NodeProps,
} from "@xyflow/react"
import "@xyflow/react/dist/style.css"
import {
  pipelines, runs, triggers, nodeTypes, versions, artifactUrl, files,
  RUN_STATUSES, TASK_STATUSES,
  type PipelineDetail, type PipelineEdge,
  type NodeDefinition, type PipelineRun, type TaskRun, type RunLog, type RunSnapshot, type TriggerItem,
  type TaskDetail, type TaskAttempt, type VersionDetail, type SourceFile,
} from "@/api/client"
import { subscribeToRun } from "@/api/realtime"
import { ScheduleDialog } from "@/components/ScheduleDialog"
import { WebhookDialog } from "@/components/WebhookDialog"
import { EditorAiDialog } from "@/components/EditorAiDialog"
import { SqlEditor, type UpstreamNode } from "@/components/SqlEditor"
import { cronToHuman } from "@/lib/cronHelper"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Skeleton } from "@/components/ui/skeleton"
import {
  Play,
  Terminal,
  ChevronUp,
  ChevronDown,
  Table,
  Sparkles,
  ArrowLeft,
  Check,
  X,
  Search,
  GripVertical,
  Database,
  FileText,
  Layers,
  Filter,
  ArrowUpDown,
  GitMerge,
  Code2,
  Sigma,
  HardDrive,
  LayoutGrid,
  Trash2,
  Loader2,
  FileEdit,
} from "lucide-react"

interface Props {
  pipelineId: string
  onBack: () => void
  onLogout: () => void
}

type FlowNodeData = {
  label: string
  nodeType: string
  configJson: string
  status?: number
} & Record<string, unknown>

const FILTER_OPS = ["equals", "notEquals", "contains", "startsWith", "greaterThan", "lessThan", "isEmpty", "isNotEmpty"]

function parseConfig(json: string): Record<string, unknown> | null {
  try {
    const v = JSON.parse(json)
    return typeof v === "object" && v !== null ? v as Record<string, unknown> : null
  } catch {
    return null
  }
}

function FileReadConfigForm({ configJson, onChange }: {
  configJson: string
  onChange: (json: string) => void
}) {
  const cfg = parseConfig(configJson) ?? {}
  const [available, setAvailable] = useState<SourceFile[]>([])
  const [error, setError] = useState("")
  const [busy, setBusy] = useState(false)

  const set = (k: string, v: unknown) => onChange(JSON.stringify({ ...cfg, [k]: v }, null, 1))
  const str = (k: string) => typeof cfg[k] === "string" ? cfg[k] as string : ""

  useEffect(() => {
    let cancelled = false
    files.list().then(f => { if (!cancelled) setAvailable(f) }).catch(() => { if (!cancelled) setAvailable([]) })
    return () => { cancelled = true }
  }, [])

  const upload = async (file: File) => {
    setBusy(true)
    setError("")
    try {
      const content = await file.text()
      const created = await files.upload(file.name, content)
      setAvailable(await files.list())
      set("fileId", created.id)
      set("format", file.name.toLowerCase().endsWith(".json") ? "json" : "csv")
    } catch (e) {
      setError(e instanceof Error ? e.message : "Upload failed")
    } finally {
      setBusy(false)
    }
  }

  const isJson = str("format") === "json"

  return (
    <div className="space-y-2">
      <label className="text-xs block">Source file
        <select className="h-8 rounded-md border px-2 text-xs w-full"
          value={str("fileId")}
          onChange={e => set("fileId", e.target.value)}>
          <option value="">— select a file —</option>
          {available.map(f => <option key={f.id} value={f.id}>{f.fileName}</option>)}
        </select>
      </label>
      <label className="text-xs block">Upload CSV or JSON
        <input type="file" accept=".csv,.json" disabled={busy} className="block w-full text-xs"
          onChange={e => { const f = e.target.files?.[0]; if (f) void upload(f); e.target.value = "" }} />
      </label>
      {busy && <p className="text-xs text-muted-foreground">Uploading…</p>}
      {error && <p className="text-xs text-destructive">{error}</p>}
      {!str("fileId") && <p className="text-xs text-muted-foreground">Select or upload a file first.</p>}
      {isJson
        ? <label className="text-xs block">Root path
            <Input className="h-8 font-mono" placeholder="data.orders"
              value={str("rootPath")} onChange={e => set("rootPath", e.target.value)} />
          </label>
        : <div className="flex gap-2">
            <label className="text-xs">Delimiter<Input className="h-8 w-16" value={str("delimiter") || ","}
              onChange={e => set("delimiter", e.target.value.slice(0, 1) || ",")} /></label>
            <label className="text-xs flex items-center gap-1 mt-5">
              <input type="checkbox" checked={cfg["hasHeader"] !== false}
                onChange={e => set("hasHeader", e.target.checked)} /> header
            </label>
          </div>}
    </div>
  )
}

function NodeConfigForm({ nodeType, configJson, onChange, upstreamNodes = [] }: {
  nodeType: string
  configJson: string
  onChange: (json: string) => void
  upstreamNodes?: UpstreamNode[]
}) {
  const cfg = parseConfig(configJson)
  if (cfg === null) {
    return (
      <div className="space-y-1">
        <p className="text-xs text-destructive">Invalid JSON — fix below:</p>
        <RawJson configJson={configJson} onChange={onChange} />
      </div>
    )
  }
  const set = (k: string, v: unknown) => onChange(JSON.stringify({ ...cfg, [k]: v }, null, 1))
  const str = (k: string) => typeof cfg[k] === "string" ? cfg[k] as string : ""
  const bool = (k: string, dflt: boolean) => typeof cfg[k] === "boolean" ? cfg[k] as boolean : dflt

  switch (nodeType) {
    case "file.read":
      return <FileReadConfigForm configJson={configJson} onChange={onChange} />
    case "csv.read":
      return (
        <div className="space-y-2">
          <label className="text-xs">CSV text<textarea className="w-full text-xs font-mono border rounded p-2" rows={6}
            value={str("csvText")} onChange={e => set("csvText", e.target.value)} /></label>
          <div className="flex gap-2">
            <label className="text-xs">Delimiter<Input className="h-8" value={str("delimiter") || ","}
              onChange={e => set("delimiter", e.target.value.slice(0, 1) || ",")} /></label>
            <label className="text-xs flex items-center gap-1 mt-5">
              <input type="checkbox" checked={bool("hasHeader", true)} onChange={e => set("hasHeader", e.target.checked)} /> header
            </label>
          </div>
        </div>
      )
    case "json.read":
      return (
        <div className="space-y-2">
          <label className="text-xs">JSON text<textarea className="w-full text-xs font-mono border rounded p-2" rows={6}
            value={str("jsonText")} onChange={e => set("jsonText", e.target.value)} /></label>
          <label className="text-xs">Root path<Input className="h-8 font-mono" placeholder="data.orders"
            value={str("rootPath")} onChange={e => set("rootPath", e.target.value)} /></label>
          <label className="text-xs">Upstream column (unpack mode)<Input className="h-8 font-mono"
            value={str("column")} onChange={e => set("column", e.target.value)} /></label>
        </div>
      )
    case "filter":
      return (
        <div className="space-y-2">
          <label className="text-xs">Column<Input className="h-8 font-mono"
            value={str("column")} onChange={e => set("column", e.target.value)} /></label>
          <label className="text-xs">Operator
            <select className="h-8 rounded-md border px-2 text-xs w-full" value={str("operator") || "equals"}
              onChange={e => set("operator", e.target.value)}>
              {FILTER_OPS.map(o => <option key={o} value={o}>{o}</option>)}
            </select></label>
          <label className="text-xs">Value<Input className="h-8 font-mono"
            value={str("value")} onChange={e => set("value", e.target.value)} /></label>
        </div>
      )
    case "transform":
      return (
        <div className="space-y-2">
          <label className="text-xs">Select (comma-separated)<Input className="h-8 font-mono"
            value={(cfg["select"] as string[] | undefined)?.join(", ") ?? ""}
            onChange={e => set("select", e.target.value.split(",").map(s => s.trim()).filter(Boolean))} /></label>
          <label className="text-xs">Drop columns (comma-separated)<Input className="h-8 font-mono"
            value={(cfg["dropColumns"] as string[] | undefined)?.join(", ") ?? ""}
            onChange={e => set("dropColumns", e.target.value.split(",").map(s => s.trim()).filter(Boolean))} /></label>
          <label className="text-xs">Renames (old=new, one per line)<textarea className="w-full text-xs font-mono border rounded p-2" rows={2}
            value={Object.entries((cfg["renames"] as Record<string, string> | undefined) ?? {}).map(([k, v]) => `${k}=${v}`).join("\n")}
            onChange={e => {
              const map: Record<string, string> = {}
              e.target.value.split("\n").forEach(l => {
                const i = l.indexOf("=")
                if (i > 0) map[l.slice(0, i).trim()] = l.slice(i + 1).trim()
              })
              set("renames", map)
            }} /></label>
        </div>
      )
    case "data.sql":
      return (
        <div className="space-y-1.5">
          <label className="text-xs font-semibold text-foreground">SQL Query</label>
          <SqlEditor
            value={str("query")}
            onChange={val => set("query", val)}
            upstreamNodes={upstreamNodes}
          />
        </div>
      )
    case "aggregate":
      return (
        <div className="space-y-2">
          <label className="text-xs">Group by (comma-separated)<Input className="h-8 font-mono"
            value={(cfg["groupBy"] as string[] | undefined)?.join(", ") ?? ""}
            onChange={e => set("groupBy", e.target.value.split(",").map(s => s.trim()).filter(Boolean))} /></label>
          <label className="text-xs">Operations (function:column:alias, one per line)<textarea
            className="w-full text-xs font-mono border rounded p-2" rows={3}
            placeholder={"sum:amount:total\ncount:::n"}
            value={((cfg["operations"] as Array<{ function: string; column?: string; as?: string }> | undefined) ?? [])
              .map(o => [o.function, o.column ?? "", o.as ?? ""].join(":")).join("\n")}
            onChange={e => set("operations", e.target.value.split("\n").map(l => {
              const [f, c, a] = l.split(":")
              return { function: (f ?? "").trim(), column: (c ?? "").trim(), as: (a ?? "").trim() }
            }).filter(o => o.function))} /></label>
          <p className="text-[11px] text-muted-foreground">functions: count, countDistinct, sum, avg, min, max</p>
        </div>
      )
    case "sort":
      return (
        <div className="space-y-2">
          <label className="text-xs">Order by (column:direction, one per line)<textarea
            className="w-full text-xs font-mono border rounded p-2" rows={3} placeholder={"amount:desc\ncountry:asc"}
            value={((cfg["orderBy"] as Array<{ column: string; direction?: string }> | undefined) ?? [])
              .map(o => `${o.column}:${o.direction ?? "asc"}`).join("\n")}
            onChange={e => set("orderBy", e.target.value.split("\n").map(l => {
              const [c, d] = l.split(":")
              return { column: (c ?? "").trim(), direction: (d ?? "asc").trim() }
            }).filter(o => o.column))} /></label>
        </div>
      )
    case "join":
      return (
        <div className="space-y-2">
          <label className="text-xs">On (shared key)<Input className="h-8 font-mono"
            value={str("on")} onChange={e => set("on", e.target.value)} /></label>
          <div className="flex gap-2">
            <label className="text-xs">Left key<Input className="h-8 font-mono"
              value={str("leftOn")} onChange={e => set("leftOn", e.target.value)} /></label>
            <label className="text-xs">Right key<Input className="h-8 font-mono"
              value={str("rightOn")} onChange={e => set("rightOn", e.target.value)} /></label>
          </div>
          <label className="text-xs">How
            <select className="h-8 rounded-md border px-2 text-xs w-full" value={str("how") || "inner"}
              onChange={e => set("how", e.target.value)}>
              <option value="inner">inner</option>
              <option value="left">left</option>
            </select></label>
        </div>
      )
    case "data.output":
      return (
        <div className="space-y-2">
          <label className="text-xs">Format
            <select className="h-8 rounded-md border px-2 text-xs w-full" value={str("format") || "json"}
              onChange={e => set("format", e.target.value)}>
              <option value="json">json</option>
              <option value="csv">csv</option>
            </select></label>
          <label className="text-xs">File name (optional)<Input className="h-8 font-mono"
            value={str("fileName")} onChange={e => set("fileName", e.target.value)} /></label>
        </div>
      )
    default:
      return <RawJson configJson={configJson} onChange={onChange} />
  }
}

function RawJson({ configJson, onChange }: { configJson: string; onChange: (j: string) => void }) {
  return <textarea className="w-full text-xs font-mono border rounded p-2" rows={8} value={configJson} onChange={e => onChange(e.target.value)} />
}

function getNodeIcon(type: string, category?: string) {
  switch (type) {
    case "file.read":
    case "csv.read":
    case "json.read":
      return <FileText className="h-3.5 w-3.5" />
    case "trigger.payload":
      return <Database className="h-3.5 w-3.5" />
    case "filter":
      return <Filter className="h-3.5 w-3.5" />
    case "sort":
      return <ArrowUpDown className="h-3.5 w-3.5" />
    case "join":
      return <GitMerge className="h-3.5 w-3.5" />
    case "transform":
      return <Layers className="h-3.5 w-3.5" />
    case "data.sql":
      return <Code2 className="h-3.5 w-3.5" />
    case "aggregate":
      return <Sigma className="h-3.5 w-3.5" />
    case "data.output":
      return <HardDrive className="h-3.5 w-3.5" />
    default:
      if (category === "Sources") return <Database className="h-3.5 w-3.5" />
      if (category === "Transform") return <Layers className="h-3.5 w-3.5" />
      if (category === "Analysis") return <Code2 className="h-3.5 w-3.5" />
      if (category === "Sinks") return <HardDrive className="h-3.5 w-3.5" />
      return <Layers className="h-3.5 w-3.5" />
  }
}

function getCategoryIcon(cat: string) {
  switch (cat) {
    case "Sources":
      return <Database className="h-3 w-3" />
    case "Transform":
      return <Layers className="h-3 w-3" />
    case "Analysis":
      return <Code2 className="h-3 w-3" />
    case "Sinks":
      return <HardDrive className="h-3 w-3" />
    default:
      return <Layers className="h-3 w-3" />
  }
}

function PipelineNodeView({ id, data, selected }: NodeProps) {
  const d = data as unknown as FlowNodeData & { category?: string }
  const isRunning = d.status === 2
  const isSuccess = d.status === 3
  const isFailed = d.status === 4
  const isQueued = d.status === 0 || d.status === 1 || d.status === 6

  const statusBorder =
    isRunning ? "border-blue-500 ring-2 ring-blue-500/50 shadow-md shadow-blue-500/20" :
    isSuccess ? "border-emerald-500/80 ring-1 ring-emerald-500/40" :
    isFailed ? "border-rose-500 ring-2 ring-rose-500/40" :
    isQueued ? "border-amber-500/60" :
    selected ? "ring-2 ring-primary border-primary" : "border-border"

  return (
    <div className={`rounded-lg border bg-card px-3 py-2.5 shadow-2xs min-w-44 transition-all ${statusBorder}`}>
      <Handle type="target" position={Position.Left} className="!w-2.5 !h-2.5 !bg-muted-foreground/60" />
      <div className="flex items-center justify-between gap-2">
        <div className="flex items-center gap-1.5 min-w-0">
          <span className="text-muted-foreground shrink-0">
            {getNodeIcon(d.nodeType, d.category)}
          </span>
          <div className="text-xs font-semibold truncate text-foreground">{d.label}</div>
        </div>
        {isRunning && (
          <span className="flex items-center gap-1 text-[10px] text-blue-600 dark:text-blue-400 font-mono font-medium animate-pulse shrink-0">
            <span className="h-1.5 w-1.5 rounded-full bg-blue-500 animate-ping" />
            running
          </span>
        )}
        {isSuccess && (
          <span className="inline-flex items-center gap-1 text-[10px] px-1.5 py-0.5 rounded bg-emerald-100 text-emerald-700 dark:bg-emerald-950/50 dark:text-emerald-400 font-medium shrink-0">
            <Check className="h-3 w-3" />
            ok
          </span>
        )}
        {isFailed && (
          <span className="inline-flex items-center gap-1 text-[10px] px-1.5 py-0.5 rounded bg-rose-100 text-rose-700 dark:bg-rose-950/50 dark:text-rose-400 font-medium shrink-0">
            <X className="h-3 w-3" />
            error
          </span>
        )}
        {isQueued && (
          <span className="text-[10px] px-1.5 py-0.2 rounded bg-amber-100 text-amber-700 dark:bg-amber-950 dark:text-amber-400 font-medium shrink-0">
            queued
          </span>
        )}
      </div>
      <div className="flex items-center justify-between gap-1.5 mt-2">
        <span className="text-[9px] px-1.5 py-0.5 rounded font-mono font-medium inline-block bg-muted/80 text-muted-foreground border border-border/40">
          {d.nodeType}
        </span>
        <span className="text-[10px] font-mono text-muted-foreground truncate" title={`Table name: ${id}`}>
          ({id})
        </span>
      </div>
      <Handle type="source" position={Position.Right} className="!w-2.5 !h-2.5 !bg-muted-foreground/60" />
    </div>
  )
}

const nodeTypesMap = { pipelineNode: PipelineNodeView }

const toFlow = (d: PipelineDetail, defs?: NodeDefinition[]): Node[] =>
  d.nodes.map(n => ({
    id: n.nodeId,
    type: "pipelineNode",
    position: { x: n.positionX, y: n.positionY },
    data: {
      label: n.label || n.nodeId,
      nodeType: n.nodeType,
      configJson: n.configJson,
      category: defs?.find(def => def.type === n.nodeType)?.category,
    },
  }))

// Connections must stay readable when fitView zooms a wide graph out. The
// library default is a 1px light-grey line, which shrinks below a pixel and
// effectively disappears at 0.5 zoom.
const EDGE_COLOR = "#64748b"

function parseUtcDate(dateStr: string | null | undefined): number {
  if (!dateStr) return Date.now()
  const normalized = dateStr.endsWith("Z") || dateStr.includes("+") ? dateStr : `${dateStr}Z`
  return new Date(normalized).getTime()
}

const edgeVisuals = () => ({
  type: "smoothstep" as const,
  style: { stroke: EDGE_COLOR, strokeWidth: 2 },
  markerEnd: { type: MarkerType.ArrowClosed, color: EDGE_COLOR, width: 18, height: 18 },
})

const toFlowEdges = (edges: PipelineEdge[]): Edge[] =>
  edges.map((e, i) => ({
    ...edgeVisuals(),
    id: `e${i}-${e.sourceNodeId}-${e.targetNodeId}`,
    source: e.sourceNodeId,
    target: e.targetNodeId,
  }))

const snapshot = (name: string, nodes: Node[], edges: Edge[]) =>
  JSON.stringify({
    name,
    nodes: nodes.map(n => {
      const d = n.data as unknown as FlowNodeData
      return {
        id: n.id,
        position: { x: Math.round(n.position.x), y: Math.round(n.position.y) },
        data: { label: d.label, nodeType: d.nodeType, configJson: d.configJson },
      }
    }),
    edges: edges.map(e => ({ s: e.source, t: e.target })),
  })

export function PipelineEditorPage(props: Props) {
  return (
    <ReactFlowProvider>
      <PipelineEditor {...props} />
    </ReactFlowProvider>
  )
}

function PipelineEditor({ pipelineId, onBack, onLogout }: Props) {
  const { screenToFlowPosition } = useReactFlow()
  const [detail, setDetail] = useState<PipelineDetail | null>(null)
  const [defs, setDefs] = useState<NodeDefinition[]>([])
  const defsRef = useRef<NodeDefinition[]>([])
  useEffect(() => { defsRef.current = defs }, [defs])
  const [name, setName] = useState("")
  const [nodes, setNodes] = useState<Node[]>([])
  const [edges, setEdges] = useState<Edge[]>([])
  const [savedSnap, setSavedSnap] = useState("")
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [nodeIdInput, setNodeIdInput] = useState("")
  const [selectedEdgeId, setSelectedEdgeId] = useState<string | null>(null)
  const [activeView, setActiveView] = useState<"canvas" | "runs" | "triggers" | "versions">("canvas")
  const [paletteQuery, setPaletteQuery] = useState("")
  const [now, setNow] = useState(() => Date.now())
  const [msg, setMsg] = useState("")
  const [err, setErr] = useState("")

  const [runList, setRunList] = useState<PipelineRun[]>([])
  const [runId, setRunId] = useState<string | null>(null)
  const [activeRunState, setActiveRunState] = useState<PipelineRun | null>(null)
  const [consoleOpen, setConsoleOpen] = useState(false)
  const [consoleTab, setConsoleTab] = useState<"logs" | "output" | "tasks">("logs")
  const [tasks, setTasks] = useState<TaskRun[]>([])
  const [logs, setLogs] = useState<RunLog[]>([])
  const [taskDetailId, setTaskDetailId] = useState<string | null>(null)
  const [taskDetail, setTaskDetail] = useState<TaskDetail | null>(null)
  const [attempts, setAttempts] = useState<TaskAttempt[]>([])

  const selectTask = (id: string | null) => {
    setTaskDetailId(id)
    if (!id) {
      setTaskDetail(null)
      setAttempts([])
    }
  }

  const [versionList, setVersionList] = useState<Array<{ versionNumber: number; publishedAt: string }>>([])
  const [versionDetail, setVersionDetail] = useState<VersionDetail | null>(null)

  const [triggerList, setTriggerList] = useState<TriggerItem[]>([])
  const [scheduleDialogOpen, setScheduleDialogOpen] = useState(false)
  const [webhookDialogOpen, setWebhookDialogOpen] = useState(false)
  const [aiDialogOpen, setAiDialogOpen] = useState(false)

  const [isSaving, setIsSaving] = useState(false)
  const [isRunningTest, setIsRunningTest] = useState(false)
  const [isPublishing, setIsPublishing] = useState(false)
  const [isValidating, setIsValidating] = useState(false)
  const [isDeleting, setIsDeleting] = useState(false)
  const [isCancellingRun, setIsCancellingRun] = useState(false)

  useEffect(() => {
    const handleGlobalKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "k") {
        e.preventDefault()
        setAiDialogOpen(prev => !prev)
      }
    }
    window.addEventListener("keydown", handleGlobalKey)
    return () => window.removeEventListener("keydown", handleGlobalKey)
  }, [])

  const handleApplyAiDraft = (newNodes: Node[], newEdges: Edge[], mode: "replace" | "append") => {
    if (mode === "replace") {
      setNodes(newNodes)
      setEdges(newEdges)
      say(`Replaced canvas with ${newNodes.length} AI-generated nodes`)
    } else {
      const existingIds = new Set(nodes.map(n => n.id))
      const idMap = new Map<string, string>()
      const maxX = nodes.reduce((max, n) => Math.max(max, n.position.x), 0)
      const offsetX = nodes.length > 0 ? maxX + 320 : 0

      const mappedNodes: Node[] = newNodes.map(n => {
        let uniqueId = n.id
        let counter = 1
        while (existingIds.has(uniqueId)) {
          counter++
          uniqueId = `${n.id}_${counter}`
        }
        existingIds.add(uniqueId)
        idMap.set(n.id, uniqueId)

        return {
          ...n,
          id: uniqueId,
          position: { x: n.position.x + offsetX, y: n.position.y },
          data: {
            ...n.data,
            label: n.data.label === n.id ? uniqueId : n.data.label,
          },
        }
      })

      const mappedEdges: Edge[] = newEdges.map(e => {
        const newSource = idMap.get(e.source) ?? e.source
        const newTarget = idMap.get(e.target) ?? e.target
        return {
          ...e,
          id: `e-${newSource}-${newTarget}`,
          source: newSource,
          target: newTarget,
        }
      })

      setNodes(prev => [...prev, ...mappedNodes])
      setEdges(prev => [...prev, ...mappedEdges])
      say(`Added ${mappedNodes.length} nodes to canvas`)
    }
  }

  const say = (m: string) => { setMsg(m); setErr("") }
  const fail = (e: unknown) => { setErr(e instanceof Error ? e.message : "Failed"); setMsg("") }
  const dirty = savedSnap !== "" && savedSnap !== snapshot(name, nodes, edges)

  const paletteGroups = (() => {
    const q = paletteQuery.trim().toLowerCase()
    const filtered = q
      ? defs.filter(d => (d.displayName + " " + d.type + " " + d.description).toLowerCase().includes(q))
      : defs
    const groups = new Map<string, NodeDefinition[]>()
    for (const d of filtered) {
      const list = groups.get(d.category) ?? []
      list.push(d)
      groups.set(d.category, list)
    }
    return [...groups.entries()]
  })()

  const upstreamNodes = useMemo(() => {
    if (!selectedId) return []
    const incomingSources = new Set(edges.filter(e => e.target === selectedId).map(e => e.source))
    return nodes
      .filter(n => incomingSources.has(n.id))
      .map(n => ({
        id: n.id,
        label: (n.data as unknown as FlowNodeData)?.label || n.id,
        nodeType: (n.data as unknown as FlowNodeData)?.nodeType || "",
      }))
  }, [edges, nodes, selectedId])

  const paintStatuses = (t: TaskRun[]) => {
    const byNode = new Map(t.map(x => [x.nodeId, x.status] as const))
    setNodes(prev => prev.map(n => ({ ...n, data: { ...n.data, status: byNode.get(n.id) } })))
    setEdges(prev => prev.map(e => {
      const s = byNode.get(e.source)
      return {
        ...e,
        animated: s === 2,
        style: s === 3 ? { stroke: "#22c55e", strokeWidth: 2 }
          : s === 4 ? { stroke: "#ef4444", strokeWidth: 2 }
          : s === 2 ? { stroke: "#3b82f6", strokeWidth: 2 }
          : undefined,
      }
    }))
  }

  const autoLayout = () => {
    const incoming = new Map<string, string[]>()
    const outgoing = new Map<string, string[]>()
    for (const n of nodes) { incoming.set(n.id, []); outgoing.set(n.id, []) }
    for (const e of edges) {
      outgoing.get(e.source)?.push(e.target)
      incoming.get(e.target)?.push(e.source)
    }
    const depth = new Map<string, number>()
    const visit = (id: string, d: number, stack: Set<string>) => {
      if (stack.has(id)) return
      if ((depth.get(id) ?? -1) >= d) return
      depth.set(id, d)
      stack.add(id)
      for (const next of outgoing.get(id) ?? []) visit(next, d + 1, stack)
      stack.delete(id)
    }
    for (const n of nodes) if ((incoming.get(n.id) ?? []).length === 0) visit(n.id, 0, new Set())
    for (const n of nodes) if (!depth.has(n.id)) visit(n.id, 0, new Set())
    const layers = new Map<number, string[]>()
    for (const [id, d] of depth) {
      const list = layers.get(d) ?? []
      list.push(id)
      layers.set(d, list)
    }
    const pos = new Map<string, { x: number; y: number }>()
    for (const [d, ids] of [...layers.entries()].sort((a, b) => a[0] - b[0]))
      ids.sort().forEach((id, i) => pos.set(id, { x: 80 + d * 300, y: 80 + i * 150 }))
    setNodes(ns => ns.map(n => ({ ...n, position: pos.get(n.id) ?? n.position })))
    say("Auto-layout applied — Save to keep it")
  }

  const deleteSelection = useCallback(() => {
    if (selectedId) {
      setNodes(ns => ns.filter(n => n.id !== selectedId))
      setEdges(es => es.filter(e => e.source !== selectedId && e.target !== selectedId))
      setSelectedId(null)
    } else if (selectedEdgeId) {
      setEdges(es => es.filter(e => e.id !== selectedEdgeId))
      setSelectedEdgeId(null)
    }
  }, [selectedId, selectedEdgeId])

  useEffect(() => {
    const h = (e: KeyboardEvent) => {
      const t = e.target as HTMLElement
      if (t.tagName === "INPUT" || t.tagName === "TEXTAREA" || t.tagName === "SELECT" || t.isContentEditable) return
      if (e.key === "Delete" || e.key === "Backspace") deleteSelection()
    }
    window.addEventListener("keydown", h)
    return () => window.removeEventListener("keydown", h)
  }, [deleteSelection])

  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 1000)
    return () => clearInterval(timer)
  }, [])

  const reload = useCallback(async () => {
    try {
      const d = await pipelines.get(pipelineId)
      setDetail(d); setName(d.name)
      const n = toFlow(d, defsRef.current); const e = toFlowEdges(d.edges)
      setNodes(n); setEdges(e)
      setSavedSnap(snapshot(d.name, n, e))
      setRunList(await runs.list(pipelineId))
      setTriggerList(await triggers.list(pipelineId))
      setVersionList(await pipelines.versions(pipelineId))
    } catch (e) { fail(e) }
  }, [pipelineId])

  useEffect(() => {
    let alive = true
    nodeTypes.list()
      .then(d => {
        if (!alive) return
        defsRef.current = d
        setDefs(d)
        void reload()
      })
      .catch(() => {
        if (!alive) return
        defsRef.current = []
        setDefs([])
        void reload()
      })
    return () => { alive = false }
  }, [reload])

  useEffect(() => {
    if (!runId) return
    let alive = true
    const handleSnapshot = (snapshot?: RunSnapshot) => {
      if (!alive) return
      if (snapshot) {
        setTasks(snapshot.tasks)
        setLogs(snapshot.logs)
        paintStatuses(snapshot.tasks)
        setActiveRunState(snapshot.run)
        setRunList(prev => {
          const idx = prev.findIndex(x => x.id === snapshot.run.id)
          if (idx >= 0) {
            const next = [...prev]
            next[idx] = snapshot.run
            return next
          }
          return [snapshot.run, ...prev]
        })
      } else {
        Promise.all([runs.tasks(runId), runs.logs(runId), runs.get(runId)])
          .then(([t, l, r]) => {
            if (!alive) return
            setTasks(t); setLogs(l); paintStatuses(t)
            setActiveRunState(r)
            setRunList(prev => {
              const idx = prev.findIndex(x => x.id === r.id)
              if (idx >= 0) {
                const next = [...prev]
                next[idx] = r
                return next
              }
              return [r, ...prev]
            })
          })
          .catch(() => {})
      }
    }
    const stopSignalR = subscribeToRun(runId, handleSnapshot)
    return () => { alive = false; stopSignalR() }
  }, [runId])

  useEffect(() => {
    if (!taskDetailId) return
    let alive = true
    runs.taskDetail(taskDetailId)
      .then(d => { if (alive) setTaskDetail(d) })
      .catch(fail)
    runs.taskAttempts(taskDetailId)
      .then(a => { if (alive) setAttempts(a) })
      .catch(() => { if (alive) setAttempts([]) })
    return () => { alive = false }
  }, [taskDetailId])

  const onNodesChange = useCallback(
    (changes: NodeChange[]) => setNodes(ns => applyNodeChanges(changes, ns) as Node[]), [])
  const onEdgesChange = useCallback(
    (changes: EdgeChange[]) => setEdges(es => applyEdgeChanges(changes, es)), [])
  const onConnect = useCallback(
    (c: Connection) => setEdges(es => addEdge({ ...c, ...edgeVisuals() }, es)), [])

  const onDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault()
    const type = e.dataTransfer.getData("application/reflow-nodetype")
    if (!type) return
    // Convert screen coordinates through React Flow's current pan/zoom
    // transform; DOM offsets only line up at the default viewport.
    const point = screenToFlowPosition({ x: e.clientX, y: e.clientY })
    const definition = defs.find(d => d.type === type)
    const baseName = (definition?.displayName || type.split('.').pop() || "node")
      .toLowerCase().replace(/[^a-z0-9_]/g, "_").replace(/^_+|_+$/g, "") || "node"
    let id = baseName
    let counter = 1
    while (nodes.some(n => n.id === id)) {
      counter++
      id = `${baseName}_${counter}`
    }
    setNodes(ns => [...ns, {
      id, type: "pipelineNode",
      position: { x: point.x - 70, y: point.y - 30 },
      data: {
        label: definition?.displayName ?? type,
        nodeType: type,
        configJson: type === "data.sql" ? "{\"query\":\"SELECT * FROM input\"}" : "{}",
        category: definition?.category,
      },
    }])
  }, [defs, screenToFlowPosition, nodes])

  const selected = nodes.find(n => n.id === selectedId)

  useEffect(() => {
    if (selectedId) {
      setNodeIdInput(selectedId)
    }
  }, [selectedId])

  const commitNodeIdChange = () => {
    if (!selectedId) return
    const clean = nodeIdInput.trim().toLowerCase().replace(/[^a-z0-9_]/g, "_").replace(/^_+|_+$/g, "")
    if (!clean) {
      setNodeIdInput(selectedId)
      return
    }
    if (clean === selectedId) return
    if (nodes.some(n => n.id === clean)) {
      say(`Table name '${clean}' is already in use by another node`)
      setNodeIdInput(selectedId)
      return
    }

    const oldId = selectedId
    setNodes(ns => ns.map(n => n.id === oldId ? { ...n, id: clean } : n))
    setEdges(es => es.map(e => ({
      ...e,
      source: e.source === oldId ? clean : e.source,
      target: e.target === oldId ? clean : e.target,
    })))
    setSelectedId(clean)
    setNodeIdInput(clean)
    say(`Renamed table to '${clean}'`)
  }
  const activeRun = (runId && activeRunState?.id === runId) ? activeRunState : (runList.find(r => r.id === runId) ?? activeRunState)
  const patchSelected = (patch: Partial<FlowNodeData>) => {
    if (!selectedId) return
    setNodes(ns => ns.map(n => n.id === selectedId ? { ...n, data: { ...n.data, ...patch } } : n))
  }

  const save = async () => {
    if (isSaving) return
    setIsSaving(true)
    try {
      const outNodes = nodes.map(n => {
        const d = n.data as unknown as FlowNodeData
        JSON.parse(d.configJson)
        return {
          nodeId: n.id, nodeType: d.nodeType, configJson: d.configJson,
          label: d.label === n.id ? null : d.label,
          positionX: Math.round(n.position.x), positionY: Math.round(n.position.y),
        }
      })
      const outEdges = edges.map(e => ({ sourceNodeId: e.source, targetNodeId: e.target }))
      await pipelines.update(pipelineId, { name, nodes: outNodes, edges: outEdges })
      setSavedSnap(snapshot(name, nodes, edges))
      say("Saved")
      await reload()
    } catch (e) {
      fail(e)
    } finally {
      setIsSaving(false)
    }
  }

  const handleRunTest = async () => {
    if (isRunningTest) return
    setIsRunningTest(true)
    try {
      const id = await runs.start(pipelineId)
      setRunId(id)
      setActiveRunState({
        id,
        pipelineId,
        versionNumber: detail?.currentVersion ?? 1,
        status: 1,
        triggerKind: "manual",
        error: null,
        createdAt: new Date().toISOString(),
        startedAt: new Date().toISOString(),
        completedAt: null,
      })
      selectTask(null)
      setConsoleOpen(true)
      setConsoleTab("logs")
      say("Run started")
      runs.list(pipelineId).then(setRunList).catch(() => {})
    } catch (e) {
      fail(e)
    } finally {
      setIsRunningTest(false)
    }
  }

  const handlePublish = async () => {
    if (isPublishing) return
    setIsPublishing(true)
    try {
      const r = await pipelines.publish(pipelineId)
      say(`Published v${r.version}`)
      await reload()
    } catch (e) {
      fail(e)
    } finally {
      setIsPublishing(false)
    }
  }

  const handleValidate = async () => {
    if (isValidating) return
    setIsValidating(true)
    try {
      const r = await pipelines.validate(pipelineId)
      if (r.valid) say("Pipeline is valid")
      else fail(r.errors.join("; "))
    } catch (e) {
      fail(e)
    } finally {
      setIsValidating(false)
    }
  }

  const removePipeline = async () => {
    if (!window.confirm(`Delete pipeline "${name}" and all its runs?`)) return
    if (isDeleting) return
    setIsDeleting(true)
    try {
      await pipelines.remove(pipelineId)
      onBack()
    } catch (e) {
      fail(e)
      setIsDeleting(false)
    }
  }

  const handleCancelRun = async (runIdToCancel: string) => {
    if (isCancellingRun) return
    setIsCancellingRun(true)
    try {
      await runs.cancel(runIdToCancel)
      await reload()
    } catch (e) {
      fail(e)
    } finally {
      setIsCancellingRun(false)
    }
  }

  const outputPreview = (taskDetail?.outputJson ?? null) ? (() => {
    try {
      const rows = JSON.parse(taskDetail!.outputJson!) as Array<Record<string, string | null>>
      if (!Array.isArray(rows) || rows.length === 0) return null
      const cols = Object.keys(rows[0])
      return { cols, rows: rows.slice(0, 50), total: rows.length }
    } catch { return null }
  })() : null

  if (!detail) {
    return (
      <div className="h-screen flex flex-col bg-background text-foreground">
        <div className="flex items-center justify-between px-4 py-2 border-b bg-card shrink-0">
          <div className="flex items-center gap-3">
            <Button variant="ghost" size="sm" className="h-8 w-8 p-0 text-muted-foreground" onClick={onBack}>
              <ArrowLeft className="h-4 w-4" />
            </Button>
            <Skeleton className="h-5 w-40 rounded" />
            <Skeleton className="h-5 w-24 rounded-md" />
          </div>
          <div className="flex items-center gap-2">
            <Skeleton className="h-8 w-20 rounded-md" />
            <Skeleton className="h-8 w-16 rounded-md" />
          </div>
        </div>
        <div className="flex-1 flex overflow-hidden">
          <div className="flex-1 flex items-center justify-center p-8">
            {err ? (
              <p className="text-destructive text-xs bg-destructive/10 border border-destructive/20 p-4 rounded-lg">
                {err}
              </p>
            ) : (
              <div className="space-y-3 text-center">
                <Skeleton className="h-10 w-10 rounded-lg mx-auto" />
                <Skeleton className="h-3 w-32 mx-auto rounded" />
              </div>
            )}
          </div>
          <div className="w-80 border-l bg-card p-4 space-y-3 hidden md:block">
            <Skeleton className="h-4 w-24 rounded" />
            <div className="space-y-2 pt-2">
              <Skeleton className="h-12 w-full rounded-md" />
              <Skeleton className="h-12 w-full rounded-md" />
              <Skeleton className="h-12 w-full rounded-md" />
            </div>
          </div>
        </div>
      </div>
    )
  }

  return (
    <div className="h-screen flex flex-col bg-background text-foreground">
      {/* Top Header */}
      <div className="flex items-center justify-between px-4 py-2 border-b bg-card shrink-0">
        {/* Left: Back + Name + Status */}
        <div className="flex items-center gap-3">
          <Button variant="ghost" size="sm" className="h-8 w-8 p-0 text-muted-foreground hover:text-foreground" onClick={onBack} title="Back to Pipelines">
            <ArrowLeft className="h-4 w-4" />
          </Button>
          <div className="flex items-center gap-2">
            <Input
              value={name}
              onChange={e => setName(e.target.value)}
              className="w-48 h-8 text-sm font-semibold border-transparent hover:border-border focus:border-border transition-colors px-2"
              placeholder="Pipeline Name"
            />
            <div className="inline-flex items-center gap-1.5 px-2 py-1 rounded-md border border-border/60 bg-muted/30 text-xs">
              {detail.status === 1 ? (
                <>
                  <Check className="h-3.5 w-3.5 text-emerald-600 dark:text-emerald-400" />
                  <span className="font-semibold text-foreground">Published</span>
                </>
              ) : (
                <>
                  <FileEdit className="h-3.5 w-3.5 text-muted-foreground" />
                  <span className="font-medium text-muted-foreground">Draft</span>
                </>
              )}
              <span className="text-muted-foreground/30">|</span>
              <span className="font-mono text-[11px] text-muted-foreground font-normal">v{detail.currentVersion}</span>
            </div>
            {dirty && (
              <span className="text-[11px] text-amber-600 dark:text-amber-400 font-medium px-2 py-0.5 rounded-md border border-amber-500/25 bg-amber-500/10">
                unsaved
              </span>
            )}
          </div>
        </div>

        {/* Center: View Switcher Segmented Control */}
        <div className="flex items-center p-0.5 rounded-lg bg-muted/60 border text-xs">
          <button
            onClick={() => setActiveView("canvas")}
            className={`px-3 py-1 rounded-md font-medium transition-all ${
              activeView === "canvas" ? "bg-background shadow-xs text-foreground" : "text-muted-foreground hover:text-foreground"
            }`}
          >
            Canvas
          </button>
          <button
            onClick={() => setActiveView("runs")}
            className={`px-3 py-1 rounded-md font-medium transition-all flex items-center gap-1.5 ${
              activeView === "runs" ? "bg-background shadow-xs text-foreground" : "text-muted-foreground hover:text-foreground"
            }`}
          >
            Runs & Logs
            {activeRun && activeRun.status < 2 && (
              <span className="w-2 h-2 rounded-full bg-blue-500 animate-pulse" />
            )}
          </button>
          <button
            onClick={() => setActiveView("triggers")}
            className={`px-3 py-1 rounded-md font-medium transition-all flex items-center gap-1.5 ${
              activeView === "triggers" ? "bg-background shadow-xs text-foreground" : "text-muted-foreground hover:text-foreground"
            }`}
          >
            Triggers & Schedules
            {triggerList.length > 0 && (
              <span className="text-[10px] px-1 py-0.2 rounded bg-muted-foreground/15 font-mono">{triggerList.length}</span>
            )}
          </button>
          <button
            onClick={() => setActiveView("versions")}
            className={`px-3 py-1 rounded-md font-medium transition-all ${
              activeView === "versions" ? "bg-background shadow-xs text-foreground" : "text-muted-foreground hover:text-foreground"
            }`}
          >
            Versions
          </button>
        </div>

        {/* Right: Actions */}
        <div className="flex items-center gap-2">
          {msg && <span className="text-xs text-emerald-600 font-medium px-2">{msg}</span>}
          {err && <span className="text-xs text-destructive font-medium px-2">{err}</span>}
          <Button
            size="sm"
            variant={dirty ? "default" : "outline"}
            className="h-8 text-xs font-medium gap-1.5 min-w-[68px]"
            onClick={save}
            disabled={isSaving}
          >
            {isSaving ? (
              <>
                <Loader2 className="h-3.5 w-3.5 animate-spin" />
                Saving...
              </>
            ) : (
              "Save"
            )}
          </Button>
          <Button
            size="sm"
            variant="outline"
            className="h-8 text-xs font-medium gap-1.5 border-blue-500/30 hover:border-blue-500/60 hover:bg-blue-500/5 text-foreground"
            onClick={() => setAiDialogOpen(true)}
            title="Generate or extend workflow using AI (Ctrl+K)"
          >
            <Sparkles className="h-3.5 w-3.5 text-blue-500" />
            AI Assistant
          </Button>
          <Button
            size="sm"
            variant="outline"
            className="h-8 text-xs font-medium gap-1.5 min-w-[88px]"
            onClick={handleRunTest}
            disabled={isRunningTest}
          >
            {isRunningTest ? (
              <>
                <Loader2 className="h-3.5 w-3.5 animate-spin text-emerald-600" />
                Starting...
              </>
            ) : (
              <>
                <Play className="h-3 w-3 text-emerald-600" />
                Run Test
              </>
            )}
          </Button>
          <Button
            size="sm"
            variant="default"
            className="h-8 text-xs bg-emerald-600 hover:bg-emerald-700 text-white font-medium gap-1.5 min-w-[76px]"
            onClick={handlePublish}
            disabled={isPublishing}
          >
            {isPublishing ? (
              <>
                <Loader2 className="h-3.5 w-3.5 animate-spin text-white" />
                Publishing...
              </>
            ) : (
              "Publish"
            )}
          </Button>
          <Button
            size="sm"
            variant="ghost"
            className="h-8 text-xs text-muted-foreground gap-1.5 min-w-[72px]"
            onClick={handleValidate}
            disabled={isValidating}
          >
            {isValidating ? (
              <>
                <Loader2 className="h-3.5 w-3.5 animate-spin" />
                Validating...
              </>
            ) : (
              "Validate"
            )}
          </Button>
          <Button
            size="sm"
            variant="ghost"
            className="h-8 text-xs text-destructive hover:bg-destructive/10 gap-1.5 min-w-[64px]"
            onClick={removePipeline}
            disabled={isDeleting}
          >
            {isDeleting ? (
              <>
                <Loader2 className="h-3.5 w-3.5 animate-spin text-destructive" />
                Deleting...
              </>
            ) : (
              "Delete"
            )}
          </Button>
          <Button size="sm" variant="ghost" className="h-8 text-xs text-muted-foreground" onClick={onLogout}>
            Logout
          </Button>
        </div>
      </div>

      {/* Main Workspace Body */}
      {/* 1. Canvas View */}
      <div className={activeView === "canvas" ? "flex flex-1 min-h-0 relative" : "hidden"}>
        {/* Left: Palette */}
        <div className="w-64 border-r p-3 space-y-2.5 overflow-auto bg-card/30 shrink-0 flex flex-col">
          <div className="flex items-center justify-between">
            <span className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">Components</span>
            <span className="text-[10px] font-mono px-1.5 py-0.2 rounded-md bg-muted text-muted-foreground font-medium">
              {defs.length}
            </span>
          </div>

          <div className="relative">
            <Search className="absolute left-2.5 top-2.5 h-3.5 w-3.5 text-muted-foreground pointer-events-none" />
            <Input
              className="h-8 pl-8 pr-7 text-xs bg-background/50 focus:bg-background"
              placeholder="Search nodes…"
              value={paletteQuery}
              onChange={e => setPaletteQuery(e.target.value)}
            />
            {paletteQuery && (
              <button
                type="button"
                onClick={() => setPaletteQuery("")}
                className="absolute right-2 top-2 p-0.5 rounded text-muted-foreground hover:text-foreground hover:bg-muted"
                title="Clear search"
              >
                <X className="h-3 w-3" />
              </button>
            )}
          </div>

          <div className="space-y-3 flex-1 overflow-auto">
            {paletteGroups.map(([cat, items]) => (
              <div key={cat} className="space-y-1.5">
                <div className="flex items-center justify-between px-0.5 pt-1">
                  <div className="flex items-center gap-1.5 text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">
                    {getCategoryIcon(cat)}
                    <span>{cat}</span>
                  </div>
                  <span className="text-[10px] font-mono text-muted-foreground/70">
                    {items.length}
                  </span>
                </div>
                <div className="space-y-1">
                  {items.map(d => (
                    <div
                      key={d.type}
                      draggable
                      onDragStart={e => e.dataTransfer.setData("application/reflow-nodetype", d.type)}
                      title={d.description}
                      className="group relative flex items-center justify-between p-2 rounded-lg border border-border/50 bg-card hover:bg-accent/40 hover:border-foreground/25 hover:shadow-2xs transition-all cursor-grab active:cursor-grabbing"
                    >
                      <div className="flex items-center gap-2 min-w-0">
                        <div className="w-6 h-6 rounded bg-muted/60 border border-border/40 flex items-center justify-center text-muted-foreground group-hover:text-foreground group-hover:bg-muted shrink-0 transition-colors">
                          {getNodeIcon(d.type, d.category)}
                        </div>
                        <div className="min-w-0">
                          <div className="text-xs font-medium text-foreground truncate">{d.displayName}</div>
                          <div className="text-[10px] font-mono text-muted-foreground truncate">{d.type}</div>
                        </div>
                      </div>
                      <GripVertical className="h-3.5 w-3.5 text-muted-foreground/30 group-hover:text-muted-foreground/70 transition-colors shrink-0" />
                    </div>
                  ))}
                </div>
              </div>
            ))}
            {paletteGroups.length === 0 && (
              <div className="p-4 text-center text-xs text-muted-foreground space-y-1">
                <p>No components match.</p>
                <button
                  type="button"
                  onClick={() => setPaletteQuery("")}
                  className="text-[11px] text-primary hover:underline"
                >
                  Clear search
                </button>
              </div>
            )}
          </div>

          <div className="pt-2 border-t space-y-1 shrink-0">
            <Button
              size="sm"
              variant="outline"
              className="w-full text-xs h-7 text-muted-foreground hover:text-foreground justify-center gap-1.5"
              onClick={autoLayout}
            >
              <LayoutGrid className="h-3.5 w-3.5" />
              Auto-layout Canvas
            </Button>
            {selectedEdgeId && (
              <Button
                size="sm"
                variant="outline"
                className="w-full text-destructive hover:bg-destructive/10 text-xs h-7 justify-center gap-1.5"
                onClick={() => {
                  setEdges(es => es.filter(e => e.id !== selectedEdgeId))
                  setSelectedEdgeId(null)
                }}
              >
                <Trash2 className="h-3.5 w-3.5" />
                Delete Selected Edge
              </Button>
            )}
            <p className="text-[10px] text-center text-muted-foreground/60 pt-0.5">
              Drag components onto canvas
            </p>
          </div>
        </div>

        {/* Center: ReactFlow Canvas */}
        <div className="flex-1 min-w-0 h-full relative" onDrop={onDrop} onDragOver={e => e.preventDefault()}>
          <ReactFlow
            nodes={nodes} edges={edges}
            onNodesChange={onNodesChange} onEdgesChange={onEdgesChange} onConnect={onConnect}
            onNodeClick={(_, n) => {
              setSelectedId(n.id)
              setSelectedEdgeId(null)
              const matchingTask = tasks.find(t => t.nodeId === n.id)
              if (matchingTask) {
                setTaskDetailId(matchingTask.id)
                if (matchingTask.status === 3) {
                  setConsoleTab("output")
                }
              }
            }}
            onEdgeClick={(_, e) => { setSelectedEdgeId(e.id); setSelectedId(null) }}
            onPaneClick={() => { setSelectedId(null); setSelectedEdgeId(null) }}
            nodeTypes={nodeTypesMap} fitView>
            <Background />
            <Controls />
            <MiniMap pannable zoomable />
          </ReactFlow>

          {/* Docked Execution Console Drawer */}
          <div className={`absolute bottom-0 left-0 right-0 z-20 border-t bg-card/95 backdrop-blur-sm transition-all duration-200 shadow-xl flex flex-col ${
            consoleOpen ? "h-72" : "h-9"
          }`}>
            {/* Console Header Bar */}
            <div
              className="h-9 px-3 flex items-center justify-between border-b bg-muted/40 cursor-pointer select-none text-xs"
              onClick={() => setConsoleOpen(!consoleOpen)}
            >
              <div className="flex items-center gap-3">
                <div className="flex items-center gap-1.5 font-semibold text-foreground">
                  <Terminal className="h-3.5 w-3.5 text-muted-foreground" />
                  <span>Execution Console</span>
                </div>

                {activeRun && (
                  <div className="flex items-center gap-2 font-mono text-[11px]">
                    <span className={`px-2 py-0.5 rounded-full font-medium ${
                      activeRun.status === 2 ? "bg-blue-100 text-blue-700 dark:bg-blue-950 dark:text-blue-400 animate-pulse" :
                      activeRun.status === 3 ? "bg-emerald-100 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-400" :
                      activeRun.status === 4 ? "bg-rose-100 text-rose-700 dark:bg-rose-950 dark:text-rose-400" : "bg-muted text-muted-foreground"
                    }`}>
                      {RUN_STATUSES[activeRun.status]}
                    </span>
                    {activeRun.status < 2 ? (
                      <span className="text-blue-600 font-mono font-medium">
                        {Math.max(0, Math.round((now - parseUtcDate(activeRun.startedAt ?? activeRun.createdAt)) / 1000))}s
                      </span>
                    ) : (
                      <span className="text-muted-foreground">v{activeRun.versionNumber}</span>
                    )}
                  </div>
                )}

                {consoleOpen && (
                  <div className="flex items-center gap-1 ml-2" onClick={e => e.stopPropagation()}>
                    <button
                      onClick={() => setConsoleTab("logs")}
                      className={`px-2 py-0.5 rounded text-[11px] font-medium transition-colors ${
                        consoleTab === "logs" ? "bg-background shadow-xs text-foreground font-semibold" : "text-muted-foreground hover:text-foreground"
                      }`}
                    >
                      Terminal Logs ({logs.length})
                    </button>
                    <button
                      onClick={() => setConsoleTab("output")}
                      className={`px-2 py-0.5 rounded text-[11px] font-medium transition-colors ${
                        consoleTab === "output" ? "bg-background shadow-xs text-foreground font-semibold" : "text-muted-foreground hover:text-foreground"
                      }`}
                    >
                      Output Table {taskDetail ? `(${taskDetail.nodeId})` : ""}
                    </button>
                    <button
                      onClick={() => setConsoleTab("tasks")}
                      className={`px-2 py-0.5 rounded text-[11px] font-medium transition-colors ${
                        consoleTab === "tasks" ? "bg-background shadow-xs text-foreground font-semibold" : "text-muted-foreground hover:text-foreground"
                      }`}
                    >
                      Nodes ({tasks.length})
                    </button>
                  </div>
                )}
              </div>

              <div className="flex items-center gap-2" onClick={e => e.stopPropagation()}>
                {activeRun && activeRun.status < 2 && (
                  <Button
                    size="sm"
                    variant="ghost"
                    className="h-6 text-[10px] text-destructive hover:bg-destructive/10 gap-1"
                    onClick={() => handleCancelRun(activeRun.id)}
                    disabled={isCancellingRun}
                  >
                    {isCancellingRun && <Loader2 className="h-3 w-3 animate-spin" />}
                    Cancel Run
                  </Button>
                )}
                <Button
                  size="sm"
                  variant="ghost"
                  className="h-6 w-6 p-0 text-muted-foreground hover:text-foreground"
                  onClick={() => setConsoleOpen(!consoleOpen)}
                  title={consoleOpen ? "Collapse Console" : "Expand Console"}
                >
                  {consoleOpen ? <ChevronDown className="h-4 w-4" /> : <ChevronUp className="h-4 w-4" />}
                </Button>
              </div>
            </div>

            {/* Console Content when Expanded */}
            {consoleOpen && (
              <div className="flex-1 min-h-0 overflow-auto bg-card">
                {consoleTab === "logs" && (
                  <div className="h-full bg-zinc-950 p-3 font-mono text-xs text-zinc-200 overflow-auto space-y-1">
                    {logs.map((l, i) => {
                      const levelColor =
                        l.level === "Error" ? "text-rose-400 font-bold" :
                        l.level === "Warning" ? "text-amber-400 font-bold" : "text-emerald-400"
                      return (
                        <div key={i} className="flex items-start gap-2 leading-relaxed">
                          <span className="text-zinc-500 shrink-0">
                            {l.timestamp ? new Date(parseUtcDate(l.timestamp)).toLocaleTimeString() : "--:--:--"}
                          </span>
                          <span className={`shrink-0 ${levelColor}`}>[{l.level.toUpperCase()}]</span>
                          <span className="break-all">{l.message}</span>
                        </div>
                      )
                    })}
                    {logs.length === 0 && (
                      <p className="text-zinc-500 italic py-4 text-center">
                        {activeRun ? "Streaming execution logs over WebSockets..." : "No logs available. Click 'Run Test' above to execute."}
                      </p>
                    )}
                  </div>
                )}

                {consoleTab === "output" && (
                  <div className="h-full p-3 overflow-auto">
                    {taskDetail && outputPreview ? (
                      <div className="space-y-2">
                        <div className="flex items-center justify-between text-xs">
                          <span className="font-semibold text-foreground">
                            Previewing node: <code className="font-mono text-primary">{taskDetail.nodeId}</code>
                          </span>
                          <span className="text-muted-foreground font-mono text-[11px]">
                            Showing {outputPreview.rows.length} of {outputPreview.total} rows
                          </span>
                        </div>
                        <div className="border rounded overflow-auto max-h-52">
                          <table className="text-xs font-mono w-full border-collapse">
                            <thead className="bg-muted/60 sticky top-0">
                              <tr>
                                {outputPreview.cols.map(c => (
                                  <th key={c} className="text-left px-2.5 py-1.5 border-b font-semibold text-foreground">
                                    {c}
                                  </th>
                                ))}
                              </tr>
                            </thead>
                            <tbody>
                              {outputPreview.rows.map((r, i) => (
                                <tr key={i} className="odd:bg-muted/15 hover:bg-muted/30 transition-colors">
                                  {outputPreview.cols.map(c => (
                                    <td key={c} className="px-2.5 py-1 border-b text-muted-foreground truncate max-w-xs">
                                      {r[c] ?? "∅"}
                                    </td>
                                  ))}
                                </tr>
                              ))}
                            </tbody>
                          </table>
                        </div>
                      </div>
                    ) : (
                      <div className="h-full flex flex-col items-center justify-center text-xs text-muted-foreground py-8">
                        <Table className="h-8 w-8 text-muted-foreground/40 mb-2" />
                        <p className="font-medium">No node output selected</p>
                        <p className="text-[11px] text-muted-foreground mt-0.5">Click any node on the canvas to inspect its output table rows.</p>
                      </div>
                    )}
                  </div>
                )}

                {consoleTab === "tasks" && (
                  <div className="p-3 grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-2 overflow-auto max-h-60">
                    {tasks.map(t => {
                      const isSelected = t.id === taskDetailId
                      return (
                        <div
                          key={t.id}
                          onClick={() => {
                            setTaskDetailId(t.id)
                            setConsoleTab("output")
                          }}
                          className={`p-2.5 rounded-lg border cursor-pointer transition-all space-y-1 ${
                            isSelected ? "border-primary bg-accent/60" : "bg-card hover:border-border"
                          }`}
                        >
                          <div className="flex items-center justify-between">
                            <span className="font-mono font-medium text-xs text-foreground truncate">{t.nodeId}</span>
                            <span className={`text-[10px] px-1.5 py-0.5 rounded font-medium ${
                              t.status === 3 ? "bg-emerald-100 text-emerald-700" :
                              t.status === 4 ? "bg-rose-100 text-rose-700" :
                              t.status === 2 ? "bg-blue-100 text-blue-700 animate-pulse" : "bg-muted text-muted-foreground"
                            }`}>
                              {TASK_STATUSES[t.status]}
                            </span>
                          </div>
                          <div className="flex items-center justify-between text-[11px] text-muted-foreground font-mono">
                            <span>{t.nodeType}</span>
                            {t.nodeType === "data.output" && t.status === 3 && runId && (
                              <a
                                onClick={e => e.stopPropagation()}
                                href={artifactUrl(runId, t.nodeId)}
                                download
                                className="text-emerald-600 hover:underline"
                              >
                                download
                              </a>
                            )}
                          </div>
                          {t.error && <p className="text-[10px] text-destructive truncate">{t.error}</p>}
                        </div>
                      )
                    })}
                    {tasks.length === 0 && (
                      <p className="text-xs text-muted-foreground col-span-full py-6 text-center">
                        No tasks executed yet.
                      </p>
                    )}
                  </div>
                )}
              </div>
            )}
          </div>
        </div>

        {/* Right: Selected Node Configuration Inspector */}
        <div className="w-80 border-l overflow-auto bg-card/20 shrink-0">
          {selected ? (
            <div className="p-3.5 space-y-3.5">
              <div className="flex items-start justify-between gap-2 border-b pb-3">
                <div className="min-w-0">
                  <p className="text-sm font-semibold truncate">{(selected.data as unknown as FlowNodeData).label}</p>
                  <p className="text-[10px] font-mono text-muted-foreground truncate">node: {selected.id}</p>
                </div>
                <span className="text-[10px] px-2 py-0.5 rounded font-mono font-medium bg-muted text-muted-foreground border border-border/50 shrink-0">
                  {(selected.data as unknown as FlowNodeData).nodeType}
                </span>
              </div>

              <div className="space-y-1">
                <label className="text-xs font-semibold text-foreground">Table Name (for SQL queries)</label>
                <Input
                  className="font-mono text-xs h-8"
                  placeholder="e.g. orders, age, users"
                  value={nodeIdInput}
                  onChange={e => setNodeIdInput(e.target.value)}
                  onBlur={commitNodeIdChange}
                  onKeyDown={e => { if (e.key === "Enter") commitNodeIdChange() }}
                />
                <p className="text-[10px] text-muted-foreground">
                  Reference this node in SQL as: <code className="bg-muted px-1 py-0.5 rounded font-mono font-semibold">{nodeIdInput.trim() || selected.id}</code>
                </p>
              </div>

              <div className="space-y-1">
                <label className="text-xs font-semibold text-foreground">Display Label</label>
                <Input
                  className="h-8 text-xs"
                  placeholder="Display name on canvas"
                  value={(selected.data as unknown as FlowNodeData).label}
                  onChange={e => patchSelected({ label: e.target.value })}
                />
              </div>

              <div className="space-y-1 pt-1 border-t">
                <label className="text-xs font-semibold text-foreground">Node Configuration</label>
                <NodeConfigForm
                  nodeType={(selected.data as unknown as FlowNodeData).nodeType}
                  configJson={(selected.data as unknown as FlowNodeData).configJson}
                  onChange={c => patchSelected({ configJson: c })}
                  upstreamNodes={upstreamNodes}
                />
              </div>

              <div className="pt-3 border-t">
                <Button size="sm" variant="ghost" className="w-full text-destructive hover:bg-destructive/10 text-xs h-8"
                  onClick={() => {
                    setNodes(ns => ns.filter(n => n.id !== selectedId))
                    setEdges(es => es.filter(e => e.source !== selectedId && e.target !== selectedId))
                    setSelectedId(null)
                  }}>
                  Delete Node
                </Button>
              </div>
            </div>
          ) : (
            <div className="p-6 text-center text-xs text-muted-foreground space-y-2">
              <p className="font-medium text-foreground">No node selected</p>
              <p>Click any node on the canvas to configure its table name and parameters.</p>
              <p className="pt-2 text-[11px] opacity-75">Drag components from the left palette to add nodes.</p>
            </div>
          )}
        </div>
      </div>

      {/* 2. Runs & Logs View */}
      {activeView === "runs" && (
        <div className="flex-1 flex min-h-0 overflow-hidden bg-background">
          {/* Left Runs List */}
          <div className="w-64 border-r flex flex-col bg-card/30 shrink-0">
            <div className="p-3 border-b flex items-center justify-between">
              <span className="text-xs font-semibold text-foreground uppercase tracking-wide">Run History</span>
              <span className="text-[11px] font-mono text-muted-foreground">{runList.length} total</span>
            </div>
            <div className="flex-1 overflow-auto p-2 space-y-1">
              {runList.map(r => {
                const isSelected = r.id === runId
                return (
                  <button
                    key={r.id}
                    onClick={() => { setRunId(r.id); selectTask(null) }}
                    className={`w-full text-left p-2.5 rounded-lg border transition-all text-xs space-y-1 ${
                      isSelected
                        ? "bg-accent border-primary/50 shadow-2xs"
                        : "hover:bg-muted/40 border-transparent hover:border-border"
                    }`}
                  >
                    <div className="flex items-center justify-between">
                      <span className="font-semibold font-mono text-foreground">v{r.versionNumber}</span>
                      <span className={`text-[10px] px-1.5 py-0.5 rounded-full font-medium ${
                        r.status === 2 ? "bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400" :
                        r.status === 3 ? "bg-rose-100 text-rose-700 dark:bg-rose-950/40 dark:text-rose-400" :
                        r.status === 1 ? "bg-blue-100 text-blue-700 dark:bg-blue-950/40 dark:text-blue-400 animate-pulse" :
                        "bg-muted text-muted-foreground"
                      }`}>
                        {RUN_STATUSES[r.status]}
                      </span>
                    </div>
                    <div className="flex items-center justify-between text-[11px] text-muted-foreground">
                      <span className="capitalize">{r.triggerKind}</span>
                      {r.status < 2 ? (
                        <span className="text-blue-600 font-mono">
                          {Math.max(0, Math.round((now - parseUtcDate(r.startedAt ?? r.createdAt)) / 1000))}s
                        </span>
                      ) : (
                        <span>{new Date(parseUtcDate(r.createdAt)).toLocaleTimeString()}</span>
                      )}
                    </div>
                  </button>
                )
              })}
              {runList.length === 0 && (
                <p className="text-xs text-muted-foreground text-center py-8">
                  No runs yet. Click <span className="font-semibold text-foreground">Run Test</span> in the header to run this pipeline.
                </p>
              )}
            </div>
          </div>

          {/* Right Run Details & Console */}
          <div className="flex-1 flex flex-col min-w-0 overflow-auto">
            {activeRun ? (
              <div className="p-4 space-y-4 max-w-5xl w-full">
                {/* Run Header Banner */}
                <div className="flex items-center justify-between p-3 rounded-lg border bg-card">
                  <div className="space-y-0.5">
                    <div className="flex items-center gap-2">
                      <h3 className="text-sm font-semibold">Run v{activeRun.versionNumber}</h3>
                      <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${
                        activeRun.status === 2 ? "bg-emerald-100 text-emerald-700" :
                        activeRun.status === 3 ? "bg-rose-100 text-rose-700" :
                        activeRun.status === 1 ? "bg-blue-100 text-blue-700" : "bg-muted text-muted-foreground"
                      }`}>
                        {RUN_STATUSES[activeRun.status]}
                      </span>
                      <span className="text-xs text-muted-foreground font-mono">({activeRun.triggerKind} trigger)</span>
                    </div>
                    <p className="text-xs text-muted-foreground">
                      Started: {new Date(parseUtcDate(activeRun.startedAt ?? activeRun.createdAt)).toLocaleString()}
                    </p>
                  </div>

                  <div className="flex items-center gap-2">
                    {activeRun.status < 2 && (
                      <Button
                        size="sm"
                        variant="outline"
                        className="text-destructive h-8 text-xs gap-1.5"
                        onClick={() => handleCancelRun(activeRun.id)}
                        disabled={isCancellingRun}
                      >
                        {isCancellingRun ? (
                          <>
                            <Loader2 className="h-3.5 w-3.5 animate-spin" />
                            Cancelling...
                          </>
                        ) : (
                          "Cancel Run"
                        )}
                      </Button>
                    )}
                  </div>
                </div>

                {activeRun.error && (
                  <div className="p-3 rounded-lg bg-destructive/10 border border-destructive/20 text-destructive text-xs font-mono">
                    {activeRun.error}
                  </div>
                )}

                {/* Tasks Node Cards */}
                <div className="space-y-2">
                  <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">Executed Nodes</p>
                  <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-2">
                    {tasks.map(t => {
                      const isSelected = t.id === taskDetailId
                      return (
                        <div
                          key={t.id}
                          onClick={() => selectTask(t.id)}
                          className={`p-3 rounded-lg border cursor-pointer transition-all space-y-1.5 ${
                            isSelected ? "border-primary bg-accent/60 shadow-xs" : "bg-card hover:border-border"
                          }`}
                        >
                          <div className="flex items-center justify-between">
                            <span className="font-mono font-medium text-xs text-foreground truncate">{t.nodeId}</span>
                            <span className={`text-[10px] px-1.5 py-0.5 rounded font-medium ${
                              t.status === 3 ? "bg-emerald-100 text-emerald-700" :
                              t.status === 4 ? "bg-rose-100 text-rose-700" :
                              t.status === 2 ? "bg-blue-100 text-blue-700 animate-pulse" : "bg-muted text-muted-foreground"
                            }`}>
                              {TASK_STATUSES[t.status]}
                            </span>
                          </div>
                          <div className="flex items-center justify-between text-[11px] text-muted-foreground">
                            <span className="font-mono text-[10px]">{t.nodeType}</span>
                            <div className="flex items-center gap-1.5">
                              {t.status === 4 && (
                                <button
                                  onClick={(e) => { e.stopPropagation(); runs.retryTask(t.id).then(reload).catch(fail) }}
                                  className="text-primary hover:underline"
                                >
                                  retry
                                </button>
                              )}
                              {t.nodeType === "data.output" && t.status === 3 && runId && (
                                <a
                                  onClick={e => e.stopPropagation()}
                                  href={artifactUrl(runId, t.nodeId)}
                                  download
                                  className="text-emerald-600 hover:underline"
                                >
                                  download
                                </a>
                              )}
                            </div>
                          </div>
                          {t.error && <p className="text-[11px] text-destructive truncate">{t.error}</p>}
                        </div>
                      )
                    })}
                  </div>
                </div>

                {/* Output Preview Table (if task selected) */}
                {taskDetail && (
                  <div className="space-y-3 border rounded-lg bg-card p-3">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-2">
                        <h4 className="text-xs font-semibold text-foreground">
                          Output Preview: <code className="font-mono text-primary">{taskDetail.nodeId}</code>
                        </h4>
                        {attempts.length > 1 && (
                          <span className="text-[10px] px-1.5 py-0.5 rounded bg-muted text-muted-foreground font-mono">
                            {attempts.length} attempts
                          </span>
                        )}
                      </div>
                      {outputPreview && (
                        <span className="text-[11px] text-muted-foreground font-mono">
                          Showing {outputPreview.rows.length} of {outputPreview.total} rows
                        </span>
                      )}
                    </div>

                    {attempts.length > 1 && (
                      <div className="border rounded p-2 bg-muted/20 space-y-1">
                        <p className="text-[10px] font-semibold text-muted-foreground uppercase">Retry History</p>
                        <div className="flex flex-wrap gap-2">
                          {attempts.map(a => (
                            <span key={a.attemptNumber} className="text-[11px] font-mono px-2 py-0.5 rounded border bg-card flex items-center gap-1.5">
                              <span>#{a.attemptNumber}</span>
                              <span className={a.status === 3 ? "text-emerald-600" : "text-rose-600"}>
                                {TASK_STATUSES[a.status] ?? a.status}
                              </span>
                              {a.error && <span className="text-destructive truncate max-w-xs">({a.error})</span>}
                            </span>
                          ))}
                        </div>
                      </div>
                    )}
                    {outputPreview ? (
                      <div className="overflow-auto max-h-60 border rounded">
                        <table className="text-xs font-mono w-full border-collapse">
                          <thead className="bg-muted/60 sticky top-0">
                            <tr>
                              {outputPreview.cols.map(c => (
                                <th key={c} className="text-left px-2.5 py-1.5 border-b font-semibold text-foreground">
                                  {c}
                                </th>
                              ))}
                            </tr>
                          </thead>
                          <tbody>
                            {outputPreview.rows.map((r, i) => (
                              <tr key={i} className="odd:bg-muted/15 hover:bg-muted/30 transition-colors">
                                {outputPreview.cols.map(c => (
                                  <td key={c} className="px-2.5 py-1 border-b text-muted-foreground truncate max-w-xs">
                                    {r[c] ?? "∅"}
                                  </td>
                                ))}
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                    ) : (
                      <p className="text-xs text-muted-foreground py-2">No output rows for this task.</p>
                    )}
                  </div>
                )}

                {/* Console Log Terminal */}
                <div className="space-y-1.5">
                  <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">Live Execution Logs</p>
                  <div className="rounded-lg border bg-zinc-950 p-3 font-mono text-xs text-zinc-200 max-h-56 overflow-auto space-y-1">
                    {logs.map((l, i) => {
                      const levelColor =
                        l.level === "Error" ? "text-rose-400 font-bold" :
                        l.level === "Warning" ? "text-amber-400 font-bold" : "text-emerald-400"
                      return (
                        <div key={i} className="flex items-start gap-2 leading-relaxed">
                          <span className="text-zinc-500 shrink-0">
                            {l.timestamp ? new Date(parseUtcDate(l.timestamp)).toLocaleTimeString() : "--:--:--"}
                          </span>
                          <span className={`shrink-0 ${levelColor}`}>[{l.level.toUpperCase()}]</span>
                          <span className="break-all">{l.message}</span>
                        </div>
                      )
                    })}
                    {logs.length === 0 && <p className="text-zinc-500 italic">Waiting for log output...</p>}
                  </div>
                </div>
              </div>
            ) : (
              <div className="flex-1 flex items-center justify-center text-xs text-muted-foreground">
                Select a run from the left history to view its timeline and logs.
              </div>
            )}
          </div>
        </div>
      )}

      {/* 3. Triggers & Schedules View */}
      {activeView === "triggers" && (
        <div className="flex-1 overflow-auto p-6 max-w-5xl mx-auto w-full space-y-6">
          <div className="flex items-start justify-between">
            <div>
              <h2 className="text-base font-semibold text-foreground">Triggers & Schedules</h2>
              <p className="text-xs text-muted-foreground mt-0.5">
                Configure automated cron schedules and external webhook endpoints to execute this pipeline.
              </p>
            </div>
            <div className="flex items-center gap-2">
              <Button size="sm" variant="outline" className="h-8 text-xs gap-1" onClick={() => setScheduleDialogOpen(true)}>
                + Add Schedule
              </Button>
              <Button size="sm" variant="outline" className="h-8 text-xs gap-1" onClick={() => setWebhookDialogOpen(true)}>
                + Add Webhook
              </Button>
            </div>
          </div>

          {/* Schedules Section */}
          <div className="space-y-3">
            <h3 className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">Recurring Schedules</h3>
            <div className="border rounded-lg bg-card overflow-hidden">
              <table className="w-full text-xs">
                <thead className="bg-muted/40 border-b text-muted-foreground">
                  <tr>
                    <th className="text-left px-3 py-2 w-16">Active</th>
                    <th className="text-left px-3 py-2">Name</th>
                    <th className="text-left px-3 py-2">Schedule</th>
                    <th className="text-left px-3 py-2">Timezone</th>
                    <th className="text-left px-3 py-2">Concurrency</th>
                    <th className="text-right px-3 py-2 w-20">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {triggerList.filter(t => t.kind === 0).map(t => (
                    <tr key={t.id} className="hover:bg-muted/20 transition-colors">
                      <td className="px-3 py-2.5">
                        <button
                          onClick={async () => {
                            try {
                              await triggers.updateSchedule(pipelineId, t.id, { isEnabled: !t.isEnabled })
                              reload()
                            } catch (e) { fail(e) }
                          }}
                          className={`w-4 h-4 rounded-full border flex items-center justify-center transition-colors ${
                            t.isEnabled ? "bg-emerald-600 border-emerald-600 text-white" : "border-muted-foreground/40 bg-transparent"
                          }`}
                          title={t.isEnabled ? "Enabled (click to pause)" : "Paused (click to activate)"}
                        >
                          {t.isEnabled && <Check className="h-2.5 w-2.5 text-white" />}
                        </button>
                      </td>
                      <td className="px-3 py-2.5 font-medium text-foreground">{t.name}</td>
                      <td className="px-3 py-2.5 text-foreground font-medium">
                        {t.cron ? cronToHuman(t.cron, t.timezone) : "--"}
                      </td>
                      <td className="px-3 py-2.5 font-mono text-[11px] text-muted-foreground">{t.timezone || "UTC"}</td>
                      <td className="px-3 py-2.5 text-[11px] text-muted-foreground">
                        {t.overlap === 0 ? "Skip if active" : "Queue next"}
                      </td>
                      <td className="px-3 py-2.5 text-right">
                        <button
                          className="text-muted-foreground hover:text-destructive text-xs px-1.5 py-0.5 rounded"
                          onClick={() => triggers.remove(pipelineId, t.id).then(reload).catch(fail)}
                          title="Delete schedule"
                        >
                          Delete
                        </button>
                      </td>
                    </tr>
                  ))}
                  {triggerList.filter(t => t.kind === 0).length === 0 && (
                    <tr>
                      <td colSpan={6} className="text-center py-6 text-muted-foreground">
                        No automated schedules created yet. Click <strong>+ Add Schedule</strong> to set one up.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>

          {/* Webhooks Section */}
          <div className="space-y-3">
            <h3 className="text-xs font-semibold text-muted-foreground uppercase tracking-wide">Inbound Webhooks</h3>
            <div className="border rounded-lg bg-card overflow-hidden">
              <table className="w-full text-xs">
                <thead className="bg-muted/40 border-b text-muted-foreground">
                  <tr>
                    <th className="text-left px-3 py-2">Webhook Name</th>
                    <th className="text-left px-3 py-2">Endpoint URL</th>
                    <th className="text-right px-3 py-2 w-32">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {triggerList.filter(t => t.kind === 1).map(t => (
                    <tr key={t.id} className="hover:bg-muted/20 transition-colors">
                      <td className="px-3 py-2.5 font-medium text-foreground">{t.name}</td>
                      <td className="px-3 py-2.5 font-mono text-[11px] text-muted-foreground">
                        {((import.meta.env.VITE_API_URL as string | undefined)?.replace(/\/$/, '') || window.location.origin)}/api/v1/hooks/••••••••
                      </td>
                      <td className="px-3 py-2.5 text-right space-x-2">
                        <button
                          className="text-primary hover:underline text-xs"
                          onClick={async () => {
                            try {
                              const r = await triggers.regenerateWebhook(pipelineId, t.id)
                              const origin = (import.meta.env.VITE_API_URL as string | undefined)?.replace(/\/$/, '') || window.location.origin
                              navigator.clipboard.writeText(`${origin}/api/v1/hooks/${r.token}`)
                              say("New Webhook URL copied to clipboard!")
                            } catch (e) { fail(e) }
                          }}
                        >
                          Copy URL
                        </button>
                        <button
                          className="text-muted-foreground hover:text-destructive text-xs"
                          onClick={() => triggers.remove(pipelineId, t.id).then(reload).catch(fail)}
                        >
                          Delete
                        </button>
                      </td>
                    </tr>
                  ))}
                  {triggerList.filter(t => t.kind === 1).length === 0 && (
                    <tr>
                      <td colSpan={3} className="text-center py-6 text-muted-foreground">
                        No webhooks generated yet. Click <strong>+ Add Webhook</strong> to create an endpoint.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}

      {/* 4. Versions View */}
      {activeView === "versions" && (
        <div className="flex-1 overflow-auto p-6 max-w-4xl mx-auto w-full space-y-4">
          <div>
            <h2 className="text-base font-semibold text-foreground">Published Versions</h2>
            <p className="text-xs text-muted-foreground mt-0.5">
              Each published version is an immutable snapshot used for scheduled runs and execution.
            </p>
          </div>

          <div className="border rounded-lg bg-card overflow-hidden">
            <div className="divide-y">
              {versionList.map(v => {
                const isSelected = versionDetail?.versionNumber === v.versionNumber
                return (
                  <div key={v.versionNumber} className="p-3 hover:bg-muted/20 transition-colors space-y-2">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-2">
                        <span className="font-mono font-semibold text-xs text-foreground bg-primary/10 text-primary px-2 py-0.5 rounded">
                          v{v.versionNumber}
                        </span>
                        <span className="text-xs text-muted-foreground">
                          Published: {new Date(parseUtcDate(v.publishedAt)).toLocaleString()}
                        </span>
                      </div>
                      <Button
                        size="sm"
                        variant="outline"
                        className="h-7 text-xs"
                        onClick={() => versions.detail(pipelineId, v.versionNumber).then(setVersionDetail).catch(fail)}
                      >
                        {isSelected ? "Hide Snapshot" : "Inspect Snapshot"}
                      </Button>
                    </div>

                    {isSelected && versionDetail && (
                      <pre className="text-[11px] font-mono bg-muted/50 rounded-lg p-3 max-h-64 overflow-auto border">
                        {JSON.stringify(JSON.parse(versionDetail.definition), null, 2)}
                      </pre>
                    )}
                  </div>
                )
              })}
              {versionList.length === 0 && (
                <p className="text-xs text-muted-foreground text-center py-8">
                  No published versions yet. Click <strong>Publish</strong> in the top header to create version 1.
                </p>
              )}
            </div>
          </div>
        </div>
      )}

      <ScheduleDialog
        isOpen={scheduleDialogOpen}
        onClose={() => setScheduleDialogOpen(false)}
        onSave={async (sched) => {
          await triggers.createSchedule(pipelineId, sched)
          say("Schedule created successfully")
          reload()
        }}
      />

      <WebhookDialog
        isOpen={webhookDialogOpen}
        onClose={() => setWebhookDialogOpen(false)}
        onCreate={async (wName) => {
          const res = await triggers.createWebhook(pipelineId, { name: wName })
          reload()
          return res
        }}
      />

      <EditorAiDialog
        isOpen={aiDialogOpen}
        onClose={() => setAiDialogOpen(false)}
        existingNodesCount={nodes.length}
        defs={defs}
        onApply={handleApplyAiDraft}
      />
    </div>
  )
}
