<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import AppLogo from '@/components/AppLogo.vue'
import PageHead from '@/components/PageHead.vue'
import EmptyState from '@/components/EmptyState.vue'
import type { SkillItemDto, CreateSkillDto, UpdateSkillDto } from '@/types/skills'
import { skillsApi } from '@/services/skillsApi'

// ===== State =====
const skills = ref<SkillItemDto[]>([])
const loading = ref(false)
const error = ref('')
const searchKeyword = ref('')
const showFormModal = ref(false)
const editingSkill = ref<SkillItemDto | null>(null)
const formSubmitting = ref(false)
const formError = ref('')
const togglingIds = ref<Set<string>>(new Set())

// EmptyState 图标（v-html 注入；lucide zap 风格）
const emptyIconSvg =
  '<svg xmlns="http://www.w3.org/2000/svg" width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2"/></svg>'
const errorIconSvg =
  '<svg xmlns="http://www.w3.org/2000/svg" width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><line x1="15" y1="9" x2="9" y2="15"/><line x1="9" y1="9" x2="15" y2="15"/></svg>'

// Form fields
const formName = ref('')
const formDescription = ref('')
const formCategory = ref('')
const formSystemPrompt = ref('')
const formToolIds = ref('')

// ===== Computed =====
const filteredSkills = computed(() => {
  const kw = searchKeyword.value.trim().toLowerCase()
  if (!kw) return skills.value
  return skills.value.filter(
    (s) =>
      s.name.toLowerCase().includes(kw) ||
      s.description.toLowerCase().includes(kw) ||
      s.category.toLowerCase().includes(kw),
  )
})

const isEditing = computed(() => !!editingSkill.value)

// ===== Lifecycle =====
onMounted(() => {
  loadSkills()
})

// ===== Methods =====
async function loadSkills(): Promise<void> {
  loading.value = true
  error.value = ''
  try {
    skills.value = await skillsApi.fetchSkills()
  } catch (e) {
    error.value = e instanceof Error ? e.message : '加载技能列表失败'
  } finally {
    loading.value = false
  }
}

async function handleToggle(skill: SkillItemDto): Promise<void> {
  if (togglingIds.value.has(skill.id)) return
  togglingIds.value.add(skill.id)
  try {
    const updated = await skillsApi.toggleSkill(skill.id)
    const idx = skills.value.findIndex((s) => s.id === updated.id)
    if (idx !== -1) {
      skills.value[idx] = updated
    }
  } catch (e) {
    console.error('切换技能状态失败:', e)
  } finally {
    togglingIds.value.delete(skill.id)
  }
}

function openCreateModal(): void {
  editingSkill.value = null
  formName.value = ''
  formDescription.value = ''
  formCategory.value = ''
  formSystemPrompt.value = ''
  formToolIds.value = ''
  formError.value = ''
  showFormModal.value = true
}

function openEditModal(skill: SkillItemDto): void {
  editingSkill.value = skill
  formName.value = skill.name
  formDescription.value = skill.description
  formCategory.value = skill.category
  formSystemPrompt.value = ''
  formToolIds.value = ''
  formError.value = ''
  showFormModal.value = true

  // Load detail to get systemPrompt and toolIds
  skillsApi.fetchSkillDetail(skill.id).then((detail) => {
    formSystemPrompt.value = detail.systemPrompt ?? ''
    formToolIds.value = (detail.toolIds ?? []).join(', ')
  }).catch(() => {
    // Silently fail - detail fields are optional in form
  })
}

function closeFormModal(): void {
  showFormModal.value = false
  editingSkill.value = null
}

