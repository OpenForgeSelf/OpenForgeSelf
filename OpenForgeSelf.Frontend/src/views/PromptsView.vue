<template>
  <div class="prompts-view">
    <!-- ===== Left Sidebar: Prompt Library (240px) ===== -->
    <aside class="sidebar">
      <div class="sidebar-header">
        <span class="sidebar-title">提示词库</span>
        <button class="btn-new-prompts" aria-label="新建提示词">
          <svg
            width="12"
            height="12"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2.5"
            stroke-linecap="round"
            stroke-linejoin="round"
          >
            <line x1="12" y1="5" x2="12" y2="19" />
            <line x1="5" y1="12" x2="19" y2="12" />
          </svg>
          新建提示词
        </button>
      </div>

      <div class="prompt-list">
        <div
          v-for="item in promptList"
          :key="item.id"
          class="prompt-item"
          :class="{ 'prompt-item--active': activeId === item.id }"
          @click="activeId = item.id"
        >
          <div class="prompt-item-dot" :class="{ 'prompt-item-dot--active': activeId === item.id }" />
          <div class="prompt-item-body">
            <div class="prompt-item-name">{{ item.name }}</div>
            <div class="prompt-item-meta">
              <span
                class="prompt-item-tag"
                :class="`prompt-item-tag--${item.level}`"
              >{{ item.levelLabel }}</span>
              <span class="prompt-item-chars">{{ item.chars }}</span>
            </div>
          </div>
        </div>
      </div>
    </aside>

    <!-- ===== Right Content: Prompt Editor ===== -->
    <div class="editor-area">
      <!-- Editor Header -->
      <div class="editor-header">
        <div class="editor-header-left">
          <span class="editor-title">{{ currentPrompt.name }}</span>
          <span
            class="editor-tag"
            :class="`editor-tag--${currentPrompt.level}`"
          >{{ currentPrompt.levelLabel }}</span>
          <span class="editor-chars">{{ currentPrompt.chars }} 字符</span>
        </div>
        <div class="editor-header-right">
          <el-select v-model="selectedRole">
            <el-option v-for="opt in roleOptions" :key="opt.value" :label="opt.label" :value="opt.value" />
          </el-select>
          <el-select v-model="selectedScope">
            <el-option v-for="opt in scopeOptions" :key="opt.value" :label="opt.label" :value="opt.value" />
          </el-select>
        </div>
      </div>

      <!-- Editor Body -->
      <div class="editor-body">
        <div class="editor-code-area">
          <textarea
            v-model="currentContent"
            class="editor-textarea"
            spellcheck="false"
            placeholder="输入提示指令内容..."
          />
        </div>
      </div>

      <!-- Bottom Actions -->
      <div class="editor-footer">
        <div class="editor-footer-left">
          <el-button type="primary" size="small" @click="handleSave">
            <svg
              width="14"
              height="14"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z" />
              <polyline points="17 21 17 13 7 13 7 21" />
              <polyline points="7 3 7 8 15 8" />
            </svg>
            保存
          </el-button>
          <el-button size="small" @click="handleReset">
            <svg
              width="14"
              height="14"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <polyline points="1 4 1 10 7 10" />
              <path d="M3.51 15a9 9 0 1 0 2.13-9.36L1 10" />
            </svg>
            重置
          </el-button>
          <el-button size="small" @click="handleExport">
            <svg
              width="14"
              height="14"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
              <polyline points="7 10 12 15 17 10" />
              <line x1="12" y1="15" x2="12" y2="3" />
            </svg>
            导出
          </el-button>
        </div>
        <span class="editor-last-edited">上次编辑: 2 小时前</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'

interface PromptItem {
  id: string
  name: string
  level: string
  levelLabel: string
  chars: string
  content: string
}

