<script setup lang="ts">
/**
 * 任务中心（战役 2026-11-02-maf-runtime-hardening B3）：
 * 跨入口任务总览——开放任务会话 Run / 计划任务 AgentRun / 定时任务，统一列表、
 * 状态徽标、失败原因、重试/续跑入口；15s 静默轮询（复用既有 REST），后台运行任务与聊天视图解耦。
 * 设计对齐 DESIGN.md：PageHead / ForgeCard / EmptyState / token 引用禁裸色值。
 */
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import PageHead from '@/components/PageHead.vue'
import ForgeCard from '@/components/ForgeCard.vue'
import EmptyState from '@/components/EmptyState.vue'
import { useOpenPage } from '@/composables/useOpenPage'
import {
  fetchSchedulerLastFailure,
  fetchTaskCenterItems,
  resumePlannedRun,
  cancelRun,
  runSchedulerNow,
} from '@/services/taskCenterApi'
import type { BucketErrors, TaskOverviewItem, TaskSource, TaskStatus } from '@/services/taskCenterApi'

const POLL_MS = 15_000

const { openPage } = useOpenPage()

// ===== 状态 =====
const items = ref<TaskOverviewItem[]>([])
const errors = ref<BucketErrors>({})
const loading = ref(false)
/** 首次加载完成后才允许空态/静默轮询接管 */
const loadedOnce = ref(false)
/** 全部来源都失败时的页面级错误（原文，不编原因） */
const fatalErrors = ref<string[]>([])
const lastUpdated = ref<Date | null>(null)
const sourceFilter = ref<'all' | TaskSource>('all')
const onlyIssues = ref(false)
/** 进行中的续跑/立即运行（防重复点击） */
const actingIds = ref<Set<string>>(new Set())
/** 定时任务失败原因缓存（taskId → 原文；按需拉取） */
const schedulerReasons = ref<Map<number, string>>(new Map())

let pollTimer: number | null = null
/** 防闪指纹：数据未变不重渲染（ui-ux-design 轮询防闪条目） */
let lastFingerprint = ''

const emptyIconSvg =
  '<svg xmlns="http://www.w3.org/2000/svg" width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="9 11 12 14 22 4"/><path d="M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11"/></svg>'
const errorIconSvg =
  '<svg xmlns="http://www.w3.org/2000/svg" width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><line x1="15" y1="9" x2="9" y2="15"/><line x1="9" y1="9" x2="15" y2="15"/></svg>'

// ===== 展示映射 =====
const SOURCE_LABELS: Record<TaskSource, string> = {
  open: '开放任务',
  planned: '计划任务',
  scheduler: '定时任务',
}

const STATUS_LABELS: Record<TaskStatus, string> = {
  running: '执行中',
  planning: '规划中',
  pending: '待执行',
  completed: '已完成',
  failed: '失败',
  stuck: '卡住',
  cancelled: '已取消',
  scheduled: '待触发',
  unknown: '无轨迹',
}

function sourceLabel(source: TaskSource): string {
  return SOURCE_LABELS[source]
}

function statusLabel(status: TaskStatus): string {
  return STATUS_LABELS[status]
}

function isIssue(item: TaskOverviewItem): boolean {
  return item.status === 'failed' || item.status === 'stuck'
}

/** 行内失败/卡住原因（scheduler 行回填按需拉取的日志原文） */
function reasonText(item: TaskOverviewItem): string {
  if (item.source === 'scheduler') {
    const fromLog = schedulerReasons.value.get(Number(item.sourceRef))
    return fromLog || item.failureReason || ''
  }
  return item.failureReason || ''
}

function pad(n: number): string {
  return n < 10 ? `0${n}` : String(n)
}

