import { useState, useEffect, useCallback } from "react"
import {
  pipelines, runs, triggers, nodeTypes,
  RUN_STATUSES, TASK_STATUSES,
  type PipelineDetail, type PipelineNode, type PipelineEdge,
  type NodeDefinition, type PipelineRun, type TaskRun, type RunLog, type TriggerItem,
} from "@/api/client"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Card, CardHeader, CardTitle, CardContent } from "@/components/ui/card"

interface Props {
  pipelineId: string
  onBack: () => void
  onLogout: () => void
}

const emptyNode = (type: string, n: number): PipelineNode => ({
  nodeId: `n${n}`, nodeType: type, configJson: "{}",
  label: null, positionX: 0, positionY: 0,
})

export function PipelineEditorPage({ pipelineId, onBack, onLogout }: Props) {
  const [detail, setDetail] = useState<PipelineDetail | null>(null)
  const [defs, setDefs] = useState<NodeDefinition[]>([])
  const [name, setName] = useState("")
  const [nodes, setNodes] = useState<PipelineNode[]>([])
  const [edges, setEdges] = useState<PipelineEdge[]>([])
  const [msg, setMsg] = useState("")
  const [err, setErr] = useState("")
  const [newType, setNewType] = useState("")
  const [edgeFrom, setEdgeFrom] = useState("")
  const [edgeTo, setEdgeTo] = useState("")

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
      setDetail(d); setName(d.name); setNodes(d.nodes); setEdges(d.edges)
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

  const save = async () => {
    try {
      nodes.forEach(n => { JSON.parse(n.configJson) });
      await pipelines.update(pipelineId, { name, nodes, edges })
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
      setRunId(id); say("Run started"); reload()
    } catch (e) { fail(e) }
  }

  const addNode = () => {
    if (!newType) return
    setNodes(prev => [...prev, emptyNode(newType, prev.length + 1)])
  }

  const addEdge = () => {
    if (!edgeFrom || !edgeTo) return
    setEdges(prev => [...prev, { sourceNodeId: edgeFrom, targetNodeId: edgeTo }])
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
    <div className="max-w-5xl mx-auto p-8 space-y-6">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Button variant="ghost" size="sm" onClick={onBack}>← Back</Button>
          <Input value={name} onChange={e => setName(e.target.value)} className="w-64" />
        </div>
        <div className="flex gap-2">
          <Button size="sm" onClick={save}>Save</Button>
          <Button size="sm" variant="outline" onClick={validate}>Validate</Button>
          <Button size="sm" variant="outline" onClick={publish}>Publish</Button>
          <Button size="sm" variant="ghost" onClick={archive}>Archive</Button>
          <Button size="sm" variant="ghost" onClick={onLogout}>Logout</Button>
        </div>
      </div>
      {msg && <p className="text-sm text-green-700">{msg}</p>}
      {err && <p className="text-sm text-destructive">{err}</p>}

      <Card>
        <CardHeader><CardTitle className="text-base">Nodes ({nodes.length})</CardTitle></CardHeader>
        <CardContent className="space-y-3">
          {nodes.map((n, i) => (
            <div key={i} className="border rounded p-3 space-y-2">
              <div className="flex gap-2 items-center">
                <Input className="w-28" value={n.nodeId} onChange={e => setNodes(p => p.map((x, j) => j === i ? { ...x, nodeId: e.target.value } : x))} />
                <span className="text-xs px-2 py-0.5 rounded bg-blue-100 text-blue-700">{n.nodeType}</span>
                <Input className="w-32" placeholder="label" value={n.label ?? ""} onChange={e => setNodes(p => p.map((x, j) => j === i ? { ...x, label: e.target.value || null } : x))} />
                <Button variant="ghost" size="sm" className="text-destructive ml-auto" onClick={() => setNodes(p => p.filter((_, j) => j !== i))}>x</Button>
              </div>
              <textarea className="w-full text-xs font-mono border rounded p-2" rows={2} value={n.configJson}
                onChange={e => setNodes(p => p.map((x, j) => j === i ? { ...x, configJson: e.target.value } : x))} />
            </div>
          ))}
          <div className="flex gap-2">
            <select className="h-9 rounded-md border px-3 text-sm flex-1" value={newType} onChange={e => setNewType(e.target.value)}>
              <option value="">Add node…</option>
              {defs.map(d => <option key={d.type} value={d.type}>{d.displayName} ({d.type})</option>)}
            </select>
            <Button size="sm" variant="outline" onClick={addNode}>Add</Button>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle className="text-base">Edges ({edges.length})</CardTitle></CardHeader>
        <CardContent className="space-y-2">
          {edges.map((e, i) => (
            <div key={i} className="flex items-center gap-2 text-sm">
              <span className="font-mono">{e.sourceNodeId} → {e.targetNodeId}</span>
              <Button variant="ghost" size="sm" className="text-destructive" onClick={() => setEdges(p => p.filter((_, j) => j !== i))}>x</Button>
            </div>
          ))}
          <div className="flex gap-2">
            <select className="h-9 rounded-md border px-3 text-sm flex-1" value={edgeFrom} onChange={e => setEdgeFrom(e.target.value)}>
              <option value="">From…</option>
              {nodes.map(n => <option key={n.nodeId} value={n.nodeId}>{n.nodeId}</option>)}
            </select>
            <select className="h-9 rounded-md border px-3 text-sm flex-1" value={edgeTo} onChange={e => setEdgeTo(e.target.value)}>
              <option value="">To…</option>
              {nodes.map(n => <option key={n.nodeId} value={n.nodeId}>{n.nodeId}</option>)}
            </select>
            <Button size="sm" variant="outline" onClick={addEdge}>Add</Button>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle className="text-base">Runs</CardTitle>
            <Button size="sm" onClick={startRun}>Start run</Button>
          </div>
        </CardHeader>
        <CardContent className="space-y-2">
          {runList.map(r => (
            <div key={r.id} className="flex items-center gap-2 text-sm">
              <button className="underline font-mono" onClick={() => setRunId(r.id)}>v{r.versionNumber} · {RUN_STATUSES[r.status]}</button>
              <span className="text-muted-foreground">{r.triggerKind} · {new Date(r.createdAt).toLocaleString()}</span>
              {r.error && <span className="text-destructive">{r.error}</span>}
              {r.status < 2 && <Button size="sm" variant="ghost" onClick={() => runs.cancel(r.id).then(reload).catch(fail)}>Cancel</Button>}
            </div>
          ))}
          {runId && (
            <div className="border rounded p-3 mt-2 space-y-2">
              <p className="text-sm font-medium font-mono">{runId}</p>
              {tasks.map(t => (
                <div key={t.id} className="flex items-center gap-2 text-sm">
                  <span className="font-mono">{t.nodeId} ({t.nodeType})</span>
                  <span>{TASK_STATUSES[t.status]} · attempts {t.attemptCount}</span>
                  {t.error && <span className="text-destructive">{t.error}</span>}
                  {t.status === 4 && <Button size="sm" variant="ghost" onClick={() => runs.retryTask(t.id).then(reload).catch(fail)}>Retry</Button>}
                </div>
              ))}
              <div className="max-h-48 overflow-auto text-xs font-mono bg-muted/50 rounded p-2">
                {logs.map((l, i) => <p key={i}>[{l.level}] {l.message}</p>)}
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle className="text-base">Triggers</CardTitle></CardHeader>
        <CardContent className="space-y-2">
          {triggerList.map(t => (
            <div key={t.id} className="flex items-center gap-2 text-sm">
              <span>{t.name} ({t.kind === 0 ? "schedule" : "webhook"}) {t.isEnabled ? "" : "[disabled]"}</span>
              {t.cron && <span className="font-mono text-muted-foreground">{t.cron} {t.timezone}</span>}
              {t.nextRunAt && <span className="text-muted-foreground">next {new Date(t.nextRunAt).toLocaleString()}</span>}
              <Button size="sm" variant="ghost" className="text-destructive ml-auto" onClick={() => triggers.remove(pipelineId, t.id).then(reload).catch(fail)}>x</Button>
            </div>
          ))}
          <div className="flex gap-2 pt-2">
            <Input className="w-32" placeholder="Name" value={schedName} onChange={e => setSchedName(e.target.value)} />
            <Input className="w-32 font-mono" value={schedCron} onChange={e => setSchedCron(e.target.value)} />
            <Input className="w-28" value={schedTz} onChange={e => setSchedTz(e.target.value)} />
            <Button size="sm" variant="outline" onClick={createSchedule}>Add schedule</Button>
          </div>
          <div className="flex gap-2">
            <Input className="w-32" placeholder="Name" value={hookName} onChange={e => setHookName(e.target.value)} />
            <Button size="sm" variant="outline" onClick={createWebhook}>Add webhook</Button>
          </div>
          {hookToken && <p className="text-xs font-mono break-all bg-muted/50 rounded p-2">POST /api/v1/hooks/{hookToken}</p>}
        </CardContent>
      </Card>
    </div>
  )
}
