using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
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
