using System;
using System.Collections.Generic;
using System.Text;

namespace Industrial.Core
{
    /// <summary>
    /// 共享数据中心：存储模拟器的所有状态（传感器数值、配置参数）。
    /// 关键特性：线程安全 (Thread-Safe)。
    /// 它确保"数据生成器"和"Modbus服务器"这两个并发运行的线程，
    /// 在同时读写数据时不会发生冲突或数据损坏。
    /// </summary>
    public class SharedData
    {
        // 1. 锁对象 (Lock Object)
        // 这是线程安全的基石。任何线程想要读取或修改下面的变量，都必须先拿到这把"钥匙"。
        // 同一时间只允许一个线程持有锁，其他线程必须等待。
        readonly object _lock = new object();

        // --- 传感器实时数据 (由 RunGenerator 写入，由 RunModbusServer 读取) ---
        float _temperature = 25.0f; // 当前温度 (°C)
        float _pressure = 100.0f;   // 当前压力 (kPa)
        bool _status = false;       // 设备开关状态 (True=运行, False=停止)

        // --- 控制配置参数 (由外部 Modbus 客户端写入，由 RunGenerator 读取) ---

        // 模拟模式枚举：
        // Random = 纯随机波动
        // Trend  = 正弦波趋势变化
        // Frozen = 数值冻结不变
        public enum SimulationMode { Random = 0, Trend = 1, Frozen = 2 }

        SimulationMode _mode = SimulationMode.Random; // 当前运行模式
        float _noise = 1.0f;        // 噪声系数 (值越大，数据跳动越剧烈)
        int _delayMs = 0;           // 模拟响应延迟 (毫秒)，用于测试超时逻辑

        // ========================================================================
        // 读取操作 (Read Operations)
        // 所有读取方法都包裹在 lock 中，确保读到的是"完整且一致"的数据快照。
        // ========================================================================

        /// <summary>
        /// 获取数据的完整快照。
        /// 返回一个元组 (Temp, Press, Status)。
        /// 用途：供生成器在"冻结模式"下自我检查，或用于日志记录。
        /// </summary>
        public (float Temp, float Press, bool Status) Snapshot()
        {
            // 锁定期间一次性读出三个值，保证它们属于同一个时间点
            lock (_lock) return (_temperature, _pressure, _status);
        }

        /// <summary>
        /// 获取温度寄存器值 (用于 Modbus 协议)。
        /// 注意：这里进行了 *10 操作，将浮点数转为整数 (例如 25.5 -> 255)，
        /// 因为 Modbus 寄存器通常只支持整数传输。
        /// </summary>
        public ushort GetTempReg()
        {
            lock (_lock) return (ushort)(_temperature * 10);
        }

        /// <summary>
        /// 获取压力寄存器值 (用于 Modbus 协议)。
        /// 同样放大 10 倍以保留一位小数精度。
        /// </summary>
        public ushort GetPressReg()
        {
            lock (_lock) return (ushort)(_pressure * 10);
        }

        /// <summary>
        /// 获取线圈状态 (用于 Modbus 协议)。
        /// 直接返回布尔值，对应 Modbus 的 0 或 1。
        /// </summary>
        public bool GetStatusCoil()
        {
            lock (_lock) return _status;
        }

        // --- 以下方法供 RunGenerator 读取配置，决定下一步如何生成数据 ---

        public SimulationMode GetMode()
        {
            lock (_lock) return _mode;
        }

        public float GetNoiseMultiplier()
        {
            lock (_lock) return _noise;
        }

        public int GetResponseDelayMs()
        {
            lock (_lock) return _delayMs;
        }

        // ========================================================================
        // 写入操作 (Write Operations)
        // 所有写入方法也包裹在 lock 中，防止写入过程中被读取打断。
        // ========================================================================

        /// <summary>
        /// 批量更新传感器数据。
        /// 用途：由 RunGenerator 调用，将计算好的新温度、压力、状态存入。
        /// 使用批量更新是为了保证原子性：不会出现温度更新了但压力还是旧的情况。
        /// </summary>
        public void Update(float temp, float press, bool status)
        {
            lock (_lock)
            {
                _temperature = temp;
                _pressure = press;
                _status = status;
            }
        }

        // --- 以下方法供 Modbus 服务器调用，响应外部客户端的"写请求" ---

        /// <summary>
        /// 强制设置状态位。
        /// 用途：外部客户端写入线圈 (Write Single Coil)。
        /// </summary>
        public void SetStatus(bool s)
        {
            lock (_lock) _status = s;
        }

        /// <summary>
        /// 切换模拟模式。
        /// 用途：外部客户端写入寄存器 (Write Single Register)，例如写入 2 切换到冻结模式。
        /// </summary>
        public void SetMode(SimulationMode m)
        {
            lock (_lock) _mode = m;
        }

        /// <summary>
        /// 调整噪声强度。
        /// 用途：外部客户端动态调整模拟环境的干扰程度。
        /// </summary>
        public void SetNoiseMultiplier(float m)
        {
            lock (_lock) _noise = m;
        }

        /// <summary>
        /// 设置模拟延迟。
        /// 用途：外部客户端故意让模拟器变慢，测试系统稳定性。
        /// </summary>
        public void SetResponseDelay(int ms)
        {
            lock (_lock) _delayMs = ms;
        }

        // ========================================================================
        // 故障模拟方法 (Fault Injection)
        // 用于测试上位机的异常处理能力，由外部客户端通过 Modbus 触发
        // ========================================================================

        /// <summary>
        /// 注入异常温度值（模拟传感器故障）。
        /// 温度值设为 999.9°C，明显超出正常范围，用于测试上位机的数据校验逻辑。
        /// </summary>
        public void InjectFaultyTemperature()
        {
            lock (_lock) _temperature = 999.9f;
        }

        /// <summary>
        /// 注入异常压力值（模拟传感器故障）。
        /// 压力值设为 -50.0 kPa，物理上不可能，用于测试上位机的数据校验逻辑。
        /// </summary>
        public void InjectFaultyPressure()
        {
            lock (_lock) _pressure = -50.0f;
        }

        /// <summary>
        /// 冻结数据更新（模拟传感器卡死）。
        /// 切换到 Frozen 模式，数据生成器将停止更新数值。
        /// </summary>
        public void FreezeData()
        {
            SetMode(SimulationMode.Frozen);
        }

        /// <summary>
        /// 恢复正常数据生成。
        /// 切换到 Random 模式，数据生成器恢复正常工作。
        /// </summary>
        public void ResumeNormal()
        {
            SetMode(SimulationMode.Random);
        }
    }
}
