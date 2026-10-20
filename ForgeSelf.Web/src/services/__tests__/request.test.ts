import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { request, setStoredToken } from '@/services/request';

/**
 * request() 空响应体守卫（2026-10-18 T020 e2e 缺陷）：
 * DELETE /api/ai-providers/{id} 返回 204 No Content（空 body），旧实现无条件
 * res.json() 抛 SyntaxError，被 deleteProvider 的空 catch 当「用户取消」吞掉，
 * 造成后端已删但列表不刷新、无提示。插件侧 AIAgent web/http.ts 已修同源缺陷，
 * 本文件守卫宿主侧不再回退。
 */
describe('request · 204 空响应体', () => {
  beforeEach(() => setStoredToken('sk-test'));
  afterEach(() => vi.unstubAllGlobals());

  function stubResponse(init: { status: number; body?: string; headers?: Record<string, string> }) {
    const res = {
      ok: init.status >= 200 && init.status < 300,
      status: init.status,
      statusText: '',
      headers: new Headers(init.headers ?? {}),
      json: vi.fn(async () => {
        if (init.body === undefined) throw new SyntaxError('Unexpected end of JSON input');
        return JSON.parse(init.body);
      }),
    };
    vi.stubGlobal('fetch', vi.fn(async () => res));
    return res;
  }

  it('204 → 返回 undefined，不调用 res.json()（修复回退守卫）', async () => {
    const res = stubResponse({ status: 204 });
    await expect(request('/api/ai-providers/1', { method: 'DELETE' })).resolves.toBeUndefined();
    expect(res.json).not.toHaveBeenCalled();
  });

  it('200 JSON → 正常解包', async () => {
    stubResponse({ status: 200, body: JSON.stringify({ success: true, data: { id: 1 } }) });
    const json = await request<any>('/api/ai-providers/1');
    expect(json.data.id).toBe(1);
  });

  it('400 带 message → 抛错并透传后端信息', async () => {
    stubResponse({ status: 400, body: JSON.stringify({ success: false, message: '无法删除唯一的默认提供方' }) });
    await expect(request('/api/ai-providers/1', { method: 'DELETE' })).rejects.toThrow('无法删除唯一的默认提供方');
  });
});
