<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import AgentAssistant from './AgentAssistant.vue'
import { PAGE_TOOL_SCOPE, agentFocusFromQuery } from './session.logic'
import type { AgentAssistantLayout } from './types'

const route = useRoute()
const layout = computed<AgentAssistantLayout>(() =>
  route.query.layout === 'panel' ? 'panel' : 'page'
)
const focus = computed(() => agentFocusFromQuery(route.query as Record<string, unknown>))
</script>

<template>
  <AgentAssistant
    :layout="layout"
    :context="{ pluginId: 'ai-agent' }"
    :tool-scope="PAGE_TOOL_SCOPE"
    :initial-session="focus.sessionId"
    :initial-plan-id="focus.planId"
  />
</template>
