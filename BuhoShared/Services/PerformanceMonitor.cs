using System.Diagnostics;

namespace BuhoShared.Services;

public class PerformanceMonitor
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, Stopwatch> _activeTimers = new();
    private readonly Dictionary<string, PerformanceMetrics> _metrics = new();
    private readonly object _lockObject = new();

    public PerformanceMonitor(ILogger logger)
    {
        _logger = logger;
    }

    public void StartTimer(string operationId)
    {
        lock (_lockObject)
        {
            if (_activeTimers.ContainsKey(operationId))
            {
                _activeTimers[operationId].Restart();
            }
            else
            {
                _activeTimers[operationId] = Stopwatch.StartNew();
            }
        }
    }

    public void StopTimer(string operationId, string category = "Performance")
    {
        lock (_lockObject)
        {
            if (_activeTimers.TryGetValue(operationId, out var stopwatch))
            {
                stopwatch.Stop();
                var duration = stopwatch.Elapsed;
                
                UpdateMetrics(operationId, duration);
                _logger.LogPerformance(category, operationId, duration);
                
                _activeTimers.Remove(operationId);
            }
        }
    }

    public IDisposable TimeOperation(string operationId, string category = "Performance")
    {
        return new TimedOperation(this, operationId, category);
    }

    public PerformanceMetrics? GetMetrics(string operationId)
    {
        lock (_lockObject)
        {
            return _metrics.TryGetValue(operationId, out var metrics) ? metrics : null;
        }
    }

    public Dictionary<string, PerformanceMetrics> GetAllMetrics()
    {
        lock (_lockObject)
        {
            return new Dictionary<string, PerformanceMetrics>(_metrics);
        }
    }

    private void UpdateMetrics(string operationId, TimeSpan duration)
    {
        if (!_metrics.TryGetValue(operationId, out var metrics))
        {
            metrics = new PerformanceMetrics(operationId);
            _metrics[operationId] = metrics;
        }

        metrics.AddSample(duration);
    }

    private class TimedOperation : IDisposable
    {
        private readonly PerformanceMonitor _monitor;
        private readonly string _operationId;
        private readonly string _category;

        public TimedOperation(PerformanceMonitor monitor, string operationId, string category)
        {
            _monitor = monitor;
            _operationId = operationId;
            _category = category;
            _monitor.StartTimer(operationId);
        }

        public void Dispose()
        {
            _monitor.StopTimer(_operationId, _category);
        }
    }
}

public class PerformanceMetrics
{
    private readonly Queue<TimeSpan> _samples = new();
    private const int MAX_SAMPLES = 100;

    public string OperationId { get; }
    public int SampleCount { get; private set; }
    public TimeSpan TotalTime { get; private set; }
    public TimeSpan AverageTime => SampleCount > 0 ? TimeSpan.FromTicks(TotalTime.Ticks / SampleCount) : TimeSpan.Zero;
    public TimeSpan MinTime { get; private set; } = TimeSpan.MaxValue;
    public TimeSpan MaxTime { get; private set; } = TimeSpan.MinValue;
    public DateTime LastUpdate { get; private set; }

    public PerformanceMetrics(string operationId)
    {
        OperationId = operationId;
    }

    public void AddSample(TimeSpan duration)
    {
        _samples.Enqueue(duration);
        
        if (_samples.Count > MAX_SAMPLES)
        {
            var oldSample = _samples.Dequeue();
            TotalTime = TotalTime.Subtract(oldSample);
            SampleCount--;
        }

        TotalTime = TotalTime.Add(duration);
        SampleCount++;

        if (duration < MinTime) MinTime = duration;
        if (duration > MaxTime) MaxTime = duration;

        LastUpdate = DateTime.UtcNow;
    }

    public override string ToString()
    {
        return $"{OperationId}: Avg={AverageTime.TotalMilliseconds:F2}ms, Min={MinTime.TotalMilliseconds:F2}ms, Max={MaxTime.TotalMilliseconds:F2}ms, Samples={SampleCount}";
    }
}
