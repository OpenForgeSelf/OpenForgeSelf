import type { PortConfig, PortCheckResult } from '@/types/portConfig';
import { request } from './request';

const API_BASE = '/api/portconfiguration';

/**
 * 端口配置 API 客户端
 * 提供端口查询、更新和可用性检查功能
 */
export const portConfigApi = {
  /** 获取当前端口配置 */
  async getPortConfig(): Promise<PortConfig> {
    const json = await request<any>(`${API_BASE}`);
    return json.data as PortConfig;
  },

  /** 更新端口配置 */
  async updatePortConfig(port: number): Promise<PortConfig> {
    const json = await request<any>(`${API_BASE}`, {
      method: 'POST',
      body: JSON.stringify({ PortNumber: port }),
    });
    return json.data as PortConfig;
  },

  /** 检查端口是否可用 */
  async checkPortAvailable(port: number): Promise<PortCheckResult> {
    const json = await request<any>(`${API_BASE}/check/${port}`);
    const data = json.data as { portNumber: number; isAvailable: boolean; message?: string };
    return {
      port: data.portNumber,
      available: data.isAvailable,
      message: data.message,
    } as PortCheckResult;
  },
};
