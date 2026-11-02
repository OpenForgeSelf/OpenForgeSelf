<script setup lang="ts">
/**
 * IM 网关插件根视图（ImGatewayView）—— v2.1.0 起双视图（PILOT-078）：
 * - 会话视图（默认）：左栏已绑定 IM 联系人列表（12s 轮询先比对再赋值防闪动），
 *   右栏消息线程（in=左气泡 / out=右气泡 / error 失败标记 + ErrorText 摘要 / beforeId 加载更多 / 12s 增量轮询）。
 * - 配置视图：v2.0.0 既有通道卡 + 扫码模态 + 保存逻辑原样迁移（功能一条不减）。
 *
 * 约束（plugin-development 铁律 §4）：插件是独立预编译产物，不能用 <ElXxx>，
 * 一律原生 HTML + --el-* / design token 变量（零裸色值）；滚动容器子区块 flex-shrink:0。
 */

import { nextTick, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import {
  fetchConfig, saveConfig, fetchStatus, reconnectWeCom, startScanAuth, fetchScanAuth, fetchPluginVersion,
  fetchConversations, fetchMessages,
  type ConversationDto, type ChatMessageDto, type ScanAuthDto,
} from './http'
import ChatList from './components/ChatList.vue'
import ChatThread from './components/ChatThread.vue'

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

// ============ 会话视图状态（PILOT-078） ============
/** 视图模式：chats=会话（默认）/ config=配置。 */
const viewMode = ref<'chats' | 'config'>('chats')

const conversations = ref<ConversationDto[]>([])
const conversationsLoading = ref(false)
/** 轮询/加载失败时的错误条（保旧数据，不闪空列表）。 */
const conversationsError = ref('')
const selectedKey = ref('')

const messages = ref<ChatMessageDto[]>([])
const threadHasMore = ref(false)
const threadLoading = ref(false)
const loadingMore = ref(false)
const threadError = ref('')
const threadRef = ref<InstanceType<typeof ChatThread> | null>(null)

const POLL_INTERVAL = 12_000
const PAGE_SIZE = 50
let pollTimer: ReturnType<typeof setInterval> | undefined

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

onMounted(() => {
  void load()
  void refreshConversations(false)
  startPolling()
})
onBeforeUnmount(() => {
  clearScanTimer()
  stopPolling()
})

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

// ============ 会话视图逻辑（PILOT-078） ============

/** 联系人显示名：接口给的，取不到时按 ChannelKey 末段兜底。 */
function displayNameOf(key: string): string {
  const c = conversations.value.find((x) => x.channelKey === key)
  if (c) return c.displayName
  const parts = key.split(':')
  return parts.length ? parts[parts.length - 1]! : key
}

/**
 * 刷新联系人列表（12s 轮询 + 首次加载共用）。
 * 先比对再赋值防闪动（todo-tracker 先例）；失败保旧数据仅更新错误条（轮询失败静默重试）。
 */
async function refreshConversations(silent: boolean): Promise<void> {
  if (!silent) conversationsLoading.value = true
  try {
    const list = await fetchConversations()
    if (list) {
      if (JSON.stringify(list) !== JSON.stringify(conversations.value)) conversations.value = list
      conversationsError.value = ''
      // 选中项已从映射消失（防御，正常只增不减）：复位线程
      if (selectedKey.value && !list.some((c) => c.channelKey === selectedKey.value)) {
        selectedKey.value = ''
        messages.value = []
        threadHasMore.value = false
      }
    }
  } catch (e) {
    conversationsError.value = `会话列表加载失败：${(e as Error).message}`
  } finally {
    if (!silent) conversationsLoading.value = false
  }
}

/** 选中联系人：清线程拉初始 50 条（同 key 不重复拉取 = 快速连点防抖）。 */
async function selectConversation(channelKey: string): Promise<void> {
  if (channelKey === selectedKey.value && (threadLoading.value || loadingMore.value || messages.value.length > 0)) return
  selectedKey.value = channelKey
  messages.value = []
  threadHasMore.value = false
  threadError.value = ''
  threadLoading.value = true
  try {
    const page = await fetchMessages(channelKey, PAGE_SIZE)
    if (selectedKey.value !== channelKey) return // 快速切换时丢弃过期响应
    if (page) {
      messages.value = page.messages
      threadHasMore.value = page.hasMore
    }
    await nextTick()
    threadRef.value?.scrollToBottom()
  } catch (e) {
    threadError.value = `消息加载失败：${(e as Error).message}`
  } finally {
    threadLoading.value = false
  }
}

/** 加载更多：以当前最旧 Id 为 beforeId 拉旧消息前插，保持视口位置（scrollHeight 差补偿）。 */
async function loadMore(): Promise<void> {
  const key = selectedKey.value
  const oldest = messages.value[0]
  if (!key || !oldest || loadingMore.value || !threadHasMore.value) return
  loadingMore.value = true
  const el = threadRef.value?.scrollEl ?? null
  const prevHeight = el?.scrollHeight ?? 0
  const prevTop = el?.scrollTop ?? 0
  try {
    const page = await fetchMessages(key, PAGE_SIZE, oldest.id)
    if (!page || selectedKey.value !== key) return
    const existing = new Set(messages.value.map((m) => m.id))
    const older = page.messages.filter((m) => !existing.has(m.id))
    if (older.length > 0) messages.value = [...older, ...messages.value]
    threadHasMore.value = page.hasMore
    await nextTick()
    // 视口位置保持：补偿新增内容撑高的部分
    if (el) el.scrollTop = el.scrollHeight - prevHeight + prevTop
  } catch (e) {
    threadError.value = `加载更多失败：${(e as Error).message}`
  } finally {
    loadingMore.value = false
  }
}

/**
 * 12s 轮询：列表全量比对赋值；线程仅追加比现有最大 Id 更新的消息（增量，不重绘全列表），
 * 且仅当用户已接近底部时自动滚底（不打扰向上翻阅）。
 */
function startPolling(): void {
  stopPolling()
  pollTimer = setInterval(() => {
    void refreshConversations(true)
    if (viewMode.value === 'chats' && selectedKey.value) void pollThread()
  }, POLL_INTERVAL)
}

function stopPolling(): void {
  if (pollTimer) {
    clearInterval(pollTimer)
    pollTimer = undefined
  }
}

/** 线程增量轮询：新消息按 id 追加，已存在的不动（DOM 节点复用，无闪动）。 */
async function pollThread(): Promise<void> {
  const key = selectedKey.value
  if (!key || threadLoading.value || loadingMore.value) return
  try {
    const page = await fetchMessages(key, PAGE_SIZE)
    if (!page || selectedKey.value !== key || page.messages.length === 0) return
    const maxId = messages.value.length ? messages.value[messages.value.length - 1]!.id : 0
    const fresh = page.messages.filter((m) => m.id > maxId)
    if (fresh.length === 0) return
    const wasNearBottom = threadRef.value?.isNearBottom() ?? true
    messages.value = [...messages.value, ...fresh]
    if (wasNearBottom) {
      await nextTick()
      threadRef.value?.scrollToBottom()
    }
  } catch {
    /* 轮询失败静默（保留旧数据） */
  }
}
</script>

<template>
  <div class="ig ig-root">
    <header class="ig-header">
      <div class="ig-eyebrow">IM Gateway · 即时通讯网关</div>
      <div class="ig-header__row">
        <div class="ig-title">IM 网关</div>
        <span v-if="pluginVersion" class="ig-version">v{{ pluginVersion }}</span>
        <span class="ig-header__spacer"></span>
        <button
          v-if="viewMode === 'chats'"
          type="button"
          class="ig-btn ig-header__config"
          @click="viewMode = 'config'"
        >
          配置
        </button>
        <button v-else type="button" class="ig-btn ig-header__back" @click="viewMode = 'chats'">
          返回会话
        </button>
      </div>
      <div class="ig-subtitle">企业微信「智能机器人」WebSocket 长连接，统一接入 AI Agent。</div>
    </header>

    <div v-if="message" class="ig-alert" :class="messageType === 'err' ? 'ig-alert--err' : 'ig-alert--ok'">
      {{ message }}
    </div>

    <!-- ============ 会话视图（默认） ============ -->
    <div v-show="viewMode === 'chats'" class="ig-chat">
      <aside class="ig-chat__side">
        <!-- 连接状态灯（FR-5：置于列表顶部，沿用 CONN_TEXT / canReconnect 逻辑） -->
        <div class="ig-conn ig-conn--side">
          <span class="ig-dot" :class="'ig-dot--' + (statusOf()?.connection?.state || 'disconnected')"></span>
          <span class="ig-conn__text">{{ connText() }}</span>
          <button v-if="canReconnect()" type="button" class="ig-btn ig-btn--reconnect" @click="reconnect">
            重连
          </button>
        </div>

        <div v-if="conversationsError" class="ig-chat__error">{{ conversationsError }}</div>

        <div v-if="conversationsLoading" class="ig-chat__hint">正在加载联系人…</div>

        <!-- 列表空态（全新库）：引导去配置 -->
        <div v-else-if="conversations.length === 0" class="ig-chat__empty">
          <div class="ig-chat__empty-title">还没有 IM 用户发来消息</div>
          <div class="ig-chat__empty-desc">启用企微通道后，用户发来的消息与 AI 回复会出现在这里。</div>
          <button type="button" class="ig-btn ig-btn--scan ig-chat__empty-btn" @click="viewMode = 'config'">
            去配置
          </button>
        </div>

        <!-- 联系人列表 -->
        <div v-else class="ig-chat__list">
          <ChatList :conversations="conversations" :selected-key="selectedKey" @select="selectConversation" />
        </div>
      </aside>

      <section class="ig-chat__main">
        <ChatThread
          v-if="selectedKey"
          ref="threadRef"
          :display-name="displayNameOf(selectedKey)"
          :messages="messages"
          :has-more="threadHasMore"
          :loading="threadLoading"
          :loading-more="loadingMore"
          :error="threadError"
          @load-more="loadMore"
        />
        <div v-else class="ig-chat__placeholder">
          <div class="ig-chat__placeholder-title">
            {{ conversations.length > 0 ? '从左侧选择一个联系人查看消息' : '通道收到消息后，这里会展示收到的与 AI 回复的内容' }}
          </div>
        </div>
      </section>
    </div>

    <!-- ============ 配置视图（v2.0.0 既有功能原样迁移） ============ -->
    <div v-show="viewMode === 'config'" class="ig-config">
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

      <footer class="ig-footer">
        <button class="ig-btn ig-btn--primary" :disabled="saving" @click="save">
          {{ saving ? '保存中…' : '保存配置' }}
        </button>
        <span class="ig-hint">保存后立即生效（改密钥会重建长连接）。</span>
      </footer>
    </div>

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
            <div class="ig-scan-tip">用手机企业微信扫一上方二维码，确认创建/授权机器人（勾选数据能力）。</div>
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
  </div>
</template>

<style>
/* 冻结模式：仅引用设计稿 token 与 --el-*（宿主级联），无裸色值、无 hex 回退。 */
.ig-root {
  font-family: var(--font-sans-cn, var(--el-font-family, system-ui, sans-serif));
  color: var(--fg-primary);
  padding: 20px;
  max-width: 1080px;
  margin: 0 auto;
  box-sizing: border-box;
  /* P0 视觉修复：根视图自带不透明主题底（浅=宣纸白 / 深=主题深底），盖住宿主背景图，
     页头/面板/气泡全部落在主题底上（宿主内容页 /ai-agent 先例 + DESIGN.md D-1 对比度条款） */
  background: var(--el-bg-color);
  /* v2.1.0 聊天式双栏：根视图占满高度，内部各视图自管滚动 */
  height: 100%;
  display: flex;
  flex-direction: column;
}
.ig-header {
  margin-bottom: 16px;
  flex-shrink: 0;
}
.ig-eyebrow {
  font-family: var(--font-mono, monospace);
  font-size: 10px;
  letter-spacing: 0.18em;
  text-transform: uppercase;
  color: var(--bronze-500);
  margin-bottom: 4px;
}
.ig-header__row {
  display: flex;
  align-items: baseline;
  gap: 10px;
}
.ig-header__spacer {
  flex: 1;
}
.ig-header__config,
.ig-header__back {
  height: 30px;
  padding: 0 14px;
  font-size: 12px;
  color: var(--fg-regular, var(--fg-secondary));
}
.ig-header__config:hover:not(:disabled),
.ig-header__back:hover:not(:disabled) {
  border-color: var(--gold-400);
  color: var(--gold-400);
}
.ig-version {
  font-family: var(--font-mono, monospace);
  font-size: 11px;
  padding: 1px 8px;
  border-radius: var(--radius-sm);
  background: var(--gold-tint);
  color: var(--bronze-500);
}
.ig-title {
  font-family: var(--font-display, serif);
  font-size: 26px;
  font-weight: 700;
  letter-spacing: 0.05em;
  line-height: 1.2;
  color: var(--fg-primary);
}
.ig-subtitle {
  margin-top: 6px;
  font-size: 13px;
  color: var(--fg-secondary);
  line-height: 1.5;
}
.ig-alert {
  padding: 10px 14px;
  border-radius: var(--radius-md);
  margin-bottom: 14px;
  font-size: 13px;
  flex-shrink: 0;
}
.ig-alert--ok {
  background: color-mix(in srgb, var(--bronze-500) 14%, transparent);
  color: var(--el-color-success);
  border: 1px solid color-mix(in srgb, var(--bronze-500) 32%, transparent);
}
.ig-alert--err {
  background: color-mix(in srgb, var(--cinnabar-400) 14%, transparent);
  color: var(--cinnabar-400);
  border: 1px solid color-mix(in srgb, var(--cinnabar-400) 32%, transparent);
}

/* ============ 会话视图（聊天式双栏） ============ */
.ig-chat {
  flex: 1;
  min-height: 0;
  display: flex;
  gap: 14px;
}
.ig-chat__side {
  flex: 0 0 300px;
  /* 长文本压力防 automatic minimum 撑破（60 字符 nowrap 预览 min-content ≈720px 逐级上传），
     见 B3 flex 尺寸双面铁律：外层 flex 子项必须 min-width:0，内层省略链才接得住 */
  min-width: 0;
  min-height: 0;
  display: flex;
  flex-direction: column;
  gap: 10px;
  /* P0：实底轻 tint 取代 40% 玻璃（浑浊灰根因），跟随主题浅/深两态 */
  background: var(--el-fill-color-light);
  border: 1px solid var(--el-border-color);
  border-radius: var(--glass-radius);
  box-shadow: var(--glass-shadow);
  padding: 12px;
  box-sizing: border-box;
}
.ig-conn--side {
  margin-top: 0;
  padding-bottom: 10px;
  border-bottom: 1px solid var(--el-border-color-lighter, var(--el-border-color));
  flex-shrink: 0;
}
.ig-chat__error {
  padding: 8px 10px;
  border-radius: var(--radius-md);
  background: color-mix(in srgb, var(--cinnabar-400) 14%, transparent);
  color: var(--cinnabar-400);
  font-size: 12px;
  flex-shrink: 0;
  word-break: break-all;
}
.ig-chat__hint {
  font-size: 12px;
  color: var(--fg-secondary);
  flex-shrink: 0;
}
.ig-chat__empty {
  margin: auto 0;
  text-align: center;
  padding: 24px 10px;
  flex-shrink: 0;
}
.ig-chat__empty-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--fg-secondary);
}
.ig-chat__empty-desc {
  margin-top: 6px;
  font-size: 12px;
  line-height: 1.6;
  color: var(--fg-secondary);
}
.ig-chat__empty-btn {
  margin-top: 12px;
}
/* 列表滚动壳：flex 列滚动容器，子区块 flex-shrink:0（铁律 8） */
.ig-chat__list {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
}
.ig-chat__list > * {
  flex-shrink: 0;
}
.ig-chat__main {
  flex: 1;
  /* 同 D1 铁律：1fr 子项同样钉死 automatic minimum，防对侧长文本/长 userId 撑破主栏 */
  min-width: 0;
  min-height: 0;
  background: var(--el-fill-color-light);
  border: 1px solid var(--el-border-color);
  border-radius: var(--glass-radius);
  box-shadow: var(--glass-shadow);
  overflow: hidden;
}
.ig-chat__placeholder {
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
}
.ig-chat__placeholder-title {
  font-size: 13px;
  color: var(--fg-secondary);
  padding: 0 20px;
  text-align: center;
  line-height: 1.6;
}

