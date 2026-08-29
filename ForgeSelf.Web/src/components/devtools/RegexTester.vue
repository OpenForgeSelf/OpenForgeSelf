<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { devToolsApi } from '@/services/devToolsApi'
import type { RegexMatchResult, RegexReplaceResult, RegexPatternItem } from '@/types/devTools'

const pattern = ref('')
const testInput = ref('')
const replacement = ref('')
const ignoreCase = ref(false)
const multiline = ref(false)
const singleline = ref(false)
const globalFlag = ref(true)

const matchResult = ref<RegexMatchResult | null>(null)
const replaceResult = ref<RegexReplaceResult | null>(null)
const patterns = ref<RegexPatternItem[]>([])
const selectedCategory = ref('')
const categories = ref<string[]>([])
const showPatterns = ref(true)
const isProcessing = ref(false)
const activeTab = ref<'match' | 'replace' | 'split'>('match')
const splitResult = ref<string[]>([])

async function loadPatterns(): Promise<void> {
  try {
    patterns.value = await devToolsApi.getRegexPatterns()
    const cats = new Set(patterns.value.map(p => p.category))
    categories.value = Array.from(cats)
  } catch (e) {
    console.error('加载正则模板失败', e)
  }
}

function filteredPatterns(): RegexPatternItem[] {
  if (!selectedCategory.value) return patterns.value
  return patterns.value.filter(p => p.category === selectedCategory.value)
}

function applyPattern(pat: RegexPatternItem): void {
  pattern.value = pat.pattern
  if (pat.example) {
    testInput.value = pat.example
  }
  testMatch()
}

async function testMatch(): Promise<void> {
  if (!pattern.value.trim()) {
    matchResult.value = null
    return
  }
  try {
    isProcessing.value = true
    matchResult.value = await devToolsApi.testRegex(pattern.value, testInput.value, {
      ignoreCase: ignoreCase.value,
      multiline: multiline.value,
      singleline: singleline.value,
    })
  } catch (e) {
    matchResult.value = {
      success: false,
      matchCount: 0,
      captureGroupCount: 0,
      matches: [],
      error: e instanceof Error ? e.message : '测试失败',
    }
  } finally {
    isProcessing.value = false
  }
}

async function doReplace(): Promise<void> {
  if (!pattern.value.trim()) {
    replaceResult.value = null
    return
  }
  try {
    isProcessing.value = true
    replaceResult.value = await devToolsApi.regexReplace(pattern.value, testInput.value, replacement.value, {
      ignoreCase: ignoreCase.value,
      multiline: multiline.value,
      singleline: singleline.value,
    })
  } catch (e) {
    replaceResult.value = {
      result: testInput.value,
      replacementCount: 0,
      error: e instanceof Error ? e.message : '替换失败',
    }
  } finally {
    isProcessing.value = false
  }
}

async function doSplit(): Promise<void> {
  if (!pattern.value.trim()) {
    splitResult.value = []
    return
  }
  try {
    isProcessing.value = true
    const result = await devToolsApi.regexSplit(pattern.value, testInput.value, {
      ignoreCase: ignoreCase.value,
      multiline: multiline.value,
      singleline: singleline.value,
    })
    splitResult.value = result.parts
  } catch {
    splitResult.value = [testInput.value]
  } finally {
    isProcessing.value = false
  }
}

function copyToClipboard(text: string): void {
  navigator.clipboard.writeText(text).catch(() => {})
}

const highlightedText = computed(() => {
  if (!matchResult.value?.success || !matchResult.value.matches.length || !testInput.value) {
    return testInput.value
  }

  const input = testInput.value
  const matches = matchResult.value.matches
  let result = ''
  let lastIndex = 0

  for (const m of matches) {
    result += escapeHtml(input.slice(lastIndex, m.index))
    result += `<mark class="match-highlight">${escapeHtml(m.value)}</mark>`
    lastIndex = m.index + m.length
  }
  result += escapeHtml(input.slice(lastIndex))

  return result
})

function escapeHtml(text: string): string {
  const div = document.createElement('div')
  div.textContent = text
  return div.innerHTML
}

