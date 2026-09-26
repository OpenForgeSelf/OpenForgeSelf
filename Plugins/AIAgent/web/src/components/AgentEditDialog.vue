<template>
  <ElDialog
    :model-value="visible"
    :title="dialogTitle"
    width="760px"
    :close-on-click-modal="false"
    @update:model-value="(v) => $emit('update:visible', v)"
  >
    <div class="aed">
      <!-- 基本信息 -->
      <div class="aed__sec">
        <div class="aed__sec-title">基本信息</div>
        <div class="aed__row aed__row--inline">
          <div class="aed__field aed__field--grow">
            <label class="aed__label">名称 <span class="aed__req">*</span></label>
            <input v-model="form.name" class="aed__input" type="text" placeholder="Agent 名称" />
          </div>
          <div class="aed__field aed__field--sm">
            <label class="aed__label">头像 emoji</label>
            <input v-model="form.avatar" class="aed__input aed__input--avatar" type="text" maxlength="4" />
          </div>
        </div>

        <div class="aed__row">
          <label class="aed__label">描述</label>
          <input v-model="form.description" class="aed__input" type="text" placeholder="一句话描述该 Agent 的职责" />
        </div>

        <div class="aed__row aed__row--inline">
          <div class="aed__field aed__field--grow">
            <label class="aed__label">类型</label>
            <select v-model="form.type" class="aed__select">
              <option v-for="opt in typeOptions" :key="opt.value" :value="opt.value">
                {{ opt.label }}
              </option>
            </select>
          </div>
          <div class="aed__field aed__field--sm">
            <label class="aed__label">排序</label>
            <input v-model.number="form.sortOrder" class="aed__input aed__input--num" type="number" />
          </div>
          <div class="aed__field aed__field--sm">
            <label class="aed__label">最大迭代</label>
            <input v-model.number="form.maxIterations" class="aed__input aed__input--num" type="number" min="1" max="50" />
          </div>
        </div>

        <div class="aed__row aed__row--switch">
          <label class="aed__switch">
            <input v-model="form.isEnabled" type="checkbox" />
            <span class="aed__switch-ui"></span>
            <span class="aed__switch-label">启用该 Agent</span>
          </label>
        </div>

        <div class="aed__row">
          <label class="aed__label">执行模式</label>
          <div class="aed__mode">
            <label class="aed__mode-opt" :class="{ 'aed__mode-opt--on': form.executionMode === 'free' }">
              <input v-model="form.executionMode" type="radio" value="free" />
              <span class="aed__mode-name">自由循环 FreeLoop</span>
              <span class="aed__mode-desc">现状默认：一次对话内自由思考与工具调用</span>
            </label>
            <label class="aed__mode-opt" :class="{ 'aed__mode-opt--on': form.executionMode === 'plan' }">
              <input v-model="form.executionMode" type="radio" value="plan" />
              <span class="aed__mode-name">计划驱动 PlanDriven</span>
              <span class="aed__mode-desc">先规划后逐步执行，步骤可复盘/卡住可人工介入（029）</span>
            </label>
          </div>
        </div>
      </div>

      <!-- 系统提示词 -->
      <div class="aed__sec">
        <div class="aed__sec-title">
          系统提示词
          <span class="aed__hint">（核心行为指令，直接影响回复风格与能力边界）</span>
        </div>
        <textarea
          v-model="form.systemPrompt"
          class="aed__textarea"
          rows="10"
          placeholder="在此编辑系统提示词…"
        ></textarea>
      </div>

      <!-- 能力画像 -->
      <div class="aed__sec">
        <div class="aed__sec-title">
          能力画像
          <span class="aed__hint">（0~100，影响模型角色化程度）</span>
        </div>
        <div v-for="d in pentadDims" :key="d.key" class="aed__pentad">
          <div class="aed__pentad-row">
            <span class="aed__pentad-label">{{ d.label }}</span>
            <input
              v-model.number="(form as Record<string, number>)[d.key]"
              class="aed__range"
              type="range"
              min="0"
              max="100"
              step="5"
            />
            <span class="aed__pentad-val">{{ (form as Record<string, number>)[d.key] }}</span>
          </div>
        </div>

        <div class="aed__row">
          <label class="aed__label">擅长领域</label>
          <TagInput v-model="form.strengths" placeholder="输入擅长领域，回车添加，如：深度研究" />
        </div>
      </div>

      <!-- 能力与工具 -->
      <div class="aed__sec">
        <div class="aed__sec-title">能力与工具</div>
        <div class="aed__row">
          <label class="aed__label">能力（capabilities）</label>
          <TagInput v-model="form.capabilities" placeholder="输入能力标识，回车添加，如：research" />
        </div>
        <div class="aed__row">
          <label class="aed__label">工具（tools）</label>
          <TagInput v-model="form.tools" placeholder="输入工具名，回车添加，如：get_current_time" />
        </div>
        <div class="aed__row">
          <label class="aed__label">局限性</label>
          <TagInput v-model="form.limitations" placeholder="输入局限，回车添加，如：可能过于学术化" />
        </div>
      </div>

      <!-- 关联工作流 -->
      <div class="aed__sec">
        <div class="aed__sec-title">
          关联工作流
          <span class="aed__hint">（可多选。执行时注入提示词，由 Agent 按需用 execute_workflow 执行）</span>
        </div>
        <div class="aed__row">
          <label class="aed__label">已关联（{{ associatedWorkflows.length }} 个）</label>
          <div v-if="associatedWorkflows.length" class="aed__wf-chips">
            <span v-for="w in associatedWorkflows" :key="w.id" class="aed__wf-chip">
              {{ w.name }}
            </span>
          </div>
          <div v-else class="aed__wf-empty">未关联工作流</div>
        </div>
        <select
          v-if="workflowOptions.length"
          v-model="form.workflowIds"
          multiple
          size="5"
          class="aed__multiselect"
        >
          <option v-for="w in workflowOptions" :key="w.id" :value="w.id">
            {{ w.name }}{{ w.description ? ` — ${w.description}` : '' }}
          </option>
        </select>
        <div v-else class="aed__wf-empty">暂无可用工作流（联系工作流引擎）</div>
        <div class="aed__hint">Ctrl / Shift 多选；关联后可多选多个，执行时由 LLM 按任务匹配。</div>
      </div>

      <div v-if="error" class="aed__error">{{ error }}</div>
    </div>

    <template #footer>
      <ElButton
        v-if="isEditMode"
        type="danger"
        plain
        :loading="deleting"
        @click="onDelete"
      >
        删除
      </ElButton>
      <div class="aed__footer-right">
        <ElButton @click="$emit('update:visible', false)">取消</ElButton>
        <ElButton type="primary" :loading="saving" @click="onSave">{{ isEditMode ? '保存' : '创建' }}</ElButton>
      </div>
    </template>
  </ElDialog>
