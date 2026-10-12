using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Launchpad.Services;

public sealed class SingleInstanceService : IDisposable
{
	private const string MutexName = "Local\\Fluorite_SingleInstance_Mutex_App_v1";

	private const string PipeName = "Fluorite_IPC_Pipe_App_v1";

	private Mutex? _mutex;

	private bool _isFirstInstance;

	private CancellationTokenSource? _cts;

	public bool IsFirstInstance => _isFirstInstance;

	public event Action<string>? FileAddRequested;

	public event Action? ShowRequested;

	public bool CheckIsFirstInstance()
	{
		try
		{
			_mutex = new Mutex(initiallyOwned: true, "Local\\Fluorite_SingleInstance_Mutex_App_v1", out _isFirstInstance);
			return _isFirstInstance;
		}
		catch (AbandonedMutexException)
		{
			_isFirstInstance = true;
			return true;
		}
		catch (Exception ex2)
		{
			App.Log("[SingleInstance] Mutex check error: " + ex2.Message);
			_isFirstInstance = true;
			return true;
		}
	}

	public async Task<bool> SendMessageToFirstInstanceAsync(string message, int timeoutMs = 1200)
	{
		try
		{
			using NamedPipeClientStream client = new NamedPipeClientStream(".", "Fluorite_IPC_Pipe_App_v1", PipeDirection.Out);
			using CancellationTokenSource timeoutCts = new CancellationTokenSource(timeoutMs);
			await client.ConnectAsync(timeoutCts.Token).ConfigureAwait(continueOnCapturedContext: false);
			using StreamWriter writer = new StreamWriter(client, Encoding.UTF8)
			{
				AutoFlush = true
			};
			await writer.WriteLineAsync(message.AsMemory(), timeoutCts.Token).ConfigureAwait(continueOnCapturedContext: false);
			App.Log("[SingleInstance] Successfully sent IPC message: " + message);
			return true;
		}
		catch (Exception ex)
		{
			App.Log("[SingleInstance] Failed to send message to primary instance: " + ex.Message);
			return false;
		}
	}

	public void StartServer()
	{
		if (_cts == null)
		{
			_cts = new CancellationTokenSource();
			RunServerLoopAsync(_cts.Token);
		}
	}

	private async Task RunServerLoopAsync(CancellationToken ct)
	{
		while (!ct.IsCancellationRequested)
		{
			try
			{
				PipeSecurity pipeSecurity = new PipeSecurity();
				pipeSecurity.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), PipeAccessRights.ReadWrite, AccessControlType.Allow));
				using NamedPipeServerStream server = NamedPipeServerStreamAcl.Create("Fluorite_IPC_Pipe_App_v1", PipeDirection.In, -1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, pipeSecurity);
				await server.WaitForConnectionAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
				using StreamReader reader = new StreamReader(server, Encoding.UTF8);
				string text = await reader.ReadLineAsync(ct).ConfigureAwait(continueOnCapturedContext: false);
				if (!string.IsNullOrWhiteSpace(text))
				{
					ProcessMessage(text.Trim());
				}
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch (Exception ex2)
			{
				App.Log("[SingleInstance] Pipe server error: " + ex2.Message);
				try
				{
					await Task.Delay(300, ct).ConfigureAwait(continueOnCapturedContext: false);
				}
				catch
				{
				}
			}
		}
	}

	private void ProcessMessage(string message)
	{
		App.Log("[SingleInstance] Received IPC message: " + message);
		if (message.StartsWith("ADD|", StringComparison.OrdinalIgnoreCase))
		{
			string target = message.Substring(4);
			FileAddRequested?.Invoke(target);
		}
		else if (string.Equals(message, "SHOW", StringComparison.OrdinalIgnoreCase))
		{
			ShowRequested?.Invoke();
		}
	}

	public void Dispose()
	{
		try
		{
			_cts?.Cancel();
			_cts?.Dispose();
		}
		catch
		{
		}
		if (_mutex == null)
		{
			return;
		}
		if (_isFirstInstance)
		{
			try
			{
				_mutex.ReleaseMutex();
			}
			catch
			{
			}
		}
		_mutex.Dispose();
		_mutex = null;
	}
}
