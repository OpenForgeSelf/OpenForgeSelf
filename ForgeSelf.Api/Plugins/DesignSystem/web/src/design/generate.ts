/**
 * 设计系统生成器（通用内核）。
 *
 * 职责：输入一段需求描述 → 输出「一份属于该需求的设计系统」，
 * 其完备度与形态对标参考示例 Stardust（品牌 / 颜色 / 类型 / 间距 / 组件 / 应用示例 / 自检）。
 *
 * 设计取向：**确定性 + 可解释**。不接 LLM：
 * - 同业关键词 → 行业画像（决定品牌色相、字体搭配、组件集合、应用形态）
 * - 颜色词 → 直接覆盖品牌色相（"做一个蓝色调的…"）
 * - 其余 token（间距/圆角/阴影/动效/中性色/语义色）走**稳定导轨**，跨品牌不变
 *   理由：这些是结构与可达性的骨架，随品牌漂移只会带来脏观感和可达性风险。
 *
 * 因此同一段需求永远产出同一份设计系统 —— 可复现、可 diff、可回归、可离线。
 */

import type { ApplicationSpec, ComponentSpec, DesignSystem, Scale } from './schema.ts'
import { NEUTRAL, SEMANTIC, accentHueOf, brandScale, surfaceSet, textSet } from './colorScale.ts'

/* =========================================================================
 * 稳定导轨（跨品牌不变的结构性 token）
 * ====================================================================== */

const SPACING: Scale = {
  'space-1': '4px',
  'space-2': '8px',
  'space-3': '12px',
  'space-4': '16px',
  'space-5': '24px',
  'space-6': '32px',
  'space-7': '48px',
  'space-8': '64px',
  'space-9': '96px',
}
const RADIUS: Scale = { xs: '4px', sm: '6px', md: '8px', lg: '12px', xl: '16px', '2xl': '24px', pill: '999px' }
const ELEVATION = {
  'shadow-sm': '0 1px 2px rgba(15, 23, 42, 0.06), 0 1px 3px rgba(15, 23, 42, 0.08)',
  'shadow-md': '0 4px 6px rgba(15, 23, 42, 0.05), 0 10px 15px rgba(15, 23, 42, 0.08)',
  'shadow-lg': '0 10px 25px rgba(15, 23, 42, 0.1), 0 20px 40px rgba(15, 23, 42, 0.12)',
}
const BORDER = {
  'border-1': 'rgba(15, 23, 42, 0.08)',
  'border-2': 'rgba(15, 23, 42, 0.14)',
  'border-3': 'rgba(15, 23, 42, 0.24)',
}
const MOTION = {
  easing: { 'ease-out-expo': 'cubic-bezier(0.16, 1, 0.3, 1)', 'ease-standard': 'cubic-bezier(0.4, 0, 0.2, 1)' },
  duration: { fast: '150ms', base: '250ms', slow: '400ms' },
}
const TYPE_SCALE = {
  display: '64px',
  h1: '40px',
  h2: '28px',
  h3: '20px',
  h4: '16px',
  'body-lg': '18px',
  body: '15px',
  small: '13px',
  micro: '12px',
}
const WEIGHTS = { regular: '400', medium: '500', semibold: '600', bold: '700' }
const LINE_HEIGHTS = { display: '1.05', tight: '1.2', normal: '1.5', relaxed: '1.65' }

/** 核心组件（任何设计系统都要有）。 */
const CORE_COMPONENTS: ComponentSpec[] = [
  { name: 'Buttons', purpose: '承载页面主动作', variants: 'Primary / Secondary / Ghost / Danger', anatomy: ['文字', '可选图标', '填充或描边'] },
  { name: 'Button States', purpose: '保证交互反馈完整', variants: 'default / hover / active / focus / disabled', anatomy: ['背景色阶', '阴影层级', 'focus 描边'] },
  { name: 'Inputs', purpose: '数据录入', variants: 'default / focus / invalid', anatomy: ['标签', '输入框', '提示与错误文案'] },
  { name: 'Status Badges', purpose: '表达资源状态', variants: '健康 / 降级 / 故障 / 进行中', anatomy: ['状态点', '图标', '文字', '语义底色'] },
  { name: 'Nav Item', purpose: '主导航', variants: 'active / rest', anatomy: ['图标', '文字', '激活态指示条'] },
  { name: 'Table Row', purpose: '列表化资源', variants: '默认 / hover', anatomy: ['名称', '状态', '关键指标'] },
]

