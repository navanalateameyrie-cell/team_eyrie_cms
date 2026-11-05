import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../../auth/AuthContext";
import { mastersApi, type StaffDto } from "../../api/masters";
import { ApiError } from "../../api/client";
import { MasterTable } from "../../components/MasterTable";
import { MasterFormDialog, TextField } from "../../components/MasterFormDialog";
import { Button } from "../../components/ui";

const empty = { fullName: "", email: "", code: "", phone: "", role: "" };

export default function StaffPage() {
  const { hasPermission } = useAuth();
  const canWrite = hasPermission("masters.write");

  const [rows, setRows] = useState<StaffDto[]>([]);
  const [search, setSearch] = useState("");
  const [showInactive, setShowInactive] = useState(false);
  const [loading, setLoading] = useState(false);
  const [listError, setListError] = useState<string | null>(null);
  const [editing, setEditing] = useState<StaffDto | null>(null);
  const [draft, setDraft] = useState(empty);
  const [formOpen, setFormOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setListError(null);
    try {
      setRows(await mastersApi.staff.list(search || undefined, showInactive ? undefined : true));
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
  function openEdit(r: StaffDto) {
    setEditing(r);
    setDraft({
      fullName: r.fullName,
      email: r.email,
      code: r.code,
      phone: r.phone ?? "",
      role: r.role ?? "",
    });
    setFormError(null);
    setFormOpen(true);
  }
  async function save() {
    setSaving(true);
    setFormError(null);
    try {
      const body = {
        fullName: draft.fullName,
        email: draft.email,
        code: draft.code || null,
        phone: draft.phone || null,
        role: draft.role || null,
      };
      if (editing) await mastersApi.staff.update(editing.id, body);
      else await mastersApi.staff.create(body);
      setFormOpen(false);
      await load();
    } catch (ex: unknown) {
      if (ex instanceof ApiError && ex.status === 409) setFormError("Email or code already in use.");
      else setFormError(ex instanceof Error ? ex.message : "Save failed");
    } finally {
      setSaving(false);
    }
  }
  async function deactivate(r: StaffDto) {
    if (!confirm(`Deactivate ${r.fullName}?`)) return;
    await mastersApi.staff.deactivate(r.id);
    await load();
  }

  return (
    <>
      <MasterTable<StaffDto>
        title="Staff Directory"
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
        extraActions={
          <div className="flex items-center gap-2">
            {hasPermission("users.manage") ? (
              <Link to="/admin/rbac">
                <Button variant="outline" size="sm">
                  User Accounts & Roles (RBAC) →
                </Button>
              </Link>
            ) : null}
            {hasPermission("staff.read") ? (
              <Link to="/admin/staff-workload">
                <Button variant="outline" size="sm">
                  View Workload Allocations →
                </Button>
              </Link>
            ) : null}
          </div>
        }
        columns={[
          { header: "Code", cell: (r) => <span className="font-mono">{r.code}</span> },
          { header: "Name", cell: (r) => r.fullName },
          { header: "Email", cell: (r) => r.email, className: "font-mono text-xs" },
          { header: "Role", cell: (r) => r.role ?? "—" },
          { header: "Phone", cell: (r) => r.phone ?? "—" },
        ]}
      />
      {formOpen && (
        <MasterFormDialog
          title={editing ? `Edit ${editing.fullName}` : "New staff"}
          onClose={() => setFormOpen(false)}
          onSubmit={save}
          busy={saving}
          error={formError}
        >
          <TextField label="Full name" required maxLength={200} value={draft.fullName}
            onChange={(v) => setDraft((d) => ({ ...d, fullName: v }))} />
          <TextField label="Email" required type="email" maxLength={256} value={draft.email}
            onChange={(v) => setDraft((d) => ({ ...d, email: v }))} />
          <TextField label="Code (optional)" maxLength={32} value={draft.code}
            onChange={(v) => setDraft((d) => ({ ...d, code: v.toUpperCase() }))} />
          <TextField label="Phone" maxLength={32} value={draft.phone}
            onChange={(v) => setDraft((d) => ({ ...d, phone: v }))} />
          <TextField label="Role" maxLength={100} value={draft.role}
            onChange={(v) => setDraft((d) => ({ ...d, role: v }))} />
        </MasterFormDialog>
      )}
    </>
  );
}
