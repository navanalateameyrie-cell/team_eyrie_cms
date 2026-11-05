import { Link, Navigate, useLocation } from "react-router-dom";
import type { ReactNode } from "react";
import { useAuth } from "../auth/AuthContext";

export function RequireAuth({ children }: { children: ReactNode }) {
  const { token } = useAuth();
  const loc = useLocation();
  if (!token) return <Navigate to="/login" state={{ from: loc.pathname }} replace />;
  return <>{children}</>;
}

export function RequirePermission({
  permission,
  children,
}: {
  permission: string;
  children: ReactNode;
}) {
  const { token, hasPermission, roles, userName, logout } = useAuth();
  const loc = useLocation();
  if (!token) return <Navigate to="/login" state={{ from: loc.pathname }} replace />;

  if (!hasPermission(permission)) {
    return (
      <div className="min-h-[70vh] flex items-center justify-center p-6">
        <div className="max-w-md w-full bg-white rounded-2xl shadow-xl border border-slate-200 overflow-hidden text-center">
          {/* Header Banner */}
          <div className="bg-gradient-to-br from-[#102747] to-[#19385F] px-6 py-8 text-white relative">
            <div className="inline-flex items-center justify-center w-16 h-16 rounded-2xl bg-white/10 backdrop-blur-md border border-white/20 mb-3 shadow-inner">
              <svg className="w-8 h-8 text-[#E8C98F]" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.75} d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
              </svg>
            </div>
            <div className="inline-block px-3 py-1 rounded-full text-xs font-semibold tracking-wide uppercase bg-[#C86A24]/30 text-[#FDBA74] border border-[#FDBA74]/30 mb-2">
              RBAC Authorization Gate
            </div>
            <h2 className="text-xl font-bold tracking-tight text-white">Access Restricted</h2>
            <p className="text-xs text-slate-300 mt-1">
              You do not have the required statutory permission for this resource.
            </p>
          </div>

          {/* Details Body */}
          <div className="p-6 space-y-4 text-left">
            <div className="p-3.5 rounded-xl bg-slate-50 border border-slate-200/80 space-y-2 text-xs">
              <div className="flex justify-between items-center">
                <span className="text-slate-500 font-medium">Signed In As:</span>
                <span className="font-semibold text-slate-800">{userName || "Authenticated User"}</span>
              </div>
              <div className="flex justify-between items-center">
                <span className="text-slate-500 font-medium">Assigned Role(s):</span>
                <span className="inline-flex gap-1 flex-wrap justify-end">
                  {roles && roles.length > 0 ? (
                    roles.map((r) => (
                      <span key={r} className="px-2 py-0.5 rounded-md font-semibold text-[11px] bg-blue-100 text-[#102747]">
                        {r}
                      </span>
                    ))
                  ) : (
                    <span className="text-slate-400 italic">None</span>
                  )}
                </span>
              </div>
              <div className="flex justify-between items-center pt-1 border-t border-slate-200">
                <span className="text-slate-500 font-medium">Required Permission:</span>
                <code className="px-2 py-0.5 rounded font-mono text-[11px] font-bold text-amber-800 bg-amber-100/80 border border-amber-200">
                  {permission}
                </code>
              </div>
            </div>

            <p className="text-xs text-slate-500 leading-relaxed text-center">
              If your current engagement requires access to this module, please contact your practice partner or system administrator to request role reassignment.
            </p>

            <div className="pt-2 flex flex-col sm:flex-row gap-2">
              <Link
                to="/dashboard"
                className="flex-1 inline-flex items-center justify-center px-4 py-2.5 rounded-xl font-semibold text-xs text-white bg-gradient-to-r from-[#102747] to-[#19385F] hover:from-[#0C1E36] hover:to-[#102747] shadow transition-all"
              >
                ← Back to Dashboard
              </Link>
              <button
                type="button"
                onClick={() => void logout()}
                className="px-4 py-2.5 rounded-xl font-semibold text-xs text-slate-600 hover:text-slate-800 hover:bg-slate-100 border border-slate-200 transition-all"
              >
                Sign Out
              </button>
            </div>
          </div>
        </div>
      </div>
    );
  }

  return <>{children}</>;
}

/**
 * Granular in-page UI permission guard. Renders children only when permission is granted.
 */
export function HasPermission({
  permission,
  children,
  fallback = null,
}: {
  permission: string;
  children: ReactNode;
  fallback?: ReactNode;
}) {
  const { hasPermission } = useAuth();
  return hasPermission(permission) ? <>{children}</> : <>{fallback}</>;
}

/**
 * Granular guard for any of multiple permissions.
 */
export function HasAnyPermission({
  permissions,
  children,
  fallback = null,
}: {
  permissions: string[];
  children: ReactNode;
  fallback?: ReactNode;
}) {
  const { hasPermission } = useAuth();
  const granted = permissions.some((p) => hasPermission(p));
  return granted ? <>{children}</> : <>{fallback}</>;
}

/**
 * Role-based UI guard.
 */
export function HasRole({
  role,
  children,
  fallback = null,
}: {
  role: string | string[];
  children: ReactNode;
  fallback?: ReactNode;
}) {
  const { roles } = useAuth();
  const targetRoles = Array.isArray(role) ? role : [role];
  const hasRole =
    roles.includes("SystemAdmin") ||
    targetRoles.some((r) => roles.includes(r));
  return hasRole ? <>{children}</> : <>{fallback}</>;
}
