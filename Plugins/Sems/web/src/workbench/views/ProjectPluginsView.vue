<script setup lang="ts">
/**
 * S07 插件（交接包 FR-17）
 * 交接包里这一屏是「项目已装插件」；sems 没有插件实体，但插件自身对外注册了 22 个
 * sems_* 工具（SemsPlugin.RegisterToolExtensions）。因此这里如实展示这份能力清单，
 * 版本取 /api/plugin 的 sems 版本，不编造「可安装插件市场」。
 */
import { computed } from 'vue'
import { ic } from '../icons'
import { store, projectPlugins, toast, currentProject } from '../workbenchStore'

const list = computed(() => projectPlugins())
const p = computed(() => currentProject())
const version = computed(() => store.version || '1.3.0')

function onInfo(name: string) {
  toast(`${name} 经 mcp-center 的 list_tools / universal_tool 对外调用`)
}
</script>

<template>
  <div class="proj-header">
    <h2>Sems 工具（{{ list.length }} / 22）</h2>
    <div class="proj-tools">
      <label class="search">
        <span v-html="ic('search', 'sm')"></span>
        <input v-model="store.pluginFilter" placeholder="搜索能力 / 分组" aria-label="搜索插件" />
      </label>
      <button class="btn-icon" aria-label="更多" @click="onInfo('sems_*')">
        <span v-html="ic('more')"></span>
      </button>
    </div>
  </div>

  <div class="proj-canvas">
    <p class="page-section-title">已注册能力（{{ list.length }} / 22 个 sems_* 工具）</p>
    <p class="wb-notice">
      <strong>不是插件市场：</strong>本屏展示 Sems 插件已注册的 22 个 <code>sems_*</code> 工具
      （来自 SemsPlugin.cs 工具注册表，与 mcp-center 的 list_tools 一致）。
      Sems <strong>不提供插件安装 / 下载 / 依赖解析</strong>；新增工具需在后端登记并随插件发版。
    </p>

    <div v-if="!list.length" class="card">没有匹配的能力</div>
    <div v-for="t in list" :key="t.name" class="list-row wb-plugin-row">
      <span class="row-icon"><span v-html="ic('puzzle', 'sm')"></span></span>
      <div class="row-main">
        <div class="row-title">{{ t.name }}</div>
        <div class="row-sub">{{ t.desc }}</div>
      </div>
      <div class="row-meta">
        <span class="badge muted">分组 {{ t.group }}</span>
        <span class="badge muted">插件 v{{ version }}</span>
        <a class="link-btn" href="#" @click.prevent="onInfo(t.name)">更多信息 <span v-html="ic('external', 'sm')"></span></a>
      </div>
    </div>
  </div>
</template>
