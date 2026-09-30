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
  createProject,
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
import TokenShowcase from './sections/TokenShowcase.vue'

type SectionKey =
  | 'projects'
  | 'studio'
  | 'color'
  | 'type'
  | 'scale'
  | 'motion'
  | 'themes'
  | 'brand'
  | 'components'
  | 'icons'
  | 'audit'
  | 'export'
  | 'releases'
  | 'showcase'

interface NavItem {
  key: SectionKey
  label: string
  group: string
  /** 需要的后端能力；缺能力时入口置灰并说明原因（不是藏起来装没看见） */
  capability?: string
  skin?: boolean
}

const NAV: NavItem[] = [
  { key: 'projects', label: '项目与生成', group: '建系统' },
  { key: 'studio', label: '令牌工作台', group: '建系统' },
  { key: 'color', label: '色彩实验室', group: '建系统' },
  { key: 'type', label: '排版标度', group: '建系统' },
  { key: 'scale', label: '尺度与密度', group: '建系统' },
  { key: 'motion', label: '阴影与动效', group: '建系统' },
  { key: 'themes', label: '主题实验室', group: '建系统' },
  { key: 'brand', label: '品牌资产', group: '建系统', capability: 'assets' },
  { key: 'icons', label: '图标库', group: '建系统' },
  { key: 'audit', label: '审计与门禁', group: '把质量' },
  { key: 'export', label: '导出交付', group: '把质量', capability: 'export' },
  { key: 'releases', label: '版本与对比', group: '把质量', capability: 'releases' },
  { key: 'components', label: '组件库', group: '看效果', skin: true },
  { key: 'showcase', label: '品牌展示页', group: '看效果', skin: true },
]

const active = ref<SectionKey>('projects')
const navError = ref('')

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

async function newProject(): Promise<void> {
  const name = window.prompt('新项目显示名（如 铸己匣控制台）')?.trim()
  if (!name) return
  const code = window.prompt('项目代码（小写字母/数字/短横线，将作为库内唯一键）', name.slice(0, 12).toLowerCase().replace(/[^a-z0-9-]/g, '-'))?.trim()
  if (!code) return
  const p = await createProject({ code, name })
  if (!p) navError.value = lastError.value
}

async function refreshAll(): Promise<void> {
  navError.value = ''
  await Promise.all([loadMeta(), loadProjects()])
  await loadEffective()
  await loadSkin()
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
        <button class="ds-mini" type="button" @click="newProject">新建</button>
        <button class="ds-mini" type="button" @click="refreshAll">刷新</button>
      </div>
    </header>

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
.ds-chip {
  font: inherit;
  font-size: var(--ds-fs-micro);
  padding: 3px 10px;
  border-radius: var(--ds-radius-pill);
  border: 1px solid var(--ds-border-1);
  background: var(--ds-surface-1);
  color: var(--ds-fg-2);
  cursor: pointer;
}
.ds-chip--on {
  border-color: var(--ds-color-primary);
  color: var(--ds-color-primary);
}
.ds-chip--density {
  border-style: dashed;
}
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
