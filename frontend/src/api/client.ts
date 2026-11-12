export interface HealthResponse {
  status: string;
  service: string;
  version: string;
  utc: string;
}

export const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? "").replace(/\/+$/, "");

export function apiUrl(path: string): string {
  if (/^https?:\/\//i.test(path)) return path;
  const normalized = path.startsWith("/") ? path : `/${path}`;
  return `${API_BASE_URL}${normalized}`;
}

export const STORAGE_KEY = "cms.auth.v1";
const CORRELATION_HEADER = "X-Correlation-Id";

function newCorrelationId(): string {
  return (crypto.randomUUID?.() ?? Math.random().toString(16).slice(2)).replace(/-/g, "");
}

export interface AuthEnvelope {
  token?: string | null;        // access token
  refreshToken?: string | null;
  userName?: string | null;
  roles?: string[];
  permissions?: string[];
}

let inMemoryToken: string | null = null;
let inMemoryRefreshToken: string | null = null;

export function readStorage(): AuthEnvelope | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as AuthEnvelope) : null;
  } catch { return null; }
}

export function writeStorage(patch: Partial<AuthEnvelope>) {
  try {
    const current = readStorage() ?? {};
    const updated = { ...current, ...patch };
    if (patch.token !== undefined) inMemoryToken = patch.token ?? null;
    if (patch.refreshToken !== undefined) inMemoryRefreshToken = patch.refreshToken ?? null;
    localStorage.setItem(STORAGE_KEY, JSON.stringify(updated));
  } catch { /* ignore */ }
}

export function setAuthStorage(env: AuthEnvelope) {
  inMemoryToken = env.token ?? null;
  inMemoryRefreshToken = env.refreshToken ?? null;
  try {
    if (env.token) {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(env));
    } else {
      localStorage.removeItem(STORAGE_KEY);
    }
  } catch { /* ignore */ }
}

export function clearAuthStorage() {
  inMemoryToken = null;
  inMemoryRefreshToken = null;
  try {
    localStorage.removeItem(STORAGE_KEY);
  } catch { /* ignore */ }
}

// Initialise memory cache on module load
try {
  const initial = readStorage();
  if (initial?.token) {
    inMemoryToken = initial.token;
    inMemoryRefreshToken = initial.refreshToken ?? null;
  }
} catch { /* ignore */ }

export function getAccessToken(): string | null {
  if (inMemoryToken) return inMemoryToken;
  const env = readStorage();
  if (env?.token) {
    inMemoryToken = env.token;
    inMemoryRefreshToken = env.refreshToken ?? null;
    return env.token;
  }
  return null;
}

export class ApiError extends Error {
  constructor(public status: number, public body: string, message?: string) {
    super(message ?? `API ${status}: ${body || "no body"}`);
  }
}

/**
 * Coordinates refresh calls across concurrent requests. When many requests get 401 at the same
 * time, we want exactly ONE call to `/auth/refresh`; everyone else awaits its outcome.
 */
let refreshInFlight: Promise<string | null> | null = null;

async function tryRefresh(): Promise<string | null> {
  const env = readStorage();
  const refreshToken = inMemoryRefreshToken ?? env?.refreshToken;
  if (!refreshToken) return null;
  if (refreshInFlight) return refreshInFlight;

  refreshInFlight = (async () => {
    try {
      const res = await fetch(apiUrl("/api/v1/auth/refresh"), {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ refreshToken }),
      });
      if (!res.ok) {
        clearAuthStorage();
        return null;
      }
      const data = await res.json() as { accessToken: string; refreshToken: string };
      writeStorage({ token: data.accessToken, refreshToken: data.refreshToken });
      return data.accessToken;
    } catch { return null; }
    finally { refreshInFlight = null; }
  })();
  return refreshInFlight;
}

async function fetchOnce(
  path: string,
  init: RequestInit | undefined,
  bearer: string | null,
  expectJson: boolean,
): Promise<Response> {
  const headers = new Headers(init?.headers);
  if (!headers.has(CORRELATION_HEADER)) headers.set(CORRELATION_HEADER, newCorrelationId());
  const isFormData = typeof FormData !== "undefined" && init?.body instanceof FormData;
  if (expectJson && !headers.has("Accept")) headers.set("Accept", "application/json");
  if (init?.body && !isFormData && expectJson && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }
  if (bearer && !headers.has("Authorization")) headers.set("Authorization", `Bearer ${bearer}`);
  return fetch(apiUrl(path), { ...init, headers });
}

export async function apiRawFetch(
  path: string,
  init?: RequestInit,
  options?: { expectJson?: boolean },
): Promise<Response> {
  const expectJson = options?.expectJson ?? false;
  let access = getAccessToken();
  if (!access) {
    access = await tryRefresh();
  }

  let res = await fetchOnce(path, init, access, expectJson);

  // 401 → attempt one silent refresh then replay. Only redirects to /login if that also fails.
  if (res.status === 401) {
    const next = await tryRefresh();
    if (next && next !== access) {
      res = await fetchOnce(path, init, next, expectJson);
    } else if (typeof window !== "undefined" && window.location
               && !window.location.pathname.startsWith("/login")) {
      clearAuthStorage();
      const back = encodeURIComponent(window.location.pathname + window.location.search);
      window.location.href = `/login?next=${back}`;
    }
  }

  return res;
}

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await apiRawFetch(path, init, { expectJson: true });

  if (res.status === 204) return undefined as unknown as T;
  if (!res.ok) throw new ApiError(res.status, await res.text().catch(() => ""));
  return (await res.json()) as T;
}

export const api = {
  health: () => apiFetch<HealthResponse>("/api/v1/health"),
};
