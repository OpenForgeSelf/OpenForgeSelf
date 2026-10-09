<script setup lang="ts">
/**
 * 工具测试台（MCP 中心 v2.3.0）：
 * 左侧工具清单 → 选中后按 inputSchema 动态渲染参数表单 → 发起真实调用 → 展示耗时/文本/原始 JSON。
 *
 * 组件边界：只负责呈现与交互编排；schema 解析与参数序列化全部委托给纯函数模块 schemaForm.ts
 * （那里有 vitest 常驻判据，这里不重复实现解析逻辑）。
 */
import { computed, ref, watch } from 'vue'
import { ElButton, ElInput, ElInputNumber, ElMessage, ElOption, ElSelect, ElSwitch, ElTag } from 'element-plus'
import { invokeExternalTool } from '../api/external'
import type { McpExternalToolDto, McpToolInvokeResult } from '../types/external'
import {
  buildDefaultArguments,
  buildFieldDescriptors,
  missingRequiredFields,
  parseInputSchema,
  serializeArguments,
  type FieldDescriptor,
} from './schemaForm'

const props = defineProps<{
  serverId: string
  tools: McpExternalToolDto[]
  loading?: boolean
}>()

/** 当前选中的工具名（原生名，不是 mcp.<id>.<name>）。 */
const selectedName = ref<string>('')
/** 表单值：参数名 → 值（统一存 unknown，序列化时按字段类型转换）。 */
const values = ref<Record<string, unknown>>({})
/** 结果面板。 */
const result = ref<McpToolInvokeResult | null>(null)
const invoking = ref(false)
const errorText = ref<string>('')
/** JSON 直编模式（处理嵌套 object/array 等表单表达不了的结构）。 */
const jsonMode = ref(false)
const jsonText = ref('{}')
const showRaw = ref(false)

const selected = computed(() => props.tools.find((t) => t.name === selectedName.value) ?? null)

const fields = computed<FieldDescriptor[]>(() =>
  buildFieldDescriptors(parseInputSchema(selected.value?.inputSchemaJson)),
)

/** 必填未填的字段名（用于禁用调用按钮并给出明确提示）。 */
const missing = computed(() => missingRequiredFields(fields.value, values.value))

/** 参数 JSON 预览（表单模式时也实时展示，让用户知道到底会发出什么）。 */
const previewJson = computed(() =>
  jsonMode.value ? jsonText.value : serializeArguments(fields.value, values.value),
)

function selectTool(t: McpExternalToolDto): void {
  if (selectedName.value === t.name) return
  selectedName.value = t.name
}

watch(
  () => [props.serverId, props.tools],
  () => {
    // 换服务器 / 工具清单刷新：重置选择（避免拿着旧工具的 schema 去调新工具）
    selectedName.value = props.tools.some((t) => t.name === selectedName.value) ? selectedName.value : ''
    result.value = null
    errorText.value = ''
  },
  { deep: true },
)

watch(
  selected,
  (tool) => {
    result.value = null
    errorText.value = ''
    if (!tool) {
      values.value = {}
      jsonText.value = '{}'
      return
    }
    const f = buildFieldDescriptors(parseInputSchema(tool.inputSchemaJson))
    values.value = buildDefaultArguments(f)
    jsonText.value = serializeArguments(f, values.value)
  },
  { immediate: true },
)

/** 表单 ↔ JSON 双向同步的入口：切模式时以当前值为准重新序列化 / 解析。 */
function toggleMode(): void {
  if (jsonMode.value) {
    // JSON → 表单：解析失败时不切（避免静默丢掉用户输入）
    try {
      const parsed = JSON.parse(jsonText.value) as unknown
      if (parsed === null || typeof parsed !== 'object' || Array.isArray(parsed)) {
        ElMessage.warning('JSON 模式下参数必须是对象')
        return
      }
      values.value = parsed as Record<string, unknown>
    } catch {
      ElMessage.warning('参数 JSON 不合法，请先修正')
      return
    }
  } else {
    jsonText.value = serializeArguments(fields.value, values.value)
  }
  jsonMode.value = !jsonMode.value
}

