<script setup lang="ts">
/**
 * API 密钥面板 — 设置页「API 密钥」分类
 * 结构：
 *   1. 顶部只读主密钥卡片（R13：不可删除 / 不可停用，只能重新生成）
 *   2. 子密钥列表（R9：名称 / 掩码 / 状态 / 创建时间 / 最后使用 / 备注 / 操作）
 *   3. 创建与 roll 的一次性明文弹窗（Q3：关闭后不可再次查看）
 * 写法对齐 ApiServerPanel.vue：Element Plus 组件 + Tailwind 布局，颜色全部走 --el-* / 主题 token。
 */
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';
import { ElMessage, ElMessageBox } from 'element-plus';
import { Delete, Edit, Key, Plus, Refresh } from '@element-plus/icons-vue';
import { apiKeysApi } from '@/services/apiKeysApi';
import { apiServerApi } from '@/services/apiServerApi';
import type { ApiKeyItem, CreateApiKeyRequest } from '@/types/apiKey';
import type { ApiServerConfig } from '@/types/apiServer';

/** 明文弹窗中「复制」按钮的 id（HTML 字符串无法直接绑事件，改用 document 事件委托） */
const PLAIN_COPY_BUTTON_ID = 'forge-api-key-copy-plain';

/** 子密钥列表 */
const items = ref<ApiKeyItem[]>([]);
/** 主密钥信息（来自 /api/api-server/status，仅掩码） */
const master = ref<ApiServerConfig | null>(null);
const loading = ref(false);
const loaded = ref(false);
const error = ref('');
const submitting = ref(false);

/** 新建 / 编辑弹窗：editingId 为 null 表示新建 */
const dialogVisible = ref(false);
const editingId = ref<number | null>(null);
const formName = ref('');
const formRemark = ref('');
const formExpiresAt = ref('');

/** 最近一次生成的明文，仅用于明文弹窗内的复制按钮 */
let lastPlainKey = '';

const dialogTitle = computed(() => (editingId.value === null ? '新建密钥' : '编辑密钥'));
const masterMasked = computed(() => master.value?.apiKeyMasked || '—');

/** 数字补零，用于日期格式化 */
function pad(value: number): string {
  return String(value).padStart(2, '0');
}

/** 时间格式化：空值或非法值统一显示占位符 */
function formatDateTime(value?: string | null): string {
  if (!value) return '—';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '—';
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(
    date.getHours(),
  )}:${pad(date.getMinutes())}`;
}

/** ISO 时间转日期选择器的输入值（YYYY-MM-DD HH:mm:ss） */
function toInputValue(iso?: string | null): string {
  if (!iso) return '';
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '';
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(
    date.getHours(),
  )}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`;
}

/** 日期选择器的输入值转 ISO 字符串；空或非法返回 null（表示不过期） */
function toIsoOrNull(value: string): string | null {
  if (!value) return null;
  const date = new Date(value.replace(' ', 'T'));
  return Number.isNaN(date.getTime()) ? null : date.toISOString();
}

/** 状态文案 */
function statusText(row: ApiKeyItem): string {
  if (row.isExpired) return '已过期';
  return row.enabled ? '启用' : '已停用';
}

/** 状态标签类型 */
function statusType(row: ApiKeyItem): 'success' | 'danger' | 'info' {
  if (row.isExpired) return 'danger';
  return row.enabled ? 'success' : 'info';
}

/** HTML 转义：明文虽为服务端生成的 hex 串，仍按不可信内容处理 */
function escapeHtml(value: string): string {
  return value.replace(/[&<>"']/g, (ch) => {
    switch (ch) {
      case '&':
        return '&amp;';
      case '<':
        return '&lt;';
      case '>':
        return '&gt;';
      case '"':
        return '&quot;';
      default:
        return '&#39;';
    }
  });
}

/** 复制文本到剪贴板 */
async function copyText(text: string): Promise<void> {
  if (!text) return;
  try {
    await navigator.clipboard.writeText(text);
    ElMessage.success({ message: '已复制到剪贴板', offset: 60 });
  } catch {
    ElMessage.error({ message: '复制失败，请手动选择复制', offset: 60 });
  }
}

