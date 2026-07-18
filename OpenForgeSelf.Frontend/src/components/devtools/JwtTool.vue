<script setup lang="ts">
import { ref, computed } from 'vue'
import { devToolsApi } from '@/services/devToolsApi'
import type { JwtDecodeResult, JwtValidateResult, JwtGenerateResult } from '@/types/devTools'

const activeSection = ref<'decode' | 'validate' | 'generate'>('decode')

const jwtToken = ref('')
const decodeResult = ref<JwtDecodeResult | null>(null)
const isDecoding = ref(false)
const decodeError = ref('')

const validateSecret = ref('')
const validateAlgorithm = ref('HS256')
const validateResult = ref<JwtValidateResult | null>(null)
const isValidating = ref(false)

const generatePayload = ref(JSON.stringify({
  sub: '1234567890',
  name: 'Test User',
  iat: Math.floor(Date.now() / 1000)
}, null, 2))
const generateSecret = ref('your-secret-key')
const generateAlgorithm = ref('HS256')
const generateExpiresIn = ref(60)
const generateResult = ref<JwtGenerateResult | null>(null)
const isGenerating = ref(false)
const generateError = ref('')

const headerCollapsed = ref(false)
const payloadCollapsed = ref(false)

const jwtAlgorithms = ['HS256', 'HS384', 'HS512']

const formattedHeader = computed(() => {
  if (!decodeResult.value?.header) return ''
  return JSON.stringify(decodeResult.value.header, null, 2)
})

const formattedPayload = computed(() => {
  if (!decodeResult.value?.payload) return ''
  return JSON.stringify(decodeResult.value.payload, null, 2)
})

async function handleDecode(): Promise<void> {
  if (!jwtToken.value.trim()) {
    decodeError.value = '请输入JWT Token'
    return
  }
  try {
    isDecoding.value = true
    decodeError.value = ''
    decodeResult.value = await devToolsApi.decodeJwt(jwtToken.value.trim())
  } catch (e) {
    decodeError.value = e instanceof Error ? e.message : '解析失败'
  } finally {
    isDecoding.value = false
  }
}

async function handleValidate(): Promise<void> {
  if (!jwtToken.value.trim()) {
    decodeError.value = '请输入JWT Token'
    return
  }
  if (!validateSecret.value.trim()) {
    decodeError.value = '请输入密钥'
    return
  }
  try {
    isValidating.value = true
    decodeError.value = ''
    validateResult.value = await devToolsApi.validateJwt(
      jwtToken.value.trim(),
      validateSecret.value.trim(),
      validateAlgorithm.value
    )
  } catch (e) {
    decodeError.value = e instanceof Error ? e.message : '验证失败'
  } finally {
    isValidating.value = false
  }
}

async function handleGenerate(): Promise<void> {
  if (!generateSecret.value.trim()) {
    generateError.value = '请输入密钥'
    return
  }
  try {
    const payload = JSON.parse(generatePayload.value)
    isGenerating.value = true
    generateError.value = ''
    generateResult.value = await devToolsApi.generateJwt(
      payload,
      generateSecret.value.trim(),
      generateAlgorithm.value,
      generateExpiresIn.value || undefined
    )
  } catch (e) {
    if (e instanceof SyntaxError) {
      generateError.value = 'Payload JSON格式错误'
    } else {
      generateError.value = e instanceof Error ? e.message : '生成失败'
    }
  } finally {
    isGenerating.value = false
  }
}

function copyToClipboard(text: string): void {
  navigator.clipboard.writeText(text).catch(() => {})
}

function useGeneratedToken(): void {
  if (generateResult.value?.token) {
    jwtToken.value = generateResult.value.token
    decodeResult.value = null
    validateResult.value = null
    activeSection.value = 'decode'
  }
}
</script>

