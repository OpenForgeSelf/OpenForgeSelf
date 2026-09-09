<template>
  <!-- 中栏聊天区：min-h-0（配合父级 agent__main 的 min-h-0）是滚动修复关键——
       否则长回复会把 chat 撑破容器、消息列表不自滚、composer 底部被裁 -->
  <section class="chat flex min-h-0 flex-1 flex-col">
    <!-- 顶部条：折叠按钮 + 标题 + 状态 + token 计数
         （模型选择已按设计 §6 移入 composer 操作栏右侧，发送前显式可见） -->
    <header class="chat__bar">
      <div class="chat__bar-left">
        <button
          type="button"
          class="chat__fold"
          :title="leftCollapsed ? '展开左栏' : '收起左栏'"
          @click="emit('toggle-left')"
        >
          <!-- 收起/展开随栏状态自动切换：箭头语义暧昧，改用 EP Fold/Expand，点开/收起意图一目了然 -->
          <component :is="leftCollapsed ? Expand : Fold" class="chat__fold-ico" />
        </button>
        <span class="chat__title">
          AI Agent
          <!-- 插件版本徽标：用户要求「标题后显示插件版本」；
               设计原型标题旁无此元素，故作为追加元素挂在这里，既满足需求又不破坏原型布局。 -->
          <span v-if="version" class="chat__version">v{{ version }}</span>
        </span>
        <span class="chat__status">
          <span class="chat__dot" :class="{ 'chat__dot--busy': sending }"></span>
          {{ sending ? '思考中' : '就绪' }}
        </span>
      </div>

      <div class="chat__bar-right">
        <button
          type="button"
          class="chat__fold"
          :title="rightCollapsed ? '展开右栏' : '收起右栏'"
          @click="emit('toggle-right')"
        >
          <component :is="rightCollapsed ? Expand : Fold" class="chat__fold-ico" />
        </button>
      </div>
    </header>

    <!-- 消息列表（ElScrollbar 接管滚动：零自研滚动条，布局 flex-1 + min-h-0 后 wrap 内部滚动） -->
    <ElScrollbar ref="listEl" class="chat__list min-h-0 flex-1">
      <!-- TransitionGroup：每条消息进入时淡入+上滑（B1 动效），流出不影响布局 -->
      <TransitionGroup name="msg" tag="div" class="chat__list-inner">
        <!-- 无消息时的引导语（对应设计原型的 AI 欢迎消息） -->
        <div v-if="messages.length === 0" key="welcome" class="chat__msg">
          <div class="chat__avatar chat__avatar--ai">
            <span class="chat__avatar-emoji">🤖</span>
          </div>
          <div class="chat__bubble-wrap">
            <div class="chat__bubble chat__bubble--ai">
              你好！我是铸己匣的 AI Agent。在下方选择目录、工具、技能与模型后开始对话。有什么需要帮忙的吗？
            </div>
          </div>
        </div>

        <div
          v-for="msg in messages"
          :key="msg.id"
          class="chat__msg"
          :class="{ 'chat__msg--user': isUser(msg.role) }"
        >
          <!-- AI 消息：头像在左 -->
          <template v-if="!isUser(msg.role)">
            <div class="chat__avatar chat__avatar--ai">
              <span class="chat__avatar-emoji">🤖</span>
            </div>
            <div class="chat__bubble-wrap">
              <!-- 空占位（流式等待首个 token/工具事件）：显示打字指示 -->
              <div
                v-if="!msg.content && !(msg.toolEvents && msg.toolEvents.length)"
                class="chat__bubble chat__bubble--ai chat__bubble--typing"
              >…</div>
              <template v-else>
                <!-- 工具调用卡片（流式实时收集，可折叠；执行中默认展开） -->
                <div v-if="msg.toolEvents && msg.toolEvents.length" class="chat__tools">
                  <details
                    v-for="(t, ti) in msg.toolEvents"
                    :key="ti"
                    class="chat__tool-card"
                    :open="t.pending === true"
                  >
                    <summary class="chat__tool-summary">
                      <svg class="chat__tool-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M4 17l6-6-6-6M12 19h8" />
                      </svg>
                      <span class="chat__tool-name">{{ t.name }}</span>
                      <span class="chat__tool-status" :class="toolStatusClass(t)">{{ toolStatusText(t) }}</span>
                    </summary>
                    <div class="chat__tool-body">
                      <pre v-if="t.args" class="chat__tool-pre"><span class="chat__tool-label">参数</span>{{ t.args }}</pre>
                      <pre v-if="t.result" class="chat__tool-pre"><span class="chat__tool-label">结果</span>{{ t.result }}</pre>
                    </div>
                  </details>
                </div>
                <!-- 模型输出带 Markdown/LaTeX，渲染后展示。v-html 安全性由 renderMarkdown 保证：
                     先 HTML 转义、再套固定模板标签，链接仅放行 http/https/mailto（见 markdown.ts）。 -->
                <div
                  v-if="msg.content"
                  class="chat__bubble chat__bubble--ai md"
                  v-html="renderMarkdown(msg.content)"
                ></div>
              </template>
            </div>
          </template>

          <!-- 用户消息：头像在右 -->
          <template v-else>
            <div class="chat__bubble chat__bubble--user">{{ msg.content }}</div>
            <div class="chat__avatar chat__avatar--user">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
                <circle cx="12" cy="7" r="4" />
              </svg>
            </div>
          </template>
        </div>
      </TransitionGroup>
    </ElScrollbar>

    <!-- 错误提示 -->
    <p v-if="error" class="chat__error">{{ error }}</p>

    <!-- Composer：chips + textarea + 操作栏（📁 目录 / 🔧 工具 / ⚡ 技能 / 🤖 Agent / 💠 模型 / ➤ 发送） -->
    <footer class="chat__composer-wrap">
      <div class="chat__composer">
        <!-- chips 区：已选上下文（所见即所发）；ElTag closable 取代自研胶囊 -->
        <div v-if="hasChips" class="chat__chips">
          <ElTag
            v-if="projectDir"
            class="chat__chip chat__chip--dir"
            size="small"
            closable
            @close="emit('clear-directory')"
          >
            <Folder :size="12" class="chat__chip-ic" /><span class="chat__chip-txt">{{ dirLabel }}</span>
          </ElTag>
          <ElTag
            v-for="t in selectedToolNames"
            :key="t"
            class="chat__chip"
            size="small"
            closable
            @close="removeTool(t)"
          >
            <Tools :size="12" class="chat__chip-ic" /><span class="chat__chip-txt">{{ t }}</span>
          </ElTag>
          <ElTag
            v-for="s in selectedSkillIds"
            :key="s"
            class="chat__chip"
            size="small"
            closable
            @close="removeSkill(s)"
          >
            <Lightning :size="12" class="chat__chip-ic" /><span class="chat__chip-txt">{{ skillLabel(s) }}</span>
          </ElTag>
          <ElTag
            v-if="activeAgentId && activeAgentId !== DEFAULT_AGENT"
            class="chat__chip chat__chip--agent"
            size="small"
            closable
            @close="resetAgent"
          >
            <Cpu :size="12" class="chat__chip-ic" /><span class="chat__chip-txt">{{ agentLabel }}</span>
          </ElTag>
        </div>

        <textarea
          ref="taEl"
          v-model="draft"
          class="chat__textarea"
          rows="2"
          placeholder="输入消息…（Enter 发送 / Shift+Enter 换行）"
          :disabled="sending"
          @input="autoGrow"
          @keydown.enter.exact.prevent="submit"
        ></textarea>

        <!-- 操作栏：左 = 上下文选择；右 = 模型 + 发送（ElButton 取代自研工具条按钮） -->
        <div class="chat__actionbar">
          <!-- B2：popover 打开时入口加 --open 高亮态（可见"正在展开哪个"） -->
          <ElButton
            class="chat__abtn"
            :class="{ 'chat__abtn--sel': !!projectDir, 'chat__abtn--open': activePop === 'dir' }"
            text
            size="small"
            @click="togglePop('dir')"
          >
            <Folder :size="14" class="chat__abtn-ico" />目录
          </ElButton>
          <ElButton
            class="chat__abtn"
            :class="{ 'chat__abtn--sel': selectedToolNames.length > 0, 'chat__abtn--open': activePop === 'tools' }"
            text
            size="small"
            @click="togglePop('tools')"
          >
            <Tools :size="14" class="chat__abtn-ico" />工具
            <span v-if="selectedToolNames.length > 0" class="chat__abtn-n">{{ selectedToolNames.length }}</span>
          </ElButton>
          <ElButton
            class="chat__abtn"
            :class="{ 'chat__abtn--sel': selectedSkillIds.length > 0, 'chat__abtn--open': activePop === 'skills' }"
            text
            size="small"
            @click="togglePop('skills')"
          >
            <Lightning :size="14" class="chat__abtn-ico" />技能
            <span v-if="selectedSkillIds.length > 0" class="chat__abtn-n">{{ selectedSkillIds.length }}</span>
          </ElButton>
          <ElButton
            class="chat__abtn"
            :class="{ 'chat__abtn--sel': activeAgentId && activeAgentId !== DEFAULT_AGENT, 'chat__abtn--open': activePop === 'agents' }"
            text
            size="small"
            @click="togglePop('agents')"
          >
            <Cpu :size="14" class="chat__abtn-ico" />Agent
          </ElButton>
          <span class="chat__ab-spacer"></span>
          <button
            type="button"
            class="chat__model-pick"
            :class="{ 'chat__model-pick--open': activePop === 'models' }"
            @click="togglePop('models')"
          >
            <Coin :size="14" class="chat__abtn-ico" />{{ currentModelLabel }}<span class="chat__model-caret">▾</span>
          </button>
          <ElButton
            class="chat__send"
            :class="{ 'chat__send--stop': sending }"
            circle
            :disabled="!sending && !draft.trim()"
            :aria-label="sending ? '停止' : '发送'"
            @click="onSendClick"
          >
            <Promotion v-if="!sending" :size="16" />
            <VideoPause v-else :size="16" />
          </ElButton>
        </div>

        <!-- 📁 目录 popover -->
        <div v-if="activePop === 'dir'" class="chat__pop">
          <div class="chat__pop-head"><span class="chat__pop-title">选择项目目录</span></div>
          <div class="chat__pop-dir">
            <div class="chat__pop-dir-row">
              <input
                v-model="dirInput"
                class="chat__pop-input"
                placeholder="输入工作目录绝对路径"
                spellcheck="false"
                @keydown.enter="applyDir()"
              />
              <button type="button" class="chat__pop-go" :disabled="!dirInput.trim()" @click="applyDir()">加载</button>
            </div>
            <div v-if="recentDirs.length > 0" class="chat__pop-recent">
              <div class="chat__pop-recent-t">最近使用</div>
              <button
                v-for="d in recentDirs"
                :key="d"
                type="button"
                class="chat__pop-recent-item"
                :title="d"
                @click="applyDir(d)"
              >{{ d }}</button>
            </div>
            <p class="chat__pop-hint">目录 = 项目工作区根，文件工具（读/写/列目录）以此为根。会话级持久。</p>
          </div>
        </div>

        <!-- 🔧 工具 popover（多选；未勾选 = 后端默认全挂，勾选 = 仅启用所选） -->
        <div v-else-if="activePop === 'tools'" class="chat__pop">
          <div class="chat__pop-head">
            <span class="chat__pop-title">本会话启用工具</span>
            <input v-model="toolSearch" class="chat__pop-search" placeholder="搜索工具…" />
          </div>
          <div class="chat__pop-body">
            <button
              v-for="t in filteredTools"
              :key="t.name"
              type="button"
              class="chat__opt"
              :class="{ 'chat__opt--on': isToolSelected(t.name ?? '') }"
              @click="toggleTool(t.name ?? '')"
            >
              <span class="chat__opt-box">✓</span>
              <span class="chat__opt-name">{{ t.name }}</span>
              <span class="chat__opt-desc">{{ t.description }}</span>
            </button>
            <p v-if="filteredTools.length === 0" class="chat__pop-hint">未找到匹配工具</p>
          </div>
          <div class="chat__pop-foot">未勾选 = 启用默认全部（{{ tools.length }}）；勾选 = 仅启用所选</div>
        </div>

        <!-- ⚡ 技能 popover（多选；注入 system prompt） -->
        <div v-else-if="activePop === 'skills'" class="chat__pop">
          <div class="chat__pop-head">
            <span class="chat__pop-title">注入技能</span>
            <input v-model="skillSearch" class="chat__pop-search" placeholder="搜索技能…" />
          </div>
          <div class="chat__pop-body">
            <button
              v-for="s in filteredSkills"
              :key="s.id"
              type="button"
              class="chat__opt"
              :class="{ 'chat__opt--on': isSkillSelected(s.id ?? '') }"
              @click="toggleSkill(s.id ?? '')"
            >
              <span class="chat__opt-box">✓</span>
              <span class="chat__opt-name">{{ s.name }}</span>
              <span class="chat__opt-desc">{{ s.source }}</span>
            </button>
            <p v-if="filteredSkills.length === 0" class="chat__pop-hint">未识别到技能（选择工作目录后自动识别）</p>
          </div>
          <div class="chat__pop-foot">选中技能的提示词随消息注入 system prompt</div>
        </div>

        <!-- 🤖 Agent popover（单选） -->
        <div v-else-if="activePop === 'agents'" class="chat__pop">
          <div class="chat__pop-head"><span class="chat__pop-title">切换 Agent</span></div>
          <div class="chat__pop-body">
            <button
              v-for="a in agents"
              :key="a.id"
              type="button"
              class="chat__opt"
              :class="{ 'chat__opt--on': activeAgentId === a.id }"
              @click="selectAgent(a.id ?? '')"
            >
              <span class="chat__opt-box chat__opt-box--radio">{{ activeAgentId === a.id ? '●' : '' }}</span>
              <span class="chat__opt-name">{{ a.name }}</span>
              <span class="chat__opt-desc">{{ a.description }}</span>
            </button>
            <p v-if="agents.length === 0" class="chat__pop-hint">暂无可用 Agent</p>
          </div>
          <div class="chat__pop-foot">Agent 决定系统提示词与行为画像</div>
        </div>

        <!-- 模型 popover（单选；右侧对齐发送按钮） -->
        <div v-else-if="activePop === 'models'" class="chat__pop chat__pop--right">
          <div class="chat__pop-head">
            <span class="chat__pop-title">模型</span>
            <input v-model="modelSearch" class="chat__pop-search" placeholder="搜索模型…" />
          </div>
          <div class="chat__pop-body">
            <button
              v-for="m in filteredModels"
              :key="m.chatModelId"
              type="button"
              class="chat__opt"
              :class="{ 'chat__opt--on': selectedModelId === m.chatModelId }"
              @click="selectModel(m.chatModelId ?? '')"
            >
              <span class="chat__opt-box chat__opt-box--radio">{{ selectedModelId === m.chatModelId ? '●' : '' }}</span>
              <span class="chat__opt-name">{{ modelLabel(m) }}</span>
              <span v-if="m.providerName" class="chat__opt-desc">{{ m.providerName }}</span>
            </button>
            <p v-if="filteredModels.length === 0" class="chat__pop-hint">暂无可用模型</p>
          </div>
          <div class="chat__pop-foot">选择将持久化，跨会话记忆</div>
        </div>
      </div>
    </footer>
  </section>
