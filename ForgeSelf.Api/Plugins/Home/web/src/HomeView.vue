<script setup lang="ts">
import { computed, inject, onMounted, onUnmounted, ref } from 'vue'
import { useRouter, type RouteLocationRaw } from 'vue-router'
import { useHomeStore } from './homeStore'
import { useUsageStatsStore } from './usageStats'
import { mergeFeatureList } from './features'
import {
  HOME_ENTRY_LIMIT,
  buildHomeEntries,
  rankEntriesDetailed,
  type HomeEntry,
  type UsageSnapshot,
} from './homeEntries'
import { lucideIconSvg } from './featureIcons'
import type { TodoCreateRequest } from './types'

// 导航桥（四级降级，契约优先、兜底兜住）：
//   ① inject('forgeOpenPage')      —— 宿主组件级契约主路（唯一保证开 tab + 记 usage 的通道）
//   ② window.__FORGE_OPEN_PAGE__   —— 跨实例硬兜底（宿主与插件唯一确定共享的全局）
//   ③ useRouter().push             —— 插件自带 router（仅 URL 跳转，不记 tab/usage）
//   ④ location.assign              —— 最后保底（整页跳转，必然可用）
//
// 为何要这么多级：inject 依赖「宿主与插件共享同一份 vue 实例」（provide 表住在 vue 模块作用域内），
// 该前提被破坏时 inject 会**静默返回 undefined**，且报错发生在点击那一刻，与「宿主没 provide」无法区分。
// 全仓无 router.afterEach，tab 栏与 usage 统计仅由宿主 useOpenPage().openPage 触发，
// 故 ① 是唯一完整语义的通道；②③④ 保证即便桥断，导航功能仍不失效（降级为纯跳转）。
const router = useRouter()

/** 判定一个候选桥函数是否可用（防注入到空值/非函数）。 */
function isBridge(fn: unknown): fn is (to: RouteLocationRaw, label?: string) => void {
  return typeof fn === 'function'
}

/** ① 宿主组件级桥（provide）。 */
const injectedBridge = inject<unknown>('forgeOpenPage', null)

/** 实际生效的导航实现 + 通道名（通道名用于诊断，也便于后续按需切主路）。 */
function resolveOpenPage(): {
  fn: (to: RouteLocationRaw, label?: string) => void
  channel: string
} {
  if (isBridge(injectedBridge)) return { fn: injectedBridge, channel: 'inject' }

  const globalBridge = typeof window !== 'undefined' ? window.__FORGE_OPEN_PAGE__ : undefined
  if (isBridge(globalBridge)) return { fn: globalBridge as never, channel: 'window' }

  // ② 插件自带 router：仅做 URL 跳转（不注册 tab / 不记 usage），故排在宿主桥之后。
  if (router && typeof router.push === 'function') {
    return { fn: (to) => router.push(to as never), channel: 'router' }
  }

  // ③ 最后保底：整页跳转。router 都取不到时仍保证用户点得动。
  return {
    fn: (to) => {
      const path = typeof to === 'string' ? to : (to.path ?? '/')
      if (typeof window !== 'undefined') window.location.assign(path)
    },
    channel: 'location',
  }
}

const _resolved = resolveOpenPage()
// 开发期把生效通道暴露到控制台，避免「桥静默降级」再次成为难查的悬案。
console.debug('[Home] 导航桥通道:', _resolved.channel)
const openPage = _resolved.fn

const homeStore = useHomeStore()
const usageStore = useUsageStatsStore()

// 时段问候语
const greeting = computed(() => {
  const h = new Date().getHours()
  if (h < 12) return '早安，锻造师'
  if (h < 18) return '下午好，锻造师'
  return '晚上好，锻造师'
})

// 快速提问输入
const quickAskText = ref('')

function submitQuickAsk() {
  if (!quickAskText.value.trim()) return
  openPage({ path: '/ai-agent', query: { q: quickAskText.value.trim() } }, 'AI Agent')
  quickAskText.value = ''
}

// ===== 首页待办面板：快速添加 =====
const todoDialogVisible = ref(false)
const todoForm = ref<{ title: string; remark: string; dueDate: string }>({
  title: '',
  remark: '',
  dueDate: '',
})

function openTodoDialog() {
  todoForm.value = { title: '', remark: '', dueDate: '' }
  todoDialogVisible.value = true
}

function submitTodoDialog() {
  if (!todoForm.value.title.trim()) return
  void homeStore.addTodo({
    title: todoForm.value.title.trim(),
    remark: todoForm.value.remark || undefined,
    dueDate: todoForm.value.dueDate || undefined,
  })
  todoDialogVisible.value = false
}

function handleTodoToggle(todo: { id: number; status: 'Pending' | 'Completed' }): void {
  void homeStore.toggleTodo(todo.id)
}

function gotoTodoPage() {
  openPage('/todo', '待办事项')
}

// 推荐动作
const recommendedActions = [
  { label: '整理周报', path: '/ai-agent' },
  { label: '运行健康检查', path: '/system-monitor' },
  { label: '管理技能', path: '/skills' },
  { label: '查看工作流', path: '/workflows' },
]

function goAction(path: string, label: string) {
  openPage(path, label)
}

// ===== 功能快捷网格（动态排版）=====
// 数据源：features.ts 内置功能 + 后端插件清单贡献的功能（含动态安装/启停的插件）。
// 排序：固定的排最前 → 按热度（带时间衰减）降序 → 冷启动回退 features.ts 原顺序。
const usageSnapshot = ref<UsageSnapshot>({ visits: {}, pinned: [] })
const rankTick = ref(Date.now())

