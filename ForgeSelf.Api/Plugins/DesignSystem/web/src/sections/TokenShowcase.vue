<script setup lang="ts">
/**
 * 设计令牌展示（Token Showcase）。
 * 五类 token：类型 / 颜色 / 间距 / 组件(圆角·阴影·描边·动效) / 品牌(在组件与官网区块体现)。
 */
const purple = [50, 100, 200, 300, 400, 500, 600, 700, 800, 900]
const cyan = [50, 100, 200, 300, 400, 500, 600, 700, 800, 900]
const gray = [50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 950]

const semantic = [
  { name: 'success', var: '--ds-success' },
  { name: 'warning', var: '--ds-warning' },
  { name: 'danger', var: '--ds-danger' },
  { name: 'info', var: '--ds-info' },
]
const surfaces = [
  { name: 'surface-bg', var: '--ds-surface-bg' },
  { name: 'surface-1', var: '--ds-surface-1' },
  { name: 'surface-2', var: '--ds-surface-2' },
  { name: 'surface-3', var: '--ds-surface-3' },
]
const texts = [
  { name: 'fg-1', var: '--ds-fg-1' },
  { name: 'fg-2', var: '--ds-fg-2' },
  { name: 'fg-3', var: '--ds-fg-3' },
  { name: 'fg-4', var: '--ds-fg-4' },
  { name: 'link', var: '--ds-link' },
]

const spacing = [1, 2, 3, 4, 5, 6, 7, 8, 9]
const radii = [
  { name: 'xs', var: '--ds-radius-xs' },
  { name: 'sm', var: '--ds-radius-sm' },
  { name: 'md', var: '--ds-radius-md' },
  { name: 'lg', var: '--ds-radius-lg' },
  { name: 'xl', var: '--ds-radius-xl' },
  { name: '2xl', var: '--ds-radius-2xl' },
  { name: 'pill', var: '--ds-radius-pill' },
]
const elevations = [
  { name: 'shadow-sm', var: '--ds-shadow-sm' },
  { name: 'shadow-md', var: '--ds-shadow-md' },
  { name: 'shadow-lg', var: '--ds-shadow-lg' },
]
const borders = [
  { name: 'border-1 · 8%', var: '--ds-border-1' },
  { name: 'border-2 · 14%', var: '--ds-border-2' },
  { name: 'border-3 · 24%', var: '--ds-border-3' },
]
const motion = [
  { name: 'ease-out-expo', var: '--ds-ease-out-expo' },
  { name: 'ease-standard', var: '--ds-ease-standard' },
  { name: 'dur-fast · 150ms', var: '--ds-dur-fast' },
  { name: 'dur-base · 250ms', var: '--ds-dur-base' },
  { name: 'dur-slow · 400ms', var: '--ds-dur-slow' },
]

function cssVar(v: string): string {
  return `var(${v})`
}
</script>