</template>

<script setup lang="ts">
/**
 * 中栏「完整聊天区」（对应设计原型 flex-1 主区）。
 *
 * 说明：本组件只负责展示与交互，消息的**发送与历史加载由父组件持有状态并驱动**，
 * 这样右栏才能拿到真实的消息数做统计（避免两处状态不一致）。
 *
 * Stage 3 重构（composer）：输入区改为 composer 卡片——chips（已选上下文，所见即所发）+
 * textarea + 操作栏（📁 目录 / 🔧 工具 / ⚡ 技能 / 🤖 Agent / 💠 模型 / ➤ 发送）。
 * 选中状态上移至父组件（AiAgentView），本组件经 props/emit 双向同步；目录与模型、Agent
 * 会话级持久（父组件 localStorage），工具/技能随消息发出后清空。
 */
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
// 插件是独立预编译产物，宿主 unplugin-vue-components 不处理其模板，EP 组件必须**显式 import**；
// 运行时经 import map 解析到宿主共享桥（public/shared/element-plus.js）取同一份实例。
// 组件样式由 index.ts 自备（element-plus 各组件 style 入口）。
import { ElButton, ElScrollbar, ElTag } from 'element-plus'
// EP 图标：经 import map 解析到宿主共享桥（public/shared/element-plus-icons.js）取同一份实例。
import { Coin, Cpu, Expand, Fold, Folder, Lightning, Promotion, Tools, VideoPause } from '@element-plus/icons-vue'
import { renderMarkdown } from '../markdown'
import type { AgentDefinition, AgentTool, AIModel, ChatMessage, ProjectSkillItem, ToolEvent } from '../types'