const effectiveFeatures = computed(() => mergeFeatureList(homeStore.menuContributions))

const rankedEntries = computed(() =>
  rankEntriesDetailed(
    buildHomeEntries(effectiveFeatures.value),
    usageSnapshot.value,
    rankTick.value,
  ).slice(0, HOME_ENTRY_LIMIT),
)

const hasMoreEntries = computed(
  () => buildHomeEntries(effectiveFeatures.value).length > HOME_ENTRY_LIMIT,
)

function togglePin(key: string): void {
  usageStore.togglePin(key)
  // 固定是显式操作，用户期待即时反馈，因此立即刷新排序
  usageSnapshot.value = usageStore.snapshot()
}

function clickEntry(entry: HomeEntry): void {
  openPage(entry.path, entry.label)
}

function goAllFeatures(): void {
  openPage('/all-features', '所有功能')
}

// 相对时间格式化
function formatRelativeTime(date: Date | null): string {
  if (!date) return ''
  const t = date instanceof Date ? date : new Date(date)
  if (isNaN(t.getTime())) return ''
  const diff = Date.now() - t.getTime()
  if (diff < 60_000) return '刚刚'
  if (diff < 3_600_000) return `${Math.floor(diff / 60_000)} 分钟前`
  if (diff < 86_400_000) return `${Math.floor(diff / 3_600_000)} 小时前`
  return `${Math.floor(diff / 86_400_000)} 天前`
}

// 系统监控进度数值（百分比 0-100）
const cpuPercent = computed(() => homeStore.systemMetrics?.cpu?.totalUsage ?? 0)
const memoryPercent = computed(() => homeStore.systemMetrics?.memory?.usagePercent ?? 0)
const diskPercent = computed(() => {
  const disks = homeStore.systemMetrics?.disks
  if (!disks || disks.length === 0) return 0
  return disks[0].usagePercent ?? 0
})

// 能力成长等级名称
const forgeLevelName = computed(() => {
  const lv = homeStore.forgeLevel
  if (lv <= 1) return '见习'
  if (lv === 2) return '学徒'
  if (lv === 3) return '匠人'
  if (lv === 4) return '锻造师'
  return '大师'
})

// 实时时钟
const currentTime = ref('')
let clockTimer: ReturnType<typeof setInterval> | null = null

function updateClock() {
  const now = new Date()
  currentTime.value = now.toTimeString().slice(0, 8)
}

onMounted(() => {
  updateClock()
  clockTimer = setInterval(updateClock, 1000)

  // 使用度快照：本次会话据此排序（挂载时取一次，避免点一下就把卡片挤走）
  usageStore.load()
  usageSnapshot.value = usageStore.snapshot()
  rankTick.value = Date.now()

  // 初始化首页聚合数据（并行拉取各模块接口；失败时保持空/0）
  void homeStore.init()
})

onUnmounted(() => {
  if (clockTimer) clearInterval(clockTimer)
})
</script>

