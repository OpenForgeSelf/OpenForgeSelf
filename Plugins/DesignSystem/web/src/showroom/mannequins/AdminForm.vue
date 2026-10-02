<script setup lang="ts">
/**
 * 后台 · 表单模特（切片 A）。
 *
 * 为什么特意摆一个**校验失败态**与一条**成功提示条**：普通态控件几乎都一样，
 * 真正会露馅的是"错误红"和"成功绿"落在这套皮肤里是否还看得清、是否与描边/底色打架。
 * 所以这一页把两种语义色都翻出来静态展示，值只取 `--ds-semantic-danger` 与 `--ds-semantic-success`，
 * 换成别的主题时它们会跟着定义走，不会停在写死的红绿上。
 */
</script>

<template>
  <div class="mq-page mq-admin-form" data-mq-page="admin-form">
    <div class="mq-row mq-row--between">
      <div class="mq-stack">
        <h1 class="mq-page__title">新建客户</h1>
        <span class="mq-page__subtitle">通用占位表单，演示控件与语义色</span>
      </div>
      <span class="mq-badge mq-badge--neutral" data-mq-wear="badge">草稿</span>
    </div>

    <div class="mq-alert mq-alert--success">保存成功，已同步到客户列表。</div>

    <div class="mq-grid mq-grid--split">
      <section class="mq-card" data-mq-wear="card">
        <h2 class="mq-card__title">基本信息</h2>
        <div class="mq-field">
          <label class="mq-label" for="mq-form-name">姓名</label>
          <input id="mq-form-name" class="mq-input" data-mq-wear="input" placeholder="请输入姓名" />
        </div>
        <div class="mq-field">
          <label class="mq-label" for="mq-form-phone">手机号</label>
          <input
            id="mq-form-phone"
            class="mq-input mq-input--invalid"
            data-mq-wear="input"
            placeholder="请输入手机号"
          />
          <span class="mq-error">手机号格式不正确</span>
        </div>
        <div class="mq-field">
          <label class="mq-label" for="mq-form-tags">标签</label>
          <input id="mq-form-tags" class="mq-input" data-mq-wear="input" placeholder="用逗号分隔" />
        </div>
        <div class="mq-field">
          <label class="mq-label" for="mq-form-owner">负责人</label>
          <input id="mq-form-owner" class="mq-input" data-mq-wear="input" placeholder="请输入负责人" />
        </div>
        <span class="mq-badge mq-badge--warning" data-mq-wear="badge">必填</span>
      </section>

      <section class="mq-card" data-mq-wear="card">
        <h2 class="mq-card__title">来源与备注</h2>
        <div class="mq-field">
          <label class="mq-label" for="mq-form-source">来源渠道</label>
          <select id="mq-form-source" class="mq-select" data-mq-wear="select">
            <option>官网</option>
            <option>转介绍</option>
            <option>广告投放</option>
          </select>
        </div>
        <div class="mq-field">
          <label class="mq-label" for="mq-form-level">客户等级</label>
          <select id="mq-form-level" class="mq-select" data-mq-wear="select">
            <option>普通</option>
            <option>重点</option>
            <option>战略</option>
          </select>
        </div>
        <div class="mq-field">
          <label class="mq-label" for="mq-form-note">备注</label>
          <textarea id="mq-form-note" class="mq-textarea" data-mq-wear="input" placeholder="补充说明"></textarea>
        </div>
      </section>
    </div>

    <div class="mq-row">
      <button type="button" class="mq-btn mq-btn--primary" data-mq-wear="button">提交</button>
      <button type="button" class="mq-btn mq-btn--secondary" data-mq-wear="button">取消</button>
    </div>
  </div>
</template>