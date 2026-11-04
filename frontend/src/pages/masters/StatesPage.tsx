import { useCallback, useEffect, useState } from "react";
import { useAuth } from "../../auth/AuthContext";
import { mastersApi, type StateDto } from "../../api/masters";
import { ApiError } from "../../api/client";
import { MasterTable } from "../../components/MasterTable";
import { MasterFormDialog, TextField } from "../../components/MasterFormDialog";

const empty = { code: "", name: "", countryCode: "IN", displayOrder: 0 };

export default function StatesPage() {
  const { hasPermission } = useAuth();
  const canWrite = hasPermission("masters.write");

  const [rows, setRows] = useState<StateDto[]>([]);
  const [search, setSearch] = useState("");
  const [showInactive, setShowInactive] = useState(false);
  const [loading, setLoading] = useState(false);
  const [listError, setListError] = useState<string | null>(null);
  const [editing, setEditing] = useState<StateDto | null>(null);
  const [draft, setDraft] = useState(empty);
  const [formOpen, setFormOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setListError(null);
    try {
      setRows(await mastersApi.states.list(search || undefined, showInactive ? undefined : true));
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
  function openEdit(row: StateDto) {
    setEditing(row);
    setDraft({ code: row.code, name: row.name, countryCode: row.countryCode, displayOrder: row.displayOrder });
    setFormError(null);
    setFormOpen(true);
  }
  async function save() {
    setSaving(true);
    setFormError(null);
    try {
      const body = { ...draft, displayOrder: Number(draft.displayOrder) || 0 };
      if (editing) await mastersApi.states.update(editing.id, body);
      else await mastersApi.states.create(body);
      setFormOpen(false);
      await load();
    } catch (ex: unknown) {
      if (ex instanceof ApiError && ex.status === 409) setFormError("That code+country already exists.");
      else setFormError(ex instanceof Error ? ex.message : "Save failed");
    } finally {
      setSaving(false);
    }
  }
  async function deactivate(row: StateDto) {
    if (!confirm(`Deactivate ${row.name}?`)) return;
    await mastersApi.states.deactivate(row.id);
    await load();
  }

  return (
    <>
      <MasterTable<StateDto>
        title="States"
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
          { header: "Country", cell: (r) => <span className="font-mono">{r.countryCode}</span>, className: "w-24" },
          { header: "Code", cell: (r) => <span className="font-mono">{r.code}</span> },
          { header: "Name", cell: (r) => r.name },
          { header: "Order", cell: (r) => r.displayOrder, className: "text-right w-16" },
        ]}
      />
      {formOpen && (
        <MasterFormDialog
          title={editing ? `Edit ${editing.name}` : "New state"}
          onClose={() => setFormOpen(false)}
          onSubmit={save}
          busy={saving}
          error={formError}
        >
          <TextField label="Country code" required maxLength={8} value={draft.countryCode}
            onChange={(v) => setDraft((d) => ({ ...d, countryCode: v.toUpperCase() }))} />
          <TextField label="State code" required maxLength={16} value={draft.code}
            onChange={(v) => setDraft((d) => ({ ...d, code: v.toUpperCase() }))} />
          <TextField label="Name" required maxLength={200} value={draft.name}
            onChange={(v) => setDraft((d) => ({ ...d, name: v }))} />
          <TextField label="Display order" type="number" value={String(draft.displayOrder)}
            onChange={(v) => setDraft((d) => ({ ...d, displayOrder: Number(v) || 0 }))} />
        </MasterFormDialog>
      )}
    </>
  );
}
