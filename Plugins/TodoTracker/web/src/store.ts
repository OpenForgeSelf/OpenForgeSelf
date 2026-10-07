/**
 * 待办插件界面的状态与动作（模块级 reactive 单例）。
 *
 * 三条一贯口径（plugin-development §3.4 交互设计统一要求）：
 * 1. **点即保存**：改完就发请求，不摆一个"再点保存才生效"的按钮；
 * 2. **成败可见**：每个动作都落到轻提示，失败把后端 reason 原文端出来且不清空表单；
 * 3. **不闪**：刷新后先比对新旧 JSON，一致就不赋值，避免整块列表重渲染。
 *
 * 提示与确认一律走 `./notify`（插件自带，不依赖宿主 Element Plus 桥）。
 */
import { computed, reactive } from 'vue'
import * as api from './http'
import { confirmAction, showFailure, showToast } from './notify'
import type {
  AgentStatus,
  DelegateResult,
  DispatchPreview,
  ExecutionDraft,
  ImportResult,
  TaskExecution,
  TodoItem,
  TodoProject,
  TodoSaveRequest
} from './types'

export const state = reactive({
  items: [] as TodoItem[],
  total: 0,
  page: 1,
  pageSize: 20,
  loading: false,

  stageFilter: '' as string,
  statusFilter: '' as string,
  projectFilter: 0,
  keyword: '',

  projects: [] as TodoProject[],
  selectedId: 0,
  selected: null as TodoItem | null,
  records: [] as TaskExecution[],
  recordsTotal: 0,
  preview: null as DispatchPreview | null,
  agent: null as AgentStatus | null,
  operating: false
})

export const selectedProject = computed<TodoProject | null>(() =>
  state.projects.find(p => p.id === (state.selected?.projectId ?? -1)) ?? null)

function same(a: unknown, b: unknown): boolean {
  try {
    return JSON.stringify(a) === JSON.stringify(b)
  } catch {
    return false
  }
}

export async function loadTodos(): Promise<void> {
  state.loading = true
  try {
    const page = await api.listTodos({
      status: state.statusFilter || undefined,
      stage: state.stageFilter || undefined,
      projectId: state.projectFilter || undefined,
      q: state.keyword.trim() || undefined,
      page: state.page,
      pageSize: state.pageSize
    })
    const items = page?.items ?? []
    // 只换引用不换语义：内容一致时不重新赋值，轮询就不闪
    if (!same(items, state.items)) state.items = items
    state.total = page?.total ?? 0
    syncSelected()
  } catch (e) {
    showFailure('读取任务列表', e)
  } finally {
    state.loading = false
  }
}

export async function loadProjects(): Promise<void> {
  try {
    const list = (await api.listProjects()) ?? []
    if (!same(list, state.projects)) state.projects = list
  } catch (e) {
    showFailure('读取项目清单', e)
  }
}

function syncSelected(): void {
  if (!state.selectedId) return
  const hit = state.items.find(t => t.id === state.selectedId)
  if (hit) {
    if (!same(hit, state.selected)) state.selected = { ...hit }
    return
  }
  // 当前页里找不到（被过滤掉/翻页了）：直接按 id 取一条，详情面板不能空着
  void refreshSelected()
}

async function refreshSelected(): Promise<void> {
  try {
    const fresh = await api.getTodo(state.selectedId)
    state.selected = fresh ?? null
    if (!fresh) state.selectedId = 0
  } catch (e) {
    showFailure('读取任务详情', e)
  }
}

export async function selectTodo(id: number): Promise<void> {
  state.selectedId = id
  state.preview = null
  state.agent = null
  syncSelected()
  await loadRecords()
}

export function closeDetail(): void {
  state.selectedId = 0
  state.selected = null
  state.records = []
  state.recordsTotal = 0
  state.preview = null
  state.agent = null
}

export async function loadRecords(): Promise<void> {
  if (!state.selectedId) return
  try {
    const page = await api.listRecords(state.selectedId, 1, 100)
    // 最新一条在最上面：时间线倒序读，序号本身仍是服务端递增的
    state.records = [...(page?.items ?? [])].reverse()
    state.recordsTotal = page?.total ?? 0
  } catch (e) {
    showFailure('读取执行记录', e)
  }
}

