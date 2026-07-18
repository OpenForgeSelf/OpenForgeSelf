<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
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
    <!-- ===== Ambient glow ===== -->
    <div class="ambient-glow" aria-hidden="true" />

    <div class="skills-page">
      <!-- ===== Breadcrumb ===== -->
      <nav class="breadcrumb" aria-label="面包屑导航">
        <a href="/" class="breadcrumb-link" @click.prevent="$router.push('/')">首页</a>
        <svg
          width="12"
          height="12"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          stroke-linecap="round"
          stroke-linejoin="round"
          class="breadcrumb-chevron"
        >
          <polyline points="9 18 15 12 9 6" />
        </svg>
        <span class="breadcrumb-current">技能管理</span>
      </nav>

      <!-- ===== Page Header ===== -->
      <div class="page-header">
        <div class="header-left">
          <h1 class="page-title">技能管理</h1>
          <span class="count-badge">{{ skills.length }} 个技能</span>
        </div>
        <div class="header-right">
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
        </div>
      </div>

      <!-- ===== Loading ===== -->
      <div v-if="loading" class="state-message">
        <svg
          width="20"
          height="20"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          stroke-linecap="round"
          stroke-linejoin="round"
          class="spin-icon"
        >
          <line x1="12" y1="2" x2="12" y2="6" />
          <line x1="12" y1="18" x2="12" y2="22" />
          <line x1="4.93" y1="4.93" x2="7.76" y2="7.76" />
          <line x1="16.24" y1="16.24" x2="19.07" y2="19.07" />
          <line x1="2" y1="12" x2="6" y2="12" />
          <line x1="18" y1="12" x2="22" y2="12" />
          <line x1="4.93" y1="19.07" x2="7.76" y2="16.24" />
          <line x1="16.24" y1="7.76" x2="19.07" y2="4.93" />
        </svg>
        <span>加载中...</span>
      </div>

      <!-- ===== Error ===== -->
      <div v-else-if="error" class="state-message state-error">
        <svg
          width="20"
          height="20"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          stroke-linecap="round"
          stroke-linejoin="round"
        >
          <circle cx="12" cy="12" r="10" />
          <line x1="15" y1="9" x2="9" y2="15" />
          <line x1="9" y1="9" x2="15" y2="15" />
        </svg>
        <span>{{ error }}</span>
        <button class="btn-retry" @click="loadSkills">重试</button>
      </div>

      <!-- ===== Empty State ===== -->
      <div v-else-if="filteredSkills.length === 0" class="state-message">
        <svg
          width="24"
          height="24"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="1.5"
          stroke-linecap="round"
          stroke-linejoin="round"
        >
          <line x1="16.5" y1="9.4" x2="7.5" y2="4.21" />
          <path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z" />
          <polyline points="3.27 6.96 12 12.01 20.73 6.96" />
          <line x1="12" y1="22.08" x2="12" y2="12" />
        </svg>
        <span>{{ searchKeyword ? '没有匹配的技能' : '暂无技能，点击"创建技能"开始' }}</span>
      </div>

      <!-- ===== Skills Grid ===== -->
      <div v-else class="skills-grid">
        <article
          v-for="skill in filteredSkills"
          :key="skill.id"
          class="skill-card"
          :class="{ 'skill-card--enabled': skill.isEnabled }"
        >
          <!-- Card Top -->
          <div class="card-top">
            <div class="card-name-row">
              <div class="card-icon" :class="{ 'card-icon--enabled': skill.isEnabled }">
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
            </div>
            <span class="status-badge" :class="skill.isEnabled ? 'status-badge--enabled' : 'status-badge--disabled'">
              <span class="status-dot" :class="skill.isEnabled ? 'status-dot--enabled' : 'status-dot--disabled'" />
              {{ skill.isEnabled ? '已启用' : '未启用' }}
            </span>
          </div>

          <!-- Description -->
          <p class="card-desc">{{ skill.description }}</p>

          <!-- Bottom -->
          <div class="card-bottom">
            <div class="card-tools">
              <span class="tools-label">工具函数</span>
              <span v-if="skill.toolCount > 0" class="tool-tag">{{ skill.toolCount }} 个关联</span>
              <span v-else class="tool-tag tool-tag--empty">暂无关联工具</span>
            </div>
            <div class="card-actions">
              <span class="usage-count">使用 {{ skill.usageCount }} 次</span>
              <button class="btn-ghost" :disabled="togglingIds.has(skill.id)" @click="handleToggle(skill)">
                {{ skill.isEnabled ? '停用' : '启用' }}
              </button>
              <button class="btn-ghost" @click="openEditModal(skill)">编辑</button>
            </div>
          </div>
        </article>
      </div>
    </div>

    <!-- ===== Create/Edit Modal ===== -->
    <Teleport to="body">
      <div v-if="showFormModal" class="modal-overlay" @click.self="closeFormModal">
        <div class="modal-panel">
          <div class="modal-header">
            <h2 class="modal-title">{{ isEditing ? '编辑技能' : '创建技能' }}</h2>
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

