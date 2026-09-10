<template>
  <!-- 中栏步骤进度卡（029 计划驱动执行）：计划 → 逐步执行 → 完成/卡住。
       折叠设计：默认只露头部（目标 + 状态 + 进度），点击展开步骤明细（目标→产出）。 -->
  <section class="spc" :class="{ 'spc--collapsed': collapsed }">
    <header class="spc__head" @click="collapsed = !collapsed">
      <span class="spc__ico">
        <Clock v-if="status === 'pending'" :size="14" class="spc__ico-svg" />
        <Loading v-else-if="status === 'planning' || status === 'running'" :size="14" class="spc__ico-svg spc__ico-svg--spin" />
        <CircleCheck v-else-if="status === 'completed'" :size="14" class="spc__ico-svg" />
        <WarningFilled v-else-if="status === 'stuck'" :size="14" class="spc__ico-svg" />
        <CircleClose v-else :size="14" class="spc__ico-svg" />
      </span>
      <span class="spc__title">
        计划驱动执行
        <span v-if="plan?.goal" class="spc__goal">{{ plan.goal }}</span>
      </span>
      <span class="spc__badge" :class="`spc__badge--${status}`">{{ statusText }}</span>
      <span class="spc__count">{{ doneCount }}/{{ steps.length }}</span>
      <span class="spc__caret">{{ collapsed ? '▸' : '▾' }}</span>
    </header>

    <!-- 卡住/错误提示（不折叠时在步骤上方醒目展示） -->
    <div v-if="stuckReason" class="spc__stuck">
      <WarningFilled :size="12" class="spc__stuck-ico" />
      <span>{{ stuckReason }}</span>
    </div>
    <div v-if="error" class="spc__error">{{ error }}</div>

    <div v-if="!collapsed" class="spc__body">
      <!-- 无步骤（规划中）占位 -->
      <div v-if="steps.length === 0" class="spc__empty">
        <Loading :size="12" class="spc__ico-svg spc__ico-svg--spin" />
        {{ status === 'planning' ? '正在规划执行步骤…' : '暂无步骤' }}
      </div>

      <!-- 步骤明细（折叠：目标→产出） -->
      <details
        v-for="s in steps"
        :key="s.index"
        class="spc__step"
        :open="s.status === 'running' || s.status === 'stuck'"
      >
        <summary class="spc__step-sum">
          <span class="spc__step-ico" :class="`spc__step-ico--${s.status}`">
            <Clock v-if="s.status === 'pending'" :size="12" class="spc__ico-svg" />
            <Loading v-else-if="s.status === 'running'" :size="12" class="spc__ico-svg spc__ico-svg--spin" />
            <CircleCheck v-else-if="s.status === 'completed'" :size="12" class="spc__ico-svg" />
            <Right v-else-if="s.status === 'skipped'" :size="12" class="spc__ico-svg" />
            <WarningFilled v-else-if="s.status === 'stuck'" :size="12" class="spc__ico-svg" />
            <CircleClose v-else :size="12" class="spc__ico-svg" />
          </span>
          <span class="spc__step-idx">{{ s.index + 1 }}</span>
          <span class="spc__step-name">{{ s.name || `步骤 ${s.index + 1}` }}</span>
          <span class="spc__step-status" :class="`spc__step-status--${s.status}`">{{ stepStatusText(s.status) }}</span>
        </summary>
        <div class="spc__step-body">
          <p v-if="s.objective" class="spc__step-line">
            <span class="spc__step-label">目标</span>{{ s.objective }}
          </p>
          <p v-if="s.stuckReason" class="spc__step-line spc__step-line--stuck">
            <span class="spc__step-label">卡住</span>{{ s.stuckReason }}
          </p>
          <p v-if="s.output" class="spc__step-line">
            <span class="spc__step-label">产出</span>
            <span class="spc__step-output">{{ s.output }}</span>
          </p>
        </div>
      </details>
    </div>
  </section>
</template>

