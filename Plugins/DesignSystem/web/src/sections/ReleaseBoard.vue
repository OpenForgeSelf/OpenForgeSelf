<script setup lang="ts">
/**
 * 版本与对比（FR14 / AC11 / AC14 / AC16）。
 *
 * - 列表读 `api.listReleases`；新建走 `api.createRelease`；**409 必须原样显示后端原因**并翻成"人话"
 *   （发布门禁 = 审计面板里存在未通过的 critical 项）；
 * - 两版对比走 `api.diffReleases`，分 added / removed / changed（按 主题+路径 分组，含 field/from/to）
 *   + missingThemes + total；`isEmpty` 时明说"两版内容一致"；
 * - 单版快照走 `api.releaseTokens`（令牌数 + 前 N 条）与 `api.releaseDtcg`（该版本 DTCG 全文预览）。
 * 所有数字（令牌数、哈希、差异）都来自后端，前端一个都不算。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { api, type Release, type ReleaseDiff, type ReleaseToken, type TokenChange } from '../api'
import { ApiError } from '../http'
import { currentProject, supports, themeCode, themes, unauthorized } from '../state'
import { nextVersion } from '../design/derive'
import PanelState from '../components/PanelState.vue'

type BumpKind = 'major' | 'minor' | 'patch'

const releases = ref<Release[]>([])
const loading = ref(false)
const err = ref('')

/* ------------------------------- 新建发布 ------------------------------- */
const kind = ref<BumpKind>('patch')
const version = ref('')
const notes = ref('')
const creating = ref(false)
const createErr = ref('')
const createPlain = ref('')
const createHint = ref('')

function suggest(): string {
  return nextVersion(currentProject.value?.version ?? '0.0.0', kind.value)
}

/** 把后端 409 原文翻成人话（原文照实显示，人话补充说明门禁语义） */
function plainReason(raw: string): string {
  if (raw.includes('critical')) return '门禁未清：审计面板里仍有未通过的 critical 项，先到「审计与门禁」修掉再来发布。'
  if (raw.includes('递增版本号') || raw.includes('内容不同')) return '版本号占用：Release 快照不可变，同版本号内容不同即冲突——递增版本号后重发。'
  if (raw.includes('还没有任何令牌')) return '空项目：还没有可冻结的令牌，先生成或导入令牌。'
  return '后端拒绝了本次发布，原因见上方原文。'
}

async function loadReleases(): Promise<void> {
  const project = currentProject.value
  err.value = ''
  if (!project) {
    releases.value = []
    return
  }
  loading.value = true
  try {
    releases.value = await api.listReleases(project.id)
  } catch (e) {
    err.value = e instanceof ApiError ? `${e.status} ${e.message}` : String(e)
  } finally {
    loading.value = false
  }
}

async function create(): Promise<void> {
  const project = currentProject.value
  createErr.value = ''
  createPlain.value = ''
  createHint.value = ''
  if (!project) {
    createErr.value = '未选中设计系统项目。'
    return
  }
  if (!version.value.trim()) {
    createErr.value = '版本号不能为空。'
    return
  }
  creating.value = true
  try {
    const rel = await api.createRelease(project.id, version.value.trim(), notes.value.trim() || undefined, 0)
    createHint.value = `已发布 ${rel?.version ?? version.value.trim()}（令牌 ${rel?.tokenCount ?? '—'}）`
    notes.value = ''
    await loadReleases()
  } catch (e) {
    const raw = e instanceof ApiError ? `${e.status} ${e.message}` : String(e)
    createErr.value = raw
    if (e instanceof ApiError && e.isConflict) createPlain.value = plainReason(e.message)
  } finally {
    creating.value = false
  }
}

/* ------------------------------- 版本对比 ------------------------------- */
const fromId = ref<number>(0)
const toId = ref<number>(0)
const diff = ref<ReleaseDiff | null>(null)
const diffing = ref(false)
const diffErr = ref('')

