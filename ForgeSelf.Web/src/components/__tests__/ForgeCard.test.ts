import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import ForgeCard from '../ForgeCard.vue'

const cardSrc = readFileSync(resolve(process.cwd(), 'src/components/ForgeCard.vue'), 'utf-8')
const themeSrc = readFileSync(resolve(process.cwd(), 'src/styles/themes/workshop-forge.css'), 'utf-8')

describe('ForgeCard — 渲染契约', () => {
  it('渲染默认 slot 内容', () => {
    const wrapper = mount(ForgeCard, { slots: { default: '<p>card body</p>' } })
    expect(wrapper.text()).toContain('card body')
  })

  it('interactive 为 true 时挂 .forge-card--interactive 类', () => {
    const wrapper = mount(ForgeCard, { props: { interactive: true } })
    expect(wrapper.find('.forge-card').classes()).toContain('forge-card--interactive')
  })

  it('padding prop 落到内联样式', () => {
    const wrapper = mount(ForgeCard, { props: { padding: 20 } })
    expect(wrapper.find('.forge-card').attributes('style')).toContain('padding: 20px')
  })
})

describe('ForgeCard — 动效刻度（DESIGN.md Motion）', () => {
  it('交互 hover/focus 反馈引用 --dur-base（220ms），不硬编码秒数', () => {
    expect(cardSrc).toMatch(/transition:[^;]*var\(--dur-base\)/)
    expect(cardSrc).not.toMatch(/0\.\d{1,2}s/)
  })

  it('进入挂载动画引用 --dur-fast（160ms）', () => {
    expect(cardSrc).toMatch(/animation:[^;]*var\(--dur-fast\)/)
  })

  it('缓动引用 --ease-out（非 --ease-forge 回弹，本组件不滥用锤落回弹）', () => {
    expect(cardSrc).toMatch(/var\(--ease-out\)/)
  })

  it('主题 token 刻度与设计稿一致（160/220/360ms + 两条缓动曲线）', () => {
    expect(themeSrc).toMatch(/--dur-fast:\s*160ms/)
    expect(themeSrc).toMatch(/--dur-base:\s*220ms/)
    expect(themeSrc).toMatch(/--dur-slow:\s*360ms/)
    expect(themeSrc).toMatch(/--ease-out:\s*cubic-bezier\(0\.22,\s*1,\s*0\.36,\s*1\)/)
    expect(themeSrc).toMatch(/--ease-forge:\s*cubic-bezier\(0\.34,\s*1\.56,\s*0\.64,\s*1\)/)
  })
})
