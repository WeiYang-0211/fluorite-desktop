namespace Launchpad.Infrastructure;

public static class DistributionChannel
{
	public const string StoreUpdateMessage = "此版本由 Microsoft Store 更新，请在商店的下载页面检查更新。";

	public const string CompatibilityUpdateMessage = "此版本请手动安装更新。";

	public static bool IsStore => false;

	public static bool IsCompatibilityTest => false;
}
