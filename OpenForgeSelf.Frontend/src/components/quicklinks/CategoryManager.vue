<script setup lang="ts">
import { ref, computed } from 'vue'
import type { QuickLinkCategory } from '@/types/quickLinks'
import { useQuickLinksStore } from '@/stores/quickLinks'

const emit = defineEmits<{
  (e: 'close'): void
}>()

const quickLinksStore = useQuickLinksStore()

const newCategoryName = ref('')
const newCategoryIcon = ref('')
const editingCategoryId = ref<string | null>(null)
const editingName = ref('')
const editingIcon = ref('')
const isAdding = ref(false)
const draggedCategoryId = ref<string | null>(null)
const dragOverCategoryId = ref<string | null>(null)

const categories = computed(() => quickLinksStore.sortedCategories)

function startAdd(): void {
  isAdding.value = true
  newCategoryName.value = ''
  newCategoryIcon.value = ''
}

function cancelAdd(): void {
  isAdding.value = false
  newCategoryName.value = ''
  newCategoryIcon.value = ''
}

async function handleAdd(): Promise<void> {
  if (!newCategoryName.value.trim()) return

  try {
    await quickLinksStore.addCategory({
      name: newCategoryName.value.trim(),
      icon: newCategoryIcon.value.trim() || undefined
    })
    cancelAdd()
  } catch (e) {
    console.error('添加分类失败:', e)
  }
}

function startEdit(category: QuickLinkCategory): void {
  editingCategoryId.value = category.id
  editingName.value = category.name
  editingIcon.value = category.icon
}

function cancelEdit(): void {
  editingCategoryId.value = null
  editingName.value = ''
  editingIcon.value = ''
}

async function handleEdit(): Promise<void> {
  if (!editingCategoryId.value || !editingName.value.trim()) return

  try {
    await quickLinksStore.updateCategory(editingCategoryId.value, {
      name: editingName.value.trim(),
      icon: editingIcon.value.trim() || undefined
    })
    cancelEdit()
  } catch (e) {
    console.error('编辑分类失败:', e)
  }
}

async function handleDelete(category: QuickLinkCategory): Promise<void> {
  if (!confirm(`确定要删除分类"${category.name}"吗？该分类下的所有链接也将被删除。`)) {
    return
  }

  try {
    await quickLinksStore.removeCategory(category.id)
  } catch (e) {
    console.error('删除分类失败:', e)
  }
}

function selectCategory(categoryId: string | null): void {
  quickLinksStore.setCurrentCategory(categoryId)
}

function handleDragStart(event: DragEvent, categoryId: string): void {
  draggedCategoryId.value = categoryId
  if (event.dataTransfer) {
    event.dataTransfer.effectAllowed = 'move'
    event.dataTransfer.setData('text/plain', categoryId)
  }
}

function handleDragOver(event: DragEvent, categoryId: string): void {
  event.preventDefault()
  if (event.dataTransfer) {
    event.dataTransfer.dropEffect = 'move'
  }
  dragOverCategoryId.value = categoryId
}

function handleDragLeave(): void {
  dragOverCategoryId.value = null
}

async function handleDrop(event: DragEvent, targetCategoryId: string): Promise<void> {
  event.preventDefault()
  dragOverCategoryId.value = null

  if (!draggedCategoryId.value || draggedCategoryId.value === targetCategoryId) {
    draggedCategoryId.value = null
    return
  }

  const draggedIndex = categories.value.findIndex(c => c.id === draggedCategoryId.value)
  const targetIndex = categories.value.findIndex(c => c.id === targetCategoryId)

  if (draggedIndex === -1 || targetIndex === -1) {
    draggedCategoryId.value = null
    return
  }

  const newCategories = [...categories.value]
  const [removed] = newCategories.splice(draggedIndex, 1)
  newCategories.splice(targetIndex, 0, removed)

  try {
    const updates = newCategories.map((cat, index) =>
      quickLinksStore.updateCategory(cat.id, { sortOrder: index })
    )
    await Promise.all(updates)
  } catch (e) {
    console.error('排序分类失败:', e)
  }

  draggedCategoryId.value = null
}

function handleDragEnd(): void {
  draggedCategoryId.value = null
  dragOverCategoryId.value = null
}
</script>

