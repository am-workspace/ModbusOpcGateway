using Industrial.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using System.Collections.Concurrent;

namespace Industrial.Core.Services
{
    /// <summary>
    /// 告警检测服务：实时监控数据，触发告警规则
    /// </summary>
    public class AlarmService : BackgroundService
    {
        private readonly SharedData _sharedData;
        private readonly ILogger _log;
        private readonly List<AlarmRule> _rules = new();
        private readonly ConcurrentDictionary<string, AlarmRecord> _activeAlarms = new();
        private readonly List<AlarmRecord> _alarmHistory = new();
        private readonly object _historyLock = new();

        public event EventHandler<AlarmTriggeredEventArgs>? AlarmTriggered;
        public event EventHandler<AlarmTriggeredEventArgs>? AlarmRecovered;

        public AlarmService(SharedData sharedData)
        {
            _sharedData = sharedData;
            _log = Log.ForContext<AlarmService>();
            InitializeDefaultRules();
        }

        /// <summary>
        /// 初始化默认告警规则
        /// </summary>
        private void InitializeDefaultRules()
        {
            _rules.Add(new AlarmRule
            {
                Name = "高温警告",
                Parameter = "Temperature",
                Operator = AlarmOperator.GreaterThan,
                Threshold = 35.0f,
                Level = AlarmLevel.Warning,
                Description = "温度超过35°C"
            });

            _rules.Add(new AlarmRule
            {
                Name = "高温危险",
                Parameter = "Temperature",
                Operator = AlarmOperator.GreaterThan,
                Threshold = 45.0f,
                Level = AlarmLevel.Critical,
                Description = "温度超过45°C，危险！"
            });

            _rules.Add(new AlarmRule
            {
                Name = "低压警告",
                Parameter = "Pressure",
                Operator = AlarmOperator.LessThan,
                Threshold = 80.0f,
                Level = AlarmLevel.Warning,
                Description = "压力低于80kPa"
            });

            _rules.Add(new AlarmRule
            {
                Name = "高压危险",
                Parameter = "Pressure",
                Operator = AlarmOperator.GreaterThan,
                Threshold = 130.0f,
                Level = AlarmLevel.Critical,
                Description = "压力超过130kPa，危险！"
            });

            _rules.Add(new AlarmRule
            {
                Name = "设备停止",
                Parameter = "Status",
                Operator = AlarmOperator.EqualTo,
                Threshold = 0,
                Level = AlarmLevel.Warning,
                Description = "设备已停止运行"
            });
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _sharedData.DataChanged += OnDataChanged;

            var tcs = new TaskCompletionSource();
            stoppingToken.Register(() =>
            {
                _sharedData.DataChanged -= OnDataChanged;
                tcs.TrySetResult();
            });

            _log.Information("[AlarmService] 告警检测服务已启动，已加载 {RuleCount} 条规则", _rules.Count);

            return tcs.Task;
        }

        private void OnDataChanged(object? sender, DataChangedEventArgs e)
        {
            CheckRules(e.Temperature, e.Pressure, e.Status);
        }

        private void CheckRules(float temperature, float pressure, bool status)
        {
            foreach (var rule in _rules.Where(r => r.IsEnabled))
            {
                var (actualValue, triggered) = rule.Parameter switch
                {
                    "Temperature" => (temperature, CheckCondition(temperature, rule.Operator, rule.Threshold)),
                    "Pressure" => (pressure, CheckCondition(pressure, rule.Operator, rule.Threshold)),
                    "Status" => (status ? 1f : 0f, CheckCondition(status ? 1f : 0f, rule.Operator, rule.Threshold)),
                    _ => (0f, false)
                };

                var ruleKey = $"{rule.Id}";

                if (triggered)
                {
                    // 触发告警
                    if (!_activeAlarms.ContainsKey(ruleKey))
                    {
                        var record = new AlarmRecord
                        {
                            RuleId = rule.Id,
                            RuleName = rule.Name,
                            Level = rule.Level,
                            Parameter = rule.Parameter,
                            ActualValue = actualValue,
                            Threshold = rule.Threshold,
                            TriggeredAt = DateTime.Now,
                            IsActive = true
                        };

                        _activeAlarms[ruleKey] = record;
                        lock (_historyLock) { _alarmHistory.Add(record); }

                        AlarmTriggered?.Invoke(this, new AlarmTriggeredEventArgs(record));
                        _log.Warning("[Alarm] 触发告警: {RuleName}, 参数={Parameter}, 实际值={ActualValue:F2}, 阈值={Threshold:F2}",
                            rule.Name, rule.Parameter, actualValue, rule.Threshold);
                    }
                }
                else
                {
                    // 恢复告警
                    if (_activeAlarms.TryRemove(ruleKey, out var record))
                    {
                        record.IsActive = false;
                        AlarmRecovered?.Invoke(this, new AlarmTriggeredEventArgs(record));
                        _log.Information("[Alarm] 告警恢复: {RuleName}", rule.Name);
                    }
                }
            }
        }

        private static bool CheckCondition(float actual, AlarmOperator op, float threshold)
        {
            return op switch
            {
                AlarmOperator.GreaterThan => actual > threshold,
                AlarmOperator.LessThan => actual < threshold,
                AlarmOperator.EqualTo => Math.Abs(actual - threshold) < 0.001f,
                AlarmOperator.NotEqualTo => Math.Abs(actual - threshold) >= 0.001f,
                _ => false
            };
        }

        /// <summary>
        /// 获取当前活动告警
        /// </summary>
        public List<AlarmRecord> GetActiveAlarms()
        {
            return _activeAlarms.Values.OrderByDescending(a => a.TriggeredAt).ToList();
        }

        /// <summary>
        /// 获取告警历史
        /// </summary>
        public List<AlarmRecord> GetAlarmHistory(int count = 100)
        {
            lock (_historyLock)
            {
                return _alarmHistory.OrderByDescending(a => a.TriggeredAt).Take(count).ToList();
            }
        }

        /// <summary>
        /// 确认告警
        /// </summary>
        public bool AcknowledgeAlarm(string alarmId, string acknowledgedBy)
        {
            var alarm = _activeAlarms.Values.FirstOrDefault(a => a.Id == alarmId);
            if (alarm == null) return false;

            alarm.AcknowledgedAt = DateTime.Now;
            alarm.AcknowledgedBy = acknowledgedBy;
            _log.Information("[Alarm] 告警已确认: {AlarmId} by {User}", alarmId, acknowledgedBy);
            return true;
        }

        /// <summary>
        /// 获取所有告警规则
        /// </summary>
        public List<AlarmRule> GetRules()
        {
            return _rules.ToList();
        }

        /// <summary>
        /// 添加告警规则
        /// </summary>
        public void AddRule(AlarmRule rule)
        {
            _rules.Add(rule);
            _log.Information("[Alarm] 添加规则: {RuleName}", rule.Name);
        }

        /// <summary>
        /// 删除告警规则
        /// </summary>
        public bool RemoveRule(string ruleId)
        {
            var rule = _rules.FirstOrDefault(r => r.Id == ruleId);
            if (rule == null) return false;

            _rules.Remove(rule);
            _activeAlarms.TryRemove(ruleId, out _);
            _log.Information("[Alarm] 删除规则: {RuleName}", rule.Name);
            return true;
        }
    }
}
