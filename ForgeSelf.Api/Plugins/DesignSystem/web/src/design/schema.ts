/**
 * 设计系统的通用数据模型。
 *
 * ⚠️ 本文件只定义「一份设计系统由什么构成」这一**结构**（骨架），
 *    不包含任何具体品牌取色——具体品牌一律是 preset 或运行时生成物。
 *    参考示例（Stardust）见 presets.ts。
 *
 * 结构对齐「一份设计系统该覆盖什么」的参考答案，共五类：
 * 品牌 / 颜色 / 类型 / 间距与质感 / 组件，外加应用示例与设计自检。
 */

/** 色阶：数值档 → 十六进制色值（如 { '50': '#f5f3ff', … }）。 */
export type Scale = Record<string, string>

/** 品牌与身份。 */
export interface BrandSpec {
  /** 品牌/产品名。 */
  name: string
  /** Logo 概念说明（构成、隐喻、用法）。 */
  logoConcept: string
  /** 品牌渐变（用于 Logo / Hero 光晕）。 */
  gradient: string
  /** 图标系统的规范（风格、描边、尺寸）。 */
  iconSystem: string
  /** 装饰母题（用于空态 / Hero / 背景）。 */
  motif: string
}

/** 一个组件的规格。 */
export interface ComponentSpec {
  name: string
  purpose: string
  variants?: string
  anatomy: string[]
}

/** 应用示例（设计系统落到真实界面的形态）。 */
export interface ApplicationSpec {
  name: string
  desc: string
  /** 该应用由哪些区块构成。 */
  sections: string[]
}

/** 设计自检条目。 */
export interface CheckItem {
  item: string
  detail: string
}

/** 一份完整的设计系统。 */
export interface DesignSystem {
  meta: {
    /** 设计系统名（通常由品牌名派生）。 */
    name: string
    /** 触发本次生成的需求描述。 */
    brief: string
    /** 生成来源：preset 引用 / 由需求生成。 */
    source: 'preset' | 'generated'
    /** 生成参数（可复现的关键种子）。 */
    seed: { hue: number; accentHue: number; industry: string; fontKey: string }
    generatedAt: string
  }
  brand: BrandSpec
  tokens: {
    color: {
      brand: Scale
      accent: Scale
      neutral: Scale
      semantic: Record<string, string>
      surface: Record<string, string>
      text: Record<string, string>
    }
    typography: {
      fontSans: string
      fontMono: string
      /** 字阶：display / h1–h4 / body-lg / body / small / micro。 */
      scale: Record<string, string>
      weights: Record<string, string>
      lineHeights: Record<string, string>
    }
    spacing: Scale
    radius: Scale
    elevation: Record<string, string>
    border: Record<string, string>
    motion: { easing: Record<string, string>; duration: Record<string, string> }
  }
  components: ComponentSpec[]
  applications: ApplicationSpec[]
  selfCheck: CheckItem[]
}
