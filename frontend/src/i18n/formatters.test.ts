import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  formatDate,
  formatList,
  formatNumber,
  formatRelativeTime,
} from "./formatters";

describe("formatDate", () => {
  it("formats a Date object", () => {
    const date = new Date("2024-01-15T12:00:00Z");
    const result = formatDate(date, "en-US");
    expect(result).toBeTypeOf("string");
    expect(result.length).toBeGreaterThan(0);
  });

  it("accepts an ISO string", () => {
    const result = formatDate("2024-01-15T12:00:00Z", "en-US");
    expect(result).toBeTypeOf("string");
    expect(result.length).toBeGreaterThan(0);
  });

  it("accepts a numeric timestamp", () => {
    const ts = new Date("2024-01-15T12:00:00Z").getTime();
    const result = formatDate(ts, "en-US");
    expect(result).toBeTypeOf("string");
    expect(result.length).toBeGreaterThan(0);
  });

  it("respects Intl.DateTimeFormatOptions", () => {
    const date = new Date("2024-01-15T00:00:00Z");
    const result = formatDate(date, "en-US", {
      year: "numeric",
      month: "long",
      day: "numeric",
      timeZone: "UTC",
    });
    expect(result).toContain("January");
    expect(result).toContain("2024");
  });

  it("produces different output for different locales", () => {
    const date = new Date("2024-01-15T00:00:00Z");
    const en = formatDate(date, "en-US", { timeZone: "UTC" });
    const pt = formatDate(date, "pt-BR", { timeZone: "UTC" });
    expect(en).not.toBe(pt);
  });
});

describe("formatNumber", () => {
  it("formats a number", () => {
    const result = formatNumber(1234.56, "en-US");
    expect(result).toBe("1,234.56");
  });

  it("formats with pt-BR locale (comma decimal separator)", () => {
    const result = formatNumber(1234.56, "pt-BR");
    // pt-BR uses comma as decimal and period as thousands separator
    expect(result).toContain(",");
  });

  it("respects Intl.NumberFormatOptions (currency)", () => {
    const result = formatNumber(9.99, "en-US", {
      style: "currency",
      currency: "USD",
    });
    expect(result).toContain("9.99");
    expect(result).toContain("$");
  });

  it("respects maximumFractionDigits option", () => {
    const result = formatNumber(1.999, "en-US", { maximumFractionDigits: 0 });
    expect(result).toBe("2");
  });
});

describe("formatRelativeTime", () => {
  // formatRelativeTime reads the clock itself, so a reference instant taken here
  // would drift from the one the assertion sees: in a full-suite run the gap
  // between collection and execution can outgrow the 30-second case and format
  // it as "1 minute ago". Freeze the clock instead, so every case below compares
  // two fixed instants no matter how long the suite takes to reach it.
  const now = new Date("2024-01-15T12:00:00Z").getTime();

  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(now);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("returns seconds for sub-minute differences", () => {
    const recent = new Date(now - 30 * 1000); // 30 seconds ago
    const result = formatRelativeTime(recent, "en-US");
    expect(result).toMatch(/second/);
  });

  it("returns minutes for sub-hour differences", () => {
    const fiveMinutesAgo = new Date(now - 5 * 60 * 1000);
    const result = formatRelativeTime(fiveMinutesAgo, "en-US");
    expect(result).toMatch(/minute/);
  });

  it("returns hours for sub-day differences", () => {
    const threeHoursAgo = new Date(now - 3 * 60 * 60 * 1000);
    const result = formatRelativeTime(threeHoursAgo, "en-US");
    expect(result).toMatch(/hour/);
  });

  it("returns days for multi-day differences", () => {
    const threeDaysAgo = new Date(now - 3 * 24 * 60 * 60 * 1000);
    const result = formatRelativeTime(threeDaysAgo, "en-US");
    expect(result).toMatch(/day/);
  });

  it("handles future dates (positive diff)", () => {
    const inTwoDays = new Date(now + 2 * 24 * 60 * 60 * 1000);
    const result = formatRelativeTime(inTwoDays, "en-US");
    expect(result).toMatch(/day/);
    expect(result).not.toMatch(/ago/);
  });

  it("accepts ISO string input", () => {
    const fiveMinutesAgo = new Date(now - 5 * 60 * 1000).toISOString();
    const result = formatRelativeTime(fiveMinutesAgo, "en-US");
    expect(result).toMatch(/minute/);
  });

  it("accepts numeric timestamp input", () => {
    const tenSecondsAgo = now - 10 * 1000;
    const result = formatRelativeTime(tenSecondsAgo, "en-US");
    expect(result).toBeTypeOf("string");
  });
});

describe("formatList", () => {
  it("joins with the en-US conjunction", () => {
    expect(formatList(["Steam ID", "Region"], "en-US")).toBe(
      "Steam ID and Region",
    );
  });

  it("uses the locale's own conjunction rather than a hardcoded separator", () => {
    expect(formatList(["Steam ID", "Region"], "pt-BR")).toBe(
      "Steam ID e Region",
    );
  });

  it("joins three or more values", () => {
    expect(formatList(["a", "b", "c"], "en-US")).toBe("a, b, and c");
  });

  it("returns a single value unchanged", () => {
    expect(formatList(["Steam ID"], "en-US")).toBe("Steam ID");
  });

  it("returns an empty string for no values", () => {
    expect(formatList([], "en-US")).toBe("");
  });
});
