<script setup lang="ts">
/**
 * 权限审计视图 — B4（只读）：策略决策 + 工具审批流水
 * GET /api/permissions/audit（裸封套 items/total/page/pageSize；toolName/kind 过滤；page≥1、pageSize 1..200）
 */
import { onMounted, ref } from 'vue';
import { Refresh, Search } from '@element-plus/icons-vue';
import { queryAudit } from '@/services/permissionPolicyApi';
import type { PermissionAuditItem, PermissionAuditKind, PermissionAuditPage } from '@/services/permissionPolicyApi';

const KIND_LABEL: Record<PermissionAuditKind, string> = {
  'policy-decision': '策略决策',
  'tool-approval': '人工审批'
};

const toolFilter = ref('');
const kindFilter = ref<PermissionAuditKind | ''>('');
const page = ref(1);
const pageSize = ref(20);
const items = ref<PermissionAuditItem[]>([]);
const total = ref(0);
const loading = ref(false);
const error = ref('');
const loadedOnce = ref(false);

function fmtTime(t: string): string {
  const d = new Date(t);
  if (Number.isNaN(d.getTime())) return t;
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getMonth() + 1}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`;
}

function statusLabel(item: PermissionAuditItem): string {
  if (item.kind === 'policy-decision') {
    return item.status === 'Allow' ? '放行' : item.status === 'Ask' ? '转审批' : item.status === 'Deny' ? '拒绝' : item.status;
  }
  return (
    item.status === 'Approved' ? '已批准'
      : item.status === 'Denied' ? '已拒绝'
        : item.status === 'Expired' ? '已过期'
          : item.status === 'Timeout' ? '已超时'
            : item.status
  );
}

function sourceLabel(s: string): string {
  if (s === 'default-policy') return '内置默认';
  if (s.startsWith('profile:')) return `策略 #${s.slice('profile:'.length)}`;
  return s;
}

async function fetchPage(targetPage = page.value) {
  loading.value = true;
  error.value = '';
  try {
    const data: PermissionAuditPage = await queryAudit({
      toolName: toolFilter.value || undefined,
      kind: kindFilter.value || undefined,
      page: targetPage,
      pageSize: pageSize.value
    });
    items.value = data.items;
    total.value = data.total;
    page.value = data.page;
    pageSize.value = data.pageSize;
    loadedOnce.value = true;
  } catch (e) {
    error.value = e instanceof Error ? e.message : '查询失败';
  } finally {
    loading.value = false;
  }
}

function applyFilters() {
  void fetchPage(1);
}

function changePage(target: number) {
  void fetchPage(target);
}

onMounted(() => {
  void fetchPage(1);
});
</script>

