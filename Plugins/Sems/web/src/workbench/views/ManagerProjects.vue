<script setup lang="ts">
/**
 * S01 项目列表 / S02 空状态（交接包 features.csv 前 8 行 + FR-01~FR-08）
 * 数据源换成真实项目档案：GET /api/projects（WbProject）。
 * 保留 sems 原有能力：档案字段（类型 / 标签 / git / 目录可达）、重命名写回、移除二次确认。
 */
import { computed, ref } from 'vue'
import { ElMessageBox } from 'element-plus'
import { ic } from '../icons'
import {
  store, listProjects, projectTypes, projectLastRun, openProject, setFavorite, renameProject, removeProject,
  loadProjects, toast, go, projectLayout, setProjectLayout,
  refreshRunningCount, projectHasRunningSessions, runningSessionCountOf,
} from '../workbenchStore'
import { removeProjectMessage, removeBlockedByRunningMessage } from '../../confirmOps'

const rows = computed(() => listProjects())
const types = computed(() => projectTypes())
const busyRemoveId = ref<number | null>(null)

async function guideToStop(p: (typeof rows.value)[number], runningCount = 0) {
  try {
    await ElMessageBox.confirm(
      removeBlockedByRunningMessage(p.name, runningCount),
      '无法移除',
      { type: 'warning', confirmButtonText: '去运行页停止', cancelButtonText: '取消' },
    )
    openProject(p.id, '/runs')
  } catch {
    /* 用户取消，留在当前页 */
  }
}

async function onRemove(p: (typeof rows.value)[number]) {
  if (busyRemoveId.value) return
  await refreshRunningCount()
  if (projectHasRunningSessions(p)) {
    await guideToStop(p, runningSessionCountOf(p))
    return
  }
  try {
    await ElMessageBox.confirm(
      removeProjectMessage(p.name, p.commands?.length ?? 0),
      '移除项目',
      { type: 'warning', confirmButtonText: '确认移除', cancelButtonText: '取消' },
    )
  } catch {
    return
  }
  busyRemoveId.value = p.id
  const result = await removeProject(p.id)
  busyRemoveId.value = null
  if (result.ok) {
    toast(`已移除项目「${p.name}」的档案（磁盘文件未改动）`)
    return
  }
  if (result.blockedByRunning) {
    await guideToStop(p)
    return
  }
  toast('移除失败：' + result.error)
}

function onRename(p: (typeof rows.value)[number]) {
  store.renameId = p.id
}

async function saveRename(p: (typeof rows.value)[number], e: Event) {
  // Enter 会紧接着触发 blur；用 renameId 守卫，避免重复 PUT。
  if (store.renameId !== p.id) return
  store.renameId = null
  const value = (e.target as HTMLInputElement).value
  const ok = await renameProject(p.id, value)
  toast(ok ? '项目名称已更新' : '项目名称未更新')
}

function cancelRename() {
  store.renameId = null
}

function onEditor(p: (typeof rows.value)[number]) {
  toast(`「${p.name}」位于 ${p.path}；插件视图不调用外部编辑器`)
}

function lastRunText(p: (typeof rows.value)[number]): string {
  const h = projectLastRun(p)
  if (!h) return '尚未运行过命令'
  const label = h.status === 'running' ? '运行中' : h.status === 'done' ? '已完成' : '空闲'
  return `最近运行 ${h.name} · ${label}`
}
</script>

