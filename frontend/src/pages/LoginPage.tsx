import { useState } from "react"
import { auth } from "@/api/client"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from "@/components/ui/card"
import { Workflow, ArrowRight, Loader2 } from "lucide-react"
import { GoogleOAuthProvider, useGoogleLogin } from "@react-oauth/google"

interface LoginPageProps {
  onLogin: () => void
}

function GoogleIcon(props: React.SVGProps<SVGSVGElement>) {
  return (
    <svg viewBox="0 0 24 24" width="16" height="16" {...props}>
      <path
        fill="#4285F4"
        d="M23.745 12.27c0-.7-.06-1.4-.19-2.07H12v4.51h6.6c-.29 1.52-1.14 2.8-2.4 3.65v3.03h3.88c2.27-2.09 3.66-5.17 3.66-9.12z"
      />
      <path
        fill="#34A853"
        d="M12 24c3.24 0 5.95-1.08 7.93-2.91l-3.88-3.03c-1.08.72-2.45 1.16-4.05 1.16-3.12 0-5.77-2.1-6.72-4.93H1.25v3.13C3.27 21.44 7.35 24 12 24z"
      />
      <path
        fill="#FBBC05"
        d="M5.28 14.29c-.25-.72-.38-1.49-.38-2.29s.13-1.57.38-2.29V6.58H1.25C.45 8.17 0 9.97 0 12s.45 3.83 1.25 5.42l4.03-3.13z"
      />
      <path
        fill="#EA4335"
        d="M12 4.75c1.77 0 3.35.61 4.6 1.8l3.42-3.42C17.95 1.19 15.24 0 12 0 7.35 0 3.27 2.56 1.25 6.58l4.03 3.13c.95-2.83 3.6-4.96 6.72-4.96z"
      />
    </svg>
  )
}

function ActiveGoogleButton({
  onSuccess,
  onError,
  loading,
}: {
  onSuccess: (data: { accessToken: string }) => void
  onError: (err: string) => void
  loading: boolean
}) {
  const login = useGoogleLogin({
    onSuccess: (tokenResponse) => {
      onSuccess({ accessToken: tokenResponse.access_token })
    },
    onError: () => {
      onError("Google Sign-In was cancelled or failed.")
    },
  })

  return (
    <button
      type="button"
      onClick={() => login()}
      disabled={loading}
      className="group relative w-full h-9 px-3 rounded-md border border-border/80 bg-background/80 hover:bg-accent/60 hover:border-foreground/25 active:scale-[0.985] transition-all duration-150 flex items-center justify-center gap-2.5 text-xs font-medium text-foreground shadow-xs cursor-pointer disabled:opacity-50 disabled:pointer-events-none"
    >
      {loading ? (
        <Loader2 className="h-3.5 w-3.5 animate-spin text-muted-foreground" />
      ) : (
        <GoogleIcon className="h-4 w-4 shrink-0 transition-transform duration-200 group-hover:scale-110" />
      )}
      <span className="tracking-tight">
        {loading ? "Authenticating with Google..." : "Continue with Google"}
      </span>
      <span className="absolute right-2.5 hidden sm:inline-flex items-center text-[10px] font-mono text-muted-foreground/60 border border-border/60 rounded px-1.5 py-0.5 group-hover:border-foreground/20 group-hover:text-muted-foreground transition-colors">
        SSO
      </span>
    </button>
  )
}

function InactiveGoogleButton({ onClick }: { onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="group relative w-full h-9 px-3 rounded-md border border-border/80 bg-background/80 hover:bg-accent/60 hover:border-foreground/25 active:scale-[0.985] transition-all duration-150 flex items-center justify-center gap-2.5 text-xs font-medium text-foreground shadow-xs cursor-pointer"
    >
      <GoogleIcon className="h-4 w-4 shrink-0 transition-transform duration-200 group-hover:scale-110" />
      <span className="tracking-tight">Continue with Google</span>
      <span className="absolute right-2.5 hidden sm:inline-flex items-center text-[10px] font-mono text-muted-foreground/60 border border-border/60 rounded px-1.5 py-0.5 group-hover:border-foreground/20 group-hover:text-muted-foreground transition-colors">
        SSO
      </span>
    </button>
  )
}

