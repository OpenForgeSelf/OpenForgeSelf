<script setup lang="ts">
import { computed, onMounted, watch } from 'vue'
import AgentComposer from './AgentComposer.vue'
import AgentEmptyState from './AgentEmptyState.vue'
import AgentPageHead from './AgentPageHead.vue'
import AgentSessionList from './AgentSessionList.vue'
import AgentThread from './AgentThread.vue'
import AgentWorkspace from './AgentWorkspace.vue'
import './assistant.css'
import type { AgentAssistantLayout, AgentEmbedContext, AgentToolScope, AgentAttachment } from './types'
import { useAgentSession } from './useAgentSession'

const props = withDefaults(
  defineProps<{
    layout?: AgentAssistantLayout
    context?: AgentEmbedContext
    toolScope?: AgentToolScope
    surfaceKey?: string
    initialPanel?: string
  }>(),
  { layout: 'page' }
)

const emit = defineEmits<{
  insertText: [text: string]
  openPath: [path: string]
}>()

const session = useAgentSession({
  layout: () => props.layout,
  context: () => props.context ?? { pluginId: 'ai-agent' },
  toolScope: () => props.toolScope,
  surfaceKey: () => props.surfaceKey ?? props.context?.surfaceKey,
})

const rootClass = computed(() => [
  'agent-assistant',
  props.layout === 'panel' ? 'is-panel' : 'is-page',
  props.layout === 'panel' ? `is-${session.panelLayer.value}` : '',
])

function previewPanel() {
  const url = new URL(window.location.href)
  url.searchParams.set('layout', 'panel')
  window.location.assign(url.toString())
}

function addPin(kind: AgentAttachment['kind'], name: string) {
  const id = `${kind}-${Date.now()}`
  session.attachments.value = [...session.attachments.value, { kind, id, name }]
  session.attachOpen.value = false
}

function removePin(id: string) {
  session.attachments.value = session.attachments.value.filter((p) => p.id !== id)
}

async function onRetry() {
  await session.actStuck('retry')
}

onMounted(async () => {
  await session.loadCatalogs()
  await session.refreshSessions()
  if (props.initialPanel && props.initialPanel !== 'catalog') {
    await session.openPanel(props.initialPanel as 'plan')
  }
})

watch(
  () => props.layout,
  (layout) => {
    if (layout === 'page') session.panelLayer.value = 'chat'
  }
)

defineExpose({ insertText: (text: string) => emit('insertText', text) })
</script>

<template>
  <div :class="rootClass" data-testid="agent-assistant" :data-layout="layout">
    <AgentSessionList
      v-if="layout === 'page' || session.panelLayer.value === 'sessions'"
      :sessions="session.sessions.value"
      :active-id="session.activeId.value"
      :show-demo="layout === 'page'"
      :show-back="layout === 'panel'"
      @select="session.openSession($event); session.panelLayer.value = 'chat'"
      @demo="previewPanel"
      @chat="session.panelLayer.value = 'chat'"
    />
    <div v-if="layout === 'page' || session.panelLayer.value === 'chat'" class="aa-main">
      <AgentPageHead
        :layout="layout"
        :title="session.title.value"
        :mode="session.mode.value"
        :kind="session.kind.value"
        @new="session.newSession()"
        @confirm="session.confirm()"
        @sessions="session.panelLayer.value = 'sessions'"
        @workspace="session.panelLayer.value = 'workspace'"
      />
      <p v-if="session.error.value" class="aa-error">{{ session.error.value }}</p>
      <div class="aa-stage" :class="{ 'is-empty': session.empty.value, 'has-rail': session.railOpen.value }">
        <AgentEmptyState />
        <AgentThread
          :items="session.thread.items"
          :stuck-input="session.stuckInput.value"
          @update:stuck-input="session.stuckInput.value = $event"
          @decide="(id, approved) => session.decide(id, approved)"
          @intervene="session.actStuck($event)"
        />
        <AgentComposer
          :draft="session.draft.value"
          @update:draft="session.draft.value = $event"
          :mode="session.mode.value"
          :agent-id="session.agentId.value"
          :model-id="session.modelId.value"
          :agents="session.agents.value"
          :models="session.models.value"
          :attachments="session.attachments.value"
          :attach-open="session.attachOpen.value"
          :running="session.running.value"
          @update:mode="session.changeMode($event)"
          @update:agent-id="session.agentId.value = $event"
          @update:model-id="session.modelId.value = $event"
          @update:attach-open="session.attachOpen.value = $event"
          @send="session.send()"
          @stop="session.stop()"
          @attach="addPin"
          @remove-pin="removePin"
        />
      </div>
    </div>
    <AgentWorkspace
      v-if="layout === 'page' || session.panelLayer.value === 'workspace'"
      :layout="layout"
      :panel="session.workspacePanel.value"
      :catalog="session.thread.catalog"
      :plan="session.thread.plan"
      :detail="session.workspaceDetail.value"
      :kind="session.kind.value"
      :revising="session.revising.value"
      :status="session.thread.status"
      :revise-note="session.reviseNote.value"
      @update:revise-note="session.reviseNote.value = $event"
      :memory-draft="session.memoryDraft.value"
      @update:memory-draft="session.memoryDraft.value = $event"
      @chat="session.panelLayer.value = 'chat'"
      @open="session.openPanel($event)"
      @confirm="session.confirm()"
      @start-revise="session.revising.value = true"
      @revise="session.revise()"
      @retry="onRetry"
      @write-memory="session.writeMemory()"
      @commit-design="session.commitDesign()"
      @dispatch-todo="session.dispatchTodo($event)"
      @pause-workflow="session.pauseWorkflow()"
      @cancel-workflow="session.cancelWorkflow()"
    />
  </div>
</template>
