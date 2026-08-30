import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import ChatView from '@/views/ChatView.vue'
import MessageList from '@/components/MessageList.vue'
import MessageInput from '@/components/MessageInput.vue'

// 模型列表 API（ChatView onMounted 动态加载模型，须离线 mock）
vi.mock('@/services/aiModelsApi', () => ({
  aiModelsApi: {
    list: vi.fn().mockResolvedValue([
      {
        providerId: 1,
        providerName: 'default',
        models: [
          { id: 1, providerId: 1, providerName: 'default', upstreamModelId: 'qwythos-9b-v2', chatModelId: 'default:qwythos-9b-v2', alias: null, capabilities: [], maxContext: 0, enabled: true, owner: null, lastSyncTime: '', createTime: '', updateTime: '' },
          { id: 2, providerId: 1, providerName: 'default', upstreamModelId: 'gemma-2b', chatModelId: 'default:gemma-2b', alias: 'Gemma 2B', capabilities: [], maxContext: 0, enabled: true, owner: null, lastSyncTime: '', createTime: '', updateTime: '' },
        ],
      },
    ]),
  },
}))

// Create a factory function to create mock store
const createMockStore = (options: Record<string, any> = {}) => ({
  messages: options.messages || [],
  conversations: options.conversations || [],
  currentConversationId: options.currentConversationId || null,
  isLoading: options.isLoading || false,
  isStreaming: options.isStreaming || false,
  isWsConnected: options.isWsConnected || false,
  error: options.error || null,
  initWebSocket: vi.fn().mockResolvedValue(undefined),
  loadConversations: vi.fn().mockResolvedValue(undefined),
  sendMessage: options.sendMessage || vi.fn().mockResolvedValue(undefined),
  createNewConversation: vi.fn().mockResolvedValue(undefined),
  loadConversation: vi.fn().mockResolvedValue(undefined),
  deleteConversation: vi.fn().mockResolvedValue(undefined),
  clearError: vi.fn(),
})

// Mock the chat store - use a mutable reference
const mockStoreRef = { current: createMockStore() }

vi.mock('@/stores/chat', () => ({
  useChatStore: vi.fn(() => mockStoreRef.current),
}))

