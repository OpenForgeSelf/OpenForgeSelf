<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { apiGet } from './http'
import type { ClockNowDto } from './http'

/** 显示用时间字符串（后端返回 yyyy-MM-dd HH:mm:ss）。 */
const time = ref('')
/** Unix 毫秒时间戳（原始值，界面同时展示原始值便于核对）。 */
const timestamp = ref<number | null>(null)
/** 请求状态文案（加载中 / 失败原因）。 */
const status = ref('')
const loading = ref(false)

/** 版本徽标（plugin-development 铁律 13：根视图标题旁必须展示自身版本号）。 */
const version = ref('')
async function loadPluginVersion(): Promise<void> {
  try {
    // 宿主 GET /api/plugin 的响应是 {data:[...], code, message, success} 信封，http.request 会自动解包
    const list = await apiGet<Array<{ id?: string; version?: string }>>('/api/plugin')
    const hit = Array.isArray(list) ? list.find((p) => p?.id === 'clock-badge') : undefined
    if (hit?.version) version.value = `v${hit.version}`
  } catch {
    // 版本拉取失败不阻断主流程
  }
}

const hasData = computed(() => timestamp.value !== null)

async function refresh(): Promise<void> {
  if (loading.value) return
  loading.value = true
  status.value = '加载中…'
  try {
    const dto = await apiGet<ClockNowDto>('/api/clock-badge/now')
    if (dto) {
      time.value = dto.time
      timestamp.value = dto.timestamp
      status.value = ''
    } else {
      status.value = '响应为空，请重试'
    }
  } catch (e) {
    // 失败保留错误原文 + 重试入口（不吞错）
    status.value = e instanceof Error ? e.message : '未知错误'
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  void refresh()
  void loadPluginVersion()
})
</script>

<template>
  <div class="cb cb-root">
    <header class="cb-header">
      <div class="cb-eyebrow">ClockBadge · 演示插件</div>
      <div class="cb-header__row">
        <div class="cb-title">时钟徽标</div>
        <span v-if="version" class="cb-version">{{ version }}</span>
        <span class="cb-header__spacer"></span>
        <button type="button" class="cb-btn" :disabled="loading" @click="refresh">
          {{ loading ? '刷新中…' : '刷新' }}
        </button>
      </div>
      <div class="cb-subtitle">演示 GET /api/clock-badge/now → { time, timestamp }。</div>
    </header>

    <section class="cb-clock">
      <div v-if="loading && !hasData" class="cb-hint">正在加载服务器时间…</div>
      <template v-else-if="hasData">
        <div class="cb-clock__time">{{ time }}</div>
        <div class="cb-clock__meta">timestamp = {{ timestamp }}</div>
      </template>
      <div v-if="status" class="cb-error">{{ status }}</div>
    </section>
  </div>
</template>

<style>
/* 冻结模式：仅引用 --el-*（宿主级）与本地无裸色值回退，
   版本徽标样式按铁律 13 用小字号圆角灰底（--el-fill-color-light + --el-text-color-secondary），不抢标题视觉。 */
.cb-root {
  font-family: var(--el-font-family, system-ui, sans-serif);
  color: var(--el-text-color-primary);
  padding: 20px;
  max-width: 720px;
  margin: 0 auto;
  box-sizing: border-box;
  background: var(--el-bg-color);
  min-height: 100%;
  display: flex;
  flex-direction: column;
}
.cb-header {
  margin-bottom: 16px;
  flex-shrink: 0;
}
.cb-eyebrow {
  font-size: 10px;
  letter-spacing: 0.18em;
  text-transform: uppercase;
  color: var(--el-text-color-secondary);
  margin-bottom: 4px;
}
.cb-header__row {
  display: flex;
  align-items: baseline;
  gap: 10px;
}
.cb-header__spacer {
  flex: 1;
}
.cb-title {
  font-size: 24px;
  font-weight: 700;
  line-height: 1.2;
  color: var(--el-text-color-primary);
}
.cb-version {
  font-size: 11px;
  padding: 1px 8px;
  border-radius: var(--el-border-radius-base, 4px);
  background: var(--el-fill-color-light);
  color: var(--el-text-color-secondary);
}
.cb-btn {
  height: 30px;
  padding: 0 14px;
  font-size: 12px;
  border-radius: var(--el-border-radius-base, 4px);
  border: 1px solid var(--el-border-color);
  background: var(--el-bg-color);
  color: var(--el-text-color-regular);
  cursor: pointer;
}
.cb-btn:hover:not(:disabled) {
  border-color: var(--el-color-primary);
  color: var(--el-color-primary);
}
.cb-btn:disabled {
  cursor: not-allowed;
  opacity: 0.6;
}
.cb-subtitle {
  margin-top: 6px;
  font-size: 13px;
  color: var(--el-text-color-secondary);
  line-height: 1.5;
}
.cb-clock {
  border: 1px solid var(--el-border-color-light);
  border-radius: var(--el-border-radius-base, 4px);
  padding: 24px;
  text-align: center;
  flex-shrink: 0;
}
.cb-clock__time {
  font-size: 32px;
  font-weight: 700;
  font-variant-numeric: tabular-nums;
  color: var(--el-text-color-primary);
}
.cb-clock__meta {
  margin-top: 8px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
  font-variant-numeric: tabular-nums;
}
.cb-hint,
.cb-error {
  margin-top: 8px;
  font-size: 13px;
}
.cb-hint {
  color: var(--el-text-color-secondary);
}
.cb-error {
  color: var(--el-color-danger);
}
</style>
