<template>
  <div class="pdlg">
    <div class="pdlg__mask" @click="$emit('close')"></div>
    <div class="pdlg__box">
      <h3 class="pdlg__title">编辑项目档案</h3>
      <p class="pdlg__root" :title="project.root">{{ project.name }} · {{ project.root }}</p>

      <label class="pdlg__field">
        <span>类型</span>
        <div class="pdlg__type">
          <select v-model="typeMode" class="pdlg__select">
            <option value="preset">预设</option>
            <option value="custom">自定义</option>
          </select>
          <template v-if="typeMode === 'preset'">
            <select v-model="form.type" class="pdlg__select pdlg__select--grow">
              <option v-for="o in presets" :key="o.value" :value="o.value">{{ o.label }}</option>
            </select>
          </template>
          <template v-else>
            <input v-model.trim="form.type" class="pdlg__input pdlg__input--grow" type="text" placeholder="如：数据服务" />
          </template>
        </div>
      </label>

      <label class="pdlg__field">
        <span>描述</span>
        <textarea v-model="form.description" class="pdlg__textarea" rows="3" placeholder="项目简介…"></textarea>
      </label>

      <label class="pdlg__field">
        <span>标签（回车添加，逗号亦可分隔）</span>
        <div class="pdlg__tags-input">
          <span v-for="t in tags" :key="t" class="pdlg__chip">
            {{ t }}
            <button class="pdlg__chip-x" @click="removeTag(t)">×</button>
          </span>
          <input
            v-model="tagDraft"
            class="pdlg__tag-field"
            type="text"
            placeholder="输入后回车"
            @keydown.enter.prevent="commitTag"
            @keydown.comma.prevent="commitTag"
            @blur="commitTag"
          />
        </div>
      </label>

      <p v-if="error" class="pdlg__err">{{ error }}</p>

      <div class="pdlg__foot">
        <button class="pdlg__btn" @click="$emit('close')">取消</button>
        <button class="pdlg__btn pdlg__btn--primary" :disabled="saving" @click="save">{{ saving ? '保存中…' : '保存' }}</button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * 项目档案编辑弹层（spec028 §6 / T07）：
 * - 类型：预设下拉 + 允许切换「自定义」输入任意文本
 * - 描述：文本框
 * - 标签：多标签输入（回车/逗号分隔，可删除）
 * 保存调 PUT api/projects/{id}（body: ProjectUpdate）。
 */
import { computed, ref, watch } from 'vue'
import { apiPut } from './http'
import type { ProjectInfo, ProjectUpdate } from './types'

const props = defineProps<{ project: ProjectInfo }>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'saved', projectId: number): void
}>()

const presets = [
  { value: 'frontend', label: '前端' },
  { value: 'backend', label: '后端' },
  { value: 'fullstack', label: '全栈' },
  { value: 'library', label: '库' },
  { value: 'tool', label: '工具' },
  { value: 'other', label: '其他' },
]

const presetValues = new Set(presets.map((p) => p.value))
const typeMode = ref(presetValues.has(props.project.type) ? 'preset' : 'custom')
const form = ref<{ type: string; description: string }>({
  type: props.project.type,
  description: props.project.description ?? '',
})

const tags = ref<string[]>(
  (props.project.tags ?? '')
    .split(',')
    .map((s) => s.trim())
    .filter(Boolean),
)
const tagDraft = ref('')
const saving = ref(false)
const error = ref('')

function commitTag() {
  const raw = tagDraft.value.trim()
  if (!raw) return
  const parts = raw.split(',').map((s) => s.trim()).filter(Boolean)
  for (const p of parts) {
    if (p && !tags.value.includes(p)) tags.value.push(p)
  }
  tagDraft.value = ''
}

function removeTag(t: string) {
  tags.value = tags.value.filter((x) => x !== t)
}

// 当 typeMode 切到预设时，若当前 form.type 不属于预设，则默认选一个。
function onTypeModeChange() {
  if (typeMode.value === 'preset' && !presetValues.has(form.value.type)) {
    form.value.type = 'frontend'
  }
}

const effectiveType = computed(() => form.value.type)

