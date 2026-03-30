# Modbus Slave Simulator (ModbusOpcGateway)

一个基于 **.NET 10** 的**高可靠、高可扩展的 Modbus TCP 从站模拟器**，用于模拟真实的 PLC 或传感器设备。支持多种数据生成模式、动态配置、结构化日志和单元测试。

**附带 Blazor Server Web HMI 界面**，提供实时监控、历史趋势图、告警管理等完整 SCADA 功能。

## 📌 核心特性

### ✅ Modbus 模拟器
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

### 🔌 多协议支持
- **OPC UA Server**：基于 OPC Foundation .NET Standard SDK
  - 地址空间：`Objects` → `Industrial` → 变量节点
  - 支持 Subscription（订阅推送），数据变化实时通知客户端
  - 自动证书管理，支持安全连接
  - 验证工具：UA Expert
  - 端口：4840（可配置）
- **MQTT Publisher**：基于 MQTTnet 5.x
  - Topic 格式：`industrial/{sensorId}/{parameter}`
  - QoS 1（至少一次）保证消息可靠传输
  - JSON 消息格式：`{parameter, value, timestamp, sensorId}`
  - 条件式认证支持（Username/Password 可选）
  - 验证工具：mosquitto_sub、MQTT.fx
  - 端口：1883（可配置）
- **协议独立开关**：每个协议可通过 `Enabled` 字段独立启用/禁用

### 🖥️ Web HMI 界面 (BlazorScadaHmi)
- **实时监控面板**：温度、压力、设备状态实时展示
- **📊 历史趋势图**：数据可视化，支持 1/5/15 分钟时间范围选择
- **🔔 告警系统**：
  - 实时告警检测（高温、低压、设备停止等）
  - 告警历史记录
  - 告警确认机制
  - 可配置告警规则
- **⚡ SignalR 实时推送**：毫秒级数据更新，无需刷新页面
- **📡 MQTT 数据订阅**：支持从 MQTT Broker 订阅数据，实现与模拟器/网关分离部署
- **🔐 用户权限控制**：
  - Cookie 认证
  - 三级角色：Admin / Operator / Viewer
  - 操作级别权限控制

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

### 运行 Web HMI 界面

```bash
dotnet run --project BlazorScadaHmi/BlazorScadaHmi.csproj
```

访问 https://localhost:5001 或 http://localhost:5000

