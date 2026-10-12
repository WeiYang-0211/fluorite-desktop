using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Launchpad.Services;

public interface IAptabaseClient
{
	void TrackEvent(string eventName, Dictionary<string, object>? props = null);

	Task TrackEventAsync(string eventName, Dictionary<string, object>? props = null);

	void TrackError(Exception exception, bool fatal = false, string kind = "handled");

	Task TrackErrorAsync(Exception exception, bool fatal = false, string kind = "handled");
}
