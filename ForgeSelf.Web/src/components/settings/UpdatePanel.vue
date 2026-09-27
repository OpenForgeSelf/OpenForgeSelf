<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue';
import { ElMessage, ElMessageBox } from 'element-plus';
import { updateApi } from '@/services/updateApi';
import type { UpdateCheckResult, UpdateConfigInfo, UpdateConfigPatch, UpdateStageInfo, UpdateStatus } from '@/services/updateApi';

const status = ref<UpdateStatus | null>(null);
const checkResult = ref<UpdateCheckResult | null>(null);
const stage = ref<UpdateStageInfo | null>(null);

// 更新源配置（2026-09-28：精简为下拉 + 对应地址输入框）
const config = ref<UpdateConfigInfo | null>(null);
const configProvider = ref('github');
const configGithubUrl = ref('');
const configGiteeUrl = ref('');
const configServerUrl = ref('');
const configLocalDir = ref('');
const savingConfig = ref(false);

/** 更新源选项：value 传给后端，label 下拉展示，hint 为选项下的简短说明 */
const providerOptions: { value: string; label: string; hint: string }[] = [
  { value: 'github', label: 'GitHub', hint: '从 GitHub 仓库拉取更新' },
  { value: 'gitee', label: 'Gitee', hint: '从 Gitee 仓库拉取更新（国内访问更稳）' },
  { value: 'stardust', label: '更新服务器', hint: '从版本更新服务器拉取更新' },
  { value: 'local', label: '本地目录', hint: '从本机打包目录拉取更新' },
];

const providerHint = () => providerOptions.find(o => o.value === configProvider.value)?.hint ?? '';

