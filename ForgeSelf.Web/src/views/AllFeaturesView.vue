<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { categories, mergeFeatureList, type FeatureItem } from '@/data/features'
// 图标表已抽到单一源（原为本地常量，首页「常用功能」也需使用）
import { lucideIconSvg } from '@/data/featureIcons'
import { usePluginManifestStore } from '@/stores/pluginManifest'
import type { PluginMenuContribution } from '@/types/plugin'
import { useOpenPage } from '@/composables/useOpenPage'
import PageHead from '@/components/PageHead.vue'
import ForgeCard from '@/components/ForgeCard.vue'
import EmptyState from '@/components/EmptyState.vue'

const manifestStore = usePluginManifestStore()

const activeCategory = ref('all')
const searchQuery = ref('')

// 优先读插件清单（mergeFeatureList 在清单为空/未就绪时回退到内置 features.ts）。
const effectiveFeatures = computed(() => mergeFeatureList(manifestStore.menus))

const filteredFeatures = computed(() => {
  return effectiveFeatures.value.filter((f) => {
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

onMounted(() => {
  void manifestStore.loadManifest()
})

function setCategory(key: string): void {
  activeCategory.value = key
}

const { openPage } = useOpenPage()

// 「配置」按钮目标标签文案：指向 /settings 时显示「设置」，否则显示功能名
function configLabel(item: FeatureItem): string {
  return item.configPath === '/settings' ? '设置' : item.name
}

// 插件管理卡片专用：「插件页面」下拉 = 已启用且声明了菜单的插件贡献（route 即插件页面）
const pluginPages = computed<PluginMenuContribution[]>(() =>
  manifestStore.menus.filter((p) => !!p.route),
)

function openPluginPage(p: PluginMenuContribution): void {
  if (p.route) openPage(p.route, p.menu)
}

function openPluginDetail(p: PluginMenuContribution): void {
  openPage(`/plugins/${p.id}`, p.menu)
}

// 空态图标（lucide search-x / folder）
const emptyIconSvg = `<svg xmlns="http://www.w3.org/2000/svg" width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/><line x1="8" y1="11" x2="14" y2="11"/></svg>`
</script>

<template>
  <div class="all-features-view">
    <div class="page-content">
      <!-- ===== PageHead ===== -->
      <PageHead
        eyebrow="Features · 全部功能"
        title="所有功能"
      >
        <template #actions>
          <div class="search-box">
            <svg
              width="14"
              height="14"
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
        </template>
      </PageHead>

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

      <!-- Feature Grid -->
      <section class="grid-section">
        <div v-if="filteredFeatures.length === 0" class="grid-empty">
          <EmptyState
            :icon="emptyIconSvg"
            title="没有找到匹配的功能"
            sub="试试调整搜索关键字或分类筛选"
          />
        </div>
        <div v-else class="feature-grid">
          <ForgeCard
            v-for="item in filteredFeatures"
            :key="item.id"
            class="feature-card"
            :padding="0"
            :interactive="true"
            @click="openPage(item.path as string, item.name)"
          >
            <!-- Accent bar -->
            <div class="accent-bar" :style="{ background: item.color }" />

            <div class="card-body">
              <!-- Status dot -->
              <div
                class="status-dot"
                :style="{ background: item.color, boxShadow: `0 0 8px ${item.color}` }"
              />

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
                  <div class="status-dot-sm" :style="{ background: 'var(--bronze-500)' }" />
                  <span class="status-text" :style="{ color: 'var(--bronze-500)' }">已启用</span>
                </div>
                <div class="card-actions" @click.stop>
                  <button
                    class="btn btn--primary"
                    :disabled="!item.path"
                    @click="openPage(item.path as string, item.name)"
                  >
                    打开
                  </button>
                  <ElDropdown
                    v-if="item.id === 'plugins'"
                    :disabled="pluginPages.length === 0"
                    trigger="click"
                    @command="openPluginDetail"
                  >
                    <button class="btn btn--ghost">
                      插件详情
                      <svg
                        width="12"
                        height="12"
                        viewBox="0 0 24 24"
                        fill="none"
                        stroke="currentColor"
                        stroke-width="2"
                        stroke-linecap="round"
                        stroke-linejoin="round"
                        aria-hidden="true"
                      >
                        <polyline points="6 9 12 15 18 9" />
                      </svg>
                    </button>
                    <template #dropdown>
                      <ElDropdownMenu>
                        <ElDropdownItem v-for="p in pluginPages" :key="`d-${p.id}`" :command="p">
                          {{ p.menu }}
                        </ElDropdownItem>
                      </ElDropdownMenu>
                    </template>
                  </ElDropdown>
                  <ElDropdown
                    v-if="item.id === 'plugins'"
                    :disabled="pluginPages.length === 0"
                    trigger="click"
                    @command="openPluginPage"
                  >
                    <button class="btn btn--ghost">
                      插件页面
                      <svg
                        width="12"
                        height="12"
                        viewBox="0 0 24 24"
                        fill="none"
                        stroke="currentColor"
                        stroke-width="2"
                        stroke-linecap="round"
                        stroke-linejoin="round"
                        aria-hidden="true"
                      >
                        <polyline points="6 9 12 15 18 9" />
                      </svg>
                    </button>
                    <template #dropdown>
                      <ElDropdownMenu>
                        <ElDropdownItem v-for="p in pluginPages" :key="`p-${p.id}`" :command="p">
                          {{ p.menu }}
                        </ElDropdownItem>
                      </ElDropdownMenu>
                    </template>
                  </ElDropdown>
                  <button
                    class="btn btn--ghost"
                    :disabled="!item.path"
                    @click="openPage((item.configPath ?? item.path) as string, configLabel(item))"
                  >
                    配置
                  </button>
                </div>
              </div>
            </div>
          </ForgeCard>
        </div>
      </section>

      <!-- Bottom CTA -->
      <section class="cta-section">
        <ForgeCard class="cta-card" :padding="20">
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
                style="color: var(--gold-400);"
              >
                <path d="M12 3v12" />
                <path d="M8 8l4 4 4-4" />
                <path d="M8 16h8" />
                <path d="M4 20h16" />
              </svg>
            </div>
            <div class="cta-text">
              <p class="cta-title">铸己匣持续进化中</p>
              <p class="cta-subtitle">{{ effectiveFeatures.length }} 个功能 · 持续扩展中</p>
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
        </ForgeCard>
      </section>
    </div>
  </div>
</template>

<style scoped>
.all-features-view {
  height: 100%;
  overflow-y: auto;
  padding: 24px;
}

.page-content {
  max-width: 1200px;
  margin: 0 auto;
}

/* ===== Search (in PageHead actions) ===== */
.search-box {
  display: flex;
  align-items: center;
  gap: 8px;
  width: 280px;
  border: 1px solid var(--border-default);
  border-radius: var(--radius-md);
  background: var(--glass-bg);
  padding: 6px 12px;
  transition: border-color var(--dur-fast) var(--ease-out);
}

.search-box:focus-within {
  border-color: var(--gold-400);
}

.search-icon {
  color: var(--fg-tertiary);
  flex-shrink: 0;
}

.search-input {
  background: transparent;
  border: none;
  outline: none;
  flex: 1;
  font-size: 13px;
  color: var(--fg-primary);
}

.search-input::placeholder {
  color: var(--fg-muted);
}

/* ===== Filter Pills ===== */
.filter-bar {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  margin-bottom: 20px;
}

.filter-pill {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 4px 14px;
  white-space: nowrap;
  font-size: 13px;
  font-weight: 500;
  color: var(--fg-secondary);
  border: 1px solid var(--border-default);
  border-radius: var(--radius-md);
  background: transparent;
  cursor: pointer;
  transition:
    background var(--dur-fast) var(--ease-out),
    color var(--dur-fast) var(--ease-out),
    border-color var(--dur-fast) var(--ease-out);
}

.filter-pill:hover {
  background: var(--paper-tint);
  color: var(--fg-primary);
}

.filter-pill--active {
  color: var(--ink-950);
  background: var(--gold-400);
  border-color: var(--gold-400);
}

.filter-pill--active:hover {
  background: var(--gold-300);
}

/* ===== Feature Grid ===== */
.grid-section {
  margin-bottom: 32px;
}

.grid-empty {
  padding: 40px 0;
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

/* ===== Feature Card ===== */
.feature-card {
  position: relative;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.accent-bar {
  height: 3px;
  flex-shrink: 0;
}

.card-body {
  display: flex;
  flex-direction: column;
  flex: 1;
  padding: 18px;
}

.status-dot {
  position: absolute;
  top: 14px;
  right: 14px;
  width: 8px;
  height: 8px;
  border-radius: 50%;
  z-index: 1;
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
  width: 42px;
  height: 42px;
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
  gap: 2px;
  min-width: 0;
}

.card-title {
  font-size: 15px;
  font-weight: 600;
  color: var(--fg-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  margin: 0;
}

.card-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 1px 8px;
  white-space: nowrap;
  flex-shrink: 0;
  font-size: 11px;
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
  font-size: 13px;
  color: var(--fg-secondary);
  line-height: 1.6;
  margin: 0 0 16px 0;
}

.card-spacer {
  flex: 1;
}

/* Stats */
.card-stats {
  display: flex;
  align-items: center;
  gap: 16px;
  margin-bottom: 14px;
}

.stat-item {
  font-size: 11px;
  color: var(--fg-tertiary);
  white-space: nowrap;
}

.stat-mono {
  font-family: var(--font-mono);
}

/* Footer */
.card-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
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
  font-size: 11px;
  white-space: nowrap;
}

.card-actions {
  display: flex;
  align-items: center;
  gap: 6px;
}

/* Buttons */
.btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 5px 12px;
  white-space: nowrap;
  font-size: 12px;
  font-weight: 500;
  border-radius: var(--radius-md);
  cursor: pointer;
  transition:
    background var(--dur-fast) var(--ease-out),
    color var(--dur-fast) var(--ease-out),
    border-color var(--dur-fast) var(--ease-out);
}

.btn--primary {
  color: var(--ink-950);
  background: var(--gold-400);
  border: none;
}

.btn--primary:hover {
  background: var(--gold-300);
}

.btn--ghost {
  color: var(--fg-secondary);
  border: 1px solid var(--border-default);
  background: transparent;
}

.btn--ghost:hover {
  background: var(--paper-tint);
  color: var(--fg-primary);
}

/* ===== CTA Section ===== */
.cta-section {
  margin-bottom: 32px;
}

.cta-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 16px;
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
  border-radius: var(--radius-md);
  background: var(--gold-tint);
  flex-shrink: 0;
}

.cta-text {
  min-width: 0;
}

.cta-title {
  font-size: 14px;
  font-weight: 500;
  color: var(--fg-primary);
  margin: 0;
}

.cta-subtitle {
  font-size: 12px;
  color: var(--fg-tertiary);
  margin: 2px 0 0 0;
}

.cta-btn {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 8px 16px;
  white-space: nowrap;
  flex-shrink: 0;
  font-size: 13px;
  font-weight: 500;
  color: var(--fg-secondary);
  border: 1px solid var(--border-default);
  border-radius: var(--radius-md);
  background: transparent;
  cursor: pointer;
  transition:
    background var(--dur-fast) var(--ease-out),
    color var(--dur-fast) var(--ease-out);
}

.cta-btn:hover {
  background: var(--paper-tint);
  color: var(--fg-primary);
}
</style>
