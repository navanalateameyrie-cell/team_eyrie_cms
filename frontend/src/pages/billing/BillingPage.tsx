import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { apiFetch } from "../../api/client";
import { useAuth } from "../../auth/AuthContext";
import { formatInr } from "../../utils/formatters";
import InvoiceCreateDialog from "./InvoiceCreateDialog";
import SendInvoiceEmailDialog from "./SendInvoiceEmailDialog";
import {
  PageHeader,
  MetricCard,
  Card,
  Button,
  Badge,
  Select,
  LoadingSpinner,
  EmptyState,
} from "../../components/ui";

type InvoiceStatus = "Draft" | "Issued" | "PartPaid" | "Paid" | "Cancelled" | number;

interface LineItem {
  id: string;
  lineNumber: number;
  description: string;
  serviceCode: string | null;
  fileNumber: string | null;
  obligationPeriodKey: string | null;
  quantity: number;
  unitPriceInr: number;
  gstRatePercent: number;
  lineSubtotalInr: number;
  lineGstInr: number;
  lineTotalInr: number;
}

interface InvoiceDto {
  id: string;
  number: string;
  financialYear: string;
  clientId: string;
  clientCode: string;
  clientName: string;
  branchName: string;
  placeOfSupplyStateName: string;
  status: InvoiceStatus;
  issueDate: string;
  dueDate: string;
  notes: string | null;
  isIntraState: boolean;
  subtotalInr: number;
  cgstInr: number;
  sgstInr: number;
  igstInr: number;
  totalInr: number;
  paidInr: number;
  creditedInr: number;
  outstandingInr: number;
  lineItems: LineItem[];
  version: number;
  eInvoiceStatus: number | string;
  irn: string | null;
  ackNo: string | null;
  ackDateUtc: string | null;
}

const EINV_GENERATED = (s: number | string) => s === 2 || s === "Generated";
const EINV_CANCELLED = (s: number | string) => s === 3 || s === "Cancelled";

interface InvoicePage {
  items: InvoiceDto[];
  total: number;
  skip: number;
  take: number;
}

interface Aggregates {
  outstandingInr: number;
  overdueInr: number;
  revenueThisFyInr: number;
  draft: number;
  issued: number;
  partPaid: number;
  paid: number;
  cancelled: number;
}

const STATUS_CONFIG: Record<number, { label: string; tone: "neutral" | "brand" | "warning" | "success" | "danger" }> = {
  1: { label: "Draft", tone: "neutral" },
  2: { label: "Issued", tone: "brand" },
  3: { label: "Part Paid", tone: "warning" },
  4: { label: "Paid", tone: "success" },
  5: { label: "Cancelled", tone: "danger" },
};

function statusNum(s: InvoiceStatus) {
  if (typeof s === "number") return s;
  return { Draft: 1, Issued: 2, PartPaid: 3, Paid: 4, Cancelled: 5 }[s] ?? 1;
}

