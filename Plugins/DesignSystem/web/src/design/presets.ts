/**
 * 预设（Presets）。
 *
 * 两个不同性质的东西，别混：
 *
 * 1. `SHELL_TOKENS` —— **插件自身外壳**的中性主题（slate / indigo，刻意不带任何品牌性格）。
 *    插件是工具，不是品牌展台；它的界面应当低调，好让生成出来的设计系统成为视觉主角。
 *    由 scripts/gen-tokens-css.ts 生成为 src/styles/tokens.css。
 *
 * 2. `STARDUST` —— **参考示例**（verbatim）。它是「一份设计系统该长什么样」的答案样本：
 *    类型 / 颜色 / 间距 / 组件 / 品牌 / 应用示例 一应俱全，取值与需求文档逐字一致。
 *    它不是插件的身份，只是一份可被加载、对照、学习的样例数据。
 */

import type { DesignSystem } from './schema.ts'
import { NEUTRAL, SEMANTIC, accentHueOf, brandScale, hslToHex, surfaceSet, textSet } from './colorScale.ts'

/* =========================================================================
 * 1. 插件外壳主题（中性）
 * ====================================================================== */

/** 外壳色相：沉稳的 indigo-slate，工具感，无品牌性格。 */
const SHELL_HUE = 222
const SHELL_ACCENT = 205

export const SHELL: DesignSystem = {
  meta: {
    name: 'ForgeShell',
    brief: '插件自身外壳的中性主题（工具界面应低调，让生成的设计系统成为视觉主角）',
    source: 'preset',
    seed: { hue: SHELL_HUE, accentHue: SHELL_ACCENT, industry: 'enterprise', fontKey: 'inter' },
    generatedAt: '2026-09-01T00:00:00.000Z',
  },
  brand: {
    name: 'ForgeShell',
    logoConcept: '无品牌性格的几何 Mark（工具感）',
    gradient: `135° ${brandScale(SHELL_HUE)['500']} → ${brandScale(SHELL_ACCENT)['400']}`,
    iconSystem: '线性图标；stroke-width 1.5；currentColor 着色',
    motif: '无（外壳不使用装饰母题）',
  },
  tokens: {
    color: {
      brand: brandScale(SHELL_HUE),
      accent: brandScale(SHELL_ACCENT),
      neutral: NEUTRAL,
      semantic: SEMANTIC,
      surface: surfaceSet(SHELL_HUE),
      text: textSet(brandScale(SHELL_ACCENT)['700']),
    },
    typography: {
      fontSans:
        "'Inter', system-ui, -apple-system, 'Segoe UI', Roboto, 'PingFang SC', 'Microsoft YaHei', sans-serif",
      fontMono: "'JetBrains Mono', 'SFMono-Regular', Menlo, Consolas, monospace",
      scale: {
        display: '64px',
        h1: '40px',
        h2: '28px',
        h3: '20px',
        h4: '16px',
        'body-lg': '18px',
        body: '15px',
        small: '13px',
        micro: '12px',
      },
      weights: { regular: '400', medium: '500', semibold: '600', bold: '700' },
      lineHeights: { display: '1.05', tight: '1.2', normal: '1.5', relaxed: '1.65' },
    },
    spacing: {
      'space-1': '4px',
      'space-2': '8px',
      'space-3': '12px',
      'space-4': '16px',
      'space-5': '24px',
      'space-6': '32px',
      'space-7': '48px',
      'space-8': '64px',
      'space-9': '96px',
    },
    radius: { xs: '4px', sm: '6px', md: '8px', lg: '12px', xl: '16px', '2xl': '24px', pill: '999px' },
    elevation: {
      'shadow-sm': '0 1px 2px rgba(15, 23, 42, 0.06), 0 1px 3px rgba(15, 23, 42, 0.08)',
      'shadow-md': '0 4px 6px rgba(15, 23, 42, 0.05), 0 10px 15px rgba(15, 23, 42, 0.08)',
      'shadow-lg': '0 10px 25px rgba(15, 23, 42, 0.1), 0 20px 40px rgba(15, 23, 42, 0.12)',
    },
    border: {
      'border-1': 'rgba(15, 23, 42, 0.08)',
      'border-2': 'rgba(15, 23, 42, 0.14)',
      'border-3': 'rgba(15, 23, 42, 0.24)',
    },
    motion: {
      easing: { 'ease-out-expo': 'cubic-bezier(0.16, 1, 0.3, 1)', 'ease-standard': 'cubic-bezier(0.4, 0, 0.2, 1)' },
      duration: { fast: '150ms', base: '250ms', slow: '400ms' },
    },
  },
  components: [],
  applications: [],
  selfCheck: [],
}

