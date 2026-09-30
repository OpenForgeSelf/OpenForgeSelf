<script setup lang="ts">
/**
 * 审计与门禁（FR8/FR17；对应 AC11/AC22 的前端面）。
 *
 * 这一块的意义是：**可达性在这里是库里的数据，不是 README 里的散文**。
 * 判定全部发生在后端 `AuditEngine` + `ContrastMath`，结果落 `DesignAudit` 行；
 * `POST projects/{id}/audit` 跑并落库，`GET projects/{id}/audit` 读回。发布端点读的是同一批行，
 * 所以这里看到 critical 未清 == `POST releases` 一定返回 409，不是"提醒一下"。
 *
 * 三条不可让的约束：
 * 1. **一个比率都不自己算**：`ratio` 与判级来自后端（effective 也带 contrastRatio/wcagLevel），
 *    界面只做显示换算（ratioText / severityClass / wcagBadge 的着色）。
 * 2. **跑完必须回读**：runAudit 的返回值只是摘要，明细一律用 `refreshAudit()` 从库里再取一遍。
 * 3. **草稿态口径**：读的是 `releaseId=0`（草稿）的审计行，与发布快照的审计不混在一起看。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { api, type AuditItem } from '../api'
import { ApiError } from '../http'
import { audit, currentProject, lastError, loadProjects, meta, refreshAudit, unauthorized } from '../state'
import { matchesKeyword, ratioText, severityClass, wcagBadge } from '../design/derive'
import PanelState from '../components/PanelState.vue'

/**
 * 类别词表读 `GET /meta.auditKinds`（后端 `AuditKinds.All`，展示序 = 先阻断级后提示级）。
 * 界面这里只剩**每类在查什么**的说明文案：文案可以只有界面有（它是对后端判据的转述，不参与判定），
 * 但"有哪些类、按什么序"不再另列一份 —— 后端加一类，界面下拉自动多一项。
 */
const KIND_LABEL: Record<string, string | undefined> = {
  contrast: '色对 WCAG 2.2 实测（正文 AA 4.5 / 大字与非文本 3.0 / AAA 7.0），未达 = critical；禁用态按 1.4.3「非活动构件」豁免，只报读数不拦发布；声明为 color 却解析不出颜色 = warning（无法判定）',
  alias: '别名图：成环 / 悬空 / 逆向引用 = critical',
  'tier-violation': 'component 色令牌直连 primitive、绕过语义层 = warning',
  focus: ':focus-visible 描边令牌是否齐备（WCAG 2.2 2.4.7 / 1.4.11）= 缺失 critical',
  'reduced-motion': '每个 duration.* 是否有 -reduced 派生（prefers-reduced-motion 可关）= 缺失 warning',
  'target-size': '*.min-height 是否 ≥24px（WCAG 2.2 2.5.8 指针目标；该条有例外，故只报 warning 不拦发布）',
  'ramp-monotonic': 'space / radius / duration 按声明序必须递增 —— 手改出一个逆序档，档位号就失去含义 = warning',
  naming: 'path 必须是点分 kebab，否则 CSS 变量名与 DTCG 树静默产出用不了的键 = warning',
  'lifecycle-ref': '仍被别名引用的 deprecated / removed 令牌（换肤或清理时链条会静默断掉）= warning',
  orphan: '未被任何上层引用的 primitive（info）/ 存在但没有任何语义行的主题（warning）',
  unused: '未被组件层引用的 semantic 角色（info，可能是给人手用的）',
}

const running = ref(false)
const reading = ref(false)
const runError = ref('')
const runNote = ref('')

const kindFilter = ref('')
const onlyFailed = ref(false)
const keyword = ref('')

const items = computed<AuditItem[]>(() => audit.value?.items ?? [])
const summary = computed(() => audit.value?.summary ?? null)

/** 审计类别词表（后端 `AuditKinds.All` 经 /meta 出过来）：说明清单整张都列，即使本次一条没报 */
const kindVocabulary = computed(() => meta.value?.auditKinds ?? [])

