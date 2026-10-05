<script setup lang="ts">
/**
 * 微调面板（§U 契约）：容器 `[data-tune]`，控件 `aria-label="品牌色" / "圆润度" / "疏密" / "动效"`，
 * 按钮 `还原`、`保存为新设计`。
 *
 * 映射全部委托给 `tune.ts`（向导与展厅共用一份，防止两处圆润度对不上）：
 * 品牌色 → seedColor；圆润度 → radiusBase；疏密 → density；动效 → motionScale。
 * 已存项目不在展厅内改（本项目里程碑刻意不做"项目另存起点"）——那时面板置灰并说明去处。
 */
import { computed } from 'vue'
import type { StyleAxisInfo } from '../api'
import { term } from '../design/glossary'
import { DENSITY_OPTIONS, MOTION_OPTIONS, RADIUS_MAX, RADIUS_MIN, isValidBrandColor, type StyleAxesState, type TuneState } from './tune'

const props = defineProps<{
  tune: TuneState
  /** 当前衣服是否支持微调（预设 / 微调衣服为真；已存项目为假） */
  enabled: boolean
  saving: boolean
  error: string
  /** 风格轴清单：一律来自 `GET meta.styleAxes`（本组件不列任何轴名或取值，见 vocabulary 守卫） */
  styleAxes: StyleAxisInfo[]
}>()

const emit = defineEmits<{
  'update:tune': [patch: Partial<TuneState>]
  reset: []
  save: []
}>()

const brandInvalid = computed(() => !isValidBrandColor(props.tune.seedColor))
/** 取色器要求一个具体色值：跟随预设时给一个中性起点（不影响 tune.seedColor 本身） */
const pickerValue = computed(() => props.tune.seedColor || '#3366ff')

function setSeed(v: string): void {
  emit('update:tune', { seedColor: v })
}

/** 轴控件的当前值：没选过就显示后端给的默认值（不显示成空白，否则用户以为没生效） */
function axisValue(axis: StyleAxisInfo): string | number {
  const v = props.tune.axes?.[axis.field]
  return v === undefined || v === '' ? axis.default : v
}

function setAxis(axis: StyleAxisInfo, value: string | number): void {
  const axes: StyleAxesState = { ...(props.tune.axes ?? {}), [axis.field]: value }
  emit('update:tune', { axes })
}

/** 数值轴的显示值（range 控件不接受 undefined） */
function axisNumber(axis: StyleAxisInfo): number {
  return Number(axisValue(axis))
}
</script>

