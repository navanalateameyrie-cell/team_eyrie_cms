import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { apiFetch } from "../../api/client";
import { useAuth } from "../../auth/AuthContext";
import {
  PageHeader,
  Card,
  CardHeader,
  CardContent,
  CardFooter,
  Tabs,
  FormField,
  Input,
  Select,
  Button,
  LoadingSpinner,
} from "../../components/ui";

interface FirmProfile {
  name?: string;
  line1?: string;
  line2?: string;
  city?: string;
  state?: string;
  pincode?: string;
  gstin?: string;
  pan?: string;
  phone?: string;
  email?: string;
  website?: string;
  bank?: {
    accountName?: string;
    accountNumber?: string;
    ifsc?: string;
    bankName?: string;
    branchName?: string;
  };
}

interface MailSettings {
  smtpHost?: string;
  smtpPort?: number;
  useSsl?: boolean;
  username?: string;
  password?: string;
  fromAddress?: string;
  fromName?: string;
  devDropFolder?: string;
}

interface ReminderSettings {
  enabled?: boolean;
  windowDays?: number;
  dailyRunHourUtc?: number;
}

interface EInvoicingSettings {
  enabled?: boolean;
  provider?: string;
  baseUrl?: string;
  clientId?: string;
  clientSecret?: string;
  gstin?: string;
  username?: string;
  password?: string;
}

interface DigitalSignatureSettings {
  enabled?: boolean;
  certificatePath?: string;
  certificatePassword?: string;
  signerName?: string;
}

interface SettingDto<T> {
  key: string;
  value: T;
  updatedAtUtc: string | null;
  updatedByUserName: string | null;
}

type SettingTab = "firm" | "mail" | "reminders" | "einvoicing" | "dsc";

