<script setup lang="ts">
/**
 * S04 新建 · 2 步「登记」向导（02-spec.md AC-D1）
 *  - 第 1 步：确认已选目录（可返回目录浏览器重选）
 *  - 第 2 步：填写档案字段（name / type / tags / description）并登记
 *  - 只登记已存在的目录：POST /api/projects + PUT /api/projects/{id} 写回档案，
 *    不生成脚手架、不执行 npm / git、不在磁盘创建或修改任何文件（BR-1）。
 *  - 原 4 步向导的包管理器 / 预设 / 功能勾选 / 覆盖目录 / 精简脚手架 / Git 初始化
 *    六个假区域已整体删除（FR-3 不做假能力）。
 */
import { computed, ref } from 'vue'
import { ic } from '../icons'
import {
  store, go, toast, registerProject, openProject,
} from '../workbenchStore'
import type { WbWizardMeta } from '../workbenchStore'

const busy = ref(false)
const error = ref('')
const w = store.wizard

/** 目录由 S03 浏览时选定；为空表示尚未选择，禁止进入下一步。 */
const root = computed(() => w.root || store.folder.cwd || '')
const hasRoot = computed(() => root.value.trim().length > 0)

function back() {
  if (w.step > 0) { w.step--; error.value = '' }
  else go('/create')
}

async function next() {
  if (busy.value) return
  error.value = ''
  // 第 1 步：确认目录
  if (w.step === 0) {
    if (!hasRoot.value) {
      error.value = '请先返回目录浏览器选择一个已存在的目录'
      toast(error.value)
      return
    }
    w.step = 1
    return
  }
  // 第 2 步：校验名称并提交登记
  if (!w.name.trim()) {
    error.value = '请先填写项目名称'
    toast(error.value)
    return
  }
  busy.value = true
  const meta: WbWizardMeta = {
    type: w.type.trim(),
    tags: w.tags.trim(),
    description: w.description.trim(),
  }
  const p = await registerProject(root.value, w.name.trim(), meta)
  busy.value = false
  if (!p) return
  openProject(p.id)
  w.step = 0
  w.name = ''
  w.type = ''
  w.tags = ''
  w.description = ''
  toast(`项目已登记：${p.name}`)
}
</script>

<template>
  <div class="manager-body">
    <div class="wizard">
      <p class="wb-notice">
        <strong>登记说明：</strong>本向导只把<strong>已有目录</strong>登记到 Sems，并写回类型 / 标签 / 描述。
        <strong>不会在你的目录中创建或修改任何文件，也不会执行 npm / git 命令</strong>。
      </p>

      <!-- 第 1 步 · 确认目录 -->
      <template v-if="w.step === 0">
        <div class="form-group">
          <div class="sub-title">第 1 步 · 确认项目目录</div>
          <div class="form-inline">
            <input :value="root" class="form-control" aria-label="项目目录" readonly
                   placeholder="尚未选择目录" />
            <button class="btn-icon" aria-label="重选目录" @click="go('/create')">
              <span v-html="ic('pencil', 'sm')"></span>
            </button>
          </div>
          <div class="form-hint">目录必须已存在；Sems 只登记档案，不创建目录或脚手架。</div>
          <p v-if="!hasRoot" class="wb-hint" style="color:var(--el-color-error)">
            未选择目录：请点右侧按钮返回目录浏览器选择，否则无法进入下一步。
          </p>
          <p v-if="error" class="wb-hint" style="color:var(--el-color-error)">{{ error }}</p>
        </div>
      </template>

      <!-- 第 2 步 · 填写档案 -->
      <template v-else>
        <div class="form-group">
          <div class="sub-title">第 2 步 · 填写项目档案</div>
          <label class="form-label">项目名称（必填）</label>
          <input v-model="w.name" class="form-control" aria-label="项目名称"
                 placeholder="输入项目名称" maxlength="60" />
          <div class="form-hint">{{ root }}</div>
        </div>
        <div class="form-group">
          <label class="form-label">项目类型</label>
          <input v-model="w.type" class="form-control" aria-label="项目类型"
                 placeholder="如 frontend / backend / tool" />
        </div>
        <div class="form-group">
          <label class="form-label">标签</label>
          <input v-model="w.tags" class="form-control" aria-label="项目标签"
                 placeholder="多个标签用逗号分隔" />
        </div>
        <div class="form-group">
          <label class="form-label">描述</label>
          <input v-model="w.description" class="form-control" aria-label="项目描述" placeholder="可选" />
        </div>
        <div class="form-group">
          <div class="summary">
            <div><span>目标目录</span><span>{{ root }}</span></div>
            <div><span>项目名称</span><span>{{ w.name || '（未填写）' }}</span></div>
            <div><span>写回字段</span><span>type / tags / description</span></div>
          </div>
          <p class="wb-hint">提交后只登记目录并写回档案字段；<strong>不会在你的目录中创建或修改任何文件</strong>。</p>
        </div>
      </template>

      <div class="form-actions">
        <button class="btn btn-secondary btn-lg" :disabled="busy" @click="back()">
          <span v-html="ic('close')"></span>{{ w.step === 0 ? '取消' : '上一步' }}
        </button>
        <button class="btn btn-primary btn-lg" :disabled="busy || (w.step === 0 && !hasRoot)" @click="next()">
          {{ busy ? '登记中…' : w.step === 1 ? '登记项目' : '下一步' }}
          <span v-html="ic(w.step === 1 ? 'check' : 'back', 'sm')"></span>
        </button>
      </div>
    </div>
  </div>
</template>
