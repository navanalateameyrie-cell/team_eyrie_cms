/**
 * Centralized formatting utilities for the Indian CA domain and financial SaaS UI.
 */

export function formatInr(amount: number | null | undefined, fractionDigits = 2): string {
  if (amount == null || isNaN(amount)) return "₹ 0.00";
  return amount.toLocaleString("en-IN", {
    style: "currency",
    currency: "INR",
    minimumFractionDigits: fractionDigits,
    maximumFractionDigits: fractionDigits,
  });
}

export function formatInrCompact(amount: number | null | undefined): string {
  if (amount == null || isNaN(amount)) return "₹ 0";
  if (Math.abs(amount) >= 10_000_000) {
    return `₹ ${(amount / 10_000_000).toFixed(2)} Cr`;
  }
  if (Math.abs(amount) >= 100_000) {
    return `₹ ${(amount / 100_000).toFixed(2)} L`;
  }
  return formatInr(amount, 0);
}

export function formatDate(date: string | Date | null | undefined, format: "short" | "medium" | "long" = "medium"): string {
  if (!date) return "—";
  const d = typeof date === "string" ? new Date(date) : date;
  if (isNaN(d.getTime())) {
    // If it's a date-only string like "2026-10-23"
    if (typeof date === "string" && /^\d{4}-\d{2}-\d{2}$/.test(date)) {
      const [year, month, day] = date.split("-").map(Number);
      const temp = new Date(year, month - 1, day);
      return temp.toLocaleDateString("en-IN", { day: "2-digit", month: "short", year: "numeric" });
    }
    return "—";
  }

  if (format === "short") {
    return d.toLocaleDateString("en-IN", { day: "2-digit", month: "2-digit", year: "2-digit" });
  }
  if (format === "long") {
    return d.toLocaleDateString("en-IN", { day: "numeric", month: "long", year: "numeric" });
  }
  return d.toLocaleDateString("en-IN", { day: "2-digit", month: "short", year: "numeric" });
}

export function formatDateTime(date: string | Date | null | undefined): string {
  if (!date) return "—";
  const d = typeof date === "string" ? new Date(date) : date;
  if (isNaN(d.getTime())) return "—";
  return `${formatDate(d)} ${d.toLocaleTimeString("en-IN", { hour: "2-digit", minute: "2-digit" })}`;
}

export function formatPan(pan: string | null | undefined): string {
  if (!pan) return "—";
  return pan.trim().toUpperCase();
}

export function formatGstin(gstin: string | null | undefined): string {
  if (!gstin) return "—";
  return gstin.trim().toUpperCase();
}

export function formatFinancialYear(fy: string | null | undefined): string {
  if (!fy) return "—";
  return fy.startsWith("FY") ? fy : `FY ${fy}`;
}

export function getQuarterLabel(q: number | string): string {
  switch (String(q)) {
    case "1": return "Q1 (Apr – Jun)";
    case "2": return "Q2 (Jul – Sep)";
    case "3": return "Q3 (Oct – Dec)";
    case "4": return "Q4 (Jan – Mar)";
    default: return `Q${q}`;
  }
}

