Imports System
Imports System.Drawing.Imaging

Public Class B2SPictureBox

    Inherits PictureBox

    ' A transparent backglass pixel is an opening in the printed canvas. Show
    ' that opening over a neutral mid-gray backing in the editor. This leaves
    ' visible headroom for behind-canvas brightness, highlights, color and light
    ' temperature while the renderer still treats transparency as transmissive.
    ' Opaque artwork, including true black pixels, is drawn unchanged.
    Private Shared ReadOnly EditorCanvasUnderlayColor As Color = Color.FromArgb(96, 96, 96)

    Private ReadOnly lightBlinkTimer As Windows.Forms.Timer
    Private ReadOnly lightBlinkStartedAt As Long = Diagnostics.Stopwatch.GetTimestamp()

    ' Enhanced 2.8.6: reduce flicker and repaint overhead on large canvases.
    Public Sub New()
        MyBase.New()
        Me.DoubleBuffered = True
        Me.ResizeRedraw = False
        Me.SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or ControlStyles.OptimizedDoubleBuffer, True)
        Me.UpdateStyles()
        lightBlinkTimer = New Windows.Forms.Timer With {.Interval = 25}
        AddHandler lightBlinkTimer.Tick, AddressOf LightBlinkTimer_Tick
    End Sub

    Private Sub LightBlinkTimer_Tick(ByVal sender As Object, ByVal e As EventArgs)
        MyBase.Invalidate()
    End Sub

    Private Function IsBlinkPhaseOn(ByVal bulb As Illumination.BulbInfo) As Boolean
        ' Designer illumination preview is a steady, inspectable view of the
        ' finished lights. Runtime blink timing must not make that preview go
        ' dark or appear to jump between bulbs after selection/canvas clicks.
        If ShowIllumination Then Return True
        If bulb Is Nothing OrElse Not bulb.BlinkEnabled OrElse bulb.IsImageSnippit OrElse bulb.IlluMode = Illumination.eIlluMode.Flasher Then Return True
        Dim interval As Integer = Math.Max(1, Math.Min(60000, bulb.BlinkInterval))
        Dim elapsedMilliseconds As Double = (Diagnostics.Stopwatch.GetTimestamp() - lightBlinkStartedAt) * 1000.0R / Diagnostics.Stopwatch.Frequency
        Return (CLng(Math.Floor(elapsedMilliseconds / interval)) Mod 2L) = 0L
    End Function

    Private Sub UpdateLightBlinkTimer()
        ' WinForms can invoke OnPaint from the base PictureBox constructor before
        ' this derived class has created its timer. Early startup paint is valid;
        ' there is simply no blinker timer to update yet.
        If lightBlinkTimer Is Nothing OrElse Me.IsDisposed OrElse Me.Disposing Then Return
        If ShowIllumination Then
            lightBlinkTimer.Stop()
            Return
        End If

        Dim shortestInterval As Integer = Integer.MaxValue
        If Backglass.currentBulbs IsNot Nothing Then
            For Each bulb As Illumination.BulbInfo In Backglass.currentBulbs
                If bulb IsNot Nothing AndAlso bulb.BlinkEnabled AndAlso Not bulb.IsImageSnippit AndAlso bulb.IlluMode <> Illumination.eIlluMode.Flasher Then
                    shortestInterval = Math.Min(shortestInterval, Math.Max(1, Math.Min(60000, bulb.BlinkInterval)))
                End If
            Next
        End If
        If shortestInterval = Integer.MaxValue Then
            lightBlinkTimer.Stop()
        Else
            lightBlinkTimer.Interval = Math.Max(1, Math.Min(100, shortestInterval))
            If Not lightBlinkTimer.Enabled Then lightBlinkTimer.Start()
        End If
    End Sub

    Private helper As HelperBase = New HelperBase()

    Public Event MyMouseDown(ByVal sender As Object, ByVal e As System.Windows.Forms.MouseEventArgs)
    Public Event MyMouseUp(ByVal sender As Object, ByVal e As System.Windows.Forms.MouseEventArgs)
    Public Event MyMouseMove(ByVal sender As Object, ByVal e As Mouse.MouseMoveEventArgs)
    Public Event CopyDMDCopyArea(ByVal sender As Object, ByVal e As System.EventArgs)
    Public Event RemoveDMDCopyArea(ByVal sender As Object, ByVal e As System.EventArgs)
    Public Event SelectedItemClicked(ByVal sender As Object, ByVal e As Mouse.MouseMoveEventArgs)
    Public Event SelectedItemMoving(ByVal sender As Object, ByVal e As Mouse.MouseMoveEventArgs)
    Public Event SelectedBulbMoved(ByVal sender As Object, ByVal e As System.Windows.Forms.MouseEventArgs)
    Public Event SelectedBulbEdited(ByVal sender As Object, ByVal e As System.EventArgs)
    Public Event SelectedItemRemoved(ByVal sender As Object, ByVal e As System.EventArgs)
    Public Event LightsReportProgress(ByVal sender As Object, ByVal e As Illumination.Lights.LightsProgressEventArgs)

    Public WithEvents Mouse As Mouse = Nothing
    Public WithEvents Lights As Illumination.Lights = Nothing

    Private currentMouseLocation As Point = Nothing

    ' Enhanced 3.0.4 Stage 3: retain the completed native-resolution visual
    ' composite. Selection frames, hover markers and other editor-only paints
    ' can now reuse the exact same bitmap instead of re-running every mask and
    ' alpha blend. The lightweight signature changes whenever a visual input
    ' changes, including position, size, Z-order, opacity, masks or artwork.
    Private unifiedCompositeCache As Bitmap = Nothing
    Private unifiedCompositeSignature As Long = Long.MinValue

    ' Enhanced 3.0.7 Stage 6: while an object is being dragged, keep a frozen
    ' full-quality composite that excludes the moving bulbs/snippets. Mouse
    ' paints then draw only lightweight previews at their current positions.
    Private dragPreviewActive As Boolean = False
    Private dragPreviewBase As Bitmap = Nothing
    Private ReadOnly dragPreviewBulbs As New Generic.List(Of Illumination.BulbInfo)()

    Private Function IsDragPreviewBulb(ByVal bulb As Illumination.BulbInfo) As Boolean
        For Each previewBulb As Illumination.BulbInfo In dragPreviewBulbs
            If Object.ReferenceEquals(previewBulb, bulb) Then Return True
        Next
        Return False
    End Function

    Private Function PictureAnimationPrefix(ByVal bulb As Illumination.BulbInfo) As String
        If bulb Is Nothing OrElse Not bulb.IsImageSnippit OrElse String.IsNullOrEmpty(bulb.Name) OrElse
           Not bulb.Name.StartsWith("PA_", StringComparison.OrdinalIgnoreCase) Then Return String.Empty
        Dim separator As Integer = bulb.Name.LastIndexOf("_"c)
        If separator <= 2 OrElse separator >= bulb.Name.Length - 1 Then Return String.Empty
        Dim frameNumber As Integer
        If Not Integer.TryParse(bulb.Name.Substring(separator + 1), frameNumber) Then Return String.Empty
        Return bulb.Name.Substring(0, separator + 1)
    End Function

    ' The normal editor uses one frame as a lightweight visual reference for a
    ' grouped picture animation. Actual animation preview has a separate
    ' step-driven renderer and therefore continues to display every frame at
    ' its proper time.
    Private Function IsPictureAnimationReferenceFrame(ByVal bulb As Illumination.BulbInfo) As Boolean
        Dim prefix As String = PictureAnimationPrefix(bulb)
        If prefix.Length = 0 OrElse Backglass.currentBulbs Is Nothing Then Return True

        Dim separator As Integer = bulb.Name.LastIndexOf("_"c)
        Dim currentFrame As Integer
        Integer.TryParse(bulb.Name.Substring(separator + 1), currentFrame)
        Dim lowestFrame As Integer = currentFrame
        For Each candidate As Illumination.BulbInfo In Backglass.currentBulbs
            If candidate Is Nothing OrElse Not candidate.IsImageSnippit OrElse String.IsNullOrEmpty(candidate.Name) OrElse
               Not candidate.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then Continue For
            Dim candidateSeparator As Integer = candidate.Name.LastIndexOf("_"c)
            Dim candidateFrame As Integer
            If candidateSeparator > 2 AndAlso Integer.TryParse(candidate.Name.Substring(candidateSeparator + 1), candidateFrame) Then
                lowestFrame = Math.Min(lowestFrame, candidateFrame)
            End If
        Next
        Return currentFrame = lowestFrame
    End Function

    Public Sub BeginDragPreview(ByVal selectedItems As Generic.IEnumerable(Of InfoBase))
        EndDragPreview(False)
        If selectedItems Is Nothing OrElse Me.Image Is Nothing Then Return
        For Each selected As InfoBase In selectedItems
            If TypeOf selected Is Illumination.BulbInfo Then
                dragPreviewBulbs.Add(DirectCast(selected, Illumination.BulbInfo))
            End If
        Next
        If dragPreviewBulbs.Count = 0 Then Return

        Dim nativeWidth As Integer = Me.Image.Width
        Dim nativeHeight As Integer = Me.Image.Height
        If nativeWidth <= 0 OrElse nativeHeight <= 0 Then Return
        Dim previewBase As New Bitmap(nativeWidth, nativeHeight, PixelFormat.Format32bppArgb)
        Try
            Using layerGraphics As Graphics = Graphics.FromImage(previewBase)
                layerGraphics.Clear(EditorCanvasUnderlayColor)
                layerGraphics.SmoothingMode = Drawing2D.SmoothingMode.HighQuality
                layerGraphics.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
                layerGraphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality

                DrawMainBackglassLayer(layerGraphics, New Rectangle(0, 0, nativeWidth, nativeHeight))
                Dim ordered As Generic.IList(Of Object) = OrderedVisualItems()
                For Each item As Object In ordered
                    If TypeOf item Is Illumination.BulbInfo Then
                        Dim bulb As Illumination.BulbInfo = DirectCast(item, Illumination.BulbInfo)
                        If Not IsDragPreviewBulb(bulb) Then DrawUnifiedBulbOrSnippetNative(layerGraphics, previewBase, bulb)
                    ElseIf TypeOf item Is ReelAndLED.ScoreInfo Then
                        DrawScoreLayerNative(layerGraphics, DirectCast(item, ReelAndLED.ScoreInfo))
                    End If
                Next
            End Using
            dragPreviewBase = previewBase
            previewBase = Nothing
            dragPreviewActive = True
        Finally
            If previewBase IsNot Nothing Then previewBase.Dispose()
        End Try
    End Sub

    Public Sub EndDragPreview(Optional ByVal invalidateCanvas As Boolean = True)
        dragPreviewActive = False
        dragPreviewBulbs.Clear()
        If dragPreviewBase IsNot Nothing Then
            dragPreviewBase.Dispose()
            dragPreviewBase = Nothing
        End If
        If invalidateCanvas Then MyBase.Invalidate()
    End Sub

    Public Sub RefreshAfterVisualCollectionChanged()
        EndDragPreview(False)
        ClearUnifiedCompositeCache()
        cachedVisualOrder.Clear()
        cachedCollectionOrder.Clear()
        cachedZOrders.Clear()
        Lights.ClearImages()

        If ShowIllumination Then
            ' Rebuild the bitmap held by the base PictureBox as well as the
            ' unified compositor. Invalidation alone can redraw that old bitmap.
            ShowIllumination = False
            ShowIllumination = True
        Else
            MyBase.Invalidate()
        End If
        MyBase.Update()
    End Sub

    Private Sub DrawLightweightDragPreviews(ByVal graphics As Graphics)
        If Not dragPreviewActive OrElse dragPreviewBulbs.Count = 0 OrElse Mouse Is Nothing Then Return
        Dim factor As Double = Mouse.factor
        For Each bulb As Illumination.BulbInfo In dragPreviewBulbs
            If bulb Is Nothing OrElse Not LayerManager.IsVisible(bulb) Then Continue For
            If Not IsPictureAnimationReferenceFrame(bulb) Then Continue For
            Dim target As New Rectangle(CInt(bulb.Location.X * factor), CInt(bulb.Location.Y * factor),
                                        Math.Max(1, CInt(bulb.Size.Width * factor)), Math.Max(1, CInt(bulb.Size.Height * factor)))
            Dim cachedLight As Illumination.Lights.ImageInfo = Nothing
            If Not bulb.IsImageSnippit AndAlso Lights IsNot Nothing AndAlso Lights.Images.ContainsKey(bulb.ID) Then
                cachedLight = Lights.Images(bulb.ID)
                If cachedLight IsNot Nothing AndAlso cachedLight.Image IsNot Nothing Then
                    target = New Rectangle(CInt(cachedLight.Rectangle.X * factor), CInt(cachedLight.Rectangle.Y * factor),
                                           Math.Max(1, CInt(cachedLight.Rectangle.Width * factor)),
                                           Math.Max(1, CInt(cachedLight.Rectangle.Height * factor)))
                End If
            End If
            Dim rotationState As Drawing2D.GraphicsState = Nothing
            If Mouse.IsPictureAnimationRotationActive Then
                rotationState = graphics.Save()
                Dim centerX As Single = CSng((bulb.Location.X + bulb.Size.Width / 2.0R) * factor)
                Dim centerY As Single = CSng((bulb.Location.Y + bulb.Size.Height / 2.0R) * factor)
                graphics.TranslateTransform(centerX, centerY)
                graphics.RotateTransform(Mouse.PendingPictureAnimationAngle)
                graphics.TranslateTransform(-centerX, -centerY)
            ElseIf Mouse.IsLightRotationActive AndAlso Object.ReferenceEquals(bulb, Mouse.SelectedBulb) Then
                rotationState = graphics.Save()
                Dim centerX As Single = CSng((bulb.Location.X + bulb.Size.Width / 2.0R) * factor)
                Dim centerY As Single = CSng((bulb.Location.Y + bulb.Size.Height / 2.0R) * factor)
                graphics.TranslateTransform(centerX, centerY)
                graphics.RotateTransform(Mouse.PendingLightRotationAngle - bulb.LightRotationAngle)
                graphics.TranslateTransform(-centerX, -centerY)
            ElseIf Not target.IntersectsWith(Me.ClientRectangle) Then
                Continue For
            End If
            Try
                ' A light/flasher may also carry a generated image, but that image
                ' is not a movable snippet. Drawing it directly exposes its opaque
                ' rectangular background during a drag. Only real snippets use the
                ' image branch; lights and flashers use the transparent preview.
                If bulb.IsImageSnippit AndAlso bulb.Image IsNot Nothing Then
                    Dim opacity As Integer = Math.Max(20, LayerManager.GetOpacity(bulb))
                    Using attributes As New ImageAttributes()
                        Dim matrix As New ColorMatrix()
                        Dim brightnessScale As Single = CSng(Math.Max(0, Math.Min(200, bulb.SnippitInfo.Brightness)) / 100.0R)
                        matrix.Matrix00 = brightnessScale
                        matrix.Matrix11 = brightnessScale
                        matrix.Matrix22 = brightnessScale
                        matrix.Matrix33 = CSng(Math.Min(1.0, opacity / 100.0))
                        attributes.SetColorMatrix(matrix)
                        graphics.DrawImage(bulb.Image, target, 0, 0, bulb.Image.Width, bulb.Image.Height, GraphicsUnit.Pixel, attributes)
                    End Using
                ElseIf cachedLight IsNot Nothing AndAlso cachedLight.Image IsNot Nothing Then
                    graphics.DrawImage(cachedLight.Image, target)
                Else
                    Using brush As New SolidBrush(Color.FromArgb(110, bulb.LightColor))
                        graphics.FillEllipse(brush, target)
                    End Using
                End If
            Finally
                If rotationState IsNot Nothing Then graphics.Restore(rotationState)
            End Try
        Next
    End Sub

    Private Shared Function MixVisualHash(ByVal hash As Long, ByVal value As Integer) As Long
        ' Keep arithmetic below Int64 overflow even when VB overflow checking is enabled.
        Return ((hash * 16777619L) + CLng(value And &H7FFFFFFF)) Mod 2147483647L
    End Function

    Private Function CurrentVisualSignature(ByVal nativeWidth As Integer, ByVal nativeHeight As Integer) As Long
        Dim hash As Long = 216613626L
        hash = MixVisualHash(hash, nativeWidth)
        hash = MixVisualHash(hash, nativeHeight)
        hash = MixVisualHash(hash, If(Me.Image Is Nothing, 0, Me.Image.GetHashCode()))
        hash = MixVisualHash(hash, If(ShowIllumination, 1, 0))
        hash = MixVisualHash(hash, If(IsExternalIlluminationImageIlluminated, 1, 0))
        hash = MixVisualHash(hash, If(String.IsNullOrEmpty(RomInfoFilter), 0, RomInfoFilter.GetHashCode()))
        If Backglass.currentBulbs IsNot Nothing Then
            hash = MixVisualHash(hash, Backglass.currentBulbs.Count)
            For Each bulb As Illumination.BulbInfo In Backglass.currentBulbs
                If bulb Is Nothing Then Continue For
                hash = MixVisualHash(hash, bulb.ID)
                hash = MixVisualHash(hash, bulb.ZOrder)
                hash = MixVisualHash(hash, bulb.Location.X)
                hash = MixVisualHash(hash, bulb.Location.Y)
                hash = MixVisualHash(hash, bulb.Size.Width)
                hash = MixVisualHash(hash, bulb.Size.Height)
                hash = MixVisualHash(hash, bulb.LocationX.X)
                hash = MixVisualHash(hash, bulb.LocationX.Y)
                hash = MixVisualHash(hash, bulb.SizeX.Width)
                hash = MixVisualHash(hash, bulb.SizeX.Height)
                hash = MixVisualHash(hash, If(bulb.IsImageSnippit, 1, 0))
                hash = MixVisualHash(hash, If(bulb.Image Is Nothing, 0, bulb.Image.GetHashCode()))
                hash = MixVisualHash(hash, If(bulb.SnippitInfo Is Nothing, 100, bulb.SnippitInfo.Brightness))
                hash = MixVisualHash(hash, If(bulb.SnippitInfo IsNot Nothing AndAlso bulb.SnippitInfo.BehindCanvas, 1, 0))
                hash = MixVisualHash(hash, bulb.Intensity)
                hash = MixVisualHash(hash, If(bulb.BlinkEnabled, 1, 0))
                hash = MixVisualHash(hash, bulb.BlinkInterval)
                If bulb.BlinkEnabled Then hash = MixVisualHash(hash, If(IsBlinkPhaseOn(bulb), 1, 0))
                hash = MixVisualHash(hash, bulb.LightColor.ToArgb())
                hash = MixVisualHash(hash, bulb.DodgeColor.ToArgb())
                hash = MixVisualHash(hash, bulb.IlluMode)
                hash = MixVisualHash(hash, bulb.GlowSpread)
                hash = MixVisualHash(hash, bulb.GlowSoftness)
                hash = MixVisualHash(hash, bulb.GlowFalloff)
                hash = MixVisualHash(hash, bulb.LightDiffusion)
                hash = MixVisualHash(hash, bulb.LightTemperature)
                hash = MixVisualHash(hash, CInt(Math.Round(bulb.LightRotationAngle * 100.0F)))
                hash = MixVisualHash(hash, bulb.GlowIntensity)
                hash = MixVisualHash(hash, CInt(bulb.LightPurpose))
                hash = MixVisualHash(hash, bulb.FlasherStyle)
                hash = MixVisualHash(hash, bulb.FlasherSaturation)
                hash = MixVisualHash(hash, bulb.FlasherHighlightProtection)
                hash = MixVisualHash(hash, bulb.FlasherDarkAreaLift)
                hash = MixVisualHash(hash, bulb.FlasherHotspotX)
                hash = MixVisualHash(hash, bulb.FlasherHotspotY)
                hash = MixVisualHash(hash, bulb.SelectionFeather)
                hash = MixVisualHash(hash, bulb.ArtworkBrightness)
                hash = MixVisualHash(hash, bulb.ArtworkContrast)
                hash = MixVisualHash(hash, bulb.ArtworkAdjustmentPasses)
                hash = MixVisualHash(hash, bulb.MaskRadius)
                hash = MixVisualHash(hash, If(bulb.MaskSmartRadius, 1, 0))
                hash = MixVisualHash(hash, bulb.MaskSmooth)
                hash = MixVisualHash(hash, bulb.MaskFeather)
                hash = MixVisualHash(hash, bulb.MaskContrast)
                hash = MixVisualHash(hash, bulb.MaskShiftEdge)
                hash = MixVisualHash(hash, bulb.FlasherRadialSpikes)
                hash = MixVisualHash(hash, If(bulb.GlobalMaskLayerExplicit, 1, 0))
                hash = MixVisualHash(hash, If(bulb.InFrontOfGlobalMask, 1, 0))
                hash = MixVisualHash(hash, If(bulb.LightBehindCanvas, 1, 0))
                hash = MixVisualHash(hash, If(String.IsNullOrEmpty(bulb.SelectionMaskData), 0, bulb.SelectionMaskData.GetHashCode()))
                hash = MixVisualHash(hash, If(String.IsNullOrEmpty(bulb.Text), 0, bulb.Text.GetHashCode()))
                hash = MixVisualHash(hash, If(String.IsNullOrEmpty(bulb.FontName), 0, bulb.FontName.GetHashCode()))
                hash = MixVisualHash(hash, CInt(bulb.FontSize * 100.0F))
                hash = MixVisualHash(hash, CInt(bulb.FontStyle))
                hash = MixVisualHash(hash, CInt(bulb.TextAlignment))
                hash = MixVisualHash(hash, LayerManager.GetOpacity(bulb))
                hash = MixVisualHash(hash, If(LayerManager.IsVisible(bulb), 1, 0))
            Next
        End If
        If Backglass.currentScores IsNot Nothing Then
            hash = MixVisualHash(hash, Backglass.currentScores.Count)
            For Each score As ReelAndLED.ScoreInfo In Backglass.currentScores
                If score Is Nothing Then Continue For
                hash = MixVisualHash(hash, score.ID)
                hash = MixVisualHash(hash, score.ZOrder)
                hash = MixVisualHash(hash, If(score.BehindCanvas, 1, 0))
                hash = MixVisualHash(hash, score.Location.X)
                hash = MixVisualHash(hash, score.Location.Y)
                hash = MixVisualHash(hash, score.Size.Width)
                hash = MixVisualHash(hash, score.Size.Height)
                hash = MixVisualHash(hash, CInt(Math.Round(score.RotationAngle * 100.0F)))
                hash = MixVisualHash(hash, CInt(Math.Round(score.PerspectiveDepth * 1000.0F)))
                hash = MixVisualHash(hash, CInt(Math.Round(score.PerspectiveLeftScale * 1000.0F)))
                hash = MixVisualHash(hash, CInt(Math.Round(score.PerspectiveRightScale * 1000.0F)))
                hash = MixVisualHash(hash, score.Digits)
                hash = MixVisualHash(hash, score.Spacing)
                hash = MixVisualHash(hash, If(String.IsNullOrEmpty(score.ReelType), 0, score.ReelType.GetHashCode()))
                hash = MixVisualHash(hash, score.ReelColor.ToArgb())
                hash = MixVisualHash(hash, CInt(score.ReelIlluLocation))
                hash = MixVisualHash(hash, score.ReelIlluIntensity)
                hash = MixVisualHash(hash, If(score.Reel3DEnabled, 1, 0))
                hash = MixVisualHash(hash, score.Reel3DBrightness)
                hash = MixVisualHash(hash, score.Reel3DTemperature)
                hash = MixVisualHash(hash, score.Reel3DDepth)
                hash = MixVisualHash(hash, score.Reel3DGlass)
                hash = MixVisualHash(hash, CInt(score.DisplayState))
                hash = MixVisualHash(hash, If(LayerManager.IsVisible(score), 1, 0))
            Next
        End If
        Return hash
    End Function

    Private Sub ClearUnifiedCompositeCache()
        If unifiedCompositeCache IsNot Nothing Then
            unifiedCompositeCache.Dispose()
            unifiedCompositeCache = Nothing
        End If
        unifiedCompositeSignature = Long.MinValue
    End Sub

    ' Enhanced 3.0.2 Stage 1: the visual Z stack changes far less often than
    ' objects move. Keep the sorted references until collection membership or
    ' a Z value actually changes. Location/size changes do not rebuild this list.
    Private ReadOnly cachedVisualOrder As New Generic.List(Of Illumination.BulbInfo)()
    Private ReadOnly cachedCollectionOrder As New Generic.List(Of Illumination.BulbInfo)()
    Private ReadOnly cachedZOrders As New Generic.List(Of Integer)()

    Private Function VisualOrderChanged() As Boolean
        If Backglass.currentBulbs Is Nothing Then Return cachedCollectionOrder.Count <> 0
        If cachedCollectionOrder.Count <> Backglass.currentBulbs.Count Then Return True
        For i As Integer = 0 To Backglass.currentBulbs.Count - 1
            If Not Object.ReferenceEquals(cachedCollectionOrder(i), Backglass.currentBulbs(i)) Then Return True
            If cachedZOrders(i) <> Backglass.currentBulbs(i).ZOrder Then Return True
        Next
        Return False
    End Function

    Private Function OrderedVisualBulbs() As Generic.IList(Of Illumination.BulbInfo)
        If VisualOrderChanged() Then
            cachedVisualOrder.Clear()
            cachedCollectionOrder.Clear()
            cachedZOrders.Clear()
            If Backglass.currentBulbs IsNot Nothing Then
                For Each bulb As Illumination.BulbInfo In Backglass.currentBulbs
                    cachedVisualOrder.Add(bulb)
                    cachedCollectionOrder.Add(bulb)
                    cachedZOrders.Add(bulb.ZOrder)
                Next
                cachedVisualOrder.Sort(Function(left As Illumination.BulbInfo, right As Illumination.BulbInfo)
                                           Dim result As Integer = left.ZOrder.CompareTo(right.ZOrder)
                                           If result <> 0 Then Return result
                                           Return Backglass.currentBulbs.IndexOf(right).CompareTo(Backglass.currentBulbs.IndexOf(left))
                                       End Function)
            End If
        End If
        Return cachedVisualOrder
    End Function

    Private Function OrderedVisualItems() As Generic.IList(Of Object)
        Dim items As New Generic.List(Of Object)()
        If Backglass.currentBulbs IsNot Nothing Then
            For Each bulb As Illumination.BulbInfo In Backglass.currentBulbs
                items.Add(bulb)
            Next
        End If
        If Backglass.currentScores IsNot Nothing Then
            For Each score As ReelAndLED.ScoreInfo In Backglass.currentScores
                items.Add(score)
            Next
        End If
        items.Sort(Function(left As Object, right As Object)
                       Dim leftZ As Integer = If(TypeOf left Is Illumination.BulbInfo, DirectCast(left, Illumination.BulbInfo).ZOrder, DirectCast(left, ReelAndLED.ScoreInfo).ZOrder)
                       Dim rightZ As Integer = If(TypeOf right Is Illumination.BulbInfo, DirectCast(right, Illumination.BulbInfo).ZOrder, DirectCast(right, ReelAndLED.ScoreInfo).ZOrder)
                       Dim result As Integer = leftZ.CompareTo(rightZ)
                       If result <> 0 Then Return result
                       If TypeOf left Is Illumination.BulbInfo AndAlso TypeOf right Is Illumination.BulbInfo Then
                           Return Backglass.currentBulbs.IndexOf(DirectCast(right, Illumination.BulbInfo)).CompareTo(Backglass.currentBulbs.IndexOf(DirectCast(left, Illumination.BulbInfo)))
                       End If
                       If TypeOf left Is ReelAndLED.ScoreInfo AndAlso TypeOf right Is ReelAndLED.ScoreInfo Then
                           Return Backglass.currentScores.IndexOf(DirectCast(right, ReelAndLED.ScoreInfo)).CompareTo(Backglass.currentScores.IndexOf(DirectCast(left, ReelAndLED.ScoreInfo)))
                       End If
                       Return If(TypeOf left Is Illumination.BulbInfo, -1, 1)
                   End Function)
        Return items
    End Function

    ' Return the complete editor footprint of a bulb.  A normal light/flasher is
    ' rendered beyond its blue selection box when GlowSpread is non-zero, so a
    ' drag repaint based only on Location/Size leaves the old outer glow behind.
    Friend Function BulbEditorBounds(ByVal bulb As Illumination.BulbInfo,
                                     ByVal displayScale As Single) As Rectangle
        If bulb Is Nothing Then Return Rectangle.Empty

        Dim safeScale As Double = Math.Max(0.0001R, CDbl(displayScale))
        Dim selectionLeft As Integer = CInt(Math.Floor(bulb.Location.X * safeScale))
        Dim selectionTop As Integer = CInt(Math.Floor(bulb.Location.Y * safeScale))
        Dim selectionRight As Integer = CInt(Math.Ceiling((bulb.Location.X + Math.Max(1, bulb.Size.Width)) * safeScale))
        Dim selectionBottom As Integer = CInt(Math.Ceiling((bulb.Location.Y + Math.Max(1, bulb.Size.Height)) * safeScale))
        Dim bounds As New Rectangle(selectionLeft,
                                    selectionTop,
                                    Math.Max(1, selectionRight - selectionLeft),
                                    Math.Max(1, selectionBottom - selectionTop))

        ' Snippets are drawn inside their selection rectangle.  Only actual
        ' lights and flashers use the expanded illumination destination.
        If bulb.IsImageSnippit Then Return bounds

        Dim nativeRect As New Rectangle(bulb.LocationX, bulb.SizeX)
        If nativeRect.Width <= 0 OrElse nativeRect.Height <= 0 Then Return bounds
        If bulb.GlowSpread > 0 AndAlso bulb.Size.Width > 0 AndAlso bulb.Size.Height > 0 Then
            ' Mirror DrawBulbLayerOnCurrentComposite exactly, including its
            ' edge-clipping scale, so the dirty area matches the real bitmap.
            Dim glowScaleX As Double = nativeRect.Width / CDbl(bulb.Size.Width)
            Dim glowScaleY As Double = nativeRect.Height / CDbl(bulb.Size.Height)
            nativeRect.Inflate(CInt(Math.Round(bulb.GlowSpread * glowScaleX)),
                               CInt(Math.Round(bulb.GlowSpread * glowScaleY)))
        End If
        nativeRect = Illumination.Create.RotatedLightBounds(nativeRect, bulb.LightRotationAngle)
        If Me.Image IsNot Nothing Then
            nativeRect.Intersect(New Rectangle(0, 0, Me.Image.Width, Me.Image.Height))
        End If
        If nativeRect.Width <= 0 OrElse nativeRect.Height <= 0 Then Return bounds

        Dim renderedLeft As Integer = CInt(Math.Floor(nativeRect.Left * safeScale))
        Dim renderedTop As Integer = CInt(Math.Floor(nativeRect.Top * safeScale))
        Dim renderedRight As Integer = CInt(Math.Ceiling(nativeRect.Right * safeScale))
        Dim renderedBottom As Integer = CInt(Math.Ceiling(nativeRect.Bottom * safeScale))
        Dim renderedBounds As New Rectangle(renderedLeft,
                                            renderedTop,
                                            Math.Max(1, renderedRight - renderedLeft),
                                            Math.Max(1, renderedBottom - renderedTop))
        Return Rectangle.Union(bounds, renderedBounds)
    End Function

    ' Repaint only the union of the object's previous and current screen bounds.
    ' Update keeps dragging responsive without forcing a full-control Refresh.
    Private pendingDragDirty As Rectangle = Rectangle.Empty
    Private lastDragPaintTick As Integer = Environment.TickCount
    Private Const DragPaintIntervalMs As Integer = 15

    Public Sub RefreshDragRegion(ByVal previousBounds As Rectangle, ByVal currentBounds As Rectangle)
        Dim dirty As Rectangle = Rectangle.Union(previousBounds, currentBounds)
        If dirty.IsEmpty Then Return
        dirty.Inflate(24, 24)
        dirty.Intersect(Me.ClientRectangle)
        If dirty.IsEmpty Then Return

        pendingDragDirty = If(pendingDragDirty.IsEmpty, dirty, Rectangle.Union(pendingDragDirty, dirty))
        MyBase.Invalidate(dirty)

        ' Enhanced 3.0.3 Stage 2: mouse messages can arrive much faster than the
        ' display can paint. Force at most one synchronous update per frame and
        ' let Windows merge all intervening invalid rectangles.
        Dim nowTick As Integer = Environment.TickCount
        If CUInt(nowTick - lastDragPaintTick) >= DragPaintIntervalMs Then
            lastDragPaintTick = nowTick
            pendingDragDirty = Rectangle.Empty
            Me.Update()
        End If
    End Sub

    Public Sub FlushDragPainting()
        If Not pendingDragDirty.IsEmpty Then
            MyBase.Invalidate(pendingDragDirty)
            pendingDragDirty = Rectangle.Empty
        End If
        lastDragPaintTick = Environment.TickCount
        Me.Update()
    End Sub

    ' Enhanced 2.3.2: selection state for the actual background/DMD canvas image.
    Public Property IsCanvasImageSelected As Boolean = False

    Private IsExternalIlluminationImageFramed As Boolean = False
    Private IsExternalIlluminationImageIlluminated As Boolean = False

    Protected Overrides Sub OnPaint(pe As System.Windows.Forms.PaintEventArgs)

        MyBase.OnPaint(pe)
        UpdateLightBlinkTimer()

        ' Enhanced 2.3.2: show a clear selection frame around the actual canvas image.
        If IsCanvasImageSelected AndAlso Me.Image IsNot Nothing Then
            Using selectionPen As New Pen(Color.DodgerBlue, 2.0F)
                selectionPen.DashStyle = Drawing2D.DashStyle.Dash
                pe.Graphics.DrawRectangle(selectionPen, 1, 1, Math.Max(0, Me.ClientSize.Width - 3), Math.Max(0, Me.ClientSize.Height - 3))
            End Using
        End If

        ' Draw snippets and bulbs through one shared Z-order pipeline.
        ' Lower Z values are painted first; higher Z values are painted last.
        ' For objects sharing a Z layer, the bulb collection order is the
        ' front-to-back tie-break order used by the Layer Manager.
        pe.Graphics.SmoothingMode = Drawing2D.SmoothingMode.HighQuality

        If currentAnimationSteps IsNot Nothing Then
            pe.Graphics.Clear(EditorCanvasUnderlayColor)
            DrawMainBackglassLayer(pe.Graphics)
            DrawAnimationLights(pe.Graphics)
        Else
            DrawUnifiedVisualLayers(pe.Graphics)
        End If

        ' show dmd cut image frame
        If CopyDMDImageFromBackglass AndAlso Mouse IsNot Nothing Then
            Dim framecolor As Color = Color.Yellow
            Dim pen As Pen = New Pen(framecolor)
            Dim pendashed As Pen = New Pen(Brushes.Black)
            pendashed.DashPattern = New Single() {2.0F, 20.0F}
            Dim factor As Double = Mouse.factor
            If Backglass.currentData.DMDCopyArea.Location <> Nothing AndAlso Backglass.currentData.DMDCopyArea.Size <> Nothing Then
                ' draw marker frame
                Dim rect As Rectangle = New Rectangle(New Point(CInt(Backglass.currentData.DMDCopyArea.Location.X * factor), CInt(Backglass.currentData.DMDCopyArea.Location.Y * factor)),
                                                      New Size(CInt(Backglass.currentData.DMDCopyArea.Size.Width * factor), CInt(Backglass.currentData.DMDCopyArea.Size.Height * factor)))
                pe.Graphics.DrawRectangle(pen, rect)
                pe.Graphics.DrawRectangle(pendashed, rect)
                ' draw camera image
                Dim size As Size = New Size(16 * factor, 16 * factor)
                Dim loc As Point = New Point((Backglass.currentData.DMDCopyArea.Location.X + Backglass.currentData.DMDCopyArea.Size.Width - 3) * factor - size.Width,
                                             (Backglass.currentData.DMDCopyArea.Location.Y + 1) * factor)
                pe.Graphics.DrawImage(My.Resources.Camera32x32, New Rectangle(loc, size))
            End If
            pendashed.Dispose()
            pen.Dispose()
        End If

        ' show illumination frames
        If ShowIlluFrames AndAlso Mouse IsNot Nothing AndAlso Backglass.currentBulbs IsNot Nothing AndAlso Backglass.currentBulbs.Count > 0 Then
            Dim framecolor As Color = Color.White
            Dim pen As Pen = New Pen(framecolor)
            Dim pendashed As Pen = New Pen(Brushes.Black)
            pendashed.DashPattern = New Single() {2.0F, 20.0F}
            Dim factor As Double = Mouse.factor
            Dim bulbloop As Integer = 1
            Do While True
                For index As Integer = Backglass.currentBulbs.Count - 1 To 0 Step -1
                    If Not LayerManager.IsVisible(Backglass.currentBulbs(index)) Then Continue For
                    If Not IsPictureAnimationReferenceFrame(Backglass.currentBulbs(index)) Then Continue For
                    With Backglass.currentBulbs(index)
                        If String.IsNullOrEmpty(RomInfoFilter) OrElse
                            RomInfoFilter.Equals(.B2SInfo2String) OrElse
                            RomInfoFilter.Equals(.RomInfo2String) OrElse
                            (RomInfoFilter.Equals("withoutid") AndAlso ((Backglass.currentData.CommType = eCommType.B2S AndAlso String.IsNullOrEmpty(.B2SInfo2String)) OrElse (Backglass.currentData.CommType = eCommType.Rom AndAlso String.IsNullOrEmpty(.RomInfo2String)))) OrElse
                            (RomInfoFilter.Equals("withname") AndAlso Not String.IsNullOrEmpty(.Name)) OrElse
                            (RomInfoFilter.Equals("off") AndAlso .InitialState = 0) OrElse
                            (RomInfoFilter.Equals("on") AndAlso .InitialState = 1) OrElse
                            (RomInfoFilter.Equals("alwayson") AndAlso .InitialState = 2) OrElse
                            (RomInfoFilter.Equals("authentic") AndAlso .DualMode <> eDualMode.Fantasy) OrElse
                            (RomInfoFilter.Equals("fantasy") AndAlso .DualMode <> eDualMode.Authentic) Then
                            ' maybe draw image
                            If bulbloop = 1 Then
                                ' The actual snippet image is painted by DrawUnifiedVisualLayers,
                                ' together with normal bulbs in the shared Z-order pipeline.
                            Else
                                ' draw marker frame. Lights use yellow and flashers use blue so
                                ' their type can be recognized immediately without reading the label.
                                Dim isSnippet As Boolean = .IsImageSnippit
                                Dim isFlasher As Boolean = (Not isSnippet AndAlso
                                                            (.IlluMode = Illumination.eIlluMode.Flasher OrElse
                                                             .LightPurpose = Illumination.eLightPurpose.Flasher))
                                Dim isBlinker As Boolean = (Not isSnippet AndAlso Not isFlasher AndAlso .BlinkEnabled)
                                Dim objectFrameColor As Color = If(isSnippet, Color.Red, If(isFlasher, Color.RoyalBlue, If(isBlinker, Color.DarkOrange, Color.Gold)))
                                pen.Color = objectFrameColor
                                pendashed.Color = objectFrameColor

                                Dim rect As Rectangle = New Rectangle(CInt(.Location.X * factor), CInt(.Location.Y * factor), CInt(.Size.Width * factor), CInt(.Size.Height * factor))
                                Dim frameAngle As Single = 0.0F
                                If Not isSnippet Then
                                    frameAngle = .LightRotationAngle
                                    If .Equals(Mouse.SelectedBulb) AndAlso Mouse.IsLightRotationActive Then
                                        frameAngle = Mouse.PendingLightRotationAngle
                                    End If
                                End If
                                Dim frameBounds As Rectangle = If(Not isSnippet,
                                                                    Illumination.Create.RotatedLightBounds(rect, frameAngle),
                                                                    rect)
                                frameBounds.Inflate(20, 20)
                                If Not frameBounds.IntersectsWith(pe.ClipRectangle) Then Continue For
                                Dim lightFrameState As Drawing2D.GraphicsState = Nothing
                                If Not isSnippet AndAlso Math.Abs(frameAngle) >= 0.001F Then
                                    lightFrameState = pe.Graphics.Save()
                                    Dim frameCenterX As Single = rect.X + rect.Width / 2.0F
                                    Dim frameCenterY As Single = rect.Y + rect.Height / 2.0F
                                    pe.Graphics.TranslateTransform(frameCenterX, frameCenterY)
                                    pe.Graphics.RotateTransform(frameAngle)
                                    pe.Graphics.TranslateTransform(-frameCenterX, -frameCenterY)
                                End If
                                pe.Graphics.DrawRectangle(pen, rect)
                                Dim x As Integer = CInt(rect.X + rect.Width / 2)
                                Dim y As Integer = CInt(rect.Y + rect.Height / 2)
                                If Mouse.IsSelected(DirectCast(Backglass.currentBulbs(index), InfoBase)) Then
                                    pe.Graphics.DrawRectangle(pen, rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2)
                                    pe.Graphics.DrawRectangle(pen, rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4)
                                    pe.Graphics.DrawLine(pen, x - 3, y, x + 3, y)
                                    pe.Graphics.DrawLine(pen, x, y - 3, x, y + 3)
                                Else
                                    pe.Graphics.DrawRectangle(pendashed, rect)
                                End If
                                ' maybe draw preview frame
                                If .Equals(Mouse.PreviewedBulb) Then
                                    pe.Graphics.DrawRectangle(pen, rect)
                                    pe.Graphics.DrawRectangle(pen, rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2)
                                End If
                                ' Compact type badge: the frame color already communicates the
                                ' object family, so one bold letter is clearer on small windows.
                                Dim typeText As String = If(isSnippet, "S", If(isFlasher, "F", If(isBlinker, "B", "L")))
                                Dim typeBackColor As Color = objectFrameColor
                                Dim typeForeColor As Color = If(isSnippet OrElse isFlasher OrElse isBlinker, Color.White, Color.Black)
                                Using typeFont As New Font("Tahoma", 7.0F, FontStyle.Bold)
                                    Dim badgeSide As Integer = Math.Min(15, Math.Min(rect.Width - 6, rect.Height - 6))
                                    Dim typeRect As New Rectangle(rect.X + 3, rect.Y + 3, badgeSide, badgeSide)
                                    If badgeSide >= 9 Then
                                        Using typeBrush As New SolidBrush(typeBackColor)
                                            pe.Graphics.FillRectangle(typeBrush, typeRect)
                                        End Using
                                        TextRenderer.DrawText(pe.Graphics, typeText, typeFont, typeRect, typeForeColor,
                                                              TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or
                                                              TextFormatFlags.NoPadding Or TextFormatFlags.NoPrefix)
                                    End If
                                End Using

                                ' draw remove X
                                Dim isSmallRect As Boolean = (rect.Width * factor < 25 OrElse rect.Height * factor < 25)
                                If .Equals(Mouse.SelectedBulb) Then
                                    If Not isSmallRect Then
                                        pe.Graphics.FillRectangle(Brushes.White, New Rectangle(rect.X + rect.Width - If(isSmallRect, 11, 15), rect.Y + 5, If(isSmallRect, 6, 10), If(isSmallRect, 6, 10)))
                                        pe.Graphics.DrawLine(Pens.Black, rect.X + rect.Width - If(isSmallRect, 9, 13), rect.Y + 7, rect.X + rect.Width - 7, rect.Y + If(isSmallRect, 9, 13))
                                        pe.Graphics.DrawLine(Pens.Black, rect.X + rect.Width - 7, rect.Y + 7, rect.X + rect.Width - If(isSmallRect, 9, 13), rect.Y + If(isSmallRect, 9, 13))
                                    End If
                                End If
                                ' maybe draw bulb name
                                Dim font As Font = New Font("Tahoma", 7, If(Mouse.IsSelected(DirectCast(Backglass.currentBulbs(index), InfoBase)), FontStyle.Bold, FontStyle.Regular))
                                If Not String.IsNullOrEmpty(.Name) Then
                                    TextRenderer.DrawText(pe.Graphics, .Name, font, New Point(rect.X + 3, rect.Y + 20), framecolor, TextFormatFlags.Left Or TextFormatFlags.NoPrefix)
                                End If
                                ' maybe draw lamp state text
                                If .InitialState >= 0 Then
                                    TextRenderer.DrawText(pe.Graphics, Choose(.InitialState + 1, "Off", "On", "Always on", "At startup on") & If(Backglass.currentData.DualBackglass, "/" + Choose(.DualMode + 1, "B", "A", "F"), ""), font, New Rectangle(rect.X + 3, rect.Y + rect.Height - 14, 80, 15), framecolor, TextFormatFlags.VerticalCenter Or TextFormatFlags.Left Or TextFormatFlags.NoPrefix)
                                End If
                                ' maybe draw lamp or solenoid info text
                                If .B2SID > 0 Then
                                    Dim toleft As Integer = If(.Equals(Mouse.SelectedBulb), If(isSmallRect, -1, 13), 0)
                                    TextRenderer.DrawText(pe.Graphics, .B2SID.ToString() & If(.B2SValue > 0, "/" & .B2SValue.ToString(), ""), font, New Rectangle(rect.X + rect.Width - 50, rect.Y + 3, 47 - toleft, 15), framecolor, TextFormatFlags.VerticalCenter Or TextFormatFlags.Right Or TextFormatFlags.NoPrefix)
                                End If
                                If .RomID > 0 AndAlso .RomIDType > eRomIDType.NotUsed Then
                                    Dim text As String = If(.RomInverted, "I", String.Empty) & Choose(.RomIDType, "L", "S", "GI") & .RomID.ToString()
                                    '    Dim toleft As Integer = If(Mouse.IsSelected(DirectCast(Backglass.currentBulbs(index), InfoBase)), If((rect.Width < 30 OrElse rect.Height < 30), 9, 13), 0)
                                    TextRenderer.DrawText(pe.Graphics, text, font, New Rectangle(rect.X + rect.Width - 50, rect.Y + rect.Height - 14, 47, 15), framecolor, TextFormatFlags.VerticalCenter Or TextFormatFlags.Right Or TextFormatFlags.NoPrefix)
                                End If
                                font.Dispose()
                                ' maybe render text
                                If Not ShowIllumination AndAlso Not String.IsNullOrEmpty(.Text) Then
                                    font = New Font(.FontName, CSng(.FontSize * factor), .FontStyle)
                                    Dim horizontal As TextFormatFlags = If(.TextAlignment = Illumination.eTextAlignment.Left, TextFormatFlags.Left, If(.TextAlignment = Illumination.eTextAlignment.Right, TextFormatFlags.Right, TextFormatFlags.HorizontalCenter))
                                    TextRenderer.DrawText(pe.Graphics, .Text, font, rect, framecolor, TextFormatFlags.VerticalCenter Or horizontal Or TextFormatFlags.NoPrefix)
                                    font.Dispose()
                                End If
                                If lightFrameState IsNot Nothing Then pe.Graphics.Restore(lightFrameState)
                            End If
                        End If
                    End With
                Next
                bulbloop += 1
                If bulbloop > 2 Then Exit Do
            Loop
            Dim selectedLight As Illumination.BulbInfo = Mouse.SelectedBulb
            Dim showRotationHandle As Boolean = selectedLight IsNot Nothing AndAlso
                ((selectedLight.IsImageSnippit AndAlso
                  (Mouse.SelectedItems.Count = 1 OrElse
                   (selectedLight.Name.StartsWith("PA_", StringComparison.OrdinalIgnoreCase) AndAlso Mouse.SelectedItems.Count > 1))) OrElse
                 (Not selectedLight.IsImageSnippit AndAlso Mouse.SelectedItems.Count = 1))
            If showRotationHandle Then
                Dim selectedRect As New Rectangle(CInt(selectedLight.Location.X * factor),
                                                  CInt(selectedLight.Location.Y * factor),
                                                  CInt(selectedLight.Size.Width * factor),
                                                  CInt(selectedLight.Size.Height * factor))
                Dim centerX As Single = selectedRect.X + selectedRect.Width / 2.0F
                Dim centerY As Single = selectedRect.Y + selectedRect.Height / 2.0F
                Dim handleRadius As Single = selectedRect.Height / 2.0F + 24.0F
                Dim handleAngle As Single = If(selectedLight.IsImageSnippit,
                                               Mouse.PendingPictureAnimationAngle,
                                               If(Mouse.IsLightRotationActive,
                                                  Mouse.PendingLightRotationAngle,
                                                  selectedLight.LightRotationAngle))
                Dim radians As Double = handleAngle * Math.PI / 180.0R
                Dim handleX As Single = centerX + CSng(Math.Sin(radians) * handleRadius)
                Dim handleY As Single = centerY - CSng(Math.Cos(radians) * handleRadius)
                Dim stemX As Single = centerX + CSng(Math.Sin(radians) * selectedRect.Height / 2.0F)
                Dim stemY As Single = centerY - CSng(Math.Cos(radians) * selectedRect.Height / 2.0F)
                Using rotationPen As New Pen(Color.DeepSkyBlue, 2.0F)
                    pe.Graphics.DrawLine(rotationPen, stemX, stemY, handleX, handleY)
                    pe.Graphics.DrawEllipse(rotationPen, handleX - 7, handleY - 7, 14, 14)
                End Using
            End If
            pendashed.Dispose()
            pen.Dispose()
        End If

        ' show score frames
        If (ShowScoreFrames OrElse ShowScoring) AndAlso Mouse IsNot Nothing AndAlso Backglass.currentScores IsNot Nothing AndAlso Backglass.currentScores.Count > 0 Then
            Dim fontbold As Font = New Font("Tahoma", 8, FontStyle.Bold) ' Segoe UI
            Dim font As Font = New Font("Tahoma", 7, FontStyle.Regular)
            Dim framecolor As Color = Color.DarkOrange
            Dim pen As Pen = New Pen(framecolor)
            Dim pendashed As Pen = New Pen(Brushes.White)
            pendashed.DashPattern = New Single() {5.0F, 5.0F}
            'Dim brush As SolidBrush = New SolidBrush(framecolor)
            Dim factor As Double = Mouse.factor
            For index As Integer = Backglass.currentScores.Count - 1 To 0 Step -1
                If Not LayerManager.IsVisible(Backglass.currentScores(index)) Then Continue For
                With Backglass.currentScores(index)
                    Dim rect As Rectangle = New Rectangle(CInt(.Location.X * factor), CInt(.Location.Y * factor), CInt(.Size.Width * factor), CInt(.Size.Height * factor))
                    Dim scoreState As Drawing2D.GraphicsState = pe.Graphics.Save()
                    Dim scoreCenterX As Single = rect.X + rect.Width / 2.0F
                    Dim scoreCenterY As Single = rect.Y + rect.Height / 2.0F
                    pe.Graphics.TranslateTransform(scoreCenterX, scoreCenterY)
                    pe.Graphics.RotateTransform(.RotationAngle)
                    pe.Graphics.TranslateTransform(-scoreCenterX, -scoreCenterY)
                    ' draw images for score reels
                    If False AndAlso .Digits > 0 Then
                        If .IsSingleReelSizeDirty OrElse .SingleReelFactor <> factor Then
                            Dim width As Single = ((rect.Width - .Spacing * factor / 2 * (.Digits - 1)) / .Digits)
                            Do While width * .Digits > rect.Width - 1
                                width -= 1
                            Loop
                            Dim height As Integer = rect.Height - 1
                            If .PerfectScaleWidthFix Then
                                height = CInt(My.Resources.LED_0.Height / My.Resources.LED_0.Width * width)
                                .Size.Height = CInt(height / factor) + 1
                                .PerfectScaleWidthFix = False
                                Me.Invalidate()
                            End If
                            .SingleReelSize = New SizeF(width, height)
                            .IsSingleReelSizeDirty = False
                            .SingleReelFactor = factor
                        End If
                        Dim x As Single = rect.X + 1
                        Dim y As Integer = rect.Y + 1
                        Dim newsize As Size = New Size(.SingleReelSize.Width, .SingleReelSize.Height)
                        Dim image As Image = GetReelImage(.ReelType, .ReelColor, Backglass.currentData.UseDream7LEDs, Backglass.currentData.D7Thickness, Backglass.currentData.D7Shear, Backglass.currentData.D7Glow, newsize).Resized(newsize)
                        If image IsNot Nothing Then
                            For i As Integer = 1 To .Digits
                                pe.Graphics.DrawImage(image, New Point(x, y))
                                x += .SingleReelSize.Width + .Spacing * factor / 2
                            Next
                            image.Dispose()
                        End If
                    End If
                    ' draw marker frame
                    If ShowScoreFrames Then
                        pe.Graphics.DrawRectangle(pen, rect)
                        If Mouse.IsSelected(DirectCast(Backglass.currentScores(index), InfoBase)) Then
                            pe.Graphics.DrawRectangle(pen, rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2)
                            pe.Graphics.DrawRectangle(pen, rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4)
                        Else
                            pe.Graphics.DrawRectangle(pendashed, rect)
                        End If
                    End If
                    ' maybe draw preview frame
                    If .Equals(Mouse.PreviewedScore) Then
                        pe.Graphics.DrawRectangle(pen, rect)
                        pe.Graphics.DrawRectangle(pen, rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2)
                    End If
                    ' draw remove X
                    If ShowScoreFrames AndAlso Mouse.IsSelected(DirectCast(Backglass.currentScores(index), InfoBase)) Then
                        Dim isSmallRect As Boolean = (rect.Width * factor < 25 OrElse rect.Height * factor < 25)
                        If Not isSmallRect Then
                            pe.Graphics.FillRectangle(Brushes.White, New Rectangle(rect.X + rect.Width - 15, rect.Y + 5, 10, 10))
                            pe.Graphics.DrawLine(Pens.Black, rect.X + rect.Width - 13, rect.Y + 7, rect.X + rect.Width - 7, rect.Y + 13)
                            pe.Graphics.DrawLine(Pens.Black, rect.X + rect.Width - 7, rect.Y + 7, rect.X + rect.Width - 13, rect.Y + 13)
                        End If
                    End If
                    ' maybe draw start digit
                    If ShowScoreFrames AndAlso .ReelIlluB2SID > 0 Then
                        TextRenderer.DrawText(pe.Graphics, .ReelIlluB2SID.ToString() & If(.ReelIlluB2SValue > 0, "/" & .ReelIlluB2SValue.ToString(), ""), font, New Rectangle(rect.X + 3, rect.Y + rect.Height - 15, 80, 15), framecolor, Color.White, TextFormatFlags.VerticalCenter Or TextFormatFlags.Left Or TextFormatFlags.NoPrefix)
                    End If
                    ' maybe draw reel illumination info
                    If ShowScoreFrames AndAlso .B2SStartDigit > 0 Then
                        Dim toleft As Integer = If(Mouse.IsSelected(DirectCast(Backglass.currentScores(index), InfoBase)), 13, 0) 'If(Mouse.IsSelected(DirectCast(Backglass.currentBulbs(index), InfoBase)), If(isSmallRect, -1, 13), 0)
                        TextRenderer.DrawText(pe.Graphics, If(.B2SPlayerNo <> eB2SPlayerNo.NotUsed, "P" & CInt(.B2SPlayerNo).ToString() & "/", "") & .B2SStartDigit.ToString(), font, New Rectangle(rect.X + rect.Width - 50, rect.Y + 3, 47 - toleft, 15), framecolor, Color.White, TextFormatFlags.VerticalCenter Or TextFormatFlags.Right Or TextFormatFlags.NoPrefix)
                    End If
                    ' draw player number
                    If ShowScoreFrames Then
                        Dim reelBadgeSide As Integer = Math.Min(15, Math.Min(rect.Width - 6, rect.Height - 6))
                        If reelBadgeSide >= 9 Then
                            Dim reelBadge As New Rectangle(rect.X + 3, rect.Y + 3, reelBadgeSide, reelBadgeSide)
                            Dim reelBadgeColor As Color = Color.FromArgb(72, 226, 143)
                            Using reelBadgeBrush As New SolidBrush(reelBadgeColor)
                                pe.Graphics.FillRectangle(reelBadgeBrush, reelBadge)
                            End Using
                            TextRenderer.DrawText(pe.Graphics, "R", fontbold, reelBadge, Color.Black,
                                                  TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or
                                                  TextFormatFlags.NoPadding Or TextFormatFlags.NoPrefix)
                            TextRenderer.DrawText(pe.Graphics, .ID.ToString(), fontbold, New Point(reelBadge.Right + 3, rect.Y + 3), framecolor, Color.White)
                        Else
                            TextRenderer.DrawText(pe.Graphics, .ID.ToString(), fontbold, New Point(rect.X + 3, rect.Y + 3), framecolor, Color.White)
                        End If
                        'pe.Graphics.DrawString(.ID.ToString(), fontbold, brush, New Point(rect.X + 3, rect.Y + 3))
                    End If
                    pe.Graphics.Restore(scoreState)
                    If ShowScoreFrames AndAlso Mouse.IsSelected(DirectCast(Backglass.currentScores(index), InfoBase)) Then
                        Dim perspectivePoints As PointF() = ScorePerspectivePoints(DirectCast(Backglass.currentScores(index), ReelAndLED.ScoreInfo), CSng(factor))
                        pe.Graphics.DrawPolygon(Pens.DeepSkyBlue, perspectivePoints)

                        ' Independent vertical-perspective handles. Drag either
                        ' upper corner up/down to stretch that end of the display.
                        Dim leftPerspectiveHandle As PointF = perspectivePoints(0)
                        Dim rightPerspectiveHandle As PointF = perspectivePoints(1)
                        pe.Graphics.FillEllipse(Brushes.White, leftPerspectiveHandle.X - 6, leftPerspectiveHandle.Y - 6, 12, 12)
                        pe.Graphics.DrawEllipse(Pens.LimeGreen, leftPerspectiveHandle.X - 6, leftPerspectiveHandle.Y - 6, 12, 12)
                        pe.Graphics.DrawLine(Pens.LimeGreen, leftPerspectiveHandle.X, leftPerspectiveHandle.Y - 3, leftPerspectiveHandle.X, leftPerspectiveHandle.Y + 3)
                        pe.Graphics.FillEllipse(Brushes.White, rightPerspectiveHandle.X - 6, rightPerspectiveHandle.Y - 6, 12, 12)
                        pe.Graphics.DrawEllipse(Pens.DeepSkyBlue, rightPerspectiveHandle.X - 6, rightPerspectiveHandle.Y - 6, 12, 12)
                        pe.Graphics.DrawLine(Pens.DeepSkyBlue, rightPerspectiveHandle.X, rightPerspectiveHandle.Y - 3, rightPerspectiveHandle.X, rightPerspectiveHandle.Y + 3)

                        ' Center depth handle: drag left/right to turn the complete score window in depth.
                        Dim depthCenter As PointF = New PointF(scoreCenterX, scoreCenterY)
                        pe.Graphics.FillEllipse(Brushes.White, depthCenter.X - 7, depthCenter.Y - 7, 14, 14)
                        pe.Graphics.DrawEllipse(Pens.DeepSkyBlue, depthCenter.X - 7, depthCenter.Y - 7, 14, 14)
                        pe.Graphics.DrawLine(Pens.DeepSkyBlue, depthCenter.X - 4, depthCenter.Y, depthCenter.X + 4, depthCenter.Y)

                        Dim radians As Double = .RotationAngle * Math.PI / 180.0
                        Dim localX As Double = 0
                        Dim localY As Double = -(rect.Height / 2.0 + 24.0)
                        Dim handleX As Single = CSng(scoreCenterX + localX * Math.Cos(radians) - localY * Math.Sin(radians))
                        Dim handleY As Single = CSng(scoreCenterY + localX * Math.Sin(radians) + localY * Math.Cos(radians))
                        Dim topMid As PointF = LerpPoint(perspectivePoints(0), perspectivePoints(1), 0.5F)
                        pe.Graphics.DrawLine(pen, topMid.X, topMid.Y, handleX, handleY)
                        pe.Graphics.FillEllipse(Brushes.White, handleX - 6, handleY - 6, 12, 12)
                        pe.Graphics.DrawEllipse(pen, handleX - 6, handleY - 6, 12, 12)
                    End If
                End With
            Next
            'brush.Dispose()
            pendashed.Dispose()
            pen.Dispose()
            font.Dispose()
            fontbold.Dispose()
        End If

        ' maybe show grill height marker
        If (SetGrillHeight OrElse SetSmallGrillHeight) AndAlso Mouse IsNot Nothing AndAlso Backglass.currentData IsNot Nothing AndAlso Not Backglass.currentData.IsDMDImageShown AndAlso currentMouseLocation <> Nothing Then
            Dim font As Font = New Font("Tahoma", 7, FontStyle.Bold)
            If Backglass.currentData.GrillHeight > 0 Then
                pe.Graphics.FillRectangle(Brushes.ForestGreen, New Rectangle(0, (Backglass.currentData.Image.Height - Backglass.currentData.GrillHeight) * Mouse.factor - 2, Me.Width - 1, 3))
                Dim size As Size = TextRenderer.MeasureText(My.Resources.TXT_GrillTop, font)
                Dim y As Integer = (Backglass.currentData.Image.Height - Backglass.currentData.GrillHeight) * Mouse.factor - size.Height - 4
                TextRenderer.DrawText(pe.Graphics, My.Resources.TXT_GrillTop, font, New Point(Me.Width - size.Width - 3 - 15, y), Color.ForestGreen)
                ' draw remove X
                pe.Graphics.FillRectangle(Brushes.ForestGreen, New Rectangle(Me.Width - 15, y, 12, 12))
                pe.Graphics.DrawLine(Pens.White, Me.Width - 13, y + 2, Me.Width - 5, y + 10)
                pe.Graphics.DrawLine(Pens.White, Me.Width - 13, y + 10, Me.Width - 5, y + 2)
            End If
            If Backglass.currentData.SmallGrillHeight > 0 Then
                pe.Graphics.FillRectangle(Brushes.DarkRed, New Rectangle(0, (Backglass.currentData.Image.Height - Backglass.currentData.SmallGrillHeight) * Mouse.factor - 2, Me.Width - 1, 3))
                Dim size As Size = TextRenderer.MeasureText(My.Resources.TXT_MiniGrillTop, font)
                Dim y As Integer = (Backglass.currentData.Image.Height - Backglass.currentData.SmallGrillHeight) * Mouse.factor - size.Height - 4
                TextRenderer.DrawText(pe.Graphics, My.Resources.TXT_MiniGrillTop, font, New Point(Me.Width - size.Width - 3 - 15, y), Color.DarkRed)
                ' draw remove X
                pe.Graphics.FillRectangle(Brushes.DarkRed, New Rectangle(Me.Width - 15, y, 12, 12))
                pe.Graphics.DrawLine(Pens.White, Me.Width - 13, y + 2, Me.Width - 5, y + 10)
                pe.Graphics.DrawLine(Pens.White, Me.Width - 13, y + 10, Me.Width - 5, y + 2)
            End If
            pe.Graphics.FillRectangle(If(SetSmallGrillHeight, Brushes.OrangeRed, Brushes.LightGreen), New Rectangle(0, currentMouseLocation.Y * Mouse.factor - 2, Me.Width - 1, 3))
            font.Dispose()
        End If

        ' maybe show dmd for default location
        If SetDMDDefaultLocation AndAlso Mouse IsNot Nothing AndAlso Backglass.currentData IsNot Nothing AndAlso Not Backglass.currentData.IsDMDImageShown AndAlso currentMouseLocation <> Nothing Then
            If Backglass.currentData.DMDImage IsNot Nothing Then
                Dim dmdsize As Size = New Size(Backglass.currentData.DMDImage.Width * Mouse.factor, Backglass.currentData.DMDImage.Height * Mouse.factor)
                If Backglass.currentData.DMDDefaultLocation <> Nothing Then
                    Dim dmdloc As Point = New Point(Backglass.currentData.DMDDefaultLocation.X * Mouse.factor, Backglass.currentData.DMDDefaultLocation.Y * Mouse.factor)
                    pe.Graphics.FillRectangle(Brushes.ForestGreen, New Rectangle(dmdloc, dmdsize))
                    ' draw remove X
                    pe.Graphics.FillRectangle(Brushes.ForestGreen, New Rectangle(dmdloc.X + dmdsize.Width + 5, dmdloc.Y, 12, 12))
                    pe.Graphics.DrawLine(Pens.White, dmdloc.X + dmdsize.Width + 5 + 2, dmdloc.Y + 2, dmdloc.X + dmdsize.Width + 5 + 10, dmdloc.Y + 10)
                    pe.Graphics.DrawLine(Pens.White, dmdloc.X + dmdsize.Width + 5 + 2, dmdloc.Y + 10, dmdloc.X + dmdsize.Width + 5 + 10, dmdloc.Y + 2)
                End If
                pe.Graphics.FillRectangle(Brushes.DarkRed, New Rectangle(New Point(currentMouseLocation.X * Mouse.factor, currentMouseLocation.Y * Mouse.factor), dmdsize))
            End If
        End If

        'If True Then
        '    Dim pendashed As Pen = New Pen(Brushes.Gray)
        '    pendashed.DashPattern = New Single() {1.0F, 2.0F}
        '    For i As Integer = 1 To 300
        '        Dim y As Single = i * Mouse.factor
        '        pe.Graphics.DrawLine(pendashed, 0, y, Me.Width - 1, y)
        '    Next
        '    For i As Integer = 1 To 300
        '        Dim x As Single = i * Mouse.factor
        '        pe.Graphics.DrawLine(pendashed, x, 0, x, Me.Height - 1)
        '    Next
        '    pendashed.Dispose()
        'End If

    End Sub
    Private Sub DrawAnimationLights(ByVal graphics As Graphics)
        If Lights Is Nothing OrElse Lights.Images Is Nothing OrElse Me.Image Is Nothing Then Return

        Dim x As Single = Me.Image.Width / Math.Max(1, Me.Width)
        Dim y As Single = Me.Image.Height / Math.Max(1, Me.Height)
        For Each light As KeyValuePair(Of Integer, Illumination.Lights.ImageInfo) In Lights.OrderedImages
            With light.Value
                If animationOn.Count > 0 AndAlso Not String.IsNullOrEmpty(.Name) AndAlso animationOn.Contains(.Name.ToLower()) Then
                    Dim rectF As New RectangleF(.Rectangle.X / x, .Rectangle.Y / y, .Rectangle.Width / x, .Rectangle.Height / y)
                    graphics.DrawImage(.Image, rectF)
                End If
            End With
        Next
    End Sub

    Private Sub DrawUnifiedVisualLayers(ByVal graphics As Graphics)
        If dragPreviewActive AndAlso dragPreviewBase IsNot Nothing Then
            graphics.DrawImage(dragPreviewBase, New Rectangle(0, 0, Me.ClientSize.Width, Me.ClientSize.Height))
            DrawLightweightDragPreviews(graphics)
            Return
        End If

        ' Enhanced 3.0.4 Stage 3: cache the finished native-resolution stack.
        ' The expensive CreateOverlayImage/mask pipeline runs only when the
        ' signature changes; ordinary selection and regional repaints reuse it.
        Dim nativeWidth As Integer = If(Me.Image IsNot Nothing, Me.Image.Width, Math.Max(1, Me.ClientSize.Width))
        Dim nativeHeight As Integer = If(Me.Image IsNot Nothing, Me.Image.Height, Math.Max(1, Me.ClientSize.Height))
        If nativeWidth <= 0 OrElse nativeHeight <= 0 Then Return

        Dim signature As Long = CurrentVisualSignature(nativeWidth, nativeHeight)
        Dim rebuild As Boolean = (unifiedCompositeCache Is Nothing OrElse
                                  unifiedCompositeCache.Width <> nativeWidth OrElse
                                  unifiedCompositeCache.Height <> nativeHeight OrElse
                                  unifiedCompositeSignature <> signature)
        If rebuild Then
            ClearUnifiedCompositeCache()
            Dim nextComposite As New Bitmap(nativeWidth, nativeHeight, PixelFormat.Format32bppArgb)
            Try
                Using layerGraphics As Graphics = Graphics.FromImage(nextComposite)
                    layerGraphics.Clear(EditorCanvasUnderlayColor)
                    layerGraphics.SmoothingMode = Drawing2D.SmoothingMode.HighQuality
                    layerGraphics.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
                    layerGraphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality

                    DrawMainBackglassLayer(layerGraphics, New Rectangle(0, 0, nativeWidth, nativeHeight))
                    Dim ordered As Generic.IList(Of Object) = OrderedVisualItems()
                    For Each item As Object In ordered
                        If TypeOf item Is Illumination.BulbInfo Then
                            DrawUnifiedBulbOrSnippetNative(layerGraphics, nextComposite, DirectCast(item, Illumination.BulbInfo))
                        ElseIf TypeOf item Is ReelAndLED.ScoreInfo Then
                            DrawScoreLayerNative(layerGraphics, DirectCast(item, ReelAndLED.ScoreInfo))
                        End If
                    Next
                End Using
                unifiedCompositeCache = nextComposite
                unifiedCompositeSignature = signature
                nextComposite = Nothing
            Finally
                If nextComposite IsNot Nothing Then nextComposite.Dispose()
            End Try
        End If

        If unifiedCompositeCache IsNot Nothing Then
            graphics.DrawImage(unifiedCompositeCache, New Rectangle(0, 0, Me.ClientSize.Width, Me.ClientSize.Height))
        End If
    End Sub

    ' Build the same native-resolution layer stack used by the designer canvas,
    ' without editor adornments.  The Light Settings window uses this so its
    ' live preview includes snippets, scores, layer visibility/opacity and the
    ' real Z order instead of previewing against the bare backglass image.
    Public Function CreateLayeredPreviewImage(Optional ByVal excludedBulb As Illumination.BulbInfo = Nothing,
                                              Optional ByVal forceVisibleBulb As Illumination.BulbInfo = Nothing) As Bitmap
        Dim excluded As Generic.IEnumerable(Of Illumination.BulbInfo) = Nothing
        Dim forced As Generic.IEnumerable(Of Illumination.BulbInfo) = Nothing
        If excludedBulb IsNot Nothing Then excluded = New Illumination.BulbInfo() {excludedBulb}
        If forceVisibleBulb IsNot Nothing Then forced = New Illumination.BulbInfo() {forceVisibleBulb}
        Return CreateLayeredPreviewImageForBulbs(excluded, forced)
    End Function

    ' Group-aware version used by Light Settings.  The off image excludes every
    ' selected lamp and the on image forces every selected lamp through the same
    ' native compositor used by the designer canvas.
    Public Function CreateLayeredPreviewImageForBulbs(ByVal excludedBulbs As Generic.IEnumerable(Of Illumination.BulbInfo),
                                                       ByVal forceVisibleBulbs As Generic.IEnumerable(Of Illumination.BulbInfo)) As Bitmap
        If Me.Image Is Nothing Then Return Nothing

        Dim nativeWidth As Integer = Me.Image.Width
        Dim nativeHeight As Integer = Me.Image.Height
        If nativeWidth <= 0 OrElse nativeHeight <= 0 Then Return Nothing

        Dim excluded As New Generic.HashSet(Of Illumination.BulbInfo)()
        Dim forced As New Generic.HashSet(Of Illumination.BulbInfo)()
        If excludedBulbs IsNot Nothing Then
            For Each item As Illumination.BulbInfo In excludedBulbs
                If item IsNot Nothing Then excluded.Add(item)
            Next
        End If
        If forceVisibleBulbs IsNot Nothing Then
            For Each item As Illumination.BulbInfo In forceVisibleBulbs
                If item IsNot Nothing Then forced.Add(item)
            Next
        End If

        Dim result As New Bitmap(nativeWidth, nativeHeight, PixelFormat.Format32bppArgb)
        Try
            Using layerGraphics As Graphics = Graphics.FromImage(result)
                layerGraphics.Clear(EditorCanvasUnderlayColor)
                layerGraphics.SmoothingMode = Drawing2D.SmoothingMode.HighQuality
                layerGraphics.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
                layerGraphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality

                DrawMainBackglassLayer(layerGraphics, New Rectangle(0, 0, nativeWidth, nativeHeight))
                For Each item As Object In OrderedVisualItems()
                    If TypeOf item Is Illumination.BulbInfo Then
                        Dim itemBulb As Illumination.BulbInfo = DirectCast(item, Illumination.BulbInfo)
                        If excluded.Contains(itemBulb) Then Continue For
                        DrawUnifiedBulbOrSnippetNative(layerGraphics, result, itemBulb, forced.Contains(itemBulb))
                    ElseIf TypeOf item Is ReelAndLED.ScoreInfo Then
                        DrawScoreLayerNative(layerGraphics, DirectCast(item, ReelAndLED.ScoreInfo))
                    End If
                Next
            End Using
            Return result
        Catch
            result.Dispose()
            Return Nothing
        End Try
    End Function

    Friend Function ScorePerspectivePoints(ByVal score As ReelAndLED.ScoreInfo, ByVal scale As Single) As PointF()
        Dim centerX As Single = (score.Location.X + score.Size.Width / 2.0F) * scale
        Dim centerY As Single = (score.Location.Y + score.Size.Height / 2.0F) * scale
        Dim halfWidth As Single = score.Size.Width * scale / 2.0F
        Dim halfHeight As Single = score.Size.Height * scale / 2.0F

        Dim depth As Single = Math.Max(-1.0F, Math.Min(1.0F, score.PerspectiveDepth))
        Dim amount As Single = Math.Abs(depth)
        ' Turning in depth compresses the overall width and makes the far edge shorter.
        Dim projectedHalfWidth As Single = halfWidth * (1.0F - 0.34F * amount)
        Dim nearScale As Single = 1.0F + 0.06F * amount
        Dim farScale As Single = 1.0F - 0.58F * amount
        Dim leftScale As Single = If(depth >= 0.0F, nearScale, farScale) * Math.Max(0.2F, Math.Min(2.5F, score.PerspectiveLeftScale))
        Dim rightScale As Single = If(depth >= 0.0F, farScale, nearScale) * Math.Max(0.2F, Math.Min(2.5F, score.PerspectiveRightScale))

        Dim points() As PointF = {
            New PointF(centerX - projectedHalfWidth, centerY - halfHeight * leftScale),
            New PointF(centerX + projectedHalfWidth, centerY - halfHeight * rightScale),
            New PointF(centerX + projectedHalfWidth, centerY + halfHeight * rightScale),
            New PointF(centerX - projectedHalfWidth, centerY + halfHeight * leftScale)}

        If Math.Abs(score.RotationAngle) > 0.001F Then
            Dim radians As Double = score.RotationAngle * Math.PI / 180.0
            For i As Integer = 0 To points.Length - 1
                Dim dx As Double = points(i).X - centerX
                Dim dy As Double = points(i).Y - centerY
                points(i) = New PointF(CSng(centerX + dx * Math.Cos(radians) - dy * Math.Sin(radians)),
                                       CSng(centerY + dx * Math.Sin(radians) + dy * Math.Cos(radians)))
            Next
        End If
        Return points
    End Function

    ' Complete editor-only footprint for a selected score. Drag invalidation
    ' must include more than the unrotated score rectangle: the perspective
    ' corners, rotation stem and handle all paint outside that rectangle.
    Friend Function ScoreEditorBounds(ByVal score As ReelAndLED.ScoreInfo, ByVal scale As Single) As Rectangle
        If score Is Nothing OrElse scale <= 0.0F Then Return Rectangle.Empty

        Dim centerX As Single = (score.Location.X + score.Size.Width / 2.0F) * scale
        Dim centerY As Single = (score.Location.Y + score.Size.Height / 2.0F) * scale
        Dim halfWidth As Single = score.Size.Width * scale / 2.0F
        Dim halfHeight As Single = score.Size.Height * scale / 2.0F
        Dim radians As Double = score.RotationAngle * Math.PI / 180.0
        Dim visualPoints As New Generic.List(Of PointF)(ScorePerspectivePoints(score, scale))

        ' The standard selection frame is drawn as the original rectangle and
        ' then rotated, so include its four transformed corners as well.
        Dim localCorners() As PointF = {
            New PointF(-halfWidth, -halfHeight),
            New PointF(halfWidth, -halfHeight),
            New PointF(halfWidth, halfHeight),
            New PointF(-halfWidth, halfHeight)}
        For Each corner As PointF In localCorners
            visualPoints.Add(New PointF(CSng(centerX + corner.X * Math.Cos(radians) - corner.Y * Math.Sin(radians)),
                                        CSng(centerY + corner.X * Math.Sin(radians) + corner.Y * Math.Cos(radians))))
        Next

        ' Center depth handle and the rotation handle at the end of its stem.
        visualPoints.Add(New PointF(centerX, centerY))
        Dim rotationLocalY As Double = -(halfHeight + 24.0)
        visualPoints.Add(New PointF(CSng(centerX - rotationLocalY * Math.Sin(radians)),
                                    CSng(centerY + rotationLocalY * Math.Cos(radians))))

        Dim minX As Single = visualPoints(0).X
        Dim maxX As Single = minX
        Dim minY As Single = visualPoints(0).Y
        Dim maxY As Single = minY
        For Each point As PointF In visualPoints
            minX = Math.Min(minX, point.X)
            maxX = Math.Max(maxX, point.X)
            minY = Math.Min(minY, point.Y)
            maxY = Math.Max(maxY, point.Y)
        Next

        ' Seven pixels covers the largest center handle; two more cover its
        ' outline, antialiasing and the selection-frame pen.
        Const editorPadding As Integer = 9
        Return Rectangle.FromLTRB(CInt(Math.Floor(minX)) - editorPadding,
                                  CInt(Math.Floor(minY)) - editorPadding,
                                  CInt(Math.Ceiling(maxX)) + editorPadding + 1,
                                  CInt(Math.Ceiling(maxY)) + editorPadding + 1)
    End Function

    ' Complete editor-only footprint for the grouped picture-animation
    ' rotation control. RefreshDragRegion must invalidate the previous and new
    ' stem, handle and angle-label positions, not only the unchanged image box.
    Friend Function PictureAnimationEditorBounds(ByVal bulb As Illumination.BulbInfo,
                                                  ByVal scale As Single,
                                                  ByVal angle As Single) As Rectangle
        If bulb Is Nothing OrElse scale <= 0.0F Then Return Rectangle.Empty

        Dim imageRect As New Rectangle(CInt(bulb.Location.X * scale),
                                       CInt(bulb.Location.Y * scale),
                                       Math.Max(1, CInt(bulb.Size.Width * scale)),
                                       Math.Max(1, CInt(bulb.Size.Height * scale)))
        Dim centerX As Single = imageRect.X + imageRect.Width / 2.0F
        Dim centerY As Single = imageRect.Y + imageRect.Height / 2.0F
        Dim handleRadius As Single = imageRect.Height / 2.0F + 24.0F
        Dim radians As Double = angle * Math.PI / 180.0R
        Dim handleX As Single = centerX + CSng(Math.Sin(radians) * handleRadius)
        Dim handleY As Single = centerY - CSng(Math.Cos(radians) * handleRadius)

        Dim cosine As Double = Math.Abs(Math.Cos(radians))
        Dim sine As Double = Math.Abs(Math.Sin(radians))
        Dim rotatedHalfWidth As Double = cosine * imageRect.Width / 2.0R + sine * imageRect.Height / 2.0R
        Dim rotatedHalfHeight As Double = sine * imageRect.Width / 2.0R + cosine * imageRect.Height / 2.0R
        Dim rotatedImageBounds As Rectangle = Rectangle.FromLTRB(
            CInt(Math.Floor(centerX - rotatedHalfWidth)) - 10,
            CInt(Math.Floor(centerY - rotatedHalfHeight)) - 10,
            CInt(Math.Ceiling(centerX + rotatedHalfWidth)) + 11,
            CInt(Math.Ceiling(centerY + rotatedHalfHeight)) + 11)
        Dim handleBounds As Rectangle = Rectangle.FromLTRB(
            CInt(Math.Floor(Math.Min(centerX, handleX))) - 10,
            CInt(Math.Floor(Math.Min(centerY, handleY))) - 10,
            CInt(Math.Ceiling(Math.Max(centerX, handleX))) + 11,
            CInt(Math.Ceiling(Math.Max(centerY, handleY))) + 11)
        Return Rectangle.Union(rotatedImageBounds, handleBounds)
    End Function

    Private Shared Function LerpPoint(ByVal a As PointF, ByVal b As PointF, ByVal t As Single) As PointF
        Return New PointF(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t)
    End Function

    Private Sub DrawPerspectiveBitmap(ByVal graphics As Graphics, ByVal bitmap As Bitmap, ByVal quad As PointF())
        If graphics Is Nothing OrElse bitmap Is Nothing OrElse quad Is Nothing OrElse quad.Length <> 4 Then Return
        Dim strips As Integer = Math.Max(24, Math.Min(160, bitmap.Width))
        Dim oldMode As Drawing2D.InterpolationMode = graphics.InterpolationMode
        graphics.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
        Try
            For i As Integer = 0 To strips - 1
                Dim t0 As Single = CSng(i / CDbl(strips))
                Dim t1 As Single = CSng((i + 1) / CDbl(strips))
                Dim top0 As PointF = LerpPoint(quad(0), quad(1), t0)
                Dim top1 As PointF = LerpPoint(quad(0), quad(1), t1)
                Dim bottom0 As PointF = LerpPoint(quad(3), quad(2), t0)
                Dim srcX0 As Single = bitmap.Width * t0
                Dim srcX1 As Single = bitmap.Width * t1
                Dim dest() As PointF = {top0, top1, bottom0}
                graphics.DrawImage(bitmap, dest, New RectangleF(srcX0, 0, Math.Max(1.0F, srcX1 - srcX0 + 0.75F), bitmap.Height), GraphicsUnit.Pixel)
            Next
        Finally
            graphics.InterpolationMode = oldMode
        End Try
    End Sub

    Private Sub DrawScoreLayerNative(ByVal graphics As Graphics, ByVal score As ReelAndLED.ScoreInfo)
        If graphics Is Nothing OrElse score Is Nothing OrElse Not LayerManager.IsVisible(score) Then Return
        If Not (ShowScoreFrames OrElse ShowScoring) Then Return
        If score.DisplayState <> eScoreDisplayState.Visible OrElse score.Digits <= 0 Then Return
        Dim rect As New Rectangle(score.Location, score.Size)
        If rect.Width <= 1 OrElse rect.Height <= 1 Then Return

        Dim width As Single = ((rect.Width - score.Spacing / 2.0F * (score.Digits - 1)) / score.Digits)
        Do While width * score.Digits > rect.Width - 1 AndAlso width > 1
            width -= 1
        Loop
        Dim height As Integer = rect.Height - 1
        If score.PerfectScaleWidthFix Then height = CInt(My.Resources.LED_0.Height / My.Resources.LED_0.Width * width)
        Dim newSize As New Size(Math.Max(1, CInt(width)), Math.Max(1, height))
        Dim reelImage As Image = GetReelImage(score.ReelType, score.ReelColor, Backglass.currentData.UseDream7LEDs, Backglass.currentData.D7Thickness, Backglass.currentData.D7Shear, Backglass.currentData.D7Glow, newSize).Resized(newSize)
        If reelImage Is Nothing Then Return
        Try
            If score.ReelIlluLocation <> eReelIlluminationLocation.Off AndAlso score.ReelIlluIntensity > 0 Then
                Dim illuminated As Image = Lights.DrawIlluminatedReelImage(reelImage, score.ReelIlluIntensity, score.ReelIlluLocation)
                If illuminated IsNot Nothing AndAlso Not Object.ReferenceEquals(illuminated, reelImage) Then
                    reelImage.Dispose()
                    reelImage = illuminated
                End If
            End If
            If score.Reel3DEnabled Then
                Dim enhanced As Bitmap = ReelAndLED.Reel3DEffect.RenderReel(reelImage,
                                                                           score.Reel3DBrightness,
                                                                           score.Reel3DTemperature,
                                                                           score.Reel3DDepth)
                If enhanced IsNot Nothing Then
                    reelImage.Dispose()
                    reelImage = enhanced
                End If
            End If
            Using scoreBitmap As New Bitmap(Math.Max(2, rect.Width), Math.Max(2, rect.Height), Imaging.PixelFormat.Format32bppArgb)
                Using sg As Graphics = Graphics.FromImage(scoreBitmap)
                    sg.Clear(Color.Transparent)
                    sg.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
                    Dim x As Single = 1.0F
                    For i As Integer = 1 To score.Digits
                        sg.DrawImage(reelImage, New PointF(x, 1.0F))
                        x += width + score.Spacing / 2.0F
                    Next
                End Using
                Dim clippedScore As Bitmap = Nothing
                If score.BehindCanvas AndAlso Me.Image IsNot Nothing Then
                    clippedScore = Illumination.Lights.CreateCanvasClippedSnippet(scoreBitmap, Me.Image, rect)
                End If
                Try
                    DrawPerspectiveBitmap(graphics, If(clippedScore, scoreBitmap), ScorePerspectivePoints(score, 1.0F))
                Finally
                    If clippedScore IsNot Nothing Then clippedScore.Dispose()
                End Try
                If score.Reel3DEnabled Then
                    Using glassOverlay As New Bitmap(scoreBitmap.Width, scoreBitmap.Height, Imaging.PixelFormat.Format32bppArgb)
                        Using overlayGraphics As Graphics = Graphics.FromImage(glassOverlay)
                            overlayGraphics.Clear(Color.Transparent)
                            ReelAndLED.Reel3DEffect.DrawWindowOverlay(overlayGraphics,
                                                                     New Rectangle(0, 0, glassOverlay.Width, glassOverlay.Height),
                                                                     score.Reel3DDepth,
                                                                     score.Reel3DGlass)
                        End Using
                        DrawPerspectiveBitmap(graphics, glassOverlay, ScorePerspectivePoints(score, 1.0F))
                    End Using
                End If
            End Using
        Finally
            reelImage.Dispose()
        End Try
    End Sub

    Private Sub DrawUnifiedBulbOrSnippetNative(ByVal graphics As Graphics,
                                                ByVal composed As Bitmap,
                                                ByVal bulb As Illumination.BulbInfo,
                                                Optional ByVal forceVisible As Boolean = False)
        If bulb Is Nothing OrElse Not LayerManager.IsVisible(bulb) OrElse Not BulbPassesCurrentFilter(bulb) OrElse (Not forceVisible AndAlso Not IsBlinkPhaseOn(bulb)) Then Return
        If Not IsPictureAnimationReferenceFrame(bulb) Then Return
        If bulb.IsImageSnippit Then
            DrawSnippetLayerNative(graphics, bulb)
        ElseIf (ShowIllumination OrElse forceVisible) AndAlso Not IsExternalIlluminationImageIlluminated Then
            DrawBulbLayerOnCurrentComposite(graphics, composed, bulb)
        End If
    End Sub

    Private Sub DrawSnippetLayerNative(ByVal graphics As Graphics, ByVal bulb As Illumination.BulbInfo)
        If bulb.Image Is Nothing Then Return
        Dim target As New Rectangle(bulb.Location, bulb.Size)
        If target.Width <= 0 OrElse target.Height <= 0 Then Return
        Dim layerOpacity As Integer = LayerManager.GetOpacity(bulb)
        Dim brightness As Integer = Math.Max(0, Math.Min(200, bulb.SnippitInfo.Brightness))
        Dim clippedSnippet As Bitmap = Nothing
        Dim snippetImage As Image = bulb.Image
        If Not String.IsNullOrEmpty(bulb.SelectionMaskData) Then
            clippedSnippet = Illumination.Lights.CreateSelectionMaskedSnippet(snippetImage, bulb.SelectionMaskData, target)
            If clippedSnippet IsNot Nothing Then snippetImage = clippedSnippet
        End If
        If bulb.SnippitInfo.BehindCanvas AndAlso Me.Image IsNot Nothing Then
            Dim canvasClipped As Bitmap = Illumination.Lights.CreateCanvasClippedSnippet(snippetImage, Me.Image, target)
            If canvasClipped IsNot Nothing Then
                If clippedSnippet IsNot Nothing Then clippedSnippet.Dispose()
                clippedSnippet = canvasClipped
                snippetImage = clippedSnippet
            End If
        End If
        Try
            If layerOpacity >= 100 AndAlso brightness = 100 Then
                If clippedSnippet IsNot Nothing Then
                    graphics.DrawImageUnscaled(snippetImage, target.Location)
                Else
                    graphics.DrawImage(snippetImage, target)
                End If
            ElseIf layerOpacity > 0 Then
            Using attributes As New ImageAttributes()
                Dim matrix As New ColorMatrix()
                Dim brightnessScale As Single = CSng(brightness / 100.0R)
                matrix.Matrix00 = brightnessScale
                matrix.Matrix11 = brightnessScale
                matrix.Matrix22 = brightnessScale
                matrix.Matrix33 = CSng(layerOpacity / 100.0)
                attributes.SetColorMatrix(matrix)
                    graphics.DrawImage(snippetImage, target, 0, 0, snippetImage.Width, snippetImage.Height, GraphicsUnit.Pixel, attributes)
                End Using
            End If
        Finally
            If clippedSnippet IsNot Nothing Then clippedSnippet.Dispose()
        End Try
    End Sub

    Private Sub DrawBulbLayerOnCurrentComposite(ByVal graphics As Graphics,
                                                 ByVal composed As Bitmap,
                                                 ByVal bulb As Illumination.BulbInfo)
        If composed Is Nothing OrElse bulb Is Nothing Then Return

        Dim rect As New Rectangle(bulb.Location, bulb.Size)
        Dim rectX As New Rectangle(bulb.LocationX, bulb.SizeX)
        If bulb.GlowSpread > 0 AndAlso rect.Width > 0 AndAlso rect.Height > 0 Then
            Dim scaleX As Double = If(rect.Width > 0, rectX.Width / CDbl(rect.Width), 1.0)
            Dim scaleY As Double = If(rect.Height > 0, rectX.Height / CDbl(rect.Height), 1.0)
            rect.Inflate(bulb.GlowSpread, bulb.GlowSpread)
            rectX.Inflate(CInt(Math.Round(bulb.GlowSpread * scaleX)), CInt(Math.Round(bulb.GlowSpread * scaleY)))
        End If
        rectX = Illumination.Create.RotatedLightBounds(rectX, bulb.LightRotationAngle)
        rectX.Intersect(New Rectangle(0, 0, composed.Width, composed.Height))
        If rectX.Width <= 0 OrElse rectX.Height <= 0 Then Return

        graphics.Flush()
        Dim renderer As New Illumination.Create()
        Dim overlay As Image = Nothing
        Dim font As Font = Nothing
        Dim convertedCanvas As Bitmap = Nothing
        Try
            If Not String.IsNullOrEmpty(bulb.Text) Then
                font = New Font(bulb.FontName, bulb.FontSize, bulb.FontStyle)
            End If
            Dim renderBackground As Bitmap = composed
            If bulb.LightBehindCanvas AndAlso Me.Image IsNot Nothing Then
                renderBackground = TryCast(Me.Image, Bitmap)
                If renderBackground Is Nothing Then
                    convertedCanvas = New Bitmap(Me.Image.Width, Me.Image.Height, PixelFormat.Format32bppArgb)
                    Using canvasGraphics As Graphics = Graphics.FromImage(convertedCanvas)
                        canvasGraphics.Clear(Color.Transparent)
                        canvasGraphics.DrawImageUnscaled(Me.Image, 0, 0)
                    End Using
                    renderBackground = convertedCanvas
                End If
            End If
            overlay = renderer.CreateOverlayImage(renderBackground,
                                                  rect,
                                                  rectX,
                                                  bulb.Intensity,
                                                  bulb.LightColor,
                                                  bulb.DodgeColor,
                                                  If(bulb.Text, String.Empty),
                                                  font,
                                                  bulb.TextAlignment,
                                                  bulb.IlluMode,
                                                  bulb.GlowSoftness,
                                                  bulb.GlowFalloff,
                                                  bulb.GlowIntensity,
                                                  bulb.SelectionMaskData,
                                                  bulb.SelectionFeather,
                                                  bulb.GlobalMaskLayerExplicit AndAlso Not bulb.InFrontOfGlobalMask, bulb.FlasherStyle, bulb.FlasherSaturation, bulb.FlasherHighlightProtection, bulb.FlasherDarkAreaLift, bulb.FlasherHotspotX, bulb.FlasherHotspotY, bulb.LightDiffusion, bulb.LightTemperature, bulb.LightPurpose = Illumination.eLightPurpose.Flasher, bulb.ArtworkContrast, bulb.MaskRadius, bulb.MaskSmartRadius, bulb.MaskSmooth, bulb.MaskFeather, bulb.MaskContrast, bulb.MaskShiftEdge, bulb.FlasherRadialSpikes, bulb.LightRotationAngle, bulb.LightBehindCanvas)
            If overlay IsNot Nothing AndAlso bulb.LightBehindCanvas AndAlso Me.Image IsNot Nothing Then
                Dim clipped As Bitmap = Illumination.Lights.CreateCanvasClippedSnippet(overlay, Me.Image, rectX)
                If clipped IsNot Nothing Then
                    overlay.Dispose()
                    overlay = clipped
                End If
            End If
            If overlay IsNot Nothing Then graphics.DrawImageUnscaled(overlay, rectX.Location)
        Finally
            If overlay IsNot Nothing Then overlay.Dispose()
            If font IsNot Nothing Then font.Dispose()
            If convertedCanvas IsNot Nothing Then convertedCanvas.Dispose()
        End Try
    End Sub

    Private Sub DrawMainBackglassLayer(ByVal graphics As Graphics)
        DrawMainBackglassLayer(graphics, New Rectangle(0, 0, Me.ClientSize.Width, Me.ClientSize.Height))
    End Sub

    Private Sub DrawMainBackglassLayer(ByVal graphics As Graphics, ByVal target As Rectangle)
        If Me.Image Is Nothing OrElse Backglass.currentData Is Nothing OrElse Not LayerManager.IsVisible(Backglass.currentData) Then Return
        If target.Width <= 0 OrElse target.Height <= 0 Then Return
        Dim opacity As Integer = LayerManager.GetOpacity(Backglass.currentData)
        If opacity >= 100 Then
            graphics.DrawImage(Me.Image, target)
        ElseIf opacity > 0 Then
            Using attributes As New ImageAttributes()
                Dim matrix As New ColorMatrix()
                matrix.Matrix33 = CSng(opacity / 100.0)
                attributes.SetColorMatrix(matrix)
                graphics.DrawImage(Me.Image, target, 0, 0, Me.Image.Width, Me.Image.Height, GraphicsUnit.Pixel, attributes)
            End Using
        End If
    End Sub

    Private Function BulbPassesCurrentFilter(ByVal bulb As Illumination.BulbInfo) As Boolean
        If bulb Is Nothing Then Return False
        If String.IsNullOrEmpty(RomInfoFilter) Then Return True
        If RomInfoFilter.Equals(bulb.B2SInfo2String) OrElse RomInfoFilter.Equals(bulb.RomInfo2String) Then Return True
        If RomInfoFilter.Equals("withoutid") Then
            Return (Backglass.currentData.CommType = eCommType.B2S AndAlso String.IsNullOrEmpty(bulb.B2SInfo2String)) OrElse
                   (Backglass.currentData.CommType = eCommType.Rom AndAlso String.IsNullOrEmpty(bulb.RomInfo2String))
        End If
        If RomInfoFilter.Equals("withname") Then Return Not String.IsNullOrEmpty(bulb.Name)
        If RomInfoFilter.Equals("off") Then Return bulb.InitialState = 0
        If RomInfoFilter.Equals("on") Then Return bulb.InitialState = 1
        If RomInfoFilter.Equals("alwayson") Then Return bulb.InitialState = 2
        If RomInfoFilter.Equals("authentic") Then Return bulb.DualMode <> eDualMode.Fantasy
        If RomInfoFilter.Equals("fantasy") Then Return bulb.DualMode <> eDualMode.Authentic
        Return False
    End Function

    Private Sub DrawSnippetLayer(ByVal graphics As Graphics, ByVal bulb As Illumination.BulbInfo, ByVal factor As Double)
        If bulb.Image Is Nothing Then Return
        Dim target As New Rectangle(CInt(bulb.Location.X * factor), CInt(bulb.Location.Y * factor),
                                    CInt(bulb.Size.Width * factor), CInt(bulb.Size.Height * factor))
        Dim layerOpacity As Integer = LayerManager.GetOpacity(bulb)
        Dim brightness As Integer = Math.Max(0, Math.Min(200, bulb.SnippitInfo.Brightness))
        If layerOpacity >= 100 AndAlso brightness = 100 Then
            graphics.DrawImage(bulb.Image, target)
        ElseIf layerOpacity > 0 Then
            Using attributes As New ImageAttributes()
                Dim matrix As New ColorMatrix()
                Dim brightnessScale As Single = CSng(brightness / 100.0R)
                matrix.Matrix00 = brightnessScale
                matrix.Matrix11 = brightnessScale
                matrix.Matrix22 = brightnessScale
                matrix.Matrix33 = CSng(layerOpacity / 100.0)
                attributes.SetColorMatrix(matrix)
                graphics.DrawImage(bulb.Image, target, 0, 0, bulb.Image.Width, bulb.Image.Height, GraphicsUnit.Pixel, attributes)
            End Using
        End If
    End Sub

    Private Sub DrawBulbLayer(ByVal graphics As Graphics, ByVal bulb As Illumination.BulbInfo, ByVal factor As Double)
        If Lights Is Nothing OrElse Lights.Images Is Nothing Then Return

        For Each light As KeyValuePair(Of Integer, Illumination.Lights.ImageInfo) In Lights.Images
            If light.Value Is Nothing OrElse light.Value.BulbID <> bulb.ID OrElse light.Value.Image Is Nothing Then Continue For
            Dim sourceRect As Rectangle = light.Value.Rectangle
            Dim target As New RectangleF(CSng(sourceRect.X * factor), CSng(sourceRect.Y * factor),
                                         CSng(sourceRect.Width * factor), CSng(sourceRect.Height * factor))
            graphics.DrawImage(light.Value.Image, target)
            Exit For
        Next
    End Sub

    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        If disposing Then
            ClearUnifiedCompositeCache()
            If lightBlinkTimer IsNot Nothing Then
                lightBlinkTimer.Stop()
                RemoveHandler lightBlinkTimer.Tick, AddressOf LightBlinkTimer_Tick
                lightBlinkTimer.Dispose()
            End If
        End If
        MyBase.Dispose(disposing)
    End Sub

    Protected Overrides Sub OnPaintBackground(pevent As System.Windows.Forms.PaintEventArgs)
        ' nothing to do
    End Sub

    Private Sub Lights_ReportProgress(sender As Object, e As Illumination.Lights.LightsProgressEventArgs) Handles Lights.ReportProgress
        RaiseEvent LightsReportProgress(sender, e)
    End Sub

    Private Sub Mouse_MouseDown(sender As Object, e As System.Windows.Forms.MouseEventArgs) Handles Mouse.MouseDown
        ' raise event
        RaiseEvent MyMouseDown(sender, e)
    End Sub
    Private Sub Mouse_MouseUp(sender As Object, e As System.Windows.Forms.MouseEventArgs) Handles Mouse.MouseUp
        If e.Button = Windows.Forms.MouseButtons.Left Then
            If SetGrillHeight AndAlso currentMouseLocation <> Nothing AndAlso Not Mouse.IsMouseOverGrillRemover Then
                Dim grillheight As Integer = (currentMouseLocation.Y * -1) + Backglass.currentData.Image.Height
                Backglass.currentData.GrillHeight = grillheight
                B2SMessageBox.Show(String.Format(My.Resources.TXT_GrillHeight, grillheight), AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information)
                DirectCast(Parent, B2STabPage).SetGrillHeight = False
            ElseIf SetSmallGrillHeight AndAlso currentMouseLocation <> Nothing AndAlso Not Mouse.IsMouseOverSmallGrillRemover Then
                Dim smallgrillheight As Integer = (currentMouseLocation.Y * -1) + Backglass.currentData.Image.Height
                Backglass.currentData.SmallGrillHeight = smallgrillheight
                B2SMessageBox.Show(String.Format(My.Resources.TXT_MiniGrillHeight, smallgrillheight), AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information)
                DirectCast(Parent, B2STabPage).SetSmallGrillHeight = False
            ElseIf SetDMDDefaultLocation AndAlso currentMouseLocation <> Nothing AndAlso Not Mouse.IsMouseOverDMDLocationRemover Then
                Backglass.currentData.DMDDefaultLocation = currentMouseLocation
                B2SMessageBox.Show(String.Format(My.Resources.TXT_DMDDefaultLocation, currentMouseLocation.X, currentMouseLocation.Y), AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information)
                DirectCast(Parent, B2STabPage).SetDMDDefaultLocation = False
            ElseIf SetGrillHeight AndAlso Mouse.IsMouseOverGrillRemover Then
                Backglass.currentData.GrillHeight = 0
                B2SMessageBox.Show(My.Resources.TXT_GrillRemoved, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information)
                DirectCast(Parent, B2STabPage).SetGrillHeight = False
            ElseIf SetSmallGrillHeight AndAlso Mouse.IsMouseOverSmallGrillRemover Then
                Backglass.currentData.SmallGrillHeight = 0
                B2SMessageBox.Show(My.Resources.TXT_MiniGrillRemoved, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information)
                DirectCast(Parent, B2STabPage).SetSmallGrillHeight = False
            ElseIf SetDMDDefaultLocation AndAlso Mouse.IsMouseOverDMDLocationRemover Then
                Backglass.currentData.DMDDefaultLocation = Nothing
                B2SMessageBox.Show(My.Resources.TXT_DMDDefaultLocationRemoved, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information)
                DirectCast(Parent, B2STabPage).SetDMDDefaultLocation = False
            End If
            Me.Invalidate()
        End If
        ' raise event
        RaiseEvent MyMouseUp(sender, e)
    End Sub
    Private Sub Mouse_MouseMove(sender As Object, e As Mouse.MouseMoveEventArgs) Handles Mouse.MouseMove
        If SetGrillHeight OrElse SetSmallGrillHeight OrElse SetDMDDefaultLocation Then
            currentMouseLocation = e.Location
            Me.Invalidate()
        End If
        RaiseEvent MyMouseMove(sender, e)
    End Sub

    Private Sub Mouse_RemoveDMDCopyArea(sender As Object, e As System.EventArgs) Handles Mouse.RemoveDMDCopyArea
        RaiseEvent RemoveDMDCopyArea(sender, e)
    End Sub
    Private Sub Mouse_CopyDMDCopyArea(sender As Object, e As System.EventArgs) Handles Mouse.CopyDMDCopyArea
        DirectCast(Parent, B2STabPage).CopyDMDImageFromBackglass = False
        RaiseEvent CopyDMDCopyArea(sender, e)
    End Sub
    Private Sub Mouse_SelectedBulbMoved(sender As Object, e As System.Windows.Forms.MouseEventArgs) Handles Mouse.SelectedBulbMoved
        ' Imported picture animations cache each PA_ frame's rendered image and
        ' rectangle when Preview first runs. A group move changes the BulbInfo
        ' locations, while resize/rotate can also replace the source images. Do
        ' not let the next preview reuse those stale rectangles or image handles.
        Dim movedBulb As Illumination.BulbInfo = If(Mouse Is Nothing, Nothing, Mouse.SelectedBulb)
        If movedBulb IsNot Nothing AndAlso movedBulb.IsImageSnippit AndAlso
           Not String.IsNullOrEmpty(movedBulb.Name) AndAlso
           movedBulb.Name.StartsWith("PA_", StringComparison.OrdinalIgnoreCase) Then
            RefreshPictureAnimationPreviewCacheAfterEdit()
        End If
        RaiseEvent SelectedBulbMoved(sender, e)
    End Sub

    Private Sub RefreshPictureAnimationPreviewCacheAfterEdit()
        If Lights Is Nothing Then Return

        Lights.ClearImages()

        ' A preview normally finishes before editing. If a frame group is edited
        ' while its preview timer is still active, rebuild immediately so the
        ' remainder of that preview stays visible at the new location.
        If currentAnimationSteps IsNot Nothing AndAlso Me.Image IsNot Nothing Then
            Lights.DrawImages(Me.Image)
        End If
        MyBase.Invalidate()
    End Sub
    Private Sub Mouse_SelectedBulbEdited(sender As Object, e As System.EventArgs) Handles Mouse.SelectedBulbEdited
        RaiseEvent SelectedBulbEdited(sender, e)
    End Sub
    Private Sub Mouse_SelectedItemClicked(sender As Object, e As Mouse.MouseMoveEventArgs) Handles Mouse.SelectedItemClicked
        RaiseEvent SelectedItemClicked(sender, e)
    End Sub
    Private Sub Mouse_SelectedItemMoving(sender As Object, e As Mouse.MouseMoveEventArgs) Handles Mouse.SelectedItemMoving
        RaiseEvent SelectedItemMoving(sender, e)
    End Sub
    Private Sub Mouse_SelectedItemRemoved(sender As Object, e As System.EventArgs) Handles Mouse.SelectedItemRemoved
        RefreshAfterVisualCollectionChanged()
        RaiseEvent SelectedItemRemoved(sender, e)
    End Sub

    Public Sub New(ByVal _isDMD As Boolean)
        Me.IsDMDPictureBox = _isDMD
        Me.SizeMode = PictureBoxSizeMode.StretchImage
        Mouse = New Mouse(Me)
        Lights = New Illumination.Lights(Me, Me.IsDMDPictureBox)
    End Sub

    Public Shadows Sub Invalidate()
        ' recalc the factor
        If Backglass.currentData IsNot Nothing AndAlso Me.Image IsNot Nothing AndAlso Mouse IsNot Nothing Then
            Mouse.factor = Me.Width / Me.Image.Width 'Backglass.currentData.Image.Width
            Lights.factor = Mouse.factor
        End If
        ' the real invalidate
        MyBase.Invalidate()
    End Sub

    Private _SetGrillHeight As Boolean = False
    Public Property SetGrillHeight() As Boolean
        Get
            Return _SetGrillHeight
        End Get
        Set(ByVal value As Boolean)
            If _SetGrillHeight <> value Then
                _SetGrillHeight = value
                Mouse.SetGrillHeight = value
                Me.Invalidate()
            End If
        End Set
    End Property
    Private _SetSmallGrillHeight As Boolean = False
    Public Property SetSmallGrillHeight() As Boolean
        Get
            Return _SetSmallGrillHeight
        End Get
        Set(ByVal value As Boolean)
            If _SetSmallGrillHeight <> value Then
                _SetSmallGrillHeight = value
                Mouse.SetSmallGrillHeight = value
                Me.Invalidate()
            End If
        End Set
    End Property

    Public Property _CutDMDImageFromBackglass() As Boolean
    Public Property CopyDMDImageFromBackglass() As Boolean
        Get
            Return _CutDMDImageFromBackglass
        End Get
        Set(ByVal value As Boolean)
            If _CutDMDImageFromBackglass <> value Then
                _CutDMDImageFromBackglass = value
                Mouse.CopyDMDImageFromBackglass = value
                Me.Invalidate()
            End If
        End Set
    End Property
    Public Property _SetDMDDefaultLocation() As Boolean
    Public Property SetDMDDefaultLocation() As Boolean
        Get
            Return _SetDMDDefaultLocation
        End Get
        Set(ByVal value As Boolean)
            If _SetDMDDefaultLocation <> value Then
                _SetDMDDefaultLocation = value
                Mouse.SetDMDLocation = value
                Me.Invalidate()
            End If
        End Set
    End Property

    Private _ShowScoreFrames As Boolean = False
    Public Property ShowScoreFrames() As Boolean
        Get
            Return _ShowScoreFrames
        End Get
        Set(ByVal value As Boolean)
            If _ShowScoreFrames <> value Then
                _ShowScoreFrames = value
                Mouse.ShowScoreFrames = value
                ' Reel/LED previews are part of the cached unified composite.
                ' A frame-state change must rebuild that composite immediately;
                ' otherwise previews appear only after an object is moved and
                ' remain cached after the frames are switched off.
                ClearUnifiedCompositeCache()
                Me.Refresh()
            End If
        End Set
    End Property

    Private _ShowScoring As Boolean = False
    Public Property ShowScoring() As Boolean
        Get
            Return _ShowScoring
        End Get
        Set(ByVal value As Boolean)
            If _ShowScoring <> value Then
                _ShowScoring = value
                ClearUnifiedCompositeCache()
                Me.Refresh()
            End If
        End Set
    End Property

    Private _ShowIlluFrames As Boolean = False
    Public Property ShowIlluFrames() As Boolean
        Get
            Return _ShowIlluFrames
        End Get
        Set(ByVal value As Boolean)
            If _ShowIlluFrames <> value Then
                _ShowIlluFrames = value
                Mouse.ShowIlluFrames = value
                If (Backglass.currentData.IsExternalIlluminationImageSelected OrElse IsExternalIlluminationImageFramed) AndAlso Not IsExternalIlluminationImageIlluminated Then
                    If _ShowIlluFrames Then
                        IsExternalIlluminationImageFramed = True
                        MyBase.Image = GetFirstSelectedIlluImage()
                    Else
                        IsExternalIlluminationImageFramed = False
                        MyBase.Image = _image
                    End If
                End If
                Me.Invalidate()
            End If
        End Set
    End Property

    Private _ShowIllumination As Boolean = False
    Public Property ShowIllumination() As Boolean
        Get
            Return _ShowIllumination
        End Get
        Set(ByVal value As Boolean)
            If _ShowIllumination <> value Then
                _ShowIllumination = value
                If _ShowIllumination Then
                    If _image IsNot Nothing AndAlso Not IsDMDPictureBox AndAlso Backglass.currentData.IsExternalIlluminationImageSelected Then
                        IsExternalIlluminationImageIlluminated = True
                        Dim factor As Double = Mouse.factor
                        Dim illuimage As Image = GetFirstSelectedIlluImage()
                        Dim newimage As Image = _image.Clone
                        Using gr As Graphics = Graphics.FromImage(newimage)
                            For Each bulb As Illumination.BulbInfo In Backglass.currentBulbs
                                With bulb
                                    Dim rect As Rectangle = New Rectangle(bulb.LocationX, bulb.SizeX)
                                    Dim frameimage As Image = illuimage.PartFromImage(rect)
                                    gr.DrawImage(frameimage, rect)
                                End With
                            Next
                        End Using
                        MyBase.Image = newimage
                    Else
                        Dim image As Image = If(IsDMDPictureBox, Backglass.currentData.DMDImage, Backglass.currentData.Image)
                        If image IsNot Nothing Then
                            If _ShowIntensityIllumination Then
                                Lights.PrepareImage(image)
                                Lights.DrawImages(image, , , , RomInfoFilter)
                            Else
                                Lights.DrawImage(image, RomInfoFilter)
                            End If
                        End If
                    End If
                Else
                    If MyBase.Image IsNot Nothing AndAlso Not IsDMDPictureBox AndAlso IsExternalIlluminationImageIlluminated Then
                        IsExternalIlluminationImageIlluminated = False
                        MyBase.Image.Dispose()
                        If ShowIlluFrames AndAlso Backglass.currentData.IsExternalIlluminationImageSelected Then
                            IsExternalIlluminationImageFramed = True
                            MyBase.Image = GetFirstSelectedIlluImage()
                        Else
                            MyBase.Image = _image
                        End If
                    Else
                        'Lights.ClearImages()
                        Lights.ClearImage()
                    End If
                End If
                Me.Invalidate()
            End If
        End Set
    End Property

    Private _ShowIntensityIllumination As Boolean
    Public Property ShowIntensityIllumination() As Boolean
        Get
            Return _ShowIntensityIllumination
        End Get
        Set(ByVal value As Boolean)
            _ShowIntensityIllumination = value
            ShowIllumination = value
        End Set
    End Property

    Public Sub ResetAnimationLights()
        Lights.ClearImages()
    End Sub
    Public Sub ShowAnimation(ByVal _animationname As String)
        If Lights.Images.Count = 0 Then
            Lights.DrawImages(Image)
        End If
        StartAnimation(_animationname)
    End Sub

    Private _image As Image = Nothing
    Public Shadows Property Image() As Image
        Get
            Return _image
        End Get
        Set(ByVal value As Image)
            ' Enhanced 3.0.0: retain the artwork for the shared layer renderer,
            ' but do not let PictureBox paint it automatically behind all layers.
            MyBase.Image = Nothing
            _image = value
            ' set size
            If value IsNot Nothing Then
                Me.Size = value.Size
            Else
                Me.Size = New Size(0, 0)
            End If
            ' maybe show picture box
            If Not Me.Visible Then Me.Visible = (value IsNot Nothing)
        End Set
    End Property

    Public Property IsDMDPictureBox() As Boolean = False

    Private _rominfofilter As String = String.Empty
    Public Property RomInfoFilter() As String
        Get
            Return _rominfofilter
        End Get
        Set(value As String)
            _rominfofilter = value
            Mouse.RomInfoFilter = value
            Lights.ClearImages()
            Me.Invalidate()
        End Set
    End Property

    'Public ReadOnly Property ImportedIlluminatedImage() As Image
    '    Get
    '        Return If(myIsExternalIlluminationImageSelected, MyBase.Image, Nothing)
    '    End Get
    'End Property

    Public ReadOnly Property OffImage() As Image
        Get
            Return GetFirstOffImage()
        End Get
    End Property

    Public ReadOnly Property OnImageRomID() As Integer
        Get
            Return GetOnImageRomID()
        End Get
    End Property
    Public ReadOnly Property OnImageRomIDType() As eRomIDType
        Get
            Return GetOnImageRomIDType()
        End Get
    End Property

    Public ReadOnly Property DarkImage() As Image
        Get
            Dim newimage As Bitmap = New Bitmap(ExportCanvasImage())
            Using gr As Graphics = Graphics.FromImage(newimage)
                For Each score As ReelAndLED.ScoreInfo In Backglass.currentScores
                    With score
                        If .Digits > 0 Then
                            Dim rect As Rectangle = New Rectangle(score.Location, score.Size)
                            Dim width As Single = ((rect.Width - .Spacing / 2 * (.Digits - 1)) / .Digits)
                            Do While width * .Digits > rect.Width - 1
                                width -= 1
                            Loop
                            Dim height As Integer = rect.Height - 1
                            Dim x As Single = rect.X + 1
                            Dim y As Integer = rect.Y + 1
                            Dim newsize As Size = New Size(width, height)
                            Dim image As Image = GetReelImage(.ReelType, Color.FromArgb(0, 0, 0), Backglass.currentData.UseDream7LEDs, Backglass.currentData.D7Thickness, Backglass.currentData.D7Shear, Backglass.currentData.D7Glow, newsize).Resized(newsize)
                            If image IsNot Nothing Then
                                For i As Integer = 1 To .Digits
                                    gr.DrawImage(image, New Point(x, y))
                                    x += width + .Spacing / 2
                                Next
                                image.Dispose()
                            End If
                        End If
                    End With
                Next
            End Using
            ' that's it
            Return newimage
        End Get
    End Property
    Public ReadOnly Property IlluminatedImage() As Image
        Get
            ' create the light image
            Lights.DrawImage(GetFirstOnImage())
            ' do the overlay of the images
            Dim newimage As Bitmap = New Bitmap(ExportCanvasImage())
            Dim lightimage As Bitmap = New Bitmap(Lights.Image)
            'lightimage.MakeTransparent(Color.White)
            Using gr As Graphics = Graphics.FromImage(newimage)
                gr.DrawImage(lightimage, 0, 0)
                For Each bulb As Illumination.BulbInfo In Backglass.currentBulbs
                    If bulb.IsImageSnippit AndAlso bulb.Image IsNot Nothing Then
                        Dim brightness As Single = CSng(Math.Max(0, Math.Min(200, bulb.SnippitInfo.Brightness)) / 100.0R)
                        If brightness = 1.0F Then
                            gr.DrawImage(bulb.Image, New Rectangle(bulb.Location, bulb.Size))
                        Else
                            Using attributes As New ImageAttributes()
                                Dim matrix As New ColorMatrix()
                                matrix.Matrix00 = brightness
                                matrix.Matrix11 = brightness
                                matrix.Matrix22 = brightness
                                attributes.SetColorMatrix(matrix)
                                gr.DrawImage(bulb.Image, New Rectangle(bulb.Location, bulb.Size), 0, 0, bulb.Image.Width, bulb.Image.Height, GraphicsUnit.Pixel, attributes)
                            End Using
                        End If
                    End If
                Next
                For Each score As ReelAndLED.ScoreInfo In Backglass.currentScores
                    With score
                        If .Digits > 0 Then
                            Dim rect As Rectangle = New Rectangle(score.Location, score.Size)
                            Dim width As Single = ((rect.Width - .Spacing / 2 * (.Digits - 1)) / .Digits)
                            Do While width * .Digits > rect.Width - 1
                                width -= 1
                            Loop
                            Dim height As Integer = rect.Height - 1
                            Dim x As Single = rect.X + 1
                            Dim y As Integer = rect.Y + 1
                            Dim newsize As Size = New Size(width, height)
                            Dim image As Image = GetReelImage(.ReelType, .ReelColor, Backglass.currentData.UseDream7LEDs, Backglass.currentData.D7Thickness, Backglass.currentData.D7Shear, Backglass.currentData.D7Glow, newsize).Resized(newsize)
                            If image IsNot Nothing Then
                                For i As Integer = 1 To .Digits
                                    gr.DrawImage(image, New Point(x, y))
                                    x += width + .Spacing / 2
                                Next
                                image.Dispose()
                            End If
                        End If
                    End With
                Next
            End Using
            lightimage.Dispose()
            ' that's it
            Return newimage
        End Get
    End Property
    Public ReadOnly Property IlluminatedImageOnlyWithAlwaysOnLights() As Image
        Get
            Dim canvas As Image = ExportCanvasImage()
            If canvas IsNot Nothing Then
                Lights.ClearImages()
                ' create the light image
                Lights.DrawImages(GetFirstOnImage(), , True)
                ' do the overlay of the images
                Dim newimage As Bitmap = New Bitmap(canvas)
                For Each light As KeyValuePair(Of Integer, Illumination.Lights.ImageInfo) In Lights.OrderedImages
                    With light.Value
                        Dim lightimage As Bitmap = New Bitmap(.Image)
                        lightimage.MakeTransparent(Color.White)
                        Using gr As Graphics = Graphics.FromImage(newimage)
                            gr.DrawImage(lightimage, .Rectangle.X, .Rectangle.Y)
                        End Using
                        lightimage.Dispose()
                    End With
                Next
                ' that's it
                Return newimage
            Else
                Return Nothing
            End If
        End Get
    End Property
    Public ReadOnly Property IlluminatedImageOnlyWithOnLights() As Image
        Get
            Dim canvas As Image = ExportCanvasImage()
            If canvas IsNot Nothing Then
                Lights.ClearImages()
                ' create the light image
                Lights.DrawImages(GetFirstOnImage(), , , True)
                ' do the overlay of the images
                Dim newimage As Bitmap = New Bitmap(canvas)
                For Each light As KeyValuePair(Of Integer, Illumination.Lights.ImageInfo) In Lights.OrderedImages
                    With light.Value
                        Dim lightimage As Bitmap = New Bitmap(.Image)
                        lightimage.MakeTransparent(Color.White)
                        Using gr As Graphics = Graphics.FromImage(newimage)
                            gr.DrawImage(lightimage, .Rectangle.X, .Rectangle.Y)
                        End Using
                        lightimage.Dispose()
                    End With
                Next
                ' that's it
                Return newimage
            Else
                Return Nothing
            End If
        End Get
    End Property

    Public ReadOnly Property FirstStoredIlluminationImage() As Image
        Get
            Return GetFirstSelectedIlluImage()
        End Get
    End Property

    Public ReadOnly Property IlluminatedImages(ByVal currentimage As Image, _
                                               Optional ByVal currentoffimage As Image = Nothing) As Generic.SortedList(Of Integer, Illumination.Lights.ImageInfo)
        Get
            Lights.ClearImages()
            Lights.DrawImages(currentimage, currentoffimage)
            Return Lights.Images()
        End Get
    End Property

    Public Function DrawIlluminatedReelImage(ByVal reelimage As Image, ByVal reelintensity As Integer, ByVal reelillulocation As eReelIlluminationLocation) As Image
        Return Lights.DrawIlluminatedReelImage(reelimage, reelintensity, reelillulocation)
    End Function

    Private Function GetFirstOffImage() As Image
        Dim ret As Image = Nothing
        For Each item As Images.ImageInfo In Backglass.currentData.Images
            If item.Type = Images.eImageInfoType.BackgroundImage AndAlso item.BackgroundImageType = Images.eBackgroundImageType.Off Then
                ret = item.Image
                Exit For
            End If
        Next
        Return ret
    End Function

    Private Function GetFirstOnImage() As Image
        ' DMD artwork has its own canvas and must never be replaced by the first
        ' backglass On image from the shared resource collection.
        If IsDMDPictureBox Then Return Me.Image

        Dim ret As Image = MyBase.Image
        For Each item As Images.ImageInfo In Backglass.currentData.Images
            If item.Type = Images.eImageInfoType.BackgroundImage AndAlso item.BackgroundImageType = Images.eBackgroundImageType.On Then
                ret = item.Image
                Exit For
            End If
        Next
        Return ret
    End Function

    Private Function ExportCanvasImage() As Image
        ' Keep the proven backglass export path unchanged. Only the DMD canvas
        ' needs the shadowed renderer image because its base PictureBox image is
        ' intentionally empty when the DMD tab is not active.
        Return If(IsDMDPictureBox, Me.Image, MyBase.Image)
    End Function
    Private Function GetOnImageRomID() As Integer
        Dim ret As Integer = 0
        For Each item As Images.ImageInfo In Backglass.currentData.Images
            If item.Type = Images.eImageInfoType.BackgroundImage AndAlso item.BackgroundImageType = Images.eBackgroundImageType.On Then
                ret = item.RomID
                Exit For
            End If
        Next
        Return ret
    End Function
    Private Function GetOnImageRomIDType() As eRomIDType
        Dim ret As eRomIDType = eRomIDType.NotUsed
        For Each item As Images.ImageInfo In Backglass.currentData.Images
            If item.Type = Images.eImageInfoType.BackgroundImage AndAlso item.BackgroundImageType = Images.eBackgroundImageType.On Then
                ret = item.RomIDType
                Exit For
            End If
        Next
        Return ret
    End Function

    Private Function GetFirstSelectedIlluImage() As Image
        Dim ret As Image = Nothing
        For Each item As Images.ImageInfo In Backglass.currentData.Images
            If item.Type = Images.eImageInfoType.IlluminationImage Then 'AndAlso item.Selected Then
                ret = item.Image
                Exit For
            End If
        Next
        Return ret
    End Function

    Private animationOn As Generic.List(Of String) = New Generic.List(Of String)
    Private animationtimer As Timer = Nothing
    Private currentAnimationStep As Integer = -1
    Private currentAnimationSteps As Animation.AnimationStepCollection = Nothing
    Private Sub StartAnimation(ByVal _animationname As String)
        ' reset some basic animation stuff
        animationOn.Clear()
        animationtimeroff = False
        animationtimerticks = 0
        animationtimerloops = 0
        ' look for the animation
        Dim animation As Animation.AnimationHeader = Nothing
        For Each animationheader As Animation.AnimationHeader In Backglass.currentAnimations
            If animationheader.Name.Equals(_animationname) Then
                animation = animationheader
                Exit For
            End If
        Next
        ' maybe start the animation
        If animation IsNot Nothing AndAlso animation.AnimationSteps.Count > 0 Then
            If animationtimer Is Nothing Then
                animationtimer = New Timer()
                With animationtimer
                    .Enabled = False
                    AddHandler .Tick, AddressOf AnimationTimer_Tick
                End With
            End If
            animationtimer.Interval = animation.Interval
            ' The editor preview must always finish. A permanent loop (Loops=0)
            ' runs once here, while the exported backglass repeats continuously.
            animationtimerloops = If(animation.Loops = 0, 1, animation.Loops)
            currentAnimationStep = 0
            currentAnimationSteps = animation.AnimationSteps
            AnimationTimer_Tick()
        End If
    End Sub
    Private animationtimeroff As Boolean = False
    Private animationtimerticks As Integer = 0
    Private animationtimerloops As Integer = 0
    Private Sub AnimationTimer_Tick()
        animationtimer.Stop()
        animationtimerticks -= 1
        Do While animationtimerticks <= 0
            If Not animationtimeroff Then
                animationtimeroff = True
                animationtimerticks = currentAnimationSteps(currentAnimationStep).WaitLoopsAfterOn
                For Each cntrl As String In currentAnimationSteps(currentAnimationStep).On.Split(",")
                    If Not animationOn.Contains(cntrl.ToLower) Then animationOn.Add(cntrl.ToLower)
                Next
                Me.Invalidate()

            Else
                animationtimeroff = False
                animationtimerticks = currentAnimationSteps(currentAnimationStep).WaitLoopsAfterOff
                For Each cntrl As String In currentAnimationSteps(currentAnimationStep).Off.Split(",")
                    If animationOn.Contains(cntrl.ToLower) Then animationOn.Remove(cntrl.ToLower)
                Next
                Me.Invalidate()

                currentAnimationStep += 1
            End If
            If currentAnimationStep >= currentAnimationSteps.Count Then
                Exit Do
            End If
        Loop
        If currentAnimationStep < currentAnimationSteps.Count Then
            animationtimer.Start()
        ElseIf animationtimerloops > 1 Then
            animationtimerloops -= 1
            currentAnimationStep = 0
            AnimationTimer_Tick()
        Else
            currentAnimationSteps = Nothing
        End If
    End Sub

End Class