</template>

<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { ElButton, ElDialog, ElMessageBox } from 'element-plus'
import type { AgentDefinition, AgentPersonality, WorkflowItem } from '../types'
import { createAgent, deleteAgent, fetchWorkflows, updateAgent } from '../http'
import TagInput from './TagInput.vue'

const props = defineProps<{
  /** 编辑的 Agent；为 null 表示新建。 */
  agent: AgentDefinition | null
  visible: boolean
}>()

const emit = defineEmits<{
  (e: 'update:visible', v: boolean): void
  (e: 'saved', agent: AgentDefinition): void
  (e: 'deleted', agentId: string): void
}>()

/** Agent 类型选项（与后端枚举 AgentType 一致：Coordinator=0…Generalist=99）。 */
const typeOptions: Array<{ value: number; label: string }> = [
  { value: 0, label: '协调者（Coordinator）' },
  { value: 1, label: '研究员（Researcher）' },
  { value: 2, label: '写作者（Writer）' },
  { value: 3, label: '程序员（Programmer）' },
  { value: 4, label: '分析师（Analyst）' },
  { value: 5, label: '评论家（Critic）' },
  { value: 99, label: '通用助手（Generalist）' },
]

/** 五维画像维度（表单内以 0~100 存储，保存时除以 100）。 */
const pentadDims: Array<{ key: string; label: string }> = [
  { key: 'creativity', label: '创造力' },
  { key: 'analytical', label: '分析力' },
  { key: 'empathy', label: '同理心' },
  { key: 'confidence', label: '自信度' },
  { key: 'formality', label: '正式度' },
]

