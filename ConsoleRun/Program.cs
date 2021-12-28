using System;
using System.IO;
using ConsoleRun;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MNP.Common.SqlDbUtils;
using Serilog;

using (var dockerCompose = new DockerCompose())
{
	//dockerCompose.Build();
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
	Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(configuration).CreateLogger();
	#endregion

	// Build ServiceCollection:
	var serviceCollection = new ServiceCollection()
		.AddSingleton(configuration)
		.AddLogging(configure => configure.AddSerilog(dispose: true))
		.AddMemoryCache()
		.AddSqlServerConnectionHandleFactory()
	;

	// Add database context for outbox messages and set up Chatter:
	serviceCollection.AddDbContext<OutboxContext>(o => o.UseSqlServer(configuration.GetConnectionString("ChatTest")));
	serviceCollection.AddChatterCqrs(configuration,
		builder =>
		{
			builder.WithOutboxProcessingBehavior<OutboxContext>(); // Outgoing messages will be read from this DB context
		})
		.AddMessageBrokers()
		.AddAzureServiceBus(builder => builder.UseConfig());

	try
	{
		using var serviceProvider = serviceCollection.BuildServiceProvider();
		var testPub = ActivatorUtilities.CreateInstance<TestPub>(serviceProvider);
		//await testPub.RunSinglePublisher(3);
		await testPub.RunBatchPublisher();
	}
	catch (Exception ex)
	{
		Log.Logger.Error(ex, "Error caught in main");
	}

	dockerCompose.Down();
}