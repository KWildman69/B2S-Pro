using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
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
[assembly: AssemblyTitle("B2S Pro Server Setup")]
[assembly: AssemblyProduct("B2S Pro Server")]
[assembly: AssemblyVersion("3.0.10.0")]
[assembly: AssemblyFileVersion("3.0.10.0")]
[assembly: AssemblyInformationalVersion("3.0.10")]
#else
[assembly: AssemblyTitle("B2S Pro Setup")]
[assembly: AssemblyProduct("B2S Pro Backglass Designer")]
[assembly: AssemblyVersion("1.0.9.0")]
[assembly: AssemblyFileVersion("1.0.9.0")]
[assembly: AssemblyInformationalVersion("1.0.9")]
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
        public const string Title = "B2S Pro Server Setup";
        public const string Product = "B2S Pro Server";
        public const string ButtonText = "Install / Update Server";
#else
        public static readonly bool ServerOnly = false;
        public const string Title = "B2S Pro Setup & Updater";
        public const string Product = "B2S Pro";
        public const string ButtonText = "Install / Update B2S Pro";
#endif
        public static string DisplayVersion { get { var attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false); return attributes.Length > 0 ? ((AssemblyInformationalVersionAttribute)attributes[0]).InformationalVersion : Assembly.GetExecutingAssembly().GetName().Version.ToString(); } }
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
        private readonly CheckBox _disableAutomaticUpdateNotifications = new CheckBox();
        private readonly Button _installButton = new NeonButton { Primary = true };
        private readonly Button _restoreButton = new NeonButton();
        private bool _operationRunning;
        private readonly NeonProgress _progress = new NeonProgress();
        private readonly Label _status = new Label();
        private readonly Label _intro = new Label();
        private readonly Panel _contentHost = new NeonBackdrop();
        private readonly TableLayoutPanel _root = new TableLayoutPanel();
        private readonly PackagePaths _localPackages;
        private bool _fittingLayout;
        private string _suggestedVpxRoot;
        private string _suggestedServerFolder;

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
            ClientSize = SetupEdition.ServerOnly ? new Size(620, 450) : new Size(620, 560);
            BuildInterface();
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (_operationRunning) e.Cancel = true; };
            FormBorderStyle = FormBorderStyle.None;
            Padding = new Padding(4);
            var chrome = new NeonChrome(this) { Dock = DockStyle.Top, Height = 30 };
            Controls.Add(chrome);
            SizeChanged += delegate { NeonDrawing.ShapeWindow(this); };
            NeonDrawing.ShapeWindow(this);
            LoadSavedPaths();
            _disableAutomaticUpdateNotifications.Checked = UpdateNotificationPreference.IsDisabled();
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
                int targetWidth = Math.Min(ScaleLogicalPixels(620), availableWidth);

                ClientSize = new Size(targetWidth, Math.Min(ClientSize.Height, availableHeight));
                int textWidth = Math.Max(ScaleLogicalPixels(280), targetWidth - _root.Padding.Horizontal - ScaleLogicalPixels(8));
                _intro.MaximumSize = new Size(textWidth, 0);
                _status.MaximumSize = new Size(textWidth, 0);
                _root.PerformLayout();
                _contentHost.PerformLayout();

                int preferredHeight = Math.Max(_root.PreferredSize.Height, _root.Height);
                int captionHeight = 0;
                foreach (Control control in Controls) if (control is NeonChrome) captionHeight += control.Height;
                int targetHeight = Math.Min(preferredHeight + captionHeight + Padding.Vertical, availableHeight);
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
            if (Directory.Exists(paths[2]) && !ServerLocator.IsLegacyInTreeLocation(paths[0], paths[2]))
                _serverFolder.Text = paths[2];
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
            _root.Padding = new Padding(14, 8, 14, 8);
            _root.BackColor = Color.Transparent;
            _root.ColumnCount = 1;
            _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _root.RowCount = SetupEdition.ServerOnly ? 8 : 10;
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
            _serverFolder.ReadOnly = false;
            _serverFolder.TextChanged += delegate { UpdateServerDestinationStatus(); };
            if (!SetupEdition.ServerOnly) _root.Controls.Add(CreateArchitectureRow(), 0, nextRow++);
            _root.Controls.Add(CreateSourcePanel(), 0, nextRow++);
            _root.Controls.Add(CreateUpdateNotificationPanel(), 0, nextRow++);

            var statusPanel = new TableLayoutPanel { BackColor = Color.Transparent, Padding = new Padding(5), Margin = new Padding(0, 2, 0, 10), Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 2 };
            statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            statusPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
            statusPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _progress.Dock = DockStyle.Fill;
            _progress.MinimumSize = new Size(0, 24);
            _progress.Style = ProgressBarStyle.Continuous;
            _status.AutoSize = true;
            _status.Dock = DockStyle.Top;
            _status.Padding = new Padding(2, 8, 2, 0);
            _status.ForeColor = Color.FromArgb(140, 220, 255);
            _status.Text = SetupEdition.ServerOnly ? "Choose the Visual Pinball and Server folders, then click Install / Update." : "Choose the three folders, then click Install / Update.";
            statusPanel.Controls.Add(_progress, 0, 0);
            statusPanel.Controls.Add(_status, 0, 1);
            _root.Controls.Add(statusPanel, 0, nextRow++);

            var buttons = new FlowLayoutPanel { BackColor = Color.Transparent, Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.RightToLeft, WrapContents = true, Padding = new Padding(0, 2, 0, 0) };
            var close = StyledButton("Close", 104);
            close.Click += delegate { Close(); };
            _installButton.Text = SetupEdition.ButtonText;
            StyleButton(_installButton, SetupEdition.ServerOnly ? 220 : 250);
            _installButton.MinimumSize = new Size(SetupEdition.ServerOnly ? 220 : 250, 38);
            _installButton.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            _installButton.BackColor = Color.FromArgb(25, 112, 68);
            _installButton.Click += InstallClicked;
            buttons.Controls.Add(close);
            buttons.Controls.Add(_installButton);
            _restoreButton.Text = "Restore Previous Setup";
            StyleButton(_restoreButton, 190);
            _restoreButton.Click += RestoreClicked;
            buttons.Controls.Add(_restoreButton);
            _root.Controls.Add(buttons, 0, nextRow);
        }

        private Control CreateHeader()
        {
            var header = new TableLayoutPanel { Dock = DockStyle.Top, Height = 100, BackColor = Color.Transparent, ColumnCount = 3, Margin = new Padding(0, 0, 0, 5), Padding = Padding.Empty };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            header.RowCount = 1;
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var logo = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent, Margin = Padding.Empty };
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("B2SProLogo.png"))
            {
                if (stream == null) throw new InvalidOperationException("The embedded B2S Pro logo is missing.");
                using (Image artwork = Image.FromStream(stream)) logo.Image = new Bitmap(artwork);
            }
            logo.Disposed += delegate { if (logo.Image != null) logo.Image.Dispose(); };
            header.Controls.Add(logo, 1, 0);
            return header;
        }
        private Control CreateIntro()
        {
            _intro.AutoSize = true;
            _intro.Dock = DockStyle.Top;
            _intro.Text = SetupEdition.ServerOnly
                ? "VPX Server only • your Designer and backglasses stay untouched"
                : "Designer + Server • verified packages • automatic backups";
            _intro.Font = new Font(Font, FontStyle.Bold);
            _intro.ForeColor = Color.FromArgb(230, 235, 245);
            _intro.Padding = new Padding(2, 8, 2, 2);
            return _intro;
        }

        private Control CreatePathRow(string title, TextBox textBox, EventHandler browse, string buttonText)
        {
            var card = new NeonCard();
            var panel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, RowCount = 3, BackColor = Color.Transparent };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94));
            var icon = new NeonGlyph { Kind = "folder", Dock = DockStyle.Fill, Margin = new Padding(0, 6, 12, 0) };
            panel.Controls.Add(icon, 0, 0); panel.SetRowSpan(icon, 3);
            bool server = Object.ReferenceEquals(textBox, _serverFolder);
            string heading = server ? "B2S Server folder" : Object.ReferenceEquals(textBox, _vpxFolder) ? "Visual Pinball folder" : "B2S Pro Designer folder";
            var label = new Label { Text = heading, AutoSize = true, ForeColor = Color.FromArgb(231, 242, 255), Font = new Font("Segoe UI", 10F, FontStyle.Bold), Margin = new Padding(0, 0, 0, 4) };
            var description = new Label { Text = server ? "Automatically detected • editable location" : Object.ReferenceEquals(textBox, _vpxFolder) ? "Select your Visual Pinball installation folder" : "Select the folder for B2S Pro Designer", AutoSize = true, ForeColor = Color.FromArgb(149, 176, 204), Margin = new Padding(0, 0, 0, 9) };
            panel.Controls.Add(label, 1, 0); panel.SetColumnSpan(label, 2);
            description.Visible = false; panel.Controls.Add(description, 1, 1); panel.SetColumnSpan(description, 2);
            textBox.Dock = DockStyle.Fill; textBox.Margin = new Padding(0, 4, 10, 4);
            textBox.BackColor = Color.FromArgb(24, 38, 56); textBox.ForeColor = Color.White;
            textBox.Font = new Font("Segoe UI", 10F); textBox.BorderStyle = BorderStyle.FixedSingle;
            var button = StyledButton(buttonText, 86); button.Dock = DockStyle.Fill; button.Margin = Padding.Empty; button.Click += browse;
            var inputFrame = new AngledInput { Dock = DockStyle.Fill, Height = 31, Margin = new Padding(0, 0, 8, 0) }; textBox.BorderStyle = BorderStyle.None; textBox.Margin = Padding.Empty; textBox.BackColor = Color.FromArgb(20, 35, 51); inputFrame.Controls.Add(textBox); panel.Controls.Add(inputFrame, 1, 2); panel.Controls.Add(button, 2, 2);
            card.Controls.Add(panel); return card;
        }

        private Control CreateArchitectureRow()
        {
            var card = new NeonCard();
            var panel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, BackColor = Color.Transparent };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30)); panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45)); panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            panel.Controls.Add(new NeonGlyph { Kind = "chip", Dock = DockStyle.Fill, MinimumSize = new Size(32, 32) }, 0, 0);
            panel.Controls.Add(new Label { Text = "Designer edition", AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI", 10F, FontStyle.Bold), Margin = new Padding(0, 5, 0, 0) }, 1, 0);
            _architecture.DropDownStyle = ComboBoxStyle.DropDownList; _architecture.Items.AddRange(new object[] { "64-bit (recommended)", "32-bit" });
            _architecture.SelectedIndex = Environment.Is64BitOperatingSystem ? 0 : 1;
            _architecture.Dock = DockStyle.Fill; _architecture.BackColor = Color.FromArgb(24, 38, 56); _architecture.ForeColor = Color.White; _architecture.Font = new Font("Segoe UI", 10F); _architecture.FlatStyle = FlatStyle.Flat;
            _architecture.DrawMode = DrawMode.OwnerDrawFixed;
            _architecture.DrawItem += delegate(object sender, DrawItemEventArgs e) { if (e.Index < 0) return; using (var brush = new SolidBrush(Color.FromArgb(24, 38, 56))) e.Graphics.FillRectangle(brush, e.Bounds); TextRenderer.DrawText(e.Graphics, _architecture.Items[e.Index].ToString(), _architecture.Font, e.Bounds, Color.White, TextFormatFlags.Left | TextFormatFlags.VerticalCenter); if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle(); };
            panel.Controls.Add(_architecture, 2, 0); card.Controls.Add(panel); return card;
        }

        private Control CreateSourcePanel()
        {
            var card = new NeonCard();
            var flow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, BackColor = Color.Transparent };
            flow.Controls.Add(new Label { Text = "Install source", AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI", 10F, FontStyle.Bold), Margin = new Padding(0, 0, 0, 8) });
            _localSource.Text = "Use local packages"; _localSource.AutoSize = true; _localSource.ForeColor = Color.White; _localSource.BackColor = Color.Transparent; _localSource.UseVisualStyleBackColor = false;
            _localSource.Enabled = _localPackages.AreAvailable(SetupEdition.ServerOnly);
            _localSource.Checked = _localSource.Enabled;
            _githubSource.Text = "Download from GitHub"; _githubSource.AutoSize = true; _githubSource.ForeColor = Color.White; _githubSource.BackColor = Color.Transparent; _githubSource.UseVisualStyleBackColor = false; _githubSource.Checked = !_localSource.Checked;
            _githubSource.Margin = new Padding(12, 1, 14, 4); _localSource.Margin = new Padding(0, 1, 0, 4);
            flow.Controls.Add(_githubSource); flow.Controls.Add(_localSource);
            card.Controls.Add(flow); return card;
        }

        private Control CreateUpdateNotificationPanel()
        {
            var card = new NeonCard();
            var panel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, BackColor = Color.Transparent };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.Controls.Add(new NeonGlyph { Kind = "chip", Dock = DockStyle.Fill, MinimumSize = new Size(32, 32) }, 0, 0);
            _disableAutomaticUpdateNotifications.Text = "Disable automatic update notifications";
            _disableAutomaticUpdateNotifications.AutoSize = true;
            _disableAutomaticUpdateNotifications.ForeColor = Color.White;
            _disableAutomaticUpdateNotifications.BackColor = Color.Transparent;
            _disableAutomaticUpdateNotifications.UseVisualStyleBackColor = false;
            _disableAutomaticUpdateNotifications.Margin = new Padding(0, 5, 0, 1);
            panel.Controls.Add(_disableAutomaticUpdateNotifications, 1, 0);
            var description = new Label
            {
                Text = "Manual Check for Updates remains available.",
                AutoSize = true,
                ForeColor = Color.FromArgb(149, 176, 204),
                Margin = new Padding(0, 1, 0, 3)
            };
            panel.Controls.Add(description, 1, 1);
            card.Controls.Add(panel);
            return card;
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
            string serverSuggestion = ServerLocator.FindOrSuggest(vpxRoot);
            bool replaceServerFolder = ServerLocator.ShouldReplaceSuggestion(_serverFolder.Text, _suggestedServerFolder);
            _suggestedServerFolder = serverSuggestion;
            if (replaceServerFolder) _serverFolder.Text = serverSuggestion;
            UpdateServerDestinationStatus();
        }

        private void UpdateServerDestinationStatus()
        {
            string serverFolder = _serverFolder.Text.Trim();
            if (String.IsNullOrWhiteSpace(serverFolder))
            {
                _status.Text = "Choose the B2S Server folder.";
                return;
            }
            if (File.Exists(Path.Combine(serverFolder, "B2SBackglassServer.dll")))
                _status.Text = "Existing B2S Server detected: " + serverFolder;
            else
                _status.Text = "Fresh installation: the complete server will be installed in " + serverFolder;
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
                NeonMessage.Show(this, validation, SetupEdition.Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                    var plan = package.CreatePlan(SetupEdition.ServerOnly ? null : _designerFolder.Text.Trim(), _serverFolder.Text.Trim(), _vpxFolder.Text.Trim(), arch, !SetupEdition.ServerOnly);
                    if (!ConfirmReplacement(plan))
                    {
                        SetBusy(false, "Installation cancelled. No files were changed.");
                        return;
                    }

                    SetBusy(true, "Backing up and verifying the server files this update replaces...");
                    string recovery = await Task.Run(delegate { return plan.CreateRecovery(); });
                    SetBusy(true, "Backup verified. Installing files...");
                    InstallResult result = await Task.Run(delegate { return plan.Execute(true, !SetupEdition.ServerOnly, !SetupEdition.ServerOnly); });
                    SetBusy(false, SetupEdition.Product + " installation completed successfully.");
                    string message = result.BuildSummary();
                    message += "\r\nServer recovery backup: " + recovery + "\r\nUse Restore Previous Setup to restore the previous server files. Existing settings and plugins stay in place.";
                    try
                    {
                        SetupPaths.Save(_vpxFolder.Text.Trim(), SetupEdition.ServerOnly ? null : _designerFolder.Text.Trim(), _serverFolder.Text.Trim());
                        UpdateNotificationPreference.Save(_disableAutomaticUpdateNotifications.Checked);
                    }
                    catch (Exception ex)
                    {
                        message += Environment.NewLine + "Installation paths could not be remembered: " + ex.Message;
                    }
                    NeonMessage.Show(this, message, SetupEdition.Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Close();
                }
            }
            catch (Exception ex)
            {
                SetBusy(false, "Installation stopped. No unprotected settings were intentionally replaced.");
                NeonMessage.Show(this, ex.Message, SetupEdition.Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        private async void RestoreClicked(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Choose a dated B2S Pro recovery backup";
                dialog.Filter = "B2S Pro recovery (Recovery.json)|Recovery.json";
                if (Directory.Exists(FolderRecovery.DefaultRoot)) dialog.InitialDirectory = FolderRecovery.DefaultRoot;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    SetBusy(true, "Verifying the selected backup...");
                    RecoveryRecord record = await Task.Run(delegate { return FolderRecovery.Read(dialog.FileName); });
                    SetBusy(false, "Backup verified. Review the original locations before restoring.");
                    string message = "Restore the saved files and settings from " + record.Created + "?\r\n\r\n" + String.Join("\r\n", record.Roots.ToArray()) +
                        "\r\n\r\nClose VPX and all backglasses first. Only files covered by this backup will be restored. Their current copies will be preserved first. Files recorded as added by the update will be removed; other files are kept.\r\n\r\nWindows registration and shortcuts are not restored.";
                    if (NeonMessage.Show(this, message, "Restore Previous Setup", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                    SetBusy(true, "Verifying backup and preserving current files before restore...");
                    string saved = await Task.Run(delegate { return FolderRecovery.Restore(dialog.FileName, FolderRecovery.DefaultRoot); });
                    SetBusy(false, "Previous folder files and settings restored.");
                    NeonMessage.Show(this, "Restore completed.\r\nCurrent files were preserved here:\r\n" + saved, "Restore Previous Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    SetBusy(false, "Restore stopped. See the message for details.");
                    NeonMessage.Show(this, ex.Message, "Restore Previous Setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
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
            message.AppendLine("Only existing server files replaced or removed by this update will be backed up and verified in " + FolderRecovery.DefaultRoot + ". Existing settings, plugins, Designer projects, tables and old backups are not copied. Windows registration and shortcuts are not backed up.");
            return NeonMessage.Show(this, message.ToString(), "Existing installation found", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
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
            _operationRunning = busy;
            _root.Enabled = !busy;
            _restoreButton.Enabled = !busy;
            _installButton.Enabled = !busy;
            _status.Text = text;
            if (!busy)
            {
                _progress.Style = ProgressBarStyle.Continuous;
                _progress.Value = 0;
            }
            else { _progress.Value = 0; _progress.Style = ProgressBarStyle.Marquee; }
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
            var button = new NeonButton { Text = text };
            StyleButton(button, width);
            return button;
        }

        private static void StyleButton(Button button, int width)
        {
            button.AutoSize = true;
            button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            button.MinimumSize = new Size(width, 31);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(55, 145, 255);
            button.BackColor = Color.FromArgb(28, 48, 82);
            button.ForeColor = Color.White;
        }
    }

    internal static class NeonDrawing
    {
        public static GraphicsPath Cut(RectangleF r, float cut)
        {
            var path = new GraphicsPath(); cut = Math.Min(cut, r.Height / 2);
            path.AddPolygon(new PointF[] { new PointF(r.X + cut,r.Y),new PointF(r.Right-cut,r.Y),new PointF(r.Right,r.Y+cut),new PointF(r.Right,r.Bottom-cut),new PointF(r.Right-cut,r.Bottom),new PointF(r.X+cut,r.Bottom),new PointF(r.X,r.Bottom-cut),new PointF(r.X,r.Y+cut) }); return path;
        }
        public static void ShapeWindow(Form form)
        {
            if (form.Width < 60 || form.Height < 60) return;
            using(var path=new GraphicsPath()) { path.AddPolygon(new PointF[]{new PointF(12,0),new PointF(form.Width-12,0),new PointF(form.Width,12),new PointF(form.Width,form.Height-40),new PointF(form.Width-40,form.Height),new PointF(40,form.Height),new PointF(0,form.Height-40),new PointF(0,12)}); var previous=form.Region; form.Region=new Region(path); if(previous!=null)previous.Dispose(); }
        }
        public static GraphicsPath Round(RectangleF r, float radius)
        {
            var path = new GraphicsPath(); float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            path.AddArc(r.Left, r.Top, d, d, 180, 90); path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
        }
    }
    internal sealed class NeonBackdrop : Panel
    {
        public NeonBackdrop() { DoubleBuffered = true; SetStyle(ControlStyles.ResizeRedraw, true); }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Color.FromArgb(6, 14, 25));
            using (var pen = new Pen(Color.FromArgb(10, 24, 37)))
            { for (int y = 30; y < Height; y += 28) { e.Graphics.DrawLine(pen, 0, y, 12, y); e.Graphics.DrawLine(pen, Width - 12, y, Width, y); } }
            using (var pen = new Pen(Color.FromArgb(36, 132, 169), 1)) e.Graphics.DrawLines(pen,new Point[]{new Point(3,3),new Point(3,Height-40),new Point(40,Height-4),new Point(Width-40,Height-4),new Point(Width-4,Height-40),new Point(Width-4,3)});
        }
    }
    internal sealed class NeonCard : Panel
    {
        public NeonCard() { DoubleBuffered = true; AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink; Dock = DockStyle.Top; Padding = new Padding(10, 5, 10, 5); Margin = new Padding(0, 0, 0, 5); BackColor = Color.FromArgb(12, 24, 38); SetStyle(ControlStyles.ResizeRedraw, true); }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Color.FromArgb(6, 14, 25));
            using (var pen = new Pen(Color.FromArgb(25, 64, 85))) e.Graphics.DrawLine(pen, 12, Height - 1, Math.Max(12, Width - 12), Height - 1);
        }
    }
    internal sealed class AngledInput : Panel
    {
        public AngledInput() { DoubleBuffered=true; Padding=new Padding(9,5,9,5); BackColor=Color.FromArgb(20,35,51); MinimumSize=new Size(0,31); }
        protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); if(Width>20&&Height>10) using(var path=NeonDrawing.Cut(new RectangleF(0,0,Width,Height),8)) { var old=Region;Region=new Region(path);if(old!=null)old.Dispose(); } }
        protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var path=NeonDrawing.Cut(new RectangleF(1,1,Width-3,Height-3),8))using(var pen=new Pen(Color.FromArgb(40,133,169)))e.Graphics.DrawPath(pen,path); }
    }
    internal sealed class SetupWordmark : Control
    {
        public SetupWordmark(){DoubleBuffered=true;BackColor=Color.FromArgb(6,14,25);}
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(var family=new FontFamily("Segoe UI"))using(var letters=new GraphicsPath())
            { letters.AddString("B2S",family,(int)(FontStyle.Bold|FontStyle.Italic),43,new PointF(0,-7),StringFormat.GenericDefault);
              using(var shine=new LinearGradientBrush(new Rectangle(0,0,130,46),Color.White,Color.FromArgb(82,112,138),90F))using(var outline=new Pen(Color.FromArgb(150,205,235),.8F)){e.Graphics.FillPath(shine,letters);e.Graphics.DrawPath(outline,letters);} }
            using(var font=new Font("Segoe UI",18,FontStyle.Bold|FontStyle.Italic))using(var brush=new LinearGradientBrush(new Rectangle(20,34,98,30),Color.FromArgb(255,181,45),Color.FromArgb(255,34,32),90F))e.Graphics.DrawString("PRO",font,brush,new PointF(43,32));
            using(var pen=new Pen(Color.FromArgb(0,207,246),1.5F))e.Graphics.DrawLine(pen,6,62,123,62);
            using(var ball=new LinearGradientBrush(new Rectangle(113,3,24,24),Color.White,Color.FromArgb(28,49,74),45F))e.Graphics.FillEllipse(ball,113,3,24,24);
            using(var pen=new Pen(Color.FromArgb(149,196,231)))e.Graphics.DrawEllipse(pen,113,3,24,24);
        }
    }
    internal sealed class NeonGlyph : Control
    {
        public string Kind = "folder";
        public NeonGlyph() { SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true); BackColor = Color.Transparent; }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; float size = Math.Min(32, Math.Min(Width - 4, Height - 4)); if (size < 8) return;
            float x = (Width - size) / 2, y = (Height - size) / 2;
            using (var pen = new Pen(Color.FromArgb(0, 224, 247), 2))
            { if (Kind == "folder") { e.Graphics.DrawLines(pen, new PointF[] { new PointF(x, y + 7), new PointF(x, y + 2), new PointF(x + size * .4F, y + 2), new PointF(x + size * .55F, y + 7), new PointF(x + size, y + 7), new PointF(x + size, y + size), new PointF(x, y + size), new PointF(x, y + 7) }); }
              else { e.Graphics.DrawRectangle(pen, x + 5, y + 5, size - 10, size - 10); for (int i = 10; i < size - 5; i += 7) { e.Graphics.DrawLine(pen, x + i, y, x + i, y + 5); e.Graphics.DrawLine(pen, x + i, y + size - 5, x + i, y + size); e.Graphics.DrawLine(pen, x, y + i, x + 5, y + i); e.Graphics.DrawLine(pen, x + size - 5, y + i, x + size, y + i); } } }
        }
    }
    internal sealed class NeonButton : Button
    {
        public bool Primary;
        private bool hover;
        public NeonButton() { DoubleBuffered = true; }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent == null || Parent.BackColor.A < 255 ? Color.FromArgb(6, 14, 25) : Parent.BackColor);
            if (Width < 5 || Height < 5) return;
            Color top = Primary && Enabled ? Color.FromArgb(56, 232, 250) : Color.FromArgb(28, 49, 70);
            Color bottom = Primary && Enabled ? Color.FromArgb(0, 166, 206) : Color.FromArgb(11, 23, 39);
            using (var shape = NeonDrawing.Cut(new RectangleF(2, 2, Width - 5, Height - 5), 10))
            using (var brush = new LinearGradientBrush(ClientRectangle, top, bottom, 90F))
            using (var border = new Pen(!Enabled ? Color.FromArgb(55, 68, 80) : hover || Primary ? Color.FromArgb(100, 246, 255) : Color.FromArgb(0, 193, 226), hover ? 2 : 1))
            { e.Graphics.FillPath(brush, shape); e.Graphics.DrawPath(border, shape); }
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, !Enabled ? Color.Gray : Primary ? Color.FromArgb(2, 27, 40) : Color.FromArgb(210, 245, 255), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -6, -6));
        }
    }
    internal sealed class NeonProgress : Control
    {
        private int value, phase;
        private ProgressBarStyle style;
        private readonly Timer pulse = new Timer { Interval = 40 };
        public int Value { get { return value; } set { this.value = Math.Max(0, Math.Min(100, value)); Invalidate(); } }
        public ProgressBarStyle Style { get { return style; } set { style = value; pulse.Enabled = value == ProgressBarStyle.Marquee; Invalidate(); } }
        public NeonProgress() { DoubleBuffered = true; BackColor = Color.FromArgb(6, 14, 25); pulse.Tick += delegate { phase = (phase + 3) % 120; Invalidate(); }; }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 70 || Height < 12) return; e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var track = new RectangleF(5, 5, Width - 58, Height - 10);
            using (var shape = NeonDrawing.Round(track, track.Height / 2))
            { using (var glow = new Pen(Color.FromArgb(95, 169, 59, 246), 5)) e.Graphics.DrawPath(glow, shape); using (var back = new SolidBrush(Color.FromArgb(10, 28, 44))) e.Graphics.FillPath(back, shape);
              if (value > 0 || style == ProgressBarStyle.Marquee) { var old = e.Graphics.Save(); e.Graphics.SetClip(shape); float width = style == ProgressBarStyle.Marquee ? track.Width * .23F : track.Width * value / 100F; float x = style == ProgressBarStyle.Marquee ? track.X + track.Width * phase / 100F - width : track.X; using (var fill = new LinearGradientBrush(ClientRectangle, Color.FromArgb(50, 235, 255), Color.FromArgb(0, 147, 185), 90F)) e.Graphics.FillRectangle(fill, x, track.Y, width, track.Height); e.Graphics.Restore(old); }
              using (var border = new Pen(Color.FromArgb(0, 232, 252), 2)) e.Graphics.DrawPath(border, shape); }
            TextRenderer.DrawText(e.Graphics, value + "%", Font, new Rectangle(Width - 47, 0, 47, Height), Color.FromArgb(185, 235, 255), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        protected override void Dispose(bool disposing) { if (disposing) pulse.Dispose(); base.Dispose(disposing); }
    }

    internal sealed class NeonChrome : Panel
    {
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, IntPtr l);
        public NeonChrome(Form owner)
        {
            BackColor = Color.FromArgb(9, 22, 37); Padding = new Padding(12, 0, 2, 0);
            var close = new NeonButton { Text = "×", Dock = DockStyle.Right, Width = 36, AutoSize = false, Font = new Font("Segoe UI", 13F) };
            close.Click += delegate { owner.Close(); };
            var title = new Label { Text = owner.Text, UseMnemonic = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(145, 189, 215) };
            MouseEventHandler drag = delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(owner.Handle, 0xA1, new IntPtr(2), IntPtr.Zero); } };
            title.MouseDown += drag; MouseDown += drag; Controls.Add(title); Controls.Add(close);
            if (owner is InstallerForm)
            {
                var minimize = new NeonButton { Text = "—", Dock = DockStyle.Right, Width = 36, AutoSize = false };
                minimize.Click += delegate { owner.WindowState = FormWindowState.Minimized; };
                Controls.Add(minimize);
                Controls.SetChildIndex(minimize, 1);
            }
        }
    }
    internal sealed class NeonMessage : Form
    {
        internal NeonMessage(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            Text = caption; BackColor = Color.FromArgb(6, 14, 25); ForeColor = Color.White; Font = new Font("Segoe UI", 10F);
            FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false; Padding = new Padding(4); AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(500, 340);
            var chrome = new NeonChrome(this) { Dock = DockStyle.Top, Height = 32 };
            var body = new NeonBackdrop { Dock = DockStyle.Fill, Padding = new Padding(24, 18, 24, 18) };
            var buttonsPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 58, FlowDirection = FlowDirection.RightToLeft, BackColor = Color.Transparent, Padding = new Padding(0, 6, 0, 0) };
            bool question = buttons == MessageBoxButtons.YesNo;
            if (question)
            {
                var no = new NeonButton { Text = "No", Width = 112, Height = 42, DialogResult = DialogResult.No };
                var yes = new NeonButton { Text = "Yes", Width = 112, Height = 42, DialogResult = DialogResult.Yes, Primary = true };
                buttonsPanel.Controls.Add(no); buttonsPanel.Controls.Add(yes); AcceptButton = yes; CancelButton = no;
                FormClosing += delegate { if (DialogResult == DialogResult.None || DialogResult == DialogResult.Cancel) DialogResult = DialogResult.No; };
            }
            else
            {
                var ok = new NeonButton { Text = "OK", Width = 112, Height = 42, DialogResult = DialogResult.OK, Primary = true };
                buttonsPanel.Controls.Add(ok); AcceptButton = ok; CancelButton = ok;
            }
            string heading = icon == MessageBoxIcon.Information ? "Installation complete" : icon == MessageBoxIcon.Error ? "Installation could not finish" : question ? "Confirm replacement" : "Check installation settings";
            var title = new Label { Text = heading, Dock = DockStyle.Top, Height = 48, Font = new Font("Segoe UI", 18F, FontStyle.Bold), ForeColor = icon == MessageBoxIcon.Error ? Color.FromArgb(255, 128, 145) : Color.FromArgb(30, 229, 249), BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleLeft };
            var message = new TextBox { Text = text.Replace("\r\n", "\n").Replace("\n", Environment.NewLine), Dock = DockStyle.Fill, Multiline = true, WordWrap = true, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = Color.FromArgb(12, 24, 38), ForeColor = Color.FromArgb(220, 237, 250), Font = Font, TabStop = true, ScrollBars = ScrollBars.Vertical };
            Shown += delegate { message.SelectionStart = 0; message.SelectionLength = 0; if (AcceptButton is Control) ((Control)AcceptButton).Focus(); };
            var card = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), BackColor = message.BackColor };
            card.Controls.Add(message); body.Controls.Add(card); body.Controls.Add(title); body.Controls.Add(buttonsPanel); Controls.Add(body); Controls.Add(chrome);
            SizeChanged += delegate { NeonDrawing.ShapeWindow(this); };
            NeonDrawing.ShapeWindow(this);
        }
        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        { using (var dialog = new NeonMessage(text, caption, buttons, icon)) { Rectangle area = owner is Control ? Screen.FromControl((Control)owner).WorkingArea : Screen.PrimaryScreen.WorkingArea; dialog.ClientSize = new Size(Math.Min(500, Math.Max(320, area.Width - 32)), Math.Min(360, Math.Max(260, area.Height - 32))); return dialog.ShowDialog(owner); } }
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
                client.DefaultRequestHeaders.UserAgent.ParseAdd(serverOnly ? "B2S-Server-Updater/3.0.10" : "B2S-Pro-Installer/1.0.9");
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
        private readonly string _designerDigest;
        private readonly string _serverDigest;
        private readonly ZipArchive _designerArchive;
        private readonly ZipArchive _serverArchive;

        public ReleaseBundle(string designerPath, string serverPath)
        {
            if (String.IsNullOrWhiteSpace(serverPath)) throw new InvalidDataException("The Server package path is missing. Nothing was installed.");
            _designerDigest = String.IsNullOrWhiteSpace(designerPath) ? null : Hashing.Sha256(designerPath);
            _serverDigest = Hashing.Sha256(serverPath);
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
                if (HasCommonDesignerPayload())
                {
                    Require(_designerArchive, "common/B2SProHelp.html");
                    Require(_designerArchive, "common/B2SProHelp.chm");
                    Require(_designerArchive, "common/IDTester.B2SPro");
                    Require(_designerArchive, "common/InfinityBulb.png");
                    Require(_designerArchive, "common/OcrTools/python/python.exe");
                    Require(_designerArchive, "common/OcrTools/node/node.exe");
                    Require(_designerArchive, "common/OcrTools/eng.traineddata");
                }
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

        public InstallPlan CreatePlan(string designer, string server, string vpx, string arch, bool installDesigner)
        {
            return new InstallPlan(_designerArchive, _serverArchive, designer, server, vpx, arch, installDesigner, GetServerPrefix(), _designerDigest, _serverDigest);
        }

        internal InstallPlan CreatePlanForTest(string designer, string server, string vpx, string arch, bool installDesigner, Func<string, bool> serverIdentityReader)
        {
            return new InstallPlan(_designerArchive, _serverArchive, designer, server, vpx, arch, installDesigner, GetServerPrefix(), _designerDigest, _serverDigest, serverIdentityReader);
        }

        private string GetServerPrefix()
        {
            return _serverArchive.GetEntry("Runtime/B2SServer/B2SBackglassServer.dll") != null ? "Runtime/B2SServer/" : String.Empty;
        }

        private bool HasCommonDesignerPayload()
        {
            return _designerArchive != null && _designerArchive.GetEntry("common/OcrTools/python/python.exe") != null;
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

    internal sealed class RecoveryFile
    {
        public int Root { get; set; }
        public string Relative { get; set; }
        public string Sha256 { get; set; }
        public int Attributes { get; set; }
        public long ModifiedUtc { get; set; }
    }

    internal sealed class RecoveryRecord
    {
        public int Format { get; set; }
        public string Created { get; set; }
        public List<string> Roots { get; set; }
        public List<RecoveryFile> Files { get; set; }
        public List<string> AddedFiles { get; set; }
        public List<string> Directories { get; set; }
    }

    // Folder recovery is independent of package installation and never changes registration.
    internal static class FolderRecovery
    {
        public static string DefaultRoot { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "B2SPro", "Recovery"); } }
        private static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }; }
        private static string Full(string path) { return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar); }
        private static bool Within(string path, string root) { return Full(path).StartsWith(Full(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase); }
        private static bool Covered(string path, List<string> roots) { foreach (string root in roots) if (Within(path, root)) return true; return false; }

        private static void CheckPath(string path)
        {
            string full = Path.GetFullPath(path);
            for (string current = full; !String.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            {
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("A linked folder or file cannot be safely backed up or restored: " + current);
                if (String.Equals(current, Path.GetPathRoot(current), StringComparison.OrdinalIgnoreCase)) break;
            }
        }

        private static void EnsureClosed(List<string> roots)
        {
            foreach (Process process in Process.GetProcesses())
            {
                using (process)
                {
                    string name = process.ProcessName;
                    if (name.StartsWith("VPinballX", StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(name, "B2SPro", StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(name, "B2SBackglassServerEXE", StringComparison.OrdinalIgnoreCase))
                    {
                        // VPX can host the server DLL even when its executable lives outside the server folder.
                        bool relevant = name.StartsWith("VPinballX", StringComparison.OrdinalIgnoreCase);
                        try { relevant = relevant || Covered(process.MainModule.FileName, roots); }
                        catch { relevant = true; }
                        if (relevant) throw new IOException("Close VPX, B2S Pro Designer and running backglasses before backing up or restoring.");
                    }
                }
            }
        }

        private static void Scan(string folder, List<string> files, List<string> directories)
        {
            CheckPath(folder);
            directories.Add(folder);
            foreach (string file in Directory.GetFiles(folder)) { CheckPath(file); files.Add(file); }
            foreach (string child in Directory.GetDirectories(folder)) Scan(child, files, directories);
        }

        public static string Create(List<string> requestedRoots, List<string> additions, string backupRoot)
        {
            return CreateSelected(requestedRoots, additions, backupRoot, null);
        }

        public static string CreateSelected(List<string> requestedRoots, List<string> additions, string backupRoot, List<string> selectedFiles)
        {
            EnsureClosed(requestedRoots);
            var roots = new List<string>();
            foreach (string value in requestedRoots)
            {
                string root = Full(value);
                if (root.Length <= Path.GetPathRoot(root).Length || root.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.Windows), StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Choose an application folder, not a drive or Windows folder.");
                CheckPath(root);
                bool nested = false;
                foreach (string existing in roots) if (String.Equals(root, existing, StringComparison.OrdinalIgnoreCase) || Within(root, existing)) nested = true;
                if (nested) continue;
                roots.RemoveAll(delegate(string existing) { return Within(existing, root); });
                roots.Add(root);
            }
            if (roots.Count == 0) throw new IOException("No installation folders were supplied.");
            foreach (string root in roots)
                if (String.Equals(Full(backupRoot), root, StringComparison.OrdinalIgnoreCase) || Within(backupRoot, root) || Within(root, backupRoot))
                    throw new IOException("The recovery folder must be separate from the installation folders: " + backupRoot);
            CheckPath(backupRoot);
            var record = new RecoveryRecord { Format = 1, Created = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), Roots = roots,
                Files = new List<RecoveryFile>(), AddedFiles = new List<string>(), Directories = new List<string>() };
            foreach (string addition in additions)
            {
                if (!Covered(addition, roots)) throw new IOException("An installation file is outside the backup folders.");
                CheckPath(addition);
                if (!File.Exists(addition)) record.AddedFiles.Add(Full(addition));
            }
            var files = new List<string>();
            if (selectedFiles == null)
            {
                foreach (string root in roots) if (Directory.Exists(root)) Scan(root, files, record.Directories);
            }
            else
            {
                var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string value in selectedFiles)
                {
                    string file = Full(value);
                    if (!Covered(file, roots)) throw new IOException("A recovery file is outside the server folder.");
                    CheckPath(file);
                    if (File.Exists(file) && unique.Add(file)) files.Add(file);
                }
            }
            long bytes = 0;
            foreach (string file in files) bytes = checked(bytes + new FileInfo(file).Length);
            Directory.CreateDirectory(backupRoot);
            var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(backupRoot)));
            if (drive.AvailableFreeSpace < bytes + 64L * 1024 * 1024) throw new IOException("Not enough free space for the complete backup. No installation files were changed.");
            string destination = Path.Combine(backupRoot, DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(destination);
            foreach (string file in files)
            {
                int index = roots.FindIndex(delegate(string root) { return Within(file, root); });
                string relative = file.Substring(roots[index].Length + 1);
                string copy = Path.Combine(destination, "Files", index.ToString(), relative);
                Directory.CreateDirectory(Path.GetDirectoryName(copy));
                // Deny writers during the copy and hash check; a changing file stops installation.
                using (var locked = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    File.Copy(file, copy, false);
                    string hash = Hashing.Sha256(file);
                    if (!String.Equals(hash, Hashing.Sha256(copy), StringComparison.OrdinalIgnoreCase)) throw new IOException("Backup verification failed: " + file);
                    record.Files.Add(new RecoveryFile { Root = index, Relative = relative, Sha256 = hash,
                        Attributes = (int)File.GetAttributes(file), ModifiedUtc = File.GetLastWriteTimeUtc(file).Ticks });
                }
            }
            string manifest = Path.Combine(destination, "Recovery.json");
            File.WriteAllText(manifest, Serializer().Serialize(record), Encoding.UTF8);
            File.WriteAllText(Path.Combine(destination, "README.txt"), "Open either B2S setup and choose Restore Previous Setup, then select Recovery.json.\r\nThis is a folder backup, not a Windows registration or shortcut backup.\r\nOriginal locations:\r\n" + String.Join("\r\n", roots.ToArray()));
            Read(manifest);
            return manifest;
        }

        private static string Target(RecoveryRecord record, RecoveryFile file)
        {
            if (file.Root < 0 || file.Root >= record.Roots.Count || String.IsNullOrEmpty(file.Relative) || Path.IsPathRooted(file.Relative)) throw new InvalidDataException("Invalid backup file path.");
            string target = Path.GetFullPath(Path.Combine(record.Roots[file.Root], file.Relative));
            if (!Within(target, record.Roots[file.Root])) throw new InvalidDataException("Backup path escapes its original folder.");
            CheckPath(target);
            return target;
        }

        public static RecoveryRecord Read(string manifest)
        {
            CheckPath(manifest);
            var record = Serializer().Deserialize<RecoveryRecord>(File.ReadAllText(manifest));
            if (record == null || record.Format != 1 || record.Roots == null || record.Roots.Count == 0 || record.Files == null || record.AddedFiles == null || record.Directories == null)
                throw new InvalidDataException("This is not a complete B2S Pro recovery backup.");
            foreach (string root in record.Roots)
            {
                if (!Path.IsPathRooted(root) || Full(root).Length <= Path.GetPathRoot(root).Length) throw new InvalidDataException("Invalid original folder.");
                CheckPath(root);
                if (Within(manifest, root)) throw new InvalidDataException("Recovery backup must be outside the original folders.");
            }
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (RecoveryFile file in record.Files)
            {
                if (!names.Add(Target(record, file))) throw new InvalidDataException("Duplicate backup file.");
                string stored = Path.Combine(Path.GetDirectoryName(manifest), "Files", file.Root.ToString(), file.Relative);
                CheckPath(stored);
                if (!String.Equals(Hashing.Sha256(stored), file.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Backup is damaged or incomplete: " + stored);
            }
            foreach (string addition in record.AddedFiles)
            {
                if (!Covered(addition, record.Roots) || names.Contains(Full(addition))) throw new InvalidDataException("Invalid added-file record.");
                CheckPath(addition);
            }
            foreach (string directory in record.Directories)
            {
                if (!record.Roots.Contains(directory) && !Covered(directory, record.Roots)) throw new InvalidDataException("Invalid backup directory.");
                CheckPath(directory);
            }
            return record;
        }

        public static string Restore(string manifest, string backupRoot)
        {
            RecoveryRecord record = Read(manifest);
            EnsureClosed(record.Roots);
            var targets = new List<string>(record.AddedFiles);
            var absent = new List<string>();
            foreach (RecoveryFile file in record.Files)
            {
                string target = Target(record, file);
                targets.Add(target);
                if (!File.Exists(target)) absent.Add(target);
            }
            foreach (string target in targets)
                if (File.Exists(target)) using (var test = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.None)) { }
            string undo = CreateSelected(record.Roots, absent, backupRoot, targets);
            try { Apply(record, manifest); }
            catch (Exception ex)
            {
                try { Apply(Read(undo), undo); }
                catch (Exception recovery) { throw new IOException("Restore failed: " + ex.Message + "\r\nAutomatic recovery also failed: " + recovery.Message + "\r\nYour pre-restore backup is: " + undo); }
                throw new IOException("Restore failed; the pre-restore files were put back. Backup: " + undo + "\r\n" + ex.Message);
            }
            return undo;
        }

        private static void Apply(RecoveryRecord record, string manifest)
        {
            foreach (string directory in record.Directories) Directory.CreateDirectory(directory);
            foreach (RecoveryFile file in record.Files)
            {
                string target = Target(record, file);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                if (File.Exists(target)) File.SetAttributes(target, FileAttributes.Normal);
                File.Copy(Path.Combine(Path.GetDirectoryName(manifest), "Files", file.Root.ToString(), file.Relative), target, true);
                if (!String.Equals(Hashing.Sha256(target), file.Sha256, StringComparison.OrdinalIgnoreCase)) throw new IOException("Restored file verification failed: " + target);
                File.SetAttributes(target, FileAttributes.Normal);
                File.SetLastWriteTimeUtc(target, new DateTime(file.ModifiedUtc, DateTimeKind.Utc));
                File.SetAttributes(target, (FileAttributes)file.Attributes);
            }
            foreach (string addition in record.AddedFiles)
                if (File.Exists(addition)) { File.SetAttributes(addition, FileAttributes.Normal); File.Delete(addition); }
        }
    }

    internal static class ConflictingServerCleanup
    {
        // These runtime components can compete with the selected server when another B2S Server copy is present.
        // User settings, documentation, templates, Plugins and Plugins64 are intentionally excluded.
        internal static readonly string[] RuntimeFiles =
        {
            "B2SBackglassServer.dll",
            "B2SBackglassServerEXE.exe",
            "B2SBackglassServerEXE.exe.config",
            "B2SBackglassServerRegisterApp.exe",
            "B2S_SetUp.exe",
            "B2S_SetUp.exe.config",
            "B2S_ScreenResIdentifier.exe",
            "B2S_ScreenResIdentifier.exe.config",
            "B2SServerPluginInterface.dll",
            "B2SWindowPunch.exe",
            "B2SInit.cmd"
        };

        private static bool HasB2SServerIdentity(string serverDll)
        {
            FileVersionInfo identity = FileVersionInfo.GetVersionInfo(serverDll);
            return String.Equals(identity.ProductName, "B2S Backglass Server", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(identity.FileDescription, "B2S Backglass Server", StringComparison.OrdinalIgnoreCase);
        }

        internal static List<string> Find(string vpxFolder, string selectedServerFolder, Func<string, bool> identityReader = null)
        {
            var files = new List<string>();
            if (String.IsNullOrWhiteSpace(vpxFolder) || !Directory.Exists(vpxFolder)) return files;
            string vpx = Path.GetFullPath(vpxFolder).TrimEnd(Path.DirectorySeparatorChar);
            string selected = Path.GetFullPath(selectedServerFolder).TrimEnd(Path.DirectorySeparatorChar);
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pending = new Stack<string>();
            pending.Push(vpx);
            while (pending.Count > 0)
            {
                string folder = Path.GetFullPath(pending.Pop()).TrimEnd(Path.DirectorySeparatorChar);
                if (!visited.Add(folder) || String.Equals(folder, selected, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(folder)) continue;
                try
                {
                    foreach (string child in Directory.GetDirectories(folder))
                    {
                        string fullChild = Path.GetFullPath(child).TrimEnd(Path.DirectorySeparatorChar);
                        if (String.Equals(fullChild, selected, StringComparison.OrdinalIgnoreCase) || IsProtectedStorageFolder(fullChild)) continue;
                        try
                        {
                            if ((File.GetAttributes(fullChild) & FileAttributes.ReparsePoint) == 0) pending.Push(fullChild);
                        }
                        catch (IOException) { }
                        catch (UnauthorizedAccessException) { }
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                string serverDll = Path.Combine(folder, "B2SBackglassServer.dll");
                if (!File.Exists(serverDll)) continue;
                bool isB2SServer;
                try { isB2SServer = identityReader == null ? HasB2SServerIdentity(serverDll) : identityReader(serverDll); }
                catch (IOException) { continue; }
                catch (UnauthorizedAccessException) { continue; }
                if (!isB2SServer) continue;
                foreach (string name in RuntimeFiles)
                {
                    string path = Path.Combine(folder, name);
                    if (File.Exists(path)) files.Add(path);
                }
            }
            return files;
        }

        private static bool IsProtectedStorageFolder(string folder)
        {
            string name = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar));
            return String.Equals(name, "B2SPro-Backups", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(name, "Backup", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(name, "Backups", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(name, "Recovery", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(name, "AutoRecovery", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(name, "Archive", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(name, "Archives", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(name, "Quarantine", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal sealed class InstallPlan
    {
        private readonly ZipArchive _designerArchive;
        private readonly ZipArchive _serverArchive;
        private readonly string _designer;
        private readonly string _server;
        private readonly string _vpx;
        private readonly string _arch;
        private readonly bool _installDesigner;
        private readonly string _serverPrefix;
        private readonly bool _freshServer;
        private readonly string _designerDigest;
        private readonly string _serverDigest;
        private readonly List<CopyItem> _items = new List<CopyItem>();
        private readonly List<string> _legacyRuntimeFiles;
        private static readonly string[] RetiredServerFiles =
        {
            "Changelog.txt",
            "B2S-native-rotation-changelog.txt",
            "B2SNativeRotationDiagnostic.log",
            "B2SNativeRotationDiagnostic.txt"
        };
        public readonly List<string> ExistingProgramFiles = new List<string>();

        public string CreateRecovery(string backupRoot = null)
        {
            var roots = new List<string>();
            roots.Add(_server);
            foreach (string file in _legacyRuntimeFiles)
            {
                string folder = Path.GetDirectoryName(file);
                bool present = false;
                foreach (string root in roots)
                    if (String.Equals(root, folder, StringComparison.OrdinalIgnoreCase)) { present = true; break; }
                if (!present) roots.Add(folder);
            }
            var additions = new List<string>();
            var selected = new List<string>();
            foreach (CopyItem item in _items)
            {
                if (item.IsDesigner) continue;
                // Match Execute: existing protected settings/plugins are not written.
                if (item.Protected && File.Exists(item.Destination)) continue;
                selected.Add(item.Destination);
                if (!File.Exists(item.Destination)) additions.Add(item.Destination);
            }
            foreach (string name in RetiredServerFiles) selected.Add(Path.Combine(_server, name));
            selected.AddRange(_legacyRuntimeFiles);
            string receipt = Path.Combine(_server, "B2S-Package.sha256");
            selected.Add(receipt);
            if (!File.Exists(receipt)) additions.Add(receipt);
            return FolderRecovery.CreateSelected(roots, additions, backupRoot ?? FolderRecovery.DefaultRoot, selected);
        }

        public InstallPlan(ZipArchive designerArchive, ZipArchive serverArchive, string designer, string server, string vpx, string arch, bool installDesigner, string serverPrefix, string designerDigest, string serverDigest, Func<string, bool> serverIdentityReader = null)
        {
            _designerDigest = designerDigest;
            _serverDigest = serverDigest;
            _designerArchive = designerArchive;
            _serverArchive = serverArchive;
            _installDesigner = installDesigner;
            _designer = installDesigner ? Path.GetFullPath(designer) : null;
            _server = Path.GetFullPath(server);
            _vpx = Path.GetFullPath(vpx);
            _arch = arch;
            _serverPrefix = serverPrefix ?? String.Empty;
            _freshServer = !File.Exists(Path.Combine(_server, "B2SBackglassServer.dll"));
            _legacyRuntimeFiles = ConflictingServerCleanup.Find(_vpx, _server, serverIdentityReader);
            BuildItems();
            ExistingProgramFiles.AddRange(_legacyRuntimeFiles);
        }

        private void BuildItems()
        {
            if (_installDesigner)
            {
                const string commonPrefix = "common/";
                foreach (ZipArchiveEntry entry in _designerArchive.Entries)
                {
                    if (String.IsNullOrEmpty(entry.Name) || !entry.FullName.StartsWith(commonPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                    string relative = entry.FullName.Substring(commonPrefix.Length).Replace('/', Path.DirectorySeparatorChar);
                    Add(entry, Path.Combine(_designer, relative), false, true);
                }
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
            // These copies exist only long enough to roll back this install transaction.
            // Permanent user recovery is handled by the verified FolderRecovery archive.
            string designerBackup = _installDesigner ? Path.Combine(Path.GetTempPath(), "B2SPro-InstallRollback-" + Guid.NewGuid().ToString("N")) : null;
            string serverBackup = Path.Combine(Path.GetTempPath(), "B2SServer-InstallRollback-" + Guid.NewGuid().ToString("N"));
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

                var legacyFolders = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (string destination in _legacyRuntimeFiles)
                {
                    if (!File.Exists(destination)) continue;
                    string folder = Path.GetDirectoryName(destination);
                    int legacyFolder;
                    if (!legacyFolders.TryGetValue(folder, out legacyFolder))
                    {
                        legacyFolder = legacyFolders.Count;
                        legacyFolders.Add(folder, legacyFolder);
                    }
                    string backup = Path.Combine(serverBackup, "Conflicting-Server", legacyFolder.ToString(), Path.GetFileName(destination));
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

                if (_installDesigner) WritePackageReceipt(_designer, designerBackup, _designerDigest, changed);
                WritePackageReceipt(_server, serverBackup, _serverDigest, changed);

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
                return new InstallResult(_designer, _server, installed, preserved, retired, _freshServer, registerServer, registrationError, registerFileAssociations || registerServer, fileAssociationError, createShortcuts, shortcutError, _installDesigner);
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
            finally
            {
                foreach (string temporaryBackup in new[] { designerBackup, serverBackup })
                    if (!String.IsNullOrEmpty(temporaryBackup) && Directory.Exists(temporaryBackup))
                        try { Directory.Delete(temporaryBackup, true); } catch { /* A locked temporary copy may be removed by normal temp cleanup. */ }
            }
        }

        private static void WritePackageReceipt(string folder, string backupFolder, string digest, List<RollbackItem> changed)
        {
            string destination = Path.Combine(folder, "B2S-Package.sha256");
            bool existed = File.Exists(destination);
            string backup = Path.Combine(backupFolder, "B2S-Package.sha256");
            if (existed)
            {
                Directory.CreateDirectory(backupFolder);
                File.Copy(destination, backup, true);
            }
            changed.Add(new RollbackItem(destination, existed ? backup : null, existed));
            File.WriteAllText(destination, digest + Environment.NewLine);
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

    internal static class UpdateNotificationPreference
    {
        private const string DisabledFileName = "AutomaticNotificationsDisabled.txt";

        private static string DefaultFolder
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "B2SPro",
                    "UpdateChecks");
            }
        }

        public static bool IsDisabled()
        {
            return IsDisabledIn(DefaultFolder);
        }

        public static void Save(bool disabled)
        {
            SaveIn(DefaultFolder, disabled);
        }

        internal static bool IsDisabledIn(string folder)
        {
            try
            {
                string path = Path.Combine(folder, DisabledFileName);
                return File.Exists(path) && String.Equals(File.ReadAllText(path).Trim(), "1", StringComparison.Ordinal);
            }
            catch { return false; }
        }

        internal static void SaveIn(string folder, bool disabled)
        {
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, DisabledFileName);
            if (!disabled)
            {
                if (File.Exists(path)) File.Delete(path);
                return;
            }
            string temporary = path + ".new";
            File.WriteAllText(temporary, "1", Encoding.ASCII);
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
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
        private readonly bool _freshServer;
        private readonly bool _registrationAttempted;
        private readonly string _registrationError;
        private readonly bool _fileAssociationsAttempted;
        private readonly string _fileAssociationError;
        private readonly bool _shortcutsAttempted;
        private readonly string _shortcutError;
        private readonly bool _installedDesigner;
        public InstallResult(string designer, string server, int installed, int preserved, int retired, bool freshServer, bool registrationAttempted, string registrationError, bool fileAssociationsAttempted, string fileAssociationError, bool shortcutsAttempted, string shortcutError, bool installedDesigner)
        {
            _designer = designer;
            _server = server;
            _installed = installed;
            _preserved = preserved;
            _retired = retired;
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
        internal static bool IsLegacyInTreeLocation(string vpxRoot, string serverFolder)
        {
            if (String.IsNullOrWhiteSpace(vpxRoot) || String.IsNullOrWhiteSpace(serverFolder)) return false;
            string vpx = Path.GetFullPath(vpxRoot).TrimEnd(Path.DirectorySeparatorChar);
            string server = Path.GetFullPath(serverFolder).TrimEnd(Path.DirectorySeparatorChar);
            return String.Equals(server, vpx, StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(server, Path.Combine(vpx, "Tables"), StringComparison.OrdinalIgnoreCase);
        }

        internal static bool ShouldReplaceSuggestion(string currentFolder, string previousSuggestion)
        {
            return String.IsNullOrWhiteSpace(currentFolder) ||
                   (!String.IsNullOrWhiteSpace(previousSuggestion) && String.Equals(Path.GetFullPath(currentFolder), Path.GetFullPath(previousSuggestion), StringComparison.OrdinalIgnoreCase));
        }

        public static string FindOrSuggest(string vpxRoot)
        {
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
                string preferenceFolder = Path.Combine(sandbox, "UpdateChecks");
                UpdateNotificationPreference.SaveIn(preferenceFolder, true);
                if (!UpdateNotificationPreference.IsDisabledIn(preferenceFolder)) throw new Exception("Automatic update notification opt-out was not saved.");
                UpdateNotificationPreference.SaveIn(preferenceFolder, false);
                if (UpdateNotificationPreference.IsDisabledIn(preferenceFolder)) throw new Exception("Automatic update notifications could not be re-enabled.");
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
                        InstallPlan plan = package.CreatePlan(designer, server, vpxLayout, arch, true);
                        if (plan.ExistingProgramFiles.Count < 2) throw new Exception(arch + " existing-program detection failed.");
                        plan.Execute(false, false, false);
                        if (File.ReadAllText(Path.Combine(server, "ScreenRes.txt")) != "SELF-TEST-SCREENRES") throw new Exception(arch + " ScreenRes.txt was overwritten.");
                        if (File.ReadAllText(Path.Combine(server, "Plugins", "Plugins.txt")) != "SELF-TEST-PLUGIN") throw new Exception(arch + " plugin settings were overwritten.");
                        if (new FileInfo(Path.Combine(designer, "B2SPro.exe")).Length < 1000000) throw new Exception(arch + " Designer payload was not installed.");
                        foreach (string sharedFile in new[] { "B2SProHelp.html", "B2SProHelp.chm", "IDTester.B2SPro", "InfinityBulb.png", Path.Combine("OcrTools", "python", "python.exe"), Path.Combine("OcrTools", "node", "node.exe"), Path.Combine("OcrTools", "eng.traineddata") })
                            if (!File.Exists(Path.Combine(designer, sharedFile))) throw new Exception(arch + " shared Designer payload was not installed: " + sharedFile);
                        if (new FileInfo(Path.Combine(server, "B2SBackglassServer.dll")).Length < 100000) throw new Exception(arch + " Server payload was not installed.");
                        if (new FileInfo(Path.Combine(designer, "B2SUpdateChecker.exe")).Length < 10000) throw new Exception(arch + " Designer update checker was not installed.");
                        if (new FileInfo(Path.Combine(server, "B2SUpdateChecker.exe")).Length < 10000) throw new Exception(arch + " Server update checker was not installed.");
                        if (Directory.Exists(Path.Combine(designer, "B2SPro-Backups"))) throw new Exception(arch + " Designer recovery archive should not be retained.");
                        if (Directory.Exists(Path.Combine(server, "B2SPro-Backups"))) throw new Exception(arch + " temporary rollback files were retained inside the Server folder.");
                        foreach (string name in retiredServerFiles)
                        {
                            if (File.Exists(Path.Combine(server, name))) throw new Exception(arch + " obsolete Server file was not removed: " + name);
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
                if (!ServerLocator.IsLegacyInTreeLocation(freshVpx, freshVpx))
                    throw new Exception("The Visual Pinball root was not classified as a conflicting legacy Server location.");
                if (!ServerLocator.IsLegacyInTreeLocation(freshVpx, Path.Combine(freshVpx, "Tables")))
                    throw new Exception("The Tables folder was not classified as a conflicting legacy Server location.");
                if (ServerLocator.IsLegacyInTreeLocation(freshVpx, expectedFreshServer))
                    throw new Exception("The dedicated B2SServer folder was incorrectly classified as a legacy location.");
                if (ServerLocator.IsLegacyInTreeLocation(freshVpx, Path.Combine(sandbox, "CustomServer")))
                    throw new Exception("A separate custom Server folder was incorrectly classified as a legacy location.");
                if (!ServerLocator.ShouldReplaceSuggestion(String.Empty, null))
                    throw new Exception("An empty Server folder was not eligible for automatic suggestion.");
                if (!ServerLocator.ShouldReplaceSuggestion(expectedFreshServer, expectedFreshServer))
                    throw new Exception("The previous automatic Server suggestion was not eligible for refresh.");
                if (ServerLocator.ShouldReplaceSuggestion(Path.Combine(sandbox, "CustomServer"), expectedFreshServer))
                    throw new Exception("A user-selected Server folder would be overwritten by automatic detection.");
                string legacyFolder = Path.Combine(freshVpx, "Tables");
                Directory.CreateDirectory(legacyFolder);
                foreach (string name in ConflictingServerCleanup.RuntimeFiles) File.WriteAllText(Path.Combine(legacyFolder, name), "LEGACY-RUNTIME");
                Directory.CreateDirectory(Path.Combine(legacyFolder, "Plugins"));
                Directory.CreateDirectory(Path.Combine(legacyFolder, "Plugins64"));
                File.WriteAllText(Path.Combine(legacyFolder, "Plugins", "keep.txt"), "KEEP-PLUGIN");
                File.WriteAllText(Path.Combine(legacyFolder, "Plugins64", "keep.txt"), "KEEP-PLUGIN64");
                File.WriteAllText(Path.Combine(legacyFolder, "ScreenRes.txt"), "KEEP-SCREENRES");
                File.WriteAllText(Path.Combine(legacyFolder, "README.txt"), "KEEP-DOCUMENTATION");
                List<string> legacyFiles = ConflictingServerCleanup.Find(freshVpx, freshServer, delegate(string ignored) { return true; });
                if (legacyFiles.Count != ConflictingServerCleanup.RuntimeFiles.Length) throw new Exception("The verified conflicting-server runtime inventory was not detected exactly.");
                foreach (string path in legacyFiles)
                    if (path.IndexOf("Plugins", StringComparison.OrdinalIgnoreCase) >= 0 || String.Equals(Path.GetFileName(path), "ScreenRes.txt", StringComparison.OrdinalIgnoreCase) || String.Equals(Path.GetFileName(path), "README.txt", StringComparison.OrdinalIgnoreCase))
                        throw new Exception("Legacy cleanup included a protected plugin, setting or documentation file.");
                if (ConflictingServerCleanup.Find(freshVpx, freshServer, delegate(string ignored) { return false; }).Count != 0)
                    throw new Exception("A DLL without B2S Server product identity was incorrectly classified as a server copy.");
                using (var package = new ReleaseBundle(args[1], args[2]))
                {
                    package.Validate("x64", true);
                    package.CreatePlan(Path.Combine(freshVpx, "B2SPro"), freshServer, freshVpx, "x64", true).Execute(false, false, false);
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
                        InstallPlan plan = package.CreatePlan(null, serverOnlyRoot, sandbox, "x64", false);
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
                    }
                    if (Directory.Exists(Path.Combine(serverOnlyRoot, "B2SPro-Backups"))) throw new Exception("Server-only setup retained temporary rollback files inside the Server folder.");
                    if (!File.Exists(Path.Combine(serverOnlyRoot, "B2SServer-Install.log"))) throw new Exception("Server-only install log was not created.");
                    if (Directory.GetFiles(serverOnlyRoot, "B2SPro.exe", SearchOption.AllDirectories).Length != 0) throw new Exception("Server-only setup installed Designer files.");
                }

                string legacyVpx = Path.Combine(sandbox, "LegacyVisualPinball");
                string legacyTables = Path.Combine(legacyVpx, "Tables");
                string legacyCustom = Path.Combine(legacyVpx, "UserFolders", "CustomServerLocation");
                string protectedBackup = Path.Combine(legacyVpx, "B2SPro-Backups", "OldServerSnapshot");
                string replacementServer = Path.Combine(legacyVpx, "B2SServer");
                Directory.CreateDirectory(legacyTables);
                Directory.CreateDirectory(legacyCustom);
                Directory.CreateDirectory(protectedBackup);
                foreach (string name in ConflictingServerCleanup.RuntimeFiles)
                {
                    File.WriteAllText(Path.Combine(legacyVpx, name), "ORIGINAL-2.1.5-" + name);
                    File.WriteAllText(Path.Combine(legacyTables, name), "ORIGINAL-3.0-" + name);
                    File.WriteAllText(Path.Combine(legacyCustom, name), "ORIGINAL-CUSTOM-" + name);
                    File.WriteAllText(Path.Combine(protectedBackup, name), "PROTECTED-BACKUP-" + name);
                }
                File.SetAttributes(Path.Combine(legacyCustom, "B2SBackglassServerEXE.exe.config"), FileAttributes.Hidden);
                File.SetAttributes(Path.Combine(legacyCustom, "B2S_SetUp.exe.config"), FileAttributes.Hidden);
                File.SetAttributes(Path.Combine(legacyCustom, "B2S_ScreenResIdentifier.exe.config"), FileAttributes.Hidden);
                Directory.CreateDirectory(Path.Combine(legacyTables, "Plugins"));
                Directory.CreateDirectory(Path.Combine(legacyTables, "Plugins64"));
                File.WriteAllText(Path.Combine(legacyTables, "Plugins", "keep.txt"), "KEEP-PLUGIN");
                File.WriteAllText(Path.Combine(legacyTables, "Plugins64", "keep.txt"), "KEEP-PLUGIN64");
                File.WriteAllText(Path.Combine(legacyTables, "ScreenRes.txt"), "KEEP-SCREENRES");
                File.WriteAllText(Path.Combine(legacyTables, "README.txt"), "KEEP-README");
                string legacyRecovery;
                using (var package = new ReleaseBundle(null, args[2]))
                {
                    InstallPlan plan = package.CreatePlanForTest(null, replacementServer, legacyVpx, "x64", false, delegate(string ignored) { return true; });
                    legacyRecovery = plan.CreateRecovery(Path.Combine(sandbox, "LegacyRecovery"));
                    plan.Execute(false, false, false);
                }
                foreach (string name in ConflictingServerCleanup.RuntimeFiles)
                {
                    if (File.Exists(Path.Combine(legacyVpx, name)) || File.Exists(Path.Combine(legacyTables, name)) || File.Exists(Path.Combine(legacyCustom, name)))
                        throw new Exception("Conflicting server runtime file was not removed from every discovered location: " + name);
                    if (!File.Exists(Path.Combine(protectedBackup, name))) throw new Exception("A protected server backup file was removed: " + name);
                }
                if (File.ReadAllText(Path.Combine(legacyTables, "Plugins", "keep.txt")) != "KEEP-PLUGIN" ||
                    File.ReadAllText(Path.Combine(legacyTables, "Plugins64", "keep.txt")) != "KEEP-PLUGIN64" ||
                    File.ReadAllText(Path.Combine(legacyTables, "ScreenRes.txt")) != "KEEP-SCREENRES" ||
                    File.ReadAllText(Path.Combine(legacyTables, "README.txt")) != "KEEP-README")
                    throw new Exception("Legacy cleanup changed a protected plugin, setting or documentation file.");
                FolderRecovery.Restore(legacyRecovery, Path.Combine(sandbox, "LegacyRestoreUndo"));
                foreach (string name in ConflictingServerCleanup.RuntimeFiles)
                {
                    if (File.ReadAllText(Path.Combine(legacyVpx, name)) != "ORIGINAL-2.1.5-" + name) throw new Exception("Restore did not return the 2.x server runtime file: " + name);
                    if (File.ReadAllText(Path.Combine(legacyCustom, name)) != "ORIGINAL-CUSTOM-" + name) throw new Exception("Restore did not return the custom-location server runtime file: " + name);
                    if (File.ReadAllText(Path.Combine(legacyTables, name)) != "ORIGINAL-3.0-" + name) throw new Exception("Restore did not return the 3.x server runtime file: " + name);
                }
                if (File.Exists(Path.Combine(replacementServer, "B2SBackglassServer.dll")))
                    throw new Exception("Restore left the replacement server DLL behind.");

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
