Imports System
Imports System.Drawing
Imports System.Drawing.Imaging
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
        DrawDMDLayer(e, False)
    End Sub

    Private Sub DrawDMDLayer(e As PaintEventArgs, ByVal behindCanvas As Boolean)
        If B2SData.DMDIlluminations.Count > 0 Then

            If Not B2SData.UseDMDZOrder Then

                ' draw all standard images
                For Each illu As KeyValuePair(Of String, B2SPictureBox) In B2SData.DMDIlluminations
                    If illu.Value.BehindCanvas = behindCanvas AndAlso (illu.Value.Visible OrElse illu.Value.HasPersistentMotionPathSource) Then
                        DrawIlluminationImage(e.Graphics, illu.Value)
                    End If
                Next

            Else

                ' first of all draw zorderd images
                For Each illus As KeyValuePair(Of Integer, B2SPictureBox()) In B2SData.ZOrderDMDImages
                    For Each illu As B2SPictureBox In illus.Value
                        If illu.BehindCanvas = behindCanvas AndAlso (illu.Visible OrElse illu.HasPersistentMotionPathSource) Then
                            DrawIlluminationImage(e.Graphics, illu)
                        End If
                    Next
                Next
                ' now draw all standard images
                For Each illu As KeyValuePair(Of String, B2SPictureBox) In B2SData.DMDIlluminations
                    If illu.Value.BehindCanvas = behindCanvas AndAlso (illu.Value.Visible OrElse illu.Value.HasPersistentMotionPathSource) AndAlso illu.Value.ZOrder = 0 Then
                        DrawIlluminationImage(e.Graphics, illu.Value)
                    End If
                Next

            End If

        End If

    End Sub

    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        ' Only authored behind-canvas objects use this pass. The ordinary
        ' DMD background and legacy foreground retain their existing path.
        Dim hasBehindCanvas As Boolean = False
        For Each entry As KeyValuePair(Of String, B2SPictureBox) In B2SData.DMDIlluminations
            If entry.Value.BehindCanvas Then
                hasBehindCanvas = True
                Exit For
            End If
        Next
        If Not hasBehindCanvas Then
            MyBase.OnPaintBackground(e)
            Return
        End If
        Using backing As New SolidBrush(Me.BackColor)
            e.Graphics.FillRectangle(backing, e.ClipRectangle)
        End Using
        DrawDMDLayer(e, True)
        ' B2SScreen sets DMD backgrounds to Stretch. Paint its alpha over
        ' behind-canvas artwork instead of clearing that artwork a second time.
        If Me.BackgroundImage IsNot Nothing Then e.Graphics.DrawImage(Me.BackgroundImage, Me.ClientRectangle)
    End Sub

    Private Sub DrawIlluminationImage(ByVal graphics As Graphics, ByVal picbox As B2SPictureBox)
        If picbox Is Nothing OrElse picbox.BackgroundImage Is Nothing Then Return
        If picbox.IsMotionPathSourceSeparate AndAlso picbox.MotionPathSourceImage IsNot Nothing Then
            DrawDMDArtwork(graphics, picbox, picbox.MotionPathSourceImage,
                           picbox.MotionPathSourceRectangle, picbox.MotionPathSourceRollAngle, True)
        End If
        If Not picbox.Visible Then Return
        Dim image As Image = picbox.BackgroundImage
        If picbox.PhysicsSelectionMaskAlpha IsNot Nothing Then
            Dim bounds As Rectangle
            Using frame As Bitmap = CreatePhysicsMaskedFrame(picbox, image, bounds)
                graphics.DrawImageUnscaled(frame, bounds.Location)
            End Using
            Return
        End If
        DrawDMDArtwork(graphics, picbox, image, picbox.VisualArtworkBounds, picbox.VisualRotationAngle, False)
    End Sub

    Private Sub DrawDMDArtwork(ByVal graphics As Graphics, ByVal picbox As B2SPictureBox,
                               ByVal image As Image, ByVal bounds As RectangleF, ByVal angle As Single,
                               ByVal persistentSource As Boolean)
        If Not picbox.NativeRotation AndAlso Not picbox.MotionPathRollEnabled Then
            If picbox.HasFullResolutionSnippet Then
                Dim saved As Drawing2D.GraphicsState = graphics.Save()
                Try
                    graphics.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
                    graphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality
                    graphics.DrawImage(image, bounds)
                Finally
                    graphics.Restore(saved)
                End Try
            Else
                graphics.DrawImage(image, bounds)
            End If
            Return
        End If
        Dim state As Drawing2D.GraphicsState = graphics.Save()
        Try
            graphics.SmoothingMode = Drawing2D.SmoothingMode.HighSpeed
            graphics.InterpolationMode = Drawing2D.InterpolationMode.Bilinear
            graphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighSpeed
            Dim destination As PointF()
            If persistentSource Then
                destination = BallRollDestinationPoints(bounds, angle)
            ElseIf picbox.NativeRotation Then
                destination = NativeRotationDestinationPoints(picbox, bounds)
            ElseIf picbox.MotionPathRollEnabled AndAlso Not picbox.NativeRotation Then
                destination = BallRollDestinationPoints(bounds, angle)
            Else
                destination = RotationDestinationPoints(bounds, angle, picbox.RotationPivotX, picbox.RotationPivotY)
            End If
            graphics.DrawImage(image, destination, New RectangleF(0, 0, image.Width, image.Height), GraphicsUnit.Pixel)
        Finally
            graphics.Restore(state)
        End Try
    End Sub

    Private Function CreatePhysicsMaskedFrame(ByVal picbox As B2SPictureBox, ByVal source As Image,
                                               ByRef frameBounds As Rectangle) As Bitmap
        Dim visualBounds As RectangleF = picbox.VisualArtworkBounds
        Dim points As PointF() = BallRollDestinationPoints(visualBounds, picbox.VisualRotationAngle)
        Dim fourth As New PointF(points(1).X + points(2).X - points(0).X, points(1).Y + points(2).Y - points(0).Y)
        Dim left As Single = Math.Min(Math.Min(points(0).X, points(1).X), Math.Min(points(2).X, fourth.X))
        Dim top As Single = Math.Min(Math.Min(points(0).Y, points(1).Y), Math.Min(points(2).Y, fourth.Y))
        Dim right As Single = Math.Max(Math.Max(points(0).X, points(1).X), Math.Max(points(2).X, fourth.X))
        Dim bottom As Single = Math.Max(Math.Max(points(0).Y, points(1).Y), Math.Max(points(2).Y, fourth.Y))
        frameBounds = Rectangle.FromLTRB(CInt(Math.Floor(left)), CInt(Math.Floor(top)),
                                         CInt(Math.Ceiling(right)), CInt(Math.Ceiling(bottom)))
        frameBounds.Width = Math.Max(1, frameBounds.Width)
        frameBounds.Height = Math.Max(1, frameBounds.Height)
        Dim frame As New Bitmap(frameBounds.Width, frameBounds.Height, PixelFormat.Format32bppArgb)
        Try
            Dim localPoints(points.Length - 1) As PointF
            For index As Integer = 0 To points.Length - 1
                localPoints(index) = New PointF(points(index).X - frameBounds.Left, points(index).Y - frameBounds.Top)
            Next
            Using graphics As Graphics = Graphics.FromImage(frame)
                graphics.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
                graphics.DrawImage(source, localPoints, New RectangleF(0, 0, source.Width, source.Height), GraphicsUnit.Pixel)
            End Using
            Dim maskSize As Size = picbox.PhysicsSelectionMaskSize
            Dim maskAlpha As Byte() = picbox.PhysicsSelectionMaskAlpha
            Dim canvasWidth As Integer = Math.Max(1, Me.ClientSize.Width)
            Dim canvasHeight As Integer = Math.Max(1, Me.ClientSize.Height)
            Dim data As BitmapData = frame.LockBits(New Rectangle(Point.Empty, frame.Size), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb)
            Try
                Dim row(frame.Width * 4 - 1) As Byte
                For y As Integer = 0 To frame.Height - 1
                    Dim rowPointer As IntPtr = IntPtr.Add(data.Scan0, y * data.Stride)
                    Runtime.InteropServices.Marshal.Copy(rowPointer, row, 0, row.Length)
                    Dim maskY As Integer = CInt(Math.Floor((frameBounds.Top + y + 0.5F) * maskSize.Height / canvasHeight))
                    For x As Integer = 0 To frame.Width - 1
                        Dim maskX As Integer = CInt(Math.Floor((frameBounds.Left + x + 0.5F) * maskSize.Width / canvasWidth))
                        Dim alpha As Integer = 0
                        If maskX >= 0 AndAlso maskY >= 0 AndAlso maskX < maskSize.Width AndAlso maskY < maskSize.Height Then
                            alpha = maskAlpha(maskY * maskSize.Width + maskX)
                        End If
                        row(x * 4 + 3) = CByte((CInt(row(x * 4 + 3)) * alpha + 127) \ 255)
                    Next
                    Runtime.InteropServices.Marshal.Copy(row, 0, rowPointer, row.Length)
                Next
            Finally
                frame.UnlockBits(data)
            End Try
            Return frame
        Catch
            frame.Dispose()
            Throw
        End Try
    End Function

    Private Function RotationDestinationPoints(ByVal rect As RectangleF, ByVal angle As Single,
                                               Optional ByVal pivotX As Single = 0.5F,
                                               Optional ByVal pivotY As Single = 0.5F) As PointF()
        Dim radians As Double = angle * Math.PI / 180.0R
        Dim cosine As Double = Math.Cos(radians)
        Dim sine As Double = Math.Sin(radians)
        Dim centerX As Single = rect.Left + rect.Width * pivotX
        Dim centerY As Single = rect.Top + rect.Height * pivotY
        Return New PointF() {
            RotationDestinationPoint(centerX, centerY, rect.Width, rect.Height, -pivotX, -pivotY, cosine, sine),
            RotationDestinationPoint(centerX, centerY, rect.Width, rect.Height, 1.0R - pivotX, -pivotY, cosine, sine),
            RotationDestinationPoint(centerX, centerY, rect.Width, rect.Height, -pivotX, 1.0R - pivotY, cosine, sine)
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
        Dim pixelX As Double = width * x
        Dim pixelY As Double = height * y
        Return New PointF(CSng(centerX + pixelX * cosine - pixelY * sine),
                          CSng(centerY + pixelX * sine + pixelY * cosine))
    End Function

    Private Function NativeRotationDestinationPoints(ByVal picbox As B2SPictureBox,
                                                     ByVal rect As RectangleF) As PointF()
        If picbox.Width <= 0 OrElse picbox.Height <= 0 Then
            Return RotationDestinationPoints(rect, picbox.VisualRotationAngle,
                                             picbox.RotationPivotX, picbox.RotationPivotY)
        End If
        ' The designer rotates the authored image before the backglass is resized.
        ' Applying the unequal screen scales first changes both its arc and shape.
        Dim angle As Double = picbox.VisualRotationAngle * Math.PI / 180.0R
        Dim cosine As Double = Math.Cos(angle), sine As Double = Math.Sin(angle)
        Dim scaleX As Double = rect.Width / picbox.Width, scaleY As Double = rect.Height / picbox.Height
        Dim hingeX As Double = rect.Left + rect.Width * picbox.RotationPivotX
        Dim hingeY As Double = rect.Top + rect.Height * picbox.RotationPivotY
        Dim left As Double = -picbox.Width * picbox.RotationPivotX
        Dim right As Double = picbox.Width * (1.0R - picbox.RotationPivotX)
        Dim top As Double = -picbox.Height * picbox.RotationPivotY
        Dim bottom As Double = picbox.Height * (1.0R - picbox.RotationPivotY)
        Return New PointF() {
            New PointF(CSng(hingeX + (left * cosine - top * sine) * scaleX),
                       CSng(hingeY + (left * sine + top * cosine) * scaleY)),
            New PointF(CSng(hingeX + (right * cosine - top * sine) * scaleX),
                       CSng(hingeY + (right * sine + top * cosine) * scaleY)),
            New PointF(CSng(hingeX + (left * cosine - bottom * sine) * scaleX),
                       CSng(hingeY + (left * sine + bottom * cosine) * scaleY))
        }
    End Function

    Private Sub formDMD_MouseClick(sender As Object, e As MouseEventArgs) Handles MyBase.MouseClick
        If e.Button = Windows.Forms.MouseButtons.Right Then
            B2SScreen.formBackglass.formBackglass_MouseClick(sender, e)
        End If
    End Sub

#End Region

End Class
