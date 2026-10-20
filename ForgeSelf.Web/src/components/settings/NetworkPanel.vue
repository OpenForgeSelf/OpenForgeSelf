<script setup lang="ts">
/**
 * 网络设置面板 — 模型请求代理（2026-10-16-settings-proxy-mode-bypass）
 * 行业通用形态：三模式（跟随系统/手动配置/直连）+ 旁路条目（一行一条，可增删）+ 手动模式「测试」按钮。
 * 保存即生效（后端实时读取，无需重启）；回环永不走代理；仅支持 HTTP/HTTPS 代理（SOCKS5 未支持）。
 * 布局对齐 GeneralPanel 行式形态（label 左 + 控件右，CRAP-Repetition）；错误行常驻 h-4 等高占位（防吞点击回归，Part B B3）。
 */
import { nextTick, onMounted, reactive, ref, computed } from 'vue';
import { ElMessage } from 'element-plus';
import { CircleCheck, CircleClose, Delete, Plus } from '@element-plus/icons-vue';
import { settingsApi } from '@/services/settingsApi';
import type { ProxyMode, TestProxyResult, UpdateAppSettings } from '@/types/settings';

type TestState = 'idle' | 'testing' | 'success' | 'fail';

const loading = ref(false);
const saving = ref(false);

const form = reactive({
  proxyMode: 'system' as ProxyMode,
  proxyHost: '',
  proxyPort: 0,
  proxyUsername: '',
  proxyPassword: '',
});
/** 不走代理的条目（待保存态；保存统一落盘） */
const bypassEntries = ref<string[]>([]);
/** 后端回显：是否已存有代理密码（密码本体永不下发） */
const proxyPasswordSet = ref(false);
/** 用户点了「清除密码」：本次保存提交空串显式清空 */
const markClearPassword = ref(false);

const errors = reactive({ host: '', port: '', bypass: '' });

/** 测试按钮状态机：idle→testing→success/fail；改任意输入或切模式重置（BR：结果必须与当前表单一致） */
const testState = ref<TestState>('idle');
const testResult = ref<TestProxyResult | null>(null);
const probeUrl = ref('');

const MODES: { value: ProxyMode; label: string }[] = [
  { value: 'system', label: '跟随系统' },
  { value: 'manual', label: '手动配置' },
  { value: 'direct', label: '直连·不走代理' },
];

const BYPASS_ENTRY_PATTERN = /^[A-Za-z0-9.*_:-]+$/;
const BYPASS_MAX = 50;

const passwordPlaceholder = computed(() =>
  proxyPasswordSet.value ? '已保存（留空则不修改）' : '可选：代理需要认证时填写'
);
const showBypassArea = computed(() => form.proxyMode === 'manual' || form.proxyMode === 'system');
const canAddEntry = computed(() => bypassEntries.value.length < BYPASS_MAX);

async function load() {
  loading.value = true;
  try {
    const s = await settingsApi.getSettings();
    applyServer(s);
  } catch {
    // 加载失败保持默认值（跟随系统/空），不闪错误页；下次进入面板再试
  } finally {
    loading.value = false;
  }
}

function applyServer(s: Awaited<ReturnType<typeof settingsApi.getSettings>>) {
  form.proxyMode = s.proxyMode;
  form.proxyHost = s.proxyHost;
  form.proxyPort = s.proxyPort;
  form.proxyUsername = s.proxyUsername;
  form.proxyPassword = '';
  bypassEntries.value = [...s.proxyBypass];
  markClearPassword.value = false;
  proxyPasswordSet.value = s.proxyPasswordSet;
  errors.host = '';
  errors.port = '';
  errors.bypass = '';
  resetTest();
}

/** 任意输入变更/模式切换 → 测试结果失效回 idle（避免拿着旧配置的结论误导） */
function resetTest() {
  testState.value = 'idle';
  testResult.value = null;
}

