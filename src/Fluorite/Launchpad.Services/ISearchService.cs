using System.Collections.Generic;
using Launchpad.Models;

namespace Launchpad.Services;

public interface ISearchService
{
	IReadOnlyList<AppItem> Search(string query, IEnumerable<AppItem> sourceApps);
}
