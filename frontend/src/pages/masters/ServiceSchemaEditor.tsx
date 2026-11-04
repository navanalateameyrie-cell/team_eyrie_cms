import { useMemo } from "react";
import type { ServiceFieldDef } from "../../api/masters";

type FieldType = ServiceFieldDef["type"];
const FIELD_TYPES: FieldType[] = ["string", "date", "number", "select"];

interface Props {
  title: string;
  fields: ServiceFieldDef[];
  defaultRequired: boolean; // if adding a row from here, pre-check required?
  onChange: (next: ServiceFieldDef[]) => void;
}

/**
 * Table-based editor for a list of service field definitions. The parent component owns the
 * canonical required[] / optional[] arrays; this component only emits new arrays via `onChange`.
 * Validation is light-touch — a strict regex compile check runs client-side on edit.
 */
export default function ServiceSchemaEditor({ title, fields, defaultRequired, onChange }: Props) {
  const emptyField = useMemo<ServiceFieldDef>(
    () => ({ name: "", label: "", type: "string", required: defaultRequired }),
    [defaultRequired]
  );

  function update(i: number, patch: Partial<ServiceFieldDef>) {
    const next = fields.slice();
    next[i] = { ...next[i], ...patch };
    onChange(next);
  }

  function remove(i: number) {
    onChange(fields.filter((_, j) => j !== i));
  }

  function add() {
    onChange([...fields, { ...emptyField }]);
  }

  function parseOptions(csv: string): string[] | null {
    const parts = csv.split(",").map((s) => s.trim()).filter(Boolean);
    return parts.length > 0 ? parts : null;
  }

  function regexError(pattern: string | null | undefined): string | null {
    if (!pattern) return null;
    try {
      new RegExp(pattern);
      return null;
    } catch (ex) {
      return (ex as Error).message;
    }
  }

  return (
    <fieldset className="rounded-xl border border-slate-200 bg-slate-50/70 p-4 space-y-3">
      <legend className="px-2 text-xs font-bold uppercase tracking-wider text-slate-700">
        {title} ({fields.length})
      </legend>

      {fields.length === 0 && (
        <p className="text-xs text-slate-500 italic py-1">No custom schema fields defined yet.</p>
      )}

      {fields.map((f, i) => {
        const regErr = regexError(f.pattern ?? null);
        return (
          <div
            key={i}
            className="rounded-lg border border-slate-200 bg-white p-3 shadow-xs space-y-2.5 transition hover:border-slate-300"
          >
            <div className="grid grid-cols-12 gap-2.5 items-center">
              <input
                className="col-span-12 sm:col-span-3 rounded-md border border-slate-300 px-2.5 py-1.5 text-xs font-mono text-slate-900 focus:border-brand-500 focus:ring-1 focus:ring-brand-500/20 outline-none"
                placeholder="field name (e.g. branchId)"
                value={f.name}
                onChange={(e) => update(i, { name: e.target.value.trim() })}
              />
              <input
                className="col-span-12 sm:col-span-4 rounded-md border border-slate-300 px-2.5 py-1.5 text-xs text-slate-900 focus:border-brand-500 focus:ring-1 focus:ring-brand-500/20 outline-none"
                placeholder="Label shown in UI"
                value={f.label}
                onChange={(e) => update(i, { label: e.target.value })}
              />
              <select
                className="col-span-6 sm:col-span-2 rounded-md border border-slate-300 px-2.5 py-1.5 text-xs text-slate-900 bg-white focus:border-brand-500 focus:ring-1 focus:ring-brand-500/20 outline-none"
                value={f.type}
                onChange={(e) => update(i, { type: e.target.value as FieldType })}
              >
                {FIELD_TYPES.map((t) => (
                  <option key={t} value={t}>
                    {t}
                  </option>
                ))}
              </select>
              <label className="col-span-4 sm:col-span-2 flex items-center gap-1.5 text-xs font-medium text-slate-700 cursor-pointer">
                <input
                  type="checkbox"
                  checked={!!f.required}
                  onChange={(e) => update(i, { required: e.target.checked })}
                  className="rounded border-slate-300 text-brand-600 focus:ring-brand-500"
                />
                Required
              </label>
              <div className="col-span-2 sm:col-span-1 text-right">
                <button
                  type="button"
                  onClick={() => remove(i)}
                  className="text-xs font-medium text-red-600 hover:text-red-800 transition"
                >
                  Delete
                </button>
              </div>
            </div>

            <div className="grid grid-cols-12 gap-2.5">
              <input
                className="col-span-12 sm:col-span-4 rounded-md border border-slate-300 px-2.5 py-1.5 text-xs text-slate-700 placeholder-slate-400 focus:border-brand-500 focus:ring-1 focus:ring-brand-500/20 outline-none"
                placeholder="placeholder text (optional)"
                value={f.placeholder ?? ""}
                onChange={(e) => update(i, { placeholder: e.target.value || null })}
              />
              <input
                className={`col-span-12 sm:col-span-4 rounded-md border px-2.5 py-1.5 text-xs font-mono placeholder-slate-400 focus:ring-1 focus:ring-brand-500/20 outline-none ${
                  regErr ? "border-red-400 bg-red-50 text-red-700" : "border-slate-300 text-slate-700 focus:border-brand-500"
                }`}
                placeholder="regex pattern (optional)"
                value={f.pattern ?? ""}
                onChange={(e) => update(i, { pattern: e.target.value || null })}
              />
              <input
                className="col-span-12 sm:col-span-4 rounded-md border border-slate-300 px-2.5 py-1.5 text-xs text-slate-700 placeholder-slate-400 focus:border-brand-500 focus:ring-1 focus:ring-brand-500/20 outline-none"
                placeholder={f.type === "select" ? "options: option1, option2" : "help text (optional)"}
                value={f.type === "select" ? (f.options ?? []).join(", ") : f.helpText ?? ""}
                onChange={(e) => {
                  if (f.type === "select") update(i, { options: parseOptions(e.target.value) });
                  else update(i, { helpText: e.target.value || null });
                }}
              />
            </div>
            {regErr && (
              <p className="text-[11px] font-medium text-red-600 flex items-center gap-1">
                <span>⚠ Invalid Regex:</span> {regErr}
              </p>
            )}
          </div>
        );
      })}

      <button
        type="button"
        onClick={add}
        className="inline-flex items-center gap-1 text-xs font-semibold text-brand-600 hover:text-brand-700 transition pt-1"
      >
        <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
        </svg>
        Add Field Definition
      </button>
    </fieldset>
  );
}
