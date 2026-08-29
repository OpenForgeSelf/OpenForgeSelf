<template>
  <div class="profile-view">
    <div class="profile-header">
      <div class="header-content">
        <h1 class="page-title">
          <i class="fa-solid fa-user-astronaut" />
          我的画像
        </h1>
        <p class="page-subtitle">了解你的使用习惯和能力成长</p>
      </div>
      <div class="header-stats">
        <div class="stat-card">
          <div class="stat-value">{{ profile?.totalUsageDays || 0 }}</div>
          <div class="stat-label">使用天数</div>
        </div>
        <div class="stat-card">
          <div class="stat-value">{{ profile?.totalActions || 0 }}</div>
          <div class="stat-label">总操作数</div>
        </div>
        <div class="stat-card">
          <div class="stat-value">{{ Math.round((profile?.efficiencyScore || 0) * 100) }}%</div>
          <div class="stat-label">效率评分</div>
        </div>
        <div class="stat-card">
          <div class="stat-value">{{ Math.round((profile?.learningRate || 0) * 100) }}%</div>
          <div class="stat-label">学习速率</div>
        </div>
      </div>
    </div>

    <div class="profile-content">
      <div class="profile-left">
        <div class="section-card">
          <div class="section-header">
            <h3>
              <i class="fa-solid fa-chart-pie" />
              使用风格
            </h3>
          </div>
          <div class="style-badge">
            <i class="fa-solid fa-robot" />
            <span>{{ profile?.usageStyle || '探索型' }}</span>
          </div>
          <p class="style-desc">{{ getStyleDescription(profile?.usageStyle) }}</p>
          <div class="style-info">
            <div class="info-item">
              <span class="info-label">主要使用时间</span>
              <span class="info-value">{{ profile?.primaryUseTime || '不固定' }}</span>
            </div>
          </div>
        </div>

        <div class="section-card">
          <div class="section-header">
            <h3>
              <i class="fa-solid fa-tools" />
              技能图谱
            </h3>
            <span class="badge">{{ skills.length }} 项技能</span>
          </div>
          <div v-if="skills.length > 0" class="skills-list">
            <div v-for="skill in skills" :key="skill.id" class="skill-item">
              <div class="skill-info">
                <span class="skill-name">{{ skill.skillName }}</span>
                <span class="skill-level">{{ getSkillLevel(skill.proficiencyLevel) }}</span>
              </div>
              <div class="skill-bar-bg">
                <div
                  class="skill-bar-fill"
                  :style="{ width: skill.proficiencyLevel * 100 + '%', background: getSkillColor(skill.proficiencyLevel) }"
                />
              </div>
              <div class="skill-stats">
                <span>使用 {{ skill.usageCount }} 次</span>
                <span>成功率 {{ Math.round(skill.successRate * 100) }}%</span>
              </div>
            </div>
          </div>
          <div v-else class="empty-skills">
            <i class="fa-solid fa-seedling" />
            <p>技能正在成长中...</p>
            <p class="hint">继续使用各种工具和功能，你的技能树会逐渐成长</p>
          </div>
        </div>

        <div class="section-card">
          <div class="section-header">
            <h3>
              <i class="fa-solid fa-heart" />
              偏好分析
            </h3>
          </div>
          <div v-if="preferences.length > 0" class="preferences-list">
            <div v-for="pref in preferences.slice(0, 8)" :key="pref.id" class="preference-item">
              <div class="pref-key">{{ pref.preferenceKey }}</div>
              <div class="pref-value">{{ pref.preferenceValue }}</div>
              <div class="pref-confidence">
                <div class="confidence-bar-bg">
                  <div
                    class="confidence-bar-fill"
                    :style="{ width: pref.confidence * 100 + '%' }"
                  />
                </div>
                <span>{{ Math.round(pref.confidence * 100) }}%</span>
              </div>
            </div>
          </div>
          <div v-else class="empty-text">暂无偏好数据</div>
        </div>
      </div>

      <div class="profile-right">
        <div class="section-card">
          <div class="section-header">
            <h3>
              <i class="fa-solid fa-lightbulb" />
              智能建议
            </h3>
            <button class="btn-icon" title="刷新" @click="loadSuggestions">
              <i class="fa-solid fa-refresh" :class="{ spinning: loading }" />
            </button>
          </div>
          <div v-if="suggestions.length > 0" class="suggestions-list">
            <div
              v-for="suggestion in suggestions"
              :key="suggestion.id"
              class="suggestion-card"
              :class="'priority-' + suggestion.priority"
            >
              <div class="suggestion-header">
                <div class="suggestion-icon">
                  <i :class="getSuggestionIcon(suggestion.type)" />
                </div>
                <div class="suggestion-title">
                  <h4>{{ suggestion.title }}</h4>
                  <span class="suggestion-type">{{ getSuggestionTypeLabel(suggestion.type) }}</span>
                </div>
                <span class="priority-badge">{{ getPriorityLabel(suggestion.priority) }}</span>
              </div>
              <p class="suggestion-desc">{{ suggestion.description }}</p>
              <div v-if="suggestion.content" class="suggestion-content">
                <p v-for="(line, idx) in suggestion.content.split('\n').slice(0, 3)" :key="idx">
                  {{ line }}
                </p>
              </div>
              <div class="suggestion-actions">
                <button v-if="suggestion.actionUrl" class="btn btn-primary btn-sm" @click="actionSuggestion(suggestion)">
                  <i class="fa-solid fa-arrow-right" />
                  去试试
                </button>
                <button class="btn btn-ghost btn-sm" @click="dismissSuggestion(suggestion.id)">
                  <i class="fa-solid fa-xmark" />
                  忽略
                </button>
              </div>
            </div>
          </div>
          <div v-else class="empty-suggestions">
            <i class="fa-solid fa-lightbulb" />
            <p>暂无建议</p>
            <p class="hint">继续使用系统，我们会为你生成个性化建议</p>
          </div>
        </div>

        <div class="section-card">
          <div class="section-header">
            <h3>
              <i class="fa-solid fa-radar" />
              行为模式
            </h3>
          </div>
          <div v-if="patterns.length > 0" class="patterns-list">
            <div v-for="pattern in patterns" :key="pattern.id" class="pattern-item">
              <div class="pattern-header">
                <div class="pattern-icon" :class="'pattern-' + getPatternClass(pattern.patternType)">
                  <i :class="getPatternIcon(pattern.patternType)" />
                </div>
                <div class="pattern-info">
                  <h4>{{ pattern.patternName }}</h4>
                  <span class="pattern-type">{{ getPatternTypeLabel(pattern.patternType) }}</span>
                </div>
                <div class="pattern-confidence">
                  <span class="confidence-value">{{ Math.round(pattern.confidence * 100) }}%</span>
                  <span class="confidence-label">置信度</span>
                </div>
              </div>
              <p class="pattern-desc">{{ pattern.patternDescription }}</p>
              <div class="pattern-meta">
                <span><i class="fa-solid fa-repeat" /> {{ pattern.occurrenceCount }} 次</span>
                <span><i class="fa-solid fa-clock" /> 首次发现: {{ formatDate(pattern.firstObservedAt) }}</span>
              </div>
            </div>
          </div>
          <div v-else class="empty-text">暂无模式分析数据</div>
        </div>

        <div class="section-card">
          <div class="section-header">
            <h3>
              <i class="fa-solid fa-fire" />
              热门功能
            </h3>
          </div>
          <div v-if="profile?.topTools && profile.topTools.length > 0" class="top-tools">
            <div v-for="(tool, index) in profile.topTools" :key="tool" class="top-tool-item">
              <span class="rank">{{ index + 1 }}</span>
              <span class="tool-name">{{ tool }}</span>
            </div>
          </div>
          <div v-else class="empty-text">暂无数据</div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { planningApi } from '@/services/planningApi'
