<script setup lang="ts">
import { ref, markRaw } from 'vue';
import type { Component } from 'vue';
import {
  Setting,
  Monitor,
  Grid,
  Connection,
  Link,
  Brush,
  Coin,
  InfoFilled,
  Key,
  Refresh
} from '@element-plus/icons-vue';
import PageHead from '@/components/PageHead.vue';
import GeneralPanel from '@/components/settings/GeneralPanel.vue';
import AiAgentPanel from '@/components/settings/AiAgentPanel.vue';
import PluginsPanel from '@/components/settings/PluginsPanel.vue';
import AiProvidersPanel from '@/components/settings/AiProvidersPanel.vue';
import ApiServerPanel from '@/components/settings/ApiServerPanel.vue';
import ApiKeysPanel from '@/components/settings/ApiKeysPanel.vue';
import AppearancePanel from '@/components/settings/AppearancePanel.vue';
import DataStoragePanel from '@/components/settings/DataStoragePanel.vue';
import UpdatePanel from '@/components/settings/UpdatePanel.vue';
import AboutPanel from '@/components/settings/AboutPanel.vue';

type SettingsCategory =
  | 'general'
  | 'ai-agent'
  | 'plugins'
  | 'ai-providers'
  | 'api-server'
  | 'api-keys'
  | 'appearance'
  | 'data'
  | 'update'
  | 'about';

const activeCategory = ref<SettingsCategory>('general');

interface NavItem {
  key: SettingsCategory;
  label: string;
  icon: Component;
}

const navItems: NavItem[] = [
  { key: 'general', label: '通用', icon: markRaw(Setting) },
  { key: 'ai-agent', label: 'AI Agent', icon: markRaw(Monitor) },
  { key: 'plugins', label: '插件管理', icon: markRaw(Grid) },
  { key: 'ai-providers', label: 'AI 提供方', icon: markRaw(Connection) },
  { key: 'api-server', label: 'API 服务器', icon: markRaw(Link) },
  { key: 'api-keys', label: 'API 密钥', icon: markRaw(Key) },
  { key: 'appearance', label: '外观', icon: markRaw(Brush) },
  { key: 'data', label: '数据与存储', icon: markRaw(Coin) },
  { key: 'update', label: '版本更新', icon: markRaw(Refresh) },
  { key: 'about', label: '关于', icon: markRaw(InfoFilled) }
];
</script>

<template>
  <div class="settings-view">
    <!-- ===== 页头（设计稿 PageHead 统一规格） ===== -->
    <PageHead eyebrow="Settings · 通用设置" title="配置应用的基本行为与偏好" />

    <!-- 左右分栏：左分类玻璃栏 + 右内容玻璃卡（用全局 .glass 裸容器，避免 ForgeCard 恒带 role=button 与真实按钮 getByRole 冲突） -->
    <div class="settings-layout">
      <!-- 左侧导航（玻璃卡） -->
      <div class="glass settings-nav-card">
        <nav class="settings-cat-nav" aria-label="设置分类">
          <button
            v-for="item in navItems"
            :key="item.key"
            class="settings-cat-btn w-full flex items-center gap-3 px-3 py-2.5 text-left rounded-r-md border-l-[3px] transition-colors cursor-pointer bg-transparent border-none font-inherit"
            :class="
              activeCategory === item.key
                ? '!border-l-[var(--el-color-primary)] bg-primary/10 text-primary font-medium'
                : '!border-l-transparent text-text-regular hover:bg-fill hover:text-text'
            "
            @click="activeCategory = item.key"
          >
            <el-icon :size="18"><component :is="item.icon" /></el-icon>
            <span class="text-sm truncate">{{ item.label }}</span>
          </button>
        </nav>
      </div>

      <!-- 右侧面板（玻璃卡） -->
      <div class="glass settings-content-card">
        <GeneralPanel v-show="activeCategory === 'general'" />
        <AiAgentPanel v-show="activeCategory === 'ai-agent'" />
        <PluginsPanel v-show="activeCategory === 'plugins'" />
        <AiProvidersPanel v-show="activeCategory === 'ai-providers'" />
        <ApiServerPanel v-show="activeCategory === 'api-server'" />
        <ApiKeysPanel v-show="activeCategory === 'api-keys'" />
        <AppearancePanel v-show="activeCategory === 'appearance'" />
        <DataStoragePanel v-show="activeCategory === 'data'" />
        <UpdatePanel v-show="activeCategory === 'update'" />
        <AboutPanel v-show="activeCategory === 'about'" />
      </div>
    </div>
  </div>
</template>

<style scoped>
.settings-view {
  height: 100%;
  min-height: 0;
  overflow-y: auto;
  padding: 24px;
}

.settings-layout {
  display: grid;
  grid-template-columns: 200px 1fr;
  gap: 24px;
  max-width: 1100px;
  margin: 0 auto;
  align-items: start;
}

/* 左分类栏：贴合内容高度，滚动时吸顶 */
.settings-nav-card {
  position: sticky;
  top: 0;
  padding: 8px;
}

.settings-content-card {
  padding: 20px;
}

.settings-cat-nav {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.settings-cat-btn {
  font-family: inherit;
  font-size: 13px;
  transition:
    background-color var(--dur-fast) var(--ease-out),
    color var(--dur-fast) var(--ease-out),
    transform var(--dur-fast) var(--ease-out);
}

.settings-cat-btn:hover {
  transform: translateY(-1px);
}

/* 窄屏收为单列 */
@media (max-width: 720px) {
  .settings-layout {
    grid-template-columns: 1fr;
  }

  .settings-nav-card {
    position: static;
  }
}
</style>
