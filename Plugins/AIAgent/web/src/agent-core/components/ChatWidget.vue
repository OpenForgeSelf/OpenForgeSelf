<script setup>
import { ref, computed, watch, nextTick, onMounted, onBeforeUnmount } from 'vue'
// 组件样式（顺序须与组件源 embed.js 的 Shadow 注入一致：widget.css 先、blocks.css 后）。
// 组件源靠 Shadow DOM + `?inline` 注入；SFC 集成版无 Shadow DOM，
// 必须由 Vite 正常 import 打包进 style.css，否则所有 .aiw-* 规则缺失（头像/气泡/sr-only 全失效）。
import '../styles/widget.css'
import '../styles/blocks.css'
import { useChat } from '../composables/useChat'
import MessageItem from './MessageItem.vue'
import ComposeTrigger from './ComposeTrigger.vue'
import ContextChips from './ContextChips.vue'
import ContextBar from './ContextBar.vue'
import AskPanel from './AskPanel.vue'
import TodoEntry from './TodoEntry.vue'
import { matchTrigger } from '../utils/trigger'
import { getAuth } from '../utils/auth'
import { useRemote } from '../composables/useRemote'
import { formatWhen } from '../utils/sessions'

const props = defineProps({
  config: { type: Object, required: true },
  /** 由 embed.js 持有的共享响应式状态，外部可通过它控制开关 */
  control: { type: Object, required: true },
})

// v1.8.0 集成扩展：宿主页面监听「消息状态已变化」（发送完成 / 会话切换灌入历史）
const emit = defineEmits(['state-changed'])

const { messages, sending, streamingText, error, retrying, reconnecting, canRetry, fellBackToMock, lastDataAt, lastUsage, load, seed, reset, send, push, abort, retry, touch, resolveApproval, audit, hydrate: hydrateMessages,
  sessions, currentId, historyOn, newSession, switchSession, removeSession, renameSession, clearSessions,
  exportSession, exportAllSessions } =
  useChat(props.config)

/* 遥控（v1.8）：组件只管会话、校验、限流、审计；通道与二维码交给宿主 */
const {
  enabled: remoteEnabled, state: remoteState, session: remoteSession, code: remoteCode,
  count: remoteCount, pairUrl, qrSrc: remoteQrSrc, receive: remoteReceive,
  start: startRemote, rotate: remoteRotate, unpair: remoteUnpair,
} = useRemote(props.config, { audit, send, isBusy: () => sending.value || !!reconnecting.value })

const showPair = ref(false)
const remoteLabel = computed(() => {
  if (!remoteEnabled) return ''
  if (remoteState.value === 'paired') return '遥控·已连'
  if (remoteState.value === 'expired') return '遥控·已过期'
  return '遥控·待配对'
})

/* ---------- 无障碍 id（v1.11.0） ----------
   多个实例共存时 id 不能撞：撞了就会把另一块面板的标题念成本面板的标题。
   所以每个实例各自生成，不做成常量。 */
const uid = Math.random().toString(36).slice(2, 8)
const titleId = 'aiw-title-' + uid
const descId = 'aiw-desc-' + uid
const panelId = 'aiw-panel-' + uid
const historyId = 'aiw-history-' + uid

const draft = ref('')
const unread = ref(0)
const scrollEl = ref(null)
const inputEl = ref(null)
const panelEl = ref(null)
const launcherEl = ref(null)
const historyBtnEl = ref(null)
const historyPanelEl = ref(null)

/* ---------- 会话历史侧栏（v1.12.0） ---------- */
const showHistory = ref(false)
/** 「清空全部」是破坏性且不可撤销的：第一下只武装，第二下才真删 */
const clearArmed = ref(false)
let clearArmTimer = null
function whenText(ts) {
  return formatWhen(ts, Date.now())
}
function openHistory() {
  if (!historyOn()) return
  showHistory.value = true
  nextTick(() => {
    const el = historyPanelEl.value
    const first = el && el.querySelector('button')
    if (first) first.focus()
  })
}
function closeHistory() {
  if (!showHistory.value) return
  showHistory.value = false
  nextTick(() => {
    const btn = historyBtnEl.value
    if (btn) btn.focus()
  })
}
function toggleHistory() {
  showHistory.value ? closeHistory() : openHistory()
}
function onNewSession() {
  newSession()
  closeHistory()
}
function onSwitchSession(id) {
  switchSession(id)
  closeHistory()
}
function onRemoveSession(id) {
  removeSession(id)
}
function onClearSessions() {
  if (!clearArmed.value) {
    clearArmed.value = true
    if (clearArmTimer) clearTimeout(clearArmTimer)
    clearArmTimer = setTimeout(() => (clearArmed.value = false), 3000)
    return
  }
  clearArmed.value = false
  if (clearArmTimer) clearTimeout(clearArmTimer)
  clearSessions()
}

/* ---------- 主题变量 ---------- */
function hexToRgba(hex, alpha) {
  let h = String(hex || '').trim().replace('#', '')
  if (h.length === 3) h = h.split('').map((c) => c + c).join('')
  if (!/^[0-9a-f]{6}$/i.test(h)) return `rgba(79,70,229,${alpha})`
  const n = parseInt(h, 16)
  return `rgba(${(n >> 16) & 255},${(n >> 8) & 255},${n & 255},${alpha})`
}

const rootStyle = computed(() => ({
  '--aiw-primary': props.config.primaryColor,
  '--aiw-primary-08': hexToRgba(props.config.primaryColor, 0.08),
  '--aiw-primary-16': hexToRgba(props.config.primaryColor, 0.16),
  '--aiw-primary-32': hexToRgba(props.config.primaryColor, 0.32),
  '--aiw-panel-w': `${props.config.panelWidth}px`,
  '--aiw-panel-h': `${props.config.panelHeight}px`,
  '--aiw-off-x': `${props.config.offsetX}px`,
  '--aiw-off-y': `${props.config.offsetY}px`,
  // FR-804：整体缩放交给 CSS 的 zoom，面板尺寸与字号一起变，
  // 比逐条改 font-size 可靠（块样式里有几十处硬编码 px）
  '--aiw-scale': String(props.config.fontScale || 1),
}))

const isOpen = computed(() => !!props.control.open)
const canSend = computed(() => !!draft.value.trim() && !sending.value)
/** 用户发过第一条消息后，快捷问题收起，避免占用空间 */
const showQuick = computed(
  () =>
    !props.config.readOnly && // 只读模式下没有输入框，快捷问题点了也发不出去
    props.config.quickReplies?.length > 0 &&
    !messages.value.some((m) => m.role === 'user')
)

/* ---------- 状态矩阵：空状态 / 加载中（041 状态矩阵专项） ----------
   这两类态以前一条都没有。空状态尤其容易被跳过 ——
   它只在「刚打开还没说话」时出现几秒，但**恰恰是用户对产品的第一印象**；
   一片空白等于把人晾在白板前，不知道能干什么、也不敢乱试。
   加载中同理：历史会话加载慢时，空白会被读成「坏了」。 */
