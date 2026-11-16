import { useState, useEffect, useMemo } from "react"
import {
  ReactFlow,
  ReactFlowProvider,
  Background,
  Controls,
  MarkerType,
  Handle,
  Position,
  type Node,
  type Edge,
  type NodeProps,
} from "@xyflow/react"
import "@xyflow/react/dist/style.css"
import { assistant, type GeneratedWorkflowDraft, type NodeDefinition } from "@/api/client"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Sparkles, X, Plus, RefreshCw, AlertCircle, Check, Code, Eye, Key } from "lucide-react"

interface EditorAiDialogProps {
  isOpen: boolean
  onClose: () => void
  existingNodesCount: number
  defs: NodeDefinition[]
  onApply: (nodes: Node[], edges: Edge[], mode: "replace" | "append") => void
}

const EXAMPLES = [
  "Read an orders CSV file, filter for EU countries, and compute total revenue per country",
  "Load customer data and join with orders on customer_id, then sort by highest purchases",
  "Accept inbound webhook payload, filter invalid records, and save to output CSV",
  "Aggregate sales by month and product category using SQL with rolling averages",
]

// Custom preview node for the interactive dialog canvas
function PreviewNodeView({ id, data, selected }: NodeProps) {
  const d = data as { label: string; nodeType: string; isSelected?: boolean }
  return (
    <div
      className={`rounded-lg border bg-card px-3 py-2 shadow-xs min-w-40 transition-all ${
        selected ? "border-primary ring-2 ring-primary/40 shadow-sm" : "border-border hover:border-muted-foreground/50"
      }`}
    >
      <Handle type="target" position={Position.Left} className="!w-2 !h-2 !bg-muted-foreground" />
      <div className="flex items-center justify-between gap-2">
        <span className="text-xs font-semibold truncate text-foreground">{d.label}</span>
      </div>
      <div className="flex items-center justify-between gap-1.5 mt-1 text-[10px] font-mono text-muted-foreground">
        <span className="px-1.5 py-0.2 rounded bg-muted text-foreground/80 font-medium">
          {d.nodeType}
        </span>
        <span className="truncate">({id})</span>
      </div>
      <Handle type="source" position={Position.Right} className="!w-2 !h-2 !bg-muted-foreground" />
    </div>
  )
}

const previewNodeTypes = {
  previewNode: PreviewNodeView,
}

