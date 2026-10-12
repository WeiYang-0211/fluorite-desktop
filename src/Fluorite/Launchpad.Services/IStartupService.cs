namespace Launchpad.Services;

public interface IStartupService
{
	bool IsStartupEnabled();

	bool SetStartup(bool enable);

	void MigrateLegacyRunValue();
}
