<script setup lang="ts">
import { onMounted } from 'vue'
import PageHead from '@/components/PageHead.vue'
import ForgeCard from '@/components/ForgeCard.vue'
import EmptyState from '@/components/EmptyState.vue'
import { useAgentStore } from '@/stores/agent'
import type { AgentDefinition } from '@/types/agent'

const store = useAgentStore()

onMounted(() => {
  store.loadAgents()
})

// 空态/错误态图标（currentColor，无裸色值；EmptyState 经 v-html 渲染）
const ICON_LOADER =
  '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 12a9 9 0 1 1-6.219-8.56"/></svg>'
const ICON_ALERT =
  '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3"/><path d="M12 9v4"/><path d="M12 17h.01"/></svg>'
const ICON_BOT =
  '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 8V4H8"/><rect width="16" height="12" x="4" y="8" rx="2"/><path d="M2 14h2"/><path d="M20 14h2"/><path d="M15 13v2"/><path d="M9 13v2"/></svg>'

function memoryCount(agent: AgentDefinition): number {
  // Derive a plausible memory count from available data
  if (agent.personality?.strengths?.length) {
    return agent.personality.strengths.length * 2
  }
  return 0
}

/** 后端 AgentType 枚举序列化为数字（0-5、99），统一映射为字符串名称，避免模板 .toLowerCase()/比较崩溃 */
const AGENT_TYPE_NAMES: Record<number, string> = {
  0: 'Coordinator',
  1: 'Researcher',
  2: 'Writer',
  3: 'Programmer',
  4: 'Analyst',
  5: 'Critic',
  99: 'Generalist',
}

function agentTypeName(type: number | string): string {
  if (typeof type === 'string') return type
  return AGENT_TYPE_NAMES[type] ?? 'Generalist'
}
</script>

<template>
  <div class="agents-manage-view">
    <!-- ===== 页头（设计稿 PageHead 统一规格） ===== -->
    <PageHead eyebrow="AI Agent · 智能代理" title="接入 · 调度 · 记忆">
      <template #actions>
        <span v-if="!store.loading" class="agent-count-badge">
          {{ store.agents.length }} 个 Agent
        </span>
        <button class="btn-create">
          <svg
            width="16"
            height="16"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
          >
            <line x1="12" y1="5" x2="12" y2="19" />
            <line x1="5" y1="12" x2="19" y2="12" />
          </svg>
          创建 Agent
        </button>
      </template>
    </PageHead>

    <!-- ===== 三态：加载 / 错误(可重试) / 空态 ===== -->
    <ForgeCard v-if="store.loading" :padding="20" class="state-card">
      <EmptyState :icon="ICON_LOADER" title="加载中" sub="正在拉取 Agent 列表…" />
    </ForgeCard>

    <ForgeCard v-else-if="store.error" :padding="20" class="state-card">
      <EmptyState :icon="ICON_ALERT" title="加载失败" :sub="store.error">
        <template #action>
          <button class="btn-retry" @click="store.loadAgents()">重试</button>
        </template>
      </EmptyState>
    </ForgeCard>

    <ForgeCard v-else-if="store.agents.length === 0" :padding="20" class="state-card">
      <EmptyState :icon="ICON_BOT" title="尚无 Agent" sub="点击右上角「创建 Agent」开始搭建" />
    </ForgeCard>

    <!-- ===== Agent 列表（玻璃卡栈） ===== -->
    <div v-else class="agent-list">
      <ForgeCard
        v-for="agent in store.agents"
        :key="agent.id"
        interactive
        :padding="16"
        class="agent-card"
        :class="{ 'agent-card--active': agent.isEnabled }"
      >
        <div class="agent-card-inner">
          <!-- Icon -->
          <div class="agent-icon" :class="`agent-icon--${agentTypeName(agent.type).toLowerCase()}`">
            <svg
              v-if="agentTypeName(agent.type) === 'Generalist' || agentTypeName(agent.type) === 'Coordinator'"
              width="20"
              height="20"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <path d="M12 8V4H8" /><rect
                width="16"
                height="12"
                x="4"
                y="8"
                rx="2"
              /><path d="M2 14h2" /><path d="M20 14h2" /><path d="M15 13v2" /><path d="M9 13v2" />
            </svg>
            <svg
              v-else-if="agentTypeName(agent.type) === 'Programmer'"
              width="20"
              height="20"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <polyline points="16 18 22 12 16 6" /><polyline points="8 6 2 12 8 18" />
            </svg>
            <svg
              v-else-if="agentTypeName(agent.type) === 'Analyst'"
              width="20"
              height="20"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <line x1="12" y1="20" x2="12" y2="10" /><line x1="18" y1="20" x2="18" y2="4" /><line x1="6" y1="20" x2="6" y2="16" />
            </svg>
            <svg
              v-else
              width="20"
              height="20"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <path d="M4.8 2.3A.3.3 0 1 0 5 2H4a2 2 0 0 0-2 2v5a6 6 0 0 0 6 6v0a6 6 0 0 0 6-6V4a2 2 0 0 0-2-2h-1a.2.2 0 1 0 .3.3" /><path d="M8 15v1a6 6 0 0 0 6 6v0a6 6 0 0 0 6-6v-4" /><circle cx="20" cy="10" r="2" />
            </svg>
          </div>

          <!-- Info -->
          <div class="agent-info">
            <div class="agent-info-top">
              <span class="agent-name">{{ agent.name }}</span>
              <span
                class="agent-tag"
                :class="agent.isBuiltin ? 'agent-tag--default' : 'agent-tag--type'"
              >{{ agent.isBuiltin ? '默认' : store.getAgentTypeLabel(agentTypeName(agent.type) as import('@/types/agent').AgentType) }}</span>
            </div>
            <p class="agent-desc">{{ agent.description }}</p>
            <div class="agent-meta">
              <span>模型: <span class="agent-meta-value">{{ agent.version || '-' }}</span></span>
              <span class="meta-sep">|</span>
              <span>技能: <span class="agent-meta-value">{{ agent.capabilities?.length || 0 }} 个</span></span>
              <span class="meta-sep">|</span>
              <span>MCP: <span class="agent-meta-value">{{ agent.tools?.length || 0 }} 服务器</span></span>
              <span class="meta-sep">|</span>
              <span>记忆: <span class="agent-meta-value">{{ memoryCount(agent) }} 条</span></span>
            </div>
          </div>

          <!-- Stats + Actions -->
          <div class="agent-actions">
            <div class="agent-stats">
              <span>今日对话: <span class="agent-stats-value">0</span></span>
              <span>工具调用: <span class="agent-stats-value">0</span></span>
            </div>
            <div class="agent-buttons">
              <button class="btn-config">配置</button>
              <button class="btn-chat">对话</button>
            </div>
          </div>
        </div>
      </ForgeCard>
    </div>
  </div>
