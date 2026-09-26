<script setup lang="ts">
/**
 * Agent 卡片：展示单个已登记 agent 的健康灯、能力矩阵（F1-F6）、交互口与操作按钮。
 *
 * 能力矩阵的渲染铁律：**每格要么 ✅ 要么显式 ❌**——不支持的格子必须显示为 ❌，
 * 而不是留白。「不显示」会让用户误以为支持但没数据。
 */

import type { AgentDto } from './http'

const props = defineProps<{
  agent: AgentDto
  probing: boolean
}>()

const emit = defineEmits<{
  edit: []
  remove: []
  probe: []
  trust: []
  revoke: []
}>()

/** 能力面顺序与中文短名（与后端 AgentFacets 保持一致，改动须双端同步）。 */
const FACETS = [
  { key: 'F1_Driving', label: '委派执行' },
  { key: 'F2_Sessions', label: '会话管理' },
  { key: 'F3_Transcripts', label: '历史读取' },
  { key: 'F4_Configure', label: '配置管理' },
  { key: 'F5_HealthUsage', label: '健康用量' },
  { key: 'F6_Policy', label: '权限策略' },
] as const

function supports(facet: string): boolean {
  return props.agent.capabilities?.facets?.[facet] === true
}

function facetNote(facet: string): string {
  return props.agent.capabilities?.notes?.[facet] ?? ''
}

/** 健康灯：Missing > Degraded > Unknown > Ok（取各交互口最差者，后端已算好）。 */
function healthClass(): string {
  switch (props.agent.health) {
    case 'Ok':
      return 'ok-health--ok'
    case 'Degraded':
      return 'ok-health--warn'
    case 'Missing':
      return 'ok-health--bad'
    default:
      return 'ok-health--unknown'
  }
}

function healthLabel(): string {
  switch (props.agent.health) {
    case 'Ok':
      return '可用'
    case 'Degraded':
      return '降级'
    case 'Missing':
      return '不可用'
    default:
      return '未探测'
  }
}

function apHealthLabel(h?: string): string {
  switch (h) {
    case 'Ok':
      return '可用'
    case 'Degraded':
      return '降级'
    case 'Missing':
      return '缺失'
    default:
      return '未探测'
  }
}
</script>

<template>
  <article class="ok-card">
    <div class="ok-card__head">
      <div class="ok-card__ident">
        <span class="ok-health" :class="healthClass()" :title="healthLabel()"></span>
        <span class="ok-card__name">{{ agent.name }}</span>
        <span v-if="agent.vendor" class="ok-code">{{ agent.vendor }}</span>
        <span class="ok-badge" :class="agent.enabled ? 'ok-badge--ok' : 'ok-badge--idle'">
          {{ agent.enabled ? '已启用' : '已停用' }}
        </span>
        <span class="ok-badge" :class="agent.trusted ? 'ok-badge--wait' : 'ok-badge--idle'">
          {{ agent.trusted ? '已授信' : '未授信' }}
        </span>
        <span class="ok-health__text">{{ healthLabel() }}</span>
      </div>

      <div class="ok-card__actions">
        <button class="ok-btn ok-btn--sm" :disabled="probing" @click="emit('probe')">
          {{ probing ? '探测中…' : '探测' }}
        </button>
        <button v-if="!agent.trusted" class="ok-btn ok-btn--sm" @click="emit('trust')">授信</button>
        <button v-else class="ok-btn ok-btn--sm" @click="emit('revoke')">取消授信</button>
        <button class="ok-btn ok-btn--sm" @click="emit('edit')">编辑</button>
        <button class="ok-btn ok-btn--sm ok-btn--danger" @click="emit('remove')">删除</button>
      </div>
    </div>

    <div v-if="agent.tags?.length" class="ok-card__tags">
      <span v-for="t in agent.tags" :key="t" class="ok-tag">{{ t }}</span>
    </div>

    <!-- 能力矩阵：六格全列，不支持显式打叉 -->
    <div class="ok-matrix">
      <div
        v-for="f in FACETS"
        :key="f.key"
        class="ok-matrix__cell"
        :class="supports(f.key) ? 'ok-matrix__cell--yes' : 'ok-matrix__cell--no'"
        :title="facetNote(f.key) || (supports(f.key) ? '支持' : '不支持')"
      >
        <span class="ok-matrix__mark">{{ supports(f.key) ? '✓' : '✕' }}</span>
        <span class="ok-matrix__label">{{ f.label }}</span>
      </div>
    </div>

    <!-- 交互口 -->
    <div v-if="agent.accessPoints?.length" class="ok-aps">
      <div class="ok-aps__title">交互口（{{ agent.accessPoints.length }}）</div>
      <div class="ok-ap" v-for="ap in agent.accessPoints" :key="ap.id">
        <span class="ok-ap__mode">{{ ap.mode === 'Sessionful' ? '会话型' : '一次性' }}</span>
        <span class="ok-code">{{ ap.transport }}</span>
        <code class="ok-ap__exe">{{ ap.executable || '—' }}</code>
        <span class="ok-ap__health" :class="ap.health === 'Ok' ? 'ok-ap__health--ok' : 'ok-ap__health--bad'">
          {{ apHealthLabel(ap.health) }}
        </span>
        <span v-if="ap.lastVersion" class="ok-hint">{{ ap.lastVersion }}</span>
        <span v-if="ap.isDefault" class="ok-badge ok-badge--idle">默认</span>
      </div>
    </div>

    <div v-if="agent.trusted && agent.trustedScopes?.length" class="ok-card__trusted">
      授信范围：<code class="ok-code">{{ agent.trustedScopes.join(', ') }}</code>
    </div>

    <div v-if="agent.defaultCwd || agent.notes" class="ok-card__notes">
      <span v-if="agent.defaultCwd">默认目录：<code class="ok-code">{{ agent.defaultCwd }}</code></span>
      <span v-if="agent.notes" class="ok-card__note-text">{{ agent.notes }}</span>
    </div>
  </article>
