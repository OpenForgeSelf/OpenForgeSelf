<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue';
import { ElMessage, ElMessageBox } from 'element-plus';
import { updateApi } from '@/services/updateApi';
import type { UpdateCheckResult, UpdateStageInfo, UpdateStatus } from '@/services/updateApi';

const status = ref<UpdateStatus | null>(null);
const checkResult = ref<UpdateCheckResult | null>(null);
const stage = ref<UpdateStageInfo | null>(null);

const checking = ref(false);
const downloading = ref(false);
const applying = ref(false);

let pollTimer: number | null = null;

const statusText: Record<string, string> = {
  idle: '空闲',
  checking: '检查中',
  checked: '发现新版本',
  downloading: '下载中',
  verifying: '校验中',
  extracting: '解压中',
  ready: '已就绪',
  applying: '重启中',
  failed: '失败'
};

function formatSize(bytes: number): string {
  if (!bytes || bytes <= 0) return '-';
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

function busy(): boolean {
  const s = stage.value?.status ?? status.value?.state?.status;
  return s === 'checking' || s === 'downloading' || s === 'verifying' || s === 'extracting' || s === 'applying';
}

const isReady = () => (stage.value?.status ?? status.value?.state?.status) === 'ready';

async function loadStatus() {
  try {
    status.value = await updateApi.getStatus();
    stage.value = status.value.state;
    if (status.value.state.check) checkResult.value = status.value.state.check;
  } catch (e) {
    ElMessage.error({ message: `加载更新状态失败: ${e instanceof Error ? e.message : e}`, offset: 60 });
  }
}

async function onCheck() {
  checking.value = true;
  try {
    const result = await updateApi.check();
    checkResult.value = result;
    stage.value = result.isSuccess && result.hasUpdate
      ? { ...(stage.value ?? { progress: 0 }), status: 'checked', message: '发现新版本，可点击下载' }
      : { ...(stage.value ?? { progress: 0 }), status: 'idle', message: result.isSuccess ? '已是最新版本' : result.errorMessage };
    if (!result.isSuccess) {
      ElMessage.warning({ message: result.errorMessage || '检查更新失败', offset: 60 });
    } else if (result.hasUpdate) {
      ElMessage.success({ message: `发现新版本 ${result.latestVersionTag ?? result.latestVersion}`, offset: 60 });
    } else {
      ElMessage.info({ message: '已是最新版本', offset: 60 });
    }
  } catch (e) {
    ElMessage.error({ message: `检查更新失败: ${e instanceof Error ? e.message : e}`, offset: 60 });
  } finally {
    checking.value = false;
  }
}

async function onDownload() {
  downloading.value = true;
  try {
    stage.value = await updateApi.download();
    startPolling();
  } catch (e) {
    ElMessage.error({ message: `启动下载失败: ${e instanceof Error ? e.message : e}`, offset: 60 });
  } finally {
    downloading.value = false;
  }
}

function startPolling() {
  stopPolling();
  pollTimer = window.setInterval(async () => {
    try {
      const s = await updateApi.getProgress();
      stage.value = s;
      if (!['checking', 'downloading', 'verifying', 'extracting'].includes(s.status)) {
        stopPolling();
        if (s.status === 'ready') {
          ElMessage.success({ message: '更新已就绪，点击「重启并更新」完成升级', offset: 60 });
        } else if (s.status === 'failed') {
          ElMessage.error({ message: s.message || '下载更新失败', offset: 60 });
        }
      }
    } catch {
      // 服务暂不可达时静默重试（下载期间宿主仍在线，此处防御网络抖动）
    }
  }, 2000);
}

function stopPolling() {
  if (pollTimer !== null) {
    window.clearInterval(pollTimer);
    pollTimer = null;
  }
}

async function onApply() {
  try {
    await ElMessageBox.confirm(
      '应用将退出并由更新代理完成文件替换后自动重启，通常需要十几秒。确定现在重启并更新吗？',
      '重启并更新',
      { confirmButtonText: '重启并更新', cancelButtonText: '稍后', type: 'warning' }
    );
  } catch {
    return;
  }

  applying.value = true;
  try {
    stage.value = await updateApi.apply();
    waitForRestart();
  } catch (e) {
    applying.value = false;
    ElMessage.error({ message: `应用更新失败: ${e instanceof Error ? e.message : e}`, offset: 60 });
  }
}

/** 重启等待：轮询后端恢复，恢复后刷新页面 */
function waitForRestart() {
  stopPolling();
  let gone = false;
  pollTimer = window.setInterval(async () => {
    try {
      const res = await fetch('/api/update/progress', { method: 'GET', cache: 'no-store' });
      if (res.ok && gone) {
        stopPolling();
        ElMessage.success({ message: '更新完成，应用已重启', offset: 60 });
        window.location.reload();
      } else if (!res.ok) {
        gone = true;
      }
    } catch {
      gone = true; // 连接失败 = 宿主已退出，进入等待
    }
  }, 3000);
}

onMounted(async () => {
  await loadStatus();
  const s = stage.value?.status;
  if (s && ['checking', 'downloading', 'verifying', 'extracting'].includes(s)) {
    startPolling();
  }
});

onBeforeUnmount(stopPolling);
</script>

<template>
  <div class="space-y-6">
    <!-- 页头 -->
    <div class="flex items-start justify-between pb-4 border-b border-border">
      <div>
        <h2 class="text-xl font-bold text-text m-0 leading-tight">版本更新</h2>
        <p class="text-sm text-text-secondary mt-1 mb-0">
          从 {{ status?.provider === 'github' ? `GitHub Releases（${status.githubRepo || '未配置仓库'}）` : '更新服务器' }}
          检查并安装更新
        </p>
      </div>
    </div>

    <el-card shadow="never">
      <!-- 当前版本 -->
      <div class="flex items-center justify-between">
        <div>
          <div class="text-sm text-text-regular">当前版本</div>
          <div class="text-lg font-semibold text-text mt-1">v{{ status?.currentVersion ?? '…' }}</div>
          <div v-if="status && !status.githubTokenConfigured && status.provider === 'github'" class="text-xs text-warning mt-1">
            未配置 GitHub 访问令牌：私有仓库将检查失败（可设置环境变量 FORGESELF_UPDATE_TOKEN）
          </div>
        </div>
        <el-button :loading="checking || busy()" @click="onCheck">检查更新</el-button>
      </div>
    </el-card>

    <!-- 更新结果 -->
    <el-card v-if="checkResult && checkResult.hasUpdate" shadow="never">
      <div class="space-y-4">
        <div class="flex items-center justify-between">
          <div>
            <div class="text-base font-medium text-text">
              发现新版本 {{ checkResult.latestVersionTag ?? checkResult.latestVersion }}
            </div>
            <div class="text-xs text-text-secondary mt-1">
              包大小 {{ formatSize(checkResult.packageSize) }}
              <span v-if="checkResult.packageHash"> · SHA256 校验已启用</span>
            </div>
          </div>
          <el-button
            type="primary"
            :loading="downloading || busy()"
            :disabled="isReady()"
            @click="onDownload">
            {{ isReady() ? '已下载' : '下载更新' }}
          </el-button>
        </div>

        <!-- 下载进度 -->
        <div v-if="stage && ['downloading', 'verifying', 'extracting', 'checked'].includes(stage.status)">
          <div class="flex items-center justify-between text-xs text-text-regular mb-1">
            <span>{{ stage.message ?? statusText[stage.status] ?? stage.status }}</span>
            <span>{{ stage.progress }}%</span>
          </div>
          <el-progress :percentage="stage.progress" :stroke-width="8" :show-text="false" />
        </div>

        <!-- 就绪 → 重启并更新 -->
        <div v-if="stage?.status === 'ready' || isReady()" class="flex items-center justify-between">
          <span class="text-sm text-success">更新已就绪，重启后生效</span>
          <el-button type="success" :loading="applying" @click="onApply">重启并更新</el-button>
        </div>

        <!-- 失败 -->
        <div v-if="stage?.status === 'failed'" class="text-sm text-error">
          {{ stage.message ?? '更新失败' }}
        </div>

        <!-- 更新说明 -->
        <div v-if="checkResult.releaseNotes">
          <div class="text-sm font-medium text-text mb-1">更新说明</div>
          <pre class="text-xs text-text-regular bg-fill rounded p-3 max-h-60 overflow-y-auto whitespace-pre-wrap m-0 font-inherit">{{ checkResult.releaseNotes }}</pre>
        </div>
      </div>
    </el-card>

    <!-- 无更新提示 -->
    <el-card v-else-if="checkResult && checkResult.isSuccess && !checkResult.hasUpdate" shadow="never">
      <div class="flex items-center gap-2 text-sm text-text-regular">
        <span class="text-success">✔</span> 已是最新版本（{{ status?.currentVersion }}）
      </div>
    </el-card>
  </div>
</template>
