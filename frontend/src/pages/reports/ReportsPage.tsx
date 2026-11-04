import { useEffect, useMemo, useState } from "react";
import { apiFetch } from "../../api/client";
import { formatInr, formatInrCompact } from "../../utils/formatters";
import {
  PageHeader,
  MetricCard,
  Card,
  CardHeader,
  CardContent,
  Select,
} from "../../components/ui";

interface RevenueMonthly {
  year: number;
  month: number;
  label: string;
  amountInr: number;
  invoiceCount: number;
}

interface AgeingBucket {
  label: string;
  amountInr: number;
  invoiceCount: number;
}

interface TopClient {
  clientId: string;
  clientCode: string;
  clientName: string;
  revenueInr: number;
  invoiceCount: number;
}

interface ComplianceStat {
  serviceCode: string;
  serviceName: string;
  pending: number;
  inProgress: number;
  filed: number;
  delayed: number;
  waived: number;
  overdue: number;
}

interface ClientGrowthPoint {
  year: number;
  month: number;
  label: string;
  newClients: number;
}

function currentFy(): string {
  const d = new Date();
  const start = d.getMonth() + 1 >= 4 ? d.getFullYear() : d.getFullYear() - 1;
  return `${start}-${(start + 1).toString().slice(-2)}`;
}

