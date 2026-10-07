<script setup lang="ts">
/**
 * 轻提示与确认框的宿主组件（渲染 notify.ts 的两份状态）。
 *
 * 确认框按钮文本用 `data-test` 锚点（e2e 要分别点「取消 / 确认」两条路径，
 * 只按文本找容易撞上页面里同名按钮）。
 */
import { computed } from 'vue'
import { confirmOptions, confirmVisible, dismissToast, resolveConfirm, toasts } from './notify'

const confirmTitle = computed(() => confirmOptions.value.title ?? '请确认')
</script>

<template>
  <div class="tt-notify">
    <div class="tt-toasts" role="status" aria-live="polite">
      <div v-for="t in toasts" :key="t.id" class="tt-toast" :class="`is-${t.kind}`" @click="dismissToast(t.id)">
        <span class="tt-toast-text">{{ t.message }}</span>
        <button class="tt-toast-x" type="button" aria-label="关闭提示">×</button>
      </div>
    </div>

    <div v-if="confirmVisible" class="tt-confirm-mask">
      <div class="tt-confirm" role="alertdialog" aria-modal="true" :aria-label="confirmTitle">
        <h3 class="tt-confirm-title">{{ confirmTitle }}</h3>
        <p class="tt-confirm-message">{{ confirmOptions.message }}</p>
        <p v-if="confirmOptions.detail" class="tt-confirm-detail">{{ confirmOptions.detail }}</p>
        <div class="tt-confirm-actions">
          <button type="button" class="tt-btn" data-test="confirm-cancel" @click="resolveConfirm(false)">
            {{ confirmOptions.cancelText ?? '取消' }}
          </button>
          <button type="button" class="tt-btn" :class="confirmOptions.danger ? 'is-danger' : 'is-primary'"
            data-test="confirm-ok" @click="resolveConfirm(true)">
            {{ confirmOptions.confirmText ?? '确认' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.tt-toasts {
  position: fixed;
  right: 16px;
  bottom: 16px;
  z-index: 3000;
  display: flex;
  flex-direction: column;
  gap: 8px;
  max-width: min(420px, calc(100vw - 32px));
}

.tt-toast {
  padding: 10px 12px;
  border-radius: var(--el-border-radius-base, 4px);
  border: 1px solid var(--el-border-color-lighter);
  background: var(--el-bg-color-overlay, var(--el-bg-color));
  color: var(--el-text-color-primary);
  box-shadow: var(--el-box-shadow-light);
  cursor: pointer;
  display: flex;
  align-items: flex-start;
  gap: 8px;
  font-size: 13px;
  line-height: 1.6;
}

.tt-toast.is-success { border-left: 3px solid var(--el-color-success); }
.tt-toast.is-error { border-left: 3px solid var(--el-color-danger); }
.tt-toast.is-warning { border-left: 3px solid var(--el-color-warning); }
.tt-toast.is-info { border-left: 3px solid var(--el-color-primary); }
.tt-toast-text { flex: 1; word-break: break-word; }
.tt-toast-x { border: 0; background: transparent; color: var(--el-text-color-secondary); cursor: pointer; }

.tt-confirm-mask {
  position: fixed;
  inset: 0;
  z-index: 3100;
  background: rgb(0 0 0 / 40%);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 16px;
}

.tt-confirm {
  width: min(440px, 100%);
  background: var(--el-bg-color-overlay, var(--el-bg-color));
  border: 1px solid var(--el-border-color-lighter);
  border-radius: var(--el-border-radius-base, 4px);
  box-shadow: var(--el-box-shadow-light);
  padding: 18px 20px 14px;
  color: var(--el-text-color-primary);
}

.tt-confirm-title { margin: 0 0 8px; font-size: 15px; font-weight: 600; }
.tt-confirm-message { margin: 0 0 6px; font-size: 13px; line-height: 1.7; }
.tt-confirm-detail { margin: 0 0 10px; font-size: 12px; line-height: 1.7; color: var(--el-text-color-secondary); }
.tt-confirm-actions { display: flex; justify-content: flex-end; gap: 8px; margin-top: 10px; }

.tt-btn {
  border: 1px solid var(--el-border-color);
  background: var(--el-bg-color);
  color: var(--el-text-color-regular);
  border-radius: var(--el-border-radius-base, 4px);
  padding: 6px 14px;
  font-size: 13px;
  cursor: pointer;
}

.tt-btn.is-primary { background: var(--el-color-primary); border-color: var(--el-color-primary); color: #fff; }
.tt-btn.is-danger { background: var(--el-color-danger); border-color: var(--el-color-danger); color: #fff; }
</style>