async function handleFormSubmit(): Promise<void> {
  if (!formName.value.trim()) {
    formError.value = '技能名称不能为空'
    return
  }
  formSubmitting.value = true
  formError.value = ''

  const toolIds = formToolIds.value
    .split(',')
    .map((s) => s.trim())
    .filter(Boolean)

  try {
    if (isEditing.value && editingSkill.value) {
      const dto: UpdateSkillDto = {
        name: formName.value.trim(),
        description: formDescription.value.trim() || undefined,
        category: formCategory.value.trim() || undefined,
        systemPrompt: formSystemPrompt.value || undefined,
        toolIds: toolIds.length > 0 ? toolIds : undefined,
      }
      await skillsApi.updateSkill(editingSkill.value.id, dto)
    } else {
      const dto: CreateSkillDto = {
        name: formName.value.trim(),
        description: formDescription.value.trim() || undefined,
        category: formCategory.value.trim() || undefined,
        systemPrompt: formSystemPrompt.value || undefined,
        toolIds: toolIds.length > 0 ? toolIds : undefined,
      }
      await skillsApi.createSkill(dto)
    }
    closeFormModal()
    await loadSkills()
  } catch (e) {
    formError.value = e instanceof Error ? e.message : '操作失败'
  } finally {
    formSubmitting.value = false
  }
}

function getSkillIcon(name: string): string {
  const icons: Record<string, string> = {
    '系统诊断': 'activity',
    '脚本生成': 'file-code',
    '文件整理': 'folder-sync',
    '代码分析': 'search-code',
    '数据查询': 'database',
    '日志分析': 'scroll-text',
  }
  return icons[name] || 'zap'
}
</script>