const changedGroups = computed(() => {
  const map = new Map<string, TokenChange[]>()
  for (const c of diff.value?.changed ?? []) {
    const key = `${c.theme} · ${c.path}`
    const arr = map.get(key)
    if (arr) arr.push(c)
    else map.set(key, [c])
  }
  return [...map.entries()].map(([key, items]) => ({ key, items }))
})

/** 规格字段值可能很长（svgBody / tokenRefs 清单），列表里只给可辨认的片段，完整值看快照文件 */
function brief(v: string | null | undefined): string {
  if (v === null || v === undefined) return '∅'
  return v.length > 72 ? `${v.slice(0, 72)}…` : v
}

async function runDiff(): Promise<void> {
  const project = currentProject.value
  diffErr.value = ''
  diff.value = null
  if (!project) return
  if (!fromId.value || !toId.value || fromId.value === toId.value) {
    diffErr.value = '请选择两个不同的版本进行对比。'
    return
  }
  diffing.value = true
  try {
    diff.value = await api.diffReleases(project.id, fromId.value, toId.value)
  } catch (e) {
    diffErr.value = e instanceof ApiError ? `${e.status} ${e.message}` : String(e)
  } finally {
    diffing.value = false
  }
}

/* ------------------------------- 单版快照 ------------------------------- */
const SNAPSHOT_LIMIT = 24
const selReleaseId = ref<number>(0)
const snapshot = ref<{ version: string; generatedAt: string; tokens: ReleaseToken[] } | null>(null)
const snapLoading = ref(false)
const snapErr = ref('')
const dtcgTheme = ref<string>('light')
const dtcg = ref('')
const dtcgLoading = ref(false)
const dtcgErr = ref('')

const snapshotTokens = computed(() => snapshot.value?.tokens ?? [])

async function loadSnapshot(): Promise<void> {
  const project = currentProject.value
  snapErr.value = ''
  snapshot.value = null
  dtcg.value = ''
  if (!project || !selReleaseId.value) return
  snapLoading.value = true
  try {
    snapshot.value = await api.releaseTokens(project.id, selReleaseId.value)
  } catch (e) {
    snapErr.value = e instanceof ApiError ? `${e.status} ${e.message}` : String(e)
  } finally {
    snapLoading.value = false
  }
  dtcgLoading.value = true
  dtcgErr.value = ''
  try {
    const res = await api.releaseDtcg(project.id, selReleaseId.value, dtcgTheme.value)
    dtcg.value = res?.content ?? ''
  } catch (e) {
    dtcgErr.value = e instanceof ApiError ? `${e.status} ${e.message}` : String(e)
  } finally {
    dtcgLoading.value = false
  }
}

const releaseOptions = computed(() => releases.value.map((r) => ({ id: r.id, label: `v${r.version}` })))

watch(kind, () => {
  version.value = suggest()
})
watch(() => currentProject.value?.id, () => {
  void loadReleases()
  version.value = suggest()
  fromId.value = 0
  toId.value = 0
  diff.value = null
  selReleaseId.value = 0
  snapshot.value = null
  dtcg.value = ''
})
watch(selReleaseId, () => void loadSnapshot())
watch(dtcgTheme, () => void loadSnapshot())

onMounted(() => {
  version.value = suggest()
  dtcgTheme.value = themes.value.find((t) => t.modeKind === 'color')?.code ?? themeCode.value ?? 'light'
  void loadReleases()
})
</script>

