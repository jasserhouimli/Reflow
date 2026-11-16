import { useState, useRef, useCallback } from "react"
import {
  Maximize2,
  Minimize2,
  Sparkles,
  Copy,
  Check,
  Database,
  Plus,
} from "lucide-react"
import { Button } from "@/components/ui/button"

export interface UpstreamNode {
  id: string
  label: string
  nodeType: string
}

interface SqlEditorProps {
  value: string
  onChange: (value: string) => void
  upstreamNodes?: UpstreamNode[]
}

const SQL_KEYWORDS = [
  "SELECT",
  "DISTINCT",
  "FROM",
  "WHERE",
  "GROUP BY",
  "ORDER BY",
  "HAVING",
  "LIMIT",
  "OFFSET",
  "LEFT JOIN",
  "RIGHT JOIN",
  "INNER JOIN",
  "FULL JOIN",
  "CROSS JOIN",
  "JOIN",
  "ON",
  "AS",
  "AND",
  "OR",
  "NOT",
  "IN",
  "IS NULL",
  "IS NOT NULL",
  "BETWEEN",
  "LIKE",
  "ILIKE",
  "EXISTS",
  "CASE",
  "WHEN",
  "THEN",
  "ELSE",
  "END",
  "UNION ALL",
  "UNION",
  "WITH",
  "DESC",
  "ASC",
  "COUNT",
  "SUM",
  "AVG",
  "MIN",
  "MAX",
  "COALESCE",
  "CAST",
]

/**
 * Formats SQL keywords to UPPERCASE and structures major clauses with standard indentation.
 */
function formatSql(rawSql: string): string {
  if (!rawSql || !rawSql.trim()) return rawSql

  // Preserve string literals and comments
  const preservedTokens: string[] = []
  const tokenPlaceholder = (match: string) => {
    preservedTokens.push(match)
    return `___SQL_TOKEN_${preservedTokens.length - 1}___`
  }

  // Replace single-quoted strings, double-quoted strings, and line comments
  let text = rawSql
    .replace(/--.*$/gm, tokenPlaceholder)
    .replace(/'(?:''|[^'])*'/g, tokenPlaceholder)
    .replace(/"(?:""|[^"])*"/g, tokenPlaceholder)

  // Uppercase keywords
  for (const kw of SQL_KEYWORDS) {
    const pattern = new RegExp(`\\b${kw.replace(/ /g, "\\s+")}\\b`, "gi")
    text = text.replace(pattern, kw)
  }

  // Restore preserved tokens
  text = text.replace(/___SQL_TOKEN_(\d+)___/g, (_, index) => {
    return preservedTokens[Number(index)] ?? ""
  })

  return text
}

