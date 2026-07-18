<script setup lang="ts">
import { ref } from 'vue'
import { useFileToolsStore } from '@/stores/fileTools'
import type { RenameRuleType } from '@/types/fileTools'

const store = useFileToolsStore()
const isDragOver = ref(false)
const showConfirmDialog = ref(false)
const fileInput = ref<HTMLInputElement | null>(null)

const ruleTypeOptions: { type: RenameRuleType; label: string; icon: string }[] = [
  { type: 'sequence', label: '序号', icon: '🔢' },
  { type: 'date', label: '日期', icon: '📅' },
  { type: 'replace', label: '查找替换', icon: '🔄' },
  { type: 'regex', label: '正则表达式', icon: '🔍' },
  { type: 'prefix', label: '前缀', icon: '➡️' },
  { type: 'suffix', label: '后缀', icon: '⬅️' },
  { type: 'extension', label: '扩展名', icon: '📄' }
]

function handleDragOver(e: DragEvent): void {
  e.preventDefault()
  isDragOver.value = true
}

function handleDragLeave(): void {
  isDragOver.value = false
}

function handleDrop(e: DragEvent): void {
  e.preventDefault()
  isDragOver.value = false

  if (e.dataTransfer?.files) {
    const files = Array.from(e.dataTransfer.files).map(f => f.name)
    store.addFiles(files)
  }
}

function handleFileSelect(e: Event): void {
  const target = e.target as HTMLInputElement
  if (target.files) {
    const files = Array.from(target.files).map(f => f.name)
    store.addFiles(files)
    target.value = ''
  }
}

function openFileDialog(): void {
  fileInput.value?.click()
}

function addDemoFiles(): void {
  const demoFiles = [
    'photo_001.jpg',
    'photo_002.jpg',
    'photo_003.png',
    'document.pdf',
    'report.docx'
  ]
  store.addFiles(demoFiles)
}

function getRuleLabel(ruleType: RenameRuleType): string {
  const option = ruleTypeOptions.find(o => o.type === ruleType)
  return option ? `${option.icon} ${option.label}` : ruleType
}

function handleExecute(): void {
  showConfirmDialog.value = true
}

async function confirmExecute(): Promise<void> {
  showConfirmDialog.value = false
  const result = await store.executeRename()
  if (result.success > 0) {
    store.clearFiles()
  }
}

function cancelExecute(): void {
  showConfirmDialog.value = false
}
</script>

