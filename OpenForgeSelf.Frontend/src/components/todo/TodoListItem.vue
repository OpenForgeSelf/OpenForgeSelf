<script setup lang="ts">
import { computed } from 'vue'
import { ElCheckbox, ElButton, ElTag, ElTooltip, ElText } from 'element-plus'
import { Delete, Edit, Calendar } from '@element-plus/icons-vue'
import type { TodoItem } from '@/types/todo'

const props = defineProps<{
  todo: TodoItem
}>()

const emit = defineEmits<{
  (e: 'toggle-status', id: number): void
  (e: 'edit', id: number): void
  (e: 'delete', id: number): void
}>()

const isCompleted = computed(() => props.todo.status === 'Completed')

const dueDateText = computed(() => {
  if (!props.todo.dueDate) return ''
  try {
    const d = new Date(props.todo.dueDate)
    return d.toLocaleDateString('zh-CN')
  } catch {
    return props.todo.dueDate
  }
})

const isOverdue = computed(() => {
  if (!props.todo.dueDate || isCompleted.value) return false
  try {
    const due = new Date(props.todo.dueDate)
    const now = new Date()
    now.setHours(0, 0, 0, 0)
    return due.getTime() < now.getTime()
  } catch {
    return false
  }
})

function onCheckboxChange(value: string | number | boolean) {
  if (value) {
    emit('toggle-status', props.todo.id)
  } else {
    emit('toggle-status', props.todo.id)
  }
}
</script>

<template>
  <div class="todo-item flex items-start gap-3 px-4 py-3 hover:bg-[var(--el-fill-color-light)] transition-colors border-b border-[var(--el-border-color-lighter)]">
    <ElCheckbox
      :model-value="isCompleted"
      class="!mt-1"
      @change="onCheckboxChange"
      aria-label="标记完成状态"
    />

    <div class="flex-1 min-w-0">
      <div class="flex items-center gap-2 flex-wrap">
        <ElText
          :type="isCompleted ? 'info' : ''"
          :class="{ 'line-through opacity-60': isCompleted }"
          class="!font-medium"
        >
          {{ todo.title }}
        </ElText>
        <ElTag
          v-if="isCompleted"
          type="success"
          size="small"
          effect="plain"
        >
          已完成
        </ElTag>
        <ElTag
          v-else-if="isOverdue"
          type="danger"
          size="small"
          effect="plain"
        >
          已逾期
        </ElTag>
      </div>

      <ElText
        v-if="todo.remark"
        type="info"
        size="small"
        class="!block !mt-1 break-words"
      >
        {{ todo.remark }}
      </ElText>

      <div v-if="dueDateText" class="!mt-1 flex items-center gap-1">
        <ElIcon class="text-[var(--el-text-color-secondary)]"><Calendar /></ElIcon>
        <ElText
          :type="isOverdue ? 'danger' : 'info'"
          size="small"
        >
          截止: {{ dueDateText }}
        </ElText>
      </div>
    </div>

    <div class="flex items-center gap-1 shrink-0">
      <ElTooltip content="编辑" placement="top">
        <ElButton
          size="small"
          text
          :icon="Edit"
          aria-label="编辑待办"
          @click="emit('edit', todo.id)"
        />
      </ElTooltip>
      <ElTooltip content="删除" placement="top">
        <ElButton
          size="small"
          text
          type="danger"
          :icon="Delete"
          aria-label="删除待办"
          @click="emit('delete', todo.id)"
        />
      </ElTooltip>
    </div>
  </div>
</template>
