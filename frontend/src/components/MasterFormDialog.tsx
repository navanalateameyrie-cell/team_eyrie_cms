import type { ReactNode } from "react";
import { Modal, FormField, Input, Button } from "./ui";

interface Props {
  title: string;
  onClose: () => void;
  onSubmit: () => void | Promise<void>;
  busy?: boolean;
  error?: string | null;
  children: ReactNode;
}

export function MasterFormDialog({ title, onClose, onSubmit, busy, error, children }: Props) {
  return (
    <Modal open={true} onClose={onClose} title={title} size="2xl">
      <form
        onSubmit={(e) => {
          e.preventDefault();
          void onSubmit();
        }}
        className="space-y-4"
      >
        <div className="space-y-3">{children}</div>

        {error && (
          <div className="rounded-lg bg-red-50 border border-red-200 p-2.5 text-xs text-red-700">
            {error}
          </div>
        )}

        <div className="flex justify-end gap-2 pt-3 border-t border-slate-100">
          <Button
            type="button"
            variant="outline"
            onClick={onClose}
            disabled={busy}
          >
            Cancel
          </Button>
          <Button
            type="submit"
            variant="primary"
            loading={busy}
          >
            {busy ? "Saving…" : "Save Record"}
          </Button>
        </div>
      </form>
    </Modal>
  );
}

export function TextField({
  label,
  value,
  onChange,
  required,
  type = "text",
  maxLength,
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
  required?: boolean;
  type?: string;
  maxLength?: number;
}) {
  return (
    <FormField label={label} required={required}>
      <Input
        type={type}
        required={required}
        value={value}
        maxLength={maxLength}
        onChange={(e) => onChange(e.target.value)}
      />
    </FormField>
  );
}
