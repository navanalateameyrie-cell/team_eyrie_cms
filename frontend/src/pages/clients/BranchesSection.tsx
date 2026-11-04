import { useCallback, useEffect, useState } from "react";
import { useAuth } from "../../auth/AuthContext";
import {
  branchesApi,
  type BranchDto,
  type CreateBranchBody,
  type UpdateBranchBody,
} from "../../api/branches";
import { ApiError } from "../../api/client";
import BranchFormDialog from "./BranchFormDialog";
import TaxRegistrationsPanel from "./TaxRegistrationsPanel";
import BillingContextPanel from "./BillingContextPanel";
import { Card, CardHeader, CardContent, Button, Badge, LoadingSpinner, EmptyState } from "../../components/ui";

export default function BranchesSection({ clientId }: { clientId: string }) {
  const { hasPermission } = useAuth();
  const canWrite = hasPermission("clients.write");

  const [branches, setBranches] = useState<BranchDto[]>([]);
  const [showInactive, setShowInactive] = useState(false);
  const [loading, setLoading] = useState(true);
  const [editing, setEditing] = useState<BranchDto | null>(null);
  const [creating, setCreating] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setBranches(await branchesApi.list(clientId, showInactive ? undefined : true));
    } finally {
      setLoading(false);
    }
  }, [clientId, showInactive]);

  useEffect(() => {
    void load();
  }, [load]);

  async function create(body: CreateBranchBody) {
    await branchesApi.create(clientId, body);
    setCreating(false);
    await load();
  }

  async function update(body: UpdateBranchBody) {
    if (!editing) return;
    await branchesApi.update(clientId, editing.id, body);
    setEditing(null);
    await load();
  }

  async function makePrimary(b: BranchDto) {
    setActionError(null);
    try {
      await branchesApi.makePrimary(clientId, b.id, b.version);
      await load();
    } catch (ex: unknown) {
      setActionError(apiMessage(ex));
    }
  }

  async function close(b: BranchDto) {
    if (!confirm(`Close branch ${b.code}? The client itself will remain active.`)) return;
    setActionError(null);
    try {
      await branchesApi.close(clientId, b.id, b.version);
      await load();
    } catch (ex: unknown) {
      setActionError(
        apiMessage(ex, {
          primary_branch_requires_replacement:
            "This is the primary branch and another active branch exists — promote one of them to primary first.",
        })
      );
    }
  }

  async function reopen(b: BranchDto) {
    setActionError(null);
    try {
      await branchesApi.reopen(clientId, b.id, b.version);
      await load();
    } catch (ex: unknown) {
      setActionError(apiMessage(ex));
    }
  }

  return (
    <Card>
      <CardHeader
        title="Branches & Jurisdictions"
        description="Multi-state establishment records, GSTIN registrations, and billing jurisdictions."
        action={
          <div className="flex items-center gap-3">
            <label className="flex items-center gap-1.5 text-xs text-slate-500 cursor-pointer">
              <input
                type="checkbox"
                checked={showInactive}
                onChange={(e) => setShowInactive(e.target.checked)}
                className="rounded border-slate-300 text-brand-600 focus:ring-brand-500"
              />
              <span>Show closed branches</span>
            </label>
            {canWrite && (
              <Button
                variant="primary"
                size="sm"
                onClick={() => setCreating(true)}
              >
                + Add Branch
              </Button>
            )}
          </div>
        }
      />

      <CardContent className="space-y-4">
        {actionError && (
          <div className="rounded-lg bg-red-50 border border-red-200 p-3 text-xs text-red-700 flex items-start gap-2">
            <svg className="w-4 h-4 text-red-600 shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            <span>{actionError}</span>
          </div>
        )}

        {loading ? (
          <div className="py-8 flex justify-center">
            <LoadingSpinner text="Loading branches…" />
          </div>
        ) : branches.length === 0 ? (
          <EmptyState
            title="No branches registered"
            description="Establish the client's Head Office or state-level operating branches."
            action={
              canWrite ? (
                <Button variant="primary" size="sm" onClick={() => setCreating(true)}>
                  Register First Branch
                </Button>
              ) : undefined
            }
          />
        ) : (
          <div className="grid grid-cols-1 gap-4">
            {branches.map((b) => (
              <div
                key={b.id}
                className={`rounded-xl border transition-all ${
                  b.isActive
                    ? "border-slate-200/90 bg-white hover:border-slate-300 hover:shadow-xs"
                    : "border-slate-200 bg-slate-50/60 opacity-80"
                } p-4`}
              >
                <div className="flex flex-col sm:flex-row sm:items-start justify-between gap-3">
                  <div>
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="font-mono text-xs font-semibold px-2 py-0.5 rounded bg-slate-100 text-slate-800 border border-slate-200">
                        {b.code}
                      </span>
                      <h4 className="font-semibold text-slate-900 text-sm">{b.name}</h4>
                      {b.isPrimary && (
                        <Badge variant="primary" size="sm">
                          PRIMARY HEAD OFFICE
                        </Badge>
                      )}
                      {b.isBillingEnabled && (
                        <Badge variant="success" size="sm">
                          Billing Enabled
                        </Badge>
                      )}
                      {!b.isActive && (
                        <Badge variant="neutral" size="sm">
                          Closed {b.closedOn ? `· ${b.closedOn}` : ""}
                        </Badge>
                      )}
                    </div>

                    <div className="mt-2 text-xs text-slate-600 flex flex-wrap items-center gap-y-1 gap-x-3">
                      <span className="font-medium text-slate-700">{b.type === "HeadOffice" ? "Head Office" : "Branch Office"}</span>
                      <span className="text-slate-300">|</span>
                      <span>State: <strong className="text-slate-800">{b.stateName ?? b.stateId}</strong></span>
                      <span className="text-slate-300">|</span>
                      <span className="text-slate-500">
                        {b.address.line1}
                        {b.address.line2 ? `, ${b.address.line2}` : ""}, {b.address.city} · {b.address.pincode}
                      </span>
                    </div>
                  </div>

                  {canWrite && (
                    <div className="flex items-center gap-1.5 shrink-0">
                      {b.isActive && (
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => setEditing(b)}
                        >
                          Edit
                        </Button>
                      )}
                      {b.isActive && !b.isPrimary && (
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => makePrimary(b)}
                        >
                          Set Primary
                        </Button>
                      )}
                      {b.isActive && (
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => close(b)}
                          className="text-red-600 hover:text-red-700 hover:bg-red-50"
                        >
                          Close
                        </Button>
                      )}
                      {!b.isActive && (
                        <Button
                          variant="secondary"
                          size="sm"
                          onClick={() => reopen(b)}
                        >
                          Reopen
                        </Button>
                      )}
                    </div>
                  )}
                </div>

                <TaxRegistrationsPanel clientId={clientId} branchId={b.id} canWrite={canWrite} />
                <BillingContextPanel clientId={clientId} branchId={b.id} />
              </div>
            ))}
          </div>
        )}
      </CardContent>

      {creating && (
        <BranchFormDialog
          onSave={(body) => create(body as CreateBranchBody)}
          onClose={() => setCreating(false)}
        />
      )}
      {editing && (
        <BranchFormDialog
          initial={editing}
          onSave={(body) => update(body as UpdateBranchBody)}
          onClose={() => setEditing(null)}
        />
      )}
    </Card>
  );
}

function apiMessage(ex: unknown, mapping: Record<string, string> = {}): string {
  if (ex instanceof ApiError) {
    try {
      const parsed = JSON.parse(ex.body) as { error?: string };
      if (parsed.error && mapping[parsed.error]) return mapping[parsed.error];
      if (parsed.error) return parsed.error;
    } catch {
      // fall through
    }
    return ex.message;
  }
  return ex instanceof Error ? ex.message : "Request failed";
}
