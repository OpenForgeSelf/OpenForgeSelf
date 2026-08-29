<script setup lang="ts">
/**
 * API 服务器面板 — 独立组件
 * 对齐设计稿 forgeself-design/pages/api-server.html
 * 纯 Element Plus 组件 + Tailwind 布局，无自定义 token
 */
import { ref, onMounted } from 'vue';
import { ElMessage, ElMessageBox } from 'element-plus';
import { Refresh, Document, Edit, Check, Key, Lock } from '@element-plus/icons-vue';
import { apiServerApi } from '@/services/apiServerApi';
import { portConfigApi } from '@/services/portConfigApi';
import { pollForRestart, formatPollDuration } from '@/services/applicationRestart';
import { getStoredToken, setStoredToken } from '@/services/request';
import type { ApiServerConfig } from '@/types/apiServer';
import type { PortConfig } from '@/types/portConfig';

const config = ref<ApiServerConfig | null>(null);
const portConfig = ref<PortConfig | null>(null);
const loading = ref(false);
const error = ref('');

// 认证错误状态
const authError = ref(false);
const showKeyInput = ref(false);
const manualKeyInput = ref('');

// 端口编辑
const editing = ref(false);
const editingPort = ref(7102);
const saving = ref(false);
const restarting = ref(false);
const restartProgress = ref(0);

async function loadConfig() {
  loading.value = true;
  error.value = '';
  authError.value = false;
  showKeyInput.value = false;
  try {
    // 加载配置——401 不阻塞页面，仅标记认证错误
    try {
      const [apiCfg, portCfg] = await Promise.all([
        apiServerApi.getConfig(),
        portConfigApi.getPortConfig().catch(() => null)
      ]);
      config.value = apiCfg;
      authError.value = false;
      if (portCfg) {
        portConfig.value = portCfg;
        editingPort.value = portCfg.portNumber;
      } else {
        portConfig.value = null;
        editingPort.value = 7102;
      }
    } catch (e) {
      if (e instanceof Error && e.message.includes('401')) {
        // 401 认证失败：不阻塞页面，保留已有配置（如果有），标记认证错误
        authError.value = true;
        // 不清除已有 config，保留上次成功加载的数据
        if (!config.value) {
          // 首次加载就 401，用空配置让页面能渲染
          config.value = null;
        }
      } else {
        throw e;
      }
    }
  } catch (e) {
    error.value = e instanceof Error ? e.message : '加载失败';
  } finally {
    loading.value = false;
  }
}

/** 提交手动输入的 API 密钥 */
async function submitManualKey() {
  const key = manualKeyInput.value.trim();
  if (!key) {
    ElMessage.warning({ message: '请输入有效的 API 密钥', offset: 60 });
    return;
  }
  setStoredToken(key);
  showKeyInput.value = false;
  manualKeyInput.value = '';
  await loadConfig();
}

/** 显示密钥输入框（用于重新填写） */
function showManualKeyInput() {
  showKeyInput.value = true;
  manualKeyInput.value = getStoredToken() || '';
}

/** 取消密钥输入 */
function cancelManualKeyInput() {
  showKeyInput.value = false;
  manualKeyInput.value = '';
}

async function regenerateKey() {
  loading.value = true;
  error.value = '';
  try {
    config.value = await apiServerApi.regenerateKey();
    authError.value = false;
    ElMessage.success({ message: '密钥已重新生成', offset: 60 });
  } catch (e) {
    if (e instanceof Error && e.message.includes('401')) {
      authError.value = true;
      ElMessage.error({ message: '认证失败，请检查 API 密钥是否正确', offset: 60 });
    } else {
      error.value = e instanceof Error ? e.message : '重新生成失败';
    }
  } finally {
    loading.value = false;
  }
}

function startEdit() {
  editing.value = true;
}

function cancelEdit() {
  editing.value = false;
}

