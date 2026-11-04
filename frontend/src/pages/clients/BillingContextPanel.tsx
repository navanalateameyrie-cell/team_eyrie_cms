import { useCallback, useEffect, useState } from "react";
import { billingApi, type BillingContextDto } from "../../api/billing";
import { ApiError } from "../../api/client";
import { LoadingSpinner } from "../../components/ui";

interface Props {
  clientId: string;
  branchId: string;
}

export default function BillingContextPanel({ clientId, branchId }: Props) {
  const [data, setData] = useState<BillingContextDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [open, setOpen] = useState(false);

  const load = useCallback(async () => {
    if (!open) return;
    setLoading(true);
    setError(null);
    try {
      setData(await billingApi.forBranch(clientId, branchId));
    } catch (ex: unknown) {
      if (ex instanceof ApiError) setError(ex.message);
      else setError(ex instanceof Error ? ex.message : "Failed to load billing context");
    } finally {
      setLoading(false);
    }
  }, [clientId, branchId, open]);

  useEffect(() => {
    void load();
  }, [load]);

  return (
    <div className="mt-2.5 pt-2 border-t border-slate-100">
      <button
        type="button"
        className="inline-flex items-center gap-1.5 text-xs font-medium text-slate-500 hover:text-brand-600 transition-colors"
        onClick={() => setOpen((v) => !v)}
      >
        <svg
          className={`w-3.5 h-3.5 transition-transform duration-200 ${open ? "rotate-90" : ""}`}
          fill="none"
          stroke="currentColor"
          viewBox="0 0 24 24"
        >
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
        </svg>
        <span>{open ? "Hide Billing Profile" : "View Billing Profile & Place of Supply"}</span>
      </button>

      {open && (
        <div className="mt-2.5 text-xs animate-fadeIn">
          {loading && (
            <div className="py-2">
              <LoadingSpinner text="Fetching billing context…" size="sm" />
            </div>
          )}
          {error && <p className="text-xs text-red-600">{error}</p>}
          {!loading && !error && data && (
            <div className="rounded-xl border border-slate-200/80 bg-slate-50/70 p-3.5 space-y-3">
              <div className="flex items-center justify-between">
                <span className="font-semibold text-slate-800 text-xs">
                  Invoice Header Details
                </span>
                {!data.branchIsBillingEnabled && (
                  <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-medium bg-amber-50 text-amber-700 border border-amber-200">
                    Billing disabled on this branch
                  </span>
                )}
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 gap-3 text-xs">
                <div>
                  <span className="text-slate-400 block text-[10px] uppercase font-medium">Billed Legal Name</span>
                  <span className="font-medium text-slate-900 mt-0.5 block">{data.legalName}</span>
                </div>
                <div>
                  <span className="text-slate-400 block text-[10px] uppercase font-medium">Place of Supply State</span>
                  <span className="font-medium text-slate-900 mt-0.5 block">
                    {data.stateName ?? "—"} ({data.stateCode ?? "—"})
                  </span>
                </div>
                <div>
                  <span className="text-slate-400 block text-[10px] uppercase font-medium">GSTIN</span>
                  <span className="font-mono font-medium text-slate-900 mt-0.5 block">
                    {data.gstin ?? "Unregistered / None"}
                  </span>
                </div>
                <div>
                  <span className="text-slate-400 block text-[10px] uppercase font-medium">Income Tax PAN</span>
                  <span className="font-mono font-medium text-slate-900 mt-0.5 block">
                    {data.pan ?? "—"}
                  </span>
                </div>
              </div>

              <div className="pt-2 border-t border-slate-200/60">
                <div className="flex items-center justify-between mb-1.5">
                  <span className="text-[11px] font-medium text-slate-600">
                    Active Service Engagements Linked ({data.activeServiceFiles.length})
                  </span>
                </div>
                {data.activeServiceFiles.length === 0 ? (
                  <p className="text-[11px] text-slate-400 italic">No active service files tied directly to this branch.</p>
                ) : (
                  <div className="divide-y divide-slate-200/60 rounded-lg border border-slate-200/80 bg-white overflow-hidden">
                    {data.activeServiceFiles.map((f) => (
                      <div key={f.id} className="p-2 flex items-center justify-between text-xs">
                        <div className="flex items-center gap-2">
                          <span className="font-mono font-medium text-slate-700">{f.fileNumber}</span>
                          <span className="text-slate-400">·</span>
                          <span className="font-medium text-slate-800">{f.serviceName}</span>
                        </div>
                        <span className="text-[11px] text-slate-400">Opened {f.openedOn}</span>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
