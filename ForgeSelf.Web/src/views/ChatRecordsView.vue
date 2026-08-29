<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import type { ChatSessionSummary, ChatSessionDetail, ChatSessionsResponse, ChatTurn } from '@/types/chatRecords'
import type { StreamMessageChunk } from '@/types/chat'
import { chatRecordsApi } from '@/services/chatRecordsApi'
import { wsService } from '@/services/websocket'
import ChatRecordsList from '@/components/chatrecords/ChatRecordsList.vue'
import ChatRecordDetail from '@/components/chatrecords/ChatRecordDetail.vue'

const source = ref('')
const clientKind = ref('')
const key = ref('')
const style = ref('')
const fromDate = ref('')
const toDate = ref('')
const page = ref(1)
const pageSize = ref(20)

const sessions = ref<ChatSessionsResponse['sessions']>([])
const total = ref(0)
const loading = ref(false)
const selectedSession = ref<ChatSessionSummary | null>(null)
const selectedTurns = ref<ChatTurn[]>([])
const detailLoading = ref(false)
const showDetail = ref(false)

// WebSocket 连接状态：连上时用推送驱动更新；未连接时仅手动查询
const wsConnected = ref(false)
const lastUpdatedAt = ref<Date | null>(null)

function formatTime(d: Date): string {
  return d.toLocaleTimeString('zh-CN', { hour12: false })
}

function formatDuration(ms: number): string {
  if (ms < 1000) return `${ms}ms`
  if (ms < 60000) return `${(ms / 1000).toFixed(2)}s`
  return `${(ms / 60000).toFixed(2)}分钟`
}

interface LiveStream {
  requestId: string
  sessionId: string
  text: string
  done: boolean
  error: boolean
  finishedAt: number
}

// 实时流式：来自后端的 chat_record_chunk / chat_record_completed 推送（扁平 payload，无 .data 包裹）
const liveStreams = ref<Record<string, LiveStream>>({})
const liveStreamList = computed(() => Object.values(liveStreams.value))
const hasLiveStreams = computed(() => liveStreamList.value.length > 0)

interface ChatRecordWsData {
  requestId: string
  sessionId: string
  text?: string
  error?: boolean
  recordId?: number
}

function handleWsMessage(msg: StreamMessageChunk): void {
  if (msg.type !== 'chat_record_chunk' && msg.type !== 'chat_record_completed') return
  // 后端 IWebSocketBroadcaster 广播的就是扁平对象本身，字段在 msg 顶层
  const data = msg as unknown as ChatRecordWsData
  if (!data || !data.requestId) return

  if (msg.type === 'chat_record_chunk') {
    const existing = liveStreams.value[data.requestId]
    if (existing) {
      existing.text += data.text || ''
      existing.done = false
    } else {
      liveStreams.value[data.requestId] = {
        requestId: data.requestId,
        sessionId: data.sessionId,
        text: data.text || '',
        done: false,
        error: false,
        finishedAt: 0
      }
    }
  } else {
    // chat_record_completed：定稿。标记 live 卡片并刷新会话列表（计数/新会话可能变化）。
    const existing = liveStreams.value[data.requestId]
    if (existing) {
      existing.done = true
      existing.error = !!data.error
      existing.finishedAt = Date.now()
    } else {
      liveStreams.value[data.requestId] = {
        requestId: data.requestId,
        sessionId: data.sessionId,
        text: '',
        done: true,
        error: !!data.error,
        finishedAt: Date.now()
      }
    }
    void fetchSessions()
    const rid = data.requestId
    window.setTimeout(() => {
      const cur = liveStreams.value[rid]
      if (cur && cur.done && !cur.error) delete liveStreams.value[rid]
    }, 8000)
  }
}

const sourceOptions = [
  { value: '', label: '全部' },
  { value: 'App', label: 'App 自有聊天' },
  { value: 'Proxy', label: '代理录制' }
]

const styleOptions = [
  { value: '', label: '全部' },
  { value: 'OpenAI_Chat', label: 'OpenAI Chat' },
  { value: 'OpenAI_Responses', label: 'OpenAI Responses' },
  { value: 'Anthropic_Messages', label: 'Anthropic Messages' },
  { value: 'AppChat', label: 'App Chat' }
]

