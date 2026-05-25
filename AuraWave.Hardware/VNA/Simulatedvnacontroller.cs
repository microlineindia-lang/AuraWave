using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuraWave.Core.Interfaces;
using AuraWave.Core.Models;

namespace AuraWave.Hardware.VNA
{
    /// <summary>
    /// Simulated VNA — generates realistic S-parameter data for development
    /// without physical hardware. Produces a Gaussian beam pattern in S21.
    /// </summary>
    public sealed class SimulatedVnaController : IVnaController
    {
        private readonly ILogService _logService;
        private readonly Random _rng = new();
        private readonly VnaStatus _status = new();

        private double _startFreqHz = 2.4e9;
        private double _stopFreqHz = 2.5e9;
        private int _sweepPoints = 201;
        private double _ifBandwidthHz = 10e3;
        private double _powerDbm = -10;
        private double _currentAngle = 0;

        public string InstrumentId => "SIMULATED,AuraWave VNA Emulator,SN001,FW1.0";
        public bool IsConnected => _status.IsConnected;
        public VnaStatus Status => _status;

        public event EventHandler<SParameterData>? SweepCompleted;
        public event EventHandler<string>? ScpiMessageSent;
        public event EventHandler<string>? ScpiResponseReceived;

        public SimulatedVnaController(ILogService logService)
        {
            _logService = logService;
        }

        public async Task<bool> ConnectAsync(string resourceAddress, CancellationToken ct = default)
        {
            await Task.Delay(400, ct);
            _status.IsConnected = true;
            _status.InstrumentId = InstrumentId;
            _status.ResourceAddress = "SIM://localhost";
            _logService.Info("VNA[SIM]", "Simulated VNA connected");
            return true;
        }

        public Task DisconnectAsync()
        {
            _status.IsConnected = false;
            _logService.Info("VNA[SIM]", "Simulated VNA disconnected");
            return Task.CompletedTask;
        }

        public Task ResetAsync() => Task.CompletedTask;

        public Task SetStartFrequencyAsync(double freqHz, CancellationToken ct = default)
        {
            _startFreqHz = freqHz;
            EmitScpi($"SENS1:FREQ:STAR {freqHz:F0}");
            return Task.CompletedTask;
        }

        public Task SetStopFrequencyAsync(double freqHz, CancellationToken ct = default)
        {
            _stopFreqHz = freqHz;
            EmitScpi($"SENS1:FREQ:STOP {freqHz:F0}");
            return Task.CompletedTask;
        }

        public Task SetSweepPointsAsync(int points, CancellationToken ct = default)
        {
            _sweepPoints = points;
            return Task.CompletedTask;
        }

        public Task SetIfBandwidthAsync(double bwHz, CancellationToken ct = default)
        {
            _ifBandwidthHz = bwHz;
            return Task.CompletedTask;
        }

        public Task SetPortPowerAsync(int port, double powerDbm, CancellationToken ct = default)
        {
            _powerDbm = powerDbm;
            return Task.CompletedTask;
        }

        public Task TriggerSweepAsync(CancellationToken ct = default)
        {
            _status.IsSweeping = true;
            return Task.Delay(50, ct).ContinueWith(_ => _status.IsSweeping = false, ct);
        }

        public async Task<SParameterData> ReadS21Async(CancellationToken ct = default)
        {
            await Task.Delay(30, ct);
            var data = GenerateS21Data(_currentAngle);
            SweepCompleted?.Invoke(this, data);
            return data;
        }

        public async Task<SParameterData> ReadS11Async(CancellationToken ct = default)
        {
            await Task.Delay(20, ct);
            return GenerateS11Data();
        }

        public async Task<double> ReadSinglePointS21Async(double freqHz, CancellationToken ct = default)
        {
            await Task.Delay(20, ct);
            return ComputeGainAtAngle(_currentAngle) + (_rng.NextDouble() - 0.5) * 0.3;
        }