/**
 * 明文弹窗：展示一次性明文 + 复制按钮 + 「关闭后不可再次查看」警示。
 * 明文只在创建 / roll 的当次响应出现，关闭后列表只剩掩码。
 */
function showPlainKeyDialog(title: string, plainKey: string, authHeader: string): void {
  lastPlainKey = plainKey;
  const header = authHeader || `Authorization: Bearer ${plainKey}`;
  const html = [
    `<p style="margin:0 0 12px;color:var(--el-color-error)">请立即复制并妥善保存，关闭本窗口后不可再次查看。</p>`,
    `<div style="display:flex;align-items:center;gap:8px;margin:0 0 12px">`,
    `<code style="flex:1;word-break:break-all;padding:8px;border-radius:4px;background-color:var(--el-fill-color-light);color:var(--el-text-color-primary)">${escapeHtml(
      plainKey,
    )}</code>`,
    `<button id="${PLAIN_COPY_BUTTON_ID}" type="button" style="flex:none;padding:6px 12px;cursor:pointer;border-radius:4px;border:1px solid var(--el-border-color);background-color:var(--el-bg-color);color:var(--el-text-color-regular)">复制</button>`,
    `</div>`,
    `<p style="margin:0;color:var(--el-text-color-secondary)">授权标头：${escapeHtml(header)}</p>`,
  ].join('');

  void ElMessageBox.alert(html, title, {
    confirmButtonText: '我已保存',
    dangerouslyUseHTMLString: true,
    customClass: 'api-key-plain-dialog',
  });
}

/** 明文弹窗复制按钮的事件委托：HTML 字符串内无法直接绑定 Vue 事件 */
function onDocumentClick(event: MouseEvent): void {
  const target = event.target as HTMLElement | null;
  if (target && typeof target.closest === 'function' && target.closest(`#${PLAIN_COPY_BUTTON_ID}`)) {
    void copyText(lastPlainKey);
  }
}

/** 加载子密钥列表 */
async function loadList(): Promise<void> {
  items.value = await apiKeysApi.list();
}

/** 加载主密钥信息（失败不阻塞列表，仅提示） */
async function loadMaster(): Promise<void> {
  try {
    master.value = await apiServerApi.getConfig();
  } catch (e) {
    // 401 等认证异常：保留已有值，不阻塞子密钥列表渲染
    console.warn('[ApiKeys] 主密钥信息加载失败：', e);
  }
}

/** 刷新全部数据 */
async function refresh(): Promise<void> {
  loading.value = true;
  error.value = '';
  try {
    await Promise.all([loadList(), loadMaster()]);
  } catch (e) {
    error.value = e instanceof Error ? e.message : '加载密钥列表失败';
  } finally {
    loading.value = false;
    loaded.value = true;
  }
}

/** 打开新建弹窗 */
function openCreate(): void {
  editingId.value = null;
  formName.value = '';
  formRemark.value = '';
  formExpiresAt.value = '';
  dialogVisible.value = true;
}

/** 打开编辑（重命名 / 备注 / 过期时间）弹窗 */
function openEdit(row: ApiKeyItem): void {
  editingId.value = row.id;
  formName.value = row.name;
  formRemark.value = row.remark || '';
  formExpiresAt.value = toInputValue(row.expiresAt);
  dialogVisible.value = true;
}

/** 提交新建 / 编辑 */
async function submitForm(): Promise<void> {
  const name = formName.value.trim();
  if (editingId.value !== null && !name) {
    ElMessage.warning({ message: '请输入密钥名称', offset: 60 });
    return;
  }

  const payload: CreateApiKeyRequest = {
    name,
    remark: formRemark.value.trim(),
    expiresAt: toIsoOrNull(formExpiresAt.value),
  };

  submitting.value = true;
  try {
    if (editingId.value === null) {
      const result = await apiKeysApi.create(payload);
      dialogVisible.value = false;
      await refresh();
      showPlainKeyDialog('密钥已创建', result.plainKey, result.authHeader);
    } else {
      await apiKeysApi.update(editingId.value, payload);
      dialogVisible.value = false;
      ElMessage.success({ message: '密钥已更新', offset: 60 });
      await refresh();
    }
  } catch (e) {
    ElMessage.error({
      message: `保存失败：${e instanceof Error ? e.message : '未知错误'}`,
      offset: 60,
    });
  } finally {
    submitting.value = false;
  }
}

