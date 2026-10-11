<template>
  <div class="profile-view">
    <div class="profile-header">
      <PageHead
        eyebrow="Profile · 我的画像"
        title="我的画像"
      />
      <div class="header-stats">
        <div class="stat-card glass">
          <div class="stat-value">{{ profile?.totalUsageDays || 0 }}</div>
          <div class="stat-label">使用天数</div>
        </div>
        <div class="stat-card glass">
          <div class="stat-value">{{ profile?.totalActions || 0 }}</div>
          <div class="stat-label">总操作数</div>
        </div>
        <div class="stat-card glass">
          <div class="stat-value">{{ Math.round((profile?.efficiencyScore || 0) * 100) }}%</div>
          <div class="stat-label">效率评分</div>
        </div>
        <div class="stat-card glass">
          <div class="stat-value">{{ Math.round((profile?.learningRate || 0) * 100) }}%</div>
          <div class="stat-label">学习速率</div>
        </div>
      </div>
    </div>

    <div class="profile-content">
      <div class="profile-left">
        <div class="section-card glass">
          <div class="section-header">
            <h3>
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <line x1="18" y1="20" x2="18" y2="10" /><line x1="12" y1="20" x2="12" y2="4" /><line x1="6" y1="20" x2="6" y2="14" />
              </svg>
              使用风格
            </h3>
          </div>
          <div class="style-badge">
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

        <div class="section-card glass">
          <div class="section-header">
            <h3>
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <polygon points="12 2 2 7 12 12 22 7 12 2" /><polyline points="2 17 12 22 22 17" /><polyline points="2 12 12 17 22 12" />
              </svg>
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
                  :class="getSkillLevelClass(skill.proficiencyLevel)"
                  :style="{ width: skill.proficiencyLevel * 100 + '%' }"
                />
              </div>
              <div class="skill-stats">
                <span>使用 {{ skill.usageCount }} 次</span>
                <span>成功率 {{ Math.round(skill.successRate * 100) }}%</span>
              </div>
            </div>
          </div>
          <EmptyState
            v-else
            icon="<svg xmlns='http://www.w3.org/2000/svg' width='28' height='28' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><path d='M12 2L12 22' /><path d='M12 8L8 4' /><path d='M12 8L16 4' /><path d='M12 14L7 9' /><path d='M12 14L17 9' /></svg>"
            title="技能正在成长中"
            sub="继续使用各种工具和功能，你的技能树会逐渐成长"
          />
        </div>

        <div class="section-card glass">
          <div class="section-header">
            <h3>
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z" />
              </svg>
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
          <EmptyState
            v-else
            icon="<svg xmlns='http://www.w3.org/2000/svg' width='28' height='28' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><circle cx='12' cy='12' r='10' /><line x1='12' y1='16' x2='12' y2='12' /><line x1='12' y1='8' x2='12.01' y2='8' /></svg>"
            title="暂无偏好数据"
            sub="使用过程中会自动学习你的偏好"
          />
        </div>
      </div>

      <div class="profile-right">
        <div class="section-card glass">
          <div class="section-header">
            <h3>
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d='M9 18h6' /><path d='M10 22h4' /><path d='M12 2a7 7 0 0 0-4 12.7V17h8v-2.3A7 7 0 0 0 12 2z' />
              </svg>
              智能建议
            </h3>
            <button class="btn-icon" title="刷新" @click="loadSuggestions">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" :class="{ spinning: loading }">
                <polyline points="23 4 23 10 17 10" /><path d="M20.49 15a9 9 0 1 1-2.12-9.36L23 10" />
              </svg>
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
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                    <path d='M9 18h6' /><path d='M10 22h4' /><path d='M12 2a7 7 0 0 0-4 12.7V17h8v-2.3A7 7 0 0 0 12 2z' />
                  </svg>
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
                  去试试
                </button>
                <button class="btn btn-ghost btn-sm" @click="dismissSuggestion(suggestion.id)">
                  忽略
                </button>
              </div>
            </div>
          </div>
          <EmptyState
            v-else
            icon="<svg xmlns='http://www.w3.org/2000/svg' width='28' height='28' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><path d='M9 18h6' /><path d='M10 22h4' /><path d='M12 2a7 7 0 0 0-4 12.7V17h8v-2.3A7 7 0 0 0 12 2z' /></svg>"
            title="暂无建议"
            sub="继续使用系统，我们会为你生成个性化建议"
          />
        </div>

        <div class="section-card glass">
          <div class="section-header">
            <h3>
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <circle cx="12" cy="12" r="10" /><circle cx="12" cy="12" r="6" /><circle cx="12" cy="12" r="2" />
              </svg>
              行为模式
            </h3>
          </div>
          <div v-if="patterns.length > 0" class="patterns-list">
            <div v-for="pattern in patterns" :key="pattern.id" class="pattern-item">
              <div class="pattern-header">
                <div class="pattern-icon" :class="'pattern-' + getPatternClass(pattern.patternType)">
                  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                    <circle cx="12" cy="12" r="10" /><polyline points="12 6 12 12 16 14" />
                  </svg>
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
                <span>{{ pattern.occurrenceCount }} 次</span>
                <span>首次发现: {{ formatDate(pattern.firstObservedAt) }}</span>
              </div>
            </div>
          </div>
          <EmptyState
            v-else
            icon="<svg xmlns='http://www.w3.org/2000/svg' width='28' height='28' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><circle cx='12' cy='12' r='10' /><circle cx='12' cy='12' r='6' /><circle cx='12' cy='12' r='2' /></svg>"
            title="暂无模式分析数据"
            sub="使用一段时间后将自动分析行为模式"
          />
        </div>

        <div class="section-card glass">
          <div class="section-header">
            <h3>
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d='M8.5 14.5A2.5 2.5 0 0 0 11 12c0-1.38-.5-2-1-3-1.072-2.143-.224-4.054 2-6 .5 2.5 2 4.9 4 6.5 2 1.6 3 3.5 3 5.5a7 7 0 1 1-14 0c0-1.153.433-2.294 1-3a2.5 2.5 0 0 0 2.5 2.5z' />
              </svg>
              热门功能
            </h3>
          </div>
          <div v-if="profile?.topTools && profile.topTools.length > 0" class="top-tools">
            <div v-for="(tool, index) in profile.topTools" :key="tool" class="top-tool-item">
              <span class="rank">{{ index + 1 }}</span>
              <span class="tool-name">{{ tool }}</span>
            </div>
          </div>
          <EmptyState
            v-else
            icon="<svg xmlns='http://www.w3.org/2000/svg' width='28' height='28' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'><path d='M13 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V9z' /><polyline points='13 2 13 9 20 9' /></svg>"
            title="暂无数据"
            sub="使用功能后将自动统计热门排名"
          />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { planningApi } from '@/services/planningApi'
