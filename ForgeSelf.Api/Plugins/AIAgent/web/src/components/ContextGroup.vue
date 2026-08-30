<template>
  <div class="grp">
    <!-- 分组头：点击切换展开/收起 -->
    <button type="button" class="grp__head" @click="$emit('toggle')">
      <span class="grp__label">
        <svg class="grp__chevron" :class="{ 'grp__chevron--open': open }" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="M9 18l6-6-6-6" />
        </svg>
        {{ title }}
      </span>
      <span v-if="loading" class="grp__badge">…</span>
      <span v-else class="grp__badge" :class="{ 'grp__badge--active': count > 0 }">{{ count }}</span>
    </button>

    <!-- 分组内容 -->
    <div v-show="open" class="grp__body">
      <p v-if="error" class="grp__error">{{ error }}</p>
      <slot v-else />
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * 左栏分组容器（标题 + 计数徽标 + 可折叠内容）。
 * 抽出来是为了让 MCP 工具 / 技能 / 提示指令 / 记忆四组行为一致。
 */
defineProps<{
  /** 分组标题。 */
  title: string
  /** 计数徽标数值。 */
  count?: number
  /** 是否加载中。 */
  loading?: boolean
  /** 加载错误信息；非空时展示错误而不是插槽内容。 */
  error?: string
  /** 是否展开。 */
  open?: boolean
}>()

defineEmits<{
  /** 点击分组头时触发（切换展开状态）。 */
  (e: 'toggle'): void
}>()
</script>

<style scoped>
.grp {
  display: flex;
  flex-direction: column;
  border-bottom: 1px solid var(--el-border-color-dark, #2b2b2c);
}

.grp__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  width: 100%;
  padding: 8px 12px;
  background: transparent;
  border: none;
  cursor: pointer;
  transition: background 0.15s ease;
}

.grp__head:hover {
  background: var(--el-fill-color, #262727);
}

.grp__label {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: var(--el-font-size-small, 13px);
  font-weight: var(--el-weight-medium, 500);
  color: var(--el-text-color-primary, #e5eaf3);
}

.grp__chevron {
  width: 12px;
  height: 12px;
  color: var(--el-text-color-secondary, #a3a6ad);
  transition: transform 0.15s ease;
}

.grp__chevron--open {
  transform: rotate(90deg);
}

.grp__badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 18px;
  height: 18px;
  padding: 0 5px;
  font-size: var(--el-font-size-extra-small, 12px);
  font-weight: var(--el-weight-semibold, 600);
  color: var(--el-text-color-secondary, #a3a6ad);
  background: var(--el-bg-color-overlay, #1d1e1f);
  border-radius: 999px;
}

.grp__badge--active {
  color: var(--el-color-primary, #ffb84d);
  background: var(--el-color-primary-light, rgba(255, 184, 77, 0.12));
}

.grp__body {
  display: flex;
  flex-direction: column;
  gap: 2px;
  padding: 0 12px 8px;
}

.grp__error {
  margin: 0;
  font-size: var(--el-font-size-extra-small, 12px);
  color: var(--el-color-danger, #f56c6c);
}
</style>
