import type { ComponentStory } from './types'

/**
 * 故事聚合（画廊的唯一注册点）
 *
 * 为什么用「显式静态 import」而不是 import.meta.glob：
 * glob 在 HMR 活跃环境下会给模块附加查询串，模块图不稳定、偶发漏注册
 * （表现为 mountStory 报「未知 story」）。显式导入确定性最高；新增故事只需在这里加一行。
 *
 * 约定：story 文件与被测组件同目录，命名 `<组件名>.story.ts`。
 * 守卫 `scripts/check-component-conventions.mjs` 会机械校验「每个 story 文件都被本文件引用」。
 */
import { stories as forgeCardStories } from '../src/components/ForgeCard.story'
import { stories as pageHeadStories } from '../src/components/PageHead.story'
import { stories as emptyStateStories } from '../src/components/EmptyState.story'
// AIAgent 插件聊天助手：组件在插件包内，宿主薄壳在 src/components（跨包理由见该 story 注释）
import { stories as aiAgentMessageStories } from '../src/components/AIAgentMessage.story'

export const stories: ComponentStory[] = [
  ...forgeCardStories,
  ...pageHeadStories,
  ...emptyStateStories,
  ...aiAgentMessageStories,
]

export interface NavGroupConfig {
  /** 分组 key，必须与 story id 的 `/` 前缀一致（如 AIAgent/MessageText → AIAgent） */
  key: string
  /** 侧栏显示名（不带计数，计数由画廊自动追加） */
  title: string
}

export interface NavConfig {
  /** 侧栏标题 */
  title: string
  /** 分组显示名与排序；未在此列出的分组按 id 前缀自动追加在末尾 */
  groups: NavGroupConfig[]
  /** 故事总数超过该值时，分组默认收起 */
  collapseWhenMoreThan: number
}

/** 侧边栏导航配置（唯一配置点） */
export const navConfig: NavConfig = {
  title: '组件故事（点击预览）',
  groups: [
    { key: 'ForgeCard', title: 'ForgeCard · 玻璃卡片' },
    { key: 'PageHead', title: 'PageHead · 页头' },
    { key: 'EmptyState', title: 'EmptyState · 空状态' },
    { key: 'AIAgent', title: 'AIAgent · 聊天助手' },
  ],
  collapseWhenMoreThan: 25,
}
