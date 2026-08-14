<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import AppLogo from '@/components/AppLogo.vue'
import type { QuickLink, QuickLinkCreateRequest, ImportMode } from '@/types/quickLinks'
import { useQuickLinksStore } from '@/stores/quickLinks'
import LinkCard from '@/components/quicklinks/LinkCard.vue'
import CategoryManager from '@/components/quicklinks/CategoryManager.vue'
import LinkFormModal from '@/components/quicklinks/LinkFormModal.vue'

const quickLinksStore = useQuickLinksStore()

const showFormModal = ref(false)
const editingLink = ref<QuickLink | null>(null)
const showCategoryManager = ref(false)
const draggedLinkId = ref<string | null>(null)
const dragOverIndex = ref<number | null>(null)
const fileInputRef = ref<HTMLInputElement | null>(null)
const importMode = ref<ImportMode>('append')
const showImportModal = ref(false)

const sortedLinks = computed(() => quickLinksStore.sortedLinks)

async function loadData(): Promise<void> {
  await Promise.all([
    quickLinksStore.loadLinks(),
    quickLinksStore.loadCategories()
  ])
}

function handleSearch(event: Event): void {
  const target = event.target as HTMLInputElement
  quickLinksStore.setSearchKeyword(target.value)
}

function openAddModal(): void {
  editingLink.value = null
  showFormModal.value = true
}

function openEditModal(link: QuickLink): void {
  editingLink.value = link
  showFormModal.value = true
}

function closeFormModal(): void {
  showFormModal.value = false
  editingLink.value = null
}

async function handleDelete(link: QuickLink): Promise<void> {
  if (!confirm(`确定要删除链接"${link.name}"吗？`)) {
    return
  }

  try {
    await quickLinksStore.removeLink(link.id)
  } catch (e) {
    console.error('删除链接失败:', e)
  }
}

function handleDragStart(link: QuickLink): void {
  draggedLinkId.value = link.id
}

function handleDragEnd(): void {
  draggedLinkId.value = null
  dragOverIndex.value = null
}

function handleDragOver(event: DragEvent, index: number): void {
  event.preventDefault()
  if (event.dataTransfer) {
    event.dataTransfer.dropEffect = 'move'
  }
  dragOverIndex.value = index
}

function handleDragLeave(): void {
  dragOverIndex.value = null
}

async function handleDrop(event: DragEvent, targetIndex: number): Promise<void> {
  event.preventDefault()
  dragOverIndex.value = null

  if (!draggedLinkId.value) return

  const links = sortedLinks.value
  const draggedIndex = links.findIndex(l => l.id === draggedLinkId.value)

  if (draggedIndex === -1 || draggedIndex === targetIndex) {
    draggedLinkId.value = null
    return
  }

  const newLinks = [...links]
  const [removed] = newLinks.splice(draggedIndex, 1)
  newLinks.splice(targetIndex, 0, removed)

  const orderedIds = newLinks.map(l => l.id)

  try {
    await quickLinksStore.reorder(orderedIds)
  } catch (e) {
    console.error('重新排序失败:', e)
  }

  draggedLinkId.value = null
}

function triggerImport(): void {
  showImportModal.value = true
}

function handleFileSelect(event: Event): void {
  const target = event.target as HTMLInputElement
  const file = target.files?.[0]
  if (!file) return

  const reader = new FileReader()
  reader.onload = async (e) => {
    try {
      const content = e.target?.result as string
      const data = JSON.parse(content)

      let linksToImport: QuickLinkCreateRequest[] = []
      if (Array.isArray(data)) {
        linksToImport = data
      } else if (data.links && Array.isArray(data.links)) {
        linksToImport = data.links
      }

      if (linksToImport.length === 0) {
        alert('未找到有效的链接数据')
        return
      }

      await quickLinksStore.importData(linksToImport, importMode.value)
      showImportModal.value = false
    } catch (err) {
      console.error('导入失败:', err)
      alert('导入失败：文件格式不正确')
    }
  }
  reader.readAsText(file)

  if (fileInputRef.value) {
    fileInputRef.value.value = ''
  }
}

