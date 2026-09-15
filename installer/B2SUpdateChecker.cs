using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: AssemblyTitle("B2S Pro Update Checker")]
[assembly: AssemblyProduct("B2S Pro")]
[assembly: AssemblyCompany("B2S Pro")]
[assembly: AssemblyCopyright("Copyright © 2026 Ken Wildman")]
[assembly: AssemblyVersion("1.0.1.0")]
[assembly: AssemblyFileVersion("1.0.1.0")]
[assembly: AssemblyInformationalVersion("1.0.1")]

namespace B2SPro.Updates
{
    internal enum UpdateComponent
    {
        Unknown,
        Designer,
        Server
    }

    internal static class Program
    {
        private const string CheckNowArgument = "--check-now";
        private const string TestNotificationArgument = "--test-notification";
        private const string SelfTestArgument = "--self-test";

        [STAThread]
        private static void Main(string[] args)
        {
            if (HasArgument(args, SelfTestArgument))
            {
                Environment.ExitCode = SelfTest.Run();
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            UpdateComponent component = ComponentSelection.Resolve(args, AppDomain.CurrentDomain.BaseDirectory);
            if (HasArgument(args, TestNotificationArgument))
            {
                if (component == UpdateComponent.Unknown)
                {
                    MessageBox.Show(
                        "Choose either designer or server after --test-notification.",
                        "B2S Pro Update Test",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    Environment.ExitCode = 2;
                    return;
                }

                ShowTestNotification(UpdateTarget.Create(component, AppDomain.CurrentDomain.BaseDirectory));
                return;
            }

            bool interactive = HasArgument(args, CheckNowArgument);
            if (component == UpdateComponent.Unknown)
            {
                if (interactive)
                {
                    MessageBox.Show(
                        "The update checker could not identify a B2S Pro Designer or B2S Server installation beside it.",
                        "B2S Pro Update Check",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                Environment.ExitCode = 2;
                return;
            }

            try
            {
                RunCheck(UpdateTarget.Create(component, AppDomain.CurrentDomain.BaseDirectory), interactive);
            }
            catch (Exception ex)
            {
                // Automatic checks must never interrupt startup or table shutdown.
                // A user-requested check reports the exact failure instead.
                if (interactive)
                {
                    MessageBox.Show(
                        "The update check could not be completed. Nothing was changed.\r\n\r\n" + ex.Message,
                        "B2S Pro Update Check",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                Environment.ExitCode = 1;
            }
        }

        private static void RunCheck(UpdateTarget target, bool interactive)
        {
            string mutexName = "Local\\B2SPro.UpdateCheck." + target.Component;
            using (var mutex = new Mutex(false, mutexName))
            {
                bool ownsMutex;
                try
                {
                    ownsMutex = mutex.WaitOne(0, false);
                }
                catch (AbandonedMutexException)
                {
                    ownsMutex = true;
                }

                if (!ownsMutex) return;
                try
                {
                    if (!interactive && !DailyCheckState.Begin(target.Component)) return;

                    Version installedVersion = target.ReadInstalledVersion();
                    AvailableUpdate available = GitHubReleaseClient.GetLatest(target);
                    if (available.Version <= installedVersion)
                    {
                        if (interactive)
                        {
                            MessageBox.Show(
                                target.ProductName + " is up to date.\r\n\r\nInstalled version: " + VersionText(installedVersion),
                                "B2S Pro Update Check",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                        }
                        return;
                    }

                    string message = target.ProductName + " update available.\r\n\r\n" +
                        "Installed version: " + VersionText(installedVersion) + "\r\n" +
                        "Available version: " + VersionText(available.Version) + "\r\n\r\n" +
                        "Download the verified update and open setup now?\r\n\r\n" +
                        target.CloseBeforeInstallMessage;
                    DialogResult answer = MessageBox.Show(
                        message,
                        "B2S Pro Update Available",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information,
                        MessageBoxDefaultButton.Button2);
                    if (answer != DialogResult.Yes) return;

                    try
                    {
                        string setupPath = GitHubReleaseClient.DownloadVerifiedSetup(target, available);
                        LaunchSetup(target, setupPath);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            "The update was not started. Nothing was installed.\r\n\r\n" + ex.Message,
                            "B2S Pro Update",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
        }

        private static void LaunchSetup(UpdateTarget target, string setupPath)
        {
            var start = new ProcessStartInfo();
            start.FileName = setupPath;
            start.Arguments = target.SetupFolderArgument + " " + QuoteArgument(target.InstallationFolder);
            start.WorkingDirectory = Path.GetDirectoryName(setupPath);
            start.UseShellExecute = true;
            if (Process.Start(start) == null) throw new InvalidOperationException("Windows did not start the setup program.");
        }

        private static void ShowTestNotification(UpdateTarget target)
        {
            Version installed;
            try
            {
                installed = target.ReadInstalledVersion();
            }
            catch
            {
                installed = target.Component == UpdateComponent.Designer
                    ? new Version(1, 0, 1, 0)
                    : new Version(3, 0, 0, 0);
            }

            Version available = new Version(installed.Major, installed.Minor, Math.Max(0, installed.Build) + 1, 0);
            string message = "TEST NOTIFICATION — no files will be downloaded or changed.\r\n\r\n" +
                target.ProductName + " update available.\r\n\r\n" +
                "Installed version: " + VersionText(installed) + "\r\n" +
                "Available version: " + VersionText(available) + "\r\n\r\n" +
                "Download the verified update and open setup now?\r\n\r\n" +
                target.CloseBeforeInstallMessage;
            DialogResult answer = MessageBox.Show(
                message,
                "B2S Pro Update Test",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button2);
            if (answer == DialogResult.Yes)
            {
                MessageBox.Show(
                    "Test passed. In a real update, the verified setup program would open now.\r\n\r\nNothing was downloaded, installed, or changed.",
                    "B2S Pro Update Test",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private static bool HasArgument(string[] args, string expected)
        {
            foreach (string argument in args)
            {
                if (String.Equals(argument, expected, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + value.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace("\"", "") + "\"";
        }

        internal static string VersionText(Version version)
        {
            if (version.Revision > 0) return version.Major + "." + version.Minor + "." + Math.Max(0, version.Build) + "." + version.Revision;
            return version.Major + "." + version.Minor + "." + Math.Max(0, version.Build);
        }
    }

    internal static class ComponentSelection
    {
        public static UpdateComponent Resolve(string[] args, string baseDirectory)
        {
            foreach (string argument in args)
            {
                UpdateComponent parsed = Parse(argument);
                if (parsed != UpdateComponent.Unknown) return parsed;
            }

            bool designerExists = File.Exists(Path.Combine(baseDirectory, "B2SPro.exe"));
            bool serverExists = File.Exists(Path.Combine(baseDirectory, "B2SBackglassServer.dll"));
            return Infer(designerExists, serverExists);
        }

        internal static UpdateComponent Parse(string value)
        {
            if (String.Equals(value, "designer", StringComparison.OrdinalIgnoreCase)) return UpdateComponent.Designer;
            if (String.Equals(value, "server", StringComparison.OrdinalIgnoreCase)) return UpdateComponent.Server;
            return UpdateComponent.Unknown;
        }

        internal static UpdateComponent Infer(bool designerExists, bool serverExists)
        {
            if (designerExists == serverExists) return UpdateComponent.Unknown;
            return designerExists ? UpdateComponent.Designer : UpdateComponent.Server;
        }
    }

    internal sealed class UpdateTarget
    {
        public readonly UpdateComponent Component;
        public readonly string ProductName;
        public readonly string InstallationFolder;
        public readonly string InstalledFileName;
        public readonly string PackagePattern;
        public readonly string SetupAssetName;
        public readonly string SetupFolderArgument;
        public readonly string CloseBeforeInstallMessage;

        private UpdateTarget(
            UpdateComponent component,
            string productName,
            string installationFolder,
            string installedFileName,
            string packagePattern,
            string setupAssetName,
            string setupFolderArgument,
            string closeBeforeInstallMessage)
        {
            Component = component;
            ProductName = productName;
            InstallationFolder = Path.GetFullPath(installationFolder);
            InstalledFileName = installedFileName;
            PackagePattern = packagePattern;
            SetupAssetName = setupAssetName;
            SetupFolderArgument = setupFolderArgument;
            CloseBeforeInstallMessage = closeBeforeInstallMessage;
        }

        public static UpdateTarget Create(UpdateComponent component, string baseDirectory)
        {
            if (component == UpdateComponent.Designer)
            {
                return new UpdateTarget(
                    component,
                    "B2S Pro Designer",
                    baseDirectory,
                    "B2SPro.exe",
                    "^B2S-Pro-Backglass-(?<version>[0-9]+(?:\\.[0-9]+){1,3})\\.zip$",
                    "B2SProSetup.exe",
                    "--installed-designer",
                    "Before clicking Install / Update, save your work and close B2S Pro and Visual Pinball.");
            }
            if (component == UpdateComponent.Server)
            {
                return new UpdateTarget(
                    component,
                    "B2S Server",
                    baseDirectory,
                    "B2SBackglassServer.dll",
                    "^B2S-Pro-Server-(?<version>[0-9]+(?:\\.[0-9]+){1,3})\\.zip$",
                    "B2SServerSetup.exe",
                    "--installed-server",
                    "Before clicking Install / Update, close Visual Pinball so the Server files are no longer in use.");
            }
            throw new ArgumentException("An update component is required.", "component");
        }

        public Version ReadInstalledVersion()
        {
            string installedPath = Path.Combine(InstallationFolder, InstalledFileName);
            if (!File.Exists(installedPath)) throw new FileNotFoundException("The installed program file was not found beside the update checker.", installedPath);
            string value = FileVersionInfo.GetVersionInfo(installedPath).FileVersion;
            return VersionRules.ParseAndNormalize(value);
        }

        public Version ParsePackageVersion(string assetName)
        {
            Match match = Regex.Match(assetName ?? String.Empty, PackagePattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success) return null;
            return VersionRules.ParseAndNormalize(match.Groups["version"].Value);
        }
    }

    internal static class VersionRules
    {
        public static Version ParseAndNormalize(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) throw new FormatException("A version number is missing.");
            Match match = Regex.Match(value.Trim(), "^[vV]?(?<version>[0-9]+(?:\\.[0-9]+){1,3})");
            if (!match.Success) throw new FormatException("The version number is invalid: " + value);
            Version parsed;
            if (!Version.TryParse(match.Groups["version"].Value, out parsed)) throw new FormatException("The version number is invalid: " + value);
            return new Version(parsed.Major, parsed.Minor, Math.Max(0, parsed.Build), Math.Max(0, parsed.Revision));
        }
    }

    internal sealed class AvailableUpdate
    {
        public readonly Version Version;
        public readonly string SetupUrl;
        public readonly string SetupDigest;

        public AvailableUpdate(Version version, string setupUrl, string setupDigest)
        {
            Version = version;
            SetupUrl = setupUrl;
            SetupDigest = setupDigest;
        }
    }

    internal static class GitHubReleaseClient
    {
        private const string LatestApi = "https://api.github.com/repos/KWildman69/B2S-Pro/releases/latest";
        private const int MaximumSetupBytes = 32 * 1024 * 1024;

        public static AvailableUpdate GetLatest(UpdateTarget target)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            using (var client = CreateClient(target.Component))
            {
                string json = client.GetStringAsync(LatestApi).GetAwaiter().GetResult();
                var serializer = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 };
                var release = serializer.DeserializeObject(json) as Dictionary<string, object>;
                if (release == null) throw new InvalidDataException("GitHub returned an unreadable release response.");

                object rawAssets;
                if (!release.TryGetValue("assets", out rawAssets)) throw new InvalidDataException("The latest GitHub release has no downloadable assets.");
                var assets = rawAssets as object[];
                if (assets == null) throw new InvalidDataException("GitHub returned an unreadable asset list.");

                Version packageVersion = null;
                string setupUrl = null;
                string setupDigest = null;
                foreach (object rawAsset in assets)
                {
                    var asset = rawAsset as Dictionary<string, object>;
                    if (asset == null) continue;
                    string name = ReadString(asset, "name");
                    Version parsed = target.ParsePackageVersion(name);
                    if (parsed != null) packageVersion = parsed;
                    if (String.Equals(name, target.SetupAssetName, StringComparison.OrdinalIgnoreCase))
                    {
                        setupUrl = ReadString(asset, "browser_download_url");
                        setupDigest = ReadString(asset, "digest");
                    }
                }

                if (packageVersion == null) throw new InvalidDataException("The latest release does not contain the expected " + target.ProductName + " package.");
                if (String.IsNullOrWhiteSpace(setupUrl)) throw new InvalidDataException("The latest release does not contain " + target.SetupAssetName + ".");
                return new AvailableUpdate(packageVersion, setupUrl, setupDigest);
            }
        }

        public static string DownloadVerifiedSetup(UpdateTarget target, AvailableUpdate update)
        {
            string expectedHash = NormalizeDigest(update.SetupDigest);
            if (expectedHash == null) throw new InvalidDataException("GitHub did not provide a SHA-256 digest for " + target.SetupAssetName + ".");

            string updaterRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "B2SPro",
                "Updater");
            Directory.CreateDirectory(updaterRoot);
            string destination = Path.Combine(updaterRoot, target.SetupAssetName);
            string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".download";
            try
            {
                using (var client = CreateClient(target.Component))
                {
                    byte[] payload = client.GetByteArrayAsync(update.SetupUrl).GetAwaiter().GetResult();
                    if (payload.Length < 1024 || payload.Length > MaximumSetupBytes) throw new InvalidDataException("The downloaded setup size is invalid.");
                    File.WriteAllBytes(temporary, payload);
                }

                string actualHash = Sha256(temporary);
                if (!String.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The downloaded setup failed SHA-256 verification.");

                if (File.Exists(destination))
                {
                    File.SetAttributes(destination, FileAttributes.Normal);
                    File.Delete(destination);
                }
                File.Move(temporary, destination);
                return destination;
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch { }
            }
        }

        internal static string NormalizeDigest(string digest)
        {
            Match match = Regex.Match(digest ?? String.Empty, "^(?:sha256:)?(?<hash>[A-Fa-f0-9]{64})$", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups["hash"].Value.ToLowerInvariant() : null;
        }

        private static HttpClient CreateClient(UpdateComponent component)
        {
            var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(component == UpdateComponent.Server
                ? "B2S-Server-UpdateChecker/3.0.0"
                : "B2S-Pro-UpdateChecker/1.0.1");
            return client;
        }

        private static string ReadString(Dictionary<string, object> values, string key)
        {
            object value;
            return values.TryGetValue(key, out value) && value != null ? Convert.ToString(value, CultureInfo.InvariantCulture) : null;
        }

        private static string Sha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(stream);
                var text = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash) text.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                return text.ToString();
            }
        }
    }

    internal static class DailyCheckState
    {
        public static bool Begin(UpdateComponent component)
        {
            DateTime now = DateTime.UtcNow;
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "B2SPro",
                "UpdateChecks");
            string statePath = Path.Combine(folder, component + ".txt");
            try
            {
                if (File.Exists(statePath))
                {
                    DateTime last;
                    if (DateTime.TryParse(
                        File.ReadAllText(statePath).Trim(),
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind,
                        out last) &&
                        last.ToUniversalTime() >= now.AddDays(-1) &&
                        last.ToUniversalTime() <= now.AddMinutes(5)) return false;
                }
                Directory.CreateDirectory(folder);
                File.WriteAllText(statePath, now.ToString("O", CultureInfo.InvariantCulture), Encoding.ASCII);
            }
            catch
            {
                // A read-only profile must not prevent a one-time update check.
            }
            return true;
        }
    }

    internal static class SelfTest
    {
        public static int Run()
        {
            try
            {
                UpdateTarget designer = UpdateTarget.Create(UpdateComponent.Designer, Environment.CurrentDirectory);
                UpdateTarget server = UpdateTarget.Create(UpdateComponent.Server, Environment.CurrentDirectory);

                AssertEqual(new Version(1, 0, 1, 0), designer.ParsePackageVersion("B2S-Pro-Backglass-1.0.1.zip"), "Designer package parsing");
                AssertEqual(new Version(3, 0, 0, 0), server.ParsePackageVersion("B2S-Pro-Server-3.0.0.zip"), "Server package parsing");
                Assert(designer.ParsePackageVersion("B2S-Pro-Backglass-Source-1.0.1.zip") == null, "Designer source archive was accepted as a runtime package.");
                Assert(server.ParsePackageVersion("B2S-Pro-Server-3.0.0.zip.sha256") == null, "Server checksum was accepted as a runtime package.");
                AssertEqual(new Version(1, 0, 1, 0), VersionRules.ParseAndNormalize("1.0.1"), "Three-part version normalization");
                Assert(VersionRules.ParseAndNormalize("1.0.2") > VersionRules.ParseAndNormalize("1.0.1.0"), "Newer-version comparison failed.");
                Assert(ComponentSelection.Infer(true, false) == UpdateComponent.Designer, "Designer inference failed.");
                Assert(ComponentSelection.Infer(false, true) == UpdateComponent.Server, "Server inference failed.");
                Assert(ComponentSelection.Infer(true, true) == UpdateComponent.Unknown, "Ambiguous installation inference was not rejected.");
                Assert(ComponentSelection.Parse("SERVER") == UpdateComponent.Server, "Explicit component parsing failed.");
                Assert(GitHubReleaseClient.NormalizeDigest("sha256:" + new string('a', 64)) == new string('a', 64), "GitHub digest parsing failed.");
                Assert(GitHubReleaseClient.NormalizeDigest("sha256:not-a-hash") == null, "Invalid GitHub digest was accepted.");
                Assert(designer.SetupFolderArgument == "--installed-designer", "Designer setup handoff is incorrect.");
                Assert(server.SetupFolderArgument == "--installed-server", "Server setup handoff is incorrect.");
                Console.WriteLine("SELF-TEST PASSED: component selection, package parsing, version comparison, digest validation, and setup handoff");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("SELF-TEST FAILED: " + ex);
                return 1;
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void AssertEqual(Version expected, Version actual, string description)
        {
            if (actual == null || expected != actual)
                throw new InvalidOperationException(description + " failed. Expected " + expected + ", got " + (actual == null ? "null" : actual.ToString()) + ".");
        }
    }
}
