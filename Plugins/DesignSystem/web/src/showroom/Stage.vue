<script setup lang="ts">
/**
 * 舞台（§U DOM 契约）：`[data-stage]` 带 `data-outfit / data-theme / data-device / data-scene / data-page`；
 * 场景页签与页面页签都是 `role="tab"`（名即判据）；控件组 `aria-label="明暗" / "疏密" / "设备" / "视图"`。
 *
 * 舞台里的模特页只认 `--ds-*` 变量：`OutfitScope` 把**后端文本**的 CSS 收窄到本件衣服上，
 * 于是"画布上看到的"与"导出交付的"必然同源（同源纪律）。
 *
 * 视图档（输入22）解决"预览区太小、只看到部分"：
 * - `fit`（默认）：按画布可用宽度等比缩放整页，一屏看全貌；缩放比来自 `fit.ts` 的纯函数（有单测）。
 * - `actual`：1:1 真实像素，超出部分由 `overflow:auto` 的滚动条到达（纵横都有，任何区域可达）。
 * - `max`：由 `Showroom` 让开两侧栏，画布吃满可用宽高；仍保留拖拽改尺寸。
 * 三档都可**拖右下角改画布盒尺寸**（原生 `resize`，不自研拖拽数学）；`fit` 下改宽会重算缩放比。
 * 用 CSS `zoom` 而不是 `transform: scale()`：`zoom` 会参与布局，盒子高度自动跟着缩，
 * 不必再测一次内容高去折算（那需要第二个 ResizeObserver，且容易和滚动条互相抖）。
 */
import type { Component } from 'vue'
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import DeviceFrame from './DeviceFrame.vue'
import OutfitScope from './OutfitScope.vue'
import { DEVICES, SCENES, deviceById, sceneById, type DeviceId } from './scenes'
import { VIEW_MODES, fitScale, type ViewMode } from './fit'
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
  view: ViewMode
}>()

const emit = defineEmits<{
  'update:theme': [code: string]
  'update:density': [id: DensityId]
  'update:device': [id: DeviceId]
  'update:view': [id: ViewMode]
  'pick:scene': [id: string]
  'pick:page': [id: string]
}>()

/** 明暗标签：后端固定明/暗两档说人话，项目自定义主题档直接用它的 code（不编中文名） */
const THEME_LABELS: Record<string, string> = { light: '浅色', dark: '深色' }
const themeOptions = computed(() => props.outfit.themes.map((code) => ({ code, label: THEME_LABELS[code] ?? code })))

const scene = computed(() => sceneById(props.sceneId))
const pageOptions = computed(() => scene.value?.pages ?? [])

/** 稿宽＝该设备档的框宽（与 `DeviceFrame` 同一真源，含同一回落规则） */
const frameWidth = computed(() => deviceById(props.device)?.width ?? deviceById('desktop')!.width)

const viewportEl = ref<HTMLElement | null>(null)
const availWidth = ref(0)
let observer: ResizeObserver | null = null

/** 适应档的实际缩放比；非适应档恒为 1（1:1 或最大化下靠滚动条，不缩） */
const scale = computed(() => (props.view === 'fit' ? fitScale(availWidth.value, frameWidth.value) : 1))

onMounted(() => {
  const el = viewportEl.value
  if (!el) return
  availWidth.value = el.clientWidth
  if (typeof ResizeObserver === 'undefined') return
  observer = new ResizeObserver(() => {
    availWidth.value = viewportEl.value?.clientWidth ?? 0
  })
  observer.observe(el)
})

onBeforeUnmount(() => {
  observer?.disconnect()
  observer = null
})
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
      <div class="ds-stage__control" role="radiogroup" aria-label="视图">
        <button
          v-for="m in VIEW_MODES"
          :key="m.id"
          type="button"
          class="ds-chip"
          :class="{ 'ds-chip--on': view === m.id }"
          role="radio"
          :aria-checked="view === m.id"
          :title="m.hint"
          @click="emit('update:view', m.id)"
        >
          {{ m.label }}
        </button>
      </div>
      <span class="ds-stage__zoom ds-small" data-stage-zoom>{{ Math.round(scale * 100) }}%</span>
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

    <div
      ref="viewportEl"
      class="ds-stage__viewport"
      :class="`ds-stage__viewport--${view}`"
      data-stage-viewport
    >
      <div class="ds-stage__scaler" :style="{ zoom: scale }">
        <DeviceFrame :device="device">
          <OutfitScope :outfit-id="outfit.id" :css="css">
            <component :is="mannequin" v-if="mannequin" />
            <p v-else class="ds-stage__fallback ds-small">该页面还没有模特。</p>
          </OutfitScope>
        </DeviceFrame>
      </div>
    </div>

    <p v-if="view !== 'fit'" class="ds-stage__hint ds-small" data-stage-hint>
      画布外的部分：滚轮或拖滚动条即可到达；拖画布右下角可改画布尺寸。
    </p>

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
  min-width: 240px;
  max-width: 100%;
  overflow: auto;
}
/* 「有滚动条」在本机 Chrome 上只能靠"可达 + 提示"，不能靠 CSS 造一条常驻条：
 * 实测（2026-10-05，e2e E2 探针）`overflow:scroll` 的空白 div 条宽 = 0（浮层滚动条，滚动/悬停时才画），
 * 且给本元素写 `::-webkit-scrollbar { width: 40px; background: #f0f }` **完全没画出来**
 * （占位仍是 0px）⇒ 自定义滚动条样式被这台浏览器的滚动条策略忽略。
 * 所以这里只保留 `overflow: auto`，把"还有东西在画布外"这件事用下面那行提示说出来。 */
.ds-stage__scaler {
  min-width: 0;
}
.ds-stage__hint {
  margin: 0;
  color: var(--ds-fg-2);
}
/* 适应：整页按可用宽等比缩放（宽度不被裁），高度封顶 ⇒ 超长页走盒内滚动，不把整页拉成两屏 */
.ds-stage__viewport--fit {
  max-height: min(80vh, 940px);
  resize: horizontal;
}
/* 1:1：真实像素，纵横滚动条都在（任何区域可达）；盒子可拖宽拖高 */
.ds-stage__viewport--actual {
  height: min(70vh, 900px);
  resize: both;
}
/* 最大化：两侧栏已由 Showroom 让开，这里吃满可用高度，仍保留滚动条与拖拽 */
.ds-stage__viewport--max {
  height: calc(100vh - 300px);
  min-height: 320px;
  resize: both;
}
.ds-stage__zoom {
  margin-left: auto;
  color: var(--ds-fg-2);
  font-variant-numeric: tabular-nums;
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