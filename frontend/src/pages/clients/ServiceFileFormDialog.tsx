import { useEffect, useState } from "react";
import { mastersApi, type ServiceDto, type ServiceFieldDef, type ServiceSchemaDto } from "../../api/masters";
import { branchesApi, type BranchDto } from "../../api/branches";
import type { CreateServiceFileBody, ServiceFileDto, UpdateServiceFileBody } from "../../api/serviceFiles";
import { Modal, FormField, Input, Select, Textarea, Button } from "../../components/ui";

interface Props {
  clientId: string;
  initial?: ServiceFileDto;
  onSave: (body: CreateServiceFileBody | UpdateServiceFileBody) => Promise<void>;
  onClose: () => void;
}

function today() {
  return new Date().toISOString().slice(0, 10);
}

export default function ServiceFileFormDialog({ clientId, initial, onSave, onClose }: Props) {
  const [services, setServices] = useState<ServiceDto[]>([]);
  const [branches, setBranches] = useState<BranchDto[]>([]);
  const [schema, setSchema] = useState<ServiceSchemaDto | null>(null);
  const [schemaErr, setSchemaErr] = useState<string | null>(null);

  const [serviceId, setServiceId] = useState(initial?.serviceDefinitionId ?? "");
  const [branchId, setBranchId] = useState(initial?.branchId ?? "");
  const [legacy, setLegacy] = useState(initial?.legacyFileNumber ?? "");
  const [notes, setNotes] = useState(initial?.notes ?? "");
  const [openedOn, setOpenedOn] = useState(initial?.openedOn ?? today());
  const [meta, setMeta] = useState<Record<string, string>>(parseInitialMeta(initial?.metadataJson));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  useEffect(() => {
    void mastersApi.services.list(undefined, true).then(setServices);
    void branchesApi.list(clientId, true).then(setBranches);
  }, [clientId]);

  // Load the picked service's schema; branches dropdown may be required if the schema lists branchId.
  useEffect(() => {
    if (!serviceId) {
      setSchema(null);
      return;
    }
    setSchemaErr(null);
    mastersApi.services
      .schema(serviceId)
      .then((s) => setSchema(s))
      .catch((ex: Error) => setSchemaErr(ex.message));
  }, [serviceId]);

  const schemaFields = schema ? [...schema.required, ...schema.optional] : [];
  const needsBranch = schemaFields.some((f) => f.name === "branchId" && f.required);

  function setFieldValue(name: string, value: string) {
    setMeta((prev) => ({ ...prev, [name]: value }));
    setFieldErrors((prev) => {
      const n = { ...prev };
      delete n[name];
      return n;
    });
  }

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    setFieldErrors({});
    try {
      const metaOut: Record<string, string> = {};
      for (const f of schemaFields) {
        if (f.name === "branchId") continue;
        const v = (meta[f.name] ?? "").trim();
        if (v) metaOut[f.name] = v;
      }
      const metadataJson = Object.keys(metaOut).length > 0 ? JSON.stringify(metaOut) : null;

      if (initial) {
        const body: UpdateServiceFileBody = {
          serviceDefinitionId: serviceId,
          branchId: branchId || null,
          legacyFileNumber: legacy || null,
          notes: notes || null,
          version: initial.version,
          metadataJson,
        };
        await onSave(body);
      } else {
        const body: CreateServiceFileBody = {
          serviceDefinitionId: serviceId,
          branchId: branchId || null,
          openedOn,
          legacyFileNumber: legacy || null,
          notes: notes || null,
          metadataJson,
        };
        await onSave(body);
      }
    } catch (ex: unknown) {
      const msg = ex instanceof Error ? ex.message : "Save failed";
      try {
        const parsed = JSON.parse(msg.replace(/^API \d+: /, "")) as {
          error?: string;
          fieldErrors?: { fieldName: string; code: string; detail?: string }[];
        };
        if (parsed.fieldErrors) {
          const fe: Record<string, string> = {};
          for (const err of parsed.fieldErrors) {
            fe[err.fieldName] = err.code + (err.detail ? ` (${err.detail})` : "");
          }
          setFieldErrors(fe);
          setError("Please resolve the highlighted validation errors.");
          return;
        }
      } catch {
        // ignore
      }
      setError(msg);
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal
      open={true}
      onClose={onClose}
      title={initial ? `Edit Service File · ${initial.fileNumber}` : "Open New Service File"}
      size="lg"
    >
      <form onSubmit={submit} className="space-y-4">
        <FormField label="Service Practice Definition" required>
          <Select
            required
            value={serviceId}
            onChange={(e) => setServiceId(e.target.value)}
            disabled={services.length === 0}
          >
            <option value="">
              {services.length === 0
                ? "— No active services; create one under Masters → Services —"
                : "— Select Practice Service —"}
            </option>
            {services.map((s) => (
              <option key={s.id} value={s.id}>
                {s.code} · {s.name}
              </option>
            ))}
          </Select>
          {services.length === 0 && (
            <p className="mt-1 text-xs text-amber-600">
              No active services configured. Add service master definitions under Masters first.
            </p>
          )}
        </FormField>

        <FormField
          label={`Branch Establishment ${needsBranch ? "*" : "(Optional)"}`}
          hint={needsBranch ? "Required by selected service definition" : "Leave blank for client-wide services"}
        >
          <Select
            required={needsBranch}
            value={branchId}
            onChange={(e) => setBranchId(e.target.value)}
          >
            <option value="">— Client-Level Engagement (No Specific Branch) —</option>
            {branches.map((b) => (
              <option key={b.id} value={b.id}>
                {b.code} · {b.name} {b.isPrimary ? "(Primary)" : ""}
              </option>
            ))}
          </Select>
        </FormField>

        {schemaErr && (
          <p className="text-xs text-red-600 bg-red-50 p-2 rounded border border-red-200">
            Failed to load service schema: {schemaErr}
          </p>
        )}

        {/* Dynamic service-specific fields */}
        {schema && schemaFields.filter((f) => f.name !== "branchId").length > 0 && (
          <div className="rounded-xl border border-brand-200/60 bg-brand-50/20 p-4 space-y-3">
            <h4 className="text-xs font-semibold uppercase tracking-wider text-brand-900">
              {schema.name} · Compliance & Tax Attributes
            </h4>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              {schemaFields
                .filter((f) => f.name !== "branchId")
                .map((f) => (
                  <DynField
                    key={f.name}
                    def={f}
                    value={meta[f.name] ?? ""}
                    onChange={(v) => setFieldValue(f.name, v)}
                    error={fieldErrors[f.name]}
                  />
                ))}
            </div>
          </div>
        )}

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          {!initial && (
            <FormField label="File Opened Date" required>
              <Input
                type="date"
                required
                value={openedOn}
                onChange={(e) => setOpenedOn(e.target.value)}
              />
            </FormField>
          )}
          <FormField label="Legacy / Prior File Number" hint="Historical audit cross-reference">
            <Input
              maxLength={64}
              value={legacy}
              onChange={(e) => setLegacy(e.target.value)}
              placeholder="e.g. IT-2022-FILE-09"
              className="font-mono"
            />
          </FormField>
        </div>

        <FormField label="Internal Engagement Notes">
          <Textarea
            maxLength={2000}
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            rows={2}
            placeholder="Important filing instructions, client contact notes, or scope notes..."
          />
        </FormField>

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
            {busy ? "Saving File…" : initial ? "Save Changes" : "Open Service File"}
          </Button>
        </div>
      </form>
    </Modal>
  );
}

