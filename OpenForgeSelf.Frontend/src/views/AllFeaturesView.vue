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
    color: 'var(--primary-color)',
    bgColor: 'var(--primary-soft)',
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
    color: 'var(--info-color)',
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
    color: 'var(--info-color)',
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
    color: 'var(--info-color)',
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
    color: 'var(--success-color)',
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
                  <div class="status-dot-sm" :style="{ background: 'var(--success-color)' }" />
                  <span class="status-text" :style="{ color: 'var(--success-color)' }">已启用</span>
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
                style="color: var(--primary-color);"
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
  background: var(--bg-primary);
  display: flex;
  flex-direction: column;
}

.bg-grid {
  flex: 1;
  background-image:
    repeating-linear-gradient(0deg, var(--border-light) 0px, var(--border-light) 1px, transparent 1px, transparent 48px),
    repeating-linear-gradient(90deg, var(--border-light) 0px, var(--border-light) 1px, transparent 1px, transparent 48px);
  background-size: 48px 48px;
}

/* ===== Page Header ===== */
.page-header {
  padding: var(--space-10) var(--space-6) var(--space-6);
}

@media (min-width: 1024px) {
  .page-header {
    padding: var(--space-12) var(--space-12) var(--space-6);
  }
}

.page-title {
  font-size: 2.25rem;
  font-weight: 700;
  color: var(--text-primary);
  line-height: 1.2;
  margin-bottom: var(--space-2);
  overflow-wrap: break-word;
  word-break: keep-all;
}

.page-subtitle {
  font-size: var(--space-4);
  color: var(--text-secondary);
  line-height: 1.625;
  margin-bottom: var(--space-6);
}

/* ===== Search ===== */
.search-box {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  max-width: 36rem;
  margin-bottom: var(--space-5);
  border: 1px solid var(--border-color);
  border-radius: var(--radius-md);
  background: var(--bg-secondary);
  padding: var(--space-3) var(--space-4);
  transition: border-color var(--motion-fast);
}

.search-box:focus-within {
  border-color: var(--primary-color);
}

.search-icon {
  color: var(--text-muted);
  flex-shrink: 0;
}

.search-input {
  background: transparent;
  border: none;
  outline: none;
  flex: 1;
  font-size: var(--space-3-5, 0.875rem);
  color: var(--text-primary);
  font-family: var(--font-family-base);
}

.search-input::placeholder {
  color: var(--text-muted);
}

/* ===== Filter Pills ===== */
.filter-bar {
  display: flex;
  align-items: center;
  gap: var(--space-2);
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
  padding: var(--space-1-5, 0.375rem) var(--space-3);
  white-space: nowrap;
  flex-shrink: 0;
  font-size: var(--space-3-5, 0.875rem);
  font-weight: 500;
  color: var(--text-secondary);
  border: 1px solid var(--border-color);
  border-radius: var(--radius-md);
  background: transparent;
  transition:
    background var(--motion-fast),
    color var(--motion-fast),
    border-color var(--motion-fast);
}

.filter-pill:hover {
  background: var(--bg-hover);
  color: var(--text-primary);
}

.filter-pill--active {
  color: var(--primary-contrast);
  background: var(--primary-color);
  border-color: var(--primary-color);
}

.filter-pill--active:hover {
  background: var(--primary-hover);
  color: var(--primary-contrast);
}

/* ===== Feature Grid ===== */
.grid-section {
  padding: 0 var(--space-6) var(--space-12);
}

@media (min-width: 1024px) {
  .grid-section {
    padding: 0 var(--space-12) var(--space-12);
  }
}

.feature-grid {
  display: grid;
  grid-template-columns: 1fr;
  gap: var(--space-4);
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
  padding: var(--space-16) 0;
  color: var(--text-muted);
  font-size: var(--space-4);
}

/* ===== Feature Card ===== */
.feature-card {
  position: relative;
  display: flex;
  flex-direction: column;
  border: 1px solid var(--border-color);
  border-radius: var(--radius-lg);
  background: var(--bg-secondary);
  overflow: hidden;
  transition:
    transform 150ms cubic-bezier(.2,.8,.2,1),
    border-color 150ms cubic-bezier(.2,.8,.2,1);
}