<template>
  <div class="manager-body">
    <div class="manager-inner">
      <!-- 三段式 ①：页头（标题 + 说明 + 双按钮） -->
      <div class="manager-head">
        <div>
          <h2>项目</h2>
          <p>登记已有目录以管理其命令与规格；登记不会创建或修改任何文件。</p>
        </div>
        <div class="proj-tools">
          <span class="wb-badge" :class="{ info: store.mode === 'live' }">
            共 {{ store.projects.length }} 个 · 真实档案
          </span>
          <button class="btn btn-secondary" :disabled="store.loading" @click="loadProjects()">
            <span v-html="ic('refresh', 'sm')"></span>刷新
          </button>
          <button class="btn btn-secondary" @click="go('/import')">
            <span v-html="ic('download', 'sm')"></span>导入目录
          </button>
          <button class="btn btn-primary" @click="go('/create')">
            <span v-html="ic('plus', 'sm')"></span>登记项目
          </button>
        </div>
      </div>

      <!-- 三段式 ②：搜索 + 类型筛选 + 排序 + 展示模式 -->
      <div v-if="store.projects.length" class="wb-toolbar wb-filter-bar" style="margin-bottom:16px">
        <div class="search-wrap" style="margin:0;flex:1;min-width:200px">
          <label class="search">
            <span v-html="ic('search', 'sm')"></span>
            <input v-model="store.filter" placeholder="搜索项目（名称 / 路径 / 类型 / 标签）" aria-label="搜索项目" />
          </label>
        </div>
        <div class="wb-chips">
          <button class="chip" :class="{ on: !store.typeFilter }"
                  :aria-pressed="!store.typeFilter" @click="store.typeFilter = ''">
            全部 {{ store.projects.length }}
          </button>
          <button v-for="t in types" :key="t" class="chip" :class="{ on: store.typeFilter === t }"
                  :aria-pressed="store.typeFilter === t" @click="store.typeFilter = t">
            {{ t }} {{ store.projects.filter((p) => (p.type || '未分类') === t).length }}
          </button>
        </div>
        <label class="wb-select">
          排序
          <select v-model="store.projectSort" aria-label="排序方式">
            <option value="recent">最近活跃</option>
            <option value="name">名称</option>
            <option value="commands">命令数</option>
          </select>
        </label>
        <div class="layout-toggle" role="group" aria-label="展示模式">
          <button class="btn-icon" :class="{ on: projectLayout.value === 'list' }"
                  aria-label="列表模式" :aria-pressed="projectLayout.value === 'list'"
                  @click="setProjectLayout('list')">
            <span v-html="ic('list', 'sm')"></span>
          </button>
          <button class="btn-icon" :class="{ on: projectLayout.value === 'card' }"
                  aria-label="卡片模式" :aria-pressed="projectLayout.value === 'card'"
                  @click="setProjectLayout('card')">
            <span v-html="ic('grid', 'sm')"></span>
          </button>
        </div>
      </div>

      <!-- 五种状态：加载中 / 出错可重试 / 无项目引导 / 无匹配 / 正常 -->
      <div v-if="store.loading && !store.projects.length" class="empty-state">
        <span class="wb-spin"></span><div>正在加载项目档案…</div>
      </div>
      <div v-else-if="store.mode === 'demo' && store.error && !store.projects.length" class="empty-state">
        <span v-html="ic('info', 'lg')"></span><div>加载失败：{{ store.error }}</div>
        <button class="btn btn-primary" @click="loadProjects()">重试</button>
      </div>
      <div v-else-if="!store.projects.length" class="empty-state">
        <span v-html="ic('clip', 'lg')"></span>
        <div class="es-quote">还没有登记任何项目</div>
        <div class="es-sub">登记已有目录，即可在此统一管理它的运行命令、规格与工程插件。</div>
        <div class="wb-toolbar" style="justify-content:center;margin-top:12px">
          <button class="btn btn-primary" @click="go('/create')">登记项目</button>
          <button class="btn btn-secondary" @click="go('/import')">导入目录</button>
        </div>
      </div>
      <div v-else-if="!rows.length" class="empty-state">
        <span v-html="ic('search', 'lg')"></span>
        <div class="es-quote">没有匹配的项目</div>
        <div class="es-sub">试试其他关键词，或清空搜索与筛选条件。</div>
      </div>

      <!-- 列表模式 -->
      <div v-else-if="projectLayout.value === 'list'" class="project-list">
        <div v-for="p in rows" :key="p.id" class="project-row" :class="{ current: p.id === store.currentProjectId }">
          <button class="star-btn" :class="{ on: p.favorite }" aria-label="收藏"
                  :aria-pressed="p.favorite ? 'true' : 'false'" @click.stop="setFavorite(p.id)">
            <span v-html="ic('star', p.favorite ? '' : 'sm')"></span>
          </button>

          <div class="project-info" @click="openProject(p.id)">
            <input v-if="store.renameId === p.id" class="project-name-input" :value="p.name"
                   aria-label="项目名称" maxlength="80" autofocus
                   @click.stop @keydown.enter.prevent="saveRename(p, $event)"
                   @keydown.esc.prevent="cancelRename" @blur="saveRename(p, $event)" />
            <span v-else class="project-name">{{ p.name }}</span>
            <span class="project-path">{{ p.path }}</span>
          </div>

          <div class="project-actions">
            <span class="wb-badge info">{{ p.type || '未分类' }}</span>
            <span v-if="p.isGitRepo" class="wb-badge">git</span>
            <span v-if="!p.pathExists" class="wb-badge warn">目录不可达</span>
            <span class="wb-badge muted">{{ lastRunText(p) }}</span>
            <button class="editor-btn" @click.stop="onEditor(p)">
              <span v-html="ic('external', 'sm')"></span>在编辑器中打开
            </button>
            <button class="btn-icon" :aria-label="store.renameId === p.id ? '保存重命名' : '重命名'"
                    @click.stop="onRename(p)">
              <span v-html="ic(store.renameId === p.id ? 'check' : 'pencil', 'sm')"></span>
            </button>
            <button class="btn-icon danger" aria-label="移除" :disabled="busyRemoveId === p.id"
                    @click.stop="onRemove(p)">
              <span v-html="ic('close', 'sm')"></span>
            </button>
          </div>
        </div>
      </div>

      <!-- 卡片模式 -->
      <div v-else class="project-cards">
        <div v-for="p in rows" :key="p.id" class="project-card" :class="{ current: p.id === store.currentProjectId }">
          <div class="project-card-head">
            <button class="star-btn" :class="{ on: p.favorite }" aria-label="收藏"
                    :aria-pressed="p.favorite ? 'true' : 'false'" @click.stop="setFavorite(p.id)">
              <span v-html="ic('star', p.favorite ? '' : 'sm')"></span>
            </button>
            <input v-if="store.renameId === p.id" class="project-name-input" :value="p.name"
                   aria-label="项目名称" maxlength="80" autofocus
                   @click.stop @keydown.enter.prevent="saveRename(p, $event)"
                   @keydown.esc.prevent="cancelRename" @blur="saveRename(p, $event)" />
            <span v-else class="project-card-title" @click="openProject(p.id)">{{ p.name }}</span>
            <span class="wb-badge info">{{ p.type || '未分类' }}</span>
          </div>

          <div class="project-card-path" @click="openProject(p.id)">{{ p.path }}</div>

          <div class="project-card-meta">
            <span class="wb-badge">{{ p.commands?.length ?? 0 }} 个运行命令</span>
            <span v-if="p.isGitRepo" class="wb-badge">git</span>
            <span v-if="!p.pathExists" class="wb-badge warn">目录不可达</span>
            <span class="wb-badge muted">{{ lastRunText(p) }}</span>
          </div>

          <div class="project-card-actions">
            <button class="btn btn-primary btn-sm" @click.stop="openProject(p.id)">
              <span v-html="ic('chevron', 'sm')"></span>进入项目
            </button>
            <button class="editor-btn" @click.stop="onEditor(p)">
              <span v-html="ic('external', 'sm')"></span>编辑器
            </button>
            <button class="btn-icon" :aria-label="store.renameId === p.id ? '保存重命名' : '重命名'"
                    @click.stop="onRename(p)">
              <span v-html="ic(store.renameId === p.id ? 'check' : 'pencil', 'sm')"></span>
            </button>
            <button class="btn-icon danger" aria-label="移除" :disabled="busyRemoveId === p.id"
                    @click.stop="onRemove(p)">
              <span v-html="ic('close', 'sm')"></span>
            </button>
          </div>
        </div>
      </div>

      <p class="wb-hint" style="margin-top:16px">
        「登记 / 导入」登记的是本机已存在的目录，不会创建脚手架或写入文件；
        移除只删除档案，不动磁盘。
      </p>
    </div>
  </div>
</template>
