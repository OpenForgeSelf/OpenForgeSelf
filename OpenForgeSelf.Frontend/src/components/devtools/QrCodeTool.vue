<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { devToolsApi } from '@/services/devToolsApi'
import type { QrCodeGenerateResult } from '@/types/devTools'

const contentTypes = [
  { value: 'text', label: '文本', icon: 'fa-solid fa-font' },
  { value: 'url', label: 'URL', icon: 'fa-solid fa-link' },
  { value: 'wifi', label: 'WiFi', icon: 'fa-solid fa-wifi' },
  { value: 'vcard', label: '名片', icon: 'fa-solid fa-address-card' },
  { value: 'sms', label: '短信', icon: 'fa-solid fa-comment-sms' },
  { value: 'email', label: '邮箱', icon: 'fa-solid fa-envelope' },
]

const errorLevels = [
  { value: 'L', label: '低 (7%)' },
  { value: 'M', label: '中 (15%)' },
  { value: 'Q', label: '较高 (25%)' },
  { value: 'H', label: '高 (30%)' },
]

const activeTab = ref<'generate' | 'decode'>('generate')
const contentType = ref('text')
const textContent = ref('Hello, World!')
const urlContent = ref('https://example.com')

const wifiSsid = ref('')
const wifiPassword = ref('')
const wifiEncryption = ref('WPA')

const vcardName = ref('')
const vcardPhone = ref('')
const vcardEmail = ref('')
const vcardOrg = ref('')

const smsPhone = ref('')
const smsBody = ref('')

const emailTo = ref('')
const emailSubject = ref('')
const emailBody = ref('')

const size = ref(256)
const errorLevel = ref('M')
const margin = ref(4)
const foregroundColor = ref('#000000')
const backgroundColor = ref('#FFFFFF')

const qrResult = ref<QrCodeGenerateResult | null>(null)
const isGenerating = ref(false)
const errorMessage = ref('')

const actualContent = computed(() => {
  switch (contentType.value) {
    case 'url':
      return urlContent.value
    case 'wifi':
      return `WIFI:T:${wifiEncryption.value};S:${wifiSsid.value};P:${wifiPassword.value};;`
    case 'vcard':
      return `BEGIN:VCARD\nVERSION:3.0\nN:${vcardName.value}\nTEL:${vcardPhone.value}\nEMAIL:${vcardEmail.value}\nORG:${vcardOrg.value}\nEND:VCARD`
    case 'sms':
      return `SMSTO:${smsPhone.value}:${smsBody.value}`
    case 'email':
      return `mailto:${emailTo.value}?subject=${encodeURIComponent(emailSubject.value)}&body=${encodeURIComponent(emailBody.value)}`
    default:
      return textContent.value
  }
})

async function generateQrCode(): Promise<void> {
  const content = actualContent.value
  if (!content.trim()) {
    errorMessage.value = '请输入内容'
    return
  }
  try {
    isGenerating.value = true
    errorMessage.value = ''
    qrResult.value = await devToolsApi.generateCustomQrCode(
      content.trim(),
      size.value,
      errorLevel.value,
      margin.value,
      foregroundColor.value,
      backgroundColor.value
    )
  } catch (e) {
    errorMessage.value = e instanceof Error ? e.message : '生成失败'
  } finally {
    isGenerating.value = false
  }
}

function downloadQrCode(): void {
  if (!qrResult.value?.imageBase64) return
  const link = document.createElement('a')
  link.download = `qrcode_${Date.now()}.png`
  link.href = `data:image/png;base64,${qrResult.value.imageBase64}`
  link.click()
}

async function copyQrImage(): Promise<void> {
  if (!qrResult.value?.imageBase64) return
  try {
    const response = await fetch(`data:image/png;base64,${qrResult.value.imageBase64}`)
    const blob = await response.blob()
    await navigator.clipboard.write([
      new ClipboardItem({ 'image/png': blob })
    ])
  } catch {
    // ignore
  }
}

watch(
  [textContent, urlContent, wifiSsid, wifiPassword, wifiEncryption, vcardName, vcardPhone, vcardEmail, vcardOrg,
   smsPhone, smsBody, emailTo, emailSubject, emailBody, contentType, size, errorLevel, margin, foregroundColor, backgroundColor],
  () => {
    generateQrCode()
  },
  { deep: true }
)

onMounted(() => {
  generateQrCode()
})
</script>

