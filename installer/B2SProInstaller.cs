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
[assembly: AssemblyVersion("3.0.2.0")]
[assembly: AssemblyFileVersion("3.0.2.0")]
[assembly: AssemblyInformationalVersion("3.0.2")]
#else
[assembly: AssemblyTitle("B2S Pro Setup")]
[assembly: AssemblyProduct("B2S Pro Backglass Designer")]
[assembly: AssemblyVersion("1.0.3.0")]
[assembly: AssemblyFileVersion("1.0.3.0")]
[assembly: AssemblyInformationalVersion("1.0.3")]
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
            Application.Run(new InstallerForm(InstallerLaunchOptions.Parse(args)));
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

    internal sealed class InstallerLaunchOptions
    {
        public string InstalledDesignerFolder { get; private set; }
        public string InstalledServerFolder { get; private set; }

        public static InstallerLaunchOptions Parse(string[] args)
        {
            var options = new InstallerLaunchOptions();
            for (int index = 0; index < args.Length; index++)
            {
                if (String.Equals(args[index], "--installed-designer", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
                {
                    options.InstalledDesignerFolder = NormalizeFolder(args[++index]);
                }
                else if (String.Equals(args[index], "--installed-server", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
                {
                    options.InstalledServerFolder = NormalizeFolder(args[++index]);
                }
            }
            return options;
        }

        public bool HasInstalledFolder
        {
            get { return !String.IsNullOrWhiteSpace(InstalledDesignerFolder) || !String.IsNullOrWhiteSpace(InstalledServerFolder); }
        }

        public static string FindVpxRootNear(string installationFolder)
        {
            if (String.IsNullOrWhiteSpace(installationFolder)) return null;
            DirectoryInfo candidate;
            try { candidate = new DirectoryInfo(Path.GetFullPath(installationFolder)); }
            catch { return null; }

            for (int level = 0; level < 4 && candidate != null; level++, candidate = candidate.Parent)
            {
                try
                {
                    if (candidate.Exists && Directory.GetFiles(candidate.FullName, "VPinballX*.exe", SearchOption.TopDirectoryOnly).Length > 0)
                        return candidate.FullName;
                }
                catch { }
            }
            return null;
        }

        private static string NormalizeFolder(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return null;
            return Path.GetFullPath(value.Trim().Trim('"'));
        }
    }

    internal sealed class InstallerForm : Form
    {
        private readonly TextBox _vpxFolder = new TextBox();
        private readonly TextBox _designerFolder = new TextBox();
        private readonly TextBox _serverFolder = new TextBox();
        private readonly ComboBox _architecture = new ComboBox();
        private readonly RadioButton _localSource = new RadioButton();
        private readonly RadioButton _githubSource = new RadioButton();
        private readonly Button _installButton = new Button();
        private readonly ProgressBar _progress = new ProgressBar();
        private readonly Label _status = new Label();
        private readonly Label _intro = new Label();
        private readonly Panel _contentHost = new Panel();
        private readonly TableLayoutPanel _root = new TableLayoutPanel();
        private readonly PackagePaths _localPackages;
        private bool _fittingLayout;
        private string _suggestedVpxRoot;

        public InstallerForm(InstallerLaunchOptions launchOptions)
        {
            _localPackages = FindLocalPackages();
            Text = SetupEdition.Title;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            BackColor = Color.FromArgb(9, 13, 20);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9F);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = SetupEdition.ServerOnly ? new Size(704, 422) : new Size(704, 532);
            BuildInterface();
            LoadSavedPaths();
            ApplyLaunchOptions(launchOptions);
            _vpxFolder.Leave += delegate { if (Directory.Exists(_vpxFolder.Text.Trim())) SetSuggestedFolders(_vpxFolder.Text.Trim()); };
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            FitWindowToCurrentDisplay();
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            if (IsHandleCreated && !IsDisposed)
                BeginInvoke(new MethodInvoker(FitWindowToCurrentDisplay));
        }

        private int ScaleLogicalPixels(int value)
        {
            return Math.Max(1, (int)Math.Ceiling(value * DeviceDpi / 96F));
        }

        private void FitWindowToCurrentDisplay()
        {
            if (_fittingLayout || _root.Parent == null) return;
            _fittingLayout = true;
            try
            {
                SuspendLayout();
                MinimumSize = Size.Empty;

                Rectangle workingArea = Screen.FromControl(this).WorkingArea;
                int chromeWidth = Math.Max(0, Width - ClientSize.Width);
                int chromeHeight = Math.Max(0, Height - ClientSize.Height);
                int outerMargin = ScaleLogicalPixels(24);
                int availableWidth = Math.Max(1, workingArea.Width - chromeWidth - outerMargin);
                int availableHeight = Math.Max(1, workingArea.Height - chromeHeight - outerMargin);
                int targetWidth = Math.Min(ScaleLogicalPixels(704), availableWidth);

                ClientSize = new Size(targetWidth, Math.Min(ClientSize.Height, availableHeight));
                int textWidth = Math.Max(ScaleLogicalPixels(280), targetWidth - _root.Padding.Horizontal - ScaleLogicalPixels(8));
                _intro.MaximumSize = new Size(textWidth, 0);
                _status.MaximumSize = new Size(textWidth, 0);
                _root.PerformLayout();
                _contentHost.PerformLayout();

                int preferredHeight = Math.Max(_root.PreferredSize.Height, _root.Height);
                int targetHeight = Math.Min(preferredHeight, availableHeight);
                ClientSize = new Size(targetWidth, targetHeight);
                _contentHost.AutoScrollMinSize = new Size(0, preferredHeight);

                int minimumHeight = Math.Min(ScaleLogicalPixels(300), availableHeight);
                MinimumSize = SizeFromClientSize(new Size(Math.Min(targetWidth, availableWidth), minimumHeight));

                int x = Math.Max(workingArea.Left, Math.Min(Left, workingArea.Right - Width));
                int y = Math.Max(workingArea.Top, Math.Min(Top, workingArea.Bottom - Height));
                Location = new Point(x, y);
            }
            finally
            {
                ResumeLayout(true);
                _fittingLayout = false;
            }
        }

        private void LoadSavedPaths()
        {
            string[] paths = SetupPaths.Load();
            if (Directory.Exists(paths[0]) && Directory.GetFiles(paths[0], "VPinballX*.exe").Length > 0)
            {
                _vpxFolder.Text = paths[0];
                SetSuggestedFolders(paths[0]);
            }
            if (!SetupEdition.ServerOnly && Directory.Exists(paths[1])) _designerFolder.Text = paths[1];
            if (Directory.Exists(paths[2])) _serverFolder.Text = paths[2];
        }

        private void ApplyLaunchOptions(InstallerLaunchOptions options)
        {
            if (options == null || !options.HasInstalledFolder) return;

            string installedFolder = SetupEdition.ServerOnly ? options.InstalledServerFolder : options.InstalledDesignerFolder;
            if (String.IsNullOrWhiteSpace(installedFolder)) return;

            if (!SetupEdition.ServerOnly) _designerFolder.Text = installedFolder;
            string vpxRoot = InstallerLaunchOptions.FindVpxRootNear(installedFolder);
            if (!String.IsNullOrWhiteSpace(vpxRoot))
            {
                _vpxFolder.Text = vpxRoot;
                SetSuggestedFolders(vpxRoot);
            }

            if (SetupEdition.ServerOnly) _serverFolder.Text = installedFolder;
            if (!SetupEdition.ServerOnly)
            {
                string installedDesigner = Path.Combine(installedFolder, "B2SPro.exe");
                if (File.Exists(installedDesigner)) _architecture.SelectedIndex = PeArchitecture.Is32Bit(installedDesigner) ? 1 : 0;
            }

            _githubSource.Checked = true;
            _localSource.Checked = false;
            _status.Text = String.IsNullOrWhiteSpace(vpxRoot)
                ? "Existing installation folder loaded. Choose the Visual Pinball folder to continue."
                : "Existing installation locations loaded. The latest verified release will be downloaded from GitHub.";
        }

        private void BuildInterface()
        {
            _contentHost.Dock = DockStyle.Fill;
            _contentHost.AutoScroll = true;
            _contentHost.BackColor = BackColor;

            _root.Dock = DockStyle.Top;
            _root.AutoSize = true;
            _root.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _root.Padding = new Padding(16, 12, 16, 12);
            _root.ColumnCount = 1;
            _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _root.RowCount = SetupEdition.ServerOnly ? 7 : 9;
            for (int row = 0; row < _root.RowCount; row++)
                _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _contentHost.Controls.Add(_root);
            Controls.Add(_contentHost);

            _root.Controls.Add(CreateHeader(), 0, 0);
            _root.Controls.Add(CreateIntro(), 0, 1);
            _root.Controls.Add(CreatePathRow("1. Visual Pinball folder", _vpxFolder, BrowseVpx, "Browse..."), 0, 2);
            int nextRow = 3;
            if (!SetupEdition.ServerOnly)
            {
                _root.Controls.Add(CreatePathRow("2. B2S Pro Designer folder", _designerFolder, delegate { BrowseFolder(_designerFolder); }, "Browse..."), 0, nextRow++);
            }
            _root.Controls.Add(CreatePathRow(SetupEdition.ServerOnly ? "2. B2S Server folder — automatically detected" : "B2S Server folder — automatically detected", _serverFolder, delegate { BrowseFolder(_serverFolder); }, "Change..."), 0, nextRow++);
            _serverFolder.ReadOnly = true;
            if (!SetupEdition.ServerOnly) _root.Controls.Add(CreateArchitectureRow(), 0, nextRow++);
            _root.Controls.Add(CreateSourcePanel(), 0, nextRow++);

            var statusPanel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 2 };
            statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            statusPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 8));
            statusPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _progress.Dock = DockStyle.Fill;
            _progress.MinimumSize = new Size(0, 8);
            _progress.Style = ProgressBarStyle.Continuous;
            _status.AutoSize = true;
            _status.Dock = DockStyle.Top;
            _status.Padding = new Padding(2, 8, 2, 0);
            _status.ForeColor = Color.FromArgb(140, 220, 255);
            _status.Text = SetupEdition.ServerOnly ? "Choose the Visual Pinball and Server folders, then click Install / Update." : "Choose the three folders, then click Install / Update.";
            statusPanel.Controls.Add(_progress, 0, 0);
            statusPanel.Controls.Add(_status, 0, 1);
            _root.Controls.Add(statusPanel, 0, nextRow++);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(0, 2, 0, 0) };
            var close = StyledButton("Close", 104);
            close.Click += delegate { Close(); };
            _installButton.Text = SetupEdition.ButtonText;
            StyleButton(_installButton, SetupEdition.ServerOnly ? 184 : 178);
            _installButton.BackColor = Color.FromArgb(25, 112, 68);
            _installButton.Click += InstallClicked;
            buttons.Controls.Add(close);
            buttons.Controls.Add(_installButton);
            _root.Controls.Add(buttons, 0, nextRow);
        }

        private Control CreateHeader()
        {
            var panel = new Panel { Dock = DockStyle.Top, BackColor = Color.Black, MinimumSize = new Size(0, SetupEdition.ServerOnly ? 78 : 82) };
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
            _intro.AutoSize = true;
            _intro.Dock = DockStyle.Top;
            _intro.Text = SetupEdition.ServerOnly
                ? "Installs or updates only the B2S Server used by Visual Pinball. B2S Pro and the original Backglass Designer are not installed or changed."
                : "B2S Pro installs beside the original Backglass Designer and includes the required B2S Server update. Nothing is written until all locations are validated and every existing program-file replacement is approved.";
            _intro.Font = new Font(Font, FontStyle.Bold);
            _intro.ForeColor = Color.FromArgb(230, 235, 245);
            _intro.Padding = new Padding(2, 8, 2, 2);
            return _intro;
        }

        private Control CreatePathRow(string title, TextBox textBox, EventHandler browse, string buttonText)
        {
            var panel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, RowCount = 2 };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var label = new Label { Text = title, AutoSize = true, Dock = DockStyle.Top, ForeColor = Color.FromArgb(80, 210, 255), Font = new Font(Font, FontStyle.Bold) };
            textBox.Dock = DockStyle.Fill;
            textBox.Margin = new Padding(3, 3, 3, 4);
            textBox.BackColor = Color.FromArgb(27, 33, 45);
            textBox.ForeColor = Color.White;
            textBox.BorderStyle = BorderStyle.FixedSingle;
            var button = StyledButton(buttonText, 90);
            button.Click += browse;

            panel.Controls.Add(label, 0, 0);
            panel.SetColumnSpan(label, 2);
            panel.Controls.Add(textBox, 0, 1);
            panel.Controls.Add(button, 1, 1);
            return panel;
        }

        private Control CreateArchitectureRow()
        {
            var panel = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 3, 0, 0), WrapContents = true };
            panel.Controls.Add(new Label { Text = "Designer edition:", AutoSize = true, MinimumSize = new Size(114, 27), TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(80, 210, 255), Font = new Font(Font, FontStyle.Bold) });
            _architecture.DropDownStyle = ComboBoxStyle.DropDownList;
            _architecture.Items.AddRange(new object[] { "64-bit (recommended)", "32-bit" });
            _architecture.SelectedIndex = Environment.Is64BitOperatingSystem ? 0 : 1;
            _architecture.MinimumSize = new Size(194, 0);
            panel.Controls.Add(_architecture);
            panel.Controls.Add(new Label { Text = "Server supports both x86 and x64.", AutoSize = true, MinimumSize = new Size(250, 27), TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.Silver });
            return panel;
        }

        private Control CreateSourcePanel()
        {
            var group = new GroupBox { Text = "Build source", Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ForeColor = Color.White, Padding = new Padding(10, 7, 10, 5) };
            var flow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(4, 1, 0, 0) };
            _localSource.Text = SetupEdition.ServerOnly ? "Install from the verified Server package beside this updater" : "Install from the verified Designer and Server packages beside this updater";
            _localSource.AutoSize = true;
            _localSource.Margin = new Padding(3, 0, 3, 0);
            _localSource.ForeColor = Color.White;
            _localSource.BackColor = Color.Transparent;
            _localSource.UseVisualStyleBackColor = false;
            _localSource.Enabled = _localPackages.AreAvailable(SetupEdition.ServerOnly);
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
            if (String.Equals(_suggestedVpxRoot, vpxRoot, StringComparison.OrdinalIgnoreCase)) return;
            _suggestedVpxRoot = vpxRoot;
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

            PackagePaths packagePaths = null;
            string downloadFolder = null;
            try
            {
                SetBusy(true, "Preparing the verified release packages...");
                if (_localSource.Checked)
                {
                    packagePaths = _localPackages;
                    if (!SetupEdition.ServerOnly) VerifySidecar(packagePaths.DesignerPath);
                    VerifySidecar(packagePaths.ServerPath);
                }
                else
                {
                    downloadFolder = Path.Combine(Path.GetTempPath(), (SetupEdition.ServerOnly ? "B2SServerSetup-" : "B2SProSetup-") + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(downloadFolder);
                    packagePaths = await GitHubRelease.DownloadLatestAsync(downloadFolder, SetupEdition.ServerOnly, ReportDownloadProgress);
                }

                string arch = SetupEdition.ServerOnly ? "x64" : (_architecture.SelectedIndex == 1 ? "x86" : "x64");
                using (var package = new ReleaseBundle(packagePaths.DesignerPath, packagePaths.ServerPath))
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
                    try
                    {
                        SetupPaths.Save(_vpxFolder.Text.Trim(), SetupEdition.ServerOnly ? null : _designerFolder.Text.Trim(), _serverFolder.Text.Trim());
                    }
                    catch (Exception ex)
                    {
                        message += Environment.NewLine + "Installation paths could not be remembered: " + ex.Message;
                    }
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
                if (IsLockedForReplacement(Path.Combine(_designerFolder.Text.Trim(), "B2SPro.exe")))
                    return "B2S Pro is still open. Save your work and close B2S Pro before installing the update.";
            }
            if (IsLockedForReplacement(Path.Combine(_serverFolder.Text.Trim(), "B2SBackglassServer.dll")))
                return "The B2S Server is still in use. Close Visual Pinball and any running backglass before installing the update.";
            if (_localSource.Checked && !_localPackages.AreAvailable(SetupEdition.ServerOnly))
                return SetupEdition.ServerOnly
                    ? "The required local Server ZIP and checksum are no longer beside the installer."
                    : "The required local Designer and Server ZIPs and checksums are no longer beside the installer.";
            return null;
        }

        private static bool IsLockedForReplacement(string path)
        {
            if (!File.Exists(path)) return false;
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                return false;
            }
            catch (IOException) { return true; }
            catch (UnauthorizedAccessException) { return true; }
        }

        private bool ConfirmReplacement(InstallPlan plan)
        {
            if (plan.ExistingProgramFiles.Count == 0) return true;
            var message = new StringBuilder();
            message.AppendLine("An existing B2S installation was found.");
            message.AppendLine();
            message.AppendLine(SetupEdition.ServerOnly
                ? "Do you want to back up and overwrite these B2S Server program files?"
                : "The B2S Server update is included. Do you want to back up and overwrite these program files?");
            message.AppendLine();
            message.AppendLine("ScreenRes.txt, B2STableSettings.xml, plug-ins, projects, tables, and backglasses will not be overwritten.");
            return MessageBox.Show(this, message.ToString(), "Existing installation found", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        private static PackagePaths FindLocalPackages()
        {
            string folder = AppDomain.CurrentDomain.BaseDirectory;
            string designer = SetupEdition.ServerOnly ? null : FindLocalVersionedPackage(folder, "B2S-Pro-Backglass-*.zip", "^B2S-Pro-Backglass-[0-9][A-Za-z0-9._-]*\\.zip$");
            string server = FindLocalVersionedPackage(folder, "B2S-Pro-Server-*.zip", "^B2S-Pro-Server-[0-9][A-Za-z0-9._-]*\\.zip$");
            return new PackagePaths(designer, server);
        }

        private static string FindLocalVersionedPackage(string folder, string pattern, string validNamePattern)
        {
            string[] matches = Directory.GetFiles(folder, pattern, SearchOption.TopDirectoryOnly);
            Array.Sort(matches, StringComparer.OrdinalIgnoreCase);
            for (int index = matches.Length - 1; index >= 0; index--)
            {
                if (Regex.IsMatch(Path.GetFileName(matches[index]), validNamePattern, RegexOptions.IgnoreCase)) return matches[index];
            }
            return null;
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

        private static void VerifySidecar(string zipPath)
        {
            if (String.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath)) throw new InvalidDataException("A required local release package is missing. Nothing was installed.");
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
            button.AutoSize = true;
            button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            button.MinimumSize = new Size(width, 30);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(55, 145, 255);
            button.BackColor = Color.FromArgb(28, 48, 82);
            button.ForeColor = Color.White;
        }
    }

    internal sealed class PackagePaths
    {
        public readonly string DesignerPath;
        public readonly string ServerPath;

        public PackagePaths(string designerPath, string serverPath)
        {
            DesignerPath = designerPath;
            ServerPath = serverPath;
        }

        public bool AreAvailable(bool serverOnly)
        {
            bool serverAvailable = !String.IsNullOrWhiteSpace(ServerPath) && File.Exists(ServerPath) && File.Exists(ServerPath + ".sha256");
            if (serverOnly) return serverAvailable;
            return serverAvailable && !String.IsNullOrWhiteSpace(DesignerPath) && File.Exists(DesignerPath) && File.Exists(DesignerPath + ".sha256");
        }
    }

    internal static class GitHubRelease
    {
        private const string LatestApi = "https://api.github.com/repos/KWildman69/B2S-Pro/releases/latest";

        public static async Task<PackagePaths> DownloadLatestAsync(string destination, bool serverOnly, Action<int, string> progress)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd(serverOnly ? "B2S-Server-Updater/3.0.2" : "B2S-Pro-Installer/1.0.3");
                string json;
                try
                {
                    json = await client.GetStringAsync(LatestApi);
                }
                catch (HttpRequestException ex)
                {
                    throw new InvalidOperationException("The latest GitHub release could not be reached. If the cabinet is offline, keep the verified release ZIP files and checksums beside this setup program.\r\n\r\n" + ex.Message);
                }

                var serializer = new JavaScriptSerializer();
                var release = serializer.DeserializeObject(json) as Dictionary<string, object>;
                if (release == null) throw new InvalidDataException("GitHub returned an unreadable release response.");
                string tag = release.ContainsKey("tag_name") ? Convert.ToString(release["tag_name"]) : "latest";
                string serverName = FindPackageName(release, "^B2S-Pro-Server-[0-9][A-Za-z0-9._-]*\\.zip$");
                string designerName = serverOnly ? null : FindPackageName(release, "^B2S-Pro-Backglass-[0-9][A-Za-z0-9._-]*\\.zip$");
                if (serverName == null || (!serverOnly && designerName == null))
                    throw new InvalidDataException("The latest release does not contain the required " + (serverOnly ? "Server" : "Designer and Server") + " ZIP package" + (serverOnly ? "" : "s") + ".");

                string designerPath = null;
                if (!serverOnly) designerPath = await DownloadVerifiedPackage(client, release, destination, designerName, tag, "B2S Pro Designer", 0, 70, progress);
                string serverPath = await DownloadVerifiedPackage(client, release, destination, serverName, tag, "B2S Server", serverOnly ? 0 : 70, serverOnly ? 100 : 30, progress);
                progress(100, "Downloaded and verified " + tag + ".");
                return new PackagePaths(designerPath, serverPath);
            }
        }

        private static string FindPackageName(Dictionary<string, object> release, string validNamePattern)
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
                if (Regex.IsMatch(name, validNamePattern, RegexOptions.IgnoreCase)) return name;
            }
            return null;
        }

        private static async Task<string> DownloadVerifiedPackage(HttpClient client, Dictionary<string, object> release, string destination, string packageName, string tag, string product, int progressOffset, int progressSpan, Action<int, string> progress)
        {
            string hashName = packageName + ".sha256";
            string packageUrl = FindAsset(release, packageName);
            string hashUrl = FindAsset(release, hashName);
            if (packageUrl == null || hashUrl == null) throw new InvalidDataException("The latest release does not contain " + packageName + " and its SHA-256 file.");

            string zipPath = Path.Combine(destination, packageName);
            string hashPath = Path.Combine(destination, hashName);
            await DownloadFile(client, packageUrl, zipPath, tag, product, progressOffset, progressSpan, progress);
            File.WriteAllBytes(hashPath, await client.GetByteArrayAsync(hashUrl));
            string expected = Regex.Match(File.ReadAllText(hashPath), "[A-Fa-f0-9]{64}").Value;
            string actual = Hashing.Sha256(zipPath);
            if (expected.Length != 64 || !String.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(product + " failed SHA-256 verification. Nothing was installed.");
            progress(progressOffset + progressSpan, "Downloaded and verified " + product + ".");
            return zipPath;
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

        private static async Task DownloadFile(HttpClient client, string url, string destination, string tag, string product, int progressOffset, int progressSpan, Action<int, string> progress)
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
                        progress(progressOffset + (percent * progressSpan / 100), "Downloading " + product + " " + tag + "... " + percent + "%");
                    }
                }
            }
        }
    }

    internal sealed class ReleaseBundle : IDisposable
    {
        private readonly ZipArchive _designerArchive;
        private readonly ZipArchive _serverArchive;

        public ReleaseBundle(string designerPath, string serverPath)
        {
            if (String.IsNullOrWhiteSpace(serverPath)) throw new InvalidDataException("The Server package path is missing. Nothing was installed.");
            if (!String.IsNullOrWhiteSpace(designerPath)) _designerArchive = ZipFile.OpenRead(designerPath);
            try { _serverArchive = ZipFile.OpenRead(serverPath); }
            catch
            {
                if (_designerArchive != null) _designerArchive.Dispose();
                throw;
            }
        }

        public void Validate(string arch, bool installDesigner)
        {
            if (installDesigner)
            {
                if (_designerArchive == null) throw new InvalidDataException("The Designer package is missing. Nothing was installed.");
                Require(_designerArchive, arch + "/B2SPro.exe");
                Require(_designerArchive, arch + "/B2SPro.exe.config");
                Require(_designerArchive, arch + "/B2SVPinMAMEStarter.exe");
                Require(_designerArchive, arch + "/B2SUpdateChecker.exe");
            }
            string serverPrefix = GetServerPrefix();
            string[] requiredServerFiles =
            {
                "B2S-Pro-Changelog.md",
                "B2SBackglassServer.dll",
                "B2SBackglassServerEXE.exe",
                "B2SBackglassServerEXE.exe.config",
                "B2SBackglassServerRegisterApp.exe",
                "B2SInit.cmd",
                "B2SServerPluginInterface.dll",
                "B2SUpdateChecker.exe",
                "B2SWindowPunch.exe",
                "B2S_ScreenResIdentifier.exe",
                "B2S_ScreenResIdentifier.exe.config",
                "B2S_SetUp.exe",
                "B2S_SetUp.exe.config",
                "license.txt",
                "README.txt",
                "ScreenResTemplate.txt",
                "ScreenResTemplates.cmd",
                "B2STools/B2SRandom.cmd",
                "B2STools/B2STools.txt",
                "B2STools/directb2sReelSoundsONOFF.cmd",
                "B2STools/directb2sReelSoundsONOFF.xsl",
                "B2STools/DmdDeviceIniScale.cmd",
                "Plugins/Plugins.txt",
                "Plugins64/Plugins.txt",
                "ScreenResTemplates/ScreenResTemplates.txt"
            };
            foreach (string name in requiredServerFiles) Require(_serverArchive, serverPrefix + name);
            Forbid(_serverArchive, serverPrefix + "ScreenRes.txt");
            Forbid(_serverArchive, serverPrefix + "Changelog.txt");
            Forbid(_serverArchive, serverPrefix + "B2S-native-rotation-changelog.txt");
            Forbid(_serverArchive, serverPrefix + "B2SNativeRotationDiagnostic.log");
            Forbid(_serverArchive, serverPrefix + "B2SNativeRotationDiagnostic.txt");
        }

        public InstallPlan CreatePlan(string designer, string server, string arch, bool installDesigner)
        {
            return new InstallPlan(_designerArchive, _serverArchive, designer, server, arch, installDesigner, GetServerPrefix());
        }

        private string GetServerPrefix()
        {
            return _serverArchive.GetEntry("Runtime/B2SServer/B2SBackglassServer.dll") != null ? "Runtime/B2SServer/" : String.Empty;
        }

        private static void Require(ZipArchive archive, string name)
        {
            if (archive.GetEntry(name) == null) throw new InvalidDataException("The package is missing " + name + ". Nothing was installed.");
        }

        private static void Forbid(ZipArchive archive, string name)
        {
            if (archive.GetEntry(name) != null) throw new InvalidDataException("The package contains retired file " + name + ". Nothing was installed.");
        }

        public void Dispose()
        {
            if (_designerArchive != null) _designerArchive.Dispose();
            _serverArchive.Dispose();
        }
    }

    internal sealed class InstallPlan
    {
        private readonly ZipArchive _designerArchive;
        private readonly ZipArchive _serverArchive;
        private readonly string _designer;
        private readonly string _server;
        private readonly string _arch;
        private readonly bool _installDesigner;
        private readonly string _serverPrefix;
        private readonly bool _freshServer;
        private readonly List<CopyItem> _items = new List<CopyItem>();
        private static readonly string[] RetiredServerFiles =
        {
            "Changelog.txt",
            "B2S-native-rotation-changelog.txt",
            "B2SNativeRotationDiagnostic.log",
            "B2SNativeRotationDiagnostic.txt"
        };
        public readonly List<string> ExistingProgramFiles = new List<string>();

        public InstallPlan(ZipArchive designerArchive, ZipArchive serverArchive, string designer, string server, string arch, bool installDesigner, string serverPrefix)
        {
            _designerArchive = designerArchive;
            _serverArchive = serverArchive;
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
            if (_installDesigner)
            {
                string designerPrefix = _arch + "/";
                foreach (ZipArchiveEntry entry in _designerArchive.Entries)
                {
                    if (String.IsNullOrEmpty(entry.Name) || !entry.FullName.StartsWith(designerPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                    string relative = entry.FullName.Substring(designerPrefix.Length).Replace('/', Path.DirectorySeparatorChar);
                    Add(entry, Path.Combine(_designer, relative), false, true);
                }
            }
            foreach (ZipArchiveEntry entry in _serverArchive.Entries)
            {
                if (String.IsNullOrEmpty(entry.Name) || !entry.FullName.StartsWith(_serverPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                string relative = entry.FullName.Substring(_serverPrefix.Length).Replace('/', Path.DirectorySeparatorChar);
                bool protect = IsProtectedServerPath(relative);
                Add(entry, Path.Combine(_server, relative), protect, false);
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
            int retired = 0;
            try
            {
                foreach (string name in RetiredServerFiles)
                {
                    string destination = Path.Combine(_server, name);
                    if (!File.Exists(destination)) continue;
                    string backup = Path.Combine(serverBackup, name);
                    Directory.CreateDirectory(Path.GetDirectoryName(backup));
                    File.Copy(destination, backup, true);
                    changed.Add(new RollbackItem(destination, backup, true));
                    File.SetAttributes(destination, FileAttributes.Normal);
                    File.Delete(destination);
                    retired++;
                }

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
                if (registerFileAssociations || registerServer)
                {
                    try
                    {
                        if (registerFileAssociations) FileAssociationManager.Register(_designer, _server);
                        FileAssociationManager.RegisterIcons(_server);
                    }
                    catch (Exception ex) { fileAssociationError = ex.Message; }
                }

                string shortcutError = null;
                if (createShortcuts)
                {
                    try { ShortcutManager.Create(_designer); }
                    catch (Exception ex) { shortcutError = ex.Message; }
                }

                string log = Path.Combine(_installDesigner ? _designer : _server, _installDesigner ? "B2SPro-Install.log" : "B2SServer-Install.log");
                File.AppendAllText(log, DateTime.Now.ToString("s") + " Installed " + installed + " files; preserved " + preserved + " protected files; retired " + retired + " obsolete files; architecture " + _arch + "; registration " + (registerServer ? (registrationError == null ? "successful" : "failed: " + registrationError) : "skipped for test") + "; file associations " + ((registerFileAssociations || registerServer) ? (fileAssociationError == null ? "successful" : "failed: " + fileAssociationError) : "skipped") + "; shortcuts " + (createShortcuts ? (shortcutError == null ? "successful" : "failed: " + shortcutError) : "skipped for test") + Environment.NewLine);
                ApplyVisibility(log, _installDesigner);
                if (_installDesigner) ApplyVisibility(Path.Combine(_server, "B2SServer-Install.log"), false);
                if (_installDesigner) MakeBackupVisible(Path.Combine(_designer, "B2SPro-Backups"));
                MakeBackupVisible(Path.Combine(_server, "B2SPro-Backups"));
                bool backupCreated = (_installDesigner && Directory.Exists(designerBackup)) || Directory.Exists(serverBackup);
                return new InstallResult(_designer, _server, installed, preserved, retired, backupCreated ? stamp : null, _freshServer, registerServer, registrationError, registerFileAssociations || registerServer, fileAssociationError, createShortcuts, shortcutError, _installDesigner);
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
            if (!File.Exists(path)) return;
            string name = Path.GetFileName(path);
            bool hidden = String.Equals(name, "B2SUpdateChecker.exe", StringComparison.OrdinalIgnoreCase) ||
                (isDesigner
                    ? String.Equals(name, "B2SPro.exe.config", StringComparison.OrdinalIgnoreCase) ||
                      String.Equals(name, "B2SVPinMAMEStarter.exe.config", StringComparison.OrdinalIgnoreCase) ||
                      String.Equals(name, "B2SPro-Install.log", StringComparison.OrdinalIgnoreCase)
                    : String.Equals(name, "B2S_ScreenResIdentifier.exe.config", StringComparison.OrdinalIgnoreCase) ||
                      String.Equals(name, "B2S_SetUp.exe.config", StringComparison.OrdinalIgnoreCase) ||
                      String.Equals(name, "B2SBackglassServerEXE.exe.config", StringComparison.OrdinalIgnoreCase) ||
                      String.Equals(name, "B2SServer-Install.log", StringComparison.OrdinalIgnoreCase));
            FileAttributes attributes = File.GetAttributes(path) & ~FileAttributes.Hidden & ~FileAttributes.System;
            File.SetAttributes(path, hidden ? attributes | FileAttributes.Hidden : attributes);
        }

        private static void MakeVisible(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path)) return;
            FileAttributes attributes = File.GetAttributes(path);
            File.SetAttributes(path, attributes & ~FileAttributes.Hidden & ~FileAttributes.System);
        }

        private static void MakeBackupVisible(string path)
        {
            if (!Directory.Exists(path)) return;
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) return;
            MakeVisible(path);
            foreach (string file in Directory.GetFiles(path)) MakeVisible(file);
            foreach (string directory in Directory.GetDirectories(path)) MakeBackupVisible(directory);
        }
    }

    internal static class SetupPaths
    {
        private static readonly string SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "B2SPro", "SetupPaths.txt");

        public static string[] Load()
        {
            try
            {
                string[] paths = File.ReadAllLines(SettingsPath);
                if (paths.Length == 3) return paths;
            }
            catch { }
            return new string[] { "", "", "" };
        }

        public static void Save(string vpx, string designer, string server)
        {
            string[] paths = Load();
            paths[0] = vpx;
            if (designer != null) paths[1] = designer;
            paths[2] = server;
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            string temporary = SettingsPath + ".new";
            File.WriteAllLines(temporary, paths);
            if (File.Exists(SettingsPath)) File.Replace(temporary, SettingsPath, null);
            else File.Move(temporary, SettingsPath);
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
        private readonly int _retired;
        private readonly string _backupStamp;
        private readonly bool _freshServer;
        private readonly bool _registrationAttempted;
        private readonly string _registrationError;
        private readonly bool _fileAssociationsAttempted;
        private readonly string _fileAssociationError;
        private readonly bool _shortcutsAttempted;
        private readonly string _shortcutError;
        private readonly bool _installedDesigner;
        public InstallResult(string designer, string server, int installed, int preserved, int retired, string backupStamp, bool freshServer, bool registrationAttempted, string registrationError, bool fileAssociationsAttempted, string fileAssociationError, bool shortcutsAttempted, string shortcutError, bool installedDesigner)
        {
            _designer = designer;
            _server = server;
            _installed = installed;
            _preserved = preserved;
            _retired = retired;
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
            if (_retired > 0) text.AppendLine("Obsolete B2S text/diagnostic files removed: " + _retired);
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
                if (_installedDesigner) text.AppendLine(".B2SPro and .directB2S files were associated with the B2S Pro editor.");
                text.AppendLine(".B2SPro files use the Pro icon; .directB2S files use the legacy B2S icon.");
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

        public static void Register(string designerFolder, string serverFolder)
        {
            string editor = GetEditorPath(designerFolder);
            if (!File.Exists(editor)) throw new FileNotFoundException("B2SPro.exe was not found for file association registration.", editor);

            using (RegistryKey extension = Registry.ClassesRoot.CreateSubKey(".B2SPro"))
                extension.SetValue("", B2SProProgId);
            using (RegistryKey legacyExtension = Registry.ClassesRoot.CreateSubKey(".directb2s"))
                legacyExtension.SetValue("", LegacyProgId);

            RegisterFileType(B2SProProgId, ".B2SPro", editor, designerFolder);
            RegisterFileType(LegacyProgId, ".directB2S", Path.Combine(serverFolder, "B2SBackglassServerEXE.exe"), designerFolder);
            SHChangeNotify(AssociationChanged, IdList, IntPtr.Zero, IntPtr.Zero);
        }

        // Extension-level icons remain independent of the user's opening application.
        // Both icon resources are installed with the server, including server-only setup.
        public static void RegisterIcons(string serverFolder)
        {
            string proIcon = Path.Combine(Path.GetFullPath(serverFolder), "B2SUpdateChecker.exe");
            string legacyIcon = Path.Combine(Path.GetFullPath(serverFolder), "B2SBackglassServerEXE.exe");
            if (!File.Exists(proIcon)) throw new FileNotFoundException("B2S Pro icon resource was not installed.", proIcon);
            if (!File.Exists(legacyIcon)) throw new FileNotFoundException("Legacy B2S icon resource was not installed.", legacyIcon);

            using (RegistryKey icon = Registry.ClassesRoot.CreateSubKey(".B2SPro\\DefaultIcon"))
                icon.SetValue("", Quote(proIcon) + ",0");
            using (RegistryKey icon = Registry.ClassesRoot.CreateSubKey(".directb2s\\DefaultIcon"))
                icon.SetValue("", Quote(legacyIcon) + ",0");
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
                if (args.Length < 3 || !File.Exists(args[1]) || !File.Exists(args[2])) throw new ArgumentException("Usage: B2SSetup.exe --self-test <designer-build.zip> <server-build.zip>");
                string sandbox = Path.Combine(Path.GetTempPath(), "B2SProInstallerSelfTest-" + Guid.NewGuid().ToString("N"));
                string originalDesigner = Path.Combine(sandbox, "OriginalDesigner");
                Directory.CreateDirectory(originalDesigner);
                string originalDesignerExe = Path.Combine(originalDesigner, "B2SBackglassDesigner.exe");
                File.WriteAllText(originalDesignerExe, "ORIGINAL-DESIGNER-MUST-STAY");
                string[] retiredServerFiles =
                {
                    "Changelog.txt",
                    "B2S-native-rotation-changelog.txt",
                    "B2SNativeRotationDiagnostic.log",
                    "B2SNativeRotationDiagnostic.txt"
                };

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
                    foreach (string name in retiredServerFiles) File.WriteAllText(Path.Combine(server, name), "OBSOLETE-SERVER-FILE");

                    using (var package = new ReleaseBundle(args[1], args[2]))
                    {
                        package.Validate(arch, true);
                        InstallPlan plan = package.CreatePlan(designer, server, arch, true);
                        if (plan.ExistingProgramFiles.Count < 2) throw new Exception(arch + " existing-program detection failed.");
                        plan.Execute(false, false, false);
                        if (File.ReadAllText(Path.Combine(server, "ScreenRes.txt")) != "SELF-TEST-SCREENRES") throw new Exception(arch + " ScreenRes.txt was overwritten.");
                        if (File.ReadAllText(Path.Combine(server, "Plugins", "Plugins.txt")) != "SELF-TEST-PLUGIN") throw new Exception(arch + " plugin settings were overwritten.");
                        if (new FileInfo(Path.Combine(designer, "B2SPro.exe")).Length < 1000000) throw new Exception(arch + " Designer payload was not installed.");
                        if (new FileInfo(Path.Combine(server, "B2SBackglassServer.dll")).Length < 100000) throw new Exception(arch + " Server payload was not installed.");
                        if (new FileInfo(Path.Combine(designer, "B2SUpdateChecker.exe")).Length < 10000) throw new Exception(arch + " Designer update checker was not installed.");
                        if (new FileInfo(Path.Combine(server, "B2SUpdateChecker.exe")).Length < 10000) throw new Exception(arch + " Server update checker was not installed.");
                        if (Directory.GetFiles(Path.Combine(designer, "B2SPro-Backups"), "B2SPro.exe", SearchOption.AllDirectories).Length != 1) throw new Exception(arch + " Designer backup was not created.");
                        if (Directory.GetFiles(Path.Combine(server, "B2SPro-Backups"), "B2SBackglassServer.dll", SearchOption.AllDirectories).Length != 1) throw new Exception(arch + " Server backup was not created.");
                        foreach (string name in retiredServerFiles)
                        {
                            if (File.Exists(Path.Combine(server, name))) throw new Exception(arch + " obsolete Server file was not removed: " + name);
                            if (Directory.GetFiles(Path.Combine(server, "B2SPro-Backups"), name, SearchOption.AllDirectories).Length != 1) throw new Exception(arch + " obsolete Server file was not backed up: " + name);
                        }
                        if ((File.GetAttributes(Path.Combine(designer, "B2SPro.exe")) & FileAttributes.Hidden) != 0) throw new Exception(arch + " main Designer executable was hidden.");
                        if ((File.GetAttributes(Path.Combine(designer, "B2SPro.exe.config")) & FileAttributes.Hidden) == 0) throw new Exception(arch + " Designer config was not hidden.");
                        if ((File.GetAttributes(Path.Combine(designer, "B2SPro.exe.config")) & FileAttributes.System) != 0) throw new Exception(arch + " Designer config was marked as a system file.");
                        if ((File.GetAttributes(Path.Combine(server, "B2SBackglassServer.dll")) & FileAttributes.Hidden) != 0) throw new Exception(arch + " Server DLL was hidden.");
                        if ((File.GetAttributes(Path.Combine(server, "B2SBackglassServer.dll")) & FileAttributes.System) != 0) throw new Exception(arch + " Server DLL was marked as a system file.");
                        if ((File.GetAttributes(Path.Combine(designer, "B2SUpdateChecker.exe")) & (FileAttributes.Hidden | FileAttributes.System)) != FileAttributes.Hidden) throw new Exception(arch + " Designer update checker visibility was incorrect.");
                        if ((File.GetAttributes(Path.Combine(server, "B2SUpdateChecker.exe")) & (FileAttributes.Hidden | FileAttributes.System)) != FileAttributes.Hidden) throw new Exception(arch + " Server update checker visibility was incorrect.");
                        if ((File.GetAttributes(Path.Combine(server, "ScreenRes.txt")) & FileAttributes.Hidden) != 0) throw new Exception(arch + " ScreenRes.txt was hidden.");
                        if ((File.GetAttributes(Path.Combine(designer, "B2SPro-Install.log")) & (FileAttributes.Hidden | FileAttributes.System)) != FileAttributes.Hidden) throw new Exception(arch + " install log visibility was incorrect.");
                        if ((File.GetAttributes(Path.Combine(designer, "B2SPro-Backups")) & (FileAttributes.Hidden | FileAttributes.System)) != 0) throw new Exception(arch + " Designer backup folder was hidden or marked as a system file.");
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
                using (var package = new ReleaseBundle(args[1], args[2]))
                {
                    package.Validate("x64", true);
                    package.CreatePlan(Path.Combine(freshVpx, "B2SPro"), freshServer, "x64", true).Execute(false, false, false);
                }
                if (!File.Exists(Path.Combine(freshVpx, "B2SPro", "B2SPro.exe"))) throw new Exception("The fresh Designer was not installed in its separate folder.");
                if (!File.Exists(Path.Combine(freshServer, "B2SBackglassServer.dll"))) throw new Exception("The fresh Server was not installed in the suggested B2SServer folder.");
                if (!File.Exists(Path.Combine(freshVpx, "B2SPro", "B2SUpdateChecker.exe"))) throw new Exception("The fresh Designer update checker was not installed.");
                if (!File.Exists(Path.Combine(freshServer, "B2SUpdateChecker.exe"))) throw new Exception("The fresh Server update checker was not installed.");
                if (!File.Exists(Path.Combine(freshServer, "ScreenResTemplate.txt"))) throw new Exception("The fresh Server ScreenRes template was not installed.");
                if (!File.Exists(Path.Combine(freshServer, "B2S-Pro-Changelog.md"))) throw new Exception("The fresh B2S Pro changelog was not installed.");
                if (File.Exists(Path.Combine(freshServer, "ScreenRes.txt"))) throw new Exception("The fresh Server package installed an active ScreenRes.txt.");
                foreach (string name in retiredServerFiles)
                    if (File.Exists(Path.Combine(freshServer, name))) throw new Exception("The fresh Server installed obsolete file: " + name);
                InstallerLaunchOptions designerUpdate = InstallerLaunchOptions.Parse(new[] { "--installed-designer", Path.Combine(freshVpx, "B2SPro") });
                if (!String.Equals(designerUpdate.InstalledDesignerFolder, Path.Combine(freshVpx, "B2SPro"), StringComparison.OrdinalIgnoreCase))
                    throw new Exception("The Designer update location was not parsed correctly.");
                if (!String.Equals(InstallerLaunchOptions.FindVpxRootNear(designerUpdate.InstalledDesignerFolder), freshVpx, StringComparison.OrdinalIgnoreCase))
                    throw new Exception("The Visual Pinball folder was not inferred from the installed Designer location.");
                InstallerLaunchOptions serverUpdate = InstallerLaunchOptions.Parse(new[] { "--installed-server", freshServer });
                if (!String.Equals(serverUpdate.InstalledServerFolder, freshServer, StringComparison.OrdinalIgnoreCase))
                    throw new Exception("The Server update location was not parsed correctly.");
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
                    string serverOnlyRoot = Path.Combine(sandbox, "ServerOnly");
                    Directory.CreateDirectory(serverOnlyRoot);
                    File.WriteAllText(Path.Combine(serverOnlyRoot, "ScreenRes.txt"), "SERVER-ONLY-SCREENRES");
                    Directory.CreateDirectory(Path.Combine(serverOnlyRoot, "Plugins"));
                    File.WriteAllText(Path.Combine(serverOnlyRoot, "Plugins", "Plugins.txt"), "SERVER-ONLY-PLUGIN");
                    File.WriteAllText(Path.Combine(serverOnlyRoot, "B2SBackglassServer.dll"), "OLD-SERVER");
                    foreach (string name in retiredServerFiles) File.WriteAllText(Path.Combine(serverOnlyRoot, name), "OBSOLETE-SERVER-FILE");
                    using (var package = new ReleaseBundle(null, args[2]))
                    {
                        package.Validate("x64", false);
                        InstallPlan plan = package.CreatePlan(null, serverOnlyRoot, "x64", false);
                        if (plan.ExistingProgramFiles.Count == 0) throw new Exception("Server-only existing-program detection failed.");
                        plan.Execute(false, false, false);
                    }
                    if (File.ReadAllText(Path.Combine(serverOnlyRoot, "ScreenRes.txt")) != "SERVER-ONLY-SCREENRES") throw new Exception("Server-only setup overwrote ScreenRes.txt.");
                    if (File.ReadAllText(Path.Combine(serverOnlyRoot, "Plugins", "Plugins.txt")) != "SERVER-ONLY-PLUGIN") throw new Exception("Server-only setup overwrote plugin settings.");
                    if (new FileInfo(Path.Combine(serverOnlyRoot, "B2SBackglassServer.dll")).Length < 100000) throw new Exception("Server-only payload was not installed.");
                    if (new FileInfo(Path.Combine(serverOnlyRoot, "B2SUpdateChecker.exe")).Length < 10000) throw new Exception("Server-only update checker was not installed.");
                    if (!File.Exists(Path.Combine(serverOnlyRoot, "ScreenResTemplate.txt"))) throw new Exception("Server-only ScreenRes template was not installed.");
                    foreach (string name in retiredServerFiles)
                    {
                        if (File.Exists(Path.Combine(serverOnlyRoot, name))) throw new Exception("Server-only obsolete file was not removed: " + name);
                        if (Directory.GetFiles(Path.Combine(serverOnlyRoot, "B2SPro-Backups"), name, SearchOption.AllDirectories).Length != 1) throw new Exception("Server-only obsolete file was not backed up: " + name);
                    }
                    if (Directory.GetFiles(Path.Combine(serverOnlyRoot, "B2SPro-Backups"), "B2SBackglassServer.dll", SearchOption.AllDirectories).Length != 1) throw new Exception("Server-only backup was not created.");
                    if (!File.Exists(Path.Combine(serverOnlyRoot, "B2SServer-Install.log"))) throw new Exception("Server-only install log was not created.");
                    if (Directory.GetFiles(serverOnlyRoot, "B2SPro.exe", SearchOption.AllDirectories).Length != 0) throw new Exception("Server-only setup installed Designer files.");
                }

                Directory.Delete(sandbox, true);
                Console.WriteLine("SELF-TEST PASSED: split Designer/Server packages, update checker payloads and setup handoff, full x64/x86 plus server-only install, backups, protected files, Designer isolation, and shortcuts; live registration skipped");
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
