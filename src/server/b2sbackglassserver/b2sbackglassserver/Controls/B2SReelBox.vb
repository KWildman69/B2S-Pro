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
            DrawReelImage(graphics, illuminatedImages(illuminatedName), illuminatedName & "|illuminated")
            Return True
        End If
        If regularImages.ContainsKey(regularName) Then
            DrawReelImage(graphics, regularImages(regularName), regularName & "|regular")
            Return True
        End If
        Return False
    End Function

    Private Sub DrawReelImage(ByVal graphics As Graphics, ByVal reelImage As Image, ByVal cacheName As String)
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
        If Reel3DEnabled Then
            ' A triggered 3D reel keeps its depth and glass while inactive, but
            ' its backlight follows the same state as the legacy reel light.
            ' Without a trigger, preserve the original always-backlit behavior.
            Dim backlightOn As Boolean = (RomID <= 0 OrElse _Illuminated)
            Dim effectiveBrightness As Integer = If(backlightOn, Reel3DBrightness, 0)
            Dim effectiveTemperature As Integer = If(backlightOn, Reel3DTemperature, 4000)
            Dim cacheKey As String = cacheName & "|" & renderSize.Width.ToString() & "x" & renderSize.Height.ToString() &
                                     "|" & effectiveBrightness.ToString() & "|" & effectiveTemperature.ToString() & "|" & Reel3DDepth.ToString()
            If Not reel3DImageCache.ContainsKey(cacheKey) Then
                Dim enhanced As Bitmap = Reel3DEffect.RenderReel(reelImage,
                                                                 renderSize,
                                                                 effectiveBrightness,
                                                                 effectiveTemperature,
                                                                 Reel3DDepth)
                If enhanced IsNot Nothing Then reel3DImageCache.Add(cacheKey, enhanced)
            End If
            If reel3DImageCache.ContainsKey(cacheKey) Then imageToDraw = reel3DImageCache(cacheKey)
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
                                       "|" & Reel3DDepth.ToString() & "|" & Reel3DGlass.ToString()
            If reel3DOverlayCache Is Nothing OrElse reel3DOverlayCacheKey <> overlayKey Then
                If reel3DOverlayCache IsNot Nothing Then reel3DOverlayCache.Dispose()
                reel3DOverlayCache = Reel3DEffect.CreateWindowOverlay(New Size(Reel3DDisplayWidth, overlayHeight),
                                                                      Reel3DDepth,
                                                                      Reel3DGlass)
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

    End Sub

    ' Places this digit on its slice of the transformed score (design units).
    Friend Sub ApplyScoreTransform(ByVal digitQuad As PointF())
        scoreSlice = New ScoreTransformState(digitQuad)
        Me.Location = scoreSlice.Bounds.Location
        Me.Size = scoreSlice.Bounds.Size
        scoreSlice.ApplyRegion(Me)
    End Sub

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
    Public Property Reel3DDisplayWidth As Integer = 0
    Public Property Reel3DDisplayOffsetX As Integer = 0
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
                    If AnimateReelChange AndAlso Not isLED Then
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
