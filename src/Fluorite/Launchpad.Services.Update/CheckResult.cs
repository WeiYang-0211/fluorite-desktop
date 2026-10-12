namespace Launchpad.Services.Update;

public sealed record CheckResult(bool Ok, string Message, UpdateInfo? Info)
{
	public static CheckResult UpToDate(string version)
	{
		return new CheckResult(Ok: true, "已是最新版本 " + version, null);
	}

	public static CheckResult Failed(string message)
	{
		return new CheckResult(Ok: false, message, null);
	}
}
