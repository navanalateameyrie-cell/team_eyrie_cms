import type { HTMLAttributes, ReactNode } from "react";

export function Card({ className = "", children, ...props }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      className={`rounded-xl border border-slate-200/90 bg-white shadow-card transition-shadow ${className}`}
      {...props}
    >
      {children}
    </div>
  );
}

export function CardHeader({
  title,
  subtitle,
  description,
  icon,
  action,
  variant = "light",
  className = "",
}: {
  title?: ReactNode;
  subtitle?: ReactNode;
  description?: ReactNode;
  icon?: ReactNode;
  action?: ReactNode;
  variant?: "light" | "navy";
  className?: string;
}) {
  const sub = subtitle ?? description;
  const isNavy = variant === "navy";

  return (
    <div
      className={`flex items-start justify-between gap-4 px-5 py-3.5 ${
        isNavy
          ? "bg-[#102747] text-white border-b border-[#19385F] rounded-t-2xl"
          : "border-b border-slate-100"
      } ${className}`}
    >
      <div className="flex items-center gap-3 min-w-0">
        {icon && (
          <div
            className={`h-8 w-8 rounded-full flex items-center justify-center shrink-0 shadow-2xs ${
              isNavy
                ? "bg-white/10 text-[#C59A52] border border-[#C59A52]/30"
                : "bg-[#102747]/5 text-[#C49A52] border border-[#C49A52]/30 rounded-lg"
            }`}
          >
            {icon}
          </div>
        )}
        <div className="min-w-0">
          {title && (
            <h3 className={`font-bold leading-tight ${isNavy ? "text-white text-sm" : "text-slate-900"}`}>
              {title}
            </h3>
          )}
          {sub && (
            <p className={`text-xs mt-0.5 leading-tight ${isNavy ? "text-slate-300" : "text-slate-500"}`}>
              {sub}
            </p>
          )}
        </div>
      </div>
      {action && <div className="shrink-0">{action}</div>}
    </div>
  );
}

export function CardContent({ className = "", children, ...props }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div className={`p-5 ${className}`} {...props}>
      {children}
    </div>
  );
}

export function CardFooter({ className = "", children, ...props }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div className={`border-t border-slate-100 bg-slate-50/50 px-5 py-3 rounded-b-xl ${className}`} {...props}>
      {children}
    </div>
  );
}

