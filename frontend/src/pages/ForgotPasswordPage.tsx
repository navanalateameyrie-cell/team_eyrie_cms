import { useState } from "react";
import { Link } from "react-router-dom";
import { Button } from "../components/ui";

export default function ForgotPasswordPage() {
  const [email, setEmail] = useState("");
  const [status, setStatus] = useState<"idle" | "sending" | "done" | "error">("idle");

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setStatus("sending");
    try {
      const r = await fetch("/api/v1/auth/forgot-password", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email }),
      });
      // Always confirm success regardless of status to avoid user enumeration — the backend
      // follows the same convention.
      setStatus(r.ok ? "done" : "error");
    } catch {
      setStatus("error");
    }
  }

  return (
    <div className="min-h-screen grid md:grid-cols-2 bg-navy-950 font-sans selection:bg-brand-500 selection:text-white">
      {/* Brand panel */}
      <aside className="hidden md:flex relative overflow-hidden flex-col justify-between p-12 text-white border-r border-navy-800">
        <div className="absolute inset-0 bg-gradient-to-br from-navy-950 via-navy-900 to-navy-950" />
        <div className="absolute -right-24 -bottom-24 h-96 w-96 rounded-full bg-brand-500/15 blur-3xl pointer-events-none" />

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

          <div className="mt-20">
            <div className="inline-flex items-center gap-2 rounded-full bg-brand-500/20 border border-brand-500/30 px-3 py-1 text-xs font-semibold text-brand-300 uppercase tracking-widest mb-4">
              Access Recovery
            </div>
            <h1 className="text-3xl lg:text-4xl font-bold leading-tight tracking-tight text-white">
              Can't sign in to your practice account?
            </h1>
            <p className="mt-4 text-sm text-slate-300 leading-relaxed max-w-md">
              Enter the email address registered with your CMS firm account. If it exists in our system, you will receive a secure one-time password reset link expiring in 60 minutes.
            </p>
          </div>
        </div>

        <div className="relative z-10 pt-8 border-t border-navy-800/80 flex items-center justify-between text-xs text-slate-400">
          <span>© CMS · Chartered Practice Management</span>
          <span>Secured Identity Flow</span>
        </div>
      </aside>

      {/* Form panel */}
      <main className="flex items-center justify-center p-6 sm:p-10 bg-slate-50">
        <form
          onSubmit={submit}
          className="w-full max-w-md space-y-5 rounded-2xl border border-slate-200/80 bg-white p-8 sm:p-10 shadow-lg"
        >
          <div>
            <span className="text-[11px] font-bold uppercase tracking-wider text-brand-600 bg-brand-50 px-2.5 py-0.5 rounded-full border border-brand-200">
              Password Assistance
            </span>
            <h2 className="mt-3 text-2xl font-bold tracking-tight text-slate-900">Reset your password</h2>
            <p className="mt-1 text-sm text-slate-500">
              We will send you a one-time link to create a new password.
            </p>
          </div>

          <div className="pt-1">
            <label htmlFor="resetEmail" className="block text-xs font-semibold text-slate-700 uppercase tracking-wider mb-1.5">
              Email Address
            </label>
            <input
              id="resetEmail"
              type="email"
              autoComplete="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className="w-full rounded-lg border border-slate-300 px-3.5 py-2.5 text-sm text-slate-900 placeholder-slate-400 focus:border-brand-500 focus:ring-2 focus:ring-brand-500/20 transition outline-none"
              placeholder="name@firm.com"
            />
          </div>

          <Button
            type="submit"
            variant="primary"
            size="lg"
            busy={status === "sending"}
            className="w-full shadow-md"
          >
            {status === "sending" ? "Sending reset link…" : "Send Reset Link →"}
          </Button>

          {status === "done" && (
            <div className="rounded-lg border border-emerald-200 bg-emerald-50 p-3 text-xs text-emerald-800 flex items-start gap-2">
              <svg className="w-4 h-4 text-emerald-600 shrink-0 mt-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
              </svg>
              <span>If an account exists for that email, a password reset link has been dispatched. Please check your inbox and spam folder.</span>
            </div>
          )}

          {status === "error" && (
            <div className="rounded-lg border border-red-200 bg-red-50 p-3 text-xs text-red-700 flex items-start gap-2">
              <svg className="w-4 h-4 text-red-500 shrink-0 mt-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
              <span>Unable to communicate with the identity service. Please verify your connection and try again.</span>
            </div>
          )}

          <div className="text-center pt-2">
            <Link to="/login" className="text-xs text-slate-500 hover:text-slate-800 transition">
              ← Return to login
            </Link>
          </div>
        </form>
      </main>
    </div>
  );
}
