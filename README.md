# Modbus Slave Simulator (ModbusOpcGateway)

一个基于 **.NET 10** 的**高可靠、高可扩展的 Modbus TCP 从站模拟器**，用于模拟真实的 PLC 或传感器设备。支持多种数据生成模式、动态配置、结构化日志和单元测试。

## 📌 核心特性

### ✅ 功能特性
- **Modbus TCP 从站实现**：基于 NModbus4.NetCore，完全支持 Modbus 功能码（FC03、FC05、FC06、FC16 等）
- **多模式数据生成**：
  - `Random` - 纯随机波动
  - `Trend` - 正弦波趋势变化（模拟物理过程周期性）
  - `Frozen` - 数值冻结不变（故障模拟）
- **实时控制参数**：
  - 噪声系数调整（动态增加/减少数据抖动）
  - 响应延迟模拟（测试超时处理）
  - 模式动态切换（无需重启）
- **⚡ 配置热重载**：修改 `appsettings.json` 后自动生效，无需重启服务（支持端口、SlaveId、IP 变更自动重启）
- **🔧 故障模拟功能**：通过 Modbus 寄存器触发异常数据，测试上位机异常处理能力
- **🪟 Windows 服务支持**：支持部署为 Windows 服务，7×24 小时运行
- **线程安全**：采用细粒度锁保护，支持并发读写
- **结构化日志**：Serilog 集成，支持控制台、文件和 Windows 事件日志输出
- **可配置化**：所有参数（端口、IP、生成周期、初始模式等）支持 `appsettings.json` 配置
- **单元测试**：xUnit 测试框架，覆盖核心逻辑（SharedData、RegisterMap 等）
- **文档生成**：自动生成 `REGISTER_MAP.md`，详细说明每个寄存器的地址、类型、缩放因子

---

## 🚀 快速开始

### 环境要求
- **.NET 10** SDK（或更新版本）
- Windows / Linux / macOS

### 安装与运行

#### 1️⃣ 克隆项目
```bash
git clone https://github.com/am-workspace/ModbusOpcGateway
cd ModbusOpcGateway
```

#### 2️⃣ 构建项目
```bash
dotnet build
```

#### 3️⃣ 运行模拟器
```bash
dotnet run --project ModbusOpcGateway/ModbusOpcGateway.csproj
```

#### 4️⃣ 预期输出
```
Loaded Config: Port=5020, Mode=Random
=== Modbus Slave Simulator Starting ===
=== Generating Register Map documentation ===
[Modbus] Listening on 0.0.0.0:5020
[Modbus] Server started with dynamic data binding.
[Gen] Generated data: Temp=25.34, Press=102.12, Status=True
...
Press Ctrl+C to stop.
```

---

## 📝 配置说明

编辑 `appsettings.json` 自定义模拟器行为：

```json
{
  "Modbus": {
    "Port": 5020,                  // Modbus TCP 监听端口
    "SlaveId": 1,                  // 从站单位 ID
    "IpAddress": "0.0.0.0"         // 绑定 IP (0.0.0.0 = 所有网卡)
  },
  "Simulation": {
    "InitialMode": "Random",       // 初始生成模式 (Random/Trend/Frozen)
    "TimeoutMs": 1000,             // 冻结模式检查间隔 (ms)
    "UpdateIntervalMs": 2000,      // 数据生成间隔 (ms)
    "DefaultNoise": 1.0,           // 初始噪声系数 (0.0-10.0)
    "DefaultDelayMs": 0            // 初始响应延迟 (ms)
  }
}
```

---

## 📦 寄存器映射

### 保持寄存器 (Holding Registers)

| 地址 | 名称 | 读写 | 说明 | 缩放 |
|------|------|------|------|------|
| **0** | Temperature | R | 当前温度（°C） | ÷10 |
| **1** | Pressure | R | 当前压力（kPa） | ÷10 |
| **10** | SimulationMode | RW | 模式：0=Random, 1=Trend, 2=Frozen | - |
| **11** | NoiseMultiplier | RW | 噪声系数 | ÷100 |
| **12** | ResponseDelayMs | RW | 响应延迟（ms） | - |
| **100** | FaultInjectionControl | RW | 故障注入控制：0=正常, 1=异常温度, 2=异常压力, 3=冻结数据 | - |

### 线圈 (Coils)

| 地址 | 名称 | 读写 | 说明 |
|------|------|------|------|
| **0** | StatusCoil | RW | 设备状态 (True=运行, False=停止) |

### 使用示例

**读取温度**（Modbus 功能码 03）：
```
从站ID: 1, 地址: 0, 数量: 1
响应: 253 = 25.3°C
```

**切换为 Trend 模式**（Modbus 功能码 06）：
```
从站ID: 1, 地址: 10, 值: 1
```

---

## 🪟 Windows 服务部署

### 安装服务（以管理员身份运行 PowerShell）

```powershell
# 编译 Release 版本
dotnet build -c Release

# 安装并启动服务
.\Install-Service.ps1
```

### 管理服务

