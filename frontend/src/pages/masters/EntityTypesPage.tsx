import { useCallback, useEffect, useState } from "react";
import { useAuth } from "../../auth/AuthContext";
import { mastersApi, type EntityTypeDto } from "../../api/masters";
import { ApiError } from "../../api/client";
import { MasterTable } from "../../components/MasterTable";
import { MasterFormDialog, TextField } from "../../components/MasterFormDialog";

const empty = { code: "", name: "", description: "", displayOrder: 0 };

export default function EntityTypesPage() {
  const { hasPermission } = useAuth();
  const canWrite = hasPermission("masters.write");

  const [rows, setRows] = useState<EntityTypeDto[]>([]);
  const [search, setSearch] = useState("");
  const [showInactive, setShowInactive] = useState(false);
  const [loading, setLoading] = useState(false);
  const [listError, setListError] = useState<string | null>(null);

  const [editing, setEditing] = useState<EntityTypeDto | null>(null);
  const [draft, setDraft] = useState(empty);
  const [formOpen, setFormOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setListError(null);
    try {
      const list = await mastersApi.entityTypes.list(search || undefined, showInactive ? undefined : true);
      setRows(list);
    } catch (ex: unknown) {
      setListError(ex instanceof Error ? ex.message : "Failed to load");
    } finally {
      setLoading(false);
    }
  }, [search, showInactive]);

  useEffect(() => {
    void load();
  }, [load]);

  function openAdd() {
    setEditing(null);
    setDraft(empty);
    setFormError(null);
    setFormOpen(true);
  }
  function openEdit(row: EntityTypeDto) {
    setEditing(row);
    setDraft({
      code: row.code,
      name: row.name,
      description: row.description ?? "",
      displayOrder: row.displayOrder,
    });
    setFormError(null);
    setFormOpen(true);
  }

  async function save() {
    setSaving(true);
    setFormError(null);
    try {
      const body = {
        code: draft.code,
        name: draft.name,
        description: draft.description || null,
        displayOrder: Number(draft.displayOrder) || 0,
      };
      if (editing) await mastersApi.entityTypes.update(editing.id, body);
      else await mastersApi.entityTypes.create(body);
      setFormOpen(false);
      await load();
    } catch (ex: unknown) {
      if (ex instanceof ApiError && ex.status === 409) {
        setFormError("That code is already in use.");
      } else setFormError(ex instanceof Error ? ex.message : "Save failed");
    } finally {
      setSaving(false);
    }
  }

  async function deactivate(row: EntityTypeDto) {
    if (!confirm(`Deactivate ${row.name}?`)) return;
    await mastersApi.entityTypes.deactivate(row.id);
    await load();
  }

  return (
    <>
      <MasterTable<EntityTypeDto>
        title="Entity Types"
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
          { header: "Description", cell: (r) => r.description ?? "—" },
          { header: "Order", cell: (r) => r.displayOrder, className: "text-right w-16" },
        ]}
      />
      {formOpen && (
        <MasterFormDialog
          title={editing ? `Edit ${editing.name}` : "New entity type"}
          onClose={() => setFormOpen(false)}
          onSubmit={save}
          busy={saving}
          error={formError}
        >
          <TextField
            label="Code"
            required
            maxLength={32}
            value={draft.code}
            onChange={(v) => setDraft((d) => ({ ...d, code: v }))}
          />
          <TextField
            label="Name"
            required
            maxLength={200}
            value={draft.name}
            onChange={(v) => setDraft((d) => ({ ...d, name: v }))}
          />
          <TextField
            label="Description"
            maxLength={1000}
            value={draft.description}
            onChange={(v) => setDraft((d) => ({ ...d, description: v }))}
          />
          <TextField
            label="Display order"
            type="number"
            value={String(draft.displayOrder)}
            onChange={(v) => setDraft((d) => ({ ...d, displayOrder: Number(v) || 0 }))}
          />
        </MasterFormDialog>
      )}
    </>
  );
}
