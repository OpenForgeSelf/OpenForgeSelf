<script setup>
/**
 * 单个内容块的错误边界。
 *
 * 为什么必须有：一个块渲染抛错会让整个消息消失，而且 Vue 只往控制台打一行，
 * 页面上什么都看不到——排查成本极高（这个组件就是被一次真实事故逼出来的：
 * BlockRenderer 拿不到 config 时只有图表块抛错，其它块全部正常，表现为
 * 「图表神秘空白」，查了 5 轮）。
 *
 * 现在任何块抛错都会就地降级成一条可见的错误提示，并且不影响同消息内的其它块。
 */
import { ref, onErrorCaptured } from 'vue'

const props = defineProps({
  label: { type: String, default: '内容块' },
})

const failed = ref(false)
const errMsg = ref('')

onErrorCaptured((err) => {
  failed.value = true
  errMsg.value = (err && err.message) || String(err)
  console.warn(`[ai-assistant] ${props.label} 渲染失败：`, err)
  return false // 阻止继续向上冒泡，保住同消息里的其它块
})

function retry() {
  failed.value = false
  errMsg.value = ''
}
</script>

<template>
  <div v-if="failed" class="aiw-error">
    <span>{{ label }}渲染失败：{{ errMsg }}</span>
    <button type="button" @click="retry">重试</button>
  </div>
  <slot v-else />
</template>
