/**
 * 端口配置相关类型定义
 */

/** 端口配置数据 */
export interface PortConfig {
  /** 端口号 */
  portNumber: number;
  /** 消息 */
  message?: string;
}

/** 端口检查结果 */
export interface PortCheckResult {
  /** 端口号 */
  port: number;
  /** 是否可用 */
  available: boolean;
  /** 不可用时的错误信息 */
  message?: string;
}

/** 服务重启状态 */
export interface RestartStatus {
  /** 是否正在重启 */
  isRestarting: boolean;
  /** 重启开始时间 */
  startTime?: string;
  /** 新端口 */
  newPort?: number;
}
