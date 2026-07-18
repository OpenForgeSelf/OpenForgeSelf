<template>
  <div class="memory-view">
    <div class="memory-header">
      <h1 class="page-title">
        <i class="fa-solid fa-brain" />
        记忆管理
      </h1>
      <p class="page-subtitle">管理你的 AI 记忆，让 AI 更懂你</p>
    </div>

    <div class="memory-content">
      <div class="memory-sidebar">
        <div class="sidebar-section">
          <div class="sidebar-header">
            <span>分类</span>
            <button class="btn-icon" title="添加分类" @click="showAddCategory = true">
              <i class="fa-solid fa-plus" />
            </button>
          </div>
          <div class="category-list">
            <div
              class="category-item"
              :class="{ active: currentCategory === null }"
              @click="setCurrentCategory(null)"
            >
              <i class="fa-solid fa-layer-group" />
              <span class="category-name">全部记忆</span>
              <span class="category-count">{{ stats?.totalMemories || 0 }}</span>
            </div>
            <div
              v-for="cat in sortedCategories"
              :key="cat.id"
              class="category-item"
              :class="{ active: currentCategory === cat.id }"
              @click="setCurrentCategory(cat.id)"
            >
              <i :class="cat.icon || 'fa-folder'" />
              <span class="category-name">{{ cat.name }}</span>
              <span class="category-count">{{ cat.memoryCount }}</span>
            </div>
          </div>
        </div>

        <div class="sidebar-section">
          <div class="sidebar-header">
            <span>类型筛选</span>
          </div>
          <div class="filter-list">
            <div
              class="filter-item"
              :class="{ active: filterType === null }"
              @click="setFilterType(null)"
            >
              全部类型
            </div>
            <div
              v-for="(label, key) in memoryTypeLabels"
              :key="key"
              class="filter-item"
              :class="{ active: filterType === asMemoryType(Number(key)) }"
              @click="setFilterType(asMemoryType(Number(key)))"
            >
              {{ label }}
            </div>
          </div>
        </div>

        <div class="sidebar-section">
          <div class="sidebar-header">
            <span>重要程度</span>
          </div>
          <div class="filter-list">
            <div
              class="filter-item"
              :class="{ active: filterImportance === null }"
              @click="setFilterImportance(null)"
            >
              全部
            </div>
            <div
              v-for="(label, key) in memoryImportanceLabels"
              :key="key"
              class="filter-item"
              :class="{ active: filterImportance === asMemoryImportance(Number(key)) }"
              @click="setFilterImportance(asMemoryImportance(Number(key)))"
            >
              <span
                class="importance-dot"
                :style="{ backgroundColor: memoryImportanceColors[asMemoryImportance(Number(key))] }"
              />
              {{ label }}
            </div>
          </div>
        </div>
      </div>

      <div class="memory-main">
        <div class="memory-toolbar">
          <div class="search-box">
            <i class="fa-solid fa-search" />
            <input
              v-model="searchKeyword"
              type="text"
              placeholder="搜索记忆..."
              @input="debounceSearch"
            />
          </div>
          <div class="toolbar-actions">
            <button class="btn btn-primary" @click="showAddMemory = true">
              <i class="fa-solid fa-plus" />
              新建记忆
            </button>
          </div>
        </div>

        <div class="memory-stats-bar">
          <div class="stat-item">
            <span class="stat-label">总计</span>
            <span class="stat-value">{{ total }}</span>
          </div>
          <div class="stat-item">
            <span class="stat-label">今日访问</span>
            <span class="stat-value">{{ stats?.todayAccessed || 0 }}</span>
          </div>
          <div class="stat-item">
            <span class="stat-label">本周访问</span>
            <span class="stat-value">{{ stats?.weekAccessed || 0 }}</span>
          </div>
        </div>

        <div v-loading="loading" class="memory-list">
          <div
            v-for="memory in memories"
            :key="memory.id"
            class="memory-card"
            @click="selectMemory(memory)"
          >
            <div class="memory-card-header">
              <h3 class="memory-title">{{ memory.title }}</h3>
              <div class="memory-importance">
                <span
                  class="importance-badge"
                  :style="{ backgroundColor: memoryImportanceColors[memory.importance] }"
                >
                  {{ memoryImportanceLabels[memory.importance] }}
                </span>
              </div>
            </div>
            <p class="memory-content-preview">{{ memory.content.slice(0, 100) }}{{ memory.content.length > 100 ? '...' : '' }}</p>
            <div class="memory-tags">
              <span v-for="tag in memory.tags.slice(0, 3)" :key="tag" class="memory-tag">
                {{ tag }}
              </span>
              <span v-if="memory.tags.length > 3" class="memory-tag-more">
                +{{ memory.tags.length - 3 }}
              </span>
            </div>
            <div class="memory-card-footer">
              <span class="memory-type">{{ memoryTypeLabels[memory.type] }}</span>
              <span class="memory-category">{{ memory.categoryName || '未分类' }}</span>
              <span class="memory-access">
                <i class="fa-solid fa-eye" />
                {{ memory.accessCount }}
              </span>
            </div>
          </div>

          <div v-if="!loading && memories.length === 0" class="empty-state">
            <i class="fa-solid fa-brain empty-icon" />
            <p>还没有记忆</p>
            <p class="empty-hint">点击"新建记忆"来添加你的第一条记忆</p>
          </div>
        </div>

        <div v-if="total > pageSize" class="pagination">
          <button
            class="btn-page"
            :disabled="currentPage <= 1"
            @click="changePage(currentPage - 1)"
          >
            <i class="fa-solid fa-chevron-left" />
          </button>
          <span class="page-info">第 {{ currentPage }} / {{ Math.ceil(total / pageSize) }} 页</span>
          <button
            class="btn-page"
            :disabled="currentPage >= Math.ceil(total / pageSize)"
            @click="changePage(currentPage + 1)"
          >
            <i class="fa-solid fa-chevron-right" />
          </button>
        </div>
      </div>
    </div>

    <div v-if="showAddMemory" class="modal-overlay" @click.self="showAddMemory = false">
      <div class="modal modal-lg">
        <div class="modal-header">
          <h3>新建记忆</h3>
          <button class="btn-icon" @click="showAddMemory = false">
            <i class="fa-solid fa-xmark" />
          </button>
        </div>
        <div class="modal-body">
          <div class="form-group">
            <label>标题 *</label>
            <input v-model="newMemory.title" type="text" placeholder="输入记忆标题" />
          </div>
          <div class="form-row">
            <div class="form-group">
              <label>类型</label>
              <select v-model="newMemory.type">
                <option v-for="(label, key) in memoryTypeLabels" :key="key" :value="Number(key)">
                  {{ label }}
                </option>
              </select>
            </div>
            <div class="form-group">
              <label>重要程度</label>
              <select v-model="newMemory.importance">
                <option v-for="(label, key) in memoryImportanceLabels" :key="key" :value="Number(key)">
                  {{ label }}
                </option>
              </select>
            </div>
          </div>
          <div class="form-row">
            <div class="form-group">
              <label>分类</label>
              <select v-model="newMemory.categoryId">
                <option :value="null">未分类</option>
                <option v-for="cat in sortedCategories" :key="cat.id" :value="cat.id">
                  {{ cat.name }}
                </option>
              </select>
            </div>
            <div class="form-group">
              <label>标签（逗号分隔）</label>
              <input v-model="newMemory.tagsInput" type="text" placeholder="标签1, 标签2" />
            </div>
          </div>
          <div class="form-group">
            <label>内容 *</label>
            <textarea v-model="newMemory.content" rows="6" placeholder="输入记忆内容..." />
          </div>
        </div>
        <div class="modal-footer">
          <button class="btn" @click="showAddMemory = false">取消</button>
          <button class="btn btn-primary" :disabled="!newMemory.title || !newMemory.content" @click="handleAddMemory">
            保存
          </button>
        </div>
      </div>
    </div>

    <div v-if="showAddCategory" class="modal-overlay" @click.self="showAddCategory = false">
      <div class="modal">
        <div class="modal-header">
          <h3>新建分类</h3>
          <button class="btn-icon" @click="showAddCategory = false">
            <i class="fa-solid fa-xmark" />
          </button>
        </div>
        <div class="modal-body">
          <div class="form-group">
            <label>分类名称 *</label>
            <input v-model="newCategory.name" type="text" placeholder="输入分类名称" />
          </div>
          <div class="form-group">
            <label>描述</label>
            <input v-model="newCategory.description" type="text" placeholder="分类描述（可选）" />
          </div>
          <div class="form-group">
            <label>图标</label>
            <input v-model="newCategory.icon" type="text" placeholder="FontAwesome 图标类名" />
          </div>
        </div>
        <div class="modal-footer">
          <button class="btn" @click="showAddCategory = false">取消</button>
          <button class="btn btn-primary" :disabled="!newCategory.name" @click="handleAddCategory">
            创建
          </button>
        </div>
      </div>
    </div>

    <div v-if="selectedMemory" class="memory-detail-drawer" :class="{ open: showDetail }">
      <div class="drawer-header">
        <h3>{{ selectedMemory.title }}</h3>
        <button class="btn-icon" @click="showDetail = false">
          <i class="fa-solid fa-xmark" />
        </button>
      </div>
      <div class="drawer-body">
        <div class="detail-meta">
          <span class="detail-badge" :style="{ backgroundColor: memoryImportanceColors[selectedMemory.importance] }">
            {{ memoryImportanceLabels[selectedMemory.importance] }}
          </span>
          <span class="detail-type">{{ memoryTypeLabels[selectedMemory.type] }}</span>
          <span class="detail-category">{{ selectedMemory.categoryName || '未分类' }}</span>
        </div>
        <div class="detail-tags">
          <span v-for="tag in selectedMemory.tags" :key="tag" class="memory-tag">{{ tag }}</span>
        </div>
        <div class="detail-content">
          <h4>内容</h4>
          <p>{{ selectedMemory.content }}</p>
        </div>
        <div class="detail-info">
          <div class="info-item">
            <span class="info-label">访问次数</span>
            <span class="info-value">{{ selectedMemory.accessCount }}</span>
          </div>
          <div class="info-item">
            <span class="info-label">创建时间</span>
            <span class="info-value">{{ formatDate(selectedMemory.createdAt) }}</span>
          </div>
          <div class="info-item">
            <span class="info-label">最后访问</span>
            <span class="info-value">{{ selectedMemory.lastAccessedAt ? formatDate(selectedMemory.lastAccessedAt) : '从未' }}</span>
          </div>
        </div>
      </div>
      <div class="drawer-footer">
        <button class="btn btn-danger" @click="handleDeleteMemory">
          <i class="fa-solid fa-trash" />
          删除
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useMemoryStore } from '@/stores/memory'
import {
  MemoryType,
  MemoryImportance,
  memoryTypeLabels,
  memoryImportanceLabels,
  memoryImportanceColors,
  type Memory,
  type CreateMemoryRequest
} from '@/types/memory'