/* =========================================================================
 * 行业画像
 * ====================================================================== */

interface Industry {
  key: string
  label: string
  keywords: string[]
  /** 品牌色相（HSL hue 0–360）。 */
  hue: number
  fontKey: string
  /** 品牌调性一句话（写进 Logo/渐变说明）。 */
  tone: string
  components: ComponentSpec[]
  applications: ApplicationSpec[]
}

const INDUSTRIES: Industry[] = [
  {
    key: 'devtools',
    label: '开发者工具 / 云与运维',
    keywords: ['控制台', '监控', '运维', '集群', '部署', '指标', '可观测', '日志', 'api', 'k8s', 'kubernetes', '开发者', 'devops', '云'],
    hue: 262,
    fontKey: 'inter',
    tone: '理性、精密、可信；强调信息密度与状态可读',
    components: [
      { name: 'Metric Cards', purpose: '呈现关键运行指标', variants: 'up / down / flat', anatomy: ['指标名', '数值（tabular-nums）', '单位', '趋势'] },
      { name: 'Code Block', purpose: '展示配置与代码', variants: 'YAML / JSON / 代码', anatomy: ['文件名栏', '语法高亮'] },
      { name: 'Topology View', purpose: '呈现依赖与影响面', variants: '画布 / 图例', anatomy: ['节点', '依赖边', '详情面板'] },
      { name: 'Log Console', purpose: '查看日志流', variants: '按级别着色', anatomy: ['级别标签', '时间', '正文'] },
      { name: 'Console UI Kit', purpose: '控制台界面套件', variants: 'Overview / Services / Topology / Config / Deployments', anatomy: ['侧栏', '内容区', '指标栅格'] },
    ],
    applications: [
      { name: '控制台 Console', desc: '运维与研发的日常工作台', sections: ['总览 Overview', '资源列表', '依赖拓扑', '参数配置', '发布流水'] },
      { name: '官网 Marketing', desc: '品牌叙事与转化', sections: ['Hero', '能力矩阵', '架构图', '代码示例', 'CTA'] },
    ],
  },
  {
    key: 'finance',
    label: '金融 / 支付',
    keywords: ['金融', '支付', '银行', '理财', '证券', '风控', '账单', '结算', '交易', '保险'],
    hue: 220,
    fontKey: 'plex',
    tone: '稳重、专业、可核验；数字必须等宽对齐',
    components: [
      { name: 'Amount Display', purpose: '金额展示', variants: '正数 / 负数 / 遮掩', anatomy: ['货币符号', '整数位', '小数位（弱化）'] },
      { name: 'KPI Card', purpose: '经营指标', variants: '同比 / 环比', anatomy: ['指标名', '数值', '对比增量'] },
      { name: 'Data Table', purpose: '高密度账目', variants: '紧凑 / 可排序 / 分页', anatomy: ['列头', '行', '合计行'] },
      { name: 'Risk Badge', purpose: '风险等级', variants: '低 / 中 / 高', anatomy: ['色块', '等级文字'] },
      { name: 'Stepper', purpose: '流程进度（开户/放款）', variants: '横向 / 纵向', anatomy: ['步骤点', '连接线', '说明'] },
    ],
    applications: [
      { name: '交易台 Trading', desc: '高频、信息密集的操作界面', sections: ['行情区', '持仓与委托', '风控提示'] },
      { name: '风控后台', desc: '审核与规则配置', sections: ['待审队列', '风险画像', '规则配置'] },
    ],
  },
  {
    key: 'health',
    label: '医疗 / 健康',
    keywords: ['医疗', '健康', '医院', '患者', '病历', '问诊', '体检', '药品', '护理'],
    hue: 175,
    fontKey: 'inter',
    tone: '清洁、安抚、无威胁；留白充足、对比明确',
    components: [
      { name: 'Patient Card', purpose: '患者概览', variants: '概要 / 详情', anatomy: ['头像', '姓名与编号', '关键标签'] },
      { name: 'Vital Sign', purpose: '生命体征', variants: '正常 / 偏高 / 偏低', anatomy: ['指标名', '数值', '单位', '阈值提示'] },
      { name: 'Timeline', purpose: '就诊时间轴', variants: '纵向', anatomy: ['时间节点', '事件', '附件'] },
      { name: 'Dose Chip', purpose: '用药剂量', variants: '单次 / 每日', anatomy: ['药名', '剂量', '频次'] },
    ],
    applications: [
      { name: '医护工作台', desc: '医生/护士的日常工作', sections: ['今日患者', '病历详情', '医嘱'] },
      { name: '患者端', desc: '面向患者的轻应用', sections: ['首页', '问诊', '报告'] },
    ],
  },
  {
    key: 'commerce',
    label: '电商 / 零售',
    keywords: ['商城', '电商', '购物', '商品', '订单', '支付', '购物车', '结算', '零售', '促销', '会员'],
    hue: 22,
    fontKey: 'inter',
    tone: '热情、明快、促成决策；价格与库存就地可见',
    components: [
      { name: 'Product Card', purpose: '商品陈列', variants: '网格 / 列表', anatomy: ['主图', '标题', '价格', '行动按钮'] },
      { name: 'Price Tag', purpose: '价格展示', variants: '原价 / 折扣价', anatomy: ['货币符号', '价格', '划线原价'] },
      { name: 'Cart Summary', purpose: '金额试算', variants: '侧栏 / 页面', anatomy: ['商品行', '优惠', '合计', '结算按钮'] },
      { name: 'Order Status', purpose: '订单生命周期', variants: '待付款 / 待发货 / 已完成 / 已退款', anatomy: ['状态徽章', '时间', '物流'] },
    ],
    applications: [
      { name: '商城前台', desc: '浏览到下单的转化链路', sections: ['首页', '商品列表', '商品详情', '购物车', '结算'] },
      { name: '商家后台', desc: '经营与履约', sections: ['商品管理', '订单处理', '数据看板'] },
    ],
  },
  {
    key: 'content',
    label: '内容 / 媒体 / 社区',
    keywords: ['内容', '文章', '博客', '社区', '资讯', '媒体', '帖子', '创作', '发布', 'feed', '论坛', '视频'],
    hue: 340,
    fontKey: 'editorial',
    tone: '有辨识度、以阅读为中心；正文节奏优先',
    components: [
      { name: 'Article Body', purpose: '正文排版', variants: '宽栏 / 窄栏', anatomy: ['标题层级', '段落', '引用', '配图'] },
      { name: 'Editor', purpose: '内容创作', variants: '写作 / 预览', anatomy: ['标题输入', '正文区', '发布设置'] },
      { name: 'Comment', purpose: '互动', variants: '一级 / 回复', anatomy: ['头像', '正文', '操作'] },
      { name: 'Tag Chip', purpose: '分类与话题', variants: '可选 / 只读', anatomy: ['标签文字'] },
    ],
    applications: [
      { name: '内容站', desc: '消费与分发', sections: ['首页推荐', '频道', '正文详情'] },
      { name: '创作后台', desc: '生产与运营', sections: ['编辑器', '作品管理', '数据'] },
    ],
  },
  {
    key: 'edu',
    label: '教育',
    keywords: ['教育', '课程', '学习', '教学', '学生', '老师', '题库', '培训', '考试'],
    hue: 150,
    fontKey: 'nunito',
    tone: '亲和、清晰、鼓励性；进度与反馈明确',
    components: [
      { name: 'Course Card', purpose: '课程陈列', variants: '网格 / 列表', anatomy: ['封面', '标题', '进度'] },
      { name: 'Progress Ring', purpose: '学习进度', variants: '环形 / 条形', anatomy: ['已完成比例', '文字说明'] },
      { name: 'Quiz Item', purpose: '题目', variants: '单选 / 多选 / 判断', anatomy: ['题干', '选项', '解析'] },
    ],
    applications: [
      { name: '学习端', desc: '学生视角', sections: ['我的课程', '课程详情', '练习'] },
      { name: '教师后台', desc: '教学管理', sections: ['班级', '题库', '学情'] },
    ],
  },
  {
    key: 'enterprise',
    label: '企业 / 通用 SaaS',
    keywords: ['企业', 'crm', 'erp', 'oa', '协同', '办公', '人力', 'hr', 'saas', '管理后台', '门户', '系统'],
    hue: 222,
    fontKey: 'inter',
    tone: '克制、高效、可长期凝视；中性主导，品牌色只点关键动作',
    components: [
      { name: 'Data Table', purpose: '主数据管理', variants: '可筛选 / 可排序 / 批量', anatomy: ['工具栏', '列头', '行', '分页'] },
      { name: 'Form Layout', purpose: '录入与编辑', variants: '单列 / 分组 / 抽屉', anatomy: ['分组标题', '字段', '操作区'] },
      { name: 'Empty State', purpose: '空态即引导', variants: '无数据 / 无权限 / 出错', anatomy: ['插画或图标', '说明', '引导动作'] },
      { name: 'Filter Bar', purpose: '检索与筛选', variants: '平铺 / 折叠', anatomy: ['搜索框', '筛选项', '已选标签'] },
    ],
    applications: [
      { name: '管理后台', desc: '主数据与配置', sections: ['概览', '列表', '详情', '设置'] },
      { name: '官网', desc: '价值叙事与试用转化', sections: ['Hero', '功能', '定价', 'CTA'] },
    ],
  },
]

