/**
 * 开始模式向导状态机（FR4 / AC8）。**纯模块**：不碰 DOM、不直接 import api——
 * 网络三个动作（推荐 / 全量预设 / 创建）与"创建后回读"由注入的 `deps` 提供，
 * 于是 vitest 能用受控 promise 断言「推进/回退/必填/单飞/失败保留/陈旧丢弃」。
 *
 * 用法：`reactive(new Wizard(deps))`（界面）或 `new Wizard(mockDeps)`（测试）。
 */
import type { GenerateRequest, PresetMatch, QuickCreateInput, QuickCreateResult, StylePreset } from '../api'
import { createLatest } from '../design/latest'
import { ApiError } from '../http'
import { defaultTune, isValidBrandColor, tuneFromPreset, tuneToRequest, type TuneState } from '../showroom/tune'

/* ------------------------------------------------------------------ */
/* 场景（§W 映射：场景 → 项目 kind + 默认舞台场景）                      */
/* ------------------------------------------------------------------ */

export type SceneId = 'admin' | 'workbench' | 'board' | 'landing' | 'mobile' | 'other'

export interface SceneOption {
  id: SceneId
  /** 大白话标签（§U 契约的 `[data-scene]` 选项） */
  label: string
  hint: string
  /** 项目 kind（后端 `quick-create` 的 kind） */
  kind: string
  /** 默认舞台场景（展厅 Stage 用） */
  stageScenario: string
}

export const SCENES: readonly SceneOption[] = [
  { id: 'admin', label: '后台 / 中台管理', hint: '表格、表单、仪表盘', kind: 'console', stageScenario: 'admin' },
  { id: 'workbench', label: '工具 / 工作台', hint: '编辑器、工作区', kind: 'console', stageScenario: 'workbench' },
  { id: 'board', label: '状态板', hint: '监控、告警大屏', kind: 'console', stageScenario: 'board' },
  { id: 'landing', label: '官网 / 落地页', hint: '营销页、产品介绍', kind: 'marketing', stageScenario: 'landing' },
  { id: 'mobile', label: '移动端 H5', hint: '手机上的页面', kind: 'product', stageScenario: 'mobile' },
  { id: 'other', label: '其它', hint: '还没想好', kind: 'product', stageScenario: 'admin' },
]

export function sceneOf(id: SceneId | null): SceneOption | null {
  return SCENES.find((s) => s.id === id) ?? null
}

/* ------------------------------------------------------------------ */
/* 预设选择                                                            */
/* ------------------------------------------------------------------ */

/** 预设卡（推荐与全量统一成同一形状；`request` 为 null 表示只剩目录信息可展示） */
export interface PresetChoice {
  id: string
  name: string
  tagline: string
  request: GenerateRequest | null
}

function toChoice(m: PresetMatch): PresetChoice {
  return { id: m.id, name: m.name, tagline: m.tagline, request: m.request ?? null }
}

/* ------------------------------------------------------------------ */
/* 依赖与状态                                                          */
/* ------------------------------------------------------------------ */

export interface WizardDeps {
  /** `POST presets/recommend`（`limit=3` 取 Top-3；`limit=100` 取全量以携带各自 request） */
  recommend: (input: { kind?: string | null; brief?: string | null; limit: number }) => Promise<PresetMatch[]>
  /** `GET presets`（推荐失败时的目录兜底；无 request） */
  catalog: () => Promise<StylePreset[]>
  /** `POST projects/quick-create`（dryRun=false 落库） */
  create: (input: QuickCreateInput) => Promise<QuickCreateResult>
  /** 创建后按 code 回读最新项目（核对令牌数，>0 才算真落库；查不到返回 null） */
  reload: (code: string) => Promise<{ tokenCount: number } | null>
}

export type WizardStep = 1 | 2 | 3 | 4
export type WizardPhase = 'idle' | 'recommending' | 'creating' | 'done'

export const CODE_PATTERN = /^[a-z0-9][a-z0-9-]{0,39}$/

function errorMessage(e: unknown): string {
  return e instanceof Error ? e.message : String(e)
}

/** 创建冲突（code 占用）：后端 `quick-create` 目前是 400 + 中文文案，故文案兜底也要认 */
function isCodeConflict(e: unknown): boolean {
  if (e instanceof ApiError && e.isConflict) return true
  return /code|已存在/i.test(errorMessage(e))
}

export class Wizard {
  step: WizardStep = 1
  scene: SceneId | null = null
  brief = ''
  presetId: string | null = null
  presetRequest: GenerateRequest | null = null
  tune: TuneState = defaultTune()

  /**
   * 风格轴字段清单（由界面从 `meta.styleAxes` 注入）。
   * 状态机自己不列轴名：填了才认，没填（词表还没到）就一个轴都不带，绝不猜。
   */
  axisFields: string[] = []

  /** 推荐的 Top-3（进②时取；陈旧响应丢弃） */
  matches: PresetChoice[] = []
  /** 「全部风格」展开后的全量（惰性取；推荐失败时也用它兜底） */
  allChoices: PresetChoice[] = []
  allLoaded = false
  showAll = false

  phase: WizardPhase = 'idle'
  recommendError = ''
  error = ''
  warnings: string[] = []
  advancedOpen = false
  name = ''
  code = ''
  created: { code: string; name: string; tokenCount: number } | null = null

  constructor(private readonly deps: WizardDeps) {}

  get sceneOption(): SceneOption | null {
    return sceneOf(this.scene)
  }

  /** 当前要展示的预设卡：展开全量 / 推荐为空但有兜底 → 全部，否则 Top-3 */
  get choices(): PresetChoice[] {
    if (this.showAll) return this.allChoices.length ? this.allChoices : this.matches
    if (this.matches.length === 0) return this.allChoices
    return this.matches
  }