</template>

<style>
.ok-card {
  background: var(--el-bg-color, #fff);
  border: 1px solid var(--el-border-color-lighter, #ebeef5);
  border-radius: var(--el-border-radius-base, 6px);
  padding: 14px 16px;
  margin-bottom: 12px;
}
.ok-card__head {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 12px;
  flex-wrap: wrap;
}
.ok-card__ident {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}
.ok-card__name {
  font-size: 15px;
  font-weight: 600;
}
.ok-card__actions {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
}
.ok-health {
  width: 9px;
  height: 9px;
  border-radius: 50%;
  flex: none;
  display: inline-block;
}
.ok-health--ok {
  background: var(--el-color-success, #67c23a);
}
.ok-health--warn {
  background: var(--el-color-warning, #e6a23c);
}
.ok-health--bad {
  background: var(--el-color-danger, #f56c6c);
}
.ok-health--unknown {
  background: var(--el-text-color-placeholder, #a8abb2);
}
.ok-health__text {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}
.ok-card__tags {
  display: flex;
  gap: 6px;
  margin-top: 8px;
  flex-wrap: wrap;
}
.ok-tag {
  font-size: 11px;
  padding: 1px 8px;
  border-radius: 10px;
  background: var(--el-fill-color-light, #f4f4f5);
  color: var(--el-text-color-regular);
}
.ok-matrix {
  display: grid;
  grid-template-columns: repeat(6, minmax(0, 1fr));
  gap: 6px;
  margin-top: 12px;
}
@media (max-width: 720px) {
  .ok-matrix {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }
}
.ok-matrix__cell {
  display: flex;
  align-items: center;
  gap: 5px;
  padding: 5px 8px;
  border-radius: var(--el-border-radius-base, 6px);
  font-size: 12px;
  border: 1px solid transparent;
}
.ok-matrix__cell--yes {
  background: var(--el-color-success-light-9, #e1f3d8);
  color: var(--el-color-success, #67c23a);
}
.ok-matrix__cell--no {
  background: var(--el-fill-color-light, #f4f4f5);
  color: var(--el-text-color-placeholder, #a8abb2);
}
.ok-matrix__mark {
  font-weight: 700;
}
.ok-aps {
  margin-top: 12px;
  padding-top: 10px;
  border-top: 1px solid var(--el-border-color-lighter, #ebeef5);
}
.ok-aps__title {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  margin-bottom: 6px;
}
.ok-ap {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12px;
  padding: 3px 0;
  flex-wrap: wrap;
}
.ok-ap__mode {
  color: var(--el-text-color-regular);
}
.ok-ap__exe {
  font-family: var(--el-font-family-mono, monospace);
  font-size: 12px;
  color: var(--el-text-color-primary);
}
.ok-ap__health--ok {
  color: var(--el-color-success, #67c23a);
}
.ok-ap__health--bad {
  color: var(--el-color-danger, #f56c6c);
}
.ok-card__trusted {
  margin-top: 10px;
  font-size: 12px;
  color: var(--el-color-warning, #e6a23c);
}
.ok-card__notes {
  margin-top: 8px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
  display: flex;
  flex-direction: column;
  gap: 4px;
  line-height: 1.6;
}
</style>
