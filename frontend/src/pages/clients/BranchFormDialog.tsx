import { useEffect, useState } from "react";
import { mastersApi, type StateDto } from "../../api/masters";
import type { BranchDto, BranchType, CreateBranchBody, UpdateBranchBody } from "../../api/branches";
import { Modal, FormField, Input, Select, Button } from "../../components/ui";

interface Props {
  initial?: BranchDto;
  onSave: (body: CreateBranchBody | UpdateBranchBody) => Promise<void>;
  onClose: () => void;
}

function today(): string {
  return new Date().toISOString().slice(0, 10);
}

export default function BranchFormDialog({ initial, onSave, onClose }: Props) {
  const [states, setStates] = useState<StateDto[]>([]);
  const [code, setCode] = useState(initial?.code ?? "");
  const [name, setName] = useState(initial?.name ?? "");
  const [type, setType] = useState<BranchType>(initial?.type ?? "HeadOffice");
  const [stateId, setStateId] = useState(initial?.stateId ?? "");
  const [line1, setLine1] = useState(initial?.address.line1 ?? "");
  const [line2, setLine2] = useState(initial?.address.line2 ?? "");
  const [city, setCity] = useState(initial?.address.city ?? "");
  const [pincode, setPincode] = useState(initial?.address.pincode ?? "");
  const [openedOn, setOpenedOn] = useState(initial?.openedOn ?? today());
  const [isPrimary, setIsPrimary] = useState(initial?.isPrimary ?? false);
  const [isBillingEnabled, setBilling] = useState(initial?.isBillingEnabled ?? true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void mastersApi.states.list(undefined, true).then(setStates);
  }, []);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const addr = { line1, line2: line2 || null, city, pincode };
      if (initial) {
        const body: UpdateBranchBody = {
          code,
          name,
          type,
          stateId,
          address: addr,
          isBillingEnabled,
          version: initial.version,
        };
        await onSave(body);
      } else {
        const body: CreateBranchBody = {
          code,
          name,
          type,
          stateId,
          address: addr,
          openedOn,
          isPrimary,
          isBillingEnabled,
        };
        await onSave(body);
      }
    } catch (ex: unknown) {
      setError(ex instanceof Error ? ex.message : "Save failed");
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal
      open={true}
      onClose={onClose}
      title={initial ? `Edit Branch · ${initial.code}` : "Register New Branch"}
      size="lg"
    >
      <form onSubmit={submit} className="space-y-4">
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <FormField label="Branch Code" required hint="Short unique code (e.g. MUM-HO)">
            <Input
              required
              maxLength={32}
              value={code}
              onChange={(e) => setCode(e.target.value.toUpperCase())}
              placeholder="e.g. DEL-01"
              className="font-mono uppercase"
            />
          </FormField>

          <FormField label="Branch / Location Name" required>
            <Input
              required
              maxLength={200}
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="e.g. Mumbai Corporate Office"
            />
          </FormField>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <FormField label="Branch Classification" required>
            <Select
              value={type}
              onChange={(e) => setType(e.target.value as BranchType)}
            >
              <option value="HeadOffice">Head Office</option>
              <option value="Branch">Branch / Regional Office</option>
            </Select>
          </FormField>

          <FormField label="State / Jurisdiction" required>
            <Select
              required
              value={stateId}
              onChange={(e) => setStateId(e.target.value)}
            >
              <option value="">— Select State —</option>
              {states.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.code} · {s.name}
                </option>
              ))}
            </Select>
          </FormField>
        </div>

        <div className="rounded-lg border border-slate-200 bg-slate-50/50 p-4 space-y-3">
          <h4 className="text-xs font-semibold uppercase tracking-wider text-slate-500">
            Physical Operating Address
          </h4>
          <div className="space-y-2">
            <Input
              required
              maxLength={200}
              placeholder="Address Line 1 *"
              value={line1}
              onChange={(e) => setLine1(e.target.value)}
            />
            <Input
              maxLength={200}
              placeholder="Address Line 2 (Suite, Floor, Landmark)"
              value={line2}
              onChange={(e) => setLine2(e.target.value)}
            />
            <div className="grid grid-cols-2 gap-3">
              <Input
                required
                maxLength={100}
                placeholder="City *"
                value={city}
                onChange={(e) => setCity(e.target.value)}
              />
              <Input
                required
                maxLength={16}
                placeholder="PIN Code *"
                value={pincode}
                onChange={(e) => setPincode(e.target.value)}
                className="font-mono"
              />
            </div>
          </div>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 pt-1">
          {!initial && (
            <FormField label="Establishment / Opened Date">
              <Input
                type="date"
                value={openedOn}
                onChange={(e) => setOpenedOn(e.target.value)}
              />
            </FormField>
          )}

          <div className="flex flex-col justify-center space-y-2 pt-2">
            <label className="flex items-center gap-2 cursor-pointer">
              <input
                type="checkbox"
                checked={isBillingEnabled}
                onChange={(e) => setBilling(e.target.checked)}
                className="rounded border-slate-300 text-brand-600 focus:ring-brand-500"
              />
              <span className="text-xs font-medium text-slate-700">Billing Enabled for this Branch</span>
            </label>

            {!initial && (
              <label className="flex items-center gap-2 cursor-pointer">
                <input
                  type="checkbox"
                  checked={isPrimary}
                  onChange={(e) => setIsPrimary(e.target.checked)}
                  className="rounded border-slate-300 text-brand-600 focus:ring-brand-500"
                />
                <span className="text-xs font-medium text-slate-700">Set as Primary Practice Branch</span>
              </label>
            )}
          </div>
        </div>

        {error && (
          <div className="rounded-lg bg-red-50 border border-red-200 p-3 text-xs text-red-700">
            {error}
          </div>
        )}

        <div className="flex justify-end gap-2 pt-3 border-t border-slate-200">
          <Button type="button" variant="outline" onClick={onClose} disabled={busy}>
            Cancel
          </Button>
          <Button type="submit" variant="primary" loading={busy}>
            {busy ? "Saving Branch…" : "Save Branch"}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
