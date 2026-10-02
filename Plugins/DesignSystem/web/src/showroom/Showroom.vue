<script setup lang="ts">
/**
 * 展厅（切片 A 步骤7 的编排层）。
 *
 * 一件事：把"衣柜里的衣服"渲染成"舞台上的真实界面"，全程**只搬运后端文本**。
 * - 衣柜：`buildOutfits`（项目 + 内置预设）→ `Wardrobe`；
 * - 舞台：`createOutfitLoader` 按「衣服 × 主题 × 疏密」取 CSS（`export?format=css` / `preview-css`）
 *   交给 `Stage`，由 `OutfitScope` 收窄作用域后喂给模特页；
 * - 微调：`tune.ts` 的映射叠加到预设 `request` 上，「保存为新设计」走 `quick-create`（唯一写入口）。
 *
 * 三条纪律：① 任何 `await` 后写共享 ref 都过 `createLatest()` 序号守卫；② 试穿/微调不写库；
 * ③ 取数依赖从 `api.ts` 注入（路径只在那一处出现）。
 */
import { computed, onMounted, ref, shallowRef, watch, type Component } from 'vue'
import { api, type PresetMatch, type Theme } from '../api'
import PanelState from '../components/PanelState.vue'
import { createLatest } from '../design/latest'
import { mapLimit } from '../design/pool'
import { projects as sharedProjects, projectsState } from '../state'
import AdminDashboard from './mannequins/AdminDashboard.vue'
import AdminDetail from './mannequins/AdminDetail.vue'
import AdminForm from './mannequins/AdminForm.vue'
import AdminList from './mannequins/AdminList.vue'
import AdminSettings from './mannequins/AdminSettings.vue'
import LandingHome from './mannequins/LandingHome.vue'
import MobileHome from './mannequins/MobileHome.vue'
import StatusBoard from './mannequins/StatusBoard.vue'
import WorkbenchEditor from './mannequins/WorkbenchEditor.vue'
import CompareStrip from './CompareStrip.vue'
import Stage from './Stage.vue'
import TunePanel from './TunePanel.vue'
import Wardrobe from './Wardrobe.vue'
import { applyProjectThemes, buildOutfits, createOutfitLoader, OUTFIT_LIMIT, type Outfit, type PresetInput } from './outfits'
import { SCENES, deviceById, findPage, firstPage, sceneById, type DeviceId } from './scenes'
import { defaultTune, tuneFromPreset, tuneToRequest, type DensityId, type TuneState } from './tune'
import type { RouteState } from '../design/route'

/** 深链还原入参（root 从 `parseHash` 拿到后传入；无深链时 null，走原有默认） */
const props = withDefaults(defineProps<{ initial?: RouteState | null }>(), { initial: null })

const emit = defineEmits<{
  created: [code: string]
  /** 舞台当前选择变化（供 root 写回 URL 哈希；`page` 即深链 sub） */
  route: [payload: { outfit: string; theme: string; device: string; page: string }]
}>()

/** `scenes.ts` 的 `component` 键 → SFC（注册表只留字符串键，映射在此一处） */
const MANNEQUINS: Record<string, Component> = {
  AdminDashboard,
  AdminList,
  AdminForm,
  AdminDetail,
  AdminSettings,
  StatusBoard,
  WorkbenchEditor,
  LandingHome,
  MobileHome,
}

function msg(err: unknown): string {
  return err instanceof Error ? err.message : String(err)
}

/* ---------------- 数据：预设 / 衣柜 ---------------- */

const presets = shallowRef<PresetInput[]>([])
const presetsError = ref('')
const limit = ref(OUTFIT_LIMIT)
/** 预设目录（`GET presets` + `recommend` 合并）是否仍在加载——深链判定"目标确实不存在"的依据之一（缺陷 D14） */
const presetsLoading = ref(true)

/** `GET presets` 只回目录、`recommend` 才带 `request`（偏差 D3）：按 id 合并两源，缺失的预设无预览 */
async function loadPresets(): Promise<void> {
  presetsLoading.value = true
  try {
    const catalog = await api.listPresets()
    let matches: PresetMatch[] = []
    let recommendFailed = false
    try {
      matches = await api.recommendPresets({ limit: 999 })
    } catch {
      recommendFailed = true
    }
    const requestById = new Map(matches.map((m) => [m.id, m.request]))
    presets.value = catalog.map((p) => ({ ...p, request: requestById.get(p.id) ?? null }))
    presetsError.value = recommendFailed ? '预设预览数据暂不可用（推荐接口未返回），部分预设可能无法试穿。' : ''
  } catch (err) {
    presets.value = []
    presetsError.value = `预设目录读取失败：${msg(err)}`
  } finally {
    // 无论成功/失败都落定：失败时也必须让深链有机会回落首件，不能永远等下去
    presetsLoading.value = false
  }
}

