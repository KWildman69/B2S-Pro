Imports System

Public Class B2SLine

    Inherits Control

    Protected Overrides Sub OnPaint(e As System.Windows.Forms.PaintEventArgs)
        MyBase.OnPaint(e)

        e.Graphics.SmoothingMode = Drawing2D.SmoothingMode.HighQuality

        ' draw the horizontal line with or without text
        Dim y As Integer = CInt(Me.Height / 2)
        Using linePen As New Pen(Me.ForeColor, 1.0F)
            If String.IsNullOrEmpty(Me.Text) Then
                e.Graphics.DrawLine(linePen, 0, y, Me.Width - 1, y)
            Else
                Dim drawFont As Font = If(Me.Font, SystemFonts.DefaultFont)
                TextRenderer.DrawText(e.Graphics,
                                      Me.Text,
                                      drawFont,
                                      New Point(0, Math.Max(0, y - (drawFont.Height \ 2))),
                                      Me.ForeColor,
                                      TextFormatFlags.NoPrefix Or TextFormatFlags.SingleLine)
            End If
        End Using
    End Sub

    Public Sub New()
        Me.SetStyle(ControlStyles.ResizeRedraw, True)
        Me.DoubleBuffered = True

        Me.Enabled = False
    End Sub

End Class
