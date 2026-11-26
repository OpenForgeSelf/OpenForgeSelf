<script setup lang="ts">
/**
 * 运行实况：要你处理、要续跑、全量在跑、知道即可、待触发。
 * 15 秒拉取。决定走各来源现成接口，卡片留下一句结果，下一次刷新再离开。
 */
import { computed, onMounted, ref } from 'vue'
import PageHead from '@/components/PageHead.vue'
import EmptyState from '@/components/EmptyState.vue'
import { useOpenPage } from '@/composables/useOpenPage'
import type { BoardCard } from '@/services/runtimeBoard'
import { attentionCount } from '@/services/runtimeBoard'
import { useRuntimeBoardStore } from '@/stores/runtimeBoard'

const board = useRuntimeBoardStore()
const { openPage } = useOpenPage()
const confirmCard = ref<BoardCard | null>(null)

onMounted(() => {
  board.start()
})

const blocking = computed(() => attentionCount(board.cards))
const urgent = computed(() => board.cards.filter((c) => c.lane === 'urgent'))
const waiting = computed(() => board.cards.filter((c) => c.lane === 'wait'))
const resume = computed(() => board.cards.filter((c) => c.lane === 'resume'))
const known = computed(() => board.cards.filter((c) => c.lane === 'known'))
const scheduled = computed(() => board.cards.filter((c) => c.lane === 'scheduled'))

const runningGroups = computed(() => {
  const map = new Map<string, BoardCard[]>()
  for (const card of board.cards) {
    if (card.lane !== 'running') continue
    const name = card.project || '未归属'
    const list = map.get(name) ?? []
    list.push(card)
    map.set(name, list)
  }
  return [...map.entries()]
    .sort((a, b) => (a[0] === '未归属' ? 1 : b[0] === '未归属' ? -1 : a[0].localeCompare(b[0], 'zh')))
    .map(([project, cards]) => ({ project, cards }))
})

const firstUrgentId = computed(() => urgent.value[0]?.id ?? '')
const nothing = computed(() => board.loadedOnce && board.cards.length === 0 && board.notices.length === 0)

function groupsOf(cards: BoardCard[]): Array<{ key: string; title: string; cards: BoardCard[] }> {
  const groups: Array<{ key: string; title: string; cards: BoardCard[] }> = []
  for (const card of cards) {
    const key = card.sessionGroup ? `${card.source}:${card.sessionGroup}` : card.id
    const found = groups.find((g) => g.key === key)
    if (found) found.cards.push(card)
    else groups.push({ key, title: card.sessionGroup ? `${card.source} · ${card.sessionGroup}` : card.source, cards: [card] })
  }
  return groups
}

function openCard(card: BoardCard): void {
  openPage({ path: card.link.path, query: card.link.query }, card.title)
}

function openSecondary(card: BoardCard): void {
  if (!card.secondaryLink) return
  openPage({ path: card.secondaryLink.path, query: card.secondaryLink.query }, card.secondaryLink.label)
}

function onApprove(card: BoardCard): void {
  if (card.destructive) {
    confirmCard.value = card
    return
  }
  void board.decide(card, true)
}

function confirmDestructive(): void {
  const card = confirmCard.value
  confirmCard.value = null
  if (!card) return
  void board.decide(card, true, true)
}
</script>

