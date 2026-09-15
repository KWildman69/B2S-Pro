Imports System.Diagnostics
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Reflection
Imports System.Security.Cryptography
Imports System.Text
Imports System.Threading
Imports System.Windows.Forms
Imports System.Xml

''' <summary>
''' Restores the exact table-folder backglass after an ID Tester VPX run.
''' The journal and backups live in per-user application data, never in a project.
''' </summary>
Public Module IDTesterRestore
    Private Const ResourceName As String = "B2SBackglassDesigner.IDTester.B2SPro"
    Private Const MonitorArgument As String = "--id-tester-monitor"

    Private Class Journal
        Public Property FileName As String = String.Empty
        Public Property Destination As String = String.Empty
        Public Property BackupPath As String = String.Empty
        Public Property HadOriginal As Boolean
        Public Property TesterHash As String = String.Empty
        Public Property ProcessId As Integer
        Public Property ProcessStartTicks As Long
    End Class

    Public ReadOnly Property BackupFolder As String
        Get
            Return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                "B2SPro", "IDTester", "Backups")
        End Get
    End Property

    Public Function IsMonitorCommand(ByVal arguments As System.Collections.ObjectModel.ReadOnlyCollection(Of String)) As Boolean
        Return arguments IsNot Nothing AndAlso arguments.Count = 2 AndAlso
               String.Equals(arguments(0), MonitorArgument, StringComparison.Ordinal)
    End Function

    Public Function StageForLaunch(ByVal destination As String, ByVal backupFolderPath As String,
                                   ByVal backglassName As String) As String
        If Not String.Equals(Path.GetFullPath(backupFolderPath), Path.GetFullPath(BackupFolder), StringComparison.OrdinalIgnoreCase) Then
            Throw New InvalidOperationException("ID Tester backup folder is outside its per-user application data.")
        End If
        Directory.CreateDirectory(BackupFolder)
        For Each active As String In Directory.GetFiles(BackupFolder, "active-*.xml", SearchOption.TopDirectoryOnly)
            Try
                If String.Equals(ReadJournal(active).Destination, Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase) Then
                    Throw New IOException("An earlier ID test for this table still needs to be restored.")
                End If
            Catch ex As FileNotFoundException
                ' A restore monitor completed while this folder was being inspected.
            End Try
        Next

        Dim bytes As Byte() = EmbeddedTesterBytes(backglassName)
        Dim journal As New Journal() With {
            .FileName = Path.Combine(BackupFolder, "active-" & Guid.NewGuid().ToString("N") & ".xml"),
            .Destination = Path.GetFullPath(destination),
            .HadOriginal = File.Exists(destination),
            .TesterHash = HashBytes(bytes)
        }
        If journal.HadOriginal Then
            journal.BackupPath = Path.Combine(BackupFolder, Path.GetFileName(destination) & "." &
                                              DateTime.Now.ToString("yyyyMMdd-HHmmss") & "." &
                                              Guid.NewGuid().ToString("N") & ".backup")
            File.Copy(destination, journal.BackupPath, False)
            If Not String.Equals(HashFile(destination), HashFile(journal.BackupPath), StringComparison.Ordinal) Then
                Throw New IOException("The existing table backglass backup was not verified.")
            End If
        End If

        ' Commit the recovery instructions before the table-folder file changes.
        WriteJournal(journal)
        Try
            File.WriteAllBytes(journal.Destination, bytes)
            If Not String.Equals(HashFile(journal.Destination), journal.TesterHash, StringComparison.Ordinal) Then
                Throw New IOException("The staged ID tester copy was not verified.")
            End If
            Return journal.FileName
        Catch
            ' During this method no other process has yet been launched for the tester.
            Try
                If journal.HadOriginal Then
                    File.Copy(journal.BackupPath, journal.Destination, True)
                    If Not String.Equals(HashFile(journal.BackupPath), HashFile(journal.Destination), StringComparison.Ordinal) Then
                        Throw New IOException("Rollback verification failed.")
                    End If
                ElseIf File.Exists(journal.Destination) Then
                    File.Delete(journal.Destination)
                End If
                File.Delete(journal.FileName)
                If journal.HadOriginal Then TryDeleteFile(journal.BackupPath)
            Catch
                ' Leave the journal and backup for recovery on the next startup.
            End Try
            Throw
        End Try
    End Function

    Public Sub RecordVPXProcess(ByVal journalPath As String, ByVal game As Process)
        Dim journal As Journal = ReadJournal(journalPath)
        journal.ProcessId = game.Id
        journal.ProcessStartTicks = game.StartTime.ToUniversalTime().Ticks
        WriteJournal(journal)
    End Sub

    Public Sub StartMonitor(ByVal journalPath As String)
        Try
            Dim start As New ProcessStartInfo() With {
                .FileName = Application.ExecutablePath,
                .Arguments = MonitorArgument & " """ & journalPath & """",
                .WorkingDirectory = Application.StartupPath,
                .UseShellExecute = False,
                .CreateNoWindow = True,
                .WindowStyle = ProcessWindowStyle.Hidden
            }
            Using monitor As Process = Process.Start(start)
                If monitor Is Nothing Then Throw New IOException("ID Tester restore monitor did not start.")
            End Using
        Catch
            ' Keep the editor process alive until the game exits if the independent
            ' same-EXE monitor could not start. The startup journal is a second fallback.
            Dim worker As New Thread(Sub() MonitorFromChild(journalPath)) With {.IsBackground = False}
            worker.Start()
        End Try
    End Sub

    Public Sub MonitorFromChild(ByVal journalPath As String)
        Try
            Dim journal As Journal = ReadJournal(journalPath)
            If journal.ProcessId > 0 Then
                Try
                    Using game As Process = Process.GetProcessById(journal.ProcessId)
                        If game.StartTime.ToUniversalTime().Ticks = journal.ProcessStartTicks Then
                            Try
                                IDTesterWindows.WatchBackglassAndCloseVPX(game, journal.ProcessStartTicks)
                            Catch ex As Exception
                                ' A window-monitor failure must not skip the file restore.
                                ' The exact launched VPX process is still watched to exit.
                                Debug.WriteLine("ID Tester window monitor: " & ex.Message)
                            End Try
                            game.WaitForExit()
                        End If
                    End Using
                Catch ex As ArgumentException
                    ' The original VPX process already exited.
                Catch ex As InvalidOperationException
                    ' The original VPX process already exited.
                End Try
            End If
            For attempt As Integer = 1 To 15
                Try
                    RestoreNow(journalPath)
                    Return
                Catch ex As IOException
                    If attempt = 15 Then Throw
                    Thread.Sleep(2000)
                End Try
            Next
        Catch ex As Exception
            MessageBox.Show("ID Tester could not restore the table backglass automatically." & Environment.NewLine &
                            ex.Message & Environment.NewLine & "The backup and recovery record were kept at:" &
                            Environment.NewLine & BackupFolder,
                            "ID Tester restore", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Public Sub RecoverPendingTests()
        If Not Directory.Exists(BackupFolder) Then Return
        For Each journalPath As String In Directory.GetFiles(BackupFolder, "active-*.xml", SearchOption.TopDirectoryOnly)
            Dim journal As Journal
            Try
                journal = ReadJournal(journalPath)
            Catch ex As FileNotFoundException
                Continue For
            End Try
            ' A crash between VPX launch and recording its PID leaves zero here.
            ' In that narrow case, keep the tester in place while any VPX game runs.
            If journal.ProcessId = 0 AndAlso AnyVPXRunning() Then Continue For
            If Not IsOriginalVPXRunning(journal) Then RestoreNow(journalPath)
        Next
    End Sub

    Public Sub RestoreNow(ByVal journalPath As String)
        Dim mutexName As String = "Local\B2SPro-IDTester-" & Path.GetFileNameWithoutExtension(journalPath)
        Using gate As New Mutex(False, mutexName)
            Dim acquired As Boolean = False
            Try
                Try
                    acquired = gate.WaitOne(15000)
                Catch ex As AbandonedMutexException
                    acquired = True
                End Try
                If Not acquired Then Throw New IOException("Another ID Tester restore is still working on this file.")
                If Not File.Exists(journalPath) Then Return
                Dim journal As Journal = ReadJournal(journalPath)
                Dim destinationExists As Boolean = File.Exists(journal.Destination)
                If destinationExists AndAlso Not String.Equals(HashFile(journal.Destination), journal.TesterHash, StringComparison.Ordinal) Then
                    ' A previous restore may have completed just before its journal
                    ' was removed. Recognize that verified state and finish cleanup.
                    If journal.HadOriginal AndAlso File.Exists(journal.BackupPath) AndAlso
                       String.Equals(HashFile(journal.Destination), HashFile(journal.BackupPath), StringComparison.Ordinal) Then
                        File.Delete(journal.FileName)
                        TryDeleteFile(journal.BackupPath)
                        Return
                    End If
                    Throw New InvalidDataException("The table backglass changed during the ID test. It was not overwritten. " &
                                                   "The original backup remains at " & journal.BackupPath)
                End If

                If journal.HadOriginal Then
                    If Not File.Exists(journal.BackupPath) Then Throw New FileNotFoundException("Original backglass backup is missing.", journal.BackupPath)
                    File.Copy(journal.BackupPath, journal.Destination, True)
                    If Not String.Equals(HashFile(journal.BackupPath), HashFile(journal.Destination), StringComparison.Ordinal) Then
                        Throw New IOException("Restored backglass did not match its original backup.")
                    End If
                ElseIf destinationExists Then
                    File.Delete(journal.Destination)
                End If
                File.Delete(journal.FileName)
                If journal.HadOriginal Then TryDeleteFile(journal.BackupPath)
            Finally
                If acquired Then gate.ReleaseMutex()
            End Try
        End Using
    End Sub

    Private Function AnyVPXRunning() As Boolean
        For Each candidate As Process In Process.GetProcesses()
            Try
                If candidate.ProcessName.StartsWith("VPinballX", StringComparison.OrdinalIgnoreCase) Then Return True
            Catch ex As InvalidOperationException
                ' The process ended during enumeration.
            Finally
                candidate.Dispose()
            End Try
        Next
        Return False
    End Function

    Private Function IsOriginalVPXRunning(ByVal journal As Journal) As Boolean
        If journal.ProcessId <= 0 Then Return False
        Try
            Using game As Process = Process.GetProcessById(journal.ProcessId)
                Return Not game.HasExited AndAlso game.StartTime.ToUniversalTime().Ticks = journal.ProcessStartTicks
            End Using
        Catch ex As ArgumentException
            Return False
        Catch ex As InvalidOperationException
            Return False
        End Try
    End Function

    Private Function EmbeddedTesterBytes(ByVal backglassName As String) As Byte()
        Using input As Stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            If input Is Nothing Then Throw New InvalidDataException("Embedded ID tester resource is missing.")
            Using buffer As New MemoryStream()
                input.CopyTo(buffer)
                Dim document As New XmlDocument()
                buffer.Position = 0
                document.Load(buffer)
                Dim root As XmlElement = document.DocumentElement
                If root Is Nothing OrElse root.Name <> "DirectB2SData" Then
                    Throw New InvalidDataException("Embedded ID tester is not DirectB2SData XML.")
                End If
                Dim version As Version = Nothing
                If Not Version.TryParse(root.GetAttribute("Version"), version) OrElse
                   version.CompareTo(New Version(1, 0)) < 0 OrElse version.CompareTo(New Version(2, 1, 6)) > 0 Then
                    Throw New InvalidDataException("Embedded ID tester version is unsupported by this server.")
                End If
                If String.IsNullOrWhiteSpace(backglassName) Then
                    Throw New InvalidDataException("The running table name is missing.")
                End If

                Dim background As XmlElement = TryCast(root.SelectSingleNode("Images/BackglassImage"), XmlElement)
                Dim designerData As XmlElement = TryCast(root.SelectSingleNode("B2SProDesignerData"), XmlElement)
                If background Is Nothing OrElse designerData Is Nothing Then
                    Throw New InvalidDataException("Embedded ID tester artwork or project data is missing.")
                End If
                Dim imageBytes As Byte() = Convert.FromBase64String(background.GetAttribute("Value"))
                Dim namedImage As Byte() = AddTableName(imageBytes, backglassName)
                Dim namedImageBase64 As String = Convert.ToBase64String(namedImage)
                background.SetAttribute("Value", namedImageBase64)

                Dim project As New XmlDocument()
                project.LoadXml(Encoding.UTF8.GetString(Convert.FromBase64String(designerData.InnerText)))
                Dim projectImage As XmlElement = TryCast(project.SelectSingleNode(
                    "B2SBackglassData/Images/BackgroundImages/MainImage"), XmlElement)
                If projectImage Is Nothing Then
                    Throw New InvalidDataException("Embedded ID tester project background is missing.")
                End If
                projectImage.SetAttribute("Image", namedImageBase64)
                designerData.InnerText = Convert.ToBase64String(Encoding.UTF8.GetBytes(project.OuterXml))

                Using output As New MemoryStream()
                    document.Save(output)
                    Return output.ToArray()
                End Using
            End Using
        End Using
    End Function

    Private Function AddTableName(ByVal source As Byte(), ByVal tableName As String) As Byte()
        Using input As New MemoryStream(source, False),
              background As New Bitmap(input)
            If background.Width <> 3000 OrElse background.Height <> 3000 Then
                Throw New InvalidDataException("Embedded ID tester background size changed.")
            End If
            Using graphics As Graphics = Graphics.FromImage(background),
                  brush As New SolidBrush(Color.FromArgb(240, 247, 255)),
                  format As New StringFormat()
                graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
                graphics.TextRenderingHint = Drawing.Text.TextRenderingHint.AntiAliasGridFit
                format.Alignment = StringAlignment.Center
                format.LineAlignment = StringAlignment.Center
                Dim fontSize As Single = 54.0F
                Using initialFont As New Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel)
                    While graphics.MeasureString(tableName, initialFont).Width > 1210 AndAlso fontSize > 27.0F
                        fontSize -= 2.0F
                        Using measureFont As New Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel)
                            If graphics.MeasureString(tableName, measureFont).Width <= 1210 Then Exit While
                        End Using
                    End While
                End Using
                Using nameFont As New Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel)
                    graphics.DrawString(tableName, nameFont, brush,
                        New RectangleF(875, 2873, 1250, 76), format)
                End Using
            End Using
            Using output As New MemoryStream()
                background.Save(output, ImageFormat.Png)
                Return output.ToArray()
            End Using
        End Using
    End Function

    Private Function ReadJournal(ByVal journalPath As String) As Journal
        Dim expectedFolder As String = Path.GetFullPath(BackupFolder).TrimEnd(Path.DirectorySeparatorChar)
        If Not String.Equals(Path.GetDirectoryName(Path.GetFullPath(journalPath)), expectedFolder, StringComparison.OrdinalIgnoreCase) OrElse
           Not Path.GetFileName(journalPath).StartsWith("active-", StringComparison.OrdinalIgnoreCase) Then
            Throw New InvalidDataException("ID Tester recovery record path is invalid.")
        End If
        Dim document As New XmlDocument()
        document.Load(journalPath)
        Dim root As XmlNode = document.SelectSingleNode("/ActiveIDTester")
        If root Is Nothing Then Throw New InvalidDataException("ID Tester recovery record is invalid.")
        Return New Journal() With {
            .FileName = Path.GetFullPath(journalPath),
            .Destination = RequiredValue(root, "Destination"),
            .BackupPath = OptionalValue(root, "BackupPath"),
            .HadOriginal = Boolean.Parse(RequiredValue(root, "HadOriginal")),
            .TesterHash = RequiredValue(root, "TesterHash"),
            .ProcessId = Integer.Parse(RequiredValue(root, "ProcessId")),
            .ProcessStartTicks = Long.Parse(RequiredValue(root, "ProcessStartTicks"))
        }
    End Function

    Private Sub WriteJournal(ByVal journal As Journal)
        Dim document As New XmlDocument()
        Dim root As XmlElement = document.CreateElement("ActiveIDTester")
        document.AppendChild(root)
        AddValue(document, root, "Destination", journal.Destination)
        AddValue(document, root, "BackupPath", journal.BackupPath)
        AddValue(document, root, "HadOriginal", journal.HadOriginal.ToString())
        AddValue(document, root, "TesterHash", journal.TesterHash)
        AddValue(document, root, "ProcessId", journal.ProcessId.ToString())
        AddValue(document, root, "ProcessStartTicks", journal.ProcessStartTicks.ToString())
        Dim temporary As String = journal.FileName & ".new-" & Guid.NewGuid().ToString("N")
        document.Save(temporary)
        If File.Exists(journal.FileName) Then
            File.Replace(temporary, journal.FileName, Nothing)
        Else
            File.Move(temporary, journal.FileName)
        End If
    End Sub

    Private Sub AddValue(ByVal document As XmlDocument, ByVal root As XmlElement, ByVal name As String, ByVal value As String)
        Dim element As XmlElement = document.CreateElement(name)
        element.InnerText = value
        root.AppendChild(element)
    End Sub

    Private Function RequiredValue(ByVal root As XmlNode, ByVal name As String) As String
        Dim node As XmlNode = root.SelectSingleNode(name)
        If node Is Nothing OrElse String.IsNullOrWhiteSpace(node.InnerText) Then
            Throw New InvalidDataException("ID Tester recovery record is missing " & name & ".")
        End If
        Return node.InnerText
    End Function

    Private Function OptionalValue(ByVal root As XmlNode, ByVal name As String) As String
        Dim node As XmlNode = root.SelectSingleNode(name)
        Return If(node Is Nothing, String.Empty, node.InnerText)
    End Function

    Private Function HashBytes(ByVal bytes As Byte()) As String
        Using sha As SHA256 = SHA256.Create()
            Return Convert.ToBase64String(sha.ComputeHash(bytes))
        End Using
    End Function

    Private Function HashFile(ByVal filename As String) As String
        Using input As New FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite),
              sha As SHA256 = SHA256.Create()
            Return Convert.ToBase64String(sha.ComputeHash(input))
        End Using
    End Function

    Private Sub TryDeleteFile(ByVal filename As String)
        If String.IsNullOrWhiteSpace(filename) Then Return
        Try
            If File.Exists(filename) Then File.Delete(filename)
        Catch ex As IOException
            Debug.WriteLine("ID Tester cleanup: " & ex.Message)
        Catch ex As UnauthorizedAccessException
            Debug.WriteLine("ID Tester cleanup: " & ex.Message)
        End Try
    End Sub
End Module
