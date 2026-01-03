using RagSharp.SkillInstaller;

namespace RagSharp.SkillInstaller.Tests;

public class InstallerTests
{
    [Test]
    public async Task InstallsAndUninstallsSkills()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"ragsharp-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        Directory.CreateDirectory(Path.Combine(tempRoot, ".git"));

        await Installer.InstallAsync(tempRoot, ".codex/skills", force: true, verbose: false, CancellationToken.None);
        var status = await Installer.GetStatusAsync(tempRoot, ".codex/skills", CancellationToken.None);
        await Assert.That(status.Installed).IsTrue();
        var manifestPath = Path.Combine(tempRoot, ".codex/skills", "ragsharp.manifest.json");
        await Assert.That(File.Exists(manifestPath)).IsTrue();

        await Installer.UninstallAsync(tempRoot, ".codex/skills", CancellationToken.None);
        status = await Installer.GetStatusAsync(tempRoot, ".codex/skills", CancellationToken.None);
        await Assert.That(status.Installed).IsFalse();
    }

    [Test]
    public async Task ReturnsStatusWhenNothingInstalled()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"ragsharp-status-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        Directory.CreateDirectory(Path.Combine(tempRoot, ".git"));

        var status = await Installer.GetStatusAsync(tempRoot, ".codex/skills", CancellationToken.None);

        await Assert.That(status.Installed).IsFalse();
        await Assert.That(status.Files).IsEmpty();
        await Assert.That(status.SkillDir).Contains(".codex/skills");
    }
}
