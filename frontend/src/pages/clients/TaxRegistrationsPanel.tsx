import { useCallback, useEffect, useState } from "react";
import {
  taxRegistrationsApi,
  type TaxRegistrationDto,
  type TaxRegistrationType,
  type TaxRegistrationUpsertBody,
} from "../../api/branches";
import { ApiError } from "../../api/client";
import { Button, Input, Select } from "../../components/ui";

interface Props {
  clientId: string;
  branchId: string;
  canWrite: boolean;
}

const TYPES: TaxRegistrationType[] = ["Gstin", "Tan", "Pt", "Pf", "Esic", "Iec"];

export default function TaxRegistrationsPanel({ clientId, branchId, canWrite }: Props) {
  const [rows, setRows] = useState<TaxRegistrationDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [adding, setAdding] = useState(false);
  const [draft, setDraft] = useState<TaxRegistrationUpsertBody>({
    type: "Gstin",
    number: "",
    effectiveFrom: null,
    effectiveTo: null,
  });
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setRows(await taxRegistrationsApi.list(clientId, branchId));
    } finally {
      setLoading(false);
    }
  }, [clientId, branchId]);

  useEffect(() => {
    void load();
  }, [load]);

  async function add() {
    setError(null);
    try {
      await taxRegistrationsApi.create(clientId, branchId, {
        ...draft,
        number: draft.number.trim().toUpperCase(),
      });
      setDraft({ type: "Gstin", number: "", effectiveFrom: null, effectiveTo: null });
      setAdding(false);
      await load();
    } catch (ex: unknown) {
      if (ex instanceof ApiError) {
        try {
          const parsed = JSON.parse(ex.body) as { error?: string };
          setError(parsed.error ?? ex.message);
        } catch {
          setError(ex.message);
        }
      } else {
        setError(ex instanceof Error ? ex.message : "Failed to record tax registration");
      }
    }
  }

  async function remove(row: TaxRegistrationDto) {
    if (!confirm(`Deactivate ${row.type} ${row.number}?`)) return;
    await taxRegistrationsApi.deactivate(clientId, branchId, row.id, row.version);
    await load();
  }

  return (
    <div className="mt-3 pt-3 border-t border-slate-100">
      <div className="flex justify-between items-center text-xs">
        <span className="font-semibold text-slate-700 uppercase tracking-wider text-[11px]">
          Tax Registrations & Filings
        </span>
        {canWrite && !adding && (
          <button
            type="button"
            onClick={() => setAdding(true)}
            className="text-xs font-semibold text-brand-600 hover:text-brand-700 transition-colors"
          >
            + Add Registration
          </button>
        )}
      </div>

      {loading ? (
        <p className="text-xs text-slate-400 mt-1">Loading tax registrations…</p>
      ) : rows.length === 0 && !adding ? (
        <p className="text-xs text-slate-400 mt-1 italic">No state tax registrations recorded for this branch.</p>
      ) : (
        <div className="mt-2 flex flex-wrap gap-2">
          {rows.map((r) => (
            <div
              key={r.id}
              className={`inline-flex items-center gap-2 rounded-lg border px-2.5 py-1 text-xs transition-colors ${
                r.isActive
                  ? "bg-slate-50/80 border-slate-200 text-slate-800"
                  : "bg-slate-100 border-slate-200 text-slate-400 opacity-75"
              }`}
            >
              <span className="font-semibold text-[10px] uppercase tracking-wider text-slate-500">
                {r.type}
              </span>
              <span className="font-mono font-medium text-slate-900 tracking-wide">
                {r.number}
              </span>
              {!r.isActive && <span className="text-[10px] text-slate-400">(inactive)</span>}
              {canWrite && r.isActive && (
                <button
                  type="button"
                  onClick={() => remove(r)}
                  title={`Deactivate ${r.type}`}
                  className="text-slate-400 hover:text-red-600 ml-1 transition-colors"
                >
                  ×
                </button>
              )}
            </div>
          ))}
        </div>
      )}

      {adding && (
        <div className="mt-2.5 rounded-lg border border-brand-200 bg-brand-50/30 p-2.5 flex flex-wrap items-center gap-2 text-xs animate-fadeIn">
          <Select
            value={draft.type}
            onChange={(e) => setDraft({ ...draft, type: e.target.value as TaxRegistrationType })}
            className="w-auto py-1 text-xs"
          >
            {TYPES.map((t) => (
              <option key={t} value={t}>
                {t}
              </option>
            ))}
          </Select>
          <Input
            value={draft.number}
            placeholder="Registration Number (e.g. 27AAAAA0000A1Z5)"
            onChange={(e) => setDraft({ ...draft, number: e.target.value.toUpperCase() })}
            className="flex-1 min-w-[200px] py-1 text-xs font-mono tracking-wider uppercase"
          />
          <Button variant="primary" size="sm" onClick={add}>
            Save
          </Button>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => {
              setAdding(false);
              setError(null);
            }}
          >
            Cancel
          </Button>
        </div>
      )}
      {error && <p className="mt-1 text-xs text-red-600">{error}</p>}
    </div>
  );
}
