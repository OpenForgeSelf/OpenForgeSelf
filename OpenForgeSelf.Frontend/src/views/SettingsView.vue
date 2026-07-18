<script setup lang="ts">
import { computed, ref } from 'vue'
import ForgeSwitch from '@/components/forge/ForgeSwitch.vue'
import ForgeSelect from '@/components/forge/ForgeSelect.vue'
import ForgeButton from '@/components/forge/ForgeButton.vue'
import ForgeCard from '@/components/forge/ForgeCard.vue'
import { useThemeStore } from '@/stores/theme'
import type { ThemeMode } from '@/stores/theme'

const themeStore = useThemeStore()

type SettingsCategory = 'general' | 'ai-agent' | 'plugins' | 'appearance' | 'data' | 'about'

const activeCategory = ref<SettingsCategory>('general')

interface NavItem {
  key: SettingsCategory
  label: string
  icon: string
}

const navItems: NavItem[] = [
  { key: 'general', label: '通用', icon: 'M4 6a2 2 0 012-2h12a2 2 0 012 2v12a2 2 0 01-2 2H6a2 2 0 01-2-2V6zm2 0h12v12H6V6zm2 2h8v2H8V8zm0 4h8v2H8v-2zm0 4h4v2H8v-2z' },
  { key: 'ai-agent', label: 'AI Agent', icon: 'M12 2a10 10 0 00-10 10c0 4.42 2.65 8.17 6.35 9.95.5.09.68-.22.68-.48v-1.7c-2.58.56-3.13-1.24-3.13-1.24-.42-1.07-1.03-1.35-1.03-1.35-.84-.57.06-.56.06-.56.93.07 1.42 1.07 1.42 1.07.83 1.42 2.18 1.01 2.71.77.08-.6.33-1.01.6-1.24-2.07-.24-4.25-1.04-4.25-4.62 0-1.02.36-1.86.95-2.52-.1-.24-.42-1.2.09-2.5 0 0 .78-.25 2.55.95A8.9 8.9 0 0112 7c.8 0 1.6.11 2.36.33 1.77-1.2 2.55-.95 2.55-.95.51 1.3.19 2.26.09 2.5.59.66.95 1.5.95 2.52 0 3.58-2.18 4.38-4.25 4.62.33.29.55.73.55 1.48v2.13c0 .26.18.58.69.48A10.02 10.02 0 0022 12 10 10 0 0012 2z' },
  { key: 'plugins', label: '插件管理', icon: 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 17.93c-3.95-.49-7-3.85-7-7.93 0-.62.08-1.21.21-1.79L9 15v1c0 1.1.9 2 2 2v1.93zm6.9-2.54c-.26-.81-1-1.39-1.9-1.39h-1v-3c0-.55-.45-1-1-1H8v-2h2c.55 0 1-.45 1-1V7h2c1.1 0 2-.9 2-2v-.41c2.93 1.19 5 4.06 5 7.41 0 2.08-.8 3.97-2.1 5.39z' },
  { key: 'appearance', label: '外观', icon: 'M12 3v2m0 14v2m9-9h-2M5 12H3m15.36-6.36l-1.42 1.42M7.06 16.94l-1.42 1.42M18.36 18.36l-1.42-1.42M7.06 7.06L5.64 5.64M16 12a4 4 0 11-8 0 4 4 0 018 0z' },
  { key: 'data', label: '数据与存储', icon: 'M4 7v10c0 2.21 3.58 4 8 4s8-1.79 8-4V7c0-2.21-3.58-4-8-4s-8 1.79-8 4zm16 0c0 2.21-3.58 4-8 4s-8-1.79-8-4m0 5c0 2.21 3.58 4 8 4s8-1.79 8-4' },
  { key: 'about', label: '关于', icon: 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z' },
]

// ---- General panel state ----
const language = ref('zh-CN')
const startupPage = ref('home')
const defaultModel = ref('gemma-2b')
const autoUpdate = ref(true)
const autoStart = ref(false)

// ---- AI Agent panel state ----
const agentDefaultModel = ref('gemma-2b')
const localModelPath = ref('~/.forgeself/models')
const maxContext = ref(4096)
const temperature = ref(0.7)
const systemPrompt = ref('你是一个高效的个人 AI 助手，专注于工具调用和任务执行。')

// ---- Plugins panel state ----
interface PluginItem {
  name: string
  icon: string
  version: string
  enabled: boolean
}

const plugins = ref<PluginItem[]>([
  { name: 'JSON 格式化', icon: 'M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8l-6-6zM6 20V4h7v5h5v11H6z', version: 'v1.2.0', enabled: true },
  { name: '正则测试器', icon: 'M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8l-6-6zM6 20V4h7v5h5v11H6z', version: 'v1.0.3', enabled: true },
  { name: '图片压缩', icon: 'M21 19V5a2 2 0 00-2-2H5a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2zM8.5 10a1.5 1.5 0 100-3 1.5 1.5 0 000 3zm10.5 5l-4-4-4 4-3-3-4 4', version: 'v0.9.1', enabled: false },
])

function togglePlugin(index: number): void {
  plugins.value[index].enabled = !plugins.value[index].enabled
}

// ---- Appearance panel state ----
const accentColor = ref('amber')
const fontSize = ref(15)
const animationEnabled = ref(true)

const colorOptions: { key: string; color: string; label: string }[] = [
  { key: 'amber', color: '#F59E0B', label: '琥珀色' },
  { key: 'blue', color: '#58A6FF', label: '蓝色' },
  { key: 'green', color: '#3FB950', label: '绿色' },
  { key: 'purple', color: '#A371F7', label: '紫色' },
  { key: 'red', color: '#F85149', label: '红色' },
]

function setTheme(mode: ThemeMode): void {
  themeStore.setMode(mode)
}

// ---- Data panel state ----
const storageUsed = 128
const storageTotal = 1024
const storagePercent = computed(() => ((storageUsed / storageTotal) * 100).toFixed(1))
</script>

<template>
  <div class="settings-view">
    <div class="settings-layout">
      <!-- Left: Settings Category Nav -->
      <nav class="settings-nav" aria-label="设置分类">
        <button
          v-for="item in navItems"
          :key="item.key"
          class="settings-nav__item"
          :class="{ 'settings-nav__item--active': activeCategory === item.key }"
          @click="activeCategory = item.key"
        >
          <svg
            class="settings-nav__icon"
            width="18"
            height="18"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <path :d="item.icon" />
          </svg>
          <span class="settings-nav__label">{{ item.label }}</span>
        </button>
      </nav>

      <!-- Right: Settings Content Panel -->
      <div class="settings-panel-wrapper">
        <!-- ===== General Panel ===== -->
        <ForgeCard v-show="activeCategory === 'general'" class="settings-panel">
          <div class="settings-panel__header">
            <h2 class="settings-panel__title">通用设置</h2>
            <p class="settings-panel__desc">配置应用的基本行为与偏好</p>
          </div>
          <div class="settings-panel__body">
            <!-- 语言 -->
            <div class="settings-row">
              <div class="settings-row__info">
                <label class="settings-row__label">语言</label>
                <span class="settings-row__hint">界面显示语言</span>
              </div>
              <ForgeSelect
                v-model="language"
                :options="[
                  { value: 'zh-CN', label: '简体中文' },
                  { value: 'en', label: 'English' },
                  { value: 'ja', label: '日本語' },
                ]"
              />
            </div>
            <!-- 启动时打开 -->
            <div class="settings-row">
              <div class="settings-row__info">
                <label class="settings-row__label">启动时打开</label>
                <span class="settings-row__hint">应用启动后显示的默认页面</span>
              </div>
              <ForgeSelect
                v-model="startupPage"
                :options="[
                  { value: 'home', label: '首页' },
                  { value: 'plugin-store', label: '插件商店' },
                  { value: 'system-monitor', label: '系统监控' },
                ]"
              />
            </div>
            <!-- 默认 AI 模型 -->
            <div class="settings-row">
              <div class="settings-row__info">
                <label class="settings-row__label">默认 AI 模型</label>
                <span class="settings-row__hint">AI Agent 使用的默认推理模型</span>
              </div>
              <ForgeSelect
                v-model="defaultModel"
                :options="[
                  { value: 'gemma-2b', label: 'Gemma 2B 本地' },
                  { value: 'qwen-7b', label: 'Qwen 7B 本地' },
                  { value: 'deepseek', label: 'DeepSeek API' },
                ]"
              />
            </div>
            <!-- 数据存储路径 -->
            <div class="settings-row">
              <div class="settings-row__info">
                <label class="settings-row__label">数据存储路径</label>
                <span class="settings-row__hint">所有本地数据与缓存的存储位置</span>
              </div>
              <div class="settings-row__control-row">
                <code class="settings-row__path">~/.forgeself/data</code>
                <ForgeButton variant="ghost" size="sm">更改</ForgeButton>
              </div>
            </div>
            <!-- 自动更新 -->
            <div class="settings-row">
              <div class="settings-row__info">
                <label class="settings-row__label">自动更新</label>
                <span class="settings-row__hint">有新版本时自动下载并安装</span>
              </div>
              <ForgeSwitch v-model="autoUpdate" />
            </div>
            <!-- 开机自启 -->
            <div class="settings-row settings-row--last">
              <div class="settings-row__info">
                <label class="settings-row__label">开机自启</label>
                <span class="settings-row__hint">系统启动时自动运行铸己匣</span>
              </div>
              <ForgeSwitch v-model="autoStart" />
            </div>
          </div>
        </ForgeCard>

        <!-- ===== AI Agent Panel ===== -->
        <ForgeCard v-show="activeCategory === 'ai-agent'" class="settings-panel">
          <div class="settings-panel__header">
            <h2 class="settings-panel__title">AI Agent 配置</h2>
            <p class="settings-panel__desc">调整模型参数、上下文长度与推理行为</p>
          </div>
          <div class="settings-panel__body">
            <!-- 默认模型 -->
            <div class="settings-row">
              <div class="settings-row__info">
                <label class="settings-row__label">默认模型</label>
                <span class="settings-row__hint">推理使用的模型</span>
              </div>
              <ForgeSelect
                v-model="agentDefaultModel"
                :options="[
                  { value: 'gemma-2b', label: 'Gemma 2B 本地' },
                  { value: 'qwen-7b', label: 'Qwen 7B 本地' },
                ]"
              />
            </div>
            <!-- 本地模型路径 -->
            <div class="settings-row">
              <div class="settings-row__info">
                <label class="settings-row__label">本地模型路径</label>
                <span class="settings-row__hint">本地模型权重文件目录</span>
              </div>
              <div class="settings-row__control-row">
                <code class="settings-row__path">{{ localModelPath }}</code>
                <ForgeButton variant="ghost" size="sm">浏览</ForgeButton>
              </div>
            </div>
            <!-- 最大上下文长度 -->
            <div class="settings-row">
              <div class="settings-row__info">
                <label class="settings-row__label">最大上下文长度</label>
                <span class="settings-row__hint">单次对话的最大 token 数</span>
              </div>
              <div class="settings-row__slider-group">
                <input
                  v-model.number="maxContext"
                  type="range"
                  class="settings-slider"
                  min="512"
                  max="8192"
                  step="512"
                  aria-label="最大上下文长度"
                />
                <code class="settings-slider__value">{{ maxContext }}</code>
              </div>
            </div>
            <!-- 温度 -->
            <div class="settings-row">
              <div class="settings-row__info">
                <label class="settings-row__label">温度</label>
                <span class="settings-row__hint">控制生成文本的随机性</span>
              </div>
              <div class="settings-row__slider-group">
                <input
                  v-model.number="temperature"
                  type="range"
                  class="settings-slider"
                  min="0"
                  max="2"
                  step="0.1"
                  aria-label="温度"
                />
                <code class="settings-slider__value">{{ temperature.toFixed(1) }}</code>
              </div>
            </div>
            <!-- 系统提示词 -->
            <div class="settings-row settings-row--vertical settings-row--last">
              <div class="settings-row__info">
                <label class="settings-row__label">系统提示词</label>
                <span class="settings-row__hint">AI Agent 的系统级指令</span>
              </div>
              <textarea
                v-model="systemPrompt"
                class="settings-textarea"
                rows="3"
                placeholder="输入系统提示词..."
              />
            </div>
          </div>
        </ForgeCard>

        <!-- ===== Plugins Panel ===== -->
        <ForgeCard v-show="activeCategory === 'plugins'" class="settings-panel">
          <div class="settings-panel__header">
            <h2 class="settings-panel__title">插件管理</h2>
            <p class="settings-panel__desc">启用、禁用或导入第三方插件</p>
          </div>
          <div class="settings-panel__body">
            <div class="settings-plugin-list">
              <div
                v-for="(plugin, index) in plugins"
                :key="plugin.name"
                class="settings-plugin-item"
                :class="{ 'settings-plugin-item--last': index === plugins.length - 1 }"
              >
                <div class="settings-plugin-item__info">
                  <svg
                    class="settings-plugin-item__icon"
                    width="18"
                    height="18"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    stroke-width="2"
                    stroke-linecap="round"
                    stroke-linejoin="round"
                    aria-hidden="true"
                  >
                    <path :d="plugin.icon" />
                  </svg>
                  <div class="settings-plugin-item__meta">
                    <span class="settings-plugin-item__name">{{ plugin.name }}</span>
                    <span class="settings-plugin-item__version">{{ plugin.version }}</span>
                  </div>
                </div>
                <ForgeSwitch
                  :model-value="plugin.enabled"
                  @update:model-value="togglePlugin(index)"
                />
              </div>
            </div>
            <div class="settings-plugin-actions">
              <ForgeButton variant="ghost" size="md">
                <template #default>
                  <svg
                    width="16"
                    height="16"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    stroke-width="2"
                    stroke-linecap="round"
                    stroke-linejoin="round"
                    aria-hidden="true"
                  >
                    <line x1="12" y1="5" x2="12" y2="19" />
                    <line x1="5" y1="12" x2="19" y2="12" />
                  </svg>
                  导入插件
                </template>
              </ForgeButton>
              <a href="#" class="settings-plugin-link">打开插件目录</a>
            </div>
          </div>
        </ForgeCard>

        <!-- ===== Appearance Panel ===== -->
        <ForgeCard v-show="activeCategory === 'appearance'" class="settings-panel">
          <div class="settings-panel__header">
            <h2 class="settings-panel__title">外观</h2>
            <p class="settings-panel__desc">主题、配色与视觉偏好</p>
          </div>
          <div class="settings-panel__body">
            <!-- 主题 -->
            <div class="settings-row">
              <div class="settings-row__info">
                <label class="settings-row__label">主题</label>
                <span class="settings-row__hint">界面明暗模式</span>
              </div>
              <div class="theme-toggle-group">
                <button
                  class="theme-toggle-btn"
                  :class="{ 'theme-toggle-btn--active': themeStore.mode === 'light' }"
                  @click="setTheme('light')"
                >
                  浅色
                </button>
                <button
                  class="theme-toggle-btn"
                  :class="{ 'theme-toggle-btn--active': themeStore.mode === 'dark' }"
                  @click="setTheme('dark')"
                >
                  深色
                </button>
                <button
                  class="theme-toggle-btn"
                  :class="{ 'theme-toggle-btn--active': themeStore.mode === 'system' }"
                  @click="setTheme('system')"
                >
                  跟随系统
                </button>
              </div>
            </div>
            <!-- 强调色 -->
            <div class="settings-row">
              <div class="settings-row__info">
                <label class="settings-row__label">强调色</label>
                <span class="settings-row__hint">按钮、链接与交互元素的颜色</span>
              </div>
              <div class="color-picker">
                <button
                  v-for="opt in colorOptions"
                  :key="opt.key"
                  class="color-picker__dot"
                  :class="{ 'color-picker__dot--active': accentColor === opt.key }"
                  :style="{ background: opt.color }"
                  :aria-label="opt.label"
                  @click="accentColor = opt.key"
                />
              </div>
            </div>
            <!-- 字体大小 -->
            <div class="settings-row">
              <div class="settings-row__info">
                <label class="settings-row__label">字体大小</label>
                <span class="settings-row__hint">界面文字的缩放比例</span>
              </div>
              <div class="settings-row__slider-group">
                <span class="settings-row__slider-label" style="font-size: 12px;">A</span>
                <input
                  v-model.number="fontSize"
                  type="range"
                  class="settings-slider"
                  min="12"
                  max="20"
                  step="1"
                  aria-label="字体大小"
                />
                <span class="settings-row__slider-label" style="font-size: 18px;">A</span>
              </div>
            </div>
            <!-- 动画效果 -->
            <div class="settings-row settings-row--last">
              <div class="settings-row__info">
                <label class="settings-row__label">动画效果</label>
                <span class="settings-row__hint">界面过渡与微交互动画</span>
              </div>
              <ForgeSwitch v-model="animationEnabled" />
            </div>
          </div>
        </ForgeCard>

        <!-- ===== Data & Storage Panel ===== -->
        <ForgeCard v-show="activeCategory === 'data'" class="settings-panel">
          <div class="settings-panel__header">
            <h2 class="settings-panel__title">数据与存储</h2>
            <p class="settings-panel__desc">管理本地数据、缓存与导出</p>
          </div>
          <div class="settings-panel__body">
            <!-- 存储用量 -->
            <div class="settings-storage">
              <div class="settings-storage__header">
                <span class="settings-storage__label">使用数据</span>
                <span class="settings-storage__value">{{ storageUsed }} MB / {{ storageTotal }} GB</span>
              </div>
              <div class="settings-storage__bar">
                <div class="settings-storage__fill" :style="{ width: storagePercent + '%' }" />
              </div>
            </div>
            <!-- Action buttons -->
            <div class="settings-data-actions">
              <ForgeButton variant="ghost" size="md">
                <template #default>
                  <svg
                    width="16"
                    height="16"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    stroke-width="2"
                    stroke-linecap="round"
                    stroke-linejoin="round"
                    aria-hidden="true"
                  >
                    <polyline points="3 6 5 6 21 6" />
                    <path d="M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6m3 0V4a2 2 0 012-2h4a2 2 0 012 2v2" />
                  </svg>
                  清除缓存
                </template>
              </ForgeButton>
              <ForgeButton variant="ghost" size="md">
                <template #default>
                  <svg
                    width="16"
                    height="16"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    stroke-width="2"
                    stroke-linecap="round"
                    stroke-linejoin="round"
                    aria-hidden="true"
                  >
                    <path d="M21 15v4a2 2 0 01-2 2H5a2 2 0 01-2-2v-4" />
                    <polyline points="7 10 12 15 17 10" />
                    <line x1="12" y1="15" x2="12" y2="3" />
                  </svg>
                  导出数据
                </template>
              </ForgeButton>
            </div>
            <!-- Danger zone -->
            <div class="settings-danger">
              <div class="settings-row__info">
                <span class="settings-danger__label">重置所有设置</span>
                <span class="settings-row__hint">恢复为出厂默认值，此操作不可撤销</span>
              </div>
              <ForgeButton variant="danger" size="sm">重置</ForgeButton>
            </div>
          </div>
        </ForgeCard>

        <!-- ===== About Panel ===== -->
        <ForgeCard v-show="activeCategory === 'about'" class="settings-panel">
          <div class="settings-panel__header">
            <h2 class="settings-panel__title">关于</h2>
          </div>
          <div class="settings-about">
            <!-- Logo -->
            <svg
              class="settings-about__logo"
              width="48"
              height="48"
              viewBox="0 0 28 28"
              fill="none"
              aria-hidden="true"
            >
              <rect
                x="3"
                y="14"
                width="22"
                height="8"
                rx="2"
                fill="var(--fs-color-primary)"
                opacity="0.9"
              />
              <rect
                x="6"
                y="8"
                width="16"
                height="8"
                rx="2"
                fill="var(--fs-color-primary)"
                opacity="0.7"
              />
              <rect
                x="9"
                y="3"
                width="10"
                height="7"
                rx="2"
                fill="var(--fs-color-primary)"
              />
              <rect
                x="12"
                y="22"
                width="4"
                height="3"
                rx="1"
                fill="var(--fs-color-text-tertiary)"
              />
            </svg>
            <div class="settings-about__info">
              <h3 class="settings-about__name">铸己匣 OpenForgeSelf</h3>
              <code class="settings-about__version">v0.1.0</code>
            </div>
            <p class="settings-about__tagline">以器铸己，日积寸进</p>
            <div class="settings-about__links">
              <a href="#" class="settings-about__link">
                <svg
                  width="16"
                  height="16"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  stroke-width="2"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                  aria-hidden="true"
                >
                  <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 17.93c-3.95-.49-7-3.85-7-7.93 0-.62.08-1.21.21-1.79L9 15v1c0 1.1.9 2 2 2v1.93zm6.9-2.54c-.26-.81-1-1.39-1.9-1.39h-1v-3c0-.55-.45-1-1-1H8v-2h2c.55 0 1-.45 1-1V7h2c1.1 0 2-.9 2-2v-.41c2.93 1.19 5 4.06 5 7.41 0 2.08-.8 3.97-2.1 5.39z" />
                </svg>
                GitHub
              </a>
              <span class="settings-about__license">MIT License</span>
            </div>
          </div>
        </ForgeCard>
      </div>
    </div>
  </div>