<template>
  <aside class="ds-tune" data-tune>
    <div class="ds-micro">微调</div>

    <label class="ds-tune__field">
      <span class="ds-tune__label">品牌色</span>
      <span class="ds-tune__row">
        <input
          class="ds-tune__color"
          type="color"
          :value="pickerValue"
          aria-label="品牌色选择器"
          :disabled="!enabled"
          @input="setSeed(($event.target as HTMLInputElement).value)"
        />
        <input
          class="ds-tune__input ds-mono"
          type="text"
          aria-label="品牌色"
          placeholder="留空则跟随风格"
          :value="tune.seedColor"
          :disabled="!enabled"
          @input="setSeed(($event.target as HTMLInputElement).value)"
        />
      </span>
      <span v-if="brandInvalid" class="ds-tune__warn ds-small">品牌色要写成 #rgb 或 #rrggbb。</span>
    </label>

    <label class="ds-tune__field">
      <span class="ds-tune__label">圆润度（{{ tune.radiusBase }}）</span>
      <input
        class="ds-tune__range"
        type="range"
        :min="RADIUS_MIN"
        :max="RADIUS_MAX"
        step="1"
        aria-label="圆润度"
        :value="tune.radiusBase"
        :disabled="!enabled"
        @input="emit('update:tune', { radiusBase: Number(($event.target as HTMLInputElement).value) })"
      />
    </label>

    <div class="ds-tune__field">
      <span class="ds-tune__label">疏密</span>
      <div class="ds-tune__row" role="radiogroup" aria-label="疏密">
        <button
          v-for="d in DENSITY_OPTIONS"
          :key="d.id"
          type="button"
          class="ds-chip"
          :class="{ 'ds-chip--on': tune.density === d.id }"
          role="radio"
          :aria-checked="tune.density === d.id"
          :disabled="!enabled"
          @click="emit('update:tune', { density: d.id })"
        >
          {{ d.label }}
        </button>
      </div>
    </div>

    <div class="ds-tune__field">
      <span class="ds-tune__label">动效</span>
      <div class="ds-tune__row" role="radiogroup" aria-label="动效">
        <button
          v-for="m in MOTION_OPTIONS"
          :key="m.label"
          type="button"
          class="ds-chip"
          :class="{ 'ds-chip--on': tune.motionScale === m.value }"
          role="radio"
          :aria-checked="tune.motionScale === m.value"
          :disabled="!enabled"
          @click="emit('update:tune', { motionScale: m.value })"
        >
          {{ m.label }}
        </button>
      </div>
    </div>

    <details v-if="styleAxes.length" class="ds-tune__axes" data-style-axes>
      <summary class="ds-tune__label">{{ term('style axes') }}</summary>
      <p class="ds-tune__hint ds-small">{{ term('style axis hint') }}</p>

      <div v-for="axis in styleAxes" :key="axis.field" class="ds-tune__field" :data-axis="axis.field" :data-axis-kind="axis.kind">
        <span class="ds-tune__label">
          {{ axis.label }}<template v-if="axis.kind === 'number'">（{{ axisNumber(axis) }}）</template>
        </span>
        <div v-if="axis.kind === 'number'" class="ds-tune__row">
          <input
            class="ds-tune__range"
            type="range"
            :aria-label="axis.label"
            :min="axis.min"
            :max="axis.max"
            :step="axis.step ?? 0.05"
            :value="axisNumber(axis)"
            :disabled="!enabled"
            @input="setAxis(axis, Number(($event.target as HTMLInputElement).value))"
          />
        </div>
        <div v-else class="ds-tune__row" role="radiogroup" :aria-label="axis.label">
          <button
            v-for="v in axis.values ?? []"
            :key="v"
            type="button"
            class="ds-chip"
            :class="{ 'ds-chip--on': String(axisValue(axis)) === v }"
            role="radio"
            :aria-checked="String(axisValue(axis)) === v"
            :data-axis-value="v"
            :disabled="!enabled"
            @click="setAxis(axis, v)"
          >
            {{ axis.valueLabels?.[v] ?? v }}
          </button>
        </div>
      </div>
    </details>

    <div class="ds-tune__actions">
      <button type="button" class="ds-tune__btn" :disabled="!enabled" @click="emit('reset')">还原</button>
      <button
        type="button"
        class="ds-tune__btn ds-tune__btn--primary"
        :disabled="!enabled || saving || brandInvalid"
        @click="emit('save')"
      >
        {{ saving ? '正在保存…' : '保存为新设计' }}
      </button>
    </div>

    <p v-if="!enabled" class="ds-tune__hint ds-small">当前是已存项目：项目内微调请到工作台。</p>
    <p v-if="error" class="ds-tune__error ds-small" role="alert">{{ error }}</p>
  </aside>
</template>

<style scoped>
.ds-tune {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-4);
  min-width: 0;
}
.ds-tune__field {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-1);
}
.ds-tune__label {
  font-size: var(--ds-fs-small);
  color: var(--ds-fg-2);
}
.ds-tune__row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--ds-space-2);
}
.ds-tune__color {
  inline-size: 40px;
  block-size: 32px;
  padding: 0;
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-sm);
  background: var(--ds-surface-1);
}
.ds-tune__input {
  flex: 1;
  min-width: 0;
  font: inherit;
  font-size: var(--ds-fs-small);
  color: var(--ds-fg-1);
  background: var(--ds-surface-1);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-sm);
  padding: 6px var(--ds-space-3);
}
.ds-tune__range {
  inline-size: 100%;
}
.ds-tune__axes {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-2);
  padding: var(--ds-space-2) var(--ds-space-3);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-md);
  background: var(--ds-surface-2);
}
.ds-tune__axes > summary {
  cursor: pointer;
  font-size: var(--ds-fs-small);
  color: var(--ds-fg-2);
}
.ds-tune__axes[open] > summary {
  color: var(--ds-fg-1);
}
.ds-tune__actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--ds-space-2);
}
.ds-tune__btn {
  font: inherit;
  font-size: var(--ds-fs-small);
  color: var(--ds-fg-1);
  background: var(--ds-surface-2);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-sm);
  padding: 7px var(--ds-space-4);
  cursor: pointer;
}
.ds-tune__btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
.ds-tune__btn--primary {
  color: var(--ds-surface-1);
  background: var(--ds-color-primary);
  border-color: var(--ds-color-primary);
}
.ds-tune__btn--primary:disabled {
  color: var(--ds-fg-3);
  background: var(--ds-surface-2);
  border-color: var(--ds-border-1);
}
.ds-tune__warn,
.ds-tune__error {
  margin: 0;
  color: var(--ds-danger);
}
.ds-tune__hint {
  margin: 0;
}
</style>