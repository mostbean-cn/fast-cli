using System.Linq;
using System.Windows.Documents;
using FastCli.Desktop.Utilities;
using Xunit;

namespace FastCli.Tests;

public sealed class MarkdownFlowDocumentBuilderTests
{
    [Fact]
    public void Build_RendersHeadingAndBulletList()
    {
        var document = MarkdownFlowDocumentBuilder.Build("""
            # 新版本

            - 修复设置页遮挡问题
            - 优化更新提示
            """);

        Assert.Equal(2, document.Blocks.Count);
        Assert.IsType<Paragraph>(document.Blocks.FirstBlock);
        Assert.IsType<System.Windows.Documents.List>(document.Blocks.LastBlock);
    }

    [Fact]
    public void Build_RendersCodeAndLink()
    {
        var document = MarkdownFlowDocumentBuilder.Build("""
            请查看 [发布页面](https://github.com/mostbean-cn/fast-cli/releases)

            ```bash
            build\package-exe.bat
            ```
            """);

        var blocks = document.Blocks.ToList();
        Assert.Equal(2, blocks.Count);

        var paragraph = Assert.IsType<Paragraph>(blocks[0]);
        Assert.Contains(paragraph.Inlines, inline => inline is Hyperlink);

        var codeBlock = Assert.IsType<Paragraph>(blocks[1]);
        Assert.Contains("build\\package-exe.bat", new TextRange(codeBlock.ContentStart, codeBlock.ContentEnd).Text);
    }
}
