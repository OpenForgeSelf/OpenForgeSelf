<script setup lang="ts">
import { ref, computed } from 'vue'
import { useFileToolsStore } from '@/stores/fileTools'
import { fileToolsApi } from '@/services/fileToolsApi'
import type { CleanupFilterType } from '@/types/fileTools'

const store = useFileToolsStore()
const showConfirmDialog = ref(false)
const deletePermanently = ref(false)
const cleanEmptyFolders = ref(false)
const cleanDuplicates = ref(false)

const filterTypeOptions: { type: CleanupFilterType; label: string; icon: string }[] = [
  { type: 'extension', label: '按扩展名', icon: '📄' },
  { type: 'size', label: '按大小', icon: '📏' },
  { type: 'dateCreated', label: '按创建时间', icon: '📅' },
  { type: 'dateModified', label: '按修改时间', icon: '🕐' }
]

const allSelected = computed(() => {
  if (store.cleanupPreview.length === 0) return false
  return store.cleanupPreview.every(item => item.selected)
})

const selectedCount = computed(() => {
  return store.cleanupPreview.filter(item => item.selected).length
})

function getFilterLabel(filterType: CleanupFilterType): string {
  const option = filterTypeOptions.find(o => o.type === filterType)
  return option ? `${option.icon} ${option.label}` : filterType
}

function setDemoDirectory(): void {
  store.setSelectedDirectory('C:/Users/User/Documents')
}

function toggleSelectAll(): void {
  store.selectAllCleanupItems(!allSelected.value)
}

function handleExecute(): void {
  showConfirmDialog.value = true
}

async function confirmExecute(): Promise<void> {
  showConfirmDialog.value = false
  const result = await store.executeCleanup(deletePermanently.value)
  if (result.success > 0) {
    store.cleanupPreview = []
  }
}

function cancelExecute(): void {
  showConfirmDialog.value = false
}

function getConditionOptions(filterType: CleanupFilterType): { value: string; label: string }[] {
  switch (filterType) {
    case 'extension':
      return [
        { value: 'include', label: '包含' },
        { value: 'exclude', label: '排除' }
      ]
    case 'size':
      return [
        { value: 'greater', label: '大于' },
        { value: 'less', label: '小于' }
      ]
    case 'dateCreated':
    case 'dateModified':
      return [
        { value: 'before', label: '在此之前' },
        { value: 'after', label: '在此之后' }
      ]
    default:
      return []
  }
}
</script>