<template>
  <div class="home-view">
    <!-- Forge ambient glow -->
    <div class="ambient-glow" aria-hidden="true" />

    <div class="home-content">
      <!-- ===== Hero 区 ===== -->
      <section class="hero-section">
        <div class="hero-text">
          <h1 class="hero-title">
            <svg
              class="hero-anvil-icon"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
              aria-hidden="true"
            >
              <path d="M12 20h-6a2 2 0 0 1-2-2v-2l-2-4V8a2 2 0 0 1 2-2h1l3-4h4l3 4h1a2 2 0 0 1 2 2v4l-2 4v2a2 2 0 0 1-2 2h-1" />
              <path d="M8 16h8" />
            </svg>
            {{ greeting }}
          </h1>
          <p class="hero-subtitle">{{ homeStore.summaryText }}</p>
        </div>

        <!-- 快速提问条 -->
        <div class="quick-ask">
          <input
            v-model="quickAskText"
            class="quick-ask-input"
            placeholder="想做什么？例如：整理周报、运行健康检查…"
            @keydown.enter="submitQuickAsk"
          />
          <button class="quick-ask-button" title="发送" @click="submitQuickAsk">
            <svg
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <line x1="22" y1="2" x2="11" y2="13" />
              <polygon points="22 2 15 22 11 13 2 9 22 2" />
            </svg>
          </button>
        </div>

        <!-- 推荐动作 chips -->
        <div class="action-chips">
          <button
            v-for="action in recommendedActions"
            :key="action.path"
            class="action-chip"
            @click="goAction(action.path, action.label)"
          >
            {{ action.label }}
          </button>
        </div>
      </section>

      <!-- ===== 功能快捷网格（动态排版：固定优先 + 使用频率降序）===== -->
      <section class="quick-grid">
        <div class="quick-grid-header">
          <h2 class="section-title">常用功能</h2>
          <button v-if="hasMoreEntries" class="quick-grid-more" title="浏览全部功能" @click="goAllFeatures">
            查看全部 →
          </button>
        </div>
        <div class="quick-grid-entries">
          <div
            v-for="item in rankedEntries"
            :key="item.entry.key"
            class="quick-entry"
            :class="{ 'quick-entry--hot': item.score > 0, 'quick-entry--pinned': item.pinned }"
            :title="item.entry.label"
            tabindex="0"
            @click="clickEntry(item.entry)"
            @keydown.enter.prevent="clickEntry(item.entry)"
          >
            <span class="quick-entry__icon" v-html="lucideIconSvg(item.entry.icon, 18)" />
            <span class="quick-entry__label">{{ item.entry.label }}</span>
            <span
              class="quick-entry__pin"
              role="button"
              tabindex="0"
              :aria-pressed="item.pinned"
              :title="item.pinned ? '取消固定' : '固定到最前'"
              @click.stop="togglePin(item.entry.key)"
              @keydown.enter.stop.prevent="togglePin(item.entry.key)"
            >
              <svg
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="2"
                stroke-linecap="round"
                stroke-linejoin="round"
                aria-hidden="true"
              >
                <path d="M12 17v5" />
                <path d="M9 10.76a2 2 0 0 1-1.11 1.79l-1.78.9A2 2 0 0 0 7 17h10a2 2 0 0 0 .89-3.55l-1.78-.9A2 2 0 0 1 15 10.76V7a1 1 0 0 1 1-1 2 2 0 0 0 0-4H8a2 2 0 0 0 0 4 1 1 0 0 1 1 1z" />
              </svg>
            </span>
          </div>
        </div>
      </section>

      <!-- ===== 核心数据看板 + 右栏 ===== -->
      <div class="dashboard-layout">
        <!-- 核心数据看板 -->
        <section class="dashboard">
          <div class="dashboard-header">
            <h2 class="section-title">核心看板</h2>
          </div>
          <div class="widget-grid">
            <!-- AI Agent Widget -->
            <div class="widget-card widget-card--forge">
              <div class="widget-card-header">
                <h3 class="widget-card-title">AI Agent</h3>
                <span
                  class="status-dot"
                  :class="homeStore.aiAgentStatus.connected ? 'status-dot--online' : 'status-dot--offline'"
                  aria-hidden="true"
                />
              </div>
              <div class="widget-card-body">
                <div class="widget-status-text">
                  {{ homeStore.aiAgentStatus.connected ? '就绪' : '离线' }}
                </div>
                <div class="widget-meta">今日 {{ homeStore.aiAgentStatus.todayConversations }} 次对话</div>
                <div class="widget-meta">{{ homeStore.aiAgentStatus.currentModel }}</div>
              </div>
            </div>

            <!-- 工作流 Widget -->
            <div class="widget-card">
              <div class="widget-card-header">
                <h3 class="widget-card-title">工作流</h3>
              </div>
              <div class="widget-card-body">
                <template v-if="homeStore.workflowStatus.total === 0">
                  <div class="empty-hint">暂无工作流</div>
                </template>
                <template v-else>
                  <div class="widget-status-text">{{ homeStore.workflowStatus.total }} 个</div>
                  <div v-if="homeStore.workflowStatus.running > 0" class="widget-meta">
                    <span class="badge-running">{{ homeStore.workflowStatus.running }} 运行中</span>
                  </div>
                </template>
              </div>
            </div>

            <!-- 脚本运行器 Widget -->
            <div class="widget-card">
              <div class="widget-card-header">
                <h3 class="widget-card-title">脚本运行器</h3>
              </div>
              <div class="widget-card-body">
                <template v-if="homeStore.scriptStatus.total === 0">
                  <div class="empty-hint">暂无脚本</div>
                </template>
                <template v-else>
                  <div class="widget-status-text">{{ homeStore.scriptStatus.total }} 个脚本</div>
                  <div class="widget-meta">
                    {{ homeStore.scriptStatus.lastRunTime ? `上次: ${formatRelativeTime(homeStore.scriptStatus.lastRunTime)}` : '尚未执行脚本' }}
                  </div>
                </template>
              </div>
            </div>

            <!-- 能力成长 Widget -->
            <div class="widget-card">
              <div class="widget-card-header">
                <h3 class="widget-card-title">能力成长</h3>
              </div>
              <div class="widget-card-body">
                <div class="widget-status-text">Lv.{{ homeStore.forgeLevel }} · {{ forgeLevelName }}</div>
                <div class="metric-bar">
                  <div class="metric-bar-fill metric-bar-fill--forge" :style="{ width: `${homeStore.forgeProgress}%` }" />
                </div>
              </div>
            </div>

            <!-- 系统监控 Widget -->
            <div class="widget-card widget-card--forge">
              <div class="widget-card-header">
                <h3 class="widget-card-title">系统监控</h3>
                <span
                  class="status-dot"
                  :class="homeStore.systemMetrics ? 'status-dot--online' : 'status-dot--idle'"
                  aria-hidden="true"
                />
              </div>
              <div class="widget-card-body">
                <template v-if="!homeStore.systemMetrics">
                  <div class="skeleton-bar" />
                  <div class="skeleton-bar" />
                  <div class="skeleton-bar" />
                </template>
                <template v-else>
                  <div class="metric-row">
                    <span class="metric-label">CPU</span>
                    <div class="metric-bar">
                      <div class="metric-bar-fill metric-bar-fill--lava" :style="{ width: `${cpuPercent}%` }" />
                    </div>
                    <span class="metric-value">{{ cpuPercent.toFixed(1) }}%</span>
                  </div>
                  <div class="metric-row">
                    <span class="metric-label">内存</span>
                    <div class="metric-bar">
                      <div class="metric-bar-fill metric-bar-fill--lava" :style="{ width: `${memoryPercent}%` }" />
                    </div>
                    <span class="metric-value">{{ memoryPercent.toFixed(1) }}%</span>
                  </div>
                  <div class="metric-row">
                    <span class="metric-label">磁盘</span>
                    <div class="metric-bar">
                      <div class="metric-bar-fill metric-bar-fill--lava" :style="{ width: `${diskPercent}%` }" />
                    </div>
                    <span class="metric-value">{{ diskPercent.toFixed(1) }}%</span>
                  </div>
                </template>
              </div>
            </div>
          </div>
        </section>

        <!-- 右栏动态 -->
        <aside class="right-rail">
          <section class="todo-panel">
            <div class="panel-header">
              <h2 class="panel-title">待办</h2>
              <div class="panel-header-actions">
                <button
                  class="panel-action-btn"
                  title="新建待办"
                  aria-label="新建待办"
                  @click="openTodoDialog"
                >
                  <svg
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    stroke-width="2"
                    stroke-linecap="round"
                    stroke-linejoin="round"
                    aria-hidden="true"
                  >
                    <line x1="12" y1="5" x2="12" y2="19" />
                    <line x1="5" y1="12" x2="19" y2="12" />
                  </svg>
                </button>
                <button
                  class="panel-action-btn panel-action-btn--link"
                  title="查看全部"
                  aria-label="查看全部待办"
                  @click="gotoTodoPage"
                >
                  <svg
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    stroke-width="2"
                    stroke-linecap="round"
                    stroke-linejoin="round"
                    aria-hidden="true"
                  >
                    <polyline points="9 18 15 12 9 6" />
                  </svg>
                </button>
              </div>
            </div>
            <div class="panel-body">
              <div v-if="homeStore.recentTodos.length === 0" class="empty-hint">暂无待处理待办</div>
              <ul v-else class="todo-list">
                <li v-for="todo in homeStore.recentTodos" :key="todo.id" class="todo-item">
                  <input
                    type="checkbox"
                    class="todo-checkbox"
                    :checked="todo.status === 'Completed'"
                    @change="handleTodoToggle(todo)"
                  />
                  <span class="todo-text" :class="{ 'todo-text--done': todo.status === 'Completed' }">{{ todo.title }}</span>
                </li>
              </ul>
              <div v-if="homeStore.todoPendingTotal > homeStore.recentTodos.length" class="panel-footer-link" @click="gotoTodoPage">
                查看全部 {{ homeStore.todoPendingTotal }} 条 →
              </div>
            </div>
          </section>

          <section class="activity-panel">
            <div class="panel-header">
              <h2 class="panel-title">最近活动</h2>
            </div>
            <div class="panel-body">
              <div v-if="homeStore.recentActivities.length === 0" class="empty-hint">暂无最近活动</div>
              <ul v-else class="activity-list">
                <li v-for="activity in homeStore.recentActivities" :key="activity.id" class="activity-item">
                  <span class="activity-icon" :class="`activity-icon--${activity.type}`" aria-hidden="true" />
                  <div class="activity-content">
                    <div class="activity-desc">{{ activity.description }}</div>
                    <div class="activity-time">{{ formatRelativeTime(activity.timestamp) }}</div>
                  </div>
                </li>
              </ul>
            </div>
          </section>
        </aside>
      </div>
    </div>

    <!-- ===== 底部状态栏 ===== -->
    <footer class="bottom-bar">
      <div class="bottom-bar-left">
        <svg
          width="14"
          height="14"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          stroke-linecap="round"
          stroke-linejoin="round"
        >
          <path d="M6 9H4.5a2.5 2.5 0 0 1 0-5H6" />
          <path d="M18 9h1.5a2.5 2.5 0 0 0 0-5H18" />
          <path d="M4 22h16" />
          <path d="M10 14.66V17c0 .55-.47.98-.97 1.21C7.85 18.75 7 20.24 7 22" />
          <path d="M14 14.66V17c0 .55.47.98.97 1.21C16.15 18.75 17 20.24 17 22" />
          <path d="M18 2H6v7a6 6 0 0 0 12 0V2Z" />
        </svg>
        <span class="bottom-bar-label">锻层 Lv.{{ homeStore.forgeLevel }}</span>
        <div class="bottom-bar-track">
          <div class="progress-fill" :style="{ width: `${homeStore.forgeProgress}%` }" />
        </div>
      </div>
      <div class="bottom-bar-right">
        <span class="bottom-bar-clock">{{ currentTime }}</span>
      </div>
    </footer>

    <!-- 首页快速添加待办弹窗（内联原生 modal，不依赖宿主 TodoEditDialog） -->
    <div v-if="todoDialogVisible" class="todo-modal-mask" @click.self="todoDialogVisible = false">
      <div class="todo-modal" role="dialog" aria-modal="true" aria-label="新建待办">
        <div class="todo-modal-header">
          <h3 class="todo-modal-title">新建待办</h3>
          <button class="todo-modal-close" aria-label="关闭" @click="todoDialogVisible = false">×</button>
        </div>
        <div class="todo-modal-body">
          <label class="todo-field">
            <span class="todo-field-label">标题<span class="req">*</span></span>
            <input
              v-model="todoForm.title"
              class="todo-input"
              placeholder="待办标题"
              @keydown.enter="submitTodoDialog"
            />
          </label>
          <label class="todo-field">
            <span class="todo-field-label">备注</span>
            <textarea v-model="todoForm.remark" class="todo-textarea" rows="2" placeholder="可选" />
          </label>
          <label class="todo-field">
            <span class="todo-field-label">截止日期</span>
            <input v-model="todoForm.dueDate" type="date" class="todo-input" />
          </label>
        </div>
        <div class="todo-modal-footer">
          <button class="todo-btn todo-btn--ghost" @click="todoDialogVisible = false">取消</button>
          <button class="todo-btn todo-btn--primary" :disabled="!todoForm.title.trim()" @click="submitTodoDialog">
            保存
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
/* ============================
   HomeView — 首页仪表板（插件内副本，与宿主同源样式）
   四段式布局 · 工坊锻造风格
   ============================ */

