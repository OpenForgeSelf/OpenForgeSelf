import { request } from './request';

const API_BASE = '/api/update';

/** 版本检查结果（与后端 UpdateCheckResult 对齐；Version 序列化为字符串） */
export interface UpdateCheckResult {
  currentVersion?: string | null;
  latestVersion?: string | null;
  latestVersionTag?: string | null;
  hasUpdate: boolean;
  downloadUrl?: string | null;
  releaseNotes?: string | null;
  packageHash?: string | null;
  packageSize: number;
  checkTime?: string;
  errorMessage?: string | null;
  isSuccess: boolean;
}

/** 暂存状态（与后端 UpdateStageInfo 对齐） */
export interface UpdateStageInfo {
  /** idle | checking | checked | downloading | verifying | extracting | ready | applying | failed */
  status: string;
  progress: number;
  message?: string | null;
  tag?: string | null;
  stagedDir?: string | null;
  check?: UpdateCheckResult | null;
  updatedAt?: string;
}

export interface UpdateStatus {
  currentVersion: string;
  provider: string;
  githubRepo: string;
  channel: string;
  state: UpdateStageInfo;
}

/**
 * 自动更新 API（spec 036）。
 * 检查 → 下载（暂存）→ 轮询进度 → 重启并更新。
 */
export const updateApi = {
  async getStatus(): Promise<UpdateStatus> {
    const json = await request(`${API_BASE}/status`);
    return json.data as UpdateStatus;
  },

  async check(): Promise<UpdateCheckResult> {
    const json = await request(`${API_BASE}/check`, { method: 'POST' });
    return json.data as UpdateCheckResult;
  },

  async download(): Promise<UpdateStageInfo> {
    const json = await request(`${API_BASE}/download`, { method: 'POST' });
    if (json.success === false) {
      throw new Error(json.error || '启动下载失败');
    }
    return json.data as UpdateStageInfo;
  },

  async getProgress(): Promise<UpdateStageInfo> {
    const json = await request(`${API_BASE}/progress`);
    return json.data as UpdateStageInfo;
  },

  /** 应用已暂存的更新：宿主进程随即退出，由更新代理换文件并重启 */
  async apply(): Promise<UpdateStageInfo> {
    const json = await request(`${API_BASE}/apply`, { method: 'POST' });
    if (json.success === false) {
      throw new Error(json.error || '应用更新失败');
    }
    return json.data as UpdateStageInfo;
  },
};
