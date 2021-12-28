using System.Threading.Tasks;
using Chatter.CQRS;
using Chatter.CQRS.Context;
using Microsoft.Extensions.Logging;

namespace ConsoleRun;

internal class TestCommandHandler : IMessageHandler<TestCommand>
{
	private readonly ILogger _logger;
	public TestCommandHandler(ILogger<TestCommandHandler> logger) => _logger = logger;

	public Task Handle(TestCommand message, IMessageHandlerContext context)
	{
		_logger.LogInformation("Received TestCommand {@message}", message);
		return Task.CompletedTask;
	}
}
