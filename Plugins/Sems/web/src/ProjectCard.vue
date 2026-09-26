<template>
  <div class="pcard" :class="{ 'pcard--dead': !project.pathExists }">
    <div class="pcard__top">
      <div class="pcard__title-wrap">
        <span class="pcard__name" :title="project.name">{{ project.name }}</span>
        <span class="pcard__type" :style="typeStyle">{{ typeLabel }}</span>
      </div>
      <div class="pcard__actions">
        <button class="pcard__icon-btn" title="编辑档案" @click="$emit('edit', project)">✎</button>
        <button class="pcard__icon-btn" :title="expanded ? '收起命令' : '管理命令'" @click="expanded = !expanded">
          {{ expanded ? '▴' : '▾' }}
        </button>
      </div>
    </div>

    <p v-if="project.description" class="pcard__desc" :title="project.description">{{ project.description }}</p>

    <div class="pcard__tags">
      <span v-if="project.isGitRepo" class="pcard__tag">git</span>
      <span v-if="!project.pathExists" class="pcard__tag pcard__tag--warn">不可达</span>
      <span v-for="t in tags" :key="t" class="pcard__chip">{{ t }}</span>
    </div>

    <p class="pcard__root" :title="project.root">{{ project.root }}</p>

    <footer class="pcard__foot">
      <span>最近活动：{{ fmt(project.lastActiveAt) }}</span>
      <span v-if="project.source" class="pcard__source">来源 {{ project.source }}</span>
    </footer>

    <div v-if="expanded" class="pcard__cmds">
      <CommandList
        :project-id="project.id"
        :commands="project.commands"
        @changed="$emit('commands-changed', project.id)"
        @run="$emit('run-command', $event)"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * 项目卡片（spec028 §6 / T07）：
 * - 类型徽标（不同色，按预设映射或自定义）
 * - 标签 chip 渲染（多标签）
 * - 描述摘要（过长省略号，title 看全文）
 * - 路径 / git / 不可达 徽标
 * - 命令管理区（展开后内嵌 CommandList）
 */
import { computed, ref } from 'vue'
import type { ProjectInfo } from './types'
import CommandList from './CommandList.vue'

const props = defineProps<{ project: ProjectInfo }>()

const expanded = ref(false)

const emit = defineEmits<{
  (e: 'edit', project: ProjectInfo): void
  (e: 'commands-changed', projectId: number): void
  (e: 'run-command', commandId: number): void
}>()

/** 标签文本拆分（逗号分隔，去空）。 */
const tags = computed(() => {
  const raw = props.project.tags ?? ''
  return raw
    .split(',')
    .map((s) => s.trim())
    .filter(Boolean)
})

/** 类型展示文案：自定义类型直接显示原文。 */
const typeLabel = computed(() => {
  const t = (props.project.type ?? '').trim()
  if (!t) return '未分类'
  const map: Record<string, string> = {
    frontend: '前端',
    backend: '后端',
    fullstack: '全栈',
    library: '库',
    tool: '工具',
    other: '其他',
  }
  return map[t] ?? t
})

/** 类型徽标配色：预设映射固定色，自定义走中性色。 */
const typeStyle = computed(() => {
  const t = (props.project.type ?? '').trim()
  const colors: Record<string, [string, string]> = {
    frontend: ['#60a5fa', 'rgba(96,165,250,0.14)'],
    backend: ['#34d399', 'rgba(52,211,153,0.14)'],
    fullstack: ['#a78bfa', 'rgba(167,139,250,0.14)'],
    library: ['#f472b6', 'rgba(244,114,182,0.14)'],
    tool: ['#fbbf24', 'rgba(251,191,36,0.14)'],
    other: ['#94a3b8', 'rgba(148,163,184,0.14)'],
  }
  if (t && colors[t]) {
    const [fg, bg] = colors[t]
    return { color: fg, background: bg }
  }
  return { color: 'var(--el-text-color-secondary, #a3a6ad)', background: 'var(--el-fill-color, #262727)' }
})

function fmt(v?: string): string {
  if (!v) return '—'
  const d = new Date(v)
  if (Number.isNaN(d.getTime())) return '—'
  const p = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}`
}
</script>

<style scoped>
.pcard {
  padding: 14px 16px;
  background: var(--el-bg-color, #1d1e1f);
  border: 1px solid var(--el-border-color, #414243);
  border-radius: 8px;
  transition: border-color 0.15s ease;
}

.pcard:hover {
  border-color: var(--el-color-primary, #ffb84d);
}

.pcard--dead {
  opacity: 0.55;
}

.pcard__top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}

.pcard__title-wrap {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}

.pcard__name {
  font-size: 15px;
  font-weight: 600;
  color: var(--el-text-color-primary, #e5eaf3);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.pcard__type {
  flex-shrink: 0;
  padding: 1px 8px;
  border-radius: 4px;
  font-size: 12px;
  font-weight: 600;
  line-height: 18px;
}

.pcard__actions {
  display: flex;
  gap: 4px;
  flex-shrink: 0;
}

.pcard__icon-btn {
  width: 26px;
  height: 26px;
  border: 1px solid var(--el-border-color, #414243);
  border-radius: 6px;
  background: transparent;
  color: var(--el-text-color-secondary, #a3a6ad);
  cursor: pointer;
  font-size: 13px;
  line-height: 1;
  transition: all 0.15s ease;
}

.pcard__icon-btn:hover {
  border-color: var(--el-color-primary, #ffb84d);
  color: var(--el-color-primary, #ffb84d);
}

.pcard__desc {
  margin: 8px 0 0;
  font-size: 12px;
  line-height: 1.5;
  color: var(--el-text-color-regular, #cfd3dc);
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.pcard__tags {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  margin-top: 8px;
}

.pcard__tag {
  padding: 0 6px;
  border-radius: 3px;
  font-size: 11px;
  line-height: 16px;
  color: var(--el-color-success, #67c23a);
  background: var(--el-color-success-light, rgba(103, 194, 58, 0.12));
}

.pcard__tag--warn {
  color: var(--el-color-warning, #e6a23c);
  background: var(--el-color-warning-light, rgba(230, 162, 60, 0.14));
}

.pcard__chip {
  padding: 0 6px;
  border-radius: 10px;
  font-size: 11px;
  line-height: 16px;
  color: var(--el-text-color-secondary, #a3a6ad);
  background: var(--el-fill-color, #262727);
}

.pcard__root {
  margin: 8px 0 0;
  font-family: var(--el-font-family-mono, monospace);
  font-size: 12px;
  color: var(--el-text-color-secondary, #a3a6ad);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.pcard__foot {
  display: flex;
  justify-content: space-between;
  gap: 8px;
  margin-top: 10px;
  font-size: 12px;
  color: var(--el-text-color-secondary, #a3a6ad);
}

.pcard__source {
  flex-shrink: 0;
}

.pcard__cmds {
  margin-top: 12px;
  padding-top: 12px;
  border-top: 1px dashed var(--el-border-color, #414243);
}
</style>
