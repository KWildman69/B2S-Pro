Imports System
Imports System.Runtime.InteropServices

Public Class formSetLEDColor

    <DllImport("gdi32.dll")>
    Private Shared Function CreateDC(ByVal lpszDriver As String, ByVal lpszDevice As String, ByVal lpszOutput As String, ByVal lpInitData As IntPtr) As IntPtr
    End Function
    <DllImport("gdi32.dll")> _
    Private Shared Function DeleteDC(ByVal hdc As IntPtr) As Boolean
    End Function
    <DllImport("gdi32.dll")> _
    Private Shared Function GetPixel(ByVal hdc As IntPtr, ByVal nXPos As Integer, ByVal nYPos As Integer) As Integer
    End Function
    <DllImport("user32.dll")> _
    Private Shared Function GetAsyncKeyState(ByVal vKey As Int32) As Short
    End Function

    Public Class LEDColorEventArgs
        Inherits System.EventArgs

        Public Color As Color = Nothing

        Public Sub New(ByVal _color As Color)
            Color = _color
        End Sub
    End Class
    Public Event ColorChanged(ByVal sender As Object, ByVal e As LEDColorEventArgs)

    Private IsDirty As Boolean = False

    Private wait4MouseUp As Boolean = False

    Public Shadows Function ShowDialog(ByVal owner As IWin32Window,
                                       ByRef reelColor As Color,
                                       Optional ByVal wait4MouseUp As Boolean = False) As DialogResult
        B2SColorBarLEDs.CurrentColor = reelColor
        Me.wait4MouseUp = wait4MouseUp
        ' now show the dialog
        Dim nRet As DialogResult = MyBase.ShowDialog(owner)
        If nRet = Windows.Forms.DialogResult.OK Then
            reelColor = B2SColorBarLEDs.CurrentColor
        End If
        Return nRet
    End Function

    Private Sub formSetLEDColor_Shown(sender As Object, e As System.EventArgs) Handles Me.Shown
        IsDirty = False
    End Sub

    Private Sub B2SColorBarLEDs_ColorChanged(sender As Object, e As System.EventArgs) Handles B2SColorBarLEDs.ColorChanged
        If Not wait4MouseUp Then
            RaiseEvent ColorChanged(Me, New LEDColorEventArgs(B2SColorBarLEDs.CurrentColor))
        End If
    End Sub
    Private Sub B2SColorBarLEDs_TextChanged(sender As Object, e As System.EventArgs) Handles B2SColorBarLEDs.TextChanged
        If wait4MouseUp Then
            RaiseEvent ColorChanged(Me, New LEDColorEventArgs(B2SColorBarLEDs.CurrentColor))
        End If
    End Sub
    Private Sub B2SColorBarLEDs_MouseUp(sender As Object, e As System.Windows.Forms.MouseEventArgs) Handles B2SColorBarLEDs.MouseUp
        If wait4MouseUp Then
            RaiseEvent ColorChanged(Me, New LEDColorEventArgs(B2SColorBarLEDs.CurrentColor))
        End If
    End Sub

    Private Sub GetColor_Click(sender As System.Object, e As System.EventArgs) Handles btnGetColor.Click
        Static msgboxShown As Boolean = False
        If TimerGetColor.Enabled Then
            TimerGetColor.Stop()
            Me.CancelButton = btnCancel
        Else
            Me.CancelButton = Nothing
            If Not msgboxShown Then ShowThemedColorPickerInstructions()
            msgboxShown = True
            TimerGetColor.Start()
        End If
    End Sub

    Private Sub ShowThemedColorPickerInstructions()
        Using dialog As New B2SThemedForm()
            dialog.Name = "formLEDColorInstructions"
            dialog.Text = AppTitle
            dialog.StartPosition = FormStartPosition.CenterParent
            dialog.FormBorderStyle = FormBorderStyle.FixedDialog
            dialog.ClientSize = New Size(430, 150)
            dialog.MinimumSize = dialog.Size
            dialog.MaximumSize = dialog.Size
            dialog.ShowInTaskbar = False
            dialog.MinimizeBox = False
            dialog.MaximizeBox = False
            dialog.BackColor = Color.FromArgb(2, 4, 8)
            dialog.ForeColor = Color.White
            dialog.Font = New Font("Segoe UI", 9.0F, FontStyle.Regular)

            Dim informationIcon As New Label With {
                .Text = "i",
                .TextAlign = ContentAlignment.MiddleCenter,
                .Font = New Font("Segoe UI", 14.0F, FontStyle.Bold),
                .ForeColor = Color.White,
                .BackColor = Color.FromArgb(25, 145, 225),
                .Size = New Size(34, 34),
                .Location = New Point(20, 27)
            }

            Dim instructions As New Label With {
                .Text = My.Resources.MSG_GetColor,
                .AutoSize = False,
                .Location = New Point(70, 22),
                .Size = New Size(337, 54),
                .TextAlign = ContentAlignment.MiddleLeft,
                .ForeColor = Color.White,
                .BackColor = Color.Transparent
            }

            Dim okButton As New Button With {
                .Text = "OK",
                .DialogResult = DialogResult.OK,
                .Size = New Size(86, 30),
                .Location = New Point(321, 92),
                .FlatStyle = FlatStyle.Flat,
                .BackColor = Color.FromArgb(15, 27, 48),
                .ForeColor = Color.White,
                .UseVisualStyleBackColor = False
            }
            okButton.FlatAppearance.BorderColor = Color.FromArgb(35, 190, 255)
            okButton.FlatAppearance.BorderSize = 1

            dialog.Controls.Add(informationIcon)
            dialog.Controls.Add(instructions)
            dialog.Controls.Add(okButton)
            dialog.AcceptButton = okButton
            dialog.CancelButton = okButton
            dialog.ShowDialog(Me)
        End Using
    End Sub
    Private Sub TimerGetColor_Tick(sender As Object, e As System.EventArgs) Handles TimerGetColor.Tick
        Dim keyState As Short = GetAsyncKeyState(Keys.Escape)
        If keyState <> 0 Then
            TimerGetColor.Stop()
            Me.CancelButton = btnCancel
        Else
            B2SColorBarLEDs.CurrentColor = GetPixelColor(Cursor.Position.X, Cursor.Position.Y)
        End If
    End Sub
    Private Function GetPixelColor(ByVal x As Integer, ByVal y As Integer) As Color
        Dim hdcScreen As IntPtr = CreateDC("Display", Nothing, Nothing, IntPtr.Zero)
        Dim colorRef As Integer = GetPixel(hdcScreen, x, y)
        DeleteDC(hdcScreen)
        Return Color.FromArgb(colorRef And &HFF, (colorRef And &HFF00) >> 8, (colorRef And &HFF0000) >> 16)
    End Function

    Private Sub Ok_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnOk.Click
        MyBase.DialogResult = Windows.Forms.DialogResult.OK
        Me.Close()
    End Sub
    Private Sub Cancel_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnCancel.Click
        If IsDirty Then
            Dim ret As DialogResult = B2SMessageBox.Show(My.Resources.MSG_IsDirty, AppTitle, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question)
            If ret = Windows.Forms.DialogResult.Yes Then
                btnOk.PerformClick()
            ElseIf ret = Windows.Forms.DialogResult.No Then
                Me.Close()
            End If
        Else
            Me.Close()
        End If
    End Sub

End Class
