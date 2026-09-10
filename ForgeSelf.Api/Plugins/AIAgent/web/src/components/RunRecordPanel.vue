<template>
  <!-- 执行记录面板（029 计划驱动执行，tasks.md T025）：
       Run 列表（状态徽标）+ Run 详情（步骤时间线、入参出参、卡住原因）+ 操作按钮。
       以 ElDialog 呈现（宿主共享桥已导出 ElDialog，插件模板显式 import 经 import map 解析）。 -->
  <ElDialog
    :model-value="visible"
    title="执行记录"
    width="820px"
    top="6vh"
    @update:model-value="(v) => $emit('update:visible', v)"
  >
    <div class="rrp">
      <!-- 左：Run 列表（当前会话，按时间倒序） -->
      <aside class="rrp__list">
        <div class="rrp__list-head">
          <span class="rrp__list-title">运行记录</span>
          <button type="button" class="rrp__reload" title="刷新" @click="reloadList()">
            <Refresh :size="12" class="rrp__ico" />
          </button>
        </div>
        <ElScrollbar class="rrp__list-body">
          <button
            v-for="r in runs"
            :key="r.id"
            type="button"
            class="rrp__run"
            :class="{ 'rrp__run--on': selected?.id === r.id }"
            @click="openRun(r.id)"
          >
            <span class="rrp__run-badge" :class="`rrp__run-badge--${runStatusName(r.status)}`">{{ runStatusText(r.status) }}</span>
            <span class="rrp__run-task">{{ r.taskInput }}</span>
            <span class="rrp__run-meta">
              {{ r.stepCount ?? 0 }} 步{{ r.totalTokens ? ` · ${r.totalTokens} tok` : '' }}
            </span>
          </button>
          <p v-if="runs.length === 0" class="rrp__empty">
            当前会话暂无执行记录。使用「计划驱动」Agent 发送任务后会自动生成。
          </p>
        </ElScrollbar>
      </aside>

      <!-- 右：Run 详情（步骤时间线 + 入参出参 + 卡住原因 + 操作） -->
      <main class="rrp__detail">
        <template v-if="selected">
          <div class="rrp__detail-head">
            <span class="rrp__detail-status" :class="`rrp__detail-status--${runStatusName(selected.status)}`">
              {{ runStatusText(selected.status) }}
            </span>
            <span class="rrp__detail-id">#{{ selected.id }}</span>
            <span class="rrp__detail-time">{{ fmtTime(selected.createTime) }}</span>
          </div>

          <!-- 卡住原因 / 失败原因 -->
          <div v-if="selected.stuckReason" class="rrp__stuck">
            <WarningFilled :size="12" class="rrp__ico" />
            <span>{{ selected.stuckReason }}</span>
          </div>

          <!-- 步骤时间线 -->
          <div class="rrp__steps">
            <div
              v-for="s in steps"
              :key="s.id"
              class="rrp__step"
              :class="{ 'rrp__step--current': s.stepIndex === selected.currentStepIndex }"
            >
              <div class="rrp__step-row">
                <span class="rrp__step-ico" :class="`rrp__step-ico--${stepStatusName(s.status)}`">
                  <Clock v-if="stepStatusName(s.status) === 'pending'" :size="12" class="rrp__ico" />
                  <Loading v-else-if="stepStatusName(s.status) === 'running'" :size="12" class="rrp__ico rrp__ico--spin" />
                  <CircleCheck v-else-if="stepStatusName(s.status) === 'completed'" :size="12" class="rrp__ico" />
                  <Right v-else-if="stepStatusName(s.status) === 'skipped'" :size="12" class="rrp__ico" />
                  <WarningFilled v-else-if="stepStatusName(s.status) === 'stuck'" :size="12" class="rrp__ico" />
                  <CircleClose v-else :size="12" class="rrp__ico" />
                </span>
                <span class="rrp__step-idx">{{ s.stepIndex + 1 }}</span>
                <span class="rrp__step-name">{{ s.name || `步骤 ${s.stepIndex + 1}` }}</span>
                <span class="rrp__step-status" :class="`rrp__step-status--${stepStatusName(s.status)}`">{{ stepStatusText(s.status) }}</span>
                <span v-if="s.durationMs != null" class="rrp__step-dur">{{ (s.durationMs / 1000).toFixed(1) }}s</span>
              </div>
              <!-- 展开详情：目标 / 入参 / 出参 / 卡住原因 / 人工介入 -->
              <div v-if="stepDetailVisible(s)" class="rrp__step-detail">
                <p v-if="s.objective" class="rrp__line"><span class="rrp__label">目标</span>{{ s.objective }}</p>
                <p v-if="s.stuckReason" class="rrp__line rrp__line--stuck">
                  <span class="rrp__label">卡住</span>{{ s.stuckReason }}
                </p>
                <p v-if="s.humanNote" class="rrp__line rrp__line--human">
                  <span class="rrp__label">批注</span>{{ s.humanNote }}
                </p>
                <p v-if="s.humanOverride" class="rrp__line rrp__line--human">
                  <span class="rrp__label">补位</span>{{ s.humanOverride }}
                </p>
                <pre v-if="s.inputJson" class="rrp__pre"><span class="rrp__label">入参</span>{{ s.inputJson }}</pre>
                <pre v-if="s.outputJson" class="rrp__pre"><span class="rrp__label">出参</span>{{ s.outputJson }}</pre>
                <p v-if="s.retryCount" class="rrp__line rrp__line--meta">重试 {{ s.retryCount }} 次</p>
              </div>
            </div>
          </div>
          <p v-if="steps.length === 0" class="rrp__empty">该 Run 暂无步骤明细。</p>

          <!-- 操作按钮：继续 / 重开 / 跳过 / 补位 / 取消 -->
          <div class="rrp__actions">
            <ElButton size="small" :disabled="!canResume" :loading="acting === 'resume'" @click="onResume">
              <RefreshRight :size="12" class="rrp__ico" />继续
            </ElButton>
            <ElButton size="small" :loading="acting === 'restart'" @click="onRestart">
              <Refresh :size="12" class="rrp__ico" />重开
            </ElButton>
            <ElButton size="small" :disabled="!canIntervene" :loading="acting === 'skip'" @click="onSkip">
              <Right :size="12" class="rrp__ico" />跳过
            </ElButton>
            <ElButton size="small" :disabled="!canIntervene" :loading="acting === 'override'" @click="onOverride">
              <EditPen :size="12" class="rrp__ico" />补位
            </ElButton>
            <span class="rrp__actions-spacer"></span>
            <ElButton size="small" :disabled="!canCancel" :loading="acting === 'cancel'" @click="onCancel">
              <CircleClose :size="12" class="rrp__ico" />取消
            </ElButton>
          </div>
        </template>
        <div v-else class="rrp__empty rrp__empty--center">从左侧选择一条运行记录查看详情</div>
      </main>
    </div>
  </ElDialog>
