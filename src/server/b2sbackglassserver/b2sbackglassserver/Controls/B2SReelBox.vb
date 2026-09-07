Imports System
Imports System.Windows.Forms
Imports System.Drawing

Public Class B2SReelBox

    Inherits B2SBaseBox

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

        If Not String.IsNullOrEmpty(reelindex) Then
            If intermediates = -1 AndAlso timer.Enabled Then
                Static firstintermediatecount As Integer = 1
                Dim regularName As String = _ReelType & "_" & reelindex & "_" & firstintermediatecount.ToString()
                Dim illuminatedName As String = _ReelType & "_" & reelindex & If(SetID > 0, "_" & SetID.ToString(), "") & "_" & firstintermediatecount.ToString()
                If DrawAvailableReelImage(e.Graphics, B2SData.ReelIntermediateImages, B2SData.ReelIntermediateIlluImages, regularName, illuminatedName) Then
                    firstintermediatecount += 1
                    intermediates2go = 2
                Else
                    regularName = _ReelType & "_" & ConvertText(_CurrentText + 1)
                    illuminatedName = regularName & If(SetID > 0, "_" & SetID.ToString(), "")
                    DrawAvailableReelImage(e.Graphics, B2SData.ReelImages, B2SData.ReelIlluImages, regularName, illuminatedName)
                    intermediates = firstintermediatecount - 1
                    intermediates2go = 1
                End If
            ElseIf intermediates2go > 0 Then
                Dim intermediateIndex As String = (intermediates - intermediates2go + 1).ToString()
                Dim regularName As String = _ReelType & "_" & reelindex & "_" & intermediateIndex
                Dim illuminatedName As String = _ReelType & "_" & reelindex & If(SetID > 0, "_" & SetID.ToString(), "") & "_" & intermediateIndex
                DrawAvailableReelImage(e.Graphics, B2SData.ReelIntermediateImages, B2SData.ReelIntermediateIlluImages, regularName, illuminatedName)
            Else
                Dim regularName As String = _ReelType & "_" & reelindex
                Dim illuminatedName As String = regularName & If(SetID > 0, "_" & SetID.ToString(), "")
                DrawAvailableReelImage(e.Graphics, B2SData.ReelImages, B2SData.ReelIlluImages, regularName, illuminatedName)
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
        If graphics Is Nothing OrElse reelImage Is Nothing Then Return

        Dim imageToDraw As Image = reelImage
        If Reel3DEnabled Then
            ' A triggered 3D reel keeps its depth and glass while inactive, but
            ' its backlight follows the same state as the legacy reel light.
            ' Without a trigger, preserve the original always-backlit behavior.
            Dim backlightOn As Boolean = (RomID <= 0 OrElse _Illuminated)
            Dim effectiveBrightness As Integer = If(backlightOn, Reel3DBrightness, 0)
            Dim effectiveTemperature As Integer = If(backlightOn, Reel3DTemperature, 4000)
            Dim cacheKey As String = cacheName & "|" & ClientSize.Width.ToString() & "x" & ClientSize.Height.ToString() &
                                     "|" & effectiveBrightness.ToString() & "|" & effectiveTemperature.ToString() & "|" & Reel3DDepth.ToString()
            If Not reel3DImageCache.ContainsKey(cacheKey) Then
                Dim enhanced As Bitmap = Reel3DEffect.RenderReel(reelImage,
                                                                 ClientSize,
                                                                 effectiveBrightness,
                                                                 effectiveTemperature,
                                                                 Reel3DDepth)
                If enhanced IsNot Nothing Then reel3DImageCache.Add(cacheKey, enhanced)
            End If
            If reel3DImageCache.ContainsKey(cacheKey) Then imageToDraw = reel3DImageCache(cacheKey)
        End If

        graphics.DrawImage(imageToDraw, Me.ClientRectangle)

        ' Score controls are WinForms child controls and normally always appear
        ' above their parent's background. For a reel placed behind the canvas,
        ' composite the matching canvas patch over the live reel frame. Canvas
        ' transparency exposes the reel while opaque artwork remains in front.
        If _BehindCanvas AndAlso Me.Parent IsNot Nothing AndAlso Me.Parent.BackgroundImage IsNot Nothing Then
            Dim canvas As Image = Me.Parent.BackgroundImage
            Dim source As New Rectangle(Me.Left, Me.Top, Me.Width, Me.Height)
            graphics.DrawImage(canvas, Me.ClientRectangle, source, GraphicsUnit.Pixel)
        End If

        ' Glass sits in front of both the reel and the canvas. Each digit crops
        ' its aligned piece from one display-wide overlay, so the reflection is
        ' continuous across the complete score window.
        If Reel3DEnabled AndAlso Reel3DDisplayWidth > 2 Then
            Dim overlayKey As String = Reel3DDisplayWidth.ToString() & "x" & ClientSize.Height.ToString() &
                                       "|" & Reel3DDepth.ToString() & "|" & Reel3DGlass.ToString()
            If reel3DOverlayCache Is Nothing OrElse reel3DOverlayCacheKey <> overlayKey Then
                If reel3DOverlayCache IsNot Nothing Then reel3DOverlayCache.Dispose()
                reel3DOverlayCache = Reel3DEffect.CreateWindowOverlay(New Size(Reel3DDisplayWidth, ClientSize.Height),
                                                                      Reel3DDepth,
                                                                      Reel3DGlass)
                reel3DOverlayCacheKey = overlayKey
            End If
            If reel3DOverlayCache IsNot Nothing Then
                Dim sourceX As Integer = Math.Max(0, Math.Min(reel3DOverlayCache.Width - 1, Reel3DDisplayOffsetX))
                Dim sourceWidth As Integer = Math.Min(ClientSize.Width, reel3DOverlayCache.Width - sourceX)
                If sourceWidth > 0 Then
                    graphics.DrawImage(reel3DOverlayCache,
                                       New Rectangle(0, 0, sourceWidth, ClientSize.Height),
                                       New Rectangle(sourceX, 0, sourceWidth, reel3DOverlayCache.Height),
                                       GraphicsUnit.Pixel)
                End If
            End If
        End If
    End Sub

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
    End Sub

    Private _BehindCanvas As Boolean = False
    Public Property BehindCanvas() As Boolean
        Get
            Return _BehindCanvas
        End Get
        Set(ByVal value As Boolean)
            If _BehindCanvas <> value Then
                _BehindCanvas = value
                Me.Invalidate()
            End If
        End Set
    End Property

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
