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

    <!-- 分组：能力画像 -->
    <div class="sess__grp">
      <div class="sess__head">
        <svg class="sess__head-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <circle cx="12" cy="12" r="10" />
          <circle cx="12" cy="12" r="6" />
          <circle cx="12" cy="12" r="2" />
        </svg>
        <span class="sess__title">能力画像</span>
      </div>
      <!-- 后端暂无「能力画像」统计接口，如实留空，不编造等级与百分比 -->
      <EmptyHint text="后端暂无接口（待补）" />
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

      <!-- 当前 Agent：后端暂无多 Agent 接口，只展示当前这一个真实项 -->
      <div class="sess__agent sess__agent--active">
        <span class="sess__agent-dot"></span>
        <span class="sess__agent-name">默认助手</span>
        <span class="sess__agent-tag">当前</span>
      </div>
      <EmptyHint text="多 Agent 后端暂无接口（待补）" />
    </div>
  </aside>
</template>

<script setup lang="ts">
/**
 * 右栏「会话与统计」面板（对应设计原型 260px 侧栏）。
 *
 * 统计口径：消息数/工具调用数由父组件按真实消息列表传入；
 * 能力画像与多 Agent 列表因后端暂无接口，按约定如实留空，不编造数据。
 */
import { computed } from 'vue'
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
}>()

defineEmits<{
  /** 请求新建会话。 */
  (e: 'new-session'): void
}>()

/** 会话 id 通常较长，界面只展示前 8 位，完整值放在 title 上。 */
const sessionShort = computed(() => {
  const id = props.sessionId ?? ''
  return id.length > 8 ? `${id.slice(0, 8)}…` : id || '—'
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

/* Agent 列表项：当前项用主色淡底 + 左侧 2px 主色边框（仿设计原型） */
.sess__agent {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 10px;
  border-left: 2px solid transparent;
  border-radius: 0 4px 4px 0;
  cursor: pointer;
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
