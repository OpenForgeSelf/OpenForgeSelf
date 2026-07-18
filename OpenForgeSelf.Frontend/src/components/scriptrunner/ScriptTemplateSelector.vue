<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useScriptRunnerStore } from '@/stores/scriptRunner'
import type { ScriptTemplate, ScriptLanguage, ScriptParameter } from '@/types/scriptRunner'

const emit = defineEmits<{
  (e: 'select', template: ScriptTemplate): void
  (e: 'use-template', code: string, parameters: ScriptParameter[], name: string, description: string, language: ScriptLanguage): void
  (e: 'close'): void
}>()

const store = useScriptRunnerStore()

const searchKeyword = ref('')
const selectedCategory = ref('')
const selectedLanguage = ref<ScriptLanguage | ''>('')
const selectedTemplate = ref<ScriptTemplate | null>(null)
const showPreview = ref(false)

const categories = [
  { value: '', label: '全部分类' },
  { value: 'file', label: '📁 文件处理' },
  { value: 'system', label: '⚙️ 系统管理' },
  { value: 'network', label: '🌐 网络请求' },
  { value: 'data', label: '📊 数据转换' },
  { value: 'text', label: '📝 文本处理' }
]

const languageOptions: { value: ScriptLanguage | ''; label: string; icon: string }[] = [
  { value: '', label: '全部语言', icon: '🌐' },
  { value: 'powershell', label: 'PowerShell', icon: '💠' },
  { value: 'python', label: 'Python', icon: '🐍' },
  { value: 'nodejs', label: 'Node.js', icon: '📦' },
  { value: 'shell', label: 'Shell', icon: '🐚' },
  { value: 'cmd', label: 'CMD', icon: '⚫' }
]

const filteredTemplates = computed(() => {
  let templates = store.scriptTemplates

  if (selectedCategory.value) {
    templates = templates.filter(t => t.category === selectedCategory.value)
  }

  if (selectedLanguage.value) {
    templates = templates.filter(t => t.language === selectedLanguage.value)
  }

  if (searchKeyword.value.trim()) {
    const keyword = searchKeyword.value.toLowerCase()
    templates = templates.filter(t =>
      t.name.toLowerCase().includes(keyword) ||
      t.description.toLowerCase().includes(keyword) ||
      t.tags.some(tag => tag.toLowerCase().includes(keyword))
    )
  }

  return templates
})

const categoryIcon = (category: string): string => {
  const icons: Record<string, string> = {
    file: '📁',
    system: '⚙️',
    network: '🌐',
    data: '📊',
    text: '📝'
  }
  return icons[category] || '📄'
}

onMounted(async () => {
  if (store.scriptTemplates.length === 0) {
    await store.loadScriptTemplates()
  }
})

function handleSelect(template: ScriptTemplate): void {
  selectedTemplate.value = template
  showPreview.value = true
  emit('select', template)
}

function handleUseTemplate(): void {
  if (selectedTemplate.value) {
    emit('use-template',
      selectedTemplate.value.code,
      selectedTemplate.value.parameters,
      selectedTemplate.value.name,
      selectedTemplate.value.description,
      selectedTemplate.value.language
    )
  }
}

function handleClose(): void {
  showPreview.value = false
  selectedTemplate.value = null
  emit('close')
}

function handleClosePreview(): void {
  showPreview.value = false
  selectedTemplate.value = null
}
</script>

<template>
  <div class="script-template-selector">
    <div class="selector-header">
      <h3>📚 脚本模板库</h3>
      <button class="btn-close" @click="handleClose">×</button>
    </div>

    <div class="selector-body">
      <div class="filter-section">
        <div class="search-box">
          <input
            v-model="searchKeyword"
            type="text"
            placeholder="🔍 搜索模板..."
          />
        </div>
        <div class="filter-row">
          <select v-model="selectedCategory">
            <option v-for="cat in categories" :key="cat.value" :value="cat.value">
              {{ cat.label }}
            </option>
          </select>
          <select v-model="selectedLanguage">
            <option v-for="opt in languageOptions" :key="opt.value" :value="opt.value">
              {{ opt.icon }} {{ opt.label }}
            </option>
          </select>
        </div>
      </div>

      <div class="templates-grid">
        <div
          v-for="template in filteredTemplates"
          :key="template.id"
          class="template-card"
          @click="handleSelect(template)"
        >
          <div class="template-icon">
            {{ categoryIcon(template.category) }}
          </div>
          <div class="template-info">
            <h4 class="template-name">{{ template.name }}</h4>
            <p class="template-desc">{{ template.description }}</p>
            <div class="template-meta">
              <span class="template-lang">{{ template.language }}</span>
              <span class="template-params">{{ template.parameters.length }} 参数</span>
            </div>
            <div v-if="template.tags.length > 0" class="template-tags">
              <span v-for="tag in template.tags.slice(0, 3)" :key="tag" class="tag">
                {{ tag }}
              </span>
            </div>
          </div>
        </div>

        <div v-if="filteredTemplates.length === 0 && !store.isLoading" class="empty-state">
          <p>没有找到匹配的模板</p>
        </div>

        <div v-if="store.isLoading" class="loading-state">
          <p>加载中...</p>
        </div>
      </div>
    </div>

    <div v-if="showPreview && selectedTemplate" class="preview-overlay" @click.self="handleClosePreview">
      <div class="preview-panel">
        <div class="preview-header">
          <h4>{{ categoryIcon(selectedTemplate.category) }} {{ selectedTemplate.name }}</h4>
          <button class="btn-close" @click="handleClosePreview">×</button>
        </div>

        <div class="preview-body">
          <div class="preview-section">
            <strong>描述：</strong>
            <p>{{ selectedTemplate.description }}</p>
          </div>

          <div class="preview-section">
            <strong>语言：</strong>
            <span class="lang-badge">{{ selectedTemplate.language }}</span>
          </div>

          <div v-if="selectedTemplate.parameters.length > 0" class="preview-section">
            <strong>参数 ({{ selectedTemplate.parameters.length }})：</strong>
            <ul class="param-list">
              <li v-for="param in selectedTemplate.parameters" :key="param.name">
                <span class="param-name">{{ param.name }}</span>
                <span class="param-type">({{ param.type }})</span>
                <span v-if="param.description"> - {{ param.description }}</span>
              </li>
            </ul>
          </div>

          <div v-if="selectedTemplate.tags.length > 0" class="preview-section">
            <strong>标签：</strong>
            <div class="tags-list">
              <span v-for="tag in selectedTemplate.tags" :key="tag" class="tag">
                {{ tag }}
              </span>
            </div>
          </div>

          <div class="preview-section">
            <strong>代码预览：</strong>
            <pre class="code-preview"><code>{{ selectedTemplate.code.slice(0, 500) }}{{ selectedTemplate.code.length > 500 ? '...' : '' }}</code></pre>
          </div>
        </div>

        <div class="preview-footer">
          <button class="btn btn-secondary" @click="handleClosePreview">关闭</button>
          <button class="btn btn-primary" @click="handleUseTemplate">
            使用此模板
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.script-template-selector {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: var(--bg-secondary);
  border-radius: 8px;
  overflow: hidden;
}