export default function SettingsPage() {
  const { hasPermission } = useAuth();
  const canReadAudit = hasPermission("audit.read");
  const [activeTab, setActiveTab] = useState<SettingTab>("firm");
  const [firm, setFirm] = useState<SettingDto<FirmProfile> | null>(null);
  const [mail, setMail] = useState<SettingDto<MailSettings> | null>(null);
  const [reminders, setReminders] = useState<SettingDto<ReminderSettings> | null>(null);
  const [einvoicing, setEinvoicing] = useState<SettingDto<EInvoicingSettings> | null>(null);
  const [dsc, setDsc] = useState<SettingDto<DigitalSignatureSettings> | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [toast, setToast] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = async () => {
    setLoading(true);
    try {
      const [f, m, r, e, d] = await Promise.all([
        apiFetch<SettingDto<FirmProfile>>("/api/v1/settings/firmProfile"),
        apiFetch<SettingDto<MailSettings>>("/api/v1/settings/mail"),
        apiFetch<SettingDto<ReminderSettings>>("/api/v1/settings/reminders"),
        apiFetch<SettingDto<EInvoicingSettings>>("/api/v1/settings/eInvoicing"),
        apiFetch<SettingDto<DigitalSignatureSettings>>("/api/v1/settings/digitalSignature"),
      ]);
      setFirm(f);
      setMail(m);
      setReminders(r);
      setEinvoicing(e);
      setDsc(d);
      setError(null);
    } catch (e) {
      setError(String(e));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  async function save<T>(key: string, value: T, setter: (dto: SettingDto<T>) => void) {
    setError(null);
    try {
      const updated = await apiFetch<SettingDto<T>>(`/api/v1/settings/${key}`, {
        method: "PUT",
        body: JSON.stringify({ value }),
      });
      setter(updated);
      setToast(`Settings for ${key} persisted successfully.`);
      setTimeout(() => setToast(null), 3000);
    } catch (e) {
      setError(String(e));
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        breadcrumbs={[
          { label: "Settings", to: "/admin/settings" },
          { label: "Configuration" },
        ]}
        title="Practice & Integration Settings"
        subtitle="Manage firm identity, invoice banking particulars, SMTP gateways, GST e-invoicing, and digital signatures."
        actions={
          canReadAudit ? (
            <Link to="/admin/audit">
              <Button variant="outline" size="sm">
                View Audit Activity Log →
              </Button>
            </Link>
          ) : null
        }
      />

      {error && (
        <div className="rounded-lg bg-red-50 border border-red-200 p-3 text-xs text-red-700 flex items-center gap-2">
          <svg className="w-4 h-4 text-red-600 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          <span>{error}</span>
        </div>
      )}

      {toast && (
        <div className="rounded-lg bg-emerald-50 border border-emerald-200 p-3 text-xs text-emerald-800 flex items-center gap-2 animate-fadeIn">
          <svg className="w-4 h-4 text-emerald-600 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
          </svg>
          <span>{toast}</span>
        </div>
      )}

      {loading ? (
        <div className="py-16 flex justify-center">
          <LoadingSpinner text="Loading practice configuration…" size="lg" />
        </div>
      ) : (
        <div className="space-y-6">
          <Tabs<SettingTab>
            tabs={[
              { id: "firm", label: "Firm Profile & Bank" },
              { id: "mail", label: "Email / SMTP Gateway" },
              { id: "reminders", label: "Statutory Reminders" },
              { id: "einvoicing", label: "GST E-Invoicing (IRN)" },
              { id: "dsc", label: "Digital Signature (PFX)" },
            ]}
            activeTab={activeTab}
            onChange={setActiveTab}
            variant="underline"
          />

          {activeTab === "firm" && firm && (
            <FirmProfileCard dto={firm} onSave={(v) => save("firmProfile", v, setFirm)} />
          )}

          {activeTab === "mail" && mail && (
            <MailCard dto={mail} onSave={(v) => save("mail", v, setMail)} />
          )}

          {activeTab === "reminders" && reminders && (
            <ReminderCard dto={reminders} onSave={(v) => save("reminders", v, setReminders)} />
          )}

          {activeTab === "einvoicing" && einvoicing && (
            <EInvoiceCard dto={einvoicing} onSave={(v) => save("eInvoicing", v, setEinvoicing)} />
          )}

          {activeTab === "dsc" && dsc && (
            <DigitalSignatureCard dto={dsc} onSave={(v) => save("digitalSignature", v, setDsc)} />
          )}
        </div>
      )}
    </div>
  );
}

function metaText<T>(dto: SettingDto<T>) {
  if (!dto.updatedAtUtc) return "Default values · not yet saved";
  return `Last modified: ${new Date(dto.updatedAtUtc).toLocaleString()}${
    dto.updatedByUserName ? ` by ${dto.updatedByUserName}` : ""
  }`;
}

function FirmProfileCard({
  dto,
  onSave,
}: {
  dto: SettingDto<FirmProfile>;
  onSave: (v: FirmProfile) => void;
}) {
  const [v, setV] = useState<FirmProfile>({ ...dto.value, bank: dto.value.bank ?? {} });
  const [saving, setSaving] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true);
    await onSave(v);
    setSaving(false);
  }

  return (
    <Card>
      <CardHeader
        title="Chartered Accountant Firm Profile"
        subtitle={metaText(dto)}
      />
      <form onSubmit={handleSubmit}>
        <CardContent className="space-y-5">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <FormField label="Practice Firm Name" required>
              <Input
                value={v.name ?? ""}
                onChange={(e) => setV({ ...v, name: e.target.value })}
                placeholder="e.g. Haran & Co. Chartered Accountants"
                required
              />
            </FormField>

            <FormField label="Official Website">
              <Input
                value={v.website ?? ""}
                onChange={(e) => setV({ ...v, website: e.target.value })}
                placeholder="https://www.cafirm.in"
              />
            </FormField>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <FormField label="Practice GSTIN">
              <Input
                value={v.gstin ?? ""}
                onChange={(e) => setV({ ...v, gstin: e.target.value.toUpperCase() })}
                placeholder="27AAAAA0000A1Z5"
                className="font-mono uppercase"
              />
            </FormField>

            <FormField label="Practice PAN">
              <Input
                value={v.pan ?? ""}
                onChange={(e) => setV({ ...v, pan: e.target.value.toUpperCase() })}
                placeholder="AAAAA0000A"
                className="font-mono uppercase"
              />
            </FormField>

            <FormField label="Official Phone">
              <Input
                value={v.phone ?? ""}
                onChange={(e) => setV({ ...v, phone: e.target.value })}
                placeholder="+91 22 1234 5678"
              />
            </FormField>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <FormField label="Address Line 1">
              <Input
                value={v.line1 ?? ""}
                onChange={(e) => setV({ ...v, line1: e.target.value })}
                placeholder="Suite, Floor, Building"
              />
            </FormField>

            <FormField label="Address Line 2">
              <Input
                value={v.line2 ?? ""}
                onChange={(e) => setV({ ...v, line2: e.target.value })}
                placeholder="Street, Landmark"
              />
            </FormField>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <FormField label="City">
              <Input
                value={v.city ?? ""}
                onChange={(e) => setV({ ...v, city: e.target.value })}
                placeholder="e.g. Mumbai"
              />
            </FormField>

            <FormField label="State">
              <Input
                value={v.state ?? ""}
                onChange={(e) => setV({ ...v, state: e.target.value })}
                placeholder="e.g. Maharashtra"
              />
            </FormField>

            <FormField label="PIN Code">
              <Input
                value={v.pincode ?? ""}
                onChange={(e) => setV({ ...v, pincode: e.target.value })}
                placeholder="400001"
                className="font-mono"
              />
            </FormField>
          </div>

          {/* Bank Details Strip */}
          <div className="rounded-xl border border-slate-200 bg-slate-50/70 p-4 space-y-4">
            <h4 className="text-xs font-semibold uppercase tracking-wider text-slate-700">
              Settlement Bank Account (Rendered on Outbound Invoices)
            </h4>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <FormField label="Account Beneficiary Name">
                <Input
                  value={v.bank?.accountName ?? ""}
                  onChange={(e) =>
                    setV({ ...v, bank: { ...v.bank, accountName: e.target.value } })
                  }
                  placeholder="Haran & Co."
                />
              </FormField>

              <FormField label="Bank Account Number">
                <Input
                  value={v.bank?.accountNumber ?? ""}
                  onChange={(e) =>
                    setV({ ...v, bank: { ...v.bank, accountNumber: e.target.value } })
                  }
                  placeholder="9180200000000"
                  className="font-mono"
                />
              </FormField>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
              <FormField label="IFSC Code">
                <Input
                  value={v.bank?.ifsc ?? ""}
                  onChange={(e) =>
                    setV({ ...v, bank: { ...v.bank, ifsc: e.target.value.toUpperCase() } })
                  }
                  placeholder="HDFC0000001"
                  className="font-mono uppercase"
                />
              </FormField>

              <FormField label="Bank Name">
                <Input
                  value={v.bank?.bankName ?? ""}
                  onChange={(e) =>
                    setV({ ...v, bank: { ...v.bank, bankName: e.target.value } })
                  }
                  placeholder="HDFC Bank"
                />
              </FormField>

              <FormField label="Branch Location">
                <Input
                  value={v.bank?.branchName ?? ""}
                  onChange={(e) =>
                    setV({ ...v, bank: { ...v.bank, branchName: e.target.value } })
                  }
                  placeholder="Fort, Mumbai"
                />
              </FormField>
            </div>
          </div>
        </CardContent>

        <CardFooter className="flex justify-end">
          <Button type="submit" variant="primary" loading={saving}>
            Save Firm Profile
          </Button>
        </CardFooter>
      </form>
    </Card>
  );
}

function MailCard({
  dto,
  onSave,
}: {
  dto: SettingDto<MailSettings>;
  onSave: (v: MailSettings) => void;
}) {
  const [v, setV] = useState<MailSettings>({ ...dto.value });
  const [saving, setSaving] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true);
    await onSave(v);
    setSaving(false);
  }

  return (
    <Card>
      <CardHeader
        title="Outbound SMTP Email Gateway"
        subtitle={metaText(dto)}
      />
      <form onSubmit={handleSubmit}>
        <CardContent className="space-y-4">
          <p className="text-xs text-slate-500">
            Configure SMTP credentials for dispatching tax invoices, reminder notices, and compliance letters. Leave host blank to drop outbound messages into the local dev drop folder.
          </p>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <FormField label="SMTP Server Host">
              <Input
                value={v.smtpHost ?? ""}
                onChange={(e) => setV({ ...v, smtpHost: e.target.value })}
                placeholder="smtp.office365.com or smtp.gmail.com"
              />
            </FormField>

            <FormField label="SMTP Port">
              <Input
                type="number"
                value={v.smtpPort ?? 587}
                onChange={(e) => setV({ ...v, smtpPort: Number(e.target.value) })}
                placeholder="587 or 465"
                className="font-mono"
              />
            </FormField>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <FormField label="Sender From Address" required>
              <Input
                value={v.fromAddress ?? ""}
                onChange={(e) => setV({ ...v, fromAddress: e.target.value })}
                placeholder="billing@cafirm.in"
              />
            </FormField>

            <FormField label="Sender Display Name" required>
              <Input
                value={v.fromName ?? ""}
                onChange={(e) => setV({ ...v, fromName: e.target.value })}
                placeholder="Accounts & Billing Desk"
              />
            </FormField>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <FormField label="SMTP Username">
              <Input
                value={v.username ?? ""}
                onChange={(e) => setV({ ...v, username: e.target.value })}
                placeholder="username@cafirm.in"
              />
            </FormField>

            <FormField label="SMTP Password">
              <Input
                type="password"
                value={v.password ?? ""}
                onChange={(e) => setV({ ...v, password: e.target.value })}
                placeholder="••••••••••••"
              />
            </FormField>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 pt-1">
            <FormField label="Local Dev Drop Folder (Fallback)">
              <Input
                value={v.devDropFolder ?? ""}
                onChange={(e) => setV({ ...v, devDropFolder: e.target.value })}
                placeholder="/tmp/cms-mail-drop"
                className="font-mono text-xs"
              />
            </FormField>

            <div className="flex items-center pt-5">
              <label className="flex items-center gap-2 cursor-pointer">
                <input
                  type="checkbox"
                  checked={!!v.useSsl}
                  onChange={(e) => setV({ ...v, useSsl: e.target.checked })}
                  className="rounded border-slate-300 text-brand-600 focus:ring-brand-500"
                />
                <span className="text-xs font-semibold text-slate-800">
                  Enforce SSL / TLS Encryption
                </span>
              </label>
            </div>
          </div>
        </CardContent>

        <CardFooter className="flex justify-end">
          <Button type="submit" variant="primary" loading={saving}>
            Save SMTP Settings
          </Button>
        </CardFooter>
      </form>
    </Card>
  );
}