const props = defineProps<{
  /** 消息列表（由父组件持有）。 */
  messages: ChatMessage[]
  /** 可用模型列表。 */
  models: AIModel[]
  /** 当前选中的模型 id。 */
  selectedModelId: string
  /** 是否正在发送。 */
  sending: boolean
  /** 发送/加载的错误信息。 */
  error: string
  /** 插件版本号，展示在标题后（用于确认「宿主加载的是哪个版本的插件界面」）。 */
  version: string
  /** 左栏（会话列表）是否折叠——折叠时顶栏按钮显示「展开」反之「收起」。 */
  leftCollapsed: boolean
  /** 右栏（AI 上下文）是否折叠。 */
  rightCollapsed: boolean
  /** composer 🔧 可选工具（父层按后端白名单 pluginId 过滤后传入）。 */
  tools: AgentTool[]
  /** composer ⚡ 可选技能（项目技能或全局技能）。 */
  skills: ProjectSkillItem[]
  /** composer 🤖 可选 Agent。 */
  agents: AgentDefinition[]
  /** 当前激活 Agent id。 */
  activeAgentId: string
  /** 当前项目工作目录（📁 chip 显示用；空 = 未选）。 */
  projectDir: string
  /** 本会话已选工具名（🔧 多选）。 */
  selectedToolNames: string[]
  /** 本会话已选技能 id（⚡ 多选）。 */
  selectedSkillIds: string[]
}>()

