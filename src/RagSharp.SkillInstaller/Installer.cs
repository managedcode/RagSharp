using System.Reflection;
using System.Text.Json;

namespace RagSharp.SkillInstaller;

public static class Installer
{
    private const string ManifestName = "ragsharp.manifest.json";

    public static async Task InstallAsync(string root, string skillDir, bool force, bool verbose, CancellationToken cancellationToken)
    {
        var rootPath = ResolveRoot(root);
        var targetDir = Path.Combine(rootPath, skillDir);
        Directory.CreateDirectory(targetDir);

        var manifestPath = Path.Combine(targetDir, ManifestName);
        if (File.Exists(manifestPath))
        {
            if (!force)
            {
                throw new InvalidOperationException("Skills already installed. Use --force to reinstall.");
            }

            await RemoveInstalledFilesAsync(manifestPath, targetDir, cancellationToken).ConfigureAwait(false);
        }

        var tempDir = Path.Combine(targetDir, $".tmp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var installedFiles = ExtractTemplates(tempDir, verbose);
        foreach (var file in installedFiles)
        {
            var destination = Path.Combine(targetDir, file);
            Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? targetDir);
            File.Copy(Path.Combine(tempDir, file), destination, true);
        }

        var manifest = new InstallManifest
        {
            Version = GetVersion(),
            InstalledAtUtc = DateTimeOffset.UtcNow,
            Files = installedFiles
        };

        var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        await File.WriteAllTextAsync(manifestPath, manifestJson, cancellationToken).ConfigureAwait(false);

        Directory.Delete(tempDir, true);
        Console.Error.WriteLine($"Installed ragsharp skills to {targetDir}.");
    }

    public static async Task UninstallAsync(string root, string skillDir, CancellationToken cancellationToken)
    {
        var rootPath = ResolveRoot(root);
        var targetDir = Path.Combine(rootPath, skillDir);
        var manifestPath = Path.Combine(targetDir, ManifestName);
        if (!File.Exists(manifestPath))
        {
            Console.Error.WriteLine("No ragsharp manifest found.");
            return;
        }

        await RemoveInstalledFilesAsync(manifestPath, targetDir, cancellationToken).ConfigureAwait(false);

        Console.Error.WriteLine("Removed ragsharp skills.");
    }

    public static async Task<InstallStatus> GetStatusAsync(string root, string skillDir, CancellationToken cancellationToken)
    {
        var rootPath = ResolveRoot(root);
        var targetDir = Path.Combine(rootPath, skillDir);
        var manifestPath = Path.Combine(targetDir, ManifestName);
        if (!File.Exists(manifestPath))
        {
            return new InstallStatus(false, null, null, targetDir, Array.Empty<string>());
        }

        var manifest = JsonSerializer.Deserialize<InstallManifest>(await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false));
        var files = manifest?.Files ?? new List<string>();
        return new InstallStatus(true, manifest?.Version, manifest?.InstalledAtUtc, targetDir, files);
    }

    public static void RunDoctor(string root)
    {
        var rootPath = ResolveRoot(root);
        Console.Error.WriteLine($"Root: {rootPath}");
        Console.Error.WriteLine("dotnet: ensure .NET SDK is installed and on PATH.");
        Console.Error.WriteLine("MSBuildWorkspace: available with Microsoft.CodeAnalysis.Workspaces.MSBuild.");
    }

    private static string ResolveRoot(string root)
    {
        var current = Path.GetFullPath(root);
        var dir = new DirectoryInfo(current);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        return current;
    }

    private static List<string> ExtractTemplates(string outputDir, bool verbose)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resources = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith("skill-templates/", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var files = new List<string>();
        foreach (var resource in resources)
        {
            var relative = resource.Replace("skill-templates/", string.Empty, StringComparison.OrdinalIgnoreCase);
            var outputPath = Path.Combine(outputDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? outputDir);
            using var stream = assembly.GetManifestResourceStream(resource);
            if (stream is null)
            {
                continue;
            }

            using var fileStream = File.Create(outputPath);
            stream.CopyTo(fileStream);
            files.Add(relative);
            if (verbose)
            {
                Console.Error.WriteLine($"Extracted {relative}");
            }
        }

        return files;
    }

    private static async Task RemoveInstalledFilesAsync(string manifestPath, string targetDir, CancellationToken cancellationToken)
    {
        var manifest = JsonSerializer.Deserialize<InstallManifest>(await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false))
            ?? new InstallManifest();

        foreach (var file in manifest.Files)
        {
            var path = Path.Combine(targetDir, file);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        if (File.Exists(manifestPath))
        {
            File.Delete(manifestPath);
        }
    }

    private static string GetVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
    }
}

public sealed class InstallManifest
{
    public string? Version { get; set; }
    public DateTimeOffset InstalledAtUtc { get; set; }
    public List<string> Files { get; set; } = new();
}

public sealed record InstallStatus(
    bool Installed,
    string? Version,
    DateTimeOffset? InstalledAtUtc,
    string SkillDir,
    IReadOnlyList<string> Files);
