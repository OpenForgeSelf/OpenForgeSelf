<script setup lang="ts">
import { ref, computed } from 'vue'
import type { Script, ScriptLanguage } from '@/types/scriptRunner'
import ScriptCard from './ScriptCard.vue'

const props = defineProps<{
  scripts: Script[]
  categories: string[]
  isLoading?: boolean
}>()

const emit = defineEmits<{
  (e: 'search', keyword: string): void
  (e: 'filter', filters: { category?: string; language?: ScriptLanguage; isFavorite?: boolean }): void
  (e: 'create'): void
  (e: 'select', script: Script): void
  (e: 'execute', script: Script): void
  (e: 'edit', script: Script): void
  (e: 'delete', script: Script): void
  (e: 'toggleFavorite', script: Script): void
}>()

const searchKeyword = ref('')
const selectedCategory = ref('all')
const selectedLanguage = ref<ScriptLanguage | 'all'>('all')
const favoriteFilter = ref<'all' | 'favorites'>('all')
const debounceTimer = ref<number | null>(null)

const languageOptions: { value: ScriptLanguage | 'all'; label: string }[] = [
  { value: 'all', label: '全部语言' },
  { value: 'powershell', label: 'PowerShell' },
  { value: 'python', label: 'Python' },
  { value: 'nodejs', label: 'Node.js' },
  { value: 'shell', label: 'Shell' },
  { value: 'cmd', label: 'CMD' }
]

const filteredScripts = computed(() => {
  let result = [...props.scripts]

  if (searchKeyword.value.trim()) {
    const keyword = searchKeyword.value.toLowerCase().trim()
    result = result.filter(s =>
      s.name.toLowerCase().includes(keyword) ||
      s.description?.toLowerCase().includes(keyword) ||
      s.tags.some(t => t.toLowerCase().includes(keyword))
    )
  }

  if (selectedCategory.value !== 'all') {
    result = result.filter(s => s.category === selectedCategory.value)
  }

  if (selectedLanguage.value !== 'all') {
    result = result.filter(s => s.language === selectedLanguage.value)
  }

  if (favoriteFilter.value === 'favorites') {
    result = result.filter(s => s.isFavorite)
  }

  return result
})

function handleSearchInput(): void {
  if (debounceTimer.value) {
    clearTimeout(debounceTimer.value)
  }
  debounceTimer.value = window.setTimeout(() => {
    emit('search', searchKeyword.value.trim())
  }, 300)
}

function handleCategoryChange(category: string): void {
  selectedCategory.value = category
  emitFilters()
}

function handleLanguageChange(): void {
  emitFilters()
}

function handleFavoriteChange(filter: 'all' | 'favorites'): void {
  favoriteFilter.value = filter
  emitFilters()
}

function emitFilters(): void {
  const filters: { category?: string; language?: ScriptLanguage; isFavorite?: boolean } = {}
  if (selectedCategory.value !== 'all') {
    filters.category = selectedCategory.value
  }
  if (selectedLanguage.value !== 'all') {
    filters.language = selectedLanguage.value
  }
  if (favoriteFilter.value === 'favorites') {
    filters.isFavorite = true
  }
  emit('filter', filters)
}

function handleCreate(): void {
  emit('create')
}

function handleCardClick(script: Script): void {
  emit('select', script)
}

function handleExecute(script: Script): void {
  emit('execute', script)
}

function handleEdit(script: Script): void {
  emit('edit', script)
}

function handleDelete(script: Script): void {
  emit('delete', script)
}

function handleToggleFavorite(script: Script): void {
  emit('toggleFavorite', script)
}
</script>

<template>
  <div class="script-list-panel">
    <div class="panel-header">
      <div class="header-title">
        <h2>脚本库</h2>
        <span class="script-count">{{ scripts.length }} 个脚本</span>
      </div>
      <button class="btn btn-primary" @click="handleCreate">
        + 新建脚本
      </button>
    </div>

    <div class="search-section">
      <div class="search-bar">
        <span class="search-icon">🔍</span>
        <input
          v-model="searchKeyword"
          type="text"
          class="search-input"
          placeholder="搜索脚本名称、描述或标签..."
          aria-label="搜索脚本"
          @input="handleSearchInput"
        />
        <button
          v-if="searchKeyword"
          class="clear-btn"
          aria-label="清除搜索"
          @click="searchKeyword = ''; handleSearchInput()"
        >
          ×
        </button>
      </div>
    </div>

    <div class="filters-section">
      <div class="filter-row">
        <div class="filter-tabs">
          <button
            class="filter-tab"
            :class="{ active: favoriteFilter === 'all' }"
            @click="handleFavoriteChange('all')"
          >
            全部
          </button>
          <button
            class="filter-tab"
            :class="{ active: favoriteFilter === 'favorites' }"
            @click="handleFavoriteChange('favorites')"
          >
            ⭐ 收藏
          </button>
        </div>
      </div>

      <div class="filter-row">
        <select
          v-model="selectedCategory"
          class="filter-select"
          aria-label="按分类筛选"
          @change="handleCategoryChange(selectedCategory)"
        >
          <option value="all">全部分类</option>
          <option v-for="cat in categories" :key="cat" :value="cat">
            {{ cat }}
          </option>
        </select>

        <select
          v-model="selectedLanguage"
          class="filter-select"
          aria-label="按语言筛选"
          @change="handleLanguageChange"
        >
          <option v-for="opt in languageOptions" :key="opt.value" :value="opt.value">
            {{ opt.label }}
          </option>
        </select>
      </div>
    </div>

    <div class="results-info">
      <span v-if="!isLoading">
        显示 {{ filteredScripts.length }} 个结果
      </span>
      <span v-else>加载中...</span>
    </div>

    <div class="scripts-list">
      <TransitionGroup name="card-list">
        <ScriptCard
          v-for="script in filteredScripts"
          :key="script.id"
          :script="script"
          @click="handleCardClick"
          @execute="handleExecute"
          @edit="handleEdit"
          @delete="handleDelete"
          @toggle-favorite="handleToggleFavorite"
        />
      </TransitionGroup>
    </div>

    <div v-if="isLoading" class="loading-state">
      <div class="loading-spinner" />
      <p>加载脚本中...</p>
    </div>

    <div v-else-if="filteredScripts.length === 0" class="empty-state">
      <div class="empty-icon">📜</div>
      <h3>没有找到脚本</h3>
      <p v-if="searchKeyword || selectedCategory !== 'all' || selectedLanguage !== 'all' || favoriteFilter === 'favorites'">
        试试调整搜索条件或筛选条件
      </p>
      <p v-else>
        还没有脚本，点击"新建脚本"开始创建吧
      </p>
      <div class="empty-actions">
        <button class="btn btn-primary" @click="handleCreate">
          + 新建脚本
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.script-list-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: #f8f9fa;
  border-right: 1px solid #e9ecef;
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 20px;
  background: #fff;
  border-bottom: 1px solid #e9ecef;
}

