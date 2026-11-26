<script setup lang="ts">
/**
 * S09 项目设置（02-spec.md AC-D3）= 项目档案 + 工作台显示偏好。
 * 修 D-2：
 *  - 显示偏好控件改为进屏常驻（原实现藏在默认折叠的 config-card 里，实测 input/switch 全 0）；
 *  - 每项都必须真实生效：列表/卡片布局落 localStorage 并由 S01 读取，
 *    状态栏路径标记落 localStorage 并由 WorkbenchView 状态栏读取；
 *  - 删除 autoRefresh / confirmDangerous / defaultCommandPrefix 三个「写了不生效」的假开关（FR-3）。
 * 项目档案仍沿用原 ProjectEditDialog 经真实 PUT 写回后端。
 */
import { computed, ref } from 'vue'
import { ic } from '../icons'
import {
  currentProject, projectConfigs, setProjectConfig, projectLayout, setProjectLayout,
  toProjectInfo, loadProjects, toast,
} from '../workbenchStore'
import ProjectEditDialog from '../../ProjectEditDialog.vue'

const p = computed(() => currentProject())
const projectInfo = computed(() => toProjectInfo(p.value))
const editing = ref(false)
const cfg = computed(() => projectConfigs(p.value))

const showPathBadge = computed(() => cfg.value.showPathBadge !== false)
const layout = computed(() => projectLayout.value)

function onTogglePathBadge() {
  setProjectConfig(p.value, 'showPathBadge', !showPathBadge.value)
  toast(showPathBadge.value ? '已显示状态栏路径' : '已隐藏状态栏路径')
}
function onLayout(v: 'list' | 'card') {
  setProjectLayout(v)
  toast(v === 'list' ? '项目列表已切换为列表模式' : '项目列表已切换为卡片模式')
}
async function onSaved() {
  editing.value = false
  await loadProjects()
}
</script>

<template>
  <div class="proj-header">
    <div class="proj-lead">
      <span class="wb-eyebrow">{{ p?.name }}</span>
      <h2>项目设置</h2>
    </div>
    <div class="proj-tools">
      <button class="btn btn-secondary" @click="editing = true">
        <span v-html="ic('pencil', 'sm')"></span>编辑项目档案
      </button>
    </div>
  </div>

  <div class="proj-canvas">
    <!-- 工作台显示偏好：常驻可见，两个控件都真实作用到工作台 UI -->
    <div class="card" style="max-width:560px">
      <div class="sub-title">工作台显示偏好（本地持久化）</div>
      <div class="form-group" style="margin-top:12px">
        <label class="form-label">项目列表布局</label>
        <div class="wb-toolbar" role="group" aria-label="项目列表布局">
          <button class="btn" :class="layout === 'list' ? 'btn-primary' : 'btn-secondary'"
                  :aria-pressed="layout === 'list' ? 'true' : 'false'"
                  aria-label="列表模式" @click="onLayout('list')">列表</button>
          <button class="btn" :class="layout === 'card' ? 'btn-primary' : 'btn-secondary'"
                  :aria-pressed="layout === 'card' ? 'true' : 'false'"
                  aria-label="卡片模式" @click="onLayout('card')">卡片</button>
        </div>
        <div class="form-hint">切换后立即作用于「项目工作台」列表，并记住偏好。</div>
      </div>
      <div class="switch-row">
        <span>状态栏显示项目路径</span>
        <button class="switch" :class="{ on: showPathBadge }" role="switch"
                :aria-checked="showPathBadge ? 'true' : 'false'" aria-label="状态栏显示项目路径"
                @click="onTogglePathBadge()"></button>
      </div>
      <p class="wb-hint" style="margin-top:12px">
        开关变更即时生效并自动保存到 localStorage，刷新页面后保留。
      </p>
    </div>

    <!-- 项目档案：真实后端字段，PUT 写回 -->
    <div class="card" style="max-width:560px;margin-top:16px">
      <div class="sub-title">项目档案（后端字段）</div>
      <dl class="wb-kv" style="margin-top:8px">
        <dt>名称</dt><dd>{{ p?.name }}</dd>
        <dt>类型</dt><dd>{{ p?.type || '未分类' }}</dd>
        <dt>标签</dt><dd>{{ p?.tags || '无' }}</dd>
        <dt>描述</dt><dd>{{ p?.description || '—' }}</dd>
        <dt>根目录（只读）</dt><dd>{{ p?.path }}</dd>
      </dl>
      <button class="btn btn-secondary" style="margin-top:12px" @click="editing = true">
        <span v-html="ic('pencil', 'sm')"></span>编辑项目档案
      </button>
      <p class="wb-hint" style="margin-top:12px">
        档案的修改经 PUT /api/projects/{id} 写回后端；失败可在本屏重新编辑重试（BR-1）。
      </p>
    </div>

    <ProjectEditDialog v-if="editing && projectInfo" :project="projectInfo" @close="editing = false" @saved="onSaved" />
  </div>
</template>
