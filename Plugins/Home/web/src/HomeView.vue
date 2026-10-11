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
const router = useRouter()

/** 判定一个候选桥函数是否可用（防注入到空值/非函数）。 */
function isBridge(fn: unknown): fn is (to: RouteLocationRaw, label?: string) => void {
  return typeof fn === 'function'
}

/** ① 宿主组件级桥（provide）。 */
const injectedBridge = inject<unknown>('forgeOpenPage', null)

/** 实际生效的导航实现 + 通道名。 */
function resolveOpenPage(): {
  fn: (to: RouteLocationRaw, label?: string) => void
  channel: string
} {
  if (isBridge(injectedBridge)) return { fn: injectedBridge, channel: 'inject' }

  const globalBridge = typeof window !== 'undefined' ? window.__FORGE_OPEN_PAGE__ : undefined
  if (isBridge(globalBridge)) return { fn: globalBridge as never, channel: 'window' }

  if (router && typeof router.push === 'function') {
    return { fn: (to) => router.push(to as never), channel: 'router' }
  }

  return {
    fn: (to) => {
      const path = typeof to === 'string' ? to : (to.path ?? '/')
      if (typeof window !== 'undefined') window.location.assign(path)
    },
    channel: 'location',
  }
}

const _resolved = resolveOpenPage()
console.debug('[Home] 导航桥通道:', _resolved.channel)
const openPage = _resolved.fn

const homeStore = useHomeStore()
const usageStore = useUsageStatsStore()

// 时段问候语（设计稿 HomeScreen.jsx 六档）
const greeting = computed(() => {
  const h = new Date().getHours()
  if (h < 6) return '凌晨'
  if (h < 12) return '上午'
  if (h < 14) return '中午'
  if (h < 18) return '下午'
  if (h < 22) return '傍晚'
  return '深夜'
})

// 快速提问输入
const quickAskText = ref('')

function submitQuickAsk() {
  if (!quickAskText.value.trim()) return
  openPage({ path: '/ai-agent', query: { q: quickAskText.value.trim() } }, 'AI Agent')
  quickAskText.value = ''
}

// 推荐建议（点选即填充输入框，设计稿 suggest-chip）
const suggestions = ['整理周报', '运行健康检查', '管理技能', '查看工作流']

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

// ===== 功能快捷网格（动态排版）=====
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

function togglePin(key: string): void {
  usageStore.togglePin(key)
  usageSnapshot.value = usageStore.snapshot()
}

function clickEntry(entry: HomeEntry): void {
  openPage(entry.path, entry.label)
}

// ===== 核心看板（5 列，设计稿 board-card 形态，数据全部来自 homeStore）=====
const boardCards = computed(() => {
  const agent = homeStore.aiAgentStatus
  const wf = homeStore.workflowStatus
  const sc = homeStore.scriptStatus
  const sys = homeStore.systemMetrics
  return [
    {
      title: 'AI Agent',
      dot: agent.connected ? 'on' : 'off',
      status: agent.connected ? '就绪' : '离线',
      body: `今日 ${agent.todayConversations} 次调用`,
      sub: agent.currentModel,
      tag: '/ai-agent',
    },
    {
      title: '工作流',
      dot: wf.total > 0 ? 'on' : 'off',
      status: wf.total > 0 ? `${wf.total} 个` : '暂无',
      body: wf.total > 0 ? (wf.running > 0 ? `${wf.running} 运行中` : '空闲') : '暂无工作流',
      sub: '',
      tag: '/workflows',
    },
    {
      title: '脚本运行器',
      dot: sc.total > 0 ? 'on' : 'off',
      status: sc.total > 0 ? `${sc.total} 个` : '暂无',
      body: sc.total > 0 ? (sc.lastRunTime ? `上次 ${formatRelativeTime(sc.lastRunTime)}` : '尚未执行') : '暂无脚本',
      sub: '',
      tag: '/script-runner',
    },
    {
      title: '能力成长',
      dot: 'green',
      status: '',
      body: `Lv.${homeStore.forgeLevel}`,
      sub: forgeLevelName.value,
      tag: '/profile',
    },
    {
      title: '系统监控',
      dot: sys ? 'on' : 'off',
      status: sys ? '正常' : '离线',
      body: '',
      sub: '',
      tag: '/system-monitor',
    },
  ]
})

