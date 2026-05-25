using System;
using System.Threading;
using System.Threading.Tasks;
using AuraWave.Core.Interfaces;
using AuraWave.Core.Models;

namespace AuraWave.Hardware.Turntable
{
    public sealed class SimulatedTurntableController : ITurntableController
    {
        private readonly ILogService _logService;
        private readonly TurntableStatus _status = new();

        public bool IsConnected => _status.IsConnected;
        public bool IsHomed => _status.IsHomed;
        public double CurrentAngle => _status.CurrentAngleDeg;
        public TurntableStatus Status => _status;

        public event EventHandler<double>? PositionChanged;
        public event EventHandler? HomeComplete;
        public event EventHandler<string>? SerialMessageSent;
        public event EventHandler<string>? SerialResponseReceived;

        public SimulatedTurntableController(ILogService logService)
        {
            _logService = logService;
        }

        public async Task<bool> ConnectAsync(string portName, int baudRate = 115200, CancellationToken ct = default)
        {
            await Task.Delay(300, ct);
            _status.IsConnected = true;
            _status.PortName = "SIM://COM0";
            _logService.Info("TTL[SIM]", "Simulated turntable connected");
            return true;
        }

        public Task DisconnectAsync()
        {
            _status.IsConnected = false;
            return Task.CompletedTask;
        }

        public async Task HomeAsync(CancellationToken ct = default)
        {
            _logService.Info("TTL[SIM]", "Homing...");
            _status.IsMoving = true;
            await AnimateToAngle(0, ct);
            _status.IsHomed = true;
            _status.IsMoving = false;
            _logService.Info("TTL[SIM]", "Home complete");
            HomeComplete?.Invoke(this, EventArgs.Empty);
        }

        public async Task MoveToAngleAsync(double angleDeg, CancellationToken ct = default)
        {
            _status.IsMoving = true;
            _status.TargetAngleDeg = angleDeg;
            _logService.Debug("TTL[SIM]", $"MoveTo {angleDeg:F2}°");
            await AnimateToAngle(angleDeg, ct);
            _status.IsMoving = false;
        }

        public async Task MoveRelativeAsync(double deltaDeg, CancellationToken ct = default)
        {
            await MoveToAngleAsync(_status.CurrentAngleDeg + deltaDeg, ct);
        }

        public Task SetSpeedAsync(double degPerSec, CancellationToken ct = default)
        {
            _status.SpeedDegPerSec = degPerSec;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            _status.IsMoving = false;
            return Task.CompletedTask;
        }

        public Task EmergencyStopAsync()
        {
            _status.EmergencyStop = true;
            _status.IsMoving = false;
            _logService.Warning("TTL[SIM]", "Emergency stop");
            return Task.CompletedTask;
        }

        private async Task AnimateToAngle(double target, CancellationToken ct)
        {
            double speed = _status.SpeedDegPerSec > 0 ? _status.SpeedDegPerSec : 30;
            double start = _status.CurrentAngleDeg;
            double delta = target - start;
            double duration = Math.Abs(delta) / speed;
            double elapsed = 0;
            const double dt = 0.05; // 50ms steps

            while (elapsed < duration && !ct.IsCancellationRequested)
            {
                await Task.Delay((int)(dt * 1000), ct);
                elapsed += dt;
                double t = Math.Min(elapsed / duration, 1.0);
                _status.CurrentAngleDeg = start + delta * t;
                _status.LastUpdate = DateTime.UtcNow;
                PositionChanged?.Invoke(this, _status.CurrentAngleDeg);
            }

            if (!ct.IsCancellationRequested)
            {
                _status.CurrentAngleDeg = target;
                PositionChanged?.Invoke(this, target);
            }
        }

        public void Dispose() { }
    }
}