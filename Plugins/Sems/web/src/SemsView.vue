<template>
  <div class="sems">
    <header class="sems__head">
      <div class="sems__title-row">
        <h1 class="sems__title">软件工程管理系统</h1>
        <span class="sems__badge">项目工作区</span>
        <span v-if="version" class="sems__version">v{{ version }}</span>
      </div>
      <p class="sems__sub">
        在本页即可管理项目档案：添加项目（选择一个已存在的目录）、编辑、维护运行命令、启停进程。
        同一目录被 AI Agent 选为工作目录时也会自动登记，两者互不覆盖。
      </p>
      <div class="sems__toolbar">
        <button class="sems__btn sems__btn--primary" @click="openPicker">添加项目</button>
        <button class="sems__btn" :disabled="loading" @click="reloadProjects">刷新</button>
      </div>
    </header>

    <section class="sems__stats">
      <div class="sems__stat">
        <span class="sems__stat-num">{{ total }}</span>
        <span class="sems__stat-label">项目总数</span>
      </div>
      <div class="sems__stat">
        <span class="sems__stat-num">{{ totalCommands }}</span>
        <span class="sems__stat-label">运行命令</span>
      </div>
      <div class="sems__stat">
        <span class="sems__stat-num">{{ runningCount }}</span>
        <span class="sems__stat-label">运行中</span>
      </div>
    </section>

    <RunPanel
      ref="runPanelRef"
      :command-urls="commandUrls"
      :launchable-count="launchableCount"
      @run-all="runAll"
    />

    <section class="sems__body">
      <!-- 空态分级（§3.4-4）：加载中 / 加载失败可重试 / 无项目引导 / 有项目。
           加载占位仅在首载（尚无数据）时显示：后台刷新若替换整个网格，ProjectCard 会重挂载、
           展开态丢失（保存命令后卡片自己收起，e2e 实抓），故有数据时静默原地刷新。 -->
      <div v-if="loading && projects.length === 0" class="sems__empty">加载中…</div>
      <div v-else-if="error && projects.length === 0" class="sems__empty sems__empty--error">
        <p>加载失败：{{ error }}</p>
        <button class="sems__btn" @click="reloadProjects">重试</button>
      </div>
      <div v-else-if="projects.length === 0" class="sems__empty">
        <p>还没有项目。</p>
        <p class="sems__empty-hint">
          点上方「添加项目」选择一个已存在的目录即可登记，无需经过其他页面。
        </p>
        <button class="sems__btn sems__btn--primary" @click="openPicker">添加项目</button>
      </div>

      <div v-else class="sems__grid">
        <ProjectCard
          v-for="p in projects"
          :key="p.id"
          :project="p"
          @edit="openEdit"
          @remove="removeProject"
          @commands-changed="reloadProjects"
          @run-command="runOne"
        />
      </div>
    </section>

    <ProjectEditDialog
      v-if="editing"
      :project="editing"
      @close="editing = null"
      @saved="onSaved"
    />

    <DirectoryPickerDialog
      v-if="picking"
      :saving="registering"
      @close="picking = false"
      @pick="onPick"
    />
  </div>
</template>

<script setup lang="ts">
/**
 * sems 首页壳：
 * - 拉 GET /api/projects（含每项目命令概要）装配统计、commandUrls、可启动数
 * - **插件内自洽的项目生命周期**：添加项目（目录浏览/手工路径 → POST /api/projects）、
 *   编辑档案、移除项目（二次确认 → DELETE /api/projects/{id}），不依赖任何其他插件的动作
 * - 运行面板（RunPanel）：启动全部 = 遍历所有项目命令逐个调起
 * - 版本徽标（铁律 13）：GET /api/plugin 解包后按 id=sems 取 version
 * 独立构建的插件界面：原生 HTML + CSS + --el-* 变量；element-plus 经宿主 import map 解析到同一实例。
 */
