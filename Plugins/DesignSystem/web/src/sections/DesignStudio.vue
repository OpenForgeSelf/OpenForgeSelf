<script setup lang="ts">
/**
 * 设计工作台（Design Studio）—— 设计插件的核心能力（通用生成器）。
 *
 * 本质：输入一段需求描述 → 生成「一份属于该需求的设计系统」（品牌 / 颜色 / 类型 / 间距 / 组件 / 应用示例 / 自检）
 *      → 预览（内联换肤预览 + 自包含 preview.html）→ 导出（6 类产出文件）。
 *
 * 本组件不含任何具体品牌：生成引擎据行业画像与颜色词推导品牌色相，同一段需求永远产出同一套系统。
 * 生成的设计系统会 emit 给外壳（update:activeDs），使「设计令牌 / 组件库 / 控制台 / 官网」等展示页即时换肤为该系统。
 *
 * 生产化要点：
 * - 持久化：工作区 + 历史落 localStorage，刷新可恢复（后端持久化待评估，见 README 路线图）。
 * - 健壮性：空输入校验、生成异常兜底为错误态（不让整页白屏）。
 * - 反馈：复制/下载/恢复均有 aria-live 提示。
 * - 可达性：tablist/tab/tabpanel 语义、aria-selected、表单 aria-label、focus-visible。
 */
import { computed, onMounted, ref, watch } from 'vue'
import type { DesignSystem } from '../design/schema'
import { INDUSTRY_OPTIONS, generateDesignSystem } from '../design/generate'
import { tokensToStyleAttr } from '../design/tokensToCss'
import { buildArtifacts, downloadArtifact, openPreviewInNewTab, type Artifact } from '../design/exporters'
import {
  clearHistory,
  loadHistory,
  loadWorkspace,
  pushHistory,
  removeHistory,
  saveWorkspace,
  type HistoryEntry,
} from '../design/storage'
import DsButton from '../components/DsButton.vue'
import Icon from '../components/Icon.vue'

const props = defineProps<{ activeDs: DesignSystem }>()
const emit = defineEmits<{ 'update:activeDs': [DesignSystem] }>()

const INDUSTRY_LABEL: Record<string, string> = Object.fromEntries(
  INDUSTRY_OPTIONS.map((o) => [o.key, o.label]),
)

const SAMPLES: { label: string; brief: string }[] = [
  {
    label: '运维监控平台',
    brief:
      '我要做一个面向 SRE 团队的《星尘控制台》，用于管理跨区域微服务集群：需要服务健康总览、P99 与吞吐指标、服务依赖拓扑、参数配置与发布部署，要求异常能快速定位。',
  },
  {
    label: '潮流电商',
    brief:
      '做一个面向年轻用户的潮流商城，支持商品浏览筛选、详情页加购、购物车试算、下单支付与订单物流追踪，希望转化路径尽量短，主色用暖橙色。',
  },
  {
    label: '在线教育',
    brief:
      '做一个面向 K12 的在线学习平台，学生可以选课、看课程视频、做章节测验、查看学习进度，老师端能管理班级与题库，希望界面亲和、鼓励性强。',
  },
  {
    label: '医疗健康',
    brief:
      '做一个面向医院的医护工作台，医生可以查看今日患者、调阅病历、下达医嘱，患者端能预约问诊与查看检查报告，要求界面清洁、留白充足、对比明确。',
  },
]

const brief = ref('')
const industry = ref<string>('auto')
const design = ref<DesignSystem | null>(null)
const history = ref<HistoryEntry[]>([])
const restored = ref(false)
const error = ref('')
const toast = ref('')
let toastTimer: ReturnType<typeof setTimeout> | null = null

const tabs = [
  { key: 'overview', label: '概览' },
  { key: 'brand', label: '品牌与颜色' },
  { key: 'type', label: '类型与间距' },
  { key: 'components', label: '组件' },
  { key: 'applications', label: '应用示例' },
  { key: 'live', label: '实时预览' },
  { key: 'artifacts', label: '产出文件' },
] as const
const activeTab = ref<(typeof tabs)[number]['key']>('overview')

