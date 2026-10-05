<script setup lang="ts">
/**
 * 设计系统插件根视图（v2：库驱动工作台）。
 *
 * 与 v1 的根本差别：v1 的数据在 localStorage 里、"生成/导出"是前端两套自写实现；
 * v2 一律以后端设计系统库为唯一真相 —— 界面读的是 `tokens/effective`，
 * 预览换肤用的是后端 `export?format=css` 的产物（预览与交付同源）。
 *
 * 外壳读的是中性变量（styles/tokens.css 由 gen-tokens-css.ts 生成），
 * 预览容器 `.ds-skin` 内再套一层"用户系统"，所以外壳永远不被用户品牌污染。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { buildSkinStyles, definedVars } from './design/skin'
import {
  currentProject,
  lastError,
  loadEffective,
  loadMeta,
  loadProjects,
  loadSkin,
  meta,
  projects,
  projectsState,
  resolveCssVar,
  selectProject,
  setTheme,
  skinApplied,
  skinCss,
  skinTheme,
  supports,
  themeCode,
  themes,
  unauthorized,
} from './state'
import PanelState from './components/PanelState.vue'
import ModeBar from './shell/ModeBar.vue'
import { NAV, isSectionKey, type NavItem, type SectionKey } from './shell/nav'
import { readStoredMode, resolveInitialMode, storeMode, type Mode } from './shell/mode'
import { formatHash, parseHash, type RouteState } from './design/route'
import { proTerms, toggleProTerms } from './design/glossary'
import StartMode from './start/StartMode.vue'
import Showroom from './showroom/Showroom.vue'
import DeliveryMode from './delivery/DeliveryMode.vue'

import Projects from './sections/Projects.vue'
import TokenStudio from './sections/TokenStudio.vue'
import ColorLab from './sections/ColorLab.vue'
import TypeScale from './sections/TypeScale.vue'
import DensityScales from './sections/DensityScales.vue'
import ShadowMotion from './sections/ShadowMotion.vue'
import ThemeLab from './sections/ThemeLab.vue'
import BrandAssets from './sections/BrandAssets.vue'
import ComponentGallery from './sections/ComponentGallery.vue'
import IconLibrary from './sections/IconLibrary.vue'
import AuditBoard from './sections/AuditBoard.vue'
import ExportCenter from './sections/ExportCenter.vue'
import ReleaseBoard from './sections/ReleaseBoard.vue'
import Guidelines from './sections/Guidelines.vue'
import TokenShowcase from './sections/TokenShowcase.vue'

/** 初始深链：只在挂载时解析一次（写回用 replaceState，不监听 back/forward，故无需响应 hashchange） */
const initialRoute = parseHash(location.hash)

/** 工作台栏目：深链给出合法栏目时用它，否则回落「项目」 */
const active = ref<SectionKey>(
  initialRoute?.mode === 'workbench' && initialRoute.sub && isSectionKey(initialRoute.sub) ? initialRoute.sub : 'projects',
)
const navError = ref('')

/**
 * 四模式外壳（FR2）：模式条切换 + 写回 `ds.mode`；「新建」切到开始模式。
 * 初始判定在项目加载前只能假设"无项目"（projects 是异步的），加载完且用户未手动切过时重判一次。
 */
const mode = ref<Mode>(resolveInitialMode({ hash: location.hash, hasProjects: false, stored: readStoredMode() }))
const userPicked = ref(false)
/** 顶栏「新建」递增它，强制 StartMode 重新挂载（向导状态重置），见步骤6 */
const wizardKey = ref(0)
function pickMode(m: Mode): void {
  userPicked.value = true
  mode.value = m
  storeMode(m)
}

/* ---------------- 深链（FR15/AC22）：初始还原 + replaceState 写回 ---------------- */

/** 展厅深链只在"初始就落在展厅"时下发一次（之后手动切到展厅不复活旧 URL 选择） */
const showroomInitial: RouteState | null = mode.value === 'showroom' ? initialRoute : null