async function handleExport(): Promise<void> {
  try {
    const links = await quickLinksStore.exportData(
      quickLinksStore.currentCategory || undefined
    )

    const dataStr = JSON.stringify(links, null, 2)
    const blob = new Blob([dataStr], { type: 'application/json' })
    const url = URL.createObjectURL(blob)

    const a = document.createElement('a')
    a.href = url
    a.download = `quick-links-${new Date().toISOString().split('T')[0]}.json`
    document.body.appendChild(a)
    a.click()
    document.body.removeChild(a)
    URL.revokeObjectURL(url)
  } catch (e) {
    console.error('导出失败:', e)
    alert('导出失败')
  }
}

function closeImportModal(): void {
  showImportModal.value = false
}

function toggleCategoryManager(): void {
  showCategoryManager.value = !showCategoryManager.value
}

onMounted(() => {
  loadData()
})
</script>

<template>
  <div class="quick-links-view">
    <header class="view-header">
      <div class="header-left">
        <button
          v-if="!showCategoryManager"
          class="menu-btn"
          aria-label="显示分类"
          @click="toggleCategoryManager"
        >
          ☰
        </button>
        <h1 class="view-title">快捷链接</h1>
      </div>

      <div class="header-center">
        <div class="search-box">
          <span class="search-icon">🔍</span>
          <input
            type="text"
            class="search-input"
            placeholder="搜索链接..."
            :value="quickLinksStore.searchKeyword"
            @input="handleSearch"
          />
        </div>
      </div>

      <div class="header-right">
        <button class="action-btn import-btn" @click="triggerImport">
          <span>📥</span>
          <span class="btn-text">导入</span>
        </button>
        <button class="action-btn export-btn" @click="handleExport">
          <span>📤</span>
          <span class="btn-text">导出</span>
        </button>
        <button class="action-btn add-btn" @click="openAddModal">
          <span>+</span>
          <span class="btn-text">添加链接</span>
        </button>
      </div>
    </header>

    <div class="view-content">
      <aside v-if="showCategoryManager" class="sidebar">
        <CategoryManager @close="toggleCategoryManager" />
      </aside>

      <main class="main-content">
        <div v-if="quickLinksStore.loading" class="loading-state">
          <div class="loading-spinner" />
          <span>加载中...</span>
        </div>

        <div v-else-if="quickLinksStore.error" class="error-state">
          <span class="error-icon">⚠️</span>
          <p>{{ quickLinksStore.error }}</p>
          <button class="retry-btn" @click="loadData">重试</button>
        </div>

        <div v-else-if="sortedLinks.length === 0" class="empty-state">
          <span class="empty-icon">🔗</span>
          <h3>暂无链接</h3>
          <p>点击"添加链接"按钮创建你的第一个快捷链接</p>
          <button class="add-first-btn" @click="openAddModal">
            + 添加链接
          </button>
        </div>

        <div v-else class="links-grid">
          <div
            v-for="(link, index) in sortedLinks"
            :key="link.id"
            class="grid-item"
            :class="{
              'drag-over': dragOverIndex === index,
              'dragging': draggedLinkId === link.id
            }"
            @dragover="handleDragOver($event, index)"
            @dragleave="handleDragLeave"
            @drop="handleDrop($event, index)"
          >
            <LinkCard
              :link="link"
              :draggable="true"
              @edit="openEditModal"
              @delete="handleDelete"
              @dragstart="handleDragStart"
              @dragend="handleDragEnd"
            />
          </div>
        </div>
      </main>
    </div>

    <LinkFormModal
      :visible="showFormModal"
      :link="editingLink"
      @close="closeFormModal"
      @saved="closeFormModal"
    />

    <Teleport to="body">
      <Transition name="modal">
        <div
          v-if="showImportModal"
          class="modal-overlay"
          role="dialog"
          aria-modal="true"
          @click.self="closeImportModal"
        >
          <div class="modal-container import-modal">
            <div class="modal-header">
              <div class="flex items-center gap-2">
                <AppLogo :size="20" />
                <h2 class="modal-title">导入链接</h2>
              </div>
              <button class="close-btn" aria-label="关闭" @click="closeImportModal">
                ×
              </button>
            </div>

            <div class="modal-body">
              <div class="import-options">
                <h4>导入方式</h4>
                <div class="mode-selector">
                  <label class="mode-option">
                    <input
                      v-model="importMode"
                      type="radio"
                      value="append"
                    />
                    <span class="mode-label">追加模式</span>
                    <span class="mode-desc">添加到现有链接之后</span>
                  </label>
                  <label class="mode-option">
                    <input
                      v-model="importMode"
                      type="radio"
                      value="replace"
                    />
                    <span class="mode-label">替换模式</span>
                    <span class="mode-desc">替换所有现有链接</span>
                  </label>
                </div>
              </div>

              <div class="file-upload-area">
                <input
                  ref="fileInputRef"
                  type="file"
                  accept=".json"
                  class="file-input"
                  @change="handleFileSelect"
                />
                <div class="upload-placeholder">
                  <span class="upload-icon">📁</span>
                  <p>点击选择 JSON 文件</p>
                  <p class="upload-hint">支持 .json 格式的链接数据</p>
                </div>
              </div>
            </div>
          </div>
        </div>
      </Transition>
    </Teleport>
  </div>