</template>

<script setup lang="ts">
/**
 * 执行记录面板（029，tasks.md T025）。
 *
 * 数据：全部来自 AgentRunsController 真实 API（零 mock）：
 * - 列表：GET /api/ai-agent/runs?sessionId=…（打开/刷新时加载）
 * - 详情：GET /api/ai-agent/runs/{id}（点击 Run 加载，含步骤明细）
 * - 操作：resume（继续）/ restart（重开）/ cancel（取消）/ PATCH steps/{index}（跳过/补位）
 *
 * 状态流转：resume 走 SSE 流式（复用 AiAgentView 的计划驱动流），本面板只负责
 * 记录查询与人工介入；补位用 ElMessageBox.prompt 输入产出文本。
 */
import { computed, ref, watch } from 'vue'
import { ElButton, ElDialog, ElMessage, ElMessageBox, ElScrollbar } from 'element-plus'
import {
  CircleCheck,
  CircleClose,
  Clock,
  EditPen,
  Loading,
  Refresh,
  RefreshRight,
  Right,
  WarningFilled,
} from '@element-plus/icons-vue'
import {
  cancelRun,
  getRunDetail,
  interveneRun,
  listRuns,
  restartRun,
  type AgentRunDto,
  type AgentStepRunDto,
} from '../http'
import { runStatusName, stepStatusName } from '../types'

