using System;
using System.IO;
using ConsoleRun;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MNP.Common.SqlDbUtils;
using Serilog;
using Serilog.Events;

using var dockerCompose = new DockerCompose();
//dockerCompose.Build(); // Uncomment if dockerfile needs to be rebuilt (i.e due to changes in DB schema)
dockerCompose.Up();

#region Build program configuration and logger
var env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
var configbuilder = new ConfigurationBuilder()
	.SetBasePath(Directory.GetCurrentDirectory())
	.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
;
if (env == "Development")
{
	configbuilder.AddUserSecrets<Program>(optional: true, reloadOnChange: false);
}
configbuilder.AddCommandLine(args);
IConfiguration configuration = configbuilder.Build();
Log.Logger = new LoggerConfiguration()
	.ReadFrom.Configuration(configuration)
	.MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
	.CreateLogger();
#endregion

// Set up HostBuilder object with ServiceCollection:
var hostBuilder = new HostBuilder().ConfigureServices(services =>
{
	services.AddSingleton(configuration)
		.AddLogging(configure => configure.AddSerilog(dispose: true))
		.AddMemoryCache()
		.AddSqlServerConnectionHandleFactory();

	// Add database context for outbox messages and set up Chatter:
	services.AddDbContext<OutboxContext>(o => o.UseSqlServer(configuration.GetConnectionString("ChatTest")));
	services.AddChatterCqrs(configuration,
		builder =>
		{
			builder.WithOutboxProcessingBehavior<OutboxContext>(); // Outgoing messages will be read from this DB context
		})
		.AddMessageBrokers(builder => builder.AddReliabilityOptions(rbuilder => rbuilder.WithOutboxPollingProcessor())) // Temporarily required - switch to FromConfig() later
		.AddAzureServiceBus(builder => builder.UseConfig())
	;
});

try
{
	// Build host and launch processes:
	using var host = hostBuilder.Build();
	var hostTask = host.RunAsync();

	// Create test publisher object and run publish tests:
	var testPub = ActivatorUtilities.CreateInstance<TestPub>(host.Services);
	await testPub.RunSinglePublisher(4);
	await testPub.RunBatchPublisher();

	// Allow host processes to run until shut down:
	await hostTask;
}
catch (Exception ex)
{
	Log.Logger.Error(ex, "Error caught in main");
}

dockerCompose.Down();