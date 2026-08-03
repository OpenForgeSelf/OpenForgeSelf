<script setup lang="ts">
import { useDevToolsStore } from '@/stores/devTools'
import type { HashType } from '@/types/devTools'

const store = useDevToolsStore()

const hashTypes: { key: HashType; label: string }[] = [
  { key: 'md5', label: 'MD5' },
  { key: 'sha1', label: 'SHA-1' },
  { key: 'sha256', label: 'SHA-256' },
  { key: 'sha512', label: 'SHA-512' },
]

function copyToClipboard(text: string): void {
  navigator.clipboard.writeText(text).catch(() => {})
}
</script>

<template>
  <div class="hash-tool">
    <div class="toolbar">
      <div class="type-selector">
        <button
          v-for="type in hashTypes"
          :key="type.key"
          class="type-btn"
          :class="{ active: store.hashType === type.key }"
          @click="store.setHashType(type.key)"
        >
          {{ type.label }}
        </button>
      </div>
      <div class="action-group">
        <button class="tool-btn" :disabled="store.isProcessing" @click="store.computeAllHashes()">
          <i class="fa-solid fa-layer-group" />
          计算全部
        </button>
        <button class="tool-btn primary" :disabled="store.isProcessing" @click="store.computeHash()">
          <i class="fa-solid fa-fingerprint" />
          计算哈希
        </button>
      </div>
    </div>

    <div v-if="store.hashAllResult" class="all-hashes">
      <h4 class="all-hashes-title">所有哈希值</h4>
      <div class="hash-grid">
        <div class="hash-item">
          <span class="hash-label">MD5</span>
          <div class="hash-value-row">
            <code class="hash-value">{{ store.hashAllResult.md5 }}</code>
            <button class="copy-btn" title="复制" @click="copyToClipboard(store.hashAllResult.md5)">
              <i class="fa-regular fa-copy" />
            </button>
          </div>
        </div>
        <div class="hash-item">
          <span class="hash-label">SHA-1</span>
          <div class="hash-value-row">
            <code class="hash-value">{{ store.hashAllResult.sha1 }}</code>
            <button class="copy-btn" title="复制" @click="copyToClipboard(store.hashAllResult.sha1)">
              <i class="fa-regular fa-copy" />
            </button>
          </div>
        </div>
        <div class="hash-item">
          <span class="hash-label">SHA-256</span>
          <div class="hash-value-row">
            <code class="hash-value">{{ store.hashAllResult.sha256 }}</code>
            <button class="copy-btn" title="复制" @click="copyToClipboard(store.hashAllResult.sha256)">
              <i class="fa-regular fa-copy" />
            </button>
          </div>
        </div>
        <div class="hash-item">
          <span class="hash-label">SHA-512</span>
          <div class="hash-value-row">
            <code class="hash-value">{{ store.hashAllResult.sha512 }}</code>
            <button class="copy-btn" title="复制" @click="copyToClipboard(store.hashAllResult.sha512)">
              <i class="fa-regular fa-copy" />
            </button>
          </div>
        </div>
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
          placeholder="在此输入需要计算哈希的文本..."
          spellcheck="false"
        />
      </div>

      <div class="editor-panel">
        <div class="editor-header">
          <span class="editor-title">输出</span>
        </div>
        <textarea
          v-model="store.outputText"
          class="editor-textarea output"
          placeholder="哈希值将显示在这里..."
          readonly
          spellcheck="false"
        />
      </div>
    </div>
  </div>
</template>

<style scoped>
.hash-tool {
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
  background: var(--el-bg-color-page);
  border-radius: 8px;
}

.type-btn {
  padding: 8px 14px;
  border: none;
  background: transparent;
  color: var(--el-text-color-secondary);
  font-size: 14px;
  font-weight: 500;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.type-btn:hover {
  background: var(--el-fill-color);
  color: var(--el-text-color-regular);
}

.type-btn.active {
  background: var(--el-color-primary);
  color: var(--el-color-white);
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

.all-hashes {
  padding: 16px;
  background: var(--el-bg-color-page);
  border-radius: 8px;
}

.all-hashes-title {
  margin: 0 0 12px 0;
  font-size: 14px;
  font-weight: 600;
  color: var(--el-text-color-regular);
}

.hash-grid {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.hash-item {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.hash-label {
  font-size: 12px;
  font-weight: 600;
  color: var(--el-text-color-secondary);
}

.hash-value-row {
  display: flex;
  gap: 8px;
  align-items: center;
}

.hash-value {
  flex: 1;
  padding: 8px 12px;
  background: var(--el-bg-color);
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  font-family: 'Monaco', 'Menlo', 'Consolas', monospace;
  font-size: 12px;
  color: var(--el-text-color-primary);
  word-break: break-all;
  line-height: 1.4;
}

.copy-btn {
  padding: 6px 10px;
  border: 1px solid var(--el-border-color);
  background: var(--el-bg-color);
  color: var(--el-text-color-secondary);
  border-radius: 6px;
  cursor: pointer;
  font-size: 13px;
  transition: all 0.2s;
}

.copy-btn:hover {
  background: var(--el-fill-color);
  color: var(--el-text-color-regular);
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

.clear-btn {
  padding: 4px 10px;
  border: none;
  background: transparent;
  color: var(--el-text-color-secondary);
  font-size: 13px;
  cursor: pointer;
  border-radius: 4px;
  transition: all 0.2s;
}

.clear-btn:hover {
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