async function fetchSessions() {
  loading.value = true
  try {
    const response = await chatRecordsApi.getSessions({
      source: source.value || undefined,
      clientKind: clientKind.value || undefined,
      style: style.value || undefined,
      from: fromDate.value || undefined,
      to: toDate.value || undefined,
      key: key.value || undefined,
      page: page.value,
      pageSize: pageSize.value
    })
    sessions.value = response.sessions
    total.value = response.total
  } catch (err) {
    console.error('获取会话列表失败:', err)
  } finally {
    loading.value = false
    lastUpdatedAt.value = new Date()
  }
}

async function handleViewDetail(id: number) {
  detailLoading.value = true
  showDetail.value = true
  try {
    const detail: ChatSessionDetail = await chatRecordsApi.getSessionById(id)
    selectedSession.value = detail.session
    selectedTurns.value = detail.turns
  } catch (err) {
    console.error('获取会话详情失败:', err)
  } finally {
    detailLoading.value = false
  }
}

function handlePageChange(newPage: number) {
  page.value = newPage
  fetchSessions()
}

function handleSearch() {
  page.value = 1
  fetchSessions()
}

function handleReset() {
  source.value = ''
  clientKind.value = ''
  key.value = ''
  style.value = ''
  fromDate.value = ''
  toDate.value = ''
  page.value = 1
  fetchSessions()
}

onMounted(() => {
  // 首次一次性加载历史会话（非轮询）；之后靠 WS 推送增量更新
  fetchSessions()
  // 注册 WS 连接状态回调：实时推送
  wsService.connect({
    onOpen: () => {
      wsConnected.value = true
    },
    onClose: () => {
      wsConnected.value = false
    },
    onError: () => {
      wsConnected.value = false
    }
  })
  wsService.subscribe(handleWsMessage)
})

onUnmounted(() => {
  wsService.unsubscribe(handleWsMessage)
})
</script>