<script setup lang="ts">
/**
 * 中栏步骤进度卡（029 计划驱动执行，tasks.md T024）。
 *
 * 职责：把 Run 的执行进度以「计划 → 步骤 → 产出」的可折叠卡片呈现，
 * 与消息流并列。状态由父组件（AiAgentView）依据 SSE 事件增量维护后整体传入，
 * 本组件纯展示（折叠态内部持有）。
 *
 * 折叠约定：默认展开；头部点击切换。执行中/卡住步骤自动展开（:open 绑定）。
 */
import { computed, ref } from 'vue'
import {
  CircleCheck,
  CircleClose,
  Clock,
  Loading,
  Right,
  WarningFilled,
} from '@element-plus/icons-vue'
import type { AgentPlan, AgentRunStatus, AgentStepStatus, PlanStepView } from '../types'

const props = defineProps<{
  /** 执行计划（plan_created 到达后填充；null = 尚未产出）。 */
  plan: AgentPlan | null
  /** 步骤视图状态（由 SSE 事件增量维护）。 */
  steps: PlanStepView[]
  /** Run 状态（驱动头部徽标与整体样式）。 */
  status: AgentRunStatus
  /** 卡住原因（run_stuck）。 */
  stuckReason?: string
  /** 执行错误（error 事件）。 */
  error?: string
}>()

/** 折叠态（默认展开）。 */
const collapsed = ref(false)

/** 已完成（含跳过）步骤数，用于 x/y 进度。 */
const doneCount = computed(() =>
  props.steps.filter((s) => s.status === 'completed' || s.status === 'skipped').length,
)

/** Run 状态文案。 */
const statusText = computed(() => {
  switch (props.status) {
    case 'pending':
      return '等待'
    case 'planning':
      return '规划中'
    case 'running':
      return '执行中'
    case 'completed':
      return '已完成'
    case 'stuck':
      return '卡住'
    case 'failed':
      return '失败'
    case 'cancelled':
      return '已取消'
    default:
      return props.status
  }
})

/** 步骤状态文案。 */
function stepStatusText(s: AgentStepStatus): string {
  switch (s) {
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
      return s
  }
}
</script>