<template>
  <div class="cleanup-panel">
    <div class="panel-grid">
      <div class="panel-section rules-section">
        <div class="section-header">
          <h3 class="section-title">⚙️ 清理规则</h3>
        </div>

        <div class="directory-input">
          <label class="form-label">目标目录</label>
          <div class="input-group">
            <input
              type="text"
              class="form-input"
              :value="store.selectedDirectory"
              placeholder="选择或输入目录路径"
              @input="(e) => store.setSelectedDirectory((e.target as HTMLInputElement).value)"
            />
            <button class="btn btn-sm btn-secondary" @click="setDemoDirectory">
              示例
            </button>
          </div>
        </div>

        <div class="add-rule-bar">
          <select
            class="rule-type-select"
            @change="(e) => {
              const target = e.target as HTMLSelectElement
              if (target.value) {
                store.addCleanupRule(target.value as CleanupFilterType)
                target.value = ''
              }
            }"
          >
            <option value="">+ 添加筛选规则...</option>
            <option v-for="opt in filterTypeOptions" :key="opt.type" :value="opt.type">
              {{ opt.icon }} {{ opt.label }}
            </option>
          </select>
        </div>

        <div v-if="store.cleanupRules.length === 0" class="empty-rules">
          <div class="empty-icon">📋</div>
          <div class="empty-text">暂无筛选规则</div>
          <div class="empty-hint">添加规则来指定要清理的文件</div>
        </div>

        <div v-else class="rules-list">
          <div
            v-for="(rule, index) in store.cleanupRules"
            :key="rule.id"
            class="rule-card"
            :class="{ disabled: !rule.enabled }"
          >
            <div class="rule-header">
              <div class="rule-title">
                <span class="rule-order">{{ index + 1 }}</span>
                {{ getFilterLabel(rule.filterType) }}
              </div>
              <div class="rule-actions">
                <button
                  class="icon-btn toggle-btn"
                  :class="{ active: rule.enabled }"
                  aria-label="启用/禁用"
                  @click="store.updateCleanupRule(rule.id, { enabled: !rule.enabled })"
                >
                  {{ rule.enabled ? '✓' : '○' }}
                </button>
                <button
                  class="icon-btn delete-btn"
                  aria-label="删除"
                  @click="store.removeCleanupRule(rule.id)"
                >
                  🗑️
                </button>
              </div>
            </div>

            <div class="rule-body">
              <template v-if="rule.filterType === 'extension'">
                <div class="form-row">
                  <label class="form-label">筛选方式</label>
                  <select
                    class="form-select"
                    :value="rule.condition"
                    @change="(e) => store.updateCleanupRule(rule.id, { condition: (e.target as HTMLSelectElement).value })"
                  >
                    <option v-for="opt in getConditionOptions(rule.filterType)" :key="opt.value" :value="opt.value">
                      {{ opt.label }}
                    </option>
                  </select>
                </div>
                <div class="form-row">
                  <label class="form-label">扩展名列表</label>
                  <input
                    type="text"
                    class="form-input"
                    :value="rule.value"
                    placeholder="用逗号分隔，如: tmp,log,bak"
                    @input="(e) => store.updateCleanupRule(rule.id, { value: (e.target as HTMLInputElement).value })"
                  />
                </div>
              </template>

              <template v-else-if="rule.filterType === 'size'">
                <div class="form-row">
                  <label class="form-label">筛选方式</label>
                  <select
                    class="form-select"
                    :value="rule.condition"
                    @change="(e) => store.updateCleanupRule(rule.id, { condition: (e.target as HTMLSelectElement).value })"
                  >
                    <option v-for="opt in getConditionOptions(rule.filterType)" :key="opt.value" :value="opt.value">
                      {{ opt.label }}
                    </option>
                  </select>
                </div>
                <div class="form-row">
                  <label class="form-label">大小 (MB)</label>
                  <input
                    type="number"
                    class="form-input"
                    :value="rule.value"
                    placeholder="输入大小"
                    @input="(e) => store.updateCleanupRule(rule.id, { value: (e.target as HTMLInputElement).value })"
                  />
                </div>
              </template>

              <template v-else-if="rule.filterType === 'dateCreated' || rule.filterType === 'dateModified'">
                <div class="form-row">
                  <label class="form-label">筛选方式</label>
                  <select
                    class="form-select"
                    :value="rule.condition"
                    @change="(e) => store.updateCleanupRule(rule.id, { condition: (e.target as HTMLSelectElement).value })"
                  >
                    <option v-for="opt in getConditionOptions(rule.filterType)" :key="opt.value" :value="opt.value">
                      {{ opt.label }}
                    </option>
                  </select>
                </div>
                <div class="form-row">
                  <label class="form-label">日期</label>
                  <input
                    type="date"
                    class="form-input"
                    :value="rule.value"
                    @input="(e) => store.updateCleanupRule(rule.id, { value: (e.target as HTMLInputElement).value })"
                  />
                </div>
              </template>
            </div>
          </div>
        </div>

        <div class="extra-options">
          <h4 class="options-title">额外选项</h4>
          <label class="form-checkbox">
            <input v-model="cleanEmptyFolders" type="checkbox" />
            📂 清理空文件夹
          </label>
          <label class="form-checkbox">
            <input v-model="cleanDuplicates" type="checkbox" />
            📝 清理重复文件
          </label>
        </div>
      </div>

      <div class="panel-section preview-section">
        <div class="section-header">
          <h3 class="section-title">👁️ 清理预览</h3>
          <span v-if="store.cleanupPreview.length > 0" class="preview-stats">
            已选 {{ selectedCount }} / {{ store.cleanupPreview.length }}
            ({{ fileToolsApi.formatFileSize(store.totalCleanupSize) }})
          </span>
        </div>

        <div v-if="store.cleanupPreview.length === 0" class="empty-preview">
          <div class="empty-icon">🔍</div>
          <div class="empty-text">点击"预览清理"查看结果</div>
        </div>

        <div v-else class="preview-list">
          <div class="preview-header">
            <label class="select-all">
              <input
                type="checkbox"
                :checked="allSelected"
                @change="toggleSelectAll"
              />
              全选
            </label>
            <span class="header-size">大小</span>
            <span class="header-reason">原因</span>
          </div>
          <div class="preview-body">
            <div
              v-for="(item, index) in store.cleanupPreview"
              :key="index"
              class="preview-item"
              :class="{ selected: item.selected }"
              @click="store.toggleCleanupItem(index)"
            >
              <label class="item-checkbox" @click.stop>
                <input
                  type="checkbox"
                  :checked="item.selected"
                  @change="store.toggleCleanupItem(index)"
                />
              </label>
              <div class="item-info">
                <div class="item-name" :title="item.fileName">{{ item.fileName }}</div>
                <div class="item-path" :title="item.filePath">{{ item.filePath }}</div>
              </div>
              <div class="item-size">{{ fileToolsApi.formatFileSize(item.size) }}</div>
              <div class="item-reason">
                <span class="reason-tag">{{ item.reason }}</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <div class="action-bar">
      <button
        class="btn btn-primary"
        :disabled="!store.selectedDirectory || store.isProcessing"
        @click="store.previewCleanup()"
      >
        🔍 预览清理
      </button>
      <button
        class="btn btn-danger"
        :disabled="selectedCount === 0 || store.isProcessing"
        @click="handleExecute"
      >
        🗑️ 执行清理
      </button>
    </div>

    <div v-if="showConfirmDialog" class="modal-overlay" @click.self="cancelExecute">
      <div class="modal-dialog">
        <div class="modal-header">
          <h4 class="modal-title">确认清理</h4>
        </div>
        <div class="modal-body">
          <p>
            确定要删除 <strong>{{ selectedCount }}</strong> 个文件
            （共 <strong>{{ fileToolsApi.formatFileSize(store.totalCleanupSize) }}</strong>）吗？
          </p>
          <div class="warning-options">
            <label class="form-checkbox">
              <input v-model="deletePermanently" type="checkbox" />
              永久删除（不移到回收站）
            </label>
          </div>
          <p class="modal-warning">
            ⚠️ {{ deletePermanently ? '永久删除的文件无法恢复！' : '删除的文件将移到回收站，可恢复。' }}
          </p>
        </div>
        <div class="modal-footer">
          <button class="btn btn-secondary" @click="cancelExecute">取消</button>
          <button class="btn btn-danger" @click="confirmExecute">确认删除</button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.cleanup-panel {
  display: flex;
  flex-direction: column;
  gap: 16px;
  height: 100%;
}

