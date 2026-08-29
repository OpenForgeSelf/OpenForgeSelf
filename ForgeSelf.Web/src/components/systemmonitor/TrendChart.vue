<script setup lang="ts">
import { ref, onMounted, onUnmounted, watch, nextTick } from 'vue'

export interface ChartDataset {
  label: string
  data: { value: number; timestamp: number }[]
  color: string
  fill?: boolean
}

const props = withDefaults(defineProps<{
  datasets: ChartDataset[]
  title?: string
  height?: number
  yAxisLabel?: string
  showLegend?: boolean
  formatValue?: (value: number) => string
}>(), {
  title: '',
  height: 200,
  yAxisLabel: '',
  showLegend: false,
  formatValue: (value: number) => String(value)
})

const canvasRef = ref<HTMLCanvasElement | null>(null)
const containerRef = ref<HTMLDivElement | null>(null)
let animationFrameId: number | null = null
let dpr = 1

const defaultFormatValue = (value: number): string => value.toFixed(1)

function getFormatValue(value: number): string {
  return props.formatValue ? props.formatValue(value) : defaultFormatValue(value)
}

function getMinMax(datasets: ChartDataset[]): { min: number; max: number } {
  let min = Infinity
  let max = -Infinity

  for (const dataset of datasets) {
    for (const point of dataset.data) {
      if (point.value < min) min = point.value
      if (point.value > max) max = point.value
    }
  }

  if (min === Infinity) {
    min = 0
    max = 100
  }

  if (min === max) {
    min = 0
    max = Math.max(max, 1)
  }

  const range = max - min
  min = Math.max(0, min - range * 0.1)
  max = max + range * 0.1

  return { min, max }
}

function drawChart(): void {
  const canvas = canvasRef.value
  const container = containerRef.value
  if (!canvas || !container) return

  const ctx = canvas.getContext('2d')
  if (!ctx) return

  const rect = container.getBoundingClientRect()
  const width = rect.width
  const height = props.height

  dpr = window.devicePixelRatio || 1
  canvas.width = width * dpr
  canvas.height = height * dpr
  canvas.style.width = width + 'px'
  canvas.style.height = height + 'px'
  ctx.scale(dpr, dpr)

  const padding = { top: 20, right: 20, bottom: 30, left: 50 }
  const chartWidth = width - padding.left - padding.right
  const chartHeight = height - padding.top - padding.bottom

  ctx.clearRect(0, 0, width, height)

  const { min, max } = getMinMax(props.datasets)

  drawGrid(ctx, padding, chartWidth, chartHeight)
  drawYAxis(ctx, padding, chartHeight, min, max)
  drawXAxis(ctx, padding, chartWidth, chartHeight)

  for (const dataset of props.datasets) {
    drawLine(ctx, dataset, padding, chartWidth, chartHeight, min, max)
  }

  if (props.showLegend) {
    drawLegend(ctx, width, padding)
  }
}

function drawGrid(
  ctx: CanvasRenderingContext2D,
  padding: { top: number; right: number; bottom: number; left: number },
  chartWidth: number,
  chartHeight: number
): void {
  ctx.strokeStyle = '#f1f3f5'
  ctx.lineWidth = 1

  const gridLines = 5
  for (let i = 0; i <= gridLines; i++) {
    const y = padding.top + (chartHeight / gridLines) * i
    ctx.beginPath()
    ctx.moveTo(padding.left, y)
    ctx.lineTo(padding.left + chartWidth, y)
    ctx.stroke()
  }
}

function drawYAxis(
  ctx: CanvasRenderingContext2D,
  padding: { top: number; right: number; bottom: number; left: number },
  chartHeight: number,
  min: number,
  max: number
): void {
  ctx.fillStyle = '#6c757d'
  ctx.font = '11px -apple-system, BlinkMacSystemFont, sans-serif'
  ctx.textAlign = 'right'
  ctx.textBaseline = 'middle'

  const gridLines = 5
  for (let i = 0; i <= gridLines; i++) {
    const value = max - ((max - min) / gridLines) * i
    const y = padding.top + (chartHeight / gridLines) * i
    ctx.fillText(getFormatValue(value), padding.left - 8, y)
  }
}

