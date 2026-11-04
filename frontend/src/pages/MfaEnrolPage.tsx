import { useEffect, useState } from "react";
import { apiFetch } from "../api/client";
import { PageHeader, Card, Button, Badge, LoadingSpinner } from "../components/ui";

interface Status {
  isEnrolled: boolean;
  enrolledAtUtc: string | null;
}

interface EnrolStart {
  base32Secret: string;
  otpAuthUri: string;
}

export default function MfaEnrolPage() {
  const [status, setStatus] = useState<Status | null>(null);
  const [enrol, setEnrol] = useState<EnrolStart | null>(null);
  const [code, setCode] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [copied, setCopied] = useState(false);

  async function refresh() {
    try {
      setStatus(await apiFetch<Status>("/api/v1/auth/mfa/status"));
    } catch (e) {
      setError(String(e));
    }
  }

  useEffect(() => {
    void refresh();
  }, []);

  async function start() {
    setBusy(true);
    setError(null);
    try {
      const r = await apiFetch<EnrolStart>("/api/v1/auth/mfa/enrol-start", { method: "POST" });
      setEnrol(r);
      setCode("");
    } catch (e) {
      setError(String(e));
    } finally {
      setBusy(false);
    }
  }

  async function verify(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await apiFetch("/api/v1/auth/mfa/enrol-verify", {
        method: "POST",
        body: JSON.stringify({ code }),
      });
      setEnrol(null);
      setCode("");
      await refresh();
    } catch (e) {
      setError(String(e));
    } finally {
      setBusy(false);
    }
  }

  async function disable(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await apiFetch("/api/v1/auth/mfa/disable", {
        method: "POST",
        body: JSON.stringify({ code }),
      });
      setCode("");
      await refresh();
    } catch (e) {
      setError(String(e));
    } finally {
      setBusy(false);
    }
  }

  function copySecret(secret: string) {
    navigator.clipboard.writeText(secret);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }

  return (
    <div className="space-y-6 max-w-2xl">
      <PageHeader
        title="Two-Factor Authentication (2FA)"
        subtitle="Protect your practice account with a time-based one-time password (TOTP) from Google Authenticator, 1Password, Authy, or Microsoft Authenticator."
      />

      {error && (
        <div className="rounded-lg border border-red-200 bg-red-50 text-sm text-red-700 p-4">
          <p className="font-semibold">Operation Error</p>
          <p className="text-xs mt-0.5">{error}</p>
        </div>
      )}

      {status === null ? (
        <Card className="p-8">
          <LoadingSpinner size="md" label="Checking MFA status…" />
        </Card>
      ) : status.isEnrolled ? (
        <Card className="p-6 border-emerald-200 bg-emerald-50/50 space-y-4">
          <div className="flex items-start justify-between">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-full bg-emerald-100 text-emerald-700 flex items-center justify-center font-bold text-lg">
                ✓
              </div>
              <div>
                <div className="flex items-center gap-2">
                  <h3 className="text-base font-bold text-emerald-950">MFA is Active</h3>
                  <Badge tone="success" size="sm">Enabled</Badge>
                </div>
                <p className="text-xs text-emerald-800 mt-0.5">
                  Enrolled on {status.enrolledAtUtc ? new Date(status.enrolledAtUtc).toLocaleString() : "Unknown"}
                </p>
              </div>
            </div>
          </div>

          <p className="text-xs text-slate-600 leading-relaxed">
            Your login is protected with two-factor authentication. Every time you sign in to the practice console, you will be prompted for a 6-digit TOTP code.
          </p>

          <form onSubmit={disable} className="pt-2 border-t border-emerald-200 flex flex-col sm:flex-row items-end gap-3">
            <div className="flex-1 w-full">
              <label htmlFor="disableMfaCode" className="block text-xs font-semibold text-slate-700 mb-1">
                Enter current 6-digit code to disable
              </label>
              <input
                id="disableMfaCode"
                value={code}
                onChange={(e) => setCode(e.target.value)}
                inputMode="numeric"
                pattern="[0-9]{6}"
                maxLength={6}
                required
                placeholder="123456"
                className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm font-mono tracking-widest outline-none focus:border-red-500 focus:ring-1 focus:ring-red-500/20"
              />
            </div>
            <Button
              type="submit"
              variant="danger"
              size="md"
              busy={busy}
              className="w-full sm:w-auto shrink-0"
            >
              Disable 2FA
            </Button>
          </form>
        </Card>
      ) : enrol ? (
        <Card className="p-6 space-y-5">
          <div>
            <div className="flex items-center justify-between">
              <Badge tone="brand" size="sm">Step 1 of 2</Badge>
              <button
                type="button"
                onClick={() => setEnrol(null)}
                className="text-xs text-slate-500 hover:text-slate-800"
              >
                Cancel
              </button>
            </div>
            <h3 className="mt-2 text-lg font-bold text-slate-900">Add account to your Authenticator</h3>
            <p className="text-xs text-slate-600 mt-1 leading-relaxed">
              Open your authenticator app (e.g. Google Authenticator, 1Password, Authy), choose "Add Account" and enter the secret key manually or scan the URI.
            </p>
          </div>

          <div className="space-y-3">
            <div>
              <div className="flex items-center justify-between">
                <span className="text-xs font-semibold uppercase tracking-wider text-slate-500">
                  Secret Key (Base32)
                </span>
                <button
                  type="button"
                  onClick={() => copySecret(enrol.base32Secret)}
                  className="text-xs text-brand-600 hover:text-brand-700 font-medium inline-flex items-center gap-1"
                >
                  {copied ? "Copied!" : "Copy Key"}
                </button>
              </div>
              <code className="block mt-1 font-mono text-sm bg-slate-50 border border-slate-200 rounded-lg p-3 text-slate-900 break-all select-all font-semibold">
                {enrol.base32Secret}
              </code>
            </div>

            <div>
              <span className="text-xs font-semibold uppercase tracking-wider text-slate-500">
                Provisioning URI
              </span>
              <code className="block mt-1 font-mono text-xs bg-slate-50 border border-slate-200 rounded-lg p-2.5 text-slate-600 break-all select-all">
                {enrol.otpAuthUri}
              </code>
            </div>
          </div>

          <div className="pt-4 border-t border-slate-100">
            <h4 className="text-xs font-semibold uppercase tracking-wider text-slate-700 mb-2">
              Step 2: Verify Enrollment
            </h4>
            <form onSubmit={verify} className="flex flex-col sm:flex-row items-end gap-3">
              <div className="flex-1 w-full">
                <label htmlFor="verifyMfaCode" className="block text-xs text-slate-600 mb-1">
                  Enter 6-digit verification code from app
                </label>
                <input
                  id="verifyMfaCode"
                  value={code}
                  onChange={(e) => setCode(e.target.value)}
                  inputMode="numeric"
                  pattern="[0-9]{6}"
                  maxLength={6}
                  required
                  autoFocus
                  placeholder="000 000"
                  className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm font-mono tracking-widest text-center outline-none focus:border-brand-500 focus:ring-1 focus:ring-brand-500/20"
                />
              </div>
              <Button
                type="submit"
                variant="primary"
                size="md"
                busy={busy}
                className="w-full sm:w-auto shrink-0"
              >
                Verify & Activate
              </Button>
            </form>
          </div>
        </Card>
      ) : (
        <Card className="p-6 space-y-4">
          <div className="flex items-start gap-4">
            <div className="w-12 h-12 rounded-xl bg-brand-50 text-brand-600 flex items-center justify-center shrink-0 border border-brand-200">
              <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
              </svg>
            </div>
            <div>
              <h3 className="text-base font-bold text-slate-900">Two-factor authentication is not active</h3>
              <p className="text-xs text-slate-600 mt-1 leading-relaxed">
                Add an extra layer of protection to prevent unauthorized access to your practice files, client PAN details, and confidential tax documents.
              </p>
            </div>
          </div>

          <div className="pt-2">
            <Button
              type="button"
              variant="primary"
              size="md"
              busy={busy}
              onClick={start}
            >
              Start 2FA Enrolment →
            </Button>
          </div>
        </Card>
      )}
    </div>
  );
}
