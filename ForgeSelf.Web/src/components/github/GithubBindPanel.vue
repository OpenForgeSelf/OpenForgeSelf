<script setup lang="ts">
/**
 * GitHub 绑定面板（PILOT-083 + 绑定 UX 二轮）。
 * 四态：① 点击绑定 → 后端准备期 loading「正在准备设备码，请稍候…」（按钮 loading 防连点）
 *      ② 出码 → 自动复制 + 横幅「设备码已复制到剪贴板」+ 3s 倒计时自动打开预填授权页
 *      ③ 出码期间持续轮询（尊重 slow_down）
 *      ④ 终态：成功（展示账号）/ 失败（透传上游原因 + 重试）
 * 颜色一律 --el-* 变量（DESIGN.md 红线：禁裸色值）。
 */
import { computed, onBeforeUnmount, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { Loading } from '@element-plus/icons-vue'
import { githubAccountApi } from '@/services/githubAccountApi'
import { copyToClipboard } from '@/utils/clipboard'
import type { DeviceCodeStart, GitHubPollStatus } from '@/types/githubAccount'

const emit = defineEmits<{ (e: 'bound', accountLogin: string): void }>()

type Phase = 'idle' | 'preparing' | 'code' | 'error'

const phase = ref<Phase>('idle')
const preparing = ref(false)
const start = ref<DeviceCodeStart | null>(null)
const errorMessage = ref('')
const countdown = ref(0)
const copied = ref(false)

let countdownTimer: ReturnType<typeof setInterval> | null = null
let pollTimer: ReturnType<typeof setTimeout> | null = null
let pollIntervalMs = 5000
let currentDeviceCode = ''

const codePhaseTitle = computed(() => `第 1 步：在 GitHub 输入设备码 ${start.value?.userCode ?? ''}`)

function clearTimers(): void {
  if (countdownTimer) { clearInterval(countdownTimer); countdownTimer = null }
  if (pollTimer) { clearTimeout(pollTimer); pollTimer = null }
}

onBeforeUnmount(clearTimers)

/** ① 发起绑定：后端申请设备码期间整块 loading，按钮 loading 防连点 */
async function onBindClick(): Promise<void> {
  if (preparing.value) return
  clearTimers()
  phase.value = 'preparing'
  preparing.value = true
  errorMessage.value = ''
  copied.value = false
  const res = await githubAccountApi.startBind()
  preparing.value = false
  if (!res.success || !res.data) {
    phase.value = 'error'
    errorMessage.value = res.error || '申请设备码失败'
    return
  }
  start.value = res.data
  currentDeviceCode = res.data.deviceCode
  pollIntervalMs = Math.max(3, res.data.interval) * 1000
  phase.value = 'code'
  await autoCopyUserCode()
  startCountdownAndOpen()
  schedulePoll()
}

/** ② 出码即复制 + 横幅提示（复制失败不阻塞流程，横幅显示手动复制引导） */
async function autoCopyUserCode(): Promise<void> {
  if (!start.value) return
  const ok = await copyToClipboard(start.value.userCode)
  copied.value = ok
  if (ok) ElMessage.success('设备码已复制到剪贴板')
  else ElMessage.warning('自动复制失败，请手动复制设备码')
}

async function onManualCopy(): Promise<void> {
  if (!start.value) return
  const ok = await copyToClipboard(start.value.userCode)
  copied.value = ok
  if (ok) ElMessage.success('设备码已复制到剪贴板')
  else ElMessage.error('复制失败，请手动选择复制')
}

/** ② 3s 倒计时自动打开预填授权页（user_code 已在后端 URL 预填） */
function startCountdownAndOpen(): void {
  countdown.value = 3
  countdownTimer = setInterval(() => {
    countdown.value -= 1
    if (countdown.value <= 0) {
      clearCountdownOnly()
      openAuthPage()
    }
  }, 1000)
}

function clearCountdownOnly(): void {
  if (countdownTimer) { clearInterval(countdownTimer); countdownTimer = null }
}

function openAuthPage(): void {
  const url = start.value?.verificationUriPrefilled || start.value?.verificationUri
  if (url) window.open(url, '_blank', 'noopener')
}

/** ③ 轮询：pending 继续、slow_down 放慢 5s、终态收口 */
function schedulePoll(extraDelayMs = 0): void {
  pollTimer = setTimeout(() => { void pollOnce() }, pollIntervalMs + extraDelayMs)
}

async function pollOnce(): Promise<void> {
  if (phase.value !== 'code' || !currentDeviceCode) return
  const res = await githubAccountApi.pollBind(currentDeviceCode)
  if (phase.value !== 'code') return
  if (!res.success || !res.data) {
    // 网络层失败不终止轮询，退避后重试
    schedulePoll(3000)
    return
  }
  const status = res.data.status as GitHubPollStatus
  if (status === 'pending') { schedulePoll(); return }
  if (status === 'slow_down') { pollIntervalMs += 5000; schedulePoll(); return }
  if (status === 'bound') {
    clearTimers()
    phase.value = 'idle'
    ElMessage.success(`GitHub 账号已绑定：${res.data.accountLogin ?? ''}`)
    emit('bound', res.data.accountLogin ?? '')
    return
  }
  clearTimers()
  phase.value = 'error'
  errorMessage.value = res.data.error || '绑定未完成'
}
</script>

<template>
  <div class="github-bind-panel">
    <!-- ① 空闲：入口按钮 -->
    <template v-if="phase === 'idle'">
      <div class="bind-entry">
        <ElButton type="primary" :loading="preparing" data-test="bind-button" @click="onBindClick">
          绑定 GitHub 账号
        </ElButton>
        <p class="bind-hint">绑定后，插件市场安装与 GitHub 仓库同步将自动使用你的账号凭据。</p>
      </div>
    </template>

    <!-- ① 后端准备期：整块 loading -->
    <template v-else-if="phase === 'preparing'">
      <div class="bind-preparing" data-test="preparing-block">
        <ElIcon class="is-loading preparing-icon"><Loading /></ElIcon>
        <span data-test="preparing-text">正在准备设备码，请稍候…</span>
      </div>
    </template>

    <!-- ② 出码：复制横幅 + 设备码 + 倒计时 -->
    <template v-else-if="phase === 'code' && start">
      <div class="code-block" data-test="code-block">
        <ElAlert v-if="copied" type="success" :closable="false" show-icon data-test="copied-banner"
          title="设备码已复制到剪贴板" />
        <ElAlert v-else type="warning" :closable="false" show-icon data-test="copy-fail-banner"
          title="自动复制失败，请手动复制下方设备码" />
        <div class="code-row">
          <code class="user-code" data-test="user-code">{{ start.userCode }}</code>
          <ElButton size="small" data-test="copy-button" @click="onManualCopy">复制</ElButton>
        </div>
        <p class="code-hint" data-test="countdown-hint">
          {{ countdown > 0 ? `${countdown} 秒后自动打开 GitHub 授权页…` : 'GitHub 授权页已打开，粘贴设备码并授权后回到本页等待。' }}
        </p>
        <div class="code-actions">
          <ElButton size="small" data-test="open-page-button" @click="openAuthPage">手动打开授权页</ElButton>
        </div>
      </div>
    </template>

    <!-- ④ 失败：透传原因 + 重试 -->
    <template v-else-if="phase === 'error'">
      <div class="error-block" data-test="error-block">
        <ElAlert type="error" :closable="false" show-icon :title="codePhaseTitle" data-test="error-banner">
          <span data-test="error-text">{{ errorMessage }}</span>
        </ElAlert>
        <ElButton type="primary" class="retry-button" data-test="retry-button" @click="onBindClick">
          重试
        </ElButton>
      </div>
    </template>
  </div>
</template>

<style scoped>
.github-bind-panel {
  width: 100%;
}

.bind-hint {
  margin-top: 8px;
  font-size: var(--el-font-size-extra-small);
  color: var(--el-text-color-secondary);
}

.bind-preparing {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px 0;
  color: var(--el-text-color-regular);
}

.preparing-icon {
  font-size: var(--el-font-size-medium);
  color: var(--el-color-primary);
}

.code-row {
  display: flex;
  align-items: center;
  gap: 8px;
  margin: 12px 0;
}

.user-code {
  font-size: var(--el-font-size-large);
  font-weight: 600;
  letter-spacing: 2px;
  padding: 6px 10px;
  background: var(--el-fill-color-light);
  border-radius: var(--el-border-radius-base);
  color: var(--el-text-color-primary);
}

.code-hint {
  font-size: var(--el-font-size-small);
  color: var(--el-text-color-secondary);
}

.code-actions {
  margin-top: 8px;
}

.error-block .retry-button {
  margin-top: 12px;
}
</style>
