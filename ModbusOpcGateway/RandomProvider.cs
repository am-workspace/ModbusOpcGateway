using System;
using System.Collections.Generic;
using System.Text;

namespace ModernGateway
{
    // 随机数服务接口：用于替代 new Random()
    public interface IRandomProvider
    {
        double NextDouble();
        int Next(int maxValue);
    }

    // 实现类 (生产环境用)
    public class RandomProvider : IRandomProvider
    {
        private readonly Random _rand = new Random(42); // 固定种子保证生产环境也有迹可循
        public double NextDouble() => _rand.NextDouble();
        public int Next(int max) => _rand.Next(max);
    }
}
