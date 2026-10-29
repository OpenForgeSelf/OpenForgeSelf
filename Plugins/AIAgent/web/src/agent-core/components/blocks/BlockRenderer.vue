<script setup>
/**
 * 富内容块渲染器。
 *
 * 递归说明：`group` 块内部通过 <BlockRenderer> 自引用渲染嵌套块
 * （Vue SFC 支持按文件名自引用），因此不需要额外引入 BlockList，
 * 也就避免了 BlockRenderer ↔ BlockList 的循环 import 问题。
 */
import { ref, computed, onMounted, onBeforeUnmount, watch } from 'vue'
import { renderMarkdown } from '../../utils/markdown'
import { renderChart } from '../../utils/charts'
import { getAuth } from '../../utils/auth'

const props = defineProps({
  block: { type: Object, required: true },
  config: { type: Object, required: true },
  depth: { type: Number, default: 0 },
})
const emit = defineEmits(['retry', 'resolve'])

const open = ref(props.block.open !== false && !props.block.collapsed)

/* ================= 权限门禁 =================
   必须放在模板分支最前面：Vue 计算属性只在被求值时执行，
   门禁先短路才能保证被拒绝的块**数据不进 DOM** —— 图表、表格、
   审批入参的计算属性都不会被求值，连字符串都不会出现在 shadow 里。 */
const auth = computed(() => getAuth(props.config))
const gateOk = computed(() => auth.value.allowed(props.block))
const gateLocked = computed(() => !gateOk.value && !props.config.hideUnauthorized)
const gateHidden = computed(() => !gateOk.value && !!props.config.hideUnauthorized)
const gateRole = computed(() => auth.value.requiredLabel(props.block))
const BLOCK_LABEL = {
  thinking: '思考过程', tool: '工具调用', plan: '执行计划', text: '正文', chart: '图表',
  kpi: '指标', table: '表格', citations: '引用来源', html: 'HTML 预览', code: '代码',
  image: '图片', error: '错误', skeleton: '加载占位', approval: '危险操作审批', group: '步骤分组',
}
const gateLabel = computed(() => BLOCK_LABEL[props.block.type] || props.block.type || '内容')
const copied = ref(false)

/* ================= 思考过程 ================= */
const thinkMeta = computed(() => {
  if (!props.block.done) return '思考中…'
  const ms = props.block.durationMs
  if (!ms) return '已完成'
  return ms >= 1000 ? `思考 ${(ms / 1000).toFixed(1)} 秒` : `思考 ${ms} 毫秒`
})

/* ---------- 思考块的单行摘要（对齐 2026-10-26 参考形态） ----------
   折叠态只占一行：`思考 · 首行摘要…`。原来这里右边挂「思考中… / 思考 N 秒」，
   两个信息抢同一行的宽度，长摘要直接被挤成省略号 —— 时长改到展开后的正文顶部。 */
const THINK_PEEK_MAX = 88
const thinkPreview = computed(() => {
  const raw = String(props.block.text || '').replace(/\s+/g, ' ').trim()
  if (!raw) return props.block.done ? '' : '正在推理…'
  return raw.length > THINK_PEEK_MAX ? raw.slice(0, THINK_PEEK_MAX) + '…' : raw
})

