<script setup lang="ts">
/**
 * 移动端 H5 · 首页模特（切片 B，03-plan §M）。
 *
 * 为什么单独一页手机界面：窄屏下"同一套令牌"最容易露馅——间距、字号、控件高度在 390 宽
 * 的框里会挤到一起，圆角与描边也更显眼。这页用来确认设计系统在**窄屏 + 列表卡 + 底部标签栏**
 * 的组合下仍站得住；底部标签栏（nav 四项 + 当前项顶部指示条）是本页唯一的导航形态。
 * 结构只摆 `mq-*` 类与 `data-mq-wear`，全部观感交给 mannequin.css 的 `--ds-*` 变量。
 */
</script>

<template>
  <div class="mq-page mq-mobile-home" data-mq-page="mobile-home">
    <header class="mq-topbar">
      <div class="mq-topbar__brand">
        <span class="mq-topbar__mark">铸</span>
        <span>移动端</span>
      </div>
      <span class="mq-badge mq-badge--success" data-mq-wear="badge">在线</span>
    </header>

    <input class="mq-input" type="search" placeholder="搜索订单、客户…" aria-label="搜索" data-mq-wear="input" />

    <div class="mq-stack">
      <article class="mq-card mq-listcard" data-mq-wear="card">
        <div class="mq-stack">
          <span class="mq-card__title">订单 1042</span>
          <span class="mq-caption">李青 · 320 元</span>
        </div>
        <span class="mq-spacer"></span>
        <span class="mq-badge mq-badge--success" data-mq-wear="badge">已完成</span>
        <button type="button" class="mq-btn mq-btn--sm" data-mq-wear="button">详情</button>
      </article>
      <article class="mq-card mq-listcard" data-mq-wear="card">
        <div class="mq-stack">
          <span class="mq-card__title">订单 1041</span>
          <span class="mq-caption">周然 · 1,050 元</span>
        </div>
        <span class="mq-spacer"></span>
        <span class="mq-badge mq-badge--warning" data-mq-wear="badge">待发货</span>
        <button type="button" class="mq-btn mq-btn--sm" data-mq-wear="button">详情</button>
      </article>
      <article class="mq-card mq-listcard" data-mq-wear="card">
        <div class="mq-stack">
          <span class="mq-card__title">订单 1040</span>
          <span class="mq-caption">吴迪 · 88 元</span>
        </div>
        <span class="mq-spacer"></span>
        <span class="mq-badge mq-badge--info" data-mq-wear="badge">处理中</span>
        <button type="button" class="mq-btn mq-btn--sm" data-mq-wear="button">详情</button>
      </article>
    </div>

    <button type="button" class="mq-btn mq-btn--primary" data-mq-wear="button">新建订单</button>

    <nav class="mq-tabbar" data-mq-wear="nav" aria-label="底部标签栏">
      <span class="mq-tabbar__item mq-tabbar__item--active">
        <span class="mq-tabbar__indicator"></span>
        首页
      </span>
      <span class="mq-tabbar__item">订单</span>
      <span class="mq-tabbar__item">消息</span>
      <span class="mq-tabbar__item">我的</span>
    </nav>
  </div>
</template>