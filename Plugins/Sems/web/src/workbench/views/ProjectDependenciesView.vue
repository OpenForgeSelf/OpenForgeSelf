<script setup lang="ts">
/**
 * S08 依赖（交接包 FR-18：主 / 开发分组 + wanted / latest 双版本）
 * sems 没有包依赖实体；这里把「运行命令」如实映射为项目依赖清单：
 *   wanted = 命令脚本，latest = 命令最近更新时间，分组按 serve/build 等主命令划分。
 * 内嵌原 CommandList 组件保留命令增删改与启动（sems 原有能力）。
 */
import { computed } from 'vue'
import { ic } from '../icons'
import {
  store, currentProject, projectDeps, loadProjects, toast, go,
} from '../workbenchStore'
import CommandList from '../../CommandList.vue'

const p = computed(() => currentProject())
const deps = computed(() => projectDeps(p.value))
const total = computed(() => deps.value.main.length + deps.value.dev.length)

function onInfo(name: string) {
  toast(`${name} 是项目运行命令；可在下方维护脚本与访问地址`)
}
async function onChanged() {
  await loadProjects()
}
function onRun() {
  toast('命令已启动，可在「任务」页查看会话与日志')
  void go('/tasks')
}
</script>

<template>
  <div class="proj-header">
    <div class="proj-lead">
      <span class="wb-eyebrow">{{ p?.name }}</span>
      <h2>运行命令</h2>
    </div>
    <div class="proj-tools">
      <label class="search">
        <span v-html="ic('search', 'sm')"></span>
        <input v-model="store.depFilter" placeholder="搜索命令 / 脚本" aria-label="搜索依赖" />
      </label>
    </div>
  </div>

  <div class="proj-canvas">
    <p class="page-section-title">运行命令清单（{{ total }} 条）</p>
    <p class="wb-notice">
      新增命令会在项目目录执行脚本，请只登记可信命令。
    </p>

    <template v-if="deps.main.length">
      <div class="group-title">主命令（serve / build / start …）</div>
      <div v-for="d in deps.main" :key="d.id" class="list-row">
        <span class="row-icon"><span v-html="ic('box', 'sm')"></span></span>
        <div class="row-main">
          <div class="row-title">{{ d.name }}</div>
          <div class="row-sub">期望 {{ d.wanted }}</div>
        </div>
        <div class="row-meta">
          <span class="badge muted">更新 {{ d.latest }}</span>
          <a v-if="d.url" class="link-btn" :href="d.url" target="_blank" rel="noopener">
            打开 <span v-html="ic('external', 'sm')"></span>
          </a>
          <a class="link-btn" href="#" @click.prevent="onInfo(d.name)">更多信息 <span v-html="ic('external', 'sm')"></span></a>
        </div>
      </div>
    </template>

    <template v-if="deps.dev.length">
      <div class="group-title">辅助命令（lint / test / inspect …）</div>
      <div v-for="d in deps.dev" :key="d.id" class="list-row">
        <span class="row-icon"><span v-html="ic('box', 'sm')"></span></span>
        <div class="row-main">
          <div class="row-title">{{ d.name }}</div>
          <div class="row-sub">期望 {{ d.wanted }}</div>
        </div>
        <div class="row-meta">
          <span class="badge muted">更新 {{ d.latest }}</span>
          <a class="link-btn" href="#" @click.prevent="onInfo(d.name)">更多信息 <span v-html="ic('external', 'sm')"></span></a>
        </div>
      </div>
    </template>

    <div v-if="!total" class="card">没有匹配的命令，可在下方添加</div>

    <div v-if="p" class="wb-embedded">
      <h3>维护运行命令</h3>
      <CommandList :project-id="p.id" :commands="p.commands" @changed="onChanged" @run="onRun" />
    </div>
  </div>
</template>
