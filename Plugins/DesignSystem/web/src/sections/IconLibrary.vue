<script setup lang="ts">
/**
 * 图标库（FR10 / AC16）：读取后端图标库（内置集合 `forge` + 项目自绘图标），
 * 支持搜索、按集合过滤、向项目导入自定义图标。
 *
 * 内置零许可证负担集合单独成区并明确标注 license 值——用户要求：插件必须自带一套
 * 无许可证负担的图标，允许用户再导入自己的（自定义图标可带自己的 license）。
 *
 * SVG 渲染：用 `v-html` 注入后端返回的 `svgBody`，并固定包一层带 viewBox 的 `<svg>`。
 * ⚠️ svgBody 来自本插件自己的库（内置是自绘 path 数据，项目图标是用户导入后落库的行），
 *    **不是任意用户输入**；因此渲染时不接宿主/第三方可控内容。路径用 currentColor，由令牌着色。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { api, type Icon } from '../api'
import { ApiError } from '../http'
import { currentProject, unauthorized } from '../state'
import { matchesKeyword } from '../design/derive'
import PanelState from '../components/PanelState.vue'

/** 内置图标库的项目 ID 哨兵（与后端 DesignSystemConstants.BuiltinProjectId 一致） */
const BUILTIN_PROJECT_ID = 0

const all = ref<Icon[]>([])
const loading = ref(false)
const err = ref('')
const keyword = ref('')
const collectionFilter = ref('')

function passFilter(icon: Icon): boolean {
  if (collectionFilter.value && icon.collection !== collectionFilter.value) return false
  const k = keyword.value.trim()
  if (!k) return true
  return (
    matchesKeyword(icon.code, k) ||
    matchesKeyword(icon.name ?? '', k) ||
    matchesKeyword(icon.collection, k) ||
    matchesKeyword(icon.tags ?? '', k)
  )
}

const collections = computed(() => [...new Set(all.value.map((i) => i.collection).filter(Boolean))].sort())
const visibleBuiltin = computed(() => all.value.filter((i) => i.projectId === BUILTIN_PROJECT_ID && passFilter(i)))
const visibleProject = computed(() => all.value.filter((i) => i.projectId !== BUILTIN_PROJECT_ID && passFilter(i)))
const builtinLicenses = computed(() => [...new Set(visibleBuiltin.value.map((i) => i.license ?? '未标注'))])

function errorText(e: unknown): string {
  return e instanceof ApiError ? `${e.status} ${e.message}` : String(e)
}

async function load(): Promise<void> {
  loading.value = true
  err.value = ''
  try {
    all.value = await api.listIcons(currentProject.value?.id ?? BUILTIN_PROJECT_ID)
  } catch (e) {
    err.value = errorText(e)
  } finally {
    loading.value = false
  }
}

/* ---------------------------------- 导入表单 ---------------------------------- */
const emptyForm = { code: '', name: '', collection: 'custom', svgBody: '', strokeWidth: '1.5', gridPx: '24', license: '' }
const form = ref({ ...emptyForm })
const saving = ref(false)
const saveErr = ref('')
const saveHint = ref('')

/** 写项目图标：后端会拒绝对内置项目（BuiltinProjectId=0）写入，错误原文照实显示 */
async function submit(): Promise<void> {
  const project = currentProject.value
  saveErr.value = ''
  saveHint.value = ''
  if (!project) {
    saveErr.value = '未选中项目，无法写入（内置库只读，须落到某个项目）。'
    return
  }
  if (!form.value.code.trim()) {
    saveErr.value = 'code 不能为空。'
    return
  }
  saving.value = true
  try {
    await api.saveIcon(project.id, {
      code: form.value.code.trim(),
      name: form.value.name.trim() || undefined,
      collection: form.value.collection.trim() || 'custom',
      svgBody: form.value.svgBody,
      strokeWidth: Number(form.value.strokeWidth) || 1.5,
      gridPx: Number(form.value.gridPx) || 24,
      license: form.value.license.trim() || undefined,
    })
    saveHint.value = `已写入项目 ${project.code}：${form.value.code.trim()}（已重新拉取列表）`
    form.value = { ...emptyForm }
    await load()
  } catch (e) {
    saveErr.value = errorText(e)
  } finally {
    saving.value = false
  }
}

watch(() => currentProject.value?.id, () => void load())
onMounted(() => void load())
</script>

