import { useEffect, useState, useMemo } from "react";
import { apiFetch, apiRawFetch } from "../../api/client";
import { Card, PageHeader, MetricCard, Badge, Button, EmptyState, LoadingSpinner } from "../../components/ui";
import { formatInr } from "../../utils/formatters";

type Status = number | string;

interface Inv {
  id: string;
  number: string;
  financialYear: string;
  issueDate: string;
  dueDate: string;
  status: Status;
  totalInr: number;
  paidInr: number;
  outstandingInr: number;
  eInvoiceStatus: Status;
}

const STATUS_CONFIG: Record<number, { label: string; tone: "neutral" | "brand" | "warning" | "success" | "danger" }> = {
  1: { label: "Draft", tone: "neutral" },
  2: { label: "Issued", tone: "brand" },
  3: { label: "Part Paid", tone: "warning" },
  4: { label: "Paid", tone: "success" },
  5: { label: "Cancelled", tone: "danger" },
};

const num = (s: Status): number =>
  typeof s === "number"
    ? s
    : ({ Draft: 1, Issued: 2, PartPaid: 3, Paid: 4, Cancelled: 5 } as Record<string, number>)[s] ?? 0;

export default function PortalInvoicesPage() {
  const [rows, setRows] = useState<Inv[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [downloadingId, setDownloadingId] = useState<string | null>(null);

  useEffect(() => {
    apiFetch<Inv[]>("/api/v1/portal/invoices")
      .then(setRows)
      .catch((e) => setError(String(e)));
  }, []);

  const metrics = useMemo(() => {
    if (!rows) return { totalBilled: 0, totalPaid: 0, totalOutstanding: 0, count: 0 };
    return {
      totalBilled: rows.reduce((acc, r) => acc + (r.totalInr || 0), 0),
      totalPaid: rows.reduce((acc, r) => acc + (r.paidInr || 0), 0),
      totalOutstanding: rows.reduce((acc, r) => acc + (r.outstandingInr || 0), 0),
      count: rows.length,
    };
  }, [rows]);

  async function downloadPdf(inv: Inv) {
    setDownloadingId(inv.id);
    try {
      const r = await apiRawFetch(`/api/v1/portal/invoices/${inv.id}/pdf`);
      if (!r.ok) {
        alert(`Download failed (${r.status})`);
        return;
      }
      const blob = await r.blob();
      const url = URL.createObjectURL(blob);
      window.open(url, "_blank", "noopener");
      setTimeout(() => URL.revokeObjectURL(url), 60_000);
    } finally {
      setDownloadingId(null);
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Invoices & Statements"
        subtitle="Review professional fee billing, GST tax breakdowns, payment receipts, and download certified tax invoices."
      />

      {error && (
        <div className="rounded-lg border border-red-200 bg-red-50 text-sm text-red-700 p-4">
          <p className="font-semibold">Unable to load invoices</p>
          <p className="text-xs mt-0.5">{error}</p>
        </div>
      )}

      {/* Financial KPI Summary Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <MetricCard
          label="Total Invoiced"
          value={formatInr(metrics.totalBilled)}
          hint={`${metrics.count} total billing statements`}
          tone="default"
        />
        <MetricCard
          label="Total Paid"
          value={formatInr(metrics.totalPaid)}
          hint="Reconciled payments received"
          tone="success"
        />
        <MetricCard
          label="Outstanding Balance"
          value={formatInr(metrics.totalOutstanding)}
          hint={metrics.totalOutstanding > 0 ? "Pending remittance" : "Account fully reconciled"}
          tone={metrics.totalOutstanding > 0 ? "brand" : "success"}
        />
        <MetricCard
          label="Statements"
          value={metrics.count}
          hint="All financial years"
          tone="info"
        />
      </div>

      {/* Invoices Data Table */}
      <Card>
        {rows === null ? (
          <div className="py-16">
            <LoadingSpinner size="md" label="Loading invoices…" />
          </div>
        ) : rows.length === 0 ? (
          <EmptyState
            title="No invoices found"
            description="Your Chartered Accountant practice has not generated any invoices for your account yet."
          />
        ) : (
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-slate-200 text-sm">
              <thead className="bg-slate-50 text-slate-600 text-xs uppercase tracking-wider font-semibold">
                <tr>
                  <th className="text-left px-4 py-3">Invoice Number</th>
                  <th className="text-left px-4 py-3">Issue Date</th>
                  <th className="text-left px-4 py-3">Due Date</th>
                  <th className="text-right px-4 py-3">Total Amount</th>
                  <th className="text-right px-4 py-3">Paid</th>
                  <th className="text-right px-4 py-3">Outstanding</th>
                  <th className="text-center px-4 py-3">Status</th>
                  <th className="text-right px-4 py-3">Action</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 bg-white">
                {rows.map((inv) => {
                  const statusNum = num(inv.status);
                  const cfg = STATUS_CONFIG[statusNum] ?? { label: "Unknown", tone: "neutral" };
                  return (
                    <tr key={inv.id} className="hover:bg-slate-50/80 transition-colors">
                      <td className="px-4 py-3">
                        <div className="font-mono text-xs font-semibold text-slate-900">{inv.number}</div>
                        <div className="text-[11px] text-slate-500">FY {inv.financialYear}</div>
                      </td>
                      <td className="px-4 py-3 text-xs text-slate-600">{inv.issueDate}</td>
                      <td className="px-4 py-3 text-xs text-slate-600">{inv.dueDate}</td>
                      <td className="px-4 py-3 text-right tabular-nums font-semibold text-slate-900">
                        {formatInr(inv.totalInr)}
                      </td>
                      <td className="px-4 py-3 text-right tabular-nums text-emerald-700 font-medium">
                        {formatInr(inv.paidInr)}
                      </td>
                      <td className="px-4 py-3 text-right tabular-nums font-medium">
                        <span className={inv.outstandingInr > 0 ? "text-amber-700 font-semibold" : "text-slate-400"}>
                          {formatInr(inv.outstandingInr)}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-center">
                        <Badge tone={cfg.tone} size="sm">
                          {cfg.label}
                        </Badge>
                      </td>
                      <td className="px-4 py-3 text-right">
                        <Button
                          variant="outline"
                          size="sm"
                          busy={downloadingId === inv.id}
                          onClick={() => downloadPdf(inv)}
                          icon={
                            <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 10v6m0 0l-3-3m3 3l3-3m2 8H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                            </svg>
                          }
                        >
                          PDF
                        </Button>
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
