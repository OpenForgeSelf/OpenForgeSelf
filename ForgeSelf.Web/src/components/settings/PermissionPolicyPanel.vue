<script setup lang="ts">
/**
 * 权限策略面板 — B4 权限可配置化 UI（契约冻结版）
 * 策略配置（PermissionProfile 卡片 + 规则矩阵 + 规则级白名单）| 审计日志（PermissionAuditView）
 * 显式保存：编辑仅本地草稿，点「保存策略」PUT；409 后端原文逐字透出
 */
import { onMounted, ref } from 'vue';
import { ElMessage, ElMessageBox } from 'element-plus';
import { Plus, Delete, Refresh, Lock } from '@element-plus/icons-vue';
import {
  listProfiles,
  saveProfile,
  deleteProfile
} from '@/services/permissionPolicyApi';
import type {
  PermissionEffect,
  PermissionProfile,
  ToolPermissionRule
} from '@/services/permissionPolicyApi';
import PermissionAuditView from '@/components/settings/PermissionAuditView.vue';

const EFFECTS: PermissionEffect[] = ['Allow', 'Ask', 'Deny'];
const EFFECT_LABEL: Record<PermissionEffect, string> = { Allow: '放行', Ask: '转审批', Deny: '拒绝' };
const EMPTY_RULE_EFFECT: PermissionEffect = 'Ask';

const view = ref<'policy' | 'audit'>('policy');
const profiles = ref<PermissionProfile[]>([]);
const loading = ref(false);
const error = ref('');
const loadFailed = ref(false);

/** 展开卡片的本地草稿（深拷贝）；fingerprint 变化 = 脏 */
const expandedId = ref<number | null>(null);
const drafts = ref<Record<number, PermissionProfile>>({});
const savingIds = ref<Set<number>>(new Set());
/** 行内新建草稿（id=0）；null = 未在新建 */
const newDraft = ref<PermissionProfile | null>(null);
const newDraftError = ref('');

function fingerprint(p: PermissionProfile): string {
  return JSON.stringify({
    agentId: p.agentId,
    enabled: p.enabled,
    remark: p.remark ?? '',
    rules: [...p.rules]
      .map((r) => ({ t: r.toolName, e: r.effect, w: [r.pathAllowlist, r.domainAllowlist, r.commandAllowlist] }))
      .sort((a, b) => a.t.localeCompare(b.t))
  });
}

function isDirty(id: number): boolean {
  const draft = drafts.value[id];
  const origin = profiles.value.find((p) => p.id === id);
  if (!draft || !origin) return false;
  return fingerprint(draft) !== fingerprint(origin);
}

function draftOf(p: PermissionProfile): PermissionProfile {
  if (!drafts.value[p.id]) drafts.value[p.id] = JSON.parse(JSON.stringify(p)) as PermissionProfile;
  return drafts.value[p.id];
}

function effectCounts(p: PermissionProfile): Record<PermissionEffect, number> {
  const counts: Record<PermissionEffect, number> = { Allow: 0, Ask: 0, Deny: 0 };
  for (const r of p.rules) counts[r.effect] += 1;
  return counts;
}

function profileTitle(p: PermissionProfile): string {
  return p.agentId.trim() === '' ? '全局默认' : p.agentId;
}

async function load(manual = false) {
  if (manual) loading.value = true;
  error.value = '';
  loadFailed.value = false;
  try {
    profiles.value = await listProfiles();
    drafts.value = {};
    expandedId.value = null;
  } catch (e) {
    error.value = e instanceof Error ? e.message : '加载失败';
    loadFailed.value = true;
  } finally {
    loading.value = false;
  }
}

