namespace NewHorizon.Automation.Domain.Jobs;

/// <summary>
/// Lifecycle of one workflow run. Persisted as a string so the filtered unique index on
/// IdempotencyKey (WHERE Status &lt;&gt; 'Cancelled') stays readable in SQL.
/// </summary>
public enum JobStatus
{
    /// <summary>Enqueued and waiting to be claimed. This set is the queue.</summary>
    Pending = 0,

    /// <summary>Claimed by a worker and executing.</summary>
    Running = 1,

    /// <summary>Paused at a Partial-mode gate; needs a business approval to continue.</summary>
    AwaitingApproval = 2,

    /// <summary>Stopped on a business error or exhausted retries. Recoverable via retry/resume.</summary>
    Failed = 3,

    /// <summary>All stages and operations finished. Terminal.</summary>
    Completed = 4,

    /// <summary>Abandoned by an operator or by a rejection. Terminal.</summary>
    Cancelled = 5,
}
