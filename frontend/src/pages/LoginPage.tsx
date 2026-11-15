import { useState } from "react"
import { auth } from "@/api/client"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from "@/components/ui/card"
import { Workflow, ArrowRight } from "lucide-react"

interface LoginPageProps {
  onLogin: () => void
}

export function LoginPage({ onLogin }: LoginPageProps) {
  const [isRegister, setIsRegister] = useState(false)
  const [email, setEmail] = useState("")
  const [password, setPassword] = useState("")
  const [displayName, setDisplayName] = useState("")
  const [error, setError] = useState("")
  const [loading, setLoading] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError("")
    setLoading(true)

    try {
      if (isRegister) {
        await auth.register({ email, password, displayName })
      }
      await auth.login({ email, password })
      onLogin()
    } catch (err) {
      setError(err instanceof Error ? err.message : "Authentication failed")
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="min-h-screen flex items-center justify-center p-4 bg-background">
      <Card className="w-full max-w-[380px] border-border shadow-xs">
        <CardHeader className="space-y-2 pb-4">
          <div className="flex items-center gap-2.5">
            <div className="h-8 w-8 rounded-md bg-foreground text-background flex items-center justify-center">
              <Workflow className="h-4 w-4" />
            </div>
            <div>
              <CardTitle className="text-base font-semibold tracking-tight">Reflow</CardTitle>
              <p className="text-[11px] text-muted-foreground font-mono">Real-time workflow engine</p>
            </div>
          </div>
          <CardDescription className="text-xs pt-1">
            {isRegister ? "Create an account to build and run pipelines." : "Sign in to your Reflow workspace."}
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="space-y-3.5">
            {isRegister && (
              <div className="space-y-1">
                <label className="text-[11px] font-medium text-muted-foreground">Display Name</label>
                <Input
                  className="h-8 text-xs"
                  placeholder="e.g. Alex"
                  value={displayName}
                  onChange={e => setDisplayName(e.target.value)}
                  required
                />
              </div>
            )}
            <div className="space-y-1">
              <label className="text-[11px] font-medium text-muted-foreground">Email</label>
              <Input
                className="h-8 text-xs font-mono"
                placeholder="name@example.com"
                type="email"
                value={email}
                onChange={e => setEmail(e.target.value)}
                required
              />
            </div>
            <div className="space-y-1">
              <label className="text-[11px] font-medium text-muted-foreground">Password</label>
              <Input
                className="h-8 text-xs font-mono"
                placeholder="••••••••"
                type="password"
                value={password}
                onChange={e => setPassword(e.target.value)}
                required
              />
            </div>

            {error && (
              <p className="text-xs text-destructive bg-destructive/10 border border-destructive/20 p-2 rounded">
                {error}
              </p>
            )}

            <Button type="submit" size="sm" className="w-full h-8 text-xs font-medium gap-1.5" disabled={loading}>
              {loading ? "Processing..." : isRegister ? "Create Account" : "Sign In"}
              <ArrowRight className="h-3 w-3" />
            </Button>

            <button
              type="button"
              className="w-full text-center text-xs text-muted-foreground hover:text-foreground transition-colors pt-1"
              onClick={() => { setIsRegister(!isRegister); setError("") }}
            >
              {isRegister ? "Already have an account? Sign in" : "Don't have an account? Create one"}
            </button>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
