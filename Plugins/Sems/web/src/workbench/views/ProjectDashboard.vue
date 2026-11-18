<script setup lang="ts">
/**
 * S06 项目仪表盘（交接包 FR-16）：4 类 widget + 项目档案卡。
 * 数值全部来自真实项目档案 / 运行命令 / 运行会话，不再用静态示例数字。
 */
import { computed, ref } from 'vue'
import { ic } from '../icons'
import {
  store, currentProject, go, projectDeps, projectPorts, refreshRunningCount,
  loadProjects, toProjectInfo,
} from '../workbenchStore'
import ProjectEditDialog from '../../ProjectEditDialog.vue'

// Sems 已注册的 sems_* 工具数（与后端 SemsPlugin.cs 工具注册表一致）
const TOOL_COUNT = 22

const p = computed(() => currentProject())
const projectInfo = computed(() => toProjectInfo(p.value))
const editing = ref(false)
const deps = computed(() => projectDeps(p.value))
const ports = computed(() => projectPorts(p.value))
const urls = computed(() => (p.value?.commands ?? []).filter((c) => c.url).length)
const recent = computed(() => (p.value?.commands ?? []).slice(0, 5))

function portActive(c: { name?: string }): boolean {
  return ports.value.some((pt) => pt.commandName === c.name && pt.running)
}

async function onSaved() {
  editing.value = false
  await loadProjects()
}
</script>