const emit = defineEmits<{
  /** 切换模型。 */
  (e: 'update:selectedModelId', value: string): void
  /** 切换 Agent。 */
  (e: 'update:activeAgentId', value: string): void
  /** 更新已选工具名集合。 */
  (e: 'update:selectedToolNames', value: string[]): void
  /** 更新已选技能 id 集合。 */
  (e: 'update:selectedSkillIds', value: string[]): void
  /** 发送一条消息。 */
  (e: 'send', value: string): void
  /** 停止当前流式回复（发送按钮发送中变 ■）。 */
  (e: 'stop'): void
  /** 加载工作目录（📁 popover）。 */
  (e: 'select-directory', path: string): void
  /** 移除目录 chip。 */
  (e: 'clear-directory'): void
  /** 折叠左栏。 */
  (e: 'toggle-left'): void
  /** 折叠右栏。 */
  (e: 'toggle-right'): void
}>()

/** 默认 Agent id（恢复默认时不显示 chip）。 */
const DEFAULT_AGENT = 'agent.generalist'

/** 最近使用目录的 localStorage 键。 */
const RECENT_KEY = 'forgeself-agent-recent-dirs'

/** 输入框草稿。 */
const draft = ref('')
/** textarea 元素（自动增高用）。 */
const taEl = ref<HTMLTextAreaElement | null>(null)
/** 消息列表容器，用于自动滚到底。 */
const listEl = ref<HTMLElement | null>(null)

/** 当前打开的 popover 类型（'' = 关闭）。 */
const activePop = ref<'dir' | 'tools' | 'skills' | 'agents' | 'models' | ''>('')
/** 📁 popover 目录输入。 */
const dirInput = ref('')
/** 最近使用目录（localStorage 持久化，最多 5 条）。 */
const recentDirs = ref<string[]>(loadRecentDirs())
/** popover 搜索词。 */
const toolSearch = ref('')
const skillSearch = ref('')
const modelSearch = ref('')

/** 判断是否为用户消息（后端角色字段大小写不固定，统一小写比较）。 */
function isUser(role: string): boolean {
  return (role ?? '').toLowerCase() === 'user'
}

/** 模型显示名：别名优先，其次上游模型 ID，最后聊天模型 ID。 */
function modelLabel(m: AIModel): string {
  return m.alias || m.upstreamModelId || m.chatModelId || '未命名模型'
}

/** 工具事件状态文案。 */
function toolStatusText(t: ToolEvent): string {
  if (t.pending) return '执行中'
  return t.success === false ? '失败' : '成功'
}

/** 工具事件状态样式类。 */
function toolStatusClass(t: ToolEvent): string {
  if (t.pending) return 'chat__tool-status--pending'
  return t.success === false ? 'chat__tool-status--fail' : 'chat__tool-status--ok'
}

/* ---- composer 选择逻辑（状态上移：选择集合由父组件持有，本组件经 emit 更新） ---- */

/** 打开/关闭 popover（再次点击当前按钮关闭）。 */
function togglePop(name: 'dir' | 'tools' | 'skills' | 'agents' | 'models') {
  activePop.value = activePop.value === name ? '' : name
}

/** 点击外部 / Esc 关闭 popover。 */
function onDocClick(e: MouseEvent) {
  const t = e.target as HTMLElement
  if (t.closest('.chat__composer')) return
  activePop.value = ''
}

function onDocKeydown(e: KeyboardEvent) {
  if (e.key === 'Escape') activePop.value = ''
}

onMounted(() => {
  document.addEventListener('click', onDocClick)
  document.addEventListener('keydown', onDocKeydown)
})

onUnmounted(() => {
  document.removeEventListener('click', onDocClick)
  document.removeEventListener('keydown', onDocKeydown)
})

/** 工具是否已选。 */
function isToolSelected(name: string): boolean {
  return props.selectedToolNames.includes(name)
}

/** 技能是否已选。 */
function isSkillSelected(id: string): boolean {
  return props.selectedSkillIds.includes(id)
}

/** 勾选/取消工具（chips 与 popover 共用同一份集合）。 */
function toggleTool(name: string) {
  const set = new Set(props.selectedToolNames)
  if (set.has(name)) set.delete(name)
  else set.add(name)
  emit('update:selectedToolNames', [...set])
}

/** 移除工具 chip。 */
function removeTool(name: string) {
  emit('update:selectedToolNames', props.selectedToolNames.filter((t) => t !== name))
}

/** 勾选/取消技能。 */
function toggleSkill(id: string) {
  const set = new Set(props.selectedSkillIds)
  if (set.has(id)) set.delete(id)
  else set.add(id)
  emit('update:selectedSkillIds', [...set])
}

/** 移除技能 chip。 */
function removeSkill(id: string) {
  emit('update:selectedSkillIds', props.selectedSkillIds.filter((s) => s !== id))
}

/** 选择 Agent（单选，选完关 popover）。 */
function selectAgent(id: string) {
  emit('update:activeAgentId', id)
  activePop.value = ''
}

/** 恢复默认 Agent（chip ×）。 */
function resetAgent() {
  emit('update:activeAgentId', DEFAULT_AGENT)
}

/** 选择模型（单选，选完关 popover）。 */
function selectModel(id: string) {
  emit('update:selectedModelId', id)
  activePop.value = ''
}

/** 📁 加载目录（父组件成功后回写 projectDir → chip 显示 + 记入最近使用）。 */
function applyDir(path?: string) {
  const p = (path ?? dirInput.value).trim()
  if (!p) return
  activePop.value = ''
  emit('select-directory', p)
}

/** 目录成功加载后记入「最近使用」。 */
watch(
  () => props.projectDir,
  (v) => {
    if (v) addRecent(v)
  },
)

function loadRecentDirs(): string[] {
  try {
    const raw = localStorage.getItem(RECENT_KEY)
    const arr = raw ? (JSON.parse(raw) as unknown) : []
    return Array.isArray(arr) ? arr.filter((x): x is string => typeof x === 'string').slice(0, 5) : []
  } catch {
    return []
  }
}

function addRecent(dir: string) {
  const next = [dir, ...recentDirs.value.filter((d) => d !== dir)].slice(0, 5)
  recentDirs.value = next
  try {
    localStorage.setItem(RECENT_KEY, JSON.stringify(next))
  } catch {
    // 存储不可用时忽略
  }
}

/** 发送按钮点击：发送中 = 停止，否则 = 提交。 */
function onSendClick() {
  if (props.sending) {
    emit('stop')
  } else {
    submit()
  }
}