const store = useMemoryStore()

function asMemoryType(val: number): MemoryType {
  return val as MemoryType
}

function asMemoryImportance(val: number): MemoryImportance {
  return val as MemoryImportance
}

const {
  memories,
  stats,
  currentCategory,
  loading,
  searchKeyword,
  filterType,
  filterImportance,
  total,
  currentPage,
  pageSize,
  sortedCategories,
  loadMemories,
  loadCategories,
  loadStats,
  addMemory,
  removeMemory,
  addCategory,
  setCurrentCategory,
  setSearchKeyword,
  setFilterType,
  setFilterImportance,
  setPage
} = store

const showAddMemory = ref(false)
const showAddCategory = ref(false)
const showDetail = ref(false)
const selectedMemory = ref<Memory | null>(null)

const newMemory = ref({
  title: '',
  content: '',
  type: MemoryType.Fact,
  importance: MemoryImportance.Medium,
  categoryId: null as number | null,
  tagsInput: ''
})

const newCategory = ref({
  name: '',
  description: '',
  icon: 'fa-folder'
})

let searchTimer: ReturnType<typeof setTimeout> | null = null

function debounceSearch() {
  if (searchTimer) clearTimeout(searchTimer)
  searchTimer = setTimeout(() => {
    setSearchKeyword(searchKeyword)
    loadMemories()
  }, 300)
}

