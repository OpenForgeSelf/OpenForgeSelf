<script setup lang="ts">
import { ref, computed } from 'vue'
import { useFileToolsStore } from '@/stores/fileTools'
import { fileToolsApi } from '@/services/fileToolsApi'
import type { ArchiveFormat, CompressionLevel } from '@/types/fileTools'

const store = useFileToolsStore()
const isDragOver = ref(false)
const fileInput = ref<HTMLInputElement | null>(null)
const archiveInput = ref<HTMLInputElement | null>(null)
const showPassword = ref(false)

const formatOptions: { value: ArchiveFormat; label: string }[] = [
  { value: 'zip', label: 'ZIP' },
  { value: '7z', label: '7Z' },
  { value: 'tar', label: 'TAR' },
  { value: 'tar.gz', label: 'TAR.GZ' }
]

const compressionLevelOptions: { value: CompressionLevel; label: string }[] = [
  { value: 'store', label: '存储（不压缩）' },
  { value: 'fastest', label: '最快' },
  { value: 'standard', label: '标准' },
  { value: 'optimal', label: '最优' }
]

const compressionRatioText = computed(() => {
  if (!store.archiveInfo) return '-'
  return ((1 - store.archiveInfo.compressionRatio) * 100).toFixed(1) + '%'
})

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

function handleArchiveSelect(e: Event): void {
  const target = e.target as HTMLInputElement
  if (target.files && target.files[0]) {
    const file = target.files[0]
    store.setArchiveInputPath(file.name)
    store.loadArchiveInfo()
    target.value = ''
  }
}

function openArchiveDialog(): void {
  archiveInput.value?.click()
}

function addDemoFiles(): void {
  const demoFiles = [
    'document.pdf',
    'photo.jpg',
    'report.docx',
    'data.xlsx',
    'presentation.pptx'
  ]
  store.addFiles(demoFiles)
}

function setDemoArchive(): void {
  store.setArchiveInputPath('archive.zip')
  store.loadArchiveInfo()
}

function setDemoOutput(): void {
  store.setArchiveOutputPath('C:/Users/User/Documents/extracted')
}

function togglePasswordVisibility(): void {
  showPassword.value = !showPassword.value
}

async function handleCompress(): Promise<void> {
  const success = await store.compress()
  if (success) {
    // 压缩成功
  }
}

async function handleExtract(): Promise<void> {
  const success = await store.extract()
  if (success) {
    // 解压成功
  }
}
</script>

