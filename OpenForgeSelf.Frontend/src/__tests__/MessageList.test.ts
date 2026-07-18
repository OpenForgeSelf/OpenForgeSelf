import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import MessageList from '@/components/MessageList.vue'
import MessageItem from '@/components/MessageItem.vue'
import type { ChatMessage } from '@/types/chat'

describe('MessageList', () => {
  const createMessage = (id: string, role: 'user' | 'assistant' | 'system', content: string, isStreaming?: boolean): ChatMessage => ({
    id,
    role,
    content,
    timestamp: new Date(),
    isStreaming,
  })

  it('renders empty state when no messages', () => {
    const wrapper = mount(MessageList, {
      props: {
        messages: [],
      },
      global: {
        stubs: {
          MessageItem: true,
        },
      },
    })

    expect(wrapper.find('.empty-state').exists()).toBe(true)
    expect(wrapper.find('.empty-icon').text()).toBe('💬')
    expect(wrapper.find('.empty-title').text()).toBe('开始对话')
    expect(wrapper.find('.empty-description').text()).toBe('在下方输入框中输入消息，开始与AI助手对话')
  })

  it('does not render empty state when messages exist', () => {
    const messages: ChatMessage[] = [
      createMessage('1', 'user', 'Hello'),
    ]

    const wrapper = mount(MessageList, {
      props: {
        messages,
      },
      global: {
        stubs: {
          MessageItem: true,
        },
      },
    })

    expect(wrapper.find('.empty-state').exists()).toBe(false)
  })

  it('renders messages container when messages exist', () => {
    const messages: ChatMessage[] = [
      createMessage('1', 'user', 'Hello'),
    ]

    const wrapper = mount(MessageList, {
      props: {
        messages,
      },
      global: {
        stubs: {
          MessageItem: true,
        },
      },
    })

    expect(wrapper.find('.messages-container').exists()).toBe(true)
  })

  it('renders MessageItem for each message', () => {
    const messages: ChatMessage[] = [
      createMessage('1', 'user', 'Hello'),
      createMessage('2', 'assistant', 'Hi there!'),
      createMessage('3', 'user', 'How are you?'),
    ]

    const wrapper = mount(MessageList, {
      props: {
        messages,
      },
    })

    const messageItems = wrapper.findAllComponents(MessageItem)
    expect(messageItems.length).toBe(3)
  })

  it('passes correct props to MessageItem', () => {
    const messages: ChatMessage[] = [
      createMessage('1', 'user', 'Hello'),
    ]

    const wrapper = mount(MessageList, {
      props: {
        messages,
      },
    })

    const messageItem = wrapper.findComponent(MessageItem)
    expect(messageItem.props('message')).toEqual(messages[0])
  })

  it('does not show loading indicator when isLoading is false', () => {
    const wrapper = mount(MessageList, {
      props: {
        messages: [],
        isLoading: false,
      },
      global: {
        stubs: {
          MessageItem: true,
        },
      },
    })

    expect(wrapper.find('.loading-indicator').exists()).toBe(false)
  })

  it('shows loading indicator when isLoading is true', () => {
    const wrapper = mount(MessageList, {
      props: {
        messages: [],
        isLoading: true,
      },
      global: {
        stubs: {
          MessageItem: true,
        },
      },
    })

    expect(wrapper.find('.loading-indicator').exists()).toBe(true)
    expect(wrapper.find('.loading-text').text()).toBe('AI正在思考...')
  })

  it('shows loading dots animation', () => {
    const wrapper = mount(MessageList, {
      props: {
        messages: [],
        isLoading: true,
      },
      global: {
        stubs: {
          MessageItem: true,
        },
      },
    })

    const dots = wrapper.findAll('.loading-dots span')
    expect(dots.length).toBe(3)
  })

  it('updates when messages prop changes', async () => {
    const wrapper = mount(MessageList, {
      props: {
        messages: [],
      },
      global: {
        stubs: {
          MessageItem: true,
        },
      },
    })

    expect(wrapper.find('.empty-state').exists()).toBe(true)

    await wrapper.setProps({
      messages: [createMessage('1', 'user', 'Hello')],
    })

    expect(wrapper.find('.empty-state').exists()).toBe(false)
    expect(wrapper.find('.messages-container').exists()).toBe(true)
  })

  it('has correct container structure', () => {
    const wrapper = mount(MessageList, {
      props: {
        messages: [],
      },
      global: {
        stubs: {
          MessageItem: true,
        },
      },
    })

    expect(wrapper.find('.message-list').exists()).toBe(true)
  })

  it('exposes scrollToBottom method', () => {
    const wrapper = mount(MessageList, {
      props: {
        messages: [],
      },
      global: {
        stubs: {
          MessageItem: true,
        },
      },
    })

    expect(wrapper.vm.scrollToBottom).toBeDefined()
    expect(typeof wrapper.vm.scrollToBottom).toBe('function')
  })

  it('scrolls to bottom when messages length changes', async () => {
    const scrollSpy = vi.spyOn(Element.prototype, 'scrollTop', 'set')

    const wrapper = mount(MessageList, {
      props: {
        messages: [],
      },
      global: {
        stubs: {
          MessageItem: true,
        },
      },
      attachTo: document.body,
    })

    // Add a message
    await wrapper.setProps({
      messages: [createMessage('1', 'user', 'Hello')],
    })

    // Wait for nextTick
    await wrapper.vm.$nextTick()

    // The scroll should have been attempted
    expect(scrollSpy).toHaveBeenCalled()

    scrollSpy.mockRestore()
    wrapper.unmount()
  })

  it('renders streaming message correctly', () => {
    const messages: ChatMessage[] = [
      createMessage('1', 'assistant', 'Hello...', true),
    ]

    const wrapper = mount(MessageList, {
      props: {
        messages,
      },
    })

    const messageItem = wrapper.findComponent(MessageItem)
    expect(messageItem.props('message').isStreaming).toBe(true)
  })

  it('renders multiple messages with different roles', () => {
    const messages: ChatMessage[] = [
      createMessage('1', 'user', 'Hello'),
      createMessage('2', 'assistant', 'Hi there!'),
      createMessage('3', 'system', 'System message'),
    ]

    const wrapper = mount(MessageList, {
      props: {
        messages,
      },
    })

    const messageItems = wrapper.findAllComponents(MessageItem)
    expect(messageItems[0].props('message').role).toBe('user')
    expect(messageItems[1].props('message').role).toBe('assistant')
    expect(messageItems[2].props('message').role).toBe('system')
  })
})