<script setup lang="ts">
/**
 * 任务详情：一条任务 = 一个可下发给 agent 的工作单元。
 *
 * 交互口径（plugin-development §3.4）：
 * - **点即保存**：文本栏失焦即回写（只发变化的那一栏），不留"编辑完还要再点保存"的坑；
 * - **状态突变要二次确认**：删除/标记完成/覆盖正文/解除关联走 NotifyHost 的确认框；
 * - **可达阶段照服务端给的 allowedTargets 渲染**，界面不抄一份流转表（否则"按钮能点、后端 409"）；
 * - **空态分级**：未关联项目 / 无工件目录 / 委派能力缺席，各有各自的文案与下一步。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue'
import ExecutionTimeline from './ExecutionTimeline.vue'
import * as store from '../store'
import { listArtifactSets } from '../http'
import { confirmAction, showToast } from '../notify'
import { copyText, defaultSelectionFor, missingLabels, priorityLabel, shortPath, stageLabel, stageTagType } from '../actions'
import type { ArtifactSet, TodoItem } from '../types'

const props = defineProps<{ task: TodoItem }>()
const s = store.state

const form = reactive({
  title: '', remark: '', objective: '', content: '', acceptance: '', verification: '',
  allowedScope: '', forbiddenScope: '', assignee: '', priority: 1, permissionMode: 'read-only'
})

const blockReason = ref('')
const projectPath = ref('')
const projectEditing = ref(false)

const sets = ref<ArtifactSet[]>([])
const setsError = ref('')
const chosenDir = ref('')
const chosenFiles = ref<string[]>([])
const importBusy = ref(false)

const previewBusy = ref(false)
const agentId = ref<number | undefined>(undefined)

function fill(t: TodoItem): void {
  form.title = t.title
  form.remark = t.remark ?? ''
  form.objective = t.objective
  form.content = t.content
  form.acceptance = t.acceptance
  form.verification = t.verification
  form.allowedScope = t.allowedScope
  form.forbiddenScope = t.forbiddenScope
  form.assignee = t.assignee
  form.priority = t.priority || 1
  form.permissionMode = t.permissionMode || 'read-only'
  projectPath.value = t.projectRoot || t.projectPathRaw || ''
}

fill(props.task)
watch(() => props.task.id, () => fill(props.task))

const dirty = computed(() => {
  const t = props.task
  return form.title !== t.title || (form.remark ?? '') !== (t.remark ?? '') ||
    form.objective !== t.objective || form.content !== t.content ||
    form.acceptance !== t.acceptance || form.verification !== t.verification ||
    form.allowedScope !== t.allowedScope || form.forbiddenScope !== t.forbiddenScope ||
    form.assignee !== t.assignee || form.priority !== t.priority ||
    form.permissionMode !== t.permissionMode
})

const missingText = computed(() => missingLabels(props.task.missing))
const projectLabel = computed(() => props.task.projectName || shortPath(props.task.projectRoot, 40))
const chosenSet = computed(() => sets.value.find(x => x.dir === chosenDir.value) ?? null)
const canDelegate = computed(() => !!s.preview?.canDelegate)
/**
 * 禁用原因必须"照实说"：预览还没生成 / 接缝不在场 / 四栏不齐，是三种不同的下一步。
 * （实测：把"先生成提示词"当默认文案会在预览已生成但不可委派时撒谎。）
 */
const delegateHint = computed(() => {
  if (canDelegate.value) return '经 agent-hub 起一个真实 agent 执行'
  if (!s.preview) return '先生成提示词（委派前要先据它判断）'
  if (!s.preview.delegationAvailable) return s.preview.delegationError || '未检测到 agent 委派能力'
  return '四栏齐备（目标/正文/判据/验证命令）后才能一键委派'
})
const terminalStatus = computed(() => {
  const a = s.agent
  if (!a?.ok) return ''
  return `${a.status}${a.exitCode != null ? ` · exit ${a.exitCode}` : ''}${a.errorCode ? ` · ${a.errorCode}` : ''}`
})

