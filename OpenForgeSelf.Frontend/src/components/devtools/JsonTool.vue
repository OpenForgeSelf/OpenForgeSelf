<script setup lang="ts">
import { useDevToolsStore } from '@/stores/devTools'

const store = useDevToolsStore()
</script>

<template>
  <div class="json-tool">
    <div class="toolbar">
      <div class="tool-group">
        <button class="tool-btn primary" :disabled="store.isProcessing" @click="store.formatJson()">
          <i class="fa-solid fa-indent" />
          格式化
        </button>
        <button class="tool-btn" :disabled="store.isProcessing" @click="store.minifyJson()">
          <i class="fa-solid fa-compress" />
          压缩
        </button>
        <button class="tool-btn" :disabled="store.isProcessing" @click="store.validateJson()">
          <i class="fa-solid fa-check-circle" />
          校验
        </button>
      </div>
      <div class="tool-group">
        <label class="indent-label">
          缩进:
          <select v-model.number="store.indentSize" class="indent-select">
            <option :value="2">2 空格</option>
            <option :value="4">4 空格</option>
          </select>
        </label>
      </div>
    </div>

    <div class="jsonpath-section">
      <div class="jsonpath-input">
        <label class="jsonpath-label">JSONPath</label>
        <input
          v-model="store.jsonPathExpression"
          type="text"
          class="jsonpath-text"
          placeholder="例如: $.store.book[0].title"
          @keyup.enter="store.queryJsonPath()"
        />
        <button class="tool-btn small" :disabled="store.isProcessing" @click="store.queryJsonPath()">
          查询
        </button>
      </div>
    </div>

    <div v-if="store.validateResult" class="validate-result" :class="{ valid: store.validateResult.isValid, invalid: !store.validateResult.isValid }">
      <span v-if="store.validateResult.isValid" class="valid-icon">✅</span>
      <span v-else class="invalid-icon">❌</span>
      <span class="validate-text">
        {{ store.validateResult.isValid ? 'JSON 格式有效' : `JSON 格式错误: ${store.validateResult.errorMessage}` }}
        <span v-if="!store.validateResult.isValid && store.validateResult.lineNumber > 0">
          (第 {{ store.validateResult.lineNumber }} 行)
        </span>
      </span>
    </div>

    <div class="editors">
      <div class="editor-panel">
        <div class="editor-header">
          <span class="editor-title">输入</span>
          <button class="clear-btn" @click="store.clearInput()">清空</button>
        </div>
        <textarea
          v-model="store.inputText"
          class="editor-textarea"
          placeholder="在此输入 JSON 文本..."
          spellcheck="false"
        />
      </div>

      <div class="editor-panel">
        <div class="editor-header">
          <span class="editor-title">输出</span>
          <button class="swap-btn" title="交换输入输出" @click="store.swapInputOutput()">
            <i class="fa-solid fa-right-left" />
          </button>
        </div>
        <textarea
          v-model="store.outputText"
          class="editor-textarea output"
          placeholder="处理结果将显示在这里..."
          readonly
          spellcheck="false"
        />
      </div>
    </div>
  </div>
</template>

<style scoped>
.json-tool {
  display: flex;
  flex-direction: column;
  gap: 12px;
  height: 100%;
}

.toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 12px;
}

.tool-group {
  display: flex;
  gap: 8px;
  align-items: center;
}

.tool-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 8px 16px;
  border: 1px solid var(--el-border-color);
  background: var(--el-bg-color);
  color: var(--el-text-color-regular);
  font-size: 14px;
  font-weight: 500;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.tool-btn:hover:not(:disabled) {
  background: var(--el-bg-color-page);
  border-color: var(--border-strong);
}

.tool-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.tool-btn.primary {
  background: var(--el-color-primary);
  border-color: var(--el-color-primary);
  color: var(--el-color-white);
}

.tool-btn.primary:hover:not(:disabled) {
  background: var(--el-color-primary-light-3);
  border-color: var(--el-color-primary-light-3);
}

.tool-btn.small {
  padding: 6px 12px;
  font-size: 13px;
}

.indent-label {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  color: var(--el-text-color-secondary);
}

.indent-select {
  padding: 6px 10px;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  font-size: 14px;
  background: var(--el-bg-color);
  cursor: pointer;
}

.jsonpath-section {
  padding: 12px;
  background: var(--el-bg-color-page);
  border-radius: 8px;
}

.jsonpath-input {
  display: flex;
  gap: 8px;
  align-items: center;
}

.jsonpath-label {
  font-size: 14px;
  font-weight: 500;
  color: var(--el-text-color-regular);
  white-space: nowrap;
}

.jsonpath-text {
  flex: 1;
  padding: 8px 12px;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  font-size: 14px;
  font-family: 'Monaco', 'Menlo', monospace;
}

.validate-result {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 14px;
  border-radius: 8px;
  font-size: 14px;
}

.validate-result.valid {
  background: #f0fdf4;
  border: 1px solid #bbf7d0;
  color: #16a34a;
}

.validate-result.invalid {
  background: #fef2f2;
  border: 1px solid #fecaca;
  color: #dc2626;
}

.editors {
  flex: 1;
  display: flex;
  gap: 12px;
  min-height: 0;
}

.editor-panel {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-width: 0;
  background: var(--el-bg-color);
  border: 1px solid var(--el-border-color);
  border-radius: 8px;
  overflow: hidden;
}

.editor-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 14px;
  background: var(--el-bg-color-page);
  border-bottom: 1px solid var(--el-border-color);
}

.editor-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--el-text-color-regular);
}

.clear-btn,
.swap-btn {
  padding: 4px 10px;
  border: none;
  background: transparent;
  color: var(--el-text-color-secondary);
  font-size: 13px;
  cursor: pointer;
  border-radius: 4px;
  transition: all 0.2s;
}

.clear-btn:hover,
.swap-btn:hover {
  background: var(--el-fill-color);
  color: var(--el-text-color-regular);
}

.editor-textarea {
  flex: 1;
  padding: 12px;
  border: none;
  resize: none;
  font-family: 'Monaco', 'Menlo', 'Consolas', monospace;
  font-size: 13px;
  line-height: 1.6;
  color: var(--el-text-color-primary);
  background: var(--el-bg-color);
  outline: none;
}

.editor-textarea.output {
  background: var(--el-bg-color-page);
}
</style>
