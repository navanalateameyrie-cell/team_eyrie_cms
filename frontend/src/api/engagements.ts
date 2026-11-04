import { apiFetch } from "./client";

export type EngagementStatus = "Planned" | "InProgress" | "Completed" | "Cancelled";
export type EngagementPeriodType = "Annual" | "HalfYear" | "Quarter" | "Month" | "AdHoc";

export interface EngagementDto {
  id: string;
  serviceFileId: string;
  financialYear: string;
  assessmentYear: string | null;
  periodType: EngagementPeriodType;
  periodLabel: string | null;
  plannedStartDate: string;
  plannedEndDate: string;
  actualStartDate: string | null;
  actualEndDate: string | null;
  status: EngagementStatus;
  notes: string | null;
  version: number;
}

export interface CreateEngagementBody {
  financialYear: string;
  assessmentYear: string | null;
  periodType: EngagementPeriodType;
  periodLabel: string | null;
  plannedStartDate: string;
  plannedEndDate: string;
  notes: string | null;
}

export interface UpdateEngagementBody extends CreateEngagementBody {
  version: number;
}

const base = (clientId: string, fileId: string) =>
  `/api/v1/clients/${clientId}/service-files/${fileId}/engagements`;

export const engagementsApi = {
  list: (clientId: string, fileId: string, financialYear?: string) => {
    const qs = financialYear ? `?financialYear=${encodeURIComponent(financialYear)}` : "";
    return apiFetch<EngagementDto[]>(`${base(clientId, fileId)}${qs}`);
  },
  create: (clientId: string, fileId: string, body: CreateEngagementBody) =>
    apiFetch<EngagementDto>(base(clientId, fileId), { method: "POST", body: JSON.stringify(body) }),
  update: (clientId: string, fileId: string, id: string, body: UpdateEngagementBody) =>
    apiFetch<EngagementDto>(`${base(clientId, fileId)}/${id}`, { method: "PUT", body: JSON.stringify(body) }),
  start: (clientId: string, fileId: string, id: string, version: number, actualStart?: string) => {
    const d = actualStart ? `&actualStart=${actualStart}` : "";
    return apiFetch<EngagementDto>(
      `${base(clientId, fileId)}/${id}/start?version=${version}${d}`, { method: "POST" }
    );
  },
  complete: (clientId: string, fileId: string, id: string, version: number, actualEnd?: string) => {
    const d = actualEnd ? `&actualEnd=${actualEnd}` : "";
    return apiFetch<EngagementDto>(
      `${base(clientId, fileId)}/${id}/complete?version=${version}${d}`, { method: "POST" }
    );
  },
  cancel: (clientId: string, fileId: string, id: string, version: number) =>
    apiFetch<EngagementDto>(
      `${base(clientId, fileId)}/${id}/cancel?version=${version}`, { method: "POST" }
    ),
};