/** 提交消息：空内容直接忽略。 */
function submit() {
  const text = draft.value.trim()
  if (!text || props.sending) return
  emit('send', text)
  draft.value = ''
  // 发送后 textarea 高度复位
  if (taEl.value) taEl.value.style.height = ''
}

/** textarea 自动增高（rows 2 → 6 效果，上限 168px）。 */
function autoGrow(e: Event) {
  const el = e.target as HTMLTextAreaElement
  el.style.height = 'auto'
  el.style.height = `${Math.min(el.scrollHeight, 168)}px`
}

/* ---- 派生状态 ---- */

/** 是否有已选上下文（chips 区是否显示）。 */
const hasChips = computed(
  () =>
    !!props.projectDir ||
    props.selectedToolNames.length > 0 ||
    props.selectedSkillIds.length > 0 ||
    (!!props.activeAgentId && props.activeAgentId !== DEFAULT_AGENT),
)

/** 目录 chip 文案：取路径最后一段。 */
const dirLabel = computed(() => {
  const p = props.projectDir
  if (!p) return ''
  return p.split(/[\\/]/).filter(Boolean).pop() ?? p
})

/** 技能 chip 文案：id → 名称（未命中显示原 id）。 */
function skillLabel(id: string): string {
  return props.skills.find((s) => s.id === id)?.name ?? id
}

/** Agent chip 文案。 */
const agentLabel = computed(() => props.agents.find((a) => a.id === props.activeAgentId)?.name ?? '')

/** 模型按钮文案：当前选中模型显示名。 */
const currentModelLabel = computed(() => {
  const m = props.models.find((x) => x.chatModelId === props.selectedModelId)
  return m?.alias || m?.upstreamModelId || m?.chatModelId || '选择模型'
})

/** 工具 popover 过滤。 */
const filteredTools = computed(() => {
  const q = toolSearch.value.trim().toLowerCase()
  if (!q) return props.tools
  return props.tools.filter(
    (t) => (t.name ?? '').toLowerCase().includes(q) || (t.description ?? '').toLowerCase().includes(q),
  )
})

/** 技能 popover 过滤。 */
const filteredSkills = computed(() => {
  const q = skillSearch.value.trim().toLowerCase()
  if (!q) return props.skills
  return props.skills.filter(
    (s) => (s.name ?? '').toLowerCase().includes(q) || (s.id ?? '').toLowerCase().includes(q),
  )
})

/** 模型 popover 过滤。 */
const filteredModels = computed(() => {
  const q = modelSearch.value.trim().toLowerCase()
  if (!q) return props.models
  return props.models.filter(
    (m) => modelLabel(m).toLowerCase().includes(q) || (m.chatModelId ?? '').toLowerCase().includes(q),
  )
})

/** 消息变化后自动滚动到底部。除消息数/发送态外，还跟踪最后一条消息的内容长度——
 *  流式逐字期间长度持续增长，驱动滚动跟随（否则长回复流式中会停在首屏）。 */
watch(
  () => [
    props.messages.length,
    props.sending,
    props.messages[props.messages.length - 1]?.content.length ?? 0,
  ],
  async () => {
    await nextTick()
    if (listEl.value) listEl.value.scrollTop = listEl.value.scrollHeight
  },
)

/** 是否显示空态引导（无消息且未发送中）。 */
const showEmpty = computed(() => props.messages.length === 0 && !props.sending)
</script>

<style scoped>
.chat {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-width: 0;
}