async function toggleExpand(p: PermissionProfile) {
  if (expandedId.value === p.id) {
    expandedId.value = null;
    return;
  }
  if (expandedId.value != null && isDirty(expandedId.value)) {
    const ok = await ElMessageBox.confirm('有未保存的变更，放弃并切换？', '未保存变更', {
      confirmButtonText: '放弃并切换',
      cancelButtonText: '继续编辑',
      type: 'warning'
    }).then(() => true).catch(() => false);
    if (!ok) return;
  }
  expandedId.value = p.id;
  draftOf(p);
}

// ===== 规则矩阵操作（全部作用于草稿） =====
function draftOfById(id: number): PermissionProfile {
  const origin = profiles.value.find((p) => p.id === id);
  if (!origin) throw new Error('策略不存在');
  return draftOf(origin);
}

function addRule(id: number, toolNameRaw: string): boolean {
  const toolName = toolNameRaw.trim();
  const draft = draftOfById(id);
  if (!toolName) return false;
  if (draft.rules.some((r) => r.toolName === toolName)) {
    ElMessage.warning({ message: `规则「${toolName}」已存在`, offset: 60 });
    return false;
  }
  draft.rules.push({
    toolName,
    effect: EMPTY_RULE_EFFECT,
    pathAllowlist: [],
    domainAllowlist: [],
    commandAllowlist: []
  });
  ElMessage.success({ message: '已添加', offset: 60 });
  return true;
}

function removeRule(id: number, toolName: string) {
  const draft = draftOfById(id);
  if (toolName === '*') return; // 兜底规则不可移除
  draft.rules = draft.rules.filter((r) => r.toolName !== toolName);
}

// ===== 规则级白名单（契约：ToolPermissionRule 三组） =====
const WL_GROUPS = [
  { key: 'pathAllowlist', label: '路径', placeholder: '前缀匹配，如 D:\\src\\ 或 /home/user/' },
  { key: 'domainAllowlist', label: '域名', placeholder: '相等或子域命中，如 example.com' },
  { key: 'commandAllowlist', label: '命令', placeholder: '前缀匹配，如 git status' }
] as const;

type WlGroupKey = (typeof WL_GROUPS)[number]['key'];

function addWhitelistEntry(rule: ToolPermissionRule, key: WlGroupKey, valueRaw: string): boolean {
  const value = valueRaw.trim();
  if (!value) return false;
  const list = rule[key];
  if (list.includes(value)) {
    ElMessage.warning({ message: '条目已存在', offset: 60 });
    return false;
  }
  list.push(value);
  return true;
}

function removeWhitelistEntry(rule: ToolPermissionRule, key: WlGroupKey, value: string) {
  const list = rule[key];
  const idx = list.indexOf(value);
  if (idx >= 0) list.splice(idx, 1);
}

/** Deny 汇总：原 effect 非 Deny → 草稿 Deny 的规则名 */
function newlyDeniedNames(id: number): string[] {
  const draft = drafts.value[id];
  const origin = profiles.value.find((p) => p.id === id);
  if (!draft || !origin) return [];
  return draft.rules
    .filter((r) => r.effect === 'Deny')
    .filter((r) => origin.rules.find((o) => o.toolName === r.toolName)?.effect !== 'Deny')
    .map((r) => (r.toolName === '*' ? '兜底（*）' : r.toolName));
}

// ===== 保存 / 删除 / 新建 =====
async function saveProfileDraft(id: number) {
  const draft = drafts.value[id];
  if (!draft) return;
  const denied = newlyDeniedNames(id);
  if (denied.length > 0) {
    const ok = await ElMessageBox.confirm(
      `以下工具将被拒绝执行：${denied.join('、')}。确认保存？`,
      '保存含 Deny 决策的策略',
      { confirmButtonText: '确认保存', cancelButtonText: '取消', type: 'warning' }
    ).then(() => true).catch(() => false);
    if (!ok) return;
  }
  savingIds.value.add(id);
  try {
    const saved = await saveProfile(draft);
    const idx = profiles.value.findIndex((p) => p.id === id);
    if (idx >= 0) profiles.value[idx] = saved;
    delete drafts.value[id];
    ElMessage.success({ message: '策略已保存', offset: 60 });
  } catch (e) {
    ElMessage.error({ message: e instanceof Error ? e.message : '保存失败', offset: 60 });
  } finally {
    savingIds.value.delete(id);
  }
}

