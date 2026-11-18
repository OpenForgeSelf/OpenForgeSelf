<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import AgentAssistant from './AgentAssistant.vue'
import type { AgentAssistantLayout } from './types'

const route = useRoute()
const layout = computed<AgentAssistantLayout>(() =>
  route.query.layout === 'panel' ? 'panel' : 'page'
)
</script>

<template>
  <AgentAssistant :layout="layout" :context="{ pluginId: 'ai-agent' }" />
</template>
