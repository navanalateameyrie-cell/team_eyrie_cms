import type { ReactNode } from "react";

export type BadgeTone = "neutral" | "brand" | "success" | "warning" | "danger" | "info" | "purple" | "gold";

export interface BadgeProps {
  children: ReactNode;
  tone?: BadgeTone;
  variant?: BadgeTone | "primary";
  size?: "sm" | "md";
  dot?: boolean;
  className?: string;
}

const toneStyles: Record<BadgeTone, { bg: string; text: string; dot: string; border: string }> = {
  neutral: {
    bg: "bg-slate-100",
    text: "text-slate-700",
    dot: "bg-slate-400",
    border: "border-slate-200",
  },
  brand: {
    bg: "bg-navy-50",
    text: "text-navy-800",
    dot: "bg-navy-700",
    border: "border-navy-200",
  },
  gold: {
    bg: "bg-[#FFF5ED]",
    text: "text-[#C86A24]",
    dot: "bg-[#C86A24]",
    border: "border-[#FDBA74]/60",
  },
  success: {
    bg: "bg-emerald-50",
    text: "text-emerald-700",
    dot: "bg-emerald-500",
    border: "border-emerald-200",
  },
  warning: {
    bg: "bg-amber-50",
    text: "text-amber-800",
    dot: "bg-amber-500",
    border: "border-amber-200",
  },
  danger: {
    bg: "bg-rose-50",
    text: "text-rose-700",
    dot: "bg-rose-500",
    border: "border-rose-200",
  },
  info: {
    bg: "bg-sky-50",
    text: "text-sky-700",
    dot: "bg-sky-500",
    border: "border-sky-200",
  },
  purple: {
    bg: "bg-purple-50",
    text: "text-purple-700",
    dot: "bg-purple-500",
    border: "border-purple-200",
  },
};

export function Badge({
  children,
  tone,
  variant,
  size = "sm",
  dot = false,
  className = "",
}: BadgeProps) {
  const resolvedTone: BadgeTone =
    (variant === "primary" ? "brand" : variant) ?? tone ?? "neutral";
  const styles = toneStyles[resolvedTone] ?? toneStyles.neutral;
  const sizeCls = size === "sm" ? "px-2 py-0.5 text-[11px]" : "px-2.5 py-1 text-xs";

  return (
    <span
      className={`inline-flex items-center gap-1.5 font-medium rounded-full border ${styles.bg} ${styles.text} ${styles.border} ${sizeCls} ${className}`}
    >
      {dot && <span className={`h-1.5 w-1.5 rounded-full ${styles.dot}`} />}
      {children}
    </span>
  );
}

export interface StatusBadgeProps {
  status: string | boolean;
  size?: "sm" | "md";
  pulse?: boolean;
  className?: string;
}

export function StatusBadge({ status, size = "sm", pulse = false, className = "" }: StatusBadgeProps) {
  const normalized = typeof status === "boolean" ? (status ? "active" : "inactive") : status.toLowerCase();

  let tone: BadgeTone = "neutral";
  let label = String(status);

  if (["active", "open", "filed", "completed", "approved", "reconciled"].includes(normalized)) {
    tone = "success";
    label = typeof status === "boolean" ? "Active" : status;
  } else if (["inactive", "closed", "waived", "cancelled", "rejected"].includes(normalized)) {
    tone = "neutral";
    label = typeof status === "boolean" ? "Inactive" : status;
  } else if (["pending", "in progress", "inprogress", "onhold", "planned", "submitted"].includes(normalized)) {
    tone = "warning";
  } else if (["overdue", "delayed", "failed", "unpaid", "disputed"].includes(normalized)) {
    tone = "danger";
  }

  return (
    <Badge tone={tone} size={size} dot={pulse} className={className}>
      {label}
    </Badge>
  );
}

