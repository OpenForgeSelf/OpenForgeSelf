<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue'

// 设计稿 StatusBar（kit-styles.css:113-126 / App.jsx:47-55）：
// 22px 玻璃条、mono 10px、左金「● 匠人 Lv.1」+ 分隔项、最右时钟（每秒更新）
const now = ref(new Date())
let timer: ReturnType<typeof setInterval> | null = null

function tick(): void {
  now.value = new Date()
}

onMounted(() => {
  tick()
  timer = setInterval(tick, 1000)
})

onUnmounted(() => {
  if (timer) clearInterval(timer)
})

const clockText = (): string => now.value.toLocaleTimeString('zh-CN', {
  hour12: false,
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
})
</script>

<template>
  <footer class="status-bar shrink-0" aria-hidden="true">
    <span class="brand">● 匠人 Lv.1</span>
    <span>· REST + WS 已就绪</span>
    <span>· AI 离线</span>
    <span class="clock">{{ clockText() }}</span>
  </footer>
</template>

<style scoped>
.status-bar {
  height: 22px;
  flex: none;
  background: var(--ink-veil);
  backdrop-filter: blur(20px);
  -webkit-backdrop-filter: blur(20px);
  border-top: 1px solid var(--border-hairline);
  display: flex;
  align-items: center;
  gap: 14px;
  padding: 0 16px;
  font-family: var(--font-mono);
  font-size: 10px;
  color: var(--fg-tertiary);
  letter-spacing: 0.04em;
  z-index: 20;
}

.status-bar .brand {
  color: var(--el-color-primary);
}

.status-bar .clock {
  margin-left: auto;
}
</style>
