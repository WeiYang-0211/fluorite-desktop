using System;
using Launchpad.Infrastructure;
using Launchpad.Interop;
using Launchpad.Services;

namespace Launchpad;

public static class Program
{
	public const string AppUserModelId = "Fluorite.App";

	[STAThread]
	public static int Main(string[] args)
	{
		try
		{
			NativeMethods.SetCurrentProcessExplicitAppUserModelID("Fluorite.App");
		}
		catch
		{
		}
		if (SelfProbe.Requested(args))
		{
			try
			{
				NativeMethods.AttachConsole(3u);
			}
			catch
			{
			}
			return SelfProbe.Run(args);
		}
		SingleInstanceService singleInstanceService = new SingleInstanceService();
		if (!singleInstanceService.CheckIsFirstInstance())
		{
			if (StartupService.IsTrayStartup(args))
			{
				singleInstanceService.Dispose();
				return 0;
			}
			string text = App.ExtractAddPath(args);
			string message = ((!string.IsNullOrEmpty(text)) ? ("ADD|" + text) : "SHOW");
			if (singleInstanceService.SendMessageToFirstInstanceAsync(message, 800).GetAwaiter().GetResult())
			{
				singleInstanceService.Dispose();
				return 0;
			}
			App.Log("[Program] Existing instance not responding. Proceeding to take over as primary instance.");
		}
		App app = new App();
		app.SetSingleInstanceService(singleInstanceService);
		app.InitializeComponent();
		app.Run();
		return 0;
	}
}
