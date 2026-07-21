<script setup lang="ts">
import { ref } from 'vue'

const language = ref('zh-CN')
const startupPage = ref('home')
const defaultModel = ref('gemma-2b')
const autoUpdate = ref(true)
const autoStart = ref(false)
</script>

<template>
  <div class="space-y-6">
    <!-- 页头 -->
    <div class="flex items-start justify-between pb-4 border-b border-border">
      <div>
        <h2 class="text-xl font-bold text-text m-0 leading-tight">通用设置</h2>
        <p class="text-sm text-text-regular mt-1 mb-0">配置应用的基本行为与偏好</p>
      </div>
    </div>

    <!-- 设置项列表 -->
    <el-card shadow="never">
      <!-- 语言 -->
      <div class="flex items-center justify-between py-3 border-b border-border-light">
        <div>
          <span class="text-sm font-medium text-text">语言</span>
          <p class="text-xs text-text-secondary mt-0.5 mb-0">界面显示语言</p>
        </div>
        <el-select v-model="language" class="w-[160px]">
          <el-option value="zh-CN" label="简体中文" />
          <el-option value="en" label="English" />
          <el-option value="ja" label="日本語" />
        </el-select>
      </div>

      <!-- 启动时打开 -->
      <div class="flex items-center justify-between py-3 border-b border-border-light">
        <div>
          <span class="text-sm font-medium text-text">启动时打开</span>
          <p class="text-xs text-text-secondary mt-0.5 mb-0">应用启动后显示的默认页面</p>
        </div>
        <el-select v-model="startupPage" class="w-[160px]">
          <el-option value="home" label="首页" />
          <el-option value="plugin-store" label="插件商店" />
          <el-option value="system-monitor" label="系统监控" />
        </el-select>
      </div>

      <!-- 默认 AI 模型 -->
      <div class="flex items-center justify-between py-3 border-b border-border-light">
        <div>
          <span class="text-sm font-medium text-text">默认 AI 模型</span>
          <p class="text-xs text-text-secondary mt-0.5 mb-0">AI Agent 使用的默认推理模型</p>
        </div>
        <el-select v-model="defaultModel" class="w-[160px]">
          <el-option value="gemma-2b" label="Gemma 2B 本地" />
          <el-option value="qwen-7b" label="Qwen 7B 本地" />
          <el-option value="deepseek" label="DeepSeek API" />
        </el-select>
      </div>

      <!-- 数据存储路径 -->
      <div class="flex items-center justify-between py-3 border-b border-border-light">
        <div>
          <span class="text-sm font-medium text-text">数据存储路径</span>
          <p class="text-xs text-text-secondary mt-0.5 mb-0">所有本地数据与缓存的存储位置</p>
        </div>
        <div class="flex items-center gap-2">
          <code class="text-xs font-mono text-text-regular">~/.forgeself/data</code>
          <el-button size="small">更改</el-button>
        </div>
      </div>

      <!-- 自动更新 -->
      <div class="flex items-center justify-between py-3 border-b border-border-light">
        <div>
          <span class="text-sm font-medium text-text">自动更新</span>
          <p class="text-xs text-text-secondary mt-0.5 mb-0">有新版本时自动下载并安装</p>
        </div>
        <el-switch v-model="autoUpdate" />
      </div>

      <!-- 开机自启 -->
      <div class="flex items-center justify-between py-3 last:border-b-0">
        <div>
          <span class="text-sm font-medium text-text">开机自启</span>
          <p class="text-xs text-text-secondary mt-0.5 mb-0">系统启动时自动运行铸己匣</p>
        </div>
        <el-switch v-model="autoStart" />
      </div>
    </el-card>
  </div>
</template>