.home-view {
  position: relative;
  display: flex;
  flex-direction: column;
  height: 100%;
  background: var(--el-bg-color);
  overflow: hidden;
}

/* Forge ambient glow（Hero 区附近高光） */
.ambient-glow {
  position: absolute;
  top: 0;
  left: 0;
  width: 600px;
  height: 500px;
  background: radial-gradient(ellipse at 30% 20%, rgba(245, 158, 11, 0.08) 0%, transparent 70%);
  pointer-events: none;
  z-index: 0;
}

.home-content {
  position: relative;
  z-index: 1;
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  scrollbar-width: thin;
  padding: 24px 28px;
  max-width: 1600px;
  width: 100%;
  margin: 0 auto;
  display: flex;
  flex-direction: column;
  gap: 20px;
}

/* 关键约束：home-content 是「flex 列 + overflow-y:auto」的滚动容器，
   其子区块必须保持自然高度，绝不能被 flex 压缩。
   否则当内容总高超过容器（视口较矮时）时，各区块会被 flex-shrink 挤扁，
   再配合区块自身的 overflow:hidden，直接裁掉内部文字
   （典型症状：1280×720 下 Hero 问候语被裁掉半截、副标题/提问条/推荐动作全被吃掉）。
   用「直接子元素」选择器而非逐个类名，保证后续新增区块自动继承该不可压缩约束。 */