function goBoard(board: { tag: string }): void {
  openPage(board.tag, '核心看板')
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

// 能力成长等级名称
const forgeLevelName = computed(() => {
  const lv = homeStore.forgeLevel
  if (lv <= 1) return '见习'
  if (lv === 2) return '学徒'
  if (lv === 3) return '匠人'
  if (lv === 4) return '锻造师'
  return '大师'
})

onMounted(() => {
  usageStore.load()
  usageSnapshot.value = usageStore.snapshot()
  rankTick.value = Date.now()

  void homeStore.init()
})

onUnmounted(() => {
  // 无定时器（时钟已由宿主 StatusBar 承载）
})
</script>

<template>
  <div class="home-view">
    <div class="home-content">
      <!-- ===== HERO 问候卡（设计稿 hero-card glass） ===== -->
      <section class="hero-card glass">
        <div class="greet">
          <div class="glyph" aria-hidden="true">匠</div>
          <h1>{{ greeting }}好，锻造师</h1>
        </div>
        <div class="sub">
          今日 <span class="num">{{ homeStore.todayToolCalls }}</span> 次工具调用 · <span class="num">{{ homeStore.enabledSkillsCount }}</span> 个技能已启用
        </div>

        <div class="composer">
          <input
            v-model="quickAskText"
            placeholder="想做什么？例如：整理周报，运行健康检查…"
            @keydown.enter="submitQuickAsk"
          />
          <button class="send" title="发送" aria-label="发送" @click="submitQuickAsk">
            <svg
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
              aria-hidden="true"
            >
              <line x1="22" y1="2" x2="11" y2="13" />
              <polygon points="22 2 15 22 11 13 2 9 22 2" />
            </svg>
          </button>
        </div>

        <div class="suggest-row">
          <div
            v-for="s in suggestions"
            :key="s"
            class="suggest-chip"
            role="button"
            tabindex="0"
            @click="quickAskText = s"
            @keydown.enter.prevent="quickAskText = s"
          >
            {{ s }}
          </div>
        </div>
      </section>

      <!-- ===== 常用功能（设计稿 func-row） ===== -->
      <div class="section-title">常用功能</div>
      <div class="func-row">
        <div
          v-for="item in rankedEntries"
          :key="item.entry.key"
          class="func-tile glass interactive"
          :class="{ 'func-tile--pinned': item.pinned }"
          :title="item.entry.label"
          tabindex="0"
          @click="clickEntry(item.entry)"
          @keydown.enter.prevent="clickEntry(item.entry)"
        >
          <span class="func-tile__icon" v-html="lucideIconSvg(item.entry.icon, 16)" />
          <span class="func-tile__label">{{ item.entry.label }}</span>
          <span
            class="func-tile__pin"
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

      <!-- ===== 核心看板（设计稿 board-row 5 列） ===== -->
      <div class="section-title">核心看板</div>
      <div class="board-row">
        <div
          v-for="b in boardCards"
          :key="b.title"
          class="board-card glass interactive"
          tabindex="0"
          @click="goBoard(b)"
          @keydown.enter.prevent="goBoard(b)"
        >
          <div class="h">
            <div class="t"><span :class="['dot', b.dot]" aria-hidden="true" />{{ b.title }}</div>
            <div v-if="b.status" :class="['status', { on: b.dot === 'on' }]">{{ b.status }}</div>
          </div>
          <div class="body">
            {{ b.body }}
            <strong v-if="b.sub">{{ b.sub }}</strong>
          </div>
        </div>
      </div>

      <!-- ===== 底部两列：待办 + 最近活动（设计稿 bottom-row） ===== -->
      <div class="bottom-row">
        <div class="glass todo-list">
          <div class="section-title" style="margin: 0; margin-bottom: 8px">
            待办
            <div class="side-action">
              <button title="添加" aria-label="新建待办" @click="openTodoDialog">
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
              <button title="更多" aria-label="查看全部待办" @click="gotoTodoPage">
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
          <div v-if="homeStore.recentTodos.length === 0" class="todo-empty">暂无待处理待办</div>
          <template v-else>
            <div
              v-for="todo in homeStore.recentTodos"
              :key="todo.id"
              class="todo-item"
              :class="{ done: todo.status === 'Completed' }"
              role="checkbox"
              :aria-checked="todo.status === 'Completed'"
              tabindex="0"
              @click="handleTodoToggle(todo)"
              @keydown.enter.prevent="handleTodoToggle(todo)"
            >
              <div class="cb">
                <svg
                  v-if="todo.status === 'Completed'"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  stroke-width="3"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                  aria-hidden="true"
                >
                  <polyline points="20 6 9 17 4 12" />
                </svg>
              </div>
              <div class="txt">{{ todo.title }}</div>
            </div>
            <div v-if="homeStore.todoPendingTotal > homeStore.recentTodos.length" class="view-all" @click="gotoTodoPage">
              查看全部 {{ homeStore.todoPendingTotal }} 条 →
            </div>
          </template>
        </div>

        <div class="glass activity-list">
          <div class="section-title" style="margin: 0; margin-bottom: 8px">最近活动</div>
          <div v-if="homeStore.recentActivities.length === 0" class="activity-empty">暂无最近活动</div>
          <div
            v-for="a in homeStore.recentActivities"
            :key="a.id"
            class="activity-item"
          >
            <div class="icon"><span :class="['activity-dot', `activity-dot--${a.type}`]" aria-hidden="true" /></div>
            <div class="meta">
              <div class="t">{{ a.description }}</div>
              <div class="s">{{ formatRelativeTime(a.timestamp) }}</div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- 首页快速添加待办弹窗（内联原生 modal） -->
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
   HomeView — 首页（按设计稿 HomeScreen.jsx + kit-styles.css 重构）
   三区块流式布局：Hero 玻璃卡 / 常用功能平铺 / 5 列看板 / 底部两列
   颜色一律引用设计稿 token（--ink-* / --gold-* / --paper-* / --fg-* / --border-*），
   禁止裸色值（DESIGN.md 门禁 check-style-tokens）。
   宿主全局注入 .glass / .glass-strong 与设计稿 token，插件直接复用。
   ============================ */

.home-view {
  position: relative;
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow: hidden;
}

.home-content {
  position: relative;
  z-index: 1;
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  scrollbar-width: thin;
  padding: 24px 28px 32px;
  max-width: 1600px;
  width: 100%;
  margin: 0 auto;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

/* 滚动容器子区块不可被 flex 压缩（防裁字，历史教训 033 补丁） */
.home-content > * {
  flex-shrink: 0;
}

/* ========== 版块标题（kit-styles.css:247） ========== */
.section-title {
  font-family: var(--font-serif-cn, serif);
  font-size: 13px;
  font-weight: 500;
  color: color-mix(in srgb, var(--fg-primary) 70%, transparent);
  margin: 10px 0 10px;
  letter-spacing: 0.04em;
  display: flex;
  align-items: center;
  gap: 8px;
}

.section-title .side-action {
  margin-left: auto;
  display: flex;
  align-items: center;
  gap: 6px;
}

.section-title .side-action button {
  width: 22px;
  height: 22px;
  background: transparent;
  border: 0;
  cursor: pointer;
  color: var(--fg-tertiary);
  border-radius: 4px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  transition: background 0.16s, color 0.16s;
}

.section-title .side-action button:hover {
  background: var(--paper-tint);
  color: var(--fg-primary);
}

.section-title .side-action svg {
  width: 13px;
  height: 13px;
}

/* ========== Hero 问候卡（kit-styles.css:158） ========== */
.hero-card {
  padding: 20px 24px;
  margin-bottom: 8px;
  position: relative;
  overflow: hidden;
}

.hero-card .greet {
  display: flex;
  align-items: baseline;
  gap: 12px;
  margin-bottom: 4px;
}

.hero-card .glyph {
  width: 28px;
  height: 28px;
  background: linear-gradient(135deg, var(--gold-400), var(--gold-500));
  border-radius: 6px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-family: var(--font-display, serif);
  color: var(--ink-900);
  font-weight: 900;
  font-size: 15px;
  box-shadow: 0 4px 12px color-mix(in srgb, var(--gold-400) 35%, transparent);
  align-self: center;
}

.hero-card h1 {
  margin: 0;
  font-family: var(--font-display, serif);
  font-size: 22px;
  font-weight: 700;
  letter-spacing: 0.05em;
  color: var(--fg-primary);
}

.hero-card .sub {
  font-size: 12px;
  color: color-mix(in srgb, var(--fg-primary) 55%, transparent);
  margin-bottom: 14px;
  font-family: var(--font-sans-cn, sans-serif);
  letter-spacing: 0.02em;
}

.hero-card .sub .num {
  color: var(--gold-400);
  font-family: var(--font-mono, monospace);
  font-weight: 500;
}

/* 对话输入框（kit-styles.css:195） */
.composer {
  display: flex;
  align-items: center;
  gap: 10px;
  background: color-mix(in srgb, var(--ink-950) 38%, transparent);
  border: 1px solid var(--border-subtle);
  border-radius: 10px;
  padding: 8px 8px 8px 14px;
  transition: border-color 0.16s;
}

.composer:hover {
  border-color: var(--border-default);
}

.composer:focus-within {
  border-color: var(--border-brand);
  box-shadow: 0 0 0 3px color-mix(in srgb, var(--gold-400) 12%, transparent);
}

.composer input {
  flex: 1;
  min-width: 0;
  background: transparent;
  border: 0;
  outline: none;
  font-family: var(--font-sans-cn, sans-serif);
  font-size: 13.5px;
  color: var(--fg-primary);
}

.composer input::placeholder {
  color: var(--fg-tertiary);
}

.composer .send {
  width: 34px;
  height: 34px;
  background: linear-gradient(135deg, var(--gold-400), var(--gold-500));
  border: 0;
  border-radius: 7px;
  color: var(--ink-900);
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  box-shadow: 0 3px 10px color-mix(in srgb, var(--gold-400) 28%, transparent);
  transition: all 0.16s;
}

.composer .send:hover {
  transform: translateY(-1px);
  box-shadow: 0 5px 14px color-mix(in srgb, var(--gold-400) 40%, transparent);
}

.composer .send svg {
  width: 16px;
  height: 16px;
}

/* 建议 chips（kit-styles.css:229） */
.suggest-row {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 12px;
}

.suggest-chip {
  padding: 5px 12px;
  background: var(--paper-tint-weak);
  border: 1px solid var(--border-subtle);
  border-radius: 20px;
  font-size: 12px;
  color: color-mix(in srgb, var(--fg-primary) 75%, transparent);
  cursor: pointer;
  transition: all 0.16s;
  font-family: var(--font-sans-cn, sans-serif);
}

.suggest-chip:hover {
  background: var(--gold-tint);
  border-color: var(--border-brand);
  color: var(--gold-300);
}

/* ========== 常用功能（kit-styles.css:270） ========== */
.func-row {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  margin-bottom: 10px;
}

.func-tile {
  min-width: 108px;
  padding: 12px 14px;
  display: inline-flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
  font-size: 13px;
  color: color-mix(in srgb, var(--fg-primary) 85%, transparent);
  position: relative;
  transition: color 0.16s, box-shadow 0.16s;
}

.func-tile__icon {
  display: inline-flex;
  color: var(--gold-400);
  flex: none;
}

.func-tile__icon svg {
  width: 16px;
  height: 16px;
}

.func-tile:hover {
  color: var(--fg-primary);
}

/* 已固定入口：金左条微光（既有功能保留，hover 显形图钉） */
.func-tile--pinned {
  box-shadow: inset 2px 0 0 var(--gold-400), 0 0 14px color-mix(in srgb, var(--gold-400) 10%, transparent);
}

.func-tile__pin {
  position: absolute;
  right: 4px;
  top: 4px;
  width: 18px;
  height: 18px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
  color: var(--fg-tertiary);
  opacity: 0;
  transition: opacity 0.16s, color 0.16s, background 0.16s;
}

.func-tile__pin svg {
  width: 12px;
  height: 12px;
}

.func-tile:hover .func-tile__pin,
.func-tile__pin:focus-visible {
  opacity: 1;
}

.func-tile__pin:hover {
  color: var(--gold-400);
  background: var(--gold-tint);
}

.func-tile--pinned .func-tile__pin {
  opacity: 1;
  color: var(--gold-400);
}

/* ========== 核心看板（kit-styles.css:289） ========== */
.board-row {
  display: grid;
  grid-template-columns: repeat(5, 1fr);
  gap: 12px;
  margin-bottom: 10px;
}

.board-card {
  padding: 14px 16px;
  cursor: pointer;
  min-height: 88px;
  display: flex;
  flex-direction: column;
  transition: color 0.16s, box-shadow 0.16s;
}

.board-card .h {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 6px;
}

.board-card .h .t {
  font-family: var(--font-sans-cn, sans-serif);
  font-size: 13px;
  font-weight: 500;
  color: var(--fg-primary);
  display: flex;
  align-items: center;
  gap: 6px;
}

.board-card .h .t .dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: var(--gold-400);
  box-shadow: 0 0 6px currentColor;
  animation: pulse-dot 2s ease-in-out infinite;
}

.board-card .h .t .dot.off {
  background: color-mix(in srgb, var(--fg-primary) 30%, transparent);
  box-shadow: none;
  animation: none;
}

.board-card .h .t .dot.green {
  background: var(--bronze-500);
  box-shadow: none;
  animation: none;
}

.board-card .status {
  font-family: var(--font-mono, monospace);
  font-size: 10px;
  color: var(--fg-tertiary);
  letter-spacing: 0.04em;
}

.board-card .status.on {
  color: var(--gold-300);
}

.board-card .body {
  font-family: var(--font-serif-cn, serif);
  font-size: 12.5px;
  color: color-mix(in srgb, var(--fg-primary) 60%, transparent);
  margin-top: auto;
  padding-top: 8px;
}

.board-card .body strong {
  display: block;
  font-family: var(--font-sans-cn, sans-serif);
  font-size: 15px;
  color: var(--fg-primary);
  font-weight: 500;
  margin-top: 4px;
  letter-spacing: 0.02em;
}

@keyframes pulse-dot {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.4; }
}

