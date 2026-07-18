<script setup lang="ts">
import { useDevToolsStore } from '@/stores/devTools'
import type { EncodingType } from '@/types/devTools'

const store = useDevToolsStore()

const encodingTypes: { key: EncodingType; label: string }[] = [
  { key: 'base64', label: 'Base64' },
  { key: 'url', label: 'URL' },
  { key: 'unicode', label: 'Unicode' },
  { key: 'html', label: 'HTML 实体' },
  { key: 'hex', label: '十六进制' },
]
</script>

<template>
  <div class="encoding-tool">
    <div class="toolbar">
      <div class="type-selector">
        <button
          v-for="type in encodingTypes"
          :key="type.key"
          class="type-btn"
          :class="{ active: store.encodingType === type.key }"
          @click="store.setEncodingType(type.key)"
        >
          {{ type.label }}
        </button>
      </div>
      <div class="action-group">
        <button class="tool-btn primary" :disabled="store.isProcessing" @click="store.encodeText()">
          <i class="fa-solid fa-lock" />
          编码
        </button>
        <button class="tool-btn" :disabled="store.isProcessing" @click="store.decodeText()">
          <i class="fa-solid fa-unlock" />
          解码
        </button>
      </div>
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
          placeholder="在此输入文本..."
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
.encoding-tool {
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

.type-selector {
  display: flex;
  gap: 4px;
  padding: 4px;
  background: var(--bg-secondary);
  border-radius: 8px;
}

.type-btn {
  padding: 8px 14px;
  border: none;
  background: transparent;
  color: var(--text-muted);
  font-size: 14px;
  font-weight: 500;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.type-btn:hover {
  background: var(--bg-hover);
  color: var(--text-secondary);
}

.type-btn.active {
  background: var(--primary-color);
  color: var(--primary-contrast);
}

.action-group {
  display: flex;
  gap: 8px;
}

.tool-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 8px 16px;
  border: 1px solid var(--border-color);
  background: var(--bg-card);
  color: var(--text-secondary);
  font-size: 14px;
  font-weight: 500;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.tool-btn:hover:not(:disabled) {
  background: var(--bg-secondary);
  border-color: var(--border-strong);
}

.tool-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.tool-btn.primary {
  background: var(--primary-color);
  border-color: var(--primary-color);
  color: var(--primary-contrast);
}

.tool-btn.primary:hover:not(:disabled) {
  background: var(--primary-hover);
  border-color: var(--primary-hover);
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
  background: var(--bg-card);
  border: 1px solid var(--border-color);
  border-radius: 8px;
  overflow: hidden;
}

.editor-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 14px;
  background: var(--bg-secondary);
  border-bottom: 1px solid var(--border-color);
}

.editor-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-secondary);
}

.clear-btn,
.swap-btn {
  padding: 4px 10px;
  border: none;
  background: transparent;
  color: var(--text-muted);
  font-size: 13px;
  cursor: pointer;
  border-radius: 4px;
  transition: all 0.2s;
}

.clear-btn:hover,
.swap-btn:hover {
  background: var(--bg-hover);
  color: var(--text-secondary);
}

.editor-textarea {
  flex: 1;
  padding: 12px;
  border: none;
  resize: none;
  font-family: 'Monaco', 'Menlo', 'Consolas', monospace;
  font-size: 13px;
  line-height: 1.6;
  color: var(--text-primary);
  background: var(--bg-card);
  outline: none;
}

.editor-textarea.output {
  background: var(--bg-secondary);
}
</style>
