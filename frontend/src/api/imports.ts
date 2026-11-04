import { apiFetch } from "./client";

export type ImportBatchStatus =
  | "Received" | "Parsed" | "Validated" | "Committed" | "Failed" | "Cancelled";
export type ImportDecision = "Pending" | "CreateNew" | "UpdateExisting" | "Skip";

export interface BatchDto {
  id: string;
  fileName: string;
  fileSha256: string;
  fileSizeBytes: number;
  status: ImportBatchStatus;
  sourceSheet: string | null;
  uploadedAtUtc: string;
  rowCount: number;
  committedClientCount: number;
  committedServiceFileCount: number;
  errorCount: number;
  notes: string | null;
  version: number;
}

export interface StagingClientDto {
  id: string;
  batchId: string;
  rowNumber: number;
  normalizedPan: string | null;
  normalizedLegalName: string | null;
  normalizedTradeName: string | null;
  normalizedLegacyClientId: string | null;
  entityTypeCode: string | null;
  clientGroupCode: string | null;
  validationErrorsJson: string | null;
  candidateClientIdsJson: string | null;
  decision: ImportDecision;
  decidedClientId: string | null;
  committedClientId: string | null;
}

export interface StagingServiceFileDto {
  id: string;
  batchId: string;
  sourceStagingClientId: string;
  rowNumber: number;
  serviceCode: string;
  legacyFileNumber: string | null;
  validationErrorsJson: string | null;
  decision: ImportDecision;
  committedServiceFileId: string | null;
}

export interface ReconciliationDto {
  batch: BatchDto;
  pending: number;
  createNew: number;
  updateExisting: number;
  skip: number;
  clientRowsWithErrors: number;
  serviceFileRowsWithErrors: number;
}

const STORAGE_KEY = "cms.auth.v1";
function tokenFromStorage(): string | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    const p = JSON.parse(raw) as { token?: string };
    return p.token ?? null;
  } catch { return null; }
}

export const importsApi = {
  list: () => apiFetch<BatchDto[]>("/api/v1/imports"),
  get: (id: string) => apiFetch<BatchDto>(`/api/v1/imports/${id}`),
  rows: (id: string) => apiFetch<StagingClientDto[]>(`/api/v1/imports/${id}/rows`),
  serviceFileRows: (id: string) => apiFetch<StagingServiceFileDto[]>(`/api/v1/imports/${id}/service-file-rows`),
  reconciliation: (id: string) => apiFetch<ReconciliationDto>(`/api/v1/imports/${id}/reconciliation`),
  validate: (id: string) =>
    apiFetch<BatchDto>(`/api/v1/imports/${id}/validate`, { method: "POST" }),
  autoResolve: (id: string) =>
    apiFetch<BatchDto>(`/api/v1/imports/${id}/auto-resolve`, { method: "POST" }),
  commit: (id: string) =>
    apiFetch<{ clientsCreated: number; clientsUpdated: number; serviceFilesCreated: number; rowsSkipped: number; errors: number }>(
      `/api/v1/imports/${id}/commit`, { method: "POST" }),
  setRowDecision: (rowId: string, decision: ImportDecision, decidedClientId: string | null) =>
    apiFetch<StagingClientDto>(`/api/v1/imports/rows/${rowId}`, {
      method: "PATCH", body: JSON.stringify({ decision, decidedClientId }),
    }),
  upload: async (file: File): Promise<{ batchId: string; wasDuplicate: boolean; sha256: string }> => {
    const fd = new FormData();
    fd.append("file", file);
    // apiFetch sets Content-Type: application/json when a body is present; for multipart, bypass it.
    const token = tokenFromStorage();
    const res = await fetch("/api/v1/imports/upload", {
      method: "POST",
      headers: token ? { Authorization: `Bearer ${token}` } : {},
      body: fd,
    });
    if (!res.ok) throw new Error(`Upload failed (${res.status})`);
    return await res.json();
  },
};
