import { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import {
  apiUrl,
  readStorage,
  setAuthStorage,
  clearAuthStorage,
} from "../api/client";

export interface AuthState {
  token: string | null;
  refreshToken: string | null;
  userName: string | null;
  roles: string[];
  permissions: string[];
}

export interface LoginResult {
  mfaRequired: boolean;
  mfaPendingToken?: string;
}

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
  const env = readStorage();
  if (!env || !env.token) return empty;
  return {
    token: env.token,
    refreshToken: env.refreshToken ?? null,
    userName: env.userName ?? null,
    roles: env.roles ?? [],
    permissions: env.permissions ?? [],
  };
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>(loadFromStorage);

  useEffect(() => {
    // Keep local storage in sync whenever state changes
    if (state.token) {
      setAuthStorage(state);
    } else {
      clearAuthStorage();
    }
  }, [state]);

  const login = useCallback(async (email: string, password: string) => {
    const res = await fetch(apiUrl("/api/v1/auth/login"), {
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
    const res = await fetch(apiUrl("/api/v1/auth/mfa/challenge"), {
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
    const me = await fetch(apiUrl("/api/v1/auth/me"), {
      headers: { Authorization: `Bearer ${data.accessToken}` },
    }).then((r) => (r.ok ? r.json() : null)).catch(() => null);

    const nextState: AuthState = {
      token: data.accessToken,
      refreshToken: data.refreshToken,
      userName: me?.userName ?? email ?? me?.email ?? "signed in",
      roles: data.roles ?? [],
      permissions: data.permissions ?? [],
    };

    // 1. SYNCHRONOUSLY store in memory and localStorage IMMEDIATELY.
    // This avoids async React state lag when subsequent route transition or API calls run.
    setAuthStorage(nextState);

    // 2. Update React state
    setState(nextState);
  }

  const logout = useCallback(async () => {
    const current = state.refreshToken;

    // 1. SYNCHRONOUSLY clear credentials from memory and localStorage IMMEDIATELY
    clearAuthStorage();
    setState(empty);

    // 2. Best-effort server-side token revocation
    try {
      if (current) {
        await fetch(apiUrl("/api/v1/auth/logout"), {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ refreshToken: current }),
        });
      }
    } catch { /* best-effort — local state is already cleared */ }
  }, [state.refreshToken]);

  const logoutEverywhere = useCallback(async () => {
    const currentToken = state.token;

    // 1. SYNCHRONOUSLY clear credentials from memory and localStorage IMMEDIATELY
    clearAuthStorage();
    setState(empty);

    // 2. Best-effort server-side token revocation
    try {
      if (currentToken) {
        await fetch(apiUrl("/api/v1/auth/logout-everywhere"), {
          method: "POST",
          headers: { Authorization: `Bearer ${currentToken}` },
        });
      }
    } catch { /* best-effort — local state is already cleared */ }
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
