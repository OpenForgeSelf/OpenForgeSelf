<script setup lang="ts">
/**
 * 品牌资产（FR10/AC16 延伸）：资产（logo / 母题）、字体登记、页面清单三张表的读写面。
 *
 * 为什么单独成一个 section：后端 `meta.capabilities` 一直声明 assets/screens/fonts 三项能力，
 * 但过去**只有 GET、也没有任何地方种数据** —— 界面只能显示"无…"。
 * "声明了却拿不到"和假能力同罪，所以这里补齐写入口，并让 generate 落的种子看得见、可改。
 *
 * 三条纪律：
 * 1. 数字只从后端来（列表与计数都是 API 回读，不前端自算）；
 * 2. 写完必须回读自证（保存后重新拉列表，显示"现在有几条"）；
 * 3. 字体许可证不许留空 —— 空许可证是给自己埋合规雷，界面直接拒绝提交。
 */
import { computed, onMounted, ref } from 'vue'
import { api, type Asset, type FontFace, type Screen } from '../api'
import { ApiError } from '../http'
import { currentProject, unauthorized } from '../state'
import PanelState from '../components/PanelState.vue'

const assets = ref<Asset[]>([])
const fonts = ref<FontFace[]>([])
const screens = ref<Screen[]>([])
const loading = ref(false)
const err = ref('')
const hint = ref('')

function errorText(e: unknown): string {
  return e instanceof ApiError ? `${e.status} ${e.message}` : String(e)
}

async function load(): Promise<void> {
  const project = currentProject.value
  err.value = ''
  if (!project) {
    assets.value = []
    fonts.value = []
    screens.value = []
    return
  }
  loading.value = true
  try {
    const [a, f, s] = await Promise.all([api.listAssets(project.id), api.listFonts(project.id), api.listScreens(project.id)])
    assets.value = a
    fonts.value = f
    screens.value = s
  } catch (e) {
    err.value = errorText(e)
  } finally {
    loading.value = false
  }
}

onMounted(() => void load())

/* ------------------------------- 资产表单 ------------------------------- */
const assetForm = ref({ code: '', name: '', kind: 'logo', svgBody: '', license: 'Owned' })
const assetErr = ref('')
const assetBusy = ref(false)