/** 启停子密钥（临时吊销无需二次确认，可再启用） */
async function toggleKey(row: ApiKeyItem, enabled: boolean): Promise<void> {
  try {
    await apiKeysApi.toggle(row.id, enabled);
    ElMessage.success({ message: enabled ? '密钥已启用' : '密钥已停用', offset: 60 });
    await refresh();
  } catch (e) {
    ElMessage.error({
      message: `${enabled ? '启用' : '停用'}失败：${e instanceof Error ? e.message : '未知错误'}`,
      offset: 60,
    });
  }
}

/** 重新生成子密钥明文（旧值立即失效，需二次确认） */
async function rollKey(row: ApiKeyItem): Promise<void> {
  try {
    await ElMessageBox.confirm(
      `重新生成后旧密钥立即失效，使用它的客户端需要同步更新。是否继续？`,
      `重新生成「${row.name}」`,
      {
        confirmButtonText: '重新生成',
        cancelButtonText: '取消',
        type: 'warning',
      },
    );
  } catch {
    // 用户取消
    return;
  }

  try {
    const result = await apiKeysApi.roll(row.id);
    await refresh();
    showPlainKeyDialog('密钥已重新生成', result.plainKey, result.authHeader);
  } catch (e) {
    ElMessage.error({
      message: `重新生成失败：${e instanceof Error ? e.message : '未知错误'}`,
      offset: 60,
    });
  }
}

/** 删除子密钥（二次确认，硬删除不可恢复） */
async function removeKey(row: ApiKeyItem): Promise<void> {
  try {
    await ElMessageBox.confirm(
      `确定删除密钥「${row.name}」吗？删除后立即失效且不可恢复。`,
      '删除确认',
      {
        confirmButtonText: '删除',
        cancelButtonText: '取消',
        type: 'warning',
      },
    );
  } catch {
    // 用户取消
    return;
  }

  try {
    await apiKeysApi.remove(row.id);
    ElMessage.success({ message: '密钥已删除', offset: 60 });
    await refresh();
  } catch (e) {
    ElMessage.error({
      message: `删除失败：${e instanceof Error ? e.message : '未知错误'}`,
      offset: 60,
    });
  }
}

/** 重新生成主密钥（复用 apiServerApi，它已会同步 localStorage） */
async function regenerateMaster(): Promise<void> {
  try {
    await ElMessageBox.confirm(
      '重新生成后旧主密钥立即失效，本机主界面与其它已配置的客户端都需要更新。是否继续？',
      '重新生成主密钥',
      {
        confirmButtonText: '重新生成',
        cancelButtonText: '取消',
        type: 'warning',
      },
    );
  } catch {
    // 用户取消
    return;
  }

  try {
    const config = await apiServerApi.regenerateKey();
    master.value = config;
    if (config.apiKeyPlain) {
      showPlainKeyDialog('主密钥已重新生成', config.apiKeyPlain, config.authHeader);
    } else {
      ElMessage.success({ message: '主密钥已重新生成', offset: 60 });
    }
  } catch (e) {
    ElMessage.error({
      message: `重新生成失败：${e instanceof Error ? e.message : '未知错误'}`,
      offset: 60,
    });
  }
}

onMounted(() => {
  document.addEventListener('click', onDocumentClick);
  void refresh();
});

onBeforeUnmount(() => {
  document.removeEventListener('click', onDocumentClick);
});
</script>