import type { SuggestionEntity, UsagePatternEntity, UserSkillEntity, UserPreferenceEntity, UserProfileSummary } from '@/types/planning'

const profile = ref<UserProfileSummary | null>(null)
const skills = ref<UserSkillEntity[]>([])
const preferences = ref<UserPreferenceEntity[]>([])
const suggestions = ref<SuggestionEntity[]>([])
const patterns = ref<UsagePatternEntity[]>([])
const loading = ref(false)

onMounted(() => {
  loadAll()
})

async function loadAll() {
  loading.value = true
  try {
    await Promise.all([
      loadProfile(),
      loadSkills(),
      loadPreferences(),
      loadSuggestions(),
      loadPatterns()
    ])
  } catch (e) {
    console.error('加载画像数据失败:', e)
  } finally {
    loading.value = false
  }
}

async function loadProfile() {
  try {
    profile.value = await planningApi.getProfile()
  } catch (e) {
    console.error('加载用户画像失败:', e)
  }
}

async function loadSkills() {
  try {
    skills.value = await planningApi.getSkills()
  } catch (e) {
    console.error('加载技能失败:', e)
  }
}

async function loadPreferences() {
  try {
    preferences.value = await planningApi.getPreferences()
  } catch (e) {
    console.error('加载偏好失败:', e)
  }
}