watch([pattern, testInput, ignoreCase, multiline, singleline], () => {
  if (activeTab.value === 'match') {
    testMatch()
  }
}, { debounce: 300 } as Record<string, unknown>)

watch(activeTab, (tab) => {
  if (tab === 'match') testMatch()
  else if (tab === 'replace') doReplace()
  else if (tab === 'split') doSplit()
})

onMounted(() => {
  loadPatterns()
})
</script>

<template>
  <div class="regex-tester">
    <div class="regex-layout">
      <div class="regex-main">
        <div class="pattern-input-section">
          <div class="input-row">
            <span class="regex-delimiter">/</span>
            <input
              v-model="pattern"
              type="text"
              class="pattern-input"
              placeholder="输入正则表达式..."
              spellcheck="false"
            />
            <span class="regex-delimiter">/</span>
            <span class="flags-display">
              <span v-if="globalFlag">g</span>
              <span v-if="ignoreCase">i</span>
              <span v-if="multiline">m</span>
              <span v-if="singleline">s</span>
            </span>
            <button class="copy-btn" title="复制正则" @click="copyToClipboard(pattern)">
              <i class="fa-regular fa-copy" />
            </button>
          </div>

          <div class="flags-row">
            <label class="flag-checkbox">
              <input v-model="globalFlag" type="checkbox" />
              <span>全局 (g)</span>
            </label>
            <label class="flag-checkbox">
              <input v-model="ignoreCase" type="checkbox" />
              <span>忽略大小写 (i)</span>
            </label>
            <label class="flag-checkbox">
              <input v-model="multiline" type="checkbox" />
              <span>多行 (m)</span>
            </label>
            <label class="flag-checkbox">
              <input v-model="singleline" type="checkbox" />
              <span>单行 (s)</span>
            </label>
          </div>
        </div>

        <div class="tabs-row">
          <button
            class="tab-btn"
            :class="{ active: activeTab === 'match' }"
            @click="activeTab = 'match'"
          >
            <i class="fa-solid fa-magnifying-glass" />
            匹配测试
          </button>
          <button
            class="tab-btn"
            :class="{ active: activeTab === 'replace' }"
            @click="activeTab = 'replace'"
          >
            <i class="fa-solid fa-right-left" />
            替换
          </button>
          <button
            class="tab-btn"
            :class="{ active: activeTab === 'split' }"
            @click="activeTab = 'split'"
          >
            <i class="fa-solid fa-scissors" />
            分割
          </button>
        </div>

        <div v-if="activeTab === 'match'" class="content-area">
          <div class="editor-panel">
            <div class="editor-header">
              <span class="editor-title">测试文本</span>
              <span v-if="matchResult?.success" class="match-stats">
                <span class="stat-item">匹配: {{ matchResult.matchCount }}</span>
                <span class="stat-item">捕获组: {{ matchResult.captureGroupCount }}</span>
              </span>
              <span v-if="matchResult?.error" class="error-text">
                {{ matchResult.error }}
              </span>
            </div>
            <textarea
              v-model="testInput"
              class="editor-textarea"
              placeholder="在此输入要测试的文本..."
              spellcheck="false"
            />
          </div>

          <div class="result-panel">
            <div class="editor-header">
              <span class="editor-title">高亮预览</span>
            </div>
            <div class="highlight-preview" v-html="highlightedText || '<span class=\'placeholder\'>匹配结果将显示在这里...</span>'" />
          </div>

          <div v-if="matchResult?.matches?.length" class="matches-list">
            <div class="editor-header">
              <span class="editor-title">匹配详情</span>
            </div>
            <div class="matches-container">
              <div
                v-for="(match, idx) in matchResult.matches"
                :key="idx"
                class="match-item"
              >
                <div class="match-header">
                  <span class="match-index">匹配 #{{ idx + 1 }}</span>
                  <span class="match-position">位置: {{ match.index }}, 长度: {{ match.length }}</span>
                </div>
                <code class="match-value">"{{ match.value }}"</code>
                <div v-if="match.groups?.length" class="groups-list">
                  <div
                    v-for="(group, gIdx) in match.groups"
                    :key="gIdx"
                    class="group-item"
                  >
                    <span class="group-name">{{ group.name || `$${gIdx + 1}` }}:</span>
                    <code class="group-value">{{ group.value || '(空)' }}</code>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div v-if="activeTab === 'replace'" class="content-area">
          <div class="replace-input-section">
            <label class="replace-label">替换为：</label>
            <input
              v-model="replacement"
              type="text"
              class="replace-input"
              placeholder="输入替换文本（支持 $1, $2 等引用）..."
              spellcheck="false"
            />
          </div>

          <div class="editor-panel">
            <div class="editor-header">
              <span class="editor-title">原始文本</span>
            </div>
            <textarea
              v-model="testInput"
              class="editor-textarea"
              placeholder="在此输入原始文本..."
              spellcheck="false"
            />
          </div>

          <div class="result-panel">
            <div class="editor-header">
              <span class="editor-title">替换结果</span>
              <span v-if="replaceResult" class="match-stats">
                替换了 {{ replaceResult.replacementCount }} 处
              </span>
              <span v-if="replaceResult?.error" class="error-text">
                {{ replaceResult.error }}
              </span>
              <button v-if="replaceResult?.result" class="copy-btn-sm" @click="copyToClipboard(replaceResult.result)">
                <i class="fa-regular fa-copy" />
                复制
              </button>
            </div>
            <textarea
              :value="replaceResult?.result || ''"
              class="editor-textarea output"
              placeholder="替换结果将显示在这里..."
              readonly
              spellcheck="false"
            />
          </div>
        </div>

        <div v-if="activeTab === 'split'" class="content-area">
          <div class="editor-panel">
            <div class="editor-header">
              <span class="editor-title">待分割文本</span>
            </div>
            <textarea
              v-model="testInput"
              class="editor-textarea"
              placeholder="在此输入要分割的文本..."
              spellcheck="false"
            />
          </div>

          <div class="result-panel">
            <div class="editor-header">
              <span class="editor-title">分割结果</span>
              <span class="match-stats">{{ splitResult.length }} 部分</span>
            </div>
            <div class="split-results">
              <div v-if="splitResult.length === 0" class="placeholder">
                分割结果将显示在这里...
              </div>
              <div
                v-for="(part, idx) in splitResult"
                :key="idx"
                class="split-item"
              >
                <span class="split-index">{{ idx + 1 }}</span>
                <code class="split-value">{{ part || '(空)' }}</code>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div v-if="showPatterns" class="patterns-sidebar">
        <div class="sidebar-header">
          <span class="sidebar-title">常用正则</span>
          <button class="toggle-btn" @click="showPatterns = false">
            <i class="fa-solid fa-chevron-right" />
          </button>
        </div>

        <div class="category-filter">
          <select v-model="selectedCategory" class="category-select">
            <option value="">全部分类</option>
            <option v-for="cat in categories" :key="cat" :value="cat">{{ cat }}</option>
          </select>
        </div>

        <div class="patterns-list">
          <div
            v-for="pat in filteredPatterns()"
            :key="pat.name"
            class="pattern-card"
            @click="applyPattern(pat)"
          >
            <div class="pattern-name">{{ pat.name }}</div>
            <code class="pattern-regex">{{ pat.pattern }}</code>
            <div class="pattern-desc">{{ pat.description }}</div>
          </div>
        </div>
      </div>

      <button v-if="!showPatterns" class="patterns-toggle-btn" @click="showPatterns = true">
        <i class="fa-solid fa-chevron-left" />
        模板
      </button>
    </div>
  </div>
