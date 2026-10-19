/// <reference types="vitest/config" />

import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: ["./src/test/setup.ts"],
    // tests/ holds Playwright specs (projects "ui" and "real"); vitest unit/component
    // tests live under src/. Exclude the whole tree so renames inside tests/ can't leak
    // Playwright specs into the vitest run.
    exclude: ["tests/**", "node_modules/**"],
    env: {
      VITE_API_URL: "http://localhost",
    },
    coverage: {
      provider: "v8",
      exclude: [
        "**/*.d.ts",
        "src/main.tsx",
        "src/vite-env.d.ts",
        "src/**/index.ts",
        "src/test/**",
        "src/i18n/locales/**",
      ],
      thresholds: {
        lines: 60,
        branches: 58,
        functions: 60,
        statements: 60,
      },
    },
  },
});