/** 字体搭配（按行业气质）。 */
const FONTS: Record<string, { sans: string; mono: string; note: string }> = {
  inter: {
    sans: "'Inter', system-ui, -apple-system, 'Segoe UI', Roboto, 'PingFang SC', 'Microsoft YaHei', sans-serif",
    mono: "'JetBrains Mono', 'SFMono-Regular', Menlo, Consolas, monospace",
    note: '中性无衬线 + 等宽，通用性最强',
  },
  plex: {
    sans: "'IBM Plex Sans', system-ui, 'PingFang SC', 'Microsoft YaHei', sans-serif",
    mono: "'IBM Plex Mono', 'SFMono-Regular', Menlo, Consolas, monospace",
    note: 'IBM Plex：数字辨识度高，适合金融/数据密集',
  },
  editorial: {
    sans: "'Source Serif 4', Georgia, 'Songti SC', serif",
    mono: "'JetBrains Mono', 'SFMono-Regular', Menlo, Consolas, monospace",
    note: '衬线标题/正文：编辑气质，长文阅读更友好',
  },
  nunito: {
    sans: "'Nunito Sans', system-ui, 'PingFang SC', 'Microsoft YaHei', sans-serif",
    mono: "'JetBrains Mono', 'SFMono-Regular', Menlo, Consolas, monospace",
    note: '圆润亲和，适合教育与面向大众的产品',
  },
}

