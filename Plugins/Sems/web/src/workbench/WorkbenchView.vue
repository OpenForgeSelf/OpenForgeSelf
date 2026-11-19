<script setup lang="ts">
/**
 * sems 项目工作台 · 应用壳
 *
 * 承接交接包 project-workbench.html 的信息架构：
 *   顶栏（Projects / Create / Import 三态 Tab，向导时切四步步骤条）
 *   + 管理器区（S01–S05） / 项目外壳（侧栏 5+1 项，S06–S11）
 *   + 深色状态栏（路径 / 版本 / 模式 / 实时时间）+ Toast。
 *
 * 「+1」是 sems 扩展的「规格」页，用来保留原插件的规格/验收覆盖能力；
 * 其余 5 项与交接包 S06–S10 一一对应，只是数据源换成真实 sems API。
 */
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { ICON_SPRITE, ic } from './icons'
import {
  store, go, toast, currentProject, isProjectRoute, routeTab, syncFromHash,
  disposeWorkbench, projectConfigs,
} from './workbenchStore'
import ManagerProjects from './views/ManagerProjects.vue'
import ManagerBrowser from './views/ManagerBrowser.vue'
import ManagerWizard from './views/ManagerWizard.vue'
import ProjectDashboard from './views/ProjectDashboard.vue'
import ProjectPluginsView from './views/ProjectPluginsView.vue'
import ProjectConfigurationView from './views/ProjectConfigurationView.vue'
import ProjectTasksView from './views/ProjectTasksView.vue'
import ProjectSpecsView from './views/ProjectSpecsView.vue'

const TABS = [
  { key: 'projects', label: '项目', icon: 'list', route: '/projects' },
  { key: 'create', label: '登记', icon: 'plus', route: '/create' },
  { key: 'import', label: '导入', icon: 'download', route: '/import' },
] as const

const NAV = [
  { path: '/dashboard', label: '概览', icon: 'grid' },
  { path: '/runs', label: '运行', icon: 'tasks' },
  { path: '/specs', label: '规格', icon: 'clip' },
  { path: '/plugins', label: '插件', icon: 'puzzle' },
  { path: '/settings', label: '设置', icon: 'sliders' },
] as const

/** AC-D1：2 步登记向导（与 ManagerWizard.vue 的两屏一一对应）。 */
const WIZARD_STEPS = ['确认目录', '填写档案']

const now = ref('')
let timer: ReturnType<typeof setInterval> | undefined

const tab = computed(() => routeTab())
const isWizard = computed(() => store.route === '/create/wizard')
const inProject = computed(() => isProjectRoute())
const p = computed(() => currentProject())
/** 显示偏好：状态栏路径标记由 S09 开关控制，切换即时生效（修 D-2）。 */
const showPathBadge = computed(() => projectConfigs(p.value).showPathBadge !== false)

function tick() {
  now.value = new Date().toLocaleString('zh-CN', { hour12: false })
}

function goTab(key: string) {
  const t = TABS.find((x) => x.key === key)
  if (t) go(t.route as typeof store.route)
}

function stepClass(i: number) {
  return i === store.wizard.step ? 'current' : i < store.wizard.step ? 'done' : ''
}

function onAbout() {
  toast('SEMS 项目工作台 · 管项目、跑命令、看执行、记规格、看项目插件')
}

function closeMoreOnOutside(e: MouseEvent) {
  if (!store.moreOpen) return
  const target = e.target as HTMLElement | null
  if (target && !target.closest('.more-wrap')) store.moreOpen = false
}

onMounted(() => {
  tick()
  timer = setInterval(tick, 1000)
  syncFromHash()
  window.addEventListener('hashchange', syncFromHash)
  document.addEventListener('click', closeMoreOnOutside)
})
onUnmounted(() => {
  if (timer) clearInterval(timer)
  disposeWorkbench()
  window.removeEventListener('hashchange', syncFromHash)
  document.removeEventListener('click', closeMoreOnOutside)
})
</script>