export function BillingPage() {
  const { hasPermission } = useAuth();
  const canWrite = hasPermission("billing.write");
  const canEmail = hasPermission("email.send");
  const [emailing, setEmailing] = useState<InvoiceDto | null>(null);
  const [status, setStatus] = useState<string>("");
  const [data, setData] = useState<InvoicePage | null>(null);
  const [agg, setAgg] = useState<Aggregates | null>(null);
  const [skip, setSkip] = useState(0);
  const take = 25;
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);

  useEffect(() => {
    setLoading(true);
    setError(null);
    const q = new URLSearchParams();
    if (status) q.set("status", status);
    q.set("skip", String(skip));
    q.set("take", String(take));
    apiFetch<InvoicePage>(`/api/v1/billing/invoices?${q}`)
      .then(setData)
      .catch((e) => setError(String(e)))
      .finally(() => setLoading(false));
  }, [status, skip, refreshKey]);

  useEffect(() => {
    apiFetch<Aggregates>("/api/v1/billing/invoices/aggregates")
      .then(setAgg)
      .catch(() => void 0);
  }, [refreshKey]);

  const items = useMemo(() => data?.items ?? [], [data]);

  async function issue(id: string) {
    try {
      await apiFetch(`/api/v1/billing/invoices/${id}/issue`, { method: "POST" });
      setRefreshKey((n) => n + 1);
    } catch (e) {
      alert(String(e));
    }
  }

  async function cancel(id: string) {
    if (!confirm("Cancel this tax invoice? This irreversible action updates practice financials.")) return;
    try {
      await apiFetch(`/api/v1/billing/invoices/${id}/cancel`, { method: "POST" });
      setRefreshKey((n) => n + 1);
    } catch (e) {
      alert(String(e));
    }
  }

  async function issueNote(inv: InvoiceDto, kind: "credit-notes" | "debit-notes") {
    if (!canWrite) return;
    const label = kind === "credit-notes" ? "Credit Note" : "Debit Note";
    const subtotal = window.prompt(`Subtotal amount ₹ for the ${label}:`, "");
    if (!subtotal || Number(subtotal) <= 0) return;
    const gstRate = window.prompt("GST percentage % applicable:", "18");
    if (!gstRate) return;
    const reason = window.prompt("Audit reason / note (optional):", "") || null;
    try {
      await apiFetch(`/api/v1/billing/${kind}`, {
        method: "POST",
        body: JSON.stringify({
          invoiceId: inv.id,
          issueDate: new Date().toISOString().slice(0, 10),
          reason,
          subtotalInr: Number(subtotal),
          gstRatePercent: Number(gstRate),
        }),
      });
      setRefreshKey((n) => n + 1);
    } catch (e) {
      alert(String(e));
    }
  }

  async function generateIrn(inv: InvoiceDto) {
    if (!canWrite) return;
    if (!confirm(`Transmit invoice ${inv.number} to the Government IRP for E-Invoice QR code & IRN?`)) return;
    try {
      await apiFetch(`/api/v1/billing/invoices/${inv.id}/einvoice/generate`, { method: "POST" });
      setRefreshKey((n) => n + 1);
    } catch (e) {
      alert(String(e));
    }
  }

  async function cancelIrn(inv: InvoiceDto) {
    if (!canWrite) return;
    const reason = window.prompt(
      "Cancellation reason code (1=Duplicate, 2=Data Entry Error, 3=Order Cancelled, 4=Other):",
      "4"
    );
    if (!reason) return;
    try {
      await apiFetch(`/api/v1/billing/invoices/${inv.id}/einvoice/cancel`, {
        method: "POST",
        body: JSON.stringify({ reason }),
      });
      setRefreshKey((n) => n + 1);
    } catch (e) {
      alert(String(e));
    }
  }

  async function downloadPdf(inv: InvoiceDto) {
    const token = (() => {
      try {
        return JSON.parse(localStorage.getItem("cms.auth.v1") ?? "null")?.token ?? null;
      } catch {
        return null;
      }
    })();
    try {
      const res = await fetch(`/api/v1/billing/invoices/${inv.id}/pdf`, {
        headers: token ? { Authorization: `Bearer ${token}` } : undefined,
      });
      if (!res.ok) {
        alert(`PDF generation failed (${res.status})`);
        return;
      }
      const blob = await res.blob();
      const url = URL.createObjectURL(blob);
      window.open(url, "_blank", "noopener");
      setTimeout(() => URL.revokeObjectURL(url), 60_000);
    } catch (e) {
      alert(String(e));
    }
  }

  async function recordPayment(inv: InvoiceDto) {
    const amount = window.prompt(
      `Settlement amount in INR (Outstanding: ${formatInr(inv.outstandingInr)}):`,
      inv.outstandingInr.toFixed(2)
    );
    if (!amount) return;
    const paidOn = window.prompt("Receipt date (YYYY-MM-DD):", new Date().toISOString().slice(0, 10));
    if (!paidOn) return;
    const mode = window.prompt("Instrument (Bank/NEFT/RTGS/UPI/Cheque/Cash):", "Bank") || "Bank";
    const reference = window.prompt("Transaction UTR / Cheque No. (optional):", "") || null;
    try {
      await apiFetch(`/api/v1/billing/invoices/${inv.id}/payments`, {
        method: "POST",
        body: JSON.stringify({ paidOn, amountInr: Number(amount), mode, reference }),
      });
      setRefreshKey((n) => n + 1);
    } catch (e) {
      alert(String(e));
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        breadcrumbs={[
          { label: "Billing", to: "/admin/billing" },
          { label: "Tax Invoices" },
        ]}
        title="Fee Invoicing & GST Accounting"
        subtitle="Practice invoices, CGST/SGST/IGST breakdown, E-Invoicing (IRN), and settlement tracking."
        actions={
          <div className="flex items-center gap-2">
            <Link to="/admin/billing/reconcile">
              <Button variant="outline" size="sm">
                <svg className="w-4 h-4 mr-1 text-slate-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
                Bank Statement Reconcile
              </Button>
            </Link>
            {canWrite && (
              <Button
                variant="primary"
                size="sm"
                onClick={() => setCreating(true)}
              >
                + Create Tax Invoice
              </Button>
            )}
          </div>
        }
      />

      {agg && (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          <MetricCard
            label="Outstanding Receivables"
            value={formatInr(agg.outstandingInr)}
            tone="warning"
            hint="Total unpaid invoices"
          />
          <MetricCard
            label="Overdue Balance"
            value={formatInr(agg.overdueInr)}
            tone="danger"
            hint="Past invoice credit terms"
          />
          <MetricCard
            label="Practice Revenue FY"
            value={formatInr(agg.revenueThisFyInr)}
            tone="success"
            hint="Collected fee receipts"
          />
          <MetricCard
            label="Invoice Status Funnel"
            value={`${agg.issued + agg.partPaid} Active`}
            tone="brand"
            hint={`Draft: ${agg.draft} · Paid: ${agg.paid}`}
          />
        </div>
      )}

      <Card>
        <div className="px-5 py-3.5 border-b border-slate-100 flex flex-wrap items-center justify-between gap-3">
          <div className="flex items-center gap-3">
            <div className="flex items-center gap-2 text-xs">
              <span className="font-semibold text-slate-600">Status Filter:</span>
              <Select
                value={status}
                onChange={(e) => {
                  setStatus(e.target.value);
                  setSkip(0);
                }}
                className="w-auto py-1 text-xs"
              >
                <option value="">All Invoices</option>
                <option value="Draft">Draft</option>
                <option value="Issued">Issued</option>
                <option value="PartPaid">Partially Paid</option>
                <option value="Paid">Fully Paid</option>
                <option value="Cancelled">Cancelled</option>
              </Select>
            </div>
          </div>

          <div className="text-xs text-slate-500 font-medium">
            {data ? (
              <span>
                Showing <strong className="text-slate-800">{items.length}</strong> of{" "}
                <strong className="text-slate-800">{data.total}</strong> invoices
              </span>
            ) : loading ? (
              "Loading records…"
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
                <th className="text-left px-4 py-3">Invoice No.</th>
                <th className="text-left px-4 py-3">Client & Jurisdiction</th>
                <th className="text-left px-4 py-3">Date</th>
                <th className="text-left px-4 py-3">Due Date</th>
                <th className="text-right px-4 py-3">Gross Total</th>
                <th className="text-right px-4 py-3">Paid (₹)</th>
                <th className="text-right px-4 py-3">Balance (₹)</th>
                <th className="text-left px-4 py-3">Status</th>
                <th className="text-right px-4 py-3">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 bg-white">
              {loading && items.length === 0 ? (
                <tr>
                  <td colSpan={9} className="py-12 text-center">
                    <LoadingSpinner text="Loading invoices…" />
                  </td>
                </tr>
              ) : items.length === 0 ? (
                <tr>
                  <td colSpan={9} className="py-12">
                    <EmptyState
                      title="No invoices found"
                      description="No invoices match the selected status filter."
                      action={
                        canWrite ? (
                          <Button variant="primary" size="sm" onClick={() => setCreating(true)}>
                            Create First Invoice
                          </Button>
                        ) : undefined
                      }
                    />
                  </td>
                </tr>
              ) : (
                items.map((inv) => {
                  const s = statusNum(inv.status);
                  const isOverdue = (s === 2 || s === 3) && new Date(inv.dueDate) < new Date();
                  const cfg = STATUS_CONFIG[s] ?? { label: "Unknown", tone: "neutral" };

                  return (
                    <tr key={inv.id} className="hover:bg-slate-50/60 transition-colors">
                      <td className="px-4 py-3 font-mono font-semibold text-slate-800 whitespace-nowrap">
                        {inv.number}
                        <div className="text-[10px] text-slate-400 font-normal">
                          {inv.isIntraState ? "CGST + SGST" : "IGST"}
                        </div>
                      </td>

                      <td className="px-4 py-3">
                        <Link
                          to={`/admin/clients/${inv.clientId}`}
                          className="font-medium text-slate-900 hover:text-brand-600 transition-colors block"
                        >
                          {inv.clientName}
                        </Link>
                        <div className="text-[11px] text-slate-500 flex items-center gap-1 font-mono mt-0.5">
                          <span>{inv.clientCode}</span>
                          {inv.placeOfSupplyStateName && (
                            <>
                              <span className="text-slate-300">·</span>
                              <span className="font-sans text-[10px] text-slate-400">
                                {inv.placeOfSupplyStateName}
                              </span>
                            </>
                          )}
                        </div>
                      </td>

                      <td className="px-4 py-3 text-slate-600 font-mono text-[11px] whitespace-nowrap">
                        {inv.issueDate}
                      </td>

                      <td className="px-4 py-3 font-mono text-[11px] whitespace-nowrap">
                        <span className={isOverdue ? "text-rose-600 font-bold" : "text-slate-600"}>
                          {inv.dueDate}
                        </span>
                        {isOverdue && (
                          <span className="block text-[10px] text-rose-500 font-sans font-medium">
                            Overdue
                          </span>
                        )}
                      </td>

                      <td className="px-4 py-3 text-right font-mono font-semibold text-slate-900 whitespace-nowrap">
                        {formatInr(inv.totalInr)}
                      </td>

                      <td className="px-4 py-3 text-right font-mono text-emerald-700 whitespace-nowrap">
                        {formatInr(inv.paidInr)}
                      </td>

                      <td className="px-4 py-3 text-right font-mono whitespace-nowrap">
                        <span
                          className={`font-semibold ${inv.outstandingInr > 0 ? "text-amber-900" : "text-slate-400"
                            }`}
                        >
                          {formatInr(inv.outstandingInr)}
                        </span>
                      </td>

                      <td className="px-4 py-3">
                        <Badge tone={cfg.tone} size="sm">
                          {cfg.label}
                        </Badge>
                        {EINV_GENERATED(inv.eInvoiceStatus) && (
                          <div className="mt-1">
                            <span
                              className="inline-block rounded bg-indigo-50 border border-indigo-200 text-indigo-800 px-1.5 py-0.5 text-[10px] font-mono"
                              title={inv.irn ?? ""}
                            >
                              IRN: {inv.irn?.slice(0, 8)}…
                            </span>
                          </div>
                        )}
                        {EINV_CANCELLED(inv.eInvoiceStatus) && (
                          <div className="mt-1">
                            <span className="inline-block rounded bg-slate-100 text-slate-500 px-1.5 py-0.5 text-[10px]">
                              IRN Cancelled
                            </span>
                          </div>
                        )}
                      </td>

                      <td className="px-4 py-3 text-right whitespace-nowrap">
                        <div className="flex items-center justify-end gap-1.5 flex-wrap">
                          <button
                            type="button"
                            onClick={() => downloadPdf(inv)}
                            className="rounded px-2 py-1 text-[11px] font-medium text-slate-700 bg-white border border-slate-200 hover:bg-slate-50"
                          >
                            PDF
                          </button>

                          {canEmail && (
                            <button
                              type="button"
                              onClick={() => setEmailing(inv)}
                              className="rounded px-2 py-1 text-[11px] font-medium text-slate-700 bg-white border border-slate-200 hover:bg-slate-50"
                            >
                              Email
                            </button>
                          )}

                          {canWrite && s === 1 && (
                            <button
                              type="button"
                              onClick={() => issue(inv.id)}
                              className="rounded px-2 py-1 text-[11px] font-medium text-sky-800 bg-sky-50 border border-sky-200 hover:bg-sky-100"
                            >
                              Issue
                            </button>
                          )}

                          {canWrite && (s === 2 || s === 3) && (
                            <button
                              type="button"
                              onClick={() => recordPayment(inv)}
                              className="rounded px-2 py-1 text-[11px] font-medium text-emerald-800 bg-emerald-50 border border-emerald-200 hover:bg-emerald-100"
                            >
                              Pay
                            </button>
                          )}

                          {canWrite && (s === 2 || s === 3 || s === 4) && !EINV_GENERATED(inv.eInvoiceStatus) && !EINV_CANCELLED(inv.eInvoiceStatus) && (
                            <button
                              type="button"
                              onClick={() => generateIrn(inv)}
                              className="rounded px-2 py-1 text-[11px] font-medium text-indigo-800 bg-indigo-50 border border-indigo-200 hover:bg-indigo-100"
                            >
                              IRN
                            </button>
                          )}

                          {canWrite && (s === 2 || s === 3) && (
                            <>
                              <button
                                type="button"
                                onClick={() => issueNote(inv, "credit-notes")}
                                title="Issue Credit Note"
                                className="rounded px-1.5 py-1 text-[11px] font-medium text-rose-800 bg-rose-50 border border-rose-200 hover:bg-rose-100"
                              >
                                CN
                              </button>
                              <button
                                type="button"
                                onClick={() => issueNote(inv, "debit-notes")}
                                title="Issue Debit Note"
                                className="rounded px-1.5 py-1 text-[11px] font-medium text-purple-800 bg-purple-50 border border-purple-200 hover:bg-purple-100"
                              >
                                DN
                              </button>
                            </>
                          )}

                          {canWrite && EINV_GENERATED(inv.eInvoiceStatus) && (
                            <button
                              type="button"
                              onClick={() => cancelIrn(inv)}
                              className="rounded px-1.5 py-1 text-[11px] text-slate-600 bg-slate-100 hover:bg-slate-200"
                            >
                              Cancel IRN
                            </button>
                          )}

                          {canWrite && s !== 4 && s !== 5 && (
                            <button
                              type="button"
                              onClick={() => cancel(inv.id)}
                              className="rounded px-1.5 py-1 text-[11px] text-rose-700 hover:bg-rose-50"
                            >
                              Cancel
                            </button>
                          )}
                        </div>
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

      {creating && (
        <InvoiceCreateDialog
          onClose={() => setCreating(false)}
          onCreated={() => {
            setCreating(false);
            setRefreshKey((n) => n + 1);
          }}
        />
      )}

      {emailing && (
        <SendInvoiceEmailDialog
          invoice={{
            id: emailing.id,
            number: emailing.number,
            totalInr: emailing.totalInr,
            dueDate: emailing.dueDate,
            clientName: emailing.clientName,
          }}
          onClose={() => setEmailing(null)}
          onSent={() => {
            setEmailing(null);
            setRefreshKey((n) => n + 1);
          }}
        />
      )}
    </div>
  );
}

export default BillingPage;

