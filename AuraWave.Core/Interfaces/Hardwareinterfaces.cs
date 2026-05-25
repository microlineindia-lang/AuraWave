using AuraWave.Core.Enums;
using AuraWave.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AuraWave.Core.Interfaces
{
    // ─────────────────────────────────────────────────────────────────────────
    //  VNA
    // ─────────────────────────────────────────────────────────────────────────

    public interface IVnaController : IDisposable
    {
        string InstrumentId { get; }
        bool IsConnected { get; }
        VnaStatus Status { get; }

        Task<bool> ConnectAsync(string resourceAddress, CancellationToken ct = default);
        Task DisconnectAsync();
        Task ResetAsync();

        Task SetStartFrequencyAsync(double freqHz, CancellationToken ct = default);
        Task SetStopFrequencyAsync(double freqHz, CancellationToken ct = default);
        Task SetSweepPointsAsync(int points, CancellationToken ct = default);
        Task SetIfBandwidthAsync(double bwHz, CancellationToken ct = default);
        Task SetPortPowerAsync(int port, double powerDbm, CancellationToken ct = default);

        Task TriggerSweepAsync(CancellationToken ct = default);
        Task<SParameterData> ReadS21Async(CancellationToken ct = default);
        Task<SParameterData> ReadS11Async(CancellationToken ct = default);
        Task<double> ReadSinglePointS21Async(double freqHz, CancellationToken ct = default);

        Task<string> SendRawScpiAsync(string command, CancellationToken ct = default);

        event EventHandler<SParameterData>? SweepCompleted;
        event EventHandler<string>? ScpiMessageSent;
        event EventHandler<string>? ScpiResponseReceived;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  TURNTABLE
    // ─────────────────────────────────────────────────────────────────────────

    public interface ITurntableController : IDisposable
    {
        bool IsConnected { get; }
        bool IsHomed { get; }
        double CurrentAngle { get; }
        TurntableStatus Status { get; }

        Task<bool> ConnectAsync(string portName, int baudRate = 115200, CancellationToken ct = default);
        Task DisconnectAsync();

        Task HomeAsync(CancellationToken ct = default);
        Task MoveToAngleAsync(double angleDeg, CancellationToken ct = default);
        Task MoveRelativeAsync(double deltaDeg, CancellationToken ct = default);
        Task SetSpeedAsync(double degPerSec, CancellationToken ct = default);
        Task StopAsync();
        Task EmergencyStopAsync();

        event EventHandler<double>? PositionChanged;
        event EventHandler? HomeComplete;
        event EventHandler<string>? SerialMessageSent;
        event EventHandler<string>? SerialResponseReceived;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  RF SWITCH
    // ─────────────────────────────────────────────────────────────────────────

    public interface IRfSwitchController : IDisposable
    {
        bool IsConnected { get; }
        RfSwitchStatus Status { get; }

        Task<bool> ConnectAsync(string portName, CancellationToken ct = default);
        Task DisconnectAsync();
        Task SetPathAsync(PolarizationType polarization, CancellationToken ct = default);
        Task<PolarizationType> GetActivePathAsync(CancellationToken ct = default);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  SCAN ENGINE
    // ─────────────────────────────────────────────────────────────────────────

    public interface IScanEngine
    {
        ScanPhase CurrentPhase { get; }
        LiveScanState State { get; }
        MeasurementResult? ActiveResult { get; }

        Task<MeasurementResult> StartScanAsync(ScanConfiguration config, CancellationToken ct = default);
        Task PauseAsync();
        Task ResumeAsync();
        Task AbortAsync();

        event EventHandler<LiveScanState>? StateUpdated;
        event EventHandler<MeasurementPoint>? PointAcquired;
        event EventHandler<MeasurementResult>? ScanCompleted;
        event EventHandler<string>? ErrorOccurred;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  LOG SERVICE
    // ─────────────────────────────────────────────────────────────────────────

    public interface ILogService
    {
        IReadOnlyList<LogEntry> Entries { get; }
        void Log(LogSeverity severity, string source, string message);
        void Debug(string source, string message);
        void Info(string source, string message);
        void Warning(string source, string message);
        void Error(string source, string message);
        void Scpi(string source, string message);

        event EventHandler<LogEntry>? EntryAdded;
        void Clear();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  NAVIGATION SERVICE
    // ─────────────────────────────────────────────────────────────────────────

    public interface INavigationService
    {
        NavigationPage CurrentPage { get; }
        void NavigateTo(NavigationPage page);
        event EventHandler<NavigationPage>? Navigated;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  PROJECT SERVICE
    // ─────────────────────────────────────────────────────────────────────────

    public interface IProjectService
    {
        AuraWaveProject? CurrentProject { get; }
        void NewProject(string name);
        Task<bool> OpenProjectAsync(string filePath);
        Task SaveProjectAsync(string? filePath = null);
        string? CurrentFilePath { get; }

        event EventHandler<AuraWaveProject>? ProjectChanged;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  HARDWARE MANAGER
    // ─────────────────────────────────────────────────────────────────────────

    public interface IHardwareManager
    {
        IVnaController Vna { get; }
        ITurntableController Turntable { get; }
        IRfSwitchController RfSwitch { get; }
        SystemHardwareState State { get; }

        Task RefreshStatusAsync();
        event EventHandler<SystemHardwareState>? StateChanged;
    }
}