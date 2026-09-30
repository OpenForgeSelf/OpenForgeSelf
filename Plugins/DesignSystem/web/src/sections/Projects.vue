<script setup lang="ts">
/**
 * 项目与生成（FR1/FR5/FR17；对应 AC3/AC5/AC18/AC22 的前端面）。
 *
 * 本面板只做两件事：把库里的设计系统项目如实列出来（读），以及把**唯一会大批写库的动作**
 * —— generate —— 做成不可反悔的两步：先 `generate/preview`（后端算但不落库），确认后才 `generate`。
 *
 * 四条不可让的约束：
 * 1. **写库前必须先看预览**：generate 是整批写入（一次几百行），参数打错就是脏库；
 *    所以预览之后若再改参数，写入按钮立即失效（`paramsStale`），不允许"预览的和写进去的不是一套"。
 * 2. **overwrite 默认关**（AC18）：`Generator=manual` 的人工手改行默认受保护，勾选等于自愿放弃这层保护，
 *    风险必须写在复选框旁边而不是藏进 tooltip。
 * 3. **写完自证**：generate 返回的 `audit` 摘要只是"后端说它写了"，界面还必须 `invalidate()` 回读
 *    有效令牌与项目行，显示的令牌数以库为准。
 * 4. **颜色一个都不自己算**：种子色只判形状（hex / oklch 字面量），解析与取色全在后端。
 */
import { computed, onMounted, ref, watch } from 'vue'
import { api, type GeneratePreview, type GenerateRequest, type GenerateResult, type Project } from '../api'
import { ApiError } from '../http'
import {
  createProject,
  currentProject,
  effective,
  invalidate,
  lastError,
  loadProjects,
  projects,
  projectsState,
  selectProject,
  themeCode,
  themes,
  unauthorized,
} from '../state'
import PanelState from '../components/PanelState.vue'

/** 与后端 `DesignProject.Kind` 的注释取值一致（product|console|brand|marketing|system，默认 product） */
const KINDS = ['product', 'console', 'brand', 'marketing', 'system']
/** 与后端 `ScaleGenerators.BaseUnit/DensityScale` 的三档一致；其它值后端一律按 default 处理，所以不给自由输入 */
const DENSITIES = ['default', 'compact', 'comfortable']
/** 与后端 `DesignGenerator.Industries` 的键一致（industry 只决定参数缺省包，不是"行业配色心理学"） */
const INDUSTRIES = ['devtools', 'finance', 'healthcare', 'education', 'commerce', 'media', 'general']
const STATUSES = ['draft', 'published', 'archived']

/* ------------------------------------------------------------------ */
/* 新建项目（FR1：Code 是库内唯一键，重復由后端判 409）                  */
/* ------------------------------------------------------------------ */
const code = ref('')
const name = ref('')
const kind = ref('product')
const seedText = ref('')
const creating = ref(false)
const createError = ref('')
const createHint = ref('')

/** 后端会 Trim + ToLower 后判重；前端只做形状校验——判重必须直查库（铁律 11），这里不假装能预判 */
const CODE_RE = /^[a-z0-9][a-z0-9-]{0,39}$/
const codeProblem = computed(() => {
  const c = code.value.trim()
  if (!c) return ''
  return CODE_RE.test(c) ? '' : '只可用小写字母、数字与短横线，首字符须为字母或数字'
})
const canCreate = computed(() => !creating.value && !!code.value.trim() && !!name.value.trim() && !codeProblem.value)

async function submitNew(): Promise<void> {
  if (!canCreate.value) return
  creating.value = true
  createError.value = ''
  createHint.value = ''
  const p = await createProject({
    code: code.value.trim(),
    name: name.value.trim(),
    kind: kind.value,
    seedText: seedText.value.trim() || undefined,
  })
  creating.value = false
  if (p) {
    createHint.value = `已创建 ${p.code}（id ${p.id}，v${p.version}），并已切为当前项目`
    code.value = ''
    name.value = ''
    seedText.value = ''
  } else {
    // 后端原文（如「项目标识 x 已存在」）必须原样给出，不替换成"创建失败"这种无信息量的话
    createError.value = lastError.value || '后端未返回原因（确认宿主已加载 design-system 插件，并检查登录态）'
  }
}

/* ------------------------------------------------------------------ */
/* 项目列表与切换                                                      */
/* ------------------------------------------------------------------ */
const listBusy = ref(false)

async function reloadList(): Promise<void> {
  listBusy.value = true
  await loadProjects()
  listBusy.value = false
}

