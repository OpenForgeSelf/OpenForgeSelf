<script setup lang="ts">
/**
 * API 服务器面板 — 独立组件
 * 对齐设计稿 forgeself-design/pages/api-server.html
 * 纯 Element Plus 组件 + Tailwind 布局，无自定义 token
 */
import { ref, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { CopyDocument, Refresh, Document, Key, Lock } from '@element-plus/icons-vue'
import { apiServerApi } from '@/services/apiServerApi'
import type { ApiServerConfig } from '@/types/apiServer'

const config = ref<ApiServerConfig | null>(null)
const loading = ref(false)
const error = ref('')

async function loadConfig() {
  loading.value = true
  error.value = ''
  try {
    config.value = await apiServerApi.getConfig()
  } catch (e) {
    error.value = e instanceof Error ? e.message : '加载失败'
  } finally {
    loading.value = false
  }
}

async function regenerateKey() {
  loading.value = true
  error.value = ''
  try {
    config.value = await apiServerApi.regenerateKey()
    ElMessage.success('密钥已重新生成')
  } catch (e) {
    error.value = e instanceof Error ? e.message : '重新生成失败'
  } finally {
    loading.value = false
  }
}

async function copyValue(value: string, label: string) {
  try {
    await navigator.clipboard.writeText(value)
    ElMessage.success(`${label}已复制`)
  } catch {
    ElMessage.error('复制失败，请手动复制')
  }
}

function openApiDocs() {
  window.open('/scalar/v1', '_blank', 'noopener,noreferrer')
}

onMounted(() => {
  loadConfig()
})
</script>

<template>
  <div class="max-w-[800px] space-y-6">
    <!-- 页头 -->
    <div class="flex items-start justify-between pb-4 border-b border-border">
      <div>
        <h2 class="text-2xl font-bold text-text m-0 leading-tight">API 服务器</h2>
        <p class="text-sm text-text-regular mt-1 mb-0">通过 OpenAI 兼容的 HTTP API 暴露铸己匣的 AI 功能</p>
      </div>
      <el-button type="success" :icon="Document" @click="openApiDocs">
        API 文档
      </el-button>
    </div>

    <!-- 首次加载：骨架屏 -->
    <el-skeleton v-if="loading && !config" :rows="4" animated />

    <!-- 错误提示 -->
    <el-alert
      v-if="error && !loading"
      :title="error"
      type="error"
      show-icon
      :closable="false"
    />

    <!-- 主内容 -->
    <template v-if="config">
      <!-- 运行状态 -->
      <el-card shadow="never" class="!border-success/30">
        <div class="flex items-center gap-3">
          <span class="w-2 h-2 rounded-full bg-success shrink-0 ring-4 ring-success/10" />
          <div>
            <span class="text-sm font-semibold text-success">运行中</span>
            <div class="flex items-center gap-2 mt-1">
              <code class="font-mono text-sm text-text-regular">{{ config.apiBaseUrl }}</code>
              <el-button
                :icon="CopyDocument"
                size="small"
                text
                @click="copyValue(config.apiBaseUrl, 'API 地址')"
              />
            </div>
          </div>
        </div>
      </el-card>

      <!-- API 密钥 -->
      <el-card shadow="never">
        <template #header>
          <div class="flex items-center justify-between">
            <div class="flex items-center gap-2">
              <el-icon :size="16" class="text-accent"><Key /></el-icon>
              <span class="text-base font-semibold text-text">API 密钥</span>
            </div>
            <div class="flex items-center gap-2">
              <el-button
                size="small"
                :icon="CopyDocument"
                @click="copyValue(config.authHeader.replace('Authorization: Bearer ', ''), 'API 密钥')"
              >
                复制
              </el-button>
              <el-button
                size="small"
                type="primary"
                :icon="Refresh"
                :loading="loading"
                @click="regenerateKey"
              >
                重新生成
              </el-button>
            </div>
          </div>
        </template>
        <p class="text-sm text-text-secondary mt-0 mb-3">用于 API 访问的安全认证令牌</p>
        <el-input
          :model-value="config.apiKeyMasked || '未配置'"
          readonly
          class="font-mono"
        />
      </el-card>

      <!-- 授权标头 -->
      <el-card shadow="never">
        <template #header>
          <div class="flex items-center justify-between">
            <div class="flex items-center gap-2">
              <el-icon :size="16" class="text-accent"><Lock /></el-icon>
              <span class="text-base font-semibold text-text">授权标头</span>
            </div>
            <el-button
              size="small"
              :icon="CopyDocument"
              @click="copyValue(config.authHeader, '授权标头')"
            >
              复制
            </el-button>
          </div>
        </template>
        <p class="text-sm text-text-secondary mt-0 mb-3">请求时需要在 HTTP 头中携带的认证信息</p>
        <el-input
          :model-value="config.authHeader || '—'"
          readonly
          class="font-mono"
        />
      </el-card>
    </template>

    <!-- 空状态 -->
    <el-empty
      v-if="!loading && !config && !error"
      description="无法加载 API 服务器配置，请确认后端正在运行。"
    >
      <el-button type="primary" @click="loadConfig">重试</el-button>
    </el-empty>
  </div>
</template>
