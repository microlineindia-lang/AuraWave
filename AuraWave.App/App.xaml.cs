using AuraWave.App.Views.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Navigation;
using System;
using AuraWave.App.ViewModels.Shell;
using AuraWave.Core.Interfaces;
using AuraWave.Core.Services;
using AuraWave.Hardware.Turntable;
using AuraWave.Hardware.VNA;


namespace AuraWave.App
{
    public partial class App : Application
    {
        private IHost? _host;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // ── SERILOG ───────────────────────────────────────────────────────
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    "logs/aurawave_.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            // ── HOST ──────────────────────────────────────────────────────────
            _host = Host.CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureServices(ConfigureServices)
                .Build();

            await _host.StartAsync();

            // Show main window
            var mainVm = _host.Services.GetRequiredService<MainWindowViewModel>();
            var mainWin = new MainWindow { DataContext = mainVm };
            MainWindow = mainWin;
            mainWin.Show();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // ── CORE SERVICES ────────────────────────────────────────────────
            services.AddSingleton<ILogService, LogService>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<IProjectService, ProjectService>();
            services.AddSingleton<IScanEngine, ScanEngine>();

            // ── HARDWARE (Simulated — swap for real controllers in production)
            services.AddSingleton<IVnaController, SimulatedVnaController>();
            services.AddSingleton<ITurntableController, SimulatedTurntableController>();
            services.AddSingleton<IRfSwitchController, SimulatedRfSwitchController>();
            services.AddSingleton<IHardwareManager, HardwareManager>();

            // ── APP SERVICES ─────────────────────────────────────────────────
            services.AddSingleton<IThemeService, ThemeService>();

            // ── SHELL VIEW MODEL ─────────────────────────────────────────────
            services.AddSingleton<MainWindowViewModel>();

            // ── PAGE VIEW MODELS ─────────────────────────────────────────────
            services.AddTransient<ViewModels.Dashboard.DashboardViewModel>();
            services.AddTransient<ViewModels.Hardware.HardwareControlViewModel>();
            services.AddTransient<ViewModels.Measurement.MeasurementSetupViewModel>();
            services.AddTransient<ViewModels.Measurement.LiveMeasurementViewModel>();
            services.AddTransient<ViewModels.Analysis.AnalysisViewModel>();
            services.AddTransient<ViewModels.Calibration.CalibrationViewModel>();
            services.AddTransient<ViewModels.Reports.ReportsViewModel>();
            services.AddTransient<ViewModels.Settings.SettingsViewModel>();
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            if (_host is not null)
            {
                await _host.StopAsync(TimeSpan.FromSeconds(3));
                _host.Dispose();
            }
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}