**默认用户**：
| 用户名 | 密码 | 角色 |
|--------|------|------|
| admin | admin | Admin |
| operator | operator | Operator |
| viewer | viewer | Viewer |

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
  },
  "OpcUa": {
    "Enabled": true,               // 启用 OPC UA Server
    "Port": 4840,                  // OPC UA 端口
    "ApplicationName": "ModbusOpcGateway",
    "ApplicationUri": "urn:localhost:ModbusOpcGateway"
  },
  "Mqtt": {
    "Enabled": true,               // 启用 MQTT Publisher
    "Broker": "localhost",         // MQTT Broker 地址
    "Port": 1883,                  // MQTT 端口
    "TopicPrefix": "industrial",   // Topic 前缀
    "Username": "gateway",         // 认证用户名（可选）
    "Password": "secret"           // 认证密码（可选）
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

## 🏗️ 系统架构

### 三项目独立部署架构

本项目采用**模块化、可独立部署**的架构设计，三个项目通过 **MQTT** 进行数据解耦：

```
┌─────────────────────────────────────────────────────────────────────────┐
│  数据源层                                                                │
├─────────────────────────────┬───────────────────────────────────────────┤
│  ModbusOpcGateway           │   PlcGateway                              │
│  (Modbus/OPC UA 模拟器)      │   (Modbus/OPC UA 网关)                     │
│  ┌──────────────────────┐   │   ┌──────────────────────┐                │
│  │ GeneratorService     │   │   │ ModbusClientService  │                │
│  │ ModbusServerService  │   │   │ OpcUaClientService   │                │
│  │ OpcUaServerService   │   │   └──────────┬───────────┘                │
│  └──────────┬───────────┘   │              │                            │
│             │               │              ▼                            │
│             ▼               │   ┌──────────────────────┐                │
│  ┌──────────────────────┐   │   │ MqttPublishService   │                │
│  │ MqttPublisherService │   │   └──────────┬───────────┘                │
│  └──────────┬───────────┘   └──────────────┼────────────────────────────┘
│             │                              │
│             └──────────────┬───────────────┘
│                            ▼
│                   ┌─────────────────┐
│                   │   MQTT Broker   │  ← 统一数据总线
│                   │  (Mosquitto等)  │
│                   └────────┬────────┘
│                            │
└────────────────────────────┼─────────────────────────────────────────────┘
                             │ MQTT Subscribe
                             ▼
┌─────────────────────────────────────────────────────────────────────────┐
│  展示层: BlazorScadaHmi                                                  │
│  ┌──────────────────────────────────────────────────────────────────┐   │
│  │  MqttSubscriberService → SharedData → ScadaBroadcastService      │   │
│  │                           ↓                      ↓               │   │
│  │                     DataHistoryService      ScadaHub (SignalR)   │   │
│  │                           ↓                      ↓               │   │
│  │                     AlarmService          浏览器客户端           │   │
│  └──────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────┘
```

### 数据流向

| 项目 | 角色 | 数据流向 |
|------|------|----------|
| **ModbusOpcGateway** | 数据源 | 本地生成 → MQTT 发布 |
| **PlcGateway** | 数据采集 | Modbus/OPC 读取 → MQTT 发布 |
| **BlazorScadaHmi** | 数据展示 | MQTT 订阅 → SignalR 推送 |

### 部署灵活性

- **单机部署**：三个项目运行在同一机器，通过 localhost:1883 通信
- **分布式部署**：
  - ModbusOpcGateway 运行在服务器 A（连接现场设备）
  - PlcGateway 运行在服务器 B（连接 PLC）
  - BlazorScadaHmi 运行在服务器 C（Web 展示）
- **混合部署**：根据现场需求灵活组合

---

## 🏛️ 项目结构

```
ModbusOpcGateway/                 # Modbus 模拟器（控制台/Windows 服务）
├── Program.cs                    # 主程序
├── appsettings.json              # 配置文件
└── logs/                         # 日志输出目录

Industrial.Core/                  # 核心类库（共享）
├── Services/
│   ├── AlarmService.cs           # 告警检测服务
│   ├── AlarmBroadcastService.cs  # 告警 SignalR 广播
│   ├── DataHistoryService.cs     # 历史数据缓存
│   └── AuthService.cs            # 用户认证服务
├── Models/
│   ├── Alarm.cs                  # 告警模型
│   └── User.cs                   # 用户模型
├── ScadaHub.cs                   # SignalR Hub
├── SharedData.cs                 # 线程安全数据模型
├── GeneratorService.cs           # 数据生成后台服务
├── ModbusServerService.cs        # Modbus TCP 服务
├── OpcUaServerService.cs         # OPC UA Server 服务
├── MqttPublisherService.cs       # MQTT Publisher 服务
├── MqttSubscriberService.cs      # MQTT Subscriber 服务（HMI 数据订阅）
├── RegisterMap.cs                # 寄存器定义
└── AppSettingsMap.cs             # 配置类

BlazorScadaHmi/                   # Web HMI 界面
├── Components/
│   ├── Pages/
│   │   ├── ScadaMonitor.razor    # 监控面板
│   │   ├── Trends.razor          # 历史趋势图
│   │   ├── Alarms.razor          # 告警管理
│   │   └── Login.razor           # 登录页面
│   ├── Charts/
│   │   └── TrendChart.razor      # 趋势图组件
│   ├── Alarms/
│   │   ├── ActiveAlarmsTable.razor    # 活动告警表
│   │   ├── AlarmHistoryTable.razor    # 告警历史表
│   │   └── AlarmRulesTable.razor      # 告警规则表
│   └── Layout/                   # 布局组件
├── Services/
│   ├── CustomAuthStateProvider.cs    # 认证状态提供者
│   └── UiStateService.cs             # UI 状态服务
├── Program.cs                    # Blazor 程序入口
├── appsettings.json              # 用户配置
└── logs/                         # 日志输出目录

PlcGateway/                       # PLC 网关（独立部署）
├── Services/
│   ├── ModbusClientService.cs    # Modbus 客户端服务
│   ├── OpcUaClientService.cs     # OPC UA 客户端服务
│   └── MqttPublishService.cs     # MQTT 发布服务
├── Configuration/
│   └── GatewaySettings.cs        # 网关配置类
├── Worker.cs                     # 后台工作器
├── Program.cs                    # 程序入口
└── appsettings.json              # 配置文件

ModbusOpcGateway_xUnit/           # 单元测试
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

## 👥 用户角色权限

| 角色 | 查看数据 | 参数调整 | 告警确认 | 故障注入 |
|------|:--------:|:--------:|:--------:|:--------:|
| **Admin** | ✅ | ✅ | ✅ | ✅ |
| **Operator** | ✅ | ✅ | ✅ | ❌ |
| **Viewer** | ✅ | ❌ | ❌ | ❌ |

---

## 💡 常见用途

1. **测试 Modbus 客户端** - 无需真实 PLC
2. **学习 Modbus 协议** - 可配置的测试场景
3. **性能测试** - 调整噪声和延迟参数
4. **故障场景模拟** - Frozen 模式、网络延迟等
5. **SCADA 系统开发** - 完整的 HMI 界面参考实现
6. **告警系统原型** - 可配置规则、实时检测、历史记录

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

**版本**：v1.4.0  
**维护者**：am-workspace  
**更新时间**：2026-03-30

### v1.4.0 更新内容

- **新增 MQTT 订阅功能**：BlazorScadaHmi 支持从 MQTT Broker 订阅数据
- **架构升级**：支持三项目独立部署（ModbusOpcGateway / PlcGateway / BlazorScadaHmi）
- **数据解耦**：通过 MQTT 统一数据总线，实现数据源与展示的分离
- **新增 PlcGateway 项目**：独立的 Modbus/OPC UA 数据采集网关