<template>
  <section class="il">
    <header class="il__bar">
      <div class="il__filters">
        <input v-model="keyword" class="ds-input" type="search" placeholder="按 code / 名称 / 标签检索" />
        <select v-model="collectionFilter" class="ds-input ds-input--narrow" aria-label="集合">
          <option value="">全部集合</option>
          <option v-for="c in collections" :key="c" :value="c">{{ c }}</option>
        </select>
      </div>
      <div class="ds-micro il__count">
        内置 <strong>{{ visibleBuiltin.length }}</strong> · 项目 <strong>{{ visibleProject.length }}</strong>
        · <button class="ds-link" type="button" @click="load">刷新</button>
      </div>
    </header>

    <p v-if="err" class="il__err" role="alert">{{ err }}</p>

    <PanelState v-if="unauthorized" state="unauthorized" />
    <PanelState v-else-if="loading && !all.length" state="loading" />
    <PanelState v-else-if="err && !all.length" state="error" :title="err" hint="图标端点读取失败；检查宿主是否已加载 design-system 插件并已登录。" />
    <PanelState v-else-if="!all.length" state="empty" title="图标库为空" hint="内置集合为空时，可先向项目导入自定义图标；或确认后端已首植内置 `forge` 集合。" />

    <template v-else>
      <!-- 内置零许可证负担集合 -->
      <div v-if="visibleBuiltin.length" class="il__group">
        <div class="ds-section-title">
          <h3 class="ds-h3">内置集合 · 零许可证负担</h3>
          <span class="il__badge">license: {{ builtinLicenses.join(' / ') }}</span>
        </div>
        <p class="ds-small il__note">
          这些图标随插件自带（collection=<code class="ds-mono">forge</code>，项目 id={{ BUILTIN_PROJECT_ID }}），
          后端将其视为<strong>只读</strong>：任何写入都会被拒绝。可在下方把它们复制/改写后导入到你的项目。
        </p>
        <div class="il__grid">
          <div v-for="icon in visibleBuiltin" :key="icon.id" class="il__tile ds-surface-2">
            <div class="il__glyph">
              <svg
                :viewBox="icon.viewBox || '0 0 24 24'"
                fill="none"
                stroke="currentColor"
                :stroke-width="icon.strokeWidth || 1.5"
                stroke-linecap="round"
                stroke-linejoin="round"
                role="img"
                :aria-label="icon.name || icon.code"
                v-html="icon.svgBody || ''"
              ></svg>
            </div>
            <div class="il__meta">
              <div class="ds-mono il__code">{{ icon.code }}</div>
              <div class="ds-small il__name">{{ icon.name || '—' }}</div>
              <div class="ds-micro il__tags" v-if="icon.tags">{{ icon.tags }}</div>
            </div>
          </div>
        </div>
      </div>

      <!-- 项目自定义图标 -->
      <div class="il__group">
        <div class="ds-section-title">
          <h3 class="ds-h3">项目图标</h3>
          <span class="ds-micro">{{ currentProject ? currentProject.name : '未选中项目' }}</span>
        </div>

        <PanelState v-if="!currentProject" state="empty" title="未选中项目" hint="在顶部选择一个设计系统项目后，这里显示其自定义图标，并可导入新图标。" />
        <PanelState v-else-if="!visibleProject.length" state="empty" title="该项目暂无自定义图标" hint="用下面的表单导入；或用「导入自己的图标」把项目专属图标写入库。" />

        <div v-else class="il__grid">
          <div v-for="icon in visibleProject" :key="icon.id" class="il__tile ds-surface-2">
            <div class="il__glyph">
              <svg
                :viewBox="icon.viewBox || '0 0 24 24'"
                fill="none"
                stroke="currentColor"
                :stroke-width="icon.strokeWidth || 1.5"
                stroke-linecap="round"
                stroke-linejoin="round"
                role="img"
                :aria-label="icon.name || icon.code"
                v-html="icon.svgBody || ''"
              ></svg>
            </div>
            <div class="il__meta">
              <div class="ds-mono il__code">{{ icon.code }}</div>
              <div class="ds-small il__name">{{ icon.name || '—' }}</div>
              <dl class="il__fields">
                <div><dt>collection</dt><dd class="ds-mono">{{ icon.collection }}</dd></div>
                <div><dt>grid</dt><dd class="ds-mono">{{ icon.gridPx }}px</dd></div>
                <div><dt>stroke</dt><dd class="ds-mono">{{ icon.strokeWidth }}</dd></div>
                <div><dt>viewBox</dt><dd class="ds-mono">{{ icon.viewBox || '0 0 24 24' }}</dd></div>
                <div><dt>license</dt><dd class="ds-mono">{{ icon.license || '未标注' }}</dd></div>
                <div v-if="icon.tags"><dt>tags</dt><dd class="ds-mono">{{ icon.tags }}</dd></div>
              </dl>
            </div>
          </div>
        </div>

        <!-- 导入表单 -->
        <div v-if="currentProject" class="il__form ds-surface">
          <h4 class="ds-h4">导入自定义图标 → 项目 <span class="ds-mono">{{ currentProject.code }}</span></h4>
          <div class="il__form-grid">
            <label class="il__field"><span class="ds-micro">code *</span><input v-model="form.code" class="ds-input" placeholder="如 project-logo" /></label>
            <label class="il__field"><span class="ds-micro">name</span><input v-model="form.name" class="ds-input" placeholder="显示名" /></label>
            <label class="il__field"><span class="ds-micro">collection</span><input v-model="form.collection" class="ds-input" placeholder="custom" /></label>
            <label class="il__field"><span class="ds-micro">stroke-width</span><input v-model="form.strokeWidth" class="ds-input" inputmode="decimal" /></label>
            <label class="il__field"><span class="ds-micro">gridPx</span><input v-model="form.gridPx" class="ds-input" inputmode="numeric" /></label>
            <label class="il__field"><span class="ds-micro">license</span><input v-model="form.license" class="ds-input" placeholder="如 MIT / Owned" /></label>
          </div>
          <label class="il__field il__field--wide">
            <span class="ds-micro">svgBody（仅内部元素，如 &lt;path d="..."/&gt;）</span>
            <textarea v-model="form.svgBody" class="ds-input il__svg-input" rows="3" placeholder="<path d='M4 4h16v16H4z'/>"></textarea>
          </label>
          <p v-if="saveErr" class="il__err" role="alert">{{ saveErr }}</p>
          <p v-if="saveHint" class="il__hint">{{ saveHint }}</p>
          <div class="il__form-ops">
            <button class="ds-mini ds-mini--primary" :disabled="saving" @click="submit">{{ saving ? '写入中…' : '保存到项目' }}</button>
            <span class="ds-micro il__hint-readonly">内置库（id={{ BUILTIN_PROJECT_ID }}）只读——若把 collection 改成内置集合并试图回写，后端将拒绝并在此显示原文。</span>
          </div>
        </div>
      </div>
    </template>
  </section>
