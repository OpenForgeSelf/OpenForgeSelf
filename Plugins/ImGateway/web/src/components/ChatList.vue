<script setup lang="ts">
/**
 * 会话列表左栏条目（PILOT-078 FR-5）。
 * 纯展示组件：条目 = 通道头像 + 显示名 + 最后消息预览（单行省略）+ 相对时间 + 选中高亮。
 * 空态/错误条由父组件（ImGatewayView 会话视图）渲染——「去配置」引导要触发视图切换，属页面职责。
 * 样式铁律：插件是独立预编译产物，禁 <ElXxx>，一律原生 HTML + --el-* / design token（零裸色值）。
 */
import type { ConversationDto } from '../http'
import { avatarText, relativeTime } from './im-format'

defineProps<{
  conversations: ConversationDto[]
  /** 当前选中联系人的 ChannelKey。 */
  selectedKey: string
}>()

const emit = defineEmits<{ (e: 'select', channelKey: string): void }>()
</script>

<template>
  <ul class="ig-clist">
    <li v-for="c in conversations" :key="c.channelKey">
      <button
        type="button"
        class="ig-clist__item"
        :class="{ 'ig-clist__item--active': c.channelKey === selectedKey }"
        @click="emit('select', c.channelKey)"
      >
        <span class="ig-clist__avatar" aria-hidden="true">{{ avatarText(c.displayName) }}</span>
        <span class="ig-clist__body">
          <span class="ig-clist__row">
            <span class="ig-clist__name">{{ c.displayName }}</span>
            <span class="ig-clist__time">{{ relativeTime(c.lastActiveAt) }}</span>
          </span>
          <span class="ig-clist__preview">
            {{ c.lastMessage ? c.lastMessage.preview : '暂无消息记录' }}
          </span>
        </span>
      </button>
    </li>
  </ul>
</template>

<style>
/* 全 token：零裸色值（--el-* 宿主级联 + design token） */
.ig-clist {
  list-style: none;
  margin: 0;
  padding: 0;
}
.ig-clist__item {
  display: flex;
  align-items: center;
  gap: 10px;
  width: 100%;
  padding: 9px 10px;
  border: none;
  border-radius: var(--radius-md);
  background: transparent;
  color: var(--fg-primary);
  cursor: pointer;
  text-align: left;
  font-size: 13px;
  transition: background var(--dur-fast) var(--ease-out);
}
.ig-clist__item:hover {
  background: var(--el-fill-color);
}
.ig-clist__item--active,
.ig-clist__item--active:hover {
  background: var(--gold-tint);
}
.ig-clist__avatar {
  width: 34px;
  height: 34px;
  flex: none;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
  background: color-mix(in srgb, var(--bronze-500) 20%, transparent);
  color: var(--bronze-500);
  font-weight: 600;
  font-size: 14px;
}
.ig-clist__body {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.ig-clist__row {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 8px;
}
.ig-clist__name {
  font-weight: 600;
  color: var(--fg-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.ig-clist__time {
  flex: none;
  font-size: 11px;
  color: var(--fg-secondary);
  font-family: var(--font-mono, monospace);
}
.ig-clist__preview {
  font-size: 12px;
  color: var(--fg-secondary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