const kindOptions = computed(() => {
  const seen = new Set(items.value.map((a) => a.kind))
  const vocabulary = kindVocabulary.value
  // 词表内的按后端序排；数据里出现词表外的 kind（后端加了类还没发界面）照样列出来，不吞结论
  return [...vocabulary.filter((k) => seen.has(k)), ...[...seen].filter((k) => !vocabulary.includes(k))]
})

const filtered = computed(() =>
  items.value
    .filter((a) => !kindFilter.value || a.kind === kindFilter.value)
    .filter((a) => !onlyFailed.value || !a.passed)
    .filter((a) => matchesKeyword(a.targetPath, keyword.value)),
)

/** 按 kind 分组：未通过的排前面（一进来先看该修的，不看已通过刷绿意的） */
const groups = computed(() => {
  const map = new Map<string, AuditItem[]>()
  for (const a of filtered.value) {
    const bucket = map.get(a.kind)
    if (bucket) bucket.push(a)
    else map.set(a.kind, [a])
  }
  const vocabulary = kindVocabulary.value
  const rank = (k: string): number => {
    const i = vocabulary.indexOf(k)
    return i < 0 ? vocabulary.length : i
  }
  return [...map.keys()]
    .sort((a, b) => rank(a) - rank(b))
    .map((kind) => ({
      kind,
      items: (map.get(kind) ?? []).slice().sort((x, y) => Number(x.passed) - Number(y.passed) || (x.targetPath < y.targetPath ? -1 : 1)),
    }))
})

/** 审计行的 CheckedAt 由后端每次重跑覆盖，所以它就是"这批结论的时间"；不做时区换算，直接显示库里的 ISO 串 */
function checkedAtText(): string {
  const first = items.value[0]
  return first ? first.checkedAt.replace('T', ' ').slice(0, 19) : '—'
}

const blockingText = computed(() => {
  const s = summary.value
  if (!s) return ''
  return s.blocking
    ? `发布门禁：拒绝。有 ${s.critical} 条未通过的 critical —— POST releases 会返回 409，必须先清掉`
    : '发布门禁：放行。没有未通过的 critical 项'
})

async function load(): Promise<void> {
  if (!currentProject.value) return
  reading.value = true
  await refreshAudit()
  reading.value = false
}

/** 跑审计（POST，落库）→ 立刻回读 GET，界面显示的是库里的行而不是返回值 */
async function run(): Promise<void> {
  const p = currentProject.value
  if (!p || running.value) return
  running.value = true
  runError.value = ''
  runNote.value = ''
  try {
    const s = await api.runAudit(p.id)
    runNote.value = `已重跑并落库（草稿态 releaseId=0）：共 ${s.total} 条，未通过 critical ${s.critical} / warning ${s.warning} / info ${s.info}`
    await refreshAudit()
  } catch (err) {
    runError.value = err instanceof ApiError ? `${err.status} ${err.message}` : String(err)
  } finally {
    running.value = false
  }
}

onMounted(async () => {
  if (!currentProject.value) await loadProjects()
  await load()
})
watch(
  () => currentProject.value?.id,
  () => {
    kindFilter.value = ''
    onlyFailed.value = false
    keyword.value = ''
    runNote.value = ''
    void load()
  },
)

function passedText(a: AuditItem): string {
  return a.passed ? '通过' : '未通过'
}
</script>