describe('ChatView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    // Reset mock store to default
    mockStoreRef.current = createMockStore()
  })

  it('renders properly with main structure', () => {
    const wrapper = mount(ChatView, {
      global: {
        stubs: {
          MessageList: true,
          MessageInput: true,
        },
      },
    })
    expect(wrapper.find('.chat-view').exists()).toBe(true)
    expect(wrapper.find('.chat-header').exists()).toBe(true)
    // ChatView 已精简：原 .workbench 改为 .chat-anvil（聊天主区）
    expect(wrapper.find('.chat-anvil').exists()).toBe(true)
  })

  it('displays the correct title', () => {
    const wrapper = mount(ChatView, {
      global: {
        stubs: {
          MessageList: true,
          MessageInput: true,
        },
      },
    })
    expect(wrapper.find('.chat-agent-name').text()).toBe('AI Agent')
  })

  it('displays connection status as disconnected initially', () => {
    const wrapper = mount(ChatView, {
      global: {
        stubs: {
          MessageList: true,
          MessageInput: true,
        },
      },
    })
    const status = wrapper.find('.chat-status-text')
    expect(status.exists()).toBe(true)
    expect(status.text()).toBe('离线')
    expect(status.classes()).not.toContain('connected')
  })

  it('displays connection status as connected when WebSocket is connected', async () => {
    mockStoreRef.current = createMockStore({ isWsConnected: true })

    const wrapper = mount(ChatView, {
      global: {
        stubs: {
          MessageList: true,
          MessageInput: true,
        },
      },
    })
    const status = wrapper.find('.chat-status-text')
    expect(status.text()).toBe('就绪')
    expect(status.classes()).toContain('connected')
  })

  it('renders MessageList component', () => {
    const wrapper = mount(ChatView, {
      global: {
        stubs: {
          MessageInput: true,
        },
      },
    })
    expect(wrapper.findComponent(MessageList).exists()).toBe(true)
  })

  it('renders MessageInput component', () => {
    const wrapper = mount(ChatView, {
      global: {
        stubs: {
          MessageList: true,
        },
      },
    })
    expect(wrapper.findComponent(MessageInput).exists()).toBe(true)
  })

  it('has menu button for toggling sidebar', () => {
    const wrapper = mount(ChatView, {
      global: {
        stubs: {
          MessageList: true,
          MessageInput: true,
        },
      },
    })
    expect(wrapper.find('.menu-button').exists()).toBe(true)
  })

  it('sidebar is hidden initially', () => {
    const wrapper = mount(ChatView, {
      global: {
        stubs: {
          MessageList: true,
          MessageInput: true,
        },
      },
    })
    expect(wrapper.find('.sidebar-overlay').exists()).toBe(false)
  })

  it('toggles sidebar when menu button is clicked', async () => {
    const wrapper = mount(ChatView, {
      global: {
        stubs: {
          MessageList: true,
          MessageInput: true,
        },
      },
    })

    // Click menu button to open sidebar
    await wrapper.find('.menu-button').trigger('click')
    expect(wrapper.find('.sidebar-overlay').exists()).toBe(true)
    expect(wrapper.find('.sidebar').exists()).toBe(true)

    // Click menu button again to close sidebar
    await wrapper.find('.menu-button').trigger('click')
    expect(wrapper.find('.sidebar-overlay').exists()).toBe(false)
  })

  it('closes sidebar when clicking overlay', async () => {
    const wrapper = mount(ChatView, {
      global: {
        stubs: {
          MessageList: true,
          MessageInput: true,
        },
      },
    })

    // Open sidebar
    await wrapper.find('.menu-button').trigger('click')
    expect(wrapper.find('.sidebar-overlay').exists()).toBe(true)

    // Click overlay to close
    await wrapper.find('.sidebar-overlay').trigger('click')
    expect(wrapper.find('.sidebar-overlay').exists()).toBe(false)
  })

  it('has new chat button in sidebar', async () => {
    const wrapper = mount(ChatView, {
      global: {
        stubs: {
          MessageList: true,
          MessageInput: true,
        },
      },
    })

    await wrapper.find('.menu-button').trigger('click')
    expect(wrapper.find('.new-chat-button').exists()).toBe(true)
    expect(wrapper.find('.new-chat-button').text()).toContain('新对话')
  })

  it('displays empty conversations message when no conversations', async () => {
    const wrapper = mount(ChatView, {
      global: {
        stubs: {
          MessageList: true,
          MessageInput: true,
        },
      },
    })

    await wrapper.find('.menu-button').trigger('click')
    expect(wrapper.find('.empty-conversations').exists()).toBe(true)
    expect(wrapper.find('.empty-conversations').text()).toBe('暂无对话记录')
  })

  it('displays conversations list when available', async () => {
    mockStoreRef.current = createMockStore({
      conversations: [
        { id: 'conv-1', title: '对话1', createdAt: new Date(), updatedAt: new Date() },
        { id: 'conv-2', title: '对话2', createdAt: new Date(), updatedAt: new Date() },
      ],
      currentConversationId: 'conv-1',
    })

    const wrapper = mount(ChatView, {
      global: {
        stubs: {
          MessageList: true,
          MessageInput: true,
        },
      },
    })

    await wrapper.find('.menu-button').trigger('click')
    const items = wrapper.findAll('.conversation-item')
    expect(items.length).toBe(2)
    expect(items[0].find('.conversation-title').text()).toBe('对话1')
    expect(items[0].classes()).toContain('active')
  })

  it('passes correct props to MessageList', () => {
    mockStoreRef.current = createMockStore({
      messages: [
        { id: '1', role: 'user', content: 'Hello', timestamp: new Date() },
      ],
      isLoading: true,
    })

    const wrapper = mount(ChatView, {
      global: {
        stubs: {
          MessageInput: true,
        },
      },
    })

    const messageList = wrapper.findComponent(MessageList)
    expect(messageList.props('messages')).toHaveLength(1)
    expect(messageList.props('isLoading')).toBe(true)
  })

  it('passes correct props to MessageInput', () => {
    mockStoreRef.current = createMockStore({
      isLoading: true,
    })

    const wrapper = mount(ChatView, {
      global: {
        stubs: {
          MessageList: true,
        },
      },
    })

    const messageInput = wrapper.findComponent(MessageInput)
    expect(messageInput.props('disabled')).toBe(true)
  })

  it('calls sendMessage when MessageInput emits send event', async () => {
    const mockSendMessage = vi.fn().mockResolvedValue(undefined)
    mockStoreRef.current = createMockStore({
      sendMessage: mockSendMessage,
    })

    const wrapper = mount(ChatView, {
      global: {
        stubs: {
          MessageList: true,
        },
      },
    })

    // 等待 onMounted 的模型列表加载完成（默认选中第一个模型）
    await vi.waitFor(() => {
      expect(wrapper.findAll('.model-select option').length).toBe(3)
    })

    const messageInput = wrapper.findComponent(MessageInput)
    await messageInput.vm.$emit('send', 'Test message')

    expect(mockSendMessage).toHaveBeenCalledWith('Test message', 'default:qwythos-9b-v2')
  })

  it('renders model select with dynamic options and default selection', async () => {
    const wrapper = mount(ChatView, {
      global: {
        stubs: {
          MessageList: true,
          MessageInput: true,
        },
      },
    })

    await vi.waitFor(() => {
      const options = wrapper.findAll('.model-select option')
      expect(options.length).toBe(3) // 默认模型 + 2 个动态模型
    })

    const select = wrapper.find('.model-select')
    expect((select.element as HTMLSelectElement).value).toBe('default:qwythos-9b-v2')
    // 别名优先展示
    expect(wrapper.find('.model-select').text()).toContain('Gemma 2B')
    expect(wrapper.find('.model-select').text()).toContain('qwythos-9b-v2')
  })
})