function changePage(page: number) {
  setPage(page)
  loadMemories()
}

function selectMemory(memory: Memory) {
  selectedMemory.value = memory
  showDetail.value = true
}

async function handleAddMemory() {
  const tags = newMemory.value.tagsInput
    .split(',')
    .map(t => t.trim())
    .filter(t => t.length > 0)

  const request: CreateMemoryRequest = {
    title: newMemory.value.title,
    content: newMemory.value.content,
    type: newMemory.value.type,
    importance: newMemory.value.importance,
    categoryId: newMemory.value.categoryId ?? undefined,
    tags: tags.length > 0 ? tags : undefined
  }

  await addMemory(request)
  showAddMemory.value = false
  resetNewMemory()
  loadMemories()
  loadStats()
}

function resetNewMemory() {
  newMemory.value = {
    title: '',
    content: '',
    type: MemoryType.Fact,
    importance: MemoryImportance.Medium,
    categoryId: null,
    tagsInput: ''
  }
}

async function handleAddCategory() {
  await addCategory(newCategory.value.name, newCategory.value.description, newCategory.value.icon)
  showAddCategory.value = false
  newCategory.value = { name: '', description: '', icon: 'fa-folder' }
  loadCategories()
}

async function handleDeleteMemory() {
  if (!selectedMemory.value) return
  if (!confirm('确定要删除这条记忆吗？')) return

  await removeMemory(selectedMemory.value.id)
  showDetail.value = false
  selectedMemory.value = null
  loadMemories()
  loadStats()
}

