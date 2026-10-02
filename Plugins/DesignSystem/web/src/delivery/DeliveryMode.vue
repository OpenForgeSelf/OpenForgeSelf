<script setup lang="ts">
/**
 * 交付与接入模式（FR13 / AC18–AC21）。
 *
 * 六张卡：① MCP 接入 ② 给 AI 的使用规则 ③ 设计说明书 ④ 交付文件 ⑤ AI 可用工具 ⑥ 贴代码试审查。
 *
 * 三条硬纪律：
 * - **同源**：文本一律来自后端（`export?format=` / `review` / `agent/tools`），前端只渲染原文；
 * - **SECURITY**：MCP 片段里只写占位符，令牌状态只报"已配置/未配置"，**绝不渲染掩码或明文**
 *   （掩码含真令牌首尾片段，反查也会命中，所以连掩码也不摆）；
 * - **客户端先拦**：试审查 > 200KB 直接就地报错，不发一个注定被拒的请求。
 *
 * §U DOM 契约（e2e 判据）：各卡 `[data-dv="mcp|rules|brief|files|tools|review"]`；
 * 文本区 `<pre data-dv-text="agent-rules|brief">`；开关 `role="switch" aria-label="允许 AI 修改设计"`；
 * 审查输入 `aria-label="待审查代码"`、语言 `aria-label="代码语言"`、按钮可访问名 `开始审查`；
 * 复制按钮可访问名 `复制`（回退时出现 `[data-copy-fallback]`）。
 */
import { onMounted, ref, watch } from 'vue'
import { api, type AgentTool, type McpConfig, type ReviewResult } from '../api'
import { ApiError } from '../http'
import { currentProject, meta, themeCode } from '../state'
import { term } from '../design/glossary'
import { createLatest } from '../design/latest'
import PanelState from '../components/PanelState.vue'
import { deliveryFormats, mcpEndpoint, mcpSnippet, reviewTooLarge, byteLength, REVIEW_LIMIT_BYTES } from './snippets'

/** 试审查支持的语言（与后端 `DesignScanner.Scan` 的 switch 口径一致；不含 sass/styl，它们会被跳过） */
const LANGUAGES: readonly string[] = ['css', 'scss', 'less', 'vue', 'html', 'ts', 'js', 'tsx', 'jsx']

/* ------------------------------------------------------------------ */
/* 状态                                                                */
/* ------------------------------------------------------------------ */

const loading = ref(true)

/** MCP 接入卡 */
const mcp = ref<McpConfig | null>(null)
const mcpError = ref('')

/** 工具卡 */
const tools = ref<AgentTool[]>([])
const toolsError = ref('')
const allowWrite = ref(false)
const accessError = ref('')
const accessBusy = ref(false)

/** 规则 / 说明书卡（文本来自后端导出） */
const rulesText = ref('')
const rulesError = ref('')
const briefText = ref('')
const briefError = ref('')
const docsLoading = ref(false)

/** 试审查卡 */
const reviewCode = ref('')
const reviewLang = ref('css')
const reviewBusy = ref(false)
const reviewResult = ref<ReviewResult | null>(null)
const reviewError = ref('')

/** 复制反馈：`copied` = 最近成功的按钮键；`fallback` = 回退提示（含所属卡键） */
const copied = ref('')
const fallback = ref<{ key: string; msg: string } | null>(null)

/** 各卡 `<pre>` 引用（回退复制时用于选中文本） */
const mcpPre = ref<HTMLElement | null>(null)
const rulesPre = ref<HTMLElement | null>(null)
const briefPre = ref<HTMLElement | null>(null)

/* ------------------------------------------------------------------ */
/* 取数                                                                */
/* ------------------------------------------------------------------ */

/** 错误一律显示后端原文（不降级成"加载失败"） */
function errText(e: unknown): string {
  return e instanceof ApiError ? `${e.status} ${e.message}` : String(e)
}

/** 读 MCP 中心网关配置（跨插件只读） */
async function loadMcp(): Promise<void> {
  mcpError.value = ''
  try {
    mcp.value = await api.getMcpConfig()
  } catch (e) {
    mcpError.value = errText(e)
  }
}

