<script setup lang="ts">
/**
 * dsh MCP 服务器管理面板（v2.3.0）：独立选项卡，可视化管理 DeepSeek Harness 补丁层
 * cordis.patch.yml 里 @deepseek-ai/dsh-mcp-client 的全部 MCP 条目（列出/新增/编辑/启停/删除/去重）。
 * 参考成熟插件 dsh-mcp-manager 的交互：服务器卡片 + 新增/编辑表单 + 启停开关 + 两步删除。
 */
import { ref, computed, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import {
  fetchDshMcpConfig,
  addDshServer,
  updateDshServer,
  deleteDshServer,
  toggleDshServer,
  dedupeDshServers,
} from './api/dsh'
import type { DshMcpConfigDto, DshMcpServerDto } from './types/dsh'

const cfg = ref<DshMcpConfigDto | null>(null)
const loading = ref(false)
const profile = ref('desktop')

const servers = computed(() => cfg.value?.servers ?? [])
const dupes = computed(() => cfg.value?.duplicateIds ?? [])

const dialog = ref(false)
const mode = ref<'add' | 'edit'>('add')
const saving = ref(false)
const form = ref({
  id: '',
  serverName: '',
  transport: 'streamable-http',
  url: '',
  command: '',
  args: '',
  headers: '',
  enabled: true,
})

async function load(): Promise<void> {
  loading.value = true
  try {
    const c = await fetchDshMcpConfig(profile.value)
    if (c) cfg.value = c
  } catch (e: unknown) {
    ElMessage.error(e instanceof Error ? e.message : '加载 dsh MCP 配置失败')
  } finally {
    loading.value = false
  }
}

function useForgeSelfUrl(): void {
  if (cfg.value?.defaultUrl) form.value.url = cfg.value.defaultUrl
}

function openAdd(): void {
  mode.value = 'add'
  form.value = {
    id: '',
    serverName: '',
    transport: 'streamable-http',
    url: cfg.value?.defaultUrl ?? '',
    command: '',
    args: '',
    headers: '',
    enabled: true,
  }
  dialog.value = true
}

function openEdit(s: DshMcpServerDto): void {
  mode.value = 'edit'
  form.value = {
    id: s.id,
    serverName: s.serverName,
    transport: s.transport || 'streamable-http',
    url: s.url,
    command: s.command,
    args: (s.args ?? []).join(' '),
    headers: Object.entries(s.headers ?? {}).map(([k, v]) => `${k}: ${v}`).join('\n'),
    enabled: s.enabled,
  }
  dialog.value = true
}

function parseHeaders(text: string): Record<string, string> {
  const out: Record<string, string> = {}
  for (const line of text.split('\n')) {
    const i = line.indexOf(':')
    if (i > 0) out[line.slice(0, i).trim()] = line.slice(i + 1).trim()
  }
  return out
}

async function submit(): Promise<void> {
  const name = form.value.serverName.trim()
  if (!name) {
    ElMessage.warning('请填写 serverName')
    return
  }
  const body = {
    id: form.value.id.trim() || undefined,
    serverName: name,
    transport: form.value.transport,
    url: form.value.url.trim(),
    command: form.value.command.trim(),
    args: form.value.args,
    headers: parseHeaders(form.value.headers),
    enabled: form.value.enabled,
  }
  saving.value = true
  try {
    const c = mode.value === 'add'
      ? await addDshServer(profile.value, body)
      : await updateDshServer(profile.value, form.value.id, body)
    if (c) {
      cfg.value = c
      dialog.value = false
      ElMessage.success(mode.value === 'add' ? '已新增 dsh MCP 服务器' : '已更新 dsh MCP 服务器')
    }
  } catch (e: unknown) {
    ElMessage.error(e instanceof Error ? e.message : '保存失败')
  } finally {
    saving.value = false
  }
}

async function toggle(s: DshMcpServerDto, enabled: boolean | string | number): Promise<void> {
  try {
    const c = await toggleDshServer(profile.value, s.id, Boolean(enabled))
    if (c) cfg.value = c
  } catch (e: unknown) {
    ElMessage.error(e instanceof Error ? e.message : '切换失败')
    await load()
  }
}

async function remove(s: DshMcpServerDto): Promise<void> {
  try {
    await ElMessageBox.confirm(
      `确认删除 dsh MCP 服务器「${s.serverName || s.id}」？其条目将从 cordis.patch.yml 移除（写入前自动备份 .bak）。`,
      '删除确认',
      { type: 'warning', confirmButtonText: '删除', cancelButtonText: '取消' },
    )
  } catch {
    return
  }
  try {
    const c = await deleteDshServer(profile.value, s.id)
    if (c) cfg.value = c
    ElMessage.success('已删除')
  } catch (e: unknown) {
    ElMessage.error(e instanceof Error ? e.message : '删除失败')
  }
}

async function dedupe(): Promise<void> {
  try {
    await ElMessageBox.confirm(
      `将删除重复的条目（同一 id 只保留第一条）：${dupes.value.join('、')}。写入前自动备份 .bak。`,
      '一键去重',
      { type: 'warning', confirmButtonText: '去重', cancelButtonText: '取消' },
    )
  } catch {
    return
  }
  try {
    const c = await dedupeDshServers(profile.value)
    if (c) cfg.value = c
    ElMessage.success('已去重')
  } catch (e: unknown) {
    ElMessage.error(e instanceof Error ? e.message : '去重失败')
  }
}

onMounted(load)
</script>

<template>
  <div class="dsh-panel">
    <div class="dsh-head">
      <div class="dsh-head-text">
        <h2 class="dsh-title">dsh MCP 服务器</h2>
        <p class="dsh-desc">
          可视化管理 DeepSeek Harness 补丁层
          <code class="mono-inline">cordis.patch.yml</code> 里
          <code class="mono-inline">@deepseek-ai/dsh-mcp-client</code> 的全部 MCP 条目：新增 / 编辑 / 启停 / 删除。
        </p>
      </div>
      <div class="dsh-head-actions">
        <ElButton size="small" :loading="loading" @click="load">刷新</ElButton>
        <ElButton size="small" type="primary" @click="openAdd">新增服务器</ElButton>
      </div>
    </div>

    <div class="dsh-meta">
      <span>profile：<code class="mono-inline">{{ cfg?.profile || 'desktop' }}</code></span>
      <span>条目数：{{ servers.length }}</span>
      <span class="dsh-path">文件：<code class="mono-inline">{{ cfg?.configPath || '-' }}</code></span>
    </div>

    <div v-if="cfg?.lastError" class="dsh-error">读取 dsh 配置失败：{{ cfg.lastError }}</div>

    <div v-if="dupes.length" class="dsh-warn">
      <span>检测到重复条目 id：<code class="mono-inline">{{ dupes.join('、') }}</code>（同一 id 出现多次，建议去重）</span>
      <ElButton size="small" type="warning" plain @click="dedupe">一键去重</ElButton>
    </div>

    <div v-loading="loading" class="dsh-list">
      <div v-if="!loading && servers.length === 0" class="dsh-empty">
        尚未配置任何 dsh MCP 服务器，点击「新增服务器」开始。
      </div>

      <div
        v-for="s in servers"
        :key="s.id + ':' + s.line"
        class="dsh-card"
        :class="{ 'dsh-card-off': !s.enabled }"
      >
        <div class="dsh-card-main">
          <div class="dsh-card-top">
            <span class="dsh-name">{{ s.serverName || s.id }}</span>
            <ElTag size="small" :type="s.transport === 'stdio' ? 'info' : 'success'">
              {{ s.transport || '-' }}
            </ElTag>
            <ElTag v-if="!s.enabled" size="small" type="warning">已停用</ElTag>
          </div>
          <div class="dsh-endpoint">
            <code class="mono-inline">
              {{ s.transport === 'stdio' ? ((s.command || '') + ' ' + (s.args || []).join(' ')).trim() : s.url }}
            </code>
          </div>
          <div class="dsh-sub">id：<code class="mono-inline">{{ s.id }}</code> · 第 {{ s.line }} 行</div>
        </div>
        <div class="dsh-card-ops">
          <ElSwitch
            :model-value="s.enabled"
            @change="(v: string | number | boolean) => toggle(s, v)"
          />
          <ElButton size="small" @click="openEdit(s)">编辑</ElButton>
          <ElButton size="small" type="danger" plain @click="remove(s)">删除</ElButton>
        </div>
      </div>
    </div>

    <ElDialog
      v-model="dialog"
      :title="mode === 'add' ? '新增 dsh MCP 服务器' : '编辑 dsh MCP 服务器'"
      width="560px"
    >
      <div class="dsh-form">
        <div class="dsh-form-row">
          <label class="dsh-label">serverName</label>
          <ElInput v-model="form.serverName" placeholder="例如 ForgeSelf" />
        </div>

        <div class="dsh-form-row">
          <label class="dsh-label">条目 id</label>
          <ElInput v-model="form.id" :disabled="mode === 'edit'" placeholder="留空自动生成 mcp-<serverName>" />
        </div>

        <div class="dsh-form-row">
          <label class="dsh-label">传输类型</label>
          <select v-model="form.transport" class="dsh-select">
            <option value="streamable-http">streamable-http</option>
            <option value="http-sse">http-sse</option>
            <option value="stdio">stdio</option>
          </select>
        </div>

        <template v-if="form.transport === 'stdio'">
          <div class="dsh-form-row">
            <label class="dsh-label">command</label>
            <ElInput v-model="form.command" placeholder="例如 npx" />
          </div>
          <div class="dsh-form-row">
            <label class="dsh-label">args</label>
            <ElInput v-model="form.args" placeholder="空格分隔，例如 -y @modelcontextprotocol/server-filesystem D:\data" />
          </div>
        </template>
        <template v-else>
          <div class="dsh-form-row">
            <label class="dsh-label">MCP 地址</label>
            <ElInput v-model="form.url" placeholder="http://127.0.0.1:51888/mcp" />
          </div>
          <div class="dsh-form-row dsh-form-row-inline">
            <ElButton size="small" @click="useForgeSelfUrl">填入当前网关地址</ElButton>
          </div>
        </template>

        <div class="dsh-form-row">
          <label class="dsh-label">请求头</label>
          <ElInput
            v-model="form.headers"
            type="textarea"
            :rows="2"
            placeholder="每行一个，例如 Authorization: Bearer xxx（可留空）"
          />
        </div>

        <div class="dsh-form-row dsh-form-row-inline">
          <label class="dsh-label">启用</label>
          <ElSwitch v-model="form.enabled" />
        </div>
      </div>

      <template #footer>
        <ElButton @click="dialog = false">取消</ElButton>
        <ElButton type="primary" :loading="saving" @click="submit">保存</ElButton>
      </template>
    </ElDialog>
  </div>
</template>

<style scoped>
.dsh-panel { display: flex; flex-direction: column; gap: 14px; }
.dsh-head { display: flex; align-items: flex-start; justify-content: space-between; gap: 16px; flex-shrink: 0; }
.dsh-title { margin: 0 0 4px; font-size: 16px; font-weight: 600; }
.dsh-desc { margin: 0; font-size: 12px; line-height: 1.6; opacity: 0.72; max-width: 720px; }
.dsh-head-actions { display: flex; gap: 8px; flex-shrink: 0; }
.dsh-meta { display: flex; flex-wrap: wrap; gap: 16px; font-size: 12px; opacity: 0.75; flex-shrink: 0; }
.dsh-path { word-break: break-all; }
.dsh-error { padding: 8px 12px; border-radius: 6px; font-size: 12px; background: rgba(245, 108, 108, 0.12); color: #f56c6c; }
.dsh-warn {
  display: flex; align-items: center; justify-content: space-between; gap: 12px;
  padding: 8px 12px; border-radius: 6px; font-size: 12px;
  background: rgba(230, 162, 60, 0.12); color: #e6a23c;
}
.dsh-list { display: flex; flex-direction: column; gap: 10px; min-height: 80px; }
.dsh-empty { padding: 32px; text-align: center; font-size: 13px; opacity: 0.55; }
.dsh-card {
  display: flex; align-items: center; justify-content: space-between; gap: 16px;
  padding: 12px 14px; border-radius: 8px; border: 1px solid rgba(255, 255, 255, 0.08);
  background: rgba(255, 255, 255, 0.02);
}
.dsh-card-off { opacity: 0.5; }
.dsh-card-main { min-width: 0; flex: 1; }
.dsh-card-top { display: flex; align-items: center; gap: 8px; margin-bottom: 4px; }
.dsh-name { font-size: 14px; font-weight: 600; }
.dsh-endpoint { font-size: 12px; opacity: 0.85; word-break: break-all; }
.dsh-sub { font-size: 11px; opacity: 0.55; margin-top: 2px; }
.dsh-card-ops { display: flex; align-items: center; gap: 8px; flex-shrink: 0; }
.dsh-form { display: flex; flex-direction: column; gap: 12px; }
.dsh-form-row { display: flex; flex-direction: column; gap: 6px; }
.dsh-form-row-inline { flex-direction: row; align-items: center; gap: 12px; }
.dsh-label { font-size: 12px; opacity: 0.75; }
.dsh-select {
  height: 32px; border-radius: 4px; padding: 0 8px; font-size: 13px;
  background: rgba(255, 255, 255, 0.04); color: inherit;
  border: 1px solid rgba(255, 255, 255, 0.16);
}
.mono-inline { font-family: var(--el-font-family, monospace); font-size: 0.92em; }
</style>