const artifacts = computed<Artifact[]>(() => (design.value ? buildArtifacts(design.value) : []))
const htmlArtifact = computed(() => artifacts.value.find((a) => a.filename === 'preview.html') ?? null)
const canGenerate = computed(() => brief.value.trim().length >= 10)
/** 实时预览容器样式：把生成系统的变量注入容器，后代组件整体换肤。 */
const liveStyle = computed(() => (design.value ? tokensToStyleAttr(design.value) : ''))

onMounted(() => {
  const ws = loadWorkspace()
  if (ws) {
    brief.value = ws.brief
    // 兼容 v1.1.0 旧 workspace：旧 design 没有 meta.seed，直接丢弃以免访问 undefined
    const validDesign = ws.design && (ws.design as DesignSystem).meta?.seed ? ws.design : null
    if (validDesign) {
      design.value = validDesign
      industry.value = validDesign.meta.seed.industry
      restored.value = true
    } else if (typeof ws.industry === 'string') {
      industry.value = ws.industry
    }
  } else {
    brief.value = SAMPLES[0].brief
  }
  history.value = loadHistory()
})

watch([brief, industry, design], () => {
  saveWorkspace({
    brief: brief.value,
    industry: industry.value,
    design: design.value,
    updatedAt: new Date().toISOString(),
  })
})

function showToast(msg: string) {
  toast.value = msg
  if (toastTimer) clearTimeout(toastTimer)
  toastTimer = setTimeout(() => {
    toast.value = ''
  }, 2600)
}

function generate() {
  error.value = ''
  const text = brief.value.trim()
  if (text.length < 10) {
    error.value = '需求描述太短（至少 10 个字），请补充目标用户与核心场景后再生成。'
    return
  }
  try {
    const ds = generateDesignSystem(text, industry.value === 'auto' ? {} : { industry: industry.value })
    design.value = ds
    activeTab.value = 'overview'
    history.value = pushHistory(ds)
    emit('update:activeDs', ds)
    showToast('已生成设计系统，可在「设计令牌 / 组件库」查看换肤效果')
  } catch (e) {
    error.value = `生成失败：${e instanceof Error ? e.message : String(e)}`
  }
}

function useSample(i: number) {
  brief.value = SAMPLES[i].brief
  error.value = ''
}

function loadFromHistory(entry: HistoryEntry) {
  // 防御 v1.1.0 旧历史（无 meta.seed）被点击
  if (!entry.design || !(entry.design as DesignSystem).meta?.seed) {
    history.value = removeHistory(entry.id)
    showToast('该历史记录已过期，已自动清理')
    return
  }
  brief.value = entry.brief
  design.value = entry.design
  industry.value = entry.industry
  restored.value = true
  error.value = ''
  activeTab.value = 'overview'
  emit('update:activeDs', entry.design)
  showToast(`已载入历史：${entry.title}`)
}

function deleteHistory(id: string) {
  history.value = removeHistory(id)
  showToast('已删除该条历史')
}

function clearAllHistory() {
  history.value = clearHistory()
  showToast('已清空历史')
}

function kb(size: number): string {
  return size > 1024 ? `${(size / 1024).toFixed(1)} KB` : `${size} B`
}

async function copy(a: Artifact) {
  try {
    await navigator.clipboard.writeText(a.content)
    showToast(`已复制 ${a.filename}`)
  } catch {
    showToast(`复制失败，请改用「下载」获取 ${a.filename}`)
  }
}

function downloadAll() {
  artifacts.value.forEach((a, i) => {
    setTimeout(() => downloadArtifact(a), i * 150)
  })
  showToast(`正在下载 ${artifacts.value.length} 个文件`)
}
</script>

