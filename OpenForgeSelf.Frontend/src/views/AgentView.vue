<template>
  <div class="agent-view">
    <!-- ===== Three-Column Layout ===== -->
    <main class="agent-main-layout">
      <!-- ================================
           LEFT PANEL — AI Context (220px)
           ================================ -->
      <aside class="panel-left">
        <div class="panel-header">
          <span class="panel-title">AI 上下文</span>
          <i class="fa-solid fa-sliders" />
        </div>

        <!-- MCP 工具 -->
        <div class="context-section">
          <button class="section-toggle" @click="toggleSection('mcp')">
            <div class="section-toggle-left">
              <i class="fa-solid fa-wrench section-icon" />
              <span class="section-label">MCP 工具</span>
            </div>
            <span class="section-badge">3</span>
          </button>
          <div v-show="expandedSections.mcp" class="section-items">
            <div class="section-item">
              <span class="item-name mono">get_cpu_usage</span>
            </div>
            <div class="section-item">
              <span class="item-name mono">get_memory_usage</span>
            </div>
            <div class="section-item">
              <span class="item-name mono">get_process_list</span>
            </div>
            <a class="section-manage" href="#">
              <span>管理</span>
              <i class="fa-solid fa-chevron-right" />
            </a>
          </div>
        </div>

        <!-- 技能 -->
        <div class="context-section">
          <button class="section-toggle" @click="toggleSection('skills')">
            <div class="section-toggle-left">
              <i class="fa-solid fa-bolt section-icon" />
              <span class="section-label">技能</span>
            </div>
            <span class="section-badge muted">2</span>
          </button>
          <div v-show="expandedSections.skills" class="section-items">
            <div class="section-item">
              <span class="item-name">系统诊断</span>
            </div>
            <div class="section-item">
              <span class="item-name">脚本生成</span>
            </div>
            <a class="section-manage" href="#">
              <span>管理</span>
              <i class="fa-solid fa-chevron-right" />
            </a>
          </div>
        </div>

        <!-- 提示指令 -->
        <div class="context-section">
          <button class="section-toggle" @click="toggleSection('prompts')">
            <div class="section-toggle-left">
              <i class="fa-solid fa-message section-icon muted-icon" />
              <span class="section-label">提示指令</span>
            </div>
            <span class="section-badge muted">1</span>
          </button>
          <div v-show="expandedSections.prompts" class="section-items">
            <div class="section-item">
              <span class="item-name">默认系统提示</span>
            </div>
            <a class="section-manage" href="#">
              <span>管理</span>
              <i class="fa-solid fa-chevron-right" />
            </a>
          </div>
        </div>

        <!-- 记忆 -->
        <div class="context-section">
          <button class="section-toggle" @click="toggleSection('memory')">
            <div class="section-toggle-left">
              <i class="fa-solid fa-brain section-icon muted-icon" />
              <span class="section-label">记忆</span>
            </div>
            <span class="section-badge muted">2</span>
          </button>
          <div v-show="expandedSections.memory" class="section-items">
            <div class="section-item">
              <span class="item-name">用户偏好</span>
            </div>
            <div class="section-item">
              <span class="item-name">工具使用习惯</span>
            </div>
            <a class="section-manage" href="#">
              <span>管理</span>
              <i class="fa-solid fa-chevron-right" />
            </a>
          </div>
        </div>
      </aside>

      <!-- ================================
           CENTER — Chat Area (flex-1)
           ================================ -->
      <section class="panel-center">
        <!-- Chat Header -->
        <div class="chat-header">
          <div class="chat-header-left">
            <span class="chat-title">AI Agent</span>
            <div class="status-indicator">
              <div class="status-dot" />
              <span class="status-text">就绪</span>
            </div>
          </div>
          <div class="chat-header-right">
            <div class="model-selector">
              <i class="fa-solid fa-microchip" />
              <span class="model-name">Gemma 2B 本地</span>
              <i class="fa-solid fa-chevron-down" />
            </div>
            <span class="token-count">
              <i class="fa-solid fa-hashtag" />
              1.2k tokens
            </span>
          </div>
        </div>

        <!-- Message List -->
        <div ref="messageListRef" class="message-list">
          <div class="message-container">
            <!-- AI Welcome Message -->
            <div class="message-row ai">
              <div class="ai-avatar">
                <div class="ai-dot" />
              </div>
              <div class="message-content">
                <div class="message-bubble ai-bubble">
                  <p>你好！我是铸己匣的 AI Agent。我可以调用 50+ 工具函数、规划工作流、生成脚本。有什么需要帮忙的吗？</p>
                </div>
              </div>
            </div>

            <!-- User Message 1 -->
            <div class="message-row user">
              <div class="message-content user-content">
                <div class="message-bubble user-bubble">
                  <p>帮我检查一下系统当前的 CPU 和内存使用情况</p>
                </div>
              </div>
              <div class="user-avatar">
                <i class="fa-solid fa-user" />
              </div>
            </div>

            <!-- AI Response with tool call badges -->
            <div class="message-row ai">
              <div class="ai-avatar">
                <div class="ai-dot" />
              </div>
              <div class="message-content">
                <div class="tool-badges">
                  <span class="tool-badge">
                    <i class="fa-solid fa-terminal" />
                    get_cpu_usage
                  </span>
                  <span class="tool-badge">
                    <i class="fa-solid fa-terminal" />
                    get_memory_usage
                  </span>
                </div>
                <div class="message-bubble ai-bubble">
                  <p>当前系统状态：CPU 使用率 <span class="highlight">42%</span>，内存使用 <span class="highlight">8.2GB/16GB (51%)</span>。系统运行正常，无异常进程。</p>
                </div>
              </div>
            </div>

            <!-- User Message 2 -->
            <div class="message-row user">
              <div class="message-content user-content">
                <div class="message-bubble user-bubble">
                  <p>把最近 30 天使用最多的 5 个工具列出来</p>
                </div>
              </div>
              <div class="user-avatar">
                <i class="fa-solid fa-user" />
              </div>
            </div>

            <!-- AI Response 3 -->
            <div class="message-row ai">
              <div class="ai-avatar">
                <div class="ai-dot" />
              </div>
              <div class="message-content">
                <div class="message-bubble ai-bubble">
                  <p>根据你的使用记录，最近 30 天最常用的工具是：</p>
                  <ol class="tool-list">
                    <li><span class="mono">JSON 格式化</span> <span class="count">(47次)</span></li>
                    <li><span class="mono">Base64 编解码</span> <span class="count">(32次)</span></li>
                    <li><span class="mono">文件批量重命名</span> <span class="count">(28次)</span></li>
                    <li><span class="mono">系统进程查看</span> <span class="count">(19次)</span></li>
                    <li><span class="mono">正则测试</span> <span class="count">(15次)</span></li>
                  </ol>
                </div>
              </div>
            </div>

            <!-- User Message 3 -->
            <div class="message-row user">
              <div class="message-content user-content">
                <div class="message-bubble user-bubble">
                  <p>帮我生成一个 Python 脚本，批量重命名 D 盘的图片文件</p>
                </div>
              </div>
              <div class="user-avatar">
                <i class="fa-solid fa-user" />
              </div>
            </div>

            <!-- AI Response 4 -->
            <div class="message-row ai">
              <div class="ai-avatar">
                <div class="ai-dot" />
              </div>
              <div class="message-content">
                <div class="tool-badges">
                  <span class="tool-badge">
                    <i class="fa-solid fa-terminal" />
                    generate_script
                  </span>
                </div>
                <div class="message-bubble ai-bubble">
                  <p>已为你生成批量重命名脚本。脚本将扫描 D 盘指定目录下的所有图片文件，按日期_序号格式重命名。是否需要我直接执行？</p>
                </div>
              </div>
            </div>

            <!-- Streaming state -->
            <div v-if="chatStore.isStreaming" class="message-row ai">
              <div class="ai-avatar">
                <div class="ai-dot" />
              </div>
              <div class="message-content">
                <div class="message-bubble ai-bubble streaming">
                  <span class="streaming-dots">
                    <span class="dot" />
                    <span class="dot" />
                    <span class="dot" />
                  </span>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Input Area -->
        <div class="input-area">
          <div class="input-inner">
            <button class="input-btn" aria-label="附件">
              <i class="fa-solid fa-paperclip" />
            </button>
            <div class="input-wrapper">
              <textarea
                v-model="inputMessage"
                placeholder="输入消息..."
                rows="2"
                @keydown.enter.exact.prevent="handleSend"
              />
            </div>
            <button class="input-btn" aria-label="语音输入">
              <i class="fa-solid fa-microphone" />
            </button>
            <button class="send-btn" aria-label="发送" @click="handleSend">
              <i class="fa-solid fa-paper-plane" />
            </button>
          </div>
        </div>
      </section>

      <!-- ================================
           RIGHT PANEL — Session & Stats (260px)
           ================================ -->
      <aside class="panel-right">
        <!-- 当前会话 -->
        <div class="right-section">
          <div class="right-section-header">
            <i class="fa-solid fa-message" />
            <span>当前会话</span>
          </div>
          <div class="session-name-row">
            <span class="session-name">系统诊断-0626</span>
            <button class="icon-btn" aria-label="重命名会话">
              <i class="fa-solid fa-pencil" />
            </button>
          </div>
          <div class="stats-rows">
            <div class="stat-row">
              <span class="stat-label">消息数</span>
              <span class="stat-value mono">6</span>
            </div>
            <div class="stat-row">
              <span class="stat-label">工具调用</span>
              <span class="stat-value mono">4 次</span>
            </div>
            <div class="stat-row">
              <span class="stat-label">Token 使用</span>
              <span class="stat-value mono">1.2k / 8k</span>
            </div>
          </div>
        </div>

        <!-- 能力画像 -->
        <div class="right-section">
          <div class="right-section-header">
            <i class="fa-solid fa-chart-simple" />
            <span>能力画像</span>
          </div>
          <div class="skill-bars">
            <div class="skill-bar-item">
              <div class="skill-bar-header">
                <span class="skill-name">工具调用</span>
                <span class="skill-level primary">L3 精通</span>
              </div>
              <div class="progress-track">
                <div class="progress-fill primary-fill" style="width: 75%" />
              </div>
            </div>
            <div class="skill-bar-item">
              <div class="skill-bar-header">
                <span class="skill-name">脚本生成</span>
                <span class="skill-level">L2 进阶</span>
              </div>
              <div class="progress-track">
                <div class="progress-fill" style="width: 45%" />
              </div>
            </div>
            <div class="skill-bar-item">
              <div class="skill-bar-header">
                <span class="skill-name">工作流规划</span>
                <span class="skill-level">L2 进阶</span>
              </div>
              <div class="progress-track">
                <div class="progress-fill" style="width: 40%" />
              </div>
            </div>
            <div class="skill-bar-item">
              <div class="skill-bar-header">
                <span class="skill-name">系统操作</span>
                <span class="skill-level primary">L3 精通</span>
              </div>
              <div class="progress-track">
                <div class="progress-fill primary-fill" style="width: 70%" />
              </div>
            </div>
          </div>
        </div>

        <!-- Agent 列表 -->
        <div class="right-section">
          <div class="right-section-header">
            <i class="fa-solid fa-robot" />
            <span>Agent 列表</span>
          </div>
          <div class="agent-list">
            <div class="agent-item active">
              <div class="agent-dot active-dot" />
              <span class="agent-name-text active-text">默认助手</span>
              <span class="agent-current">当前</span>
            </div>
            <div class="agent-item">
              <div class="agent-dot" />
              <span class="agent-name-text">系统诊断专家</span>
            </div>
            <div class="agent-item">
              <div class="agent-dot" />
              <span class="agent-name-text">代码助手</span>
            </div>
            <button class="create-agent-btn">
              <i class="fa-solid fa-plus" />
              <span>创建新 Agent</span>
            </button>
          </div>
        </div>
      </aside>
    </main>

    <!-- ===== Legacy Modal: Coordination ===== -->
    <div v-if="showCoordination" class="modal-overlay" @click.self="showCoordination = false">
      <div class="modal modal-large">
        <div class="modal-header">
          <h3>
            <i class="fa-solid fa-users" />
            多 Agent 协作
          </h3>
          <button class="btn-close" @click="showCoordination = false">
            <i class="fa-solid fa-xmark" />
          </button>
        </div>
        <div class="modal-body">
          <div class="coordination-input">
            <label>描述你的任务</label>
            <textarea
              v-model="coordinationRequest"
              placeholder="描述你想要完成的任务，系统会自动分配合适的 Agent..."
              rows="4"
            />
          </div>

          <div class="coordination-actions">
            <button
              class="btn btn-primary"
              :disabled="!coordinationRequest.trim() || agentStore.executing"
              @click="executeCoordination"
            >
              <i v-if="agentStore.executing" class="fa-solid fa-spinner fa-spin" />
              <i v-else class="fa-solid fa-play" />
              {{ agentStore.executing ? '执行中...' : '开始协作' }}
            </button>
          </div>

          <div v-if="agentStore.currentPlan" class="plan-section">
            <h4>
              <i class="fa-solid fa-list-check" />
              执行计划
            </h4>
            <div class="plan-info">
              <p><strong>策略：</strong>{{ agentStore.currentPlan.strategy }}</p>
              <p><strong>推理：</strong>{{ agentStore.currentPlan.coordinatorReasoning }}</p>
            </div>
            <div class="task-list">
              <div
                v-for="(task, index) in agentStore.currentPlan.tasks"
                :key="task.taskId"
                class="task-item"
              >
                <div class="task-number">{{ index + 1 }}</div>
                <div class="task-content">
                  <div class="task-header">
                    <span class="task-name">{{ task.description }}</span>
                    <span class="task-agent">
                      <i :class="getTypeIcon(task.assignedAgentType)" />
                      {{ agentStore.getAgentTypeLabel(task.assignedAgentType) }}
                    </span>
                  </div>
                  <div class="task-priority">
                    优先级: {{ agentStore.getTaskPriorityLabel(task.priority) }}
                  </div>
                </div>
              </div>
            </div>
          </div>

          <div v-if="coordinationResult" class="result-section">
            <h4>
              <i class="fa-solid fa-check-circle" />
              执行结果
            </h4>
            <div class="result-content markdown-body" v-html="formatResult(coordinationResult)" />
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, reactive } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAgentStore } from '@/stores/agent'
import { useChatStore } from '@/stores/chat'
import type { AgentType } from '@/types/agent'

