<template>
  <aside class="sess">
    <!-- 分组：当前会话 -->
    <div class="sess__grp">
      <div class="sess__head">
        <svg class="sess__head-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="M21 11.5a8.38 8.38 0 0 1-.9 3.8 8.5 8.5 0 0 1-7.6 4.7 8.38 8.38 0 0 1-3.8-.9L3 21l1.9-5.7a8.38 8.38 0 0 1-.9-3.8 8.5 8.5 0 0 1 4.7-7.6 8.38 8.38 0 0 1 3.8-.9h.5a8.48 8.48 0 0 1 8 8v.5z" />
        </svg>
        <span class="sess__title">当前会话</span>
      </div>

      <div class="sess__name-row">
        <span class="sess__name" :title="sessionId">{{ sessionShort }}</span>
        <button type="button" class="sess__icon-btn" aria-label="新会话" @click="$emit('new-session')">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M12 20h9M16.5 3.5a2.12 2.12 0 0 1 3 3L7 19l-4 1 1-4 12.5-12.5z" />
          </svg>
        </button>
      </div>

      <div class="sess__stats">
        <div class="sess__stat">
          <span>消息数</span><span class="sess__stat-val">{{ messageCount }}</span>
        </div>
        <div class="sess__stat">
          <span>工具调用</span><span class="sess__stat-val">{{ toolCallCount }} 次</span>
        </div>
        <div class="sess__stat">
          <span>Token 使用</span><span class="sess__stat-val">{{ tokenText }}</span>
        </div>
      </div>
    </div>

    <!-- 分组：能力画像（取当前 Agent 的五维人格画像） -->
    <div class="sess__grp">
      <div class="sess__head">
        <svg class="sess__head-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <circle cx="12" cy="12" r="10" />
          <circle cx="12" cy="12" r="6" />
          <circle cx="12" cy="12" r="2" />
        </svg>
        <span class="sess__title">能力画像<template v-if="activeAgent?.name">&nbsp;· {{ activeAgent.name }}</template></span>
      </div>

      <template v-if="skillBars.length > 0">
        <div v-for="bar in skillBars" :key="bar.key" class="sess__skill">
          <div class="sess__skill-row">
            <span class="sess__skill-name">{{ bar.label }}</span>
            <span class="sess__skill-val">{{ bar.pct }}%</span>
          </div>
          <div class="sess__skill-track">
            <div class="sess__skill-fill" :style="{ width: bar.pct + '%' }"></div>
          </div>
        </div>
      </template>
      <!-- 后端未返回画像时如实留空，不编造等级与百分比 -->
      <EmptyHint v-else text="暂无画像数据" />
    </div>

    <!-- 分组：Agent 列表 -->
    <div class="sess__grp">
      <div class="sess__head">
        <svg class="sess__head-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <rect x="4" y="8" width="16" height="12" rx="2" />
          <path d="M12 8V4M8 4h8" />
          <path d="M9 13h.01M15 13h.01" />
        </svg>
        <span class="sess__title">Agent 列表</span>
      </div>

      <button
        v-for="agent in agents"
        :key="agent.id"
        type="button"
        class="sess__agent"
        :class="{ 'sess__agent--active': agent.id === activeAgentId }"
        :title="agent.description"
        @click="$emit('activate-agent', agent.id ?? '')"
      >
        <span class="sess__agent-dot"></span>
        <span class="sess__agent-name">{{ agent.avatar ? agent.avatar + ' ' : '' }}{{ agent.name }}</span>
        <span v-if="agent.id === activeAgentId" class="sess__agent-tag">当前</span>
      </button>
      <EmptyHint v-if="agents.length === 0" text="暂无 Agent" />
    </div>
  </aside>
</template>

<script setup lang="ts">
/**
 * 右栏「会话与统计」面板（对应设计原型 260px 侧栏）。
 *
 * 统计口径：消息数/工具调用数由父组件按真实消息列表传入；
 * 能力画像与 Agent 列表来自后端 GET /api/agents 的真实 AgentDefinition 数据。
 * Token 用量后端暂无记账接口，由父组件以占位符传入（如实，不编造）。
 */
import { computed } from 'vue'
import type { AgentDefinition } from '../types'
import EmptyHint from './EmptyHint.vue'

const props = defineProps<{
  /** 当前会话 id。 */
  sessionId: string
  /** 消息条数（真实统计）。 */
  messageCount: number
  /** 工具调用次数（真实统计）。 */
  toolCallCount: number
  /** token 用量文案（后端未返回统计时为「—」）。 */
  tokenText: string
  /** Agent 列表（来自 GET /api/agents，真实数据）。 */
  agents: AgentDefinition[]
  /** 当前激活 Agent 的 id。 */
  activeAgentId: string
}>()

defineEmits<{
  /** 请求新建会话。 */
  (e: 'new-session'): void
  /** 切换当前激活 Agent。 */
  (e: 'activate-agent', agentId: string): void
}>()

