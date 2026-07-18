<template>
  <div class="code-snippets-view">
    <div class="view-header">
      <h2>
        <i class="fa-solid fa-code" />
        代码片段库
      </h2>
      <p class="view-subtitle">管理和复用你的常用代码片段</p>
    </div>

    <div class="view-content">
      <div class="list-panel">
        <CodeSnippetList
          :snippets="snippets"
          :total="totalSnippets"
          :current-page="currentPage"
          :page-size="pageSize"
          :languages="languages"
          :categories="categories"
          :is-loading="isLoading"
          :search-keyword="searchKeyword"
          :filter-language="filterLanguage"
          :filter-category="filterCategory"
          :filter-favorite="filterFavorite"
          @select="onSelectSnippet"
          @create="onCreateSnippet"
          @toggle-favorite="onToggleFavorite"
          @search="onSearch"
          @filter="onFilter"
          @page-change="onPageChange"
        />
      </div>

      <div class="detail-panel">
        <div v-if="isEditing" class="editor-wrapper">
          <CodeSnippetEditor
            :snippet="editingSnippet"
            :categories="categories"
            :is-saving="isSaving"
            @save="onSaveSnippet"
            @cancel="onCancelEdit"
          />
        </div>
        <div v-else-if="currentSnippet" class="viewer-wrapper">
          <CodeSnippetViewer
            :snippet="currentSnippet"
            @edit="onStartEdit"
            @delete="onDeleteSnippet"
            @toggle-favorite="onToggleFavorite"
          />
        </div>
        <div v-else class="empty-detail">
          <i class="fa-solid fa-code" />
          <p>选择一个代码片段查看详情</p>
          <p class="hint">或点击"新建"按钮创建新的代码片段</p>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useCodeSnippetStore } from '@/stores/codeSnippet'
import { storeToRefs } from 'pinia'
import CodeSnippetList from '@/components/scriptrunner/CodeSnippetList.vue'
import CodeSnippetViewer from '@/components/scriptrunner/CodeSnippetViewer.vue'
import CodeSnippetEditor from '@/components/scriptrunner/CodeSnippetEditor.vue'
import type { CodeSnippet, CreateCodeSnippetRequest, UpdateCodeSnippetRequest } from '@/types/codeSnippet'

const codeSnippetStore = useCodeSnippetStore()

const {
  snippets,
  currentSnippet,
  languages,
  categories,
  isLoading,
  totalSnippets,
  currentPage,
  pageSize,
  searchKeyword,
  filterLanguage,
  filterCategory,
  filterFavorite
} = storeToRefs(codeSnippetStore)

const isEditing = ref(false)
const editingSnippet = ref<CodeSnippet | null>(null)
const isSaving = ref(false)

async function loadSnippets() {
  await codeSnippetStore.loadSnippets({
    keyword: searchKeyword.value,
    language: filterLanguage.value,
    category: filterCategory.value,
    isFavorite: filterFavorite.value ?? undefined,
    page: currentPage.value,
    pageSize: pageSize.value
  })
}

function onSelectSnippet(snippet: CodeSnippet) {
  isEditing.value = false
  codeSnippetStore.loadSnippet(snippet.id)
}

function onCreateSnippet() {
  editingSnippet.value = null
  isEditing.value = true
}

function onStartEdit() {
  editingSnippet.value = currentSnippet.value
  isEditing.value = true
}

function onCancelEdit() {
  isEditing.value = false
  editingSnippet.value = null
}

async function onSaveSnippet(data: CreateCodeSnippetRequest | UpdateCodeSnippetRequest) {
  isSaving.value = true
  try {
    if (editingSnippet.value?.id) {
      await codeSnippetStore.updateSnippet(editingSnippet.value.id, data)
    } else {
      await codeSnippetStore.createSnippet(data as CreateCodeSnippetRequest)
    }
    isEditing.value = false
    editingSnippet.value = null
    await loadSnippets()
  } catch (e) {
    console.error('保存失败:', e)
  } finally {
    isSaving.value = false
  }
}

async function onDeleteSnippet(id: number) {
  const success = await codeSnippetStore.deleteSnippet(id)
  if (success) {
    await loadSnippets()
  }
}

async function onToggleFavorite(id: number, isFavorite: boolean) {
  await codeSnippetStore.favoriteSnippet(id, isFavorite)
}

async function onSearch(keyword: string) {
  codeSnippetStore.setSearchKeyword(keyword)
  codeSnippetStore.currentPage = 1
  await loadSnippets()
}

async function onFilter(params: { language: string; category: string; isFavorite: boolean | null }) {
  codeSnippetStore.setFilterLanguage(params.language)
  codeSnippetStore.setFilterCategory(params.category)
  codeSnippetStore.setFilterFavorite(params.isFavorite)
  codeSnippetStore.currentPage = 1
  await loadSnippets()
}

async function onPageChange(page: number) {
  codeSnippetStore.currentPage = page
  await loadSnippets()
}

onMounted(async () => {
  await codeSnippetStore.loadLanguages()
  await codeSnippetStore.loadCategories()
  await loadSnippets()
})
</script>

<style scoped>
.code-snippets-view {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow: hidden;
  background: var(--bg-primary, #f9fafb);
}

.view-header {
  padding: 20px 24px;
  background: var(--bg-card, #fff);
  border-bottom: 1px solid var(--border-color, #e5e7eb);
}

.view-header h2 {
  margin: 0 0 4px 0;
  font-size: 20px;
  font-weight: 600;
  color: var(--text-primary, #1f2937);
  display: flex;
  align-items: center;
  gap: 10px;
}

.view-header h2 i {
  color: var(--primary-color, #3b82f6);
}

.view-subtitle {
  margin: 0;
  font-size: 14px;
  color: var(--text-secondary, #6b7280);
}

.view-content {
  flex: 1;
  display: flex;
  overflow: hidden;
}

.list-panel {
  width: 400px;
  min-width: 320px;
  max-width: 500px;
  border-right: 1px solid var(--border-color, #e5e7eb);
  background: var(--bg-card, #fff);
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.detail-panel {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.editor-wrapper,
.viewer-wrapper {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.empty-detail {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  color: var(--text-muted, #9ca3af);
  gap: 12px;
}

.empty-detail i {
  font-size: 64px;
  opacity: 0.2;
}

.empty-detail p {
  margin: 0;
  font-size: 14px;
}

.empty-detail .hint {
  font-size: 12px;
  opacity: 0.7;
}
</style>
