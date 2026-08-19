<script setup lang="ts">
import { ref, watch, computed } from 'vue'
import { ElMessage } from 'element-plus'
import { captureApi } from '@/services/captureApi'
import type { CaptureSessionDetail } from '@/types/capture'

/**
 * 抓包记录详情抽屉（Fiddler 风格）：展示一条会话的完整请求/响应/原始数据。
 */

const props = defineProps<{
  visible: boolean
  sessionId: number | null
}>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
}>()

const loading = ref(false)
const detail = ref<CaptureSessionDetail | null>(null)
const loadError = ref('')

// 解析 "Key: Value" 文本为键值对列表（headers 展示用）
function parseHeaders(text?: string | null): Array<{ key: string; value: string }> {
  if (!text) return []
  return text
    .split('\n')
    .map((line) => line.trim())
    .filter(Boolean)
    .map((line) => {
      const idx = line.indexOf(':')
      if (idx <= 0) return { key: line, value: '' }
      return { key: line.slice(0, idx).trim(), value: line.slice(idx + 1).trim() }
    })
}

const requestHeaders = computed(() => parseHeaders(detail.value?.requestHeaders))
const responseHeaders = computed(() => parseHeaders(detail.value?.responseHeaders))

// 状态码语义色：2xx 成功、4xx/5xx 失败，其余中性
function statusType(code?: number | null): 'success' | 'danger' | 'info' {
  if (code == null) return 'info'
  if (code >= 200 && code < 300) return 'success'
  if (code >= 400) return 'danger'
  return 'info'
}

// 字节数人性化
function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / 1024 / 1024).toFixed(2)} MB`
}

watch(
  () => props.visible,
  async (visible) => {
    if (!visible || props.sessionId == null) return
    loading.value = true
    loadError.value = ''
    detail.value = null
    try {
      detail.value = await captureApi.fetchSessionDetail(props.sessionId)
    } catch (e) {
      loadError.value = e instanceof Error ? e.message : '加载失败'
      ElMessage.error(loadError.value)
    } finally {
      loading.value = false
    }
  }
)
</script>

<template>
  <ElDrawer
    :model-value="visible"
    title="抓包详情"
    size="680px"
    destroy-on-close
    @update:model-value="emit('update:visible', $event)"
  >
    <div v-loading="loading" class="capture-detail">
      <ElEmpty v-if="loadError" :description="`加载失败：${loadError}`" />

      <template v-else-if="detail">
        <ElDescriptions :column="2" border size="small" class="!mb-4">
          <ElDescriptionsItem label="协议">
            <ElTag size="small" effect="plain">{{ detail.protocol }}</ElTag>
          </ElDescriptionsItem>
          <ElDescriptionsItem label="时间">
            {{ new Date(detail.timestamp).toLocaleString() }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="方法">{{ detail.method || '-' }}</ElDescriptionsItem>
          <ElDescriptionsItem label="状态码">
            <ElTag v-if="detail.statusCode" :type="statusType(detail.statusCode)" size="small">
              {{ detail.statusCode }}
            </ElTag>
            <span v-else>-</span>
          </ElDescriptionsItem>
          <ElDescriptionsItem label="URL" :span="2">
            <span class="break-all text-[var(--el-text-color-regular)]">{{ detail.url || '-' }}</span>
          </ElDescriptionsItem>
          <ElDescriptionsItem label="客户端 IP">{{ detail.clientIp }}</ElDescriptionsItem>
          <ElDescriptionsItem label="本地端点">{{ detail.localEndpoint || '-' }}</ElDescriptionsItem>
          <ElDescriptionsItem label="目标">{{ detail.target || '（仅抓包，未转发）' }}</ElDescriptionsItem>
          <ElDescriptionsItem label="转发">
            <ElTag :type="detail.forwarded ? 'success' : 'info'" size="small" effect="plain">
              {{ detail.forwarded ? '已转发' : '未转发' }}
            </ElTag>
          </ElDescriptionsItem>
          <ElDescriptionsItem label="大小">
            {{ formatBytes(detail.requestBytes) }} / {{ formatBytes(detail.responseBytes) }}
          </ElDescriptionsItem>
          <ElDescriptionsItem label="耗时">{{ detail.durationMs }} ms</ElDescriptionsItem>
          <ElDescriptionsItem v-if="detail.errorMessage" label="错误" :span="2">
            <span class="text-[var(--el-color-danger)]">{{ detail.errorMessage }}</span>
          </ElDescriptionsItem>
        </ElDescriptions>

        <ElTabs type="border-card">
          <ElTabPane label="请求">
            <div v-if="requestHeaders.length" class="capture-block">
              <div class="capture-block-title">Headers</div>
              <div class="capture-headers">
                <div v-for="h in requestHeaders" :key="h.key" class="capture-header-row">
                  <span class="capture-header-key">{{ h.key }}</span>
                  <span class="capture-header-value">{{ h.value }}</span>
                </div>
              </div>
            </div>
            <div v-if="detail.requestBody" class="capture-block">
              <div class="capture-block-title">Body</div>
              <pre class="capture-pre">{{ detail.requestBody }}</pre>
            </div>
            <ElEmpty v-if="!requestHeaders.length && !detail.requestBody" description="无请求体数据" :image-size="60" />
          </ElTabPane>

          <ElTabPane label="响应">
            <div v-if="responseHeaders.length" class="capture-block">
              <div class="capture-block-title">Headers</div>
              <div class="capture-headers">
                <div v-for="h in responseHeaders" :key="h.key" class="capture-header-row">
                  <span class="capture-header-key">{{ h.key }}</span>
                  <span class="capture-header-value">{{ h.value }}</span>
                </div>
              </div>
            </div>
            <div v-if="detail.responseBody" class="capture-block">
              <div class="capture-block-title">Body</div>
              <pre class="capture-pre">{{ detail.responseBody }}</pre>
            </div>
            <ElEmpty v-if="!responseHeaders.length && !detail.responseBody" description="无响应体数据" :image-size="60" />
          </ElTabPane>

          <ElTabPane label="原始数据">
            <pre v-if="detail.rawPreview" class="capture-pre">{{ detail.rawPreview }}</pre>
            <ElEmpty v-else description="无原始数据" :image-size="60" />
          </ElTabPane>
        </ElTabs>
      </template>
    </div>
  </ElDrawer>
</template>

<style scoped>
/* 布局与排版仅用官方 token 派生，不定义自定义色值 token */
.capture-headers {
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 4px;
  overflow: hidden;
}
.capture-header-row {
  display: flex;
  gap: 12px;
  padding: 6px 10px;
  font-size: 12px;
  line-height: 1.5;
  border-bottom: 1px solid var(--el-border-color-lighter);
}
.capture-header-row:last-child {
  border-bottom: none;
}
.capture-header-key {
  min-width: 140px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  flex-shrink: 0;
}
.capture-header-value {
  color: var(--el-text-color-regular);
  word-break: break-all;
}
.capture-pre {
  margin: 0;
  padding: 10px;
  max-height: 320px;
  overflow: auto;
  font-family: var(--el-font-family-mono);
  font-size: 12px;
  line-height: 1.6;
  white-space: pre-wrap;
  word-break: break-all;
  background: var(--el-fill-color-light);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 4px;
  color: var(--el-text-color-regular);
}
.capture-block-title {
  font-size: 12px;
  font-weight: 600;
  color: var(--el-text-color-secondary);
  margin-bottom: 6px;
}
.capture-block {
  margin-bottom: 12px;
}
</style>
