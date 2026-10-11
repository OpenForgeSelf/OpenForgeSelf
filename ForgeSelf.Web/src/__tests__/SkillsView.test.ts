import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import SkillsView from '@/views/SkillsView.vue'
import type { SkillItemDto, SkillDetailDto } from '@/types/skills'

const pushMock = vi.fn()
vi.mock('vue-router', () => ({
  useRouter: () => ({ push: pushMock }),
}))

vi.mock('@/services/skillsApi', () => ({
  skillsApi: {
    fetchSkills: vi.fn(),
    fetchSkillDetail: vi.fn(),
    createSkill: vi.fn(),
    updateSkill: vi.fn(),
    toggleSkill: vi.fn(),
  },
}))

import { skillsApi } from '@/services/skillsApi'

function createMockSkills(): SkillItemDto[] {
  return [
    {
      id: 'skill_1',
      name: '系统诊断',
      description: '诊断系统运行状态和潜在问题',
      category: '系统',
      isEnabled: true,
      toolCount: 3,
      usageCount: 42,
      createdAt: '2024-01-01T00:00:00Z',
      updatedAt: '2024-01-02T00:00:00Z',
    },
    {
      id: 'skill_2',
      name: '代码分析',
      description: '分析代码质量和潜在缺陷',
      category: '开发',
      isEnabled: false,
      toolCount: 0,
      usageCount: 7,
      createdAt: '2024-01-01T00:00:00Z',
      updatedAt: '2024-01-02T00:00:00Z',
    },
    {
      id: 'skill_3',
      name: '数据查询',
      description: '查询数据库和缓存中的数据',
      category: '数据',
      isEnabled: true,
      toolCount: 2,
      usageCount: 15,
      createdAt: '2024-01-01T00:00:00Z',
      updatedAt: '2024-01-02T00:00:00Z',
    },
  ]
}

function mountView() {
  return mount(SkillsView, {
    attachTo: document.body,
    global: {
      stubs: {
        Teleport: true,
      },
    },
  })
}

