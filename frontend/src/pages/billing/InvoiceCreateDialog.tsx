import { useEffect, useState } from "react";
import { apiFetch } from "../../api/client";
import { formatInr } from "../../utils/formatters";
import { Modal, FormField, Input, Select, Textarea, Button } from "../../components/ui";

interface ClientHit {
  id: string;
  code: string;
  name: string;
  kind: number | string;
}

interface BranchDto {
  id: string;
  code: string;
  name: string;
  stateId: string;
  stateName: string | null;
}

interface StateDto {
  id: string;
  code: string;
  name: string;
}

interface ServiceDto {
  id: string;
  code: string;
  name: string;
}

type Mode = {
  description: string;
  serviceId?: string;
  qty: number;
  unitPrice: number;
  gstRate: number;
};

export default function InvoiceCreateDialog({
  onClose,
  onCreated,
}: {
  onClose: () => void;
  onCreated: () => void;
}) {
  const [services, setServices] = useState<ServiceDto[]>([]);
  const [states, setStates] = useState<StateDto[]>([]);

  useEffect(() => {
    apiFetch<ServiceDto[]>("/api/v1/masters/services?activeOnly=true")
      .then(setServices)
      .catch(() => void 0);
    apiFetch<StateDto[]>("/api/v1/masters/states?take=100")
      .then(setStates)
      .catch(() => void 0);
  }, []);

  const [clientSearch, setClientSearch] = useState("");
  const [clientHits, setClientHits] = useState<ClientHit[]>([]);
  const [client, setClient] = useState<ClientHit | null>(null);
  const [branches, setBranches] = useState<BranchDto[]>([]);
  const [branchId, setBranchId] = useState<string>("");
  const [placeOfSupplyStateId, setPos] = useState<string>("");
  const [issueDate, setIssueDate] = useState(new Date().toISOString().slice(0, 10));
  const [dueDate, setDueDate] = useState(() => {
    const d = new Date();
    d.setDate(d.getDate() + 30);
    return d.toISOString().slice(0, 10);
  });
  const [notes, setNotes] = useState("");
  const [lines, setLines] = useState<Mode[]>([
    { description: "", qty: 1, unitPrice: 0, gstRate: 18 },
  ]);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!clientSearch || clientSearch.length < 2) {
      setClientHits([]);
      return;
    }
    const timeout = setTimeout(() => {
      apiFetch<{ items: ClientHit[] }>(
        `/api/v1/search?q=${encodeURIComponent(clientSearch)}&kind=Client&take=8`
      )
        .then((r) => setClientHits(r.items ?? []))
        .catch(() => setClientHits([]));
    }, 200);
    return () => clearTimeout(timeout);
  }, [clientSearch]);

  async function pickClient(hit: ClientHit) {
    setClient(hit);
    setClientHits([]);
    setClientSearch(hit.name);
    try {
      const b = await apiFetch<BranchDto[]>(`/api/v1/clients/${hit.id}/branches`);
      setBranches(b);
      const primary = b[0] ?? null;
      if (primary) {
        setBranchId(primary.id);
        setPos(primary.stateId);
      }
    } catch {
      setBranches([]);
    }
  }

  async function autoFillUnitPrice(idx: number, serviceId: string) {
    if (!client || !serviceId) return;
    try {
      const fee = await apiFetch<{ amountInr: number; gstRatePercent: number } | null>(
        `/api/v1/billing/fee-schedules/resolve?serviceId=${serviceId}&clientId=${client.id}&asOf=${issueDate}`
      );
      if (fee) {
        updateLine(idx, { unitPrice: fee.amountInr, gstRate: fee.gstRatePercent });
      }
    } catch {
      // non-fatal
    }
  }

  function updateLine(i: number, patch: Partial<Mode>) {
    setLines((ls) => ls.map((l, idx) => (idx === i ? { ...l, ...patch } : l)));
  }

  function addLine() {
    setLines((ls) => [...ls, { description: "", qty: 1, unitPrice: 0, gstRate: 18 }]);
  }

  function removeLine(i: number) {
    setLines((ls) => ls.filter((_, idx) => idx !== i));
  }

  const subtotal = lines.reduce((sum, l) => sum + l.qty * l.unitPrice, 0);
  const totalGst = lines.reduce((sum, l) => sum + (l.qty * l.unitPrice * l.gstRate) / 100, 0);
  const grandTotal = subtotal + totalGst;

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (!client || !branchId || !placeOfSupplyStateId) {
      setError("Please select a client, client branch, and place of supply state.");
      return;
    }
    if (lines.length === 0 || lines.some((l) => !l.description.trim())) {
      setError("Every line item must have a valid description.");
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      const body = {
        clientId: client.id,
        branchId,
        placeOfSupplyStateId,
        issueDate,
        dueDate,
        notes: notes || null,
        lineItems: lines.map((l) => ({
          description: l.description,
          serviceDefinitionId: l.serviceId || null,
          serviceFileId: null,
          complianceObligationId: null,
          quantity: l.qty,
          unitPriceInr: l.unitPrice,
          gstRatePercent: l.gstRate,
        })),
      };
      await apiFetch("/api/v1/billing/invoices", { method: "POST", body: JSON.stringify(body) });
      onCreated();
    } catch (e) {
      setError(String(e));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Modal open={true} onClose={onClose} title="Draft New Tax Invoice" size="3xl">
      <form onSubmit={submit} className="space-y-4">
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div className="relative">
            <FormField label="Client Entity" required hint="Search by client name, PAN, or 8-character code">
              <Input
                value={clientSearch}
                onChange={(e) => {
                  setClientSearch(e.target.value);
                  setClient(null);
                }}
                placeholder="Type 2+ letters to search…"
                required
              />
            </FormField>
            {clientHits.length > 0 && (
              <div className="absolute z-20 left-0 right-0 mt-1 rounded-xl border border-slate-200 bg-white shadow-xl max-h-48 overflow-y-auto text-xs divide-y divide-slate-100">
                {clientHits.map((h) => (
                  <button
                    type="button"
                    key={h.id}
                    onClick={() => pickClient(h)}
                    className="w-full text-left px-3 py-2 hover:bg-slate-50 transition-colors flex items-center justify-between"
                  >
                    <span className="font-medium text-slate-800">{h.name}</span>
                    <span className="font-mono text-[11px] text-slate-500 bg-slate-100 px-1.5 py-0.5 rounded">
                      {h.code}
                    </span>
                  </button>
                ))}
              </div>
            )}
          </div>

          <FormField label="Billing Branch" required hint="Branch receiving invoice">
            <Select
              value={branchId}
              onChange={(e) => {
                const b = branches.find((x) => x.id === e.target.value);
                setBranchId(e.target.value);
                if (b) setPos(b.stateId);
              }}
              required
              disabled={!client}
            >
              <option value="">{client ? "— Select Branch —" : "— Select client first —"}</option>
              {branches.map((b) => (
                <option key={b.id} value={b.id}>
                  {b.name} ({b.code})
                </option>
              ))}
            </Select>
          </FormField>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
          <FormField label="Place of Supply (POS State)" required hint="Determines CGST+SGST vs. IGST">
            <Select
              value={placeOfSupplyStateId}
              onChange={(e) => setPos(e.target.value)}
              required
            >
              <option value="">— Select State —</option>
              {states.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name} ({s.code})
                </option>
              ))}
            </Select>
          </FormField>

          <FormField label="Invoice Issue Date" required>
            <Input
              type="date"
              value={issueDate}
              onChange={(e) => setIssueDate(e.target.value)}
              required
            />
          </FormField>

          <FormField label="Payment Due Date" required>
            <Input
              type="date"
              value={dueDate}
              onChange={(e) => setDueDate(e.target.value)}
              required
            />
          </FormField>
        </div>

        {/* Line Items Table */}
        <div className="space-y-2 pt-2">
          <div className="flex items-center justify-between">
            <h4 className="text-xs font-semibold uppercase tracking-wider text-slate-700">
              Taxable Services & Disbursements ({lines.length})
            </h4>
            <Button type="button" variant="outline" size="xs" onClick={addLine}>
              + Add Item
            </Button>
          </div>

          <div className="overflow-x-auto rounded-lg border border-slate-200">
            <table className="min-w-full divide-y divide-slate-200 text-xs">
              <thead className="bg-slate-50 font-medium text-slate-600">
                <tr>
                  <th className="text-left px-3 py-2">Service Description *</th>
                  <th className="text-left px-3 py-2 w-36">SAC / Service</th>
                  <th className="text-right px-3 py-2 w-16">Qty</th>
                  <th className="text-right px-3 py-2 w-28">Fee Rate (₹)</th>
                  <th className="text-right px-3 py-2 w-20">GST %</th>
                  <th className="text-right px-3 py-2 w-28">Line Total</th>
                  <th className="w-8" />
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 bg-white">
                {lines.map((l, i) => (
                  <tr key={i} className="hover:bg-slate-50/50">
                    <td className="p-2">
                      <Input
                        value={l.description}
                        onChange={(e) => updateLine(i, { description: e.target.value })}
                        placeholder="e.g. Statutory Audit for FY 2024-25"
                        required
                        className="py-1 text-xs"
                      />
                    </td>
                    <td className="p-2">
                      <Select
                        value={l.serviceId ?? ""}
                        onChange={(e) => {
                          updateLine(i, { serviceId: e.target.value || undefined });
                          if (e.target.value) autoFillUnitPrice(i, e.target.value);
                        }}
                        className="py-1 text-xs"
                      >
                        <option value="">— Generic —</option>
                        {services.map((s) => (
                          <option key={s.id} value={s.id}>
                            {s.code}
                          </option>
                        ))}
                      </Select>
                    </td>
                    <td className="p-2">
                      <Input
                        type="number"
                        step="0.001"
                        min="0.001"
                        value={l.qty}
                        onChange={(e) => updateLine(i, { qty: Number(e.target.value) })}
                        className="py-1 text-xs text-right font-mono"
                      />
                    </td>
                    <td className="p-2">
                      <Input
                        type="number"
                        step="0.01"
                        min="0"
                        value={l.unitPrice}
                        onChange={(e) => updateLine(i, { unitPrice: Number(e.target.value) })}
                        className="py-1 text-xs text-right font-mono"
                      />
                    </td>
                    <td className="p-2">
                      <Select
                        value={l.gstRate}
                        onChange={(e) => updateLine(i, { gstRate: Number(e.target.value) })}
                        className="py-1 text-xs text-right font-mono"
                      >
                        <option value={0}>0%</option>
                        <option value={5}>5%</option>
                        <option value={12}>12%</option>
                        <option value={18}>18%</option>
                        <option value={28}>28%</option>
                      </Select>
                    </td>
                    <td className="p-2 text-right font-mono font-medium text-slate-800">
                      {formatInr(l.qty * l.unitPrice * (1 + l.gstRate / 100))}
                    </td>
                    <td className="p-2 text-center">
                      {lines.length > 1 && (
                        <button
                          type="button"
                          onClick={() => removeLine(i)}
                          className="text-slate-400 hover:text-rose-600 transition-colors p-1"
                        >
                          ×
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        {/* GST Totals Summary Box */}
        <div className="rounded-xl border border-slate-200 bg-slate-50/70 p-4 space-y-2 text-xs">
          <div className="flex justify-between text-slate-600">
            <span>Taxable Subtotal (INR)</span>
            <span className="font-mono font-medium">{formatInr(subtotal)}</span>
          </div>
          <div className="flex justify-between text-slate-600">
            <span>Estimated GST Split (CGST + SGST or IGST)</span>
            <span className="font-mono font-medium">{formatInr(totalGst)}</span>
          </div>
          <div className="flex justify-between text-slate-900 font-bold text-sm pt-2 border-t border-slate-200">
            <span>Total Invoice Amount (INR)</span>
            <span className="font-mono text-brand-900">{formatInr(grandTotal)}</span>
          </div>
        </div>

        <FormField label="Invoice Remarks / Client Notes">
          <Textarea
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            rows={2}
            placeholder="Payment instructions, bank account details reference, or special terms..."
          />
        </FormField>

        {error && (
          <div className="rounded-lg bg-red-50 border border-red-200 p-2.5 text-xs text-red-700">
            {error}
          </div>
        )}

        <div className="flex justify-end gap-2 pt-3 border-t border-slate-200">
          <Button type="button" variant="outline" onClick={onClose} disabled={submitting}>
            Cancel
          </Button>
          <Button type="submit" variant="primary" loading={submitting}>
            {submitting ? "Creating Invoice…" : "Save Draft Invoice"}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