export function SqlEditor({ value, onChange, upstreamNodes = [] }: SqlEditorProps) {
  const [isFullscreen, setIsFullscreen] = useState(false)
  const [copied, setCopied] = useState(false)

  const textareaRef = useRef<HTMLTextAreaElement>(null)
  const lineNumbersRef = useRef<HTMLDivElement>(null)
  const modalTextareaRef = useRef<HTMLTextAreaElement>(null)
  const modalLineNumbersRef = useRef<HTMLDivElement>(null)

  // Sync line numbers scrolling with textarea
  const handleScroll = (
    src: HTMLTextAreaElement | null,
    target: HTMLDivElement | null
  ) => {
    if (!src || !target) return
    target.scrollTop = src.scrollTop
  }

  const handleCopy = async () => {
    if (!value) return
    try {
      await navigator.clipboard.writeText(value)
      setCopied(true)
      setTimeout(() => setCopied(false), 1800)
    } catch {
      // ignore
    }
  }

  const handleFormat = () => {
    const formatted = formatSql(value)
    onChange(formatted)
  }

  const handleInsert = useCallback(
    (textToInsert: string, isModal = false) => {
      const activeTextarea = isModal
        ? modalTextareaRef.current
        : textareaRef.current
      if (!activeTextarea) {
        onChange(value ? `${value} ${textToInsert}` : textToInsert)
        return
      }

      const start = activeTextarea.selectionStart
      const end = activeTextarea.selectionEnd
      const before = value.substring(0, start)
      const after = value.substring(end)
      const newValue = `${before}${textToInsert}${after}`

      onChange(newValue)

      // Reposition cursor after inserted text
      requestAnimationFrame(() => {
        const newPos = start + textToInsert.length
        activeTextarea.focus()
        activeTextarea.setSelectionRange(newPos, newPos)
      })
    },
    [onChange, value]
  )

  const handleKeyDown = (
    e: React.KeyboardEvent<HTMLTextAreaElement>,
    isModal = false
  ) => {
    const target = e.currentTarget
    const start = target.selectionStart
    const end = target.selectionEnd

    // Tab and Shift+Tab handling
    if (e.key === "Tab") {
      e.preventDefault()

      if (e.shiftKey) {
        // Dedent
        const lineStart = value.lastIndexOf("\n", start - 1) + 1
        const lineEnd = value.indexOf("\n", end)
        const effectiveEnd = lineEnd === -1 ? value.length : lineEnd
        const block = value.substring(lineStart, effectiveEnd)
        const dedented = block
          .split("\n")
          .map(line => (line.startsWith("  ") ? line.slice(2) : line.startsWith(" ") ? line.slice(1) : line))
          .join("\n")

        const updated = value.substring(0, lineStart) + dedented + value.substring(effectiveEnd)
        onChange(updated)

        requestAnimationFrame(() => {
          target.setSelectionRange(
            Math.max(lineStart, start - 2),
            Math.max(lineStart, end - (block.length - dedented.length))
          )
        })
      } else {
        // Indent
        if (start !== end) {
          // Multiline indent
          const lineStart = value.lastIndexOf("\n", start - 1) + 1
          const lineEnd = value.indexOf("\n", end)
          const effectiveEnd = lineEnd === -1 ? value.length : lineEnd
          const block = value.substring(lineStart, effectiveEnd)
          const indented = block
            .split("\n")
            .map(line => `  ${line}`)
            .join("\n")

          const updated = value.substring(0, lineStart) + indented + value.substring(effectiveEnd)
          onChange(updated)

          requestAnimationFrame(() => {
            target.setSelectionRange(start + 2, end + (indented.length - block.length))
          })
        } else {
          // Single cursor indent (insert 2 spaces)
          const updated = `${value.substring(0, start)}  ${value.substring(end)}`
          onChange(updated)
          requestAnimationFrame(() => {
            target.setSelectionRange(start + 2, start + 2)
          })
        }
      }
      return
    }

    // Auto-indent on Enter
    if (e.key === "Enter") {
      const lineStart = value.lastIndexOf("\n", start - 1) + 1
      const currentLine = value.substring(lineStart, start)
      const indentMatch = currentLine.match(/^\s+/)
      const currentIndent = indentMatch ? indentMatch[0] : ""

      e.preventDefault()
      const insertion = `\n${currentIndent}`
      const updated = `${value.substring(0, start)}${insertion}${value.substring(end)}`
      onChange(updated)

      requestAnimationFrame(() => {
        const nextPos = start + insertion.length
        target.setSelectionRange(nextPos, nextPos)
      })
      return
    }

    // Wrap selection in quotes / brackets if text is selected
    if (start !== end && ["'", '"', "(", "["].includes(e.key)) {
      e.preventDefault()
      const openChar = e.key
      const closeChar = openChar === "(" ? ")" : openChar === "[" ? "]" : openChar
      const selectedText = value.substring(start, end)
      const wrapped = `${openChar}${selectedText}${closeChar}`
      const updated = `${value.substring(0, start)}${wrapped}${value.substring(end)}`
      onChange(updated)

      requestAnimationFrame(() => {
        target.setSelectionRange(start + 1, end + 1)
      })
      return
    }

    // Escape in fullscreen closes modal
    if (e.key === "Escape" && isModal) {
      e.preventDefault()
      setIsFullscreen(false)
    }
  }

  const lines = (value || "").split("\n")
  const lineCount = Math.max(lines.length, 1)

  return (
    <div className="space-y-2">
      {/* Sidebar Editor Container */}
      <div className="rounded-lg border bg-card text-card-foreground shadow-xs overflow-hidden flex flex-col">
        {/* Editor Toolbar */}
        <div className="flex items-center justify-between border-b px-2.5 py-1.5 bg-muted/40 text-xs">
          <div className="flex items-center gap-1.5 font-medium text-muted-foreground">
            <Database className="h-3.5 w-3.5 text-foreground" />
            <span className="text-[11px] font-mono font-semibold text-foreground">SQL (DuckDB)</span>
          </div>

          <div className="flex items-center gap-1">
            <Button
              type="button"
              variant="ghost"
              size="sm"
              className="h-6 px-1.5 text-[11px] text-muted-foreground hover:text-foreground"
              onClick={handleFormat}
              title="Auto-format SQL keywords"
            >
              <Sparkles className="h-3 w-3 mr-1" />
              Format
            </Button>

            <Button
              type="button"
              variant="ghost"
              size="sm"
              className="h-6 px-1.5 text-[11px] text-muted-foreground hover:text-foreground"
              onClick={handleCopy}
              title="Copy SQL Query"
            >
              {copied ? (
                <>
                  <Check className="h-3 w-3 mr-1 text-emerald-500" />
                  Copied
                </>
              ) : (
                <>
                  <Copy className="h-3 w-3 mr-1" />
                  Copy
                </>
              )}
            </Button>

            <Button
              type="button"
              variant="ghost"
              size="sm"
              className="h-6 px-1.5 text-[11px] text-muted-foreground hover:text-foreground"
              onClick={() => setIsFullscreen(true)}
              title="Expand to Fullscreen Editor"
            >
              <Maximize2 className="h-3 w-3 mr-1" />
              Expand
            </Button>
          </div>
        </div>

        {/* Upstream Connected Tables Helper */}
        {upstreamNodes.length > 0 && (
          <div className="border-b px-2.5 py-1.5 bg-muted/20 flex flex-wrap items-center gap-1.5 text-[11px]">
            <span className="text-muted-foreground text-[10px] font-medium uppercase tracking-wider">
              Tables:
            </span>
            {upstreamNodes.map(node => (
              <button
                key={node.id}
                type="button"
                onClick={() => handleInsert(node.id, false)}
                title={`Click to insert table "${node.id}" at cursor`}
                className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded font-mono text-[10px] bg-background border hover:border-primary/50 hover:bg-primary/5 transition-colors text-foreground"
              >
                <span>{node.id}</span>
                <Plus className="h-2.5 w-2.5 text-muted-foreground" />
              </button>
            ))}
          </div>
        )}

        {/* Code Canvas with Line Numbers */}
        <div className="relative flex min-h-[180px] max-h-[360px] bg-background font-mono text-xs overflow-hidden">
          {/* Line Numbers Gutter */}
          <div
            ref={lineNumbersRef}
            aria-hidden="true"
            className="w-8 shrink-0 select-none border-r bg-muted/20 py-2 text-right pr-2 text-[11px] text-muted-foreground/50 overflow-hidden leading-5 font-mono"
          >
            {Array.from({ length: lineCount }).map((_, i) => (
              <div key={i}>{i + 1}</div>
            ))}
          </div>

          {/* Text Area */}
          <textarea
            ref={textareaRef}
            value={value}
            onChange={e => onChange(e.target.value)}
            onKeyDown={e => handleKeyDown(e, false)}
            onScroll={() => handleScroll(textareaRef.current, lineNumbersRef.current)}
            placeholder="SELECT * FROM input_table WHERE ..."
            rows={8}
            spellCheck={false}
            className="flex-1 resize-y bg-transparent p-2 text-xs leading-5 font-mono text-foreground outline-none focus:ring-0 border-0 placeholder:text-muted-foreground/40 whitespace-pre overflow-auto"
          />
        </div>

        {/* Editor Footer / Info */}
        <div className="flex items-center justify-between border-t px-2.5 py-1 text-[10px] text-muted-foreground bg-muted/20 font-mono">
          <span>{lineCount} lines · {value.length} chars</span>
          <span>Tab indents · Shift+Tab dedents</span>
        </div>
      </div>

      {/* Expanded / Fullscreen Dialog Modal */}
      {isFullscreen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 sm:p-6 backdrop-blur-xs animate-overlay-fade">
          <div
            role="dialog"
            aria-modal="true"
            className="w-full max-w-4xl h-[80vh] flex flex-col rounded-lg border bg-background shadow-2xl overflow-hidden animate-dialog-pop"
          >
            {/* Modal Header */}
            <div className="flex items-center justify-between border-b px-4 py-3 bg-muted/30">
              <div className="space-y-0.5">
                <div className="flex items-center gap-2">
                  <Database className="h-4 w-4 text-primary" />
                  <h3 className="text-sm font-semibold text-foreground">SQL Query Editor</h3>
                  <span className="text-[10px] font-mono uppercase bg-muted px-1.5 py-0.5 rounded text-muted-foreground">
                    DuckDB
                  </span>
                </div>
                <p className="text-xs text-muted-foreground">
                  Reference upstream connected nodes directly by table name.
                </p>
              </div>

              <div className="flex items-center gap-1.5">
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  className="h-7 text-xs"
                  onClick={handleFormat}
                >
                  <Sparkles className="h-3.5 w-3.5 mr-1" />
                  Format SQL
                </Button>

                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  className="h-7 text-xs"
                  onClick={handleCopy}
                >
                  {copied ? (
                    <>
                      <Check className="h-3.5 w-3.5 mr-1 text-emerald-500" />
                      Copied
                    </>
                  ) : (
                    <>
                      <Copy className="h-3.5 w-3.5 mr-1" />
                      Copy
                    </>
                  )}
                </Button>

                <Button
                  type="button"
                  variant="default"
                  size="sm"
                  className="h-7 text-xs"
                  onClick={() => setIsFullscreen(false)}
                >
                  <Minimize2 className="h-3.5 w-3.5 mr-1" />
                  Done
                </Button>
              </div>
            </div>

            {/* Quick Actions & Upstream Tables */}
            <div className="border-b px-4 py-2 bg-muted/10 flex flex-wrap items-center justify-between gap-2 text-xs">
              {/* Upstream Tables */}
              <div className="flex items-center gap-1.5 flex-wrap">
                <span className="text-[11px] font-medium text-muted-foreground">
                  Upstream Tables:
                </span>
                {upstreamNodes.length > 0 ? (
                  upstreamNodes.map(node => (
                    <button
                      key={node.id}
                      type="button"
                      onClick={() => handleInsert(node.id, true)}
                      title={`Insert table "${node.id}"`}
                      className="inline-flex items-center gap-1 px-2 py-0.5 rounded border bg-background font-mono text-[11px] hover:border-primary/60 hover:bg-primary/5 transition-colors"
                    >
                      <span>{node.id}</span>
                      <Plus className="h-3 w-3 text-muted-foreground" />
                    </button>
                  ))
                ) : (
                  <span className="text-[11px] text-muted-foreground italic">
                    None connected (connect an incoming node to query it)
                  </span>
                )}
              </div>

              {/* Quick Snippets */}
              <div className="flex items-center gap-1">
                <span className="text-[11px] font-medium text-muted-foreground mr-1">
                  Snippets:
                </span>
                <button
                  type="button"
                  onClick={() => handleInsert("SELECT * FROM ", true)}
                  className="text-[10px] font-mono px-1.5 py-0.5 rounded border bg-background hover:bg-muted"
                >
                  SELECT *
                </button>
                <button
                  type="button"
                  onClick={() => handleInsert("\nWHERE ", true)}
                  className="text-[10px] font-mono px-1.5 py-0.5 rounded border bg-background hover:bg-muted"
                >
                  WHERE
                </button>
                <button
                  type="button"
                  onClick={() => handleInsert("\nGROUP BY ", true)}
                  className="text-[10px] font-mono px-1.5 py-0.5 rounded border bg-background hover:bg-muted"
                >
                  GROUP BY
                </button>
                <button
                  type="button"
                  onClick={() => handleInsert("\nORDER BY ", true)}
                  className="text-[10px] font-mono px-1.5 py-0.5 rounded border bg-background hover:bg-muted"
                >
                  ORDER BY
                </button>
              </div>
            </div>

            {/* Modal Editor Area */}
            <div className="relative flex-1 flex bg-background font-mono text-xs overflow-hidden">
              {/* Line Numbers Gutter */}
              <div
                ref={modalLineNumbersRef}
                aria-hidden="true"
                className="w-10 shrink-0 select-none border-r bg-muted/20 py-3 text-right pr-2.5 text-xs text-muted-foreground/50 overflow-hidden leading-6 font-mono"
              >
                {Array.from({ length: lineCount }).map((_, i) => (
                  <div key={i}>{i + 1}</div>
                ))}
              </div>

              {/* Large Text Area */}
              <textarea
                ref={modalTextareaRef}
                value={value}
                onChange={e => onChange(e.target.value)}
                onKeyDown={e => handleKeyDown(e, true)}
                onScroll={() =>
                  handleScroll(
                    modalTextareaRef.current,
                    modalLineNumbersRef.current
                  )
                }
                placeholder="SELECT * FROM input_table WHERE ..."
                spellCheck={false}
                autoFocus
                className="flex-1 bg-transparent p-3 text-xs leading-6 font-mono text-foreground outline-none focus:ring-0 border-0 placeholder:text-muted-foreground/40 whitespace-pre overflow-auto"
              />
            </div>

            {/* Modal Footer */}
            <div className="flex items-center justify-between border-t px-4 py-2 text-xs text-muted-foreground bg-muted/20 font-mono">
              <div className="flex items-center gap-3">
                <span>{lineCount} lines</span>
                <span>{value.length} characters</span>
              </div>
              <div className="flex items-center gap-3 text-[11px]">
                <span>Tab / Shift+Tab to indent/dedent</span>
                <span>Auto-indents on Enter</span>
                <span>Esc to close</span>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
