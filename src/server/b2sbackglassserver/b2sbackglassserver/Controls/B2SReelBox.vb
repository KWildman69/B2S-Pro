Imports System
Imports System.Windows.Forms
Imports System.Drawing

Public Class B2SReelBox

    Inherits B2SBaseBox

    Public Property UnifiedLayerZOrder As Integer = 0
    Public Property UnifiedLayerOverlay As Action(Of Control, PaintEventArgs) = Nothing

    Public KeepDigitAspect As Boolean = False
    Public ReelDigitWidth As Integer = 0
    Public ReelDesignSize As Size = Size.Empty

    ' B2S Pro: this digit's slice of a rotated/perspective score (Nothing = straight on).
    Private scoreSlice As ScoreTransformState = Nothing
    ' The rectangle the reel content is drawn into: the client area normally, or
    ' the off-screen content bitmap for a transformed reel.
    Private paintTarget As Rectangle = Rectangle.Empty

    Public Enum eScoreType
        NotUsed = 0
        Scores = 1
        Credits = 2
    End Enum

    Public Class ReelRollOverEventArgs
        Inherits EventArgs

        Public Digit As Integer = 0

        Public Sub New(ByVal _digit As Integer)
            Digit = _digit
        End Sub
    End Class
    Public Event ReelRollOver(ByVal sender As Object, ByVal e As ReelRollOverEventArgs)

    Private timer As Timer = Nothing
    Private cTimerInterval As Integer = 101

    Private isLED As Boolean = False

    Private length As Integer = 1
    Private initValue As String = "0"
    Private reelindex As String = String.Empty

    Private intermediates As Integer = -1
    Private intermediates2go As Integer = 0
    Private ReadOnly reel3DImageCache As New Generic.Dictionary(Of String, Bitmap)()
    Private reel3DOverlayCache As Bitmap = Nothing
    Private reel3DOverlayCacheKey As String = String.Empty

    Protected Overrides Sub Dispose(disposing As Boolean)
        MyBase.Dispose(disposing)
        On Error Resume Next
        If disposing Then
            If timer IsNot Nothing Then
                timer.Stop()
                RemoveHandler timer.Tick, AddressOf ReelAnimationTimer_Tick
                timer.Dispose()
            End If
            timer = Nothing
            ClearReel3DCache()
        End If
    End Sub

    Protected Overrides Sub OnPaint(ByVal e As System.Windows.Forms.PaintEventArgs)

        If scoreSlice IsNot Nothing Then
            Dim quad As PointF() = scoreSlice.Scaled(ClientSize)
            Dim contentSize As Size = scoreSlice.ContentSize(ClientSize)
            Dim contentWidth As Integer = contentSize.Width
            Dim contentHeight As Integer = contentSize.Height
            Using content As New Bitmap(contentWidth, contentHeight, Imaging.PixelFormat.Format32bppPArgb)
                Using contentGraphics As Graphics = Graphics.FromImage(content)
                    contentGraphics.Clear(Color.Transparent)
                    paintTarget = New Rectangle(0, 0, contentWidth, contentHeight)
                    PaintReelContent(contentGraphics)
                End Using
                ScoreTransform.DrawWarped(e.Graphics, content, quad)
            End Using
        Else
            paintTarget = Me.ClientRectangle
            PaintReelContent(e.Graphics)
        End If

        If UnifiedLayerOverlay IsNot Nothing Then UnifiedLayerOverlay.Invoke(Me, e)

    End Sub

    Private Sub PaintReelContent(ByVal graphics As Graphics)

        ' Realistic style: the drum turns smoothly, so the shown number is
        ' drawn with its roll instead of intermediate images.
        If smoothRolling AndAlso Not String.IsNullOrEmpty(reelindex) Then
            Dim rollingName As String = _ReelType & "_" & reelindex
            Dim rollingIlluminatedName As String = rollingName & If(SetID > 0, "_" & SetID.ToString(), "")
            DrawAvailableReelImage(graphics, B2SData.ReelImages, B2SData.ReelIlluImages, rollingName, rollingIlluminatedName)
            Return
        End If

        If Not String.IsNullOrEmpty(reelindex) Then
            If intermediates = -1 AndAlso timer.Enabled Then
                Static firstintermediatecount As Integer = 1
                Dim regularName As String = _ReelType & "_" & reelindex & "_" & firstintermediatecount.ToString()
                Dim illuminatedName As String = _ReelType & "_" & reelindex & If(SetID > 0, "_" & SetID.ToString(), "") & "_" & firstintermediatecount.ToString()
                If DrawAvailableReelImage(graphics, B2SData.ReelIntermediateImages, B2SData.ReelIntermediateIlluImages, regularName, illuminatedName) Then
                    firstintermediatecount += 1
                    intermediates2go = 2
                Else
                    regularName = _ReelType & "_" & ConvertText(_CurrentText + 1)
                    illuminatedName = regularName & If(SetID > 0, "_" & SetID.ToString(), "")
                    DrawAvailableReelImage(graphics, B2SData.ReelImages, B2SData.ReelIlluImages, regularName, illuminatedName)
                    intermediates = firstintermediatecount - 1
                    intermediates2go = 1
                End If
            ElseIf intermediates2go > 0 Then
                Dim intermediateIndex As String = (intermediates - intermediates2go + 1).ToString()
                Dim regularName As String = _ReelType & "_" & reelindex & "_" & intermediateIndex
                Dim illuminatedName As String = _ReelType & "_" & reelindex & If(SetID > 0, "_" & SetID.ToString(), "") & "_" & intermediateIndex
                DrawAvailableReelImage(graphics, B2SData.ReelIntermediateImages, B2SData.ReelIntermediateIlluImages, regularName, illuminatedName)
            Else
                Dim regularName As String = _ReelType & "_" & reelindex
                Dim illuminatedName As String = regularName & If(SetID > 0, "_" & SetID.ToString(), "")
                DrawAvailableReelImage(graphics, B2SData.ReelImages, B2SData.ReelIlluImages, regularName, illuminatedName)
            End If
        End If

    End Sub

    Private Function DrawAvailableReelImage(ByVal graphics As Graphics,
                                            ByVal regularImages As Generic.Dictionary(Of String, Image),
                                            ByVal illuminatedImages As Generic.Dictionary(Of String, Image),
                                            ByVal regularName As String,
                                            ByVal illuminatedName As String) As Boolean
        If _Illuminated AndAlso illuminatedImages.ContainsKey(illuminatedName) Then
            drawnFromIlluminated = True
            DrawReelImage(graphics, illuminatedImages(illuminatedName), illuminatedName & "|illuminated")
            Return True
        End If
        If regularImages.ContainsKey(regularName) Then
            drawnFromIlluminated = False
            DrawReelImage(graphics, regularImages(regularName), regularName & "|regular")
            Return True
        End If
        Return False
    End Function

    Private Sub DrawReelImage(ByVal graphics As Graphics, ByVal reelImage As Image, ByVal cacheName As String)
        ' The Realistic style sizes the number itself, from the original artwork.
        realisticSource = If(Reel3DEnabled AndAlso Reel3DStyle = ReelRealistic.StyleRealistic, reelImage, Nothing)
        If KeepDigitAspect Then
            Dim renderSize As Size = AuthoredReelSize()
            Using fitted As Bitmap = If(ReelDigitWidth > 0,
                                        ReelImageSizing.StretchToWidth(reelImage, renderSize, ReelDigitWidth),
                                        ReelImageSizing.Fit(reelImage, renderSize))
                DrawReelImageCore(graphics, fitted, cacheName & "|aspect")
            End Using
        Else
            DrawReelImageCore(graphics, reelImage, cacheName)
        End If
    End Sub

    Private Sub DrawReelImageCore(ByVal graphics As Graphics, ByVal reelImage As Image, ByVal cacheName As String)
        If graphics Is Nothing OrElse reelImage Is Nothing Then Return

        Dim renderSize As Size = AuthoredReelSize()
        Dim imageToDraw As Image = reelImage
        Dim rollingFrame As Bitmap = Nothing
        If Reel3DEnabled Then
            ' A triggered 3D reel keeps its depth and glass while inactive, but
            ' its backlight follows the same state as the legacy reel light.
            ' Without a trigger, preserve the original always-backlit behavior.
            Dim backlightOn As Boolean = (RomID <= 0 OrElse _Illuminated)
            Dim effectiveBrightness As Integer = If(backlightOn, Reel3DBrightness, 0)
            Dim effectiveTemperature As Integer = If(backlightOn, Reel3DTemperature, 4000)
            Dim cacheKey As String = cacheName & "|" & renderSize.Width.ToString() & "x" & renderSize.Height.ToString() &
                                     "|" & effectiveBrightness.ToString() & "|" & effectiveTemperature.ToString() & "|" & Reel3DDepth.ToString()
            If realisticSource IsNot Nothing Then
                ' The strip around the shown number: offsets -2 to +2 (+1 = next, above).
                Dim numbers(4) As Image
                For offset As Integer = -2 To 2
                    numbers(offset + 2) = If(offset = 0, realisticSource, NeighbourReelImage(offset))
                Next
                Dim above As Image = numbers(3)
                Dim below As Image = numbers(1)
                If smoothRolling AndAlso smoothRoll <> 0.0 Then
                    ' A turning drum changes every frame, so it is drawn, not cached.
                    rollingFrame = Reel3DEffect.RenderRealisticReel(numbers, renderSize,
                                                                    KeepDigitAspect, If(KeepDigitAspect, ReelDigitWidth, 0),
                                                                    effectiveBrightness, effectiveTemperature, Reel3DDepth,
                                                                    Reel3DCurve, Reel3DShadow, smoothRoll)
                Else
                    cacheKey &= "|realistic|" & Reel3DCurve.ToString() & "|" & Reel3DShadow.ToString() & "|" &
                                If(above Is Nothing, "-", above.GetHashCode().ToString()) & "|" & If(below Is Nothing, "-", below.GetHashCode().ToString())
                    If Not reel3DImageCache.ContainsKey(cacheKey) Then
                        Dim realistic As Bitmap = Reel3DEffect.RenderRealisticReel(numbers, renderSize,
                                                                                   KeepDigitAspect, If(KeepDigitAspect, ReelDigitWidth, 0),
                                                                                   effectiveBrightness, effectiveTemperature, Reel3DDepth,
                                                                                   Reel3DCurve, Reel3DShadow, 0.0)
                        If realistic IsNot Nothing Then reel3DImageCache.Add(cacheKey, realistic)
                    End If
                End If
            ElseIf Not reel3DImageCache.ContainsKey(cacheKey) Then
                Dim enhanced As Bitmap = Reel3DEffect.RenderReel(reelImage,
                                                                 renderSize,
                                                                 effectiveBrightness,
                                                                 effectiveTemperature,
                                                                 Reel3DDepth)
                If enhanced IsNot Nothing Then reel3DImageCache.Add(cacheKey, enhanced)
            End If
            If rollingFrame IsNot Nothing Then
                imageToDraw = rollingFrame
            ElseIf reel3DImageCache.ContainsKey(cacheKey) Then
                imageToDraw = reel3DImageCache(cacheKey)
            End If
        End If

        ' Legacy directB2S files and pre-unified B2S Pro exports keep the original
        ' 3.0.11 reel drawing exactly. Only backglasses written by the current
        ' Designer (UnifiedLayerDeck) use high-quality scaling.
        If (imageToDraw.Width = paintTarget.Width AndAlso imageToDraw.Height = paintTarget.Height) OrElse
           Not B2SData.UnifiedLayerDeck Then
            graphics.DrawImage(imageToDraw, paintTarget)
        Else
            Dim oldInterpolation As Drawing2D.InterpolationMode = graphics.InterpolationMode
            Dim oldPixelOffset As Drawing2D.PixelOffsetMode = graphics.PixelOffsetMode
            graphics.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
            graphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality
            Try
                graphics.DrawImage(imageToDraw,
                                   paintTarget,
                                   New Rectangle(Point.Empty, imageToDraw.Size),
                                   GraphicsUnit.Pixel)
            Finally
                graphics.InterpolationMode = oldInterpolation
                graphics.PixelOffsetMode = oldPixelOffset
            End Try
        End If

        ' Each digit crops its aligned piece from one display-wide overlay, so
        ' the reflection is continuous across the complete score window.
        If Reel3DEnabled AndAlso Reel3DDisplayWidth > 2 Then
            Dim overlayHeight As Integer = renderSize.Height
            Dim overlayKey As String = Reel3DDisplayWidth.ToString() & "x" & overlayHeight.ToString() &
                                       "|" & Reel3DDepth.ToString() & "|" & Reel3DGlass.ToString() & "|" & Reel3DGlassStyle.ToString() &
                                       "|" & Reel3DDividerWidth.ToString()
            If reel3DOverlayCache Is Nothing OrElse reel3DOverlayCacheKey <> overlayKey Then
                If reel3DOverlayCache IsNot Nothing Then reel3DOverlayCache.Dispose()
                reel3DOverlayCache = Reel3DEffect.CreateWindowOverlay(New Size(Reel3DDisplayWidth, overlayHeight),
                                                                      Reel3DDepth,
                                                                      Reel3DGlass,
                                                                      Reel3DGlassStyle,
                                                                      Reel3DDividerCenters,
                                                                      Reel3DDividerWidth)
                reel3DOverlayCacheKey = overlayKey
            End If
            If reel3DOverlayCache IsNot Nothing Then
                Dim sourceX As Integer = Math.Max(0, Math.Min(reel3DOverlayCache.Width - 1, Reel3DDisplayOffsetX))
                Dim sourceWidth As Integer = Math.Min(renderSize.Width, reel3DOverlayCache.Width - sourceX)
                If sourceWidth > 0 Then
                    graphics.DrawImage(reel3DOverlayCache,
                                       paintTarget,
                                       New Rectangle(sourceX, 0, sourceWidth, reel3DOverlayCache.Height),
                                       GraphicsUnit.Pixel)
                End If
            End If
        End If

        If rollingFrame IsNot Nothing Then rollingFrame.Dispose()

    End Sub

    ' Places this digit on its slice of the transformed score (design units).
    Friend Sub ApplyScoreTransform(ByVal digitQuad As PointF())
        scoreSlice = New ScoreTransformState(digitQuad)
        Me.Location = scoreSlice.Bounds.Location
        Me.Size = scoreSlice.Bounds.Size
        scoreSlice.ApplyRegion(Me)
    End Sub

    ' The number next to the shown one on the reel strip (+1 = above, -1 =
    ' below), from the same image set. Nothing when the reel shows no number.
    Private Function NeighbourReelImage(ByVal offset As Integer) As Image
        Dim digit As Integer
        If String.IsNullOrEmpty(reelindex) OrElse Not Integer.TryParse(reelindex, digit) OrElse digit < 0 OrElse digit > 9 Then Return Nothing
        Dim name As String = _ReelType & "_" & ConvertText((digit + offset + 10) Mod 10)
        If drawnFromIlluminated Then
            Dim illuminatedName As String = name & If(SetID > 0, "_" & SetID.ToString(), "")
            If B2SData.ReelIlluImages.ContainsKey(illuminatedName) Then Return B2SData.ReelIlluImages(illuminatedName)
        End If
        If B2SData.ReelImages.ContainsKey(name) Then Return B2SData.ReelImages(name)
        Return Nothing
    End Function

    Private Function AuthoredReelSize() As Size
        If ReelDesignSize.Width > 0 AndAlso ReelDesignSize.Height > 0 Then Return ReelDesignSize
        Return paintTarget.Size
    End Function

    Private Sub ClearReel3DCache()
        If reel3DImageCache IsNot Nothing Then
            For Each image As Bitmap In reel3DImageCache.Values
                image.Dispose()
            Next
            reel3DImageCache.Clear()
        End If
        If reel3DOverlayCache IsNot Nothing Then
            reel3DOverlayCache.Dispose()
            reel3DOverlayCache = Nothing
        End If
        reel3DOverlayCacheKey = String.Empty
    End Sub

    Protected Overrides Sub OnSizeChanged(ByVal e As EventArgs)
        ClearReel3DCache()
        MyBase.OnSizeChanged(e)
        If scoreSlice IsNot Nothing Then scoreSlice.ApplyRegion(Me)
    End Sub

    Public Property Reel3DEnabled As Boolean = False
    Public Property Reel3DBrightness As Integer = 100
    Public Property Reel3DTemperature As Integer = 4000
    Public Property Reel3DDepth As Integer = 100
    Public Property Reel3DGlass As Integer = 55
    Public Property Reel3DStyle As Integer = 0
    Public Property Reel3DCurve As Integer = 100
    Public Property Reel3DShadow As Integer = 100
    Public Property Reel3DGlassStyle As Integer = 0
    Private realisticSource As Image = Nothing

    ' Realistic style: the drum turns smoothly from one number to the next,
    ' one step taking the same time as a Classic step (ReelRealistic.StepMilliseconds).
    Private Const SmoothFrameInterval As Integer = 15
    Private smoothRolling As Boolean = False
    Private smoothRoll As Double = 0.0
    Private smoothStepStart As Double = 0.0
    Private ReadOnly smoothClock As New Diagnostics.Stopwatch()

    Private ReadOnly Property UsesSmoothRoll As Boolean
        Get
            Return Reel3DEnabled AndAlso Reel3DStyle = ReelRealistic.StyleRealistic AndAlso Not isLED
        End Get
    End Property

    Private Sub StartSmoothRoll()
        ' A roll in progress keeps turning toward the new number.
        If smoothRolling Then Return
        smoothRolling = True
        smoothRoll = 0.0
        smoothStepStart = 0.0
        smoothClock.Restart()
        timer.Stop()
        timer.Interval = SmoothFrameInterval
        timer.Start()
    End Sub

    Private Sub StopSmoothRoll()
        smoothRolling = False
        smoothRoll = 0.0
        smoothClock.Stop()
        timer.Stop()
        timer.Interval = CInt(_RollingInterval / (If((intermediates = -1), 3, intermediates) + 2))
    End Sub

    ' Intermediate images of the shown number, as the plugin counts them.
    Private Function SmoothIntermediateCount() As Integer
        Dim count As Integer = 0
        Do While B2SData.ReelIntermediateImages.ContainsKey(_ReelType & "_" & reelindex & "_" & (count + 1).ToString())
            count += 1
        Loop
        Return count
    End Function

    Private Sub SmoothRollTick()
        Dim stepMs As Double = ReelRealistic.StepMilliseconds(_RollingInterval, SmoothIntermediateCount())
        Dim phase As Double = (smoothClock.Elapsed.TotalMilliseconds - smoothStepStart) / stepMs
        Do While phase >= 1.0
            smoothStepStart += stepMs
            phase -= 1.0
            ' add one reel step (as the Classic timer does)
            _CurrentText += 1
            If _CurrentText > 9 Then
                _CurrentText = 0
                RaiseEvent ReelRollOver(Me, New ReelRollOverEventArgs(ID))
            End If
            reelindex = ConvertText(_CurrentText)
            Try
                If Sound() IsNot Nothing Then
                    My.Computer.Audio.Play(Sound(), AudioPlayMode.Background)
                ElseIf SoundName() = "stille" Then
                    ' no sound
                Else
                    My.Computer.Audio.Play(My.Resources.EMReel, AudioPlayMode.Background)
                End If
            Catch
            End Try
            If _CurrentText = _Text OrElse _Text >= 10 Then
                StopSmoothRoll()
                Me.Invalidate()
                Return
            End If
        Loop
        smoothRoll = phase
        Me.Invalidate()
    End Sub
    Private drawnFromIlluminated As Boolean = False
    Public Property Reel3DDisplayWidth As Integer = 0
    Public Property Reel3DDisplayOffsetX As Integer = 0
    ' B2S Pro reel dividers: line width in design pixels (0 = off) and the digit
    ' boundaries of the whole display, in display (overlay) pixels.
    Public Property Reel3DDividerWidth As Integer = 0
    Public Property Reel3DDividerCenters As Double() = Nothing
    ' B2S Pro: the gap to the next digit of a rotated/perspective display, in
    ' design units (Nothing for the last digit or a straight-on display).
    Friend Property Reel3DGapDesignQuad As PointF() = Nothing

    ' Design-unit box of this digit's transformed slice (empty when straight on).
    Friend ReadOnly Property ScoreSliceDesignBounds As Rectangle
        Get
            Return If(scoreSlice Is Nothing, Rectangle.Empty, scoreSlice.Bounds)
        End Get
    End Property
    'Protected Overrides Sub OnPaintBackground(pevent As System.Windows.Forms.PaintEventArgs)

    '    ' nothing to do but important

    'End Sub

    Public Sub New()

        ' set some styles
        Me.SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or ControlStyles.DoubleBuffer, True) 
        'Or ControlStyles.SupportsTransparentBackColor, True)

        Me.DoubleBuffered = True

        ' let transparent reel art render over the backglass
        'Me.BackColor = Color.Transparent

        ' create timer
        timer = New Timer()
        timer.Interval = CInt(_RollingInterval / (If((intermediates = -1), 3, intermediates) + 2))
        AddHandler timer.Tick, AddressOf ReelAnimationTimer_Tick

    End Sub

    Private Sub B2SReelBox_Disposed(sender As Object, e As System.EventArgs) Handles Me.Disposed
        On Error Resume Next
        If timer IsNot Nothing Then
            timer.Stop()
            RemoveHandler timer.Tick, AddressOf ReelAnimationTimer_Tick
            timer.Dispose()
            timer = Nothing
        End If
    End Sub

    Private Sub ReelAnimationTimer_Tick(ByVal sender As Object, ByVal e As System.EventArgs)

        If smoothRolling Then
            SmoothRollTick()
            Return
        End If

        If intermediates2go > 0 OrElse intermediates = -1 Then

            Me.Invalidate()
            intermediates2go -= 1

        Else

            If intermediates2go = 0 Then
                ' add one reel step
                _CurrentText += 1
                If _CurrentText > 9 Then
                    _CurrentText = 0
                    RaiseEvent ReelRollOver(Me, New ReelRollOverEventArgs(ID))
                End If

                reelindex = ConvertText(_CurrentText)

                ' play sound and redraw reel
                Try
                    If Sound() IsNot Nothing Then
                        My.Computer.Audio.Play(Sound(), AudioPlayMode.Background)
                    ElseIf SoundName() = "stille" Then
                        ' no sound
                    Else
                        My.Computer.Audio.Play(My.Resources.EMReel, AudioPlayMode.Background)
                    End If
                Catch
                End Try
                Me.Invalidate()

                intermediates2go -= 1
            ElseIf intermediates2go = -1 Then
                intermediates2go -= 1
            Else
                ' maybe stop timer
                intermediates2go = intermediates
                If _CurrentText = _Text OrElse _Text >= 10 Then
                    timer.Stop()
                    timer.Interval = CInt(_RollingInterval / (If((intermediates = -1), 3, intermediates) + 2))
                End If
            End If

        End If

    End Sub

    Public Property SetID() As Integer

    Private _ReelType As String
    Public Property ReelType() As String
        Get
            Return _ReelType
        End Get
        Set(ByVal value As String)
            reelindex = "0"
            If value.Substring(value.Length - 1, 1) = "_" Then
                length = 2
                reelindex = "00"
                value = value.Substring(0, value.Length - 1)
            End If
            If value.StartsWith("LED", StringComparison.CurrentCultureIgnoreCase) OrElse value.StartsWith("ImportedLED", StringComparison.CurrentCultureIgnoreCase) Then
                isLED = True
                reelindex = "Empty"
                initValue = "Empty"
                _Text = -1
            End If
            _ReelType = value
        End Set
    End Property

    Public Property SoundName() As String = String.Empty
    Public Property Sound() As Byte() = Nothing

    Public Property ScoreType() As eScoreType = eScoreType.NotUsed

    Public Property GroupName() As String = String.Empty

    Private _Illuminated As Boolean
    Public Property Illuminated() As Boolean
        Get
            Return _Illuminated
        End Get
        Set(ByVal value As Boolean)
            If _Illuminated <> value Then
                _Illuminated = value
                intermediates2go = 0
                Me.Invalidate()
            End If
        End Set
    End Property

    Private _Value As Integer = 0
    Public Property Value(Optional ByVal refresh As Boolean = False) As Integer
        Get
            Return _Value
        End Get
        Set(ByVal value As Integer)
            If _Value <> value OrElse refresh Then
                _Value = value
                reelindex = ConvertValue(_Value)
                Me.Invalidate()
            End If
        End Set
    End Property

    Private _CurrentText As Integer = 0
    Private _Text As Integer = 0
    Public Shadows Property Text(Optional ByVal AnimateReelChange As Boolean = True) As Integer
        Get
            Return _Text
        End Get
        Set(ByVal value As Integer)
            If value >= 0 Then
                If _Text <> value Then
                    _Text = value
                    If AnimateReelChange AndAlso Not isLED AndAlso UsesSmoothRoll Then
                        StartSmoothRoll()
                    ElseIf AnimateReelChange AndAlso Not isLED Then
                        timer.Stop()
                        intermediates2go = intermediates
                        timer.Start()
                    Else
                        reelindex = ConvertText(_Text)
                        Me.Invalidate()
                    End If
                End If
            End If
        End Set
    End Property
    Public ReadOnly Property CurrentText() As Integer
        Get
            Return _CurrentText
        End Get
    End Property

    Private _RollingInterval As Integer = cTimerInterval
    Public Property RollingInterval() As Integer
        Get
            Return _RollingInterval
        End Get
        Set(ByVal value As Integer)
            If _RollingInterval <> value Then
                _RollingInterval = value
                If _RollingInterval < 10 Then _RollingInterval = cTimerInterval
            End If
        End Set
    End Property

    Public ReadOnly Property IsInReelRolling() As Boolean
        Get
            If smoothRolling Then Return True
            Return (intermediates2go <= 0)
        End Get
    End Property
    Public ReadOnly Property IsInAction() As Boolean
        Get
            Return timer.Enabled
        End Get
    End Property

    Private Function ConvertValue(ByVal value As Integer) As String
        Dim ret As String = initValue
        ' remove the "," from the 7-segmenter
        If value >= 128 AndAlso value <= 255 Then
            value -= 128
        End If
        ' map value
        If value > 0 Then
            Select Case value
                ' 7-segment stuff
                Case 63
                    ret = "0"
                Case 6
                    ret = "1"
                Case 91
                    ret = "2"
                Case 79
                    ret = "3"
                Case 102
                    ret = "4"
                Case 109
                    ret = "5"
                Case 125
                    ret = "6"
                Case 7
                    ret = "7"
                Case 127
                    ret = "8"
                Case 111
                    ret = "9"
                Case Else
                    'additional 10-segment stuff
                    Select Case value
                        Case 768
                            ret = "1"
                        Case 124
                            ret = "6"
                        Case 103
                            ret = "9"
                            'Case Else
                            '    Debug.WriteLine(_Value)
                    End Select
            End Select
        End If
        Return If(length = 2, "0", "") & ret
    End Function
    Private Function ConvertText(ByVal text As Integer) As String
        Dim ret As String = String.Empty
        ret = "00" & text.ToString()
        ret = ret.Substring(ret.Length - length, length)
        Return ret
    End Function