const agentStore = useAgentStore()
const chatStore = useChatStore()
const route = useRoute()
const router = useRouter()

const showCoordination = ref(false)
const coordinationRequest = ref('')
const coordinationResult = ref('')
const inputMessage = ref('')
const messageListRef = ref<HTMLElement | null>(null)

const expandedSections = reactive({
  mcp: true,
  skills: true,
  prompts: true,
  memory: true,
})

function toggleSection(key: keyof typeof expandedSections) {
  expandedSections[key] = !expandedSections[key]
}

onMounted(() => {
  // 预填首页快速提问条携带的问题
  const q = route.query.q
  if (typeof q === 'string' && q.trim()) {
    inputMessage.value = q.trim()
    // 清除 query 参数，避免刷新重复预填
    router.replace({ path: '/ai-agent' })
  }

  agentStore.loadAgents()
  agentStore.loadInstances()
})

function getTypeIcon(type: AgentType | string): string {
  return agentStore.getAgentTypeIcon(type as AgentType)
}

function handleSend() {
  if (!inputMessage.value.trim()) return
  chatStore.sendMessage(inputMessage.value.trim())
  inputMessage.value = ''
}

async function executeCoordination() {
  if (!coordinationRequest.value.trim()) return
  try {
    coordinationResult.value = ''
    const result = await agentStore.handleRequest(coordinationRequest.value)
    coordinationResult.value = result
  } catch (e) {
    console.error('协作执行失败:', e)
  }
}

