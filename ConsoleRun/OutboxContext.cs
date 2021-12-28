using System.Reflection;
using Chatter.MessageBrokers.Reliability.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace ConsoleRun;

internal class OutboxContext : DbContext
{
	public OutboxContext(DbContextOptions<OutboxContext> options) : base(options)
	{
	}

	/// <summary>
	///     Called when [model creating].
	/// </summary>
	/// <param name="modelBuilder">The model builder.</param>
	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.ApplyConfigurationsFromAssembly(typeof(OutboxMessageConfiguration).Assembly);
		base.OnModelCreating(modelBuilder);
	}
}