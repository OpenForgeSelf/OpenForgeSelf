<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { fetchPluginVersion, toolBridgeApi } from './api'
import { clipboardAvailable, copyText } from './clipboard'
import { EMPTY_STATE_TEXT, emptyState, formatTime, statsLine, toResultRow } from './parseView'
import type { ParseResult, PromptData, ToolResult, TurnResponse, TurnSummary, WorkspaceInfo } from './types'

const PLUGIN_ID = 'tool-bridge'

const version = ref('加载中')
const prompt = ref<PromptData | null>(null)
const workspace = ref<WorkspaceInfo | null>(null)
const workspaceDraft = ref('')
const confirmUnsafe = ref(false)

const pasted = ref('')
const parsed = ref<ParseResult | null>(null)
const turn = ref<TurnResponse | null>(null)
const mode = ref<'json' | 'plain'>('json')

const turns = ref<TurnSummary[]>([])
const turnsTotal = ref(0)
const turnsNote = ref('')

const busy = ref<'' | 'parse' | 'turn' | 'workspace'>('')
const errorText = ref('')
const unauthorized = ref(false)
const copyHint = ref('')
const expanded = ref<Record<string, boolean>>({})
// 用户一旦改过工作根输入框，迟到的 loadAll 就不许再把它覆盖回去（e2e 实测竞态：
// 首屏渲染早于四个请求返回，用户填完相对路径后又被填回默认绝对路径 ⇒ 保存的是错的那个）。
const workspaceTouched = ref(false)

// 复制按钮文案恒定，每次点击都真实写剪贴板；复制结果只体现在按钮旁的独立提示里。
// 提示与 copyHint 分开：copyHint 是全页共用的一条，会被别的动作覆盖。
interface CopyNote { text: string; ok: boolean }
const promptNote = ref<CopyNote | null>(null)
const resultNote = ref<CopyNote | null>(null)
let promptCopies = 0
let resultCopies = 0

// 粘贴框焦点记录：仅「之前没有焦点」时，鼠标点击才全选；已有焦点的再次点击只放光标。
// 不挂在 focus 上触发全选——切回窗口会重触发 focus，那不是用户点击。
const pasteFocused = ref(false)

const busyLabel = computed(() =>
  busy.value === 'parse' ? '解析中…' : busy.value === 'turn' ? '执行中…' : busy.value === 'workspace' ? '保存中…' : '')

const activeResultText = computed(() =>
  !turn.value ? '' : mode.value === 'json' ? turn.value.resultTextJson : turn.value.resultTextPlain)

const rows = computed(() => (turn.value?.results ?? []).map((r, i) => toResultRow(r, i)))

const pasteEmpty = computed(() =>
  emptyState({ hasText: pasted.value.trim().length > 0, recognized: parsed.value?.calls.length ?? 0, unauthorized: unauthorized.value }))

const pasteEmptyText = computed(() => (pasteEmpty.value ? EMPTY_STATE_TEXT[pasteEmpty.value] : ''))

function noteError(e: unknown) {
  const anyErr = e as { status?: number; message?: string }
  unauthorized.value = anyErr?.status === 401
  errorText.value = anyErr?.message ? `操作失败：${anyErr.message}` : '操作失败：未知错误'
}

function clockText(): string {
  const d = new Date()
  const p = (n: number) => String(n).padStart(2, '0')
  return `${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`
}

// 每轮解析 / 执行 / 回看都要把结果区的复制提示与计数清零，免得上一轮的「已复制」带到新结果上
function resetResultCopy() {
  resultNote.value = null
  resultCopies = 0
}

function onPasteMouseDown(e: MouseEvent) {
  // 只处理左键点击；右键菜单、中键不拦截。已有焦点时放行，只放光标。
  if (e.button !== 0 || pasteFocused.value) return
  const el = e.currentTarget as HTMLTextAreaElement
  // 点在右下角缩放手柄上不拦截，免得缩放失效（18px 为经验值，未实测）
  const rect = el.getBoundingClientRect()
  if (e.clientX > rect.right - 18 && e.clientY > rect.bottom - 18) return
  // 首次点击：拦截默认行为（否则 mouseup 会把刚选中的内容取消），手动聚焦并全选
  e.preventDefault()
  el.focus()
  el.select()
}