/** 展厅舞台当前选择（由 Showroom 的 `route` 事件回填），供写回哈希 */
const showroomRoute = ref<{ outfit: string; theme: string; device: string; page: string } | null>(null)

/**
 * 依当前模式 / 工作台栏目 / 展厅选择拼出哈希并用 `replaceState` 写回。
 * 传入以 `#` 开头的相对 URL，浏览器只替换片段、保留宿主 history 模式的路径。
 */
function writeHash(): void {
  const state: RouteState = { mode: mode.value }
  if (mode.value === 'workbench') {
    state.sub = active.value
  } else if (mode.value === 'showroom' && showroomRoute.value) {
    state.sub = showroomRoute.value.page
    state.outfit = showroomRoute.value.outfit
    state.theme = showroomRoute.value.theme
    state.device = showroomRoute.value.device
  }
  const next = formatHash(state)
  if (location.hash !== next) history.replaceState(null, '', next)
}

function onShowroomRoute(payload: { outfit: string; theme: string; device: string; page: string }): void {
  showroomRoute.value = payload
  writeHash()
}

// 模式切换 / 工作台换栏目 → 写回哈希（展厅选择另经 `onShowroomRoute` 写回）
watch([mode, active], () => writeHash())

const colorThemes = computed(() => themes.value.filter((t) => t.modeKind === 'color'))
const densityThemes = computed(() => themes.value.filter((t) => t.modeKind === 'density'))
const currentNav = computed(() => NAV.find((n) => n.key === active.value))

/** 导航分组在脚本里算好：模板里写 filter/map 回调会让 vue-tsc 推不出参数类型（隐式 any） */
const groups = computed(() => {
  const order: string[] = []
  const map = new Map<string, (NavItem & { enabled: boolean })[]>()
  for (const n of NAV) {
    if (!order.includes(n.group)) order.push(n.group)
    const list = map.get(n.group) ?? []
    list.push({ ...n, enabled: !n.capability || supports(n.capability) })
    map.set(n.group, list)
  }
  return order.map((group) => ({ group, items: map.get(group) ?? [] }))
})

/** 换肤两段样式：① 后端 CSS 收窄到 .ds-skin ② 外壳变量重键（只引用 ① 里真存在的变量） */
const skinStyles = computed(() => buildSkinStyles(skinCss.value, definedVars(skinCss.value)))

/**
 * 投影状态必须说得出口：`skinTheme` 是**已注入投影所属的档**，`themeCode` 是**界面选中的档**，
 * 两者可以不一致（投影只在预览页注入，在别的页切档不会重取，本次会话没进过预览页时更是压根没取过）。
 * 历史故障：同一步骤截图两次运行一暗一亮 —— 因为"画布是哪一档"从来没人显示，也没人断言。
 * （v2.6.8 的 e2e 又抓到一条：新建第一个项目后从没进过预览页，那时投影是「未取」而不是「待重取」，
 * 文案把两种情况混成一句就等于说谎。）
 */
const skinState = computed<'applied' | 'pending' | 'unloaded' | 'unavailable'>(() => {
  if (skinApplied.value) return 'applied'
  if (skinTheme.value) return 'pending'
  return currentProject.value && supports('export') ? 'unloaded' : 'unavailable'
})
const skinStateText = computed(() => {
  const onSkinPage = currentNav.value?.skin === true
  switch (skinState.value) {
    case 'applied':
      return onSkinPage ? `画布投影 ${skinTheme.value}` : `画布投影 ${skinTheme.value}（本页是中性外壳，投影只作用于「看效果」页画布）`
    case 'pending':
      return onSkinPage
        ? `正在重取画布投影 ${skinTheme.value} → ${themeCode.value}`
        : `画布投影待重取 ${skinTheme.value} → ${themeCode.value}（进入「看效果」页时应用）`
    case 'unloaded':
      return onSkinPage ? `正在取画布投影 ${themeCode.value}` : `画布投影未取（进入「看效果」页时按 ${themeCode.value} 取）`
    default:
      return '画布投影不可用（没有项目，或后端未声明导出能力）'
  }
})
/** 角标显示的是投影文本里的真值（别名顺到字面值），不是界面另算的色 */
const skinSurfaceBg = computed(() => resolveCssVar(skinCss.value, 'semantic.surface-bg'))
const skinBrand = computed(() => resolveCssVar(skinCss.value, 'semantic.brand'))

