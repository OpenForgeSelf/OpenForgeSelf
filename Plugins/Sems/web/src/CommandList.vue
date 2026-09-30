<template>
  <div class="clist">
    <div class="clist__head">
      <span class="clist__title">运行命令（{{ sortedCommands.length }}）</span>
      <button class="clist__add" @click="openAdd">+ 新增命令</button>
    </div>

    <div v-if="loading" class="clist__hint">加载命令中…</div>
    <div v-else-if="loadError" class="clist__hint clist__hint--error">加载失败：{{ loadError }}</div>
    <div v-else-if="sortedCommands.length === 0" class="clist__hint">
      还没有运行命令。点「+ 新增命令」添加一条，脚本会在该项目根目录里执行。
    </div>

    <ul v-else class="clist__items">
      <li v-for="(cmd, idx) in sortedCommands" :key="cmd.id" class="clist__item">
        <div class="clist__order">
          <button class="clist__order-btn" title="上移" :disabled="idx === 0" @click="move(idx, -1)">↑</button>
          <button class="clist__order-btn" title="下移" :disabled="idx === sortedCommands.length - 1" @click="move(idx, 1)">↓</button>
        </div>
        <div class="clist__main">
          <div class="clist__row1">
            <span class="clist__name">{{ cmd.name }}</span>
            <a v-if="cmd.url" class="clist__link" :href="cmd.url" target="_blank" rel="noopener" title="打开访问地址">↗</a>
          </div>
          <code class="clist__script" :title="cmd.script">{{ cmd.script }}</code>
        </div>
        <div class="clist__ops">
          <button class="clist__op clist__op--run" title="启动" :disabled="busyId === cmd.id" @click="run(cmd.id)">启动</button>
          <button class="clist__op" title="编辑" @click="openEdit(cmd)">编辑</button>
          <button class="clist__op clist__op--del" title="删除" @click="remove(cmd)">删除</button>
        </div>
      </li>
    </ul>

    <!-- 新增/编辑弹层（轻量 inline 表单，避免引入新依赖） -->
    <div v-if="dialogOpen" class="cmddlg">
      <div class="cmddlg__mask" @click="dialogOpen = false"></div>
      <div class="cmddlg__box">
        <h3 class="cmddlg__title">{{ editing ? '编辑命令' : '新增命令' }}</h3>
        <label class="cmddlg__field">
          <span>名称 *</span>
          <input v-model.trim="form.name" type="text" placeholder="如：前端 dev" />
        </label>
        <label class="cmddlg__field">
          <span>脚本 *</span>
          <input v-model.trim="form.script" type="text" placeholder="在项目根执行的命令，如 npm run dev" />
        </label>
        <label class="cmddlg__field">
          <span>访问地址</span>
          <input v-model.trim="form.url" type="text" placeholder="可选，如 http://localhost:5173" />
        </label>
        <p v-if="formError" class="cmddlg__err">{{ formError }}</p>
        <div class="cmddlg__foot">
          <button class="cmddlg__btn" @click="dialogOpen = false">取消</button>
          <button class="cmddlg__btn cmddlg__btn--primary" :disabled="saving" @click="save">{{ saving ? '保存中…' : '保存' }}</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * 命令管理列表（spec028 §6 / T07）：
 * - 列出某项目命令（名称/脚本/访问地址）
 * - 新增 / 编辑 / 删除（删除二次确认） / 排序（上移下移）
 * - 启动按钮（emit 给上层）
 * 调 ProjectCommandsController 四端点：
 *   GET  api/projects/{id}/commands
 *   POST api/projects/{id}/commands
 *   PUT  api/commands/{id}
 *   DELETE api/commands/{id}
 */
import { computed, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { apiGet, apiPost, apiPut, apiDelete } from './http'
import { deleteCommandMessage, errorMessage, runWithConfirm } from './confirmOps'
import type { CommandsResp, RunCommandInfo, RunCommandAdd, RunCommandUpdate } from './types'

const props = defineProps<{
  projectId: number
  /** 父级已随项目列表带过来的命令（用于初始渲染，进入时再拉全量确认）。 */
  commands: RunCommandInfo[]
}>()

const emit = defineEmits<{
  (e: 'changed'): void
  (e: 'run', commandId: number): void
}>()

const sortedCommands = ref<RunCommandInfo[]>([...props.commands])
const loading = ref(false)
const loadError = ref('')

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    const r = await apiGet<CommandsResp>(`/api/projects/${props.projectId}/commands`)
    sortedCommands.value = (r?.commands ?? []).slice().sort((a, b) => a.sort - b.sort)
  } catch (e) {
    loadError.value = errorMessage(e)
  } finally {
    loading.value = false
  }
}

