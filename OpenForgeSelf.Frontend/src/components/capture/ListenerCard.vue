<script setup lang="ts">
import { VideoPause, VideoPlay, Edit, Delete } from '@element-plus/icons-vue'
import type { ListenerConfig } from '@/types/capture'

/**
 * 监听器卡片：展示配置信息与运行状态，提供启停/编辑/删除操作。
 */

defineProps<{
  listener: ListenerConfig
}>()

const emit = defineEmits<{
  toggle: [listener: ListenerConfig]
  edit: [listener: ListenerConfig]
  delete: [listener: ListenerConfig]
}>()

// 目标展示：未配置则「仅抓包」
function targetText(listener: ListenerConfig): string {
  if (!listener.targetHost) return '仅抓包，不转发'
  return `${listener.targetHost}:${listener.targetPort}`
}
</script>

<template>
  <ElCard shadow="never" class="listener-card !mb-3" body-class="!p-3">
    <div class="flex items-start justify-between gap-2">
      <div class="min-w-0">
        <div class="flex items-center gap-2">
          <span class="!text-sm !font-semibold text-[var(--el-text-color-primary)] truncate">{{ listener.name }}</span>
          <ElTag :type="listener.isRunning ? 'success' : 'info'" size="small" effect="light">
            {{ listener.isRunning ? '运行中' : '已停止' }}
          </ElTag>
        </div>
        <p v-if="listener.description" class="!mt-1 !mb-1 !text-xs text-[var(--el-text-color-secondary)] truncate">
          {{ listener.description }}
        </p>
      </div>
      <ElButton
        :type="listener.isRunning ? 'warning' : 'success'"
        size="small"
        :icon="listener.isRunning ? VideoPause : VideoPlay"
        circle
        :title="listener.isRunning ? '停止监听' : '启动监听'"
        @click="emit('toggle', listener)"
      />
    </div>

    <div class="listener-meta !mt-2">
      <div class="listener-meta-item">
        <span class="listener-meta-label">监听</span>
        <span class="listener-meta-value">{{ listener.listenAddress }}:{{ listener.listenPort }}</span>
      </div>
      <div class="listener-meta-item">
        <span class="listener-meta-label">目标</span>
        <span class="listener-meta-value" :class="{ 'listener-meta-muted': !listener.targetHost }">
          {{ targetText(listener) }}
        </span>
      </div>
    </div>

    <div class="flex justify-end gap-2 !mt-2">
      <ElButton size="small" :icon="Edit" @click="emit('edit', listener)">编辑</ElButton>
      <ElButton
        size="small"
        type="danger"
        plain
        :icon="Delete"
        @click="emit('delete', listener)"
      >
        删除
      </ElButton>
    </div>
  </ElCard>
</template>

<style scoped>
.listener-meta {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.listener-meta-item {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12px;
}
.listener-meta-label {
  flex-shrink: 0;
  color: var(--el-text-color-secondary);
  width: 32px;
}
.listener-meta-value {
  color: var(--el-text-color-regular);
  font-family: var(--el-font-family-mono);
  word-break: break-all;
}
.listener-meta-muted {
  color: var(--el-text-color-placeholder);
  font-family: var(--el-font-family);
}
</style>