<style scoped>
/* 卡片：铸造主题描边 + 主色左缘（与消息气泡同语言） */
.spc {
  flex-shrink: 0;
  border: 1px solid var(--el-border-color-dark, #2b2b2c);
  border-left: 3px solid var(--el-color-primary, #ffb84d);
  border-radius: var(--el-border-radius-small, 4px);
  background: var(--el-fill-color, #262727);
  overflow: hidden;
}

.spc__head {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  cursor: pointer;
  user-select: none;
}

.spc__head:hover {
  background: var(--el-fill-color-light, #303132);
}

.spc__ico {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 20px;
  height: 20px;
  flex-shrink: 0;
  color: var(--el-color-primary, #ffb84d);
}

.spc__ico-svg {
  width: 14px;
  height: 14px;
}

.spc__ico-svg--spin {
  animation: spc-spin 1s linear infinite;
}

@keyframes spc-spin {
  to {
    transform: rotate(360deg);
  }
}

.spc__title {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: var(--el-font-size-small, 13px);
  font-weight: var(--el-weight-semibold, 600);
  color: var(--el-text-color-primary, #e5eaf3);
}

.spc__goal {
  margin-left: 6px;
  font-weight: normal;
  font-size: var(--el-font-size-extra-small, 12px);
  color: var(--el-text-color-secondary, #a3a6ad);
}

.spc__badge {
  margin-left: auto;
  padding: 1px 8px;
  border-radius: 999px;
  font-size: 11px;
  white-space: nowrap;
}

.spc__badge--pending {
  color: var(--el-text-color-secondary, #a3a6ad);
  background: var(--el-fill-color-dark, #2f3031);
}

.spc__badge--planning,
.spc__badge--running {
  color: var(--el-color-warning, #e6a23c);
  background: rgba(230, 162, 60, 0.12);
}

.spc__badge--completed {
  color: var(--el-color-success, #67c23a);
  background: rgba(103, 194, 58, 0.12);
}

.spc__badge--stuck {
  color: var(--el-color-danger, #f56c6c);
  background: rgba(245, 108, 108, 0.12);
}

.spc__badge--failed,
.spc__badge--cancelled {
  color: var(--el-text-color-secondary, #a3a6ad);
  background: var(--el-fill-color-dark, #2f3031);
}

.spc__count {
  font-family: var(--el-font-family-mono, monospace);
  font-size: var(--el-font-size-extra-small, 12px);
  color: var(--el-text-color-secondary, #a3a6ad);
  white-space: nowrap;
}

.spc__caret {
  color: var(--el-text-color-placeholder, #8c959f);
  font-size: 11px;
}

/* 卡住 / 错误提示条 */
.spc__stuck {
  display: flex;
  align-items: flex-start;
  gap: 6px;
  margin: 0 12px 8px;
  padding: 6px 8px;
  border-radius: var(--el-border-radius-small, 4px);
  background: rgba(245, 108, 108, 0.08);
  color: var(--el-color-danger, #f56c6c);
  font-size: var(--el-font-size-extra-small, 12px);
  line-height: 1.5;
}

.spc__stuck-ico {
  width: 12px;
  height: 12px;
  flex-shrink: 0;
  margin-top: 1px;
}

.spc__error {
  margin: 0 12px 8px;
  padding: 6px 8px;
  border-radius: var(--el-border-radius-small, 4px);
  background: rgba(245, 108, 108, 0.08);
  color: var(--el-color-danger, #f56c6c);
  font-size: var(--el-font-size-extra-small, 12px);
}

.spc__body {
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding: 0 12px 10px;
}

.spc__empty {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px;
  font-size: var(--el-font-size-extra-small, 12px);
  color: var(--el-text-color-placeholder, #8c959f);
}

/* 步骤明细行 */
.spc__step {
  border: 1px solid var(--el-border-color-dark, #2b2b2c);
  border-radius: var(--el-border-radius-small, 4px);
  background: var(--el-bg-color, #1d1e1f);
  overflow: hidden;
}

.spc__step-sum {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 10px;
  cursor: pointer;
  list-style: none;
  user-select: none;
}

.spc__step-sum::-webkit-details-marker {
  display: none;
}

.spc__step-ico {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 16px;
  height: 16px;
  flex-shrink: 0;
}

.spc__step-ico--pending {
  color: var(--el-text-color-placeholder, #8c959f);
}

.spc__step-ico--running {
  color: var(--el-color-warning, #e6a23c);
}

.spc__step-ico--completed {
  color: var(--el-color-success, #67c23a);
}

.spc__step-ico--skipped {
  color: var(--el-text-color-placeholder, #8c959f);
}

.spc__step-ico--stuck {
  color: var(--el-color-danger, #f56c6c);
}

.spc__step-ico--failed {
  color: var(--el-color-danger, #f56c6c);
}

.spc__step-idx {
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

.spc__step-name {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  flex: 1;
  font-size: var(--el-font-size-extra-small, 12px);
  color: var(--el-text-color-regular, #cfd3dc);
}

.spc__step-status {
  flex-shrink: 0;
  font-size: 11px;
}

.spc__step-status--pending {
  color: var(--el-text-color-placeholder, #8c959f);
}

.spc__step-status--running {
  color: var(--el-color-warning, #e6a23c);
}

.spc__step-status--completed {
  color: var(--el-color-success, #67c23a);
}

.spc__step-status--skipped {
  color: var(--el-text-color-placeholder, #8c959f);
}

.spc__step-status--stuck,
.spc__step-status--failed {
  color: var(--el-color-danger, #f56c6c);
}

.spc__step-body {
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding: 6px 10px;
  border-top: 1px solid var(--el-border-color-dark, #2b2b2c);
}

.spc__step-line {
  margin: 0;
  font-size: var(--el-font-size-extra-small, 12px);
  line-height: 1.6;
  color: var(--el-text-color-secondary, #a3a6ad);
  word-break: break-word;
}

.spc__step-line--stuck {
  color: var(--el-color-danger, #f56c6c);
}

.spc__step-label {
  display: inline-block;
  margin-right: 6px;
  font-weight: var(--el-weight-medium, 500);
  color: var(--el-color-primary, #ffb84d);
}

.spc__step-output {
  white-space: pre-wrap;
}
</style>
