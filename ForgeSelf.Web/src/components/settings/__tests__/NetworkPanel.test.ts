import { describe, it, expect, beforeEach, afterEach, vi, type Mock } from 'vitest';
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils';
import NetworkPanel from '@/components/settings/NetworkPanel.vue';

/**
 * 网络设置面板单测（2026-10-16-settings-proxy-mode-bypass AC-9 前端侧）。
 * 模式沿用 ApiKeysPanel.test.ts：vi.stubGlobal fetch + mount，零 mock 服务层。
 * 覆盖：三模式渲染区、手动表单校验、h-4 常驻占位、测试按钮状态机（拦截/loading/成败/重置）、
 * 旁路条目增删与校验、保存载荷、密码三语义、后端 400 原文透出。
 */

function jsonResponse(data: unknown, ok = true, status = 200): Response {
  return {
    ok,
    status,
    json: async () => (ok ? { data } : { message: (data as { message?: string })?.message ?? '请求失败' }),
  } as unknown as Response;
}

const SERVER_DEFAULT = {
  defaultModel: 'OpenAI:gpt-4o',
  homePluginId: 'home',
  proxyMode: 'system',
  proxyBypass: [] as string[],
  proxyHost: '',
  proxyPort: 0,
  proxyUsername: '',
  proxyPasswordSet: false,
};

const SERVER_MANUAL = {
  ...SERVER_DEFAULT,
  proxyMode: 'manual',
  proxyHost: 'proxy.example.com',
  proxyPort: 7890,
};

