<script setup lang="ts">
import { computed } from 'vue'
import { Delete, Download } from '@element-plus/icons-vue'

const storageUsed = 128
const storageTotal = 1024
const storagePercent = computed(() => Math.round((storageUsed / storageTotal) * 100))
</script>

<template>
  <div class="space-y-6">
    <!-- 页头 -->
    <div class="flex items-start justify-between pb-4 border-b border-border">
      <div>
        <h2 class="text-xl font-bold text-text m-0 leading-tight">数据与存储</h2>
        <p class="text-sm text-text-regular mt-1 mb-0">管理本地数据、缓存与导出</p>
      </div>
    </div>

    <!-- 存储用量 -->
    <el-card shadow="never">
      <div class="flex items-center justify-between mb-3">
        <span class="text-sm font-medium text-text">使用数据</span>
        <span class="text-sm text-text-secondary">{{ storageUsed }} MB / {{ storageTotal }} GB</span>
      </div>
      <el-progress :percentage="storagePercent" :stroke-width="8" :show-text="false" />
    </el-card>

    <!-- 操作按钮 -->
    <el-card shadow="never">
      <div class="flex items-center justify-between py-3 border-b border-border-light">
        <div>
          <span class="text-sm font-medium text-text">清除缓存</span>
          <p class="text-xs text-text-secondary mt-0.5 mb-0">释放临时文件与缓存占用的空间</p>
        </div>
        <el-button :icon="Delete">清除缓存</el-button>
      </div>
      <div class="flex items-center justify-between py-3 last:border-b-0">
        <div>
          <span class="text-sm font-medium text-text">导出数据</span>
          <p class="text-xs text-text-secondary mt-0.5 mb-0">将所有数据导出为备份文件</p>
        </div>
        <el-button :icon="Download">导出数据</el-button>
      </div>
    </el-card>

    <!-- 危险区域 -->
    <el-card shadow="never" class="!border-error/30">
      <div class="flex items-center justify-between py-1">
        <div>
          <span class="text-sm font-medium text-error">重置所有设置</span>
          <p class="text-xs text-text-secondary mt-0.5 mb-0">恢复为出厂默认值，此操作不可撤销</p>
        </div>
        <el-button type="danger">重置</el-button>
      </div>
    </el-card>
  </div>
</template>