async function invoke(): Promise<void> {
  if (!selected.value) return
  if (!jsonMode.value && missing.value.length > 0) {
    ElMessage.warning(`必填参数未填：${missing.value.join('、')}`)
    return
  }
  invoking.value = true
  errorText.value = ''
  try {
    const r = await invokeExternalTool(props.serverId, {
      tool: selected.value.name,
      argumentsJson: previewJson.value,
    })
    if (!r) {
      errorText.value = '调用未返回结果'
      return
    }
    result.value = r
    if (r.ok) {
      ElMessage.success(`调用成功（${r.elapsedMs}ms）`)
    } else {
      // 远端声明 isError：展示出来，不静默
      ElMessage.warning(`工具返回错误（${r.elapsedMs}ms），详见结果面板`)
    }
  } catch (e: unknown) {
    errorText.value = e instanceof Error ? e.message : String(e)
    ElMessage.error(`调用失败：${errorText.value}`)
  } finally {
    invoking.value = false
  }
}

/** 字段类型 → 展示用标签。 */
function typeLabel(f: FieldDescriptor): string {
  if (f.type === 'array') return `array<${f.itemType ?? 'unknown'}>`
  return f.type
}

/** 输入框占位提示。array/object 额外给出 JSON 样例（写在脚本里，避免模板属性里嵌引号破坏解析）。 */
function placeholderFor(f: FieldDescriptor): string {
  const base = f.description || f.name
  if (f.type === 'array') return `${base}（填 JSON，如 ["a"]）`
  if (f.type === 'object') return `${base}（填 JSON，如 {"k":1}）`
  return base
}
</script>

<template>
  <div class="tp" data-testid="tool-playground">
    <!-- 左：工具清单 -->
    <div class="tp-list">
      <div v-if="loading" class="tp-hint">加载工具清单...</div>
      <div v-else-if="tools.length === 0" class="tp-hint" data-testid="tp-empty">
        该服务器未暴露工具（或未连接）
      </div>
      <button
        v-for="t in tools"
        :key="t.fullName"
        type="button"
        class="tp-item"
        :class="{ active: selectedName === t.name }"
        :data-testid="`tp-tool-${t.name}`"
        @click="selectTool(t)"
      >
        <span class="tp-item-name">{{ t.name }}</span>
        <span class="tp-item-desc">{{ t.description || '（无描述）' }}</span>
      </button>
    </div>

    <!-- 右：参数表单 + 结果 -->
    <div class="tp-main">
      <div v-if="!selected" class="tp-hint" data-testid="tp-noselection">
        <span v-if="tools.length > 0">从左侧选一个工具，按它的参数说明发起调用</span>
        <span v-else>无可用工具</span>
      </div>

      <template v-else>
        <div class="tp-head">
          <span class="tp-title" data-testid="tp-selected">{{ selected.name }}</span>
          <el-tag size="small" type="info">{{ selected.fullName }}</el-tag>
          <el-button size="small" text @click="toggleMode">
            {{ jsonMode ? '切回表单模式' : 'JSON 模式' }}
          </el-button>
        </div>
        <p v-if="selected.description" class="tp-desc">{{ selected.description }}</p>

        <!-- 表单模式 -->
        <div v-if="!jsonMode" class="tp-form" data-testid="tp-form">
          <div v-if="fields.length === 0" class="tp-hint">
            该工具没声明参数（inputSchema 为空），可直接调用
          </div>
          <div v-for="f in fields" :key="f.name" class="tp-field" :data-testid="`tp-field-${f.name}`">
            <label class="tp-label">
              <span>{{ f.name }}</span>
              <span class="tp-type">{{ typeLabel(f) }}</span>
              <span v-if="f.required" class="tp-required" :data-testid="`tp-required-${f.name}`">必填</span>
            </label>

            <el-select
              v-if="f.type === 'enum'"
              v-model="values[f.name]"
              size="small"
              :placeholder="f.description || '请选择'"
            >
              <el-option v-for="v in f.enumValues ?? []" :key="String(v)" :label="String(v)" :value="v" />
            </el-select>

            <el-switch v-else-if="f.type === 'boolean'" v-model="values[f.name] as boolean" size="small" />

            <el-input-number
              v-else-if="f.type === 'number' || f.type === 'integer'"
              v-model="values[f.name] as number"
              size="small"
              :precision="f.type === 'integer' ? 0 : undefined"
              :placeholder="f.description || f.name"
            />

            <el-input
              v-else
              v-model="values[f.name] as string"
              size="small"
              :type="f.type === 'array' || f.type === 'object' ? 'textarea' : 'text'"
              :rows="2"
              :placeholder="placeholderFor(f)"
            />
            <p v-if="f.description" class="tp-field-desc">{{ f.description }}</p>
          </div>
        </div>

        <!-- JSON 模式 -->
        <div v-else class="tp-json">
          <el-input v-model="jsonText" type="textarea" :rows="6" placeholder='{"key":"value"}' />
        </div>

        <div class="tp-preview">
          <span class="tp-preview-label">将发送：</span>
          <code data-testid="tp-preview">{{ previewJson }}</code>
        </div>

        <div class="tp-actions">
          <el-button
            type="primary"
            size="small"
            :loading="invoking"
            data-testid="tp-invoke"
            @click="invoke"
          >
            发起调用
          </el-button>
          <span v-if="!jsonMode && missing.length > 0" class="tp-warn">
            必填未填：{{ missing.join('、') }}
          </span>
        </div>

        <!-- 结果面板 -->
        <div v-if="errorText" class="tp-error" data-testid="tp-error">{{ errorText }}</div>
        <div v-if="result" class="tp-result" data-testid="tp-result">
          <div class="tp-result-head">
            <el-tag size="small" :type="result.ok ? 'success' : 'danger'">
              {{ result.isError ? '工具返回 isError' : result.ok ? '成功' : '失败' }}
            </el-tag>
            <span class="tp-elapsed" data-testid="tp-elapsed">{{ result.elapsedMs }} ms</span>
            <el-button size="small" text @click="showRaw = !showRaw">
              {{ showRaw ? '隐藏原始 JSON' : '查看原始 JSON' }}
            </el-button>
          </div>
          <pre class="tp-result-text" data-testid="tp-result-text">{{ result.text || '（无文本内容）' }}</pre>
          <pre v-if="showRaw" class="tp-result-raw" data-testid="tp-result-raw">{{ result.rawJson }}</pre>
        </div>
      </template>
    </div>
  </div>