const isLoading = computed(() => !!props.config.loading)
const showEmpty = computed(
  () =>
    !isLoading.value &&
    messages.value.length === 0 &&
    !streamingText.value &&
    !sending.value &&
    // 配对卡占着画面时不算空态 —— 那时屏上有东西，只是不是消息
    !(remoteEnabled.value && showPair.value)
)
/** 空态文案由宿主给；组件只备一句兜底，不替宿主决定该说什么（组件零业务） */
const emptyTitle = computed(() => props.config.emptyTitle || '还没有对话')
const emptyHint = computed(() => props.config.emptyHint || '在下面输入点什么，就能开始')

/* ---------- FR-802 自动刷新：定时回调宿主取数 ----------
   组件不发明查询——它只负责「到点了，该刷了」，取什么由宿主的 onRefresh 决定。
   同一页面挂 4 个面板时各自独立计时，互不影响。 */
let refreshTimer = null
function armRefresh() {
  const ms = Number(props.config.refreshInterval) || 0
  if (refreshTimer) {
    clearInterval(refreshTimer)
    refreshTimer = null
  }
  if (ms <= 0) return
  refreshTimer = setInterval(() => {
    // FR-803 / NFR-15：后台标签页不空转。大屏浏览器本身会节流，
    // 自己再叠一层可以避免「切回前台瞬间积压 N 次刷新」
    if (props.config.pauseWhenHidden && typeof document !== 'undefined' && document.hidden) return
    // FR-1208：正在生成或正在续传时跳过这一拍。
    // onRefresh 会让宿主重新取数、往往连带 push 新消息，与流式补齐同时写
    // 同一份界面（滚动位置、消息列表）会互相打扰，大屏上就是画面跳一下。
    // 让刷新等流式结束是更安全的取舍：丢一拍远比画面抽搐可接受。
    if (props.config.pauseRefreshWhileBusy && (sending.value || reconnecting.value)) {
      const reason = reconnecting.value ? 'reconnecting' : 'busy'
      audit.emit('refresh:skip', { reason })
      if (props.config.debug) console.warn('[ai-assistant] 本拍刷新已跳过：' + reason)
      return
    }
    audit.emit('refresh', { interval: ms })
    try {
      props.config.onRefresh?.()
    } catch (e) {
      console.warn('[ai-assistant] onRefresh 抛错：', e)
    }
  }, ms)
}

/* ---------- FR-803 数据新鲜度 ---------- */
const nowTick = ref(Date.now())
let freshTimer = null
function armFreshness() {
  if (freshTimer) {
    clearInterval(freshTimer)
    freshTimer = null
  }
  if (!props.config.showFreshness) return
  // 采样周期跟着 staleAfter 走：阈值小的时候必须更快复核，否则「过期」要等满 15 秒才出现。
  // 下限 1s（够便宜）、上限 15s（避免无意义的空转）
  const after = Number(props.config.staleAfter) || 0
  const period = Math.max(1000, Math.min(15000, Math.floor(after / 4) || 15000))
  freshTimer = setInterval(() => {
    nowTick.value = Date.now()
  }, period)
}
const freshnessText = computed(() => {
  const at = Number(lastDataAt.value) || 0
  if (!at) return ''
  const diff = Math.max(0, nowTick.value - at)
  const min = Math.floor(diff / 60000)
  if (diff < 60000) return '数据刚刚'
  if (min < 60) return `数据 ${min} 分钟前`
  const hour = Math.floor(min / 60)
  if (hour < 24) return `数据 ${hour} 小时前`
  return `数据 ${Math.floor(hour / 24)} 天前`
})
const isStale = computed(() => {
  const after = Number(props.config.staleAfter) || 0
  if (!after) return false
  return nowTick.value - (Number(lastDataAt.value) || 0) > after
})
/** 供宿主主动拨准（非 push 通道更新数据时） */
function markFresh() {
  touch()
  nowTick.value = Date.now()
}
const typing = computed(() => sending.value && !streamingText.value)
/** 触屏设备：打开面板时不主动聚焦，否则软键盘立刻弹出把面板顶出视口（FR-203） */
const isTouchDevice = () =>
  typeof window !== 'undefined' &&
  ('ontouchstart' in window || (window.matchMedia && window.matchMedia('(hover: none)').matches))
/** 流式期间允许随时中断（FR-212） */
function stop() {
  abort()
}

/* ---------- 无障碍：焦点管理（v1.11.0） ----------
   模态面板的三件事，缺一件键盘用户就会迷路：
     1) 打开时要把焦点送进面板（否则焦点还停在启动按钮上，Tab 顺序是错的）；
     2) Tab 要圈在面板内（否则一路 Tab 就跑到宿主页面里去了，看不见焦点在哪）；
     3) 关闭后要把焦点**还回去**（否则焦点丢在 body 上，键盘用户得从头 Tab）。
   触屏设备不主动聚焦：软键盘会立刻弹起，把面板顶出视口（FR-203 的既有取舍）。 */
const FOCUSABLE =
  'a[href],button:not([disabled]),textarea:not([disabled]),input:not([disabled]),select:not([disabled]),[tabindex]:not([tabindex="-1"])'

function focusIntoPanel() {
  // autoFocus 是宿主开关，显式给了就以它为准；没给才走触屏启发式（FR-203：
  // 触屏自动聚焦会弹软键盘，把面板顶出视口）。
  // 顺带把这条路径变成**可确定性断言**的 —— 否则测试会被宿主的触屏启发式左右，
  // 而 jsdom 里 `'ontouchstart' in window` 恒为 true，根本测不到非触屏分支。
  if (props.config.autoFocus === false) return
  if (props.config.autoFocus !== true && isTouchDevice()) return
  const panel = panelEl.value
  if (!panel) return
  // 不依赖 ref 是否已赋值：直接问 DOM 要输入框。
  // autoOpen 时挂载瞬间 ref 可能还没绑上，那时若退回面板本身，
  // 焦点虽然「进了面板」却停在 div 上，键盘用户还得再 Tab 一次才到输入框。
  const target = inputEl.value || panel.querySelector('textarea') || panel.querySelector('input') || panel
  if (target && typeof target.focus === 'function') {
    try {
      target.focus({ preventScroll: true })
    } catch (e) {
      /* 老浏览器不支持 options 参数时退化为无参调用 */
      try { target.focus() } catch (e2) { /* noop */ }
    }
  }
}
/** 面板内当前真正聚焦的节点。焦点在 shadow 里时 document.activeElement 是宿主，不能直接用 */
function activeInPanel() {
  const panel = panelEl.value
  if (!panel) return null
  const root = panel.getRootNode ? panel.getRootNode() : null
  return (root && root.activeElement) || document.activeElement
}