<template>
  <div class="rs">
    <PageHead eyebrow="Runtime · 运行实况" :title="board.loadedOnce ? `运行实况 · ${blocking}` : '运行实况'">
      <template #actions>
        <span class="rs-hint">只查找已登记项目目录下的进程，不启动、不停止</span>
        <button class="rs-btn" type="button" :disabled="board.scanning" @click="board.scan()">
          {{ board.scanning ? '正在扫描' : '重新扫描' }}
        </button>
      </template>
    </PageHead>

    <p v-if="board.scanNote" class="rs-scan-note">{{ board.scanNote }}</p>

    <p v-if="board.loading && !board.loadedOnce" class="rs-loading">正在读取</p>

    <ul v-if="board.notices.length" class="rs-notices">
      <li v-for="notice in board.notices" :key="notice.source + notice.kind">{{ notice.message }}</li>
    </ul>

    <EmptyState v-if="nothing" title="现在没有要处理的，也没有在跑的" />

    <section v-if="urgent.length || waiting.length" class="rs-band">
      <h2>要你处理 <span>{{ blocking }}</span></h2>
      <div class="rs-scroll">
        <div v-for="group in groupsOf([...urgent, ...waiting])" :key="group.key" class="rs-group">
          <div class="rs-group-title">{{ group.title }}</div>
          <article v-for="card in group.cards" :key="card.id" class="rs-card" :data-lane="card.lane">
            <button class="rs-card-main" type="button" @click="openCard(card)">
              <span class="rs-source">{{ card.source }}</span>
              <span class="rs-title">{{ card.title }}</span>
              <span class="rs-detail">{{ card.detail }}</span>
            </button>
            <p v-if="card.note" class="rs-note">{{ card.note }}</p>
            <p v-if="board.results[card.id]" class="rs-result">{{ board.results[card.id] }}</p>
            <div v-else-if="card.decide" class="rs-actions">
              <button
                class="rs-btn"
                :class="card.id === firstUrgentId ? 'rs-btn-gold' : ''"
                type="button"
                :disabled="board.acting[card.id]"
                @click="onApprove(card)"
              >
                同意
              </button>
              <button class="rs-btn rs-btn-quiet" type="button" :disabled="board.acting[card.id]" @click="board.decide(card, false)">
                拒绝
              </button>
            </div>
          </article>
        </div>
      </div>
    </section>

    <section v-if="resume.length" class="rs-band">
      <h2>要续跑</h2>
      <article v-for="card in resume" :key="card.id" class="rs-card">
        <button class="rs-card-main" type="button" @click="openCard(card)">
          <span class="rs-source">{{ card.source }}</span>
          <span class="rs-title">{{ card.title }}</span>
          <span class="rs-detail">{{ card.detail }}</span>
        </button>
        <p v-if="board.results[card.id]" class="rs-result">{{ board.results[card.id] }}</p>
        <div v-else class="rs-actions">
          <button class="rs-btn" type="button" :disabled="board.acting[card.id]" @click="board.decide(card, true)">继续</button>
        </div>
      </article>
    </section>

    <section v-if="runningGroups.length" class="rs-band">
      <h2>在跑</h2>
      <div class="rs-scroll rs-scroll-tall">
        <div v-for="group in runningGroups" :key="group.project" class="rs-group">
          <div class="rs-group-title">{{ group.project }}</div>
          <article v-for="card in group.cards" :key="card.id" class="rs-card">
            <button class="rs-card-main" type="button" @click="openCard(card)">
              <span class="rs-source">{{ card.source }}</span>
              <span class="rs-title">{{ card.title }}</span>
              <span class="rs-detail">{{ card.detail }}</span>
            </button>
            <button v-if="card.secondaryLink" class="rs-link" type="button" @click="openSecondary(card)">
              {{ card.secondaryLink.label }}
            </button>
          </article>
        </div>
      </div>
    </section>

    <section v-if="board.loadedOnce && known.length" class="rs-band">
      <h2>知道即可</h2>
      <article v-for="card in known" :key="card.id" class="rs-card">
        <button class="rs-card-main" type="button" @click="openCard(card)">
          <span class="rs-source">{{ card.source }}</span>
          <span class="rs-title">{{ card.title }}</span>
          <span class="rs-detail">{{ card.detail }}</span>
        </button>
      </article>
    </section>

    <section v-if="board.loadedOnce && scheduled.length" class="rs-band">
      <h2>待触发</h2>
      <article v-for="card in scheduled" :key="card.id" class="rs-card">
        <button class="rs-card-main" type="button" @click="openCard(card)">
          <span class="rs-source">{{ card.source }}</span>
          <span class="rs-title">{{ card.title }}</span>
          <span class="rs-detail">{{ card.detail }}</span>
        </button>
        <p v-if="board.results[card.id]" class="rs-result">{{ board.results[card.id] }}</p>
        <div v-else class="rs-actions">
          <button class="rs-btn" type="button" :disabled="board.acting[card.id]" @click="board.decide(card, true)">立即运行</button>
        </div>
      </article>
    </section>

    <div v-if="confirmCard" class="rs-mask" @click.self="confirmCard = null">
      <div class="rs-dialog" role="dialog" aria-labelledby="rs-confirm-title">
        <h3 id="rs-confirm-title">这条会破坏数据</h3>
        <p>{{ confirmCard.title }}</p>
        <div class="rs-actions">
          <button class="rs-btn" type="button" @click="confirmCard = null">取消</button>
          <button class="rs-btn rs-btn-danger" type="button" @click="confirmDestructive">批准</button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.rs-hint {
  color: var(--fg-tertiary);
  font-size: 12px;
  margin-right: 8px;
}