function drawXAxis(
  ctx: CanvasRenderingContext2D,
  padding: { top: number; right: number; bottom: number; left: number },
  chartWidth: number,
  chartHeight: number
): void {
  ctx.fillStyle = '#6c757d'
  ctx.font = '11px -apple-system, BlinkMacSystemFont, sans-serif'
  ctx.textAlign = 'center'
  ctx.textBaseline = 'top'

  const firstDataset = props.datasets[0]
  if (!firstDataset || firstDataset.data.length === 0) return

  const data = firstDataset.data
  const timeRange = data.length > 1
    ? data[data.length - 1].timestamp - data[0].timestamp
    : 60000

  const labels = 5
  for (let i = 0; i <= labels; i++) {
    const x = padding.left + (chartWidth / labels) * i
    const timestamp = data[0]?.timestamp || 0
    const time = timestamp + (timeRange / labels) * i
    const date = new Date(time)
    const timeStr = `${date.getHours().toString().padStart(2, '0')}:${date.getMinutes().toString().padStart(2, '0')}:${date.getSeconds().toString().padStart(2, '0')}`
    ctx.fillText(timeStr, x, padding.top + chartHeight + 6)
  }
}

function drawLine(
  ctx: CanvasRenderingContext2D,
  dataset: ChartDataset,
  padding: { top: number; right: number; bottom: number; left: number },
  chartWidth: number,
  chartHeight: number,
  min: number,
  max: number
): void {
  const data = dataset.data
  if (data.length < 2) return

  const range = max - min
  const firstTs = data[0].timestamp
  const lastTs = data[data.length - 1].timestamp
  const timeRange = lastTs - firstTs || 1

  ctx.strokeStyle = dataset.color
  ctx.lineWidth = 2
  ctx.lineJoin = 'round'
  ctx.lineCap = 'round'

  if (dataset.fill) {
    const gradient = ctx.createLinearGradient(0, padding.top, 0, padding.top + chartHeight)
    gradient.addColorStop(0, dataset.color + '40')
    gradient.addColorStop(1, dataset.color + '05')
    ctx.fillStyle = gradient
    ctx.beginPath()
    ctx.moveTo(padding.left, padding.top + chartHeight)
    for (let i = 0; i < data.length; i++) {
      const x = padding.left + ((data[i].timestamp - firstTs) / timeRange) * chartWidth
      const y = padding.top + chartHeight - ((data[i].value - min) / range) * chartHeight
      ctx.lineTo(x, y)
    }
    ctx.lineTo(padding.left + chartWidth, padding.top + chartHeight)
    ctx.closePath()
    ctx.fill()
  }

  ctx.beginPath()
  for (let i = 0; i < data.length; i++) {
    const x = padding.left + ((data[i].timestamp - firstTs) / timeRange) * chartWidth
    const y = padding.top + chartHeight - ((data[i].value - min) / range) * chartHeight
    if (i === 0) {
      ctx.moveTo(x, y)
    } else {
      ctx.lineTo(x, y)
    }
  }
  ctx.stroke()
}

function drawLegend(
  ctx: CanvasRenderingContext2D,
  _width: number,
  padding: { top: number; right: number; bottom: number; left: number }
): void {
  ctx.font = '12px -apple-system, BlinkMacSystemFont, sans-serif'
  let x = padding.left
  const y = 6

  for (const dataset of props.datasets) {
    ctx.fillStyle = dataset.color
    ctx.fillRect(x, y, 12, 12)
    ctx.fillStyle = '#495057'
    ctx.textAlign = 'left'
    ctx.textBaseline = 'top'
    x += 18
    ctx.fillText(dataset.label, x, y - 1)
    x += ctx.measureText(dataset.label).width + 16
  }
}

function scheduleDraw(): void {
  if (animationFrameId !== null) {
    cancelAnimationFrame(animationFrameId)
  }
  animationFrameId = requestAnimationFrame(() => {
    drawChart()
    animationFrameId = null
  })
}

function handleResize(): void {
  nextTick(() => {
    scheduleDraw()
  })
}

watch(
  () => props.datasets.map(d => d.data.length),
  () => {
    scheduleDraw()
  },
  { deep: true }
)

onMounted(() => {
  nextTick(() => {
    drawChart()
    window.addEventListener('resize', handleResize)
  })
})

onUnmounted(() => {
  if (animationFrameId !== null) {
    cancelAnimationFrame(animationFrameId)
  }
  window.removeEventListener('resize', handleResize)
})
</script>

<template>
  <div ref="containerRef" class="trend-chart">
    <div v-if="title" class="chart-title">{{ title }}</div>
    <canvas ref="canvasRef" role="img" :aria-label="title || '趋势图表'" />
  </div>
</template>

<style scoped>
.trend-chart {
  background: #fff;
  border-radius: 12px;
  padding: 16px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);
}

.chart-title {
  font-size: 15px;
  font-weight: 600;
  color: #212529;
  margin-bottom: 12px;
}

canvas {
  display: block;
  width: 100%;
}
</style>
