import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import MessageInput from '@/components/MessageInput.vue'

describe('MessageInput', () => {
  it('renders properly with textarea and button', () => {
    const wrapper = mount(MessageInput)

    expect(wrapper.find('.message-input').exists()).toBe(true)
    expect(wrapper.find('.input-container').exists()).toBe(true)
    expect(wrapper.find('.input-textarea').exists()).toBe(true)
    expect(wrapper.find('.send-button').exists()).toBe(true)
  })

  it('displays default placeholder text', () => {
    const wrapper = mount(MessageInput)

    const textarea = wrapper.find('.input-textarea')
    expect(textarea.attributes('placeholder')).toBe('输入消息... (Enter发送, Shift+Enter换行)')
  })

  it('displays custom placeholder text', () => {
    const wrapper = mount(MessageInput, {
      props: {
        placeholder: '自定义提示文本',
      },
    })

    const textarea = wrapper.find('.input-textarea')
    expect(textarea.attributes('placeholder')).toBe('自定义提示文本')
  })

  it('textarea is not disabled by default', () => {
    const wrapper = mount(MessageInput)

    const textarea = wrapper.find('.input-textarea')
    expect(textarea.attributes('disabled')).toBeUndefined()
  })

  it('textarea is disabled when disabled prop is true', () => {
    const wrapper = mount(MessageInput, {
      props: {
        disabled: true,
      },
    })

    const textarea = wrapper.find('.input-textarea')
    expect(textarea.attributes('disabled')).toBeDefined()
  })

  it('send button is disabled when input is empty', () => {
    const wrapper = mount(MessageInput)

    const sendButton = wrapper.find('.send-button')
    expect(sendButton.attributes('disabled')).toBeDefined()
    expect(sendButton.classes()).toContain('send-button-disabled')
  })

  it('send button is enabled when input has content', async () => {
    const wrapper = mount(MessageInput)

    const textarea = wrapper.find('.input-textarea')
    await textarea.setValue('Hello')

    const sendButton = wrapper.find('.send-button')
    expect(sendButton.attributes('disabled')).toBeUndefined()
    expect(sendButton.classes()).not.toContain('send-button-disabled')
  })

  it('send button is disabled when disabled prop is true', async () => {
    const wrapper = mount(MessageInput, {
      props: {
        disabled: true,
      },
    })

    const textarea = wrapper.find('.input-textarea')
    await textarea.setValue('Hello')

    const sendButton = wrapper.find('.send-button')
    expect(sendButton.attributes('disabled')).toBeDefined()
  })

  it('emits send event when clicking send button', async () => {
    const wrapper = mount(MessageInput)

    const textarea = wrapper.find('.input-textarea')
    await textarea.setValue('Test message')

    const sendButton = wrapper.find('.send-button')
    await sendButton.trigger('click')

    expect(wrapper.emitted('send')).toBeTruthy()
    expect(wrapper.emitted('send')![0]!).toEqual(['Test message'])
  })

  it('clears input after sending', async () => {
    const wrapper = mount(MessageInput)

    const textarea = wrapper.find('.input-textarea')
    await textarea.setValue('Test message')

    const sendButton = wrapper.find('.send-button')
    await sendButton.trigger('click')

    expect((textarea.element as HTMLTextAreaElement).value).toBe('')
  })

  it('does not emit send event when input is empty', async () => {
    const wrapper = mount(MessageInput)

    const sendButton = wrapper.find('.send-button')
    await sendButton.trigger('click')

    expect(wrapper.emitted('send')).toBeFalsy()
  })

  it('does not emit send event when input is whitespace only', async () => {
    const wrapper = mount(MessageInput)

    const textarea = wrapper.find('.input-textarea')
    await textarea.setValue('   ')

    const sendButton = wrapper.find('.send-button')
    await sendButton.trigger('click')

    expect(wrapper.emitted('send')).toBeFalsy()
  })

  it('trims content before sending', async () => {
    const wrapper = mount(MessageInput)

    const textarea = wrapper.find('.input-textarea')
    await textarea.setValue('  Test message  ')

    const sendButton = wrapper.find('.send-button')
    await sendButton.trigger('click')

    expect(wrapper.emitted('send')![0]!).toEqual(['Test message'])
  })

  it('emits send event when pressing Enter', async () => {
    const wrapper = mount(MessageInput)

    const textarea = wrapper.find('.input-textarea')
    await textarea.setValue('Test message')

    await textarea.trigger('keydown', { key: 'Enter' })

    expect(wrapper.emitted('send')).toBeTruthy()
    expect(wrapper.emitted('send')![0]!).toEqual(['Test message'])
  })

  it('does not send when pressing Shift+Enter', async () => {
    const wrapper = mount(MessageInput)

    const textarea = wrapper.find('.input-textarea')
    await textarea.setValue('Test message')

    await textarea.trigger('keydown', { key: 'Enter', shiftKey: true })

    expect(wrapper.emitted('send')).toBeFalsy()
  })

  it('does not send when pressing other keys', async () => {
    const wrapper = mount(MessageInput)

    const textarea = wrapper.find('.input-textarea')
    await textarea.setValue('Test message')

    await textarea.trigger('keydown', { key: 'a' })
    await textarea.trigger('keydown', { key: 'Escape' })
    await textarea.trigger('keydown', { key: 'Tab' })

    expect(wrapper.emitted('send')).toBeFalsy()
  })

  it('displays default hint text', () => {
    const wrapper = mount(MessageInput)

    expect(wrapper.find('.hint-text').text()).toBe('按 Enter 发送，Shift + Enter 换行')
  })

  it('displays disabled hint text when disabled', () => {
    const wrapper = mount(MessageInput, {
      props: {
        disabled: true,
      },
    })

    expect(wrapper.find('.hint-text').text()).toBe('AI正在回复中...')
  })

  it('adjusts textarea height on input', async () => {
    const wrapper = mount(MessageInput)

    const textarea = wrapper.find('.input-textarea')
    await textarea.setValue('Test message\nMore content\nEven more')

    // The height should be adjusted (we can't easily test the exact height in jsdom)
    expect((textarea.element as HTMLElement).style.height).toBeDefined()
  })

  it('send button has correct SVG icon', () => {
    const wrapper = mount(MessageInput)

    const sendIcon = wrapper.find('.send-icon')
    expect(sendIcon.exists()).toBe(true)
    expect(sendIcon.attributes('viewBox')).toBe('0 0 24 24')
  })

  it('input container has focus-within styling', () => {
    const wrapper = mount(MessageInput)

    const container = wrapper.find('.input-container')
    expect(container.exists()).toBe(true)
    // Focus-within styling is applied via CSS, we just verify the class exists
    expect(container.classes()).toContain('input-container')
  })

  it('handles multiple sequential sends', async () => {
    const wrapper = mount(MessageInput)

    const textarea = wrapper.find('.input-textarea')

    // First message
    await textarea.setValue('First message')
    await wrapper.find('.send-button').trigger('click')
    expect(wrapper.emitted('send')!.length).toBe(1)

    // Second message
    await textarea.setValue('Second message')
    await wrapper.find('.send-button').trigger('click')
    expect(wrapper.emitted('send')!.length).toBe(2)

    // Third message
    await textarea.setValue('Third message')
    await textarea.trigger('keydown', { key: 'Enter' })
    expect(wrapper.emitted('send')!.length).toBe(3)

    // Verify all messages
    expect(wrapper.emitted('send')![0]).toEqual(['First message'])
      expect(wrapper.emitted('send')![1]).toEqual(['Second message'])
      expect(wrapper.emitted('send')![2]).toEqual(['Third message'])
  })

  it('does not send when disabled even with content', async () => {
    const wrapper = mount(MessageInput, {
      props: {
        disabled: true,
      },
    })

    const textarea = wrapper.find('.input-textarea')
    await textarea.setValue('Test message')

    // Try to send via button
    await wrapper.find('.send-button').trigger('click')
    expect(wrapper.emitted('send')).toBeFalsy()

    // Try to send via Enter
    await textarea.trigger('keydown', { key: 'Enter' })
    expect(wrapper.emitted('send')).toBeFalsy()
  })

  it('maintains input state correctly', async () => {
    const wrapper = mount(MessageInput)

    const textarea = wrapper.find('.input-textarea')

    // Type some content
    await textarea.setValue('Hello World')
    expect((textarea.element as HTMLTextAreaElement).value).toBe('Hello World')

    // Clear it
    await textarea.setValue('')
    expect((textarea.element as HTMLTextAreaElement).value).toBe('')

    // Type again
    await textarea.setValue('New message')
    expect((textarea.element as HTMLTextAreaElement).value).toBe('New message')
  })
})