.feature-card:hover {
  transform: translateY(-2px);
  border-color: var(--primary-color);
}

.status-dot {
  position: absolute;
  top: var(--space-3);
  right: var(--space-3);
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
  padding: var(--space-5);
}

/* Header row */
.card-header-row {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  margin-bottom: var(--space-3);
}

.card-icon-wrap {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 44px;
  height: 44px;
  border-radius: var(--radius-md);
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
  gap: var(--space-0-5, 2px);
  min-width: 0;
}

.card-title {
  font-size: var(--space-4-5, 1.125rem);
  font-weight: 600;
  color: var(--text-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.card-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: var(--space-0-5, 2px) var(--space-2);
  white-space: nowrap;
  flex-shrink: 0;
  font-size: var(--space-3, 0.75rem);
  font-weight: 500;
  border-radius: var(--radius-sm);
  width: fit-content;
}

/* Description */
.card-desc {
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  font-size: var(--space-3-5, 0.875rem);
  color: var(--text-secondary);
  line-height: 1.625;
  margin-bottom: var(--space-4);
}

.card-spacer {
  flex: 1;
}

/* Stats */
.card-stats {
  display: flex;
  align-items: center;
  gap: var(--space-4);
  margin-bottom: var(--space-4);
}

.stat-item {
  font-size: var(--space-3, 0.75rem);
  color: var(--text-muted);
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
  gap: var(--space-1-5, 6px);
}

.status-dot-sm {
  width: 7px;
  height: 7px;
  border-radius: 50%;
}

.status-text {
  font-size: var(--space-3, 0.75rem);
  white-space: nowrap;
}

.card-actions {
  display: flex;
  align-items: center;
  gap: var(--space-2);
}

/* Buttons */
.btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: var(--space-1-5, 6px) var(--space-3);
  white-space: nowrap;
  font-size: var(--space-3, 0.75rem);
  font-weight: 500;
  border-radius: var(--radius-md);
  transition:
    background var(--motion-fast),
    color var(--motion-fast),
    border-color var(--motion-fast);
}

.btn--primary {
  color: var(--primary-contrast);
  background: var(--primary-color);
  border: none;
}

.btn--primary:hover {
  background: var(--primary-hover);
}

.btn--ghost {
  color: var(--text-secondary);
  border: 1px solid var(--border-color);
  background: transparent;
}

.btn--ghost:hover {
  background: var(--bg-hover);
  color: var(--text-primary);
}

/* ===== CTA Section ===== */
.cta-section {
  padding: 0 var(--space-6) var(--space-12);
}

@media (min-width: 1024px) {
  .cta-section {
    padding: 0 var(--space-12) var(--space-12);
  }
}

.cta-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--space-4);
  border: 1px solid var(--border-light);
  border-radius: var(--radius-lg);
  background: var(--bg-secondary);
  padding: var(--space-6) var(--space-8);
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
  gap: var(--space-3);
  min-width: 0;
}

.cta-icon-wrap {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 36px;
  height: 36px;
  border-radius: var(--radius-md);
  background: var(--primary-soft);
  flex-shrink: 0;
}

.cta-text {
  min-width: 0;
}

.cta-title {
  font-size: var(--space-3-5, 0.875rem);
  font-weight: 500;
  color: var(--text-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.cta-subtitle {
  font-size: var(--space-3, 0.75rem);
  color: var(--text-muted);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.cta-btn {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  padding: var(--space-2) var(--space-4);
  white-space: nowrap;
  flex-shrink: 0;
  font-size: var(--space-3-5, 0.875rem);
  font-weight: 500;
  color: var(--text-secondary);
  border: 1px solid var(--border-color);
  border-radius: var(--radius-md);
  background: transparent;
  transition:
    background var(--motion-fast),
    color var(--motion-fast);
}

.cta-btn:hover {
  background: var(--bg-hover);
  color: var(--text-primary);
}
</style>