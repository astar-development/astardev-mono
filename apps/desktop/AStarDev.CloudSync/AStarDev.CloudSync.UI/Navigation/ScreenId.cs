namespace AStarDev.CloudSync.UI.Navigation;

/// <summary>Every destination the navigation service can show. The first eight are the rail; the rest sit under Settings &gt; Advanced. Each value needs exactly one registered <see cref="IScreen"/>.</summary>
public enum ScreenId
{
    /// <summary>Trust and health overview, and the landing screen.</summary>
    Dashboard,

    /// <summary>Operation visibility.</summary>
    Queue,

    /// <summary>Conflict resolution.</summary>
    Conflicts,

    /// <summary>Live execution.</summary>
    Activity,

    /// <summary>Completed activity.</summary>
    History,

    /// <summary>The audit trail: what the daemon recorded as having happened.</summary>
    Audit,

    /// <summary>Provider management.</summary>
    Providers,

    /// <summary>Configuration.</summary>
    Settings,

    /// <summary>Advanced operational surface: metrics. Reached from Settings.</summary>
    Metrics,

    /// <summary>Advanced operational surface: logs. Reached from Settings.</summary>
    Logs,

    /// <summary>Advanced operational surface: traces. Reached from Settings.</summary>
    Traces,

    /// <summary>Advanced operational surface: diagnostics. Reached from Settings.</summary>
    Diagnostics
}