<template>
  <div class="rename-panel">
    <div class="panel-grid">
      <div class="panel-section file-section">
        <div class="section-header">
          <h3 class="section-title">📁 选择文件</h3>
          <button class="btn btn-sm btn-secondary" @click="addDemoFiles">
            添加示例
          </button>
        </div>

        <div
          class="drop-zone"
          :class="{ 'drag-over': isDragOver }"
          @dragover="handleDragOver"
          @dragleave="handleDragLeave"
          @drop="handleDrop"
          @click="openFileDialog"
        >
          <div class="drop-icon">📤</div>
          <div class="drop-text">拖拽文件到此处或点击选择</div>
          <div class="drop-hint">支持批量选择</div>
          <input
            ref="fileInput"
            type="file"
            multiple
            hidden
            @change="handleFileSelect"
          />
        </div>

        <div v-if="store.selectedFiles.length > 0" class="file-list">
          <div class="file-list-header">
            <span>已选文件 ({{ store.selectedFiles.length }})</span>
            <button class="btn btn-link btn-sm" @click="store.clearFiles()">
              清空
            </button>
          </div>
          <div class="file-list-content">
            <div
              v-for="(file, index) in store.selectedFiles"
              :key="index"
              class="file-item"
            >
              <span class="file-icon">📄</span>
              <span class="file-name">{{ file }}</span>
              <button
                class="file-remove"
                aria-label="移除文件"
                @click.stop="store.removeFile(file)"
              >
                ✕
              </button>
            </div>
          </div>
        </div>
      </div>

      <div class="panel-section rules-section">
        <div class="section-header">
          <h3 class="section-title">⚙️ 重命名规则</h3>
        </div>

        <div class="add-rule-bar">
          <select
            class="rule-type-select"
            @change="(e) => {
              const target = e.target as HTMLSelectElement
              if (target.value) {
                store.addRenameRule(target.value as RenameRuleType)
                target.value = ''
              }
            }"
          >
            <option value="">+ 添加规则...</option>
            <option v-for="opt in ruleTypeOptions" :key="opt.type" :value="opt.type">
              {{ opt.icon }} {{ opt.label }}
            </option>
          </select>
        </div>

        <div v-if="store.renameRules.length === 0" class="empty-rules">
          <div class="empty-icon">📋</div>
          <div class="empty-text">暂无规则，点击上方添加</div>
        </div>

        <div v-else class="rules-list">
          <div
            v-for="(rule, index) in store.renameRules"
            :key="rule.id"
            class="rule-card"
            :class="{ disabled: !rule.enabled }"
          >
            <div class="rule-header">
              <div class="rule-title">
                <span class="rule-order">{{ index + 1 }}</span>
                {{ getRuleLabel(rule.ruleType) }}
              </div>
              <div class="rule-actions">
                <button
                  class="icon-btn"
                  :disabled="index === 0"
                  aria-label="上移"
                  @click="store.moveRenameRule(rule.id, 'up')"
                >
                  ↑
                </button>
                <button
                  class="icon-btn"
                  :disabled="index === store.renameRules.length - 1"
                  aria-label="下移"
                  @click="store.moveRenameRule(rule.id, 'down')"
                >
                  ↓
                </button>
                <button
                  class="icon-btn toggle-btn"
                  :class="{ active: rule.enabled }"
                  aria-label="启用/禁用"
                  @click="store.toggleRenameRule(rule.id)"
                >
                  {{ rule.enabled ? '✓' : '○' }}
                </button>
                <button
                  class="icon-btn delete-btn"
                  aria-label="删除"
                  @click="store.removeRenameRule(rule.id)"
                >
                  🗑️
                </button>
              </div>
            </div>

            <div class="rule-body">
              <template v-if="rule.ruleType === 'sequence'">
                <div class="form-row">
                  <label class="form-label">起始值</label>
                  <input
                    type="number"
                    class="form-input"
                    :value="(rule.params as any).startValue"
                    @input="(e) => store.updateRenameRule(rule.id, { startValue: Number((e.target as HTMLInputElement).value) })"
                  />
                </div>
                <div class="form-row">
                  <label class="form-label">步长</label>
                  <input
                    type="number"
                    class="form-input"
                    :value="(rule.params as any).step"
                    @input="(e) => store.updateRenameRule(rule.id, { step: Number((e.target as HTMLInputElement).value) })"
                  />
                </div>
                <div class="form-row">
                  <label class="form-label">位数</label>
                  <input
                    type="number"
                    class="form-input"
                    min="1"
                    max="10"
                    :value="(rule.params as any).digits"
                    @input="(e) => store.updateRenameRule(rule.id, { digits: Number((e.target as HTMLInputElement).value) })"
                  />
                </div>
                <div class="form-row">
                  <label class="form-label">位置</label>
                  <select
                    class="form-select"
                    :value="(rule.params as any).position"
                    @change="(e) => store.updateRenameRule(rule.id, { position: (e.target as HTMLSelectElement).value })"
                  >
                    <option value="prefix">前缀</option>
                    <option value="suffix">后缀</option>
                    <option value="replace">替换</option>
                  </select>
                </div>
                <div class="form-row">
                  <label class="form-label">分隔符</label>
                  <input
                    type="text"
                    class="form-input"
                    :value="(rule.params as any).separator"
                    @input="(e) => store.updateRenameRule(rule.id, { separator: (e.target as HTMLInputElement).value })"
                  />
                </div>
              </template>

              <template v-else-if="rule.ruleType === 'date'">
                <div class="form-row">
                  <label class="form-label">日期格式</label>
                  <input
                    type="text"
                    class="form-input"
                    :value="(rule.params as any).format"
                    placeholder="YYYYMMDD"
                    @input="(e) => store.updateRenameRule(rule.id, { format: (e.target as HTMLInputElement).value })"
                  />
                </div>
                <div class="form-row">
                  <label class="form-label">位置</label>
                  <select
                    class="form-select"
                    :value="(rule.params as any).position"
                    @change="(e) => store.updateRenameRule(rule.id, { position: (e.target as HTMLSelectElement).value })"
                  >
                    <option value="prefix">前缀</option>
                    <option value="suffix">后缀</option>
                  </select>
                </div>
                <div class="form-row">
                  <label class="form-label">分隔符</label>
                  <input
                    type="text"
                    class="form-input"
                    :value="(rule.params as any).separator"
                    @input="(e) => store.updateRenameRule(rule.id, { separator: (e.target as HTMLInputElement).value })"
                  />
                </div>
                <div class="format-hint">
                  支持: YYYY(年) MM(月) DD(日) HH(时) mm(分) ss(秒)
                </div>
              </template>

              <template v-else-if="rule.ruleType === 'replace'">
                <div class="form-row">
                  <label class="form-label">查找内容</label>
                  <input
                    type="text"
                    class="form-input"
                    :value="(rule.params as any).find"
                    placeholder="要查找的文本"
                    @input="(e) => store.updateRenameRule(rule.id, { find: (e.target as HTMLInputElement).value })"
                  />
                </div>
                <div class="form-row">
                  <label class="form-label">替换为</label>
                  <input
                    type="text"
                    class="form-input"
                    :value="(rule.params as any).replace"
                    placeholder="替换后的文本"
                    @input="(e) => store.updateRenameRule(rule.id, { replace: (e.target as HTMLInputElement).value })"
                  />
                </div>
                <div class="form-row">
                  <label class="form-checkbox">
                    <input
                      type="checkbox"
                      :checked="(rule.params as any).caseSensitive"
                      @change="(e) => store.updateRenameRule(rule.id, { caseSensitive: (e.target as HTMLInputElement).checked })"
                    />
                    区分大小写
                  </label>
                </div>
              </template>

              <template v-else-if="rule.ruleType === 'regex'">
                <div class="form-row">
                  <label class="form-label">正则模式</label>
                  <input
                    type="text"
                    class="form-input"
                    :value="(rule.params as any).pattern"
                    placeholder="正则表达式"
                    @input="(e) => store.updateRenameRule(rule.id, { pattern: (e.target as HTMLInputElement).value })"
                  />
                </div>
                <div class="form-row">
                  <label class="form-label">替换为</label>
                  <input
                    type="text"
                    class="form-input"
                    :value="(rule.params as any).replace"
                    placeholder="替换文本"
                    @input="(e) => store.updateRenameRule(rule.id, { replace: (e.target as HTMLInputElement).value })"
                  />
                </div>
                <div class="form-row">
                  <label class="form-label">标志位</label>
                  <input
                    type="text"
                    class="form-input"
                    :value="(rule.params as any).flags"
                    placeholder="g, i, m"
                    @input="(e) => store.updateRenameRule(rule.id, { flags: (e.target as HTMLInputElement).value })"
                  />
                </div>
              </template>

              <template v-else-if="rule.ruleType === 'prefix'">
                <div class="form-row">
                  <label class="form-label">前缀文本</label>
                  <input
                    type="text"
                    class="form-input"
                    :value="(rule.params as any).text"
                    placeholder="输入前缀"
                    @input="(e) => store.updateRenameRule(rule.id, { text: (e.target as HTMLInputElement).value })"
                  />
                </div>
              </template>

              <template v-else-if="rule.ruleType === 'suffix'">
                <div class="form-row">
                  <label class="form-label">后缀文本</label>
                  <input
                    type="text"
                    class="form-input"
                    :value="(rule.params as any).text"
                    placeholder="输入后缀"
                    @input="(e) => store.updateRenameRule(rule.id, { text: (e.target as HTMLInputElement).value })"
                  />
                </div>
              </template>

              <template v-else-if="rule.ruleType === 'extension'">
                <div class="form-row">
                  <label class="form-label">新扩展名</label>
                  <input
                    type="text"
                    class="form-input"
                    :value="(rule.params as any).newExtension"
                    placeholder="如: jpg"
                    @input="(e) => store.updateRenameRule(rule.id, { newExtension: (e.target as HTMLInputElement).value })"
                  />
                </div>
              </template>
            </div>
          </div>
        </div>
      </div>
    </div>

    <div class="action-bar">
      <button
        class="btn btn-primary"
        :disabled="store.selectedFiles.length === 0 || store.isProcessing"
        @click="store.previewRename()"
      >
        👁️ 预览重命名
      </button>
      <button
        class="btn btn-success"
        :disabled="store.renamePreview.length === 0 || store.validPreviewCount === 0 || store.isProcessing"
        @click="handleExecute"
      >
        ✅ 执行重命名
      </button>
    </div>

    <div v-if="store.renamePreview.length > 0" class="preview-section">
      <div class="section-header">
        <h3 class="section-title">👁️ 预览结果</h3>
        <span class="preview-stats">
          有效: {{ store.validPreviewCount }} / {{ store.renamePreview.length }}
        </span>
      </div>
      <div class="preview-table">
        <div class="preview-header">
          <div class="preview-col">原文件名</div>
          <div class="preview-arrow">→</div>
          <div class="preview-col">新文件名</div>
          <div class="preview-status">状态</div>
        </div>
        <div class="preview-body">
          <div
            v-for="(item, index) in store.renamePreview"
            :key="index"
            class="preview-row"
            :class="{ invalid: !item.isValid }"
          >
            <div class="preview-col" :title="item.originalName">{{ item.originalName }}</div>
            <div class="preview-arrow">→</div>
            <div class="preview-col" :title="item.newName">{{ item.newName }}</div>
            <div class="preview-status">
              <span v-if="item.isValid" class="status-ok">✓ 有效</span>
              <span v-else class="status-error" :title="item.errorMessage || ''">
                ✗ {{ item.errorMessage }}
              </span>
            </div>
          </div>
        </div>
      </div>
    </div>

    <div v-if="showConfirmDialog" class="modal-overlay" @click.self="cancelExecute">
      <div class="modal-dialog">
        <div class="modal-header">
          <h4 class="modal-title">确认执行</h4>
        </div>
        <div class="modal-body">
          <p>确定要对 <strong>{{ store.validPreviewCount }}</strong> 个文件执行重命名操作吗？</p>
          <p class="modal-warning">⚠️ 此操作不可撤销，请确认预览结果无误。</p>
        </div>
        <div class="modal-footer">
          <button class="btn btn-secondary" @click="cancelExecute">取消</button>
          <button class="btn btn-primary" @click="confirmExecute">确认执行</button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.rename-panel {
  display: flex;
  flex-direction: column;
  gap: 16px;
  height: 100%;
}