import { computed, onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import {
  apiGet,
  apiPost,
  fetchPluginVersion,
  registerProject,
  removeProject as removeProjectApi,
} from './http'
import { errorMessage, removeProjectMessage, runWithConfirm } from './confirmOps'
import type { ProjectInfo, ProjectsResp } from './types'
import ProjectCard from './ProjectCard.vue'
import ProjectEditDialog from './ProjectEditDialog.vue'
import DirectoryPickerDialog from './DirectoryPickerDialog.vue'
import RunPanel from './RunPanel.vue'

const projects = ref<ProjectInfo[]>([])
const total = ref(0)
const loading = ref(true)
const error = ref('')
const editing = ref<ProjectInfo | null>(null)
const picking = ref(false)
const registering = ref(false)
const version = ref('')
const runningCount = ref(0)
const runPanelRef = ref<InstanceType<typeof RunPanel> | null>(null)

async function loadProjects() {
  loading.value = true
  error.value = ''
  try {
    const r = await apiGet<ProjectsResp>('/api/projects')
    projects.value = r?.projects ?? []
    total.value = r?.total ?? projects.value.length
  } catch (e) {
    error.value = errorMessage(e)
  } finally {
    loading.value = false
  }
}

onMounted(async () => {
  await loadProjects()
  try {
    version.value = await fetchPluginVersion('sems')
  } catch {
    // 版本徽标缺失不阻断主功能
  }
})

async function reloadProjects() {
  await loadProjects()
  runningCount.value = await (runPanelRef.value?.refresh() ?? Promise.resolve(0))
}

/** 命令总数（统计卡）。 */
const totalCommands = computed(() =>
  projects.value.reduce((sum, p) => sum + (p.commands?.length ?? 0), 0),
)

/** 命令 id → 访问 url 映射（供 RunPanel 快捷访问图标）。 */
const commandUrls = computed<Record<number, string>>(() => {
  const map: Record<number, string> = {}
  for (const p of projects.value) {
    for (const c of p.commands ?? []) {
      if (c.url) map[c.id] = c.url
    }
  }
  return map
})

/** 可启动命令总数（启动全部按钮的可用依据）。 */
const launchableCount = computed(() => totalCommands.value)

function openEdit(p: ProjectInfo) {
  editing.value = p
}

async function onSaved(_projectId: number) {
  editing.value = null
  await reloadProjects()
}

function openPicker() {
  picking.value = true
}

/** 目录选择 → 登记为项目（点即生效，无需再点保存）。 */
async function onPick(payload: { root: string; name?: string }) {
  if (registering.value) return
  registering.value = true
  try {
    const project = await registerProject({ root: payload.root, name: payload.name })
    picking.value = false
    ElMessage.success(`已添加项目「${project.name}」`)
    await reloadProjects()
  } catch (e) {
    // 失败保留弹层并打印原因，用户可改路径重试（§3.4-2 操作成败可见）
    ElMessage.error(`添加项目失败：${errorMessage(e)}`)
  } finally {
    registering.value = false
  }
}

/** 移除项目：先确认、后请求；文案必须写明不动磁盘。 */
async function removeProject(p: ProjectInfo) {
  const result = await runWithConfirm({
    title: '移除项目',
    message: removeProjectMessage(p.name, p.commands?.length ?? 0),
    confirm: async (message, title) => {
      try {
        await ElMessageBox.confirm(message, title, {
          type: 'warning',
          confirmButtonText: '确认移除',
          cancelButtonText: '取消',
        })
        return true
      } catch {
        return false
      }
    },
    action: () => removeProjectApi(p.id),
  })

  if (result.outcome === 'done') {
    ElMessage.success(`已移除项目「${p.name}」的档案（磁盘文件未改动）`)
    await reloadProjects()
  } else if (result.outcome === 'failed') {
    ElMessage.error(`移除失败：${result.error}`)
  }
}

/** 命令启动的 POST 由 CommandList 层发出（含 busy 守卫）；此处只刷新运行面板与统计。
 *  曾在此重复 POST 导致每次启动必 409、且成功刷新路径永不执行（运行列表恒空，e2e 实抓）。 */
async function runOne(_commandId: number) {
  await reloadProjects()
}

/** 启动全部：遍历所有项目命令逐个调起（重复启动由后端拒绝 409，吞掉继续）。 */
async function runAll() {
  const ids: number[] = []
  for (const p of projects.value) {
    for (const c of p.commands ?? []) ids.push(c.id)
  }
  let started = 0
  for (const id of ids) {
    try {
      await apiPost(`/api/commands/${id}/run`, {})
      started++
    } catch {
      // 已运行 / 不存在：忽略，继续下一个
    }
  }
  await reloadProjects()
  ElMessage[started > 0 ? 'success' : 'warning'](`启动完成：新起 ${started} 个，其余已在运行或不可启动`)
}
</script>

<style scoped>
/* 只复用 --el-* CSS 变量并给兜底值；锻造/金属主题由宿主全局变量注入。 */
.sems {
  padding: 24px;
  max-width: 1080px;
  margin: 0 auto;
}

.sems__head {
  border-bottom: 1px solid var(--el-border-color, #414243);
  padding-bottom: 16px;
}

.sems__title-row {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}

.sems__title {
  margin: 0;
  font-size: 22px;
  font-weight: 700;
  color: var(--el-text-color-primary, #e5eaf3);
}

.sems__badge {
  padding: 2px 8px;
  border-radius: 4px;
  font-size: 12px;
  color: var(--el-color-primary, #ffb84d);
  background: var(--el-color-primary-light, rgba(255, 184, 77, 0.12));
}

/* 铁律 13：根视图标题旁展示自身版本号（小字号圆角灰底，不抢标题） */
.sems__version {
  padding: 1px 8px;
  border-radius: 10px;
  font-size: 11px;
  line-height: 16px;
  color: var(--el-text-color-secondary, #a3a6ad);
  background: var(--el-fill-color-light, #262727);
}

.sems__sub {
  margin: 8px 0 0;
  font-size: 13px;
  line-height: 1.6;
  color: var(--el-text-color-secondary, #a3a6ad);
}

.sems__toolbar {
  display: flex;
  gap: 10px;
  margin-top: 12px;
}

.sems__btn {
  height: 30px;
  padding: 0 14px;
  border: 1px solid var(--el-border-color, #414243);
  border-radius: 6px;
  background: transparent;
  color: var(--el-text-color-regular, #cfd3dc);
  font-size: 13px;
  cursor: pointer;
}

.sems__btn:hover:not(:disabled) {
  border-color: var(--el-color-primary, #ffb84d);
  color: var(--el-color-primary, #ffb84d);
}

.sems__btn:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

.sems__btn--primary {
  border-color: var(--el-color-primary, #ffb84d);
  background: var(--el-color-primary, #ffb84d);
  color: var(--el-color-primary-dark, #1d1e1f);
  font-weight: 600;
}

.sems__stats {
  display: flex;
  gap: 12px;
  margin: 20px 0;
}

.sems__stat {
  display: inline-flex;
  flex-direction: column;
  align-items: center;
  gap: 2px;
  min-width: 120px;
  padding: 16px;
  background: var(--el-fill-color, #262727);
  border: 1px solid var(--el-border-color, #414243);
  border-radius: 8px;
}

.sems__stat-num {
  font-size: 30px;
  font-weight: 700;
  color: var(--el-color-primary, #ffb84d);
}

.sems__stat-label {
  font-size: 12px;
  color: var(--el-text-color-secondary, #a3a6ad);
}

.sems__grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 12px;
}

.sems__empty {
  padding: 40px 16px;
  text-align: center;
  font-size: 13px;
  color: var(--el-text-color-secondary, #a3a6ad);
}

.sems__empty p {
  margin: 0 0 10px;
}

.sems__empty-hint {
  font-size: 12px;
}

.sems__empty--error {
  color: var(--el-color-danger, #f56c6c);
}
</style>