```powershell
# 启动服务
Start-Service -Name ModbusOpcGateway

# 停止服务
Stop-Service -Name ModbusOpcGateway

# 查看服务状态
Get-Service -Name ModbusOpcGateway

# 查看日志
Get-Content .\logs\modbus-simulator-*.log -Tail 50
```

### 卸载服务

```powershell
.\Uninstall-Service.ps1
```

---

## ⚡ 配置热重载

修改 `appsettings.json` 后，服务会自动检测变更：

| 配置项 | 热重载行为 |
|--------|-----------|
| `Simulation` 相关 | 立即生效，无需重启 |
| `Modbus.Port` / `SlaveId` / `IpAddress` | 自动重启服务，重新绑定端口 |

---

## 🔧 故障模拟

通过向寄存器 **100** 写入控制码，触发异常数据：

| 写入值 | 效果 | 用途 |
|--------|------|------|
| 0 | 恢复正常数据生成 | 结束测试 |
| 1 | 温度变为 999.9°C | 测试数据上限校验 |
| 2 | 压力变为 -50.0 kPa | 测试数据下限校验 |
| 3 | 冻结数据更新 | 测试超时处理 |

**示例**（使用 Modbus 功能码 06）：
```
从站ID: 1, 地址: 100, 值: 1  → 触发异常温度
从站ID: 1, 地址: 100, 值: 0  → 恢复正常
```

---

## 🧪 单元测试

```bash
dotnet test ModbusOpcGateway_xUnit/ModbusOpcGateway_xUnit.csproj
```

测试覆盖：
- ✅ SharedData 并发安全性
- ✅ RegisterMap 验证
- ✅ 数据生成器逻辑
- ✅ Modbus 协议处理

---

## 🏛️ 项目结构

```
ModbusOpcGateway/
├── Program.cs                    # 主程序
├── GeneratorService.cs           # 数据生成后台服务
├── ModbusServerService.cs        # Modbus TCP 服务（支持热重载）
├── SharedData.cs                 # 线程安全数据模型（含故障模拟）
├── RegisterMap.cs                # 寄存器定义
├── TimeProvider.cs               # 时间接口（支持单测）
├── RandomProvider.cs             # 随机数接口（支持单测）
├── AppSettings.cs                # 配置类
├── appsettings.json              # 配置文件
└── logs/                         # 日志输出目录

ModbusOpcGateway_xUnit/
├── SharedDataTests.cs            # 数据层单测
├── RegisterMapTests.cs           # 寄存器映射单测
├── AppSettingsTests.cs           # 配置类单测
├── ProviderTests.cs              # Provider 单测
└── ...

# 部署脚本
├── Install-Service.ps1           # Windows 服务安装脚本
└── Uninstall-Service.ps1         # Windows 服务卸载脚本
```

---

## 🔧 核心概念

### SharedData - 线程安全共享数据
所有读写通过 `lock` 保护，确保数据一致性：

```csharp
// 原子性读取
var (temp, press, status) = data.Snapshot();

// 原子性写入
data.Update(25.5f, 101.2f, true);

// 配置控制（由 Modbus 客户端驱动）
data.SetMode(SharedData.SimulationMode.Trend);
data.SetNoiseMultiplier(0.8f);
```

### RunGenerator - 数据生成器

| 模式 | 特点 | 用途 |
|------|------|------|
| **Random** | 纯随机 ±10% | 基础测试 |
| **Trend** | 随机 + 正弦波 | 周期性过程模拟 |
| **Frozen** | 数值不变 | 故障模拟 |

### RunModbusServer - Modbus 服务

通过事件驱动实现数据映射：
- `DataStoreReadFrom` - 从 SharedData 读取
- `DataStoreWrittenTo` - 写入到 SharedData

---

## 💡 常见用途

1. **测试 Modbus 客户端** - 无需真实 PLC
2. **学习 Modbus 协议** - 可配置的测试场景
3. **性能测试** - 调整噪声和延迟参数
4. **故障场景模拟** - Frozen 模式、网络延迟等

---

## 🐛 故障排查

### 无法连接到 5020 端口
```bash
# 查看占用情况 (Windows)
netstat -ano | findstr :5020

# 修改 appsettings.json 端口
"Port": 5021
```

### 日志文件无法写入
```bash
mkdir logs
# 确保有写入权限
```

---

## 📚 相关资源

- [Modbus 官方规范](http://www.modbus.org)
- [NModbus4.NetCore](https://github.com/NModbus4/NModbus4.NetCore)
- [Serilog 日志](https://serilog.net)
- [.NET 10 文档](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10)

---

## 📄 许可证

MIT License

---

## 🤝 贡献

欢迎 Issue 和 Pull Request！

---

## 📞 联系

- **GitHub**：[am-workspace/ModbusOpcGateway](https://github.com/am-workspace/ModbusOpcGateway)
- **问题反馈**：[Issues](https://github.com/am-workspace/ModbusOpcGateway/issues)

---

**版本**：v1.1.0  
**维护者**：am-workspace  
**更新时间**：2026-03-27
