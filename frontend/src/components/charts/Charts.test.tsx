import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import HorizontalBarChart from "./HorizontalBarChart";
import DonutChart from "./DonutChart";
import RankedBarChart from "./RankedBarChart";

describe("HorizontalBarChart", () => {
  const sampleData = [
    { label: "Private Limited", count: 284 },
    { label: "Individual / Proprietorship", count: 1167 },
    { label: "Partnership Firm", count: 124 },
    { label: "Trust / NGO", count: 18 },
    { label: "Limited Liability Partnership", count: 10 },
  ];

  it("renders with sorted data and percentages", () => {
    render(
      <HorizontalBarChart
        title="Clients by Entity Type"
        subtitle="Breakdown"
        rows={sampleData}
        totalLabel="Clients"
      />
    );

    expect(screen.getByText("Clients by Entity Type")).toBeInTheDocument();
    expect(screen.getByText("1,603 clients")).toBeInTheDocument();
    expect(screen.getByText("Individual / Proprietorship")).toBeInTheDocument();
    expect(screen.getByText("1,167")).toBeInTheDocument();
    expect(screen.getByText("72.8%")).toBeInTheDocument();
  });

  it("renders an informative empty state when data is empty", () => {
    render(
      <HorizontalBarChart
        title="Empty Chart"
        rows={[]}
        emptyTitle="No Data Found"
        emptyDescription="Please add items to populate."
      />
    );

    expect(screen.getByText("No Data Found")).toBeInTheDocument();
    expect(screen.getByText("Please add items to populate.")).toBeInTheDocument();
  });

  it("supports expandable view when items exceed maxInitialRows", () => {
    const manyRows = Array.from({ length: 10 }, (_, i) => ({
      label: `Service ${i + 1}`,
      count: (10 - i) * 10,
    }));

    render(
      <HorizontalBarChart
        title="Services"
        rows={manyRows}
        maxInitialRows={4}
      />
    );

    expect(screen.getByText("View all (10)")).toBeInTheDocument();
    expect(screen.queryByText("Service 8")).not.toBeInTheDocument();

    fireEvent.click(screen.getByText("View all (10)"));
    expect(screen.getByText("Service 8")).toBeInTheDocument();
    expect(screen.getByText("Show less")).toBeInTheDocument();
  });
});

describe("DonutChart", () => {
  const sampleStates = [
    { label: "Tamil Nadu", count: 120 },
    { label: "Maharashtra", count: 45 },
    { label: "Karnataka", count: 35 },
  ];

  it("renders donut chart with center total and legend", () => {
    render(
      <DonutChart
        title="Clients by State"
        subtitle="Geographic distribution"
        rows={sampleStates}
        centerLabel="Clients"
      />
    );

    expect(screen.getByText("Clients by State")).toBeInTheDocument();
    expect(screen.getByText("200")).toBeInTheDocument(); // 120 + 45 + 35
    expect(screen.getByText("Tamil Nadu")).toBeInTheDocument();
    expect(screen.getByText("60.0%")).toBeInTheDocument(); // 120 / 200
  });

  it("renders informative empty state when no branch data is available", () => {
    render(
      <DonutChart
        title="Clients by State"
        rows={[]}
        emptyTitle="No State of Supply Data"
        emptyDescription="No primary branch locations registered yet."
      />
    );

    expect(screen.getByText("No State of Supply Data")).toBeInTheDocument();
    expect(screen.getByText("No primary branch locations registered yet.")).toBeInTheDocument();
  });
});

describe("RankedBarChart", () => {
  const sampleStaff = [
    { label: "CA Rajesh Sharma", count: 24 },
    { label: "Priya Patel", count: 18 },
    { label: "Neha Gupta", count: 9 },
  ];

  it("renders ranked list with #1, #2, #3 badges and disclaimer", () => {
    render(
      <RankedBarChart
        title="Top Staff"
        rows={sampleStaff}
      />
    );

    expect(screen.getByText("CA Rajesh Sharma")).toBeInTheDocument();
    expect(screen.getByText("#1")).toBeInTheDocument();
    expect(screen.getByText("#2")).toBeInTheDocument();
    expect(screen.getByText("#3")).toBeInTheDocument();
    expect(screen.getByText(/Active assignment counts reflect case delegation capacity/i)).toBeInTheDocument();
  });

  it("renders restricted view when staff access is forbidden", () => {
    render(
      <RankedBarChart
        title="Top Staff"
        rows={[]}
        isRestricted={true}
      />
    );

    expect(screen.getByText("Staff Workload Restricted")).toBeInTheDocument();
    expect(screen.getByText(/staff\.read/i)).toBeInTheDocument();
  });

  it("renders informative empty state when no active assignments exist", () => {
    render(
      <RankedBarChart
        title="Top Staff"
        rows={[]}
      />
    );

    expect(screen.getByText("No Active Staff Assignments")).toBeInTheDocument();
  });
});

