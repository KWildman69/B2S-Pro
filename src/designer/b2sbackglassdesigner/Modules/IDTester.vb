Imports System.Diagnostics
Imports System.IO
Imports System.Windows.Forms
Imports System.Xml

''' <summary>
''' Launch the embedded ID tester for the exact matching VPX table.
''' No designer project, registry setting, or legacy backglass file is changed.
''' </summary>
Public Module IDTester
    Private ReadOnly SettingsPath As String = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "B2SPro", "IDTester", "Settings.xml")

    Private Class IDTesterConfig
        Public Property VPXExecutable As String = String.Empty
        Public Property TablesFolder As String = String.Empty
    End Class

    Public Sub Run(ByVal owner As IWin32Window, ByVal backglassName As String, ByVal designerTablesFolder As String)
        Try
            IDTesterRestore.RecoverPendingTests()
            If String.IsNullOrWhiteSpace(backglassName) OrElse
               Not String.Equals(Path.GetFileName(backglassName), backglassName, StringComparison.Ordinal) Then
                Throw New InvalidDataException("The open backglass does not have a valid table filename.")
            End If

            Dim config As IDTesterConfig = ReadConfig()
            If config Is Nothing Then config = New IDTesterConfig()
            If String.IsNullOrWhiteSpace(config.TablesFolder) AndAlso Directory.Exists(designerTablesFolder) Then
                config.TablesFolder = designerTablesFolder
            End If

            If Not ValidConfig(config) Then
                If Not Configure(owner, config) Then Return
            End If

            Dim tablePath As String = MatchingTablePath(config.TablesFolder, backglassName)
            If Not File.Exists(tablePath) Then
                Dim response As DialogResult = MessageBox.Show(owner,
                    "No exact-name VPX table was found:" & Environment.NewLine & tablePath & Environment.NewLine &
                    "Change the ID Tester tables folder?", "ID Tester", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
                If response <> DialogResult.Yes OrElse Not Configure(owner, config) Then Return
                tablePath = MatchingTablePath(config.TablesFolder, backglassName)
                If Not File.Exists(tablePath) Then
                    Throw New FileNotFoundException("No exact-name VPX table was found in that folder.", tablePath)
                End If
            End If

            Dim destination As String = Path.Combine(config.TablesFolder, backglassName & B2SProFileExtension)
            Dim backupFolder As String = IDTesterRestore.BackupFolder
            Dim journalPath As String = IDTesterRestore.StageForLaunch(destination, backupFolder, backglassName)

            Dim game As Process = Nothing
            Try
                Dim start As New ProcessStartInfo() With {
                    .FileName = config.VPXExecutable,
                    .Arguments = "-Play """ & tablePath & """",
                    .WorkingDirectory = config.TablesFolder,
                    .UseShellExecute = False
                }
                game = Process.Start(start)
                If game Is Nothing Then Throw New IOException("VPX did not start.")
                IDTesterRestore.RecordVPXProcess(journalPath, game)
                IDTesterRestore.StartMonitor(journalPath)
            Catch
                If game IsNot Nothing AndAlso Not game.HasExited Then
                    Dim runningGame As Process = game
                    Dim worker As New System.Threading.Thread(
                        Sub()
                            runningGame.WaitForExit()
                            IDTesterRestore.MonitorFromChild(journalPath)
                        End Sub) With {.IsBackground = False}
                    worker.Start()
                    game = Nothing
                Else
                    IDTesterRestore.RestoreNow(journalPath)
                End If
                Throw
            Finally
                If game IsNot Nothing Then game.Dispose()
            End Try
        Catch ex As Exception
            MessageBox.Show(owner, "ID Tester could not launch: " & ex.Message,
                            "ID Tester", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Function ValidConfig(ByVal config As IDTesterConfig) As Boolean
        Return config IsNot Nothing AndAlso
               config.VPXExecutable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) AndAlso
               File.Exists(config.VPXExecutable) AndAlso
               Directory.Exists(config.TablesFolder)
    End Function

    Private Function MatchingTablePath(ByVal tablesFolder As String, ByVal backglassName As String) As String
        Return Path.Combine(tablesFolder, backglassName & ".vpx")
    End Function

    Private Function ReadConfig() As IDTesterConfig
        If Not File.Exists(SettingsPath) Then Return Nothing
        Dim document As New XmlDocument()
        document.Load(SettingsPath)
        Dim root As XmlNode = document.SelectSingleNode("/IDTester")
        If root Is Nothing Then Throw New InvalidDataException("ID Tester settings have no IDTester root.")
        Dim vpx As XmlNode = root.SelectSingleNode("VPX")
        Dim folder As XmlNode = root.SelectSingleNode("TablesFolder")
        Return New IDTesterConfig() With {
            .VPXExecutable = If(vpx Is Nothing, String.Empty, vpx.InnerText.Trim()),
            .TablesFolder = If(folder Is Nothing, String.Empty, folder.InnerText.Trim())
        }
    End Function

    Private Sub SaveConfig(ByVal config As IDTesterConfig)
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath))
        Dim document As New XmlDocument()
        Dim root As XmlElement = document.CreateElement("IDTester")
        document.AppendChild(root)
        Dim vpx As XmlElement = document.CreateElement("VPX")
        vpx.InnerText = config.VPXExecutable
        root.AppendChild(vpx)
        Dim folder As XmlElement = document.CreateElement("TablesFolder")
        folder.InnerText = config.TablesFolder
        root.AppendChild(folder)
        document.Save(SettingsPath)
    End Sub

    Private Function Configure(ByVal owner As IWin32Window, ByVal config As IDTesterConfig) As Boolean
        Using dialog As New Form()
            dialog.Text = "ID Tester - one-time setup"
            dialog.Font = New Drawing.Font("Segoe UI", 9.0F)
            dialog.StartPosition = FormStartPosition.CenterParent
            dialog.FormBorderStyle = FormBorderStyle.FixedDialog
            dialog.MaximizeBox = False
            dialog.MinimizeBox = False
            dialog.ClientSize = New Drawing.Size(680, 158)

            Dim exeLabel As New Label() With {.Text = "VPX program", .Location = New Drawing.Point(16, 23), .Size = New Drawing.Size(108, 23)}
            Dim exeBox As New TextBox() With {.Text = config.VPXExecutable, .Location = New Drawing.Point(126, 20), .Size = New Drawing.Size(468, 23)}
            Dim exeBrowse As New Button() With {.Text = "Browse...", .Location = New Drawing.Point(601, 19), .Size = New Drawing.Size(66, 26)}
            AddHandler exeBrowse.Click,
                Sub()
                    Using picker As New OpenFileDialog()
                        picker.Filter = "VPX executable (*.exe)|*.exe"
                        picker.CheckFileExists = True
                        If File.Exists(exeBox.Text) Then picker.FileName = exeBox.Text
                        If picker.ShowDialog(dialog) = DialogResult.OK Then exeBox.Text = picker.FileName
                    End Using
                End Sub

            Dim folderLabel As New Label() With {.Text = "Tables folder", .Location = New Drawing.Point(16, 66), .Size = New Drawing.Size(108, 23)}
            Dim folderBox As New TextBox() With {.Text = config.TablesFolder, .Location = New Drawing.Point(126, 63), .Size = New Drawing.Size(468, 23)}
            Dim folderBrowse As New Button() With {.Text = "Browse...", .Location = New Drawing.Point(601, 62), .Size = New Drawing.Size(66, 26)}
            AddHandler folderBrowse.Click,
                Sub()
                    Using picker As New FolderBrowserDialog()
                        picker.Description = "Choose the folder containing your VPX .vpx tables"
                        If Directory.Exists(folderBox.Text) Then picker.SelectedPath = folderBox.Text
                        If picker.ShowDialog(dialog) = DialogResult.OK Then folderBox.Text = picker.SelectedPath
                    End Using
                End Sub

            Dim info As New Label() With {
                .Text = "After setup, the toolbar button launches the matching table directly.",
                .Location = New Drawing.Point(16, 104), .Size = New Drawing.Size(520, 23)
            }
            Dim save As New Button() With {.Text = "Save and launch", .Location = New Drawing.Point(548, 112), .Size = New Drawing.Size(119, 30)}
            AddHandler save.Click,
                Sub()
                    Dim proposed As New IDTesterConfig() With {
                        .VPXExecutable = exeBox.Text.Trim(), .TablesFolder = folderBox.Text.Trim()
                    }
                    If Not ValidConfig(proposed) Then
                        MessageBox.Show(dialog, "Choose an existing VPX executable and tables folder.",
                                        "ID Tester", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                        Return
                    End If
                    config.VPXExecutable = Path.GetFullPath(proposed.VPXExecutable)
                    config.TablesFolder = Path.GetFullPath(proposed.TablesFolder)
                    SaveConfig(config)
                    dialog.DialogResult = DialogResult.OK
                    dialog.Close()
                End Sub
            dialog.AcceptButton = save
            dialog.Controls.AddRange({exeLabel, exeBox, exeBrowse, folderLabel, folderBox, folderBrowse, info, save})
            Return dialog.ShowDialog(owner) = DialogResult.OK
        End Using
    End Function

End Module
