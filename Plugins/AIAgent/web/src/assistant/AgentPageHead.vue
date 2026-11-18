<script setup lang="ts">
import { MODE_LABEL } from './session.logic'
import type { AgentAssistantLayout, AgentMode } from './types'

const props = defineProps<{
  layout: AgentAssistantLayout
  title: string
  mode: AgentMode
  kind: 'new' | 'confirm-plan' | 'approve'
}>()
const emit = defineEmits<{
  new: []
  confirm: []
  sessions: []
  workspace: []
}>()
</script>

<template>
  <header class="aa-pagehead">
    <div>
      <div class="aa-eyebrow">智能体 · {{ MODE_LABEL[mode] }}</div>
      <h1>{{ title }}</h1>
    </div>
    <div class="aa-head-actions">
      <button v-if="layout === 'panel'" type="button" class="aa-layer-btn" @click="emit('sessions')">会话</button>
      <button v-if="layout === 'panel'" type="button" class="aa-layer-btn" @click="emit('workspace')">工作区</button>
      <button
        v-if="kind !== 'new'"
        type="button"
        class="aa-btn ghost"
        @click="emit('new')"
      >新建会话</button>
      <button
        v-if="kind === 'new'"
        type="button"
        class="aa-btn pri"
        @click="emit('new')"
      >新建会话</button>
      <button
        v-else-if="kind === 'confirm-plan'"
        type="button"
        class="aa-btn pri"
        @click="emit('confirm')"
      >确认并执行</button>
    </div>
  </header>
</template>
