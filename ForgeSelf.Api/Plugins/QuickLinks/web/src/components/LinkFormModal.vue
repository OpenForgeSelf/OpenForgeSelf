<script setup lang="ts">
import { ref, computed, watch, onMounted, onUnmounted } from 'vue'
import type { QuickLink, QuickLinkCreateRequest } from '../types'
import { useQuickLinksStore } from '../store'

const props = defineProps<{
  visible: boolean
  link?: QuickLink | null
}>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'saved'): void
}>()

const quickLinksStore = useQuickLinksStore()

// 用变量绑定而非静态 src：Vite 会把静态 /logo/... 当作待解析资源，
// 而该图片由宿主 wwwroot 提供，插件构建产物中不存在，静态写法会导致构建失败。
const logoUrl = '/logo/logo-128.png'

const name = ref('')
const url = ref('')
const icon = ref('')
const description = ref('')
const categoryId = ref('')
const isSaving = ref(false)
const errors = ref<Record<string, string>>({})

const isEditMode = computed(() => !!props.link)
const modalTitle = computed(() => isEditMode.value ? '编辑链接' : '添加链接')
const categories = computed(() => quickLinksStore.sortedCategories)

// 动态新增分类：在「添加链接」弹窗内即可创建分类，避免无分类时无法提交
const showAddCategory = ref(false)
const newCategoryName = ref('')
const isAddingCategory = ref(false)
const addCategoryError = ref('')

function openAddCategory(): void {
  showAddCategory.value = true
  newCategoryName.value = ''
  addCategoryError.value = ''
}

function cancelAddCategory(): void {
  showAddCategory.value = false
  newCategoryName.value = ''
  addCategoryError.value = ''
}

async function confirmAddCategory(): Promise<void> {
  const name = newCategoryName.value.trim()
  if (!name || isAddingCategory.value) return
  isAddingCategory.value = true
  addCategoryError.value = ''
  try {
    const created = await quickLinksStore.addCategory({ name })
    // 新增后立即选中，下拉选项随 categories 响应式更新
    categoryId.value = created.id
    errors.value.categoryId = ''
    cancelAddCategory()
  } catch (e) {
    addCategoryError.value = e instanceof Error ? e.message : '新增分类失败'
    console.error('新增分类失败:', e)
  } finally {
    isAddingCategory.value = false
  }
}

function validateForm(): boolean {
  errors.value = {}

  if (!name.value.trim()) {
    errors.value.name = '请输入链接名称'
  }

  if (!url.value.trim()) {
    errors.value.url = '请输入链接地址'
  } else if (!isValidUrl(url.value.trim())) {
    errors.value.url = '请输入有效的URL地址'
  }

  if (!categoryId.value) {
    errors.value.categoryId = '请选择分类'
  }

  return Object.keys(errors.value).length === 0
}

function isValidUrl(urlStr: string): boolean {
  try {
    new URL(urlStr)
    return true
  } catch {
    return /^https?:\/\//.test(urlStr)
  }
}

async function handleSave(): Promise<void> {
  if (!validateForm()) return

  isSaving.value = true
  try {
    const linkData: QuickLinkCreateRequest = {
      name: name.value.trim(),
      url: url.value.trim(),
      icon: icon.value.trim() || undefined,
      description: description.value.trim() || undefined,
      categoryId: categoryId.value
    }

    if (isEditMode.value && props.link) {
      await quickLinksStore.updateLink(props.link.id, linkData)
    } else {
      await quickLinksStore.addLink(linkData)
    }

    emit('saved')
    handleClose()
  } catch (e) {
    console.error('保存链接失败:', e)
  } finally {
    isSaving.value = false
  }
}

function handleClose(): void {
  emit('close')
}

function handleBackdropClick(event: MouseEvent): void {
  if (event.target === event.currentTarget) {
    handleClose()
  }
}

function handleKeydown(event: KeyboardEvent): void {
  if (event.key === 'Escape') {
    handleClose()
  }
  if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) {
    handleSave()
  }
}

function resetForm(): void {
  name.value = ''
  url.value = ''
  icon.value = ''
  description.value = ''
  categoryId.value = quickLinksStore.currentCategory || (categories.value[0]?.id || '')
  errors.value = {}
}

function loadLinkData(): void {
  if (props.link) {
    name.value = props.link.name
    url.value = props.link.url
    // 后端 Icon/Description 可空，回填 null 时兜底为空串，避免保存时 trim() 崩溃
    icon.value = props.link.icon ?? ''
    description.value = props.link.description ?? ''
    categoryId.value = props.link.categoryId
  } else {
    resetForm()
  }
  errors.value = {}
}

watch(() => props.visible, (newVal) => {
  if (newVal) {
    loadLinkData()
  }
})

watch(() => props.link, () => {
  if (props.visible) {
    loadLinkData()
  }
})

onMounted(() => {
  document.addEventListener('keydown', handleKeydown)
})

