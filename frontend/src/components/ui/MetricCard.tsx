import type { ReactNode } from "react";

export interface MetricCardProps {
  label: string;
  value: string | number;
  subtext?: string;
  hint?: string;
  icon?: ReactNode;
  tone?: "default" | "brand" | "gold" | "success" | "warning" | "danger" | "info" | "purple";
  trend?: {
    value?: string;
    label?: string;
    direction?: "up" | "down";
    isPositive?: boolean;
  };
  onClick?: () => void;
  className?: string;
}

interface GlassStyle {
  cardBg: string;
  borderColor: string;
  orbBg: string;
  topLight: string;
  labelColor: string;
  valueColor: string;
  iconBg: string;
  iconText: string;
  iconBorder: string;
  footerBorder: string;
  footerText: string;
  dotBg: string;
  cardShadow: string;
}

const toneGlassStyles: Record<string, GlassStyle> = {
  brand: {
    cardBg: "bg-gradient-to-br from-[#1e40af] via-[#2563eb] to-[#1e1b4b]",
    borderColor: "border-white/25 hover:border-white/45",
    orbBg: "bg-blue-300/30",
    topLight: "from-white/50 via-white/25 to-transparent",
    labelColor: "text-blue-100",
    valueColor: "text-white",
    iconBg: "bg-white/20 backdrop-blur-md shadow-inner",
    iconText: "text-white",
    iconBorder: "border-white/30",
    footerBorder: "border-white/20",
    footerText: "text-blue-100/90",
    dotBg: "bg-cyan-300 shadow-[0_0_8px_rgba(103,232,249,0.9)]",
    cardShadow: "shadow-[0_12px_28px_-6px_rgba(37,99,235,0.45),inset_0_1px_1px_0_rgba(255,255,255,0.35)]",
  },
  info: {
    cardBg: "bg-gradient-to-br from-[#0284c7] via-[#0ea5e9] to-[#0369a1]",
    borderColor: "border-white/25 hover:border-white/45",
    orbBg: "bg-cyan-300/30",
    topLight: "from-white/50 via-white/25 to-transparent",
    labelColor: "text-sky-100",
    valueColor: "text-white",
    iconBg: "bg-white/20 backdrop-blur-md shadow-inner",
    iconText: "text-white",
    iconBorder: "border-white/30",
    footerBorder: "border-white/20",
    footerText: "text-sky-100/90",
    dotBg: "bg-white shadow-[0_0_8px_rgba(255,255,255,0.9)]",
    cardShadow: "shadow-[0_12px_28px_-6px_rgba(2,132,199,0.45),inset_0_1px_1px_0_rgba(255,255,255,0.35)]",
  },
  warning: {
    cardBg: "bg-gradient-to-br from-[#d97706] via-[#ea580c] to-[#b45309]",
    borderColor: "border-white/25 hover:border-white/45",
    orbBg: "bg-yellow-300/30",
    topLight: "from-white/50 via-white/25 to-transparent",
    labelColor: "text-amber-100",
    valueColor: "text-white",
    iconBg: "bg-white/20 backdrop-blur-md shadow-inner",
    iconText: "text-white",
    iconBorder: "border-white/30",
    footerBorder: "border-white/20",
    footerText: "text-amber-100/90",
    dotBg: "bg-yellow-200 shadow-[0_0_8px_rgba(254,240,138,0.9)]",
    cardShadow: "shadow-[0_12px_28px_-6px_rgba(234,88,12,0.45),inset_0_1px_1px_0_rgba(255,255,255,0.35)]",
  },
  purple: {
    cardBg: "bg-gradient-to-br from-[#7c3aed] via-[#6366f1] to-[#4338ca]",
    borderColor: "border-white/25 hover:border-white/45",
    orbBg: "bg-fuchsia-300/30",
    topLight: "from-white/50 via-white/25 to-transparent",
    labelColor: "text-purple-100",
    valueColor: "text-white",
    iconBg: "bg-white/20 backdrop-blur-md shadow-inner",
    iconText: "text-white",
    iconBorder: "border-white/30",
    footerBorder: "border-white/20",
    footerText: "text-purple-100/90",
    dotBg: "bg-pink-300 shadow-[0_0_8px_rgba(244,114,182,0.9)]",
    cardShadow: "shadow-[0_12px_28px_-6px_rgba(124,58,237,0.45),inset_0_1px_1px_0_rgba(255,255,255,0.35)]",
  },
  default: {
    cardBg: "bg-gradient-to-br from-[#6366f1] via-[#4f46e5] to-[#312e81]",
    borderColor: "border-white/25 hover:border-white/45",
    orbBg: "bg-indigo-300/30",
    topLight: "from-white/50 via-white/25 to-transparent",
    labelColor: "text-indigo-100",
    valueColor: "text-white",
    iconBg: "bg-white/20 backdrop-blur-md shadow-inner",
    iconText: "text-white",
    iconBorder: "border-white/30",
    footerBorder: "border-white/20",
    footerText: "text-indigo-100/90",
    dotBg: "bg-indigo-200 shadow-[0_0_8px_rgba(199,210,254,0.9)]",
    cardShadow: "shadow-[0_12px_28px_-6px_rgba(99,102,241,0.45),inset_0_1px_1px_0_rgba(255,255,255,0.35)]",
  },
  success: {
    cardBg: "bg-gradient-to-br from-[#059669] via-[#0d9488] to-[#065f46]",
    borderColor: "border-white/25 hover:border-white/45",
    orbBg: "bg-emerald-300/30",
    topLight: "from-white/50 via-white/25 to-transparent",
    labelColor: "text-emerald-100",
    valueColor: "text-white",
    iconBg: "bg-white/20 backdrop-blur-md shadow-inner",
    iconText: "text-white",
    iconBorder: "border-white/30",
    footerBorder: "border-white/20",
    footerText: "text-emerald-100/90",
    dotBg: "bg-emerald-200 shadow-[0_0_8px_rgba(167,243,208,0.9)]",
    cardShadow: "shadow-[0_12px_28px_-6px_rgba(13,148,136,0.45),inset_0_1px_1px_0_rgba(255,255,255,0.35)]",
  },
  gold: {
    cardBg: "bg-gradient-to-br from-[#b45309] via-[#d97706] to-[#78350f]",
    borderColor: "border-white/25 hover:border-white/45",
    orbBg: "bg-amber-300/30",
    topLight: "from-white/50 via-white/25 to-transparent",
    labelColor: "text-amber-100",
    valueColor: "text-white",
    iconBg: "bg-white/20 backdrop-blur-md shadow-inner",
    iconText: "text-white",
    iconBorder: "border-white/30",
    footerBorder: "border-white/20",
    footerText: "text-amber-100/90",
    dotBg: "bg-yellow-200 shadow-[0_0_8px_rgba(254,240,138,0.9)]",
    cardShadow: "shadow-[0_12px_28px_-6px_rgba(217,119,6,0.45),inset_0_1px_1px_0_rgba(255,255,255,0.35)]",
  },
  danger: {
    cardBg: "bg-gradient-to-br from-[#e11d48] via-[#dc2626] to-[#991b1b]",
    borderColor: "border-white/25 hover:border-white/45",
    orbBg: "bg-rose-300/30",
    topLight: "from-white/50 via-white/25 to-transparent",
    labelColor: "text-rose-100",
    valueColor: "text-white",
    iconBg: "bg-white/20 backdrop-blur-md shadow-inner",
    iconText: "text-white",
    iconBorder: "border-white/30",
    footerBorder: "border-white/20",
    footerText: "text-rose-100/90",
    dotBg: "bg-rose-200 shadow-[0_0_8px_rgba(254,205,211,0.9)]",
    cardShadow: "shadow-[0_12px_28px_-6px_rgba(225,29,72,0.45),inset_0_1px_1px_0_rgba(255,255,255,0.35)]",
  },
};