/* ---------- 开关 ---------- */
function open() {
  if (props.control.open) return
  props.control.open = true
  unread.value = 0
  try {
    props.config.onOpen?.()
  } catch (e) {
    /* noop */
  }
}
function close() {
  // 内联模式（大屏宫格）面板常驻，不允许收起
  if (props.config.inline) return
  if (!props.control.open) return
  props.control.open = false
  // 焦点归还启动按钮：键盘用户不丢位置（内联模式没有启动按钮，跳过）
  nextTick(() => {
    const btn = launcherEl.value
    if (btn && typeof btn.focus === 'function') {
      try { btn.focus({ preventScroll: true }) } catch (e) { /* noop */ }
    }
  })
  try {
    props.config.onClose?.()
  } catch (e) {
    /* noop */
  }
}
function toggle() {
  isOpen.value ? close() : open()
}

/* ---------- 滚动 ---------- */
const atBottom = ref(true)
const belowCount = ref(0) // 用户上翻期间，底下又新来了几条

async function scrollToBottom(behavior = 'smooth') {
  await nextTick()
  const el = scrollEl.value
  if (!el) return
  el.scrollTo({ top: el.scrollHeight, behavior })
}

/** FR-217：内容不在最底下时才显示「回到最新」。
    留 24px 容差 —— 亚像素与 smooth 收尾误差会让按钮在真到底时也闪一下。 */
const BOTTOM_GAP = 24
function onBodyScroll() {
  const el = scrollEl.value
  if (!el) return
  const gap = el.scrollHeight - el.scrollTop - el.clientHeight
  const was = atBottom.value
  atBottom.value = gap <= BOTTOM_GAP
  computeActiveTick()
  /* 角标的意思是「你上翻之后又来了几条」——
     所以只要**离开底部**就清零：之前攒下的计数（含挂载期那些）对用户毫无意义，
     不清就会在上翻的瞬间先弹出一个凭空的数字。 */
  if (atBottom.value || was) belowCount.value = 0
}

/* FR-218 用户消息刻度条 */
const userTicks = computed(() =>
  messages.value
    .filter((m) => m.role === 'user')
    .map((m) => ({
      id: m.id,
      text: String(m.content || '').replace(/\s+/g, ' ').trim().slice(0, 80) || '（空消息）',
    }))
)
const activeTick = ref(0)

/** 点刻度跳转时把目标消息顶边放在视口顶上方这么多 px（留白，免得贴死顶边）。 */
const JUMP_OFFSET = 12
/* 「算作已越过视口顶」的容差。
   ★ 必须 **大于** JUMP_OFFSET —— 否则用户点最下面那条刻度后，
   目标消息内容顶 = scrollTop + JUMP_OFFSET > scrollTop + 容差，被判为「还没越过」，
   刻度回落亮倒数第二条，还得再手动滚一点点才亮（真浏览器 RAIL-02 抓到）。
   取 JUMP_OFFSET + 8，兼顾抗亚像素抖动。 */
const TICK_TOLERANCE = JUMP_OFFSET + 8

/** 当前阅读位置对应第几条用户输入：取「顶部已越过的最后一条」。
    到底部时它自然就是最下面那条 —— 与「默认亮起最下面那条」一致。 */
function computeActiveTick() {
  const el = scrollEl.value
  if (!el) return
  const nodes = el.querySelectorAll('.aiw-msg--user')
  if (!nodes.length) { activeTick.value = 0; return }
  /* ★ 真 bug（真浏览器 RAIL-01 抓到）：原来只比「消息顶边是否高于视口顶边」，
     而**贴底时最后一条消息的顶边仍在视口内**，于是它被排除，亮的是倒数第二条 ——
     恰好和需求要的「默认亮起最下面那条」相反。
     在底部就直说在底部：亮最下面那条。中途才用「已越过的最后一条」。 */
  if (el.scrollHeight - el.scrollTop - el.clientHeight <= BOTTOM_GAP) {
    activeTick.value = nodes.length - 1
    return
  }
  const base = el.getBoundingClientRect().top - el.scrollTop
  let idx = 0
  for (let i = 0; i < nodes.length; i++) {
    if (nodes[i].getBoundingClientRect().top - base <= el.scrollTop + TICK_TOLERANCE) idx = i
  }
  activeTick.value = idx
}

/* FR-218b 随滚动浮动的「当前提问」指示条。
   位置：钉在消息区顶部、不随内容滚动（锚定在最上方）。
   内容：跟随阅读位置 —— 显示当前视口顶部对应的那条用户提问，
   而不是永远钉在最上面那条（最新）。
   底部时隐藏：此时本来就在看最新，指示条没有意义，也正好满足
   「不固定悬浮最新输入」——只有上翻进历史时才出现、且显示你正读到的那条。 */
const floatTickVisible = computed(() => userTicks.value.length > 1 && !atBottom.value)
const floatTickText = computed(() => {
  const t = userTicks.value[activeTick.value]
  return t ? t.text : ''
})