onUnmounted(() => {
  document.removeEventListener('keydown', handleKeydown)
})
</script>

<template>
  <Teleport to="body">
    <Transition name="modal">
      <div
        v-if="visible"
        class="modal-overlay"
        role="dialog"
        aria-modal="true"
        :aria-labelledby="modalTitle"
        @click="handleBackdropClick"
      >
        <div class="modal-container" role="document">
          <div class="modal-header">
            <div class="header-left">
              <span class="app-logo">
                <img :src="logoUrl" alt="铸己匣" />
              </span>
              <h2 class="modal-title">{{ modalTitle }}</h2>
            </div>
            <button class="close-btn" aria-label="关闭" @click="handleClose">
              ×
            </button>
          </div>

          <form class="modal-form" @submit.prevent="handleSave">
            <div class="form-group">
              <label for="link-name" class="form-label">
                链接名称 <span class="required">*</span>
              </label>
              <input
                id="link-name"
                v-model="name"
                type="text"
                class="form-input"
                :class="{ 'has-error': errors.name }"
                placeholder="输入链接名称"
                maxlength="50"
              />
              <p v-if="errors.name" class="error-message">{{ errors.name }}</p>
            </div>

            <div class="form-group">
              <label for="link-url" class="form-label">
                链接地址 <span class="required">*</span>
              </label>
              <input
                id="link-url"
                v-model="url"
                type="text"
                class="form-input"
                :class="{ 'has-error': errors.url }"
                placeholder="https://example.com"
                maxlength="500"
              />
              <p v-if="errors.url" class="error-message">{{ errors.url }}</p>
            </div>

            <div class="form-row">
              <div class="form-group icon-group">
                <label for="link-icon" class="form-label">图标</label>
                <input
                  id="link-icon"
                  v-model="icon"
                  type="text"
                  class="form-input"
                  placeholder="🔗"
                  maxlength="10"
                />
              </div>

              <div class="form-group category-group">
                <label for="link-category" class="form-label">
                  分类 <span class="required">*</span>
                </label>
                <div class="category-select-row">
                  <select
                    id="link-category"
                    v-model="categoryId"
                    class="form-select"
                    :class="{ 'has-error': errors.categoryId }"
                  >
                    <option value="" disabled>请选择分类</option>
                    <option v-for="cat in categories" :key="cat.id" :value="cat.id">
                      {{ cat.icon ? cat.icon + ' ' : '' }}{{ cat.name }}
                    </option>
                  </select>
                  <button
                    type="button"
                    class="add-cat-btn"
                    title="新增分类"
                    aria-label="新增分类"
                    @click="openAddCategory"
                  >
                    +
                  </button>
                </div>
                <div v-if="showAddCategory" class="add-cat-inline">
                  <input
                    v-model="newCategoryName"
                    type="text"
                    class="form-input"
                    placeholder="输入新分类名称"
                    maxlength="20"
                    @keyup.enter="confirmAddCategory"
                    @keyup.esc="cancelAddCategory"
                  />
                  <button
                    type="button"
                    class="add-cat-confirm"
                    :disabled="isAddingCategory"
                    @click="confirmAddCategory"
                  >
                    ✓
                  </button>
                  <button
                    type="button"
                    class="add-cat-cancel"
                    @click="cancelAddCategory"
                  >
                    ✕
                  </button>
                </div>
                <p v-if="addCategoryError" class="error-message">{{ addCategoryError }}</p>
                <p v-if="errors.categoryId" class="error-message">{{ errors.categoryId }}</p>
              </div>
            </div>

            <div class="form-group">
              <label for="link-description" class="form-label">描述</label>
              <textarea
                id="link-description"
                v-model="description"
                class="form-textarea"
                placeholder="输入链接描述（可选）"
                rows="3"
                maxlength="200"
              />
            </div>
          </form>

          <div class="modal-footer">
            <button type="button" class="btn btn-secondary" @click="handleClose">
              取消
            </button>
            <button
              type="button"
              class="btn btn-primary"
              :disabled="isSaving"
              @click="handleSave"
            >
              <span v-if="isSaving" class="btn-loading" />
              {{ isSaving ? '保存中...' : '保存' }}
            </button>
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
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

.modal-container {
  background: var(--el-bg-color);
  border-radius: 16px;
  width: 100%;
  max-width: 480px;
  max-height: 90vh;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
}

.modal-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 20px 24px;
  border-bottom: 1px solid var(--el-border-color-light);
  flex-shrink: 0;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 8px;
}

.app-logo {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  overflow: hidden;
  border-radius: 6px;
  line-height: 0;
  width: 20px;
  height: 20px;
}

