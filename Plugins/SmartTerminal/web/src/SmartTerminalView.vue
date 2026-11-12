<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import './styles.css'
import { api, SESSIONS_PATH, asList } from './api'
import { ApiError } from './http'
import { classifyRisk, decide, hardRejectReason } from './classify'
import { loadRestore, saveRestore, RESTORED_TOAST } from './restore'
import {
  FIXTURE_CHAT,
  FIXTURE_HISTORY,
  FIXTURE_PROFILES,
  FIXTURE_SECRETS,
  FIXTURE_SESSIONS,
  FIXTURE_SHELLS,
  FIXTURE_SNIPPETS,
  systemBlock,
} from './fixtures'
import { historyLines } from './history'
import { scrubLaunchParamsFromUrl } from './launchFromQuery'
import type {
  Approval,
  AuthMethod,
  Block,
  ChatMessage,
  CommandSource,
  CreateSessionBody,
  PermissionMode,
  Profile,
  RailTab,
  SecretMeta,
  Session,
  SessionKind,
  SessionStatus,
  ShellInfo,
  Snippet,
  StoryId,
} from './types'

const props = defineProps<{ story?: StoryId }>()
const isFixture = computed(() => !!props.story)

interface ViewSession extends Session {
  draft: string
  history: string[]
  histIndex: number
  blocks: Block[]
  permissionMode: PermissionMode
}

const pageState = ref<'loading' | 'ready' | 'empty' | 'error' | 'unauthorized'>('loading')
const errorSummary = ref('')
const profiles = ref<Profile[]>([])
const shells = ref<ShellInfo[]>([])
const secrets = ref<SecretMeta[]>([])
const snippets = ref<Snippet[]>([])
const sessions = ref<ViewSession[]>([])
const activeId = ref<string | null>(null)
const railOpen = ref(false)
const railTab = ref<RailTab>('ai')
const selectedBlockId = ref<string | null>(null)
const chats = ref<Record<string, ChatMessage[]>>({})
const chatDraft = ref('')
const thinking = ref(false)
const toastText = ref('')
const toastTestId = ref<string | undefined>()
const profileQuery = ref('')
const historyQuery = ref('')
const profileFilter = ref<'all' | 'local' | 'ssh'>('all')
const suggestOn = ref(false)
const permMenu = ref(false)
const paletteOpen = ref(false)
const paletteQuery = ref('')
const paletteIndex = ref(0)
const showNew = ref(false)
const newKind = ref<SessionKind>('local')
const newTitle = ref('')
const newShellId = ref('bash')
const newCwd = ref('~')
const newHost = ref('')
const newPort = ref(22)
const newUser = ref('')
const newAuth = ref<AuthMethod>('key')
const newSecretId = ref('')
const newIdentityFile = ref('')
const newError = ref('')
const passwordOpen = ref(false)
const passwordValue = ref('')
const passwordReveal = ref(false)
const passwordError = ref('')
const passwordCtx = ref<{
  mode: 'create' | 'reconnect'
  user: string
  host: string
  port: number
  body?: CreateSessionBody
  sessionId?: string
} | null>(null)
const approval = ref<Approval | null>(null)
const approvalSource = ref<CommandSource>('prompt')
let toastTimer: ReturnType<typeof setTimeout> | undefined
let pollTimer: ReturnType<typeof setInterval> | undefined
let seq = 100
let persistReady = false

const active = computed(() => sessions.value.find((s) => s.id === activeId.value) ?? null)
const filteredProfiles = computed(() => {
  const q = profileQuery.value.trim().toLowerCase()
  return profiles.value.filter((p) => {
    if (profileFilter.value !== 'all' && p.kind !== profileFilter.value) return false
    if (!q) return true
    const hay = `${p.name} ${profileSummary(p)} ${p.host ?? ''} ${p.user ?? ''}`.toLowerCase()
    return hay.includes(q)
  })
})
const suggestions = computed(() => {
  const s = active.value
  if (!s) return []
  const q = s.draft.trim().toLowerCase()
  if (!q) return []
  const fromHist = s.history.filter((c) => c.toLowerCase().startsWith(q) && c !== s.draft)
  const fromSnip = snippets.value.map((x) => x.command).filter((c) => c.toLowerCase().startsWith(q) && c !== s.draft)
  return Array.from(new Set([...fromHist, ...fromSnip])).slice(0, 6)
})
const chatMessages = computed(() => (active.value ? chats.value[active.value.id] ?? [] : []))
const contextBlock = computed(() => {
  const s = active.value
  if (!s) return null
  if (selectedBlockId.value) return s.blocks.find((b) => b.id === selectedBlockId.value) ?? null
  return [...s.blocks].reverse().find((b) => b.kind !== 'system') ?? null
})
const contextLine = computed(() => {
  const b = contextBlock.value
  if (!b) return '尚未点选命令块，将使用最近一条非系统命令作上下文'
  return `上下文：${b.command}`
})
const promptBusy = computed(() => {
  const s = active.value
  if (!s || s.status !== 'connected') return true
  return s.blocks.some((b) => b.status === 'running')
})

function clone<T>(v: T): T {
  return JSON.parse(JSON.stringify(v)) as T
}

function toView(s: Session, prev?: ViewSession): ViewSession {
  return {
    ...s,
    blocks: s.blocks ? clone(s.blocks) : prev?.blocks ?? [],
    draft: prev?.draft ?? '',
    history: prev?.history ?? [],
    histIndex: prev?.histIndex ?? -1,
    permissionMode: s.permissionMode ?? prev?.permissionMode ?? 'AskWrite',
  }
}

function profileSummary(p: Profile): string {
  if (p.summary) return p.summary
  if (p.kind === 'local') return `${p.shellId ?? 'shell'} · ${p.cwd ?? '~'}`
  const auth = p.auth === 'key' ? '密钥' : '交互密码'
  return `${p.user ?? ''}@${p.host ?? ''}:${p.port ?? 22} · ${auth}`
}

function paneMeta(s: ViewSession): string {
  if (s.kind === 'ssh') return `${s.user ?? ''}@${s.host ?? ''}`
  return `${s.shell ?? 'shell'} · ${s.cwd ?? '~'}`
}

function ps1(s: ViewSession): string {
  if (s.prompt) return s.prompt
  const user = s.user || 'user'
  const host = s.kind === 'ssh' ? s.host || 'host' : 'localhost'
  const cwd = s.cwd || '~'
  return `${user}@${host}:${cwd}$`
}

function showToast(text: string, testId?: string) {
  toastText.value = text
  toastTestId.value = testId
  if (toastTimer) clearTimeout(toastTimer)
  toastTimer = setTimeout(() => {
    toastText.value = ''
    toastTestId.value = undefined
  }, 2800)
}

