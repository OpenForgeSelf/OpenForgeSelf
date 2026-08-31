<template>
  <div class="sems">
    <header class="sems__head">
      <div class="sems__title-row">
        <h1 class="sems__title">软件工程管理系统</h1>
        <span class="sems__badge">项目清单</span>
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
    </section>

    <section class="sems__body">
      <div v-if="loading" class="sems__empty">加载中…</div>
      <div v-else-if="error" class="sems__empty sems__empty--error">加载失败：{{ error }}</div>
      <div v-else-if="projects.length === 0" class="sems__empty">
        暂无项目。请在「AI Agent」页选择工作目录，选择后会自动登记为项目并显示在下方。
      </div>

      <div v-else class="sems__grid">
        <div
          v-for="p in projects"
          :key="p.root"
          class="sems__card"
          :class="{ 'sems__card--dead': p.pathExists === false }"
        >
          <div class="sems__card-top">
            <span class="sems__card-name">{{ p.name }}</span>
            <span class="sems__card-tags">
              <span v-if="p.isGitRepo" class="sems__tag">git</span>
              <span v-if="!p.pathExists" class="sems__tag sems__tag--warn">不可达</span>
            </span>
          </div>
          <p class="sems__card-root" :title="p.root">{{ p.root }}</p>
          <footer class="sems__card-foot">
            <span>最近活动：{{ fmt(p.lastActivityAt) }}</span>
            <span v-if="p.source" class="sems__card-source">来源 {{ p.source }}</span>
          </footer>
        </div>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
/**
 * sems 首页：展示已登记的项目列表（来自 GET /api/projects）。
 * 数据源为宿主共享项目清单（AIAgent 选定工作目录时写入），本页只读取展示。
 * 独立构建的插件界面，仅用原生 HTML + CSS（不用 <ElXxx>），复用 --el-* 变量保持视觉一致。
 */
import { onMounted, ref } from 'vue'
import { apiGet } from './http'
import type { ProjectRecord } from './types'

const projects = ref<ProjectRecord[]>([])
const total = ref(0)
const loading = ref(true)
const error = ref('')

async function load() {
  loading.value = true
  error.value = ''
  try {
    const r = await apiGet<{ total: number; projects: ProjectRecord[] }>('/api/projects')
    projects.value = r?.projects ?? []
    total.value = r?.total ?? projects.value.length
  } catch (e) {
    error.value = e instanceof Error ? e.message : String(e)
  } finally {
    loading.value = false
  }
}

function fmt(v?: string): string {
  if (!v) return '—'
  const d = new Date(v)
  if (Number.isNaN(d.getTime())) return '—'
  const p = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}`
}

onMounted(load)
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

.sems__card {
  padding: 14px 16px;
  background: var(--el-bg-color, #1d1e1f);
  border: 1px solid var(--el-border-color, #414243);
  border-radius: 8px;
  transition: border-color 0.15s ease;
}

.sems__card:hover {
  border-color: var(--el-color-primary, #ffb84d);
}

.sems__card--dead {
  opacity: 0.55;
}

.sems__card-top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}

.sems__card-name {
  font-size: 15px;
  font-weight: 600;
  color: var(--el-text-color-primary, #e5eaf3);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.sems__card-tags {
  display: flex;
  gap: 4px;
  flex-shrink: 0;
}

.sems__tag {
  padding: 0 6px;
  border-radius: 3px;
  font-size: 11px;
  line-height: 16px;
  color: var(--el-color-success, #67c23a);
  background: var(--el-color-success-light, rgba(103, 194, 58, 0.12));
}

.sems__tag--warn {
  color: var(--el-color-warning, #e6a23c);
  background: var(--el-color-warning-light, rgba(230, 162, 60, 0.14));
}

.sems__card-root {
  margin: 8px 0 0;
  font-family: var(--el-font-family-mono, monospace);
  font-size: 12px;
  color: var(--el-text-color-secondary, #a3a6ad);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.sems__card-foot {
  display: flex;
  justify-content: space-between;
  gap: 8px;
  margin-top: 10px;
  font-size: 12px;
  color: var(--el-text-color-secondary, #a3a6ad);
}

.sems__card-source {
  flex-shrink: 0;
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