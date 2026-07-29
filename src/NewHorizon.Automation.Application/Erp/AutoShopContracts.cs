namespace NewHorizon.Automation.Application.Erp;

/// <summary>
/// A site (location) in the client's ERP. One installation serves one client, but that client has
/// several sites, and every AutoShop call is scoped to one of them.
/// </summary>
/// <remarks>
/// From <c>GET /api/v1/admin/location/list</c>, which the ERP team is modifying. Fields beyond the
/// identifier are provisional until that response is confirmed.
/// </remarks>
public sealed record ErpSite(string SiteId, string? Name);

/// <summary>
/// One SJO row returned for a site, already known to have its BOM created. Ordered by
/// <paramref name="DeliveryDate"/> ascending before submission — the sequence is the point of the
/// call, so the ordering is part of the contract rather than a display concern.
/// </summary>
/// <remarks>
/// From <c>GET /api/v1/planning/autoshopsjosequence/GetSJODetails/{SiteId}/S</c>. The full row
/// shape is pending; only the fields the agent must act on are modelled here.
/// </remarks>
public sealed record SjoSequenceRow(
    string SjoNumber,
    DateTimeOffset? DeliveryDate,
    int? Sequence);

/// <summary>
/// An OAF awaiting SJO creation — the agent's entry point, since everything before this is
/// manually authorised inside the ERP.
/// </summary>
public sealed record OafAwaitingSjo(string OafNumber, string? SiteId);

/// <summary>Outcome of submitting a sorted SJO sequence for one site.</summary>
public sealed record SequenceSubmissionResult(int RowsSubmitted, string? Reference);