<template>
  <div class="jwt-tool">
    <div class="tool-sections">
      <div class="section-tabs">
        <button
          class="section-tab"
          :class="{ active: activeSection === 'decode' }"
          @click="activeSection = 'decode'"
        >
          <i class="fa-solid fa-eye" />
          解析
        </button>
        <button
          class="section-tab"
          :class="{ active: activeSection === 'validate' }"
          @click="activeSection = 'validate'"
        >
          <i class="fa-solid fa-check" />
          验证签名
        </button>
        <button
          class="section-tab"
          :class="{ active: activeSection === 'generate' }"
          @click="activeSection = 'generate'"
        >
          <i class="fa-solid fa-plus" />
          生成
        </button>
      </div>

      <div v-if="decodeError" class="error-message">
        <i class="fa-solid fa-circle-exclamation" />
        {{ decodeError }}
      </div>

      <div v-if="activeSection === 'decode'" class="section-content">
        <div class="input-group">
          <label class="input-label">JWT Token</label>
          <textarea
            v-model="jwtToken"
            class="token-input"
            placeholder="粘贴你的JWT Token..."
            rows="4"
          />
        </div>
        <button class="primary-btn" :disabled="isDecoding" @click="handleDecode">
          <i v-if="isDecoding" class="fa-solid fa-spinner fa-spin" />
          <i v-else class="fa-solid fa-magnifying-glass" />
          {{ isDecoding ? '解析中...' : '解析JWT' }}
        </button>

        <div v-if="decodeResult && decodeResult.success" class="decode-result">
          <div class="result-section">
            <div class="section-header" @click="headerCollapsed = !headerCollapsed">
              <i class="fa-solid fa-chevron-down" :class="{ rotated: headerCollapsed }" />
              <span class="section-title">Header</span>
            </div>
            <div v-show="!headerCollapsed" class="section-body">
              <pre class="json-display">{{ formattedHeader }}</pre>
            </div>
          </div>

          <div class="result-section">
            <div class="section-header" @click="payloadCollapsed = !payloadCollapsed">
              <i class="fa-solid fa-chevron-down" :class="{ rotated: payloadCollapsed }" />
              <span class="section-title">Payload</span>
            </div>
            <div v-show="!payloadCollapsed" class="section-body">
              <pre class="json-display">{{ formattedPayload }}</pre>
            </div>
          </div>

          <div class="result-section">
            <div class="section-header">
              <span class="section-title">签名</span>
            </div>
            <div class="section-body">
              <div class="signature-display">{{ decodeResult.signature }}</div>
            </div>
          </div>

          <div class="expiry-info">
            <div
              class="expiry-status"
              :class="{
                'status-valid': !decodeResult.isExpired && decodeResult.timeRemaining,
                'status-expired': decodeResult.isExpired,
                'status-warning': !decodeResult.isExpired && decodeResult.timeRemaining?.includes('分钟')
              }"
            >
              <i
                class="fa-solid"
                :class="{
                  'fa-check-circle': !decodeResult.isExpired,
                  'fa-circle-exclamation': decodeResult.isExpired,
                  'fa-triangle-exclamation': !decodeResult.isExpired && decodeResult.timeRemaining?.includes('分钟')
                }"
              />
              <span v-if="decodeResult.isExpired">已过期</span>
              <span v-else>有效</span>
            </div>
            <div class="expiry-details">
              <div class="detail-item">
                <span class="detail-label">签发时间</span>
                <span class="detail-value">{{ decodeResult.issuedAt || '-' }}</span>
              </div>
              <div class="detail-item">
                <span class="detail-label">过期时间</span>
                <span class="detail-value">{{ decodeResult.expiration || '永不过期' }}</span>
              </div>
              <div class="detail-item">
                <span class="detail-label">剩余时间</span>
                <span class="detail-value">{{ decodeResult.timeRemaining || '-' }}</span>
              </div>
            </div>
          </div>
        </div>

        <div v-else-if="decodeResult && !decodeResult.success" class="error-message">
          <i class="fa-solid fa-circle-exclamation" />
          {{ decodeResult.errorMessage }}
        </div>
      </div>

      <div v-if="activeSection === 'validate'" class="section-content">
        <div class="input-group">
          <label class="input-label">JWT Token</label>
          <textarea
            v-model="jwtToken"
            class="token-input"
            placeholder="粘贴你的JWT Token..."
            rows="3"
          />
        </div>
        <div class="input-group">
          <label class="input-label">密钥</label>
          <input
            v-model="validateSecret"
            type="text"
            class="text-input"
            placeholder="输入签名密钥..."
          />
        </div>
        <div class="input-group">
          <label class="input-label">算法</label>
          <select v-model="validateAlgorithm" class="select-input">
            <option v-for="algo in jwtAlgorithms" :key="algo" :value="algo">{{ algo }}</option>
          </select>
        </div>
        <button class="primary-btn" :disabled="isValidating" @click="handleValidate">
          <i v-if="isValidating" class="fa-solid fa-spinner fa-spin" />
          <i v-else class="fa-solid fa-shield-halved" />
          {{ isValidating ? '验证中...' : '验证签名' }}
        </button>

        <div
          v-if="validateResult"
          class="validate-result"
          :class="{
            'valid': validateResult.isValid,
            'invalid': !validateResult.isValid
          }"
        >
          <i
            class="fa-solid"
            :class="{
              'fa-circle-check': validateResult.isValid,
              'fa-circle-xmark': !validateResult.isValid
            }"
          />
          <span>{{ validateResult.isValid ? '签名验证通过' : '签名验证失败' }}</span>
          <p v-if="validateResult.errorMessage" class="error-detail">{{ validateResult.errorMessage }}</p>
        </div>
      </div>

      <div v-if="activeSection === 'generate'" class="section-content">
        <div v-if="generateError" class="error-message">
          <i class="fa-solid fa-circle-exclamation" />
          {{ generateError }}
        </div>

        <div class="input-group">
          <label class="input-label">Payload (JSON)</label>
          <textarea
            v-model="generatePayload"
            class="payload-input"
            placeholder="{&quot;sub&quot;: &quot;123&quot;, &quot;name&quot;: &quot;User&quot;}"
            rows="6"
          />
        </div>
        <div class="input-row">
          <div class="input-group">
            <label class="input-label">密钥</label>
            <input
              v-model="generateSecret"
              type="text"
              class="text-input"
              placeholder="输入签名密钥..."
            />
          </div>
          <div class="input-group">
            <label class="input-label">算法</label>
            <select v-model="generateAlgorithm" class="select-input">
              <option v-for="algo in jwtAlgorithms" :key="algo" :value="algo">{{ algo }}</option>
            </select>
          </div>
        </div>
        <div class="input-group">
          <label class="input-label">过期时间（分钟，0表示永不过期）</label>
          <input
            v-model.number="generateExpiresIn"
            type="number"
            class="text-input"
            min="0"
          />
        </div>
        <button class="primary-btn" :disabled="isGenerating" @click="handleGenerate">
          <i v-if="isGenerating" class="fa-solid fa-spinner fa-spin" />
          <i v-else class="fa-solid fa-wand-magic-sparkles" />
          {{ isGenerating ? '生成中...' : '生成JWT' }}
        </button>

        <div v-if="generateResult" class="generate-result">
          <div class="result-header">
            <span class="result-title">生成的Token</span>
            <div class="result-actions">
              <button class="icon-btn" title="复制" @click="copyToClipboard(generateResult.token)">
                <i class="fa-solid fa-copy" />
              </button>
              <button class="icon-btn" title="用于解析" @click="useGeneratedToken">
                <i class="fa-solid fa-arrow-right" />
              </button>
            </div>
          </div>
          <pre class="token-display">{{ generateResult.token }}</pre>
          <div class="gen-info">
            <span>签发: {{ generateResult.issuedAt }}</span>
            <span v-if="generateResult.expiration">过期: {{ generateResult.expiration }}</span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.jwt-tool {
  height: 100%;
  overflow-y: auto;
  padding: 16px;
  box-sizing: border-box;
}

