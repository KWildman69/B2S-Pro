Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Threading.Tasks
Imports System.Windows.Forms

Public Class B2SPictureBox

    Inherits B2SBaseBox

    Public Enum ePictureBoxType
        StandardImage = 0
        SelfRotatingImage = 1
        MechRotatingImage = 2
    End Enum
    Public Enum eSnippitRotationDirection
        Clockwise = 0
        AntiClockwise = 1
    End Enum
    Public Enum eSnippitRotationStopBehaviour
        SpinOff = 0
        StopImmediatelly = 1
        RunAnimationTillEnd = 2
        RunAnimationToFirstStep = 3
    End Enum


    Protected Overrides Sub OnPaint(e As System.Windows.Forms.PaintEventArgs)
        ' rectangle area for painting
        Dim rect As Rectangle = New Rectangle(0, 0, Me.Width - 1, Me.Height - 1)

        ' draw dashed frame
        Dim pen As Pen = New Pen(Brushes.LightGray)
        pen.DashPattern = New Single() {3.0F, 3.0F}
        e.Graphics.DrawRectangle(pen, rect)
        pen.Dispose()

        ' draw text
        'If Not String.IsNullOrEmpty(Me.Text) Then
        '	TextRenderer.DrawText(e.Graphics, Me.Text, Me.Font, rect, Color.White, TextFormatFlags.WordBreak Or TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter)
        'End If
    End Sub

    Public Sub New()
        ' set some drawing styles
        Me.SetStyle(ControlStyles.SupportsTransparentBackColor, True)
        'Me.SetStyle(ControlStyles.ResizeRedraw Or ControlStyles.SupportsTransparentBackColor, True)
        'Me.DoubleBuffered = True
        Me.SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or ControlStyles.DoubleBuffer, True)

        ' backcolor needs to be transparent
        Me.BackColor = Color.Transparent

        ' do not show the control
        MyBase.Visible = False

        nativeRotationTimer = New Timer()
        AddHandler nativeRotationTimer.Tick, AddressOf NativeRotationTimer_Tick
        motionPathTimer = New Timer() With {.Interval = 16}
        AddHandler motionPathTimer.Tick, AddressOf MotionPathTimer_Tick
        motionPathSourceRespawnTimer = New Timer() With {.Interval = 16}
        AddHandler motionPathSourceRespawnTimer.Tick, AddressOf MotionPathSourceRespawnTimer_Tick
        pivotRotationTimer = New Timer() With {.Interval = 16}
        AddHandler pivotRotationTimer.Tick, AddressOf PivotRotationTimer_Tick
        flasherPulseTimer = New Timer()
        AddHandler flasherPulseTimer.Tick, AddressOf FlasherPulseTimer_Tick
    End Sub

    Public Property PictureBoxType() As ePictureBoxType = ePictureBoxType.StandardImage

    Public Property GroupName() As String = String.Empty

    Public Property Intensity() As Integer = 1
    Public Property InitialState() As Integer = 0
    Public Property DualMode() As B2SData.eDualMode = B2SData.eDualMode.Both
    Public Property ZOrder() As Integer = 0

    Public Property IsImageSnippit() As Boolean = False
    Public Property PreservePhysicsArtworkAspect() As Boolean = False
    Public ReadOnly Property VisualArtworkBounds As RectangleF
        Get
            Dim bounds As RectangleF = Me.RectangleF
            If Not PreservePhysicsArtworkAspect OrElse bounds.IsEmpty OrElse Me.Width <= 0 OrElse Me.Height <= 0 Then Return bounds
            Dim scale As Single = Math.Min(bounds.Width / Me.Width, bounds.Height / Me.Height)
            Dim width As Single = Me.Width * scale, height As Single = Me.Height * scale
            Dim anchorX As Single = If(PivotRotation, RotationPivotX, 0.5F)
            Dim anchorY As Single = If(PivotRotation, RotationPivotY, 0.5F)
            Return New RectangleF(bounds.Left + bounds.Width * anchorX - width * anchorX,
                                  bounds.Top + bounds.Height * anchorY - height * anchorY,
                                  width, height)
        End Get
    End Property
    Public Property SnippitRotationStopBehaviour() As eSnippitRotationStopBehaviour = eSnippitRotationStopBehaviour.SpinOff
    Public Property RotationAngle() As Single = 0.0F
    Public Property RotationDirection() As eSnippitRotationDirection = eSnippitRotationDirection.Clockwise
    Public Property AutoStartRotation() As Boolean = False
    ' True only for the new single-bitmap rotation format. Older self/mech
    ' entries still use the server's generated frame cache.
    Public Property NativeRotation() As Boolean = False
    Public Property NativeRotationSteps() As Integer = 24
    Public Property NativeRotationInterval() As Integer = 50
    Public Property PivotRotation() As Boolean = False
    Public Property PivotDownAngle() As Single = 0.0F
    Public Property PivotUpAngle() As Single = -30.0F
    Public Property PivotMoveDuration() As Integer = 80
    Public Property PivotAutomaticOscillation() As Boolean = False
    Private pivotAutomaticActive As Boolean = False

    Public Sub SetPivotTriggerState(ByVal enabled As Boolean)
        If Me.Parent IsNot Nothing AndAlso Me.Parent.IsHandleCreated AndAlso Me.Parent.InvokeRequired Then
            Me.Parent.BeginInvoke(New MethodInvoker(Sub() SetPivotTriggerState(enabled)))
            Return
        End If
        If PivotAutomaticOscillation Then
            If enabled = pivotAutomaticActive Then Return
            pivotAutomaticActive = enabled
            ' Automatic pivots swing between the configured limits, but rest in
            ' the unrotated pose shown on the designer's main canvas.
            SetPivotRotationTarget(If(enabled, PivotUpAngle, 0.0F), PivotMoveDuration)
            Return
        End If
        SetPivotRotationTarget(If(enabled, PivotUpAngle, PivotDownAngle), PivotMoveDuration)
    End Sub
    Public Property RotationPivotX() As Single = 0.5F
    Public Property RotationPivotY() As Single = 0.5F
    Public Property RotationTipX() As Single = 0.9F
    Public Property RotationTipY() As Single = 0.5F
    Public Property BehindCanvas() As Boolean = False
    ' Zero preserves the legacy behavior where visibility follows the trigger state.
    Public Property FlasherPulseDuration() As Integer = 0
    Public Property MotionPathDuration() As Integer = 3000
    Public Property MotionPathLoop() As Boolean = False
    Public Property MotionPathSolenoidID() As Integer = 0
    Public Property MotionPathLampID() As Integer = 0
    Public Property MotionPathB2SID() As Integer = 0
    Public Property MotionPathStopB2SID() As Integer = 0
    Public Property MotionPathResumeB2SID() As Integer = 0
    Public Property MotionPathQueueTriggers() As Boolean = False
    Public Property MotionPathRollEnabled() As Boolean = False
    Public Property MotionPathRollAngle() As Single = 0.0F
    Public Property MotionPathSequenceGroup() As String = String.Empty
    Public Property MotionPathSequenceOrder() As Integer = 0
    Public Property MotionPathRespawnEnabled() As Boolean = False
    Public Property MotionPathRespawnPoint() As PointF = PointF.Empty
    Public Property MotionPathRespawnDuration() As Integer = 350
    Public Property MotionPathExitDuration() As Integer = 3000
    Public Property MotionPathRemoveSolenoidID() As Integer = 0
    Public Property MotionPathRemoveLampID() As Integer = 0
    Public Property MotionPathRemoveB2SID() As Integer = 0
    Public ReadOnly Property MotionPathPoints() As List(Of PointF)
        Get
            Return _motionPathPoints
        End Get
    End Property
    Public ReadOnly Property MotionPathExitPoints() As List(Of PointF)
        Get
            Return _motionPathExitPoints
        End Get
    End Property
    Public ReadOnly Property IsMotionPathActive() As Boolean
        Get
            Return motionPathTimer IsNot Nothing AndAlso (motionPathTimer.Enabled OrElse motionPathPaused)
        End Get
    End Property
    Public ReadOnly Property HasPersistentMotionPathSource() As Boolean
        Get
            Return motionPathSequenceSourceAnchor AndAlso _motionPathPoints.Count >= 2
        End Get
    End Property
    Public ReadOnly Property MotionPathSourceRectangle() As RectangleF
        Get
            If motionPathSourceRespawnActive Then Return motionPathSourceRespawnBounds
            If Not motionPathSourceBounds.IsEmpty Then Return motionPathSourceBounds
            Return Me.RectangleF
        End Get
    End Property
    Public ReadOnly Property VisualRotationAngle() As Single
        Get
            Return RotationAngle + If(MotionPathRollEnabled, MotionPathRollAngle, 0.0F)
        End Get
    End Property
    Public ReadOnly Property MotionPathSourceRollAngle() As Single
        Get
            Return If(MotionPathRollEnabled, _motionPathSourceRollAngle, 0.0F)
        End Get
    End Property
    Public ReadOnly Property MotionPathSourceImage() As Image
        Get
            Return If(_motionPathSourceImage, Me.BackgroundImage)
        End Get
    End Property
    Public ReadOnly Property IsMotionPathSourceSeparate() As Boolean
        Get
            If Not HasPersistentMotionPathSource OrElse Not motionPathSequenceSourceVisible Then Return False
            Dim source As RectangleF = MotionPathSourceRectangle
            Return Not Me.Visible OrElse
                   Math.Abs(source.Left - Me.RectangleF.Left) > 0.5F OrElse
                   Math.Abs(source.Top - Me.RectangleF.Top) > 0.5F OrElse
                   Math.Abs(source.Width - Me.RectangleF.Width) > 0.5F OrElse
                   Math.Abs(source.Height - Me.RectangleF.Height) > 0.5F
        End Get
    End Property
    Public ReadOnly Property IsMotionPathSequenceSourceVisible() As Boolean
        Get
            Return HasPersistentMotionPathSource AndAlso motionPathSequenceSourceVisible
        End Get
    End Property
    Public Event MotionPathCompleted As EventHandler
    Public Event MotionPathLaunchSegmentCompleted As EventHandler
    Public Event MotionPathSourceRespawnCompleted As EventHandler

    Private nativeRotationTimer As Timer
    ' Remember the last external trigger state. Some controller update paths can
    ' report the same energized output more than once; treating every report as
    ' a new start resets the stopwatch and makes triggered rotation crawl.
    Private nativeRotationCommandedOn As Boolean = False
    Private nativeRotationProgress As Single = 0.0F
    Private nativeRotationClock As New Stopwatch()
    Private nativeSlowDownSteps As Integer = 0
    Private nativeRunTillEnd As Boolean = False
    Private nativeRunToFirstStep As Boolean = False
    Private nativeRotationSource As Image = Nothing
    Private nativeRotationFrames As List(Of Image) = Nothing
    Private nativeFrameGeneration As Integer = 0
    Private nativeFrameIndex As Integer = -1
    Private motionPathTimer As Timer
    Private motionPathSourceRespawnTimer As Timer
    Private flasherPulseTimer As Timer
    Private flasherPulseActive As Boolean = False
    Private motionPathSourceRespawnClock As New Stopwatch()
    Private pivotRotationTimer As Timer
    Private pivotRotationClock As New Stopwatch()
    Private pivotRotationStartAngle As Single
    Private pivotRotationTargetAngle As Single
    Private pivotRotationDuration As Integer = 80
    Private motionPathClock As New Stopwatch()
    Private ReadOnly _motionPathPoints As New List(Of PointF)()
    Private ReadOnly _motionPathExitPoints As New List(Of PointF)()
    Private motionPathRuntimePoints As List(Of PointF) = Nothing
    Private motionPathRuntimeEntryPoints As List(Of PointF) = Nothing
    Private motionPathRuntimeExitPoints As List(Of PointF) = Nothing
    Private motionPathStartRectangle As RectangleF
    Private motionPathSourceBounds As RectangleF = RectangleF.Empty
    Private motionPathRuntimeRespawnPoint As PointF = PointF.Empty
    Private motionPathSourceRespawnBounds As RectangleF = RectangleF.Empty
    Private motionPathSourceRespawnStartBounds As RectangleF = RectangleF.Empty
    Private motionPathSourceRespawnEndBounds As RectangleF = RectangleF.Empty
    Private motionPathSourceRespawnActive As Boolean = False
    Private _motionPathSourceRollAngle As Single = 0.0F
    Private _motionPathSourceImage As Image = Nothing
    Private motionPathSequenceSourceAnchor As Boolean = False
    Private motionPathSequenceSourceVisible As Boolean = False
    Private motionPathLaunchSegmentReported As Boolean = True
    Private motionPathLaunchSegmentProgress As Single = 1.0F
    Private motionPathRuntimePrepared As Boolean = False
    Private motionPathPaused As Boolean = False
    Private motionPathQueuedStarts As Integer = 0
    Private motionPathActiveDuration As Integer = 3000
    Private motionPathActiveLoop As Boolean = False
    Private motionPathExternalShift As Boolean = False
    Private motionPathExternalStartVelocity As PointF = PointF.Empty
    Private motionPathExternalEndVelocity As PointF = PointF.Empty
    Private motionPathExternalLastVelocity As PointF = PointF.Empty
    Private nativePreparingWidth As Integer = 0
    Private nativePreparingHeight As Integer = 0
    Public ReadOnly Property NativeRotationFramePlayback As Boolean
        Get
            Return nativeRotationFrames IsNot Nothing AndAlso nativeRotationFrames.Count > 0
        End Get
    End Property

    Public Sub PrepareNativeRotationFrames()
        ' Automatic pivots rotate the original image continuously around a saved
        ' hinge; the discrete native frame cache would replace it with a
        ' pre-scaled frame and reset its live rotation angle.
        If Not NativeRotation OrElse PivotAutomaticOscillation OrElse
           PictureBoxType = ePictureBoxType.MechRotatingImage OrElse nativeRotationSource Is Nothing Then Return
        Dim targetWidth As Integer = Math.Max(1, CInt(Math.Round(Me.RectangleF.Width)))
        Dim targetHeight As Integer = Math.Max(1, CInt(Math.Round(Me.RectangleF.Height)))
        If targetWidth <= 1 AndAlso Me.Width > 1 Then targetWidth = Me.Width
        If targetHeight <= 1 AndAlso Me.Height > 1 Then targetHeight = Me.Height
        If nativeRotationFrames IsNot Nothing AndAlso nativeRotationFrames.Count > 0 AndAlso
           nativeRotationFrames(0).Width = targetWidth AndAlso nativeRotationFrames(0).Height = targetHeight Then Return
        If nativePreparingWidth = targetWidth AndAlso nativePreparingHeight = targetHeight Then Return

        If nativeRotationFrames IsNot Nothing Then
            _BackgroundImage = nativeRotationSource
            DisposeNativeRotationFrames(nativeRotationFrames)
            nativeRotationFrames = Nothing
            nativeFrameIndex = -1
        End If

        nativeFrameGeneration += 1
        Dim generation As Integer = nativeFrameGeneration
        ' Bound the cache by both frame count and total decoded pixels. This
        ' keeps a mistakenly selected full-screen layer from allocating an
        ' unbounded collection of large bitmaps.
        Dim requestedFrames As Integer = Math.Max(4, Math.Min(60, NativeRotationSteps))
        Dim targetPixels As Long = Math.Max(1L, CLng(targetWidth) * targetHeight)
        Dim memoryBoundFrames As Integer = CInt(Math.Max(4L, 50000000L \ targetPixels))
        Dim frameCount As Integer = Math.Min(requestedFrames, memoryBoundFrames)
        Dim direction As eSnippitRotationDirection = RotationDirection
        Dim source As Image = New Bitmap(nativeRotationSource)
        nativePreparingWidth = targetWidth
        nativePreparingHeight = targetHeight

        Task.Run(
            Function() As List(Of Image)
                Try
                    Dim prepared As New List(Of Image)(frameCount)
                    For frameIndex As Integer = 0 To frameCount - 1
                        Dim angle As Single = 360.0F * frameIndex / frameCount
                        If direction = eSnippitRotationDirection.AntiClockwise Then angle = -angle
                        prepared.Add(CreateNativeRotationFrame(source, angle, targetWidth, targetHeight))
                    Next
                    Return prepared
                Finally
                    source.Dispose()
                End Try
            End Function).ContinueWith(
                Sub(task As Task(Of List(Of Image)))
                    If task.IsCanceled Then Return
                    If task.IsFaulted Then Return
                    Dim prepared As List(Of Image) = task.Result
                    If Me.IsDisposed OrElse Me.Parent Is Nothing OrElse Not Me.Parent.IsHandleCreated Then
                        DisposeNativeRotationFrames(prepared)
                        Return
                    End If
                    Try
                        Me.Parent.BeginInvoke(New Action(Of List(Of Image), Integer)(AddressOf InstallNativeRotationFrames), prepared, generation)
                    Catch
                        DisposeNativeRotationFrames(prepared)
                    End Try
                End Sub)
    End Sub

    Private Shared Function CreateNativeRotationFrame(ByVal source As Image, ByVal angle As Single,
                                                       ByVal targetWidth As Integer, ByVal targetHeight As Integer) As Image
        ' Match the proven legacy render order. Rotate on the source's square
        ' canvas first, then apply the backglass' non-uniform display scaling.
        ' Rotating after scaling turns a circular disc into an ellipse whose
        ' long axis tumbles around the screen instead of remaining fixed.
        Dim rotatedSource As New Bitmap(source.Width, source.Height, PixelFormat.Format32bppPArgb)
        Using graphics As Graphics = Graphics.FromImage(rotatedSource)
            graphics.Clear(Color.Transparent)
            graphics.CompositingMode = CompositingMode.SourceCopy
            graphics.CompositingQuality = CompositingQuality.HighSpeed
            graphics.InterpolationMode = InterpolationMode.Bilinear
            graphics.PixelOffsetMode = PixelOffsetMode.HighSpeed
            graphics.SmoothingMode = SmoothingMode.HighSpeed
            graphics.TranslateTransform(source.Width / 2.0F, source.Height / 2.0F)
            graphics.RotateTransform(angle)
            graphics.TranslateTransform(-source.Width / 2.0F, -source.Height / 2.0F)
            graphics.DrawImageUnscaled(source, 0, 0)
        End Using

        Dim frame As New Bitmap(targetWidth, targetHeight, PixelFormat.Format32bppPArgb)
        Using graphics As Graphics = Graphics.FromImage(frame)
            graphics.Clear(Color.Transparent)
            graphics.CompositingMode = CompositingMode.SourceCopy
            graphics.CompositingQuality = CompositingQuality.HighSpeed
            graphics.InterpolationMode = InterpolationMode.Bilinear
            graphics.PixelOffsetMode = PixelOffsetMode.HighSpeed
            graphics.SmoothingMode = SmoothingMode.HighSpeed
            graphics.DrawImage(rotatedSource, New Rectangle(0, 0, targetWidth, targetHeight),
                               New Rectangle(0, 0, rotatedSource.Width, rotatedSource.Height), GraphicsUnit.Pixel)
        End Using
        rotatedSource.Dispose()
        Return frame
    End Function

    Private Sub InstallNativeRotationFrames(ByVal prepared As List(Of Image), ByVal generation As Integer)
        nativePreparingWidth = 0
        nativePreparingHeight = 0
        If Me.IsDisposed OrElse generation <> nativeFrameGeneration Then
            DisposeNativeRotationFrames(prepared)
            Return
        End If
        Dim replaced As List(Of Image) = nativeRotationFrames
        nativeRotationFrames = prepared
        nativeFrameIndex = -1
        DisposeNativeRotationFrames(replaced)
        ShowNativeRotationFrame()
    End Sub

    Private Shared Sub DisposeNativeRotationFrames(ByVal frames As List(Of Image))
        If frames Is Nothing Then Return
        For Each frame As Image In frames
            If frame IsNot Nothing Then frame.Dispose()
        Next
        frames.Clear()
    End Sub

    Private Sub ShowNativeRotationFrame()
        If nativeRotationFrames Is Nothing OrElse nativeRotationFrames.Count = 0 Then Return
        Dim normalized As Single = nativeRotationProgress Mod 360.0F
        If normalized < 0 Then normalized += 360.0F
        Dim index As Integer = Math.Min(nativeRotationFrames.Count - 1, CInt(Math.Floor(normalized * nativeRotationFrames.Count / 360.0F)))
        If index = nativeFrameIndex Then Return
        nativeFrameIndex = index
        _BackgroundImage = nativeRotationFrames(index)
        RotationAngle = 0.0F
    End Sub
    Public Sub SetNativeRotationState(ByVal enabled As Boolean)
        If Me.Parent IsNot Nothing AndAlso Me.Parent.IsHandleCreated AndAlso Me.Parent.InvokeRequired Then
            Me.Parent.BeginInvoke(New Action(Of Boolean)(AddressOf SetNativeRotationState), enabled)
            Return
        End If
        If nativeRotationCommandedOn = enabled Then Return
        nativeRotationCommandedOn = enabled
        If enabled Then
            StartNativeRotation()
        Else
            StopNativeRotation()
        End If
    End Sub

    Public Sub SetNativeMechPosition(ByVal position As Integer)
        If Not NativeRotation OrElse PictureBoxType <> ePictureBoxType.MechRotatingImage Then Return
        If Me.Parent IsNot Nothing AndAlso Me.Parent.IsHandleCreated AndAlso Me.Parent.InvokeRequired Then
            Me.Parent.BeginInvoke(New Action(Of Integer)(AddressOf SetNativeMechPosition), position)
            Return
        End If

        nativeRotationTimer.Stop()
        ' VPinMAME mech callbacks report an absolute mechanical angle. Do not
        ' reinterpret that degree value as an index into the Designer's legacy
        ' frame count (for example, Mod 16); doing so makes the artwork jump
        ' around the wheel and land on the wrong award.
        Dim normalizedPosition As Integer = position Mod 360
        If normalizedPosition < 0 Then normalizedPosition += 360
        Dim angle As Single = CSng(normalizedPosition)
        ' A mechanical position identifies a wheel segment, while the source
        ' artwork starts on the segment boundary. Move the artwork back by half
        ' one configured position so the selected segment is centered beneath
        ' the fixed pointer. For a 16-position wheel this is 11.25 degrees.
        Dim alignmentOffset As Single = 180.0F / Math.Max(2, NativeRotationSteps)
        Dim directedAngle As Single = If(RotationDirection = eSnippitRotationDirection.AntiClockwise, -angle, angle)
        RotationAngle = directedAngle - alignmentOffset
        Me.Visible = True
        InvalidateNativeRotation()
    End Sub

    Public Sub StartNativeRotation()
        If Not NativeRotation Then Return
        If Me.Parent IsNot Nothing AndAlso Me.Parent.IsHandleCreated AndAlso Me.Parent.InvokeRequired Then
            Me.Parent.BeginInvoke(New MethodInvoker(AddressOf StartNativeRotation))
            Return
        End If

        nativeRotationCommandedOn = True
        If nativeRotationTimer.Enabled Then Return

        NativeRotationSteps = Math.Max(2, Math.Min(360, NativeRotationSteps))
        NativeRotationInterval = Math.Max(10, Math.Min(500, NativeRotationInterval))
        nativeSlowDownSteps = 0
        nativeRunTillEnd = False
        nativeRunToFirstStep = False
        nativeRotationTimer.Stop()
        nativeRotationTimer.Interval = NativeRotationInterval
        nativeRotationClock.Restart()
        PrepareNativeRotationFrames()
        Me.Visible = True
        InvalidateNativeRotation()
        nativeRotationTimer.Start()
    End Sub

    Public Sub StopNativeRotation()
        If Not NativeRotation Then Return
        If Me.Parent IsNot Nothing AndAlso Me.Parent.IsHandleCreated AndAlso Me.Parent.InvokeRequired Then
            Me.Parent.BeginInvoke(New MethodInvoker(AddressOf StopNativeRotation))
            Return
        End If
        nativeRotationCommandedOn = False
        If Not nativeRotationTimer.Enabled Then Return

        Select Case SnippitRotationStopBehaviour
            Case eSnippitRotationStopBehaviour.SpinOff
                nativeSlowDownSteps = 1
            Case eSnippitRotationStopBehaviour.RunAnimationTillEnd
                nativeRunTillEnd = True
            Case eSnippitRotationStopBehaviour.RunAnimationToFirstStep
                nativeRunToFirstStep = True
            Case Else
                nativeRotationTimer.Stop()
                nativeRotationClock.Reset()
        End Select
    End Sub

    Private Sub NativeRotationTimer_Tick(ByVal sender As Object, ByVal e As EventArgs)
        Dim stepAngle As Single = 360.0F / Math.Max(4, NativeRotationSteps)
        Dim elapsedMilliseconds As Double = nativeRotationClock.Elapsed.TotalMilliseconds
        nativeRotationClock.Restart()
        Dim speedFactor As Double = 1.0
        Dim slowDownTickCount As Integer = 0
        If nativeSlowDownSteps > 0 Then
            ' Keep repainting at the configured frame interval while reducing
            ' angular velocity. Increasing the timer interval made the final
            ' part of Spin Off visibly jump between increasingly stale frames.
            slowDownTickCount = 25 + CInt(Math.Ceiling(900.0 / Math.Max(1, NativeRotationInterval)))
            speedFactor = Math.Max(0.0, 1.0 - CDbl(nativeSlowDownSteps - 1) / Math.Max(1, slowDownTickCount))
        End If
        Dim nextProgress As Single = nativeRotationProgress + CSng(stepAngle * elapsedMilliseconds / Math.Max(1, NativeRotationInterval) * speedFactor)
        Dim completedRotation As Boolean = (nextProgress >= 360.0F)
        nativeRotationProgress = nextProgress Mod 360.0F

        If NativeRotationFramePlayback Then
            ShowNativeRotationFrame()
        Else
            RotationAngle = If(RotationDirection = eSnippitRotationDirection.AntiClockwise, -nativeRotationProgress, nativeRotationProgress)
        End If
        Me.Visible = True
        InvalidateNativeRotation()

        If nativeSlowDownSteps > 0 Then
            If nativeSlowDownSteps >= slowDownTickCount Then
                nativeRotationTimer.Stop()
                nativeRotationClock.Reset()
                nativeSlowDownSteps = 0
                nativeRotationTimer.Interval = NativeRotationInterval
            Else
                nativeSlowDownSteps += 1
            End If
        ElseIf nativeRunTillEnd AndAlso completedRotation Then
            nativeRunTillEnd = False
            nativeRotationTimer.Stop()
            nativeRotationClock.Reset()
        ElseIf nativeRunToFirstStep AndAlso completedRotation Then
            nativeRunToFirstStep = False
            RotationAngle = 0.0F
            nativeRotationTimer.Stop()
            nativeRotationClock.Reset()
            InvalidateNativeRotation()
        End If
    End Sub

    Private Sub InvalidateNativeRotation()
        If Me.Parent Is Nothing Then Return
        If PivotRotation Then
            Dim pivotX As Single = Me.RectangleF.Left + Me.RectangleF.Width * RotationPivotX
            Dim pivotY As Single = Me.RectangleF.Top + Me.RectangleF.Height * RotationPivotY
            Dim leftDistance As Single = Me.RectangleF.Width * RotationPivotX
            Dim rightDistance As Single = Me.RectangleF.Width * (1.0F - RotationPivotX)
            Dim topDistance As Single = Me.RectangleF.Height * RotationPivotY
            Dim bottomDistance As Single = Me.RectangleF.Height * (1.0F - RotationPivotY)
            Dim radius As Single = CSng(Math.Sqrt(Math.Max(leftDistance, rightDistance) ^ 2 + Math.Max(topDistance, bottomDistance) ^ 2)) + 8.0F
            If PivotAutomaticOscillation AndAlso Me.Width > 0 AndAlso Me.Height > 0 Then
                ' This pivot rotates in authored coordinates before the unequal
                ' screen scales. Its repaint radius must contain that larger arc.
                Dim authoredX As Single = Me.Width * Math.Max(RotationPivotX, 1.0F - RotationPivotX)
                Dim authoredY As Single = Me.Height * Math.Max(RotationPivotY, 1.0F - RotationPivotY)
                Dim largestScale As Single = Math.Max(Me.RectangleF.Width / Me.Width,
                                                      Me.RectangleF.Height / Me.Height)
                radius = CSng(Math.Sqrt(authoredX * authoredX + authoredY * authoredY)) * largestScale + 8.0F
            End If
            Dim redrawBounds As Rectangle = Rectangle.Ceiling(New RectangleF(pivotX - radius, pivotY - radius, radius * 2.0F, radius * 2.0F))
            Me.Parent.Invalidate(redrawBounds)
            ' Pivot strokes are brief and expose transparent pixels outside the
            ' source rectangle. Finish this local composite before the next
            ' timer angle so intermediate frames cannot retain a clipped edge.
            Me.Parent.Update()
        Else
            Me.Parent.Invalidate(Rectangle.Round(Me.RectangleF))
        End If
    End Sub

    Public Sub StartMotionPath()
        StartMotionPathCore(False)
    End Sub

    Public Sub SetMotionPathSequenceSourceAnchor(ByVal enabled As Boolean)
        If motionPathSequenceSourceAnchor = enabled Then Return
        Dim oldSource As Rectangle = MotionPathPaintBounds(MotionPathSourceRectangle, MotionPathSourceRollAngle)
        motionPathSequenceSourceAnchor = enabled
        motionPathSequenceSourceVisible = enabled
        If Not enabled Then
            StopMotionPathSourceRespawn()
            motionPathSourceBounds = RectangleF.Empty
            _motionPathSourceImage = Nothing
        End If
        If Me.Parent IsNot Nothing AndAlso Not oldSource.IsEmpty Then Me.Parent.Invalidate(oldSource)
    End Sub

    Public Sub SetMotionPathSequenceSourceImage(ByVal image As Image)
        If Object.ReferenceEquals(_motionPathSourceImage, image) Then Return
        Dim dirty As Rectangle = MotionPathPaintBounds(MotionPathSourceRectangle, MotionPathSourceRollAngle)
        _motionPathSourceImage = image
        If Me.Parent IsNot Nothing AndAlso Not dirty.IsEmpty Then
            dirty.Inflate(2, 2)
            Me.Parent.Invalidate(dirty)
        End If
    End Sub

    Public Sub SetMotionPathSequenceSourceVisible(ByVal visible As Boolean)
        If Not motionPathSequenceSourceAnchor OrElse motionPathSequenceSourceVisible = visible Then Return
        Dim oldSource As Rectangle = MotionPathPaintBounds(MotionPathSourceRectangle, MotionPathSourceRollAngle)
        If Not visible Then StopMotionPathSourceRespawn()
        motionPathSequenceSourceVisible = visible
        If Me.Parent IsNot Nothing Then
            Dim dirty As Rectangle = Rectangle.Union(oldSource, MotionPathPaintBounds(MotionPathSourceRectangle, MotionPathSourceRollAngle))
            dirty.Inflate(2, 2)
            Me.Parent.Invalidate(dirty)
        End If
    End Sub

    Public Function StartMotionPathSequenceSourceRespawn() As Boolean
        If Not motionPathSequenceSourceAnchor OrElse Not MotionPathRespawnEnabled OrElse
           Not motionPathRuntimePrepared OrElse motionPathSourceBounds.IsEmpty OrElse Me.Parent Is Nothing Then Return False

        Dim width As Single = motionPathSourceBounds.Width
        Dim height As Single = motionPathSourceBounds.Height
        Dim startBounds As New RectangleF(motionPathRuntimeRespawnPoint.X - width / 2.0F,
                                          motionPathRuntimeRespawnPoint.Y - height / 2.0F,
                                          width, height)
        If Math.Abs(startBounds.Left - motionPathSourceBounds.Left) < 0.5F AndAlso
           Math.Abs(startBounds.Top - motionPathSourceBounds.Top) < 0.5F Then Return False

        StopMotionPathSourceRespawn()
        motionPathSourceRespawnStartBounds = startBounds
        motionPathSourceRespawnEndBounds = motionPathSourceBounds
        motionPathSourceRespawnBounds = startBounds
        motionPathSourceRespawnActive = True
        motionPathSequenceSourceVisible = True
        Dim dirty As Rectangle = Rectangle.Union(MotionPathPaintBounds(startBounds, MotionPathSourceRollAngle),
                                                 MotionPathPaintBounds(motionPathSourceBounds, MotionPathSourceRollAngle))
        dirty.Inflate(2, 2)
        Me.Parent.Invalidate(dirty)
        motionPathSourceRespawnClock.Restart()
        motionPathSourceRespawnTimer.Start()
        Return True
    End Function

    Private Sub StopMotionPathSourceRespawn()
        If motionPathSourceRespawnTimer IsNot Nothing Then motionPathSourceRespawnTimer.Stop()
        motionPathSourceRespawnClock.Reset()
        motionPathSourceRespawnActive = False
        motionPathSourceRespawnBounds = RectangleF.Empty
    End Sub

    Private Sub MotionPathSourceRespawnTimer_Tick(ByVal sender As Object, ByVal e As EventArgs)
        If Not motionPathSourceRespawnActive OrElse Me.Parent Is Nothing Then
            StopMotionPathSourceRespawn()
            Return
        End If
        Dim duration As Integer = Math.Max(50, Math.Min(5000, MotionPathRespawnDuration))
        Dim progress As Single = CSng(Math.Min(1.0, motionPathSourceRespawnClock.Elapsed.TotalMilliseconds / duration))
        Dim eased As Single = 1.0F - CSng(Math.Pow(1.0F - progress, 3.0F))
        Dim oldVisualBounds As Rectangle = MotionPathPaintBounds(motionPathSourceRespawnBounds, _motionPathSourceRollAngle)
        Dim nextBounds As New RectangleF(
            motionPathSourceRespawnStartBounds.Left + (motionPathSourceRespawnEndBounds.Left - motionPathSourceRespawnStartBounds.Left) * eased,
            motionPathSourceRespawnStartBounds.Top + (motionPathSourceRespawnEndBounds.Top - motionPathSourceRespawnStartBounds.Top) * eased,
            motionPathSourceRespawnEndBounds.Width,
            motionPathSourceRespawnEndBounds.Height)
        AdvanceMotionPathRoll(RectangleCenter(motionPathSourceRespawnBounds), RectangleCenter(nextBounds),
                              motionPathSourceRespawnEndBounds.Size, _motionPathSourceRollAngle)
        motionPathSourceRespawnBounds = nextBounds
        Dim dirty As Rectangle = Rectangle.Union(oldVisualBounds, MotionPathPaintBounds(motionPathSourceRespawnBounds, _motionPathSourceRollAngle))
        dirty.Inflate(2, 2)
        Me.Parent.Invalidate(dirty)
        If progress >= 1.0F Then
            motionPathSourceRespawnBounds = motionPathSourceRespawnEndBounds
            motionPathSourceRespawnTimer.Stop()
            motionPathSourceRespawnClock.Reset()
            motionPathSourceRespawnActive = False
            RaiseEvent MotionPathSourceRespawnCompleted(Me, EventArgs.Empty)
        End If
    End Sub

    Public Sub SetPivotRotationTarget(ByVal targetAngle As Single, ByVal duration As Integer)
        If Me.Parent IsNot Nothing AndAlso Me.Parent.IsHandleCreated AndAlso Me.Parent.InvokeRequired Then
            Me.Parent.BeginInvoke(New MethodInvoker(Sub() SetPivotRotationTarget(targetAngle, duration)))
            Return
        End If
        pivotRotationTimer.Stop()
        pivotRotationClock.Reset()
        pivotRotationStartAngle = RotationAngle
        pivotRotationTargetAngle = targetAngle
        pivotRotationDuration = Math.Max(10, Math.Min(If(PivotAutomaticOscillation, 5000, 1000), duration))
        PivotRotation = True
        NativeRotation = True
        Me.Visible = True
        pivotRotationClock.Start()
        pivotRotationTimer.Start()
        InvalidateNativeRotation()
    End Sub

    Private Sub PivotRotationTimer_Tick(ByVal sender As Object, ByVal e As EventArgs)
        Dim progress As Single = CSng(Math.Min(1.0, pivotRotationClock.Elapsed.TotalMilliseconds / pivotRotationDuration))
        Dim eased As Single = progress * progress * (3.0F - 2.0F * progress)
        RotationAngle = pivotRotationStartAngle + (pivotRotationTargetAngle - pivotRotationStartAngle) * eased
        InvalidateNativeRotation()
        If progress >= 1.0F Then
            pivotRotationTimer.Stop()
            pivotRotationClock.Reset()
            RotationAngle = pivotRotationTargetAngle
            If PivotAutomaticOscillation AndAlso pivotAutomaticActive Then
                SetPivotRotationTarget(If(Math.Abs(pivotRotationTargetAngle - PivotUpAngle) < 0.01F,
                                          PivotDownAngle, PivotUpAngle), PivotMoveDuration)
            End If
        End If
    End Sub

    Public Sub StartMotionPathExit()
        StartMotionPathCore(True)
    End Sub

    Public ReadOnly Property MotionPathCenter() As PointF
        Get
            Return New PointF(Me.RectangleF.Left + Me.RectangleF.Width / 2.0F,
                              Me.RectangleF.Top + Me.RectangleF.Height / 2.0F)
        End Get
    End Property

    Public Sub SetMotionPathPosition(ByVal center As PointF, Optional ByVal advanceRoll As Boolean = False)
        If Me.Parent IsNot Nothing AndAlso Me.Parent.IsHandleCreated AndAlso Me.Parent.InvokeRequired Then
            Me.Parent.BeginInvoke(New MethodInvoker(Sub() SetMotionPathPosition(center, advanceRoll)))
            Return
        End If
        ' Slot normalization is authoritative. A survivor shift and the exit
        ' path can finish on adjacent timer ticks; cancel the survivor timer so
        ' it cannot move the normalized object back onto an occupied slot.
        motionPathTimer.Stop()
        motionPathClock.Reset()
        motionPathPaused = False
        motionPathActiveLoop = False
        motionPathLaunchSegmentReported = True
        motionPathStartRectangle = Me.RectangleF
        SetMotionPathCenter(center, advanceRoll)
    End Sub

    Public Sub StartMotionPathShift(ByVal target As PointF, ByVal duration As Integer)
        If Me.Parent Is Nothing Then Return
        If Me.Parent.IsHandleCreated AndAlso Me.Parent.InvokeRequired Then
            Me.Parent.BeginInvoke(New MethodInvoker(Sub() StartMotionPathShift(target, duration)))
            Return
        End If
        motionPathTimer.Stop()
        motionPathClock.Reset()
        motionPathPaused = False
        motionPathExternalShift = False
        motionPathLaunchSegmentReported = True
        motionPathStartRectangle = Me.RectangleF
        motionPathRuntimePoints = New List(Of PointF) From {MotionPathCenter, target}
        motionPathActiveDuration = Math.Max(250, Math.Min(30000, duration))
        motionPathActiveLoop = False
        Me.Visible = True
        motionPathClock.Start()
        motionPathTimer.Start()
    End Sub

    Public Sub StartExternalGridShift(ByVal target As PointF, ByVal duration As Integer, Optional ByVal lookAhead As Nullable(Of PointF) = Nothing)
        If Me.Parent Is Nothing Then Return
        If Me.Parent.IsHandleCreated AndAlso Me.Parent.InvokeRequired Then
            Me.Parent.BeginInvoke(New MethodInvoker(Sub() StartExternalGridShift(target, duration, lookAhead)))
            Return
        End If
        Dim startCenter As PointF = MotionPathCenter
        Dim startVelocity As PointF = motionPathExternalLastVelocity
        If motionPathExternalShift AndAlso motionPathTimer.Enabled AndAlso motionPathRuntimePoints IsNot Nothing AndAlso motionPathRuntimePoints.Count >= 2 Then
            Dim oldProgress As Single = CSng(Math.Min(1.0, motionPathClock.Elapsed.TotalMilliseconds / Math.Max(1, motionPathActiveDuration)))
            startCenter = ExternalGridPoint(oldProgress)
            startVelocity = ExternalGridVelocity(oldProgress)
            SetMotionPathCenter(startCenter, True)
        End If
        motionPathTimer.Stop()
        motionPathClock.Reset()
        motionPathPaused = False
        motionPathLaunchSegmentReported = True
        motionPathStartRectangle = Me.RectangleF
        motionPathRuntimePoints = New List(Of PointF) From {startCenter, target}
        motionPathActiveDuration = Math.Max(10, Math.Min(1000, duration))
        motionPathActiveLoop = False
        motionPathExternalShift = True
        motionPathExternalStartVelocity = LimitExternalGridVelocity(startVelocity, startCenter, target, motionPathActiveDuration)
        Dim nextPoint As PointF = If(lookAhead.HasValue, lookAhead.Value, target)
        motionPathExternalEndVelocity = New PointF((nextPoint.X - target.X) / motionPathActiveDuration,
                                                    (nextPoint.Y - target.Y) / motionPathActiveDuration)
        motionPathExternalLastVelocity = motionPathExternalStartVelocity
        Me.Visible = True
        motionPathClock.Start()
        motionPathTimer.Start()
    End Sub

    Private Sub StartMotionPathCore(ByVal useExitPath As Boolean)
        Dim sourcePoints As List(Of PointF) = If(useExitPath, _motionPathExitPoints, _motionPathPoints)
        If sourcePoints.Count < 2 OrElse Me.Parent Is Nothing Then Return
        If Me.Parent.IsHandleCreated AndAlso Me.Parent.InvokeRequired Then
            If useExitPath Then
                Me.Parent.BeginInvoke(New MethodInvoker(AddressOf StartMotionPathExit))
            Else
                Me.Parent.BeginInvoke(New MethodInvoker(AddressOf StartMotionPath))
            End If
            Return
        End If

        If Not useExitPath AndAlso MotionPathQueueTriggers AndAlso (motionPathTimer.Enabled OrElse motionPathPaused) Then
            motionPathQueuedStarts = Math.Min(32, motionPathQueuedStarts + 1)
            Return
        End If

        motionPathTimer.Stop()
        motionPathClock.Reset()
        motionPathPaused = False
        motionPathExternalShift = False
        motionPathActiveDuration = Math.Max(250, Math.Min(30000, If(useExitPath, MotionPathExitDuration, MotionPathDuration)))
        motionPathActiveLoop = Not useExitPath AndAlso MotionPathLoop
        If Not motionPathRuntimePrepared Then
            motionPathStartRectangle = Me.RectangleF
            If motionPathSequenceSourceAnchor AndAlso motionPathSourceBounds.IsEmpty Then
                ' Sequence member 1 is also the authored source artwork. Preserve
                ' its final screen-scaled start rectangle before that control moves.
                motionPathSourceBounds = motionPathStartRectangle
            End If
            Dim originalWidth As Single = Math.Max(1.0F, Me.Width)
            Dim originalHeight As Single = Math.Max(1.0F, Me.Height)
            Dim scaleX As Single = motionPathStartRectangle.Width / originalWidth
            Dim scaleY As Single = motionPathStartRectangle.Height / originalHeight
            Dim originalCenter As New PointF(Me.Left + originalWidth / 2.0F, Me.Top + originalHeight / 2.0F)
            Dim runtimeCenter As New PointF(motionPathStartRectangle.Left + motionPathStartRectangle.Width / 2.0F,
                                            motionPathStartRectangle.Top + motionPathStartRectangle.Height / 2.0F)
            motionPathRuntimeEntryPoints = New List(Of PointF)(_motionPathPoints.Count)
            For Each point As PointF In _motionPathPoints
                motionPathRuntimeEntryPoints.Add(New PointF(runtimeCenter.X + (point.X - originalCenter.X) * scaleX,
                                                              runtimeCenter.Y + (point.Y - originalCenter.Y) * scaleY))
            Next
            motionPathRuntimeExitPoints = New List(Of PointF)(_motionPathExitPoints.Count)
            For Each point As PointF In _motionPathExitPoints
                motionPathRuntimeExitPoints.Add(New PointF(runtimeCenter.X + (point.X - originalCenter.X) * scaleX,
                                                             runtimeCenter.Y + (point.Y - originalCenter.Y) * scaleY))
            Next
            motionPathRuntimeRespawnPoint = New PointF(runtimeCenter.X + (MotionPathRespawnPoint.X - originalCenter.X) * scaleX,
                                                       runtimeCenter.Y + (MotionPathRespawnPoint.Y - originalCenter.Y) * scaleY)
            motionPathRuntimePrepared = True
        End If
        motionPathRuntimePoints = If(useExitPath, motionPathRuntimeExitPoints, motionPathRuntimeEntryPoints)
        motionPathLaunchSegmentReported = useExitPath
        motionPathLaunchSegmentProgress = If(useExitPath, 1.0F, FirstMotionPathSegmentCompletion(motionPathRuntimePoints))
        SetMotionPathCenter(motionPathRuntimePoints(0))
        Me.Visible = True
        motionPathClock.Start()
        motionPathTimer.Start()
    End Sub

    Public Sub StopMotionPath()
        If Me.Parent IsNot Nothing AndAlso Me.Parent.IsHandleCreated AndAlso Me.Parent.InvokeRequired Then
            Me.Parent.BeginInvoke(New MethodInvoker(AddressOf StopMotionPath))
            Return
        End If
        If motionPathTimer.Enabled Then
            motionPathTimer.Stop()
            motionPathClock.Stop()
            motionPathPaused = True
        End If
    End Sub

    Public Sub ResumeMotionPath()
        If Me.Parent IsNot Nothing AndAlso Me.Parent.IsHandleCreated AndAlso Me.Parent.InvokeRequired Then
            Me.Parent.BeginInvoke(New MethodInvoker(AddressOf ResumeMotionPath))
            Return
        End If
        If Not motionPathPaused OrElse motionPathRuntimePoints Is Nothing OrElse motionPathRuntimePoints.Count < 2 Then Return
        motionPathPaused = False
        motionPathClock.Start()
        motionPathTimer.Start()
    End Sub

    Private Sub MotionPathTimer_Tick(ByVal sender As Object, ByVal e As EventArgs)
        If motionPathRuntimePoints Is Nothing OrElse motionPathRuntimePoints.Count < 2 Then
            motionPathTimer.Stop()
            motionPathClock.Reset()
            Return
        End If
        Dim progress As Single = CSng(Math.Min(1.0, motionPathClock.Elapsed.TotalMilliseconds / motionPathActiveDuration))
        If motionPathExternalShift Then
            SetMotionPathCenter(ExternalGridPoint(progress), True)
            motionPathExternalLastVelocity = ExternalGridVelocity(progress)
        Else
            SetMotionPathCenter(PointAlongMotionPath(progress), True)
            If Not motionPathLaunchSegmentReported AndAlso progress >= motionPathLaunchSegmentProgress Then
                motionPathLaunchSegmentReported = True
                RaiseEvent MotionPathLaunchSegmentCompleted(Me, EventArgs.Empty)
            End If
        End If
        If progress >= 1.0F Then
            If motionPathActiveLoop Then
                motionPathClock.Restart()
            Else
                motionPathTimer.Stop()
                motionPathClock.Reset()
                motionPathPaused = False
                RaiseEvent MotionPathCompleted(Me, EventArgs.Empty)
                If MotionPathQueueTriggers AndAlso motionPathQueuedStarts > 0 Then
                    motionPathQueuedStarts -= 1
                    StartMotionPath()
                End If
            End If
        End If
    End Sub

    Private Function ExternalGridPoint(ByVal progress As Single) As PointF
        Dim p0 As PointF = motionPathRuntimePoints(0)
        Dim p1 As PointF = motionPathRuntimePoints(1)
        Dim t As Single = Math.Max(0.0F, Math.Min(1.0F, progress))
        Dim t2 As Single = t * t
        Dim t3 As Single = t2 * t
        Dim duration As Single = Math.Max(1, motionPathActiveDuration)
        Dim h00 As Single = 2.0F * t3 - 3.0F * t2 + 1.0F
        Dim h10 As Single = t3 - 2.0F * t2 + t
        Dim h01 As Single = -2.0F * t3 + 3.0F * t2
        Dim h11 As Single = t3 - t2
        Return New PointF(h00 * p0.X + h10 * duration * motionPathExternalStartVelocity.X + h01 * p1.X + h11 * duration * motionPathExternalEndVelocity.X,
                          h00 * p0.Y + h10 * duration * motionPathExternalStartVelocity.Y + h01 * p1.Y + h11 * duration * motionPathExternalEndVelocity.Y)
    End Function

    Private Function ExternalGridVelocity(ByVal progress As Single) As PointF
        Dim p0 As PointF = motionPathRuntimePoints(0)
        Dim p1 As PointF = motionPathRuntimePoints(1)
        Dim t As Single = Math.Max(0.0F, Math.Min(1.0F, progress))
        Dim t2 As Single = t * t
        Dim duration As Single = Math.Max(1, motionPathActiveDuration)
        Dim dh00 As Single = 6.0F * t2 - 6.0F * t
        Dim dh10 As Single = 3.0F * t2 - 4.0F * t + 1.0F
        Dim dh01 As Single = -6.0F * t2 + 6.0F * t
        Dim dh11 As Single = 3.0F * t2 - 2.0F * t
        Return New PointF((dh00 * p0.X + dh10 * duration * motionPathExternalStartVelocity.X + dh01 * p1.X + dh11 * duration * motionPathExternalEndVelocity.X) / duration,
                          (dh00 * p0.Y + dh10 * duration * motionPathExternalStartVelocity.Y + dh01 * p1.Y + dh11 * duration * motionPathExternalEndVelocity.Y) / duration)
    End Function

    Private Function LimitExternalGridVelocity(ByVal velocity As PointF, ByVal fromPoint As PointF, ByVal toPoint As PointF, ByVal duration As Integer) As PointF
        Dim dx As Double = toPoint.X - fromPoint.X
        Dim dy As Double = toPoint.Y - fromPoint.Y
        Dim distance As Double = Math.Sqrt(dx * dx + dy * dy)
        Dim speed As Double = Math.Sqrt(velocity.X * velocity.X + velocity.Y * velocity.Y)
        Dim maximum As Double = Math.Max(0.01, distance * 2.0 / Math.Max(1, duration))
        If speed <= maximum OrElse speed <= 0 Then Return velocity
        Dim scale As Single = CSng(maximum / speed)
        Return New PointF(velocity.X * scale, velocity.Y * scale)
    End Function

    Private Function PointAlongMotionPath(ByVal progress As Single) As PointF
        Dim lengths As New List(Of Double)(motionPathRuntimePoints.Count - 1)
        Dim total As Double = 0
        Dim segmentCount As Integer = motionPathRuntimePoints.Count - 1 + If(motionPathActiveLoop, 1, 0)
        For index As Integer = 0 To segmentCount - 1
            Dim fromPoint As PointF = motionPathRuntimePoints(index Mod motionPathRuntimePoints.Count)
            Dim toPoint As PointF = motionPathRuntimePoints((index + 1) Mod motionPathRuntimePoints.Count)
            Dim dx As Double = toPoint.X - fromPoint.X
            Dim dy As Double = toPoint.Y - fromPoint.Y
            Dim length As Double = Math.Sqrt(dx * dx + dy * dy)
            lengths.Add(length)
            total += length
        Next
        If total <= 0 Then Return motionPathRuntimePoints(0)
        Dim target As Double = Math.Max(0.0, Math.Min(1.0, progress)) * total
        For index As Integer = 0 To lengths.Count - 1
            If target <= lengths(index) OrElse index = lengths.Count - 1 Then
                Dim part As Single = If(lengths(index) <= 0, 0.0F, CSng(target / lengths(index)))
                Dim fromPoint As PointF = motionPathRuntimePoints(index Mod motionPathRuntimePoints.Count)
                Dim toPoint As PointF = motionPathRuntimePoints((index + 1) Mod motionPathRuntimePoints.Count)
                Return New PointF(fromPoint.X + (toPoint.X - fromPoint.X) * part,
                                  fromPoint.Y + (toPoint.Y - fromPoint.Y) * part)
            End If
            target -= lengths(index)
        Next
        Return motionPathRuntimePoints(motionPathRuntimePoints.Count - 1)
    End Function

    Private Function FirstMotionPathSegmentCompletion(ByVal points As List(Of PointF)) As Single
        If points Is Nothing OrElse points.Count < 2 Then Return 1.0F
        Dim total As Double = 0.0
        Dim firstDistinctEnd As Double = -1.0
        For index As Integer = 0 To points.Count - 2
            Dim dx As Double = points(index + 1).X - points(index).X
            Dim dy As Double = points(index + 1).Y - points(index).Y
            Dim length As Double = Math.Sqrt(dx * dx + dy * dy)
            total += length
            If firstDistinctEnd < 0.0 AndAlso length > 0.001 Then firstDistinctEnd = total
        Next
        If total <= 0.001 OrElse firstDistinctEnd < 0.0 Then Return 1.0F
        Return CSng(Math.Max(0.0, Math.Min(1.0, firstDistinctEnd / total)))
    End Function

    Private Sub SetMotionPathCenter(ByVal center As PointF, Optional ByVal advanceRoll As Boolean = False)
        Dim previousCenter As PointF = MotionPathCenter
        Dim oldBounds As Rectangle = MotionPathPaintBounds(Me.RectangleF, VisualRotationAngle)
        If advanceRoll Then AdvanceMotionPathRoll(previousCenter, center, Me.RectangleF.Size, MotionPathRollAngle)
        Me.RectangleF = New RectangleF(center.X - motionPathStartRectangle.Width / 2.0F,
                                       center.Y - motionPathStartRectangle.Height / 2.0F,
                                       motionPathStartRectangle.Width,
                                       motionPathStartRectangle.Height)
        If Me.Parent IsNot Nothing Then
            Dim dirty As Rectangle = Rectangle.Union(oldBounds, MotionPathPaintBounds(Me.RectangleF, VisualRotationAngle))
            dirty.Inflate(2, 2)
            Me.Parent.Invalidate(dirty)
        End If
    End Sub

    Private Sub AdvanceMotionPathRoll(ByVal previousCenter As PointF, ByVal currentCenter As PointF,
                                      ByVal visualSize As SizeF, ByRef angle As Single)
        If Not MotionPathRollEnabled Then Return
        Dim dx As Single = currentCenter.X - previousCenter.X
        Dim dy As Single = currentCenter.Y - previousCenter.Y
        Dim distance As Double = Math.Sqrt(dx * dx + dy * dy)
        If distance <= 0.0001R Then Return
        Dim direction As Single = If(Math.Abs(dx) >= Math.Abs(dy), Math.Sign(dx), Math.Sign(dy))
        If direction = 0.0F Then Return
        Dim radius As Single = Math.Max(1.0F, Math.Min(visualSize.Width, visualSize.Height) / 2.0F)
        angle = CSng((angle + direction * distance / radius * 180.0R / Math.PI) Mod 360.0R)
    End Sub

    Private Shared Function RectangleCenter(ByVal bounds As RectangleF) As PointF
        Return New PointF(bounds.Left + bounds.Width / 2.0F, bounds.Top + bounds.Height / 2.0F)
    End Function

    Private Shared Function RotationPaintBounds(ByVal bounds As RectangleF, ByVal angle As Single) As Rectangle
        If bounds.IsEmpty OrElse Math.Abs(angle) < 0.001F Then Return Rectangle.Round(bounds)
        Dim radians As Double = angle * Math.PI / 180.0R
        Dim width As Single = CSng(Math.Abs(bounds.Width * Math.Cos(radians)) + Math.Abs(bounds.Height * Math.Sin(radians)))
        Dim height As Single = CSng(Math.Abs(bounds.Width * Math.Sin(radians)) + Math.Abs(bounds.Height * Math.Cos(radians)))
        Dim center As PointF = RectangleCenter(bounds)
        Return Rectangle.Ceiling(New RectangleF(center.X - width / 2.0F, center.Y - height / 2.0F, width, height))
    End Function

    Private Function MotionPathPaintBounds(ByVal bounds As RectangleF, ByVal angle As Single) As Rectangle
        ' Motion-path rolling uses a shape-preserving transform: the ball's
        ' authored silhouette never rotates. Reusing ordinary rotated-rectangle
        ' bounds here can under-invalidate the fixed width near 90 degrees.
        If MotionPathRollEnabled AndAlso Not NativeRotation Then
            Dim radians As Double = angle * Math.PI / 180.0R
            Dim factor As Single = CSng(Math.Abs(Math.Cos(radians)) + Math.Abs(Math.Sin(radians)))
            Dim center As PointF = RectangleCenter(bounds)
            Dim transformed As Rectangle = Rectangle.Ceiling(New RectangleF(
                center.X - bounds.Width * factor / 2.0F,
                center.Y - bounds.Height * factor / 2.0F,
                bounds.Width * factor,
                bounds.Height * factor))
            Dim rollingBounds As Rectangle = Rectangle.Union(Rectangle.Ceiling(bounds), transformed)
            rollingBounds.Inflate(4, 4)
            Return rollingBounds
        End If
        Return RotationPaintBounds(bounds, angle)
    End Function

    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        If disposing AndAlso flasherPulseTimer IsNot Nothing Then
            flasherPulseTimer.Stop()
            RemoveHandler flasherPulseTimer.Tick, AddressOf FlasherPulseTimer_Tick
            flasherPulseTimer.Dispose()
            flasherPulseTimer = Nothing
        End If
        If disposing AndAlso pivotRotationTimer IsNot Nothing Then
            pivotRotationTimer.Stop()
            RemoveHandler pivotRotationTimer.Tick, AddressOf PivotRotationTimer_Tick
            pivotRotationTimer.Dispose()
            pivotRotationTimer = Nothing
        End If
        If disposing AndAlso motionPathTimer IsNot Nothing Then
            motionPathTimer.Stop()
            motionPathClock.Reset()
            RemoveHandler motionPathTimer.Tick, AddressOf MotionPathTimer_Tick
            motionPathTimer.Dispose()
            motionPathTimer = Nothing
        End If
        If disposing AndAlso motionPathSourceRespawnTimer IsNot Nothing Then
            motionPathSourceRespawnTimer.Stop()
            motionPathSourceRespawnClock.Reset()
            RemoveHandler motionPathSourceRespawnTimer.Tick, AddressOf MotionPathSourceRespawnTimer_Tick
            motionPathSourceRespawnTimer.Dispose()
            motionPathSourceRespawnTimer = Nothing
        End If
        If disposing AndAlso nativeRotationTimer IsNot Nothing Then
            nativeRotationTimer.Stop()
            nativeRotationClock.Reset()
            nativeFrameGeneration += 1
            DisposeNativeRotationFrames(nativeRotationFrames)
            nativeRotationFrames = Nothing
            If nativeRotationSource IsNot Nothing Then nativeRotationSource.Dispose()
            nativeRotationSource = Nothing
            RemoveHandler nativeRotationTimer.Tick, AddressOf NativeRotationTimer_Tick
            nativeRotationTimer.Dispose()
            nativeRotationTimer = Nothing
        End If
        MyBase.Dispose(disposing)
    End Sub

    Private _Visible As Boolean
    Public Shadows Property Visible(Optional ByVal _SetThruAnimation As Boolean = False) As Boolean
        Get
            Return _Visible
        End Get
        Set(ByVal value As Boolean)
            If FlasherPulseDuration > 0 Then
                If value Then
                    SetVisibleState(True, _SetThruAnimation)
                    flasherPulseActive = True
                    RestartFlasherPulseTimer()
                    Return
                ElseIf flasherPulseActive Then
                    ' A momentary ROM/B2S pulse commonly returns OFF immediately.
                    ' Keep the artwork visible for the authored pulse duration.
                    Return
                End If
            End If
            SetVisibleState(value, _SetThruAnimation)
        End Set
    End Property

    Private Sub SetVisibleState(ByVal value As Boolean, ByVal setThruAnimationValue As Boolean)
        If _Visible = value Then Return
        SetThruAnimation = setThruAnimationValue
        _Visible = value
        If Me.Parent IsNot Nothing Then
            Dim dirty As Rectangle = Rectangle.Round(Me.RectangleF)
            If HasPersistentMotionPathSource Then dirty = Rectangle.Union(dirty, Rectangle.Round(MotionPathSourceRectangle))
            Me.Parent.Invalidate(dirty)
        End If
    End Sub

    Private Sub RestartFlasherPulseTimer()
        If flasherPulseTimer Is Nothing OrElse Me.IsDisposed Then Return
        If Me.IsHandleCreated AndAlso Me.InvokeRequired Then
            Try
                Me.BeginInvoke(New MethodInvoker(AddressOf RestartFlasherPulseTimer))
            Catch ex As InvalidOperationException
                flasherPulseActive = False
            End Try
            Return
        End If

        flasherPulseTimer.Stop()
        flasherPulseTimer.Interval = Math.Max(50, Math.Min(5000, FlasherPulseDuration))
        flasherPulseTimer.Start()
    End Sub

    Private Sub FlasherPulseTimer_Tick(ByVal sender As Object, ByVal e As EventArgs)
        If flasherPulseTimer IsNot Nothing Then flasherPulseTimer.Stop()
        flasherPulseActive = False
        SetVisibleState(False, False)
    End Sub
    Public Property SetThruAnimation() As Boolean = False

    Private _BackgroundImage As Image
    Public Shadows Property BackgroundImage() As Image
        Get
            Return _BackgroundImage
        End Get
        Set(ByVal value As Image)
            If _BackgroundImage Is Nothing OrElse Not _BackgroundImage.Equals(value) Then
                _BackgroundImage = value
                If Not Object.ReferenceEquals(value, nativeRotationSource) Then
                    nativeFrameGeneration += 1
                    DisposeNativeRotationFrames(nativeRotationFrames)
                    nativeRotationFrames = Nothing
                    nativeFrameIndex = -1
                    If nativeRotationSource IsNot Nothing Then nativeRotationSource.Dispose()
                    nativeRotationSource = If(value Is Nothing, Nothing, New Bitmap(value))
                End If
                ' do a invalidate at the parent form
                If Me.Parent IsNot Nothing Then
                    Dim dirty As Rectangle = Rectangle.Round(Me.RectangleF)
                    If HasPersistentMotionPathSource Then dirty = Rectangle.Union(dirty, Rectangle.Round(MotionPathSourceRectangle))
                    Me.Parent.Invalidate(dirty)
                End If
            End If
        End Set
    End Property

    Private _OffImage As Image
    Public Property OffImage() As Image
        Get
            Return _OffImage
        End Get
        Set(ByVal value As Image)
            _OffImage = value
        End Set
    End Property

End Class