.ambient-glow {
  position: fixed;
  top: 60px;
  left: 40px;
  width: 600px;
  height: 500px;
  background: radial-gradient(ellipse at 30% 20%, rgba(245, 158, 11, 0.04) 0%, transparent 70%);
  pointer-events: none;
  z-index: 0;
}

.skills-page {
  position: relative;
  z-index: 1;
  max-width: 1200px;
  margin: 0 auto;
  padding: var(--space-6) var(--space-8);
}

/* ===== Breadcrumb ===== */
.breadcrumb {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-bottom: var(--space-5);
}

.breadcrumb-link {
  font-size: 0.8125rem;
  color: var(--text-muted);
  text-decoration: none;
  transition: color var(--motion-fast);
}

.breadcrumb-link:hover {
  color: var(--primary-color);
}

.breadcrumb-chevron {
  color: var(--text-muted);
  flex-shrink: 0;
}

.breadcrumb-current {
  font-size: 0.8125rem;
  color: var(--text-primary);
  font-weight: 500;
}

/* ===== Page Header ===== */
.page-header {
  display: flex;
  align-items: center;
  gap: var(--space-4);
  margin-bottom: var(--space-6);
  flex-wrap: wrap;
}

.header-left {
  display: flex;
  align-items: center;
  gap: var(--space-3);
}

.page-title {
  font-size: 1.75rem;
  font-weight: 700;
  color: var(--text-primary);
  white-space: nowrap;
}

.count-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 24px;
  height: 24px;
  border-radius: var(--radius-pill);
  background: var(--primary-soft);
  font-size: 0.75rem;
  font-weight: 600;
  color: var(--primary-color);
  font-family: var(--font-family-mono);
  padding: 0 8px;
}

.header-right {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  margin-left: auto;
}

.btn-create {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  height: 32px;
  padding: 0 14px;
  border-radius: var(--radius-md);
  background: var(--primary-color);
  color: var(--primary-contrast);
  border: none;
  font-size: 0.8125rem;
  font-weight: 500;
  font-family: var(--font-family-base);
  cursor: pointer;
  transition: background var(--motion-fast);
}

.btn-create:hover {
  background: var(--primary-hover);
}

.search-box {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  height: 32px;
  padding: 0 10px;
  background: var(--bg-secondary);
  border: 1px solid var(--border-color);
  border-radius: var(--radius-md);
  transition: border-color var(--motion-fast), box-shadow var(--motion-fast);
}

.search-box:focus-within {
  border-color: var(--primary-color);
  box-shadow: 0 0 0 2px var(--primary-light);
}

.search-icon {
  color: var(--text-muted);
  flex-shrink: 0;
}

.search-input {
  background: transparent;
  border: none;
  outline: none;
  font-size: 0.8125rem;
  color: var(--text-primary);
  font-family: var(--font-family-base);
  width: 180px;
}

.search-input::placeholder {
  color: var(--text-muted);
}

/* ===== State Messages ===== */
.state-message {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: var(--space-3);
  padding: var(--space-16) var(--space-4);
  color: var(--text-muted);
  font-size: 0.875rem;
}

.state-message.state-error {
  color: var(--danger-color);
}

.spin-icon {
  animation: spin 1.5s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.btn-retry {
  margin-top: var(--space-2);
  padding: 6px 16px;
  border-radius: var(--radius-md);
  background: var(--primary-color);
  color: var(--primary-contrast);
  border: none;
  font-size: 0.8125rem;
  cursor: pointer;
}

.btn-retry:hover {
  background: var(--primary-hover);
}

/* ===== Skills Grid ===== */
.skills-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: var(--space-4);
}

/* ===== Skill Card ===== */
.skill-card {
  background: var(--bg-card);
  border: 1px solid var(--border-color);
  border-radius: var(--radius-md);
  padding: var(--space-5);
  transition: border-color var(--motion-fast), background var(--motion-fast);
  border-left: 2px solid transparent;
}

.skill-card--enabled {
  border-left-color: var(--primary-color);
}

.skill-card:hover {
  background: var(--bg-secondary);
  border-color: var(--primary-border);
}

.skill-card:hover .btn-ghost {
  border-color: var(--primary-border);
  color: var(--primary-color);
}

/* Card Top */
.card-top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-2);
  margin-bottom: var(--space-2);
}

.card-name-row {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  min-width: 0;
}

.card-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  border-radius: var(--radius-sm);
  background: var(--bg-hover);
  flex-shrink: 0;
  color: var(--text-muted);
}

.card-icon--enabled {
  background: var(--primary-soft);
  color: var(--primary-color);
}

