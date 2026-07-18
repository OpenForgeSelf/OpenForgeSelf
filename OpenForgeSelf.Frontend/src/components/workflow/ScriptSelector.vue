<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { scriptRunnerApi } from '@/services/scriptRunnerApi'
import type { Script, ScriptLanguage } from '@/types/scriptRunner'

const props = defineProps<{
  modelValue?: string
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: string): void
  (e: 'select', script: Script): void
}>()

const scripts = ref<Script[]>([])
const isLoading = ref(false)
const searchKeyword = ref('')
const selectedLanguage = ref<ScriptLanguage | ''>('')
const showSelector = ref(false)
const selectedScript = ref<Script | null>(null)

const languages: { value: ScriptLanguage | ''; label: string }[] = [
  { value: '', label: '全部语言' },
  { value: 'powershell', label: 'PowerShell' },
  { value: 'python', label: 'Python' },
  { value: 'nodejs', label: 'Node.js' },
  { value: 'shell', label: 'Shell' },
  { value: 'cmd', label: 'CMD' }
]

const filteredScripts = computed(() => {
  let result = scripts.value

  if (searchKeyword.value) {
    const keyword = searchKeyword.value.toLowerCase()
    result = result.filter(s =>
      s.name.toLowerCase().includes(keyword) ||
      s.description?.toLowerCase().includes(keyword) ||
      s.tags.some(t => t.toLowerCase().includes(keyword))
    )
  }

  if (selectedLanguage.value) {
    result = result.filter(s => s.language === selectedLanguage.value)
  }

  return result
})

const selectedScriptInfo = computed(() => {
  if (!props.modelValue) return null
  return scripts.value.find(s => s.id === props.modelValue) || null
})

async function loadScripts(): Promise<void> {
  try {
    isLoading.value = true
    const result = await scriptRunnerApi.listScripts({ pageSize: 100 })
    scripts.value = result.items
  } catch (e) {
    console.error('加载脚本列表失败:', e)
  } finally {
    isLoading.value = false
  }
}

function openSelector(): void {
  showSelector.value = true
  if (scripts.value.length === 0) {
    loadScripts()
  }
}

function closeSelector(): void {
  showSelector.value = false
}

function selectScript(script: Script): void {
  selectedScript.value = script
  emit('update:modelValue', script.id)
  emit('select', script)
  closeSelector()
}

function getLanguageLabel(lang: ScriptLanguage): string {
  return languages.find(l => l.value === lang)?.label || lang
}

function getLanguageIcon(lang: ScriptLanguage): string {
  const icons: Record<ScriptLanguage, string> = {
    powershell: '🔵',
    python: '🐍',
    nodejs: '🟢',
    shell: '🐚',
    cmd: '⬛'
  }
  return icons[lang] || '📜'
}

onMounted(() => {
  if (props.modelValue) {
    loadScripts()
  }
})
</script>

<template>
  <div class="script-selector">
    <div class="selector-trigger" @click="openSelector">
      <template v-if="selectedScriptInfo">
        <span class="script-icon">{{ getLanguageIcon(selectedScriptInfo.language) }}</span>
        <span class="script-name">{{ selectedScriptInfo.name }}</span>
        <span class="script-lang">{{ getLanguageLabel(selectedScriptInfo.language) }}</span>
      </template>
      <template v-else>
        <span class="placeholder">选择脚本...</span>
      </template>
      <span class="arrow">▼</span>
    </div>

    <div v-if="showSelector" class="selector-dropdown">
      <div class="dropdown-header">
        <input
          v-model="searchKeyword"
          type="text"
          class="search-input"
          placeholder="搜索脚本..."
        />
        <select v-model="selectedLanguage" class="language-filter">
          <option v-for="lang in languages" :key="lang.value" :value="lang.value">
            {{ lang.label }}
          </option>
        </select>
      </div>

      <div class="script-list">
        <div v-if="isLoading" class="loading">加载中...</div>
        <div v-else-if="filteredScripts.length === 0" class="empty">
          没有找到匹配的脚本
        </div>
        <div
          v-for="script in filteredScripts"
          :key="script.id"
          class="script-item"
          :class="{ selected: script.id === modelValue }"
          @click="selectScript(script)"
        >
          <span class="script-icon">{{ getLanguageIcon(script.language) }}</span>
          <div class="script-info">
            <div class="script-name">{{ script.name }}</div>
            <div class="script-desc">{{ script.description || '暂无描述' }}</div>
          </div>
          <span class="script-lang-badge">{{ getLanguageLabel(script.language) }}</span>
        </div>
      </div>

      <div class="dropdown-footer">
        <button class="btn btn-text" @click="closeSelector">关闭</button>
      </div>
    </div>

    <div v-if="selectedScriptInfo" class="script-preview">
      <div class="preview-header">
        <span>脚本预览</span>
      </div>
      <div class="preview-content">
        <div class="preview-row">
          <span class="label">语言:</span>
          <span>{{ getLanguageLabel(selectedScriptInfo.language) }}</span>
        </div>
        <div class="preview-row">
          <span class="label">参数:</span>
          <span>{{ selectedScriptInfo.parameters.length }} 个</span>
        </div>
        <div class="preview-row">
          <span class="label">超时:</span>
          <span>{{ selectedScriptInfo.timeout }} 秒</span>
        </div>
        <div v-if="selectedScriptInfo.tags.length > 0" class="preview-tags">
          <span v-for="tag in selectedScriptInfo.tags" :key="tag" class="tag">
            {{ tag }}
          </span>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.script-selector {
  position: relative;
  width: 100%;
}

