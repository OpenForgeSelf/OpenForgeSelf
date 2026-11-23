<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { statusLabel } from './session.logic'
import type { AgentSessionStatus, AgentWorkspaceItemStatus, AgentWorkspacePanelId } from './types'

const props = defineProps<{
  panel: AgentWorkspacePanelId
  catalog: AgentWorkspaceItemStatus[]
  plan: {
    goal?: string
    steps?: Array<{ id?: string; title: string; detail?: string; status?: string }>
    constraints?: string[]
  } | null
  detail: Record<string, unknown> | null
  kind: 'new' | 'confirm-plan' | 'approve'
  revising: boolean
  status: string
  layout?: 'page' | 'panel'
}>()
const emit = defineEmits<{
  open: [id: AgentWorkspacePanelId]
  confirm: []
  startRevise: []
  revise: []
  retry: []
  writeMemory: []
  commitDesign: []
  dispatchTodo: [id: string]
  pauseWorkflow: []
  cancelWorkflow: []
  chat: []
}>()
const reviseNote = defineModel<string>('reviseNote', { default: '' })
const memoryDraft = defineModel<string>('memoryDraft', { default: '' })

function asList(key: string): Array<Record<string, unknown>> {
  const v = props.detail?.[key]
  return Array.isArray(v) ? (v as Array<Record<string, unknown>>) : []
}
function text(v: unknown): string {
  return typeof v === 'string' ? v : ''
}

function suggestionPanel(it: Record<string, unknown>): AgentWorkspacePanelId | '' {
  const panel = text(it.panel)
  if (panel) return panel as AgentWorkspacePanelId
  const action = text(it.action)
  if (action === 'open_workflow') return 'workflow'
  if (action === 'write_memory') return 'memory'
  if (action === 'dispatch_todo') return 'todo'
  return ''
}

function display(v: unknown): string {
  if (typeof v === 'string') return v
  if (typeof v === 'number' && Number.isFinite(v)) return String(v)
  return ''
}

const runLabel = computed(() => {
  if (props.status === 'cancelled' || props.status === 'failed' || props.status === 'completed') {
    return statusLabel(props.status as AgentSessionStatus)
  }
  const raw = display(props.detail?.runStatus)
  if (!raw) return ''
  return statusLabel(raw as AgentSessionStatus)
})

const roundText = computed(() => {
  const round = display(props.detail?.round)
  if (!round) return ''
  const max = display(props.detail?.turnMax)
  return max ? `${round} / ${max}` : round
})

const hasRun = computed(() => Boolean(runLabel.value || roundText.value || asList('steps').length || asList('items').length))


const canWriteMemory = computed(() => {
  if (memoryDraft.value.trim()) return true
  const data = props.detail
  const items = Array.isArray(data?.items) ? data!.items : Array.isArray(data?.entries) ? data!.entries : []
  return items.some((it) => {
    const rec = it as Record<string, unknown>
    return Boolean(text(rec.content) || text(rec.title) || text(rec.summary))
  })
})

const canCommitDesign = computed(() => {
  const data = props.detail
  return Boolean(text(data?.preview) || text(data?.content) || (Array.isArray(data?.tokens) && data!.tokens.length))
})

const designConfirming = ref(false)
const designCommitted = computed(() => {
  const v = props.detail?.committed
  return v === true || v === 'true' || v === '已写入'
})

watch(
  () => [props.panel, designCommitted.value],
  () => {
    if (props.panel !== 'design' || designCommitted.value) designConfirming.value = false
  }
)

const locallyDispatched = ref(new Set<string>())

function isDispatched(it: Record<string, unknown>): boolean {
  const status = text(it.status)
  const id = text(it.id)
  return (
    it.dispatched === true ||
    it.assigned === true ||
    status === 'dispatched' ||
    status === '已下发' ||
    (id !== '' && locallyDispatched.value.has(id))
  )
}