.selector-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px;
  border-bottom: 1px solid var(--border-color);
  background: var(--bg-primary);
}

.selector-header h3 {
  margin: 0;
  font-size: 16px;
}

.btn-close {
  background: none;
  border: none;
  font-size: 20px;
  cursor: pointer;
  color: var(--text-secondary);
  padding: 4px 8px;
}

.btn-close:hover {
  color: var(--text-primary);
}

.selector-body {
  padding: 16px;
  flex: 1;
  overflow-y: auto;
}

.filter-section {
  margin-bottom: 16px;
}

.search-box input {
  width: 100%;
  padding: 10px 12px;
  border: 1px solid var(--border-color);
  border-radius: 6px;
  background: var(--bg-primary);
  color: var(--text-primary);
  font-size: 14px;
  margin-bottom: 12px;
}

.search-box input:focus {
  outline: none;
  border-color: var(--primary-color);
}

.filter-row {
  display: flex;
  gap: 12px;
}

.filter-row select {
  flex: 1;
  padding: 8px 12px;
  border: 1px solid var(--border-color);
  border-radius: 6px;
  background: var(--bg-primary);
  color: var(--text-primary);
  font-size: 14px;
}

.templates-grid {
  display: grid;
  grid-template-columns: 1fr;
  gap: 12px;
}

.template-card {
  display: flex;
  gap: 12px;
  padding: 14px;
  background: var(--bg-primary);
  border: 1px solid var(--border-color);
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.template-card:hover {
  border-color: var(--primary-color);
  transform: translateY(-1px);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}

.template-icon {
  font-size: 28px;
  flex-shrink: 0;
  width: 48px;
  height: 48px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--bg-tertiary);
  border-radius: 8px;
}

.template-info {
  flex: 1;
  min-width: 0;
}

.template-name {
  margin: 0 0 4px 0;
  font-size: 14px;
  font-weight: 600;
}

.template-desc {
  margin: 0 0 8px 0;
  font-size: 12px;
  color: var(--text-secondary);
  line-height: 1.4;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.template-meta {
  display: flex;
  gap: 12px;
  font-size: 11px;
  color: var(--text-secondary);
  margin-bottom: 6px;
}

.template-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}

.tag {
  padding: 2px 8px;
  background: var(--bg-tertiary);
  border-radius: 4px;
  font-size: 11px;
  color: var(--text-secondary);
}

.empty-state,
.loading-state {
  text-align: center;
  padding: 40px 20px;
  color: var(--text-secondary);
}

.preview-overlay {
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

.preview-panel {
  background: var(--bg-primary);
  border-radius: 12px;
  width: 100%;
  max-width: 700px;
  max-height: 85vh;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.preview-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 20px;
  border-bottom: 1px solid var(--border-color);
}

.preview-header h4 {
  margin: 0;
  font-size: 16px;
}

.preview-body {
  padding: 20px;
  flex: 1;
  overflow-y: auto;
}

.preview-section {
  margin-bottom: 16px;
  font-size: 14px;
  line-height: 1.6;
}

.preview-section strong {
  display: block;
  margin-bottom: 6px;
  color: var(--text-primary);
}

.preview-section p {
  margin: 0;
  color: var(--text-secondary);
}

.lang-badge {
  display: inline-block;
  padding: 4px 10px;
  background: var(--primary-color);
  color: white;
  border-radius: 4px;
  font-size: 12px;
}

.param-list {
  margin: 8px 0 0 0;
  padding-left: 20px;
}

.param-list li {
  margin-bottom: 4px;
  font-size: 13px;
}

.param-name {
  font-weight: 600;
  color: var(--primary-color);
}

.param-type {
  color: var(--text-secondary);
  font-size: 12px;
}

.tags-list {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-top: 8px;
}

.code-preview {
  margin: 8px 0 0 0;
  padding: 12px;
  background: var(--bg-tertiary);
  border-radius: 6px;
  overflow-x: auto;
  font-size: 12px;
  line-height: 1.5;
  max-height: 300px;
  overflow-y: auto;
}

.code-preview code {
  font-family: 'Consolas', 'Monaco', monospace;
}

.preview-footer {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  padding: 16px 20px;
  border-top: 1px solid var(--border-color);
}
</style>
