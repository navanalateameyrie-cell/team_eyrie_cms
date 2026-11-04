import { apiFetch } from "./client";
import type { AssignmentRole, AssignmentScope } from "./assignments";

export interface StaffWorkloadSummary {
  staffId: string;
  staffCode: string;
  staffFullName: string;
  staffIsActive: boolean;
  clientPrimaryCount: number;
  clientSecondaryCount: number;
  clientReviewerCount: number;
  filePrimaryCount: number;
  fileSecondaryCount: number;
  fileReviewerCount: number;
  totalClients: number;
  totalFiles: number;
}

export interface WorkloadRow {
  assignmentId: string;
  scope: AssignmentScope;
  scopeId: string;
  scopeCode: string | null;
  scopeLabel: string | null;
  role: AssignmentRole;
  effectiveFrom: string;
  effectiveTo: string | null;
}

export interface StaffWorkloadDetail {
  staffId: string;
  staffCode: string;
  staffFullName: string;
  staffIsActive: boolean;
  clients: WorkloadRow[];
  serviceFiles: WorkloadRow[];
}

export const workloadApi = {
  all: (asOf?: string) => {
    const qs = asOf ? `?asOf=${asOf}` : "";
    return apiFetch<StaffWorkloadSummary[]>(`/api/v1/workload${qs}`);
  },
  forStaff: (staffId: string, opts: { asOf?: string; includeHistory?: boolean } = {}) => {
    const p = new URLSearchParams();
    if (opts.asOf) p.set("asOf", opts.asOf);
    if (opts.includeHistory) p.set("includeHistory", "true");
    const qs = p.toString();
    return apiFetch<StaffWorkloadDetail>(
      qs ? `/api/v1/staff/${staffId}/workload?${qs}` : `/api/v1/staff/${staffId}/workload`
    );
  },
};
