import { useCallback, useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useAuth } from "../../auth/AuthContext";
import { clientsApi, type ClientDto } from "../../api/clients";
import { ApiError } from "../../api/client";
import ClientForm from "./ClientForm";
import BranchesSection from "./BranchesSection";
import ServiceFilesSection from "./ServiceFilesSection";
import StaffAssignmentsPanel from "./StaffAssignmentsPanel";
import DocumentsPanel from "../../components/DocumentsPanel";
import TdsSection from "./TdsSection";
import {
  Card,
  CardHeader,
  CardContent,
  Button,
  StatusBadge,
  PageHeader,
  Tabs,
  LoadingSpinner,
  ErrorState,
  ConfirmDialog,
} from "../../components/ui";

type ClientTab = "overview" | "branches" | "services" | "tds" | "documents";

export default function ClientProfilePage() {
  const { id } = useParams<{ id: string }>();
  const { hasPermission } = useAuth();
  const canWrite = hasPermission("clients.write");
  const nav = useNavigate();

  const [client, setClient] = useState<ClientDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [editing, setEditing] = useState(false);
  const [working, setWorking] = useState(false);
  const [activeTab, setActiveTab] = useState<ClientTab>("overview");
  const [confirmLifecycle, setConfirmLifecycle] = useState(false);
  const [copiedPan, setCopiedPan] = useState(false);

  const load = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    setError(null);
    try {
      setClient(await clientsApi.get(id));
    } catch (ex: unknown) {
      setError(ex instanceof Error ? ex.message : "Failed to load client details");
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    void load();
  }, [load]);

  async function handleToggleLifecycle() {
    if (!client) return;
    setWorking(true);
    try {
      const updated = client.isActive
        ? await clientsApi.deactivate(client.id, client.version)
        : await clientsApi.reactivate(client.id, client.version);
      setClient(updated);
      setConfirmLifecycle(false);
    } catch (ex: unknown) {
      if (ex instanceof ApiError && ex.status === 409) {
        alert("This client was updated by another user. Reloading page.");
        void load();
      } else {
        alert(ex instanceof Error ? ex.message : "Action failed");
      }
    } finally {
      setWorking(false);
    }
  }

  function handleCopyPan() {
    if (!client?.pan) return;
    void navigator.clipboard.writeText(client.pan);
    setCopiedPan(true);
    setTimeout(() => setCopiedPan(false), 2000);
  }

  if (loading) {
    return (
      <div className="py-16 flex justify-center">
        <LoadingSpinner text="Loading client profile…" size="lg" />
      </div>
    );
  }

  if (error || !client) {
    return (
      <ErrorState
        title={error ? "Unable to load client" : "Client not found"}
        description={error || "The requested client record does not exist or has been deleted."}
        onRetry={load}
      />
    );
  }

  if (editing) {
    return (
      <div className="space-y-6 max-w-4xl">
        <PageHeader
          breadcrumbs={[
            { label: "Clients", to: "/admin/clients" },
            { label: client.code, to: `/admin/clients/${client.id}` },
            { label: "Edit" },
          ]}
          title={`Edit · ${client.legalName}`}
          subtitle={`Client Code: ${client.code}`}
        />
        <ClientForm
          initial={client}
          onSaved={(c) => {
            setClient(c);
            setEditing(false);
          }}
          onCancel={() => setEditing(false)}
        />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <PageHeader
        breadcrumbs={[
          { label: "Clients", to: "/admin/clients" },
          { label: client.code },
        ]}
        title={client.legalName}
        subtitle={client.tradeName ? `Trading as: ${client.tradeName}` : undefined}
        status={
          <StatusBadge
            status={client.isActive ? "active" : "inactive"}
            pulse={client.isActive}
            size="md"
          />
        }
        actions={
          <div className="flex items-center gap-2">
            {hasPermission("audit.read") && (
              <Link to={`/admin/audit?clientId=${client.id}`}>
                <Button variant="outline" size="sm">
                  <svg className="w-3.5 h-3.5 mr-1 text-slate-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                  </svg>
                  Audit Trail
                </Button>
              </Link>
            )}
            {canWrite && (
              <>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setEditing(true)}
                >
                  <svg className="w-3.5 h-3.5 mr-1 text-slate-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15.232 5.232l3.536 3.536m-2.036-5.036a2.5 2.5 0 113.536 3.536L6.5 21.036H3v-3.572L16.732 3.732z" />
                  </svg>
                  Edit Profile
                </Button>
                <Button
                  variant={client.isActive ? "danger" : "secondary"}
                  size="sm"
                  onClick={() => setConfirmLifecycle(true)}
                  disabled={working}
                >
                  {client.isActive ? "Deactivate Client" : "Reactivate Client"}
                </Button>
              </>
            )}
          </div>
        }
      />

      {/* Metadata Pill Banner */}
      <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3">
        <div className="bg-white border border-slate-200/80 rounded-xl p-3 shadow-xs">
          <p className="text-[11px] font-medium uppercase tracking-wider text-slate-400">Client Code</p>
          <p className="font-mono font-bold text-slate-800 text-sm mt-0.5">{client.code}</p>
        </div>

        <div className="bg-white border border-slate-200/80 rounded-xl p-3 shadow-xs">
          <p className="text-[11px] font-medium uppercase tracking-wider text-slate-400">PAN</p>
          <div className="flex items-center gap-1.5 mt-0.5">
            <span className="font-mono font-bold text-slate-800 text-sm">{client.pan ?? "—"}</span>
            {client.pan && (
              <button
                type="button"
                onClick={handleCopyPan}
                title="Copy PAN"
                className="text-slate-400 hover:text-slate-700 transition-colors"
              >
                <svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z" />
                </svg>
              </button>
            )}
            {copiedPan && <span className="text-[10px] text-emerald-600 font-medium">Copied!</span>}
          </div>
        </div>

        <div className="bg-white border border-slate-200/80 rounded-xl p-3 shadow-xs">
          <p className="text-[11px] font-medium uppercase tracking-wider text-slate-400">Entity Type</p>
          <p className="font-medium text-slate-800 text-sm mt-0.5 truncate" title={client.entityTypeName ?? client.entityTypeId}>
            {client.entityTypeName ?? client.entityTypeId}
          </p>
        </div>

        <div className="bg-white border border-slate-200/80 rounded-xl p-3 shadow-xs">
          <p className="text-[11px] font-medium uppercase tracking-wider text-slate-400">Client Group</p>
          <p className="font-medium text-slate-800 text-sm mt-0.5 truncate" title={client.clientGroupName ?? "Independent"}>
            {client.clientGroupName ?? "—"}
          </p>
        </div>

        <div className="bg-white border border-slate-200/80 rounded-xl p-3 shadow-xs">
          <p className="text-[11px] font-medium uppercase tracking-wider text-slate-400">Legacy ID</p>
          <p className="font-mono text-slate-700 text-sm mt-0.5 truncate">
            {client.legacyClientId ?? "—"}
          </p>
        </div>

        <div className="bg-white border border-slate-200/80 rounded-xl p-3 shadow-xs">
          <p className="text-[11px] font-medium uppercase tracking-wider text-slate-400">Entity Version</p>
          <p className="font-mono text-slate-500 text-xs mt-1">
            v{client.version}
          </p>
        </div>
      </div>

      {/* Tabs Navigation */}
      <Tabs<ClientTab>
        tabs={[
          { id: "overview", label: "Overview & Staff" },
          { id: "branches", label: "Branches & GSTIN" },
          { id: "services", label: "Service Files" },
          { id: "tds", label: "TDS / Form 26AS" },
          { id: "documents", label: "Documents" },
        ]}
        activeTab={activeTab}
        onChange={setActiveTab}
        variant="underline"
      />

      {/* Tab Panels */}
      {activeTab === "overview" && (
        <div className="space-y-6">
          <Card>
            <CardHeader
              title="Practice Staff Assignments"
              description="Designated Partner-in-charge, Engagement Manager, and Article Assistants."
            />
            <CardContent>
              <StaffAssignmentsPanel clientId={client.id} />
            </CardContent>
          </Card>

          <Card>
            <CardHeader
              title="System Identifiers & Compliance Specifications"
              description="Permanent audit trail metadata and entity lineage."
            />
            <CardContent>
              <dl className="grid grid-cols-1 md:grid-cols-2 gap-4 text-sm">
                <div className="p-3 bg-slate-50 rounded-lg border border-slate-100">
                  <dt className="text-xs text-slate-500 uppercase font-medium">Permanent Client Reference</dt>
                  <dd className="mt-1 font-mono font-semibold text-slate-900">{client.code}</dd>
                  <p className="text-[11px] text-slate-400 mt-0.5">
                    Immutable 8-character practice identifier preserved across all re-activations.
                  </p>
                </div>
                <div className="p-3 bg-slate-50 rounded-lg border border-slate-100">
                  <dt className="text-xs text-slate-500 uppercase font-medium">Income Tax PAN</dt>
                  <dd className="mt-1 font-mono font-semibold text-slate-900">{client.pan ?? "Not provided"}</dd>
                  <p className="text-[11px] text-slate-400 mt-0.5">
                    Central CBDT PAN used for cross-filing and TDS matching.
                  </p>
                </div>
                <div className="p-3 bg-slate-50 rounded-lg border border-slate-100">
                  <dt className="text-xs text-slate-500 uppercase font-medium">Legal Constitution</dt>
                  <dd className="mt-1 text-slate-800 font-medium">{client.entityTypeName ?? client.entityTypeId}</dd>
                </div>
                <div className="p-3 bg-slate-50 rounded-lg border border-slate-100">
                  <dt className="text-xs text-slate-500 uppercase font-medium">Corporate Affiliation / Group</dt>
                  <dd className="mt-1 text-slate-800 font-medium">{client.clientGroupName ?? "Independent Entity"}</dd>
                </div>
              </dl>
            </CardContent>
          </Card>
        </div>
      )}

      {activeTab === "branches" && (
        <BranchesSection clientId={client.id} />
      )}

      {activeTab === "services" && (
        <ServiceFilesSection clientId={client.id} />
      )}

      {activeTab === "tds" && (
        <TdsSection clientId={client.id} />
      )}

      {activeTab === "documents" && (
        <Card>
          <CardHeader
            title="Client Document Repository"
            description="Engagement letters, incorporation certificates, PAN/TAN copies, and financial statements."
          />
          <CardContent>
            <DocumentsPanel ownerType="Client" ownerId={client.id} title="Attached to this client" />
          </CardContent>
        </Card>
      )}

      <div className="pt-2">
        <button
          type="button"
          onClick={() => nav("/admin/clients")}
          className="inline-flex items-center text-xs font-medium text-slate-500 hover:text-brand-600 transition-colors"
        >
          <svg className="w-4 h-4 mr-1" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10 19l-7-7m0 0l7-7m-7 7h18" />
          </svg>
          Back to Clients List
        </button>
      </div>

      {/* Confirmation Modal */}
      <ConfirmDialog
        open={confirmLifecycle}
        onClose={() => setConfirmLifecycle(false)}
        onConfirm={handleToggleLifecycle}
        title={client.isActive ? `Deactivate ${client.legalName}?` : `Reactivate ${client.legalName}?`}
        description={
          client.isActive
            ? "Deactivating will prevent new engagements and invoices from being issued. Existing records and client codes remain intact."
            : "Reactivating will restore the client to active practice operations and allow new service files."
        }
        confirmLabel={client.isActive ? "Deactivate Client" : "Reactivate Client"}
        variant={client.isActive ? "danger" : "primary"}
        loading={working}
      />
    </div>
  );
}
