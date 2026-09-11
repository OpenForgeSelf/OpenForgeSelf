import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils';
import ApiKeysPanel from '@/components/settings/ApiKeysPanel.vue';

/** 构造一条符合 ApiKeyItem 的假数据 */
function makeItem(overrides: Record<string, unknown> = {}) {
  return {
    id: 1,
    name: '笔记本脚本',
    remark: 'NAS 同步',
    maskedKey: 'sk-****abcd',
    canDecrypt: true,
    enabled: true,
    isExpired: false,
    expiresAt: null,
    lastUsedAt: null,
    createdAt: '2026-01-02T03:04:05Z',
    updatedAt: '2026-01-02T03:04:05Z',
    ...overrides,
  };
}

const MASTER_CONFIG = {
  apiBaseUrl: 'http://localhost:7102/v1',
  apiKeyMasked: 'sk-****9999',
  authHeader: 'Authorization: Bearer sk-****9999',
  hasKey: true,
};

const CREATE_RESULT = {
  item: makeItem({ id: 2, name: '新密钥', maskedKey: 'sk-****1111' }),
  plainKey: 'sk-new-plain-key-0001',
  authHeader: 'Authorization: Bearer sk-new-plain-key-0001',
};

/** 一次性明文弹窗中需要出现的明文与警示文案 */
const PLAIN_KEY = 'sk-new-plain-key-0001';
const PLAIN_WARNING = '关闭本窗口后不可再次查看';

function jsonResponse(data: unknown, ok = true, status = 200): Response {
  return { ok, status, json: async () => ({ data }) } as unknown as Response;
}

/** 在 Element Plus MessageBox（挂载于 body）中按文案点击按钮 */
function clickMessageBoxButton(text: string): void {
  const buttons = Array.from(document.querySelectorAll('.el-message-box__btns button'));
  const target = buttons.find((btn) => btn.textContent?.trim() === text) as HTMLButtonElement | undefined;
  expect(target, `MessageBox 中未找到按钮「${text}」`).toBeTruthy();
  target!.click();
}

