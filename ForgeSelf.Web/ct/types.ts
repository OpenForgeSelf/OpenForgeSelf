import type { Component, VNodeChild } from 'vue'

/**
 * 画廊故事（Component Story）
 *
 * 一条 story = 组件的一个**确定性渲染状态**：给组件传一组 props，看它渲染成什么样。
 * 画廊（ct/main.ts）与回归用例（*.ct.spec.ts）共用同一份 story，做到「所见即所测」。
 */
export interface ComponentStory {
  /** 全局唯一，形如 `ForgeCard/Default`（前半段是组件名，用于侧栏分组） */
  id: string
  /** 被测组件（SFC 或带 props 的壳组件） */
  component: Component
  /** 该变体固定的 props；禁止放时间/随机数/真实网络等非确定性数据 */
  props?: Record<string, unknown>
  /**
   * 该变体固定的插槽内容。
   * 卡片/页头这类「没有插槽就只剩个空壳」的组件，必须靠它才能拍到真实形态。
   */
  slots?: Record<string, () => VNodeChild>
}
