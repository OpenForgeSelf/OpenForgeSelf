<script setup lang="ts">
/**
 * IM 网关配置页（ImGatewayView）—— v2.0.0 起仅企业微信「智能机器人」长连接一通道。
 *
 * 职责：可视化配置企微智能机器人凭证（BotId + Secret，支持扫码授权自动回填与手填），
 * 展示长连接状态灯。回调形态已整体移除（公众号/飞书/钉钉不再提供）。
 *
 * 约束（plugin-development 铁律 §4）：插件是独立预编译产物，不能用 <ElXxx>，
 * 一律原生 HTML + --el-* token 变量。
 */

import { onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { fetchConfig, saveConfig, fetchStatus, reconnectWeCom, startScanAuth, fetchScanAuth, fetchPluginVersion, type ScanAuthDto } from './http'

/** 企微单通道配置（camelCase 与后端 STJ 命名一致）。 */
interface WeComConfigDto {
  enabled: boolean
  boundAgentId?: string | null
  boundChatModelId?: string | null
  botId?: string
  secret?: string
}

interface GatewayConfigDto {
  weCom: WeComConfigDto
}

/** 长连接状态。 */
interface ConnectionDto {
  state: string
  message?: string | null
  connectedAt?: string | null
}

interface StatusDto {
  type: string
  name: string
  enabled: boolean
  /** websocket=长连接形态（本端外连，无需公网 IP）。 */
  transport?: string
  connection?: ConnectionDto
}

const config = reactive<GatewayConfigDto>({
  weCom: { enabled: false, botId: '', secret: '' },
})
const statuses = ref<StatusDto[]>([])
const saving = ref(false)
const message = ref('')
const messageType = ref<'ok' | 'err'>('ok')
/** 本插件运行版本（从宿主 /api/plugin 读，界面展示「自身版本」，便于排查版本错位）。 */
const pluginVersion = ref('')

// 扫码授权弹窗状态（方案 A：CLI 扫码 → 后端解密自动回填，前端只轮询展示）
const scanOpen = ref(false)
const scanning = ref(false)
const scan = ref<ScanAuthDto | null>(null)
let scanTimer: ReturnType<typeof setInterval> | undefined

const SCAN_STATE_TEXT: Record<string, string> = {
  starting: '正在拉起企微 CLI…',
  waitingscan: '请用手机企业微信扫码确认',
  succeeded: '授权成功，凭据已自动回填并保存',
  failed: '授权失败',
}

function scanText(): string {
  return scan.value ? (SCAN_STATE_TEXT[scan.value.state] ?? scan.value.state) : ''
}

/** 发起扫码授权并轮询状态。 */
async function openScanAuth() {
  scanOpen.value = true
  scanning.value = true
  scan.value = null
  try {
    const s = await startScanAuth()
    if (!s) throw new Error('扫码会话创建失败')
    scan.value = s
    pollScan()
  } catch (e) {
    scan.value = { id: '', state: 'failed', error: (e as Error).message }
  } finally {
    scanning.value = false
  }
}

/** 轮询扫码状态（2s 一次；终态停止并刷新配置）。 */
function pollScan() {
  clearScanTimer()
  scanTimer = setInterval(async () => {
    if (!scan.value?.id) return
    try {
      const s = await fetchScanAuth(scan.value.id)
      if (!s) return
      scan.value = s
      if (s.state === 'succeeded' || s.state === 'failed') {
        clearScanTimer()
        await load() // 回填后的配置读回表单
      }
    } catch {
      clearScanTimer()
    }
  }, 2000)
}

function clearScanTimer() {
  if (scanTimer) {
    clearInterval(scanTimer)
    scanTimer = undefined
  }
}

function closeScanAuth() {
  clearScanTimer()
  scanOpen.value = false
  scan.value = null
}

onMounted(load)
onBeforeUnmount(clearScanTimer)

/** 取企微通道的状态条目（v2.0.0 仅此一通道；模板用 enabled/connection 驱动徽标与状态灯）。 */
function statusOf(): StatusDto | undefined {
  return statuses.value.find((s) => s.type === 'wecom')
}

/** 长连接状态中文文案（状态对不上时回落到"未连接"，不臆造）。 */
const CONN_TEXT: Record<string, string> = {
  connected: '长连接已就绪',
  connecting: '正在建立长连接…',
  failed: '连接失败（自动重试中）',
  kicked: '已被其它实例抢占，已停止重连',
  disconnected: '未连接',
}

function connText(): string {
  const state = statusOf()?.connection?.state || 'disconnected'
  return CONN_TEXT[state] ?? '未连接'
}

async function load() {
  try {
    const cfg = (await fetchConfig()) as GatewayConfigDto | undefined
    if (cfg && cfg.weCom) config.weCom = cfg.weCom
    const st = (await fetchStatus()) as StatusDto[] | undefined
    if (st) statuses.value = st
    pluginVersion.value = await fetchPluginVersion('im-gateway')
  } catch (e) {
    setMsg(`加载失败：${(e as Error).message}`, 'err')
  }
}

function setMsg(text: string, type: 'ok' | 'err' = 'ok') {
  message.value = text
  messageType.value = type
}

async function save() {
  saving.value = true
  message.value = ''
  try {
    await saveConfig(config)
    await load()
    setMsg('已保存并重新加载配置', 'ok')
  } catch (e) {
    setMsg(`保存失败：${(e as Error).message}`, 'err')
  } finally {
    saving.value = false
  }
}

/** 手动重连（D2）：被踢后重新抢占 / 立即重连不等自动退避。 */
async function reconnect() {
  try {
    await reconnectWeCom()
    setMsg('已触发重连', 'ok')
    // 稍后刷新状态（连接建立需要时间）
    setTimeout(load, 1500)
  } catch (e) {
    setMsg(`重连失败：${(e as Error).message}`, 'err')
  }
}

/** 连接非就绪且通道已启用时，显示「重连」按钮。 */
function canReconnect(): boolean {
  const st = statusOf()
  return !!st?.enabled && st?.connection?.state !== 'connected'
}
</script>

<template>
  <div class="ig ig-root">
    <header class="ig-header">
      <div class="ig-header__row">
        <div class="ig-title">IM 网关</div>
        <span v-if="pluginVersion" class="ig-version">v{{ pluginVersion }}</span>
      </div>
      <div class="ig-subtitle">企业微信「智能机器人」WebSocket 长连接，统一接入 AI Agent。</div>
    </header>

    <div v-if="message" class="ig-alert" :class="messageType === 'err' ? 'ig-alert--err' : 'ig-alert--ok'">
      {{ message }}
    </div>

    <section class="ig-channel">
      <div class="ig-channel__head">
        <div>
          <div class="ig-channel__name">
            企业微信（智能机器人）
            <span
              class="ig-badge"
              :class="statusOf()?.enabled ? 'ig-badge--on' : 'ig-badge--off'"
            >
              {{ statusOf()?.enabled ? '已启用' : '未启用' }}
            </span>
          </div>
          <div class="ig-channel__desc">长连接形态：本端主动外连 wss，无需公网 IP / 回调域名</div>
        </div>
        <label class="ig-switch">
          <input type="checkbox" v-model="config.weCom.enabled" />
          <span class="ig-switch__track"></span>
        </label>
      </div>

      <div class="ig-field">
        <label>BotId（智能机器人 ID）</label>
        <input v-model="config.weCom.botId" placeholder="在企微后台开启「API 模式-长连接」后获得" />
        <label>Secret（长连接专用密钥）</label>
        <input v-model="config.weCom.secret" type="password" placeholder="长连接 Secret，非应用 Secret" />
        <div class="ig-scan-row">
          <button class="ig-btn ig-btn--scan" :disabled="scanning" @click="openScanAuth">
            {{ scanning ? '正在拉起…' : '扫码授权（自动回填）' }}
          </button>
          <span class="ig-hint">也可直接手填上方字段；扫码成功后凭据自动保存并重建长连接。</span>
        </div>
        <div class="ig-note">
          获取方式：企业微信管理后台 → 智能机器人 → 开启「API 模式」→ 选择「长连接」→ 复制 BotID 与 Secret。
          Secret 是长连接专用密钥，与回调模式的 Token / EncodingAESKey 不是同一回事。
          同一机器人同时只允许一个长连接，别处再连会把本连接踢下线。
        </div>
      </div>

      <div class="ig-bind">
        <label>绑定 AgentId（留空=默认）</label>
        <input v-model="config.weCom.boundAgentId" placeholder="可选，指定某 Agent 应答" />
        <label>绑定 ChatModel（provider:modelId，留空=默认）</label>
        <input v-model="config.weCom.boundChatModelId" placeholder="可选，如 openai:gpt-4o" />
      </div>

      <!-- 长连接状态灯（websocket 形态，v2.0.0 起唯一形态，恒展示） -->
      <div class="ig-conn">
        <span
          class="ig-dot"
          :class="'ig-dot--' + (statusOf()?.connection?.state || 'disconnected')"
        ></span>
        <span class="ig-conn__text">{{ connText() }}</span>
        <span v-if="statusOf()?.connection?.message" class="ig-conn__msg">
          {{ statusOf()!.connection!.message }}
        </span>
        <button v-if="canReconnect()" class="ig-btn ig-btn--reconnect" @click="reconnect">
          重连
        </button>
      </div>
    </section>

    <!-- 扫码授权弹窗（原生模态：无 EP 组件依赖，仅 --el-* token） -->
    <div v-if="scanOpen" class="ig-modal" @click.self="closeScanAuth">
      <div class="ig-modal__box">
        <div class="ig-modal__head">
          <span>扫码授权企业微信智能机器人</span>
          <button class="ig-btn ig-btn--close" @click="closeScanAuth">✕</button>
        </div>
        <div class="ig-modal__body">
          <div class="ig-scan-state">{{ scanText() }}</div>

          <template v-if="scan && scan.state !== 'failed'">
            <pre v-if="scan.qrText" class="ig-qr">{{ scan.qrText }}</pre>
            <a v-if="scan.qrLink" class="ig-qr-link" :href="scan.qrLink" target="_blank" rel="noopener">
              打不开二维码？点此打开扫码链接
            </a>
            <div class="ig-scan-tip">用手机企业微信扫一扫上方二维码，确认创建/授权机器人（勾选数据能力）。</div>
          </template>

          <div v-if="scan && scan.state === 'succeeded'" class="ig-scan-ok">
            已自动回填并保存 BotId：{{ scan.botId }}，长连接已按新凭据重建。可关闭本窗口。
          </div>
          <div v-if="scan && scan.state === 'failed'" class="ig-scan-err">
            {{ scan.error || '授权失败，请重试或改用手填' }}
          </div>
        </div>
        <div class="ig-modal__foot">
          <button class="ig-btn" @click="closeScanAuth">关闭</button>
        </div>
      </div>
    </div>

    <footer class="ig-footer">
      <button class="ig-btn ig-btn--primary" :disabled="saving" @click="save">
        {{ saving ? '保存中…' : '保存配置' }}
      </button>
      <span class="ig-hint">保存后立即生效（改密钥会重建长连接）。</span>
    </footer>
  </div>
</template>

<style>
/* 仅走 --el-* token，不定义独立色值；插件无 Tailwind，布局类自写。 */
.ig-root {
  font-family: var(--el-font-family, system-ui, sans-serif);
  color: var(--el-text-color-primary);
  background: var(--el-bg-color-page, #f5f7fa);
  padding: 20px;
  max-width: 880px;
  margin: 0 auto;
  box-sizing: border-box;
}
.ig-header {
  margin-bottom: 16px;
}
.ig-header__row {
  display: flex;
  align-items: center;
  gap: 10px;
}
.ig-version {
  font-size: 12px;
  font-weight: 500;
  padding: 1px 8px;
  border-radius: 10px;
  background: var(--el-fill-color-light, #f4f4f5);
  color: var(--el-text-color-secondary);
}
.ig-title {
  font-size: 20px;
  font-weight: 600;
}
.ig-subtitle {
  margin-top: 4px;
  font-size: 13px;
  color: var(--el-text-color-secondary);
  line-height: 1.5;
}
.ig-alert {
  padding: 10px 14px;
  border-radius: var(--el-border-radius-base, 6px);
  margin-bottom: 14px;
  font-size: 13px;
}
.ig-alert--ok {
  background: var(--el-color-success-light-9, #e1f3d8);
  color: var(--el-color-success, #67c23a);
  border: 1px solid var(--el-color-success-light-5, #b3e19d);
}
.ig-alert--err {
  background: var(--el-color-danger-light-9, #fef0f0);
  color: var(--el-color-danger, #f56c6c);
  border: 1px solid var(--el-color-danger-light-5, #fab6b6);
}
.ig-channel {
  background: var(--el-bg-color, #fff);
  border: 1px solid var(--el-border-color-lighter, #ebeef5);
  border-radius: var(--el-border-radius-base, 6px);
  padding: 16px;
  margin-bottom: 14px;
  box-shadow: var(--el-box-shadow-lighter, 0 1px 4px rgba(0, 0, 0, 0.04));
}
.ig-channel__head {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 12px;
}
.ig-channel__name {
  font-size: 16px;
  font-weight: 600;
  display: flex;
  align-items: center;
  gap: 8px;
}
.ig-channel__desc {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  margin-top: 2px;
}
.ig-badge {
  font-size: 12px;
  padding: 1px 8px;
  border-radius: 10px;
  font-weight: 500;
}
.ig-badge--on {
  background: var(--el-color-success-light-9, #e1f3d8);
  color: var(--el-color-success, #67c23a);
}
.ig-badge--off {
  background: var(--el-fill-color-light, #f4f4f5);
  color: var(--el-text-color-secondary);
}
.ig-field,
.ig-bind {
  display: grid;
  grid-template-columns: 220px 1fr;
  align-items: center;
  gap: 8px 12px;
  margin-bottom: 10px;
}
.ig-field label,
.ig-bind label {
  font-size: 13px;
  color: var(--el-text-color-regular);
}
.ig-field input,
.ig-bind input {
  height: 32px;
  padding: 0 10px;
  border: 1px solid var(--el-border-color, #dcdfe6);
  border-radius: var(--el-border-radius-base, 6px);
  background: var(--el-fill-color-blank, #fff);
  color: var(--el-text-color-primary);
  font-size: 13px;
  box-sizing: border-box;
  outline: none;
}
.ig-field input:focus,
.ig-bind input:focus {
  border-color: var(--el-color-primary, #409eff);
}
/* 通道说明框（跨满两列栅格） */
.ig-note {
  grid-column: 1 / -1;
  font-size: 12px;
  line-height: 1.6;
  color: var(--el-text-color-secondary);
  background: var(--el-fill-color-light, #f4f4f5);
  border-radius: var(--el-border-radius-base, 6px);
  padding: 8px 10px;
}
/* 长连接状态行 */
.ig-conn {
  margin-top: 10px;
  font-size: 12px;
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}
.ig-conn__text {
  color: var(--el-text-color-regular);
}
.ig-conn__msg {
  color: var(--el-text-color-secondary);
  word-break: break-all;
}
.ig-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  flex: none;
  background: var(--el-text-color-placeholder, #c0c4cc);
}
.ig-dot--connected {
  background: var(--el-color-success, #67c23a);
}
.ig-dot--connecting {
  background: var(--el-color-primary, #409eff);
}
.ig-dot--failed {
  background: var(--el-color-danger, #f56c6c);
}
.ig-dot--kicked {
  background: var(--el-color-warning, #e6a23c);
}
.ig-footer {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-top: 4px;
}
.ig-btn {
  height: 34px;
  padding: 0 18px;
  border-radius: var(--el-border-radius-base, 6px);
  border: 1px solid var(--el-border-color, #dcdfe6);
  background: var(--el-bg-color, #fff);
  color: var(--el-text-color-primary);
  font-size: 13px;
  cursor: pointer;
}
.ig-btn--primary {
  background: var(--el-color-primary, #409eff);
  border-color: var(--el-color-primary, #409eff);
  color: #fff;
}
.ig-btn--primary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}
.ig-btn--reconnect {
  margin-left: 4px;
  height: 26px;
  padding: 0 12px;
  font-size: 12px;
}
/* 扫码授权行（跨满两列栅格） */
.ig-scan-row {
  grid-column: 1 / -1;
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 2px;
}
.ig-btn--scan {
  height: 32px;
  padding: 0 16px;
  font-size: 13px;
  font-weight: 500;
  color: #fff;
  border-color: var(--el-color-primary, #409eff);
  background: var(--el-color-primary, #409eff);
}
.ig-btn--scan:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}
/* 扫码授权模态 */
.ig-modal {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.6);
  backdrop-filter: blur(4px);
  -webkit-backdrop-filter: blur(4px);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 2000;
}
.ig-modal__box {
  background: var(--el-bg-color, #fff);
  border-radius: 12px;
  border: 1px solid var(--el-border-color-lighter, #ebeef5);
  box-shadow: var(--el-box-shadow, 0 8px 32px rgba(0, 0, 0, 0.35));
  width: min(560px, calc(100vw - 40px));
  max-height: calc(100vh - 80px);
  display: flex;
  flex-direction: column;
  overflow: hidden;
}
.ig-modal__head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 14px 16px;
  border-bottom: 1px solid var(--el-border-color-lighter, #ebeef5);
  font-size: 15px;
  font-weight: 600;
}
.ig-btn--close {
  height: 24px;
  padding: 0 8px;
  border: none;
  background: transparent;
  font-size: 13px;
  color: var(--el-text-color-secondary);
}
.ig-modal__body {
  padding: 16px;
  overflow-y: auto;
  font-size: 13px;
  color: var(--el-text-color-regular);
}
.ig-scan-state {
  font-weight: 600;
  margin-bottom: 10px;
  color: var(--el-text-color-primary);
}
.ig-qr {
  margin: 0 auto 10px;
  padding: 12px;
  background: var(--el-fill-color-light, #f4f4f5);
  border-radius: var(--el-border-radius-base, 6px);
  font-family: var(--el-font-family-mono, monospace);
  font-size: 10px;
  line-height: 1;
  letter-spacing: 0;
  white-space: pre;
  text-align: center;
  color: var(--el-text-color-primary);
}
.ig-qr-link {
  display: block;
  margin-bottom: 10px;
  font-size: 12px;
  color: var(--el-color-primary, #409eff);
  word-break: break-all;
}
.ig-scan-tip {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}
.ig-scan-ok {
  padding: 10px;
  border-radius: var(--el-border-radius-base, 6px);
  background: var(--el-color-success-light-9, #e1f3d8);
  color: var(--el-color-success, #67c23a);
  font-size: 13px;
  word-break: break-all;
}
.ig-scan-err {
  padding: 10px;
  border-radius: var(--el-border-radius-base, 6px);
  background: var(--el-color-danger-light-9, #fef0f0);
  color: var(--el-color-danger, #f56c6c);
  font-size: 13px;
  word-break: break-all;
}
.ig-modal__foot {
  padding: 12px 16px;
  border-top: 1px solid var(--el-border-color-lighter, #ebeef5);
  display: flex;
  justify-content: flex-end;
}
.ig-hint {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}
/* 原生开关（无 EP 组件）：checkbox 隐藏，用 track 表现 */
.ig-switch {
  position: relative;
  display: inline-block;
  width: 44px;
  height: 24px;
  flex: none;
}
.ig-switch input {
  position: absolute;
  opacity: 0;
  width: 100%;
  height: 100%;
  margin: 0;
  cursor: pointer;
  /* 必须盖在 track 之上：track 是 absolute inset:0 且排在 input 之后，默认绘制在上层，
     会拦截指针事件 → 真实控件（checkbox）点不到（自动化/辅助技术失效）。
     提到上层后，点击开关任意位置都命中真实控件，视觉无变化（input 本身 opacity:0）。 */
  z-index: 1;
}
.ig-switch__track {
  position: absolute;
  inset: 0;
  background: var(--el-border-color, #dcdfe6);
  border-radius: 12px;
  transition: background 0.2s;
}
.ig-switch__track::after {
  content: '';
  position: absolute;
  top: 2px;
  left: 2px;
  width: 20px;
  height: 20px;
  background: #fff;
  border-radius: 50%;
  transition: transform 0.2s;
}
.ig-switch input:checked + .ig-switch__track {
  background: var(--el-color-primary, #409eff);
}
.ig-switch input:checked + .ig-switch__track::after {
  transform: translateX(20px);
}
</style>
