using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleRun;

internal class DockerCompose : IDisposable, IAsyncDisposable
{
	#region Private fields
	private readonly List<string> _loggedLines = new(); // Storage for process stdout/stderr
	private Process? _dockerProcess = null; // Handle for docker compose process
	private Task? _procTask = null; // Task for start process/end process
	private bool disposed = false;
	#endregion

	/// <summary>
	/// Run docker compose build to refresh container definition
	/// </summary>
	public void Build()
	{
		using var dockerBuildProcess = new Process { StartInfo = new ProcessStartInfo("docker", "compose build") };
		dockerBuildProcess.Start();
		dockerBuildProcess.WaitForExit();
	}

	/// <summary>
	/// Bring up docker containers
	/// </summary>
	public void Up()
	{
		// Set up external process for docker compose command, capturing console output:
		_dockerProcess = new Process
		{
			StartInfo = new ProcessStartInfo("docker", "compose up")
			{
				RedirectStandardError = true,
				RedirectStandardOutput = true
			}
		};
		_dockerProcess.OutputDataReceived += (object sender, DataReceivedEventArgs e) => { if (e.Data != null) _loggedLines.Add(e.Data); };
		_dockerProcess.ErrorDataReceived += (object sender, DataReceivedEventArgs e) => { if (e.Data != null) _loggedLines.Add(e.Data); };

		// Launch process asynchronously, saving Task handle in field:
		_procTask = Task.Run(() =>
		{
			_dockerProcess.Start();
			_dockerProcess.BeginOutputReadLine();
			_dockerProcess.WaitForExit();
			_dockerProcess.CancelOutputRead();
		});

		// Wait up to 60 seconds for console output to indicate SQL services are running:
		bool sqlServicesUp = false;
		for (int i = 0; i < 60 && !sqlServicesUp; ++i)
		{
			if (_procTask.IsCompleted) // Task should not complete until "docker compose down" is executed:
			{
				throw new Exception("'docker compose' exited prematurely");
			}
			Thread.Sleep(1000);
			if (_loggedLines.Any(l => l.Contains("Service Broker manager has started")))
			{
				sqlServicesUp = true;
			}
		}
	}

	/// <summary>
	/// Bring down docker containers
	/// </summary>
	public void Down()
	{
		if (!(_dockerProcess?.HasExited ?? false) && !(_procTask?.IsCompleted ?? false))
		{
			// Create and execute docker process:
			using var dockerDownProcess = new Process { StartInfo = new ProcessStartInfo("docker", "compose down") };
			dockerDownProcess.Start();
			dockerDownProcess.WaitForExit();
		}
	}

	#region IDisposable and IAsyncDisposable implementations
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
		if (!disposed && disposing)
		{
			Down();
			if (_procTask != null)
			{
				_procTask.Wait();
			}
			if (_dockerProcess != null)
			{
				// Dispose of process resources (process has exited if Task is complete):
				_dockerProcess.Dispose();
			}
		}
		_procTask = null;
		_dockerProcess = null;
		disposed = true;
	}
	/// <summary>
	/// Public async Dispose function
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		// Perform async cleanup.
		await DisposeAsyncCore();

		// Dispose of unmanaged resources.
		Dispose(disposing: false);

		// Suppress finalization.
		GC.SuppressFinalize(this);
	}
	/// <summary>
	/// Protected function to perform actual async Dispose of resources
	/// </summary>
	protected virtual async ValueTask DisposeAsyncCore()
	{
		if (!disposed)
		{
			Down();
			if (_procTask != null)
			{
				await _procTask;
			}
			if (_dockerProcess != null)
			{
				// Dispose of process resources (process has exited if Task is complete):
				_dockerProcess.Dispose();
			}
		}
	}
	#endregion
}
