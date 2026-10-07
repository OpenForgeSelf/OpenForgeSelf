import { test, expect, type Page } from '@playwright/test'
import { mkdirSync, readFileSync, existsSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { getRealApiKey, injectRealApiKey } from '../../helpers/real-auth'
import { backendUrl } from '../../helpers/e2e-env'

/**
 * todo-tracker 插件层 e2e（PILOT-054；对应 docs/ai/pilot/2026-10-07-todo-agent-dispatch/02-spec.md
 * AC-14/16/17 与 04-task.md 的 Allowed 范围）。
 *
 * 零 mock：走 globalSetup 拉起的真实宿主 + 真实前端 dev；地址一律取自 helpers/e2e-env（禁止硬编码端口）。
 * 覆盖四组：
 *  A 远程加载冒烟 —— `/todo` 已由插件自带 web/ 接管（不再是宿主内置页），入口 JS 200 且 MIME 为 JS，
 *    标题旁版本徽标与 plugin.json 一致，无组件/导出解析类报错；
 *  B 下发主链路 —— 建任务 → 补四栏 → 用 **Git-Bash 写法**（`/c/...`）关联项目（断言后端归一成 Windows 根）
 *    → 指定真实工件目录导入（含覆盖二次确认）→ 生成提示词（含按 taskKey 的回报契约、无真实 token）
 *    → 补记执行记录 → 阶段流转 → 完成；
 *  C 交互安全 —— 删除的「取消 / 确认」两条路径（取消必须一个请求都不发、后端状态未变）；
 *  D 鉴权与兼容 —— 无 token 打 api/todos 必须 401（铁律 17 的证据）；旧建单形状（Home 在用）仍可建。
 *
 * 选择器口径：块级定位只用唯一文本（'项目路径'/'从工件导入'），按钮用唯一可访问名；
 * 「下发」二字会同时命中「下发对象」标签所在块，因此下发区一律走 `生成提示词`/`交给 AgentHub 执行` 按钮定位。
 *
 * 数据安全：临时项目目录**只创建不删除**（plugin-development 铁律 10）；本用例只删自己建的任务。
 */

/**
 * 仓库根：用**仓库独有文件**（`ForgeSelf.slnx`）向上锚定，不靠相对层数猜。
 * 实测教训：这里曾写 `'../../../'`（少一层），拿到的是 `ForgeSelf.Web` —— 它同样是"真实存在的目录"，
 * 于是"归一后等于 REPO_ROOT"的断言两边同源、自洽地假绿，直到工件清单接口回「该项目没有 docs\ai\pilot 目录」才露馅。
 */
const REPO_ROOT = (() => {
  let dir = path.resolve(fileURLToPath(new URL('../../../../', import.meta.url)))
  for (let i = 0; i < 8 && !existsSync(path.join(dir, 'ForgeSelf.slnx')); i++) dir = path.dirname(dir)
  if (!existsSync(path.join(dir, 'ForgeSelf.slnx'))) {
    throw new Error(`找不到仓库根（锚点文件 ForgeSelf.slnx），最后停在 ${dir}`)
  }
  return dir
})()

/** 用户真实写法之一：Git-Bash 的 `/c/...`（用户输入1 点名的格式）。 */
const GITBASH_ROOT = (() => {
  const m = /^([A-Za-z]):[\\/](.*)$/.exec(REPO_ROOT)
  return m ? `/${m[1].toLowerCase()}/${m[2].replace(/\\/g, '/')}` : REPO_ROOT.replace(/\\/g, '/')
})()

const MANIFEST = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../../../Plugins/TodoTracker/plugin.json', import.meta.url)), 'utf-8'),
) as { Id: string; Version: string; frontend: { route: string; entry?: string } }

const ROUTE = MANIFEST.frontend.route            // '/todo'
const VERSION = MANIFEST.Version                 // '1.1.0'
const ENTRY = MANIFEST.frontend.entry ?? ''      // 'web/dist/index.js'

