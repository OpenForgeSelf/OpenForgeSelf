<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { usePluginStore } from '@/stores/plugin'
import type { PluginTemplateInfo } from '@/types/plugin'

const pluginStore = usePluginStore()

const formData = ref({
  name: '',
  description: '',
  author: '',
  pluginType: 'Tool'
})

const isGenerating = ref(false)
const generateResult = ref<{ success: boolean; message: string } | null>(null)
const selectedTemplate = ref<PluginTemplateInfo | null>(null)

function selectTemplate(template: PluginTemplateInfo): void {
  selectedTemplate.value = template
  formData.value.pluginType = template.id
}

async function handleGenerate(): Promise<void> {
  if (!formData.value.name || !formData.value.author) {
    generateResult.value = { success: false, message: '请填写插件名称和作者' }
    return
  }

  isGenerating.value = true
  generateResult.value = null

  try {
    const blob = await pluginStore.generateScaffold({
      name: formData.value.name,
      description: formData.value.description,
      author: formData.value.author,
      pluginType: formData.value.pluginType
    })

    const fileName = `${formData.value.name}-template.zip`
    downloadBlob(blob, fileName)

    generateResult.value = { success: true, message: '插件项目已生成并开始下载！' }
  } catch (e) {
    generateResult.value = {
      success: false,
      message: e instanceof Error ? e.message : '生成失败'
    }
  } finally {
    isGenerating.value = false
  }
}

function downloadBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = fileName
  document.body.appendChild(a)
  a.click()
  document.body.removeChild(a)
  URL.revokeObjectURL(url)
}

onMounted(() => {
  pluginStore.loadTemplates()
})
</script>

<template>
  <div class="plugin-scaffolder-page">
    <header class="page-header">
      <h1 class="page-title">插件开发脚手架</h1>
      <p class="page-subtitle">快速生成插件项目模板，开始你的插件开发之旅</p>
    </header>

    <div class="scaffolder-container">
      <div class="left-panel">
        <h2 class="section-title">选择模板</h2>
        <div class="template-grid">
          <div
            v-for="template in pluginStore.templates"
            :key="template.id"
            class="template-card"
            :class="{ selected: selectedTemplate?.id === template.id }"
            @click="selectTemplate(template)"
          >
            <div class="template-icon">{{ template.icon }}</div>
            <h3 class="template-name">{{ template.name }}</h3>
            <p class="template-desc">{{ template.description }}</p>
            <div class="template-tags">
              <span v-for="tag in template.tags" :key="tag" class="tag">
                {{ tag }}
              </span>
            </div>
          </div>
        </div>

        <div v-if="selectedTemplate" class="template-preview">
          <h3 class="preview-title">模板内容预览</h3>
          <ul class="preview-list">
            <li v-for="item in selectedTemplate.files" :key="item">
              📄 {{ item }}
            </li>
          </ul>
        </div>
      </div>

      <div class="right-panel">
        <h2 class="section-title">插件信息</h2>

        <form class="plugin-form" @submit.prevent="handleGenerate">
          <div class="form-group">
            <label class="form-label">
              插件名称 <span class="required">*</span>
            </label>
            <input
              v-model="formData.name"
              type="text"
              class="form-input"
              placeholder="如：MyAwesomePlugin"
              required
            />
          </div>

          <div class="form-group">
            <label class="form-label">
              插件描述
            </label>
            <textarea
              v-model="formData.description"
              class="form-textarea"
              placeholder="描述你的插件功能..."
              rows="3"
            />
          </div>

          <div class="form-group">
            <label class="form-label">
              作者 <span class="required">*</span>
            </label>
            <input
              v-model="formData.author"
              type="text"
              class="form-input"
              placeholder="你的名字或团队名称"
              required
            />
          </div>

          <div class="form-group">
            <label class="form-label">
              插件类型
            </label>
            <select v-model="formData.pluginType" class="form-select">
              <option v-for="t in pluginStore.templates" :key="t.id" :value="t.id">
                {{ t.name }}
              </option>
            </select>
          </div>

          <div v-if="generateResult" class="result-message" :class="{ success: generateResult.success, error: !generateResult.success }">
            {{ generateResult.message }}
          </div>

          <button
            type="submit"
            class="generate-btn"
            :disabled="isGenerating || !formData.name || !formData.author"
          >
            <span v-if="isGenerating" class="btn-spinner" />
            {{ isGenerating ? '生成中...' : '🚀 生成插件项目' }}
          </button>
        </form>

        <div class="usage-guide">
          <h3 class="guide-title">📖 使用说明</h3>
          <ol class="guide-list">
            <li>选择适合你需求的插件模板</li>
            <li>填写插件基本信息</li>
            <li>点击「生成插件项目」下载 ZIP 包</li>
            <li>解压到本地目录开始开发</li>
            <li>开发完成后打包为 .forgeself-plugin 文件</li>
            <li>在插件市场中导入安装</li>
          </ol>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.plugin-scaffolder-page {
  padding: 24px;
  min-height: 100%;
}