function bumpPage() {
  if (pageState.value === 'error' || pageState.value === 'unauthorized') return
  pageState.value = sessions.value.length ? 'ready' : 'empty'
  if (pageState.value !== 'ready') railOpen.value = false
}

function persist() {
  if (!persistReady) return
  if (pageState.value === 'loading' || pageState.value === 'error' || pageState.value === 'unauthorized') return
  saveRestore({
    activeId: activeId.value,
    railOpen: railOpen.value,
    railTab: railTab.value,
    tabs: sessions.value.map((s) => ({
      id: s.id,
      title: s.title,
      profileId: s.profileId ?? null,
      kind: s.kind,
      shell: s.shell ?? null,
      cwd: s.cwd ?? null,
      status: s.status,
      host: s.host ?? null,
      port: s.port ?? null,
      user: s.user ?? null,
      auth: s.auth ?? null,
      blocks: s.blocks,
    })),
  })
}

watch([sessions, activeId, railOpen, railTab], persist, { deep: true })

function nid(prefix: string) {
  seq += 1
  return `${prefix}-${seq}`
}

function focusPrompt() {
  void nextTick(() => {
    const el = document.querySelector('[data-testid="st-command-input"]') as HTMLInputElement | null
    el?.focus()
  })
}

function applyStory(id: StoryId) {
  persistReady = false
  profiles.value = clone(FIXTURE_PROFILES)
  shells.value = clone(FIXTURE_SHELLS)
  secrets.value = clone(FIXTURE_SECRETS)
  snippets.value = clone(FIXTURE_SNIPPETS)
  chats.value = {}
  chatDraft.value = ''
  thinking.value = false
  showNew.value = false
  passwordOpen.value = false
  approval.value = null
  paletteOpen.value = false
  selectedBlockId.value = null
  railOpen.value = false
  railTab.value = 'ai'
  errorSummary.value = ''
  newError.value = ''
  passwordError.value = ''
  passwordValue.value = ''

  const take = (...ids: string[]) =>
    ids.map((sid) =>
      toView(clone(FIXTURE_SESSIONS[sid]), {
        ...clone(FIXTURE_SESSIONS[sid]),
        draft: '',
        history: clone(FIXTURE_HISTORY[sid] ?? []),
        histIndex: -1,
        blocks: clone(FIXTURE_SESSIONS[sid].blocks ?? []),
        permissionMode: 'AskWrite',
      }),
    )

  switch (id) {
    case 'S0':
      sessions.value = []
      activeId.value = null
      pageState.value = 'empty'
      break
    case 'S1':
      sessions.value = take('s-local')
      activeId.value = 's-local'
      pageState.value = 'ready'
      break
    case 'S2':
      sessions.value = take('s-local', 's-key', 's-long')
      activeId.value = 's-long'
      pageState.value = 'ready'
      break
    case 'S3':
      sessions.value = []
      activeId.value = null
      pageState.value = 'empty'
      openNew('local')
      break
    case 'S4':
      sessions.value = take('s-key')
      activeId.value = 's-key'
      pageState.value = 'ready'
      break
    case 'S5':
      sessions.value = []
      activeId.value = null
      pageState.value = 'empty'
      openPassword({
        mode: 'create',
        user: 'ops',
        host: '10.9.1.4',
        port: 22,
        body: { kind: 'ssh', title: 'jump-01', host: '10.9.1.4', port: 22, user: 'ops', auth: 'interactive', profileId: 'p-ssh-pass' },
      })
      break
    case 'S6':
      sessions.value = take('s-local')
      activeId.value = 's-local'
      pageState.value = 'ready'
      approval.value = { id: 'ap-write', command: 'sudo nginx -s reload', risk: 'write', mode: 'AskWrite', status: 'pending', sessionId: 's-local' }
      approvalSource.value = 'prompt'
      break
    case 'S7':
      sessions.value = take('s-local')
      activeId.value = 's-local'
      pageState.value = 'ready'
      approval.value = { id: 'ap-high', command: 'rm -rf /tmp/st-demo-orphan', risk: 'high', mode: 'AskHighRisk', status: 'pending', sessionId: 's-local' }
      approvalSource.value = 'prompt'
      break
    case 'S8':
      sessions.value = take('s-pass')
      activeId.value = 's-pass'
      pageState.value = 'ready'
      break
    case 'S9':
      sessions.value = take('s-local')
      activeId.value = 's-local'
      pageState.value = 'ready'
      railOpen.value = true
      railTab.value = 'ai'
      chats.value = { 's-local': clone(FIXTURE_CHAT) }
      break
    case 'S10':
      sessions.value = take('s-local')
      activeId.value = 's-local'
      pageState.value = 'ready'
      railOpen.value = true
      railTab.value = 'snippets'
      break
    case 'S11-loading':
      sessions.value = []
      activeId.value = null
      pageState.value = 'loading'
      break
    case 'S11-error':
      sessions.value = []
      activeId.value = null
      pageState.value = 'error'
      errorSummary.value = `GET ${SESSIONS_PATH} → 503 服务暂不可用`
      break
    case 'S11-401':
      sessions.value = []
      activeId.value = null
      pageState.value = 'unauthorized'
      errorSummary.value = `GET ${SESSIONS_PATH} → 401 Unauthorized`
      break
    case 'S-R':
      sessions.value = take('s-local', 's-key')
      activeId.value = 's-local'
      pageState.value = 'ready'
      railOpen.value = true
      railTab.value = 'ai'
      persistReady = true
      persist()
      showToast(RESTORED_TOAST, 'st-restored')
      break
  }
  persistReady = pageState.value === 'ready' || pageState.value === 'empty'
  if (pageState.value === 'ready') focusPrompt()
}

