<script setup lang="ts">
/**
 * 后台 · 设置模特（切片 A）。
 *
 * 为什么要摆一个**打开态的对话框**：对话框是后台里唯一会盖住整页的组件，它的遮罩不透明度、
 * 气泡底色与投影只有真正叠在内容上才看得出来；只画一个孤立的卡片，评审时根本判断不了
 * "这层遮罩会不会太黑、气泡会不会飘不起来"。所以这里把遮罩与对话框**静态铺开**，
 * 遮罩值取 `--ds-component-dialog-scrim`，其余取 `--ds-component-dialog-*`。
 * 危险操作区单列，是为了看清"销毁类动作"用的语义红在这套皮肤里的可读性。
 */
</script>

<template>
  <div class="mq-page mq-admin-settings" data-mq-page="admin-settings">
    <div class="mq-row mq-row--between">
      <h1 class="mq-page__title">系统设置</h1>
      <span class="mq-badge mq-badge--brand" data-mq-wear="badge">管理员</span>
    </div>

    <div class="mq-tabs" data-mq-wear="tabs" role="tablist" aria-label="设置分组">
      <span class="mq-tab mq-tab--active" role="tab">常规</span>
      <span class="mq-tab" role="tab">通知</span>
      <span class="mq-tab" role="tab">权限</span>
      <span class="mq-tab" role="tab">高级</span>
    </div>

    <section class="mq-card" data-mq-wear="card">
      <h2 class="mq-card__title">常规</h2>
      <div class="mq-field">
        <label class="mq-label" for="mq-set-name">站点名称</label>
        <input id="mq-set-name" class="mq-input" data-mq-wear="input" placeholder="请输入站点名称" />
      </div>
      <div class="mq-field">
        <label class="mq-label" for="mq-set-domain">绑定域名</label>
        <input id="mq-set-domain" class="mq-input" data-mq-wear="input" placeholder="example.com" />
      </div>
      <div class="mq-field">
        <label class="mq-label" for="mq-set-lang">默认语言</label>
        <select id="mq-set-lang" class="mq-select" data-mq-wear="select">
          <option>简体中文</option>
          <option>English</option>
        </select>
      </div>
      <div class="mq-field">
        <label class="mq-label" for="mq-set-zone">时区</label>
        <select id="mq-set-zone" class="mq-select" data-mq-wear="select">
          <option>Asia/Shanghai</option>
          <option>UTC</option>
        </select>
      </div>
      <div class="mq-row">
        <button type="button" class="mq-btn mq-btn--primary" data-mq-wear="button">保存设置</button>
      </div>
    </section>

    <div class="mq-danger-zone">
      <div class="mq-stack">
        <span class="mq-danger-zone__title">危险操作</span>
        <span class="mq-hint">清空全部业务数据，操作不可撤销。</span>
      </div>
      <button type="button" class="mq-btn mq-btn--danger" data-mq-wear="button">清空数据</button>
    </div>

    <div class="mq-dialog-stage">
      <section class="mq-card" data-mq-wear="card">
        <h2 class="mq-card__title">高级</h2>
        <div class="mq-desc">
          <span class="mq-desc__term">审核模式</span>
          <span class="mq-desc__value">开启</span>
          <span class="mq-desc__term">审计留存</span>
          <span class="mq-desc__value">六个月</span>
        </div>
      </section>
      <div class="mq-scrim">
        <div class="mq-dialog" data-mq-wear="dialog" role="dialog" aria-label="确认清空数据">
          <h3 class="mq-dialog__title">确认清空数据</h3>
          <p class="mq-hint">此操作将删除全部订单与客户记录，且不可恢复。</p>
          <div class="mq-dialog__actions">
            <button type="button" class="mq-btn mq-btn--secondary" data-mq-wear="button">取消</button>
            <button type="button" class="mq-btn mq-btn--danger" data-mq-wear="button">确认清空</button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>