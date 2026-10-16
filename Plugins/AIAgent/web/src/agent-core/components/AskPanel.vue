<script setup>
/**
 * AskPanel —— 紧贴输入框**上方**的「待你动作」区（041 §3.4）。
 *
 * 2026-10-15 修订：**只承载审批 / 询问**两类瞬时动作。待办已拆成独立入口
 * `TodoEntry.vue`（输入框下方）——待办是常驻任务面板（看全部、独立入口），
 * 审批/询问是瞬时待你动作（要立刻操作），两者生命周期不同，混在一起
 * 待办会被审批卡挤走。
 *
 * 为什么不再放进消息流：审批、Agent 反问都属于「**现在轮到你动作**」，
 * 混在 messages 里会被滚动带走，用户以为没这回事。放在输入框上沿，
 * 与「下一步在这里操作」同一视觉区，和状态提示条（.aiw-status）是同一个道理。
 */
import { computed, ref } from 'vue'

const props = defineProps({
  /** 待办动作条目（已由宿主过滤掉已解决项） */
  items: { type: Array, default: () => [] },
  /** 待办完成比例 0~1（宿主算好，组件只画） */
  progress: { type: Number, default: 0 },
})
const emit = defineEmits(['resolve', 'answer'])

/* 只认审批 / 询问两类：待办已拆独立入口 TodoEntry（输入框下方底行），
   宿主若误把 todo 塞进来也一律忽略 —— 两处入口各自渲染同一份待办会重复。 */
const actionItems = computed(() => (props.items || []).filter((i) => i.kind === 'approval' || i.kind === 'question'))
const has = computed(() => actionItems.value.length > 0)

/* 折叠态：审批/询问**默认展开**（要立刻操作，收起会挡住动作）；
   用户也可手动收起长卡片。按 id 记，条目增删不串态。 */
const collapsed = ref({})
const isCollapsed = (it) => collapsed.value[it.id] ?? false
const toggleItem = (it) => {
  collapsed.value = { ...collapsed.value, [it.id]: !isCollapsed(it) }
}

const RISK_TEXT = { low: '低风险', medium: '中风险', high: '高风险', critical: '极高风险' }

function decide(id, decision) {
  emit('resolve', { id, decision })
}
function answer(id, option) {
  emit('answer', { id, option })
}
</script>

<template>
  <div v-if="has" class="aiw-ask" role="region" aria-label="待你处理">
    <div v-for="it in items" :key="it.id" class="aiw-ask__item" :class="`is-${it.kind}`">
      <template v-if="it.kind === 'approval'">
        <span class="aiw-ask__badge" :class="`aiw-ask__badge--${it.risk || 'medium'}`">
          {{ RISK_TEXT[it.risk || 'medium'] }}
        </span>
        <div class="aiw-ask__body">
          <div class="aiw-ask__title">{{ it.title }}</div>
          <div v-if="it.detail" class="aiw-ask__detail">{{ it.detail }}</div>
        </div>
        <div class="aiw-ask__btns">
          <button type="button" class="aiw-ask__btn aiw-ask__btn--ok" @click="decide(it.id, 'approve')">
            批准
          </button>
          <button type="button" class="aiw-ask__btn" @click="decide(it.id, 'reject')">拒绝</button>
        </div>
      </template>

      <template v-else-if="it.kind === 'question'">
        <span class="aiw-ask__badge aiw-ask__badge--question">需要确认</span>
        <div class="aiw-ask__body">
          <div class="aiw-ask__title">{{ it.title }}</div>
          <div v-if="it.detail" class="aiw-ask__detail">{{ it.detail }}</div>
          <div v-if="it.options?.length" class="aiw-ask__opts">
            <button
              v-for="o in it.options"
              :key="o"
              type="button"
              class="aiw-ask__opt"
              @mousedown.prevent
              @click="answer(it.id, o)"
            >
              {{ o }}
            </button>
          </div>
        </div>
      </template>

    </div>
  </div>
</template>
