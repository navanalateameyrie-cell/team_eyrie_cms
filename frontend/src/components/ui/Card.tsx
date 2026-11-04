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
  className = "",
}: {
  title?: ReactNode;
  subtitle?: ReactNode;
  description?: ReactNode;
  icon?: ReactNode;
  action?: ReactNode;
  className?: string;
}) {
  const sub = subtitle ?? description;
  return (
    <div className={`flex items-start justify-between gap-4 border-b border-slate-100 px-5 py-3.5 ${className}`}>
      <div className="flex items-center gap-3 min-w-0">
        {icon && (
          <div className="h-8 w-8 rounded-lg bg-[#102747]/5 text-[#C49A52] border border-[#C49A52]/30 flex items-center justify-center shrink-0 shadow-2xs">
            {icon}
          </div>
        )}
        <div className="min-w-0">
          {title && <h3 className="font-bold text-slate-900 leading-tight">{title}</h3>}
          {sub && <p className="text-xs text-slate-500 mt-0.5">{sub}</p>}
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

