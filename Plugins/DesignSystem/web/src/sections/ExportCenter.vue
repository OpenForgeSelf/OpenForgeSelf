<script setup lang="ts">
/**
 * 导出交付中心（FR12 / AC12 / AC16）。
 *
 * 铁律：**导出的是后端投影产物，界面不参与任何计算**。
 * 格式清单来自 `api.exportFormats(projectId)`（不写死列表）；每个格式的文本来自 `api.exportText(...)`；
 * 下载直链来自 `api.exportUrl(...)`。前端一个字节都不生成——这正是 v1 "界面好看、导出另一套" 的根因所在。
 * 预览失败必须显示状态码与后端原文（操作失败必须可查）。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { api, type ImportPlanView, type ImportResultView } from '../api'
import { mapLimit } from '../design/pool'
import { ApiError } from '../http'
import { currentProject, invalidate, meta, supports, themeCode, themes, unauthorized } from '../state'
import PanelState from '../components/PanelState.vue'

interface FormatRow {
  name: string
  bundle: boolean
  text: string
  error: string
  loading: boolean
  expanded: boolean
}

const PREVIEW_LIMIT = 700
/**
 * 批量读回同时在途的请求数上限。这一页一次要读十几种格式预览 + 十类实体行数，
 * 全并发会把宿主的 SQLite 顶出 `database is locked`（随机一个请求变 500）。
 */
const READ_LIMIT = 3

const projectionVersion = ref('')
const rows = ref<FormatRow[]>([])
const loading = ref(false)
const err = ref('')
const selTheme = ref<string>('shared')

/** 主题下拉：始终含 `shared`（跨主题基线），其余来自项目主题清单 */
const themeOptions = computed(() => ['shared', ...themes.value.map((t) => t.code)])

function themeParam(): string | undefined {
  return selTheme.value === 'shared' ? undefined : selTheme.value
}

function urlOf(row: FormatRow): string {
  const project = currentProject.value
  return project ? api.exportUrl(project.id, row.name, themeParam()) : ''
}

function errorText(e: unknown): string {
  return e instanceof ApiError ? `${e.status} ${e.message}` : String(e)
}

/**
 * 十类逻辑实体的只读插座（`GET api/design-system/{id}/{entity}.json`）。
 * 实体名一律来自 `GET /meta.entities`（后端 `ExportService.StardustEntities`），界面不另列一份；
 * 表里的「行数」是本页刚才真读回的 `total`——链接摆出来但不读，就只是"看起来能用"。
 */
const entityReads = ref<Record<string, { total: number | null; error: string }>>({})
const entityKey = (entity: string): string => `${entity}@${selTheme.value}`

const entityRows = computed(() =>
  (meta.value?.entities ?? []).map((entity) => {
    const read = entityReads.value[entityKey(entity)]
    const project = currentProject.value
    return {
      entity,
      url: project ? api.entityUrl(project.id, entity, themeParam()) : '',
      total: read?.total ?? null,
      error: read?.error ?? '',
    }
  }),
)

async function readEntities(): Promise<void> {
  const project = currentProject.value
  if (!project) return
  await mapLimit(meta.value?.entities ?? [], READ_LIMIT, async (entity) => {
    try {
      const view = await api.entity(project.id, entity, themeParam())
      entityReads.value[entityKey(entity)] = { total: view.total, error: '' }
    } catch (e) {
      entityReads.value[entityKey(entity)] = { total: null, error: errorText(e) }
    }
  })
}

async function loadPreview(row: FormatRow): Promise<void> {
  const project = currentProject.value
  if (!project || row.bundle) return
  row.loading = true
  row.error = ''
  try {
    row.text = await api.exportText(project.id, row.name, themeParam())
  } catch (e) {
    row.error = errorText(e)
    row.text = ''
  } finally {
    row.loading = false
  }
}

/**
 * 换主题 / 换项目后把所有读回一起刷新：格式预览与十类实体的行数都不能留旧值。
 * 分两阶段、每阶段限流：这一页光预览就有十几种格式，全并发会把宿主那个 SQLite 库顶出
 * `database is locked`（v2.6.8 的 e2e 实测：随机某个请求 500，界面那格就显示"读取失败"）。
 */
async function reloadReads(): Promise<void> {
  await mapLimit(rows.value, READ_LIMIT, loadPreview)
  await readEntities()
}

