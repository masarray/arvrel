namespace Arvrel.Protection;

public readonly record struct IecCurveParameters(double K, double Alpha, double C, string ShortName, string DisplayName);

public static class IecCurveCalculator
{
    public static IecCurveParameters GetParameters(
        IecCurveFamily family,
        double userK = 0.14,
        double userAlpha = 0.02,
        double userC = 0)
        => family switch
        {
            IecCurveFamily.StandardInverse => new(0.14, 0.02, 0, "IEC SI", "IEC Standard / Normal Inverse"),
            IecCurveFamily.VeryInverse => new(13.5, 1, 0, "IEC VI", "IEC Very Inverse"),
            IecCurveFamily.ExtremelyInverse => new(80, 2, 0, "IEC EI", "IEC Extremely Inverse"),
            IecCurveFamily.LongTimeInverse => new(120, 1, 0, "IEC LTI", "IEC Long-Time Inverse"),
            IecCurveFamily.UserDefined => new(userK, userAlpha, userC, "USER", "User-defined IEC-form curve"),
            IecCurveFamily.DefiniteTime => new(0, 0, 0, "DT", "Definite Time"),
            _ => throw new ArgumentOutOfRangeException(nameof(family))
        };

    public static double GetOperateTimeSeconds(
        IecCurveFamily family,
        double multiple,
        double timeMultiplier,
        TimeSpan definiteDelay,
        TimeSpan minimumOperateTime,
        double userK = 0.14,
        double userAlpha = 0.02,
        double userC = 0)
    {
        if (!double.IsFinite(multiple) || multiple <= 1)
            return double.PositiveInfinity;
        if (!double.IsFinite(timeMultiplier) || timeMultiplier <= 0)
            throw new ArgumentOutOfRangeException(nameof(timeMultiplier));
        if (definiteDelay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(definiteDelay));
        if (minimumOperateTime < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(minimumOperateTime));

        double seconds;
        if (family == IecCurveFamily.DefiniteTime)
        {
            seconds = definiteDelay.TotalSeconds;
        }
        else
        {
            var parameters = GetParameters(family, userK, userAlpha, userC);
            var denominator = Math.Pow(multiple, parameters.Alpha) - 1;
            if (denominator <= 1e-12)
                return double.PositiveInfinity;
            seconds = timeMultiplier * ((parameters.K / denominator) + parameters.C);
        }

        seconds = Math.Max(seconds, minimumOperateTime.TotalSeconds);
        return Math.Clamp(seconds, 0.001, TimeSpan.FromMinutes(10).TotalSeconds);
    }

    public static string Formula(
        IecCurveFamily family,
        double userK = 0.14,
        double userAlpha = 0.02,
        double userC = 0)
    {
        if (family == IecCurveFamily.DefiniteTime)
            return "t = definite delay";
        var parameters = GetParameters(family, userK, userAlpha, userC);
        return $"t = TMS × ({parameters.K:0.####} / (M^{parameters.Alpha:0.####} − 1) + {parameters.C:0.####})";
    }
}