async function loadSuggestions() {
  try {
    suggestions.value = await planningApi.getSuggestions(10)
  } catch (e) {
    console.error('加载建议失败:', e)
  }
}

async function loadPatterns() {
  try {
    patterns.value = await planningApi.getPatterns(30)
  } catch (e) {
    console.error('加载模式失败:', e)
  }
}

async function dismissSuggestion(id: number) {
  try {
    await planningApi.dismissSuggestion(id)
    suggestions.value = suggestions.value.filter(s => s.id !== id)
  } catch (e) {
    console.error('忽略建议失败:', e)
  }
}

function actionSuggestion(suggestion: SuggestionEntity) {
  if (suggestion.actionUrl) {
    planningApi.actionSuggestion(suggestion.id)
    window.location.hash = suggestion.actionUrl
  }
}

function getStyleDescription(style?: string): string {
  const descriptions: Record<string, string> = {
    '探索型': '你正在探索 ForgeSelf 的各项功能，保持好奇心，发现更多可能性！',
    '成长型': '你在快速学习和成长，继续保持这个势头！',
    '进阶用户': '你已经掌握了不少功能，尝试挑战更复杂的任务吧！',
    '工具达人': '你擅长使用各种工具，效率很高。试试工作流自动化？',
    '自动化专家': '你已经是自动化高手了，享受高效工作的乐趣吧！'
  }
  return descriptions[style || '探索型'] || '继续探索，发现更多可能'
}

function getSkillLevel(level: number): string {
  if (level < 0.2) return '入门'
  if (level < 0.4) return '熟悉'
  if (level < 0.6) return '熟练'
  if (level < 0.8) return '精通'
  return '专家'
}

function getSkillColor(level: number): string {
  if (level < 0.2) return '#94a3b8'
  if (level < 0.4) return '#60a5fa'
  if (level < 0.6) return '#34d399'
  if (level < 0.8) return '#fbbf24'
  return '#f472b6'
}

function getSuggestionIcon(type: number): string {
  const icons: Record<number, string> = {
    0: 'fa-solid fa-wrench',
    1: 'fa-solid fa-diagram-project',
    2: 'fa-solid fa-brain',
    3: 'fa-solid fa-users-gear',
    4: 'fa-solid fa-keyboard',
    5: 'fa-solid fa-rocket',
    6: 'fa-solid fa-graduation-cap'
  }
  return icons[type] || 'fa-solid fa-lightbulb'
}

function getSuggestionTypeLabel(type: number): string {
  const labels: Record<number, string> = {
    0: '工具推荐',
    1: '工作流推荐',
    2: '记忆提醒',
    3: 'Agent 推荐',
    4: '快捷建议',
    5: '优化提示',
    6: '学习推荐'
  }
  return labels[type] || '建议'
}