<template>
  <section class="ab">
    <div class="ds-section-title">
      <h3 class="ds-h3">审计与门禁</h3>
      <span class="ds-small">可达性与分层纪律作为库里的数据：审计行由后端 AuditEngine 产出，发布端点读同一批行判能不能发。</span>
    </div>

    <!-- 门禁口径（转述后端事实，界面不重新判定） -->
    <div class="ab__rules ds-surface-2">
      <h4 class="ds-h4">门禁口径</h4>
      <ul class="ab__rules-list">
        <li>对比度按 <strong>WCAG 2.2</strong>：正文 1.4.3 <strong class="ds-num">4.5:1</strong>（AAA 7.0）；大字 1.4.3 与非文本 UI/图形对象/焦点指示 1.4.11 <strong class="ds-num">3.0:1</strong>（大字 AAA 4.5；1.4.11 规范里没有 AAA 档）。</li>
        <li><strong>APCA 不作为门禁</strong>：它随 2023 年 WCAG 3 草案一起被移出规范，只作参考读数，不参与 blocking 判定。</li>
        <li>密度轴主题（modeKind=density）不参与颜色审计 —— 按颜色审它等于凭空造一批假 critical。</li>
        <li>门禁盯的是<strong>库里的现值</strong>，不是"生成器当初算得对不对"：手改出的尺度逆序、可点高度 &lt;24px、
          不合形状的 path、仍被引用的已退役令牌，都会在下面这几类里现形（这几类报 warning，不拦发布）。</li>
        <li>存在<strong>未通过的 critical</strong> 时 <code>POST projects/{id}/releases</code> 返回 409；这是硬门禁，不是提示。</li>
        <li>本页只转述后端结论：比率、判级、severity 全部来自 <code>DesignAudit</code> 行，前端一个都不重算。</li>
      </ul>
      <div class="ab__kindmap">
        <div v-for="k in kindVocabulary" :key="k" class="ab__kindmap-row">
          <span class="ds-mono ab__kind">{{ k }}</span>
          <span class="ds-small">{{ KIND_LABEL[k] ?? '后端新增的审计类别（界面说明还没跟上，判定与展示不受影响）' }}</span>
        </div>
      </div>
    </div>

    <PanelState v-if="unauthorized" state="unauthorized" />
    <PanelState
      v-else-if="!currentProject"
      state="empty"
      title="没有选中设计系统项目"
      hint="审计是针对某个项目的令牌跑的，先到「项目与生成」选一个项目。"
    />

    <template v-else>
      <div class="ab__bar ds-surface">
        <div class="ds-row ds-wrap ds-gap-3">
          <button class="ds-btn ds-btn--primary" type="button" :disabled="running || reading" @click="run">
            {{ running ? '审计中…（写库）' : '跑审计（POST audit，落库）' }}
          </button>
          <button class="ds-mini" type="button" :disabled="reading" @click="load">{{ reading ? '回读中…' : '只回读（GET audit）' }}</button>
          <span class="ds-small">
            项目 <strong>{{ currentProject.code }}</strong> · 草稿态 releaseId=0
            <template v-if="items.length"> · 这批结论检查于 <span class="ds-mono">{{ checkedAtText() }}</span></template>
          </span>
        </div>
        <p v-if="runNote" class="ab__hint">{{ runNote }}</p>
        <p v-if="runError" class="ab__error" role="alert">后端原文：{{ runError }}</p>
        <p v-else-if="lastError" class="ab__error" role="alert">后端原文：{{ lastError }}</p>
      </div>

      <p v-if="blockingText" class="ab__gate" :class="summary?.blocking ? 'ab__gate--block' : 'ab__gate--pass'" role="status">{{ blockingText }}</p>

      <!-- 概览卡：数字全部来自后端 summary，界面只加标签 -->
      <div v-if="summary" class="ab__cards">
        <div class="ab__card"><span class="ds-micro">总计</span><strong class="ds-num ab__n">{{ summary.total }}</strong></div>
        <div class="ab__card"><span class="ds-micro">通过</span><strong class="ds-num ab__n ab__ok">{{ summary.passed }}</strong></div>
        <div class="ab__card"><span class="ds-micro">critical（未通过）</span><strong class="ds-num ab__n ab__bad">{{ summary.critical }}</strong></div>
        <div class="ab__card"><span class="ds-micro">warning（未通过）</span><strong class="ds-num ab__n ab__warn">{{ summary.warning }}</strong></div>
        <div class="ab__card"><span class="ds-micro">info（未通过）</span><strong class="ds-num ab__n">{{ summary.info }}</strong></div>
        <div class="ab__card ab__card--gate">
          <span class="ds-micro">blocking</span>
          <strong class="ab__n">{{ summary.blocking ? '拒绝发布' : '可发布' }}</strong>
        </div>
      </div>

      <PanelState
        v-if="reading && !items.length"
        state="loading"
        title="正在读取审计行…"
        hint="GET projects/{id}/audit；从没跑过审计的项目这里会是空的。"
      />
      <PanelState
        v-else-if="!items.length"
        state="empty"
        title="该项目还没有审计记录"
        hint="点上面的「跑审计」：后端按色对/别名/分层/焦点/动效逐项判定并落库，之后这里显示每一行的期望值与实测值。"
      />

      <template v-else>
        <div class="ab__filters ds-row ds-wrap ds-gap-2">
          <input v-model="keyword" class="ds-input" type="search" placeholder="按对象路径检索（如 dark:semantic.text-1）" />
          <select v-model="kindFilter" class="ds-input ds-input--narrow" aria-label="审计类别">
            <option value="">全部类别</option>
            <option v-for="k in kindOptions" :key="k" :value="k">{{ k }}</option>
          </select>
          <label class="ab__check">
            <input v-model="onlyFailed" type="checkbox" />
            <span>只看未通过</span>
          </label>
          <span class="ds-micro">命中 {{ filtered.length }} / 共 {{ items.length }}</span>
        </div>

        <p v-if="!groups.length" class="ab__hint">筛选后 0 条 —— 上面的类别/关键词条件再放宽一点。</p>

        <div v-for="g in groups" :key="g.kind" class="ab__group ds-surface">
          <h4 class="ds-h4 ab__group-title">
            {{ g.kind }}<span class="ds-micro"> · {{ g.items.length }} 条</span>
            <span class="ds-small ab__group-desc">{{ KIND_LABEL[g.kind] ?? '后端新增的审计类别（界面照原样列出）' }}</span>
          </h4>
          <table class="ab__table">
            <thead>
              <tr>
                <th>对象路径</th>
                <th>判据</th>
                <th>severity</th>
                <th>结果</th>
                <th class="ab__num">ratio</th>
                <th>期望 / 实测</th>
                <th>说明与建议</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="a in g.items" :key="`${a.kind}:${a.targetType}:${a.targetPath}`" :class="{ 'ab__row--fail': !a.passed }">
                <td class="ds-mono">{{ a.targetPath }}</td>
                <td class="ds-micro">{{ a.rule || '—' }}</td>
                <td><span class="ab__sev" :class="`ab__sev--${severityClass(a.severity)}`">{{ a.severity }}</span></td>
                <td class="ds-small">{{ passedText(a) }}</td>
                <td class="ds-num ab__num" :class="`ab__r--${wcagBadge(a.ratio).level}`">{{ ratioText(a.ratio) }}</td>
                <td class="ds-small">
                  期望 <span class="ds-mono">{{ a.expected || '—' }}</span>
                  / 实测 <span class="ds-mono">{{ a.actual || '—' }}</span>
                  <span v-if="a.pairedPath" class="ds-micro"> · 配对 {{ a.pairedPath }}</span>
                </td>
                <td class="ab__msg">
                  <div v-if="a.message">{{ a.message }}</div>
                  <div v-if="a.suggestion" class="ab__sugg">建议：{{ a.suggestion }}</div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </template>
    </template>
  </section>