const bundle = computed(() => buildOutfits({ projects: sharedProjects.value, presets: presets.value, limit: limit.value }))

/* ---------------- 项目主题：补出"舞台可用主题集" ---------------- */

const themeMap = ref<Map<number, Theme[]>>(new Map())

async function syncThemes(projectIds: readonly number[]): Promise<void> {
  const need = projectIds.filter((id) => !themeMap.value.has(id))
  if (!need.length) return
  const loaded: [number, Theme[]][] = []
  await mapLimit(need, 3, async (id) => {
    try {
      loaded.push([id, await api.listThemes(id)])
    } catch {
      loaded.push([id, []])
    }
  })
  const next = new Map(themeMap.value)
  for (const [id, list] of loaded) next.set(id, list)
  themeMap.value = next
}

/** 项目衣服补主题集（色向主题 + 可能的 compact 密度主题） */
const mine = computed<Outfit[]>(() =>
  bundle.value.mine.map((o) => applyProjectThemes(o, themeMap.value.get(o.projectId ?? -1) ?? [])),
)

watch(
  () => bundle.value.mine.map((o) => o.projectId).filter((id): id is number => id != null),
  (ids) => void syncThemes(ids),
  { immediate: true },
)

/* ---------------- 选择：衣柜 → 舞台 ---------------- */

const options = computed<Outfit[]>(() => [...mine.value, ...bundle.value.presets])
const selectedId = ref('')
const selectedOutfit = computed<Outfit | null>(() => options.value.find((o) => o.id === selectedId.value) ?? null)

/* ---------------- 舞台状态：场景 / 页面 / 主题 / 疏密 / 设备 ---------------- */

const sceneId = ref(SCENES[0].id)
const pageId = ref(firstPage(SCENES[0]).id)
const device = ref<DeviceId>(SCENES[0].device)
const theme = ref('light')
const density = ref<DensityId>('default')

/** 深链待还原主题：选中衣服稳定后由主题重置 watcher 消费一次（声明须先于该 watcher，避免 TDZ） */
const restoreTheme = ref('')

/** 切换场景时确保页面属于该场景（页面页签随之重排） */
watch(sceneId, () => {
  const pages = sceneById(sceneId.value)?.pages ?? []
  if (!pages.some((p) => p.id === pageId.value)) pageId.value = pages[0]?.id ?? ''
})

function pickScene(id: string): void {
  sceneId.value = id
  device.value = sceneById(id)?.device ?? 'desktop'
}

const mannequin = computed<Component | undefined>(() => {
  const page = sceneById(sceneId.value)?.pages.find((p) => p.id === pageId.value)
  return page ? MANNEQUINS[page.component] : undefined
})

/* ---------------- 取数：CSS 文本 ---------------- */

const loader = createOutfitLoader({
  loadProjectCss: (projectId, t) => api.exportText(projectId, 'css', t),
  previewCss: api.previewCss,
})

const css = ref('')
const cssError = ref('')
const cssLoading = ref(false)

async function loadCss(): Promise<void> {
  const outfit = selectedOutfit.value
  if (!outfit) {
    css.value = ''
    cssError.value = ''
    return
  }
  const run = createLatest()
  cssLoading.value = true
  try {
    const text = await loader.load(outfit, { theme: theme.value, density: density.value })
    if (!run.isCurrent()) return
    css.value = text
    cssError.value = ''
  } catch (err) {
    if (!run.isCurrent()) return
    css.value = ''
    cssError.value = msg(err)
  } finally {
    if (run.isCurrent()) cssLoading.value = false
  }
}

/**
 * 换衣服：主题回到该件的首档（预设 light / 项目首个色向主题），疏密回到适中。
 * 深链还原时优先用 URL 指定主题（`restoreTheme`），消费一次即清空，避免污染后续换装。
 *
 * 为什么盯 `selectedId` 而不是 `selectedOutfit`（缺陷 D10）：`buildOutfits` 每次重算都会
 * **按 id 重建**衣服对象（项目列表/预设晚一步到达时都会触发），引用变了但"选的还是那一件"。
 * 若挂在对象上，这次重建会被误判成一次换衣服，把刚由深链还原的 `theme=dark` 重置回首档
 * ——只有"深链给了非首档主题 + 衣柜随后刷新"才暴露，浅色档看不出来。
 */
watch(selectedId, () => {
  const outfit = selectedOutfit.value
  const want = restoreTheme.value
  restoreTheme.value = ''
  theme.value = want && outfit?.themes.includes(want) ? want : (outfit?.themes[0] ?? 'light')
  density.value = 'default'
})

// 任一项变化都重取（旧响应由 loadCss 内的序号守卫丢弃）
watch([selectedOutfit, theme, density], () => void loadCss(), { immediate: true })

