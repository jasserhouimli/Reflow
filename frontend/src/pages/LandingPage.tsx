import { useState, useEffect } from "react"
import { Button } from "@/components/ui/button"
import {
  Workflow,
  ArrowRight,
  Sparkles,
  Code2,
  Clock,
  Radio,
  FileSpreadsheet,
  Filter,
  Download,
  Play,
  ExternalLink,
  UploadCloud
} from "lucide-react"

interface LandingPageProps {
  onGetStarted: () => void
  isLoggedIn?: boolean
}

export function LandingPage({ onGetStarted, isLoggedIn }: LandingPageProps) {
  useEffect(() => {
    document.title = "Reflow · Visual Data Pipeline Platform"
  }, [])

  // Interactive tab in the hero showcase: 'canvas' | 'ai' | 'execution'
  const [heroTab, setHeroTab] = useState<"canvas" | "ai" | "execution">("canvas")

  // Interactive AI prompt in the AI section
  const [selectedPromptIndex, setSelectedPromptIndex] = useState(0)

  // Interactive Node category tab
  const [activeCategory, setActiveCategory] = useState<"sources" | "shaping" | "analytics" | "sinks">("sources")

  const aiExamples = [
    {
      title: "E-Commerce Sales Analysis",
      prompt: "Read sales data from a CSV file, filter for orders over 100 dollars, calculate total revenue per category using SQL, and export the result to CSV.",
      nodes: [
        { name: "Orders CSV", type: "file.read", desc: "Upload orders_2026.csv" },
        { name: "Filter Orders", type: "filter", desc: "amount >= 100" },
        { name: "Revenue per Category", type: "data.sql", desc: "SELECT category, SUM(amount)..." },
        { name: "Export Report", type: "data.output", desc: "Download as CSV" }
      ]
    },
    {
      title: "Customer Inactivity Alert",
      prompt: "Import customer activity from JSON, aggregate total logins per user, find users inactive for more than 30 days, and output high-risk accounts.",
      nodes: [
        { name: "User Telemetry", type: "json.read", desc: "Parse activity stream" },
        { name: "Login Aggregate", type: "aggregate", desc: "COUNT logins by user_id" },
        { name: "Sort Inactive", type: "sort", desc: "days_since_active DESC" },
        { name: "Alert Output", type: "data.output", desc: "Export flagged users" }
      ]
    },
    {
      title: "Webhook Ingestion & Deduplication",
      prompt: "Capture Stripe payment webhooks, extract the customer and charge details, filter for successful payments, and compile daily revenue totals.",
      nodes: [
        { name: "Payment Webhook", type: "trigger.payload", desc: "Live HTTP webhook event" },
        { name: "Clean Fields", type: "transform", desc: "Select customer, amount, status" },
        { name: "Filter Succeeded", type: "filter", desc: "status == 'succeeded'" },
        { name: "Daily Revenue", type: "data.sql", desc: "GROUP BY date" }
      ]
    }
  ]

  const nodeCatalog = {
    sources: {
      title: "Source Nodes",
      subtitle: "Bring your data in from files, documents, or real-time webhooks.",
      items: [
        {
          name: "File Read",
          badge: "file.read",
          desc: "Upload CSV or JSON files with drag-and-drop. Automatically detects headers, row counts, and delimiters."
        },
        {
          name: "CSV Read",
          badge: "csv.read",
          desc: "Paste or edit raw inline CSV data directly in the browser with instant tabular formatting."
        },
        {
          name: "JSON Read",
          badge: "json.read",
          desc: "Load structured JSON objects or unnest arrays into relational columns ready for analysis."
        },
        {
          name: "Trigger Payload",
          badge: "trigger.payload",
          desc: "Capture incoming payloads from automated schedule triggers or external HTTP webhooks."
        }
      ]
    },
    shaping: {
      title: "Shaping Nodes",
      subtitle: "Clean, filter, and organize columns before running calculations.",
      items: [
        {
          name: "Filter",
          badge: "filter",
          desc: "Keep only the rows you need using simple operators like equals, greater than, contains, or is-null."
        },
        {
          name: "Transform",
          badge: "transform",
          desc: "Select specific fields, exclude sensitive columns, or quickly rename attributes without writing code."
        },
        {
          name: "Sort",
          badge: "sort",
          desc: "Order your data by one or multiple columns in ascending or descending order."
        }
      ]
    },
    analytics: {
      title: "Analytics Nodes",
      subtitle: "Perform high-speed computations, group-bys, and relational joins.",
      items: [
        {
          name: "SQL Query",
          badge: "data.sql",
          desc: "Write standard SQL directly in your pipeline. Upstream nodes are automatically queryable tables."
        },
        {
          name: "Aggregate",
          badge: "aggregate",
          desc: "Calculate categorical totals with count, sum, average, minimum, maximum, and distinct metrics."
        },
        {
          name: "Join",
          badge: "join",
          desc: "Combine two separate tables together on shared keys using inner or left outer joins."
        }
      ]
    },
    sinks: {
      title: "Output Sinks",
      subtitle: "Save and download your cleaned, transformed results.",
      items: [
        {
          name: "Data Output",
          badge: "data.output",
          desc: "Persist final pipeline outputs as clean CSV or JSON files ready for one-click browser download."
        }
      ]
    }
  }

  return (
    <div className="min-h-screen bg-background text-foreground flex flex-col font-sans selection:bg-foreground selection:text-background">
      {/* ────────────────────────────────────────────────────────────
          NAVIGATION
          ──────────────────────────────────────────────────────────── */}
      <header className="sticky top-0 z-40 w-full border-b border-border/80 bg-background/95 backdrop-blur-sm">
        <div className="max-w-6xl mx-auto px-4 sm:px-6 h-14 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="h-7 w-7 rounded-md bg-foreground text-background flex items-center justify-center font-bold">
              <Workflow className="h-4 w-4" />
            </div>
            <div className="flex items-center gap-2">
              <span className="font-semibold text-sm tracking-tight">Reflow</span>
              <span className="text-[10px] font-mono text-muted-foreground border border-border/70 rounded px-1.5 py-0.5 bg-muted/40">
                Platform
              </span>
            </div>
          </div>

          <nav className="hidden md:flex items-center gap-6 text-xs text-muted-foreground">
            <a href="#features" className="hover:text-foreground transition-colors">
              Features
            </a>
            <a href="#ai-assistant" className="hover:text-foreground transition-colors">
              AI Assistant
            </a>
            <a href="#nodes" className="hover:text-foreground transition-colors">
              Node Catalog
            </a>
            <a href="#automation" className="hover:text-foreground transition-colors">
              Automation
            </a>
            <a href="#how-it-works" className="hover:text-foreground transition-colors">
              How It Works
            </a>
          </nav>

          <div className="flex items-center gap-3">
            <a
              href="https://github.com/jasserhouimli/Reflow"
              target="_blank"
              rel="noreferrer"
              className="hidden sm:inline-flex items-center gap-1.5 text-xs text-muted-foreground hover:text-foreground transition-colors"
            >
              <span>GitHub</span>
              <ExternalLink className="h-3 w-3" />
            </a>
            <Button
              size="sm"
              onClick={onGetStarted}
              className="h-8 px-3.5 text-xs font-medium tracking-tight gap-1.5 bg-foreground text-background hover:bg-foreground/90 active:scale-[0.985] transition-all cursor-pointer shadow-xs"
            >
              <span>{isLoggedIn ? "Open Dashboard" : "Open Workspace"}</span>
              <ArrowRight className="h-3.5 w-3.5" />
            </Button>
          </div>
        </div>
      </header>

      {/* ────────────────────────────────────────────────────────────
          HERO SECTION: Clean, focused, showcase-first
          ──────────────────────────────────────────────────────────── */}
      <section className="pt-16 pb-12 sm:pt-20 sm:pb-16 border-b border-border/70">
        <div className="max-w-4xl mx-auto px-4 sm:px-6 text-center">
          <div className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full border border-border/80 bg-muted/40 text-[11px] font-medium text-muted-foreground mb-6">
            <span>Visual Data Pipeline Platform</span>
          </div>

          <h1 className="text-3xl sm:text-5xl font-bold tracking-tight text-foreground leading-[1.15]">
            Build, automate, and run data pipelines visually.
          </h1>

          <p className="mt-4 text-sm sm:text-base text-muted-foreground max-w-2xl mx-auto leading-relaxed">
            Reflow makes transforming and orchestrating data simple. Drag nodes onto an interactive canvas, connect them with arrows, run in-process SQL analytics, or let AI generate complete workflows from a single prompt.
          </p>

          <div className="mt-7 flex flex-wrap items-center justify-center gap-3">
            <Button
              size="lg"
              onClick={onGetStarted}
              className="h-10 px-5 text-xs sm:text-sm font-medium tracking-tight gap-2 bg-foreground text-background hover:bg-foreground/90 active:scale-[0.985] transition-all cursor-pointer shadow-xs"
            >
              <span>{isLoggedIn ? "Go to Dashboard" : "Launch Reflow Workspace"}</span>
              <ArrowRight className="h-4 w-4" />
            </Button>
            <a
              href="#features"
              className="inline-flex items-center gap-2 h-10 px-4 rounded-md border border-border/80 bg-background hover:bg-muted/40 text-xs sm:text-sm font-medium text-foreground transition-all"
            >
              <span>Explore Features</span>
            </a>
          </div>
        </div>

        {/* ────────────────────────────────────────────────────────────
            INTERACTIVE PRODUCT SHOWCASE WINDOW
            ──────────────────────────────────────────────────────────── */}
        <div className="max-w-5xl mx-auto px-4 sm:px-6 mt-12">
          <div className="rounded-xl border border-border/80 bg-card shadow-sm overflow-hidden">
            {/* Window Top Bar with View Mode Switchers */}
            <div className="px-4 py-3 border-b border-border/80 bg-muted/30 flex flex-wrap items-center justify-between gap-3 text-xs">
              <div className="flex items-center gap-2">
                <span className="h-2.5 w-2.5 rounded-full bg-border" />
                <span className="h-2.5 w-2.5 rounded-full bg-border" />
                <span className="h-2.5 w-2.5 rounded-full bg-border" />
                <span className="text-muted-foreground ml-2 font-mono text-[11px]">Reflow Workspace</span>
              </div>

              {/* View Selector Tabs */}
              <div className="flex items-center gap-1 bg-background p-1 rounded-md border border-border/70">
                <button
                  onClick={() => setHeroTab("canvas")}
                  className={`px-3 py-1 rounded text-xs font-medium transition-colors cursor-pointer ${
                    heroTab === "canvas" ? "bg-muted text-foreground font-semibold" : "text-muted-foreground hover:text-foreground"
                  }`}
                >
                  Visual Canvas
                </button>
                <button
                  onClick={() => setHeroTab("ai")}
                  className={`px-3 py-1 rounded text-xs font-medium transition-colors cursor-pointer flex items-center gap-1.5 ${
                    heroTab === "ai" ? "bg-muted text-foreground font-semibold" : "text-muted-foreground hover:text-foreground"
                  }`}
                >
                  <Sparkles className="h-3 w-3" />
                  <span>AI Generator</span>
                </button>
                <button
                  onClick={() => setHeroTab("execution")}
                  className={`px-3 py-1 rounded text-xs font-medium transition-colors cursor-pointer ${
                    heroTab === "execution" ? "bg-muted text-foreground font-semibold" : "text-muted-foreground hover:text-foreground"
                  }`}
                >
                  Live Run & Output
                </button>
              </div>
            </div>

            {/* TAB 1: VISUAL CANVAS PREVIEW */}
            {heroTab === "canvas" && (
              <div className="p-6 sm:p-10 bg-background/50 flex flex-col items-center justify-center min-h-[380px]">
                <div className="w-full max-w-4xl grid grid-cols-1 md:grid-cols-4 gap-4 relative">
                  {/* Step 1 */}
                  <div className="rounded-lg border border-border/80 bg-card p-4 shadow-2xs space-y-3">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-2">
                        <FileSpreadsheet className="h-4 w-4 text-foreground" />
                        <span className="text-xs font-semibold">Orders Data</span>
                      </div>
                      <span className="text-[10px] font-mono px-1.5 py-0.5 rounded bg-muted/60 text-muted-foreground border">
                        file.read
                      </span>
                    </div>
                    <p className="text-[11px] text-muted-foreground">
                      Reads uploaded <code>orders.csv</code> (4,820 rows) with auto-detected headers.
                    </p>
                    <div className="pt-2 border-t text-[10px] text-muted-foreground font-mono flex items-center justify-between">
                      <span>Source</span>
                      <span className="text-foreground">Output: (orders)</span>
                    </div>
                  </div>

                  {/* Step 2 */}
                  <div className="rounded-lg border border-border/80 bg-card p-4 shadow-2xs space-y-3">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-2">
                        <Filter className="h-4 w-4 text-foreground" />
                        <span className="text-xs font-semibold">Filter Orders</span>
                      </div>
                      <span className="text-[10px] font-mono px-1.5 py-0.5 rounded bg-muted/60 text-muted-foreground border">
                        filter
                      </span>
                    </div>
                    <p className="text-[11px] text-muted-foreground">
                      Filters for <code>amount &gt;= 100</code> to isolate high-value purchases.
                    </p>
                    <div className="pt-2 border-t text-[10px] text-muted-foreground font-mono flex items-center justify-between">
                      <span>Shaping</span>
                      <span className="text-foreground">1,290 rows kept</span>
                    </div>
                  </div>

                  {/* Step 3 */}
                  <div className="rounded-lg border border-border/80 bg-card p-4 shadow-2xs space-y-3">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-2">
                        <Code2 className="h-4 w-4 text-foreground" />
                        <span className="text-xs font-semibold">SQL Analytics</span>
                      </div>
                      <span className="text-[10px] font-mono px-1.5 py-0.5 rounded bg-muted/60 text-muted-foreground border">
                        data.sql
                      </span>
                    </div>
                    <p className="text-[11px] text-muted-foreground">
                      Calculates revenue and totals grouped by product category.
                    </p>
                    <div className="pt-2 border-t text-[10px] text-muted-foreground font-mono flex items-center justify-between">
                      <span>Analytics</span>
                      <span className="text-foreground">In-process SQL</span>
                    </div>
                  </div>

                  {/* Step 4 */}
                  <div className="rounded-lg border border-border/80 bg-card p-4 shadow-2xs space-y-3">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-2">
                        <Download className="h-4 w-4 text-foreground" />
                        <span className="text-xs font-semibold">Download CSV</span>
                      </div>
                      <span className="text-[10px] font-mono px-1.5 py-0.5 rounded bg-muted/60 text-muted-foreground border">
                        data.output
                      </span>
                    </div>
                    <p className="text-[11px] text-muted-foreground">
                      Saves the clean summary report directly as a downloadable CSV artifact.
                    </p>
                    <div className="pt-2 border-t text-[10px] text-muted-foreground font-mono flex items-center justify-between">
                      <span>Sink</span>
                      <span className="text-foreground">One-click save</span>
                    </div>
                  </div>
                </div>

                <div className="mt-6 flex items-center gap-2 text-xs text-muted-foreground">
                  <span className="h-1.5 w-1.5 rounded-full bg-foreground" />
                  <span>Interactive canvas: drag nodes from palette, connect handles, and configure inline.</span>
                </div>
              </div>
            )}

            {/* TAB 2: AI WORKFLOW GENERATOR PREVIEW */}
            {heroTab === "ai" && (
              <div className="p-6 sm:p-10 bg-background/50 flex flex-col items-center justify-center min-h-[380px]">
                <div className="w-full max-w-2xl rounded-lg border border-border/80 bg-card p-5 shadow-2xs space-y-4">
                  <div className="flex items-center justify-between pb-3 border-b">
                    <div className="flex items-center gap-2">
                      <Sparkles className="h-4 w-4 text-foreground" />
                      <span className="text-xs font-semibold">AI Workflow Generator</span>
                    </div>
                    <span className="text-[10px] font-mono text-muted-foreground">Text-to-Pipeline</span>
                  </div>

                  <div className="p-3 rounded-md bg-muted/30 border border-border/60 text-xs text-foreground font-mono leading-relaxed">
                    &ldquo;Read sales data from a CSV file, filter for orders over 100 dollars, calculate total revenue per category using SQL, and export the result to CSV&rdquo;
                  </div>

                  <div className="space-y-2">
                    <span className="text-[11px] font-medium text-muted-foreground">
                      Generated Pipeline on Canvas:
                    </span>
                    <div className="grid grid-cols-2 sm:grid-cols-4 gap-2 text-xs">
                      <div className="p-2 rounded border bg-background text-center">
                        <div className="text-[10px] text-muted-foreground font-mono">Node 1</div>
                        <div className="font-semibold mt-0.5">Orders CSV</div>
                      </div>
                      <div className="p-2 rounded border bg-background text-center">
                        <div className="text-[10px] text-muted-foreground font-mono">Node 2</div>
                        <div className="font-semibold mt-0.5">Filter (&gt; 100)</div>
                      </div>
                      <div className="p-2 rounded border bg-background text-center">
                        <div className="text-[10px] text-muted-foreground font-mono">Node 3</div>
                        <div className="font-semibold mt-0.5">SQL Revenue</div>
                      </div>
                      <div className="p-2 rounded border bg-background text-center">
                        <div className="text-[10px] text-muted-foreground font-mono">Node 4</div>
                        <div className="font-semibold mt-0.5">Export CSV</div>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            )}

            {/* TAB 3: LIVE EXECUTION & OUTPUT PREVIEW */}
            {heroTab === "execution" && (
              <div className="p-6 sm:p-10 bg-background/50 flex flex-col items-center justify-center min-h-[380px]">
                <div className="w-full max-w-3xl rounded-lg border border-border/80 bg-card overflow-hidden shadow-2xs">
                  <div className="p-3 border-b bg-muted/20 flex items-center justify-between text-xs">
                    <div className="flex items-center gap-2">
                      <span className="h-2 w-2 rounded-full bg-foreground" />
                      <span className="font-semibold">Run Results & Output Table</span>
                    </div>
                    <span className="text-[11px] font-mono text-muted-foreground">Status: Completed · 4 rows</span>
                  </div>

                  <div className="overflow-x-auto">
                    <table className="w-full text-xs text-left">
                      <thead className="bg-muted/40 border-b text-[11px] text-muted-foreground font-mono">
                        <tr>
                          <th className="py-2 px-4">Category</th>
                          <th className="py-2 px-4">Total Orders</th>
                          <th className="py-2 px-4">Total Revenue</th>
                          <th className="py-2 px-4">Avg Order Value</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-border/60">
                        <tr>
                          <td className="py-2 px-4 font-medium">Electronics</td>
                          <td className="py-2 px-4 font-mono">482</td>
                          <td className="py-2 px-4 font-mono">$142,390.00</td>
                          <td className="py-2 px-4 font-mono">$295.41</td>
                        </tr>
                        <tr>
                          <td className="py-2 px-4 font-medium">Home & Office</td>
                          <td className="py-2 px-4 font-mono">318</td>
                          <td className="py-2 px-4 font-mono">$78,120.50</td>
                          <td className="py-2 px-4 font-mono">$245.66</td>
                        </tr>
                        <tr>
                          <td className="py-2 px-4 font-medium">Apparel</td>
                          <td className="py-2 px-4 font-mono">290</td>
                          <td className="py-2 px-4 font-mono">$41,890.00</td>
                          <td className="py-2 px-4 font-mono">$144.45</td>
                        </tr>
                        <tr>
                          <td className="py-2 px-4 font-medium">Outdoor Gear</td>
                          <td className="py-2 px-4 font-mono">200</td>
                          <td className="py-2 px-4 font-mono">$28,500.00</td>
                          <td className="py-2 px-4 font-mono">$142.50</td>
                        </tr>
                      </tbody>
                    </table>
                  </div>

                  <div className="p-3 border-t bg-muted/10 flex items-center justify-between text-xs">
                    <span className="text-muted-foreground font-mono text-[11px]">Artifact: category_revenue.csv</span>
                    <button className="inline-flex items-center gap-1.5 px-3 py-1 rounded border border-border/80 bg-background hover:bg-muted font-medium text-xs cursor-pointer shadow-xs">
                      <Download className="h-3.5 w-3.5" />
                      <span>Download CSV</span>
                    </button>
                  </div>
                </div>
              </div>
            )}
          </div>
        </div>
      </section>

      {/* ────────────────────────────────────────────────────────────
          CORE PLATFORM FEATURES
          ──────────────────────────────────────────────────────────── */}
      <section id="features" className="py-16 sm:py-20 border-b border-border/70">
        <div className="max-w-6xl mx-auto px-4 sm:px-6">
          <div className="max-w-2xl mb-12">
            <span className="text-xs font-mono uppercase tracking-wider text-muted-foreground block mb-2">
              Platform Features
            </span>
            <h2 className="text-2xl sm:text-3xl font-bold tracking-tight text-foreground">
              Everything you need to design and automate data workflows.
            </h2>
            <p className="text-sm text-muted-foreground mt-2 leading-relaxed">
              From visual drag-and-drop construction to automated scheduling and high-speed in-browser SQL queries, Reflow gives you complete control over your data.
            </p>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {/* Feature 1 */}
            <div className="rounded-lg border border-border/80 bg-card p-6 shadow-2xs space-y-3">
              <div className="h-9 w-9 rounded-md bg-muted flex items-center justify-center">
                <Workflow className="h-5 w-5 text-foreground" />
              </div>
              <h3 className="text-base font-semibold tracking-tight text-foreground">
                Visual DAG Canvas
              </h3>
              <p className="text-xs text-muted-foreground leading-relaxed">
                Drag nodes from the catalog onto the canvas. Connect output handles to inputs, rearrange steps, and configure node settings in a clean side drawer.
              </p>
            </div>

            {/* Feature 2 */}
            <div className="rounded-lg border border-border/80 bg-card p-6 shadow-2xs space-y-3">
              <div className="h-9 w-9 rounded-md bg-muted flex items-center justify-center">
                <Sparkles className="h-5 w-5 text-foreground" />
              </div>
              <h3 className="text-base font-semibold tracking-tight text-foreground">
                AI Workflow Generator
              </h3>
              <p className="text-xs text-muted-foreground leading-relaxed">
                Describe the pipeline you want in plain English. The AI assistant drafts the nodes, sets up their parameters, and connects the arrows automatically.
              </p>
            </div>

            {/* Feature 3 */}
            <div className="rounded-lg border border-border/80 bg-card p-6 shadow-2xs space-y-3">
              <div className="h-9 w-9 rounded-md bg-muted flex items-center justify-center">
                <Code2 className="h-5 w-5 text-foreground" />
              </div>
              <h3 className="text-base font-semibold tracking-tight text-foreground">
                In-Process SQL Analytics
              </h3>
              <p className="text-xs text-muted-foreground leading-relaxed">
                Write standard SQL queries directly inside your workflow without managing database servers. Run calculations, groupings, and window functions with instant execution.
              </p>
            </div>

            {/* Feature 4 */}
            <div className="rounded-lg border border-border/80 bg-card p-6 shadow-2xs space-y-3">
              <div className="h-9 w-9 rounded-md bg-muted flex items-center justify-center">
                <UploadCloud className="h-5 w-5 text-foreground" />
              </div>
              <h3 className="text-base font-semibold tracking-tight text-foreground">
                File Ingestion & Dropzone
              </h3>
              <p className="text-xs text-muted-foreground leading-relaxed">
                Drag and drop CSV or JSON files straight into the File Read node. Reflow automatically detects column headers, row counts, and delimiters (comma, tab, semicolon).
              </p>
            </div>

            {/* Feature 5 */}
            <div className="rounded-lg border border-border/80 bg-card p-6 shadow-2xs space-y-3">
              <div className="h-9 w-9 rounded-md bg-muted flex items-center justify-center">
                <Clock className="h-5 w-5 text-foreground" />
              </div>
              <h3 className="text-base font-semibold tracking-tight text-foreground">
                Schedules & Webhooks
              </h3>
              <p className="text-xs text-muted-foreground leading-relaxed">
                Run pipelines automatically on cron schedules (hourly, daily, or custom intervals) or trigger them via unique webhook URLs from external platforms like Stripe or Shopify.
              </p>
            </div>

            {/* Feature 6 */}
            <div className="rounded-lg border border-border/80 bg-card p-6 shadow-2xs space-y-3">
              <div className="h-9 w-9 rounded-md bg-muted flex items-center justify-center">
                <Download className="h-5 w-5 text-foreground" />
              </div>
              <h3 className="text-base font-semibold tracking-tight text-foreground">
                Live Output & 1-Click Export
              </h3>
              <p className="text-xs text-muted-foreground leading-relaxed">
                Watch tasks execute in real time with live streaming logs. Preview output tables in the bottom drawer and download clean CSV or JSON files with a single click.
              </p>
            </div>
          </div>
        </div>
      </section>

      {/* ────────────────────────────────────────────────────────────
          AI WORKFLOW ASSISTANT SHOWCASE
          ──────────────────────────────────────────────────────────── */}
      <section id="ai-assistant" className="py-16 sm:py-20 border-b border-border/70 bg-muted/20">
        <div className="max-w-6xl mx-auto px-4 sm:px-6">
          <div className="max-w-2xl mb-8">
            <span className="text-xs font-mono uppercase tracking-wider text-muted-foreground block mb-2">
              Natural Language Workflows
            </span>
            <h2 className="text-2xl sm:text-3xl font-bold tracking-tight text-foreground">
              Generate entire pipelines from plain English prompts.
            </h2>
            <p className="text-sm text-muted-foreground mt-2 leading-relaxed">
              Instead of manually picking and configuring nodes from scratch, type what you want to achieve. Reflow designs the pipeline layout, configures parameters, and draws the connections for you.
            </p>
          </div>

          <div className="rounded-xl border border-border/80 bg-card p-6 shadow-2xs space-y-6">
            {/* Example Prompt Switchers */}
            <div className="flex flex-wrap gap-2">
              {aiExamples.map((item, idx) => (
                <button
                  key={item.title}
                  onClick={() => setSelectedPromptIndex(idx)}
                  className={`px-3 py-1.5 rounded-md text-xs font-medium transition-colors cursor-pointer ${
                    selectedPromptIndex === idx
                      ? "bg-foreground text-background"
                      : "border border-border/80 bg-background text-muted-foreground hover:text-foreground"
                  }`}
                >
                  {item.title}
                </button>
              ))}
            </div>

            {/* Prompt Box */}
            <div className="p-4 rounded-lg bg-muted/40 border border-border/60 space-y-2">
              <div className="text-[11px] font-medium text-muted-foreground">Sample User Prompt:</div>
              <p className="text-xs sm:text-sm text-foreground font-mono leading-relaxed">
                &ldquo;{aiExamples[selectedPromptIndex].prompt}&rdquo;
              </p>
            </div>

            {/* Output Steps */}
            <div>
              <div className="text-xs font-medium text-muted-foreground mb-3">
                Automatically Generated DAG Nodes:
              </div>
              <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 gap-3">
                {aiExamples[selectedPromptIndex].nodes.map((node, i) => (
                  <div key={node.name} className="p-3 rounded-lg border border-border/80 bg-background space-y-1">
                    <div className="flex items-center justify-between">
                      <span className="text-[10px] font-mono text-muted-foreground">Step {i + 1}</span>
                      <span className="text-[10px] font-mono px-1 rounded bg-muted text-muted-foreground">
                        {node.type}
                      </span>
                    </div>
                    <div className="text-xs font-semibold text-foreground pt-1">{node.name}</div>
                    <p className="text-[11px] text-muted-foreground leading-normal">{node.desc}</p>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* ────────────────────────────────────────────────────────────
          NODE CATALOG (4 SIMPLE CATEGORIES)
          ──────────────────────────────────────────────────────────── */}
      <section id="nodes" className="py-16 sm:py-20 border-b border-border/70">
        <div className="max-w-6xl mx-auto px-4 sm:px-6">
          <div className="max-w-2xl mb-8">
            <span className="text-xs font-mono uppercase tracking-wider text-muted-foreground block mb-2">
              Node Catalog
            </span>
            <h2 className="text-2xl sm:text-3xl font-bold tracking-tight text-foreground">
              Specialized nodes for every stage of your data.
            </h2>
            <p className="text-sm text-muted-foreground mt-2 leading-relaxed">
              Reflow organizes nodes into four straightforward categories: Sources, Shaping, Analytics, and Sinks.
            </p>
          </div>

          {/* Category Tabs */}
          <div className="flex items-center gap-2 border-b pb-3 mb-6 overflow-x-auto">
            {(["sources", "shaping", "analytics", "sinks"] as const).map(cat => (
              <button
                key={cat}
                onClick={() => setActiveCategory(cat)}
                className={`px-3 py-1.5 rounded-md text-xs font-medium capitalize transition-colors cursor-pointer ${
                  activeCategory === cat
                    ? "bg-foreground text-background"
                    : "text-muted-foreground hover:text-foreground hover:bg-muted/50"
                }`}
              >
                {cat}
              </button>
            ))}
          </div>

          {/* Active Category Display */}
          <div className="space-y-4">
            <div>
              <h3 className="text-base font-semibold text-foreground">
                {nodeCatalog[activeCategory].title}
              </h3>
              <p className="text-xs text-muted-foreground mt-0.5">
                {nodeCatalog[activeCategory].subtitle}
              </p>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4 pt-2">
              {nodeCatalog[activeCategory].items.map(item => (
                <div
                  key={item.name}
                  className="rounded-lg border border-border/80 bg-card p-4 shadow-2xs space-y-2 hover:border-foreground/30 transition-colors"
                >
                  <div className="flex items-center justify-between">
                    <span className="text-xs font-semibold text-foreground">{item.name}</span>
                    <span className="text-[10px] font-mono px-1.5 py-0.5 rounded bg-muted text-muted-foreground border">
                      {item.badge}
                    </span>
                  </div>
                  <p className="text-xs text-muted-foreground leading-relaxed">{item.desc}</p>
                </div>
              ))}
            </div>
          </div>
        </div>
      </section>

      {/* ────────────────────────────────────────────────────────────
          AUTOMATION & EXECUTION
          ──────────────────────────────────────────────────────────── */}
      <section id="automation" className="py-16 sm:py-20 border-b border-border/70 bg-muted/20">
        <div className="max-w-6xl mx-auto px-4 sm:px-6">
          <div className="max-w-2xl mb-12">
            <span className="text-xs font-mono uppercase tracking-wider text-muted-foreground block mb-2">
              Flexible Automation
            </span>
            <h2 className="text-2xl sm:text-3xl font-bold tracking-tight text-foreground">
              Run on demand, on a schedule, or from external triggers.
            </h2>
            <p className="text-sm text-muted-foreground mt-2 leading-relaxed">
              Keep your data synchronized automatically without manual intervention.
            </p>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
            <div className="rounded-lg border border-border/80 bg-card p-6 shadow-2xs space-y-3">
              <div className="h-8 w-8 rounded-md bg-muted flex items-center justify-center font-bold">
                <Clock className="h-4 w-4 text-foreground" />
              </div>
              <h3 className="text-sm font-semibold text-foreground">Automated Cron Scheduler</h3>
              <p className="text-xs text-muted-foreground leading-relaxed">
                Set pipelines to execute on a recurring schedule — such as every hour, daily at midnight, or custom intervals with full timezone support.
              </p>
              <div className="text-[11px] font-mono text-muted-foreground pt-1">
                <code>0 * * * * (Every hour)</code>
              </div>
            </div>

            <div className="rounded-lg border border-border/80 bg-card p-6 shadow-2xs space-y-3">
              <div className="h-8 w-8 rounded-md bg-muted flex items-center justify-center font-bold">
                <Radio className="h-4 w-4 text-foreground" />
              </div>
              <h3 className="text-sm font-semibold text-foreground">Webhook Triggers</h3>
              <p className="text-xs text-muted-foreground leading-relaxed">
                Generate dedicated webhook URLs so external apps like Stripe, GitHub, or Shopify can trigger your pipeline whenever new data arrives.
              </p>
              <div className="text-[11px] font-mono text-muted-foreground pt-1">
                <code>POST /api/webhooks/&#123;id&#125;</code>
              </div>
            </div>

            <div className="rounded-lg border border-border/80 bg-card p-6 shadow-2xs space-y-3">
              <div className="h-8 w-8 rounded-md bg-muted flex items-center justify-center font-bold">
                <Play className="h-4 w-4 text-foreground" />
              </div>
              <h3 className="text-sm font-semibold text-foreground">Interactive On-Demand Runs</h3>
              <p className="text-xs text-muted-foreground leading-relaxed">
                Click &ldquo;Run Test&rdquo; at any time while building to watch task execution live. Inspect intermediate tables and view execution logs in real time.
              </p>
              <div className="text-[11px] font-mono text-muted-foreground pt-1">
                <span>Live streaming logs</span>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* ────────────────────────────────────────────────────────────
          HOW IT WORKS (3 SIMPLE STEPS)
          ──────────────────────────────────────────────────────────── */}
      <section id="how-it-works" className="py-16 sm:py-20 border-b border-border/70">
        <div className="max-w-6xl mx-auto px-4 sm:px-6">
          <div className="text-center max-w-2xl mx-auto mb-12">
            <span className="text-xs font-mono uppercase tracking-wider text-muted-foreground block mb-2">
              Simple Workflow
            </span>
            <h2 className="text-2xl sm:text-3xl font-bold tracking-tight text-foreground">
              From raw data to clean insights in 3 steps.
            </h2>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-8 text-center sm:text-left">
            <div className="space-y-3">
              <div className="h-8 w-8 rounded-full bg-foreground text-background flex items-center justify-center text-xs font-bold mx-auto sm:mx-0">
                1
              </div>
              <h3 className="text-sm font-semibold text-foreground">Build or Generate</h3>
              <p className="text-xs text-muted-foreground leading-relaxed">
                Drag nodes from the palette onto the canvas, or click &ldquo;Generate with AI&rdquo; to draft a complete connected pipeline from a text description.
              </p>
            </div>

            <div className="space-y-3">
              <div className="h-8 w-8 rounded-full bg-foreground text-background flex items-center justify-center text-xs font-bold mx-auto sm:mx-0">
                2
              </div>
              <h3 className="text-sm font-semibold text-foreground">Configure &amp; Query</h3>
              <p className="text-xs text-muted-foreground leading-relaxed">
                Upload your CSV/JSON data files, configure filters, and write standard SQL queries directly in the browser with automatic column detection.
              </p>
            </div>

            <div className="space-y-3">
              <div className="h-8 w-8 rounded-full bg-foreground text-background flex items-center justify-center text-xs font-bold mx-auto sm:mx-0">
                3
              </div>
              <h3 className="text-sm font-semibold text-foreground">Automate &amp; Download</h3>
              <p className="text-xs text-muted-foreground leading-relaxed">
                Set an hourly or daily schedule, hook up a webhook for external apps, and download your clean result files with one click.
              </p>
            </div>
          </div>
        </div>
      </section>

      {/* ────────────────────────────────────────────────────────────
          CALL TO ACTION
          ──────────────────────────────────────────────────────────── */}
      <section className="py-16 sm:py-20">
        <div className="max-w-3xl mx-auto px-4 sm:px-6 text-center space-y-6">
          <h2 className="text-2xl sm:text-4xl font-bold tracking-tight text-foreground">
            Ready to build your first data pipeline?
          </h2>
          <p className="text-xs sm:text-sm text-muted-foreground max-w-xl mx-auto leading-relaxed">
            Get started with Reflow today. Build pipelines visually, automate transformations, and export clean data without heavy infrastructure.
          </p>

          <div className="flex flex-wrap items-center justify-center gap-3 pt-2">
            <Button
              size="lg"
              onClick={onGetStarted}
              className="h-10 px-6 text-xs sm:text-sm font-medium tracking-tight gap-2 bg-foreground text-background hover:bg-foreground/90 active:scale-[0.985] transition-all cursor-pointer shadow-xs"
            >
              <span>{isLoggedIn ? "Open Dashboard" : "Open Workspace"}</span>
              <ArrowRight className="h-4 w-4" />
            </Button>
            <a
              href="https://github.com/jasserhouimli/Reflow"
              target="_blank"
              rel="noreferrer"
              className="inline-flex items-center gap-1.5 h-10 px-4 rounded-md border border-border/80 bg-background hover:bg-muted/40 text-xs sm:text-sm font-medium text-foreground transition-all"
            >
              <span>View on GitHub</span>
              <ExternalLink className="h-3 w-3" />
            </a>
          </div>
        </div>
      </section>

      {/* ────────────────────────────────────────────────────────────
          FOOTER
          ──────────────────────────────────────────────────────────── */}
      <footer className="py-8 bg-background border-t border-border/60 text-xs text-muted-foreground">
        <div className="max-w-6xl mx-auto px-4 sm:px-6 flex flex-col sm:flex-row items-center justify-between gap-4">
          <div className="flex items-center gap-2">
            <div className="h-6 w-6 rounded bg-foreground text-background flex items-center justify-center font-bold">
              <Workflow className="h-3.5 w-3.5" />
            </div>
            <span className="font-semibold text-foreground">Reflow</span>
            <span className="text-[11px] font-mono text-muted-foreground ml-2">
              Visual Data Pipeline Platform
            </span>
          </div>

          <div className="flex items-center gap-6 font-mono text-[11px]">
            <a
              href="https://github.com/jasserhouimli/Reflow"
              target="_blank"
              rel="noreferrer"
              className="hover:text-foreground transition-colors"
            >
              GitHub
            </a>
            <a
              href="https://github.com/jasserhouimli/Reflow/blob/main/README.md"
              target="_blank"
              rel="noreferrer"
              className="hover:text-foreground transition-colors"
            >
              Documentation
            </a>
          </div>
        </div>
      </footer>
    </div>
  )
}
