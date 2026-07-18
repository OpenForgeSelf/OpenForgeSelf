<script setup lang="ts">
import { ref, computed } from 'vue'
import { devToolsApi } from '@/services/devToolsApi'
import type { UuidGenerateResult, SnowflakeGenerateResult, SnowflakeIdInfo } from '@/types/devTools'

const idType = ref<'uuid-v4' | 'uuid-v1' | 'snowflake'>('uuid-v4')
const count = ref(1)
const uppercase = ref(false)
const withHyphens = ref(true)

const workerId = ref(1)
const datacenterId = ref(1)

const uuidResult = ref<UuidGenerateResult | null>(null)
const snowflakeResult = ref<SnowflakeGenerateResult | null>(null)
const isGenerating = ref(false)
const errorMessage = ref('')

const resultList = computed(() => {
  if (idType.value === 'snowflake' && snowflakeResult.value) {
    return snowflakeResult.value.ids
  }
  if ((idType.value === 'uuid-v4' || idType.value === 'uuid-v1') && uuidResult.value) {
    return uuidResult.value.ids.map(id => ({ id, timestamp: '', workerId: 0, datacenterId: 0, sequence: 0 }))
  }
  return []
})

async function handleGenerate(): Promise<void> {
  if (count.value < 1 || count.value > 100) {
    errorMessage.value = '数量必须在1-100之间'
    return
  }
  try {
    isGenerating.value = true
    errorMessage.value = ''

    if (idType.value === 'snowflake') {
      snowflakeResult.value = await devToolsApi.generateSnowflakeId(
        workerId.value,
        datacenterId.value,
        count.value
      )
      uuidResult.value = null
    } else {
      const version = idType.value === 'uuid-v1' ? 'v1' : 'v4'
      uuidResult.value = await devToolsApi.generateUuid(
        version,
        count.value,
        uppercase.value,
        withHyphens.value
      )
      snowflakeResult.value = null
    }
  } catch (e) {
    errorMessage.value = e instanceof Error ? e.message : '生成失败'
  } finally {
    isGenerating.value = false
  }
}

function copyToClipboard(text: string): void {
  navigator.clipboard.writeText(text).catch(() => {})
}

function copyAll(): void {
  const allText = resultList.value.map(item => item.id).join('\n')
  navigator.clipboard.writeText(allText).catch(() => {})
}

function regenerate(): void {
  handleGenerate()
}
</script>

<template>
  <div class="uuid-tool">
    <div class="tool-container">
      <div class="settings-panel">
        <h3 class="panel-title">
          <i class="fa-solid fa-sliders" />
          设置
        </h3>

        <div class="input-group">
          <label class="input-label">类型</label>
          <div class="type-options">
            <button
              class="type-btn"
              :class="{ active: idType === 'uuid-v4' }"
              @click="idType = 'uuid-v4'"
            >
              <i class="fa-solid fa-dice" />
              UUID v4
            </button>
            <button
              class="type-btn"
              :class="{ active: idType === 'uuid-v1' }"
              @click="idType = 'uuid-v1'"
            >
              <i class="fa-regular fa-clock" />
              UUID v1
            </button>
            <button
              class="type-btn"
              :class="{ active: idType === 'snowflake' }"
              @click="idType = 'snowflake'"
            >
              <i class="fa-regular fa-snowflake" />
              雪花ID
            </button>
          </div>
        </div>

        <div class="input-group">
          <label class="input-label">数量 (1-100)</label>
          <input
            v-model.number="count"
            type="number"
            class="text-input"
            min="1"
            max="100"
          />
        </div>

        <div v-if="idType !== 'snowflake'" class="format-options">
          <div class="input-group">
            <label class="checkbox-label">
              <input v-model="uppercase" type="checkbox" />
              <span>大写</span>
            </label>
          </div>
          <div class="input-group">
            <label class="checkbox-label">
              <input v-model="withHyphens" type="checkbox" />
              <span>带连字符</span>
            </label>
          </div>
        </div>

        <div v-if="idType === 'snowflake'" class="snowflake-options">
          <div class="input-group">
            <label class="input-label">工作ID (0-31)</label>
            <input
              v-model.number="workerId"
              type="number"
              class="text-input"
              min="0"
              max="31"
            />
          </div>
          <div class="input-group">
            <label class="input-label">数据中心ID (0-31)</label>
            <input
              v-model.number="datacenterId"
              type="number"
              class="text-input"
              min="0"
              max="31"
            />
          </div>
        </div>

        <button class="primary-btn generate-btn" :disabled="isGenerating" @click="handleGenerate">
          <i v-if="isGenerating" class="fa-solid fa-spinner fa-spin" />
          <i v-else class="fa-solid fa-wand-magic-sparkles" />
          {{ isGenerating ? '生成中...' : '生成' }}
        </button>
      </div>

      <div class="result-panel">
        <div class="result-header">
          <h3 class="panel-title">
            <i class="fa-solid fa-list-ul" />
            结果
            <span v-if="resultList.length" class="result-count">({{ resultList.length }})</span>
          </h3>
          <div v-if="resultList.length" class="result-actions">
            <button class="action-btn" title="全部复制" @click="copyAll">
              <i class="fa-solid fa-copy" />
              全部复制
            </button>
            <button class="action-btn" title="重新生成" @click="regenerate">
              <i class="fa-solid fa-rotate-right" />
              重新生成
            </button>
          </div>
        </div>

        <div v-if="errorMessage" class="error-message">
          <i class="fa-solid fa-circle-exclamation" />
          {{ errorMessage }}
        </div>

        <div v-if="!resultList.length && !errorMessage" class="empty-state">
          <i class="fa-solid fa-fingerprint empty-icon" />
          <p>点击"生成"按钮开始</p>
        </div>

        <div v-else class="result-list">
          <div
            v-for="(item, index) in resultList"
            :key="index"
            class="result-item"
          >
            <div class="item-main">
              <span class="item-index">{{ index + 1 }}.</span>
              <span class="item-id">{{ item.id }}</span>
              <button
                class="copy-icon-btn"
                title="复制"
                @click="copyToClipboard(item.id)"
              >
                <i class="fa-regular fa-copy" />
              </button>
            </div>
            <div v-if="idType === 'snowflake'" class="item-details">
              <span class="detail-tag">
                <i class="fa-regular fa-clock" />
                {{ (item as SnowflakeIdInfo).timestamp }}
              </span>
              <span class="detail-tag">
                <i class="fa-solid fa-hashtag" />
                Worker: {{ (item as SnowflakeIdInfo).workerId }}
              </span>
              <span class="detail-tag">
                <i class="fa-solid fa-building" />
                DC: {{ (item as SnowflakeIdInfo).datacenterId }}
              </span>
              <span class="detail-tag">
                <i class="fa-solid fa-list-ol" />
                Seq: {{ (item as SnowflakeIdInfo).sequence }}
              </span>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.uuid-tool {
  height: 100%;
  overflow-y: auto;
  padding: 16px;
  box-sizing: border-box;
}