function ReminderCard({
  dto,
  onSave,
}: {
  dto: SettingDto<ReminderSettings>;
  onSave: (v: ReminderSettings) => void;
}) {
  const [v, setV] = useState<ReminderSettings>({ ...dto.value });
  const [saving, setSaving] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true);
    await onSave(v);
    setSaving(false);
  }

  return (
    <Card>
      <CardHeader
        title="Compliance Email Reminders"
        subtitle={metaText(dto)}
      />
      <form onSubmit={handleSubmit}>
        <CardContent className="space-y-4">
          <p className="text-xs text-slate-500">
            Automated statutory reminders alert clients and staff before upcoming filing deadlines.
          </p>

          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 items-center">
            <div className="pt-2">
              <label className="flex items-center gap-2 cursor-pointer">
                <input
                  type="checkbox"
                  checked={!!v.enabled}
                  onChange={(e) => setV({ ...v, enabled: e.target.checked })}
                  className="rounded border-slate-300 text-brand-600 focus:ring-brand-500"
                />
                <span className="text-xs font-semibold text-slate-800">
                  Enable Daily Automated Reminders
                </span>
              </label>
            </div>

            <FormField label="Lookahead Window (Days)">
              <Input
                type="number"
                min={1}
                max={90}
                value={v.windowDays ?? 7}
                onChange={(e) => setV({ ...v, windowDays: Number(e.target.value) })}
                className="font-mono"
              />
            </FormField>

            <FormField label="Daily Dispatch Hour (UTC 0–23)">
              <Input
                type="number"
                min={0}
                max={23}
                value={v.dailyRunHourUtc ?? 4}
                onChange={(e) => setV({ ...v, dailyRunHourUtc: Number(e.target.value) })}
                className="font-mono"
              />
            </FormField>
          </div>
        </CardContent>

        <CardFooter className="flex justify-end">
          <Button type="submit" variant="primary" loading={saving}>
            Save Reminder Preferences
          </Button>
        </CardFooter>
      </form>
    </Card>
  );
}