const BACKEND = backendUrl()
const API = `${BACKEND}/api/todos`
const AUTH = { Authorization: `Bearer ${getRealApiKey()}`, 'Content-Type': 'application/json' }

const SHOTS = path.resolve(fileURLToPath(new URL('../../../screenshots/e2e/todo-tracker', import.meta.url)))

/** 本任务的工件目录：真实存在，且带 01-04 核心件，能喂给导入链路。 */
const ARTIFACT_DIR = '2026-10-07-todo-agent-dispatch'

/** 会让插件界面白屏/半屏的报错形态（沿用 ai-agent/agent-hub 已验证的模式表）。 */
const FATAL_CONSOLE = [
  /Failed to resolve component/i,
  /does not provide an export named/i,
  /Failed to (fetch|resolve) dynamically imported module/i,
  /Failed to load module script/i,
]

async function api<T>(urlPath: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${BACKEND}${urlPath}`, { ...init, headers: { ...AUTH, ...(init?.headers ?? {}) } })
  const text = await res.text()
  if (!res.ok) throw new Error(`${init?.method ?? 'GET'} ${urlPath} → ${res.status} ${text.slice(0, 200)}`)
  return (JSON.parse(text) as { data: T }).data
}

async function createTask(title: string, extra: Record<string, unknown> = {}): Promise<number> {
  const data = await api<{ id: number }>('/api/todos', { method: 'POST', body: JSON.stringify({ title, ...extra }) })
  return data.id
}

async function removeTask(id: number): Promise<void> {
  try { await fetch(`${API}/${id}`, { method: 'DELETE', headers: AUTH }) } catch { /* 清理失败不影响结论 */ }
}

async function shot(page: Page, name: string): Promise<void> {
  mkdirSync(SHOTS, { recursive: true })
  await page.screenshot({ path: path.join(SHOTS, `${name}.png`), fullPage: false })
}

async function openTodoPage(page: Page): Promise<void> {
  await injectRealApiKey(page)
  await page.goto(ROUTE)
  await expect(page.getByRole('heading', { name: '待办任务' })).toBeVisible()
}

/** 打开详情面板（列表项按唯一标题命中）。 */
async function openDetail(page: Page, title: string): Promise<void> {
  await page.locator('.tt-item', { hasText: title }).first().click()
  await expect(page.locator('.td-key')).toBeVisible()
}

test.describe('todo-tracker 插件界面（远程加载 + 下发主链路）', () => {
  test.describe.configure({ timeout: 180_000 })

  test('A1 清单 entry 生效：/todo 由插件产物渲染，入口 200，版本徽标与 plugin.json 一致', async ({ page }) => {
    expect(ENTRY, 'plugin.json 必须声明 entry，否则界面仍走宿主回退').toContain('web/dist/index.js')

    const fatal: string[] = []
    const entryResponses: string[] = []
    page.on('console', m => {
      if (m.type() === 'error' && FATAL_CONSOLE.some(p => p.test(m.text()))) fatal.push(m.text())
    })
    page.on('response', r => {
      if (r.url().includes(`/plugins/${MANIFEST.Id}/`)) entryResponses.push(`${r.status()} ${r.headers()['content-type']} ${r.url()}`)
    })

    await openTodoPage(page)

    // 渲染证据：插件自己的根节点与徽标
    await expect(page.locator('.tt-root'), '插件界面未渲染').toBeVisible()
    await expect(page.locator('.tt-version')).toHaveText(new RegExp(`^v${VERSION.replace(/\./g, '\\.')}$`), { timeout: 15_000 })
    expect(page.url(), '宿主静态路由应已让位给插件声明的 /todo').toMatch(/\/todo$/)

    // 网络证据：入口 JS 被真实请求且 200 + JS MIME（404 时页面会走错误占位，光看徽标不够）
    const hit = await expect
      .poll(() => entryResponses.find(l => l.includes('web/dist/index.js')) ?? '', {
        message: `未捕获到 /plugins/${MANIFEST.Id}/web/dist/index.js 请求：${entryResponses.join(' | ')}`,
        timeout: 15_000,
      })
      .toMatch(/^200 /)
      .then(() => entryResponses.find(l => l.includes('web/dist/index.js')) as string)
    expect(hit, `入口资源 MIME 不是 JS：${hit}`).toContain('javascript')

    expect(fatal, `远程加载出现致命解析错误：${fatal.join(' | ')}`).toHaveLength(0)
    await shot(page, 'a1-remote-loaded')
  })

  test('B1 建任务 → 补齐四栏 → Git-Bash 写法关联项目 → 指定工件目录导入 → 生成提示词', async ({ page }) => {
    // 前提自查（阳性对照）：项目根与工件目录必须真实存在。
    // 没有这一句时，"关联后的根 == REPO_ROOT"这种两边同源的断言会自洽地假绿（实测踩过：路径少一层拿到 ForgeSelf.Web）。
    expect(existsSync(path.join(REPO_ROOT, 'docs', 'ai', 'pilot', ARTIFACT_DIR)),
      `工件目录不存在：${REPO_ROOT}${path.sep}docs${path.sep}ai${path.sep}pilot${path.sep}${ARTIFACT_DIR}`).toBe(true)

    const title = `E2E-下发链路-${Date.now()}`
    const id = await createTask(title)
    // 把插件页面真正打过的写请求与工件清单响应端出来：
    // 「连点保存旧快照盖新状态」「工件列表为空但界面只说没目录」这两类缺陷都只能靠响应正文分辨
    const writes: string[] = []
    const apiLog: string[] = []
    page.on('response', r => {
      const url = r.url()
      if (!url.includes('/api/todos')) return
      if (r.request().method() !== 'GET') writes.push(`${r.request().method()} ${r.status()}`)
      if (url.includes('artifact-sets')) {
        const sent = url.slice(url.indexOf('/api/'))
        r.text().then(b => apiLog.push(`GET ${sent} ⇒ ${b.slice(0, 300)}`)).catch(() => apiLog.push(`GET ${sent} ⇒（响应体读取失败）`))
      }
    })
    try {
      await openTodoPage(page)
      await openDetail(page, title)

      // 缺栏提示必须在界面上看得见（否则用户不知道"下发"为什么点不动）
      await expect(page.locator('.td-missing')).toContainText('还缺')

      await page.locator('#td-obj').fill('执行记录与下发载荷在界面与 REST 两侧一致')
      await page.locator('#td-content').fill('## 目标\n让 agent 拿到任务就能开工')
      await page.locator('#td-accept').fill('- [ ] e2e 判据一\n- [ ] e2e 判据二')
      await page.locator('#td-verify').fill('dotnet test --filter ~TodoTracker')
      // 点即保存走的是 blur：最后一栏必须失焦，否则 verification 还停在表单里没落库
      await page.locator('#td-verify').blur()
      await expect(page.locator('.td-ok'), `四栏齐备后界面应翻成可下发（写请求：${writes.join(' / ')}）`)
        .toHaveText('四栏齐备，可下发', { timeout: 15_000 })
      // 服务端也必须真有四栏：界面翻对了不代表存对了
      const four = await api<{ objective: string; content: string; acceptance: string; verification: string }>(`/api/todos/${id}`)
      expect(four.acceptance).toContain('e2e 判据二')
      expect(four.verification).toContain('dotnet test')

      // 关联项目：喂 Git-Bash 的 /c/... 写法，断言后端归一成真实存在的规范根
      const projectBlock = page.locator('.td-block').filter({ has: page.getByText('项目路径', { exact: true }) })
      await projectBlock.locator('input.td-input').first().fill(GITBASH_ROOT)
      await projectBlock.getByRole('button', { name: '关联' }).click()
      await expect(page.locator('.tt-toast').last()).toContainText('已关联项目', { timeout: 15_000 })
      // 详情面板必须翻成"已关联"：浏览器走查实测过 ProjectId 落成 0 ⇒ 列表显示项目、详情仍说「未关联项目」
      await expect(page.locator('.td-proj-line b'), '关联后详情面板仍显示未关联项目（ProjectId 没落上）').toBeVisible()
      const linked = await api<{ projectRoot: string; projectPathRaw: string; projectId: number }>(`/api/todos/${id}`)
      expect(linked.projectId, '显式关联必须登记宿主项目档案并拿到真实 id（0 = 项目过滤/计数全失效）').toBeGreaterThan(0)
      expect(linked.projectRoot.replace(/\\/g, '/').toLowerCase())
        .toBe(REPO_ROOT.replace(/\\/g, '/').toLowerCase())
      expect(linked.projectPathRaw, '用户原始写法要留档，便于回看"我当初填的是什么"').toBe(GITBASH_ROOT)

      // 指定本任务的工件目录（列表按名字升序，不指定就会选中最早那批）
      const importBlock = page.locator('.td-block').filter({ has: page.getByText('从工件导入', { exact: true }) })
      await importBlock.getByRole('button', { name: '列目录' }).click()
      await expect.poll(() => apiLog.join(' | '), {
        message: '点「列目录」后没有发出 artifact-sets 请求', timeout: 20_000,
      }).toContain('artifact-sets')
      // 先固定请求/响应证据，再断言界面：这样"清单为空"能一眼看出是后端空还是选择器错
      await expect(importBlock.locator(`option[value="${ARTIFACT_DIR}"]`),
        `工件清单里没有本任务的目录；实际请求与响应：${apiLog.join(' | ')}`).toBeAttached({ timeout: 20_000 })
      await importBlock.locator('select.td-input').first().selectOption(ARTIFACT_DIR)
      await expect(importBlock.locator('.td-files label')).not.toHaveCount(0)

      // 正文非空 → 导入是覆盖动作，必须弹二次确认（plugin-development §3.2）
      await importBlock.getByRole('button', { name: /导入为正文/ }).click()
      await expect(page.locator('.tt-confirm'), '覆盖正文未要求确认').toBeVisible()
      await expect(page.locator('.tt-confirm-detail')).toContainText('不可自动还原')
      await page.locator('[data-test=confirm-ok]').click()
      await expect(page.locator('.tt-toast').last()).toContainText('已导入', { timeout: 20_000 })

      const afterImport = await api<{ content: string; artifactRef: string }>(`/api/todos/${id}`)
      expect(afterImport.content).toContain('### 0')
      expect(afterImport.artifactRef).toContain(ARTIFACT_DIR)

      // 生成提示词：必须带按 taskKey 的回报契约，且不得出现真实 token
      const task = await api<{ taskKey: string }>(`/api/todos/${id}`)
      await page.getByRole('button', { name: '生成提示词' }).click()
      const promptBox = page.locator('textarea[aria-label="下发提示词"]')
      await expect(promptBox).toBeVisible()
      const prompt = await promptBox.inputValue()
      expect(prompt).toContain(`/api/todos/by-key/${task.taskKey}/records`)
      expect(prompt).toContain('Bearer <token>')
      expect(prompt, '真实密钥绝不进下发正文（BR-7）').not.toContain(getRealApiKey())
      await shot(page, 'b1-dispatch-preview')
    } finally {
      await removeTask(id)
    }
  })

  test('B2 下发 → 补记执行记录 → 阶段流转 → 完成，台账按序号可回放', async ({ page }) => {
    const title = `E2E-执行记录-${Date.now()}`
    const id = await createTask(title, {
      objective: '可验证目标', content: '正文', acceptance: '- [ ] 判据', verification: 'dotnet test',
    })
    try {
      await openTodoPage(page)
      await openDetail(page, title)

      await page.getByRole('button', { name: '下发', exact: true }).click()
      await expect(page.locator('.tt-toast').last()).toContainText('已下发', { timeout: 15_000 })

      await page.locator('[data-test=toggle-record-form]').click()
      await page.locator('[data-test=record-action]').fill('跑定向后端测试')
      await page.locator('[data-test=record-files]').fill('Plugins/TodoTracker/Services/TodoService.cs\nM Plugins/TodoTracker/plugin.json')
      await page.locator('textarea[placeholder="跑了什么命令 + 真实结果（PASS/FAIL）"]').fill('dotnet test → PASS')
      await page.locator('[data-test=record-submit]').click()
      await expect(page.locator('.tt-toast').last()).toContainText('执行记录已追加', { timeout: 15_000 })

      // 草稿起点下发会留两格系统记录（Draft→Ready、下发），加本次手工补记 = 3 条
      const serverTotal = (await api<{ total: number }>(`/api/todos/${id}/records?page=1&pageSize=100`)).total
      await expect(page.locator('.tl-item'), '时间线条数应与后端一致')
        .toHaveCount(Math.max(3, serverTotal), { timeout: 15_000 })
      // 倒序展示：最新一条在最上面，且「改了哪些文件」逐条成 code
      await expect(page.locator('.tl-item').first().locator('b.tl-action')).toHaveText('跑定向后端测试')
      await expect(page.locator('.tl-item').first().locator('code')).toHaveCount(2)
      await shot(page, 'b2-execution-timeline')

      // 阶段流转照服务端给的可达目标（Dispatched 之后可达 Running）
      const running = page.locator('[data-test=stage-Running]')
      await expect(running, '下发后应给出可流转到执行中的入口').toBeVisible()
      await running.click()
      await expect(page.locator('.tt-toast').last()).toContainText('已流转', { timeout: 15_000 })

      // 标记完成要二次确认
      await page.getByRole('button', { name: '标记完成', exact: true }).click()
      await expect(page.locator('.tt-confirm')).toBeVisible()
      await page.locator('[data-test=confirm-ok]').click()
      await expect(page.locator('.tt-toast').last()).toContainText('已标记完成', { timeout: 15_000 })
      await expect(page.locator('.td-stage')).toHaveText('完成')
    } finally {
      await removeTask(id)
    }
  })

  test('C1 删除：取消 ⇒ 一个删除请求都不发且后端状态未变；确认 ⇒ 真删', async ({ page }) => {
    const title = `E2E-删除确认-${Date.now()}`
    const id = await createTask(title)
    try {
      await openTodoPage(page)
      await openDetail(page, title)

      let deletes = 0
      page.on('request', r => { if (r.method() === 'DELETE' && r.url().includes(`/api/todos/${id}`)) deletes++ })

      await page.getByRole('button', { name: '删除任务' }).click()
      await expect(page.locator('.tt-confirm-detail')).toContainText('不可撤销')   // 文案必须交代后果
      await page.locator('[data-test=confirm-cancel]').click()
      await expect(page.locator('.tt-confirm')).toBeHidden()
      expect(deletes).toBe(0)
      await api<{ id: number }>(`/api/todos/${id}`)   // 后端仍然读得到，才算"真的没删"

      await page.getByRole('button', { name: '删除任务' }).click()
      await page.locator('[data-test=confirm-ok]').click()
      await expect(page.locator('.tt-toast').last()).toContainText('已删除', { timeout: 15_000 })
      await expect(page.locator('.tt-item', { hasText: title })).toHaveCount(0)
    } finally {
      await removeTask(id)
    }
  })

  test('D1 管理面鉴权（铁律 17）：不带 token 读 api/todos 必须 401', async () => {
    const res = await fetch(API)
    expect(res.status, '宿主无全局鉴权中间件，控制器必须自带策略').toBe(401)
  })

  test('D2 兼容：Home 面板的旧建单形状（只有 title/remark）仍可建可列', async () => {
    const title = `E2E-Home兼容-${Date.now()}`
    const created = await api<{ id: number; status: string; stage: string; taskKey: string }>('/api/todos', {
      method: 'POST',
      body: JSON.stringify({ title, remark: '只有三个字段' }),
    })
    try {
      expect(created.status).toBe('Pending')
      expect(created.stage).toBe('Draft')
      expect(created.taskKey).toHaveLength(32)   // 新键照发，旧调用不受影响
      const pageData = await api<{ items: { id: number }[]; total: number }>(`/api/todos?status=Pending&page=1&pageSize=20`)
      expect(pageData.items.some(t => t.id === created.id)).toBe(true)
      expect(pageData.total).toBeGreaterThan(0)
    } finally {
      await removeTask(created.id)
    }
  })

  test('D3 委派：能力在场就给真实回执，缺席就如实禁用并说明原因', async ({ page }) => {
    const title = `E2E-委派-${Date.now()}`
    const id = await createTask(title, {
      objective: '目标', content: '正文', acceptance: '- [ ] 判据', verification: 'dotnet test',
    })
    try {
      await openTodoPage(page)
      await openDetail(page, title)
      await page.getByRole('button', { name: '生成提示词' }).click()
      // 委派按钮的禁用原因取自预览结果：预览没落地就读 title，读到的只会是"还没生成"的占位文案
      await expect(page.locator('textarea[aria-label="下发提示词"]')).toBeVisible({ timeout: 15_000 })

      const btn = page.getByRole('button', { name: '交给 AgentHub 执行' })
      await expect(btn).toBeVisible()
      if (await btn.isDisabled()) {
        const reason = (await btn.getAttribute('title')) ?? ''
        expect(reason.length, '禁用必须给出原因').toBeGreaterThan(0)
        expect(reason, `禁用原因应交代 agent-hub 接缝状态：${reason}`).toContain('agent-hub')
      } else {
        // 能点说明 agent-hub 在场：那就必须给出真实回执（成功或原文透传的原因），不许静默
        await btn.click()
        const toast = page.locator('.tt-toast').last()
        await expect(toast).toBeVisible({ timeout: 25_000 })
        const text = await toast.innerText()
        expect(/已交给|未|不在|白名单|停用|失败/.test(text), `回执不可解释：${text}`).toBe(true)
      }
      await shot(page, 'd3-delegate')
    } finally {
      await removeTask(id)
    }
  })

  test('V1 视觉走查取证：列表 + 详情（空态、留白、溢出、图标）', async ({ page }) => {
    await openTodoPage(page)
    expect(existsSync(SHOTS) || mkdirSync(SHOTS, { recursive: true })).toBeTruthy()
    await shot(page, 'v1-list-default')

    const longTitle = `E2E-超长标题-${'很长'.repeat(30)}`
    const long = await createTask(longTitle)
    try {
      // 任务是接口建的（绕过界面），而本插件**不做轮询刷新**（§3.4 防闪要求，改由「刷新」按钮手动取）
      // ⇒ 不点刷新就永远看不到它。实测教训：此前直接 click .tt-item 能过，是因为库里躺着别人没清掉的残留，
      // 截的 v1-detail-open 根本不是本用例建的那条（同源自洽的假绿）。
      await page.getByRole('button', { name: '刷新' }).click()
      await openDetail(page, longTitle)
      // 详情里显示的 taskKey 必须等于**这条建出来的**任务的 key（库里可能同时躺着别的用例的残留，
      // 光断"有 key 且可见"锁不住"点开的是不是它"）。
      const stored = await api<{ id: number; taskKey: string }>(`/api/todos/${long}`)
      await expect(page.locator('.td-key')).toHaveText(stored.taskKey)
      await shot(page, 'v1-detail-open')

      await page.reload()
      const item = page.locator('.tt-item', { hasText: longTitle }).first()
      await expect(item.locator('.tt-item-title')).toBeVisible()
      const box = await item.locator('.tt-item-title').boundingBox()
      expect(box, '长标题应被单行省略号约束（不得撑破卡片）').not.toBeNull()
      const card = await item.locator('.tt-item-top').boundingBox()
      expect(box!.width, '标题宽度不应超出所在行容器').toBeLessThanOrEqual((card?.width ?? 0) + 1)
      await shot(page, 'v1-long-title')
    } finally {
      await removeTask(long)
    }
  })
})