function pick(p: Project): void {
  void selectProject(p)
  clearWizardOutput()
}

/** 时间戳不做本地时区换算：直接显示后端给的 ISO 串前 16 位，避免"界面时间与库里不一致" */
function shortTime(iso?: string | null): string {
  if (!iso) return '—'
  return iso.replace('T', ' ').slice(0, 16)
}

/* ------------------------------------------------------------------ */
/* 项目改名 / 改状态 / 归档（FR1：归档=软删，数据行永不物理删）          */
/* ------------------------------------------------------------------ */
const editName = ref('')
const editStatus = ref('draft')
const saving = ref(false)
const writeError = ref('')
const writeHint = ref('')

/** 编辑框必须跟着库里的行初始化：否则直接进面板时 name 是空的，"部分更新"会变成什么都不改 */
function syncEdit(): void {
  const p = currentProject.value
  if (!p) return
  editName.value = p.name
  editStatus.value = p.status
}

watch(
  () => currentProject.value?.id,
  () => {
    writeHint.value = ''
    syncEdit()
    // 换项目后预览/结果都不属于新项目了，必须清掉（否则会拿 A 项目的预览去写 B 项目）
    clearWizardOutput()
  },
)

async function saveProject(): Promise<void> {
  const p = currentProject.value
  if (!p || saving.value) return
  saving.value = true
  writeError.value = ''
  writeHint.value = ''
  try {
    await api.updateProject(p.id, { name: editName.value.trim() || undefined, status: editStatus.value })
    await Promise.all([invalidate(), loadProjects()])
    writeHint.value = `已更新项目 ${p.code} 并回读（当前 status=${currentProject.value?.status ?? editStatus.value}）`
  } catch (err) {
    writeError.value = err instanceof ApiError ? `${err.status} ${err.message}` : String(err)
  } finally {
    saving.value = false
  }
}

/** 归档二次确认：确认文案就是"软删不删数据"这件事本身，不藏进 tooltip */
async function archive(p: Project): Promise<void> {
  const soft = `归档项目「${p.name}」（${p.code}）？\n\n归档 = Status=archived 的软删：令牌 / 主题 / 审计 / 发布快照数据行一律保留，不删任何库文件；\n只是从工作台默认清单里退出，并且不能再作为发布对象。\n随时可把状态改回 draft 恢复。`
  if (!window.confirm(soft)) return
  writeError.value = ''
  writeHint.value = ''
  try {
    await api.archiveProject(p.id)
    await Promise.all([loadProjects(), p.id === currentProject.value?.id ? invalidate() : Promise.resolve()])
    writeHint.value = `已归档 ${p.code}（软删，数据仍在库里）`
  } catch (err) {
    writeError.value = err instanceof ApiError ? `${err.status} ${err.message}` : String(err)
  }
}

/** 模板里不直接传 `currentProject`（可能是 null），统一走这个有守卫的入口 */
function archiveCurrent(): void {
  const p = currentProject.value
  if (p) void archive(p)
}

/* ------------------------------------------------------------------ */
/* 生成向导（FR5：同输入必同输出；AC18：手改行默认不覆盖）              */
/* ------------------------------------------------------------------ */
const brief = ref('')
const seedHex = ref('')
const hue = ref('')
const chroma = ref('')
const density = ref('')
const typeRatio = ref('')
const typeBasePx = ref('')
const radiusBase = ref('')
const motionScale = ref('')
const brandName = ref('')
const industry = ref('')
const picked = ref<string[]>(['light', 'dark'])
const overwrite = ref(false)

/**
 * 密度轴主题默认一起勾选：密度主题现在铺的是**自己的尺度覆盖**（space/radius/duration），
 * 不勾就切到 compact 尺度不变，"mode 轴"又变回装饰。用户仍可手动取消。
 */
watch(
  themes,
  (list) => {
    const extra = list.filter((t) => t.modeKind === 'density').map((t) => t.code).filter((c) => !picked.value.includes(c))
    if (extra.length > 0) picked.value = [...picked.value, ...extra]
  },
  { immediate: true },
)

const previewBusy = ref(false)
const genBusy = ref(false)
const previewData = ref<GeneratePreview | null>(null)
const previewSnapshot = ref('')
const genResult = ref<GenerateResult | null>(null)
const genError = ref('')
const readback = ref('')

function clearWizardOutput(): void {
  previewData.value = null
  previewSnapshot.value = ''
  genResult.value = null
  genError.value = ''
  readback.value = ''
}