function formatResult(content: string): string {
  return content
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/\n/g, '<br>')
    .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
    .replace(/### (.+?)(<br>|$)/g, '<h3>$1</h3>')
    .replace(/## (.+?)(<br>|$)/g, '<h2>$1</h2>')
    .replace(/- (.+?)(<br>|$)/g, '<li>$1</li>')
}
</script>

<style scoped>
/* ============================================
   Agent View — Three-Column Layout
   Uses design tokens from main.css
   ============================================ */

.agent-view {
  height: 100%;
  display: flex;
  flex-direction: column;
  background: var(--bg-primary);
  color: var(--text-primary);
}

.agent-main-layout {
  display: flex;
  flex: 1;
  overflow: hidden;
}

/* ================================
   Left Panel (220px)
   ================================ */
.panel-left {
  width: 220px;
  display: flex;
  flex-direction: column;
  flex-shrink: 0;
  overflow-y: auto;
  background: var(--bg-secondary);
  border-right: 1px solid var(--border-color);
}

.panel-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 12px;
  border-bottom: 1px solid var(--border-color);
  flex-shrink: 0;
}

.panel-title {
  font-size: var(--fs-text-sm, 0.8125rem);
  font-weight: 600;
  color: var(--text-primary);
}

.panel-header i {
  font-size: 12px;
  color: var(--text-muted);
}