export async function createTodo(title: string): Promise<TodoItem | null> {
  try {
    const created = await api.createTodo({ title })
    showToast('已创建任务（草稿），补齐四栏后可下发', 'success')
    await loadTodos()
    if (created) await selectTodo(created.id)
    return created ?? null
  } catch (e) {
    showFailure('创建任务', e)
    return null
  }
}

/** 详情表单保存：只发变化字段（null = 不动），部分更新不覆盖别人改过的栏位。 */
export async function saveDetail(patch: TodoSaveRequest): Promise<boolean> {
  if (!state.selected) return false
  state.operating = true
  try {
    const saved = await api.updateTodo(state.selected.id, patch)
    if (saved) applyUpdated(saved)
    showToast('已保存', 'success')
    return true
  } catch (e) {
    showFailure('保存任务', e)
    return false
  } finally {
    state.operating = false
  }
}

export async function removeTodo(task: TodoItem): Promise<void> {
  const ok = await confirmAction({
    title: '删除任务',
    message: `确认删除「${task.title}」？`,
    detail: `该任务的 ${task.recordCount} 条执行记录会一并删除，删除后不可撤销。`,
    confirmText: '删除',
    danger: true
  })
  if (!ok) return
  try {
    await api.removeTodo(task.id)
    showToast('已删除', 'success')
    if (state.selectedId === task.id) closeDetail()
    await loadTodos()
  } catch (e) {
    showFailure('删除任务', e)
  }
}

/** 阶段流转。Blocked 的原因由界面先收齐再传（后端同口径，省一次 400 往返）。 */
export async function changeStage(task: TodoItem, target: string, blockReason?: string): Promise<void> {
  state.operating = true
  try {
    const saved = await api.changeStage(task.id, target, blockReason, blockReason)
    if (saved) {
      applyUpdated(saved)
      showToast(`已流转：${task.stageLabel} → ${saved.stageLabel}`, 'success')
    }
  } catch (e) {
    showFailure('状态流转', e)
  } finally {
    state.operating = false
    await loadRecords()
  }
}

export async function completeOrReopen(task: TodoItem): Promise<void> {
  const toDone = task.status === 'Pending'
  if (toDone) {
    const ok = await confirmAction({
      title: '标记完成',
      message: `确认把「${task.title}」标记为完成？`,
      detail: '完成后任务仍在列表里（阶段=完成），可用「重新打开」撤回。',
      confirmText: '标记完成'
    })
    if (!ok) return
  }
  state.operating = true
  try {
    const saved = toDone ? await api.completeTodo(task.id) : await api.reopenTodo(task.id)
    if (saved) {
      applyUpdated(saved)
      showToast(toDone ? '已标记完成' : '已重新打开（回到草稿）', 'success')
    }
  } catch (e) {
    showFailure(toDone ? '标记完成' : '重新打开', e)
  } finally {
    state.operating = false
  }
}

function applyUpdated(task: TodoItem): void {
  state.selected = task
  const idx = state.items.findIndex(t => t.id === task.id)
  if (idx >= 0) state.items[idx] = task
}

// ── 项目关联 ──────────────────────────────────────────────────────────

/** 关联项目：任何写法都由服务端归一后落库，提示里回显"你的写法被理解成什么"。 */
export async function linkProject(task: TodoItem, path: string): Promise<boolean> {
  state.operating = true
  try {
    const saved = await api.linkProject(task.id, path)
    if (saved) {
      applyUpdated(saved)
      showToast(
        saved.projectPathRaw && saved.projectPathRaw !== saved.projectRoot
          ? `已关联项目：${saved.projectRoot}（由「${saved.projectPathRaw}」归一）`
          : `已关联项目：${saved.projectRoot}`,
        'success', 6000)
      await loadProjects()
      return true
    }
    return false
  } catch (e) {
    showFailure('关联项目', e)
    return false
  } finally {
    state.operating = false
  }
}

export async function unlinkProject(task: TodoItem): Promise<void> {
  const ok = await confirmAction({
    title: '解除项目关联',
    message: `解除「${task.title}」与 ${task.projectRoot || '（未关联）'} 的关联？`,
    detail: '只解除本任务的关联，不会删除宿主里的项目档案，也不影响执行记录。',
    confirmText: '解除关联'
  })
  if (!ok) return
  state.operating = true
  try {
    const saved = await api.unlinkProject(task.id)
    if (saved) applyUpdated(saved)
    showToast('已解除关联', 'success')
  } catch (e) {
    showFailure('解除项目关联', e)
  } finally {
    state.operating = false
  }
}

