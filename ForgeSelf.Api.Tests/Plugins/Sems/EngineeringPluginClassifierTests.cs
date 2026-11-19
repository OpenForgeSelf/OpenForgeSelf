using FluentAssertions;
using ForgeSelf.Api.Plugins.Sems.Services;
using Xunit;

namespace ForgeSelf.Api.Tests.Plugins.Sems;

/// <summary>
/// 工程插件包名识别与分组（需求 v1.0 F19）。
/// eslint 相关条目按「只排除配置包、计入 eslint 插件」解释，含无后缀的
/// <c>@typescript-eslint/eslint-plugin</c>。
/// </summary>
public class EngineeringPluginClassifierTests
{
    [Theory]
    [InlineData("@vitejs/plugin-vue", "构建")]
    [InlineData("vite-plugin-dts", "构建")]
    [InlineData("foo-vite-plugin", "构建")]
    [InlineData("rollup-plugin-visualizer", "构建")]
    [InlineData("unplugin-auto-import", "构建")]
    [InlineData("html-webpack-plugin", "构建")]
    [InlineData("webpack-plugin-serve", "构建")]
    [InlineData("Vite-Plugin-Dts", "构建")]
    [InlineData("@VITEJS/PLUGIN-VUE", "构建")]
    [InlineData("@babel/plugin-proposal-class-properties", "转译")]
    [InlineData("babel-plugin-import", "转译")]
    [InlineData("eslint-plugin-vue", "代码质量")]
    [InlineData("@typescript-eslint/eslint-plugin", "代码质量")]
    [InlineData("@foo/eslint-plugin-bar", "代码质量")]
    [InlineData("prettier-plugin-tailwindcss", "代码质量")]
    [InlineData("ESLint-Plugin-Vue", "代码质量")]
    [InlineData("@vue/cli-plugin-babel", "框架")]
    [InlineData("vue-cli-plugin-stylelint", "框架")]
    public void TryClassify_Matches_Known_Plugins(string name, string group)
    {
        EngineeringPluginClassifier.TryClassify(name, out var actual).Should().BeTrue();
        actual.Should().Be(group);
    }

    [Theory]
    [InlineData("eslint-config-prettier")]
    [InlineData("@vue/eslint-config-typescript")]
    [InlineData("vue")]
    [InlineData("vite")]
    [InlineData("eslint")]
    [InlineData("typescript")]
    [InlineData("sems_list_projects")]
    [InlineData("sems_register_project")]
    [InlineData("mcp_list_tools")]
    [InlineData("")]
    [InlineData("eslint-plugin")] // 无后缀，不算 eslint-plugin-*
    public void TryClassify_Rejects_Config_Ordinary_And_Host_Tools(string name)
    {
        EngineeringPluginClassifier.TryClassify(name, out var group).Should().BeFalse();
        group.Should().BeEmpty();
    }
}
