using System;
using System.Linq;
using System.Threading.Tasks;
using Chatter.MessageBrokers.Reliability.Outbox;
using Microsoft.Extensions.Logging;
using MNP.Common.SqlDbUtils.Interfaces;
using MNP.Common.SqlDbUtils.Models;

namespace ConsoleRun;

internal class TestPub
{
	#region Fields and constructor
	private readonly ISqlConnectionHandleFactory _sqlFactory;
	private readonly ILogger _logger;
	private readonly IBrokeredMessageOutbox _brokeredMessageOutbox;
	private readonly IOutboxProcessor _outboxProcessor;

	public TestPub(
		ISqlConnectionHandleFactory sqlFactory,
		ILogger<TestPub> logger,
		IBrokeredMessageOutbox brokeredMessageOutbox,
		IOutboxProcessor outboxProcessor
	)
	{
		_sqlFactory = sqlFactory;
		_logger = logger;
		_brokeredMessageOutbox = brokeredMessageOutbox;
		_outboxProcessor = outboxProcessor;
	}
	#endregion

	/// <summary>
	/// Single-message publisher execution: run runCount times, calling stored proc to conditionally generate one-off Chatter Outbox messages
	/// </summary>
	public async Task RunSinglePublisher(int runCount)
	{
		await using var cnn = _sqlFactory.CreateConnection("ChatTest");
		await cnn.OpenAsync();

		for (int i = 0; i < runCount; ++i)
		{
			#region Call stored procedure and retrieve return code
			var outputparameters = await cnn.ExecuteCommand("dbo.usp_MaybeCreateData", new CommandParameters()
			{
				["@SeedVal"] = i,
				["@CorrelationId"] = Guid.NewGuid().ToString()
			});
			var retcode = outputparameters.GetReturnCode();
			#endregion

			if (retcode == 201)
			{
				// Outbox message was created; use outbox broker to retrieve unprocessed messages:
				_logger.LogDebug("Created data for seed {SeedValue}", i);
				var messages = await _brokeredMessageOutbox.GetUnprocessedMessagesFromOutbox();
				if (messages.Any())
				{
					// Messages retrieved; iterate through and deliver to destination:
					_logger.LogInformation("{MessageCount} messages in outbox", messages.Count());
					foreach (var message in messages.OrderBy(m => m.SentToOutboxAtUtc))
					{
						await _outboxProcessor.Process(message);
					}
				}
			}
			else if (retcode == 204) // Stored procedure chose to ignore this seed value
			{
				_logger.LogTrace("Ignored seed {SeedValue}", i);
			}
			else // Unexpected return code
			{
				_logger.LogWarning("Invalid return code {returnValue} for seed {SeedValue}", retcode, i);
			}
		}
	}

	/// <summary>
	/// Batch publisher execution: call stored proc to generate a set of Chatter Outbox messages
	/// </summary>
	public async Task RunBatchPublisher()
	{
		await using var cnn = _sqlFactory.CreateConnection("ChatTest");
		await cnn.OpenAsync();

		#region Call stored procedure and retrieve return code
		var outputparameters = await cnn.ExecuteCommand("dbo.usp_BatchCreateData", new CommandParameters()
		{
			["@CorrelationId"] = Guid.NewGuid().ToString()
		});
		var retcode = outputparameters.GetReturnCode();
		var batchId = outputparameters["@BatchID"] as Guid?;
		#endregion

		if (retcode == 201 && batchId != null)
		{
			_logger.LogDebug("Created batch {BatchId}", batchId);
			await _outboxProcessor.ProcessBatch((Guid)batchId);
		}
		else // Unexpected return code
		{
			_logger.LogWarning("Invalid return code {returnValue} or null BatchId", retcode);
		}
	}
}
