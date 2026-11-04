import { apiFetch } from "./client";

export type AssignmentScope = "Client" | "ServiceFile";
export type AssignmentRole = "Primary" | "Secondary" | "Reviewer";

export interface AssignmentDto {
  id: string;
  scope: AssignmentScope;
  scopeId: string;
  staffId: string;
  staffCode: string | null;
  staffFullName: string | null;
  role: AssignmentRole;
  effectiveFrom: string;
  effectiveTo: string | null;
  notes: string | null;
  version: number;
}

export interface AssignBody {
  staffId: string;
  role: AssignmentRole;
  effectiveFrom: string;
  notes: string | null;
}

const clientBase = (clientId: string) => `/api/v1/clients/${clientId}/staff-assignments`;
const fileBase = (clientId: string, fileId: string) =>
  `/api/v1/clients/${clientId}/service-files/${fileId}/staff-assignments`;

export const clientAssignmentsApi = {
  list: (clientId: string, opts: { asOf?: string; includeHistory?: boolean; role?: AssignmentRole } = {}) => {
    const p = new URLSearchParams();
    if (opts.asOf) p.set("asOf", opts.asOf);
    if (opts.includeHistory) p.set("includeHistory", "true");
    if (opts.role) p.set("role", opts.role);
    const qs = p.toString();
    return apiFetch<AssignmentDto[]>(qs ? `${clientBase(clientId)}?${qs}` : clientBase(clientId));
  },
  assign: (clientId: string, body: AssignBody) =>
    apiFetch<AssignmentDto>(clientBase(clientId), { method: "POST", body: JSON.stringify(body) }),
  end: (clientId: string, id: string, version: number, effectiveTo?: string) => {
    const d = effectiveTo ? `&effectiveTo=${effectiveTo}` : "";
    return apiFetch<AssignmentDto>(
      `${clientBase(clientId)}/${id}/end?version=${version}${d}`, { method: "POST" }
    );
  },
};

export const fileAssignmentsApi = {
  list: (clientId: string, fileId: string, opts: { asOf?: string; includeHistory?: boolean; role?: AssignmentRole } = {}) => {
    const p = new URLSearchParams();
    if (opts.asOf) p.set("asOf", opts.asOf);
    if (opts.includeHistory) p.set("includeHistory", "true");
    if (opts.role) p.set("role", opts.role);
    const qs = p.toString();
    return apiFetch<AssignmentDto[]>(qs ? `${fileBase(clientId, fileId)}?${qs}` : fileBase(clientId, fileId));
  },
  assign: (clientId: string, fileId: string, body: AssignBody) =>
    apiFetch<AssignmentDto>(fileBase(clientId, fileId), { method: "POST", body: JSON.stringify(body) }),
  end: (clientId: string, fileId: string, id: string, version: number, effectiveTo?: string) => {
    const d = effectiveTo ? `&effectiveTo=${effectiveTo}` : "";
    return apiFetch<AssignmentDto>(
      `${fileBase(clientId, fileId)}/${id}/end?version=${version}${d}`, { method: "POST" }
    );
  },
};
