using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;

namespace AuraWave.Core.Models
{
    // ─────────────────────────────────────────────────────────────────────────
    //  MEASUREMENT DATA MODELS
    // ─────────────────────────────────────────────────────────────────────────

    public class MeasurementPoint
    {
        public double AngleDegrees { get; set; }
        public double FrequencyHz { get; set; }
        public double GainDbi { get; set; }
        public double S21Magnitude { get; set; }   // dB
        public double S21Phase { get; set; }   // degrees
        public double S11Magnitude { get; set; }   // dB
        public double S11Phase { get; set; }   // degrees
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class MeasurementResult
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ScanConfiguration ScanConfig { get; set; } = new();
        public List<MeasurementPoint> DataPoints { get; set; } = new();
        public AntennaMetrics? Metrics { get; set; }

        public bool IsComplete { get; set; }
        public double ProgressPercent => ScanConfig.TotalPoints > 0
            ? (double)DataPoints.Count / ScanConfig.TotalPoints * 100.0
            : 0;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  SCAN CONFIGURATION
    // ─────────────────────────────────────────────────────────────────────────

    public class ScanConfiguration
    {
        public double StartAngleDeg { get; set; } = 0;
        public double StopAngleDeg { get; set; } = 360;
        public double StepSizeDeg { get; set; } = 1;
        public double StartFreqHz { get; set; } = 2.4e9;
        public double StopFreqHz { get; set; } = 2.5e9;
        public int FrequencyPoints { get; set; } = 201;
        public double IfBandwidthHz { get; set; } = 10e3;
        public double PowerLevelDbm { get; set; } = -10;

        public PolarizationType Polarization { get; set; } = PolarizationType.EPlane;
        public MeasurementType MeasType { get; set; } = MeasurementType.GainPattern;

        public int TotalPoints => (int)Math.Round((StopAngleDeg - StartAngleDeg) / StepSizeDeg) + 1;

        public double TurntableSpeedDegPerSec { get; set; } = 5.0;
        public bool UseStepMode { get; set; } = true;
        public int SettlingTimeMs { get; set; } = 200;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  ANTENNA METRICS (ANALYSIS OUTPUT)
    // ─────────────────────────────────────────────────────────────────────────

    public class AntennaMetrics
    {
        public double PeakGainDbi { get; set; }
        public double PeakGainAngle { get; set; }
        public double Hpbw { get; set; }   // Half-Power Beam Width  (deg)
        public double Fnbw { get; set; }   // First Null Beam Width  (deg)
        public double SideLobeLevel { get; set; }   // dB below peak
        public double FrontToBackRatio { get; set; }   // dB
        public double Efficiency { get; set; }   // percent
        public double Vswr { get; set; }
        public double ReturnLossDb { get; set; }
        public double BeamTiltDeg { get; set; }
        public double FirstNullAngle { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  HARDWARE STATUS MODELS
    // ─────────────────────────────────────────────────────────────────────────

    public class VnaStatus
    {
        public bool IsConnected { get; set; }
        public string InstrumentId { get; set; } = string.Empty;
        public string ResourceAddress { get; set; } = string.Empty;
        public double CurrentFreqHz { get; set; }
        public double PowerLevelDbm { get; set; }
        public bool IsSweeping { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
    }

    public class TurntableStatus
    {
        public bool IsConnected { get; set; }
        public bool IsHomed { get; set; }
        public bool IsMoving { get; set; }
        public double CurrentAngleDeg { get; set; }
        public double TargetAngleDeg { get; set; }
        public double SpeedDegPerSec { get; set; }
        public string PortName { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public bool EmergencyStop { get; set; }
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
    }

    public class RfSwitchStatus
    {
        public bool IsConnected { get; set; }
        public string PortName { get; set; } = string.Empty;
        public PolarizationType ActivePath { get; set; } = PolarizationType.EPlane;
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
    }

    public class ChamberEnvironment
    {
        public double TemperatureCelsius { get; set; }
        public double HumidityPercent { get; set; }
        public bool DoorLocked { get; set; }
        public bool ShieldingIntact { get; set; }
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
    }

    public class SystemHardwareState
    {
        public VnaStatus Vna { get; set; } = new();
        public TurntableStatus Turntable { get; set; } = new();
        public RfSwitchStatus RfSwitch { get; set; } = new();
        public ChamberEnvironment Chamber { get; set; } = new();
        public bool AllReady =>
            Vna.IsConnected && Turntable.IsConnected &&
            Turntable.IsHomed && RfSwitch.IsConnected;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  LIVE SCAN STATE
    // ─────────────────────────────────────────────────────────────────────────

    public class LiveScanState
    {
        public ScanPhase Phase { get; set; } = ScanPhase.Idle;
        public int CurrentStep { get; set; }
        public int TotalSteps { get; set; }
        public double CurrentAngle { get; set; }
        public double CurrentFreqHz { get; set; }
        public double CurrentGain { get; set; }
        public double CurrentS21 { get; set; }
        public TimeSpan ElapsedTime { get; set; }
        public TimeSpan EstimatedRemaining { get; set; }
        public double ProgressPercent =>
            TotalSteps > 0 ? (double)CurrentStep / TotalSteps * 100.0 : 0;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  VNA SCPI RESPONSE
    // ─────────────────────────────────────────────────────────────────────────

    public class SParameterData
    {
        public double[] Frequencies { get; set; } = Array.Empty<double>();
        public double[] MagnitudeDb { get; set; } = Array.Empty<double>();
        public double[] PhaseDeg { get; set; } = Array.Empty<double>();
        public string Parameter { get; set; } = "S21";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  PROJECT
    // ─────────────────────────────────────────────────────────────────────────

    public class AuraWaveProject
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "New Project";
        public string Description { get; set; } = string.Empty;
        public string AntennaType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ScanConfiguration DefaultConfig { get; set; } = new();
        public List<MeasurementResult> Measurements { get; set; } = new();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  LOG ENTRY (TERMINAL CONSOLE)
    // ─────────────────────────────────────────────────────────────────────────

    public class LogEntry
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public LogSeverity Severity { get; set; } = LogSeverity.Info;
        public string Source { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;

        public string FormattedLine =>
            $"[{Timestamp:HH:mm:ss.fff}] [{Severity,-7}] [{Source,-12}] {Message}";
    }
}