async function removeProfile(p: PermissionProfile) {
  if (expandedId.value === p.id && isDirty(p.id)) {
    const giveUp = await ElMessageBox.confirm('有未保存的变更，放弃并继续删除？', '未保存变更', {
      confirmButtonText: '放弃并删除',
      cancelButtonText: '取消',
      type: 'warning'
    }).then(() => true).catch(() => false);
    if (!giveUp) return;
  }
  const ok = await ElMessageBox.confirm(
    '删除后该 Agent 将回落内置默认策略（沉默放行 + 高风险工具转审批），确认删除？',
    '删除策略',
    { confirmButtonText: '确认删除', cancelButtonText: '取消', type: 'warning' }
  ).then(() => true).catch(() => false);
  if (!ok) return;
  try {
    await deleteProfile(p.id);
    profiles.value = profiles.value.filter((x) => x.id !== p.id);
    delete drafts.value[p.id];
    if (expandedId.value === p.id) expandedId.value = null;
    ElMessage.success({ message: '已删除，已回落内置默认', offset: 60 });
  } catch (e) {
    ElMessage.error({ message: e instanceof Error ? e.message : '删除失败', offset: 60 });
  }
}

function startCreate() {
  const hasGlobal = profiles.value.some((p) => p.agentId.trim() === '');
  newDraft.value = {
    id: 0,
    agentId: '',
    enabled: true,
    rules: [{ toolName: '*', effect: EMPTY_RULE_EFFECT, pathAllowlist: [], domainAllowlist: [], commandAllowlist: [] }],
    remark: null,
    updatedAt: ''
  };
  newDraftError.value = '';
  if (hasGlobal) {
    ElMessage.info({ message: '提示：全局默认策略已存在，再次以空 Agent 保存将被拒绝', offset: 60 });
  }
}

/** 展开卡片的「添加规则」输入缓冲（按 profile id） */
const newRuleNames = ref<Record<number, string>>({});

async function addRuleFromInput(id: number) {
  const ok = addRule(id, newRuleNames.value[id] ?? '');
  if (ok) newRuleNames.value[id] = '';
}

function cancelCreate() {
  newDraft.value = null;
  newDraftError.value = '';
}

async function saveNewDraft() {
  const draft = newDraft.value;
  if (!draft) return;
  savingIds.value.add(0);
  newDraftError.value = '';
  try {
    const saved = await saveProfile(draft);
    profiles.value = [...profiles.value, saved];
    newDraft.value = null;
    expandedId.value = saved.id;
    draftOf(saved);
    ElMessage.success({ message: '策略已保存', offset: 60 });
  } catch (e) {
    newDraftError.value = e instanceof Error ? e.message : '保存失败';
  } finally {
    savingIds.value.delete(0);
  }
}

onMounted(() => {
  load();
});
</script>

