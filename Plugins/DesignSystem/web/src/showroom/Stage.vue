<script setup lang="ts">
/**
 * 舞台（§U DOM 契约）：`[data-stage]` 带 `data-outfit / data-theme / data-device / data-scene / data-page`；
 * 场景页签与页面页签都是 `role="tab"`（名即判据）；三个控件 `aria-label="明暗" / "疏密" / "设备"`。
 *
 * 舞台里的模特页只认 `--ds-*` 变量：`OutfitScope` 把**后端文本**的 CSS 收窄到本件衣服上，
 * 于是"画布上看到的"与"导出交付的"必然同源（同源纪律）。
 * 设备框用 `DeviceFrame`，超宽时由本组件的 `.ds-stage__viewport` 横向滚动承接，不让整页撑破。
 */
import type { Component } from 'vue'
import { computed } from 'vue'
import DeviceFrame from './DeviceFrame.vue'
import OutfitScope from './OutfitScope.vue'
import { DEVICES, SCENES, sceneById, type DeviceId } from './scenes'
import { DENSITY_OPTIONS, type DensityId } from './tune'
import type { Outfit } from './outfits'

const props = defineProps<{
  outfit: Outfit
  /** 已解析的模特组件（`scenes.ts` 的 `component` 键 → SFC，由 Showroom 映射） */
  mannequin?: Component
  css: string
  error: string
  theme: string
  density: DensityId
  device: DeviceId
  sceneId: string
  pageId: string
}>()

const emit = defineEmits<{
  'update:theme': [code: string]
  'update:density': [id: DensityId]
  'update:device': [id: DeviceId]
  'pick:scene': [id: string]
  'pick:page': [id: string]
}>()

/** 明暗标签：后端固定明/暗两档说人话，项目自定义主题档直接用它的 code（不编中文名） */
const THEME_LABELS: Record<string, string> = { light: '浅色', dark: '深色' }
const themeOptions = computed(() => props.outfit.themes.map((code) => ({ code, label: THEME_LABELS[code] ?? code })))

const scene = computed(() => sceneById(props.sceneId))
const pageOptions = computed(() => scene.value?.pages ?? [])
</script>

<template>
  <section
    class="ds-stage"
    data-stage
    :data-outfit="outfit.id"
    :data-theme="theme"
    :data-device="device"
    :data-scene="sceneId"
    :data-page="pageId"
  >
    <div class="ds-stage__bar">
      <div class="ds-stage__control" role="radiogroup" aria-label="明暗">
        <button
          v-for="t in themeOptions"
          :key="t.code"
          type="button"
          class="ds-chip"
          :class="{ 'ds-chip--on': theme === t.code }"
          role="radio"
          :aria-checked="theme === t.code"
          @click="emit('update:theme', t.code)"
        >
          {{ t.label }}
        </button>
      </div>
      <div class="ds-stage__control" role="radiogroup" aria-label="疏密">
        <button
          v-for="d in DENSITY_OPTIONS"
          :key="d.id"
          type="button"
          class="ds-chip"
          :class="{ 'ds-chip--on': density === d.id }"
          role="radio"
          :aria-checked="density === d.id"
          @click="emit('update:density', d.id)"
        >
          {{ d.label }}
        </button>
      </div>
      <div class="ds-stage__control" role="radiogroup" aria-label="设备">
        <button
          v-for="d in DEVICES"
          :key="d.id"
          type="button"
          class="ds-chip"
          :class="{ 'ds-chip--on': device === d.id }"
          role="radio"
          :aria-checked="device === d.id"
          @click="emit('update:device', d.id)"
        >
          {{ d.label }}
        </button>
      </div>
    </div>

    <div class="ds-stage__tabs" role="tablist" aria-label="场景">
      <button
        v-for="s in SCENES"
        :key="s.id"
        type="button"
        class="ds-stage__tab"
        :class="{ 'ds-stage__tab--on': sceneId === s.id }"
        role="tab"
        :aria-selected="sceneId === s.id"
        @click="emit('pick:scene', s.id)"
      >
        {{ s.label }}
      </button>
    </div>

    <div class="ds-stage__tabs" role="tablist" aria-label="页面">
      <button
        v-for="p in pageOptions"
        :key="p.id"
        type="button"
        class="ds-stage__tab"
        :class="{ 'ds-stage__tab--on': pageId === p.id }"
        role="tab"
        :aria-selected="pageId === p.id"
        @click="emit('pick:page', p.id)"
      >
        {{ p.label }}
      </button>
    </div>

    <div class="ds-stage__viewport">
      <DeviceFrame :device="device">
        <OutfitScope :outfit-id="outfit.id" :css="css">
          <component :is="mannequin" v-if="mannequin" />
          <p v-else class="ds-stage__fallback ds-small">该页面还没有模特。</p>
        </OutfitScope>
      </DeviceFrame>
    </div>

    <p v-if="error" class="ds-stage__error ds-small" role="alert">舞台取数失败：{{ error }}</p>
  </section>
</template>

<style scoped>
.ds-stage {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-3);
  min-width: 0;
}
.ds-stage__bar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--ds-space-2) var(--ds-space-4);
}
.ds-stage__control {
  display: flex;
  align-items: center;
  gap: var(--ds-space-1);
}
.ds-stage__tabs {
  display: flex;
  flex-wrap: wrap;
  gap: var(--ds-space-1);
  border-bottom: 1px solid var(--ds-border-1);
}
.ds-stage__tab {
  font: inherit;
  font-size: var(--ds-fs-small);
  padding: var(--ds-space-2) var(--ds-space-3);
  border: none;
  border-bottom: 2px solid transparent;
  background: transparent;
  color: var(--ds-fg-2);
  cursor: pointer;
}
.ds-stage__tab--on {
  color: var(--ds-color-primary);
  border-bottom-color: var(--ds-color-primary);
}
.ds-stage__viewport {
  width: 100%;
  min-width: 0;
  overflow-x: auto;
}
.ds-stage__fallback {
  margin: 0;
  padding: var(--ds-space-5);
}
.ds-stage__error {
  margin: 0;
  color: var(--ds-danger);
}
</style>