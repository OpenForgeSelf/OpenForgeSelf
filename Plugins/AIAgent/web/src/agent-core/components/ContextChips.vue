<script setup>
/**
 * ContextChips —— 输入区上方的「已选上下文」条（旧版 composer chips 的等价物）。
 *
 * 「所见即所发」：这一条显示什么，本次发送就带上什么 —— 用户不必记住自己勾过什么。
 * 组件只渲染 `chips`（`{ key, label, icon?, removable }`）并把点击与删除抛给宿主，
 * 不认识「项目 / 工具 / 技能 / Agent / 模型」这些概念（那是宿主的事）。
 */
const props = defineProps({
  chips: { type: Array, default: () => [] },
})
const emit = defineEmits(['remove', 'click'])
</script>

<template>
  <div v-if="chips.length" class="aiw-chips" role="group" aria-label="已选上下文">
    <span
      v-for="c in chips"
      :key="c.key"
      class="aiw-chip-ctx"
      :class="{ 'is-clickable': c.clickable !== false }"
      :title="c.title || c.label"
    >
      <button
        type="button"
        class="aiw-chip-ctx__main"
        @click="c.clickable !== false && emit('click', c.key)"
      >
        <span v-if="c.icon" class="aiw-chip-ctx__ico" aria-hidden="true">{{ c.icon }}</span>
        <span class="aiw-chip-ctx__txt">{{ c.label }}</span>
      </button>
      <button
        v-if="c.removable"
        type="button"
        class="aiw-chip-ctx__x"
        :aria-label="'移除 ' + c.label"
        @click="emit('remove', c.key)"
      >×</button>
    </span>
  </div>
</template>
