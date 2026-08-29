import type { ApiServerConfig } from '@/types/apiServer';
import { request, setStoredToken, getStoredToken } from './request';

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

  /**
   * 初始化 API 密钥
   * @returns 返回新密钥，如果已有密钥则返回 null
   */
  async initToken(): Promise<string | null> {
    // 先检查本地是否有 token
    const existingToken = getStoredToken();
    if (existingToken) {
      return null; // 已有 token，不需要初始化
    }

    // 没有 token，尝试请求初始化（GET 方法）
    try {
      const json = await request(`${API_BASE}/init-token`);
      const config = json.data as ApiServerConfig;
      if (config?.apiKeyPlain) {
        setStoredToken(config.apiKeyPlain);
        return config.apiKeyPlain;
      }
      return null;
    } catch (error) {
      // 如果是 403 错误，说明需要手动输入 token
      if (error instanceof Error && error.message.includes('403')) {
        // eslint-disable-next-line preserve-caught-error
        throw new Error('NEED_MANUAL_TOKEN');
      }
      throw error;
    }
  },

  /** 重启 API 服务器（用于端口更改后） */
  async restart(): Promise<void> {
    await request(`${API_BASE}/restart`, { method: 'POST' });
  },
};
