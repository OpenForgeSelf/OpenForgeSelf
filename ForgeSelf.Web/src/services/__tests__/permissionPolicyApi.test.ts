import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  deleteProfile,
  listProfiles,
  queryAudit,
  saveProfile
} from '../permissionPolicyApi';
import type { PermissionProfile } from '../permissionPolicyApi';
import { setStoredToken } from '../request';

function jsonResponse(data: unknown, status = 200): Response {
  return new Response(JSON.stringify(data), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });
}

function errorResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });
}

function profile(overrides: Partial<PermissionProfile> = {}): PermissionProfile {
  return {
    id: 1,
    agentId: '',
    enabled: true,
    rules: [{ toolName: '*', effect: 'Ask', pathAllowlist: [], domainAllowlist: [], commandAllowlist: [] }],
    remark: null,
    updatedAt: '2026-11-02T22:00:00+08:00',
    ...overrides
  };
}

describe('permissionPolicyApi', () => {
  beforeEach(() => {
    setStoredToken('sk-test');
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  describe('listProfiles', () => {
    it('解析裸数组并附带 Authorization 头（数值 effect 归一为字面量）', async () => {
      const fetchMock = vi.fn(async (url: unknown, init?: RequestInit) => {
        expect(String(url)).toBe('/api/permissions/profiles');
        expect((init?.headers as Record<string, string>).Authorization).toBe('Bearer sk-test');
        return jsonResponse([
          { id: 1, agentId: '', enabled: true, rules: [{ toolName: '*', effect: 2, pathAllowlist: [], domainAllowlist: [], commandAllowlist: [] }], remark: null, updatedAt: '' },
          { id: 2, agentId: 'coder', enabled: true, rules: [], remark: null, updatedAt: '' }
        ]);
      });
      vi.stubGlobal('fetch', fetchMock);

      const list = await listProfiles();
      expect(list).toHaveLength(2);
      expect(list[0].rules[0].effect).toBe('Deny'); // 数值 2 → 'Deny'
      expect(list[1].agentId).toBe('coder');
    });

    it('空数组原样返回（零配置兼容红线：不合成数据）', async () => {
      vi.stubGlobal('fetch', vi.fn(async () => jsonResponse([])));
      expect(await listProfiles()).toEqual([]);
    });

    it('503 → 权限服务未就绪文案（面板错误态锚点）', async () => {
      vi.stubGlobal('fetch', vi.fn(async () => new Response('Service Unavailable', { status: 503 })));
      await expect(listProfiles()).rejects.toThrow('权限策略服务未就绪，请升级宿主');
    });

    it('其他错误透出后端 error 原文', async () => {
      vi.stubGlobal('fetch', vi.fn(async () => errorResponse(500, { error: '数据库不可用' })));
      await expect(listProfiles()).rejects.toThrow('数据库不可用');
    });
  });

  describe('saveProfile', () => {
    it('id>0 → PUT /profiles/{id}，提交体 effect 转数值枚举（线上契约）', async () => {
      const target = profile({ id: 7, rules: [{ toolName: '*', effect: 'Ask', pathAllowlist: [], domainAllowlist: [], commandAllowlist: [] }] });
      const fetchMock = vi.fn(async (url: unknown, init?: RequestInit) => {
        expect(String(url)).toBe('/api/permissions/profiles/7');
        expect(init?.method).toBe('PUT');
        const body = JSON.parse(String(init?.body)) as { agentId: string; updatedAt: string; rules: Array<{ effect: number }> };
        expect(body.agentId).toBe('');
        expect(body.rules[0].effect).toBe(1); // Ask → 1（MVC 无 JsonStringEnumConverter，线上为数值）
        expect(body.updatedAt).not.toBe(''); // 空串已补当前时刻
        return jsonResponse({ id: 7, agentId: '', enabled: true, rules: [{ toolName: '*', effect: 1, pathAllowlist: [], domainAllowlist: [], commandAllowlist: [] }], remark: null, updatedAt: '2026-11-02T23:00:00+08:00' });
      });
      vi.stubGlobal('fetch', fetchMock);

      const saved = await saveProfile(target);
      expect(saved.updatedAt).toBe('2026-11-02T23:00:00+08:00');
      expect(saved.rules[0].effect).toBe('Ask'); // 响应数值 → 归一为字面量
    });

    it('id=0 → PUT /profiles/0（id=0 新建，U6 对表实锤）', async () => {
      const draft = profile({ id: 0, agentId: 'coder' });
      const fetchMock = vi.fn(async (url: unknown) => {
        expect(String(url)).toBe('/api/permissions/profiles/0');
        return jsonResponse({ id: 9, agentId: 'coder', enabled: true, rules: [{ toolName: '*', effect: 1, pathAllowlist: [], domainAllowlist: [], commandAllowlist: [] }], remark: null, updatedAt: '' });
      });
      vi.stubGlobal('fetch', fetchMock);

      const saved = await saveProfile(draft);
      expect(saved.id).toBe(9);
      expect(saved.rules[0].effect).toBe('Ask');
    });

    it('409 AgentId 冲突 → 逐字透出后端 error', async () => {
      vi.stubGlobal('fetch', vi.fn(async () => errorResponse(409, { error: 'AgentId「coder」已存在策略配置' })));
      await expect(saveProfile(profile({ id: 0, agentId: 'coder' }))).rejects.toThrow('AgentId「coder」已存在策略配置');
    });
  });

  describe('deleteProfile', () => {
    it('200 deleted → 正常返回', async () => {
      const fetchMock = vi.fn(async (url: unknown, init?: RequestInit) => {
        expect(String(url)).toBe('/api/permissions/profiles/3');
        expect(init?.method).toBe('DELETE');
        return jsonResponse({ deleted: true });
      });
      vi.stubGlobal('fetch', fetchMock);
      await expect(deleteProfile(3)).resolves.toBeUndefined();
    });

    it('404 幂等返回不抛错', async () => {
      vi.stubGlobal('fetch', vi.fn(async () => new Response('Not Found', { status: 404 })));
      await expect(deleteProfile(999)).resolves.toBeUndefined();
    });

    it('500 → 抛后端原文', async () => {
      vi.stubGlobal('fetch', vi.fn(async () => errorResponse(500, { message: '删除失败' })));
      await expect(deleteProfile(1)).rejects.toThrow('删除失败');
    });
  });

  describe('queryAudit', () => {
    it('拼接 toolName/kind/page/pageSize 并解析裸封套', async () => {
      const fetchMock = vi.fn(async (url: unknown) => {
        const u = new URL(String(url), 'http://localhost');
        expect(u.pathname).toBe('/api/permissions/audit');
        expect(u.searchParams.get('toolName')).toBe('write_file');
        expect(u.searchParams.get('kind')).toBe('policy-decision');
        expect(u.searchParams.get('page')).toBe('2');
        expect(u.searchParams.get('pageSize')).toBe('50');
        return jsonResponse({
          items: [
            {
              id: 1,
              kind: 'policy-decision',
              time: '2026-11-02T22:59:00+08:00',
              sessionId: null,
              agentId: 'coder',
              toolName: 'write_file',
              argsHash: 'ab12',
              status: 'Deny',
              source: 'profile:1',
              reason: '命中 Deny 规则',
              approvalId: null
            }
          ],
          total: 1,
          page: 2,
          pageSize: 50
        });
      });
      vi.stubGlobal('fetch', fetchMock);

      const data = await queryAudit({ toolName: 'write_file', kind: 'policy-decision', page: 2, pageSize: 50 });
      expect(data.items).toHaveLength(1);
      expect(data.items[0].status).toBe('Deny');
      expect(data.total).toBe(1);
    });

    it('page/pageSize 本地钳位（page≥1、pageSize 1..200）', async () => {
      const fetchMock = vi.fn(async (url: unknown) => {
        const u = new URL(String(url), 'http://localhost');
        expect(u.searchParams.get('page')).toBe('1');
        expect(u.searchParams.get('pageSize')).toBe('200');
        return jsonResponse({ items: [], total: 0, page: 1, pageSize: 200 });
      });
      vi.stubGlobal('fetch', fetchMock);

      await queryAudit({ page: 0, pageSize: 500 });
    });

    it('400 非法 kind → 透出后端 error', async () => {
      vi.stubGlobal('fetch', vi.fn(async () => errorResponse(400, { error: '非法 kind' })));
      await expect(queryAudit({ kind: 'bogus' as never })).rejects.toThrow('非法 kind');
    });

    it('封套字段缺失 → 保守默认值不崩', async () => {
      vi.stubGlobal('fetch', vi.fn(async () => jsonResponse({})));
      const data = await queryAudit({});
      expect(data.items).toEqual([]);
      expect(data.total).toBe(0);
      expect(data.page).toBe(1);
      expect(data.pageSize).toBe(50);
    });
  });
});