/* ================= 工具调用 ================= */
const TOOL_ICONS = {
  sql: 'M4 5c0-1.1 3.6-2 8-2s8 .9 8 2-3.6 2-8 2-8-.9-8-2Zm0 4c1.8.9 4.7 1.4 8 1.4S18.2 9.9 20 9v3c0 1.1-3.6 2-8 2s-8-.9-8-2V9Zm0 7c1.8.9 4.7 1.4 8 1.4s6.2-.5 8-1.4v3c0 1.1-3.6 2-8 2s-8-.9-8-2v-3Z',
  search: 'M10.5 3a7.5 7.5 0 1 1-4.6 13.4l-3 3-1.4-1.4 3-3A7.5 7.5 0 0 1 10.5 3Zm0 2a5.5 5.5 0 1 0 0 11 5.5 5.5 0 0 0 0-11Z',
  code: 'M9.4 16.6 4.8 12l4.6-4.6L8 6l-6 6 6 6 1.4-1.4Zm5.2 0L19.2 12l-4.6-4.6L16 6l6 6-6 6-1.4-1.4Z',
  chart: 'M4 20h16v2H4v-2ZM6 10h3v8H6v-8Zm4.5-5h3v13h-3V5Zm4.5 8h3v5h-3v-5Z',
  file: 'M6 2h7l5 5v15H6V2Zm7 1.5V8h4.5L13 3.5Z',
  globe: 'M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20Zm6.9 9h-3a15 15 0 0 0-1.2-5.6A8 8 0 0 1 18.9 11ZM12 4.2c.9 1.2 1.6 3.3 1.8 6.8h-3.6c.2-3.5.9-5.6 1.8-6.8ZM5.1 11a8 8 0 0 1 4.2-5.6A15 15 0 0 0 8.1 11h-3Zm0 2h3a15 15 0 0 0 1.2 5.6A8 8 0 0 1 5.1 13ZM12 19.8c-.9-1.2-1.6-3.3-1.8-6.8h3.6c-.2 3.5-.9 5.6-1.8 6.8Zm2.7-.8a15 15 0 0 0 1.2-5.6h3a8 8 0 0 1-4.2 5.6Z',
  trash: 'M9.5 3h5l.8 2H20v2H4V5h4.7l.8-2ZM6.2 9h11.6l-.9 11.1A2 2 0 0 1 14.9 22H9.1a2 2 0 0 1-2-1.9L6.2 9Z',
  money: 'M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20Zm1 15.9V19h-2v-1.1c-1.7-.3-3-1.3-3.2-3.1h2c.2.9 1 1.4 2.2 1.4 1.1 0 1.8-.5 1.8-1.2 0-.7-.5-1-2.1-1.4-2-.4-3.6-1.1-3.6-3 0-1.6 1.2-2.6 2.9-2.9V6h2v1.2c1.6.3 2.8 1.3 3 2.9h-2c-.2-.8-.9-1.3-1.9-1.3-1 0-1.6.5-1.6 1.1 0 .7.6 1 2.2 1.4 2 .4 3.5 1.1 3.5 3 0 1.7-1.2 2.8-3 3.1Z',
  default: 'M12 2 4 6v6c0 5 3.4 9.3 8 10 4.6-.7 8-5 8-10V6l-8-4Zm0 2.2 6 3v4.8c0 3.9-2.5 7.4-6 8.1-3.5-.7-6-4.2-6-8.1V7.2l6-3Z',
}
const toolIcon = computed(() => TOOL_ICONS[props.block.icon] || TOOL_ICONS.default)
const statusText = computed(
  () =>
    ({ running: '执行中', ok: '完成', error: '失败', awaiting: '等待审批', denied: '已拒绝' }[props.block.status] ||
    '完成')
)
const durationText = computed(() => {
  const ms = props.block.durationMs
  if (!ms) return ''
  return ms >= 1000 ? `${(ms / 1000).toFixed(1)}s` : `${ms}ms`
})
/**
 * 工具「行为」：折叠行要显示 `工具调用 · 工具名 · 行为`。
 *
 * 后端没有统一的行为字段（各工具各写各的），所以既认显式字段，也从入参里
 * 按常见命名逐个试。取不到就留空 —— 宁可少一段，也不要瞎猜一个词上去。
 */
const ACTION_KEYS = ['action', 'op', 'operation', 'command', 'cmd', 'method', 'tool', 'type', 'name']
const toolAction = computed(() => {
  const b = props.block
  if (typeof b.action === 'string' && b.action.trim()) return b.action.trim()
  if (typeof b.actionName === 'string' && b.actionName.trim()) return b.actionName.trim()
  if (b.action && typeof b.action === 'object' && typeof b.action.name === 'string') {
    const n = b.action.name.trim()
    if (n) return n
  }
  let a = b.args
  if (typeof a === 'string') {
    try {
      a = JSON.parse(a)
    } catch (e) {
      return ''
    }
  }
  if (a && typeof a === 'object') {
    for (const k of ACTION_KEYS) {
      const v = a[k]
      if (typeof v === 'string' && v.trim() && v.trim() !== b.name) return v.trim()
    }
  }
  return ''
})

const argsText = computed(() => {
  const a = props.block.args
  if (a == null) return ''
  return typeof a === 'string' ? a : JSON.stringify(a, null, 2)
})
const resultText = computed(() => {
  const r = props.block.result
  if (r == null) return ''
  return typeof r === 'string' ? r : JSON.stringify(r, null, 2)
})

