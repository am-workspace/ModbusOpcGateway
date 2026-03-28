using Xunit;
using FluentAssertions;
using Industrial.Core;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Linq;

public class AppSettingsTests
{
    [Fact]
    public void ModbusSettings_Should_Have_Default_Values()
    {
        // Arrange & Act
        var settings = new ModbusSettings();

        // Assert
        settings.Port.Should().Be(5020);
        settings.SlaveId.Should().Be(1);
        settings.IpAddress.Should().Be("0.0.0.0");
    }

    [Fact]
    public void SimulationSettings_Should_Have_Default_Values()
    {
        // Arrange & Act
        var settings = new SimulationSettings();

        // Assert
        settings.InitialMode.Should().Be("Random");
        settings.TimeoutMs.Should().Be(1000);
        settings.UpdateIntervalMs.Should().Be(2000);
        settings.DefaultNoise.Should().Be(1.0f);
        settings.DefaultDelayMs.Should().Be(0);
    }

    [Theory]
    [InlineData(1024, true)]   // 最小有效端口
    [InlineData(5020, true)]   // 默认端口
    [InlineData(65535, true)]  // 最大有效端口
    [InlineData(1023, false)]  // 小于最小值
    [InlineData(65536, false)] // 大于最大值
    public void ModbusSettings_Port_Range_Validation(int port, bool isValid)
    {
        // Arrange
        var settings = new ModbusSettings { Port = port };
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(settings);

        // Act
        bool result = Validator.TryValidateObject(settings, validationContext, validationResults, true);

        // Assert
        if (isValid)
        {
            validationResults.Should().BeEmpty();
        }
        else
        {
            validationResults.Should().ContainSingle()
                .Which.MemberNames.Should().Contain("Port");
        }
    }

    [Theory]
    [InlineData(1, true)]    // 最小有效ID
    [InlineData(247, true)]  // 最大有效ID
    [InlineData(0, false)]   // 小于最小值
    [InlineData(248, false)] // 大于最大值
    public void ModbusSettings_SlaveId_Range_Validation(byte slaveId, bool isValid)
    {
        // Arrange
        var settings = new ModbusSettings { SlaveId = slaveId };
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(settings);

        // Act
        bool result = Validator.TryValidateObject(settings, validationContext, validationResults, true);

        // Assert
        if (isValid)
        {
            validationResults.Should().BeEmpty();
        }
        else
        {
            validationResults.Should().ContainSingle()
                .Which.MemberNames.Should().Contain("SlaveId");
        }
    }

    [Fact]
    public void AppSettings_Should_Contain_All_Configuration_Sections()
    {
        // Arrange & Act
        var appSettings = new AppSettings();

        // Assert
        appSettings.Modbus.Should().NotBeNull();
        appSettings.Simulation.Should().NotBeNull();
        appSettings.Serilog.Should().NotBeNull();
    }
}
