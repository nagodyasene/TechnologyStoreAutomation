using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace TechnologyStoreAutomation.backend.visitorCountPrediction;

/// <summary>
/// Predicts store visitor counts based on sales velocity data.
/// Uses historical sales as a proxy for foot traffic, assuming more visitors = more sales.
/// </summary>
public class VisitorCountPredictor : IVisitorCountPredictor
{
    private readonly string _connectionString;
    private readonly ILogger<VisitorCountPredictor> _logger;
    
    #region Configuration Constants
    
    // Conversion rate assumption: ~30% of visitors make a purchase (industry average for tech retail)
    private const double DefaultConversionRate = 0.30;
    
    // Traffic level thresholds (z-score based)
    private const double VeryLowThreshold = -1.5;
    private const double LowThreshold = -0.5;
    private const double HighThreshold = 0.5;
    private const double VeryHighThreshold = 1.5;
    
    // Trend detection thresholds
    private const double RisingTrendThreshold = 0.05;
    private const double FallingTrendThreshold = -0.05;
    
    // Confidence decay rate per day
    private const double ConfidenceDecayRate = 0.05;
    private const double MinimumConfidence = 0.5;
    
    #endregion
    
    #region Special Day Multipliers
    
    private const double BlackFridayMultiplier = 2.5;
    private const double CyberMondayMultiplier = 2.0;
    private const double ChristmasRushMultiplier = 1.8;
    private const double PostChristmasMultiplier = 1.5;
    private const double NewYearSalesMultiplier = 1.3;
    private const double AppleLaunchSeasonMultiplier = 1.4;
    private const double SaturdayMultiplier = 1.2;
    private const double SundayMultiplier = 1.1;
    
    #endregion
    
    public VisitorCountPredictor(string connectionString)
    {
        _connectionString = connectionString;
        _logger = AppLogger.CreateLogger<VisitorCountPredictor>();
    }