<template>
  <template v-if="p">
    <div class="proj-header">
      <div class="proj-lead">
        <h2>仪表盘</h2>
        <div class="es-sub">{{ p.name }} · {{ p.type || '未分类' }}</div>
      </div>
      <div class="proj-tools">
        <span class="wb-mode" :class="{ demo: store.mode === 'demo' }">{{ p.source }}</span>
        <button class="btn btn-secondary" @click="refreshRunningCount()">
          <span v-html="ic('refresh', 'sm')"></span>刷新会话
        </button>
        <button class="btn btn-primary" @click="editing = true">
          <span v-html="ic('pencil', 'sm')"></span>编辑档案
        </button>
      </div>
    </div>

    <div class="proj-canvas">
      <!-- 统计磁贴 -->
      <div class="stat-row">
        <div class="stat">
          <div class="stat-head">
            <span class="row-icon"><span v-html="ic('terminal', 'sm')"></span></span>
            <span class="stat-k">运行中会话</span>
          </div>
          <div class="stat-v">{{ store.runningCount }}</div>
          <div class="stat-d">{{ urls }} 条命令带访问地址</div>
        </div>
        <div class="stat">
          <div class="stat-head">
            <span class="row-icon"><span v-html="ic('tasks', 'sm')"></span></span>
            <span class="stat-k">已登记命令</span>
          </div>
          <div class="stat-v">{{ (p.commands ?? []).length }}</div>
          <div class="stat-d">来自项目命令清单</div>
        </div>
        <div class="stat">
          <div class="stat-head">
            <span class="row-icon"><span v-html="ic('box', 'sm')"></span></span>
            <span class="stat-k">依赖项</span>
          </div>
          <div class="stat-v">{{ deps.main.length + deps.dev.length }}</div>
          <div class="stat-d">主命令 {{ deps.main.length }} · 辅助 {{ deps.dev.length }}</div>
        </div>
        <div class="stat">
          <div class="stat-head">
            <span class="row-icon"><span v-html="ic('puzzle', 'sm')"></span></span>
            <span class="stat-k">可用工具</span>
          </div>
          <div class="stat-v">{{ TOOL_COUNT }}</div>
          <div class="stat-d">Sems 已注册能力</div>
        </div>
      </div>

      <!-- 双栏：最近命令 / 快速入口 -->
      <div class="dash-grid">
        <section class="card">
          <h3>最近命令</h3>
          <div v-if="!recent.length" class="empty-state" style="padding:24px 0">
            <span v-html="ic('tasks', 'lg')"></span>
            <div class="es-sub">尚未登记任何运行命令</div>
          </div>
          <div v-for="c in recent" :key="c.name" class="list-row">
            <span class="row-icon"><span v-html="ic('play', 'sm')"></span></span>
            <div class="row-main">
              <div class="row-title">{{ c.name }}</div>
              <div class="row-sub">{{ c.url || c.script || '无访问地址' }}</div>
            </div>
            <span class="badge" :class="portActive(c) ? 'warn' : 'muted'">
              {{ portActive(c) ? '占用中' : '空闲' }}
            </span>
          </div>
        </section>

        <section class="card">
          <h3>快速入口</h3>
          <a class="quick-row" href="#/tasks" @click.prevent="go('/tasks')">
            <span class="row-icon"><span v-html="ic('terminal', 'sm')"></span></span>
            <span class="row-main">
              <span class="row-title">登记命令</span>
              <span class="row-sub">在项目下维护可运行的命令脚本</span>
            </span>
            <span v-html="ic('external', 'sm')"></span>
          </a>
          <a class="quick-row" href="#/specs" @click.prevent="go('/specs')">
            <span class="row-icon"><span v-html="ic('clip', 'sm')"></span></span>
            <span class="row-main">
              <span class="row-title">查看规格</span>
              <span class="row-sub">规格版本、关联与验收覆盖</span>
            </span>
            <span v-html="ic('external', 'sm')"></span>
          </a>
          <a class="quick-row" href="#/plugins" @click.prevent="go('/plugins')">
            <span class="row-icon"><span v-html="ic('puzzle', 'sm')"></span></span>
            <span class="row-main">
              <span class="row-title">工具清单</span>
              <span class="row-sub">Sems 已注册的 {{ TOOL_COUNT }} 个能力工具</span>
            </span>
            <span v-html="ic('external', 'sm')"></span>
          </a>
          <a class="quick-row" href="#/configuration" @click.prevent="go('/configuration')">
            <span class="row-icon"><span v-html="ic('sliders', 'sm')"></span></span>
            <span class="row-main">
              <span class="row-title">项目设置</span>
              <span class="row-sub">显示选项与项目档案</span>
            </span>
            <span v-html="ic('external', 'sm')"></span>
          </a>
          <div class="wb-note">
            <span v-html="ic('info', 'sm')"></span>
            <span>本页只展示 Sems 真实能力；不提供端口管理等遗留概念。</span>
          </div>
        </section>
      </div>

      <!-- 项目档案（sems 原有字段） -->
      <div class="card" style="margin-top:16px;max-width:760px">
        <h3>项目档案（sems 原有字段）</h3>
        <dl class="wb-kv" style="margin-top:12px">
          <dt>根目录</dt><dd>{{ p.path }}</dd>
          <dt>类型 / 标签</dt><dd>{{ p.type || '未分类' }} / {{ p.tags || '无' }}</dd>
          <dt>描述</dt><dd>{{ p.description || '—' }}</dd>
          <dt>来源</dt><dd>{{ p.source }}</dd>
          <dt>目录状态</dt><dd>{{ p.pathExists ? '可达' : '不可达' }} · {{ p.isGitRepo ? 'git 仓库' : '非 git 仓库' }}</dd>
          <dt>最近活跃</dt><dd>{{ p.openDate || '—' }}</dd>
        </dl>
        <div class="wb-toolbar" style="margin-top:16px">
          <a class="link-btn" href="#/specs" @click.prevent="go('/specs')">
            规格与验收覆盖 <span v-html="ic('external', 'sm')"></span>
          </a>
          <a class="link-btn" href="#/configuration" @click.prevent="go('/configuration')">
            项目配置 <span v-html="ic('external', 'sm')"></span>
          </a>
        </div>
      </div>
    </div>

    <ProjectEditDialog v-if="editing && projectInfo" :project="projectInfo" @close="editing = false" @saved="onSaved" />
  </template>
</template>
