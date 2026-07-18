<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { devToolsApi } from '@/services/devToolsApi'
import type { TimestampCurrentResult, TimestampConvertResult, TimezoneItem } from '@/types/devTools'

const currentTimestamp = ref<TimestampCurrentResult | null>(null)
const timeUnit = ref<'seconds' | 'milliseconds'>('seconds')
const timestampInput = ref('')
const dateTimeInput = ref('')
const timestampResult = ref<TimestampConvertResult | null>(null)
const dateTimeResult = ref<TimestampConvertResult | null>(null)
const timezones = ref<TimezoneItem[]>([])
const fromTimezone = ref('')
const toTimezone = ref('')
const convertTimestampInput = ref('')
const convertResult = ref<{
  fromDateTime: string
  toDateTime: string
  toTimestamp: number
} | null>(null)
const activeTab = ref<'to-datetime' | 'from-datetime' | 'convert'>('to-datetime')

let timer: ReturnType<typeof setInterval> | null = null

async function loadCurrentTimestamp(): Promise<void> {
  try {
    currentTimestamp.value = await devToolsApi.getCurrentTimestamp()
  } catch (e) {
    console.error('获取当前时间戳失败', e)
  }
}

async function loadTimezones(): Promise<void> {
  try {
    timezones.value = await devToolsApi.getTimezoneList()
    if (timezones.value.length > 0) {
      const local = Intl.DateTimeFormat().resolvedOptions().timeZone
      fromTimezone.value = local || timezones.value[0].id
      toTimezone.value = 'UTC'
    }
  } catch (e) {
    console.error('加载时区列表失败', e)
  }
}

async function convertTimestampToDateTime(): Promise<void> {
  if (!timestampInput.value.trim()) {
    timestampResult.value = null
    return
  }
  try {
    const ts = parseFloat(timestampInput.value)
    if (isNaN(ts)) return

    timestampResult.value = await devToolsApi.timestampToDateTime(ts, timeUnit.value)
  } catch (e) {
    console.error('时间戳转换失败', e)
  }
}

async function convertDateTimeToTimestamp(): Promise<void> {
  if (!dateTimeInput.value.trim()) {
    dateTimeResult.value = null
    return
  }
  try {
    dateTimeResult.value = await devToolsApi.dateTimeToTimestamp(dateTimeInput.value, timeUnit.value)
  } catch (e) {
    console.error('日期转换失败', e)
  }
}

async function convertTimezone(): Promise<void> {
  if (!convertTimestampInput.value.trim() || !fromTimezone.value || !toTimezone.value) {
    convertResult.value = null
    return
  }
  try {
    const ts = parseFloat(convertTimestampInput.value)
    if (isNaN(ts)) return

    const result = await devToolsApi.convertTimezone(ts, fromTimezone.value, toTimezone.value, timeUnit.value)
    convertResult.value = {
      fromDateTime: result.fromDateTime,
      toDateTime: result.toDateTime,
      toTimestamp: result.toTimestamp,
    }
  } catch (e) {
    console.error('时区转换失败', e)
  }
}

function copyToClipboard(text: string): void {
  navigator.clipboard.writeText(text).catch(() => {})
}

function useCurrentTimestamp(): void {
  if (!currentTimestamp.value) return
  const value = timeUnit.value === 'seconds'
    ? currentTimestamp.value.timestampSeconds.toString()
    : currentTimestamp.value.timestampMilliseconds.toString()
  timestampInput.value = value
  convertTimestampInput.value = value
  convertTimestampToDateTime()
  convertTimezone()
}

onMounted(() => {
  loadCurrentTimestamp()
  loadTimezones()
  timer = setInterval(() => {
    loadCurrentTimestamp()
  }, 1000)

  const now = new Date()
  dateTimeInput.value = now.toISOString().slice(0, 19)
})

onUnmounted(() => {
  if (timer) {
    clearInterval(timer)
  }
})
</script>