/** 颜色词 → 色相（用户直说颜色时优先）。 */
const COLOR_WORDS: { words: string[]; hue: number }[] = [
  { words: ['蓝', 'blue'], hue: 220 },
  { words: ['绿', 'green'], hue: 150 },
  { words: ['青', 'cyan', 'teal'], hue: 187 },
  { words: ['紫', 'purple', 'violet'], hue: 262 },
  { words: ['红', 'red'], hue: 355 },
  { words: ['橙', 'orange'], hue: 25 },
  { words: ['黄', 'yellow'], hue: 45 },
  { words: ['粉', 'pink'], hue: 330 },
]

/* =========================================================================
 * 生成
 * ====================================================================== */

/** 关键词打分识别行业；无命中回落通用企业 SaaS。 */
export function detectIndustry(brief: string): Industry {
  const text = brief.toLowerCase()
  let best = INDUSTRIES.find((i) => i.key === 'enterprise') as Industry
  let bestScore = 0
  for (const ind of INDUSTRIES) {
    const score = ind.keywords.reduce((acc, k) => (text.includes(k.toLowerCase()) ? acc + 1 : acc), 0)
    if (score > bestScore) {
      bestScore = score
      best = ind
    }
  }
  return best
}

/** 需求里直说的颜色优先于行业默认色。 */
export function detectHueOverride(brief: string): number | null {
  const text = brief.toLowerCase()
  for (const c of COLOR_WORDS) {
    if (c.words.some((w) => text.includes(w))) return c.hue
  }
  return null
}

