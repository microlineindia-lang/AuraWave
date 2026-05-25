using System.Windows;
using AuraWave.App.Views.Dashboard;
using AuraWave.App.Views.Hardware;
using AuraWave.App.Views.Measurement;
using AuraWave.App.Views.Analysis;
using AuraWave.App.Views.Calibration;
using AuraWave.App.Views.Reports;
using AuraWave.App.Views.Settings;


namespace AuraWave.App.Views.Shell;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        MainFrame.Navigate(new DashboardPage());
    }

    private void Dashboard_Click(object sender, RoutedEventArgs e)
    {
        MainFrame.Navigate(new DashboardPage());
    }

    private void Hardware_Click(object sender, RoutedEventArgs e)
    {
        MainFrame.Navigate(new HardwareControlPage());
    }

    private void Setup_Click(object sender, RoutedEventArgs e)
    {
        MainFrame.Navigate(new MeasurementSetupPage());
    }

    private void LiveMeasurement_Click(object sender, RoutedEventArgs e)
    {
        MainFrame.Navigate(new LiveMeasurementPage());
    }

    private void Analysis_Click(object sender, RoutedEventArgs e)
    {
        MainFrame.Navigate(new AnalysisPage());
    }

    private void Calibration_Click(object sender, RoutedEventArgs e)
    {
        MainFrame.Navigate(new CalibrationPage());
    }

    private void Reports_Click(object sender, RoutedEventArgs e)
    {
        MainFrame.Navigate(new ReportsPage());
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        MainFrame.Navigate(new SettingsPage());
    }
}