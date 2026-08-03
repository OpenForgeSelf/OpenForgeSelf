<script setup lang="ts">
import { ref, computed } from 'vue'

interface FeatureItem {
  id: string
  name: string
  icon: string
  category: string
  categoryLabel: string
  color: string
  bgColor: string
  description: string
  stats: string
  extraInfo?: string
  enabled: boolean
}

const categories = [
  { key: 'all', label: '全部' },
  { key: 'tools', label: '工具类' },
  { key: 'ai', label: 'AI类' },
  { key: 'system', label: '系统类' },
  { key: 'orchestration', label: '编排类' },
  { key: 'dev', label: '开发类' },
]

const activeCategory = ref('all')
const searchQuery = ref('')

const features: FeatureItem[] = [
  {
    id: 'ai-agent',
    name: 'AI Agent',
    icon: 'bot',
    category: 'ai',
    categoryLabel: 'AI',
    color: 'var(--el-color-primary)',
    bgColor: 'var(--el-color-primary-light-9)',
    description: 'AI代理核心，自然语言操控工具，自动规划执行复杂任务链',
    stats: '50+ 工具函数',
    extraInfo: '支持多模型',
    enabled: true,
  },
  {
    id: 'quick-links',
    name: '快捷链接',
    icon: 'link',
    category: 'tools',
    categoryLabel: '工具',
    color: 'var(--el-color-info)',
    bgColor: 'rgba(29, 78, 216, 0.12)',
    description: '一键打开常用网址，支持分组管理',
    stats: '12 个链接',
    enabled: true,
  },
  {
    id: 'text-tools',
    name: '文本工具',
    icon: 'type',
    category: 'tools',
    categoryLabel: '工具',
    color: 'var(--el-color-info)',
    bgColor: 'rgba(29, 78, 216, 0.12)',
    description: '格式化、编码转换、哈希计算等文本处理工具集',
    stats: '8 个工具',
    enabled: true,
  },
  {
    id: 'file-tools',
    name: '文件工具',
    icon: 'folder-open',
    category: 'tools',
    categoryLabel: '工具',
    color: 'var(--el-color-info)',
    bgColor: 'rgba(29, 78, 216, 0.12)',
    description: '批量重命名、清理、压缩解压等文件管理工具',
    stats: '5 个工具',
    enabled: true,
  },
  {
    id: 'system-monitor',
    name: '系统监控',
    icon: 'monitor',
    category: 'system',
    categoryLabel: '系统',
    color: 'var(--el-color-success)',
    bgColor: 'rgba(4, 120, 87, 0.12)',
    description: 'CPU、内存、磁盘实时监控，进程管理',
    stats: '5 个监控项',
    enabled: true,
  },
  {
    id: 'workflow',
    name: '工作流引擎',
    icon: 'git-branch',
    category: 'orchestration',
    categoryLabel: '编排',
    color: '#A78BFA',
    bgColor: 'rgba(167, 139, 250, 0.12)',
    description: '多工具编排，任务链自动执行，支持条件分支',
    stats: '12 个工作流',
    enabled: true,
  },
  {
    id: 'scheduler',
    name: '定时任务',
    icon: 'clock',
    category: 'orchestration',
    categoryLabel: '编排',
    color: '#2DD4BF',
    bgColor: 'rgba(45, 212, 191, 0.12)',
    description: 'Cron表达式管理，定时执行脚本和工作流',
    stats: '5 个任务',
    enabled: true,
  },
  {
    id: 'script-runner',
    name: '脚本运行器',
    icon: 'terminal',
    category: 'dev',
    categoryLabel: '开发',
    color: '#2DD4BF',
    bgColor: 'rgba(45, 212, 191, 0.12)',
    description: 'PowerShell/Python/Node脚本运行，支持AI生成',
    stats: '支持 3 种语言',
    enabled: true,
  },
  {
    id: 'dev-tools',
    name: '开发者工具箱',
    icon: 'wrench',
    category: 'dev',
    categoryLabel: '开发',
    color: '#2DD4BF',
    bgColor: 'rgba(45, 212, 191, 0.12)',
    description: 'JSON/YAML/Base64/哈希/正则/时间戳等25+开发工具',
    stats: '25+ 工具',
    enabled: true,
  },
]

const filteredFeatures = computed(() => {
  return features.filter((f) => {
    const matchesCategory = activeCategory.value === 'all' || f.category === activeCategory.value
    const q = searchQuery.value.trim().toLowerCase()
    const matchesSearch =
      !q ||
      f.name.toLowerCase().includes(q) ||
      f.description.toLowerCase().includes(q) ||
      f.categoryLabel.toLowerCase().includes(q)
    return matchesCategory && matchesSearch
  })
})