End Class

' B2S Pro score display transform, matching the Designer
' (B2SPictureBox.ScorePerspectivePoints and DrawPerspectiveBitmap).
Friend NotInheritable Class ScoreTransform
    Public ReadOnly RotationAngle As Single
    Public ReadOnly PerspectiveDepth As Single
    Public ReadOnly PerspectiveLeftScale As Single
    Public ReadOnly PerspectiveRightScale As Single

    Private Sub New(ByVal angle As Single, ByVal depth As Single, ByVal leftScale As Single, ByVal rightScale As Single)
        RotationAngle = angle
        PerspectiveDepth = depth
        PerspectiveLeftScale = leftScale
        PerspectiveRightScale = rightScale
    End Sub

    ' Returns Nothing when the score is drawn straight on.
    Public Shared Function Read(ByVal node As Xml.XmlNode) As ScoreTransform
        Dim angle As Single = ReadSingle(node, "RotationAngle", 0.0F)
        Dim depth As Single = ReadSingle(node, "PerspectiveDepth", 0.0F)
        Dim leftScale As Single = ReadSingle(node, "PerspectiveLeftScale", 1.0F)
        Dim rightScale As Single = ReadSingle(node, "PerspectiveRightScale", 1.0F)
        If Math.Abs(angle) <= 0.001F AndAlso Math.Abs(depth) <= 0.001F AndAlso
           Math.Abs(leftScale - 1.0F) <= 0.001F AndAlso Math.Abs(rightScale - 1.0F) <= 0.001F Then Return Nothing
        Return New ScoreTransform(angle, depth, leftScale, rightScale)
    End Function

    Private Shared Function ReadSingle(ByVal node As Xml.XmlNode, ByVal name As String, ByVal fallback As Single) As Single
        Dim value As Single = fallback
        If node IsNot Nothing AndAlso node.Attributes(name) IsNot Nothing Then
            If Not Single.TryParse(node.Attributes(name).InnerText, Globalization.NumberStyles.Float,
                                   Globalization.CultureInfo.InvariantCulture, value) Then value = fallback
        End If
        Return value
    End Function

    ' The four corners (top-left, top-right, bottom-right, bottom-left) of the
    ' score rectangle after perspective and rotation, in design units.
    Public Function DisplayQuad(ByVal rect As RectangleF) As PointF()
        Dim centerX As Single = rect.X + rect.Width / 2.0F
        Dim centerY As Single = rect.Y + rect.Height / 2.0F
        Dim halfWidth As Single = rect.Width / 2.0F
        Dim halfHeight As Single = rect.Height / 2.0F
        Dim depth As Single = Math.Max(-1.0F, Math.Min(1.0F, PerspectiveDepth))
        Dim amount As Single = Math.Abs(depth)
        Dim projectedHalfWidth As Single = halfWidth * (1.0F - 0.34F * amount)
        Dim nearScale As Single = 1.0F + 0.06F * amount
        Dim farScale As Single = 1.0F - 0.58F * amount
        Dim leftScale As Single = If(depth >= 0.0F, nearScale, farScale) * Math.Max(0.2F, Math.Min(2.5F, PerspectiveLeftScale))
        Dim rightScale As Single = If(depth >= 0.0F, farScale, nearScale) * Math.Max(0.2F, Math.Min(2.5F, PerspectiveRightScale))
        Dim points() As PointF = {
            New PointF(centerX - projectedHalfWidth, centerY - halfHeight * leftScale),
            New PointF(centerX + projectedHalfWidth, centerY - halfHeight * rightScale),
            New PointF(centerX + projectedHalfWidth, centerY + halfHeight * rightScale),
            New PointF(centerX - projectedHalfWidth, centerY + halfHeight * leftScale)}
        If Math.Abs(RotationAngle) > 0.001F Then
            Dim radians As Double = RotationAngle * Math.PI / 180.0
            For i As Integer = 0 To points.Length - 1
                Dim dx As Double = points(i).X - centerX
                Dim dy As Double = points(i).Y - centerY
                points(i) = New PointF(CSng(centerX + dx * Math.Cos(radians) - dy * Math.Sin(radians)),
                                       CSng(centerY + dx * Math.Sin(radians) + dy * Math.Cos(radians)))
            Next
        End If
        Return points
    End Function

    ' The part of the quad covering the flat fractions u0..u1 across and v0..v1 down.
    Public Shared Function SubQuad(ByVal quad As PointF(), ByVal u0 As Single, ByVal u1 As Single,
                                   ByVal v0 As Single, ByVal v1 As Single) As PointF()
        Return {Bilinear(quad, u0, v0), Bilinear(quad, u1, v0), Bilinear(quad, u1, v1), Bilinear(quad, u0, v1)}
    End Function

    Private Shared Function Bilinear(ByVal quad As PointF(), ByVal u As Single, ByVal v As Single) As PointF
        Dim top As PointF = Lerp(quad(0), quad(1), u)
        Dim bottom As PointF = Lerp(quad(3), quad(2), u)
        Return Lerp(top, bottom, v)
    End Function

    Private Shared Function Lerp(ByVal a As PointF, ByVal b As PointF, ByVal t As Single) As PointF
        Return New PointF(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t)
    End Function

    Public Shared Function Distance(ByVal a As PointF, ByVal b As PointF) As Double
        Return Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y))
    End Function

    Public Shared Function BoundingBox(ByVal quad As PointF()) As Rectangle
        Return Rectangle.FromLTRB(CInt(Math.Floor(quad.Min(Function(point) point.X))),
                                  CInt(Math.Floor(quad.Min(Function(point) point.Y))),
                                  CInt(Math.Ceiling(quad.Max(Function(point) point.X))),
                                  CInt(Math.Ceiling(quad.Max(Function(point) point.Y))))
    End Function

    ' Draws the flat bitmap onto the quad: one affine draw for a rotated
    ' rectangle, vertical strips for a perspective trapezoid (as the Designer).
    Public Shared Sub DrawWarped(ByVal graphics As Graphics, ByVal bitmap As Bitmap, ByVal quad As PointF())
        If graphics Is Nothing OrElse bitmap Is Nothing OrElse quad Is Nothing OrElse quad.Length <> 4 Then Return
        Dim oldMode As Drawing2D.InterpolationMode = graphics.InterpolationMode
        graphics.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
        Try
            If Math.Abs(quad(0).X + quad(2).X - quad(1).X - quad(3).X) < 0.01F AndAlso
               Math.Abs(quad(0).Y + quad(2).Y - quad(1).Y - quad(3).Y) < 0.01F Then
                graphics.DrawImage(bitmap, New PointF() {quad(0), quad(1), quad(3)},
                                   New RectangleF(0, 0, bitmap.Width, bitmap.Height), GraphicsUnit.Pixel)
                Return
            End If
            Dim strips As Integer = Math.Max(24, Math.Min(160, bitmap.Width))
            For i As Integer = 0 To strips - 1
                Dim t0 As Single = CSng(i / CDbl(strips))
                Dim t1 As Single = CSng((i + 1) / CDbl(strips))
                Dim top0 As PointF = Lerp(quad(0), quad(1), t0)
                Dim top1 As PointF = Lerp(quad(0), quad(1), t1)
                Dim bottom0 As PointF = Lerp(quad(3), quad(2), t0)
                Dim srcX0 As Single = bitmap.Width * t0
                Dim srcX1 As Single = bitmap.Width * t1
                graphics.DrawImage(bitmap, New PointF() {top0, top1, bottom0},
                                   New RectangleF(srcX0, 0, Math.Max(1.0F, srcX1 - srcX0 + 0.75F), bitmap.Height), GraphicsUnit.Pixel)
            Next
        Finally
            graphics.InterpolationMode = oldMode
        End Try
    End Sub
