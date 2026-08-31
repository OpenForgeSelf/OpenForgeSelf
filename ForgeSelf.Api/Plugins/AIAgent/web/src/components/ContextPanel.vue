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

    <!-- 分组：项目目录（一个目录视为一个项目） -->
    <ContextGroup
      title="项目目录"
      :count="projectDir ? 1 : 0"
      :open="openProject"
      @toggle="openProject = !openProject"
    >
      <!-- 目录输入 + 加载 -->
      <div class="pj">
        <div class="pj__row">
          <input
            class="pj__input"
            v-model="dirInput"
            type="text"
            placeholder="输入工作目录绝对路径"
            spellcheck="false"
            @keydown.enter="applyDirectory"
          />
          <button type="button" class="pj__btn" :disabled="applying" @click="applyDirectory">
            {{ applying ? '…' : '加载' }}
          </button>
        </div>
        <p v-if="projectDir" class="pj__root" :title="projectDir">{{ projectDir }}</p>

        <!-- 文件列表 -->
        <p v-if="errorFiles" class="pj__error">{{ errorFiles }}</p>
        <template v-else>
          <button
            v-if="currentRel"
            type="button"
            class="pj__item pj__item--up"
            @click="goUp"
          >
            ↑ 上级目录
          </button>
          <button
            v-for="f in files"
            :key="f.relativePath"
            type="button"
            class="pj__item"
            :class="{ 'pj__item--dir': f.isDirectory }"
            @click="onOpen(f)"
          >
            <span class="pj__item-name">{{ f.isDirectory ? '📁 ' : '📄 ' }}{{ f.name }}</span>
            <span v-if="!f.isDirectory && f.size > 0" class="pj__item-size">{{ sizeLabel(f.size) }}</span>
          </button>
          <p v-if="!loadingFiles && files.length === 0 && projectDir" class="pj__empty">
            空目录
          </p>
          <p v-if="!projectDir" class="pj__empty">请先加载一个目录作为项目</p>
        </template>
      </div>
    </ContextGroup>

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
      <div
        v-for="s in skills"
        :key="s.id"
        class="ctx__item ctx__item--static"
        :title="s.description"
      >
        <span class="ctx__item-name">{{ s.name }}</span>
        <span v-if="s.source" class="ctx__item-tag" :class="s.source === 'agents' ? '--agents' : '--commands'">{{ s.source }}</span>
      </div>
      <EmptyHint
        v-if="!loadingSkills"
        :text="hasProject ? '未识别到技能（无 .agents/skills 或 .codebuddy/commands）' : '选择工作目录后自动识别项目技能'"
      />
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
import { onMounted, ref, watch } from 'vue'
import { apiGet, withQuery } from '../http'
import type { McpServer, McpTool, ProjectEntry, ProjectSkillItem } from '../types'
import ContextGroup from './ContextGroup.vue'
import EmptyHint from './EmptyHint.vue'

const props = defineProps<{
  /** 当前项目根目录（绝对路径，来自后端选定结果）；未选择时为空串。 */
  projectDir?: string
}>()

const emit = defineEmits<{
  /** 用户点击「加载」：把左栏输入的目录作为工作目录（由父层写后端并持久化）。 */
  (e: 'select-directory', path: string): void
  /** 用户点击一个文件：请求父层读取内容并打开编辑器。 */
  (e: 'open-file', file: { path: string; name: string }): void
}>()

/** 已加载的 MCP 工具（已平铺各服务器）。 */
const tools = ref<McpTool[]>([])
/** 已加载的技能（选择项目目录后为自动识别结果）。 */
const skills = ref<ProjectSkillItem[]>([])
const loadingTools = ref(false)
const loadingSkills = ref(false)
const errorTools = ref('')
const errorSkills = ref('')
/** 当前选中的工具名（仅界面选中态，不改变后端行为）。 */
const selectedTool = ref('')
/** 是否已选定项目目录（决定技能源：项目技能 vs 全局技能）。 */
const hasProject = ref(!!props.projectDir)

// ---- 项目目录面板状态 ----
/** 目录输入框内容。 */
const dirInput = ref(props.projectDir ?? '')
/** 是否正在提交目录。 */
const applying = ref(false)
/** 当前浏览的子目录相对路径（空 = 项目根）。 */
const currentRel = ref('')
/** 项目根下当前目录的文件/子目录列表。 */
const files = ref<ProjectEntry[]>([])
const loadingFiles = ref(false)
const errorFiles = ref('')

// 各分组的展开状态
const openProject = ref(true)
const openTools = ref(true)
const openSkills = ref(true)
const openPrompts = ref(true)
const openMemory = ref(true)

// 父层 projectDir 变化（成功加载目录）时同步输入框并回到根浏览
watch(
  () => props.projectDir,
  (v) => {
    if (v !== undefined && v !== null) {
      dirInput.value = v
      currentRel.value = ''
      reloadFiles('')
    }
  }
)