<template>
  <div class="wb">
    <!-- 自绘内联 SVG 精灵（只挂一次，clean-room 无外部资源） -->
    <div v-html="ICON_SPRITE"></div>

    <div class="wb__app">
      <!-- ============ 管理器区：S01–S05 ============ -->
      <template v-if="!inProject">
        <header class="topbar">
          <div class="corner left">
            <button v-if="tab !== 'projects'" data-action="go" aria-label="返回项目" @click="go('/projects')">
              <span v-html="ic('back')"></span>
            </button>
          </div>
          <div class="wb-brandline">
            <span class="wb-eyebrow">SEMS</span>
            <h1>{{ isWizard ? '登记项目' : '项目工作台' }}</h1>
            <span v-if="store.version" class="wb-version">v{{ store.version }}</span>
          </div>

          <div v-if="isWizard" class="steps">
            <span v-for="(s, i) in WIZARD_STEPS" :key="s" class="step" :class="stepClass(i)">
              <span class="num">{{ i < store.wizard.step ? '✓' : i + 1 }}</span>{{ s }}
            </span>
          </div>
          <div v-else class="topbar-nav">
            <a v-for="t in TABS" :key="t.key" class="tab" :class="{ selected: tab === t.key }"
               :href="'#/' + t.key" @click.prevent="goTab(t.key)">
              <span v-html="ic(t.icon, 'sm')"></span>{{ t.label }}
            </a>
          </div>

          <div class="corner right">
            <button aria-label="帮助" @click="onAbout"><span v-html="ic('help')"></span></button>
          </div>
        </header>

        <ManagerProjects v-if="store.route === '/projects'" />
        <ManagerBrowser v-else-if="store.route === '/create' || store.route === '/import'"
                        :mode="store.route === '/create' ? 'create' : 'import'" />
        <ManagerWizard v-else-if="store.route === '/create/wizard'" />
      </template>

      <!-- ============ 项目外壳：S06–S11 ============ -->
      <template v-else>
        <div v-if="!p" class="manager-body">
          <div class="empty-state">
            <span v-html="ic('info', 'lg')"></span>
            <div>请先打开一个项目</div>
            <a class="btn btn-primary" href="#/projects" @click.prevent="go('/projects')">返回项目列表</a>
          </div>
        </div>
        <div v-else class="project-shell">
          <aside class="sidebar">
            <button class="project-select" @click="go('/projects')">
              {{ p.name }}<span v-html="ic('chevron', 'sm')"></span>
            </button>
            <nav class="nav-list">
              <a v-for="n in NAV" :key="n.path" class="nav-item"
                 :class="{ selected: store.route === n.path }"
                 :href="'#' + n.path" :aria-label="n.label"
                 @click.prevent="go(n.path as typeof store.route)">
                <span v-html="ic(n.icon)"></span><span>{{ n.label }}</span>
              </a>
            </nav>
            <div class="sidebar-spacer"></div>
            <div class="sidebar-foot">
              <div class="more-wrap">
                <div v-if="store.moreOpen" class="more-menu">
                  <button @click="go('/projects')">项目工作台</button>
                  <button @click="onAbout(); store.moreOpen = false">关于</button>
                </div>
                <button class="btn-icon" aria-label="更多" @click.stop="store.moreOpen = !store.moreOpen">
                  <span v-html="ic('more')"></span>
                </button>
              </div>
            </div>
          </aside>

          <section class="project-main">
            <ProjectDashboard v-if="store.route === '/dashboard'" />
            <ProjectTasksView v-else-if="store.route === '/runs'" />
            <ProjectSpecsView v-else-if="store.route === '/specs'" />
            <ProjectPluginsView v-else-if="store.route === '/plugins'" />
            <ProjectConfigurationView v-else-if="store.route === '/settings'" />
          </section>
        </div>
      </template>
    </div>

    <!-- ============ 深色状态栏（FR-21） ============ -->
    <div class="statusbar wb__statusbar" id="statusbar">
      <div class="status-left">
        <button class="btn-icon" style="color:var(--el-text-color-primary);width:18px;height:18px"
                aria-label="项目工作台" @click="go('/projects')"><span v-html="ic('home', 'sm')"></span></button>
        <button class="crumb" style="color:var(--el-text-color-primary);font-size:12px;padding:0"
                aria-label="项目目录" @click="go('/projects')"><span v-html="ic('folder', 'sm')"></span></button>
        <span v-if="showPathBadge" class="status-path">{{ p ? p.path : '/workspace' }}</span>
        <button class="btn-icon" style="color:var(--el-text-color-primary);width:18px;height:18px"
                aria-label="运行日志" @click="go('/runs')"><span v-html="ic('terminal', 'sm')"></span></button>
        <span class="wb-version-text">Sems 工作台 {{ store.version ? 'v' + store.version : '' }}</span>
        <span v-if="store.runningCount > 0" class="wb-badge info">运行中 {{ store.runningCount }}</span>
        <span v-if="store.error" class="wb-badge warn" :title="store.error">加载失败</span>
      </div>
      <div class="status-right">
        <span id="status-time">{{ now }}</span>
      </div>
    </div>

    <!-- ============ Toast ============ -->
    <div class="wb__toast-host" id="toast-host">
      <div v-for="t in store.toasts" :key="t.id" class="toast">{{ t.text }}</div>
    </div>
  </div>
</template>
