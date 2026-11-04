import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import AuditExplorerPage from "./AuditExplorerPage";

describe("AuditExplorerPage", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("renders the Audit Explorer UI with filters and records", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async (url: string) => {
        if (url.includes("/api/v1/audit/filter-options")) {
          return new Response(
            JSON.stringify({
              entityTypes: ["Client", "Branch", "Staff"],
              users: [{ userId: "u1", userName: "admin@cms.local" }],
              actions: [
                { id: 1, name: "Create" },
                { id: 2, name: "Update" },
              ],
            }),
            { status: 200, headers: { "Content-Type": "application/json" } }
          );
        }
        if (url.includes("/api/v1/clients")) {
          return new Response(
            JSON.stringify({
              total: 1,
              skip: 0,
              take: 100,
              items: [{ id: "c1", code: "10000001", legalName: "Acme Corp", isActive: true }],
            }),
            { status: 200, headers: { "Content-Type": "application/json" } }
          );
        }
        if (url.includes("/api/v1/audit")) {
          return new Response(
            JSON.stringify({
              total: 1,
              skip: 0,
              take: 20,
              items: [
                {
                  id: "a1",
                  occurredAtUtc: "2026-10-08T12:00:00Z",
                  entityType: "Client",
                  entityId: "c1",
                  action: 2, // Update
                  changedFields: JSON.stringify(["LegalName", "TradeName"]),
                  oldValues: JSON.stringify({ LegalName: "Old Corp", TradeName: "Old Trade" }),
                  newValues: JSON.stringify({ LegalName: "New Corp", TradeName: "New Trade" }),
                  userId: "u1",
                  userName: "admin@cms.local",
                  diff: [
                    { field: "LegalName", oldValue: "Old Corp", newValue: "New Corp", changed: true },
                    { field: "TradeName", oldValue: "Old Trade", newValue: "New Trade", changed: true },
                  ],
                },
              ],
            }),
            { status: 200, headers: { "Content-Type": "application/json" } }
          );
        }
        return new Response("Not found", { status: 404 });
      })
    );

    render(<AuditExplorerPage />);

    expect(screen.getByText("Audit Explorer")).toBeInTheDocument();
    expect(screen.getByText("Non-Editable Audit Log")).toBeInTheDocument();
    expect(screen.getByText("Filter Activity Records")).toBeInTheDocument();

    // Verify row rendered in table and options
    await waitFor(() => expect(screen.getAllByText("admin@cms.local").length).toBeGreaterThanOrEqual(1));
    expect(screen.getAllByText("Client").length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText("Update").length).toBeGreaterThanOrEqual(1);

    // Click "Compare Diff" to open the side-by-side diff modal
    const compareBtn = screen.getByRole("button", { name: /compare diff/i });
    fireEvent.click(compareBtn);

    // Verify side-by-side modal opens
    await waitFor(() =>
      expect(screen.getByText("Client Diff Inspection")).toBeInTheDocument()
    );
    expect(screen.getByText("Old Value (Previous)")).toBeInTheDocument();
    expect(screen.getByText("New Value (Current)")).toBeInTheDocument();
    expect(screen.getByText("Old Corp")).toBeInTheDocument();
    expect(screen.getByText("New Corp")).toBeInTheDocument();
  });
});
