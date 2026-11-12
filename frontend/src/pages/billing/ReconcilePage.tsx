import { useEffect, useRef, useState } from "react";
import { apiFetch, apiRawFetch } from "../../api/client";
import { useAuth } from "../../auth/AuthContext";
import { formatInr } from "../../utils/formatters";
import {
  PageHeader,
  Card,
  CardHeader,
  CardContent,
  Button,
  Badge,
  Select,
} from "../../components/ui";

interface Batch {
  id: string;
  fileName: string;
  sizeBytes: number;
  uploadedAtUtc: string;
  uploadedByUserName: string | null;
  lineCount: number;
  matchedInvoiceCount: number;
  matchedAdvanceCount: number;
  ignoredCount: number;
}

interface Line {
  id: string;
  batchId: string;
  valueDate: string;
  description: string;
  reference: string | null;
  amountInr: number;
  status: number | string;
  matchedInvoiceId: string | null;
  matchedInvoiceNumber: string | null;
  matchedAdvanceId: string | null;
  matchNote: string | null;
  version: number;
}

interface Invoice {
  id: string;
  number: string;
  clientName: string;
  totalInr: number;
  paidInr: number;
  creditedInr: number;
  outstandingInr: number;
  status: number | string;
}

const STATUS_CONFIG: Record<number, { label: string; tone: "warning" | "success" | "brand" | "neutral" }> = {
  1: { label: "Unmatched", tone: "warning" },
  2: { label: "Matched · Invoice", tone: "success" },
  3: { label: "Matched · Advance", tone: "brand" },
  4: { label: "Ignored", tone: "neutral" },
};

const toNum = (s: number | string) =>
  typeof s === "number"
    ? s
    : ({ Unmatched: 1, MatchedInvoice: 2, MatchedAdvance: 3, Ignored: 4 } as Record<string, number>)[s] ?? 1;

