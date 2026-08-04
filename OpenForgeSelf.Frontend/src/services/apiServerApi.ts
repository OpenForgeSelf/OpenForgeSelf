import type { ApiServerConfig } from '@/types/apiServer';
import { request, getStoredToken, setStoredToken, STORAGE_KEY } from './request';

const API_BASE = '/api/api-server';

/**
 * API 服务器管理 API 封装。
 * 提供状态查询与密钥轮换，请求自动携带 Authorization 头。
 */
export const apiServerApi = {
  /** 获取 API 服务器配置（API 地址、密钥、授权标头） */
  async getConfig(): Promise<ApiServerConfig> {
    const json = await request(`${API_BASE}/status`);
    return json.data as ApiServerConfig;
  },

  /** 重新生成 API 密钥（旧密钥立即失效） */
  async regenerateKey(): Promise<ApiServerConfig> {
    const json = await request(`${API_BASE}/regenerate`, { method: 'POST' });
    // 重新生成后同步更新 localStorage 中的 token
    const config = json.data as ApiServerConfig;
    if (config.apiKeyPlain) {
      setStoredToken(config.apiKeyPlain);
    }
    return config;
  },
};
