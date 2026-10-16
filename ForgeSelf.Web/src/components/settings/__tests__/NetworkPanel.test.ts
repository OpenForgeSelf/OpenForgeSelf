import { describe, it, expect, beforeEach, afterEach, vi, type Mock } from 'vitest';
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils';
import NetworkPanel from '@/components/settings/NetworkPanel.vue';

/**
 * 网络设置面板单测（2026-10-16-settings-network-proxy AC-UX-1/AC-UX-2/AC-4 前端侧）。
 * 模式沿用 ApiKeysPanel.test.ts：vi.stubGlobal fetch + mount，零 mock 服务层。
 */

function jsonResponse(data: unknown, ok = true, status = 200): Response {
  return {
    ok,
    status,
    json: async () => (ok ? { data } : { message: (data as { message?: string })?.message ?? '请求失败' }),
  } as unknown as Response;
}

const SERVER_OFF = {
  defaultModel: 'OpenAI:gpt-4o',
  homePluginId: 'home',
  proxyEnabled: false,
  proxyHost: '',
  proxyPort: 0,
  proxyUsername: '',
  proxyPasswordSet: false,
};

describe('NetworkPanel', () => {
  let fetchMock: Mock;

  beforeEach(() => {
    fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
    fetchMock.mockImplementation(async () => jsonResponse(SERVER_OFF));
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
    document.body.innerHTML = '';
  });

  async function mountPanel(server: Record<string, unknown> = SERVER_OFF): Promise<VueWrapper> {
    fetchMock.mockImplementation(async (_url: string, opts?: RequestInit) => {
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
    if ('proxyEnabled' in body) patch.proxyEnabled = body.proxyEnabled;
    if ('proxyHost' in body) patch.proxyHost = body.proxyHost;
    if ('proxyPort' in body) patch.proxyPort = body.proxyPort;
    if ('proxyUsername' in body) patch.proxyUsername = body.proxyUsername;
    if ('proxyPassword' in body) patch.proxyPasswordSet = body.proxyPassword !== '';
    return patch;
  }

  async function toggleSwitch(wrapper: VueWrapper): Promise<void> {
    await wrapper.find('input.el-switch__input').trigger('click');
    await wrapper.vm.$nextTick();
  }

  function postBodies(): Record<string, unknown>[] {
    return fetchMock.mock.calls
      .filter((c) => (c[1] as RequestInit | undefined)?.method === 'POST')
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

  it('关闭态：只显示说明文案，不渲染地址/端口/账号表单（AC-UX-1）', async () => {
    const wrapper = await mountPanel();
    expect(wrapper.text()).toContain('当前不走代理');
    expect(wrapper.find('input[aria-label="代理服务器地址"]').exists()).toBe(false);
    expect(wrapper.find('[data-testid="save-btn"]').exists()).toBe(false);
  });

  it('开启后按「地址、端口、用户名、密码」顺序显示表单行（AC-UX-1）', async () => {
    const wrapper = await mountPanel();
    await toggleSwitch(wrapper);

    const html = wrapper.html();
    const order = ['代理服务器地址', '代理端口', '代理认证用户名', '代理认证密码'].map(
      (label) => html.indexOf(`aria-label="${label}"`),
    );
    expect(order.every((i) => i >= 0)).toBe(true);
    expect(order).toEqual([...order].sort((a, b) => a - b));
  });

  it('开启态：无错误时错误行仍常驻等高占位（防布局跳动吞点击，2026-10-16 e2e 实证）', async () => {
    const wrapper = await mountPanel();
    await toggleSwitch(wrapper);

    expect(wrapper.find('[data-testid="host-error"]').exists()).toBe(true);
    expect(wrapper.find('[data-testid="host-error"]').text()).toBe('');
    expect(wrapper.find('[data-testid="port-error"]').text()).toBe('');
  });

  it('开启但地址为空：保存被拦截，行内报错且不发请求（AC-UX-2）', async () => {
    const wrapper = await mountPanel();
    await toggleSwitch(wrapper);
    await clickSave(wrapper);

    expect(wrapper.find('[data-testid="host-error"]').text()).toContain('请输入代理服务器地址');
    expect(postBodies()).toHaveLength(0);
  });

  it('地址带 http:// 前缀：前端校验拦截，错误文案与后端 BR-5 同规则', async () => {
    const wrapper = await mountPanel();
    await toggleSwitch(wrapper);
    setInput(wrapper, '代理服务器地址', 'http://proxy.local');
    setInput(wrapper, '代理认证用户名', 'u');
    // 端口保持 0 → 也属非法，先验证 host 错误
    await clickSave(wrapper);

    expect(wrapper.find('[data-testid="host-error"]').text()).toContain('仅支持域名或 IPv4');
    expect(postBodies()).toHaveLength(0);
  });

  it('端口非法（0）：行内报错端口提示', async () => {
    const wrapper = await mountPanel();
    await toggleSwitch(wrapper);
    setInput(wrapper, '代理服务器地址', 'proxy.example.com');
    await clickSave(wrapper);

    expect(wrapper.find('[data-testid="port-error"]').text()).toContain('1-65535');
    expect(postBodies()).toHaveLength(0);
  });

  it('合法配置保存：载荷 Trim 后提交，不含 proxyPassword（留空≠清空）', async () => {
    const wrapper = await mountPanel();
    await toggleSwitch(wrapper);
    setInput(wrapper, '代理服务器地址', ' 10.0.0.9 ');
    // el-input-number：直接改内部 input 并触发 change 不可靠，改为断言校验通过后由 vm 提交端口
    // 端口通过组件默认 0 会被拦；这里用键盘输入方式设置值
    const portInput = wrapper.find('input[aria-label="代理端口"]');
    (portInput.element as HTMLInputElement).value = '7890';
    portInput.trigger('change');
    await flushPromises();
    setInput(wrapper, '代理认证用户名', ' alice ');
    await clickSave(wrapper);

    const bodies = postBodies();
    expect(bodies).toHaveLength(1);
    expect(bodies[0]).toEqual({
      proxyEnabled: true,
      proxyHost: '10.0.0.9',
      proxyPort: 7890,
      proxyUsername: 'alice',
    });
    expect('proxyPassword' in bodies[0]).toBe(false);
    expect(document.body.textContent).toContain('网络设置已保存');
  });

  it('输入密码：随保存提交覆盖', async () => {
    const wrapper = await mountPanel();
    await toggleSwitch(wrapper);
    setInput(wrapper, '代理服务器地址', 'proxy.example.com');
    const portInput = wrapper.find('input[aria-label="代理端口"]');
    (portInput.element as HTMLInputElement).value = '8080';
    portInput.trigger('change');
    await flushPromises();
    setInput(wrapper, '代理认证密码', 'pass123');
    await clickSave(wrapper);

    expect(postBodies()[0]).toMatchObject({ proxyPassword: 'pass123' });
  });

  it('已存密码：placeholder 提示「已保存（留空则不修改）」并显示清除入口', async () => {
    const wrapper = await mountPanel({ ...SERVER_OFF, proxyEnabled: true, proxyHost: 'proxy.example.com', proxyPort: 7890, proxyPasswordSet: true });
    // 服务端为开启态，表单直接可见
    expect(wrapper.find('input[aria-label="代理认证密码"]').attributes('placeholder')).toBe('已保存（留空则不修改）');
    const clearBtn = wrapper.findAll('button').find((b) => b.text().trim() === '清除密码');
    expect(clearBtn).toBeTruthy();
  });

  it('点「清除密码」后保存：提交空串显式清空', async () => {
    const wrapper = await mountPanel({ ...SERVER_OFF, proxyEnabled: true, proxyHost: 'proxy.example.com', proxyPort: 7890, proxyPasswordSet: true });
    const clearBtn = wrapper.findAll('button').find((b) => b.text().trim() === '清除密码');
    await clearBtn!.trigger('click');
    expect(wrapper.text()).toContain('保存后将清空');

    await clickSave(wrapper);
    expect(postBodies()[0]).toMatchObject({ proxyPassword: '' });
  });

  it('后端 400：错误原文透出，用户输入不清空', async () => {
    fetchMock.mockImplementation(async (_url: string, opts?: RequestInit) => {
      if ((opts?.method ?? 'GET') === 'POST') {
        return jsonResponse({ message: '代理地址无效：仅支持域名或 IPv4（不能带 http:// 前缀或端口）' }, false, 400);
      }
      return jsonResponse(SERVER_OFF);
    });
    const wrapper = mount(NetworkPanel, { attachTo: document.body });
    await flushPromises();
    await toggleSwitch(wrapper);
    setInput(wrapper, '代理服务器地址', 'proxy.example.com');
    const portInput = wrapper.find('input[aria-label="代理端口"]');
    (portInput.element as HTMLInputElement).value = '7890';
    portInput.trigger('change');
    await flushPromises();
    await clickSave(wrapper);

    expect(document.body.textContent).toContain('代理地址无效');
    expect((wrapper.find('input[aria-label="代理服务器地址"]').element as HTMLInputElement).value).toBe('proxy.example.com');
  });
});