.rs-loading,
.rs-scan-note {
  color: var(--fg-tertiary);
  font-size: 13px;
  margin: 0 0 12px;
}

.rs-notices {
  margin: 0 0 12px;
  padding: 8px 12px;
  list-style: none;
  border: 1px solid var(--border-subtle);
  border-radius: var(--el-border-radius-base);
  color: var(--fg-secondary);
  font-size: 13px;
}

.rs-band {
  margin-bottom: 20px;
}

.rs-band h2 {
  margin: 0 0 8px;
  font-size: 14px;
  color: var(--fg-primary);
}

.rs-band h2 span {
  color: var(--gold-500);
}

.rs-scroll {
  max-height: 280px;
  overflow: auto;
}

.rs-scroll-tall {
  max-height: 420px;
}

.rs-group-title {
  font-size: 12px;
  color: var(--fg-tertiary);
  padding: 6px 0;
}

.rs-card {
  border-bottom: 1px solid var(--border-subtle);
  padding: 8px 0;
}

.rs-card-main {
  display: flex;
  gap: 12px;
  align-items: baseline;
  width: 100%;
  border: none;
  background: transparent;
  color: var(--fg-primary);
  text-align: left;
  cursor: pointer;
  font-size: 13px;
}

.rs-card-main:hover {
  background: var(--paper-tint);
}

.rs-source {
  flex: 0 0 auto;
  color: var(--fg-tertiary);
  font-size: 12px;
}

.rs-title {
  flex: 1 1 auto;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-weight: 600;
}

.rs-detail {
  flex: 0 1 40%;
  color: var(--fg-tertiary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.rs-note,
.rs-result {
  margin: 4px 0 0;
  font-size: 12px;
  color: var(--fg-secondary);
}

.rs-actions {
  display: flex;
  gap: 8px;
  margin-top: 8px;
}

.rs-btn,
.rs-link {
  border: 1px solid var(--border-default);
  border-radius: var(--el-border-radius-base);
  background: var(--el-bg-color);
  color: var(--fg-primary);
  font-size: 13px;
  padding: 4px 12px;
  cursor: pointer;
}

.rs-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.rs-btn-gold {
  background: var(--gold-400);
  border-color: var(--gold-400);
  color: var(--ink-950);
  font-weight: 600;
}

.rs-btn-quiet {
  background: transparent;
  border-color: transparent;
  color: var(--cinnabar-400);
}

.rs-btn-danger {
  background: var(--cinnabar-400);
  border-color: var(--cinnabar-400);
  color: var(--el-color-white);
}

.rs-link {
  margin-top: 6px;
}

.rs-mask {
  position: fixed;
  inset: 0;
  background: color-mix(in srgb, var(--fg-primary) 28%, transparent);
  display: flex;
  align-items: center;
  justify-content: center;
}

.rs-dialog {
  width: min(420px, calc(100% - 32px));
  padding: 16px;
  background: var(--el-bg-color);
  border: 1px solid var(--border-subtle);
  border-radius: var(--el-border-radius-base);
  color: var(--fg-primary);
}

.rs-dialog h3 {
  margin: 0 0 8px;
  font-size: 16px;
}

.rs-dialog p {
  margin: 0;
  font-size: 13px;
  word-break: break-all;
}
</style>
