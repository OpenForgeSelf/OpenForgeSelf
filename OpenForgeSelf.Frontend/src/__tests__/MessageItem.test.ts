import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import MessageItem from '@/components/MessageItem.vue'
import type { ChatMessage } from '@/types/chat'

describe('MessageItem', () => {
  const createMessage = (
    id: string,
    role: 'user' | 'assistant' | 'system',
    content: string,
    isStreaming?: boolean
  ): ChatMessage => ({
    id,
    role,
    content,
    timestamp: new Date('2024-01-15T10:30:00'),
    isStreaming,
  })

  it('renders properly with message content', () => {
    const message = createMessage('1', 'user', 'Hello World')
    const wrapper = mount(MessageItem, {
      props: { message },
    })

    expect(wrapper.find('.message-item').exists()).toBe(true)
    expect(wrapper.find('.message-text').text()).toBe('Hello World')
  })

  it('renders user message with correct styling', () => {
    const message = createMessage('1', 'user', 'Hello')
    const wrapper = mount(MessageItem, {
      props: { message },
    })

    const messageItem = wrapper.find('.message-item')
    expect(messageItem.classes()).toContain('message-user')
    expect(messageItem.classes()).not.toContain('message-assistant')
    expect(messageItem.classes()).not.toContain('message-system')
  })

  it('renders assistant message with correct styling', () => {
    const message = createMessage('1', 'assistant', 'Hi there')
    const wrapper = mount(MessageItem, {
      props: { message },
    })

    const messageItem = wrapper.find('.message-item')
    expect(messageItem.classes()).toContain('message-assistant')
    expect(messageItem.classes()).not.toContain('message-user')
    expect(messageItem.classes()).not.toContain('message-system')
  })

  it('renders system message with correct styling', () => {
    const message = createMessage('1', 'system', 'System notification')
    const wrapper = mount(MessageItem, {
      props: { message },
    })

    const messageItem = wrapper.find('.message-item')
    expect(messageItem.classes()).toContain('message-system')
    expect(messageItem.classes()).not.toContain('message-user')
    expect(messageItem.classes()).not.toContain('message-assistant')
  })

  it('displays correct role label for user', () => {
    const message = createMessage('1', 'user', 'Hello')
    const wrapper = mount(MessageItem, {
      props: { message },
    })

    expect(wrapper.find('.message-role').text()).toBe('你')
  })

  it('displays correct role label for assistant', () => {
    const message = createMessage('1', 'assistant', 'Hi there')
    const wrapper = mount(MessageItem, {
      props: { message },
    })

    expect(wrapper.find('.message-role').text()).toBe('AI助手')
  })

  it('displays correct role label for system', () => {
    const message = createMessage('1', 'system', 'System message')
    const wrapper = mount(MessageItem, {
      props: { message },
    })

    expect(wrapper.find('.message-role').text()).toBe('系统')
  })

  it('displays correct avatar for user', () => {
    const message = createMessage('1', 'user', 'Hello')
    const wrapper = mount(MessageItem, {
      props: { message },
    })

    const avatar = wrapper.find('.avatar-icon')
    expect(avatar.text()).toBe('👤')
    expect(avatar.classes()).toContain('avatar-user')
  })

  it('displays correct avatar for assistant', () => {
    const message = createMessage('1', 'assistant', 'Hi there')
    const wrapper = mount(MessageItem, {
      props: { message },
    })

    const avatar = wrapper.find('.avatar-icon')
    expect(avatar.text()).toBe('🤖')
    expect(avatar.classes()).toContain('avatar-assistant')
  })

  it('displays formatted timestamp', () => {
    const message = createMessage('1', 'user', 'Hello')
    const wrapper = mount(MessageItem, {
      props: { message },
    })

    const time = wrapper.find('.message-time').text()
    // The timestamp should be formatted as HH:MM in zh-CN locale
    expect(time).toMatch(/^\d{2}:\d{2}$/)
  })

  it('displays message header with role and time', () => {
    const message = createMessage('1', 'user', 'Hello')
    const wrapper = mount(MessageItem, {
      props: { message },
    })

    expect(wrapper.find('.message-header').exists()).toBe(true)
    expect(wrapper.find('.message-role').exists()).toBe(true)
    expect(wrapper.find('.message-time').exists()).toBe(true)
  })

  it('displays streaming indicator when isStreaming is true', () => {
    const message = createMessage('1', 'assistant', 'Hello', true)
    const wrapper = mount(MessageItem, {
      props: { message },
    })

    expect(wrapper.find('.cursor-blink').exists()).toBe(true)
    expect(wrapper.find('.cursor-blink').text()).toBe('▌')
  })

  it('does not display streaming indicator when isStreaming is false', () => {
    const message = createMessage('1', 'assistant', 'Hello', false)
    const wrapper = mount(MessageItem, {
      props: { message },
    })

    expect(wrapper.find('.cursor-blink').exists()).toBe(false)
  })

  it('adds streaming class to message text when streaming', () => {
    const message = createMessage('1', 'assistant', 'Hello', true)
    const wrapper = mount(MessageItem, {
      props: { message },
    })

    const messageText = wrapper.find('.message-text')
    expect(messageText.classes()).toContain('streaming')
  })

  it('does not add streaming class when not streaming', () => {
    const message = createMessage('1', 'assistant', 'Hello', false)
    const wrapper = mount(MessageItem, {
      props: { message },
    })

    const messageText = wrapper.find('.message-text')
    expect(messageText.classes()).not.toContain('streaming')
  })

  it('displays placeholder text when content is empty', () => {
    const message = createMessage('1', 'assistant', '')
    const wrapper = mount(MessageItem, {
      props: { message },
    })

    expect(wrapper.find('.message-text').text()).toBe('...')
  })

  it('displays placeholder with streaming indicator when content is empty and streaming', () => {
    const message = createMessage('1', 'assistant', '', true)
    const wrapper = mount(MessageItem, {
      props: { message },
    })

    const messageText = wrapper.find('.message-text')
    expect(messageText.text()).toContain('...')
    expect(wrapper.find('.cursor-blink').exists()).toBe(true)
  })

  it('renders long message content correctly', () => {
    const longContent = 'This is a very long message that should still be displayed correctly without any issues. It contains multiple sentences and should wrap properly.'
    const message = createMessage('1', 'user', longContent)
    const wrapper = mount(MessageItem, {
      props: { message },
    })

    expect(wrapper.find('.message-text').text()).toBe(longContent)
  })

  it('renders message with special characters', () => {
    const specialContent = 'Hello! How are you? 😊 <script>alert("test")</script>'
    const message = createMessage('1', 'user', specialContent)
    const wrapper = mount(MessageItem, {
      props: { message },
    })

    expect(wrapper.find('.message-text').text()).toBe(specialContent)
  })

  it('renders message with newlines', () => {
    const multilineContent = 'Line 1\nLine 2\nLine 3'
    const message = createMessage('1', 'user', multilineContent)
    const wrapper = mount(MessageItem, {
      props: { message },
    })

    // The content should preserve newlines (white-space: pre-wrap)
    expect(wrapper.find('.message-text').text()).toBe(multilineContent)
  })

  it('has correct structure for message item', () => {
    const message = createMessage('1', 'user', 'Hello')
    const wrapper = mount(MessageItem, {
      props: { message },
    })

    expect(wrapper.find('.message-item').exists()).toBe(true)
    expect(wrapper.find('.message-avatar').exists()).toBe(true)
    expect(wrapper.find('.message-content').exists()).toBe(true)
  })

  it('updates when message prop changes', async () => {
    const message = createMessage('1', 'user', 'Hello')
    const wrapper = mount(MessageItem, {
      props: { message },
    })

    expect(wrapper.find('.message-text').text()).toBe('Hello')

    await wrapper.setProps({
      message: createMessage('1', 'user', 'Updated content'),
    })

    expect(wrapper.find('.message-text').text()).toBe('Updated content')
  })

  it('updates role styling when message role changes', async () => {
    const message = createMessage('1', 'user', 'Hello')
    const wrapper = mount(MessageItem, {
      props: { message },
    })

    expect(wrapper.find('.message-item').classes()).toContain('message-user')

    await wrapper.setProps({
      message: createMessage('1', 'assistant', 'Hi there'),
    })

    expect(wrapper.find('.message-item').classes()).toContain('message-assistant')
    expect(wrapper.find('.message-item').classes()).not.toContain('message-user')
  })

  it('updates streaming state when isStreaming changes', async () => {
    const message = createMessage('1', 'assistant', 'Hello', false)
    const wrapper = mount(MessageItem, {
      props: { message },
    })

    expect(wrapper.find('.cursor-blink').exists()).toBe(false)

    await wrapper.setProps({
      message: createMessage('1', 'assistant', 'Hello streaming', true),
    })

    expect(wrapper.find('.cursor-blink').exists()).toBe(true)
  })
})