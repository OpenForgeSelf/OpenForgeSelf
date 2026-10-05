<script setup lang="ts">
/**
 * 开始模式：四步向导（FR4）。
 *
 * 大白话优先（FR5）：所有专业名词经 `term()`，默认显示"设计变量/现成风格"这类说法，
 * 打开「专业术语」开关才显示 Token / 预设。状态机在 `wizard.ts`（纯模块，已单测），
 * 本组件只负责渲染与把用户动作转发给它。
 *
 * §U DOM 契约（e2e 判据）：根 `[data-start][data-step]`、`[data-scene]`、`[data-preset]`、
 * `aria-label="设计系统名称"` / `"项目代码"` / `"高级设置"`、按钮可访问名 `创建我的设计系统`、
 * 成功区 `[data-wizard-done]`（三个去向按钮）。
 */
import { computed, reactive, watch } from 'vue'
import { api } from '../api'
import { term } from '../design/glossary'
import { meta } from '../state'
import type { Mode } from '../shell/mode'
import { DENSITY_OPTIONS, MOTION_OPTIONS, RADIUS_MAX, RADIUS_MIN, axisFields } from '../showroom/tune'
import { SCENES, Wizard } from './wizard'

const emit = defineEmits<{ created: [code: string]; go: [mode: Mode] }>()

// 依赖注入：端口全部走 api.ts（路径只在那一处出现），回读走项目列表按 code 找（核对令牌数）
const w = reactive(
  new Wizard({
    recommend: (input) => api.recommendPresets(input),
    catalog: () => api.listPresets(),
    create: (input) => api.quickCreate(input),
    reload: async (code) => {
      const list = await api.listProjects()
      const p = list.find((x) => x.code === code)
      return p ? { tokenCount: p.tokenCount } : null
    },
  }),
)

/**
 * 风格轴控件的数据源：只读 `GET meta.styleAxes`（后端 `StyleAxes` 是唯一真源）。
 * 词表没到（还没加载完）时这一整块**不渲染** —— 摆一排空控件比不摆更糟。
 */
const styleAxes = computed(() => meta.value?.styleAxes ?? [])
watch(styleAxes, (v) => { w.axisFields = axisFields(v) }, { immediate: true })

/** 控件当前值：没选过就显示后端给的默认值，让用户看得见"现在在哪一档" */
function axisValue(field: string, fallback: string | number): string | number {
  const v = w.tune.axes?.[field]
  return v === undefined || v === '' ? fallback : v
}

const steps = [
  { n: 1, label: '做什么' },
  { n: 2, label: '挑风格' },
  { n: 3, label: '微调' },
  { n: 4, label: '起名创建' },
] as const

const nextLabel = computed(() => (w.step === 2 ? '就选它' : '下一步'))

function submit(): void {
  void w.submit().then(() => {
    if (w.phase === 'done' && w.created) emit('created', w.created.code)
  })
}
</script>

