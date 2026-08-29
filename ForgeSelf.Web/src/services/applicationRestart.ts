/**
 * 应用重启轮询服务
 * 用于在端口更改后轮询服务直到新端口就绪
 */

const HEALTH_ENDPOINT = '/api/health';
const DEFAULT_POLL_INTERVAL = 1000; // 1 秒
const DEFAULT_POLL_TIMEOUT = 60000; // 60 秒

/**
 * 轮询服务重启直到新端口就绪
 * @param newPort 新端口号
 * @param options 轮询选项
 * @returns 成功返回 true，超时返回 false
 */
export async function pollForRestart(
  newPort: number,
  options: {
    pollInterval?: number;
    pollTimeout?: number;
    onRetry?: (attempt: number, elapsed: number) => void;
  } = {}
): Promise<boolean> {
  const {
    pollInterval = DEFAULT_POLL_INTERVAL,
    pollTimeout = DEFAULT_POLL_TIMEOUT,
    onRetry,
  } = options;

  const startTime = Date.now();
  let attempt = 0;

  return new Promise((resolve) => {
    const checkHealth = async () => {
      attempt++;
      const elapsed = Date.now() - startTime;

      // 检查是否超时
      if (elapsed >= pollTimeout) {
        console.warn(`[RestartPoll] 超时 (${pollTimeout}ms)，放弃轮询`);
        resolve(false);
        return;
      }

      try {
        // 尝试访问新端口的健康检查接口
        const url = `http://localhost:${newPort}${HEALTH_ENDPOINT}`;
        const response = await fetch(url, {
          method: 'GET',
          cache: 'no-cache',
        });

        if (response.ok) {
          console.log(`[RestartPoll] 服务已在新端口 ${newPort} 就绪`);
          resolve(true);
          return;
        }
      } catch (error) {
        // 网络错误，服务还未就绪，继续轮询
        console.debug(`[RestartPoll] 第 ${attempt} 次尝试失败：${error instanceof Error ? error.message : '未知错误'}`);
      }

      // 调用回调
      if (onRetry) {
        onRetry(attempt, elapsed);
      }

      // 等待下一次轮询
      setTimeout(checkHealth, pollInterval);
    };

    // 开始轮询
    checkHealth();
  });
}

/**
 * 格式化轮询耗时
 */
export function formatPollDuration(ms: number): string {
  const seconds = Math.floor(ms / 1000);
  if (seconds < 60) {
    return `${seconds}秒`;
  }
  const minutes = Math.floor(seconds / 60);
  const remainingSeconds = seconds % 60;
  return `${minutes}分${remainingSeconds}秒`;
}