function setCategory(key: string): void {
  activeCategory.value = key
}

function lucideIconSvg(name: string): string {
  const icons: Record<string, string> = {
    bot: '<svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="11" width="18" height="10" rx="2"/><circle cx="12" cy="5" r="2"/><path d="M12 7v4"/><line x1="8" y1="16" x2="8" y2="16"/><line x1="16" y1="16" x2="16" y2="16"/></svg>',
    link: '<svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71"/><path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71"/></svg>',
    type: '<svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="4 7 4 4 20 4 20 7"/><line x1="9" y1="20" x2="15" y2="20"/><line x1="12" y1="4" x2="12" y2="20"/></svg>',
    'folder-open': '<svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M2 6a2 2 0 0 1 2-2h5l2 2h9a2 2 0 0 1 2 2v0"/><path d="M2 6v12a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V9a2 2 0 0 0-2-2H4a2 2 0 0 1-2-2z"/></svg>',
    monitor: '<svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="2" y="3" width="20" height="14" rx="2"/><line x1="8" y1="21" x2="16" y2="21"/><line x1="12" y1="17" x2="12" y2="21"/></svg>',
    'git-branch': '<svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="6" y1="3" x2="6" y2="15"/><circle cx="18" cy="6" r="3"/><circle cx="6" cy="18" r="3"/><path d="M18 9a9 9 0 0 1-9 9"/></svg>',
    clock: '<svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>',
    terminal: '<svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="4 17 10 11 4 5"/><line x1="12" y1="19" x2="20" y2="19"/></svg>',
    wrench: '<svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z"/></svg>',
  }
  return icons[name] || ''
}
</script>

<template>
  <div class="all-features-view">
    <!-- Blueprint grid background -->
    <div class="bg-grid">
      <!-- Page Header -->
      <section class="page-header">
        <h1 class="page-title">所有功能</h1>
        <p class="page-subtitle">9 个插件 · 50+ 工具函数 · 持续扩展中</p>

        <!-- Search -->
        <div class="search-box">
          <svg
            width="16"
            height="16"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
            class="search-icon"
            aria-hidden="true"
          >
            <circle cx="11" cy="11" r="8" />
            <line x1="21" y1="21" x2="16.65" y2="16.65" />
          </svg>
          <input
            v-model="searchQuery"
            type="text"
            placeholder="搜索插件或工具..."
            class="search-input"
            aria-label="搜索插件或工具"
          />
        </div>

        <!-- Category filter pills -->
        <div class="filter-bar">
          <button
            v-for="cat in categories"
            :key="cat.key"
            class="filter-pill"
            :class="{ 'filter-pill--active': activeCategory === cat.key }"
            @click="setCategory(cat.key)"
          >
            {{ cat.label }}
          </button>
        </div>
      </section>

      <!-- Feature Grid -->
      <section class="grid-section">
        <div v-if="filteredFeatures.length === 0" class="empty-state">
          <p>没有找到匹配的功能</p>
        </div>
        <div v-else class="feature-grid">
          <article
            v-for="item in filteredFeatures"
            :key="item.id"
            class="feature-card"
          >
            <!-- Status dot -->
            <div
              class="status-dot"
              :style="{ background: item.color, boxShadow: `0 0 8px ${item.color}` }"
            />
            <!-- Accent bar -->
            <div class="accent-bar" :style="{ background: item.color }" />

            <div class="card-body">
              <!-- Icon + Name row -->
              <div class="card-header-row">
                <div
                  class="card-icon-wrap"
                  :style="{ background: item.bgColor }"
                >
                  <span
                    class="card-icon"
                    :style="{ color: item.color }"
                    v-html="lucideIconSvg(item.icon)"
                  />
                </div>
                <div class="card-title-group">
                  <h3 class="card-title">{{ item.name }}</h3>
                  <span
                    class="card-badge"
                    :style="{
                      color: item.color,
                      background: item.bgColor,
                    }"
                  >
                    {{ item.categoryLabel }}
                  </span>
                </div>
              </div>

              <!-- Description -->
              <p class="card-desc">{{ item.description }}</p>

              <!-- Spacer -->
              <div class="card-spacer" />

              <!-- Stats -->
              <div class="card-stats">
                <span class="stat-item stat-mono">{{ item.stats }}</span>
                <span v-if="item.extraInfo" class="stat-item">{{ item.extraInfo }}</span>
              </div>

              <!-- Status + Actions -->
              <div class="card-footer">
                <div class="status-indicator">
                  <div class="status-dot-sm" :style="{ background: 'var(--el-color-success)' }" />
                  <span class="status-text" :style="{ color: 'var(--el-color-success)' }">已启用</span>
                </div>
                <div class="card-actions">
                  <button class="btn btn--primary">打开</button>
                  <button class="btn btn--ghost">配置</button>
                </div>
              </div>
            </div>
          </article>
        </div>
      </section>

      <!-- Bottom CTA -->
      <section class="cta-section">
        <div class="cta-card">
          <div class="cta-content">
            <div class="cta-icon-wrap">
              <svg
                width="18"
                height="18"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="2"
                stroke-linecap="round"
                stroke-linejoin="round"
                style="color: var(--el-color-primary);"
              >
                <path d="M12 3v12" />
                <path d="M8 8l4 4 4-4" />
                <path d="M8 16h8" />
                <path d="M4 20h16" />
              </svg>
            </div>
            <div class="cta-text">
              <p class="cta-title">铸己匣持续进化中</p>
              <p class="cta-subtitle">更多插件与工具正在锻造</p>
            </div>
          </div>
          <button class="cta-btn">
            查看演进路线
            <svg
              width="14"
              height="14"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
              aria-hidden="true"
            >
              <polyline points="9 18 15 12 9 6" />
            </svg>
          </button>
        </div>
      </section>
    </div>
  </div>
