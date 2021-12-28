using Chatter.CQRS.Commands;
using Chatter.MessageBrokers;

namespace ConsoleRun;

[BrokeredMessage("test-chatter", "test-chatter")]
internal class TestCommand : ICommand
{
	public int SeedVal { get; set; }
	public string? Method { get; set; }
}