<template>
  <div class="archive-panel">
    <div class="mode-switch">
      <button
        class="mode-btn"
        :class="{ active: store.archiveMode === 'compress' }"
        @click="store.setArchiveMode('compress')"
      >
        📦 压缩文件
      </button>
      <button
        class="mode-btn"
        :class="{ active: store.archiveMode === 'extract' }"
        @click="store.setArchiveMode('extract')"
      >
        📤 解压文件
      </button>
    </div>

    <div v-if="store.archiveMode === 'compress'" class="compress-mode">
      <div class="panel-grid">
        <div class="panel-section">
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
            <div class="drop-hint">支持多文件和文件夹</div>
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

        <div class="panel-section">
          <div class="section-header">
            <h3 class="section-title">⚙️ 压缩设置</h3>
          </div>

          <div class="settings-form">
            <div class="form-group">
              <label class="form-label">输出路径</label>
              <div class="input-group">
                <input
                  type="text"
                  class="form-input"
                  :value="store.archiveOutputPath"
                  placeholder="输入输出路径"
                  @input="(e) => store.setArchiveOutputPath((e.target as HTMLInputElement).value)"
                />
                <button class="btn btn-sm btn-secondary" @click="setDemoOutput">
                  示例
                </button>
              </div>
            </div>

            <div class="form-group">
              <label class="form-label">压缩格式</label>
              <select
                class="form-select"
                :value="store.archiveFormat"
                @change="(e) => store.setArchiveFormat((e.target as HTMLSelectElement).value as ArchiveFormat)"
              >
                <option v-for="opt in formatOptions" :key="opt.value" :value="opt.value">
                  {{ opt.label }}
                </option>
              </select>
            </div>

            <div class="form-group">
              <label class="form-label">压缩级别</label>
              <select
                class="form-select"
                :value="store.compressionLevel"
                @change="(e) => store.setCompressionLevel((e.target as HTMLSelectElement).value as CompressionLevel)"
              >
                <option v-for="opt in compressionLevelOptions" :key="opt.value" :value="opt.value">
                  {{ opt.label }}
                </option>
              </select>
            </div>

            <div class="form-group">
              <label class="form-label">密码（可选）</label>
              <div class="password-input">
                <input
                  :type="showPassword ? 'text' : 'password'"
                  class="form-input"
                  :value="store.archivePassword"
                  placeholder="设置密码"
                  @input="(e) => store.setArchivePassword((e.target as HTMLInputElement).value)"
                />
                <button
                  class="toggle-password"
                  :aria-label="showPassword ? '隐藏密码' : '显示密码'"
                  @click="togglePasswordVisibility"
                >
                  {{ showPassword ? '🙈' : '👁️' }}
                </button>
              </div>
            </div>

            <div class="form-group">
              <label class="form-label">分卷大小（可选）</label>
              <input
                type="text"
                class="form-input"
                placeholder="如: 100MB，留空则不分卷"
              />
            </div>
          </div>
        </div>
      </div>

      <div class="action-bar">
        <button
          class="btn btn-primary btn-large"
          :disabled="store.selectedFiles.length === 0 || !store.archiveOutputPath || store.isProcessing"
          @click="handleCompress"
        >
          📦 开始压缩
        </button>
      </div>
    </div>

    <div v-else class="extract-mode">
      <div class="panel-grid">
        <div class="panel-section">
          <div class="section-header">
            <h3 class="section-title">📦 选择压缩包</h3>
            <button class="btn btn-sm btn-secondary" @click="setDemoArchive">
              示例
            </button>
          </div>

          <div class="archive-select" @click="openArchiveDialog">
            <div class="archive-icon">📦</div>
            <div v-if="!store.archiveInputPath" class="archive-placeholder">
              点击选择压缩包文件
            </div>
            <div v-else class="archive-info">
              <div class="archive-name">{{ store.archiveInputPath }}</div>
              <div class="archive-hint">点击更换文件</div>
            </div>
            <input
              ref="archiveInput"
              type="file"
              accept=".zip,.7z,.tar,.tar.gz,.rar"
              hidden
              @change="handleArchiveSelect"
            />
          </div>

          <div v-if="store.archiveInfo" class="archive-details">
            <h4 class="details-title">压缩包信息</h4>
            <div class="details-grid">
              <div class="detail-item">
                <span class="detail-label">文件数量</span>
                <span class="detail-value">{{ store.archiveInfo.fileCount }} 个</span>
              </div>
              <div class="detail-item">
                <span class="detail-label">压缩前大小</span>
                <span class="detail-value">{{ fileToolsApi.formatFileSize(store.archiveInfo.size) }}</span>
              </div>
              <div class="detail-item">
                <span class="detail-label">压缩后大小</span>
                <span class="detail-value">{{ fileToolsApi.formatFileSize(store.archiveInfo.compressedSize) }}</span>
              </div>
              <div class="detail-item">
                <span class="detail-label">压缩率</span>
                <span class="detail-value">{{ compressionRatioText }}</span>
              </div>
              <div class="detail-item">
                <span class="detail-label">格式</span>
                <span class="detail-value">{{ store.archiveInfo.format.toUpperCase() }}</span>
              </div>
              <div class="detail-item">
                <span class="detail-label">密码保护</span>
                <span class="detail-value">{{ store.archiveInfo.hasPassword ? '是' : '否' }}</span>
              </div>
            </div>
          </div>
        </div>

        <div class="panel-section">
          <div class="section-header">
            <h3 class="section-title">⚙️ 解压设置</h3>
          </div>

          <div class="settings-form">
            <div class="form-group">
              <label class="form-label">输出路径</label>
              <div class="input-group">
                <input
                  type="text"
                  class="form-input"
                  :value="store.archiveOutputPath"
                  placeholder="输入解压路径"
                  @input="(e) => store.setArchiveOutputPath((e.target as HTMLInputElement).value)"
                />
                <button class="btn btn-sm btn-secondary" @click="setDemoOutput">
                  示例
                </button>
              </div>
            </div>

            <div class="form-group">
              <label class="form-label">密码（如需要）</label>
              <div class="password-input">
                <input
                  :type="showPassword ? 'text' : 'password'"
                  class="form-input"
                  :value="store.archivePassword"
                  placeholder="输入密码"
                  @input="(e) => store.setArchivePassword((e.target as HTMLInputElement).value)"
                />
                <button
                  class="toggle-password"
                  :aria-label="showPassword ? '隐藏密码' : '显示密码'"
                  @click="togglePasswordVisibility"
                >
                  {{ showPassword ? '🙈' : '👁️' }}
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div class="action-bar">
        <button
          class="btn btn-primary btn-large"
          :disabled="!store.archiveInputPath || !store.archiveOutputPath || store.isProcessing"
          @click="handleExtract"
        >
          📤 开始解压
        </button>
      </div>
    </div>

    <div v-if="store.isProcessing" class="progress-section">
      <div class="progress-header">
        <span>{{ store.archiveMode === 'compress' ? '压缩中...' : '解压中...' }}</span>
        <span>{{ store.progress }}%</span>
      </div>
      <div class="progress-bar-large">
        <div class="progress-fill" :style="{ width: store.progress + '%' }" />
      </div>
    </div>
  </div>
