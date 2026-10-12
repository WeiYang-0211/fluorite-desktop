using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Launchpad.Services;
using Launchpad.Services.Update;

namespace Launchpad.Infrastructure;

/// <summary>
/// Developer-only command line probes. Enable them with
/// <c>dotnet run -- --update-probe</c> while debugging; they are harmless in
/// release builds because every step here is read-only unless explicitly asked for.
/// </summary>
public static class SelfProbe
{
    public static bool Requested(string[] args)
    {
        if (args == null)
        {
            return false;
        }
        return IndexOf(args, "--update-probe") >= 0;
    }

    public static int Run(string[] args)
    {
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
        }
        catch
        {
        }
        int pageNumber;
        try
        {
            pageNumber = UpdateProbe(args)().GetAwaiter().GetResult();
        }
        catch (Exception value)
        {
            Say($"[PROBE] threw: {value}");
            return 99;
        }
        Say($"[PROBE] exit={pageNumber}");
        return pageNumber;
    }

    private static Func<Task<int>> UpdateProbe(string[] args)
    {
        return async delegate
        {
            var storage = new StorageService();
            var updates = new UpdateService(storage);
            Say($"[UPDATE] current={updates.CurrentVersion} server={UpdateEndpoint.BaseUrl}");

            CheckResult checkResult = await updates.CheckAsync(manual: true).ConfigureAwait(continueOnCapturedContext: false);
            Say($"[UPDATE] check ok={checkResult.Ok}: {checkResult.Message}");

            var info = checkResult.Info;
            if (info == null)
            {
                return checkResult.Ok ? 0 : 1;
            }

            Say($"[UPDATE] version={info.Version} size={info.SizeBytes} sha256={info.Sha256 ?? "-"}");
            Say("[UPDATE] notes=" + info.Notes);
            Say("[UPDATE] url=" + info.DownloadUrl);

            if (info.AlreadyDownloaded)
            {
                Say("[UPDATE] package already on disk and verified: " + info.LocalPath);
                return ReportPackage(info.LocalPath);
            }
            if (IndexOf(args, "--download") < 0)
            {
                Say("[UPDATE] add --download to fetch and verify the package");
                return 0;
            }

            var progress = new Progress<DownloadProgress>(delegate(DownloadProgress p)
            {
                Say("\r[UPDATE] " + p.Line + "   ", newline: false);
            });

            string text;
            try
            {
                text = await updates.DownloadAsync(info, progress, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
            }
            catch (Exception ex)
            {
                Say("");
                Say("[UPDATE] download refused: " + ex.Message);
                return 1;
            }
            Say("");
            Say("[UPDATE] downloaded " + text);
            return ReportPackage(text);
        };
    }

    private static int ReportPackage(string path)
    {
        try
        {
            var fileInfo = new FileInfo(path);
            Say($"[UPDATE] package bytes={fileInfo.Length} verified=ok");
            return fileInfo.Length < UpdateService.MinInstallerBytes ? 1 : 0;
        }
        catch (Exception ex)
        {
            Say("[UPDATE] package unreadable: " + ex.Message);
            return 1;
        }
    }

    private static void Say(string message, bool newline = true)
    {
        try
        {
            if (newline)
            {
                Console.WriteLine(message);
            }
            else
            {
                Console.Write(message);
            }
        }
        catch
        {
        }
        if (newline)
        {
            App.Log(message);
        }
    }

    private static int IndexOf(string[] args, string name)
    {
        return Array.IndexOf(args, name);
    }
}