describe('NetworkPanel', () => {
  let fetchMock: Mock;
  let testProxyResult: Record<string, unknown> = { reachable: true, statusCode: 200, latencyMs: 128, message: '代理可用（上游返回 HTTP 200）' };

  beforeEach(() => {
    fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
    testProxyResult = { reachable: true, statusCode: 200, latencyMs: 128, message: '代理可用（上游返回 HTTP 200）' };
    fetchMock.mockImplementation(async () => jsonResponse(SERVER_DEFAULT));
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
    document.body.innerHTML = '';
  });

  async function mountPanel(server: Record<string, unknown> = SERVER_DEFAULT): Promise<VueWrapper> {
    fetchMock.mockImplementation(async (url: string, opts?: RequestInit) => {
      if (String(url).includes('test-proxy')) return jsonResponse(testProxyResult);
      if ((opts?.method ?? 'GET') === 'POST') return jsonResponse({ ...server, ...echoPost(opts) });
      return jsonResponse(server);
    });
    const wrapper = mount(NetworkPanel, { attachTo: document.body });
    await flushPromises();
    await flushPromises();
    return wrapper;
  }

  /** POST 响应 = 服务端回显（模拟控制器返回全量 DTO；密码永不下发只有存在性） */
  function echoPost(opts?: RequestInit): Record<string, unknown> {
    const body = JSON.parse(String(opts?.body ?? '{}')) as Record<string, unknown>;
    const patch: Record<string, unknown> = {};
    if ('proxyMode' in body) patch.proxyMode = body.proxyMode;
    if ('proxyBypass' in body) patch.proxyBypass = body.proxyBypass;
    if ('proxyHost' in body) patch.proxyHost = body.proxyHost;
    if ('proxyPort' in body) patch.proxyPort = body.proxyPort;
    if ('proxyUsername' in body) patch.proxyUsername = body.proxyUsername;
    if ('proxyPassword' in body) patch.proxyPasswordSet = body.proxyPassword !== '';
    return patch;
  }

  async function selectMode(wrapper: VueWrapper, mode: 'system' | 'manual' | 'direct'): Promise<void> {
    await wrapper.find(`input[value="${mode}"]`).trigger('click');
    await wrapper.vm.$nextTick();
  }

  function postBodies(urlFilter?: string): Record<string, unknown>[] {
    return fetchMock.mock.calls
      .filter((c) => (c[1] as RequestInit | undefined)?.method === 'POST')
      .filter((c) => !urlFilter || String(c[0]).includes(urlFilter))
      .map((c) => JSON.parse(String((c[1] as RequestInit).body)));
  }

  async function clickSave(wrapper: VueWrapper): Promise<void> {
    await wrapper.find('[data-testid="save-btn"]').trigger('click');
    await flushPromises();
    await flushPromises();
  }

  function setInput(wrapper: VueWrapper, ariaLabel: string, value: string): void {
    const input = wrapper.find(`input[aria-label="${ariaLabel}"]`);
    expect(input.exists(), `缺少 aria-label=${ariaLabel} 的输入框`).toBe(true);
    (input.element as HTMLInputElement).value = value;
    input.trigger('input');
  }

  function setPort(wrapper: VueWrapper, value: string): void {
    const portInput = wrapper.find('input[aria-label="代理端口"]');
    (portInput.element as HTMLInputElement).value = value;
    portInput.trigger('change');
  }

  async function clickTest(wrapper: VueWrapper): Promise<void> {
    await wrapper.find('[data-testid="proxy-test-btn"]').trigger('click');
    await flushPromises();
    await flushPromises();
  }

// region 三模式渲染

  it('默认跟随系统：显示系统代理说明 + 旁路条目区，无地址表单（FR-1/FR-2）', async () => {
    const wrapper = await mountPanel();
    expect(wrapper.text()).toContain('按 Windows 系统代理设置');
    expect(wrapper.find('input[aria-label="代理服务器地址"]').exists()).toBe(false);
    expect(wrapper.find('#bypass-list').exists()).toBe(true);
  });

  it('切手动：出现地址/探测/端口/用户名/密码表单与测试按钮，按序排列（FR-1/FR-3）', async () => {
    const wrapper = await mountPanel();
    await selectMode(wrapper, 'manual');

    const html = wrapper.html();
    const order = ['代理服务器地址', '探测目标地址', '代理端口', '代理认证用户名', '代理认证密码'].map(
      (label) => html.indexOf(`aria-label="${label}"`),
    );
    expect(order.every((i) => i >= 0)).toBe(true);
    expect(order).toEqual([...order].sort((a, b) => a - b));
    expect(wrapper.find('[data-testid="proxy-test-btn"]').exists()).toBe(true);
  });

  it('切直连：显示说明且隐藏旁路条目区（全部直连时旁路无意义）', async () => {
    const wrapper = await mountPanel();
    await selectMode(wrapper, 'direct');
    expect(wrapper.text()).toContain('当前不发任何代理');
    expect(wrapper.find('#bypass-list').exists()).toBe(false);
  });

  it('模式切换不清空已填表单值', async () => {
    const wrapper = await mountPanel();
    await selectMode(wrapper, 'manual');
    setInput(wrapper, '代理服务器地址', 'keep.me');
    await selectMode(wrapper, 'direct');
    await selectMode(wrapper, 'manual');
    expect((wrapper.find('input[aria-label="代理服务器地址"]').element as HTMLInputElement).value).toBe('keep.me');
  });

// endregion

// region 手动表单校验（沿用上一批 AC-UX-2 + h-4 占位）

  it('手动无地址：保存被拦，行内报错且不发请求；错误行常驻等高占位', async () => {
    const wrapper = await mountPanel();
    await selectMode(wrapper, 'manual');

    expect(wrapper.find('[data-testid="host-error"]').exists()).toBe(true);
    expect(wrapper.find('[data-testid="host-error"]').text()).toBe('');

    await clickSave(wrapper);
    expect(wrapper.find('[data-testid="host-error"]').text()).toContain('请输入代理服务器地址');
    expect(postBodies()).toHaveLength(0);
  });

  it('地址带 http:// 前缀：前端拦截，文案与后端 BR-5 同规则', async () => {
    const wrapper = await mountPanel();
    await selectMode(wrapper, 'manual');
    setInput(wrapper, '代理服务器地址', 'http://proxy.local');
    await clickSave(wrapper);

    expect(wrapper.find('[data-testid="host-error"]').text()).toContain('仅支持域名或 IPv4');
    expect(postBodies()).toHaveLength(0);
  });

  it('端口非法（0）：行内报错端口提示', async () => {
    const wrapper = await mountPanel();
    await selectMode(wrapper, 'manual');
    setInput(wrapper, '代理服务器地址', 'proxy.example.com');
    await clickSave(wrapper);

    expect(wrapper.find('[data-testid="port-error"]').text()).toContain('1-65535');
    expect(postBodies()).toHaveLength(0);
  });

// endregion

// region 测试按钮状态机（FR-3）

  it('未填地址点测试：拦截提示且不发 test-proxy 请求', async () => {
    const wrapper = await mountPanel();
    await selectMode(wrapper, 'manual');
    await clickTest(wrapper);

    expect(wrapper.find('[data-testid="host-error"]').text()).toContain('请先填写地址和端口');
    expect(postBodies('test-proxy')).toHaveLength(0);
  });

  it('测试成功：任何 HTTP 响应=代理可用，行内反馈含状态码与延迟', async () => {
    testProxyResult = { reachable: true, statusCode: 401, latencyMs: 128, message: '代理可用（上游返回 HTTP 401）' };
    const wrapper = await mountPanel(SERVER_MANUAL);
    await clickTest(wrapper);

    const fb = wrapper.find('[data-testid="proxy-test-feedback"]').text();
    expect(fb).toContain('代理可用');
    expect(fb).toContain('HTTP 401');
    expect(fb).toContain('128ms');
    // 载荷 = 当前表单值（未保存也按其测试）
    expect(postBodies('test-proxy')[0]).toMatchObject({ proxyHost: 'proxy.example.com', proxyPort: 7890 });
  });

  it('测试失败：红色文案透出错误原文', async () => {
    testProxyResult = { reachable: false, statusCode: null, latencyMs: 5, message: '由于目标计算机积极拒绝，无法连接。' };
    const wrapper = await mountPanel(SERVER_MANUAL);
    await clickTest(wrapper);

    expect(wrapper.find('[data-testid="proxy-test-feedback"]').text()).toContain('积极拒绝');
  });

  it('已有结果后改任意输入 → 反馈重置回 idle（结果必须与当前表单一致）', async () => {
    const wrapper = await mountPanel(SERVER_MANUAL);
    await clickTest(wrapper);
    expect(wrapper.find('[data-testid="proxy-test-feedback"]').text()).toContain('代理可用');

    setInput(wrapper, '代理服务器地址', 'other.proxy');
    // setInput 内 trigger 未 await：重置回 idle 需等一次渲染刷新才能读到新 DOM
    await flushPromises();
    expect(wrapper.find('[data-testid="proxy-test-feedback"]').text()).toContain('收到任何 HTTP 响应');
  });

  it('探测地址填入后随测试载荷提交', async () => {
    const wrapper = await mountPanel(SERVER_MANUAL);
    setInput(wrapper, '探测目标地址', 'https://gpustack.luxvisions-inno.com/v1');
    await clickTest(wrapper);

    expect(postBodies('test-proxy')[0]).toMatchObject({ proxyTestUrl: 'https://gpustack.luxvisions-inno.com/v1' });
  });

// endregion

// region 旁路条目（FR-2/FR-4）

  it('空态显示说明文案；点添加出现可删行，删除后回空态', async () => {
    const wrapper = await mountPanel();
    expect(wrapper.find('[data-testid="bypass-empty"]').exists()).toBe(true);

    await wrapper.find('[data-testid="bypass-add-btn"]').trigger('click');
    await wrapper.vm.$nextTick();
    expect(wrapper.findAll('[data-testid="bypass-row"]')).toHaveLength(1);
    expect(wrapper.find('[data-testid="bypass-empty"]').exists()).toBe(false);

    await wrapper.find('[data-testid="bypass-remove-btn"]').trigger('click');
    await wrapper.vm.$nextTick();
    expect(wrapper.find('[data-testid="bypass-empty"]').exists()).toBe(true);
  });

  it('条目输入非法字符：保存被拦且行内报错带行号，不发请求（BR-5 前端侧）', async () => {
    const wrapper = await mountPanel();
    await wrapper.find('[data-testid="bypass-add-btn"]').trigger('click');
    await wrapper.vm.$nextTick();
    const rowInput = wrapper.find('[data-testid="bypass-row"] input');
    (rowInput.element as HTMLInputElement).value = 'bad entry!';
    rowInput.trigger('input');
    await clickSave(wrapper);

    expect(wrapper.find('[data-testid="bypass-error"]').text()).toContain('第 1 条无效');
    expect(postBodies()).toHaveLength(0);
  });

  it('保存载荷：条目 trim 去空后以数组提交（点即保存语义不变）', async () => {
    const wrapper = await mountPanel(SERVER_MANUAL);
    await wrapper.find('[data-testid="bypass-add-btn"]').trigger('click');
    await wrapper.vm.$nextTick();
    const rowInput = wrapper.find('[data-testid="bypass-row"] input');
    (rowInput.element as HTMLInputElement).value = ' *.internal.dev ';
    rowInput.trigger('input');
    await clickSave(wrapper);

    const body = postBodies().find((b) => 'proxyMode' in b);
    expect(body).toMatchObject({ proxyMode: 'manual', proxyBypass: ['*.internal.dev'] });
  });

  it('已有条目随 GET 回显渲染为行列表', async () => {
    const wrapper = await mountPanel({ ...SERVER_DEFAULT, proxyBypass: ['*.a.com', '10.*'] });
    expect(wrapper.findAll('[data-testid="bypass-row"]')).toHaveLength(2);
  });

  it('50 条上限：按钮禁用并有原因提示', async () => {
    const many = Array.from({ length: 50 }, (_, i) => `h${i}.example.com`);
    const wrapper = await mountPanel({ ...SERVER_DEFAULT, proxyBypass: many });
    const btn = wrapper.find('[data-testid="bypass-add-btn"]');
    expect((btn.element as HTMLButtonElement).disabled).toBe(true);
    expect(btn.attributes('title')).toContain('50');
  });

// endregion

// region 保存与密码语义（不回归）

  it('合法配置保存：载荷含 proxyMode/proxyBypass，不含 proxyPassword（留空≠清空）', async () => {
    const wrapper = await mountPanel();
    await selectMode(wrapper, 'manual');
    setInput(wrapper, '代理服务器地址', ' 10.0.0.9 ');
    setPort(wrapper, '7890');
    await flushPromises();
    setInput(wrapper, '代理认证用户名', ' alice ');
    await clickSave(wrapper);

    const bodies = postBodies();
    expect(bodies).toHaveLength(1);
    expect(bodies[0]).toEqual({
      proxyMode: 'manual',
      proxyHost: '10.0.0.9',
      proxyPort: 7890,
      proxyUsername: 'alice',
      proxyBypass: [],
    });
    expect('proxyPassword' in bodies[0]).toBe(false);
    expect(document.body.textContent).toContain('网络设置已保存');
  });

  it('输入密码：随保存提交覆盖', async () => {
    const wrapper = await mountPanel(SERVER_MANUAL);
    setInput(wrapper, '代理认证密码', 'pass123');
    await clickSave(wrapper);

    expect(postBodies()[0]).toMatchObject({ proxyPassword: 'pass123' });
  });

  it('已存密码：placeholder 提示并显示清除入口；点清除后保存提交空串', async () => {
    const wrapper = await mountPanel({ ...SERVER_MANUAL, proxyPasswordSet: true });
    expect(wrapper.find('input[aria-label="代理认证密码"]').attributes('placeholder')).toBe('已保存（留空则不修改）');

    const clearBtn = wrapper.findAll('button').find((b) => b.text().trim() === '清除密码');
    await clearBtn!.trigger('click');
    expect(wrapper.text()).toContain('保存后将清空');

    await clickSave(wrapper);
    expect(postBodies()[0]).toMatchObject({ proxyPassword: '' });
  });

  it('后端 400：错误原文透出，用户输入不清空', async () => {
    fetchMock.mockImplementation(async (url: string, opts?: RequestInit) => {
      if ((opts?.method ?? 'GET') === 'POST' && !String(url).includes('test-proxy')) {
        return jsonResponse({ message: '代理模式无效：仅支持 system / manual / direct' }, false, 400);
      }
      return jsonResponse(SERVER_DEFAULT);
    });
    const wrapper = mount(NetworkPanel, { attachTo: document.body });
    await flushPromises();
    await selectMode(wrapper, 'manual');
    setInput(wrapper, '代理服务器地址', 'proxy.example.com');
    setPort(wrapper, '7890');
    await flushPromises();
    await clickSave(wrapper);

    expect(document.body.textContent).toContain('代理模式无效');
    expect((wrapper.find('input[aria-label="代理服务器地址"]').element as HTMLInputElement).value).toBe('proxy.example.com');
  });

// endregion
});