</template>

<style scoped>
.all-features-view {
  height: 100%;
  overflow-y: auto;
  background: var(--el-bg-color);
  display: flex;
  flex-direction: column;
}

.bg-grid {
  flex: 1;
  background-image:
    repeating-linear-gradient(0deg, var(--el-border-color-light) 0px, var(--el-border-color-light) 1px, transparent 1px, transparent 48px),
    repeating-linear-gradient(90deg, var(--el-border-color-light) 0px, var(--el-border-color-light) 1px, transparent 1px, transparent 48px);
  background-size: 48px 48px;
}

/* ===== Page Header ===== */
.page-header {
  padding: 40px 24px 24px;
}

@media (min-width: 1024px) {
  .page-header {
    padding: 48px 48px 24px;
  }
}

.page-title {
  font-size: 2.25rem;
  font-weight: 700;
  color: var(--el-text-color-primary);
  line-height: 1.2;
  margin-bottom: 8px;
  overflow-wrap: break-word;
  word-break: keep-all;
}

.page-subtitle {
  font-size: 16px;
  color: var(--el-text-color-regular);
  line-height: 1.625;
  margin-bottom: 24px;
}

/* ===== Search ===== */
.search-box {
  display: flex;
  align-items: center;
  gap: 12px;
  max-width: 36rem;
  margin-bottom: 20px;
  border: 1px solid var(--el-border-color);
  border-radius: var(--el-border-radius-base);
  background: var(--el-bg-color-page);
  padding: 12px 16px;
  transition: border-color 150ms ease;
}

.search-box:focus-within {
  border-color: var(--el-color-primary);
}

.search-icon {
  color: var(--el-text-color-secondary);
  flex-shrink: 0;
}

.search-input {
  background: transparent;
  border: none;
  outline: none;
  flex: 1;
  font-size: 0.875rem;
  color: var(--el-text-color-primary);
  font-family: var(--font-family-base);
}

.search-input::placeholder {
  color: var(--el-text-color-secondary);
}

/* ===== Filter Pills ===== */
.filter-bar {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: nowrap;
  overflow-x: auto;
}

.filter-bar::-webkit-scrollbar {
  display: none;
}

.filter-bar {
  -ms-overflow-style: none;
  scrollbar-width: none;
}

.filter-pill {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 0.375rem 12px;
  white-space: nowrap;
  flex-shrink: 0;
  font-size: 0.875rem;
  font-weight: 500;
  color: var(--el-text-color-regular);
  border: 1px solid var(--el-border-color);
  border-radius: var(--el-border-radius-base);
  background: transparent;
  transition:
    background 150ms ease,
    color 150ms ease,
    border-color 150ms ease;
}

.filter-pill:hover {
  background: var(--el-fill-color);
  color: var(--el-text-color-primary);
}

.filter-pill--active {
  color: var(--el-color-white);
  background: var(--el-color-primary);
  border-color: var(--el-color-primary);
}

.filter-pill--active:hover {
  background: var(--el-color-primary-light-3);
  color: var(--el-color-white);
}

/* ===== Feature Grid ===== */
.grid-section {
  padding: 0 24px 48px;
}

@media (min-width: 1024px) {
  .grid-section {
    padding: 0 48px 48px;
  }
}

.feature-grid {
  display: grid;
  grid-template-columns: 1fr;
  gap: 16px;
}

