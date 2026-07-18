<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'

const route = useRoute()

const pluginId = computed(() => route.meta?.pluginId as string)
const title = computed(() => route.meta?.title as string)
</script>

<template>
  <div class="plugin-page">
    <h2>{{ title || 'Plugin Page' }}</h2>
    <p v-if="pluginId">Plugin: {{ pluginId }}</p>
    <p>Plugin content will be rendered here.</p>
  </div>
</template>

<style scoped>
.plugin-page {
  padding: 20px;
}
</style>