<template>
  <div class="pp-panel">
    <!-- 工具栏：视图切换 + 新建 -->
    <div class="pp-toolbar">
      <div class="pp-seg" role="tablist" aria-label="权限策略视图">
        <button
          class="pp-seg-btn"
          :class="{ 'pp-seg-btn--active': view === 'policy' }"
          role="tab"
          :aria-selected="view === 'policy'"
          @click="view = 'policy'"
        >
          策略配置
        </button>
        <button
          class="pp-seg-btn"
          :class="{ 'pp-seg-btn--active': view === 'audit' }"
          role="tab"
          :aria-selected="view === 'audit'"
          @click="view = 'audit'"
        >
          审计日志
        </button>
      </div>
      <el-button
        v-if="view === 'policy'"
        type="primary"
        size="small"
        :icon="Plus"
        :disabled="newDraft != null"
        @click="startCreate"
      >
        新建策略
      </el-button>
    </div>

    <!-- 错误态（策略视图） -->
    <el-alert
      v-if="view === 'policy' && error"
      :title="error"
      type="error"
      show-icon
      :closable="false"
      class="pp-alert"
    >
      <el-button size="small" :icon="Refresh" @click="load(true)">重试</el-button>
    </el-alert>

    <!-- 加载态 -->
    <el-skeleton v-if="view === 'policy' && loading && profiles.length === 0" :rows="4" animated />

    <template v-if="view === 'policy'">
      <!-- 内置默认说明卡（无任何策略时固定展示，只读） -->
      <div v-if="!loading && !error && profiles.length === 0 && newDraft == null" class="pp-builtin-card">
        <div class="pp-builtin-title">内置默认策略（当前生效）</div>
        <ul class="pp-builtin-list">
          <li>未匹配任何自定义策略的工具：<b>沉默放行</b>（allow）。</li>
          <li>高风险工具（写文件 / 终端命令 / 浏览器操作等）每次执行前<b>转人工审批</b>（ask）。</li>
          <li>策略匹配序：Agent 精确策略 → 全局默认策略 → 内置默认；白名单未命中时浏览器类拒绝（引导加白）、文件/终端转审批、参数解析失败转审批（fail-safe）。</li>
        </ul>
        <div class="pp-builtin-hint">新建策略即可为特定 Agent 或工具覆盖以上行为。</div>
      </div>

      <!-- 行内新建草稿卡 -->
      <div v-if="newDraft" class="pp-card pp-card--new">
        <div class="pp-card-head">
          <el-icon :size="18" class="pp-card-icon"><Plus /></el-icon>
          <span class="pp-card-title">新建策略</span>
        </div>
        <div class="pp-new-form">
          <div class="pp-field-row">
            <label class="pp-field-label">Agent 标识</label>
            <el-input
              v-model="newDraft.agentId"
              size="small"
              placeholder="留空 = 全局默认策略；填 Agent 标识 = 仅该 Agent 生效"
              class="pp-field-input"
            />
          </div>
          <div class="pp-field-row">
            <label class="pp-field-label">备注</label>
            <el-input v-model="newDraft.remark" size="small" placeholder="可选" class="pp-field-input" />
          </div>
          <el-alert
            v-if="newDraftError"
            :title="newDraftError"
            type="error"
            show-icon
            :closable="false"
            class="pp-alert"
          />
          <div class="pp-actions">
            <el-button size="small" @click="cancelCreate">取消</el-button>
            <el-button type="primary" size="small" :loading="savingIds.has(0)" @click="saveNewDraft">保存策略</el-button>
          </div>
        </div>
      </div>

      <!-- 策略卡片列表 -->
      <div
        v-for="p in profiles"
        :key="p.id"
        class="pp-card"
        :class="{ 'pp-card--expanded': expandedId === p.id, 'pp-card--dirty': isDirty(p.id) }"
      >
        <!-- 卡片头 -->
        <div
          class="pp-card-head"
          role="button"
          tabindex="0"
          @click="toggleExpand(p)"
          @keydown.enter="toggleExpand(p)"
        >
          <el-icon :size="18" class="pp-card-icon"><Lock /></el-icon>
          <div class="pp-card-main">
            <div class="pp-card-titleline">
              <span class="pp-card-title">{{ profileTitle(p) }}</span>
              <span v-if="p.agentId.trim() === ''" class="pp-badge pp-badge--global">默认</span>
              <span v-if="!p.enabled" class="pp-badge pp-badge--off">已停用</span>
              <span v-if="isDirty(p.id)" class="pp-badge pp-badge--dirty">未保存</span>
            </div>
            <div class="pp-card-chips">
              <span v-for="e in EFFECTS" :key="e" class="pp-chip" :data-effect="e">
                {{ EFFECT_LABEL[e] }} {{ effectCounts(p)[e] }}
              </span>
              <span v-if="p.remark" class="pp-card-remark">{{ p.remark }}</span>
            </div>
          </div>
          <div class="pp-card-headactions">
            <span class="pp-enabled" @click.stop>
              <el-switch v-model="draftOf(p).enabled" size="small" />
            </span>
            <el-button
              size="small"
              text
              :icon="Delete"
              class="pp-del-btn"
              @click.stop="removeProfile(p)"
            />
          </div>
        </div>

        <!-- 展开编辑区 -->
        <div v-if="expandedId === p.id" class="pp-editor">
          <div class="pp-matrix">
            <div
              v-for="rule in draftOf(p).rules"
              :key="rule.toolName"
              class="pp-rule"
              :data-deny="rule.effect === 'Deny'"
            >
              <div class="pp-rule-row">
                <span class="pp-rule-name" :title="rule.toolName">{{ rule.toolName === '*' ? '兜底（其余工具）' : rule.toolName }}</span>
                <div class="pp-rule-seg" role="radiogroup" :aria-label="`工具 ${rule.toolName} 决策`">
                  <button
                    v-for="e in EFFECTS"
                    :key="e"
                    class="pp-rule-seg-btn"
                    :class="{ 'pp-rule-seg-btn--active': rule.effect === e }"
                    role="radio"
                    :aria-checked="rule.effect === e"
                    @click="rule.effect = e"
                  >
                    {{ EFFECT_LABEL[e] }}
                  </button>
                </div>
                <el-button
                  v-if="rule.toolName !== '*'"
                  size="small"
                  text
                  :icon="Delete"
                  class="pp-rule-del"
                  @click="removeRule(p.id, rule.toolName)"
                />
                <span v-if="rule.toolName === '*'" class="pp-rule-star-hint">兜底规则不可移除</span>
              </div>
              <!-- 规则级白名单（契约：ToolPermissionRule 三组） -->
              <div class="pp-wl">
                <div v-for="g in WL_GROUPS" :key="g.key" class="pp-wl-group">
                  <span class="pp-wl-label">{{ g.label }}</span>
                  <div class="pp-wl-tags">
                    <span v-for="entry in rule[g.key]" :key="entry" class="pp-wl-tag">
                      {{ entry }}
                      <button class="pp-wl-tag-x" :aria-label="`移除 ${entry}`" @click="removeWhitelistEntry(rule, g.key, entry)">×</button>
                    </span>
                    <span v-if="rule[g.key].length === 0" class="pp-wl-empty">{{ g.placeholder }}</span>
                  </div>
                  <el-input
                    size="small"
                    :placeholder="`添加${g.label}白名单条目，回车确认`"
                    class="pp-wl-input"
                    @keydown.enter.prevent="addWhitelistEntry(rule, g.key, ($event.target as HTMLInputElement).value) && (($event.target as HTMLInputElement).value = '')"
                  />
                </div>
              </div>
            </div>
          </div>

          <!-- 添加规则 -->
          <div class="pp-addrule">
            <el-input
              v-model="newRuleNames[p.id]"
              size="small"
              placeholder="工具名，如 write_file（回车添加）"
              class="pp-addrule-input"
              @keydown.enter.prevent="addRuleFromInput(p.id)"
            />
            <el-button size="small" :icon="Plus" @click="addRuleFromInput(p.id)">添加规则</el-button>
          </div>

          <div class="pp-editor-foot">
            <span v-if="isDirty(p.id)" class="pp-dirty-hint">有未保存的变更</span>
            <span v-else class="pp-clean-hint">与已保存版本一致</span>
            <el-button
              type="primary"
              size="small"
              :loading="savingIds.has(p.id)"
              :disabled="!isDirty(p.id)"
              @click="saveProfileDraft(p.id)"
            >
              保存策略
            </el-button>
          </div>
        </div>
      </div>

      <!-- 空列表但非错误时提示（说明卡已展示） -->
    </template>

    <!-- 审计日志 -->
    <PermissionAuditView v-show="view === 'audit'" />
  </div>