<template>
  <div class="timestamp-tool">
    <div class="current-timestamp-card">
      <div class="card-header">
        <i class="fa-regular fa-clock" />
        <span class="card-title">当前时间戳</span>
      </div>
      <div class="current-timestamp-content">
        <div class="timestamp-value-big">
          {{ timeUnit === 'seconds'
            ? currentTimestamp?.timestampSeconds?.toLocaleString()
            : currentTimestamp?.timestampMilliseconds?.toLocaleString()
          }}
          <span class="unit-label">{{ timeUnit === 'seconds' ? '秒' : '毫秒' }}</span>
        </div>
        <div class="unit-toggle">
          <button
            class="unit-btn"
            :class="{ active: timeUnit === 'seconds' }"
            @click="timeUnit = 'seconds'"
          >
            秒
          </button>
          <button
            class="unit-btn"
            :class="{ active: timeUnit === 'milliseconds' }"
            @click="timeUnit = 'milliseconds'"
          >
            毫秒
          </button>
        </div>
        <div class="current-datetime">
          <i class="fa-regular fa-calendar-days" />
          {{ currentTimestamp?.dateTimeLocal }}
        </div>
      </div>
    </div>

    <div class="tool-tabs">
      <button
        class="tool-tab"
        :class="{ active: activeTab === 'to-datetime' }"
        @click="activeTab = 'to-datetime'"
      >
        <i class="fa-solid fa-arrow-right" />
        时间戳转日期
      </button>
      <button
        class="tool-tab"
        :class="{ active: activeTab === 'from-datetime' }"
        @click="activeTab = 'from-datetime'"
      >
        <i class="fa-solid fa-arrow-left" />
        日期转时间戳
      </button>
      <button
        class="tool-tab"
        :class="{ active: activeTab === 'convert' }"
        @click="activeTab = 'convert'"
      >
        <i class="fa-solid fa-earth-asia" />
        时区转换
      </button>
    </div>

    <div v-if="activeTab === 'to-datetime'" class="converter-section">
      <div class="input-group">
        <label class="input-label">
          <i class="fa-regular fa-clock" />
          时间戳 ({{ timeUnit === 'seconds' ? '秒' : '毫秒' }})
        </label>
        <div class="input-with-btn">
          <input
            v-model="timestampInput"
            type="text"
            class="text-input"
            placeholder="输入时间戳..."
            @input="convertTimestampToDateTime"
          />
          <button class="quick-btn" title="使用当前时间" @click="useCurrentTimestamp">
            <i class="fa-solid fa-play" />
            当前
          </button>
        </div>
      </div>

      <div v-if="timestampResult" class="results-section">
        <div class="section-title">
          <i class="fa-solid fa-list-check" />
          常用格式
        </div>
        <div class="format-grid">
          <div
            v-for="format in Object.entries(timestampResult.formats)"
            :key="format[0]"
            class="format-item"
          >
            <span class="format-label">{{ format[0] }}</span>
            <code class="format-value">{{ format[1] }}</code>
            <button class="copy-inline-btn" @click="copyToClipboard(format[1])">
              <i class="fa-regular fa-copy" />
            </button>
          </div>
        </div>

        <div class="section-title">
          <i class="fa-solid fa-info-circle" />
          详细信息
        </div>
        <div class="detail-grid">
          <div class="detail-item">
            <span class="detail-label">时间戳 (秒)</span>
            <span class="detail-value">{{ timestampResult.timestampSeconds.toLocaleString() }}</span>
          </div>
          <div class="detail-item">
            <span class="detail-label">时间戳 (毫秒)</span>
            <span class="detail-value">{{ timestampResult.timestampMilliseconds.toLocaleString() }}</span>
          </div>
          <div class="detail-item">
            <span class="detail-label">ISO 8601</span>
            <span class="detail-value mono">{{ timestampResult.dateTimeIso }}</span>
          </div>
          <div class="detail-item">
            <span class="detail-label">本地时间</span>
            <span class="detail-value mono">{{ timestampResult.dateTimeLocal }}</span>
          </div>
        </div>
      </div>
    </div>

    <div v-if="activeTab === 'from-datetime'" class="converter-section">
      <div class="input-group">
        <label class="input-label">
          <i class="fa-regular fa-calendar" />
          日期时间
        </label>
        <div class="datetime-inputs">
          <input
            v-model="dateTimeInput"
            type="text"
            class="text-input"
            placeholder="例如: 2024-01-15 10:30:00"
            @input="convertDateTimeToTimestamp"
          />
        </div>
        <div class="format-hint">
          <i class="fa-solid fa-circle-info" />
          支持格式: YYYY-MM-DD HH:mm:ss、YYYY/MM/DD、ISO 8601 等
        </div>
      </div>

      <div v-if="dateTimeResult" class="results-section">
        <div class="section-title">
          <i class="fa-solid fa-clock" />
          时间戳结果
        </div>
        <div class="timestamp-result-big">
          <div class="result-item">
            <span class="result-label">秒级时间戳</span>
            <div class="result-value-row">
              <code class="result-value">{{ dateTimeResult.timestampSeconds.toLocaleString() }}</code>
              <button class="copy-inline-btn" @click="copyToClipboard(dateTimeResult.timestampSeconds.toString())">
                <i class="fa-regular fa-copy" />
              </button>
            </div>
          </div>
          <div class="result-item">
            <span class="result-label">毫秒级时间戳</span>
            <div class="result-value-row">
              <code class="result-value">{{ dateTimeResult.timestampMilliseconds.toLocaleString() }}</code>
              <button class="copy-inline-btn" @click="copyToClipboard(dateTimeResult.timestampMilliseconds.toString())">
                <i class="fa-regular fa-copy" />
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <div v-if="activeTab === 'convert'" class="converter-section">
      <div class="timezone-row">
        <div class="timezone-input-group">
          <label class="input-label">
            <i class="fa-regular fa-clock" />
            源时间戳
          </label>
          <input
            v-model="convertTimestampInput"
            type="text"
            class="text-input"
            placeholder="输入时间戳..."
            @input="convertTimezone"
          />
        </div>

        <div class="timezone-select-group">
          <div class="select-row">
            <label class="select-label">源时区</label>
            <select v-model="fromTimezone" class="select-input" @change="convertTimezone">
              <option v-for="tz in timezones" :key="tz.id" :value="tz.id">
                {{ tz.displayName }} ({{ tz.baseUtcOffset }})
              </option>
            </select>
          </div>

          <div class="swap-btn-wrapper">
            <button class="swap-btn" @click="convertTimezone()">
              <i class="fa-solid fa-arrow-down-up-across-line" />
            </button>
          </div>

          <div class="select-row">
            <label class="select-label">目标时区</label>
            <select v-model="toTimezone" class="select-input" @change="convertTimezone">
              <option v-for="tz in timezones" :key="tz.id" :value="tz.id">
                {{ tz.displayName }} ({{ tz.baseUtcOffset }})
              </option>
            </select>
          </div>
        </div>
      </div>

      <div v-if="convertResult" class="results-section">
        <div class="section-title">
          <i class="fa-solid fa-globe" />
          转换结果
        </div>
        <div class="timezone-convert-result">
          <div class="tz-result-item">
            <span class="tz-label">{{ fromTimezone }}</span>
            <code class="tz-datetime">{{ convertResult.fromDateTime }}</code>
          </div>
          <div class="tz-arrow">
            <i class="fa-solid fa-arrow-down" />
          </div>
          <div class="tz-result-item target">
            <span class="tz-label">{{ toTimezone }}</span>
            <code class="tz-datetime">{{ convertResult.toDateTime }}</code>
            <div class="tz-timestamp">
              时间戳: {{ convertResult.toTimestamp.toLocaleString() }}
              <button class="copy-inline-btn" @click="copyToClipboard(convertResult.toTimestamp.toString())">
                <i class="fa-regular fa-copy" />
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.timestamp-tool {
  display: flex;
  flex-direction: column;
  gap: 16px;
  height: 100%;
  overflow-y: auto;
  padding: 4px;
}