/** owner/repo ↔ 完整仓库地址 转换（github/gitee 输入框展示完整地址，后端存 owner/repo） */
function repoToUrl(repo: string | null | undefined, host: 'github' | 'gitee'): string {
  const r = (repo ?? '').trim();
  if (!r) return '';
  if (/^https?:\/\//i.test(r)) return r;
  return `https://${host}.com/${r}`;
}
function parseRepoUrl(url: string): string {
  const t = url.trim().replace(/\/+$/, '');
  const m = t.match(/^(?:https?:\/\/)?(?:www\.)?(?:github\.com|gitee\.com)\/(.+)$/i);
  return m ? m[1].trim() : t;
}

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

async function loadConfig() {
  try {
    config.value = await updateApi.getConfig();
    configProvider.value = config.value.provider;
    configGithubUrl.value = repoToUrl(config.value.githubRepo, 'github');
    configGiteeUrl.value = repoToUrl(config.value.giteeRepo, 'gitee');
    configServerUrl.value = config.value.serverUrl ?? '';
    configLocalDir.value = config.value.localDir ?? '';
  } catch (e) {
    ElMessage.error({ message: `加载更新源配置失败: ${e instanceof Error ? e.message : e}`, offset: 60 });
  }
}

async function onSaveConfig() {
  if (busy()) {
    ElMessage.warning({ message: '更新进行中，请等待完成后再修改配置', offset: 60 });
    return;
  }
  savingConfig.value = true;
  try {
    const patch: UpdateConfigPatch = { provider: configProvider.value };
    if (configProvider.value === 'github') patch.githubRepo = parseRepoUrl(configGithubUrl.value);
    else if (configProvider.value === 'gitee') patch.giteeRepo = parseRepoUrl(configGiteeUrl.value);
    else if (configProvider.value === 'stardust') patch.serverUrl = configServerUrl.value;
    else if (configProvider.value === 'local') patch.localDir = configLocalDir.value;
    const saved = await updateApi.saveConfig(patch);
    config.value = saved;
    configProvider.value = saved.provider;
    configGithubUrl.value = repoToUrl(saved.githubRepo, 'github');
    configGiteeUrl.value = repoToUrl(saved.giteeRepo, 'gitee');
    configServerUrl.value = saved.serverUrl ?? '';
    configLocalDir.value = saved.localDir ?? '';
    ElMessage.success({ message: '更新源配置已保存', offset: 60 });
    await loadStatus();
  } catch (e) {
    ElMessage.error({ message: `保存失败: ${e instanceof Error ? e.message : e}`, offset: 60 });
  } finally {
    savingConfig.value = false;
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

/**
 * 重启等待：轮询后端恢复，恢复后刷新页面。
 * 必须带 Authorization（/api/update/* 走 ApiKeyPolicy 鉴权）——此前裸 fetch 无 token，
 * 新实例起来后 /progress 一直 401，gone 分支永远等不到 res.ok，页面死等转圈（2026-09-28 修复）。
 */
function waitForRestart() {
  stopPolling();
  let gone = false;
  pollTimer = window.setInterval(async () => {
    try {
      const res = await fetch('/api/update/progress', {
        method: 'GET',
        cache: 'no-store',
        headers: { Authorization: `Bearer ${localStorage.getItem('forge_api_token') ?? ''}` },
      });
      if (res.ok) {
        if (gone) {
          stopPolling();
          ElMessage.success({ message: '更新完成，应用已重启', offset: 60 });
          window.location.reload();
        }
      } else {
        gone = true;
      }
    } catch {
      gone = true; // 连接失败 = 宿主已退出，进入等待
    }
  }, 3000);
}

onMounted(async () => {
  await loadConfig();
  await loadStatus();
  const s = stage.value?.status;
  if (s && ['checking', 'downloading', 'verifying', 'extracting'].includes(s)) {
    startPolling();
  } else if (s === 'applying') {
    // 页面在宿主重启期间被刷新/恢复：继续等待重启完成
    applying.value = true;
    waitForRestart();
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
          从 {{ providerOptions.find(o => o.value === (status?.provider ?? 'stardust'))?.label ?? '更新服务器' }} 检查并安装更新
        </p>
      </div>
    </div>

    <!-- 更新源配置（2026-09-28：下拉 + 对应地址输入框，选项下简短说明） -->
    <el-card shadow="never">
      <div class="flex items-center justify-between mb-3">
        <div>
          <div class="text-base font-medium text-text">更新源配置</div>
          <div class="text-xs text-text-secondary mt-1">选择更新来源，填写对应地址后保存</div>
        </div>
      </div>
      <div class="space-y-3">
        <div class="flex items-start gap-3">
          <el-select v-model="configProvider" style="width: 200px" :disabled="busy() || savingConfig">
            <el-option v-for="o in providerOptions" :key="o.value" :label="o.label" :value="o.value" />
          </el-select>
          <div style="flex: 1">
            <el-input
              v-if="configProvider === 'github'"
              v-model="configGithubUrl"
              placeholder="https://github.com/OpenForgeSelf/OpenForgeSelf"
              :disabled="savingConfig"
            />
            <el-input
              v-else-if="configProvider === 'gitee'"
              v-model="configGiteeUrl"
              placeholder="https://gitee.com/OpenForgeSelf/OpenForgeSelf"
              :disabled="savingConfig"
            />
            <el-input
              v-else-if="configProvider === 'stardust'"
              v-model="configServerUrl"
              placeholder="更新服务器地址（尚未配置线上地址）"
              :disabled="savingConfig"
            />
            <el-input
              v-else-if="configProvider === 'local'"
              v-model="configLocalDir"
              placeholder="本机打包输出目录，如 D:\updates"
              :disabled="savingConfig"
            />
            <div class="text-xs text-text-secondary mt-1">{{ providerHint() }}</div>
          </div>
          <el-button type="primary" :loading="savingConfig" :disabled="busy()" @click="onSaveConfig">保存</el-button>
        </div>
      </div>
    </el-card>

    <el-card shadow="never">
      <!-- 当前版本 -->
      <div class="flex items-center justify-between">
        <div>
          <div class="text-sm text-text-regular">当前版本</div>
          <div class="text-lg font-semibold text-text mt-1">v{{ status?.currentVersion ?? '…' }}</div>
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
            @click="onDownload"
          >
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

        <!-- 重启中：宿主已退出，由更新代理换文件并重启（2026-09-28：加显式提示，避免无反馈转圈） -->
        <div v-if="applying" class="flex items-center gap-2 text-sm text-text-regular">
          <span class="inline-block h-3 w-3 rounded-full border-2 border-border animate-spin" style="border-top-color: var(--el-color-primary)" />
          正在重启应用并安装更新，完成后页面将自动刷新…
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
