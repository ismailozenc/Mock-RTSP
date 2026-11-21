namespace Birel_Plaka;

// The files next to this one are copied verbatim from Birel Plaka (see SOURCE.txt). Of Birel Plaka's
// settings they need only [Travel], which TravelEstimator reads through AppConfig.Travel: this file
// provides exactly that, set by MockRtsp.PlateScanner before a scan (PlateScanSettings).

/// <summary>[Travel] of Birel Plaka's camera-settings.ini.</summary>
internal sealed record TravelOptions(
    TimeSpan TrackTimeout,
    int MinReads,
    TimeSpan MinDuration,
    TimeSpan StoppedAfter,
    double SizeThreshold,
    double RoadThreshold);

/// <summary>The part of Birel Plaka's AppConfig the copied files use.</summary>
internal static class AppConfig
{
    public static TravelOptions Travel { get; set; } = null!;
}
