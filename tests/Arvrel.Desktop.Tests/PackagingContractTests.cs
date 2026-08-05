using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Arvrel.Desktop.Tests;

[TestClass]
public sealed class PackagingContractTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [TestMethod]
    public void WindowsInstaller_RemainsPerUserAndNonElevated()
    {
        var script = Read("packaging", "avalonia", "windows", "ARVREL-Avalonia.iss");

        StringAssert.Contains(script, "PrivilegesRequired=lowest");
        StringAssert.Contains(script, @"DefaultDirName={localappdata}\Programs\ARVREL-Avalonia");
        StringAssert.Contains(script, "ARVREL-Avalonia-v{#AppVersion}-win-x64-setup");
        Assert.IsFalse(
            script.Contains("PrivilegesRequiredOverridesAllowed", StringComparison.Ordinal),
            "The cross-platform Windows installer must not offer an elevation override.");
        Assert.IsFalse(
            script.Contains(@"{pf}\", StringComparison.OrdinalIgnoreCase) ||
            script.Contains("Program Files", StringComparison.OrdinalIgnoreCase),
            "The installer must not target a machine-wide Program Files location.");
    }

    [TestMethod]
    public void LinuxPackages_UseStableApplicationAndLauncherPaths()
    {
        var packageScript = Read("scripts", "package-avalonia-linux.sh");
        var desktopEntry = Read("packaging", "avalonia", "linux", "arvrel.desktop");

        StringAssert.Contains(packageScript, "/opt/arvrel");
        StringAssert.Contains(packageScript, "/usr/bin/arvrel");
        StringAssert.Contains(packageScript, "linux-x64.tar.gz");
        StringAssert.Contains(packageScript, "linux-x64.deb");
        StringAssert.Contains(packageScript, "no libpcap backend is implemented");
        StringAssert.Contains(desktopEntry, "Exec=/opt/arvrel/Arvrel.Desktop");
        StringAssert.Contains(desktopEntry, "Icon=arvrel");
        StringAssert.Contains(desktopEntry, "Terminal=false");
    }

    [TestMethod]
    public void MacPackages_DeclareStableBundleAndUnsignedDistributionBoundary()
    {
        var packageScript = Read("scripts", "package-avalonia-macos.sh");
        var plist = Read("packaging", "avalonia", "macos", "Info.plist");

        StringAssert.Contains(plist, "io.github.masarray.arvrel");
        StringAssert.Contains(plist, "Arvrel.Desktop");
        StringAssert.Contains(plist, "LSMinimumSystemVersion");
        StringAssert.Contains(packageScript, "codesign --force --deep --sign -");
        StringAssert.Contains(packageScript, "\"notarized\": false");
        StringAssert.Contains(packageScript, "osx-arm64.app.zip");
        StringAssert.Contains(packageScript, "osx-arm64.dmg");
    }

    [TestMethod]
    public void Workflow_PublishesSelfContainedVerifiedPackagesForAllPlatforms()
    {
        var workflow = Read(".github", "workflows", "avalonia-packaging.yml");

        foreach (var rid in new[] { "win-x64", "linux-x64", "osx-arm64" })
            StringAssert.Contains(workflow, $"--runtime {rid}");

        Assert.AreEqual(
            3,
            CountOccurrences(workflow, "--self-contained true"),
            "Every supported desktop RID must be published self-contained.");

        foreach (var packageSuffix in new[]
        {
            "win-x64-portable.zip",
            "win-x64-setup.exe",
            "linux-x64.tar.gz",
            "linux-x64.deb",
            "osx-arm64.app.zip",
            "osx-arm64.dmg"
        })
        {
            StringAssert.Contains(workflow, packageSuffix);
        }

        StringAssert.Contains(workflow, "sha256sum --check SHA256SUMS-win-x64.txt");
        StringAssert.Contains(workflow, "sha256sum --check SHA256SUMS-linux-x64.txt");
        StringAssert.Contains(workflow, "sha256sum --check SHA256SUMS-osx-arm64.txt");
        StringAssert.Contains(workflow, "actions/attest-build-provenance@");
    }

    private static string Read(params string[] path)
    {
        var fullPath = Path.Combine(new[] { RepositoryRoot }.Concat(path).ToArray());
        Assert.IsTrue(File.Exists(fullPath), $"Expected packaging contract file does not exist: {fullPath}");
        return File.ReadAllText(fullPath);
    }

    private static int CountOccurrences(string value, string token)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }
        return count;
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "VERSION")) &&
                Directory.Exists(Path.Combine(current.FullName, ".github")) &&
                Directory.Exists(Path.Combine(current.FullName, "src")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate the ARVREL repository root above {AppContext.BaseDirectory}.");
    }
}
