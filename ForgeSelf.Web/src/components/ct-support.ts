import type { Page } from '@playwright/test'

/**
 * CT 用例共享助手（只给 `*.ct.spec.ts` 用；vitest 不会收集它，生产构建也不会打包）。
 */
declare global {
  interface Window {
    mountStory: (id: string, props?: Record<string, unknown>) => void
    setStoryProps: (patch: Record<string, unknown>) => void
    unmountStory: () => void
  }
}

/**
 * 打开一个故事并等到「首屏稳定」，再允许截图。
 *
 * 三个稳定化动作都来自实测踩坑：
 *   1. `reducedMotion: 'reduce'`：组件挂载动画（如 ForgeCard 的 160ms 上浮）会制造半透明中间帧，
 *      不开这个开关截图会随机截到「淡入一半」的像素。
 *   2. `chrome=0`：人类浏览时深链也会渲染侧栏，CT 必须关掉，否则左侧留白会把基线整体推移；
 *      下面再兜底移除 `#gallery-nav`，防将来导航回归后拦截指针事件
 *      （Cube.Vue 踩过：侧栏空壳仍占位，点击全部超时）。
 *   3. 固定等待 150ms：等首屏渲染后的微任务/字体度量落地（量高度做折叠的组件依赖 nextTick）。
 */
export async function openStory(page: Page, id: string, waitFor?: string): Promise<void> {
  await page.emulateMedia({ reducedMotion: 'reduce' })
  await page.goto(`/?story=${encodeURIComponent(id)}&chrome=0`)
  await page.evaluate(() => document.getElementById('gallery-nav')?.remove())
  if (waitFor) await page.waitForSelector(waitFor)
  await page.waitForTimeout(150)
}
