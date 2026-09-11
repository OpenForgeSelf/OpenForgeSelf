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
  Key
} from '@element-plus/icons-vue';
import GeneralPanel from '@/components/settings/GeneralPanel.vue';
import AiAgentPanel from '@/components/settings/AiAgentPanel.vue';
import PluginsPanel from '@/components/settings/PluginsPanel.vue';
import AiProvidersPanel from '@/components/settings/AiProvidersPanel.vue';
import ApiServerPanel from '@/components/settings/ApiServerPanel.vue';
import ApiKeysPanel from '@/components/settings/ApiKeysPanel.vue';
import AppearancePanel from '@/components/settings/AppearancePanel.vue';
import DataStoragePanel from '@/components/settings/DataStoragePanel.vue';
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
  { key: 'about', label: '关于', icon: markRaw(InfoFilled) }
];
</script>

<template>
  <div class="p-6 h-full min-h-0 overflow-y-auto">
    <!-- 统一半透明面板：导航 + 内容在同一 surface 上，模仿 VS Code 设置页整体感 -->
    <div class="settings-surface flex gap-6 max-w-[900px] mx-auto rounded-xl p-4">
      <!-- 左侧导航 -->
      <nav class="settings-cat-nav w-[180px] shrink-0 space-y-1" aria-label="设置分类">
        <button
          v-for="item in navItems"
          :key="item.key"
          class="w-full flex items-center gap-3 px-3 py-2.5 text-left rounded-r-md border-l-[3px] transition-colors cursor-pointer bg-transparent border-none font-inherit"
          :class="
            activeCategory === item.key
              ? '!border-l-[var(--el-color-primary)] bg-primary/10 text-primary font-medium'
              : '!border-l-transparent text-text-regular hover:bg-fill hover:text-text'
          "
          @click="activeCategory = item.key">
          <el-icon :size="18"><component :is="item.icon" /></el-icon>
          <span class="text-sm truncate">{{ item.label }}</span>
        </button>
      </nav>

      <!-- 右侧面板 -->
      <div class="flex-1 min-w-0">
        <GeneralPanel v-show="activeCategory === 'general'" />
        <AiAgentPanel v-show="activeCategory === 'ai-agent'" />
        <PluginsPanel v-show="activeCategory === 'plugins'" />
        <AiProvidersPanel v-show="activeCategory === 'ai-providers'" />
        <ApiServerPanel v-show="activeCategory === 'api-server'" />
        <ApiKeysPanel v-show="activeCategory === 'api-keys'" />
        <AppearancePanel v-show="activeCategory === 'appearance'" />
        <DataStoragePanel v-show="activeCategory === 'data'" />
        <AboutPanel v-show="activeCategory === 'about'" />
      </div>
    </div>
  </div>
</template>
