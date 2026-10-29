<script setup lang="ts">
/**
 * 仅 CT 画廊使用的宿主壳：把 AIAgent 插件聊天链路的 MessageItem 挂进宿主页面。
 *
 * 为什么需要它：
 *   1. 插件前端是独立包，CT 用例必须从宿主侧加载它（Playwright 的 @playwright/test 只在宿主
 *      node_modules 里，插件目录没有、也不该新增依赖）；
 *   2. MessageItem 的样式来自插件 widget.css/blocks.css，颜色与字号全部依赖 `.aiw-root`
 *      上定义的 `--aiw-*` 变量 —— 单独挂载会拿不到变量（气泡无色、字号回退），基线就不是真形态。
 *
 * 本文件只做「最小真宿主」：固定宽度 + 消息区底色 + 固定内边距，保证像素基线稳定。
 * 底色取 `--aiw-bg-soft`（就是 widget 里 `.aiw-body` 的底色）而不是纯白：
 * 正文气泡去边框后靠「白块压在浅灰底上」区分，若宿主也是纯白，基线里根本看不出气泡边界，
 * 像素回归就失去了对「气泡是否还在」的约束。
 * 不改插件组件源码；插件升级后这里无需改动（props 契约由插件自身测试把着）。
 */
import MessageItem from '../../../Plugins/AIAgent/web/src/agent-core/components/MessageItem.vue'
import '../../../Plugins/AIAgent/web/src/agent-core/styles/widget.css'
import '../../../Plugins/AIAgent/web/src/agent-core/styles/blocks.css'

defineProps<{
  /** 聊天消息（role/content/status/blocks/... 由插件契约定义，此处只透传） */
  message: Record<string, unknown>
  /** 组件配置（showBotAvatar 等） */
  config: Record<string, unknown>
  avatar?: string
}>()
</script>

<template>
  <div class="aiw-root aiw-story-host">
    <MessageItem :message="message" :config="config" :avatar="avatar" />
  </div>
</template>

<style scoped>
/* 画廊侧固定画布尺寸：不随视口变化，避免基线因窗口大小漂移 */
.aiw-story-host {
  width: 560px;
  padding: 16px;
  background: var(--aiw-bg-soft);
}
</style>