/* ---------------- 深链还原（切片 C 步骤12）：把 URL 哈希里的选择落成舞台状态 ---------------- */

/** 首次拿到非空衣柜时应用深链/默认选择（只应用一次，后续衣柜刷新不打断用户选择） */
let restored = false

/** 把 `props.initial`（root 从 `parseHash` 解析）落成舞台状态；非法项逐项回落，不整体失败 */
function applyInitial(list: Outfit[]): void {
  const init = props.initial
  if (init?.sub) {
    const found = findPage(init.sub)
    if (found) {
      sceneId.value = found.scene.id
      pageId.value = found.page.id
      device.value = found.scene.device
    }
  }
  if (init?.device) {
    const d = deviceById(init.device)
    if (d) device.value = d.id
  }
  const wanted = init?.outfit && list.some((o) => o.id === init.outfit) ? init.outfit : list[0].id
  restoreTheme.value = init?.theme ?? ''
  selectedId.value = wanted
}

/**
 * 深链指定衣服的**来源是否已读完** —— 读完才允许判定"它确实不存在"（缺陷 D14）。
 *
 * 缺陷现场（闸门2 独立复验读出）：深链要 `preset:admin-calm`，而"衣柜首个非空快照"可能
 * **只有项目衣服**——项目走宿主 shell 的 `projects`（先到），预设要等 `GET presets` +
 * `recommend` 合并后一次性落值（后到）。原实现只要衣柜非空就消费深链、找不到目标就回落
 * `list[0]`，并把 `restored` 闩死 → 预设到达后深链指定的衣服**永不选中**（`aria-selected` 恒 false）。
 * 串行/快网下两者同批到达，所以只在并行或慢请求下暴露。
 *
 * 只等与目标**同类**的那一路：`preset:*` 看 `presetsLoading`；`project:*` 看宿主 `projectsState`；
 * 其它（如 `tuned:*`）无外部来源，直接算读完——避免任一路卡住时整体不选。
 */
function sourceSettled(outfitId: string): boolean {
  if (outfitId.startsWith('preset:')) return !presetsLoading.value
  if (outfitId.startsWith('project:'))
    return projectsState.value !== 'idle' && projectsState.value !== 'loading'
  return true
}

watch(
  options,
  (list) => {
    if (!list.length) return
    if (!restored) {
      // 深链指定的衣服还没出现、且它的来源仍在加载 → 本轮先不消费深链（否则会把"还在路上"误判成"不存在"）
      const want = props.initial?.outfit ?? ''
      if (want && !list.some((o) => o.id === want) && !sourceSettled(want)) return
      restored = true
      applyInitial(list)
      return
    }
    // 衣柜刷新（如项目主题补全）后，仅在当前选中项失效时回落第一件
    if (!list.some((o) => o.id === selectedId.value)) selectedId.value = list[0].id
  },
  { immediate: true },
)

/** 舞台选择变化 → 通知 root 写回 URL 哈希（FR15/AC22；root 用 replaceState，不新增历史条目） */
watch([selectedId, theme, device, pageId], () => {
  emit('route', { outfit: selectedId.value, theme: theme.value, device: device.value, page: pageId.value })
})

/* ---------------- 微调 ---------------- */

const tune = ref<TuneState>(defaultTune())
const tuneError = ref('')
const saving = ref(false)
const tuneEnabled = computed(() => selectedOutfit.value != null && selectedOutfit.value.kind !== 'project')

/* 换衣服才重置微调面板；同样盯 `selectedId`（理由见上，D10），避免衣柜刷新时清掉用户正在调的参数 */
watch(selectedId, () => {
  const outfit = selectedOutfit.value
  tuneError.value = ''
  tune.value = tuneFromPreset(outfit?.request ?? null)
})

function onTune(patch: Partial<TuneState>): void {
  tune.value = { ...tune.value, ...patch }
}

function resetTune(): void {
  tune.value = tuneFromPreset(selectedOutfit.value?.request ?? null)
}

async function saveTuned(): Promise<void> {
  const base = selectedOutfit.value
  if (!base || saving.value) return
  saving.value = true
  tuneError.value = ''
  try {
    const request = tuneToRequest(base.request ?? null, tune.value)
    const res = await api.quickCreate({ name: `${base.label} 微调`.slice(0, 60), request })
    if (!res.project) throw new Error(res.warnings?.join('；') || '创建未返回项目')
    emit('created', res.project.code)
  } catch (err) {
    tuneError.value = msg(err)
  } finally {
    saving.value = false
  }
}

onMounted(() => void loadPresets())

/* ---------------- 并排对比（§U：衣柜项按钮「加入对比」→ 对比区两帧 + 差异条） ---------------- */

/** 对比槽上限 2（§U：对比区恰两个 `[data-stage-frame]`） */
const COMPARE_LIMIT = 2