.tool-container {
  display: grid;
  grid-template-columns: 300px 1fr;
  gap: 20px;
  max-width: 1100px;
  margin: 0 auto;
  height: calc(100% - 20px);
}

.settings-panel,
.result-panel {
  background: white;
  border: 1px solid #dee2e6;
  border-radius: 8px;
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 16px;
  overflow: hidden;
}

.result-panel {
  min-height: 0;
}

.panel-title {
  font-size: 14px;
  font-weight: 600;
  color: #495057;
  margin: 0;
  display: flex;
  align-items: center;
  gap: 8px;
}

.result-count {
  color: #6c757d;
  font-weight: 400;
  font-size: 13px;
}

.input-group {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.input-label {
  font-size: 13px;
  font-weight: 500;
  color: #6c757d;
}

.type-options {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.type-btn {
  padding: 10px 14px;
  border: 1px solid #dee2e6;
  background: white;
  color: #495057;
  border-radius: 6px;
  font-size: 13px;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  gap: 8px;
  text-align: left;
}

.type-btn:hover {
  border-color: #0d6efd;
  color: #0d6efd;
}

.type-btn.active {
  background: #0d6efd;
  border-color: #0d6efd;
  color: white;
}

.text-input {
  width: 100%;
  padding: 8px 12px;
  border: 1px solid #dee2e6;
  border-radius: 6px;
  font-size: 14px;
  box-sizing: border-box;
  transition: border-color 0.2s;
}

.text-input:focus {
  outline: none;
  border-color: #0d6efd;
  box-shadow: 0 0 0 3px rgba(13, 110, 253, 0.1);
}

.format-options,
.snowflake-options {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.checkbox-label {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  color: #495057;
  cursor: pointer;
}

.checkbox-label input[type="checkbox"] {
  width: 16px;
  height: 16px;
  cursor: pointer;
}

.primary-btn {
  padding: 10px 20px;
  background: #0d6efd;
  color: white;
  border: none;
  border-radius: 6px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: background 0.2s;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
}

.primary-btn:hover {
  background: #0b5ed7;
}

.primary-btn:disabled {
  background: #6c757d;
  cursor: not-allowed;
}

.generate-btn {
  margin-top: auto;
}

.result-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-shrink: 0;
}

.result-actions {
  display: flex;
  gap: 8px;
}

.action-btn {
  padding: 6px 12px;
  background: #f8f9fa;
  color: #495057;
  border: 1px solid #dee2e6;
  border-radius: 4px;
  font-size: 12px;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  gap: 4px;
}

.action-btn:hover {
  background: #e9ecef;
  border-color: #ced4da;
}

.error-message {
  padding: 12px 16px;
  background: #fef2f2;
  border: 1px solid #fecaca;
  border-radius: 6px;
  color: #dc2626;
  font-size: 14px;
  display: flex;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
}

.empty-state {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  color: #adb5bd;
  gap: 12px;
}

.empty-icon {
  font-size: 48px;
  opacity: 0.5;
}

.empty-state p {
  margin: 0;
  font-size: 14px;
}

.result-list {
  flex: 1;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 8px;
  min-height: 0;
}

.result-item {
  padding: 10px 12px;
  background: #f8f9fa;
  border: 1px solid #e9ecef;
  border-radius: 6px;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.item-main {
  display: flex;
  align-items: center;
  gap: 8px;
}

.item-index {
  font-size: 12px;
  color: #adb5bd;
  font-weight: 600;
  min-width: 24px;
}

.item-id {
  flex: 1;
  font-family: 'Consolas', 'Monaco', monospace;
  font-size: 13px;
  color: #212529;
  word-break: break-all;
}

.copy-icon-btn {
  padding: 4px 8px;
  background: transparent;
  border: none;
  color: #adb5bd;
  cursor: pointer;
  border-radius: 4px;
  transition: all 0.2s;
}

.copy-icon-btn:hover {
  background: #e9ecef;
  color: #0d6efd;
}

.item-details {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  padding-left: 32px;
}

.detail-tag {
  padding: 2px 8px;
  background: #e9ecef;
  border-radius: 4px;
  font-size: 11px;
  color: #6c757d;
  display: flex;
  align-items: center;
  gap: 4px;
}
</style>
