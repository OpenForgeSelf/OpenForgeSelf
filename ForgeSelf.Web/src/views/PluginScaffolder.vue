<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { usePluginStore } from '@/stores/plugin'
import type { PluginTemplateInfo } from '@/types/plugin'
import PageHead from '@/components/PageHead.vue'

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
    <PageHead
      eyebrow="Plugin Dev · 插件脚手架"
      title="插件开发脚手架"
    />

    <div class="scaffolder-container">
      <div class="left-panel glass">
        <h2 class="section-title">选择模板</h2>
        <div class="template-grid">
          <div
            v-for="template in pluginStore.templates"
            :key="template.id"
            class="template-card"
            :class="{ selected: selectedTemplate?.id === template.id }"
            @click="selectTemplate(template)"
          >
            <div class="template-icon">
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
                <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
                <polyline points="M14 2 14 8 20 8" />
                <line x1="16" y1="13" x2="8" y2="13" />
                <line x1="16" y1="17" x2="8" y2="17" />
                <polyline points="M10 9 9 9 8 9" />
              </svg>
            </div>
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
              <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="preview-icon">
                <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
                <polyline points="M14 2 14 8 20 8" />
              </svg>
              {{ item }}
            </li>
          </ul>
        </div>
      </div>

      <div class="right-panel glass">
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
            {{ isGenerating ? '生成中...' : '生成插件项目' }}
          </button>
        </form>

        <div class="usage-guide">
          <h3 class="guide-title">使用说明</h3>
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
  max-width: 1200px;
  margin: 0 auto;
}

.scaffolder-container {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 20px;
}

.section-title {
  font-size: 18px;
  font-weight: 600;
  color: var(--fg-primary);
  margin: 0 0 16px 0;
  font-family: var(--font-serif-cn);
}

.left-panel,
.right-panel {
  padding: 24px;
}

.template-grid {
  display: grid;
  grid-template-columns: 1fr;
  gap: 12px;
  margin-bottom: 24px;
}

.template-card {
  border: 2px solid var(--border-default);
  border-radius: var(--radius-md);
  padding: 16px;
  cursor: pointer;
  transition: border-color var(--dur-fast) var(--ease-out), background var(--dur-fast) var(--ease-out);
}

.template-card:hover {
  border-color: var(--gold-400);
}

.template-card.selected {
  border-color: var(--gold-400);
  background: var(--gold-tint);
}

.template-icon {
  color: var(--gold-400);
  margin-bottom: 10px;
  display: flex;
}

.template-name {
  font-size: 15px;
  font-weight: 600;
  color: var(--fg-primary);
  margin: 0 0 6px 0;
}

.template-desc {
  font-size: 13px;
  color: var(--fg-secondary);
  margin: 0 0 10px 0;
  line-height: 1.5;
}

.template-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.tag {
  background: var(--paper-tint);
  padding: 2px 8px;
  border-radius: var(--radius-sm);
  font-size: 11px;
  color: var(--fg-secondary);
}

.template-preview {
  padding-top: 20px;
  border-top: 1px solid var(--border-subtle);
}

.preview-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--fg-primary);
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
  color: var(--fg-secondary);
}

.preview-icon {
  color: var(--fg-tertiary);
  margin-right: 6px;
  flex-shrink: 0;
}

.preview-list li {
  display: flex;
  align-items: center;
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
  color: var(--fg-primary);
}

.required {
  color: var(--cinnabar-400);
}

.form-input,
.form-textarea,
.form-select {
  padding: 10px 14px;
  border: 1px solid var(--border-default);
  border-radius: var(--radius-md);
  font-size: 14px;
  color: var(--fg-primary);
  transition: border-color var(--dur-fast) var(--ease-out);
  font-family: inherit;
  background: var(--glass-bg);
}

.form-input:focus,
.form-textarea:focus,
.form-select:focus {
  outline: none;
  border-color: var(--gold-400);
}

.form-textarea {
  resize: vertical;
}

.result-message {
  padding: 12px 16px;
  border-radius: var(--radius-md);
  font-size: 14px;
}

.result-message.success {
  background: color-mix(in srgb, var(--bronze-500) 12%, transparent);
  color: var(--bronze-500);
  border: 1px solid color-mix(in srgb, var(--bronze-500) 30%, transparent);
}

.result-message.error {
  background: color-mix(in srgb, var(--cinnabar-400) 12%, transparent);
  color: var(--cinnabar-400);
  border: 1px solid color-mix(in srgb, var(--cinnabar-400) 30%, transparent);
}

.generate-btn {
  width: 100%;
  padding: 14px;
  background: var(--gold-400);
  color: var(--ink-950);
  border: none;
  border-radius: var(--radius-lg);
  font-size: 15px;
  font-weight: 600;
  cursor: pointer;
  transition: background var(--dur-fast) var(--ease-out), transform var(--dur-instant) var(--ease-out);
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 10px;
  font-family: var(--font-sans-cn);
}

.generate-btn:hover:not(:disabled) {
  background: var(--gold-300);
  transform: translateY(-1px);
}

.generate-btn:active:not(:disabled) {
  transform: translateY(1px);
}

.generate-btn:disabled {
  opacity: 0.5;
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
  border-top: 1px solid var(--border-subtle);
}

.guide-title {
  font-size: 15px;
  font-weight: 600;
  color: var(--fg-primary);
  margin: 0 0 12px 0;
}

.guide-list {
  margin: 0;
  padding-left: 20px;
  font-size: 13px;
  color: var(--fg-secondary);
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