export default function ReportsPage() {
  const [fy, setFy] = useState(currentFy());
  const [revenue, setRevenue] = useState<RevenueMonthly[] | null>(null);
  const [ageing, setAgeing] = useState<AgeingBucket[] | null>(null);
  const [top, setTop] = useState<TopClient[] | null>(null);
  const [comp, setComp] = useState<ComplianceStat[] | null>(null);
  const [growth, setGrowth] = useState<ClientGrowthPoint[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setError(null);
    apiFetch<RevenueMonthly[]>(`/api/v1/reports/revenue-monthly?fy=${fy}`)
      .then(setRevenue)
      .catch((e) => setError(String(e)));
    apiFetch<TopClient[]>(`/api/v1/reports/top-clients?fy=${fy}&take=10`)
      .then(setTop)
      .catch(() => void 0);
  }, [fy]);

  useEffect(() => {
    apiFetch<AgeingBucket[]>("/api/v1/reports/outstanding-ageing")
      .then(setAgeing)
      .catch(() => void 0);
    apiFetch<ComplianceStat[]>("/api/v1/reports/compliance-stats")
      .then(setComp)
      .catch(() => void 0);
    apiFetch<ClientGrowthPoint[]>("/api/v1/reports/client-growth?months=12")
      .then(setGrowth)
      .catch(() => void 0);
  }, []);

  const totalRevenue = useMemo(
    () => (revenue ?? []).reduce((s, r) => s + r.amountInr, 0),
    [revenue]
  );
  const totalOutstanding = useMemo(
    () => (ageing ?? []).reduce((s, b) => s + b.amountInr, 0),
    [ageing]
  );
  const totalOverdue = useMemo(
    () =>
      (ageing ?? [])
        .filter((b) => b.label !== "Current")
        .reduce((s, b) => s + b.amountInr, 0),
    [ageing]
  );

  const fyOptions = useMemo(() => {
    const now = new Date().getFullYear();
    return [...Array(6)].map((_, i) => {
      const y = now - i - (new Date().getMonth() + 1 >= 4 ? 0 : 1);
      return `${y}-${(y + 1).toString().slice(-2)}`;
    });
  }, []);

  return (
    <div className="space-y-6">
      <PageHeader
        breadcrumbs={[
          { label: "Overview", to: "/dashboard" },
          { label: "Reports & Analytics" },
        ]}
        title="Practice Analytics & Financial Intelligence"
        subtitle="Revenue velocity, debtor ageing analysis, client concentration, and compliance deliverable metrics."
        actions={
          <div className="flex items-center gap-2 text-xs">
            <span className="font-semibold text-slate-600">Financial Year:</span>
            <Select
              value={fy}
              onChange={(e) => setFy(e.target.value)}
              className="py-1 text-xs w-auto font-mono"
            >
              {fyOptions.map((f) => (
                <option key={f} value={f}>
                  FY {f}
                </option>
              ))}
            </Select>
          </div>
        }
      />

      {error && (
        <div className="rounded-lg bg-red-50 border border-red-200 p-3 text-xs text-red-700">
          {error}
        </div>
      )}

      {/* Primary KPI Row */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <MetricCard
          label={`Billed Revenue (FY ${fy})`}
          value={formatInrCompact(totalRevenue)}
          tone="success"
          hint={formatInr(totalRevenue)}
        />
        <MetricCard
          label="Total Debtor Receivables"
          value={formatInrCompact(totalOutstanding)}
          tone="warning"
          hint={formatInr(totalOutstanding)}
        />
        <MetricCard
          label="Overdue Receivables (>0 Days)"
          value={formatInrCompact(totalOverdue)}
          tone="danger"
          hint={formatInr(totalOverdue)}
        />
      </div>

      {/* Charts Grid */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <Card>
          <CardHeader
            title="Monthly Fee Revenue Velocity"
            subtitle={`Monthly billing performance across FY ${fy}`}
          />
          <CardContent>
            {revenue && (
              <BarGrid
                rows={revenue.map((r) => ({
                  label: r.label,
                  value: r.amountInr,
                  meta: `${r.invoiceCount} invoices`,
                }))}
                formatValue={formatInrCompact}
              />
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader
            title="Debtor Ageing Distribution"
            subtitle="Receivables aging from invoice due date"
          />
          <CardContent>
            {ageing && (
              <BarGrid
                rows={ageing.map((b) => ({
                  label: b.label,
                  value: b.amountInr,
                  meta: `${b.invoiceCount} invoices`,
                }))}
                formatValue={formatInrCompact}
                colorFn={(l) =>
                  l === "Current"
                    ? "bg-emerald-500"
                    : l === "1-30"
                      ? "bg-amber-500"
                      : l === "31-60"
                        ? "bg-orange-500"
                        : l === "61-90"
                          ? "bg-rose-500"
                          : "bg-rose-700"
                }
              />
            )}
          </CardContent>
        </Card>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <Card>
          <CardHeader
            title={`Top Client Accounts · FY ${fy}`}
            subtitle="Top 10 fee-generating clients by aggregate billed amount"
          />
          <CardContent className="p-0">
            {top && (
              <table className="min-w-full divide-y divide-slate-100 text-xs">
                <thead className="bg-slate-50 font-medium text-slate-500">
                  <tr>
                    <th className="text-left px-4 py-2.5">Client Legal Entity</th>
                    <th className="text-right px-4 py-2.5">Billed Amount</th>
                    <th className="text-right px-4 py-2.5">Invoices</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100 bg-white">
                  {top.map((t) => (
                    <tr key={t.clientId} className="hover:bg-slate-50/50">
                      <td className="px-4 py-2.5">
                        <div className="font-medium text-slate-900">{t.clientName}</div>
                        <div className="text-[11px] font-mono text-slate-400">{t.clientCode}</div>
                      </td>
                      <td className="px-4 py-2.5 text-right font-mono font-semibold text-slate-800">
                        {formatInr(t.revenueInr)}
                      </td>
                      <td className="px-4 py-2.5 text-right font-mono text-slate-500">
                        {t.invoiceCount}
                      </td>
                    </tr>
                  ))}
                  {top.length === 0 && (
                    <tr>
                      <td colSpan={3} className="px-4 py-8 text-center text-slate-400 italic">
                        No invoices generated in FY {fy} yet.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader
            title="Client Portfolio Expansion"
            subtitle="New entity onboarding velocity over the past 12 months"
          />
          <CardContent>
            {growth && (
              <BarGrid
                rows={growth.map((g) => ({
                  label: g.label,
                  value: g.newClients,
                  meta: `${g.newClients} clients`,
                }))}
                formatValue={(v) => `${v}`}
              />
            )}
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader
          title="Practice Compliance Pipeline by Service Domain"
          subtitle="Statutory filing throughput, overdue bottlenecks, and clearance rate"
        />
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-200 text-xs">
            <thead className="bg-slate-50 font-medium text-slate-500">
              <tr>
                <th className="text-left px-4 py-3">Practice Discipline</th>
                <th className="text-right px-3 py-3">Pending</th>
                <th className="text-right px-3 py-3">In Progress</th>
                <th className="text-right px-3 py-3 text-emerald-800">Filed & Lodged</th>
                <th className="text-right px-3 py-3">Delayed</th>
                <th className="text-right px-3 py-3">Waived</th>
                <th className="text-right px-4 py-3 text-rose-700 font-bold">Overdue Alert</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 bg-white">
              {comp &&
                comp.map((s) => (
                  <tr key={s.serviceCode} className="hover:bg-slate-50/50 transition-colors">
                    <td className="px-4 py-3">
                      <div className="font-semibold text-slate-900">{s.serviceCode}</div>
                      <div className="text-[11px] text-slate-500">{s.serviceName}</div>
                    </td>
                    <td className="px-3 py-3 text-right font-mono text-slate-600">{s.pending}</td>
                    <td className="px-3 py-3 text-right font-mono text-amber-700 font-medium">{s.inProgress}</td>
                    <td className="px-3 py-3 text-right font-mono text-emerald-700 font-semibold">{s.filed}</td>
                    <td className="px-3 py-3 text-right font-mono text-slate-500">{s.delayed}</td>
                    <td className="px-3 py-3 text-right font-mono text-slate-400">{s.waived}</td>
                    <td className="px-4 py-3 text-right font-mono font-bold text-rose-600">
                      {s.overdue}
                    </td>
                  </tr>
                ))}
            </tbody>
          </table>
        </div>
      </Card>
    </div>
  );
}

interface Row {
  label: string;
  value: number;
  meta?: string;
}

function BarGrid({
  rows,
  formatValue,
  colorFn,
}: {
  rows: Row[];
  formatValue: (v: number) => string;
  colorFn?: (label: string) => string;
}) {
  if (rows.length === 0) return <p className="text-xs text-slate-400 italic">No metrics recorded.</p>;
  const max = Math.max(...rows.map((r) => r.value), 1);
  return (
    <ul className="space-y-2.5 text-xs">
      {rows.map((r) => {
        const pct = Math.max(3, Math.round((r.value / max) * 100));
        const barClass = colorFn ? colorFn(r.label) : "bg-brand-500";
        return (
          <li key={r.label} className="flex items-center gap-3">
            <span className="w-20 text-slate-700 font-mono font-medium truncate" title={r.label}>
              {r.label}
            </span>
            <div className="flex-1 h-4 rounded-md bg-slate-100 relative overflow-hidden">
              <div
                className={`absolute inset-y-0 left-0 rounded-md transition-all duration-300 ${barClass}`}
                style={{ width: `${pct}%` }}
              />
            </div>
            <span className="w-24 text-right font-mono font-semibold text-slate-900">
              {formatValue(r.value)}
            </span>
            {r.meta && <span className="w-20 text-right text-[11px] text-slate-400 truncate">{r.meta}</span>}
          </li>
        );
      })}
    </ul>
  );
}