</template>

<style scoped>
.regex-tester {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
}

.regex-layout {
  display: flex;
  gap: 12px;
  height: 100%;
  min-height: 0;
  flex: 1;
}

.regex-main {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 10px;
  min-width: 0;
}

.pattern-input-section {
  flex-shrink: 0;
}

.input-row {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 12px;
  background: #f8f9fa;
  border: 1px solid #dee2e6;
  border-radius: 8px;
}

.regex-delimiter {
  font-family: 'Monaco', 'Menlo', 'Consolas', monospace;
  font-size: 18px;
  color: #6c757d;
  font-weight: bold;
}

.pattern-input {
  flex: 1;
  border: none;
  background: transparent;
  font-family: 'Monaco', 'Menlo', 'Consolas', monospace;
  font-size: 14px;
  color: #212529;
  outline: none;
  padding: 4px 0;
}

.flags-display {
  font-family: 'Monaco', 'Menlo', 'Consolas', monospace;
  font-size: 14px;
  color: #0d6efd;
  font-weight: 600;
}

.copy-btn {
  padding: 6px 10px;
  border: 1px solid #dee2e6;
  background: white;
  color: #6c757d;
  border-radius: 6px;
  cursor: pointer;
  font-size: 13px;
  transition: all 0.2s;
}

