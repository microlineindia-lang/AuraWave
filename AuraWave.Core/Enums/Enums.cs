namespace AuraWave.Core.Enums
{
    public enum PolarizationType
    {
        EPlane,
        HPlane,
        Vertical,
        Horizontal,
        LHCP,
        RHCP
    }

    public enum MeasurementType
    {
        GainPattern,
        RadiationPattern,
        Polarization,
        Efficiency,
        Custom
    }

    public enum ScanPhase
    {
        Idle,
        Initializing,
        Homing,
        Running,
        Paused,
        Aborting,
        Complete,
        Error
    }

    public enum LogSeverity
    {
        Debug,
        Info,
        Warning,
        Error,
        Critical,
        Scpi
    }

    public enum NavigationPage
    {
        Dashboard,
        HardwareControl,
        MeasurementSetup,
        LiveMeasurement,
        Analysis,
        Calibration,
        Reports,
        Settings
    }

    public enum VnaModel
    {
        KeysightE5063A,
        KeysightE5080B,
        AnritsuMS46122B,
        RohdeSchwarzZNB,
        CopperMountainC1209,
        Simulated
    }

    public enum TurntableModel
    {
        CustomSerial,
        AgilentU1731A,
        Simulated
    }

    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Error
    }
}