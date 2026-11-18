<script setup lang="ts">
import { ATTACH_KINDS } from './session.logic'
import type { AgentAttachment, AgentCatalogAgent, AgentCatalogModel, AgentMode } from './types'

const props = defineProps<{
  mode: AgentMode
  agentId: string
  modelId: string
  agents: AgentCatalogAgent[]
  models: AgentCatalogModel[]
  attachments: AgentAttachment[]
  attachOpen: boolean
  running: boolean
  disabled?: boolean
}>()
const emit = defineEmits<{
  'update:mode': [AgentMode]
  'update:agentId': [string]
  'update:modelId': [string]
  'update:attachOpen': [boolean]
  send: []
  stop: []
  attach: [kind: AgentAttachment['kind'], name: string]
  removePin: [id: string]
}>()
const draft = defineModel<string>('draft', { default: '' })

const canSend = () => draft.value.trim().length > 0 && !props.running

function onKey(e: KeyboardEvent) {
  if (e.key === 'Enter' && !e.shiftKey) {
    e.preventDefault()
    if (canSend()) emit('send')
  }
}
</script>

<template>
  <div class="aa-dock">
    <div class="aa-dock-inner">
      <div class="aa-composer">
        <div v-if="attachments.length" class="aa-pins">
          <span v-for="pin in attachments" :key="pin.id" class="aa-pin">
            {{ pin.name }}
            <button type="button" aria-label="移除" @click="emit('removePin', pin.id)">×</button>
          </span>
        </div>
        <textarea
          v-model="draft"
          rows="2"
          placeholder="说明你要做的事。Enter 发送，Shift+Enter 换行。"
          :disabled="running"
          @keydown="onKey"
        />
        <div class="aa-composer-bar">
          <div class="aa-attach">
            <button
              type="button"
              class="aa-attach-btn"
              aria-label="附加上下文"
              title="附加上下文"
              @click="emit('update:attachOpen', !attachOpen)"
            >
              <svg width="14" height="14" viewBox="0 0 14 14" fill="none"><path d="M7 2.5v9M2.5 7h9" stroke="currentColor" stroke-width="1.4" stroke-linecap="round"/></svg>
            </button>
            <div class="aa-attach-menu" :class="{ 'is-open': attachOpen }">
              <button
                v-for="item in ATTACH_KINDS"
                :key="item.kind"
                type="button"
                @click="emit('attach', item.kind, item.name)"
              >{{ item.name }}</button>
            </div>
          </div>
          <div class="aa-seg">
            <button type="button" :class="{ 'is-on': mode === 'open' }" @click="emit('update:mode', 'open')">开放任务</button>
            <button type="button" :class="{ 'is-on': mode === 'planned' }" @click="emit('update:mode', 'planned')">计划任务</button>
          </div>
          <select class="aa-select" :value="agentId" @change="emit('update:agentId', ($event.target as HTMLSelectElement).value)">
            <option v-if="!agents.length" value="">智能体</option>
            <option v-for="a in agents" :key="a.id" :value="a.id">{{ a.name }}</option>
          </select>
          <select class="aa-select" :value="modelId" @change="emit('update:modelId', ($event.target as HTMLSelectElement).value)">
            <option v-if="!models.length" value="">已配置模型</option>
            <option v-for="m in models" :key="m.id" :value="m.id">{{ m.name }}</option>
          </select>
          <span class="aa-spacer"></span>
          <button
            v-if="!running"
            type="button"
            class="aa-send"
            aria-label="发送"
            :disabled="!draft.trim()"
            @click="canSend() && emit('send')"
          >
            <svg width="14" height="14" viewBox="0 0 14 14" fill="none"><path d="M2 7h10M8 3l4 4-4 4" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/></svg>
          </button>
          <button
            v-else
            type="button"
            class="aa-send is-stop"
            aria-label="停止"
            title="任务进行中，可以停止，或等它完成后再继续追问"
            @click="emit('stop')"
          >
            <svg width="10" height="10" viewBox="0 0 10 10"><rect x="1" y="1" width="8" height="8" fill="currentColor" rx="1"/></svg>
          </button>
        </div>
      </div>
      <p class="aa-hint">{{ running ? '任务进行中，可以停止，或等它完成后再继续追问。' : '复用宿主已配置的模型，不另开账号。' }}</p>
    </div>
  </div>
</template>
