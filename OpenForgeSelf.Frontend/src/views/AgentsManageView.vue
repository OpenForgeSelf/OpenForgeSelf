<template>
  <div class="agents-manage-view">
    <!-- Page Header -->
    <div class="page-header">
      <div class="page-header-left">
        <h1 class="page-title">Agent 管理</h1>
        <span v-if="!store.loading" class="agent-count-badge">
          {{ store.agents.length }} 个 Agent
        </span>
      </div>
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
    </div>

    <!-- Loading State -->
    <div v-if="store.loading" class="loading-state">
      <span>加载中...</span>
    </div>

    <!-- Error State -->
    <div v-else-if="store.error" class="error-state">
      <span>{{ store.error }}</span>
    </div>

    <!-- Agent Cards List -->
    <div v-else class="agent-list">
      <div
        v-for="agent in store.agents"
        :key="agent.id"
        class="agent-card"
        :class="{ 'agent-card--active': agent.isEnabled }"
      >
        <div class="agent-card-inner">
          <!-- Icon -->
          <div class="agent-icon" :class="`agent-icon--${agent.type.toLowerCase()}`">
            <svg
              v-if="agent.type === 'Generalist' || agent.type === 'Coordinator'"
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
              v-else-if="agent.type === 'Programmer'"
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
              v-else-if="agent.type === 'Analyst'"
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
              >{{ agent.isBuiltin ? '默认' : store.getAgentTypeLabel(agent.type) }}</span>
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
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted } from 'vue'
import { useAgentStore } from '@/stores/agent'
import type { AgentDefinition } from '@/types/agent'

const store = useAgentStore()

onMounted(() => {
  store.loadAgents()
})

function memoryCount(agent: AgentDefinition): number {
  // Derive a plausible memory count from available data
  if (agent.personality?.strengths?.length) {
    return agent.personality.strengths.length * 2
  }
  return 0
}
</script>

<style scoped>
.agents-manage-view {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  padding: var(--fs-space-8, 2rem) var(--fs-space-6, 1.5rem);
}

/* ================================
   Page Header
   ================================ */
.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: var(--fs-space-8, 2rem);
  flex-shrink: 0;
}

.page-header-left {
  display: flex;
  align-items: center;
  gap: var(--fs-space-3, 0.75rem);
}

.page-title {
  margin: 0;
  font-size: var(--fs-text-2xl, 1.75rem);
  font-weight: var(--fs-weight-bold, 700);
  color: var(--fs-color-text-primary, var(--text-primary));
  line-height: var(--fs-leading-tight, 1.2);
}

.agent-count-badge {
  display: inline-flex;
  align-items: center;
  padding: 2px 10px;
  font-size: var(--fs-text-xs, 0.75rem);
  font-weight: var(--fs-weight-medium, 500);
  border-radius: var(--fs-radius-md, 8px);
  background: var(--fs-color-primary-light, var(--primary-soft));
  color: var(--fs-color-primary, var(--primary-color));
  border: 1px solid rgba(245, 158, 11, 0.2);
  line-height: 1.5;
}

.btn-create {
  display: inline-flex;
  align-items: center;
  gap: var(--fs-space-2, 0.5rem);
  padding: var(--fs-space-2, 0.5rem) var(--fs-space-4, 1rem);
  font-size: var(--fs-text-sm, 0.8125rem);
  font-weight: var(--fs-weight-medium, 500);
  font-family: var(--fs-font-body, inherit);
  border: none;
  border-radius: var(--fs-radius-lg, 12px);
  cursor: pointer;
  white-space: nowrap;
  background: var(--fs-color-primary, var(--primary-color));
  color: var(--fs-color-text-inverse, var(--primary-contrast));
  transition:
    background var(--fs-transition-fast, 150ms ease);
}

.btn-create:hover {
  background: var(--fs-color-primary-hover, var(--primary-hover));
}

.btn-create svg {
  flex-shrink: 0;
}

/* ================================
   Loading / Error
   ================================ */
.loading-state,
.error-state {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: var(--fs-text-sm, 0.8125rem);
  color: var(--fs-color-text-tertiary, var(--text-muted));
}

/* ================================
   Agent List (vertical stack)
   ================================ */
.agent-list {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: var(--fs-space-3, 0.75rem);
  overflow-y: auto;
}

/* ================================
   Agent Card
   ================================ */
.agent-card {
  position: relative;
  border-radius: var(--fs-radius-xl, 12px);
  border: 1px solid var(--fs-color-border-default, var(--border-color));
  background: var(--fs-surface-card, var(--bg-secondary));
  transition:
    background var(--fs-transition-fast, 150ms ease),
    box-shadow var(--fs-transition-fast, 150ms ease);
  cursor: pointer;
}

.agent-card:hover {
  background: var(--fs-surface-card-hover, var(--bg-tertiary));
}

/* Active (enabled) state: amber left border + glow */
.agent-card--active {
  border-left: 3px solid var(--fs-color-primary, var(--primary-color));
  box-shadow: 0 0 16px rgba(245, 158, 11, 0.06);
}

.agent-card--active:hover {
  box-shadow: 0 0 20px rgba(245, 158, 11, 0.1);
}

