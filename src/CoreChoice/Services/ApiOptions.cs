namespace CoreChoice.Services;

/// <summary>
/// Where the backend lives. A plain settable POCO rather than a record, because the options
/// pattern binds it from configuration.
/// </summary>
public sealed class ApiOptions
{
    public string BaseUrl { get; set; } = "https://api.corechoice.lechdigital.nl/";
}