.copy-btn:hover {
  background: #e9ecef;
  color: #495057;
}

.flags-row {
  display: flex;
  gap: 16px;
  padding: 8px 4px;
  flex-wrap: wrap;
}

.flag-checkbox {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  color: #495057;
  cursor: pointer;
  user-select: none;
}

.flag-checkbox input {
  cursor: pointer;
}

.tabs-row {
  display: flex;
  gap: 4px;
  padding: 4px;
  background: #f8f9fa;
  border-radius: 8px;
  flex-shrink: 0;
}

.tab-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 14px;
  border: none;
  background: transparent;
  color: #6c757d;
  font-size: 13px;
  font-weight: 500;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.tab-btn:hover {
  background: #e9ecef;
  color: #495057;
}

.tab-btn.active {
  background: #0d6efd;
  color: white;
}

.content-area {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 10px;
  min-height: 0;
}

.editor-panel,
.result-panel {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-height: 0;
  background: white;
  border: 1px solid #dee2e6;
  border-radius: 8px;
  overflow: hidden;
}

.editor-header {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 14px;
  background: #f8f9fa;
  border-bottom: 1px solid #dee2e6;
  flex-shrink: 0;
}

.editor-title {
  font-size: 13px;
  font-weight: 600;
  color: #495057;
}

.match-stats {
  display: flex;
  gap: 12px;
  font-size: 12px;
  color: #6c757d;
}

.stat-item {
  padding: 2px 8px;
  background: #e9ecef;
  border-radius: 4px;
  font-weight: 500;
}

.error-text {
  color: #dc2626;
  font-size: 12px;
  font-weight: 500;
}

.editor-textarea {
  flex: 1;
  padding: 12px;
  border: none;
  resize: none;
  font-family: 'Monaco', 'Menlo', 'Consolas', monospace;
  font-size: 13px;
  line-height: 1.6;
  color: #212529;
  background: white;
  outline: none;
  min-height: 80px;
}

.editor-textarea.output {
  background: #f8f9fa;
}

.highlight-preview {
  flex: 1;
  padding: 12px;
  font-family: 'Monaco', 'Menlo', 'Consolas', monospace;
  font-size: 13px;
  line-height: 1.6;
  color: #212529;
  background: #f8f9fa;
  white-space: pre-wrap;
  word-break: break-all;
  overflow-y: auto;
  min-height: 80px;
}

.placeholder {
  color: #adb5bd;
}

.match-highlight {
  background: #fef08a;
  padding: 1px 2px;
  border-radius: 2px;
  color: #854d0e;
}

.matches-list {
  max-height: 200px;
  overflow-y: auto;
  border: 1px solid #dee2e6;
  border-radius: 8px;
  flex-shrink: 0;
}

.matches-container {
  padding: 8px;
}

.match-item {
  padding: 10px;
  background: #f8f9fa;
  border-radius: 6px;
  margin-bottom: 6px;
}

.match-item:last-child {
  margin-bottom: 0;
}

.match-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 6px;
}

.match-index {
  font-size: 12px;
  font-weight: 600;
  color: #0d6efd;
}

.match-position {
  font-size: 11px;
  color: #6c757d;
}

.match-value {
  display: block;
  padding: 6px 8px;
  background: white;
  border-radius: 4px;
  font-size: 12px;
  color: #212529;
  word-break: break-all;
  margin-bottom: 6px;
}

.groups-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding-left: 12px;
  border-left: 2px solid #dee2e6;
}

.group-item {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12px;
}

.group-name {
  color: #6c757d;
  font-weight: 500;
  min-width: 60px;
}