/** 抽取品牌名：优先引号内，其次首句截断。 */
export function extractName(brief: string, fallback: string): string {
  const quoted = brief.match(/[「《"'“]([^」》"'”]{2,20})[」》"'”]/)
  if (quoted) return quoted[1]
  const first = brief.split(/[。！？\n,.!?]/)[0]?.trim()
  if (first && first.length >= 2) return first.slice(0, 18)
  return fallback
}

export interface GenerateOptions {
  /** 强制指定品牌色相（0–360），覆盖自动识别。 */
  hue?: number
  /** 强制指定行业 key。 */
  industry?: string
}

/**
 * 生成一份设计系统。
 *
 * @param brief 需求描述
 * @param opts  可选覆盖（色相 / 行业）
 */
export function generateDesignSystem(brief: string, opts: GenerateOptions = {}): DesignSystem {
  const text = brief.trim()
  const industry =
    (opts.industry && INDUSTRIES.find((i) => i.key === opts.industry)) || detectIndustry(text)
  const hue = opts.hue ?? detectHueOverride(text) ?? industry.hue
  const accentHue = accentHueOf(hue)
  const brand = brandScale(hue)
  const accent = brandScale(accentHue)
  const fonts = FONTS[industry.fontKey] ?? FONTS.inter
  const name = extractName(text, `${industry.label}设计系统`)

  return {
    meta: {
      name,
      brief: text,
      source: 'generated',
      seed: { hue, accentHue, industry: industry.key, fontKey: industry.fontKey },
      generatedAt: new Date().toISOString(),
    },
    brand: {
      name,
      logoConcept: `以「${name}」首字母或核心隐喻构成 Mark + Wordmark；调性：${industry.tone}`,
      gradient: `135° ${brand['500']} → ${accent['400']}；仅用于 Logo 与 Hero / CTA 光晕，功能界面不滥用`,
      iconSystem: '线性图标；stroke-width 1.5；currentColor 着色；20 / 16 / 14 三档尺寸',
      motif: '以品牌核心隐喻构成的几何装饰元件（SVG），用于 Hero 与空态',
    },
    tokens: {
      color: {
        brand,
        accent,
        neutral: NEUTRAL,
        semantic: SEMANTIC,
        surface: surfaceSet(hue),
        text: textSet(accent['700']),
      },
      typography: {
        fontSans: fonts.sans,
        fontMono: fonts.mono,
        scale: TYPE_SCALE,
        weights: WEIGHTS,
        lineHeights: LINE_HEIGHTS,
      },
      spacing: SPACING,
      radius: RADIUS,
      elevation: ELEVATION,
      border: BORDER,
      motion: MOTION,
    },
    components: [...CORE_COMPONENTS, ...industry.components],
    applications: industry.applications,
    selfCheck: [
      { item: '状态完整性', detail: '每个组件覆盖 default / hover / active / focus / disabled，不只设计 happy path' },
      { item: '对比度可达性', detail: `正文 fg-1(${NEUTRAL['900']}) 对 surface-1(#ffffff) ≥ 4.5:1；辅助文字仅用于非关键信息` },
      { item: '间距节奏', detail: '全部间距取自 8pt 刻度（space 1–9），不出现游离值' },
      { item: '焦点可见', detail: `可交互元素具备 :focus-visible 描边（辅助色 2px + 2px offset）` },
      { item: '语义色克制', detail: 'success/warning/danger/info 仅承载状态语义，不用于装饰' },
      { item: '动效一致', detail: '统一 ease-out-expo 与 150/250/400ms 三档，不逐元素自定义' },
      { item: '中性导轨稳定', detail: '中性色与语义色不随品牌色相漂移，换肤后观感仍干净' },
      { item: '字体搭配已落地', detail: `${fonts.note}（fontKey=${industry.fontKey}）` },
      { item: '渐变使用克制', detail: '品牌渐变只用于 Logo / Hero / CTA，功能界面不用' },
    ],
  }
}

/** 供界面展示的行业选项。 */
export const INDUSTRY_OPTIONS: { key: string; label: string }[] = INDUSTRIES.map((i) => ({
  key: i.key,
  label: i.label,
}))