</template>

<style scoped>
.archive-panel {
  display: flex;
  flex-direction: column;
  gap: 16px;
  height: 100%;
}

.mode-switch {
  display: flex;
  gap: 8px;
  padding: 6px;
  background-color: var(--bg-muted);
  border-radius: 8px;
  flex-shrink: 0;
}

.mode-btn {
  flex: 1;
  padding: 10px 16px;
  border: none;
  background: transparent;
  border-radius: 6px;
  font-size: 14px;
  font-weight: 500;
  color: var(--text-secondary);
  cursor: pointer;
  transition: all 0.2s ease;
}

.mode-btn:hover {
  background-color: var(--bg-hover);
}

.mode-btn.active {
  background-color: var(--bg-card);
  color: var(--primary-color);
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
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

.settings-form {
  display: flex;
  flex-direction: column;
  gap: 16px;
  flex: 1;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.form-label {
  font-size: 13px;
  font-weight: 500;
  color: var(--text-secondary);
}

.form-input,
.form-select {
  width: 100%;
  padding: 8px 12px;
  border: 1px solid var(--border-color);
  border-radius: 6px;
  font-size: 13px;
  color: var(--text-secondary);
  background-color: var(--bg-card);
  box-sizing: border-box;
}

.form-input:focus,
.form-select:focus {
  outline: none;
  border-color: var(--primary-color);
  box-shadow: 0 0 0 3px var(--primary-light);
}

.input-group {
  display: flex;
  gap: 8px;
}

.input-group .form-input {
  flex: 1;
}

.password-input {
  position: relative;
}

.password-input .form-input {
  padding-right: 40px;
}

.toggle-password {
  position: absolute;
  right: 8px;
  top: 50%;
  transform: translateY(-50%);
  background: none;
  border: none;
  font-size: 18px;
  cursor: pointer;
  padding: 4px;
  line-height: 1;
}

.archive-select {
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

.archive-select:hover {
  border-color: var(--primary-color);
  background-color: var(--primary-soft);
}

.archive-icon {
  font-size: 48px;
  margin-bottom: 8px;
}

.archive-placeholder {
  font-size: 14px;
  color: var(--text-muted);
}

.archive-info {
  text-align: center;
}

.archive-name {
  font-size: 14px;
  font-weight: 500;
  color: var(--text-primary);
  margin-bottom: 4px;
}

.archive-hint {
  font-size: 12px;
  color: var(--text-muted);
}

.archive-details {
  margin-top: 16px;
  flex: 1;
  overflow-y: auto;
}

.details-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-primary);
  margin: 0 0 12px 0;
}

.details-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}

.detail-item {
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding: 10px;
  background-color: var(--bg-secondary);
  border-radius: 6px;
}

.detail-label {
  font-size: 11px;
  color: var(--text-muted);
}

.detail-value {
  font-size: 13px;
  font-weight: 500;
  color: var(--text-primary);
}

.action-bar {
  display: flex;
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

.btn-large {
  padding: 12px 32px;
  font-size: 15px;
}

.btn-link {
  background: none;
  color: var(--primary-color);
  padding: 4px 8px;
}

.btn-link:hover {
  text-decoration: underline;
}

.progress-section {
  background-color: var(--bg-card);
  border: 1px solid var(--border-color);
  border-radius: 8px;
  padding: 16px;
  flex-shrink: 0;
}

.progress-header {
  display: flex;
  justify-content: space-between;
  font-size: 14px;
  color: var(--text-secondary);
  margin-bottom: 8px;
}

.progress-bar-large {
  height: 8px;
  background-color: var(--border-color);
  border-radius: 4px;
  overflow: hidden;
}

.progress-fill {
  height: 100%;
  background-color: var(--primary-color);
  border-radius: 4px;
  transition: width 0.3s ease;
}

@media (max-width: 900px) {
  .panel-grid {
    grid-template-columns: 1fr;
    grid-template-rows: auto auto;
  }
}

@media (max-width: 640px) {
  .mode-btn {
    font-size: 13px;
    padding: 8px 12px;
  }

  .details-grid {
    grid-template-columns: 1fr;
  }

  .action-bar {
    flex-direction: column;
  }

  .btn-large {
    width: 100%;
    justify-content: center;
  }
}
</style>