</template>

<style scoped>
.il {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-5);
}
.il__bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--ds-space-4);
  flex-wrap: wrap;
}
.il__filters {
  display: flex;
  gap: var(--ds-space-2);
  flex-wrap: wrap;
}
.il__count {
  color: var(--ds-fg-3);
}
.il__group {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-3);
}
.il__note {
  color: var(--ds-fg-3);
}
.il__badge {
  font-size: var(--ds-fs-micro);
  padding: 1px 8px;
  border-radius: var(--ds-radius-pill);
  background: var(--ds-success);
  color: var(--ds-surface-1);
}
.il__grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
  gap: var(--ds-space-3);
}
.il__tile {
  display: flex;
  align-items: center;
  gap: var(--ds-space-3);
  padding: var(--ds-space-3);
}
.il__glyph {
  width: 40px;
  height: 40px;
  flex: none;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  color: var(--ds-color-primary);
  background: var(--ds-surface-1);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-md);
}
.il__glyph svg {
  width: 24px;
  height: 24px;
}
.il__meta {
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.il__code {
  overflow: hidden;
  text-overflow: ellipsis;
}
.il__tags {
  color: var(--ds-fg-4);
}
.il__fields {
  margin: var(--ds-space-1) 0 0;
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 1px var(--ds-space-3);
}
.il__fields > div {
  display: flex;
  gap: 4px;
  align-items: baseline;
}
.il__fields dt {
  color: var(--ds-fg-4);
  font-size: var(--ds-fs-micro);
}
.il__fields dd {
  margin: 0;
  font-size: var(--ds-fs-micro);
}
.il__form {
  padding: var(--ds-space-5);
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-3);
}
.il__form-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: var(--ds-space-3);
}
.il__field {
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.il__field--wide {
  grid-column: 1 / -1;
}
.il__svg-input {
  font-family: var(--ds-font-mono);
  resize: vertical;
}
.il__form-ops {
  display: flex;
  align-items: center;
  gap: var(--ds-space-3);
  flex-wrap: wrap;
}
.il__hint-readonly {
  color: var(--ds-fg-4);
}
.il__err {
  color: var(--ds-danger);
  font-size: var(--ds-fs-small);
}
.il__hint {
  color: var(--ds-success);
  font-size: var(--ds-fs-small);
}
.ds-input {
  font: inherit;
  font-size: var(--ds-fs-small);
  color: var(--ds-fg-1);
  background: var(--ds-surface-1);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-sm);
  padding: 6px var(--ds-space-3);
}
.ds-input--narrow {
  width: auto;
}
.ds-mini {
  font: inherit;
  font-size: var(--ds-fs-micro);
  color: var(--ds-fg-2);
  background: var(--ds-surface-2);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-sm);
  padding: 6px var(--ds-space-3);
  cursor: pointer;
}
.ds-mini--primary {
  color: var(--ds-surface-1);
  background: var(--ds-color-primary);
  border-color: var(--ds-color-primary);
}
.ds-mini:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
</style>