<template>
  <div class="ds-stack ds-gap-5">
    <div>
      <h2 class="ds-h2">设计工作台 Design Studio</h2>
      <div class="ds-small">
        输入需求描述 → 生成「一份属于该需求的设计系统」（品牌 / 颜色 / 类型 / 间距 / 组件 / 应用示例）→ 预览 / 导出
      </div>
    </div>

    <!-- 输入区 -->
    <div class="ds-surface ds-stack ds-gap-4" style="padding: var(--ds-space-6)">
      <div class="ds-row ds-wrap ds-gap-2">
        <span class="ds-micro">示例：</span>
        <DsButton v-for="(s, i) in SAMPLES" :key="s.label" size="sm" variant="ghost" @click="useSample(i)">
          {{ s.label }}
        </DsButton>
      </div>

      <label class="ds-stack ds-gap-2">
        <span class="ds-small" style="color: var(--ds-fg-2); font-weight: 600">需求描述</span>
        <textarea
          v-model="brief"
          class="ds-studio__input"
          rows="5"
          aria-label="需求描述"
          placeholder="描述你要做的产品：目标用户、核心场景、关键能力…（如「做一个蓝色调的金融风控后台」）"
        />
        <span class="ds-micro" :class="{ 'ds-studio__warn': !canGenerate }">
          {{ brief.trim().length }} 字{{ canGenerate ? '' : '（至少 10 字才能生成）' }}
        </span>
      </label>

      <div class="ds-row ds-wrap ds-gap-4" style="align-items: flex-end">
        <label class="ds-stack ds-gap-2">
          <span class="ds-small" style="color: var(--ds-fg-2); font-weight: 600">行业画像（可选，留空自动识别）</span>
          <select v-model="industry" class="ds-studio__select" aria-label="行业画像">
            <option value="auto">自动识别（按关键词 / 颜色词）</option>
            <option v-for="o in INDUSTRY_OPTIONS" :key="o.key" :value="o.key">{{ o.label }}</option>
          </select>
        </label>
        <DsButton icon="palette" :disabled="!canGenerate" @click="generate">生成设计系统</DsButton>
      </div>

      <p v-if="error" class="ds-studio__error" role="alert">{{ error }}</p>
    </div>

    <!-- 历史 -->
    <div v-if="history.length" class="ds-surface ds-stack ds-gap-3" style="padding: var(--ds-space-5)">
      <div class="ds-row" style="justify-content: space-between">
        <span class="ds-h4">设计历史（本地保存 {{ history.length }} 条）</span>
        <DsButton size="sm" variant="ghost" @click="clearAllHistory">清空</DsButton>
      </div>
      <div class="ds-stack" style="gap: 0">
        <div v-for="h in history" :key="h.id" class="ds-hist">
          <div class="ds-stack" style="gap: 2px; flex: 1; min-width: 200px">
            <span class="ds-body" style="font-weight: 600">{{ h.title }}</span>
            <span class="ds-micro">{{ INDUSTRY_LABEL[h.industry] ?? h.industry }} · {{ h.savedAt.replace('T', ' ').slice(0, 19) }}</span>
          </div>
          <div class="ds-row ds-gap-2">
            <DsButton size="sm" variant="secondary" @click="loadFromHistory(h)">载入</DsButton>
            <DsButton size="sm" variant="ghost" @click="deleteHistory(h.id)">删除</DsButton>
          </div>
        </div>
      </div>
    </div>

    <!-- 结果区 -->
    <template v-if="design">
      <div class="ds-surface ds-row ds-wrap ds-gap-4" style="padding: var(--ds-space-5); justify-content: space-between">
        <div class="ds-stack" style="gap: 2px">
          <div class="ds-h3">{{ design.meta.name }}</div>
          <div class="ds-small">
            {{ INDUSTRY_LABEL[design.meta.seed.industry] ?? design.meta.seed.industry }} ·
            色相 {{ design.meta.seed.hue }}° / 辅助 {{ design.meta.seed.accentHue }}° ·
            生成于 {{ design.meta.generatedAt.replace('T', ' ').slice(0, 19) }}
            <span v-if="restored"> · 已从本地恢复</span>
          </div>
        </div>
        <div class="ds-row ds-gap-2">
          <DsButton v-if="htmlArtifact" variant="secondary" icon="link" @click="openPreviewInNewTab(htmlArtifact)">
            独立预览
          </DsButton>
          <DsButton icon="box" @click="downloadAll">全部下载</DsButton>
        </div>
      </div>

      <nav class="ds-tabs" role="tablist" aria-label="设计系统结果分区">
        <button
          v-for="t in tabs"
          :key="t.key"
          class="ds-tab"
          :class="{ 'ds-tab--active': activeTab === t.key }"
          role="tab"
          :aria-selected="activeTab === t.key"
          @click="activeTab = t.key"
        >
          {{ t.label }}
        </button>
      </nav>

      <div role="tabpanel" :aria-label="tabs.find((t) => t.key === activeTab)?.label ?? ''">
        <!-- 概览 -->
        <div v-if="activeTab === 'overview'" class="ds-stack ds-gap-5">
          <div class="ds-surface ds-stack ds-gap-3" style="padding: var(--ds-space-5)">
            <div class="ds-h4">品牌</div>
            <div class="ds-row ds-wrap ds-gap-6">
              <div class="ds-stack ds-gap-2" style="flex: 1; min-width: 240px">
                <div class="ds-micro">Logo 概念</div>
                <div class="ds-small">{{ design.brand.logoConcept }}</div>
              </div>
              <div class="ds-stack ds-gap-2" style="flex: 1; min-width: 240px">
                <div class="ds-micro">品牌渐变</div>
                <div class="ds-small">{{ design.brand.gradient }}</div>
              </div>
              <div class="ds-stack ds-gap-2" style="flex: 1; min-width: 240px">
                <div class="ds-micro">图标系统 / 母题</div>
                <div class="ds-small">{{ design.brand.iconSystem }}</div>
                <div class="ds-small" style="color: var(--ds-fg-3)">{{ design.brand.motif }}</div>
              </div>
            </div>
          </div>

          <div class="ds-surface ds-stack ds-gap-3" style="padding: var(--ds-space-5)">
            <div class="ds-h4">生成种子（可复现）</div>
            <div class="ds-row ds-wrap ds-gap-5">
              <div class="ds-stack ds-gap-1"><div class="ds-micro">行业</div><div class="ds-body">{{ INDUSTRY_LABEL[design.meta.seed.industry] ?? design.meta.seed.industry }}</div></div>
              <div class="ds-stack ds-gap-1"><div class="ds-micro">品牌色相</div><div class="ds-body">{{ design.meta.seed.hue }}°</div></div>
              <div class="ds-stack ds-gap-1"><div class="ds-micro">辅助色相</div><div class="ds-body">{{ design.meta.seed.accentHue }}°</div></div>
              <div class="ds-stack ds-gap-1"><div class="ds-micro">字体搭配</div><div class="ds-body">{{ design.meta.seed.fontKey }}</div></div>
            </div>
            <div class="ds-micro" style="margin-top: var(--ds-space-3); color: var(--ds-fg-3)">需求原文</div>
            <div class="ds-studio__quote">{{ design.meta.brief }}</div>
          </div>
        </div>

        <!-- 品牌与颜色 -->
        <div v-else-if="activeTab === 'brand'" class="ds-stack ds-gap-4">
          <div v-for="(map, group) in design.tokens.color" :key="group" class="ds-surface ds-stack ds-gap-3" style="padding: var(--ds-space-5)">
            <div class="ds-h4">{{ group }}</div>
            <div class="ds-row ds-wrap ds-gap-3">
              <div v-for="(v, k) in map" :key="k" class="ds-sw">
                <span class="ds-sw__chip" :style="{ background: v }" />
                <span class="ds-sw__label">{{ k }}</span>
                <span class="ds-sw__val">{{ v }}</span>
              </div>
            </div>
          </div>
        </div>

        <!-- 类型与间距 -->
        <div v-else-if="activeTab === 'type'" class="ds-stack ds-gap-4">
          <div class="ds-surface ds-stack ds-gap-3" style="padding: var(--ds-space-5)">
            <div class="ds-h4">字体</div>
            <div class="ds-small">正文：{{ design.tokens.typography.fontSans }}</div>
            <div class="ds-small">等宽：{{ design.tokens.typography.fontMono }}</div>
          </div>
          <div class="ds-surface ds-stack ds-gap-3" style="padding: var(--ds-space-5)">
            <div class="ds-h4">字阶</div>
            <div class="ds-stack ds-gap-2">
              <div v-for="(v, k) in design.tokens.typography.scale" :key="k" class="ds-type-row">
                <span class="ds-micro" style="min-width: 64px">{{ k }}</span>
                <span :style="{ fontSize: v, fontWeight: k.startsWith('h') || k === 'display' ? 'var(--ds-fw-semibold)' : 'var(--ds-fw-regular)' }">{{ v }} 中文 Agile 设计系统</span>
              </div>
            </div>
          </div>
          <div class="ds-surface ds-stack ds-gap-3" style="padding: var(--ds-space-5)">
            <div class="ds-h4">间距与质感</div>
            <div class="ds-small">间距：{{ Object.entries(design.tokens.spacing).map(([k, v]) => `${k}=${v}`).join(' · ') }}</div>
            <div class="ds-small">圆角：{{ Object.entries(design.tokens.radius).map(([k, v]) => `${k}=${v}`).join(' · ') }}</div>
            <div class="ds-small">动效：{{ Object.entries(design.tokens.motion.duration).map(([k, v]) => `${k}=${v}`).join(' · ') }}</div>
          </div>
        </div>

        <!-- 组件 -->
        <div v-else-if="activeTab === 'components'" class="ds-surface ds-stack ds-gap-3" style="padding: var(--ds-space-5)">
          <div class="ds-h4">组件清单（共 {{ design.components.length }} 个）</div>
          <div class="ds-stack" style="gap: 0">
            <div v-for="c in design.components" :key="c.name" class="ds-ia-row">
              <span class="ds-h4" style="font-size: var(--ds-fs-body); min-width: 140px">{{ c.name }}</span>
              <span class="ds-body" style="flex: 1">{{ c.purpose }}</span>
              <span class="ds-small" style="flex: 1; color: var(--ds-fg-3)">{{ c.variants ?? '—' }}</span>
            </div>
          </div>
        </div>

        <!-- 应用示例 -->
        <div v-else-if="activeTab === 'applications'" class="ds-stack ds-gap-4">
          <div v-for="a in design.applications" :key="a.name" class="ds-surface ds-stack ds-gap-3" style="padding: var(--ds-space-5)">
            <div class="ds-h4">{{ a.name }}</div>
            <div class="ds-small">{{ a.desc }}</div>
            <div class="ds-row ds-wrap ds-gap-2">
              <span v-for="s in a.sections" :key="s" class="ds-chip">{{ s }}</span>
            </div>
          </div>
        </div>

        <!-- 实时预览（内联换肤） -->
        <div v-else-if="activeTab === 'live'" class="ds-stack ds-gap-4">
          <div class="ds-small">以下为按本设计系统 token 实时换肤的预览（与导出的 preview.html 观感一致，可直接看到「你的」品牌色）。</div>
          <div class="ds-live" :style="liveStyle">
            <div class="ds-live__hero">
              <div class="ds-live__h">{{ design.meta.name }}</div>
              <div class="ds-live__p">{{ design.brand.logoConcept }}</div>
              <div class="ds-live__btns">
                <span class="ds-live__btn ds-live__btn--primary">Primary</span>
                <span class="ds-live__btn ds-live__btn--secondary">Secondary</span>
                <span class="ds-live__btn ds-live__btn--ghost">Ghost</span>
              </div>
            </div>
            <div class="ds-live__grid">
              <div v-for="(v, k) in design.tokens.color.brand" :key="k" class="ds-live__sw" :style="{ background: v }">
                <span>{{ k }}</span>
              </div>
            </div>
            <div class="ds-live__card">
              <div class="ds-live__card-title">指标卡片示例</div>
              <div class="ds-live__metric">1,284 <span class="ds-live__unit">req/s</span></div>
              <div class="ds-live__chips">
                <span v-for="c in design.components.slice(0, 5)" :key="c.name" class="ds-live__chip">{{ c.name }}</span>
              </div>
            </div>
          </div>
        </div>

        <!-- 产出文件 -->
        <div v-else-if="activeTab === 'artifacts'" class="ds-stack ds-gap-3">
          <div class="ds-small">共 {{ artifacts.length }} 个文件，全部在浏览器本地生成，不依赖后端。</div>
          <div v-for="a in artifacts" :key="a.filename" class="ds-art">
            <div class="ds-stack" style="gap: 2px; flex: 1; min-width: 200px">
              <div class="ds-mono" style="font-weight: 600">{{ a.filename }}</div>
              <div class="ds-small">{{ a.label }} · {{ kb(a.content.length) }}</div>
            </div>
            <div class="ds-row ds-gap-2">
              <DsButton size="sm" variant="ghost" @click="copy(a)">复制</DsButton>
              <DsButton v-if="a.filename === 'preview.html'" size="sm" variant="secondary" @click="openPreviewInNewTab(a)">
                预览
              </DsButton>
              <DsButton size="sm" @click="downloadArtifact(a)">下载</DsButton>
            </div>
          </div>
        </div>
      </div>
    </template>

    <!-- 空态 -->
    <div v-else class="ds-surface ds-stack ds-gap-3" style="padding: var(--ds-space-7); align-items: center">
      <Icon name="palette" :size="28" />
      <div class="ds-small">填写需求描述（≥10 字）后点击「生成设计系统」，即可获得一套完整设计系统、实时预览与可导出文件。</div>
    </div>

    <!-- 操作反馈（aria-live 播报） -->
    <div class="ds-studio__toast-host" aria-live="polite" aria-atomic="true">
      <div v-if="toast" class="ds-studio__toast">{{ toast }}</div>
    </div>
  </div>