</template>

<style scoped>
.quick-links-view {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: var(--el-bg-color);
}

.view-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px 24px;
  background: var(--el-bg-color);
  border-bottom: 1px solid var(--el-border-color);
  gap: 16px;
  flex-shrink: 0;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-shrink: 0;
}

.menu-btn {
  display: none;
  width: 36px;
  height: 36px;
  border: none;
  background: var(--el-fill-color-light);
  border-radius: 8px;
  font-size: 18px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.menu-btn:hover {
  background: var(--el-fill-color);
}

.view-title {
  font-size: 20px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 0;
}

.header-center {
  flex: 1;
  max-width: 400px;
}

.search-box {
  position: relative;
  display: flex;
  align-items: center;
}

.search-icon {
  position: absolute;
  left: 12px;
  font-size: 14px;
  z-index: 1;
}

.search-input {
  width: 100%;
  padding: 10px 12px 10px 36px;
  border: 1px solid var(--el-border-color);
  border-radius: 8px;
  font-size: 14px;
  background: var(--el-bg-color-page);
  transition: all 0.2s ease;
}

.search-input:hover {
  border-color: var(--border-strong);
  background: var(--el-bg-color);
}

.search-input:focus {
  outline: none;
  border-color: var(--el-color-primary);
  background: var(--el-bg-color);
  box-shadow: 0 0 0 3px var(--primary-light);
}

.header-right {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
}

.action-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 14px;
  border: none;
  border-radius: 8px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s ease;
}

.import-btn,
.export-btn {
  background: var(--el-fill-color-light);
  color: var(--el-text-color-regular);
}

.import-btn:hover,
.export-btn:hover {
  background: var(--el-fill-color);
}

.add-btn {
  background: var(--el-color-primary);
  color: var(--el-color-white);
}

.add-btn:hover {
  background: var(--el-color-primary-light-3);
}

.view-content {
  display: flex;
  flex: 1;
  overflow: hidden;
}

.sidebar {
  width: 280px;
  flex-shrink: 0;
  border-right: 1px solid var(--el-border-color);
  background: var(--el-bg-color);
  padding: 16px;
  overflow-y: auto;
}

.main-content {
  flex: 1;
  padding: 24px;
  overflow-y: auto;
}

.loading-state,
.error-state,
.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 60px 24px;
  text-align: center;
  color: var(--el-text-color-secondary);
}

