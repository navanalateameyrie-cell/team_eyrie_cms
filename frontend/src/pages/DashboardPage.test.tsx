import { render, screen, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { MemoryRouter } from "react-router-dom";
import DashboardPage from "./DashboardPage";
import { dashboardApi } from "../api/dashboard";

vi.mock("../api/dashboard", () => ({
  dashboardApi: {
    kpis: vi.fn(),
    clientsByEntityType: vi.fn(),
    clientsByState: vi.fn(),
    serviceFilesByService: vi.fn(),
    staffWorkloadTop: vi.fn(),
  },
}));

describe("DashboardPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders loading spinner while fetching", () => {
    // Return unresolved promises
    vi.mocked(dashboardApi.kpis).mockReturnValue(new Promise(() => {}));
    vi.mocked(dashboardApi.clientsByEntityType).mockReturnValue(new Promise(() => {}));
    vi.mocked(dashboardApi.clientsByState).mockReturnValue(new Promise(() => {}));
    vi.mocked(dashboardApi.serviceFilesByService).mockReturnValue(new Promise(() => {}));
    vi.mocked(dashboardApi.staffWorkloadTop).mockReturnValue(new Promise(() => {}));

    render(
      <MemoryRouter>
        <DashboardPage />
      </MemoryRouter>
    );

    expect(screen.getByText(/Loading practice dashboard metrics/i)).toBeInTheDocument();
  });

  it("renders error state on API failure", async () => {
    vi.mocked(dashboardApi.kpis).mockRejectedValue(new Error("Network connection lost"));
    vi.mocked(dashboardApi.clientsByEntityType).mockResolvedValue([]);
    vi.mocked(dashboardApi.clientsByState).mockResolvedValue([]);
    vi.mocked(dashboardApi.serviceFilesByService).mockResolvedValue([]);
    vi.mocked(dashboardApi.staffWorkloadTop).mockResolvedValue([]);

    render(
      <MemoryRouter>
        <DashboardPage />
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText("Failed to load dashboard")).toBeInTheDocument();
      expect(screen.getByText("Network connection lost")).toBeInTheDocument();
    });
  });

  it("renders KPI cards and charts with actual practice metrics", async () => {
    vi.mocked(dashboardApi.kpis).mockResolvedValue({
      activeClients: 1603,
      inactiveClients: 0,
      activeServiceFiles: 1382,
      onHoldServiceFiles: 0,
      closedServiceFiles: 0,
      openEngagements: 0,
      inProgressEngagements: 0,
      completedEngagements: 0,
      activeBranches: 0,
      activeStaff: 5,
    });

    vi.mocked(dashboardApi.clientsByEntityType).mockResolvedValue([
      { label: "Individual / Proprietorship", count: 1167 },
      { label: "Private Limited Company", count: 284 },
      { label: "Partnership Firm", count: 124 },
    ]);

    vi.mocked(dashboardApi.clientsByState).mockResolvedValue([]);

    vi.mocked(dashboardApi.serviceFilesByService).mockResolvedValue([
      { label: "Income Tax Filing & Audit", count: 847 },
      { label: "Annual GST Audit & Return", count: 133 },
    ]);

    vi.mocked(dashboardApi.staffWorkloadTop).mockResolvedValue([]);

    render(
      <MemoryRouter>
        <DashboardPage />
      </MemoryRouter>
    );

    await waitFor(() => {
      // 5 KPI cards
      expect(screen.getByText("Active Clients")).toBeInTheDocument();
      expect(screen.getByText("1,603")).toBeInTheDocument();
      expect(screen.getByText("Open Service Files")).toBeInTheDocument();
      expect(screen.getByText("1,382")).toBeInTheDocument();
      expect(screen.getByText("Active Staff")).toBeInTheDocument();
      expect(screen.getByText("5")).toBeInTheDocument();

      // 4 Charts
      expect(screen.getByText("Clients by Entity Type")).toBeInTheDocument();
      expect(screen.getByText("Individual / Proprietorship")).toBeInTheDocument();

      expect(screen.getByText("Clients by State (Place of Supply)")).toBeInTheDocument();
      expect(screen.getByText("No State of Supply Data")).toBeInTheDocument();

      expect(screen.getByText("Open Service Files by Service")).toBeInTheDocument();
      expect(screen.getByText("Income Tax Filing & Audit")).toBeInTheDocument();

      expect(screen.getByText("Top Staff by Active Assignments")).toBeInTheDocument();
      expect(screen.getByText("No Active Staff Assignments")).toBeInTheDocument();
    });
  });
});