/* ---- 顶部条（设计原型 44px） ---- */
.chat__bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  height: 44px;
  flex-shrink: 0;
  padding: 0 16px;
  background: var(--el-bg-color, #1d1e1f);
  border-bottom: 1px solid var(--el-border-color, #414243);
}

.chat__bar-left,
.chat__bar-right {
  display: flex;
  align-items: center;
  gap: 12px;
}

.chat__title {
  font-size: var(--el-font-size-base, 14px);
  font-weight: var(--el-weight-semibold, 600);
  color: var(--el-text-color-primary, #e5eaf3);
}

/* 版本徽标：主色淡底胶囊 */
.chat__version {
  display: inline-block;
  margin-left: 8px;
  padding: 1px 6px;
  font-size: var(--el-font-size-extra-small, 12px);
  font-weight: var(--el-weight-medium, 500);
  color: var(--el-color-primary, #ffb84d);
  background: var(--el-color-primary-light, rgba(255, 184, 77, 0.12));
  border-radius: 999px;
  vertical-align: middle;
}

.chat__status {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: var(--el-font-size-extra-small, 12px);
  color: var(--el-color-success, #67c23a);
}

.chat__dot {
  width: 6px;
  height: 6px;
  border-radius: 999px;
  background: var(--el-color-success, #67c23a);
}

.chat__dot--busy {
  background: var(--el-color-warning, #e6a23c);
}

/* 左右栏折叠按钮 */
.chat__fold {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
  border: none;
  border-radius: var(--el-border-radius-small, 4px);
  background: transparent;
  color: var(--el-text-color-secondary, #a3a6ad);
  cursor: pointer;
}

.chat__fold:hover {
  background: var(--el-fill-color-light, #303132);
  color: var(--el-text-color-primary, #e5eaf3);
}

/* Fold/Expand 图标尺寸钳制（EP 图标 :size 在共享桥下不转 width/height，须显式给宽高） */
.chat__fold-ico {
  width: 14px;
  height: 14px;
}

/* ---- 消息列表 ---- */
.chat__list {
  flex: 1;
  overflow-y: auto;
  scrollbar-width: thin;
}

.chat__list-inner {
  display: flex;
  flex-direction: column;
  gap: 16px;
  width: 100%;
  max-width: 820px;
  margin: 0 auto;
  padding: 16px 24px;
}

.chat__msg {
  display: flex;
  gap: 12px;
}

.chat__msg--user {
  justify-content: flex-end;
}

/* 头像块：设计原型 32px，AI 用主色描边，用户用 accent 底 */
.chat__avatar {
  display: flex;
  align-items: flex-start;
  justify-content: center;
  width: 32px;
  height: 32px;
  flex-shrink: 0;
  border-radius: var(--el-border-radius-small, 4px);
}

.chat__avatar--ai {
  padding: 0;
  background: transparent;
  border: none;
  font-size: 22px;
  line-height: 1;
}

.chat__avatar-emoji {
  display: inline-block;
  width: 28px;
  height: 28px;
  line-height: 28px;
  text-align: center;
  font-size: 20px;
  /* emoji 字体回退链：跨 Windows / macOS / Linux / 无字体环境都能正常渲染 🤖，
     emoji 关键字作为兜底；不命中时浏览器会显式回退到平台 emoji 字体。 */
  font-family: "Apple Color Emoji", "Segoe UI Emoji", "Segoe UI Symbol",
               "Noto Color Emoji", "Twemoji Mozilla", "EmojiOne Color",
               emoji, inherit;
}

.chat__avatar--user {
  padding: 6px;
  background: var(--el-color-accent-muted, rgba(96, 165, 250, 0.15));
  color: var(--el-color-accent, #60a5fa);
}

.chat__avatar--user svg {
  width: 18px;
  height: 18px;
}

.chat__bubble-wrap {
  display: flex;
  flex-direction: column;
  gap: 8px;
  min-width: 0;
  flex: 1;
  padding-left: 12px;
  margin-left: -2px;
  border-left: 2px solid var(--el-color-primary, #ffb84d);
}

/* 气泡：AI 与用户用不同底色，圆角左上/右上小、右下 base */
.chat__bubble {
  padding: 10px 14px;
  max-width: 85%;
  font-size: var(--el-font-size-small, 13px);
  line-height: 1.6;
  white-space: pre-wrap;
  word-break: break-word;
}

.chat__bubble--ai {
  color: var(--el-text-color-primary, #e5eaf3);
  background: var(--el-bg-color, #1d1e1f);
  border: 1px solid var(--el-border-color-dark, #2b2b2c);
  border-radius: 4px 4px 8px 4px;
}

.chat__msg--user .chat__bubble--user {
  color: var(--el-text-color-primary, #e5eaf3);
  background: var(--el-bg-color-overlay, #1d1e1f);
  border: 1px solid var(--el-border-color, #414243);
  border-radius: 4px 4px 8px 4px;
}

.chat__bubble--typing {
  color: var(--el-text-color-secondary, #a3a6ad);
}

/* 工具调用卡片（可折叠） */
.chat__tools {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.chat__tool-card {
  border: 1px solid var(--el-border-color-dark, #2b2b2c);
  border-radius: var(--el-border-radius-small, 4px);
  background: var(--el-fill-color, #262727);
  overflow: hidden;
}

.chat__tool-summary {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 10px;
  cursor: pointer;
  list-style: none;
  font-size: var(--el-font-size-extra-small, 12px);
  font-family: var(--el-font-family-mono, monospace);
  color: var(--el-color-primary, #ffb84d);
  user-select: none;
}

.chat__tool-summary::-webkit-details-marker {
  display: none;
}

.chat__tool-icon {
  width: 12px;
  height: 12px;
  flex-shrink: 0;
}

.chat__tool-name {
  font-weight: var(--el-weight-medium, 500);
}

.chat__tool-status {
  margin-left: auto;
  padding: 1px 6px;
  border-radius: 999px;
}

.chat__tool-status--pending {
  color: var(--el-color-warning, #e6a23c);
  background: rgba(230, 162, 60, 0.12);
}

.chat__tool-status--ok {
  color: var(--el-color-success, #67c23a);
  background: rgba(103, 194, 58, 0.12);
}

.chat__tool-status--fail {
  color: var(--el-color-danger, #f56c6c);
  background: rgba(245, 108, 108, 0.12);
}

.chat__tool-body {
  padding: 8px 10px;
  border-top: 1px solid var(--el-border-color-dark, #2b2b2c);
}

.chat__tool-pre {
  margin: 0 0 6px;
  max-height: 200px;
  overflow-y: auto;
  font-size: var(--el-font-size-extra-small, 12px);
  font-family: var(--el-font-family-mono, monospace);
  color: var(--el-text-color-secondary, #a3a6ad);
  white-space: pre-wrap;
  word-break: break-word;
}

.chat__tool-pre:last-child {
  margin-bottom: 0;
}

.chat__tool-label {
  display: block;
  margin-bottom: 2px;
  font-weight: var(--el-weight-medium, 500);
  color: var(--el-color-primary, #ffb84d);
}

/* ---- 错误提示 ---- */
.chat__error {
  margin: 0;
  padding: 6px 24px;
  font-size: var(--el-font-size-extra-small, 12px);
  color: var(--el-color-danger, #f56c6c);
  background: var(--el-bg-color, #1d1e1f);
}

/* ---- Composer（输入区重构：chips + textarea + 操作栏 + popovers） ---- */
.chat__composer-wrap {
  flex-shrink: 0;
  padding: 12px 24px 16px;
  background: var(--el-bg-color, #1d1e1f);
  border-top: 1px solid var(--el-border-color, #414243);
}

.chat__composer {
  position: relative;
  max-width: 820px;
  margin: 0 auto;
  background: var(--el-bg-color, #1d1e1f);
  border: 1px solid var(--el-border-color, #414243);
  border-radius: var(--el-border-radius-base, 8px);
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.25);
  transition: border-color 0.15s ease;
}

.chat__composer:focus-within {
  border-color: var(--el-color-primary, #ffb84d);
}

/* chips 区 */
.chat__chips {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  padding: 10px 12px 0;
}

/* chip：强制单行（nowrap），宽度由内容自适应 + spill-right 受限，绝不换行；
   min-width:0 是让文本能收缩的前提（flex 子项默认 min-width:auto 会按内容撑开导致换行） */
.chat__chips .chat__chip {
  max-width: 100%;
  min-width: 0;
  padding: 3px 8px;
  font-size: var(--el-font-size-extra-small, 12px);
  color: var(--el-text-color-regular, #cfd3dc);
  background: var(--el-fill-color, #262727);
  border: 1px solid var(--el-border-color-light, #363637);
  border-radius: 999px;
  white-space: nowrap; /* 图标+文字单行，文字过长由 chip-txt 省略 */
  animation: chat-chip-in 0.15s ease;
}
/* ElTag 内部 content 容器（默认 flex wrap）强制单行：图在左、文在右、文字过长省略。
   :deep 穿透 scoped 才能命中 ElTag 内部类。 */
.chat__chips .chat__chip :deep(.el-tag__content) {
  display: inline-flex;
  align-items: center;
  flex-wrap: nowrap;
  gap: 5px;
  max-width: 100%;
  min-width: 0;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

@keyframes chat-chip-in {
  from {
    opacity: 0;
    transform: translateY(2px);
  }
  to {
    opacity: 1;
    transform: none;
  }
}

.chat__chip--dir {
  color: var(--el-color-primary-light-7, #fde68a);
  border-color: rgba(245, 158, 11, 0.35);
}

.chat__chip--agent {
  color: var(--el-color-info, #58a6ff);
  border-color: rgba(88, 166, 255, 0.35);
}

.chat__chip-ico {
  flex-shrink: 0;
}

.chat__chip-txt {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-family: var(--el-font-family-mono, monospace);
}

.chat__chip--dir .chat__chip-txt {
  font-family: inherit;
}

.chat__chip-x {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 14px;
  height: 14px;
  flex-shrink: 0;
  border: none;
  border-radius: 999px;
  background: transparent;
  color: var(--el-text-color-placeholder, #8c959f);
  font-size: 12px;
  line-height: 1;
  cursor: pointer;
}

.chat__chip-x:hover {
  background: rgba(245, 108, 108, 0.15);
  color: var(--el-color-error, #f56c6c);
}

/* textarea：透明底、自动增高（2~6 行，上限 168px） */
.chat__textarea {
  display: block;
  width: 100%;
  min-height: 56px;
  max-height: 168px;
  padding: 12px 14px 8px;
  font-size: var(--el-font-size-small, 13px);
  font-family: var(--el-font-family, inherit);
  line-height: 1.6;
  color: var(--el-text-color-primary, #e5eaf3);
  background: transparent;
  border: none;
  outline: none;
  resize: none;
}

.chat__textarea::placeholder {
  color: var(--el-text-color-placeholder, #8c959f);
}

/* 操作栏 */
.chat__actionbar {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 6px 8px 8px;
}

.chat__abtn {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  height: 28px;
  padding: 0 10px;
  border: none;
  border-radius: var(--el-border-radius-small, 4px);
  background: transparent;
  color: var(--el-text-color-secondary, #a3a6ad);
  font-size: var(--el-font-size-extra-small, 12px);
  cursor: pointer;
}

.chat__abtn:hover {
  background: var(--el-fill-color-light, #303132);
  color: var(--el-text-color-primary, #e5eaf3);
}

.chat__abtn--sel {
  color: var(--el-color-primary, #ffb84d);
  background: var(--el-color-primary-light-9, rgba(255, 184, 77, 0.1));
}

.chat__abtn-ico {
  flex-shrink: 0;
  /* 共享桥下 EP icon 的 :size prop 不转 width/height，图标会回退超大，须显式钳制 */
  width: 14px;
  height: 14px;
}

/* chip 内图标 */
.chat__chip-ic {
  flex-shrink: 0;
  width: 12px;
  height: 12px;
  vertical-align: -2px;
}

/* B2：popover 打开时入口按钮高亮 */
.chat__abtn--open,
.chat__abtn--open:hover {
  color: var(--el-color-primary, #ffb84d);
  background: var(--el-color-primary-light-9, rgba(255, 184, 77, 0.1));
}

.chat__model-pick--open {
  border-color: var(--el-color-primary, #ffb84d);
  color: var(--el-color-primary, #ffb84d);
}

/* A3：自研按钮键盘聚焦可见焦点环（ElButton 由 EP 自带聚焦样式） */
.chat__fold:focus-visible,
.chat__model-pick:focus-visible,
.chat__pop-go:focus-visible,
.chat__pop-recent-item:focus-visible,
.chat__opt:focus-visible {
  outline: 2px solid var(--el-color-primary, #ffb84d);
  outline-offset: 1px;
}

/* B1：消息进入过渡（淡入 + 上滑，流式更顺滑） */
.msg-enter-active {
  transition: opacity 0.22s ease, transform 0.22s ease;
}

.msg-enter-from {
  opacity: 0;
  transform: translateY(6px);
}

.chat__abtn-n {
  padding: 0 4px;
  border-radius: 999px;
  background: rgba(245, 158, 11, 0.18);
  font-family: var(--el-font-family-mono, monospace);
}

.chat__ab-spacer {
  flex: 1;
}

/* 模型选择 + 发送 */
.chat__model-pick {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  height: 28px;
  padding: 0 10px;
  border: 1px solid var(--el-border-color, #414243);
  border-radius: var(--el-border-radius-small, 4px);
  background: var(--el-fill-color, #262727);
  color: var(--el-text-color-regular, #cfd3dc);
  font-size: var(--el-font-size-extra-small, 12px);
  cursor: pointer;
}

.chat__model-pick:hover {
  border-color: var(--el-color-primary, #ffb84d);
}

.chat__model-caret {
  font-size: 10px;
}

.chat__send {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 34px;
  height: 34px;
  margin-left: 8px;
  border: none;
  border-radius: var(--el-border-radius-small, 4px);
  background: var(--el-color-primary, #ffb84d);
  color: var(--el-text-color-inverse, #1d1e1f);
  font-size: 15px;
  cursor: pointer;
  transition: opacity 0.15s ease;
}

/* 发送/停止图标同尺寸钳制（共享桥下 :size 不生效） */
.chat__send svg {
  width: 16px;
  height: 16px;
}

.chat__send--stop {
  background: var(--el-color-error, #f56c6c);
  color: #fff;
}

.chat__send:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

/* popover：上弹面板（底部对齐 composer 顶部） */
.chat__pop {
  position: absolute;
  bottom: calc(100% + 8px);
  left: 12px;
  z-index: 30;
  display: flex;
  flex-direction: column;
  width: 340px;
  max-height: 320px;
  overflow: hidden;
  background: var(--el-bg-color-overlay, #262727);
  border: 1px solid var(--el-border-color, #414243);
  border-radius: var(--el-border-radius-base, 8px);
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.4);
  animation: chat-pop-in 0.12s ease;
}

.chat__pop--right {
  left: auto;
  right: 8px;
}

@keyframes chat-pop-in {
  from {
    opacity: 0;
    transform: translateY(4px);
  }
  to {
    opacity: 1;
    transform: none;
  }
}

.chat__pop-head {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 10px;
  border-bottom: 1px solid var(--el-border-color-dark, #2b2b2c);
}

.chat__pop-title {
  font-size: var(--el-font-size-extra-small, 12px);
  font-weight: var(--el-weight-semibold, 600);
  color: var(--el-text-color-secondary, #a3a6ad);
  white-space: nowrap;
}

.chat__pop-search {
  flex: 1;
  height: 26px;
  min-width: 0;
  padding: 0 8px;
  border: 1px solid var(--el-border-color, #414243);
  border-radius: var(--el-border-radius-small, 4px);
  outline: none;
  background: var(--el-fill-color, #262727);
  color: var(--el-text-color-primary, #e5eaf3);
  font-size: var(--el-font-size-extra-small, 12px);
  font-family: inherit;
}

.chat__pop-search:focus {
  border-color: var(--el-color-primary, #ffb84d);
}

.chat__pop-body {
  overflow-y: auto;
  padding: 6px;
}

.chat__pop-foot {
  padding: 6px 10px;
  border-top: 1px solid var(--el-border-color-dark, #2b2b2c);
  font-size: 11px;
  color: var(--el-text-color-placeholder, #8c959f);
}

.chat__pop-hint {
  margin: 0;
  padding: 6px 8px;
  font-size: var(--el-font-size-extra-small, 11px);
  line-height: 1.6;
  color: var(--el-text-color-placeholder, #8c959f);
}

/* 选项行 */
.chat__opt {
  display: flex;
  align-items: center;
  gap: 8px;
  width: 100%;
  padding: 7px 8px;
  border: none;
  border-radius: var(--el-border-radius-small, 4px);
  background: transparent;
  color: var(--el-text-color-regular, #cfd3dc);
  text-align: left;
  font-size: var(--el-font-size-small, 13px);
  cursor: pointer;
}

.chat__opt:hover {
  background: var(--el-fill-color, #262727);
}

.chat__opt--on {
  background: rgba(245, 158, 11, 0.1);
}

.chat__opt-box {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 15px;
  height: 15px;
  flex-shrink: 0;
  border: 1px solid var(--el-border-color, #414243);
  border-radius: 3px;
  font-size: 11px;
  color: transparent;
}

.chat__opt--on .chat__opt-box {
  background: var(--el-color-primary, #ffb84d);
  border-color: var(--el-color-primary, #ffb84d);
  color: #1d1e1f;
}

.chat__opt-box--radio {
  border-radius: 999px;
}

.chat__opt-name {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-family: var(--el-font-family-mono, monospace);
  font-size: var(--el-font-size-extra-small, 12px);
}

.chat__opt-desc {
  margin-left: auto;
  max-width: 140px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--el-text-color-placeholder, #8c959f);
  font-size: 11px;
}

/* 📁 目录 popover */
.chat__pop-dir {
  padding: 10px;
}

.chat__pop-dir-row {
  display: flex;
  gap: 6px;
}

.chat__pop-input {
  flex: 1;
  height: 30px;
  min-width: 0;
  padding: 0 8px;
  border: 1px solid var(--el-border-color, #414243);
  border-radius: var(--el-border-radius-small, 4px);
  outline: none;
  background: var(--el-fill-color, #262727);
  color: var(--el-text-color-primary, #e5eaf3);
  font-size: var(--el-font-size-extra-small, 12px);
  font-family: var(--el-font-family-mono, monospace);
}

.chat__pop-input:focus {
  border-color: var(--el-color-primary, #ffb84d);
}

.chat__pop-go {
  height: 30px;
  padding: 0 12px;
  border: none;
  border-radius: var(--el-border-radius-small, 4px);
  background: var(--el-color-primary, #ffb84d);
  color: #1d1e1f;
  font-size: var(--el-font-size-extra-small, 12px);
  cursor: pointer;
}

.chat__pop-go:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.chat__pop-recent {
  margin-top: 10px;
}

.chat__pop-recent-t {
  margin-bottom: 4px;
  font-size: 11px;
  color: var(--el-text-color-placeholder, #8c959f);
}

.chat__pop-recent-item {
  display: block;
  width: 100%;
  padding: 5px 8px;
  margin: 2px 0;
  border: none;
  border-radius: var(--el-border-radius-small, 4px);
  background: transparent;
  color: var(--el-text-color-regular, #cfd3dc);
  font-size: var(--el-font-size-extra-small, 12px);
  font-family: var(--el-font-family-mono, monospace);
  text-align: left;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  cursor: pointer;
}

.chat__pop-recent-item:hover {
  background: var(--el-fill-color, #262727);
}

/* ---- Markdown 渲染（见 markdown.ts）---- */
/* 容器内首/末元素去掉多余外边距，气泡内排版更紧凑。 */
.md > :first-child {
  margin-top: 0;
}

.md > :last-child {
  margin-bottom: 0;
}

.md-p {
  margin: 0 0 6px;
}

.md-h {
  margin: 8px 0 4px;
  font-size: var(--el-font-size-small, 13px);
  font-weight: var(--el-weight-semibold, 600);
  color: var(--el-text-color-primary, #e5eaf3);
}

.md-list {
  margin: 0 0 6px;
  padding-left: 18px;
}

.md-list li {
  margin: 2px 0;
}

.md-quote {
  margin: 4px 0;
  padding: 2px 8px;
  border-left: 3px solid var(--el-color-primary, #ffb84d);
  color: var(--el-text-color-secondary, #a3a6ad);
}

.md-code {
  padding: 1px 4px;
  font-family: var(--el-font-family-mono, monospace);
  font-size: 0.95em;
  color: var(--el-color-primary, #ffb84d);
  background: var(--el-fill-color, #262727);
  border-radius: 3px;
}

.md-pre {
  margin: 6px 0;
  padding: 8px 10px;
  overflow-x: auto;
  max-height: 260px;
  background: var(--el-fill-color, #262727);
  border: 1px solid var(--el-border-color-dark, #2b2b2c);
  border-radius: 4px;
}

.md-pre code {
  font-family: var(--el-font-family-mono, monospace);
  font-size: var(--el-font-size-extra-small, 12px);
  line-height: 1.5;
  color: var(--el-text-color-regular, #cfd3dc);
  white-space: pre;
}

.md-hr {
  margin: 8px 0;
  border: none;
  border-top: 1px solid var(--el-border-color, #414243);
}

.md-link {
  color: var(--el-color-primary, #ffb84d);
  text-decoration: underline;
}
</style>
