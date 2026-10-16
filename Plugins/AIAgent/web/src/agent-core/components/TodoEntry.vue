<script setup>
/**
 * TodoEntry —— 待办**独立入口**（041 修订二）。
 *
 * 为什么拆出来：初版把待办和审批/询问塞进同一个 `.aiw-ask` 区，用户指出待办是
 * **常驻任务面板**（要看全部、要独立入口），而审批/询问是**瞬时待你动作**——
 * 两者生命周期不同，混在一起待办会被审批卡挤走。
 *
 * 位置：输入框**下方**一行（与上下文图标行同一行的左侧），点击展开浮层列表。
 * 形态：一个入口按钮（图标 + 「待办 N」+ 未完成数角标），点开是浮层
 * （absolute 定位，不占内容区高度）；浮层内列表可整体收起/展开。
 */
import { computed, ref, watch, onBeforeUnmount } from 'vue'

const props = defineProps({
  /** 未完成条数（宿主算好） */
  openCount: { type: Number, default: 0 },
  /** 完成比例 0~1 */
  progress: { type: Number, default: 0 },
  /** 任务清单：[{ id, title, done }] */
  todos: { type: Array, default: () => [] },
  /** 是否显示（无待办时不渲染入口） */
  visible: { type: Boolean, default: true },
})
const emit = defineEmits(['toggle-item'])

const open = ref(false)
const listCollapsed = ref(false)

const has = computed(() => props.visible && props.todos.length > 0)

function togglePanel() {
  open.value = !open.value
}
function toggleList() {
  listCollapsed.value = !listCollapsed.value
}

/* 点入口外部收起浮层：一次注册、随生命周期清理 */
const rootEl = ref(null)
function onDocPointer(e) {
  if (!open.value) return
  const el = rootEl.value
  if (el && !el.contains(e.target)) open.value = false
}
watch(open, (v) => {
  if (typeof document === 'undefined') return
  if (v) document.addEventListener('pointerdown', onDocPointer, true)
  else document.removeEventListener('pointerdown', onDocPointer, true)
})
onBeforeUnmount(() => {
  if (typeof document === 'undefined') return
  document.removeEventListener('pointerdown', onDocPointer, true)
})
</script>

<template>
  <span v-if="has" ref="rootEl" class="aiw-todoentry">
    <button
      type="button"
      class="aiw-todoentry__btn"
      :class="{ 'is-on': open }"
      :aria-expanded="open ? 'true' : 'false'"
      aria-haspopup="true"
      :title="`待办 ${todos.length} 项，${openCount} 项未完成`"
      @mousedown.prevent
      @click="togglePanel"
    >
      <svg viewBox="0 0 24 24" aria-hidden="true">
        <path d="M9.5 16.5 5 12l1.4-1.4 3.1 3.1 8.1-8.1L19 7l-9.5 9.5Z" fill="currentColor" />
      </svg>
      <span>待办</span>
      <span v-if="openCount" class="aiw-todoentry__n">{{ openCount > 99 ? '99+' : openCount }}</span>
    </button>
    <span v-if="open" class="aiw-todoentry__pop">
      <div class="aiw-todopanel">
        <button
          type="button"
          class="aiw-todopanel__head"
          :aria-expanded="listCollapsed ? 'false' : 'true'"
          @mousedown.prevent
          @click="toggleList"
        >
          <span class="aiw-todopanel__title">待办 {{ todos.length }} 项 · {{ openCount }} 未完成</span>
          <span v-if="progress > 0" class="aiw-todopanel__pct">{{ Math.round(progress * 100) }}%</span>
          <svg class="aiw-todopanel__chev" :class="{ 'is-open': !listCollapsed }" viewBox="0 0 24 24" aria-hidden="true">
            <path d="M7.4 8.6 12 13.2l4.6-4.6L18 10l-6 6-6-6z" fill="currentColor" />
          </svg>
        </button>
        <ul v-show="!listCollapsed" class="aiw-todopanel__list">
          <li v-for="(t, i) in todos" :key="t.id || i" :class="{ 'is-done': t.done }">
            <span class="aiw-todopanel__dot" aria-hidden="true"></span>
            <span class="aiw-todopanel__txt">{{ t.title }}</span>
          </li>
        </ul>
      </div>
    </span>
  </span>
</template>
