namespace RimCore.DataCenters
{
    /// <summary>The single headline status shown for a server rack.</summary>
    public enum RackStatus
    {
        Operational,
        NoPower,
        NoNetwork,
        TooHot,
        MaintenanceRequired,
        OutputFull
    }

    /// <summary>Why a rack has no usable network link (shown as a detail line).</summary>
    public enum NetworkFailure
    {
        None,
        NoCoreInRange,
        CoreFull,
        CoreOffline
    }
}
