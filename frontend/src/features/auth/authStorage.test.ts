import { describe, it, expect, beforeEach } from "vitest";
import {
  getAccessToken,
  setAccessToken,
  clearAccessToken,
} from "./authStorage";

describe("authStorage", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it("getAccessToken returns null when nothing stored", () => {
    expect(getAccessToken()).toBeNull();
  });

  it("setAccessToken and getAccessToken roundtrip", () => {
    setAccessToken("my-token");
    expect(getAccessToken()).toBe("my-token");
  });

  it("setAccessToken overwrites previous value", () => {
    setAccessToken("first");
    setAccessToken("second");
    expect(getAccessToken()).toBe("second");
  });

  it("clearAccessToken removes stored token", () => {
    setAccessToken("my-token");
    clearAccessToken();
    expect(getAccessToken()).toBeNull();
  });

  it("clearAccessToken is safe when no token stored", () => {
    expect(() => clearAccessToken()).not.toThrow();
    expect(getAccessToken()).toBeNull();
  });
});