export function LoginPage({ onLogin }: LoginPageProps) {
  const [isRegister, setIsRegister] = useState(false)
  const [email, setEmail] = useState("")
  const [password, setPassword] = useState("")
  const [displayName, setDisplayName] = useState("")
  const [error, setError] = useState("")
  const [loading, setLoading] = useState(false)

  const googleClientId = import.meta.env.VITE_GOOGLE_CLIENT_ID as string | undefined

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

  const handleGoogleSuccess = async (data: { idToken?: string; accessToken?: string }) => {
    setError("")
    setLoading(true)
    try {
      await auth.googleLogin(data)
      onLogin()
    } catch (err) {
      setError(err instanceof Error ? err.message : "Google authentication failed")
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="min-h-screen flex items-center justify-center p-4 bg-background bg-dot-grid relative overflow-hidden">
      {/* Subtle ambient glow behind card */}
      <div className="absolute w-96 h-96 rounded-full bg-primary/10 blur-3xl pointer-events-none" />

      <Card className="w-full max-w-[390px] border-border/80 bg-card/90 backdrop-blur-md shadow-2xl relative z-10 animate-dialog-pop">
        <CardHeader className="space-y-2 pb-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2.5">
              <div className="h-8 w-8 rounded-md bg-primary text-primary-foreground flex items-center justify-center shadow-xs">
                <Workflow className="h-4 w-4" />
              </div>
              <div>
                <CardTitle className="text-base font-semibold tracking-tight text-foreground">Reflow</CardTitle>
                <p className="text-[11px] text-muted-foreground font-mono">Real-time workflow engine</p>
              </div>
            </div>
            <span className="text-[10px] font-mono tracking-wider text-muted-foreground/80 uppercase px-2 py-0.5 rounded-full border border-border/80 bg-muted/40">
              Workspace
            </span>
          </div>
          <CardDescription className="text-xs pt-1">
            {isRegister
              ? "Create an account to build and run automated pipelines."
              : "Sign in to access your data pipelines and compute workflows."}
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {/* Databricks / Shadcn Themed Google SSO Button */}
          <div className="w-full">
            {googleClientId ? (
              <GoogleOAuthProvider clientId={googleClientId}>
                <ActiveGoogleButton
                  onSuccess={handleGoogleSuccess}
                  onError={(err) => setError(err)}
                  loading={loading}
                />
              </GoogleOAuthProvider>
            ) : (
              <InactiveGoogleButton
                onClick={() => {
                  setError("Google OAuth is not configured. Set VITE_GOOGLE_CLIENT_ID in your frontend .env file.")
                }}
              />
            )}
          </div>

          <div className="relative flex items-center justify-center py-0.5">
            <div className="absolute inset-0 flex items-center">
              <span className="w-full border-t border-border/80" />
            </div>
            <span className="relative bg-card px-2.5 text-[10px] uppercase tracking-widest text-muted-foreground font-mono">
              Or continue with email
            </span>
          </div>

          <form onSubmit={handleSubmit} className="space-y-3.5">
            {isRegister && (
              <div className="space-y-1">
                <label className="text-[11px] font-medium text-muted-foreground tracking-tight">Display Name</label>
                <Input
                  className="h-8.5 text-xs font-mono placeholder:font-sans"
                  placeholder="e.g. Alex"
                  value={displayName}
                  onChange={e => setDisplayName(e.target.value)}
                  required
                />
              </div>
            )}
            <div className="space-y-1">
              <label className="text-[11px] font-medium text-muted-foreground tracking-tight">Email</label>
              <Input
                className="h-8.5 text-xs font-mono"
                placeholder="name@example.com"
                type="email"
                value={email}
                onChange={e => setEmail(e.target.value)}
                required
              />
            </div>
            <div className="space-y-1">
              <label className="text-[11px] font-medium text-muted-foreground tracking-tight">Password</label>
              <Input
                className="h-8.5 text-xs font-mono"
                placeholder="••••••••"
                type="password"
                value={password}
                onChange={e => setPassword(e.target.value)}
                required
              />
            </div>

            {error && (
              <p className="text-xs text-destructive bg-destructive/10 border border-destructive/20 p-2.5 rounded-md leading-relaxed">
                {error}
              </p>
            )}

            <Button
              type="submit"
              size="sm"
              className="group relative w-full h-9 text-xs font-medium tracking-tight gap-2 shadow-xs active:scale-[0.985] transition-all cursor-pointer"
              disabled={loading}
            >
              {loading ? (
                <>
                  <Loader2 className="h-3.5 w-3.5 animate-spin" />
                  <span>Processing...</span>
                </>
              ) : (
                <>
                  <span>{isRegister ? "Create Account" : "Sign In to Workspace"}</span>
                  <ArrowRight className="h-3.5 w-3.5 transition-transform duration-150 group-hover:translate-x-0.5" />
                </>
              )}
            </Button>

            <button
              type="button"
              className="w-full text-center text-xs text-muted-foreground hover:text-foreground transition-colors pt-1 cursor-pointer"
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
