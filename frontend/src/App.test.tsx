import { render, screen, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import App from "./App";

describe("App shell", () => {
  beforeEach(() => {
    localStorage.clear();
    vi.restoreAllMocks();
  });

  it("renders the Home nav and health card when unauthenticated", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () =>
        new Response(
          JSON.stringify({
            status: "ok",
            service: "CMS.Api",
            version: "1.0",
            utc: "2026-01-01T00:00:00Z",
          }),
          { status: 200, headers: { "Content-Type": "application/json" } }
        )
      )
    );

    render(<App />);
    expect(screen.getByText("CMS")).toBeInTheDocument();
    expect(screen.getByText("API health")).toBeInTheDocument();
    await waitFor(() => expect(screen.getByText("CMS.Api")).toBeInTheDocument());
    expect(screen.getByText(/Not signed in/i)).toBeInTheDocument();
  });

  it("surfaces health errors without blowing up the shell", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => new Response("boom", { status: 500 }))
    );
    render(<App />);
    // The shell still renders even when the probe fails.
    expect(screen.getByText("CMS")).toBeInTheDocument();
    await waitFor(() => expect(screen.getByText(/Checking…/i)).toBeInTheDocument());
  });
});
