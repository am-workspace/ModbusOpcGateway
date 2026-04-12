using Xunit;
using FluentAssertions;
using ModbusOpcGateway.Core;
using System;
using System.Threading;
using System.Threading.Tasks;

public class ProviderTests
{
    #region TimeProvider Tests

    [Fact]
    public void TimeProvider_UtcNow_Should_Return_Utc_Time()
    {
        // Arrange
        var timeProvider = new ModbusOpcGateway.Core.TimeProvider();

        // Act
        var result = timeProvider.UtcNow;

        // Assert
        result.Kind.Should().Be(DateTimeKind.Utc);
        // 允许一定的误差（测试执行时间）
        result.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task TimeProvider_Delay_Should_Respect_CancellationToken()
    {
        // Arrange
        var timeProvider = new ModbusOpcGateway.Core.TimeProvider();
        using var cts = new CancellationTokenSource();

        // Act
        cts.Cancel();
        Func<Task> act = async () => await timeProvider.Delay(1000, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task TimeProvider_Delay_Should_Complete_Without_Exception()
    {
        // Arrange
        var timeProvider = new ModbusOpcGateway.Core.TimeProvider();
        using var cts = new CancellationTokenSource();

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await timeProvider.Delay(50, cts.Token);
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(45); // 允许小误差
    }

    #endregion

    #region RandomProvider Tests

    [Fact]
    public void RandomProvider_NextDouble_Should_Return_Value_Between_0_And_1()
    {
        // Arrange
        var randomProvider = new RandomProvider();

        // Act
        var result = randomProvider.NextDouble();

        // Assert
        result.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public void RandomProvider_NextDouble_Should_Return_Different_Values()
    {
        // Arrange
        var randomProvider = new RandomProvider();

        // Act
        var value1 = randomProvider.NextDouble();
        var value2 = randomProvider.NextDouble();

        // Assert
        // 虽然理论上可能相同，但概率极低
        value1.Should().NotBe(value2);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(10)]
    [InlineData(100)]
    public void RandomProvider_Next_Should_Return_Value_LessThan_MaxValue(int maxValue)
    {
        // Arrange
        var randomProvider = new RandomProvider();

        // Act
        var result = randomProvider.Next(maxValue);

        // Assert
        result.Should().BeInRange(0, maxValue - 1);
    }

    [Fact]
    public void RandomProvider_Next_With_MaxValue_2_Should_Return_0_Or_1()
    {
        // Arrange
        var randomProvider = new RandomProvider();
        var results = new System.Collections.Generic.HashSet<int>();

        // Act - 多次调用收集结果
        for (int i = 0; i < 100; i++)
        {
            results.Add(randomProvider.Next(2));
        }

        // Assert - 应该能生成 0 和 1
        results.Should().Contain(0);
        results.Should().Contain(1);
        results.Should().NotContain(2);
    }

    [Fact]
    public void RandomProvider_Should_Be_Deterministic_With_Same_Seed()
    {
        // 注意：当前实现使用固定种子 42，所以是确定性的
        // 这个测试验证相同种子产生相同序列

        // Arrange
        var randomProvider1 = new RandomProvider();
        var randomProvider2 = new RandomProvider();

        // Act
        var sequence1 = new System.Collections.Generic.List<double>();
        var sequence2 = new System.Collections.Generic.List<double>();

        for (int i = 0; i < 10; i++)
        {
            sequence1.Add(randomProvider1.NextDouble());
            sequence2.Add(randomProvider2.NextDouble());
        }

        // Assert - 相同种子的 Random 应该产生相同序列
        sequence1.Should().Equal(sequence2);
    }

    #endregion
}