/** 前端即时校验（与后端 BR-5 同规则：仅域名/IPv4、端口 1-65535、旁路字符集/长度/条数） */
function validate(): boolean {
  errors.host = '';
  errors.port = '';
  errors.bypass = '';
  if (form.proxyMode === 'manual') {
    const host = form.proxyHost.trim();
    if (!host) errors.host = '请输入代理服务器地址';
    else if (!/^[A-Za-z0-9._-]+$/.test(host))
      errors.host = '仅支持域名或 IPv4（不能带 http:// 前缀或端口）';
    if (!Number.isInteger(form.proxyPort) || form.proxyPort < 1 || form.proxyPort > 65535)
      errors.port = '端口必须为 1-65535 的整数';
  }
  bypassEntries.value.forEach((e, i) => {
    const v = e.trim();
    if (v && (v.length > 128 || !BYPASS_ENTRY_PATTERN.test(v)))
      errors.bypass = `第 ${i + 1} 条无效：仅支持域名/IPv4/通配符（* . - _ :），单条≤128 字符`;
  });
  return !errors.host && !errors.port && !errors.bypass;
}

async function save() {
  if (saving.value) return;
  if (!validate()) return; // 不发请求，行内错误可见
  saving.value = true;
  try {
    const payload: UpdateAppSettings = {
      proxyMode: form.proxyMode,
      proxyHost: form.proxyHost.trim(),
      proxyPort: form.proxyPort,
      proxyUsername: form.proxyUsername.trim(),
      proxyBypass: bypassEntries.value.map((e) => e.trim()).filter((e) => e.length > 0),
    };
    // 密码语义：有输入=覆盖；点过「清除密码」=空串清空；否则不提交该字段（留空≠清空）
    if (form.proxyPassword) payload.proxyPassword = form.proxyPassword;
    else if (markClearPassword.value) payload.proxyPassword = '';

    const s = await settingsApi.updateSettings(payload);
    applyServer(s);
    ElMessage.success({ message: '网络设置已保存，模型请求即刻生效', offset: 60 });
  } catch (e) {
    // 后端 400 原文透出（request.ts 已带），不清用户输入
    ElMessage.error({ message: e instanceof Error ? e.message : '保存失败', offset: 60 });
  } finally {
    saving.value = false;
  }
}

/** 测试按钮：经入参代理发一次性探测（后端零副作用）；host/port 未填→行内拦截不发请求 */
async function runTest() {
  if (testState.value === 'testing') return;
  const host = form.proxyHost.trim();
  if (!host || !Number.isInteger(form.proxyPort) || form.proxyPort < 1 || form.proxyPort > 65535) {
    errors.host = host ? errors.host : '请先填写地址和端口';
    if (!errors.host) errors.port = '请先填写地址和端口';
    testState.value = 'idle';
    testResult.value = null;
    return;
  }
  testState.value = 'testing';
  testResult.value = null;
  try {
    const r = await settingsApi.testProxy({
      proxyHost: host,
      proxyPort: form.proxyPort,
      proxyUsername: form.proxyUsername.trim() || undefined,
      proxyPassword: form.proxyPassword || undefined,
      proxyTestUrl: probeUrl.value.trim() || undefined,
    });
    testResult.value = r;
    testState.value = r.reachable ? 'success' : 'fail';
  } catch (e) {
    testResult.value = { reachable: false, statusCode: null, latencyMs: 0, message: e instanceof Error ? e.message : '测试请求失败' };
    testState.value = 'fail';
  }
}

const testFeedback = computed(() => {
  const r = testResult.value;
  if (!r) return '';
  if (r.reachable) return `代理可用 · HTTP ${r.statusCode} · ${r.latencyMs}ms`;
  return r.message || '代理不可达';
});

function addBypassEntry() {
  if (!canAddEntry.value) return;
  bypassEntries.value.push('');
  // 新行聚焦（交互规格：点「添加条目」→ 尾部新空行并聚焦）
  nextTick(() => {
    const inputs = document.querySelectorAll<HTMLInputElement>('#bypass-list input');
    inputs[inputs.length - 1]?.focus();
  });
}

function removeBypassEntry(index: number) {
  bypassEntries.value.splice(index, 1);
  if (errors.bypass) validate();
}

function requestClearPassword() {
  markClearPassword.value = true;
  form.proxyPassword = '';
}

onMounted(() => {
  load();
});
</script>