</template>

<style scoped>
.agents-manage-view {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  padding: 24px;
}

/* ================================
   页头操作区（#actions）
   ================================ */
.agent-count-badge {
  display: inline-flex;
  align-items: center;
  padding: 2px 10px;
  font-size: 0.75rem;
  font-weight: 500;
  border-radius: var(--radius-md);
  background: var(--gold-tint-strong);
  color: var(--gold-400);
  border: 1px solid color-mix(in srgb, var(--gold-400) 40%, transparent);
  line-height: 1.5;
  font-family: var(--font-mono);
}

.btn-create {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 1rem;
  font-size: 0.8125rem;
  font-weight: 500;
  font-family: inherit;
  border: none;
  border-radius: var(--radius-md);
  cursor: pointer;
  white-space: nowrap;
  background: var(--el-color-primary);
  color: var(--el-color-white);
  transition:
    background var(--dur-fast) var(--ease-out),
    transform var(--dur-fast) var(--ease-out),
    box-shadow var(--dur-fast) var(--ease-out);
}

.btn-create:hover {
  background: var(--el-color-primary-light-3);
  transform: translateY(-1px);
}

.btn-create:active {
  transform: translateY(1px);
}

.btn-create svg {
  flex-shrink: 0;
}

/* ================================
   三态卡（加载 / 错误 / 空）
   ================================ */
.state-card {
  margin-top: 8px;
}

.btn-retry {
  display: inline-flex;
  align-items: center;
  padding: 6px 16px;
  font-size: 0.8125rem;
  font-weight: 500;
  font-family: inherit;
  cursor: pointer;
  border-radius: var(--radius-md);
  border: 1px solid color-mix(in srgb, var(--gold-400) 40%, transparent);
  background: var(--gold-tint-strong);
  color: var(--gold-400);
  transition:
    background var(--dur-fast) var(--ease-out),
    transform var(--dur-fast) var(--ease-out);
}

.btn-retry:hover {
  transform: translateY(-1px);
}

/* ================================
   Agent 列表（玻璃卡栈）
   ================================ */
