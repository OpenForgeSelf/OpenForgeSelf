<script setup lang="ts">
/**
 * Agent 登记 / 编辑表单（内联展开，不用弹窗——弹窗与后面授信弹窗的 z-index 会打架）。
 *
 * 关键设计：交互口「不填 = 按 profile 预填」。所以新增时默认把交互口区域留空，
 * 只在编辑已有 agent 时展示其交互口（只读展示，不改——改交互口属高级操作，
 * 首版收敛到「用 profile 默认」这一条路径，避免把手写参数模板的口子开太大）。
 */

import { computed, reactive, watch } from 'vue'
import type { AgentDto, AgentSaveRequest } from './http'

const props = defineProps<{
  agents: AgentDto[]
  editingId: number | null
}>()

const emit = defineEmits<{
  submit: [body: AgentSaveRequest, id: number | null]
  cancel: []
}>()

/** 内置厂商可选（与 Data/Profiles/*.json 对齐）；也允许手工填其它厂商名。 */
const VENDOR_PRESETS = [
  { value: 'opencode', label: 'opencode' },
  { value: 'codex', label: 'Codex CLI' },
  { value: 'qodercli', label: 'Qoder CLI' },
  { value: 'claude', label: 'Claude Code' },
] as const

const form = reactive({
  name: '',
  vendor: '',
  kind: 'Coding',
  tagsText: '',
  defaultCwd: '',
  enabled: true,
  priority: 0,
  notes: '',
  permissionMode: 'read-only',
  timeoutSeconds: 600,
  approvalTimeoutSeconds: 120,
  maxConcurrency: 1,
  allowedCwdsText: '',
  writableCwdsText: '',
})

const isEdit = computed(() => props.editingId != null)

/** 进入编辑时回填表单。 */
watch(
  () => props.editingId,
  (id) => {
    if (id == null) {
      resetForm()
      return
    }
    const a = props.agents.find((x) => x.id === id)
    if (!a) return
    form.name = a.name ?? ''
    form.vendor = a.vendor ?? ''
    form.kind = a.kind ?? 'Coding'
    form.tagsText = (a.tags ?? []).join(', ')
    form.defaultCwd = a.defaultCwd ?? ''
    form.enabled = a.enabled ?? true
    form.priority = a.priority ?? 0
    form.notes = a.notes ?? ''
    form.permissionMode = a.policy?.permissionMode ?? 'read-only'
    form.timeoutSeconds = a.policy?.timeoutSeconds ?? 600
    form.approvalTimeoutSeconds = a.policy?.approvalTimeoutSeconds ?? 120
    form.maxConcurrency = a.policy?.maxConcurrency ?? 1
    form.allowedCwdsText = (a.policy?.allowedCwds ?? []).join('\n')
    form.writableCwdsText = (a.policy?.writableCwds ?? []).join('\n')
  },
  { immediate: true },
)

function resetForm() {
  form.name = ''
  form.vendor = ''
  form.kind = 'Coding'
  form.tagsText = ''
  form.defaultCwd = ''
  form.enabled = true
  form.priority = 0
  form.notes = ''
  form.permissionMode = 'read-only'
  form.timeoutSeconds = 600
  form.approvalTimeoutSeconds = 120
  form.maxConcurrency = 1
  form.allowedCwdsText = ''
  form.writableCwdsText = ''
}

/** 文本域按行拆目录（去空行、去首尾空白）。 */
function splitLines(text: string): string[] {
  return text
    .split(/\r?\n/)
    .map((s) => s.trim())
    .filter(Boolean)
}

/** 「危险模式不许当默认」——前端也拦一道，不给用户提交后被拒的挫败感。 */
const FORBIDDEN_MODES = ['yolo', 'danger-full-access', 'dangerously-skip-permissions']

function onSubmit() {
  const name = form.name.trim()
  const vendor = form.vendor.trim()
  if (!name || !vendor) return
  if (FORBIDDEN_MODES.includes(form.permissionMode)) return

  const body: AgentSaveRequest = {
    name,
    vendor,
    kind: form.kind,
    tags: form.tagsText
      .split(',')
      .map((s) => s.trim())
      .filter(Boolean),
    enabled: form.enabled,
    priority: form.priority,
    notes: form.notes.trim() || undefined,
    defaultCwd: form.defaultCwd.trim() || undefined,
    policy: {
      permissionMode: form.permissionMode,
      timeoutSeconds: form.timeoutSeconds,
      approvalTimeoutSeconds: form.approvalTimeoutSeconds,
      maxConcurrency: form.maxConcurrency,
      allowedCwds: splitLines(form.allowedCwdsText),
      writableCwds: splitLines(form.writableCwdsText),
    },
    // 新增时不传 accessPoints → 后端按 profile 预填默认交互口
    accessPoints: isEdit.value ? undefined : undefined,
  }
  emit('submit', body, props.editingId)
}
</script>

