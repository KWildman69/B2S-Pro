Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Windows.Forms
Imports System.Xml

''' <summary>
''' Persists a form's normal bounds and window state in the user's AppData folder.
''' Changes are saved automatically after move/resize settles, and again on close.
''' Saved bounds are corrected when monitors are removed or their resolution changes.
''' </summary>
Public NotInheritable Class WindowStateManager
    Private Shared ReadOnly attached As New HashSet(Of Form)()
    Private Shared ReadOnly timers As New Dictionary(Of Form, Timer)()
    Private Shared ReadOnly restoring As New HashSet(Of Form)()
    Private Shared ReadOnly syncRoot As New Object()
    Private Shared ReadOnly stateFile As String = ResolveStateFile()
    Private Shared openFormTrackingStarted As Boolean

    Private Sub New()
    End Sub

    Private Shared Function ResolveStateFile() As String
        Dim currentFile As String = Path.Combine(Application.UserAppDataPath, "window-layout.xml")
        If File.Exists(currentFile) Then Return currentFile

        ' The executable used to be named B2SBackglassDesigner.exe.  Renaming it
        ' changes WinForms' UserAppDataPath, so copy the newest legacy layout once.
        Try
            Dim legacyRoot As String = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "B2SBackglassDesigner")
            If Directory.Exists(legacyRoot) Then
                Dim newestLegacyFile As String = Nothing
                Dim newestWriteTime As DateTime = DateTime.MinValue
                For Each candidate As String In Directory.GetFiles(legacyRoot, "window-layout.xml", SearchOption.AllDirectories)
                    Dim writeTime As DateTime = File.GetLastWriteTimeUtc(candidate)
                    If writeTime > newestWriteTime Then
                        newestWriteTime = writeTime
                        newestLegacyFile = candidate
                    End If
                Next
                If newestLegacyFile IsNot Nothing Then
                    Directory.CreateDirectory(Path.GetDirectoryName(currentFile))
                    File.Copy(newestLegacyFile, currentFile, False)
                End If
            End If
        Catch
            ' A missing or inaccessible legacy layout must never prevent startup.
        End Try

        Return currentFile
    End Function

    Public Shared Sub Attach(ByVal form As Form)
        EnsureOpenFormTracking()
        If form Is Nothing OrElse Not form.TopLevel OrElse attached.Contains(form) Then Return
        attached.Add(form)

        Dim saveTimer As New Timer() With {.Interval = 350}
        AddHandler saveTimer.Tick,
            Sub(sender As Object, e As EventArgs)
                saveTimer.Stop()
                SaveNow(form)
            End Sub
        timers(form) = saveTimer

        AddHandler form.Load, AddressOf Restore
        AddHandler form.LocationChanged, AddressOf QueueSave
        AddHandler form.SizeChanged, AddressOf QueueSave
        AddHandler form.FormClosing, AddressOf SaveOnClose
        AddHandler form.FormClosed, AddressOf Detach

        If form.IsHandleCreated Then Restore(form, EventArgs.Empty)
    End Sub


    Private Shared Sub EnsureOpenFormTracking()
        If openFormTrackingStarted Then Return
        openFormTrackingStarted = True
        AddHandler Application.Idle, AddressOf AttachOpenForms
    End Sub

    Private Shared Sub AttachOpenForms(ByVal sender As Object, ByVal e As EventArgs)
        ' This also captures secondary dialogs and editors that do not inherit formBase.
        ' Copy the collection first because attaching forms adds event handlers.
        Dim openForms As New List(Of Form)()
        For Each openForm As Form In Application.OpenForms
            ' Modal dialogs must use the size and CenterParent position chosen
            ' for the current question. Persisting a transient message box can
            ' move the recovery prompt away from the designer or restore stale
            ' dialog bounds.
            If openForm.Modal Then Continue For
            openForms.Add(openForm)
        Next
        For Each openForm As Form In openForms
            Attach(openForm)
        Next
    End Sub

    Public Shared Sub KeepOwnedFormsOnOwnerScreen(ByVal owner As Form)
        If owner Is Nothing OrElse Not owner.IsHandleCreated Then Return

        Dim ownerWorkArea As Rectangle = Screen.FromControl(owner).WorkingArea
        For Each openForm As Form In Application.OpenForms
            If openForm Is owner OrElse openForm.IsDisposed Then Continue For
            If openForm.Owner Is owner Then
                MoveIntoWorkingArea(openForm, ownerWorkArea)
            End If
        Next
    End Sub

    Public Shared Function HasSavedState(ByVal form As Form) As Boolean
        If form Is Nothing OrElse Not File.Exists(stateFile) Then Return False
        Try
            Dim doc As New XmlDocument()
            doc.Load(stateFile)
            Return doc.SelectSingleNode("/windows/window[@name=" & QuoteXPath(FormKey(form)) & "]") IsNot Nothing
        Catch
            Return False
        End Try
    End Function

    Private Shared Sub Restore(ByVal sender As Object, ByVal e As EventArgs)
        Dim form As Form = TryCast(sender, Form)
        If form Is Nothing OrElse Not form.TopLevel OrElse Not File.Exists(stateFile) Then Return

        Try
            restoring.Add(form)
            Dim doc As New XmlDocument()
            doc.Load(stateFile)
            Dim node As XmlNode = doc.SelectSingleNode("/windows/window[@name=" & QuoteXPath(FormKey(form)) & "]")
            If node Is Nothing Then Return

            Dim bounds As New Rectangle(ReadInt(node, "x", form.Left),
                                        ReadInt(node, "y", form.Top),
                                        ReadInt(node, "width", form.Width),
                                        ReadInt(node, "height", form.Height))

            ' Window sizes are physical pixels. A layout saved on a monitor with a
            ' different DPI must be converted before it is reused. Older layout
            ' files did not store DPI; for tool windows keep the freshly-created
            ' size in that case because it has already been autoscaled by WinForms.
            Dim savedDpi As Integer = ReadInt(node, "dpi", 0)
            Dim currentDpi As Integer = Math.Max(96, form.DeviceDpi)
            If savedDpi > 0 AndAlso savedDpi <> currentDpi Then
                bounds.Width = ScaleForDpi(bounds.Width, savedDpi, currentDpi)
                bounds.Height = ScaleForDpi(bounds.Height, savedDpi, currentDpi)
            ElseIf savedDpi = 0 AndAlso IsToolWindow(form) Then
                bounds.Size = form.Size
            End If

            ' Never let a stale layout squeeze a tool window below the size that
            ' its controls required when the form was created at the current DPI.
            If IsToolWindow(form) Then
                bounds.Width = Math.Max(bounds.Width, form.Width)
                bounds.Height = Math.Max(bounds.Height, form.Height)

                ' The illumination editor is a compact property panel. A stale
                ' full-screen width can be saved if a themed title-bar gesture is
                ' interrupted. Reject that invalid size instead of reopening the
                ' panel across the designer workspace on every launch.
                If String.Equals(FormKey(form), "formToolIllumination", StringComparison.OrdinalIgnoreCase) AndAlso
                   (bounds.Width > form.Width * 2 OrElse bounds.Height > form.Height * 2) Then
                    bounds.Size = form.Size
                End If
            End If
            If form.Owner IsNot Nothing AndAlso form.Owner.IsHandleCreated Then
                ' Owned tool windows and dialogs belong with the main designer.
                ' Preserve their saved size and approximate position, but clamp
                ' them to the owner's current monitor instead of another screen.
                bounds = MakeVisibleInWorkingArea(bounds, form.MinimumSize, Screen.FromControl(form.Owner).WorkingArea)
            Else
                bounds = MakeVisible(bounds, form.MinimumSize)
            End If

            form.StartPosition = FormStartPosition.Manual
            form.Bounds = bounds

            Dim savedState As String = ReadAttribute(node, "state", "Normal")
            If savedState = FormWindowState.Maximized.ToString() AndAlso form.MaximizeBox Then
                form.WindowState = FormWindowState.Maximized
            Else
                form.WindowState = FormWindowState.Normal
            End If
        Catch
            ' A damaged or inaccessible layout file must never stop the designer opening.
        Finally
            restoring.Remove(form)
        End Try
    End Sub

    Private Shared Sub QueueSave(ByVal sender As Object, ByVal e As EventArgs)
        Dim form As Form = TryCast(sender, Form)
        If form Is Nothing OrElse restoring.Contains(form) OrElse Not form.IsHandleCreated Then Return
        Dim saveTimer As Timer = Nothing
        If timers.TryGetValue(form, saveTimer) Then
            saveTimer.Stop()
            saveTimer.Start()
        End If
    End Sub

    Private Shared Sub SaveOnClose(ByVal sender As Object, ByVal e As FormClosingEventArgs)
        Dim form As Form = TryCast(sender, Form)
        If form Is Nothing Then Return
        SaveNow(form)
    End Sub

    Public Shared Sub SaveNow(ByVal form As Form)
        If form Is Nothing OrElse Not form.TopLevel OrElse restoring.Contains(form) Then Return
        Try
            SyncLock syncRoot
                Directory.CreateDirectory(Path.GetDirectoryName(stateFile))
                Dim doc As New XmlDocument()
                If File.Exists(stateFile) Then
                    Try
                        doc.Load(stateFile)
                    Catch
                        doc.RemoveAll()
                    End Try
                End If
                If doc.DocumentElement Is Nothing Then doc.AppendChild(doc.CreateElement("windows"))

                Dim key As String = FormKey(form)
                Dim node As XmlElement = TryCast(doc.SelectSingleNode("/windows/window[@name=" & QuoteXPath(key) & "]"), XmlElement)
                If node Is Nothing Then
                    node = doc.CreateElement("window")
                    doc.DocumentElement.AppendChild(node)
                End If

                Dim b As Rectangle = If(form.WindowState = FormWindowState.Normal, form.Bounds, form.RestoreBounds)
                If b.Width <= 0 OrElse b.Height <= 0 Then Return

                Dim screen As Screen = Screen.FromRectangle(b)
                node.SetAttribute("name", key)
                node.SetAttribute("x", b.X.ToString())
                node.SetAttribute("y", b.Y.ToString())
                node.SetAttribute("width", b.Width.ToString())
                node.SetAttribute("height", b.Height.ToString())
                node.SetAttribute("dpi", Math.Max(96, form.DeviceDpi).ToString())
                node.SetAttribute("state", form.WindowState.ToString())
                node.SetAttribute("screen", screen.DeviceName)
                doc.Save(stateFile)
            End SyncLock
        Catch
            ' Layout persistence is helpful, but never allowed to interrupt normal use.
        End Try
    End Sub

    Private Shared Sub Detach(ByVal sender As Object, ByVal e As FormClosedEventArgs)
        Dim form As Form = TryCast(sender, Form)
        If form Is Nothing Then Return
        Dim saveTimer As Timer = Nothing
        If timers.TryGetValue(form, saveTimer) Then
            saveTimer.Stop()
            saveTimer.Dispose()
            timers.Remove(form)
        End If
        attached.Remove(form)
        restoring.Remove(form)
    End Sub

    Private Shared Sub MoveIntoWorkingArea(ByVal form As Form, ByVal work As Rectangle)
        If form Is Nothing OrElse form.IsDisposed Then Return

        Dim bounds As Rectangle = If(form.WindowState = FormWindowState.Normal, form.Bounds, form.RestoreBounds)
        bounds = MakeVisibleInWorkingArea(bounds, form.MinimumSize, work)
        If form.WindowState = FormWindowState.Minimized Then form.WindowState = FormWindowState.Normal
        form.StartPosition = FormStartPosition.Manual
        form.Bounds = bounds
    End Sub

    Private Shared Function MakeVisibleInWorkingArea(ByVal bounds As Rectangle, ByVal minimumSize As Size, ByVal work As Rectangle) As Rectangle
        Dim width As Integer = Math.Max(bounds.Width, Math.Max(100, minimumSize.Width))
        Dim height As Integer = Math.Max(bounds.Height, Math.Max(80, minimumSize.Height))
        width = Math.Min(width, work.Width)
        height = Math.Min(height, work.Height)

        Dim corrected As New Rectangle(bounds.X, bounds.Y, width, height)
        corrected.X = Math.Max(work.Left, Math.Min(corrected.X, work.Right - corrected.Width))
        corrected.Y = Math.Max(work.Top, Math.Min(corrected.Y, work.Bottom - corrected.Height))
        Return corrected
    End Function

    Private Shared Function MakeVisible(ByVal bounds As Rectangle, ByVal minimumSize As Size) As Rectangle
        Dim width As Integer = Math.Max(bounds.Width, Math.Max(100, minimumSize.Width))
        Dim height As Integer = Math.Max(bounds.Height, Math.Max(80, minimumSize.Height))
        Dim corrected As New Rectangle(bounds.X, bounds.Y, width, height)

        Dim target As Screen = Nothing
        Dim bestArea As Long = 0
        For Each candidate As Screen In Screen.AllScreens
            Dim intersection As Rectangle = Rectangle.Intersect(candidate.WorkingArea, corrected)
            Dim area As Long = CLng(intersection.Width) * CLng(intersection.Height)
            If area > bestArea Then
                bestArea = area
                target = candidate
            End If
        Next
        If target Is Nothing OrElse bestArea < 2500 Then target = Screen.PrimaryScreen

        Dim work As Rectangle = target.WorkingArea
        corrected.Width = Math.Min(corrected.Width, work.Width)
        corrected.Height = Math.Min(corrected.Height, work.Height)
        corrected.X = Math.Max(work.Left, Math.Min(corrected.X, work.Right - corrected.Width))
        corrected.Y = Math.Max(work.Top, Math.Min(corrected.Y, work.Bottom - corrected.Height))
        Return corrected
    End Function

    Private Shared Function FormKey(ByVal form As Form) As String
        Return If(String.IsNullOrEmpty(form.Name), form.GetType().Name, form.Name)
    End Function

    Private Shared Function IsToolWindow(ByVal form As Form) As Boolean
        Return form IsNot Nothing AndAlso
               FormKey(form).StartsWith("formTool", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Shared Function ScaleForDpi(ByVal value As Integer,
                                        ByVal sourceDpi As Integer,
                                        ByVal targetDpi As Integer) As Integer
        If sourceDpi <= 0 OrElse sourceDpi = targetDpi Then Return value
        Return Math.Max(1, CInt(Math.Round(CDbl(value) * targetDpi / sourceDpi)))
    End Function

    Private Shared Function ReadAttribute(ByVal node As XmlNode, ByVal name As String, ByVal fallback As String) As String
        If node.Attributes Is Nothing OrElse node.Attributes(name) Is Nothing Then Return fallback
        Return node.Attributes(name).Value
    End Function

    Private Shared Function ReadInt(ByVal node As XmlNode, ByVal name As String, ByVal fallback As Integer) As Integer
        Dim value As Integer
        If Integer.TryParse(ReadAttribute(node, name, fallback.ToString()), value) Then Return value
        Return fallback
    End Function

    Private Shared Function QuoteXPath(ByVal value As String) As String
        ' WinForms control names are identifiers and normally contain no quotes.
        ' Handle an apostrophe defensively by using a double-quoted XPath literal.
        If value.Contains(ChrW(34)) Then value = value.Replace(ChrW(34), String.Empty)
        If value.Contains("'") Then Return ChrW(34) & value & ChrW(34)
        Return "'" & value & "'"
    End Function
End Class
