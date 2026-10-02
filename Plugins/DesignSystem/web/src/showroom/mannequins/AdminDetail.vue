<script setup lang="ts">
/**
 * 后台 · 详情模特（切片 A）。
 *
 * 为什么用"面包屑 + 头部 + 页签 + 描述列表 + 操作记录 + tooltip"这套组合：这是后台里
 * 层级信息最密的一屏 —— 用户要靠面包屑确认"我在哪"，靠头部按钮决定"下一步"，靠记录表回溯"发生了什么"。
 * tooltip 在这里**静态摆开**（不是 hover 才出现），因为展厅要能直接看出提示气泡的底色、圆角与投影
 * 在明/暗两套主题下是否都压得住，值取 `--ds-component-tooltip-*`。
 */
</script>

<template>
  <div class="mq-page mq-admin-detail" data-mq-page="admin-detail">
    <nav class="mq-breadcrumb" data-mq-wear="nav" aria-label="面包屑">
      <span class="mq-breadcrumb__item">订单</span>
      <span class="mq-breadcrumb__sep">/</span>
      <span class="mq-breadcrumb__item">列表</span>
      <span class="mq-breadcrumb__sep">/</span>
      <span class="mq-breadcrumb__item mq-breadcrumb__item--current">D-1042</span>
    </nav>

    <div class="mq-row mq-row--between">
      <div class="mq-row">
        <h1 class="mq-page__title">订单 D-1042</h1>
        <span class="mq-badge mq-badge--success" data-mq-wear="badge">已完成</span>
      </div>
      <div class="mq-row">
        <button type="button" class="mq-btn mq-btn--secondary" data-mq-wear="button">打印</button>
        <button type="button" class="mq-btn mq-btn--primary" data-mq-wear="button">编辑</button>
      </div>
    </div>

    <div class="mq-tabs" data-mq-wear="tabs" role="tablist" aria-label="订单详情">
      <span class="mq-tab mq-tab--active" role="tab">概览</span>
      <span class="mq-tab" role="tab">商品</span>
      <span class="mq-tab" role="tab">日志</span>
    </div>

    <section class="mq-card" data-mq-wear="card">
      <div class="mq-card__head">
        <h2 class="mq-card__title">基本信息</h2>
        <span class="mq-tooltip">
          <span class="mq-tooltip__anchor">字段说明</span>
          <span class="mq-tooltip__tip" data-mq-wear="tooltip">金额为含税总价，单位：元</span>
        </span>
      </div>
      <div class="mq-desc">
        <span class="mq-desc__term">客户</span>
        <span class="mq-desc__value">李青</span>
        <span class="mq-desc__term">下单时间</span>
        <span class="mq-desc__value">2026-09-30 14:20</span>
        <span class="mq-desc__term">金额</span>
        <span class="mq-desc__value">¥ 320</span>
        <span class="mq-desc__term">收货地址</span>
        <span class="mq-desc__value">上海市浦东新区示例路 100 号</span>
      </div>
    </section>

    <section class="mq-card" data-mq-wear="card">
      <div class="mq-card__head">
        <h2 class="mq-card__title">操作记录</h2>
        <span class="mq-caption">最近 4 条</span>
      </div>
      <div class="mq-table mq-table--5" data-mq-wear="table">
        <div class="mq-table__head">
          <span>时间</span>
          <span>操作人</span>
          <span>动作</span>
          <span>结果</span>
          <span>操作</span>
        </div>
        <div class="mq-table__row">
          <span class="mq-table__num">09-30 14:20</span>
          <span>系统</span>
          <span>创建订单</span>
          <span><span class="mq-badge mq-badge--info" data-mq-wear="badge">成功</span></span>
          <span><button type="button" class="mq-btn mq-btn--sm mq-btn--ghost" data-mq-wear="button">查看</button></span>
        </div>
        <div class="mq-table__row">
          <span class="mq-table__num">09-30 15:02</span>
          <span>客服小张</span>
          <span>修改地址</span>
          <span><span class="mq-badge mq-badge--success" data-mq-wear="badge">成功</span></span>
          <span><button type="button" class="mq-btn mq-btn--sm mq-btn--ghost" data-mq-wear="button">查看</button></span>
        </div>
        <div class="mq-table__row">
          <span class="mq-table__num">10-01 09:14</span>
          <span>仓库</span>
          <span>发货</span>
          <span><span class="mq-badge mq-badge--success" data-mq-wear="badge">成功</span></span>
          <span><button type="button" class="mq-btn mq-btn--sm mq-btn--ghost" data-mq-wear="button">查看</button></span>
        </div>
        <div class="mq-table__row">
          <span class="mq-table__num">10-01 18:40</span>
          <span>系统</span>
          <span>自动收货</span>
          <span><span class="mq-badge mq-badge--warning" data-mq-wear="badge">待确认</span></span>
          <span><button type="button" class="mq-btn mq-btn--sm mq-btn--ghost" data-mq-wear="button">查看</button></span>
        </div>
      </div>
    </section>
  </div>
</template>