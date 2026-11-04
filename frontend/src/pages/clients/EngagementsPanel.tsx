import { useCallback, useEffect, useState } from "react";
import { useAuth } from "../../auth/AuthContext";
import { ApiError } from "../../api/client";
import {
  engagementsApi,
  type CreateEngagementBody,
  type EngagementDto,
  type EngagementStatus,
  type UpdateEngagementBody,
} from "../../api/engagements";
import EngagementFormDialog from "./EngagementFormDialog";
import { Badge, Button, LoadingSpinner } from "../../components/ui";

const statusBadgeVariant: Record<EngagementStatus, "neutral" | "warning" | "success" | "danger"> = {
  Planned: "neutral",
  InProgress: "warning",
  Completed: "success",
  Cancelled: "danger",
};

interface Props {
  clientId: string;
  fileId: string;
  fileIsClosed: boolean;
}

export default function EngagementsPanel({ clientId, fileId, fileIsClosed }: Props) {
  const { hasPermission } = useAuth();
  const canWrite = hasPermission("clients.write");

  const [rows, setRows] = useState<EngagementDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<EngagementDto | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setRows(await engagementsApi.list(clientId, fileId));
    } finally {
      setLoading(false);
    }
  }, [clientId, fileId]);

  useEffect(() => {
    void load();
  }, [load]);

  async function act(fn: () => Promise<unknown>) {
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

  return (
    <div className="space-y-3">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2">
        <div>
          <h4 className="text-xs font-semibold uppercase tracking-wider text-slate-800">
            Annual & Periodic Engagements
          </h4>
          <p className="text-[11px] text-slate-500">
            Year-wise compliance mandates (e.g. FY 2024-25, Q1 TDS, Annual Tax Audit).
          </p>
        </div>
        {canWrite && !fileIsClosed && (
          <Button
            variant="outline"
            size="sm"
            onClick={() => setCreating(true)}
          >
            + Add Engagement
          </Button>
        )}
      </div>

      {error && (
        <div className="rounded-lg bg-red-50 border border-red-200 p-2 text-xs text-red-700">
          {error}
        </div>
      )}

      {loading ? (
        <div className="py-3">
          <LoadingSpinner text="Loading engagements…" size="sm" />
        </div>
      ) : rows.length === 0 ? (
        <p className="text-xs text-slate-400 italic py-2">
          No periodic engagements scheduled yet for this file.
        </p>
      ) : (
        <div className="overflow-x-auto rounded-lg border border-slate-200/80">
          <table className="min-w-full divide-y divide-slate-200 text-xs">
            <thead className="bg-slate-50 font-medium text-slate-500">
              <tr>
                <th className="text-left px-3 py-2">FY</th>
                <th className="text-left px-3 py-2">AY</th>
                <th className="text-left px-3 py-2">Cadence / Period</th>
                <th className="text-left px-3 py-2">Target Timeline</th>
                <th className="text-left px-3 py-2">Execution Dates</th>
                <th className="text-left px-3 py-2">Status</th>
                {canWrite && <th className="text-right px-3 py-2">Actions</th>}
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 bg-white">
              {rows.map((e) => (
                <tr key={e.id} className="hover:bg-slate-50/50 transition-colors">
                  <td className="px-3 py-2 font-mono font-semibold text-slate-800">
                    {e.financialYear}
                  </td>
                  <td className="px-3 py-2 font-mono text-slate-500">
                    {e.assessmentYear ?? "—"}
                  </td>
                  <td className="px-3 py-2 text-slate-700">
                    {e.periodType}
                    {e.periodLabel ? ` · ${e.periodLabel}` : ""}
                  </td>
                  <td className="px-3 py-2 text-slate-600 font-mono text-[11px]">
                    {e.plannedStartDate} → {e.plannedEndDate}
                  </td>
                  <td className="px-3 py-2 text-slate-600 font-mono text-[11px]">
                    {e.actualStartDate ?? "—"}
                    {e.actualEndDate ? ` → ${e.actualEndDate}` : ""}
                  </td>
                  <td className="px-3 py-2">
                    <Badge variant={statusBadgeVariant[e.status]} size="sm">
                      {e.status}
                    </Badge>
                  </td>
                  {canWrite && (
                    <td className="px-3 py-2 text-right whitespace-nowrap">
                      <div className="flex items-center justify-end gap-1.5">
                        {e.status === "Planned" && (
                          <>
                            <button
                              type="button"
                              onClick={() => setEditing(e)}
                              className="text-xs text-brand-600 hover:text-brand-800 font-medium"
                            >
                              Edit
                            </button>
                            <span className="text-slate-300">|</span>
                            <button
                              type="button"
                              onClick={() => act(() => engagementsApi.start(clientId, fileId, e.id, e.version))}
                              className="text-xs text-emerald-600 hover:text-emerald-800 font-medium"
                            >
                              Start
                            </button>
                            <span className="text-slate-300">|</span>
                            <button
                              type="button"
                              onClick={() => act(() => engagementsApi.cancel(clientId, fileId, e.id, e.version))}
                              className="text-xs text-red-600 hover:text-red-800 font-medium"
                            >
                              Cancel
                            </button>
                          </>
                        )}
                        {e.status === "InProgress" && (
                          <>
                            <button
                              type="button"
                              onClick={() => setEditing(e)}
                              className="text-xs text-brand-600 hover:text-brand-800 font-medium"
                            >
                              Edit
                            </button>
                            <span className="text-slate-300">|</span>
                            <button
                              type="button"
                              onClick={() => act(() => engagementsApi.complete(clientId, fileId, e.id, e.version))}
                              className="text-xs text-emerald-600 hover:text-emerald-800 font-medium"
                            >
                              Complete
                            </button>
                            <span className="text-slate-300">|</span>
                            <button
                              type="button"
                              onClick={() => act(() => engagementsApi.cancel(clientId, fileId, e.id, e.version))}
                              className="text-xs text-red-600 hover:text-red-800 font-medium"
                            >
                              Cancel
                            </button>
                          </>
                        )}
                      </div>
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {creating && (
        <EngagementFormDialog
          onSave={async (body) => {
            await engagementsApi.create(clientId, fileId, body as CreateEngagementBody);
            setCreating(false);
            await load();
          }}
          onClose={() => setCreating(false)}
        />
      )}
      {editing && (
        <EngagementFormDialog
          initial={editing}
          onSave={async (body) => {
            await engagementsApi.update(clientId, fileId, editing.id, body as UpdateEngagementBody);
            setEditing(null);
            await load();
          }}
          onClose={() => setEditing(null)}
        />
      )}
    </div>
  );
}
