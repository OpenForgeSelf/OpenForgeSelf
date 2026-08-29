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
  background: #f8f9fa;
  border: 1px solid #e9ecef;
}

.runtime-badge.available {
  background: #f0fff4;
  border-color: #c6f6d5;
  color: #22543d;
}

.runtime-badge.unavailable {
  background: #fff5f5;
  border-color: #fed7d7;
  color: #742a2a;
}

.status-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #cbd5e0;
}

.available .status-dot {
  background: #38a169;
  box-shadow: 0 0 0 2px rgba(56, 161, 105, 0.2);
}

.unavailable .status-dot {
  background: #e53e3e;
}

.runtime-icon {
  font-size: 14px;
}

.runtime-name {
  white-space: nowrap;
}
</style>