/* ---------------------------------------------------------------------------
 * 导入 / 回流（M13）：选文件 → 后端算差异 → 确认写入。
 * 前端**不解析 DTCG、不自算差异**：差异算第二遍，就等于给同一件事写第二份真相，必然与后端漂移。
 * ------------------------------------------------------------------------ */
const importDoc = ref<Record<string, unknown> | null>(null)
const importFileName = ref('')
const importPlan = ref<ImportPlanView | null>(null)
const importResult = ref<ImportResultView | null>(null)
const importErr = ref('')
const importBusy = ref(false)
const importOverwrite = ref(false)

/** 首期格式清单只有一项：界面取后端给的第一项，不写死 "dtcg" */
const importFormat = computed(() => meta.value?.importFormats?.[0] ?? '')
const importSupported = computed(() => importFormat.value !== '' && supports('import'))
const importLimitsText = computed(() => {
  const l = meta.value?.importLimits
  return l ? `${l.maxEntries} 条 / ${Math.round(l.maxBytes / 1024)} KiB` : '上限未知'
})
/** 预览表只摊前若干行：一次导入上千条时，全渲染会把这一页变成滚动条地狱 */
const IMPORT_PREVIEW_ROWS = 30
const importWillWrite = computed(() => importPlan.value?.willWrite.slice(0, IMPORT_PREVIEW_ROWS) ?? [])
const importWriteCount = computed(() => (importPlan.value ? importPlan.value.counts.entries - importPlan.value.counts.rejected : 0))

function importQuery(): { format?: string; theme?: string; overwrite?: boolean } {
  return { format: importFormat.value, theme: themeParam(), overwrite: importOverwrite.value }
}

async function previewImport(): Promise<void> {
  const project = currentProject.value
  if (!project || !importDoc.value) return
  importBusy.value = true
  importErr.value = ''
  try {
    importPlan.value = await api.importPreview(project.id, importDoc.value, importQuery())
  } catch (e) {
    importPlan.value = null
    importErr.value = errorText(e)
  } finally {
    importBusy.value = false
  }
}

async function onPickImport(ev: Event): Promise<void> {
  const file = (ev.target as HTMLInputElement).files?.[0]
  importErr.value = ''
  importResult.value = null
  importPlan.value = null
  if (!file) {
    importDoc.value = null
    importFileName.value = ''
    return
  }
  importFileName.value = file.name
  try {
    importDoc.value = JSON.parse(await file.text()) as Record<string, unknown>
  } catch (e) {
    importDoc.value = null
    importErr.value = `文件不是合法 JSON：${errorText(e)}`
    return
  }
  await previewImport()
}

async function confirmImport(): Promise<void> {
  const project = currentProject.value
  if (!project || !importDoc.value) return
  importBusy.value = true
  importErr.value = ''
  try {
    importResult.value = await api.import(project.id, importDoc.value, importQuery())
    // 写完立刻自证：清缓存重拉有效值 + 重读预览，界面看到的就该是库里的现在值
    await invalidate()
    await reloadReads()
  } catch (e) {
    importResult.value = null
    importErr.value = errorText(e)
  } finally {
    importBusy.value = false
  }
}

async function load(): Promise<void> {
  const project = currentProject.value
  rows.value = []
  entityReads.value = {}
  err.value = ''
  projectionVersion.value = ''
  if (!project) return
  if (!supports('export')) {
    err.value = '后端未声明 export 能力，导出中心不可用（见 meta.capabilities）。'
    return
  }
  loading.value = true
  try {
    const formats = await api.exportFormats(project.id)
    projectionVersion.value = formats.projectionVersion
    rows.value = formats.formats.map((f) => ({ name: f.name, bundle: f.bundle, text: '', error: '', loading: false, expanded: false }))
  } catch (e) {
    err.value = errorText(e)
  } finally {
    loading.value = false
  }
  await reloadReads()
}

function toggle(row: FormatRow): void {
  row.expanded = !row.expanded
}

watch(() => currentProject.value?.id, () => void load())
watch(selTheme, () => void reloadReads())
onMounted(() => {
  selTheme.value = themeCode.value || 'shared'
  void load()
})
</script>

