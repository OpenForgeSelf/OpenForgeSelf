<template>
  <div class="sems">
    <header class="sems__head">
      <div class="sems__title-row">
        <h1 class="sems__title">软件工程管理系统</h1>
        <span class="sems__badge">项目工作区</span>
      </div>
      <p class="sems__sub">
        会话每选定一个工作目录即登记为一个项目。选择目录请前往「AI Agent」页。
      </p>
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
    </section>

    <RunPanel
      ref="runPanelRef"
      :command-urls="commandUrls"
      :launchable-count="launchableCount"
      @run-all="runAll"
    />

    <section class="sems__body">
      <div v-if="loading" class="sems__empty">加载中…</div>
      <div v-else-if="error" class="sems__empty sems__empty--error">加载失败：{{ error }}</div>
      <div v-else-if="projects.length === 0" class="sems__empty">
        暂无项目。请在「AI Agent」页选择工作目录，选择后会自动登记为项目并显示在下方。
      </div>

      <div v-else class="sems__grid">
        <ProjectCard
          v-for="p in projects"
          :key="p.id"
          :project="p"
          @edit="openEdit"
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
  </div>
</template>

<script setup lang="ts">
/**
 * sems 首页壳（spec028 §6 / T07+T08）：
 * - 拉 GET /api/projects（后端已带每项目 commands 概要）
 * - 装配 commandUrls（命令 id → url）与 launchableCount（可启动命令总数）
 * - 编辑弹层（ProjectEditDialog）
 * - 运行面板（RunPanel）：启动全部 = 遍历所有项目命令逐个启动
 * 独立构建的插件界面，仅用原生 HTML + CSS，复用 --el-* 变量。
 */
import { computed, onMounted, ref } from 'vue'
import { apiGet, apiPost } from './http'
import type { ProjectInfo, ProjectsResp } from './types'
import ProjectCard from './ProjectCard.vue'
import ProjectEditDialog from './ProjectEditDialog.vue'
import RunPanel from './RunPanel.vue'

const projects = ref<ProjectInfo[]>([])
const total = ref(0)
const loading = ref(true)
const error = ref('')
const editing = ref<ProjectInfo | null>(null)
const runPanelRef = ref<InstanceType<typeof RunPanel> | null>(null)

async function loadProjects() {
  loading.value = true
  error.value = ''
  try {
    const r = await apiGet<ProjectsResp>('/api/projects')
    projects.value = r?.projects ?? []
    total.value = r?.total ?? projects.value.length
  } catch (e) {
    error.value = e instanceof Error ? e.message : String(e)
  } finally {
    loading.value = false
  }
}

onMounted(loadProjects)

async function reloadProjects() {
  await loadProjects()
  await runPanelRef.value?.refresh()
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

/** 单条命令启动（来自卡片）。 */
async function runOne(commandId: number) {
  try {
    await apiPost(`/api/commands/${commandId}/run`, {})
    await runPanelRef.value?.refresh()
  } catch (e) {
    window.alert(`启动失败：${e instanceof Error ? e.message : String(e)}`)
  }
}

/** 启动全部：遍历所有项目命令逐个调起（重复启动由后端拒绝 409，吞掉）。 */
async function runAll() {
  const ids: number[] = []
  for (const p of projects.value) {
    for (const c of p.commands ?? []) ids.push(c.id)
  }
  for (const id of ids) {
    try {
      await apiPost(`/api/commands/${id}/run`, {})
    } catch {
      // 已运行 / 不存在：忽略，继续下一个
    }
  }
  await runPanelRef.value?.refresh()
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

.sems__sub {
  margin: 8px 0 0;
  font-size: 13px;
  color: var(--el-text-color-secondary, #a3a6ad);
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

.sems__empty--error {
  color: var(--el-color-danger, #f56c6c);
}
</style>