async function savePort() {
  if (!portConfig.value) {
    ElMessage.error({ message: '端口配置未加载，请刷新页面重试', offset: 60 });
    await loadConfig();
    return;
  }

  const newPort = editingPort.value;
  if (newPort < 1 || newPort > 65535) {
    ElMessage.error({ message: '端口号必须在 1-65535 之间', offset: 60 });
    return;
  }

  if (newPort === portConfig.value.portNumber) {
    ElMessage.info({ message: '端口号未改变', offset: 60 });
    editing.value = false;
    return;
  }

  // 检查端口是否可用
  try {
    const checkResult = await portConfigApi.checkPortAvailable(newPort);
    if (!checkResult.available) {
      ElMessage.error({ message: checkResult.message || `端口 ${newPort} 不可用`, offset: 60 });
      return;
    }
  } catch (e) {
    ElMessage.error({
      message: `检查端口失败：${e instanceof Error ? e.message : '未知错误'}`,
      offset: 60
    });
    return;
  }

  // 确认重启
  try {
    await ElMessageBox.confirm(
      `修改端口号需要重启服务。期间服务将暂时不可用，预计耗时 10-30 秒。是否继续？`,
      '确认修改端口',
      {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        type: 'warning'
      }
    );
  } catch {
    return;
  }

  // 保存配置
  saving.value = true;
  try {
    const newConfig = await portConfigApi.updatePortConfig(newPort);
    portConfig.value = newConfig;
    editing.value = false;

    // 调用重启接口（后端会从 Forge 配置文件读取新端口）
    await apiServerApi.restart();

    ElMessage.success({ message: `端口已更新为 ${newPort}，服务将在 5 秒后重启...`, offset: 60 });

    // 开始轮询新端口
    restarting.value = true;
    const startTime = Date.now();

    const success = await pollForRestart(newPort, {
      pollInterval: 1000,
      pollTimeout: 60000,
      onRetry: (_attempt, elapsed) => {
        restartProgress.value = Math.min((elapsed / 60000) * 100, 95);
      }
    });

    if (success) {
      const duration = Date.now() - startTime;
      ElMessage.success(`服务已在新端口 ${newPort} 重新启动，耗时 ${formatPollDuration(duration)}`);
      setTimeout(() => {
        window.location.reload();
      }, 1500);
    } else {
      ElMessage.warning('服务重启超时，请手动检查端口是否生效');
    }
  } catch (e) {
    ElMessage.error(`保存失败：${e instanceof Error ? e.message : '未知错误'}`);
  } finally {
    saving.value = false;
    restarting.value = false;
    restartProgress.value = 0;
  }
}

function openApiDocs() {
  window.open('/scalar/v1', '_blank', 'noopener,noreferrer');
}

/**
 * 确保存在 API token：首次打开页面且本地无 token 时，
 * 通过后端 init-token 一次性接口获取初始密钥并保存。
 * 失败（如 403 首次初始化已完成）不阻塞页面，由 loadConfig 的 401 分支提示手动输入。
 */
async function ensureToken() {
  if (getStoredToken()) return;
  try {
    await apiServerApi.initToken();
  } catch {
    // initToken 失败（NEED_MANUAL_TOKEN / 网络异常）：忽略，交由 401 分支处理
  }
}

onMounted(async () => {
  await ensureToken();
  await loadConfig();
});
</script>

