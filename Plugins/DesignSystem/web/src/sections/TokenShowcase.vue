<script setup lang="ts">
/**
 * 品牌展示页（FR15 / AC16）：把当前项目当**一个真品牌**来展示——一切样式都来自后端有效令牌，
 * 界面不写死任何颜色/字体/文案（v1 的紫色色板、写死字体名、"50–900" 常量已全删）。
 *
 * 数据来源：项目名/标语（currentProject）、主色与色阶 + 语义色（effective）、排版样张（size 与 font 系列令牌）、
 * 圆角与阴影（radius 与 shadow 系列令牌）、logo 与母题（api.listAssets）、字体清单（api.listFonts）、页面清单（api.listScreens）。
 * 全部 :style 绑定后端返回值；svgBody 用 v-html 注入，来自本插件自己的库（非任意用户输入）。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { api, type Asset, type EffectiveToken, type FontFace, type Screen } from '../api'
import { ApiError } from '../http'
import { currentProject, effective, loadEffective, meta, unauthorized } from '../state'
import { colorFamilies, rampSteps } from '../design/derive'
import PanelState from '../components/PanelState.vue'

const assets = ref<Asset[]>([])
const fonts = ref<FontFace[]>([])
const screens = ref<Screen[]>([])
const extraErr = ref('')

const items = computed<EffectiveToken[]>(() => effective.value?.items ?? [])

function byPath(path: string): EffectiveToken | undefined {
  return items.value.find((t) => t.path === path)
}
function colorOf(paths: string[]): string {
  for (const p of paths) {
    const t = byPath(p)
    if (t) return t.colorHex || t.value
  }
  return ''
}

const brandColor = computed(() => colorOf(['semantic.brand', 'color.brand.500']))
const textColor = computed(() => colorOf(['semantic.text-1']))
const text2Color = computed(() => colorOf(['semantic.text-2']))
const surfaceColor = computed(() => colorOf(['semantic.surface-1', 'semantic.surface-bg']))
const borderColor = computed(() => colorOf(['semantic.border-1']))

/** 色阶：每个色族（brand/accent/neutral/…）按后端档位渲染，前端不补中间值 */
const ramps = computed(() =>
  colorFamilies(items.value, meta.value?.colorFamilies ?? []).map((family) => ({ family, steps: rampSteps(items.value, family) })),
)

const semanticSwatches = computed(() =>
  items.value.filter((t) => t.type === 'color' && t.path.startsWith('semantic.')),
)

const sizeTokens = computed(() => items.value.filter((t) => t.path.startsWith('size.') && t.type === 'dimension'))
const fontTokens = computed(() => items.value.filter((t) => t.path.startsWith('font.') && t.type === 'fontFamily'))
const radiusTokens = computed(() => items.value.filter((t) => t.path.startsWith('radius.')))
const shadowTokens = computed(() => items.value.filter((t) => t.path.startsWith('shadow.')))

async function loadExtras(): Promise<void> {
  const project = currentProject.value
  extraErr.value = ''
  if (!project) {
    assets.value = []
    fonts.value = []
    screens.value = []
    return
  }
  try {
    const [a, f, s] = await Promise.all([api.listAssets(project.id), api.listFonts(project.id), api.listScreens(project.id)])
    assets.value = a
    fonts.value = f
    screens.value = s
  } catch (e) {
    extraErr.value = e instanceof ApiError ? `${e.status} ${e.message}` : String(e)
  }
}

async function refresh(): Promise<void> {
  if (!effective.value) await loadEffective()
  await loadExtras()
}

watch(() => currentProject.value?.id, () => void refresh())
onMounted(() => void refresh())
</script>

