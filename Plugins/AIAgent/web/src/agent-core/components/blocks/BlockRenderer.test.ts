import { describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { nextTick } from 'vue'
import BlockRenderer from './BlockRenderer.vue'
import { normalizeMessage } from '../../utils/schema'

/**
 * 富内容块渲染测试（组件源 REQUIREMENTS §6.3 FR-301~317 / §6.4 FR-401~412）。
 *
 * 背景：迁移进 OpenForgeSelf 后，AIAgent 链路只走纯文本 content，
 * blocks（图表 / 表格 / KPI / HTML 预览 / 审批 / 分组…）在本仓**没有任何生产者**，
 * MessageItem.test.ts 又把 BlockRenderer stub 掉了 —— 富内容渲染层零覆盖。
 * 本文件锁各类块的用户可见行为；真实链路（LLM 出 markdown）由 e2e 覆盖。
 *
 * jsdom 边界：不跑布局 → 不断言尺寸 / 溢出，只断言 DOM 结构、属性与交互门禁。
 */

const config = { theme: 'page' } as never

function mountBlock(block: Record<string, unknown>, cfg: Record<string, unknown> = config) {
  return mount(BlockRenderer, {
    props: { block, config: cfg as never },
    global: { stubs: { 'transition': false } },
  })
}

describe('BlockRenderer 富内容块（FR-301~317）', () => {
  /* ---------- FR-306 图表：零依赖 SVG + 可读名（FR-1506） ---------- */
  it('chart 渲染内置 SVG，带 role="img" 与 aria-label 数据摘要', () => {
    const w = mountBlock({
      type: 'chart',
      title: '月度活跃',
      option: { type: 'bar', data: [{ name: '一月', value: 12 }, { name: '二月', value: 30 }] },
    })
    const svg = w.find('.aiw-chart__svg')
    expect(svg.exists()).toBe(true)
    expect(svg.attributes('role')).toBe('img')
    // 可读名必须含图表类型与数据点，读屏才「看得见」图表（FR-1506）
    const label = svg.attributes('aria-label') || ''
    expect(label).toContain('柱状图')
    expect(label).toContain('一月')
    expect(label).toContain('二月')
    expect(w.find('.aiw-chart__title').text()).toBe('月度活跃')
  })

  it('chart 顶层 chartType+categories+series 写法同样出图（schema 兼容双写法）', () => {
    /* 走真实数据路径：后端按 REQUIREMENTS §8.2 发顶层写法，先过 normalizeMessage
       （push/load/hydrate 同路）再进渲染器——渲染器本身只认 option。 */
    const { message } = normalizeMessage({
      id: 'm1',
      role: 'assistant',
      content: '',
      blocks: [
        { type: 'chart', chartType: 'line', categories: ['周一', '周二', '周三'], series: [5, 9, 3] },
      ],
    })
    const w = mountBlock((message.blocks as Record<string, unknown>[])[0])
    const svg = w.find('.aiw-chart__svg')
    expect(svg.exists()).toBe(true)
    expect(svg.attributes('aria-label')).toContain('折线图')
  })

  it('chart 渲染抛错时就地降级为可见提示，不白屏（FR-316 的图表侧）', () => {
    // 喂一个让 normalize 之后 data 为空但类型非法的组合，走降级分支
    const w = mountBlock({ type: 'chart', option: { type: 'bar', data: [] } })
    expect(w.find('.aiw-chart__empty').text()).toContain('无数据')
  })

  /* ---------- FR-309 表格 ---------- */
  it('table 渲染表头与所有行', () => {
    const w = mountBlock({
      type: 'table',
      columns: ['城市', '人口'],
      rows: [['东莞', '1000 万'], ['深圳', '1700 万']],
    })
    const th = w.findAll('.aiw-table th')
    expect(th.map((n) => n.text())).toEqual(['城市', '人口'])
    const rows = w.findAll('.aiw-table tbody tr')
    expect(rows).toHaveLength(2)
    expect(rows[0].findAll('td').map((n) => n.text())).toEqual(['东莞', '1000 万'])
  })

  /* ---------- FR-308 KPI：delta 正负着色 ---------- */
  it('kpi 渲染指标与 delta，负值为 is-down、正值为 is-up', () => {
    const w = mountBlock({
      type: 'kpi',
      items: [
        { label: '收入', value: '¥1.2 万', delta: '+8%' },
        { label: '成本', value: '¥3 千', delta: '-2%' },
      ],
    })
    const items = w.findAll('.aiw-kpi__item')
    expect(items).toHaveLength(2)
    expect(items[0].find('.aiw-kpi__delta').classes()).toContain('is-up')
    expect(items[1].find('.aiw-kpi__delta').classes()).toContain('is-down')
  })

  /* ---------- FR-311 HTML 预览：沙箱无同源 ---------- */
  it('html 块 iframe 只授 allow-scripts、绝不给 allow-same-origin（NFR-05）', () => {
    const w = mountBlock({ type: 'html', html: '<b>hi</b>', height: 200 })
    const frame = w.get('.aiw-html__frame')
    expect(frame.attributes('sandbox')).toBe('allow-scripts')
    expect(frame.attributes('sandbox')).not.toContain('allow-same-origin')
    expect((frame.attributes('srcdoc') || '')).toContain('<b>hi</b>')
    // 折叠按钮须带 aria-expanded（FR-1507）
    expect(w.get('.aiw-html__btn').attributes('aria-expanded')).toBe('true')
  })

  /* ---------- FR-312 代码块 ---------- */
  it('code 块显示语言标签与原文，带复制按钮', () => {
    const w = mountBlock({ type: 'code', lang: 'csharp', code: 'var x = 1;' })
    expect(w.get('.aiw-codeblk__bar').text()).toContain('csharp')
    expect(w.get('.aiw-code code').text()).toBe('var x = 1;')
    expect(w.find('.aiw-codeblk__bar button').text()).toBe('复制')
  })

  /* ---------- FR-304 计划清单 ---------- */
  it('plan 渲染全部步骤', () => {
    const w = mountBlock({
      type: 'plan',
      title: '执行计划',
      steps: [
        { title: '读文件', status: 'completed' },
        { title: '改代码', status: 'running' },
      ],
    })
    const items = w.findAll('.aiw-plan__list li')
    expect(items).toHaveLength(2)
    expect(items[0].classes()).toContain('is-completed')
    expect(items[1].classes()).toContain('is-running')
  })

  /* ---------- FR-301 思考过程 ---------- */
  it('thinking 完成时显示耗时，未完成显示思考中', () => {
    const done = mountBlock({ type: 'thinking', label: '推理', text: '想', done: true, durationMs: 1500 })
    expect(done.find('.aiw-think--done').exists()).toBe(true)
    expect(done.find('.aiw-think__meta').text()).toBe('思考 1.5 秒')
    const live = mountBlock({ type: 'thinking', text: '想' })
    expect(live.find('.aiw-think__meta').text()).toBe('思考中…')
  })

  /* ---------- FR-302 工具调用：状态与写操作标记 ---------- */
  /* FR-302 规约已变更（2026-10-26 展示形态对齐）：
     折叠行 = 「图标 · 行为 · 工具名」，成功态**不再写「完成」** ——
     一次调用成功了就没有可读信息，写出来只是噪声；
     执行中 / 失败仍必须不展开就看得见（下两条用例守住这一点）。 */
  it('tool 折叠行渲染名称与写操作标签；成功态不写状态词', () => {
    const w = mountBlock({
      type: 'tool',
      name: 'delete_file',
      status: 'ok',
      durationMs: 320,
      dangerous: true,
      args: { path: '/tmp/a' },
      result: 'done',
    })
    expect(w.find('.aiw-tool__name').text()).toBe('delete_file')
    expect(w.find('.aiw-tool__label').text()).toBe('工具调用')
    expect(w.find('.aiw-tool__status').exists()).toBe(false)
    expect(w.find('.aiw-tool__dur').text()).toBe('320ms')
    expect(w.find('.aiw-tool__danger').text()).toBe('写操作')
    expect(w.find('.aiw-tool__pre').text()).toContain('/tmp/a')
    // 展开后的分区标题对齐参考形态：输入 / 输出
    const keys = w.findAll('.aiw-tool__k')
    expect(keys[0].text()).toBe('输入')
    expect(keys[keys.length - 1].text()).toBe('输出')
  })

  it('tool 执行中与失败必须在折叠态就可见', () => {
    expect(mountBlock({ type: 'tool', name: 'x', status: 'running' }).find('.aiw-tool__status').text()).toBe('执行中')
    expect(mountBlock({ type: 'tool', name: 'x', status: 'error' }).find('.aiw-tool__status').text()).toBe('失败')
  })

  it('tool 行为从显式字段或入参推导；推不出就留空', () => {
    expect(
      mountBlock({ type: 'tool', name: 'plugin_manager', action: 'install_bundle' })
        .find('.aiw-tool__action')
        .text()
    ).toBe('install_bundle')
    // 入参是 JSON 字符串也要能认
    expect(
      mountBlock({ type: 'tool', name: 'fs', args: '{"op":"write_file","path":"a"}' })
        .find('.aiw-tool__action')
        .text()
    ).toBe('write_file')
    // 推不出就不生成空段，不瞎猜一个词
    expect(mountBlock({ type: 'tool', name: 'fs', args: { path: 'a' } }).find('.aiw-tool__action').exists()).toBe(false)
  })

  it('思考折叠行给出单行摘要，超长截断', () => {
    const short = mountBlock({ type: 'thinking', text: '先看结构', done: true })
    expect(short.find('.aiw-think__peek').text()).toBe('先看结构')
    const long = mountBlock({ type: 'thinking', text: 'x'.repeat(200), done: true })
    const peek = long.find('.aiw-think__peek').text()
    expect(peek.endsWith('…')).toBe(true)
    expect(peek.length).toBeLessThan(100)
  })

  /* ---------- 左列对齐：图标槽（2026-10-26 用户指「悬浮多出箭头让左边不齐」） ----------
     结构契约：三种可折叠行的 button 首个元素都是 .aiw-ico，槽内**同时**有类型图标（glyph）
     与折叠箭头（chev），二者同位叠放、只切 opacity ⇒ 悬浮不产生任何横向位移。
     jsdom 不跑布局，所以「x 相等 / 不漂移」只能由宿主 CT 的几何断言守（AC2）。 */
  it('三种可折叠行的行首都以图标槽开头，槽内同时有类型图标与折叠箭头', () => {
    const rows: Array<[Record<string, unknown>, string]> = [
      [{ type: 'tool', name: 'x' }, '.aiw-tool__head'],
      [{ type: 'thinking', text: '想', done: true }, '.aiw-think__head'],
      [{ type: 'group', title: '执行过程', blocks: [] }, '.aiw-grp__head'],
    ]
    for (const [block, headSel] of rows) {
      const head = mountBlock(block).find(headSel)
      const slot = head.find('.aiw-ico')
      expect(slot.exists(), `${headSel} 缺少图标槽 .aiw-ico`).toBe(true)
      // 槽必须是行首第一个元素：排在后面就等于箭头还在抢位置
      expect(head.element.firstElementChild).toBe(slot.element)
      expect(slot.find('.aiw-ico__glyph').exists()).toBe(true)
      // 箭头只能有一个，且在槽内 —— 整行有第二个就是「悬浮多出来的那一个」
      expect(head.findAll('.aiw-ico__chev').length).toBe(1)
      expect(slot.find('.aiw-ico__chev').exists()).toBe(true)
    }
  })

  it('展开态由图标槽上的 is-open 表达（箭头常驻并旋转，图标让位）', async () => {
    // 块默认展开（open !== false），这里显式从折叠态起手，才能断言两个方向的切换
    const w = mountBlock({ type: 'tool', name: 'x', args: { a: 1 }, collapsed: true })
    const slot = () => w.get('.aiw-ico')
    expect(slot().classes()).not.toContain('is-open')
    await w.get('.aiw-tool__head').trigger('click')
    expect(slot().classes()).toContain('is-open')
    await w.get('.aiw-tool__head').trigger('click')
    expect(slot().classes()).not.toContain('is-open')
  })

  /* ---------- FR-402~406 审批：风险分级 + 确认词门禁 ---------- */
  it('approval 未输确认词时批准按钮禁用；严格全等才放行（US-12-AC5）', async () => {
    const w = mountBlock({
      type: 'approval',
      id: 'ap1',
      risk: 'critical',
      title: '删除生产数据库',
      text: '将删除 **prod** 库',
      impact: '不可恢复',
      action: { name: 'drop_db', args: { db: 'prod' } },
      requireConfirmText: 'DELETE',
      timeoutMs: 0,
      status: 'pending',
    })
    expect(w.find('.aiw-appr--critical').exists()).toBe(true)
    expect(w.find('.aiw-appr__risk').text()).toBe('极高风险')

    const approve = w.get('.aiw-appr__btn--ok')
    expect(approve.attributes('disabled')).toBeDefined()
    expect(approve.attributes('aria-disabled')).toBe('true')

    // 大小写不符 / 带空格都不放行（不 trim）
    const input = w.get('.aiw-appr__confirm input')
    await input.setValue('delete')
    expect(w.get('.aiw-appr__btn--ok').attributes('disabled')).toBeDefined()
    await input.setValue(' DELETE ')
    expect(w.get('.aiw-appr__btn--ok').attributes('disabled')).toBeDefined()
    await input.setValue('DELETE')
    expect(w.get('.aiw-appr__btn--ok').attributes('disabled')).toBeUndefined()

    await w.get('.aiw-appr__btn--ok').trigger('click')
    const ev = w.emitted('resolve')
    expect(ev).toBeTruthy()
    expect(ev![0]).toEqual([{ id: 'ap1', decision: 'approve' }])
  })

  it('approval 已裁决态只展示结果，无按钮', () => {
    const w = mountBlock({
      type: 'approval',
      id: 'ap2',
      risk: 'low',
      status: 'approved',
      resolvedBy: '笑笑',
      resolvedAt: 1760000000000,
    })
    expect(w.find('.aiw-appr__btns').exists()).toBe(false)
    // bar 里只放终态文案；裁决人与时间在详情行（.aiw-appr__resolved）
    expect(w.find('.aiw-appr__done').text()).toContain('已批准')
    expect(w.find('.aiw-appr__resolved').text()).toContain('笑笑')
  })

  /* ---------- FR-410~411 步骤分组：汇总 + 递归 ---------- */
  it('group 自动汇总嵌套块并递归渲染子块', () => {
    const w = mountBlock({
      type: 'group',
      title: '执行过程',
      blocks: [
        { type: 'tool', name: 'read_file', status: 'ok' },
        { type: 'tool', name: 'write_file', status: 'ok', durationMs: 500 },
        { type: 'group', blocks: [{ type: 'text', text: '结论' }] },
      ],
    })
    // 汇总口径：顶层步数（3 步），嵌套块按类型计数（2 工具 + 1 步骤 + 1 结论）
    const summary = w.find('.aiw-grp__summary').text()
    expect(summary).toContain('3 步')
    expect(summary).toContain('2 个工具调用')
    expect(summary).toContain('1 个结论')
    expect(summary).toContain('0.5s')
    // 递归：内层 text 块走 markdown 渲染出来
    expect(w.findAll('.aiw-grp').length).toBe(2)
    expect(w.text()).toContain('结论')
  })

  /* ---------- FR-317 未知类型降级 ---------- */
  it('未知块类型渲染可见提示，不留白', () => {
    const w = mountBlock({ type: 'video', unknownType: 'video', text: '原文' })
    expect(w.find('.aiw-unknown').exists()).toBe(true)
    expect(w.find('.aiw-unknown code').text()).toBe('video')
    expect(w.text()).toContain('原文')
  })

  /* ---------- FR-305 text 块走 markdown 管线 ---------- */
  it('text 块按 FR-207 渲染 markdown（先转义再白名单替换）', () => {
    const w = mountBlock({ type: 'text', text: '**粗体** 与 `代码` 与 <script>alert(1)</script>' })
    const html = w.get('.aiw-md').html()
    expect(html).toContain('<strong>粗体</strong>')
    expect(html).toContain('<code class="aiw-inline-code">代码</code>')
    // XSS 必须被转义，不能出现可执行脚本标签
    expect(html).not.toContain('<script>alert(1)</script>')
    expect(html).toContain('&lt;script&gt;')
  })

  /* ---------- FR-903 权限门禁：数据不进 DOM ---------- */
  it('无权限时图表/表格数据不渲染，只出占位（FR-903）', () => {
    const w = mountBlock(
      {
        type: 'table',
        minRole: 'admin',
        columns: ['机密列'],
        rows: [['机密值']],
      },
      { theme: 'page', role: 'viewer' } as never
    )
    expect(w.find('.aiw-table').exists()).toBe(false)
    expect(w.find('.aiw-locked').exists()).toBe(true)
    expect(w.text()).not.toContain('机密值')
  })
})