.panel-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
  flex: 1;
  min-height: 0;
}

.panel-section {
  display: flex;
  flex-direction: column;
  background-color: var(--bg-card);
  border: 1px solid var(--border-color);
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
  color: var(--text-primary);
  margin: 0;
}

.drop-zone {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 32px 16px;
  border: 2px dashed var(--border-color);
  border-radius: 8px;
  background-color: var(--bg-secondary);
  cursor: pointer;
  transition: all 0.2s ease;
  flex-shrink: 0;
}

.drop-zone:hover,
.drop-zone.drag-over {
  border-color: var(--primary-color);
  background-color: var(--primary-soft);
}

.drop-icon {
  font-size: 48px;
  margin-bottom: 8px;
}

.drop-text {
  font-size: 14px;
  color: var(--text-secondary);
  margin-bottom: 4px;
}

.drop-hint {
  font-size: 12px;
  color: var(--text-muted);
}

.file-list {
  display: flex;
  flex-direction: column;
  margin-top: 12px;
  flex: 1;
  min-height: 0;
}

.file-list-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 0;
  font-size: 13px;
  color: var(--text-muted);
  border-bottom: 1px solid var(--border-color);
  flex-shrink: 0;
}

.file-list-content {
  flex: 1;
  overflow-y: auto;
  padding: 4px 0;
}

