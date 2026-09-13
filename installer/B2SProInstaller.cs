using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using Microsoft.Win32;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

#if SERVER_ONLY
[assembly: AssemblyTitle("B2S Server Setup")]
[assembly: AssemblyProduct("B2S Pro Server")]
[assembly: AssemblyVersion("3.0.0.0")]
[assembly: AssemblyFileVersion("3.0.0.0")]
[assembly: AssemblyInformationalVersion("3.0.0")]
#else
[assembly: AssemblyTitle("B2S Pro Setup")]
[assembly: AssemblyProduct("B2S Pro Backglass Designer")]
[assembly: AssemblyVersion("1.0.1.0")]
[assembly: AssemblyFileVersion("1.0.1.0")]
[assembly: AssemblyInformationalVersion("1.0.1")]
#endif
[assembly: AssemblyCompany("B2S Pro")]
[assembly: AssemblyCopyright("Copyright © 2026 Ken Wildman")]

namespace B2SPro.Setup
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length >= 1 && String.Equals(args[0], "--self-test", StringComparison.OrdinalIgnoreCase))
            {
                Environment.ExitCode = SelfTest.Run(args);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new InstallerForm());
        }
    }

    internal static class SetupEdition
    {
#if SERVER_ONLY
        public static readonly bool ServerOnly = true;
        public const string Title = "B2S Server Setup & Updater";
        public const string Product = "B2S Server";
        public const string ButtonText = "Install / Update Server";
#else
        public static readonly bool ServerOnly = false;
        public const string Title = "B2S Pro Setup & Updater";
        public const string Product = "B2S Pro";
        public const string ButtonText = "Install / Update B2S Pro";
#endif
    }

    internal sealed class InstallerForm : Form
    {
        private const string CompletePackageName = "B2S-Latest-Complete-Build.zip";
        private readonly TextBox _vpxFolder = new TextBox();
        private readonly TextBox _designerFolder = new TextBox();
        private readonly TextBox _serverFolder = new TextBox();
        private readonly ComboBox _architecture = new ComboBox();
        private readonly RadioButton _localSource = new RadioButton();
        private readonly RadioButton _githubSource = new RadioButton();
        private readonly Button _installButton = new Button();
        private readonly ProgressBar _progress = new ProgressBar();
        private readonly Label _status = new Label();
        private readonly string _localPackage;

        public InstallerForm()
        {
            _localPackage = FindLocalPackage();
            Text = SetupEdition.Title;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = SetupEdition.ServerOnly ? new Size(700, 450) : new Size(700, 550);
            Size = SetupEdition.ServerOnly ? new Size(720, 460) : new Size(720, 565);
            MaximizeBox = false;
            BackColor = Color.FromArgb(9, 13, 20);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9F);
            AutoScaleMode = AutoScaleMode.Dpi;
            BuildInterface();
            _vpxFolder.Leave += delegate { if (Directory.Exists(_vpxFolder.Text.Trim())) SetSuggestedFolders(_vpxFolder.Text.Trim()); };
        }

        private void BuildInterface()
        {
            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(16, 12, 16, 12);
            root.ColumnCount = 1;
            root.RowCount = SetupEdition.ServerOnly ? 7 : 9;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, SetupEdition.ServerOnly ? 78 : 82));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, SetupEdition.ServerOnly ? 48 : 56));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            if (!SetupEdition.ServerOnly)
            {
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            }
            else root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            Controls.Add(root);

            root.Controls.Add(CreateHeader(), 0, 0);
            root.Controls.Add(CreateIntro(), 0, 1);
            root.Controls.Add(CreatePathRow("1. Visual Pinball folder", _vpxFolder, BrowseVpx, "Browse..."), 0, 2);
            int nextRow = 3;
            if (!SetupEdition.ServerOnly)
            {
                root.Controls.Add(CreatePathRow("2. B2S Pro Designer folder", _designerFolder, delegate { BrowseFolder(_designerFolder); }, "Browse..."), 0, nextRow++);
            }
            root.Controls.Add(CreatePathRow(SetupEdition.ServerOnly ? "2. B2S Server folder — automatically detected" : "B2S Server folder — automatically detected", _serverFolder, delegate { BrowseFolder(_serverFolder); }, "Change..."), 0, nextRow++);
            _serverFolder.ReadOnly = true;
            if (!SetupEdition.ServerOnly) root.Controls.Add(CreateArchitectureRow(), 0, nextRow++);
            root.Controls.Add(CreateSourcePanel(), 0, nextRow++);

            var statusPanel = new Panel { Dock = DockStyle.Fill };
            _progress.Dock = DockStyle.Top;
            _progress.Height = 8;
            _progress.Style = ProgressBarStyle.Continuous;
            _status.Dock = DockStyle.Fill;
            _status.Padding = new Padding(2, 8, 2, 0);
            _status.ForeColor = Color.FromArgb(140, 220, 255);
            _status.Text = SetupEdition.ServerOnly ? "Choose the Visual Pinball and Server folders, then click Install / Update." : "Choose the three folders, then click Install / Update.";
            statusPanel.Controls.Add(_status);
            statusPanel.Controls.Add(_progress);
            root.Controls.Add(statusPanel, 0, nextRow++);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(0, 2, 0, 0) };
            var close = StyledButton("Close", 104);
            close.Click += delegate { Close(); };
            _installButton.Text = SetupEdition.ButtonText;
            StyleButton(_installButton, SetupEdition.ServerOnly ? 184 : 178);
            _installButton.BackColor = Color.FromArgb(25, 112, 68);
            _installButton.Click += InstallClicked;
            buttons.Controls.Add(close);
            buttons.Controls.Add(_installButton);
            root.Controls.Add(buttons, 0, nextRow);
        }

        private Control CreateHeader()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Black };
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("B2SProHeader.png"))
                {
                    if (stream != null)
                    {
                        var picture = new PictureBox();
                        using (Image source = Image.FromStream(stream)) picture.Image = new Bitmap(source);
                        picture.SizeMode = PictureBoxSizeMode.Zoom;
                        picture.Dock = DockStyle.Fill;
                        panel.Controls.Add(picture);
                    }
                }
            }
            catch { }
            return panel;
        }

        private Control CreateIntro()
        {
            var label = new Label();
            label.Dock = DockStyle.Fill;
            label.Text = SetupEdition.ServerOnly
                ? "Installs or updates only the B2S Server used by Visual Pinball. B2S Pro and the original Backglass Designer are not installed or changed."
                : "B2S Pro installs beside the original Backglass Designer and includes the required B2S Server update. Nothing is written until all locations are validated and every existing program-file replacement is approved.";
            label.Font = new Font(Font, FontStyle.Bold);
            label.ForeColor = Color.FromArgb(230, 235, 245);
            label.Padding = new Padding(2, 8, 2, 2);
            return label;
        }

        private Control CreatePathRow(string title, TextBox textBox, EventHandler browse, string buttonText)
        {
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 98));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 23));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

            var label = new Label { Text = title, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(80, 210, 255), Font = new Font(Font, FontStyle.Bold) };
            textBox.Dock = DockStyle.Fill;
            textBox.BackColor = Color.FromArgb(27, 33, 45);
            textBox.ForeColor = Color.White;
            textBox.BorderStyle = BorderStyle.FixedSingle;
            var button = StyledButton(buttonText, 90);
            button.Dock = DockStyle.Fill;
            button.Click += browse;

            panel.Controls.Add(label, 0, 0);
            panel.SetColumnSpan(label, 2);
            panel.Controls.Add(textBox, 0, 1);
            panel.Controls.Add(button, 1, 1);
            return panel;
        }

        private Control CreateArchitectureRow()
        {
            var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 3, 0, 0), WrapContents = false };
            panel.Controls.Add(new Label { Text = "Designer edition:", Width = 114, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(80, 210, 255), Font = new Font(Font, FontStyle.Bold), Height = 27 });
            _architecture.DropDownStyle = ComboBoxStyle.DropDownList;
            _architecture.Items.AddRange(new object[] { "64-bit (recommended)", "32-bit" });
            _architecture.SelectedIndex = Environment.Is64BitOperatingSystem ? 0 : 1;
            _architecture.Width = 194;
            panel.Controls.Add(_architecture);
            panel.Controls.Add(new Label { Text = "Server supports both x86 and x64.", Width = 250, Height = 27, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.Silver });
            return panel;
        }

        private Control CreateSourcePanel()
        {
            var group = new GroupBox { Text = "Build source", Dock = DockStyle.Fill, ForeColor = Color.White, Padding = new Padding(10, 7, 10, 5) };
            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(4, 1, 0, 0) };
            _localSource.Text = SetupEdition.ServerOnly ? "Install from the verified server package beside this updater" : "Install from the verified complete package beside this updater";
            _localSource.AutoSize = true;
            _localSource.Margin = new Padding(3, 0, 3, 0);
            _localSource.ForeColor = Color.White;
            _localSource.BackColor = Color.Transparent;
            _localSource.UseVisualStyleBackColor = false;
            _localSource.Enabled = File.Exists(_localPackage);
            _githubSource.Text = "Download the latest verified " + SetupEdition.Product + " release from GitHub";
            _githubSource.AutoSize = true;
            _githubSource.Margin = new Padding(3, 0, 3, 0);
            _githubSource.ForeColor = Color.White;
            _githubSource.BackColor = Color.Transparent;
            _githubSource.UseVisualStyleBackColor = false;
            if (_localSource.Enabled) _localSource.Checked = true; else _githubSource.Checked = true;
            flow.Controls.Add(_localSource);
            flow.Controls.Add(_githubSource);
            group.Controls.Add(flow);
            return group;
        }

        private void BrowseVpx(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Locate your Visual Pinball executable";
                dialog.Filter = "Visual Pinball executables (VPinballX*.exe)|VPinballX*.exe|Executable files (*.exe)|*.exe";
                dialog.CheckFileExists = true;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                _vpxFolder.Text = Path.GetDirectoryName(dialog.FileName);
                SetSuggestedFolders(_vpxFolder.Text);
                if (!SetupEdition.ServerOnly)
                {
                    if (PeArchitecture.Is32Bit(dialog.FileName)) _architecture.SelectedIndex = 1;
                    else if (Environment.Is64BitOperatingSystem) _architecture.SelectedIndex = 0;
                }
            }
        }

        private void SetSuggestedFolders(string vpxRoot)
        {
            if (String.IsNullOrWhiteSpace(vpxRoot)) return;
            if (!SetupEdition.ServerOnly && String.IsNullOrWhiteSpace(_designerFolder.Text)) _designerFolder.Text = Path.Combine(vpxRoot, "B2SPro");
            _serverFolder.Text = ServerLocator.FindOrSuggest(vpxRoot);
            if (File.Exists(Path.Combine(_serverFolder.Text, "B2SBackglassServer.dll")))
                _status.Text = "Existing B2S Server detected: " + _serverFolder.Text;
            else
                _status.Text = "Fresh installation: the complete server will be installed in " + _serverFolder.Text;
        }

        private void BrowseFolder(TextBox destination)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Choose the installation folder";
                dialog.ShowNewFolderButton = true;
                if (Directory.Exists(destination.Text)) dialog.SelectedPath = destination.Text;
                if (dialog.ShowDialog(this) == DialogResult.OK) destination.Text = dialog.SelectedPath;
            }
        }

        private async void InstallClicked(object sender, EventArgs e)
        {
            string validation = ValidateInputs();
            if (validation != null)
            {
                MessageBox.Show(this, validation, SetupEdition.Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string packagePath = null;
            string downloadFolder = null;
            try
            {
                SetBusy(true, "Preparing the verified package...");
                if (_localSource.Checked)
                {
                    packagePath = _localPackage;
                    VerifySidecarIfPresent(packagePath);
                }
                else
                {
                    downloadFolder = Path.Combine(Path.GetTempPath(), (SetupEdition.ServerOnly ? "B2SServerSetup-" : "B2SProSetup-") + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(downloadFolder);
                    packagePath = await GitHubRelease.DownloadLatestAsync(downloadFolder, SetupEdition.ServerOnly, ReportDownloadProgress);
                }

                string arch = SetupEdition.ServerOnly ? "x64" : (_architecture.SelectedIndex == 1 ? "x86" : "x64");
                using (var package = new ReleasePackage(packagePath))
                {
                    package.Validate(arch, !SetupEdition.ServerOnly);
                    var plan = package.CreatePlan(SetupEdition.ServerOnly ? null : _designerFolder.Text.Trim(), _serverFolder.Text.Trim(), arch, !SetupEdition.ServerOnly);
                    if (!ConfirmReplacement(plan))
                    {
                        SetBusy(false, "Installation cancelled. No files were changed.");
                        return;
                    }

                    SetBusy(true, "Backing up and installing files...");
                    InstallResult result = await Task.Run(delegate { return plan.Execute(true, !SetupEdition.ServerOnly, !SetupEdition.ServerOnly); });
                    SetBusy(false, SetupEdition.Product + " installation completed successfully.");
                    string message = result.BuildSummary();
                    MessageBox.Show(this, message, SetupEdition.Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Close();
                }
            }
            catch (Exception ex)
            {
                SetBusy(false, "Installation stopped. No unprotected settings were intentionally replaced.");
                MessageBox.Show(this, ex.Message, SetupEdition.Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (downloadFolder != null)
                {
                    try { Directory.Delete(downloadFolder, true); } catch { }
                }
            }
        }

        private string ValidateInputs()
        {
            string vpx = _vpxFolder.Text.Trim();
            if (!Directory.Exists(vpx)) return "Choose an existing Visual Pinball folder.";
            string[] executables = Directory.GetFiles(vpx, "VPinballX*.exe", SearchOption.TopDirectoryOnly);
            if (executables.Length == 0) return "The selected Visual Pinball folder does not contain VPinballX*.exe.";
            if (String.IsNullOrWhiteSpace(_serverFolder.Text)) return "Choose the B2S Server folder.";
            if (!SetupEdition.ServerOnly)
            {
                if (String.IsNullOrWhiteSpace(_designerFolder.Text)) return "Choose a separate B2S Pro Designer folder.";
                if (PathEquals(_designerFolder.Text, _serverFolder.Text)) return "The Designer and Server must use separate folders.";
                if (PathEquals(_designerFolder.Text, vpx) && File.Exists(Path.Combine(vpx, "B2SBackglassDesigner.exe")))
                    return "Choose a separate B2S Pro Designer folder so the original Designer remains untouched.";
            }
            if (_localSource.Checked && !File.Exists(_localPackage)) return "The local verified ZIP is no longer beside the installer.";
            return null;
        }

        private bool ConfirmReplacement(InstallPlan plan)
        {
            if (plan.ExistingProgramFiles.Count == 0) return true;
            var message = new StringBuilder();
            message.AppendLine("An existing B2S installation was found.");
            message.AppendLine();
            foreach (string file in plan.ExistingProgramFiles) message.AppendLine("• " + file);
            message.AppendLine();
            message.AppendLine(SetupEdition.ServerOnly
                ? "Do you want to back up and overwrite these B2S Server program files?"
                : "The B2S Server update is included. Do you want to back up and overwrite these program files?");
            message.AppendLine();
            message.AppendLine("ScreenRes.txt, B2STableSettings.xml, plug-ins, projects, tables, and backglasses will not be overwritten.");
            return MessageBox.Show(this, message.ToString(), "Existing installation found", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        private static string FindLocalPackage()
        {
            string folder = AppDomain.CurrentDomain.BaseDirectory;
            if (!SetupEdition.ServerOnly) return Path.Combine(folder, CompletePackageName);
            string stable = Path.Combine(folder, "B2S-Latest-Server.zip");
            if (File.Exists(stable)) return stable;
            string[] matches = Directory.GetFiles(folder, "B2S-Pro-Server-*.zip", SearchOption.TopDirectoryOnly);
            Array.Sort(matches, StringComparer.OrdinalIgnoreCase);
            for (int index = matches.Length - 1; index >= 0; index--)
            {
                if (matches[index].IndexOf("-Source-", StringComparison.OrdinalIgnoreCase) < 0) return matches[index];
            }
            return stable;
        }

        private void ReportDownloadProgress(int percent, string text)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<int, string>(ReportDownloadProgress), percent, text);
                return;
            }
            _progress.Style = ProgressBarStyle.Continuous;
            _progress.Value = Math.Max(0, Math.Min(100, percent));
            _status.Text = text;
        }

        private void SetBusy(bool busy, string text)
        {
            _installButton.Enabled = !busy;
            _status.Text = text;
            if (!busy)
            {
                _progress.Style = ProgressBarStyle.Continuous;
                _progress.Value = 0;
            }
            else if (_progress.Value == 0) _progress.Style = ProgressBarStyle.Marquee;
            UseWaitCursor = busy;
        }

        private static void VerifySidecarIfPresent(string zipPath)
        {
            string sidecar = zipPath + ".sha256";
            if (!File.Exists(sidecar)) throw new InvalidDataException("The SHA-256 verification file is missing. Nothing was installed.");
            string expected = Regex.Match(File.ReadAllText(sidecar), "[A-Fa-f0-9]{64}").Value;
            if (expected.Length != 64) throw new InvalidDataException("The local SHA-256 file is invalid.");
            string actual = Hashing.Sha256(zipPath);
            if (!String.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The local package failed SHA-256 verification. Nothing was installed.");
        }

        private static bool PathEquals(string left, string right)
        {
            if (String.IsNullOrWhiteSpace(left) || String.IsNullOrWhiteSpace(right)) return false;
            return String.Equals(Path.GetFullPath(left.Trim()).TrimEnd('\\'), Path.GetFullPath(right.Trim()).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
        }

        private static Button StyledButton(string text, int width)
        {
            var button = new Button { Text = text };
            StyleButton(button, width);
            return button;
        }

        private static void StyleButton(Button button, int width)
        {
            button.Width = width;
            button.Height = 30;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(55, 145, 255);
            button.BackColor = Color.FromArgb(28, 48, 82);
            button.ForeColor = Color.White;
        }
    }

    internal static class GitHubRelease
    {
        private const string LatestApi = "https://api.github.com/repos/KWildman69/B2S-Pro/releases/latest";
        private const string CompletePackageName = "B2S-Latest-Complete-Build.zip";

        public static async Task<string> DownloadLatestAsync(string destination, bool serverOnly, Action<int, string> progress)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd(serverOnly ? "B2S-Server-Updater/3.0.0" : "B2S-Pro-Installer/1.0.1");
                string json;
                try
                {
                    json = await client.GetStringAsync(LatestApi);
                }
                catch (HttpRequestException ex)
                {
                    throw new InvalidOperationException("The latest GitHub release could not be reached. While the repository is private or the cabinet is offline, use the verified local ZIP beside this setup program.\r\n\r\n" + ex.Message);
                }

                var serializer = new JavaScriptSerializer();
                var release = serializer.DeserializeObject(json) as Dictionary<string, object>;
                if (release == null) throw new InvalidDataException("GitHub returned an unreadable release response.");
                string tag = release.ContainsKey("tag_name") ? Convert.ToString(release["tag_name"]) : "latest";
                string packageName = serverOnly ? FindServerPackageName(release) : CompletePackageName;
                string hashName = packageName == null ? null : packageName + ".sha256";
                string packageUrl = packageName == null ? null : FindAsset(release, packageName);
                string hashUrl = hashName == null ? null : FindAsset(release, hashName);
                if (packageUrl == null || hashUrl == null) throw new InvalidDataException("The latest release does not contain the required " + (serverOnly ? "server" : "complete-build") + " ZIP and SHA-256 file.");

                string zipPath = Path.Combine(destination, packageName);
                string hashPath = Path.Combine(destination, hashName);
                await DownloadFile(client, packageUrl, zipPath, tag, serverOnly ? "B2S Server" : "B2S Pro", progress);
                File.WriteAllBytes(hashPath, await client.GetByteArrayAsync(hashUrl));
                string expected = Regex.Match(File.ReadAllText(hashPath), "[A-Fa-f0-9]{64}").Value;
                string actual = Hashing.Sha256(zipPath);
                if (expected.Length != 64 || !String.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The GitHub package failed SHA-256 verification. Nothing was installed.");
                progress(100, "Downloaded and verified " + tag + ".");
                return zipPath;
            }
        }

        private static string FindServerPackageName(Dictionary<string, object> release)
        {
            object rawAssets;
            if (!release.TryGetValue("assets", out rawAssets)) return null;
            var assets = rawAssets as object[];
            if (assets == null) return null;
            foreach (object raw in assets)
            {
                var asset = raw as Dictionary<string, object>;
                if (asset == null) continue;
                string name = Convert.ToString(asset["name"]);
                if (Regex.IsMatch(name, "^B2S-Pro-Server-[0-9][A-Za-z0-9._-]*\\.zip$", RegexOptions.IgnoreCase)
                    && name.IndexOf("-Source-", StringComparison.OrdinalIgnoreCase) < 0) return name;
            }
            return null;
        }

        private static string FindAsset(Dictionary<string, object> release, string name)
        {
            object rawAssets;
            if (!release.TryGetValue("assets", out rawAssets)) return null;
            var assets = rawAssets as object[];
            if (assets == null) return null;
            foreach (object raw in assets)
            {
                var asset = raw as Dictionary<string, object>;
                if (asset == null) continue;
                if (String.Equals(Convert.ToString(asset["name"]), name, StringComparison.OrdinalIgnoreCase))
                    return Convert.ToString(asset["browser_download_url"]);
            }
            return null;
        }

        private static async Task DownloadFile(HttpClient client, string url, string destination, string tag, string product, Action<int, string> progress)
        {
            using (HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                long total = response.Content.Headers.ContentLength.GetValueOrDefault(-1L);
                using (Stream input = await response.Content.ReadAsStreamAsync())
                using (var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    byte[] buffer = new byte[1024 * 128];
                    long readTotal = 0;
                    int read;
                    while ((read = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await output.WriteAsync(buffer, 0, read);
                        readTotal += read;
                        int percent = total > 0 ? (int)(readTotal * 100L / total) : 0;
                        progress(percent, "Downloading " + product + " " + tag + "... " + percent + "%");
                    }
                }
            }
        }
    }

    internal sealed class ReleasePackage : IDisposable
    {
        private readonly ZipArchive _archive;
        public ReleasePackage(string path)
        {
            _archive = ZipFile.OpenRead(path);
        }

        public void Validate(string arch, bool installDesigner)
        {
            if (installDesigner)
            {
                Require("Runtime/" + arch + "/B2SPro.exe");
                Require("Runtime/" + arch + "/B2SPro.exe.config");
                Require("Runtime/" + arch + "/B2SVPinMAMEStarter.exe");
            }
            string serverPrefix = GetServerPrefix();
            Require(serverPrefix + "B2SBackglassServer.dll");
            Require(serverPrefix + "B2SBackglassServerRegisterApp.exe");
        }

        public InstallPlan CreatePlan(string designer, string server, string arch, bool installDesigner)
        {
            return new InstallPlan(_archive, designer, server, arch, installDesigner, GetServerPrefix());
        }

        private string GetServerPrefix()
        {
            return _archive.GetEntry("Runtime/B2SServer/B2SBackglassServer.dll") != null ? "Runtime/B2SServer/" : String.Empty;
        }

        private void Require(string name)
        {
            if (_archive.GetEntry(name) == null) throw new InvalidDataException("The package is missing " + name + ". Nothing was installed.");
        }

        public void Dispose() { _archive.Dispose(); }
    }

    internal sealed class InstallPlan
    {
        private readonly ZipArchive _archive;
        private readonly string _designer;
        private readonly string _server;
        private readonly string _arch;
        private readonly bool _installDesigner;
        private readonly string _serverPrefix;
        private readonly bool _freshServer;
        private readonly List<CopyItem> _items = new List<CopyItem>();
        public readonly List<string> ExistingProgramFiles = new List<string>();

        public InstallPlan(ZipArchive archive, string designer, string server, string arch, bool installDesigner, string serverPrefix)
        {
            _archive = archive;
            _installDesigner = installDesigner;
            _designer = installDesigner ? Path.GetFullPath(designer) : null;
            _server = Path.GetFullPath(server);
            _arch = arch;
            _serverPrefix = serverPrefix ?? String.Empty;
            _freshServer = !File.Exists(Path.Combine(_server, "B2SBackglassServer.dll"));
            BuildItems();
        }

        private void BuildItems()
        {
            string designerPrefix = "Runtime/" + _arch + "/";
            foreach (ZipArchiveEntry entry in _archive.Entries)
            {
                if (String.IsNullOrEmpty(entry.Name)) continue;
                if (_installDesigner && entry.FullName.StartsWith(designerPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    string relative = entry.FullName.Substring(designerPrefix.Length).Replace('/', Path.DirectorySeparatorChar);
                    Add(entry, Path.Combine(_designer, relative), false, true);
                }
                else if (entry.FullName.StartsWith(_serverPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    string relative = entry.FullName.Substring(_serverPrefix.Length).Replace('/', Path.DirectorySeparatorChar);
                    bool protect = IsProtectedServerPath(relative);
                    Add(entry, Path.Combine(_server, relative), protect, false);
                }
            }
        }

        private void Add(ZipArchiveEntry entry, string destination, bool protect, bool isDesigner)
        {
            var item = new CopyItem(entry, destination, protect, isDesigner);
            _items.Add(item);
            if (File.Exists(destination) && !protect) ExistingProgramFiles.Add(destination);
        }

        private static bool IsProtectedServerPath(string relative)
        {
            string normalized = relative.Replace('/', '\\');
            string name = Path.GetFileName(normalized);
            if (String.Equals(name, "ScreenRes.txt", StringComparison.OrdinalIgnoreCase)) return true;
            if (String.Equals(name, "B2STableSettings.xml", StringComparison.OrdinalIgnoreCase)) return true;
            if (normalized.StartsWith("Plugins\\", StringComparison.OrdinalIgnoreCase)) return true;
            if (normalized.StartsWith("Plugins64\\", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public InstallResult Execute(bool registerServer, bool createShortcuts, bool registerFileAssociations)
        {
            if (_installDesigner) Directory.CreateDirectory(_designer);
            Directory.CreateDirectory(_server);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string designerBackup = _installDesigner ? Path.Combine(_designer, "B2SPro-Backups", stamp) : null;
            string serverBackup = Path.Combine(_server, "B2SPro-Backups", stamp);
            var changed = new List<RollbackItem>();
            int installed = 0;
            int preserved = 0;
            try
            {
                foreach (CopyItem item in _items)
                {
                    if (item.Protected && File.Exists(item.Destination))
                    {
                        preserved++;
                        continue;
                    }

                    string root = item.IsDesigner ? _designer : _server;
                    string backupRoot = item.IsDesigner ? designerBackup : serverBackup;
                    string relative = item.Destination.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar);
                    bool existed = File.Exists(item.Destination);
                    if (existed)
                    {
                        string backup = Path.Combine(backupRoot, relative);
                        Directory.CreateDirectory(Path.GetDirectoryName(backup));
                        File.Copy(item.Destination, backup, true);
                        changed.Add(new RollbackItem(item.Destination, backup, true));
                    }
                    else changed.Add(new RollbackItem(item.Destination, null, false));

                    Directory.CreateDirectory(Path.GetDirectoryName(item.Destination));
                    string temporary = item.Destination + ".b2spro-new";
                    using (Stream input = item.Entry.Open())
                    using (var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None)) input.CopyTo(output);
                    if (File.Exists(item.Destination)) File.Delete(item.Destination);
                    File.Move(temporary, item.Destination);
                    ApplyVisibility(item.Destination, item.IsDesigner);
                    installed++;
                }

                string registrationError = null;
                if (registerServer)
                {
                    try { RegisterServer(); }
                    catch (Exception ex) { registrationError = ex.Message; }
                }

                string fileAssociationError = null;
                if (registerFileAssociations)
                {
                    try { FileAssociationManager.Register(_designer); }
                    catch (Exception ex) { fileAssociationError = ex.Message; }
                }

                string shortcutError = null;
                if (createShortcuts)
                {
                    try { ShortcutManager.Create(_designer); }
                    catch (Exception ex) { shortcutError = ex.Message; }
                }

                string log = Path.Combine(_installDesigner ? _designer : _server, _installDesigner ? "B2SPro-Install.log" : "B2SServer-Install.log");
                File.AppendAllText(log, DateTime.Now.ToString("s") + " Installed " + installed + " files; preserved " + preserved + " protected files; architecture " + _arch + "; registration " + (registerServer ? (registrationError == null ? "successful" : "failed: " + registrationError) : "skipped for test") + "; file associations " + (registerFileAssociations ? (fileAssociationError == null ? "successful" : "failed: " + fileAssociationError) : "skipped") + "; shortcuts " + (createShortcuts ? (shortcutError == null ? "successful" : "failed: " + shortcutError) : "skipped for test") + Environment.NewLine);
                MarkProtected(log);
                if (_installDesigner) MarkProtected(Path.Combine(_designer, "B2SPro-Backups"));
                MarkProtected(Path.Combine(_server, "B2SPro-Backups"));
                bool backupCreated = (_installDesigner && Directory.Exists(designerBackup)) || Directory.Exists(serverBackup);
                return new InstallResult(_designer, _server, installed, preserved, backupCreated ? stamp : null, _freshServer, registerServer, registrationError, registerFileAssociations, fileAssociationError, createShortcuts, shortcutError, _installDesigner);
            }
            catch
            {
                for (int index = changed.Count - 1; index >= 0; index--)
                {
                    try
                    {
                        RollbackItem item = changed[index];
                        if (item.Existed) File.Copy(item.Backup, item.Destination, true);
                        else if (File.Exists(item.Destination)) File.Delete(item.Destination);
                    }
                    catch { }
                }
                throw;
            }
        }

        private void RegisterServer()
        {
            string registerApp = Path.Combine(_server, "B2SBackglassServerRegisterApp.exe");
            if (!File.Exists(registerApp)) throw new FileNotFoundException("The server registration application was not installed.", registerApp);
            var start = new ProcessStartInfo();
            start.FileName = registerApp;
            start.Arguments = "silent";
            start.WorkingDirectory = _server;
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            using (Process process = Process.Start(start))
            {
                if (process == null) throw new InvalidOperationException("The server registration application could not be started.");
                if (!process.WaitForExit(120000)) throw new TimeoutException("Server registration did not finish within two minutes.");
                if (process.ExitCode != 0) throw new InvalidOperationException("Server registration returned exit code " + process.ExitCode + ".");
            }
        }

        private static void ApplyVisibility(string path, bool isDesigner)
        {
            string name = Path.GetFileName(path);
            bool hidden;
            if (isDesigner)
            {
                hidden = !String.Equals(name, "B2SPro.exe", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                hidden = name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith(".config", StringComparison.OrdinalIgnoreCase)
                    || String.Equals(name, "B2SBackglassServerEXE.exe", StringComparison.OrdinalIgnoreCase)
                    || String.Equals(name, "B2SInit.cmd", StringComparison.OrdinalIgnoreCase)
                    || String.Equals(name, "B2SWindowPunch.exe", StringComparison.OrdinalIgnoreCase);
            }

            FileAttributes attributes = File.GetAttributes(path);
            if (hidden) File.SetAttributes(path, attributes | FileAttributes.Hidden | FileAttributes.System);
            else File.SetAttributes(path, attributes & ~FileAttributes.Hidden & ~FileAttributes.System);
        }

        private static void MarkProtected(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path)) return;
            FileAttributes attributes = File.GetAttributes(path);
            File.SetAttributes(path, attributes | FileAttributes.Hidden | FileAttributes.System);
        }
    }

    internal sealed class CopyItem
    {
        public readonly ZipArchiveEntry Entry;
        public readonly string Destination;
        public readonly bool Protected;
        public readonly bool IsDesigner;
        public CopyItem(ZipArchiveEntry entry, string destination, bool protect, bool isDesigner)
        {
            Entry = entry;
            Destination = destination;
            Protected = protect;
            IsDesigner = isDesigner;
        }
    }

    internal sealed class RollbackItem
    {
        public readonly string Destination;
        public readonly string Backup;
        public readonly bool Existed;
        public RollbackItem(string destination, string backup, bool existed)
        {
            Destination = destination;
            Backup = backup;
            Existed = existed;
        }
    }

    internal sealed class InstallResult
    {
        private readonly string _designer;
        private readonly string _server;
        private readonly int _installed;
        private readonly int _preserved;
        private readonly string _backupStamp;
        private readonly bool _freshServer;
        private readonly bool _registrationAttempted;
        private readonly string _registrationError;
        private readonly bool _fileAssociationsAttempted;
        private readonly string _fileAssociationError;
        private readonly bool _shortcutsAttempted;
        private readonly string _shortcutError;
        private readonly bool _installedDesigner;
        public InstallResult(string designer, string server, int installed, int preserved, string backupStamp, bool freshServer, bool registrationAttempted, string registrationError, bool fileAssociationsAttempted, string fileAssociationError, bool shortcutsAttempted, string shortcutError, bool installedDesigner)
        {
            _designer = designer;
            _server = server;
            _installed = installed;
            _preserved = preserved;
            _backupStamp = backupStamp;
            _freshServer = freshServer;
            _registrationAttempted = registrationAttempted;
            _registrationError = registrationError;
            _fileAssociationsAttempted = fileAssociationsAttempted;
            _fileAssociationError = fileAssociationError;
            _shortcutsAttempted = shortcutsAttempted;
            _shortcutError = shortcutError;
            _installedDesigner = installedDesigner;
        }

        public string BuildSummary()
        {
            var text = new StringBuilder();
            text.AppendLine((_installedDesigner ? "B2S Pro and B2S Server were" : "B2S Server was") + " installed successfully.");
            text.AppendLine();
            if (_installedDesigner) text.AppendLine("Designer: " + _designer);
            text.AppendLine("Server: " + _server);
            text.AppendLine("Server installation: " + (_freshServer ? "Fresh installation" : "Existing installation updated"));
            text.AppendLine("Files installed: " + _installed);
            text.AppendLine("Protected existing files preserved: " + _preserved);
            if (_backupStamp != null) text.AppendLine("Backup: B2SPro-Backups\\" + _backupStamp);
            text.AppendLine();
            text.AppendLine(_installedDesigner ? "The original Backglass Designer was not changed." : "No B2S Designer files were installed or changed.");
            text.AppendLine();
            if (_registrationAttempted && _registrationError == null)
            {
                text.AppendLine("The B2S Server DLL and context-menu support were registered automatically.");
            }
            else if (_registrationAttempted)
            {
                text.AppendLine("WARNING: Automatic server registration did not finish:");
                text.AppendLine(_registrationError);
                text.AppendLine("The installed files were kept so registration can be retried safely.");
            }
            else text.AppendLine("Server registration was skipped for this sandbox test.");
            if (_fileAssociationsAttempted && _fileAssociationError == null)
            {
                text.AppendLine(".B2SPro and .directB2S files were associated with the B2S Pro editor.");
            }
            else if (_fileAssociationsAttempted)
            {
                text.AppendLine("WARNING: Backglass file associations could not be registered: " + _fileAssociationError);
            }
            if (_shortcutsAttempted && _shortcutError == null)
            {
                text.AppendLine("Desktop and Start Menu shortcuts were created for B2S Pro.");
            }
            else if (_shortcutsAttempted)
            {
                text.AppendLine("WARNING: The shortcuts could not be created: " + _shortcutError);
            }
            return text.ToString();
        }
    }

    internal static class FileAssociationManager
    {
        private const string B2SProProgId = "B2SPro.Backglass";
        private const string LegacyProgId = "B2SPro.LegacyDirectB2S";
        private const uint AssociationChanged = 0x08000000;
        private const uint IdList = 0x0000;

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);

        public static void Register(string designerFolder)
        {
            string editor = GetEditorPath(designerFolder);
            if (!File.Exists(editor)) throw new FileNotFoundException("B2SPro.exe was not found for file association registration.", editor);

            using (RegistryKey extension = Registry.ClassesRoot.CreateSubKey(".B2SPro"))
                extension.SetValue("", B2SProProgId);
            using (RegistryKey legacyExtension = Registry.ClassesRoot.CreateSubKey(".directb2s"))
                legacyExtension.SetValue("", LegacyProgId);

            RegisterFileType(B2SProProgId, ".B2SPro", editor, designerFolder);
            RegisterFileType(LegacyProgId, ".directB2S", editor, designerFolder);
            SHChangeNotify(AssociationChanged, IdList, IntPtr.Zero, IntPtr.Zero);
        }

        private static void RegisterFileType(string progId, string description, string editor, string designerFolder)
        {
            using (RegistryKey fileType = Registry.ClassesRoot.CreateSubKey(progId))
            {
                fileType.SetValue("", description);
                fileType.SetValue("FriendlyTypeName", description);
                using (RegistryKey icon = fileType.CreateSubKey("DefaultIcon"))
                    icon.SetValue("", Quote(editor) + ",0");
                using (RegistryKey command = fileType.CreateSubKey("shell\\open\\command"))
                    command.SetValue("", BuildOpenCommand(designerFolder));
            }
        }

        internal static string GetProgId(string extension)
        {
            return String.Equals(extension, ".B2SPro", StringComparison.OrdinalIgnoreCase) ? B2SProProgId : LegacyProgId;
        }

        internal static string GetTypeDescription(string extension)
        {
            return String.Equals(extension, ".B2SPro", StringComparison.OrdinalIgnoreCase)
                ? ".B2SPro"
                : ".directB2S";
        }

        internal static string GetEditorPath(string designerFolder)
        {
            return Path.Combine(Path.GetFullPath(designerFolder), "B2SPro.exe");
        }

        internal static string BuildOpenCommand(string designerFolder)
        {
            return Quote(GetEditorPath(designerFolder)) + " \"%1\"";
        }

        private static string Quote(string value)
        {
            return "\"" + value + "\"";
        }
    }

    internal static class ShortcutManager
    {
        public static void Create(string designerFolder)
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            if (String.IsNullOrWhiteSpace(desktop) || String.IsNullOrWhiteSpace(programs))
                throw new InvalidOperationException("Windows did not provide the current user's shortcut folders.");
            CreateAt(designerFolder, desktop, programs);
        }

        public static void CreateAt(string designerFolder, string desktop, string programs)
        {
            string target = Path.Combine(designerFolder, "B2SPro.exe");
            if (!File.Exists(target)) throw new FileNotFoundException("B2SPro.exe was not found for shortcut creation.", target);

            Directory.CreateDirectory(desktop);
            string startMenuFolder = Path.Combine(programs, "B2S Pro");
            Directory.CreateDirectory(startMenuFolder);
            CreateShortcut(Path.Combine(desktop, "B2S Pro.lnk"), target, designerFolder);
            CreateShortcut(Path.Combine(startMenuFolder, "B2S Pro.lnk"), target, designerFolder);
        }

        private static void CreateShortcut(string shortcutPath, string target, string workingDirectory)
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) throw new InvalidOperationException("Windows Script Host is unavailable for shortcut creation.");
            object shell = Activator.CreateInstance(shellType);
            try
            {
                object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
                try
                {
                    Type shortcutType = shortcut.GetType();
                    shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { target });
                    shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workingDirectory });
                    shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "B2S Pro Backglass Designer" });
                    shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { target + ",0" });
                    shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
                }
                finally
                {
                    if (shortcut != null && System.Runtime.InteropServices.Marshal.IsComObject(shortcut))
                        System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
                }
            }
            finally
            {
                if (shell != null && System.Runtime.InteropServices.Marshal.IsComObject(shell))
                    System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
            }
        }
    }

    internal static class Hashing
    {
        public static string Sha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(stream);
                var text = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash) text.Append(value.ToString("x2"));
                return text.ToString();
            }
        }
    }

    internal static class PeArchitecture
    {
        public static bool Is32Bit(string path)
        {
            try
            {
                using (var stream = File.OpenRead(path))
                using (var reader = new BinaryReader(stream))
                {
                    stream.Position = 0x3c;
                    int peOffset = reader.ReadInt32();
                    stream.Position = peOffset + 4;
                    ushort machine = reader.ReadUInt16();
                    return machine == 0x014c;
                }
            }
            catch { return false; }
        }
    }

    internal static class ServerLocator
    {
        public static string FindOrSuggest(string vpxRoot)
        {
            string[] candidates =
            {
                Path.Combine(vpxRoot, "B2SServer"),
                Path.Combine(vpxRoot, "Tables"),
                vpxRoot
            };
            foreach (string candidate in candidates)
            {
                if (File.Exists(Path.Combine(candidate, "B2SBackglassServer.dll"))) return candidate;
            }
            return Path.Combine(vpxRoot, "B2SServer");
        }
    }

    internal static class SelfTest
    {
        public static int Run(string[] args)
        {
            try
            {
                if (args.Length < 2 || !File.Exists(args[1])) throw new ArgumentException("Usage: B2SSetup.exe --self-test <complete-build.zip> [server-build.zip]");
                string sandbox = Path.Combine(Path.GetTempPath(), "B2SProInstallerSelfTest-" + Guid.NewGuid().ToString("N"));
                string originalDesigner = Path.Combine(sandbox, "OriginalDesigner");
                Directory.CreateDirectory(originalDesigner);
                string originalDesignerExe = Path.Combine(originalDesigner, "B2SBackglassDesigner.exe");
                File.WriteAllText(originalDesignerExe, "ORIGINAL-DESIGNER-MUST-STAY");

                string vpxLayout = Path.Combine(sandbox, "VisualPinball");
                string detectedServer = Path.Combine(vpxLayout, "B2SServer");
                Directory.CreateDirectory(detectedServer);
                File.WriteAllText(Path.Combine(detectedServer, "B2SBackglassServer.dll"), "LOCATOR-TEST");
                if (!String.Equals(ServerLocator.FindOrSuggest(vpxLayout), detectedServer, StringComparison.OrdinalIgnoreCase))
                    throw new Exception("The standard B2SServer folder was not auto-detected.");

                foreach (string arch in new[] { "x64", "x86" })
                {
                    string designer = Path.Combine(sandbox, "B2SPro-" + arch);
                    string server = Path.Combine(sandbox, "B2SServer-" + arch);
                    Directory.CreateDirectory(designer);
                    Directory.CreateDirectory(server);
                    File.WriteAllText(Path.Combine(server, "ScreenRes.txt"), "SELF-TEST-SCREENRES");
                    Directory.CreateDirectory(Path.Combine(server, "Plugins"));
                    File.WriteAllText(Path.Combine(server, "Plugins", "Plugins.txt"), "SELF-TEST-PLUGIN");
                    File.WriteAllText(Path.Combine(server, "B2SBackglassServer.dll"), "OLD-SERVER");
                    File.WriteAllText(Path.Combine(designer, "B2SPro.exe"), "OLD-DESIGNER");

                    using (var package = new ReleasePackage(args[1]))
                    {
                        package.Validate(arch, true);
                        InstallPlan plan = package.CreatePlan(designer, server, arch, true);
                        if (plan.ExistingProgramFiles.Count < 2) throw new Exception(arch + " existing-program detection failed.");
                        plan.Execute(false, false, false);
                        if (File.ReadAllText(Path.Combine(server, "ScreenRes.txt")) != "SELF-TEST-SCREENRES") throw new Exception(arch + " ScreenRes.txt was overwritten.");
                        if (File.ReadAllText(Path.Combine(server, "Plugins", "Plugins.txt")) != "SELF-TEST-PLUGIN") throw new Exception(arch + " plugin settings were overwritten.");
                        if (new FileInfo(Path.Combine(designer, "B2SPro.exe")).Length < 1000000) throw new Exception(arch + " Designer payload was not installed.");
                        if (new FileInfo(Path.Combine(server, "B2SBackglassServer.dll")).Length < 100000) throw new Exception(arch + " Server payload was not installed.");
                        if (Directory.GetFiles(Path.Combine(designer, "B2SPro-Backups"), "B2SPro.exe", SearchOption.AllDirectories).Length != 1) throw new Exception(arch + " Designer backup was not created.");
                        if (Directory.GetFiles(Path.Combine(server, "B2SPro-Backups"), "B2SBackglassServer.dll", SearchOption.AllDirectories).Length != 1) throw new Exception(arch + " Server backup was not created.");
                        if ((File.GetAttributes(Path.Combine(designer, "B2SPro.exe")) & FileAttributes.Hidden) != 0) throw new Exception(arch + " main Designer executable was hidden.");
                        if ((File.GetAttributes(Path.Combine(designer, "B2SPro.exe.config")) & FileAttributes.Hidden) == 0) throw new Exception(arch + " Designer config was not hidden.");
                        if ((File.GetAttributes(Path.Combine(designer, "B2SPro.exe.config")) & FileAttributes.System) == 0) throw new Exception(arch + " Designer config was not marked as a protected system file.");
                        if ((File.GetAttributes(Path.Combine(server, "B2SBackglassServer.dll")) & FileAttributes.Hidden) == 0) throw new Exception(arch + " Server DLL was not hidden.");
                        if ((File.GetAttributes(Path.Combine(server, "B2SBackglassServer.dll")) & FileAttributes.System) == 0) throw new Exception(arch + " Server DLL was not marked as a protected system file.");
                        if ((File.GetAttributes(Path.Combine(server, "ScreenRes.txt")) & FileAttributes.Hidden) != 0) throw new Exception(arch + " ScreenRes.txt was hidden.");
                        if ((File.GetAttributes(Path.Combine(designer, "B2SPro-Install.log")) & (FileAttributes.Hidden | FileAttributes.System)) != (FileAttributes.Hidden | FileAttributes.System)) throw new Exception(arch + " install log was not protected.");
                        if ((File.GetAttributes(Path.Combine(designer, "B2SPro-Backups")) & (FileAttributes.Hidden | FileAttributes.System)) != (FileAttributes.Hidden | FileAttributes.System)) throw new Exception(arch + " Designer backup folder was not protected.");
                    }
                }

                if (File.ReadAllText(originalDesignerExe) != "ORIGINAL-DESIGNER-MUST-STAY") throw new Exception("The original Backglass Designer was changed.");

                string freshVpx = Path.Combine(sandbox, "FreshVisualPinball");
                Directory.CreateDirectory(freshVpx);
                File.WriteAllText(Path.Combine(freshVpx, "VPinballX64.exe"), "VPX-LOCATION-TEST");
                string freshServer = ServerLocator.FindOrSuggest(freshVpx);
                string expectedFreshServer = Path.Combine(freshVpx, "B2SServer");
                if (!String.Equals(freshServer, expectedFreshServer, StringComparison.OrdinalIgnoreCase))
                    throw new Exception("The fresh server location was not suggested correctly.");
                using (var package = new ReleasePackage(args[1]))
                {
                    package.Validate("x64", true);
                    package.CreatePlan(Path.Combine(freshVpx, "B2SPro"), freshServer, "x64", true).Execute(false, false, false);
                }
                if (!File.Exists(Path.Combine(freshVpx, "B2SPro", "B2SPro.exe"))) throw new Exception("The fresh Designer was not installed in its separate folder.");
                if (!File.Exists(Path.Combine(freshServer, "B2SBackglassServer.dll"))) throw new Exception("The fresh Server was not installed in the suggested B2SServer folder.");
                string expectedEditor = Path.Combine(freshVpx, "B2SPro", "B2SPro.exe");
                string expectedOpenCommand = "\"" + expectedEditor + "\" \"%1\"";
                if (!String.Equals(FileAssociationManager.GetEditorPath(Path.Combine(freshVpx, "B2SPro")), expectedEditor, StringComparison.OrdinalIgnoreCase))
                    throw new Exception("The B2S Pro file association did not target the installed Designer.");
                if (!String.Equals(FileAssociationManager.BuildOpenCommand(Path.Combine(freshVpx, "B2SPro")), expectedOpenCommand, StringComparison.Ordinal))
                    throw new Exception("The B2S Pro file association command was not quoted correctly.");
                if (String.Equals(FileAssociationManager.GetProgId(".B2SPro"), FileAssociationManager.GetProgId(".directb2s"), StringComparison.OrdinalIgnoreCase))
                    throw new Exception("B2S Pro and legacy directB2S files were assigned the same Windows file type.");
                if (!String.Equals(FileAssociationManager.GetTypeDescription(".B2SPro"), ".B2SPro", StringComparison.Ordinal))
                    throw new Exception("The B2S Pro Windows file-type description is incorrect.");
                if (!String.Equals(FileAssociationManager.GetTypeDescription(".directb2s"), ".directB2S", StringComparison.Ordinal))
                    throw new Exception("The legacy directB2S Windows file-type description is incorrect.");
                string testDesktop = Path.Combine(sandbox, "TestDesktop");
                string testPrograms = Path.Combine(sandbox, "TestPrograms");
                ShortcutManager.CreateAt(Path.Combine(freshVpx, "B2SPro"), testDesktop, testPrograms);
                if (!File.Exists(Path.Combine(testDesktop, "B2S Pro.lnk"))) throw new Exception("The desktop shortcut was not created.");
                if (!File.Exists(Path.Combine(testPrograms, "B2S Pro", "B2S Pro.lnk"))) throw new Exception("The Start Menu shortcut was not created.");

                if (args.Length >= 3)
                {
                    if (!File.Exists(args[2])) throw new ArgumentException("The server-only package does not exist: " + args[2]);
                    string serverOnlyRoot = Path.Combine(sandbox, "ServerOnly");
                    Directory.CreateDirectory(serverOnlyRoot);
                    File.WriteAllText(Path.Combine(serverOnlyRoot, "ScreenRes.txt"), "SERVER-ONLY-SCREENRES");
                    Directory.CreateDirectory(Path.Combine(serverOnlyRoot, "Plugins"));
                    File.WriteAllText(Path.Combine(serverOnlyRoot, "Plugins", "Plugins.txt"), "SERVER-ONLY-PLUGIN");
                    File.WriteAllText(Path.Combine(serverOnlyRoot, "B2SBackglassServer.dll"), "OLD-SERVER");
                    using (var package = new ReleasePackage(args[2]))
                    {
                        package.Validate("x64", false);
                        InstallPlan plan = package.CreatePlan(null, serverOnlyRoot, "x64", false);
                        if (plan.ExistingProgramFiles.Count == 0) throw new Exception("Server-only existing-program detection failed.");
                        plan.Execute(false, false, false);
                    }
                    if (File.ReadAllText(Path.Combine(serverOnlyRoot, "ScreenRes.txt")) != "SERVER-ONLY-SCREENRES") throw new Exception("Server-only setup overwrote ScreenRes.txt.");
                    if (File.ReadAllText(Path.Combine(serverOnlyRoot, "Plugins", "Plugins.txt")) != "SERVER-ONLY-PLUGIN") throw new Exception("Server-only setup overwrote plugin settings.");
                    if (new FileInfo(Path.Combine(serverOnlyRoot, "B2SBackglassServer.dll")).Length < 100000) throw new Exception("Server-only payload was not installed.");
                    if (Directory.GetFiles(Path.Combine(serverOnlyRoot, "B2SPro-Backups"), "B2SBackglassServer.dll", SearchOption.AllDirectories).Length != 1) throw new Exception("Server-only backup was not created.");
                    if (!File.Exists(Path.Combine(serverOnlyRoot, "B2SServer-Install.log"))) throw new Exception("Server-only install log was not created.");
                    if (Directory.GetFiles(serverOnlyRoot, "B2SPro.exe", SearchOption.AllDirectories).Length != 0) throw new Exception("Server-only setup installed Designer files.");
                }

                Directory.Delete(sandbox, true);
                Console.WriteLine("SELF-TEST PASSED: full x64/x86 plus server-only install, online/offline package layouts, backups, protected files, Designer isolation, and shortcuts; live registration skipped");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("SELF-TEST FAILED: " + ex);
                return 1;
            }
        }
    }
}