function EInvoiceCard({
  dto,
  onSave,
}: {
  dto: SettingDto<EInvoicingSettings>;
  onSave: (v: EInvoicingSettings) => void;
}) {
  const [v, setV] = useState<EInvoicingSettings>({ ...dto.value });
  const [saving, setSaving] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true);
    await onSave(v);
    setSaving(false);
  }

  return (
    <Card>
      <CardHeader
        title="GST E-Invoicing & Invoice Registration Portal (IRP)"
        subtitle={metaText(dto)}
      />
      <form onSubmit={handleSubmit}>
        <CardContent className="space-y-4">
          <p className="text-xs text-slate-500">
            Transmit B2B tax invoices directly to the Government IRP to obtain 64-character Invoice Reference Numbers (IRN) and digitally signed QR codes.
          </p>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div className="flex items-center pt-2">
              <label className="flex items-center gap-2 cursor-pointer">
                <input
                  type="checkbox"
                  checked={!!v.enabled}
                  onChange={(e) => setV({ ...v, enabled: e.target.checked })}
                  className="rounded border-slate-300 text-brand-600 focus:ring-brand-500"
                />
                <span className="text-xs font-semibold text-slate-800">
                  Enable Direct E-Invoicing API
                </span>
              </label>
            </div>

            <FormField label="Integration Provider">
              <Select
                value={v.provider ?? "Stub"}
                onChange={(e) => setV({ ...v, provider: e.target.value })}
              >
                <option value="Stub">Stub Emulator (Local Dev / QA)</option>
                <option value="Nic">NIC Government IRP</option>
              </Select>
            </FormField>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <FormField label="IRP Gateway Base URL">
              <Input
                value={v.baseUrl ?? ""}
                onChange={(e) => setV({ ...v, baseUrl: e.target.value })}
                placeholder="https://einv-apisandbox.nic.in"
              />
            </FormField>

            <FormField label="Practice GSTIN for IRP Auth">
              <Input
                value={v.gstin ?? ""}
                onChange={(e) => setV({ ...v, gstin: e.target.value.toUpperCase() })}
                placeholder="27AAAAA0000A1Z5"
                className="font-mono uppercase"
              />
            </FormField>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <FormField label="GSP / ASP Client ID">
              <Input
                value={v.clientId ?? ""}
                onChange={(e) => setV({ ...v, clientId: e.target.value })}
                placeholder="Client ID issued by GSP"
              />
            </FormField>

            <FormField label="GSP / ASP Client Secret">
              <Input
                type="password"
                value={v.clientSecret ?? ""}
                onChange={(e) => setV({ ...v, clientSecret: e.target.value })}
                placeholder="••••••••••••"
              />
            </FormField>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <FormField label="IRP Portal Username">
              <Input
                value={v.username ?? ""}
                onChange={(e) => setV({ ...v, username: e.target.value })}
                placeholder="Username configured on e-invoice portal"
              />
            </FormField>

            <FormField label="IRP Portal Password">
              <Input
                type="password"
                value={v.password ?? ""}
                onChange={(e) => setV({ ...v, password: e.target.value })}
                placeholder="••••••••••••"
              />
            </FormField>
          </div>
        </CardContent>

        <CardFooter className="flex justify-end">
          <Button type="submit" variant="primary" loading={saving}>
            Save E-Invoicing Credentials
          </Button>
        </CardFooter>
      </form>
    </Card>
  );
}

