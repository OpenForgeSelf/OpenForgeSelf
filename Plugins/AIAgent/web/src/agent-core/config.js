import { env } from './env'

/** 重试策略默认值：指数退避 + 抖动 */
const defaultRetry = {
  attempts: 2,                                     // 失败后额外重试次数（不含首次）
  baseDelay: 600,                                  // 首次退避基数(ms)
  maxDelay: 5000,                                  // 退避上限(ms)
  statuses: [408, 425, 429, 500, 502, 503, 504],   // 这些 HTTP 状态码才重试
}

/** 断网续传策略（v1.7 / Q-09）：流式响应中途断开后的重连行为 */
const defaultReconnect = {
  attempts: 3,      // 流中断后的续传重连次数（不含首次）；0 = 关闭
  baseDelay: 800,   // 首次续传退避基数(ms)
  maxDelay: 8000,   // 续传退避上限(ms)
}

/**
 * 组件默认配置。
 * 优先级：init(config) 传入值  >  .env.* 注入值  >  下面的默认值
 */
export const defaultConfig = {
  /* ---------- 外观 ---------- */
  title: '智能助手',
  subtitle: '在线 · 通常秒回',
  avatar: '',
  primaryColor: '#4f46e5',
  position: 'right',
  offsetX: 24,
  offsetY: 24,
  zIndex: 2147483000,
  panelWidth: 384,
  panelHeight: 620,
  welcome: '你好，我是智能助手 👋\n有什么可以帮你？',
  placeholder: '输入你的问题…',
  quickReplies: ['你们的产品怎么收费？', '怎么把助手接入我的网站？', '支持流式输出吗？'],
  // 041 状态矩阵：消息区「加载中」，宿主拉历史 / 切会话时置 true
  loading: false,
  /* 041 状态矩阵：空态文案。默认留空 = 用组件兜底文案 ——
     组件零业务，不替宿主决定空着的时候该说什么。 */
  emptyTitle: '',
  emptyHint: '',
  footerNote: '内容由 AI 生成，请自行甄别',
  showFooterNote: true,
  darkMode: true,
  theme: 'page',

  /* ---------- 挂载方式 ---------- */
  // 传入容器（CSS 选择器或 DOM 元素）则进入「内联模式」：
  // 面板嵌在容器里铺满、不显示悬浮按钮、常驻打开。大屏多宫格用这个。
  container: null,
  inline: false,                                   // 由 container 推导，无需手填

  /* ---------- 集成扩展（v1.8.0 迁移适配） ----------
     供宿主页面（如 OpenForgeSelf AIAgent 插件）以纯 props/config 方式复用本组件：
     隐藏自身标题栏由宿主页面提供标题；发送通道交给宿主注入的 transport。 */
  showHeader: true,       // false = 隐藏面板顶部标题栏（内嵌场景）
  /* 行内触发面板（`/` 命令 / `@` 提及）。默认 null = 关闭，组件行为与没有它时完全一致。
     形状：[{ char, groups: () => Promise<[{key,title,items:[{id,name,desc?,meta?}]}]>,
             onPick: (item) => string | Promise<string> }]
     —— 数据与「选中后插入什么」全由宿主给，组件不认识任何业务概念。 */
  triggers: null,
  /* 输入区上方的上下文 chips（所见即所发）。默认 null = 不渲染。
     形状：{ chips: [{key,label,icon?,removable?,clickable?}], onRemove(key), onClick(key) } */
  context: null,
  /* 输入框**下方**的上下文图标行 + 权限胶囊（041）。默认 null = 不渲染。
     形状：{ items:[{key,icon,label,count?,active?,detail?}],
             panels:{[key]:{title,multi?,items:[{id,name,desc?,active?}]}},
             access:{modes:[{key,label,desc}],value,onChange(mode)},
             onOpen(key), onClose(key), onPick({key,id}) }
     —— 入口恒显示（chips 是「有值才出现」），组件不认识任何业务概念。 */
  contextBar: null,
  /* 紧贴输入框**上沿**的「待你动作」区（041）：**只放审批 / Agent 反问**两类瞬时动作。
     待办已拆成独立入口（见 config.todos）。默认 null = 不渲染。
     形状：{ items:[{id,kind:'approval'|'question',title,detail?,risk?,options?}], progress } */
  ask: null,
  /* 待办**独立入口**（041 修订二）：渲染在输入框下方底行最左（与上下文图标行同一行），
     点击展开浮层列表。待办是常驻任务面板（要独立入口、看全部），
     与审批/询问的生命周期不同，故拆开。默认 null = 不渲染。
     形状：{ items:[{id,title,done}], openCount, progress } */
  todos: null,
  /* v1.8.0 展示精简：false = 助手消息不渲染头像（宿主页面风格，如 OpenForgeSelf AIAgent）。
     注：组件源 FR-205 验收要点写的是「AI 左侧带头像」，默认必须保持 true；
     关掉它是**宿主的自主裁剪**，不是组件默认值，组件独立交付时才不会与需求文档冲突。 */
  showBotAvatar: true,
  transport: null,        // (ctx) => Promise；非空时 send() 改走宿主注入通道，替代默认 OpenAI fetch
  sessionId: '',          // 外部会话标识透传（transport 上下文携带）

  /* ---------- 行为 ---------- */
  autoOpen: false,
  persist: true,
  storageKey: 'ai-assistant-widget',
  maxHistory: 40,
  unreadBadge: true,

  /* ---------- 接口 ---------- */
  mode: env.mode,
  baseUrl: env.baseUrl,
  apiPath: env.apiPath,
  apiUrl: '',
  apiKey: env.apiKey,
  authHeader: 'Authorization',
  authScheme: 'Bearer ',
  model: env.model,
  systemPrompt: '你是网站的专业客服助手。回答简洁、准确、友好，默认使用中文。',
  stream: true,
  temperature: 0.7,
  maxTokens: 1024,
  headers: {},
  // 超时：0 = 不限制。默认值是「防止永久转圈」的兜底，不要设成 0。
  timeout: 30000,        // 首字节超时：发出请求后多久没等到响应就放弃
  idleTimeout: 60000,    // 流空闲超时：两个数据块之间最长间隔（流式输出的真正保护）
  retry: { ...defaultRetry },
  // v1.7：断流续传默认开启。旧行为是「吐了内容就放弃」，对无人值守大屏太脆
  mock: env.mode === 'mock',
  mockDelay: 600,
  fallbackToMock: false,
  debug: false,
  chartRenderer: null,

  /* ---------- 无障碍（v1.11.0） ---------- */
  // 焦点陷阱：面板打开时把 Tab 圈在面板内，键盘用户不会一路 Tab 到宿主页面里去
  // （内联/大屏模式常驻、无模态语义，自动不生效，无需在此关闭）
  trapFocus: true,
  // 打开面板是否自动聚焦：null = 交给触屏启发式（默认）；true = 总是；false = 从不
  autoFocus: null,

  /* ---------- 权限分级（v1.6） ---------- */
  // 只有显式给出 role 才启用门禁（默认 null = 不设防，向后兼容）
  role: null,              // 'viewer' | 'operator' | 'admin' | 自定义
  roles: null,             // 覆盖/扩展角色等级表，如 { viewer: 1, editor: 2, admin: 3 }
  hideUnauthorized: false, // true = 直接不渲染；false = 渲染「需要 X 权限」占位（不泄数据）
  actor: null,             // { id, name } 操作者身份，进审计日志

  /* ---------- 审计日志（v1.6） ---------- */
  audit: false,            // true | { max: 500, redact: (type, payload) => payload }
  onAudit: null,           // (event, total) => void；宿主负责把它送到服务端

  /* ---------- 大屏专用（v1.5） ---------- */
  // 挂在墙上的大屏没人敲键盘：隐藏输入区，只保留滚动与展开折叠
  readOnly: false,
  // 无人值守时的自动刷新：定时回调宿主去重新取数，组件自己不发明查询
  refreshInterval: 0,        // ms，0 = 关闭
  pauseWhenHidden: true,     // 后台标签页不空转（大屏浏览器也会做节流）
  // 数据新鲜度：大屏最怕展示过期数据却没人知道
  showFreshness: false,
  staleAfter: 300000,        // 超过 5 分钟标为「可能已过期」
  // 字号档位：实际取决于屏幕距离，不能写死倍数
  fontScale: 1,
  // 防烧屏：内容缓慢漂移 + 亮度呼吸，长期点亮的 LCD 会留残影
  burnInGuard: false,
  // FR-1208：正在生成 / 续传时跳过这一拍刷新。onRefresh 与流式补齐会同时
  // 写同一份界面（滚动位置、消息列表），大屏上表现为画面「跳一下」
  pauseRefreshWhileBusy: true,

  /* ---------- 会话历史（v1.12.0） ----------
     默认关闭：老页面零影响，存储格式也不变（仍是单会话裸数组）。
     开启后才写 sessions 结构，并把老的裸数组迁移成第一个会话。 */
  history: {
    enabled: false,
    max: 20,          // 最多保留多少个会话（超出按最后更新时间淘汰最旧的）
  },

  /* ---------- 移动端遥控（v1.8 / Q-10） ----------
     组件不发明传输通道：subscribe 由宿主注入（SSE / WebSocket / 轮询皆可），
     二维码也由宿主生成（qr 钩子），组件只做会话、校验、限流与审计。 */
  remote: {
    enabled: false,        // 默认关闭，老页面零影响
    session: '',           // 留空则自动生成；宿主可指定（多屏场景固定会话）
    code: '',              // 留空则自动生成；指定后二维码可预生成
    codeLength: 6,
    ttl: 900000,           // 配对码有效期(ms)，0 = 不过期
    maxLength: 2000,       // 远端单条文本上限（超出截断，不整条丢弃）
    minIntervalMs: 400,    // 限流：两条遥控消息的最小间隔
    requireCode: true,     // false = 只校验会话（弱化，仅限可信内网）
    urlBase: '',           // 手机端页面地址，默认 location.origin + '/remote'
    qr: null,              // (info) => dataURL；宿主生成二维码，组件只负责显示
    qrSrc: "",             // 或直接给一个静态图片地址
    subscribe: null,       // (onMessage) => unsubscribe；★ 宿主注入的接收通道
    authorize: null,       // (text, meta) => boolean；返回 false 或抛错即拒绝
    onVerdict: null,       // (v, msg) => void；v = { id, accepted, reason }，宿主据此回执给手机端
    onPair: null,          // (info) => void；配对信息变化（含轮换）时回调
    onExpire: null,        // () => void
  },

  /* ---------- 回调 ---------- */
  onSend: null,
  onReceive: null,
  onOpen: null,
  onClose: null,
  onError: null,
  onApproval: null,   // (status:'approved'|'rejected', block) => void；真正执行危险动作的地方
  onRefresh: null,    // () => void；自动刷新的每一拍调用它，由宿主决定重新取什么
}

