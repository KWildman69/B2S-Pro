Imports System.Drawing
Imports System.Windows.Forms

' View transforms only: authored points and sizes remain in image coordinates.
Public MustInherit Class EditorZoomCanvas
    Inherits Control

    Private zoom As Single = 1.0F
    Private pan As PointF
    Private navigating As Boolean
    Private startMouse As Point
    Private startPan As PointF
    Private oldCursor As Cursor

    <Runtime.InteropServices.DllImport("user32.dll")>
    Private Shared Function GetAsyncKeyState(key As Integer) As Short
    End Function

    Public Shared Function IsSpaceHeld() As Boolean
        Return (CInt(GetAsyncKeyState(CInt(Keys.Space))) And &H8000) <> 0
    End Function

    Protected MustOverride Function BaseImageView() As RectangleF

    Protected Function ZoomedImageView() As RectangleF
        Dim fit As RectangleF = BaseImageView()
        Dim width As Single = fit.Width * zoom
        Dim height As Single = fit.Height * zoom
        Return New RectangleF(fit.X + (fit.Width - width) / 2 + pan.X,
                              fit.Y + (fit.Height - height) / 2 + pan.Y, width, height)
    End Function

    Protected Overrides Sub OnMouseEnter(e As EventArgs)
        MyBase.OnMouseEnter(e)
        Focus()
    End Sub

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        Dim before As RectangleF = ZoomedImageView()
        If before.Width <= 0 OrElse before.Height <= 0 OrElse navigating Then Return
        Dim nextZoom As Single = CSng(Math.Max(0.1R, Math.Min(20.0R, zoom * Math.Pow(1.2R, e.Delta / 120.0R))))
        Dim relativeX As Single = (e.X - before.X) / before.Width
        Dim relativeY As Single = (e.Y - before.Y) / before.Height
        zoom = nextZoom
        Dim after As RectangleF = ZoomedImageView()
        pan = New PointF(pan.X + e.X - after.X - relativeX * after.Width,
                         pan.Y + e.Y - after.Y - relativeY * after.Height)
        Dim handled As HandledMouseEventArgs = TryCast(e, HandledMouseEventArgs)
        If handled IsNot Nothing Then handled.Handled = True
        Invalidate()
    End Sub

    Protected Function BeginNavigation(e As MouseEventArgs) As Boolean
        If e.Button <> MouseButtons.Middle AndAlso Not (e.Button = MouseButtons.Left AndAlso IsSpaceHeld()) Then Return False
        Focus()
        navigating = True
        startMouse = e.Location
        startPan = pan
        oldCursor = Cursor
        Cursor = Cursors.Hand
        Capture = True
        Return True
    End Function

    Protected Function MoveNavigation(e As MouseEventArgs) As Boolean
        If Not navigating Then Return False
        pan = New PointF(startPan.X + e.X - startMouse.X, startPan.Y + e.Y - startMouse.Y)
        Invalidate()
        Return True
    End Function

    Protected Function EndNavigation() As Boolean
        If Not navigating Then Return False
        navigating = False
        Cursor = oldCursor
        Capture = False
        Return True
    End Function

    Protected Overrides Sub OnMouseCaptureChanged(e As EventArgs)
        If Not Capture Then EndNavigation()
        MyBase.OnMouseCaptureChanged(e)
    End Sub

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        If e.Control AndAlso e.KeyCode = Keys.D0 Then
            zoom = 1.0F
            pan = PointF.Empty
            Invalidate()
            e.SuppressKeyPress = True
        End If
        MyBase.OnKeyDown(e)
    End Sub
End Class

' PictureBox disables selection by default, so TabStop alone cannot give it
' keyboard focus for wheel zoom and Space-drag in the image editing dialogs.
Public Class EditorPreviewPictureBox
    Inherits PictureBox
    Public Sub New()
        SetStyle(ControlStyles.Selectable, True)
        TabStop = True
    End Sub
End Class
