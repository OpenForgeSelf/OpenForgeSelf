<script setup lang="ts">
/**
 * Distinct brand mark for Runtime / Agent rows.
 * Prefers OAR brand SVG; falls back to a letter glyph (never a shared placeholder).
 */
import { computed } from 'vue'
import { brandGlyph, brandIconUrl } from './runtimeBrand'

const props = defineProps<{
  runtimeId?: string | null
  vendor?: string | null
  name?: string | null
  size?: number
}>()

const url = computed(() => brandIconUrl(props.runtimeId) || brandIconUrl(props.vendor))
const glyph = computed(() => brandGlyph(props.runtimeId || props.vendor, props.name))
const px = computed(() => props.size ?? 28)
</script>

<template>
  <span
    class="ok-brand"
    :style="{ width: px + 'px', height: px + 'px' }"
    :title="name || runtimeId || vendor || ''"
    aria-hidden="true"
  >
    <img v-if="url" class="ok-brand__img" :src="url" alt="" />
    <span v-else class="ok-brand__glyph">{{ glyph }}</span>
  </span>
</template>

<style>
.ok-brand {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  border-radius: var(--el-border-radius-base, 6px);
  background: var(--el-fill-color-light);
  border: 1px solid var(--el-border-color-lighter);
  overflow: hidden;
}
.ok-brand__img {
  width: 70%;
  height: 70%;
  object-fit: contain;
  display: block;
}
.ok-brand__glyph {
  font-size: 12px;
  font-weight: 700;
  font-family: var(--el-font-family, system-ui, sans-serif);
  color: var(--el-text-color-primary);
  letter-spacing: 0;
  line-height: 1;
}
</style>