.context-section {
  display: flex;
  flex-direction: column;
  border-bottom: 1px solid var(--border-light);
}

.section-toggle {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 12px;
  width: 100%;
  text-align: left;
  background: transparent;
  border: none;
  cursor: pointer;
  transition: background var(--motion-fast);
}

.section-toggle:hover {
  background: var(--bg-hover);
}

.section-toggle-left {
  display: flex;
  align-items: center;
  gap: 8px;
}

.section-icon {
  font-size: 11px;
  color: var(--primary-color);
  width: 14px;
}

.section-icon.muted-icon {
  color: var(--text-muted);
}

.section-label {
  font-size: var(--fs-text-sm, 0.8125rem);
  font-weight: 500;
  color: var(--text-primary);
}

.section-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 11px;
  font-weight: 600;
  color: var(--primary-color);
  background: var(--primary-soft);
  min-width: 18px;
  height: 18px;
  border-radius: var(--radius-pill);
  padding: 0 5px;
}

.section-badge.muted {
  color: var(--text-muted);
  background: var(--bg-tertiary);
}

.section-items {
  display: flex;
  flex-direction: column;
  padding: 0 12px 8px;
  gap: 2px;
}

.section-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 4px 8px;
  border-left: 2px solid transparent;
  border-radius: 0 var(--radius-sm) var(--radius-sm) 0;
}