const promptList: PromptItem[] = [
  {
    id: 'default',
    name: '默认系统提示',
    level: 'system',
    levelLabel: '系统级',
    chars: '1,280',
    content: `你是铸己匣 OpenForgeSelf 的 AI 助手。你可以：
1. 调用 50+ 工具函数完成各种任务
2. 规划和执行多步骤工作流
3. 生成 PowerShell/Python/Node.js 脚本
4. 分析系统状态和性能数据
5. 管理文件和目录

回复要求：
- 使用中文回复
- 工具调用使用 JSON 格式
- 提供简洁明了的操作步骤
- 遇到错误时给出修复建议`,
  },
  {
    id: 'code-review',
    name: '代码审查专家',
    level: 'professional',
    levelLabel: '专业级',
    chars: '2,450',
    content: '你是一位资深的代码审查专家。请审查以下代码，关注：\n1. 代码质量和可读性\n2. 潜在的性能问题\n3. 安全漏洞\n4. 最佳实践合规性\n5. 测试覆盖率',
  },
  {
    id: 'data-analysis',
    name: '数据分析助手',
    level: 'professional',
    levelLabel: '专业级',
    chars: '1,860',
    content: '你是一位数据分析专家。请分析提供的数据，输出：\n1. 数据概览和统计摘要\n2. 关键趋势和模式\n3. 异常值检测\n4. 可视化建议',
  },
  {
    id: 'file-organization',
    name: '文件整理规则',
    level: 'task',
    levelLabel: '任务级',
    chars: '920',
    content: '请根据以下规则整理文件：\n1. 按类型分类到对应目录\n2. 命名规范化\n3. 清理临时文件和重复文件\n4. 生成整理报告',
  },
  {
    id: 'daily-report',
    name: '日报生成模板',
    level: 'task',
    levelLabel: '任务级',
    chars: '640',
    content: '请生成今日工作日报，包含：\n1. 今日完成工作\n2. 遇到的问题及解决方案\n3. 明日工作计划\n4. 需要的支持',
  },
  {
    id: 'quick-fix',
    name: '快速修复建议',
    level: 'assist',
    levelLabel: '辅助级',
    chars: '380',
    content: '请快速诊断问题并提供修复步骤：\n1. 问题定位\n2. 根因分析\n3. 修复方案\n4. 验证方法',
  },
]

const activeId = ref('default')
const selectedRole = ref('system')
const selectedScope = ref('global')

const roleOptions = [
  { value: 'system', label: '系统助手' },
  { value: 'user', label: '用户' },
  { value: 'assistant', label: '助手' },
]

const scopeOptions = [
  { value: 'global', label: '全局' },
  { value: 'project', label: '项目' },
  { value: 'session', label: '会话' },
]

const currentPrompt = computed(() => {
  return promptList.find((p) => p.id === activeId.value) ?? promptList[0]
})

const currentContent = ref(currentPrompt.value.content)

watch(activeId, () => {
  currentContent.value = currentPrompt.value.content
})

function handleSave(): void {
  // TODO: implement save
  console.log('保存提示词:', activeId.value, currentContent.value)
}

function handleReset(): void {
  currentContent.value = currentPrompt.value.content
}

function handleExport(): void {
  // TODO: implement export
  console.log('导出提示词:', activeId.value)
}
</script>

<style scoped>
.prompts-view {
  display: flex;
  height: 100%;
  background: var(--bg-primary);
  color: var(--text-primary);
  overflow: hidden;
}

/* ================================
   Sidebar (240px)
   ================================ */
.sidebar {
  width: 240px;
  display: flex;
  flex-direction: column;
  flex-shrink: 0;
  background: var(--bg-secondary);
  border-right: 1px solid var(--border-color);
  overflow: hidden;
}

.sidebar-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  height: 40px;
  padding: 0 12px;
  flex-shrink: 0;
  border-bottom: 1px solid var(--border-color);
}

.sidebar-title {
  font-size: var(--fs-text-sm, 0.8125rem);
  font-weight: 600;
  color: var(--text-primary);
}

.btn-new-prompts {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 2px 8px;
  font-size: var(--fs-text-xs, 0.75rem);
  font-weight: 500;
  color: var(--primary-color);
  background: var(--primary-soft);
  border: 1px solid var(--primary-color);
  border-radius: var(--fs-radius-sm, 4px);
  cursor: pointer;
  transition:
    background var(--fs-transition-fast, 150ms ease),
    opacity var(--fs-transition-fast, 150ms ease);
}

.btn-new-prompts:hover {
  background: var(--primary-light);
}

.btn-new-prompts svg {
  flex-shrink: 0;
}

/* Prompt List */
.prompt-list {
  flex: 1;
  overflow-y: auto;
  padding: 4px 0;
  scrollbar-width: thin;
}

.prompt-item {
  display: flex;
  align-items: flex-start;
  gap: 10px;
  padding: 10px 12px;
  cursor: pointer;
  border-left: 3px solid transparent;
  transition: background var(--motion-fast);
}

.prompt-item:hover {
  background: var(--bg-hover);
}

.prompt-item--active {
  background: var(--primary-soft);
  border-left-color: var(--primary-color);
}

.prompt-item-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: var(--text-muted);
  flex-shrink: 0;
  margin-top: 5px;
}