import type { SuggestionEntity, UsagePatternEntity, UserSkillEntity, UserPreferenceEntity, UserProfileSummary } from '@/types/planning'
import PageHead from '@/components/PageHead.vue'
import EmptyState from '@/components/EmptyState.vue'

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

function getSkillLevelClass(level: number): string {
  if (level < 0.2) return 'skill-level-1'
  if (level < 0.4) return 'skill-level-2'
  if (level < 0.6) return 'skill-level-3'
  if (level < 0.8) return 'skill-level-4'
  return 'skill-level-5'
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
  padding: 24px;
  max-width: 1200px;
  margin: 0 auto;
}

.profile-header {
  margin-bottom: 20px;
}

.header-stats {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
  margin-top: 20px;
}

.stat-card {
  padding: 16px;
  text-align: center;
}

.stat-value {
  font-family: var(--font-mono);
  font-size: 24px;
  font-weight: 700;
  color: var(--gold-400);
  margin-bottom: 4px;
}

.stat-label {
  font-size: 12px;
  color: var(--fg-tertiary);
}

.profile-content {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 20px;
}

.section-card {
  padding: 20px;
  margin-bottom: 20px;
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
  color: var(--fg-primary);
  display: flex;
  align-items: center;
  gap: 10px;
  font-family: var(--font-serif-cn);
}