<template>
  <section class="ec">
    <header class="ec__bar">
      <label class="ec__picker">
        <span class="ds-micro">主题</span>
        <select v-model="selTheme" aria-label="导出主题" class="ds-input ds-input--narrow">
          <option v-for="t in themeOptions" :key="t" :value="t">{{ t }}</option>
        </select>
      </label>
      <div class="ds-micro ec__meta">
        投影版本 <strong>{{ projectionVersion || '—' }}</strong> · 格式 <strong>{{ rows.length }}</strong>
        · <button class="ds-link" type="button" @click="load">刷新</button>
      </div>
    </header>

    <p class="ds-small ec__note">
      下列每一个工件都由后端投影生成（<code class="ds-mono">export?format=&amp;theme=</code>），
      界面只负责读回与展示，<strong>不参与任何设计值计算</strong>；预览截断处可「看全文」。
    </p>

    <p v-if="err" class="ec__err" role="alert">{{ err }}</p>

    <PanelState v-if="unauthorized" state="unauthorized" />
    <PanelState v-else-if="!currentProject" state="empty" title="还没有选中设计系统项目" hint="先到「项目与生成」页选一个项目，导出中心按项目读取投影工件。" />
    <PanelState v-else-if="loading && !rows.length" state="loading" />
    <PanelState v-else-if="!err && !rows.length" state="empty" title="后端未返回任何导出格式" hint="检查 design-system 插件是否已加载并声明 export 能力。" />

    <div v-else class="ec__list">
      <article v-for="row in rows" :key="row.name" class="ec__card ds-surface">
        <header class="ec__card-head">
          <h3 class="ds-h4">{{ row.name }}</h3>
          <div class="ec__ops">
            <a class="ec__dl" :href="urlOf(row)" download>下载</a>
          </div>
        </header>

        <p v-if="row.bundle" class="ds-small ec__zip">
          单文件压缩包（zip，含全部格式 + 各主题投影 + SHA 清单）：只下载，不做文本预览。
        </p>

        <template v-else>
          <p v-if="row.loading" class="ds-small ec__loading">正在读取后端投影文本…</p>
          <p v-else-if="row.error" class="ec__err" role="alert">预览失败：{{ row.error }}</p>
          <div v-else class="ec__preview">
            <pre class="ds-mono ec__text" :class="{ 'ec__text--full': row.expanded }">{{ row.expanded ? row.text : row.text.slice(0, PREVIEW_LIMIT) }}</pre>
            <div v-if="row.text.length > PREVIEW_LIMIT" class="ec__trunc">
              <button class="ds-link" type="button" @click="toggle(row)">{{ row.expanded ? '收起' : '看全文' }}</button>
              <span class="ds-micro">共 {{ row.text.length }} 字符</span>
            </div>
          </div>
        </template>
      </article>
    </div>

    <div v-if="currentProject && entityRows.length" class="ec__entities ds-surface">
      <h3 class="ds-h4">
        逻辑实体明细 <span class="ds-micro">{{ entityRows.length }} 类 · 名字来自 <code class="ds-mono">meta.entities</code></span>
      </h3>
      <p class="ds-small ec__note">
        这十类是 <code class="ds-mono">GET api/design-system/{项目id}/{实体}.json</code> 的<strong>只读插座</strong>，
        返回裸 <code class="ds-mono">{entity, source, generated, total, data[]}</code>（不走 success/data 信封）——
        参考物那类实体查看器直连这个形状。「行数」是本页按当前主题<strong>实际读回</strong>的 <code class="ds-mono">total</code>，
        不是清单上的承诺；读失败就把状态码摆在这行，不留空。
      </p>
      <table class="ec__table">
        <thead>
          <tr><th>实体</th><th>url（当前主题）</th><th>行数</th></tr>
        </thead>
        <tbody>
          <tr v-for="e in entityRows" :key="e.entity">
            <td class="ds-mono">{{ e.entity }}</td>
            <td><a class="ec__link ds-mono" :href="e.url" target="_blank" rel="noreferrer">{{ e.url }}</a></td>
            <td class="ds-mono">
              <span v-if="e.total !== null">{{ e.total }}</span>
              <span v-else-if="e.error" class="ec__err" role="alert">{{ e.error }}</span>
              <span v-else>读取中…</span>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
    <div v-if="currentProject" class="ec__import ds-surface">
      <h3 class="ds-h4">
        导入 / 回流
        <span class="ds-micro">格式 {{ importFormat || '（后端未声明）' }} · 上限 {{ importLimitsText }} · 主题 {{ selTheme }}</span>
      </h3>
      <p class="ds-small ec__note">
        导入只做一件事：把外部 <code class="ds-mono">DTCG</code> 文件交给后端解析成待写清单，<strong>差异由后端算</strong>；
        写入走与生成同一个批量入口（整批事务：别名成环 / 悬空引用一律整批不写）。
        手改过的行默认不覆盖 —— 要覆盖请显式勾选下面的开关。导入成功的行来源记为
        <code class="ds-mono">imported</code>，此后重新生成不会再动它们。
      </p>

      <p v-if="!importSupported" class="ec__err" role="alert">
        后端未声明 <code class="ds-mono">import</code> 能力或没有可用格式，导入入口不可用（摆出来点不动才是诚实）。
      </p>

      <div class="ec__import-row">
        <label class="ds-micro ec__import-file">
          选择 DTCG 文件
          <input type="file" accept="application/json,.json" aria-label="选择 DTCG 文件"
                 :disabled="!importSupported" @change="onPickImport" />
        </label>
        <label class="ds-small ec__import-ow">
          <input type="checkbox" aria-label="覆盖手改过的行" v-model="importOverwrite" :disabled="!importSupported" @change="previewImport" />
          覆盖手改过的行（overwrite）
        </label>
        <button class="ds-mini" type="button" :disabled="!importDoc || importBusy || !importSupported" @click="previewImport">重新预览</button>
        <button class="ds-mini" type="button" data-import-confirm :disabled="!importPlan || importBusy || !importSupported" @click="confirmImport">确认写入</button>
      </div>

      <p v-if="importFileName" class="ds-small">
        文件：<span class="ds-mono">{{ importFileName }}</span>
        <span v-if="importPlan?.documentProject"> · 文档声明项目 <span class="ds-mono">{{ importPlan.documentProject }}</span></span>
        <span v-if="importBusy"> · 处理中…</span>
      </p>
      <p v-if="importErr" class="ec__err" role="alert">{{ importErr }}</p>

      <template v-if="importPlan">
        <p class="ds-small ec__import-counts" data-import-counts>
          将写入 <strong>{{ importWriteCount }}</strong> 条
          （别名 {{ importPlan.counts.aliases }} · 字面 {{ importPlan.counts.literals }} · 复合 {{ importPlan.counts.composites }}）
          · 被拒 <strong>{{ importPlan.counts.rejected }}</strong>
          · 与手改冲突 <strong>{{ importPlan.counts.conflicts }}</strong>
          <span v-if="importOverwrite && importPlan.counts.conflicts">（已勾选覆盖 → 这些会被改写）</span>
        </p>
        <table class="ec__table">
          <thead>
            <tr><th>路径</th><th>层级</th><th>类型</th><th>主题</th><th>值 / 别名</th></tr>
          </thead>
          <tbody>
            <tr v-for="w in importWillWrite" :key="`${w.themeId}:${w.path}`">
              <td class="ds-mono">{{ w.path }}</td>
              <td class="ds-mono">{{ w.tier }}</td>
              <td class="ds-mono">{{ w.type }}</td>
              <td class="ds-mono">{{ w.themeId === 0 ? 'shared' : w.themeId }}</td>
              <td class="ds-mono">{{ w.alias ? '{' + w.alias + '}' : w.value }}</td>
            </tr>
            <tr v-for="r in importPlan.rejected" :key="`rej:${r.path}`" class="ec__row-reject">
              <td class="ds-mono">{{ r.path }}</td>
              <td colspan="4" class="ec__err">被拒：{{ r.reason }}</td>
            </tr>
          </tbody>
        </table>
        <p v-if="importPlan.willWrite.length > importWillWrite.length" class="ds-small ec__note">
          表里只摊前 {{ importWillWrite.length }} 行，实际将写入 {{ importWriteCount }} 条（计数在上一行，不在这里重复）。
        </p>
      </template>

      <div v-if="importResult" class="ds-small ec__import-result" data-import-result>
        写入结果：新增 <strong>{{ importResult.created }}</strong> · 更新 <strong>{{ importResult.updated }}</strong>
        · 受保护跳过 <strong>{{ importResult.skippedProtected }}</strong>
        <span v-if="importResult.diagnostics.length" class="ec__err">
          · 整批已回滚（{{ importResult.diagnostics.length }} 条诊断）：<span class="ds-mono">{{ importResult.diagnostics[0].message }}</span>
        </span>
        <span v-else class="ec__ok"> · {{ importResult.auditHint }}</span>
      </div>
    </div>
  </section>