<template>
  <div class="qrcode-tool">
    <div class="tool-container">
      <div class="settings-panel">
        <div class="panel-tabs">
          <button
            class="panel-tab"
            :class="{ active: activeTab === 'generate' }"
            @click="activeTab = 'generate'"
          >
            <i class="fa-solid fa-qrcode" />
            生成二维码
          </button>
          <button
            class="panel-tab"
            :class="{ active: activeTab === 'decode' }"
            @click="activeTab = 'decode'"
          >
            <i class="fa-solid fa-barcode-read" />
            解析二维码
          </button>
        </div>

        <div v-if="activeTab === 'generate'" class="settings-content">
          <div class="input-group">
            <label class="input-label">内容类型</label>
            <div class="type-grid">
              <button
                v-for="type in contentTypes"
                :key="type.value"
                class="type-btn"
                :class="{ active: contentType === type.value }"
                @click="contentType = type.value"
              >
                <i :class="type.icon" />
                <span>{{ type.label }}</span>
              </button>
            </div>
          </div>

          <div v-if="contentType === 'text'" class="input-group">
            <label class="input-label">文本内容</label>
            <textarea
              v-model="textContent"
              class="text-input"
              rows="3"
              placeholder="输入文本内容..."
            />
          </div>

          <div v-if="contentType === 'url'" class="input-group">
            <label class="input-label">URL地址</label>
            <input
              v-model="urlContent"
              type="url"
              class="text-input"
              placeholder="https://example.com"
            />
          </div>

          <div v-if="contentType === 'wifi'" class="wifi-fields">
            <div class="input-group">
              <label class="input-label">WiFi名称 (SSID)</label>
              <input
                v-model="wifiSsid"
                type="text"
                class="text-input"
                placeholder="MyWiFi"
              />
            </div>
            <div class="input-group">
              <label class="input-label">密码</label>
              <input
                v-model="wifiPassword"
                type="text"
                class="text-input"
                placeholder="password123"
              />
            </div>
            <div class="input-group">
              <label class="input-label">加密方式</label>
              <select v-model="wifiEncryption" class="select-input">
                <option value="WPA">WPA/WPA2</option>
                <option value="WEP">WEP</option>
                <option value="nopass">无密码</option>
              </select>
            </div>
          </div>

          <div v-if="contentType === 'vcard'" class="vcard-fields">
            <div class="input-group">
              <label class="input-label">姓名</label>
              <input v-model="vcardName" type="text" class="text-input" placeholder="张三" />
            </div>
            <div class="input-group">
              <label class="input-label">电话</label>
              <input v-model="vcardPhone" type="tel" class="text-input" placeholder="13800138000" />
            </div>
            <div class="input-group">
              <label class="input-label">邮箱</label>
              <input v-model="vcardEmail" type="email" class="text-input" placeholder="name@example.com" />
            </div>
            <div class="input-group">
              <label class="input-label">公司</label>
              <input v-model="vcardOrg" type="text" class="text-input" placeholder="公司名称" />
            </div>
          </div>

          <div v-if="contentType === 'sms'" class="sms-fields">
            <div class="input-group">
              <label class="input-label">手机号</label>
              <input v-model="smsPhone" type="tel" class="text-input" placeholder="13800138000" />
            </div>
            <div class="input-group">
              <label class="input-label">短信内容</label>
              <textarea
                v-model="smsBody"
                class="text-input"
                rows="2"
                placeholder="输入短信内容..."
              />
            </div>
          </div>

          <div v-if="contentType === 'email'" class="email-fields">
            <div class="input-group">
              <label class="input-label">收件人</label>
              <input v-model="emailTo" type="email" class="text-input" placeholder="name@example.com" />
            </div>
            <div class="input-group">
              <label class="input-label">主题</label>
              <input v-model="emailSubject" type="text" class="text-input" placeholder="邮件主题" />
            </div>
            <div class="input-group">
              <label class="input-label">正文</label>
              <textarea
                v-model="emailBody"
                class="text-input"
                rows="2"
                placeholder="输入邮件正文..."
              />
            </div>
          </div>

          <div class="divider" />

          <div class="advanced-settings">
            <h4 class="settings-title">
              <i class="fa-solid fa-gear" />
              高级设置
            </h4>

            <div class="input-group">
              <label class="input-label">尺寸: {{ size }}px</label>
              <input
                v-model.number="size"
                type="range"
                min="128"
                max="512"
                step="32"
                class="range-input"
              />
            </div>

            <div class="input-group">
              <label class="input-label">容错级别</label>
              <div class="level-options">
                <button
                  v-for="level in errorLevels"
                  :key="level.value"
                  class="level-btn"
                  :class="{ active: errorLevel === level.value }"
                  @click="errorLevel = level.value"
                >
                  {{ level.value }}
                </button>
              </div>
            </div>

            <div class="input-group">
              <label class="input-label">边距: {{ margin }}</label>
              <input
                v-model.number="margin"
                type="range"
                min="0"
                max="8"
                step="1"
                class="range-input"
              />
            </div>

            <div class="color-row">
              <div class="input-group">
                <label class="input-label">前景色</label>
                <div class="color-input-wrap">
                  <input v-model="foregroundColor" type="color" class="color-input" />
                  <input v-model="foregroundColor" type="text" class="color-text" />
                </div>
              </div>
              <div class="input-group">
                <label class="input-label">背景色</label>
                <div class="color-input-wrap">
                  <input v-model="backgroundColor" type="color" class="color-input" />
                  <input v-model="backgroundColor" type="text" class="color-text" />
                </div>
              </div>
            </div>
          </div>
        </div>

        <div v-if="activeTab === 'decode'" class="decode-content">
          <div class="decode-placeholder">
            <i class="fa-solid fa-upload placeholder-icon" />
            <p>二维码解析功能开发中...</p>
            <p class="hint">敬请期待</p>
          </div>
        </div>
      </div>

      <div class="preview-panel">
        <h3 class="panel-title">
          <i class="fa-solid fa-eye" />
          预览
        </h3>

        <div v-if="errorMessage" class="error-message">
          <i class="fa-solid fa-circle-exclamation" />
          {{ errorMessage }}
        </div>

        <div class="qr-preview">
          <div v-if="isGenerating" class="loading">
            <i class="fa-solid fa-spinner fa-spin" />
            <span>生成中...</span>
          </div>
          <div v-else-if="qrResult" class="qr-image-container">
            <img
              :src="`data:image/png;base64,${qrResult.imageBase64}`"
              alt="QR Code"
              class="qr-image"
            />
          </div>
          <div v-else class="empty-qr">
            <i class="fa-solid fa-qrcode" />
          </div>
        </div>

        <div v-if="qrResult" class="qr-actions">
          <button class="action-btn primary" @click="downloadQrCode">
            <i class="fa-solid fa-download" />
            下载PNG
          </button>
          <button class="action-btn" @click="copyQrImage">
            <i class="fa-regular fa-copy" />
            复制图片
          </button>
        </div>

        <div v-if="qrResult" class="qr-info">
          <div class="info-item">
            <span class="info-label">尺寸</span>
            <span class="info-value">{{ qrResult.size }} x {{ qrResult.size }}</span>
          </div>
          <div class="info-item">
            <span class="info-label">容错</span>
            <span class="info-value">Level {{ qrResult.level }}</span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.qrcode-tool {
  height: 100%;
  overflow-y: auto;
  padding: 16px;
  box-sizing: border-box;
}