const props = defineProps<{
  /** 面板是否打开。 */
  visible: boolean
  /** 当前聊天会话 id（Run 列表按会话过滤）。 */
  sessionId: string
}>()

const emit = defineEmits<{
  (e: 'update:visible', v: boolean): void
  /** 用户点击「继续」：父组件以该 Run 起 SSE 流（复用计划驱动流）。 */
  (e: 'resume', run: AgentRunDto): void
}>()

/** Run 列表。 */
const runs = ref<AgentRunDto[]>([])
/** 当前选中的 Run。 */
const selected = ref<AgentRunDto | null>(null)
/** 当前选中 Run 的步骤明细。 */
const steps = ref<AgentStepRunDto[]>([])
/** 正在执行的操作（用于按钮 loading 与互斥）。 */
const acting = ref<'resume' | 'restart' | 'skip' | 'override' | 'cancel' | ''>('')
const loadingList = ref(false)

/* ---- 打开/关闭时刷新 ---- */
watch(
  () => props.visible,
  (v) => {
    if (v) void reloadList()
  },
)

/** 加载 Run 列表（当前会话，取最近 50 条）。 */
async function reloadList() {
  loadingList.value = true
  try {
    const data = await listRuns(props.sessionId || undefined, undefined, 1, 50)
    runs.value = data?.items ?? []
    // 若当前选中项仍在列表则保留，否则清空选中
    if (selected.value && !runs.value.some((r) => r.id === selected.value?.id)) {
      selected.value = null
      steps.value = []
    }
  } catch {
    runs.value = []
  } finally {
    loadingList.value = false
  }
}

/** 点击 Run：加载详情（含步骤）。 */
async function openRun(id: number) {
  selected.value = runs.value.find((r) => r.id === id) ?? null
  steps.value = []
  try {
    const data = await getRunDetail(id)
    if (data?.run) selected.value = data.run
    steps.value = data?.steps ?? []
  } catch (e) {
    ElMessage.error(`加载 Run 详情失败：${e instanceof Error ? e.message : String(e)}`)
  }
}

/* ---- 状态派生（按钮可用性） ---- */
const canResume = computed(
  () => !!selected.value && (runStatusName(selected.value.status) === 'stuck' || runStatusName(selected.value.status) === 'failed'),
)
const canIntervene = computed(
  () => !!selected.value && (runStatusName(selected.value.status) === 'stuck' || runStatusName(selected.value.status) === 'running'),
)
const canCancel = computed(
  () =>
    !!selected.value &&
    !['completed', 'failed', 'cancelled'].includes(runStatusName(selected.value.status)),
)

/* ---- 操作 ---- */
/** 继续：emit 给父组件起 SSE 流（resume 端点）。 */
function onResume() {
  if (!selected.value) return
  emit('resume', selected.value)
  emit('update:visible', false)
}

/** 重开：以同 Plan 新建 Run 从头执行。 */
async function onRestart() {
  if (!selected.value) return
  acting.value = 'restart'
  try {
    const res = await restartRun(selected.value.id)
    if (res?.success) {
      ElMessage.success('已重开，新 Run 已创建')
      emit('update:visible', false)
    } else {
      ElMessage.error('重开失败')
    }
  } catch (e) {
    ElMessage.error(`重开失败：${e instanceof Error ? e.message : String(e)}`)
  } finally {
    acting.value = ''
  }
}

/** 跳过当前卡住/待执行步骤。 */
async function onSkip() {
  const run = selected.value
  if (!run) return
  const idx = run.currentStepIndex ?? steps.value.find((s) => stepStatusName(s.status) === 'stuck')?.stepIndex ?? 0
  try {
    await ElMessageBox.confirm(
      `确定跳过步骤 ${idx + 1} 吗？该步骤标记为「已跳过」，Run 继续推进。`,
      '跳过确认',
      { type: 'warning', confirmButtonText: '跳过', cancelButtonText: '取消' },
    )
  } catch {
    return // 用户取消
  }
  acting.value = 'skip'
  try {
    const res = await interveneRun(run.id, idx, { action: 'skip' })
    ElMessage.success(`已跳过步骤 ${idx + 1}`)
    await openRun(run.id)
  } catch (e) {
    ElMessage.error(`跳过失败：${e instanceof Error ? e.message : String(e)}`)
  } finally {
    acting.value = ''
  }
}

