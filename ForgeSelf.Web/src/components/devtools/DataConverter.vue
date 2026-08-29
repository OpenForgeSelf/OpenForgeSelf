<script setup lang="ts">
import { useDevToolsStore } from '@/stores/devTools'

const store = useDevToolsStore()
</script>

<template>
  <div class="data-converter">
    <div class="toolbar">
      <div class="converter-switch">
        <button
          class="switch-btn"
          :class="{ active: store.converterDirection === 'json-to-yaml' }"
          @click="store.setConverterDirection('json-to-yaml')"
        >
          JSON → YAML
        </button>
        <button
          class="switch-btn"
          :class="{ active: store.converterDirection === 'yaml-to-json' }"
          @click="store.setConverterDirection('yaml-to-json')"
        >
          YAML → JSON
        </button>
      </div>
      <button class="tool-btn primary" :disabled="store.isProcessing" @click="store.convertJsonToYaml()">
        <i class="fa-solid fa-right-left" />
        转换
      </button>
    </div>

    <div class="editors">
      <div class="editor-panel">
        <div class="editor-header">
          <span class="editor-title">
            {{ store.converterDirection === 'json-to-yaml' ? 'JSON' : 'YAML' }}
          </span>
          <button class="clear-btn" @click="store.clearInput()">清空</button>
        </div>
        <textarea
          v-model="store.inputText"
          class="editor-textarea"
          :placeholder="`在此输入 ${store.converterDirection === 'json-to-yaml' ? 'JSON' : 'YAML'} 文本...`"
          spellcheck="false"
        />
      </div>

      <div class="editor-panel">
        <div class="editor-header">
          <span class="editor-title">
            {{ store.converterDirection === 'json-to-yaml' ? 'YAML' : 'JSON' }}
          </span>
          <button class="swap-btn" title="交换" @click="store.swapInputOutput()">
            <i class="fa-solid fa-right-left" />
          </button>
        </div>
        <textarea
          v-model="store.outputText"
          class="editor-textarea output"
          placeholder="转换结果将显示在这里..."
          readonly
          spellcheck="false"
        />
      </div>
    </div>
  </div>
</template>

<style scoped>
.data-converter {
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

.converter-switch {
  display: flex;
  gap: 4px;
  padding: 4px;
  background: var(--el-bg-color-page);
  border-radius: 8px;
}

.switch-btn {
  padding: 8px 16px;
  border: none;
  background: transparent;
  color: var(--el-text-color-secondary);
  font-size: 14px;
  font-weight: 500;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.switch-btn:hover {
  background: var(--el-fill-color);
  color: var(--el-text-color-regular);
}

.switch-btn.active {
  background: var(--el-color-primary);
  color: var(--el-color-white);
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