async function loadLive(fromRetry = false) {
  pageState.value = 'loading'
  errorSummary.value = ''
  try {
    scrubLaunchParamsFromUrl()
    const [sh, pf, sec, sn, ss] = await Promise.all([
      api.shells().catch(() => []),
      api.profiles().catch(() => []),
      api.secrets().catch(() => []),
      api.snippets().catch(() => []),
      api.sessions(),
    ])
    shells.value = sh ?? []
    profiles.value = pf ?? []
    secrets.value = sec ?? []
    snippets.value = sn ?? []
    const live = asList(ss).map((s) => toView(s))
    const snap = loadRestore()
    if (snap?.tabs.length) {
      const byId = new Map(live.map((s) => [s.id, s]))
      sessions.value = snap.tabs.map((t) => {
        const cur = byId.get(t.id)
        if (cur) {
          return toView(
            { ...cur, blocks: (cur.blocks && cur.blocks.length ? cur.blocks : t.blocks), status: cur.status },
            { ...cur, draft: '', history: [], histIndex: -1, blocks: t.blocks, permissionMode: cur.permissionMode ?? 'AskWrite' },
          )
        }
        return toView(
          {
            id: t.id,
            title: t.title,
            profileId: t.profileId,
            kind: t.kind,
            shell: t.shell ?? undefined,
            cwd: t.cwd ?? undefined,
            status: 'disconnected',
            host: t.host,
            port: t.port,
            user: t.user,
            auth: t.auth,
            processAlive: false,
            blocks: t.blocks,
            permissionMode: 'AskWrite',
          },
        )
      })
      for (const s of live) {
        if (!sessions.value.some((x) => x.id === s.id)) sessions.value.push(s)
      }
      activeId.value = snap.activeId && sessions.value.some((s) => s.id === snap.activeId) ? snap.activeId : sessions.value[0]?.id ?? null
      railOpen.value = snap.railOpen
      railTab.value = snap.railTab
      if (!fromRetry) showToast(RESTORED_TOAST, 'st-restored')
    } else {
      sessions.value = live
      activeId.value = live[0]?.id ?? null
    }
    for (const s of sessions.value) {
      if (!s.blocks.length && s.status !== 'disconnected') {
        try {
          const pack = await api.blocks(s.id, 0)
          if (pack?.items) s.blocks = pack.items
        } catch {
          // keep empty
        }
      }
      try {
        s.history = historyLines(await api.history(s.id))
      } catch {
        // optional
      }
    }
    persistReady = true
    bumpPage()
    if (sessions.value.length) focusPrompt()
  } catch (e) {
    persistReady = false
    if (e instanceof ApiError && e.status === 401) {
      pageState.value = 'unauthorized'
      errorSummary.value = `GET ${SESSIONS_PATH} → 401 Unauthorized`
      return
    }
    pageState.value = 'error'
    errorSummary.value = e instanceof ApiError ? `GET ${SESSIONS_PATH} → ${e.status} ${e.message}` : `GET ${SESSIONS_PATH} → 加载失败`
  }
}

function openNew(kind: SessionKind = 'local') {
  showNew.value = true
  newKind.value = kind
  newTitle.value = ''
  newCwd.value = kind === 'local' ? '~' : '~'
  newHost.value = ''
  newPort.value = 22
  newUser.value = ''
  newAuth.value = 'key'
  newSecretId.value = secrets.value[0]?.id ?? ''
  newIdentityFile.value = ''
  newError.value = ''
  const avail = shells.value.find((s) => s.available)
  newShellId.value = avail?.id ?? 'bash'
}

function closeModals() {
  showNew.value = false
  passwordOpen.value = false
  paletteOpen.value = false
  permMenu.value = false
  passwordValue.value = ''
  passwordReveal.value = false
  passwordError.value = ''
}

function openPassword(ctx: NonNullable<typeof passwordCtx.value>) {
  passwordCtx.value = ctx
  passwordValue.value = ''
  passwordReveal.value = false
  passwordError.value = ''
  passwordOpen.value = true
  showNew.value = false
}

async function submitNew() {
  newError.value = ''
  if (newKind.value === 'local') {
    await createSession({
      kind: 'local',
      title: newTitle.value.trim() || undefined,
      shellId: newShellId.value,
      cwd: newCwd.value.trim() || '~',
    })
    return
  }
  if (!newHost.value.trim() || !newUser.value.trim()) {
    newError.value = '请填写主机和用户后再打开'
    return
  }
  if (newAuth.value === 'key' && !newSecretId.value && !newIdentityFile.value.trim()) {
    newError.value = '选私钥时必须指定已有凭据句柄或本机 identityFile，否则无法创建（credential_required）'
    return
  }
  const body: CreateSessionBody = {
    kind: 'ssh',
    title: newTitle.value.trim() || `${newUser.value.trim()}@${newHost.value.trim()}`,
    host: newHost.value.trim(),
    port: newPort.value || 22,
    user: newUser.value.trim(),
    auth: newAuth.value,
    secretId: newAuth.value === 'key' ? newSecretId.value || null : null,
    identityFile: newAuth.value === 'key' ? newIdentityFile.value.trim() || null : null,
    cwd: newCwd.value.trim() || undefined,
  }
  if (newAuth.value === 'interactive') {
    openPassword({
      mode: 'create',
      user: body.user!,
      host: body.host!,
      port: body.port ?? 22,
      body,
    })
    return
  }
  await createSession(body)
}

async function confirmPassword() {
  if (!passwordValue.value) {
    passwordError.value = '请输入密码后再连接'
    return
  }
  const ctx = passwordCtx.value
  if (!ctx) return
  const secret = passwordValue.value
  passwordValue.value = ''
  try {
    if (ctx.mode === 'create' && ctx.body) {
      await createSession({ ...ctx.body, password: secret })
    } else if (ctx.mode === 'reconnect' && ctx.sessionId) {
      await doReconnect(ctx.sessionId, secret)
    }
    passwordOpen.value = false
    passwordCtx.value = null
  } catch (e) {
    passwordError.value = e instanceof ApiError ? e.message : '连接失败'
  }
}

function cancelPassword() {
  passwordOpen.value = false
  passwordCtx.value = null
  passwordValue.value = ''
  passwordError.value = ''
}

