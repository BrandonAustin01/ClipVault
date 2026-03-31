using System;
using System.Threading;
using System.Threading.Tasks;
using Velopack;

namespace ClipVault.Services;

public enum UpdateCheckState
{
    NoFeedConfigured,
    NotInstalled,
    UpToDate,
    UpdatePendingRestart,
    UpdateReadyToApply
}

public sealed class UpdateCheckResult
{
    public UpdateCheckState State { get; }
    public string Message { get; }
    public string? CurrentVersion { get; }
    public string? TargetVersion { get; }
    public Action? ApplyAndRestart { get; }

    private UpdateCheckResult(
        UpdateCheckState state,
        string message,
        string? currentVersion = null,
        string? targetVersion = null,
        Action? applyAndRestart = null)
    {
        State = state;
        Message = message;
        CurrentVersion = currentVersion;
        TargetVersion = targetVersion;
        ApplyAndRestart = applyAndRestart;
    }

    public static UpdateCheckResult NoFeedConfigured() =>
        new(UpdateCheckState.NoFeedConfigured, "No update feed URL is configured.");

    public static UpdateCheckResult NotInstalled(string? currentVersion) =>
        new(
            UpdateCheckState.NotInstalled,
            "ClipVault is not running from a Velopack-installed location yet.",
            currentVersion);

    public static UpdateCheckResult UpToDate(string? currentVersion) =>
        new(
            UpdateCheckState.UpToDate,
            "ClipVault is already up to date.",
            currentVersion);

    public static UpdateCheckResult UpdatePendingRestart(string? currentVersion, string? targetVersion, Action applyAndRestart) =>
        new(
            UpdateCheckState.UpdatePendingRestart,
            "An update is already downloaded and waiting for restart.",
            currentVersion,
            targetVersion,
            applyAndRestart);

    public static UpdateCheckResult UpdateReadyToApply(string? currentVersion, string? targetVersion, Action applyAndRestart) =>
        new(
            UpdateCheckState.UpdateReadyToApply,
            "A new update was downloaded and is ready to install.",
            currentVersion,
            targetVersion,
            applyAndRestart);
}

public sealed class UpdateService
{
    public async Task<UpdateCheckResult> CheckForUpdatesAsync(
        string updateFeedUrl,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(updateFeedUrl))
        {
            return UpdateCheckResult.NoFeedConfigured();
        }

        var manager = new UpdateManager(updateFeedUrl.Trim());
        string? currentVersion = manager.CurrentVersion?.ToString();

        if (!manager.IsInstalled)
        {
            return UpdateCheckResult.NotInstalled(currentVersion);
        }

        if (manager.UpdatePendingRestart is not null)
        {
            string pendingVersion = manager.UpdatePendingRestart.Version?.ToString() ?? "unknown";
            return UpdateCheckResult.UpdatePendingRestart(
                currentVersion,
                pendingVersion,
                () => manager.ApplyUpdatesAndRestart(manager.UpdatePendingRestart));
        }

        var updateInfo = await manager.CheckForUpdatesAsync();
        if (updateInfo is null)
        {
            return UpdateCheckResult.UpToDate(currentVersion);
        }

        await manager.DownloadUpdatesAsync(updateInfo, percent => progress?.Report(percent), cancellationToken);

        string targetVersion = updateInfo.TargetFullRelease?.Version?.ToString() ?? "unknown";

        return UpdateCheckResult.UpdateReadyToApply(
            currentVersion,
            targetVersion,
            () => manager.ApplyUpdatesAndRestart(updateInfo.TargetFullRelease));
    }
}
