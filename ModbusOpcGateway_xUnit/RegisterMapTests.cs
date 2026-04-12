using Xunit;
using FluentAssertions;
using ModbusOpcGateway.Core;
using System.Linq;

public class RegisterMapTests
{
    [Fact]
    public void GenerateMarkdown_Should_Contain_Headers()
    {
        // Act
        var markdown = RegisterMap.GenerateMarkdownTable();

        // Assert
        markdown.Should().Contain("| 地址 (Addr) |");
        markdown.Should().Contain("| 名称 (Name) |");
        markdown.Should().Contain("Temperature");
        markdown.Should().Contain("SimulationMode");
    }

    [Fact]
    public void Definitions_Should_Not_Have_Duplicate_Addresses()
    {
        // Arrange
        var defs = RegisterMap.GetAllDefinitions();

        // Act & Assert
        // 确保没有两个寄存器占用同一个地址（同类型下）
        var holdingRegs = defs.Where(d => d.Type == "HoldingRegister");
        holdingRegs.Select(d => d.Address).Should().OnlyHaveUniqueItems();
    }
}