        public Task<string> SendRawScpiAsync(string command, CancellationToken ct = default)
        {
            EmitScpi(command);
            if (command.Contains("*IDN?")) return Task.FromResult(InstrumentId);
            if (command.Contains("*OPC?")) return Task.FromResult("1");
            return Task.FromResult(string.Empty);
        }

        // ── SIMULATION HELPERS ───────────────────────────────────────────────

        /// <summary>
        /// Sets internal angle state so gain varies with turntable angle.
        /// Call this from the scan engine before each VNA sweep.
        /// </summary>
        public void SetSimulatedAngle(double angleDeg) => _currentAngle = angleDeg;

        private SParameterData GenerateS21Data(double angleDeg)
        {
            var freqs = Enumerable.Range(0, _sweepPoints)
                .Select(i => _startFreqHz + (_stopFreqHz - _startFreqHz) * i / (_sweepPoints - 1))
                .ToArray();

            double baseGain = ComputeGainAtAngle(angleDeg);

            var mags = freqs.Select(f =>
            {
                // Slight frequency ripple for realism
                double freqVar = 0.5 * Math.Sin(2 * Math.PI * (f - _startFreqHz) / (_stopFreqHz - _startFreqHz) * 3);
                double noise = (_rng.NextDouble() - 0.5) * 0.4;
                return baseGain + freqVar + noise;
            }).ToArray();

            var phases = freqs.Select(f =>
                -180.0 * (f - _startFreqHz) / (_stopFreqHz - _startFreqHz) + (_rng.NextDouble() - 0.5) * 2
            ).ToArray();

            return new SParameterData
            {
                Parameter = "S21",
                Frequencies = freqs,
                MagnitudeDb = mags,
                PhaseDeg = phases,
                Timestamp = DateTime.UtcNow
            };
        }

        private SParameterData GenerateS11Data()
        {
            var freqs = Enumerable.Range(0, _sweepPoints)
                .Select(i => _startFreqHz + (_stopFreqHz - _startFreqHz) * i / (_sweepPoints - 1))
                .ToArray();

            // Simulate a resonant antenna — dip in S11 at centre frequency
            double centerFreq = (_startFreqHz + _stopFreqHz) / 2.0;
            var mags = freqs.Select(f =>
            {
                double normalized = (f - centerFreq) / ((_stopFreqHz - _startFreqHz) * 0.2);
                double resonance = -25.0 * Math.Exp(-normalized * normalized);
                double background = -5.0;
                double noise = (_rng.NextDouble() - 0.5) * 0.5;
                return background + resonance + noise;
            }).ToArray();

            return new SParameterData
            {
                Parameter = "S11",
                Frequencies = freqs,
                MagnitudeDb = mags,
                PhaseDeg = new double[_sweepPoints],
                Timestamp = DateTime.UtcNow
            };
        }

        private static double ComputeGainAtAngle(double angleDeg)
        {
            // Realistic dipole-like pattern with side lobes
            double rad = angleDeg * Math.PI / 180.0;
            double mainBeam = 8.0 * Math.Exp(-rad * rad / (2 * 0.3 * 0.3));

            // Side lobes at ±90°
            double sideL1 = 2.0 * Math.Exp(-(rad - Math.PI / 2) * (rad - Math.PI / 2) / 0.1);
            double sideL2 = 2.0 * Math.Exp(-(rad + Math.PI / 2) * (rad + Math.PI / 2) / 0.1);

            // Back lobe
            double backLobe = 0.5 * Math.Exp(-(Math.Abs(rad) - Math.PI) * (Math.Abs(rad) - Math.PI) / 0.2);

            return -20.0 + mainBeam + sideL1 + sideL2 + backLobe;
        }

        private void EmitScpi(string cmd)
        {
            ScpiMessageSent?.Invoke(this, cmd);
            _logService.Scpi("VNA[SIM]>>", cmd);
        }

        public void Dispose() { }
    }
}