using System.ComponentModel.DataAnnotations;

namespace NewHorizon.Automation.Application.Configuration;

/// <summary>
/// Connection to the automation database only. The agent never connects to the ERP database;
/// all ERP effects go through <c>IErpClient</c> over HTTP.
/// </summary>
public sealed class DatabaseOptions
{
    [Required(AllowEmptyStrings = false)]
    public string ConnectionString { get; init; } = string.Empty;
}
