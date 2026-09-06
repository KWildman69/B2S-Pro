Imports System.Windows.Forms

''' <summary>
''' Applies the permanent B2S Pro appearance before a window becomes visible.
''' WinForms otherwise paints the designer's legacy light colors once and the
''' application's idle-time theme pass replaces them a moment later.
''' </summary>
Public Class B2SThemedForm
    Inherits Form

    Private preparingFirstShow As Boolean

    Public Sub New()
        ' The custom neon frame follows the outer edge of a resizable window.
        ' Repaint the complete buffered surface at each intermediate size so a
        ' previous frame is never left behind as a resize trail.
        SetStyle(ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.ResizeRedraw, True)
        UpdateStyles()

    End Sub

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)

        ' Most B2S editors are modal, so the application-wide open-form scan
        ' deliberately does not see them. Attach every themed editor here after
        ' its own Load work has finished, which restores and saves its exact
        ' size, position, monitor, DPI, and maximized state. Transient message
        ' boxes remain content-sized and centered for the current question.
        If Not String.Equals(Name, "formB2SMessage", StringComparison.OrdinalIgnoreCase) Then
            WindowStateManager.Attach(Me)
        End If
    End Sub

    Protected Overrides Sub SetVisibleCore(value As Boolean)
        If value AndAlso Not preparingFirstShow Then
            preparingFirstShow = True
            Try
                AppThemeManager.Initialize()
                AppThemeManager.ApplyToForm(Me)
            Finally
                preparingFirstShow = False
            End Try
        End If

        MyBase.SetVisibleCore(value)
    End Sub
End Class
