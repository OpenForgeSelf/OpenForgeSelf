/**
 * API 子密钥管理 API 封装（对应后端 /api/api-keys，全部需要 ApiKeyPolicy 鉴权）。
 * 请求统一经 request() 携带 Authorization 头。
 *
 * 明文红线：只有 create / roll 的返回值含明文，list 永远只有掩码。
 */
import type {
  ApiKeyItem,
  ApiKeyPlainResult,
  CreateApiKeyRequest,
  DeleteApiKeyResult,
  ToggleApiKeyRequest,
  UpdateApiKeyRequest,
} from '@/types/apiKey';
import { request } from './request';

const API_BASE = '/api/api-keys';

export const apiKeysApi = {
  /** 获取子密钥列表（仅掩码，永不含明文） */
  async list(): Promise<ApiKeyItem[]> {
    const json = await request(API_BASE);
    return (json.data ?? []) as ApiKeyItem[];
  },

  /** 创建子密钥（唯一一次返回明文） */
  async create(payload: CreateApiKeyRequest): Promise<ApiKeyPlainResult> {
    const json = await request(API_BASE, {
      method: 'POST',
      body: JSON.stringify(payload),
    });
    return json.data as ApiKeyPlainResult;
  },

  /** 更新子密钥（重命名 / 备注 / 过期时间），不存在时后端返回 404 */
  async update(id: number, payload: UpdateApiKeyRequest): Promise<ApiKeyItem> {
    const json = await request(`${API_BASE}/${id}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    });
    return json.data as ApiKeyItem;
  },

  /** 启用 / 停用子密钥（停用后立即失效，可再启用） */
  async toggle(id: number, enabled: boolean): Promise<ApiKeyItem> {
    const payload: ToggleApiKeyRequest = { enabled };
    const json = await request(`${API_BASE}/${id}/toggle`, {
      method: 'POST',
      body: JSON.stringify(payload),
    });
    return json.data as ApiKeyItem;
  },

  /** 重新生成子密钥明文（旧值立即失效，唯一一次返回新明文） */
  async roll(id: number): Promise<ApiKeyPlainResult> {
    const json = await request(`${API_BASE}/${id}/roll`, { method: 'POST' });
    return json.data as ApiKeyPlainResult;
  },

  /** 删除子密钥（硬删除，立即失效），不存在时后端返回 404 */
  async remove(id: number): Promise<DeleteApiKeyResult> {
    const json = await request(`${API_BASE}/${id}`, { method: 'DELETE' });
    return (json.data ?? { deleted: true }) as DeleteApiKeyResult;
  },
};
