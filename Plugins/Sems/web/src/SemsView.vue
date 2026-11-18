<script setup lang="ts">
/**
 * sems 插件界面入口（v1.3.0 重构）。
 *
 * - 默认「项目工作台」：按交接包 project-workbench-replica-handoff 的 11 屏 / 6 流程
 *   信息架构重构，数据全部来自 sems 真实 API。
 * - 「经典视图」：原 SemsView 原样保留（LegacySemsView.vue），一键切回，
 *   保证项目卡片 / 命令维护 / 运行面板 / 规格面板的原始交互 100% 可达。
 * - 两个视图共用同一后端、同一 localStorage 元数据键，切换不丢状态。
 */
import { onMounted } from 'vue'
import { viewMode, setViewMode, loadProjects } from './workbench/workbenchStore'
import WorkbenchView from './workbench/WorkbenchView.vue'
import LegacySemsView from './LegacySemsView.vue'
import './workbench/workbench.css'

onMounted(() => { void loadProjects() })
</script>

<template>
  <div class="sems-root">
    <div class="sems-mode-bar">
      <span class="sems-mode-title">Sems · 软件工程管理系统</span>
      <div class="sems-mode-tabs">
        <button :class="{ active: viewMode.value === 'workbench' }" @click="setViewMode('workbench')">
          项目工作台
        </button>
        <button :class="{ active: viewMode.value === 'legacy' }" @click="setViewMode('legacy')">
          经典视图
        </button>
      </div>
      <span class="sems-mode-note">经典视图 = 重构前的完整原界面</span>
    </div>

    <WorkbenchView v-if="viewMode.value === 'workbench'" />
    <LegacySemsView v-else />
  </div>
</template>

<style scoped>
.sems-root { min-height: 100vh; }
.sems-mode-bar {
  display: flex; align-items: center; gap: 12px; flex-wrap: wrap;
  padding: 6px 16px;
  border-bottom: 1px solid var(--el-border-color);
  background: var(--el-bg-color);
  font-size: 12px;
  color: var(--el-text-color-secondary);
}
.sems-mode-title { font-weight: 600; color: var(--el-text-color-primary); }
.sems-mode-tabs { display: inline-flex; gap: 4px; }
.sems-mode-tabs button {
  padding: 3px 12px; border: 1px solid var(--el-border-color);
  border-radius: var(--radius-sm); background: transparent; cursor: pointer;
  color: var(--el-text-color-regular); font-size: 12px;
}
.sems-mode-tabs button.active {
  border-color: var(--el-color-primary);
  color: var(--el-color-primary); font-weight: 600;
}
.sems-mode-note { margin-left: auto; }
@media (max-width: 640px) { .sems-mode-note { display: none; } }
</style>