function parseInitialMeta(json: string | null | undefined): Record<string, string> {
  if (!json) return {};
  try {
    const o = JSON.parse(json) as Record<string, unknown>;
    const out: Record<string, string> = {};
    for (const [k, v] of Object.entries(o)) out[k] = v == null ? "" : String(v);
    return out;
  } catch {
    return {};
  }
}

function DynField({
  def,
  value,
  onChange,
  error,
}: {
  def: ServiceFieldDef;
  value: string;
  onChange: (v: string) => void;
  error?: string;
}) {
  return (
    <FormField
      label={def.label}
      required={def.required}
      hint={def.helpText}
      error={error}
    >
      {def.type === "select" ? (
        <Select
          required={def.required}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          error={!!error}
        >
          <option value="">— Select —</option>
          {(def.options ?? []).map((o) => (
            <option key={o} value={o}>
              {o}
            </option>
          ))}
        </Select>
      ) : def.type === "date" ? (
        <Input
          type="date"
          required={def.required}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          error={!!error}
        />
      ) : def.type === "number" ? (
        <Input
          type="number"
          required={def.required}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          placeholder={def.placeholder ?? ""}
          error={!!error}
        />
      ) : (
        <Input
          type="text"
          required={def.required}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          placeholder={def.placeholder ?? ""}
          pattern={def.pattern ?? undefined}
          error={!!error}
        />
      )}
    </FormField>
  );
}