async function loadAll() {
  errorText.value = ''
  try {
    const [v, p, w, t] = await Promise.all([
      fetchPluginVersion(PLUGIN_ID),
      toolBridgeApi.fetchPrompt(),
      toolBridgeApi.getWorkspace(),
      toolBridgeApi.listTurns(20, 0),
    ])
    version.value = v
    prompt.value = p ?? null
    workspace.value = w ?? null
    if (!workspaceTouched.value) workspaceDraft.value = w?.root ?? ''
    turns.value = t?.items ?? []
    turnsTotal.value = t?.total ?? 0
    turnsNote.value = t?.note ?? ''
  } catch (e) {
    noteError(e)
  }
}

async function doParse() {
  if (!pasted.value.trim() || busy.value) return
  busy.value = 'parse'
  errorText.value = ''
  resetResultCopy()
  try {
    parsed.value = (await toolBridgeApi.parse(pasted.value)) ?? null
    turn.value = null
  } catch (e) {
    parsed.value = null
    noteError(e)
  } finally {
    busy.value = ''
  }
}

async function doExecuteParsed() {
  const calls = parsed.value?.calls ?? []
  if (!calls.length || busy.value) return
  busy.value = 'turn'
  errorText.value = ''
  resetResultCopy()
  try {
    const data = await toolBridgeApi.execute(calls)
    if (!data) return
    // execute 不落台账（回合记录只由 turn 产生），这里显式标出来，免得用户去台账里找一条不存在的记录
    turn.value = {
      turnId: '（未落档：只执行了已解析的调用）',
      calls,
      unknown: [],
      unparsed: [],
      results: data.results,
      resultTextJson: data.resultTextJson,
      resultTextPlain: data.resultTextPlain,
      stats: {
        recognized: calls.length,
        unknown: 0,
        unparsed: 0,
        executed: data.executed,
        rejected: data.rejected,
        durationMs: data.durationMs,
      },
    }
    copyHint.value = '本轮只执行、未落台账（要留档请点「解析并执行」）'
  } catch (e) {
    noteError(e)
  } finally {
    busy.value = ''
  }
}

async function doTurn() {
  if (!pasted.value.trim() || busy.value) return
  busy.value = 'turn'
  errorText.value = ''
  resetResultCopy()
  const startedAt = Date.now()
  try {
    turn.value = (await toolBridgeApi.turn(pasted.value)) ?? null
    parsed.value = turn.value
      ? { calls: turn.value.calls, unknown: turn.value.unknown, unparsed: turn.value.unparsed, stats: turn.value.stats }
      : null
    const list = await toolBridgeApi.listTurns(20, 0)
    turns.value = list?.items ?? turns.value
    turnsTotal.value = list?.total ?? turnsTotal.value
    if (turn.value?.ledgerError) errorText.value = turn.value.ledgerError
    else copyHint.value = `本轮耗时 ${Date.now() - startedAt}ms，结果已就绪，复制后粘回 AI`
  } catch (e) {
    noteError(e)
  } finally {
    busy.value = ''
  }
}

async function saveWorkspace() {
  if (busy.value) return
  busy.value = 'workspace'
  errorText.value = ''
  copyHint.value = ''
  try {
    const next = await toolBridgeApi.setWorkspace(workspaceDraft.value.trim(), confirmUnsafe.value)
    if (next) {
      workspace.value = next
      // 点即保存后必须回读确认（§3.4 之 1：界面状态与持久化一致）
      const back = await toolBridgeApi.getWorkspace()
      if (back) workspace.value = back
      workspaceDraft.value = workspace.value.root
      copyHint.value = `工作根已保存并回读一致：${workspace.value.root}`
    }
  } catch (e) {
    noteError(e)
  } finally {
    busy.value = ''
  }
}

