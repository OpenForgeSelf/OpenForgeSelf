import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { mount, flushPromises } from '@vue/test-utils';
import AboutPanel from '@/components/settings/AboutPanel.vue';

function jsonResponse(data: unknown, ok = true, status = 200): Response {
  return { ok, status, json: async () => ({ data }) } as unknown as Response;
}

function mountPanel() {
  return mount(AboutPanel);
}

describe('AboutPanel', () => {
  let fetchMock: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('挂载取数前版本行显示占位 v…', () => {
    fetchMock.mockReturnValue(new Promise(() => {}));
    const wrapper = mountPanel();
    const code = wrapper.find('code');
    expect(code.exists()).toBe(true);
    expect(code.text()).toBe('v…');
  });

  it('接口成功展示真实版本（与 UpdatePanel 同源 currentVersion）', async () => {
    fetchMock.mockResolvedValue(jsonResponse({ currentVersion: '2.4.0.2610101148', state: { status: 'idle', progress: 0 } }));
    const wrapper = mountPanel();
    await flushPromises();
    const code = wrapper.find('code');
    expect(code.text()).toBe('v2.4.0.2610101148');
  });

  it('接口拒绝时优雅降级为 v未知，且不向外抛错', async () => {
    fetchMock.mockRejectedValue(new Error('请求失败(500)'));
    const wrapper = mountPanel();
    await flushPromises();
    expect(wrapper.find('code').text()).toBe('v未知');
  });

  it('接口返回空版本号同样降级为 v未知', async () => {
    fetchMock.mockResolvedValue(jsonResponse({ currentVersion: '' }));
    const wrapper = mountPanel();
    await flushPromises();
    expect(wrapper.find('code').text()).toBe('v未知');
  });
});
