import { apiFetch } from "./client";

export type AuditAction = "Create" | "Update" | "Delete" | "Read" | "Export";

export interface FieldDiff {
  field: string;
  oldValue: string | null;
  newValue: string | null;
  changed: boolean;
}

export interface AuditEntryDto {
  id: string;
  occurredAtUtc: string;
  entityType: string;
  entityId: string;
  action: AuditAction;
  changedFields: string | null;
  oldValues: string | null;
  newValues: string | null;
  userId: string | null;
  userName: string | null;
  correlationId: string | null;
  ipAddress: string | null;
  userAgent: string | null;
  context: string | null;
  diff: FieldDiff[] | null;
}

export interface ListResponse {
  total: number;
  skip: number;
  take: number;
  items: AuditEntryDto[];
}

export interface AuditEntryDetail {
  entry: AuditEntryDto;
  diff: FieldDiff[];
}

export interface FilterOptionsDto {
  entityTypes: string[];
  users: { userId: string | null; userName: string }[];
  actions: { id: number; name: AuditAction }[];
}

export interface AuditQuery {
  clientId?: string;
  entityType?: string;
  entityId?: string;
  userId?: string;
  userName?: string;
  correlationId?: string;
  action?: AuditAction;
  fromUtc?: string;
  toUtc?: string;
  search?: string;
  skip?: number;
  take?: number;
}

export const auditApi = {
  list: (q: AuditQuery = {}) => {
    const p = new URLSearchParams();
    for (const [k, v] of Object.entries(q))
      if (v !== undefined && v !== "" && v !== null) p.set(k, String(v));
    const qs = p.toString();
    return apiFetch<ListResponse>(qs ? `/api/v1/audit?${qs}` : "/api/v1/audit");
  },
  get: (id: string) => apiFetch<AuditEntryDetail>(`/api/v1/audit/${id}`),
  filterOptions: () => apiFetch<FilterOptionsDto>("/api/v1/audit/filter-options"),
  exportUrl: (q: AuditQuery = {}) => {
    const p = new URLSearchParams();
    for (const [k, v] of Object.entries(q))
      if (v !== undefined && v !== "" && v !== null && k !== "skip" && k !== "take") p.set(k, String(v));
    const qs = p.toString();
    return qs ? `/api/v1/audit/export?${qs}` : "/api/v1/audit/export";
  },
};
