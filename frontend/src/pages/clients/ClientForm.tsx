import { useEffect, useState } from "react";
import { mastersApi, type EntityTypeDto, type ClientGroupDto } from "../../api/masters";
import type { ClientDto, ClientUpsertBody, DuplicatePanCandidate } from "../../api/clients";
import { clientsApi, isDuplicatePanProblem, parseDuplicatePanProblem } from "../../api/clients";
import { ApiError } from "../../api/client";
import { Card, CardHeader, CardContent, CardFooter, FormField, Input, Select, Button } from "../../components/ui";

interface Props {
  initial?: ClientDto;
  onSaved: (c: ClientDto) => void;
  onCancel?: () => void;
}

export default function ClientForm({ initial, onSaved, onCancel }: Props) {
  const [entityTypes, setEntityTypes] = useState<EntityTypeDto[]>([]);
  const [groups, setGroups] = useState<ClientGroupDto[]>([]);

  const [legalName, setLegalName] = useState(initial?.legalName ?? "");
  const [tradeName, setTradeName] = useState(initial?.tradeName ?? "");
  const [pan, setPan] = useState(initial?.pan ?? "");
  const [entityTypeId, setEntityTypeId] = useState(initial?.entityTypeId ?? "");
  const [clientGroupId, setClientGroupId] = useState(initial?.clientGroupId ?? "");
  const [legacyId, setLegacyId] = useState(initial?.legacyClientId ?? "");

  const [panCandidates, setPanCandidates] = useState<DuplicatePanCandidate[] | null>(null);
  const [ackDup, setAckDup] = useState(false);

  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void mastersApi.entityTypes.list(undefined, true).then(setEntityTypes);
    void mastersApi.clientGroups.list(undefined, true).then(setGroups);
  }, []);

  // Live PAN duplicate check (debounced)
  useEffect(() => {
    const normalized = pan.trim().toUpperCase();
    if (!/^[A-Z]{5}[0-9]{4}[A-Z]$/.test(normalized)) {
      setPanCandidates(null);
      return;
    }
    const h = setTimeout(() => {
      clientsApi
        .duplicatePanCandidates(normalized, initial?.id)
        .then((list) => setPanCandidates(list.length > 0 ? list : null))
        .catch(() => setPanCandidates(null));
    }, 400);
    return () => clearTimeout(h);
  }, [pan, initial?.id]);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setSaving(true);
    try {
      const body: ClientUpsertBody = {
        legalName,
        entityTypeId,
        tradeName: tradeName || null,
        pan: pan ? pan.trim().toUpperCase() : null,
        clientGroupId: clientGroupId || null,
        legacyClientId: legacyId || null,
        acknowledgeDuplicatePan: ackDup,
      };
      const saved = initial
        ? await clientsApi.update(initial.id, { ...body, version: initial.version })
        : await clientsApi.create(body);
      onSaved(saved);
    } catch (ex: unknown) {
      if (isDuplicatePanProblem(ex)) {
        const p = parseDuplicatePanProblem(ex as ApiError);
        setPanCandidates(p.candidates);
        setError(
          "This PAN is already used by another client. Check the matches below, then tick 'Accept duplicates' and save again."
        );
      } else if (ex instanceof ApiError && ex.status === 409) {
        setError(initial ? "This client was updated by someone else. Reload the page." : "Legacy client id already exists.");
      } else {
        setError(ex instanceof Error ? ex.message : "Save failed");
      }
    } finally {
      setSaving(false);
    }
  }

  return (
    <Card className="max-w-3xl">
      <CardHeader
        title={initial ? "Edit Client Entity" : "Client Registration"}
        description="Provide legal entity credentials, taxation identifiers, and corporate metadata."
      />
      <form onSubmit={submit}>
        <CardContent className="space-y-5">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <FormField label="Legal Name" required hint="Full official name as per PAN / MCA">
              <Input
                required
                maxLength={300}
                value={legalName}
                onChange={(e) => setLegalName(e.target.value)}
                placeholder="e.g. Acme FinTech Private Limited"
              />
            </FormField>

            <FormField label="Trade Name / DBA" hint="Commercial name if different from legal name">
              <Input
                maxLength={300}
                value={tradeName}
                onChange={(e) => setTradeName(e.target.value)}
                placeholder="e.g. Acme Payments"
              />
            </FormField>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <FormField label="Entity Type" required hint="Legal constitution of client">
              <Select
                required
                value={entityTypeId}
                onChange={(e) => setEntityTypeId(e.target.value)}
              >
                <option value="">— Select Entity Type —</option>
                {entityTypes.map((e) => (
                  <option key={e.id} value={e.id}>
                    {e.code} · {e.name}
                  </option>
                ))}
              </Select>
            </FormField>

            <FormField label="Client Group" hint="Conglomerate or family group grouping">
              <Select
                value={clientGroupId}
                onChange={(e) => setClientGroupId(e.target.value)}
              >
                <option value="">— Independent / None —</option>
                {groups.map((g) => (
                  <option key={g.id} value={g.id}>
                    {g.code} · {g.name}
                  </option>
                ))}
              </Select>
            </FormField>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <FormField label="Permanent Account Number (PAN)" hint="10-character Indian income tax identifier">
              <Input
                maxLength={10}
                value={pan}
                onChange={(e) => {
                  setPan(e.target.value.toUpperCase());
                  setAckDup(false);
                }}
                placeholder="AAAAA9999A"
                className="font-mono tracking-wider uppercase"
              />
            </FormField>

            <FormField label="Legacy Client Reference ID" hint="Previous software migration reference ID">
              <Input
                maxLength={64}
                value={legacyId}
                onChange={(e) => setLegacyId(e.target.value)}
                placeholder="e.g. CL-2023-884"
                className="font-mono"
              />
            </FormField>
          </div>

          {panCandidates && panCandidates.length > 0 && (
            <div className="rounded-lg bg-amber-50 border border-amber-200 p-4 text-sm animate-fadeIn">
              <div className="flex items-start gap-2.5">
                <svg className="w-5 h-5 text-amber-600 mt-0.5 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
                </svg>
                <div className="flex-1 space-y-2">
                  <p className="font-semibold text-amber-900">
                    PAN matches existing practice records:
                  </p>
                  <div className="divide-y divide-amber-100 rounded border border-amber-200/60 bg-white/70 overflow-hidden text-xs">
                    {panCandidates.map((c) => (
                      <div key={c.id} className="p-2 flex items-center justify-between text-amber-950">
                        <div>
                          <span className="font-mono font-medium text-slate-800">{c.code}</span>
                          <span className="mx-2 text-slate-300">|</span>
                          <span className="font-medium">{c.legalName}</span>
                        </div>
                        {!c.isActive ? (
                          <span className="text-[11px] font-medium bg-slate-100 text-slate-600 px-1.5 py-0.5 rounded">
                            Inactive
                          </span>
                        ) : (
                          <span className="text-[11px] font-medium bg-emerald-100 text-emerald-800 px-1.5 py-0.5 rounded">
                            Active
                          </span>
                        )}
                      </div>
                    ))}
                  </div>
                  <label className="inline-flex items-center gap-2 cursor-pointer pt-1">
                    <input
                      type="checkbox"
                      checked={ackDup}
                      onChange={(e) => setAckDup(e.target.checked)}
                      className="rounded border-amber-300 text-brand-600 focus:ring-brand-500"
                    />
                    <span className="text-xs font-semibold text-amber-950">
                      I acknowledge and authorize duplicate PAN for this legal entity
                    </span>
                  </label>
                </div>
              </div>
            </div>
          )}

          {error && (
            <div className="rounded-lg bg-red-50 border border-red-200 p-3.5 text-sm text-red-800 flex items-start gap-2.5">
              <svg className="w-5 h-5 text-red-600 mt-0.5 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
              <span>{error}</span>
            </div>
          )}
        </CardContent>

        <CardFooter className="flex items-center justify-between bg-slate-50/70 border-t border-slate-200/80">
          <div>
            {onCancel && (
              <Button type="button" variant="outline" onClick={onCancel}>
                Cancel
              </Button>
            )}
          </div>
          <div className="flex gap-2">
            <Button type="submit" variant="primary" loading={saving}>
              {saving ? "Saving Client…" : initial ? "Save Changes" : "Create Client"}
            </Button>
          </div>
        </CardFooter>
      </form>
    </Card>
  );
}
