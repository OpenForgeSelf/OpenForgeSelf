<script setup lang="ts">
/**
 * 后台 · 仪表盘模特（切片 A）。
 *
 * 为什么做成"纯静态样板"：展厅要让人一眼看见"这套皮肤长在真实后台里是什么样"，
 * 但页面上只要写死一个颜色或尺寸字面量，换主题/换疏密时这一处就不会跟着走，
 * "展厅里看到的"与"导出交付的"就悄悄分叉了。所以结构只摆 `mq-*` 类与 `data-mq-wear`，
 * 全部观感交给 mannequin.css 里的 `--ds-*` 变量。
 * 顶栏 + 侧栏给骨架，4 张 KPI 卡 + 柱状图 + 订单表覆盖后台最常见的三段信息密度。
 */
</script>

<template>
  <div class="mq-page mq-admin-dashboard" data-mq-page="admin-dashboard">
    <header class="mq-topbar">
      <div class="mq-topbar__brand">
        <span class="mq-topbar__mark">铸</span>
        <span>铸己台 · 控制台</span>
      </div>
      <div class="mq-row">
        <span class="mq-muted mq-small">运营中心</span>
        <span class="mq-badge mq-badge--success" data-mq-wear="badge">在线</span>
      </div>
    </header>

    <div class="mq-shell">
      <aside class="mq-sidebar">
        <span class="mq-label">导航</span>
        <nav class="mq-nav" data-mq-wear="nav" aria-label="后台导航">
          <span class="mq-nav__item mq-nav__item--active">
            <span class="mq-nav__indicator"></span>
            仪表盘
          </span>
          <span class="mq-nav__item">订单</span>
          <span class="mq-nav__item">客户</span>
          <span class="mq-nav__item">商品</span>
          <span class="mq-nav__item">设置</span>
        </nav>
      </aside>

      <main class="mq-main">
        <div class="mq-row mq-row--between">
          <div class="mq-stack">
            <h1 class="mq-page__title">仪表盘</h1>
            <span class="mq-page__subtitle">今日概览，数据为通用占位</span>
          </div>
          <div class="mq-row">
            <button type="button" class="mq-btn mq-btn--secondary" data-mq-wear="button">导出</button>
            <button type="button" class="mq-btn mq-btn--primary" data-mq-wear="button">新建订单</button>
          </div>
        </div>

        <div class="mq-grid mq-grid--kpi">
          <article class="mq-card" data-mq-wear="card">
            <div class="mq-card__head">
              <span class="mq-caption">今日订单</span>
              <span class="mq-badge mq-badge--success" data-mq-wear="badge">+12%</span>
            </div>
            <strong class="mq-card__metric">1,284</strong>
            <span class="mq-caption">较昨日</span>
          </article>
          <article class="mq-card" data-mq-wear="card">
            <div class="mq-card__head">
              <span class="mq-caption">成交额</span>
              <span class="mq-badge mq-badge--success" data-mq-wear="badge">+8%</span>
            </div>
            <strong class="mq-card__metric">92,410</strong>
            <span class="mq-caption">较昨日</span>
          </article>
          <article class="mq-card" data-mq-wear="card">
            <div class="mq-card__head">
              <span class="mq-caption">退款率</span>
              <span class="mq-badge mq-badge--danger" data-mq-wear="badge">-3%</span>
            </div>
            <strong class="mq-card__metric">1.8%</strong>
            <span class="mq-caption">较昨日</span>
          </article>
          <article class="mq-card" data-mq-wear="card">
            <div class="mq-card__head">
              <span class="mq-caption">活跃客户</span>
              <span class="mq-badge mq-badge--warning" data-mq-wear="badge">-1%</span>
            </div>
            <strong class="mq-card__metric">642</strong>
            <span class="mq-caption">较昨日</span>
          </article>
        </div>

        <section class="mq-card" data-mq-wear="card">
          <div class="mq-card__head">
            <h2 class="mq-card__title">近四日成交</h2>
            <span class="mq-caption">单位：单</span>
          </div>
          <div class="mq-chart">
            <div class="mq-chart__plot">
              <div class="mq-bar mq-bar--1"></div>
              <div class="mq-bar mq-bar--2"></div>
              <div class="mq-bar mq-bar--3"></div>
              <div class="mq-bar mq-bar--4"></div>
            </div>
            <div class="mq-chart__axis">
              <span>周一</span>
              <span>周二</span>
              <span>周三</span>
              <span>周四</span>
            </div>
          </div>
        </section>

        <section class="mq-card" data-mq-wear="card">
          <div class="mq-card__head">
            <h2 class="mq-card__title">最近订单</h2>
            <span class="mq-caption">最近 5 条</span>
          </div>
          <div class="mq-table" data-mq-wear="table">
            <div class="mq-table__head">
              <span>订单号</span>
              <span>客户</span>
              <span>金额</span>
              <span>状态</span>
            </div>
            <div class="mq-table__row">
              <span class="mq-table__strong">D-1042</span>
              <span>李青</span>
              <span class="mq-table__num">¥ 320</span>
              <span><span class="mq-badge mq-badge--success" data-mq-wear="badge">已完成</span></span>
            </div>
            <div class="mq-table__row">
              <span class="mq-table__strong">D-1041</span>
              <span>周然</span>
              <span class="mq-table__num">¥ 1,050</span>
              <span><span class="mq-badge mq-badge--warning" data-mq-wear="badge">待发货</span></span>
            </div>
            <div class="mq-table__row">
              <span class="mq-table__strong">D-1040</span>
              <span>吴迪</span>
              <span class="mq-table__num">¥ 88</span>
              <span><span class="mq-badge mq-badge--info" data-mq-wear="badge">处理中</span></span>
            </div>
            <div class="mq-table__row">
              <span class="mq-table__strong">D-1039</span>
              <span>徐岚</span>
              <span class="mq-table__num">¥ 640</span>
              <span><span class="mq-badge mq-badge--danger" data-mq-wear="badge">已取消</span></span>
            </div>
            <div class="mq-table__row">
              <span class="mq-table__strong">D-1038</span>
              <span>何铭</span>
              <span class="mq-table__num">¥ 210</span>
              <span><span class="mq-badge mq-badge--success" data-mq-wear="badge">已完成</span></span>
            </div>
          </div>
        </section>
      </main>
    </div>
  </div>
</template>