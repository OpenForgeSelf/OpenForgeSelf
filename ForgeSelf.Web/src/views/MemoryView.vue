<script setup lang="ts">
import { ref, onMounted } from 'vue'
import AppLogo from '@/components/AppLogo.vue'
import PageHead from '@/components/PageHead.vue'
import ForgeCard from '@/components/ForgeCard.vue'
import EmptyState from '@/components/EmptyState.vue'
import { storeToRefs } from 'pinia'
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

// state 用 storeToRefs 保持响应性（直接解构 setup store 会丢失响应性，列表不更新）
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
  sortedCategories
} = storeToRefs(store)

const {
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
    setSearchKeyword(searchKeyword.value)
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

// 空态 brain 图标（lucide brain）
const brainIconSvg = `<svg xmlns="http://www.w3.org/2000/svg" width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5a3 3 0 1 0-5.997.125 4 4 0 0 0-2.526 5.77 4 4 0 0 0 .556 6.588A4 4 0 1 0 12 18Z"/><path d="M12 5a3 3 0 1 1 5.997.125 4 4 0 0 1 2.526 5.77 4 4 0 0 1-.556 6.588A4 4 0 1 1 12 18Z"/><path d="M15 13a4.5 4.5 0 0 1-3-4 4.5 4.5 0 0 1-3 4"/></svg>`
</script>

<template>
  <div class="memory-view">
    <div class="page-content">
      <PageHead
        eyebrow="Memory · 记忆"
        title="记忆管理"
      >
        <template #actions>
          <span class="stat-chip">总计 <strong>{{ total }}</strong></span>
          <span class="stat-chip">今日 <strong>{{ stats?.todayAccessed || 0 }}</strong></span>
          <span class="stat-chip">本周 <strong>{{ stats?.weekAccessed || 0 }}</strong></span>
          <button class="btn btn-primary" @click="showAddMemory = true">
            <i class="fa-solid fa-plus" />
            新建记忆
          </button>
        </template>
      </PageHead>

      <div class="memory-layout">
        <!-- ===== Sidebar: categories + filters ===== -->
        <ForgeCard class="memory-sidebar" :padding="16">
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
        </ForgeCard>

        <!-- ===== Main: search + list + pagination ===== -->
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
          </div>

          <div v-loading="loading" class="memory-list">
            <ForgeCard
              v-for="memory in memories"
              :key="memory.id"
              class="memory-card"
              :interactive="true"
              :padding="16"
              @click="selectMemory(memory)"
            >
              <div class="memory-card-header">
                <h3 class="memory-title">{{ memory.title }}</h3>
                <span
                  class="importance-badge"
                  :style="{ backgroundColor: memoryImportanceColors[memory.importance] }"
                >
                  {{ memoryImportanceLabels[memory.importance] }}
                </span>
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
            </ForgeCard>

            <!-- 空态：匣中沉淀·尚未开始（DESIGN.md Provider 卡规格） -->
            <EmptyState
              v-if="!loading && memories.length === 0"
              class="memory-empty"
              :icon="brainIconSvg"
              title="匣中沉淀·尚未开始"
              sub="0 KB · 尚未沉淀任何记忆"
            >
              <template #action>
                <button class="btn btn-primary" @click="showAddMemory = true">
                  <i class="fa-solid fa-plus" />
                  新建记忆
                </button>
              </template>
            </EmptyState>
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
    </div>

    <!-- ===== Modals ===== -->
    <div v-if="showAddMemory" class="modal-overlay" @click.self="showAddMemory = false">
      <div class="modal modal-lg">
        <div class="modal-header">
          <div class="flex items-center gap-2">
            <AppLogo :size="20" />
            <h3>新建记忆</h3>
          </div>
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
          <div class="flex items-center gap-2">
            <AppLogo :size="20" />
            <h3>新建分类</h3>
          </div>
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

    <!-- ===== Detail drawer ===== -->
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

<style scoped>
.memory-view {
  height: 100%;
  overflow-y: auto;
  padding: 24px;
}

.page-content {
  max-width: 1200px;
  margin: 0 auto;
}

/* ===== Layout ===== */
.memory-layout {
  display: flex;
  gap: 16px;
  align-items: flex-start;
}

.memory-sidebar {
  width: 240px;
  flex-shrink: 0;
  position: sticky;
  top: 0;
  max-height: calc(100vh - 120px);
  overflow-y: auto;
}

.sidebar-section {
  margin-bottom: 24px;
}

.sidebar-section:last-child {
  margin-bottom: 0;
}

.sidebar-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 10px;
  font-weight: 600;
  color: var(--fg-tertiary);
  margin-bottom: 10px;
  text-transform: uppercase;
  letter-spacing: 0.18em;
  font-family: var(--font-mono);
}

