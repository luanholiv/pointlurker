import "@testing-library/jest-dom";
import { afterAll, afterEach, beforeAll } from "vitest";
import { server } from "./server";
// Initialize i18next so that useTranslation() works in component tests.
// Components that use t() will receive en-US strings (the default locale).
import "../i18n";

// Node.js 22+ defines globalThis.localStorage as a getter that returns undefined
// unless --localstorage-file is provided, shadowing jsdom's Storage implementation.
// Polyfill with an in-memory store so tests can use localStorage normally.
if (!globalThis.localStorage) {
  const _store: Record<string, string> = {};
  Object.defineProperty(globalThis, "localStorage", {
    configurable: true,
    writable: true,
    value: {
      get length() {
        return Object.keys(_store).length;
      },
      clear() {
        for (const k of Object.keys(_store)) delete _store[k];
      },
      getItem(key: string) {
        return Object.prototype.hasOwnProperty.call(_store, key)
          ? _store[key]
          : null;
      },
      setItem(key: string, value: string) {
        _store[key] = String(value);
      },
      removeItem(key: string) {
        delete _store[key];
      },
      key(index: number) {
        return Object.keys(_store)[index] ?? null;
      },
    } satisfies Storage,
  });
}

Object.defineProperty(window, "matchMedia", {
  writable: true,
  value: (query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: () => {},
    removeListener: () => {},
    addEventListener: () => {},
    removeEventListener: () => {},
    dispatchEvent: () => false,
  }),
});

beforeAll(() => server.listen({ onUnhandledRequest: "error" }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());
