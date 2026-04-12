namespace ModbusOpcGateway.Core.Models
{
    /// <summary>
    /// 告警级别
    /// </summary>
    public enum AlarmLevel
    {
        Warning = 1,    // 警告
        Critical = 2    // 严重
    }

    /// <summary>
    /// 告警比较运算符
    /// </summary>
    public enum AlarmOperator
    {
        GreaterThan = 1,    // >
        LessThan = 2,       // <
        EqualTo = 3,        // ==
        NotEqualTo = 4      // !=
    }

    /// <summary>
    /// 告警规则定义
    /// </summary>
    public class AlarmRule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string Name { get; set; } = "";           // 规则名称，如"高温告警"
        public string Parameter { get; set; } = "";      // 监控参数：Temperature, Pressure, Status
        public AlarmOperator Operator { get; set; }      // 比较运算符
        public float Threshold { get; set; }             // 阈值
        public AlarmLevel Level { get; set; }            // 告警级别
        public bool IsEnabled { get; set; } = true;      // 是否启用
        public string? Description { get; set; }          // 描述
    }

    /// <summary>
    /// 告警记录
    /// </summary>
    public class AlarmRecord
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string RuleId { get; set; } = "";         // 触发规则ID
        public string RuleName { get; set; } = "";       // 规则名称
        public AlarmLevel Level { get; set; }             // 告警级别
        public string Parameter { get; set; } = "";      // 参数名
        public float ActualValue { get; set; }            // 实际值
        public float Threshold { get; set; }              // 阈值
        public DateTime TriggeredAt { get; set; }         // 触发时间
        public DateTime? AcknowledgedAt { get; set; }     // 确认时间
        public string? AcknowledgedBy { get; set; }       // 确认人
        public bool IsAcknowledged => AcknowledgedAt.HasValue;
        public bool IsActive { get; set; } = true;        // 是否未恢复
    }

    /// <summary>
    /// 告警状态变更事件参数
    /// </summary>
    public class AlarmTriggeredEventArgs : EventArgs
    {
        public AlarmRecord Record { get; }

        public AlarmTriggeredEventArgs(AlarmRecord record)
        {
            Record = record;
        }
    }
}