.file-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px;
  border-radius: 6px;
  transition: background-color 0.15s;
}

.file-item:hover {
  background-color: var(--bg-secondary);
}

.file-icon {
  font-size: 16px;
  flex-shrink: 0;
}

.file-name {
  flex: 1;
  font-size: 13px;
  color: var(--text-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.file-remove {
  background: none;
  border: none;
  color: var(--text-muted);
  cursor: pointer;
  font-size: 14px;
  padding: 4px;
  line-height: 1;
  opacity: 0;
  transition: opacity 0.15s, color 0.15s;
}

.file-item:hover .file-remove {
  opacity: 1;
}

.file-remove:hover {
  color: var(--danger-color);
}

.add-rule-bar {
  margin-bottom: 12px;
  flex-shrink: 0;
}

.rule-type-select {
  width: 100%;
  padding: 8px 12px;
  border: 1px solid var(--border-color);
  border-radius: 6px;
  font-size: 14px;
  color: var(--text-secondary);
  background-color: var(--bg-card);
  cursor: pointer;
}

.rule-type-select:focus {
  outline: none;
  border-color: var(--primary-color);
  box-shadow: 0 0 0 3px var(--primary-light);
}

.empty-rules {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 32px 16px;
  color: var(--text-muted);
  flex: 1;
}

.empty-icon {
  font-size: 48px;
  margin-bottom: 8px;
}

.empty-text {
  font-size: 14px;
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
  border: 1px solid var(--border-color);
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
  background-color: var(--bg-secondary);
  border-bottom: 1px solid var(--border-color);
}

.rule-title {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  font-weight: 500;
  color: var(--text-primary);
}

.rule-order {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 22px;
  height: 22px;
  background-color: var(--primary-color);
  color: var(--primary-contrast);
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
  border: 1px solid var(--border-color);
  background-color: var(--bg-card);
  border-radius: 4px;
  cursor: pointer;
  font-size: 12px;
  color: var(--text-muted);
  transition: all 0.15s;
}

.icon-btn:hover:not(:disabled) {
  background-color: var(--bg-hover);
  color: var(--text-primary);
}

.icon-btn:disabled {
  opacity: 0.4;
  cursor: not-allowed;
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

.form-label {
  font-size: 12px;
  color: var(--text-muted);
}

.form-input,
.form-select {
  padding: 6px 10px;
  border: 1px solid var(--border-color);
  border-radius: 4px;
  font-size: 13px;
  color: var(--text-secondary);
  background-color: var(--bg-card);
}

.form-input:focus,
.form-select:focus {
  outline: none;
  border-color: var(--primary-color);
  box-shadow: 0 0 0 3px var(--primary-light);
}

.form-checkbox {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  color: var(--text-secondary);
  cursor: pointer;
}

.form-checkbox input[type="checkbox"] {
  cursor: pointer;
}

.format-hint {
  font-size: 11px;
  color: var(--text-muted);
  padding: 4px 0;
}

.action-bar {
  display: flex;
  gap: 12px;
  justify-content: center;
  padding: 12px;
  background-color: var(--bg-secondary);
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
  background-color: var(--primary-color);
  color: var(--primary-contrast);
}

.btn-primary:hover:not(:disabled) {
  background-color: var(--primary-hover);
}

.btn-success {
  background-color: #28a745;
  color: #fff;
}

.btn-success:hover:not(:disabled) {
  background-color: #218838;
}

.btn-secondary {
  background-color: var(--text-muted);
  color: var(--primary-contrast);
}

.btn-secondary:hover:not(:disabled) {
  opacity: 0.85;
}

.btn-sm {
  padding: 6px 12px;
  font-size: 12px;
}

.btn-link {
  background: none;
  color: var(--primary-color);
  padding: 4px 8px;
}

.btn-link:hover {
  text-decoration: underline;
}

.preview-section {
  background-color: var(--bg-card);
  border: 1px solid var(--border-color);
  border-radius: 8px;
  padding: 16px;
  flex-shrink: 0;
  max-height: 300px;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.preview-stats {
  font-size: 13px;
  color: var(--text-muted);
}

.preview-table {
  flex: 1;
  overflow: auto;
  border: 1px solid var(--border-color);
  border-radius: 6px;
}

.preview-header {
  display: grid;
  grid-template-columns: 1fr 40px 1fr 100px;
  padding: 10px 12px;
  background-color: var(--bg-secondary);
  font-size: 12px;
  font-weight: 600;
  color: var(--text-muted);
  position: sticky;
  top: 0;
  z-index: 1;
}

.preview-row {
  display: grid;
  grid-template-columns: 1fr 40px 1fr 100px;
  padding: 8px 12px;
  border-top: 1px solid var(--border-light);
  font-size: 13px;
  align-items: center;
}

.preview-row:hover {
  background-color: var(--bg-secondary);
}

.preview-row.invalid {
  background-color: #fff5f5;
}

.preview-col {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--text-primary);
}

.preview-arrow {
  text-align: center;
  color: var(--text-muted);
}

.preview-status {
  text-align: center;
  font-size: 12px;
}

.status-ok {
  color: #28a745;
}

.status-error {
  color: var(--danger-color);
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
  background-color: var(--bg-card);
  border-radius: 8px;
  width: 90%;
  max-width: 400px;
  box-shadow: 0 10px 40px rgba(0, 0, 0, 0.2);
}

.modal-header {
  padding: 16px 20px;
  border-bottom: 1px solid var(--border-color);
}

.modal-title {
  margin: 0;
  font-size: 16px;
  font-weight: 600;
  color: var(--text-primary);
}

.modal-body {
  padding: 20px;
  font-size: 14px;
  color: var(--text-secondary);
  line-height: 1.6;
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
  border-top: 1px solid var(--border-color);
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
  .preview-row {
    grid-template-columns: 1fr 30px 1fr 80px;
    font-size: 12px;
  }
}
</style>
