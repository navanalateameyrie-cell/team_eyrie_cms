import { useEffect, useState } from "react";
import { apiFetch } from "../../api/client";
import { useAuth } from "../../auth/AuthContext";
import { Badge, Button, LoadingSpinner } from "../../components/ui";

interface Obligation {
  id: string;
  periodKey: string;
  periodStart: string;
  periodEnd: string;
  dueDate: string;
  status: number | string;
  assignedStaffName: string | null;
  filedOn: string | null;
  reference: string | null;
}

const STATUS: Record<number, { label: string; variant: "neutral" | "warning" | "success" | "danger" }> = {
  1: { label: "Pending", variant: "neutral" },
  2: { label: "In Progress", variant: "warning" },
  3: { label: "Filed", variant: "success" },
  4: { label: "Delayed", variant: "danger" },
  5: { label: "Waived", variant: "neutral" },
};

const asNum = (s: number | string): number =>
  typeof s === "number"
    ? s
    : ({ Pending: 1, InProgress: 2, Filed: 3, Delayed: 4, Waived: 5 } as Record<string, number>)[s] ?? 0;

export default function ObligationsPanel({ fileId }: { fileId: string }) {
  const { hasPermission } = useAuth();
  const canRead = hasPermission("compliance.read");
  const canWrite = hasPermission("compliance.write");
  const [rows, setRows] = useState<Obligation[] | null>(null);
  const [refresh, setRefresh] = useState(0);

  useEffect(() => {
    if (!canRead) return;
    apiFetch<Obligation[]>(`/api/v1/service-files/${fileId}/obligations`)
      .then(setRows)
      .catch(() => setRows([]));
  }, [fileId, refresh, canRead]);

  if (!canRead) return null;
  if (rows === null) {
    return (
      <div className="py-3">
        <LoadingSpinner text="Loading statutory obligations…" size="sm" />
      </div>
    );
  }

  if (rows.length === 0) {
    return (
      <div className="rounded-xl border border-dashed border-slate-200 bg-slate-50/50 p-4 text-center">
        <p className="text-xs text-slate-500 font-medium">No statutory compliance obligations generated yet.</p>
        <p className="text-[11px] text-slate-400 mt-0.5">
          Configure recurring filing cadence under Service Master, then execute obligation generation in Compliance.
        </p>
      </div>
    );
  }

  async function markFiled(id: string) {
    const filedOn = window.prompt("Statutory Filing Date (YYYY-MM-DD):", new Date().toISOString().slice(0, 10));
    if (!filedOn) return;
    const reference = window.prompt("Filing Acknowledgment / ARN / Challan Reference:", "") || null;
    try {
      await apiFetch(`/api/v1/compliance/${id}/file`, {
        method: "POST",
        body: JSON.stringify({ filedOn, reference }),
      });
      setRefresh((n) => n + 1);
    } catch (e) {
      alert(String(e));
    }
  }

  return (
    <div className="space-y-3">
      <div>
        <h4 className="text-xs font-semibold uppercase tracking-wider text-slate-800">
          Statutory Filing Obligations & Due Dates
        </h4>
        <p className="text-[11px] text-slate-500">
          Statutory deadlines (GSTR-3B, GSTR-1, Form 26Q, Advance Tax, ITR).
        </p>
      </div>

      <div className="overflow-x-auto rounded-lg border border-slate-200/80">
        <table className="min-w-full divide-y divide-slate-200 text-xs">
          <thead className="bg-slate-50 font-medium text-slate-500">
            <tr>
              <th className="text-left px-3 py-2">Period</th>
              <th className="text-left px-3 py-2">Statutory Due Date</th>
              <th className="text-left px-3 py-2">Status</th>
              <th className="text-left px-3 py-2">Filing Evidence / ARN</th>
              {canWrite && <th className="text-right px-3 py-2">Action</th>}
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100 bg-white">
            {rows.map((o) => {
              const s = asNum(o.status);
              const open = s !== 3 && s !== 5;
              const statusCfg = STATUS[s] ?? { label: "Unknown", variant: "neutral" };
              return (
                <tr key={o.id} className="hover:bg-slate-50/50 transition-colors">
                  <td className="px-3 py-2 font-mono font-semibold text-slate-800">
                    {o.periodKey}
                  </td>
                  <td className="px-3 py-2 font-mono text-slate-700 font-medium">
                    {o.dueDate}
                  </td>
                  <td className="px-3 py-2">
                    <Badge variant={statusCfg.variant} size="sm">
                      {statusCfg.label}
                    </Badge>
                  </td>
                  <td className="px-3 py-2">
                    {o.filedOn ? (
                      <div className="text-slate-700">
                        <span className="font-medium font-mono">{o.filedOn}</span>
                        {o.reference && (
                          <span className="font-mono text-slate-500 ml-1.5 bg-slate-100 px-1.5 py-0.5 rounded text-[11px]">
                            {o.reference}
                          </span>
                        )}
                      </div>
                    ) : (
                      <span className="text-slate-400 italic">Pending Filing</span>
                    )}
                  </td>
                  {canWrite && (
                    <td className="px-3 py-2 text-right">
                      {open && (
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => markFiled(o.id)}
                          className="text-emerald-700 border-emerald-300 hover:bg-emerald-50 py-0.5 text-xs"
                        >
                          Mark Filed
                        </Button>
                      )}
                    </td>
                  )}
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