.category-list,
.filter-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.category-item,
.filter-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px 10px;
  border-radius: var(--radius-md);
  cursor: pointer;
  transition: background var(--dur-fast) var(--ease-out), color var(--dur-fast) var(--ease-out);
  color: var(--fg-secondary);
  font-size: 13px;
}

.category-item:hover,
.filter-item:hover {
  background: var(--paper-tint);
  color: var(--fg-primary);
}

.category-item.active,
.filter-item.active {
  background: var(--gold-tint-strong);
  color: var(--gold-400);
}

.category-name {
  flex: 1;
}

.category-count {
  font-size: 11px;
  font-family: var(--font-mono);
  background: var(--paper-tint);
  padding: 2px 8px;
  border-radius: var(--radius-sm);
}

.importance-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  display: inline-block;
  flex-shrink: 0;
}

/* ===== Main area ===== */
.memory-main {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.memory-toolbar {
  display: flex;
  align-items: center;
}

.search-box {
  flex: 1;
  max-width: 420px;
  position: relative;
}

.search-box i {
  position: absolute;
  left: 12px;
  top: 50%;
  transform: translateY(-50%);
  color: var(--fg-tertiary);
}

.search-box input {
  width: 100%;
  padding: 8px 12px 8px 36px;
  border: 1px solid var(--border-default);
  border-radius: var(--radius-md);
  background: var(--glass-bg);
  color: var(--fg-primary);
  font-size: 14px;
  transition: border-color var(--dur-fast) var(--ease-out);
}

.search-box input:focus {
  outline: none;
  border-color: var(--gold-400);
}

.search-box input::placeholder {
  color: var(--fg-muted);
}

/* ===== Stats chips in PageHead actions ===== */
.stat-chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px 10px;
  font-size: 11px;
  font-family: var(--font-mono);
  color: var(--fg-secondary);
  background: var(--paper-tint);
  border-radius: var(--radius-md);
}

.stat-chip strong {
  color: var(--gold-400);
  font-weight: 600;
}

/* ===== Memory list grid ===== */
.memory-list {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 16px;
  align-content: start;
  min-height: 200px;
}

.memory-card {
  cursor: pointer;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.memory-card-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 10px;
}

.memory-title {
  font-size: 15px;
  font-weight: 600;
  margin: 0;
  color: var(--fg-primary);
  flex: 1;
}

.importance-badge {
  font-size: 11px;
  padding: 2px 8px;
  border-radius: var(--radius-sm);
  color: var(--fg-primary);
  white-space: nowrap;
}

.memory-content-preview {
  font-size: 13px;
  color: var(--fg-secondary);
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
  font-size: 11px;
  padding: 2px 8px;
  background: var(--paper-tint);
  color: var(--fg-secondary);
  border-radius: var(--radius-sm);
}

.memory-tag-more {
  font-size: 11px;
  color: var(--fg-tertiary);
}

.memory-card-footer {
  display: flex;
  align-items: center;
  gap: 10px;
  font-size: 11px;
  color: var(--fg-tertiary);
  margin-top: auto;
  padding-top: 10px;
  border-top: 1px solid var(--border-hairline);
}

.memory-type {
  background: var(--paper-tint);
  padding: 2px 6px;
  border-radius: var(--radius-sm);
}

.memory-access {
  margin-left: auto;
  display: flex;
  align-items: center;
  gap: 4px;
}

.memory-empty {
  grid-column: 1 / -1;
}

/* ===== Pagination ===== */
.pagination {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 16px;
}

.page-info {
  font-size: 13px;
  color: var(--fg-secondary);
  font-family: var(--font-mono);
}

.btn-page {
  width: 32px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  border: 1px solid var(--border-default);
  border-radius: var(--radius-md);
  background: transparent;
  color: var(--fg-primary);
  cursor: pointer;
  transition: all var(--dur-fast) var(--ease-out);
}

.btn-page:hover:not(:disabled) {
  border-color: var(--gold-400);
  color: var(--gold-400);
}

.btn-page:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

