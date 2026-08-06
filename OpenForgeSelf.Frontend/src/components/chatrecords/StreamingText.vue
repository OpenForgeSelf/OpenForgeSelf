<script setup lang="ts">
import { ref, onMounted, onUnmounted, watch } from 'vue'

const props = withDefaults(
  defineProps<{
    text: string
    speed?: number
    chunkSize?: number
    autoStart?: boolean
  }>(),
  {
    speed: 18,
    chunkSize: 3,
    autoStart: true
  }
)

const displayed = ref('')
const isPlaying = ref(false)
const isDone = ref(false)
let index = 0
let timer: ReturnType<typeof setInterval> | null = null

function clearTimer(): void {
  if (timer !== null) {
    clearInterval(timer)
    timer = null
  }
}

function play(): void {
  if (isDone.value || timer !== null) return
  if (props.text.length === 0) {
    finish()
    return
  }
  isPlaying.value = true
  timer = setInterval(() => {
    const next = Math.min(index + props.chunkSize, props.text.length)
    displayed.value = props.text.slice(0, next)
    index = next
    if (index >= props.text.length) {
      finish()
    }
  }, props.speed)
}

function finish(): void {
  clearTimer()
  displayed.value = props.text
  index = props.text.length
  isPlaying.value = false
  isDone.value = true
}

function restart(): void {
  clearTimer()
  displayed.value = ''
  index = 0
  isDone.value = false
  play()
}

function toggle(): void {
  if (isPlaying.value) {
    clearTimer()
    isPlaying.value = false
  } else {
    play()
  }
}

function skip(): void {
  finish()
}

onMounted(() => {
  if (props.autoStart) play()
})

onUnmounted(clearTimer)

watch(
  () => props.text,
  () => restart()
)
</script>

<template>
  <div class="streaming-text">
    <div class="streaming-controls">
      <button type="button" class="stream-btn" @click="toggle">
        {{ isPlaying ? '⏸ 暂停' : isDone ? '↻ 重新播放' : '▶ 播放' }}
      </button>
      <button v-if="!isDone" type="button" class="stream-btn" @click="skip">⏭ 立即显示</button>
      <span v-if="isPlaying" class="stream-state">流式回放中…</span>
      <span v-else-if="isDone" class="stream-state done">已显示全文</span>
    </div>
    <div class="streaming-body">
      <span class="stream-content">{{ displayed }}</span><span v-if="isPlaying" class="stream-cursor" />
    </div>
  </div>
</template>

<style scoped>
.streaming-text {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.streaming-controls {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.stream-btn {
  padding: 4px 12px;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  background: var(--el-fill-color-light);
  color: var(--el-text-color-regular);
  font-size: 12px;
  cursor: pointer;
  transition: all 150ms ease;
}

.stream-btn:hover {
  border-color: var(--el-color-primary);
  color: var(--el-color-primary);
}

.stream-state {
  font-size: 12px;
  color: var(--el-color-primary);
}

.stream-state.done {
  color: var(--el-text-color-secondary);
}

.streaming-body {
  padding: 12px 14px;
  background: var(--el-fill-color-light);
  border: 1px solid var(--el-border-color-lighter);
  border-left: 3px solid var(--el-color-primary);
  border-radius: 8px;
  font-size: 14px;
  line-height: 1.7;
  color: var(--el-text-color-primary);
  white-space: pre-wrap;
  word-break: break-word;
}

.stream-cursor {
  display: inline-block;
  width: 7px;
  height: 1em;
  margin-left: 1px;
  vertical-align: text-bottom;
  background: var(--el-color-primary);
  animation: blink 1s step-end infinite;
}

@keyframes blink {
  0%,
  100% {
    opacity: 1;
  }
  50% {
    opacity: 0;
  }
}
</style>