function formatDate(dateStr: string): string {
  const date = new Date(dateStr)
  return date.toLocaleString('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit'
  })
}

onMounted(async () => {
  await Promise.all([
    loadCategories(),
    loadMemories(),
    loadStats()
  ])
})
</script>

<style scoped>
.memory-view {
  height: 100%;
  display: flex;
  flex-direction: column;
  background: var(--bg-primary);
}

.memory-header {
  padding: 20px 24px;
  border-bottom: 1px solid var(--border-color);
}

.page-title {
  font-size: 24px;
  font-weight: 600;
  margin: 0 0 4px 0;
  color: var(--text-primary);
  display: flex;
  align-items: center;
  gap: 10px;
}

.page-subtitle {
  margin: 0;
  color: var(--text-secondary);
  font-size: 14px;
}

.memory-content {
  flex: 1;
  display: flex;
  overflow: hidden;
}

.memory-sidebar {
  width: 240px;
  border-right: 1px solid var(--border-color);
  padding: 16px;
  overflow-y: auto;
  background: var(--bg-secondary);
}

.sidebar-section {
  margin-bottom: 24px;
}

.sidebar-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 13px;
  font-weight: 600;
  color: var(--text-secondary);
  margin-bottom: 10px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.category-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.category-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px 10px;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s;
  color: var(--text-secondary);
  font-size: 14px;
}

.category-item:hover {
  background: var(--bg-hover);
  color: var(--text-primary);
}

.category-item.active {
  background: var(--primary-color);
  color: white;
}

.category-name {
  flex: 1;
}

.category-count {
  font-size: 12px;
  background: rgba(255, 255, 255, 0.15);
  padding: 2px 6px;
  border-radius: 10px;
}

.filter-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.filter-item {
  padding: 6px 10px;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s;
  color: var(--text-secondary);
  font-size: 13px;
  display: flex;
  align-items: center;
  gap: 8px;
}

.filter-item:hover {
  background: var(--bg-hover);
  color: var(--text-primary);
}

