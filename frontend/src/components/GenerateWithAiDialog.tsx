import { useEffect, useState } from "react"
import {
  assistant, pipelines,
  type GeneratedWorkflowDraft, type PipelineNode,
} from "@/api/client"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"

interface Props {
  onClose: () => void
  onCreated: (id: string) => void
}

const EXAMPLES = [
  "Read my orders CSV, keep only EU rows, then total revenue by country",
  "Load the customers JSON and join it with orders on customer id",
  "Parse a webhook payload and save the result as a CSV artifact",
]

/**
 * AI v1 is new-pipeline-only: the server returns a validated draft, this dialog
 * reviews it, and saving goes through the normal pipeline write path.
 */
export function GenerateWithAiDialog({ onClose, onCreated }: Props) {
  const [prompt, setPrompt] = useState("")
  const [draft, setDraft] = useState<GeneratedWorkflowDraft | null>(null)
  const [draftName, setDraftName] = useState("")
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState("")

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape" && !busy) onClose() }
    window.addEventListener("keydown", onKey)
    return () => window.removeEventListener("keydown", onKey)
  }, [busy, onClose])

  const generate = async () => {
    const text = prompt.trim()
    if (text.length < 3) return
    setBusy(true)
    setError("")
    try {
      const result = await assistant.generate(text)
      setDraft(result)
      setDraftName(result.name)
    } catch (e) {
      setError(e instanceof Error ? e.message : "Generation failed")
    } finally {
      setBusy(false)
    }
  }

  const create = async () => {
    if (!draft) return
    setBusy(true)
    setError("")
    let pipelineId: string | null = null
    try {
      pipelineId = await pipelines.create({ name: draftName.trim() || draft.name })
      await pipelines.update(pipelineId, {
        nodes: layoutDraft(draft),
        edges: draft.edges.map(e => ({ sourceNodeId: e.sourceNodeId, targetNodeId: e.targetNodeId })),
      })
      onCreated(pipelineId)
    } catch (e) {
      // Do not leave an empty pipeline behind if the graph write fails.
      if (pipelineId) await pipelines.remove(pipelineId).catch(() => undefined)
      setError(e instanceof Error ? e.message : "Could not create the pipeline")
      setBusy(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div role="dialog" aria-modal="true" aria-label="Generate a pipeline with AI"
        className="w-full max-w-2xl max-h-[85vh] overflow-auto rounded-xl border bg-background p-6 shadow-xl space-y-4">
        <div className="flex items-start justify-between gap-4">
          <div>
            <h2 className="text-lg font-semibold">Generate a pipeline</h2>
            <p className="text-sm text-muted-foreground">
              Describe the data work. You will review the draft before it is saved.
            </p>
          </div>
          <Button variant="ghost" size="sm" onClick={onClose} disabled={busy} aria-label="Close">x</Button>
        </div>

        {!draft ? (
          <>
            <label className="text-sm block space-y-1">
              <span>What should the pipeline do?</span>
              <textarea
                className="w-full rounded-md border bg-transparent p-3 text-sm min-h-28 focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                placeholder={EXAMPLES[0]}
                value={prompt}
                maxLength={2000}
                autoFocus
                onChange={e => setPrompt(e.target.value)}
                onKeyDown={e => {
                  if (e.key === "Enter" && (e.ctrlKey || e.metaKey)) void generate()
                }}
              />
            </label>
            <div className="flex flex-wrap gap-2">
              {EXAMPLES.map(example => (
                <button key={example} type="button"
                  className="text-xs text-left rounded-full border px-3 py-1 hover:bg-accent transition-colors"
                  onClick={() => setPrompt(example)}>
                  {example}
                </button>
              ))}
            </div>
            <div className="flex items-center justify-between">
              <span className="text-xs text-muted-foreground">
                {prompt.length}/2000 - Ctrl+Enter to generate
              </span>
              <Button onClick={() => void generate()} disabled={busy || prompt.trim().length < 3}>
                {busy ? "Generating..." : "Generate draft"}
              </Button>
            </div>
          </>
        ) : (
          <>
            <div className="space-y-1">
              <label className="text-xs text-muted-foreground block">Pipeline name</label>
              <Input value={draftName} onChange={e => setDraftName(e.target.value)} maxLength={200} />
            </div>

            {draft.explanation && <p className="text-sm">{draft.explanation}</p>}

            <div className="rounded-lg border divide-y">
              {draft.nodes.map(node => (
                <div key={node.nodeId} className="flex items-center justify-between gap-3 px-3 py-2">
                  <div className="min-w-0">
                    <p className="text-sm font-medium truncate">{node.label || node.nodeId}</p>
                    <p className="text-xs text-muted-foreground font-mono truncate">{node.nodeId}</p>
                  </div>
                  <span className="text-xs font-mono rounded bg-muted px-2 py-0.5 shrink-0">
                    {node.nodeType}
                  </span>
                </div>
              ))}
            </div>

            {draft.warnings.length > 0 && (
              <div className="rounded-lg border border-amber-300 bg-amber-50 p-3">
                <p className="text-xs font-semibold text-amber-900 mb-1">Assumptions</p>
                <ul className="list-disc pl-4 space-y-1">
                  {draft.warnings.map(warning => (
                    <li key={warning} className="text-xs text-amber-900">{warning}</li>
                  ))}
                </ul>
              </div>
            )}

            <p className="text-xs text-muted-foreground">
              {draft.nodes.length} nodes, {draft.edges.length} edges
              {draft.repairAttempts > 0 ? ` - repaired ${draft.repairAttempts} time${draft.repairAttempts === 1 ? "" : "s"}` : ""}
              {" "}- nothing is saved until you create the pipeline.
            </p>

            <div className="flex justify-between gap-2">
              <Button variant="outline" onClick={() => { setDraft(null); setError("") }} disabled={busy}>
                Edit prompt
              </Button>
              <Button onClick={() => void create()} disabled={busy}>
                {busy ? "Creating..." : "Create pipeline"}
              </Button>
            </div>
          </>
        )}

        {error && <p className="text-sm text-destructive">{error}</p>}
      </div>
    </div>
  )
}

/** The draft has no coordinates; place nodes in layers by dependency depth. */
function layoutDraft(draft: GeneratedWorkflowDraft): PipelineNode[] {
  const depth = new Map<string, number>()
  for (const node of draft.nodes) depth.set(node.nodeId, 0)

  // Validation guarantees an acyclic graph, so n passes are enough.
  for (let pass = 0; pass < draft.nodes.length; pass++) {
    for (const edge of draft.edges) {
      const source = depth.get(edge.sourceNodeId)
      const target = depth.get(edge.targetNodeId)
      if (source === undefined || target === undefined) continue
      depth.set(edge.targetNodeId, Math.max(target, source + 1))
    }
  }

  const perLayer = new Map<number, string[]>()
  for (const node of draft.nodes) {
    const layer = depth.get(node.nodeId) ?? 0
    const ids = perLayer.get(layer) ?? []
    ids.push(node.nodeId)
    perLayer.set(layer, ids)
  }

  const positions = new Map<string, { x: number; y: number }>()
  for (const [layer, ids] of perLayer) {
    ids.sort().forEach((id, index) => {
      positions.set(id, { x: 80 + layer * 300, y: 80 + index * 140 })
    })
  }

  return draft.nodes.map(node => {
    const position = positions.get(node.nodeId) ?? { x: 80, y: 80 }
    return {
      nodeId: node.nodeId,
      nodeType: node.nodeType,
      configJson: JSON.stringify(node.config ?? {}, null, 1),
      label: node.label,
      positionX: position.x,
      positionY: position.y,
    }
  })
}