.panel-grid {
  display: grid;
  grid-template-columns: 1fr 1.2fr;
  gap: 16px;
  flex: 1;
  min-height: 0;
}

.panel-section {
  display: flex;
  flex-direction: column;
  background-color: var(--el-bg-color);
  border: 1px solid var(--el-border-color);
  border-radius: 8px;
  padding: 16px;
  min-height: 0;
  overflow: hidden;
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
  flex-shrink: 0;
}

.section-title {
  font-size: 16px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 0;
}

.directory-input {
  margin-bottom: 16px;
  flex-shrink: 0;
}

.input-group {
  display: flex;
  gap: 8px;
}

.input-group .form-input {
  flex: 1;
}

.form-label {
  display: block;
  font-size: 12px;
  color: var(--el-text-color-secondary);
  margin-bottom: 4px;
}

.form-input,
.form-select {
  width: 100%;
  padding: 8px 12px;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  font-size: 13px;
  color: var(--el-text-color-regular);
  background-color: var(--el-bg-color);
  box-sizing: border-box;
}

.form-input:focus,
.form-select:focus {
  outline: none;
  border-color: var(--el-color-primary);
  box-shadow: 0 0 0 3px var(--primary-light);
}

.add-rule-bar {
  margin-bottom: 12px;
  flex-shrink: 0;
}

.rule-type-select {
  width: 100%;
  padding: 8px 12px;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  font-size: 14px;
  color: var(--el-text-color-regular);
  background-color: var(--el-bg-color);
  cursor: pointer;
}

.rule-type-select:focus {
  outline: none;
  border-color: var(--el-color-primary);
  box-shadow: 0 0 0 3px var(--primary-light);
}

.empty-rules,
.empty-preview {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 32px 16px;
  color: var(--el-text-color-secondary);
  flex: 1;
}

.empty-icon {
  font-size: 48px;
  margin-bottom: 8px;
}

.empty-text {
  font-size: 14px;
  margin-bottom: 4px;
}

.empty-hint {
  font-size: 12px;
}

.rules-list {
  flex: 1;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding-right: 4px;
}

.rule-card {
  border: 1px solid var(--el-border-color);
  border-radius: 8px;
  overflow: hidden;
  transition: opacity 0.2s;
}

.rule-card.disabled {
  opacity: 0.6;
}

.rule-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 12px;
  background-color: var(--el-bg-color-page);
  border-bottom: 1px solid var(--el-border-color);
}

.rule-title {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  font-weight: 500;
  color: var(--el-text-color-primary);
}

.rule-order {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 22px;
  height: 22px;
  background-color: var(--el-color-primary);
  color: var(--el-color-white);
  border-radius: 50%;
  font-size: 12px;
  font-weight: 600;
}

.rule-actions {
  display: flex;
  gap: 4px;
}

.icon-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  border: 1px solid var(--el-border-color);
  background-color: var(--el-bg-color);
  border-radius: 4px;
  cursor: pointer;
  font-size: 12px;
  color: var(--el-text-color-secondary);
  transition: all 0.15s;
}

.icon-btn:hover:not(:disabled) {
  background-color: var(--el-fill-color);
  color: var(--el-text-color-primary);
}

.toggle-btn.active {
  background-color: #d4edda;
  border-color: #c3e6cb;
  color: #155724;
}

