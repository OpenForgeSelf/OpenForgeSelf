<script setup lang="ts">
/**
 * 任务详情抽屉壳：Teleport 到 body，避免插件根 overflow 裁切（PC-012）。
 * 桌面 480px，窄屏（≤768）100%；遮罩 / Escape / × 由父级与详情共同关闭。
 */
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'

const props = defineProps<{
  modelValue: boolean
  title?: string
}>()

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
}>()

const narrow = ref(false)

function measure(): void {
  narrow.value = window.matchMedia('(max-width: 768px)').matches
}

const sizeStyle = computed(() => ({
  width: narrow.value ? '100%' : '480px',
}))

function close(): void {
  emit('update:modelValue', false)
}

function onKey(event: KeyboardEvent): void {
  if (!props.modelValue || event.key !== 'Escape') return
  const tag = (event.target as HTMLElement)?.tagName ?? ''
  if (['INPUT', 'TEXTAREA', 'SELECT'].includes(tag)) return
  event.preventDefault()
  close()
}

onMounted(() => {
  measure()
  window.addEventListener('resize', measure)
  window.addEventListener('keydown', onKey)
})

onUnmounted(() => {
  window.removeEventListener('resize', measure)
  window.removeEventListener('keydown', onKey)
})

watch(() => props.modelValue, (open) => {
  document.body.style.overflow = open ? 'hidden' : ''
}, { immediate: true })

onUnmounted(() => {
  document.body.style.overflow = ''
})
</script>

<template>
  <Teleport to="body">
    <!-- 不用 Vue Transition：Teleport+leave transform 曾导致抽屉停在屏外而 toBeVisible 仍绿 -->
    <div
      v-if="modelValue"
      class="tt-drawer-mask"
      data-test="task-drawer-mask"
      aria-hidden="true"
      @click="close"
    />
    <aside
      v-if="modelValue"
      class="tt-drawer"
      data-test="task-drawer"
      role="dialog"
      aria-modal="true"
      :aria-label="title || '任务详情'"
      :style="sizeStyle"
      @click.stop
    >
      <slot />
    </aside>
  </Teleport>
</template>

<style scoped>
.tt-drawer-mask {
  position: fixed;
  inset: 0;
  z-index: 2000;
  background: color-mix(in srgb, var(--ink-950) 35%, transparent);
}
.tt-drawer {
  position: fixed;
  top: 0;
  right: 0;
  bottom: 0;
  z-index: 2001;
  display: flex;
  flex-direction: column;
  max-width: 100vw;
  background: var(--el-bg-color);
  box-shadow: var(--el-box-shadow-dark);
  box-sizing: border-box;
}
</style>
