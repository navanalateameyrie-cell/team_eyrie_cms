import { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";

const STORAGE_KEY = "cms.auth.v1";

export interface AuthState {
  token: string | null;
  refreshToken: string | null;
  userName: string | null;
  roles: string[];
  permissions: string[];
}

export interface LoginResult { mfaRequired: boolean; mfaPendingToken?: string }

interface AuthContextValue extends AuthState {
  login: (email: string, password: string) => Promise<LoginResult>;
  completeMfa: (pendingToken: string, code: string) => Promise<void>;
  logout: () => Promise<void>;
  logoutEverywhere: () => Promise<void>;
  hasPermission: (perm: string) => boolean;
}

const empty: AuthState = { token: null, refreshToken: null, userName: null, roles: [], permissions: [] };

const AuthContext = createContext<AuthContextValue | null>(null);

function loadFromStorage(): AuthState {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as AuthState) : empty;
  } catch {
    return empty;
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>(loadFromStorage);

  useEffect(() => {
    try {
      if (state.token) localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
      else localStorage.removeItem(STORAGE_KEY);
    } catch {
      // ignore
    }
  }, [state]);

  const login = useCallback(async (email: string, password: string) => {
    const res = await fetch("/api/v1/auth/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email, password }),
    });
    if (!res.ok) {
      const text = await res.text().catch(() => "");
      throw new Error(text || `Login failed (${res.status})`);
    }
    const raw = await res.json();
    if (raw?.mfaRequired) {
      // Caller must now gather a 6-digit code and call completeMfa().
      return { mfaRequired: true, mfaPendingToken: raw.mfaPendingToken as string };
    }
    await ingestTokens(email, raw);
    return { mfaRequired: false };
  }, []);

  const completeMfa = useCallback(async (pendingToken: string, code: string) => {
    const res = await fetch("/api/v1/auth/mfa/challenge", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ mfaPendingToken: pendingToken, code }),
    });
    if (!res.ok) {
      const text = await res.text().catch(() => "");
      throw new Error(text || `MFA challenge failed (${res.status})`);
    }
    await ingestTokens(null, await res.json());
  }, []);

  async function ingestTokens(email: string | null, data: {
    accessToken: string; refreshToken: string; roles: string[]; permissions: string[];
  }) {
    const me = await fetch("/api/v1/auth/me", {
      headers: { Authorization: `Bearer ${data.accessToken}` },
    }).then((r) => (r.ok ? r.json() : null));
    setState({
      token: data.accessToken,
      refreshToken: data.refreshToken,
      userName: me?.userName ?? email ?? me?.email ?? "signed in",
      roles: data.roles ?? [],
      permissions: data.permissions ?? [],
    });
  }

  const logout = useCallback(async () => {
    const current = state.refreshToken;
    try {
      if (current) {
        await fetch("/api/v1/auth/logout", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ refreshToken: current }),
        });
      }
    } catch { /* best-effort — clear local state regardless */ }
    setState(empty);
  }, [state.refreshToken]);

  const logoutEverywhere = useCallback(async () => {
    try {
      if (state.token) {
        await fetch("/api/v1/auth/logout-everywhere", {
          method: "POST",
          headers: { Authorization: `Bearer ${state.token}` },
        });
      }
    } catch { /* best-effort */ }
    setState(empty);
  }, [state.token]);

  const hasPermission = useCallback(
    (perm: string) =>
      state.permissions.includes("admin.full") || state.permissions.includes(perm),
    [state.permissions]
  );

  const value = useMemo<AuthContextValue>(
    () => ({ ...state, login, completeMfa, logout, logoutEverywhere, hasPermission }),
    [state, login, completeMfa, logout, logoutEverywhere, hasPermission]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within <AuthProvider>");
  return ctx;
}