/** 失焦即存：只提交真正改过的栏位，避免把别人刚改的内容覆盖回去。 */
async function commit(): Promise<void> {
  if (!dirty.value) return
  const t = props.task
  const patch: Record<string, string | number> = {}
  if (form.title !== t.title) patch.title = form.title
  if (form.remark !== (t.remark ?? '')) patch.remark = form.remark
  if (form.objective !== t.objective) patch.objective = form.objective
  if (form.content !== t.content) patch.content = form.content
  if (form.acceptance !== t.acceptance) patch.acceptance = form.acceptance
  if (form.verification !== t.verification) patch.verification = form.verification
  if (form.allowedScope !== t.allowedScope) patch.allowedScope = form.allowedScope
  if (form.forbiddenScope !== t.forbiddenScope) patch.forbiddenScope = form.forbiddenScope
  if (form.assignee !== t.assignee) patch.assignee = form.assignee
  if (form.priority !== t.priority) patch.priority = form.priority
  if (form.permissionMode !== t.permissionMode) patch.permissionMode = form.permissionMode
  await store.saveDetail(patch)
}

async function chooseStage(target: string): Promise<void> {
  if (target === 'Blocked' && !blockReason.value.trim()) {
    showToast('进入「阻塞」必须写明阻塞原因（缺什么、要谁处理）', 'warning')
    return
  }
  await store.changeStage(props.task, target, blockReason.value.trim() || undefined)
  blockReason.value = ''
}

async function saveProject(): Promise<void> {
  const path = projectPath.value.trim()
  if (!path) {
    showToast('要关联项目请先填路径（支持 D:\\proj、/d/proj 等写法）', 'warning')
    return
  }
  const ok = await store.linkProject(props.task, path)
  if (ok) projectEditing.value = false
}

async function removeProject(): Promise<void> {
  await store.unlinkProject(props.task)
  projectEditing.value = false
}

async function loadSets(): Promise<void> {
  setsError.value = ''
  sets.value = []
  chosenDir.value = ''
  chosenFiles.value = []
  const t = props.task
  if (!t.projectId && !t.projectRoot) {
    setsError.value = '任务还没关联项目 —— 工件在项目磁盘里，先在上方关联项目。'
    return
  }
  importBusy.value = true
  try {
    const res = await listArtifactSets(t.projectId, t.projectRoot || undefined)
    if (!res.items.length) {
      // 空清单的原因由后端给（"没有 docs/ai/pilot 目录"与"有目录但没有 NN-*.md"是两回事），界面不许自己编
      setsError.value = res.message || '后端没列出任何工件目录（未给出原因）。'
    }
    sets.value = res.items
    const withCore = res.items.find(x => x.hasCoreFiles)
    if (withCore) selectSet(withCore)
  } catch (e) {
    setsError.value = e instanceof Error ? e.message : String(e)
  } finally {
    importBusy.value = false
  }
}

function selectSet(set: ArtifactSet): void {
  chosenDir.value = set.dir
  chosenFiles.value = defaultSelectionFor(set)
}

async function doImport(overwrite: boolean): Promise<void> {
  if (!chosenDir.value) { showToast('先选一个工件目录', 'warning'); return }
  if (!chosenFiles.value.length) { showToast('至少要勾一个文件', 'warning'); return }
  if (!overwrite && props.task.content.trim()) {
    const ok = await confirmAction({
      title: '覆盖任务正文',
      message: `导入会用工件内容替换现有正文（当前 ${props.task.content.length} 字符）。`,
      detail: '这是覆盖动作，不是追加；确认前原文仍在，确认后不可自动还原。',
      confirmText: '覆盖导入',
      danger: true
    })
    if (!ok) return
    overwrite = true
  }
  importBusy.value = true
  try {
    const r = await store.importArtifacts(props.task, chosenDir.value, chosenFiles.value, overwrite)
    if (r?.ok) fill(props.task)
  } finally {
    importBusy.value = false
  }
}

async function openPreview(): Promise<void> {
  previewBusy.value = true
  try {
    await store.loadPreview(props.task)
  } finally {
    previewBusy.value = false
  }
}

