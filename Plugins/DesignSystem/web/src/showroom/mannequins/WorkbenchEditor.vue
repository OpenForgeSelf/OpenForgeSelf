<script setup lang="ts">
/**
 * 工具 · 工作台编辑器模特（切片 B，03-plan §M）。
 *
 * 为什么是"三栏 + 状态栏"：工作台编辑器代表**工具型密集界面**——左侧文件树、中间代码、
 * 右侧属性面板，信息密度远高于后台列表页；底部状态栏则是最容易在换肤时被忽略的一层。
 * 代码区刻意用 `mq-mono`（只读 `--ds-font-mono`），用来验证等宽档在换预设后是否真的跟着走；
 * 属性面板同时摆出 input 与 select，检查两种控件在紧缩疏密下是否仍对齐。
 * 结构只摆 `mq-*` 类与 `data-mq-wear`，全部观感交给 mannequin.css 的 `--ds-*` 变量。
 */
</script>

<template>
  <div class="mq-page mq-workbench-editor" data-mq-page="workbench-editor">
    <header class="mq-topbar">
      <div class="mq-topbar__brand">
        <span class="mq-topbar__mark">铸</span>
        <span>铸己匣 · 编辑器</span>
      </div>
      <div class="mq-row">
        <span class="mq-badge mq-badge--info" data-mq-wear="badge">自动保存</span>
        <button type="button" class="mq-btn" data-mq-wear="button">预览</button>
        <button type="button" class="mq-btn mq-btn--primary" data-mq-wear="button">保存</button>
      </div>
    </header>

    <div class="mq-grid mq-grid--editor">
      <aside class="mq-sidebar">
        <span class="mq-label">文件</span>
        <nav class="mq-nav" data-mq-wear="nav" aria-label="文件树">
          <span class="mq-nav__item mq-nav__item--active">
            <span class="mq-nav__indicator"></span>
            theme.tokens
          </span>
          <span class="mq-nav__item">colors.tokens</span>
          <span class="mq-nav__item">type.tokens</span>
          <span class="mq-nav__item">components.tokens</span>
        </nav>
      </aside>

      <main class="mq-main">
        <div class="mq-tabs" data-mq-wear="tabs">
          <span class="mq-tab mq-tab--active">theme.tokens</span>
          <span class="mq-tab">colors.tokens</span>
          <span class="mq-tab">type.tokens</span>
        </div>
        <pre class="mq-code mq-mono" aria-label="令牌源码">--ds-semantic-brand
--ds-component-button-primary-background
--ds-radius-lg</pre>
      </main>

      <aside class="mq-card mq-props" data-mq-wear="card">
        <div class="mq-card__head">
          <h2 class="mq-card__title">属性</h2>
          <span class="mq-badge mq-badge--brand" data-mq-wear="badge">语义</span>
        </div>
        <label class="mq-field">
          <span class="mq-label">组</span>
          <input class="mq-input" value="semantic" readonly data-mq-wear="input" />
        </label>
        <label class="mq-field">
          <span class="mq-label">键</span>
          <input class="mq-input" value="brand" readonly data-mq-wear="input" />
        </label>
        <label class="mq-field">
          <span class="mq-label">别名指向</span>
          <input class="mq-input" value="--ds-semantic-brand" readonly data-mq-wear="input" />
        </label>
        <label class="mq-field">
          <span class="mq-label">控件类型</span>
          <select class="mq-select" data-mq-wear="select">
            <option>颜色</option>
            <option>尺寸</option>
          </select>
        </label>
        <label class="mq-field">
          <span class="mq-label">作用域</span>
          <select class="mq-select" data-mq-wear="select">
            <option>全部主题</option>
            <option>仅浅色</option>
          </select>
        </label>
      </aside>
    </div>

    <footer class="mq-statusbar">
      <span class="mq-row">
        <span class="mq-badge mq-badge--success" data-mq-wear="badge">已同步</span>
        <span class="mq-badge mq-badge--info" data-mq-wear="badge">浅色</span>
      </span>
      <span class="mq-row">
        <span class="mq-caption">令牌 388</span>
        <span class="mq-badge mq-badge--neutral" data-mq-wear="badge">无差异</span>
      </span>
    </footer>
  </div>
</template>