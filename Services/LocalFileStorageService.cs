namespace ResumeAnalyzer.Api.Services;

public interface IFileStorageService
{
    /// <summary>
    /// Saves the uploaded file to disk under wwwroot/uploads using a
    /// collision-proof generated name, and returns the relative path
    /// that should be stored in the database.
    /// </summary>
    Task<string> SaveAsync(IFormFile file);
}

public class LocalFileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _environment;
    private const string UploadsFolder = "uploads";

    public LocalFileStorageService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<string> SaveAsync(IFormFile file)
    {
        var webRoot = _environment.WebRootPath
            ?? Path.Combine(_environment.ContentRootPath, "wwwroot");

        var uploadsPath = Path.Combine(webRoot, UploadsFolder);
        Directory.CreateDirectory(uploadsPath);

        var extension = Path.GetExtension(file.FileName);
        var safeFileName = $"{Guid.NewGuid()}{extension}";
        var fullPath = Path.Combine(uploadsPath, safeFileName);

        await using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // Store the relative path (not the absolute disk path) so it's
        // portable across environments/deployments.
        return Path.Combine(UploadsFolder, safeFileName).Replace('\\', '/');
    }
}
