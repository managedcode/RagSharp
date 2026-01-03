using RagSharp.SkillInstaller;
using Xunit;

namespace RagSharp.SkillInstaller.Tests;

public class InstallerTests
{
    [Fact]
    public async Task InstallsAndUninstallsSkills()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"ragsharp-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        Directory.CreateDirectory(Path.Combine(tempRoot, ".git"));

        await Installer.InstallAsync(tempRoot, ".codex/skills", force: true, verbose: false, CancellationToken.None);
        var status = await Installer.GetStatusAsync(tempRoot, ".codex/skills", CancellationToken.None);
        Assert.True(status.Installed);
        Assert.NotEmpty(status.Files);

        await Installer.UninstallAsync(tempRoot, ".codex/skills", CancellationToken.None);
        status = await Installer.GetStatusAsync(tempRoot, ".codex/skills", CancellationToken.None);
        Assert.False(status.Installed);
    }
}