<template>
  <div class="chat-records-view">
    <header class="view-header">
      <h1 class="view-title">
        <span class="title-icon">💬</span>
        聊天会话查看
      </h1>
      <p class="view-subtitle">以「会话」维度查看 API 调用与 App 自有聊天，会话下按轮次（ChatTurn）展开明细</p>
      <div class="live-indicator">
        <span class="live-dot" :class="{ active: wsConnected }" />
        <span class="live-text">
          {{ wsConnected ? 'WebSocket 实时推送中' : '未连接（仅手动查询）' }}
        </span>
        <span v-if="lastUpdatedAt" class="live-time">更新于 {{ formatTime(lastUpdatedAt) }}</span>
      </div>
    </header>

    <div v-if="hasLiveStreams" class="live-streams">
      <div class="live-streams-head">
        <span class="live-streams-title">⚡ 实时流式响应</span>
        <span class="live-streams-count">{{ liveStreamList.length }} 路进行中</span>
      </div>
      <div v-for="ls in liveStreamList" :key="ls.requestId" class="live-stream-card">
        <div class="live-stream-meta">
          <span class="live-badge" :class="{ done: ls.done, error: ls.error }">
            {{ ls.error ? '失败' : ls.done ? '完成' : '进行中' }}
          </span>
          <span class="live-rid">请求 {{ ls.requestId.slice(0, 8) }}</span>
        </div>
        <p class="live-text-preview">{{ ls.text || '（等待内容…）' }}</p>
      </div>
    </div>

    <div class="filter-bar">
      <div class="filter-row">
        <div class="filter-item">
          <label class="filter-label">来源</label>
          <select v-model="source" class="filter-select">
            <option v-for="opt in sourceOptions" :key="opt.value" :value="opt.value">
              {{ opt.label }}
            </option>
          </select>
        </div>

        <div class="filter-item">
          <label class="filter-label">客户端</label>
          <input
            v-model="clientKind"
            type="text"
            class="filter-input"
            placeholder="客户端类型"
            @keyup.enter="handleSearch"
          />
        </div>

        <div class="filter-item">
          <label class="filter-label">会话键</label>
          <input
            v-model="key"
            type="text"
            class="filter-input"
            placeholder="输入会话键"
            @keyup.enter="handleSearch"
          />
        </div>

        <div class="filter-item">
          <label class="filter-label">Style</label>
          <select v-model="style" class="filter-select">
            <option v-for="opt in styleOptions" :key="opt.value" :value="opt.value">
              {{ opt.label }}
            </option>
          </select>
        </div>

        <div class="filter-item">
          <label class="filter-label">开始日期</label>
          <input
            v-model="fromDate"
            type="date"
            class="filter-input"
          />
        </div>

        <div class="filter-item">
          <label class="filter-label">结束日期</label>
          <input
            v-model="toDate"
            type="date"
            class="filter-input"
          />
        </div>
      </div>

      <div class="filter-actions">
        <button class="search-btn" @click="handleSearch">
          <i class="fa-solid fa-search" />
          搜索
        </button>
        <button class="reset-btn" @click="handleReset">
          <i class="fa-solid fa-rotate-left" />
          重置
        </button>
      </div>
    </div>

    <div class="content-area">
      <div class="list-section">
        <ChatRecordsList
          :records="sessions"
          :total="total"
          :page="page"
          :page-size="pageSize"
          :loading="loading"
          @view-detail="handleViewDetail"
          @page-change="handlePageChange"
        />
      </div>
    </div>

    <!-- 详情改为弹窗展示：会话头 + 轮次明细 -->
    <ElDialog
      v-model="showDetail"
      :title="`会话详情 #${selectedSession?.id ?? ''}`"
      width="82%"
      top="4vh"
      class="record-detail-dialog"
      append-to-body
    >
      <div class="dialog-body">
        <div v-if="detailLoading" class="detail-loading">
          <div class="spinner" />
          <span>加载中...</span>
        </div>
        <template v-else-if="selectedSession">
          <div class="session-head">
            <div class="session-meta">
              <span><b>来源</b> {{ selectedSession.source }}</span>
              <span><b>模型</b> {{ selectedSession.model || '—' }}</span>
              <span><b>风格</b> {{ selectedSession.style || '—' }}</span>
              <span><b>客户端</b> {{ selectedSession.clientKind }}</span>
              <span><b>轮次</b> {{ selectedSession.requestCount }}</span>
              <span><b>消息</b> {{ selectedSession.messageCount }}</span>
              <span><b>末态</b> {{ selectedSession.lastStatus }}</span>
            </div>
            <div class="session-key"><b>会话键</b> {{ selectedSession.sessionKey }}</div>
            <div v-if="selectedSession.firstUserMsg" class="session-first">「{{ selectedSession.firstUserMsg }}」</div>
          </div>
          <div class="turns-list">
            <div v-for="turn in selectedTurns" :key="turn.id" class="turn-block">
              <div class="turn-head">
                轮次 #{{ turn.turnIndex }} · 模型 {{ turn.model || '—' }} · HTTP {{ turn.responseStatus }} · {{ formatDuration(turn.durationMs) }}
              </div>
              <ChatRecordDetail :record="turn" />
            </div>
            <div v-if="selectedTurns.length === 0" class="empty-hint">该会话暂无轮次明细</div>
          </div>
        </template>
      </div>
    </ElDialog>
  </div>
</template>

<style scoped>
.chat-records-view {
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding: 20px;
  height: 100%;
  overflow: hidden;
  box-sizing: border-box;
}

.view-header {
  flex-shrink: 0;
}

.view-title {
  font-size: 24px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 0 0 4px 0;
  display: flex;
  align-items: center;
  gap: 8px;
}

.title-icon {
  font-size: 28px;
}

.view-subtitle {
  font-size: 14px;
  color: var(--el-text-color-secondary);
  margin: 0;
}

.live-indicator {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-top: 10px;
  font-size: 13px;
  color: var(--el-text-color-secondary);
}

.live-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--el-text-color-disabled);
  transition: background-color 150ms ease;
}

.live-dot.active {
  background: var(--el-color-success);
  box-shadow: 0 0 0 3px color-mix(in srgb, var(--el-color-success) 25%, transparent);
  animation: pulse 1.6s ease-in-out infinite;
}

.live-text {
  font-weight: 500;
  color: var(--el-text-color-regular);
}

.live-time {
  font-variant-numeric: tabular-nums;
  color: var(--el-text-color-secondary);
}

