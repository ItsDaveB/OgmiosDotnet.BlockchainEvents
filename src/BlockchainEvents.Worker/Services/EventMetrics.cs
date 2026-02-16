namespace BlockchainEvents.Worker.Services;

public interface IEventMetrics
{
    void RecordEventEmitted(string ruleName);
    void RecordEventFailed(string ruleName, string errorType);
    void RecordProcessingLatency(double milliseconds, string ruleName);
    void RecordBlockProcessed(long slot, int transactionCount);
    void SetEnabledRules(int count, IEnumerable<string> ruleNames);
    Stopwatch StartTimer();
}

public sealed class EventMetrics : IEventMetrics
{
    private static readonly Counter EventsEmittedTotal = Metrics.CreateCounter(
        "blockchain_events_emitted_total",
        "Total number of blockchain events successfully emitted",
        new CounterConfiguration { LabelNames = ["rule"] });

    private static readonly Counter EventsFailedTotal = Metrics.CreateCounter(
        "blockchain_events_failed_total",
        "Total number of blockchain events that failed and were sent to DLQ",
        new CounterConfiguration { LabelNames = ["rule", "error_type"] });

    private static readonly Histogram ProcessingLatencySeconds = Metrics.CreateHistogram(
        "blockchain_events_processing_latency_seconds",
        "Time taken to process and emit blockchain events",
        new HistogramConfiguration
        {
            LabelNames = ["rule"],
            Buckets = [0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10]
        });

    private static readonly Counter BlocksProcessedTotal = Metrics.CreateCounter(
        "blockchain_blocks_processed_total",
        "Total number of blocks processed");

    private static readonly Gauge LastProcessedSlot = Metrics.CreateGauge(
        "blockchain_last_processed_slot",
        "The slot number of the last processed block");

    private static readonly Counter TransactionsProcessedTotal = Metrics.CreateCounter(
        "blockchain_transactions_processed_total",
        "Total number of transactions processed");

    private static readonly Gauge EventsInFlightGauge = Metrics.CreateGauge(
        "blockchain_events_in_flight",
        "Number of events currently being processed");

    private static readonly Gauge EnabledRulesGauge = Metrics.CreateGauge(
        "blockchain_enabled_rules_total",
        "Number of enabled transaction rules");

    private static readonly Gauge RuleEnabledInfo = Metrics.CreateGauge(
        "blockchain_rule_enabled",
        "Whether a rule is enabled (1) or disabled (0)",
        new GaugeConfiguration { LabelNames = ["rule"] });

    public void RecordEventEmitted(string ruleName)
    {
        EventsEmittedTotal.WithLabels(ruleName).Inc();
    }

    public void RecordEventFailed(string ruleName, string errorType)
    {
        EventsFailedTotal.WithLabels(ruleName, errorType).Inc();
    }

    public void RecordProcessingLatency(double milliseconds, string ruleName)
    {
        ProcessingLatencySeconds.WithLabels(ruleName).Observe(milliseconds / 1000.0);
    }

    public void RecordBlockProcessed(long slot, int transactionCount)
    {
        BlocksProcessedTotal.Inc();
        LastProcessedSlot.Set(slot);
        TransactionsProcessedTotal.Inc(transactionCount);
    }

    public void SetEnabledRules(int count, IEnumerable<string> ruleNames)
    {
        EnabledRulesGauge.Set(count);
        foreach (var name in ruleNames)
            RuleEnabledInfo.WithLabels(name).Set(1);
    }

    public Stopwatch StartTimer()
    {
        EventsInFlightGauge.Inc();
        return Stopwatch.StartNew();
    }

    public static void CompleteProcessing()
    {
        EventsInFlightGauge.Dec();
    }
}
