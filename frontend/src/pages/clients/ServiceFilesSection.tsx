import { useCallback, useEffect, useState } from "react";
import { useAuth } from "../../auth/AuthContext";
import { ApiError } from "../../api/client";
import {
  serviceFilesApi,
  type CreateServiceFileBody,
  type ServiceFileDto,
  type ServiceFileStatus,
  type UpdateServiceFileBody,
} from "../../api/serviceFiles";
import ServiceFileFormDialog from "./ServiceFileFormDialog";
import EngagementsPanel from "./EngagementsPanel";
import StaffAssignmentsPanel from "./StaffAssignmentsPanel";
import ObligationsPanel from "./ObligationsPanel";
import DocumentsPanel from "../../components/DocumentsPanel";
import {
  Card,
  CardHeader,
  CardContent,
  Button,
  Badge,
  Select,
  LoadingSpinner,
  EmptyState,
} from "../../components/ui";

const STATUS_FILTERS: ("All" | ServiceFileStatus)[] = ["All", "Open", "OnHold", "Closed"];

export default function ServiceFilesSection({ clientId }: { clientId: string }) {
  const { hasPermission } = useAuth();
  const canWrite = hasPermission("clients.write");

  const [rows, setRows] = useState<ServiceFileDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [statusFilter, setStatusFilter] = useState<"All" | ServiceFileStatus>("All");
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<ServiceFileDto | null>(null);
  const [expanded, setExpanded] = useState<Set<string>>(new Set());
  const [error, setError] = useState<string | null>(null);

  function toggleExpanded(id: string) {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setRows(
        await serviceFilesApi.list(
          clientId,
          statusFilter === "All" ? {} : { status: statusFilter }
        )
      );
    } finally {
      setLoading(false);
    }
  }, [clientId, statusFilter]);

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
    <Card>
      <CardHeader
        title="Practice Service Files"
        description="Statutory engagements (Income Tax, GST Returns, TDS, Statutory Audit, ROC Filings)."
        action={
          <div className="flex items-center gap-3">
            <div className="flex items-center gap-2">
              <span className="text-xs text-slate-500 font-medium">Status:</span>
              <Select
                value={statusFilter}
                onChange={(e) => setStatusFilter(e.target.value as "All" | ServiceFileStatus)}
                className="w-auto py-1 text-xs"
              >
                {STATUS_FILTERS.map((s) => (
                  <option key={s} value={s}>
                    {s === "All" ? "All Files" : s}
                  </option>
                ))}
              </Select>
            </div>
            {canWrite && (
              <Button
                variant="primary"
                size="sm"
                onClick={() => setCreating(true)}
              >
                + New Service File
              </Button>
            )}
          </div>
        }
      />

      <CardContent className="space-y-4">
        {error && (
          <div className="rounded-lg bg-red-50 border border-red-200 p-3 text-xs text-red-700 flex items-start gap-2">
            <svg className="w-4 h-4 text-red-600 shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            <span>{error}</span>
          </div>
        )}

        {loading ? (
          <div className="py-8 flex justify-center">
            <LoadingSpinner text="Loading service files…" />
          </div>
        ) : rows.length === 0 ? (
          <EmptyState
            title="No service files found"
            description="Open a service file to begin tracking engagements, deliverables, deadlines, and staff assignments."
            action={
              canWrite ? (
                <Button variant="primary" size="sm" onClick={() => setCreating(true)}>
                  Open First Service File
                </Button>
              ) : undefined
            }
          />
        ) : (
          <div className="space-y-3">
            {rows.map((f) => {
              const isExpanded = expanded.has(f.id);
              return (
                <div
                  key={f.id}
                  className={`rounded-xl border transition-all ${
                    isExpanded
                      ? "border-brand-300 bg-white shadow-xs"
                      : "border-slate-200/90 bg-white hover:border-slate-300"
                  }`}
                >
                  <div className="p-4 flex flex-col md:flex-row md:items-start justify-between gap-3">
                    <div className="flex items-start gap-3">
                      <button
                        type="button"
                        onClick={() => toggleExpanded(f.id)}
                        aria-label={isExpanded ? "Collapse file details" : "Expand file details"}
                        className="mt-0.5 p-1 rounded-md text-slate-400 hover:text-slate-700 hover:bg-slate-100 transition-colors"
                      >
                        <svg
                          className={`w-4 h-4 transition-transform duration-200 ${isExpanded ? "rotate-90" : ""}`}
                          fill="none"
                          stroke="currentColor"
                          viewBox="0 0 24 24"
                        >
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
                        </svg>
                      </button>

                      <div className="space-y-1">
                        <div className="flex flex-wrap items-center gap-2">
                          <span className="font-mono text-xs font-semibold px-2 py-0.5 rounded bg-slate-100 text-slate-800 border border-slate-200">
                            {f.fileNumber}
                          </span>
                          <span className="font-semibold text-slate-900 text-sm">
                            {f.serviceName ?? f.serviceCode}
                          </span>
                          <Badge
                            variant={
                              f.status === "Open"
                                ? "success"
                                : f.status === "OnHold"
                                ? "warning"
                                : "neutral"
                            }
                            size="sm"
                          >
                            {f.status}
                            {f.closedOn ? ` · ${f.closedOn}` : ""}
                          </Badge>
                        </div>

                        <div className="text-xs text-slate-500 flex flex-wrap items-center gap-x-3 gap-y-1">
                          <span>Opened: <strong className="text-slate-700">{f.openedOn}</strong></span>
                          <span className="text-slate-300">|</span>
                          <span>
                            Scope: <strong className="text-slate-700">{f.branchCode ? `Branch ${f.branchCode}` : "Client-Wide"}</strong>
                          </span>
                          {f.legacyFileNumber && (
                            <>
                              <span className="text-slate-300">|</span>
                              <span>Legacy ID: <span className="font-mono text-slate-600">{f.legacyFileNumber}</span></span>
                            </>
                          )}
                        </div>

                        <div className="pt-1 flex flex-wrap gap-1.5 text-[11px]">
                          {f.clientPan && (
                            <span className="rounded-md bg-slate-100 border border-slate-200/80 text-slate-700 px-2 py-0.5 font-mono">
                              PAN · {f.clientPan}
                            </span>
                          )}
                          {f.gstin && (
                            <span className="rounded-md bg-emerald-50 border border-emerald-200/80 text-emerald-800 px-2 py-0.5 font-mono">
                              GSTIN · {f.gstin}
                            </span>
                          )}
                          {f.tan && (
                            <span className="rounded-md bg-brand-50 border border-brand-200/80 text-brand-800 px-2 py-0.5 font-mono">
                              TAN · {f.tan}
                            </span>
                          )}
                          {f.clientGroupName && (
                            <span className="rounded-md bg-amber-50 border border-amber-200/80 text-amber-800 px-2 py-0.5">
                              Group · {f.clientGroupName}
                            </span>
                          )}
                        </div>

                        {f.notes && (
                          <p className="text-xs text-slate-600 bg-slate-50 rounded p-2 mt-1 border border-slate-100 italic">
                            "{f.notes}"
                          </p>
                        )}
                      </div>
                    </div>

                    {canWrite && (
                      <div className="flex items-center gap-1.5 shrink-0 self-start">
                        {f.status !== "Closed" && (
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => setEditing(f)}
                          >
                            Edit
                          </Button>
                        )}
                        {f.status === "Open" && (
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => act(() => serviceFilesApi.putOnHold(clientId, f.id, f.version))}
                            className="text-amber-700 hover:bg-amber-50"
                          >
                            Hold
                          </Button>
                        )}
                        {f.status === "OnHold" && (
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => act(() => serviceFilesApi.resume(clientId, f.id, f.version))}
                            className="text-emerald-700 hover:bg-emerald-50"
                          >
                            Resume
                          </Button>
                        )}
                        {f.status !== "Closed" && (
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => {
                              if (confirm(`Close file ${f.fileNumber}? The client remains active.`))
                                void act(() => serviceFilesApi.close(clientId, f.id, f.version));
                            }}
                            className="text-red-600 hover:text-red-700 hover:bg-red-50"
                          >
                            Close
                          </Button>
                        )}
                        {f.status === "Closed" && (
                          <Button
                            variant="secondary"
                            size="sm"
                            onClick={() => act(() => serviceFilesApi.reopen(clientId, f.id, f.version))}
                          >
                            Reopen
                          </Button>
                        )}
                      </div>
                    )}
                  </div>

                  {isExpanded && (
                    <div className="border-t border-slate-200 bg-slate-50/50 p-4 space-y-4 rounded-b-xl animate-fadeIn">
                      <div className="bg-white rounded-xl border border-slate-200 p-4 shadow-2xs">
                        <h4 className="text-xs font-semibold text-slate-800 uppercase tracking-wider mb-2">
                          File Assigned Staff & Reviewers
                        </h4>
                        <StaffAssignmentsPanel clientId={clientId} fileId={f.id} />
                      </div>

                      <div className="bg-white rounded-xl border border-slate-200 p-4 shadow-2xs">
                        <EngagementsPanel clientId={clientId} fileId={f.id} fileIsClosed={f.status === "Closed"} />
                      </div>

                      <div className="bg-white rounded-xl border border-slate-200 p-4 shadow-2xs">
                        <ObligationsPanel fileId={f.id} />
                      </div>

                      <div className="bg-white rounded-xl border border-slate-200 p-4 shadow-2xs">
                        <DocumentsPanel ownerType="ServiceFile" ownerId={f.id} title="Service File Attachments & Workpapers" />
                      </div>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        )}
      </CardContent>

      {creating && (
        <ServiceFileFormDialog
          clientId={clientId}
          onSave={async (body) => {
            await serviceFilesApi.create(clientId, body as CreateServiceFileBody);
            setCreating(false);
            await load();
          }}
          onClose={() => setCreating(false)}
        />
      )}
      {editing && (
        <ServiceFileFormDialog
          clientId={clientId}
          initial={editing}
          onSave={async (body) => {
            await serviceFilesApi.update(clientId, editing.id, body as UpdateServiceFileBody);
            setEditing(null);
            await load();
          }}
          onClose={() => setEditing(null)}
        />
      )}
    </Card>
  );
}
