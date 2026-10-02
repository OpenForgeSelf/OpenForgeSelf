<script setup lang="ts">
/**
 * 官网 · 落地页模特（切片 B，03-plan §M）。
 *
 * 为什么特意选"营销页"：它和后台/工作台的信息密度、字号层级截然不同——Hero 用最大档字号
 * （`--ds-size-display`）、按钮成对出现（主/次）、卡片数量多且需要"高亮其中一张"。
 * 这页用来确认设计系统在**大字号 + 大留白 + 强调色描边**的组合下是否仍然协调；
 * 顶栏横向导航 `.mq-nav--row` 则验证同一套 nav 令牌在横排时的表现。
 * 结构只摆 `mq-*` 类与 `data-mq-wear`，全部观感交给 mannequin.css 的 `--ds-*` 变量。
 */
</script>

<template>
  <div class="mq-page mq-landing-home" data-mq-page="landing-home">
    <header class="mq-topbar">
      <div class="mq-topbar__brand">
        <span class="mq-topbar__mark">铸</span>
        <span>铸己匣</span>
      </div>
      <nav class="mq-nav mq-nav--row" data-mq-wear="nav" aria-label="站点导航">
        <span class="mq-nav__item">产品</span>
        <span class="mq-nav__item">定价</span>
        <span class="mq-nav__item">文档</span>
      </nav>
      <button type="button" class="mq-btn mq-btn--primary" data-mq-wear="button">免费开始</button>
    </header>

    <section class="mq-hero">
      <span class="mq-badge mq-badge--brand" data-mq-wear="badge">v3 已发布</span>
      <h1 class="mq-hero__title">把设计系统交给整个团队</h1>
      <p class="mq-hero__sub">一套令牌，四种模式：开始、展厅、工作台、交付与接入。</p>
      <div class="mq-row">
        <button type="button" class="mq-btn mq-btn--primary" data-mq-wear="button">立即创建</button>
        <button type="button" class="mq-btn" data-mq-wear="button">看演示</button>
      </div>
    </section>

    <div class="mq-grid mq-grid--cards">
      <article class="mq-card" data-mq-wear="card">
        <div class="mq-card__head">
          <h3 class="mq-card__title">一致</h3>
          <span class="mq-badge mq-badge--info" data-mq-wear="badge">令牌</span>
        </div>
        <p class="mq-small mq-muted">同一份令牌同时驱动页面与导出，画布所见即交付所得。</p>
      </article>
      <article class="mq-card" data-mq-wear="card">
        <div class="mq-card__head">
          <h3 class="mq-card__title">可审计</h3>
          <span class="mq-badge mq-badge--success" data-mq-wear="badge">检查</span>
        </div>
        <p class="mq-small mq-muted">每次生成都过一遍规则审查，问题在交付前就能看见。</p>
      </article>
      <article class="mq-card" data-mq-wear="card">
        <div class="mq-card__head">
          <h3 class="mq-card__title">可交付</h3>
          <span class="mq-badge mq-badge--brand" data-mq-wear="badge">导出</span>
        </div>
        <p class="mq-small mq-muted">CSS、Tailwind、设计令牌多格式导出，接入现有工程。</p>
      </article>
    </div>

    <div class="mq-grid mq-grid--cards">
      <article class="mq-card mq-pricing" data-mq-wear="card">
        <h3 class="mq-card__title">个人</h3>
        <span class="mq-price">免费</span>
        <button type="button" class="mq-btn" data-mq-wear="button">开始使用</button>
      </article>
      <article class="mq-card mq-pricing mq-pricing--featured" data-mq-wear="card">
        <div class="mq-card__head">
          <h3 class="mq-card__title">团队</h3>
          <span class="mq-badge mq-badge--brand" data-mq-wear="badge">推荐</span>
        </div>
        <span class="mq-price">¥ 199</span>
        <button type="button" class="mq-btn mq-btn--primary" data-mq-wear="button">选择团队版</button>
      </article>
      <article class="mq-card mq-pricing" data-mq-wear="card">
        <h3 class="mq-card__title">企业</h3>
        <span class="mq-price">定制</span>
        <button type="button" class="mq-btn" data-mq-wear="button">联系我们</button>
      </article>
    </div>

    <section class="mq-card" data-mq-wear="card">
      <div class="mq-card__head">
        <h3 class="mq-card__title">订阅更新</h3>
        <span class="mq-caption">每月一封，不发广告</span>
      </div>
      <div class="mq-row">
        <input class="mq-input" type="email" placeholder="you@company.com" aria-label="邮箱" data-mq-wear="input" />
        <button type="button" class="mq-btn mq-btn--primary" data-mq-wear="button">订阅</button>
      </div>
    </section>

    <footer class="mq-footer">
      <span>© 铸己匣 · 通用占位落地页</span>
      <span class="mq-badge mq-badge--neutral" data-mq-wear="badge">v3.0.0</span>
    </footer>
  </div>
</template>