@keyframes pulse {
  0%,
  100% {
    opacity: 1;
  }
  50% {
    opacity: 0.4;
  }
}

.live-streams {
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 12px 16px;
  background: var(--el-bg-color-page);
  border: 1px solid var(--el-border-color);
  border-radius: 8px;
}

.live-streams-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.live-streams-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}

.live-streams-count {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.live-stream-card {
  padding: 10px 12px;
  background: var(--el-bg-color);
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
}

.live-stream-meta {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 6px;
}

.live-badge {
  padding: 2px 8px;
  border-radius: 4px;
  font-size: 12px;
  font-weight: 500;
  color: var(--el-color-primary);
  background: color-mix(in srgb, var(--el-color-primary) 12%, transparent);
}

.live-badge.done {
  color: var(--el-color-success);
  background: color-mix(in srgb, var(--el-color-success) 12%, transparent);
}

.live-badge.error {
  color: var(--el-color-danger);
  background: color-mix(in srgb, var(--el-color-danger) 12%, transparent);
}

.live-rid {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  font-variant-numeric: tabular-nums;
}

.live-text-preview {
  margin: 0;
  font-size: 13px;
  line-height: 1.6;
  color: var(--el-text-color-regular);
  white-space: pre-wrap;
  word-break: break-word;
  max-height: 120px;
  overflow: hidden;
}

.filter-bar {
  flex-shrink: 0;
  padding: 16px;
  background: var(--el-bg-color-page);
  border-radius: 8px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.filter-row {
  display: flex;
  flex-wrap: wrap;
  gap: 16px;
}

.filter-item {
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-width: 180px;
}

.filter-label {
  font-size: 12px;
  font-weight: 500;
  color: var(--el-text-color-secondary);
}

.filter-input,
.filter-select {
  padding: 8px 12px;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  font-size: 14px;
  background: var(--el-bg-color);
}

.filter-input:focus,
.filter-select:focus {
  outline: none;
  border-color: var(--el-color-primary);
}

.filter-actions {
  display: flex;
  gap: 8px;
}

.search-btn,
.reset-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 8px 16px;
  border: none;
  border-radius: 6px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s ease;
}

.search-btn {
  background: var(--el-color-primary);
  color: var(--el-color-white);
}

.search-btn:hover {
  background: var(--el-color-primary-light-3);
}

.reset-btn {
  background: var(--el-text-color-secondary);
  color: var(--el-color-white);
}

.reset-btn:hover {
  opacity: 0.85;
}

.content-area {
  flex: 1;
  display: flex;
  gap: 16px;
  min-height: 0;
  overflow: hidden;
}

.list-section {
  flex: 1;
  min-width: 0;
  overflow: hidden;
}

.dialog-body {
  height: 72vh;
  min-height: 0;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.detail-loading {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 12px;
  height: 100%;
  color: var(--el-text-color-secondary);
}

.spinner {
  width: 32px;
  height: 32px;
  border: 3px solid var(--el-border-color);
  border-top-color: var(--el-color-primary);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

/* 会话头 */
.session-head {
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 12px 14px;
  background: var(--el-bg-color-page);
  border: 1px solid var(--el-border-color);
  border-radius: 8px;
}
.session-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 14px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}
.session-meta b { color: var(--el-text-color-regular); font-weight: 600; margin-right: 4px; }
.session-key {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  font-family: 'Monaco', 'Menlo', monospace;
  word-break: break-all;
}
.session-key b { color: var(--el-text-color-regular); font-weight: 600; margin-right: 4px; }
.session-first {
  font-size: 13px;
  color: var(--el-text-color-regular);
}

/* 轮次明细 */
.turns-list {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 14px;
}
.turn-block {
  display: flex;
  flex-direction: column;
  gap: 6px;
  border: 1px solid var(--el-border-color);
  border-radius: 8px;
  padding: 10px 12px;
}
.turn-head {
  font-size: 12px;
  font-weight: 600;
  color: var(--el-text-color-regular);
  padding-bottom: 6px;
  border-bottom: 1px solid var(--el-border-color-lighter);
}
.empty-hint {
  font-size: 13px;
  color: var(--el-text-color-secondary);
  text-align: center;
  padding: 24px 0;
}
</style>