/* ============ 配置视图（原样迁移，滚动容器子区块 flex-shrink:0） ============ */
.ig-config {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
}
.ig-config > * {
  flex-shrink: 0;
}
.ig-channel {
  background: var(--el-fill-color-light);
  border: 1px solid var(--el-border-color);
  border-radius: var(--glass-radius);
  box-shadow: var(--glass-shadow);
  padding: 16px;
  margin-bottom: 14px;
}
.ig-channel__head {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 12px;
}
.ig-channel__name {
  font-family: var(--font-serif-cn, serif);
  font-size: 16px;
  font-weight: 600;
  color: var(--fg-primary);
  display: flex;
  align-items: center;
  gap: 8px;
}
.ig-channel__desc {
  font-size: 12px;
  color: var(--fg-secondary);
  margin-top: 2px;
}
.ig-badge {
  font-family: var(--font-mono, monospace);
  font-size: 11px;
  padding: 1px 8px;
  border-radius: var(--radius-sm);
  font-weight: 500;
}
.ig-badge--on {
  background: color-mix(in srgb, var(--bronze-500) 16%, transparent);
  color: var(--el-color-success);
}
.ig-badge--off {
  background: var(--paper-tint);
  color: var(--fg-secondary);
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
  color: var(--fg-regular, var(--fg-secondary));
}
.ig-field input,
.ig-bind input {
  height: 32px;
  padding: 0 10px;
  border: 1px solid var(--el-border-color);
  border-radius: var(--radius-md);
  background: var(--el-bg-color);
  color: var(--fg-primary);
  font-size: 13px;
  box-sizing: border-box;
  outline: none;
}
.ig-field input:focus,
.ig-bind input:focus {
  border-color: var(--gold-400);
  box-shadow: 0 0 0 3px var(--gold-tint);
}
/* 通道说明框（跨满两列栅格） */
.ig-note {
  grid-column: 1 / -1;
  font-size: 12px;
  line-height: 1.6;
  color: var(--fg-secondary);
  background: var(--paper-tint-weak);
  border-radius: var(--radius-md);
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
  color: var(--fg-secondary);
}
.ig-conn__msg {
  color: var(--fg-secondary);
  word-break: break-all;
}
.ig-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  flex: none;
  background: var(--fg-muted);
}
.ig-dot--connected {
  background: var(--bronze-500);
}
.ig-dot--connecting {
  background: var(--gold-400);
}
.ig-dot--failed {
  background: var(--cinnabar-400);
}
.ig-dot--kicked {
  background: var(--el-color-warning);
}
.ig-footer {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-top: 4px;
  padding-bottom: 4px;
}
.ig-btn {
  height: 34px;
  padding: 0 18px;
  border-radius: var(--radius-md);
  border: 1px solid var(--el-border-color);
  background: transparent;
  color: var(--fg-primary);
  font-size: 13px;
  cursor: pointer;
  transition: transform var(--dur-fast) var(--ease-out), background var(--dur-fast) var(--ease-out);
}
.ig-btn:hover:not(:disabled) { transform: translateY(-1px); }
.ig-btn--primary {
  background: var(--gold-400);
  border-color: var(--gold-400);
  color: var(--ink-950);
  font-weight: 500;
}
.ig-btn--primary:hover:not(:disabled) { background: var(--gold-300); }
.ig-btn--primary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
  transform: none;
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
  color: var(--ink-950);
  border-color: var(--gold-400);
  background: var(--gold-400);
}
.ig-btn--scan:hover:not(:disabled) { background: var(--gold-300); }
.ig-btn--scan:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}
/* 扫码授权模态 */
.ig-modal {
  position: fixed;
  inset: 0;
  background: color-mix(in srgb, var(--ink-950) 60%, transparent);
  backdrop-filter: blur(4px);
  -webkit-backdrop-filter: blur(4px);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 2000;
}
.ig-modal__box {
  background: var(--el-bg-color);
  border-radius: var(--radius-xl);
  border: 1px solid var(--el-border-color);
  box-shadow: var(--shadow-xl);
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
  border-bottom: 1px solid var(--el-border-color-lighter, var(--el-border-color));
  font-family: var(--font-serif-cn, serif);
  font-size: 15px;
  font-weight: 600;
  color: var(--fg-primary);
}
.ig-btn--close {
  height: 24px;
  padding: 0 8px;
  border: none;
  background: transparent;
  font-size: 13px;
  color: var(--fg-secondary);
}
.ig-modal__body {
  padding: 16px;
  overflow-y: auto;
  font-size: 13px;
  color: var(--fg-regular, var(--fg-secondary));
}
.ig-scan-state {
  font-weight: 600;
  margin-bottom: 10px;
  color: var(--fg-primary);
}
.ig-qr {
  margin: 0 auto 10px;
  padding: 12px;
  background: var(--paper-tint);
  border-radius: var(--radius-md);
  font-family: var(--font-mono, monospace);
  font-size: 10px;
  line-height: 1;
  letter-spacing: 0;
  white-space: pre;
  text-align: center;
  color: var(--fg-primary);
}
.ig-qr-link {
  display: block;
  margin-bottom: 10px;
  font-size: 12px;
  color: var(--gold-400);
  word-break: break-all;
}
.ig-scan-tip {
  font-size: 12px;
  color: var(--fg-secondary);
}
.ig-scan-ok {
  padding: 10px;
  border-radius: var(--radius-md);
  background: color-mix(in srgb, var(--bronze-500) 14%, transparent);
  color: var(--el-color-success);
  font-size: 13px;
  word-break: break-all;
}
.ig-scan-err {
  padding: 10px;
  border-radius: var(--radius-md);
  background: color-mix(in srgb, var(--cinnabar-400) 14%, transparent);
  color: var(--cinnabar-400);
  font-size: 13px;
  word-break: break-all;
}
.ig-modal__foot {
  padding: 12px 16px;
  border-top: 1px solid var(--el-border-color-lighter, var(--el-border-color));
  display: flex;
  justify-content: flex-end;
}
.ig-hint {
  font-size: 12px;
  color: var(--fg-secondary);
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
  background: var(--el-border-color);
  border-radius: var(--radius-full, 9999px);
  transition: background var(--dur-fast) var(--ease-out);
}
.ig-switch__track::after {
  content: '';
  position: absolute;
  top: 2px;
  left: 2px;
  width: 20px;
  height: 20px;
  background: var(--paper-white);
  border-radius: 50%;
  transition: transform var(--dur-fast) var(--ease-out);
}
.ig-switch input:checked + .ig-switch__track {
  background: var(--gold-400);
}
.ig-switch input:checked + .ig-switch__track::after {
  transform: translateX(20px);
}
</style>
