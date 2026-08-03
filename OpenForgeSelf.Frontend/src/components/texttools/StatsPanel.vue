<script setup lang="ts">
import { computed } from 'vue'
import { useTextToolsStore } from '@/stores/textTools'

const store = useTextToolsStore()

const maxValue = computed(() => {
  const stats = store.stats
  return Math.max(
    stats.charCount,
    stats.charCountNoSpace,
    stats.wordCount * 10,
    stats.lineCount * 5,
    stats.byteCount
  ) || 1
})

function getPercentage(value: number): number {
  return Math.min((value / maxValue.value) * 100, 100)
}

const statsItems = computed(() => [
  { label: '字符数', value: store.stats.charCount, icon: '🔤', weight: 1 },
  { label: '字符数(不含空格)', value: store.stats.charCountNoSpace, icon: '🔡', weight: 1 },
  { label: '词数', value: store.stats.wordCount, icon: '📝', weight: 10 },
  { label: '行数', value: store.stats.lineCount, icon: '📄', weight: 5 },
  { label: '字节数', value: store.stats.byteCount, icon: '💾', weight: 1 }
])
</script>

<template>
  <div class="stats-panel">
    <div class="stats-grid">
      <div
        v-for="item in statsItems"
        :key="item.label"
        class="stat-card"
      >
        <div class="stat-icon">{{ item.icon }}</div>
        <div class="stat-content">
          <div class="stat-value">{{ item.value.toLocaleString() }}</div>
          <div class="stat-label">{{ item.label }}</div>
          <div class="stat-bar">
            <div
              class="stat-bar-fill"
              :style="{ width: getPercentage(item.value * item.weight) + '%' }"
            />
          </div>
        </div>
      </div>
    </div>

    <div class="stats-tip">
      💡 提示：在输入框中输入文本即可实时查看统计信息
    </div>
  </div>
</template>

<style scoped>
.stats-panel {
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding: 16px;
  background-color: var(--el-bg-color-page);
  border-radius: 8px;
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 12px;
}

.stat-card {
  display: flex;
  gap: 12px;
  padding: 16px;
  background-color: var(--el-bg-color);
  border: 1px solid var(--el-border-color);
  border-radius: 8px;
  transition: box-shadow 0.2s;
}

.stat-card:hover {
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);
}

.stat-icon {
  font-size: 28px;
  flex-shrink: 0;
}

.stat-content {
  flex: 1;
  min-width: 0;
}

.stat-value {
  font-size: 20px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin-bottom: 4px;
}

.stat-label {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  margin-bottom: 8px;
}

.stat-bar {
  height: 4px;
  background-color: var(--el-border-color);
  border-radius: 2px;
  overflow: hidden;
}

.stat-bar-fill {
  height: 100%;
  background: linear-gradient(90deg, var(--el-color-primary), var(--el-color-primary-light-3));
  border-radius: 2px;
  transition: width 0.3s ease;
}

.stats-tip {
  padding: 10px 14px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
  background-color: var(--el-bg-color);
  border: 1px dashed var(--el-border-color);
  border-radius: 6px;
}
</style>
