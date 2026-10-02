<script setup lang="ts">
/**
 * 并排对比（§U DOM 契约）：`[data-compare]` 内**恰两个** `[data-stage-frame]` + 差异条 `[data-compare-diff]`。
 *
 * 互不污染靠 `OutfitScope` 的属性作用域：两件衣服各注入一份"收窄到 `[data-outfit="<id>"]`"的后端 CSS，
 * 同一页同时挂两份也不会互相覆盖（AC16 的 computed 底色断言正是查这一点）。
 *
 * 差异条只**照抄**两段后端 CSS 文本里的字面值（`readVar` 纯文本提取，不做任何换算），
 * 于是"差异条上写的"与"画布上生效的"必然同源 —— 与皮肤层同一条纪律。
 */
import type { Component } from 'vue'
import DeviceFrame from './DeviceFrame.vue'
import OutfitScope from './OutfitScope.vue'
import type { DeviceId } from './scenes'
import type { Outfit } from './outfits'

defineProps<{
  /** 参与对比的衣服（调用方保证长度恰为 2；不足两件时本组件不渲染差异条） */
  outfits: readonly Outfit[]
  /** 与 `outfits` 同序的后端 CSS 文本（各件首档主题、疏密适中） */
  css: readonly string[]
  /** 当前场景/页面对应的模特组件（两帧渲染同一页，便于同比） */
  mannequin?: Component
  /** 设备框档位（沿用舞台当前档） */
  device: DeviceId
  error: string
}>()

const emit = defineEmits<{ close: []; remove: [id: string] }>()

/**
 * 差异条展示的 5 个变量：底色 / 正文色 / 品牌色 / 圆角 / 正文字族。
 * 覆盖"颜色 + 尺度 + 排版"三类，且全部由切片 A 的 AC4 契约证明在 8 预设 × 明暗里都存在。
 */
const DIFF_VARS = [
  '--ds-semantic-surface-bg',
  '--ds-semantic-text-1',
  '--ds-semantic-brand',
  '--ds-radius-lg',
  '--ds-font-sans',
] as const

/**
 * 从后端 CSS 文本里读某个变量的**字面值**（不含解析/换算；找不到回空串）。
 *
 * @param css  后端 `preview-css` / `export?format=css` 原文
 * @param name 变量名（如 `--ds-semantic-brand`）
 * @returns 该变量在 `:root{…}` 内的原始值文本；未定义时为空串
 */
function readVar(css: string, name: string): string {
  const root = /:root\s*\{([\s\S]*?)\}/.exec(css)
  if (!root) return ''
  const escaped = name.replace(/[-/\\^$*+?.()|[\]{}]/g, '\\$&')
  const m = new RegExp(`(?:^|[;{\\s])${escaped}\\s*:\\s*([^;]+);`).exec(root[1])
  return m ? m[1].trim() : ''
}
</script>

<template>
  <section class="ds-compare" data-compare>
    <div class="ds-compare__head">
      <span class="ds-micro">并排对比</span>
      <span class="ds-small">两份皮肤同时挂在同一页，各自只作用于自己的画布（互不污染）。</span>
      <button type="button" class="ds-compare__close" @click="emit('close')">关闭对比</button>
    </div>

    <div class="ds-compare__stage">
      <div v-for="(o, i) in outfits" :key="o.id" class="ds-compare__col">
        <div class="ds-compare__cap">
          <span class="ds-compare__name">{{ o.label }}</span>
          <button
            type="button"
            class="ds-compare__remove"
            :data-compare-remove="o.id"
            @click="emit('remove', o.id)"
          >
            移出
          </button>
        </div>
        <DeviceFrame :device="device">
          <OutfitScope :outfit-id="o.id" :css="css[i] ?? ''">
            <component :is="mannequin" v-if="mannequin" />
            <p v-else class="ds-compare__fallback ds-small">该页面还没有模特。</p>
          </OutfitScope>
        </DeviceFrame>
      </div>
    </div>

    <dl v-if="outfits.length === 2" class="ds-compare__diff" data-compare-diff>
      <div v-for="name in DIFF_VARS" :key="name" class="ds-compare__diffrow" :data-compare-var="name">
        <dt class="ds-compare__varname ds-mono">{{ name }}</dt>
        <dd class="ds-compare__varval ds-mono" data-compare-side="a">{{ readVar(css[0] ?? '', name) || '—' }}</dd>
        <dd class="ds-compare__varval ds-mono" data-compare-side="b">{{ readVar(css[1] ?? '', name) || '—' }}</dd>
      </div>
    </dl>

    <p v-if="error" class="ds-compare__error ds-small" role="alert">对比取数失败：{{ error }}</p>
  </section>
</template>

<style scoped>
.ds-compare {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-3);
  border-top: 1px solid var(--ds-border-1);
  padding-top: var(--ds-space-4);
}
.ds-compare__head {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: var(--ds-space-3);
}
.ds-compare__close {
  margin-inline-start: auto;
  font: inherit;
  font-size: var(--ds-fs-small);
  color: var(--ds-fg-1);
  background: var(--ds-surface-2);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-sm);
  padding: 6px var(--ds-space-3);
  cursor: pointer;
}
.ds-compare__stage {
  display: flex;
  gap: var(--ds-space-4);
  overflow-x: auto;
  align-items: flex-start;
}
.ds-compare__col {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-2);
  flex: none;
}
.ds-compare__cap {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--ds-space-2);
}
.ds-compare__name {
  font-size: var(--ds-fs-small);
  color: var(--ds-fg-1);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.ds-compare__remove {
  flex: none;
  font: inherit;
  font-size: var(--ds-fs-micro);
  color: var(--ds-fg-3);
  background: transparent;
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-sm);
  padding: var(--ds-space-1) var(--ds-space-2);
  cursor: pointer;
}
.ds-compare__remove:hover {
  background: var(--ds-surface-2);
}
.ds-compare__fallback {
  margin: 0;
  padding: var(--ds-space-5);
}
.ds-compare__diff {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-1);
  margin: 0;
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-md);
  background: var(--ds-surface-1);
  padding: var(--ds-space-3);
}
.ds-compare__diffrow {
  display: grid;
  grid-template-columns: minmax(0, 1.4fr) minmax(0, 1fr) minmax(0, 1fr);
  gap: var(--ds-space-3);
  align-items: baseline;
}
.ds-compare__varname {
  margin: 0;
  font-size: var(--ds-fs-micro);
  color: var(--ds-fg-3);
  overflow-wrap: anywhere;
}
.ds-compare__varval {
  margin: 0;
  font-size: var(--ds-fs-micro);
  color: var(--ds-fg-1);
  overflow-wrap: anywhere;
}
.ds-compare__error {
  margin: 0;
  color: var(--ds-danger);
}
</style>