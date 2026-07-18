<script setup lang="ts">
import { useDevToolsStore } from '@/stores/devTools'

const store = useDevToolsStore()
</script>

<template>
  <div class="encrypt-tool">
    <div class="toolbar">
      <div class="mode-selector">
        <button
          class="mode-btn"
          :class="{ active: store.encryptMode === 'hmac' }"
          @click="store.setEncryptMode('hmac')"
        >
          HMAC
        </button>
        <button
          class="mode-btn"
          :class="{ active: store.encryptMode === 'aes' }"
          @click="store.setEncryptMode('aes')"
        >
          AES
        </button>
      </div>
      <div class="action-group">
        <button class="tool-btn primary" :disabled="store.isProcessing" @click="store.encrypt()">
          <i class="fa-solid fa-lock" />
          加密
        </button>
        <button class="tool-btn" :disabled="store.isProcessing" @click="store.decrypt()">
          <i class="fa-solid fa-unlock" />
          解密
        </button>
      </div>
    </div>

    <div class="options-section">
      <div v-if="store.encryptMode === 'hmac'" class="option-row">
        <label class="option-label">HMAC 算法</label>
        <select v-model="store.hmacAlgorithm" class="option-select">
          <option value="md5">HMAC-MD5</option>
          <option value="sha1">HMAC-SHA1</option>
          <option value="sha256">HMAC-SHA256</option>
          <option value="sha512">HMAC-SHA512</option>
        </select>
      </div>
      <div class="option-row">
        <label class="option-label">{{ store.encryptMode === 'hmac' ? '密钥' : '密钥 (Key)' }}</label>
        <input
          v-model="store.encryptKey"
          type="text"
          class="option-input"
          :placeholder="store.encryptMode === 'hmac' ? '输入 HMAC 密钥' : '输入 AES 密钥'"
        />
      </div>
      <div v-if="store.encryptMode === 'aes'" class="option-row">
        <label class="option-label">初始向量 (IV)</label>
        <input
          v-model="store.encryptIv"
          type="text"
          class="option-input"
          placeholder="可选，留空则自动生成"
        />
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
          placeholder="结果将显示在这里..."
          readonly
          spellcheck="false"
        />
      </div>
    </div>
  </div>
</template>

<style scoped>
.encrypt-tool {
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

.mode-selector {
  display: flex;
  gap: 4px;
  padding: 4px;
  background: var(--bg-secondary);
  border-radius: 8px;
}

.mode-btn {
  padding: 8px 16px;
  border: none;
  background: transparent;
  color: var(--text-muted);
  font-size: 14px;
  font-weight: 500;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.mode-btn:hover {
  background: var(--bg-hover);
  color: var(--text-secondary);
}

.mode-btn.active {
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

.options-section {
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding: 14px;
  background: var(--bg-secondary);
  border-radius: 8px;
}

.option-row {
  display: flex;
  align-items: center;
  gap: 12px;
}

.option-label {
  min-width: 120px;
  font-size: 14px;
  font-weight: 500;
  color: var(--text-secondary);
}

.option-input,
.option-select {
  flex: 1;
  padding: 8px 12px;
  border: 1px solid var(--border-color);
  border-radius: 6px;
  font-size: 14px;
  background: var(--bg-card);
}

.option-input:focus,
.option-select:focus {
  outline: none;
  border-color: var(--primary-color);
  box-shadow: 0 0 0 3px var(--primary-light);
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
