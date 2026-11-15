import { useState } from "react"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"

interface Props {
  isOpen: boolean
  onClose: () => void
  onCreate: (name: string) => Promise<{ token: string }>
}

export function WebhookDialog({ isOpen, onClose, onCreate }: Props) {
  const [name, setName] = useState("External Webhook")
  const [createdToken, setCreatedToken] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState("")
  const [copiedUrl, setCopiedUrl] = useState(false)
  const [copiedCurl, setCopiedCurl] = useState(false)

  if (!isOpen) return null

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault()
    setError("")
    setSaving(true)
    try {
      const res = await onCreate(name.trim() || "External Webhook")
      setCreatedToken(res.token)
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create webhook")
    } finally {
      setSaving(false)
    }
  }

  const webhookUrl = createdToken
    ? `${window.location.origin}/api/v1/hooks/${createdToken}`
    : ""

  const curlSnippet = createdToken
    ? `curl -X POST "${webhookUrl}" \\\n  -H "Content-Type: application/json" \\\n  -d '{"event":"test", "timestamp":"${new Date().toISOString()}"}'`
    : ""

  const handleClose = () => {
    setCreatedToken(null)
    setName("External Webhook")
    setError("")
    onClose()
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-xs">
      <div
        role="dialog"
        aria-modal="true"
        className="w-full max-w-lg rounded-lg border bg-background p-5 shadow-xl space-y-4"
      >
        <div className="flex items-start justify-between">
          <div>
            <h2 className="text-base font-semibold">
              {createdToken ? "Webhook Created" : "Create Webhook Trigger"}
            </h2>
            <p className="text-xs text-muted-foreground mt-0.5">
              {createdToken
                ? "Copy your secret webhook URL and curl example below."
                : "Trigger this pipeline via an external HTTP POST request."}
            </p>
          </div>
          <button
            onClick={handleClose}
            className="text-muted-foreground hover:text-foreground text-sm font-mono px-1.5 py-0.5 rounded"
          >
            ✕
          </button>
        </div>

        {error && (
          <div className="text-xs bg-destructive/10 border border-destructive/20 text-destructive p-2 rounded">
            {error}
          </div>
        )}

        {!createdToken ? (
          <form onSubmit={handleCreate} className="space-y-4 text-xs">
            <div className="space-y-1">
              <label className="font-medium text-foreground">Webhook Name</label>
              <Input
                className="h-8 text-xs"
                placeholder="e.g. GitHub Action, Stripe Event"
                value={name}
                onChange={e => setName(e.target.value)}
                autoFocus
                required
              />
            </div>

            <p className="text-[11px] text-muted-foreground">
              A unique secret URL token will be generated. Any JSON body sent to this URL is immediately fed to upstream nodes with trigger payloads.
            </p>

            <div className="flex justify-end gap-2 pt-2 border-t">
              <Button type="button" variant="outline" size="sm" onClick={handleClose} disabled={saving}>
                Cancel
              </Button>
              <Button type="submit" size="sm" disabled={saving}>
                {saving ? "Generating..." : "Generate Webhook"}
              </Button>
            </div>
          </form>
        ) : (
          <div className="space-y-3 text-xs">
            <div className="space-y-1">
              <label className="font-medium text-foreground">Webhook Endpoint URL</label>
              <div className="flex items-center gap-1.5">
                <Input
                  readOnly
                  className="h-8 font-mono text-[11px] bg-muted/30"
                  value={webhookUrl}
                />
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => {
                    navigator.clipboard.writeText(webhookUrl)
                    setCopiedUrl(true)
                    setTimeout(() => setCopiedUrl(false), 2000)
                  }}
                >
                  {copiedUrl ? "Copied!" : "Copy"}
                </Button>
              </div>
            </div>

            <div className="space-y-1">
              <div className="flex items-center justify-between">
                <label className="font-medium text-foreground">cURL Example</label>
                <button
                  type="button"
                  className="text-[11px] text-primary hover:underline font-medium"
                  onClick={() => {
                    navigator.clipboard.writeText(curlSnippet)
                    setCopiedCurl(true)
                    setTimeout(() => setCopiedCurl(false), 2000)
                  }}
                >
                  {copiedCurl ? "Copied!" : "Copy cURL"}
                </button>
              </div>
              <pre className="p-2.5 rounded border bg-muted/40 font-mono text-[10px] overflow-x-auto whitespace-pre leading-relaxed">
                {curlSnippet}
              </pre>
            </div>

            <div className="flex justify-end pt-2 border-t">
              <Button size="sm" onClick={handleClose}>
                Done
              </Button>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