// 父级传入的命令作为初始快照；挂载后拉全量确保与后端一致。
load()

const busyId = ref<number | null>(null)

async function run(id: number) {
  busyId.value = id
  try {
    await apiPost(`/api/commands/${id}/run`, {})
    emit('run', id)
  } catch (e) {
    ElMessage.error(`启动失败：${errorMessage(e)}`)
  } finally {
    busyId.value = null
  }
}

// ---- 排序（上移/下移）----
function move(idx: number, dir: number) {
  const list = sortedCommands.value
  const target = idx + dir
  if (target < 0 || target >= list.length) return
  const a = list[idx]
  const b = list[target]
  const tmp = a.sort
  a.sort = b.sort
  b.sort = tmp
  // 本地即时排序反馈
  const arr = list.slice()
  arr.sort((x, y) => x.sort - y.sort)
  sortedCommands.value = arr
  // 持久化两条记录的新 sort
  void persistSort(a)
  void persistSort(b)
}

async function persistSort(cmd: RunCommandInfo) {
  try {
    await apiPut(`/api/commands/${cmd.id}`, { sort: cmd.sort } as RunCommandUpdate)
  } catch (e) {
    ElMessage.error(`排序保存失败：${errorMessage(e)}`)
  }
}

// ---- 删除（先确认后请求，取消时一个请求都不发）----
async function remove(cmd: RunCommandInfo) {
  const result = await runWithConfirm({
    title: '删除命令',
    message: deleteCommandMessage(cmd.name),
    confirm: async (message, title) => {
      try {
        await ElMessageBox.confirm(message, title, {
          type: 'warning',
          confirmButtonText: '确认删除',
          cancelButtonText: '取消',
        })
        return true
      } catch {
        return false
      }
    },
    action: () => apiDelete(`/api/commands/${cmd.id}`),
  })

  if (result.outcome === 'done') {
    sortedCommands.value = sortedCommands.value.filter((c) => c.id !== cmd.id)
    ElMessage.success(`已删除命令「${cmd.name}」`)
    emit('changed')
  } else if (result.outcome === 'failed') {
    ElMessage.error(`删除失败：${result.error}`)
  }
}

// ---- 新增/编辑 表单 ----
const dialogOpen = ref(false)
const editing = ref<RunCommandInfo | null>(null)
const saving = ref(false)
const formError = ref('')
const form = ref<{ name: string; script: string; url: string }>({ name: '', script: '', url: '' })

function resetForm() {
  form.value = { name: '', script: '', url: '' }
  formError.value = ''
}

function openAdd() {
  editing.value = null
  resetForm()
  dialogOpen.value = true
}

function openEdit(cmd: RunCommandInfo) {
  editing.value = cmd
  form.value = { name: cmd.name, script: cmd.script, url: cmd.url ?? '' }
  formError.value = ''
  dialogOpen.value = true
}

async function save() {
  const name = form.value.name
  const script = form.value.script
  if (!name || !script) {
    formError.value = '名称与脚本均不能为空'
    return
  }
  saving.value = true
  formError.value = ''
  try {
    if (editing.value) {
      await apiPut(`/api/commands/${editing.value.id}`, {
        name,
        script,
        url: form.value.url || null,
      } as RunCommandUpdate)
    } else {
      await apiPost(`/api/projects/${props.projectId}/commands`, {
        projectId: props.projectId,
        name,
        script,
        url: form.value.url || null,
      } as RunCommandAdd)
    }
    dialogOpen.value = false
    await load()
    emit('changed')
  } catch (e) {
    formError.value = e instanceof Error ? e.message : String(e)
  } finally {
    saving.value = false
  }
}
</script>

<style scoped>
.clist {
  font-size: 13px;
}

.clist__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 8px;
}