async function copyPrompt(): Promise<void> {
  const text = s.preview?.promptMarkdown ?? ''
  const ok = await copyText(text)
  showToast(ok ? '提示词已复制，可直接粘给任意 agent' : '浏览器不允许自动复制，请在预览框里手工全选',
    ok ? 'success' : 'warning')
}

async function doDispatch(): Promise<void> {
  if (missingText.value) { showToast(`还缺：${missingText.value}`, 'warning'); return }
  await store.dispatchTask(props.task, form.assignee)
}

async function doDelegate(): Promise<void> {
  const result = await store.delegateToAgent(props.task, agentId.value, form.permissionMode)
  if (result?.ok) await openPreview()
}

onMounted(() => {
  if (props.task.agentTaskKey) void store.loadAgentStatus()
})
</script>

<template>
  <div class="td" :class="{ 'is-busy': s.operating }">
    <header class="td-head">
      <span class="td-stage" :class="`tag-${stageTagType(task.stage)}`">{{ task.stageLabel }}</span>
      <h2 class="td-key" :title="`外部键：供 agent 按 taskKey 回报`">{{ task.taskKey }}</h2>
      <button type="button" class="td-close" aria-label="关闭详情" @click="store.closeDetail()">×</button>
    </header>

    <div class="td-scroll">
      <section class="td-block">
        <label class="td-label" for="td-title">标题</label>
        <input id="td-title" v-model="form.title" class="td-input" maxlength="200" @blur="commit">
        <label class="td-label" for="td-remark">备注</label>
        <textarea id="td-remark" v-model="form.remark" class="td-input td-area" rows="2" maxlength="1000"
          placeholder="给人看的补充说明（不会进下发正文）" @blur="commit"></textarea>
        <div class="td-row">
          <div>
            <label class="td-label" for="td-prio">优先级</label>
            <select id="td-prio" v-model.number="form.priority" class="td-input" @change="commit">
              <option :value="1">P1</option>
              <option :value="2">P2</option>
              <option :value="3">P3</option>
            </select>
          </div>
          <div class="td-grow">
            <label class="td-label" for="td-assignee">下发对象</label>
            <input id="td-assignee" v-model="form.assignee" class="td-input" maxlength="100"
              placeholder="agent 名 / manual" @blur="commit">
          </div>
        </div>
        <p v-if="dirty" class="td-dirty">有未保存的改动（失焦即保存）</p>
      </section>

      <section class="td-block">
        <div class="td-block-head">
          <span class="td-label td-strong">项目路径</span>
          <button v-if="!projectEditing && task.projectId" type="button" class="td-link" @click="projectEditing = true">改</button>
        </div>
        <template v-if="task.projectId">
          <p class="td-proj-line"><b>{{ projectLabel }}</b></p>
          <p class="td-mono" :title="task.projectRoot">{{ task.projectRoot }}</p>
          <p v-if="task.projectPathRaw && task.projectPathRaw !== task.projectRoot" class="td-dim">
            你的写法：<span class="td-mono">{{ task.projectPathRaw }}</span>
          </p>
          <div v-if="projectEditing" class="td-row td-row-top">
            <input v-model="projectPath" class="td-input td-grow" placeholder="/d/project 或 D:\project">
            <button type="button" class="td-btn is-primary" @click="saveProject">保存</button>
            <button type="button" class="td-btn" @click="removeProject">解除</button>
          </div>
        </template>
        <template v-else>
          <p class="td-empty-line">未关联项目 —— 下发时 agent 不知道在哪个目录干活。</p>
          <div class="td-row td-row-top">
            <input v-model="projectPath" class="td-input td-grow" placeholder="支持 /d/project、D:\project、D:/project、/mnt/d/project">
            <button type="button" class="td-btn is-primary" :disabled="!projectPath.trim()" @click="saveProject">关联</button>
          </div>
        </template>
      </section>

      <section class="td-block">
        <div class="td-block-head">
          <span class="td-label td-strong">工作单元四栏</span>
          <span v-if="missingText" class="td-missing">还缺：{{ missingText }}</span>
          <span v-else class="td-ok">四栏齐备，可下发</span>
        </div>
        <label class="td-label" for="td-obj">Objective（可验证目标）</label>
        <input id="td-obj" v-model="form.objective" class="td-input" maxlength="500"
          placeholder="做完后仓库达到的可验证状态，一句话" @blur="commit">
        <label class="td-label" for="td-content">任务正文（markdown）</label>
        <textarea id="td-content" v-model="form.content" class="td-input td-area td-area-tall"
          placeholder="要做什么、背景与约束；也可从下方工件导入自动填充" @blur="commit"></textarea>
        <div class="td-row">
          <div class="td-grow">
            <label class="td-label" for="td-allow">允许改动范围</label>
            <textarea id="td-allow" v-model="form.allowedScope" class="td-input td-area" rows="3"
              placeholder="一行一条，如 Plugins/TodoTracker/**" @blur="commit"></textarea>
          </div>
          <div class="td-grow">
            <label class="td-label" for="td-forbid">禁止改动范围</label>
            <textarea id="td-forbid" v-model="form.forbiddenScope" class="td-input td-area" rows="3"
              placeholder="一行一条，如 宿主实体结构 / 生产环境" @blur="commit"></textarea>
          </div>
        </div>
        <label class="td-label" for="td-accept">验收判据（每行一条）</label>
        <textarea id="td-accept" v-model="form.acceptance" class="td-input td-area" rows="3"
          placeholder="- [ ] 可被命令或界面判定的结论" @blur="commit"></textarea>
        <label class="td-label" for="td-verify">验证命令（每行一条）</label>
        <textarea id="td-verify" v-model="form.verification" class="td-input td-area" rows="2"
          placeholder="dotnet test --filter ~TodoTracker" @blur="commit"></textarea>
      </section>

      <section class="td-block">
        <div class="td-block-head">
          <span class="td-label td-strong">从工件导入</span>
          <button type="button" class="td-link" :disabled="importBusy" @click="loadSets">
            {{ importBusy ? '读取中…' : '列目录' }}
          </button>
        </div>
        <p v-if="setsError" class="td-empty-line">{{ setsError }}</p>
        <template v-else-if="sets.length">
          <select class="td-input" :value="chosenDir" @change="selectSet(sets.find(x => x.dir === ($event.target as HTMLSelectElement).value)!)">
            <option value="">选择工件目录…</option>
            <option v-for="set in sets" :key="set.dir" :value="set.dir">
              {{ set.dir }}（{{ set.files.length }} 件{{ set.hasCoreFiles ? ' · 含核心件' : '' }}）
            </option>
          </select>
          <div v-if="chosenSet" class="td-files">
            <label v-for="f in chosenSet.files" :key="f.name" class="td-file">
              <input v-model="chosenFiles" type="checkbox" :value="f.name">
              <span class="td-mono">{{ f.name }}</span>
              <span v-if="f.isCore" class="td-core">核心</span>
              <span class="td-dim">{{ (f.size / 1024).toFixed(1) }} KB</span>
            </label>
          </div>
          <div class="td-row td-row-top">
            <button type="button" class="td-btn is-primary"
              :disabled="importBusy || !chosenDir || !chosenFiles.length" @click="doImport(false)">
              导入为正文（默认勾核心四件）
            </button>
            <span class="td-dim">来源：<span class="td-mono">{{ task.artifactRef || '—' }}</span></span>
          </div>
        </template>
        <p v-else class="td-dim">点「列目录」读取该项目 docs/ai/pilot 下的工件。</p>
      </section>

      <section class="td-block">
        <div class="td-block-head">
          <span class="td-label td-strong">下发</span>
          <button type="button" class="td-link" :disabled="previewBusy" @click="openPreview">
            {{ previewBusy ? '生成中…' : '生成提示词' }}
          </button>
        </div>
        <div class="td-row td-row-top">
          <button type="button" class="td-btn is-primary" :disabled="!!missingText" @click="doDispatch">下发</button>
          <select v-model="form.permissionMode" class="td-input" title="委派权限模式" @change="commit">
            <option value="read-only">read-only</option>
            <option value="workspace-write">workspace-write</option>
            <option value="accept-edits">accept-edits</option>
          </select>
          <select v-if="s.preview?.agents.length" v-model="agentId" class="td-input" title="执行 agent"
            data-test="delegate-agent">
            <option :value="undefined">默认 agent</option>
            <option v-for="a in s.preview.agents" :key="a.id" :value="a.id">{{ a.name }}（{{ a.vendor }}）</option>
          </select>
          <button type="button" class="td-btn" :disabled="!canDelegate"
            :title="delegateHint" @click="doDelegate">交给 AgentHub 执行</button>
        </div>
        <p v-if="s.preview && !s.preview.canDispatch" class="td-missing">
          还缺：{{ missingLabels(s.preview.missing) || '必填项' }}
        </p>
        <ul v-if="s.preview?.warnings?.length" class="td-warns">
          <li v-for="w in s.preview.warnings" :key="w">{{ w }}</li>
        </ul>
        <template v-if="s.preview">
          <textarea class="td-input td-area td-area-code" rows="8" readonly :value="s.preview.promptMarkdown"
            aria-label="下发提示词"></textarea>
          <div class="td-row td-row-top">
            <button type="button" class="td-btn" @click="copyPrompt">复制提示词</button>
            <span class="td-dim">回报：POST /api/todos/by-key/{{ task.taskKey }}/records</span>
          </div>
        </template>
        <template v-if="task.agentTaskKey">
          <div class="td-row td-row-top">
            <span class="td-dim">委派状态：</span>
            <b>{{ terminalStatus || (s.agent?.ok ? s.agent.status : '读取中…') }}</b>
            <button type="button" class="td-link" @click="store.loadAgentStatus()">刷新</button>
            <button type="button" class="td-btn td-btn-sm" @click="store.recordAgentResult(task)">记为执行记录</button>
          </div>
          <p v-if="s.agent?.error && !s.agent.ok" class="td-missing">{{ s.agent.error }}</p>
          <p v-if="s.agent?.resultSummary" class="td-result" :title="s.agent.resultSummary">{{ s.agent.resultSummary }}</p>
        </template>
      </section>

      <section class="td-block">
        <span class="td-label td-strong">阶段</span>
        <div class="td-chips">
          <button v-for="t in task.allowedTargets" :key="t" type="button" class="td-chip"
            :data-test="`stage-${t}`" :title="t" @click="chooseStage(t)">{{ stageLabel(t) }}</button>
          <span v-if="!task.allowedTargets.length" class="td-dim">当前阶段没有可达目标</span>
        </div>
        <input v-if="task.allowedTargets.includes('Blocked')" v-model="blockReason" class="td-input"
          placeholder="阻塞原因（进 Blocked 必填）">
        <div class="td-row td-row-top">
          <button type="button" class="td-btn td-btn-sm" @click="store.completeOrReopen(task)">
            {{ task.status === 'Pending' ? '标记完成' : '重新打开' }}
          </button>
          <button type="button" class="td-btn td-btn-sm is-danger" @click="store.removeTodo(task)">删除任务</button>
        </div>
      </section>

      <section class="td-block">
        <ExecutionTimeline :task="task" />
      </section>
    </div>
  </div>
