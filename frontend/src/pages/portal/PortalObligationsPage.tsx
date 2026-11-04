import { useEffect, useState, useMemo } from "react";
import { apiFetch } from "../../api/client";
import { Card, PageHeader, MetricCard, Badge, EmptyState, LoadingSpinner } from "../../components/ui";

type Status = number | string;

interface O {
  id: string;
  periodKey: string;
  periodStart: string;
  periodEnd: string;
  dueDate: string;
  status: Status;
  filedOn: string | null;
  serviceCode: string;
}

const STATUS_CONFIG: Record<number, { label: string; tone: "neutral" | "warning" | "success" | "danger" }> = {
  1: { label: "Pending", tone: "neutral" },
  2: { label: "In Progress", tone: "warning" },
  3: { label: "Filed", tone: "success" },
  4: { label: "Delayed", tone: "danger" },
  5: { label: "Waived", tone: "neutral" },
};

const num = (s: Status): number =>
  typeof s === "number"
    ? s
    : ({ Pending: 1, InProgress: 2, Filed: 3, Delayed: 4, Waived: 5 } as Record<string, number>)[s] ?? 0;

export default function PortalObligationsPage() {
  const [rows, setRows] = useState<O[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    apiFetch<O[]>("/api/v1/portal/obligations")
      .then(setRows)
      .catch((e) => setError(String(e)));
  }, []);

  const counts = useMemo(() => {
    if (!rows) return { total: 0, filed: 0, inProgress: 0, pending: 0, delayed: 0 };
    return {
      total: rows.length,
      filed: rows.filter((r) => num(r.status) === 3).length,
      inProgress: rows.filter((r) => num(r.status) === 2).length,
      pending: rows.filter((r) => num(r.status) === 1).length,
      delayed: rows.filter((r) => num(r.status) === 4).length,
    };
  }, [rows]);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Compliance Obligations"
        subtitle="Track GST, TDS, Advance Tax, and statutory returns filed by your Chartered Accountants on your behalf."
      />

      {error && (
        <div className="rounded-lg border border-red-200 bg-red-50 text-sm text-red-700 p-4">
          <p className="font-semibold">Unable to load compliance obligations</p>
          <p className="text-xs mt-0.5">{error}</p>
        </div>
      )}

      {/* Compliance Status Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <MetricCard
          label="Total Obligations"
          value={counts.total}
          hint="Tracked compliance items"
          tone="default"
        />
        <MetricCard
          label="Filed & Completed"
          value={counts.filed}
          hint="Acknowledged by tax portal"
          tone="success"
        />
        <MetricCard
          label="In Progress / Pending"
          value={counts.inProgress + counts.pending}
          hint="Under preparation by practice"
          tone="warning"
        />
        <MetricCard
          label="Delayed / Overdue"
          value={counts.delayed}
          hint={counts.delayed > 0 ? "Requires urgent attention" : "All filings on schedule"}
          tone={counts.delayed > 0 ? "danger" : "default"}
        />
      </div>

      {/* Compliance Data Table */}
      <Card>
        {rows === null ? (
          <div className="py-16">
            <LoadingSpinner size="md" label="Loading compliance obligations…" />
          </div>
        ) : rows.length === 0 ? (
          <EmptyState
            title="No obligations recorded"
            description="Your account has no active statutory or tax filing obligations assigned at this time."
          />
        ) : (
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-slate-200 text-sm">
              <thead className="bg-slate-50 text-slate-600 text-xs uppercase tracking-wider font-semibold">
                <tr>
                  <th className="text-left px-4 py-3">Service Code</th>
                  <th className="text-left px-4 py-3">Filing Period</th>
                  <th className="text-left px-4 py-3">Statutory Due Date</th>
                  <th className="text-center px-4 py-3">Filing Status</th>
                  <th className="text-left px-4 py-3">Date Filed</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 bg-white">
                {rows.map((o) => {
                  const statusNum = num(o.status);
                  const cfg = STATUS_CONFIG[statusNum] ?? { label: "Pending", tone: "neutral" };
                  return (
                    <tr key={o.id} className="hover:bg-slate-50/80 transition-colors">
                      <td className="px-4 py-3">
                        <span className="font-mono text-xs font-semibold px-2 py-0.5 rounded bg-slate-100 text-slate-800 border border-slate-200">
                          {o.serviceCode}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-xs font-medium text-slate-900">{o.periodKey}</td>
                      <td className="px-4 py-3 text-xs text-slate-600">
                        <span className="font-mono">{o.dueDate}</span>
                      </td>
                      <td className="px-4 py-3 text-center">
                        <Badge tone={cfg.tone} size="sm">
                          {cfg.label}
                        </Badge>
                      </td>
                      <td className="px-4 py-3 text-xs text-slate-600">
                        {o.filedOn ? (
                          <span className="text-emerald-700 font-medium">{o.filedOn}</span>
                        ) : (
                          <span className="text-slate-400">—</span>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </Card>
    </div>
  );
}
