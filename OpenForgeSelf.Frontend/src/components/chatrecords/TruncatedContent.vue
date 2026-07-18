<script setup lang="ts">
import { ref, computed } from 'vue'

const props = withDefaults(defineProps<{
  content: string
  maxLength?: number
}>(), {
  maxLength: 500
})

const copied = ref(false)

const isTruncated = computed(() => props.content.length > props.maxLength)

const displayContent = computed(() => {
  if (!isTruncated.value) return props.content
  return props.content.slice(0, props.maxLength) + '...'
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
</script>

<template>
  <div class="truncated-content">
    <pre class="content-text">{{ displayContent }}</pre>
    <div v-if="isTruncated" class="copy-section">
      <button class="copy-btn" @click="copyFullContent">
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

.content-text {
  margin: 0;
  padding: 12px;
  background: #f8f9fa;
  border-radius: 6px;
  font-family: 'Monaco', 'Menlo', 'Consolas', monospace;
  font-size: 13px;
  line-height: 1.5;
  color: #212529;
  white-space: pre-wrap;
  word-break: break-all;
  overflow-x: auto;
}

.copy-section {
  display: flex;
  align-items: center;
  gap: 12px;
}

.copy-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 6px 12px;
  border: 1px solid #dee2e6;
  background: white;
  color: #495057;
  font-size: 13px;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.copy-btn:hover {
  background: #f8f9fa;
  border-color: #ced4da;
}

.copy-success {
  font-size: 13px;
  color: #16a34a;
}
</style>