</template>

<style scoped>
.td {
  background: var(--el-bg-color);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: var(--el-border-radius-base, 4px);
  display: flex; flex-direction: column; height: 100%; box-sizing: border-box;
}
.td.is-busy { opacity: .82; }
.td-head { display: flex; align-items: center; gap: 8px; padding: 10px 12px; border-bottom: 1px solid var(--el-border-color-lighter); flex-shrink: 0; }
.td-stage { font-size: 11px; padding: 1px 7px; border-radius: 3px; }
.td-key { margin: 0; font-size: 11px; font-weight: 400; font-family: ui-monospace, Menlo, Consolas, monospace; color: var(--el-text-color-secondary); flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.td-close { border: 0; background: transparent; font-size: 18px; line-height: 1; color: var(--el-text-color-secondary); cursor: pointer; }

.td-scroll { flex: 1; min-height: 0; overflow: auto; padding: 12px; display: flex; flex-direction: column; gap: 14px; }
.td-scroll > * { flex-shrink: 0; }
.td-block { display: flex; flex-direction: column; gap: 5px; }
.td-block-head { display: flex; align-items: center; gap: 8px; }
.td-label { font-size: 12px; color: var(--el-text-color-secondary); }
.td-strong { font-size: 13px; font-weight: 600; color: var(--el-text-color-primary); }
.td-row { display: flex; gap: 8px; align-items: center; }
.td-row-top { align-items: flex-start; flex-wrap: wrap; }
.td-grow { flex: 1; min-width: 0; }

.td-input {
  width: 100%; box-sizing: border-box; border: 1px solid var(--el-border-color);
  background: var(--el-bg-color); color: var(--el-text-color-primary);
  border-radius: var(--el-border-radius-base, 4px); padding: 5px 8px; font-size: 13px; line-height: 1.6;
}
.td-area { resize: vertical; min-height: 46px; }
.td-area-tall { min-height: 120px; font-family: ui-monospace, Menlo, Consolas, monospace; font-size: 12px; }
.td-area-code { font-family: ui-monospace, Menlo, Consolas, monospace; font-size: 11.5px; background: var(--el-fill-color-lighter); }
.td-mono { font-family: ui-monospace, Menlo, Consolas, monospace; font-size: 12px; word-break: break-all; margin: 0; }

.td-btn { border: 1px solid var(--el-border-color); background: var(--el-bg-color); color: var(--el-text-color-regular); border-radius: var(--el-border-radius-base, 4px); padding: 5px 12px; font-size: 13px; cursor: pointer; }
.td-btn:disabled { opacity: .55; cursor: not-allowed; }
.td-btn-sm { padding: 3px 9px; font-size: 12px; }
.td-btn.is-primary { background: var(--el-color-primary); border-color: var(--el-color-primary); color: #fff; }
.td-btn.is-danger { color: var(--el-color-danger); border-color: var(--el-color-danger-light-5); }
.td-link { border: 0; background: transparent; color: var(--el-color-primary); cursor: pointer; font-size: 12px; padding: 0; }
.td-link:disabled { color: var(--el-text-color-placeholder); cursor: not-allowed; }

.td-dirty { margin: 2px 0 0; font-size: 12px; color: var(--el-color-warning); }
.td-missing { font-size: 12px; color: var(--el-color-danger); margin: 0; }
.td-ok { font-size: 12px; color: var(--el-color-success); }
.td-empty-line { margin: 0; font-size: 12px; color: var(--el-text-color-secondary); line-height: 1.7; }
.td-dim { color: var(--el-text-color-secondary); font-size: 12px; }
.td-warns { margin: 4px 0 0; padding-left: 18px; font-size: 12px; color: var(--el-color-warning); }
.td-result { margin: 0; font-size: 12px; color: var(--el-text-color-regular); max-height: 76px; overflow: auto; border-left: 2px solid var(--el-border-color); padding-left: 8px; }

.td-chips { display: flex; flex-wrap: wrap; gap: 6px; }
.td-chip { border: 1px solid var(--el-border-color); background: var(--el-bg-color); color: var(--el-color-primary); border-radius: 999px; padding: 3px 11px; font-size: 12px; cursor: pointer; }

.td-files { display: flex; flex-direction: column; gap: 3px; margin-top: 4px; }
.td-file { display: flex; align-items: center; gap: 6px; font-size: 12px; }
.td-core { font-size: 10px; padding: 0 5px; border-radius: 3px; background: var(--el-color-success-light-9); color: var(--el-color-success); }
.td-proj-line { margin: 0; font-size: 13px; }

.tag-info { background: var(--el-fill-color-light); color: var(--el-text-color-secondary); }
.tag-primary { background: var(--el-color-primary-light-9); color: var(--el-color-primary); }
.tag-success { background: var(--el-color-success-light-9); color: var(--el-color-success); }
.tag-warning { background: var(--el-color-warning-light-9); color: var(--el-color-warning); }
.tag-danger { background: var(--el-color-danger-light-9); color: var(--el-color-danger); }
</style>