.agent-card-inner {
  display: flex;
  align-items: center;
  gap: var(--fs-space-5, 1.25rem);
  padding: var(--fs-space-5, 1.25rem);
}

/* ================================
   Agent Icon
   ================================ */
.agent-icon {
  flex-shrink: 0;
  width: 40px;
  height: 40px;
  border-radius: var(--fs-radius-full, 9999px);
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--fs-color-primary, var(--primary-color));
  background: rgba(245, 158, 11, 0.15);
}

.agent-icon--programmer {
  color: var(--fs-color-accent, var(--info-color));
  background: rgba(88, 166, 255, 0.15);
}

.agent-icon--analyst {
  color: #BC8CFF;
  background: rgba(188, 140, 255, 0.15);
}

.agent-icon--researcher {
  color: var(--state-success, #3FB950);
  background: rgba(63, 185, 80, 0.15);
}

.agent-icon--writer {
  color: #F472B6;
  background: rgba(244, 114, 182, 0.15);
}

.agent-icon--critic {
  color: var(--state-warning, #D29922);
  background: rgba(210, 153, 34, 0.15);
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
  gap: var(--fs-space-2, 0.5rem);
  margin-bottom: var(--fs-space-1, 0.25rem);
}

.agent-name {
  font-size: var(--fs-text-base, 0.9375rem);
  font-weight: var(--fs-weight-semibold, 600);
  color: var(--fs-color-text-primary, var(--text-primary));
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.agent-tag {
  display: inline-flex;
  align-items: center;
  padding: 2px 6px;
  font-size: var(--fs-text-xs, 0.75rem);
  font-weight: var(--fs-weight-medium, 500);
  border-radius: var(--fs-radius-sm, 4px);
  flex-shrink: 0;
  line-height: 1.4;
}

.agent-tag--default {
  background: var(--fs-color-primary-light, var(--primary-soft));
  color: var(--fs-color-primary, var(--primary-color));
}

.agent-tag--type {
  background: rgba(63, 185, 80, 0.12);
  color: #3FB950;
}

.agent-desc {
  margin: 0 0 var(--fs-space-2, 0.5rem);
  font-size: var(--fs-text-sm, 0.8125rem);
  color: var(--fs-color-text-secondary, var(--text-secondary));
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.agent-meta {
  display: flex;
  align-items: center;
  gap: var(--fs-space-4, 1rem);
  font-size: var(--fs-text-xs, 0.75rem);
  color: var(--fs-color-text-tertiary, var(--text-muted));
  font-family: var(--fs-font-mono, monospace);
}

.agent-meta-value {
  color: var(--fs-color-text-secondary, var(--text-secondary));
}

.meta-sep {
  color: var(--fs-color-border-default, var(--border-color));
}

/* ================================
   Agent Actions (right)
   ================================ */
.agent-actions {
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: var(--fs-space-3, 0.75rem);
}

.agent-stats {
  display: flex;
  align-items: center;
  gap: var(--fs-space-4, 1rem);
  font-size: var(--fs-text-xs, 0.75rem);
  font-family: var(--fs-font-mono, monospace);
  color: var(--fs-color-text-tertiary, var(--text-muted));
}

.agent-stats-value {
  color: var(--fs-color-text-primary, var(--text-primary));
}

.agent-buttons {
  display: flex;
  align-items: center;
  gap: var(--fs-space-2, 0.5rem);
}

.btn-config {
  display: inline-flex;
  align-items: center;
  padding: 6px 12px;
  font-size: var(--fs-text-xs, 0.75rem);
  font-weight: var(--fs-weight-medium, 500);
  font-family: var(--fs-font-body, inherit);
  border-radius: var(--fs-radius-lg, 12px);
  cursor: pointer;
  white-space: nowrap;
  transition:
    background var(--fs-transition-fast, 150ms ease),
    border-color var(--fs-transition-fast, 150ms ease),
    color var(--fs-transition-fast, 150ms ease);
  color: var(--fs-color-text-secondary, var(--text-secondary));
  border: 1px solid var(--fs-color-border-default, var(--border-color));
  background: transparent;
}

.btn-config:hover {
  border-color: var(--fs-color-text-tertiary, var(--text-muted));
  color: var(--fs-color-text-primary, var(--text-primary));
}

.btn-chat {
  display: inline-flex;
  align-items: center;
  padding: 6px 12px;
  font-size: var(--fs-text-xs, 0.75rem);
  font-weight: var(--fs-weight-medium, 500);
  font-family: var(--fs-font-body, inherit);
  border-radius: var(--fs-radius-lg, 12px);
  cursor: pointer;
  white-space: nowrap;
  transition:
    background var(--fs-transition-fast, 150ms ease),
    border-color var(--fs-transition-fast, 150ms ease);
  color: var(--fs-color-primary, var(--primary-color));
  border: 1px solid rgba(245, 158, 11, 0.3);
  background: transparent;
}

.btn-chat:hover {
  background: var(--fs-color-primary-light, var(--primary-soft));
  border-color: rgba(245, 158, 11, 0.5);
}
</style>