</template>

<style scoped>
.ds-studio__input {
  width: 100%;
  border: 1px solid var(--ds-border-2);
  border-radius: var(--ds-radius-md);
  padding: var(--ds-space-3);
  font-family: var(--ds-font-sans);
  font-size: var(--ds-fs-body);
  color: var(--ds-fg-1);
  background: var(--ds-surface-1);
  resize: vertical;
  outline: none;
  transition:
    border-color var(--ds-dur-fast) var(--ds-ease-out-expo),
    box-shadow var(--ds-dur-fast) var(--ds-ease-out-expo);
}
.ds-studio__input:focus {
  border-color: var(--ds-color-primary);
  box-shadow: 0 0 0 3px var(--ds-brand-100);
}
.ds-studio__select {
  border: 1px solid var(--ds-border-2);
  border-radius: var(--ds-radius-md);
  padding: 8px var(--ds-space-3);
  font-family: var(--ds-font-sans);
  font-size: var(--ds-fs-body);
  background: var(--ds-surface-1);
  color: var(--ds-fg-1);
  outline: none;
}
.ds-studio__select:focus {
  border-color: var(--ds-color-primary);
  box-shadow: 0 0 0 3px var(--ds-brand-100);
}
.ds-studio__warn {
  color: var(--ds-warning);
}
.ds-studio__error {
  margin: 0;
  padding: var(--ds-space-3);
  border: 1px solid var(--ds-danger);
  border-radius: var(--ds-radius-md);
  background: var(--ds-danger-soft);
  color: #991b1b;
  font-size: var(--ds-fs-small);
}
.ds-studio__quote {
  border-left: 3px solid var(--ds-brand-200);
  padding: 4px 0 4px var(--ds-space-3);
  color: var(--ds-fg-2);
  font-size: var(--ds-fs-small);
  white-space: pre-wrap;
}
.ds-tabs {
  display: flex;
  flex-wrap: wrap;
  gap: var(--ds-space-1);
  background: var(--ds-surface-2);
  padding: 4px;
  border-radius: var(--ds-radius-pill);
  border: 1px solid var(--ds-border-1);
}
.ds-tab {
  border: none;
  background: transparent;
  color: var(--ds-fg-2);
  font-family: var(--ds-font-sans);
  font-size: var(--ds-fs-small);
  font-weight: 500;
  padding: 8px var(--ds-space-4);
  border-radius: var(--ds-radius-pill);
  cursor: pointer;
  transition: all var(--ds-dur-fast) var(--ds-ease-out-expo);
}
.ds-tab:hover {
  color: var(--ds-fg-1);
}
.ds-tab:focus-visible {
  outline: 2px solid var(--ds-color-accent);
  outline-offset: 2px;
}
.ds-tab--active {
  background: var(--ds-surface-1);
  color: var(--ds-color-primary);
  box-shadow: var(--ds-shadow-sm);
}
.ds-ia-row {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: var(--ds-space-3);
  padding: var(--ds-space-3) 0;
  border-bottom: 1px solid var(--ds-border-1);
}
.ds-ia-row:last-child {
  border-bottom: none;
}
.ds-chip {
  font-size: var(--ds-fs-small);
  padding: 4px 10px;
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-md);
  background: var(--ds-surface-2);
  color: var(--ds-fg-2);
}
.ds-sw {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
  width: 72px;
}
.ds-sw__chip {
  width: 56px;
  height: 40px;
  border-radius: var(--ds-radius-md);
  border: 1px solid var(--ds-border-1);
}
.ds-sw__label {
  font-size: var(--ds-fs-micro);
  color: var(--ds-fg-2);
  font-weight: 600;
}
.ds-sw__val {
  font-size: 10px;
  color: var(--ds-fg-4);
}
.ds-type-row {
  display: flex;
  align-items: baseline;
  gap: var(--ds-space-3);
  padding: 4px 0;
}
.ds-art,
.ds-hist {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--ds-space-4);
  justify-content: space-between;
  background: var(--ds-surface-1);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-lg);
  padding: var(--ds-space-4);
}
.ds-hist {
  border-radius: 0;
  border-left: none;
  border-right: none;
  border-top: none;
}
.ds-hist:last-child {
  border-bottom: none;
}
/* 实时预览容器：变量由 :style 注入，后代整体换肤 */
.ds-live {
  border-radius: var(--ds-radius-2xl);
  padding: var(--ds-space-6);
  background: var(--ds-surface-2);
  border: 1px solid var(--ds-border-1);
  font-family: var(--ds-font-sans);
}
.ds-live__hero {
  border-radius: var(--ds-radius-xl);
  padding: var(--ds-space-6);
  background: var(--ds-gradient-brand);
  color: #fff;
}
.ds-live__h {
  font-size: var(--ds-fs-h2);
  font-weight: var(--ds-fw-bold);
  margin-bottom: var(--ds-space-2);
}
.ds-live__p {
  font-size: var(--ds-fs-small);
  opacity: 0.92;
}
.ds-live__btns {
  display: flex;
  gap: var(--ds-space-2);
  margin-top: var(--ds-space-4);
}
.ds-live__btn {
  display: inline-flex;
  align-items: center;
  border: 1px solid transparent;
  border-radius: var(--ds-radius-md);
  padding: 8px 16px;
  font-weight: var(--ds-fw-semibold);
  font-size: var(--ds-fs-small);
}
.ds-live__btn--primary {
  background: var(--ds-color-primary);
  color: #fff;
}
.ds-live__btn--secondary {
  background: var(--ds-surface-1);
  color: var(--ds-fg-1);
}
.ds-live__btn--ghost {
  background: transparent;
  color: var(--ds-color-primary);
}
.ds-live__grid {
  display: flex;
  flex-wrap: wrap;
  gap: var(--ds-space-2);
  margin-top: var(--ds-space-4);
}
.ds-live__sw {
  width: 56px;
  height: 44px;
  border-radius: var(--ds-radius-md);
  display: flex;
  align-items: flex-end;
  justify-content: center;
  font-size: 10px;
  color: #fff;
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.35);
  padding-bottom: 4px;
}
.ds-live__card {
  margin-top: var(--ds-space-4);
  background: var(--ds-surface-1);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-lg);
  padding: var(--ds-space-5);
  box-shadow: var(--ds-shadow-sm);
}
.ds-live__card-title {
  font-size: var(--ds-fs-small);
  color: var(--ds-fg-3);
}
.ds-live__metric {
  font-size: var(--ds-fs-h2);
  font-weight: var(--ds-fw-bold);
  font-variant-numeric: tabular-nums;
}
.ds-live__unit {
  font-size: var(--ds-fs-small);
  color: var(--ds-fg-3);
}
.ds-live__chips {
  display: flex;
  flex-wrap: wrap;
  gap: var(--ds-space-2);
  margin-top: var(--ds-space-3);
}
.ds-live__chip {
  font-size: var(--ds-fs-small);
  padding: 4px 10px;
  border: 1px solid var(--ds-border-2);
  border-radius: var(--ds-radius-md);
  background: var(--ds-surface-2);
  color: var(--ds-fg-2);
}
.ds-studio__toast-host {
  position: sticky;
  bottom: var(--ds-space-4);
  display: flex;
  justify-content: center;
  pointer-events: none;
}
.ds-studio__toast {
  background: var(--ds-fg-1);
  color: #fff;
  font-size: var(--ds-fs-small);
  padding: 8px var(--ds-space-4);
  border-radius: var(--ds-radius-pill);
  box-shadow: var(--ds-shadow-md);
}
</style>
