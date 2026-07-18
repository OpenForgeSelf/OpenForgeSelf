<template>
  <div class="code-snippet-list">
    <div class="list-header">
      <div class="search-box">
        <i class="fa-solid fa-search" />
        <input
          v-model="localKeyword"
          type="text"
          placeholder="搜索代码片段..."
          @input="onSearchInput"
        />
      </div>
      <button class="new-btn" @click="$emit('create')">
        <i class="fa-solid fa-plus" />
        新建
      </button>
    </div>

    <div class="filter-bar">
      <div class="filter-group">
        <label>语言:</label>
        <select v-model="localLanguage" @change="onFilterChange">
          <option value="">全部</option>
          <option v-for="lang in languages" :key="lang" :value="lang">{{ lang }}</option>
        </select>
      </div>

      <div class="filter-group">
        <label>分类:</label>
        <select v-model="localCategory" @change="onFilterChange">
          <option value="">全部</option>
          <option v-for="cat in categories" :key="cat" :value="cat">{{ cat }}</option>
        </select>
      </div>

      <div class="filter-group">
        <button
          class="filter-btn"
          :class="{ active: localFavorite === true }"
          @click="toggleFavoriteFilter"
        >
          <i class="fa-solid fa-star" />
          收藏
        </button>
      </div>
    </div>

    <div class="list-info">
      <span>共 {{ total }} 个代码片段</span>
    </div>

    <div v-if="!isLoading && snippets.length > 0" class="snippets-grid">
      <CodeSnippetCard
        v-for="snippet in snippets"
        :key="snippet.id"
        :snippet="snippet"
        @click="$emit('select', snippet)"
        @toggle-favorite="onToggleFavorite"
      />
    </div>

    <div v-else-if="!isLoading" class="empty-state">
      <i class="fa-solid fa-code" />
      <p>{{ searchKeyword ? '没有找到匹配的代码片段' : '还没有代码片段，点击右上角新建一个吧' }}</p>
    </div>

    <div v-else class="loading-state">
      <i class="fa-solid fa-spinner fa-spin" />
      <p>加载中...</p>
    </div>

    <div v-if="total > pageSize" class="pagination">
      <button
        class="page-btn"
        :disabled="currentPage <= 1"
        @click="goToPage(currentPage - 1)"
      >
        <i class="fa-solid fa-chevron-left" />
      </button>
      <span class="page-info">第 {{ currentPage }} 页 / 共 {{ totalPages }} 页</span>
      <button
        class="page-btn"
        :disabled="currentPage >= totalPages"
        @click="goToPage(currentPage + 1)"
      >
        <i class="fa-solid fa-chevron-right" />
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import type { CodeSnippet } from '@/types/codeSnippet'
import CodeSnippetCard from './CodeSnippetCard.vue'

const props = defineProps<{
  snippets: CodeSnippet[]
  total: number
  currentPage: number
  pageSize: number
  languages: string[]
  categories: string[]
  isLoading: boolean
  searchKeyword: string
  filterLanguage: string
  filterCategory: string
  filterFavorite: boolean | null
}>()

const emit = defineEmits<{
  select: [snippet: CodeSnippet]
  create: []
  toggleFavorite: [id: number, isFavorite: boolean]
  search: [keyword: string]
  filter: [params: { language: string; category: string; isFavorite: boolean | null }]
  pageChange: [page: number]
}>()

const localKeyword = ref(props.searchKeyword)
const localLanguage = ref(props.filterLanguage)
const localCategory = ref(props.filterCategory)
const localFavorite = ref(props.filterFavorite)

const totalPages = computed(() => Math.ceil(props.total / props.pageSize))

let searchTimer: ReturnType<typeof setTimeout> | null = null

function onSearchInput() {
  if (searchTimer) {
    clearTimeout(searchTimer)
  }
  searchTimer = setTimeout(() => {
    emit('search', localKeyword.value)
  }, 300)
}

function onFilterChange() {
  emit('filter', {
    language: localLanguage.value,
    category: localCategory.value,
    isFavorite: localFavorite.value
  })
}

function toggleFavoriteFilter() {
  if (localFavorite.value === true) {
    localFavorite.value = null
  } else {
    localFavorite.value = true
  }
  onFilterChange()
}