.home-content > * {
  flex-shrink: 0;
}

/* ============================
   Hero 区
   ============================ */
.hero-section {
  display: flex;
  flex-direction: column;
  gap: 16px;
  background: var(--el-bg-color-page);
  border: 1px solid var(--el-border-color);
  border-left: 3px solid var(--el-color-primary);
  border-radius: 12px;
  padding: 24px 28px;
  box-shadow: 0 0 40px rgba(245, 158, 11, 0.04), inset 1px 0 0 rgba(245, 158, 11, 0.1);
  position: relative;
  overflow: hidden;
}

.hero-text {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.hero-title {
  font-size: 1.75rem;
  font-weight: 700;
  color: var(--el-text-color-primary);
  margin: 0;
  line-height: 1.2;
  display: flex;
  align-items: center;
  gap: 10px;
}

.hero-anvil-icon {
  flex-shrink: 0;
  width: 28px;
  height: 28px;
  color: var(--el-color-primary);
}

.hero-subtitle {
  font-size: 0.9375rem;
  color: var(--el-text-color-regular);
  margin: 0;
}

.quick-ask {
  display: flex;
  align-items: center;
  gap: 8px;
  max-width: 640px;
}

.quick-ask-input {
  flex: 1;
  min-width: 0;
  height: 44px;
  padding: 0 16px;
  background: var(--el-bg-color-page);
  border: 1px solid var(--el-border-color);
  border-radius: var(--el-border-radius-base);
  color: var(--el-text-color-primary);
  font-size: 0.9375rem;
  font-family: inherit;
  transition: border-color 150ms ease, box-shadow 150ms ease;
}

.quick-ask-input::placeholder {
  color: var(--el-text-color-secondary);
}

.quick-ask-input:focus {
  outline: none;
  border-color: var(--el-color-primary);
  box-shadow: 0 0 0 3px rgba(245, 158, 11, 0.15);
}

.quick-ask-button {
  flex-shrink: 0;
  width: 44px;
  height: 44px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--el-color-primary);
  color: var(--el-color-white);
  border: none;
  border-radius: var(--el-border-radius-base);
  cursor: pointer;
  transition: background-color 150ms ease;
}

.quick-ask-button:hover {
  background: var(--el-color-primary-light-3);
}

.quick-ask-button svg {
  width: 18px;
  height: 18px;
}

.action-chips {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.action-chip {
  padding: 6px 14px;
  background: transparent;
  border: 1px solid var(--el-border-color);
  border-radius: var(--radius-full, 9999px);
  color: var(--el-text-color-regular);
  font-size: 0.8125rem;
  font-family: inherit;
  cursor: pointer;
  transition: border-color 150ms ease, color 150ms ease, background 150ms ease;
}

.action-chip:hover {
  border-color: var(--el-color-primary);
  color: var(--el-color-primary);
  background: var(--primary-light);
}

/* ============================
   通用标题
   ============================ */
.section-title {
  font-size: 0.875rem;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 0;
}

/* ============================
   功能快捷网格
   ============================ */
.quick-grid {
  display: flex;
  flex-direction: column;
  gap: 10px;
  background: var(--el-bg-color-page);
  border: 1px solid var(--el-border-color);
  border-radius: 12px;
  padding: 16px;
}

.quick-grid-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}

/* 「查看全部」入口 */
.quick-grid-more {
  flex-shrink: 0;
  padding: 4px 10px;
  background: transparent;
  border: 1px solid var(--el-border-color);
  border-radius: var(--el-border-radius-base);
  color: var(--el-text-color-secondary);
  font-size: 0.75rem;
  font-family: inherit;
  cursor: pointer;
  transition: color 150ms ease, border-color 150ms ease, background 150ms ease;
}

