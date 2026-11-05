import { useEffect, useRef, useState } from "react";
import { NavLink, Outlet, useNavigate, useLocation } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import GlobalSearchBox from "./GlobalSearchBox";
import NotificationBell from "./NotificationBell";

interface NavGroup {
  label: string;
  items: {
    to: string;
    label: string;
    perm?: string;
    icon: React.ReactNode;
  }[];
}

interface BreadcrumbItem {
  label: string;
  to?: string;
  active?: boolean;
}

export default function Layout() {
  const { userName, roles, hasPermission, token, logout, logoutEverywhere } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [mobileOpen, setMobileOpen] = useState(false);
  const [userMenuOpen, setUserMenuOpen] = useState(false);
  const [mastersExpanded, setMastersExpanded] = useState(false);
  const [collapsed, setCollapsed] = useState(() => {
    try {
      return localStorage.getItem("cms.sidebar.collapsed") === "true";
    } catch {
      return false;
    }
  });
  const userMenuRef = useRef<HTMLDivElement>(null);

  function toggleCollapse() {
    setCollapsed((prev) => {
      const next = !prev;
      try {
        localStorage.setItem("cms.sidebar.collapsed", String(next));
      } catch {
        /* ignore */
      }
      return next;
    });
  }

  // Auto-close mobile drawer on route change
  useEffect(() => {
    setMobileOpen(false);
  }, [location.pathname]);

  // Handle click outside for user menu dropdown
  useEffect(() => {
    function onClickOutside(e: MouseEvent) {
      if (userMenuRef.current && !userMenuRef.current.contains(e.target as Node)) {
        setUserMenuOpen(false);
      }
    }
    document.addEventListener("mousedown", onClickOutside);
    return () => document.removeEventListener("mousedown", onClickOutside);
  }, []);

  const navGroups: NavGroup[] = [
    {
      label: "Practice Overview",
      items: [
        {
          to: "/dashboard",
          label: "Dashboard",
          perm: "clients.read",
          icon: (
            <svg className="w-4 h-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.75} d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
            </svg>
          ),
        },
        {
          to: "/admin/reports",
          label: "Reports & Analytics",
          perm: "reports.read",
          icon: (
            <svg className="w-4 h-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.75} d="M7 12l3-3 3 3 4-4M8 21l4-4 4 4M3 4h18M4 4h16v12a1 1 0 01-1 1H5a1 1 0 01-1-1V4z" />
            </svg>
          ),
        },
      ],
    },
    {
      label: "Client Management",
      items: [
        {
          to: "/admin/clients",
          label: "Clients Directory",
          perm: "clients.read",
          icon: (
            <svg className="w-4 h-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.75} d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
            </svg>
          ),
        },
      ],
    },
    {
      label: "Operations & Compliance",
      items: [
        {
          to: "/admin/compliance",
          label: "Compliance Calendar",
          perm: "compliance.read",
          icon: (
            <svg className="w-4 h-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.75} d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" />
            </svg>
          ),
        },
        {
          to: "/admin/timesheet",
          label: "Timesheet",
          perm: "timesheet.read",
          icon: (
            <svg className="w-4 h-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.75} d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          ),
        },
        {
          to: "/admin/staff-workload",
          label: "Staff Workload",
          perm: "staff.read",
          icon: (
            <svg className="w-4 h-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.75} d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
            </svg>
          ),
        },
        {
          to: "/admin/letter-templates",
          label: "Letter Templates",
          perm: "letters.read",
          icon: (
            <svg className="w-4 h-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.75} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
            </svg>
          ),
        },
      ],
    },
    {
      label: "Billing & Finance",
      items: [
        {
          to: "/admin/billing",
          label: "Invoices & Billing",
          perm: "billing.read",
          icon: (
            <svg className="w-4 h-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.75} d="M9 14l6-6m-5.5.5h.01m4.99 5h.01M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16l3.5-2 3.5 2 3.5-2 3.5 2z" />
            </svg>
          ),
        },
      ],
    },
    {
      label: "Practice Administration",
      items: [
        {
          to: "/admin/imports",
          label: "Data Imports",
          perm: "imports.read",
          icon: (
            <svg className="w-4 h-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.75} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" />
            </svg>
          ),
        },
        {
          to: "/admin/settings",
          label: "Practice Settings",
          perm: "settings.manage",
          icon: (
            <svg className="w-4 h-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.75} d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.75} d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
            </svg>
          ),
        },
      ],
    },
  ];

  const masterSubItems = [
    { to: "/admin/masters/services", label: "Services" },
    { to: "/admin/masters/entity-types", label: "Entity Types" },
    { to: "/admin/masters/states", label: "States & GST" },
    { to: "/admin/masters/client-groups", label: "Client Groups" },
  ];

  const isMastersActive = location.pathname.startsWith("/admin/masters");

  // Format breadcrumb segments from location path
  function getBreadcrumbs(): BreadcrumbItem[] {
    const path = location.pathname;
    if (path === "/dashboard" || path === "/") {
      return [{ label: "Practice Overview" }, { label: "Dashboard", active: true }];
    }
    if (path.startsWith("/admin/clients/")) {
      return [
        { label: "Clients", to: "/admin/clients" },
        { label: "Client Profile", active: true },
      ];
    }
    if (path === "/admin/clients") {
      return [{ label: "Client Management" }, { label: "Clients Directory", active: true }];
    }
    if (path === "/admin/compliance") {
      return [{ label: "Operations & Compliance" }, { label: "Compliance Calendar", active: true }];
    }
    if (path === "/admin/timesheet") {
      return [{ label: "Operations & Compliance" }, { label: "Timesheet Tracker", active: true }];
    }
    if (path === "/admin/staff-workload") {
      return [{ label: "Operations & Compliance" }, { label: "Staff Workload & Capacity", active: true }];
    }
    if (path === "/admin/letter-templates") {
      return [{ label: "Operations & Compliance" }, { label: "Letter & Notice Templates", active: true }];
    }
    if (path === "/admin/billing") {
      return [{ label: "Billing & Finance" }, { label: "Invoices & Receipts", active: true }];
    }
    if (path === "/admin/billing/reconcile" || path === "/admin/reconcile") {
      return [{ label: "Billing & Finance", to: "/admin/billing" }, { label: "Bank Reconciliation", active: true }];
    }
    if (path === "/admin/reports") {
      return [{ label: "Practice Overview" }, { label: "Reports & Analytics", active: true }];
    }
    if (path.startsWith("/admin/imports")) {
      return [{ label: "Administration" }, { label: "Data Imports", active: true }];
    }
    if (path.startsWith("/admin/masters")) {
      return [{ label: "Reference Data" }, { label: "Masters", active: true }];
    }
    if (path === "/admin/settings") {
      return [{ label: "Administration" }, { label: "Practice Settings", active: true }];
    }
    if (path === "/admin/audit") {
      return [{ label: "Administration" }, { label: "Audit Activity Log", active: true }];
    }
    return [{ label: "Practice Management" }, { label: "Overview", active: true }];
  }

  const breadcrumbs = getBreadcrumbs();

  const sidebarContent = (
    <div className={`flex flex-col h-full bg-[#0C1E36] text-slate-300 transition-all duration-200 select-none ${collapsed ? "w-20" : "w-[255px]"}`}>
      {/* Brand Header */}
      <div className={`py-4 border-b border-[#19385F] flex items-center justify-between ${collapsed ? "px-3 justify-center" : "px-5"}`}>
        <NavLink to="/dashboard" className="flex items-center gap-3 group">
          <div className="h-9 w-9 rounded-lg bg-gradient-to-br from-[#C59A52] to-[#B08642] text-[#0C1E36] font-black flex items-center justify-center text-sm shadow-md group-hover:brightness-110 transition-all shrink-0">
            CA
          </div>
          {!collapsed && (
            <div className="min-w-0">
              <div className="text-white font-bold tracking-wide leading-tight text-base flex items-center gap-1.5">
                <span>CMS</span>
                <span className="text-[#E8C98F]">_CA</span>
                <span className="text-[9px] font-mono text-[#E8C98F] bg-[#C59A52]/15 border border-[#C59A52]/30 px-1 py-0.2 rounded font-semibold tracking-wider">
                  ERP
                </span>
              </div>
              <div className="text-[10px] text-[#E8C98F] font-semibold tracking-wider uppercase leading-tight truncate">
                Practice Management
              </div>
            </div>
          )}
        </NavLink>
        {mobileOpen && (
          <button
            type="button"
            onClick={() => setMobileOpen(false)}
            className="p-1 rounded text-slate-400 hover:text-white lg:hidden"
            aria-label="Close navigation"
          >
            ✕
          </button>
        )}
      </div>

      {/* Nav Link Groups */}
      <div className="flex-1 overflow-y-auto px-2 py-3 space-y-4 text-xs scrollbar-thin">
        {navGroups.map((group) => {
          const visibleItems = group.items.filter((item) => !item.perm || hasPermission(item.perm));
          if (visibleItems.length === 0) return null;

          return (
            <div key={group.label} className="space-y-1">
              {!collapsed ? (
                <div className="px-3 text-[10px] font-bold uppercase tracking-wider text-[#98A4B5]">
                  {group.label}
                </div>
              ) : (
                <div className="my-2 border-t border-[#19385F] mx-2" title={group.label} />
              )}
              <div className="space-y-0.5 mt-1">
                {visibleItems.map((item) => (
                  <NavLink
                    key={item.to}
                    to={item.to}
                    end={item.to === "/dashboard" || item.to === "/"}
                    title={collapsed ? item.label : undefined}
                    className={({ isActive }) =>
                      `group flex items-center rounded-xl font-medium transition-all duration-150 ${collapsed
                        ? "justify-center p-2.5 mx-auto"
                        : "gap-2.5 px-3 py-2 border-l-4"
                      } ${isActive
                        ? `${collapsed ? "" : "border-[#E8C98F]"} bg-gradient-to-r from-[#C59A52] to-[#B08642] text-white font-semibold shadow-sm hover:brightness-105`
                        : `${collapsed ? "" : "border-transparent hover:border-[#D47E38]/85"} text-slate-300 hover:bg-gradient-to-r hover:from-[#C87432]/22 hover:via-[#B86221]/16 hover:to-[#C87432]/8 hover:text-[#FFF2E5]`
                      }`
                    }
                  >
                    {({ isActive }) => (
                      <>
                        <span className={`shrink-0 transition-colors duration-150 ${isActive ? "text-white" : "text-slate-300 group-hover:text-[#FDBA74]"}`}>
                          {item.icon}
                        </span>
                        {!collapsed && <span className="truncate">{item.label}</span>}
                      </>
                    )}
                  </NavLink>
                ))}
              </div>
            </div>
          );
        })}

        {/* Master Data Collapsible section */}
        {hasPermission("masters.read") && (
          <div className="space-y-1">
            {!collapsed ? (
              <div className="px-3 text-[10px] font-bold uppercase tracking-wider text-[#98A4B5]">
                Reference Data
              </div>
            ) : (
              <div className="my-2 border-t border-[#19385F] mx-2" title="Reference Data" />
            )}
            <div className="space-y-0.5 mt-1">
              <button
                type="button"
                onClick={() => setMastersExpanded((v) => !v)}
                title={collapsed ? "Reference Masters" : undefined}
                className={`group w-full flex items-center rounded-xl font-medium transition-all duration-150 text-left ${collapsed
                  ? "justify-center p-2.5"
                  : "justify-between px-3 py-2 border-l-4"
                  } ${isMastersActive
                    ? `${collapsed ? "" : "border-[#E8C98F]"} bg-gradient-to-r from-[#C59A52] to-[#B08642] text-white font-semibold shadow-sm hover:brightness-105`
                    : `${collapsed ? "" : "border-transparent hover:border-[#D47E38]/85"} text-slate-300 hover:bg-gradient-to-r hover:from-[#C87432]/22 hover:via-[#B86221]/16 hover:to-[#C87432]/8 hover:text-[#FFF2E5]`
                  }`}
              >
                <div className={`flex items-center ${collapsed ? "justify-center" : "gap-2.5"}`}>
                  <svg className={`w-4 h-4 shrink-0 transition-colors duration-150 ${isMastersActive ? "text-white" : "text-slate-300 group-hover:text-[#FDBA74]"}`} fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.75} d="M4 7v10c0 2 1 3 3 3h10c2 0 3-1 3-3V7c0-2-1-3-3-3H7C5 4 4 5 4 7zM9 9h6M9 13h6M9 17h4" />
                  </svg>
                  {!collapsed && <span>Masters</span>}
                </div>
                {!collapsed && (
                  <span className={`text-[10px] transition-colors duration-150 ${isMastersActive ? "text-white" : "text-slate-400 group-hover:text-[#FDBA74]"}`}>
                    {mastersExpanded || isMastersActive ? "▾" : "▸"}
                  </span>
                )}
              </button>

              {(!collapsed && (mastersExpanded || isMastersActive)) && (
                <div className="pl-6 space-y-0.5 pt-1 border-l border-[#19385F] ml-4">
                  {masterSubItems.map((sub) => (
                    <NavLink
                      key={sub.to}
                      to={sub.to}
                      className={({ isActive }) =>
                        `block px-2.5 py-1.5 rounded-lg text-[11px] transition-colors duration-150 ${isActive
                          ? "text-[#E8C98F] font-semibold bg-[#19385F]"
                          : "text-slate-400 hover:text-[#FFF2E5] hover:bg-[#C87432]/20"
                        }`
                      }
                    >
                      {sub.label}
                    </NavLink>
                  ))}
                </div>
              )}
            </div>
          </div>
        )}
      </div>

      {/* Sidebar Collapse Toggle & User Profile Card */}
      <div className="p-2 border-t border-[#19385F] bg-[#102747] space-y-2">
        {/* Collapse button for desktop */}
        <div className="hidden lg:flex items-center justify-end">
          <button
            type="button"
            onClick={toggleCollapse}
            title={collapsed ? "Expand sidebar" : "Collapse sidebar"}
            className="group w-full flex items-center justify-center gap-2 py-1.5 px-2 rounded-lg text-slate-400 hover:text-[#FFF2E5] hover:bg-[#C87432]/15 text-[11px] transition-colors duration-150"
          >
            <svg
              className={`w-4 h-4 transition-transform duration-150 group-hover:text-[#FDBA74] ${collapsed ? "rotate-180" : ""}`}
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
            >
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11 19l-7-7 7-7m8 14l-7-7 7-7" />
            </svg>
            {!collapsed && <span>Collapse Sidebar</span>}
          </button>
        </div>

        {token ? (
          <div className={`flex items-center gap-2 p-2 rounded-xl bg-[#0C1E36] border border-[#19385F] ${collapsed ? "justify-center" : "justify-between"}`}>
            <div className="flex items-center gap-2.5 min-w-0">
              <div className="h-7 w-7 rounded-full bg-[#102747] text-[#C59A52] border border-[#C59A52]/40 flex items-center justify-center font-bold text-xs uppercase shrink-0 shadow-xs">
                {userName ? userName.charAt(0) : "U"}
              </div>
              {!collapsed && (
                <div className="min-w-0 flex-1">
                  <div className="text-xs font-semibold text-white truncate">{userName}</div>
                  <div className="text-[10px] text-[#E8C98F] truncate font-mono">
                    {roles[0] ?? "Staff"}
                  </div>
                </div>
              )}
            </div>
            {!collapsed && (
              <button
                type="button"
                onClick={async () => {
                  await logout();
                  navigate("/login");
                }}
                className="p-1.5 rounded-lg text-slate-400 hover:text-[#FDBA74] hover:bg-[#C87432]/20 transition-colors shrink-0"
                title="Sign Out"
              >
                <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
                </svg>
              </button>
            )}
          </div>
        ) : (
          <NavLink
            to="/login"
            className="flex items-center justify-center w-full py-2 px-3 rounded-xl bg-[#C59A52] hover:bg-[#b08642] text-white font-medium text-xs shadow-sm transition-colors"
          >
            {collapsed ? "Sign In" : "Sign in to Portal"}
          </NavLink>
        )}
      </div>
    </div>
  );

  return (
    <div className="min-h-screen flex bg-[#F3F5FA] font-sans text-[#142743]">
      {/* Desktop Sidebar */}
      <aside className={`hidden lg:flex lg:flex-col lg:fixed lg:inset-y-0 z-30 shadow-lg border-r border-[#19385F] transition-all duration-200 ${collapsed ? "lg:w-20" : "lg:w-[255px]"}`}>
        {sidebarContent}
      </aside>

      {/* Mobile Drawer Overlay */}
      {mobileOpen && (
        <div className="fixed inset-0 z-50 lg:hidden flex">
          <div
            className="fixed inset-0 bg-slate-900/60 backdrop-blur-sm"
            onClick={() => setMobileOpen(false)}
            aria-hidden="true"
          />
          <div className="relative flex-1 flex flex-col max-w-xs w-full bg-[#0C1E36] shadow-xl z-10">
            {sidebarContent}
          </div>
        </div>
      )}

      {/* Main Layout Area */}
      <div className={`flex-1 flex flex-col min-h-screen min-w-0 transition-all duration-200 ${collapsed ? "lg:pl-20" : "lg:pl-[255px]"}`}>
        {/* Top Navbar */}
        <header className="sticky top-0 z-20 bg-[#EEF4FB] border-b border-[#D4E2F0] shadow-2xs backdrop-blur-md">
          <div className="w-full px-4 sm:px-6 lg:px-8 py-3 flex items-center justify-between gap-4">
            {/* Mobile hamburger & Breadcrumbs */}
            <div className="flex items-center gap-3 min-w-0">
              <button
                type="button"
                onClick={() => setMobileOpen(true)}
                className="lg:hidden p-2 rounded-lg text-slate-600 hover:text-[#102747] hover:bg-white/80 focus:outline-none"
                aria-label="Open sidebar"
              >
                <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 6h16M4 12h16M4 18h16" />
                </svg>
              </button>

              {/* Contextual Breadcrumbs */}
              <nav aria-label="Breadcrumb" className="hidden sm:flex items-center gap-2 text-xs">
                {breadcrumbs.map((crumb, idx) => (
                  <div key={idx} className="flex items-center gap-2">
                    {idx > 0 && <span className="text-slate-400 font-semibold">/</span>}
                    {crumb.to ? (
                      <NavLink to={crumb.to} className="text-[#556987] hover:text-[#102747] font-medium transition-colors">
                        {crumb.label}
                      </NavLink>
                    ) : crumb.active ? (
                      <span className="text-[#102747] font-bold">{crumb.label}</span>
                    ) : (
                      <span className="text-[#556987] font-medium">{crumb.label}</span>
                    )}
                  </div>
                ))}
              </nav>
            </div>

            {/* Right toolbar: Search, Notifications, User Menu */}
            <div className="flex items-center gap-2.5 sm:gap-3.5 shrink-0">
              {/* FY 2024-25 status badge (as seen in reference Image 1 & 5) */}
              <div className="hidden md:inline-flex items-center gap-2 px-3 py-1.5 rounded-lg bg-white/90 border border-[#CBDCE9] shadow-2xs text-xs font-semibold text-[#102747]">
                <span className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse shrink-0" />
                <span>FY 2024-25</span>
              </div>

              {token && <GlobalSearchBox />}
              {token && <NotificationBell />}

              <div className="h-6 w-px bg-[#CBDCE9] hidden sm:block" />

              {/* User Dropdown Menu */}
              {token ? (
                <div className="relative" ref={userMenuRef}>
                  <button
                    type="button"
                    onClick={() => setUserMenuOpen((v) => !v)}
                    className="flex items-center gap-2.5 p-1.5 rounded-xl text-left bg-white/70 hover:bg-white border border-transparent hover:border-[#CBDCE9] transition-all focus:outline-none focus:ring-2 focus:ring-[#C59A52]/40"
                  >
                    <div className="h-8 w-8 rounded-full bg-[#102747] text-[#C59A52] border border-[#C59A52]/30 flex items-center justify-center font-bold text-xs uppercase shadow-xs">
                      {userName ? userName.charAt(0) : "U"}
                    </div>
                    <div className="hidden md:block text-left">
                      <div className="text-xs font-semibold text-[#102747] max-w-[120px] truncate leading-tight">
                        {userName}
                      </div>
                      <div className="text-[10px] text-slate-500 leading-tight font-mono">
                        {roles[0] ?? "Staff"}
                      </div>
                    </div>
                    <span className="text-slate-400 text-xs hidden md:inline">▾</span>
                  </button>

                  {userMenuOpen && (
                    <div className="absolute right-0 mt-2 w-56 rounded-xl border border-slate-200 bg-white shadow-2xl z-50 py-1 divide-y divide-slate-100 animate-in fade-in slide-in-from-top-1 duration-150">
                      <div className="px-4 py-3 bg-slate-50/70">
                        <p className="text-xs font-semibold text-slate-900 truncate">{userName}</p>
                        <p className="text-[11px] text-slate-500 truncate mt-0.5 font-mono">
                          {roles.join(", ") || "Staff User"}
                        </p>
                      </div>

                      <div className="py-1">
                        <NavLink
                          to="/admin/account/mfa"
                          onClick={() => setUserMenuOpen(false)}
                          className="flex items-center gap-2 px-4 py-2 text-xs text-slate-700 hover:bg-slate-50 hover:text-navy-900 font-medium"
                        >
                          <span>🔒</span>
                          <span>Multi-Factor Auth (MFA)</span>
                        </NavLink>
                        {hasPermission("audit.read") && (
                          <NavLink
                            to="/admin/audit"
                            onClick={() => setUserMenuOpen(false)}
                            className="flex items-center gap-2 px-4 py-2 text-xs text-slate-700 hover:bg-slate-50 hover:text-navy-900 font-medium"
                          >
                            <span>📋</span>
                            <span>Audit Activity Log</span>
                          </NavLink>
                        )}
                      </div>

                      <div className="py-1">
                        <button
                          type="button"
                          onClick={async () => {
                            setUserMenuOpen(false);
                            await logout();
                            navigate("/login");
                          }}
                          className="w-full text-left px-4 py-2 text-xs text-rose-600 hover:bg-rose-50 font-medium"
                        >
                          Sign Out
                        </button>
                        <button
                          type="button"
                          onClick={async () => {
                            setUserMenuOpen(false);
                            await logoutEverywhere();
                            navigate("/login");
                          }}
                          className="w-full text-left px-4 py-2 text-[11px] text-slate-500 hover:bg-slate-50"
                        >
                          Sign out of all devices
                        </button>
                      </div>
                    </div>
                  )}
                </div>
              ) : (
                <NavLink
                  to="/login"
                  className="rounded-lg bg-brand-500 hover:bg-brand-600 text-white text-xs font-medium px-3.5 py-1.5 shadow transition-colors"
                >
                  Sign In
                </NavLink>
              )}
            </div>
          </div>
        </header>

        {/* Dynamic Page Content */}
        <main className="flex-1 w-full px-4 sm:px-6 lg:px-8 py-5 sm:py-6">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