.clist__title {
  font-weight: 600;
  color: var(--el-text-color-primary, #e5eaf3);
}

.clist__add {
  padding: 3px 10px;
  border: 1px solid var(--el-color-primary, #ffb84d);
  border-radius: 6px;
  background: transparent;
  color: var(--el-color-primary, #ffb84d);
  cursor: pointer;
  font-size: 12px;
}

.clist__add:hover {
  background: var(--el-color-primary-light, rgba(255, 184, 77, 0.12));
}

.clist__hint {
  padding: 10px 4px;
  font-size: 12px;
  color: var(--el-text-color-secondary, #a3a6ad);
}

.clist__hint--error {
  color: var(--el-color-danger, #f56c6c);
}

.clist__items {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.clist__item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 10px;
  background: var(--el-fill-color, #262727);
  border: 1px solid var(--el-border-color, #414243);
  border-radius: 6px;
}

.clist__order {
  display: flex;
  flex-direction: column;
  gap: 2px;
  flex-shrink: 0;
}

.clist__order-btn {
  width: 20px;
  height: 16px;
  border: 1px solid var(--el-border-color, #414243);
  border-radius: 4px;
  background: transparent;
  color: var(--el-text-color-secondary, #a3a6ad);
  cursor: pointer;
  font-size: 10px;
  line-height: 1;
}

.clist__order-btn:hover:not(:disabled) {
  border-color: var(--el-color-primary, #ffb84d);
  color: var(--el-color-primary, #ffb84d);
}

.clist__order-btn:disabled {
  opacity: 0.3;
  cursor: not-allowed;
}

.clist__main {
  flex: 1;
  min-width: 0;
}

.clist__row1 {
  display: flex;
  align-items: center;
  gap: 6px;
}

.clist__name {
  font-weight: 600;
  color: var(--el-text-color-primary, #e5eaf3);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.clist__link {
  color: var(--el-color-primary, #ffb84d);
  text-decoration: none;
  font-size: 13px;
}

.clist__link:hover {
  text-decoration: underline;
}

.clist__script {
  display: block;
  margin-top: 2px;
  font-family: var(--el-font-family-mono, monospace);
  font-size: 11px;
  color: var(--el-text-color-secondary, #a3a6ad);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.clist__ops {
  display: flex;
  gap: 4px;
  flex-shrink: 0;
}

.clist__op {
  padding: 3px 8px;
  border: 1px solid var(--el-border-color, #414243);
  border-radius: 5px;
  background: transparent;
  color: var(--el-text-color-regular, #cfd3dc);
  cursor: pointer;
  font-size: 12px;
}

.clist__op:hover {
  border-color: var(--el-color-primary, #ffb84d);
  color: var(--el-color-primary, #ffb84d);
}

.clist__op--run {
  color: var(--el-color-success, #67c23a);
  border-color: var(--el-color-success, #67c23a);
}

.clist__op--run:hover:not(:disabled) {
  background: var(--el-color-success-light, rgba(103, 194, 58, 0.12));
}

.clist__op--run:disabled {
  opacity: 0.5;
  cursor: default;
}

.clist__op--del:hover {
  border-color: var(--el-color-danger, #f56c6c);
  color: var(--el-color-danger, #f56c6c);
}

/* 轻量弹层 */
.cmddlg__mask {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.5);
  z-index: 50;
}

.cmddlg__box {
  position: fixed;
  z-index: 51;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  width: 380px;
  max-width: calc(100vw - 32px);
  padding: 18px 20px;
  background: var(--el-bg-color-overlay, #1d1e1f);
  border: 1px solid var(--el-border-color, #414243);
  border-radius: 10px;
  box-shadow: 0 8px 30px rgba(0, 0, 0, 0.4);
}

.cmddlg__title {
  margin: 0 0 12px;
  font-size: 15px;
  color: var(--el-text-color-primary, #e5eaf3);
}

.cmddlg__field {
  display: flex;
  flex-direction: column;
  gap: 4px;
  margin-bottom: 10px;
}

.cmddlg__field > span {
  font-size: 12px;
  color: var(--el-text-color-secondary, #a3a6ad);
}

.cmddlg__field input {
  padding: 6px 8px;
  border: 1px solid var(--el-border-color, #414243);
  border-radius: 6px;
  background: var(--el-fill-color, #262727);
  color: var(--el-text-color-primary, #e5eaf3);
  font-size: 13px;
}

.cmddlg__field input:focus {
  outline: none;
  border-color: var(--el-color-primary, #ffb84d);
}

.cmddlg__err {
  margin: 0 0 8px;
  font-size: 12px;
  color: var(--el-color-danger, #f56c6c);
}

.cmddlg__foot {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  margin-top: 6px;
}

.cmddlg__btn {
  padding: 6px 14px;
  border: 1px solid var(--el-border-color, #414243);
  border-radius: 6px;
  background: transparent;
  color: var(--el-text-color-regular, #cfd3dc);
  cursor: pointer;
  font-size: 13px;
}

.cmddlg__btn--primary {
  border-color: var(--el-color-primary, #ffb84d);
  color: var(--el-color-primary, #ffb84d);
}

.cmddlg__btn--primary:hover:not(:disabled) {
  background: var(--el-color-primary-light, rgba(255, 184, 77, 0.12));
}

.cmddlg__btn:disabled {
  opacity: 0.5;
  cursor: default;
}
</style>