</template>

<style scoped>
.settings-view {
  padding: var(--space-6);
  height: 100%;
  min-height: 0;
  overflow-y: auto;
}

.settings-layout {
  display: flex;
  gap: var(--space-6);
  max-width: 800px;
  margin: 0 auto;
}

/* ===== Left Navigation ===== */
.settings-nav {
  display: flex;
  flex-direction: column;
  gap: 2px;
  width: 180px;
  flex-shrink: 0;
}

.settings-nav__item {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  padding: 10px var(--space-3);
  border: none;
  border-left: 3px solid transparent;
  border-radius: 0 var(--radius-md) var(--radius-md) 0;
  background: transparent;
  color: var(--text-secondary);
  cursor: pointer;
  font-size: var(--fs-text-sm, 0.8125rem);
  font-weight: var(--fs-weight-medium, 500);
  font-family: inherit;
  text-align: left;
  transition: background-color var(--motion-fast, 150ms ease), color var(--motion-fast, 150ms ease), border-color var(--motion-fast, 150ms ease);
  width: 100%;
}

.settings-nav__item:hover {
  background: var(--bg-hover);
  color: var(--text-primary);
}

.settings-nav__item--active {
  border-left-color: var(--primary-color);
  background: var(--primary-soft);
  color: var(--primary-color);
}