/** 读工具表（与 meta.agentTools / design_guide 同源）*/
async function loadTools(): Promise<void> {
  toolsError.value = ''
  try {
    tools.value = await api.listAgentTools()
  } catch (e) {
    toolsError.value = errText(e)
  }
}

/** 读写开关（AI 是否能改设计）*/
async function loadAccess(): Promise<void> {
  accessError.value = ''
  try {
    const a = await api.getAgentAccess()
    allowWrite.value = a.allowWrite
  } catch (e) {
    accessError.value = errText(e)
  }
}

/** 读当前项目的规则 / 说明书文本（带序号守卫：切项目时旧响应不覆盖新结果）*/
async function loadDocs(): Promise<void> {
  const p = currentProject.value
  if (!p) {
    rulesText.value = ''
    briefText.value = ''
    return
  }
  docsLoading.value = true
  const run = createLatest()
  rulesError.value = ''
  briefError.value = ''
  try {
    const rules = await api.exportText(p.id, 'agent-rules', themeCode.value)
    if (run.isCurrent()) rulesText.value = rules
  } catch (e) {
    if (run.isCurrent()) rulesError.value = errText(e)
  }
  try {
    const brief = await api.exportText(p.id, 'brief', themeCode.value)
    if (run.isCurrent()) briefText.value = brief
  } catch (e) {
    if (run.isCurrent()) briefError.value = errText(e)
  }
  if (run.isCurrent()) docsLoading.value = false
}

async function load(): Promise<void> {
  loading.value = true
  await Promise.all([loadMcp(), loadTools(), loadAccess()])
  await loadDocs()
  loading.value = false
}

onMounted(() => {
  void load()
})

// 换项目 → 重取规则/说明书（导出文本随项目变）
watch(currentProject, () => {
  void loadDocs()
})

/* ------------------------------------------------------------------ */
/* MCP 片段与地址                                                       */
/* ------------------------------------------------------------------ */

/** 网关地址（`listenUrl + /mcp`）；未运行时为空串，界面给提示而不是给个假地址 */
function gatewayUrl(): string {
  return mcp.value ? mcpEndpoint(mcp.value) : ''
}

/** 通用 Streamable HTTP 配置片段（含占位凭据；hasToken=false 时无 headers） */
function gatewaySnippet(): string {
  return mcp.value ? mcpSnippet(mcp.value) : ''
}

/* ------------------------------------------------------------------ */
/* 工具写开关                                                           */
/* ------------------------------------------------------------------ */

/** 切换「允许 AI 修改设计」：PUT 后重读确认，界面显示的一定是服务端真值 */
async function toggleAccess(): Promise<void> {
  if (accessBusy.value) return
  accessBusy.value = true
  accessError.value = ''
  try {
    await api.putAgentAccess(!allowWrite.value)
    const fresh = await api.getAgentAccess()
    allowWrite.value = fresh.allowWrite
  } catch (e) {
    accessError.value = errText(e)
  } finally {
    accessBusy.value = false
  }
}

/* ------------------------------------------------------------------ */
/* 交付文件直链                                                         */
/* ------------------------------------------------------------------ */

/** 只列后端真声明且属于交付首选的格式（不给"能下载"的假象）*/
function formats(): string[] {
  return deliveryFormats(meta.value?.exportFormats ?? [])
}

/** 某格式的下载直链（路径由 api.ts 统一拼，前端不手写 URL）*/
function downloadUrl(format: string): string {
  const p = currentProject.value
  return p ? api.exportUrl(p.id, format, themeCode.value) : ''
}

/* ------------------------------------------------------------------ */
/* 试审查                                                               */
/* ------------------------------------------------------------------ */

