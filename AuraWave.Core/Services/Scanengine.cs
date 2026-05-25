using AuraWave.Core.Enums;
using AuraWave.Core.Interfaces;
using AuraWave.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace AuraWave.Core.Services
{
    /// <summary>
    /// Orchestrates a full antenna measurement scan:
    /// Home → SetPolarization → StepAngle → TriggerVNA → ReadS21 → StorePoint → Repeat
    /// </summary>
    public sealed class ScanEngine : IScanEngine
    {
        private readonly IHardwareManager _hw;
        private readonly ILogService _log;
        private readonly ILogger<ScanEngine> _logger;

        private CancellationTokenSource? _cts;
        private readonly SemaphoreSlim _pauseSem = new(0, 1);
        private bool _paused;
        private readonly LiveScanState _state = new();

        public ScanPhase CurrentPhase => _state.Phase;
        public LiveScanState State => _state;
        public MeasurementResult? ActiveResult { get; private set; }

        public event EventHandler<LiveScanState>? StateUpdated;
        public event EventHandler<MeasurementPoint>? PointAcquired;
        public event EventHandler<MeasurementResult>? ScanCompleted;
        public event EventHandler<string>? ErrorOccurred;

        public ScanEngine(IHardwareManager hw, ILogService log, ILogger<ScanEngine> logger)
        {
            _hw = hw;
            _log = log;
            _logger = logger;
        }

        // ── PUBLIC API ───────────────────────────────────────────────────────

        public async Task<MeasurementResult> StartScanAsync(
            ScanConfiguration config, CancellationToken ct = default)
        {
            if (_state.Phase == ScanPhase.Running)
                throw new InvalidOperationException("Scan already in progress.");

            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var result = new MeasurementResult
            {
                ScanConfig = config,
                Name = $"Scan_{DateTime.Now:yyyyMMdd_HHmmss}"
            };
            ActiveResult = result;

            _ = ExecuteScanAsync(config, result, _cts.Token);
            return result;
        }

        public async Task PauseAsync()
        {
            if (_state.Phase != ScanPhase.Running) return;
            _paused = true;
            _state.Phase = ScanPhase.Paused;
            _log.Info("SCAN", "Scan paused");
            UpdateState();
        }

        public async Task ResumeAsync()
        {
            if (_state.Phase != ScanPhase.Paused) return;
            _paused = false;
            _state.Phase = ScanPhase.Running;
            _pauseSem.Release();
            _log.Info("SCAN", "Scan resumed");
            UpdateState();
        }

        public async Task AbortAsync()
        {
            _log.Warning("SCAN", "Scan abort requested");
            _cts?.Cancel();
            if (_paused) _pauseSem.Release();
            await _hw.Turntable.StopAsync();
            _state.Phase = ScanPhase.Aborting;
            UpdateState();
        }

        // ── CORE SCAN LOOP ───────────────────────────────────────────────────

        private async Task ExecuteScanAsync(
            ScanConfiguration config,
            MeasurementResult result,
            CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                // ── INITIALIZE ─────────────────────────────────────────────
                _state.Phase = ScanPhase.Initializing;
                _state.TotalSteps = config.TotalPoints;
                UpdateState();

                _log.Info("SCAN", $"Initializing scan: {config.TotalPoints} points, " +
                    $"{config.StartAngleDeg}° → {config.StopAngleDeg}°, " +
                    $"step {config.StepSizeDeg}°");

                // Configure VNA
                await _hw.Vna.SetStartFrequencyAsync(config.StartFreqHz, ct);
                await _hw.Vna.SetStopFrequencyAsync(config.StopFreqHz, ct);
                await _hw.Vna.SetSweepPointsAsync(config.FrequencyPoints, ct);
                await _hw.Vna.SetIfBandwidthAsync(config.IfBandwidthHz, ct);
                await _hw.Vna.SetPortPowerAsync(1, config.PowerLevelDbm, ct);

                // Configure turntable speed
                await _hw.Turntable.SetSpeedAsync(config.TurntableSpeedDegPerSec, ct);

                // ── HOME ────────────────────────────────────────────────────
                if (!_hw.Turntable.IsHomed)
                {
                    _state.Phase = ScanPhase.Homing;
                    UpdateState();
                    await _hw.Turntable.HomeAsync(ct);
                }

                // Move to start angle
                await _hw.Turntable.MoveToAngleAsync(config.StartAngleDeg, ct);

                // ── SCAN LOOP ────────────────────────────────────────────────
                _state.Phase = ScanPhase.Running;
                UpdateState();
                _log.Info("SCAN", "Scan started");

                double angle = config.StartAngleDeg;
                int stepIndex = 0;

                while (angle <= config.StopAngleDeg + 1e-6 && !ct.IsCancellationRequested)
                {
                    // Pause support
                    if (_paused)
                        await _pauseSem.WaitAsync(ct);

                    ct.ThrowIfCancellationRequested();

                    // Move turntable
                    await _hw.Turntable.MoveToAngleAsync(angle, ct);

                    // Settling time
                    if (config.SettlingTimeMs > 0)
                        await Task.Delay(config.SettlingTimeMs, ct);

                    // Trigger and read VNA
                    await _hw.Vna.TriggerSweepAsync(ct);
                    double s21 = await _hw.Vna.ReadSinglePointS21Async(
                        (config.StartFreqHz + config.StopFreqHz) / 2.0, ct);

                    // Build measurement point
                    var point = new MeasurementPoint
                    {
                        AngleDegrees = angle,
                        FrequencyHz = (config.StartFreqHz + config.StopFreqHz) / 2.0,
                        GainDbi = s21,
                        S21Magnitude = s21,
                        Timestamp = DateTime.UtcNow
                    };

                    result.DataPoints.Add(point);

                    // Update live state
                    _state.CurrentStep = ++stepIndex;
                    _state.CurrentAngle = angle;
                    _state.CurrentFreqHz = point.FrequencyHz;
                    _state.CurrentGain = s21;
                    _state.CurrentS21 = s21;
                    _state.ElapsedTime = sw.Elapsed;

                    double rate = stepIndex / sw.Elapsed.TotalSeconds;
                    int remaining = config.TotalPoints - stepIndex;
                    _state.EstimatedRemaining = rate > 0
                        ? TimeSpan.FromSeconds(remaining / rate)
                        : TimeSpan.Zero;

                    UpdateState();
                    PointAcquired?.Invoke(this, point);

                    _log.Debug("SCAN",
                        $"[{stepIndex}/{config.TotalPoints}] Angle={angle:F2}° S21={s21:F2}dB");

                    angle += config.StepSizeDeg;
                }

                // ── COMPLETE ─────────────────────────────────────────────────
                if (!ct.IsCancellationRequested)
                {
                    result.IsComplete = true;
                    _state.Phase = ScanPhase.Complete;
                    _state.CurrentStep = config.TotalPoints;
                    UpdateState();
                    _log.Info("SCAN", $"Scan complete. {result.DataPoints.Count} points in {sw.Elapsed:mm\\:ss}");
                    ScanCompleted?.Invoke(this, result);
                }
                else
                {
                    _state.Phase = ScanPhase.Idle;
                    UpdateState();
                    _log.Warning("SCAN", "Scan aborted");
                }
            }
            catch (OperationCanceledException)
            {
                _state.Phase = ScanPhase.Idle;
                UpdateState();
                _log.Warning("SCAN", "Scan cancelled");
            }
            catch (Exception ex)
            {
                _state.Phase = ScanPhase.Error;
                UpdateState();
                _log.Error("SCAN", $"Scan error: {ex.Message}");
                ErrorOccurred?.Invoke(this, ex.Message);
            }
        }

        private void UpdateState()
        {
            _state.LastUpdate = DateTime.UtcNow;
            StateUpdated?.Invoke(this, _state);
        }
    }
}