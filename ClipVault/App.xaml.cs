using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ClipVault.Helpers;
using ClipVault.Services;
using Velopack;
using WpfApplication = System.Windows.Application;

namespace ClipVault;

public partial class App : WpfApplication
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build()
            .OnFirstRun(version =>
            {
                try
                {
                    LogService.Info($"ClipVault first run completed for version {version}.");
                }
                catch
                {
                    // Never let updater hooks break startup.
                }
            })
            .OnRestarted(version =>
            {
                try
                {
                    string currentVersion = Convert.ToString(version)?.Trim() ?? string.Empty;

                    LogService.Info($"ClipVault was restarted by Velopack after updating to version {currentVersion}.");

                    if (!PostUpdateExperienceService.HasPendingAnnouncement())
                    {
                        string changelogText = ChangelogCatalog.BuildChangesSince(null, currentVersion);

                        PostUpdateExperienceService.QueueAnnouncement(
                            string.Empty,
                            currentVersion,
                            changelogText);

                        LogService.Info(
                            $"Queued fallback post-update announcement from Velopack restart hook for version {currentVersion}.");
                    }
                    else
                    {
                        LogService.Info("A post-update announcement was already queued before restart.");
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        LogService.Error(ex, "Failed to queue fallback post-update announcement from Velopack restart hook.");
                    }
                    catch
                    {
                        // Never let updater hooks break startup.
                    }
                }
            })
            .Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        RegisterGlobalExceptionHandlers();
        base.OnStartup(e);

        try
        {
            LogService.Info("ClipVault startup started.");

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();

            mainWindow.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (!mainWindow.IsLoaded || mainWindow.Visibility != Visibility.Visible)
                    {
                        LogService.Warn("Skipped startup post-update announcement check because the main window was not ready.");
                        return;
                    }

                    LogService.Info("Running startup check for a pending post-update announcement.");
                    PostUpdateExperienceService.ShowPendingAnnouncement(mainWindow);
                }
                catch (Exception ex)
                {
                    LogService.Error(ex, "Failed to show the post-update announcement.");
                }
            }), DispatcherPriority.ApplicationIdle);

            LogService.Info("ClipVault startup completed.");
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "ClipVault failed to start.", true);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        LogService.Info($"ClipVault shutting down. Exit code: {e.ApplicationExitCode}");
        base.OnExit(e);
    }

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ErrorHandler.Handle(e.Exception, "An unexpected UI error occurred.");
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception
                 ?? new Exception("Unknown non-UI unhandled exception.");

        ErrorHandler.Handle(ex, "A fatal application error occurred.", true);
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        ErrorHandler.Handle(e.Exception, "A background task failed.");
        e.SetObserved();
    }
}