function numOrNull(raw: string): number | null {
  const t = raw.trim()
  if (!t) return null
  const n = Number(t)
  return Number.isFinite(n) ? n : null
}

const numericFields = computed(() => [
  { label: 'hue', raw: hue.value },
  { label: 'chroma', raw: chroma.value },
  { label: 'typeRatio', raw: typeRatio.value },
  { label: 'typeBasePx', raw: typeBasePx.value },
  { label: 'radiusBase', raw: radiusBase.value },
  { label: 'motionScale', raw: motionScale.value },
])
/** 留空 = 不覆盖（后端用行业倾向推导）；填了就必须是可解析数字，否则当场拦住而不是让后端 400 */
const badNumbers = computed(() => numericFields.value.filter((f) => f.raw.trim() !== '' && !Number.isFinite(Number(f.raw))).map((f) => f.label))

/** 种子色只判**形状**（hex / oklch 字面量），解析取色全在后端 */
const seedProblem = computed(() => {
  const t = seedHex.value.trim()
  if (!t) return ''
  if (/^#?[\da-f]{3}$/i.test(t) || /^#?[\da-f]{6}$/i.test(t)) return ''
  if (/^oklch\(/i.test(t)) return ''
  return '种子色只接受 hex（三位或六位）或 oklch(...) 字面量；其它写法后端也解析不了 —— 不如有意留空，让后端按需求文本哈希定色相'
})
const normalizedSeed = computed(() => {
  const t = seedHex.value.trim()
  if (!t) return null
  return /^[\da-f]/i.test(t) ? `#${t}` : t
})

const paramProblem = computed(() => {
  if (badNumbers.value.length) return `这些参数不是数字：${badNumbers.value.join('、')}`
  return seedProblem.value
})

/** 请求体的唯一构造点：预览与写入必须喂**同一个**对象，否则"预览的不是写进去的" */
function buildRequest(): GenerateRequest {
  return {
    brief: brief.value.trim() || null,
    seedColor: normalizedSeed.value,
    hue: numOrNull(hue.value),
    chroma: numOrNull(chroma.value),
    density: density.value || null,
    typeRatio: numOrNull(typeRatio.value),
    typeBasePx: numOrNull(typeBasePx.value),
    radiusBase: numOrNull(radiusBase.value),
    motionScale: numOrNull(motionScale.value),
    brandName: brandName.value.trim() || null,
    themes: picked.value.slice(),
    industry: industry.value || null,
  }
}

const paramsKey = computed(() => JSON.stringify(buildRequest()))
/** 预览之后又改了参数 → 预览作废（约束 1） */
const paramsStale = computed(() => previewData.value !== null && previewSnapshot.value !== paramsKey.value)
const canPreview = computed(() => !!currentProject.value && !paramProblem.value && !previewBusy.value && !genBusy.value)
const canGenerate = computed(
  () => !!currentProject.value && previewData.value !== null && !paramsStale.value && !paramProblem.value && !genBusy.value && !previewBusy.value,
)

/** `<input type=color>` 只是 hex 文本框的取色助手：不维护第二份颜色状态。
 *  它只接受六位 hex（三位就地展开是等价写法，不是新值），形状不合时返回 null 让输入框根本不渲染——
 *  给它塞空串浏览器会报 "does not conform to the required format"。 */
const pickerValue = computed<string | null>(() => {
  const t = seedHex.value.trim().replace(/^#/, '')
  if (/^[\da-f]{6}$/i.test(t)) return `#${t}`
  if (/^[\da-f]{3}$/i.test(t)) return `#${t.split('').map((c) => c + c).join('')}`
  return null
})
function onPick(e: Event): void {
  seedHex.value = (e.target as HTMLInputElement).value
}

function toggleTheme(c: string): void {
  picked.value = picked.value.includes(c) ? picked.value.filter((x) => x !== c) : [...picked.value, c]
}

async function runPreview(): Promise<void> {
  if (!currentProject.value) return
  if (paramProblem.value) {
    genError.value = paramProblem.value
    return
  }
  previewBusy.value = true
  genError.value = ''
  genResult.value = null
  try {
    previewData.value = await api.generatePreview(buildRequest())
    previewSnapshot.value = paramsKey.value
  } catch (err) {
    previewData.value = null
    previewSnapshot.value = ''
    genError.value = err instanceof ApiError ? `${err.status} ${err.message}` : String(err)
  } finally {
    previewBusy.value = false
  }
}

async function runGenerate(): Promise<void> {
  const p = currentProject.value
  if (!p || !canGenerate.value) return
  if (overwrite.value && !window.confirm(`overwrite=true 会连**人工手改过（Generator=manual）**的令牌一起重写，等于放弃 AC18 的保护。\n确认继续？`)) return
  genBusy.value = true
  genError.value = ''
  writeHint.value = ''
  try {
    const res = await api.generate(p.id, buildRequest(), overwrite.value)
    genResult.value = res
    // 自证：返回值只说明后端跑了什么，界面显示的令牌数一律来自回读（invalidate 重拉 effective + 项目行）
    await Promise.all([invalidate(), loadProjects()])
    readback.value = `回读库：项目 ${p.code} 令牌数 ${currentProject.value?.tokenCount ?? 0} · 组件数 ${currentProject.value?.componentCount ?? 0} · 主题 ${themeCode.value} 有效令牌 ${effective.value?.count ?? 0}`
  } catch (err) {
    genError.value = err instanceof ApiError ? `${err.status} ${err.message}` : String(err)
  } finally {
    genBusy.value = false
  }
}

onMounted(() => {
  if (!projects.value.length) void loadProjects()
  syncEdit()
})

const previewThemeEntries = computed(() => Object.entries(previewData.value?.themes ?? {}))
</script>

<template>
  <section class="pj">
    <div class="ds-section-title">
      <h3 class="ds-h3">项目与生成</h3>
      <span class="ds-small">设计系统库的唯一写入口：项目 = 一套三层令牌 + 多主题 + 审计。这里不存任何东西，刷新即回读库。</span>
    </div>

    <PanelState v-if="unauthorized" state="unauthorized" />

    <template v-else>
      <div class="pj__top">
        <!-- 项目清单 -->
        <div class="pj__list ds-surface">
          <div class="pj__list-head">
            <h4 class="ds-h4">项目清单</h4>
            <span class="ds-micro">
              {{ projects.length }} 个
              <button class="ds-link" type="button" @click="reloadList">{{ listBusy ? '读取中…' : '重新读取' }}</button>
            </span>
          </div>

          <PanelState v-if="projectsState === 'loading' && !projects.length" state="loading" />
          <PanelState
            v-else-if="!projects.length"
            state="empty"
            title="库里还没有设计系统项目"
            hint="用右边的表单建一个（code 小写短横线、重复会返回 409）；建好后项目会自动带上 light / dark / high-contrast / compact 四个主题轴，令牌要靠下面的「生成向导」写入。"
          />

          <table v-else class="pj__table">
            <thead>
              <tr>
                <th>code</th>
                <th>名称</th>
                <th>用途</th>
                <th>版本</th>
                <th>状态</th>
                <th class="pj__num">令牌</th>
                <th class="pj__num">组件</th>
                <th>发布时间</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="p in projects" :key="p.id" :class="{ 'pj__row--on': p.id === currentProject?.id }">
                <td class="ds-mono">{{ p.code }}<span class="ds-micro pj__id"> #{{ p.id }}</span></td>
                <td>{{ p.name }}</td>
                <td class="ds-small">{{ p.kind }}</td>
                <td class="ds-mono">v{{ p.version }}</td>
                <td><span class="pj__status" :class="`pj__status--${p.status}`">{{ p.status }}</span></td>
                <td class="ds-num pj__num">{{ p.tokenCount }}</td>
                <td class="ds-num pj__num">{{ p.componentCount }}</td>
                <td class="ds-small">{{ shortTime(p.publishedAt) }}</td>
                <td class="pj__ops">
                  <button class="ds-mini" type="button" :disabled="p.id === currentProject?.id" @click="pick(p)">
                    {{ p.id === currentProject?.id ? '当前' : '选为工作项目' }}
                  </button>
                  <button class="ds-mini ds-mini--danger" type="button" @click="archive(p)">归档</button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- 新建项目 -->
        <form class="pj__new ds-surface-2" @submit.prevent="submitNew">
          <h4 class="ds-h4">新建项目</h4>
          <label class="pj__field">
            <span class="ds-micro">code（库内唯一键）</span>
            <input v-model="code" class="ds-input" type="text" placeholder="如 console-ui" autocomplete="off" />
            <span v-if="codeProblem" class="pj__field-err">{{ codeProblem }}</span>
            <span v-else class="ds-micro pj__note">重复的 code 后端返回 409（原文会显示在下面）</span>
          </label>
          <label class="pj__field">
            <span class="ds-micro">name（显示名）</span>
            <input v-model="name" class="ds-input" type="text" placeholder="如 铸己匣控制台" autocomplete="off" />
          </label>
          <label class="pj__field">
            <span class="ds-micro">kind（用途类型）</span>
            <select v-model="kind" class="ds-input">
              <option v-for="k in KINDS" :key="k" :value="k">{{ k }}</option>
            </select>
          </label>
          <label class="pj__field">
            <span class="ds-micro">seedText（项目种子文本，可空）</span>
            <textarea v-model="seedText" class="ds-input pj__area" rows="2" placeholder="一句这个项目是什么；生成时它只作为参数线索，不参与判色"></textarea>
          </label>
          <button class="ds-btn" type="submit" :disabled="!canCreate">{{ creating ? '创建中…' : '创建项目' }}</button>
          <p v-if="createHint" class="pj__hint">{{ createHint }}</p>
          <p v-if="createError" class="pj__error" role="alert">后端原文：{{ createError }}</p>
        </form>
      </div>

      <!-- 生成向导 -->
      <div class="pj__wizard ds-surface">
        <div class="pj__list-head">
          <h4 class="ds-h4">生成向导</h4>
          <span class="ds-small">
            目标项目
            <strong>{{ currentProject?.code ?? '（未选中）' }}</strong>
            · 主题轴 <strong>{{ themes.length }}</strong> 个 · 同参数必得同输出（种子已写进每行 GeneratorSeed）
          </span>
        </div>

        <PanelState
          v-if="!currentProject"
          state="empty"
          title="先生成不了：没有选中项目"
          hint="generate 是「往某个项目里铺令牌」，所以上面必须先选一个项目（或新建一个）。"
        />

        <template v-else>
          <div class="pj__grid">
            <label class="pj__field pj__field--wide">
              <span class="ds-micro">brief（可空 = 纯参数模式）</span>
              <textarea v-model="brief" class="ds-input pj__area" rows="2" placeholder="例：面向运维的控制台，密度高、深色要能读"></textarea>
              <span class="ds-micro pj__note">brief 不参与色彩符号学判断：没给种子色时后端只按它的稳定哈希定色相（同文本必同色）</span>
            </label>

            <div class="pj__field">
              <span class="ds-micro">seedColor（留空 = 由后端推导）</span>
              <div class="pj__seed">
                <input v-if="pickerValue" class="pj__picker" type="color" :value="pickerValue" aria-label="取色器" @input="onPick" />
                <span v-else class="pj__picker pj__picker--void" title="填三位或六位 hex 后这里变成取色器"></span>
                <input v-model="seedHex" class="ds-input" type="text" placeholder="hex（如 aabbcc，可省 #）或 oklch(0.6 0.18 265)" autocomplete="off" />
              </div>
              <span v-if="seedProblem" class="pj__field-err">{{ seedProblem }}</span>
            </div>

            <label class="pj__field">
              <span class="ds-micro">hue（0~360，优先级低于 seedColor）</span>
              <input v-model="hue" class="ds-input" type="text" inputmode="decimal" placeholder="留空=不覆盖" />
            </label>
            <label class="pj__field">
              <span class="ds-micro">chroma（oklch 彩度）</span>
              <input v-model="chroma" class="ds-input" type="text" inputmode="decimal" placeholder="留空=行业缺省" />
            </label>
            <label class="pj__field">
              <span class="ds-micro">density</span>
              <select v-model="density" class="ds-input">
                <option value="">留空=行业缺省</option>
                <option v-for="d in DENSITIES" :key="d" :value="d">{{ d }}</option>
              </select>
            </label>
            <label class="pj__field">
              <span class="ds-micro">typeRatio（模块化比例）</span>
              <input v-model="typeRatio" class="ds-input" type="text" inputmode="decimal" placeholder="如 1.25" />
            </label>
            <label class="pj__field">
              <span class="ds-micro">typeBasePx（正文基准）</span>
              <input v-model="typeBasePx" class="ds-input" type="text" inputmode="decimal" placeholder="留空=16" />
            </label>
            <label class="pj__field">
              <span class="ds-micro">radiusBase（圆角中点 px）</span>
              <input v-model="radiusBase" class="ds-input" type="text" inputmode="decimal" placeholder="留空=行业缺省" />
            </label>
            <label class="pj__field">
              <span class="ds-micro">motionScale（时长倍率 0.5~2）</span>
              <input v-model="motionScale" class="ds-input" type="text" inputmode="decimal" placeholder="留空=行业缺省" />
            </label>
            <label class="pj__field">
              <span class="ds-micro">brandName</span>
              <input v-model="brandName" class="ds-input" type="text" placeholder="写进 component 层的品牌名" autocomplete="off" />
            </label>
            <label class="pj__field">
              <span class="ds-micro">industry（只决定参数缺省包）</span>
              <select v-model="industry" class="ds-input">
                <option value="">留空=由 brief 推断</option>
                <option v-for="i in INDUSTRIES" :key="i" :value="i">{{ i }}</option>
              </select>
            </label>

            <div class="pj__field pj__field--wide">
              <span class="ds-micro">themes（要铺语义层的主题；不选 = 后端只出 light）</span>
              <div class="pj__themes ds-row ds-wrap ds-gap-2">
                <label v-for="t in themes" :key="t.code" class="pj__check">
                  <input type="checkbox" :checked="picked.includes(t.code)" @change="toggleTheme(t.code)" />
                  <span>{{ t.code }}</span>
                  <span class="ds-micro">{{ t.modeKind }}</span>
                </label>
                <span v-if="!themes.length" class="ds-small">该项目没有主题，先回上表选一个正常项目。</span>
              </div>
            </div>
          </div>

          <p v-if="paramProblem" class="pj__error" role="alert">{{ paramProblem }}</p>

          <div class="pj__acts ds-row ds-wrap ds-gap-3">
            <button class="ds-btn" type="button" :disabled="!canPreview" @click="runPreview">
              {{ previewBusy ? '计算中…' : '① 预览（不落库）' }}
            </button>
            <button class="ds-btn ds-btn--primary" type="button" :disabled="!canGenerate" @click="runGenerate">
              {{ genBusy ? '写入中…' : '② 确认写入（generate）' }}
            </button>
            <label class="pj__check pj__check--risk">
              <input v-model="overwrite" type="checkbox" />
              <span>overwrite=true</span>
            </label>
            <span class="ds-micro pj__note">
              {{ overwrite ? '风险：连人工手改（Generator=manual）的行一起重写，AC18 的保护失效' : '默认 false：人工手改过的行受保护，只补新的、不覆盖它们' }}
            </span>
          </div>

          <p v-if="paramsStale" class="pj__warn" role="alert">参数已改动，当前预览不再代表将要写入的内容 —— 请重新点「① 预览」。</p>
          <p v-if="genError" class="pj__error" role="alert">后端原文：{{ genError }}</p>

          <!-- 预览结果（无副作用，可反复看） -->
          <div v-if="previewData" class="pj__preview ds-surface-2">
            <div class="ds-row ds-wrap ds-gap-4">
              <span class="ds-small">种子 <strong class="ds-mono">{{ previewData.seed }}</strong></span>
              <span class="ds-small">行业 <strong>{{ previewData.industry }}</strong></span>
              <span class="ds-small">色相 <strong class="ds-num">{{ previewData.hue }}°</strong></span>
              <span class="ds-small">共享层令牌 <strong class="ds-num">{{ previewData.shared }}</strong></span>
              <span class="ds-small">各主题条数
                <strong v-for="[c, n] in previewThemeEntries" :key="c" class="ds-num">{{ c }}={{ n }} </strong>
              </span>
            </div>
            <div class="pj__strip">
              <span
                v-for="s in previewData.sample"
                :key="s.path"
                class="pj__chip"
                :style="{ background: s.value ?? 'transparent' }"
                :title="`${s.path} = ${s.value}`"
              >
                <span class="pj__chip-label">{{ s.path.replace('color.', '') }}</span>
              </span>
            </div>
            <p class="ds-micro pj__note">上面只是预览：色值来自后端 generate/preview 的计算结果，没有写任何一行库。</p>
            <ul v-if="previewData.notes.length" class="pj__notes">
              <li v-for="n in previewData.notes" :key="n">{{ n }}</li>
            </ul>
          </div>

          <!-- 写入结果：必须带后端 audit 摘要 + 回读 -->
          <div v-if="genResult" class="pj__result ds-surface-2">
            <h4 class="ds-h4">写入结果（后端返回）</h4>
            <div class="ds-row ds-wrap ds-gap-4">
              <span class="ds-small">种子 <strong class="ds-mono">{{ genResult.seed }}</strong></span>
              <span class="ds-small">行业 <strong>{{ genResult.industry }}</strong></span>
              <span class="ds-small">色相 <strong class="ds-num">{{ genResult.hue }}°</strong></span>
              <span class="ds-small">令牌 <strong class="ds-num">{{ genResult.tokens }}</strong></span>
              <span class="ds-small">组件目录 <strong class="ds-num">{{ genResult.components ?? 0 }}</strong></span>
              <span class="ds-small">变体格 <strong class="ds-num">{{ genResult.variants ?? 0 }}</strong></span>
              <span class="ds-small">字体 <strong class="ds-num">{{ genResult.fonts ?? 0 }}</strong></span>
              <span class="ds-small">页面 <strong class="ds-num">{{ genResult.screens ?? 0 }}</strong></span>
              <span class="ds-small">资产 <strong class="ds-num">{{ genResult.assets ?? 0 }}</strong></span>
              <span class="ds-small">主题 <strong v-for="c in genResult.themes" :key="c">{{ c }} </strong></span>
            </div>
            <p class="ds-micro pj__caveat">
              后五项是库里现存条数（种子只补空、不覆盖同名行），逐条可在「组件库」与「品牌资产」里看到。
            </p>
            <div class="ds-row ds-wrap ds-gap-4 pj__gate">
              <span class="ds-small">审计 total <strong class="ds-num">{{ genResult.audit.total }}</strong></span>
              <span class="ds-small">critical <strong class="pj__bad">{{ genResult.audit.critical }}</strong></span>
              <span class="ds-small">warning <strong class="pj__warn-text">{{ genResult.audit.warning }}</strong></span>
              <span class="ds-small">{{ genResult.audit.blocking ? '→ 有未通过的 critical，发布门禁会拒绝（详见「审计与门禁」）' : '→ 无阻断项' }}</span>
            </div>
            <ul v-if="genResult.notes.length" class="pj__notes">
              <li v-for="n in genResult.notes" :key="n">{{ n }}</li>
            </ul>
            <!--
              手改保护必须可见：静默跳过几十行 = "生成成功但库里没变"的静默失败（AC18 的判据）。
              后端早就回了 skippedProtected/conflicts，此前界面没渲染，等于这条约束没接上。
            -->
            <p v-if="(genResult.skippedProtected ?? 0) > 0 || genResult.conflicts?.length" class="pj__warn-text">
              人工手改（Generator=manual）的保护生效：跳过 {{ genResult.skippedProtected ?? 0 }} 行未覆盖。
              <template v-if="genResult.conflicts?.length">冲突：{{ genResult.conflicts.join('；') }}</template>
              要连手改一起重写，勾上 overwrite 再执行（会二次确认）。
            </p>
            <p class="pj__hint">{{ readback }}</p>
          </div>
        </template>
      </div>

      <!-- 当前项目设置 -->
      <div v-if="currentProject" class="pj__settings ds-surface-2">
        <h4 class="ds-h4">当前项目设置（{{ currentProject.code }}）</h4>
        <div class="pj__grid">
          <label class="pj__field">
            <span class="ds-micro">name</span>
            <input v-model="editName" class="ds-input" type="text" />
          </label>
          <label class="pj__field">
            <span class="ds-micro">status</span>
            <select v-model="editStatus" class="ds-input">
              <option v-for="s in STATUSES" :key="s" :value="s">{{ s }}</option>
            </select>
          </label>
        </div>
        <div class="ds-row ds-wrap ds-gap-3">
          <button class="ds-btn" type="button" :disabled="saving" @click="saveProject">{{ saving ? '保存中…' : '保存（PUT projects/{id}）' }}</button>
          <button class="ds-mini ds-mini--danger" type="button" @click="archiveCurrent">归档本项目</button>
          <span class="ds-micro pj__note">status 三态 draft|published|archived；archived 是软删 —— 令牌与审计行都还在，随时可改回 draft。</span>
        </div>
        <p v-if="writeHint" class="pj__hint">{{ writeHint }}</p>
        <p v-if="writeError" class="pj__error" role="alert">后端原文：{{ writeError }}</p>
      </div>
    </template>
  </section>
</template>

<style scoped>
.pj {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-5);
}
.pj__top {
  display: grid;
  grid-template-columns: minmax(0, 1.6fr) minmax(260px, 1fr);
  gap: var(--ds-space-4);
  align-items: start;
}
@media (max-width: 1100px) {
  .pj__top {
    grid-template-columns: 1fr;
  }
}
.pj__list,
.pj__new,
.pj__wizard,
.pj__settings {
  padding: var(--ds-space-4);
  min-width: 0;
}
.pj__list-head {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--ds-space-3);
  flex-wrap: wrap;
  margin-bottom: var(--ds-space-3);
}
.pj__table {
  width: 100%;
  border-collapse: collapse;
  font-size: var(--ds-fs-small);
}
.pj__table th {
  text-align: left;
  color: var(--ds-fg-3);
  font-weight: var(--ds-fw-medium);
  border-bottom: 1px solid var(--ds-border-1);
  padding: 6px var(--ds-space-2);
  white-space: nowrap;
}
.pj__table td {
  padding: 6px var(--ds-space-2);
  border-bottom: 1px solid var(--ds-border-1);
  vertical-align: middle;
}
.pj__num {
  text-align: right;
}
.pj__row--on td {
  background: color-mix(in oklab, var(--ds-color-primary) 10%, transparent);
}
.pj__id {
  margin-left: 4px;
}
.pj__ops {
  white-space: nowrap;
  text-align: right;
}
.pj__status {
  font-size: var(--ds-fs-micro);
  padding: 1px 8px;
  border-radius: var(--ds-radius-pill);
  border: 1px solid var(--ds-border-1);
  background: var(--ds-surface-2);
  color: var(--ds-fg-2);
}
.pj__status--published {
  color: var(--ds-success);
}
.pj__status--archived {
  color: var(--ds-fg-4);
}
.pj__status--draft {
  color: var(--ds-warning);
}
.pj__grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: var(--ds-space-3);
  margin-bottom: var(--ds-space-3);
}
.pj__field {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
}
.pj__field--wide {
  grid-column: 1 / -1;
}
.pj__field-err {
  color: var(--ds-danger);
  font-size: var(--ds-fs-small);
}
.pj__note {
  color: var(--ds-fg-4);
  text-transform: none;
  letter-spacing: 0;
}
.pj__area {
  resize: vertical;
  font-family: var(--ds-font-sans);
}
.pj__seed {
  display: flex;
  align-items: center;
  gap: var(--ds-space-2);
}
.pj__picker {
  inline-size: 34px;
  block-size: 30px;
  padding: 2px;
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-sm);
  background: var(--ds-surface-1);
  cursor: pointer;
}
/* 种子色还不是 hex 时的占位：同一个盒子形状，虚线表示"这里还没有颜色可取" */
.pj__picker--void {
  display: inline-block;
  border-style: dashed;
  background: var(--ds-surface-2);
  cursor: default;
}
.pj__themes {
  padding: var(--ds-space-2);
  border: 1px dashed var(--ds-border-2);
  border-radius: var(--ds-radius-md);
}
.pj__check {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: var(--ds-fs-small);
  color: var(--ds-fg-2);
  cursor: pointer;
}
.pj__check--risk {
  color: var(--ds-danger);
}
.pj__acts {
  align-items: center;
}
.pj__preview,
.pj__result {
  margin-top: var(--ds-space-3);
  padding: var(--ds-space-3);
}
.pj__strip {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  margin: var(--ds-space-2) 0;
}
.pj__chip {
  min-width: 68px;
  height: 26px;
  border-radius: var(--ds-radius-sm);
  border: 1px solid var(--ds-border-2);
  display: inline-flex;
  align-items: center;
  justify-content: center;
}
.pj__chip-label {
  font-family: var(--ds-font-mono);
  font-size: var(--ds-fs-micro);
  /* 色块上的文字不能假设有底色：用后端语义前景变量，跟随换肤 */
  color: var(--ds-fg-1);
  background: color-mix(in oklab, var(--ds-surface-1) 70%, transparent);
  padding: 0 4px;
  border-radius: var(--ds-radius-xs);
}
.pj__notes {
  margin: var(--ds-space-2) 0 0;
  padding-left: var(--ds-space-5);
  color: var(--ds-fg-3);
  font-size: var(--ds-fs-small);
}
.pj__gate {
  margin-top: var(--ds-space-2);
}
.pj__bad {
  color: var(--ds-danger);
}
.pj__warn-text {
  color: var(--ds-warning);
}
.pj__error {
  color: var(--ds-danger);
  font-size: var(--ds-fs-small);
}
.pj__warn {
  color: var(--ds-warning);
  font-size: var(--ds-fs-small);
}
.pj__hint {
  color: var(--ds-success);
  font-size: var(--ds-fs-small);
}
.pj__caveat {
  margin: 0;
  color: var(--ds-fg-3);
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
  padding: 2px 8px;
  margin-left: 4px;
  cursor: pointer;
}
.ds-mini--danger {
  color: var(--ds-danger);
}
</style>
