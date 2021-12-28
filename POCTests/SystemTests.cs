using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DockerComposeFixture;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MNP.Common.SqlDbUtils.SqlServer;
using Xunit;

namespace POCTests;

public class SystemTests : IClassFixture<DockerFixture>, IDisposable
{
	#region Fields and constructor
	private readonly MemoryCache _memoryCache;
	private readonly SqlServerConnectionHandleFactory _sqlFactory;
	private DockerFixture? _dockerFixture = null;
	private bool disposed = false;

	/// <summary>
	/// Public constructor - Start up docker container(s) and initialize SQL DB state and handle factory
	/// </summary>
	/// <param name="dockerFixture"></param>
    public SystemTests(DockerFixture dockerFixture)
    {
		// Set up connection handle factory for use once container is running; note that Password value in
		// this connection string must be kept in sync with PASSWORD arg in docker-compose.yml:
		_memoryCache = new(Options.Create(new MemoryCacheOptions()));
		_sqlFactory = new(
			new Dictionary<string, string>()
			{
				["ChatTest"] = "Server=.,1344;Database=ChatTest;User Id=sa;Password=P@ssword12;TrustServerCertificate=True;"
			},
			_memoryCache
		);

		// Execute docker-compose file and wait for SQL container to indicate startup complete:
		_dockerFixture = dockerFixture;
        _dockerFixture.InitOnce(
            () => new DockerFixtureOptions
            {
				DockerComposeUpArgs = "--build",
                CustomUpTest = output => output.Any(l => l.Contains("Service Broker manager has started"))
            });
	}
	#endregion

	[Fact]
	[Trait("Category", "System")]
	public async Task ReadyConnectsToDatabase()
	{
		var cnn = _sqlFactory.CreateConnection("ChatTest");
		Assert.Null(await Record.ExceptionAsync(() => cnn.OpenAsync()));
	}

	#region IDisposable implementation
	/// <summary>
	/// Public synchronous Dispose function
	/// </summary>
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
	/// <summary>
	/// Protected function to perform actual Dispose of resources
	/// </summary>
	protected virtual void Dispose(bool disposing)
	{
		if (disposing && !disposed && _dockerFixture != null)
		{
			_dockerFixture.Dispose();
			_dockerFixture = null;
		}
		disposed = true;
	}
	#endregion
}