/* ================= 步骤分组 ================= */
const NESTED_LABEL = { tool: '工具调用', thinking: '思考', plan: '计划', chart: '图表', table: '表格', text: '结论', kpi: '指标', code: '代码', html: '预览', citations: '引用', approval: '审批', group: '步骤' }

/** 自动汇总：没手写 summary 时按嵌套块统计，省得后端还要算一遍 */
const groupSummary = computed(() => {
  if (props.block.summary) return props.block.summary
  const list = props.block.blocks || []
  if (!list.length) return '无步骤'
  const counts = {}
  let ms = 0
  const walk = (arr) => {
    for (const b of arr) {
      counts[b.type] = (counts[b.type] || 0) + 1
      if (b.durationMs) ms += b.durationMs
      if (b.type === 'group' && b.blocks) walk(b.blocks)
    }
  }
  walk(list)
  const parts = Object.entries(counts)
    .filter(([t]) => t !== 'thinking')
    .map(([t, n]) => `${n} 个${NESTED_LABEL[t] || t}`)
  const head = `${list.length} 步`
  const tail = ms ? ` · ${(ms / 1000).toFixed(1)}s` : ''
  return parts.length ? `${head} · ${parts.join(' · ')}${tail}` : head + tail
})

/* ================= 审批（human-in-the-loop） ================= */
const RISK_META = {
  low: { label: '低风险', cls: 'low' },
  medium: { label: '中风险', cls: 'medium' },
  high: { label: '高风险', cls: 'high' },
  critical: { label: '极高风险', cls: 'critical' },
}
const risk = computed(() => RISK_META[props.block.risk] || RISK_META.medium)
const isPending = computed(() => !props.block.status || props.block.status === 'pending')
const needTyping = computed(() => !!props.block.requireConfirmText)
const typed = ref('')
// 严格全等，不做 trim：多余空格/大小写不符一律不放行（US-12-AC5）
const typedOk = computed(
  () => !needTyping.value || typed.value === String(props.block.requireConfirmText)
)

/** 倒计时：Agent 不能无限期等一个人。超时后自动作废，避免挂住的会话被误批 */
const now = ref(Date.now())
let ticker = null
const remaining = computed(() => {
  const total = Number(props.block.timeoutMs) || 0
  if (!total || !isPending.value) return null
  const start = props.block.createdAt || 0
  if (!start) return null
  return Math.max(0, start + total - now.value)
})
const remainText = computed(() => {
  const r = remaining.value
  if (r == null) return ''
  const s = Math.ceil(r / 1000)
  return s >= 60 ? `${Math.floor(s / 60)} 分 ${s % 60} 秒` : `${s} 秒`
})
const progress = computed(() => {
  const total = Number(props.block.timeoutMs) || 0
  const r = remaining.value
  if (!total || r == null) return 0
  return Math.max(0, Math.min(100, (r / total) * 100))
})

onMounted(() => {
  if (props.block.type === 'approval' && props.block.timeoutMs) {
    ticker = setInterval(() => {
      now.value = Date.now()
    }, 500)
  }
})
onBeforeUnmount(() => {
  if (ticker) clearInterval(ticker)
})
watch(
  () => [props.block.type, props.block.timeoutMs, isPending.value],
  () => {
    if (ticker) clearInterval(ticker)
    ticker = null
    if (props.block.type === 'approval' && props.block.timeoutMs && isPending.value) {
      ticker = setInterval(() => {
        now.value = Date.now()
      }, 500)
    }
  }
)

const resolvedText = computed(() => {
  const s = props.block.status
  if (s === 'approved') return '已批准'
  if (s === 'rejected') return '已拒绝'
  if (s === 'expired') return '已超时作废'
  return ''
})

function decide(decision) {
  if (!isPending.value) return
  if (decision === 'approve' && !typedOk.value) return
  emit('resolve', { id: props.block.id, decision })
}

/* ================= Markdown / 代码 ================= */
const mdHtml = computed(() => renderMarkdown(props.block.text || ''))

async function copyCode() {
  try {
    await navigator.clipboard.writeText(props.block.code || '')
    copied.value = true
    setTimeout(() => (copied.value = false), 1500)
  } catch (e) {
    /* 剪贴板不可用（非 https / 未授权）时静默失败 */
  }
}