.section-item:first-child {
  border-left-color: var(--primary-color);
  background: var(--primary-soft);
}

.item-name {
  font-size: 12px;
  color: var(--text-secondary);
}

.item-name.mono {
  font-family: var(--font-family-mono);
}

.section-manage {
  display: flex;
  align-items: center;
  gap: 4px;
  margin-top: 4px;
  font-size: 12px;
  color: var(--info-color);
  text-decoration: none;
  padding: 2px 8px;
  transition: color var(--motion-fast);
}

.section-manage i {
  font-size: 10px;
}

/* ================================
   Center Panel (Chat Area)
   ================================ */
.panel-center {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-width: 0;
}

.chat-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  height: 44px;
  padding: 0 16px;
  background: var(--bg-secondary);
  border-bottom: 1px solid var(--border-color);
  flex-shrink: 0;
}

.chat-header-left {
  display: flex;
  align-items: center;
  gap: 10px;
}

.chat-title {
  font-size: var(--fs-text-base, 0.9375rem);
  font-weight: 600;
  color: var(--text-primary);
}

.status-indicator {
  display: flex;
  align-items: center;
  gap: 6px;
}

.status-dot {
  width: 6px;
  height: 6px;
  border-radius: var(--radius-pill);
  background: var(--success-color);
  box-shadow: 0 0 6px rgba(52, 211, 153, 0.4);
}

.status-text {
  font-size: 12px;
  color: var(--success-color);
}

.chat-header-right {
  display: flex;
  align-items: center;
  gap: 12px;
}

.model-selector {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 4px 8px;
  border: 1px solid var(--border-color);
  border-radius: var(--radius-sm);
  background: var(--bg-tertiary);
  cursor: pointer;
  transition: border-color var(--motion-fast);
}

.model-selector i {
  font-size: 11px;
  color: var(--primary-color);
}

.model-selector .fa-chevron-down {
  color: var(--text-muted);
}

.model-name {
  font-size: 12px;
  color: var(--text-secondary);
  font-family: var(--font-family-mono);
}

.token-count {
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: 12px;
  color: var(--text-muted);
  font-family: var(--font-family-mono);
}

.token-count i {
  font-size: 10px;
}

/* Message List */
.message-list {
  flex: 1;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  scrollbar-width: thin;
}

.message-container {
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding: 16px 24px;
  max-width: 820px;
  width: 100%;
  margin: 0 auto;
}

.message-row {
  display: flex;
  gap: 12px;
}

.message-row.ai {
  align-items: flex-start;
}

.message-row.user {
  justify-content: flex-end;
}

.ai-avatar {
  display: flex;
  align-items: flex-start;
  justify-content: center;
  width: 32px;
  height: 32px;
  border-radius: var(--radius-sm);
  background: var(--bg-tertiary);
  border: 2px solid var(--primary-color);
  padding: 4px;
  flex-shrink: 0;
}

.ai-dot {
  width: 8px;
  height: 8px;
  border-radius: var(--radius-pill);
  background: var(--primary-color);
  margin-top: 2px;
}