<template>
  <div class="category-manager">
    <div class="manager-header">
      <h3>分类管理</h3>
      <button class="close-btn" aria-label="关闭" @click="emit('close')">
        ×
      </button>
    </div>

    <div class="category-list">
      <div
        class="category-item all-category"
        :class="{ active: quickLinksStore.currentCategory === null }"
        role="button"
        tabindex="0"
        @click="selectCategory(null)"
      >
        <span class="category-icon">📋</span>
        <span class="category-name">全部链接</span>
        <span class="category-count">{{ quickLinksStore.links.length }}</span>
      </div>

      <div
        v-for="category in categories"
        :key="category.id"
        class="category-item"
        :class="{
          active: quickLinksStore.currentCategory === category.id,
          dragging: draggedCategoryId === category.id,
          'drag-over': dragOverCategoryId === category.id
        }"
        role="button"
        tabindex="0"
        draggable="true"
        @click="selectCategory(category.id)"
        @dragstart="handleDragStart($event, category.id)"
        @dragover="handleDragOver($event, category.id)"
        @dragleave="handleDragLeave"
        @drop="handleDrop($event, category.id)"
        @dragend="handleDragEnd"
      >
        <template v-if="editingCategoryId === category.id">
          <div class="edit-form">
            <input
              v-model="editingIcon"
              class="icon-input"
              type="text"
              placeholder="图标"
              maxlength="2"
            />
            <input
              v-model="editingName"
              class="name-input"
              type="text"
              placeholder="分类名称"
              @keyup.enter="handleEdit"
              @keyup.esc="cancelEdit"
            />
            <button class="save-btn" @click.stop="handleEdit">✓</button>
            <button class="cancel-btn" @click.stop="cancelEdit">✕</button>
          </div>
        </template>
        <template v-else>
          <span class="drag-handle">⋮⋮</span>
          <span class="category-icon">{{ category.icon || '📁' }}</span>
          <span class="category-name">{{ category.name }}</span>
          <span class="category-count">
            {{ quickLinksStore.links.filter(l => l.categoryId === category.id).length }}
          </span>
          <div class="category-actions">
            <button class="action-btn" aria-label="编辑分类" @click.stop="startEdit(category)">
              ✏️
            </button>
            <button class="action-btn" aria-label="删除分类" @click.stop="handleDelete(category)">
              🗑️
            </button>
          </div>
        </template>
      </div>
    </div>

    <div v-if="isAdding" class="add-category-form">
      <input
        v-model="newCategoryIcon"
        class="icon-input"
        type="text"
        placeholder="图标"
        maxlength="2"
      />
      <input
        v-model="newCategoryName"
        class="name-input"
        type="text"
        placeholder="输入分类名称"
        @keyup.enter="handleAdd"
        @keyup.esc="cancelAdd"
      />
      <button class="save-btn" @click="handleAdd">✓</button>
      <button class="cancel-btn" @click="cancelAdd">✕</button>
    </div>

    <button v-else class="add-category-btn" @click="startAdd">
      <span class="add-icon">+</span>
      添加分类
    </button>
  </div>
</template>

<style scoped>
.category-manager {
  background: var(--el-bg-color);
  border-radius: 12px;
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 12px;
  height: 100%;
}

.manager-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding-bottom: 12px;
  border-bottom: 1px solid var(--el-border-color-light);
}

.manager-header h3 {
  font-size: 16px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 0;
}

.close-btn {
  width: 28px;
  height: 28px;
  border: none;
  background: var(--el-fill-color-light);
  border-radius: 6px;
  font-size: 18px;
  color: var(--el-text-color-secondary);
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s ease;
}

.close-btn:hover {
  background: var(--el-fill-color);
  color: var(--el-text-color-primary);
}

.category-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
  flex: 1;
  overflow-y: auto;
}

.category-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 12px;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s ease;
  border: 2px solid transparent;
}

.category-item:hover {
  background: var(--el-bg-color-page);
}

.category-item.active {
  background: var(--el-color-primary-light-9);
  border-color: var(--el-color-primary);
}

.category-item.dragging {
  opacity: 0.5;
}

.category-item.drag-over {
  border-color: var(--el-color-primary);
  background: var(--el-color-primary-light-9);
}

.all-category {
  font-weight: 500;
}

.drag-handle {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  cursor: grab;
  flex-shrink: 0;
  user-select: none;
}

.drag-handle:active {
  cursor: grabbing;
}

.category-icon {
  font-size: 18px;
  flex-shrink: 0;
}

.category-name {
  flex: 1;
  font-size: 14px;
  color: var(--el-text-color-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.category-count {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  background: var(--el-fill-color-light);
  padding: 2px 8px;
  border-radius: 10px;
  flex-shrink: 0;
}

.category-item.active .category-count {
  background: var(--el-bg-color);
  color: var(--el-color-primary);
}

.category-actions {
  display: none;
  gap: 4px;
  flex-shrink: 0;
}

.category-item:hover .category-actions {
  display: flex;
}

.action-btn {
  width: 24px;
  height: 24px;
  border: none;
  background: var(--el-bg-color);
  border-radius: 4px;
  font-size: 12px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s ease;
}

.action-btn:hover {
  background: var(--el-fill-color);
}

.edit-form {
  display: flex;
  align-items: center;
  gap: 6px;
  width: 100%;
}

.icon-input {
  width: 36px;
  padding: 4px 6px;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  font-size: 14px;
  text-align: center;
}

.name-input {
  flex: 1;
  padding: 4px 8px;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  font-size: 14px;
}

.icon-input:focus,
.name-input:focus {
  outline: none;
  border-color: var(--el-color-primary);
  box-shadow: 0 0 0 2px var(--primary-light);
}

.save-btn,
.cancel-btn {
  width: 28px;
  height: 28px;
  border: none;
  border-radius: 6px;
  font-size: 14px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s ease;
}

.save-btn {
  background: #d4edda;
  color: #155724;
}

.save-btn:hover {
  background: #c3e6cb;
}

.cancel-btn {
  background: #f8d7da;
  color: #721c24;
}

.cancel-btn:hover {
  background: #f5c6cb;
}

.add-category-form {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 12px;
  background: var(--el-bg-color-page);
  border-radius: 8px;
}

.add-category-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  padding: 10px 16px;
  border: 2px dashed var(--el-border-color);
  background: transparent;
  border-radius: 8px;
  font-size: 14px;
  color: var(--el-text-color-secondary);
  cursor: pointer;
  transition: all 0.2s ease;
}

.add-category-btn:hover {
  border-color: var(--el-color-primary);
  color: var(--el-color-primary);
  background: var(--el-color-primary-light-9);
}

.add-icon {
  font-size: 18px;
  font-weight: bold;
}

@media (max-width: 768px) {
  .category-actions {
    display: flex;
  }
}
</style>
