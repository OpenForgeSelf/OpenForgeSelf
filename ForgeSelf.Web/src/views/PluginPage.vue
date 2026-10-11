<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import PageHead from '@/components/PageHead.vue'
import EmptyState from '@/components/EmptyState.vue'

const route = useRoute()

const pluginId = computed(() => route.meta?.pluginId as string)
const title = computed(() => route.meta?.title as string)
</script>

<template>
  <div class="plugin-page">
    <PageHead
      eyebrow="Plugin · 插件页"
      :title="title || 'Plugin Page'"
    />

    <div class="page-content">
      <EmptyState
        v-if="pluginId"
        icon="<svg xmlns='http://www.w3.org/2000/svg' width='28' height='28' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><path d='M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z' /><polyline points='3.27 6.96 12 12.01 20.73 6.96' /><line x1='12' y1='22.08' x2='12' y2='12' /></svg>"
        :title="`插件 ${pluginId}`"
        sub="插件内容将在此渲染"
      />
      <EmptyState
        v-else
        icon="<svg xmlns='http://www.w3.org/2000/svg' width='28' height='28' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><path d='M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z' /><polyline points='3.27 6.96 12 12.01 20.73 6.96' /><line x1='12' y1='22.08' x2='12' y2='12' /></svg>"
        title="未指定插件"
        sub="从插件管理选择一个插件以查看详情"
      />
    </div>
  </div>
</template>

<style scoped>
.plugin-page {
  padding: 24px;
  min-height: 100%;
}

.page-content {
  max-width: 1200px;
  margin: 0 auto;
}
</style>
