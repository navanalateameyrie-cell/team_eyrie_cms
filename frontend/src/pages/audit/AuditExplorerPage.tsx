import { useCallback, useEffect, useState } from "react";
import { auditApi, type AuditAction, type AuditEntryDto, type FieldDiff, type FilterOptionsDto } from "../../api/audit";
import { apiRawFetch } from "../../api/client";
import { clientsApi, type ClientSummaryDto } from "../../api/clients";
import { Card, Badge, Button, LoadingSpinner } from "../../components/ui";

const actionTone: Record<string, "neutral" | "brand" | "warning" | "success" | "danger"> = {
  Create: "success",
  Update: "brand",
  Delete: "danger",
  Read: "neutral",
  Export: "warning",
};

// The API returns action as a string enum ("Create" etc), but older callers / fixtures may still
// surface the numeric enum value. Normalise both.
const actionById: Record<number, string> = {
  1: "Create",
  2: "Update",
  3: "Delete",
  4: "Read",
  5: "Export",
};

function actionName(a: AuditAction | number | string | undefined): string {
  if (a === undefined || a === null) return "";
  if (typeof a === "number") return actionById[a] ?? String(a);
  return String(a);
}

interface Filters {
  clientId: string;
  entityType: string;
  entityId: string;
  action: string;
  userName: string;
  search: string;
  fromUtc: string;
  toUtc: string;
  skip: number;
  take: number;
}

function initialFilters(): Filters {
  // Seed from the URL querystring so deep-links from the client profile still work.
  const q = typeof window !== "undefined" ? new URLSearchParams(window.location.search) : new URLSearchParams();
  return {
    clientId: q.get("clientId") ?? "",
    entityType: q.get("entityType") ?? "",
    entityId: q.get("entityId") ?? "",
    action: q.get("action") ?? "",
    userName: q.get("userName") ?? "",
    search: q.get("search") ?? "",
    fromUtc: q.get("fromUtc") ?? "",
    toUtc: q.get("toUtc") ?? "",
    skip: parseInt(q.get("skip") ?? "0") || 0,
    take: 50,
  };
}