/** 跑一次试审查：客户端先拦 > 200KB；请求与 `POST projects/{id}/review` 完全同源 */
async function runReview(): Promise<void> {
  const p = currentProject.value
  if (!p || reviewBusy.value) return
  reviewError.value = ''
  reviewResult.value = null
  if (!reviewCode.value.trim()) {
    reviewError.value = '请先贴一段代码。'
    return
  }
  if (reviewTooLarge(reviewCode.value)) {
    reviewError.value = `代码有 ${byteLength(reviewCode.value)} 字节，超过 ${REVIEW_LIMIT_BYTES} 字节上限，请先精简。`
    return
  }
  reviewBusy.value = true
  try {
    reviewResult.value = await api.reviewCode(p.id, {
      files: [{ path: `snippet.${reviewLang.value}`, content: reviewCode.value, language: reviewLang.value }],
      theme: themeCode.value,
      maxFindings: 20,
    })
  } catch (e) {
    reviewError.value = errText(e)
  } finally {
    reviewBusy.value = false
  }
}

/* ------------------------------------------------------------------ */
/* 复制（含回退）                                                       */
/* ------------------------------------------------------------------ */

/** 选中某元素全部文本（复制 API 不可用时的回退路径，用户手动 Ctrl+C）*/
function selectElementText(el: HTMLElement | null): void {
  if (!el) return
  const sel = window.getSelection()
  if (!sel) return
  const range = document.createRange()
  range.selectNodeContents(el)
  sel.removeAllRanges()
  sel.addRange(range)
}

/**
 * 复制文本：优先用异步剪贴板 API；不可用/被拒时回退为"选中文本 + 提示"（不静默失败）。
 * @param key 按钮所属卡键（用于定位回退提示与"已复制"态）
 * @param text 待复制文本
 * @param el 回退时选中的元素（通常是该卡的 `<pre>`）
 */
async function copyText(key: string, text: string, el: HTMLElement | null): Promise<void> {
  copied.value = ''
  fallback.value = null
  try {
    if (!navigator.clipboard?.writeText) throw new Error('clipboard-unavailable')
    await navigator.clipboard.writeText(text)
    copied.value = key
  } catch {
    selectElementText(el)
    fallback.value = { key, msg: '自动复制不可用，已为你选中文本，请按 Ctrl+C' }
  }
}
</script>

