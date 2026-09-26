<script setup lang="ts">
import { confirmVisible, confirmOptions, resolveConfirm } from '../confirm'
</script>

<template>
  <Teleport to="body">
    <Transition name="modal">
      <div
        v-if="confirmVisible"
        class="confirm-overlay"
        role="alertdialog"
        aria-modal="true"
        :aria-label="confirmOptions.title || '确认操作'"
        @click.self="resolveConfirm(false)"
      >
        <div class="confirm-box">
          <h3 v-if="confirmOptions.title" class="confirm-title">{{ confirmOptions.title }}</h3>
          <p class="confirm-message">{{ confirmOptions.message }}</p>
          <div class="confirm-actions">
            <button
              type="button"
              class="btn cancel"
              @click="resolveConfirm(false)"
            >
              {{ confirmOptions.cancelText || '取消' }}
            </button>
            <button
              type="button"
              class="btn ok"
              :class="{ danger: confirmOptions.danger }"
              @click="resolveConfirm(true)"
            >
              {{ confirmOptions.confirmText || '确认' }}
            </button>
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.confirm-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1500;
  padding: 20px;
}

.confirm-box {
  background: var(--el-bg-color);
  border-radius: 12px;
  width: 100%;
  max-width: 380px;
  padding: 24px;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
}

.confirm-title {
  font-size: 17px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 0 0 12px 0;
}

.confirm-message {
  font-size: 14px;
  line-height: 1.6;
  color: var(--el-text-color-regular);
  margin: 0 0 20px 0;
  white-space: pre-wrap;
}

.confirm-actions {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}

.btn {
  padding: 9px 20px;
  border: none;
  border-radius: 8px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s ease;
}

.cancel {
  background: var(--el-fill-color-light);
  color: var(--el-text-color-regular);
}

.cancel:hover {
  background: var(--el-fill-color);
}

.ok {
  background: var(--el-color-primary);
  color: var(--el-color-white);
}

.ok:hover {
  background: var(--el-color-primary-light-3);
}

.ok.danger {
  background: var(--el-color-danger);
  color: var(--el-color-white);
}

.ok.danger:hover {
  background: var(--el-color-danger-light-3);
}

.modal-enter-active,
.modal-leave-active {
  transition: opacity 0.25s ease;
}

.modal-enter-from,
.modal-leave-to {
  opacity: 0;
}
</style>
