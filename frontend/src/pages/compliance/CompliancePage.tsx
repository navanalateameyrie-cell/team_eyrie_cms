import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { apiFetch, apiRawFetch } from "../../api/client";
import { useAuth } from "../../auth/AuthContext";
import {
  PageHeader,
  MetricCard,
  Card,
  Button,
  Badge,
  Input,
  Select,
  LoadingSpinner,
  EmptyState,
} from "../../components/ui";

type ComplianceStatus = "Pending" | "InProgress" | "Filed" | "Delayed" | "Waived";

interface Obligation {
  id: string;
  serviceFileId: string;
  fileNumber: string;
  clientId: string;
  clientCode: string;
  clientName: string;
  serviceDefinitionId: string;
  serviceCode: string;
  serviceName: string;
  periodKey: string;
  periodStart: string;
  periodEnd: string;
  dueDate: string;
  status: ComplianceStatus | number;
  assignedStaffId: string | null;
  assignedStaffName: string | null;
  filedOn: string | null;
  reference: string | null;
  notes: string | null;
  version: number;
}

interface Page<T> {
  items: T[];
  total: number;
  skip: number;
  take: number;
}

interface Aggregates {
  dueThisWeek: number;
  overdueOpen: number;
  filedThisMonth: number;
  inProgress: number;
}

const statusConfig: Record<number, { label: string; tone: "neutral" | "warning" | "success" | "danger" }> = {
  1: { label: "Pending", tone: "neutral" },
  2: { label: "In Progress", tone: "warning" },
  3: { label: "Filed", tone: "success" },
  4: { label: "Delayed", tone: "danger" },
  5: { label: "Waived", tone: "neutral" },
};

function statusNum(s: ComplianceStatus | number): number {
  if (typeof s === "number") return s;
  return { Pending: 1, InProgress: 2, Filed: 3, Delayed: 4, Waived: 5 }[s] ?? 1;
}