.agent-list {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 12px;
  overflow-y: auto;
  margin-top: 4px;
}

/* 启用态：金色左条（玻璃卡边框由 ForgeCard 提供，此处仅覆盖左边条） */
.agent-card--active {
  border-left: 3px solid var(--gold-400);
}

.agent-card-inner {
  display: flex;
  align-items: center;
  gap: 1.25rem;
}

/* ================================
   Agent Icon
   ================================ */
.agent-icon {
  flex-shrink: 0;
  width: 40px;
  height: 40px;
  border-radius: var(--radius-lg);
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--el-color-primary);
  background: var(--gold-tint-strong);
}

.agent-icon--programmer {
  color: var(--el-color-info);
  background: color-mix(in srgb, var(--el-color-info) 15%, transparent);
}

.agent-icon--analyst {
  color: var(--el-color-primary);
  background: var(--gold-tint-strong);
}

.agent-icon--researcher {
  color: var(--bronze-500);
  background: color-mix(in srgb, var(--bronze-500) 15%, transparent);
}

.agent-icon--writer {
  color: var(--cinnabar-400);
  background: color-mix(in srgb, var(--cinnabar-400) 15%, transparent);
}

.agent-icon--critic {
  color: var(--el-color-warning);
  background: color-mix(in srgb, var(--el-color-warning) 15%, transparent);
}

/* ================================
   Agent Info (center)
   ================================ */
.agent-info {
  flex: 1;
  min-width: 0;
}

.agent-info-top {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.25rem;
}

.agent-name {
  font-size: 0.9375rem;
  font-weight: 600;
  color: var(--el-text-color-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.agent-tag {
  display: inline-flex;
  align-items: center;
  padding: 2px 6px;
  font-size: 0.75rem;
  font-weight: 500;
  border-radius: var(--radius-sm);
  flex-shrink: 0;
  line-height: 1.4;
}

.agent-tag--default {
  background: var(--gold-tint-strong);
  color: var(--gold-400);
}

.agent-tag--type {
  background: color-mix(in srgb, var(--bronze-500) 12%, transparent);
  color: var(--bronze-500);
}

.agent-desc {
  margin: 0 0 0.5rem;
  font-size: 0.8125rem;
  color: var(--el-text-color-regular);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.agent-meta {
  display: flex;
  align-items: center;
  gap: 1rem;
  font-size: 0.75rem;
  color: var(--el-text-color-secondary);
  font-family: var(--font-mono);
}

.agent-meta-value {
  color: var(--el-text-color-regular);
}

.meta-sep {
  color: var(--el-border-color);
}

/* ================================
   Agent Actions (right)
   ================================ */
.agent-actions {
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 0.75rem;
}

.agent-stats {
  display: flex;
  align-items: center;
  gap: 1rem;
  font-size: 0.75rem;
  font-family: var(--font-mono);
  color: var(--el-text-color-secondary);
}

.agent-stats-value {
  color: var(--el-text-color-primary);
}

.agent-buttons {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.btn-config {
  display: inline-flex;
  align-items: center;
  padding: 6px 12px;
  font-size: 0.75rem;
  font-weight: 500;
  font-family: inherit;
  border-radius: var(--radius-md);
  cursor: pointer;
  white-space: nowrap;
  transition:
    background var(--dur-fast) var(--ease-out),
    border-color var(--dur-fast) var(--ease-out),
    color var(--dur-fast) var(--ease-out),
    transform var(--dur-fast) var(--ease-out);
  color: var(--el-text-color-regular);
  border: 1px solid var(--el-border-color);
  background: transparent;
}

.btn-config:hover {
  border-color: var(--el-text-color-secondary);
  color: var(--el-text-color-primary);
  transform: translateY(-1px);
}

.btn-chat {
  display: inline-flex;
  align-items: center;
  padding: 6px 12px;
  font-size: 0.75rem;
  font-weight: 500;
  font-family: inherit;
  border-radius: var(--radius-md);
  cursor: pointer;
  white-space: nowrap;
  transition:
    background var(--dur-fast) var(--ease-out),
    border-color var(--dur-fast) var(--ease-out),
    transform var(--dur-fast) var(--ease-out);
  color: var(--el-color-primary);
  border: 1px solid color-mix(in srgb, var(--gold-400) 30%, transparent);
  background: transparent;
}

.btn-chat:hover {
  background: var(--gold-tint);
  border-color: color-mix(in srgb, var(--gold-400) 50%, transparent);
  transform: translateY(-1px);
}
</style>