.quick-grid-more:hover {
  color: var(--el-color-primary);
  border-color: var(--el-color-primary);
  background: color-mix(in srgb, var(--el-color-primary) 10%, transparent);
}

.quick-grid-entries {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(140px, 1fr));
  gap: 10px;
}

.quick-entry {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 12px;
  background: transparent;
  border: 1px solid var(--el-border-color);
  border-radius: var(--el-border-radius-base);
  color: var(--el-text-color-regular);
  font-size: 0.8125rem;
  font-family: inherit;
  cursor: pointer;
  user-select: none;
  text-align: left;
  transition: border-color 150ms ease, background 150ms ease, color 150ms ease, box-shadow 150ms ease;
}

.quick-entry:hover {
  background: var(--el-fill-color-light);
  color: var(--el-text-color-primary);
}

.quick-entry:focus-visible {
  outline: none;
  border-color: var(--el-color-primary);
  box-shadow: 0 0 0 3px color-mix(in srgb, var(--el-color-primary) 15%, transparent);
}

.quick-entry__icon {
  flex-shrink: 0;
  display: inline-flex;
  color: var(--el-color-primary);
}

.quick-entry__icon svg {
  width: 18px;
  height: 18px;
}

/* 固定开关：默认隐藏，hover / 键盘聚焦时显形 */
.quick-entry__pin {
  flex-shrink: 0;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 20px;
  height: 20px;
  border-radius: var(--el-border-radius-small, 4px);
  color: var(--el-text-color-secondary);
  opacity: 0;
  transition: opacity 150ms ease, color 150ms ease, background 150ms ease;
}

.quick-entry:hover .quick-entry__pin,
.quick-entry__pin:focus-visible {
  opacity: 1;
}

.quick-entry__pin:hover {
  color: var(--el-color-primary);
  background: color-mix(in srgb, var(--el-color-primary) 12%, transparent);
}

.quick-entry__pin svg {
  width: 14px;
  height: 14px;
}