/* ================= 图表 ================= */
const chartEl = ref(null)
/* 防御性渲染：图表渲染器抛错时不能让整个内容块消失，改为就地降级并暴露原因 */
const chartResult = computed(() => {
  if (props.config.chartRenderer) return { svg: '', error: '' }
  try {
    return { svg: renderChart(props.block.option || {}), error: '' }
  } catch (e) {
    console.warn('[ai-assistant] 图表渲染失败：', e)
    return { svg: '', error: (e && e.message) || String(e) }
  }
})
const chartHeight = computed(() => `${(props.block.option && props.block.option.height) || 150}px`)

function paintChart() {
  const fn = props.config.chartRenderer
  if (!fn || !chartEl.value) return
  try {
    fn(chartEl.value, props.block.option || {})
  } catch (e) {
    console.warn('[ai-assistant] chartRenderer 抛错：', e)
  }
}
onMounted(paintChart)
watch(() => props.block, paintChart, { deep: true })

/* ================= HTML 预览 ================= */
const showPreview = ref(true)
const htmlHeight = computed(() => `${props.block.height || 180}px`)
const srcDoc = computed(() => {
  const body = props.block.html || ''
  return `<!doctype html><html><head><meta charset="utf-8"><style>
html,body{margin:0;padding:10px;font:13px/1.6 -apple-system,'Segoe UI','PingFang SC','Microsoft YaHei',sans-serif;color:#1f2430;background:#fff}
*{box-sizing:border-box}
</style></head><body>${body}</body></html>`
})
/* ---------- 无障碍（v1.11.0） ----------
   每个块实例一个稳定 id：折叠按钮用 aria-controls 指回自己展开的正文，
   读屏才知道「这个按钮控制的是哪块内容」。实例间不能共用同一个 id。 */
const uid = Math.random().toString(36).slice(2, 8)
const bodyId = 'aiw-blk-body-' + uid
const whyId = 'aiw-blk-why-' + uid
const inputId = 'aiw-blk-cf-' + uid
</script>

