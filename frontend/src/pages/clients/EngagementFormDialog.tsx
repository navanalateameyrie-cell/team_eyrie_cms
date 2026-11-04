import { useState } from "react";
import type {
  CreateEngagementBody,
  EngagementDto,
  EngagementPeriodType,
  UpdateEngagementBody,
} from "../../api/engagements";
import { Modal, FormField, Input, Select, Textarea, Button } from "../../components/ui";

const PERIOD_TYPES: EngagementPeriodType[] = ["Annual", "HalfYear", "Quarter", "Month", "AdHoc"];

interface Props {
  initial?: EngagementDto;
  onSave: (body: CreateEngagementBody | UpdateEngagementBody) => Promise<void>;
  onClose: () => void;
}

export default function EngagementFormDialog({ initial, onSave, onClose }: Props) {
  const [fy, setFy] = useState(initial?.financialYear ?? "");
  const [ay, setAy] = useState(initial?.assessmentYear ?? "");
  const [periodType, setPeriodType] = useState<EngagementPeriodType>(initial?.periodType ?? "Annual");
  const [periodLabel, setPeriodLabel] = useState(initial?.periodLabel ?? "");
  const [plannedStart, setPlannedStart] = useState(initial?.plannedStartDate ?? "");
  const [plannedEnd, setPlannedEnd] = useState(initial?.plannedEndDate ?? "");
  const [notes, setNotes] = useState(initial?.notes ?? "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const common: CreateEngagementBody = {
        financialYear: fy.trim(),
        assessmentYear: ay.trim() || null,
        periodType,
        periodLabel: periodType === "Annual" ? null : periodLabel.trim() || null,
        plannedStartDate: plannedStart,
        plannedEndDate: plannedEnd,
        notes: notes.trim() || null,
      };
      await onSave(initial ? { ...common, version: initial.version } : common);
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
      title={initial ? `Edit Engagement · ${initial.financialYear}` : "Schedule Period Engagement"}
      size="md"
    >
      <form onSubmit={submit} className="space-y-4">
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <FormField label="Financial Year (FY)" required hint="Format: YYYY-YY (e.g. 2024-25)">
            <Input
              required
              maxLength={7}
              value={fy}
              onChange={(e) => setFy(e.target.value)}
              placeholder="2024-25"
              className="font-mono tracking-wider"
            />
          </FormField>

          <FormField label="Assessment Year (AY)" hint="Optional income tax reference">
            <Input
              maxLength={7}
              value={ay}
              onChange={(e) => setAy(e.target.value)}
              placeholder="2025-26"
              className="font-mono tracking-wider"
            />
          </FormField>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <FormField label="Period Cadence" required>
            <Select
              value={periodType}
              onChange={(e) => setPeriodType(e.target.value as EngagementPeriodType)}
            >
              {PERIOD_TYPES.map((t) => (
                <option key={t} value={t}>
                  {t}
                </option>
              ))}
            </Select>
          </FormField>

          <FormField
            label="Period Identifier"
            hint={periodType === "Annual" ? "Not applicable for Annual" : "e.g. Q1, Q2, H1, Apr-2024"}
          >
            <Input
              maxLength={32}
              value={periodLabel}
              disabled={periodType === "Annual"}
              onChange={(e) => setPeriodLabel(e.target.value)}
              placeholder={periodType === "Annual" ? "— Not applicable —" : "e.g. Q1"}
            />
          </FormField>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <FormField label="Target Start Date" required>
            <Input
              required
              type="date"
              value={plannedStart}
              onChange={(e) => setPlannedStart(e.target.value)}
            />
          </FormField>

          <FormField label="Target Delivery / Filing Date" required>
            <Input
              required
              type="date"
              value={plannedEnd}
              onChange={(e) => setPlannedEnd(e.target.value)}
            />
          </FormField>
        </div>

        <FormField label="Deliverable Scope Notes">
          <Textarea
            maxLength={2000}
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            rows={2}
            placeholder="Special engagement terms, fee basis, or agreed milestones..."
          />
        </FormField>

        {error && (
          <div className="rounded-lg bg-red-50 border border-red-200 p-2.5 text-xs text-red-700">
            {error}
          </div>
        )}

        <div className="flex justify-end gap-2 pt-3 border-t border-slate-200">
          <Button type="button" variant="outline" onClick={onClose} disabled={busy}>
            Cancel
          </Button>
          <Button type="submit" variant="primary" loading={busy}>
            {busy ? "Saving…" : "Save Engagement"}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
