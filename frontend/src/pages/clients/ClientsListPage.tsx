import { useCallback, useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "../../auth/AuthContext";
import { clientsApi, type ClientSummaryDto } from "../../api/clients";
import { exportsApi } from "../../api/exports";
import { PageHeader, Card, Button, StatusBadge, EmptyState, SkeletonRow, ErrorState } from "../../components/ui";

export default function ClientsListPage() {
  const { hasPermission } = useAuth();
  const canWrite = hasPermission("clients.write");
  const canExport = hasPermission("exports.perform");
  const canImport = hasPermission("imports.read");
  const nav = useNavigate();

  const [rows, setRows] = useState<ClientSummaryDto[]>([]);
  const [total, setTotal] = useState(0);
  const [search, setSearch] = useState("");
  const [active, setActive] = useState<boolean | undefined>(true);
  const [skip, setSkip] = useState(0);
  const take = 25;
  const [loading, setLoading] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const r = await clientsApi.list({ search: search || undefined, active, skip, take });
      setRows(r.items);
      setTotal(r.total);
    } catch (ex: unknown) {
      setError(ex instanceof Error ? ex.message : "Failed to load clients.");
    } finally {
      setLoading(false);
    }
  }, [search, active, skip]);

  useEffect(() => {
    void load();
  }, [load]);

  async function handleExport() {
    setExporting(true);
    try {
      await exportsApi.clients({ search: search || undefined, active });
    } catch (ex: unknown) {
      alert(ex instanceof Error ? ex.message : "Export failed");
    } finally {
      setExporting(false);
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Clients Directory"
        description="Master directory of client accounts, statutory tax identities (PAN/GSTIN), and branch locations."
        breadcrumbs={[{ label: "Overview", to: "/dashboard" }, { label: "Clients" }]}
        badge={
          <span className="rounded-full bg-slate-100 px-2.5 py-0.5 text-xs font-semibold text-slate-700">
            {total} {total === 1 ? "Client" : "Clients"}
          </span>
        }
        actions={
          <div className="flex items-center gap-2.5">
            {canImport && (
              <Link to="/admin/imports">
                <Button
                  variant="outline"
                  size="sm"
                  icon={
                    <svg className="w-4 h-4 text-slate-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" />
                    </svg>
                  }
                >
                  Import Excel
                </Button>
              </Link>
            )}
            {canExport && (
              <Button
                variant="outline"
                size="sm"
                busy={exporting}
                busyText="Exporting…"
                onClick={handleExport}
                icon={
                  <svg className="w-4 h-4 text-slate-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 10v6m0 0l-3-3m3 3l3-3m2 8H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                  </svg>
                }
              >
                Export Excel
              </Button>
            )}
            {canWrite && (
              <Link to="/admin/clients/new">
                <Button variant="primary" size="sm">
                  + Add Client
                </Button>
              </Link>
            )}
          </div>
        }
      />

      <Card>
        {/* Filter and Search Bar */}
        <div className="p-4 border-b border-slate-100 flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-3 bg-slate-50/50 rounded-t-xl">
          <div className="relative flex-1 max-w-md">
            <div className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3 text-slate-400">
              <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
              </svg>
            </div>
            <input
              type="search"
              value={search}
              onChange={(e) => {
                setSearch(e.target.value);
                setSkip(0);
              }}
              placeholder="Search by code, legal name, PAN, legacy ID…"
              className="w-full rounded-lg border border-slate-300 bg-white pl-9 pr-3 py-1.5 text-xs text-slate-900 placeholder:text-slate-400 focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500 transition-colors"
            />
          </div>

          <div className="flex items-center gap-2 shrink-0">
            <span className="text-xs text-slate-500 font-medium">Status:</span>
            <select
              value={active === undefined ? "" : active ? "active" : "inactive"}
              onChange={(e) => {
                const v = e.target.value;
                setActive(v === "" ? undefined : v === "active");
                setSkip(0);
              }}
              className="rounded-lg border border-slate-300 bg-white text-xs px-2.5 py-1.5 text-slate-700 focus:border-brand-500 focus:outline-none"
            >
              <option value="active">Active Only</option>
              <option value="inactive">Inactive Only</option>
              <option value="">All Records</option>
            </select>
          </div>
        </div>

        {error && (
          <div className="p-4">
            <ErrorState message={error} onRetry={load} />
          </div>
        )}

        {/* Data Table */}
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-100 text-xs">
            <thead className="bg-slate-50/80 text-slate-600 font-semibold uppercase tracking-wider text-[10px]">
              <tr>
                <th className="px-5 py-3 text-left w-28">Client Code</th>
                <th className="px-5 py-3 text-left">Legal & Trade Name</th>
                <th className="px-5 py-3 text-left w-36">PAN</th>
                <th className="px-5 py-3 text-left w-28">Status</th>
                <th className="px-5 py-3 text-right w-16" />
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 bg-white">
              {loading && Array.from({ length: 5 }).map((_, i) => <SkeletonRow key={i} cols={5} />)}

              {!loading && rows.length === 0 && (
                <tr>
                  <td colSpan={5} className="p-8">
                    <EmptyState
                      title="No clients found"
                      description={search ? `No records match "${search}". Try adjusting your filters.` : "Get started by onboarding your practice's first client."}
                      actionLabel={canWrite && !search ? "+ Onboard Client" : undefined}
                      onAction={canWrite && !search ? () => nav("/admin/clients/new") : undefined}
                    />
                  </td>
                </tr>
              )}

              {!loading &&
                rows.map((r) => (
                  <tr
                    key={r.id}
                    onClick={() => nav(`/admin/clients/${r.id}`)}
                    className="cursor-pointer hover:bg-slate-50/80 transition-colors group"
                  >
                    <td className="px-5 py-3.5 whitespace-nowrap">
                      <span className="font-mono font-bold text-slate-900 bg-slate-100 px-2 py-0.5 rounded border border-slate-200">
                        {r.code}
                      </span>
                    </td>
                    <td className="px-5 py-3.5">
                      <div className="font-semibold text-slate-900 group-hover:text-brand-600 transition-colors">
                        {r.legalName}
                      </div>
                      {r.tradeName && <div className="text-[11px] text-slate-500 mt-0.5">{r.tradeName}</div>}
                    </td>
                    <td className="px-5 py-3.5 whitespace-nowrap font-mono text-slate-700 font-medium">
                      {r.pan ? (
                        <span className="bg-slate-50 px-1.5 py-0.5 rounded text-[11px] border border-slate-200">
                          {r.pan}
                        </span>
                      ) : (
                        <span className="text-slate-400">—</span>
                      )}
                    </td>
                    <td className="px-5 py-3.5 whitespace-nowrap">
                      <StatusBadge status={r.isActive ? "Active" : "Inactive"} />
                    </td>
                    <td className="px-5 py-3.5 text-right whitespace-nowrap text-slate-400 group-hover:text-brand-500 font-semibold">
                      →
                    </td>
                  </tr>
                ))}
            </tbody>
          </table>
        </div>

        {/* Pagination Footer */}
        {total > 0 && (
          <div className="flex items-center justify-between px-5 py-3.5 border-t border-slate-100 bg-slate-50/50 rounded-b-xl text-xs text-slate-600">
            <div>
              Showing <span className="font-semibold text-slate-900">{skip + 1}</span> to{" "}
              <span className="font-semibold text-slate-900">{Math.min(skip + take, total)}</span> of{" "}
              <span className="font-semibold text-slate-900">{total}</span> records
            </div>
            <div className="flex items-center gap-1.5">
              <Button
                variant="outline"
                size="xs"
                disabled={skip === 0}
                onClick={() => setSkip(Math.max(0, skip - take))}
              >
                Previous
              </Button>
              <Button
                variant="outline"
                size="xs"
                disabled={skip + take >= total}
                onClick={() => setSkip(skip + take)}
              >
                Next
              </Button>
            </div>
          </div>
        )}
      </Card>
    </div>
  );
}
