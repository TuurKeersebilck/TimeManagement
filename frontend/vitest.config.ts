import { fileURLToPath, URL } from "node:url";
import { readFileSync } from "node:fs";

import { defineConfig } from "vitest/config";
import vue from "@vitejs/plugin-vue";

const { version } = JSON.parse(readFileSync("./package.json", "utf-8"));

// Deliberately standalone rather than merged with vite.config.js: the dev-tools and Tailwind
// plugins do nothing for tests but cost startup time on every run.
export default defineConfig({
  define: {
    __APP_VERSION__: JSON.stringify(version),
  },
  plugins: [vue()],
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
    },
  },
  test: {
    environment: "happy-dom",
    globals: true,
    setupFiles: ["./src/test/setup.ts"],
    include: ["src/**/*.spec.ts"],
    restoreMocks: true,
  },
});