const form = reactive({
  name: '',
  description: '',
  avatar: '',
  type: 99,
  sortOrder: 0,
  maxIterations: 10,
  isEnabled: true,
  systemPrompt: '',
  /** 执行模式：free（自由循环，默认）/ plan（计划驱动，029）。随 AgentDefinition.ConfigJson 持久化。 */
  executionMode: 'free' as 'free' | 'plan',
  creativity: 50,
  analytical: 50,
  empathy: 50,
  confidence: 50,
  formality: 50,
  strengths: [] as string[],
  limitations: [] as string[],
  capabilities: [] as string[],
  tools: [] as string[],
  /** 关联工作流（存 workflowId 数组）。 */
  workflowIds: [] as number[],
})

/** 可选的关联工作流（来自 WorkflowEngine /api/workflows；懒加载一次）。 */
const workflowOptions = ref<WorkflowItem[]>([])
const workflowsLoaded = ref(false)
/** 已选中工作流的展示摘要（对应 workflowIds）。 */
const associatedWorkflows = computed(() =>
  workflowOptions.value.filter((w) => form.workflowIds.includes(w.id as number)),
)

const saving = ref(false)
const deleting = ref(false)
const error = ref('')

const isEditMode = computed(() => !!props.agent?.id)
const dialogTitle = computed(() =>
  isEditMode.value ? `编辑 Agent：${props.agent?.name ?? ''}` : '新建 Agent',
)

/** 默认新建表单值。 */
function defaultForm() {
  form.name = ''
  form.description = ''
  form.avatar = ''
  form.type = 99
  form.sortOrder = 0
  form.maxIterations = 10
  form.isEnabled = true
  form.systemPrompt = ''
  form.executionMode = 'free'
  form.creativity = 50
  form.analytical = 50
  form.empathy = 50
  form.confidence = 50
  form.formality = 50
  form.strengths = []
  form.limitations = []
  form.capabilities = []
  form.tools = []
  form.workflowIds = []
}

/** 把 agent 数据灌入表单（编辑模式）。 */
function fillForm(agent: AgentDefinition) {
  form.name = agent.name ?? ''
  form.description = agent.description ?? ''
  form.avatar = agent.avatar ?? ''
  form.type = agent.type ?? 99
  form.sortOrder = agent.sortOrder ?? 0
  form.maxIterations = agent.maxIterations ?? 10
  form.isEnabled = agent.isEnabled ?? true
  form.systemPrompt = agent.systemPrompt ?? ''
  form.executionMode = agent.executionMode ?? 'free'

  const p = agent.personality ?? ({} as AgentPersonality)
  form.creativity = Math.round((p.creativity ?? 0.5) * 100)
  form.analytical = Math.round((p.analytical ?? 0.5) * 100)
  form.empathy = Math.round((p.empathy ?? 0.5) * 100)
  form.confidence = Math.round((p.confidence ?? 0.5) * 100)
  form.formality = Math.round((p.formality ?? 0.5) * 100)
  form.strengths = p.strengths ? [...p.strengths] : []
  form.limitations = p.limitations ? [...p.limitations] : []
  form.capabilities = agent.capabilities ? [...agent.capabilities] : []
  form.tools = agent.tools ? [...agent.tools] : []
  form.workflowIds = agent.workflows ? [...agent.workflows.map((w) => w.workflowId)] : []
}

watch(
  () => props.visible,
  (v) => {
    if (!v) return
    error.value = ''
    loadWorkflows()
    if (props.agent) fillForm(props.agent)
    else defaultForm()
  },
)

/** 懒加载可用工作流一次（失败静默降级为空列表，不阻断编辑）。 */
async function loadWorkflows() {
  if (workflowsLoaded.value) return
  workflowsLoaded.value = true
  try {
    const list = await fetchWorkflows()
    workflowOptions.value = list ?? []
  } catch (e) {
    workflowOptions.value = []
    // 记录但不阻断：工作流接口不可用时仍可编辑 Agent 其他字段。
    console.error('[AgentEditDialog] 加载工作流失败', e)
  }
}

/** 组装提交 payload（Personality 转回 0~1；关联工作流转 AgentWorkflowRef 列表）。 */
function buildPayload(): AgentDefinition {
  const workflows = workflowOptions.value
    .filter((w) => form.workflowIds.includes(w.id as number))
    .map((w) => ({
      workflowId: w.id as number,
      name: w.name ?? '',
      description: w.description ?? '',
    }))
  return {
    ...(props.agent ?? {}),
    name: form.name.trim(),
    description: form.description.trim(),
    avatar: form.avatar.trim(),
    type: Number(form.type),
    sortOrder: Number(form.sortOrder ?? 0),
    maxIterations: Number(form.maxIterations ?? 10),
    isEnabled: form.isEnabled,
    systemPrompt: form.systemPrompt,
    personality: {
      ...(props.agent?.personality ?? {}),
      creativity: form.creativity / 100,
      analytical: form.analytical / 100,
      empathy: form.empathy / 100,
      confidence: form.confidence / 100,
      formality: form.formality / 100,
      strengths: form.strengths,
      limitations: form.limitations,
    },
    capabilities: form.capabilities,
    tools: form.tools,
    workflows,
    executionMode: form.executionMode,
  }
}

