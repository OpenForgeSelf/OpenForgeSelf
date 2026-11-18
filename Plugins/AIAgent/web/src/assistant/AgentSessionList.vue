<script setup lang="ts">
import { MODE_LABEL, statusLabel } from './session.logic'
import type { AgentSessionStatus, AgentSessionSummary } from './types'

defineProps<{
  sessions: AgentSessionSummary[]
  activeId: string | null
  showDemo?: boolean
  showBack?: boolean
}>()
const emit = defineEmits<{
  select: [id: string]
  demo: []
  chat: []
}>()

function dotClass(status: AgentSessionStatus): string {
  if (status === 'running') return 'run'
  if (status === 'awaiting_plan' || status === 'awaiting_approval') return 'wait'
  if (status === 'stuck' || status === 'failed') return status === 'failed' ? 'fail' : 'stuck'
  if (status === 'completed') return 'done'
  return ''
}
</script>

<template>
  <aside class="aa-sidebar">
    <div class="aa-side-head">
      <div class="aa-side-label">会话</div>
      <button v-if="showBack" type="button" class="aa-btn ghost" @click="emit('chat')">返回对话</button>
    </div>
    <div class="aa-session-list">
      <button
        v-for="s in sessions"
        :key="s.id"
        type="button"
        class="aa-session"
        :class="{ 'is-on': s.id === activeId }"
        @click="emit('select', s.id)"
      >
        <div class="aa-name">{{ s.title || '未命名会话' }}</div>
        <div class="aa-meta">
          <span class="aa-dot" :class="dotClass(s.status)"></span>
          <span>{{ MODE_LABEL[s.mode] }}</span>
          <span>{{ statusLabel(s.status) }}</span>
        </div>
      </button>
    </div>
    <button v-if="showDemo" type="button" class="aa-demo" @click="emit('demo')">预览助手面板</button>
  </aside>
</template>
