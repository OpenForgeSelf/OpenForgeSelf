<script setup lang="ts">
import { ref, computed } from 'vue'
import type { PluginInfo } from '@/types/plugin'
import { PluginState } from '@/types/plugin'
import { usePluginStore } from '@/stores/plugin'

const props = defineProps<{
  plugin: PluginInfo
  usageCount?: number
}>()

const emit = defineEmits<{
  (e: 'click', plugin: PluginInfo): void
  (e: 'toggle', plugin: PluginInfo): void
}>()

const pluginStore = usePluginStore()
const isToggling = ref(false)

const stateLabel = computed(() => {
  switch (props.plugin.state) {
    case PluginState.Running:
      return '已启用'
    case PluginState.Stopped:
      return '已禁用'
    case PluginState.Error:
      return '错误'
    case PluginState.Loaded:
      return '已安装'
    default:
      return '未知'
  }
})

const stateClass = computed(() => {
  return `state-${props.plugin.state}`
})

function handleCardClick(): void {
  emit('click', props.plugin)
}

async function handleToggleClick(event: Event): Promise<void> {
  event.stopPropagation()
  if (isToggling.value) return

  isToggling.value = true
  try {
    if (props.plugin.isEnabled) {
      await pluginStore.disablePlugin(props.plugin.id)
    } else {
      await pluginStore.enablePlugin(props.plugin.id)
    }
    emit('toggle', props.plugin)
  } catch (e) {
    console.error('切换插件状态失败:', e)
  } finally {
    isToggling.value = false
  }
}

function handleKeydown(event: KeyboardEvent): void {
  if (event.key === 'Enter' || event.key === ' ') {
    event.preventDefault()
    handleCardClick()
  }
}
</script>

<template>
  <div
    class="plugin-card"
    :class="{ disabled: !plugin.isEnabled, error: plugin.state === PluginState.Error }"
    role="button"
    tabindex="0"
    :aria-label="`插件 ${plugin.name}，版本 ${plugin.version}，状态 ${stateLabel}`"
    @click="handleCardClick"
    @keydown="handleKeydown"
  >
    <div class="card-header">
      <div class="plugin-icon">
        <span v-if="plugin.iconUrl">
          <img :src="plugin.iconUrl" :alt="plugin.name" />
        </span>
        <span v-else class="icon-fallback">📦</span>
      </div>
      <div class="plugin-basic">
        <h3 class="plugin-name">{{ plugin.name }}</h3>
        <span class="plugin-version">v{{ plugin.version }}</span>
      </div>
      <div class="plugin-state" :class="stateClass">
        <span class="state-dot" />
        <span class="state-text">{{ stateLabel }}</span>
      </div>
    </div>

    <div class="card-body">
      <p class="plugin-description">{{ plugin.description }}</p>
    </div>

    <div class="card-footer">
      <div class="plugin-meta">
        <span class="plugin-author">作者: {{ plugin.author }}</span>
        <span class="plugin-category">{{ plugin.category }}</span>
        <span v-if="usageCount !== undefined" class="plugin-usage">
          使用 {{ usageCount }} 次
        </span>
      </div>
      <button
        class="toggle-switch"
        :class="{ active: plugin.isEnabled, loading: isToggling }"
        :disabled="isToggling || plugin.state === PluginState.Error"
        :aria-label="plugin.isEnabled ? '禁用插件' : '启用插件'"
        role="switch"
        :aria-checked="plugin.isEnabled"
        @click="handleToggleClick"
      >
        <span class="toggle-slider">
          <span class="toggle-knob" />
        </span>
      </button>
    </div>
  </div>
</template>

<style scoped>
.plugin-card {
  background: #fff;
  border: 1px solid #e9ecef;
  border-radius: 12px;
  padding: 16px;
  cursor: pointer;
  transition: all 0.25s ease;
  display: flex;
  flex-direction: column;
  gap: 12px;
  outline: none;
}

.plugin-card:hover {
  border-color: #1976d2;
  box-shadow: 0 4px 12px rgba(25, 118, 210, 0.12);
  transform: translateY(-2px);
}

.plugin-card:focus-visible {
  border-color: #1976d2;
  box-shadow: 0 0 0 3px rgba(25, 118, 210, 0.2);
}

.plugin-card.disabled {
  opacity: 0.7;
}

.plugin-card.error {
  border-color: #dc3545;
}

.card-header {
  display: flex;
  align-items: flex-start;
  gap: 12px;
}

.plugin-icon {
  width: 48px;
  height: 48px;
  border-radius: 10px;
  background: #f0f7ff;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  overflow: hidden;
}

.plugin-icon img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.icon-fallback {
  font-size: 24px;
}

.plugin-basic {
  flex: 1;
  min-width: 0;
}

.plugin-name {
  font-size: 15px;
  font-weight: 600;
  color: #212529;
  margin: 0 0 4px 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.plugin-version {
  font-size: 12px;
  color: #6c757d;
}

.plugin-state {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  flex-shrink: 0;
}

.state-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #6c757d;
}

.state-enabled .state-dot {
  background: #28a745;
}

.state-disabled .state-dot {
  background: #6c757d;
}

.state-error .state-dot {
  background: #dc3545;
}

.state-installed .state-dot {
  background: #17a2b8;
}

.state-text {
  color: #6c757d;
}

.state-enabled .state-text {
  color: #28a745;
}

.state-error .state-text {
  color: #dc3545;
}

.card-body {
  flex: 1;
}

.plugin-description {
  font-size: 13px;
  color: #495057;
  line-height: 1.5;
  margin: 0;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.card-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding-top: 12px;
  border-top: 1px solid #f1f3f5;
}

.plugin-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  font-size: 12px;
  color: #6c757d;
}

.plugin-author {
  color: #495057;
}

.plugin-category {
  background: #f1f3f5;
  padding: 2px 8px;
  border-radius: 10px;
  font-size: 11px;
}

.plugin-usage {
  color: #6c757d;
}

.toggle-switch {
  background: none;
  border: none;
  padding: 0;
  cursor: pointer;
  flex-shrink: 0;
}

.toggle-switch:disabled {
  cursor: not-allowed;
  opacity: 0.5;
}

.toggle-slider {
  display: block;
  width: 44px;
  height: 24px;
  background: #dee2e6;
  border-radius: 12px;
  position: relative;
  transition: background-color 0.2s ease;
}

.toggle-switch.active .toggle-slider {
  background: #28a745;
}

.toggle-knob {
  position: absolute;
  top: 2px;
  left: 2px;
  width: 20px;
  height: 20px;
  background: #fff;
  border-radius: 50%;
  transition: transform 0.2s ease;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.2);
}

.toggle-switch.active .toggle-knob {
  transform: translateX(20px);
}

.toggle-switch.loading .toggle-knob {
  animation: toggle-loading 0.8s ease-in-out infinite;
}

@keyframes toggle-loading {
  0%, 100% {
    transform: translateX(0);
  }
  50% {
    transform: translateX(10px);
  }
}

.toggle-switch.active.loading .toggle-knob {
  animation: toggle-loading-active 0.8s ease-in-out infinite;
}

@keyframes toggle-loading-active {
  0%, 100% {
    transform: translateX(20px);
  }
  50% {
    transform: translateX(10px);
  }
}

@media (max-width: 768px) {
  .plugin-card {
    padding: 12px;
  }

  .plugin-icon {
    width: 40px;
    height: 40px;
  }

  .icon-fallback {
    font-size: 20px;
  }

  .plugin-name {
    font-size: 14px;
  }
}
</style>
