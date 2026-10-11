<template>
  <div class="code-snippets-view">
    <PageHead eyebrow="Code Snippets · 代码片段库" title="片段 · 列表 · 详情">
      <template #actions>
        <span class="count-badge">{{ totalSnippets }} 个片段</span>
      </template>
    </PageHead>

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
        <EmptyState
          v-else
          title="选择一个代码片段"
          sub="从左侧列表选择，或新建片段"
        >
          <template #icon>
            <svg
              width="28"
              height="28"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="1.8"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <polyline points="16 18 22 12 16 6" />
              <polyline points="8 6 2 12 8 18" />
            </svg>
          </template>
        </EmptyState>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useCodeSnippetStore } from '@/stores/codeSnippet'
import { storeToRefs } from 'pinia'
import PageHead from '@/components/PageHead.vue'
import EmptyState from '@/components/EmptyState.vue'
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
}

.count-badge {
  font-family: var(--font-mono);
  font-size: 0.75rem;
  color: var(--fg-secondary);
  background: var(--paper-tint);
  border: 1px solid var(--border-default);
  padding: 2px 10px;
  border-radius: var(--radius-md);
  white-space: nowrap;
}

.view-content {
  flex: 1;
  display: flex;
  gap: 16px;
  min-height: 0;
}

.list-panel {
  width: 400px;
  min-width: 320px;
  max-width: 500px;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.detail-panel {
  flex: 1;
  min-width: 0;
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

@media (max-width: 900px) {
  .view-content {
    flex-direction: column;
  }

  .list-panel {
    width: 100%;
    max-width: none;
    height: 40%;
  }
}
</style>