End Class

' B2S Pro: the strip between two digits of a 3D reel display. The Designer
' draws the window overlay (inset shadow and glass) across the whole score
' rectangle, gaps included; a B2SReelBox draws only its own part, so the
' backglass draws the gap parts at the score's place in the layer deck.
Friend NotInheritable Class ReelGapOverlay
    Public ReadOnly ZOrder As Integer
    Private ReadOnly leftReel As B2SReelBox
    Private ReadOnly rightReel As B2SReelBox

    Private Shared ReadOnly overlays As New Generic.Dictionary(Of String, Bitmap)()
    Private Shared ReadOnly slices As New Generic.Dictionary(Of String, Bitmap)()

    Private Sub New(ByVal leftReel As B2SReelBox, ByVal rightReel As B2SReelBox)
        Me.leftReel = leftReel
        Me.rightReel = rightReel
        ZOrder = leftReel.UnifiedLayerZOrder
    End Sub

    ' The gaps of the visible 3D reel displays on this parent, back to front.
    ' Empty for legacy files and for displays without 3D reels.
    Public Shared Function Collect(ByVal parent As Control) As Generic.List(Of ReelGapOverlay)
        Dim result As New Generic.List(Of ReelGapOverlay)()
        If Not B2SData.UnifiedLayerDeck OrElse parent Is Nothing Then Return result
        For Each display As B2SReelDisplay In B2SData.ReelDisplays.Values
            Dim reels As New Generic.List(Of B2SReelBox)()
            For Each reel As B2SReelBox In display.Reels.Values
                If reel IsNot Nothing AndAlso reel.Parent Is parent AndAlso reel.Visible AndAlso
                   reel.Reel3DEnabled AndAlso reel.Reel3DDisplayWidth > 2 AndAlso reel.ReelDesignSize.Height > 0 Then
                    reels.Add(reel)
                End If
            Next
            reels.Sort(Function(a, b) a.Reel3DDisplayOffsetX.CompareTo(b.Reel3DDisplayOffsetX))
            For i As Integer = 0 To reels.Count - 2
                If reels(i + 1).Reel3DDisplayOffsetX > reels(i).Reel3DDisplayOffsetX + reels(i).ReelDesignSize.Width Then
                    result.Add(New ReelGapOverlay(reels(i), reels(i + 1)))
                End If
            Next
        Next
        result.Sort(Function(a, b) a.ZOrder.CompareTo(b.ZOrder))
        Return result
    End Function

    ' Draws this gap's part of the display-wide overlay: the same overlay the
    ' digits crop from, mapped like the digits (flat box or warped slice).
    Public Sub Draw(ByVal graphics As Graphics)
        Dim overlay As Bitmap = DisplayOverlay(leftReel)
        If overlay Is Nothing Then Return
        Dim sourceX0 As Integer = Math.Max(0, Math.Min(overlay.Width, leftReel.Reel3DDisplayOffsetX + leftReel.ReelDesignSize.Width))
        Dim sourceX1 As Integer = Math.Max(sourceX0, Math.Min(overlay.Width, rightReel.Reel3DDisplayOffsetX))
        If sourceX1 <= sourceX0 Then Return
        Dim source As New Rectangle(sourceX0, 0, sourceX1 - sourceX0, overlay.Height)
        Dim state As Drawing2D.GraphicsState = graphics.Save()
        Try
            graphics.CompositingMode = Drawing2D.CompositingMode.SourceOver
            Dim gapQuad As PointF() = leftReel.Reel3DGapDesignQuad
            If gapQuad Is Nothing Then
                ' Same interpolation the digit uses for its overlay part.
                graphics.InterpolationMode = Drawing2D.InterpolationMode.Bilinear
                graphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.Default
                Dim target As Rectangle = Rectangle.FromLTRB(leftReel.Right, leftReel.Top, rightReel.Left, leftReel.Bottom)
                If target.Width > 0 AndAlso target.Height > 0 Then graphics.DrawImage(overlay, target, source, GraphicsUnit.Pixel)
            Else
                Dim design As Rectangle = leftReel.ScoreSliceDesignBounds
                If design.Width <= 0 OrElse design.Height <= 0 Then Return
                Dim scaleX As Single = leftReel.Width / CSng(design.Width)
                Dim scaleY As Single = leftReel.Height / CSng(design.Height)
                Dim target As PointF() = gapQuad.Select(Function(point) New PointF(leftReel.Left + (point.X - design.X) * scaleX,
                                                                                    leftReel.Top + (point.Y - design.Y) * scaleY)).ToArray()
                ScoreTransform.DrawWarped(graphics, OverlaySlice(overlay, source), target)
            End If
        Finally
            graphics.Restore(state)
        End Try
    End Sub

    ' The display overlay exactly as B2SReelBox builds it (same size and settings).
    Private Shared Function DisplayOverlay(ByVal reel As B2SReelBox) As Bitmap
        Dim size As New Size(reel.Reel3DDisplayWidth, reel.ReelDesignSize.Height)
        Dim key As String = size.Width.ToString() & "x" & size.Height.ToString() & "|" & reel.Reel3DDepth.ToString() &
                            "|" & reel.Reel3DGlass.ToString() & "|" & reel.Reel3DGlassStyle.ToString() &
                            "|" & reel.Reel3DDividerWidth.ToString() & "|" & DividerKey(reel.Reel3DDividerCenters)
        Dim overlay As Bitmap = Nothing
        If Not overlays.TryGetValue(key, overlay) Then
            overlay = Reel3DEffect.CreateWindowOverlay(size, reel.Reel3DDepth, reel.Reel3DGlass, reel.Reel3DGlassStyle,
                                                       reel.Reel3DDividerCenters, reel.Reel3DDividerWidth)
            overlays(key) = overlay
        End If
        Return overlay
    End Function

    Private Shared Function DividerKey(ByVal centers As Double()) As String
        If centers Is Nothing Then Return "-"
        Return String.Join(",", centers.Select(Function(center) center.ToString(Globalization.CultureInfo.InvariantCulture)))
    End Function

    Private Shared Function OverlaySlice(ByVal overlay As Bitmap, ByVal source As Rectangle) As Bitmap
        Dim key As String = overlay.GetHashCode().ToString() & "|" & source.ToString()
        Dim slice As Bitmap = Nothing
        If Not slices.TryGetValue(key, slice) Then
            slice = overlay.Clone(source, overlay.PixelFormat)
            slices(key) = slice
        End If
        Return slice
    End Function
