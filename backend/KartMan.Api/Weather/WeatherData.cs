namespace KartMan.Api.Weather;

public sealed record WeatherData(
    DateTime TimestampUtc,
    decimal TempC,
    bool IsDay,
    int ConditionCode,
    string ConditionText,
    decimal WindKph,
    decimal WindDegree,
    decimal PressureMb,
    decimal PrecipitationMm,
    decimal Humidity,
    decimal Cloud,
    decimal FeelsLikeC,
    decimal DewPointC)
{
    /// <summary>
    /// Database identifier.
    /// </summary>
    public long Id { get; set; }
}
