<script setup lang="ts">
/**
 * 衣柜（§U DOM 契约）：`role="listbox" aria-label="衣柜"`，每件衣服是 `role="option" [data-outfit-id]`。
 *
 * 方向键在选项间移动（AC23）：roving tabindex —— 只有选中项可 Tab 进入，进入后用上下键换。
 * 组合成**一个** listbox（而不是"我的/预设"两个），因为它们是同一件事（挑一件衣服试穿），
 * 分两个 listbox 会让屏幕阅读器报两个互不相关的列表。
 */
import { computed, nextTick, ref } from 'vue'
import type { Outfit } from './outfits'

const props = defineProps<{
  mine: readonly Outfit[]
  presets: readonly Outfit[]
  selectedId: string
  /** 已加入并排对比的衣服 id（≤2；按钮据此显示「加入对比 / 移出对比」） */
  compareIds: readonly string[]
  /** 因超过上限而未显示的项目数（>0 时给「更多」入口） */
  hiddenCount: number
  /** 已建但还没生成令牌的项目数（>0 时提示去工作台生成） */
  ungeneratedCount: number
}>()

const emit = defineEmits<{ select: [id: string]; compare: [id: string]; more: [] }>()

const KIND_LABELS: Record<Outfit['kind'], string> = { project: '我的', preset: '预设', tuned: '微调' }

/** 选项 = 我的 + 预设（顺序即后端顺序，与 `buildOutfits` 一致） */
const options = computed<Outfit[]>(() => [...props.mine, ...props.presets])

const list = ref<HTMLElement | null>(null)

function focusOption(id: string): void {
  void nextTick(() => {
    list.value?.querySelector<HTMLElement>(`[data-outfit-id="${id}"]`)?.focus()
  })
}

function pick(id: string): void {
  emit('select', id)
  focusOption(id)
}

/** 上下键移动选中并带焦点（不跳项、不越界；Home/End 到首尾） */
function onKeydown(e: KeyboardEvent, index: number): void {
  const last = options.value.length - 1
  const target =
    e.key === 'ArrowDown' ? Math.min(last, index + 1)
    : e.key === 'ArrowUp' ? Math.max(0, index - 1)
    : e.key === 'Home' ? 0
    : e.key === 'End' ? last
    : -1
  if (target < 0) return
  e.preventDefault()
  pick(options.value[target].id)
}
</script>

<template>
  <aside class="ds-wardrobe">
    <div class="ds-micro">衣柜</div>
    <div ref="list" class="ds-wardrobe__list" role="listbox" aria-label="衣柜">
      <!-- 每件衣服一行：左边是试穿选项（`role="option"`），右边是「加入对比」开关（§U） -->
      <div v-for="(o, i) in options" :key="o.id" class="ds-wardrobe__row">
        <button
          type="button"
          class="ds-wardrobe__opt"
          :class="{ 'ds-wardrobe__opt--on': o.id === selectedId }"
          role="option"
          :data-outfit-id="o.id"
          :aria-selected="o.id === selectedId"
          :tabindex="o.id === selectedId ? 0 : -1"
          @click="pick(o.id)"
          @keydown="onKeydown($event, i)"
        >
          <span class="ds-wardrobe__label">{{ o.label }}</span>
          <span class="ds-wardrobe__kind">{{ KIND_LABELS[o.kind] }}</span>
        </button>
        <button
          type="button"
          class="ds-wardrobe__cmp"
          :class="{ 'ds-wardrobe__cmp--on': compareIds.includes(o.id) }"
          :data-compare-toggle="o.id"
          :aria-pressed="compareIds.includes(o.id)"
          @click="emit('compare', o.id)"
        >
          {{ compareIds.includes(o.id) ? '移出对比' : '加入对比' }}
        </button>
      </div>
    </div>
    <button v-if="hiddenCount > 0" type="button" class="ds-wardrobe__more ds-small" @click="emit('more')">
      更多（{{ hiddenCount }}）
    </button>
    <p class="ds-wardrobe__hint ds-small">试穿与微调都不会写入设计系统库。</p>
    <p v-if="ungeneratedCount > 0" class="ds-wardrobe__hint ds-small">
      有 {{ ungeneratedCount }} 个项目还没生成令牌，去工作台生成后再回来试穿。
    </p>
  </aside>
</template>

<style scoped>
.ds-wardrobe {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-3);
  min-width: 0;
}
.ds-wardrobe__list {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-1);
  max-height: 60vh;
  overflow-y: auto;
}
.ds-wardrobe__row {
  display: flex;
  align-items: stretch;
  gap: var(--ds-space-1);
  min-width: 0;
}
.ds-wardrobe__opt {
  flex: 1;
  min-width: 0;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--ds-space-2);
  font: inherit;
  font-size: var(--ds-fs-small);
  text-align: left;
  padding: var(--ds-space-2) var(--ds-space-3);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-md);
  background: var(--ds-surface-1);
  color: var(--ds-fg-1);
  cursor: pointer;
}
.ds-wardrobe__cmp {
  flex: none;
  font: inherit;
  font-size: var(--ds-fs-micro);
  padding: var(--ds-space-1) var(--ds-space-2);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-sm);
  background: transparent;
  color: var(--ds-fg-3);
  cursor: pointer;
  white-space: nowrap;
}
.ds-wardrobe__cmp:hover {
  background: var(--ds-surface-2);
}
.ds-wardrobe__cmp--on {
  border-color: var(--ds-color-primary);
  color: var(--ds-color-primary);
}
.ds-wardrobe__opt:hover {
  background: var(--ds-surface-2);
}
.ds-wardrobe__opt--on {
  border-color: var(--ds-color-primary);
  box-shadow: var(--ds-shadow-sm);
}
.ds-wardrobe__label {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.ds-wardrobe__kind {
  flex: none;
  font-size: var(--ds-fs-micro);
  color: var(--ds-fg-3);
}
.ds-wardrobe__more {
  align-self: flex-start;
  font: inherit;
  font-size: var(--ds-fs-small);
  background: transparent;
  border: none;
  color: var(--ds-link);
  cursor: pointer;
  padding: 0;
}
/* 键盘可见焦点（AC23）：试穿项 / 对比开关 / 更多入口 都要看得见落在哪 */
.ds-wardrobe__opt:focus-visible,
.ds-wardrobe__cmp:focus-visible,
.ds-wardrobe__more:focus-visible {
  outline: 2px solid var(--ds-color-primary);
  outline-offset: 2px;
}
.ds-wardrobe__hint {
  margin: 0;
}
</style>