</template>

<style scoped>
.pp-panel {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.pp-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

/* segmented 视图切换 */
.pp-seg {
  display: inline-flex;
  border: 1px solid var(--el-border-color);
  border-radius: var(--el-border-radius-base);
  overflow: hidden;
}

.pp-seg-btn {
  padding: 5px 14px;
  font-size: 12px;
  border: none;
  background: transparent;
  color: var(--el-text-color-regular);
  cursor: pointer;
  transition: background-color var(--dur-fast) var(--ease-out), color var(--dur-fast) var(--ease-out);
}

.pp-seg-btn--active {
  background: var(--gold-tint-strong);
  color: var(--gold-700);
  font-weight: 600;
}

.pp-alert {
  margin: 0;
}

/* 内置默认说明卡 */
.pp-builtin-card {
  border: 1px dashed var(--el-border-color);
  border-radius: var(--el-border-radius-base);
  padding: 14px 16px;
  background: var(--paper-tint);
}

.pp-builtin-title {
  font-size: 13px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin-bottom: 6px;
}

.pp-builtin-list {
  margin: 0;
  padding-left: 18px;
  font-size: 12px;
  color: var(--el-text-color-regular);
  line-height: 1.8;
}

.pp-builtin-hint {
  margin-top: 8px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

/* 策略卡片 */
.pp-card {
  border: 1px solid var(--el-border-color-lighter);
  border-radius: var(--el-border-radius-base);
  background: transparent;
  transition: border-color var(--dur-fast) var(--ease-out);
}

.pp-card--expanded {
  border-color: var(--el-color-primary);
}

.pp-card--dirty {
  border-left: 3px solid var(--gold-400);
}

.pp-card-head {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 14px;
  cursor: pointer;
}

.pp-card-icon {
  color: var(--gold-500);
  flex-shrink: 0;
}

.pp-card-main {
  flex: 1;
  min-width: 0;
}

.pp-card-titleline {
  display: flex;
  align-items: center;
  gap: 8px;
}

.pp-card-title {
  font-size: 13px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.pp-badge {
  flex-shrink: 0;
  font-size: 10px;
  font-family: var(--font-mono);
  padding: 1px 6px;
  border-radius: var(--el-border-radius-small);
  border: 1px solid var(--el-border-color);
}

.pp-badge--global {
  color: var(--gold-700);
  background: var(--gold-tint);
  border-color: transparent;
}

.pp-badge--off {
  color: var(--el-text-color-secondary);
}

.pp-badge--dirty {
  color: var(--gold-700);
  background: var(--gold-tint-strong);
  border-color: transparent;
}

.pp-card-chips {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-top: 4px;
  flex-wrap: wrap;
}

.pp-chip {
  font-size: 10px;
  font-family: var(--font-mono);
  padding: 1px 6px;
  border-radius: var(--el-border-radius-small);
  border: 1px solid var(--el-border-color);
  color: var(--el-text-color-regular);
}

.pp-chip[data-effect='Deny'] {
  color: var(--cinnabar-400);
  border-color: color-mix(in srgb, var(--cinnabar-400) 35%, transparent);
}

.pp-chip[data-effect='Allow'] {
  color: var(--el-color-success);
}

.pp-card-remark {
  font-size: 11px;
  color: var(--el-text-color-secondary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  min-width: 0;
}

.pp-card-headactions {
  display: flex;
  align-items: center;
  gap: 6px;
  flex-shrink: 0;
}

.pp-del-btn {
  color: var(--el-text-color-secondary);
}

/* 展开编辑区 */
.pp-editor {
  border-top: 1px solid var(--el-border-color-lighter);
  padding: 12px 14px;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.pp-matrix {
  display: flex;
  flex-direction: column;
  gap: 8px;
  max-height: 420px;
  overflow-y: auto;
}

.pp-rule {
  border: 1px solid var(--el-border-color-lighter);
  border-radius: var(--el-border-radius-base);
  padding: 8px 10px;
  transition: border-color var(--dur-fast) var(--ease-out);
}

.pp-rule[data-deny='true'] {
  border-color: color-mix(in srgb, var(--cinnabar-400) 35%, transparent);
}

.pp-rule-row {
  display: flex;
  align-items: center;
  gap: 10px;
}

.pp-rule-name {
  font-size: 12px;
  font-family: var(--font-mono);
  color: var(--el-text-color-primary);
  min-width: 0;
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.pp-rule-star-hint {
  font-size: 11px;
  color: var(--el-text-color-secondary);
  flex-shrink: 0;
}

.pp-rule-seg {
  display: inline-flex;
  border: 1px solid var(--el-border-color);
  border-radius: var(--el-border-radius-base);
  overflow: hidden;
  flex-shrink: 0;
}

.pp-rule-seg-btn {
  padding: 3px 10px;
  font-size: 11px;
  border: none;
  background: var(--el-fill-color-light);
  color: var(--el-text-color-regular);
  cursor: pointer;
  transition: background-color var(--dur-fast) var(--ease-out), color var(--dur-fast) var(--ease-out);
}

.pp-rule-seg-btn--active {
  background: var(--gold-tint-strong);
  color: var(--gold-700);
  font-weight: 600;
}

.pp-rule[data-deny='true'] .pp-rule-seg-btn--active {
  background: color-mix(in srgb, var(--cinnabar-400) 14%, transparent);
  color: var(--cinnabar-400);
}

/* 规则级白名单 */
.pp-wl {
  margin-top: 8px;
  padding-top: 8px;
  border-top: 1px dashed var(--el-border-color-lighter);
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.pp-wl-group {
  display: grid;
  grid-template-columns: 42px 1fr 220px;
  gap: 8px;
  align-items: center;
}

.pp-wl-label {
  font-size: 11px;
  color: var(--el-text-color-secondary);
}

.pp-wl-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  min-width: 0;
}

.pp-wl-tag {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: 11px;
  font-family: var(--font-mono);
  padding: 1px 6px;
  border-radius: var(--el-border-radius-small);
  background: var(--el-fill-color);
  color: var(--el-text-color-regular);
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.pp-wl-tag-x {
  border: none;
  background: transparent;
  cursor: pointer;
  color: var(--el-text-color-secondary);
  padding: 0;
  font-size: 12px;
  line-height: 1;
}

.pp-wl-tag-x:hover {
  color: var(--cinnabar-400);
}

.pp-wl-empty {
  font-size: 11px;
  color: var(--el-text-color-placeholder);
}

/* 添加规则 + 编辑区底栏 */
.pp-addrule {
  display: flex;
  gap: 8px;
}

.pp-addrule-input {
  max-width: 320px;
}

.pp-editor-foot {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 10px;
}

.pp-dirty-hint {
  font-size: 11px;
  color: var(--gold-700);
}

.pp-clean-hint {
  font-size: 11px;
  color: var(--el-text-color-placeholder);
}

/* 新建草稿卡 */
.pp-card--new {
  border-color: var(--gold-400);
}

.pp-new-form {
  padding: 0 14px 12px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.pp-field-row {
  display: grid;
  grid-template-columns: 84px 1fr;
  gap: 8px;
  align-items: center;
}

.pp-field-label {
  font-size: 12px;
  color: var(--el-text-color-regular);
}

.pp-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}

/* 窄屏：白名单组收为单列 */
@media (max-width: 720px) {
  .pp-wl-group {
    grid-template-columns: 42px 1fr;
  }

  .pp-wl-input {
    grid-column: 2;
  }
}
</style>