.delete-btn:hover:not(:disabled) {
  background-color: #f8d7da;
  border-color: #f5c6cb;
  color: #721c24;
}

.rule-body {
  padding: 12px;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.form-row {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.form-checkbox {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  color: var(--el-text-color-regular);
  cursor: pointer;
}

.form-checkbox input[type="checkbox"] {
  cursor: pointer;
}

.extra-options {
  margin-top: 16px;
  padding-top: 16px;
  border-top: 1px solid var(--el-border-color);
  flex-shrink: 0;
}

.options-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 0 0 12px 0;
}

.extra-options .form-checkbox {
  margin-bottom: 8px;
}

.extra-options .form-checkbox:last-child {
  margin-bottom: 0;
}

.preview-stats {
  font-size: 13px;
  color: var(--el-text-color-secondary);
}

.preview-list {
  flex: 1;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
}

.preview-header {
  display: grid;
  grid-template-columns: 40px 1fr 80px 80px;
  padding: 10px 12px;
  background-color: var(--el-bg-color-page);
  font-size: 12px;
  font-weight: 600;
  color: var(--el-text-color-secondary);
  align-items: center;
  flex-shrink: 0;
}

.select-all {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  cursor: pointer;
}

.header-size,
.header-reason {
  text-align: center;
}

.preview-body {
  flex: 1;
  overflow-y: auto;
}

.preview-item {
  display: grid;
  grid-template-columns: 40px 1fr 80px 80px;
  padding: 10px 12px;
  border-top: 1px solid var(--el-border-color-light);
  align-items: center;
  cursor: pointer;
  transition: background-color 0.15s;
}

.preview-item:hover {
  background-color: var(--el-bg-color-page);
}

.preview-item.selected {
  background-color: var(--el-color-primary-light-9);
}

.item-checkbox {
  display: flex;
  align-items: center;
  justify-content: center;
}

.item-info {
  min-width: 0;
}

.item-name {
  font-size: 13px;
  color: var(--el-text-color-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.item-path {
  font-size: 11px;
  color: var(--el-text-color-secondary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  margin-top: 2px;
}

.item-size {
  text-align: center;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.item-reason {
  text-align: center;
}

.reason-tag {
  display: inline-block;
  padding: 2px 8px;
  background-color: var(--el-fill-color-light);
  border-radius: 10px;
  font-size: 11px;
  color: var(--el-text-color-secondary);
}

.action-bar {
  display: flex;
  gap: 12px;
  justify-content: center;
  padding: 12px;
  background-color: var(--el-bg-color-page);
  border-radius: 8px;
  flex-shrink: 0;
}

.btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 10px 20px;
  border: none;
  border-radius: 6px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s ease;
}

.btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-primary {
  background-color: var(--el-color-primary);
  color: var(--el-color-white);
}

.btn-primary:hover:not(:disabled) {
  background-color: var(--el-color-primary-light-3);
}

.btn-danger {
  background-color: var(--el-color-danger);
  color: #fff;
}

.btn-danger:hover:not(:disabled) {
  background-color: #c82333;
}

.btn-secondary {
  background-color: var(--el-text-color-secondary);
  color: var(--el-color-white);
}

.btn-secondary:hover:not(:disabled) {
  opacity: 0.85;
}

.btn-sm {
  padding: 6px 12px;
  font-size: 12px;
}

.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background-color: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.modal-dialog {
  background-color: var(--el-bg-color);
  border-radius: 8px;
  width: 90%;
  max-width: 450px;
  box-shadow: 0 10px 40px rgba(0, 0, 0, 0.2);
}

.modal-header {
  padding: 16px 20px;
  border-bottom: 1px solid var(--el-border-color);
}

.modal-title {
  margin: 0;
  font-size: 16px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}

.modal-body {
  padding: 20px;
  font-size: 14px;
  color: var(--el-text-color-regular);
  line-height: 1.6;
}

.warning-options {
  margin: 16px 0;
  padding: 12px;
  background-color: var(--el-bg-color-page);
  border-radius: 6px;
}

.modal-warning {
  color: #856404;
  background-color: #fff3cd;
  padding: 8px 12px;
  border-radius: 6px;
  margin-top: 12px;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  padding: 16px 20px;
  border-top: 1px solid var(--el-border-color);
}

@media (max-width: 900px) {
  .panel-grid {
    grid-template-columns: 1fr;
    grid-template-rows: auto auto;
  }
}

@media (max-width: 640px) {
  .action-bar {
    flex-direction: column;
  }

  .btn {
    width: 100%;
    justify-content: center;
  }

  .preview-header,
  .preview-item {
    grid-template-columns: 32px 1fr 60px;
  }

  .header-reason,
  .item-reason {
    display: none;
  }
}
</style>
