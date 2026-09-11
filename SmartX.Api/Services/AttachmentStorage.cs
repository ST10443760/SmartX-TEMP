using SmartX.Shared.Models;

namespace SmartX.Api.Services;

/// <summary>
/// Saves sensor config files, deployment photos, and hardware logs to disk
/// under a per-sensor folder, and builds the metadata record the ledger
/// tracks against the sensor profile.
/// </summary>
public sealed class AttachmentStorage(IWebHostEnvironment environment)
{
    private const long MaxFileSizeBytes = 25 * 1024 * 1024; // 25 MB

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".json", ".txt", ".log", ".cfg", ".xml", ".yaml", ".yml",
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private string RootPath => Path.Combine(environment.ContentRootPath, "UploadedFiles");

    public (bool IsValid, string? Error) Validate(IFormFile file)
    {
        if (file.Length == 0)
        {
            return (false, "The uploaded file is empty.");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return (false, $"File exceeds the {MaxFileSizeBytes / (1024 * 1024)} MB limit.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
        {
            return (false, $"File type '{extension}' is not permitted for sensor attachments.");
        }

        return (true, null);
    }

    public async Task<SensorAttachment> SaveAsync(string macAddress, IFormFile file, CancellationToken cancellationToken)
    {
        var sensorFolder = Path.Combine(RootPath, SanitiseForPath(macAddress));
        Directory.CreateDirectory(sensorFolder);

        var storedFileName = $"{Guid.NewGuid():N}_{Path.GetFileName(file.FileName)}";
        var fullPath = Path.Combine(sensorFolder, storedFileName);

        await using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        return new SensorAttachment
        {
            Id = Guid.NewGuid(),
            SensorMacAddress = macAddress,
            FileName = file.FileName,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            SizeBytes = file.Length,
            StoredFileName = storedFileName
        };
    }

    public string ResolvePath(string macAddress, string storedFileName)
        => Path.Combine(RootPath, SanitiseForPath(macAddress), storedFileName);

    private static string SanitiseForPath(string macAddress)
        => macAddress.Replace(":", "").Replace("-", "").ToUpperInvariant();
}