<template>
  <div class="max-w-[900px] space-y-6">
    <!-- 页头 -->
    <div class="flex items-start justify-between pb-4 border-b" style="border-color: var(--el-border-color-light)">
      <div>
        <h2 class="text-2xl font-bold m-0 leading-tight" style="color: var(--el-text-color-primary)">
          API 密钥
        </h2>
        <p class="text-sm mt-1 mb-0" style="color: var(--el-text-color-secondary)">
          主密钥用于本机主界面，子密钥用于第三方客户端接入；明文仅在创建时展示一次
        </p>
      </div>
      <el-button type="primary" :icon="Plus" @click="openCreate">新建密钥</el-button>
    </div>

    <!-- 首次加载：骨架屏 -->
    <el-skeleton v-if="loading && !loaded" :rows="4" animated />

    <!-- 错误提示 -->
    <el-alert
      v-if="error && !loading"
      :title="error"
      type="error"
      show-icon
      :closable="false"
    />

    <!-- 主密钥（只读） -->
    <el-card shadow="never">
      <template #header>
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-2">
            <el-icon :size="16" style="color: var(--el-color-primary)"><Key /></el-icon>
            <span class="text-base font-semibold" style="color: var(--el-text-color-primary)">主密钥</span>
            <el-tag type="primary" size="small" disable-transitions>主密钥 · 本机主界面使用</el-tag>
          </div>
          <el-button
            size="small"
            type="primary"
            :icon="Refresh"
            :loading="loading"
            @click="regenerateMaster"
          >
            重新生成
          </el-button>
        </div>
      </template>
      <p class="text-sm mt-0 mb-3" style="color: var(--el-text-color-secondary)">
        由本机自动生成并加密保存，用于主界面与 API 访问。<strong>不可删除、不可停用</strong>，只能重新生成。
      </p>
      <el-input :model-value="masterMasked" readonly class="font-mono" />
    </el-card>

    <!-- 子密钥列表 -->
    <el-card shadow="never">
      <template #header>
        <div class="flex items-center justify-between">
          <span class="text-base font-semibold" style="color: var(--el-text-color-primary)">子密钥</span>
          <el-button size="small" :icon="Refresh" :loading="loading" @click="refresh">刷新</el-button>
        </div>
      </template>

      <el-table :data="items" row-key="id" size="small" empty-text="暂无子密钥">
        <el-table-column prop="name" label="名称" min-width="120" />
        <el-table-column label="掩码" min-width="180">
          <template #default="{ row }">
            <span v-if="row.canDecrypt" class="font-mono">{{ row.maskedKey || '—' }}</span>
            <span v-else style="color: var(--el-color-warning)">无法在本机解密，请重新生成</span>
          </template>
        </el-table-column>
        <el-table-column label="状态" width="100">
          <template #default="{ row }">
            <el-tag :type="statusType(row)" size="small" disable-transitions>{{ statusText(row) }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="创建时间" width="140">
          <template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template>
        </el-table-column>
        <el-table-column label="最后使用" width="140">
          <template #default="{ row }">{{ formatDateTime(row.lastUsedAt) }}</template>
        </el-table-column>
        <el-table-column prop="remark" label="备注" min-width="120">
          <template #default="{ row }">{{ row.remark || '—' }}</template>
        </el-table-column>
        <el-table-column label="操作" width="260" fixed="right">
          <template #default="{ row }">
            <div class="flex items-center gap-1">
              <el-button
                size="small"
                text
                type="primary"
                :icon="Edit"
                @click="openEdit(row)"
              >
                重命名
              </el-button>
              <el-button
                size="small"
                text
                :type="row.enabled ? 'warning' : 'success'"
                @click="toggleKey(row, !row.enabled)"
              >
                {{ row.enabled ? '停用' : '启用' }}
              </el-button>
              <el-button
                size="small"
                text
                type="primary"
                :icon="Refresh"
                @click="rollKey(row)"
              >
                重新生成
              </el-button>
              <el-button
                size="small"
                text
                type="danger"
                :icon="Delete"
                @click="removeKey(row)"
              >
                删除
              </el-button>
            </div>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <!-- 新建 / 编辑弹窗 -->
    <el-dialog v-model="dialogVisible" :title="dialogTitle" width="480px" append-to-body>
      <el-form label-width="88px">
        <el-form-item label="名称">
          <el-input v-model="formName" placeholder="留空则由后端自动生成" maxlength="50" />
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="formRemark" placeholder="用途说明，如：NAS 同步脚本" maxlength="200" />
        </el-form-item>
        <el-form-item label="过期时间">
          <el-date-picker
            v-model="formExpiresAt"
            type="datetime"
            placeholder="留空表示不过期"
            value-format="YYYY-MM-DD HH:mm:ss"
            style="width: 100%"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="submitting" @click="submitForm">确定</el-button>
      </template>
    </el-dialog>
  </div>
</template>