/** 用户提交目录给父层。 */
function applyDirectory() {
  const p = dirInput.value.trim()
  if (!p) return
  emit('select-directory', p)
}

/** 加载目录下文件列表。 */
async function reloadFiles(rel: string) {
  if (!props.projectDir) return
  loadingFiles.value = true
  errorFiles.value = ''
  try {
    const data = await apiGet<{ files?: ProjectEntry[] }>(
      withQuery('/api/project/files', { path: rel || undefined })
    )
    files.value = data?.files ?? []
  } catch (e) {
    errorFiles.value = e instanceof Error ? e.message : String(e)
    files.value = []
  } finally {
    loadingFiles.value = false
  }
}

/** 进入子目录。 */
async function goInto(rel: string) {
  currentRel.value = rel
  await reloadFiles(rel)
}

/** 返回上级目录。 */
async function goUp() {
  const parts = currentRel.value.split('/').filter(Boolean)
  parts.pop()
  const parent = parts.join('/')
  currentRel.value = parent
  await reloadFiles(parent)
}

/** 点击列表项：目录进入，文件请求打开。 */
function onOpen(f: ProjectEntry) {
  const rel = f.relativePath ?? ''
  if (f.isDirectory) {
    void goInto(rel)
  } else {
    emit('open-file', { path: rel, name: f.name ?? rel })
  }
}

/** 格式化文件大小。 */
function sizeLabel(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`
}

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

/** 加载技能列表：已选项目目录时为该项目自动识别的技能；否则走全局技能（未选的兜底）。 */
async function loadSkills() {
  loadingSkills.value = true
  errorSkills.value = ''
  try {
    const src = hasProject.value ? '/api/project/skills' : '/api/skills'
    skills.value = (await apiGet<ProjectSkillItem[]>(src)) ?? []
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

// projectDir 变化（成功加载目录）时刷新项目技能识别
watch(
  () => props.projectDir,
  (v) => {
    hasProject.value = !!v
    void loadSkills()
  }
)
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
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 6px;
}

.ctx__item-name {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.ctx__item-tag {
  flex-shrink: 0;
  padding: 0 4px;
  border-radius: 3px;
  font-size: var(--el-font-size-extra-small, 10px);
  line-height: 14px;
}

.ctx__item-tag.--agents {
  color: var(--el-color-primary, #ffb84d);
  background: var(--el-color-primary-light, rgba(255, 184, 77, 0.12));
}

.ctx__item-tag.--commands {
  color: var(--el-color-info, #909399);
  background: var(--el-fill-color, #262727);
}

/* ---- 项目目录面板 ---- */
.pj {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.pj__row {
  display: flex;
  gap: 4px;
}

.pj__input {
  flex: 1;
  min-width: 0;
  padding: 3px 6px;
  font-size: var(--el-font-size-extra-small, 11px);
  color: var(--el-text-color-primary, #e5eaf3);
  background: var(--el-fill-color, #262727);
  border: 1px solid var(--el-border-color, #414243);
  border-radius: 4px;
  outline: none;
}

.pj__input:focus {
  border-color: var(--el-color-primary, #ffb84d);
}

.pj__btn {
  flex-shrink: 0;
  padding: 3px 8px;
  font-size: var(--el-font-size-extra-small, 11px);
  color: var(--el-color-primary, #ffb84d);
  background: var(--el-color-primary-light, rgba(255, 184, 77, 0.12));
  border: 1px solid var(--el-color-primary, #ffb84d);
  border-radius: 4px;
  cursor: pointer;
}

.pj__btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.pj__root {
  margin: 0;
  padding: 2px 4px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-family: var(--el-font-family-mono, monospace);
  font-size: var(--el-font-size-extra-small, 11px);
  color: var(--el-text-color-secondary, #a3a6ad);
}

.pj__error {
  margin: 0;
  font-size: var(--el-font-size-extra-small, 11px);
  color: var(--el-color-danger, #f56c6c);
}

.pj__item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 4px;
  width: 100%;
  text-align: left;
  padding: 2px 4px;
  font-size: var(--el-font-size-extra-small, 12px);
  color: var(--el-text-color-regular, #cfd3dc);
  background: transparent;
  border: none;
  border-radius: 4px;
  cursor: pointer;
}

.pj__item:hover {
  background: var(--el-fill-color, #262727);
}

.pj__item--up {
  color: var(--el-color-primary, #ffb84d);
  font-family: var(--el-font-family, inherit);
}

.pj__item-name {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.pj__item--dir .pj__item-name {
  color: var(--el-color-primary, #ffb84d);
}

.pj__item-size {
  flex-shrink: 0;
  font-size: var(--el-font-size-extra-small, 10px);
  color: var(--el-text-color-secondary, #a3a6ad);
}

.pj__empty {
  margin: 0;
  font-size: var(--el-font-size-extra-small, 11px);
  color: var(--el-text-color-secondary, #a3a6ad);
}
</style>