/** SVG 里烤死色值 = 换肤失效，所以提交前先拦一道（后端不猜，界面也不假装能改） */
const assetSvgHasHex = computed(() => /#[0-9a-f]{3,8}/i.test(assetForm.value.svgBody))

async function submitAsset(): Promise<void> {
  const project = currentProject.value
  assetErr.value = ''
  if (!project) return
  if (!assetForm.value.code.trim()) {
    assetErr.value = '资产 code 必填（项目内唯一键，同码即覆盖）。'
    return
  }
  if (!assetForm.value.svgBody.trim()) {
    assetErr.value = 'svgBody 必填：这里只登记图形本体（不含 <svg> 外层），颜色一律用 currentColor。'
    return
  }
  if (assetSvgHasHex.value) {
    assetErr.value = 'SVG 里出现了烤死的色值（#xxx）。请改成 currentColor，颜色交给 semantic.* 令牌，否则换肤对它无效。'
    return
  }
  assetBusy.value = true
  try {
    await api.saveAsset(project.id, {
      code: assetForm.value.code.trim(),
      name: assetForm.value.name.trim() || null,
      kind: assetForm.value.kind,
      svgBody: assetForm.value.svgBody.trim(),
      license: assetForm.value.license.trim() || null,
    })
    hint.value = `资产 ${assetForm.value.code.trim()} 已写入，正在回读列表自证。`
    assetForm.value = { code: '', name: '', kind: 'logo', svgBody: '', license: 'Owned' }
    await load()
  } catch (e) {
    assetErr.value = errorText(e)
  } finally {
    assetBusy.value = false
  }
}

/* ------------------------------- 字体表单 ------------------------------- */
const fontForm = ref({ family: '', weight: '400', style: 'normal', role: 'sans', license: '' })
const fontErr = ref('')
const fontBusy = ref(false)
const fontLicenses = computed(() => [...new Set(fonts.value.map((f) => f.license || '未标注'))].sort())

async function submitFont(): Promise<void> {
  const project = currentProject.value
  fontErr.value = ''
  if (!project) return
  if (!fontForm.value.family.trim()) {
    fontErr.value = '字族名必填（如 Source Han Sans）。'
    return
  }
  if (!fontForm.value.license.trim()) {
    fontErr.value = '许可证必填：可分发的字体要写真实许可证（如 OFL-1.1）；系统字体栈成员请写"系统提供，不分发"。留空等于埋合规雷。'
    return
  }
  fontBusy.value = true
  try {
    await api.saveFont(project.id, {
      family: fontForm.value.family.trim(),
      weight: Number.parseInt(fontForm.value.weight, 10) || 400,
      style: fontForm.value.style,
      role: fontForm.value.role,
      license: fontForm.value.license.trim(),
    })
    hint.value = `字体 ${fontForm.value.family.trim()} 已登记，正在回读自证。`
    fontForm.value = { family: '', weight: '400', style: 'normal', role: 'sans', license: '' }
    await load()
  } catch (e) {
    fontErr.value = errorText(e)
  } finally {
    fontBusy.value = false
  }
}

/* ------------------------------- 页面表单 ------------------------------- */
const screenForm = ref({ code: '', title: '', route: '', iconCode: 'dashboard' })
const screenErr = ref('')
const screenBusy = ref(false)

async function submitScreen(): Promise<void> {
  const project = currentProject.value
  screenErr.value = ''
  if (!project) return
  if (!screenForm.value.code.trim() || !screenForm.value.route.trim()) {
    screenErr.value = '页面 code 与 route 都必填 —— 没有路由的"页面"等于没登记。'
    return
  }
  screenBusy.value = true
  try {
    await api.saveScreen(project.id, {
      code: screenForm.value.code.trim(),
      title: screenForm.value.title.trim() || null,
      route: screenForm.value.route.trim(),
      iconCode: screenForm.value.iconCode.trim() || null,
    })
    hint.value = `页面 ${screenForm.value.code.trim()} 已登记，正在回读自证。`
    screenForm.value = { code: '', title: '', route: '', iconCode: 'dashboard' }
    await load()
  } catch (e) {
    screenErr.value = errorText(e)
  } finally {
    screenBusy.value = false
  }
}

const projectAssets = computed(() => assets.value.filter((a) => a.projectId === (currentProject.value?.id ?? -1)))
const counts = computed(() => `资产 ${assets.value.length} · 字体 ${fonts.value.length} · 页面 ${screens.value.length}`)
</script>

<template>
  <section class="ba">
    <header class="ba__bar">
      <div>
        <h2 class="ds-h2">品牌资产</h2>
        <span class="ds-micro">{{ counts }} · 数据来自后端库（generate 会落种子，这里可改可加）</span>
      </div>
      <button class="ds-btn ds-btn--ghost" type="button" :disabled="loading || !currentProject" @click="load">重新读取</button>
    </header>

    <PanelState v-if="unauthorized" state="unauthorized" />
    <PanelState v-else-if="!currentProject" state="empty" title="还没有选中设计系统项目" hint="资产/字体/页面都按项目登记；先在顶部选择一个项目。" />

    <template v-else>
      <p v-if="err" class="ba__err" role="alert">后端原文：{{ err }}</p>
      <p v-if="hint" class="ba__hint">{{ hint }}</p>

      <!-- 资产 -->
      <div class="ba__panel ds-surface">
        <h3 class="ds-h3">资产（logo / 母题）</h3>
        <p class="ds-micro">生成器已落 {{ projectAssets.length }} 条种子资产；SVG 只存图形本体，颜色交给 currentColor。</p>
        <ul v-if="assets.length" class="ba__list">
          <li v-for="a in assets" :key="a.id" class="ba__row">
            <span class="ba__mark" aria-hidden="true">
              <svg viewBox="0 0 24 24" width="24" height="24" v-html="a.svgBody ?? ''" />
            </span>
            <span class="ds-mono ba__code">{{ a.code }}</span>
            <span class="ds-micro">{{ a.kind }}</span>
            <span class="ba__lic">{{ a.license || '未标注' }}</span>
            <span class="ds-small ba__desc">{{ a.description }}</span>
          </li>
        </ul>
        <PanelState v-else state="empty" title="无资产" hint="后端 listAssets 未返回内容：可能项目是旧的，重新生成一次即可拿到种子资产。" />

        <div class="ba__form">
          <label class="ba__field"><span class="ds-micro">code *</span><input v-model="assetForm.code" class="ds-input" /></label>
          <label class="ba__field"><span class="ds-micro">name</span><input v-model="assetForm.name" class="ds-input" /></label>
          <label class="ba__field"><span class="ds-micro">kind</span>
            <select v-model="assetForm.kind" class="ds-input">
              <option value="logo">logo</option>
              <option value="motif">motif</option>
              <option value="illustration">illustration</option>
            </select>
          </label>
          <label class="ba__field"><span class="ds-micro">license</span><input v-model="assetForm.license" class="ds-input" /></label>
          <label class="ba__field ba__field--wide"><span class="ds-micro">svgBody *（不含 &lt;svg&gt; 外层）</span>
            <textarea v-model="assetForm.svgBody" class="ds-input ba__area" rows="2" placeholder='<circle cx="12" cy="12" r="9" fill="currentColor"/>' />
          </label>
          <p v-if="assetErr" class="ba__err" role="alert">{{ assetErr }}</p>
          <button class="ds-btn" type="button" :disabled="assetBusy || !currentProject" @click="submitAsset">{{ assetBusy ? '写入中…' : '保存资产' }}</button>
        </div>
      </div>

      <!-- 字体 -->
      <div class="ba__panel ds-surface">
        <h3 class="ds-h3">字体登记</h3>
        <p class="ds-micro">许可证分布：{{ fontLicenses.join(' / ') || '—' }}（生成器种的是系统字体栈成员，明确标注"不随产物分发"）</p>
        <table v-if="fonts.length" class="ba__table">
          <thead><tr><th>字族</th><th>role</th><th>weight</th><th>style</th><th>display</th><th>许可证</th></tr></thead>
          <tbody>
            <tr v-for="f in fonts" :key="f.id">
              <td class="ds-mono">{{ f.family }}</td>
              <td>{{ f.role || '—' }}</td>
              <td>{{ f.weight }}</td>
              <td>{{ f.style }}</td>
              <td>{{ f.display || '—' }}</td>
              <td class="ds-small">{{ f.license || '未标注' }}</td>
            </tr>
          </tbody>
        </table>
        <PanelState v-else state="empty" title="无字体登记" hint="listFonts 未返回内容；重新生成或手工登记一条。" />

        <div class="ba__form">
          <label class="ba__field"><span class="ds-micro">family *</span><input v-model="fontForm.family" class="ds-input" /></label>
          <label class="ba__field"><span class="ds-micro">weight</span><input v-model="fontForm.weight" class="ds-input ds-input--narrow" /></label>
          <label class="ba__field"><span class="ds-micro">style</span>
            <select v-model="fontForm.style" class="ds-input"><option value="normal">normal</option><option value="italic">italic</option></select>
          </label>
          <label class="ba__field"><span class="ds-micro">role</span>
            <select v-model="fontForm.role" class="ds-input"><option value="sans">sans</option><option value="mono">mono</option><option value="display">display</option></select>
          </label>
          <label class="ba__field ba__field--wide"><span class="ds-micro">license *（可分发写真实许可证；系统字体写"系统提供，不分发"）</span>
            <input v-model="fontForm.license" class="ds-input" />
          </label>
          <p v-if="fontErr" class="ba__err" role="alert">{{ fontErr }}</p>
          <button class="ds-btn" type="button" :disabled="fontBusy || !currentProject" @click="submitFont">{{ fontBusy ? '写入中…' : '登记字体' }}</button>
        </div>
      </div>

      <!-- 页面清单 -->
      <div class="ba__panel ds-surface">
        <h3 class="ds-h3">页面清单</h3>
        <p class="ds-micro">generate 按行业倾向给了起手屏（描述里写明是建议，不是产品事实）；这里可增可改。</p>
        <ul v-if="screens.length" class="ba__list">
          <li v-for="s in screens" :key="s.id" class="ba__row">
            <span class="ds-mono ba__code">{{ s.code }}</span>
            <span class="ds-small">{{ s.title }}</span>
            <span class="ba__lic">{{ s.route }}</span>
            <span class="ds-micro">图标 <span class="ds-mono">{{ s.iconCode || '—' }}</span></span>
          </li>
        </ul>
        <PanelState v-else state="empty" title="无页面清单" hint="listScreens 未返回内容；重新生成或手工登记一条。" />

        <div class="ba__form">
          <label class="ba__field"><span class="ds-micro">code *</span><input v-model="screenForm.code" class="ds-input" /></label>
          <label class="ba__field"><span class="ds-micro">title</span><input v-model="screenForm.title" class="ds-input" /></label>
          <label class="ba__field"><span class="ds-micro">route *</span><input v-model="screenForm.route" class="ds-input" placeholder="/overview" /></label>
          <label class="ba__field"><span class="ds-micro">iconCode</span><input v-model="screenForm.iconCode" class="ds-input" /></label>
          <p v-if="screenErr" class="ba__err" role="alert">{{ screenErr }}</p>
          <button class="ds-btn" type="button" :disabled="screenBusy || !currentProject" @click="submitScreen">{{ screenBusy ? '写入中…' : '登记页面' }}</button>
        </div>
      </div>
    </template>
  </section>
</template>

<style scoped>
.ba {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-5);
}
.ba__bar {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: var(--ds-space-4);
  flex-wrap: wrap;
}
.ba__panel {
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-3);
  padding: var(--ds-space-5);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-lg);
}
.ba__list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: var(--ds-space-2);
}
.ba__row {
  display: flex;
  align-items: center;
  gap: var(--ds-space-3);
  flex-wrap: wrap;
  padding: var(--ds-space-2) 0;
  border-bottom: 1px solid var(--ds-border-1);
}
.ba__mark {
  display: inline-flex;
  color: var(--ds-color-primary);
}
.ba__code {
  font-size: var(--ds-fs-small);
}
.ba__lic {
  padding: 1px 8px;
  border-radius: var(--ds-radius-pill);
  border: 1px solid var(--ds-border-1);
  background: var(--ds-surface-2);
  font-size: var(--ds-fs-micro);
}
.ba__desc {
  color: var(--ds-fg-3);
}
.ba__table {
  inline-size: 100%;
  border-collapse: collapse;
  font-size: var(--ds-fs-small);
}
.ba__table th {
  text-align: start;
  padding: var(--ds-space-2);
  border-bottom: 1px solid var(--ds-border-1);
  color: var(--ds-fg-3);
  font-weight: 600;
}
.ba__table td {
  padding: var(--ds-space-2);
  border-bottom: 1px solid var(--ds-border-1);
}
.ba__form {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: var(--ds-space-3);
  align-items: end;
  padding-top: var(--ds-space-3);
  border-top: 1px dashed var(--ds-border-2);
}
.ba__field {
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.ba__field--wide {
  grid-column: 1 / -1;
}
.ba__area {
  resize: vertical;
  font-family: var(--ds-font-mono);
  font-size: var(--ds-fs-small);
}
.ba__err {
  grid-column: 1 / -1;
  margin: 0;
  color: var(--ds-danger);
  font-size: var(--ds-fs-small);
}
.ba__hint {
  margin: 0;
  color: var(--ds-success);
  font-size: var(--ds-fs-small);
}
/* 表单控件：与其余 section 同名同值的局部类（scoped 不跨组件泄漏，必须本段自带） */
.ds-input {
  font: inherit;
  font-size: var(--ds-fs-small);
  color: var(--ds-fg-1);
  background: var(--ds-surface-1);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-sm);
  padding: 6px var(--ds-space-3);
  min-width: 0;
}
.ds-input--narrow {
  width: auto;
}
.ds-btn {
  font: inherit;
  font-size: var(--ds-fs-small);
  color: var(--ds-fg-1);
  background: var(--ds-surface-2);
  border: 1px solid var(--ds-border-1);
  border-radius: var(--ds-radius-sm);
  padding: 6px var(--ds-space-4);
  cursor: pointer;
}
.ds-btn--ghost {
  background: transparent;
  color: var(--ds-fg-2);
}
.ds-btn:disabled,
.ds-input:disabled {
  opacity: 0.45;
  cursor: not-allowed;
}
</style>