</template>

<style scoped>
.ab {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-4);
}
.ab__rules,
.ab__bar,
.ab__group {
  padding: var(--ds-space-4);
}
.ab__rules-list {
  margin: 0 0 var(--ds-space-3);
  padding-left: var(--ds-space-5);
  color: var(--ds-fg-2);
  font-size: var(--ds-fs-small);
  line-height: var(--ds-lh-normal);
}
.ab__rules-list code {
  font-family: var(--ds-font-mono);
  font-size: var(--ds-fs-micro);
}
.ab__kindmap {
  display: flex;
  flex-direction: column;
  gap: 2px;
  border-top: 1px solid var(--ds-border-1);
  padding-top: var(--ds-space-3);
}
.ab__kindmap-row {
  display: flex;
  gap: var(--ds-space-3);
  align-items: baseline;
}
.ab__kind {
  min-width: 15ch;
  color: var(--ds-fg-1);
}
.ab__gate {
  font-size: var(--ds-fs-small);
  padding: var(--ds-space-3) var(--ds-space-4);
  border-radius: var(--ds-radius-md);
  border: 1px solid var(--ds-border-1);
  background: var(--ds-surface-2);
}
.ab__gate--block {
  color: var(--ds-danger);
  border-color: var(--ds-danger);
  background: color-mix(in oklab, var(--ds-danger) 10%, transparent);
}
.ab__gate--pass {
  color: var(--ds-success);
  border-color: var(--ds-success);
  background: color-mix(in oklab, var(--ds-success) 10%, transparent);
}
.ab__cards {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
  gap: var(--ds-space-3);
}
.ab__card {
  display: flex;
  flex-direction: column;
  gap: 2px;
  padding: var(--ds-space-3);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-md);
  background: var(--ds-surface-1);
}
.ab__n {
  font-size: var(--ds-fs-h3);
  color: var(--ds-fg-1);
}
.ab__ok {
  color: var(--ds-success);
}
.ab__bad {
  color: var(--ds-danger);
}
.ab__warn {
  color: var(--ds-warning);
}
.ab__card--gate strong {
  font-size: var(--ds-fs-body);
  color: var(--ds-fg-2);
}
.ab__filters {
  align-items: center;
}
.ab__check {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: var(--ds-fs-small);
  color: var(--ds-fg-2);
  cursor: pointer;
}
.ab__group-title {
  margin-bottom: var(--ds-space-2);
}
.ab__group-desc {
  margin-left: var(--ds-space-3);
}
.ab__table {
  width: 100%;
  border-collapse: collapse;
  font-size: var(--ds-fs-small);
}
.ab__table th {
  text-align: left;
  color: var(--ds-fg-3);
  font-weight: var(--ds-fw-medium);
  border-bottom: 1px solid var(--ds-border-1);
  padding: 6px var(--ds-space-2);
  white-space: nowrap;
}
.ab__table td {
  padding: 6px var(--ds-space-2);
  border-bottom: 1px solid var(--ds-border-1);
  vertical-align: top;
}
.ab__num {
  text-align: right;
  white-space: nowrap;
}
.ab__row--fail td {
  background: color-mix(in oklab, var(--ds-danger) 8%, transparent);
}
.ab__msg {
  color: var(--ds-fg-2);
  max-width: 46ch;
}
.ab__sugg {
  color: var(--ds-fg-3);
  font-size: var(--ds-fs-micro);
  margin-top: 2px;
}
.ab__sev {
  font-size: var(--ds-fs-micro);
  padding: 1px 8px;
  border-radius: var(--ds-radius-pill);
  border: 1px solid var(--ds-border-1);
  background: var(--ds-surface-2);
  color: var(--ds-fg-2);
  white-space: nowrap;
}
.ab__sev--critical {
  color: var(--ds-danger);
}
.ab__sev--warning {
  color: var(--ds-warning);
}
.ab__sev--info {
  color: var(--ds-fg-3);
}
.ab__error {
  color: var(--ds-danger);
  font-size: var(--ds-fs-small);
}
/* ratio 的着色只按后端给的比率分档（derive.wcagBadge 是纯展示换算） */
.ab__r--fail {
  color: var(--ds-danger);
}
.ab__r--large {
  color: var(--ds-warning);
}
.ab__r--aa,
.ab__r--aaa {
  color: var(--ds-success);
}
.ab__r--unknown {
  color: var(--ds-fg-3);
}
.ab__hint {
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
  min-width: 0;
}
.ds-input--narrow {
  width: auto;
}
.ds-btn {
  font: inherit;
  font-size: var(--ds-fs-small);
  color: var(--ds-fg-1);
  background: var(--ds-surface-2);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-sm);
  padding: 6px var(--ds-space-4);
  cursor: pointer;
}
.ds-btn--primary {
  color: var(--ds-color-primary);
  border-color: var(--ds-color-primary);
}
.ds-btn:disabled,
.ds-mini:disabled {
  opacity: 0.45;
  cursor: not-allowed;
}
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
</style>
