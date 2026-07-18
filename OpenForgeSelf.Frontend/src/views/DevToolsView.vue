<script setup lang="ts">
import { useDevToolsStore } from '@/stores/devTools'
import type { DevToolTab } from '@/types/devTools'
import JsonTool from '@/components/devtools/JsonTool.vue'
import YamlTool from '@/components/devtools/YamlTool.vue'
import XmlTool from '@/components/devtools/XmlTool.vue'
import DataConverter from '@/components/devtools/DataConverter.vue'
import EncodingTool from '@/components/devtools/EncodingTool.vue'
import HashTool from '@/components/devtools/HashTool.vue'
import EncryptTool from '@/components/devtools/EncryptTool.vue'
import RegexTester from '@/components/devtools/RegexTester.vue'
import TimestampTool from '@/components/devtools/TimestampTool.vue'
import ColorTool from '@/components/devtools/ColorTool.vue'
import JwtTool from '@/components/devtools/JwtTool.vue'
import UuidTool from '@/components/devtools/UuidTool.vue'
import QrCodeTool from '@/components/devtools/QrCodeTool.vue'

const store = useDevToolsStore()

const tabs: { key: DevToolTab; label: string; icon: string }[] = [
  { key: 'json', label: 'JSON', icon: 'fa-solid fa-braille' },
  { key: 'yaml', label: 'YAML', icon: 'fa-solid fa-list-ul' },
  { key: 'xml', label: 'XML', icon: 'fa-solid fa-code' },
  { key: 'converter', label: '格式转换', icon: 'fa-solid fa-right-left' },
  { key: 'encoding', label: '编码转换', icon: 'fa-solid fa-keyboard' },
  { key: 'hash', label: '哈希计算', icon: 'fa-solid fa-fingerprint' },
  { key: 'encrypt', label: '加密解密', icon: 'fa-solid fa-lock' },
  { key: 'regex', label: '正则测试', icon: 'fa-solid fa-magnifying-glass' },
  { key: 'timestamp', label: '时间戳', icon: 'fa-regular fa-clock' },
  { key: 'color', label: '颜色工具', icon: 'fa-solid fa-palette' },
  { key: 'jwt', label: 'JWT', icon: 'fa-solid fa-key' },
  { key: 'uuid', label: 'UUID/ID', icon: 'fa-solid fa-fingerprint' },
  { key: 'qrcode', label: '二维码', icon: 'fa-solid fa-qrcode' },
]

function handleTabChange(tab: DevToolTab): void {
  store.setTab(tab)
}
</script>

<template>
  <div class="dev-tools-view">
    <header class="view-header">
      <h1 class="view-title">
        <span class="title-icon">🔧</span>
        开发者工具
      </h1>
      <p class="view-subtitle">JSON/YAML/XML、编码转换、哈希计算、加密解密、正则测试、时间戳、颜色工具等常用开发工具</p>
    </header>

    <nav class="tabs-nav">
      <button
        v-for="tab in tabs"
        :key="tab.key"
        class="tab-btn"
        :class="{ active: store.currentTab === tab.key }"
        @click="handleTabChange(tab.key)"
      >
        <i :class="tab.icon" />
        <span>{{ tab.label }}</span>
      </button>
    </nav>

    <div v-if="store.error" class="error-banner" role="alert">
      <span class="error-icon">⚠️</span>
      <span class="error-message">{{ store.error }}</span>
      <button class="error-close" aria-label="关闭错误提示" @click="store.clearError()">
        ✕
      </button>
    </div>

    <div class="tool-content">
      <JsonTool v-if="store.currentTab === 'json'" />
      <YamlTool v-else-if="store.currentTab === 'yaml'" />
      <XmlTool v-else-if="store.currentTab === 'xml'" />
      <DataConverter v-else-if="store.currentTab === 'converter'" />
      <EncodingTool v-else-if="store.currentTab === 'encoding'" />
      <HashTool v-else-if="store.currentTab === 'hash'" />
      <EncryptTool v-else-if="store.currentTab === 'encrypt'" />
      <RegexTester v-else-if="store.currentTab === 'regex'" />
      <TimestampTool v-else-if="store.currentTab === 'timestamp'" />
      <ColorTool v-else-if="store.currentTab === 'color'" />
      <JwtTool v-else-if="store.currentTab === 'jwt'" />
      <UuidTool v-else-if="store.currentTab === 'uuid'" />
      <QrCodeTool v-else-if="store.currentTab === 'qrcode'" />
    </div>
  </div>
</template>

<style scoped>
.dev-tools-view {
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding: 20px;
  height: 100%;
  overflow: hidden;
  box-sizing: border-box;
}

.view-header {
  flex-shrink: 0;
}

.view-title {
  font-size: 24px;
  font-weight: 600;
  color: var(--text-primary);
  margin: 0 0 4px 0;
  display: flex;
  align-items: center;
  gap: 8px;
}

.title-icon {
  font-size: 28px;
}

.view-subtitle {
  font-size: 14px;
  color: var(--text-muted);
  margin: 0;
}

.tabs-nav {
  display: flex;
  gap: 4px;
  padding: 4px;
  background: var(--bg-secondary);
  border-radius: 8px;
  flex-wrap: wrap;
  flex-shrink: 0;
}

.tab-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 16px;
  border: none;
  background: transparent;
  color: var(--text-muted);
  font-size: 14px;
  font-weight: 500;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.tab-btn:hover {
  background: var(--bg-hover);
  color: var(--text-secondary);
}

.tab-btn.active {
  background: var(--primary-color);
  color: var(--primary-contrast);
}

.error-banner {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px 16px;
  background: rgba(185, 28, 28, 0.08);
  border: 1px solid rgba(185, 28, 28, 0.2);
  border-radius: var(--radius-md, 8px);
  color: var(--danger-color);
  flex-shrink: 0;
}

.error-icon {
  font-size: 18px;
}

.error-message {
  flex: 1;
  font-size: 14px;
}

.error-close {
  background: none;
  border: none;
  color: var(--danger-color);
  cursor: pointer;
  font-size: 16px;
  padding: 4px;
  border-radius: 4px;
  transition: background 0.2s;
}

.error-close:hover {
  background: rgba(220, 38, 38, 0.1);
}

.tool-content {
  flex: 1;
  min-height: 0;
  overflow: hidden;
}
</style>