async function copy(key: 'prompt' | 'result') {
  const text = key === 'prompt' ? prompt.value?.text ?? '' : activeResultText.value
  const setNote = (n: CopyNote) => {
    if (key === 'prompt') promptNote.value = n
    else resultNote.value = n
  }
  if (!text) {
    setNote({ text: '没有可复制的内容', ok: false })
    return
  }
  const target = document.getElementById(key === 'prompt' ? 'tb-prompt-text' : 'tb-result-text')
  const outcome = await copyText(key, text, {
    write: async t => {
      if (!clipboardAvailable()) throw new Error('clipboard-unavailable')
      await navigator.clipboard.writeText(t)
    },
    select: () => {
      if (!target) return
      const range = document.createRange()
      range.selectNodeContents(target)
      const sel = window.getSelection()
      sel?.removeAllRanges()
      sel?.addRange(range)
    },
  })
  if (outcome.kind === 'ok') {
    const n = key === 'prompt' ? ++promptCopies : ++resultCopies
    setNote({ text: `已复制（第 ${n} 次）· ${clockText()} · ${text.length} 字符`, ok: true })
  } else {
    setNote({ text: '自动复制不可用，已选中文本，请按 Ctrl+C', ok: false })
  }
}

async function reloadTurn(item: TurnSummary) {
  errorText.value = ''
  try {
    const data = (await toolBridgeApi.getTurn(item.turnId)) as TurnResponse | undefined
    if (!data) return
    pasted.value = data.text ?? ''
    turn.value = data
    parsed.value = { calls: data.calls, unknown: data.unknown, unparsed: data.unparsed, stats: data.stats }
    resetResultCopy()
    copyHint.value = `已载入回合 ${item.turnId}（回看，不会自动重新执行）`
  } catch (e) {
    noteError(e)
  }
}

function toggle(id: string) {
  expanded.value = { ...expanded.value, [id]: !expanded.value[id] }
}

/** 收起态也要能一眼看出"这条带几个参数"，否则展开前没有任何信息量。 */
function argCount(args: Record<string, unknown> | undefined): number {
  return args ? Object.keys(args).length : 0
}

// 新一轮解析/执行 ⇒ 展开态归零（默认收起），上一轮点开的项目不该串到这一轮。
watch(parsed, () => {
  expanded.value = {}
})

onMounted(loadAll)
</script>

