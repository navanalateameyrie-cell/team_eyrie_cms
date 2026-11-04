import { apiFetch } from "./client";

export type ServiceFileStatus = "Open" | "OnHold" | "Closed";

export interface ServiceFileDto {
  id: string;
  fileNumber: string;
  legacyFileNumber: string | null;
  clientId: string;
  clientCode: string | null;
  clientLegalName: string | null;
  clientPan: string | null;
  clientGroupId: string | null;
  clientGroupCode: string | null;
  clientGroupName: string | null;
  serviceDefinitionId: string;
  serviceCode: string | null;
  serviceName: string | null;
  branchId: string | null;
  branchCode: string | null;
  branchName: string | null;
  gstin: string | null;
  tan: string | null;
  status: ServiceFileStatus;
  openedOn: string;
  closedOn: string | null;
  notes: string | null;
  metadataJson: string | null;
  version: number;
}

export interface CreateServiceFileBody {
  serviceDefinitionId: string;
  branchId: string | null;
  openedOn: string;
  legacyFileNumber: string | null;
  notes: string | null;
  metadataJson?: string | null;
}

export interface UpdateServiceFileBody {
  serviceDefinitionId: string;
  branchId: string | null;
  legacyFileNumber: string | null;
  notes: string | null;
  version: number;
  metadataJson?: string | null;
}

export const serviceFilesApi = {
  list: (clientId: string, opts: {
    status?: ServiceFileStatus;
    serviceDefinitionId?: string;
    branchId?: string;
    search?: string;
  } = {}) => {
    const p = new URLSearchParams();
    if (opts.status) p.set("status", opts.status);
    if (opts.serviceDefinitionId) p.set("serviceDefinitionId", opts.serviceDefinitionId);
    if (opts.branchId) p.set("branchId", opts.branchId);
    if (opts.search) p.set("search", opts.search);
    const qs = p.toString();
    return apiFetch<ServiceFileDto[]>(
      qs ? `/api/v1/clients/${clientId}/service-files?${qs}` : `/api/v1/clients/${clientId}/service-files`
    );
  },
  create: (clientId: string, body: CreateServiceFileBody) =>
    apiFetch<ServiceFileDto>(`/api/v1/clients/${clientId}/service-files`, {
      method: "POST", body: JSON.stringify(body),
    }),
  update: (clientId: string, id: string, body: UpdateServiceFileBody) =>
    apiFetch<ServiceFileDto>(`/api/v1/clients/${clientId}/service-files/${id}`, {
      method: "PUT", body: JSON.stringify(body),
    }),
  close: (clientId: string, id: string, version: number) =>
    apiFetch<ServiceFileDto>(
      `/api/v1/clients/${clientId}/service-files/${id}/close?version=${version}`,
      { method: "POST" }
    ),
  reopen: (clientId: string, id: string, version: number) =>
    apiFetch<ServiceFileDto>(
      `/api/v1/clients/${clientId}/service-files/${id}/reopen?version=${version}`,
      { method: "POST" }
    ),
  putOnHold: (clientId: string, id: string, version: number) =>
    apiFetch<ServiceFileDto>(
      `/api/v1/clients/${clientId}/service-files/${id}/put-on-hold?version=${version}`,
      { method: "POST" }
    ),
  resume: (clientId: string, id: string, version: number) =>
    apiFetch<ServiceFileDto>(
      `/api/v1/clients/${clientId}/service-files/${id}/resume?version=${version}`,
      { method: "POST" }
    ),
  globalSearch: (search: string) =>
    apiFetch<ServiceFileDto[]>(`/api/v1/service-files?search=${encodeURIComponent(search)}`),
};