/* ========== 底部两列（kit-styles.css:354） ========== */
.bottom-row {
  display: grid;
  grid-template-columns: 1.5fr 1fr;
  gap: 14px;
}

.todo-list,
.activity-list {
  padding: 14px 18px;
}

.todo-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 7px 0;
  font-size: 13px;
  color: color-mix(in srgb, var(--fg-primary) 80%, transparent);
  border-bottom: 1px dashed var(--border-hairline);
  cursor: pointer;
}

.todo-item:last-child {
  border-bottom: 0;
}

.todo-item .cb {
  width: 14px;
  height: 14px;
  border: 1.5px solid color-mix(in srgb, var(--fg-primary) 30%, transparent);
  border-radius: 3px;
  transition: all 0.16s;
  flex: none;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}

.todo-item .cb svg {
  width: 10px;
  height: 10px;
  color: var(--ink-900);
}

.todo-item .cb:hover {
  border-color: var(--gold-400);
}

.todo-item.done .cb {
  background: var(--gold-400);
  border-color: var(--gold-400);
}

.todo-item.done {
  opacity: 0.5;
  text-decoration: line-through;
}

.todo-item .txt {
  flex: 1;
  min-width: 0;
  word-break: break-all;
}

.todo-empty {
  padding: 14px 0;
  text-align: center;
  font-size: 12.5px;
  color: var(--fg-tertiary);
  font-family: var(--font-serif-cn, serif);
}

