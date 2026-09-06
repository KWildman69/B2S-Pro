Imports System.Drawing.Drawing2D
Imports System.IO
Imports System.Reflection

Public NotInheritable Class formSplash
    Inherits Form

    Private ReadOnly logoImage As Image
    Private ReadOnly animationTimer As Timer
    Private ReadOnly animationClock As Stopwatch

    Public Sub New()
        Me.AutoScaleMode = AutoScaleMode.Dpi
        Me.ClientSize = New Size(990, 495)
        Me.FormBorderStyle = FormBorderStyle.None
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.ShowInTaskbar = False
        Me.TopMost = True
        Me.BackColor = Color.FromArgb(1, 3, 8)
        Me.DoubleBuffered = True
        Me.Opacity = 0.0R

        logoImage = LoadLogo()

        animationClock = Stopwatch.StartNew()
        animationTimer = New Timer() With {.Interval = 16}
        AddHandler animationTimer.Tick, AddressOf AnimateSplash
        animationTimer.Start()
    End Sub

    Protected Overrides Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        animationClock.Restart()
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        If Me.ClientSize.Width <= 0 OrElse Me.ClientSize.Height <= 0 Then Return
        Using shape As GraphicsPath = RoundedRectangle(New Rectangle(Point.Empty, Me.ClientSize), 33)
            Me.Region = New Region(shape)
        End Using
    End Sub

    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        Dim bounds As New Rectangle(Point.Empty, Me.ClientSize)
        If bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
        Dim pulse As Double = PulseStrength(animationClock.Elapsed.TotalMilliseconds)
        Using background As New LinearGradientBrush(bounds,
                                                     Color.FromArgb(12 + CInt(pulse * 16.0R), 8, 22 + CInt(pulse * 22.0R)),
                                                     Color.FromArgb(1, 4, 10),
                                                     LinearGradientMode.Vertical)
            e.Graphics.FillRectangle(background, bounds)
        End Using

        Dim borderBounds As New Rectangle(2, 2, bounds.Width - 5, bounds.Height - 5)
        Using borderPath As GraphicsPath = RoundedRectangle(borderBounds, 32)
            Using glow As New Pen(Color.FromArgb(70 + CInt(pulse * 105.0R), 100, 39, 255), 7.5F + CSng(pulse * 5.0R))
                e.Graphics.DrawPath(glow, borderPath)
            End Using
            Using border As New LinearGradientBrush(borderBounds,
                                                     Color.FromArgb(0, 190, 255),
                                                     Color.FromArgb(235, 44, 255),
                                                     LinearGradientMode.Horizontal)
                Using borderPen As New Pen(border, 2.1F)
                    e.Graphics.DrawPath(borderPen, borderPath)
                End Using
            End Using
        End Using

    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        If logoImage Is Nothing Then Return

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic
        e.Graphics.CompositingQuality = CompositingQuality.HighQuality

        Dim elapsed As Double = animationClock.Elapsed.TotalMilliseconds
        Dim pulse As Double = PulseStrength(elapsed)
        Dim baseBounds As RectangleF = CalculateLogoBounds()
        Dim scale As Single = CSng(1.0R + (pulse * 0.045R))
        Dim logoBounds As New RectangleF(baseBounds.X - (baseBounds.Width * (scale - 1.0F) / 2.0F),
                                         baseBounds.Y - (baseBounds.Height * (scale - 1.0F) / 2.0F),
                                         baseBounds.Width * scale,
                                         baseBounds.Height * scale)

        Dim glowAlpha As Integer = 35 + CInt(pulse * 145.0R)
        Dim glowBounds As RectangleF = RectangleF.Inflate(logoBounds, 7.0F + CSng(pulse * 12.0R), 5.0F + CSng(pulse * 9.0R))
        Using glowBrush As New LinearGradientBrush(glowBounds,
                                                   Color.FromArgb(glowAlpha, 0, 185, 255),
                                                   Color.FromArgb(glowAlpha, 235, 42, 255),
                                                   LinearGradientMode.Horizontal)
            Using outerGlow As New Pen(glowBrush, 8.0F + CSng(pulse * 12.0R))
                e.Graphics.DrawEllipse(outerGlow, glowBounds)
            End Using
        End Using

        e.Graphics.DrawImage(logoImage, logoBounds)
        DrawLightSweep(e.Graphics, logoBounds, elapsed)
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            If animationTimer IsNot Nothing Then animationTimer.Dispose()
            If animationClock IsNot Nothing Then animationClock.Stop()
            If logoImage IsNot Nothing Then logoImage.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub

    Private Sub AnimateSplash(sender As Object, e As EventArgs)
        Dim elapsed As Double = animationClock.Elapsed.TotalMilliseconds
        If elapsed < 320.0R Then
            Me.Opacity = Math.Min(1.0R, elapsed / 320.0R)
        ElseIf elapsed > 2550.0R Then
            Me.Opacity = Math.Max(0.0R, (3000.0R - elapsed) / 450.0R)
        Else
            Me.Opacity = 1.0R
        End If
        Me.Invalidate()
    End Sub

    Private Function CalculateLogoBounds() As RectangleF
        Dim available As New RectangleF(63.0F, 48.0F, Me.ClientSize.Width - 126.0F, Me.ClientSize.Height - 129.0F)
        Dim factor As Single = Math.Min(available.Width / logoImage.Width, available.Height / logoImage.Height)
        Dim width As Single = logoImage.Width * factor
        Dim height As Single = logoImage.Height * factor
        Return New RectangleF(available.X + ((available.Width - width) / 2.0F),
                              available.Y + ((available.Height - height) / 2.0F),
                              width,
                              height)
    End Function

    Private Sub DrawLightSweep(graphics As Graphics, logoBounds As RectangleF, elapsed As Double)
        If elapsed < 320.0R OrElse elapsed > 1580.0R Then Return
        Dim sweepProgress As Single = Math.Min(1.0F, CSng((elapsed - 320.0R) / 1260.0R))
        Dim bandWidth As Single = Math.Max(90.0F, logoBounds.Width * 0.16F)
        Dim sweepX As Single = logoBounds.Left - (bandWidth * 1.5F) + ((logoBounds.Width + (bandWidth * 3.0F)) * sweepProgress)
        Dim sweepBounds As New RectangleF(sweepX, logoBounds.Top - logoBounds.Height, bandWidth, logoBounds.Height * 3.0F)

        Dim state As GraphicsState = graphics.Save()
        Try
            Using clipPath As New GraphicsPath()
                clipPath.AddEllipse(logoBounds)
                graphics.SetClip(clipPath, CombineMode.Intersect)
            End Using
            Dim centerX As Single = logoBounds.Left + (logoBounds.Width / 2.0F)
            Dim centerY As Single = logoBounds.Top + (logoBounds.Height / 2.0F)
            graphics.TranslateTransform(centerX, centerY)
            graphics.RotateTransform(-14.0F)
            graphics.TranslateTransform(-centerX, -centerY)

            Using sweepBrush As New LinearGradientBrush(sweepBounds,
                                                        Color.Transparent,
                                                        Color.Transparent,
                                                        LinearGradientMode.Horizontal)
                sweepBrush.InterpolationColors = New ColorBlend() With {
                    .Colors = New Color() {
                        Color.FromArgb(0, Color.White),
                        Color.FromArgb(28, 90, 210, 255),
                        Color.FromArgb(155, Color.White),
                        Color.FromArgb(28, 255, 90, 225),
                        Color.FromArgb(0, Color.White)
                    },
                    .Positions = New Single() {0.0F, 0.2F, 0.5F, 0.8F, 1.0F}
                }
                graphics.FillRectangle(sweepBrush, sweepBounds)
            End Using
        Finally
            graphics.Restore(state)
        End Try
    End Sub

    Private Shared Function PulseStrength(elapsed As Double) As Double
        ' Two separate impact pulses near the end: boom, boom, then fade out.
        Return Math.Max(SinglePulse(elapsed, 1660.0R, 1980.0R),
                        SinglePulse(elapsed, 2070.0R, 2390.0R))
    End Function

    Private Shared Function SinglePulse(elapsed As Double, pulseStart As Double, pulseEnd As Double) As Double
        If elapsed <= pulseStart OrElse elapsed >= pulseEnd Then Return 0.0R
        Dim position As Double = (elapsed - pulseStart) / (pulseEnd - pulseStart)
        Return Math.Sin(position * Math.PI)
    End Function

    Private Shared Function LoadLogo() As Image
        Try
            Using stream As Stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("B2SProLogo.png")
                If stream IsNot Nothing Then
                    Using source As Image = Image.FromStream(stream)
                        Return New Bitmap(source)
                    End Using
                End If
            End Using
        Catch
            ' The splash must never prevent the application from starting.
        End Try
        Return Nothing
    End Function

    Private Shared Function RoundedRectangle(bounds As Rectangle, radius As Integer) As GraphicsPath
        Dim path As New GraphicsPath()
        Dim diameter As Integer = Math.Max(2, radius * 2)
        Dim arc As New Rectangle(bounds.X, bounds.Y, diameter, diameter)
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
