<script setup lang="ts">
/**
 * 网络设置面板 — 模型请求代理（2026-10-16-settings-network-proxy）
 * 行业通用形态：开关 + 服务器地址/端口 + 可选认证；保存即生效（后端实时读取，无需重启）。
 * 关闭=显式直连（忽略系统代理）；本机回环目标永不走代理；仅支持 HTTP/HTTPS 代理（SOCKS5 未支持）。
 * 布局对齐 GeneralPanel 行式形态（label 左 + 控件右，CRAP-Repetition）。
 */
import { reactive, ref, computed, onMounted } from 'vue';
import { ElMessage } from 'element-plus';
import { settingsApi } from '@/services/settingsApi';
import type { UpdateAppSettings } from '@/types/settings';

const loading = ref(false);
const saving = ref(false);

const form = reactive({
  proxyEnabled: false,
  proxyHost: '',
  proxyPort: 0,
  proxyUsername: '',
  proxyPassword: '',
});
/** 后端回显：是否已存有代理密码（密码本体永不下发） */
const proxyPasswordSet = ref(false);
/** 用户点了「清除密码」：本次保存提交空串显式清空 */
const markClearPassword = ref(false);

const errors = reactive({ host: '', port: '' });

const passwordPlaceholder = computed(() =>
  proxyPasswordSet.value ? '已保存（留空则不修改）' : '可选：代理需要认证时填写'
);

async function load() {
  loading.value = true;
  try {
    const s = await settingsApi.getSettings();
    applyServer(s);
  } catch {
    // 加载失败保持默认值（关/空），不闪错误页；下次进入面板再试
  } finally {
    loading.value = false;
  }
}

function applyServer(s: Awaited<ReturnType<typeof settingsApi.getSettings>>) {
  form.proxyEnabled = s.proxyEnabled;
  form.proxyHost = s.proxyHost;
  form.proxyPort = s.proxyPort;
  form.proxyUsername = s.proxyUsername;
  form.proxyPassword = '';
  markClearPassword.value = false;
  proxyPasswordSet.value = s.proxyPasswordSet;
  errors.host = '';
  errors.port = '';
}

/** 前端即时校验（与后端 BR-5 同规则：仅域名/IPv4、端口 1-65535） */
function validate(): boolean {
  errors.host = '';
  errors.port = '';
  if (form.proxyEnabled) {
    const host = form.proxyHost.trim();
    if (!host) errors.host = '请输入代理服务器地址';
    else if (!/^[A-Za-z0-9._-]+$/.test(host))
      errors.host = '仅支持域名或 IPv4（不能带 http:// 前缀或端口）';
    if (!Number.isInteger(form.proxyPort) || form.proxyPort < 1 || form.proxyPort > 65535)
      errors.port = '端口必须为 1-65535 的整数';
  }
  return !errors.host && !errors.port;
}

async function save() {
  if (saving.value) return;
  if (!validate()) return; // AC-UX-2：不发请求，行内错误可见
  saving.value = true;
  try {
    const payload: UpdateAppSettings = {
      proxyEnabled: form.proxyEnabled,
      proxyHost: form.proxyHost.trim(),
      proxyPort: form.proxyPort,
      proxyUsername: form.proxyUsername.trim(),
    };
    // 密码语义：有输入=覆盖；点过「清除密码」=空串清空；否则不提交该字段（留空≠清空）
    if (form.proxyPassword) payload.proxyPassword = form.proxyPassword;
    else if (markClearPassword.value) payload.proxyPassword = '';

    const s = await settingsApi.updateSettings(payload);
    applyServer(s);
    ElMessage.success({ message: '网络设置已保存，模型请求即刻生效', offset: 60 });
  } catch (e) {
    // 后端 message 原文透出（request.ts 已带），不清用户输入
    ElMessage.error({ message: e instanceof Error ? e.message : '保存失败', offset: 60 });
  } finally {
    saving.value = false;
  }
}

function requestClearPassword() {
  markClearPassword.value = true;
  form.proxyPassword = '';
}

onMounted(load);
</script>

