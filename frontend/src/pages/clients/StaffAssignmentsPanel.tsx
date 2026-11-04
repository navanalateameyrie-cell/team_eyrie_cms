import { useCallback, useEffect, useMemo, useState } from "react";
import { useAuth } from "../../auth/AuthContext";
import { ApiError } from "../../api/client";
import {
  clientAssignmentsApi,
  fileAssignmentsApi,
  type AssignBody,
  type AssignmentDto,
  type AssignmentRole,
} from "../../api/assignments";
import { mastersApi, type StaffDto } from "../../api/masters";
import { Button, Input, Select, Badge, LoadingSpinner } from "../../components/ui";

const ROLES: AssignmentRole[] = ["Primary", "Secondary", "Reviewer"];

interface Props {
  clientId: string;
  fileId?: string; // if present → file-scope; else → client-scope
}

function today() {
  return new Date().toISOString().slice(0, 10);
}

export default function StaffAssignmentsPanel({ clientId, fileId }: Props) {
  const { hasPermission } = useAuth();
  const canWrite = hasPermission("clients.write");

  const [rows, setRows] = useState<AssignmentDto[]>([]);
  const [staff, setStaff] = useState<StaffDto[]>([]);
  const [showHistory, setShowHistory] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [draftStaff, setDraftStaff] = useState("");
  const [draftRole, setDraftRole] = useState<AssignmentRole>("Primary");
  const [draftFrom, setDraftFrom] = useState(today());
  const [draftNotes, setDraftNotes] = useState("");
  const [adding, setAdding] = useState(false);
  const [saving, setSaving] = useState(false);

  const scopeLabel = fileId ? "Service File" : "Client Practice";

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const list = fileId
        ? await fileAssignmentsApi.list(clientId, fileId, { includeHistory: showHistory })
        : await clientAssignmentsApi.list(clientId, { includeHistory: showHistory });
      setRows(list);
    } catch (ex: unknown) {
      setError(ex instanceof Error ? ex.message : "Failed to load staff assignments");
    } finally {
      setLoading(false);
    }
  }, [clientId, fileId, showHistory]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    void mastersApi.staff.list(undefined, true).then(setStaff);
  }, []);

  async function act<T>(fn: () => Promise<T>) {
    setError(null);
    try {
      await fn();
      await load();
    } catch (ex: unknown) {
      if (ex instanceof ApiError) {
        try {
          const parsed = JSON.parse(ex.body) as { error?: string; detail?: string };
          setError(parsed.detail ?? parsed.error ?? ex.message);
        } catch {
          setError(ex.message);
        }
      } else {
        setError(ex instanceof Error ? ex.message : "Request failed");
      }
    }
  }

  async function submitAssign(e: React.FormEvent) {
    e.preventDefault();
    if (!draftStaff) return;
    setSaving(true);
    const body: AssignBody = {
      staffId: draftStaff,
      role: draftRole,
      effectiveFrom: draftFrom,
      notes: draftNotes.trim() || null,
    };
    try {
      await act(() =>
        fileId
          ? fileAssignmentsApi.assign(clientId, fileId, body)
          : clientAssignmentsApi.assign(clientId, body)
      );
      setDraftStaff("");
      setDraftNotes("");
      setAdding(false);
    } finally {
      setSaving(false);
    }
  }

  async function end(a: AssignmentDto) {
    if (!confirm(`End ${a.role} assignment for ${a.staffFullName ?? a.staffCode}?`)) return;
    await act(() =>
      fileId
        ? fileAssignmentsApi.end(clientId, fileId, a.id, a.version)
        : clientAssignmentsApi.end(clientId, a.id, a.version)
    );
  }

  const groupedByRole = useMemo(() => {
    const g: Record<AssignmentRole, AssignmentDto[]> = {
      Primary: [],
      Secondary: [],
      Reviewer: [],
    };
    for (const r of rows) g[r.role].push(r);
    return g;
  }, [rows]);

  return (
    <div className={fileId ? "space-y-3" : "space-y-4"}>
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2">
        <div>
          <h4 className="text-sm font-semibold text-slate-900">
            {scopeLabel} Designated Team
          </h4>
          <p className="text-xs text-slate-500">
            Audit-tracked staffing assignments. Reassignment archives previous terms with date stamps.
          </p>
        </div>
        <div className="flex items-center gap-3 text-xs">
          <label className="flex items-center gap-1.5 text-slate-500 cursor-pointer">
            <input
              type="checkbox"
              checked={showHistory}
              onChange={(e) => setShowHistory(e.target.checked)}
              className="rounded border-slate-300 text-brand-600 focus:ring-brand-500"
            />
            <span>Show assignment history</span>
          </label>
          {canWrite && !adding && (
            <Button
              variant="outline"
              size="sm"
              onClick={() => setAdding(true)}
            >
              + Assign Staff
            </Button>
          )}
        </div>
      </div>

      {error && (
        <div className="rounded-lg bg-red-50 border border-red-200 p-2.5 text-xs text-red-700">
          {error}
        </div>
      )}

      {loading ? (
        <div className="py-4">
          <LoadingSpinner text="Loading assignments…" size="sm" />
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
          {ROLES.map((role) => (
            <div
              key={role}
              className="rounded-xl border border-slate-200/90 bg-slate-50/50 p-3 flex flex-col justify-between"
            >
              <div>
                <div className="flex items-center justify-between mb-2">
                  <span className="text-[11px] font-semibold uppercase tracking-wider text-slate-500">
                    {role === "Primary"
                      ? "Lead / In-Charge"
                      : role === "Reviewer"
                      ? "Partner Reviewer"
                      : "Secondary / Article"}
                  </span>
                  <Badge
                    variant={
                      role === "Primary"
                        ? "primary"
                        : role === "Reviewer"
                        ? "purple"
                        : "neutral"
                    }
                    size="sm"
                  >
                    {role}
                  </Badge>
                </div>

                {groupedByRole[role].length === 0 ? (
                  <p className="text-xs text-slate-400 italic py-2">No staff assigned</p>
                ) : (
                  <div className="space-y-2">
                    {groupedByRole[role].map((a) => (
                      <div
                        key={a.id}
                        className={`rounded-lg border p-2 text-xs transition-colors ${
                          a.effectiveTo
                            ? "bg-slate-100/60 border-slate-200 text-slate-500"
                            : "bg-white border-slate-200 shadow-2xs text-slate-900"
                        }`}
                      >
                        <div className="flex items-center justify-between">
                          <div className="font-medium truncate">
                            {a.staffFullName}
                            <span className="font-mono text-[11px] text-slate-400 ml-1.5">
                              ({a.staffCode})
                            </span>
                          </div>
                          {canWrite && !a.effectiveTo && (
                            <button
                              type="button"
                              onClick={() => end(a)}
                              className="text-xs font-semibold text-red-600 hover:text-red-700 ml-2"
                            >
                              End
                            </button>
                          )}
                        </div>
                        <div className="text-[11px] text-slate-400 mt-1 flex items-center gap-1 font-mono">
                          <span>{a.effectiveFrom}</span>
                          <span>→</span>
                          <span>{a.effectiveTo ?? "Present"}</span>
                        </div>
                        {a.notes && (
                          <p className="text-[11px] text-slate-500 mt-1 italic border-t border-slate-100 pt-1">
                            {a.notes}
                          </p>
                        )}
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>
          ))}
        </div>
      )}

      {adding && (
        <form
          onSubmit={submitAssign}
          className="mt-3 rounded-xl border border-brand-200 bg-brand-50/20 p-4 space-y-3 animate-fadeIn"
        >
          <h5 className="text-xs font-semibold uppercase tracking-wider text-brand-900">
            Create New Staff Assignment
          </h5>
          <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 gap-3 text-xs">
            <div>
              <label className="block font-medium text-slate-700 mb-1">Team Member *</label>
              <Select
                required
                value={draftStaff}
                onChange={(e) => setDraftStaff(e.target.value)}
                className="py-1 text-xs"
              >
                <option value="">— Select Staff —</option>
                {staff.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.code} · {s.fullName}
                  </option>
                ))}
              </Select>
            </div>

            <div>
              <label className="block font-medium text-slate-700 mb-1">Assignment Role *</label>
              <Select
                value={draftRole}
                onChange={(e) => setDraftRole(e.target.value as AssignmentRole)}
                className="py-1 text-xs"
              >
                {ROLES.map((r) => (
                  <option key={r} value={r}>
                    {r}
                  </option>
                ))}
              </Select>
            </div>

            <div>
              <label className="block font-medium text-slate-700 mb-1">Effective Date *</label>
              <Input
                type="date"
                required
                value={draftFrom}
                onChange={(e) => setDraftFrom(e.target.value)}
                className="py-1 text-xs"
              />
            </div>

            <div>
              <label className="block font-medium text-slate-700 mb-1">Scope / Work Notes</label>
              <Input
                value={draftNotes}
                onChange={(e) => setDraftNotes(e.target.value)}
                placeholder="Optional assignment instructions..."
                className="py-1 text-xs"
              />
            </div>
          </div>

          <div className="flex justify-end gap-2 pt-2 border-t border-brand-100">
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => setAdding(false)}
            >
              Cancel
            </Button>
            <Button
              type="submit"
              variant="primary"
              size="sm"
              loading={saving}
            >
              {saving ? "Saving…" : "Confirm Assignment"}
            </Button>
          </div>
        </form>
      )}
    </div>
  );
}