/** 补位：人工产出覆盖当前步骤（跳过 LLM）。 */
async function onOverride() {
  const run = selected.value
  if (!run) return
  const idx = run.currentStepIndex ?? steps.value.find((s) => stepStatusName(s.status) === 'stuck')?.stepIndex ?? 0
  let output = ''
  try {
    const res = await ElMessageBox.prompt(
      '输入人工补位产出（将直接作为该步骤结果并继续执行）：',
      `补位步骤 ${idx + 1}`,
      {
        inputType: 'textarea',
        inputPlaceholder: '人工产出内容…',
        confirmButtonText: '补位',
        cancelButtonText: '取消',
        inputValidator: (v: string) => (v && v.trim() ? true : '产出不能为空'),
      },
    )
    output = res.value
  } catch {
    return // 用户取消
  }
  acting.value = 'override'
  try {
    await interveneRun(run.id, idx, { action: 'override', output })
    ElMessage.success(`步骤 ${idx + 1} 已补位`)
    await openRun(run.id)
  } catch (e) {
    ElMessage.error(`补位失败：${e instanceof Error ? e.message : String(e)}`)
  } finally {
    acting.value = ''
  }
}

/** 取消 Run。 */
async function onCancel() {
  const run = selected.value
  if (!run) return
  try {
    await ElMessageBox.confirm('确定取消该 Run 吗？', '取消确认', {
      type: 'warning',
      confirmButtonText: '取消 Run',
      cancelButtonText: '再想想',
    })
  } catch {
    return
  }
  acting.value = 'cancel'
  try {
    const res = await cancelRun(run.id)
    if (res?.success) {
      ElMessage.success('Run 已取消')
      await openRun(run.id)
    } else {
      ElMessage.error('取消失败')
    }
  } catch (e) {
    ElMessage.error(`取消失败：${e instanceof Error ? e.message : String(e)}`)
  } finally {
    acting.value = ''
  }
}

/* ---- 展示辅助 ---- */
function runStatusText(code: number): string {
  switch (runStatusName(code)) {
    case 'pending':
      return '等待'
    case 'planning':
      return '规划中'
    case 'running':
      return '运行中'
    case 'completed':
      return '完成'
    case 'stuck':
      return '卡住'
    case 'failed':
      return '失败'
    case 'cancelled':
      return '已取消'
    default:
      return runStatusName(code)
  }
}

function stepStatusText(code: number): string {
  switch (stepStatusName(code)) {
    case 'pending':
      return '待执行'
    case 'running':
      return '执行中'
    case 'completed':
      return '完成'
    case 'skipped':
      return '已跳过'
    case 'stuck':
      return '卡住'
    case 'failed':
      return '失败'
    default:
      return stepStatusName(code)
  }
}

/** 步骤是否有可展开明细。 */
function stepDetailVisible(s: AgentStepRunDto): boolean {
  return !!(
    s.objective ||
    s.stuckReason ||
    s.humanNote ||
    s.humanOverride ||
    s.inputJson ||
    s.outputJson
  )
}

function fmtTime(t?: string): string {
  if (!t) return ''
  const d = new Date(t)
  if (Number.isNaN(d.getTime())) return t
  return d.toLocaleString()
}
</script>

<style scoped>
.rrp {
  display: flex;
  gap: 16px;
  height: 520px;
  max-height: 70vh;
}

/* 左：Run 列表 */
.rrp__list {
  display: flex;
  flex-direction: column;
  width: 260px;
  flex-shrink: 0;
  border-right: 1px solid var(--el-border-color-lighter, #2a2b2c);
  padding-right: 12px;
  min-height: 0;
}

.rrp__list-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 8px;
  flex-shrink: 0;
}