<template>
  <div class="pa-view">
    <!-- 过滤工具行 -->
    <div class="pa-filters">
      <el-input
        v-model="toolFilter"
        size="small"
        placeholder="按工具名过滤，如 write_file"
        clearable
        class="pa-tool-input"
        @keyup.enter="applyFilters"
        @clear="applyFilters"
      />
      <el-select
        v-model="kindFilter"
        size="small"
        placeholder="全部类型"
        clearable
        class="pa-kind-select"
        @change="applyFilters"
      >
        <el-option label="策略决策" value="policy-decision" />
        <el-option label="人工审批" value="tool-approval" />
      </el-select>
      <el-button size="small" :icon="Search" @click="applyFilters">查询</el-button>
      <el-button size="small" :icon="Refresh" text @click="fetchPage()">刷新</el-button>
      <span class="pa-total">共 {{ total }} 条</span>
    </div>

    <el-alert
      v-if="error"
      :title="error"
      type="error"
      show-icon
      :closable="false"
      class="pa-alert"
    >
      <el-button size="small" @click="fetchPage()">重试</el-button>
    </el-alert>

    <el-skeleton v-if="loading && items.length === 0" :rows="5" animated />

    <div v-if="!loading || items.length > 0" class="pa-table-wrap">
      <table class="pa-table" aria-label="权限审计流水">
        <thead>
          <tr>
            <th class="pa-col-time">时间</th>
            <th class="pa-col-kind">类型</th>
            <th class="pa-col-tool">工具</th>
            <th class="pa-col-agent">Agent</th>
            <th class="pa-col-status">结果</th>
            <th class="pa-col-source">来源</th>
            <th class="pa-col-reason">原因</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="it in items" :key="`${it.kind}-${it.id}`">
            <td class="pa-col-time pa-mono">{{ fmtTime(it.time) }}</td>
            <td class="pa-col-kind">
              <span class="pa-kind-tag" :data-kind="it.kind">{{ KIND_LABEL[it.kind] }}</span>
            </td>
            <td class="pa-col-tool pa-mono" :title="it.toolName">{{ it.toolName }}</td>
            <td class="pa-col-agent" :title="it.agentId ?? ''">{{ it.agentId || '—' }}</td>
            <td class="pa-col-status">
              <span class="pa-status" :data-status="it.status">{{ statusLabel(it) }}</span>
            </td>
            <td class="pa-col-source">{{ sourceLabel(it.source) }}</td>
            <td class="pa-col-reason" :title="it.reason ?? ''">{{ it.reason || '—' }}</td>
          </tr>
        </tbody>
      </table>

      <div v-if="loadedOnce && !loading && items.length === 0 && !error" class="pa-empty">
        当前过滤条件下没有记录
      </div>
    </div>

    <!-- 分页 -->
    <div v-if="total > 0" class="pa-pager">
      <el-pagination
        size="small"
        layout="prev, pager, next, sizes"
        :total="total"
        :current-page="page"
        :page-size="pageSize"
        :page-sizes="[20, 50, 100]"
        @current-change="changePage"
        @size-change="(s: number) => { pageSize = s; fetchPage(1) }"
      />
    </div>
  </div>
</template>

<style scoped>
.pa-view {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.pa-filters {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.pa-tool-input {
  width: 220px;
}

.pa-kind-select {
  width: 140px;
}

.pa-total {
  font-size: 12px;
  font-family: var(--font-mono);
  color: var(--el-text-color-secondary);
  margin-left: auto;
}

.pa-alert {
  margin: 0;
}

.pa-table-wrap {
  border: 1px solid var(--el-border-color-lighter);
  border-radius: var(--el-border-radius-base);
  overflow: hidden;
  overflow-x: auto;
}

.pa-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 12px;
}

.pa-table th {
  text-align: left;
  font-weight: 600;
  color: var(--el-text-color-secondary);
  background: var(--el-fill-color-light);
  padding: 7px 10px;
  border-bottom: 1px solid var(--el-border-color-light);
  white-space: nowrap;
}

.pa-table td {
  padding: 7px 10px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  color: var(--el-text-color-regular);
  vertical-align: top;
}

.pa-table tbody tr:last-child td {
  border-bottom: none;
}

.pa-col-time,
.pa-col-tool {
  font-family: var(--font-mono);
}

.pa-col-tool {
  max-width: 200px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.pa-col-reason {
  max-width: 280px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.pa-kind-tag {
  font-size: 10px;
  font-family: var(--font-mono);
  padding: 1px 6px;
  border-radius: var(--el-border-radius-small);
  border: 1px solid var(--el-border-color);
  color: var(--el-text-color-regular);
  white-space: nowrap;
}

.pa-status {
  font-size: 11px;
  color: var(--el-text-color-regular);
  white-space: nowrap;
}

.pa-status[data-status='Allow'],
.pa-status[data-status='Approved'] {
  color: var(--el-color-success);
}

.pa-status[data-status='Ask'] {
  color: var(--gold-700);
}

.pa-status[data-status='Deny'],
.pa-status[data-status='Denied'] {
  color: var(--cinnabar-400);
}

.pa-status[data-status='Expired'],
.pa-status[data-status='Timeout'] {
  color: var(--el-color-warning);
}

.pa-empty {
  padding: 28px 0;
  text-align: center;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.pa-pager {
  display: flex;
  justify-content: flex-end;
}

@media (max-width: 720px) {
  .pa-col-source,
  .pa-col-agent {
    display: none;
  }
}
</style>
