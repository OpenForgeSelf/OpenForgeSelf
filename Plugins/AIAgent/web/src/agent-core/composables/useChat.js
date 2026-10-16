import { ref, computed, onBeforeUnmount } from 'vue'
import { resolveApiUrl } from '../config'
import { normalizeMessage } from '../utils/schema'
import { createAudit, auditBlocks } from '../utils/audit'
import { getAuth } from '../utils/auth'
import { createResumeFilter } from '../utils/resume'
import { readStore, upsert, removeById, serialize, makeSession, formatWhen, mergeStores, STORE_SUFFIX, DEFAULT_MAX } from '../utils/sessions'
import { sessionToMarkdown, sessionsToMarkdown } from '../utils/export'

let seq = 0
const uid = () => `m${Date.now().toString(36)}_${(seq++).toString(36)}`
const sleep = (ms) => new Promise((r) => setTimeout(r, ms))

/** 从各种常见响应体里取出文本 */
function extractText(data) {
  if (data == null) return ''
  if (typeof data === 'string') return data
  return (
    data.choices?.[0]?.message?.content ??
    data.choices?.[0]?.delta?.content ??
    data.content ??
    data.reply ??
    data.message ??
    data.answer ??
    data.data?.content ??
    ''
  )
}

const MOCK_RULES = [
  [/(收费|价格|报价|多少钱|费用)/, '我们提供三个档位：\n- **基础版** ¥0 —— 单站点、每天 200 次对话\n- **专业版** ¥199/月 —— 不限站点、知识库、流式输出\n- **企业版** 按需报价 —— 私有化部署 + 专属模型\n\n需要我帮你估算一下用量吗？'],
  [/(接入|集成|嵌入|安装|怎么用|使用|部署)/, '接入只要三步：\n1. 构建后在页面里引入 `ai-assistant.js`\n2. 用 `<script data-auto-init>` 或 `AIAssistant.init({...})` 初始化\n3. 把 `baseUrl` 指向你自己的后端\n\n组件跑在 Shadow DOM 里，不会污染宿主页面的样式。'],
  [/(流式|stream|打字机)/, '支持。把 `stream: true` 打开，组件会用 `fetch` 读取 `text/event-stream`，逐字渲染，并兼容 OpenAI 的 `choices[0].delta.content` 格式。'],
  [/(vue|版本|框架|react)/, '这是 Vue 3 组件，构建产物是一个自包含的 IIFE 文件，Vue 已经打包进去了，所以**宿主页面不需要装 Vue**，React / 原生页面一样能用。'],
  [/(你好|hi|hello|在吗|嗨)/, '在的，随时可以问 🙂 你可以先试试下面的常见问题。'],
]

