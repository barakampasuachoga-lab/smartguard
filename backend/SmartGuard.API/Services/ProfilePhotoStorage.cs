namespace SmartGuard.API.Services;

public static class ProfilePhotoStorage
{
    public const long MaximumFileSize = 5 * 1024 * 1024;

    public static async Task<(string? Url, string? Error)> SaveAsync(
        IFormFile? file,
        IWebHostEnvironment environment,
        string category = "profiles",
        string imageLabel = "Profile photo")
    {
        if (file is null || file.Length == 0)
        {
            return (null, $"A {imageLabel.ToLowerInvariant()} is required.");
        }

        if (category is not ("profiles" or "properties"))
        {
            return (null, "The image storage category is not supported.");
        }

        if (file.Length > MaximumFileSize)
        {
            return (null, $"{imageLabel}s must be 5 MB or smaller.");
        }

        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        var bytes = stream.ToArray();
        var extension = GetImageExtension(bytes);
        if (extension is null)
        {
            return (null, $"Upload a valid JPEG, PNG, or WebP {imageLabel.ToLowerInvariant()}.");
        }

        var filename = $"{Guid.NewGuid():N}.{extension}";
        var directory = Path.Combine(environment.ContentRootPath, "wwwroot", "uploads", category);
        Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(Path.Combine(directory, filename), bytes);

        return ($"/uploads/{category}/{filename}", null);
    }

    public static void Delete(IWebHostEnvironment environment, string? url)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            !(url.StartsWith("/uploads/profiles/", StringComparison.Ordinal) ||
              url.StartsWith("/uploads/properties/", StringComparison.Ordinal)))
        {
            return;
        }

        var category = url.StartsWith("/uploads/properties/", StringComparison.Ordinal) ? "properties" : "profiles";
        var filename = Path.GetFileName(url);
        var path = Path.Combine(environment.ContentRootPath, "wwwroot", "uploads", category, filename);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string? GetImageExtension(byte[] bytes)
    {
        if (bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return "png";
        }

        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return "jpg";
        }

        if (bytes.Length >= 12 &&
            bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
            bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8))
        {
            return "webp";
        }

        return null;
    }
}