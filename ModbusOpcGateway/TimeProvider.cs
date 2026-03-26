using System;
using System.Collections.Generic;
using System.Text;

namespace ModbusOpcGateway
{
    // 时间服务接口：用于替代 Task.Delay
    public interface ITimeProvider
    {
        Task Delay(int milliseconds, CancellationToken token);
        DateTime UtcNow { get; }
    }

    // 实现类 (生产环境用)
    public class TimeProvider : ITimeProvider
    {
        public Task Delay(int ms, CancellationToken token) => Task.Delay(ms, token);
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
