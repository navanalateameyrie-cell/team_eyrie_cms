import { useEffect, useMemo, useState } from "react";
import { apiFetch } from "../../api/client";
import { useAuth } from "../../auth/AuthContext";
import { formatInr } from "../../utils/formatters";
import {
  Card,
  CardHeader,
  CardContent,
  MetricCard,
  Button,
  Badge,
  Input,
} from "../../components/ui";

interface Entry {
  id: string;
  clientId: string;
  deductionDate: string;
  section: string;
  deducteeName: string;
  deducteePan: string | null;
  grossAmountInr: number;
  tdsRatePercent: number;
  tdsAmountInr: number;
  quarter: number;
  financialYear: string;
  challanId: string | null;
  challanNumber: string | null;
  notes: string | null;
  version: number;
}

interface Challan {
  id: string;
  clientId: string;
  challanNumber: string;
  depositDate: string;
  bankBsrCode: string | null;
  section: string;
  totalAmountInr: number;
  notes: string | null;
  version: number;
}

interface SummaryCell {
  section: string;
  quarter: number;
  count: number;
  grossInr: number;
  tdsInr: number;
  depositedInr: number;
}

interface Summary {
  financialYear: string;
  grossInr: number;
  tdsInr: number;
  depositedInr: number;
  grid: SummaryCell[];
}

const QUARTERS = ["Q1", "Q2", "Q3", "Q4"];

function currentFy(): string {
  const d = new Date();
  const start = d.getMonth() + 1 >= 4 ? d.getFullYear() : d.getFullYear() - 1;
  return `${start}-${(start + 1).toString().slice(-2)}`;
}