.page-header {
  margin-bottom: 24px;
}

.page-title {
  font-size: 24px;
  font-weight: 600;
  color: var(--text-primary);
  margin: 0 0 4px 0;
}

.page-subtitle {
  font-size: 14px;
  color: var(--text-muted);
  margin: 0;
}

.scaffolder-container {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 24px;
}

.section-title {
  font-size: 18px;
  font-weight: 600;
  color: var(--text-primary);
  margin: 0 0 16px 0;
}

.left-panel,
.right-panel {
  background: var(--bg-card);
  border: 1px solid var(--border-color);
  border-radius: 12px;
  padding: 24px;
}

.template-grid {
  display: grid;
  grid-template-columns: 1fr;
  gap: 12px;
  margin-bottom: 24px;
}

.template-card {
  border: 2px solid var(--border-color);
  border-radius: 10px;
  padding: 16px;
  cursor: pointer;
  transition: all 0.2s;
}

.template-card:hover {
  border-color: var(--primary-color);
  background: var(--bg-secondary);
}

.template-card.selected {
  border-color: var(--primary-color);
  background: var(--primary-soft);
}

.template-icon {
  font-size: 32px;
  margin-bottom: 10px;
}

.template-name {
  font-size: 15px;
  font-weight: 600;
  color: var(--text-primary);
  margin: 0 0 6px 0;
}

.template-desc {
  font-size: 13px;
  color: var(--text-muted);
  margin: 0 0 10px 0;
  line-height: 1.5;
}

.template-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.tag {
  background: var(--bg-muted);
  padding: 2px 8px;
  border-radius: 4px;
  font-size: 11px;
  color: var(--text-secondary);
}

.template-preview {
  padding-top: 20px;
  border-top: 1px solid var(--border-light);
}

.preview-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-primary);
  margin: 0 0 12px 0;
}

.preview-list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 6px;
  font-size: 13px;
  color: var(--text-secondary);
}

.plugin-form {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.form-label {
  font-size: 14px;
  font-weight: 500;
  color: var(--text-primary);
}

.required {
  color: var(--danger-color);
}

.form-input,
.form-textarea,
.form-select {
  padding: 10px 14px;
  border: 1px solid var(--border-color);
  border-radius: 8px;
  font-size: 14px;
  color: var(--text-primary);
  transition: border-color 0.2s;
  font-family: inherit;
  background: var(--bg-card);
}

.form-input:focus,
.form-textarea:focus,
.form-select:focus {
  outline: none;
  border-color: var(--primary-color);
}

.form-textarea {
  resize: vertical;
}

.result-message {
  padding: 12px 16px;
  border-radius: 8px;
  font-size: 14px;
}

.result-message.success {
  background: #d4edda;
  color: #155724;
  border: 1px solid #c3e6cb;
}

.result-message.error {
  background: #f8d7da;
  color: #721c24;
  border: 1px solid #f5c6cb;
}

.generate-btn {
  width: 100%;
  padding: 14px;
  background: var(--primary-color);
  color: var(--primary-contrast);
  border: none;
  border-radius: 10px;
  font-size: 16px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 10px;
}

.generate-btn:hover:not(:disabled) {
  background: var(--primary-hover);
}

.generate-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn-spinner {
  width: 16px;
  height: 16px;
  border: 2px solid transparent;
  border-top-color: currentColor;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.usage-guide {
  margin-top: 24px;
  padding-top: 20px;
  border-top: 1px solid var(--border-light);
}

.guide-title {
  font-size: 15px;
  font-weight: 600;
  color: var(--text-primary);
  margin: 0 0 12px 0;
}

.guide-list {
  margin: 0;
  padding-left: 20px;
  font-size: 13px;
  color: var(--text-secondary);
  line-height: 1.8;
}

@media (max-width: 900px) {
  .scaffolder-container {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 640px) {
  .plugin-scaffolder-page {
    padding: 16px;
  }

  .left-panel,
  .right-panel {
    padding: 16px;
  }
}
</style>