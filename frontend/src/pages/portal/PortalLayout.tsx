import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { useAuth } from "../../auth/AuthContext";
import { useEffect, useState } from "react";
import { apiFetch } from "../../api/client";
import { getDeferredInstallPrompt, triggerInstallPrompt } from "../../pwa";

interface Me {
  clientId: string;
  code: string;
  legalName: string;
  tradeName: string | null;
}

export default function PortalLayout() {
  const { userName, logout, token, permissions } = useAuth();
  const nav = useNavigate();
  const [me, setMe] = useState<Me | null>(null);
  const [canInstall, setCanInstall] = useState<boolean>(() => getDeferredInstallPrompt() !== null);

  useEffect(() => {
    if (!token) {
      nav("/portal/login");
      return;
    }
    // Non-portal accounts shouldn't land here — bounce them to the admin app.
    if (!permissions.includes("portal.access")) {
      nav("/");
      return;
    }
    apiFetch<Me>("/api/v1/portal/me")
      .then(setMe)
      .catch(() => nav("/portal/login"));
  }, [token, permissions, nav]);

  useEffect(() => {
    const available = () => setCanInstall(true);
    const consumed = () => setCanInstall(false);
    window.addEventListener("cms:installprompt-available", available);
    window.addEventListener("cms:installprompt-consumed", consumed);
    return () => {
      window.removeEventListener("cms:installprompt-available", available);
      window.removeEventListener("cms:installprompt-consumed", consumed);
    };
  }, []);

  async function onInstall() {
    const outcome = await triggerInstallPrompt();
    if (outcome === "unavailable") setCanInstall(false);
  }

  return (
    <div
      className="min-h-screen flex flex-col bg-slate-50 text-slate-800"
      style={{ paddingTop: "env(safe-area-inset-top)", paddingBottom: "env(safe-area-inset-bottom)" }}
    >
      {/* Top Application Chrome */}
      <header className="bg-navy-950 text-white shadow-md border-b border-navy-800/80 sticky top-0 z-30">
        <div className="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8 py-3.5 flex items-center justify-between">
          <div className="flex items-center gap-3.5">
            <div className="h-10 w-10 rounded-lg bg-gradient-to-br from-brand-500 to-brand-600 text-white font-bold flex items-center justify-center shadow-sm ring-1 ring-white/10">
              <span className="text-base tracking-wider">CP</span>
            </div>
            <div>
              <div className="flex items-center gap-2">
                <span className="font-semibold text-sm sm:text-base tracking-tight text-white">Client Portal</span>
                <span className="text-[10px] font-medium bg-brand-500/20 text-brand-300 border border-brand-400/30 px-1.5 py-0.5 rounded">
                  Verified Client
                </span>
              </div>
              <div className="text-xs text-slate-400 font-normal truncate max-w-[200px] sm:max-w-xs md:max-w-md">
                {me ? (
                  <>
                    <span className="text-slate-200 font-medium">{me.legalName}</span>
                    <span className="font-mono text-slate-400 ml-1.5">({me.code})</span>
                    {me.tradeName && <span className="text-slate-400 ml-1.5">· {me.tradeName}</span>}
                  </>
                ) : (
                  "Loading account details…"
                )}
              </div>
            </div>
          </div>

          <div className="flex items-center gap-2 sm:gap-3 text-sm">
            <div className="hidden md:flex flex-col text-right pr-2 border-r border-navy-800">
              <span className="text-xs font-medium text-slate-200">{userName}</span>
              <span className="text-[11px] text-slate-400">Client Signatory</span>
            </div>

            {canInstall && (
              <button
                type="button"
                onClick={onInstall}
                className="inline-flex items-center gap-1.5 rounded-md border border-brand-500/40 bg-brand-500/10 px-3 py-1.5 text-xs font-medium text-brand-300 hover:bg-brand-500/20 hover:text-white transition"
              >
                <svg className="w-3.5 h-3.5 text-brand-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
                </svg>
                Install App
              </button>
            )}

            <button
              type="button"
              onClick={async () => {
                await logout();
                nav("/portal/login");
              }}
              className="inline-flex items-center gap-1.5 rounded-md border border-navy-700 bg-navy-900/60 px-3 py-1.5 text-xs font-medium text-slate-300 hover:bg-navy-800 hover:text-white transition"
            >
              <svg className="w-3.5 h-3.5 text-slate-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
              </svg>
              Sign out
            </button>
          </div>
        </div>

        {/* Tab Navigation */}
        <nav className="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8 flex gap-2 sm:gap-6 text-sm border-t border-navy-900 overflow-x-auto">
          {[
            { to: "/portal", label: "Invoices & Billing", end: true },
            { to: "/portal/obligations", label: "Compliance Obligations", end: false },
            { to: "/portal/documents", label: "Shared Documents", end: false },
          ].map(({ to, label, end }) => (
            <NavLink
              key={to}
              to={to}
              end={end}
              className={({ isActive }) =>
                isActive
                  ? "text-brand-400 font-semibold border-b-2 border-brand-500 py-2.5 px-1 inline-flex items-center gap-1.5 text-xs sm:text-sm whitespace-nowrap"
                  : "text-slate-300 hover:text-white border-b-2 border-transparent py-2.5 px-1 inline-flex items-center gap-1.5 text-xs sm:text-sm whitespace-nowrap transition"
              }
            >
              {label}
            </NavLink>
          ))}
        </nav>
      </header>

      {/* Main Content Area */}
      <main className="flex-1">
        <div className="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
          <Outlet />
        </div>
      </main>

      {/* Modern Portal Footer */}
      <footer className="border-t border-slate-200 bg-white py-6 text-center text-xs text-slate-500">
        <div className="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8 flex flex-col sm:flex-row items-center justify-between gap-2">
          <span>Chartered Accountant Practice Management System · Secure Client Portal</span>
          <span>End-to-End Encrypted & Audited Session</span>
        </div>
      </footer>
    </div>
  );
}