.loading-spinner {
  width: 40px;
  height: 40px;
  border: 3px solid var(--el-fill-color-light);
  border-top-color: var(--el-color-primary);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
  margin-bottom: 16px;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

.error-icon {
  font-size: 48px;
  margin-bottom: 16px;
}

.error-state p {
  margin: 0 0 16px 0;
  color: var(--el-color-danger);
}

.retry-btn {
  padding: 8px 20px;
  border: none;
  border-radius: 8px;
  background: var(--el-color-primary);
  color: var(--el-color-white);
  font-size: 14px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.retry-btn:hover {
  background: var(--el-color-primary-light-3);
}

.empty-icon {
  font-size: 64px;
  margin-bottom: 16px;
}

.empty-state h3 {
  font-size: 18px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 0 0 8px 0;
}

.empty-state p {
  margin: 0 0 24px 0;
  font-size: 14px;
}

.add-first-btn {
  padding: 12px 24px;
  border: none;
  border-radius: 8px;
  background: var(--el-color-primary);
  color: var(--el-color-white);
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s ease;
}

.add-first-btn:hover {
  background: var(--el-color-primary-light-3);
}

.links-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 16px;
}

.grid-item {
  transition: all 0.2s ease;
}

.grid-item.drag-over {
  transform: scale(1.02);
}

.grid-item.dragging {
  opacity: 0.5;
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
  padding: 20px;
}

.import-modal {
  max-width: 440px;
}

.modal-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 20px 24px;
  border-bottom: 1px solid var(--el-border-color-light);
}

.modal-title {
  font-size: 18px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 0;
}

.close-btn {
  width: 32px;
  height: 32px;
  border: none;
  background: var(--el-fill-color-light);
  border-radius: 8px;
  font-size: 20px;
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

.modal-body {
  padding: 24px;
}

.import-options {
  margin-bottom: 24px;
}

.import-options h4 {
  font-size: 14px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 0 0 12px 0;
}

.mode-selector {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.mode-option {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 12px;
  border: 1px solid var(--el-border-color);
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.mode-option:hover {
  border-color: var(--el-color-primary);
  background: var(--el-color-primary-light-9);
}

.mode-option input[type="radio"] {
  width: 18px;
  height: 18px;
  cursor: pointer;
}

.mode-label {
  font-size: 14px;
  font-weight: 500;
  color: var(--el-text-color-primary);
}

.mode-desc {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  margin-left: auto;
}

.file-upload-area {
  position: relative;
  border: 2px dashed var(--el-border-color);
  border-radius: 12px;
  overflow: hidden;
  transition: all 0.2s ease;
}

.file-upload-area:hover {
  border-color: var(--el-color-primary);
  background: var(--el-color-primary-light-9);
}

.file-input {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  opacity: 0;
  cursor: pointer;
}

.upload-placeholder {
  padding: 40px 24px;
  text-align: center;
  color: var(--el-text-color-secondary);
  pointer-events: none;
}

.upload-icon {
  font-size: 48px;
  display: block;
  margin-bottom: 12px;
}

.upload-placeholder p {
  margin: 0 0 4px 0;
  font-size: 14px;
  color: var(--el-text-color-regular);
}

.upload-hint {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.modal-enter-active,
.modal-leave-active {
  transition: opacity 0.3s ease;
}

.modal-enter-active .import-modal,
.modal-leave-active .import-modal {
  transition: transform 0.3s ease, opacity 0.3s ease;
}

.modal-enter-from,
.modal-leave-to {
  opacity: 0;
}

.modal-enter-from .import-modal,
.modal-leave-to .import-modal {
  transform: scale(0.95) translateY(-10px);
  opacity: 0;
}

@media (max-width: 768px) {
  .view-header {
    padding: 12px 16px;
    flex-wrap: wrap;
    gap: 12px;
  }

  .menu-btn {
    display: flex;
    align-items: center;
    justify-content: center;
  }

  .header-center {
    order: 3;
    max-width: 100%;
    flex-basis: 100%;
  }

  .btn-text {
    display: none;
  }

  .action-btn {
    padding: 8px 10px;
  }

  .sidebar {
    position: absolute;
    left: 0;
    top: 0;
    bottom: 0;
    z-index: 100;
    width: 260px;
    box-shadow: 2px 0 8px rgba(0, 0, 0, 0.1);
    transform: translateX(-100%);
    transition: transform 0.3s ease;
  }

  .sidebar-enter-active,
  .sidebar-leave-active {
    transform: translateX(0);
  }

  .main-content {
    padding: 16px;
  }

  .links-grid {
    grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
    gap: 12px;
  }
}

@media (max-width: 480px) {
  .links-grid {
    grid-template-columns: 1fr;
  }
}
</style>