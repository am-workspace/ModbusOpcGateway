using Xunit;
using FluentAssertions;
using ModernGateway;
using System.Threading.Tasks;
using System.Threading;

public class SharedDataTests
{
    [Fact]
    public void Update_And_Get_Should_Be_Consistent()
    {
        // Arrange
        var data = new SharedData();

        // Act
        data.Update(25.5f, 100.5f, true);
        var snapshot = data.Snapshot();

        // Assert
        snapshot.Temp.Should().Be(25.5f);
        snapshot.Press.Should().Be(100.5f);
        snapshot.Status.Should().BeTrue();
    }

    [Fact]
    public void GetTempReg_Should_Scale_Correctly()
    {
        // Arrange
        var data = new SharedData();
        data.Update(25.55f, 100.0f, false);

        // Act
        var regValue = data.GetTempReg();

        // Assert
        // 25.55 * 10 = 255.5 -> cast to ushort -> 255
        regValue.Should().Be(255);
    }

    [Fact]
    public async Task Thread_Safety_Test()
    {
        // Arrange
        var data = new SharedData();
        var cts = new CancellationTokenSource();

        // Act: 模拟高并发写入
        var task1 = Task.Run(() => { for (int i = 0; i < 1000; i++) data.Update(i, i, true); }, TestContext.Current.CancellationToken);
        var task2 = Task.Run(() => { for (int i = 0; i < 1000; i++) data.Update(-i, -i, false); }, TestContext.Current.CancellationToken);

        await Task.WhenAll(task1, task2);

        // Assert: 只要不崩溃、不死锁，就算通过
        // 我们可以尝试获取快照
        var snap = data.Snapshot();
        snap.Should().NotBeNull();
    }
}