.card-name {
  font-size: 0.9375rem;
  font-weight: 600;
  color: var(--text-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

/* Status Badge */
.status-badge {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 2px 8px;
  font-size: 0.75rem;
  font-weight: 500;
  border-radius: var(--radius-pill);
  flex-shrink: 0;
}

.status-badge--enabled {
  color: var(--success-color);
  background: rgba(63, 185, 80, 0.1);
}

.status-badge--disabled {
  color: var(--text-muted);
  background: var(--bg-tertiary);
}

.status-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
}

.status-dot--enabled {
  background: var(--success-color);
}

.status-dot--disabled {
  background: var(--text-muted);
}

/* Description */
.card-desc {
  font-size: 0.8125rem;
  color: var(--text-secondary);
  line-height: 1.5;
  margin-bottom: var(--space-3);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

/* Card Bottom */
.card-bottom {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-2);
}

.card-tools {
  display: flex;
  align-items: center;
  gap: 6px;
  min-width: 0;
  flex: 1;
}

.tools-label {
  font-size: 0.75rem;
  color: var(--text-muted);
  white-space: nowrap;
  flex-shrink: 0;
}

.tool-tag {
  font-size: 0.75rem;
  color: var(--info-color);
  background: rgba(88, 166, 255, 0.12);
  padding: 1px 6px;
  border-radius: var(--radius-sm);
  font-family: var(--font-family-mono);
  white-space: nowrap;
}

.tool-tag--empty {
  color: var(--text-muted);
  background: var(--bg-tertiary);
}

.card-actions {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  flex-shrink: 0;
}

.usage-count {
  font-size: 0.75rem;
  color: var(--text-muted);
  font-family: var(--font-family-mono);
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}

.btn-ghost {
  font-size: 0.75rem;
  color: var(--text-secondary);
  background: transparent;
  border: 1px solid var(--border-color);
  border-radius: var(--radius-sm);
  padding: 2px 10px;
  cursor: pointer;
  transition: all var(--motion-fast);
  font-family: var(--font-family-base);
  white-space: nowrap;
}

.btn-ghost:hover {
  background: var(--primary-soft);
  border-color: var(--primary-color);
  color: var(--primary-color);
}

.btn-ghost:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

/* ===== Modal ===== */
.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.6);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  padding: var(--space-4);
}

.modal-panel {
  background: var(--bg-card);
  border: 1px solid var(--border-color);
  border-radius: var(--radius-lg);
  width: 100%;
  max-width: 520px;
  max-height: 90vh;
  overflow-y: auto;
  box-shadow: var(--shadow-lg);
}

.modal-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: var(--space-5) var(--space-6);
  border-bottom: 1px solid var(--border-color);
}

.modal-title {
  font-size: 1.125rem;
  font-weight: 600;
  color: var(--text-primary);
}

.modal-close {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  border-radius: var(--radius-sm);
  background: transparent;
  border: none;
  color: var(--text-muted);
  cursor: pointer;
  transition: background var(--motion-fast), color var(--motion-fast);
}

.modal-close:hover {
  background: var(--bg-hover);
  color: var(--text-primary);
}

.modal-body {
  padding: var(--space-5) var(--space-6);
}

.form-field {
  margin-bottom: var(--space-4);
}

.form-label {
  display: block;
  font-size: 0.8125rem;
  font-weight: 500;
  color: var(--text-secondary);
  margin-bottom: var(--space-2);
}

.required {
  color: var(--danger-color);
}

.form-input {
  width: 100%;
  padding: 8px 12px;
  border-radius: var(--radius-md);
  background: var(--bg-secondary);
  border: 1px solid var(--border-color);
  color: var(--text-primary);
  font-size: 0.875rem;
  font-family: var(--font-family-base);
  transition: border-color var(--motion-fast), box-shadow var(--motion-fast);
}

.form-input:focus {
  border-color: var(--primary-color);
  box-shadow: 0 0 0 2px var(--primary-light);
  outline: none;
}

.form-input::placeholder {
  color: var(--text-muted);
}

.form-textarea {
  resize: vertical;
  min-height: 60px;
}

.form-textarea--code {
  font-family: var(--font-family-mono);
  font-size: 0.8125rem;
}

.form-error {
  padding: 8px 12px;
  background: rgba(248, 81, 73, 0.1);
  border: 1px solid rgba(248, 81, 73, 0.3);
  border-radius: var(--radius-md);
  color: var(--danger-color);
  font-size: 0.8125rem;
  margin-bottom: var(--space-4);
}

.form-actions {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: var(--space-3);
  padding-top: var(--space-2);
}

.btn-cancel {
  padding: 8px 16px;
  border-radius: var(--radius-md);
  background: transparent;
  border: 1px solid var(--border-color);
  color: var(--text-secondary);
  font-size: 0.875rem;
  font-family: var(--font-family-base);
  cursor: pointer;
  transition: all var(--motion-fast);
}

.btn-cancel:hover {
  background: var(--bg-hover);
  color: var(--text-primary);
}

.btn-submit {
  padding: 8px 20px;
  border-radius: var(--radius-md);
  background: var(--primary-color);
  border: none;
  color: var(--primary-contrast);
  font-size: 0.875rem;
  font-weight: 500;
  font-family: var(--font-family-base);
  cursor: pointer;
  transition: background var(--motion-fast);
}

.btn-submit:hover {
  background: var(--primary-hover);
}

.btn-submit:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

/* ===== Responsive ===== */
@media (max-width: 900px) {
  .skills-grid {
    grid-template-columns: 1fr;
  }
}
</style>