.group-value {
  color: #212529;
}

.replace-input-section {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-shrink: 0;
}

.replace-label {
  font-size: 13px;
  font-weight: 500;
  color: #495057;
  white-space: nowrap;
}

.replace-input {
  flex: 1;
  padding: 8px 12px;
  border: 1px solid #dee2e6;
  border-radius: 6px;
  font-family: 'Monaco', 'Menlo', 'Consolas', monospace;
  font-size: 13px;
  color: #212529;
  outline: none;
  transition: border-color 0.2s;
}

.replace-input:focus {
  border-color: #0d6efd;
}

.copy-btn-sm {
  padding: 4px 10px;
  border: 1px solid #dee2e6;
  background: white;
  color: #6c757d;
  border-radius: 4px;
  cursor: pointer;
  font-size: 12px;
  display: flex;
  align-items: center;
  gap: 4px;
  transition: all 0.2s;
  margin-left: auto;
}

.copy-btn-sm:hover {
  background: #e9ecef;
  color: #495057;
}

.split-results {
  flex: 1;
  padding: 8px;
  overflow-y: auto;
  background: #f8f9fa;
}

.split-item {
  display: flex;
  align-items: flex-start;
  gap: 10px;
  padding: 8px 10px;
  background: white;
  border-radius: 6px;
  margin-bottom: 4px;
}

.split-item:last-child {
  margin-bottom: 0;
}

.split-index {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
  background: #0d6efd;
  color: white;
  border-radius: 50%;
  font-size: 11px;
  font-weight: 600;
  flex-shrink: 0;
}

.split-value {
  flex: 1;
  font-size: 12px;
  color: #212529;
  word-break: break-all;
}

.patterns-sidebar {
  width: 280px;
  display: flex;
  flex-direction: column;
  background: white;
  border: 1px solid #dee2e6;
  border-radius: 8px;
  overflow: hidden;
  flex-shrink: 0;
}

.sidebar-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 14px;
  background: #f8f9fa;
  border-bottom: 1px solid #dee2e6;
}

.sidebar-title {
  font-size: 13px;
  font-weight: 600;
  color: #495057;
}

.toggle-btn {
  padding: 4px 8px;
  border: none;
  background: transparent;
  color: #6c757d;
  cursor: pointer;
  border-radius: 4px;
  font-size: 12px;
  transition: all 0.2s;
}

.toggle-btn:hover {
  background: #e9ecef;
  color: #495057;
}

.category-filter {
  padding: 10px;
  border-bottom: 1px solid #e9ecef;
}

.category-select {
  width: 100%;
  padding: 6px 10px;
  border: 1px solid #dee2e6;
  border-radius: 6px;
  font-size: 13px;
  color: #495057;
  background: white;
  outline: none;
  cursor: pointer;
}

.patterns-list {
  flex: 1;
  overflow-y: auto;
  padding: 8px;
}

.pattern-card {
  padding: 10px;
  background: #f8f9fa;
  border-radius: 6px;
  margin-bottom: 6px;
  cursor: pointer;
  transition: all 0.2s;
  border: 1px solid transparent;
}

.pattern-card:hover {
  background: #e9ecef;
  border-color: #0d6efd;
}

.pattern-name {
  font-size: 13px;
  font-weight: 600;
  color: #212529;
  margin-bottom: 4px;
}

.pattern-regex {
  display: block;
  font-size: 11px;
  color: #0d6efd;
  word-break: break-all;
  margin-bottom: 4px;
}

.pattern-desc {
  font-size: 11px;
  color: #6c757d;
  line-height: 1.4;
}

.patterns-toggle-btn {
  position: absolute;
  right: 0;
  top: 50%;
  transform: translateY(-50%);
  padding: 16px 6px;
  background: #f8f9fa;
  border: 1px solid #dee2e6;
  border-right: none;
  border-radius: 8px 0 0 8px;
  cursor: pointer;
  font-size: 12px;
  color: #6c757d;
  writing-mode: vertical-rl;
  text-orientation: mixed;
  transition: all 0.2s;
}

.patterns-toggle-btn:hover {
  background: #e9ecef;
  color: #495057;
}
</style>