.filter-item.active {
  background: var(--primary-color);
  color: white;
}

.importance-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  display: inline-block;
}

.memory-main {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.memory-toolbar {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 16px 24px;
  border-bottom: 1px solid var(--border-color);
}

.search-box {
  flex: 1;
  max-width: 400px;
  position: relative;
}

.search-box i {
  position: absolute;
  left: 12px;
  top: 50%;
  transform: translateY(-50%);
  color: var(--text-muted);
}

.search-box input {
  width: 100%;
  padding: 8px 12px 8px 36px;
  border: 1px solid var(--border-color);
  border-radius: 8px;
  background: var(--bg-primary);
  color: var(--text-primary);
  font-size: 14px;
}

.search-box input:focus {
  outline: none;
  border-color: var(--primary-color);
}

.toolbar-actions {
  display: flex;
  gap: 8px;
}

.memory-stats-bar {
  display: flex;
  gap: 24px;
  padding: 12px 24px;
  border-bottom: 1px solid var(--border-color);
  background: var(--bg-secondary);
}

.stat-item {
  display: flex;
  align-items: center;
  gap: 8px;
}

.stat-label {
  font-size: 13px;
  color: var(--text-secondary);
}

.stat-value {
  font-size: 16px;
  font-weight: 600;
  color: var(--text-primary);
}

.memory-list {
  flex: 1;
  overflow-y: auto;
  padding: 20px 24px;
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 16px;
  align-content: start;
}

.memory-card {
  background: var(--bg-primary);
  border: 1px solid var(--border-color);
  border-radius: 10px;
  padding: 16px;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.memory-card:hover {
  border-color: var(--primary-color);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
  transform: translateY(-2px);
}

.memory-card-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 10px;
}

.memory-title {
  font-size: 16px;
  font-weight: 600;
  margin: 0;
  color: var(--text-primary);
  flex: 1;
}

.importance-badge {
  font-size: 11px;
  padding: 2px 8px;
  border-radius: 10px;
  color: white;
  white-space: nowrap;
}

.memory-content-preview {
  font-size: 13px;
  color: var(--text-secondary);
  margin: 0;
  line-height: 1.5;
  display: -webkit-box;
  -webkit-line-clamp: 3;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.memory-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.memory-tag {
  font-size: 12px;
  padding: 2px 8px;
  background: var(--bg-secondary);
  color: var(--text-secondary);
  border-radius: 4px;
}

.memory-tag-more {
  font-size: 12px;
  color: var(--text-muted);
}

.memory-card-footer {
  display: flex;
  align-items: center;
  gap: 10px;
  font-size: 12px;
  color: var(--text-muted);
  margin-top: auto;
  padding-top: 10px;
  border-top: 1px solid var(--border-color);
}

.memory-type {
  background: var(--bg-secondary);
  padding: 2px 6px;
  border-radius: 4px;
}

.memory-access {
  margin-left: auto;
  display: flex;
  align-items: center;
  gap: 4px;
}

.empty-state {
  grid-column: 1 / -1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 60px 20px;
  color: var(--text-muted);
}

.empty-icon {
  font-size: 48px;
  margin-bottom: 16px;
  opacity: 0.3;
}

.empty-hint {
  font-size: 13px;
  margin: 4px 0 0 0;
}

.pagination {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 16px;
  padding: 16px 24px;
  border-top: 1px solid var(--border-color);
}

.page-info {
  font-size: 14px;
  color: var(--text-secondary);
}

.btn-page {
  width: 32px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  border: 1px solid var(--border-color);
  border-radius: 6px;
  background: var(--bg-primary);
  color: var(--text-primary);
  cursor: pointer;
  transition: all 0.2s;
}

.btn-page:hover:not(:disabled) {
  border-color: var(--primary-color);
  color: var(--primary-color);
}

.btn-page:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.modal {
  background: var(--bg-primary);
  border-radius: 12px;
  width: 90%;
  max-width: 500px;
  max-height: 80vh;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.modal-lg {
  max-width: 600px;
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 20px;
  border-bottom: 1px solid var(--border-color);
}

.modal-header h3 {
  margin: 0;
  font-size: 18px;
  color: var(--text-primary);
}

.modal-body {
  flex: 1;
  padding: 20px;
  overflow-y: auto;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  padding: 16px 20px;
  border-top: 1px solid var(--border-color);
}

.form-group {
  margin-bottom: 16px;
}

.form-group label {
  display: block;
  font-size: 13px;
  font-weight: 500;
  color: var(--text-secondary);
  margin-bottom: 6px;
}

.form-group input,
.form-group select,
.form-group textarea {
  width: 100%;
  padding: 8px 12px;
  border: 1px solid var(--border-color);
  border-radius: 8px;
  background: var(--bg-primary);
  color: var(--text-primary);
  font-size: 14px;
}

.form-group input:focus,
.form-group select:focus,
.form-group textarea:focus {
  outline: none;
  border-color: var(--primary-color);
}

.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}

.memory-detail-drawer {
  position: fixed;
  top: 0;
  right: -400px;
  width: 400px;
  height: 100%;
  background: var(--bg-primary);
  border-left: 1px solid var(--border-color);
  display: flex;
  flex-direction: column;
  z-index: 1001;
  transition: right 0.3s ease;
}

.memory-detail-drawer.open {
  right: 0;
}

.drawer-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 20px;
  border-bottom: 1px solid var(--border-color);
}

.drawer-header h3 {
  margin: 0;
  font-size: 18px;
  color: var(--text-primary);
  flex: 1;
  padding-right: 12px;
}

.drawer-body {
  flex: 1;
  padding: 20px;
  overflow-y: auto;
}

.detail-meta {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
  margin-bottom: 16px;
}

.detail-badge {
  font-size: 12px;
  padding: 2px 10px;
  border-radius: 12px;
  color: white;
}

.detail-type,
.detail-category {
  font-size: 12px;
  padding: 2px 10px;
  background: var(--bg-secondary);
  color: var(--text-secondary);
  border-radius: 12px;
}

.detail-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-bottom: 20px;
}

