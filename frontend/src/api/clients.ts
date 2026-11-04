import { apiFetch, ApiError } from "./client";

export interface ClientDto {
  id: string;
  code: string;
  legacyClientId: string | null;
  legalName: string;
  tradeName: string | null;
  pan: string | null;
  entityTypeId: string;
  entityTypeName: string | null;
  clientGroupId: string | null;
  clientGroupName: string | null;
  isActive: boolean;
  version: number;
}

export interface ClientSummaryDto {
  id: string;
  code: string;
  legalName: string;
  tradeName: string | null;
  pan: string | null;
  isActive: boolean;
}

export interface ClientListResponse {
  total: number;
  skip: number;
  take: number;
  items: ClientSummaryDto[];
}

export interface DuplicatePanCandidate {
  id: string;
  code: string;
  legalName: string;
  isActive: boolean;
}

export interface ClientUpsertBody {
  legalName: string;
  entityTypeId: string;
  tradeName?: string | null;
  pan?: string | null;
  clientGroupId?: string | null;
  legacyClientId?: string | null;
  acknowledgeDuplicatePan?: boolean;
  version?: number;
}

export interface DuplicatePanProblem {
  error: "duplicate_pan_requires_acknowledgement";
  pan: string;
  candidates: DuplicatePanCandidate[];
}

export const clientsApi = {
  list: (opts: {
    search?: string;
    active?: boolean;
    entityTypeId?: string;
    clientGroupId?: string;
    skip?: number;
    take?: number;
  } = {}) => {
    const p = new URLSearchParams();
    if (opts.search) p.set("search", opts.search);
    if (opts.active !== undefined) p.set("active", String(opts.active));
    if (opts.entityTypeId) p.set("entityTypeId", opts.entityTypeId);
    if (opts.clientGroupId) p.set("clientGroupId", opts.clientGroupId);
    if (opts.skip !== undefined) p.set("skip", String(opts.skip));
    if (opts.take !== undefined) p.set("take", String(opts.take));
    const qs = p.toString();
    return apiFetch<ClientListResponse>(qs ? `/api/v1/clients?${qs}` : "/api/v1/clients");
  },
  get: (id: string) => apiFetch<ClientDto>(`/api/v1/clients/${id}`),
  getByCode: (code: string) => apiFetch<ClientDto>(`/api/v1/clients/by-code/${code}`),
  duplicatePanCandidates: (pan: string, excludeId?: string) => {
    const p = new URLSearchParams({ pan });
    if (excludeId) p.set("excludeId", excludeId);
    return apiFetch<DuplicatePanCandidate[]>(`/api/v1/clients/duplicate-pan-candidates?${p}`);
  },
  create: (body: ClientUpsertBody) =>
    apiFetch<ClientDto>("/api/v1/clients", { method: "POST", body: JSON.stringify(body) }),
  update: (id: string, body: ClientUpsertBody & { version: number }) =>
    apiFetch<ClientDto>(`/api/v1/clients/${id}`, { method: "PUT", body: JSON.stringify(body) }),
  deactivate: (id: string, version: number) =>
    apiFetch<ClientDto>(`/api/v1/clients/${id}/deactivate?version=${version}`, { method: "POST" }),
  reactivate: (id: string, version: number) =>
    apiFetch<ClientDto>(`/api/v1/clients/${id}/reactivate?version=${version}`, { method: "POST" }),
};

export function isDuplicatePanProblem(err: unknown): err is ApiError & { problem: DuplicatePanProblem } {
  if (!(err instanceof ApiError) || err.status !== 422) return false;
  try {
    const parsed = JSON.parse(err.body) as Partial<DuplicatePanProblem>;
    return parsed.error === "duplicate_pan_requires_acknowledgement";
  } catch {
    return false;
  }
}

export function parseDuplicatePanProblem(err: ApiError): DuplicatePanProblem {
  return JSON.parse(err.body) as DuplicatePanProblem;
}