const compareIds = ref<string[]>([])

const compareOutfits = computed<Outfit[]>(() =>
  compareIds.value.map((id) => options.value.find((o) => o.id === id)).filter((o): o is Outfit => o != null),
)

/**
 * 衣柜里的「加入对比」开关：已在对比里 → 移出；否则加入，满员时挤掉最早那件
 * （恒保持 ≤2，避免出现"点了没反应"的死按钮）。
 */
function toggleCompare(id: string): void {
  if (compareIds.value.includes(id)) {
    compareIds.value = compareIds.value.filter((x) => x !== id)
    return
  }
  compareIds.value = [...compareIds.value, id].slice(-COMPARE_LIMIT)
}

const compareCss = ref<string[]>([])
const compareError = ref('')
const compareLoading = ref(false)

/** 两件对比衣服各取一次 CSS（主题取各件首档、疏密统一适中；序号守卫丢弃陈旧响应） */
async function loadCompare(): Promise<void> {
  const list = compareOutfits.value
  if (list.length < COMPARE_LIMIT) {
    compareCss.value = []
    compareError.value = ''
    compareLoading.value = false
    return
  }
  const run = createLatest()
  compareLoading.value = true
  try {
    const texts = await Promise.all(
      list.map((o) => loader.load(o, { theme: o.themes[0] ?? 'light', density: 'default' })),
    )
    if (!run.isCurrent()) return
    compareCss.value = texts
    compareError.value = ''
  } catch (err) {
    if (!run.isCurrent()) return
    compareCss.value = []
    compareError.value = msg(err)
  } finally {
    if (run.isCurrent()) compareLoading.value = false
  }
}

watch(compareOutfits, () => void loadCompare(), { immediate: true })

/** 模板事件处理器不返回 Promise（避免 no-misused-promises），内部自行兜底 */
function onSave(): void {
  void saveTuned()
}
</script>

<template>
  <section class="ds-mode-pane ds-showroom" data-showroom>
    <div class="ds-showroom__head">
      <span class="ds-micro">展厅</span>
      <span class="ds-small">挑一件衣服试穿 —— 画布上的每一处都来自后端交付文本，与导出同源。</span>
    </div>

    <p v-if="presetsError" class="ds-showroom__warn ds-small" role="alert">{{ presetsError }}</p>

    <div v-if="!options.length" class="ds-showroom__empty">
      <PanelState
        state="empty"
        title="还没有可试穿的设计系统"
        hint="去「开始」用向导创建一套，或在工作台生成令牌后再回来。"
      />
    </div>

    <div v-else class="ds-showroom__layout">
      <Wardrobe
        :mine="mine"
        :presets="bundle.presets"
        :selected-id="selectedId"
        :compare-ids="compareIds"
        :hidden-count="bundle.hiddenCount"
        :ungenerated-count="bundle.ungenerated.length"
        @select="selectedId = $event"
        @compare="toggleCompare"
        @more="limit += OUTFIT_LIMIT"
      />

      <Stage
        v-if="selectedOutfit"
        :outfit="selectedOutfit"
        :mannequin="mannequin"
        :css="css"
        :error="cssError"
        :theme="theme"
        :density="density"
        :device="device"
        :scene-id="sceneId"
        :page-id="pageId"
        @update:theme="theme = $event"
        @update:density="density = $event"
        @update:device="device = $event"
        @pick:scene="pickScene"
        @pick:page="pageId = $event"
      />

      <TunePanel
        :tune="tune"
        :enabled="tuneEnabled"
        :saving="saving"
        :error="tuneError"
        @update:tune="onTune"
        @reset="resetTune"
        @save="onSave"
      />
    </div>

    <CompareStrip
      v-if="compareOutfits.length === COMPARE_LIMIT"
      :outfits="compareOutfits"
      :css="compareCss"
      :mannequin="mannequin"
      :device="device"
      :error="compareError"
      @close="compareIds = []"
      @remove="toggleCompare"
    />

    <p v-if="cssLoading" class="ds-showroom__loading ds-small">正在取这套皮肤的投影…</p>
  </section>
</template>

<style scoped>
.ds-showroom {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-4);
}
.ds-showroom__head {
  display: flex;
  align-items: baseline;
  gap: var(--ds-space-3);
}
.ds-showroom__warn {
  margin: 0;
  color: var(--ds-warning);
}
.ds-showroom__empty {
  padding: var(--ds-space-5);
}
.ds-showroom__layout {
  display: grid;
  grid-template-columns: 240px minmax(0, 1fr) 240px;
  gap: var(--ds-space-5);
  align-items: start;
}
@media (max-width: 1100px) {
  .ds-showroom__layout {
    grid-template-columns: 1fr;
  }
}
.ds-showroom__loading {
  margin: 0;
}
</style>