async function onSave() {
  if (!form.name.trim()) {
    error.value = 'Agent 名称不能为空'
    return
  }
  saving.value = true
  error.value = ''
  try {
    const payload = buildPayload()
    const saved = isEditMode.value
      ? await updateAgent(props.agent!.id!, payload)
      : await createAgent(payload)
    if (saved) {
      emit('saved', saved)
      emit('update:visible', false)
    } else {
      error.value = '保存失败：服务端未返回更新后的 Agent'
    }
  } catch (e) {
    error.value = e instanceof Error ? e.message : String(e)
  } finally {
    saving.value = false
  }
}

async function onDelete() {
  const agentId = props.agent?.id
  if (!agentId) return
  try {
    await ElMessageBox.confirm(
      `确定删除 Agent「${props.agent?.name ?? agentId}」吗？此操作不可恢复。`,
      '删除确认',
      { type: 'warning', confirmButtonText: '删除', cancelButtonText: '取消' },
    )
  } catch {
    return // 用户取消
  }
  deleting.value = true
  error.value = ''
  try {
    await deleteAgent(agentId)
    emit('deleted', agentId)
    emit('update:visible', false)
  } catch (e) {
    error.value = e instanceof Error ? e.message : String(e)
  } finally {
    deleting.value = false
  }
}
</script>

<style scoped>
.aed {
  display: flex;
  flex-direction: column;
  gap: 20px;
  max-height: 60vh;
  overflow-y: auto;
  padding-right: 4px;
}