<template>
  <div class="tb-root">
    <header class="tb-header">
      <h2 class="view-title">工具桥</h2>
      <span class="tb-version" :title="`插件版本（来自宿主 /api/plugin）`">v{{ version }}</span>
      <span class="tb-sub">把外部 AI 的工具调用接到本机：指令出去 · 调用回来 · 结果原样送回</span>
    </header>

    <div v-if="errorText" class="tb-alert tb-alert--error" data-testid="tb-error">
      <span>{{ errorText }}</span>
      <button type="button" class="tb-btn tb-btn--ghost" @click="errorText = ''">知道了</button>
    </div>

    <section class="tb-card" data-testid="tb-workspace">
      <div class="tb-card__head">
        <h3>工作根（沙箱）</h3>
        <span class="tb-tag" :class="workspace?.dangerous ? 'tb-tag--warn' : ''">
          {{ workspace?.dangerous ? '危险根（已确认）' : workspace?.source === 'settings' ? '自定义' : '默认' }}
        </span>
      </div>
      <p class="tb-hint">
        读文件 / 写文件 / 列目录 / 命令的工作目录都在这个根内；越界一律拒绝。当前：
        <code>{{ workspace?.root || '加载中…' }}</code>
        <span v-if="workspace && !workspace.exists">（目录尚未创建，首次写入时自动建）</span>
      </p>
      <div class="tb-row">
        <input v-model="workspaceDraft" class="tb-input" data-testid="tb-workspace-input"
          placeholder="绝对路径，如 D:\scratch\agent-lab" @input="workspaceTouched = true" />
        <label class="tb-check"><input v-model="confirmUnsafe" type="checkbox" /> 我确认使用危险根（盘符根 / 用户目录根 / Git 仓库树内）</label>
        <button type="button" class="tb-btn" :disabled="busy !== ''" data-testid="tb-workspace-save" @click="saveWorkspace">
          {{ busy === 'workspace' ? '保存中…' : '保存工作根' }}
        </button>
      </div>
    </section>

    <section class="tb-card" data-testid="tb-prompt">
      <div class="tb-card__head">
        <h3>1 · 初始指令（发给网页 AI）</h3>
        <button type="button" class="tb-btn" data-testid="tb-copy-prompt" @click="copy('prompt')">复制初始指令</button>
        <span
          v-if="promptNote"
          class="tb-copy-note"
          :class="promptNote.ok ? '' : 'tb-copy-note--warn'"
          role="status"
          data-testid="tb-copy-prompt-note"
        >{{ promptNote.text }}</span>
      </div>
      <pre id="tb-prompt-text" class="tb-pre">{{ prompt?.text || '正在读取…' }}</pre>
      <details class="tb-details">
        <summary>支持的工具（{{ prompt?.tools.length ?? 0 }} 个，与解析器同源）</summary>
        <ul class="tb-tools">
          <li v-for="t in prompt?.tools ?? []" :key="t.name">
            <code>{{ t.name }}</code>
            <span>{{ t.description }}</span>
            <em v-if="t.required?.length">必填：{{ t.required.join(', ') }}</em>
          </li>
        </ul>
      </details>
    </section>

    <section class="tb-card" data-testid="tb-paste">
      <div class="tb-card__head">
        <h3>2 · 粘贴 AI 的回复</h3>
        <div class="tb-actions">
          <button type="button" class="tb-btn tb-btn--ghost" :disabled="busy !== '' || !pasted.trim()" data-testid="tb-parse" @click="doParse">
            {{ busyLabel === '解析中…' ? '解析中…' : '只解析（不执行）' }}
          </button>
          <button type="button" class="tb-btn tb-btn--ghost" :disabled="busy !== '' || !parsed?.calls.length" data-testid="tb-execute" @click="doExecuteParsed">
            只执行已解析（不落档）
          </button>
          <button type="button" class="tb-btn" :disabled="busy !== '' || !pasted.trim()" data-testid="tb-turn" @click="doTurn">
            {{ busyLabel === '执行中…' ? '执行中…' : '解析并执行' }}
          </button>
        </div>
      </div>
      <textarea
        v-model="pasted"
        class="tb-textarea"
        rows="8"
        spellcheck="false"
        data-testid="tb-paste-input"
        placeholder="把 AI 回复整段粘进来（json 围栏 / 无围栏的裸 json / 标签式 / key=value 行 / &gt; exec: 单动作都认）"
        @mousedown="onPasteMouseDown"
        @focus="pasteFocused = true"
        @blur="pasteFocused = false"
      ></textarea>
      <p v-if="pasteEmptyText" class="tb-empty" data-testid="tb-empty">{{ pasteEmptyText }}</p>

      <div v-if="parsed" class="tb-stats" data-testid="tb-stats">{{ statsLine(parsed.stats) }}</div>

      <div v-if="parsed && parsed.calls.length" class="tb-list">
        <h4>识别到的调用</h4>
        <div v-for="(c, i) in parsed.calls" :key="`c${i}`" class="tb-item tb-item--ok">
          <button
            type="button"
            class="tb-item__head tb-item__head--btn"
            :data-testid="`tb-call-head-${i}`"
            :aria-expanded="expanded[`c${i}`] ? true : false"
            @click="toggle(`c${i}`)"
          >
            <code>{{ c.tool }}</code>
            <span class="tb-via">via {{ c.via }}</span>
            <span v-if="c.rawName !== c.tool" class="tb-muted">原文写作 {{ c.rawName }}</span>
            <span class="tb-muted">{{ argCount(c.args) }} 个参数 · {{ expanded[`c${i}`] ? '收起' : '点展开' }}</span>
          </button>
          <div v-if="expanded[`c${i}`]" class="tb-item__body" :data-testid="`tb-call-body-${i}`">
            <pre class="tb-pre tb-pre--mini">{{ JSON.stringify(c.args, null, 2) }}</pre>
          </div>
        </div>
      </div>

      <div v-if="parsed && parsed.unknown.length" class="tb-list">
        <h4>认出来了，但工具不在清单（未执行）</h4>
        <div v-for="(u, i) in parsed.unknown" :key="`u${i}`" class="tb-item tb-item--warn">
          <button
            type="button"
            class="tb-item__head tb-item__head--btn"
            :data-testid="`tb-unknown-head-${i}`"
            :aria-expanded="expanded[`u${i}`] ? true : false"
            @click="toggle(`u${i}`)"
          >
            <code>{{ u.rawName }}</code>
            <span class="tb-muted">{{ u.suggestion ? `最接近：${u.suggestion}` : '无相近工具' }}</span>
            <span class="tb-muted">{{ expanded[`u${i}`] ? '收起' : '点展开看原因' }}</span>
          </button>
          <div v-if="expanded[`u${i}`]" class="tb-item__body" :data-testid="`tb-unknown-body-${i}`">
            <p class="tb-reason">{{ u.reason }}</p>
          </div>
        </div>
      </div>

      <div v-if="parsed && parsed.unparsed.length" class="tb-list">
        <h4>没认出来（原样留档，插件不猜）</h4>
        <div v-for="(p, i) in parsed.unparsed" :key="`p${i}`" class="tb-item tb-item--muted">
          <!-- 成因（reason）默认就露在外面：它是"为什么少了一条"的唯一线索，收起来就等于让用户猜。
               只把占地方原文片段（fragment）折进展开态。 -->
          <button
            v-if="p.fragment"
            type="button"
            class="tb-item__head tb-item__head--btn"
            :data-testid="`tb-unparsed-head-${i}`"
            :aria-expanded="expanded[`p${i}`] ? true : false"
            @click="toggle(`p${i}`)"
          >
            <span class="tb-reason tb-reason--inbtn">{{ p.reason }}</span>
            <span class="tb-muted">{{ expanded[`p${i}`] ? '收起' : '点展开看原文片段' }}</span>
          </button>
          <template v-else>
            <p class="tb-reason">{{ p.reason }}</p>
          </template>
          <div v-if="p.fragment && expanded[`p${i}`]" class="tb-item__body" :data-testid="`tb-unparsed-body-${i}`">
            <pre class="tb-pre tb-pre--mini">{{ p.fragment }}</pre>
          </div>
        </div>
      </div>
    </section>

    <section v-if="turn" class="tb-card" data-testid="tb-result">
      <div class="tb-card__head">
        <h3>3 · 执行结果（粘回给 AI）</h3>
        <div class="tb-actions">
          <label class="tb-check">
            <input v-model="mode" type="radio" value="json" data-testid="tb-mode-json" /> json
          </label>
          <label class="tb-check">
            <input v-model="mode" type="radio" value="plain" data-testid="tb-mode-plain" /> plain
          </label>
          <button type="button" class="tb-btn" data-testid="tb-copy-result" @click="copy('result')">复制结果</button>
          <span
            v-if="resultNote"
            class="tb-copy-note"
            :class="resultNote.ok ? '' : 'tb-copy-note--warn'"
            role="status"
            data-testid="tb-copy-result-note"
          >{{ resultNote.text }}</span>
        </div>
      </div>

      <div class="tb-stats">{{ statsLine(turn.stats) }}</div>
      <p v-if="turn.ledgerError" class="tb-alert tb-alert--warn" data-testid="tb-ledger-error">{{ turn.ledgerError }}</p>

      <div class="tb-list">
        <div v-for="row in rows" :key="row.id" class="tb-item" :class="row.ok ? 'tb-item--ok' : 'tb-item--warn'">
          <button type="button" class="tb-item__head tb-item__head--btn" @click="toggle(row.id)">
            <span>{{ row.ok ? '✓' : '✕' }}</span>
            <code>{{ row.headline }}</code>
            <span class="tb-muted">{{ row.lines.length }} 项 · 点展开</span>
          </button>
          <div v-if="expanded[row.id]" class="tb-item__body">
            <div v-for="line in row.lines" :key="line.label" class="tb-line">
              <span class="tb-line__label">{{ line.label }}</span>
              <pre v-if="line.raw" class="tb-pre tb-pre--raw">{{ line.value }}</pre>
              <span v-else class="tb-line__value">{{ line.value }}</span>
            </div>
          </div>
        </div>
      </div>

      <pre id="tb-result-text" class="tb-pre tb-pre--result" data-testid="tb-result-text">{{ activeResultText }}</pre>
    </section>

    <section class="tb-card" data-testid="tb-ledger">
      <div class="tb-card__head">
        <h3>4 · 回合记录（{{ turnsTotal }} 条）</h3>
        <button type="button" class="tb-btn tb-btn--ghost" :disabled="busy !== ''" @click="loadAll">重新读取</button>
      </div>
      <p v-if="!turns.length" class="tb-empty">{{ EMPTY_STATE_TEXT['no-turn'] }}</p>
      <ul v-else class="tb-turns">
        <li v-for="t in turns" :key="t.turnId">
          <button type="button" class="tb-turn" data-testid="tb-turn-row" @click="reloadTurn(t)">
            <span class="tb-turn__time">{{ formatTime(t.createdAt) }}</span>
            <span class="tb-turn__stats">{{ statsLine(t.stats) }}</span>
            <span class="tb-turn__preview">{{ t.corrupt ? '（记录损坏）' : t.preview }}</span>
          </button>
        </li>
      </ul>
      <p class="tb-muted">{{ turnsNote }}</p>
    </section>

    <p v-if="copyHint" class="tb-hint tb-hint--last" data-testid="tb-hint">{{ copyHint }}</p>
  </div>
