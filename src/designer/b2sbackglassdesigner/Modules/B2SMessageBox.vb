Imports System.Drawing
Imports System.Windows.Forms

''' <summary>Application-wide replacement for native light Windows message boxes.</summary>
Public NotInheritable Class B2SMessageBox
    Private Sub New()
    End Sub

    Public Shared Function Show(text As String) As DialogResult
        Return ShowCore(Nothing, text, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1)
    End Function

    Public Shared Function Show(owner As IWin32Window, text As String) As DialogResult
        Return ShowCore(owner, text, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1)
    End Function

    Public Shared Function Show(text As String, caption As String) As DialogResult
        Return ShowCore(Nothing, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1)
    End Function

    Public Shared Function Show(owner As IWin32Window, text As String, caption As String) As DialogResult
        Return ShowCore(owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1)
    End Function

    Public Shared Function Show(text As String, caption As String, buttons As MessageBoxButtons) As DialogResult
        Return ShowCore(Nothing, text, caption, buttons, MessageBoxIcon.None, MessageBoxDefaultButton.Button1)
    End Function

    Public Shared Function Show(owner As IWin32Window, text As String, caption As String, buttons As MessageBoxButtons) As DialogResult
        Return ShowCore(owner, text, caption, buttons, MessageBoxIcon.None, MessageBoxDefaultButton.Button1)
    End Function

    Public Shared Function Show(text As String, caption As String, buttons As MessageBoxButtons, icon As MessageBoxIcon) As DialogResult
        Return ShowCore(Nothing, text, caption, buttons, icon, MessageBoxDefaultButton.Button1)
    End Function

    Public Shared Function Show(owner As IWin32Window, text As String, caption As String, buttons As MessageBoxButtons, icon As MessageBoxIcon) As DialogResult
        Return ShowCore(owner, text, caption, buttons, icon, MessageBoxDefaultButton.Button1)
    End Function

    Public Shared Function Show(text As String, caption As String, buttons As MessageBoxButtons, icon As MessageBoxIcon, defaultButton As MessageBoxDefaultButton) As DialogResult
        Return ShowCore(Nothing, text, caption, buttons, icon, defaultButton)
    End Function

    Public Shared Function Show(owner As IWin32Window, text As String, caption As String, buttons As MessageBoxButtons, icon As MessageBoxIcon, defaultButton As MessageBoxDefaultButton) As DialogResult
        Return ShowCore(owner, text, caption, buttons, icon, defaultButton)
    End Function

    Private Shared Function ShowCore(owner As IWin32Window,
                                     text As String,
                                     caption As String,
                                     buttons As MessageBoxButtons,
                                     icon As MessageBoxIcon,
                                     defaultButton As MessageBoxDefaultButton) As DialogResult
        Using dialog As New B2SThemedForm()
            dialog.Name = "formB2SMessage"
            dialog.Text = If(String.IsNullOrWhiteSpace(caption), AppTitle, caption)
            dialog.StartPosition = If(owner Is Nothing, FormStartPosition.CenterScreen, FormStartPosition.CenterParent)
            dialog.FormBorderStyle = FormBorderStyle.FixedDialog
            dialog.ShowInTaskbar = False
            dialog.MinimizeBox = False
            dialog.MaximizeBox = False
            dialog.BackColor = Color.FromArgb(2, 4, 8)
            dialog.ForeColor = Color.White
            dialog.Font = New Font("Segoe UI", 9.0F, FontStyle.Regular)

            Dim messageWidth As Integer = 390
            Dim measured As Size = TextRenderer.MeasureText(If(text, String.Empty), dialog.Font,
                                                            New Size(messageWidth, 500),
                                                            TextFormatFlags.WordBreak)
            Dim contentHeight As Integer = Math.Max(58, measured.Height + 18)
            dialog.ClientSize = New Size(500, Math.Min(360, Math.Max(155, contentHeight + 76)))

            Dim root As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 2,
                .Padding = New Padding(18, 12, 18, 12),
                .BackColor = Color.FromArgb(2, 4, 8)
            }
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 40.0F))

            Dim messageRow As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 1, .BackColor = Color.Transparent}
            messageRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 48.0F))
            messageRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
            Dim iconLabel As New Label With {
                .Text = IconGlyph(icon),
                .Dock = DockStyle.Top,
                .Height = 36,
                .TextAlign = ContentAlignment.MiddleCenter,
                .Font = New Font("Segoe UI", 15.0F, FontStyle.Bold),
                .ForeColor = Color.White,
                .BackColor = IconColor(icon),
                .Margin = New Padding(0, 5, 10, 0)
            }
            Dim messageLabel As New Label With {
                .Text = text,
                .Dock = DockStyle.Fill,
                .TextAlign = ContentAlignment.MiddleLeft,
                .ForeColor = Color.White,
                .BackColor = Color.Transparent,
                .Font = dialog.Font
            }
            messageRow.Controls.Add(iconLabel, 0, 0)
            messageRow.Controls.Add(messageLabel, 1, 0)

            Dim buttonRow As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False,
                .Padding = New Padding(0, 4, 0, 0),
                .BackColor = Color.Transparent
            }
            Dim results As DialogResult() = ButtonResults(buttons)
            Dim created As New List(Of Button)()
            For i As Integer = results.Length - 1 To 0 Step -1
                Dim result As DialogResult = results(i)
                Dim command As New Button With {
                    .Text = ButtonText(result),
                    .DialogResult = result,
                    .Size = New Size(82, 29),
                    .Margin = New Padding(8, 0, 0, 0),
                    .FlatStyle = FlatStyle.Flat,
                    .BackColor = Color.FromArgb(15, 27, 48),
                    .ForeColor = Color.White,
                    .UseVisualStyleBackColor = False
                }
                command.FlatAppearance.BorderColor = Color.FromArgb(35, 190, 255)
                command.FlatAppearance.BorderSize = 1
                buttonRow.Controls.Add(command)
                created.Insert(0, command)
                If result = DialogResult.Cancel Then dialog.CancelButton = command
            Next
            Dim defaultIndex As Integer = Math.Min(created.Count - 1, Math.Max(0, CInt(defaultButton)))
            If created.Count > 0 Then dialog.AcceptButton = created(defaultIndex)

            root.Controls.Add(messageRow, 0, 0)
            root.Controls.Add(buttonRow, 0, 1)
            dialog.Controls.Add(root)
            Return If(owner Is Nothing, dialog.ShowDialog(), dialog.ShowDialog(owner))
        End Using
    End Function

    Private Shared Function ButtonResults(buttons As MessageBoxButtons) As DialogResult()
        Select Case buttons
            Case MessageBoxButtons.OKCancel : Return New DialogResult() {DialogResult.OK, DialogResult.Cancel}
            Case MessageBoxButtons.YesNo : Return New DialogResult() {DialogResult.Yes, DialogResult.No}
            Case MessageBoxButtons.YesNoCancel : Return New DialogResult() {DialogResult.Yes, DialogResult.No, DialogResult.Cancel}
            Case MessageBoxButtons.RetryCancel : Return New DialogResult() {DialogResult.Retry, DialogResult.Cancel}
            Case MessageBoxButtons.AbortRetryIgnore : Return New DialogResult() {DialogResult.Abort, DialogResult.Retry, DialogResult.Ignore}
            Case Else : Return New DialogResult() {DialogResult.OK}
        End Select
    End Function

    Private Shared Function ButtonText(result As DialogResult) As String
        Select Case result
            Case DialogResult.OK : Return "OK"
            Case DialogResult.Cancel : Return "Cancel"
            Case DialogResult.Yes : Return "Yes"
            Case DialogResult.No : Return "No"
            Case DialogResult.Retry : Return "Retry"
            Case DialogResult.Abort : Return "Abort"
            Case DialogResult.Ignore : Return "Ignore"
            Case Else : Return result.ToString()
        End Select
    End Function

    Private Shared Function IconGlyph(icon As MessageBoxIcon) As String
        If icon = MessageBoxIcon.Question Then Return "?"
        If icon = MessageBoxIcon.Error Then Return "×"
        If icon = MessageBoxIcon.Warning OrElse icon = MessageBoxIcon.Exclamation Then Return "!"
        Return "i"
    End Function

    Private Shared Function IconColor(icon As MessageBoxIcon) As Color
        If icon = MessageBoxIcon.Question Then Return Color.FromArgb(135, 58, 210)
        If icon = MessageBoxIcon.Error Then Return Color.FromArgb(210, 43, 60)
        If icon = MessageBoxIcon.Warning OrElse icon = MessageBoxIcon.Exclamation Then Return Color.FromArgb(220, 145, 22)
        Return Color.FromArgb(25, 145, 225)
    End Function
End Class
