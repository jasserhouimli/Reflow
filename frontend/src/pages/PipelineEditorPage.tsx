import { useState, useEffect, useCallback } from "react"
import {
  ReactFlow, Background, Controls, MiniMap, Handle, Position,
  applyNodeChanges, applyEdgeChanges, addEdge,
  type Node, type Edge, type NodeChange, type EdgeChange, type Connection,
  type NodeProps,
} from "@xyflow/react"
import "@xyflow/react/dist/style.css"
import {
  pipelines, runs, triggers, nodeTypes, versions, artifactUrl,
  RUN_STATUSES, TASK_STATUSES, PIPELINE_STATUSES,
  type PipelineDetail, type PipelineEdge,
  type NodeDefinition, type PipelineRun, type TaskRun, type RunLog, type TriggerItem,
  type TaskDetail, type TaskAttempt, type VersionDetail,
} from "@/api/client"
import { subscribeToRun } from "@/api/realtime"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"

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

function NodeConfigForm({ nodeType, configJson, onChange }: {
  nodeType: string
  configJson: string
  onChange: (json: string) => void
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
        <div className="space-y-2">
          <label className="text-xs">Query (tables named after source nodes)
            <textarea className="w-full text-xs font-mono border rounded p-2" rows={8}
              value={str("query")} onChange={e => set("query", e.target.value)} /></label>
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

const CATEGORY_COLORS: Record<string, string> = {
  Sources: "bg-emerald-100 text-emerald-700",
  Transform: "bg-blue-100 text-blue-700",
  Analysis: "bg-violet-100 text-violet-700",
  Sinks: "bg-amber-100 text-amber-700",
}

function PipelineNodeView({ data, selected }: NodeProps) {
  const d = data as unknown as FlowNodeData
  const color = d.status === 3 ? "ring-green-400" : d.status === 4 ? "ring-red-500" : d.status === 2 ? "ring-blue-400" : ""
  return (
    <div className={`rounded-md border bg-card px-3 py-2 shadow-sm min-w-36 ${selected ? "ring-2 ring-primary" : color ? `ring-2 ${color}` : ""}`}>
      <Handle type="target" position={Position.Left} />
      <div className="text-xs font-medium">{d.label}</div>
      <div className="text-[10px] px-1.5 py-0.5 rounded bg-blue-100 text-blue-700 inline-block mt-1">{d.nodeType}</div>
      {d.status !== undefined && d.status !== 0 && d.status !== 1 && (
        <div className="text-[10px] mt-1 text-muted-foreground">{TASK_STATUSES[d.status]}</div>
      )}
      <Handle type="source" position={Position.Right} />
    </div>
  )
}

const nodeTypesMap = { pipelineNode: PipelineNodeView }

const toFlow = (d: PipelineDetail): Node[] =>
  d.nodes.map(n => ({
    id: n.nodeId,
    type: "pipelineNode",
    position: { x: n.positionX, y: n.positionY },
    data: { label: n.label || n.nodeId, nodeType: n.nodeType, configJson: n.configJson },
  }))

const toFlowEdges = (edges: PipelineEdge[]): Edge[] =>
  edges.map((e, i) => ({ id: `e${i}-${e.sourceNodeId}-${e.targetNodeId}`, source: e.sourceNodeId, target: e.targetNodeId }))

const snapshot = (name: string, nodes: Node[], edges: Edge[]) =>
  JSON.stringify({
    name,
    nodes: nodes.map(n => ({
      id: n.id,
      position: n.position,
      data: { label: (n.data as unknown as FlowNodeData).label, nodeType: (n.data as unknown as FlowNodeData).nodeType, configJson: (n.data as unknown as FlowNodeData).configJson },
    })),
    edges,
  })

export function PipelineEditorPage({ pipelineId, onBack, onLogout }: Props) {
  const [detail, setDetail] = useState<PipelineDetail | null>(null)
  const [defs, setDefs] = useState<NodeDefinition[]>([])
  const [name, setName] = useState("")
  const [nodes, setNodes] = useState<Node[]>([])
  const [edges, setEdges] = useState<Edge[]>([])
  const [savedSnap, setSavedSnap] = useState("")
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [selectedEdgeId, setSelectedEdgeId] = useState<string | null>(null)
  const [paletteQuery, setPaletteQuery] = useState("")
  const [now, setNow] = useState(() => Date.now())
  const [msg, setMsg] = useState("")
  const [err, setErr] = useState("")

  const [runList, setRunList] = useState<PipelineRun[]>([])
  const [runId, setRunId] = useState<string | null>(null)
  const [tasks, setTasks] = useState<TaskRun[]>([])
  const [logs, setLogs] = useState<RunLog[]>([])
  const [taskDetailId, setTaskDetailId] = useState<string | null>(null)
  const [taskDetail, setTaskDetail] = useState<TaskDetail | null>(null)
  const [attempts, setAttempts] = useState<TaskAttempt[]>([])

  const [versionList, setVersionList] = useState<Array<{ versionNumber: number; publishedAt: string }>>([])
  const [versionDetail, setVersionDetail] = useState<VersionDetail | null>(null)

  const [triggerList, setTriggerList] = useState<TriggerItem[]>([])
  const [schedName, setSchedName] = useState("")
  const [schedCron, setSchedCron] = useState("0 2 * * *")
  const [schedTz, setSchedTz] = useState("UTC")
  const [hookName, setHookName] = useState("")
  const [hookToken, setHookToken] = useState("")

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
      const n = toFlow(d); const e = toFlowEdges(d.edges)
      setNodes(n); setEdges(e)
      setSavedSnap(snapshot(d.name, n, e))
      setRunList(await runs.list(pipelineId))
      setTriggerList(await triggers.list(pipelineId))
      setVersionList(await pipelines.versions(pipelineId))
    } catch (e) { fail(e) }
  }, [pipelineId])

  useEffect(() => {
    nodeTypes.list().then(setDefs).catch(() => setDefs([]))
    reload()
  }, [reload])

  useEffect(() => {
    if (!runId) return
    let alive = true
    let finished = false
    const refresh = async () => {
      try {
        const [t, l] = await Promise.all([runs.tasks(runId), runs.logs(runId)])
        if (!alive) return
        setTasks(t); setLogs(l)
        paintStatuses(t)
        const r = await runs.get(runId)
        if (!alive) return
        setRunList(prev => prev.map(x => x.id === runId ? r : x))
        if (r.status >= 2) finished = true
      } catch { /* keep polling */ }
    }
    refresh()
    const stopSignalR = subscribeToRun(runId, () => { if (!finished) refresh() })
    const fallback = setInterval(() => { if (!finished) refresh() }, 15000)
    return () => { alive = false; finished = true; stopSignalR(); clearInterval(fallback) }
  }, [runId])

  useEffect(() => {
    if (!taskDetailId) { setTaskDetail(null); setAttempts([]); return }
    runs.taskDetail(taskDetailId).then(setTaskDetail).catch(fail)
    runs.taskAttempts(taskDetailId).then(setAttempts).catch(() => setAttempts([]))
  }, [taskDetailId])

  const onNodesChange = useCallback(
    (changes: NodeChange[]) => setNodes(ns => applyNodeChanges(changes, ns) as Node[]), [])
  const onEdgesChange = useCallback(
    (changes: EdgeChange[]) => setEdges(es => applyEdgeChanges(changes, es)), [])
  const onConnect = useCallback(
    (c: Connection) => setEdges(es => addEdge(c, es)), [])

  const onDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault()
    const type = e.dataTransfer.getData("application/reflow-nodetype")
    if (!type) return
    const bounds = (e.currentTarget as HTMLElement).getBoundingClientRect()
    const position = { x: e.clientX - bounds.left - 70, y: e.clientY - bounds.top - 30 }
    const id = `n${Date.now().toString(36)}`
    setNodes(ns => [...ns, {
      id, type: "pipelineNode", position,
      data: { label: id, nodeType: type, configJson: type === "data.sql" ? "{\"query\":\"SELECT * FROM input\"}" : "{}" },
    }])
  }, [])

  const selected = nodes.find(n => n.id === selectedId)
  const activeRun = runList.find(r => r.id === runId) ?? null
  const patchSelected = (patch: Partial<FlowNodeData>) => {
    if (!selectedId) return
    setNodes(ns => ns.map(n => n.id === selectedId ? { ...n, data: { ...n.data, ...patch } } : n))
  }

  const save = async () => {
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
      say("Saved"); reload()
    } catch (e) { fail(e) }
  }

  const removePipeline = async () => {
    if (!window.confirm(`Delete pipeline "${name}" and all its runs?`)) return
    try { await pipelines.remove(pipelineId); onBack() } catch (e) { fail(e) }
  }

  const outputPreview = (taskDetail?.outputJson ?? null) ? (() => {
    try {
      const rows = JSON.parse(taskDetail!.outputJson!) as Array<Record<string, string | null>>
      if (!Array.isArray(rows) || rows.length === 0) return null
      const cols = Object.keys(rows[0])
      return { cols, rows: rows.slice(0, 50), total: rows.length }
    } catch { return null }
  })() : null

  if (!detail) return <div className="p-8">Loading... {err && <p className="text-destructive">{err}</p>}</div>

  return (
    <div className="h-screen flex flex-col">
      <div className="flex items-center justify-between px-4 py-2 border-b">
        <div className="flex items-center gap-2">
          <Button variant="ghost" size="sm" onClick={onBack}>←</Button>
          <Input value={name} onChange={e => setName(e.target.value)} className="w-56 h-8" />
          <span className="text-xs text-muted-foreground">
            {PIPELINE_STATUSES[detail.status]} · v{detail.currentVersion}{dirty ? " · unsaved ●" : ""}
          </span>
        </div>
        <div className="flex gap-2">
          <Button size="sm" onClick={save}>Save</Button>
          <Button size="sm" variant="outline" onClick={async () => {
            try {
              const r = await pipelines.validate(pipelineId)
              r.valid ? say("Valid") : fail(r.errors.join("; "))
            } catch (e) { fail(e) }
          }}>Validate</Button>
          <Button size="sm" variant="outline" onClick={async () => {
            try {
              const r = await pipelines.publish(pipelineId)
              say(`Published v${r.version}`); reload()
            } catch (e) { fail(e) }
          }}>Publish</Button>
          <Button size="sm" variant="outline" onClick={async () => {
            try { const id = await runs.start(pipelineId); setRunId(id); setTaskDetailId(null); say("Run started"); reload() }
            catch (e) { fail(e) }
          }}>Run</Button>
          <Button size="sm" variant="ghost" onClick={async () => {
            try { await pipelines.archive(pipelineId); say("Archived"); reload() } catch (e) { fail(e) }
          }}>Archive</Button>
          <Button size="sm" variant="ghost" className="text-destructive" onClick={removePipeline}>Delete</Button>
          <Button size="sm" variant="ghost" onClick={onLogout}>Logout</Button>
        </div>
      </div>
      {msg && <p className="text-sm text-green-700 px-4 pt-1">{msg}</p>}
      {err && <p className="text-sm text-destructive px-4 pt-1">{err}</p>}

      <div className="flex flex-1 min-h-0">
        <div className="w-52 border-r p-3 space-y-2 overflow-auto">
          <p className="text-xs font-medium text-muted-foreground">PALETTE — drag onto canvas</p>
          <Input className="h-8 text-xs" placeholder="Search nodes…" value={paletteQuery} onChange={e => setPaletteQuery(e.target.value)} />
          {paletteGroups.map(([cat, items]) => (
            <div key={cat} className="space-y-1">
              <p className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground pt-1">{cat}</p>
              {items.map(d => (
                <div key={d.type} draggable
                  onDragStart={e => e.dataTransfer.setData("application/reflow-nodetype", d.type)}
                  title={d.description}
                  className="border rounded p-2 cursor-grab hover:bg-accent/50 flex items-center gap-2">
                  <span className={`w-2 h-2 rounded-full shrink-0 ${(CATEGORY_COLORS[d.category] ?? "bg-gray-300").split(" ")[0]}`} />
                  <div>
                    <div className="text-xs font-medium">{d.displayName}</div>
                    <div className="text-[10px] font-mono text-muted-foreground">{d.type}</div>
                  </div>
                </div>
              ))}
            </div>
          ))}
          {paletteGroups.length === 0 && <p className="text-xs text-muted-foreground">No nodes match.</p>}
          <Button size="sm" variant="outline" className="w-full" onClick={autoLayout}>Auto-layout</Button>
          {selectedEdgeId && (
            <Button size="sm" variant="outline" className="w-full text-destructive"
              onClick={() => { setEdges(es => es.filter(e => e.id !== selectedEdgeId)); setSelectedEdgeId(null) }}>
              Delete selected edge (Del)
            </Button>
          )}
        </div>

        <div className="flex-1 min-w-0" onDrop={onDrop} onDragOver={e => e.preventDefault()}>
          <ReactFlow
            nodes={nodes} edges={edges}
            onNodesChange={onNodesChange} onEdgesChange={onEdgesChange} onConnect={onConnect}
            onNodeClick={(_, n) => { setSelectedId(n.id); setSelectedEdgeId(null) }}
            onEdgeClick={(_, e) => { setSelectedEdgeId(e.id); setSelectedId(null) }}
            onPaneClick={() => { setSelectedId(null); setSelectedEdgeId(null) }}
            nodeTypes={nodeTypesMap} fitView>
            <Background />
            <Controls />
            <MiniMap pannable zoomable />
          </ReactFlow>
        </div>

        <div className="w-96 border-l overflow-auto">
          {selected ? (
            <div className="p-3 space-y-3">
              <div className="flex items-center gap-2">
                <p className="text-sm font-medium font-mono">{selected.id}</p>
                <span className="text-xs px-2 py-0.5 rounded bg-blue-100 text-blue-700">
                  {(selected.data as unknown as FlowNodeData).nodeType}
                </span>
              </div>
              <Input placeholder="Label" value={(selected.data as unknown as FlowNodeData).label}
                onChange={e => patchSelected({ label: e.target.value })} />
              <NodeConfigForm
                nodeType={(selected.data as unknown as FlowNodeData).nodeType}
                configJson={(selected.data as unknown as FlowNodeData).configJson}
                onChange={c => patchSelected({ configJson: c })} />
              <Button size="sm" variant="ghost" className="text-destructive"
                onClick={() => {
                  setNodes(ns => ns.filter(n => n.id !== selectedId))
                  setEdges(es => es.filter(e => e.source !== selectedId && e.target !== selectedId))
                  setSelectedId(null)
                }}>Delete node</Button>
            </div>
          ) : (
            <p className="p-3 text-sm text-muted-foreground">Select a node to edit its config. Drag between handles to connect; click an edge to delete it.</p>
          )}

          <div className="p-3 border-t">
            <p className="text-sm font-medium mb-2">Runs</p>
            {activeRun && activeRun.status < 2 && (
              <p className="text-xs text-blue-700 mb-1">
                ● Running {Math.max(0, Math.round((now - new Date(activeRun.startedAt ?? activeRun.createdAt).getTime()) / 1000))}s
              </p>
            )}
            <div className="space-y-1">
              {runList.map(r => (
                <div key={r.id} className="flex items-center gap-2 text-xs">
                  <button className="underline font-mono" onClick={() => { setRunId(r.id); setTaskDetailId(null) }}>
                    v{r.versionNumber} · {RUN_STATUSES[r.status]}
                  </button>
                  <span className="text-muted-foreground">{r.triggerKind}</span>
                  {r.status < 2 && <button className="text-muted-foreground" onClick={() => runs.cancel(r.id).then(reload).catch(fail)}>cancel</button>}
                </div>
              ))}
              {runList.length === 0 && <p className="text-xs text-muted-foreground">No runs yet — publish, then Run.</p>}
            </div>
            {runId && (
              <div className="mt-2 space-y-1">
                {tasks.map(t => (
                  <div key={t.id} className="text-xs">
                    <button className="underline font-mono" onClick={() => setTaskDetailId(t.id)}>{t.nodeId}</button>
                    {" "}{TASK_STATUSES[t.status]} · ×{t.attemptCount}
                    {t.error && <span className="text-destructive"> {t.error}</span>}
                    {t.status === 4 && <button className="underline ml-1" onClick={() => runs.retryTask(t.id).then(reload).catch(fail)}>retry</button>}
                    {t.nodeType === "data.output" && t.status === 3 && runId &&
                      <a className="underline ml-1" href={artifactUrl(runId, t.nodeId)} download>download</a>}
                  </div>
                ))}
                {taskDetail && (
                  <div className="border rounded p-2 space-y-1">
                    <p className="text-xs font-medium">Output preview</p>
                    {outputPreview ? (
                      <div className="overflow-auto max-h-48">
                        <table className="text-[11px] font-mono">
                          <thead><tr>{outputPreview.cols.map(c => <th key={c} className="text-left pr-2 border-b">{c}</th>)}</tr></thead>
                          <tbody>{outputPreview.rows.map((r, i) => (
                            <tr key={i}>{outputPreview.cols.map(c => <td key={c} className="pr-2 border-b">{r[c] ?? "∅"}</td>)}</tr>
                          ))}</tbody>
                        </table>
                        <p className="text-muted-foreground">showing {outputPreview.rows.length} of {outputPreview.total} rows</p>
                      </div>
                    ) : <p className="text-xs text-muted-foreground">No output yet.</p>}
                    {attempts.length > 0 && (
                      <p className="text-xs text-muted-foreground">
                        Attempts: {attempts.map(a => `#${a.attemptNumber} ${TASK_STATUSES[a.status]}${a.error ? ` (${a.error})` : ""}`).join(" · ")}
                      </p>
                    )}
                  </div>
                )}
                <div className="max-h-40 overflow-auto text-[11px] font-mono bg-muted/50 rounded p-2">
                  {logs.map((l, i) => <p key={i}>[{l.level}] {l.message}</p>)}
                </div>
              </div>
            )}
          </div>

          <div className="p-3 border-t">
            <p className="text-sm font-medium mb-2">Versions ({versionList.length})</p>
            <div className="space-y-1">
              {versionList.map(v => (
                <button key={v.versionNumber} className="block text-xs underline font-mono"
                  onClick={() => versions.detail(pipelineId, v.versionNumber).then(setVersionDetail).catch(fail)}>
                  v{v.versionNumber} · {new Date(v.publishedAt).toLocaleString()}
                </button>
              ))}
            </div>
            {versionDetail && (
              <pre className="text-[10px] font-mono bg-muted/50 rounded p-2 mt-2 max-h-48 overflow-auto">
                {JSON.stringify(JSON.parse(versionDetail.definition), null, 1)}
              </pre>
            )}
          </div>

          <div className="p-3 border-t">
            <p className="text-sm font-medium mb-2">Triggers</p>
            <div className="space-y-1">
              {triggerList.map(t => (
                <div key={t.id} className="space-y-0.5">
                  <div className="flex items-center gap-1 text-xs">
                    <button className="underline" onClick={async () => {
                      if (t.kind !== 0) return
                      try {
                        await triggers.updateSchedule(pipelineId, t.id, { isEnabled: !t.isEnabled })
                        reload()
                      } catch (e) { fail(e) }
                    }} title="toggle enabled">{t.isEnabled ? "●" : "○"}</button>
                    <span>{t.name} ({t.kind === 0 ? `cron ${t.cron} ${t.timezone} · ${t.overlap === 0 ? "skip" : "queue"}` : "webhook"})</span>
                    <button className="text-destructive ml-auto" onClick={() => triggers.remove(pipelineId, t.id).then(reload).catch(fail)}>x</button>
                  </div>
                  {t.kind === 1 && (
                    <button className="text-[11px] underline font-mono ml-5" onClick={async () => {
                      try {
                        const r = await triggers.regenerateWebhook(pipelineId, t.id)
                        setHookToken(r.token); say("Token regenerated — copy it now")
                      } catch (e) { fail(e) }
                    }}>regenerate token</button>
                  )}
                </div>
              ))}
            </div>
            <div className="flex gap-1 mt-2">
              <Input className="h-8 text-xs" placeholder="Name" value={schedName} onChange={e => setSchedName(e.target.value)} />
              <Input className="h-8 text-xs font-mono" placeholder="cron" value={schedCron} onChange={e => setSchedCron(e.target.value)} />
            </div>
            <div className="flex gap-1 mt-1">
              <Input className="h-8 text-xs" placeholder="Timezone" value={schedTz} onChange={e => setSchedTz(e.target.value)} />
              <Button size="sm" variant="outline" onClick={async () => {
                try {
                  await triggers.createSchedule(pipelineId, { name: schedName || "Schedule", cron: schedCron, timezone: schedTz, overlap: 0 })
                  setSchedName(""); say("Schedule created"); reload()
                } catch (e) { fail(e) }
              }}>+cron</Button>
            </div>
            <div className="flex gap-1 mt-1">
              <Input className="h-8 text-xs" placeholder="Webhook name" value={hookName} onChange={e => setHookName(e.target.value)} />
              <Button size="sm" variant="outline" onClick={async () => {
                try {
                  const r = await triggers.createWebhook(pipelineId, { name: hookName || "Webhook" })
                  setHookToken(r.token); setHookName(""); say("Webhook created — copy the token now"); reload()
                } catch (e) { fail(e) }
              }}>+hook</Button>
            </div>
            {hookToken && (
              <div className="mt-1 flex items-center gap-1">
                <p className="text-[10px] font-mono break-all bg-muted/50 rounded p-1 flex-1">
                  {window.location.origin}/api/v1/hooks/{hookToken}
                </p>
                <Button size="sm" variant="ghost" onClick={() => navigator.clipboard.writeText(`${window.location.origin}/api/v1/hooks/${hookToken}`).then(() => say("Copied"))}>Copy</Button>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}