function getPriorityLabel(priority: number): string {
  const labels: Record<number, string> = {
    0: '低',
    1: '中',
    2: '高',
    3: '紧急'
  }
  return labels[priority] || '中'
}

function getPatternIcon(type: number): string {
  const icons: Record<number, string> = {
    0: 'fa-solid fa-clock',
    1: 'fa-solid fa-layer-group',
    2: 'fa-solid fa-chart-bar',
    3: 'fa-solid fa-circle-nodes',
    4: 'fa-solid fa-person'
  }
  return icons[type] || 'fa-solid fa-chart-line'
}

function getPatternTypeLabel(type: number): string {
  const labels: Record<number, string> = {
    0: '时间模式',
    1: '顺序模式',
    2: '频率模式',
    3: '上下文模式',
    4: '行为模式'
  }
  return labels[type] || '模式'
}

function getPatternClass(type: number): string {
  const classNames: Record<number, string> = {
    0: 'temporal',
    1: 'sequential',
    2: 'frequency',
    3: 'contextual',
    4: 'behavioral'
  }
  return classNames[type] || 'temporal'
}

function formatDate(dateStr: string): string {
  const date = new Date(dateStr)
  return date.toLocaleDateString('zh-CN')
}
</script>

<style scoped>
.profile-view {
  min-height: 100%;
  background-color: var(--el-bg-color);
}

.profile-header {
  background: linear-gradient(135deg, var(--brand-amber-700) 0%, var(--brand-amber-600) 50%, var(--brand-amber-500) 100%);
  color: white;
  padding: 32px 24px;
}

.header-content {
  margin-bottom: 24px;
}

.page-title {
  font-size: 28px;
  font-weight: 600;
  margin: 0 0 8px 0;
  display: flex;
  align-items: center;
  gap: 12px;
}

.page-subtitle {
  font-size: 14px;
  margin: 0;
  opacity: 0.9;
}

.header-stats {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
}

.stat-card {
  background: rgba(255, 255, 255, 0.15);
  backdrop-filter: blur(10px);
  border-radius: var(--el-border-radius-base);
  padding: 16px;
  text-align: center;
}

.stat-value {
  font-size: 28px;
  font-weight: 700;
  margin-bottom: 4px;
}

.stat-label {
  font-size: 12px;
  opacity: 0.8;
}

.profile-content {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 20px;
  padding: 20px;
}

