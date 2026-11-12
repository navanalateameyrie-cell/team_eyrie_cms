import { apiRawFetch } from "./client";

async function downloadXlsx(path: string, suggestedName: string) {
  const res = await apiRawFetch(path);
  if (!res.ok) {
    if (res.status === 403) throw new Error("You don't have permission to export.");
    throw new Error(`Export failed (${res.status})`);
  }
  const blob = await res.blob();
  const cd = res.headers.get("Content-Disposition") ?? "";
  const match = /filename\*?=(?:UTF-8''|")?([^";\s]+)/i.exec(cd);
  const name = match ? decodeURIComponent(match[1]) : suggestedName;

  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = name;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}

export const exportsApi = {
  clients: (opts: { search?: string; active?: boolean; entityTypeId?: string; clientGroupId?: string } = {}) => {
    const p = new URLSearchParams();
    if (opts.search) p.set("search", opts.search);
    if (opts.active !== undefined) p.set("active", String(opts.active));
    if (opts.entityTypeId) p.set("entityTypeId", opts.entityTypeId);
    if (opts.clientGroupId) p.set("clientGroupId", opts.clientGroupId);
    const qs = p.toString();
    return downloadXlsx(qs ? `/api/v1/exports/clients.xlsx?${qs}` : "/api/v1/exports/clients.xlsx",
                        "clients.xlsx");
  },
  serviceFiles: (opts: { search?: string; status?: string; serviceDefinitionId?: string; clientId?: string } = {}) => {
    const p = new URLSearchParams();
    if (opts.search) p.set("search", opts.search);
    if (opts.status) p.set("status", opts.status);
    if (opts.serviceDefinitionId) p.set("serviceDefinitionId", opts.serviceDefinitionId);
    if (opts.clientId) p.set("clientId", opts.clientId);
    const qs = p.toString();
    return downloadXlsx(qs ? `/api/v1/exports/service-files.xlsx?${qs}` : "/api/v1/exports/service-files.xlsx",
                        "service-files.xlsx");
  },
  branches: (opts: { clientId?: string; active?: boolean } = {}) => {
    const p = new URLSearchParams();
    if (opts.clientId) p.set("clientId", opts.clientId);
    if (opts.active !== undefined) p.set("active", String(opts.active));
    const qs = p.toString();
    return downloadXlsx(qs ? `/api/v1/exports/branches.xlsx?${qs}` : "/api/v1/exports/branches.xlsx",
                        "branches.xlsx");
  },
};