.selector-trigger {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  border: 1px solid #dee2e6;
  border-radius: 8px;
  cursor: pointer;
  background: #fff;
  transition: border-color 0.2s ease;
}

.selector-trigger:hover {
  border-color: #1976d2;
}

.script-icon {
  font-size: 16px;
}

.script-name {
  flex: 1;
  font-size: 14px;
  color: #212529;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.script-lang {
  font-size: 12px;
  color: #6c757d;
  background: #f8f9fa;
  padding: 2px 6px;
  border-radius: 4px;
}

.placeholder {
  flex: 1;
  color: #adb5bd;
  font-size: 14px;
}

.arrow {
  font-size: 10px;
  color: #6c757d;
}

.selector-dropdown {
  position: absolute;
  top: 100%;
  left: 0;
  right: 0;
  margin-top: 4px;
  background: #fff;
  border: 1px solid #dee2e6;
  border-radius: 8px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
  z-index: 1000;
  max-height: 400px;
  display: flex;
  flex-direction: column;
}

.dropdown-header {
  padding: 12px;
  border-bottom: 1px solid #e9ecef;
  display: flex;
  gap: 8px;
}

.search-input {
  flex: 1;
  padding: 6px 10px;
  border: 1px solid #dee2e6;
  border-radius: 6px;
  font-size: 13px;
}

.search-input:focus {
  outline: none;
  border-color: #1976d2;
}

.language-filter {
  padding: 6px 10px;
  border: 1px solid #dee2e6;
  border-radius: 6px;
  font-size: 13px;
  background: #fff;
}

.script-list {
  flex: 1;
  overflow-y: auto;
  max-height: 280px;
}

.loading,
.empty {
  padding: 24px;
  text-align: center;
  color: #6c757d;
  font-size: 13px;
}

.script-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 12px;
  cursor: pointer;
  transition: background 0.2s ease;
  border-bottom: 1px solid #f8f9fa;
}

.script-item:hover {
  background: #f8f9fa;
}

.script-item.selected {
  background: #e3f2fd;
}

.script-info {
  flex: 1;
  min-width: 0;
}

.script-info .script-name {
  font-size: 13px;
  font-weight: 500;
  color: #212529;
  margin-bottom: 2px;
}

.script-desc {
  font-size: 12px;
  color: #6c757d;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.script-lang-badge {
  font-size: 11px;
  color: #495057;
  background: #f1f3f5;
  padding: 2px 6px;
  border-radius: 4px;
  flex-shrink: 0;
}

.dropdown-footer {
  padding: 8px 12px;
  border-top: 1px solid #e9ecef;
  text-align: right;
}

.btn {
  padding: 6px 12px;
  border-radius: 6px;
  font-size: 13px;
  cursor: pointer;
  border: none;
  transition: background 0.2s ease;
}

.btn-text {
  background: none;
  color: #6c757d;
}

.btn-text:hover {
  color: #495057;
  background: #f8f9fa;
}

.script-preview {
  margin-top: 12px;
  background: #f8f9fa;
  border-radius: 8px;
  overflow: hidden;
}

.preview-header {
  padding: 8px 12px;
  background: #e9ecef;
  font-size: 12px;
  font-weight: 500;
  color: #495057;
}

.preview-content {
  padding: 12px;
  font-size: 13px;
}

.preview-row {
  display: flex;
  gap: 8px;
  margin-bottom: 6px;
}

.preview-row .label {
  color: #6c757d;
  min-width: 40px;
}

.preview-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  margin-top: 8px;
}

.tag {
  font-size: 11px;
  padding: 2px 6px;
  background: #e3f2fd;
  color: #1976d2;
  border-radius: 4px;
}
</style>