.user-avatar {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  border-radius: var(--radius-sm);
  background: var(--primary-soft);
  flex-shrink: 0;
}

.user-avatar i {
  font-size: 14px;
  color: var(--primary-color);
}

.message-content {
  display: flex;
  flex-direction: column;
  gap: 6px;
  min-width: 0;
  flex: 1;
}

.message-content.user-content {
  flex: 0 1 auto;
  max-width: 85%;
}

.message-bubble {
  padding: 10px 14px;
  border-radius: var(--radius-sm) var(--radius-sm) var(--radius-md) var(--radius-sm);
  line-height: 1.625;
}

.ai-bubble {
  background: var(--bg-secondary);
  border: 1px solid var(--border-light);
  border-left: 2px solid var(--primary-color);
  max-width: 85%;
}

.ai-bubble p {
  font-size: var(--fs-text-sm, 0.8125rem);
  color: var(--text-primary);
  line-height: 1.625;
}

.ai-bubble .highlight {
  font-family: var(--font-family-mono);
  color: var(--primary-color);
  font-weight: 600;
}

.user-bubble {
  background: var(--bg-tertiary);
  border: 1px solid var(--border-color);
}

.user-bubble p {
  font-size: var(--fs-text-sm, 0.8125rem);
  color: var(--text-primary);
  line-height: 1.5;
}

/* Tool call badges */
.tool-badges {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
}

.tool-badge {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 2px 8px;
  font-size: 11px;
  color: var(--primary-color);
  background: var(--primary-soft);
  border-radius: var(--radius-pill);
  font-family: var(--font-family-mono);
  font-weight: 500;
}

.tool-badge i {
  font-size: 9px;
}

/* Tool list in AI bubble */
.tool-list {
  margin: 8px 0 0 0;
  padding-left: 20px;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.tool-list li {
  font-size: var(--fs-text-sm, 0.8125rem);
  color: var(--text-primary);
  line-height: 1.5;
}

.tool-list li .mono {
  font-family: var(--font-family-mono);
  color: var(--primary-color);
  font-weight: 500;
}

.tool-list li .count {
  color: var(--text-muted);
}

/* Streaming dots */
.streaming {
  display: flex;
  align-items: center;
  min-height: 36px;
}

.streaming-dots {
  display: flex;
  gap: 4px;
  align-items: center;
}

.streaming-dots .dot {
  width: 6px;
  height: 6px;
  border-radius: var(--radius-pill);
  background: var(--text-muted);
  animation: pulse 1.4s infinite ease-in-out;
}

.streaming-dots .dot:nth-child(2) {
  animation-delay: 0.2s;
}

.streaming-dots .dot:nth-child(3) {
  animation-delay: 0.4s;
}

@keyframes pulse {
  0%, 80%, 100% { opacity: 0.3; transform: scale(0.8); }
  40% { opacity: 1; transform: scale(1.2); }
}

/* Input Area */
.input-area {
  flex-shrink: 0;
  padding: 12px 24px;
  border-top: 1px solid var(--border-color);
  background: var(--bg-secondary);
}

.input-inner {
  display: flex;
  align-items: flex-end;
  gap: 8px;
  max-width: 820px;
  width: 100%;
  margin: 0 auto;
}

.input-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 34px;
  height: 34px;
  border-radius: var(--radius-sm);
  background: transparent;
  border: 1px solid var(--border-color);
  color: var(--text-muted);
  cursor: pointer;
  flex-shrink: 0;
  transition: border-color var(--motion-fast), color var(--motion-fast);
}

.input-btn:hover {
  border-color: var(--border-strong);
  color: var(--text-secondary);
}

.input-btn i {
  font-size: 14px;
}

.input-wrapper {
  flex: 1;
  background: var(--bg-tertiary);
  border: 1px solid var(--border-color);
  border-radius: var(--radius-md);
  padding: 8px 12px;
  transition: border-color var(--motion-fast);
}

.input-wrapper:focus-within {
  border-color: var(--primary-color);
}

.input-wrapper textarea {
  width: 100%;
  background: transparent;
  border: none;
  outline: none;
  resize: none;
  font-size: var(--fs-text-sm, 0.8125rem);
  color: var(--text-primary);
  font-family: var(--font-family-base);
  line-height: 1.5;
}

