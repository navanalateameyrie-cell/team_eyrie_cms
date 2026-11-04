import type { ReactNode } from "react";
import { Modal } from "./Modal";
import { Button } from "./Button";

export interface ConfirmDialogProps {
  open: boolean;
  onClose: () => void;
  onConfirm: () => void | Promise<void>;
  title: string;
  description: ReactNode;
  confirmLabel?: string;
  cancelLabel?: string;
  tone?: "danger" | "warning" | "primary";
  variant?: "danger" | "warning" | "primary";
  busy?: boolean;
  loading?: boolean;
}

export function ConfirmDialog({
  open,
  onClose,
  onConfirm,
  title,
  description,
  confirmLabel = "Confirm",
  cancelLabel = "Cancel",
  tone,
  variant,
  busy,
  loading,
}: ConfirmDialogProps) {
  const resolvedTone = variant ?? tone ?? "primary";
  const isBusy = loading !== undefined ? loading : busy ?? false;
  const buttonVariant = resolvedTone === "danger" ? "danger" : "primary";

  return (
    <Modal open={open} onClose={onClose} maxWidth="sm">
      <div className="text-center sm:text-left">
        <div className="flex items-center gap-3 mb-3">
          <div
            className={`mx-auto sm:mx-0 flex h-10 w-10 shrink-0 items-center justify-center rounded-full ${
              resolvedTone === "danger"
                ? "bg-rose-100 text-rose-600"
                : resolvedTone === "warning"
                ? "bg-amber-100 text-amber-600"
                : "bg-brand-100 text-brand-600"
            }`}
          >
            {resolvedTone === "danger" ? (
              <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
                />
              </svg>
            ) : (
              <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"
                />
              </svg>
            )}
          </div>
          <h4 className="text-base font-semibold text-slate-900">{title}</h4>
        </div>

        <div className="text-xs text-slate-500 mb-6">{description}</div>

        <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-100">
          <Button variant="outline" size="sm" onClick={onClose} disabled={isBusy}>
            {cancelLabel}
          </Button>
          <Button
            variant={buttonVariant}
            size="sm"
            onClick={onConfirm}
            loading={isBusy}
          >
            {confirmLabel}
          </Button>
        </div>
      </div>
    </Modal>
  );
}