<template>
  <div class="network-panel max-w-[800px] space-y-6">
    <el-skeleton v-if="loading && !proxyPasswordSet" :rows="3" animated />

    <el-card v-else shadow="never">
      <template #header>
        <span class="font-semibold">模型请求代理</span>
      </template>

      <div class="space-y-0">
        <!-- 代理开关 -->
        <div class="flex items-center justify-between py-3 border-b border-border-light">
          <div class="pr-4">
            <span class="text-sm font-medium text-text">代理开关</span>
            <p class="text-xs text-text-secondary mt-0.5 mb-0">
              开启后，后端发出的全部模型请求（对话/流式/模型列表/密钥校验）经此代理；关闭=直连（忽略系统代理）
            </p>
          </div>
          <el-switch v-model="form.proxyEnabled" aria-label="代理开关" data-testid="proxy-switch" />
        </div>

        <!-- 开启态：表单组（亲密性成组，顺序=地址、端口、用户名、密码） -->
        <template v-if="form.proxyEnabled">
          <div class="py-3 border-b border-border-light">
            <div class="flex items-center justify-between">
              <span class="text-sm font-medium text-text w-[120px] shrink-0">服务器地址</span>
              <el-input
                v-model="form.proxyHost"
                class="w-[240px]"
                placeholder="如 127.0.0.1 或 proxy.example.com"
                aria-label="代理服务器地址"
                :class="{ 'is-error': errors.host }"
                @input="errors.host = ''"
              />
            </div>
            <!-- 错误行常驻等高占位（h-4）：出现/消失不改变布局 —— 避免 mousedown 后 blur 提交清错→按钮上移→mouseup 落空吞掉点击（e2e 实证 2026-10-16） -->
            <p class="text-xs mt-1 mb-0 h-4 text-right" style="color: var(--el-color-danger)" data-testid="host-error">
              {{ errors.host }}
            </p>
          </div>

          <div class="py-3 border-b border-border-light">
            <div class="flex items-center justify-between">
              <span class="text-sm font-medium text-text w-[120px] shrink-0">端口</span>
              <el-input-number
                v-model="form.proxyPort"
                :min="0"
                :max="65535"
                :step="1"
                class="w-[240px]"
                controls-position="right"
                aria-label="代理端口"
                placeholder="如 7890"
                @change="errors.port = ''"
              />
            </div>
            <p class="text-xs mt-1 mb-0 h-4 text-right" style="color: var(--el-color-danger)" data-testid="port-error">
              {{ errors.port }}
            </p>
          </div>

          <div class="flex items-center justify-between py-3 border-b border-border-light">
            <span class="text-sm font-medium text-text w-[120px] shrink-0">用户名</span>
            <el-input
              v-model="form.proxyUsername"
              class="w-[240px]"
              placeholder="可选：代理需要认证时填写"
              aria-label="代理认证用户名"
              autocomplete="off"
            />
          </div>

          <div class="flex items-center justify-between py-3 border-b border-border-light">
            <span class="text-sm font-medium text-text w-[120px] shrink-0">密码</span>
            <div class="flex items-center gap-2">
              <el-input
                v-model="form.proxyPassword"
                class="w-[240px]"
                type="password"
                show-password
                autocomplete="new-password"
                :placeholder="passwordPlaceholder"
                aria-label="代理认证密码"
              />
              <el-button
                v-if="proxyPasswordSet && !markClearPassword"
                size="small"
                text
                type="warning"
                @click="requestClearPassword"
              >
                清除密码
              </el-button>
              <span v-if="markClearPassword" class="text-xs text-text-secondary">保存后将清空</span>
            </div>
          </div>

          <div class="flex items-center justify-between py-3">
            <p class="text-xs text-text-secondary m-0">
              支持 HTTP/HTTPS 代理；本机（127.0.0.1/localhost）请求永不走代理；代理不可达时请求将直接失败，不会绕开代理直连。
            </p>
            <el-button type="primary" :loading="saving" :disabled="saving" data-testid="save-btn" @click="save">
              保存
            </el-button>
          </div>
        </template>

        <!-- 关闭态说明 -->
        <div v-else class="py-3">
          <p class="text-xs text-text-secondary m-0">
            当前不走代理：全部模型请求直连目标服务（含忽略系统代理）。需要代理时请打开上方开关并填写服务器地址。
          </p>
        </div>
      </div>
    </el-card>
  </div>
</template>