function onToggleFavorite(id: number, isFavorite: boolean) {
  emit('toggleFavorite', id, isFavorite)
}

function goToPage(page: number) {
  if (page >= 1 && page <= totalPages.value) {
    emit('pageChange', page)
  }
}

watch(() => props.searchKeyword, (val) => {
  localKeyword.value = val
})

watch(() => props.filterLanguage, (val) => {
  localLanguage.value = val
})

watch(() => props.filterCategory, (val) => {
  localCategory.value = val
})

watch(() => props.filterFavorite, (val) => {
  localFavorite.value = val
})
</script>

<style scoped>
.code-snippet-list {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow: hidden;
}

.list-header {
  display: flex;
  gap: 12px;
  padding: 12px;
  border-bottom: 1px solid var(--border-color, #e5e7eb);
  background: var(--bg-secondary, #f9fafb);
}

.search-box {
  flex: 1;
  position: relative;
}

.search-box i {
  position: absolute;
  left: 10px;
  top: 50%;
  transform: translateY(-50%);
  color: var(--text-muted, #9ca3af);
  font-size: 13px;
}

.search-box input {
  width: 100%;
  padding: 8px 12px 8px 32px;
  border: 1px solid var(--border-color, #d1d5db);
  border-radius: 6px;
  font-size: 13px;
  background: var(--bg-card, #fff);
  color: var(--text-primary, #1f2937);
}

.search-box input:focus {
  outline: none;
  border-color: var(--primary-color, #3b82f6);
  box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.1);
}

.new-btn {
  padding: 8px 16px;
  background: var(--primary-color, #3b82f6);
  color: white;
  border: none;
  border-radius: 6px;
  font-size: 13px;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 6px;
  white-space: nowrap;
}

.new-btn:hover {
  background: var(--primary-hover, #2563eb);
}

.filter-bar {
  display: flex;
  gap: 16px;
  padding: 10px 12px;
  border-bottom: 1px solid var(--border-color, #e5e7eb);
  background: var(--bg-card, #fff);
  flex-wrap: wrap;
}

.filter-group {
  display: flex;
  align-items: center;
  gap: 6px;
}

.filter-group label {
  font-size: 12px;
  color: var(--text-secondary, #6b7280);
}

.filter-group select {
  padding: 4px 8px;
  border: 1px solid var(--border-color, #d1d5db);
  border-radius: 4px;
  font-size: 12px;
  background: var(--bg-card, #fff);
  color: var(--text-primary, #1f2937);
  cursor: pointer;
}

.filter-btn {
  padding: 4px 10px;
  border: 1px solid var(--border-color, #d1d5db);
  border-radius: 4px;
  font-size: 12px;
  background: var(--bg-card, #fff);
  color: var(--text-secondary, #6b7280);
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 4px;
}

.filter-btn.active {
  background: #fef3c7;
  color: #d97706;
  border-color: #fcd34d;
}

.list-info {
  padding: 8px 12px;
  font-size: 12px;
  color: var(--text-muted, #9ca3af);
  background: var(--bg-secondary, #f9fafb);
  border-bottom: 1px solid var(--border-color, #e5e7eb);
}

.snippets-grid {
  flex: 1;
  overflow-y: auto;
  padding: 12px;
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 12px;
  align-content: start;
}

.empty-state {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  color: var(--text-muted, #9ca3af);
  gap: 12px;
}

.empty-state i {
  font-size: 48px;
  opacity: 0.3;
}

.empty-state p {
  margin: 0;
  font-size: 14px;
}

.loading-state {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  color: var(--text-muted, #9ca3af);
  gap: 12px;
}

.loading-state i {
  font-size: 24px;
}

.pagination {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
  padding: 12px;
  border-top: 1px solid var(--border-color, #e5e7eb);
  background: var(--bg-card, #fff);
}

.page-btn {
  padding: 6px 12px;
  border: 1px solid var(--border-color, #d1d5db);
  border-radius: 4px;
  background: var(--bg-card, #fff);
  color: var(--text-secondary, #6b7280);
  cursor: pointer;
  font-size: 12px;
}

.page-btn:hover:not(:disabled) {
  background: var(--bg-secondary, #f3f4f6);
}

.page-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.page-info {
  font-size: 12px;
  color: var(--text-secondary, #6b7280);
}
</style>