  /* ---- 步骤门禁 ---- */

  /** ①→② 需已选场景；②→③ 需已选预设；③→④ 恒可；④ 需名称合法 */
  canAdvance(): boolean {
    if (this.phase === 'creating') return false
    if (this.step === 1) return this.scene !== null
    if (this.step === 2) return this.presetId !== null
    if (this.step === 3) return true
    return this.canSubmit()
  }

  async next(): Promise<void> {
    if (!this.canAdvance()) return
    if (this.step === 1) {
      this.step = 2
      await this.recommend()
      return
    }
    if (this.step < 4) this.step = (this.step + 1) as WizardStep
  }

  /** 回退保留全部输入（不重置任何字段） */
  back(): void {
    if (this.phase === 'creating') return
    if (this.step > 1) this.step = (this.step - 1) as WizardStep
  }

  pickScene(id: SceneId): void {
    this.scene = id
  }

  setBrief(text: string): void {
    this.brief = text
  }

  /* ---- 推荐（序号守卫：陈旧响应丢弃；失败回落全部预设） ---- */

  async recommend(): Promise<void> {
    if (!this.scene) return
    const run = createLatest()
    this.phase = 'recommending'
    this.recommendError = ''
    try {
      const matches = await this.deps.recommend({ kind: this.sceneOption?.kind ?? null, brief: this.brief, limit: 3 })
      if (!run.isCurrent()) return
      this.matches = matches.map(toChoice)
    } catch (e) {
      if (!run.isCurrent()) return
      this.recommendError = errorMessage(e)
      await this.loadAll(run)
    } finally {
      if (run.isCurrent()) this.phase = 'idle'
    }
  }

  /** 取全量预设（携带 request）；失败再回落 `GET presets` 目录（无 request，只能展示与选 id） */
  async loadAll(run = createLatest()): Promise<void> {
    if (this.allLoaded) return
    try {
      const all = await this.deps.recommend({ kind: this.sceneOption?.kind ?? null, brief: this.brief, limit: 100 })
      if (!run.isCurrent()) return
      this.allChoices = all.map(toChoice)
      this.allLoaded = true
    } catch {
      if (!run.isCurrent()) return
      try {
        const cat = await this.deps.catalog()
        if (!run.isCurrent()) return
        this.allChoices = cat.map((p) => ({ id: p.id, name: p.name, tagline: p.tagline, request: null }))
        this.allLoaded = true
      } catch {
        /* 目录也拿不到：保留空态，不炸界面 */
      }
    }
  }

  async toggleShowAll(open: boolean): Promise<void> {
    this.showAll = open
    if (open && !this.allLoaded) await this.loadAll()
  }

  pickPreset(id: string): void {
    const choice = this.choices.find((c) => c.id === id)
    if (!choice) return
    this.presetId = id
    this.presetRequest = choice.request
    this.tune = tuneFromPreset(choice.request, this.axisFields)
  }

  /* ---- 第③步微调 ---- */

  /** 选一条风格轴的取值（`field` 来自 meta，值来自该轴的 values；本方法不认识任何具体轴名） */
  setAxis(field: string, value: string | number): void {
    this.tune = { ...this.tune, axes: { ...this.tune.axes, [field]: value } }
  }

  setTune(patch: Partial<TuneState>): void {
    this.tune = { ...this.tune, ...patch }
  }

  /* ---- 第④步命名与创建 ---- */

  nameError(): string {
    const n = this.name.trim()
    if (!n) return '给这套风格起个名字吧'
    if (n.length > 60) return '名字最多 60 个字'
    return ''
  }

  codeError(): string {
    const c = this.code.trim()
    if (!c) return ''
    return CODE_PATTERN.test(c) ? '' : '项目代码只能用小写字母、数字和中划线，且以字母或数字开头'
  }

  brandError(): string {
    return isValidBrandColor(this.tune.seedColor) ? '' : '品牌色要写成 #abc 或 #aabbcc 这种形式'
  }

  canSubmit(): boolean {
    return !this.nameError() && !this.codeError() && !this.brandError()
  }

  /** 首次提交校验；返回错误文案（''=通过）。用于按钮禁用态外的显式提示。 */
  validate(): string {
    return this.nameError() || this.brandError() || this.codeError()
  }

  /** 创建（单飞：`phase==='creating'` 时再次提交被忽略；失败停④保留输入） */
  async submit(): Promise<void> {
    if (this.phase === 'creating' || this.phase === 'done') return
    const err = this.validate()
    if (err) {
      this.error = err
      return
    }
    this.phase = 'creating'
    this.error = ''
    const name = this.name.trim()
    const code = this.code.trim() || null
    try {
      const res = await this.deps.create({
        name,
        code,
        kind: this.sceneOption?.kind ?? null,
        description: this.brief.trim() || null,
        preset: this.presetId,
        request: this.presetRequest ? tuneToRequest(this.presetRequest, this.tune) : null,
        dryRun: false,
      })
      const createdCode = res.project?.code ?? code ?? ''
      const reloaded = createdCode ? await this.deps.reload(createdCode) : null
      this.created = { code: createdCode, name: res.project?.name ?? name, tokenCount: reloaded?.tokenCount ?? 0 }
      this.warnings = res.warnings ?? []
      this.phase = 'done'
    } catch (e) {
      this.error = errorMessage(e)
      this.phase = 'idle'
      // mode code 冲突：展开「高级」把 code 露出来让用户改（§W）
      if (isCodeConflict(e)) this.advancedOpen = true
    }
  }
}

/** 场景 id → 默认舞台场景（展厅接线用；不在场景表里则回落 admin） */
export function stageScenarioOf(scene: SceneId | null): string {
  return sceneOf(scene)?.stageScenario ?? 'admin'
}