</template>

<style scoped>
/* 根 = 高度填满 + 内层滚动；滚动容器的直接子区块必须 flex-shrink:0（plugin-development 铁律 8），
   否则视口一矮区块就被压扁、叠 overflow:hidden 后文字被裁。 */
.tb-root {
  height: 100%;
  display: flex;
  flex-direction: column;
  gap: 14px;
  overflow-y: auto;
  padding: 16px 18px 28px;
  box-sizing: border-box;
  color: var(--el-text-color-primary);
  background: var(--el-bg-color);
}
.tb-root > * { flex-shrink: 0; }

.tb-header { display: flex; align-items: baseline; gap: 10px; flex-wrap: wrap; }
.view-title { margin: 0; font-size: 20px; font-weight: 600; }
.tb-version {
  font-size: 12px;
  padding: 2px 8px;
  border-radius: 10px;
  background: var(--el-fill-color-light);
  color: var(--el-text-color-secondary);
}
.tb-sub { font-size: 13px; color: var(--el-text-color-secondary); }

.tb-card {
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 10px;
  background: var(--el-fill-color-lighter);
  padding: 12px 14px;
}
.tb-card__head { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; margin-bottom: 8px; }
.tb-card__head h3 { margin: 0; font-size: 15px; font-weight: 600; }

