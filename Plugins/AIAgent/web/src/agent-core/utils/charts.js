/**
 * 内置轻量 SVG 图表渲染器 —— 零依赖，不打包任何图表库。
 *
 * 为什么内置一套？
 *   大屏页面通常已经全局加载了 ECharts，而管理后台和独立页面往往没有。
 *   内置这套保证"开箱即用"；大屏想复用宿主已有的 ECharts，
 *   只需传 config.chartRenderer(el, option) 覆盖即可，不用改组件代码。
 *
 * 配色走 CSS 变量，因此三种主题（admin / page / bigscreen）自动适配。
 *
 * v1.4.1 修复（窄面板下标签被裁）：
 *   1. 柱宽自适应   —— 数据点多时不再因 min-width 撑出右边界
 *   2. 标签抽稀     —— 按可用宽度算出最多能放几个，超出按步长跳过
 *   3. 首尾锚点钳制 —— 线性图首尾标签改为 start/end 对齐，不再切掉半个字
 *   4. 专用标签带   —— padB 固定留出标签高度，不再贴着 viewBox 底边
 *   5. 省略号截断   —— 超长名称用 … 收尾，而不是静默截掉后半段
 */

const PALETTE = [
  'var(--aiw-primary)',
  'var(--aiw-c2, #0ea5e9)',
  'var(--aiw-c3, #f59e0b)',
  'var(--aiw-c4, #10b981)',
  'var(--aiw-c5, #ef4444)',
  'var(--aiw-c6, #8b5cf6)',
]