.current-timestamp-card {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  border-radius: 12px;
  color: white;
  overflow: hidden;
  flex-shrink: 0;
}

.card-header {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px 16px;
  background: rgba(255, 255, 255, 0.1);
}

.card-title {
  font-size: 14px;
  font-weight: 600;
}

.current-timestamp-content {
  padding: 20px;
  text-align: center;
}

.timestamp-value-big {
  font-size: 32px;
  font-weight: 700;
  font-family: 'Monaco', 'Menlo', 'Consolas', monospace;
  margin-bottom: 12px;
  letter-spacing: 1px;
}

.unit-label {
  font-size: 14px;
  font-weight: 500;
  opacity: 0.8;
  margin-left: 8px;
}

.unit-toggle {
  display: inline-flex;
  gap: 4px;
  padding: 4px;
  background: rgba(255, 255, 255, 0.15);
  border-radius: 8px;
  margin-bottom: 12px;
}

.unit-btn {
  padding: 6px 16px;
  border: none;
  background: transparent;
  color: rgba(255, 255, 255, 0.8);
  font-size: 13px;
  font-weight: 500;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s;
}

.unit-btn:hover {
  background: rgba(255, 255, 255, 0.1);
}

.unit-btn.active {
  background: white;
  color: #667eea;
}

.current-datetime {
  font-size: 14px;
  opacity: 0.9;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
}

.tool-tabs {
  display: flex;
  gap: 4px;
  padding: 4px;
  background: #f8f9fa;
  border-radius: 8px;
  flex-shrink: 0;
}

.tool-tab {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  padding: 10px;
  border: none;
  background: transparent;
  color: #6c757d;
  font-size: 13px;
  font-weight: 500;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s;
}

.tool-tab:hover {
  background: #e9ecef;
  color: #495057;
}

.tool-tab.active {
  background: white;
  color: #0d6efd;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
}