/** 把 baseUrl + apiPath 拼成最终请求地址 */
export function resolveApiUrl(cfg) {
  if (cfg.apiUrl) return cfg.apiUrl
  const base = String(cfg.baseUrl || '').replace(/\/+$/, '')
  let path = String(cfg.apiPath || '')
  if (path && !path.startsWith('/')) path = '/' + path
  if (!base && !path) return '/api/chat'
  return base + path
}

export function resolveConfig(user = {}) {
  const cfg = { ...defaultConfig, ...user }

  // mock 布尔值是 mode 的兼容写法；两者都给了就以 mode 为准
  if (user.mode === undefined && user.mock !== undefined) {
    cfg.mode = user.mock ? 'mock' : 'server'
  }
  if (cfg.mode !== 'server') cfg.mode = 'mock'
  cfg.mock = cfg.mode === 'mock'

  if (user.quickReplies && !Array.isArray(user.quickReplies)) {
    cfg.quickReplies = String(user.quickReplies)
      .split(/[|,，]/)
      .map((s) => s.trim())
      .filter(Boolean)
  }
  if (cfg.position !== 'left') cfg.position = 'right'
  if (!['page', 'admin', 'bigscreen'].includes(cfg.theme)) cfg.theme = 'page'

  // 重试策略逐字段合并，避免用户只写 attempts 时把其它字段清空
  cfg.retry = { ...defaultRetry, ...(user.retry || {}) }
  // reconnect 同样逐字段合并；另外允许 reconnect:false 一次性关掉（否则 {} 展开会把默认值带回来）
  if (user.reconnect === false) cfg.reconnect = { ...defaultReconnect, attempts: 0 }
  else cfg.reconnect = { ...defaultReconnect, ...(user.reconnect || {}) }
  cfg.reconnect.attempts = Math.max(0, Number(cfg.reconnect.attempts) || 0)
  cfg.reconnect.baseDelay = Math.max(0, Number(cfg.reconnect.baseDelay) || 0)
  cfg.reconnect.maxDelay = Math.max(0, Number(cfg.reconnect.maxDelay) || 0)
  cfg.timeout = Number(cfg.timeout) || 0
  cfg.idleTimeout = Number(cfg.idleTimeout) || 0

  // 大屏专用参数的数值兜底：宁可用默认值，也不要 NaN 传播到样式里
  cfg.refreshInterval = Math.max(0, Number(cfg.refreshInterval) || 0)
  cfg.staleAfter = Math.max(0, Number(cfg.staleAfter) || 0)
  // fontScale 限制在 0.6~2.5，防止宿主传 100 把整个大屏撑爆
  const fs = Number(cfg.fontScale)
  cfg.fontScale = Number.isFinite(fs) ? Math.min(2.5, Math.max(0.6, fs)) : 1
  cfg.readOnly = !!cfg.readOnly

  // 权限与审计的兜底：类型不对就回退「不设防 / 不审计」，
  // 不能让一个写错的 role 把内容锁死、或让 audit 处在半开状态
  cfg.hideUnauthorized = !!cfg.hideUnauthorized
  if (cfg.role !== null && typeof cfg.role !== 'string') cfg.role = null
  if (cfg.role === '') cfg.role = null
  if (cfg.roles !== null && (typeof cfg.roles !== 'object' || Array.isArray(cfg.roles))) cfg.roles = null
  if (cfg.audit !== true && cfg.audit !== false && (typeof cfg.audit !== 'object' || cfg.audit === null)) cfg.audit = false
  if (cfg.roles && typeof cfg.roles === 'object' && cfg.role && cfg.roles[cfg.role] === undefined) {
    // 角色名不在表里 → 等级 0，等于什么都看不到。这是刻意的最严档，但必须让开发者看见
    if (cfg.debug) console.warn('[ai-assistant] role ' + cfg.role + ' 不在 roles 表里，该实例将看不到任何受限内容')
  }
  cfg.showFreshness = !!cfg.showFreshness
  cfg.burnInGuard = !!cfg.burnInGuard
  // 默认开启：只有显式写 false 才关掉
  cfg.pauseRefreshWhileBusy = cfg.pauseRefreshWhileBusy !== false
  // 无障碍：只有显式写 false 才关掉焦点陷阱
  cfg.trapFocus = cfg.trapFocus !== false

  // 会话历史：写错类型就退回全关，绝不让半开的历史留在页面上
  if (user.history === true) cfg.history = { ...defaultConfig.history, enabled: true }
  else if (!user.history || typeof user.history !== 'object') cfg.history = { ...defaultConfig.history }
  else cfg.history = { ...defaultConfig.history, ...user.history }
  cfg.history.enabled = !!cfg.history.enabled
  cfg.history.max = Math.min(100, Math.max(1, Number(cfg.history.max) || 20))
  if (cfg.autoFocus !== true && cfg.autoFocus !== false) cfg.autoFocus = null

  // 遥控配置兜底：写错类型就退回全关，绝不让半开的遥控留在页面上
  if (user.remote === true) cfg.remote = { ...defaultConfig.remote, enabled: true }
  else if (!user.remote || typeof user.remote !== 'object') cfg.remote = { ...defaultConfig.remote }
  else cfg.remote = { ...defaultConfig.remote, ...user.remote }
  cfg.remote.enabled = !!cfg.remote.enabled
  cfg.remote.ttl = Math.max(0, Number(cfg.remote.ttl) || 0)
  cfg.remote.maxLength = Math.max(1, Number(cfg.remote.maxLength) || 2000)
  cfg.remote.minIntervalMs = Math.max(0, Number(cfg.remote.minIntervalMs) || 0)
  cfg.remote.codeLength = Math.min(12, Math.max(4, Number(cfg.remote.codeLength) || 6))
  cfg.remote.requireCode = cfg.remote.requireCode !== false
  if (typeof cfg.remote.session !== 'string') cfg.remote.session = ''
  if (typeof cfg.remote.code !== 'string') cfg.remote.code = ''

  return cfg
}
