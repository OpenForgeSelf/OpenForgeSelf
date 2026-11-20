import { defineConfig } from 'vitest/config'

/** 仅测纯 TS（probeAllRuntimes）；不启 Vue 插件。 */
export default defineConfig({
  test: {
    environment: 'node',
    include: ['src/**/*.spec.ts'],
  },
})
