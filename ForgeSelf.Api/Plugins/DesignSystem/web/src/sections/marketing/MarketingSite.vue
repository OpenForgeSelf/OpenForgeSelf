<script setup lang="ts">
/** 官网设计稿（Marketing Site）· Hero + Features + Architecture + Code + CTA。 */
import BrandLogo from '../../components/BrandLogo.vue'
import GradientBlob from '../../components/GradientBlob.vue'
import TopologyMotif from '../../components/TopologyMotif.vue'
import DsButton from '../../components/DsButton.vue'
import Icon from '../../components/Icon.vue'
import CodeBlock from '../../components/CodeBlock.vue'

const features = [
  { icon: 'network', title: '统一编排', desc: '连接、编排、执行三层解耦，多 Agent 协作完成复杂任务链。' },
  { icon: 'server', title: '插件内核', desc: '后端插件 + 自带界面，运行时远程加载，前端共享宿主实例无双实例。' },
  { icon: 'gauge', title: '实时观测', desc: '服务健康、P99、集群负载一屏掌握，异常秒级感知。' },
  { icon: 'palette', title: '设计一致', desc: '统一的设计系统规范类型 / 颜色 / 间距 / 组件 / 品牌。' },
]
const arch = [
  { layer: '体验层', items: '控制台 UI Kit · 官网 · 插件界面' },
  { layer: '编排层', items: 'AIAgent · WorkflowEngine · Scheduler' },
  { layer: '内核层', items: '插件运行时 · 事件总线 · 共享服务表' },
  { layer: '数据层', items: 'NewLife.XCode · SQLite · 配置中枢' },
]
const code = `// 几行接入 ForgeSelf 插件内核
public class MyPlugin : IPlugin
{
    public void Apply(IContext ctx)
    {
        ctx.Register<IMyService>(new MyService());
    }
}`
</script>

<template>
  <div class="ds-mkt">
    <!-- Hero -->
    <section class="ds-mkt__hero">
      <GradientBlob :size="420" class="ds-mkt__blob" />
      <div class="ds-mkt__hero-inner">
        <BrandLogo variant="full" :size="40" />
        <h1 class="ds-display">为自托管而生的<br />智能控制台</h1>
        <p class="ds-body-lg" style="max-width: 560px; color: var(--ds-fg-2)">
          ForgeSelf（铸己匣）用统一的插件内核与设计系统，
          把 AI 编排、服务观测与自动化部署收拢到一个清爽的控制台。
        </p>
        <div class="ds-row ds-gap-3">
          <DsButton size="lg" icon="rocket">立即开始</DsButton>
          <DsButton size="lg" variant="secondary" icon="box">查看文档</DsButton>
        </div>
        <div class="ds-mkt__hero-topo"><TopologyMotif :size="220" /></div>
      </div>
    </section>

    <!-- Features -->
    <section class="ds-mkt__sec">
      <div class="ds-section-title"><span class="ds-eyebrow">FEATURES</span><h2 class="ds-h2">核心能力</h2></div>
      <div class="ds-mkt__grid">
        <div v-for="f in features" :key="f.title" class="ds-surface ds-stack ds-gap-3" style="padding: var(--ds-space-5)">
          <span class="ds-feature-ico"><Icon :name="f.icon" :size="22" /></span>
          <div class="ds-h3" style="font-size: var(--ds-fs-h4)">{{ f.title }}</div>
          <div class="ds-small">{{ f.desc }}</div>
        </div>
      </div>
    </section>

    <!-- Architecture -->
    <section class="ds-mkt__sec">
      <div class="ds-section-title"><span class="ds-eyebrow">ARCHITECTURE</span><h2 class="ds-h2">分层架构</h2></div>
      <div class="ds-stack ds-gap-3">
        <div v-for="a in arch" :key="a.layer" class="ds-mkt__layer">
          <span class="ds-mkt__layer-name ds-h4">{{ a.layer }}</span>
          <span class="ds-body">{{ a.items }}</span>
        </div>
      </div>
    </section>

    <!-- Code -->
    <section class="ds-mkt__sec">
      <div class="ds-section-title"><span class="ds-eyebrow">CODE</span><h2 class="ds-h2">开发者友好</h2></div>
      <CodeBlock :code="code" filename="MyPlugin.cs" />
    </section>

    <!-- CTA -->
    <section class="ds-mkt__cta">
      <GradientBlob :size="300" class="ds-mkt__blob" />
      <h2 class="ds-h1">开始构建你的控制台</h2>
      <p class="ds-body-lg">开源、自托管、可插件扩展。</p>
      <DsButton size="lg" icon="rocket">免费部署</DsButton>
    </section>
  </div>
</template>

<style scoped>
.ds-mkt {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-8);
}
.ds-mkt__hero {
  position: relative;
  overflow: hidden;
  border-radius: var(--ds-radius-2xl);
  background: var(--ds-gradient-brand-soft);
  border: 1px solid var(--ds-border-1);
  padding: var(--ds-space-8);
}
.ds-mkt__blob {
  position: absolute;
  top: -120px;
  right: -80px;
  pointer-events: none;
}
.ds-mkt__hero-inner {
  position: relative;
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: var(--ds-space-4);
}
.ds-mkt__hero-topo {
  position: absolute;
  right: var(--ds-space-6);
  bottom: 0;
  opacity: 0.7;
  pointer-events: none;
}
.ds-mkt__sec {
  scroll-margin-top: 80px;
}
.ds-mkt__grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: var(--ds-space-4);
}
.ds-feature-ico {
  width: 44px;
  height: 44px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: var(--ds-radius-md);
  background: var(--ds-surface-1);
  color: var(--ds-color-primary);
  border: 1px solid var(--ds-border-1);
  box-shadow: var(--ds-shadow-sm);
}
.ds-mkt__layer {
  display: flex;
  align-items: center;
  gap: var(--ds-space-4);
  background: var(--ds-surface-1);
  border: 1px solid var(--ds-border-1);
  border-left: 4px solid var(--ds-color-primary);
  border-radius: var(--ds-radius-md);
  padding: var(--ds-space-4) var(--ds-space-5);
}
.ds-mkt__layer-name {
  min-width: 96px;
  color: var(--ds-color-primary);
}
.ds-mkt__cta {
  position: relative;
  overflow: hidden;
  text-align: center;
  border-radius: var(--ds-radius-2xl);
  background: var(--ds-gradient-brand);
  color: #fff;
  padding: var(--ds-space-9) var(--ds-space-6);
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--ds-space-3);
}
.ds-mkt__cta .ds-blob {
  position: absolute;
  bottom: -140px;
  left: 50%;
  transform: translateX(-50%);
  opacity: 0.35;
  background: #fff;
}
.ds-mkt__cta :deep(.ds-btn--primary) {
  background: #fff;
  color: var(--ds-color-primary);
}
.ds-mkt__cta :deep(.ds-btn--primary):hover {
  background: var(--ds-surface-2);
}
</style>
