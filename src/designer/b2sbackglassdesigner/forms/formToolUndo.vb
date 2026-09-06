Imports System

Imports System.Drawing.Drawing2D

Public Class formToolUndo

    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call.
        MyBase.SaveName = Me.Name
        MyBase.DefaultLocation = eDefaultLocation.NE

    End Sub

    Private Sub formToolUndo_Load(sender As Object, e As System.EventArgs) Handles Me.Load

        lbHistory.ItemHeight = If(AppThemeManager.DarkMode, 30, 18)
        lbHistory.DrawMode = DrawMode.OwnerDrawFixed

    End Sub

    Private Sub lbHistory_DrawItem(sender As Object, e As System.Windows.Forms.DrawItemEventArgs) Handles lbHistory.DrawItem

        If e.Index > -1 Then
            Dim item As Undo.UndoEntry = TryCast(lbHistory.Items(e.Index), Undo.UndoEntry)
            Dim selected As Boolean = (e.State And DrawItemState.Selected) = DrawItemState.Selected
            Dim textColor As Color
            If AppThemeManager.DarkMode Then
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
                Using baseBrush As New SolidBrush(Color.FromArgb(1, 3, 6))
                    e.Graphics.FillRectangle(baseBrush, e.Bounds)
                End Using
                Dim accent As Color = If(selected, Color.FromArgb(206, 112, 255), Color.FromArgb(164, 78, 231))
                Dim cardBounds As Rectangle = Rectangle.Inflate(e.Bounds, -3, -2)
                Using path As GraphicsPath = HistoryCardPath(cardBounds, 7)
                    Using rowBrush As New LinearGradientBrush(cardBounds,
                                                              If(selected, Color.FromArgb(76, 24, 107), Color.FromArgb(10, 13, 20)),
                                                              Color.FromArgb(2, 4, 8),
                                                              LinearGradientMode.Vertical)
                        e.Graphics.FillPath(rowBrush, path)
                    End Using
                    Using glow As New Pen(Color.FromArgb(If(selected, 62, 24), accent), 3.0F)
                        e.Graphics.DrawPath(glow, path)
                    End Using
                    Using border As New Pen(Color.FromArgb(If(selected, 225, 100), accent), 1.0F)
                        e.Graphics.DrawPath(border, path)
                    End Using
                End Using
                textColor = If(selected, Color.White, Color.FromArgb(234, 238, 248))
            Else
                e.DrawBackground()
                Using rowBrush As New SolidBrush(Color.White)
                    e.Graphics.FillRectangle(rowBrush, e.Bounds)
                End Using
                textColor = Color.Black
            End If
            If item IsNot Nothing Then
                If item.Image IsNot Nothing Then
                    e.Graphics.DrawImage(item.Image, New Point(7, e.Bounds.Y + Math.Max(1, (e.Bounds.Height - item.Image.Height) \ 2)))
                End If
                Dim textBounds As New Rectangle(e.Bounds.X + 29, e.Bounds.Y, Math.Max(1, e.Bounds.Width - 35), e.Bounds.Height)
                TextRenderer.DrawText(e.Graphics, item.ToString(), Me.Font, textBounds, textColor, TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis)
            End If
            If (e.State And DrawItemState.Focus) = DrawItemState.Focus Then e.DrawFocusRectangle()
        End If

    End Sub

    Private Shared Function HistoryCardPath(bounds As Rectangle, radius As Integer) As GraphicsPath
        Dim path As New GraphicsPath()
        Dim diameter As Integer = Math.Max(2, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)))
        Dim arc As New Rectangle(bounds.Location, New Size(diameter, diameter))
        path.AddArc(arc, 180, 90)
        arc.X = bounds.Right - diameter
        path.AddArc(arc, 270, 90)
        arc.Y = bounds.Bottom - diameter
        path.AddArc(arc, 0, 90)
        arc.X = bounds.Left
        path.AddArc(arc, 90, 90)
        path.CloseFigure()
        Return path
    End Function

End Class
