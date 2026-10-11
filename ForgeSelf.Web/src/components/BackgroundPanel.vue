<script setup lang="ts">
import BackgroundSettings from './BackgroundSettings.vue'

// 右上角背景设置浮层（薄壳）：
// 仅负责 fixed 定位 + 标题栏 + 关闭按钮；具体内容（纯色/预设/上传/URL/遮罩/恢复默认）
// 全部复用 BackgroundSettings.vue，避免与设置-外观页两处实现。
// 显隐由父组件 v-if 控制（TopNavbar.vue: bgPanelOpen）。
const emit = defineEmits<{ close: [] }>()
</script>

<template>
  <div class="bg-panel glass-strong">
    <div class="head">
      <div class="t">背景 · 铸匣外观</div>
      <button class="close" title="关闭" aria-label="关闭" @click="emit('close')">×</button>
    </div>
    <BackgroundSettings />
  </div>
</template>

<style scoped>
.bg-panel {
  position: fixed;
  top: 60px;
  right: 20px;
  width: 300px;
  padding: 16px;
  z-index: 1000;
  animation: slide-in var(--dur-slow) var(--ease-out);
}

@keyframes slide-in {
  from {
    opacity: 0;
    transform: translateY(-8px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 12px;
}

.head .t {
  font-family: var(--font-display);
  font-size: 14px;
  font-weight: 600;
  color: var(--fg-primary);
  letter-spacing: 0.05em;
}

.head .close {
  width: 22px;
  height: 22px;
  background: transparent;
  border: 0;
  cursor: pointer;
  color: var(--fg-tertiary);
  border-radius: 4px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}

.head .close:hover {
  background: var(--paper-tint);
  color: var(--fg-primary);
}
</style>