async function createSession(body: CreateSessionBody) {
  if (isFixture.value) {
    if (body.kind === 'ssh' && body.auth === 'interactive' && !body.password) {
      throw new ApiError(409, '需要交互密码', { code: 'password_required', needsInteractivePassword: true }, SESSIONS_PATH)
    }
    if (body.kind === 'ssh' && (body.auth === 'key' || !body.auth) && !body.secretId && !body.identityFile && !body.profileId) {
      newError.value = '缺少密钥材料（credential_required）'
      throw new ApiError(400, 'credential_required', { code: 'credential_required' }, SESSIONS_PATH)
    }
    if (body.kind === 'ssh' && body.auth === 'key' && !body.secretId && !body.identityFile && body.profileId) {
      const p = profiles.value.find((x) => x.id === body.profileId)
      if (!p?.secretId && !p?.identityFile) {
        newError.value = '缺少密钥材料（credential_required）'
        throw new ApiError(400, 'credential_required', { code: 'credential_required' }, SESSIONS_PATH)
      }
    }
    const profile = body.profileId ? profiles.value.find((p) => p.id === body.profileId) : undefined
    const id = nid('s')
    const kind = body.kind
    const s = toView({
      id,
      title: body.title || profile?.name || (kind === 'local' ? body.shellId || 'bash' : `${body.user}@${body.host}`),
      profileId: body.profileId ?? null,
      kind,
      shell: body.shellId || profile?.shellId || 'bash',
      cwd: body.cwd || profile?.cwd || '~',
      status: 'connected',
      host: body.host ?? profile?.host ?? null,
      port: body.port ?? profile?.port ?? null,
      user: body.user ?? profile?.user ?? (kind === 'local' ? 'user' : null),
      auth: body.auth ?? profile?.auth ?? null,
      processAlive: true,
      permissionMode: 'AskWrite',
      blocks: [
        systemBlock(
          nid('b'),
          1,
          'connected',
          kind === 'ssh'
            ? `已连接到 ${body.user ?? profile?.user}@${body.host ?? profile?.host}:${body.port ?? profile?.port ?? 22} · ${ (body.auth ?? profile?.auth) === 'key' ? '密钥' : '交互密码' }`
            : `已连接到本机 ${body.shellId || 'bash'} · ${body.cwd || '~'}`,
        ),
      ],
    })
    sessions.value = [...sessions.value, s]
    activeId.value = s.id
    showNew.value = false
    bumpPage()
    focusPrompt()
    return
  }
  try {
    const created = await api.createSession(body)
    if (!created) throw new Error('empty')
    const view = toView(created)
    sessions.value = [...sessions.value, view]
    activeId.value = view.id
    showNew.value = false
    bumpPage()
    focusPrompt()
  } catch (e) {
    if (e instanceof ApiError && e.needsInteractivePassword) {
      openPassword({
        mode: 'create',
        user: body.user || '',
        host: body.host || '',
        port: body.port ?? 22,
        body,
      })
      return
    }
    if (e instanceof ApiError && e.code === 'credential_required') {
      newError.value = e.message || '缺少密钥材料（credential_required）'
      return
    }
    newError.value = e instanceof ApiError ? e.message : '创建会话失败'
  }
}

async function openProfile(p: Profile) {
  const existing = sessions.value.find((s) => s.profileId === p.id)
  if (existing) {
    activeId.value = existing.id
    focusPrompt()
    return
  }
  if (p.kind === 'ssh' && p.auth === 'interactive') {
    openPassword({
      mode: 'create',
      user: p.user || '',
      host: p.host || '',
      port: p.port ?? 22,
      body: { profileId: p.id, kind: 'ssh', title: p.name, auth: 'interactive', host: p.host ?? undefined, port: p.port ?? 22, user: p.user ?? undefined },
    })
    return
  }
  await createSession({
    profileId: p.id,
    kind: p.kind,
    title: p.name,
    shellId: p.shellId,
    cwd: p.cwd,
    host: p.host ?? undefined,
    port: p.port ?? undefined,
    user: p.user ?? undefined,
    auth: p.auth,
    secretId: p.secretId,
    identityFile: p.identityFile,
  })
}

function profileOpenStatus(p: Profile): 'on' | 'off' | 'none' {
  const s = sessions.value.find((x) => x.profileId === p.id)
  if (!s) return 'none'
  return s.status === 'connected' ? 'on' : 'off'
}

async function closeTab(id: string) {
  const idx = sessions.value.findIndex((s) => s.id === id)
  if (idx < 0) return
  if (!isFixture.value) {
    try {
      await api.closeSession(id)
    } catch {
      // still close locally
    }
  }
  const next = sessions.value.filter((s) => s.id !== id)
  const neighbor = next[idx] ?? next[idx - 1] ?? null
  sessions.value = next
  activeId.value = neighbor?.id ?? null
  bumpPage()
  if (neighbor) focusPrompt()
}

async function requestReconnect(s: ViewSession) {
  if (s.kind === 'ssh' && s.auth === 'interactive') {
    openPassword({ mode: 'reconnect', user: s.user || '', host: s.host || '', port: s.port ?? 22, sessionId: s.id })
    return
  }
  await doReconnect(s.id)
}

async function doReconnect(id: string, password?: string) {
  const s = sessions.value.find((x) => x.id === id)
  if (!s) return
  if (isFixture.value) {
    if (s.auth === 'interactive' && !password) {
      throw new ApiError(409, '需要交互密码', { code: 'password_required', needsInteractivePassword: true })
    }
    s.status = 'connected'
    s.processAlive = true
    s.blocks = [
      ...s.blocks,
      systemBlock(nid('b'), (s.blocks.at(-1)?.seq ?? 0) + 1, 'reconnect', '已重新连接。未恢复任何密码或密钥。'),
    ]
    focusPrompt()
    return
  }
  const updated = await api.reconnect(id, password)
  if (updated) Object.assign(s, toView(updated, s))
  else s.status = 'connected'
  focusPrompt()
}

async function submitCommand(command: string, source: CommandSource) {
  const s = active.value
  if (!s) return
  const cmd = command.trim()
  if (!cmd) return
  if (s.status !== 'connected') {
    showToast('会话未连接，请先重新连接')
    return
  }
  if (promptBusy.value && cmd !== 'clear') {
    showToast('当前会话有命令正在执行')
    return
  }
  if (cmd === 'clear') {
    s.blocks = []
    s.draft = ''
    focusPrompt()
    return
  }
  const mode = s.permissionMode
  if (isFixture.value) {
    const risk = classifyRisk(cmd)
    const decision = decide(risk, mode)
    if (decision === 'deny') {
      s.blocks = [
        ...s.blocks,
        { id: nid('b'), seq: (s.blocks.at(-1)?.seq ?? 0) + 1, kind: 'system', command: cmd, output: hardRejectReason(cmd), status: 'rejected', exitCode: 1 },
      ]
      showToast(hardRejectReason(cmd))
      return
    }
    if (decision === 'ask') {
      approval.value = {
        id: nid('ap'),
        command: cmd,
        risk,
        mode: risk === 'high' ? 'AskHighRisk' : 'AskWrite',
        status: 'pending',
        sessionId: s.id,
      }
      approvalSource.value = source
      return
    }
    finishFixtureCommand(s, cmd)
    return
  }
  try {
    const res = await api.runCommand(s.id, cmd, source)
    if (!res) return
    if (res.status === 'awaiting_approval' && res.approval) {
      approval.value = { ...res.approval, sessionId: s.id }
      approvalSource.value = source
      if (res.block) mergeBlock(s, res.block)
      return
    }
    if (res.block) mergeBlock(s, res.block)
    s.history = [...s.history, cmd]
    s.draft = ''
    s.histIndex = -1
    startPoll(s)
    focusPrompt()
  } catch (e) {
    if (e instanceof ApiError && e.code === 'rejected_hard') {
      showToast(e.message || hardRejectReason(cmd))
      s.blocks = [
        ...s.blocks,
        { id: nid('b'), seq: (s.blocks.at(-1)?.seq ?? 0) + 1, kind: 'system', command: cmd, output: e.message, status: 'rejected', exitCode: 1 },
      ]
      return
    }
    showToast(e instanceof ApiError ? e.message : '命令提交失败')
  }
}