<template>
  <div class="skills-view">
    <div class="skills-page">
      <!-- ===== PageHead（设计稿统一规格） ===== -->
      <PageHead eyebrow="Skills · 技能管理" title="技能工坊">
        <template #actions>
          <div class="search-box">
            <svg
              width="14"
              height="14"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
              class="search-icon"
            >
              <circle cx="11" cy="11" r="8" />
              <line x1="21" y1="21" x2="16.65" y2="16.65" />
            </svg>
            <input
              v-model="searchKeyword"
              type="text"
              placeholder="搜索技能..."
              class="search-input"
              aria-label="搜索技能"
            />
          </div>
          <button class="btn-create" @click="openCreateModal">
            <svg
              width="14"
              height="14"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2.5"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <line x1="12" y1="5" x2="12" y2="19" />
              <line x1="5" y1="12" x2="19" y2="12" />
            </svg>
            <span>创建技能</span>
          </button>
        </template>
      </PageHead>

      <!-- ===== Loading ===== -->
      <div v-if="loading" class="loading-state">
        <div class="loading-spinner" />
        <p>加载中…</p>
      </div>

      <!-- ===== Error（可见留痕 + 重试） ===== -->
      <EmptyState
        v-else-if="error"
        :icon="errorIconSvg"
        title="技能加载失败"
        :sub="error"
      >
        <template #action>
          <button class="btn-create" @click="loadSkills">重试</button>
        </template>
      </EmptyState>

      <!-- ===== Empty ===== -->
      <EmptyState
        v-else-if="filteredSkills.length === 0"
        :icon="emptyIconSvg"
        :title="searchKeyword ? '没有匹配的技能' : '暂无技能'"
        :sub="searchKeyword ? '换个关键字试试' : '从第一件工具开始打造你的匣'"
      >
        <template v-if="!searchKeyword" #action>
          <button class="btn-create" @click="openCreateModal">创建技能</button>
        </template>
      </EmptyState>

      <!-- ===== Skills Grid（4 列，DESIGN.md 技能卡规格） ===== -->
      <div v-else class="skills-grid">
        <article
          v-for="skill in filteredSkills"
          :key="skill.id"
          class="skill-card glass"
          :class="{ 'skill-card--off': !skill.isEnabled }"
        >
          <div class="skill-card__head">
            <div class="skill-card__icon" :class="skill.isEnabled ? 'is-on' : 'is-off'">
              <svg
                v-if="getSkillIcon(skill.name) === 'activity'"
                width="15"
                height="15"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="2"
                stroke-linecap="round"
                stroke-linejoin="round"
              >
                <polyline points="22 12 18 12 15 21 9 3 6 12 2 12" />
              </svg>
              <svg
                v-else-if="getSkillIcon(skill.name) === 'file-code'"
                width="15"
                height="15"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="2"
                stroke-linecap="round"
                stroke-linejoin="round"
              >
                <polyline points="16 18 22 12 16 6" />
                <polyline points="8 6 2 12 8 18" />
              </svg>
              <svg
                v-else-if="getSkillIcon(skill.name) === 'folder-sync'"
                width="15"
                height="15"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="2"
                stroke-linecap="round"
                stroke-linejoin="round"
              >
                <path d="M9 20H4a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2v2" />
                <path d="M16 17a5 5 0 0 0-5-5" />
                <path d="M21 14a5 5 0 0 0-5-5" />
                <path d="M16 22a5 5 0 0 0 5-5" />
                <path d="M11 17a5 5 0 0 0 5 5" />
              </svg>
              <svg
                v-else-if="getSkillIcon(skill.name) === 'search-code'"
                width="15"
                height="15"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="2"
                stroke-linecap="round"
                stroke-linejoin="round"
              >
                <circle cx="11" cy="11" r="8" />
                <line x1="21" y1="21" x2="16.65" y2="16.65" />
                <line x1="8" y1="11" x2="14" y2="11" />
              </svg>
              <svg
                v-else-if="getSkillIcon(skill.name) === 'database'"
                width="15"
                height="15"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="2"
                stroke-linecap="round"
                stroke-linejoin="round"
              >
                <ellipse cx="12" cy="5" rx="9" ry="3" />
                <path d="M21 12c0 1.66-4 3-9 3s-9-1.34-9-3" />
                <path d="M3 5v14c0 1.66 4 3 9 3s9-1.34 9-3V5" />
              </svg>
              <svg
                v-else-if="getSkillIcon(skill.name) === 'scroll-text'"
                width="15"
                height="15"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="2"
                stroke-linecap="round"
                stroke-linejoin="round"
              >
                <path d="M15 3h4a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-4" />
                <polyline points="10 17 15 12 10 7" />
                <line x1="15" y1="12" x2="3" y2="12" />
              </svg>
              <svg
                v-else
                width="15"
                height="15"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="2"
                stroke-linecap="round"
                stroke-linejoin="round"
              >
                <polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2" />
              </svg>
            </div>
            <h3 class="card-name">{{ skill.name }}</h3>
            <span class="status-badge" :class="skill.isEnabled ? 'status-badge--enabled' : 'status-badge--disabled'">
              <span class="status-dot" :class="skill.isEnabled ? 'status-dot--enabled' : 'status-dot--disabled'" />
              {{ skill.isEnabled ? '已启用' : '未启用' }}
            </span>
          </div>

          <p class="skill-card__desc">{{ skill.description }}</p>

          <div class="skill-card__foot">
            <span class="skill-card__meta">{{ skill.category || '未分类' }} · {{ skill.usageCount }} 次</span>
            <div class="skill-card__actions">
              <button class="skill-card__edit" @click="openEditModal(skill)">编辑</button>
              <button
                class="skill-toggle"
                :class="{ on: skill.isEnabled }"
                :disabled="togglingIds.has(skill.id)"
                :aria-label="skill.isEnabled ? '停用' : '启用'"
                @click="handleToggle(skill)"
              >
                <span class="skill-toggle__knob" />
              </button>
            </div>
          </div>
        </article>
      </div>
    </div>

    <!-- ===== Create/Edit Modal（保留原交互） ===== -->
    <Teleport to="body">
      <div v-if="showFormModal" class="modal-overlay" @click.self="closeFormModal">
        <div class="modal-panel">
          <div class="modal-header">
            <div class="flex items-center gap-2">
              <AppLogo :size="20" />
              <h2 class="modal-title">{{ isEditing ? '编辑技能' : '创建技能' }}</h2>
            </div>
            <button class="modal-close" @click="closeFormModal">
              <svg
                width="16"
                height="16"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="2"
                stroke-linecap="round"
                stroke-linejoin="round"
              >
                <line x1="18" y1="6" x2="6" y2="18" />
                <line x1="6" y1="6" x2="18" y2="18" />
              </svg>
            </button>
          </div>
          <form class="modal-body" @submit.prevent="handleFormSubmit">
            <div class="form-field">
              <label class="form-label">名称 <span class="required">*</span></label>
              <input v-model="formName" type="text" class="form-input" placeholder="技能名称" />
            </div>
            <div class="form-field">
              <label class="form-label">描述</label>
              <textarea v-model="formDescription" class="form-input form-textarea" placeholder="技能描述" rows="3" />
            </div>
            <div class="form-field">
              <label class="form-label">分类</label>
              <input v-model="formCategory" type="text" class="form-input" placeholder="如：系统、开发、数据" />
            </div>
            <div class="form-field">
              <label class="form-label">系统提示词</label>
              <textarea v-model="formSystemPrompt" class="form-input form-textarea form-textarea--code" placeholder="系统提示词" rows="4" />
            </div>
            <div class="form-field">
              <label class="form-label">关联工具 ID</label>
              <input v-model="formToolIds" type="text" class="form-input" placeholder="多个 ID 用逗号分隔" />
            </div>

            <div v-if="formError" class="form-error">{{ formError }}</div>

            <div class="form-actions">
              <button type="button" class="btn-cancel" @click="closeFormModal">取消</button>
              <button type="submit" class="btn-submit" :disabled="formSubmitting">
                {{ formSubmitting ? '提交中...' : isEditing ? '保存修改' : '创建技能' }}
              </button>
            </div>
          </form>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<style scoped>