    private IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);

    /// <summary>
    /// Gets historical daily traffic estimates based on sales data
    /// </summary>
    public async Task<IEnumerable<DailyTraffic>> GetHistoricalTrafficAsync(int days = 30)
    {
        using var db = CreateConnection();
        
        var sql = @"
            SELECT 
                sale_date,
                COUNT(*) as transaction_count,
                SUM(quantity_sold) as total_units,
                SUM(total_amount) as total_revenue
            FROM sales_transactions
            WHERE sale_date >= CURRENT_DATE - @Days::INTEGER
            GROUP BY sale_date
            ORDER BY sale_date ASC;";

        var results = await db.QueryAsync<(DateTime sale_date, int transaction_count, int total_units, decimal total_revenue)>(
            sql, new { Days = days });

        return results.Select(r => new DailyTraffic
        {
            Date = r.sale_date,
            TotalTransactions = r.transaction_count,
            TotalUnitsSold = r.total_units,
            TotalRevenue = r.total_revenue,
            // Estimate visitors based on transactions and conversion rate
            EstimatedVisitors = (int)Math.Ceiling(r.transaction_count / DefaultConversionRate),
            ConversionRate = DefaultConversionRate
        }).ToList();
    }

    /// <summary>
    /// Predicts visitor count for the next N days based on historical trends
    /// </summary>
    public async Task<IEnumerable<TrafficPrediction>> PredictTrafficAsync(int daysAhead = 7)
    {
        var historicalData = (await GetHistoricalTrafficAsync(30)).ToList();
        
        if (!historicalData.Any())
        {
            _logger.LogWarning("No historical data available for traffic prediction");
            return Enumerable.Empty<TrafficPrediction>();
        }

        var predictions = new List<TrafficPrediction>();
        
        // Calculate baseline metrics
        var avgDailyVisitors = historicalData.Average(d => d.EstimatedVisitors);
        var stdDev = CalculateStandardDeviation(historicalData.Select(d => (double)d.EstimatedVisitors).ToList());
        
        // Group by day of week to detect patterns
        var dayOfWeekAverages = historicalData
            .GroupBy(d => d.Date.DayOfWeek)
            .ToDictionary(g => g.Key, g => g.Average(d => d.EstimatedVisitors));

        // Calculate week-over-week trend
        var trend = CalculateWeeklyTrend(historicalData);

        for (int i = 1; i <= daysAhead; i++)
        {
            var predictionDate = DateTime.Today.AddDays(i);
            var dayOfWeek = predictionDate.DayOfWeek;
            
            // Start with day-of-week average if available, otherwise use overall average
            double basePrediction = dayOfWeekAverages.ContainsKey(dayOfWeek) 
                ? dayOfWeekAverages[dayOfWeek] 
                : avgDailyVisitors;
            
            // Apply trend adjustment (compound for future days)
            double trendMultiplier = Math.Pow(1 + trend, i / 7.0);
            int predictedVisitors = (int)Math.Round(basePrediction * trendMultiplier);
            
            // Apply special day adjustments
            predictedVisitors = ApplySpecialDayAdjustments(predictionDate, predictedVisitors);
            
            // Calculate confidence (decreases as we predict further out)
            double confidence = Math.Max(MinimumConfidence, 1.0 - (i * ConfidenceDecayRate));
            
            var trafficLevel = ClassifyTrafficLevel(predictedVisitors, avgDailyVisitors, stdDev);
            
            predictions.Add(new TrafficPrediction
            {
                PredictionDate = predictionDate,
                PredictedVisitors = predictedVisitors,
                ConfidenceLevel = Math.Round(confidence, 2),
                ExpectedLevel = trafficLevel,
                StaffingRecommendation = GenerateStaffingRecommendation(trafficLevel, dayOfWeek)
            });
        }

        return predictions;
    }

    /// <summary>
    /// Gets traffic analysis summary for dashboard display
    /// </summary>
    public async Task<TrafficAnalysisSummary> GetTrafficSummaryAsync()
    {
        var historical = (await GetHistoricalTrafficAsync(30)).ToList();
        var predictions = (await PredictTrafficAsync(7)).ToList();

        if (!historical.Any())
        {
            return new TrafficAnalysisSummary
            {
                HasData = false,
                Message = "Insufficient sales data for traffic analysis"
            };
        }

        var avgDaily = historical.Average(h => h.EstimatedVisitors);
        var trend = CalculateWeeklyTrend(historical);
        
        // Find peak days
        var peakDay = historical.OrderByDescending(h => h.EstimatedVisitors).First();
        var slowestDay = historical.OrderBy(h => h.EstimatedVisitors).First();
        
        // Day of week analysis
        var busiestDayOfWeek = historical
            .GroupBy(h => h.Date.DayOfWeek)
            .OrderByDescending(g => g.Average(h => h.EstimatedVisitors))
            .First().Key;

        return new TrafficAnalysisSummary
        {
            HasData = true,
            AverageDailyVisitors = (int)Math.Round(avgDaily),
            WeeklyTrendPercent = Math.Round(trend * 100, 1),
            TrendDirection = DetermineTrendDirection(trend),
            PeakDate = peakDay.Date,
            PeakVisitors = peakDay.EstimatedVisitors,
            SlowestDate = slowestDay.Date,
            SlowestVisitors = slowestDay.EstimatedVisitors,
            BusiestDayOfWeek = busiestDayOfWeek,
            TomorrowPrediction = predictions.FirstOrDefault(),
            WeekAheadPredictions = predictions
        };
    }

    /// <summary>
    /// Determines trend direction based on trend value
    /// </summary>
    private string DetermineTrendDirection(double trend)
    {
        if (trend > RisingTrendThreshold)
            return "Rising";
        
        if (trend < FallingTrendThreshold)
            return "Falling";
        
        return "Stable";
    }

    /// <summary>
    /// Calculates weekly trend as a percentage change
    /// </summary>
    private double CalculateWeeklyTrend(List<DailyTraffic> data)
    {
        if (data.Count < 14) return 0;

        var recentWeek = data.Where(d => d.Date >= DateTime.Today.AddDays(-7))
            .Sum(d => d.EstimatedVisitors);
        var previousWeek = data.Where(d => d.Date >= DateTime.Today.AddDays(-14) && d.Date < DateTime.Today.AddDays(-7))
            .Sum(d => d.EstimatedVisitors);

        if (previousWeek == 0) return 0;
        
        return (double)(recentWeek - previousWeek) / previousWeek;
    }

    /// <summary>
    /// Classifies predicted traffic into levels for easy understanding
    /// </summary>
    private TrafficLevel ClassifyTrafficLevel(int predicted, double average, double stdDev)
    {
        if (stdDev == 0) return TrafficLevel.Normal;
        
        double zScore = (predicted - average) / stdDev;

        return zScore switch
        {
            < VeryLowThreshold => TrafficLevel.VeryLow,
            < LowThreshold => TrafficLevel.Low,
            < HighThreshold => TrafficLevel.Normal,
            < VeryHighThreshold => TrafficLevel.High,
            _ => TrafficLevel.VeryHigh
        };
    }

    /// <summary>
    /// Adjusts predictions for known busy periods (holidays, product launches, etc.)
    /// </summary>
    private int ApplySpecialDayAdjustments(DateTime date, int baseVisitors)
    {
        // Black Friday (4th Friday of November)
        if (date.Month == 11 && date.DayOfWeek == DayOfWeek.Friday && date.Day >= 22 && date.Day <= 28)
            return (int)(baseVisitors * BlackFridayMultiplier);

        // Cyber Monday
        if (date.Month == 11 && date.DayOfWeek == DayOfWeek.Monday && date.Day >= 25 && date.Day <= 30)
            return (int)(baseVisitors * CyberMondayMultiplier);

        // Christmas Eve rush
        if (date.Month == 12 && date.Day >= 20 && date.Day <= 24)
            return (int)(baseVisitors * ChristmasRushMultiplier);

        // Post-Christmas returns/gift cards
        if (date.Month == 12 && date.Day >= 26 && date.Day <= 31)
            return (int)(baseVisitors * PostChristmasMultiplier);

        // New Year sales
        if (date.Month == 1 && date.Day <= 7)
            return (int)(baseVisitors * NewYearSalesMultiplier);

        // Apple launch season (September)
        if (date.Month == 9 && date.Day >= 10 && date.Day <= 25)
            return (int)(baseVisitors * AppleLaunchSeasonMultiplier);

        // Weekend boost
        if (date.DayOfWeek == DayOfWeek.Saturday)
            return (int)(baseVisitors * SaturdayMultiplier);
        if (date.DayOfWeek == DayOfWeek.Sunday)
            return (int)(baseVisitors * SundayMultiplier);

        return baseVisitors;
    }

    /// <summary>
    /// Generates staffing recommendations based on predicted traffic
    /// </summary>
    private string GenerateStaffingRecommendation(TrafficLevel level, DayOfWeek dayOfWeek)
    {
        var dayName = dayOfWeek.ToString();
        
        return level switch
        {
            TrafficLevel.VeryLow => $"🟢 {dayName}: Minimal staff needed. Consider reduced hours.",
            TrafficLevel.Low => $"🟢 {dayName}: Standard minimum staffing.",
            TrafficLevel.Normal => $"🟡 {dayName}: Normal staffing levels.",
            TrafficLevel.High => $"🟠 {dayName}: Schedule additional staff. Expect busy periods.",
            TrafficLevel.VeryHigh => $"🔴 {dayName}: PEAK DAY - All hands on deck. Consider extended hours.",
            _ => $"{dayName}: Standard staffing."
        };
    }

    /// <summary>
    /// Calculates standard deviation for a dataset
    /// </summary>
    private double CalculateStandardDeviation(List<double> values)
    {
        if (values.Count <= 1) return 0;
        
        double avg = values.Average();
        double sumOfSquares = values.Sum(v => Math.Pow(v - avg, 2));
        
        return Math.Sqrt(sumOfSquares / values.Count);
    }
}

