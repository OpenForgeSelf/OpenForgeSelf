<script setup lang="ts">
import { toasts, dismissToast } from '../toast'
</script>

<template>
  <Teleport to="body">
    <div class="toast-host" aria-live="polite" aria-atomic="false">
      <TransitionGroup name="toast">
        <div
          v-for="t in toasts"
          :key="t.id"
          class="toast"
          :class="`toast-${t.type}`"
          role="alert"
        >
          <span class="toast-msg">{{ t.message }}</span>
          <button class="toast-close" type="button" aria-label="关闭" @click="dismissToast(t.id)">
            ×
          </button>
        </div>
      </TransitionGroup>
    </div>
  </Teleport>
</template>

<style scoped>
.toast-host {
  position: fixed;
  top: 16px;
  right: 16px;
  z-index: 2000;
  display: flex;
  flex-direction: column;
  gap: 10px;
  max-width: min(360px, calc(100vw - 32px));
  pointer-events: none;
}

.toast {
  display: flex;
  align-items: flex-start;
  gap: 10px;
  padding: 10px 12px;
  border-radius: 8px;
  border: 1px solid var(--el-border-color);
  background: var(--el-bg-color-overlay);
  color: var(--el-text-color-primary);
  font-size: 14px;
  line-height: 1.4;
  box-shadow: 0 6px 20px rgba(0, 0, 0, 0.12);
  pointer-events: auto;
}

.toast-msg {
  flex: 1;
  word-break: break-word;
}

.toast-close {
  flex-shrink: 0;
  width: 20px;
  height: 20px;
  border: none;
  background: transparent;
  color: var(--el-text-color-secondary);
  font-size: 16px;
  line-height: 1;
  cursor: pointer;
  border-radius: 4px;
}

.toast-close:hover {
  background: var(--el-fill-color);
  color: var(--el-text-color-primary);
}

/* 类型配色：仅用官方 --el-* + color-mix 派生，不写死 hex */
.toast-success {
  border-color: color-mix(in srgb, var(--el-color-success) 40%, transparent);
  background: color-mix(in srgb, var(--el-color-success) 12%, var(--el-bg-color-overlay));
}

.toast-error {
  border-color: color-mix(in srgb, var(--el-color-danger) 40%, transparent);
  background: color-mix(in srgb, var(--el-color-danger) 12%, var(--el-bg-color-overlay));
}

.toast-warning {
  border-color: color-mix(in srgb, var(--el-color-warning) 40%, transparent);
  background: color-mix(in srgb, var(--el-color-warning) 12%, var(--el-bg-color-overlay));
}

.toast-info {
  border-color: color-mix(in srgb, var(--el-color-primary) 40%, transparent);
  background: color-mix(in srgb, var(--el-color-primary) 12%, var(--el-bg-color-overlay));
}

.toast-enter-active,
.toast-leave-active {
  transition: all 0.3s ease;
}

.toast-enter-from,
.toast-leave-to {
  opacity: 0;
  transform: translateX(20px);
}
</style>