function finishFixtureCommand(s: ViewSession, cmd: string, failed = false) {
  const output = failed ? `命令失败：${cmd}` : `(fixture) ${cmd}\nok`
  s.blocks = [
    ...s.blocks,
    {
      id: nid('b'),
      seq: (s.blocks.at(-1)?.seq ?? 0) + 1,
      kind: 'command',
      command: cmd,
      output,
      status: failed ? 'failed' : 'done',
      exitCode: failed ? 1 : 0,
    },
  ]
  s.history = [...s.history, cmd]
  s.draft = ''
  s.histIndex = -1
  focusPrompt()
}

function mergeBlock(s: ViewSession, block: Block) {
  const i = s.blocks.findIndex((b) => b.id === block.id)
  if (i >= 0) s.blocks.splice(i, 1, { ...s.blocks[i], ...block })
  else s.blocks = [...s.blocks, block]
}

async function confirmApproval() {
  const ap = approval.value
  const s = sessions.value.find((x) => x.id === (ap?.sessionId ?? activeId.value))
  if (!ap || !s) return
  approval.value = null
  if (isFixture.value) {
    finishFixtureCommand(s, ap.command)
    return
  }
  try {
    const res = await api.resolveApproval(ap.id, 'approve')
    const block = (res as { block?: Block } | undefined)?.block
    if (block) mergeBlock(s, block)
    s.history = [...s.history, ap.command]
    s.draft = ''
    s.histIndex = -1
    startPoll(s)
    focusPrompt()
  } catch (e) {
    showToast(e instanceof ApiError ? e.message : '批准失败')
  }
}

async function cancelApproval() {
  const ap = approval.value
  const s = sessions.value.find((x) => x.id === (ap?.sessionId ?? activeId.value))
  if (s && ap && !s.draft) s.draft = ap.command
  approval.value = null
  if (ap && !isFixture.value) {
    try {
      await api.resolveApproval(ap.id, 'reject')
    } catch {
      // already dismissed
    }
  }
  focusPrompt()
}

function startPoll(s: ViewSession) {
  if (isFixture.value) return
  if (pollTimer) clearInterval(pollTimer)
  pollTimer = setInterval(async () => {
    try {
      const after = s.blocks.reduce((m, b) => Math.max(m, b.seq), 0)
      const pack = await api.blocks(s.id, Math.max(0, after - 1))
      if (pack?.items) {
        for (const b of pack.items) mergeBlock(s, b)
      }
      if (pack?.status === 'disconnected') s.status = 'disconnected'
      if (!s.blocks.some((b) => b.status === 'running')) {
        if (pollTimer) clearInterval(pollTimer)
        pollTimer = undefined
      }
    } catch {
      // next tick
    }
  }, 800)
}

function onPromptKey(ev: KeyboardEvent, s: ViewSession) {
  if (ev.key === 'Enter') {
    ev.preventDefault()
    void submitCommand(s.draft, 'prompt')
    suggestOn.value = false
    return
  }
  if (ev.key === 'ArrowUp') {
    ev.preventDefault()
    if (!s.history.length) return
    if (s.histIndex < 0) s.histIndex = s.history.length - 1
    else if (s.histIndex > 0) s.histIndex -= 1
    s.draft = s.history[s.histIndex] ?? s.draft
    return
  }
  if (ev.key === 'ArrowDown') {
    ev.preventDefault()
    if (s.histIndex < 0) return
    if (s.histIndex < s.history.length - 1) {
      s.histIndex += 1
      s.draft = s.history[s.histIndex] ?? ''
    } else {
      s.histIndex = -1
      s.draft = ''
    }
    return
  }
  if (ev.key === 'Tab') {
    if (suggestions.value.length) {
      ev.preventDefault()
      s.draft = suggestions.value[0]
      suggestOn.value = false
    }
    return
  }
  suggestOn.value = s.draft.trim().length > 0
}

function insertCmd(command: string) {
  const s = active.value
  if (!s) {
    showToast('还没有打开的会话')
    return
  }
  if (s.status !== 'connected') {
    showToast('会话未连接，无法插入到提示符')
    return
  }
  s.draft = command
  showToast('已插入到终端提示符，不会自动执行')
  focusPrompt()
}

function askRun(command: string) {
  void submitCommand(command, 'assistant')
}

async function sendChat() {
  const text = chatDraft.value.trim()
  if (!text || thinking.value) return
  const s = active.value
  if (!s) {
    showToast('还没有打开的会话')
    return
  }
  chats.value = { ...chats.value, [s.id]: [...(chats.value[s.id] ?? []), { role: 'user', content: text }] }
  chatDraft.value = ''
  thinking.value = true
  try {
    if (isFixture.value) {
      await new Promise((r) => setTimeout(r, 240))
      const risk = classifyRisk(text)
      const suggested =
        /nginx|重载/.test(text) ? 'sudo nginx -s reload' : risk === 'high' || /删除|rm -rf/.test(text) ? 'rm -rf /tmp/st-demo-orphan' : 'git status -sb'
      chats.value = {
        ...chats.value,
        [s.id]: [
          ...chats.value[s.id],
          {
            role: 'assistant',
            content: '已根据当前会话上下文给出建议。插入到终端只写入提示符，不会自动执行；批准后执行仍走同一道权限闸。',
            suggestedCommand: suggested,
          },
        ],
      }
    } else {
      const msg = await api.sendChat(s.id, text, contextBlock.value?.id)
      if (msg) chats.value = { ...chats.value, [s.id]: [...chats.value[s.id], msg] }
    }
  } catch (e) {
    const err = e instanceof ApiError ? e.message : '宿主模型不可用'
    chats.value = {
      ...chats.value,
      [s.id]: [...(chats.value[s.id] ?? []), { role: 'assistant', content: `调用宿主模型失败：${err}` }],
    }
  } finally {
    thinking.value = false
  }
}

function onChatKey(ev: KeyboardEvent) {
  if (ev.key === 'Enter' && !ev.shiftKey) {
    ev.preventDefault()
    void sendChat()
  }
}

async function setMode(mode: PermissionMode) {
  const s = active.value
  permMenu.value = false
  if (!s) return
  s.permissionMode = mode
  if (!isFixture.value) {
    try {
      await api.setSessionPermissionMode(s.id, mode)
    } catch {
      showToast('权限模式未能同步到服务端')
    }
  }
}

function relogin() {
  window.location.assign('/')
}