@media (min-width: 768px) {
  .feature-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (min-width: 1024px) {
  .feature-grid {
    grid-template-columns: repeat(3, 1fr);
  }
}

.empty-state {
  grid-column: 1 / -1;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 64px 0;
  color: var(--el-text-color-secondary);
  font-size: 16px;
}

/* ===== Feature Card ===== */
.feature-card {
  position: relative;
  display: flex;
  flex-direction: column;
  border: 1px solid var(--el-border-color);
  border-radius: 12px;
  background: var(--el-bg-color-page);
  overflow: hidden;
  transition:
    transform 150ms cubic-bezier(.2,.8,.2,1),
    border-color 150ms cubic-bezier(.2,.8,.2,1);
}

.feature-card:hover {
  transform: translateY(-2px);
  border-color: var(--el-color-primary);
}

.status-dot {
  position: absolute;
  top: 12px;
  right: 12px;
  width: 8px;
  height: 8px;
  border-radius: 50%;
  z-index: 1;
}

.accent-bar {
  height: 4px;
  flex-shrink: 0;
}

.card-body {
  display: flex;
  flex-direction: column;
  flex: 1;
  padding: 20px;
}

/* Header row */
.card-header-row {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 12px;
}

.card-icon-wrap {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 44px;
  height: 44px;
  border-radius: var(--el-border-radius-base);
  flex-shrink: 0;
}

.card-icon {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  line-height: 0;
}

.card-title-group {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
}

.card-title {
  font-size: 1.125rem;
  font-weight: 600;
  color: var(--el-text-color-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.card-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 2px 8px;
  white-space: nowrap;
  flex-shrink: 0;
  font-size: 0.75rem;
  font-weight: 500;
  border-radius: var(--el-border-radius-small);
  width: fit-content;
}

/* Description */
.card-desc {
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  font-size: 0.875rem;
  color: var(--el-text-color-regular);
  line-height: 1.625;
  margin-bottom: 16px;
}

.card-spacer {
  flex: 1;
}

/* Stats */
.card-stats {
  display: flex;
  align-items: center;
  gap: 16px;
  margin-bottom: 16px;
}

.stat-item {
  font-size: 0.75rem;
  color: var(--el-text-color-secondary);
  white-space: nowrap;
}

.stat-mono {
  font-family: var(--font-family-mono);
}

/* Footer */
.card-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.status-indicator {
  display: flex;
  align-items: center;
  gap: 6px;
}

.status-dot-sm {
  width: 7px;
  height: 7px;
  border-radius: 50%;
}

.status-text {
  font-size: 0.75rem;
  white-space: nowrap;
}

.card-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

/* Buttons */
.btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 6px 12px;
  white-space: nowrap;
  font-size: 0.75rem;
  font-weight: 500;
  border-radius: var(--el-border-radius-base);
  transition:
    background 150ms ease,
    color 150ms ease,
    border-color 150ms ease;
}

.btn--primary {
  color: var(--el-color-white);
  background: var(--el-color-primary);
  border: none;
}

.btn--primary:hover {
  background: var(--el-color-primary-light-3);
}

.btn--ghost {
  color: var(--el-text-color-regular);
  border: 1px solid var(--el-border-color);
  background: transparent;
}

.btn--ghost:hover {
  background: var(--el-fill-color);
  color: var(--el-text-color-primary);
}

/* ===== CTA Section ===== */
.cta-section {
  padding: 0 24px 48px;
}

@media (min-width: 1024px) {
  .cta-section {
    padding: 0 48px 48px;
  }
}

.cta-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 16px;
  border: 1px solid var(--el-border-color-light);
  border-radius: 12px;
  background: var(--el-bg-color-page);
  padding: 24px 32px;
}

@media (min-width: 640px) {
  .cta-card {
    flex-direction: row;
    justify-content: space-between;
  }
}

.cta-content {
  display: flex;
  align-items: center;
  gap: 12px;
  min-width: 0;
}

.cta-icon-wrap {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 36px;
  height: 36px;
  border-radius: var(--el-border-radius-base);
  background: var(--el-color-primary-light-9);
  flex-shrink: 0;
}

.cta-text {
  min-width: 0;
}

.cta-title {
  font-size: 0.875rem;
  font-weight: 500;
  color: var(--el-text-color-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.cta-subtitle {
  font-size: 0.75rem;
  color: var(--el-text-color-secondary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.cta-btn {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 8px 16px;
  white-space: nowrap;
  flex-shrink: 0;
  font-size: 0.875rem;
  font-weight: 500;
  color: var(--el-text-color-regular);
  border: 1px solid var(--el-border-color);
  border-radius: var(--el-border-radius-base);
  background: transparent;
  transition:
    background 150ms ease,
    color 150ms ease;
}

.cta-btn:hover {
  background: var(--el-fill-color);
  color: var(--el-text-color-primary);
}
</style>