Imports System.Drawing
Imports System.Windows.Forms
Imports System.Collections.Generic

' Per-window display scaling; no project data or system display settings change.
Public NotInheritable Class EditorWindowZoom
    Implements IMessageFilter

    Private Shared ReadOnly instance As New EditorWindowZoom()
    Private Shared installed As Boolean
    Private ReadOnly windows As New Dictionary(Of Form, Integer)()

    Public Shared Sub Attach(window As Form)
        If window Is Nothing OrElse TypeOf window Is formDesigner OrElse TypeOf window Is formSplash Then Return
        If instance.windows.ContainsKey(window) Then Return
        instance.windows.Add(window, 100)
        AddHandler window.FormClosed, Sub() instance.windows.Remove(window)
        If Not installed Then
            Application.AddMessageFilter(instance)
            installed = True
        End If
    End Sub

    Public Function PreFilterMessage(ByRef message As Message) As Boolean Implements IMessageFilter.PreFilterMessage
        If (Control.ModifierKeys And Keys.Control) = 0 Then Return False
        Dim targetControl As Control = Control.FromChildHandle(message.HWnd)
        Dim window As Form = If(targetControl Is Nothing, Nothing, targetControl.FindForm())
        If window Is Nothing OrElse Not windows.ContainsKey(window) Then Return False
        Dim percent As Integer = windows(window)
        If message.Msg = &H20A Then
            Dim delta As Integer = CInt((message.WParam.ToInt64() >> 16) And &HFFFF)
            If delta >= &H8000 Then delta -= &H10000
            If delta = 0 Then Return True
            percent += If(delta > 0, 25, -25)
        ElseIf message.Msg = &H100 Then
            Select Case CType(message.WParam.ToInt32(), Keys)
                Case Keys.Add, Keys.Oemplus : percent += 25
                Case Keys.Subtract, Keys.OemMinus : percent -= 25
                Case Keys.D0, Keys.NumPad0 : percent = 100
                Case Else : Return False
            End Select
        Else
            Return False
        End If
        ScaleWindow(window, Math.Max(50, Math.Min(200, percent)))
        Return True
    End Function

    Private Sub ScaleWindow(window As Form, percent As Integer)
        Dim previous As Integer = windows(window)
        If previous = percent Then Return
        Dim factor As Single = percent / CSng(previous)
        Dim controls As New List(Of Control)()
        Collect(window, controls)
        Dim fonts As New Dictionary(Of Control, Font)()
        Dim modes As New Dictionary(Of ContainerControl, AutoScaleMode)()
        For Each child As Control In controls
            fonts(child) = New Font(child.Font.FontFamily, child.Font.Size * factor, child.Font.Style, child.Font.Unit)
            child.SuspendLayout()
            Dim container As ContainerControl = TryCast(child, ContainerControl)
            If container IsNot Nothing Then
                modes(container) = container.AutoScaleMode
                container.AutoScaleMode = AutoScaleMode.None
            End If
        Next
        Try
            Dim originalArea As Size = If(window.AutoScrollMinSize.IsEmpty, window.ClientSize, window.AutoScrollMinSize)
            Dim desired As New Size(CInt(originalArea.Width * factor), CInt(originalArea.Height * factor))
            window.AutoScrollPosition = Point.Empty
            window.Scale(New SizeF(factor, factor))
            For Each child As Control In controls
                child.Font = fonts(child)
            Next
            window.AutoScroll = True
            window.AutoScrollMinSize = desired
            Dim screenSize As Size = Screen.FromControl(window).WorkingArea.Size
            Dim border As New Size(window.Width - window.ClientSize.Width, window.Height - window.ClientSize.Height)
            window.MinimumSize = New Size(Math.Min(window.MinimumSize.Width, screenSize.Width), Math.Min(window.MinimumSize.Height, screenSize.Height))
            If window.WindowState = FormWindowState.Normal Then
                window.ClientSize = New Size(Math.Min(desired.Width, Math.Max(1, screenSize.Width - border.Width)),
                                             Math.Min(desired.Height, Math.Max(1, screenSize.Height - border.Height)))
            End If
            windows(window) = percent
        Finally
            For Each pair In modes
                pair.Key.AutoScaleDimensions = pair.Key.CurrentAutoScaleDimensions
                pair.Key.AutoScaleMode = pair.Value
            Next
            For index As Integer = controls.Count - 1 To 0 Step -1
                controls(index).ResumeLayout(True)
            Next
            window.Invalidate(True)
        End Try
    End Sub

    Private Shared Sub Collect(control As Control, result As List(Of Control))
        result.Add(control)
        For Each child As Control In control.Controls
            Collect(child, result)
        Next
    End Sub
End Class
