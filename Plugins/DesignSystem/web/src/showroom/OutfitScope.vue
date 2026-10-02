<script setup lang="ts">
/**
 * 换肤作用域容器（03-plan §S）：把一段**后端导出 CSS** 收窄到本容器的 `[data-outfit]` 上，
 * 里面放的模特页只读 `--ds-*` 变量 —— 于是"画布上看到的"与"导出交付的"必然同源。
 *
 * 为什么用属性选择器而不是固定类名：并排对比要在同一页同时挂两份皮肤，作用域必须逐件隔离，
 * 否则后注入的那份会覆盖前一份（§U 的 `[data-stage-frame]` 两帧正是靠它互不污染）。
 *
 * `outfitId` 会进选择器，所以必须先过白名单正则 —— 否则一个带引号的 id 就能改写选择器结构。
 */
import { computed, ref, watch } from 'vue'
import { pickVars, scopeCssToSkin } from '../design/skin'

const props = defineProps<{
  /** 衣服 id（`preset:<id>` / `project:<code>` / `tuned:<n>`），须匹配白名单正则 */
  outfitId: string
  /** 后端 CSS 原文（`export?format=css` 或 `preview-css`） */
  css: string
  /** 可选：只注入这些 `--ds-*` 变量（缩略图减重），未列出的依赖会被 `pickVars` 沿引用链补全 */
  wanted?: string[] | null
}>()

/** 衣服 id 白名单：只允许小写字母/数字/冒号/下划线/连字符（进选择器前必须成立） */
const ID_PATTERN = /^[a-z0-9:_-]+$/

const valid = computed(() => ID_PATTERN.test(props.outfitId))

// 非法 id 只警告一次（列表里同一件坏衣服会反复渲染，警告刷屏会淹没真正的问题）
const warned = ref(false)
watch(
  valid,
  (ok) => {
    if (ok || warned.value) return
    warned.value = true
    console.warn(`[design-system] 跳过渲染：非法衣服 id "${props.outfitId}"（须匹配 ^[a-z0-9:_-]+$）`)
  },
  { immediate: true },
)

/** 收窄后的 CSS：可选 `pickVars` 取子集 → 作用域替换为 `[data-outfit="<id>"]` */
const scopedCss = computed(() => {
  if (!valid.value) return ''
  const wanted = props.wanted && props.wanted.length ? new Set(props.wanted) : null
  const src = wanted ? pickVars(props.css, wanted) : props.css
  return scopeCssToSkin(src, `[data-outfit="${props.outfitId}"]`)
})
</script>

<template>
  <div v-if="valid" class="ds-outfit" :data-outfit="outfitId">
    <component :is="'style'">{{ scopedCss }}</component>
    <slot />
  </div>
</template>