<template>
  <div class="max-w-[800px] space-y-6">
    <!-- 页头 -->
    <div class="flex items-start justify-between pb-4 border-b border-gray-200">
      <div>
        <h2 class="text-2xl font-bold text-gray-900 m-0 leading-tight">API 服务器</h2>
        <p class="text-sm text-gray-600 mt-1 mb-0">
          通过 OpenAI 兼容的 HTTP API 暴露铸己匣的 AI 功能
        </p>
      </div>
      <el-button type="success" :icon="Document" @click="openApiDocs">API 文档</el-button>
    </div>

    <!-- 首次加载：骨架屏 -->
    <el-skeleton v-if="loading && !config" :rows="4" animated />

    <!-- 错误提示 -->
    <el-alert v-if="error && !loading" :title="error" type="error" show-icon :closable="false" />

    <!-- 认证失败提示 -->
    <el-alert
      v-if="authError && !loading"
      title="API 密钥验证失败，部分信息可能不可用。请检查密钥是否正确。"
      type="warning"
      show-icon
      :closable="false">
      <template #footer>
        <el-button size="small" type="warning" @click="showManualKeyInput">重新填写密钥</el-button>
      </template>
    </el-alert>

    <!-- 主内容——始终显示卡片布局，无数据时占位 -->
    <template v-if="!loading">
      <!-- 运行状态 + 端口编辑 -->
      <el-card shadow="never" class="!border-green-300">
        <template #header>
          <div class="flex items-center justify-between">
            <span class="font-semibold">端口配置</span>
            <div class="flex items-center gap-2">
              <template v-if="!editing">
                <el-button
                  :icon="Edit"
                  size="small"
                  text
                  type="primary"
                  :disabled="saving || restarting"
                  @click="startEdit">
                  编辑
                </el-button>
              </template>
              <template v-else>
                <el-button
                  :icon="Check"
                  size="small"
                  type="success"
                  :loading="saving"
                  :disabled="saving || restarting"
                  @click="savePort">
                  保存
                </el-button>
                <el-button size="small" :disabled="saving || restarting" @click="cancelEdit">
                  取消
                </el-button>
              </template>
            </div>
          </div>
        </template>
        <div class="flex items-center gap-3">
          <span
            class="w-2 h-2 rounded-full shrink-0"
            style="
              background-color: var(--el-color-success);
              box-shadow: 0 0 0 4px var(--el-color-success-light-9);
            " />
          <div class="flex-1">
            <span class="text-sm font-semibold" style="color: var(--el-color-success)">运行中</span>
            <div class="flex items-center gap-2 mt-1">
              <template v-if="!editing">
                <code class="font-mono text-sm text-gray-600">
                  http://localhost:{{
                    config?.apiBaseUrl?.match(/:(\d+)/)?.[1] || config?.apiBaseUrl || '—'
                  }}/v1
                </code>
              </template>
              <template v-else>
                <el-input
                  v-model.number="editingPort"
                  type="number"
                  size="small"
                  class="w-48"
                  :min="1024"
                  :max="65535"
                  placeholder="端口号"
                  @keyup.enter="savePort">
                  <template #prepend>http://localhost:</template>
                  <template #append>/v1</template>
                </el-input>
              </template>
            </div>
          </div>
        </div>
        <!-- 重启进度 -->
        <div v-if="restarting" class="mt-4">
          <el-progress
            :percentage="restartProgress"
            :stroke-width="6"
            :show-text="false"
            status="active" />
          <p class="text-xs text-gray-600 mt-2">服务正在重启，请稍候...</p>
        </div>
      </el-card>

      <!-- API 密钥 -->
      <el-card shadow="never">
        <template #header>
          <div class="flex items-center justify-between">
            <div class="flex items-center gap-2">
              <el-icon :size="16" class="text-blue-600"><Key /></el-icon>
              <span class="text-base font-semibold text-gray-900">API 密钥</span>
            </div>
            <div class="flex items-center gap-2">
              <template v-if="!showKeyInput">
                <el-button
                  size="small"
                  type="primary"
                  :icon="Refresh"
                  :loading="loading"
                  @click="regenerateKey">
                  重新生成
                </el-button>
              </template>
            </div>
          </div>
        </template>
        <p class="text-sm text-gray-600 mt-0 mb-3">用于 API 访问的安全认证令牌</p>

        <!-- 认证失败 or 手动输入模式：显示可编辑输入框 -->
        <template v-if="showKeyInput || authError">
          <el-input
            v-model="manualKeyInput"
            type="text"
            placeholder="请输入 API 密钥"
            class="font-mono mb-3"
            show-password />
          <div class="flex gap-2">
            <el-button type="primary" :loading="loading" @click="submitManualKey">
              保存密钥
            </el-button>
            <el-button v-if="showKeyInput" @click="cancelManualKeyInput">取消</el-button>
          </div>
        </template>

        <!-- 正常模式：显示只读密钥 -->
        <template v-else>
          <el-input
            :model-value="config?.apiKeyPlain || config?.apiKeyMasked || '—'"
            readonly
            class="font-mono" />
        </template>
      </el-card>

      <!-- 授权标头 -->
      <el-card shadow="never">
        <template #header>
          <div class="flex items-center justify-between">
            <div class="flex items-center gap-2">
              <el-icon :size="16" class="text-blue-600"><Lock /></el-icon>
              <span class="text-base font-semibold text-gray-900">授权标头</span>
            </div>
          </div>
        </template>
        <p class="text-sm text-gray-600 mt-0 mb-3">请求时需要在 HTTP 头中携带的认证信息</p>
        <el-input :model-value="config?.authHeader || '—'" readonly class="font-mono" />
      </el-card>
    </template>
  </div>
</template>

<style>
/* 修复 Element Plus MessageBox 弹窗定位问题 */
.api-token-dialog {
  top: 50% !important;
  left: 50% !important;
  transform: translate(-50%, -50%) !important;
  margin: 0 !important;
  position: fixed !important;
}

.api-token-dialog .el-message-box__header {
  border-bottom: 1px solid var(--el-border-color-lighter);
  padding-bottom: 12px;
}

.api-token-dialog .el-message-box__content {
  padding: 20px;
}

.api-token-dialog .el-message-box__btns {
  padding: 0 20px 20px;
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}
</style>
