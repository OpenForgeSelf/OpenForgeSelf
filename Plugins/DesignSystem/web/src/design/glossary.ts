/**
 * 术语词典（v3：面向"不懂设计的外行"用户）。
 *
 * 规则（FR5 / AC9）：
 * - 新模式（开始 / 展厅 / 交付与接入）里所有面向用户的专业名词一律经 `term(key)`；
 * - 默认大白话（`proTerms=false`），「专业术语」开关一键切回，状态持久化到 `ds.pro-terms`；
 * - **工作台文案不动**（不经过本词典）；
 * - `term()` 对未知 key 原样返回（不炸界面），但 `glossary.test.ts` 的守卫保证
 *   源码里每个 term 调用的字面量 key 都在本词典里 —— 漏词条是红不是静默。
 */
import { ref } from 'vue'

export interface GlossaryEntry {
  /** 大白话（默认显示） */
  plain: string
  /** 专业术语（开关打开后显示） */
  pro: string
}

export const GLOSSARY: Record<string, GlossaryEntry> = {
  'design system': { plain: '一套界面风格', pro: '设计系统' },
  token: { plain: '设计变量', pro: '令牌 Token' },
  theme: { plain: '风格档', pro: '主题 Theme' },
  preset: { plain: '现成风格', pro: '风格预设' },
  generate: { plain: '一键生成', pro: '生成 Generate' },
  export: { plain: '导出文件', pro: '导出 Export' },
  audit: { plain: '质量检查', pro: '审计 Audit' },
  component: { plain: '界面组件', pro: '组件' },
  variant: { plain: '组件变体', pro: '变体' },
  radius: { plain: '圆润度', pro: '圆角半径' },
  space: { plain: '疏密', pro: '间距刻度' },
  density: { plain: '疏密档', pro: '密度档' },
  font: { plain: '字体', pro: '字体族' },
  device: { plain: '设备', pro: '设备框' },
  contrast: { plain: '文字清晰度', pro: '对比度' },
  'quick-create': { plain: '快速创建', pro: '快速创建（免配置）' },
  wardrobe: { plain: '衣服架', pro: '衣柜' },
  outfit: { plain: '一套衣服', pro: '试穿搭配' },
  stage: { plain: '展示台', pro: '舞台' },
  tune: { plain: '微调', pro: '微调 Tune' },
  preview: { plain: '试穿预览', pro: '预览 Preview' },
  seed: { plain: '随机种子', pro: '种子 Seed' },
  'agent-rules': { plain: '给 AI 的使用规则', pro: 'Agent 规则' },
  brief: { plain: '设计说明书', pro: 'Brief 说明书' },
  'style axes': { plain: '更多风格选项', pro: '风格轴 Style Axes' },
  'style axis hint': { plain: '这几项改的是轮廓本身：阴影怎么打、线多粗、字用哪族、角多圆、灰偏冷还是偏暖。', pro: '七条风格轴贯穿阴影公式、描边宽、中性色阶、字体栈、圆角映射与强调色偏移；缺省取值与 M2 之前逐字节一致。' },
}

/** 开关状态（响应式，默认大白话）。读 localStorage 失败/无值 = false */
export const proTerms = ref(loadProTerms())

function loadProTerms(): boolean {
  try {
    return localStorage.getItem('ds.pro-terms') === '1'
  } catch {
    return false
  }
}

/** 切换专业术语开关并持久化；写失败静默 */
export function toggleProTerms(): void {
  proTerms.value = !proTerms.value
  try {
    localStorage.setItem('ds.pro-terms', proTerms.value ? '1' : '0')
  } catch {
    /* 静默 */
  }
}

/** 取词条：专业开关开 → pro，否则 plain；未知 key 原样返回（守卫负责揪漏词条） */
export function term(key: string): string {
  const entry = GLOSSARY[key]
  return entry ? (proTerms.value ? entry.pro : entry.plain) : key
}
