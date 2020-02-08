using System.Threading;
using System.Threading.Tasks;
using DotnetSpider.Common;
using DotnetSpider.MessageQueue;
using DotnetSpider.Statistics.Store;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotnetSpider.Statistics
{
	/// <summary>
	/// 统计服务中心
	/// </summary>
	public class StatisticsCenter : BackgroundService, IStatisticsCenter
	{
		private readonly IMq _mq;
		private readonly ILogger _logger;
		private readonly IStatisticsStore _statisticsStore;
		private readonly SpiderOptions _options;

		/// <summary>
		/// 构造方法
		/// </summary>
		/// <param name="eventBus">消息队列接口</param>
		/// <param name="options"></param>
		/// <param name="statisticsStore">统计存储接口</param>
		/// <param name="logger">日志接口</param>
		public StatisticsCenter(IMq eventBus, SpiderOptions options, IStatisticsStore statisticsStore,
			ILogger<StatisticsCenter> logger)
		{
			_options = options;
			_mq = eventBus;
			_statisticsStore = statisticsStore;
			_logger = logger;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			await _statisticsStore.EnsureDatabaseAndTableCreatedAsync();
			_logger.LogInformation("Initialize statistics center database success");
			_mq.Subscribe<string>(_options.TopicStatisticsService,
				async message => await HandleStatisticsMessageAsync(message));
			_logger.LogInformation("Statistics center started");
		}

		/// <summary>
		/// 停止统计中心
		/// </summary>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public override Task StopAsync(CancellationToken cancellationToken)
		{
			_mq.Unsubscribe(_options.TopicStatisticsService);
			_logger.LogInformation("Statistics center exited");
			return base.StopAsync(cancellationToken);
		}

		private async Task HandleStatisticsMessageAsync(MessageData<string> message)
		{
			if (message == null)
			{
				_logger.LogWarning("Statistics center receive empty message");
				return;
			}

			switch (message.Type)
			{
				case "Success":
				{
					var ownerId = message.Data;
					await _statisticsStore.IncrementSuccessAsync(ownerId);
					break;
				}

				case "Failed":
				{
					var data = message.Data.Split(',');
					await _statisticsStore.IncrementFailedAsync(data[0], int.Parse(data[1]));
					break;
				}

				case "Start":
				{
					var ownerId = message.Data;
					await _statisticsStore.StartAsync(ownerId);
					break;
				}

				case "Exit":
				{
					var ownerId = message.Data;
					await _statisticsStore.ExitAsync(ownerId);
					break;
				}

				case "Total":
				{
					var data = message.Data.Split(',');
					await _statisticsStore.IncrementTotalAsync(data[0], int.Parse(data[1]));

					break;
				}

				case "DownloadSuccess":
				{
					var data = message.Data.Split(',');
					await _statisticsStore.IncrementDownloadSuccessAsync(data[0], int.Parse(data[1]),
						long.Parse(data[2]));
					break;
				}

				case "DownloadFailed":
				{
					var data = message.Data.Split(',');
					await _statisticsStore.IncrementDownloadFailedAsync(data[0], int.Parse(data[1]),
						long.Parse(data[2]));
					break;
				}

				case "Print":
				{
					var ownerId = message.Data;
					var statistics = await _statisticsStore.GetSpiderStatisticsAsync(ownerId);
					if (statistics != null)
					{
						var left = statistics.Total >= statistics.Success
							? (statistics.Total - statistics.Success - statistics.Failed).ToString()
							: "unknown";
						_logger.LogInformation(
							$"{ownerId} total {statistics.Total}, success {statistics.Success}, failed {statistics.Failed}, left {left}");
					}

					break;
				}
			}
		}
	}
}
