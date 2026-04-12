# Gateway Memory

项目重要变更记录，供开发时快速回顾上下文。

---

## 2026-04-12 | 拆分 Industrial.Core 为双核心库

将 Industrial.Core 复制为 ModbusOpcGateway.Core（命名空间 `ModbusOpcGateway.Core`），ModbusOpcGateway 及其单元测试改为引用新库，BlazorScadaHmi 继续引用原 Industrial.Core。两个项目从此独立演进，互不影响。

## 2026-04-12 | README 更新至 v1.6.0

更新系统架构图、项目结构和版本日志，反映核心库拆分后的双核心库架构。