export default function CompliancePage() {
  const { hasPermission } = useAuth();
  const canWrite = hasPermission("compliance.write");
  const [from, setFrom] = useState<string>(todayMinus(30));
  const [to, setTo] = useState<string>(todayPlus(60));
  const [status, setStatus] = useState<string>("");
  const [serviceCode, setServiceCode] = useState<string>("");
  const [search, setSearch] = useState<string>("");
  const [data, setData] = useState<Page<Obligation> | null>(null);
  const [aggregates, setAggregates] = useState<Aggregates | null>(null);
  const [loading, setLoading] = useState(false);
  const [generating, setGenerating] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [skip, setSkip] = useState(0);
  const [refreshKey, setRefreshKey] = useState(0);
  const take = 50;

  useEffect(() => {
    setLoading(true);
    setError(null);
    const params = new URLSearchParams();
    if (from) params.set("from", from);
    if (to) params.set("to", to);
    if (status) params.set("status", status);
    if (serviceCode) params.set("serviceCode", serviceCode);
    params.set("skip", String(skip));
    params.set("take", String(take));
    apiFetch<Page<Obligation>>(`/api/v1/compliance?${params}`)
      .then(setData)
      .catch((e) => setError(String(e)))
      .finally(() => setLoading(false));
  }, [from, to, status, serviceCode, skip, refreshKey]);

  useEffect(() => {
    apiFetch<Aggregates>("/api/v1/compliance/aggregates")
      .then(setAggregates)
      .catch(() => void 0);
  }, [refreshKey]);

  const filtered = useMemo(() => {
    if (!data) return [];
    const q = search.trim().toLowerCase();
    if (!q) return data.items;
    return data.items.filter(
      (o) =>
        o.clientName.toLowerCase().includes(q) ||
        o.clientCode.toLowerCase().includes(q) ||
        o.fileNumber.toLowerCase().includes(q) ||
        o.serviceCode.toLowerCase().includes(q) ||
        o.periodKey.toLowerCase().includes(q)
    );
  }, [data, search]);

  const generate = async () => {
    if (!canWrite) return;
    setGenerating(true);
    try {
      const r = await apiFetch<{ obligationsCreated: number }>("/api/v1/compliance/generate", {
        method: "POST",
        body: JSON.stringify({ horizonMonths: 6 }),
      });
      alert(`Generated ${r.obligationsCreated} new compliance obligations.`);
      setRefreshKey((k) => k + 1);
    } catch (e) {
      alert(`Generation failed: ${String(e)}`);
    } finally {
      setGenerating(false);
    }
  };

  return (
    <div className="space-y-6">
      <PageHeader
        breadcrumbs={[
          { label: "Compliance", to: "/admin/compliance" },
          { label: "Obligations Calendar" },
        ]}
        title="Statutory Compliance & Obligations"
        subtitle="Track recurring statutory deadlines across GST returns, TDS forms, Income Tax returns, and audits."
        actions={
          canWrite ? (
            <Button
              variant="primary"
              size="sm"
              loading={generating}
              onClick={generate}
            >
              {generating ? "Generating…" : "Generate Forward (6 Months)"}
            </Button>
          ) : undefined
        }
      />

      {aggregates && (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          <MetricCard
            label="Due This Week"
            value={aggregates.dueThisWeek}
            tone="warning"
            hint="Imminent statutory deadlines"
          />
          <MetricCard
            label="Overdue Filings"
            value={aggregates.overdueOpen}
            tone="danger"
            hint="Immediate penalty exposure"
          />
          <MetricCard
            label="In Execution"
            value={aggregates.inProgress}
            tone="brand"
            hint="Work in progress by staff"
          />
          <MetricCard
            label="Filed This Month"
            value={aggregates.filedThisMonth}
            tone="success"
            hint="Completed statutory returns"
          />
        </div>
      )}

      <Card>
        <div className="px-5 py-3.5 border-b border-slate-100 flex flex-wrap items-center gap-3">
          <div className="flex items-center gap-2 text-xs">
            <span className="font-semibold text-slate-600">From:</span>
            <Input
              type="date"
              value={from}
              onChange={(e) => {
                setFrom(e.target.value);
                setSkip(0);
              }}
              className="py-1 text-xs w-auto"
            />
          </div>

          <div className="flex items-center gap-2 text-xs">
            <span className="font-semibold text-slate-600">To:</span>
            <Input
              type="date"
              value={to}
              onChange={(e) => {
                setTo(e.target.value);
                setSkip(0);
              }}
              className="py-1 text-xs w-auto"
            />
          </div>

          <div className="flex items-center gap-2 text-xs">
            <span className="font-semibold text-slate-600">Status:</span>
            <Select
              value={status}
              onChange={(e) => {
                setStatus(e.target.value);
                setSkip(0);
              }}
              className="py-1 text-xs w-auto"
            >
              <option value="">All Statuses</option>
              <option value="Pending">Pending</option>
              <option value="InProgress">In Progress</option>
              <option value="Delayed">Delayed</option>
              <option value="Filed">Filed</option>
              <option value="Waived">Waived</option>
            </Select>
          </div>

          <div className="flex items-center gap-2 text-xs">
            <span className="font-semibold text-slate-600">Service:</span>
            <Input
              value={serviceCode}
              onChange={(e) => {
                setServiceCode(e.target.value);
                setSkip(0);
              }}
              placeholder="e.g. GST, TDS"
              className="py-1 text-xs w-28 uppercase"
            />
          </div>

          <div className="flex-1 min-w-[180px]">
            <Input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Filter by client, file, or period…"
              className="py-1 text-xs"
            />
          </div>

          <div className="text-xs text-slate-500 font-medium ml-auto">
            {data ? (
              <span>
                Showing <strong className="text-slate-800">{filtered.length}</strong> of{" "}
                <strong className="text-slate-800">{data.total}</strong>
              </span>
            ) : loading ? (
              "Loading…"
            ) : null}
          </div>
        </div>

        {error && (
          <div className="px-5 py-3 text-xs text-red-700 bg-red-50 border-b border-red-100 flex items-center gap-2">
            <svg className="w-4 h-4 text-red-600 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            <span>{error}</span>
          </div>
        )}

        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-200 text-xs">
            <thead className="bg-slate-50 font-medium text-slate-500">
              <tr>
                <th className="text-left px-4 py-3">Statutory Due</th>
                <th className="text-left px-4 py-3">Client Entity</th>
                <th className="text-left px-4 py-3">Practice Service</th>
                <th className="text-left px-4 py-3">Period</th>
                <th className="text-left px-4 py-3">Status</th>
                <th className="text-left px-4 py-3">Assignee</th>
                <th className="text-left px-4 py-3">Filing Evidence</th>
                <th className="text-right px-4 py-3">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 bg-white">
              {loading && filtered.length === 0 ? (
                <tr>
                  <td colSpan={8} className="py-12 text-center">
                    <LoadingSpinner text="Loading compliance obligations…" />
                  </td>
                </tr>
              ) : filtered.length === 0 ? (
                <tr>
                  <td colSpan={8} className="py-12">
                    <EmptyState
                      title="No obligations found in date window"
                      description="Try widening the filter range or click Generate Forward to create statutory obligations."
                      action={
                        canWrite ? (
                          <Button variant="primary" size="sm" onClick={generate}>
                            Generate Forward 6 Months
                          </Button>
                        ) : undefined
                      }
                    />
                  </td>
                </tr>
              ) : (
                filtered.map((o) => {
                  const s = statusNum(o.status);
                  const cfg = statusConfig[s] ?? { label: "Unknown", tone: "neutral" };
                  return (
                    <tr key={o.id} className="hover:bg-slate-50/60 transition-colors">
                      <td className="px-4 py-3 whitespace-nowrap">
                        <DueCell due={o.dueDate} status={s} />
                      </td>

                      <td className="px-4 py-3">
                        <Link
                          to={`/admin/clients/${o.clientId}`}
                          className="font-medium text-slate-900 hover:text-brand-600 transition-colors block"
                        >
                          {o.clientName}
                        </Link>
                        <div className="text-[11px] text-slate-500 font-mono mt-0.5">
                          {o.clientCode} · File {o.fileNumber}
                        </div>
                      </td>

                      <td className="px-4 py-3">
                        <div className="font-semibold text-slate-800">{o.serviceCode}</div>
                        <div className="text-[11px] text-slate-500 truncate max-w-xs">
                          {o.serviceName}
                        </div>
                      </td>

                      <td className="px-4 py-3 font-mono text-slate-800 font-semibold whitespace-nowrap">
                        {o.periodKey}
                      </td>

                      <td className="px-4 py-3 whitespace-nowrap">
                        <Badge tone={cfg.tone} size="sm">
                          {cfg.label}
                        </Badge>
                      </td>

                      <td className="px-4 py-3 text-slate-700 whitespace-nowrap">
                        {o.assignedStaffName ?? <span className="text-slate-400 italic">Unassigned</span>}
                      </td>

                      <td className="px-4 py-3 whitespace-nowrap">
                        {o.filedOn ? (
                          <div>
                            <span className="font-mono font-medium text-slate-800">{o.filedOn}</span>
                            {o.reference && (
                              <div className="text-[10px] text-slate-500 font-mono bg-slate-100 px-1.5 py-0.2 rounded mt-0.5 inline-block">
                                {o.reference}
                              </div>
                            )}
                          </div>
                        ) : (
                          <span className="text-slate-400 italic">Pending</span>
                        )}
                      </td>

                      <td className="px-4 py-3 text-right whitespace-nowrap">
                        {canWrite && s !== 3 && s !== 5 && (
                          <ActionButtons
                            obligation={o}
                            onDone={() => setRefreshKey((k) => k + 1)}
                          />
                        )}
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>

        {data && data.total > take && (
          <div className="px-5 py-3 border-t border-slate-100 flex items-center justify-between text-xs text-slate-500">
            <span>
              Showing {skip + 1}–{Math.min(skip + take, data.total)} of {data.total}
            </span>
            <div className="flex items-center gap-2">
              <Button
                variant="outline"
                size="sm"
                disabled={skip === 0}
                onClick={() => setSkip(Math.max(0, skip - take))}
              >
                Previous
              </Button>
              <Button
                variant="outline"
                size="sm"
                disabled={skip + take >= data.total}
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

function DueCell({ due, status }: { due: string; status: number }) {
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  const d = new Date(due + "T00:00:00");
  const diff = Math.round((d.getTime() - today.getTime()) / 86_400_000);
  const open = status !== 3 && status !== 5;
  const isOverdue = open && diff < 0;
  const isImminent = open && diff >= 0 && diff <= 7;

  return (
    <div>
      <div
        className={`font-mono font-semibold ${isOverdue
          ? "text-rose-600"
          : isImminent
            ? "text-amber-700"
            : "text-slate-800"
          }`}
      >
        {due}
      </div>
      {open && (
        <div className="text-[10px] font-medium mt-0.5">
          {diff === 0 ? (
            <span className="text-amber-700 font-bold">Due Today</span>
          ) : diff > 0 ? (
            <span className={diff <= 3 ? "text-amber-600" : "text-slate-400"}>in {diff} days</span>
          ) : (
            <span className="text-rose-600 font-bold">{Math.abs(diff)}d overdue</span>
          )}
        </div>
      )}
    </div>
  );
}

async function openLetter(obligationId: string) {
  const templates = await apiFetch<Array<{ id: string; name: string }>>(
    "/api/v1/letter-templates?activeOnly=true"
  );
  if (templates.length === 0) {
    alert("No active letter templates. Create one under Admin → Letter Templates.");
    return;
  }
  const list = templates.map((t, i) => `${i + 1}. ${t.name}`).join("\n");
  const pick = window.prompt(`Select letter template:\n${list}`, "1");
  const idx = Number(pick) - 1;
  if (!(idx >= 0 && idx < templates.length)) return;
  const template = templates[idx];
  const r = await apiRawFetch(`/api/v1/compliance/${obligationId}/letter?templateId=${template.id}`);
  if (!r.ok) {
    alert(`Letter generation failed (${r.status})`);
    return;
  }
  const blob = await r.blob();
  const url = URL.createObjectURL(blob);
  window.open(url, "_blank", "noopener");
  setTimeout(() => URL.revokeObjectURL(url), 60_000);
}

function ActionButtons({ obligation, onDone }: { obligation: Obligation; onDone: () => void }) {
  async function markInProgress() {
    try {
      await apiFetch(`/api/v1/compliance/${obligation.id}/status`, {
        method: "POST",
        body: JSON.stringify({ status: "InProgress" }),
      });
      onDone();
    } catch (e) {
      alert(String(e));
    }
  }

  async function markFiled() {
    const filedOn = window.prompt(
      "Statutory Filing Date (YYYY-MM-DD):",
      new Date().toISOString().slice(0, 10)
    );
    if (!filedOn) return;
    const reference = window.prompt("Filing ARN / Acknowledgment No. (optional):", "") || null;
    try {
      await apiFetch(`/api/v1/compliance/${obligation.id}/file`, {
        method: "POST",
        body: JSON.stringify({ filedOn, reference }),
      });
      onDone();
    } catch (e) {
      alert(String(e));
    }
  }

  const s = statusNum(obligation.status);

  return (
    <div className="flex items-center justify-end gap-1.5">
      {s === 1 && (
        <button
          type="button"
          onClick={markInProgress}
          className="rounded px-2 py-1 text-[11px] font-medium text-sky-800 bg-sky-50 border border-sky-200 hover:bg-sky-100 transition-colors"
        >
          Start
        </button>
      )}
      <button
        type="button"
        onClick={markFiled}
        className="rounded px-2 py-1 text-[11px] font-medium text-emerald-800 bg-emerald-50 border border-emerald-200 hover:bg-emerald-100 transition-colors"
      >
        File
      </button>
      <button
        type="button"
        onClick={() => openLetter(obligation.id)}
        className="rounded px-2 py-1 text-[11px] font-medium text-slate-700 bg-white border border-slate-200 hover:bg-slate-50 transition-colors"
      >
        Letter
      </button>
    </div>
  );
}

function todayMinus(days: number) {
  const d = new Date();
  d.setDate(d.getDate() - days);
  return d.toISOString().slice(0, 10);
}

function todayPlus(days: number) {
  const d = new Date();
  d.setDate(d.getDate() + days);
  return d.toISOString().slice(0, 10);
}