.quick-entry__label {
  flex: 1;
  min-width: 0;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

/* 高频入口：炉火边框 + 微光阴影 */
.quick-entry--hot {
  border: 1px solid transparent;
  border-left: 2px solid var(--el-color-primary);
  box-shadow: 0 0 12px rgba(245, 158, 11, 0.1);
  color: var(--el-text-color-primary);
}

.quick-entry--hot:hover {
  border-left-color: var(--el-color-primary-light-3);
  box-shadow: 0 0 18px rgba(245, 158, 11, 0.2);
  background: var(--el-fill-color-light);
}

/* 已固定：图钉常显 + 炉火左边框，与「仅高频」区分开 */
.quick-entry--pinned {
  border-left: 2px solid var(--el-color-primary);
  padding-left: 11px;
  box-shadow: 0 0 14px color-mix(in srgb, var(--el-color-primary) 10%, transparent);
  color: var(--el-text-color-primary);
}

.quick-entry--pinned .quick-entry__pin {
  opacity: 1;
  color: var(--el-color-primary);
}

/* ============================
   看板 + 右栏 两栏布局
   ============================ */
.dashboard-layout {
  display: grid;
  grid-template-columns: 1fr 280px;
  gap: 16px;
  align-items: start;
}

/* ============================
   核心数据看板
   ============================ */
.dashboard {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.dashboard-header {
  display: flex;
  align-items: center;
}

.widget-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
  gap: 12px;
}

.widget-card {
  display: flex;
  flex-direction: column;
  gap: 8px;
  background: var(--el-bg-color-page);
  border: 1px solid var(--el-border-color);
  border-radius: 12px;
  padding: 14px 16px;
  min-height: 92px;
  transition: border-color 150ms ease, background 150ms ease, box-shadow 150ms ease;
}

.widget-card:hover {
  border-color: rgba(245, 158, 11, 0.3);
  background: var(--el-fill-color-light);
}

/* 炉火边框（关键 Widget） */
.widget-card--forge {
  border-left: 2px solid var(--el-color-primary);
  box-shadow: 0 0 20px rgba(245, 158, 11, 0.06), inset 1px 0 0 rgba(245, 158, 11, 0.1);
}

.widget-card--forge:hover {
  border-left-color: var(--el-color-primary-light-3);
  box-shadow: 0 0 30px rgba(245, 158, 11, 0.12), inset 1px 0 0 rgba(245, 158, 11, 0.2);
}

.widget-card-header {
  display: flex;
  align-items: center;
  gap: 8px;
}

.widget-card-title {
  font-size: 0.8125rem;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 0;
}

.widget-card-body {
  display: flex;
  flex-direction: column;
  gap: 4px;
  font-size: 0.8125rem;
  color: var(--el-text-color-secondary);
}

.widget-status-text {
  font-size: 0.9375rem;
  color: var(--el-text-color-primary);
  font-weight: 500;
}

.widget-meta {
  font-size: 0.75rem;
  color: var(--el-text-color-secondary);
}

.badge-running {
  display: inline-block;
  padding: 2px 8px;
  background: rgba(245, 158, 11, 0.12);
  color: var(--el-color-primary);
  border-radius: var(--radius-full, 9999px);
  font-size: 0.6875rem;
}

/* 状态点呼吸灯 */
.status-dot {
  width: 8px;
  height: 8px;
  border-radius: 9999px;
  flex-shrink: 0;
}

.status-dot--online {
  background: var(--el-color-success);
  box-shadow: 0 0 6px var(--el-color-success);
  animation: pulse 2s ease-in-out infinite;
}

.status-dot--offline {
  background: var(--el-color-danger);
  box-shadow: 0 0 6px var(--el-color-danger);
}

.status-dot--idle {
  background: var(--el-text-color-secondary);
}

@keyframes pulse {
  0%, 100% { box-shadow: 0 0 4px rgba(245, 158, 11, 0.4); }
  50% { box-shadow: 0 0 12px rgba(245, 158, 11, 0.8); }
}

/* ============================
   指标进度条（系统监控 / 能力成长）
   ============================ */
.metric-row {
  display: grid;
  grid-template-columns: 40px 1fr 48px;
  align-items: center;
  gap: 8px;
}

.metric-label {
  font-size: 0.75rem;
  color: var(--el-text-color-regular);
}

.metric-bar {
  height: 4px;
  border-radius: var(--radius-full, 9999px);
  background: var(--el-fill-color-light);
  overflow: hidden;
}

.metric-bar-fill {
  height: 100%;
  border-radius: inherit;
  transition: width 150ms ease;
}

/* 熔岩渐变进度条 */
.metric-bar-fill--lava,
.metric-bar-fill--forge {
  background: linear-gradient(90deg, var(--el-color-primary-light-3), var(--el-color-primary), var(--el-color-primary-light-3));
  background-size: 200% 100%;
  animation: shimmer 4s linear infinite;
}

.metric-value {
  font-size: 0.75rem;
  color: var(--el-text-color-regular);
  text-align: right;
  font-family: var(--font-family-mono);
  font-variant-numeric: tabular-nums;
}

/* ============================
   骨架占位
   ============================ */
.skeleton-bar {
  height: 4px;
  border-radius: var(--radius-full, 9999px);
  background: var(--el-fill-color-light);
  animation: skeleton-pulse 1.5s ease-in-out infinite;
}

.skeleton-bar + .skeleton-bar {
  margin-top: 6px;
}

@keyframes skeleton-pulse {
  0%, 100% { opacity: 0.6; }
  50% { opacity: 0.3; }
}

/* ============================
   空状态
   ============================ */
.empty-hint {
  font-size: 0.75rem;
  color: var(--el-text-color-secondary);
  text-align: center;
  padding: 12px 0;
}

/* ============================
   右栏动态
   ============================ */
.right-rail {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.todo-panel,
.activity-panel {
  display: flex;
  flex-direction: column;
  gap: 10px;
  background: var(--el-bg-color-page);
  border: 1px solid var(--el-border-color);
  border-radius: 12px;
  padding: 14px 16px;
  transition: border-color 150ms ease, background 150ms ease;
}

.todo-panel:hover,
.activity-panel:hover {
  border-color: rgba(245, 158, 11, 0.3);
  background: var(--el-fill-color-light);
}

.panel-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}

.panel-title {
  font-size: 0.8125rem;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 0;
}

.panel-header-actions {
  display: flex;
  align-items: center;
  gap: 4px;
}

.panel-action-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 22px;
  height: 22px;
  padding: 0;
  background: transparent;
  border: 1px solid transparent;
  border-radius: var(--el-border-radius-small, 4px);
  color: var(--el-text-color-secondary);
  cursor: pointer;
  transition: color 150ms ease, background 150ms ease, border-color 150ms ease;
}

.panel-action-btn svg {
  width: 14px;
  height: 14px;
}

.panel-action-btn:hover {
  color: var(--el-color-primary);
  background: var(--el-fill-color-light);
  border-color: var(--el-border-color);
}

.panel-action-btn--link:hover {
  color: var(--el-color-primary);
}

.panel-footer-link {
  margin-top: 8px;
  font-size: 0.75rem;
  color: var(--el-color-primary);
  cursor: pointer;
  text-align: right;
  transition: color 150ms ease;
}

.panel-footer-link:hover {
  color: var(--el-color-primary-light-3);
  text-decoration: underline;
}

.panel-body {
  font-size: 0.8125rem;
  color: var(--el-text-color-secondary);
}

.todo-list,
.activity-list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.todo-item {
  display: flex;
  align-items: center;
  gap: 8px;
}

.todo-checkbox {
  flex-shrink: 0;
  width: 14px;
  height: 14px;
  accent-color: var(--el-color-primary);
  cursor: pointer;
}

.todo-text {
  font-size: 0.8125rem;
  color: var(--el-text-color-regular);
  word-break: break-all;
}

.todo-text--done {
  text-decoration: line-through;
  color: var(--el-text-color-secondary);
}

.activity-item {
  display: flex;
  align-items: flex-start;
  gap: 8px;
}

.activity-icon {
  flex-shrink: 0;
  width: 8px;
  height: 8px;
  margin-top: 4px;
  border-radius: 9999px;
  background: var(--el-color-primary);
}

.activity-icon--workflow { background: var(--el-color-primary); }
.activity-icon--script { background: var(--el-color-info); }
.activity-icon--chat { background: var(--el-color-success); }
.activity-icon--system { background: var(--el-color-warning); }

