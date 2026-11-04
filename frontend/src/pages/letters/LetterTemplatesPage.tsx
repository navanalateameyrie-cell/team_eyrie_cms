import { useEffect, useState } from "react";
import { apiFetch } from "../../api/client";
import { useAuth } from "../../auth/AuthContext";
import {
  PageHeader,
  Card,
  Button,
  Badge,
  Modal,
  FormField,
  Input,
  Textarea,
  EmptyState,
} from "../../components/ui";

interface Template {
  id: string;
  name: string;
  body: string;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  version: number;
}

const KNOWN_TOKENS = [
  "client.name",
  "client.tradeName",
  "client.pan",
  "client.gstin",
  "client.code",
  "service.code",
  "service.name",
  "file.number",
  "obligation.periodKey",
  "obligation.dueDate",
  "obligation.status",
  "firm.name",
  "firm.gstin",
  "firm.pan",
  "today",
];

export default function LetterTemplatesPage() {
  const { hasPermission } = useAuth();
  const canWrite = hasPermission("letters.write");
  const [rows, setRows] = useState<Template[]>([]);
  const [editing, setEditing] = useState<Template | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [refreshKey, setRefreshKey] = useState(0);

  useEffect(() => {
    apiFetch<Template[]>("/api/v1/letter-templates?activeOnly=false")
      .then(setRows)
      .catch((e) => setError(String(e)));
  }, [refreshKey]);

  async function save(name: string, body: string) {
    try {
      if (editing && editing.id) {
        await apiFetch(`/api/v1/letter-templates/${editing.id}`, {
          method: "PUT",
          body: JSON.stringify({ name, body }),
        });
      } else {
        await apiFetch("/api/v1/letter-templates", {
          method: "POST",
          body: JSON.stringify({ name, body }),
        });
      }
      setEditing(null);
      setRefreshKey((n) => n + 1);
    } catch (e) {
      setError(String(e));
    }
  }

  async function toggleActive(t: Template) {
    try {
      await apiFetch(`/api/v1/letter-templates/${t.id}/active?value=${!t.isActive}`, {
        method: "POST",
      });
      setRefreshKey((n) => n + 1);
    } catch (e) {
      setError(String(e));
    }
  }

  async function remove(t: Template) {
    if (!confirm(`Delete template "${t.name}"?`)) return;
    try {
      await apiFetch(`/api/v1/letter-templates/${t.id}`, { method: "DELETE" });
      setRefreshKey((n) => n + 1);
    } catch (e) {
      setError(String(e));
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        breadcrumbs={[
          { label: "Settings", to: "/admin/settings" },
          { label: "Letter Templates" },
        ]}
        title="Communication & Compliance Letter Templates"
        subtitle="Automated PDF mail-merge letters for tax intimations, filing confirmations, and representation."
        actions={
          canWrite ? (
            <Button
              variant="primary"
              size="sm"
              onClick={() =>
                setEditing({
                  id: "",
                  name: "",
                  body: "",
                  isActive: true,
                  createdAtUtc: "",
                  updatedAtUtc: null,
                  version: 0,
                })
              }
            >
              + New Letter Template
            </Button>
          ) : undefined
        }
      />

      {error && (
        <div className="rounded-lg bg-red-50 border border-red-200 p-3 text-xs text-red-700">
          {error}
        </div>
      )}

      <Card>
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-200 text-xs">
            <thead className="bg-slate-50 font-medium text-slate-500">
              <tr>
                <th className="text-left px-4 py-3">Template Name</th>
                <th className="text-left px-4 py-3">Body Preview</th>
                <th className="text-left px-4 py-3">Last Modified</th>
                <th className="text-right px-4 py-3">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 bg-white">
              {rows.map((t) => (
                <tr key={t.id} className="hover:bg-slate-50/50 transition-colors">
                  <td className="px-4 py-3">
                    <div className="font-semibold text-slate-900">{t.name}</div>
                    <div className="mt-0.5">
                      <Badge tone={t.isActive ? "success" : "neutral"} size="sm">
                        {t.isActive ? "Active" : "Inactive"}
                      </Badge>
                    </div>
                  </td>

                  <td className="px-4 py-3 text-slate-600 max-w-md truncate font-mono text-[11px]">
                    {t.body.slice(0, 120)}…
                  </td>

                  <td className="px-4 py-3 text-slate-500 whitespace-nowrap">
                    {t.updatedAtUtc ? new Date(t.updatedAtUtc).toLocaleDateString() : "—"}
                  </td>

                  <td className="px-4 py-3 text-right whitespace-nowrap">
                    {canWrite && (
                      <div className="flex items-center justify-end gap-1.5">
                        <Button
                          variant="outline"
                          size="xs"
                          onClick={() => setEditing(t)}
                        >
                          Edit
                        </Button>
                        <Button
                          variant="ghost"
                          size="xs"
                          onClick={() => toggleActive(t)}
                        >
                          {t.isActive ? "Deactivate" : "Activate"}
                        </Button>
                        <Button
                          variant="ghost"
                          size="xs"
                          onClick={() => remove(t)}
                          className="text-rose-600 hover:text-rose-700 hover:bg-rose-50"
                        >
                          Delete
                        </Button>
                      </div>
                    )}
                  </td>
                </tr>
              ))}
              {rows.length === 0 && (
                <tr>
                  <td colSpan={4} className="py-12">
                    <EmptyState
                      title="No letter templates found"
                      description="Create customized letter templates with merge tokens like {{client.name}} and {{obligation.dueDate}}."
                    />
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </Card>

      {editing && (
        <EditDialog
          initial={editing}
          onSave={save}
          onCancel={() => setEditing(null)}
        />
      )}
    </div>
  );
}

function EditDialog({
  initial,
  onSave,
  onCancel,
}: {
  initial: Template;
  onSave: (name: string, body: string) => void;
  onCancel: () => void;
}) {
  const [name, setName] = useState(initial.name);
  const [body, setBody] = useState(initial.body);

  function insertToken(tok: string) {
    setBody((b) => b + `{{${tok}}}`);
  }

  return (
    <Modal
      open={true}
      onClose={onCancel}
      title={initial.id ? `Edit Template · ${initial.name}` : "Create Letter Template"}
      size="2xl"
    >
      <form
        onSubmit={(e) => {
          e.preventDefault();
          onSave(name, body);
        }}
        className="space-y-4"
      >
        <FormField label="Template Title" required>
          <Input
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
            placeholder="e.g. GST Filing Confirmation & Acknowledgement"
          />
        </FormField>

        <FormField
          label="Letter Body (Plain Text)"
          required
          hint="Supports merge tags. Click any tag below to insert."
        >
          <Textarea
            value={body}
            onChange={(e) => setBody(e.target.value)}
            required
            rows={12}
            className="font-mono text-xs"
          />
        </FormField>

        <div className="rounded-xl border border-slate-200 bg-slate-50/70 p-3 space-y-2">
          <p className="text-[11px] font-semibold uppercase tracking-wider text-slate-500">
            Available Merge Tokens (Click to append):
          </p>
          <div className="flex flex-wrap gap-1.5">
            {KNOWN_TOKENS.map((t) => (
              <button
                type="button"
                key={t}
                onClick={() => insertToken(t)}
                className="font-mono text-[11px] bg-white border border-slate-200 text-slate-700 hover:text-brand-600 hover:border-brand-300 px-2 py-0.5 rounded transition-colors"
              >
                {`{{${t}}}`}
              </button>
            ))}
          </div>
        </div>

        <div className="flex justify-end gap-2 pt-3 border-t border-slate-200">
          <Button type="button" variant="outline" onClick={onCancel}>
            Cancel
          </Button>
          <Button type="submit" variant="primary">
            Save Template
          </Button>
        </div>
      </form>
    </Modal>
  );
}