.todo-list .view-all {
  margin-top: 10px;
  text-align: right;
  font-size: 11px;
  color: var(--gold-300);
  cursor: pointer;
  font-family: var(--font-sans-cn, sans-serif);
}

.todo-list .view-all:hover {
  color: var(--gold-300);
  text-decoration: underline;
}

.activity-empty {
  padding: 28px 0;
  text-align: center;
  color: var(--fg-tertiary);
  font-family: var(--font-serif-cn, serif);
  font-size: 12.5px;
}

.activity-item {
  display: flex;
  gap: 10px;
  padding: 8px 0;
  border-bottom: 1px dashed var(--border-hairline);
}

.activity-item:last-child {
  border-bottom: 0;
}

.activity-item .icon {
  width: 26px;
  height: 26px;
  border-radius: 6px;
  background: var(--gold-tint-strong);
  color: var(--gold-400);
  flex: none;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}

.activity-item .meta {
  flex: 1;
  min-width: 0;
}

.activity-item .meta .t {
  font-size: 12.5px;
  color: var(--fg-primary);
}

.activity-item .meta .s {
  font-family: var(--font-mono, monospace);
  font-size: 10px;
  color: var(--fg-tertiary);
  margin-top: 2px;
}

.activity-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: var(--gold-400);
}