const paletteItems = computed(() => {
  const items: { id: string; label: string; run: () => void }[] = [
    { id: 'new-local', label: '新建本机会话', run: () => openNew('local') },
    { id: 'new-ssh', label: '新建 SSH 会话', run: () => openNew('ssh') },
    { id: 'open-ai', label: '打开助手对话', run: () => { railOpen.value = true; railTab.value = 'ai' } },
    { id: 'open-hist', label: '打开命令历史', run: () => { railOpen.value = true; railTab.value = 'history' } },
    { id: 'open-snip', label: '打开片段', run: () => { railOpen.value = true; railTab.value = 'snippets' } },
    ...profiles.value.map((p) => ({ id: `p-${p.id}`, label: `打开配置 ${p.name}`, run: () => void openProfile(p) })),
    ...sessions.value.map((s) => ({ id: `s-${s.id}`, label: `切换到 ${s.title}`, run: () => { activeId.value = s.id; focusPrompt() } })),
  ]
  const q = paletteQuery.value.trim().toLowerCase()
  return q ? items.filter((i) => i.label.toLowerCase().includes(q)) : items
})

function openPalette() {
  paletteOpen.value = true
  paletteQuery.value = ''
  paletteIndex.value = 0
  void nextTick(() => (document.querySelector('[data-testid="st-palette-input"]') as HTMLInputElement | null)?.focus())
}

function runPalette(i = paletteIndex.value) {
  const item = paletteItems.value[i]
  paletteOpen.value = false
  item?.run()
}

function onGlobalKey(ev: KeyboardEvent) {
  const meta = ev.metaKey || ev.ctrlKey
  if (meta && (ev.key === 'k' || ev.key === 'K' || (ev.shiftKey && (ev.key === 'p' || ev.key === 'P')))) {
    ev.preventDefault()
    openPalette()
    return
  }
  if (ev.key === 'Escape') {
    if (paletteOpen.value || showNew.value || passwordOpen.value || approval.value) {
      ev.preventDefault()
      if (approval.value) void cancelApproval()
      else if (passwordOpen.value) cancelPassword()
      else closeModals()
    }
    return
  }
  if (paletteOpen.value || showNew.value || passwordOpen.value || approval.value) return
  const t = ev.target as HTMLElement | null
  const tag = t?.tagName
  if (tag === 'INPUT' || tag === 'TEXTAREA' || t?.isContentEditable) return
  if (ev.key.length === 1 && !meta && !ev.altKey && active.value && !promptBusy.value) {
    ev.preventDefault()
    active.value.draft += ev.key
    suggestOn.value = true
    focusPrompt()
  }
}

onMounted(() => {
  window.addEventListener('keydown', onGlobalKey)
  if (props.story) applyStory(props.story)
  else void loadLive()
})

onUnmounted(() => {
  window.removeEventListener('keydown', onGlobalKey)
  if (toastTimer) clearTimeout(toastTimer)
  if (pollTimer) clearInterval(pollTimer)
})

watch(
  () => props.story,
  (id) => {
    if (id) applyStory(id)
  },
)

const showRail = computed(() => railOpen.value && pageState.value === 'ready')
const layoutClass = computed(() => (showRail.value ? 'body with-rail' : 'body'))

function approvalTitle(ap: Approval) {
  if (ap.risk === 'high' || ap.mode === 'AskHighRisk') return 'ASKHIGHRISK · 高风险确认'
  return 'ASKWRITE · 写操作确认'
}
</script>

