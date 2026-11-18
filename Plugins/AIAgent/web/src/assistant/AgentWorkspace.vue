<script setup lang="ts">
import type { AgentWorkspaceItemStatus, AgentWorkspacePanelId } from './types'

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
}>()
const reviseNote = defineModel<string>('reviseNote', { default: '' })

function asList(key: string): Array<Record<string, unknown>> {
  const v = props.detail?.[key]
  return Array.isArray(v) ? (v as Array<Record<string, unknown>>) : []
}
function text(v: unknown): string {
  return typeof v === 'string' ? v : ''
}
</script>

<template>
  <aside class="aa-artifact">
    <template v-if="panel === 'catalog'">
      <div class="aa-art-head"><h2>打开面板</h2></div>
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
        <button type="button" class="aa-btn ghost" @click="emit('open', 'catalog')">全部面板</button>
      </div>
      <div class="aa-art-body">
        <template v-if="panel === 'plan'">
          <div v-if="!plan" class="aa-art-sec"><p>还没有计划。选择计划任务并发送后，会在这里先确认再执行。</p></div>
          <template v-else>
            <div v-if="plan.goal" class="aa-art-sec"><h3>目标</h3><p>{{ plan.goal }}</p></div>
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
            <p v-if="!asList('steps').length && !asList('items').length">开始任务后，步骤、工具结果和轮次会显示在这里。</p>
            <p v-if="text(detail?.round)">轮次 {{ text(detail?.round) }}</p>
            <ol class="aa-steps">
              <li v-for="(st, i) in (asList('steps').length ? asList('steps') : asList('items'))" :key="i">
                {{ text(st.title) || text(st.name) }} · {{ text(st.status) || '记录' }}
              </li>
            </ol>
          </div>
        </template>
        <template v-else-if="panel === 'workflow'">
          <div class="aa-art-sec">
            <p v-if="!asList('steps').length">未启动工作流。</p>
            <ol class="aa-steps">
              <li v-for="(st, i) in asList('steps')" :key="i">{{ text(st.title) || text(st.name) }} · {{ text(st.status) }}</li>
            </ol>
          </div>
        </template>
        <template v-else-if="panel === 'memory'">
          <div class="aa-art-sec">
            <p v-if="!asList('items').length && !asList('entries').length">本轮还没有检索到记忆。</p>
            <ul class="aa-steps">
              <li v-for="(it, i) in (asList('items').length ? asList('items') : asList('entries'))" :key="i">
                {{ text(it.title) || text(it.content) || text(it.summary) }}
              </li>
            </ul>
          </div>
        </template>
        <template v-else-if="panel === 'design'">
          <div class="aa-art-sec">
            <p v-if="!detail || !(detail.preview || asList('tokens').length)">暂无预览。写入设计系统需要明确确认，默认不写库。</p>
            <p v-else>{{ text(detail.preview) || '已有预览，确认后才写入设计系统。' }}</p>
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
              @click="it.panel ? emit('open', String(it.panel) as AgentWorkspacePanelId) : undefined"
            >
              <span>
                <div class="aa-pi-name">{{ text(it.title) || text(it.name) }}</div>
                <div class="aa-pi-desc">{{ text(it.description) }}</div>
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
            <p v-if="!asList('items').length">暂无可下发的待办。</p>
            <div v-for="(it, i) in asList('items')" :key="i" class="aa-art-sec">
              <p>{{ text(it.title) || text(it.name) }}</p>
              <button type="button" class="aa-btn" @click="emit('dispatchTodo', text(it.id) || String(i))">下发给当前智能体</button>
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
            <p v-if="!asList('tools').length && !asList('items').length">本轮可见工具将显示在这里。未连接 MCP 时会标明未连接。不能在本面板直接执行工具。</p>
            <ul class="aa-steps">
              <li v-for="(it, i) in (asList('tools').length ? asList('tools') : asList('items'))" :key="i">
                {{ text(it.name) }} {{ text(it.status) ? '· ' + text(it.status) : '' }}
              </li>
            </ul>
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
        <button type="button" class="aa-btn" @click="emit('writeMemory')">写回记忆</button>
      </div>
      <div v-else-if="panel === 'design'" class="aa-art-foot">
        <button type="button" class="aa-btn pri" @click="emit('commitDesign')">确认写入设计系统</button>
      </div>
      <div v-else-if="panel === 'workflow'" class="aa-art-foot">
        <button type="button" class="aa-btn" @click="emit('pauseWorkflow')">暂停</button>
        <button type="button" class="aa-btn ghost" @click="emit('cancelWorkflow')">取消</button>
      </div>
    </template>
  </aside>
</template>
