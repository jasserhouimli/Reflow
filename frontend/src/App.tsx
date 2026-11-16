import { useState, useEffect, Component, type ReactNode } from "react"
import { auth } from "@/api/client"
import { LoginPage } from "@/pages/LoginPage"
import { DashboardPage } from "@/pages/DashboardPage"
import { PipelineEditorPage } from "@/pages/PipelineEditorPage"
import { Skeleton } from "@/components/ui/skeleton"

class ErrorBoundary extends Component<{ children: ReactNode }, { error: Error | null }> {
  state = { error: null as Error | null }

  static getDerivedStateFromError(error: Error) {
    return { error }
  }

  render() {
    if (this.state.error) {
      return (
        <div className="min-h-screen flex items-center justify-center p-8">
          <div className="max-w-md text-center space-y-3">
            <h2 className="font-semibold">Something went wrong rendering this page</h2>
            <p className="text-sm text-muted-foreground">{this.state.error.message}</p>
            <button className="text-sm underline" onClick={() => window.location.reload()}>Reload the page</button>
          </div>
        </div>
      )
    }
    return this.props.children
  }
}

export default function App() {
  const [user, setUser] = useState<{ id: string; email: string; displayName: string } | null>(null)
  const [selectedPipeline, setSelectedPipeline] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    auth.me()
      .then(setUser)
      .catch(() => setUser(null))
      .finally(() => setLoading(false))
  }, [])

  const handleLogout = async () => {
    await auth.logout()
    setUser(null)
    setSelectedPipeline(null)
  }

  if (loading) {
    return (
      <div className="min-h-screen bg-background flex flex-col">
        <header className="border-b bg-card px-6 py-3 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <Skeleton className="h-7 w-7 rounded-md" />
            <Skeleton className="h-4 w-20" />
          </div>
          <div className="flex items-center gap-3">
            <Skeleton className="h-7 w-32 rounded-md" />
            <Skeleton className="h-7 w-16 rounded-md" />
          </div>
        </header>
        <main className="flex-1 max-w-5xl w-full mx-auto px-6 py-8 space-y-6">
          <div className="flex items-center justify-between">
            <div className="space-y-2">
              <Skeleton className="h-6 w-36" />
              <Skeleton className="h-3.5 w-64" />
            </div>
            <div className="flex gap-2">
              <Skeleton className="h-8 w-20 rounded-md" />
              <Skeleton className="h-8 w-24 rounded-md" />
            </div>
          </div>
          <div className="space-y-3">
            <Skeleton className="h-16 w-full rounded-lg" />
            <Skeleton className="h-16 w-full rounded-lg" />
            <Skeleton className="h-16 w-full rounded-lg" />
          </div>
        </main>
      </div>
    )
  }

  if (!user) return <LoginPage onLogin={() => auth.me().then(setUser)} />

  if (selectedPipeline) {
    return (
      <ErrorBoundary key={selectedPipeline}>
        <PipelineEditorPage pipelineId={selectedPipeline} onBack={() => setSelectedPipeline(null)} onLogout={handleLogout} />
      </ErrorBoundary>
    )
  }

  return (
    <ErrorBoundary>
      <DashboardPage user={user} onSelectPipeline={setSelectedPipeline} onLogout={handleLogout} />
    </ErrorBoundary>
  )
}
