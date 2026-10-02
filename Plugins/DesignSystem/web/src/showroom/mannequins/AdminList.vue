<script setup lang="ts">
/**
 * 后台 · 列表模特（切片 A）。
 *
 * 为什么挑"筛选栏 + 状态页签 + 数据表 + 分页"四段：这是后台里**控件密度最高**的一屏，
 * 输入框、下拉、页签、徽标、按钮、表格会同时出现在同一视口，最能暴露皮肤在"很多小控件挤在一起"
 * 时的间距/描边/对齐问题。内容仍为通用占位，观感全部由 mannequin.css 的 `--ds-*` 变量决定。
 */
</script>

<template>
  <div class="mq-page mq-admin-list" data-mq-page="admin-list">
    <header class="mq-topbar">
      <div class="mq-topbar__brand">
        <span class="mq-topbar__mark">铸</span>
        <span>铸己台 · 订单</span>
      </div>
      <div class="mq-row">
        <span class="mq-badge mq-badge--brand" data-mq-wear="badge">列表</span>
      </div>
    </header>

    <section class="mq-toolbar">
      <div class="mq-field">
        <label class="mq-label" for="mq-list-keyword">关键词</label>
        <input id="mq-list-keyword" class="mq-input" data-mq-wear="input" placeholder="订单号或客户名" />
      </div>
      <div class="mq-field">
        <label class="mq-label" for="mq-list-status">状态</label>
        <select id="mq-list-status" class="mq-select" data-mq-wear="select">
          <option>全部</option>
          <option>已完成</option>
          <option>待发货</option>
        </select>
      </div>
      <div class="mq-field">
        <label class="mq-label" for="mq-list-range">时间范围</label>
        <select id="mq-list-range" class="mq-select" data-mq-wear="select">
          <option>近七日</option>
          <option>近三十日</option>
          <option>全部</option>
        </select>
      </div>
      <div class="mq-row">
        <button type="button" class="mq-btn mq-btn--primary" data-mq-wear="button">查询</button>
        <button type="button" class="mq-btn mq-btn--secondary" data-mq-wear="button">重置</button>
      </div>
    </section>

    <div class="mq-tabs" data-mq-wear="tabs" role="tablist" aria-label="订单状态">
      <span class="mq-tab mq-tab--active" role="tab">全部</span>
      <span class="mq-tab" role="tab">已完成</span>
      <span class="mq-tab" role="tab">待发货</span>
      <span class="mq-tab" role="tab">已取消</span>
    </div>

    <section class="mq-card" data-mq-wear="card">
      <div class="mq-table mq-table--5" data-mq-wear="table">
        <div class="mq-table__head">
          <span>订单号</span>
          <span>客户</span>
          <span>金额</span>
          <span>状态</span>
          <span>操作</span>
        </div>
        <div class="mq-table__row">
          <span class="mq-table__strong">D-1042</span>
          <span>李青</span>
          <span class="mq-table__num">¥ 320</span>
          <span><span class="mq-badge mq-badge--success" data-mq-wear="badge">已完成</span></span>
          <span><button type="button" class="mq-btn mq-btn--sm mq-btn--ghost" data-mq-wear="button">查看</button></span>
        </div>
        <div class="mq-table__row">
          <span class="mq-table__strong">D-1041</span>
          <span>周然</span>
          <span class="mq-table__num">¥ 1,050</span>
          <span><span class="mq-badge mq-badge--warning" data-mq-wear="badge">待发货</span></span>
          <span><button type="button" class="mq-btn mq-btn--sm mq-btn--ghost" data-mq-wear="button">查看</button></span>
        </div>
        <div class="mq-table__row">
          <span class="mq-table__strong">D-1040</span>
          <span>吴迪</span>
          <span class="mq-table__num">¥ 88</span>
          <span><span class="mq-badge mq-badge--info" data-mq-wear="badge">处理中</span></span>
          <span><button type="button" class="mq-btn mq-btn--sm mq-btn--ghost" data-mq-wear="button">查看</button></span>
        </div>
        <div class="mq-table__row">
          <span class="mq-table__strong">D-1039</span>
          <span>徐岚</span>
          <span class="mq-table__num">¥ 640</span>
          <span><span class="mq-badge mq-badge--danger" data-mq-wear="badge">已取消</span></span>
          <span><button type="button" class="mq-btn mq-btn--sm mq-btn--ghost" data-mq-wear="button">查看</button></span>
        </div>
        <div class="mq-table__row">
          <span class="mq-table__strong">D-1038</span>
          <span>何铭</span>
          <span class="mq-table__num">¥ 210</span>
          <span><span class="mq-badge mq-badge--success" data-mq-wear="badge">已完成</span></span>
          <span><button type="button" class="mq-btn mq-btn--sm mq-btn--ghost" data-mq-wear="button">查看</button></span>
        </div>
        <div class="mq-table__row">
          <span class="mq-table__strong">D-1037</span>
          <span>蔡宁</span>
          <span class="mq-table__num">¥ 96</span>
          <span><span class="mq-badge mq-badge--neutral" data-mq-wear="badge">草稿</span></span>
          <span><button type="button" class="mq-btn mq-btn--sm mq-btn--ghost" data-mq-wear="button">查看</button></span>
        </div>
      </div>
    </section>

    <div class="mq-pager">
      <button type="button" class="mq-pager__btn" data-mq-wear="button">上一页</button>
      <button type="button" class="mq-pager__btn mq-pager__btn--active" data-mq-wear="button">1</button>
      <button type="button" class="mq-pager__btn" data-mq-wear="button">2</button>
      <button type="button" class="mq-pager__btn" data-mq-wear="button">3</button>
      <button type="button" class="mq-pager__btn" data-mq-wear="button">下一页</button>
    </div>
  </div>
</template>