<template>
  <section class="rb">
    <PanelState v-if="unauthorized" state="unauthorized" />
    <PanelState v-else-if="!currentProject" state="empty" title="还没有选中设计系统项目" hint="版本快照按项目建立；先在顶部选择一个设计系统项目。" />
    <PanelState v-else-if="!supports('releases')" state="empty" title="后端未声明 releases 能力" hint="meta.capabilities 里没有 releases；当前后端不提供版本快照端点。" />

    <template v-else>
      <!-- 新建发布 -->
      <div class="rb__create ds-surface">
        <div class="ds-section-title"><h3 class="ds-h3">新建发布</h3><span class="ds-micro">当前 {{ currentProject.version }}</span></div>
        <div class="rb__create-row">
          <label class="rb__field"><span class="ds-micro">递增档</span>
            <select v-model="kind" class="ds-input ds-input--narrow">
              <option value="major">major</option>
              <option value="minor">minor</option>
              <option value="patch">patch</option>
            </select>
          </label>
          <label class="rb__field"><span class="ds-micro">版本号（可改）</span><input v-model="version" class="ds-input" /></label>
        </div>
        <label class="rb__field rb__field--wide"><span class="ds-micro">发布说明</span>
          <input v-model="notes" class="ds-input" placeholder="本版改了什么（可选）" />
        </label>
        <p v-if="createErr" class="rb__err" role="alert">{{ createErr }}</p>
        <p v-if="createPlain" class="rb__plain">{{ createPlain }}</p>
        <p v-if="createHint" class="rb__hint">{{ createHint }}</p>
        <div class="rb__create-ops">
          <button class="rb__btn rb__btn--primary" :disabled="creating" @click="create">{{ creating ? '发布中…' : '创建不可变快照' }}</button>
          <span class="ds-micro rb__tip">门禁：审计面板存在未通过的 critical 项时后端返回 409，禁止发布。</span>
        </div>
      </div>

      <!-- 列表 -->
      <div class="rb__list">
        <div class="ds-section-title"><h3 class="ds-h3">发布列表</h3><span class="ds-micro">{{ releases.length }} 个</span></div>
        <p v-if="err" class="rb__err" role="alert">{{ err }}</p>
        <PanelState v-if="loading && !releases.length" state="loading" />
        <PanelState v-else-if="!releases.length" state="empty" title="还没有发布快照" hint="上方创建第一个 Release：它会冻结当前令牌集并跑审计门禁。" />

        <table v-else class="rb__table">
          <thead>
            <tr><th>版本</th><th>状态</th><th>令牌哈希</th><th>令牌数</th><th>审计</th><th>门禁</th><th>快照</th><th>时间</th></tr>
          </thead>
          <tbody>
            <tr v-for="r in releases" :key="r.id">
              <td class="ds-mono">v{{ r.version }}</td>
              <td class="ds-micro">{{ r.status }}</td>
              <td class="ds-mono rb__hash" :title="r.tokensHash">{{ r.tokensHash.slice(0, 12) }}</td>
              <td class="ds-num">{{ r.tokenCount }}</td>
              <td class="ds-small">{{ r.auditSummary || '—' }}</td>
              <td>
                <span class="rb__pill" :class="r.auditPassed ? 'rb__pill--ok' : 'rb__pill--no'">{{ r.auditPassed ? '通过' : '未过' }}</span>
              </td>
              <td>
                <span class="rb__pill" :class="r.snapshotAvailable ? 'rb__pill--ok' : 'rb__pill--muted'">{{ r.snapshotAvailable ? '可读' : '无文件' }}</span>
              </td>
              <td class="ds-micro">{{ r.createdAt.slice(0, 19).replace('T', ' ') }}</td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- 版本对比 -->
      <div class="rb__diff ds-surface">
        <div class="ds-section-title"><h3 class="ds-h3">版本对比</h3></div>
        <div class="rb__diff-pick">
          <label class="rb__field"><span class="ds-micro">from</span>
            <select v-model.number="fromId" class="ds-input ds-input--narrow">
              <option :value="0">选择…</option>
              <option v-for="o in releaseOptions" :key="o.id" :value="o.id">{{ o.label }}</option>
            </select>
          </label>
          <label class="rb__field"><span class="ds-micro">to</span>
            <select v-model.number="toId" class="ds-input ds-input--narrow">
              <option :value="0">选择…</option>
              <option v-for="o in releaseOptions" :key="o.id" :value="o.id">{{ o.label }}</option>
            </select>
          </label>
          <button class="rb__btn" :disabled="diffing" @click="runDiff">{{ diffing ? '比对中…' : '对比' }}</button>
        </div>
        <p v-if="diffErr" class="rb__err" role="alert">{{ diffErr }}</p>

        <div v-if="diff" class="rb__diff-body">
          <div class="rb__diff-sum ds-small">
            <span><strong>{{ diff.from }}</strong> → <strong>{{ diff.to }}</strong></span>
            <span class="rb__pill rb__pill--ok">+{{ diff.added.length }}</span>
            <span class="rb__pill rb__pill--no">−{{ diff.removed.length }}</span>
            <span class="rb__pill rb__pill--muted">Δ{{ diff.changed.length }}</span>
            <span>合计 {{ diff.total }}</span>
          </div>
          <p v-if="diff.missingThemes.length" class="rb__warn">主题不对齐（仅存在于单侧）：{{ diff.missingThemes.join('、') }}</p>
          <p v-if="diff.isEmpty" class="rb__hint">两版内容一致：令牌与品牌/组件规格都没有差异。</p>

          <div v-else class="rb__diff-cols">
            <div class="rb__col">
              <h4 class="ds-h4">新增 ({{ diff.added.length }})</h4>
              <ul>
                <li v-for="(t, i) in diff.added.slice(0, 40)" :key="'a' + i" class="ds-mono rb__li"><span class="rb__theme">{{ t.theme }}</span>{{ t.path }} = {{ t.value }}</li>
              </ul>
            </div>
            <div class="rb__col">
              <h4 class="ds-h4">删除 ({{ diff.removed.length }})</h4>
              <ul>
                <li v-for="(t, i) in diff.removed.slice(0, 40)" :key="'r' + i" class="ds-mono rb__li"><span class="rb__theme">{{ t.theme }}</span>{{ t.path }}</li>
              </ul>
            </div>
            <div class="rb__col rb__col--wide">
              <h4 class="ds-h4">改值 ({{ diff.changed.length }})</h4>
              <div v-for="g in changedGroups.slice(0, 40)" :key="g.key" class="rb__chg">
                <div class="ds-mono rb__chg-key">{{ g.key }}</div>
                <div v-for="(c, i) in g.items" :key="g.key + i" class="ds-small rb__chg-line">
                  <span class="rb__field-name">{{ c.field }}</span>:
                  <span class="rb__from">{{ c.from ?? '∅' }}</span> → <span class="rb__to">{{ c.to ?? '∅' }}</span>
                </div>
              </div>
            </div>
          </div>

          <!--
            规格节：组件目录 / 变体格子 / 资产 / 起手屏 / 字体登记。
            没有这一节时，"换了 logo、改了字体许可证"在两版 diff 里完全看不出来（v2.2.0 前的真实缺口）。
          -->
          <div class="rb__specs">
            <h4 class="ds-h4">品牌与组件规格</h4>
            <p v-if="!diff.specsComparable" class="rb__warn">
              其中一版是 schema 1 的旧快照（当时只快照令牌），规格节**不可比** —— 不把它整节报成新增。
            </p>
            <template v-else>
              <p v-if="diff.notComparableKinds?.length" class="rb__warn" data-diff-not-comparable>
                这些规格类跨版本**无法比较**（其中一版的快照里根本没记过）：{{ diff.notComparableKinds.join(' / ') }} —— 不是"没有变化"。
              </p>
              <div class="ds-row ds-wrap ds-gap-3 ds-small">
                <span class="rb__pill rb__pill--ok">+{{ diff.specsAdded.length }}</span>
                <span class="rb__pill rb__pill--no">−{{ diff.specsRemoved.length }}</span>
                <span class="rb__pill rb__pill--muted">Δ{{ diff.specsChanged.length }}</span>
                <span class="ds-micro">kind = component / variant / asset / screen / font / guideline</span>
              </div>
              <ul v-if="diff.specsAdded.length || diff.specsRemoved.length" class="rb__spec-list">
                <li v-for="(s, i) in diff.specsAdded.slice(0, 40)" :key="'sa' + i" class="ds-mono rb__li">
                  <span class="rb__theme">{{ s.kind }}</span>{{ s.key }} · 新增
                </li>
                <li v-for="(s, i) in diff.specsRemoved.slice(0, 40)" :key="'sr' + i" class="ds-mono rb__li">
                  <span class="rb__theme">{{ s.kind }}</span>{{ s.key }} · 删除
                </li>
              </ul>
              <div v-for="(c, i) in diff.specsChanged.slice(0, 40)" :key="'sc' + i" class="ds-small rb__chg-line">
                <span class="rb__theme">{{ c.kind }}</span><span class="ds-mono rb__chg-key">{{ c.key }}</span>
                <span class="rb__field-name">{{ c.field }}</span>:
                <span class="rb__from">{{ brief(c.from) }}</span> → <span class="rb__to">{{ brief(c.to) }}</span>
              </div>
            </template>
          </div>
        </div>
      </div>

      <!-- 单版快照 -->
      <div class="rb__snap ds-surface">
        <div class="ds-section-title"><h3 class="ds-h3">单版快照</h3></div>
        <div class="rb__snap-pick">
          <label class="rb__field"><span class="ds-micro">版本</span>
            <select v-model.number="selReleaseId" class="ds-input ds-input--narrow">
              <option :value="0">选择…</option>
              <option v-for="o in releaseOptions" :key="o.id" :value="o.id">{{ o.label }}</option>
            </select>
          </label>
          <label class="rb__field"><span class="ds-micro">DTCG 主题</span>
            <select v-model="dtcgTheme" class="ds-input ds-input--narrow">
              <option value="shared">shared</option>
              <option v-for="t in themes" :key="t.code" :value="t.code">{{ t.code }}</option>
            </select>
          </label>
        </div>

        <PanelState v-if="!selReleaseId" state="empty" title="选择一个版本查看快照" hint="快照不可变：显示该版本冻结时的令牌与前 {{ SNAPSHOT_LIMIT }} 条 + DTCG 全文。" />
        <p v-else-if="snapErr" class="rb__err" role="alert">{{ snapErr }}</p>
        <p v-else-if="snapLoading" class="ds-small rb__warn">读取快照中…</p>

        <template v-else-if="snapshot">
          <p class="ds-small rb__snap-meta">
            v{{ snapshot.version }} · 令牌 <strong>{{ snapshotTokens.length }}</strong> 条 · 生成于 {{ snapshot.generatedAt.slice(0, 19).replace('T', ' ') }}
          </p>
          <table v-if="snapshotTokens.length" class="rb__table">
            <thead><tr><th>主题</th><th>路径</th><th>值</th></tr></thead>
            <tbody>
              <tr v-for="(t, i) in snapshotTokens.slice(0, SNAPSHOT_LIMIT)" :key="i">
                <td class="ds-micro">{{ t.theme }}</td>
                <td class="ds-mono">{{ t.path }}</td>
                <td class="ds-mono">{{ t.type === 'color' && t.hex ? t.hex : t.value }}</td>
              </tr>
            </tbody>
          </table>
          <p v-else class="ds-small rb__warn">该快照没有令牌行。</p>

          <h4 class="ds-h4 rb__dtcg-title">DTCG 全文预览（{{ dtcgTheme }}）</h4>
          <p v-if="dtcgLoading" class="ds-small rb__warn">读取中…</p>
          <p v-else-if="dtcgErr" class="rb__err" role="alert">{{ dtcgErr }}</p>
          <pre v-else class="ds-mono rb__dtcg">{{ dtcg }}</pre>
        </template>
      </div>
    </template>
  </section>
