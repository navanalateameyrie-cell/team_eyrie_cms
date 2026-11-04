import { useState } from "react";
import { apiFetch } from "../../api/client";
import { formatInr } from "../../utils/formatters";
import { Modal, FormField, Input, Textarea, Button } from "../../components/ui";

// EmailStatus.Sent = 2 on the server. The ASP.NET JSON serializer emits enums as integers.
const isSentStatus = (s: string | number) => s === 2 || s === "Sent";

interface Props {
  invoice: { id: string; number: string; totalInr: number; dueDate: string; clientName: string };
  onClose: () => void;
  onSent: () => void;
}

export default function SendInvoiceEmailDialog({ invoice, onClose, onSent }: Props) {
  const [to, setTo] = useState("");
  const [cc, setCc] = useState("");
  const [subject, setSubject] = useState(`Tax Invoice ${invoice.number} from Chartered Accountants`);
  const [body, setBody] = useState(
    `Dear Sir / Madam,\n\nPlease find attached Tax Invoice ${invoice.number} for ${formatInr(invoice.totalInr)}, due on ${invoice.dueDate}.\n\nPlease initiate payment to our designated practice bank account and share the payment remittance advice.\n\nThank you,\nFinance & Accounts Team`
  );
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<{ status: string | number; errorMessage: string | null } | null>(null);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    const toList = to
      .split(/[,;]/)
      .map((s) => s.trim())
      .filter(Boolean);
    const ccList = cc
      .split(/[,;]/)
      .map((s) => s.trim())
      .filter(Boolean);
    if (toList.length === 0) {
      setError("At least one recipient email address is required.");
      return;
    }
    setSubmitting(true);
    setError(null);
    setResult(null);
    try {
      const r = await apiFetch<{ status: string | number; errorMessage: string | null }>(
        `/api/v1/billing/invoices/${invoice.id}/send-email`,
        { method: "POST", body: JSON.stringify({ to: toList, cc: ccList, subject, body }) }
      );
      setResult(r);
      if (isSentStatus(r.status)) {
        setTimeout(onSent, 800);
      }
    } catch (e) {
      setError(String(e));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Modal
      open={true}
      onClose={onClose}
      title={`Email Invoice · ${invoice.number}`}
      description={`PDF attachment auto-generated for ${invoice.clientName}`}
      size="lg"
    >
      <form onSubmit={submit} className="space-y-4">
        <FormField label="Recipient Email Addresses (To)" required hint="Separate multiple addresses with commas or semicolons">
          <Input
            value={to}
            onChange={(e) => setTo(e.target.value)}
            required
            placeholder="client.finance@example.com, director@example.com"
          />
        </FormField>

        <FormField label="Carbon Copy (Cc)" hint="Internal partners or accountants">
          <Input
            value={cc}
            onChange={(e) => setCc(e.target.value)}
            placeholder="partner@cafirm.in"
          />
        </FormField>

        <FormField label="Subject Line" required>
          <Input
            value={subject}
            onChange={(e) => setSubject(e.target.value)}
            required
          />
        </FormField>

        <FormField label="Email Body (Plain Text)">
          <Textarea
            value={body}
            onChange={(e) => setBody(e.target.value)}
            rows={7}
            className="font-mono text-xs"
          />
        </FormField>

        {error && (
          <div className="rounded-lg bg-red-50 border border-red-200 p-2.5 text-xs text-red-700">
            {error}
          </div>
        )}

        {result && (
          <div
            className={`text-xs rounded-lg p-2.5 border ${
              isSentStatus(result.status)
                ? "text-emerald-800 bg-emerald-50 border-emerald-200"
                : "text-red-700 bg-red-50 border-red-200"
            }`}
          >
            {isSentStatus(result.status)
              ? "✓ Email sent successfully and queued in practice outbox."
              : `Delivery failed: ${result.errorMessage ?? "Unknown error"}. Check SMTP settings.`}
          </div>
        )}

        <div className="flex justify-end gap-2 pt-3 border-t border-slate-200">
          <Button type="button" variant="outline" onClick={onClose} disabled={submitting}>
            Close
          </Button>
          <Button type="submit" variant="primary" loading={submitting}>
            {submitting ? "Transmitting Email…" : "Send Invoice Email"}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