.detail-content h4 {
  font-size: 14px;
  color: var(--text-secondary);
  margin: 0 0 8px 0;
}

.detail-content p {
  font-size: 14px;
  color: var(--text-primary);
  line-height: 1.6;
  white-space: pre-wrap;
}

.detail-info {
  margin-top: 24px;
  padding-top: 20px;
  border-top: 1px solid var(--border-color);
}

.info-item {
  display: flex;
  justify-content: space-between;
  padding: 8px 0;
  font-size: 13px;
}

.info-label {
  color: var(--text-secondary);
}

.info-value {
  color: var(--text-primary);
}

.drawer-footer {
  padding: 16px 20px;
  border-top: 1px solid var(--border-color);
}

.btn {
  padding: 8px 16px;
  border: 1px solid var(--border-color);
  border-radius: 8px;
  background: var(--bg-primary);
  color: var(--text-primary);
  font-size: 14px;
  cursor: pointer;
  transition: all 0.2s;
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.btn:hover {
  background: var(--bg-hover);
}

.btn-primary {
  background: var(--primary-color);
  border-color: var(--primary-color);
  color: white;
}

.btn-primary:hover {
  background: var(--primary-hover);
  border-color: var(--primary-hover);
}

.btn-primary:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-danger {
  background: var(--danger-color);
  border-color: var(--danger-color);
  color: white;
  width: 100%;
  justify-content: center;
}

.btn-danger:hover {
  background: rgba(185, 28, 28, 0.8);
  border-color: var(--danger-color);
}

.btn-icon {
  width: 28px;
  height: 28px;
  display: flex;
  align-items: center;
  justify-content: center;
  border: none;
  background: transparent;
  color: var(--text-secondary);
  cursor: pointer;
  border-radius: 6px;
  transition: all 0.2s;
}

.btn-icon:hover {
  background: var(--bg-hover);
  color: var(--text-primary);
}
</style>