<template>
  <div class="ds-stack ds-gap-6">
    <!-- 类型 Typography -->
    <section>
      <div class="ds-section-title">
        <span class="ds-eyebrow">01</span>
        <h2 class="ds-h2">类型 Typography</h2>
      </div>
      <div class="ds-surface ds-stack ds-gap-4" style="padding: var(--ds-space-6)">
        <div class="ds-display">Display · 64px / 700</div>
        <div class="ds-h1">H1 · 标题 40px / 700</div>
        <div class="ds-h2">H2 · 标题 28px / 600</div>
        <div class="ds-h3">H3 · 标题 20px / 600</div>
        <div class="ds-h4">H4 · 标题 16px / 600</div>
        <div class="ds-body-lg">Body Large · 正文 18px / 400 — 用于导语与卡片主句。</div>
        <div class="ds-body">Body · 正文 15px / 400 — 默认正文内容，承载绝大多数界面文字。</div>
        <div class="ds-small">Small · 辅助 13px / 400 — 次要说明与元信息。</div>
        <div class="ds-micro">Micro · 12px / 500 — 标签、单位、大写 eyebrow。</div>
        <div class="ds-mono">JetBrains Mono · 代码 / 日志 · const token = 'design-system';</div>
        <div class="ds-num" style="font-size: var(--ds-fs-h3)">指标数字 · tabular-nums → 1,284,032 / 98.72%</div>
      </div>
    </section>

    <!-- 颜色 Colors -->
    <section>
      <div class="ds-section-title">
        <span class="ds-eyebrow">02</span>
        <h2 class="ds-h2">颜色 Colors</h2>
      </div>

      <h3 class="ds-h4" style="margin-bottom: var(--ds-space-3)">品牌色 Brand · 50–900</h3>
      <div class="ds-swatch-grid" style="margin-bottom: var(--ds-space-5)">
        <div v-for="n in purple" :key="'p' + n" class="ds-swatch">
          <div class="ds-swatch__chip" :style="{ background: cssVar('--ds-brand-' + n) }" />
          <div class="ds-micro">{{ n }}</div>
          <div class="ds-micro" style="color: var(--ds-fg-4)">{{ cssVar('--ds-brand-' + n) }}</div>
        </div>
      </div>

      <h3 class="ds-h4" style="margin-bottom: var(--ds-space-3)">Signal Cyan · 辅助青色 50–900</h3>
      <div class="ds-swatch-grid" style="margin-bottom: var(--ds-space-5)">
        <div v-for="n in cyan" :key="'c' + n" class="ds-swatch">
          <div class="ds-swatch__chip" :style="{ background: cssVar('--ds-accent-' + n) }" />
          <div class="ds-micro">{{ n }}</div>
        </div>
      </div>

      <h3 class="ds-h4" style="margin-bottom: var(--ds-space-3)">Cool Neutrals · gray 50–950</h3>
      <div class="ds-swatch-grid" style="margin-bottom: var(--ds-space-5)">
        <div v-for="n in gray" :key="'g' + n" class="ds-swatch">
          <div class="ds-swatch__chip" :style="{ background: cssVar('--ds-gray-' + n) }" />
          <div class="ds-micro">{{ n }}</div>
        </div>
      </div>

      <div class="ds-row ds-wrap ds-gap-5" style="margin-bottom: var(--ds-space-5)">
        <div>
          <h3 class="ds-h4" style="margin-bottom: var(--ds-space-3)">Semantic</h3>
          <div class="ds-row ds-wrap ds-gap-3">
            <div v-for="s in semantic" :key="s.name" class="ds-swatch">
              <div class="ds-swatch__chip" :style="{ background: cssVar(s.var) }" />
              <div class="ds-micro">{{ s.name }}</div>
            </div>
          </div>
        </div>
        <div>
          <h3 class="ds-h4" style="margin-bottom: var(--ds-space-3)">Surfaces</h3>
          <div class="ds-row ds-wrap ds-gap-3">
            <div v-for="s in surfaces" :key="s.name" class="ds-swatch">
              <div class="ds-swatch__chip" :style="{ background: cssVar(s.var), border: '1px solid var(--ds-border-2)' }" />
              <div class="ds-micro">{{ s.name }}</div>
            </div>
          </div>
        </div>
        <div>
          <h3 class="ds-h4" style="margin-bottom: var(--ds-space-3)">Text</h3>
          <div class="ds-row ds-wrap ds-gap-3">
            <div v-for="s in texts" :key="s.name" class="ds-swatch">
              <div class="ds-swatch__chip" :style="{ background: cssVar(s.var), border: '1px solid var(--ds-border-2)' }" />
              <div class="ds-micro">{{ s.name }}</div>
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- 间距 Spacing -->
    <section>
      <div class="ds-section-title">
        <span class="ds-eyebrow">03</span>
        <h2 class="ds-h2">间距 Spacing · 8pt 基准</h2>
      </div>
      <div class="ds-surface ds-row ds-wrap ds-gap-4" style="padding: var(--ds-space-6); align-items: flex-end">
        <div v-for="n in spacing" :key="'s' + n" class="ds-stack" style="align-items: center; gap: var(--ds-space-2)">
          <div class="ds-spacer" :style="{ height: cssVar('--ds-space-' + n) }" />
          <div class="ds-micro">{{ n }}</div>
          <div class="ds-micro" style="color: var(--ds-fg-4)">{{ cssVar('--ds-space-' + n) }}</div>
        </div>
      </div>
    </section>

    <!-- 圆角 / 阴影 / 描边 / 动效 -->
    <section>
      <div class="ds-section-title">
        <span class="ds-eyebrow">04</span>
        <h2 class="ds-h2">圆角 · 阴影 · 描边 · 动效</h2>
      </div>
      <div class="ds-row ds-wrap ds-gap-6">
        <div class="ds-stack ds-gap-3" style="flex: 1; min-width: 260px">
          <h3 class="ds-h4">Radius</h3>
          <div class="ds-row ds-wrap ds-gap-3">
            <div v-for="r in radii" :key="r.name" class="ds-stack" style="align-items: center; gap: 6px">
              <div class="ds-radius-demo" :style="{ borderRadius: cssVar(r.var) }" />
              <div class="ds-micro">{{ r.name }}</div>
            </div>
          </div>
        </div>
        <div class="ds-stack ds-gap-3" style="flex: 1; min-width: 260px">
          <h3 class="ds-h4">Elevation（亮色）</h3>
          <div class="ds-row ds-wrap ds-gap-4">
            <div v-for="e in elevations" :key="e.name" class="ds-elev-demo" :style="{ boxShadow: cssVar(e.var) }">
              <div class="ds-micro">{{ e.name }}</div>
            </div>
          </div>
        </div>
        <div class="ds-stack ds-gap-3" style="flex: 1; min-width: 260px">
          <h3 class="ds-h4">Borders · 8% / 14% / 24%</h3>
          <div class="ds-stack ds-gap-3">
            <div v-for="b in borders" :key="b.name" class="ds-border-demo" :style="{ borderColor: cssVar(b.var) }">
              <span class="ds-micro">{{ b.name }}</span>
            </div>
          </div>
        </div>
        <div class="ds-stack ds-gap-3" style="flex: 1; min-width: 260px">
          <h3 class="ds-h4">Motion Tokens</h3>
          <div class="ds-stack ds-gap-2">
            <div v-for="m in motion" :key="m.name" class="ds-surface-2 ds-micro" style="padding: 8px 12px">
              {{ m.name }} = {{ cssVar(m.var) }}
            </div>
          </div>
        </div>
      </div>
    </section>
  </div>
</template>

<style scoped>
.ds-swatch {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
}
.ds-swatch__chip {
  width: 100%;
  height: 56px;
  border-radius: var(--ds-radius-md);
}
.ds-spacer {
  width: 28px;
  background: var(--ds-color-primary);
  border-radius: var(--ds-radius-xs);
}
.ds-radius-demo {
  width: 64px;
  height: 64px;
  background: var(--ds-surface-1);
  border: 1px solid var(--ds-border-2);
}
.ds-elev-demo {
  width: 88px;
  height: 64px;
  background: var(--ds-surface-1);
  border-radius: var(--ds-radius-md);
  display: flex;
  align-items: center;
  justify-content: center;
}
.ds-border-demo {
  padding: var(--ds-space-4);
  background: var(--ds-surface-1);
  border-width: 1px;
  border-style: solid;
  border-radius: var(--ds-radius-md);
}
</style>