function todoItems(): Array<Record<string, unknown>> {
  const items = asList('items')
  const out: Array<Record<string, unknown>> = []
  for (const it of items) {
    const id = text(it.id)
    const title = text(it.title) || text(it.name)
    if (!title) continue
    out.push(isDispatched(it) ? { ...it, dispatched: true } : it)
  }
  return out
}

function onDispatchTodo(it: Record<string, unknown>, fallbackId: string) {
  const id = text(it.id) || fallbackId
  if (id) locallyDispatched.value = new Set([...locallyDispatched.value, id])
  emit('dispatchTodo', id)
}

function workflowStatusText(): string {
  const raw = text(props.detail?.workflowStatus) || text(props.detail?.WorkflowStatus)
  if (!raw) return ''
  switch (raw) {
    case 'paused':
      return '已暂停'
    case 'cancelled':
      return '已取消'
    case 'running':
      return '进行中'
    case 'completed':
      return '已完成'
    case 'idle':
      return '未启动'
    default:
      return raw
  }
}
</script>

<template>
  <aside class="aa-artifact">
    <template v-if="panel === 'catalog'">
      <div class="aa-art-head">
        <h2>打开面板</h2>
        <button v-if="layout === 'panel'" type="button" class="aa-btn ghost" @click="emit('chat')">返回对话</button>
      </div>
      <p class="aa-art-lead">从这里进入计划、记忆、工作流等。对话进行中也可以随时切换。</p>
      <div class="aa-art-body">
        <button
          v-for="item in catalog"
          :key="item.id"
          type="button"
          class="aa-panel-item"
          :class="{ 'is-hot': item.tone === 'hot', 'is-warn': item.tone === 'warn' }"
          @click="emit('open', item.id)"
        >
          <span>
            <div class="aa-pi-name">{{ item.name }}</div>
            <div class="aa-pi-desc">{{ item.description }}</div>
          </span>
          <span class="aa-pi-st">{{ item.statusText }}</span>
        </button>
      </div>
    </template>
    <template v-else>
      <div class="aa-art-head">
        <h2>{{ catalog.find((c) => c.id === panel)?.name || '面板' }}</h2>
        <div class="aa-art-actions">
          <button v-if="layout === 'panel'" type="button" class="aa-btn ghost" @click="emit('chat')">返回对话</button>
          <button type="button" class="aa-btn ghost" @click="emit('open', 'catalog')">全部面板</button>
        </div>
      </div>
      <div class="aa-art-body">
        <template v-if="panel === 'plan'">
          <div v-if="!plan" class="aa-art-sec"><p>还没有计划。选择计划任务并发送后，会在这里先确认再执行。</p></div>
          <template v-else>
            <div v-if="plan.goal" class="aa-art-sec"><h3>目标</h3><p>{{ plan.goal }}</p></div>
            <div v-if="plan.note" class="aa-art-sec"><p class="aa-muted-line">{{ plan.note }}</p></div>
            <div v-if="plan.steps?.length" class="aa-art-sec">
              <h3>步骤</h3>
              <ol class="aa-steps">
                <li v-for="(st, i) in plan.steps" :key="st.id || i">{{ st.title }}{{ st.detail ? ' · ' + st.detail : '' }}</li>
              </ol>
            </div>
            <div v-if="plan.constraints?.length" class="aa-art-sec">
              <h3>约束</h3>
              <p v-for="(c, i) in plan.constraints" :key="i">{{ c }}</p>
            </div>
            <div v-if="revising" class="aa-art-sec">
              <textarea v-model="reviseNote" rows="4" style="width:100%;background:transparent;color:inherit;border:1px solid var(--border-subtle);border-radius:var(--radius-md);padding:8px" placeholder="说明要怎么改计划" />
            </div>
          </template>
        </template>
        <template v-else-if="panel === 'records'">
          <div class="aa-art-sec">
            <p v-if="!hasRun">开始任务后，步骤、工具结果和轮次会显示在这里。</p>
            <p v-if="runLabel">状态 {{ runLabel }}</p>
            <p v-if="roundText">轮次 {{ roundText }}</p>
            <p v-if="text(detail?.failureReason) || text(detail?.stuckReason)">{{ text(detail?.failureReason) || text(detail?.stuckReason) }}</p>
            <ol class="aa-steps">
              <li v-for="(st, i) in (asList('steps').length ? asList('steps') : asList('items'))" :key="i">
                {{ text(st.title) || text(st.name) }} · {{ text(st.status) || '记录' }}
              </li>
            </ol>
          </div>
        </template>
        <template v-else-if="panel === 'workflow'">
          <div class="aa-art-sec">
            <p v-if="workflowStatusText()">状态 {{ workflowStatusText() }}</p>
            <p v-if="!asList('steps').length && !workflowStatusText()">未启动工作流。</p>
            <ol class="aa-steps">
              <li v-for="(st, i) in asList('steps')" :key="i">{{ text(st.title) || text(st.name) }} · {{ text(st.status) }}</li>
            </ol>
          </div>
        </template>
        <template v-else-if="panel === 'memory'">
          <div class="aa-art-sec">
            <p v-if="!asList('items').length && !asList('entries').length">本轮还没有检索到记忆。可以直接写下要写回的内容。</p>
            <ul class="aa-steps">
              <li v-for="(it, i) in (asList('items').length ? asList('items') : asList('entries'))" :key="i">
                {{ text(it.title) || text(it.content) || text(it.summary) }}
              </li>
            </ul>
            <textarea v-model="memoryDraft" rows="4" style="width:100%;margin-top:8px;background:transparent;color:inherit;border:1px solid var(--border-subtle);border-radius:var(--radius-md);padding:8px" placeholder="要写回记忆的内容" />
          </div>
        </template>
        <template v-else-if="panel === 'design'">
          <div class="aa-art-sec">
            <p v-if="!detail || !(detail.preview || asList('tokens').length)">暂无预览。写入设计系统需要明确确认，默认不写库。</p>
            <p v-else>{{ text(detail.preview) || '已有预览，确认后才写入设计系统。' }}</p>
            <p v-if="designCommitted">已写入</p>
          </div>
        </template>
        <template v-else-if="panel === 'suggest'">
          <div class="aa-art-sec">
            <p v-if="!asList('items').length">暂无建议。</p>
            <button
              v-for="(it, i) in asList('items')"
              :key="i"
              type="button"
              class="aa-panel-item"
              @click="suggestionPanel(it) && emit('open', suggestionPanel(it) as AgentWorkspacePanelId)"
            >
              <span>
                <div class="aa-pi-name">{{ text(it.title) || text(it.name) }}</div>
                <div class="aa-pi-desc">{{ text(it.description) || text(it.detail) }}</div>
              </span>
            </button>
          </div>
        </template>
        <template v-else-if="panel === 'project'">
          <div class="aa-art-sec">
            <p v-if="!text(detail?.name) && !text(detail?.cwd)">未绑定。不会假装已有项目。</p>
            <template v-else>
              <p>{{ text(detail?.name) || '已绑定项目' }}</p>
              <p v-if="text(detail?.cwd)">{{ text(detail?.cwd) }}</p>
              <p v-if="text(detail?.summary)">{{ text(detail?.summary) }}</p>
            </template>
          </div>
        </template>
        <template v-else-if="panel === 'todo'">
          <div class="aa-art-sec">
            <p v-if="!todoItems().length">暂无可下发的待办。</p>
            <div v-for="(it, i) in todoItems()" :key="text(it.id) || String(i)" class="aa-art-sec">
              <p>{{ text(it.title) || text(it.name) || '待办' }}</p>
              <p v-if="isDispatched(it)" class="aa-muted-line">已下发</p>
              <button v-else type="button" class="aa-btn" @click="onDispatchTodo(it, String(i))">下发给当前智能体</button>
            </div>
          </div>
        </template>
        <template v-else-if="panel === 'skills'">
          <div class="aa-art-sec">
            <h3>已启用</h3>
            <p v-if="!asList('enabled').length">暂无</p>
            <p v-for="(it, i) in asList('enabled')" :key="'e'+i">{{ text(it.name) || text(it.title) }}</p>
            <h3>未启用</h3>
            <p v-if="!asList('disabled').length">暂无</p>
            <p v-for="(it, i) in asList('disabled')" :key="'d'+i">{{ text(it.name) || text(it.title) }}</p>
            <p>启用或关闭请到智能体设置，不在对话里直接改配置。</p>
          </div>
        </template>
        <template v-else-if="panel === 'tools'">
          <div class="aa-art-sec">
            <p v-if="!asList('tools').length && !asList('items').length">本轮可见工具将显示在这里。</p>
            <ul class="aa-steps">
              <li v-for="(it, i) in (asList('tools').length ? asList('tools') : asList('items'))" :key="i">
                {{ text(it.name) }} {{ text(it.status) ? '· ' + text(it.status) : '' }}
              </li>
            </ul>
            <p v-for="(it, i) in asList('mcp')" :key="'mcp'+i">MCP {{ text(it.id) ? text(it.id) + ' · ' : '' }}{{ text(it.status) || '未连接' }}</p>
            <p v-if="!asList('mcp').length">MCP 未连接</p>
            <p>不能在本面板直接执行工具。</p>
          </div>
        </template>
        <template v-else-if="panel === 'snippets'">
          <div class="aa-art-sec">
            <p v-if="!asList('items').length">还没有代码片段。对话里产出的可复用命令或代码会出现在这里。</p>
            <div v-for="(it, i) in asList('items')" :key="i" class="aa-art-sec">
              <h3>{{ text(it.language) || text(it.title) || '片段' }}</h3>
              <pre class="aa-tool-detail">{{ text(it.content) || text(it.code) }}</pre>
              <button type="button" class="aa-btn" @click="navigator.clipboard.writeText(text(it.content) || text(it.code))">复制</button>
            </div>
          </div>
        </template>
      </div>
      <div v-if="panel === 'plan' && plan" class="aa-art-foot">
        <button v-if="kind === 'confirm-plan' && !revising" type="button" class="aa-btn pri" @click="emit('confirm')">确认并执行</button>
        <button v-if="!revising" type="button" class="aa-btn ghost" @click="emit('startRevise')">改计划</button>
        <button v-else type="button" class="aa-btn pri" @click="emit('revise')">提交修改</button>
      </div>
      <div v-else-if="panel === 'records' && status === 'failed'" class="aa-art-foot">
        <button type="button" class="aa-btn pri" @click="emit('retry')">重试</button>
      </div>
      <div v-else-if="panel === 'memory'" class="aa-art-foot">
        <button type="button" class="aa-btn" :disabled="!canWriteMemory" @click="canWriteMemory && emit('writeMemory')">写回记忆</button>
      </div>
      <div v-else-if="panel === 'design'" class="aa-art-foot">
        <button v-if="designCommitted" type="button" class="aa-btn pri" disabled>已写入</button>
        <template v-else-if="designConfirming">
          <span class="aa-confirm-ask">确认写入设计系统？</span>
          <button type="button" class="aa-btn pri" :disabled="!canCommitDesign" @click="canCommitDesign && emit('commitDesign')">确认写入</button>
          <button type="button" class="aa-btn ghost" @click="designConfirming = false">取消</button>
        </template>
        <button v-else type="button" class="aa-btn pri" :disabled="!canCommitDesign" @click="canCommitDesign && (designConfirming = true)">确认写入设计系统</button>
      </div>
      <div v-else-if="panel === 'workflow'" class="aa-art-foot">
        <button type="button" class="aa-btn" @click="emit('pauseWorkflow')">暂停</button>
        <button type="button" class="aa-btn ghost" @click="emit('cancelWorkflow')">取消</button>
      </div>
    </template>
  </aside>
</template>