/* ===== Modals ===== */
.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: var(--ink-veil);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.modal {
  background: var(--el-bg-color-overlay);
  border-radius: var(--glass-radius);
  width: 90%;
  max-width: 500px;
  max-height: 80vh;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  border: 1px solid var(--border-default);
}

.modal-lg {
  max-width: 600px;
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 20px;
  border-bottom: 1px solid var(--border-hairline);
}

.modal-header h3 {
  margin: 0;
  font-size: 16px;
  color: var(--fg-primary);
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
  border-top: 1px solid var(--border-hairline);
}

.form-group {
  margin-bottom: 16px;
}

.form-group label {
  display: block;
  font-size: 13px;
  font-weight: 500;
  color: var(--fg-secondary);
  margin-bottom: 6px;
}

.form-group input,
.form-group select,
.form-group textarea {
  width: 100%;
  padding: 8px 12px;
  border: 1px solid var(--border-default);
  border-radius: var(--radius-md);
  background: var(--el-bg-color);
  color: var(--fg-primary);
  font-size: 14px;
}

.form-group input:focus,
.form-group select:focus,
.form-group textarea:focus {
  outline: none;
  border-color: var(--gold-400);
}

.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}

/* ===== Detail drawer ===== */
.memory-detail-drawer {
  position: fixed;
  top: 0;
  right: -440px;
  width: 400px;
  height: 100%;
  background: var(--el-bg-color-overlay);
  border-left: 1px solid var(--border-default);
  display: flex;
  flex-direction: column;
  z-index: 1001;
  transition: right var(--dur-slow) var(--ease-out);
}

.memory-detail-drawer.open {
  right: 0;
}

.drawer-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 20px;
  border-bottom: 1px solid var(--border-hairline);
}

.drawer-header h3 {
  margin: 0;
  font-size: 16px;
  color: var(--fg-primary);
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
  font-size: 11px;
  padding: 2px 10px;
  border-radius: var(--radius-sm);
  color: var(--fg-primary);
}

.detail-type,
.detail-category {
  font-size: 11px;
  padding: 2px 10px;
  background: var(--paper-tint);
  color: var(--fg-secondary);
  border-radius: var(--radius-sm);
}

.detail-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-bottom: 20px;
}

.detail-content h4 {
  font-size: 13px;
  color: var(--fg-secondary);
  margin: 0 0 8px 0;
}

.detail-content p {
  font-size: 14px;
  color: var(--fg-primary);
  line-height: 1.6;
  white-space: pre-wrap;
}

.detail-info {
  margin-top: 24px;
  padding-top: 20px;
  border-top: 1px solid var(--border-hairline);
}

.info-item {
  display: flex;
  justify-content: space-between;
  padding: 8px 0;
  font-size: 13px;
}

.info-label {
  color: var(--fg-secondary);
}

.info-value {
  color: var(--fg-primary);
  font-family: var(--font-mono);
  font-size: 12px;
}

.drawer-footer {
  padding: 16px 20px;
  border-top: 1px solid var(--border-hairline);
}

/* ===== Buttons ===== */
.btn {
  padding: 8px 16px;
  border: 1px solid var(--border-default);
  border-radius: var(--radius-md);
  background: transparent;
  color: var(--fg-primary);
  font-size: 13px;
  cursor: pointer;
  transition: all var(--dur-fast) var(--ease-out);
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.btn:hover {
  background: var(--paper-tint);
}

.btn-primary {
  background: var(--gold-400);
  border-color: var(--gold-400);
  color: var(--ink-950);
  font-weight: 500;
}

.btn-primary:hover {
  background: var(--gold-300);
}

.btn-primary:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-danger {
  background: var(--cinnabar-400);
  border-color: var(--cinnabar-400);
  color: var(--fg-primary);
  width: 100%;
  justify-content: center;
}

.btn-danger:hover {
  background: color-mix(in srgb, var(--cinnabar-400) 80%, transparent);
}

.btn-icon {
  width: 28px;
  height: 28px;
  display: flex;
  align-items: center;
  justify-content: center;
  border: none;
  background: transparent;
  color: var(--fg-secondary);
  cursor: pointer;
  border-radius: var(--radius-md);
  transition: all var(--dur-fast) var(--ease-out);
}

.btn-icon:hover {
  background: var(--paper-tint);
  color: var(--fg-primary);
}

/* ===== Responsive ===== */
@media (max-width: 900px) {
  .memory-layout {
    flex-direction: column;
  }
  .memory-sidebar {
    width: 100%;
    position: static;
    max-height: none;
  }
}
</style>