</template>

<style scoped>
.tp {
  display: flex;
  gap: 12px;
  min-height: 320px;
  color: var(--el-text-color-primary);
}

.tp-list {
  width: 220px;
  flex-shrink: 0;
  max-height: 460px;
  overflow-y: auto;
  border-right: 1px solid var(--el-border-color-lighter);
  padding-right: 8px;
}

.tp-item {
  display: flex;
  flex-direction: column;
  gap: 2px;
  width: 100%;
  text-align: left;
  padding: 6px 8px;
  margin-bottom: 4px;
  border: 1px solid transparent;
  border-radius: 6px;
  background: transparent;
  color: inherit;
  cursor: pointer;
}

.tp-item:hover {
  background: var(--el-fill-color-light);
}

.tp-item.active {
  border-color: var(--el-color-primary);
  background: var(--el-color-primary-light-9);
}

.tp-item-name {
  font-size: 13px;
  font-weight: 600;
  word-break: break-all;
}

.tp-item-desc {
  font-size: 11px;
  color: var(--el-text-color-secondary);
  display: -webkit-box;
  -webkit-line-clamp: 2;
  line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.tp-main {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 8px;
  max-height: 460px;
  overflow-y: auto;
}

.tp-hint {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  padding: 8px 0;
}

.tp-head {
  display: flex;
  align-items: center;
  gap: 8px;
}

.tp-title {
  font-size: 14px;
  font-weight: 600;
  word-break: break-all;
}

.tp-desc {
  margin: 0;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.tp-form {
  display: flex;
  flex-direction: column;
  gap: 10px;
  flex-shrink: 0;
}

.tp-field {
  display: flex;
  flex-direction: column;
  gap: 4px;
  flex-shrink: 0;
}

.tp-label {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
}

.tp-type {
  color: var(--el-text-color-secondary);
  font-family: ui-monospace, monospace;
}

.tp-required {
  color: var(--el-color-danger);
  font-size: 11px;
}

.tp-field-desc {
  margin: 0;
  font-size: 11px;
  color: var(--el-text-color-secondary);
}

.tp-preview {
  font-size: 11px;
  color: var(--el-text-color-secondary);
  flex-shrink: 0;
}

.tp-preview code {
  word-break: break-all;
  background: var(--el-fill-color-light);
  padding: 1px 4px;
  border-radius: 3px;
}

.tp-actions {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-shrink: 0;
}

.tp-warn {
  font-size: 12px;
  color: var(--el-color-warning);
}

.tp-error {
  font-size: 12px;
  color: var(--el-color-danger);
  word-break: break-all;
  flex-shrink: 0;
}

.tp-result {
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
  padding: 8px;
  flex-shrink: 0;
}

.tp-result-head {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 6px;
}

.tp-elapsed {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.tp-result-text,
.tp-result-raw {
  margin: 0;
  max-height: 180px;
  overflow: auto;
  white-space: pre-wrap;
  word-break: break-all;
  font-size: 12px;
  background: var(--el-fill-color-light);
  padding: 6px;
  border-radius: 4px;
}

.tp-result-raw {
  margin-top: 6px;
  max-height: 140px;
}
</style>