export function useChat(config) {
  const messages = ref([])
  const sending = ref(false)
  const streamingText = ref('')
  const error = ref('')
  const retrying = ref(null) // { attempt, total, delay } | null
  // Q-09：断流续传中。与 retrying 分开——「重连试试」和「补上剩下的」是两件事
  const reconnecting = ref(null) // { attempt, total } | null
  const lastUserText = ref('')
  const fellBackToMock = ref(false) // FR-508：本条回复是否由降级产生
  // FR-803：最近一次「数据到达」的时间。push() 与收到回复都会刷新它，
  // 大屏据此展示「数据 X 分钟前」——过期数据没人知道比数据慢更危险
  const lastDataAt = ref(Date.now())
  // v1.8.0 集成扩展：最近一次回复的 token 用量（transport onUsage 填充；AIAgent 左栏统计用）
  const lastUsage = ref(null)
  // v1.6 审计：默认关闭。开着必带性能与隐私成本，由宿主显式决定
  const audit = createAudit(config)

  let controller = null
  let timer = null
  let userAborted = false
  let timedOutKind = ''

  const canRetry = computed(() => !!lastUserText.value && !sending.value)

  /* ---------------- 会话历史（v1.12.0） ----------------
     store 是**非响应式**的完整数据（含每段 messages）；sessions 是它的元数据投影，
     只放侧栏要显示的字段 —— 没必要把整段对话复制一份进响应式系统。 */
  const sessions = ref([])
  const currentId = ref('')
  let store = { sessions: [], current: null }
  const historyOn = () => !!(config.history && config.history.enabled)
  const maxSessions = () => (config.history && config.history.max) || DEFAULT_MAX
  const storeKey = () => config.storageKey + STORE_SUFFIX

  /** 把 store 投影成侧栏元数据 */
  function syncMeta() {
    sessions.value = store.sessions.map((s) => ({
      id: s.id,
      title: s.title,
      createdAt: s.createdAt,
      updatedAt: s.updatedAt,
      count: (s.messages || []).filter((m) => m && !m.welcome).length,
    }))
    currentId.value = store.current || ''
  }

  /** 保证有一个当前会话（首次 seed、或删空之后都要用） */
  function ensureCurrent() {
    if (!store.current) {
      const s = makeSession(uid(), Date.now(), [])
      store.sessions = upsert(store.sessions, s, maxSessions())
      store.current = s.id
      /* 建完必须把投影刷一遍：调用方（seed/reset）不一定会再调 syncMeta，
         少这一次刷新，侧栏就一直显示空列表。 */
      syncMeta()
    }
    return store.current
  }

  /* ---------- FR-219 多标签页会话同步 ----------
     没监听 storage 时，两个标签页同时开着同一份历史会互相覆盖（最后写的赢）——
     那是真丢数据。这里在收到另一个标签页的写入后**合并**，而不是各写各的。
     合并结果与本地一致就不回写：否则两个标签页会互相触发、无限对写。 */
  function parseIncoming(raw) {
    try {
      const v = JSON.parse(raw)
      if (v && Array.isArray(v.sessions)) return { sessions: v.sessions, current: v.current }
    } catch (e) {
      /* 对方写坏了也不能拖垮本页 */
    }
    return null
  }

  function onStorage(e) {
    if (!historyOn()) return
    if (config.history && config.history.syncTabs === false) return
    if (!e || e.key !== storeKey() || !e.newValue) return
    const incoming = parseIncoming(e.newValue)
    if (!incoming) return
    const merged = mergeStores(store, incoming, maxSessions())
    const before = store.sessions.map((s) => s.id + ':' + (s.updatedAt || 0)).join(',')
    const after = merged.sessions.map((s) => s.id + ':' + (s.updatedAt || 0)).join(',')
    if (before === after && merged.current === store.current) return
    const prevCurrent = store.current
    store = { sessions: merged.sessions, current: merged.current }
    syncMeta()
    audit.emit('session:sync', { total: store.sessions.length, switched: merged.current !== prevCurrent })
    /* 当前会话被别的标签页切成了另一个才跟着切；正文不动 ——
       本页可能正在进行对话，抢过来会打断用户。 */
    if (merged.current && merged.current !== prevCurrent) load(merged.current)
  }

  if (typeof window !== 'undefined' && window.addEventListener) {
    window.addEventListener('storage', onStorage)
    onBeforeUnmount(() => window.removeEventListener('storage', onStorage))
  }

  /** FR-220：导出为 Markdown 文本（不落盘、不触发下载 —— 由调用方决定怎么用）
      以**用户看到的内容**为准：会话 store 还没建起来时（例如未开历史、
      或 persist 关掉导致没落盘），就导出当前视图里的消息，而不是回一个空字符串。 */
  function exportSession(id) {
    const target = id || store.current
    const entry = store.sessions.find((s) => s.id === target)
    if (entry) return sessionToMarkdown(entry)
    const live = messages.value.filter((m) => m && !m.welcome)
    if (!live.length) return ''
    return sessionToMarkdown({ title: '当前会话', messages: live })
  }
  function exportAllSessions() {
    if (store.sessions.length) return sessionsToMarkdown(store.sessions)
    return exportSession()
  }

  function persistStore() {
    try {
      localStorage.setItem(storeKey(), serialize(store.sessions, store.current))
    } catch (e) {
      /* 配额满了也不能让组件挂掉：内存里的会话仍然可用 */
    }
  }

  /** 把某个会话的消息装载进当前视图（没有消息就补一条欢迎语） */
  function applySession(id) {
    const s = store.sessions.find((x) => x.id === id)
    if (s && s.messages && s.messages.length) {
      messages.value = s.messages.map((m) => normalizeMessage(m).message)
    } else {
      /* ★ 宿主没配欢迎语时**保持空列表**，不要塞一条 content 为空的消息：
         ① 那会在消息流里留一条空气泡（没内容却占着一条的位置）；
         ② 更要紧的是它让「空状态」永远算不出空来 ——
            组件源那套默认欢迎语是空会话的兜底，宿主没配时兜底没了，
            再叠一条空气泡，用户看到的就是一块纯白。 */
      messages.value = config.welcome
        ? [{ id: uid(), role: 'assistant', content: config.welcome, time: Date.now(), welcome: true }]
        : []
    }
    error.value = ''
    streamingText.value = ''
    retrying.value = null
    reconnecting.value = null
  }

  /** 新开一个会话（保留已有的） */
  function newSession() {
    if (!historyOn()) return false
    save()
    store.current = ''
    ensureCurrent()
    applySession(store.current)
    syncMeta()
    persistStore()
    audit.emit('session:new', { id: store.current, total: store.sessions.length })
    return true
  }

  function switchSession(id) {
    if (!historyOn()) return false
    const target = String(id || '')
    if (!store.sessions.some((s) => s.id === target)) return false
    if (store.current === target) return true
    save() // 先把当前会话落盘，别丢
    store.current = target
    applySession(target)
    syncMeta()
    persistStore()
    audit.emit('session:switch', { id: target })
    return true
  }

  function removeSession(id) {
    if (!historyOn()) return false
    const target = String(id || '')
    const before = store.sessions.length
    store.sessions = removeById(store.sessions, target, maxSessions())
    if (store.sessions.length === before) return false
    audit.emit('session:remove', { id: target, total: store.sessions.length })
    if (store.current === target) {
      // 删掉的正是当前会话：回落到剩下的第一个；一个都不剩就自动新建一个
      store.current = store.sessions[0] ? store.sessions[0].id : ''
      if (!store.current) ensureCurrent()
      applySession(store.current)
    }
    syncMeta()
    persistStore()
    return true
  }

  function renameSession(id, title) {
    if (!historyOn()) return false
    const s = store.sessions.find((x) => x.id === String(id || ''))
    if (!s) return false
    const t = String(title || '').replace(/\s+/g, ' ').trim()
    if (!t) return false
    s.title = t.slice(0, 60)
    s.titleLocked = true // 手改过就不再被首条消息覆盖
    syncMeta()
    persistStore()
    audit.emit('session:rename', { id: s.id })
    return true
  }

  /** 抹掉全部会话（隐私）。不可撤销，界面上必须二次确认 */
  function clearSessions() {
    if (!historyOn()) return false
    store = { sessions: [], current: null }
    sessions.value = []
    currentId.value = ''
    try {
      localStorage.removeItem(storeKey())
    } catch (e) {
      /* noop */
    }
    audit.emit('session:clear', {})
    reset()
    return true
  }

  /* ---------------- 持久化 ---------------- */
  function load() {
    if (!config.persist) return false

    if (historyOn()) {
      let parsed = { sessions: [], current: null, migrated: false, issues: [] }
      try {
        parsed = readStore(localStorage.getItem(storeKey()), maxSessions())
      } catch (e) {
        /* 存储不可用：当作空历史 */
      }
      // 迁移：老用户只有那份裸数组（v1.11 及以前的单会话）
      if (!parsed.sessions.length) {
        let legacy = null
        try {
          legacy = localStorage.getItem(config.storageKey)
        } catch (e) {
          legacy = null
        }
        if (legacy) parsed = readStore(legacy, maxSessions())
      }
      if (parsed.migrated && config.debug) {
        console.warn('[ai-assistant] 已把旧的单会话数据迁移为会话历史')
      }
      store = { sessions: parsed.sessions, current: parsed.current }
      syncMeta()
      const cur = store.sessions.find((s) => s.id === store.current)
      if (cur && cur.messages.length) {
        // 旧数据可能不符合当前协议（字段改名 / 类型漂移），统一走一次校验降级（Q-05）
        messages.value = cur.messages.map((m) => normalizeMessage(m).message)
        return true
      }
      return false
    }

    try {
      const raw = localStorage.getItem(config.storageKey)
      if (!raw) return false
      const arr = JSON.parse(raw)
      if (Array.isArray(arr) && arr.length) {
        messages.value = arr.map((m) => normalizeMessage(m).message)
        return true
      }
    } catch (e) {
      /* 存储不可用时静默降级 */
    }
    return false
  }

  function save() {
    /* ★ 教训（v1.12.0 真浏览器取证发现）：
       原来第一行是 `if (!config.persist) return`，把**内存里的会话记账**与
       「写不写 localStorage」当成了一回事。于是 persist:false + history 开启时，
       侧栏永远停在空列表 —— 会话建了、消息推了，投影却一次都没刷新，
       当前会话项自然也没有 aria-current（真浏览器 HIST-03 失败）。
       正确切分：historyOn() 时记账**总是**做（这是内存状态），只有落盘受 persist 管。 */
    if (historyOn()) {
      const id = ensureCurrent()
      const entry = store.sessions.find((s) => s.id === id)
      if (entry) {
        entry.messages = messages.value.slice(-config.maxHistory)
        entry.updatedAt = Date.now()
        // 标题跟着首条用户消息走；被 renameSession 锁定过就不再覆盖
        if (!entry.titleLocked) entry.title = makeSession(entry.id, 0, entry.messages).title
      }
      store.sessions = upsert(store.sessions, entry || makeSession(id, Date.now(), messages.value.slice(-config.maxHistory)), maxSessions())
      store.current = id
      syncMeta()
      if (config.persist) persistStore()
      return
    }

    if (!config.persist) return
    try {
      localStorage.setItem(config.storageKey, JSON.stringify(messages.value.slice(-config.maxHistory)))
    } catch (e) {
      /* 忽略配额错误 */
    }
  }

  function seed() {
    startExpiryWatch()
    // 同上（applySession）：没配欢迎语就保持空列表，别塞空气泡。
    messages.value = config.welcome
      ? [{ id: uid(), role: 'assistant', content: config.welcome, time: Date.now(), welcome: true }]
      : []
    if (historyOn()) ensureCurrent()
    save()
  }

  function reset() {
    abort()
    messages.value = []
    error.value = ''
    streamingText.value = ''
    retrying.value = null
    reconnecting.value = null

    if (historyOn()) {
      // 开了历史时「清空对话」= **新开一个会话**，旧会话留在侧栏里 ——
      // 这正是开历史的意义。要真的全删，用 clearSessions()。
      store.current = ''
      ensureCurrent()
      syncMeta()
      seed()
      return
    }

    try {
      localStorage.removeItem(config.storageKey)
    } catch (e) {
      /* noop */
    }
    seed()
  }

  /* ---------------- 超时 / 重试工具 ---------------- */
  function clearTimer() {
    if (timer) {
      clearTimeout(timer)
      timer = null
    }
  }

  const retryCfg = () => config.retry || {}
  const reconnectCfg = () => config.reconnect || {}

  /** 续传退避：抖动同样是防止一堆大屏同时回来把后端打垮 */
  function reBackoff(attempt) {
    const rc = reconnectCfg()
    const base = Math.min((rc.baseDelay || 800) * Math.pow(2, attempt - 1), rc.maxDelay || 8000)
    return Math.round(base * (0.7 + Math.random() * 0.6))
  }

  /** 只有「网络层失败 / 超时 / 服务端 5xx / 限流」才值得重试，4xx 重试是浪费 */
  function isRetryableError(e) {
    if (!e) return false
    if (e.name === 'TimeoutError') return true
    if (e.name === 'TypeError') return true // fetch 网络层失败：断网 / DNS / 跨域
    const m = /HTTP (\d{3})/.exec(e.message || '')
    if (m) return (retryCfg().statuses || []).includes(Number(m[1]))
    return false
  }

  /** 指数退避 + 抖动：抖动是为了避免多个客户端同时重试把后端打垮 */
  function backoff(attempt) {
    const base = Math.min((retryCfg().baseDelay || 600) * Math.pow(2, attempt), retryCfg().maxDelay || 5000)
    return Math.round(base * (0.7 + Math.random() * 0.6))
  }

  /* ---------------- 请求 ---------------- */
  function buildPayload() {
    /* 失败/中断的消息**不喂回给模型**：它们是断掉的上下文，喂进去只会让模型的下一句
       接着一个不存在的半截回复往下编。这里同时认新旧两种写法 ——
       `status:'failed'` 是当前契约，`failed:true` 是这之前留下的同义拼写
       （它只有这一处读、没有任何地方写过，等于半个契约，现在收成一处）。 */
    const history = messages.value
      .filter((m) => !m.welcome && m.status !== 'failed' && m.status !== 'stopped' && !m.failed)
      .slice(-config.maxHistory)
      .map((m) => ({ role: m.role, content: m.content }))
    const payload = {
      model: config.model,
      messages: [{ role: 'system', content: config.systemPrompt }, ...history],
      temperature: config.temperature,
      stream: !!config.stream,
    }
    if (config.maxTokens) payload.max_tokens = config.maxTokens
    return payload
  }

  /**
   * 两级超时：
   *   timeout      —— 首字节超时（发出请求到拿到响应头）
   *   idleTimeout  —— 流空闲超时（两块数据之间的最大间隔）
   * 只设首字节超时挡不住「连上了但永远不吐字」的服务端，只设空闲超时又挡不住连接挂死，
   * 所以两级都要。两者都基于同一个 AbortController，用 timedOutKind 区分是谁触发的。
   */
  async function requestStream(payload, onDelta, opts = {}) {
    controller = new AbortController()
    userAborted = false
    timedOutKind = ''

    const firstByteMs = Number(config.timeout) || 0
    const idleMs = Number(config.idleTimeout) || 0

    const arm = (kind, ms) => {
      clearTimer()
      if (ms > 0) {
        timer = setTimeout(() => {
          timedOutKind = kind
          try {
            controller.abort()
          } catch (e) {
            /* noop */
          }
        }, ms)
      }
    }

    const headers = { 'Content-Type': 'application/json', ...config.headers }
    // Q-09 续传游标：告诉后端「我收到这里了」。后端支持就接着推，不支持也不会因此报错
    if (opts.lastEventId) headers['Last-Event-ID'] = String(opts.lastEventId)
    if (config.apiKey) {
      const scheme = config.authScheme === undefined ? 'Bearer ' : config.authScheme
      headers[config.authHeader || 'Authorization'] = scheme + config.apiKey
    }

    const requestUrl = resolveApiUrl(config)
    if (config.debug) console.log('[ai-assistant] → 请求', requestUrl, payload)

    arm('first', firstByteMs)

    try {
      const res = await fetch(requestUrl, {
        method: 'POST',
        headers,
        body: JSON.stringify(payload),
        signal: controller.signal,
      })
      clearTimer()
      if (config.debug) console.log('[ai-assistant] ← 响应', res.status, res.headers.get('content-type') || '')
      if (!res.ok) throw new Error(`接口返回 HTTP ${res.status}${res.statusText ? ' ' + res.statusText : ''}`)

      const contentType = res.headers.get('content-type') || ''
      const isSse = /text\/event-stream/i.test(contentType) && res.body && res.body.getReader

      if (!isSse) {
        arm('idle', idleMs)
        const text = await res.text()
        clearTimer()
        let data = null
        try {
          data = JSON.parse(text)
        } catch (e) {
          /* 纯文本响应 */
        }
        const out = extractText(data) || text
        if (out) onDelta(out)
        return
      }

      const reader = res.body.getReader()
      const decoder = new TextDecoder('utf-8')
      let buffer = ''
      for (;;) {
        arm('idle', idleMs)
        const { done, value } = await reader.read()
        if (done) break
        buffer += decoder.decode(value, { stream: true })
        const lines = buffer.split('\n')
        buffer = lines.pop() ?? ''
        for (const line of lines) {
          const trimmed = line.trim()
          if (!trimmed || trimmed.startsWith(':')) continue
          // SSE 的 `id:` 行就是本条事件的续传游标（Q-09）。解析它，重连时回传
          if (trimmed.startsWith('id:')) {
            const eid = trimmed.slice(3).trim()
            if (eid) opts.onId?.(eid)
            continue
          }
          if (!trimmed.startsWith('data:')) continue
          const data = trimmed.slice(5).trim()
          if (data === '[DONE]') {
            clearTimer()
            return
          }
          try {
            const json = JSON.parse(data)
            const delta = extractText(json)
            if (delta) onDelta(delta)
          } catch (e) {
            /* 非 JSON 的心跳/注释行，忽略 */
          }
        }
      }
      clearTimer()
    } catch (e) {
      clearTimer()
      if (timedOutKind) {
        const err = new Error(
          timedOutKind === 'idle'
            ? `流式响应中断超时（${idleMs}ms 无数据）`
            : `请求超时（${firstByteMs}ms 未响应）`
        )
        err.name = 'TimeoutError'
        throw err
      }
      throw e
    } finally {
      clearTimer()
    }
  }

  function mockReply(text) {
    const rule = MOCK_RULES.find(([re]) => re.test(text))
    return (
      rule?.[1] ??
      `收到：「${text}」\n\n当前运行在**本地模拟模式**（\`mode: 'mock'\`），没有请求真实模型。\n把 \`mode\` 设为 \`'server'\` 并把 \`baseUrl\` 指向你的后端，就能接入真实大模型。`
    )
  }

  async function streamMock(text, onDelta) {
    const full = mockReply(text)
    await sleep(config.mockDelay)
    for (let i = 0; i < full.length; ) {
      const step = 2 + Math.floor(Math.random() * 3)
      onDelta(full.slice(i, i + step))
      i += step
      await sleep(18)
    }
  }

  async function send(rawText, meta = {}) {
    const text = String(rawText ?? '').trim()
    if (!text || sending.value) return

    /* ★ 必须在 send 入口重置，不能只放在 requestStream 里：
       AIAgent 走的是 **transport 路径**（不走 requestStream），
       而 userAborted 只在 requestStream 开头被清。于是 transport 路径下
       「用户停止一次」之后，这个 true 会一直留到下一次发送 ——
       下一条正常回复会被误判成「用户中断」，状态标记跟着全错。
       放在 send 入口则两条路径都覆盖。 */
    userAborted = false
    error.value = ''
    retrying.value = null
    fellBackToMock.value = false
    lastUserText.value = text
    const source = meta && meta.source ? String(meta.source) : 'local'
    audit.emit('message:user', { text, source })
    // 状态矩阵：先落 'sending'（气泡下显示「已送出」），请求收尾时改回 'done'。
    // 没有这一来一回，用户只看得到自己的话静静躺在流里，不知道服务端到底收到没有。
    const userMsgId = uid()
    messages.value.push({
      id: userMsgId,
      role: 'user',
      content: text,
      time: Date.now(),
      source: source === 'local' ? undefined : source,
      status: 'sending',
    })
    save()
    sending.value = true
    streamingText.value = ''
    try {
      config.onSend?.(text, messages.value)
    } catch (e) {
      /* 用户回调异常不影响主流程 */
    }

    const payload = buildPayload()

    try {
      if (config.transport) {
        // v1.8.0 集成扩展：宿主注入发送通道（AIAgent 后端），替代默认 OpenAI fetch。
        // transport 负责把文本发出并逐段回调 onDelta；错误抛给下方 catch（进 error 条）。
        controller = new AbortController()
        const signal = controller.signal
        try {
          await config.transport({
            text,
            sessionId: String(config.sessionId || ''),
            model: config.model,
            systemPrompt: config.systemPrompt,
            onDelta: (c) => { streamingText.value += c },
            onUsage: (u) => { lastUsage.value = u; audit.emit('usage', u) },
            onError: (e) => { throw e },
            signal,
          })
        } catch (e) {
          if (userAborted || e?.name === 'AbortError') {
            /* 用户中止：保留已生成内容，不报错 */
          } else {
            error.value = e?.message || '请求失败，请稍后重试'
            audit.emit('error', { message: error.value, name: e?.name || null })
            try {
              config.onError?.(e)
            } catch (e2) {
              /* noop */
            }
          }
        }
      } else if (config.mock) {
        await streamMock(text, (c) => {
          streamingText.value += c
        })
      } else {
        /* v1.7 断网续传（Q-09）。两种失败分开计数，语义不同：
             连接层（一个字都没收到）→ 用 retry 退避重发整条请求；
             流中断（已经吐了内容）→ 用 reconnect 带 Last-Event-ID 去补后半截。
           旧版对「已吐内容」直接放弃，理由是「重试会产生重复文本」——
           那个理由现在被 createResumeFilter 消掉了：后端重放也会被去重。 */
        const maxConnRetries = retryCfg().attempts || 0
        const reCfg = reconnectCfg()
        let lastEventId = null
        let turnText = ''   // 本轮累计文本（重连前后都在这一个变量里）
        let connRetries = 0
        let reconnects = 0

        const emit = (chunk) => {
          turnText += chunk
          streamingText.value = turnText
        }

        for (;;) {
          // 每次开连接都新建过滤器：它负责吃掉「后端从头重放」的那截重复前缀
          const filter = createResumeFilter(turnText)
          const onDelta = (chunk) => {
            const out = filter.push(chunk)
            if (out) emit(out)
          }
          try {
            await requestStream(payload, onDelta, {
              lastEventId,
              onId: (id) => {
                lastEventId = id
              },
            })
            break // 正常读完
          } catch (e) {
            if (userAborted || e?.name === 'AbortError') break // 用户主动中止，不再续传

            if (turnText) {
              /* 已吐内容后中断 → 优先续传，而不是像旧版直接放弃。
                 大屏网络抖一下，用户不该看到半截回复。 */
              if (reconnects < reCfg.attempts) {
                reconnects++
                reconnecting.value = { attempt: reconnects, total: reCfg.attempts }
                audit.emit('reconnect', { attempt: reconnects, lastEventId, received: turnText.length })
                const delay = reBackoff(reconnects)
                if (config.debug) console.warn(`[ai-assistant] 流中断，${delay}ms 后第 ${reconnects} 次续传重连`)
                await sleep(delay)
                if (userAborted) break
                reconnecting.value = null
                continue
              }
              // 续传次数用尽：保留已生成内容 + 报错（与旧行为一致，但已经尽力续过）
              reconnecting.value = null
              const err = new Error('流式响应中断，已保留已生成的内容')
              err.name = 'StreamInterruptedError'
              err.partial = true
              error.value = err.message
              audit.emit('error', { message: err.message, name: err.name })
              try {
                config.onError?.(err)
              } catch (e2) {
                /* noop */
              }
              break
            }

            // 一个字都没收到 → 连接层失败，走既有 retry 逻辑
            if (connRetries >= maxConnRetries || !isRetryableError(e)) {
              error.value = e?.message || '请求失败，请稍后重试'
              audit.emit('error', { message: error.value, name: e?.name || null })
              try {
                config.onError?.(e)
              } catch (e2) {
                /* noop */
              }
              break
            }
            const delay = backoff(connRetries)
            retrying.value = { attempt: connRetries + 1, total: maxConnRetries, delay }
            if (config.debug) console.warn(`[ai-assistant] 第 ${connRetries + 1} 次失败，${delay}ms 后重试：`, e?.message)
            await sleep(delay)
            if (userAborted) break
            retrying.value = null
            connRetries++
          }
        }
      }

      // FR-508 降级：接口彻底失败、且一个字都没吐出来时，切到本地模拟回复。
      // 条件刻意收紧——已吐内容或用户主动取消都不降级（前者会造成半真半假的拼接，
      // 后者是用户不想要这条回复）。降级必须让宿主看得见，所以置 fellBackToMock。
      if (error.value && !streamingText.value && !userAborted && config.fallbackToMock) {
        fellBackToMock.value = true
        audit.emit('fallback', { reason: error.value })
        await streamMock(text, (c) => {
          streamingText.value += c
        })
        error.value = ''
        if (config.debug) console.warn('[ai-assistant] 接口失败，已降级为本地模拟回复')
      }
    } finally {
      retrying.value = null
      const content = streamingText.value
      streamingText.value = ''
      sending.value = false
      clearTimer()
      controller = null

      /* ---------- 状态矩阵收口 ----------
         一条消息最终是「成了 / 挂了 / 被你停了」，只在这里定一次。
         以前这里无条件 push 成一条普通消息，于是：
           · 用户中断的半截回复伪装成正常回复进历史，回头看分不清是自己停的还是它就说了这么多；
           · 生成失败则只留输入框上方那条全局 error 条，被下一条一顶就没了，
             而这条消息还挂在上面看着像正常的。 */
      const finalStatus = userAborted ? 'stopped' : error.value ? 'failed' : 'done'
      const userMsg = messages.value.find((m) => m.id === userMsgId)
      if (userMsg) userMsg.status = 'done'

      /* 中断且一个字都没吐出来 → 不留痕（用户主动停，也没什么可留的）；
         失败即使没内容也要留一条 —— 就地可重试，比全局条被顶掉强。 */
      const keep = !!content || (!userAborted && !!error.value)
      if (keep) {
        audit.emit('message:assistant', { length: content.length })
        const msg = {
          id: uid(),
          role: 'assistant',
          content,
          time: Date.now(),
          status: finalStatus,
        }
        // 失败原因挂在消息自己身上：全局 error 条会被顶掉，消息自己的不会
        if (finalStatus === 'failed') msg.error = error.value
        messages.value.push(msg)
        lastDataAt.value = Date.now()
        save()
        try {
          config.onReceive?.(content)
        } catch (e) {
          /* noop */
        }
      }
    }
  }

  /* ---------------- 危险操作审批 ---------------- */
  /** 递归查找审批块（它可能嵌在 group 里） */
  function findApproval(blocks, id) {
    for (const b of blocks || []) {
      if (b.type === 'approval' && String(b.id) === String(id)) return b
      if (b.type === 'group' && b.blocks) {
        const hit = findApproval(b.blocks, id)
        if (hit) return hit
      }
    }
    return null
  }

  /**
   * 批准 / 拒绝一个待审批操作。
   * 这里只负责「把状态写回消息数据」并回调 onApproval，真正的执行由宿主页面
   * 在 onApproval 里发起——组件不替你做危险动作。
   */
  function resolveApproval(id, decision, extra = {}) {
    let hit = null
    for (const m of messages.value) {
      if (!Array.isArray(m.blocks)) continue
      hit = findApproval(m.blocks, id)
      if (hit) break
    }
    if (!hit) {
      if (config.debug) console.warn('[ai-assistant] 未找到审批项', id)
      return false
    }
    if (hit.status && hit.status !== 'pending') return false
    // 权限门禁：「看不见」和「批不了」必须一致 —— 角色无权看这个审批块时，
    // 顶层 approve() 也不能绕过界面把它放行，否则权限形同虚设
    if (!getAuth(config).allowed(hit)) {
      audit.emit('approval:blocked', { id: String(hit.id), action: (hit.action && hit.action.name) || null, decision })
      return false
    }
    hit.status = decision === 'approve' ? 'approved' : 'rejected'
    hit.resolvedAt = Date.now()
    // ★ 合规关键事件：谁批了哪次危险操作、批还是拒、什么时候
    audit.emit('approval:decide', {
      id: String(hit.id),
      decision: decision === 'approve' ? 'approved' : 'rejected',
      action: (hit.action && hit.action.name) || null,
      risk: hit.risk || null,
    })
    hit.resolvedAt = Date.now()
    if (extra.by) hit.resolvedBy = extra.by
    save()
    try {
      config.onApproval?.(hit.status, hit)
    } catch (e) {
      /* 用户回调异常不影响主流程 */
    }
    return true
  }

  /** 待审批项自动作废：Agent 不能无限期等一个人，否则挂住的会话会被误批 */
  let expiryTimer = null
  function startExpiryWatch() {
    if (expiryTimer) return
    expiryTimer = setInterval(() => {
      const t = Date.now()
      let changed = false
      const walk = (arr) => {
        for (const b of arr || []) {
          if (
            b.type === 'approval' &&
            (!b.status || b.status === 'pending') &&
            b.timeoutMs &&
            b.createdAt &&
            t > b.createdAt + b.timeoutMs
          ) {
            b.status = 'expired'
            audit.emit('approval:expire', { id: String(b.id), action: (b.action && b.action.name) || null })
            b.resolvedAt = t
            changed = true
          }
          if (b.type === 'group' && b.blocks) walk(b.blocks)
        }
      }
      for (const m of messages.value) if (Array.isArray(m.blocks)) walk(m.blocks)
      if (changed) save()
    }, 1000)
  }
  onBeforeUnmount(() => {
    if (expiryTimer) clearInterval(expiryTimer)
    expiryTimer = null
  })

  /** 手动把新鲜度时间戳拨到当前 —— 宿主用非 push 的通道更新了数据时调用 */
  function touch() {
    lastDataAt.value = Date.now()
  }

  /** 直接推入一条消息，支持 blocks 富内容（后端推送 / 演示 / 大屏广播） */
  function push(msg) {
    if (!msg) return
    // 外部推入的数据（后端广播 / 演示 / 大屏）一律过一遍协议校验：
    // 脏数据就地降级成可渲染的形态，绝不让它把整条消息打成白屏
    const { message: m, issues } = normalizeMessage(msg)
    if (issues.length && config.debug) console.warn('[ai-assistant] 消息协议校验：', issues)
    messages.value.push({
      id: uid(),
      role: m.role,
      content: m.content || '',
      blocks: Array.isArray(m.blocks) && m.blocks.length ? m.blocks : undefined,
      time: m.time || Date.now(),
      // ★ 真 bug（真浏览器验证才发现）：重建消息对象时漏了 source，
      //   宿主用 push 投递的遥控消息不带来源标记，界面看不出这句话来自手机。
      //   send() 有、push() 没有 —— 两条入口必须一致。
      source: m.source || undefined,
    })
    lastDataAt.value = Date.now()
    // 工具调用与审批请求都在 push 进来的块里，递归扫一遍记进审计
    auditBlocks(audit, messages.value[messages.value.length - 1].blocks)
    save()
  }

  function abort() {
    userAborted = true
    retrying.value = null
    reconnecting.value = null
    clearTimer()
    if (controller) {
      try {
        controller.abort()
      } catch (e) {
        /* noop */
      }
      controller = null
    }
  }

  function retry() {
    if (!lastUserText.value) return
    const idx = messages.value.map((m) => m.content).lastIndexOf(lastUserText.value)
    if (idx >= 0 && messages.value[idx].role === 'user') {
      // FR-1209：连同这次失败留下的**半截助手回复**一起清掉。
      // 只删用户那句话的话，重试成功后老的半截还挂在上面，
      // 看起来像回复了两遍，用户会以为重试没生效。
      messages.value.splice(idx, messages.value.length - idx)
    }
    send(lastUserText.value)
  }

  /** v1.8.0 集成扩展：外部灌入历史消息（AIAgent 会话切换）。
     只替换消息与流式状态，不写本地存储（persist 由宿主侧控制）。
     灌入空列表时回落到欢迎语（config.welcome 为空则无消息），避免空会话一片空白。 */
  function hydrate(list) {
    if (Array.isArray(list) && list.length > 0) {
      /* 必须过 schema 归一化，与 push / load 同路：
         后端按标准 schema（REQUIREMENTS §8.2）发顶层 chartType/series/categories
         或 plan.items 时，只有 normalizeMessage 会把它们映射成组件认的 option/steps。
         原来直接 slice() 灌入 → 顶层写法的图表渲染成「无数据」、计划渲染成空列表，
         且只在「会话切换」这一条路上复现（push/load 都好的），极难定位。
         issues 不在这里消费：宿主灌历史是常态路径，降级已就地完成（schema 永不抛错）。 */
      messages.value = list.map((m) => normalizeMessage(m).message)
    } else {
      messages.value = config.welcome
        ? [{ id: uid(), role: 'assistant', content: config.welcome, time: Date.now(), welcome: true }]
        : []
    }
    streamingText.value = ''
    sending.value = false
    error.value = ''
    retrying.value = null
    reconnecting.value = null
    fellBackToMock.value = false
    lastUserText.value = ''
    userAborted = false
    lastDataAt.value = Date.now()
  }

  return {
    messages, sending, streamingText, error, retrying, reconnecting, canRetry, fellBackToMock, lastDataAt, lastUsage,
    load, save, seed, reset, send, push, abort, retry, touch, hydrate,
    resolveApproval, startExpiryWatch, audit,
    // v1.12.0 会话历史
    sessions, currentId, historyOn, newSession, switchSession, removeSession, renameSession, clearSessions,
    // v1.13.0 会话导出（FR-220）：漏返回会导致上层解构拿到 undefined，
    // expose 出去后 API 层兜底成空字符串 —— O-24 曾因此失败
    exportSession, exportAllSessions,
  }
}