.prompt-item-dot--active {
  background: var(--primary-color);
}

.prompt-item-body {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.prompt-item-name {
  font-size: var(--fs-text-sm, 0.8125rem);
  font-weight: 500;
  color: var(--text-secondary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.prompt-item--active .prompt-item-name {
  color: var(--primary-color);
}

.prompt-item-meta {
  display: flex;
  align-items: center;
  gap: 6px;
}

.prompt-item-tag {
  display: inline-flex;
  align-items: center;
  font-size: 10px;
  font-weight: 500;
  padding: 0 6px;
  line-height: 1.6;
  border-radius: 999px;
  border: 1px solid;
}

.prompt-item-tag--system {
  color: var(--primary-color);
  background: var(--primary-soft);
  border-color: var(--primary-border);
}

.prompt-item-tag--professional {
  color: var(--info-color);
  background: rgba(88, 166, 255, 0.1);
  border-color: rgba(88, 166, 255, 0.25);
}

.prompt-item-tag--task {
  color: var(--success-color);
  background: rgba(52, 211, 153, 0.1);
  border-color: rgba(52, 211, 153, 0.25);
}

.prompt-item-tag--assist {
  color: var(--text-muted);
  background: var(--bg-tertiary);
  border-color: var(--border-color);
}

.prompt-item-chars {
  font-size: var(--fs-text-xs, 0.75rem);
  color: var(--text-muted);
  font-family: var(--font-family-mono);
}

/* ================================
   Editor Area (flex-1)
   ================================ */
.editor-area {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  background: var(--bg-primary);
}

/* Editor Header */
.editor-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  height: 48px;
  padding: 0 24px;
  flex-shrink: 0;
  border-bottom: 1px solid var(--border-color);
  background: var(--bg-secondary);
}

.editor-header-left {
  display: flex;
  align-items: center;
  gap: 12px;
  min-width: 0;
}

.editor-title {
  font-size: var(--fs-text-base, 0.9375rem);
  font-weight: 600;
  color: var(--text-primary);
  white-space: nowrap;
}

.editor-tag {
  display: inline-flex;
  align-items: center;
  font-size: 10px;
  font-weight: 500;
  padding: 2px 8px;
  line-height: 1.5;
  border-radius: 999px;
  border: 1px solid;
}

.editor-tag--system {
  color: var(--primary-color);
  background: var(--primary-soft);
  border-color: var(--primary-border);
}

.editor-tag--professional {
  color: var(--info-color);
  background: rgba(88, 166, 255, 0.1);
  border-color: rgba(88, 166, 255, 0.25);
}

.editor-tag--task {
  color: var(--success-color);
  background: rgba(52, 211, 153, 0.1);
  border-color: rgba(52, 211, 153, 0.25);
}

.editor-tag--assist {
  color: var(--text-muted);
  background: var(--bg-tertiary);
  border-color: var(--border-color);
}

.editor-chars {
  font-size: var(--fs-text-xs, 0.75rem);
  color: var(--text-muted);
  font-family: var(--font-family-mono);
}

.editor-header-right {
  display: flex;
  align-items: center;
  gap: 8px;
}

/* Editor Body */
.editor-body {
  flex: 1;
  padding: 20px 24px;
  overflow: hidden;
}

.editor-code-area {
  width: 100%;
  height: 100%;
  background: var(--bg-code);
  border: 1px solid var(--border-color);
  border-left: 3px solid var(--primary-color);
  border-radius: var(--fs-radius-md, 8px);
  overflow: hidden;
  box-shadow: inset 0 0 30px rgba(0, 0, 0, 0.15);
}

.editor-textarea {
  width: 100%;
  height: 100%;
  padding: 16px 20px;
  background: transparent;
  border: none;
  outline: none;
  resize: none;
  color: var(--text-primary);
  font-family: var(--font-family-mono);
  font-size: var(--fs-text-sm, 0.8125rem);
  line-height: 1.625;
  tab-size: 4;
}

.editor-textarea::placeholder {
  color: var(--text-muted);
}

/* Editor Footer */
.editor-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  height: 44px;
  padding: 0 24px;
  flex-shrink: 0;
  border-top: 1px solid var(--border-color);
  background: var(--bg-secondary);
}

.editor-footer-left {
  display: flex;
  align-items: center;
  gap: 8px;
}

.editor-last-edited {
  font-size: var(--fs-text-xs, 0.75rem);
  color: var(--text-muted);
  font-family: var(--font-family-mono);
}
</style>
