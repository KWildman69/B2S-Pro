Imports System
Imports System.Drawing
Imports System.Windows.Forms

Public Class formDMD
    Private Const MA_NOACTIVATE As System.Int32 = 3
    Private Const WM_MOUSEACTIVATE As Integer = &H21
#Region " Properties "


    Protected Overrides Sub WndProc(ByRef m As Message)
        'Don't allow the window to be activated by swallowing the mouse event.
        If B2SSettings.FormNoFocus And m.Msg = WM_MOUSEACTIVATE Then
            m.Result = New IntPtr(MA_NOACTIVATE)
            Return
        End If
        MyBase.WndProc(m)
    End Sub
#End Region 'Properties

#Region "constructor"

    Public Sub New()

        InitializeComponent()

        ' set some styles
        'Me.SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or ControlStyles.OptimizedDoubleBuffer, True)
        Me.DoubleBuffered = True

    End Sub

#End Region

#Region "painting"

    Private Sub formDMD_Paint(sender As Object, e As System.Windows.Forms.PaintEventArgs) Handles Me.Paint

        If B2SData.DMDIlluminations.Count > 0 Then

            If Not B2SData.UseDMDZOrder Then

                ' draw all standard images
                For Each illu As KeyValuePair(Of String, B2SPictureBox) In B2SData.DMDIlluminations
                    If illu.Value.Visible OrElse illu.Value.HasPersistentMotionPathSource Then
                        DrawIlluminationImage(e.Graphics, illu.Value)
                    End If
                Next

            Else

                ' first of all draw zorderd images
                For Each illus As KeyValuePair(Of Integer, B2SPictureBox()) In B2SData.ZOrderDMDImages
                    For Each illu As B2SPictureBox In illus.Value
                        If illu.Visible OrElse illu.HasPersistentMotionPathSource Then
                            DrawIlluminationImage(e.Graphics, illu)
                        End If
                    Next
                Next
                ' now draw all standard images
                For Each illu As KeyValuePair(Of String, B2SPictureBox) In B2SData.DMDIlluminations
                    If (illu.Value.Visible OrElse illu.Value.HasPersistentMotionPathSource) AndAlso illu.Value.ZOrder = 0 Then
                        DrawIlluminationImage(e.Graphics, illu.Value)
                    End If
                Next

            End If

        End If

    End Sub

    Private Sub DrawIlluminationImage(ByVal graphics As System.Drawing.Graphics, ByVal picbox As B2SPictureBox)
        If picbox Is Nothing OrElse picbox.BackgroundImage Is Nothing Then Return
        If picbox.IsMotionPathSourceSeparate Then
            Dim sourceImage As Image = picbox.MotionPathSourceImage
            If sourceImage Is Nothing Then Return
            If picbox.MotionPathRollEnabled AndAlso Math.Abs(picbox.MotionPathSourceRollAngle) > 0.001F Then
                DrawRotatedImage(graphics, sourceImage, picbox.MotionPathSourceRectangle, picbox.MotionPathSourceRollAngle, True)
            Else
                graphics.DrawImage(sourceImage, picbox.MotionPathSourceRectangle)
            End If
        End If
        If Not picbox.Visible Then Return
        If Not picbox.NativeRotation AndAlso Not picbox.MotionPathRollEnabled Then
            graphics.DrawImage(picbox.BackgroundImage, picbox.RectangleF)
            Return
        End If
        DrawRotatedImage(graphics, picbox.BackgroundImage, picbox.RectangleF, picbox.VisualRotationAngle,
                         picbox.MotionPathRollEnabled AndAlso Not picbox.NativeRotation)
    End Sub

    Private Sub DrawRotatedImage(ByVal graphics As System.Drawing.Graphics, ByVal image As Image,
                                 ByVal bounds As RectangleF, ByVal angle As Single,
                                 Optional ByVal preserveBallShape As Boolean = False)
        Dim state As System.Drawing.Drawing2D.GraphicsState = graphics.Save()
        Try
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic
            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality
            Dim destination As PointF() = If(preserveBallShape,
                                             BallRollDestinationPoints(bounds, angle),
                                             RotationDestinationPoints(bounds, angle))
            graphics.DrawImage(image, destination,
                               New RectangleF(0, 0, image.Width, image.Height),
                               GraphicsUnit.Pixel)
        Finally
            graphics.Restore(state)
        End Try
    End Sub

    Private Function RotationDestinationPoints(ByVal rect As RectangleF, ByVal angle As Single) As PointF()
        Dim radians As Double = angle * Math.PI / 180.0R
        Dim cosine As Double = Math.Cos(radians)
        Dim sine As Double = Math.Sin(radians)
        Dim centerX As Single = rect.Left + rect.Width / 2.0F
        Dim centerY As Single = rect.Top + rect.Height / 2.0F
        Return New PointF() {
            RotationDestinationPoint(centerX, centerY, rect.Width, rect.Height, -0.5R, -0.5R, cosine, sine),
            RotationDestinationPoint(centerX, centerY, rect.Width, rect.Height, 0.5R, -0.5R, cosine, sine),
            RotationDestinationPoint(centerX, centerY, rect.Width, rect.Height, -0.5R, 0.5R, cosine, sine)
        }
    End Function

    Private Function BallRollDestinationPoints(ByVal rect As RectangleF, ByVal angle As Single) As PointF()
        Dim radians As Double = angle * Math.PI / 180.0R
        Dim cosine As Double = Math.Cos(radians)
        Dim sine As Double = Math.Sin(radians)
        Dim centerX As Single = rect.Left + rect.Width / 2.0F
        Dim centerY As Single = rect.Top + rect.Height / 2.0F
        Return New PointF() {
            BallRollDestinationPoint(centerX, centerY, rect.Width, rect.Height, -0.5R, -0.5R, cosine, sine),
            BallRollDestinationPoint(centerX, centerY, rect.Width, rect.Height, 0.5R, -0.5R, cosine, sine),
            BallRollDestinationPoint(centerX, centerY, rect.Width, rect.Height, -0.5R, 0.5R, cosine, sine)
        }
    End Function

    Private Function BallRollDestinationPoint(ByVal centerX As Single, ByVal centerY As Single,
                                              ByVal width As Single, ByVal height As Single,
                                              ByVal x As Double, ByVal y As Double,
                                              ByVal cosine As Double, ByVal sine As Double) As PointF
        Return New PointF(CSng(centerX + width * (x * cosine - y * sine)),
                          CSng(centerY + height * (x * sine + y * cosine)))
    End Function

    Private Function RotationDestinationPoint(ByVal centerX As Single, ByVal centerY As Single,
                                              ByVal width As Single, ByVal height As Single,
                                              ByVal x As Double, ByVal y As Double,
                                              ByVal cosine As Double, ByVal sine As Double) As PointF
        Return New PointF(CSng(centerX + width * (x * cosine - y * sine)),
                          CSng(centerY + height * (x * sine + y * cosine)))
    End Function

    Private Sub formDMD_MouseClick(sender As Object, e As MouseEventArgs) Handles MyBase.MouseClick
        If e.Button = Windows.Forms.MouseButtons.Right Then
            B2SScreen.formBackglass.formBackglass_MouseClick(sender, e)
        End If
    End Sub

#End Region

End Class
