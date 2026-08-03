<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useHomeStore } from '@/stores/home'
import TodoEditDialog from '@/components/todo/TodoEditDialog.vue'
import type { TodoCreateRequest, TodoItem, TodoUpdateRequest } from '@/types/todo'

const router = useRouter()
const homeStore = useHomeStore()

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
  router.push({ path: '/ai-agent', query: { q: quickAskText.value.trim() } })
  quickAskText.value = ''
}

// ===== 首页待办面板：快速添加 =====
const todoDialogVisible = ref(false)

function openTodoDialog() {
  todoDialogVisible.value = true
}

async function handleTodoSubmit(payload: { id?: number; data: TodoCreateRequest | TodoUpdateRequest }): Promise<void> {
  await homeStore.addTodo({
    title: payload.data.title ?? '',
    remark: payload.data.remark,
    dueDate: payload.data.dueDate
  })
  todoDialogVisible.value = false
}

function handleTodoToggle(todo: TodoItem): void {
  void homeStore.toggleTodo(todo.id)
}

function gotoTodoPage() {
  router.push('/todo')
}

// 推荐动作
const recommendedActions = [
  { label: '整理周报', path: '/ai-agent' },
  { label: '运行健康检查', path: '/system-monitor' },
  { label: '管理技能', path: '/skills' },
  { label: '查看工作流', path: '/workflows' }
]

function goAction(path: string) {
  router.push(path)
}

// ===== 功能快捷网格 =====
interface QuickEntry {
  key: string
  label: string
  icon: string // SVG path data
  path: string
}

// 基于路由表派生；无对应路由的入口使用 '#' 占位
const quickEntries: QuickEntry[] = [
  {
    key: 'ai-agent',
    label: 'AI Agent',
    icon: 'M12 2a3 3 0 0 1 3 3c0 1.3-.8 2.4-2 2.8V9h2a4 4 0 0 1 4 4v1a3 3 0 0 1-2 2.8V20a2 2 0 0 1-2 2H9a2 2 0 0 1-2-2v-3.2A3 3 0 0 1 5 14v-1a4 4 0 0 1 4-4h2V7.8c-1.2-.4-2-1.5-2-2.8a3 3 0 0 1 3-3z',
    path: '/ai-agent'
  },
  {
    key: 'workflows',
    label: '工作流',
    icon: 'M3 3h6v6H3zM15 3h6v6h-6zM9 15h6v6H9zM3 15h2v6H3zM19 15h2v6h-2z',
    path: '/workflows'
  },
  {
    key: 'code-snippets',
    label: '脚本运行',
    icon: 'M4 17l6-6-6-6M12 19h8',
    path: '/code-snippets'
  },
  {
    key: 'system-monitor',
    label: '系统监控',
    icon: 'M3 12h4l3-9 4 18 3-9h4',
    path: '/system-monitor'
  },
  {
    key: 'skills',
    label: '技能管理',
    icon: 'M13 2L3 14h9l-1 8 10-12h-9l1-8z',
    path: '/skills'
  },
  {
    key: 'json-format',
    label: 'JSON 格式化',
    icon: 'M8 3H7a2 2 0 0 0-2 2v5a2 2 0 0 1-2 2 2 2 0 0 1 2 2v5a2 2 0 0 0 2 2h1M16 3h1a2 2 0 0 1 2 2v5a2 2 0 0 0 2 2 2 2 0 0 0-2 2v5a2 2 0 0 1-2 2h-1',
    path: '#'
  },
  {
    key: 'base64',
    label: 'Base64 编解码',
    icon: 'M3 4l3 3-3 3M21 4l-3 3 3 3M11 20h2M9 14h6',
    path: '#'
  },
  {
    key: 'file-manage',
    label: '文件管理',
    icon: 'M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V7z',
    path: '#'
  },
  {
    key: 'regex-test',
    label: '正则测试',
    icon: 'M11 17a6 6 0 1 0 0-12 6 6 0 0 0 0 12zM21 21l-4.3-4.3',
    path: '#'
  },
  {
    key: 'timestamp',
    label: '时间戳转换',
    icon: 'M12 22c5.523 0 10-4.477 10-10S17.523 2 12 2 2 6.477 2 12s4.477 10 10 10zM12 6v6l4 2',
    path: '#'
  },
  {
    key: 'hash-calc',
    label: '哈希计算',
    icon: 'M4 9h16M4 15h16M10 3L8 21M16 3l-2 18',
    path: '#'
  }
]

// 使用频率统计
const usageCounts = ref<Record<string, number>>({})

function recordUsage(key: string) {
  usageCounts.value[key] = (usageCounts.value[key] || 0) + 1
  try {
    localStorage.setItem('forge-quick-usage', JSON.stringify(usageCounts.value))
  } catch {
    // 忽略存储失败
  }
}

const sortedEntries = computed(() =>
  [...quickEntries].sort((a, b) => (usageCounts.value[b.key] || 0) - (usageCounts.value[a.key] || 0))
)

function isHot(key: string): boolean {
  return (usageCounts.value[key] || 0) > 0
}

function clickEntry(entry: QuickEntry) {
  recordUsage(entry.key)
  if (entry.path && entry.path !== '#') {
    router.push(entry.path)
  }
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

  // 加载使用频率统计
  try {
    const stored = localStorage.getItem('forge-quick-usage')
    if (stored) usageCounts.value = JSON.parse(stored)
  } catch {
    usageCounts.value = {}
  }

  // 初始化首页聚合数据（拉取技能数等）
  homeStore.init()
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
            @click="goAction(action.path)"
          >
            {{ action.label }}
          </button>
        </div>
      </section>

      <!-- ===== 功能快捷网格 ===== -->
      <section class="quick-grid">
        <div class="quick-grid-header">
          <h2 class="section-title">常用功能</h2>
        </div>
        <div class="quick-grid-entries">
          <button
            v-for="entry in sortedEntries"
            :key="entry.key"
            class="quick-entry"
            :class="{ 'quick-entry--hot': isHot(entry.key) }"
            :title="entry.label"
            @click="clickEntry(entry)"
          >
            <svg
              class="quick-entry__icon"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
              aria-hidden="true"
            >
              <path :d="entry.icon" />
            </svg>
            <span class="quick-entry__label">{{ entry.label }}</span>
          </button>
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

    <!-- 首页快速添加待办弹窗 -->
    <TodoEditDialog
      :visible="todoDialogVisible"
      :todo="null"
      @update:visible="todoDialogVisible = $event"
      @submit="handleTodoSubmit"
    />
  </div>
</template>

<style scoped>
/* ============================
   HomeView — 首页仪表板
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
  text-align: left;
  transition: border-color 150ms ease, background 150ms ease, color 150ms ease, box-shadow 150ms ease;
}

.quick-entry:hover {
  background: var(--el-fill-color-light);
  color: var(--el-text-color-primary);
}

.quick-entry__icon {
  flex-shrink: 0;
  width: 18px;
  height: 18px;
  color: var(--el-color-primary);
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
  border-radius: 9999px;
  background: var(--el-fill-color-light);
  overflow: hidden;
}

/* 熔岩进度条 */
.progress-fill {
  height: 100%;
  border-radius: 9999px;
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