function DigitalSignatureCard({
  dto,
  onSave,
}: {
  dto: SettingDto<DigitalSignatureSettings>;
  onSave: (v: DigitalSignatureSettings) => void;
}) {
  const [v, setV] = useState<DigitalSignatureSettings>({ ...dto.value });
  const [saving, setSaving] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setSaving(true);
    await onSave(v);
    setSaving(false);
  }

  return (
    <Card>
      <CardHeader
        title="Digital Signature Certificate (DSC / PFX)"
        subtitle={metaText(dto)}
      />
      <form onSubmit={handleSubmit}>
        <CardContent className="space-y-4">
          <p className="text-xs text-slate-500">
            Digitally signs outbound invoice PDFs using Class 3 DSC (.pfx) to embed a cryptographically verified signature and visible signatory stamp.
          </p>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div className="flex items-center pt-2">
              <label className="flex items-center gap-2 cursor-pointer">
                <input
                  type="checkbox"
                  checked={!!v.enabled}
                  onChange={(e) => setV({ ...v, enabled: e.target.checked })}
                  className="rounded border-slate-300 text-brand-600 focus:ring-brand-500"
                />
                <span className="text-xs font-semibold text-slate-800">
                  Enable PDF Digital Signing
                </span>
              </label>
            </div>

            <FormField label="Signatory Name (Rendered on PDF Stamp)">
              <Input
                value={v.signerName ?? ""}
                onChange={(e) => setV({ ...v, signerName: e.target.value })}
                placeholder="e.g. CA John Doe, Partner"
              />
            </FormField>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <FormField label="Server Certificate Path (.pfx)">
              <Input
                value={v.certificatePath ?? ""}
                onChange={(e) => setV({ ...v, certificatePath: e.target.value })}
                placeholder="/etc/cms/certs/firm-dsc.pfx"
                className="font-mono text-xs"
              />
            </FormField>

            <FormField label="Certificate Key Password">
              <Input
                type="password"
                value={v.certificatePassword ?? ""}
                onChange={(e) => setV({ ...v, certificatePassword: e.target.value })}
                placeholder="••••••••••••"
              />
            </FormField>
          </div>
        </CardContent>

        <CardFooter className="flex justify-end">
          <Button type="submit" variant="primary" loading={saving}>
            Save DSC Configuration
          </Button>
        </CardFooter>
      </form>
    </Card>
  );
}
