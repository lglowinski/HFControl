namespace HFControl.Services;

/// <summary>
/// Fuzzy logic controller for heating furnace.
/// Uses temperature error and rate of change to determine heating output.
/// </summary>
public class FuzzyFurnaceController
{
    private const double ErrorNegativeBigMin = -2.3;
    private const double ErrorNegativeSmallPeak = -1.2;
    private const double ErrorZeroPeak = 0;
    private const double ErrorPositiveSmallPeak = 1.2;
    private const double ErrorPositiveBigMax = 2.3;

    private const double RateNegativeMax = -1.0;
    private const double RateZeroPeak = 0;
    private const double RatePositiveMax = 1.0;

    public double TargetTemperature { get; set; } = 20.0;

    private double _previousError;
    private DateTime _previousTime = DateTime.MinValue;

    /// <summary>
    /// Calculates the heating output (0-100%) based on current temperature.
    /// </summary>
    /// <param name="currentTemperature">Current measured temperature in °C</param>
    /// <returns>Heating output as percentage (0-100)</returns>
    public double CalculateOutput(double currentTemperature)
    {
        var currentTime = DateTime.Now;
        var error = TargetTemperature - currentTemperature;

        // Calculate rate of change
        double errorRate = 0;
        if (_previousTime != DateTime.MinValue)
        {
            var deltaTime = (currentTime - _previousTime).TotalMinutes;
            if (deltaTime > 0)
            {
                errorRate = (error - _previousError) / deltaTime;
            }
        }

        _previousError = error;
        _previousTime = currentTime;

        return Defuzzify(error, errorRate);
    }

    /// <summary>
    /// Resets the controller state.
    /// </summary>
    public void Reset()
    {
        _previousError = 0;
        _previousTime = DateTime.MinValue;
    }

    private double Defuzzify(double error, double errorRate)
    {
        // Fuzzification - calculate membership values
        var errorNegativeBig = MembershipNegativeBig(error, ErrorNegativeBigMin, ErrorNegativeSmallPeak);
        var errorNegativeSmall = MembershipTriangle(error, ErrorNegativeBigMin, ErrorNegativeSmallPeak, ErrorZeroPeak);
        var errorZero = MembershipTriangle(error, ErrorNegativeSmallPeak, ErrorZeroPeak, ErrorPositiveSmallPeak);
        var errorPositiveSmall = MembershipTriangle(error, ErrorZeroPeak, ErrorPositiveSmallPeak, ErrorPositiveBigMax);
        var errorPositiveBig = MembershipPositiveBig(error, ErrorPositiveSmallPeak, ErrorPositiveBigMax);

        var rateNegative = MembershipNegativeBig(errorRate, RateNegativeMax, RateZeroPeak);
        var rateZero = MembershipTriangle(errorRate, RateNegativeMax, RateZeroPeak, RatePositiveMax);
        var ratePositive = MembershipPositiveBig(errorRate, RateZeroPeak, RatePositiveMax);

        // Rule evaluation using Mamdani inference
        // Output: 0=Off, 25=Low, 50=Medium, 75=High, 100=Full
        var rules = new (double strength, double output)[]
        {
            // Error is Positive Big (too cold) -> Heat Full
            (errorPositiveBig, 100),
            
            // Error is Positive Small (slightly cold)
            (Min(errorPositiveSmall, rateNegative), 75),  // cooling down -> High
            (Min(errorPositiveSmall, rateZero), 60),      // stable -> Medium-High
            (Min(errorPositiveSmall, ratePositive), 40),  // warming up -> Medium-Low
            
            // Error is Zero (at target)
            (Min(errorZero, rateNegative), 35),   // cooling down -> Low-Medium
            (Min(errorZero, rateZero), 15),       // stable -> Very Low (maintain)
            (Min(errorZero, ratePositive), 0),    // warming up -> Off
            
            // Error is Negative Small (slightly hot) -> reduce/stop heating
            (Min(errorNegativeSmall, rateNegative), 10),  // cooling down -> Minimal
            (Min(errorNegativeSmall, rateZero), 0),       // stable -> Off
            (Min(errorNegativeSmall, ratePositive), 0),   // warming up -> Off
            
            // Error is Negative Big (too hot) -> Heat Off
            (errorNegativeBig, 0)
        };

        // Defuzzification using weighted average (Center of Gravity approximation)
        double numerator = 0;
        double denominator = 0;

        foreach (var (strength, output) in rules)
        {
            numerator += strength * output;
            denominator += strength;
        }

        if (denominator == 0)
            return 0;

        return Math.Clamp(numerator / denominator, 0, 100);
    }

    // Membership functions
    private static double MembershipTriangle(double value, double left, double peak, double right)
    {
        if (value <= left || value >= right)
            return 0;
        if (value <= peak)
            return (value - left) / (peak - left);
        return (right - value) / (right - peak);
    }

    private static double MembershipNegativeBig(double value, double min, double peak)
    {
        if (value <= min)
            return 1;
        if (value >= peak)
            return 0;
        return (peak - value) / (peak - min);
    }

    private static double MembershipPositiveBig(double value, double peak, double max)
    {
        if (value <= peak)
            return 0;
        if (value >= max)
            return 1;
        return (value - peak) / (max - peak);
    }

    private static double Min(double a, double b) => Math.Min(a, b);
}