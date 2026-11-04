import type { ReactNode } from "react";
import { Link } from "react-router-dom";

export interface BreadcrumbItem {
  label: string;
  to?: string;
}

export interface PageHeaderProps {
  title: string;
  description?: string;
  subtitle?: ReactNode;
  breadcrumbs?: BreadcrumbItem[];
  badge?: ReactNode;
  status?: ReactNode;
  actions?: ReactNode;
  className?: string;
}

export function PageHeader({
  title,
  description,
  subtitle,
  breadcrumbs,
  badge,
  status,
  actions,
  className = "",
}: PageHeaderProps) {
  const sub = subtitle ?? description;
  const statusElement = status ?? badge;

  return (
    <div className={`mb-4 sm:mb-5 ${className}`}>
      {breadcrumbs && breadcrumbs.length > 0 && (
        <nav aria-label="Breadcrumbs" className="mb-2 flex items-center gap-1.5 text-xs text-slate-500">
          {breadcrumbs.map((item, idx) => {
            const isLast = idx === breadcrumbs.length - 1;
            return (
              <span key={item.label} className="flex items-center gap-1.5">
                {idx > 0 && <span className="text-slate-300">/</span>}
                {item.to && !isLast ? (
                  <Link to={item.to} className="hover:text-slate-900 transition-colors">
                    {item.label}
                  </Link>
                ) : (
                  <span className={isLast ? "font-medium text-slate-700" : ""}>{item.label}</span>
                )}
              </span>
            );
          })}
        </nav>
      )}

      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <div className="flex items-center gap-2.5 flex-wrap">
            <h1 className="text-2xl font-bold tracking-tight text-slate-900 sm:text-3xl">{title}</h1>
            {statusElement && <div className="shrink-0">{statusElement}</div>}
          </div>
          {sub && <div className="mt-1 text-sm text-slate-500 max-w-3xl">{sub}</div>}
        </div>

        {actions && <div className="flex items-center gap-2.5 shrink-0 mt-2 sm:mt-0">{actions}</div>}
      </div>
    </div>
  );
}

