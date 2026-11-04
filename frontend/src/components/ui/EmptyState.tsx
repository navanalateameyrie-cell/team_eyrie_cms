import type { ReactNode } from "react";
import { Button } from "./Button";

export interface EmptyStateProps {
  title: string;
  description: string;
  icon?: ReactNode;
  action?: ReactNode;
  actionLabel?: string;
  onAction?: () => void;
  className?: string;
}

export function EmptyState({
  title,
  description,
  icon,
  action,
  actionLabel,
  onAction,
  className = "",
}: EmptyStateProps) {
  return (
    <div
      className={`flex flex-col items-center justify-center p-8 sm:p-12 text-center rounded-xl border border-dashed border-slate-300 bg-white ${className}`}
    >
      <div className="flex h-12 w-12 items-center justify-center rounded-full bg-slate-100 text-slate-500 mb-3 text-xl">
        {icon ?? (
          <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={1.5}
              d="M20 13V6a2 2 0 00-2-2H6a2 2 0 00-2 2v7m16 0v5a2 2 0 01-2 2H6a2 2 0 01-2-2v-5m16 0h-2.586a1 1 0 00-.707.293l-2.414 2.414a1 1 0 01-.707.293h-3.172a1 1 0 01-.707-.293l-2.414-2.414A1 1 0 006.586 13H4"
            />
          </svg>
        )}
      </div>
      <h3 className="text-base font-semibold text-slate-900">{title}</h3>
      <p className="mt-1 text-sm text-slate-500 max-w-sm">{description}</p>
      {action ? (
        <div className="mt-5">{action}</div>
      ) : actionLabel && onAction ? (
        <div className="mt-5">
          <Button variant="primary" size="sm" onClick={onAction}>
            {actionLabel}
          </Button>
        </div>
      ) : null}
    </div>
  );
}

export function LoadingSpinner({
  size = "md",
  label,
  text,
}: {
  size?: "sm" | "md" | "lg";
  label?: string;
  text?: string;
}) {
  const displayText = label ?? text ?? "Loading…";
  const sizeMap = {
    sm: "h-4 w-4 border-2",
    md: "h-6 w-6 border-2",
    lg: "h-9 w-9 border-3",
  };

  return (
    <div className="flex items-center justify-center gap-2.5 text-slate-500">
      <div
        className={`animate-spin rounded-full border-slate-200 border-t-brand-500 ${sizeMap[size]}`}
        role="status"
        aria-label="Loading"
      />
      {displayText && <span className="text-xs font-medium text-slate-500">{displayText}</span>}
    </div>
  );
}

export function SkeletonRow({ cols = 4 }: { cols?: number }) {
  return (
    <div className="animate-pulse flex items-center gap-4 py-3 border-b border-slate-100">
      {Array.from({ length: cols }).map((_, i) => (
        <div
          key={i}
          className={`h-4 bg-slate-200 rounded ${i === 0 ? "w-28" : i === 1 ? "w-44 flex-1" : "w-20"}`}
        />
      ))}
    </div>
  );
}

export function ErrorState({
  title = "Something went wrong",
  description,
  message,
  onRetry,
}: {
  title?: string;
  description?: string;
  message?: string;
  onRetry?: () => void;
}) {
  const note = message ?? description ?? "An error occurred while loading this data.";
  return (
    <div className="rounded-xl border border-rose-200 bg-rose-50/50 p-6 text-center">
      <div className="inline-flex h-10 w-10 items-center justify-center rounded-full bg-rose-100 text-rose-600 mb-2">
        <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            strokeWidth={2}
            d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
          />
        </svg>
      </div>
      <h3 className="text-sm font-semibold text-rose-900">{title}</h3>
      <p className="mt-1 text-xs text-rose-700 max-w-sm mx-auto">{note}</p>
      {onRetry && (
        <div className="mt-4">
          <Button variant="outline" size="sm" onClick={onRetry} className="bg-white">
            Try again
          </Button>
        </div>
      )}
    </div>
  );
}