.tool-container {
  display: grid;
  grid-template-columns: 1fr 360px;
  gap: 20px;
  max-width: 1100px;
  margin: 0 auto;
  height: calc(100% - 20px);
}

.settings-panel,
.preview-panel {
  background: white;
  border: 1px solid #dee2e6;
  border-radius: 8px;
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 16px;
  overflow: hidden;
}

.settings-panel {
  overflow-y: auto;
}

.panel-tabs {
  display: flex;
  gap: 4px;
  background: #f8f9fa;
  padding: 4px;
  border-radius: 6px;
  flex-shrink: 0;
}

.panel-tab {
  flex: 1;
  padding: 8px 12px;
  border: none;
  background: transparent;
  color: #6c757d;
  font-size: 13px;
  font-weight: 500;
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
}

.panel-tab:hover {
  background: #e9ecef;
  color: #495057;
}

.panel-tab.active {
  background: #0d6efd;
  color: white;
}

.settings-content {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.panel-title {
  font-size: 14px;
  font-weight: 600;
  color: #495057;
  margin: 0;
  display: flex;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
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

.type-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 6px;
}

.type-btn {
  padding: 8px 4px;
  border: 1px solid #dee2e6;
  background: white;
  color: #495057;
  border-radius: 6px;
  font-size: 11px;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
}

.type-btn i {
  font-size: 16px;
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

.text-input,
.select-input {
  width: 100%;
  padding: 8px 12px;
  border: 1px solid #dee2e6;
  border-radius: 6px;
  font-size: 14px;
  box-sizing: border-box;
  transition: border-color 0.2s;
  resize: vertical;
  font-family: inherit;
}

.text-input:focus,
.select-input:focus {
  outline: none;
  border-color: #0d6efd;
  box-shadow: 0 0 0 3px rgba(13, 110, 253, 0.1);
}

.wifi-fields,
.vcard-fields,
.sms-fields,
.email-fields {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.divider {
  height: 1px;
  background: #e9ecef;
  margin: 4px 0;
}

.advanced-settings {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.settings-title {
  font-size: 13px;
  font-weight: 600;
  color: #495057;
  margin: 0;
  display: flex;
  align-items: center;
  gap: 6px;
}

.range-input {
  width: 100%;
  height: 6px;
  border-radius: 3px;
  background: #e9ecef;
  outline: none;
  -webkit-appearance: none;
  cursor: pointer;
}

.range-input::-webkit-slider-thumb {
  -webkit-appearance: none;
  width: 16px;
  height: 16px;
  border-radius: 50%;
  background: #0d6efd;
  cursor: pointer;
}

.level-options {
  display: flex;
  gap: 4px;
}

.level-btn {
  flex: 1;
  padding: 6px 8px;
  border: 1px solid #dee2e6;
  background: white;
  color: #495057;
  border-radius: 4px;
  font-size: 12px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
}

.level-btn:hover {
  border-color: #0d6efd;
  color: #0d6efd;
}

.level-btn.active {
  background: #0d6efd;
  border-color: #0d6efd;
  color: white;
}

.color-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}

.color-input-wrap {
  display: flex;
  gap: 6px;
  align-items: center;
}

.color-input {
  width: 36px;
  height: 36px;
  padding: 2px;
  border: 1px solid #dee2e6;
  border-radius: 4px;
  cursor: pointer;
  background: none;
}

.color-text {
  flex: 1;
  padding: 6px 8px;
  border: 1px solid #dee2e6;
  border-radius: 4px;
  font-size: 12px;
  font-family: monospace;
}

.decode-content {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
}

.decode-placeholder {
  text-align: center;
  color: #adb5bd;
}

.placeholder-icon {
  font-size: 48px;
  margin-bottom: 12px;
  opacity: 0.5;
}

.decode-placeholder p {
  margin: 4px 0;
  font-size: 14px;
}

.decode-placeholder .hint {
  font-size: 12px;
  color: #ced4da;
}

.preview-panel {
  align-items: center;
}

.qr-preview {
  width: 280px;
  height: 280px;
  border: 2px dashed #dee2e6;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #f8f9fa;
  flex-shrink: 0;
}

.qr-image-container {
  width: 100%;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
}

.qr-image {
  max-width: 100%;
  max-height: 100%;
  border-radius: 4px;
}

.empty-qr {
  font-size: 64px;
  color: #dee2e6;
}

.loading {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  color: #adb5bd;
  font-size: 14px;
}

.loading i {
  font-size: 32px;
}

.qr-actions {
  display: flex;
  gap: 8px;
  width: 100%;
}

.action-btn {
  flex: 1;
  padding: 10px 12px;
  border: 1px solid #dee2e6;
  background: white;
  color: #495057;
  border-radius: 6px;
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
}

.action-btn:hover {
  background: #f8f9fa;
  border-color: #ced4da;
}

.action-btn.primary {
  background: #0d6efd;
  border-color: #0d6efd;
  color: white;
}

.action-btn.primary:hover {
  background: #0b5ed7;
  border-color: #0b5ed7;
}

.qr-info {
  display: flex;
  gap: 20px;
  padding: 12px;
  background: #f8f9fa;
  border-radius: 6px;
  width: 100%;
  box-sizing: border-box;
}

.info-item {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.info-label {
  font-size: 11px;
  color: #adb5bd;
}

.info-value {
  font-size: 13px;
  font-weight: 600;
  color: #495057;
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
  width: 100%;
  box-sizing: border-box;
  flex-shrink: 0;
}
</style>
