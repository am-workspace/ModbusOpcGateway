using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;

namespace Industrial.Core.Services
{
    /// <summary>
    /// 数据历史缓存服务：缓存最近的数据点，支持趋势图查询
    /// </summary>
    public class DataHistoryService : BackgroundService
    {
        private readonly SharedData _sharedData;
        private readonly ConcurrentQueue<DataPoint> _history = new();
        private const int MaxDataPoints = 1000;

        public DataHistoryService(SharedData sharedData)
        {
            _sharedData = sharedData;
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

            return tcs.Task;
        }

        private void OnDataChanged(object? sender, DataChangedEventArgs e)
        {
            var point = new DataPoint
            {
                Timestamp = DateTime.Now,
                Temperature = e.Temperature,
                Pressure = e.Pressure,
                Status = e.Status
            };

            _history.Enqueue(point);

            while (_history.Count > MaxDataPoints)
            {
                _history.TryDequeue(out _);
            }
        }

        /// <summary>
        /// 获取最近时间段的历史数据
        /// </summary>
        public List<DataPoint> GetHistory(TimeSpan duration)
        {
            var cutoff = DateTime.Now - duration;
            return _history.Where(p => p.Timestamp >= cutoff).ToList();
        }

        /// <summary>
        /// 获取最近N个数据点
        /// </summary>
        public List<DataPoint> GetRecentPoints(int count)
        {
            return _history.TakeLast(count).ToList();
        }
    }

    /// <summary>
    /// 数据点模型
    /// </summary>
    public class DataPoint
    {
        public DateTime Timestamp { get; set; }
        public float Temperature { get; set; }
        public float Pressure { get; set; }
        public bool Status { get; set; }
    }
}