export function ReconcilePage() {
  const { hasPermission } = useAuth();
  const canWrite = hasPermission("billing.write");
  const [batches, setBatches] = useState<Batch[]>([]);
  const [selected, setSelected] = useState<Batch | null>(null);
  const [lines, setLines] = useState<Line[]>([]);
  const [openInvoices, setOpenInvoices] = useState<Invoice[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [uploading, setUploading] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);
  const fileInput = useRef<HTMLInputElement>(null);

  useEffect(() => {
    (async () => {
      try {
        const bs = await apiFetch<Batch[]>("/api/v1/billing/reconcile/batches");
        setBatches(bs);
        if (!selected && bs.length > 0) setSelected(bs[0]);

        // Open invoices feed the manual-match picker
        const page = await apiFetch<{ items: Invoice[] }>("/api/v1/billing/invoices?status=Issued&take=500");
        const page2 = await apiFetch<{ items: Invoice[] }>("/api/v1/billing/invoices?status=PartPaid&take=500");
        setOpenInvoices([...page.items, ...page2.items]);
      } catch (e) {
        setError(String(e));
      }
    })();
  }, [refreshKey]);

  useEffect(() => {
    if (!selected) {
      setLines([]);
      return;
    }
    apiFetch<Line[]>(`/api/v1/billing/reconcile/batches/${selected.id}/lines`)
      .then(setLines)
      .catch((e) => setError(String(e)));
  }, [selected, refreshKey]);

  async function upload(e: React.FormEvent) {
    e.preventDefault();
    const file = fileInput.current?.files?.[0];
    if (!file || !canWrite) return;
    setError(null);
    setUploading(true);
    try {
      const form = new FormData();
      form.append("File", file);
      const res = await apiRawFetch("/api/v1/billing/reconcile/upload", {
        method: "POST",
        body: form,
      });
      if (!res.ok) {
        const text = await res.text().catch(() => "");
        setError(`Upload failed (${res.status}): ${text}`);
        return;
      }
      if (fileInput.current) fileInput.current.value = "";
      setRefreshKey((n) => n + 1);
    } catch (e) {
      setError(String(e));
    } finally {
      setUploading(false);
    }
  }

  async function match(line: Line, invoiceId: string) {
    if (!canWrite) return;
    try {
      await apiFetch(`/api/v1/billing/reconcile/lines/${line.id}/match-invoice`, {
        method: "POST",
        body: JSON.stringify({ invoiceId, amountOverride: null, note: null }),
      });
      setRefreshKey((n) => n + 1);
    } catch (e) {
      setError(String(e));
    }
  }

  async function ignore(line: Line) {
    if (!canWrite) return;
    try {
      await apiFetch(`/api/v1/billing/reconcile/lines/${line.id}/ignore`, {
        method: "POST",
        body: JSON.stringify({ note: null }),
      });
      setRefreshKey((n) => n + 1);
    } catch (e) {
      setError(String(e));
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        breadcrumbs={[
          { label: "Billing", to: "/admin/billing" },
          { label: "Bank Reconciliation" },
        ]}
        title="Payment & Bank Statement Reconciliation"
        subtitle="Automated payment matching with ±₹1 tolerance on bank statement CSV feeds."
        actions={
          canWrite ? (
            <form onSubmit={upload} className="flex items-center gap-2">
              <label className="cursor-pointer inline-flex items-center px-3 py-1.5 border border-slate-300 rounded-lg text-xs font-medium text-slate-700 bg-white hover:bg-slate-50 transition-colors">
                <svg className="w-4 h-4 mr-1 text-slate-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12" />
                </svg>
                Choose CSV
                <input
                  ref={fileInput}
                  type="file"
                  accept=".csv,text/csv"
                  required
                  className="hidden"
                />
              </label>
              <Button type="submit" variant="primary" size="sm" loading={uploading}>
                {uploading ? "Ingesting Statement…" : "Upload Statement"}
              </Button>
            </form>
          ) : undefined
        }
      />

      {error && (
        <div className="rounded-lg bg-red-50 border border-red-200 p-3 text-xs text-red-700 flex items-start gap-2">
          <svg className="w-4 h-4 text-red-600 shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          <span>{error}</span>
        </div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-4 gap-6">
        {/* Left: Statement Batches Sidebar */}
        <Card className="lg:col-span-1">
          <CardHeader
            title="Imported Batches"
            subtitle="Uploaded statement files"
          />
          <CardContent className="p-3">
            <div className="space-y-1.5">
              {batches.map((b) => {
                const isSelected = selected?.id === b.id;
                const pending = b.lineCount - b.matchedInvoiceCount - b.matchedAdvanceCount - b.ignoredCount;
                return (
                  <button
                    key={b.id}
                    type="button"
                    onClick={() => setSelected(b)}
                    className={`w-full text-left rounded-xl p-3 text-xs transition-all ${isSelected
                      ? "bg-brand-50 border border-brand-200 text-brand-900 shadow-2xs"
                      : "hover:bg-slate-50 border border-transparent text-slate-700"
                      }`}
                  >
                    <div className="font-semibold truncate">{b.fileName}</div>
                    <div className="text-[11px] text-slate-500 mt-1">
                      {new Date(b.uploadedAtUtc).toLocaleDateString()} · {b.lineCount} transactions
                    </div>
                    <div className="mt-2 flex items-center gap-1.5 text-[10px]">
                      <span className="bg-emerald-100 text-emerald-800 px-1.5 py-0.2 rounded font-medium">
                        {b.matchedInvoiceCount} matched
                      </span>
                      {pending > 0 && (
                        <span className="bg-amber-100 text-amber-800 px-1.5 py-0.2 rounded font-medium">
                          {pending} review
                        </span>
                      )}
                    </div>
                  </button>
                );
              })}
              {batches.length === 0 && (
                <p className="text-xs text-slate-400 italic py-6 text-center">
                  No statement CSV uploads recorded.
                </p>
              )}
            </div>
          </CardContent>
        </Card>

        {/* Right: Statement Lines Table */}
        <Card className="lg:col-span-3">
          <CardHeader
            title={selected ? `Statement Transactions · ${selected.fileName}` : "Transaction Ledger"}
            subtitle="Matching bank credits with outstanding sales invoices"
          />
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-slate-200 text-xs">
              <thead className="bg-slate-50 font-medium text-slate-500">
                <tr>
                  <th className="text-left px-4 py-3">Value Date</th>
                  <th className="text-left px-4 py-3">Description</th>
                  <th className="text-left px-4 py-3">Bank Ref</th>
                  <th className="text-right px-4 py-3">Credit (INR)</th>
                  <th className="text-left px-4 py-3">Reconciliation Status</th>
                  <th className="text-right px-4 py-3">Action</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 bg-white">
                {lines.map((l) => {
                  const s = toNum(l.status);
                  const cfg = STATUS_CONFIG[s] ?? { label: "Unknown", tone: "neutral" };
                  return (
                    <tr key={l.id} className="hover:bg-slate-50/50 transition-colors">
                      <td className="px-4 py-3 font-mono text-slate-600 whitespace-nowrap">
                        {l.valueDate}
                      </td>

                      <td className="px-4 py-3 text-slate-900 max-w-xs truncate" title={l.description}>
                        {l.description}
                      </td>

                      <td className="px-4 py-3 font-mono text-slate-500">
                        {l.reference ?? "—"}
                      </td>

                      <td
                        className={`px-4 py-3 text-right font-mono font-medium whitespace-nowrap ${l.amountInr >= 0 ? "text-emerald-700" : "text-slate-500"
                          }`}
                      >
                        {formatInr(l.amountInr)}
                      </td>

                      <td className="px-4 py-3">
                        <Badge tone={cfg.tone} size="sm">
                          {cfg.label}
                        </Badge>
                        {l.matchedInvoiceNumber && (
                          <div className="text-[11px] text-slate-600 font-mono mt-0.5">
                            Inv: {l.matchedInvoiceNumber}
                          </div>
                        )}
                      </td>

                      <td className="px-4 py-3 text-right whitespace-nowrap">
                        {canWrite && s === 1 && l.amountInr > 0 && (
                          <div className="flex items-center justify-end gap-1.5">
                            <Select
                              defaultValue=""
                              onChange={(e) => e.target.value && match(l, e.target.value)}
                              className="py-1 text-xs w-48 font-mono"
                            >
                              <option value="">Match to Open Invoice…</option>
                              {openInvoices.map((i) => (
                                <option key={i.id} value={i.id}>
                                  {i.number} · {i.clientName} ({formatInr(i.outstandingInr)})
                                </option>
                              ))}
                            </Select>
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => ignore(l)}
                              className="text-slate-500 hover:text-slate-700 text-xs py-1"
                            >
                              Ignore
                            </Button>
                          </div>
                        )}
                      </td>
                    </tr>
                  );
                })}
                {lines.length === 0 && selected && (
                  <tr>
                    <td colSpan={6} className="py-12 text-center text-slate-400 italic">
                      No transaction rows found in this statement batch.
                    </td>
                  </tr>
                )}
                {!selected && (
                  <tr>
                    <td colSpan={6} className="py-12 text-center text-slate-400 italic">
                      Select a statement batch from the left to inspect and reconcile lines.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </Card>
      </div>
    </div>
  );
}

export default ReconcilePage;

