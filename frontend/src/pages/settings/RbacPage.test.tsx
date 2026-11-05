import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { BrowserRouter } from "react-router-dom";
import RbacPage from "./RbacPage";

vi.mock("../../auth/AuthContext", () => ({
  useAuth: () => ({
    roles: ["SystemAdmin"],
    permissions: ["admin.full", "users.manage", "clients.read"],
    userName: "admin@cms.local",
    hasPermission: (p: string) => p === "users.manage" || p === "admin.full",
  }),
}));

vi.mock("../../api/rbac", () => ({
  rbacApi: {
    getModules: vi.fn().mockResolvedValue([
      {
        key: "clients",
        name: "Clients Directory & Profiles",
        group: "Client Management",
        menuPerm: "clients.read",
        createPerm: "clients.create",
        updatePerm: "clients.update",
        deletePerm: "clients.delete",
        specialPerms: [],
      },
    ]),
    getPermissions: vi.fn().mockResolvedValue([
      { id: "clients.read", name: "View Clients", description: "Browse client directory", group: "Client Management" },
      { id: "clients.write", name: "Manage Clients", description: "Create and edit clients", group: "Client Management" },
      { id: "billing.read", name: "View Invoices", description: "Access billing ledgers", group: "Billing & Finance" },
    ]),
    getRoles: vi.fn().mockResolvedValue([
      {
        id: "role-1",
        name: "Manager",
        description: "Operations & Practice Manager",
        isSystemRole: true,
        userCount: 2,
        permissions: ["clients.read", "clients.write"],
      },
      {
        id: "role-2",
        name: "StaffUser",
        description: "Standard Staff Member",
        isSystemRole: true,
        userCount: 5,
        permissions: ["clients.read"],
      },
    ]),
    getUsers: vi.fn().mockResolvedValue([
      {
        id: "user-1",
        email: "staff.smith@cms.local",
        userName: "staff.smith@cms.local",
        staffId: "s-1",
        staffName: "John Smith",
        staffCode: "JS-01",
        clientId: null,
        clientName: null,
        roles: ["Manager"],
        isEnabled: true,
        createdAtUtc: "2026-01-01T00:00:00Z",
        lastLoginAtUtc: null,
      },
    ]),
  },
}));

vi.mock("../../api/masters", () => ({
  mastersApi: {
    staff: {
      list: vi.fn().mockResolvedValue([]),
    },
  },
}));

describe("RbacPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders the RBAC header, tabs, and role selector cards", async () => {
    render(
      <BrowserRouter>
        <RbacPage />
      </BrowserRouter>
    );

    expect(screen.getByText(/Loading Role-Based Access Control matrix/i)).toBeInTheDocument();

    await waitFor(() => {
      expect(screen.getByText("Role-Based Access Control (RBAC)")).toBeInTheDocument();
    });

    expect(screen.getByText(/Role Permission Matrix/i)).toBeInTheDocument();
    expect(screen.getByText(/User Accounts & Assignments/i)).toBeInTheDocument();
    expect(screen.getByText("My Effective Access")).toBeInTheDocument();
    expect(screen.getAllByText("Manager").length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText("StaffUser")).toBeInTheDocument();
    expect(screen.getByText("Clients Directory & Profiles")).toBeInTheDocument();
  });
});
