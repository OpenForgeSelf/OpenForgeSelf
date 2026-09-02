<script setup lang="ts">
/**
 * Lucide 图标系统（1.5px stroke / line icons）。
 * 设计系统「Icon System」：统一 1.5px 描边线性图标，走 currentColor，可被 token 着色。
 * 仅内置设计系统演示所需子集。
 */
const props = withDefaults(
  defineProps<{
    name: string
    size?: number | string
  }>(),
  { size: 20 },
)

// 每个图标 = 一组 path 的 d（Lucide 24x24 viewBox）
const PATHS: Record<string, string[]> = {
  palette: [
    'M12 2a10 10 0 1 0 0 20 2.5 2.5 0 0 0 2.5-2.5c0-.7-.3-1.3-.7-1.8-.4-.5-.7-1-.7-1.7 0-1.4 1.1-2.5 2.5-2.5H17a5 5 0 0 0 5-5c0-4.4-4.5-8-10-8Z',
    'M7.5 10.5h.01M12 10.5h.01M16.5 10.5h.01',
  ],
  dashboard: [
    'M3 13h8V3H3v10Zm0 8h8v-6H3v6Zm10 0h8V11h-8v10Zm0-18v6h8V3h-8Z',
  ],
  server: [
    'M4 4h16v6H4zM4 14h16v6H4z',
    'M8 7h.01M8 17h.01',
  ],
  network: [
    'M9 6a3 3 0 1 0 0-.01M9 6a3 3 0 1 0 0 .01Z',
    'M15 18a3 3 0 1 0 0-.01M15 18a3 3 0 1 0 0 .01Z',
    'M12 9a3 3 0 1 0 0-.01M12 9a3 3 0 1 0 0 .01Z',
    'M10.5 7.5 13.5 16.5M10.6 10.4 7.5 7.5M13.5 7.5 16.5 16.5',
  ],
  settings: [
    'M4 7h10M18 7h2M4 17h2M10 17h10',
    'M14 4v4M14 16v4M4 10v4M20 10v4',
  ],
  rocket: [
    'M5 13c-1.5 1.5-2 5-2 5s3.5-.5 5-2c.8-.8.8-2 0-2.8a2 2 0 0 0-3 0ZM9 15l-3-3c1-5 4.5-9 11-9 0 6.5-4 10-9 11Z',
    'M15 9a1.5 1.5 0 1 0 0-.01',
  ],
  check: ['M20 6 9 17l-5-5'],
  'alert-triangle': ['M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0Z', 'M12 9v4M12 17h.01'],
  'alert-octagon': [
    'M7.9 3.5 3.5 7.9 2 12l1.5 4.1L7.9 20.5 12 22l4.1-1.5L20.5 16.1 22 12l-1.5-4.1L16.1 3.5 12 2 7.9 3.5Z',
    'M12 8v4M12 16h.01',
  ],
  loader: ['M12 2v4M12 18v4M4.9 4.9l2.8 2.8M16.3 16.3l2.8 2.8M2 12h4M18 12h4M4.9 19.1l2.8-2.8M16.3 7.7l2.8-2.8'],
  box: ['M21 8 12 3 3 8v8l9 5 9-5V8Z', 'M3 8l9 5 9-5M12 13v8'],
  cpu: [
    'M9 9h6v6H9z',
    'M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5',
  ],
  activity: ['M22 12h-4l-3 9L9 3l-3 9H2'],
  gauge: [
    'M12 14a2 2 0 1 0 0-.01M12 14a2 2 0 1 0 0 .01ZM3.5 18a9 9 0 1 1 17 0',
    'M12 14 16 9',
  ],
  link: ['M10 13a5 5 0 0 0 7 0l3-3a5 5 0 0 0-7-7l-1.5 1.5', 'M14 11a5 5 0 0 0-7 0l-3 3a5 5 0 0 0 7 7l1.5-1.5'],
}

const d = PATHS[props.name] ?? PATHS.box
</script>

<template>
  <svg
    class="ds-icon"
    :width="size"
    :height="size"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    stroke-width="1.5"
    stroke-linecap="round"
    stroke-linejoin="round"
    aria-hidden="true"
  >
    <path v-for="(p, i) in d" :key="i" :d="p" />
  </svg>
</template>

<style scoped>
.ds-icon {
  display: inline-block;
  flex: none;
}
</style>
