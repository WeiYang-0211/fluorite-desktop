namespace Launchpad.Services;

public class AptabaseOptions
{
	public string? Host { get; set; }

	public bool? IsDebugMode { get; set; }

	public bool EnableCrashReporting { get; set; } = true;

	public bool EnablePersistence { get; set; } = true;
}