<template>
  <!-- ============ 思考过程 ============ -->
  <!-- ============ 权限门禁（必须是第一个分支，理由见 script 区） ============ -->
  <div v-if="gateLocked" class="aiw-locked">
    <span class="aiw-locked__icon" aria-hidden="true">
      <svg viewBox="0 0 24 24"><path d="M12 2a5 5 0 0 0-5 5v3H6a2 2 0 0 0-2 2v8a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-8a2 2 0 0 0-2-2h-1V7a5 5 0 0 0-5-5Zm-3 8V7a3 3 0 1 1 6 0v3H9Zm4 4.7V18h-2v-3.3a2 2 0 1 1 2 0Z" fill="currentColor"/></svg>
    </span>
    <span class="aiw-locked__text">{{ gateLabel }}需要 <code>{{ gateRole || '更高' }}</code> 权限，已隐藏</span>
  </div>
  <div v-else-if="gateHidden" class="aiw-locked-slot" aria-hidden="true"></div>

  <div v-else-if="block.type === 'thinking'" class="aiw-think" :class="{ 'aiw-think--done': block.done }">
    <!-- 行首是一个图标槽：默认显块类型图标，悬浮/聚焦/展开时**同一格**换成折叠箭头。
         箭头不再作为独立元素参与排版 —— 那会让图标整体右移一格，一列消息的左缘就对不齐。 -->
    <button class="aiw-think__head" type="button" :aria-expanded="open ? 'true' : 'false'" :aria-controls="bodyId" @click="open = !open">
      <span class="aiw-ico" :class="{ 'is-open': open }">
        <span class="aiw-ico__glyph aiw-think__spark" :class="{ 'is-live': !block.done }"></span>
        <svg class="aiw-ico__chev" viewBox="0 0 24 24" aria-hidden="true"><path d="M7.4 8.6 12 13.2l4.6-4.6L18 10l-6 6-6-6z" fill="currentColor"/></svg>
      </span>
      <span class="aiw-think__label">{{ block.label || '思考' }}</span>
      <span v-if="thinkPreview" class="aiw-row__sep">·</span>
      <span v-if="thinkPreview" class="aiw-think__peek">{{ thinkPreview }}</span>
    </button>
    <div v-show="open" :id="bodyId" class="aiw-think__body">
      <div v-if="thinkMeta" class="aiw-think__meta">{{ thinkMeta }}</div>
      <div class="aiw-think__text">{{ block.text }}</div>
    </div>
  </div>

  <!-- ============ 工具调用 ============ -->
  <div
    v-else-if="block.type === 'tool'"
    class="aiw-tool"
    :class="[`aiw-tool--${block.status || 'ok'}`, { 'aiw-tool--danger': block.dangerous, 'is-open': open }]"
  >
    <!-- 折叠态：无边框单行 `图标 · 行为 · 工具名`（对齐参考形态）；
         展开后才在下方生出卡片，装着「输入 / 输出」。
         成功态不显示状态词 —— 一次调用成功了就没有可读信息，写出来只是噪声；
         执行中 / 失败保留，那是唯一需要不展开就能看见的状态。 -->
    <button class="aiw-tool__head" type="button" :aria-expanded="open ? 'true' : 'false'" :aria-controls="bodyId" @click="open = !open">
      <span class="aiw-ico" :class="{ 'is-open': open }">
        <span class="aiw-ico__glyph aiw-tool__icon"><svg viewBox="0 0 24 24" aria-hidden="true"><path :d="toolIcon" fill="currentColor"/></svg></span>
        <svg class="aiw-ico__chev" viewBox="0 0 24 24" aria-hidden="true"><path d="M7.4 8.6 12 13.2l4.6-4.6L18 10l-6 6-6-6z" fill="currentColor"/></svg>
      </span>
      <span class="aiw-tool__label">工具调用</span>
      <span class="aiw-row__sep">·</span>
      <span class="aiw-tool__name">{{ block.name }}</span>
      <template v-if="toolAction">
        <span class="aiw-row__sep">·</span>
        <span class="aiw-tool__action">{{ toolAction }}</span>
      </template>
      <span v-if="block.dangerous" class="aiw-tool__danger">写操作</span>
      <span v-if="block.status && block.status !== 'ok'" class="aiw-tool__status">{{ statusText }}</span>
      <span v-if="durationText" class="aiw-tool__dur">{{ durationText }}</span>
    </button>
    <div v-show="open" :id="bodyId" class="aiw-tool__body">
      <div v-if="argsText" class="aiw-tool__section">
        <div class="aiw-tool__k">输入</div>
        <pre class="aiw-tool__pre">{{ argsText }}</pre>
      </div>
      <div v-if="resultText" class="aiw-tool__section">
        <div class="aiw-tool__k">{{ block.status === 'error' ? '错误' : '输出' }}</div>
        <pre class="aiw-tool__pre">{{ resultText }}</pre>
      </div>
      <div v-if="!argsText && !resultText" class="aiw-tool__empty">这次调用没有留下可展开的输入或输出</div>
    </div>
  </div>

  <!-- ============ 危险操作审批 ============ -->
  <div v-else-if="block.type === 'approval'" class="aiw-appr" :class="[`aiw-appr--${risk.cls}`, { 'is-resolved': !isPending }]">
    <div class="aiw-appr__bar">
      <span class="aiw-appr__risk">{{ risk.label }}</span>
      <span class="aiw-appr__action">{{ (block.action && block.action.name) || block.actionName || '待执行操作' }}</span>
      <span v-if="isPending && remaining !== null" class="aiw-appr__timer" role="timer">剩余 {{ remainText }}</span>
      <span v-else-if="resolvedText" class="aiw-appr__done">{{ resolvedText }}</span>
    </div>

    <div v-if="isPending && remaining !== null" class="aiw-appr__track">
      <div class="aiw-appr__fill" :style="{ width: progress + '%' }"></div>
    </div>

    <div class="aiw-appr__body">
      <div v-if="block.title" class="aiw-appr__title">{{ block.title }}</div>
      <div v-if="block.text" class="aiw-appr__text" v-html="renderMarkdown(block.text)"></div>

      <div v-if="block.impact" class="aiw-appr__impact">
        <svg viewBox="0 0 24 24"><path d="M12 2 1 21h22L12 2Zm0 5.8 6.5 11.2h-13L12 7.8ZM11 11v4h2v-4h-2Zm0 5v2h2v-2h-2Z" fill="currentColor"/></svg>
        <span>{{ block.impact }}</span>
      </div>

      <div v-if="block.action && block.action.args" class="aiw-appr__args">
        <div class="aiw-tool__k">将执行</div>
        <pre class="aiw-tool__pre">{{ typeof block.action.args === 'string' ? block.action.args : JSON.stringify(block.action.args, null, 2) }}</pre>
      </div>

      <template v-if="isPending">
        <div v-if="needTyping" class="aiw-appr__confirm">
          <label :for="inputId">请输入 <code>{{ block.requireConfirmText }}</code> 以确认：</label>
          <input :id="inputId" v-model="typed" type="text" spellcheck="false" :placeholder="String(block.requireConfirmText)" :aria-describedby="whyId" />
          <span :id="whyId" class="aiw-sr-only">未输入确认词 <code>{{ block.requireConfirmText }}</code> 前，批准按钮不可用</span>
        </div>
        <div class="aiw-appr__btns">
          <button
            class="aiw-appr__btn aiw-appr__btn--ok"
            type="button"
            :disabled="!typedOk"
            :aria-disabled="typedOk ? 'false' : 'true'"
            :aria-describedby="needTyping ? whyId : null"
            @click="decide('approve')"
          >
            {{ block.approveText || '批准并执行' }}
          </button>
          <button class="aiw-appr__btn aiw-appr__btn--no" type="button" @click="decide('reject')">
            {{ block.rejectText || '拒绝' }}
          </button>
        </div>
      </template>
      <div v-else class="aiw-appr__resolved">
        {{ resolvedText }}<template v-if="block.resolvedBy"> · {{ block.resolvedBy }}</template>
        <template v-if="block.resolvedAt"> · {{ new Date(block.resolvedAt).toLocaleTimeString() }}</template>
      </div>
    </div>
  </div>

  <!-- ============ 步骤分组（可折叠，内部递归渲染） ============ -->
  <div v-else-if="block.type === 'group'" class="aiw-grp" :class="[`aiw-grp--${block.status || 'done'}`, `aiw-grp--d${depth}`]">
    <button class="aiw-grp__head" type="button" :aria-expanded="open ? 'true' : 'false'" :aria-controls="bodyId" @click="open = !open">
      <span class="aiw-ico" :class="{ 'is-open': open }">
        <span class="aiw-ico__glyph aiw-grp__badge">{{ (block.blocks || []).length }}</span>
        <svg class="aiw-ico__chev" viewBox="0 0 24 24" aria-hidden="true"><path d="M7.4 8.6 12 13.2l4.6-4.6L18 10l-6 6-6-6z" fill="currentColor"/></svg>
      </span>
      <span class="aiw-grp__title">{{ block.title || '执行过程' }}</span>
      <span class="aiw-grp__summary">{{ groupSummary }}</span>
    </button>
    <div v-show="open" :id="bodyId" class="aiw-grp__body">
      <BlockRenderer
        v-for="(b, i) in block.blocks || []"
        :key="i"
        :block="b"
        :config="config"
        :depth="depth + 1"
        @retry="emit('retry')"
        @resolve="emit('resolve', $event)"
      />
    </div>
  </div>

  <!-- ============ 执行计划 ============ -->
  <div v-else-if="block.type === 'plan'" class="aiw-plan">
    <div class="aiw-plan__title">{{ block.title || '执行计划' }}</div>
    <ol class="aiw-plan__list">
      <li v-for="(s, i) in block.steps || []" :key="i" :class="`is-${s.status || 'pending'}`">
        <span class="aiw-plan__mark"></span>
        <span class="aiw-plan__text">{{ s.title }}</span>
      </li>
    </ol>
  </div>

  <!-- ============ 图表 ============ -->
  <div v-else-if="block.type === 'chart'" class="aiw-chart">
    <div v-if="block.title" class="aiw-chart__title">{{ block.title }}</div>
    <div ref="chartEl" class="aiw-chart__box" :style="{ height: chartHeight }">
      <div v-if="chartResult.error" class="aiw-chart__empty">图表渲染失败：{{ chartResult.error }}</div>
      <div v-else-if="!config.chartRenderer" v-html="chartResult.svg"></div>
    </div>
    <div v-if="block.caption" class="aiw-chart__cap">{{ block.caption }}</div>
  </div>

  <!-- ============ KPI ============ -->
  <div v-else-if="block.type === 'kpi'" class="aiw-kpi">
    <div v-for="(k, i) in block.items || []" :key="i" class="aiw-kpi__item">
      <div class="aiw-kpi__label">{{ k.label }}</div>
      <div class="aiw-kpi__value">{{ k.value }}</div>
      <div v-if="k.delta" class="aiw-kpi__delta" :class="String(k.delta).startsWith('-') ? 'is-down' : 'is-up'">{{ k.delta }}</div>
    </div>
  </div>

  <!-- ============ 表格 ============ -->
  <div v-else-if="block.type === 'table'" class="aiw-tablewrap">
    <table class="aiw-table">
      <thead><tr><th v-for="(c, i) in block.columns || []" :key="i">{{ c }}</th></tr></thead>
      <tbody>
        <tr v-for="(r, ri) in block.rows || []" :key="ri">
          <td v-for="(c, ci) in r" :key="ci">{{ c }}</td>
        </tr>
      </tbody>
    </table>
  </div>

  <!-- ============ 引用溯源 ============ -->
  <div v-else-if="block.type === 'citations'" class="aiw-cites">
    <div class="aiw-cites__title">参考来源</div>
    <a v-for="(c, i) in block.items || []" :key="i" class="aiw-cite" :href="c.url || '#'" target="_blank" rel="noopener noreferrer">
      <span class="aiw-cite__idx">{{ i + 1 }}</span>
      <span class="aiw-cite__body">
        <span class="aiw-cite__title">{{ c.title }}</span>
        <span v-if="c.snippet" class="aiw-cite__snip">{{ c.snippet }}</span>
      </span>
    </a>
  </div>

  <!-- ============ HTML 预览 ============ -->
  <div v-else-if="block.type === 'html'" class="aiw-html">
    <div class="aiw-html__bar">
      <span class="aiw-html__title">{{ block.title || 'HTML 预览' }}</span>
      <button class="aiw-html__btn" type="button" :aria-expanded="showPreview ? 'true' : 'false'" :aria-controls="bodyId" @click="showPreview = !showPreview">{{ showPreview ? '收起' : '展开' }}</button>
    </div>
    <iframe v-show="showPreview" :id="bodyId" class="aiw-html__frame" :srcdoc="srcDoc" :style="{ height: htmlHeight }" sandbox="allow-scripts" referrerpolicy="no-referrer" loading="lazy"></iframe>
  </div>

  <!-- ============ 代码块 ============ -->
  <div v-else-if="block.type === 'code'" class="aiw-codeblk">
    <div class="aiw-codeblk__bar">
      <span>{{ block.lang || 'code' }}</span>
      <button type="button" @click="copyCode">{{ copied ? '已复制' : '复制' }}</button>
    </div>
    <pre class="aiw-code"><code>{{ block.code }}</code></pre>
  </div>

  <!-- ============ 图片 / 错误 / 骨架屏 ============ -->
  <img v-else-if="block.type === 'image'" class="aiw-img" :src="block.url" :alt="block.alt || ''" loading="lazy" />

  <div v-else-if="block.type === 'error'" class="aiw-error">
    <span>{{ block.message }}</span>
    <button v-if="block.retryable !== false" type="button" @click="emit('retry')">重试</button>
  </div>

  <div v-else-if="block.type === 'skeleton'" class="aiw-skel"><span></span><span></span><span style="width: 55%"></span></div>

  <!-- ============ 未知类型：可见提示 + 原文照出，绝不留白 ============
       后端上线了前端还不认识的新块类型时，渲染成一条明确提示，并把正文透出来，
       而不是一个空 div —— 空白等于让人以为「组件坏了」，无从排查（FR-317） -->
  <div v-else-if="block.unknownType" class="aiw-unknown">
    不支持的块类型：<code>{{ block.unknownType }}</code>
    <div v-if="block.text" class="aiw-unknown__body" v-html="mdHtml"></div>
  </div>

  <!-- ============ 默认：Markdown 文本 ============ -->
  <div v-else-if="block.text" class="aiw-md" v-html="mdHtml"></div>
  <div v-else class="aiw-unknown">空内容块：<code>{{ block.type || '未知' }}</code></div>
</template>