.send-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 34px;
  height: 34px;
  border-radius: var(--radius-sm);
  background: var(--primary-color);
  border: none;
  color: var(--primary-contrast);
  cursor: pointer;
  flex-shrink: 0;
  transition: background var(--motion-fast);
}

.send-btn:hover {
  background: var(--primary-hover);
}

.send-btn i {
  font-size: 14px;
}

/* ================================
   Right Panel (260px)
   ================================ */
.panel-right {
  width: 260px;
  display: flex;
  flex-direction: column;
  flex-shrink: 0;
  overflow-y: auto;
  background: var(--bg-secondary);
  border-left: 1px solid var(--border-color);
  scrollbar-width: thin;
}

.right-section {
  display: flex;
  flex-direction: column;
  padding: 12px;
  gap: 10px;
  border-bottom: 1px solid var(--border-color);
}

.right-section-header {
  display: flex;
  align-items: center;
  gap: 6px;
}

.right-section-header i {
  font-size: 12px;
  color: var(--primary-color);
  width: 14px;
}

.right-section-header span {
  font-size: var(--fs-text-sm, 0.8125rem);
  font-weight: 600;
  color: var(--text-primary);
}

.session-name-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 6px 10px;
  background: var(--bg-tertiary);
  border-radius: var(--radius-sm);
}

.session-name {
  font-size: var(--fs-text-sm, 0.8125rem);
  color: var(--text-primary);
  font-weight: 500;
}

.icon-btn {
  background: transparent;
  border: none;
  cursor: pointer;
  color: var(--text-muted);
  padding: 2px;
  display: flex;
  align-items: center;
}

.icon-btn i {
  font-size: 11px;
}

.stats-rows {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.stat-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 10px;
}

.stat-label {
  font-size: 12px;
  color: var(--text-muted);
}

.stat-value {
  font-size: 12px;
  color: var(--text-secondary);
  font-weight: 500;
}

.stat-value.mono {
  font-family: var(--font-family-mono);
}

/* Skill bars */
.skill-bars {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.skill-bar-item {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.skill-bar-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.skill-name {
  font-size: 12px;
  color: var(--text-secondary);
}

.skill-level {
  font-size: 12px;
  color: var(--text-muted);
  font-weight: 500;
}

.skill-level.primary {
  color: var(--primary-color);
}

.progress-track {
  height: 4px;
  background: var(--bg-tertiary);
  border-radius: var(--radius-pill);
  overflow: hidden;
}

.progress-fill {
  height: 100%;
  background: var(--text-muted);
  border-radius: var(--radius-pill);
}

.progress-fill.primary-fill {
  background: var(--primary-color);
}

/* Agent list */
.agent-list {
  display: flex;
  flex-direction: column;
  gap: 1px;
}

.agent-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 10px;
  cursor: pointer;
  border-left: 2px solid transparent;
  border-radius: 0 var(--radius-sm) var(--radius-sm) 0;
  transition: background var(--motion-fast);
}

.agent-item:hover {
  background: var(--bg-hover);
}

.agent-item.active {
  background: var(--primary-soft);
  border-left-color: var(--primary-color);
}

.agent-dot {
  width: 6px;
  height: 6px;
  border-radius: var(--radius-pill);
  background: var(--text-muted);
  flex-shrink: 0;
}

.agent-dot.active-dot {
  background: var(--primary-color);
}

.agent-name-text {
  font-size: var(--fs-text-sm, 0.8125rem);
  color: var(--text-secondary);
  flex: 1;
}

.agent-item.active .agent-name-text {
  color: var(--primary-color);
  font-weight: 500;
}

.agent-current {
  font-size: 11px;
  color: var(--primary-color);
  opacity: 0.7;
}

.create-agent-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  padding: 8px 10px;
  margin-top: 4px;
  background: transparent;
  border: 1px dashed var(--border-color);
  border-radius: var(--radius-sm);
  color: var(--text-muted);
  cursor: pointer;
  transition: border-color var(--motion-fast), color var(--motion-fast);
}

.create-agent-btn:hover {
  border-color: var(--primary-color);
  color: var(--primary-color);
}

.create-agent-btn i {
  font-size: 12px;
}

.create-agent-btn span {
  font-size: 12px;
  font-weight: 500;
}

/* ================================
   Modal (Legacy)
   ================================ */
.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  padding: 20px;
}

.modal {
  background: var(--bg-card);
  border-radius: 16px;
  max-height: 90vh;
  display: flex;
  flex-direction: column;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.15);
  border: 1px solid var(--border-color);
}

