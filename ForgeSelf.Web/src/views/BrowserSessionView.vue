<script setup lang="ts">
/**
 * 浏览器单实例只读页。只显示是否打开和当前地址，不操作页面。
 */
import { onMounted, ref } from 'vue'
import PageHead from '@/components/PageHead.vue'
import { authFetch } from '@/services/authFetch'

const loading = ref(true)
const errorText = ref('')
const open = ref(false)
const url = ref('')

onMounted(async () => {
  try {
    const res = await authFetch('/api/browser-use/session')
    if (res.status === 404) {
      errorText.value = '浏览器未启用'
      return
    }
    if (res.status === 401 || res.status === 403) {
      errorText.value = '需要访问密钥'
      return
    }
    if (!res.ok) {
      errorText.value = '读取浏览器状态失败'
      return
    }
    const json = (await res.json()) as { data?: { open?: boolean; url?: string | null } }
    open.value = json.data?.open === true
    url.value = json.data?.url?.trim() || ''
  } catch (e) {
    errorText.value = e instanceof Error ? e.message : '读取失败'
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="browser">
    <PageHead eyebrow="Browser · 浏览器" title="当前页面" />
    <p v-if="loading" class="browser-muted">正在读取</p>
    <p v-else-if="errorText" class="browser-muted">{{ errorText }}</p>
    <template v-else>
      <p class="browser-line">{{ open ? '已打开' : '未打开' }}</p>
      <p class="browser-muted">{{ url || '没有当前地址' }}</p>
    </template>
  </div>
</template>

<style scoped>
.browser-line {
  margin: 0 0 8px;
  color: var(--fg-primary);
  font-weight: 600;
}

.browser-muted {
  margin: 0;
  color: var(--fg-tertiary);
  font-size: 13px;
  word-break: break-all;
}
</style>
