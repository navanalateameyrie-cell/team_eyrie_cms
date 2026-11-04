import { apiFetch, ApiError } from "./client";

export interface Kpis {
  activeClients: number;
  inactiveClients: number;
  activeServiceFiles: number;
  onHoldServiceFiles: number;
  closedServiceFiles: number;
  openEngagements: number;
  inProgressEngagements: number;
  completedEngagements: number;
  activeBranches: number;
  activeStaff: number;
}

export interface Breakdown {
  label: string;
  count: number;
}

export const dashboardApi = {
  kpis: () => apiFetch<Kpis>("/api/v1/dashboard/kpis"),
  clientsByEntityType: () => apiFetch<Breakdown[]>("/api/v1/dashboard/clients-by-entity-type"),
  clientsByState: () => apiFetch<Breakdown[]>("/api/v1/dashboard/clients-by-state"),
  serviceFilesByService: () => apiFetch<Breakdown[]>("/api/v1/dashboard/service-files-by-service"),
  staffWorkloadTop: async (take = 10): Promise<Breakdown[] | null> => {
    try {
      return await apiFetch<Breakdown[]>(`/api/v1/dashboard/staff-workload-top?take=${take}`);
    } catch (ex) {
      if (ex instanceof ApiError && ex.status === 403) return null;
      throw ex;
    }
  },
};