async function pickTheme(code: string): Promise<void> {
  await setTheme(code)
}

async function pickProject(id: number): Promise<void> {
  const p = projects.value.find((x) => x.id === id)
  if (p) await selectProject(p)
}

/** 顶栏「新建」：切到开始模式并重置向导（FR3：不再弹原生对话框） */
function newProject(): void {
  wizardKey.value++
  pickMode('start')
}

/** 向导创建成功后：重载项目列表并选中新项目，让展厅/工作台立刻用上它 */
async function onCreated(code: string): Promise<void> {
  await loadProjects()
  const p = projects.value.find((x) => x.code === code)
  if (p) await selectProject(p)
}

async function refreshAll(): Promise<void> {
  navError.value = ''
  await Promise.all([loadMeta(), loadProjects()])
  await loadEffective()
  // 投影只在预览页（nav.skin）才取：非预览页刷新不得污染「未取」语义（否则在别的页切档，
  // 主题条会把"从没进过预览页"说成"待重取"——e2e 对 data-skin-state 的断言钉的就是这个前提）
  if (currentNav.value?.skin) await loadSkin()
  // 项目加载完成后重判默认模式：用户没手动切过才改（FR2 的"无项目→start"是按真实项目数算的）
  if (!userPicked.value) {
    mode.value = resolveInitialMode({
      hash: location.hash,
      hasProjects: projects.value.some((p) => p.status !== 'archived'),
      stored: readStoredMode(),
    })
  }
}

watch(active, (key) => {
  const item = NAV.find((n) => n.key === key)
  if (item?.capability && !supports(item.capability)) return
  if (item?.skin) void loadSkin()
})

watch([themeCode, () => currentProject.value?.id], () => {
  if (currentNav.value?.skin) void loadSkin()
})

onMounted(() => {
  void refreshAll()
})
</script>