const esc = (s) =>
  String(s ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;')

/** 归一化：支持 [{name,value}] 或 {labels:[], values:[]} 或纯数字数组 */
function normalize(option) {
  let data = option.data ?? option.values ?? []
  if (Array.isArray(data) && data.length && typeof data[0] === 'number') {
    data = data.map((v, i) => ({ name: String(i + 1), value: v }))
  }
  if (!Array.isArray(data) || !data.length) {
    const labels = option.labels || option.xAxis || []
    const values = option.values || []
    data = labels.map((n, i) => ({ name: n, value: values[i] ?? 0 }))
  }
  return data.map((d, i) => ({
    name: String(d.name ?? d.label ?? i + 1),
    value: Number(d.value ?? d.count ?? 0),
    color: d.color,
  }))
}

const fmt = (n) => (Math.abs(n) >= 10000 ? (n / 1000).toFixed(1) + 'k' : String(Math.round(n * 100) / 100))

/** 超长名称用省略号收尾（9.5px 字号下，一个汉字约占 9.5px） */
function ellipsis(text, maxChars) {
  const t = String(text)
  return t.length > maxChars ? t.slice(0, Math.max(1, maxChars - 1)) + '…' : t
}

export function renderChart(option = {}) {
  const data = normalize(option)
  if (!data.length) return '<div class="aiw-chart__empty">无数据</div>'

  const type = option.type || 'bar'
  const W = 320
  const H = option.height || 150
  // 左右留白加大：首尾标签与柱子不再顶到边框
  const padL = 12
  const padR = 12
  const padT = 12
  // 专用标签带：标签画在 y = padT + ih + 15，永远落在 viewBox 内部
  const labelBand = 26
  const padB = labelBand
  const iw = W - padL - padR
  const ih = Math.max(20, H - padT - padB)
  const max = Math.max(...data.map((d) => d.value), 0) || 1
  const min = Math.min(...data.map((d) => d.value), 0)
  const span = max - min || 1
  const color = (d, i) => d.color || PALETTE[i % PALETTE.length]

  const isPie = type === 'pie' || type === 'donut'

  /* ------------------------------------------------------------------
     几何：一次性算好每个数据点的锚点 x，柱状与标签共用，避免两套公式跑偏
     ------------------------------------------------------------------ */
  const n = data.length
  const anchors = (() => {
    if (isPie) return []
    if (type === 'bar') {
      // 关键修复：slot 固定为 iw/n，gap 随之收缩，总宽永远 = iw
      const slot = iw / n
      const gap = Math.min(6, slot * 0.25)
      return data.map((_, i) => ({
        x: padL + i * slot + slot / 2,
        left: padL + i * slot,
        w: Math.max(2, slot - gap),
      }))
    }
    const step = n > 1 ? iw / (n - 1) : 0
    return data.map((_, i) => ({ x: padL + (n > 1 ? i * step : iw / 2) }))
  })()

  let body = ''

  if (isPie) {
    const cx = W / 2
    const cy = H / 2 + 4
    const R = Math.min(iw, ih) / 2 + 4
    const inner = type === 'donut' ? R * 0.58 : 0
    const total = data.reduce((s, d) => s + Math.abs(d.value), 0) || 1
    let a0 = -Math.PI / 2
    body = data
      .map((d, i) => {
        const a1 = a0 + (Math.abs(d.value) / total) * Math.PI * 2
        const large = a1 - a0 > Math.PI ? 1 : 0
        const x0 = cx + R * Math.cos(a0)
        const y0 = cy + R * Math.sin(a0)
        const x1 = cx + R * Math.cos(a1)
        const y1 = cy + R * Math.sin(a1)
        let dpath
        if (inner) {
          const x2 = cx + inner * Math.cos(a1)
          const y2 = cy + inner * Math.sin(a1)
          const x3 = cx + inner * Math.cos(a0)
          const y3 = cy + inner * Math.sin(a0)
          dpath = `M${x0} ${y0}A${R} ${R} 0 ${large} 1 ${x1} ${y1}L${x2} ${y2}A${inner} ${inner} 0 ${large} 0 ${x3} ${y3}Z`
        } else {
          dpath = `M${cx} ${cy}L${x0} ${y0}A${R} ${R} 0 ${large} 1 ${x1} ${y1}Z`
        }
        a0 = a1
        return `<path d="${dpath}" fill="${color(d, i)}" opacity="0.92"><title>${esc(d.name)}: ${fmt(d.value)}</title></path>`
      })
      .join('')
    body += `<text x="${cx}" y="${cy + 4}" text-anchor="middle" class="aiw-chart__ctr">${esc(option.centerLabel || fmt(total))}</text>`
  } else if (type === 'line' || type === 'area') {
    const pts = data.map((d, i) => {
      const y = padT + ih - ((d.value - min) / span) * ih
      return [anchors[i].x, y]
    })
    const line = pts.map((p) => `${p[0].toFixed(1)},${p[1].toFixed(1)}`).join(' ')
    if (type === 'area') {
      body += `<polygon points="${anchors[0].x.toFixed(1)},${padT + ih} ${line} ${anchors[n - 1].x.toFixed(1)},${padT + ih}" fill="var(--aiw-primary-16)" />`
    }
    for (let g = 0; g <= 3; g++) {
      const y = padT + (ih / 3) * g
      body += `<line x1="${padL}" y1="${y}" x2="${padL + iw}" y2="${y}" class="aiw-chart__grid" />`
    }
    body += `<polyline points="${line}" fill="none" stroke="var(--aiw-primary)" stroke-width="2.2" stroke-linejoin="round" stroke-linecap="round" />`
    body += pts
      .map(
        (p, i) =>
          `<circle cx="${p[0].toFixed(1)}" cy="${p[1].toFixed(1)}" r="3" fill="var(--aiw-bg)" stroke="${color(data[i], i)}" stroke-width="2"><title>${esc(data[i].name)}: ${fmt(data[i].value)}</title></circle>`
      )
      .join('')
  } else {
    // bar（默认）
    body = data
      .map((d, i) => {
        const h = Math.max(2, ((d.value - Math.min(min, 0)) / span) * ih)
        const { left, w } = anchors[i]
        const y = padT + ih - h
        return `<rect x="${left.toFixed(1)}" y="${y.toFixed(1)}" width="${w.toFixed(1)}" height="${h.toFixed(1)}" rx="3" fill="${color(d, i)}" opacity="0.9"><title>${esc(d.name)}: ${fmt(d.value)}</title></rect>`
      })
      .join('')
  }

  /* ------------------------------------------------------------------
     x 轴标签：抽稀 + 首尾钳制 + 省略号，保证不被裁切、不互相压字
     ------------------------------------------------------------------ */
  let labels = ''
  if (!isPie) {
    const fontSize = 9.5
    const charW = fontSize * 1.05          // 汉字近似宽度
    const gapPx = 6                        // 相邻标签之间至少留的缝
    const maxChars = typeof option.labelMaxChars === 'number' ? option.labelMaxChars : 8

    // 每个标签实际占多宽（按截断后的字符数算），据此决定能放几个
    const shownWidth = (name) => ellipsis(name, maxChars).length * charW
    // 末尾标签恒定保留，其余标签不得侵入它的领地（否则相邻长名会互相压字）
    const endGuard = anchors[n - 1].x - shownWidth(data[n - 1].name) / 2 - gapPx
    const avgW = Math.max(14, data.reduce((s, d) => s + shownWidth(d.name), 0) / n)
    const capacity = typeof option.maxLabels === 'number' ? option.maxLabels : Math.max(2, Math.floor(iw / (avgW + gapPx)))
    const step = Math.max(1, Math.ceil(n / capacity))
    const labelY = padT + ih + 15

    let lastRight = -Infinity
    labels = data
      .map((d, i) => {
        const isLast = i === n - 1
        // 抽稀：按步长取点，但最后一点永远保留（否则右端没有刻度感）
        if (i % step !== 0 && !isLast) return ''
        let x = anchors[i].x
        let anchor = 'middle'
        const half = shownWidth(d.name) / 2
        // 防重叠：与前一个已绘制标签的右边界比，会压字就跳过（最后一点保底保留）
        if (!isLast && (x - half < lastRight + gapPx || x + half > endGuard)) return ''
        // 首尾钳制：靠边的标签改用 start / end 对齐，避免半个字被切
        if (x - half < 2) {
          x = 2
          anchor = 'start'
        } else if (x + half > W - 2) {
          x = W - 2
          anchor = 'end'
        }
        lastRight = Math.max(lastRight, x + half)
        const rotate = option.labelRotate ? ` transform="rotate(${option.labelRotate} ${x.toFixed(1)} ${labelY})"` : ''
        return `<text x="${x.toFixed(1)}" y="${labelY}" text-anchor="${anchor}" class="aiw-chart__lbl"${rotate}>${esc(ellipsis(d.name, maxChars))}</text>`
      })
      .join('')
  }

  /* 无障碍（v1.11.0）：role="img" 却没有可读名，读屏只会念「图片」。
     把图表类型与数据摘要写进 aria-label 与 <title>，不改变任何视觉。 */
  const TYPE_NAME = { bar: '柱状图', line: '折线图', area: '面积图', pie: '饼图', donut: '环形图', scatter: '散点图' }
  const series = Array.isArray(option.data) ? option.data : []
  const chartLabel =
    (option.title ? option.title + '，' : '') +
    (TYPE_NAME[option.type] || '图表') + '：' +
    series.slice(0, 12).map((d) => (d && d.name != null ? d.name + ' ' + d.value : String(d))).join('，')

  return (
    `<svg class="aiw-chart__svg" viewBox="0 0 ${W} ${H}" preserveAspectRatio="xMidYMid meet" role="img" aria-label="${esc(chartLabel)}"><title>${esc(chartLabel)}</title>` +
    (option.showGrid !== false && !isPie
      ? Array.from({ length: 4 }, (_, g) => `<line x1="${padL}" y1="${padT + (ih / 3) * g}" x2="${padL + iw}" y2="${padT + (ih / 3) * g}" class="aiw-chart__grid" />`).join('')
      : '') +
    body +
    labels +
    '</svg>'
  )
}

export default renderChart