.header-title {
  display: flex;
  align-items: baseline;
  gap: 10px;
}

.header-title h2 {
  font-size: 18px;
  font-weight: 600;
  color: #212529;
  margin: 0;
}

.script-count {
  font-size: 13px;
  color: #6c757d;
}

.btn {
  padding: 8px 16px;
  border-radius: 8px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s ease;
  border: 1px solid transparent;
}

.btn-primary {
  background: #1976d2;
  color: #fff;
  border-color: #1976d2;
}

.btn-primary:hover {
  background: #1565c0;
  border-color: #1565c0;
}

.search-section {
  padding: 12px 20px;
  background: #fff;
  border-bottom: 1px solid #e9ecef;
}

.search-bar {
  position: relative;
  display: flex;
  align-items: center;
  background: #fff;
  border: 1px solid #e9ecef;
  border-radius: 10px;
  padding: 0 12px;
  transition: border-color 0.2s ease, box-shadow 0.2s ease;
}

.search-bar:focus-within {
  border-color: #1976d2;
  box-shadow: 0 0 0 3px rgba(25, 118, 210, 0.1);
}

.search-icon {
  font-size: 18px;
  margin-right: 10px;
  flex-shrink: 0;
}

.search-input {
  flex: 1;
  height: 40px;
  border: none;
  background: transparent;
  font-size: 14px;
  color: #212529;
}

.search-input::placeholder {
  color: #adb5bd;
}

.clear-btn {
  width: 24px;
  height: 24px;
  border: none;
  background: #e9ecef;
  border-radius: 50%;
  font-size: 16px;
  color: #6c757d;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  transition: all 0.2s ease;
}

.clear-btn:hover {
  background: #dee2e6;
  color: #495057;
}

.filters-section {
  padding: 12px 20px;
  background: #fff;
  border-bottom: 1px solid #e9ecef;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.filter-row {
  display: flex;
  gap: 10px;
  flex-wrap: wrap;
}

.filter-tabs {
  display: flex;
  gap: 4px;
  background: #f1f3f5;
  padding: 3px;
  border-radius: 8px;
}

.filter-tab {
  padding: 6px 14px;
  background: none;
  border: none;
  font-size: 13px;
  font-weight: 500;
  color: #6c757d;
  cursor: pointer;
  border-radius: 6px;
  transition: all 0.2s ease;
}

.filter-tab:hover {
  color: #495057;
}

.filter-tab.active {
  background: #fff;
  color: #1976d2;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.05);
}

.filter-select {
  padding: 8px 12px;
  border: 1px solid #dee2e6;
  border-radius: 8px;
  font-size: 13px;
  color: #495057;
  background: #fff;
  cursor: pointer;
  transition: border-color 0.2s ease;
}

.filter-select:focus {
  outline: none;
  border-color: #1976d2;
  box-shadow: 0 0 0 3px rgba(25, 118, 210, 0.1);
}

.results-info {
  padding: 10px 20px;
  font-size: 13px;
  color: #6c757d;
  background: #f8f9fa;
}

.scripts-list {
  flex: 1;
  overflow-y: auto;
  padding: 12px 20px;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.loading-state,
.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 40px 20px;
  text-align: center;
}

.loading-spinner {
  width: 36px;
  height: 36px;
  border: 3px solid #f1f3f5;
  border-top-color: #1976d2;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
  margin-bottom: 12px;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

.loading-state p {
  color: #6c757d;
  font-size: 14px;
  margin: 0;
}

.empty-icon {
  font-size: 56px;
  margin-bottom: 12px;
}

.empty-state h3 {
  font-size: 16px;
  font-weight: 600;
  color: #212529;
  margin: 0 0 6px 0;
}

.empty-state p {
  color: #6c757d;
  font-size: 14px;
  margin: 0 0 20px 0;
}

.empty-actions {
  display: flex;
  gap: 10px;
}

.card-list-enter-active,
.card-list-leave-active {
  transition: all 0.3s ease;
}

.card-list-enter-from {
  opacity: 0;
  transform: translateY(20px);
}

.card-list-leave-to {
  opacity: 0;
  transform: scale(0.95);
}

.card-list-move {
  transition: transform 0.3s ease;
}

@media (max-width: 768px) {
  .panel-header {
    padding: 12px 16px;
  }

  .header-title h2 {
    font-size: 16px;
  }

  .search-section,
  .filters-section {
    padding: 10px 16px;
  }

  .scripts-list {
    padding: 10px 16px;
  }
}
</style>