async function save() {
  const type = effectiveType.value?.trim() ?? ''
  if (!type) {
    error.value = '类型不能为空（可选预设或自定义）'
    return
  }
  saving.value = true
  error.value = ''
  const payload: ProjectUpdate = {
    type,
    description: form.value.description ?? '',
    tags: tags.value.join(','),
  }
  try {
    await apiPut(`/api/projects/${props.project.id}`, payload)
    emit('saved', props.project.id)
  } catch (e) {
    error.value = e instanceof Error ? e.message : String(e)
  } finally {
    saving.value = false
  }
}

// 监听 mode 切换（预设↔自定义）
watch(typeMode, onTypeModeChange)
</script>

<style scoped>
.pdlg__mask {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.5);
  z-index: 50;
}

.pdlg__box {
  position: fixed;
  z-index: 51;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  width: 440px;
  max-width: calc(100vw - 32px);
  padding: 18px 20px;
  background: var(--el-bg-color-overlay, #1d1e1f);
  border: 1px solid var(--el-border-color, #414243);
  border-radius: 10px;
  box-shadow: 0 8px 30px rgba(0, 0, 0, 0.4);
}

.pdlg__title {
  margin: 0 0 6px;
  font-size: 15px;
  color: var(--el-text-color-primary, #e5eaf3);
}

.pdlg__root {
  margin: 0 0 14px;
  font-size: 12px;
  color: var(--el-text-color-secondary, #a3a6ad);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.pdlg__field {
  display: flex;
  flex-direction: column;
  gap: 4px;
  margin-bottom: 12px;
}

.pdlg__field > span {
  font-size: 12px;
  color: var(--el-text-color-secondary, #a3a6ad);
}

.pdlg__type {
  display: flex;
  gap: 6px;
}

.pdlg__select,
.pdlg__input {
  padding: 6px 8px;
  border: 1px solid var(--el-border-color, #414243);
  border-radius: 6px;
  background: var(--el-fill-color, #262727);
  color: var(--el-text-color-primary, #e5eaf3);
  font-size: 13px;
}

.pdlg__select--grow,
.pdlg__input--grow {
  flex: 1;
  min-width: 0;
}

.pdlg__select:focus,
.pdlg__input:focus,
.pdlg__textarea:focus {
  outline: none;
  border-color: var(--el-color-primary, #ffb84d);
}

.pdlg__textarea {
  padding: 6px 8px;
  border: 1px solid var(--el-border-color, #414243);
  border-radius: 6px;
  background: var(--el-fill-color, #262727);
  color: var(--el-text-color-primary, #e5eaf3);
  font-size: 13px;
  font-family: inherit;
  resize: vertical;
}

.pdlg__tags-input {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  align-items: center;
  padding: 6px 8px;
  border: 1px solid var(--el-border-color, #414243);
  border-radius: 6px;
  background: var(--el-fill-color, #262727);
  min-height: 36px;
}

.pdlg__chip {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 1px 6px;
  border-radius: 10px;
  font-size: 12px;
  color: var(--el-text-color-secondary, #a3a6ad);
  background: var(--el-bg-color, #1d1e1f);
}

.pdlg__chip-x {
  border: none;
  background: transparent;
  color: var(--el-text-color-secondary, #a3a6ad);
  cursor: pointer;
  font-size: 13px;
  line-height: 1;
}

.pdlg__chip-x:hover {
  color: var(--el-color-danger, #f56c6c);
}

.pdlg__tag-field {
  flex: 1;
  min-width: 80px;
  border: none;
  background: transparent;
  color: var(--el-text-color-primary, #e5eaf3);
  font-size: 13px;
  outline: none;
}

.pdlg__err {
  margin: 0 0 8px;
  font-size: 12px;
  color: var(--el-color-danger, #f56c6c);
}

.pdlg__foot {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  margin-top: 6px;
}

.pdlg__btn {
  padding: 6px 14px;
  border: 1px solid var(--el-border-color, #414243);
  border-radius: 6px;
  background: transparent;
  color: var(--el-text-color-regular, #cfd3dc);
  cursor: pointer;
  font-size: 13px;
}

.pdlg__btn--primary {
  border-color: var(--el-color-primary, #ffb84d);
  color: var(--el-color-primary, #ffb84d);
}

.pdlg__btn--primary:hover:not(:disabled) {
  background: var(--el-color-primary-light, rgba(255, 184, 77, 0.12));
}

.pdlg__btn:disabled {
  opacity: 0.5;
  cursor: default;
}
</style>