<template>
  <section class="ds-mode-pane dv" data-delivery>
    <header class="dv__head ds-stack ds-gap-1">
      <h2 class="ds-h3">交付与接入</h2>
      <p class="ds-small">
        把「{{ term('design system') }}」交给同事或 AI：接入 MCP 网关、下载{{ term('export') }}文件、
        让 AI 照着规则写、写错了还能当场审查。
      </p>
    </header>

    <PanelState
      v-if="!currentProject"
      state="empty"
      title="还没有选中的设计系统"
      hint="「使用规则」「设计说明书」「交付文件」「试审查」需要一个项目；先到「开始」建一套，或在顶部下拉里选一个。MCP 接入与工具卡不受影响。"
    />

    <div class="dv__grid">
      <!-- ① AI 接入（MCP）-->
      <article class="dv-card ds-surface ds-stack ds-gap-3" data-dv="mcp">
        <h3 class="ds-h4">AI 接入</h3>
        <p v-if="loading && !mcp && !mcpError" class="ds-small">正在读取 MCP 网关配置…</p>
        <p v-else-if="mcpError" class="dv-err ds-small" role="alert">读取失败：{{ mcpError }}</p>
        <template v-else-if="mcp">
          <div class="dv-facts ds-stack ds-gap-1">
            <div class="ds-row ds-gap-2">
              <span class="ds-micro">网关地址</span>
              <span class="ds-mono" data-mcp-url>{{ gatewayUrl() || '（未运行，暂无地址）' }}</span>
            </div>
            <div class="ds-row ds-gap-2">
              <span class="ds-micro">运行状态</span>
              <span v-if="mcp.isRunning" class="ds-small">运行中（监听 {{ mcp.listenHost }}:{{ mcp.port }}）</span>
              <span v-else class="dv-warn-text ds-small" data-mcp-stopped>
                未运行：外部客户端此刻连不上。请先在宿主「MCP 中心」启动网关后再接入。
              </span>
            </div>
            <div class="ds-row ds-gap-2">
              <span class="ds-micro">令牌</span>
              <!-- SECURITY：只报"已配置/未配置"，连掩码都不渲染 -->
              <span v-if="mcp.hasToken" class="ds-small" data-mcp-token="configured">已配置（片段里只写占位符，真实令牌请你自己填）</span>
              <span v-else class="ds-small" data-mcp-token="none">未配置（无需凭据即可连）</span>
            </div>
          </div>

          <div class="ds-stack ds-gap-2">
            <div class="ds-row ds-gap-2 dv-row-between">
              <span class="ds-small">通用 Streamable HTTP 配置片段</span>
              <span class="ds-row ds-gap-2">
                <button type="button" class="dv-btn" @click="copyText('mcp', gatewaySnippet(), mcpPre)">复制</button>
                <span v-if="fallback?.key === 'mcp'" data-copy-fallback class="dv-err ds-micro">{{ fallback.msg }}</span>
                <span v-else-if="copied === 'mcp'" class="ds-micro" role="status">已复制</span>
              </span>
            </div>
            <pre ref="mcpPre" class="dv-pre ds-mono">{{ gatewaySnippet() }}</pre>
          </div>

          <p class="ds-small dv-note">
            外部客户端只看得到统一的 <span class="ds-mono">universal_tool</span>：先调
            <span class="ds-mono">list_tools</span> 拿到本插件暴露的工具清单，再按名字逐个调用。
          </p>
        </template>
      </article>

      <!-- ② 给 AI 的使用规则 -->
      <article v-if="currentProject" class="dv-card ds-surface ds-stack ds-gap-3" data-dv="rules">
        <h3 class="ds-h4">{{ term('agent-rules') }}</h3>
        <p class="ds-small">把这段规则放进 AI 的上下文（或写进项目文档），它就会照着你的{{ term('token') }}与规范写界面。</p>
        <p v-if="docsLoading && !rulesText" class="ds-small">正在读取…</p>
        <p v-else-if="rulesError" class="dv-err ds-small" role="alert">读取失败：{{ rulesError }}</p>
        <template v-else>
          <div class="ds-row ds-gap-2">
            <button type="button" class="dv-btn" @click="copyText('rules', rulesText, rulesPre)">复制</button>
            <a class="dv-btn" :href="downloadUrl('agent-rules')" download>下载</a>
            <span v-if="fallback?.key === 'rules'" data-copy-fallback class="dv-err ds-micro">{{ fallback.msg }}</span>
            <span v-else-if="copied === 'rules'" class="ds-micro" role="status">已复制</span>
          </div>
          <pre ref="rulesPre" class="dv-pre ds-mono" data-dv-text="agent-rules">{{ rulesText }}</pre>
        </template>
      </article>

      <!-- ③ 设计说明书 -->
      <article v-if="currentProject" class="dv-card ds-surface ds-stack ds-gap-3" data-dv="brief">
        <h3 class="ds-h4">{{ term('brief') }}</h3>
        <p class="ds-small">一段话讲清这套{{ term('design system') }}长什么样、由什么组成——给不写代码的人看。</p>
        <p v-if="docsLoading && !briefText" class="ds-small">正在读取…</p>
        <p v-else-if="briefError" class="dv-err ds-small" role="alert">读取失败：{{ briefError }}</p>
        <template v-else>
          <div class="ds-row ds-gap-2">
            <button type="button" class="dv-btn" @click="copyText('brief', briefText, briefPre)">复制</button>
            <span v-if="fallback?.key === 'brief'" data-copy-fallback class="dv-err ds-micro">{{ fallback.msg }}</span>
            <span v-else-if="copied === 'brief'" class="ds-micro" role="status">已复制</span>
          </div>
          <pre ref="briefPre" class="dv-pre ds-mono" data-dv-text="brief">{{ briefText }}</pre>
        </template>
      </article>

      <!-- ④ 交付文件 -->
      <article v-if="currentProject" class="dv-card ds-surface ds-stack ds-gap-3" data-dv="files">
        <h3 class="ds-h4">交付文件</h3>
        <p class="ds-small">下载即交付：这些文件由后端从同一份{{ term('token') }}生成，与画布上看到的一致。</p>
        <ul class="dv-files ds-stack ds-gap-2">
          <li v-for="f in formats()" :key="f" class="ds-row ds-gap-3 dv-row-between">
            <span class="ds-mono">{{ f }}</span>
            <a class="dv-btn" :href="downloadUrl(f)" download>下载</a>
          </li>
        </ul>
        <p v-if="!formats().length" class="ds-small">后端未声明可交付格式。</p>
      </article>

      <!-- ⑤ AI 可用的工具 -->
      <article class="dv-card ds-surface ds-stack ds-gap-3" data-dv="tools">
        <h3 class="ds-h4">AI 可用的工具</h3>
        <p v-if="toolsError" class="dv-err ds-small" role="alert">读取失败：{{ toolsError }}</p>
        <template v-else>
          <table class="dv-table">
            <thead>
              <tr>
                <th>工具</th>
                <th>权限</th>
                <th>说明</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="t in tools" :key="t.name">
                <td class="ds-mono">{{ t.name }}</td>
                <td>{{ t.readOnly ? '只读' : '可写' }}</td>
                <td class="ds-small">{{ t.summary }}</td>
              </tr>
            </tbody>
          </table>
          <p v-if="!tools.length" class="ds-small">还没有可用的工具。</p>

          <div class="dv-access ds-row ds-gap-3">
            <span class="ds-small">允许 AI 修改设计</span>
            <button
              type="button"
              role="switch"
              aria-label="允许 AI 修改设计"
              :aria-checked="allowWrite"
              class="dv-switch"
              :class="{ 'dv-switch--on': allowWrite }"
              :disabled="accessBusy"
              @click="toggleAccess"
            >
              <span class="dv-switch__dot" aria-hidden="true"></span>
            </button>
            <span class="ds-micro">{{ allowWrite ? '已允许：AI 可以写入' : '未允许：AI 只能读' }}</span>
          </div>
          <p v-if="accessError" class="dv-err ds-small" role="alert">{{ accessError }}</p>
        </template>
      </article>

      <!-- ⑥ 贴代码试审查 -->
      <article v-if="currentProject" class="dv-card ds-surface ds-stack ds-gap-3" data-dv="review">
        <h3 class="ds-h4">贴代码试审查</h3>
        <p class="ds-small">把你（或 AI）写的界面代码贴进来，照着这套{{ term('design system') }}检查有没有硬编码颜色、跳过的{{ term('token') }}等问题。</p>
        <div class="ds-row ds-gap-2 dv-wrap">
          <span class="ds-small">代码语言</span>
          <select class="dv-select" aria-label="代码语言" :value="reviewLang" @change="reviewLang = ($event.target as HTMLSelectElement).value">
            <option v-for="l in LANGUAGES" :key="l" :value="l">{{ l }}</option>
          </select>
        </div>
        <textarea
          class="dv-textarea ds-mono"
          rows="6"
          aria-label="待审查代码"
          placeholder="粘贴 CSS / SCSS / Vue / TS 等代码片段…"
          :value="reviewCode"
          @input="reviewCode = ($event.target as HTMLTextAreaElement).value"
        />
        <div class="ds-row ds-gap-2">
          <button type="button" class="dv-btn dv-btn--primary" :disabled="reviewBusy" @click="runReview">
            {{ reviewBusy ? '正在审查…' : '开始审查' }}
          </button>
          <span class="ds-micro">上限 200KB</span>
        </div>
        <p v-if="reviewError" class="dv-err ds-small" role="alert">{{ reviewError }}</p>

        <div v-if="reviewResult" class="ds-stack ds-gap-2" data-review-result>
          <p v-if="reviewResult.error" class="dv-err ds-small" role="alert">{{ reviewResult.error }}</p>
          <p class="ds-small">结论：{{ reviewResult.summary || '（无）' }}</p>
          <p v-if="reviewResult.themeNote" class="ds-micro">{{ reviewResult.themeNote }}</p>
          <ul v-if="reviewResult.findings.length" class="dv-findings ds-stack ds-gap-1">
            <li v-for="(f, i) in reviewResult.findings" :key="i" class="ds-small ds-mono">{{ f }}</li>
          </ul>
          <p v-else class="ds-small">没有发现问题。</p>
          <p v-if="reviewResult.truncated" class="ds-micro">只显示前 {{ reviewResult.findings.length }} 条，还有更多未列出。</p>
          <p v-if="reviewResult.skipped" class="ds-micro">跳过 {{ reviewResult.skipped }} 处（空/二进制/未知语言）。</p>
          <p v-if="reviewResult.notes" class="ds-micro">{{ reviewResult.notes }}</p>
        </div>
      </article>
    </div>
  </section>