</template>

<style scoped>
.ec {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-4);
}
.ec__bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--ds-space-4);
  flex-wrap: wrap;
}
.ec__picker {
  display: flex;
  align-items: center;
  gap: var(--ds-space-2);
}
.ec__meta {
  color: var(--ds-fg-3);
}
.ec__note {
  color: var(--ds-fg-3);
}
.ec__note code,
.ec__zip code {
  color: var(--ds-fg-2);
}
.ec__list {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(360px, 1fr));
  gap: var(--ds-space-4);
}
.ec__card {
  padding: var(--ds-space-4);
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-3);
}
.ec__card-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--ds-space-3);
}
.ec__ops {
  display: flex;
  align-items: center;
  gap: var(--ds-space-2);
}
.ec__dl {
  font-size: var(--ds-fs-micro);
  color: var(--ds-surface-1);
  background: var(--ds-color-primary);
  border: 1px solid var(--ds-color-primary);
  border-radius: var(--ds-radius-sm);
  padding: 4px var(--ds-space-3);
  text-decoration: none;
  cursor: pointer;
}
.ec__zip {
  color: var(--ds-fg-3);
}
.ec__loading {
  color: var(--ds-fg-3);
}
.ec__err {
  color: var(--ds-danger);
  font-size: var(--ds-fs-small);
}
.ec__preview {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-2);
}
.ec__text {
  margin: 0;
  max-height: 220px;
  overflow: auto;
  background: var(--ds-surface-2);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-sm);
  padding: var(--ds-space-3);
  font-size: var(--ds-fs-small);
  white-space: pre-wrap;
  word-break: break-word;
}
.ec__text--full {
  max-height: 460px;
}
.ec__trunc {
  display: flex;
  align-items: center;
  gap: var(--ds-space-2);
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
.ec__entities {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-3);
  padding: var(--ds-space-4);
}
.ec__table {
  width: 100%;
  border-collapse: collapse;
  font-size: var(--ds-fs-small);
}
.ec__table th {
  text-align: left;
  color: var(--ds-fg-3);
  font-weight: 500;
  border-bottom: 1px solid var(--ds-border-1);
  padding: 4px var(--ds-space-2);
}
.ec__table td {
  border-bottom: 1px solid var(--ds-border-1);
  padding: 4px var(--ds-space-2);
  vertical-align: top;
  word-break: break-all;
}
.ec__link {
  color: var(--ds-color-primary);
  text-decoration: none;
}
.ec__link:hover {
  text-decoration: underline;
}
/* 导入 / 回流：与导出台同一套版式（同一页的两半不该长两种样子） */
.ec__import {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-3);
  margin-top: var(--ds-space-5);
  padding: var(--ds-space-4);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-md);
}
.ec__import-row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--ds-space-4);
}
.ec__import-file {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-1);
  color: var(--ds-fg-3);
}
.ec__import-ow {
  display: flex;
  align-items: center;
  gap: var(--ds-space-2);
  color: var(--ds-fg-2);
}
.ec__import-counts strong {
  color: var(--ds-fg-1);
}
.ec__import-result {
  padding: var(--ds-space-3);
  border-radius: var(--ds-radius-sm);
  background: var(--ds-surface-2);
}
.ec__row-reject td {
  color: var(--ds-warning);
}
.ec__ok {
  color: var(--ds-success);
}
/* 导入区的两个动作按钮：与其余 section 同一套按钮形状（scoped 样式按文件自带，classes 守卫按文件核对） */
.ds-mini {
  font: inherit;
  font-size: var(--ds-fs-micro);
  color: var(--ds-fg-2);
  background: var(--ds-surface-2);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-sm);
  padding: 4px 10px;
  cursor: pointer;
}
.ds-mini:disabled {
  opacity: 0.45;
  cursor: not-allowed;
}
</style>