.modal-large {
  width: 100%;
  max-width: 700px;
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 20px 24px;
  border-bottom: 1px solid var(--border-color);
}

.modal-header h3 {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
  display: flex;
  align-items: center;
  gap: 10px;
  color: var(--text-primary);
}

.btn-close {
  background: none;
  border: none;
  font-size: 20px;
  color: var(--text-muted);
  cursor: pointer;
  padding: 4px 8px;
  border-radius: 6px;
}

.btn-close:hover {
  background: var(--bg-hover);
  color: var(--text-secondary);
}

.modal-body {
  padding: 24px;
  overflow-y: auto;
  flex: 1;
}

.coordination-input {
  margin-bottom: 20px;
}

.coordination-input label {
  display: block;
  font-size: 14px;
  font-weight: 500;
  margin-bottom: 8px;
  color: var(--text-secondary);
}

.coordination-input textarea {
  width: 100%;
  padding: 12px;
  border: 1px solid var(--border-color);
  border-radius: 10px;
  font-size: 14px;
  font-family: inherit;
  resize: vertical;
  min-height: 100px;
  transition: all 0.2s;
  background: var(--bg-tertiary);
  color: var(--text-primary);
}

.coordination-input textarea:focus {
  outline: none;
  border-color: var(--primary-color);
  box-shadow: 0 0 0 3px var(--primary-soft);
}

.coordination-actions {
  margin-bottom: 24px;
}

.btn {
  padding: 10px 16px;
  border-radius: 10px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  border: none;
  display: flex;
  align-items: center;
  gap: 8px;
  transition: all 0.2s;
}

.btn-primary {
  background: var(--primary-color);
  color: var(--primary-contrast);
}

.btn-primary:hover {
  background: var(--primary-hover);
}

.btn-primary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.plan-section,
.result-section {
  margin-top: 24px;
  padding-top: 24px;
  border-top: 1px solid var(--border-color);
}

.plan-section h4,
.result-section h4 {
  margin: 0 0 16px 0;
  font-size: 16px;
  font-weight: 600;
  color: var(--text-primary);
  display: flex;
  align-items: center;
  gap: 10px;
}

.plan-info {
  background: var(--bg-tertiary);
  padding: 16px;
  border-radius: 12px;
  margin-bottom: 16px;
}

.plan-info p {
  margin: 0 0 8px 0;
  font-size: 13px;
  color: var(--text-secondary);
}

.plan-info p:last-child {
  margin-bottom: 0;
}

.task-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.task-item {
  display: flex;
  gap: 14px;
  padding: 14px;
  background: var(--bg-tertiary);
  border-radius: 12px;
}

.task-number {
  width: 28px;
  height: 28px;
  border-radius: 50%;
  background: var(--primary-color);
  color: var(--primary-contrast);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 13px;
  font-weight: 600;
  flex-shrink: 0;
}

.task-content {
  flex: 1;
  min-width: 0;
}

.task-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 6px;
  gap: 12px;
}

.task-name {
  font-size: 14px;
  font-weight: 500;
  color: var(--text-primary);
}

.task-agent {
  font-size: 12px;
  color: var(--primary-color);
  display: flex;
  align-items: center;
  gap: 6px;
  white-space: nowrap;
}

.task-priority {
  font-size: 12px;
  color: var(--text-muted);
}

.result-content {
  background: var(--bg-tertiary);
  padding: 20px;
  border-radius: 12px;
  font-size: 14px;
  line-height: 1.6;
  color: var(--text-secondary);
  max-height: 400px;
  overflow-y: auto;
}

.result-content :deep(h2) {
  font-size: 18px;
  margin-top: 20px;
  margin-bottom: 12px;
}

.result-content :deep(h3) {
  font-size: 16px;
  margin-top: 16px;
  margin-bottom: 8px;
}

.result-content :deep(p) {
  margin-bottom: 12px;
}

.result-content :deep(code) {
  background: var(--bg-muted);
  padding: 2px 6px;
  border-radius: 4px;
  font-size: 13px;
}

.result-content :deep(pre) {
  background: var(--bg-code);
  color: var(--text-primary);
  padding: 16px;
  border-radius: 8px;
  overflow-x: auto;
}

.result-content :deep(pre code) {
  background: none;
  padding: 0;
  color: inherit;
}
</style>
