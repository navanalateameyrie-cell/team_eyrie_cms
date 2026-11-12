import { useMemo, useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { apiUrl } from "../api/client";
import { Button } from "../components/ui";

export default function ResetPasswordPage() {
  const nav = useNavigate();
  const location = useLocation();
  const token = useMemo(() => new URLSearchParams(location.search).get("token") ?? "", [location.search]);
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [status, setStatus] = useState<"idle" | "saving" | "done" | "error">("idle");
  const [error, setError] = useState<string | null>(null);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    if (password.length < 8) {
      setError("Password must be at least 8 characters in length.");
      return;
    }
    if (password !== confirm) {
      setError("Passwords do not match. Please re-enter.");
      return;
    }
    setStatus("saving");
    try {
      const r = await fetch(apiUrl("/api/v1/auth/reset-password"), {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ token, newPassword: password }),
      });
      if (!r.ok) {
        const body = (await r.json().catch(() => ({}))) as { error?: string; detail?: string };
        setError(body.detail ?? body.error ?? `Password reset failed (${r.status})`);
        setStatus("error");
        return;
      }
      setStatus("done");
      setTimeout(() => nav("/login"), 1500);
    } catch {
      setError("Could not reach the server. Please verify your internet connection.");
      setStatus("error");
    }
  }

  return (
    <div className="min-h-screen flex items-center justify-center p-6 sm:p-10 bg-slate-50 font-sans selection:bg-brand-500 selection:text-white">
      <form
        onSubmit={submit}
        className="w-full max-w-md space-y-5 rounded-2xl border border-slate-200/80 bg-white p-8 sm:p-10 shadow-lg"
      >
        <div>
          <span className="text-[11px] font-bold uppercase tracking-wider text-brand-600 bg-brand-50 px-2.5 py-0.5 rounded-full border border-brand-200">
            Account Security
          </span>
          <h2 className="mt-3 text-2xl font-bold tracking-tight text-slate-900">Set a new password</h2>
          <p className="mt-1 text-sm text-slate-500">
            Enter your new secure password below to regain account access.
          </p>
          {!token && (
            <p className="mt-2 text-xs font-semibold text-red-600 bg-red-50 p-2 rounded border border-red-200">
              Warning: Reset token is missing or invalid in the URL.
            </p>
          )}
        </div>

        <div className="space-y-4 pt-1">
          <div>
            <label htmlFor="newPassword" className="block text-xs font-semibold text-slate-700 uppercase tracking-wider mb-1.5">
              New Password (min. 8 characters)
            </label>
            <input
              id="newPassword"
              type="password"
              required
              minLength={8}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="w-full rounded-lg border border-slate-300 px-3.5 py-2.5 text-sm text-slate-900 placeholder-slate-400 focus:border-brand-500 focus:ring-2 focus:ring-brand-500/20 transition outline-none"
              placeholder="••••••••"
            />
          </div>

          <div>
            <label htmlFor="confirmPassword" className="block text-xs font-semibold text-slate-700 uppercase tracking-wider mb-1.5">
              Confirm New Password
            </label>
            <input
              id="confirmPassword"
              type="password"
              required
              minLength={8}
              value={confirm}
              onChange={(e) => setConfirm(e.target.value)}
              className="w-full rounded-lg border border-slate-300 px-3.5 py-2.5 text-sm text-slate-900 placeholder-slate-400 focus:border-brand-500 focus:ring-2 focus:ring-brand-500/20 transition outline-none"
              placeholder="••••••••"
            />
          </div>
        </div>

        {error && (
          <div className="rounded-lg border border-red-200 bg-red-50 p-3 text-xs text-red-700 flex items-start gap-2">
            <svg className="w-4 h-4 text-red-500 shrink-0 mt-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            <span>{error}</span>
          </div>
        )}

        {status === "done" && (
          <div className="rounded-lg border border-emerald-200 bg-emerald-50 p-3 text-xs text-emerald-800 flex items-center gap-2">
            <svg className="w-4 h-4 text-emerald-600 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
            </svg>
            <span>Password updated successfully. Redirecting you to sign-in…</span>
          </div>
        )}

        <Button
          type="submit"
          variant="primary"
          size="lg"
          busy={status === "saving"}
          disabled={!token || status === "done"}
          className="w-full shadow-md"
        >
          {status === "saving" ? "Saving new password…" : "Save New Password →"}
        </Button>

        <div className="text-center pt-2">
          <Link to="/login" className="text-xs text-slate-500 hover:text-slate-800 transition">
            ← Back to sign in
          </Link>
        </div>
      </form>
    </div>
  );
}
