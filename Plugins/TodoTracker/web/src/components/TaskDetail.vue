<script setup lang="ts">
/**
 * 任务详情（抽屉内）：概要恒显 + 分类折叠渐进披露。
 *
 * 交互口径（plugin-development §3.4 + pilot drawer-progressive）：
 * - **点即保存**：文本栏失焦即回写（只发变化的那一栏）；
 * - **渐进披露**：基本信息 / 项目 / 工作单元 / 范围权限 / 下发委派 / 执行记录默认收起；
 * - **缺项反馈**：点下发或列表「去补齐」时展开工作单元并点名缺项；
 * - **状态突变要二次确认**：删除/覆盖正文/解除关联走 NotifyHost 确认框；
 * - **可达阶段照服务端 allowedTargets 渲染**，界面不抄流转表。
 */
import { computed, inject, nextTick, onMounted, reactive, ref, watch } from 'vue'
import ExecutionTimeline from './ExecutionTimeline.vue'
import * as store from '../store'
import { listArtifactSets } from '../http'
import { confirmAction, showToast } from '../notify'
import {
  agentNameOrFallback, agentStatusMeta, copyText, defaultSelectionFor, missingLabels,
  priorityLabel, shortPath, stageLabel, stageTagType
} from '../actions'
import type { ArtifactSet, TodoItem } from '../types'
import type { TagType } from '../actions'

const props = defineProps<{ task: TodoItem }>()
const s = store.state

type SectionKey = 'basic' | 'project' | 'workunit' | 'scope' | 'dispatch' | 'history'
const openSections = reactive<Record<SectionKey, boolean>>({
  basic: false,
  project: false,
  workunit: false,
  scope: false,
  dispatch: false,
  history: false
})

function toggleSection(key: SectionKey): void {
  openSections[key] = !openSections[key]
}

function expandWorkUnitAndScroll(): void {
  openSections.workunit = true
  void nextTick(() => {
    document.querySelector('[data-test="section-workunit"]')?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  })
}

watch(() => s.expandWorkUnitTick, () => {
  if (s.expandWorkUnitTick > 0) expandWorkUnitAndScroll()
})

const form = reactive({
  title: '', remark: '', objective: '', content: '', acceptance: '', verification: '',
  allowedScope: '', forbiddenScope: '', assignee: '', priority: 1, permissionMode: 'read-only'
})

const blockReason = ref('')
const projectPath = ref('')
const projectEditing = ref(false)
/** 项目关联模式：'path' = 手动输入路径；'select' = 从宿主项目档案选择（FR-1.2）。 */
const projectMode = ref<'path' | 'select'>('path')
const selectedProjectId = ref(0)

const sets = ref<ArtifactSet[]>([])
const setsError = ref('')
const chosenDir = ref('')
const chosenFiles = ref<string[]>([])
const importBusy = ref(false)

const previewBusy = ref(false)
const agentId = ref<number | undefined>(undefined)
/** 委派引擎：agenthub=外部 AgentHub / builtin=本工具 AI Agent（按任务既有引擎默认，未委派过默认外部）。 */
const engine = ref<'agenthub' | 'builtin'>(props.task.agentEngine === 'builtin' ? 'builtin' : 'agenthub')
/** 内置角色序号（1..7；undefined=默认程序员）。 */
const agentRoleIndex = ref<number | undefined>(undefined)
/** 内置角色序号 → 角色 id（与后端 BuiltInRoleIndex 映射一致）。 */
const BUILTIN_ROLE_BY_INDEX: Record<number, string> = {
  1: 'agent.coordinator', 2: 'agent.analyst', 3: 'agent.critic', 4: 'agent.generalist',
  5: 'agent.writer', 6: 'agent.researcher', 7: 'agent.programmer',
}

/** 委派对象回显：打开详情按任务既有委派回填引擎/角色/agent 下拉。 */
function syncDelegation(t: TodoItem): void {
  engine.value = t.agentEngine === 'builtin' ? 'builtin' : 'agenthub'
  if (engine.value === 'builtin') {
    agentRoleIndex.value = t.agentId >= 1 && t.agentId <= 7 ? t.agentId : undefined
  } else {
    agentId.value = t.agentId > 0 ? t.agentId : undefined
  }
}

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

