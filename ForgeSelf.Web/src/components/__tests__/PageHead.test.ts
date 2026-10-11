import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import PageHead from '../PageHead.vue'

const headSrc = readFileSync(resolve(process.cwd(), 'src/components/PageHead.vue'), 'utf-8')

describe('PageHead — 渲染契约', () => {
  it('渲染 eyebrow 与 title', () => {
    const wrapper = mount(PageHead, {
      props: { eyebrow: 'System Monitor', title: '资源 · 进程 · 日志' },
    })
    expect(wrapper.find('.page-head__eyebrow').text()).toBe('System Monitor')
    expect(wrapper.find('.page-head__title').text()).toBe('资源 · 进程 · 日志')
  })

  it('无 eyebrow 时不渲染 eyebrow 块', () => {
    const wrapper = mount(PageHead, { props: { title: '仅标题' } })
    expect(wrapper.find('.page-head__eyebrow').exists()).toBe(false)
  })

  it('actions slot 渲染到右侧操作区', () => {
    const wrapper = mount(PageHead, {
      props: { title: 't' },
      slots: { actions: '<button>操作</button>' },
    })
    expect(wrapper.find('.page-head__actions button').text()).toBe('操作')
  })
})

describe('PageHead — 动效刻度（DESIGN.md Motion）', () => {
  it('title/eyebrow 进入动效引用 --dur-base（220ms ease-out）', () => {
    expect(headSrc).toMatch(/\.page-head__eyebrow[^}]*animation:[^;]*var\(--dur-base\)/)
    expect(headSrc).toMatch(/\.page-head__title[^}]*animation:[^;]*var\(--dur-base\)/)
  })

  it('缓动引用 --ease-out，标题用 --dur-instant 微延迟分层，不硬编码秒数', () => {
    expect(headSrc).toMatch(/var\(--ease-out\)/)
    expect(headSrc).toMatch(/\.page-head__title[^}]*animation:[^;]*var\(--dur-instant\)/)
    expect(headSrc).not.toMatch(/0\.\d{1,2}s/)
  })
})