<template>
  <div class="network-panel max-w-[800px] space-y-6">
    <el-skeleton v-if="loading && !proxyPasswordSet" :rows="3" animated />

    <el-card v-else shadow="never">
      <template #header>
        <span class="font-semibold">模型请求代理</span>
      </template>

      <div class="space-y-0">
        <!-- 代理模式（三选一，行业通用：跟随系统/手动/直连） -->
        <div class="flex items-center justify-between py-3 border-b border-border-light">
          <div class="pr-4">
            <span class="text-sm font-medium text-text">代理模式</span>
            <p class="text-xs text-text-secondary mt-0.5 mb-0">
              后端发出的全部模型请求（对话/流式/模型列表/密钥校验）按此模式路由
            </p>
          </div>
          <el-radio-group v-model="form.proxyMode" data-testid="proxy-mode-group" @change="resetTest">
            <el-radio v-for="m in MODES" :key="m.value" :value="m.value" data-testid="proxy-mode-item">
              {{ m.label }}
            </el-radio>
          </el-radio-group>
        </div>

        <!-- 跟随系统：只读说明（无地址表单；旁路区在下方共用） -->
        <div v-if="form.proxyMode === 'system'" class="py-3 border-b border-border-light">
          <p class="text-xs text-text-secondary m-0">
            按 Windows 系统代理设置发出请求（设置 → 网络 → 代理）。系统未配置代理时等效直连；如需为内网地址豁免，请在下方添加不走代理的条目。
          </p>
        </div>

        <!-- 直连：说明（全部直连，旁路无意义故不显示条目区） -->
        <div v-if="form.proxyMode === 'direct'" class="py-3 border-b border-border-light">
          <p class="text-xs text-text-secondary m-0">
            当前不发任何代理：全部模型请求直连目标服务（含忽略系统代理）。需要代理时请切换到「跟随系统」或「手动配置」。
          </p>
        </div>

        <!-- 手动配置：表单组（亲密性成组，顺序=地址+测试、探测地址、端口、用户名、密码） -->
        <template v-if="form.proxyMode === 'manual'">
          <div class="py-3 border-b border-border-light">
            <div class="flex items-center justify-between">
              <span class="text-sm font-medium text-text w-[120px] shrink-0">服务器地址</span>
              <div class="flex items-center gap-2">
                <el-input
                  v-model="form.proxyHost"
                  style="width: 240px"
                  placeholder="如 proxy.example.com"
                  aria-label="代理服务器地址"
                  :class="{ 'is-error': errors.host }"
                  @input="errors.host = ''; resetTest()"
                />
                <el-button
                  :loading="testState === 'testing'"
                  :disabled="testState === 'testing'"
                  data-testid="proxy-test-btn"
                  @click="runTest"
                >
                  测试
                </el-button>
              </div>
            </div>
            <!-- 错误行常驻等高占位（h-4）：出现/消失不改变布局 —— 避免 mousedown 后 blur 提交清错→按钮上移→mouseup 落空吞掉点击（e2e 实证 2026-10-16） -->
            <p class="text-xs mt-1 mb-0 h-4 text-right" style="color: var(--el-color-danger)" data-testid="host-error">
              {{ errors.host }}
            </p>
          </div>

          <!-- 测试结果行：图标+文字不单靠颜色（F 系可访问性） -->
          <div class="py-1.5 border-b border-border-light">
            <div class="flex items-center justify-between">
              <span class="text-sm font-medium text-text w-[120px] shrink-0">探测地址</span>
              <el-input
                v-model="probeUrl"
                style="width: 240px"
                placeholder="可选：默认测 baidu"
                aria-label="探测目标地址"
                @input="resetTest()"
              />
            </div>
            <div class="flex items-center justify-end gap-1 mt-1 mb-0 h-4 text-xs" data-testid="proxy-test-feedback">
              <template v-if="testState === 'success'">
                <el-icon :size="12" style="color: var(--el-color-success)"><CircleCheck /></el-icon>
                <span style="color: var(--el-color-success)">{{ testFeedback }}</span>
              </template>
              <template v-else-if="testState === 'fail'">
                <el-icon :size="12" style="color: var(--el-color-danger)"><CircleClose /></el-icon>
                <span style="color: var(--el-color-danger)">{{ testFeedback }}</span>
              </template>
              <span v-else-if="testState === 'testing'" class="text-text-secondary">测试中（最长 10s）…</span>
              <span v-else class="text-text-secondary">收到任何 HTTP 响应（含 4xx/5xx）即代表代理可用</span>
            </div>
          </div>

          <div class="py-3 border-b border-border-light">
            <div class="flex items-center justify-between">
              <span class="text-sm font-medium text-text w-[120px] shrink-0">端口</span>
              <el-input-number
                v-model="form.proxyPort"
                :min="0"
                :max="65535"
                :step="1"
                style="width: 240px"
                controls-position="right"
                aria-label="代理端口"
                placeholder="如 7890"
                @change="errors.port = ''; resetTest()"
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
              style="width: 240px"
              placeholder="可选：代理需要认证时填写"
              aria-label="代理认证用户名"
              autocomplete="off"
              @input="resetTest()"
            />
          </div>

          <div class="flex items-center justify-between py-3 border-b border-border-light">
            <span class="text-sm font-medium text-text w-[120px] shrink-0">密码</span>
            <div class="flex items-center gap-2">
              <el-input
                v-model="form.proxyPassword"
                style="width: 240px"
                type="password"
                show-password
                autocomplete="new-password"
                :placeholder="passwordPlaceholder"
                aria-label="代理认证密码"
                @input="resetTest()"
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
        </template>

        <!-- 不走代理的条目（BR-2；manual/system 生效区，direct 隐藏——全部直连时旁路无意义） -->
        <div v-if="showBypassArea" id="bypass-list" class="py-3 border-b border-border-light">
          <div class="flex items-center justify-between mb-1">
            <div class="pr-4">
              <span class="text-sm font-medium text-text">不走代理的条目</span>
              <p class="text-xs text-text-secondary mt-0.5 mb-0">
                命中条目的地址将不走代理（如 <code>*.example.com</code>、<code>10.*</code>、<code>192.168.1.5</code>）；本机回环永不走代理，无需写条目
              </p>
            </div>
            <el-button
              size="small"
              :disabled="!canAddEntry"
              :title="canAddEntry ? '' : `已达上限 ${BYPASS_MAX} 条`"
              data-testid="bypass-add-btn"
              @click="addBypassEntry"
            >
              <el-icon class="mr-1"><Plus /></el-icon>添加条目
            </el-button>
          </div>

          <p v-if="bypassEntries.length === 0" class="text-xs text-text-secondary mt-2 mb-0" data-testid="bypass-empty">
            暂无条目
          </p>
          <div
            v-for="(entry, i) in bypassEntries"
            :key="i"
            class="flex items-center gap-2 mt-2"
            data-testid="bypass-row"
          >
            <el-input
              :model-value="entry"
              style="width: 360px"
              placeholder="如 *.internal.corp 或 10.*"
              :aria-label="`不走代理的条目 ${i + 1}`"
              @update:model-value="(v: string) => { bypassEntries[i] = v; errors.bypass = ''; }"
            />
            <el-button
              size="small"
              text
              type="danger"
              :aria-label="`删除条目 ${entry || i + 1}`"
              data-testid="bypass-remove-btn"
              @click="removeBypassEntry(i)"
            >
              <el-icon><Delete /></el-icon>
            </el-button>
          </div>
          <!-- 错误就近左对齐：旁路行在左侧，提示跟着行走（右对齐会远离归属行） -->
          <p class="text-xs mt-1 mb-0 h-4" style="color: var(--el-color-danger)" data-testid="bypass-error">
            {{ errors.bypass }}
          </p>
        </div>

        <div class="flex items-center justify-between py-3">
          <p class="text-xs text-text-secondary m-0">
            支持 HTTP/HTTPS 代理；代理不可达时请求将直接失败，不会绕开代理直连（命中旁路条目的除外）。
          </p>
          <el-button type="primary" :loading="saving" :disabled="saving" data-testid="save-btn" @click="save">
            保存
          </el-button>
        </div>
      </div>
    </el-card>
  </div>
</template>
