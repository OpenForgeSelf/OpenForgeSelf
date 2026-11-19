<script setup lang="ts">
/**
 * Sems 扩展屏（交接包 11 屏之外的第 6 个导航项）。
 * 工作台 IA 没有「规格」位置，而规格版本 / 关联 / 验收覆盖是 sems 必须保留的核心能力，
 * 因此以独立一屏完整挂载原 SpecPanel 组件，不改其任何行为。
 */
import { computed } from 'vue'
import { ic } from '../icons'
import { currentProject, go } from '../workbenchStore'
import SpecPanel from '../../SpecPanel.vue'

const p = computed(() => currentProject())
</script>

<template>
  <div class="proj-header">
    <h2>项目规格</h2>
    <div class="proj-tools">
      <a class="link-btn" href="#/dashboard" @click.prevent="go('/dashboard')">
        返回概览 <span v-html="ic('external', 'sm')"></span>
      </a>
    </div>
  </div>

  <div class="proj-canvas">
    <p class="wb-hint">
      「{{ p?.name }}」的规格：只追加版本、手工关联 commit / PR / Issue、按标识反查规格版本、
      验收标准用例登记与覆盖查看。以下为 sems 原有规格面板，行为未改动。
    </p>
    <SpecPanel v-if="p" :project-id="p.id" />
  </div>
</template>
