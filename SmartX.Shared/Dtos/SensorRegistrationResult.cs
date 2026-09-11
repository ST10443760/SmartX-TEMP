namespace SmartX.Shared.Dtos;

/// <summary>Result of a registration attempt, including why it failed if it did.</summary>
public sealed class SensorRegistrationResult
{
    public required bool Success { get; init; }

    public string? ErrorMessage { get; init; }

    public Guid? SensorId { get; init; }

    public static SensorRegistrationResult Ok(Guid sensorId) => new() { Success = true, SensorId = sensorId };

    public static SensorRegistrationResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}