function fmtTime(iso?: string | null): string {
  if (!iso) return '—'
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return '—'
  return `${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
}

/** 行时间信息：开放/计划=起止；定时=上次/下次触发 */
function timeText(item: TaskOverviewItem): string {
  if (item.source === 'scheduler') {
    return `上次 ${fmtTime(item.startedAt)} · 下次 ${fmtTime(item.nextRunAt)}`
  }
  const start = fmtTime(item.startedAt)
  const end = item.endedAt ? fmtTime(item.endedAt) : '进行中'
  return `${start} → ${end}`
}

// ===== 数据加载 =====

/** 应用快照：指纹未变不替换列表（防闪），桶错误原文透出 */
function applySnapshot(snapshot: { items: TaskOverviewItem[]; errors: BucketErrors }): void {
  const errs = Object.values(snapshot.errors).filter(Boolean) as string[]
  fatalErrors.value = errs.length > 0 && snapshot.items.length === 0 ? errs : []
  errors.value = snapshot.errors
  const fingerprint = JSON.stringify([...snapshot.items].sort((a, b) => a.id.localeCompare(b.id)))
  if (fingerprint !== lastFingerprint) {
    lastFingerprint = fingerprint
    items.value = snapshot.items
    void fillSchedulerReasons(snapshot.items)
  }
  lastUpdated.value = new Date()
}

/** 定时任务失败行按需拉最近失败日志原文（并行、每轮 ≤5 条，失败静默回退计数文案） */
async function fillSchedulerReasons(list: TaskOverviewItem[]): Promise<void> {
  const targets = list
    .filter((t) => t.source === 'scheduler' && (t.failureCount ?? 0) > 0)
    .slice(0, 5)
  await Promise.all(
    targets.map(async (t) => {
      const taskId = Number(t.sourceRef)
      if (schedulerReasons.value.has(taskId)) return
      const reason = await fetchSchedulerLastFailure(taskId)
      if (reason) schedulerReasons.value.set(taskId, reason)
    }),
  )
}

/** 拉取三源；manual=true 时显示 loading（自动轮询静默） */
async function refresh(manual = false): Promise<void> {
  if (manual) loading.value = true
  try {
    const snapshot = await fetchTaskCenterItems()
    applySnapshot(snapshot)
  } catch (e) {
    // fetchTaskCenterItems 内部已按桶兜底；此处兜整函数级异常（原文透出）
    const msg = e instanceof Error ? e.message : String(e)
    fatalErrors.value = [msg]
  } finally {
    loadedOnce.value = true
    loading.value = false
  }
}

function tick(): void {
  // 后台标签页不轮询（切回前台由 visibilitychange 立即刷一次）
  if (document.hidden) return
  void refresh(false)
}

function onVisibilityChange(): void {
  if (!document.hidden) void refresh(false)
}

// ===== 交互与派生 =====

const filtered = computed(() => {
  let list = items.value
  if (sourceFilter.value !== 'all') {
    list = list.filter((t) => t.source === sourceFilter.value)
  }
  if (onlyIssues.value) {
    list = list.filter(isIssue)
  }
  return list
})

const issueCount = computed(() => items.value.filter(isIssue).length)

const bucketErrorLines = computed(() =>
  Object.entries(errors.value)
    .filter(([, msg]) => !!msg)
    .map(([source, msg]) => `${SOURCE_LABELS[source as TaskSource]}：${msg}`),
)

function onRowActivate(item: TaskOverviewItem): void {
  // 行主点击 = 去来源页处理；定时任务无独立配置页，不响应
  // T9 深链（PILOT-079 R2）：planned 带 runId 直达 Run 执行视图；open 带会话 id 直达对应会话
  const deepQuery: Record<string, string> =
    item.source === 'planned' ? { runId: item.sourceRef } : { session: item.sourceRef }
  openPage({ path: '/ai-agent', query: deepQuery }, '智能体')
}

function onOpenAgent(): void {
  openPage('/ai-agent', '智能体')
}

/** 续跑计划任务（POST resume，SSE 流读到终局事件为止） */
async function onResume(item: TaskOverviewItem): Promise<void> {
  if (!item.retryUrl || actingIds.value.has(item.id)) return
  actingIds.value.add(item.id)
  try {
    const outcome = await resumePlannedRun(item.retryUrl)
    if (outcome.ok) {
      ElMessage.success(outcome.message || '续跑完成')
    } else {
      ElMessage.error(outcome.message || '续跑失败')
    }
  } finally {
    actingIds.value.delete(item.id)
    void refresh(false)
  }
}

/** 终止任务（POST cancelUrl；破坏性动作先确认——取消/关弹窗不发起任何请求） */
async function onStop(item: TaskOverviewItem): Promise<void> {
  if (!item.cancelUrl || actingIds.value.has(item.id)) return
  try {
    await ElMessageBox.confirm(
      '终止后该任务不再执行，已产生的产物保留。确定终止？',
      '终止任务',
      { type: 'warning', confirmButtonText: '终止', cancelButtonText: '取消' }
    )
  } catch {
    return // 用户取消/关弹窗
  }
  actingIds.value.add(item.id)
  try {
    const outcome = await cancelRun(item.cancelUrl)
    if (outcome.ok) {
      ElMessage.success(outcome.message || '已终止')
    } else {
      ElMessage.error(outcome.message || '终止失败')
    }
  } finally {
    actingIds.value.delete(item.id)
    void refresh(false)
  }
}


/** 立即运行定时任务（POST run-now） */
async function onRunNow(item: TaskOverviewItem): Promise<void> {
  if (!item.runNowUrl || actingIds.value.has(item.id)) return
  actingIds.value.add(item.id)
  try {
    const outcome = await runSchedulerNow(item.runNowUrl)
    if (outcome.ok) {
      ElMessage.success(outcome.message || '已触发立即运行')
    } else {
      ElMessage.error(outcome.message || '触发失败')
    }
  } finally {
    actingIds.value.delete(item.id)
    void refresh(false)
  }
}

function isActing(item: TaskOverviewItem): boolean {
  return actingIds.value.has(item.id)
}

onMounted(() => {
  void refresh(true)
  pollTimer = window.setInterval(tick, POLL_MS)
  document.addEventListener('visibilitychange', onVisibilityChange)
})

onUnmounted(() => {
  if (pollTimer !== null) {
    window.clearInterval(pollTimer)
    pollTimer = null
  }
  document.removeEventListener('visibilitychange', onVisibilityChange)
})
</script>

<template>
  <div class="task-center">
    <PageHead eyebrow="Tasks · 任务中心" title="任务中心">
      <template #actions>
        <span class="tc-updated mono" v-if="lastUpdated">更新 {{ fmtTime(lastUpdated.toISOString()) }}</span>
        <button class="tc-btn" :disabled="loading" @click="refresh(true)">刷新</button>
      </template>
    </PageHead>

    <div class="tc-toolbar">
      <div class="tc-seg" role="group" aria-label="来源过滤">
        <button
          class="tc-seg-item"
          :class="{ active: sourceFilter === 'all' }"
          @click="sourceFilter = 'all'"
        >全部</button>
        <button
          v-for="s in (['open', 'planned', 'scheduler'] as TaskSource[])"
          :key="s"
          class="tc-seg-item"
          :class="{ active: sourceFilter === s }"
          @click="sourceFilter = s"
        >{{ sourceLabel(s) }}</button>
      </div>
      <label class="tc-toggle">
        <input v-model="onlyIssues" type="checkbox" />
        <span>仅看异常</span>
      </label>
      <span class="tc-stats mono">共 {{ items.length }} · 异常 {{ issueCount }}</span>
    </div>

    <!-- 页面级错误：全部来源都失败 -->
    <div class="tc-fatal" v-if="fatalErrors.length > 0 && !loading">
      <span class="tc-fatal-icon" v-html="errorIconSvg"></span>
      <div class="tc-fatal-body">
        <p class="tc-fatal-title">任务总览暂时不可用</p>
        <p class="tc-fatal-detail mono" v-for="(line, i) in fatalErrors" :key="i">{{ line }}</p>
      </div>
      <button class="tc-btn" :disabled="loading" @click="refresh(true)">重试</button>
    </div>

    <!-- 桶级部分失败：有数据但个别来源不可用 -->
    <div class="tc-warn mono" v-else-if="bucketErrorLines.length > 0">
      部分来源不可用 — {{ bucketErrorLines.join('；') }}
    </div>

    <ForgeCard :padding="0" class="tc-list-card">
      <div class="tc-list" role="list">
        <!-- 加载态（仅首次/手动刷新） -->
        <div class="tc-loading mono" v-if="loading && !loadedOnce">正在加载任务总览…</div>

        <template v-else>
          <component
            :is="item.source === 'scheduler' ? 'div' : 'button'"
            v-for="item in filtered"
            :key="item.id"
            class="tc-row"
            :class="{ interactive: item.source !== 'scheduler' }"
            role="listitem"
            @click="onRowActivate(item)"
          >
            <span class="tc-source mono" :data-source="item.source">{{ sourceLabel(item.source) }}</span>
            <span class="tc-title" :title="item.title">{{ item.title }}</span>
            <span class="tc-reason mono" v-if="isIssue(item) && reasonText(item)" :title="reasonText(item)">
              {{ reasonText(item) }}
            </span>
            <span class="tc-time mono">{{ timeText(item) }}</span>
            <span class="tc-badge" :data-status="item.status">{{ statusLabel(item.status) }}</span>
            <span class="tc-actions" @click.stop>
              <button
                v-if="item.retryable"
                class="tc-btn tc-btn-solid"
                :disabled="isActing(item)"
                @click="onResume(item)"
              >{{ isActing(item) ? '执行中…' : (item.status === 'stuck' ? '继续' : '重试') }}</button>
              <button
                v-if="item.cancelUrl"
                class="tc-btn"
                :disabled="isActing(item)"
                @click="onStop(item)"
              >{{ isActing(item) ? '处理中…' : '终止' }}</button>
              <button
                v-if="item.runNowUrl && item.status === 'scheduled'"
                class="tc-btn"
                :disabled="isActing(item)"
                @click="onRunNow(item)"
              >{{ isActing(item) ? '触发中…' : '立即运行' }}</button>
            </span>
          </component>

          <!-- 空态（区分「真的没任务」与「过滤后为空」） -->
          <EmptyState
            v-if="items.length === 0 && !loading"
            :icon="emptyIconSvg"
            title="暂无任务"
            sub="发起智能体会话、创建计划任务或定时任务后，这里会汇总展示它们的运行状态"
          >
            <template #action>
              <button class="tc-btn tc-btn-solid" @click="onOpenAgent">去发起任务</button>
            </template>
          </EmptyState>
          <div class="tc-empty-filter mono" v-else-if="filtered.length === 0 && items.length > 0">
            当前过滤条件下没有任务
          </div>
        </template>
      </div>
    </ForgeCard>
  </div>
</template>

<style scoped>
.task-center {
  display: flex;
  flex-direction: column;
  gap: 16px;
  max-width: 1080px;
  margin: 0 auto;
}

.tc-updated {
  color: var(--fg-tertiary);
  font-size: 12px;
  margin-right: 8px;
}

/* ---- 工具条 ---- */
.tc-toolbar {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
}

.tc-seg {
  display: inline-flex;
  border: 1px solid var(--border-default);
  border-radius: var(--el-border-radius-base);
  overflow: hidden;
  background: var(--paper-tint);
}

.tc-seg-item {
  border: none;
  background: transparent;
  color: var(--fg-secondary);
  font-size: 13px;
  padding: 4px 12px;
  cursor: pointer;
}

.tc-seg-item + .tc-seg-item {
  border-left: 1px solid var(--border-default);
}

.tc-seg-item.active {
  background: var(--gold-tint-strong);
  color: var(--gold-500);
  font-weight: 600;
}

.tc-toggle {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  color: var(--fg-secondary);
  font-size: 13px;
  cursor: pointer;
}

.tc-stats {
  margin-left: auto;
  color: var(--fg-tertiary);
  font-size: 12px;
}

/* ---- 页面级错误 / 桶级警告 ---- */
.tc-fatal {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 16px;
  border: 1px solid color-mix(in srgb, var(--cinnabar-400) 35%, transparent);
  border-radius: var(--glass-radius);
  background: color-mix(in srgb, var(--cinnabar-400) 12%, transparent);
  color: var(--cinnabar-400);
}

.tc-fatal-icon {
  display: inline-flex;
  flex-shrink: 0;
}

.tc-fatal-icon :deep(svg) {
  width: 24px;
  height: 24px;
}

.tc-fatal-title {
  margin: 0 0 4px;
  font-weight: 600;
}

.tc-fatal-detail {
  margin: 0;
  font-size: 12px;
  color: var(--cinnabar-400);
  word-break: break-all;
}

.tc-warn {
  padding: 8px 12px;
  border: 1px solid color-mix(in srgb, var(--el-color-warning) 35%, transparent);
  border-radius: var(--el-border-radius-base);
  background: color-mix(in srgb, var(--el-color-warning) 14%, transparent);
  color: var(--el-color-warning);
  font-size: 12px;
}

/* ---- 列表 ---- */
.tc-list-card {
  padding: 0;
}

.tc-list {
  display: flex;
  flex-direction: column;
}

.tc-loading {
  padding: 24px;
  text-align: center;
  color: var(--fg-tertiary);
  font-size: 13px;
}

.tc-row {
  display: flex;
  align-items: center;
  gap: 12px;
  width: 100%;
  padding: 12px 16px;
  border: none;
  border-bottom: 1px solid var(--border-subtle);
  background: transparent;
  color: var(--fg-primary);
  text-align: left;
  font-size: 13px;
}

.tc-row:last-child {
  border-bottom: none;
}

.tc-row.interactive {
  cursor: pointer;
}

.tc-row.interactive:hover {
  background: var(--paper-tint);
}

.tc-source {
  flex-shrink: 0;
  font-size: 12px;
  color: var(--fg-tertiary);
}

.tc-source[data-source='open'] {
  color: var(--el-color-info);
}

.tc-source[data-source='planned'] {
  color: var(--gold-500);
}

.tc-source[data-source='scheduler'] {
  color: var(--el-color-success);
}

.tc-title {
  flex: 1 1 auto;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-weight: 500;
}

.tc-reason {
  flex-shrink: 1;
  max-width: 30%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 12px;
  color: var(--cinnabar-400);
}

.tc-time {
  flex-shrink: 0;
  font-size: 12px;
  color: var(--fg-tertiary);
}

.tc-badge {
  flex-shrink: 0;
  font-size: 12px;
  padding: 2px 8px;
  border-radius: var(--el-border-radius-small);
  border: 1px solid var(--border-default);
  background: var(--paper-tint);
  color: var(--fg-secondary);
}

.tc-badge[data-status='running'] {
  color: var(--el-color-info);
  border-color: color-mix(in srgb, var(--el-color-info) 35%, transparent);
  background: color-mix(in srgb, var(--el-color-info) 14%, transparent);
}

.tc-badge[data-status='planning'],
.tc-badge[data-status='pending'],
.tc-badge[data-status='scheduled'] {
  color: var(--gold-500);
  border-color: var(--gold-300);
  background: var(--gold-tint-strong);
}

.tc-badge[data-status='completed'] {
  color: var(--el-color-success);
  border-color: color-mix(in srgb, var(--el-color-success) 35%, transparent);
  background: color-mix(in srgb, var(--el-color-success) 14%, transparent);
}

.tc-badge[data-status='failed'] {
  color: var(--cinnabar-400);
  border-color: color-mix(in srgb, var(--cinnabar-400) 35%, transparent);
  background: color-mix(in srgb, var(--cinnabar-400) 12%, transparent);
}

/* T15: stuck=awaiting-human, warning orange; failed stays red; pending group stays gold */
.tc-badge[data-status='stuck'] {
  color: var(--el-color-warning);
  border-color: color-mix(in srgb, var(--el-color-warning) 35%, transparent);
  background: color-mix(in srgb, var(--el-color-warning) 14%, transparent);
}


.tc-badge[data-status='cancelled'],
.tc-badge[data-status='unknown'] {
  color: var(--fg-tertiary);
}

.tc-actions {
  flex-shrink: 0;
  display: inline-flex;
  gap: 8px;
}

/* ---- 按钮（无胶囊 9999px、无裸色值） ---- */
.tc-btn {
  border: 1px solid var(--border-default);
  border-radius: var(--el-border-radius-base);
  background: var(--el-bg-color);
  color: var(--fg-primary);
  font-size: 13px;
  padding: 4px 12px;
  cursor: pointer;
}

.tc-btn:hover:not(:disabled) {
  border-color: var(--gold-400);
  color: var(--gold-500);
}

.tc-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.tc-btn-solid {
  background: var(--gold-tint-strong);
  border-color: var(--gold-300);
  color: var(--gold-500);
  font-weight: 600;
}

.tc-empty-filter {
  padding: 20px;
  text-align: center;
  color: var(--fg-tertiary);
  font-size: 13px;
}

@media (max-width: 720px) {
  .tc-time,
  .tc-reason {
    display: none;
  }
}
</style>