.rrp__list-title {
  font-size: var(--el-font-size-small, 13px);
  font-weight: var(--el-weight-semibold, 600);
  color: var(--el-text-color-regular, #cfd3dc);
}

.rrp__reload {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
  border: none;
  border-radius: var(--el-border-radius-small, 4px);
  background: transparent;
  color: var(--el-text-color-secondary, #a3a6ad);
  cursor: pointer;
}

.rrp__reload:hover {
  background: var(--el-fill-color-light, #303132);
  color: var(--el-text-color-primary, #e5eaf3);
}

.rrp__list-body {
  flex: 1;
  min-height: 0;
}

.rrp__run {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 3px;
  width: 100%;
  padding: 8px 10px;
  margin-bottom: 6px;
  border: 1px solid var(--el-border-color-dark, #2b2b2c);
  border-radius: var(--el-border-radius-small, 4px);
  background: var(--el-bg-color, #1d1e1f);
  text-align: left;
  cursor: pointer;
}

.rrp__run:hover {
  border-color: var(--el-color-primary, #ffb84d);
}

.rrp__run--on {
  border-color: var(--el-color-primary, #ffb84d);
  background: var(--el-color-primary-light-9, rgba(255, 184, 77, 0.1));
}

.rrp__run-badge {
  padding: 0 6px;
  border-radius: 999px;
  font-size: 11px;
}

.rrp__run-badge--running,
.rrp__run-badge--planning {
  color: var(--el-color-warning, #e6a23c);
  background: rgba(230, 162, 60, 0.12);
}

.rrp__run-badge--completed {
  color: var(--el-color-success, #67c23a);
  background: rgba(103, 194, 58, 0.12);
}

.rrp__run-badge--stuck,
.rrp__run-badge--failed {
  color: var(--el-color-danger, #f56c6c);
  background: rgba(245, 108, 108, 0.12);
}

.rrp__run-badge--cancelled,
.rrp__run-badge--pending {
  color: var(--el-text-color-secondary, #a3a6ad);
  background: var(--el-fill-color-dark, #2f3031);
}

.rrp__run-task {
  width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: var(--el-font-size-extra-small, 12px);
  color: var(--el-text-color-primary, #e5eaf3);
}

.rrp__run-meta {
  font-size: 11px;
  color: var(--el-text-color-placeholder, #8c959f);
}

/* 右：详情 */
.rrp__detail {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-width: 0;
  min-height: 0;
}

.rrp__detail-head {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 10px;
  flex-shrink: 0;
}

.rrp__detail-status {
  padding: 1px 8px;
  border-radius: 999px;
  font-size: 12px;
}

.rrp__detail-status--running,
.rrp__detail-status--planning {
  color: var(--el-color-warning, #e6a23c);
  background: rgba(230, 162, 60, 0.12);
}

.rrp__detail-status--completed {
  color: var(--el-color-success, #67c23a);
  background: rgba(103, 194, 58, 0.12);
}

.rrp__detail-status--stuck,
.rrp__detail-status--failed {
  color: var(--el-color-danger, #f56c6c);
  background: rgba(245, 108, 108, 0.12);
}

.rrp__detail-status--cancelled {
  color: var(--el-text-color-secondary, #a3a6ad);
  background: var(--el-fill-color-dark, #2f3031);
}

.rrp__detail-id {
  font-family: var(--el-font-family-mono, monospace);
  font-size: var(--el-font-size-extra-small, 12px);
  color: var(--el-text-color-secondary, #a3a6ad);
}

.rrp__detail-time {
  margin-left: auto;
  font-size: 11px;
  color: var(--el-text-color-placeholder, #8c959f);
}

.rrp__stuck {
  display: flex;
  align-items: flex-start;
  gap: 6px;
  margin-bottom: 10px;
  padding: 6px 8px;
  border-radius: var(--el-border-radius-small, 4px);
  background: rgba(245, 108, 108, 0.08);
  color: var(--el-color-danger, #f56c6c);
  font-size: var(--el-font-size-extra-small, 12px);
  line-height: 1.5;
}

/* 步骤时间线 */
.rrp__steps {
  flex: 1;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 6px;
  min-height: 0;
  padding-right: 4px;
}

.rrp__step {
  border: 1px solid var(--el-border-color-dark, #2b2b2c);
  border-left: 2px solid var(--el-border-color, #414243);
  border-radius: var(--el-border-radius-small, 4px);
  background: var(--el-bg-color, #1d1e1f);
  overflow: hidden;
}

.rrp__step--current {
  border-left-color: var(--el-color-primary, #ffb84d);
}

.rrp__step-row {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 10px;
}

.rrp__step-ico {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 16px;
  height: 16px;
  flex-shrink: 0;
}

.rrp__step-ico--pending {
  color: var(--el-text-color-placeholder, #8c959f);
}

.rrp__step-ico--running {
  color: var(--el-color-warning, #e6a23c);
}

.rrp__step-ico--completed {
  color: var(--el-color-success, #67c23a);
}

.rrp__step-ico--skipped {
  color: var(--el-text-color-placeholder, #8c959f);
}

.rrp__step-ico--stuck,
.rrp__step-ico--failed {
  color: var(--el-color-danger, #f56c6c);
}

.rrp__ico {
  width: 12px;
  height: 12px;
  flex-shrink: 0;
}

.rrp__ico--spin {
  animation: rrp-spin 1s linear infinite;
}

@keyframes rrp-spin {
  to {
    transform: rotate(360deg);
  }
}

.rrp__step-idx {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 18px;
  height: 18px;
  flex-shrink: 0;
  border-radius: 999px;
  background: var(--el-fill-color-dark, #2f3031);
  font-family: var(--el-font-family-mono, monospace);
  font-size: 11px;
  color: var(--el-text-color-secondary, #a3a6ad);
}

.rrp__step-name {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  flex: 1;
  font-size: var(--el-font-size-extra-small, 12px);
  color: var(--el-text-color-regular, #cfd3dc);
}

.rrp__step-status {
  flex-shrink: 0;
  font-size: 11px;
}

.rrp__step-status--pending {
  color: var(--el-text-color-placeholder, #8c959f);
}

.rrp__step-status--running {
  color: var(--el-color-warning, #e6a23c);
}

.rrp__step-status--completed {
  color: var(--el-color-success, #67c23a);
}

.rrp__step-status--skipped {
  color: var(--el-text-color-placeholder, #8c959f);
}

.rrp__step-status--stuck,
.rrp__step-status--failed {
  color: var(--el-color-danger, #f56c6c);
}

.rrp__step-dur {
  flex-shrink: 0;
  font-family: var(--el-font-family-mono, monospace);
  font-size: 11px;
  color: var(--el-text-color-placeholder, #8c959f);
}

.rrp__step-detail {
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding: 6px 10px;
  border-top: 1px solid var(--el-border-color-dark, #2b2b2c);
}

.rrp__line {
  margin: 0;
  font-size: var(--el-font-size-extra-small, 12px);
  line-height: 1.6;
  color: var(--el-text-color-secondary, #a3a6ad);
  word-break: break-word;
}

.rrp__line--stuck {
  color: var(--el-color-danger, #f56c6c);
}

.rrp__line--human {
  color: var(--el-color-primary-light-7, #fde68a);
}

.rrp__line--meta {
  color: var(--el-text-color-placeholder, #8c959f);
}

.rrp__pre {
  margin: 0;
  max-height: 140px;
  overflow-y: auto;
  font-size: var(--el-font-size-extra-small, 12px);
  font-family: var(--el-font-family-mono, monospace);
  line-height: 1.5;
  color: var(--el-text-color-secondary, #a3a6ad);
  white-space: pre-wrap;
  word-break: break-word;
}

.rrp__label {
  display: inline-block;
  margin-right: 6px;
  font-weight: var(--el-weight-medium, 500);
  color: var(--el-color-primary, #ffb84d);
}

/* 操作按钮区 */
.rrp__actions {
  display: flex;
  align-items: center;
  gap: 8px;
  padding-top: 12px;
  border-top: 1px solid var(--el-border-color-lighter, #2a2b2c);
  margin-top: 12px;
  flex-shrink: 0;
}

.rrp__actions-spacer {
  flex: 1;
}

.rrp__empty {
  margin: 0;
  padding: 12px;
  font-size: var(--el-font-size-extra-small, 12px);
  line-height: 1.6;
  color: var(--el-text-color-placeholder, #8c959f);
}

.rrp__empty--center {
  text-align: center;
  padding: 80px 0;
}
</style>
