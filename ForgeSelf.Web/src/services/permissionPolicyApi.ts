/**
 * 权限策略 API 层 — B4 权限可配置化（契约冻结版，verify-dev 2026-11-02）
 * 端点：/api/permissions/*（宿主控制器，需 API token；裸封套，无 success/data 包装）
 * 字面量：effect = "Allow" | "Ask" | "Deny"（PascalCase，契约原文）
 */
import { getStoredToken } from './request';

export type PermissionEffect = 'Allow' | 'Ask' | 'Deny';

export type PermissionAuditKind = 'policy-decision' | 'tool-approval';

/** 单工具（或 "*" 兜底）规则；白名单口径（路径/命令=前缀、域名=含子域）由后端解释 */
export interface ToolPermissionRule {
  toolName: string;
  effect: PermissionEffect;
  pathAllowlist: string[];
  domainAllowlist: string[];
  commandAllowlist: string[];
}

/** 权限策略：agentId 空串 = 全局默认（至多一张，重复后端 409） */
export interface PermissionProfile {
  id: number;
  agentId: string;
  enabled: boolean;
  rules: ToolPermissionRule[];
  remark: string | null;
  updatedAt: string;
}

export interface PermissionAuditItem {
  id: number;
  kind: PermissionAuditKind;
  time: string;
  sessionId: string | null;
  agentId: string | null;
  toolName: string;
  argsHash: string | null;
  /** 策略决策 = Allow/Ask/Deny；人工审批 = Approved/Denied/Expired/Timeout */
  status: string;
  /** "default-policy" | "profile:{id}" | 风险级 */
  source: string;
  reason: string | null;
  approvalId: number | null;
}

export interface PermissionAuditQuery {
  toolName?: string;
  sessionId?: string;
  kind?: PermissionAuditKind;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

export interface PermissionAuditPage {
  items: PermissionAuditItem[];
  total: number;
  page: number;
  pageSize: number;
}

const BASE = '/api/permissions';

/**
 * effect 线上格式映射（U2 对表实锤）：宿主 MVC 无全局 JsonStringEnumConverter，
 * HTTP 线上契约 = 数值枚举（Allow=0 / Ask=1 / Deny=2）；字符串字面量仅是 service 层持久化格式。
 * API 层集中映射，UI 恒用字符串字面量。
 */
const EFFECTS_BY_NUM: readonly PermissionEffect[] = ['Allow', 'Ask', 'Deny'];
const EFFECT_TO_NUM: Record<PermissionEffect, number> = { Allow: 0, Ask: 1, Deny: 2 };

function normalizeEffect(value: unknown): PermissionEffect {
  if (typeof value === 'number') return EFFECTS_BY_NUM[value] ?? 'Ask';
  if (typeof value === 'string' && value in EFFECT_TO_NUM) return value as PermissionEffect;
  return 'Ask';
}

/** 线上提交体：effect 转数值；updatedAt 空串补当前时刻（后端 DateTimeOffset 反序列化不接受空串） */
function toWire(profile: PermissionProfile): Record<string, unknown> {
  return {
    ...profile,
    updatedAt: profile.updatedAt || new Date().toISOString(),
    rules: profile.rules.map((r) => ({ ...r, effect: EFFECT_TO_NUM[r.effect] }))
  };
}

interface WireProfile {
  id: number;
  agentId: string;
  enabled: boolean;
  rules: Array<{ toolName: string; effect: unknown; pathAllowlist: string[]; domainAllowlist: string[]; commandAllowlist: string[] }>;
  remark: string | null;
  updatedAt: string;
}

function fromWire(dto: WireProfile): PermissionProfile {
  return {
    ...dto,
    rules: (dto.rules ?? []).map((r) => ({ ...r, effect: normalizeEffect(r.effect) }))
  };
}

/** 从错误响应体提取后端原文（409 的 {"error":…} 逐字透出；其余按常见字段回退） */
async function extractError(res: Response): Promise<string> {
  try {
    const text = await res.text();
    if (text) {
      try {
        const json = JSON.parse(text) as { error?: string; message?: string; title?: string; detail?: string };
        const detail = json.error || json.message || json.title || json.detail;
        if (detail) return detail;
      } catch {
        return text;
      }
    }
  } catch {
    /* body 不可读时走状态行 */
  }
  return `请求失败(${res.status})`;
}

async function authHeaders(): Promise<Record<string, string>> {
  const headers: Record<string, string> = { 'Content-Type': 'application/json' };
  const token = getStoredToken();
  if (token) headers.Authorization = `Bearer ${token}`;
  return headers;
}

/** 策略列表；503 = 宿主权限服务缺席（旧后端）→ 面板错误态文案 */
export async function listProfiles(): Promise<PermissionProfile[]> {
  const res = await fetch(`${BASE}/profiles`, { headers: await authHeaders() });
  if (res.status === 503) throw new Error('权限策略服务未就绪，请升级宿主');
  if (!res.ok) throw new Error(await extractError(res));
  const data = (await res.json()) as WireProfile[];
  return Array.isArray(data) ? data.map(fromWire) : [];
}

/** 保存策略：PUT /profiles/{id}（id=0 新建，id>0 更新/upsert——控制器路由 {id:long} 必填）。409 原文抛出 */
export async function saveProfile(profile: PermissionProfile): Promise<PermissionProfile> {
  const path = `${BASE}/profiles/${profile.id > 0 ? profile.id : 0}`;
  const res = await fetch(path, {
    method: 'PUT',
    headers: await authHeaders(),
    body: JSON.stringify(toWire(profile))
  });
  if (!res.ok) throw new Error(await extractError(res));
  return fromWire((await res.json()) as WireProfile);
}

/** 删除策略（回落内置默认）；404 视为已删除（幂等返回） */
export async function deleteProfile(id: number): Promise<void> {
  const res = await fetch(`${BASE}/profiles/${id}`, { method: 'DELETE', headers: await authHeaders() });
  if (res.status === 404) return;
  if (!res.ok) throw new Error(await extractError(res));
}

function clampInt(value: number | undefined, min: number, max: number, fallback: number): number {
  if (value == null || Number.isNaN(value)) return fallback;
  return Math.min(max, Math.max(min, Math.trunc(value)));
}

/** 审计流水：后端分页（裸封套 items/total/page/pageSize）；参数本地钳位（page≥1、pageSize 1..200） */
export async function queryAudit(query: PermissionAuditQuery): Promise<PermissionAuditPage> {
  const params = new URLSearchParams();
  if (query.toolName?.trim()) params.set('toolName', query.toolName.trim());
  if (query.sessionId?.trim()) params.set('sessionId', query.sessionId.trim());
  if (query.kind) params.set('kind', query.kind);
  if (query.from?.trim()) params.set('from', query.from.trim());
  if (query.to?.trim()) params.set('to', query.to.trim());
  params.set('page', String(clampInt(query.page, 1, Number.MAX_SAFE_INTEGER, 1)));
  params.set('pageSize', String(clampInt(query.pageSize, 1, 200, 50)));

  const res = await fetch(`${BASE}/audit?${params.toString()}`, { headers: await authHeaders() });
  if (!res.ok) throw new Error(await extractError(res));
  const data = (await res.json()) as Partial<PermissionAuditPage>;
  return {
    items: Array.isArray(data.items) ? data.items : [],
    total: typeof data.total === 'number' ? data.total : 0,
    page: typeof data.page === 'number' ? data.page : 1,
    pageSize: typeof data.pageSize === 'number' ? data.pageSize : 50
  };
}