describe('ApiKeysPanel', () => {
  let fetchMock: ReturnType<typeof vi.fn>;
  let listData: Record<string, unknown>[];

  beforeEach(() => {
    listData = [
      makeItem({ id: 1, name: '笔记本脚本' }),
      makeItem({ id: 2, name: '手机端', enabled: false }),
    ];
    fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: { writeText: vi.fn().mockResolvedValue(undefined) },
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.clearAllMocks();
    document.body.innerHTML = '';
  });

  /** 安装 fetch 假实现：按 URL + method 路由到不同响应 */
  function mockApi(): void {
    fetchMock.mockImplementation(async (url: string, opts?: RequestInit) => {
      const target = String(url);
      const method = opts?.method ?? 'GET';
      if (target.includes('/api/api-server/status')) return jsonResponse(MASTER_CONFIG);
      if (target.includes('/toggle')) {
        const body = JSON.parse(String(opts?.body ?? '{}')) as { enabled: boolean };
        return jsonResponse(makeItem({ enabled: body.enabled }));
      }
      if (target.endsWith('/roll')) return jsonResponse(CREATE_RESULT);
      if (target === '/api/api-keys' && method === 'GET') return jsonResponse(listData);
      if (target === '/api/api-keys' && method === 'POST') return jsonResponse(CREATE_RESULT);
      if (method === 'PUT' && target.includes('/api/api-keys/')) {
        return jsonResponse(makeItem({ name: '改过的名字' }));
      }
      if (method === 'DELETE' && target.includes('/api/api-keys/')) return jsonResponse({ deleted: true });
      return jsonResponse({}, false, 404);
    });
  }

  async function mountPanel(): Promise<VueWrapper> {
    mockApi();
    const wrapper = mount(ApiKeysPanel, { attachTo: document.body });
    await flushPromises();
    await flushPromises();
    return wrapper;
  }

  /** 按可见文案在组件内查找按钮 */
  function findButton(wrapper: VueWrapper, text: string) {
    return wrapper.findAll('button').find((btn) => btn.text().trim() === text);
  }

  /** 取子密钥表格的某一行操作按钮 */
  function rowButton(wrapper: VueWrapper, rowIndex: number, text: string) {
    const rows = wrapper.findAll('.el-table__body-wrapper tbody tr');
    return rows[rowIndex].findAll('button').find((btn) => btn.text().trim() === text);
  }

  it('渲染子密钥列表（名称 / 掩码 / 状态）', async () => {
    const wrapper = await mountPanel();
    const text = wrapper.text();

    expect(text).toContain('笔记本脚本');
    expect(text).toContain('手机端');
    expect(text).toContain('sk-****abcd');
    expect(text).toContain('启用');
    expect(text).toContain('已停用');
    // 加载时请求了列表与服务器状态
    expect(fetchMock.mock.calls.some((c) => String(c[0]) === '/api/api-keys')).toBe(true);
    expect(fetchMock.mock.calls.some((c) => String(c[0]).includes('/api/api-server/status'))).toBe(true);
  });

  it('渲染主密钥只读卡片，标注不可删除 / 不可停用', async () => {
    const wrapper = await mountPanel();
    const text = wrapper.text();

    expect(text).toContain('主密钥 · 本机主界面使用');
    expect(text).toContain('不可删除、不可停用');
    // 掩码展示在只读 input 的 value 上（text() 不含 input 值）
    const masterInput = wrapper.findAll('.el-card')[0].find('input');
    expect((masterInput.element as HTMLInputElement).value).toBe('sk-****9999');
    // 主密钥卡片只提供「重新生成」，没有删除 / 停用
    const masterButtons = wrapper
      .findAll('.el-card')[0]
      .findAll('button')
      .map((b) => b.text().trim());
    expect(masterButtons).toEqual(['重新生成']);
  });

  it('canDecrypt 为 false 时提示无法在本机解密', async () => {
    listData = [makeItem({ id: 3, name: '换机后的旧密钥', canDecrypt: false, maskedKey: '' })];
    const wrapper = await mountPanel();

    expect(wrapper.text()).toContain('无法在本机解密，请重新生成');
  });

  it('创建密钥后弹出一次性明文弹窗（含明文与不可再次查看警示）', async () => {
    const wrapper = await mountPanel();

    // 打开新建弹窗
    const createBtn = findButton(wrapper, '新建密钥');
    expect(createBtn).toBeTruthy();
    await createBtn!.trigger('click');
    await flushPromises();

    // 填写名称（el-dialog 传送到 body，需在 document 上查找）
    const nameInput = document.body.querySelector('.el-dialog .el-input__inner') as HTMLInputElement;
    expect(nameInput).toBeTruthy();
    nameInput.value = '新密钥';
    nameInput.dispatchEvent(new Event('input'));
    await flushPromises();

    // 点击确定
    const okBtn = Array.from(document.body.querySelectorAll('.el-dialog__footer button')).find(
      (btn) => btn.textContent?.trim() === '确定',
    ) as HTMLButtonElement | undefined;
    expect(okBtn).toBeTruthy();
    okBtn!.click();
    await flushPromises();
    await flushPromises();

    // 调用了 POST /api/api-keys，且带上了名称
    const postCall = fetchMock.mock.calls.find(
      (call) => (call[1]?.method ?? 'GET') === 'POST' && String(call[0]) === '/api/api-keys',
    );
    expect(postCall).toBeTruthy();
    const body = JSON.parse(String(postCall?.[1]?.body)) as { name: string };
    expect(body.name).toBe('新密钥');

    // 一次性明文弹窗：真实渲染于 body，含明文与警示文案
    const boxText = document.body.textContent ?? '';
    expect(boxText).toContain(PLAIN_KEY);
    expect(boxText).toContain(PLAIN_WARNING);

    // 关闭后列表刷新，列表里只有掩码、不含明文
    clickMessageBoxButton('我已保存');
    await flushPromises();
    expect(wrapper.text()).not.toContain(PLAIN_KEY);
  });

  it('点击停用调用 toggle 接口并传 enabled=false', async () => {
    const wrapper = await mountPanel();

    const toggleBtn = rowButton(wrapper, 0, '停用');
    expect(toggleBtn).toBeTruthy();
    await toggleBtn!.trigger('click');
    await flushPromises();

    const call = fetchMock.mock.calls.find((c) => String(c[0]).includes('/toggle'));
    expect(call).toBeTruthy();
    expect(call![1]?.method).toBe('POST');
    expect(JSON.parse(String(call![1]?.body))).toEqual({ enabled: false });
    // 停用时列表里的条目变为已停用
    expect(wrapper.text()).toContain('已停用');
  });

  it('点击删除：确认后调用 DELETE 接口', async () => {
    const wrapper = await mountPanel();

    const delBtn = rowButton(wrapper, 0, '删除');
    expect(delBtn).toBeTruthy();
    await delBtn!.trigger('click');
    await flushPromises();

    // 先弹二次确认
    expect(document.body.textContent).toContain('删除确认');
    clickMessageBoxButton('删除');
    await flushPromises();

    const call = fetchMock.mock.calls.find((c) => (c[1]?.method ?? 'GET') === 'DELETE');
    expect(call).toBeTruthy();
    expect(String(call![0])).toContain('/api/api-keys/1');
  });

  it('点击删除：取消确认时不调用 DELETE 接口', async () => {
    const wrapper = await mountPanel();

    const delBtn = rowButton(wrapper, 0, '删除');
    await delBtn!.trigger('click');
    await flushPromises();

    clickMessageBoxButton('取消');
    await flushPromises();

    const call = fetchMock.mock.calls.find((c) => (c[1]?.method ?? 'GET') === 'DELETE');
    expect(call).toBeFalsy();
  });

  it('重新生成（roll）后弹出新的明文弹窗', async () => {
    const wrapper = await mountPanel();

    const rollBtn = rowButton(wrapper, 0, '重新生成');
    expect(rollBtn).toBeTruthy();
    await rollBtn!.trigger('click');
    await flushPromises();

    // roll 前需二次确认
    expect(document.body.textContent).toContain('旧密钥立即失效');
    clickMessageBoxButton('重新生成');
    await flushPromises();
    await flushPromises();

    const call = fetchMock.mock.calls.find((c) => String(c[0]).endsWith('/roll'));
    expect(call).toBeTruthy();
    const boxText = document.body.textContent ?? '';
    expect(boxText).toContain(PLAIN_KEY);
    expect(boxText).toContain(PLAIN_WARNING);
  });
});