.section-header h3 svg {
  color: var(--gold-400);
}

.badge {
  background: var(--gold-tint);
  color: var(--gold-400);
  padding: 2px 10px;
  border-radius: var(--radius-sm);
  font-size: 12px;
  font-weight: 500;
}

.btn-icon {
  background: none;
  border: none;
  color: var(--fg-tertiary);
  cursor: pointer;
  padding: 6px;
  border-radius: var(--radius-sm);
  display: flex;
  transition: background var(--dur-fast) var(--ease-out);
}

.btn-icon:hover {
  background: var(--paper-tint);
  color: var(--fg-secondary);
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
  background: var(--gold-tint);
  color: var(--gold-400);
  padding: 8px 16px;
  border-radius: var(--radius-md);
  font-weight: 600;
  margin-bottom: 12px;
  font-family: var(--font-serif-cn);
}

.style-desc {
  font-size: 13px;
  color: var(--fg-secondary);
  line-height: 1.6;
  margin: 0 0 16px 0;
}

.style-info {
  border-top: 1px solid var(--border-subtle);
  padding-top: 12px;
}

.info-item {
  display: flex;
  justify-content: space-between;
  font-size: 13px;
}

.info-label {
  color: var(--fg-secondary);
}

.info-value {
  font-weight: 500;
  color: var(--fg-primary);
}