function resetSections(): void {
  openSections.basic = false
  openSections.project = false
  openSections.workunit = false
  openSections.scope = false
  openSections.dispatch = false
  openSections.history = false
}

fill(props.task)
syncDelegation(props.task)
watch(() => props.task.id, () => {
  fill(props.task)
  syncDelegation(props.task)
  resetSections()
  void store.loadPreview(props.task)
})

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
const canDelegateBuiltIn = computed(() => !!s.preview?.canDispatch && !!s.preview?.builtInAvailable)
/**
 * 禁用原因必须"照实说"：预览还没生成 / 接缝不在场 / 四栏不齐，是三种不同的下一步。
 */
const delegateHint = computed(() => {
  if (canDelegate.value) return '经 agent-hub 起一个真实 agent 执行'
  if (!s.preview) return '先生成提示词（委派前要先据它判断）'
  if (!s.preview.delegationAvailable) return s.preview.delegationError || '未检测到 agent 委派能力'
  return '四栏齐备（目标/正文/判据/验证命令）后才能一键委派'
})
const delegateHintBuiltIn = computed(() => {
  if (canDelegateBuiltIn.value) return '经本工具内置 AI Agent 起一个计划驱动执行（默认角色：程序员）'
  if (!s.preview) return '先生成提示词（委派前要先据它判断）'
  if (!s.preview.builtInAvailable) return s.preview.builtInError || '未检测到本工具 AI Agent 能力'
  return '四栏齐备（目标/正文/判据/验证命令）后才能一键委派'
})
const terminalStatus = computed(() => {
  const a = s.agent
  if (!a?.ok) return ''
  return `${a.status}${a.exitCode != null ? ` · exit ${a.exitCode}` : ''}${a.errorCode ? ` · ${a.errorCode}` : ''}`
})

const agentBadge = computed(() => {
  const a = s.agent
  if (!a) return { label: '', type: 'info' as TagType }
  if (!a.ok && a.error) return { label: a.error, type: 'info' as TagType }
  return agentStatusMeta(a.status)
})

const summaryOpen = ref(false)
const SUMMARY_CAP = 240
const summaryPreview = computed(() => {
  const raw = s.agent?.resultSummary ?? ''
  if (raw.length <= SUMMARY_CAP || summaryOpen.value) return raw
  return `${raw.slice(0, SUMMARY_CAP)}…`
})
const summaryLong = computed(() => (s.agent?.resultSummary?.length ?? 0) > SUMMARY_CAP)

const agentsEmpty = computed(() => !!s.preview?.delegationAvailable && !s.preview.agents.length)

const agentDisplayName = computed(() => {
  if (s.agent?.agentName) return s.agent.agentName
  if (props.task.agentEngine === 'builtin') {
    const roleName = s.preview?.builtInAgents.find(a => a.id === props.task.agentId)?.name
    return roleName || `本工具AI·角色#${props.task.agentId || '?'}`
  }
  return agentNameOrFallback(undefined, props.task.agentId)
})

const openPage = inject<((path: string) => void) | null>('forgeOpenPage', null)
function goRegister(): void {
  const bridge = openPage ?? (window as unknown as { __FORGE_OPEN_PAGE__?: (p: string) => void }).__FORGE_OPEN_PAGE__
  if (bridge) bridge('/agent-hub')
  else location.assign('/agent-hub')
}

function scrollToRecords(): void {
  openSections.history = true
  void nextTick(() => {
    document.querySelector('[data-test="execution-timeline"]')?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  })
}

async function doRecord(): Promise<void> {
  await store.recordAgentResult(props.task)
  if (s.selectedId === props.task.id) scrollToRecords()
}

async function selectProject(): Promise<void> {
  if (!selectedProjectId.value) {
    showToast('先从列表选一个项目档案', 'warning')
    return
  }
  const ok = await store.linkProjectById(props.task, selectedProjectId.value)
  if (ok) projectMode.value = 'path'
}

/** 失焦即存：只提交真正改过的栏位。 */
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
  if (target === 'Done' || target === 'Cancelled') {
    const accepted = await confirmAction({
      title: target === 'Done' ? '确认完成任务' : '确认取消任务',
      message: `将「${props.task.title}」流转到「${stageLabel(target)}」？`,
      confirmText: target === 'Done' ? '确认完成' : '确认取消',
      danger: target === 'Cancelled'
    })
    if (!accepted) return
  }
  await store.changeStage(props.task, target, blockReason.value.trim() || undefined)
  blockReason.value = ''
}