describe('SkillsView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    pushMock.mockReset()
  })

  afterEach(() => {
    document.body.innerHTML = ''
  })

  describe('加载与渲染', () => {
    it('页面加载时显示加载状态', async () => {
      let resolveFn: (skills: SkillItemDto[]) => void
      const promise = new Promise<SkillItemDto[]>((resolve) => {
        resolveFn = resolve
      })
      vi.mocked(skillsApi.fetchSkills).mockReturnValue(promise)

      const wrapper = mountView()
      await wrapper.vm.$nextTick()

      expect(wrapper.find('.loading-state').exists()).toBe(true)
      expect(wrapper.find('.loading-spinner').exists()).toBe(true)

      resolveFn!(createMockSkills())
      await flushPromises()
      wrapper.unmount()
    })

    it('加载完成后渲染技能卡片', async () => {
      vi.mocked(skillsApi.fetchSkills).mockResolvedValue(createMockSkills())

      const wrapper = mountView()
      await flushPromises()

      expect(wrapper.findAll('.skill-card')).toHaveLength(3)
      expect(wrapper.find('.page-head__title').text()).toBe('技能工坊')
      wrapper.unmount()
    })

    it('加载失败时显示错误信息和重试按钮', async () => {
      vi.mocked(skillsApi.fetchSkills).mockRejectedValue(new Error('网络错误'))

      const wrapper = mountView()
      await flushPromises()

      expect(wrapper.find('.empty-state').exists()).toBe(true)
      expect(wrapper.find('.empty-state__sub').text()).toContain('网络错误')
      expect(wrapper.find('.empty-state .btn-create').exists()).toBe(true)
      wrapper.unmount()
    })

    it('没有技能时显示空状态', async () => {
      vi.mocked(skillsApi.fetchSkills).mockResolvedValue([])

      const wrapper = mountView()
      await flushPromises()

      expect(wrapper.find('.empty-state').exists()).toBe(true)
      expect(wrapper.find('.empty-state__title').text()).toContain('暂无技能')
      wrapper.unmount()
    })
  })

  describe('搜索过滤', () => {
    it('按名称搜索过滤技能', async () => {
      vi.mocked(skillsApi.fetchSkills).mockResolvedValue(createMockSkills())

      const wrapper = mountView()
      await flushPromises()

      const searchInput = wrapper.find('.search-input')
      await searchInput.setValue('代码')
      await wrapper.vm.$nextTick()

      expect(wrapper.findAll('.skill-card')).toHaveLength(1)
      expect(wrapper.find('.card-name').text()).toBe('代码分析')
      wrapper.unmount()
    })

    it('按描述搜索过滤技能', async () => {
      vi.mocked(skillsApi.fetchSkills).mockResolvedValue(createMockSkills())

      const wrapper = mountView()
      await flushPromises()

      const searchInput = wrapper.find('.search-input')
      await searchInput.setValue('数据库')
      await wrapper.vm.$nextTick()

      expect(wrapper.findAll('.skill-card')).toHaveLength(1)
      expect(wrapper.find('.card-name').text()).toBe('数据查询')
      wrapper.unmount()
    })

    it('按分类搜索过滤技能', async () => {
      vi.mocked(skillsApi.fetchSkills).mockResolvedValue(createMockSkills())

      const wrapper = mountView()
      await flushPromises()

      const searchInput = wrapper.find('.search-input')
      await searchInput.setValue('系统')
      await wrapper.vm.$nextTick()

      expect(wrapper.findAll('.skill-card')).toHaveLength(1)
      expect(wrapper.find('.card-name').text()).toBe('系统诊断')
      wrapper.unmount()
    })

    it('搜索无结果时显示空状态', async () => {
      vi.mocked(skillsApi.fetchSkills).mockResolvedValue(createMockSkills())

      const wrapper = mountView()
      await flushPromises()

      const searchInput = wrapper.find('.search-input')
      await searchInput.setValue('不存在')
      await wrapper.vm.$nextTick()

      expect(wrapper.findAll('.skill-card')).toHaveLength(0)
      expect(wrapper.find('.empty-state__title').text()).toContain('没有匹配的技能')
      wrapper.unmount()
    })
  })

  describe('技能状态切换', () => {
    it('点击启用按钮调用 API 并更新状态', async () => {
      const skills = createMockSkills()
      vi.mocked(skillsApi.fetchSkills).mockResolvedValue(skills)
      vi.mocked(skillsApi.toggleSkill).mockResolvedValue({ ...skills[1], isEnabled: true })

      const wrapper = mountView()
      await flushPromises()

      const toggles = wrapper.findAll('.skill-toggle')
      const disabledIndex = skills.findIndex(s => !s.isEnabled)
      const toggleBtn = toggles[disabledIndex]
      expect(toggleBtn.exists()).toBe(true)

      await toggleBtn.trigger('click')
      await flushPromises()

      expect(skillsApi.toggleSkill).toHaveBeenCalledWith('skill_2')
      expect(wrapper.findAll('.skill-card')[disabledIndex].classes()).not.toContain('skill-card--off')
      wrapper.unmount()
    })

    it('点击停用按钮调用 API 并更新状态', async () => {
      const skills = createMockSkills()
      vi.mocked(skillsApi.fetchSkills).mockResolvedValue(skills)
      vi.mocked(skillsApi.toggleSkill).mockResolvedValue({ ...skills[0], isEnabled: false })

      const wrapper = mountView()
      await flushPromises()

      const toggles = wrapper.findAll('.skill-toggle')
      const enabledIndex = skills.findIndex(s => s.isEnabled)
      const toggleBtn = toggles[enabledIndex]
      expect(toggleBtn.exists()).toBe(true)

      await toggleBtn.trigger('click')
      await flushPromises()

      expect(skillsApi.toggleSkill).toHaveBeenCalledWith('skill_1')
      expect(wrapper.findAll('.skill-card')[enabledIndex].classes()).toContain('skill-card--off')
      wrapper.unmount()
    })
  })

  describe('创建技能', () => {
    it('点击创建按钮打开弹窗', async () => {
      vi.mocked(skillsApi.fetchSkills).mockResolvedValue(createMockSkills())

      const wrapper = mountView()
      await flushPromises()

      await wrapper.find('.btn-create').trigger('click')
      await wrapper.vm.$nextTick()

      expect(wrapper.find('.modal-panel').exists()).toBe(true)
      expect(wrapper.find('.modal-title').text()).toBe('创建技能')
      wrapper.unmount()
    })

    it('提交创建技能表单', async () => {
      vi.mocked(skillsApi.fetchSkills).mockResolvedValue(createMockSkills())
      vi.mocked(skillsApi.createSkill).mockResolvedValue({} as SkillDetailDto)

      const wrapper = mountView()
      await flushPromises()

      await wrapper.find('.btn-create').trigger('click')
      await wrapper.vm.$nextTick()

      const inputs = wrapper.findAll('.form-input')
      await inputs[0].setValue('新技能')
      await wrapper.find('.form-textarea').setValue('新技能描述')

      await wrapper.find('form').trigger('submit.prevent')
      await flushPromises()

      expect(skillsApi.createSkill).toHaveBeenCalledWith(
        expect.objectContaining({
          name: '新技能',
          description: '新技能描述',
        })
      )
      expect(skillsApi.fetchSkills).toHaveBeenCalledTimes(2)
      wrapper.unmount()
    })

    it('名称为空时显示错误', async () => {
      vi.mocked(skillsApi.fetchSkills).mockResolvedValue(createMockSkills())

      const wrapper = mountView()
      await flushPromises()

      await wrapper.find('.btn-create').trigger('click')
      await wrapper.vm.$nextTick()

      await wrapper.find('form').trigger('submit.prevent')
      await wrapper.vm.$nextTick()

      expect(wrapper.find('.form-error').exists()).toBe(true)
      expect(wrapper.find('.form-error').text()).toBe('技能名称不能为空')
      expect(skillsApi.createSkill).not.toHaveBeenCalled()
      wrapper.unmount()
    })
  })

  describe('编辑技能', () => {
    it('点击编辑按钮打开弹窗并加载详情', async () => {
      const skills = createMockSkills()
      vi.mocked(skillsApi.fetchSkills).mockResolvedValue(skills)
      vi.mocked(skillsApi.fetchSkillDetail).mockResolvedValue({
        ...skills[0],
        systemPrompt: '你是一个系统诊断助手',
        toolIds: ['tool_1', 'tool_2'],
      } as SkillDetailDto)

      const wrapper = mountView()
      await flushPromises()

      const editButtons = wrapper.findAll('.skill-card__edit')
      await editButtons[0].trigger('click')
      await flushPromises()

      expect(wrapper.find('.modal-panel').exists()).toBe(true)
      expect(wrapper.find('.modal-title').text()).toBe('编辑技能')
      expect(skillsApi.fetchSkillDetail).toHaveBeenCalledWith('skill_1')
      wrapper.unmount()
    })

    it('提交更新技能表单', async () => {
      const skills = createMockSkills()
      vi.mocked(skillsApi.fetchSkills).mockResolvedValue(skills)
      vi.mocked(skillsApi.updateSkill).mockResolvedValue({} as SkillDetailDto)

      const wrapper = mountView()
      await flushPromises()

      const editButtons = wrapper.findAll('.skill-card__edit')
      await editButtons[0].trigger('click')
      await wrapper.vm.$nextTick()

      const inputs = wrapper.findAll('.form-input')
      await inputs[0].setValue('系统诊断V2')

      await wrapper.find('form').trigger('submit.prevent')
      await flushPromises()

      expect(skillsApi.updateSkill).toHaveBeenCalledWith(
        'skill_1',
        expect.objectContaining({ name: '系统诊断V2' })
      )
      wrapper.unmount()
    })
  })
})