</template>

<style scoped>
.rb {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-5);
}
.ds-section-title {
  margin-bottom: var(--ds-space-3);
}
.rb__create,
.rb__diff,
.rb__snap {
  padding: var(--ds-space-5);
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-3);
}
.rb__create-row,
.rb__diff-pick,
.rb__snap-pick {
  display: flex;
  align-items: flex-end;
  gap: var(--ds-space-3);
  flex-wrap: wrap;
}
.rb__field {
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.rb__field--wide {
  max-width: 520px;
}
.rb__create-ops {
  display: flex;
  align-items: center;
  gap: var(--ds-space-3);
  flex-wrap: wrap;
}
.rb__tip {
  color: var(--ds-fg-4);
}
.rb__btn {
  font: inherit;
  font-size: var(--ds-fs-small);
  color: var(--ds-fg-2);
  background: var(--ds-surface-2);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-sm);
  padding: 6px var(--ds-space-4);
  cursor: pointer;
}
.rb__btn--primary {
  color: var(--ds-surface-1);
  background: var(--ds-color-primary);
  border-color: var(--ds-color-primary);
}
.rb__btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
.rb__err {
  color: var(--ds-danger);
  font-size: var(--ds-fs-small);
}
.rb__plain {
  color: var(--ds-warning);
  font-size: var(--ds-fs-small);
}
.rb__hint {
  color: var(--ds-success);
  font-size: var(--ds-fs-small);
}
.rb__warn {
  color: var(--ds-warning);
  font-size: var(--ds-fs-small);
}
.rb__table {
  width: 100%;
  border-collapse: collapse;
  font-size: var(--ds-fs-small);
}
.rb__table th {
  text-align: left;
  color: var(--ds-fg-3);
  border-bottom: 1px solid var(--ds-border-1);
  padding: 6px var(--ds-space-2);
}
.rb__table td {
  padding: 6px var(--ds-space-2);
  border-bottom: 1px solid var(--ds-border-1);
  vertical-align: middle;
}
.rb__hash {
  max-width: 12ch;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.rb__pill {
  font-size: var(--ds-fs-micro);
  padding: 1px 8px;
  border-radius: var(--ds-radius-pill);
  border: 1px solid var(--ds-border-1);
  background: var(--ds-surface-2);
}
.rb__pill--ok {
  color: var(--ds-success);
}
.rb__pill--no {
  color: var(--ds-danger);
}
.rb__pill--muted {
  color: var(--ds-fg-3);
}
.rb__diff-sum {
  display: flex;
  align-items: center;
  gap: var(--ds-space-3);
  flex-wrap: wrap;
}
.rb__diff-cols {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
  gap: var(--ds-space-4);
}
.rb__col--wide {
  grid-column: 1 / -1;
}
.rb__col ul {
  margin: 0;
  padding-left: var(--ds-space-4);
}
.rb__li {
  font-size: var(--ds-fs-small);
}
.rb__specs {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-2);
  margin-top: var(--ds-space-4);
  padding-top: var(--ds-space-3);
  border-top: 1px dashed var(--ds-border-2);
}
.rb__spec-list {
  margin: 0;
  padding-left: var(--ds-space-4);
  list-style: none;
}
.rb__theme {
  color: var(--ds-fg-4);
  margin-right: 6px;
}
.rb__chg {
  border-top: 1px solid var(--ds-border-1);
  padding-top: var(--ds-space-1);
  margin-top: var(--ds-space-2);
}
.rb__chg-key {
  font-size: var(--ds-fs-small);
}
.rb__chg-line {
  color: var(--ds-fg-3);
}
.rb__field-name {
  color: var(--ds-fg-2);
}
.rb__from {
  color: var(--ds-danger);
}
.rb__to {
  color: var(--ds-success);
}
.rb__snap-meta {
  color: var(--ds-fg-3);
}
.rb__dtcg-title {
  margin-top: var(--ds-space-3);
}
.rb__dtcg {
  margin: 0;
  max-height: 320px;
  overflow: auto;
  background: var(--ds-surface-2);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-sm);
  padding: var(--ds-space-3);
  font-size: var(--ds-fs-small);
  white-space: pre-wrap;
  word-break: break-word;
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
</style>