.app-logo img {
  width: 100%;
  height: 100%;
  object-fit: contain;
  display: block;
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

.modal-form {
  padding: 24px;
  overflow-y: auto;
  flex: 1;
}

.form-group {
  margin-bottom: 16px;
}

.form-row {
  display: flex;
  gap: 12px;
  margin-bottom: 16px;
}

.icon-group {
  flex: 0 0 100px;
  margin-bottom: 0;
}

.category-group {
  flex: 1;
  margin-bottom: 0;
}

.category-select-row {
  display: flex;
  gap: 8px;
  align-items: stretch;
}

.category-select-row .form-select {
  flex: 1;
}

.add-cat-btn {
  flex: 0 0 auto;
  width: 40px;
  border: 1px solid var(--el-border-color);
  border-radius: 8px;
  background: var(--el-fill-color-light);
  color: var(--el-text-color-primary);
  font-size: 20px;
  line-height: 1;
  cursor: pointer;
  transition: all 0.2s ease;
}

.add-cat-btn:hover {
  border-color: var(--el-color-primary);
  color: var(--el-color-primary);
  background: var(--el-color-primary-light-9);
}

.add-cat-inline {
  display: flex;
  gap: 6px;
  margin-top: 8px;
}

.add-cat-inline .form-input {
  flex: 1;
  padding: 8px 10px;
}

.add-cat-confirm,
.add-cat-cancel {
  flex: 0 0 auto;
  width: 32px;
  height: 36px;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  font-size: 14px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--el-fill-color-light);
  transition: all 0.2s ease;
}

.add-cat-confirm {
  color: var(--el-color-success);
}

.add-cat-confirm:hover:not(:disabled) {
  background: var(--el-fill-color);
}

.add-cat-confirm:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.add-cat-cancel {
  color: var(--el-color-danger);
}

.add-cat-cancel:hover {
  background: var(--el-fill-color);
}

.form-label {
  display: block;
  font-size: 14px;
  font-weight: 500;
  color: var(--el-text-color-primary);
  margin-bottom: 6px;
}

.required {
  color: var(--el-color-danger);
}

.form-input,
.form-select,
.form-textarea {
  width: 100%;
  padding: 10px 12px;
  border: 1px solid var(--el-border-color);
  border-radius: 8px;
  font-size: 14px;
  color: var(--el-text-color-primary);
  background: var(--el-bg-color);
  transition: all 0.2s ease;
  font-family: inherit;
}

.form-input:hover,
.form-select:hover,
.form-textarea:hover {
  border-color: var(--el-border-color);
}

.form-input:focus,
.form-select:focus,
.form-textarea:focus {
  outline: none;
  border-color: var(--el-color-primary);
  box-shadow: 0 0 0 3px var(--el-color-primary-light-8);
}

.form-input.has-error,
.form-select.has-error,
.form-textarea.has-error {
  border-color: var(--el-color-danger);
}

.form-input.has-error:focus,
.form-select.has-error:focus,
.form-textarea.has-error:focus {
  box-shadow: 0 0 0 3px color-mix(in srgb, var(--el-color-danger) 15%, transparent);
}

.form-textarea {
  resize: vertical;
  min-height: 80px;
}

.error-message {
  font-size: 12px;
  color: var(--el-color-danger);
  margin: 4px 0 0 0;
}

.modal-footer {
  padding: 16px 24px;
  border-top: 1px solid var(--el-border-color-light);
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  flex-shrink: 0;
}

.btn {
  padding: 10px 20px;
  border: none;
  border-radius: 8px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s ease;
  display: flex;
  align-items: center;
  gap: 8px;
}

.btn:disabled {
  cursor: not-allowed;
  opacity: 0.6;
}

.btn-secondary {
  background: var(--el-fill-color-light);
  color: var(--el-text-color-regular);
}

.btn-secondary:hover:not(:disabled) {
  background: var(--el-fill-color);
}

.btn-primary {
  background: var(--el-color-primary);
  color: var(--el-color-white);
}

.btn-primary:hover:not(:disabled) {
  background: var(--el-color-primary-light-3);
}

.btn-loading {
  width: 16px;
  height: 16px;
  border: 2px solid transparent;
  border-top-color: currentColor;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

.modal-enter-active,
.modal-leave-active {
  transition: opacity 0.3s ease;
}

.modal-enter-active .modal-container,
.modal-leave-active .modal-container {
  transition: transform 0.3s ease, opacity 0.3s ease;
}

.modal-enter-from,
.modal-leave-to {
  opacity: 0;
}

.modal-enter-from .modal-container,
.modal-leave-to .modal-container {
  transform: scale(0.95) translateY(-10px);
  opacity: 0;
}

@media (max-width: 640px) {
  .modal-overlay {
    padding: 0;
    align-items: stretch;
  }

  .modal-container {
    max-width: 100%;
    max-height: 100vh;
    border-radius: 0;
  }

  .modal-form {
    padding: 16px;
  }

  .modal-footer {
    padding: 12px 16px;
  }

  .form-row {
    flex-direction: column;
  }

  .icon-group {
    flex: none;
  }
}
</style>