</template>

<style scoped>
.dv {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-5);
}
.dv__head {
  gap: var(--ds-space-1);
}
.dv__grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(360px, 1fr));
  gap: var(--ds-space-4);
  align-items: start;
}
.dv-card {
  padding: var(--ds-space-5);
}
.dv-row-between {
  justify-content: space-between;
  width: 100%;
}
.dv-wrap {
  flex-wrap: wrap;
}
.dv-facts {
  padding: var(--ds-space-3) var(--ds-space-4);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-md);
  background: var(--ds-surface-2);
}
.dv-note {
  color: var(--ds-fg-3);
  margin: 0;
}
.dv-pre {
  margin: 0;
  padding: var(--ds-space-3) var(--ds-space-4);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-md);
  background: var(--ds-surface-2);
  color: var(--ds-fg-1);
  max-height: 260px;
  overflow: auto;
  white-space: pre-wrap;
  word-break: break-word;
}
.dv-btn {
  font: inherit;
  font-size: var(--ds-fs-small);
  color: var(--ds-fg-1);
  background: var(--ds-surface-2);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-sm);
  padding: 5px var(--ds-space-4);
  cursor: pointer;
  text-decoration: none;
  white-space: nowrap;
}
.dv-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
.dv-btn--primary {
  color: var(--ds-surface-1);
  background: var(--ds-color-primary);
  border-color: var(--ds-color-primary);
}
.dv-btn:focus-visible,
.dv-switch:focus-visible,
.dv-select:focus-visible,
.dv-textarea:focus-visible {
  outline: 2px solid var(--ds-color-primary);
  outline-offset: 2px;
}
.dv-select,
.dv-textarea {
  font: inherit;
  color: var(--ds-fg-1);
  background: var(--ds-surface-1);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-sm);
  padding: 6px var(--ds-space-3);
}
.dv-textarea {
  resize: vertical;
  width: 100%;
}
.dv-files,
.dv-findings {
  list-style: none;
  margin: 0;
  padding: 0;
}
.dv-findings {
  max-height: 320px;
  overflow: auto;
}
.dv-table {
  width: 100%;
  border-collapse: collapse;
  font-size: var(--ds-fs-small);
}
.dv-table th,
.dv-table td {
  text-align: left;
  padding: var(--ds-space-2) var(--ds-space-3);
  border-bottom: 1px solid var(--ds-border-1);
  vertical-align: top;
}
.dv-access {
  padding-top: var(--ds-space-2);
}
.dv-switch {
  position: relative;
  inline-size: 42px;
  block-size: 24px;
  border-radius: var(--ds-radius-pill);
  border: 1px solid var(--ds-border-1);
  background: var(--ds-surface-2);
  cursor: pointer;
  padding: 0;
  transition: background 0.15s ease;
}
.dv-switch--on {
  background: var(--ds-color-primary);
  border-color: var(--ds-color-primary);
}
.dv-switch__dot {
  position: absolute;
  top: 2px;
  left: 2px;
  inline-size: 18px;
  block-size: 18px;
  border-radius: 50%;
  background: var(--ds-surface-1);
  box-shadow: var(--ds-shadow-sm);
  transition: transform 0.15s ease;
}
.dv-switch--on .dv-switch__dot {
  transform: translateX(18px);
}
.dv-err {
  color: var(--ds-danger);
  margin: 0;
}
.dv-warn-text {
  color: var(--ds-warning);
}
</style>