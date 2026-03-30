using Serilog;
using System.Text;

namespace Industrial.Core.Services
{
    /// <summary>
    /// 数据导出服务：将历史数据导出为 CSV 格式
    /// </summary>
    public class DataExportService
    {
        private readonly DataHistoryService _historyService;
        private readonly ILogger _logger;

        public DataExportService(DataHistoryService historyService)
        {
            _historyService = historyService;
            _logger = Log.ForContext<DataExportService>();
        }

        /// <summary>
        /// 导出历史数据为 CSV 格式
        /// </summary>
        /// <param name="duration">时间范围，默认15分钟</param>
        /// <returns>CSV 格式字符串</returns>
        public string ExportHistoryToCsv(TimeSpan? duration = null)
        {
            var timeRange = duration ?? TimeSpan.FromMinutes(15);
            var data = _historyService.GetHistory(timeRange);

            var sb = new StringBuilder();
            // CSV 头部
            sb.AppendLine("Timestamp,Temperature,Pressure,Status");

            // 数据行
            foreach (var point in data.OrderBy(p => p.Timestamp))
            {
                sb.AppendLine($"{point.Timestamp:yyyy-MM-dd HH:mm:ss},{point.Temperature:F2},{point.Pressure:F2},{point.Status}");
            }

            _logger.Information("[Export] 导出 {Count} 条历史数据，时间范围: {Duration}分钟", 
                data.Count, timeRange.TotalMinutes);

            return sb.ToString();
        }

        /// <summary>
        /// 导出历史数据为 CSV 字节数组（支持中文）
        /// </summary>
        public byte[] ExportHistoryToCsvBytes(TimeSpan? duration = null)
        {
            var csv = ExportHistoryToCsv(duration);
            // UTF-8 with BOM，确保 Excel 正确识别中文
            var preamble = Encoding.UTF8.GetPreamble();
            var content = Encoding.UTF8.GetBytes(csv);
            return preamble.Concat(content).ToArray();
        }

        /// <summary>
        /// 生成导出文件名
        /// </summary>
        public string GenerateFileName(string prefix = "history")
        {
            return $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        }
    }
}