.tool-sections {
  display: flex;
  flex-direction: column;
  gap: 16px;
  max-width: 900px;
  margin: 0 auto;
}

.section-tabs {
  display: flex;
  gap: 8px;
  background: #f8f9fa;
  padding: 4px;
  border-radius: 8px;
}

.section-tab {
  flex: 1;
  padding: 10px 16px;
  border: none;
  background: transparent;
  color: #6c757d;
  font-size: 14px;
  font-weight: 500;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
}

.section-tab:hover {
  background: #e9ecef;
  color: #495057;
}

.section-tab.active {
  background: #0d6efd;
  color: white;
}

.section-content {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.input-group {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.input-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}

.input-label {
  font-size: 13px;
  font-weight: 600;
  color: #495057;
}

.token-input,
.payload-input {
  width: 100%;
  padding: 10px 12px;
  border: 1px solid #dee2e6;
  border-radius: 6px;
  font-size: 13px;
  font-family: 'Consolas', 'Monaco', monospace;
  resize: vertical;
  box-sizing: border-box;
  transition: border-color 0.2s;
}

.token-input:focus,
.payload-input:focus {
  outline: none;
  border-color: #0d6efd;
  box-shadow: 0 0 0 3px rgba(13, 110, 253, 0.1);
}

.text-input,
.select-input {
  width: 100%;
  padding: 8px 12px;
  border: 1px solid #dee2e6;
  border-radius: 6px;
  font-size: 14px;
  box-sizing: border-box;
  transition: border-color 0.2s;
}

.text-input:focus,
.select-input:focus {
  outline: none;
  border-color: #0d6efd;
  box-shadow: 0 0 0 3px rgba(13, 110, 253, 0.1);
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
}

.decode-result {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.result-section {
  background: white;
  border: 1px solid #dee2e6;
  border-radius: 8px;
  overflow: hidden;
}

.section-header {
  padding: 10px 14px;
  background: #f8f9fa;
  border-bottom: 1px solid #dee2e6;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 8px;
  user-select: none;
}

.section-header .fa-chevron-down {
  transition: transform 0.2s;
  font-size: 12px;
  color: #6c757d;
}

.section-header .fa-chevron-down.rotated {
  transform: rotate(-90deg);
}

.section-title {
  font-size: 13px;
  font-weight: 600;
  color: #495057;
}

.section-body {
  padding: 12px 14px;
}

.json-display {
  margin: 0;
  padding: 0;
  font-family: 'Consolas', 'Monaco', monospace;
  font-size: 12px;
  line-height: 1.5;
  color: #212529;
  white-space: pre-wrap;
  word-break: break-all;
  max-height: 200px;
  overflow-y: auto;
}

.signature-display {
  font-family: 'Consolas', 'Monaco', monospace;
  font-size: 12px;
  color: #6c757d;
  word-break: break-all;
}

.expiry-info {
  background: white;
  border: 1px solid #dee2e6;
  border-radius: 8px;
  padding: 16px;
}

.expiry-status {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 16px;
  font-weight: 600;
  margin-bottom: 12px;
  padding-bottom: 12px;
  border-bottom: 1px solid #e9ecef;
}

.expiry-status.status-valid {
  color: #16a34a;
}

.expiry-status.status-expired {
  color: #dc2626;
}

.expiry-status.status-warning {
  color: #d97706;
}

.expiry-details {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.detail-item {
  display: flex;
  justify-content: space-between;
  font-size: 13px;
}

.detail-label {
  color: #6c757d;
}

.detail-value {
  color: #212529;
  font-weight: 500;
}

.validate-result {
  padding: 20px;
  border-radius: 8px;
  text-align: center;
  font-size: 16px;
  font-weight: 600;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
}

.validate-result.valid {
  background: #f0fdf4;
  border: 1px solid #86efac;
  color: #16a34a;
}

.validate-result.invalid {
  background: #fef2f2;
  border: 1px solid #fecaca;
  color: #dc2626;
}

.error-detail {
  font-size: 13px;
  font-weight: 400;
  margin: 4px 0 0 0;
}

.generate-result {
  background: white;
  border: 1px solid #dee2e6;
  border-radius: 8px;
  overflow: hidden;
}

.result-header {
  padding: 10px 14px;
  background: #f8f9fa;
  border-bottom: 1px solid #dee2e6;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.result-title {
  font-size: 13px;
  font-weight: 600;
  color: #495057;
}

.result-actions {
  display: flex;
  gap: 4px;
}

.icon-btn {
  padding: 6px 8px;
  background: transparent;
  border: none;
  color: #6c757d;
  cursor: pointer;
  border-radius: 4px;
  transition: all 0.2s;
}

.icon-btn:hover {
  background: #e9ecef;
  color: #495057;
}

.token-display {
  margin: 0;
  padding: 14px;
  font-family: 'Consolas', 'Monaco', monospace;
  font-size: 12px;
  line-height: 1.5;
  color: #212529;
  white-space: pre-wrap;
  word-break: break-all;
  max-height: 150px;
  overflow-y: auto;
}

.gen-info {
  padding: 10px 14px;
  background: #f8f9fa;
  border-top: 1px solid #dee2e6;
  display: flex;
  gap: 16px;
  font-size: 12px;
  color: #6c757d;
}
</style>