.aed__sec {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.aed__sec-title {
  font-size: var(--el-font-size-small, 13px);
  font-weight: var(--el-weight-semibold, 600);
  color: var(--el-text-color-primary, #e5eaf3);
  border-bottom: 1px solid var(--el-border-color-lighter, #2a2b2c);
  padding-bottom: 6px;
}

.aed__hint {
  font-size: var(--el-font-size-extra-small, 12px);
  font-weight: normal;
  color: var(--el-text-color-secondary, #a3a6ad);
  margin-left: 6px;
}

.aed__req {
  color: var(--el-color-danger, #f56c6c);
}

.aed__row {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.aed__row--inline {
  flex-direction: row;
  gap: 16px;
  align-items: flex-end;
}

.aed__row--switch {
  flex-direction: row;
  align-items: center;
}

.aed__field {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.aed__field--grow {
  flex: 1;
}

.aed__field--sm {
  width: 110px;
}

.aed__label {
  font-size: var(--el-font-size-small, 13px);
  font-weight: var(--el-weight-medium, 500);
  color: var(--el-text-color-regular, #cfd3dc);
}

.aed__input {
  padding: 8px 12px;
  border: 1px solid var(--el-border-color, #414243);
  border-radius: var(--el-border-radius-small, 4px);
  background: var(--el-bg-color, #1d1e1f);
  color: var(--el-text-color-primary, #e5eaf3);
  font-size: var(--el-font-size-small, 13px);
  outline: none;
  transition: border-color 0.2s;
}

.aed__input:focus {
  border-color: var(--el-color-primary, #ffb84d);
}

.aed__input--num {
  width: 100%;
}

.aed__input--avatar {
  width: 100px;
}

.aed__select {
  padding: 8px 12px;
  border: 1px solid var(--el-border-color, #414243);
  border-radius: var(--el-border-radius-small, 4px);
  background: var(--el-bg-color, #1d1e1f);
  color: var(--el-text-color-primary, #e5eaf3);
  font-size: var(--el-font-size-small, 13px);
  outline: none;
  appearance: none;
  background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 24 24' fill='none' stroke='%23a3a6ad' stroke-width='2'%3E%3Cpath d='M6 9l6 6 6-6'/%3E%3C/svg%3E");
  background-repeat: no-repeat;
  background-position: right 10px center;
  padding-right: 30px;
  cursor: pointer;
}

.aed__switch {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
}

.aed__switch input {
  position: absolute;
  opacity: 0;
  pointer-events: none;
}

.aed__switch-ui {
  width: 36px;
  height: 20px;
  border-radius: var(--el-border-radius-round, 999px);
  background: var(--el-fill-color-dark, #2f3031);
  position: relative;
  transition: background 0.2s;
  flex-shrink: 0;
}

.aed__switch-ui::after {
  content: '';
  position: absolute;
  top: 2px;
  left: 2px;
  width: 16px;
  height: 16px;
  border-radius: 50%;
  background: #fff;
  transition: left 0.2s;
}

.aed__switch input:checked + .aed__switch-ui {
  background: var(--el-color-primary, #ffb84d);
}

.aed__switch input:checked + .aed__switch-ui::after {
  left: 18px;
}

.aed__switch-label {
  font-size: var(--el-font-size-small, 13px);
  color: var(--el-text-color-regular, #cfd3dc);
}

.aed__textarea {
  padding: 10px 12px;
  border: 1px solid var(--el-border-color, #414243);
  border-radius: var(--el-border-radius-small, 4px);
  background: var(--el-bg-color, #1d1e1f);
  color: var(--el-text-color-primary, #e5eaf3);
  font-size: var(--el-font-size-small, 13px);
  font-family: var(--el-font-family-mono, monospace);
  line-height: 1.6;
  resize: vertical;
  outline: none;
  transition: border-color 0.2s;
}

.aed__textarea:focus {
  border-color: var(--el-color-primary, #ffb84d);
}

/* 五维画像滑块 */
.aed__pentad {
  display: flex;
  align-items: center;
}

.aed__pentad-row {
  display: flex;
  align-items: center;
  gap: 10px;
  width: 100%;
}

.aed__pentad-label {
  width: 60px;
  flex-shrink: 0;
  font-size: var(--el-font-size-extra-small, 12px);
  color: var(--el-text-color-regular, #cfd3dc);
}

.aed__range {
  flex: 1;
  appearance: none;
  height: 4px;
  border-radius: var(--el-border-radius-round, 999px);
  background: var(--el-fill-color, #262727);
  outline: none;
  cursor: pointer;
}

.aed__range::-webkit-slider-thumb {
  appearance: none;
  width: 14px;
  height: 14px;
  border-radius: 50%;
  background: var(--el-color-primary, #ffb84d);
  cursor: pointer;
}

.aed__range::-moz-range-thumb {
  width: 14px;
  height: 14px;
  border: none;
  border-radius: 50%;
  background: var(--el-color-primary, #ffb84d);
  cursor: pointer;
}

.aed__pentad-val {
  width: 36px;
  flex-shrink: 0;
  text-align: right;
  font-family: var(--el-font-family-mono, monospace);
  font-size: var(--el-font-size-extra-small, 12px);
  color: var(--el-color-primary, #ffb84d);
}

.aed__error {
  color: var(--el-color-danger, #f56c6c);
  font-size: var(--el-font-size-extra-small, 12px);
}

.aed__multiselect {
  padding: 8px 12px;
  border: 1px solid var(--el-border-color, #414243);
  border-radius: var(--el-border-radius-small, 4px);
  background: var(--el-bg-color, #1d1e1f);
  color: var(--el-text-color-primary, #e5eaf3);
  font-size: var(--el-font-size-small, 13px);
  outline: none;
  transition: border-color 0.2s;
}

.aed__multiselect:focus {
  border-color: var(--el-color-primary, #ffb84d);
}

.aed__multiselect option {
  background: var(--el-bg-color, #1d1e1f);
  color: var(--el-text-color-primary, #e5eaf3);
}

.aed__wf-chips {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.aed__wf-chip {
  display: inline-flex;
  align-items: center;
  padding: 2px 10px;
  border-radius: var(--el-border-radius-round, 999px);
  background: var(--el-fill-color-dark, #2f3031);
  border: 1px solid var(--el-color-primary, #ffb84d);
  color: var(--el-text-color-primary, #e5eaf3);
  font-size: var(--el-font-size-extra-small, 12px);
}

.aed__wf-empty {
  padding: 8px 12px;
  border: 1px dashed var(--el-border-color, #414243);
  border-radius: var(--el-border-radius-small, 4px);
  color: var(--el-text-color-secondary, #a3a6ad);
  font-size: var(--el-font-size-extra-small, 12px);
}

.aed__footer-right {
  display: flex;
  gap: 8px;
  margin-left: auto;
}
</style>