.tb-actions { margin-left: auto; display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
.tb-btn {
  border: 1px solid var(--el-color-primary);
  background: var(--el-color-primary);
  color: #fff;
  border-radius: 6px;
  padding: 6px 12px;
  font-size: 13px;
  cursor: pointer;
}
.tb-btn--ghost { background: transparent; color: var(--el-color-primary); }
.tb-btn:disabled { opacity: 0.55; cursor: not-allowed; }

/* 复制提示：紧挨按钮，成功用成功色，回退/空内容用警告色 */
.tb-copy-note { font-size: 12.5px; color: var(--el-color-success); word-break: break-all; }
.tb-copy-note--warn { color: var(--el-color-warning); }

.tb-tag { font-size: 12px; padding: 2px 8px; border-radius: 8px; background: var(--el-fill-color); color: var(--el-text-color-secondary); }
.tb-tag--warn { background: var(--el-color-warning-light-9); color: var(--el-color-warning); }

.tb-hint { margin: 4px 0 8px; font-size: 12.5px; color: var(--el-text-color-secondary); word-break: break-all; }
.tb-hint--last { text-align: right; }
.tb-muted { font-size: 12px; color: var(--el-text-color-secondary); }
.tb-empty { margin: 8px 0 0; font-size: 13px; color: var(--el-text-color-secondary); }

.tb-alert { display: flex; align-items: center; gap: 10px; font-size: 13px; border-radius: 8px; padding: 8px 12px; }
.tb-alert--error { background: var(--el-color-danger-light-9); color: var(--el-color-danger); }
.tb-alert--warn { background: var(--el-color-warning-light-9); color: var(--el-color-warning); }

.tb-row { display: flex; flex-direction: column; gap: 8px; align-items: flex-start; }
/* .tb-row 是 flex 列，子项默认 stretch 会把按钮拉成一整条（截图读图发现：主按钮占满整行、视觉过重） */
.tb-row .tb-btn { align-self: flex-start; }
.tb-input {
  width: 100%;
  box-sizing: border-box;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  padding: 7px 10px;
  font-size: 13px;
  background: var(--el-bg-color);
  color: var(--el-text-color-primary);
}
.tb-check { font-size: 12.5px; color: var(--el-text-color-secondary); display: inline-flex; align-items: center; gap: 5px; }

.tb-textarea {
  width: 100%;
  box-sizing: border-box;
  border: 1px solid var(--el-border-color);
  border-radius: 8px;
  padding: 10px;
  font-family: var(--el-font-family-mono, ui-monospace, monospace);
  font-size: 13px;
  resize: vertical;
  background: var(--el-bg-color);
  color: var(--el-text-color-primary);
}

.tb-pre {
  margin: 8px 0 0;
  padding: 10px;
  border-radius: 8px;
  background: var(--el-bg-color);
  border: 1px solid var(--el-border-color-lighter);
  font-family: var(--el-font-family-mono, ui-monospace, monospace);
  font-size: 12.5px;
  line-height: 1.55;
  white-space: pre-wrap;
  word-break: break-word;
  max-height: 300px;
  overflow: auto;
}
.tb-pre--mini { max-height: 120px; margin: 6px 0 0; }
.tb-pre--result { max-height: 360px; }
.tb-pre--raw { background: var(--el-fill-color-lighter); }

.tb-details { margin-top: 8px; font-size: 13px; }
.tb-tools { margin: 6px 0 0; padding-left: 18px; display: flex; flex-direction: column; gap: 6px; }
.tb-tools code { color: var(--el-color-primary); }
.tb-tools em { display: block; font-style: normal; font-size: 12px; color: var(--el-text-color-secondary); }

.tb-stats { margin-top: 6px; font-size: 12.5px; color: var(--el-text-color-secondary); }
.tb-list { margin-top: 10px; display: flex; flex-direction: column; gap: 8px; }
.tb-list h4 { margin: 0; font-size: 13px; font-weight: 600; color: var(--el-text-color-regular); }
.tb-item { border: 1px solid var(--el-border-color-lighter); border-radius: 8px; background: var(--el-bg-color); padding: 8px 10px; }
.tb-item--ok { border-left: 3px solid var(--el-color-success); }
.tb-item--warn { border-left: 3px solid var(--el-color-warning); }
.tb-item--muted { border-left: 3px solid var(--el-border-color); }
.tb-item__head { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
.tb-item__head--btn { background: none; border: none; cursor: pointer; padding: 0; text-align: left; color: inherit; font-size: 13px; }
.tb-item__body { margin-top: 8px; display: flex; flex-direction: column; gap: 8px; }
.tb-via { font-size: 11.5px; color: var(--el-color-primary); }
.tb-reason { margin: 6px 0 0; font-size: 12.5px; color: var(--el-text-color-regular); }
/* 按钮里的成因行：<p> 的 margin 在 button 内不可靠，改用块级 span 自撑一行。 */
.tb-reason--inbtn { display: block; margin: 0 0 2px; text-align: left; }
.tb-line { display: flex; flex-direction: column; gap: 4px; }
.tb-line__label { font-size: 12px; color: var(--el-text-color-secondary); }
.tb-line__value { font-size: 13px; word-break: break-all; }

.tb-turns { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 6px; }
.tb-turn {
  width: 100%;
  display: grid;
  grid-template-columns: 170px 1fr;
  gap: 4px 12px;
  background: var(--el-bg-color);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 8px;
  padding: 8px 10px;
  cursor: pointer;
  text-align: left;
  color: inherit;
  font-size: 12.5px;
}
.tb-turn__time { color: var(--el-text-color-secondary); }
.tb-turn__stats { grid-column: 2; color: var(--el-color-primary); }
.tb-turn__preview { grid-column: 2; color: var(--el-text-color-regular); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

@media (max-width: 900px) {
  .tb-actions { margin-left: 0; width: 100%; }
  .tb-turn { grid-template-columns: 1fr; }
  .tb-turn__stats, .tb-turn__preview { grid-column: 1; }
}
</style>