export default function AuditExplorerPage() {
  const [filters, setFilters] = useState<Filters>(initialFilters);
  const [opts, setOpts] = useState<FilterOptionsDto | null>(null);
  const [clients, setClients] = useState<ClientSummaryDto[]>([]);
  const [rows, setRows] = useState<AuditEntryDto[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [detailRow, setDetailRow] = useState<AuditEntryDto | null>(null);

  function setFilter(key: keyof Filters, value: string) {
    setFilters((prev) => ({ ...prev, [key]: value, skip: 0 }));
  }

  async function exportCsv() {
    const url = auditApi.exportUrl({
      entityType: filters.entityType || undefined,
      entityId: filters.entityId || undefined,
      action: (filters.action as AuditAction) || undefined,
      userName: filters.userName || undefined,
      search: filters.search || undefined,
      fromUtc: filters.fromUtc ? new Date(filters.fromUtc).toISOString() : undefined,
      toUtc: filters.toUtc ? new Date(filters.toUtc).toISOString() : undefined,
    });
    const res = await apiRawFetch(url);
    if (!res.ok) {
      setError(`Export failed (${res.status})`);
      return;
    }
    const blob = await res.blob();
    const href = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = href;
    a.download = `audit-${new Date().toISOString().replace(/[:.]/g, "-")}.csv`;
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(href), 2000);
  }

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const resp = await auditApi.list({
        clientId: filters.clientId || undefined,
        entityType: filters.entityType || undefined,
        entityId: filters.entityId || undefined,
        action: (filters.action as AuditAction) || undefined,
        userName: filters.userName || undefined,
        search: filters.search || undefined,
        fromUtc: filters.fromUtc ? new Date(filters.fromUtc).toISOString() : undefined,
        toUtc: filters.toUtc ? new Date(filters.toUtc).toISOString() : undefined,
        skip: filters.skip,
        take: filters.take,
      });
      setRows(resp.items);
      setTotal(resp.total);
    } catch (ex: unknown) {
      setError(ex instanceof Error ? ex.message : "Failed to load audit records");
    } finally {
      setLoading(false);
    }
  }, [filters]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    void auditApi.filterOptions().then(setOpts).catch(() => {});
  }, []);

  useEffect(() => {
    void clientsApi.list({ take: 100 }).then((r) => setClients(r.items)).catch(() => {});
  }, []);

  // Build diff from the row's inline diff field; fall back to parsing JSON pairs.
  function diffForRow(row: AuditEntryDto): FieldDiff[] {
    if (row.diff && row.diff.length > 0) return row.diff;
    try {
      const changed: string[] = row.changedFields ? JSON.parse(row.changedFields) : [];
      const oldDict: Record<string, unknown> = row.oldValues ? JSON.parse(row.oldValues) : {};
      const newDict: Record<string, unknown> = row.newValues ? JSON.parse(row.newValues) : {};
      const keys = Array.from(new Set([...Object.keys(oldDict), ...Object.keys(newDict)])).sort();
      return keys.map((k) => ({
        field: k,
        oldValue: oldDict[k] == null ? null : String(oldDict[k]),
        newValue: newDict[k] == null ? null : String(newDict[k]),
        changed: changed.includes(k),
      }));
    } catch {
      return [];
    }
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-2 border-b border-slate-200">
        <div>
          <div className="flex items-center gap-2.5">
            <h1 className="text-2xl font-bold tracking-tight text-slate-900">Audit Explorer</h1>
            <span className="inline-flex items-center rounded-full bg-amber-50 px-2.5 py-0.5 text-xs font-semibold text-amber-800 border border-amber-200">
              Non-Editable Audit Log
            </span>
          </div>
          <p className="text-xs sm:text-sm text-slate-500 mt-1">
            Append-only trail of every protected write and sensitive read / export across practice entities.
          </p>
        </div>
        <div className="flex items-center gap-3">
          <Button
            variant="outline"
            size="sm"
            onClick={exportCsv}
            icon={
              <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
              </svg>
            }
          >
            Export CSV
          </Button>
        </div>
      </div>

      {/* Filter Section */}
      <Card className="p-4 sm:p-5">
        <h2 className="text-xs font-bold uppercase tracking-wider text-slate-700 mb-3">
          Filter Activity Records
        </h2>
        <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-3 text-xs">
          <label className="col-span-1 sm:col-span-2">
            <span className="font-medium text-slate-600 block mb-1">Search (entity id, context, user)</span>
            <input
              value={filters.search}
              onChange={(e) => setFilter("search", e.target.value)}
              placeholder="Filter by keyword…"
              className="w-full rounded-md border border-slate-300 px-3 py-1.5 text-xs outline-none focus:border-brand-500 focus:ring-1 focus:ring-brand-500/20"
            />
          </label>
          <label>
            <span className="font-medium text-slate-600 block mb-1">Client</span>
            <select
              value={filters.clientId}
              onChange={(e) => setFilter("clientId", e.target.value)}
              className="w-full rounded-md border border-slate-300 px-2.5 py-1.5 text-xs bg-white outline-none focus:border-brand-500 focus:ring-1 focus:ring-brand-500/20"
            >
              <option value="">All Clients</option>
              {clients.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.code} · {c.legalName}
                </option>
              ))}
            </select>
          </label>
          <label>
            <span className="font-medium text-slate-600 block mb-1">Entity Type</span>
            <select
              value={filters.entityType}
              onChange={(e) => setFilter("entityType", e.target.value)}
              className="w-full rounded-md border border-slate-300 px-2.5 py-1.5 text-xs bg-white outline-none focus:border-brand-500 focus:ring-1 focus:ring-brand-500/20"
            >
              <option value="">All Entities</option>
              {opts?.entityTypes.map((t) => (
                <option key={t} value={t}>
                  {t}
                </option>
              ))}
            </select>
          </label>
          <label>
            <span className="font-medium text-slate-600 block mb-1">Action</span>
            <select
              value={filters.action}
              onChange={(e) => setFilter("action", e.target.value)}
              className="w-full rounded-md border border-slate-300 px-2.5 py-1.5 text-xs bg-white outline-none focus:border-brand-500 focus:ring-1 focus:ring-brand-500/20"
            >
              <option value="">All Actions</option>
              {opts?.actions.map((a) => (
                <option key={a.id} value={a.name}>
                  {a.name}
                </option>
              ))}
            </select>
          </label>
          <label>
            <span className="font-medium text-slate-600 block mb-1">User</span>
            <select
              value={filters.userName}
              onChange={(e) => setFilter("userName", e.target.value)}
              className="w-full rounded-md border border-slate-300 px-2.5 py-1.5 text-xs bg-white outline-none focus:border-brand-500 focus:ring-1 focus:ring-brand-500/20"
            >
              <option value="">All Users</option>
              {opts?.users.map((u) => (
                <option key={u.userName} value={u.userName}>
                  {u.userName}
                </option>
              ))}
            </select>
          </label>
          <label>
            <span className="font-medium text-slate-600 block mb-1">From Date</span>
            <input
              type="date"
              value={filters.fromUtc}
              onChange={(e) => setFilter("fromUtc", e.target.value)}
              className="w-full rounded-md border border-slate-300 px-2.5 py-1.5 text-xs outline-none focus:border-brand-500 focus:ring-1 focus:ring-brand-500/20"
            />
          </label>
          <label>
            <span className="font-medium text-slate-600 block mb-1">To Date</span>
            <input
              type="date"
              value={filters.toUtc}
              onChange={(e) => setFilter("toUtc", e.target.value)}
              className="w-full rounded-md border border-slate-300 px-2.5 py-1.5 text-xs outline-none focus:border-brand-500 focus:ring-1 focus:ring-brand-500/20"
            />
          </label>
        </div>
      </Card>

      {error && (
        <div className="rounded-lg border border-red-200 bg-red-50 p-3 text-xs text-red-700">
          {error}
        </div>
      )}

      {/* Audit Log Table */}
      <Card>
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-200 text-xs">
            <thead className="bg-slate-50 text-slate-600 font-semibold uppercase tracking-wider">
              <tr>
                <th className="px-4 py-3 text-left">When (UTC)</th>
                <th className="px-4 py-3 text-left">Action</th>
                <th className="px-4 py-3 text-left">Entity</th>
                <th className="px-4 py-3 text-left">Entity id</th>
                <th className="px-4 py-3 text-left">User</th>
                <th className="px-4 py-3 text-left">Context</th>
                <th className="px-4 py-3 text-right">Inspection</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 bg-white">
              {loading && (
                <tr>
                  <td colSpan={7} className="px-4 py-10 text-center">
                    <LoadingSpinner size="md" label="Loading audit trail…" />
                  </td>
                </tr>
              )}
              {!loading && rows.length === 0 && (
                <tr>
                  <td colSpan={7} className="px-4 py-10 text-center text-slate-500">
                    No audit records match the current filter criteria.
                  </td>
                </tr>
              )}
              {!loading &&
                rows.map((r) => {
                  const name = actionName(r.action);
                  const tone = actionTone[name] ?? "neutral";
                  return (
                    <tr key={r.id} className="hover:bg-slate-50/80 transition-colors">
                      <td className="px-4 py-2.5 font-mono text-slate-600 whitespace-nowrap">
                        {new Date(r.occurredAtUtc).toISOString().replace("T", " ").slice(0, 19)}
                      </td>
                      <td className="px-4 py-2.5">
                        <Badge tone={tone} size="sm">
                          {name}
                        </Badge>
                      </td>
                      <td className="px-4 py-2.5 font-medium text-slate-900">{r.entityType}</td>
                      <td className="px-4 py-2.5 font-mono text-slate-500 max-w-[120px] truncate" title={r.entityId}>
                        {r.entityId}
                      </td>
                      <td className="px-4 py-2.5 text-slate-700">{r.userName ?? "—"}</td>
                      <td className="px-4 py-2.5 text-slate-600 max-w-[160px] truncate" title={r.context ?? ""}>
                        {r.context ?? "—"}
                      </td>
                      <td className="px-4 py-2.5 text-right whitespace-nowrap">
                        <button
                          type="button"
                          onClick={() => setDetailRow(r)}
                          className="inline-flex items-center gap-1 text-xs font-semibold text-brand-600 hover:text-brand-700 transition"
                        >
                          Compare Diff
                        </button>
                      </td>
                    </tr>
                  );
                })}
            </tbody>
          </table>
        </div>

        {/* Table Footer with Record Count & Pagination */}
        <div className="flex flex-col sm:flex-row items-center justify-between gap-3 px-4 py-3 border-t border-slate-200 bg-slate-50/60 text-xs">
          <span className="text-slate-600 font-medium">
            Showing {rows.length > 0 ? filters.skip + 1 : 0}–{Math.min(filters.skip + rows.length, total)} of{" "}
            <span className="font-bold text-slate-900">{total.toLocaleString()}</span> records
          </span>
          <div className="flex items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              disabled={filters.skip === 0}
              onClick={() => setFilters((p) => ({ ...p, skip: Math.max(0, p.skip - p.take) }))}
            >
              Prev
            </Button>
            <Button
              variant="outline"
              size="sm"
              disabled={filters.skip + filters.take >= total}
              onClick={() => setFilters((p) => ({ ...p, skip: p.skip + p.take }))}
            >
              Next
            </Button>
          </div>
        </div>
      </Card>

      {/* Side-by-side Diff Modal */}
      {detailRow && (
        <div className="fixed inset-0 bg-slate-900/50 backdrop-blur-xs flex items-start justify-center p-4 sm:p-6 z-50 overflow-y-auto">
          <div className="w-full max-w-4xl rounded-2xl bg-white shadow-2xl border border-slate-200 my-8 overflow-hidden">
            <header className="flex items-start justify-between px-6 py-4 border-b border-slate-200 bg-slate-50">
              <div>
                <h3 className="text-base font-bold text-slate-900">
                  {detailRow.entityType} Diff Inspection
                </h3>
                <div className="flex items-center gap-2 mt-1 text-xs text-slate-500 font-mono flex-wrap">
                  <span>ID: {detailRow.entityId}</span>
                  <span>·</span>
                  <span>{new Date(detailRow.occurredAtUtc).toISOString()}</span>
                  <span>·</span>
                  <span className="text-slate-800 font-medium">{detailRow.userName ?? "—"}</span>
                  {detailRow.correlationId && (
                    <>
                      <span>·</span>
                      <span>corr: {detailRow.correlationId}</span>
                    </>
                  )}
                </div>
                {detailRow.context && (
                  <p className="text-xs text-slate-600 mt-1 italic">{detailRow.context}</p>
                )}
              </div>
              <button
                type="button"
                className="text-slate-400 hover:text-slate-700 text-2xl font-semibold leading-none p-1 transition"
                onClick={() => setDetailRow(null)}
              >
                ×
              </button>
            </header>

            <div className="p-6 max-h-[70vh] overflow-y-auto">
              {diffForRow(detailRow).length === 0 ? (
                <p className="text-xs text-slate-500 py-6 text-center">
                  No field-level diff recorded for this entity action.
                </p>
              ) : (
                <div className="overflow-x-auto rounded-lg border border-slate-200">
                  <table className="min-w-full text-xs divide-y divide-slate-200">
                    <thead className="bg-slate-50 text-slate-700 font-semibold uppercase tracking-wider">
                      <tr>
                        <th className="py-2.5 px-3 text-left w-48">Field</th>
                        <th className="py-2.5 px-3 text-left">Old Value (Previous)</th>
                        <th className="py-2.5 px-3 text-left">New Value (Current)</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-100 bg-white">
                      {diffForRow(detailRow).map((d) => (
                        <tr key={d.field} className={d.changed ? "bg-amber-50/70" : "hover:bg-slate-50/50"}>
                          <td className="py-2 px-3 font-mono font-semibold text-slate-800">
                            {d.field}
                            {d.changed && (
                              <span className="ml-1.5 text-amber-600 font-bold" title="Changed in this revision">
                                ●
                              </span>
                            )}
                          </td>
                          <td className="py-2 px-3 font-mono text-slate-700 whitespace-pre-wrap break-all bg-slate-50/40">
                            {d.oldValue ?? <span className="text-slate-400 italic">∅</span>}
                          </td>
                          <td className="py-2 px-3 font-mono text-slate-900 font-medium whitespace-pre-wrap break-all bg-amber-50/30">
                            {d.newValue ?? <span className="text-slate-400 italic">∅</span>}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>

            <footer className="px-6 py-3 border-t border-slate-200 bg-slate-50 flex justify-end">
              <Button variant="outline" size="sm" onClick={() => setDetailRow(null)}>
                Close Diff Inspection
              </Button>
            </footer>
          </div>
        </div>
      )}
    </div>
  );
}
