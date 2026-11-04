import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../../auth/AuthContext";
import { workloadApi, type StaffWorkloadDetail, type StaffWorkloadSummary } from "../../api/workload";
import {
  PageHeader,
  Card,
  CardHeader,
  CardContent,
  Button,
  Badge,
  Input,
  LoadingSpinner,
} from "../../components/ui";

function today() {
  return new Date().toISOString().slice(0, 10);
}

export default function StaffWorkloadPage() {
  const { hasPermission } = useAuth();
  const canReadMasters = hasPermission("masters.read");
  const [asOf, setAsOf] = useState(today());
  const [summaries, setSummaries] = useState<StaffWorkloadSummary[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [openStaffId, setOpenStaffId] = useState<string | null>(null);
  const [detail, setDetail] = useState<StaffWorkloadDetail | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setSummaries(await workloadApi.all(asOf));
    } catch (ex: unknown) {
      setError(ex instanceof Error ? ex.message : "Failed to load staff workload metrics");
    } finally {
      setLoading(false);
    }
  }, [asOf]);

  useEffect(() => {
    void load();
  }, [load]);

  async function openDetail(staffId: string) {
    setOpenStaffId(staffId);
    setDetail(null);
    try {
      setDetail(await workloadApi.forStaff(staffId, { asOf }));
    } catch (ex: unknown) {
      setError(ex instanceof Error ? ex.message : "Failed to load assignment breakdown");
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        breadcrumbs={[
          { label: "Operations" },
          { label: "Staff Workload" },
        ]}
        title="Practice Staff Capacity & Workload Allocation"
        subtitle="Active client in-charge and engagement assignments per partner, manager, and article assistant."
        actions={
          <div className="flex flex-wrap items-center gap-3 text-xs">
            {canReadMasters && (
              <Link to="/admin/masters/staff">
                <Button variant="outline" size="sm">
                  Staff Directory & Roles →
                </Button>
              </Link>
            )}
            <div className="flex items-center gap-2">
              <span className="font-semibold text-slate-600">Effective Date:</span>
              <Input
                type="date"
                value={asOf}
                onChange={(e) => setAsOf(e.target.value)}
                className="py-1 text-xs w-auto font-mono"
              />
            </div>
          </div>
        }
      />

      {error && (
        <div className="rounded-lg bg-red-50 border border-red-200 p-3 text-xs text-red-700">
          {error}
        </div>
      )}

      <Card>
        <CardHeader
          title="Team Allocation Matrix"
          subtitle="Click any staff row to inspect specific client entities and service files."
        />
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-200 text-xs">
            <thead className="bg-slate-50 font-medium text-slate-500">
              <tr>
                <th className="px-4 py-3 text-left w-24">Staff Code</th>
                <th className="px-4 py-3 text-left">Team Member</th>
                <th className="px-3 py-3 text-right" title="Client-level Primary">Client Lead</th>
                <th className="px-3 py-3 text-right" title="Client-level Secondary">Client Sec</th>
                <th className="px-3 py-3 text-right" title="Client-level Reviewer">Client Rev</th>
                <th className="px-3 py-3 text-right" title="File-level Primary">File Lead</th>
                <th className="px-3 py-3 text-right" title="File-level Secondary">File Sec</th>
                <th className="px-3 py-3 text-right" title="File-level Reviewer">File Rev</th>
                <th className="px-4 py-3 text-right font-bold text-slate-800">Total Clients</th>
                <th className="px-4 py-3 text-right font-bold text-slate-800">Total Files</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 bg-white">
              {loading && summaries.length === 0 ? (
                <tr>
                  <td colSpan={10} className="px-4 py-12 text-center">
                    <LoadingSpinner text="Computing workload allocations…" />
                  </td>
                </tr>
              ) : summaries.length === 0 ? (
                <tr>
                  <td colSpan={10} className="px-4 py-12 text-center text-slate-400 italic">
                    No active staff assignments found on {asOf}.
                  </td>
                </tr>
              ) : (
                summaries.map((s) => {
                  const isSelected = openStaffId === s.staffId;
                  return (
                    <tr
                      key={s.staffId}
                      onClick={() => openDetail(s.staffId)}
                      className={`cursor-pointer transition-colors ${isSelected
                        ? "bg-brand-50/70"
                        : "hover:bg-slate-50"
                        } ${s.staffIsActive ? "" : "text-slate-400 opacity-60"}`}
                    >
                      <td className="px-4 py-3 font-mono font-semibold text-slate-800">
                        {s.staffCode}
                      </td>
                      <td className="px-4 py-3 font-medium text-slate-900">
                        {s.staffFullName}
                        {!s.staffIsActive && (
                          <span className="ml-2 text-[10px] text-slate-400 font-normal">(Inactive)</span>
                        )}
                      </td>
                      <td className="px-3 py-3 text-right font-mono">{s.clientPrimaryCount}</td>
                      <td className="px-3 py-3 text-right font-mono">{s.clientSecondaryCount}</td>
                      <td className="px-3 py-3 text-right font-mono">{s.clientReviewerCount}</td>
                      <td className="px-3 py-3 text-right font-mono">{s.filePrimaryCount}</td>
                      <td className="px-3 py-3 text-right font-mono">{s.fileSecondaryCount}</td>
                      <td className="px-3 py-3 text-right font-mono">{s.fileReviewerCount}</td>
                      <td className="px-4 py-3 text-right font-mono font-bold text-brand-900">
                        {s.totalClients}
                      </td>
                      <td className="px-4 py-3 text-right font-mono font-bold text-brand-900">
                        {s.totalFiles}
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </Card>

      {/* Staff Detail Breakdown Panel */}
      {openStaffId && (
        <Card className="animate-fadeIn">
          <CardHeader
            title={detail ? `${detail.staffFullName} (${detail.staffCode})` : "Loading Detail…"}
            subtitle="Breakdown of assigned client entities and individual service deliverables"
            action={
              <Button
                variant="outline"
                size="sm"
                onClick={() => {
                  setOpenStaffId(null);
                  setDetail(null);
                }}
              >
                Close Panel
              </Button>
            }
          />
          <CardContent>
            {detail ? (
              <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                <div>
                  <h4 className="text-xs font-semibold uppercase tracking-wider text-slate-700 mb-2">
                    Client-Level Assignments ({detail.clients.length})
                  </h4>
                  {detail.clients.length === 0 ? (
                    <p className="text-xs text-slate-400 italic">No direct client assignments.</p>
                  ) : (
                    <div className="divide-y divide-slate-100 rounded-lg border border-slate-200">
                      {detail.clients.map((r) => (
                        <div key={r.assignmentId} className="p-3 flex items-center justify-between text-xs">
                          <div>
                            <Link
                              to={`/admin/clients/${r.scopeId}`}
                              className="font-medium text-slate-900 hover:text-brand-600 transition-colors"
                            >
                              <span className="font-mono text-slate-500 mr-2">{r.scopeCode}</span>
                              {r.scopeLabel}
                            </Link>
                            <div className="text-[11px] text-slate-400 font-mono mt-0.5">
                              {r.effectiveFrom} → {r.effectiveTo ?? "Present"}
                            </div>
                          </div>
                          <Badge variant="primary" size="sm">
                            {r.role}
                          </Badge>
                        </div>
                      ))}
                    </div>
                  )}
                </div>

                <div>
                  <h4 className="text-xs font-semibold uppercase tracking-wider text-slate-700 mb-2">
                    Service File Assignments ({detail.serviceFiles.length})
                  </h4>
                  {detail.serviceFiles.length === 0 ? (
                    <p className="text-xs text-slate-400 italic">No specific service file assignments.</p>
                  ) : (
                    <div className="divide-y divide-slate-100 rounded-lg border border-slate-200 max-h-96 overflow-y-auto">
                      {detail.serviceFiles.map((r) => (
                        <div key={r.assignmentId} className="p-3 flex items-center justify-between text-xs">
                          <div>
                            <div className="font-medium text-slate-900">
                              <span className="font-mono text-slate-500 mr-2">{r.scopeCode}</span>
                              {r.scopeLabel}
                            </div>
                            <div className="text-[11px] text-slate-400 font-mono mt-0.5">
                              {r.effectiveFrom} → {r.effectiveTo ?? "Present"}
                            </div>
                          </div>
                          <Badge variant="primary" size="sm">
                            {r.role}
                          </Badge>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              </div>
            ) : (
              <div className="py-6 flex justify-center">
                <LoadingSpinner text="Fetching staff assignments…" />
              </div>
            )}
          </CardContent>
        </Card>
      )}
    </div>
  );
}