// ── 工件导入 ──────────────────────────────────────────────────────────

export async function importArtifacts(
  task: TodoItem, dir: string, files: string[], overwrite: boolean): Promise<ImportResult | null> {
  state.operating = true
  try {
    const result = await api.importArtifacts(task.id, dir, files, overwrite)
    if (result?.ok) {
      await refreshSelected()
      showToast(`已导入 ${result.imported.length} 个工件（正文 ${result.contentLength} 字符${
        result.acceptanceExtracted ? `，提取判据 ${result.acceptanceExtracted} 条` : ''}）`, 'success', 6000)
    } else if (result?.conflict) {
      showToast(result.error ?? '正文已有内容，需确认后覆盖', 'warning', 6000)
    } else if (result) {
      showToast(result.error ?? '导入失败', 'error')
    }
    return result ?? null
  } catch (e) {
    showFailure('导入工件', e)
    return null
  } finally {
    state.operating = false
    await loadRecords()
  }
}

// ── 下发 / 委派 ───────────────────────────────────────────────────────

export async function loadPreview(task: TodoItem): Promise<void> {
  try {
    state.preview = (await api.dispatchPreview(task.id)) ?? null
  } catch (e) {
    state.preview = null
    showFailure('生成下发预览', e)
  }
}

export async function dispatchTask(task: TodoItem, assignee: string): Promise<void> {
  state.operating = true
  try {
    const saved = await api.dispatchTask(task.id, assignee || undefined)
    if (saved) {
      applyUpdated(saved)
      showToast('已下发。提示词可直接复制给任意 agent', 'success', 5000)
    }
  } catch (e) {
    showFailure('下发任务', e)
  } finally {
    state.operating = false
    await loadRecords()
  }
}

export async function delegateToAgent(
  task: TodoItem, agentId?: number, permissionMode?: string): Promise<DelegateResult | null> {
  state.operating = true
  try {
    const result = await api.delegateToAgent(task.id, agentId, permissionMode)
    if (result?.ok) {
      await refreshSelected()
      const note = result.backfillWarning
        ?? `已交给 ${result.agentName}（${result.status}），taskKey=${result.taskKey}`
      showToast(note, result.backfillWarning ? 'warning' : 'success', 8000)
      await loadAgentStatus()
      await loadRecords()
    } else if (result?.seamMissing) {
      showToast(result.error ?? '未检测到 agent 委派能力（agent-hub 未启用）', 'warning', 6000)
    } else {
      showToast(result?.error ?? '委派失败', 'error')
    }
    return result ?? null
  } catch (e) {
    showFailure('一键交给 AgentHub', e)
    return null
  } finally {
    state.operating = false
  }
}

export async function loadAgentStatus(): Promise<void> {
  if (!state.selected?.agentTaskKey) {
    state.agent = null
    return
  }
  try {
    state.agent = (await api.agentStatus(state.selected.id)) ?? null
  } catch (e) {
    state.agent = null
    showFailure('读取委派状态', e)
  }
}

export async function recordAgentResult(task: TodoItem): Promise<void> {
  state.operating = true
  try {
    const r = await api.recordAgentResult(task.id)
    if (r?.ok) {
      showToast(`agent 结果已记入执行记录（第 ${r.seq} 条，当前 ${r.stage}）`, 'success')
      await refreshSelected()
      await loadRecords()
    } else {
      showToast(r?.error ?? '回写失败', 'error')
    }
  } catch (e) {
    showFailure('回写 agent 结果', e)
  } finally {
    state.operating = false
  }
}

export async function appendRecord(task: TodoItem, draft: ExecutionDraft): Promise<boolean> {
  if (!draft.action.trim()) {
    showToast('「做了什么操作」不能为空', 'warning')
    return false
  }
  state.operating = true
  try {
    const saved = await api.appendRecord(task.id, draft)
    if (saved) {
      applyUpdated(saved)
      showToast('执行记录已追加', 'success')
    }
    await loadRecords()
    return true
  } catch (e) {
    showFailure('追加执行记录', e)
    return false
  } finally {
    state.operating = false
  }
}
