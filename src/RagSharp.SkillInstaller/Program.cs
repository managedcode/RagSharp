using System.CommandLine;
using System.Text.Json;

namespace RagSharp.SkillInstaller;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootOption = new Option<string>("--root", () => Directory.GetCurrentDirectory(), "Repository root");
        var skillDirOption = new Option<string>("--skill-dir", () => ".codex/skills", "Skill directory");
        var forceOption = new Option<bool>("--force", "Force install");
        var verboseOption = new Option<bool>("--verbose", "Verbose output");

        var installCommand = new Command("install", "Install ragsharp skills");
        installCommand.AddOption(rootOption);
        installCommand.AddOption(skillDirOption);
        installCommand.AddOption(forceOption);
        installCommand.AddOption(verboseOption);
        installCommand.SetHandler(async (root, skillDir, force, verbose) =>
        {
            await Installer.InstallAsync(root, skillDir, force, verbose, CancellationToken.None).ConfigureAwait(false);
        }, rootOption, skillDirOption, forceOption, verboseOption);

        var uninstallCommand = new Command("uninstall", "Uninstall ragsharp skills");
        uninstallCommand.AddOption(rootOption);
        uninstallCommand.AddOption(skillDirOption);
        uninstallCommand.SetHandler(async (root, skillDir) =>
        {
            await Installer.UninstallAsync(root, skillDir, CancellationToken.None).ConfigureAwait(false);
        }, rootOption, skillDirOption);

        var statusCommand = new Command("status", "Show install status");
        var formatOption = new Option<string>("--format", () => "json", "Output format");
        statusCommand.AddOption(rootOption);
        statusCommand.AddOption(skillDirOption);
        statusCommand.AddOption(formatOption);
        statusCommand.SetHandler(async (root, skillDir, format) =>
        {
            var status = await Installer.GetStatusAsync(root, skillDir, CancellationToken.None).ConfigureAwait(false);
            if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                Console.Out.WriteLine(JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
            }
            else
            {
                Console.Out.WriteLine(status.Installed ? "installed" : "not installed");
            }
        }, rootOption, skillDirOption, formatOption);

        var doctorCommand = new Command("doctor", "Check environment");
        doctorCommand.AddOption(rootOption);
        doctorCommand.SetHandler((root) =>
        {
            Installer.RunDoctor(root);
        }, rootOption);

        var rootCommand = new RootCommand("ragsharp installer")
        {
            installCommand,
            uninstallCommand,
            statusCommand,
            doctorCommand
        };

        return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
    }
}