<template>
  <section class="ds-mode-pane ds-start" data-start :data-step="w.step">
    <ol class="ds-steps" aria-label="向导步骤">
      <li
        v-for="s in steps"
        :key="s.n"
        class="ds-steps__item"
        :class="{ 'ds-steps__item--on': w.step === s.n, 'ds-steps__item--done': w.step > s.n }"
        :aria-current="w.step === s.n ? 'step' : undefined"
      >
        <span class="ds-steps__no">{{ s.n }}</span>
        <span>{{ s.label }}</span>
      </li>
    </ol>

    <!-- ① 场景 -->
    <div v-if="w.step === 1" class="ds-card ds-stack ds-gap-4">
      <div class="ds-h3">你要做一套什么样的界面？</div>
      <div class="ds-small">不确定就选「其它」，之后随时能改。</div>
      <div class="ds-scenes">
        <button
          v-for="s in SCENES"
          :key="s.id"
          type="button"
          class="ds-scene"
          :class="{ 'ds-scene--on': w.scene === s.id }"
          :data-scene="s.id"
          :aria-pressed="w.scene === s.id"
          @click="w.pickScene(s.id)"
        >
          <span class="ds-scene__label">{{ s.label }}</span>
          <span class="ds-scene__hint">{{ s.hint }}</span>
        </button>
      </div>
      <label class="ds-field">
        <span class="ds-field__label">用一句话说说它（可留空）</span>
        <textarea
          class="ds-input ds-textarea"
          rows="2"
          aria-label="场景描述"
          :value="w.brief"
          placeholder="例如：一个管订单的后台，要清爽一点"
          @input="w.setBrief(($event.target as HTMLTextAreaElement).value)"
        />
      </label>
    </div>

    <!-- ② 风格 -->
    <div v-else-if="w.step === 2" class="ds-card ds-stack ds-gap-4">
      <div class="ds-h3">挑一套现成风格</div>
      <div class="ds-small">这些是内置风格，选一个最像的，下一步还能微调。</div>
      <p v-if="w.recommendError" class="ds-alert" role="alert">
        推荐暂时不可用（{{ w.recommendError }}），已为你列出全部风格。
      </p>
      <div v-if="w.phase === 'recommending' && !w.choices.length" class="ds-small">正在挑选最合适的…</div>
      <div class="ds-presets">
        <button
          v-for="p in w.choices"
          :key="p.id"
          type="button"
          class="ds-preset"
          :class="{ 'ds-preset--on': w.presetId === p.id }"
          :data-preset="p.id"
          :aria-pressed="w.presetId === p.id"
          @click="w.pickPreset(p.id)"
        >
          <span class="ds-preset__name">{{ p.name }}</span>
          <span class="ds-preset__tagline">{{ p.tagline }}</span>
        </button>
      </div>
      <button
        v-if="w.matches.length && !w.showAll"
        type="button"
        class="ds-link ds-small"
        @click="w.toggleShowAll(true)"
      >
        看看全部风格
      </button>
    </div>

    <!-- ③ 微调 -->
    <div v-else-if="w.step === 3" class="ds-card ds-stack ds-gap-4">
      <div class="ds-h3">顺手调一调（不想调就跳过）</div>
      <label class="ds-field">
        <span class="ds-field__label">品牌色</span>
        <span class="ds-row ds-gap-2">
          <input
            class="ds-color"
            type="color"
            :value="w.tune.seedColor || '#3366ff'"
            aria-label="品牌色选择器"
            @input="w.setTune({ seedColor: ($event.target as HTMLInputElement).value })"
          />
          <input
            class="ds-input ds-mono"
            type="text"
            aria-label="品牌色"
            placeholder="#3366ff（留空则跟随风格）"
            :value="w.tune.seedColor"
            @input="w.setTune({ seedColor: ($event.target as HTMLInputElement).value })"
          />
        </span>
        <span v-if="w.brandError()" class="ds-alert">{{ w.brandError() }}</span>
      </label>

      <div class="ds-field">
        <span class="ds-field__label">{{ term('density') }}</span>
        <div class="ds-row ds-wrap ds-gap-2" role="radiogroup" aria-label="疏密">
          <button
            v-for="d in DENSITY_OPTIONS"
            :key="d.id"
            type="button"
            class="ds-chip"
            :class="{ 'ds-chip--on': w.tune.density === d.id }"
            role="radio"
            :aria-checked="w.tune.density === d.id"
            @click="w.setTune({ density: d.id })"
          >
            {{ d.label }}
          </button>
        </div>
      </div>

      <label class="ds-field">
        <span class="ds-field__label">{{ term('radius') }}（{{ w.tune.radiusBase }}）</span>
        <input
          class="ds-range"
          type="range"
          :min="RADIUS_MIN"
          :max="RADIUS_MAX"
          step="1"
          aria-label="圆润度"
          :value="w.tune.radiusBase"
          @input="w.setTune({ radiusBase: Number(($event.target as HTMLInputElement).value) })"
        />
      </label>

      <div v-if="styleAxes.length" class="ds-field" data-style-axes>
        <details>
          <summary class="ds-field__label">{{ term('style axes') }}</summary>
          <p class="ds-micro">{{ term('style axis hint') }}</p>
          <div
            v-for="axis in styleAxes"
            :key="axis.field"
            class="ds-stack ds-gap-1"
            :data-axis="axis.field"
            :data-axis-kind="axis.kind"
          >
            <span class="ds-field__label">
              {{ axis.label }}<template v-if="axis.kind === 'number'">（{{ axisValue(axis.field, axis.default) }}）</template>
            </span>
            <input
              v-if="axis.kind === 'number'"
              class="ds-range"
              type="range"
              :aria-label="axis.label"
              :min="axis.min"
              :max="axis.max"
              :step="axis.step ?? 0.05"
              :value="axisValue(axis.field, axis.default)"
              @input="w.setAxis(axis.field, Number(($event.target as HTMLInputElement).value))"
            />
            <div v-else class="ds-row ds-wrap ds-gap-2" role="radiogroup" :aria-label="axis.label">
              <button
                v-for="v in axis.values ?? []"
                :key="v"
                type="button"
                class="ds-chip"
                :class="{ 'ds-chip--on': String(axisValue(axis.field, axis.default)) === v }"
                role="radio"
                :aria-checked="String(axisValue(axis.field, axis.default)) === v"
                :data-axis-value="v"
                @click="w.setAxis(axis.field, v)"
              >
                {{ axis.valueLabels?.[v] ?? v }}
              </button>
            </div>
          </div>
        </details>
      </div>

      <div class="ds-field">
        <span class="ds-field__label">动效</span>
        <div class="ds-row ds-wrap ds-gap-2" role="radiogroup" aria-label="动效">
          <button
            v-for="m in MOTION_OPTIONS"
            :key="m.label"
            type="button"
            class="ds-chip"
            :class="{ 'ds-chip--on': w.tune.motionScale === m.value }"
            role="radio"
            :aria-checked="w.tune.motionScale === m.value"
            @click="w.setTune({ motionScale: m.value })"
          >
            {{ m.label }}
          </button>
        </div>
      </div>
    </div>

    <!-- ④ 命名与创建 -->
    <div v-else class="ds-card ds-stack ds-gap-4">
      <template v-if="w.phase !== 'done'">
        <div class="ds-h3">给它起个名字</div>
        <label class="ds-field">
          <span class="ds-field__label">{{ term('design system') }}名称</span>
          <input
            class="ds-input"
            type="text"
            aria-label="设计系统名称"
            maxlength="60"
            :value="w.name"
            placeholder="例如：订单后台风格"
            @input="w.name = ($event.target as HTMLInputElement).value"
          />
        </label>

        <button
          type="button"
          class="ds-link ds-small"
          aria-label="高级设置"
          :aria-expanded="w.advancedOpen"
          @click="w.advancedOpen = !w.advancedOpen"
        >
          {{ w.advancedOpen ? '收起高级设置' : '高级设置（项目代码）' }}
        </button>
        <label v-if="w.advancedOpen" class="ds-field">
          <span class="ds-field__label">{{ term('quick-create') }}代码（留空自动生成）</span>
          <input
            class="ds-input ds-mono"
            type="text"
            aria-label="项目代码"
            :value="w.code"
            placeholder="小写字母/数字/中划线"
            @input="w.code = ($event.target as HTMLInputElement).value"
          />
          <span v-if="w.codeError()" class="ds-alert">{{ w.codeError() }}</span>
        </label>

        <p v-if="w.error" class="ds-alert" role="alert">{{ w.error }}</p>
        <div class="ds-small">
          选中的风格：<strong>{{ w.presetId ?? '（未选）' }}</strong> · 生成后会得到一整套
          {{ term('token') }}（{{ term('theme') }}、{{ term('component') }} 一并就绪）。
        </div>
      </template>

      <div v-else data-wizard-done class="ds-done ds-stack ds-gap-3">
        <div class="ds-h3">做好了：{{ w.created?.name }}</div>
        <div class="ds-small">
          项目代码 <span class="ds-mono">{{ w.created?.code }}</span> · 已生成
          <strong>{{ w.created?.tokenCount ?? 0 }}</strong> 条{{ term('token') }}
          <template v-if="!w.created?.tokenCount">（若为 0，请到工作台手动{{ term('generate') }}一次）</template>
        </div>
        <p v-for="(msg, i) in w.warnings" :key="i" class="ds-alert">{{ msg }}</p>
        <div class="ds-row ds-wrap ds-gap-2">
          <button type="button" class="ds-btn ds-btn--primary" @click="emit('go', 'showroom')">去展厅看看</button>
          <button type="button" class="ds-btn" @click="emit('go', 'delivery')">去交付与接入</button>
          <button type="button" class="ds-btn" @click="emit('go', 'workbench')">继续在工作台微调</button>
        </div>
      </div>
    </div>

    <div v-if="w.phase !== 'done'" class="ds-start__nav">
      <button type="button" class="ds-btn" :disabled="w.step === 1 || w.phase === 'creating'" @click="w.back()">上一步</button>
      <button
        v-if="w.step < 4"
        type="button"
        class="ds-btn ds-btn--primary"
        :disabled="!w.canAdvance()"
        @click="w.next()"
      >
        {{ nextLabel }}
      </button>
      <button
        v-else
        type="button"
        class="ds-btn ds-btn--primary"
        :disabled="!w.canSubmit() || w.phase === 'creating'"
        @click="submit"
      >
        {{ w.phase === 'creating' ? '正在创建…' : '创建我的设计系统' }}
      </button>
    </div>

  </section>