/* FR-220 导出：纯文本 → Blob → 触发下载。不依赖任何第三方库。 */
function downloadText(name, text) {
  try {
    const blob = new Blob([text], { type: 'text/markdown;charset=utf-8' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = name
    document.body.appendChild(a)
    a.click()
    a.remove()
    setTimeout(() => URL.revokeObjectURL(url), 1000)
  } catch (e) {
    /* 环境不支持下载（如只读渲染器）就静默 —— 不影响其它功能 */
  }
}

function onExportSession() {
  const md = exportSession(currentId.value)
  if (!md) return
  const cur = sessions.value.find((s) => s.id === currentId.value)
  const name = String((cur && cur.title) || '会话').replace(/[\\/:*?"<>|]/g, '_')
  downloadText(name + '.md', md)
}

function onExportAll() {
  const md = exportAllSessions()
  if (md) downloadText('全部会话.md', md)
}

function jumpToMessage(id) {
  const el = scrollEl.value
  if (!el) return
  const node = el.querySelector('[data-mid="' + id + '"]')
  if (!node) return
  const top = node.getBoundingClientRect().top - el.getBoundingClientRect().top + el.scrollTop - JUMP_OFFSET
  el.scrollTo({ top: Math.max(0, top), behavior: 'smooth' })
  /* 立刻把刻度点亮到目标，而不是等滚动动画走完：
     smooth 全程会连发 scroll 事件，中途经过的消息会抢亮一下再回来（肉眼可见的闪）。
     先点亮目标，动画收尾时 onBodyScroll 再校准成同一条。 */
  const target = userTicks.value.findIndex((t) => t.id === id)
  if (target >= 0) activeTick.value = target
  /* 兜底：动画在部分环境根本不跑（静止动效/无头渲染），此时没有 scroll 事件来校准。 */
  setTimeout(computeActiveTick, 400)
}

function jumpToBottom() {
  belowCount.value = 0
  atBottom.value = true
  scrollToBottom('smooth')
  /* 兜底：smooth 在部分环境（无头渲染、旧 WebView、被降级成静止的动效）
     根本不会动画 —— 点了没反应比不做还糟。400ms 后仍没贴底就硬贴一次。 */
  setTimeout(() => {
    const el = scrollEl.value
    if (!el) return
    if (el.scrollHeight - el.scrollTop - el.clientHeight > BOTTOM_GAP) {
      el.scrollTo({ top: el.scrollHeight, behavior: 'auto' })
    }
  }, 400)
}

/* 新消息只在「用户本来就在底部」时自动跟随。
   用户正上翻看历史时把他一把拽回底部，是最容易让人放弃的行为之一 ——
   改为停在原地、显示「回到最新（N）」。 */
/* 用 length getter，而不是直接 watch(messages)：
   直接 watch 一个 ref 数组时 deep 默认 false，而原地 push **不改 .value 的身份** ——
   于是「新消息自动跟随」在 push 通道上压根不会触发。
   长度变化才是「来了新消息」的可靠判据（未读角标用的也是它）。 */
/* ★ 计数不能信「触发了几次」：实测一次 push 会让这个 watcher 触发两次
   （响应式数组的长度依赖会重复收集），于是「来了 1 条」显示成 2。
   改成自己记「已见过多少条」——对同一长度重复触发时增量为 0，天然幂等。 */
/* ★ 真 bug（埋点实测定位）：用「长度差」当增量是错的，两条路都会歪：
   1) 长度基线与欢迎语入列有竞态 —— 实测同一用例里有的实例基线读到 1、有的读到 0；
   2) 更要命的是**欢迎语自己会被当成一条「新消息」**计进去，
      于是「来了 1 条」显示成 2 条（角标凭空多一条）。
   改为按**消息 id**记账，并且把欢迎语排除在外 ——
   每条消息只计一次，欢迎语永远不算新消息，与到达时机无关。 */
const seenIds = new Set()
watch(() => messages.value.length, () => {
  const fresh = []
  for (const m of messages.value) {
    if (seenIds.has(m.id)) continue
    seenIds.add(m.id)
    if (!m.welcome) fresh.push(m)
  }
  if (!fresh.length) return
  if (atBottom.value) scrollToBottom()
  else belowCount.value += fresh.length
  nextTick(computeActiveTick)
})
watch(streamingText, () => { if (atBottom.value) scrollToBottom('auto') })

/* 助手新消息在关闭状态下累加未读 */
watch(
  () => messages.value.length,
  (now, before) => {
    if (now <= before) return
    if (isOpen.value || !props.config.unreadBadge) return
    // 逐条累加，而不是只看最后一条：同一 tick 内批量 push 时
    // Vue 只触发一次 watch，只看末尾会让「一次到 5 条」显示成 1 条
    for (let i = before; i < now; i++) {
      const m = messages.value[i]
      if (m?.role === 'assistant' && !m.welcome) unread.value += 1
    }
  }
)

/* 打开时聚焦：优先输入框，只读模式没有输入框就聚焦面板本身（tabindex="-1"） */
watch(isOpen, (v) => {
  if (!v) return
  scrollToBottom('auto')
  setTimeout(focusIntoPanel, 60)
})

/* ---------- 输入框 ---------- */
function autoGrow() {
  const el = inputEl.value
  if (!el) return
  el.style.height = 'auto'
  el.style.height = `${Math.min(el.scrollHeight, 108)}px`
}

/* ------------------------------------------------------------------
   行内触发面板（`/` 命令 / `@` 提及）
   组件只管「什么时候弹、弹什么、键盘怎么走」，数据全由 config.triggers 注入。
   不传 config.triggers 时下面全部短路，组件行为与接入前完全一致。
   ------------------------------------------------------------------ */
const triggerEl = ref(null)
/** 当前打开的触发状态：null = 未打开 */
const activeTrigger = ref(null)
/* 每个触发定义的候选缓存：面板每次打开只向宿主取一次，之后内存过滤。
   ★ 用 **WeakMap 且以 def 对象为键**（不是 char）：
     宿主一旦换了 config.triggers 里的定义对象（比如切换项目后文件列表要重取），
     旧缓存自然失效；以 char 为键则永远命中旧数据 —— 切了项目还列着上个项目的文件。 */
const triggerCache = new WeakMap()

const triggerDefs = computed(() => (Array.isArray(props.config.triggers) ? props.config.triggers : []))
const contextChips = computed(() => (props.config.context && Array.isArray(props.config.context.chips) ? props.config.context.chips : []))

/**
 * 判定光标处是否处于「触发态」。
 * ★ 必须是**行首或前一个字符是空白**才触发 —— 否则用户打路径 `src/api/foo`、
 *   邮箱 `a@b.com` 时，面板会一路弹出来打断输入。
 */
function detectTrigger() {
  const defs = triggerDefs.value
  if (!defs.length) return closeTrigger()
  const el = inputEl.value
  if (!el) return closeTrigger()
  const pos = el.selectionStart ?? draft.value.length
  /* 判定规则（行首/空白后才触发、防止路径与邮箱误弹）在 utils/trigger.js，
     与单测同源 —— 不在这里再写一遍正则，免得两处慢慢长歪。 */
  const hit = matchTrigger(draft.value.slice(0, pos), defs.map((t) => t.char))
  if (!hit) return closeTrigger()
  const def = defs.find((t) => t.char === hit.char)
  /* 已经开着同一个触发就只更新过滤词，别重复拉数据 */
  if (activeTrigger.value && activeTrigger.value.char === def.char) {
    if (activeTrigger.value.query !== hit.query) {
      activeTrigger.value = { ...activeTrigger.value, query: hit.query }
    }
    return
  }
  activeTrigger.value = { char: def.char, query: hit.query, start: hit.start, groups: [], loading: true }
  loadTriggerGroups(def)
}

/** 取候选：命中缓存直接用，否则向宿主要一次（异步回填，面板先空态再填充）。 */
function loadTriggerGroups(def) {
  const cached = triggerCache.get(def)
  if (cached) {
    if (activeTrigger.value?.char === def.char) {
      activeTrigger.value = { ...activeTrigger.value, groups: cached, loading: false }
    }
    return
  }
  Promise.resolve(typeof def.groups === 'function' ? def.groups() : def.groups)
    .then((groups) => {
      const list = Array.isArray(groups) ? groups : []
      triggerCache.set(def, list)
      /* 异步回来时用户可能已经关掉面板或换了触发字符 —— 只对当前还开着的那个回填 */
      if (activeTrigger.value?.char === def.char) {
        activeTrigger.value = { ...activeTrigger.value, groups: list, loading: false }
      }
    })
    .catch(() => {
      triggerCache.set(def, [])
      if (activeTrigger.value?.char === def.char) {
        activeTrigger.value = { ...activeTrigger.value, groups: [], loading: false }
      }
    })
}

function closeTrigger() {
  if (activeTrigger.value) activeTrigger.value = null
}

/** 选中一项：插入什么文本由宿主的 onPick 说了算（返回 '' = 不插，只挂上下文）。 */
async function pickTrigger() {
  const st = activeTrigger.value
  const item = triggerEl.value?.current?.() ?? null
  if (!st || !item) return
  const def = triggerDefs.value.find((t) => t.char === st.char)
  const el = inputEl.value
  const end = el ? (el.selectionStart ?? draft.value.length) : draft.value.length
  const insert = (await Promise.resolve(def?.onPick ? def.onPick(item) : item.name)) ?? ''
  const head = draft.value.slice(0, st.start)
  const tail = draft.value.slice(end)
  draft.value = (head + insert + tail).replace(/^\s+/, '')
  closeTrigger()
  nextTick(() => {
    autoGrow()
    if (el) {
      el.focus()
      const pos = draft.value.length - tail.length
      el.setSelectionRange(pos, pos)
    }
  })
}

/** 输入变化：先自适应高度（原逻辑），再判定是否进入触发态。 */
function onDraftInput() {
  autoGrow()
  detectTrigger()
}

function onKeydown(e) {
  /* 面板打开时，方向键 / Enter / Tab / Esc 先归面板，不让它们去发送或跳焦点。
     焦点全程留在 textarea：弹层一抢焦点，用户正在打的字就断了。 */
  if (activeTrigger.value) {
    if (e.key === 'ArrowDown' || (e.key === 'Tab' && !e.shiftKey)) {
      e.preventDefault()
      triggerEl.value?.move(1)
      return
    }
    if (e.key === 'ArrowUp' || (e.key === 'Tab' && e.shiftKey)) {
      e.preventDefault()
      triggerEl.value?.move(-1)
      return
    }
    if (e.key === 'Escape') {
      e.preventDefault()
      e.stopPropagation()
      closeTrigger()
      return
    }
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      void pickTrigger()
      return
    }
  }
  // 中文/日文输入法组合中，Enter 是在选词，不能当作发送
  if (e.key === 'Enter' && !e.shiftKey && !e.isComposing && e.keyCode !== 229) {
    e.preventDefault()
    doSend()
  }
}

/** 审批块点了批准/拒绝：写回状态并回调宿主 */
function handleResolve({ id, decision }) {
  resolveApproval(id, decision)
}

function doSend(text) {
  const value = (text ?? draft.value).trim()
  if (!value || sending.value) return
  draft.value = ''
  /* 面板是跟着光标走的：消息都发出去了，还挂着一个「/命令」菜单纯属添乱 */
  closeTrigger()
  nextTick(autoGrow)
  /* 自己刚发出去的消息是例外：用户刚做了动作，就该看到它。
     「上翻时不抢滚动」针对的是**别人/系统**来的消息，不该把用户自己的操作也按住。 */
  atBottom.value = true
  belowCount.value = 0
  // v1.8.0 集成扩展：发送流程彻底结束（含成功/失败/停止）后通知宿主刷新外部状态
  void send(value).finally(() => emit('state-changed'))
}

/* ---------- 生命周期 ---------- */
function onKeydownGlobal(e) {
  if (e.key === 'Escape') {
    // Esc 的优先级：**先收侧栏，再收面板**。
    // 反过来的话，开着侧栏按一下 Esc 会把整个面板关掉 ——
    // 用户以为「关掉这个列表」，结果对话也没了。
    if (showHistory.value) {
      closeHistory()
      return
    }
    if (isOpen.value && !props.config.inline) close()
    return
  }
  if (e.key !== 'Tab') return
  if (!isOpen.value || props.config.inline) return
  if (props.config.trapFocus === false) return
  const panel = panelEl.value
  if (!panel) return
  const list = Array.from(panel.querySelectorAll(FOCUSABLE))
  if (!list.length) return
  const active = activeInPanel()
  const first = list[0]
  const last = list[list.length - 1]
  // 焦点跑到面板外（或丢在宿主上）：先拉回第一个，别让它继续往外 Tab
  if (!active || !panel.contains(active)) {
    e.preventDefault()
    first.focus()
    return
  }
  if (e.shiftKey && active === first) {
    e.preventDefault()
    last.focus()
  } else if (!e.shiftKey && active === last) {
    e.preventDefault()
    first.focus()
  }
}

onMounted(() => {
  if (!load()) seed()
  startRemote()
  audit.emit('session:init', {
    theme: props.config.theme,
    mode: props.config.mode,
    inline: !!props.config.inline,
    readOnly: !!props.config.readOnly,
    role: props.config.role || null,
  })
  document.addEventListener('keydown', onKeydownGlobal)
  armRefresh()
  armFreshness()
  if (props.control.open) {
    scrollToBottom('auto')
    // autoOpen 时 watch 不会触发（值是同一个），必须在这里补一次聚焦，
    // 否则「打开着但没有焦点」——键盘用户按 Tab 会从宿主页面开头开始。
    // 两拍都试：nextTick 覆盖 ref 绑定，60ms 覆盖弹出过渡结束后的稳定态。
    nextTick(focusIntoPanel)
    setTimeout(focusIntoPanel, 60)
  }
})

onBeforeUnmount(() => {
  document.removeEventListener('keydown', onKeydownGlobal)
  if (refreshTimer) clearInterval(refreshTimer)
  if (freshTimer) clearInterval(freshTimer)
  refreshTimer = null
  freshTimer = null
  abort()
})

function handleReset() {
  reset()
  unread.value = 0
  nextTick(() => inputEl.value?.focus())
}

function approve(id) {
  return resolveApproval(id, 'approve')
}
function reject(id) {
  return resolveApproval(id, 'reject')
}

defineExpose({
  open, close, toggle, send: doSend, push, reset: handleReset, abort, stop,
  approve, reject, markFresh, touch, reconnecting,
  remote: { receive: remoteReceive, rotate: remoteRotate, unpair: remoteUnpair, state: () => remoteState.value, session: () => remoteSession.value, code: () => remoteCode.value, count: () => remoteCount.value },
  can: (ability) => getAuth(props.config).can(ability),
  gates: () => ({ gated: getAuth(props.config).gated, role: props.config.role || null, level: getAuth(props.config).level }),
  focusPanel: focusIntoPanel,
  // 生成中？宿主可据此禁用自己的发送键（与 isReconnecting 对称）
  isSending: () => sending.value,
  sessions, currentId, historyOn,
  newSession, switchSession, removeSession, renameSession, clearSessions,
  exportSession, exportAllSessions,
  audit,
  // v1.8.0 集成扩展：宿主左栏统计（消息数/工具调用）依赖组件当前消息列表
  messages,
  // v1.8.0 集成扩展：会话切换时由宿主灌入历史消息
  hydrate: (list) => {
    hydrateMessages(list)
    unread.value = 0
    emit('state-changed')
  },
  lastUsage,
})
</script>

<template>
  <div
    class="aiw-root"
    :class="[
      config.position === 'left' ? 'aiw-left' : 'aiw-right', { 'aiw-open': isOpen, 'aiw-dark': config.darkMode !== false },
      `aiw-theme-${config.theme || 'page'}`,
      `aiw-density-${config.theme === 'admin' ? 'compact' : 'normal'}`,
      {
        'aiw-inline': !!config.inline,
        'aiw-readonly': !!config.readOnly,   // FR-801
        'aiw-burnin': !!config.burnInGuard,  // FR-805
      },
    ]"
    :style="rootStyle"
  >
    <!-- 启动按钮（内联模式不渲染：面板常驻、无悬浮球） -->
    <button
      v-if="!config.inline"
      ref="launcherEl"
      class="aiw-launcher"
      type="button"
      aria-haspopup="dialog"
      :aria-controls="panelId"
      :aria-label="isOpen ? '收起助手' : '打开助手'"
      :aria-expanded="isOpen ? 'true' : 'false'"
      @click="toggle"
    >
      <span v-if="!isOpen" class="aiw-launcher__glow"></span>
      <svg v-if="!isOpen" viewBox="0 0 24 24" aria-hidden="true">
        <path
          d="M12 2.6c-5.2 0-9.4 3.9-9.4 8.7 0 2.2.9 4.2 2.4 5.7-.1 1.3-.6 2.6-1.5 3.7-.2.3 0 .7.4.6 2-.3 3.7-1 4.9-1.8 1 .3 2.1.5 3.2.5 5.2 0 9.4-3.9 9.4-8.7S17.2 2.6 12 2.6Z"
          fill="currentColor"
        />
        <circle cx="8.4" cy="11.4" r="1.35" fill="#fff" opacity=".92" />
        <circle cx="12" cy="11.4" r="1.35" fill="#fff" opacity=".92" />
        <circle cx="15.6" cy="11.4" r="1.35" fill="#fff" opacity=".92" />
      </svg>
      <svg v-else viewBox="0 0 24 24" aria-hidden="true">
        <path
          d="M6.4 5 5 6.4 10.6 12 5 17.6 6.4 19 12 13.4 17.6 19 19 17.6 13.4 12 19 6.4 17.6 5 12 10.6Z"
          fill="currentColor"
        />
      </svg>
      <span v-if="unread > 0 && !isOpen" class="aiw-launcher__badge">{{ unread > 99 ? '99+' : unread }}</span>
    </button>

    <!-- 对话面板 -->
    <Transition name="aiw-pop">
      <section
        v-show="isOpen"
        :id="panelId"
        ref="panelEl"
        class="aiw-panel"
        role="dialog"
        aria-modal="true"
        :aria-labelledby="config.showHeader === false ? undefined : titleId"
        :aria-label="config.showHeader === false ? (config.title || '对话面板') : undefined"
        :aria-describedby="descId"
        tabindex="-1"
      >
        <!-- 面板自身的说明：role="dialog" 只告诉读屏「这是个对话框」，
             不说它是什么、怎么走。这段只在读屏里出现，视觉上不可见。 -->
        <span :id="descId" class="aiw-sr-only">对话助手面板。按 Esc 收起；Tab 键在面板内循环。</span>

        <header v-if="config.showHeader !== false" class="aiw-header">
          <div class="aiw-header__avatar">
            <img v-if="config.avatar" :src="config.avatar" alt="" />
            <svg v-else viewBox="0 0 24 24" aria-hidden="true">
              <path
                d="M12 2.5a1 1 0 0 1 1 1V5a5.5 5.5 0 0 1 4.5 5.4V11h.5a2.5 2.5 0 0 1 2.5 2.5v4A2.5 2.5 0 0 1 18 20H6a2.5 2.5 0 0 1-2.5-2.5v-4A2.5 2.5 0 0 1 6 11h.5v-.6A5.5 5.5 0 0 1 11 4.99V3.5a1 1 0 0 1 1-1ZM9 13.2a1.3 1.3 0 1 0 0 2.6 1.3 1.3 0 0 0 0-2.6Zm6 0a1.3 1.3 0 1 0 0 2.6 1.3 1.3 0 0 0 0-2.6Z"
                fill="currentColor"
              />
            </svg>
          </div>
          <div class="aiw-header__meta">
            <div class="aiw-header__title" :id="titleId">{{ config.title }}</div>
            <div class="aiw-header__subtitle">
              <i class="aiw-dot"></i><span>{{ config.subtitle }}</span>
              <span
                v-if="config.showFreshness"
                class="aiw-fresh"
                :class="{ 'is-stale': isStale }"
                :title="isStale ? '数据可能已过期' : ''"
              >{{ freshnessText }}{{ isStale ? ' · 可能已过期' : '' }}</span>
              <button
                v-if="remoteEnabled"
                class="aiw-remote"
                :class="`is-${remoteState}`"
                type="button"
                :title="showPair ? '收起配对信息' : '手机遥控配对'"
                @click="showPair = !showPair"
              >
                <i class="aiw-remote__dot"></i>{{ remoteLabel }}
              </button>
            </div>
          </div>
          <div class="aiw-header__actions">
            <button
              v-if="historyOn()"
              ref="historyBtnEl"
              class="aiw-history-toggle"
              type="button"
              :title="showHistory ? '收起会话历史' : '会话历史'"
              :aria-label="showHistory ? '收起会话历史' : '会话历史'"
              :aria-expanded="showHistory ? 'true' : 'false'"
              :aria-controls="historyId"
              @click="toggleHistory"
            >
              <svg viewBox="0 0 24 24" aria-hidden="true">
                <path d="M12 3a9 9 0 1 0 8.5 12h-2.2A6.8 6.8 0 1 1 12 5.2c1.9 0 3.6.8 4.8 2H14v2h6V3h-2v2.3A9 9 0 0 0 12 3Zm-1 4v6l5 3 1-1.7-4-2.4V7h-2Z" fill="currentColor" />
              </svg>
            </button>
            <button
              type="button"
              :title="historyOn() ? '新建会话' : '清空对话'"
              :aria-label="historyOn() ? '新建会话' : '清空对话'"
              @click="handleReset"
            >
              <svg viewBox="0 0 24 24" aria-hidden="true">
                <path
                  d="M9.5 3h5l.8 2H20v2H4V5h4.7l.8-2ZM6.2 9h11.6l-.9 11.1A2 2 0 0 1 14.9 22H9.1a2 2 0 0 1-2-1.9L6.2 9Zm3.3 2.4v7.2h1.6v-7.2H9.5Zm3.4 0v7.2h1.6v-7.2h-1.6Z"
                  fill="currentColor"
                />
              </svg>
            </button>
            <button v-if="!config.inline" type="button" title="收起" aria-label="收起助手" @click="close">
              <svg viewBox="0 0 24 24" aria-hidden="true">
                <path d="M5 11h14v2H5z" fill="currentColor" />
              </svg>
            </button>
          </div>
        </header>

        <!-- 会话历史侧栏：覆盖在面板上，不挤压对话区宽度 -->
        <div
          v-if="historyOn() && showHistory"
          :id="historyId"
          ref="historyPanelEl"
          class="aiw-history"
          role="group"
          aria-label="会话历史"
          tabindex="-1"
        >
          <div class="aiw-history__head">
            <span class="aiw-history__h">会话历史</span>
            <button type="button" class="aiw-history__new" @click="onNewSession">新建</button>
            <button type="button" class="aiw-history__x" aria-label="收起会话历史" @click="closeHistory">×</button>
          </div>
          <ul class="aiw-history__list">
            <li
              v-for="s in sessions"
              :key="s.id"
              class="aiw-history__item"
              :class="{ 'is-active': s.id === currentId }"
            >
              <button
                type="button"
                class="aiw-history__pick"
                :aria-current="s.id === currentId ? 'true' : null"
                @click="onSwitchSession(s.id)"
              >
                <span class="aiw-history__title">{{ s.title }}</span>
                <span class="aiw-history__meta">{{ s.count }} 条 · {{ whenText(s.updatedAt) }}</span>
              </button>
              <button
                type="button"
                class="aiw-history__del"
                :aria-label="'删除会话：' + s.title"
                @click="onRemoveSession(s.id)"
              >×</button>
            </li>
          </ul>
          <div class="aiw-history__foot">
            <span>共 {{ sessions.length }} 个</span>
            <button type="button" class="aiw-history__clear" @click="onClearSessions">
              {{ clearArmed ? '确认清空？' : '清空全部' }}
            </button>
            <button type="button" class="aiw-history__dl" title="导出当前会话为 Markdown" @click="onExportSession">导出</button>
            <button type="button" class="aiw-history__dl" title="导出全部会话为 Markdown" @click="onExportAll">全部</button>
          </div>
        </div>

        <!-- 消息区即实时区（role="log"）。
             以前 aria-live 挂在整块面板上 —— 每加一条消息、每次状态变化都把
             **整个面板**念一遍，读屏用户根本听不清哪句是新的。
             现在只有这块的「新增内容」会被播报，且流式增量天然落在里面。 -->
        <div class="aiw-bodywrap">
        <!-- FR-218b 随滚动浮动的当前提问指示条：钉在消息区顶部、不随内容滚动；
             内容跟随阅读位置显示「你正读到的那条用户提问」，而非永远钉最新。 -->
        <div
          v-if="floatTickVisible"
          class="aiw-float"
          role="status"
          :aria-label="'正在查看：' + floatTickText"
        >
          <span class="aiw-float__ico" aria-hidden="true">
            <svg viewBox="0 0 24 24"><path d="M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18Zm-1 4h2v5h3l-4 4-4-4h3V7Z" fill="currentColor" /></svg>
          </span>
          <span class="aiw-float__txt">{{ floatTickText }}</span>
        </div>
        <div
          ref="scrollEl"
          class="aiw-body"
          role="log"
          aria-live="polite"
          aria-relevant="additions text"
          aria-label="对话消息"
          @scroll="onBodyScroll"
        >
          <div class="aiw-body__inner">
            <!-- 配对卡片：二维码由宿主生成（remote.qr），组件只负责展示 -->
            <div v-if="remoteEnabled && showPair" class="aiw-pair">
              <div class="aiw-pair__head">
                <span>手机遥控</span>
                <button type="button" class="aiw-pair__x" aria-label="收起" @click="showPair = false">×</button>
              </div>
              <div v-if="remoteState === 'expired'" class="aiw-pair__expired">配对码已过期，请点击「换一个码」重新配对</div>
              <img v-if="remoteQrSrc" class="aiw-pair__qr" :src="remoteQrSrc" alt="遥控配对二维码" />
              <div class="aiw-pair__code">{{ remoteCode }}</div>
              <div class="aiw-pair__url">{{ pairUrl }}</div>
              <div class="aiw-pair__foot">
                <span>已接收 {{ remoteCount }} 条</span>
                <button type="button" @click="remoteRotate()">换一个码</button>
                <button type="button" @click="remoteUnpair()">断开</button>
              </div>
            </div>
            <!-- 状态矩阵：加载中 / 空状态。
                 空态以前完全没有——0 条消息时这里是一块纯白，
                 用户不知道能干什么、也不确定这东西是不是坏了。 -->
            <div v-if="isLoading" class="aiw-loading" role="status" aria-label="正在加载对话">
              <i></i><i></i><i></i>
            </div>
            <div v-else-if="showEmpty" class="aiw-empty">
              <svg class="aiw-empty__ico" viewBox="0 0 24 24" aria-hidden="true">
                <path d="M5 4h14a1 1 0 0 1 1 1v7a1 1 0 0 1-1 1h-5l-4 3v-3H5a1 1 0 0 1-1-1V5a1 1 0 0 1 1-1Z" fill="currentColor" />
              </svg>
              <div class="aiw-empty__t">{{ emptyTitle }}</div>
              <div class="aiw-empty__d">{{ emptyHint }}</div>
            </div>
            <MessageItem
              v-for="m in messages"
              :key="m.id"
              :message="m"
              :avatar="config.avatar"
              :config="config"
              @retry="retry"
              @resolve="handleResolve"
            />
            <MessageItem
              v-if="streamingText"
              :message="{ role: 'assistant', content: streamingText, time: Date.now(), streaming: true }"
              :avatar="config.avatar"
              :config="config"
              @retry="retry"
              @resolve="handleResolve"
            />
            <div v-if="typing" class="aiw-typing" role="status" aria-label="正在输入">
              <i></i><i></i><i></i>
            </div>
          </div>
        </div>

        <!-- FR-218 用户消息刻度条：一条输入一条刻度，悬浮看内容，点击跳过去。
             少于 2 条输入不显示 —— 一条刻度说明不了任何事，只会让人以为坏了。 -->
        <div v-if="userTicks.length > 1" class="aiw-rail" role="navigation" aria-label="用户消息导航">
          <button
            v-for="(t, i) in userTicks"
            :key="t.id"
            type="button"
            class="aiw-rail__t"
            :class="{ 'is-on': i === activeTick }"
            :aria-current="i === activeTick ? 'true' : null"
            :aria-label="'跳到第 ' + (i + 1) + ' 条输入：' + t.text"
            @click="jumpToMessage(t.id)"
          >
            <span class="aiw-rail__tip">{{ t.text }}</span>
          </button>
        </div>
        </div>

        <!-- FR-217 回到底部：只在内容不在最底下时出现。
             挂在零高度锚点上 —— 输入区多高、提示条有没有，都不影响它的位置。 -->
        <div class="aiw-jumpwrap">
          <button
            v-if="!atBottom"
            class="aiw-jump"
            type="button"
            title="回到最新消息"
            aria-label="回到最新消息"
            @click="jumpToBottom"
          >
            <svg viewBox="0 0 24 24" aria-hidden="true">
              <path d="M12 16.2 6 10.2l1.4-1.4L12 13.4l4.6-4.6L18 10.2Z" fill="currentColor" />
              <path d="M11 4h2v8h-2z" fill="currentColor" />
            </svg>
            <span v-if="belowCount" class="aiw-jump__n">{{ belowCount > 9 ? '9+' : belowCount }}</span>
          </button>
        </div>

        <div v-if="showQuick" class="aiw-quick">
          <button
            v-for="q in config.quickReplies"
            :key="q"
            class="aiw-chip"
            type="button"
            @click="doSend(q)"
          >
            {{ q }}
          </button>
        </div>

        <!-- 状态提示条：retrying / reconnecting / fellBackToMock / error 四条统一贴输入框上方，
             不再混在消息流末尾。理由：它们是「非对话流的瞬时状态」，放进 messages 里会被当成
             一条消息，越滚越远、还随历史滚动消失；贴输入框才与「下一步在这里操作」同一视觉区。 -->
        <div v-if="retrying || reconnecting || fellBackToMock || error" class="aiw-status">
          <div v-if="retrying" class="aiw-retry" role="status">
            <i class="aiw-retry__dot"></i>
            <span>连接失败，{{ retrying.delay }}ms 后重试（第 {{ retrying.attempt }}/{{ retrying.total }} 次）</span>
            <button type="button" @click="abort">取消</button>
          </div>
          <!-- Q-09 续传提示：让用户知道「在补」，而不是「卡住了」 -->
          <div v-if="reconnecting" class="aiw-reconnect" role="status">
            <i class="aiw-retry__dot"></i>
            <span>连接中断，正在续传（第 {{ reconnecting.attempt }}/{{ reconnecting.total }} 次）…</span>
            <button type="button" @click="abort">取消</button>
          </div>
          <div v-if="fellBackToMock" class="aiw-fallback" role="status">
            <i class="aiw-retry__dot"></i>
            <span>接口暂时不可用，本条为本地模拟回复</span>
          </div>
          <div v-if="error" class="aiw-error" role="alert">
            <span>{{ error }}</span>
            <button v-if="canRetry" type="button" @click="retry">重试</button>
          </div>
        </div>

        <footer v-if="!config.readOnly" class="aiw-footer">
          <!-- 「待你动作」区（041 §3.4）：权限审批 / Agent 反问 / 待办清单紧贴输入框**上沿**，
               不进消息流 —— 它们是「现在轮到你动作」，混进 messages 会被滚动带走。 -->
          <AskPanel
            v-if="config.ask?.items?.length"
            :items="config.ask.items"
            :progress="config.ask.progress ?? 0"
            @resolve="(e) => props.config.ask.onResolve?.(e)"
            @answer="(e) => props.config.ask.onAnswer?.(e)"
          />
          <!-- 已选上下文（所见即所发）+ 行内触发面板，二者都由宿主注入；
               用 mousedown.prevent 保住 textarea 焦点，否则一点面板输入框就失焦。 -->
          <div class="aiw-composer">
            <ContextChips
              v-if="contextChips.length"
              :chips="contextChips"
              @remove="(k) => props.config.context?.onRemove?.(k)"
              @click="(k) => props.config.context?.onClick?.(k)"
            />
            <div v-if="activeTrigger" class="aiw-trgwrap">
              <ComposeTrigger
                ref="triggerEl"
                :char="activeTrigger.char"
                :query="activeTrigger.query"
                :groups="activeTrigger.groups"
                :title="activeTrigger.char === '/' ? '命令与上下文' : '引用文件'"
                @pick="pickTrigger"
                @mousedown.prevent
              />
            </div>
          </div>
          <div class="aiw-input">
            <textarea
              ref="inputEl"
              v-model="draft"
              rows="1"
              :placeholder="config.placeholder"
              aria-label="输入消息"
              :disabled="sending"
              @input="onDraftInput"
              @click="detectTrigger"
              @blur="closeTrigger"
              @keydown="onKeydown"
            ></textarea>
            <!-- 输入框底行：待办入口 + 上下文图标行（同一行；负 margin 抵消输入框内边距，
                 让顶部淡分隔线贯通整个输入框宽度）——上输入框、下按钮的布局。 -->
            <div class="aiw-inputrow">
            <!-- 待办独立入口（041 修订二）：底行最左。待办是常驻任务面板（要独立入口、
                 看全部），审批/询问是瞬时动作，所以待办从这里进、审批/询问走上层 .aiw-ask 浮层。 -->
            <TodoEntry
              v-if="config.todos?.items?.length"
              :open-count="config.todos.openCount ?? 0"
              :progress="config.todos.progress ?? 0"
              :todos="config.todos.items"
            />
            <!-- 上下文图标行（041 §3.1）：**嵌在输入框内**成一行，与输入框同一圆角边框、
                 同一个整体（对齐参考图：图标与权限胶囊就在输入框底行，而不是框外再挂一条）。
                 面板内容由宿主经 config.contextBar.panels 注入（组件不认业务概念）。 -->
            <ContextBar
              v-if="config.contextBar?.items?.length"
              class="aiw-ctxbar--inline"
              :items="config.contextBar.items"
              :panels="config.contextBar.panels ?? {}"
              :access="config.contextBar.access ?? null"
              @open="(k) => props.config.contextBar.onOpen?.(k)"
              @close="(k) => props.config.contextBar.onClose?.(k)"
              @pick="(e) => props.config.contextBar.onPick?.(e)"
            @access-change="(m) => props.config.contextBar.access?.onChange?.(m)"
          />
            </div>
            <button
              class="aiw-send"
              :class="{ 'is-stop': sending }"
              type="button"
              :disabled="!sending && !canSend"
              :title="sending ? '停止生成' : '发送 (Enter)'"
              :aria-label="sending ? '停止生成' : '发送'"
              @click="sending ? stop() : doSend()"
            >
              <svg v-if="!sending" viewBox="0 0 24 24" aria-hidden="true">
                <path d="M3.4 20.6 21.5 12 3.4 3.4l.02 6.7L15.3 12 3.42 13.9Z" fill="currentColor" />
              </svg>
              <svg v-else viewBox="0 0 24 24" aria-hidden="true">
                <rect x="7" y="7" width="10" height="10" rx="1.6" fill="currentColor" />
              </svg>
            </button>
          </div>
          <div v-if="config.showFooterNote && config.footerNote" class="aiw-note">
            {{ config.footerNote }}
          </div>
        </footer>
      </section>
    </Transition>
  </div>
</template>