<template>
  <div class="ds ds-root">
    <header class="ds-header">
      <div class="ds-header__titles">
        <div class="ds-h3">
          设计系统
          <span v-if="meta" class="ds-badge">模型 {{ meta.modelVersion }} · 生成器 {{ meta.generatorVersion }} · 投影 {{ meta.projectionVersion }}</span>
        </div>
        <div class="ds-small">Design System · 库驱动的令牌/主题/审计/交付工作台</div>
      </div>

      <div class="ds-header__pickers">
        <label class="ds-picker">
          <span class="ds-micro">项目</span>
          <select class="ds-select" :value="currentProject?.id ?? 0" @change="pickProject(Number(($event.target as HTMLSelectElement).value))">
            <option v-if="!projects.length" :value="0">（还没有项目）</option>
            <option v-for="p in projects" :key="p.id" :value="p.id">{{ p.name }} <span v-if="p.version">v{{ p.version }}</span></option>
          </select>
        </label>
        <button
          v-if="mode !== 'workbench'"
          class="ds-mini"
          type="button"
          :aria-pressed="proTerms"
          :title="proTerms ? '切换为大白话文案' : '切换为专业术语文案'"
          @click="toggleProTerms"
        >
          {{ proTerms ? '大白话' : '专业术语' }}
        </button>
        <button class="ds-mini" type="button" @click="newProject">新建</button>
        <button class="ds-mini" type="button" @click="refreshAll">刷新</button>
      </div>
    </header>

    <!-- 必须显式走 pickMode：v-model 只改 mode.value，userPicked 不置位会导致 refreshAll 重判把工作台弹回 -->
    <ModeBar :model-value="mode" @update:model-value="pickMode" />

    <template v-if="mode === 'workbench'">
    <nav class="ds-nav">
      <template v-for="g in groups" :key="g.group">
        <span class="ds-nav__group">{{ g.group }}</span>
        <button
          v-for="n in g.items"
          :key="n.key"
          class="ds-nav__item"
          :class="{ 'ds-nav__item--active': active === n.key, 'ds-nav__item--off': !n.enabled }"
          :title="n.enabled ? '' : `后端未声明能力 ${n.capability}，该入口暂不可用`"
          :disabled="!n.enabled"
          @click="active = n.key"
        >
          {{ n.label }}
        </button>
      </template>
    </nav>

    <div v-if="colorThemes.length || densityThemes.length" class="ds-themebar">
      <span class="ds-micro">配色主题</span>
      <button
        v-for="t in colorThemes"
        :key="t.code"
        class="ds-chip"
        :class="{ 'ds-chip--on': themeCode === t.code }"
        @click="pickTheme(t.code)"
      >
        {{ t.name || t.code }}
      </button>
      <span v-if="densityThemes.length" class="ds-micro">密度</span>
      <button
        v-for="t in densityThemes"
        :key="t.code"
        class="ds-chip ds-chip--density"
        :class="{ 'ds-chip--on': themeCode === t.code }"
        @click="pickTheme(t.code)"
      >
        {{ t.name || t.code }}
      </button>
      <span class="ds-skinstate ds-small" :data-skin-state="skinState" :data-skin-theme="skinTheme">{{ skinStateText }}</span>
    </div>

    <div v-if="unauthorized" class="ds-warn"><PanelState state="unauthorized" /></div>
    <p v-else-if="projectsState === 'error' || navError" class="ds-warn" role="alert">{{ navError || lastError }}</p>

    <main class="ds-main">
      <Projects v-if="active === 'projects'" />
      <TokenStudio v-else-if="active === 'studio'" />
      <ColorLab v-else-if="active === 'color'" />
      <TypeScale v-else-if="active === 'type'" />
      <DensityScales v-else-if="active === 'scale'" />
      <ShadowMotion v-else-if="active === 'motion'" />
      <ThemeLab v-else-if="active === 'themes'" />
      <BrandAssets v-else-if="active === 'brand'" />
      <IconLibrary v-else-if="active === 'icons'" />
      <AuditBoard v-else-if="active === 'audit'" />
      <ExportCenter v-else-if="active === 'export'" />
      <ReleaseBoard v-else-if="active === 'releases'" />
      <Guidelines v-else-if="active === 'guidelines'" />
      <div v-else class="ds-skin" :data-skin-theme="skinTheme">
        <component :is="'style'">{{ skinStyles.scoped }}</component>
        <component :is="'style'">{{ skinStyles.alias }}</component>
        <div class="ds-skin__meta">
          <span class="ds-mono">画布投影 {{ skinTheme || '未注入' }}</span>
          <span class="ds-mono">surface-bg {{ skinSurfaceBg || '—' }}</span>
          <span class="ds-mono">brand {{ skinBrand || '—' }}</span>
          <span class="ds-skin__meta-hint">值取自后端导出投影（export?format=css，别名已顺到字面值），与交付文件同源</span>
        </div>
        <ComponentGallery v-if="active === 'components'" />
        <TokenShowcase v-else />
      </div>
    </main>
    </template>

    <StartMode v-else-if="mode === 'start'" :key="wizardKey" @created="onCreated" @go="pickMode" />

    <Showroom v-else-if="mode === 'showroom'" :initial="showroomInitial" @created="onCreated" @route="onShowroomRoute" />

    <DeliveryMode v-else />

    <footer class="ds-footer ds-small">
      设计系统插件（design-system）· 数据源：宿主后端设计系统库 · 当前主题
      <strong>{{ themeCode }}</strong>
    </footer>
  </div>
</template>