.section-card {
  background: var(--el-bg-color);
  border-radius: 12px;
  padding: 20px;
  margin-bottom: 20px;
  border: 1px solid var(--el-border-color);
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.section-header h3 {
  margin: 0;
  font-size: 16px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  display: flex;
  align-items: center;
  gap: 10px;
}

.section-header h3 i {
  color: var(--el-color-primary);
}

.badge {
  background: var(--el-color-primary-light-9);
  color: var(--el-color-primary);
  padding: 2px 10px;
  border-radius: var(--radius-pill);
  font-size: 12px;
  font-weight: 500;
}

.btn-icon {
  background: none;
  border: none;
  color: var(--el-text-color-secondary);
  cursor: pointer;
  padding: 6px;
  border-radius: var(--el-border-radius-small);
  font-size: 14px;
}

.btn-icon:hover {
  background: var(--el-fill-color);
  color: var(--el-text-color-regular);
}

.spinning {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

.style-badge {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  background: linear-gradient(135deg, rgba(251, 191, 36, 0.2) 0%, rgba(245, 158, 11, 0.25) 100%);
  color: var(--brand-amber-700);
  padding: 8px 16px;
  border-radius: var(--radius-pill);
  font-weight: 600;
  margin-bottom: 12px;
}

.style-desc {
  font-size: 13px;
  color: var(--el-text-color-regular);
  line-height: 1.6;
  margin: 0 0 16px 0;
}

.style-info {
  border-top: 1px solid var(--el-border-color);
  padding-top: 12px;
}

.info-item {
  display: flex;
  justify-content: space-between;
  font-size: 13px;
}

.info-label {
  color: var(--el-text-color-regular);
}

.info-value {
  font-weight: 500;
  color: var(--el-text-color-primary);
}

.skills-list {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.skill-item {
  padding: 12px;
  background: var(--el-fill-color-light);
  border-radius: var(--el-border-radius-base);
}

.skill-info {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
}

.skill-name {
  font-weight: 500;
  color: var(--el-text-color-primary);
  font-size: 14px;
}

.skill-level {
  font-size: 12px;
  padding: 2px 8px;
  background: var(--el-color-primary-light-9);
  color: var(--el-color-primary);
  border-radius: var(--el-border-radius-small);
  font-weight: 500;
}

.skill-bar-bg {
  height: 8px;
  background: var(--el-border-color);
  border-radius: 4px;
  overflow: hidden;
  margin-bottom: 8px;
}

.skill-bar-fill {
  height: 100%;
  border-radius: 4px;
  transition: width 0.3s;
}

.skill-stats {
  display: flex;
  justify-content: space-between;
  font-size: 11px;
  color: var(--el-text-color-secondary);
}

.empty-skills,
.empty-suggestions {
  text-align: center;
  padding: 32px 16px;
  color: var(--el-text-color-secondary);
}

.empty-skills i,
.empty-suggestions i {
  font-size: 36px;
  margin-bottom: 12px;
  color: var(--el-text-color-secondary);
}

.empty-skills p,
.empty-suggestions p {
  margin: 4px 0;
  font-size: 14px;
}

.empty-skills .hint,
.empty-suggestions .hint {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.preferences-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.preference-item {
  padding: 10px 12px;
  background: var(--el-fill-color-light);
  border-radius: 10px;
}

.pref-key {
  font-size: 12px;
  color: var(--el-text-color-regular);
  margin-bottom: 4px;
}

.pref-value {
  font-size: 14px;
  font-weight: 500;
  color: var(--el-text-color-primary);
  margin-bottom: 6px;
}

.pref-confidence {
  display: flex;
  align-items: center;
  gap: 8px;
}

.confidence-bar-bg {
  flex: 1;
  height: 4px;
  background: var(--el-border-color);
  border-radius: 2px;
  overflow: hidden;
}

.confidence-bar-fill {
  height: 100%;
  background: var(--el-color-success);
  border-radius: 2px;
}

.pref-confidence span {
  font-size: 11px;
  color: var(--el-text-color-secondary);
  min-width: 36px;
  text-align: right;
}

.suggestions-list {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.suggestion-card {
  border: 1px solid var(--el-border-color);
  border-radius: var(--el-border-radius-base);
  padding: 16px;
  transition: all 150ms ease;
}

.suggestion-card:hover {
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.05);
}

.suggestion-card.priority-2 {
  border-left: 4px solid var(--el-color-warning);
}

.suggestion-card.priority-3 {
  border-left: 4px solid var(--el-color-danger);
}

.suggestion-header {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  margin-bottom: 10px;
}

.suggestion-icon {
  width: 40px;
  height: 40px;
  border-radius: 10px;
  background: var(--el-color-primary-light-9);
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--el-color-primary);
  font-size: 16px;
  flex-shrink: 0;
}

.suggestion-title {
  flex: 1;
  min-width: 0;
}

.suggestion-title h4 {
  margin: 0 0 2px 0;
  font-size: 14px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}

.suggestion-type {
  font-size: 11px;
  color: var(--el-color-primary);
}

.priority-badge {
  font-size: 11px;
  padding: 2px 8px;
  border-radius: var(--el-border-radius-small);
  background: var(--el-fill-color-light);
  color: var(--el-text-color-secondary);
  flex-shrink: 0;
}

.priority-2 .priority-badge {
  background: rgba(217, 119, 6, 0.12);
  color: var(--el-color-warning);
}

.priority-3 .priority-badge {
  background: rgba(185, 28, 28, 0.12);
  color: var(--el-color-danger);
}

.suggestion-desc {
  font-size: 13px;
  color: var(--el-text-color-regular);
  margin: 0 0 10px 0;
  line-height: 1.5;
}

.suggestion-content {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  margin-bottom: 12px;
  line-height: 1.6;
}

.suggestion-content p {
  margin: 2px 0;
}

.suggestion-actions {
  display: flex;
  gap: 8px;
}

.btn {
  padding: 8px 14px;
  border-radius: var(--el-border-radius-small);
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
  border: none;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  transition: all 150ms ease;
}

.btn-sm {
  padding: 6px 12px;
  font-size: 12px;
}

.btn-primary {
  background: var(--el-color-primary);
  color: var(--el-color-white);
}

.btn-primary:hover {
  transform: translateY(-1px);
  box-shadow: 0 4px 12px var(--primary-light);
}

.btn-ghost {
  background: transparent;
  color: var(--el-text-color-secondary);
}

.btn-ghost:hover {
  background: var(--el-fill-color);
  color: var(--el-text-color-primary);
}

.patterns-list {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.pattern-item {
  padding: 14px;
  background: var(--el-fill-color-light);
  border-radius: var(--el-border-radius-base);
}

.pattern-header {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 10px;
}

.pattern-icon {
  width: 36px;
  height: 36px;
  border-radius: var(--el-border-radius-small);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 14px;
  flex-shrink: 0;
}

.pattern-temporal {
  background: var(--el-color-primary-light-9);
  color: var(--el-color-primary);
}

.pattern-frequency {
  background: rgba(4, 120, 87, 0.12);
  color: var(--el-color-success);
}

.pattern-sequential {
  background: rgba(217, 119, 6, 0.12);
  color: var(--el-color-warning);
}

.pattern-behavioral {
  background: rgba(139, 92, 246, 0.12);
  color: #7c3aed;
}

.pattern-contextual {
  background: rgba(190, 24, 93, 0.12);
  color: #be185d;
}

.pattern-info {
  flex: 1;
  min-width: 0;
}

.pattern-info h4 {
  margin: 0 0 2px 0;
  font-size: 14px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}

.pattern-type {
  font-size: 11px;
  color: var(--el-text-color-secondary);
}

.pattern-confidence {
  text-align: right;
  flex-shrink: 0;
}

.confidence-value {
  display: block;
  font-size: 16px;
  font-weight: 700;
  color: var(--el-color-success);
}

.confidence-label {
  font-size: 11px;
  color: var(--el-text-color-secondary);
}

.pattern-desc {
  font-size: 13px;
  color: var(--el-text-color-regular);
  margin: 0 0 10px 0;
  line-height: 1.5;
}

.pattern-meta {
  display: flex;
  gap: 16px;
  font-size: 11px;
  color: var(--el-text-color-secondary);
}

.pattern-meta span {
  display: flex;
  align-items: center;
  gap: 4px;
}

.top-tools {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.top-tool-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 12px;
  background: var(--el-fill-color-light);
  border-radius: var(--el-border-radius-small);
}

.rank {
  width: 24px;
  height: 24px;
  border-radius: var(--el-border-radius-small);
  background: var(--el-color-primary);
  color: var(--el-color-white);
  font-size: 12px;
  font-weight: 700;
  display: flex;
  align-items: center;
  justify-content: center;
}

.top-tool-item:nth-child(2) .rank {
  background: var(--el-color-primary);
  opacity: 0.8;
}

.top-tool-item:nth-child(3) .rank {
  background: var(--el-color-primary);
  opacity: 0.6;
}

.tool-name {
  flex: 1;
  font-size: 13px;
  font-weight: 500;
  color: var(--el-text-color-primary);
}

.empty-text {
  text-align: center;
  padding: 24px;
  color: var(--el-text-color-secondary);
  font-size: 13px;
}

@media (max-width: 1024px) {
  .profile-content {
    grid-template-columns: 1fr;
  }

  .header-stats {
    grid-template-columns: repeat(2, 1fr);
  }
}
</style>