<template>
  <div class="ok-formbox">
    <div class="ok-formbox__title">{{ isEdit ? '编辑 Agent' : '登记新 Agent' }}</div>

    <div class="ok-grid2">
      <div>
        <label class="ok-flabel">显示名 *</label>
        <input class="ok-input" v-model="form.name" placeholder="例如：本机 Codex" />
      </div>
      <div>
        <label class="ok-flabel">厂商标识 *（决定用哪个 profile 预填交互口）</label>
        <input class="ok-input" v-model="form.vendor" list="ok-vendor-presets" placeholder="opencode / codex / qodercli / claude" />
        <datalist id="ok-vendor-presets">
          <option v-for="v in VENDOR_PRESETS" :key="v.value" :value="v.value">{{ v.label }}</option>
        </datalist>
      </div>
      <div>
        <label class="ok-flabel">类型</label>
        <select class="ok-input" v-model="form.kind">
          <option value="Coding">Coding（写代码）</option>
          <option value="Generic">Generic（通用）</option>
        </select>
      </div>
      <div>
        <label class="ok-flabel">擅长标签（逗号分隔）</label>
        <input class="ok-input" v-model="form.tagsText" placeholder="refactor, test, docs" />
      </div>
      <div>
        <label class="ok-flabel">默认工作目录</label>
        <input class="ok-input" v-model="form.defaultCwd" placeholder="D:\\src\\my-proj\\xxx" />
      </div>
      <div>
        <label class="ok-flabel">选路优先级（大者优先）</label>
        <input class="ok-input" type="number" v-model.number="form.priority" />
      </div>
    </div>

    <div class="ok-formbox__sect">策略</div>
    <div class="ok-grid3">
      <div>
        <label class="ok-flabel">默认权限模式</label>
        <select class="ok-input" v-model="form.permissionMode">
          <option value="read-only">read-only</option>
          <option value="workspace-write">workspace-write</option>
          <option value="accept-edits">accept-edits</option>
        </select>
      </div>
      <div>
        <label class="ok-flabel">执行超时（秒）</label>
        <input class="ok-input" type="number" v-model.number="form.timeoutSeconds" />
      </div>
      <div>
        <label class="ok-flabel">审批超时（秒，超时按拒绝）</label>
        <input class="ok-input" type="number" v-model.number="form.approvalTimeoutSeconds" />
      </div>
      <div>
        <label class="ok-flabel">并发上限</label>
        <input class="ok-input" type="number" v-model.number="form.maxConcurrency" />
      </div>
      <div>
        <label class="ok-flabel">允许的工作目录（每行一个，空=仅默认目录）</label>
        <textarea class="ok-input ok-textarea ok-textarea--sm" v-model="form.allowedCwdsText" rows="3"></textarea>
      </div>
      <div>
        <label class="ok-flabel">允许写操作的目录（每行一个，空=全禁写）</label>
        <textarea class="ok-input ok-textarea ok-textarea--sm" v-model="form.writableCwdsText" rows="3"></textarea>
      </div>
    </div>

    <div class="ok-grid2">
      <div>
        <label class="ok-flabel">备注</label>
        <input class="ok-input" v-model="form.notes" placeholder="可选" />
      </div>
      <div class="ok-checkline">
        <label class="ok-scope">
          <input type="checkbox" v-model="form.enabled" />
          <span>启用（启用后才可被委派选中）</span>
        </label>
      </div>
    </div>

    <div class="ok-formbox__note">
      {{ isEdit
        ? '交互口沿用登记时按 profile 预填的配置，此处不展示。需要改参数模板请直接改数据根下的 Profiles 覆盖版本。'
        : '交互口留空 = 按厂商 profile 预填（推荐）。登记后可用「探测」验证可执行文件与版本，并显式「授信」放开自动放行范围。' }}
    </div>

    <div class="ok-formbox__actions">
      <button class="ok-btn ok-btn--primary" :disabled="!form.name.trim() || !form.vendor.trim()" @click="onSubmit">
        {{ isEdit ? '保存' : '登记' }}
      </button>
      <button class="ok-btn" @click="emit('cancel')">取消</button>
    </div>
  </div>
</template>

<style>
.ok-formbox {
  background: var(--el-bg-color, #fff);
  border: 1px solid var(--el-color-primary-light-7, #c6e2ff);
  border-radius: var(--el-border-radius-base, 6px);
  padding: 16px 18px;
  margin-bottom: 14px;
}
.ok-formbox__title {
  font-size: 15px;
  font-weight: 600;
  margin-bottom: 12px;
}
.ok-formbox__sect {
  font-size: 13px;
  font-weight: 600;
  color: var(--el-text-color-regular);
  margin: 16px 0 10px;
  padding-top: 12px;
  border-top: 1px solid var(--el-border-color-lighter, #ebeef5);
}
.ok-formbox__note {
  margin-top: 14px;
  padding: 10px 12px;
  font-size: 12px;
  line-height: 1.7;
  color: var(--el-text-color-secondary);
  background: var(--el-fill-color-light, #f4f4f5);
  border-radius: var(--el-border-radius-base, 6px);
}
.ok-formbox__actions {
  display: flex;
  gap: 10px;
  margin-top: 14px;
}
.ok-flabel {
  display: block;
  font-size: 12px;
  color: var(--el-text-color-regular);
  margin-bottom: 5px;
}
.ok-grid2 {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
}
.ok-grid3 {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 12px;
}
@media (max-width: 820px) {
  .ok-grid2,
  .ok-grid3 {
    grid-template-columns: 1fr;
  }
}
.ok-textarea--sm {
  padding: 6px 8px;
  min-height: 60px;
  line-height: 1.5;
}
.ok-checkline {
  display: flex;
  align-items: flex-end;
  padding-bottom: 8px;
}
</style>