.skills-view {
  position: relative;
  height: 100%;
  overflow-y: auto;
}

.skills-page {
  position: relative;
  max-width: 1200px;
  margin: 0 auto;
  padding: 24px 32px;
}

/* ===== Breadcrumb ===== */
.breadcrumb {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-bottom: 16px;
}

.breadcrumb-link {
  font-size: 0.8125rem;
  color: var(--fg-secondary);
  text-decoration: none;
  transition: color var(--dur-fast) var(--ease-out);
}

.breadcrumb-link:hover {
  color: var(--gold-400);
}

.breadcrumb-chevron {
  color: var(--fg-muted);
  flex-shrink: 0;
}

.breadcrumb-current {
  font-size: 0.8125rem;
  color: var(--fg-primary);
  font-weight: 500;
}

/* ===== PageHead actions ===== */
.search-box {
  display: flex;
  align-items: center;
  gap: 8px;
  height: 32px;
  padding: 0 10px;
  background: color-mix(in srgb, var(--fg-primary) 6%, transparent);
  border: 1px solid var(--glass-border);
  border-radius: var(--el-border-radius-base);
  transition: border-color var(--dur-fast) var(--ease-out), box-shadow var(--dur-fast) var(--ease-out);
}

.search-box:focus-within {
  border-color: var(--gold-400);
  box-shadow: 0 0 0 2px var(--gold-tint-strong);
}

.search-icon {
  color: var(--fg-tertiary);
  flex-shrink: 0;
}

.search-input {
  background: transparent;
  border: none;
  outline: none;
  font-size: 0.8125rem;
  color: var(--fg-primary);
  width: 180px;
}

.search-input::placeholder {
  color: var(--fg-muted);
}

.btn-create {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  height: 32px;
  padding: 0 14px;
  border-radius: var(--el-border-radius-base);
  background: var(--gold-400);
  color: var(--ink-950);
  border: none;
  font-size: 0.8125rem;
  font-weight: 600;
  cursor: pointer;
  transition: transform var(--dur-fast) var(--ease-out), background var(--dur-fast) var(--ease-out);
}

.btn-create:hover {
  background: var(--gold-300);
  transform: translateY(-1px);
}