<template>
  <section class="tk">
    <PanelState v-if="unauthorized" state="unauthorized" />
    <PanelState v-else-if="!currentProject" state="empty" title="还没有选中设计系统项目" hint="先在顶部选择一个设计系统项目，这里会把它当成一个真品牌来展示。" />

    <template v-else>
      <!-- Hero -->
      <div class="tk__hero" :style="{ background: surfaceColor, color: textColor, borderColor }">
        <h1 class="ds-display">{{ currentProject.name }}</h1>
        <p class="ds-body-lg tk__tagline" :style="{ color: text2Color }">
          {{ currentProject.description || currentProject.seedText || '（未设置标语：在项目里填写 description 或 seedText）' }}
        </p>
        <div class="tk__hero-colors">
          <span class="tk__dot" :style="{ background: brandColor }"></span>
          <span class="ds-mono ds-small">主色 {{ brandColor || '—' }}</span>
          <span class="ds-micro">v{{ currentProject.version }} · 令牌 {{ currentProject.tokenCount }}</span>
        </div>
      </div>

      <!-- 颜色：主色 / 色阶 / 语义 -->
      <section class="tk__sec">
        <div class="ds-section-title"><span class="ds-eyebrow">Colors</span><h2 class="ds-h2">主色与色阶</h2></div>
        <PanelState v-if="!items.length" state="empty" title="无令牌" hint="该项目还没有有效令牌，去「项目与生成」页生成。" />
        <div v-else class="ds-stack ds-gap-6">
          <div v-for="r in ramps" :key="r.family" class="tk__ramp">
            <h3 class="ds-h4">color.{{ r.family }} · {{ r.steps.length }} 档</h3>
            <div class="tk__ramp-bar">
              <div
                v-for="s in r.steps"
                :key="s.path"
                class="tk__ramp-step"
                :style="{ background: s.colorHex || s.value }"
                :title="`${s.path} = ${s.colorHex || s.value}`"
              ></div>
            </div>
          </div>

          <div>
            <h3 class="ds-h4">语义色 semantic.*</h3>
            <div class="ds-swatch-grid">
              <div v-for="s in semanticSwatches" :key="s.path" class="tk__swatch">
                <div class="tk__swatch-chip" :style="{ background: s.colorHex || s.value, borderColor }"></div>
                <div class="ds-mono tk__swatch-path">{{ s.path }}</div>
                <div class="ds-micro">{{ s.colorHex || s.value }}</div>
              </div>
            </div>
          </div>
        </div>
      </section>

      <!-- 排版 -->
      <section class="tk__sec">
        <div class="ds-section-title"><span class="ds-eyebrow">Type</span><h2 class="ds-h2">排版样张</h2></div>
        <div v-if="!sizeTokens.length" class="ds-small tk__empty">无 size.* 令牌。</div>
        <div v-else class="ds-stack ds-gap-3">
          <div v-for="t in sizeTokens" :key="t.path" class="tk__type-row" :style="{ borderColor }">
            <span class="tk__type-sample" :style="{ fontSize: t.value }">设计系统 Aa 123</span>
            <span class="ds-mono ds-micro">{{ t.path }} = {{ t.value }}</span>
          </div>
        </div>
        <div v-if="fontTokens.length" class="ds-stack ds-gap-3 tk__fonts">
          <div v-for="t in fontTokens" :key="t.path" class="tk__font-sample">
            <div class="ds-h4" :style="{ fontFamily: t.value }">{{ t.value.split(',')[0].replace(/['"]/g, '') }} 字族样张</div>
            <div class="ds-mono ds-micro">{{ t.path }} = {{ t.value }}</div>
          </div>
        </div>
      </section>

      <!-- 圆角与阴影 -->
      <section class="tk__sec">
        <div class="ds-section-title"><span class="ds-eyebrow">Form</span><h2 class="ds-h2">圆角 · 阴影</h2></div>
        <div class="tk__two-col">
          <div>
            <h3 class="ds-h4">radius.*</h3>
            <div v-if="!radiusTokens.length" class="ds-small tk__empty">无 radius.* 令牌。</div>
            <div v-else class="tk__radius-row">
              <div v-for="t in radiusTokens" :key="t.path" class="tk__radius-box" :style="{ borderRadius: t.value, borderColor, background: surfaceColor }">
                <span class="ds-micro">{{ t.path.replace('radius.', '') }}</span>
                <span class="ds-mono tk__radius-val">{{ t.value }}</span>
              </div>
            </div>
          </div>
          <div>
            <h3 class="ds-h4">shadow.*</h3>
            <div v-if="!shadowTokens.length" class="ds-small tk__empty">无 shadow.* 令牌。</div>
            <div v-else class="tk__shadow-row">
              <div v-for="t in shadowTokens" :key="t.path" class="tk__shadow-box" :style="{ boxShadow: t.value, background: surfaceColor }">
                <span class="ds-micro">{{ t.path }}</span>
              </div>
            </div>
          </div>
        </div>
      </section>

      <!-- Logo / 母题资产 -->
      <section class="tk__sec">
        <div class="ds-section-title"><span class="ds-eyebrow">Assets</span><h2 class="ds-h2">Logo 与母题</h2></div>
        <p v-if="extraErr" class="tk__err" role="alert">{{ extraErr }}</p>
        <PanelState v-else-if="!assets.length" state="empty" title="无资产" hint="资产（logo/母题）为空；后端 listAssets 未返回内容。" />
        <div v-else class="ds-swatch-grid">
          <div v-for="a in assets" :key="a.id" class="tk__asset ds-surface-2">
            <div class="tk__asset-glyph" :style="{ color: brandColor }">
              <svg :viewBox="'0 0 24 24'" role="img" :aria-label="a.name || a.code" v-html="a.svgBody || ''"></svg>
            </div>
            <div class="ds-mono tk__asset-code">{{ a.code }}</div>
            <div class="ds-micro">{{ a.kind }}<span v-if="a.license"> · {{ a.license }}</span></div>
          </div>
        </div>
      </section>

      <!-- 字体清单 -->
      <section class="tk__sec">
        <div class="ds-section-title"><span class="ds-eyebrow">Fonts</span><h2 class="ds-h2">字体清单</h2></div>
        <PanelState v-if="!fonts.length" state="empty" title="无字体登记" hint="listFonts 未返回内容。" />
        <table v-else class="tk__table">
          <thead><tr><th>family</th><th>weight</th><th>style</th><th>role</th><th>license</th></tr></thead>
          <tbody>
            <tr v-for="f in fonts" :key="f.id">
              <td :style="{ fontFamily: f.family }">{{ f.family }}</td>
              <td class="ds-num">{{ f.weight }}</td>
              <td class="ds-micro">{{ f.style }}</td>
              <td class="ds-small">{{ f.role || '—' }}</td>
              <td class="ds-small">{{ f.license || '未标注' }}</td>
            </tr>
          </tbody>
        </table>
      </section>

      <!-- 页面清单 -->
      <section class="tk__sec">
        <div class="ds-section-title"><span class="ds-eyebrow">Screens</span><h2 class="ds-h2">页面清单</h2></div>
        <PanelState v-if="!screens.length" state="empty" title="无页面清单" hint="listScreens 未返回内容。" />
        <table v-else class="tk__table">
          <thead><tr><th>code</th><th>title</th><th>route</th><th>icon</th></tr></thead>
          <tbody>
            <tr v-for="s in screens" :key="s.id">
              <td class="ds-mono">{{ s.code }}</td>
              <td>{{ s.title || '—' }}</td>
              <td class="ds-mono ds-small">{{ s.route || '—' }}</td>
              <td class="ds-small">{{ s.iconCode || '—' }}</td>
            </tr>
          </tbody>
        </table>
      </section>
    </template>
  </section>
</template>

<style scoped>
.tk {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-6);
}
.tk__hero {
  border: 1px solid;
  border-radius: var(--ds-radius-xl);
  padding: var(--ds-space-8);
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-3);
}
.tk__tagline {
  max-width: 620px;
}
.tk__hero-colors {
  display: flex;
  align-items: center;
  gap: var(--ds-space-3);
}
.tk__dot {
  width: 20px;
  height: 20px;
  border-radius: var(--ds-radius-pill);
  border: 1px solid var(--ds-border-2);
}
.tk__sec {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-4);
}
.tk__ramp-bar {
  display: flex;
  border-radius: var(--ds-radius-md);
  overflow: hidden;
  border: 1px solid var(--ds-border-1);
}
.tk__ramp-step {
  flex: 1;
  min-width: 24px;
  height: 48px;
}
.tk__swatch {
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.tk__swatch-chip {
  width: 100%;
  height: 56px;
  border-radius: var(--ds-radius-md);
  border: 1px solid;
}
.tk__swatch-path {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.tk__type-row {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--ds-space-4);
  border-bottom: 1px solid;
  padding-bottom: var(--ds-space-2);
}
.tk__type-sample {
  color: inherit;
}
.tk__fonts {
  margin-top: var(--ds-space-4);
}
.tk__radius-row,
.tk__shadow-row {
  display: flex;
  flex-wrap: wrap;
  gap: var(--ds-space-4);
}
.tk__radius-box,
.tk__shadow-box {
  width: 96px;
  height: 72px;
  border: 1px solid;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 2px;
}
.tk__radius-val {
  color: var(--ds-fg-4);
  font-size: var(--ds-fs-micro);
}
.tk__two-col {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  gap: var(--ds-space-6);
}
.tk__asset {
  padding: var(--ds-space-4);
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--ds-space-2);
}
.tk__asset-glyph svg {
  width: 40px;
  height: 40px;
}
.tk__asset-code {
  color: var(--ds-fg-2);
}
.tk__table {
  width: 100%;
  border-collapse: collapse;
  font-size: var(--ds-fs-small);
}
.tk__table th {
  text-align: left;
  color: var(--ds-fg-3);
  border-bottom: 1px solid var(--ds-border-1);
  padding: 6px var(--ds-space-2);
}
.tk__table td {
  border-bottom: 1px solid var(--ds-border-1);
  padding: 6px var(--ds-space-2);
}
.tk__empty {
  color: var(--ds-fg-4);
}
.tk__err {
  color: var(--ds-danger);
  font-size: var(--ds-fs-small);
}
</style>