.activity-dot--workflow { background: var(--gold-400); }
.activity-dot--script { background: var(--bronze-500); }
.activity-dot--chat { background: var(--bronze-500); }
.activity-dot--system { background: var(--cinnabar-400); }

/* ========== 内联待办弹窗 ========== */
.todo-modal-mask {
  position: fixed;
  inset: 0;
  z-index: 1000;
  display: flex;
  align-items: center;
  justify-content: center;
  background: color-mix(in srgb, var(--ink-950) 45%, transparent);
}

.todo-modal {
  width: 420px;
  max-width: calc(100vw - 32px);
  background: var(--el-bg-color-overlay, var(--el-bg-color));
  border: 1px solid var(--el-border-color);
  border-radius: 12px;
  box-shadow: var(--shadow-lg, 0 12px 32px rgba(0, 0, 0, 0.55));
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
  border-radius: 4px;
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
  box-shadow: 0 0 0 3px color-mix(in srgb, var(--el-color-primary) 15%, transparent);
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

/* ========== 响应式：小屏单列 ========== */
@media (max-width: 1023px) {
  .home-content {
    padding: 16px;
    gap: 12px;
  }

  .board-row {
    grid-template-columns: repeat(2, 1fr);
  }

  .bottom-row {
    grid-template-columns: 1fr;
  }

  .hero-card h1 {
    font-size: 19px;
  }
}

@media (max-width: 639px) {
  .board-row {
    grid-template-columns: 1fr;
  }
}
</style>
