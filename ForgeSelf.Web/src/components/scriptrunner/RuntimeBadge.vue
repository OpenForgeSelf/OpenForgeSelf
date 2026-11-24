<script setup lang="ts">
import { computed } from 'vue'
import type { RuntimeEnvironment, ScriptLanguage } from '@/types/scriptRunner'

const props = defineProps<{
  runtime?: RuntimeEnvironment
  language?: ScriptLanguage
  available?: boolean
  version?: string
}>()

const languageNames: Record<ScriptLanguage, string> = {
  powershell: 'PowerShell',
  python: 'Python',
  nodejs: 'Node.js',
  shell: 'Shell',
  cmd: 'CMD'
}

const languageIcons: Record<ScriptLanguage, string> = {
  powershell: '💠',
  python: '🐍',
  nodejs: '📦',
  shell: '🐚',
  cmd: '⚫'
}

const displayLanguage = computed(() => {
  if (props.runtime) {
    return props.runtime.name || languageNames[props.runtime.language]
  }
  if (props.language) {
    return languageNames[props.language]
  }
  return 'Unknown'
})

const displayIcon = computed(() => {
  const lang = props.runtime?.language || props.language
  return lang ? languageIcons[lang] : '📜'
})

const isAvailable = computed(() => {
  return props.runtime?.available ?? props.available ?? false
})

const displayVersion = computed(() => {
  return props.runtime?.version || props.version || ''
})
</script>

<template>
  <div
    class="runtime-badge"
    :class="{ available: isAvailable, unavailable: !isAvailable }"
    :title="displayVersion ? `${displayLanguage} ${displayVersion}` : displayLanguage"
  >
    <span class="status-dot" />
    <span class="runtime-icon">{{ displayIcon }}</span>
    <span class="runtime-name">{{ displayLanguage }}</span>
  </div>
</template>

<style scoped>
.runtime-badge {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px 10px;
  border-radius: 12px;
  font-size: 12px;
  font-weight: 500;
  background: var(--el-fill-color-light);
  border: 1px solid var(--el-border-color-lighter);
}

.runtime-badge.available {
  background: var(--el-color-success-light-9);
  border-color: var(--el-color-success-light-8);
  color: var(--el-color-success-dark-2);
}

.runtime-badge.unavailable {
  background: var(--el-color-error-light-9);
  border-color: var(--el-color-error-light-8);
  color: var(--el-color-error-dark-2);
}

.status-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--el-fill-color);
}

.available .status-dot {
  background: var(--el-color-success);
  box-shadow: 0 0 0 2px color-mix(in srgb, var(--el-color-success) 20%, transparent);
}

.unavailable .status-dot {
  background: var(--el-color-error);
}

.runtime-icon {
  font-size: 14px;
}

.runtime-name {
  white-space: nowrap;
}
</style>
