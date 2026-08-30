<template>
  <aside class="ctx">
    <!-- 面板头 -->
    <div class="ctx__head">
      <span class="ctx__title">AI 上下文</span>
      <svg class="ctx__icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        <path d="M20 7h-9M14 17H5" />
        <circle cx="17" cy="17" r="3" />
        <circle cx="7" cy="7" r="3" />
      </svg>
    </div>

    <!-- 分组：MCP 工具 -->
    <ContextGroup
      title="MCP 工具"
      :count="tools.length"
      :loading="loadingTools"
      :error="errorTools"
      :open="openTools"
      @toggle="openTools = !openTools"
    >
      <button
        v-for="t in tools"
        :key="t.name"
        type="button"
        class="ctx__item"
        :class="{ 'ctx__item--active': selectedTool === t.name }"
        @click="selectedTool = selectedTool === t.name ? '' : (t.name ?? '')"
      >
        <span class="ctx__item-name">{{ t.name }}</span>
      </button>
      <EmptyHint v-if="!loadingTools && tools.length === 0" text="未启用 MCP 服务器" />
    </ContextGroup>

    <!-- 分组：技能 -->
    <ContextGroup
      title="技能"
      :count="skills.length"
      :loading="loadingSkills"
      :error="errorSkills"
      :open="openSkills"
      @toggle="openSkills = !openSkills"
    >
      <div v-for="s in skills" :key="s.id" class="ctx__item ctx__item--static">
        <span class="ctx__item-name">{{ s.name }}</span>
      </div>
      <EmptyHint v-if="!loadingSkills && skills.length === 0" text="暂无技能" />
    </ContextGroup>

    <!-- 分组：提示指令 -->
    <ContextGroup title="提示指令" :count="0" :open="openPrompts" @toggle="openPrompts = !openPrompts">
      <!-- 后端暂无「提示指令」统一接口，此处如实留空，不编造数据 -->
      <EmptyHint text="后端暂无接口（待补）" />
    </ContextGroup>

    <!-- 分组：记忆 -->
    <ContextGroup title="记忆" :count="0" :open="openMemory" @toggle="openMemory = !openMemory">
      <!-- 后端暂无「记忆」统一接口，此处如实留空，不编造数据 -->
      <EmptyHint text="后端暂无接口（待补）" />
    </ContextGroup>
  </aside>
</template>

<script setup lang="ts">
/**
 * 左栏「AI 上下文」面板（对应设计原型 220px 侧栏）。
 *
 * 数据来源：MCP 工具与技能走真实接口；提示指令与记忆因后端暂无统一接口，
 * 按约定如实留空（不造假数据）。
 */
import { onMounted, ref } from 'vue'
import { apiGet } from '../http'
import type { McpServer, McpTool, SkillItem } from '../types'
import ContextGroup from './ContextGroup.vue'
import EmptyHint from './EmptyHint.vue'

/** 已加载的 MCP 工具（已平铺各服务器）。 */
const tools = ref<McpTool[]>([])
/** 已加载的技能。 */
const skills = ref<SkillItem[]>([])
const loadingTools = ref(false)
const loadingSkills = ref(false)
const errorTools = ref('')
const errorSkills = ref('')
/** 当前选中的工具名（仅界面选中态，不改变后端行为）。 */
const selectedTool = ref('')

// 各分组的展开状态
const openTools = ref(true)
const openSkills = ref(true)
const openPrompts = ref(true)
const openMemory = ref(true)

/** 加载 MCP 工具：先取服务器列表，再并行取各服务器的工具并平铺。 */
async function loadTools() {
  loadingTools.value = true
  errorTools.value = ''
  try {
    const servers = await apiGet<McpServer[]>('/api/mcp/servers')
    const list = servers ?? []
    const results = await Promise.all(
      list
        .filter((s) => s?.id)
        .map(async (s) => {
          try {
            const items = await apiGet<McpTool[]>(`/api/mcp/servers/${s.id}/tools`)
            return (items ?? []).map((t) => ({ ...t, serverName: s.name }))
          } catch {
            // 单个服务器取工具失败不应拖垮整栏，跳过即可
            return [] as McpTool[]
          }
        })
    )
    tools.value = results.flat()
  } catch (e) {
    errorTools.value = e instanceof Error ? e.message : String(e)
    tools.value = []
  } finally {
    loadingTools.value = false
  }
}

/** 加载技能列表。 */
async function loadSkills() {
  loadingSkills.value = true
  errorSkills.value = ''
  try {
    skills.value = (await apiGet<SkillItem[]>('/api/skills')) ?? []
  } catch (e) {
    errorSkills.value = e instanceof Error ? e.message : String(e)
    skills.value = []
  } finally {
    loadingSkills.value = false
  }
}

onMounted(() => {
  void loadTools()
  void loadSkills()
})
</script>

<style scoped>
/* 左栏固定 220px，与设计原型一致；颜色一律走 --el-* 变量并给出兜底值。 */
.ctx {
  width: 220px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  overflow-y: auto;
  background: var(--el-bg-color, #1d1e1f);
  border-right: 1px solid var(--el-border-color, #414243);
}

.ctx__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 12px;
  border-bottom: 1px solid var(--el-border-color, #414243);
}

.ctx__title {
  font-size: var(--el-font-size-small, 13px);
  font-weight: var(--el-weight-semibold, 600);
  color: var(--el-text-color-primary, #e5eaf3);
}

.ctx__icon {
  width: 14px;
  height: 14px;
  color: var(--el-text-color-secondary, #a3a6ad);
}

/* 列表项：选中态仿设计原型（左侧 2px 主色边框 + 主色淡底） */
.ctx__item {
  display: block;
  width: 100%;
  text-align: left;
  padding: 4px 8px;
  margin: 0;
  font-size: var(--el-font-size-extra-small, 12px);
  color: var(--el-text-color-regular, #cfd3dc);
  background: transparent;
  border: none;
  border-left: 2px solid transparent;
  border-radius: 0 4px 4px 0;
  cursor: pointer;
  font-family: var(--el-font-family-mono, monospace);
  transition: background 0.15s ease;
}

.ctx__item:hover {
  background: var(--el-fill-color, #262727);
}

.ctx__item--active {
  border-left-color: var(--el-color-primary, #ffb84d);
  background: var(--el-color-primary-light, rgba(255, 184, 77, 0.12));
  color: var(--el-color-primary, #ffb84d);
}

.ctx__item--static {
  cursor: default;
  font-family: var(--el-font-family, inherit);
}
</style>
