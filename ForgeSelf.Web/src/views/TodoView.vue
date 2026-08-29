<script setup lang="ts">
import { onMounted, ref, computed } from 'vue';
// 模板组件（ElButton/ElRadioGroup 等）由 unplugin-vue-components 自动解析；
// 仅保留 API 调用组件 ElMessage/ElMessageBox（JS 中 ElMessage.success()/ElMessageBox.confirm() 调用）
import { ElMessageBox, ElMessage } from 'element-plus';
import { Plus } from '@element-plus/icons-vue';
import { useTodoStore } from '@/stores/todo';
import type { TodoStatus, TodoCreateRequest, TodoUpdateRequest } from '@/types/todo';
import TodoListItem from '@/components/todo/TodoListItem.vue';
import TodoEditDialog from '@/components/todo/TodoEditDialog.vue';

const store = useTodoStore();

const dialogVisible = ref(false);
const editingId = ref<number | null>(null);

const statusFilter = computed<TodoStatus | undefined>({
  get: () => store.statusFilter,
  set: (value) => {
    store.setStatusFilter(value);
    store.loadTodos();
  }
});

const editingTodo = computed(() =>
  editingId.value !== null ? (store.items.find((t) => t.id === editingId.value) ?? null) : null
);

function openCreateDialog() {
  editingId.value = null;
  dialogVisible.value = true;
}

function openEditDialog(id: number) {
  editingId.value = id;
  dialogVisible.value = true;
}

function closeDialog() {
  dialogVisible.value = false;
  editingId.value = null;
}

async function handleSubmit(payload: { id?: number; data: TodoCreateRequest | TodoUpdateRequest }) {
  try {
    if (payload.id) {
      await store.editTodo(payload.id, payload.data as TodoUpdateRequest);
      ElMessage.success({ message: '更新待办成功', offset: 60 });
    } else {
      await store.addTodo(payload.data as TodoCreateRequest);
      ElMessage.success({ message: '创建待办成功', offset: 60 });
    }
    closeDialog();
  } catch (e) {
    ElMessage.error({ message: e instanceof Error ? e.message : '操作失败', offset: 60 });
  }
}

async function handleToggleStatus(id: number) {
  const todo = store.items.find((t) => t.id === id);
  if (!todo) return;
  try {
    if (todo.status === 'Pending') {
      await store.markComplete(id);
      ElMessage.success({ message: '标记完成成功', offset: 60 });
    } else {
      await store.markReopen(id);
      ElMessage.success({ message: '重新打开成功', offset: 60 });
    }
  } catch (e) {
    ElMessage.error({ message: e instanceof Error ? e.message : '操作失败', offset: 60 });
  }
}

async function handleDelete(id: number) {
  try {
    await ElMessageBox.confirm('确认删除该待办吗？此操作不可撤销。', '删除确认', {
      type: 'warning',
      confirmButtonText: '删除',
      cancelButtonText: '取消',
      confirmButtonClass: 'el-button--danger'
    });
    await store.removeTodo(id);
    ElMessage.success('删除待办成功');
  } catch (e) {
    if (e === 'cancel' || e === 'close') return;
    ElMessage.error(e instanceof Error ? e.message : '删除失败');
  }
}

function handlePageChange(p: number) {
  store.setPage(p);
  store.loadTodos();
}

onMounted(() => {
  store.loadTodos();
});
</script>

<template>
  <div v-loading="store.loading" class="todo-view p-6 max-w-4xl mx-auto">
    <header class="flex items-center justify-between mb-4">
      <h1 class="!text-2xl !font-bold !m-0 text-[var(--el-text-color-primary)]">待办追踪</h1>
      <ElButton type="primary" :icon="Plus" @click="openCreateDialog">新建待办</ElButton>
    </header>

    <ElCard shadow="never" class="!mb-4" body-class="!py-3">
      <div class="flex items-center justify-between flex-wrap gap-3">
        <ElRadioGroup v-model="statusFilter" aria-label="状态过滤">
          <ElRadioButton :value="undefined">全部</ElRadioButton>
          <ElRadioButton value="Pending">待处理</ElRadioButton>
          <ElRadioButton value="Completed">已完成</ElRadioButton>
        </ElRadioGroup>

        <div class="text-sm text-[var(--el-text-color-secondary)]">
          共 {{ store.total }} 条
          <span v-if="store.pendingCount > 0" class="!ml-2">待处理 {{ store.pendingCount }}</span>
        </div>
      </div>
    </ElCard>

    <ElCard shadow="never" body-class="!p-0">
      <ElEmpty v-if="store.items.length === 0 && !store.loading" description="暂无待办事项">
        <ElButton type="primary" :icon="Plus" @click="openCreateDialog">新建第一个待办</ElButton>
      </ElEmpty>

      <div v-else class="todo-list">
        <TodoListItem
          v-for="todo in store.items"
          :key="todo.id"
          :todo="todo"
          @toggle-status="handleToggleStatus"
          @edit="openEditDialog"
          @delete="handleDelete" />
      </div>

      <div
        v-if="store.total > store.pageSize"
        class="!py-3 flex justify-center border-t border-[var(--el-border-color-lighter)]">
        <ElPagination
          :current-page="store.page"
          :page-size="store.pageSize"
          :total="store.total"
          layout="prev, pager, next"
          @current-change="handlePageChange" />
      </div>
    </ElCard>

    <TodoEditDialog
      v-model:visible="dialogVisible"
      :todo="editingTodo"
      @submit="handleSubmit"
      @cancel="closeDialog" />
  </div>
</template>
