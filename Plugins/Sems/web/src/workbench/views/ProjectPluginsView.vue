<script setup lang="ts">
/**
 * 「插件」页：只读展示当前项目已声明的工程插件（需求 F19–F22）。
 * 数据来自 GET /api/projects/{id}/engineering-plugins，不得回退为 sems_* / MCP 工具清单。
 */
import { computed, onMounted, watch } from 'vue'
import { ic } from '../icons'
import {
  store, currentProject, visibleEngineeringPlugins, loadEngineeringPlugins,
} from '../workbenchStore'

const p = computed(() => currentProject())
const list = computed(() => visibleEngineeringPlugins())
const empty = computed(() => !store.engineeringPluginsLoading && !store.engineeringPluginsError && store.engineeringPlugins.length === 0)
const noMatch = computed(() => !store.engineeringPluginsLoading && !empty.value && !store.engineeringPluginsError && list.value.length === 0)

onMounted(() => { if (p.value) void loadEngineeringPlugins(p.value.id) })
watch(() => p.value?.id, (id) => { if (id != null) void loadEngineeringPlugins(id) })
</script>

<template>
  <div class="proj-header">
    <div class="proj-lead">
      <span class="wb-eyebrow">{{ p?.name }}</span>
      <h2>插件</h2>
    </div>
    <div class="proj-tools">
      <label class="search">
        <span v-html="ic('search', 'sm')"></span>
        <input v-model="store.pluginFilter" placeholder="搜索名称或分组" aria-label="搜索插件" />
      </label>
    </div>
  </div>

  <div class="proj-canvas">
    <p class="page-section-title">本项目工程插件（{{ store.engineeringPlugins.length }}）</p>
    <p class="wb-notice">只读查看当前项目根目录 <code>package.json</code> 已声明的构建、转译、代码质量或框架插件。本页不展示能力中心工具，也不安装、升级或卸载任何包。</p>

    <div v-if="store.engineeringPluginsLoading" class="card">正在识别本项目插件…</div>
    <div v-else-if="store.engineeringPluginsError" class="card">
      <p>无法读取项目工程插件：{{ store.engineeringPluginsError }}</p>
      <button type="button" class="btn btn-secondary" @click="p && loadEngineeringPlugins(p.id)">重试</button>
    </div>
    <div v-else-if="empty" class="empty-state">
      <span v-html="ic('puzzle', 'lg')"></span>
      <div>未发现本项目声明的工程插件</div>
      <div class="es-sub">本页不展示能力中心工具。没有 package.json，或其中未声明可识别的前端工程插件时，会显示此说明。</div>
    </div>
    <div v-else-if="noMatch" class="empty-state">
      <span v-html="ic('search', 'lg')"></span>
      <div>没有匹配的插件</div>
      <div class="es-sub">试试其他名称或分组。</div>
    </div>
    <div v-else>
      <div v-for="t in list" :key="t.name" class="list-row wb-plugin-row">
        <span class="row-icon"><span v-html="ic('puzzle', 'sm')"></span></span>
        <div class="row-main">
          <div class="row-title">{{ t.name }}</div>
          <div class="row-sub">声明版本 {{ t.version || '—' }} · 来源 {{ t.source }}</div>
        </div>
        <div class="row-meta">
          <span class="badge muted">{{ t.group }}</span>
        </div>
      </div>
    </div>
  </div>
</template>
