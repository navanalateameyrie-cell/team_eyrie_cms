import { useEffect, useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { apiUrl, readStorage } from "../api/client";
import { Button } from "../components/ui";

export default function LoginPage() {
  const { login, completeMfa, permissions } = useAuth();
  const nav = useNavigate();
  const loc = useLocation() as { state?: { from?: string } };

  const [email, setEmail] = useState("admin@cms.local");
  const [password, setPassword] = useState("Admin!23");
  const [showPassword, setShowPassword] = useState(false);
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [waking, setWaking] = useState(false);
  const [mfaPending, setMfaPending] = useState<string | null>(null);
  const [mfaCode, setMfaCode] = useState("");

  useEffect(() => {
    // Pre-warm the API server so free-tier cold starts begin immediately when opening login.
    fetch(apiUrl("/api/v1/health")).catch(() => {});
  }, []);

  useEffect(() => {
    if (!busy) {
      setWaking(false);
      return;
    }
    const timer = window.setTimeout(() => setWaking(true), 4000);
    return () => window.clearTimeout(timer);
  }, [busy]);

  function navigateAfterLogin() {
    const env = readStorage();
    const perms = env?.permissions ?? permissions;
    if (perms.length > 0 && perms.every((p) => p === "portal.access")) {
      nav("/portal", { replace: true });
    } else {
      const next = new URLSearchParams(window.location.search).get("next");
      nav(loc.state?.from ?? next ?? "/dashboard", { replace: true });
    }
  }

  function parseErrorMessage(ex: unknown): string {
    if (!(ex instanceof Error)) return "Authentication failed. Please check your credentials.";
    if (ex.message === "Failed to fetch" || ex.message === "Load failed") {
      return "Unable to reach the API server. If hosted on a free tier, the backend may still be waking up — please retry in a few seconds.";
    }
    try {
      const data = JSON.parse(ex.message);
      if (data.detail && typeof data.detail === "string") {
        if (data.detail.includes("password authentication failed for user")) {
          return "Database authentication failed: PostgreSQL user 'postgres' password is invalid. Please run 'ALTER USER postgres WITH PASSWORD 'postgres';' or update appsettings.json.";
        }
        return data.title || data.detail.split("\n")[0];
      }
      if (data.title) return data.title;
    } catch {
      // not JSON string
    }
    return ex.message;
  }

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setErr(null);
    setBusy(true);
    try {
      const r = await login(email, password);
      if (r.mfaRequired && r.mfaPendingToken) {
        setMfaPending(r.mfaPendingToken);
      } else {
        navigateAfterLogin();
      }
    } catch (ex: unknown) {
      setErr(parseErrorMessage(ex));
    } finally {
      setBusy(false);
    }
  }

  async function onMfaSubmit(e: React.FormEvent) {
    e.preventDefault();
    setErr(null);
    setBusy(true);
    try {
      await completeMfa(mfaPending!, mfaCode);
      navigateAfterLogin();
    } catch (ex: unknown) {
      setErr(ex instanceof Error ? ex.message : "MFA verification failed. Please try again.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="min-h-screen grid md:grid-cols-2 bg-navy-950 font-sans selection:bg-brand-500 selection:text-white">
      {/* Brand panel */}
      <aside className="hidden md:flex relative overflow-hidden flex-col justify-between p-12 text-white border-r border-navy-800">
        <div className="absolute inset-0 bg-gradient-to-br from-navy-950 via-navy-900 to-navy-950" />
        <div className="absolute -right-24 -bottom-24 h-96 w-96 rounded-full bg-brand-500/15 blur-3xl pointer-events-none" />
        <div className="absolute left-10 top-1/4 h-64 w-64 rounded-full bg-sky-500/10 blur-3xl pointer-events-none" />

        <div className="relative z-10">
          <div className="flex items-center gap-3">
            <div className="h-10 w-10 rounded-xl bg-gradient-to-br from-brand-500 to-brand-600 text-white font-bold flex items-center justify-center shadow-md ring-1 ring-white/10">
              CA
            </div>
            <div>
              <div className="font-bold tracking-tight text-white text-base">CMS</div>
              <div className="text-xs text-slate-400 font-medium">Practice Operations Suite</div>
            </div>
          </div>

          <div className="mt-16">
            <div className="inline-flex items-center gap-2 rounded-full bg-brand-500/20 border border-brand-500/30 px-3 py-1 text-xs font-semibold text-brand-300 uppercase tracking-widest mb-4">
              Enterprise Governance
            </div>
            <h1 className="text-3xl lg:text-4xl font-bold leading-tight tracking-tight text-white">
              Audit-first client management for Chartered Accountants.
            </h1>
            <p className="mt-4 text-sm text-slate-300 leading-relaxed max-w-md">
              Permission-controlled governance across clients, branch networks, service files, statutory engagements, and GST compliance. Every protected write is recorded; every sensitive read is auditable.
            </p>
          </div>

          <div className="mt-10 space-y-3.5 text-sm text-slate-300">
            <div className="flex items-center gap-3">
              <div className="w-5 h-5 rounded-full bg-emerald-500/20 text-emerald-400 flex items-center justify-center text-xs">✓</div>
              <span>Permanent 8-digit client codes with live PAN deduplication</span>
            </div>
            <div className="flex items-center gap-3">
              <div className="w-5 h-5 rounded-full bg-emerald-500/20 text-emerald-400 flex items-center justify-center text-xs">✓</div>
              <span>Multi-state branches with GSTIN & TAN verification</span>
            </div>
            <div className="flex items-center gap-3">
              <div className="w-5 h-5 rounded-full bg-emerald-500/20 text-emerald-400 flex items-center justify-center text-xs">✓</div>
              <span>Timesheet tracking with partner approval workflows</span>
            </div>
            <div className="flex items-center gap-3">
              <div className="w-5 h-5 rounded-full bg-emerald-500/20 text-emerald-400 flex items-center justify-center text-xs">✓</div>
              <span>Automated GST e-invoicing & real-time bank reconciliation</span>
            </div>
          </div>
        </div>

        <div className="relative z-10 pt-8 border-t border-navy-800/80 flex items-center justify-between text-xs text-slate-400">
          <span>© CMS · Chartered Practice Management</span>
          <span>Secured with TLS 1.3</span>
        </div>
      </aside>

      {/* Form panel */}
      <main className="flex items-center justify-center p-6 sm:p-10 bg-slate-50">
        {mfaPending ? (
          <form
            onSubmit={onMfaSubmit}
            className="w-full max-w-md space-y-6 rounded-2xl border border-slate-200/80 bg-white p-8 sm:p-10 shadow-lg"
          >
            <div>
              <span className="text-[11px] font-bold uppercase tracking-wider text-brand-600 bg-brand-50 px-2 py-0.5 rounded border border-brand-200">
                Two-Factor Security
              </span>
              <h2 className="mt-3 text-2xl font-bold tracking-tight text-slate-900">Verify your identity</h2>
              <p className="mt-1 text-sm text-slate-500">
                Enter the 6-digit security code generated by your authenticator app.
              </p>
            </div>

            <div>
              <label htmlFor="mfaCode" className="block text-xs font-semibold uppercase tracking-wider text-slate-700 mb-1.5">
                Authentication Code
              </label>
              <input
                id="mfaCode"
                autoFocus
                inputMode="numeric"
                pattern="[0-9]{6}"
                maxLength={6}
                required
                value={mfaCode}
                onChange={(e) => setMfaCode(e.target.value)}
                placeholder="000 000"
                className="w-full rounded-lg border border-slate-300 px-4 py-3 text-center tracking-[0.4em] text-2xl font-mono font-bold text-slate-900 focus:border-brand-500 focus:ring-2 focus:ring-brand-500/20 transition outline-none"
              />
            </div>

            {err && (
              <div className="rounded-lg border border-red-200 bg-red-50 p-3 text-xs text-red-700 flex items-center gap-2">
                <svg className="w-4 h-4 text-red-500 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
                <span>{err}</span>
              </div>
            )}

            <Button
              type="submit"
              variant="primary"
              size="lg"
              busy={busy}
              className="w-full shadow-md"
            >
              Verify & Sign In →
            </Button>

            <button
              type="button"
              onClick={() => {
                setMfaPending(null);
                setMfaCode("");
              }}
              className="w-full text-xs text-slate-500 hover:text-slate-800 text-center py-1 transition"
            >
              ← Back to standard login
            </button>
          </form>
        ) : (
          <form
            onSubmit={onSubmit}
            className="w-full max-w-md space-y-5 rounded-2xl border border-slate-200/80 bg-white p-8 sm:p-10 shadow-lg"
          >
            <div>
              <div className="flex items-center justify-between">
                <span className="text-[11px] font-bold uppercase tracking-wider text-brand-600 bg-brand-50 px-2.5 py-0.5 rounded-full border border-brand-200">
                  Practice Portal
                </span>
                <span className="text-xs text-slate-400 font-mono">v1.0-RC</span>
              </div>
              <h2 className="mt-3 text-2xl font-bold tracking-tight text-slate-900">Sign in to your practice</h2>
              <p className="mt-1 text-sm text-slate-500">
                Enter your authorized credentials to access practice data.
              </p>
            </div>

            <div className="space-y-4 pt-1">
              <div>
                <label htmlFor="loginEmail" className="block text-xs font-semibold text-slate-700 uppercase tracking-wider mb-1.5">
                  Email Address
                </label>
                <div className="relative">
                  <input
                    id="loginEmail"
                    type="email"
                    autoComplete="email"
                    required
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    className="w-full rounded-lg border border-slate-300 px-3.5 py-2.5 text-sm text-slate-900 placeholder-slate-400 focus:border-brand-500 focus:ring-2 focus:ring-brand-500/20 transition outline-none"
                    placeholder="name@firm.com"
                  />
                  <div className="absolute inset-y-0 right-0 flex items-center pr-3 pointer-events-none text-slate-400">
                    <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M16 12a4 4 0 10-8 0 4 4 0 008 0zm0 0v1.5a2.5 2.5 0 005 0V12a9 9 0 10-9 9m4.5-1.206a8.959 8.959 0 01-4.5 1.207" />
                    </svg>
                  </div>
                </div>
              </div>

              <div>
                <div className="flex items-center justify-between mb-1.5">
                  <label htmlFor="loginPassword" className="text-xs font-semibold text-slate-700 uppercase tracking-wider">
                    Password
                  </label>
                  <Link to="/forgot-password" className="text-xs font-medium text-brand-600 hover:text-brand-700 transition">
                    Forgot password?
                  </Link>
                </div>
                <div className="relative">
                  <input
                    id="loginPassword"
                    type={showPassword ? "text" : "password"}
                    autoComplete="current-password"
                    required
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    className="w-full rounded-lg border border-slate-300 px-3.5 py-2.5 text-sm text-slate-900 placeholder-slate-400 focus:border-brand-500 focus:ring-2 focus:ring-brand-500/20 transition outline-none pr-10"
                    placeholder="••••••••"
                  />
                  <button
                    type="button"
                    onClick={() => setShowPassword((p) => !p)}
                    className="absolute inset-y-0 right-0 flex items-center pr-3 text-slate-400 hover:text-slate-600 focus:outline-none"
                    title={showPassword ? "Hide password" : "Show password"}
                  >
                    {showPassword ? (
                      <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13.875 18.825A10.05 10.05 0 0112 19c-4.478 0-8.268-2.943-9.543-7a9.97 9.97 0 011.563-3.029m5.858.908a3 3 0 114.243 4.243M9.878 9.878l4.242 4.242M9.88 9.88l-3.29-3.29m7.532 7.532l3.29 3.29M3 3l18 18" />
                      </svg>
                    ) : (
                      <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                      </svg>
                    )}
                  </button>
                </div>
              </div>
            </div>

            {err && (
              <div className="rounded-lg border border-red-200 bg-red-50 p-3 text-xs text-red-700 flex items-center gap-2">
                <svg className="w-4 h-4 text-red-500 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
                <span>{err}</span>
              </div>
            )}

            {waking && (
              <div className="rounded-lg border border-amber-200 bg-amber-50 p-3 text-xs text-amber-800">
                Server is waking up from sleep mode (first request on free hosting can take 20–50 seconds). Please hold on…
              </div>
            )}

            <Button
              type="submit"
              variant="primary"
              size="lg"
              busy={busy}
              className="w-full shadow-md"
            >
              Sign In to Console →
            </Button>

            <div className="text-center pt-2">
              <span className="text-xs text-slate-500">
                Client representative?{" "}
                <Link to="/portal" className="text-brand-600 font-medium hover:underline">
                  Go to Client Portal
                </Link>
              </span>
            </div>
          </form>
        )}
      </main>
    </div>
  );
}