<template>
  <div class="smart-terminal">
    <main class="st-page" data-testid="st-page">
      <div class="pagehead">
        <div>
          <div class="eyebrow">TERMINAL · 会话与命令</div>
          <div class="h1">智能终端<span class="ver">v2.0.0</span></div>
        </div>
        <div class="row">
          <button
            class="perm-badge"
            type="button"
            data-testid="st-permission-badge"
            :title="'当前权限闸 · 默认 AskWrite'"
            @click="permMenu = !permMenu"
          >
            {{ active?.permissionMode || 'AskWrite' }}
          </button>
          <div v-if="permMenu" class="glass-strong perm-menu">
            <button class="btn" type="button" @click="setMode('Auto')">Auto</button>
            <button class="btn" type="button" @click="setMode('AskWrite')">AskWrite（默认）</button>
            <button class="btn" type="button" @click="setMode('AskHighRisk')">AskHighRisk</button>
          </div>
          <button class="btn" type="button" data-testid="st-command-palette-btn" @click="openPalette">命令面板</button>
          <button class="btn" type="button" data-testid="st-toggle-rail" @click="railOpen = !railOpen">助手</button>
          <button class="btn primary" type="button" data-testid="st-new-session" @click="openNew('local')">新建会话</button>
        </div>
      </div>

      <div :class="layoutClass">
        <aside v-if="pageState === 'ready' || pageState === 'empty'" class="glass side">
          <input data-testid="st-profile-search" placeholder="搜索配置、主机或标签" v-model="profileQuery" />
          <div class="types">
            <button type="button" :class="{ on: profileFilter === 'all' }" data-testid="st-filter-all" @click="profileFilter = 'all'">全部</button>
            <button type="button" :class="{ on: profileFilter === 'local' }" data-testid="st-filter-local" @click="profileFilter = 'local'">本机</button>
            <button type="button" :class="{ on: profileFilter === 'ssh' }" data-testid="st-filter-ssh" @click="profileFilter = 'ssh'">SSH</button>
          </div>
          <div class="list" data-testid="st-profile-list">
            <button
              v-for="p in filteredProfiles"
              :key="p.id"
              class="item"
              :class="{ on: active?.profileId === p.id }"
              :data-testid="`st-profile-item-${p.id}`"
              type="button"
              @click="openProfile(p)"
            >
              <i class="dot" :class="profileOpenStatus(p) === 'on' ? 'green' : profileOpenStatus(p) === 'off' ? 'red' : ''" />
              <span>
                <b>{{ p.name }}</b>
                <span class="tag">{{ p.kind === 'local' ? '本机' : 'SSH' }}</span>
                <div class="muted">{{ profileSummary(p) }}</div>
              </span>
            </button>
            <div v-if="!filteredProfiles.length" class="muted" style="padding:12px">没有匹配的配置</div>
          </div>
        </aside>

        <section class="glass workspace" data-testid="st-workspace">
          <div v-if="pageState === 'loading'" class="empty" data-testid="st-state-loading">
            <i class="dot on" />
            <div class="q">正在取回会话与配置…</div>
          </div>
          <div v-else-if="pageState === 'error'" class="empty" data-testid="st-state-error">
            <div class="q">会话列表加载失败</div>
            <div class="err mono">{{ errorSummary }}</div>
            <button class="btn" type="button" @click="loadLive(true)">重试</button>
          </div>
          <div v-else-if="pageState === 'unauthorized'" class="empty" data-testid="st-state-401">
            <div class="q">登录已过期，暂时无法打开终端会话</div>
            <div class="err mono">{{ errorSummary }}</div>
            <button class="btn primary" type="button" data-testid="st-relogin" @click="relogin">重新登录</button>
          </div>
          <div v-else-if="pageState === 'empty'" class="empty" data-testid="st-state-empty">
            <div class="q">炉火已起，还没有打开任何会话</div>
            <div class="muted">从左侧选择本机壳或 SSH 配置，也可以新建一个会话。SSH 请优先使用密钥，由宿主凭据库保管。</div>
            <button class="btn primary" type="button" @click="openNew('local')">新建会话</button>
          </div>

          <template v-else-if="active">
            <div class="tabbar" data-testid="st-tab-bar">
              <button
                v-for="s in sessions"
                :key="s.id"
                class="stab"
                :class="{ on: s.id === activeId }"
                :data-testid="`st-tab-${s.id}`"
                type="button"
                :title="s.title"
                @click="activeId = s.id; focusPrompt()"
              >
                <i class="dot" :class="s.status === 'connected' ? 'green' : 'red'" />
                <span class="t">{{ s.title }}</span>
                <span class="x" :data-testid="`st-tab-close-${s.id}`" title="关闭" @click.stop="closeTab(s.id)">×</span>
              </button>
              <button class="stab" type="button" data-testid="st-tab-new" title="新建会话" @click="openNew('local')">+</button>
            </div>
            <div class="banner bad" :class="{ show: active.status !== 'connected' }" data-testid="st-disconnected-banner">
              <span>会话已断开，输出仍保留。重新连接不会恢复密码。</span>
              <button class="btn" type="button" data-testid="st-reconnect" @click="requestReconnect(active)">重新连接</button>
            </div>
            <div class="panes" data-testid="st-panes">
              <div class="pane" data-testid="st-pane-main">
                <div class="pane-head">
                  <span>{{ paneMeta(active) }}</span>
                  <span>{{ active.status === 'connected' ? '已连接' : '已断开' }}</span>
                </div>
                <div class="term" data-testid="st-term-surface-main" @click="focusPrompt">
                  <div
                    v-for="b in active.blocks"
                    :key="b.id"
                    class="block"
                    :class="{ fail: b.status === 'failed' || (b.exitCode != null && b.exitCode !== 0), sel: selectedBlockId === b.id }"
                    :data-testid="`st-block-${b.id}`"
                    @click.stop="selectedBlockId = b.id; railOpen = true; railTab = 'ai'"
                  >
                    <div class="cmd">{{ b.kind === 'system' ? b.command : `$ ${b.command}` }}</div>
                    <pre class="out">{{ b.output }}</pre>
                    <div v-if="b.kind !== 'system'" class="meta muted">{{ b.status === 'running' ? '运行中' : `退出码 ${b.exitCode ?? 0}` }}</div>
                  </div>
                  <div class="prompt-line" data-testid="st-prompt-line" @click.stop="focusPrompt">
                    <span class="prompt-ps1" data-testid="st-prompt-ps1">{{ ps1(active) }}</span>
                    <input
                      class="prompt-input"
                      data-testid="st-command-input"
                      spellcheck="false"
                      autocomplete="off"
                      :disabled="promptBusy"
                      :placeholder="active.status !== 'connected' ? '会话未连接，请先重新连接' : ''"
                      v-model="active.draft"
                      @keydown="onPromptKey($event, active)"
                      @input="suggestOn = active.draft.trim().length > 0"
                    />
                  </div>
                  <div v-if="suggestOn && suggestions.length" class="suggest">
                    <button
                      v-for="(c, i) in suggestions"
                      :key="c"
                      type="button"
                      :data-testid="`st-suggest-${i}`"
                      @click="active.draft = c; suggestOn = false; focusPrompt()"
                    >
                      {{ c }}
                    </button>
                  </div>
                </div>
              </div>
            </div>
          </template>
        </section>

        <aside v-if="showRail" class="glass rail" data-testid="st-side-panel">
          <div class="rail-tabs">
            <button type="button" :class="{ on: railTab === 'ai' }" data-testid="st-rail-ai" @click="railTab = 'ai'">对话</button>
            <button type="button" :class="{ on: railTab === 'snippets' }" data-testid="st-rail-snippets" @click="railTab = 'snippets'">片段</button>
            <button type="button" :class="{ on: railTab === 'history' }" data-testid="st-rail-history" @click="railTab = 'history'">历史</button>
          </div>
          <div v-if="railTab === 'ai'" class="chat" data-testid="st-rail-body">
            <div class="chat-context" data-testid="st-chat-context">
              <b>复用宿主已有设施</b> · 走宿主 AI 网关与已配置模型，不另开账号
              <br />
              {{ contextLine }}{{ active ? ` · 会话「${active.title}」` : '' }}
            </div>
            <div class="chat-thread" data-testid="st-chat-thread">
              <div
                v-for="(m, i) in chatMessages"
                :key="m.id || i"
                class="bubble"
                :class="m.role"
                :data-testid="m.role === 'user' ? `st-chat-user-${i}` : `st-chat-assistant-${i}`"
              >
                <div class="who">{{ m.role === 'user' ? '你' : '助手' }}</div>
                <div class="fmt">{{ m.content }}</div>
                <template v-if="m.role === 'assistant' && m.suggestedCommand">
                  <div class="ai-cmd" data-testid="st-ai-suggestion">{{ m.suggestedCommand }}</div>
                  <div class="row">
                    <button class="btn" type="button" data-testid="st-ai-insert" @click="insertCmd(m.suggestedCommand)">插入到终端</button>
                    <button class="btn primary" type="button" data-testid="st-ai-run" @click="askRun(m.suggestedCommand)">批准后执行</button>
                  </div>
                </template>
              </div>
              <div v-if="thinking" class="bubble assistant thinking" data-testid="st-chat-thinking">正在调用宿主模型…</div>
              <div v-if="!chatMessages.length && !thinking" class="muted" style="padding:8px">还没有消息。向助手提问，Enter 发送，Shift+Enter 换行。</div>
            </div>
            <div class="chat-composer">
              <textarea
                data-testid="st-chat-input"
                rows="2"
                placeholder="向助手提问，Enter 发送，Shift+Enter 换行"
                :disabled="thinking"
                v-model="chatDraft"
                @keydown="onChatKey"
              />
              <button class="btn primary" type="button" data-testid="st-chat-send" :disabled="thinking" @click="sendChat">发送</button>
            </div>
          </div>
          <div v-else class="list" data-testid="st-rail-body">
            <template v-if="railTab === 'snippets'">
              <div v-for="sn in snippets" :key="sn.id" class="item" :data-testid="`st-snippet-${sn.id}`">
                <div>
                  <b>{{ sn.name }}</b>
                  <div class="mono muted">{{ sn.command }}</div>
                </div>
                <div class="row">
                  <button class="btn" type="button" @click="insertCmd(sn.command)">插入到终端</button>
                  <button class="btn primary" type="button" @click="submitCommand(sn.command, 'snippet')">运行</button>
                </div>
              </div>
            </template>
            <template v-else>
              <input data-testid="st-history-search" v-model="historyQuery" placeholder="搜索历史" />
              <button
                v-for="(c, i) in (active?.history ?? []).filter((x) => !historyQuery || x.includes(historyQuery)).slice().reverse()"
                :key="i + c"
                class="item"
                type="button"
                data-testid="st-history-item"
                @click="insertCmd(c)"
              >
                {{ c }}
              </button>
              <div v-if="!(active?.history ?? []).length" class="muted" style="padding:12px">还没有命令历史</div>
            </template>
          </div>
        </aside>
      </div>
    </main>

    <div v-if="showNew || passwordOpen || approval || paletteOpen" class="mask show" @click="approval ? cancelApproval() : passwordOpen ? cancelPassword() : closeModals()" />

    <div v-if="showNew" class="glass-strong modal show" role="dialog">
      <div class="sec-title">新建会话</div>
      <div class="types">
        <button type="button" :class="{ on: newKind === 'local' }" @click="newKind = 'local'">本机壳</button>
        <button type="button" :class="{ on: newKind === 'ssh' }" @click="newKind = 'ssh'">SSH</button>
      </div>
      <template v-if="newKind === 'local'">
        <div class="field">
          <label>外壳</label>
          <select v-model="newShellId">
            <option v-for="sh in (shells.length ? shells : [{ id: 'bash', displayName: 'bash', available: true }])" :key="sh.id" :value="sh.id" :disabled="!sh.available">
              {{ sh.displayName }}{{ sh.available ? '' : '（不可用）' }}
            </option>
          </select>
        </div>
        <div class="field">
          <label>起始目录</label>
          <input v-model="newCwd" />
        </div>
      </template>
      <template v-else>
        <div class="field">
          <label>主机</label>
          <input data-testid="st-ssh-host" v-model="newHost" placeholder="host.example" />
        </div>
        <div class="grid2">
          <div class="field">
            <label>端口</label>
            <input type="number" v-model.number="newPort" />
          </div>
          <div class="field">
            <label>用户</label>
            <input v-model="newUser" />
          </div>
        </div>
        <div class="field">
          <label>认证</label>
          <select v-model="newAuth">
            <option value="key">私钥（推荐，存宿主凭据库）</option>
            <option value="interactive">交互式密码</option>
          </select>
        </div>
        <div v-if="newAuth === 'key'" class="field">
          <label>凭据句柄或本机私钥路径</label>
          <select v-model="newSecretId">
            <option value="">选择已导入的宿主凭据…</option>
            <option v-for="sec in secrets" :key="sec.id" :value="sec.id">{{ sec.label }} · {{ sec.fingerprint }}</option>
          </select>
          <input v-model="newIdentityFile" placeholder="或填写本机 identityFile，例如 ~/.ssh/id_ed25519" style="margin-top:6px" />
          <div class="key-hint">密钥由宿主凭据库保管。不会提供「记住密码」。缺材料时无法创建（credential_required）。</div>
        </div>
        <div v-else class="key-hint">交互式密码确认后再创建标签，取消不建标签。不会记住密码。</div>
      </template>
      <div class="field">
        <label>可选标题</label>
        <input v-model="newTitle" :placeholder="newKind === 'local' ? newShellId : (newUser && newHost ? `${newUser}@${newHost}` : 'user@host')" />
      </div>
      <div v-if="newError" class="err-text">{{ newError }}</div>
      <div class="row" style="justify-content:flex-end;margin-top:12px">
        <button class="btn" type="button" @click="showNew = false">取消</button>
        <button class="btn primary" type="button" data-testid="st-new-session-submit" @click="submitNew">打开</button>
      </div>
    </div>

    <div v-if="passwordOpen && passwordCtx" class="glass-strong modal show" role="dialog">
      <div class="sec-title">输入 SSH 密码</div>
      <div class="muted mono">{{ passwordCtx.user }}@{{ passwordCtx.host }}:{{ passwordCtx.port }}</div>
      <div class="key-hint">推荐改用密钥登录，由宿主凭据库保管。不会记住密码，关闭后即焚。</div>
      <div class="field">
        <label>密码</label>
        <input data-testid="st-ssh-password" :type="passwordReveal ? 'text' : 'password'" v-model="passwordValue" autocomplete="off" />
        <label class="muted"><input type="checkbox" v-model="passwordReveal" /> 显示密码</label>
      </div>
      <div v-if="passwordError" class="err-text">{{ passwordError }}</div>
      <div class="row" style="justify-content:flex-end;margin-top:12px">
        <button class="btn" type="button" data-testid="st-ssh-cancel" @click="cancelPassword">取消</button>
        <button class="btn primary" type="button" data-testid="st-ssh-connect" @click="confirmPassword">连接</button>
      </div>
    </div>

    <div v-if="approval" class="glass-strong modal show" role="dialog">
      <div class="sec-title">{{ approvalTitle(approval) }}</div>
      <pre class="mono" data-testid="st-approval-cmd">{{ approval.command }}</pre>
      <div class="key-hint">默认权限闸为 AskWrite。取消不执行，草稿可留在提示符。</div>
      <div class="row" style="justify-content:flex-end;margin-top:12px">
        <button class="btn" type="button" data-testid="st-approval-cancel" @click="cancelApproval">取消</button>
        <button class="btn primary" type="button" data-testid="st-approval-confirm" @click="confirmApproval">批准并运行</button>
      </div>
    </div>

    <div v-if="paletteOpen" class="glass-strong palette show">
      <input data-testid="st-palette-input" v-model="paletteQuery" placeholder="搜索命令…" @keydown.enter.prevent="runPalette()" @keydown.esc.prevent="paletteOpen = false" />
      <button v-for="(it, i) in paletteItems" :key="it.id" type="button" class="pal-item" :class="{ on: i === paletteIndex }" @click="runPalette(i)">
        {{ it.label }}
      </button>
    </div>

    <div v-if="toastText" class="glass-strong toast show" :data-testid="toastTestId">{{ toastText }}</div>
  </div>
</template>