</template>

<style scoped>
.ds-start {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-4);
  max-width: 900px;
}
.ds-steps {
  display: flex;
  gap: var(--ds-space-4);
  list-style: none;
  margin: 0;
  padding: 0;
}
.ds-steps__item {
  display: flex;
  align-items: center;
  gap: var(--ds-space-2);
  font-size: var(--ds-fs-small);
  color: var(--ds-fg-3);
}
.ds-steps__item--on {
  color: var(--ds-color-primary);
  font-weight: var(--ds-fw-semibold);
}
.ds-steps__item--done {
  color: var(--ds-fg-2);
}
.ds-steps__no {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 22px;
  height: 22px;
  border-radius: var(--ds-radius-pill);
  border: 1px solid var(--ds-border-1);
  font-size: var(--ds-fs-micro);
}
.ds-steps__item--on .ds-steps__no {
  border-color: var(--ds-color-primary);
  background: var(--ds-color-primary);
  color: var(--ds-surface-1);
}
.ds-card {
  background: var(--ds-surface-1);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-lg);
  box-shadow: var(--ds-shadow-sm);
  padding: var(--ds-space-5);
}
.ds-scenes {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: var(--ds-space-3);
}
.ds-scene {
  display: flex;
  flex-direction: column;
  gap: 2px;
  text-align: left;
  font: inherit;
  padding: var(--ds-space-4);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-md);
  background: var(--ds-surface-2);
  color: var(--ds-fg-1);
  cursor: pointer;
}
.ds-scene--on {
  border-color: var(--ds-color-primary);
  box-shadow: var(--ds-shadow-sm);
}
.ds-scene__label {
  font-weight: var(--ds-fw-semibold);
}
.ds-scene__hint {
  font-size: var(--ds-fs-small);
  color: var(--ds-fg-3);
}
.ds-presets {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
  gap: var(--ds-space-3);
}
.ds-preset {
  display: flex;
  flex-direction: column;
  gap: 2px;
  text-align: left;
  font: inherit;
  padding: var(--ds-space-4);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-md);
  background: var(--ds-surface-2);
  color: var(--ds-fg-1);
  cursor: pointer;
}
.ds-preset--on {
  border-color: var(--ds-color-primary);
  box-shadow: var(--ds-shadow-sm);
}
.ds-preset__name {
  font-weight: var(--ds-fw-semibold);
}
.ds-preset__tagline {
  font-size: var(--ds-fs-small);
  color: var(--ds-fg-3);
}
.ds-field {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-1);
}
.ds-field__label {
  font-size: var(--ds-fs-small);
  color: var(--ds-fg-2);
}
.ds-input {
  font: inherit;
  color: var(--ds-fg-1);
  background: var(--ds-surface-1);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-sm);
  padding: 6px var(--ds-space-3);
}
.ds-textarea {
  resize: vertical;
}
.ds-color {
  inline-size: 40px;
  block-size: 34px;
  padding: 0;
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-sm);
  background: var(--ds-surface-1);
}
.ds-range {
  inline-size: 220px;
}
.ds-btn {
  font: inherit;
  font-size: var(--ds-fs-small);
  color: var(--ds-fg-1);
  background: var(--ds-surface-2);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-sm);
  padding: 7px var(--ds-space-5);
  cursor: pointer;
}
.ds-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
.ds-btn--primary {
  color: var(--ds-surface-1);
  background: var(--ds-color-primary);
  border-color: var(--ds-color-primary);
}
.ds-btn--primary:disabled {
  color: var(--ds-fg-3);
  background: var(--ds-surface-2);
  border-color: var(--ds-border-1);
}
.ds-start__nav {
  display: flex;
  justify-content: space-between;
  gap: var(--ds-space-3);
}
.ds-alert {
  color: var(--ds-danger);
  font-size: var(--ds-fs-small);
  margin: 0;
}
</style>