<script setup lang="ts">
/**
 * 代码块（Code Block）· YAML 高亮（示例 stardust.yaml）。
 * 轻量逐行 tokenizer，仅用于演示设计系统的代码排版 token（JetBrains Mono + 语法色）。
 */
import { computed } from 'vue'

const props = withDefaults(
  defineProps<{
    code: string
    filename?: string
  }>(),
  { filename: 'stardust.yaml' },
)

function esc(s: string): string {
  return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
}

/** 将一行 YAML 转成带高亮 span 的 HTML。 */
function highlightLine(line: string): string {
  const indent = line.match(/^\s*/)?.[0] ?? ''
  const rest = line.slice(indent.length)
  if (rest.startsWith('#')) {
    return `${indent}<span class="tk-comment">${esc(rest)}</span>`
  }
  // 列表项：- key: value
  const listMatch = rest.match(/^(-\s+)(.*)$/)
  if (listMatch) {
    return `${indent}<span class="tk-punct">-</span> ${highlightPair(listMatch[2])}`
  }
  return indent + highlightPair(rest)
}

/** 处理「key: value」或纯 value。 */
function highlightPair(rest: string): string {
  const m = rest.match(/^([\w.-]+)(:\s*)(.*)$/)
  if (m) {
    return `<span class="tk-key">${esc(m[1])}</span><span class="tk-punct">${esc(m[2])}</span>${highlightValue(m[3])}`
  }
  return highlightValue(rest)
}

function highlightValue(v: string): string {
  if (v === '') return ''
  if (/^(".*"|'.*')$/.test(v)) return `<span class="tk-str">${esc(v)}</span>`
  if (/^(true|false|null|~)$/.test(v.trim())) return `<span class="tk-bool">${esc(v)}</span>`
  if (/^-?\d+(\.\d+)?$/.test(v.trim())) return `<span class="tk-num">${esc(v)}</span>`
  return `<span class="tk-str">${esc(v)}</span>`
}

const html = computed(() =>
  props.code
    .split('\n')
    .map((l) => highlightLine(l))
    .join('\n'),
)
</script>

<template>
  <div class="ds-code">
    <div class="ds-code__bar">
      <span class="ds-code__filename ds-mono">{{ filename }}</span>
      <span class="ds-code__lang ds-micro">YAML</span>
    </div>
    <pre class="ds-code__pre"><code class="ds-mono" v-html="html"></code></pre>
  </div>
</template>

<style scoped>
.ds-code {
  border: 1px solid var(--ds-border-2);
  border-radius: var(--ds-radius-lg);
  overflow: hidden;
  background: var(--ds-surface-2);
  box-shadow: var(--ds-shadow-sm);
}
.ds-code__bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: var(--ds-space-2) var(--ds-space-4);
  border-bottom: 1px solid var(--ds-border-1);
  background: var(--ds-surface-3);
}
.ds-code__filename {
  color: var(--ds-fg-2);
}
.ds-code__lang {
  color: var(--ds-fg-4);
}
.ds-code__pre {
  margin: 0;
  padding: var(--ds-space-4);
  overflow-x: auto;
  font-size: var(--ds-fs-small);
  line-height: 1.7;
  color: var(--ds-fg-1);
}
.tk-comment { color: var(--ds-fg-4); font-style: italic; }
.tk-key { color: var(--ds-brand-600); font-weight: var(--ds-fw-medium); }
.tk-str { color: var(--ds-accent-700); }
.tk-num { color: var(--ds-warning); }
.tk-bool { color: var(--ds-info); }
.tk-punct { color: var(--ds-fg-3); }
</style>
