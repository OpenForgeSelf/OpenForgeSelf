<script setup lang="ts">
/**
 * 设计系统根视图（设计系统生成器 · Design System Studio）。
 *
 * 本插件是**通用设计系统生成器**：Stardust 只是内置的「参考示例」预设，不是插件的身份。
 * 外壳界面（Header / 导航 / 空态）使用中性主题，刻意不带任何品牌性格；
 * 真正有品牌性格的是用户生成、或默认加载的参考示例 Stardust。
 *
 * 数据流：
 * - `activeDs` 是当前「被预览的设计系统」，默认 = STARDUST（参考示例）。
 * - 设计工作台生成后 emit 更新 `activeDs` → 「设计令牌 / 组件库 / 控制台 / 官网」展示页即时换肤为该系统。
 */
import { ref } from 'vue'
import type { DesignSystem } from './design/schema'
import { STARDUST } from './design/presets'
import { tokensToStyleAttr } from './design/tokensToCss'
import BrandLogo from './components/BrandLogo.vue'
import TokenShowcase from './sections/TokenShowcase.vue'
import ComponentGallery from './sections/ComponentGallery.vue'
import ConsoleKit from './sections/console/ConsoleKit.vue'
import MarketingSite from './sections/marketing/MarketingSite.vue'
import DesignStudio from './sections/DesignStudio.vue'

const tabs = [
  { key: 'studio', label: '设计工作台' },
  { key: 'tokens', label: '设计令牌' },
  { key: 'components', label: '组件库' },
  { key: 'console', label: '控制台 UI Kit' },
  { key: 'marketing', label: '官网设计稿' },
] as const

const active = ref<(typeof tabs)[number]['key']>('studio')
/** 当前被预览的设计系统：默认加载参考示例 Stardust。 */
const activeDs = ref<DesignSystem>(STARDUST)
</script>

<template>
  <div class="ds ds-root">
    <header class="ds-header">
      <div class="ds-header__brand">
        <BrandLogo variant="full" :size="34" />
        <div class="ds-header__titles">
          <div class="ds-h3">设计系统生成器</div>
          <div class="ds-small">Design System Studio · 输入需求，生成一套完整设计系统（品牌 / 颜色 / 类型 / 间距 / 组件）</div>
        </div>
      </div>
      <nav class="ds-tabs">
        <button
          v-for="t in tabs"
          :key="t.key"
          class="ds-tab"
          :class="{ 'ds-tab--active': active === t.key }"
          @click="active = t.key"
        >
          {{ t.label }}
        </button>
      </nav>
    </header>

    <main class="ds-main">
      <DesignStudio v-if="active === 'studio'" v-model:active-ds="activeDs" />

      <!-- 展示页：包在换肤容器内，随 activeDs 整体变色。
           默认 activeDs = Stardust（参考示例）；工作台生成后变为用户系统。 -->
      <div v-else class="ds-skin" :style="tokensToStyleAttr(activeDs)">
        <TokenShowcase v-if="active === 'tokens'" />
        <ComponentGallery v-else-if="active === 'components'" />
        <ConsoleKit v-else-if="active === 'console'" />
        <MarketingSite v-else-if="active === 'marketing'" />

        <div class="ds-skin__hint ds-small">
          当前预览主题：<strong>{{ activeDs.meta.name }}</strong>
          <span v-if="activeDs.meta.source === 'preset'">（内置参考示例）</span>
          <span v-else>（由需求「{{ activeDs.meta.brief.slice(0, 24) }}<template v-if="activeDs.meta.brief.length > 24">…</template>」生成）</span>
          · 在「设计工作台」生成新系统可即时换肤
        </div>
      </div>
    </main>

    <footer class="ds-footer ds-small">
      设计系统生成器 · 设计插件（design-system）· 全部 token 见 styles/tokens.css
    </footer>
  </div>
</template>

<style scoped>
.ds-root {
  min-height: 100vh;
  display: flex;
  flex-direction: column;
}
.ds-header {
  position: sticky;
  top: 0;
  z-index: 10;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--ds-space-5);
  flex-wrap: wrap;
  padding: var(--ds-space-4) var(--ds-space-6);
  background: rgba(255, 255, 255, 0.82);
  backdrop-filter: blur(12px);
  border-bottom: 1px solid var(--ds-border-1);
}
.ds-header__brand {
  display: flex;
  align-items: center;
  gap: var(--ds-space-3);
}
.ds-header__titles {
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.ds-tabs {
  display: flex;
  gap: var(--ds-space-1);
  background: var(--ds-surface-2);
  padding: 4px;
  border-radius: var(--ds-radius-pill);
  border: 1px solid var(--ds-border-1);
}
.ds-tab {
  border: none;
  background: transparent;
  color: var(--ds-fg-2);
  font-family: var(--ds-font-sans);
  font-size: var(--ds-fs-small);
  font-weight: var(--ds-fw-medium);
  padding: 8px var(--ds-space-4);
  border-radius: var(--ds-radius-pill);
  cursor: pointer;
  transition: all var(--ds-dur-fast) var(--ds-ease-out-expo);
}
.ds-tab:hover {
  color: var(--ds-fg-1);
}
.ds-tab--active {
  background: var(--ds-surface-1);
  color: var(--ds-color-primary);
  box-shadow: var(--ds-shadow-sm);
}
.ds-main {
  flex: 1;
  padding: var(--ds-space-7) var(--ds-space-6);
  max-width: 1180px;
  width: 100%;
  margin: 0 auto;
}
/* 换肤容器：仅此层重新声明 token，内部所有展示组件随之变色（外壳 Header 不变） */
.ds-skin {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-5);
}
.ds-skin__hint {
  color: var(--ds-fg-3);
  border-top: 1px dashed var(--ds-border-2);
  padding-top: var(--ds-space-4);
}
.ds-footer {
  padding: var(--ds-space-5) var(--ds-space-6);
  text-align: center;
  color: var(--ds-fg-4);
  border-top: 1px solid var(--ds-border-1);
}
</style>