End Class

' The slice of a transformed score that one control shows, in design units
' relative to the control's box. B2SScreen scales the box after placement, so
' the slice is scaled with the control's client size.
Friend NotInheritable Class ScoreTransformState
    Private ReadOnly quad As PointF()
    Private ReadOnly designSize As SizeF
    Public ReadOnly Bounds As Rectangle

    Public Sub New(ByVal absoluteQuad As PointF())
        Bounds = ScoreTransform.BoundingBox(absoluteQuad)
        designSize = New SizeF(Math.Max(1, Bounds.Width), Math.Max(1, Bounds.Height))
        quad = absoluteQuad.Select(Function(point) New PointF(point.X - Bounds.X, point.Y - Bounds.Y)).ToArray()
    End Sub

    ' The slice in client pixels.
    Public Function Scaled(ByVal clientSize As Size) As PointF()
        Dim scaleX As Single = clientSize.Width / designSize.Width
        Dim scaleY As Single = clientSize.Height / designSize.Height
        Return quad.Select(Function(point) New PointF(point.X * scaleX, point.Y * scaleY)).ToArray()
    End Function

    ' The flat size of the content in client pixels (top edge by left edge).
    Public Function ContentSize(ByVal clientSize As Size) As Size
        Dim points As PointF() = Scaled(clientSize)
        Return New Size(Math.Max(1, CInt(Math.Round(ScoreTransform.Distance(points(0), points(1))))),
                        Math.Max(1, CInt(Math.Round(ScoreTransform.Distance(points(0), points(3))))))
    End Function

    ' Only the slice belongs to the control; the rest of its box shows what is behind it.
    Public Sub ApplyRegion(ByVal control As Control)
        Using path As New Drawing2D.GraphicsPath()
            path.AddPolygon(Scaled(control.ClientSize))
            Dim oldRegion As Region = control.Region
            control.Region = New Region(path)
            If oldRegion IsNot Nothing Then oldRegion.Dispose()
        End Using
    End Sub
End Class
