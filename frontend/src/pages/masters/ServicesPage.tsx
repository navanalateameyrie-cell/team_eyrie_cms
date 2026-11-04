import { useCallback, useEffect, useState } from "react";
import { useAuth } from "../../auth/AuthContext";
import { mastersApi, type ServiceDto, type ServiceFieldDef } from "../../api/masters";
import { ApiError } from "../../api/client";
import { MasterTable } from "../../components/MasterTable";
import { MasterFormDialog, TextField } from "../../components/MasterFormDialog";
import ServiceSchemaEditor from "./ServiceSchemaEditor";

interface Draft {
  code: string;
  name: string;
  category: string;
  description: string;
  displayOrder: number;
  required: ServiceFieldDef[];
  optional: ServiceFieldDef[];
}
const empty: Draft = { code: "", name: "", category: "", description: "", displayOrder: 0, required: [], optional: [] };

function parseFields(json: string | null | undefined): ServiceFieldDef[] {
  if (!json) return [];
  try { return JSON.parse(json) as ServiceFieldDef[]; } catch { return []; }
}

export default function ServicesPage() {
  const { hasPermission } = useAuth();
  const canWrite = hasPermission("masters.write");

  const [rows, setRows] = useState<ServiceDto[]>([]);
  const [search, setSearch] = useState("");
  const [showInactive, setShowInactive] = useState(false);
  const [loading, setLoading] = useState(false);
  const [listError, setListError] = useState<string | null>(null);
  const [editing, setEditing] = useState<ServiceDto | null>(null);
  const [draft, setDraft] = useState(empty);
  const [formOpen, setFormOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setListError(null);
    try {
      setRows(await mastersApi.services.list(search || undefined, showInactive ? undefined : true));
    } catch (ex: unknown) {
      setListError(ex instanceof Error ? ex.message : "Failed to load");
    } finally {
      setLoading(false);
    }
  }, [search, showInactive]);
  useEffect(() => void load(), [load]);

  function openAdd() {
    setEditing(null);
    setDraft(empty);
    setFormError(null);
    setFormOpen(true);
  }
  function openEdit(r: ServiceDto) {
    setEditing(r);
    setDraft({
      code: r.code,
      name: r.name,
      category: r.category ?? "",
      description: r.description ?? "",
      displayOrder: r.displayOrder,
      required: parseFields(r.requiredFieldsJson),
      optional: parseFields(r.optionalFieldsJson),
    });
    setFormError(null);
    setFormOpen(true);
  }
  async function save() {
    setSaving(true);
    setFormError(null);
    try {
      // Fields without a name are placeholders — drop them.
      const requiredClean = draft.required.filter((f) => f.name && f.label).map((f) => ({ ...f, required: true }));
      const optionalClean = draft.optional.filter((f) => f.name && f.label).map((f) => ({ ...f, required: false }));

      const body = {
        code: draft.code,
        name: draft.name,
        category: draft.category || null,
        description: draft.description || null,
        displayOrder: Number(draft.displayOrder) || 0,
        requiredFieldsJson: requiredClean.length > 0 ? JSON.stringify(requiredClean) : null,
        optionalFieldsJson: optionalClean.length > 0 ? JSON.stringify(optionalClean) : null,
      };
      if (editing) await mastersApi.services.update(editing.id, body);
      else await mastersApi.services.create(body);
      setFormOpen(false);
      await load();
    } catch (ex: unknown) {
      if (ex instanceof ApiError && ex.status === 409) setFormError("That code is already in use.");
      else setFormError(ex instanceof Error ? ex.message : "Save failed");
    } finally {
      setSaving(false);
    }
  }
  async function deactivate(r: ServiceDto) {
    if (!confirm(`Deactivate ${r.name}?`)) return;
    await mastersApi.services.deactivate(r.id);
    await load();
  }

  return (
    <>
      <p className="text-sm text-slate-500 mb-3">
        Services are configurable master records. They never become fixed columns on Client; clients link to services
        in a later phase via a separate join entity.
      </p>
      <MasterTable<ServiceDto>
        title="Services"
        rows={rows}
        canWrite={canWrite}
        loading={loading}
        error={listError}
        search={search}
        onSearch={setSearch}
        showInactive={showInactive}
        onToggleShowInactive={setShowInactive}
        onAdd={openAdd}
        onEdit={openEdit}
        onDeactivate={deactivate}
        columns={[
          { header: "Code", cell: (r) => <span className="font-mono">{r.code}</span> },
          { header: "Name", cell: (r) => r.name },
          { header: "Category", cell: (r) => r.category ?? "—" },
          { header: "Order", cell: (r) => r.displayOrder, className: "text-right w-16" },
        ]}
      />
      {formOpen && (
        <MasterFormDialog
          title={editing ? `Edit ${editing.name}` : "New service"}
          onClose={() => setFormOpen(false)}
          onSubmit={save}
          busy={saving}
          error={formError}
        >
          <TextField label="Code" required maxLength={32} value={draft.code}
            onChange={(v) => setDraft((d) => ({ ...d, code: v.toUpperCase() }))} />
          <TextField label="Name" required maxLength={200} value={draft.name}
            onChange={(v) => setDraft((d) => ({ ...d, name: v }))} />
          <TextField label="Category" maxLength={100} value={draft.category}
            onChange={(v) => setDraft((d) => ({ ...d, category: v }))} />
          <TextField label="Description" maxLength={1000} value={draft.description}
            onChange={(v) => setDraft((d) => ({ ...d, description: v }))} />
          <TextField label="Display order" type="number" value={String(draft.displayOrder)}
            onChange={(v) => setDraft((d) => ({ ...d, displayOrder: Number(v) || 0 }))} />

          <div className="pt-2">
            <p className="text-xs text-slate-500 mb-2">
              <strong>Field schema</strong> — defines what the Service File form prompts for when
              someone opens a file under this service. Common types: <code>string</code>, <code>date</code>,
              <code>number</code>, <code>select</code>. Use <code>branchId</code> as a required name
              to force the UI's Branch dropdown.
            </p>
            <ServiceSchemaEditor
              title="Required fields"
              fields={draft.required}
              defaultRequired={true}
              onChange={(next) => setDraft((d) => ({ ...d, required: next }))}
            />
            <div className="h-2" />
            <ServiceSchemaEditor
              title="Optional fields"
              fields={draft.optional}
              defaultRequired={false}
              onChange={(next) => setDraft((d) => ({ ...d, optional: next }))}
            />
          </div>
        </MasterFormDialog>
      )}
    </>
  );
}