.activity-content {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.activity-desc {
  font-size: 0.8125rem;
  color: var(--el-text-color-regular);
  word-break: break-all;
}

.activity-time {
  font-size: 0.6875rem;
  color: var(--el-text-color-secondary);
  font-family: var(--font-family-mono);
}

/* ============================
   底部状态栏
   ============================ */
.bottom-bar {
  position: relative;
  z-index: 1;
  display: flex;
  align-items: center;
  justify-content: space-between;
  height: 48px;
  padding: 0 28px;
  background: var(--el-bg-color-page);
  border-top: 1px solid var(--el-border-color);
  flex-shrink: 0;
}

.bottom-bar-left {
  display: flex;
  align-items: center;
  gap: 10px;
  min-width: 0;
  flex: 1;
  max-width: 360px;
  color: var(--el-color-primary);
}

.bottom-bar-left svg {
  flex-shrink: 0;
}

.bottom-bar-label {
  font-size: 0.75rem;
  font-weight: 500;
  color: var(--el-text-color-regular);
  white-space: nowrap;
  flex-shrink: 0;
}

.bottom-bar-track {
  flex: 1;
  min-width: 60px;
  height: 4px;
  border-radius: 999px;
  background: var(--el-fill-color-light);
  overflow: hidden;
}

/* 熔岩进度条 */
.progress-fill {
  height: 100%;
  border-radius: 999px;
  background: linear-gradient(90deg, var(--el-color-primary-light-3), var(--el-color-primary), var(--el-color-primary-light-3));
  background-size: 200% 100%;
  animation: shimmer 4s linear infinite;
}

@keyframes shimmer {
  0% { background-position: -200% 0; }
  100% { background-position: 200% 0; }
}

.bottom-bar-right {
  flex-shrink: 0;
  min-width: 80px;
  text-align: right;
}

.bottom-bar-clock {
  font-size: 0.75rem;
  color: var(--text-tertiary);
  font-family: var(--font-family-mono);
  font-variant-numeric: tabular-nums;
}

/* ============================
   内联待办弹窗
   ============================ */
.todo-modal-mask {
  position: fixed;
  inset: 0;
  z-index: 1000;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(0, 0, 0, 0.45);
}

.todo-modal {
  width: 420px;
  max-width: calc(100vw - 32px);
  background: var(--el-bg-color-overlay, var(--el-bg-color));
  border: 1px solid var(--el-border-color);
  border-radius: 12px;
  box-shadow: 0 12px 40px rgba(0, 0, 0, 0.3);
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.todo-modal-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 14px 16px;
  border-bottom: 1px solid var(--el-border-color);
}

.todo-modal-title {
  font-size: 0.9375rem;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 0;
}

.todo-modal-close {
  width: 24px;
  height: 24px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: transparent;
  border: none;
  border-radius: var(--el-border-radius-small, 4px);
  color: var(--el-text-color-secondary);
  font-size: 1.25rem;
  line-height: 1;
  cursor: pointer;
}

.todo-modal-close:hover {
  color: var(--el-text-color-primary);
  background: var(--el-fill-color-light);
}

.todo-modal-body {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 16px;
}

.todo-field {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.todo-field-label {
  font-size: 0.75rem;
  color: var(--el-text-color-regular);
}

.todo-field-label .req {
  color: var(--el-color-danger);
  margin-left: 2px;
}

.todo-input,
.todo-textarea {
  width: 100%;
  padding: 8px 10px;
  background: var(--el-bg-color-page);
  border: 1px solid var(--el-border-color);
  border-radius: var(--el-border-radius-base);
  color: var(--el-text-color-primary);
  font-size: 0.875rem;
  font-family: inherit;
  box-sizing: border-box;
  transition: border-color 150ms ease, box-shadow 150ms ease;
}

.todo-input:focus,
.todo-textarea:focus {
  outline: none;
  border-color: var(--el-color-primary);
  box-shadow: 0 0 0 3px rgba(245, 158, 11, 0.15);
}

.todo-textarea {
  resize: vertical;
}

.todo-modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  padding: 12px 16px;
  border-top: 1px solid var(--el-border-color);
}

.todo-btn {
  padding: 6px 16px;
  border-radius: var(--el-border-radius-base);
  font-size: 0.8125rem;
  font-family: inherit;
  cursor: pointer;
  border: 1px solid transparent;
  transition: background-color 150ms ease, border-color 150ms ease, color 150ms ease;
}

.todo-btn--ghost {
  background: transparent;
  border-color: var(--el-border-color);
  color: var(--el-text-color-regular);
}

.todo-btn--ghost:hover {
  color: var(--el-text-color-primary);
  border-color: var(--el-color-primary);
}

.todo-btn--primary {
  background: var(--el-color-primary);
  color: var(--el-color-white);
}

.todo-btn--primary:hover {
  background: var(--el-color-primary-light-3);
}

.todo-btn--primary:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

/* ============================
   Reduced motion：关闭动画
   ============================ */
@media (prefers-reduced-motion: reduce) {
  .progress-fill,
  .metric-bar-fill--lava,
  .metric-bar-fill--forge {
    animation: none;
  }

  .status-dot--online {
    animation: none;
  }

  .skeleton-bar {
    animation: none;
  }
}

/* ============================
   响应式：小屏单列
   ============================ */
@media (max-width: 1023px) {
  .home-content {
    padding: 16px;
    gap: 16px;
  }

  .dashboard-layout {
    grid-template-columns: 1fr;
  }

  .right-rail {
    order: 2;
  }

  .hero-title {
    font-size: 1.5rem;
  }

  .quick-ask {
    max-width: 100%;
  }
}
</style>