.skills-list {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.skill-item {
  padding: 12px;
  background: var(--paper-tint);
  border-radius: var(--radius-md);
}

.skill-info {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
}

.skill-name {
  font-weight: 500;
  color: var(--fg-primary);
  font-size: 14px;
}

.skill-level {
  font-size: 12px;
  padding: 2px 8px;
  background: var(--gold-tint);
  color: var(--gold-400);
  border-radius: var(--radius-sm);
  font-weight: 500;
}

.skill-bar-bg {
  height: 8px;
  background: var(--border-default);
  border-radius: 4px;
  overflow: hidden;
  margin-bottom: 8px;
}

.skill-bar-fill {
  height: 100%;
  border-radius: 4px;
  transition: width var(--dur-base) var(--ease-out);
}

.skill-level-1 { background: var(--fg-muted); }
.skill-level-2 { background: var(--el-color-info); }
.skill-level-3 { background: var(--bronze-500); }
.skill-level-4 { background: var(--gold-400); }
.skill-level-5 { background: var(--cinnabar-400); }

.skill-stats {
  display: flex;
  justify-content: space-between;
  font-size: 11px;
  color: var(--fg-tertiary);
  font-family: var(--font-mono);
}

.preferences-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.preference-item {
  padding: 10px 12px;
  background: var(--paper-tint);
  border-radius: var(--radius-md);
}

.pref-key {
  font-size: 12px;
  color: var(--fg-secondary);
  margin-bottom: 4px;
}

.pref-value {
  font-size: 14px;
  font-weight: 500;
  color: var(--fg-primary);
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
  background: var(--border-default);
  border-radius: 2px;
  overflow: hidden;
}

.confidence-bar-fill {
  height: 100%;
  background: var(--bronze-500);
  border-radius: 2px;
}

.pref-confidence span {
  font-size: 11px;
  color: var(--fg-tertiary);
  min-width: 36px;
  text-align: right;
  font-family: var(--font-mono);
}

.suggestions-list {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.suggestion-card {
  border: 1px solid var(--border-subtle);
  border-radius: var(--radius-md);
  padding: 16px;
  transition: box-shadow var(--dur-fast) var(--ease-out);
}

.suggestion-card:hover {
  box-shadow: var(--shadow-sm);
}

.suggestion-card.priority-2 {
  border-left: 4px solid var(--gold-500);
}

.suggestion-card.priority-3 {
  border-left: 4px solid var(--cinnabar-400);
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
  border-radius: var(--radius-md);
  background: var(--gold-tint);
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--gold-400);
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
  color: var(--fg-primary);
}

.suggestion-type {
  font-size: 11px;
  color: var(--gold-400);
}

.priority-badge {
  font-size: 11px;
  padding: 2px 8px;
  border-radius: var(--radius-sm);
  background: var(--paper-tint);
  color: var(--fg-tertiary);
  flex-shrink: 0;
}

.priority-2 .priority-badge {
  background: var(--gold-tint);
  color: var(--gold-500);
}

.priority-3 .priority-badge {
  background: color-mix(in srgb, var(--cinnabar-400) 12%, transparent);
  color: var(--cinnabar-400);
}

.suggestion-desc {
  font-size: 13px;
  color: var(--fg-secondary);
  margin: 0 0 10px 0;
  line-height: 1.5;
}

.suggestion-content {
  font-size: 12px;
  color: var(--fg-tertiary);
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
  border-radius: var(--radius-sm);
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
  border: none;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  transition: transform var(--dur-instant) var(--ease-out);
  font-family: var(--font-sans-cn);
}

.btn-sm {
  padding: 6px 12px;
  font-size: 12px;
}

.btn-primary {
  background: var(--gold-400);
  color: var(--ink-950);
}

.btn-primary:hover {
  transform: translateY(-1px);
  box-shadow: var(--shadow-sm);
}

.btn-ghost {
  background: transparent;
  color: var(--fg-tertiary);
}

.btn-ghost:hover {
  background: var(--paper-tint);
  color: var(--fg-primary);
}

.patterns-list {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.pattern-item {
  padding: 14px;
  background: var(--paper-tint);
  border-radius: var(--radius-md);
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
  border-radius: var(--radius-sm);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.pattern-temporal {
  background: var(--gold-tint);
  color: var(--gold-400);
}

.pattern-frequency {
  background: color-mix(in srgb, var(--bronze-500) 12%, transparent);
  color: var(--bronze-500);
}

.pattern-sequential {
  background: color-mix(in srgb, var(--gold-500) 12%, transparent);
  color: var(--gold-500);
}

.pattern-behavioral {
  background: color-mix(in srgb, var(--cinnabar-400) 12%, transparent);
  color: var(--cinnabar-400);
}

.pattern-contextual {
  background: var(--gold-tint-strong);
  color: var(--gold-300);
}

.pattern-info {
  flex: 1;
  min-width: 0;
}

.pattern-info h4 {
  margin: 0 0 2px 0;
  font-size: 14px;
  font-weight: 600;
  color: var(--fg-primary);
}

.pattern-type {
  font-size: 11px;
  color: var(--fg-tertiary);
}

.pattern-confidence {
  text-align: right;
  flex-shrink: 0;
}

.confidence-value {
  display: block;
  font-size: 16px;
  font-weight: 700;
  color: var(--bronze-500);
  font-family: var(--font-mono);
}

.confidence-label {
  font-size: 11px;
  color: var(--fg-tertiary);
}

.pattern-desc {
  font-size: 13px;
  color: var(--fg-secondary);
  margin: 0 0 10px 0;
  line-height: 1.5;
}

.pattern-meta {
  display: flex;
  gap: 16px;
  font-size: 11px;
  color: var(--fg-tertiary);
  font-family: var(--font-mono);
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
  background: var(--paper-tint);
  border-radius: var(--radius-sm);
}

.rank {
  width: 24px;
  height: 24px;
  border-radius: var(--radius-sm);
  background: var(--gold-400);
  color: var(--ink-950);
  font-size: 12px;
  font-weight: 700;
  display: flex;
  align-items: center;
  justify-content: center;
  font-family: var(--font-mono);
}

.top-tool-item:nth-child(2) .rank {
  background: var(--gold-400);
  opacity: 0.8;
}

.top-tool-item:nth-child(3) .rank {
  background: var(--gold-400);
  opacity: 0.6;
}

.tool-name {
  flex: 1;
  font-size: 13px;
  font-weight: 500;
  color: var(--fg-primary);
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