export function MetricCard({
  label,
  value,
  subtext,
  hint,
  icon,
  tone = "default",
  trend,
  onClick,
  className = "",
}: MetricCardProps) {
  const note = subtext ?? hint;
  const isPositive =
    trend?.isPositive !== undefined
      ? trend.isPositive
      : trend?.direction === "up";
  const trendText = trend?.value ?? trend?.label;
  const formattedValue = typeof value === "number" ? value.toLocaleString() : value;
  const accent = toneGlassStyles[tone] ?? toneGlassStyles.default;

  return (
    <div
      onClick={onClick}
      className={`relative overflow-hidden rounded-2xl backdrop-blur-xl border ${accent.cardBg} ${accent.borderColor} ${accent.cardShadow} hover:shadow-2xl transition-all duration-300 flex flex-col justify-between group ${
        onClick ? "cursor-pointer hover:-translate-y-1" : ""
      } ${className}`}
    >
      {/* Glass Top Specular Light Highlight */}
      <div className={`h-[2px] w-full bg-gradient-to-r ${accent.topLight}`} />

      {/* Glass Surface Gloss Sheen Overlay */}
      <div className="pointer-events-none absolute inset-0 bg-gradient-to-b from-white/20 via-white/5 to-transparent rounded-2xl" />

      {/* Ambient glowing glass orb */}
      <div
        className={`pointer-events-none absolute -right-6 -bottom-6 w-32 h-32 rounded-full blur-2xl opacity-40 group-hover:opacity-65 transition-opacity duration-300 ${accent.orbBg}`}
      />

      <div className="p-4 sm:p-5 flex-1 flex flex-col justify-between relative z-10">
        <div>
          <div className="flex items-center justify-between gap-3">
            <p className={`text-[11px] font-bold uppercase tracking-wider truncate ${accent.labelColor}`} title={label}>
              {label}
            </p>
            {icon && (
              <div
                className={`h-9 w-9 rounded-xl flex items-center justify-center shrink-0 border transition-transform duration-300 group-hover:scale-105 ${accent.iconBg} ${accent.iconText} ${accent.iconBorder} [&>svg]:text-white [&>svg]:stroke-[2.25]`}
              >
                {icon}
              </div>
            )}
          </div>

          <div className="mt-3 flex items-baseline gap-2">
            <span className={`text-2xl sm:text-3xl font-extrabold tracking-tight font-sans tabular-nums leading-none ${accent.valueColor}`}>
              {formattedValue}
            </span>
            {trendText && (
              <span
                className={`inline-flex items-center text-xs font-semibold px-2 py-0.5 rounded-full backdrop-blur-md ${
                  isPositive
                    ? "bg-white/20 text-white border border-white/30"
                    : "bg-black/20 text-white border border-white/20"
                }`}
              >
                {isPositive ? "↑" : "↓"} {trendText}
              </span>
            )}
          </div>
        </div>

        {note && (
          <div className={`mt-3.5 pt-3 border-t flex items-center gap-2 text-xs ${accent.footerBorder}`}>
            <span className={`w-1.5 h-1.5 rounded-full shrink-0 ${accent.dotBg}`} />
            <p className={`truncate text-[11px] leading-tight font-medium ${accent.footerText}`} title={note}>
              {note}
            </p>
          </div>
        )}
      </div>
    </div>
  );
}