/* =========================================================================
 * 2. 参考示例：Stardust（verbatim，逐字）
 * ====================================================================== */

/** Stardust 主色相（紫）与辅助色相（青）。 */
const STARDUST_HUE = 262

export const STARDUST: DesignSystem = {
  meta: {
    name: 'Stardust',
    brief: '参考示例：一份完整的设计系统（类型 / 颜色 / 间距 / 组件 / 品牌 / 应用示例）',
    source: 'preset',
    seed: { hue: STARDUST_HUE, accentHue: accentHueOf(STARDUST_HUE), industry: '开发者工具', fontKey: 'inter' },
    generatedAt: '2026-09-01T00:00:00.000Z',
  },
  brand: {
    name: 'Stardust',
    logoConcept: '三节点符号（连接 / 编排 / 执行）+ Wordmark；Mark 用品牌渐变描边，可作 favicon 缩至 16px 仍可辨',
    gradient: '135° 紫 → 青（#8b5cf6 → #22d3ee）；仅用于 Logo 与 Hero / CTA 光晕，功能界面不滥用',
    iconSystem: 'Lucide 线性图标；stroke-width 1.5；currentColor 着色；20 / 16 / 14 三档尺寸',
    motif: '拓扑装饰元件（节点 + 依赖边 SVG），用于 Hero 与空态',
  },
  tokens: {
    color: {
      brand: {
        '50': '#f5f3ff',
        '100': '#ede9fe',
        '200': '#ddd6fe',
        '300': '#c4b5fd',
        '400': '#a78bfa',
        '500': '#8b5cf6',
        '600': '#7c3aed',
        '700': '#6d28d9',
        '800': '#5b21b6',
        '900': '#4c1d95',
      },
      accent: {
        '50': '#ecfeff',
        '100': '#cffafe',
        '200': '#a5f3fc',
        '300': '#67e8f9',
        '400': '#22d3ee',
        '500': '#06b6d4',
        '600': '#0891b2',
        '700': '#0e7490',
        '800': '#155e75',
        '900': '#164e63',
      },
      neutral: NEUTRAL,
      semantic: SEMANTIC,
      surface: {
        'surface-bg': '#eef1f8',
        'surface-1': '#ffffff',
        'surface-2': '#f8fafc',
        'surface-3': '#f1f5f9',
      },
      text: {
        'fg-1': '#0f172a',
        'fg-2': '#334155',
        'fg-3': '#64748b',
        'fg-4': '#94a3b8',
        link: '#0891b2',
      },
    },
    typography: {
      fontSans: "'Inter', system-ui, -apple-system, 'Segoe UI', Roboto, 'PingFang SC', 'Microsoft YaHei', sans-serif",
      fontMono: "'JetBrains Mono', 'SFMono-Regular', Menlo, Consolas, monospace",
      scale: {
        display: '64px',
        h1: '40px',
        h2: '28px',
        h3: '20px',
        h4: '16px',
        'body-lg': '18px',
        body: '15px',
        small: '13px',
        micro: '12px',
      },
      weights: { regular: '400', medium: '500', semibold: '600', bold: '700' },
      lineHeights: { display: '1.05', tight: '1.2', normal: '1.5', relaxed: '1.65' },
    },
    spacing: {
      'space-1': '4px',
      'space-2': '8px',
      'space-3': '12px',
      'space-4': '16px',
      'space-5': '24px',
      'space-6': '32px',
      'space-7': '48px',
      'space-8': '64px',
      'space-9': '96px',
    },
    radius: { xs: '4px', sm: '6px', md: '8px', lg: '12px', xl: '16px', '2xl': '24px', pill: '999px' },
    elevation: {
      'shadow-sm': '0 1px 2px rgba(15, 23, 42, 0.06), 0 1px 3px rgba(15, 23, 42, 0.08)',
      'shadow-md': '0 4px 6px rgba(15, 23, 42, 0.05), 0 10px 15px rgba(15, 23, 42, 0.08)',
      'shadow-lg': '0 10px 25px rgba(15, 23, 42, 0.1), 0 20px 40px rgba(15, 23, 42, 0.12)',
    },
    border: {
      'border-1': 'rgba(15, 23, 42, 0.08)',
      'border-2': 'rgba(15, 23, 42, 0.14)',
      'border-3': 'rgba(15, 23, 42, 0.24)',
    },
    motion: {
      easing: { 'ease-out-expo': 'cubic-bezier(0.16, 1, 0.3, 1)', 'ease-standard': 'cubic-bezier(0.4, 0, 0.2, 1)' },
      duration: { fast: '150ms', base: '250ms', slow: '400ms' },
    },
  },
  components: [
    { name: 'Buttons', purpose: '承载页面主动作', variants: 'Primary / Secondary / Ghost / Danger', anatomy: ['文字', '可选图标', '填充或描边背景'] },
    { name: 'Button States', purpose: '保证交互反馈完整', variants: 'default / hover / active / focus / disabled', anatomy: ['背景色阶', '阴影层级', 'focus 描边'] },
    { name: 'Inputs', purpose: '数据录入', variants: 'default / focus / invalid', anatomy: ['标签', '输入框', '提示与错误文案'] },
    { name: 'Status Badges', purpose: '表达资源状态', variants: '健康 / 降级 / 故障 / 部署中', anatomy: ['状态点', '图标', '文字', '语义底色'] },
    { name: 'Metric Cards', purpose: '呈现关键指标', variants: 'up / down / flat 趋势', anatomy: ['指标名', '数值（tabular-nums）', '单位', '趋势增量'] },
    { name: 'Code Block', purpose: '展示配置与代码', variants: 'YAML / JSON / 代码', anatomy: ['文件名栏', '语法高亮正文'] },
    { name: 'Nav Item', purpose: '侧栏导航', variants: 'active / rest', anatomy: ['图标', '文字', '激活态指示条'] },
    { name: 'Table Row', purpose: '列表化资源', variants: '默认 / hover', anatomy: ['名称', '状态点', 'P99', '吞吐'] },
    { name: 'Console UI Kit', purpose: '控制台界面套件', variants: 'Overview / Services / Topology / Config / Deployments', anatomy: ['侧栏导航', '内容区', '指标栅格'] },
  ],
  applications: [
    {
      name: 'Console UI Kit',
      desc: '控制中枢：把设计系统落到真实的控制台界面',
      sections: ['Overview 总览', 'Services 服务列表', 'Topology 依赖拓扑', 'Config 参数配置', 'Deployments 发布流水'],
    },
    {
      name: 'Marketing Site',
      desc: '官网：品牌叙事与转化',
      sections: ['Hero 主视觉 + 主 CTA', 'Features 能力矩阵', 'Architecture 分层架构', 'Code 开发者友好', 'CTA 转化区'],
    },
  ],
  selfCheck: [
    { item: '状态完整性', detail: '每个组件覆盖 default / hover / active / focus / disabled，不只设计 happy path' },
    { item: '对比度可达性', detail: '正文 fg-1 对 surface-1 ≥ 4.5:1；辅助文字仅用于非关键信息' },
    { item: '间距节奏', detail: '全部间距取自 8pt 刻度，不出现游离值' },
    { item: '焦点可见', detail: '可交互元素具备 :focus-visible 描边' },
    { item: '语义色克制', detail: 'success/warning/danger/info 仅承载状态语义，不用于装饰' },
    { item: '动效一致', detail: '统一缓动与三档时长，不逐元素自定义' },
    { item: '中性导轨稳定', detail: '中性色与语义色不随品牌色相漂移，换肤后观感仍干净' },
    { item: '渐变使用克制', detail: '品牌渐变只用于 Logo / Hero / CTA，功能界面不用' },
  ],
}

/** 可用的参考示例清单（供界面下拉加载）。 */
export const PRESETS: DesignSystem[] = [STARDUST]