<style scoped>
.ds-root {
  min-height: 100vh;
  display: flex;
  flex-direction: column;
}
.ds-header {
  position: sticky;
  top: 0;
  z-index: 10;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--ds-space-5);
  flex-wrap: wrap;
  padding: var(--ds-space-4) var(--ds-space-6);
  background: color-mix(in oklab, var(--ds-surface-1) 88%, transparent);
  backdrop-filter: blur(12px);
  border-bottom: 1px solid var(--ds-border-1);
}
.ds-header__titles {
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.ds-badge {
  margin-left: var(--ds-space-3);
  padding: 1px 8px;
  border-radius: var(--ds-radius-pill);
  background: var(--ds-surface-2);
  border: 1px solid var(--ds-border-1);
  font-size: var(--ds-fs-micro);
  color: var(--ds-fg-3);
  vertical-align: middle;
}
.ds-header__pickers {
  display: flex;
  align-items: flex-end;
  gap: var(--ds-space-2);
}
.ds-picker {
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.ds-select {
  font: inherit;
  font-size: var(--ds-fs-small);
  color: var(--ds-fg-1);
  background: var(--ds-surface-1);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-sm);
  padding: 5px var(--ds-space-3);
  min-width: 18ch;
}
.ds-mini {
  font: inherit;
  font-size: var(--ds-fs-micro);
  color: var(--ds-fg-2);
  background: var(--ds-surface-2);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-sm);
  padding: 5px 10px;
  cursor: pointer;
}
.ds-nav {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 4px var(--ds-space-1);
  padding: var(--ds-space-3) var(--ds-space-6);
  border-bottom: 1px solid var(--ds-border-1);
  background: var(--ds-surface-2);
}
.ds-nav__group {
  font-size: var(--ds-fs-micro);
  color: var(--ds-fg-3);
  margin: 0 var(--ds-space-2) 0 var(--ds-space-1);
}
.ds-nav__item {
  font: inherit;
  font-size: var(--ds-fs-small);
  border: 1px solid transparent;
  background: transparent;
  color: var(--ds-fg-2);
  border-radius: var(--ds-radius-pill);
  padding: 6px var(--ds-space-4);
  cursor: pointer;
}
.ds-nav__item:hover {
  color: var(--ds-fg-1);
}
.ds-nav__item--active {
  background: var(--ds-surface-1);
  color: var(--ds-color-primary);
  border-color: var(--ds-border-1);
  box-shadow: var(--ds-shadow-sm);
}
.ds-nav__item--off {
  opacity: 0.45;
  cursor: not-allowed;
}
.ds-themebar {
  display: flex;
  align-items: center;
  gap: var(--ds-space-2);
  flex-wrap: wrap;
  padding: var(--ds-space-2) var(--ds-space-6) 0;
}
/* .ds-chip / .ds-mode-pane 已上移到 styles/base.css（外壳共享词汇表，跨模式组件复用） */
.ds-warn {
  margin: var(--ds-space-3) var(--ds-space-6) 0;
  color: var(--ds-danger);
}
.ds-main {
  flex: 1;
  padding: var(--ds-space-6);
  max-width: 1240px;
  width: 100%;
  margin: 0 auto;
}
/* 换肤容器：仅此层套用户系统变量，外壳 header/nav 保持中性 */
.ds-skin {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-5);
  border-radius: var(--ds-radius-lg);
  padding: var(--ds-space-5);
  background: var(--ds-bg, var(--ds-surface-1));
  color: var(--ds-fg-1);
}
/* 画布自证：显示"这份投影是哪个主题、底色/品牌色取自投影文本的真值"，截图也读得出明暗属于哪一档 */
.ds-skin__meta {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--ds-space-3);
  padding: var(--ds-space-3) var(--ds-space-4);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-md);
  background: var(--ds-surface-2);
  font-size: var(--ds-fs-small);
}
.ds-skin__meta-hint {
  color: var(--ds-fg-3);
}
.ds-skinstate {
  color: var(--ds-fg-3);
  white-space: nowrap;
}
.ds-skinstate[data-skin-state='pending'],
.ds-skinstate[data-skin-state='unloaded'] {
  color: var(--ds-warning);
}
.ds-skinstate[data-skin-state='unavailable'] {
  color: var(--ds-danger);
}
.ds-footer {
  padding: var(--ds-space-5) var(--ds-space-6);
  text-align: center;
  color: var(--ds-fg-4);
  border-top: 1px solid var(--ds-border-1);
}
</style>
