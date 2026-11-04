import type { Breakdown } from "../api/dashboard";
import HorizontalBarChart from "./charts/HorizontalBarChart";

export interface BarChartProps {
  title: string;
  subtitle?: string;
  rows: Breakdown[];
  emptyText?: string;
  emptyDescription?: string;
  maxRows?: number;
  showPercentages?: boolean;
  totalLabel?: string;
  className?: string;
}

export default function BarChart({
  title,
  subtitle,
  rows,
  emptyText = "No data available.",
  emptyDescription,
  maxRows = 6,
  showPercentages = true,
  totalLabel = "Total",
  className = "",
}: BarChartProps) {
  return (
    <HorizontalBarChart
      title={title}
      subtitle={subtitle}
      rows={rows}
      emptyTitle={emptyText}
      emptyDescription={emptyDescription}
      maxInitialRows={maxRows}
      showPercentages={showPercentages}
      totalLabel={totalLabel}
      className={className}
    />
  );
}
