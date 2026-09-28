import { useState, useEffect, useCallback } from "react"
import {
  ReactFlow, Background, Controls, Handle, Position,
  applyNodeChanges, applyEdgeChanges, addEdge,
  type Node, type Edge, type NodeChange, type EdgeChange, type Connection,
  type NodeProps,
} from "@xyflow/react"
import "@xyflow/react/dist/style.css"
import {
  pipelines, runs, triggers, nodeTypes,
  RUN_STATUSES, TASK_STATUSES,
  type PipelineDetail, type PipelineEdge,
  type NodeDefinition, type PipelineRun, type TaskRun, type RunLog, type TriggerItem,
} from "@/api/client"
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

function PipelineNodeView({ data, selected }: NodeProps) {
  const d = data as unknown as FlowNodeData
  return (
    <div className={`rounded-md border bg-card px-3 py-2 shadow-sm min-w-36 ${selected ? "ring-2 ring-primary" : ""}`}>
      <Handle type="target" position={Position.Left} />
      <div className="text-xs font-medium">{d.label}</div>
      <div className="text-[10px] px-1.5 py-0.5 rounded bg-blue-100 text-blue-700 inline-block mt-1">{d.nodeType}</div>
      {d.status !== undefined && d.status !== 0 && (
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

export function PipelineEditorPage({ pipelineId, onBack, onLogout }: Props) {
  const [detail, setDetail] = useState<PipelineDetail | null>(null)
  const [defs, setDefs] = useState<NodeDefinition[]>([])
  const [name, setName] = useState("")
  const [nodes, setNodes] = useState<Node[]>([])
  const [edges, setEdges] = useState<Edge[]>([])
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [msg, setMsg] = useState("")
  const [err, setErr] = useState("")

  const [runList, setRunList] = useState<PipelineRun[]>([])
  const [runId, setRunId] = useState<string | null>(null)
  const [tasks, setTasks] = useState<TaskRun[]>([])
  const [logs, setLogs] = useState<RunLog[]>([])

  const [triggerList, setTriggerList] = useState<TriggerItem[]>([])
  const [schedName, setSchedName] = useState("")
  const [schedCron, setSchedCron] = useState("0 2 * * *")
  const [schedTz, setSchedTz] = useState("UTC")
  const [hookName, setHookName] = useState("")
  const [hookToken, setHookToken] = useState("")

  const say = (m: string) => { setMsg(m); setErr("") }
  const fail = (e: unknown) => { setErr(e instanceof Error ? e.message : "Failed"); setMsg("") }

  const reload = useCallback(async () => {
    try {
      const d = await pipelines.get(pipelineId)
      setDetail(d); setName(d.name)
      setNodes(toFlow(d)); setEdges(toFlowEdges(d.edges))
      setRunList(await runs.list(pipelineId))
      setTriggerList(await triggers.list(pipelineId))
    } catch (e) { fail(e) }
  }, [pipelineId])

  useEffect(() => {
    nodeTypes.list().then(setDefs).catch(() => setDefs([]))
    reload()
  }, [reload])

  useEffect(() => {
    if (!runId) return
    let alive = true
    const poll = async () => {
      try {
        const [t, l] = await Promise.all([runs.tasks(runId), runs.logs(runId)])
        if (!alive) return
        setTasks(t); setLogs(l)
        const byNode = new Map(t.map(x => [x.nodeId, x.status] as const))
        setNodes(prev => prev.map(n => ({ ...n, data: { ...n.data, status: byNode.get(n.id) } })))
        const r = await runs.get(runId)
        if (!alive) return
        setRunList(prev => prev.map(x => x.id === runId ? r : x))
        if (r.status >= 2) { clearInterval(timer); return }
      } catch { /* keep polling */ }
    }
    poll()
    const timer = setInterval(poll, 2000)
    return () => { alive = false; clearInterval(timer) }
  }, [runId])

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
      data: { label: id, nodeType: type, configJson: "{}" },
    }])
  }, [])

  const selected = nodes.find(n => n.id === selectedId)
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
      say("Saved"); reload()
    } catch (e) { fail(e) }
  }

  const validate = async () => {
    try {
      const r = await pipelines.validate(pipelineId)
      r.valid ? say("Valid") : fail(r.errors.join("; "))
    } catch (e) { fail(e) }
  }

  const publish = async () => {
    try {
      const r = await pipelines.publish(pipelineId)
      say(`Published v${r.version}`); reload()
    } catch (e) { fail(e) }
  }

  const archive = async () => {
    try { await pipelines.archive(pipelineId); say("Archived"); reload() } catch (e) { fail(e) }
  }

  const startRun = async () => {
    try {
      const id = await runs.start(pipelineId)
      setRunId(id)
      setNodes(ns => ns.map(n => ({ ...n, data: { ...n.data, status: undefined } })))
      say("Run started"); reload()
    } catch (e) { fail(e) }
  }

  const createSchedule = async () => {
    try {
      await triggers.createSchedule(pipelineId, { name: schedName || "Schedule", cron: schedCron, timezone: schedTz, overlap: 0 })
      setSchedName(""); say("Schedule created"); reload()
    } catch (e) { fail(e) }
  }

  const createWebhook = async () => {
    try {
      const r = await triggers.createWebhook(pipelineId, { name: hookName || "Webhook" })
      setHookToken(r.token); setHookName(""); say("Webhook created — copy the token now"); reload()
    } catch (e) { fail(e) }
  }

  if (!detail) return <div className="p-8">Loading... {err && <p className="text-destructive">{err}</p>}</div>

  return (
    <div className="h-screen flex flex-col">
      <div className="flex items-center justify-between px-4 py-2 border-b">
        <div className="flex items-center gap-2">
          <Button variant="ghost" size="sm" onClick={onBack}>←</Button>
          <Input value={name} onChange={e => setName(e.target.value)} className="w-56 h-8" />
        </div>
        <div className="flex gap-2">
          <Button size="sm" onClick={save}>Save</Button>
          <Button size="sm" variant="outline" onClick={validate}>Validate</Button>
          <Button size="sm" variant="outline" onClick={publish}>Publish</Button>
          <Button size="sm" variant="outline" onClick={startRun}>Run</Button>
          <Button size="sm" variant="ghost" onClick={archive}>Archive</Button>
          <Button size="sm" variant="ghost" onClick={onLogout}>Logout</Button>
        </div>
      </div>
      {msg && <p className="text-sm text-green-700 px-4 pt-1">{msg}</p>}
      {err && <p className="text-sm text-destructive px-4 pt-1">{err}</p>}

      <div className="flex flex-1 min-h-0">
        <div className="w-48 border-r p-3 space-y-2 overflow-auto">
          <p className="text-xs font-medium text-muted-foreground">PALETTE — drag onto canvas</p>
          {defs.map(d => (
            <div key={d.type} draggable
              onDragStart={e => e.dataTransfer.setData("application/reflow-nodetype", d.type)}
              className="border rounded p-2 cursor-grab hover:bg-accent/50">
              <div className="text-xs font-medium">{d.displayName}</div>
              <div className="text-[10px] font-mono text-muted-foreground">{d.type}</div>
            </div>
          ))}
        </div>

        <div className="flex-1 min-w-0" onDrop={onDrop} onDragOver={e => e.preventDefault()}>
          <ReactFlow
            nodes={nodes} edges={edges}
            onNodesChange={onNodesChange} onEdgesChange={onEdgesChange} onConnect={onConnect}
            onNodeClick={(_, n) => setSelectedId(n.id)}
            onPaneClick={() => setSelectedId(null)}
            nodeTypes={nodeTypesMap} fitView>
            <Background />
            <Controls />
          </ReactFlow>
        </div>

        <div className="w-80 border-l overflow-auto">
          {selected ? (
            <div className="p-3 space-y-3">
              <p className="text-sm font-medium">Node <span className="font-mono">{selected.id}</span></p>
              <span className="text-xs px-2 py-0.5 rounded bg-blue-100 text-blue-700">
                {(selected.data as unknown as FlowNodeData).nodeType}
              </span>
              <Input placeholder="Label" value={(selected.data as unknown as FlowNodeData).label}
                onChange={e => patchSelected({ label: e.target.value })} />
              <textarea className="w-full text-xs font-mono border rounded p-2" rows={10}
                value={(selected.data as unknown as FlowNodeData).configJson}
                onChange={e => patchSelected({ configJson: e.target.value })} />
              <Button size="sm" variant="ghost" className="text-destructive"
                onClick={() => {
                  setNodes(ns => ns.filter(n => n.id !== selectedId))
                  setEdges(es => es.filter(e => e.source !== selectedId && e.target !== selectedId))
                  setSelectedId(null)
                }}>Delete node</Button>
            </div>
          ) : (
            <p className="p-3 text-sm text-muted-foreground">Select a node to edit its config. Drag between handles to connect.</p>
          )}

          <div className="p-3 border-t">
            <p className="text-sm font-medium mb-2">Runs</p>
            <div className="space-y-1">
              {runList.map(r => (
                <div key={r.id} className="flex items-center gap-2 text-xs">
                  <button className="underline font-mono" onClick={() => setRunId(r.id)}>v{r.versionNumber} · {RUN_STATUSES[r.status]}</button>
                  {r.status < 2 && <button className="text-muted-foreground" onClick={() => runs.cancel(r.id).then(reload).catch(fail)}>cancel</button>}
                </div>
              ))}
            </div>
            {runId && (
              <div className="mt-2 space-y-1">
                {tasks.map(t => (
                  <div key={t.id} className="text-xs">
                    <span className="font-mono">{t.nodeId}</span> {TASK_STATUSES[t.status]}
                    {t.error && <span className="text-destructive"> {t.error}</span>}
                    {t.status === 4 && <button className="underline ml-1" onClick={() => runs.retryTask(t.id).then(reload).catch(fail)}>retry</button>}
                  </div>
                ))}
                <div className="max-h-40 overflow-auto text-[11px] font-mono bg-muted/50 rounded p-2">
                  {logs.map((l, i) => <p key={i}>[{l.level}] {l.message}</p>)}
                </div>
              </div>
            )}
          </div>

          <div className="p-3 border-t">
            <p className="text-sm font-medium mb-2">Triggers</p>
            <div className="space-y-1">
              {triggerList.map(t => (
                <div key={t.id} className="flex items-center gap-1 text-xs">
                  <span>{t.name} ({t.kind === 0 ? `cron ${t.cron}` : "webhook"}){t.isEnabled ? "" : " [off]"}</span>
                  <button className="text-destructive ml-auto" onClick={() => triggers.remove(pipelineId, t.id).then(reload).catch(fail)}>x</button>
                </div>
              ))}
            </div>
            <div className="flex gap-1 mt-2">
              <Input className="h-8 text-xs" placeholder="Name" value={schedName} onChange={e => setSchedName(e.target.value)} />
              <Input className="h-8 text-xs font-mono" placeholder="cron" value={schedCron} onChange={e => setSchedCron(e.target.value)} />
            </div>
            <div className="flex gap-1 mt-1">
              <Input className="h-8 text-xs" placeholder="Timezone" value={schedTz} onChange={e => setSchedTz(e.target.value)} />
              <Button size="sm" variant="outline" onClick={createSchedule}>+cron</Button>
            </div>
            <div className="flex gap-1 mt-1">
              <Input className="h-8 text-xs" placeholder="Webhook name" value={hookName} onChange={e => setHookName(e.target.value)} />
              <Button size="sm" variant="outline" onClick={createWebhook}>+hook</Button>
            </div>
            {hookToken && <p className="text-[10px] font-mono break-all bg-muted/50 rounded p-1 mt-1">POST /api/v1/hooks/{hookToken}</p>}
          </div>
        </div>
      </div>
    </div>
  )
}