export function EditorAiDialog({
  isOpen,
  onClose,
  existingNodesCount,
  defs,
  onApply,
}: EditorAiDialogProps) {
  const [prompt, setPrompt] = useState("")
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState("")
  const [draft, setDraft] = useState<GeneratedWorkflowDraft | null>(null)
  const [selectedPreviewNodeId, setSelectedPreviewNodeId] = useState<string | null>(null)
  const [apiKey, setApiKey] = useState(() => localStorage.getItem("reflow_ai_api_key") || "")
  const [showKeySettings, setShowKeySettings] = useState(false)

  const handleApiKeyChange = (val: string) => {
    setApiKey(val)
    if (val.trim()) {
      localStorage.setItem("reflow_ai_api_key", val.trim())
    } else {
      localStorage.removeItem("reflow_ai_api_key")
    }
  }

  useEffect(() => {
    if (!isOpen) {
      setDraft(null)
      setError("")
      setSelectedPreviewNodeId(null)
    }
  }, [isOpen])

  useEffect(() => {
    const handleKey = (e: KeyboardEvent) => {
      if (e.key === "Escape" && !busy) onClose()
    }
    if (isOpen) {
      window.addEventListener("keydown", handleKey)
      return () => window.removeEventListener("keydown", handleKey)
    }
  }, [isOpen, busy, onClose])

  const generate = async (overridePrompt?: string) => {
    const target = (overridePrompt ?? prompt).trim()
    if (target.length < 3) return
    setBusy(true)
    setError("")
    setSelectedPreviewNodeId(null)
    try {
      const res = await assistant.generate(target, apiKey.trim() || undefined)
      setDraft(res)
      if (res.nodes.length > 0) {
        setSelectedPreviewNodeId(res.nodes[0].nodeId)
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : "AI generation failed")
    } finally {
      setBusy(false)
    }
  }

  // Convert draft into ReactFlow nodes and edges for visual preview
  const { previewNodes, previewEdges, finalNodes, finalEdges } = useMemo(() => {
    if (!draft) {
      return { previewNodes: [], previewEdges: [], finalNodes: [], finalEdges: [] }
    }

    // Topological DAG layout
    const depth = new Map<string, number>()
    for (const node of draft.nodes) depth.set(node.nodeId, 0)
    for (let pass = 0; pass < draft.nodes.length; pass++) {
      for (const edge of draft.edges) {
        const source = depth.get(edge.sourceNodeId)
        const target = depth.get(edge.targetNodeId)
        if (source !== undefined && target !== undefined) {
          depth.set(edge.targetNodeId, Math.max(target, source + 1))
        }
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
        positions.set(id, { x: 50 + layer * 240, y: 50 + index * 110 })
      })
    }

    const pNodes: Node[] = draft.nodes.map(node => {
      const pos = positions.get(node.nodeId) ?? { x: 50, y: 50 }
      return {
        id: node.nodeId,
        type: "previewNode",
        position: pos,
        data: {
          label: node.label || node.nodeId,
          nodeType: node.nodeType,
          isSelected: node.nodeId === selectedPreviewNodeId,
        },
      }
    })

    const pEdges: Edge[] = draft.edges.map(e => ({
      id: `preview-e-${e.sourceNodeId}-${e.targetNodeId}`,
      source: e.sourceNodeId,
      target: e.targetNodeId,
      animated: true,
      style: { stroke: "#3b82f6", strokeWidth: 1.5 },
      markerEnd: { type: MarkerType.ArrowClosed, color: "#3b82f6" },
    }))

    // Full editor nodes with configJson and category
    const fNodes: Node[] = draft.nodes.map(node => {
      const pos = positions.get(node.nodeId) ?? { x: 80, y: 80 }
      const definition = defs.find(d => d.type === node.nodeType)
      return {
        id: node.nodeId,
        type: "pipelineNode",
        position: pos,
        data: {
          label: node.label || node.nodeId,
          nodeType: node.nodeType,
          configJson: JSON.stringify(node.config ?? {}, null, 2),
          category: definition?.category,
        },
      }
    })

    const fEdges: Edge[] = draft.edges.map(e => ({
      id: `e-${e.sourceNodeId}-${e.targetNodeId}`,
      source: e.sourceNodeId,
      target: e.targetNodeId,
      style: { strokeWidth: 1.5 },
      markerEnd: { type: MarkerType.ArrowClosed },
    }))

    return {
      previewNodes: pNodes,
      previewEdges: pEdges,
      finalNodes: fNodes,
      finalEdges: fEdges,
    }
  }, [draft, defs, selectedPreviewNodeId])

  if (!isOpen) return null

  const selectedNodeData = draft?.nodes.find(n => n.nodeId === selectedPreviewNodeId)

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-xs p-4 animate-in fade-in-0 duration-150">
      <div
        role="dialog"
        aria-modal="true"
        aria-label="Generate Workflow with AI"
        className="w-full max-w-4xl max-h-[90vh] flex flex-col rounded-xl border bg-card shadow-2xl overflow-hidden"
      >
        {/* Header */}
        <div className="px-5 py-3.5 border-b flex items-center justify-between bg-muted/20">
          <div className="flex items-center gap-2.5">
            <div className="h-7 w-7 rounded-md bg-foreground text-background flex items-center justify-center">
              <Sparkles className="h-4 w-4" />
            </div>
            <div>
              <h2 className="text-sm font-semibold tracking-tight">AI Workflow Assistant</h2>
              <p className="text-[11px] text-muted-foreground">
                Describe the workflow logic. Preview nodes & edges before applying to canvas.
              </p>
            </div>
          </div>
          <div className="flex items-center gap-1.5">
            <Button
              variant="outline"
              size="sm"
              onClick={() => setShowKeySettings(!showKeySettings)}
              className={`h-7 px-2 text-[11px] gap-1.5 ${apiKey ? "border-emerald-500/50 text-emerald-600 bg-emerald-500/5" : "text-muted-foreground"}`}
              title="Configure Custom API Key (Gemini, Groq, OpenAI)"
            >
              <Key className="h-3 w-3" />
              <span>{apiKey ? (apiKey.startsWith("AIzaSy") ? "Gemini Key ✓" : "API Key ✓") : "Set API Key"}</span>
            </Button>
            <Button variant="ghost" size="sm" onClick={onClose} disabled={busy} className="h-7 w-7 p-0">
              <X className="h-4 w-4" />
            </Button>
          </div>
        </div>

        {/* API Key Settings Dropdown */}
        {showKeySettings && (
          <div className="px-5 py-2.5 bg-muted/40 border-b flex flex-col sm:flex-row sm:items-center justify-between gap-2 text-xs">
            <div className="space-y-0.5">
              <label className="font-medium text-foreground flex items-center gap-1.5">
                <Key className="h-3 w-3 text-primary" />
                <span>Custom API Key (Gemini, Groq, or OpenAI)</span>
              </label>
              <p className="text-[10px] text-muted-foreground">
                Paste your Google Gemini key (starts with <code className="font-mono">AIzaSy</code>) or Groq / OpenAI key. Stored in your browser.
              </p>
            </div>
            <div className="flex items-center gap-2">
              <Input
                type="password"
                placeholder="AIzaSy... (Gemini) or gsk_... (Groq)"
                value={apiKey}
                onChange={e => handleApiKeyChange(e.target.value)}
                className="h-7 text-xs w-64 font-mono"
              />
              {apiKey && (
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => handleApiKeyChange("")}
                  className="h-7 text-[10px] text-muted-foreground hover:text-destructive px-1.5"
                >
                  Clear
                </Button>
              )}
            </div>
          </div>
        )}

        {/* Content Body */}
        <div className="flex-1 overflow-auto p-5 space-y-4">
          {/* Prompt Input */}
          <div className="space-y-2">
            <div className="relative">
              <textarea
                className="w-full rounded-lg border bg-background px-3 py-2.5 text-xs min-h-20 focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-primary font-sans leading-relaxed resize-none shadow-2xs"
                placeholder="Describe your data pipeline (e.g. Read orders CSV, keep only EU rows, and aggregate total revenue with SQL)..."
                value={prompt}
                maxLength={2000}
                autoFocus
                disabled={busy}
                onChange={e => setPrompt(e.target.value)}
                onKeyDown={e => {
                  if (e.key === "Enter" && (e.ctrlKey || e.metaKey)) {
                    void generate()
                  }
                }}
              />
              <div className="absolute right-2.5 bottom-2.5 flex items-center gap-2">
                <span className="text-[10px] text-muted-foreground font-mono">
                  {prompt.length}/2000
                </span>
                <Button
                  size="sm"
                  onClick={() => void generate()}
                  disabled={busy || prompt.trim().length < 3}
                  className="h-7 text-xs font-medium gap-1.5"
                >
                  {busy ? (
                    <>
                      <RefreshCw className="h-3 w-3 animate-spin" />
                      Designing...
                    </>
                  ) : (
                    <>
                      <Sparkles className="h-3 w-3" />
                      Generate Draft
                    </>
                  )}
                </Button>
              </div>
            </div>

            {/* Quick Prompt Suggestions */}
            {!draft && (
              <div className="flex flex-wrap gap-1.5 pt-1">
                {EXAMPLES.map(ex => (
                  <button
                    key={ex}
                    type="button"
                    disabled={busy}
                    onClick={() => {
                      setPrompt(ex)
                      void generate(ex)
                    }}
                    className="text-[11px] text-left rounded-md border bg-muted/40 px-2.5 py-1 hover:bg-muted text-muted-foreground hover:text-foreground transition-colors"
                  >
                    {ex}
                  </button>
                ))}
              </div>
            )}
          </div>

          {error && (
            <div className="flex items-center gap-2 p-2.5 rounded-lg bg-destructive/10 border border-destructive/20 text-destructive text-xs">
              <AlertCircle className="h-4 w-4 shrink-0" />
              <span>{error}</span>
            </div>
          )}

          {/* Interactive Visual Graph Preview */}
          {draft && (
            <div className="space-y-3 pt-1 border-t">
              {/* Draft Info Header */}
              <div className="flex items-center justify-between text-xs">
                <div className="flex items-center gap-2">
                  <span className="font-semibold text-foreground">{draft.name}</span>
                  <span className="text-[10px] px-2 py-0.5 rounded bg-blue-100 text-blue-700 dark:bg-blue-950 dark:text-blue-300 font-mono font-medium">
                    {draft.nodes.length} nodes · {draft.edges.length} edges
                  </span>
                  {draft.repairAttempts > 0 && (
                    <span className="text-[10px] text-muted-foreground">
                      (self-repaired {draft.repairAttempts}x)
                    </span>
                  )}
                </div>
                <span className="text-[11px] text-muted-foreground flex items-center gap-1 font-mono">
                  <Eye className="h-3 w-3" />
                  Click any node to inspect configuration
                </span>
              </div>

              {draft.explanation && (
                <p className="text-xs text-muted-foreground bg-muted/30 p-2.5 rounded-md border leading-relaxed">
                  {draft.explanation}
                </p>
              )}

              {/* Visual Preview Canvas & Config Inspector Split */}
              <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
                {/* Visual Flow Preview */}
                <div className="md:col-span-2 h-72 rounded-lg border bg-background relative overflow-hidden shadow-2xs">
                  <ReactFlowProvider>
                    <ReactFlow
                      nodes={previewNodes}
                      edges={previewEdges}
                      nodeTypes={previewNodeTypes}
                      fitView
                      nodesDraggable={true}
                      zoomOnScroll={true}
                      panOnDrag={true}
                      onNodeClick={(_, n) => setSelectedPreviewNodeId(n.id)}
                    >
                      <Background gap={12} size={1} />
                      <Controls showInteractive={false} className="!bottom-2 !left-2" />
                    </ReactFlow>
                  </ReactFlowProvider>
                </div>

                {/* Node Config Inspector */}
                <div className="h-72 rounded-lg border bg-card p-3 flex flex-col min-w-0 text-xs">
                  <div className="flex items-center gap-1.5 pb-2 border-b text-foreground font-semibold">
                    <Code className="h-3.5 w-3.5 text-muted-foreground" />
                    <span>Node Config Inspector</span>
                  </div>

                  {selectedNodeData ? (
                    <div className="flex-1 overflow-auto pt-2 space-y-2">
                      <div className="flex items-center justify-between">
                        <span className="font-semibold font-mono text-foreground truncate">
                          {selectedNodeData.label || selectedNodeData.nodeId}
                        </span>
                        <span className="text-[10px] px-1.5 py-0.2 rounded bg-muted font-mono">
                          {selectedNodeData.nodeType}
                        </span>
                      </div>
                      <div className="text-[10px] text-muted-foreground font-mono">
                        id: <code>{selectedNodeData.nodeId}</code>
                      </div>
                      <div className="rounded border bg-zinc-950 p-2 text-zinc-200 font-mono text-[11px] overflow-auto max-h-44">
                        <pre className="whitespace-pre-wrap break-all">
                          {JSON.stringify(selectedNodeData.config, null, 2)}
                        </pre>
                      </div>
                    </div>
                  ) : (
                    <div className="flex-1 flex flex-col items-center justify-center text-muted-foreground text-center p-4">
                      <p>Select a node in the diagram to inspect its parameters.</p>
                    </div>
                  )}
                </div>
              </div>

              {/* Assumptions & Warnings */}
              {draft.warnings.length > 0 && (
                <div className="rounded-lg border border-amber-500/30 bg-amber-500/5 p-2.5 text-xs space-y-1">
                  <p className="font-semibold text-amber-700 dark:text-amber-400">Assumptions made by AI:</p>
                  <ul className="list-disc pl-4 space-y-0.5 text-muted-foreground text-[11px]">
                    {draft.warnings.map((w, i) => (
                      <li key={i}>{w}</li>
                    ))}
                  </ul>
                </div>
              )}
            </div>
          )}
        </div>

        {/* Footer Actions */}
        <div className="px-5 py-3 border-t bg-muted/20 flex items-center justify-between gap-3">
          <Button
            variant="ghost"
            size="sm"
            onClick={onClose}
            disabled={busy}
            className="h-8 text-xs"
          >
            Cancel
          </Button>

          {draft ? (
            <div className="flex items-center gap-2">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setDraft(null)}
                disabled={busy}
                className="h-8 text-xs"
              >
                Modify Prompt
              </Button>

              {existingNodesCount > 0 ? (
                <>
                  <Button
                    variant="outline"
                    size="sm"
                    disabled={busy}
                    onClick={() => {
                      onApply(finalNodes, finalEdges, "append")
                      onClose()
                    }}
                    className="h-8 text-xs gap-1.5"
                  >
                    <Plus className="h-3.5 w-3.5" />
                    Append to Canvas
                  </Button>
                  <Button
                    size="sm"
                    disabled={busy}
                    onClick={() => {
                      onApply(finalNodes, finalEdges, "replace")
                      onClose()
                    }}
                    className="h-8 text-xs gap-1.5 font-medium"
                  >
                    <Check className="h-3.5 w-3.5" />
                    Replace Canvas
                  </Button>
                </>
              ) : (
                <Button
                  size="sm"
                  disabled={busy}
                  onClick={() => {
                    onApply(finalNodes, finalEdges, "replace")
                    onClose()
                  }}
                  className="h-8 text-xs gap-1.5 font-medium"
                >
                  <Check className="h-3.5 w-3.5" />
                  Insert into Canvas
                </Button>
              )}
            </div>
          ) : (
            <Button
              size="sm"
              onClick={() => void generate()}
              disabled={busy || prompt.trim().length < 3}
              className="h-8 text-xs gap-1.5"
            >
              <Sparkles className="h-3.5 w-3.5" />
              Generate Draft
            </Button>
          )}
        </div>
      </div>
    </div>
  )
}