.settings-nav__icon {
  flex-shrink: 0;
}

.settings-nav__label {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* ===== Right Panel ===== */
.settings-panel-wrapper {
  flex: 1;
  min-width: 0;
}

.settings-panel {
  overflow: hidden;
}

.settings-panel__header {
  padding: var(--space-4) var(--space-6);
  border-bottom: 1px solid var(--border-color);
}

.settings-panel__title {
  font-family: var(--fs-font-display, inherit);
  font-size: var(--fs-text-lg, 1.125rem);
  font-weight: var(--fs-weight-semibold, 600);
  color: var(--text-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.settings-panel__desc {
  font-size: var(--fs-text-sm, 0.8125rem);
  color: var(--text-secondary);
  margin-top: var(--space-1);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.settings-panel__body {
  padding: 0 var(--space-6);
}

/* ===== Setting Row ===== */
.settings-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: var(--space-4) 0;
  border-bottom: 1px solid var(--border-light);
}

.settings-row--last {
  border-bottom: none;
}

.settings-row--vertical {
  flex-direction: column;
  align-items: flex-start;
  gap: var(--space-3);
}

.settings-row__info {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
  flex: 1;
  margin-right: var(--space-4);
}

.settings-row__label {
  font-size: var(--fs-text-sm, 0.8125rem);
  font-weight: var(--fs-weight-medium, 500);
  color: var(--text-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.settings-row__hint {
  font-size: var(--fs-text-xs, 0.75rem);
  color: var(--text-muted);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.settings-row__control-row {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  flex-shrink: 0;
}

.settings-row__path {
  font-family: var(--font-family-mono);
  font-size: var(--fs-text-xs, 0.75rem);
  color: var(--text-secondary);
  background: var(--bg-tertiary);
  padding: var(--space-2) var(--space-3);
  border-radius: var(--radius-md);
  border: 1px solid var(--border-light);
  max-width: 200px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.settings-row__slider-group {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  flex-shrink: 0;
}

.settings-row__slider-label {
  color: var(--text-muted);
  line-height: 1;
  user-select: none;
}

/* ===== Slider ===== */
.settings-slider {
  width: 120px;
  accent-color: var(--primary-color);
  cursor: pointer;
}

.settings-slider__value {
  font-family: var(--font-family-mono);
  font-size: var(--fs-text-sm, 0.8125rem);
  color: var(--primary-color);
  min-width: 48px;
  text-align: right;
}

/* ===== Textarea ===== */
.settings-textarea {
  width: 100%;
  background: var(--bg-tertiary);
  border: 1px solid var(--border-color);
  border-radius: var(--radius-md);
  color: var(--text-primary);
  font-size: var(--fs-text-sm, 0.8125rem);
  font-family: var(--fs-font-body, inherit);
  padding: var(--space-3);
  resize: vertical;
  line-height: 1.625;
  transition: border-color var(--motion-fast, 150ms ease);
  box-sizing: border-box;
}

.settings-textarea:focus {
  border-color: var(--primary-color);
  outline: none;
}

.settings-textarea::placeholder {
  color: var(--text-muted);
}

/* ===== Plugins ===== */
.settings-plugin-list {
  display: flex;
  flex-direction: column;
}

.settings-plugin-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: var(--space-3) 0;
  border-bottom: 1px solid var(--border-light);
}

.settings-plugin-item--last {
  border-bottom: none;
}

.settings-plugin-item__info {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  min-width: 0;
  flex: 1;
  margin-right: var(--space-4);
}

.settings-plugin-item__icon {
  color: var(--text-muted);
  flex-shrink: 0;
}

.settings-plugin-item__meta {
  display: flex;
  flex-direction: column;
  gap: 1px;
  min-width: 0;
}

.settings-plugin-item__name {
  font-size: var(--fs-text-sm, 0.8125rem);
  font-weight: var(--fs-weight-medium, 500);
  color: var(--text-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.settings-plugin-item__version {
  font-size: var(--fs-text-xs, 0.75rem);
  color: var(--text-muted);
}

.settings-plugin-actions {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  margin-top: var(--space-6);
  padding-bottom: var(--space-4);
}

.settings-plugin-link {
  font-size: var(--fs-text-xs, 0.75rem);
  color: var(--info-color);
  text-decoration: none;
}

.settings-plugin-link:hover {
  text-decoration: underline;
}

/* ===== Theme Toggle ===== */
.theme-toggle-group {
  display: flex;
  align-items: center;
  gap: 1px;
  background: var(--bg-tertiary);
  border-radius: var(--radius-md);
  padding: 2px;
  border: 1px solid var(--border-light);
  flex-shrink: 0;
}

.theme-toggle-btn {
  padding: 6px 12px;
  border: none;
  border-radius: var(--radius-sm);
  background: transparent;
  color: var(--text-secondary);
  font-size: var(--fs-text-xs, 0.75rem);
  font-weight: var(--fs-weight-medium, 500);
  cursor: pointer;
  font-family: inherit;
  transition: background-color var(--motion-fast, 150ms ease), color var(--motion-fast, 150ms ease);
  white-space: nowrap;
}

.theme-toggle-btn--active {
  background: var(--primary-color);
  color: var(--primary-contrast);
}

/* ===== Color Picker ===== */
.color-picker {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  flex-shrink: 0;
}

.color-picker__dot {
  width: 28px;
  height: 28px;
  border-radius: var(--radius-pill);
  border: 2px solid transparent;
  cursor: pointer;
  padding: 0;
  transition: border-color var(--motion-fast, 150ms ease), box-shadow var(--motion-fast, 150ms ease);
}

.color-picker__dot:hover {
  opacity: 0.85;
}

.color-picker__dot--active {
  border-color: var(--text-primary);
  box-shadow: 0 0 0 2px var(--bg-secondary);
}

/* ===== Storage ===== */
.settings-storage {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  padding: var(--space-4) 0;
  border-bottom: 1px solid var(--border-light);
}

.settings-storage__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.settings-storage__label {
  font-size: var(--fs-text-sm, 0.8125rem);
  font-weight: var(--fs-weight-medium, 500);
  color: var(--text-primary);
}

.settings-storage__value {
  font-family: var(--font-family-mono);
  font-size: var(--fs-text-xs, 0.75rem);
  color: var(--text-secondary);
}

.settings-storage__bar {
  height: 6px;
  border-radius: var(--radius-pill);
  background: var(--bg-muted);
  overflow: hidden;
}

.settings-storage__fill {
  height: 100%;
  border-radius: var(--radius-pill);
  background: linear-gradient(90deg, var(--primary-color) 0%, var(--primary-hover) 100%);
  transition: width var(--motion-base, 250ms ease);
}

/* ===== Data Actions ===== */
.settings-data-actions {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  padding: var(--space-4) 0;
  border-bottom: 1px solid var(--border-light);
}

/* ===== Danger Zone ===== */
.settings-danger {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: var(--space-4) 0;
}

.settings-danger__label {
  font-size: var(--fs-text-sm, 0.8125rem);
  font-weight: var(--fs-weight-medium, 500);
  color: var(--danger-color);
}

/* ===== About ===== */
.settings-about {
  padding: var(--space-8) var(--space-6);
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  gap: var(--space-4);
}

.settings-about__logo {
  flex-shrink: 0;
}

.settings-about__info {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
}

.settings-about__name {
  font-family: var(--fs-font-display, inherit);
  font-size: var(--fs-text-xl, 1.375rem);
  font-weight: var(--fs-weight-bold, 700);
  color: var(--text-primary);
}

.settings-about__version {
  font-family: var(--font-family-mono);
  font-size: var(--fs-text-xs, 0.75rem);
  color: var(--text-muted);
}

.settings-about__tagline {
  font-size: var(--fs-text-base, 0.9375rem);
  color: var(--primary-color);
  font-weight: var(--fs-weight-medium, 500);
  letter-spacing: 0.02em;
}

.settings-about__links {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  width: 100%;
  max-width: 280px;
  margin-top: var(--space-2);
}

.settings-about__link {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: var(--space-2);
  padding: var(--space-2) 0;
  border: 1px solid var(--border-color);
  border-radius: var(--radius-md);
  background: transparent;
  color: var(--text-secondary);
  font-size: var(--fs-text-sm, 0.8125rem);
  text-decoration: none;
  transition: background-color var(--motion-fast, 150ms ease), color var(--motion-fast, 150ms ease);
}

.settings-about__link:hover {
  background: var(--bg-hover);
  color: var(--text-primary);
}

.settings-about__license {
  font-size: var(--fs-text-xs, 0.75rem);
  color: var(--text-muted);
}
</style>