.btn-create:active {
  transform: translateY(1px);
}

/* ===== Loading ===== */
.loading-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 12px;
  padding: 64px 16px;
  color: var(--fg-secondary);
  font-size: 0.875rem;
}

.loading-spinner {
  width: 32px;
  height: 32px;
  border: 3px solid color-mix(in srgb, var(--fg-primary) 12%, transparent);
  border-top-color: var(--gold-400);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

/* ===== Skills Grid（4 列） ===== */
.skills-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
}

/* ===== Skill Card（DESIGN.md 技能卡规格） ===== */
.skill-card {
  border-radius: var(--glass-radius);
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 10px;
  transition: transform var(--dur-fast) var(--ease-out), box-shadow var(--dur-fast) var(--ease-out);
}

.skill-card:hover {
  transform: translateY(-1px);
}

.skill-card--off {
  opacity: 0.6;
}

.skill-card__head {
  display: flex;
  align-items: center;
  gap: 10px;
  min-width: 0;
}

.skill-card__icon {
  width: 30px;
  height: 30px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.skill-card__icon.is-on {
  background: var(--gold-tint-strong);
  color: var(--gold-400);
}

.skill-card__icon.is-off {
  background: color-mix(in srgb, var(--fg-muted) 5%, transparent);
  color: var(--fg-muted);
}

.card-name {
  font-size: 13px;
  font-weight: 600;
  color: var(--fg-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  margin: 0;
  min-width: 0;
  flex: 1;
}

/* Status chip（已启用青铜绿 / 未启用灰） */
.status-badge {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 1px 8px;
  font-size: 11px;
  font-weight: 500;
  border-radius: 999px;
  flex-shrink: 0;
  font-family: var(--font-mono);
}

.status-badge--enabled {
  color: var(--el-color-success);
  background: color-mix(in srgb, var(--el-color-success) 14%, transparent);
}

.status-badge--disabled {
  color: var(--fg-secondary);
  background: color-mix(in srgb, var(--fg-muted) 12%, transparent);
}

.status-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
}

.status-dot--enabled {
  background: var(--el-color-success);
}

.status-dot--disabled {
  background: var(--fg-muted);
}

.skill-card__desc {
  font-size: 0.8125rem;
  color: var(--fg-secondary);
  line-height: 1.5;
  margin: 0;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.skill-card__foot {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  margin-top: auto;
}

.skill-card__meta {
  font-family: var(--font-mono);
  font-size: 11px;
  color: var(--fg-tertiary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.skill-card__actions {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
}

.skill-card__edit {
  font-size: 12px;
  color: var(--fg-secondary);
  background: transparent;
  border: 1px solid color-mix(in srgb, var(--fg-primary) 14%, transparent);
  border-radius: var(--el-border-radius-small);
  padding: 2px 10px;
  cursor: pointer;
  transition: color var(--dur-fast) var(--ease-out), border-color var(--dur-fast) var(--ease-out);
  white-space: nowrap;
}

.skill-card__edit:hover {
  color: var(--gold-400);
  border-color: var(--gold-400);
}

/* 金色小 toggle（28×15，圆钮 11px） */
.skill-toggle {
  width: 28px;
  height: 15px;
  border-radius: 999px;
  border: 1px solid color-mix(in srgb, var(--fg-muted) 35%, transparent);
  background: color-mix(in srgb, var(--fg-muted) 18%, transparent);
  position: relative;
  cursor: pointer;
  padding: 0;
  flex-shrink: 0;
  transition: background var(--dur-fast) var(--ease-out), border-color var(--dur-fast) var(--ease-out);
}

.skill-toggle.on {
  background: var(--gold-400);
  border-color: var(--gold-400);
}

.skill-toggle:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.skill-toggle__knob {
  position: absolute;
  top: 1px;
  left: 1px;
  width: 11px;
  height: 11px;
  border-radius: 50%;
  background: var(--fg-primary);
  transition: transform var(--dur-fast) var(--ease-out);
}

.skill-toggle.on .skill-toggle__knob {
  transform: translateX(13px);
  background: var(--ink-950);
}

/* ===== Modal ===== */
.modal-overlay {
  position: fixed;
  inset: 0;
  background: color-mix(in srgb, var(--ink-950) 45%, transparent);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  padding: 16px;
}

.modal-panel {
  background: var(--ink-850);
  border: 1px solid var(--glass-border);
  border-radius: 12px;
  width: 100%;
  max-width: 520px;
  max-height: 90vh;
  overflow-y: auto;
  box-shadow: 0 18px 48px color-mix(in srgb, var(--ink-950) 60%, transparent);
}

.modal-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 20px 24px;
  border-bottom: 1px solid var(--glass-border);
}

.modal-title {
  font-size: 1.125rem;
  font-weight: 600;
  color: var(--fg-primary);
  margin: 0;
}

.modal-close {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  border-radius: var(--el-border-radius-small);
  background: transparent;
  border: none;
  color: var(--fg-secondary);
  cursor: pointer;
  transition: background var(--dur-fast) var(--ease-out), color var(--dur-fast) var(--ease-out);
}

.modal-close:hover {
  background: var(--paper-tint);
  color: var(--fg-primary);
}

.modal-body {
  padding: 20px 24px;
}

.form-field {
  margin-bottom: 16px;
}

.form-label {
  display: block;
  font-size: 0.8125rem;
  font-weight: 500;
  color: var(--fg-secondary);
  margin-bottom: 8px;
}

.required {
  color: var(--cinnabar-400);
}

.form-input {
  width: 100%;
  padding: 8px 12px;
  border-radius: var(--el-border-radius-base);
  background: color-mix(in srgb, var(--fg-primary) 6%, transparent);
  border: 1px solid var(--glass-border);
  color: var(--fg-primary);
  font-size: 0.875rem;
  transition: border-color var(--dur-fast) var(--ease-out), box-shadow var(--dur-fast) var(--ease-out);
}

.form-input:focus {
  border-color: var(--gold-400);
  box-shadow: 0 0 0 2px var(--gold-tint-strong);
  outline: none;
}

.form-input::placeholder {
  color: var(--fg-muted);
}

.form-textarea {
  resize: vertical;
  min-height: 60px;
}

.form-textarea--code {
  font-family: var(--font-mono);
  font-size: 0.8125rem;
}

.form-error {
  padding: 8px 12px;
  background: color-mix(in srgb, var(--cinnabar-400) 12%, transparent);
  border: 1px solid color-mix(in srgb, var(--cinnabar-400) 35%, transparent);
  border-radius: var(--el-border-radius-base);
  color: var(--cinnabar-400);
  font-size: 0.8125rem;
  margin-bottom: 16px;
}

.form-actions {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 12px;
  padding-top: 8px;
}

.btn-cancel {
  padding: 8px 16px;
  border-radius: var(--el-border-radius-base);
  background: transparent;
  border: 1px solid var(--glass-border);
  color: var(--fg-secondary);
  font-size: 0.875rem;
  cursor: pointer;
  transition: color var(--dur-fast) var(--ease-out), background var(--dur-fast) var(--ease-out);
}

.btn-cancel:hover {
  background: var(--paper-tint);
  color: var(--fg-primary);
}

.btn-submit {
  padding: 8px 20px;
  border-radius: var(--el-border-radius-base);
  background: var(--gold-400);
  border: none;
  color: var(--ink-950);
  font-size: 0.875rem;
  font-weight: 600;
  cursor: pointer;
  transition: background var(--dur-fast) var(--ease-out), transform var(--dur-fast) var(--ease-out);
}

.btn-submit:hover {
  background: var(--gold-300);
}

.btn-submit:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

/* ===== Responsive ===== */
@media (max-width: 1100px) {
  .skills-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 640px) {
  .skills-grid {
    grid-template-columns: 1fr;
  }
}
</style>