export default function TdsSection({ clientId }: { clientId: string }) {
  const { hasPermission } = useAuth();
  const canWrite = hasPermission("tds.write");
  const [fy, setFy] = useState(currentFy());
  const [entries, setEntries] = useState<Entry[]>([]);
  const [challans, setChallans] = useState<Challan[]>([]);
  const [summary, setSummary] = useState<Summary | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [refreshKey, setRefreshKey] = useState(0);

  useEffect(() => {
    (async () => {
      try {
        const [e, ch, s] = await Promise.all([
          apiFetch<Entry[]>(`/api/v1/clients/${clientId}/tds/entries?fy=${fy}`),
          apiFetch<Challan[]>(`/api/v1/clients/${clientId}/tds/challans`),
          apiFetch<Summary>(`/api/v1/clients/${clientId}/tds/summary?fy=${fy}`),
        ]);
        setEntries(e);
        setChallans(ch);
        setSummary(s);
        setError(null);
      } catch (ex) {
        setError(String(ex));
      }
    })();
  }, [clientId, fy, refreshKey]);

  async function addEntry() {
    if (!canWrite) return;
    const date = window.prompt("Deduction date (YYYY-MM-DD):", new Date().toISOString().slice(0, 10));
    if (!date) return;
    const section = window.prompt("Section (e.g. 194C, 194J, 194I, 194H):", "194C");
    if (!section) return;
    const deducteeName = window.prompt("Deductee party name:", "");
    if (!deducteeName) return;
    const pan = window.prompt("Deductee PAN (optional):", "") || null;
    const gross = Number(window.prompt("Gross amount ₹:", "0") ?? "0");
    const rate = Number(window.prompt("TDS rate % (e.g. 1, 2, 10):", "1") ?? "0");
    try {
      await apiFetch(`/api/v1/clients/${clientId}/tds/entries`, {
        method: "POST",
        body: JSON.stringify({
          deductionDate: date,
          section,
          deducteeName,
          deducteePan: pan,
          grossAmountInr: gross,
          tdsRatePercent: rate,
          notes: null,
        }),
      });
      setRefreshKey((n) => n + 1);
    } catch (e) {
      setError(String(e));
    }
  }

  async function addChallan() {
    if (!canWrite) return;
    const number = window.prompt("Challan identification number (CIN):", "");
    if (!number) return;
    const date = window.prompt("Deposit date (YYYY-MM-DD):", new Date().toISOString().slice(0, 10));
    if (!date) return;
    const section = window.prompt("Section:", "194C");
    if (!section) return;
    const total = Number(window.prompt("Total deposited ₹:", "0") ?? "0");
    const bsr = window.prompt("Bank BSR code (optional):", "") || null;
    try {
      await apiFetch(`/api/v1/clients/${clientId}/tds/challans`, {
        method: "POST",
        body: JSON.stringify({
          challanNumber: number,
          depositDate: date,
          bankBsrCode: bsr,
          section,
          totalAmountInr: total,
          notes: null,
        }),
      });
      setRefreshKey((n) => n + 1);
    } catch (e) {
      setError(String(e));
    }
  }

  async function linkEntryToChallan(entry: Entry) {
    if (!canWrite) return;
    const candidate = challans.filter((c) => c.section === entry.section);
    if (candidate.length === 0) {
      alert(`No challans found for Section ${entry.section}. Record a challan deposit first.`);
      return;
    }
    const list = candidate
      .map((c) => `${c.challanNumber} · ${c.depositDate} · ${formatInr(c.totalAmountInr)}`)
      .join("\n");
    const number = window.prompt(`Select Challan Number to link:\n${list}`, candidate[0].challanNumber);
    if (!number) return;
    const chosen = candidate.find((c) => c.challanNumber === number);
    if (!chosen) {
      alert("No matching challan found.");
      return;
    }
    try {
      await apiFetch(`/api/v1/clients/${clientId}/tds/challans/${chosen.id}/link`, {
        method: "POST",
        body: JSON.stringify({ entryIds: [entry.id] }),
      });
      setRefreshKey((n) => n + 1);
    } catch (e) {
      setError(String(e));
    }
  }

  const grid = useMemo(() => {
    const sections = Array.from(new Set((summary?.grid ?? []).map((c) => c.section))).sort();
    return sections.map((section) => ({
      section,
      cells: QUARTERS.map(
        (_, i) =>
          summary?.grid.find((c) => c.section === section && c.quarter === i + 1) ?? {
            section,
            quarter: i + 1,
            count: 0,
            grossInr: 0,
            tdsInr: 0,
            depositedInr: 0,
          }
      ),
    }));
  }, [summary]);

  return (
    <Card>
      <CardHeader
        title={`Tax Deducted at Source (TDS) · FY ${fy}`}
        description="Withholding ledger, quarterly returns (Form 26Q), ITNS 281 challans, and deposit matching."
        action={
          <div className="flex items-center gap-2">
            <div className="flex items-center gap-1.5">
              <span className="text-xs text-slate-500 font-medium">FY:</span>
              <Input
                value={fy}
                onChange={(e) => setFy(e.target.value)}
                className="w-24 py-1 text-xs font-mono"
                placeholder="2024-25"
              />
            </div>
            {canWrite && (
              <>
                <Button variant="outline" size="sm" onClick={addChallan}>
                  + Record Challan
                </Button>
                <Button variant="primary" size="sm" onClick={addEntry}>
                  + Add Deduction Entry
                </Button>
              </>
            )}
          </div>
        }
      />

      <CardContent className="space-y-6">
        {error && (
          <div className="rounded-lg bg-red-50 border border-red-200 p-3 text-xs text-red-700">
            {error}
          </div>
        )}

        {summary && (
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <MetricCard
              label="Gross Transaction Base"
              value={formatInr(summary.grossInr)}
              hint="Subject to TDS deduction"
            />
            <MetricCard
              label="Total TDS Withheld"
              value={formatInr(summary.tdsInr)}
              hint="Liability accrued"
            />
            <MetricCard
              label="Deposited to Govt (Challan)"
              value={formatInr(summary.depositedInr)}
              hint={
                summary.depositedInr >= summary.tdsInr
                  ? "Fully deposited"
                  : `Pending: ${formatInr(summary.tdsInr - summary.depositedInr)}`
              }
              trend={{
                direction: summary.depositedInr >= summary.tdsInr ? "up" : "down",
                label: summary.depositedInr >= summary.tdsInr ? "Matched" : "Unpaid",
              }}
            />
          </div>
        )}

        {grid.length > 0 && (
          <div className="space-y-2">
            <h4 className="text-xs font-semibold uppercase tracking-wider text-slate-700">
              Quarterly TDS Breakdown Matrix
            </h4>
            <div className="overflow-x-auto rounded-lg border border-slate-200/80">
              <table className="min-w-full divide-y divide-slate-200 text-xs">
                <thead className="bg-slate-50 font-medium text-slate-500">
                  <tr>
                    <th className="text-left px-3 py-2">Section Code</th>
                    {QUARTERS.map((q) => (
                      <th key={q} className="text-right px-3 py-2">
                        {q} TDS (₹)
                      </th>
                    ))}
                    <th className="text-right px-3 py-2 font-semibold">Total TDS</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100 bg-white">
                  {grid.map((row) => {
                    const rowTotal = row.cells.reduce((s, c) => s + c.tdsInr, 0);
                    return (
                      <tr key={row.section} className="hover:bg-slate-50/50 transition-colors">
                        <td className="px-3 py-2 font-mono font-semibold text-slate-800">
                          {row.section}
                        </td>
                        {row.cells.map((c) => (
                          <td key={c.quarter} className="px-3 py-2 text-right font-mono">
                            {c.count === 0 ? (
                              <span className="text-slate-300">—</span>
                            ) : (
                              <span
                                className={
                                  c.depositedInr >= c.tdsInr
                                    ? "text-emerald-700 font-medium"
                                    : "text-amber-700 font-medium"
                                }
                              >
                                {formatInr(c.tdsInr)}
                              </span>
                            )}
                          </td>
                        ))}
                        <td className="px-3 py-2 text-right font-mono font-semibold text-slate-900">
                          {formatInr(rowTotal)}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </div>
        )}

        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <h4 className="text-xs font-semibold uppercase tracking-wider text-slate-700">
              Deduction Register ({entries.length})
            </h4>
          </div>

          {entries.length === 0 ? (
            <p className="text-xs text-slate-400 italic py-2">No TDS entries recorded for FY {fy}.</p>
          ) : (
            <div className="overflow-x-auto rounded-lg border border-slate-200/80">
              <table className="min-w-full divide-y divide-slate-200 text-xs">
                <thead className="bg-slate-50 font-medium text-slate-500">
                  <tr>
                    <th className="text-left px-3 py-2">Date</th>
                    <th className="text-left px-3 py-2">Section</th>
                    <th className="text-left px-3 py-2">Deductee Entity & PAN</th>
                    <th className="text-right px-3 py-2">Gross Base</th>
                    <th className="text-right px-3 py-2">Rate %</th>
                    <th className="text-right px-3 py-2">TDS Deducted</th>
                    <th className="text-left px-3 py-2">ITNS Challan</th>
                    {canWrite && <th className="text-right px-3 py-2">Action</th>}
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100 bg-white">
                  {entries.map((e) => (
                    <tr key={e.id} className="hover:bg-slate-50/50 transition-colors">
                      <td className="px-3 py-2 font-mono text-slate-600 whitespace-nowrap">
                        {e.deductionDate}
                      </td>
                      <td className="px-3 py-2 font-mono font-semibold text-slate-800">
                        {e.section}
                      </td>
                      <td className="px-3 py-2">
                        <div className="font-medium text-slate-900">{e.deducteeName}</div>
                        {e.deducteePan && (
                          <div className="font-mono text-[11px] text-slate-500 uppercase tracking-wider">
                            {e.deducteePan}
                          </div>
                        )}
                      </td>
                      <td className="px-3 py-2 text-right font-mono text-slate-700">
                        {formatInr(e.grossAmountInr)}
                      </td>
                      <td className="px-3 py-2 text-right font-mono text-slate-600">
                        {e.tdsRatePercent}%
                      </td>
                      <td className="px-3 py-2 text-right font-mono font-semibold text-amber-900">
                        {formatInr(e.tdsAmountInr)}
                      </td>
                      <td className="px-3 py-2">
                        {e.challanNumber ? (
                          <Badge variant="success" size="sm">
                            <span className="font-mono">{e.challanNumber}</span>
                          </Badge>
                        ) : (
                          <span className="text-[11px] text-slate-400 italic">Unlinked</span>
                        )}
                      </td>
                      {canWrite && (
                        <td className="px-3 py-2 text-right">
                          {!e.challanId && (
                            <button
                              type="button"
                              onClick={() => linkEntryToChallan(e)}
                              className="text-xs font-semibold text-brand-600 hover:text-brand-800 transition-colors"
                            >
                              Link Challan
                            </button>
                          )}
                        </td>
                      )}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </CardContent>
    </Card>
  );
}
