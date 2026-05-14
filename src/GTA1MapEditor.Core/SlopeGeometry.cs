namespace GTA1MapEditor.Core;

/// <summary>Corner heights of a sloped block, expressed as 0..1 fractions of one block height.</summary>
public readonly record struct SlopeCorners(float Nw, float Ne, float Se, float Sw);

/// <summary>
/// GTA1 slope set (44 entries):
///   1-2  N up 26°            3-4  S up 26°
///   5-6  W up 26°            7-8  E up 26°
///   9-16 N 7-step gradient   17-24 S 7-step
///   25-32 W 7-step           33-40 E 7-step
///   41   45° N (S edge floor) 42   45° S
///   43   45° W                44   45° E
/// </summary>
public static class SlopeGeometry
{
    public static SlopeCorners? GetCorners(int slope)
    {
        if (slope <= 0) return null;
        if (slope <= 2) { float lo = (slope - 1) / 2f, hi = (slope) / 2f; return new(hi, hi, lo, lo); }
        if (slope <= 4) { float lo = (slope - 3) / 2f, hi = (slope - 2) / 2f; return new(lo, lo, hi, hi); }
        if (slope <= 6) { float lo = (slope - 5) / 2f, hi = (slope - 4) / 2f; return new(hi, lo, lo, hi); }
        if (slope <= 8) { float lo = (slope - 7) / 2f, hi = (slope - 6) / 2f; return new(lo, hi, hi, lo); }
        if (slope <= 16) { float lo = (slope - 9) / 8f, hi = (slope - 8) / 8f; return new(hi, hi, lo, lo); }
        if (slope <= 24) { float lo = (slope - 17) / 8f, hi = (slope - 16) / 8f; return new(lo, lo, hi, hi); }
        if (slope <= 32) { float lo = (slope - 25) / 8f, hi = (slope - 24) / 8f; return new(hi, lo, lo, hi); }
        if (slope <= 40) { float lo = (slope - 33) / 8f, hi = (slope - 32) / 8f; return new(lo, hi, hi, lo); }
        return slope switch
        {
            41 => new SlopeCorners(1, 1, 0, 0),
            42 => new SlopeCorners(0, 0, 1, 1),
            43 => new SlopeCorners(1, 0, 0, 1),
            44 => new SlopeCorners(0, 1, 1, 0),
            _ => null,
        };
    }
}
