<script setup lang="ts">
/**
 * 状态板 · 概览模特（切片 A）。
 *
 * 为什么是"6 张状态卡 + 事件表 + tooltip"：状态板的核心不是流程，而是**一眼分辨健康度**，
 * 所以它必须把 success / warning / danger / info 四种语义档位同时摆出来，让人看清这套皮肤
 * 在密集排布、相邻对比时四色是否仍互相分得开；迷你进度条则是唯一用到"宽度百分比"的部件，
 * 用来验证尺寸类布局在没有 px 字面量的前提下也能成立。值只取对应语义变量与 `--ds-progress` 系列。
 */
</script>

<template>
  <div class="mq-page mq-status-board" data-mq-page="status-board">
    <div class="mq-row mq-row--between">
      <div class="mq-stack">
        <h1 class="mq-page__title">服务概览</h1>
        <span class="mq-page__subtitle">通用占位状态，仅演示语义档位</span>
      </div>
      <span class="mq-tooltip">
        <span class="mq-tooltip__anchor">指标口径</span>
        <span class="mq-tooltip__tip" data-mq-wear="tooltip">数值为近五分钟聚合，每分钟刷新</span>
      </span>
    </div>

    <div class="mq-grid mq-grid--cards">
      <article class="mq-card mq-status-card" data-mq-wear="card">
        <div class="mq-card__head">
          <span class="mq-caption">接口可用性</span>
          <span class="mq-badge mq-badge--success" data-mq-wear="badge">正常</span>
        </div>
        <strong class="mq-status-card__value">99.98%</strong>
        <div class="mq-progress"><div class="mq-progress__bar mq-progress__bar--success"></div></div>
      </article>
      <article class="mq-card mq-status-card" data-mq-wear="card">
        <div class="mq-card__head">
          <span class="mq-caption">平均延迟</span>
          <span class="mq-badge mq-badge--warning" data-mq-wear="badge">偏高</span>
        </div>
        <strong class="mq-status-card__value">182 级</strong>
        <div class="mq-progress"><div class="mq-progress__bar mq-progress__bar--warning"></div></div>
      </article>
      <article class="mq-card mq-status-card" data-mq-wear="card">
        <div class="mq-card__head">
          <span class="mq-caption">错误率</span>
          <span class="mq-badge mq-badge--danger" data-mq-wear="badge">告警</span>
        </div>
        <strong class="mq-status-card__value">2.4%</strong>
        <div class="mq-progress"><div class="mq-progress__bar mq-progress__bar--danger"></div></div>
      </article>
      <article class="mq-card mq-status-card" data-mq-wear="card">
        <div class="mq-card__head">
          <span class="mq-caption">队列积压</span>
          <span class="mq-badge mq-badge--info" data-mq-wear="badge">观察</span>
        </div>
        <strong class="mq-status-card__value">1,204</strong>
        <div class="mq-progress"><div class="mq-progress__bar mq-progress__bar--info"></div></div>
      </article>
      <article class="mq-card mq-status-card" data-mq-wear="card">
        <div class="mq-card__head">
          <span class="mq-caption">缓存命中</span>
          <span class="mq-badge mq-badge--success" data-mq-wear="badge">正常</span>
        </div>
        <strong class="mq-status-card__value">94.1%</strong>
        <div class="mq-progress"><div class="mq-progress__bar mq-progress__bar--success"></div></div>
      </article>
      <article class="mq-card mq-status-card" data-mq-wear="card">
        <div class="mq-card__head">
          <span class="mq-caption">磁盘使用</span>
          <span class="mq-badge mq-badge--info" data-mq-wear="badge">观察</span>
        </div>
        <strong class="mq-status-card__value">61%</strong>
        <div class="mq-progress"><div class="mq-progress__bar mq-progress__bar--info"></div></div>
      </article>
    </div>

    <section class="mq-card" data-mq-wear="card">
      <div class="mq-card__head">
        <h2 class="mq-card__title">事件</h2>
        <span class="mq-caption">最近 4 条</span>
      </div>
      <div class="mq-table mq-table--3" data-mq-wear="table">
        <div class="mq-table__head">
          <span>时间</span>
          <span>事件</span>
          <span>级别</span>
        </div>
        <div class="mq-table__row">
          <span class="mq-table__num">14:02</span>
          <span>接口 p95 超过阈值</span>
          <span><span class="mq-badge mq-badge--warning" data-mq-wear="badge">警告</span></span>
        </div>
        <div class="mq-table__row">
          <span class="mq-table__num">13:48</span>
          <span>错误率短暂升至 2.4%</span>
          <span><span class="mq-badge mq-badge--danger" data-mq-wear="badge">严重</span></span>
        </div>
        <div class="mq-table__row">
          <span class="mq-table__num">13:20</span>
          <span>队列消费恢复</span>
          <span><span class="mq-badge mq-badge--success" data-mq-wear="badge">恢复</span></span>
        </div>
        <div class="mq-table__row">
          <span class="mq-table__num">12:55</span>
          <span>每日备份完成</span>
          <span><span class="mq-badge mq-badge--info" data-mq-wear="badge">提示</span></span>
        </div>
      </div>
    </section>
  </div>
</template>