async function saveProject(): Promise<void> {
  const path = projectPath.value.trim()
  if (!path) {
    showToast('要关联项目请先填路径（任意本机写法，由服务端归一）', 'warning')
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

/** 下发：缺项时不禁用按钮，点名反馈并展开工作单元（AC-UI-5）。 */
async function doDispatch(): Promise<void> {
  if (missingText.value) {
    showToast(`下发前还缺：${missingText.value}`, 'warning')
    expandWorkUnitAndScroll()
    return
  }
  await store.dispatchTask(props.task, form.assignee)
}

async function doDelegate(): Promise<void> {
  if (missingText.value) {
    showToast(`下发前还缺：${missingText.value}`, 'warning')
    expandWorkUnitAndScroll()
    return
  }
  const roleId = engine.value === 'builtin'
    ? (agentRoleIndex.value ? BUILTIN_ROLE_BY_INDEX[agentRoleIndex.value] ?? undefined : undefined)
    : undefined
  const result = await store.delegateToAgent(props.task, agentId.value, form.permissionMode, engine.value, roleId)
  if (result?.ok) {
    if (result.agentName && form.assignee !== result.agentName) {
      form.assignee = result.agentName
      commit()
    }
    await openPreview()
  }
}

onMounted(() => {
  void store.loadPreview(props.task)
  if (props.task.agentTaskKey) void store.loadAgentStatus()
})
</script>

<template>
  <div class="td" data-test="task-detail" :class="{ 'is-busy': s.operating }">
    <!-- 0 概要条（恒显，非折叠） -->
    <header class="td-head">
      <div class="td-head-title">
        <input
          id="td-title"
          v-model="form.title"
          class="td-input td-title-input"
          maxlength="200"
          aria-label="任务标题"
          @blur="commit"
        >
        <span class="td-stage" :class="`tag-${stageTagType(task.stage)}`">{{ task.stageLabel }}</span>
        <span class="td-prio">{{ priorityLabel(form.priority) }}</span>
        <span class="td-tid" data-test="td-tid" title="待办编号（agent 工具引用）">#{{ task.id }}</span>
        <span class="td-key" :title="`任务标识（GUID）：${task.taskKey}`">{{ task.taskKey }}</span>
      </div>
      <p v-if="missingText" class="td-soft-hint" data-test="detail-soft-hint">
        下发前待补齐：{{ missingText }}
        <button type="button" class="td-link" data-test="go-fill-workunit" @click="expandWorkUnitAndScroll">去补齐</button>
      </p>
      <div class="td-head-actions">
        <input v-if="task.allowedTargets.includes('Blocked')" v-model="blockReason" class="td-input td-block-reason"
          aria-label="阻塞原因" placeholder="流转为阻塞时填写原因">
        <button v-for="target in task.allowedTargets" :key="target" type="button" class="td-btn td-btn-sm"
          :class="{ 'is-primary': target === 'Done', 'is-danger': target === 'Cancelled' }"
          :data-test="`stage-${target}`" @click="chooseStage(target)">{{ stageLabel(target) }}</button>
      </div>
      <button type="button" class="td-close" aria-label="关闭详情" data-test="drawer-close" @click="store.closeDetail()">×</button>
    </header>

    <div class="td-scroll">
      <!-- 1 基本信息 -->
      <section class="td-section" data-test="section-basic">
        <button type="button" class="td-section-head" :aria-expanded="openSections.basic" @click="toggleSection('basic')">
          <span class="td-section-title">基本信息</span>
          <span class="td-section-chevron" :class="{ 'is-open': openSections.basic }">▸</span>
        </button>
        <div v-if="openSections.basic" class="td-section-body">
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
                :placeholder="task.agentTaskKey ? agentDisplayName || 'agent 名 / manual' : 'agent 名 / manual'"
                @blur="commit">
            </div>
          </div>
          <p v-if="dirty" class="td-dirty">有未保存的改动（失焦即保存）</p>
        </div>
      </section>

      <!-- 2 项目关联 -->
      <section class="td-section" data-test="section-project">
        <button type="button" class="td-section-head" :aria-expanded="openSections.project" @click="toggleSection('project')">
          <span class="td-section-title">项目关联</span>
          <span v-if="!task.projectId" class="td-section-badge">建议关联</span>
          <span class="td-section-chevron" :class="{ 'is-open': openSections.project }">▸</span>
        </button>
        <div v-if="openSections.project" class="td-section-body">
          <div class="td-block-head">
            <span class="td-label td-strong">项目路径</span>
            <span v-if="!task.projectId" class="td-mode-switch">
              <button type="button" class="td-link" :class="{ 'is-on': projectMode === 'select' }"
                :title="store.state.projects.length ? '从宿主项目档案里选一个，自动带出项目地址' : '宿主还没有项目档案，可输入路径创建'"
                @click="projectMode = projectMode === 'select' ? 'path' : 'select'">
                {{ projectMode === 'select' ? '改用输入路径' : '选择项目' }}
              </button>
            </span>
            <button v-if="!projectEditing && task.projectId" type="button" class="td-link" @click="projectEditing = true">改</button>
          </div>
          <template v-if="task.projectId">
            <p class="td-proj-line"><b>{{ projectLabel }}</b></p>
            <p class="td-mono" :title="task.projectRoot">{{ task.projectRoot }}</p>
            <p v-if="task.projectPathRaw && task.projectPathRaw !== task.projectRoot" class="td-dim">
              你的写法：<span class="td-mono">{{ task.projectPathRaw }}</span>
            </p>
            <div v-if="projectEditing" class="td-row td-row-top">
              <input v-model="projectPath" class="td-input td-grow" placeholder="填写项目根目录路径">
              <button type="button" class="td-btn is-primary" @click="saveProject">保存</button>
              <button type="button" class="td-btn" @click="removeProject">解除</button>
            </div>
          </template>
          <template v-else>
            <p class="td-empty-line">未关联项目 —— 下发时 agent 不知道在哪个目录干活。</p>
            <div v-if="projectMode === 'select'" class="td-row td-row-top">
              <select v-model.number="selectedProjectId" class="td-input td-grow" data-test="project-select">
                <option :value="0">选择项目档案…</option>
                <option v-for="p in store.state.projects" :key="p.id" :value="p.id" :title="`${p.root}`">
                  {{ p.name || shortPath(p.root, 24) }} · {{ shortPath(p.root, 30) }}（{{ p.taskCount }} 任务）
                </option>
              </select>
              <button type="button" class="td-btn is-primary" :disabled="!selectedProjectId" @click="selectProject">关联</button>
            </div>
            <div v-else class="td-row td-row-top">
              <input v-model="projectPath" class="td-input td-grow" placeholder="填写项目根目录路径（任意本机写法，由服务端归一）">
              <button type="button" class="td-btn is-primary" :disabled="!projectPath.trim()" @click="saveProject">关联</button>
            </div>
            <p v-if="projectMode === 'select' && !store.state.projects.length" class="td-empty-line">
              宿主还没有项目档案 —— 可切回「输入路径」直接填目录创建。
            </p>
          </template>
        </div>
      </section>

      <!-- 3 工作单元 · 下发必填 -->
      <section class="td-section" data-test="section-workunit">
        <button type="button" class="td-section-head" :aria-expanded="openSections.workunit" @click="toggleSection('workunit')">
          <span class="td-section-title">工作单元 · 下发必填</span>
          <span v-if="missingText" class="td-section-badge">待补齐</span>
          <span v-else class="td-section-badge is-ok">齐备</span>
          <span class="td-section-chevron" :class="{ 'is-open': openSections.workunit }">▸</span>
        </button>
        <div v-if="openSections.workunit" class="td-section-body">
          <div class="td-block-head">
            <span v-if="missingText" class="td-soft-inline" data-test="workunit-missing">下发前还缺：{{ missingText }}</span>
            <span v-else class="td-ok">四栏齐备，可下发</span>
          </div>
          <label class="td-label" for="td-obj">Objective（可验证目标）</label>
          <input id="td-obj" v-model="form.objective" class="td-input" maxlength="500"
            placeholder="做完后仓库达到的可验证状态，一句话（下发前再填）" @blur="commit">
          <label class="td-label" for="td-content">任务正文（markdown）</label>
          <textarea id="td-content" v-model="form.content" class="td-input td-area td-area-tall"
            placeholder="要做什么、背景与约束；也可从「下发与委派」导入工件（下发前再填）" @blur="commit"></textarea>
          <label class="td-label" for="td-accept">验收判据（每行一条）</label>
          <textarea id="td-accept" v-model="form.acceptance" class="td-input td-area" rows="3"
            placeholder="- [ ] 可被命令或界面判定的结论（下发前再填）" @blur="commit"></textarea>
          <label class="td-label" for="td-verify">验证命令（每行一条）</label>
          <textarea id="td-verify" v-model="form.verification" class="td-input td-area" rows="2"
            placeholder="dotnet test --filter ~TodoTracker（下发前再填）" @blur="commit"></textarea>
        </div>
      </section>

      <!-- 4 范围与权限 -->
      <section class="td-section" data-test="section-scope">
        <button type="button" class="td-section-head" :aria-expanded="openSections.scope" @click="toggleSection('scope')">
          <span class="td-section-title">范围与权限</span>
          <span class="td-section-chevron" :class="{ 'is-open': openSections.scope }">▸</span>
        </button>
        <div v-if="openSections.scope" class="td-section-body">
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
          <label class="td-label" for="td-perm">权限模式</label>
          <select id="td-perm" v-model="form.permissionMode" class="td-input" title="委派权限模式" @change="commit">
            <option value="read-only">read-only</option>
            <option value="workspace-write">workspace-write</option>
            <option value="accept-edits">accept-edits</option>
          </select>
        </div>
      </section>

      <!-- 5 下发与委派 -->
      <section class="td-section" data-test="section-dispatch">
        <button type="button" class="td-section-head" :aria-expanded="openSections.dispatch" @click="toggleSection('dispatch')">
          <span class="td-section-title">下发与委派</span>
          <span class="td-section-chevron" :class="{ 'is-open': openSections.dispatch }">▸</span>
        </button>
        <div v-if="openSections.dispatch" class="td-section-body">
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

          <div class="td-block-head" style="margin-top: 10px">
            <span class="td-label td-strong">下发</span>
            <button type="button" class="td-link" :disabled="previewBusy" @click="openPreview">
              {{ previewBusy ? '生成中…' : '生成提示词' }}
            </button>
          </div>
          <div class="td-row td-row-top">
            <button type="button" class="td-btn is-primary" data-test="dispatch-btn" @click="doDispatch">下发</button>
          </div>
          <div class="td-row td-row-top" data-test="delegate-engine-row">
            <select v-model="engine" class="td-input" title="委派引擎" data-test="delegate-engine">
              <option value="agenthub">外部 AgentHub</option>
              <option value="builtin">本工具 AI Agent</option>
            </select>
            <select v-if="engine === 'builtin'" v-model="agentRoleIndex"
              class="td-input" title="内置角色" data-test="delegate-role">
              <template v-if="s.preview?.builtInAgents.length">
                <option :value="undefined">默认（程序员）</option>
                <option v-for="a in s.preview.builtInAgents" :key="a.id" :value="a.id">{{ a.name }}</option>
              </template>
              <option v-else :value="undefined" disabled>
                {{ s.preview ? '本工具 AI Agent 未就绪（先生成提示词）' : '加载中…' }}
              </option>
            </select>
            <select v-else v-model="agentId" class="td-input" title="执行 agent" data-test="delegate-agent">
              <template v-if="s.preview?.agents.length">
                <option :value="undefined">默认 agent</option>
                <option v-for="a in s.preview.agents" :key="a.id" :value="a.id">{{ a.name }}（{{ a.vendor }}）</option>
              </template>
              <option v-else :value="undefined" disabled>
                {{ s.preview ? '暂无可用 agent（请先登记）' : '加载中…' }}
              </option>
            </select>
            <button v-if="engine === 'builtin'" type="button" class="td-btn" :disabled="!canDelegateBuiltIn && !missingText"
              :title="delegateHintBuiltIn" data-test="delegate-builtin" @click="doDelegate">交给本工具 AI Agent 执行</button>
            <button v-else type="button" class="td-btn" :disabled="!canDelegate && !missingText"
              :title="delegateHint" @click="doDelegate">交给 AgentHub 执行</button>
          </div>
          <p v-if="engine === 'agenthub' && !canDelegate" class="td-hint" data-test="delegate-hint">{{ delegateHint }}</p>
          <p v-if="engine === 'builtin' && !canDelegateBuiltIn" class="td-hint" data-test="delegate-hint-builtin">{{ delegateHintBuiltIn }}</p>
          <p v-if="agentsEmpty" class="td-hint">
            AgentHub 已就绪，但还没有可用 agent ——
            <button type="button" class="td-link" data-test="go-register-agent" @click="goRegister">去 Agent 中枢登记本机 agent</button>
            （opencode / claude 等）。
          </p>
          <p v-if="s.preview && !s.preview.canDispatch" class="td-soft-inline">
            下发前还缺：{{ missingLabels(s.preview.missing) || '必填项' }}
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
            <div class="td-row td-row-top" data-test="delegation-status">
              <span class="td-dim">委派状态：</span>
              <span v-if="s.agent?.agentName || task.agentId" class="td-dim">
                <span v-if="task.agentEngine === 'builtin'" class="td-engine-tag">本工具AI</span>
                agent <b>{{ agentDisplayName }}</b>
              </span>
              <span class="td-deleg-badge" :class="`tag-${agentBadge.type}`">{{ agentBadge.label || '读取中…' }}</span>
              <span v-if="terminalStatus && s.agent?.ok" class="td-dim">{{ terminalStatus }}</span>
              <button type="button" class="td-link" @click="store.loadAgentStatus()">刷新</button>
              <button type="button" class="td-btn td-btn-sm" @click="doRecord">记为执行记录</button>
              <button type="button" class="td-link" @click="scrollToRecords">查看执行记录 ↓</button>
            </div>
            <p v-if="s.agent?.error && !s.agent.ok" class="td-missing">{{ s.agent.error }}</p>
            <p v-if="s.agent?.resultSummary" class="td-result" :title="s.agent.resultSummary">{{ summaryPreview }}
              <button v-if="summaryLong" type="button" class="td-link" @click="summaryOpen = !summaryOpen">
                {{ summaryOpen ? '收起' : '展开全文' }}
              </button>
            </p>
            <p v-if="!s.agent?.ok && !s.agent?.error" class="td-dim">委派状态读取中…（每 15 秒自动刷新，结束会自动提示）</p>
          </template>
        </div>
      </section>

      <!-- 6 执行记录 -->
      <section class="td-section" data-test="section-history">
        <button type="button" class="td-section-head" :aria-expanded="openSections.history" @click="toggleSection('history')">
          <span class="td-section-title">执行记录</span>
          <span class="td-section-chevron" :class="{ 'is-open': openSections.history }">▸</span>
        </button>
        <div v-if="openSections.history" class="td-section-body">
          <div class="td-block-head">
            <button type="button" class="td-btn td-btn-sm is-danger" @click="store.removeTodo(task)">删除任务</button>
          </div>
          <ExecutionTimeline :task="task" />
        </div>
      </section>
    </div>
  </div>
</template>

<style scoped>
.td {
  background: var(--el-bg-color);
  display: flex; flex-direction: column; height: 100%; box-sizing: border-box;
}
.td-head { position: relative; padding: 12px 40px 12px 14px; border-bottom: 1px solid var(--el-border-color-lighter); flex-shrink: 0; display: flex; flex-direction: column; gap: 8px; }
.td-head-title { display: flex; align-items: center; gap: 8px; min-width: 0; flex-wrap: wrap; }
.td-title-input { flex: 1 1 100%; min-width: 0; font-size: 16px; font-weight: 600; }
.td-head-title .td-key { max-width: 82px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.td-head-actions { display: flex; align-items: center; justify-content: flex-start; gap: 5px; flex-wrap: wrap; }
.td-head .td-close { position: absolute; top: 10px; right: 10px; }
.td-block-reason { width: 180px; }
.td.is-busy { opacity: .82; }
.td-stage { font-size: 11px; padding: 1px 7px; border-radius: 3px; }
.td-prio { font-size: 11px; padding: 1px 5px; border-radius: 3px; background: var(--el-fill-color-light); color: var(--el-text-color-secondary); }
/* 待办编号徽标（PILOT-085）：数字 id，与 GUID 并排但样式区分 */
.td-tid { font-size: 11px; padding: 1px 6px; border: 1px solid var(--el-border-color-lighter); border-radius: 3px; background: var(--el-fill-color-light); color: var(--el-text-color-regular); }
.td-key { margin: 0; font-size: 11px; font-weight: 400; font-family: ui-monospace, Menlo, Consolas, monospace; color: var(--el-text-color-secondary); }
.td-close { border: 0; background: transparent; font-size: 18px; line-height: 1; color: var(--el-text-color-secondary); cursor: pointer; }

.td-soft-hint {
  margin: 0; font-size: 12px; color: var(--el-color-warning);
  background: var(--el-color-warning-light-9);
  border: 1px dashed var(--el-color-warning-light-5);
  border-radius: var(--el-border-radius-base, 4px);
  padding: 4px 8px; width: fit-content;
}
.td-soft-inline { font-size: 12px; color: var(--el-color-warning); margin: 0; }

.td-scroll { flex: 1; min-height: 0; overflow: auto; padding: 8px 12px 16px; display: flex; flex-direction: column; gap: 6px; }
.td-scroll > * { flex-shrink: 0; }

.td-section {
  border: 1px solid var(--el-border-color-lighter);
  border-radius: var(--el-border-radius-base, 4px);
  background: var(--el-bg-color);
  overflow: hidden;
}
.td-section-head {
  width: 100%; display: flex; align-items: center; gap: 8px;
  padding: 10px 12px; border: 0; background: var(--el-fill-color-blank, var(--el-bg-color));
  color: var(--el-text-color-primary); cursor: pointer; text-align: left;
}
.td-section-head:hover { background: var(--el-fill-color-lighter); }
.td-section-title { font-size: 13px; font-weight: 600; flex: 1; }
.td-section-badge {
  font-size: 11px; padding: 1px 7px; border-radius: 999px;
  background: var(--el-color-warning-light-9); color: var(--el-color-warning);
}
.td-section-badge.is-ok {
  background: var(--el-color-success-light-9); color: var(--el-color-success);
}
.td-section-chevron {
  font-size: 12px; color: var(--el-text-color-secondary);
  transition: transform .15s ease;
}
.td-section-chevron.is-open { transform: rotate(90deg); }
.td-section-body {
  display: flex; flex-direction: column; gap: 6px;
  padding: 0 12px 12px; border-top: 1px solid var(--el-border-color-extra-light, var(--el-border-color-lighter));
}

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
.td-btn.is-primary { background: var(--el-color-primary); border-color: var(--el-color-primary); color: var(--el-color-white); }
.td-btn.is-danger { color: var(--el-color-danger); border-color: var(--el-color-danger-light-5); }
.td-link { border: 0; background: transparent; color: var(--el-color-primary); cursor: pointer; font-size: 12px; padding: 0; }
.td-link:disabled { color: var(--el-text-color-placeholder); cursor: not-allowed; }
.td-mode-switch .td-link.is-on { color: var(--el-color-primary); font-weight: 600; }
.td-mode-switch { margin-left: auto; }

.td-dirty { margin: 2px 0 0; font-size: 12px; color: var(--el-color-warning); }
.td-missing { font-size: 12px; color: var(--el-color-danger); margin: 0; }
.td-ok { font-size: 12px; color: var(--el-color-success); }
.td-empty-line { margin: 0; font-size: 12px; color: var(--el-text-color-secondary); line-height: 1.7; }
.td-dim { color: var(--el-text-color-secondary); font-size: 12px; }
.td-warns { margin: 4px 0 0; padding-left: 18px; font-size: 12px; color: var(--el-color-warning); }
.td-result { margin: 0; font-size: 12px; color: var(--el-text-color-regular); max-height: 140px; overflow: auto; border-left: 2px solid var(--el-border-color); padding-left: 8px; }
.td-hint { margin: 0; font-size: 12px; color: var(--el-color-warning); line-height: 1.7; }
.td-deleg-badge { font-size: 11px; padding: 1px 8px; border-radius: 3px; white-space: nowrap; }
.td-engine-tag { font-size: 10px; padding: 0 5px; border-radius: 3px; background: var(--el-color-primary-light-9);
  color: var(--el-color-primary); margin-right: 4px; vertical-align: 1px; }
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