/** 会话 id 通常较长，界面只展示前 8 位，完整值放在 title 上。 */
const sessionShort = computed(() => {
  const id = props.sessionId ?? ''
  return id.length > 8 ? `${id.slice(0, 8)}…` : id || '—'
})

/** 当前激活 Agent 定义。 */
const activeAgent = computed(() => props.agents.find((a) => a.id === props.activeAgentId))

/** 五维能力画像条形（创造力/分析力/同理心/自信度/正式度，值域 0~1）。 */
const skillBars = computed(() => {
  const p = activeAgent.value?.personality
  if (!p) return []
  const dims: Array<{ key: string; label: string; value?: number }> = [
    { key: 'creativity', label: '创造力', value: p.creativity },
    { key: 'analytical', label: '分析力', value: p.analytical },
    { key: 'empathy', label: '同理心', value: p.empathy },
    { key: 'confidence', label: '自信度', value: p.confidence },
    { key: 'formality', label: '正式度', value: p.formality },
  ]
  return dims
    .filter((d) => typeof d.value === 'number' && Number.isFinite(d.value))
    .map((d) => {
      const clamped = Math.min(100, Math.max(0, Math.round((d.value as number) * 100)))
      return { key: d.key, label: d.label, pct: clamped }
    })
})
</script>

<style scoped>
.sess {
  width: 260px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  overflow-y: auto;
  background: var(--el-bg-color, #1d1e1f);
  border-left: 1px solid var(--el-border-color, #414243);
  scrollbar-width: thin;
}

.sess__grp {
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding: 12px;
  border-bottom: 1px solid var(--el-border-color, #414243);
}

.sess__head {
  display: flex;
  align-items: center;
  gap: 6px;
}

.sess__head-icon {
  width: 13px;
  height: 13px;
  color: var(--el-color-primary, #ffb84d);
}

.sess__title {
  font-size: var(--el-font-size-small, 13px);
  font-weight: var(--el-weight-semibold, 600);
  color: var(--el-text-color-primary, #e5eaf3);
}

.sess__name-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 6px 10px;
  background: var(--el-fill-color, #262727);
  border-radius: var(--el-border-radius-small, 4px);
}

.sess__name {
  font-size: var(--el-font-size-small, 13px);
  font-weight: var(--el-weight-medium, 500);
  font-family: var(--el-font-family-mono, monospace);
  color: var(--el-text-color-primary, #e5eaf3);
}

.sess__icon-btn {
  display: flex;
  align-items: center;
  padding: 2px;
  color: var(--el-text-color-secondary, #a3a6ad);
  background: transparent;
  border: none;
  cursor: pointer;
}

.sess__icon-btn svg {
  width: 12px;
  height: 12px;
}

.sess__stats {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.sess__stat {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 10px;
  font-size: var(--el-font-size-extra-small, 12px);
  color: var(--el-text-color-secondary, #a3a6ad);
}

.sess__stat-val {
  font-family: var(--el-font-family-mono, monospace);
  font-weight: var(--el-weight-medium, 500);
  color: var(--el-text-color-regular, #cfd3dc);
}

/* 能力画像条形 */
.sess__skill {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.sess__skill-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-size: var(--el-font-size-extra-small, 12px);
}

.sess__skill-name {
  color: var(--el-text-color-regular, #cfd3dc);
}

.sess__skill-val {
  color: var(--el-color-primary, #ffb84d);
  font-weight: var(--el-weight-medium, 500);
  font-family: var(--el-font-family-mono, monospace);
}

.sess__skill-track {
  height: 4px;
  background: var(--el-fill-color, #262727);
  border-radius: var(--el-border-radius-round, 999px);
  overflow: hidden;
}

.sess__skill-fill {
  height: 100%;
  background: var(--el-color-primary, #ffb84d);
  border-radius: var(--el-border-radius-round, 999px);
  transition: width var(--el-transition-duration, 0.2s);
}

/* Agent 列表项：当前项用主色淡底 + 左侧 2px 主色边框（仿设计原型） */
.sess__agent {
  display: flex;
  align-items: center;
  gap: 8px;
  width: 100%;
  padding: 8px 10px;
  border: none;
  border-left: 2px solid transparent;
  border-radius: 0 4px 4px 0;
  background: transparent;
  text-align: left;
  font: inherit;
  cursor: pointer;
}

.sess__agent:hover:not(.sess__agent--active) {
  background: var(--el-fill-color, #262727);
}

.sess__agent--active {
  background: var(--el-color-primary-light, rgba(255, 184, 77, 0.12));
  border-left-color: var(--el-color-primary, #ffb84d);
}

.sess__agent-dot {
  width: 6px;
  height: 6px;
  border-radius: 999px;
  background: var(--el-color-primary, #ffb84d);
}

.sess__agent-name {
  font-size: var(--el-font-size-small, 13px);
  font-weight: var(--el-weight-medium, 500);
  color: var(--el-color-primary, #ffb84d);
}

.sess__agent-tag {
  margin-left: auto;
  font-size: var(--el-font-size-extra-small, 12px);
  color: var(--el-color-primary, #ffb84d);
  opacity: 0.7;
}
</style>