.converter-section {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.input-group {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.input-label {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  font-weight: 500;
  color: #495057;
}

.input-with-btn {
  display: flex;
  gap: 8px;
}

.text-input {
  flex: 1;
  padding: 10px 12px;
  border: 1px solid #dee2e6;
  border-radius: 8px;
  font-size: 14px;
  color: #212529;
  outline: none;
  transition: border-color 0.2s, box-shadow 0.2s;
}

.text-input:focus {
  border-color: #0d6efd;
  box-shadow: 0 0 0 3px rgba(13, 110, 253, 0.1);
}

.quick-btn {
  padding: 10px 14px;
  border: none;
  background: #0d6efd;
  color: white;
  border-radius: 8px;
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 6px;
  transition: background 0.2s;
  white-space: nowrap;
}

.quick-btn:hover {
  background: #0b5ed7;
}

.datetime-inputs {
  display: flex;
  gap: 8px;
}

.format-hint {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: #6c757d;
}

.results-section {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.section-title {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  font-weight: 600;
  color: #495057;
  padding-bottom: 4px;
  border-bottom: 1px solid #e9ecef;
}

.format-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
  gap: 8px;
}

.format-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 12px;
  background: #f8f9fa;
  border-radius: 8px;
  border: 1px solid #e9ecef;
}

.format-label {
  font-size: 12px;
  color: #6c757d;
  font-weight: 500;
  min-width: 70px;
}

.format-value {
  flex: 1;
  font-size: 12px;
  color: #212529;
  word-break: break-all;
}

.copy-inline-btn {
  padding: 4px 8px;
  border: none;
  background: transparent;
  color: #6c757d;
  cursor: pointer;
  border-radius: 4px;
  font-size: 12px;
  transition: all 0.2s;
  flex-shrink: 0;
}

.copy-inline-btn:hover {
  background: #e9ecef;
  color: #0d6efd;
}

.detail-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 8px;
}

.detail-item {
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding: 10px 12px;
  background: white;
  border: 1px solid #dee2e6;
  border-radius: 8px;
}

.detail-label {
  font-size: 11px;
  color: #6c757d;
  font-weight: 500;
}

.detail-value {
  font-size: 13px;
  color: #212529;
  font-weight: 500;
}

.detail-value.mono {
  font-family: 'Monaco', 'Menlo', 'Consolas', monospace;
  font-size: 12px;
}

.timestamp-result-big {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.result-item {
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding: 14px 16px;
  background: #f8f9fa;
  border-radius: 8px;
  border: 1px solid #e9ecef;
}

.result-label {
  font-size: 12px;
  color: #6c757d;
  font-weight: 500;
}

.result-value-row {
  display: flex;
  align-items: center;
  gap: 8px;
}

.result-value {
  flex: 1;
  font-size: 18px;
  font-weight: 600;
  color: #0d6efd;
  font-family: 'Monaco', 'Menlo', 'Consolas', monospace;
}

.timezone-row {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.timezone-input-group {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.timezone-select-group {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.select-row {
  display: flex;
  align-items: center;
  gap: 10px;
}

.select-label {
  font-size: 13px;
  color: #495057;
  font-weight: 500;
  min-width: 60px;
}

.select-input {
  flex: 1;
  padding: 8px 10px;
  border: 1px solid #dee2e6;
  border-radius: 8px;
  font-size: 13px;
  color: #212529;
  background: white;
  outline: none;
  cursor: pointer;
  transition: border-color 0.2s;
}

.select-input:focus {
  border-color: #0d6efd;
}

.swap-btn-wrapper {
  display: flex;
  justify-content: center;
}

.swap-btn {
  width: 36px;
  height: 36px;
  display: flex;
  align-items: center;
  justify-content: center;
  border: 1px solid #dee2e6;
  background: white;
  color: #6c757d;
  border-radius: 50%;
  cursor: pointer;
  font-size: 14px;
  transition: all 0.2s;
}

.swap-btn:hover {
  background: #f8f9fa;
  color: #0d6efd;
  border-color: #0d6efd;
}

.timezone-convert-result {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.tz-result-item {
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding: 12px 14px;
  background: #f8f9fa;
  border-radius: 8px;
  border: 1px solid #e9ecef;
}

.tz-result-item.target {
  background: #e7f5ff;
  border-color: #a5d8ff;
}

.tz-label {
  font-size: 11px;
  color: #6c757d;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.tz-result-item.target .tz-label {
  color: #0d6efd;
}

.tz-datetime {
  font-size: 15px;
  font-weight: 600;
  color: #212529;
  font-family: 'Monaco', 'Menlo', 'Consolas', monospace;
}

.tz-result-item.target .tz-datetime {
  color: #0d6efd;
}

.tz-timestamp {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12px;
  color: #495057;
  margin-top: 4px;
}

.tz-arrow {
  display: flex;
  justify-content: center;
  color: #adb5bd;
  font-size: 14px;
}
</style>
