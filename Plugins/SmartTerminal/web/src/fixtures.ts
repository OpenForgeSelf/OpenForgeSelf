import type { Block, ChatMessage, Profile, SecretMeta, Session, ShellInfo, Snippet } from './types'

export const FIXTURE_SHELLS: ShellInfo[] = [
  { id: 'bash', displayName: 'bash', executable: '/bin/bash', kind: 'unix', available: true },
  { id: 'zsh', displayName: 'zsh', executable: '/bin/zsh', kind: 'unix', available: true },
  { id: 'sh', displayName: 'sh', executable: '/bin/sh', kind: 'unix', available: true },
  { id: 'pwsh', displayName: 'PowerShell', executable: 'pwsh', kind: 'windows', available: false, note: '当前平台不可用' },
  { id: 'cmd', displayName: 'cmd', executable: 'cmd.exe', kind: 'windows', available: false, note: '当前平台不可用' },
]

export const FIXTURE_SECRETS: SecretMeta[] = [
  { id: 'sec-deploy-ed25519', kind: 'ssh-key', fingerprint: 'SHA256:wK9f…7aQ', label: 'deploy@staging', createdAt: '2026-11-01T08:00:00+08:00' },
]

export const FIXTURE_PROFILES: Profile[] = [
  { id: 'p-local-bash', name: '本机开发', kind: 'local', shellId: 'bash', cwd: '~/work/forgeself', summary: 'bash · ~/work/forgeself' },
  { id: 'p-ssh-key', name: 'staging-web', kind: 'ssh', host: '10.2.8.21', port: 22, user: 'deploy', auth: 'key', secretId: 'sec-deploy-ed25519', cwd: '/var/www', summary: 'deploy@10.2.8.21:22 · 密钥' },
  { id: 'p-ssh-pass', name: 'jump-01', kind: 'ssh', host: '10.9.1.4', port: 22, user: 'ops', auth: 'interactive', summary: 'ops@10.9.1.4:22 · 交互密码' },
  { id: 'p-ssh-long', name: 'prod-api-01 长标题配置', kind: 'ssh', host: '10.2.8.21', port: 22, user: 'deploy', auth: 'key', secretId: 'sec-deploy-ed25519', cwd: '/var/log/nginx', summary: 'deploy@10.2.8.21:22 · 密钥' },
]

export const FIXTURE_SNIPPETS: Snippet[] = [
  { id: 'sn-status', name: '仓库状态', command: 'git status -sb' },
  { id: 'sn-reload', name: '重载 nginx', command: 'sudo nginx -s reload' },
  { id: 'sn-high', name: '清理演示目录', command: 'rm -rf /tmp/st-demo-orphan' },
]

function blk(partial: Partial<Block> & Pick<Block, 'id' | 'seq' | 'command'>): Block {
  return {
    kind: 'command',
    output: '',
    status: 'done',
    exitCode: 0,
    ...partial,
  }
}

export function systemBlock(id: string, seq: number, command: string, output: string): Block {
  return { id, seq, kind: 'system', command, output, status: 'done', exitCode: 0 }
}

export const FIXTURE_SESSIONS: Record<string, Session> = {
  's-local': {
    id: 's-local',
    title: '本机开发',
    profileId: 'p-local-bash',
    kind: 'local',
    shell: 'bash',
    cwd: '~/work/forgeself',
    status: 'connected',
    permissionMode: 'AskWrite',
    processAlive: true,
    blocks: [
      systemBlock('b-connected', 1, 'connected', '已连接到本机 bash · ~/work/forgeself'),
      blk({ id: 'b-git', seq: 2, command: 'git status -sb', output: '## feat/smart-terminal-v2\n M web/src/SmartTerminalView.vue' }),
    ],
  },
  's-key': {
    id: 's-key',
    title: 'staging-web',
    profileId: 'p-ssh-key',
    kind: 'ssh',
    shell: 'bash',
    cwd: '/var/www',
    status: 'connected',
    host: '10.2.8.21',
    port: 22,
    user: 'deploy',
    auth: 'key',
    secretId: 'sec-deploy-ed25519',
    permissionMode: 'AskWrite',
    processAlive: true,
    blocks: [
      systemBlock('b-key-ok', 1, 'connected', '已连接到 deploy@10.2.8.21:22 · 密钥'),
      blk({ id: 'b-uptime', seq: 2, command: 'uptime', output: ' 18:12:01 up 14 days,  3:08,  1 user,  load average: 0.08, 0.11, 0.09' }),
    ],
  },
  's-pass': {
    id: 's-pass',
    title: 'jump-01',
    profileId: 'p-ssh-pass',
    kind: 'ssh',
    shell: 'bash',
    cwd: '~',
    status: 'disconnected',
    host: '10.9.1.4',
    port: 22,
    user: 'ops',
    auth: 'interactive',
    permissionMode: 'AskWrite',
    processAlive: false,
    blocks: [
      systemBlock('b-pass-ok', 1, 'connected', '已连接到 ops@10.9.1.4:22 · 交互密码（密码未持久化）'),
      blk({ id: 'b-ss', seq: 2, command: 'ss -lnt', output: 'State  Recv-Q Send-Q Local Address:Port\nLISTEN 0      128          0.0.0.0:22' }),
    ],
  },
  's-long': {
    id: 's-long',
    title: 'prod-api-01 · /var/log/nginx 这是一个用来检查长标题会不会撑破标签栏的会话名称',
    profileId: 'p-ssh-long',
    kind: 'ssh',
    shell: 'bash',
    cwd: '/var/log/nginx',
    status: 'connected',
    host: '10.2.8.21',
    port: 22,
    user: 'deploy',
    auth: 'key',
    permissionMode: 'AskWrite',
    processAlive: true,
    blocks: [
      blk({
        id: 'b-tail',
        seq: 1,
        command: 'tail -n 3 access.log',
        output: '10.0.0.8 - - "GET /health" 200\n10.0.1.2 - - "POST /login" 401\n10.0.1.2 - - "POST /login" 200',
      }),
      blk({
        id: 'b-fail',
        seq: 2,
        command: 'systemctl status nginx',
        output: 'Unit nginx.service could not be found.',
        status: 'failed',
        exitCode: 4,
      }),
    ],
  },
}

export const FIXTURE_HISTORY: Record<string, string[]> = {
  's-local': ['git status -sb', 'npm test'],
  's-key': ['uptime', 'df -h'],
  's-pass': ['ss -lnt'],
  's-long': ['tail -n 3 access.log', 'systemctl status nginx'],
}

export const FIXTURE_CHAT: ChatMessage[] = [
  { id: 'm-1', role: 'user', content: '帮我重载一下 staging 上的 nginx。' },
  {
    id: 'm-2',
    role: 'assistant',
    content: '可以。下面这条会改动机状态，插入后不会自动执行；若要跑，请走「批准后执行」，仍会经过 AskWrite 确认。',
    suggestedCommand: 'sudo nginx -s reload',
  },
]
