<script setup lang="ts">
import { ref, computed } from 'vue'

const props = withDefaults(defineProps<{
  content: string
  maxLength?: number
}>(), {
  maxLength: 500
})

const copied = ref(false)
// 是否展开（仅对超长内容生效）
const expanded = ref(false)

// 超过阈值视为超长，需要展开/收起
const isLong = computed(() => props.content.length > props.maxLength)

// 收起态展示文本（截断 + 省略号）
const collapsedText = computed(() => {
  if (!isLong.value || expanded.value) return props.content
  return props.content.slice(0, props.maxLength) + '…'
})

async function copyFullContent() {
  try {
    await navigator.clipboard.writeText(props.content)
    copied.value = true
    setTimeout(() => {
      copied.value = false
    }, 2000)
  } catch (err) {
    console.error('复制失败:', err)
  }
}

function toggleExpand() {
  expanded.value = !expanded.value
}
</script>

<template>
  <div class="truncated-content">
    <!-- 展开态：内容区限定高度，超高出现滚动条 -->
    <div v-if="isLong && expanded" class="content-scroll">
      <pre class="content-text">{{ content }}</pre>
    </div>
    <!-- 收起态 / 非超长：直接展示（收起态截断） -->
    <pre v-else class="content-text">{{ collapsedText }}</pre>

    <!-- 超长内容：复制按钮旁加「展开/收起」 -->
    <div v-if="isLong" class="action-row">
      <button class="act-btn" type="button" @click="toggleExpand">
        <i :class="expanded ? 'fa-solid fa-chevron-up' : 'fa-solid fa-chevron-down'" />
        {{ expanded ? '收起' : '展开' }}
      </button>
      <button class="act-btn" type="button" @click="copyFullContent">
        <i class="fa-regular fa-copy" />
        复制完整内容
      </button>
      <span v-if="copied" class="copy-success">已复制!</span>
    </div>
  </div>
</template>

<style scoped>
.truncated-content {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

/* 展开滚动容器：超高出现滚动条，避免撑长页面 */
.content-scroll {
  max-height: 320px;
  overflow-y: auto;
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
}

.content-text {
  margin: 0;
  padding: 12px;
  background: var(--el-fill-color-light);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
  font-family: 'Monaco', 'Menlo', 'Consolas', monospace;
  font-size: 13px;
  line-height: 1.5;
  color: var(--el-text-color-primary);
  white-space: pre-wrap;
  word-break: break-all;
  overflow-x: auto;
}
/* 滚动容器内去掉内层边框/圆角/背景，避免双重边框 */
.content-scroll .content-text {
  border: none;
  border-radius: 0;
  background: transparent;
}

.action-row {
  display: flex;
  align-items: center;
  gap: 12px;
}

.act-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 6px 12px;
  border: 1px solid var(--el-border-color);
  background: var(--el-fill-color);
  color: var(--el-text-color-regular);
  font-size: 13px;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.act-btn:hover {
  border-color: var(--el-color-primary);
  color: var(--el-color-primary);
}

.copy-success {
  font-size: 13px;
  color: var(--el-color-success);
}
</style>
