import { useState } from "react"
import { auth } from "@/api/client"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from "@/components/ui/card"
import { Workflow, ArrowRight } from "lucide-react"
import { GoogleOAuthProvider, GoogleLogin } from "@react-oauth/google"

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

  const handleGoogleSuccess = async (credential: string) => {
    setError("")
    setLoading(true)
    try {
      await auth.googleLogin(credential)
      onLogin()
    } catch (err) {
      setError(err instanceof Error ? err.message : "Google authentication failed")
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
        <CardContent className="space-y-4">
          {/* Google Sign In */}
          <div className="flex flex-col items-center justify-center">
            {googleClientId ? (
              <GoogleOAuthProvider clientId={googleClientId}>
                <div className="w-full flex justify-center">
                  <GoogleLogin
                    onSuccess={(credentialResponse) => {
                      if (credentialResponse.credential) {
                        handleGoogleSuccess(credentialResponse.credential)
                      }
                    }}
                    onError={() => {
                      setError("Google Sign-In failed or was cancelled.")
                    }}
                    theme="outline"
                    size="medium"
                    width="330"
                    text="continue_with"
                    shape="rectangular"
                  />
                </div>
              </GoogleOAuthProvider>
            ) : (
              <Button
                type="button"
                variant="outline"
                size="sm"
                className="w-full h-9 text-xs font-medium gap-2 justify-center border-border"
                onClick={() => {
                  setError("Google OAuth is not configured yet. Set VITE_GOOGLE_CLIENT_ID in your .env file.")
                }}
              >
                <GoogleIcon />
                Continue with Google
              </Button>
            )}
          </div>

          <div className="relative flex items-center justify-center">
            <div className="absolute inset-0 flex items-center">
              <span className="w-full border-t border-border" />
            </div>
            <span className="relative bg-card px-2 text-[10px] uppercase tracking-wider text-muted-foreground font-mono">
              Or continue with email
            </span>
          </div>

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
