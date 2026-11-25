Imports System
Imports System.Drawing.Imaging

Public Class B2SPictureBox

    Inherits PictureBox

    <Runtime.InteropServices.DllImport("user32.dll")>
    Private Shared Function GetAsyncKeyState(ByVal key As Integer) As Short
    End Function

    Private canvasPanParent As ScrollableControl = Nothing
    Private canvasPanStart As Point
    Private canvasPanScroll As Point
    Private canvasPanCursor As Cursor

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        If e.Button = MouseButtons.Left AndAlso (CInt(GetAsyncKeyState(CInt(Keys.Space))) And &H8000) <> 0 Then
            Dim viewport As ScrollableControl = TryCast(Parent, ScrollableControl)
            If viewport IsNot Nothing AndAlso viewport.AutoScroll Then
                canvasPanParent = viewport
                canvasPanStart = PointToScreen(e.Location)
                canvasPanScroll = viewport.AutoScrollPosition
                canvasPanCursor = Cursor
                Cursor = Cursors.Hand
                Capture = True
                Return
            End If
        End If
        MyBase.OnMouseDown(e)
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        If canvasPanParent IsNot Nothing Then
            ' Screen coordinates stay stable while scrolling moves this control.
            Dim current As Point = PointToScreen(e.Location)
            canvasPanParent.AutoScrollPosition = New Point(
                Math.Max(0, -canvasPanScroll.X - (current.X - canvasPanStart.X)),
                Math.Max(0, -canvasPanScroll.Y - (current.Y - canvasPanStart.Y)))
            Return
        End If
        MyBase.OnMouseMove(e)
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        If canvasPanParent IsNot Nothing Then
            If e.Button = MouseButtons.Left Then EndCanvasPan()
            Return
        End If
        MyBase.OnMouseUp(e)
    End Sub

    Protected Overrides Sub OnMouseCaptureChanged(e As EventArgs)
        If Not Capture Then EndCanvasPan()
        MyBase.OnMouseCaptureChanged(e)
    End Sub

    Private Sub EndCanvasPan()
        If canvasPanParent Is Nothing Then Return
        canvasPanParent = Nothing
        Cursor = canvasPanCursor
        Capture = False
    End Sub

    ' A transparent backglass pixel is an opening in the printed canvas. Show
    ' that opening over a neutral mid-gray backing in the editor. This leaves
    ' visible headroom for behind-canvas brightness, highlights, color and light
    ' temperature while the renderer still treats transparency as transmissive.
    ' Opaque artwork, including true black pixels, is drawn unchanged.
    Private Shared ReadOnly EditorCanvasUnderlayColor As Color = Color.FromArgb(96, 96, 96)


    ' Enhanced 2.8.6: reduce flicker and repaint overhead on large canvases.
    Public Sub New()
        MyBase.New()
        Me.DoubleBuffered = True
        Me.ResizeRedraw = False
        Me.SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or ControlStyles.OptimizedDoubleBuffer, True)
        Me.UpdateStyles()
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
    Private NotInheritable Class CachedLightLayer
        Public Signature As Long
        Public BackdropDigest As String
        Public Bounds As Rectangle
        Public Image As Bitmap
    End Class
    Private ReadOnly lightLayerCache As New Generic.Dictionary(Of Integer, CachedLightLayer)()
    Private lightLayerCacheBytes As Long
    Private Const MaxLightLayerCacheBytes As Long = 96L * 1024 * 1024

    Private Sub ClearLightLayerCache()
        For Each entry As CachedLightLayer In lightLayerCache.Values
            entry.Image.Dispose()
        Next
        lightLayerCache.Clear()
        lightLayerCacheBytes = 0
    End Sub

    Private Shared Function BackdropDigest(ByVal bitmap As Bitmap, ByVal bounds As Rectangle) As String
        ' An upper light depends on everything already drawn underneath it.
        ' Hash actual pixels, not only the background object's identity, so
        ' moving a lower snippet/light can never reuse the old illuminated art.
        Dim data As BitmapData = bitmap.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb)
        Try
            Dim row(bounds.Width * 4 - 1) As Byte
            Using hash As Security.Cryptography.SHA256 = Security.Cryptography.SHA256.Create()
                For y As Integer = 0 To bounds.Height - 1
                    Runtime.InteropServices.Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length)
                    hash.TransformBlock(row, 0, row.Length, row, 0)
                Next
                hash.TransformFinalBlock(New Byte() {}, 0, 0)
                Return Convert.ToBase64String(hash.Hash)
            End Using
        Finally
            bitmap.UnlockBits(data)
        End Try
    End Function

    ' Enhanced 3.0.7 Stage 6: while an object is being dragged, keep a frozen
    ' full-quality composite that excludes the moving bulbs/snippets. Mouse
    ' paints then draw only lightweight previews at their current positions.
    Private dragPreviewActive As Boolean = False
    Private dragPreviewBase As Bitmap = Nothing
    Private ReadOnly dragPreviewBulbs As New Generic.List(Of Illumination.BulbInfo)()
    Private ReadOnly dragPreviewScores As New Generic.List(Of ReelAndLED.ScoreInfo)()

    Private ReadOnly dragPreviewScoreImages As New Generic.List(Of ScoreDragPreview)()

    Private Class ScoreDragPreview
        Public Score As ReelAndLED.ScoreInfo
        Public Content As Bitmap
        Public GlassOverlay As Bitmap
        Public RenderedSize As Size
        Public FrozenFinal As Bitmap
        Public FrozenBounds As Rectangle

        Public Sub Dispose()
            If FrozenFinal IsNot Nothing Then FrozenFinal.Dispose()
            FrozenFinal = Nothing
            If Content IsNot Nothing Then Content.Dispose()
            If GlassOverlay IsNot Nothing Then GlassOverlay.Dispose()
            Content = Nothing
            GlassOverlay = Nothing
        End Sub
    End Class

    Private Function IsDragPreviewScore(score As ReelAndLED.ScoreInfo) As Boolean
        Return dragPreviewScores.Any(Function(item) Object.ReferenceEquals(item, score))
    End Function

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

    ' Rendering and export can address the main and DMD picture boxes in the
    ' same operation.  Use the collection owned by this picture box instead of
    ' the collection selected by the editor's currently visible canvas.
    Private ReadOnly Property OwningBulbs() As Illumination.BulbCollection
        Get
            If Backglass.currentData Is Nothing Then Return Nothing
            Return If(IsDMDPictureBox, Backglass.currentData.DMDBulbs, Backglass.currentData.Bulbs)
        End Get
    End Property

    Private ReadOnly Property OwningScores() As ReelAndLED.ScoreCollection
        Get
            If Backglass.currentData Is Nothing Then Return Nothing
            Return If(IsDMDPictureBox, Backglass.currentData.DMDScores, Backglass.currentData.Scores)
        End Get
    End Property

    ' The normal editor uses one frame as a lightweight visual reference for a
    ' grouped picture animation. Actual animation preview has a separate
    ' step-driven renderer and therefore continues to display every frame at
    ' its proper time.
    Private Function IsPictureAnimationReferenceFrame(ByVal bulb As Illumination.BulbInfo) As Boolean
        Dim prefix As String = PictureAnimationPrefix(bulb)
        Dim visualBulbs As Illumination.BulbCollection = OwningBulbs
        If prefix.Length = 0 OrElse visualBulbs Is Nothing Then Return True

        Dim separator As Integer = bulb.Name.LastIndexOf("_"c)
        Dim currentFrame As Integer
        Integer.TryParse(bulb.Name.Substring(separator + 1), currentFrame)
        Dim lowestFrame As Integer = currentFrame
        For Each candidate As Illumination.BulbInfo In visualBulbs
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
            ElseIf TypeOf selected Is ReelAndLED.ScoreInfo Then
                dragPreviewScores.Add(DirectCast(selected, ReelAndLED.ScoreInfo))
            End If
        Next
        ' Keep the existing bulb/group preview unchanged. For scores, freeze
        ' only when the selected reels form the top of the visible stack.
        ' Upper lighting may sample a reel's pixels, so retain the normal
        ' compositor where freezing it could change the layer result.
        If dragPreviewBulbs.Count > 0 Then
            dragPreviewScores.Clear()
        ElseIf dragPreviewScores.Count > 0 Then
            Dim selectedLayerSeen As Boolean = False
            For Each item As Object In OrderedVisualItems()
                If Not LayerManager.IsVisible(DirectCast(item, InfoBase)) Then Continue For
                If TypeOf item Is ReelAndLED.ScoreInfo AndAlso IsDragPreviewScore(DirectCast(item, ReelAndLED.ScoreInfo)) Then
                    selectedLayerSeen = True
                ElseIf selectedLayerSeen AndAlso TypeOf item Is Illumination.BulbInfo Then
                    dragPreviewScores.Clear()
                    Return
                End If
            Next
        End If
        If dragPreviewBulbs.Count = 0 AndAlso dragPreviewScores.Count = 0 Then Return

        Dim nativeWidth As Integer = Me.Image.Width
        Dim nativeHeight As Integer = Me.Image.Height
        If nativeWidth <= 0 OrElse nativeHeight <= 0 Then Return
        Dim selectedScoreSeen As Boolean = False
        For Each item As Object In OrderedVisualItems()
            If Not TypeOf item Is ReelAndLED.ScoreInfo Then Continue For
            Dim score As ReelAndLED.ScoreInfo = DirectCast(item, ReelAndLED.ScoreInfo)
            If IsDragPreviewScore(score) Then selectedScoreSeen = True
            If Not selectedScoreSeen OrElse Not LayerManager.IsVisible(score) Then Continue For
            If IsDragPreviewScore(score) Then
                Dim glassOverlay As Bitmap = Nothing
                Dim content As Bitmap = CreateScoreContentBitmap(score, glassOverlay)
                dragPreviewScoreImages.Add(New ScoreDragPreview With {
                    .Score = score, .Content = content, .GlassOverlay = glassOverlay, .RenderedSize = score.Size})
            Else
                ' Preserve stationary upper displays exactly as the normal renderer
                ' draws them, rather than flattening them beneath the moving score.
                Dim points As PointF() = ScorePerspectivePoints(score, 1.0F)
                Dim bounds As Rectangle = Rectangle.FromLTRB(
                    CInt(Math.Floor(points.Min(Function(point) point.X))) - 2,
                    CInt(Math.Floor(points.Min(Function(point) point.Y))) - 2,
                    CInt(Math.Ceiling(points.Max(Function(point) point.X))) + 2,
                    CInt(Math.Ceiling(points.Max(Function(point) point.Y))) + 2)
                If bounds.Width <= 0 OrElse bounds.Height <= 0 Then Continue For
                Dim frozen As New Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb)
                Using frozenGraphics As Graphics = Graphics.FromImage(frozen)
                    frozenGraphics.TranslateTransform(-bounds.X, -bounds.Y)
                    DrawScoreLayerNative(frozenGraphics, score)
                End Using
                dragPreviewScoreImages.Add(New ScoreDragPreview With {
                    .Score = score, .FrozenFinal = frozen, .FrozenBounds = bounds})
            End If
        Next
        Dim previewBase As New Bitmap(nativeWidth, nativeHeight, PixelFormat.Format32bppArgb)
        Try
            Using layerGraphics As Graphics = Graphics.FromImage(previewBase)
                layerGraphics.Clear(EditorCanvasUnderlayColor)
                layerGraphics.SmoothingMode = Drawing2D.SmoothingMode.HighQuality
                layerGraphics.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
                layerGraphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality

                Dim ordered As Generic.IList(Of Object) = OrderedVisualItems()
                Dim belowRemaining As Integer = If(Backglass.currentData Is Nothing, 0, Backglass.currentData.CanvasLayerPositionFor(IsDMDPictureBox))
                Dim canvasDrawn As Boolean = False
                For Each item As Object In ordered
                    If Not canvasDrawn AndAlso belowRemaining <= 0 Then
                        DrawMainBackglassLayer(layerGraphics, New Rectangle(0, 0, nativeWidth, nativeHeight))
                        canvasDrawn = True
                    End If
                    If TypeOf item Is Illumination.BulbInfo Then
                        Dim bulb As Illumination.BulbInfo = DirectCast(item, Illumination.BulbInfo)
                        If Not IsDragPreviewBulb(bulb) Then DrawUnifiedBulbOrSnippetNative(layerGraphics, previewBase, bulb)
                    ElseIf TypeOf item Is ReelAndLED.ScoreInfo Then
                        Dim score As ReelAndLED.ScoreInfo = DirectCast(item, ReelAndLED.ScoreInfo)
                        If Not dragPreviewScoreImages.Any(Function(preview) Object.ReferenceEquals(preview.Score, score)) Then DrawScoreLayerNative(layerGraphics, score)
                    End If
                    If Not canvasDrawn AndAlso belowRemaining > 0 Then belowRemaining -= 1
                Next
                If Not canvasDrawn Then DrawMainBackglassLayer(layerGraphics, New Rectangle(0, 0, nativeWidth, nativeHeight))
            End Using
            If dragPreviewScores.Count > 0 Then
            dragPreviewBase = New Bitmap(Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height), PixelFormat.Format32bppArgb)
            Using displayGraphics As Graphics = Graphics.FromImage(dragPreviewBase)
                displayGraphics.DrawImage(previewBase, New Rectangle(Point.Empty, dragPreviewBase.Size))
            End Using
            Else
                dragPreviewBase = previewBase
                previewBase = Nothing
            End If
            dragPreviewActive = True
        Finally
            If previewBase IsNot Nothing Then previewBase.Dispose()
        End Try
    End Sub

    Public Sub EndDragPreview(Optional ByVal invalidateCanvas As Boolean = True)
        dragPreviewActive = False
        dragPreviewBulbs.Clear()
        dragPreviewScores.Clear()
        For Each preview As ScoreDragPreview In dragPreviewScoreImages
            preview.Dispose()
        Next
        dragPreviewScoreImages.Clear()
        If dragPreviewBase IsNot Nothing Then
            dragPreviewBase.Dispose()
            dragPreviewBase = Nothing
        End If
        If invalidateCanvas Then MyBase.Invalidate()
    End Sub

    Public Sub RefreshAfterVisualCollectionChanged()
        EndDragPreview(False)
        ClearUnifiedCompositeCache()
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
        If Not dragPreviewActive OrElse Mouse Is Nothing Then Return
        If dragPreviewScores.Count > 0 AndAlso Me.Image IsNot Nothing Then
            Dim state As Drawing2D.GraphicsState = graphics.Save()
            Try
                graphics.ScaleTransform(Me.ClientSize.Width / CSng(Me.Image.Width), Me.ClientSize.Height / CSng(Me.Image.Height))
                For Each item As Object In OrderedVisualItems()
                    If TypeOf item Is ReelAndLED.ScoreInfo Then
                        Dim preview As ScoreDragPreview = dragPreviewScoreImages.FirstOrDefault(Function(value) Object.ReferenceEquals(value.Score, item))
                        If preview IsNot Nothing Then
                            If preview.FrozenFinal IsNot Nothing Then
                                graphics.DrawImageUnscaled(preview.FrozenFinal, preview.FrozenBounds.Location)
                            Else
                                DrawScoreDragPreview(graphics, preview, 1.0F)
                            End If
                        End If
                    End If
                Next
            Finally
                graphics.Restore(state)
            End Try
        End If
        If dragPreviewBulbs.Count = 0 Then Return
        Dim factor As Double = Mouse.factor
        For Each bulb As Illumination.BulbInfo In dragPreviewBulbs
            If bulb Is Nothing OrElse Not LayerManager.IsVisible(bulb) Then Continue For
            If Not IsPictureAnimationReferenceFrame(bulb) Then Continue For
            Dim target As New Rectangle(CInt(bulb.Location.X * factor), CInt(bulb.Location.Y * factor),
                                        Math.Max(1, CInt(bulb.Size.Width * factor)), Math.Max(1, CInt(bulb.Size.Height * factor)))
            Dim rotationState As Drawing2D.GraphicsState = Nothing
            If Mouse.IsPictureAnimationRotationActive Then
                rotationState = graphics.Save()
                Dim centerX As Single = CSng((bulb.Location.X + bulb.Size.Width / 2.0R) * factor)
                Dim centerY As Single = CSng((bulb.Location.Y + bulb.Size.Height / 2.0R) * factor)
                graphics.TranslateTransform(centerX, centerY)
                graphics.RotateTransform(Mouse.PendingPictureAnimationAngle)
                graphics.TranslateTransform(-centerX, -centerY)
            ElseIf Not bulb.IsImageSnippit Then
                rotationState = graphics.Save()
                Dim centerX As Single = CSng((bulb.Location.X + bulb.Size.Width / 2.0R) * factor)
                Dim centerY As Single = CSng((bulb.Location.Y + bulb.Size.Height / 2.0R) * factor)
                graphics.TranslateTransform(centerX, centerY)
                Dim angle As Single = bulb.LightRotationAngle
                If Mouse.IsLightRotationActive AndAlso Object.ReferenceEquals(bulb, Mouse.SelectedBulb) Then
                    angle = Mouse.PendingLightRotationAngle
                End If
                graphics.RotateTransform(angle)
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

    Private Function BulbVisualSignature(ByVal bulb As Illumination.BulbInfo) As Long
        Dim hash As Long = 216613626L
        hash = MixVisualHash(hash, bulb.ID)
        hash = MixVisualHash(hash, bulb.ZOrder)
        hash = MixVisualHash(hash, bulb.LayerOrder)
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
        If bulb.SnippitInfo IsNot Nothing Then
            hash = MixVisualHash(hash, bulb.SnippitInfo.MaskEdgeSoftness)
            hash = MixVisualHash(hash, bulb.SnippitInfo.MaskOutwardDiffusion)
            hash = MixVisualHash(hash, bulb.SnippitInfo.MaskShiftEdge)
        End If
        hash = MixVisualHash(hash, bulb.Intensity)
        hash = MixVisualHash(hash, If(bulb.BlinkEnabled, 1, 0))
        hash = MixVisualHash(hash, bulb.BlinkInterval)
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
        hash = MixVisualHash(hash, If(String.IsNullOrEmpty(bulb.SelectionMaskData), 0, bulb.SelectionMaskData.GetHashCode()))
        hash = MixVisualHash(hash, If(String.IsNullOrEmpty(bulb.Text), 0, bulb.Text.GetHashCode()))
        hash = MixVisualHash(hash, If(String.IsNullOrEmpty(bulb.FontName), 0, bulb.FontName.GetHashCode()))
        hash = MixVisualHash(hash, CInt(bulb.FontSize * 100.0F))
        hash = MixVisualHash(hash, CInt(bulb.FontStyle))
        hash = MixVisualHash(hash, CInt(bulb.TextAlignment))
        hash = MixVisualHash(hash, CInt(Math.Round(bulb.TextBend * 1000.0F)))
        hash = MixVisualHash(hash, CInt(Math.Round(bulb.TextPerspectiveDepth * 1000.0F)))
        hash = MixVisualHash(hash, CInt(Math.Round(bulb.TextPerspectiveLeftScale * 1000.0F)))
        hash = MixVisualHash(hash, CInt(Math.Round(bulb.TextPerspectiveRightScale * 1000.0F)))
        hash = MixVisualHash(hash, LayerManager.GetOpacity(bulb))
        hash = MixVisualHash(hash, If(LayerManager.IsVisible(bulb), 1, 0))
        hash = MixVisualHash(hash, If(bulb.ArtworkPixelLighting, 1, 0))
        hash = MixVisualHash(hash, If(bulb.SeparateLightLayer, 1, 0))
        hash = MixVisualHash(hash, If(bulb.DynamicFrontLighting, 1, 0))
        hash = MixVisualHash(hash, If(bulb.DynamicFrontShowLight, 1, 0))
        hash = MixVisualHash(hash, bulb.DynamicFrontTargetSnippetID)
        hash = MixVisualHash(hash, If(bulb.DynamicFrontTargetSelectionDefined, 1, 0))
        For Each targetID As Integer In bulb.DynamicFrontTargetSnippetIDs.OrderBy(Function(value) value)
            hash = MixVisualHash(hash, targetID)
        Next
        If Backglass.currentData IsNot Nothing Then
            hash = MixVisualHash(hash, If(Backglass.currentData.GlobalIlluminationMaskEnabled, 1, 0))
            hash = MixVisualHash(hash, If(Backglass.currentData.GlobalIlluminationMaskInverted, 1, 0))
            hash = MixVisualHash(hash, Backglass.currentData.GlobalIlluminationMaskThreshold)
            hash = MixVisualHash(hash, If(Backglass.currentData.GlobalIlluminationMaskData, String.Empty).GetHashCode())
        End If
        Return hash
    End Function

    Private Function CurrentVisualSignature(ByVal nativeWidth As Integer, ByVal nativeHeight As Integer) As Long
        Dim hash As Long = 216613626L
        hash = MixVisualHash(hash, nativeWidth)
        hash = MixVisualHash(hash, nativeHeight)
        hash = MixVisualHash(hash, If(Me.Image Is Nothing, 0, Me.Image.GetHashCode()))
        hash = MixVisualHash(hash, If(ShowIllumination, 1, 0))
        hash = MixVisualHash(hash, If(IsExternalIlluminationImageIlluminated, 1, 0))
        hash = MixVisualHash(hash, If(String.IsNullOrEmpty(RomInfoFilter), 0, RomInfoFilter.GetHashCode()))
        If Backglass.currentData IsNot Nothing Then hash = MixVisualHash(hash, Backglass.currentData.CanvasLayerPositionFor(IsDMDPictureBox))
        Dim visualBulbs As Illumination.BulbCollection = OwningBulbs
        If visualBulbs IsNot Nothing Then
            hash = MixVisualHash(hash, visualBulbs.Count)
            For Each bulb As Illumination.BulbInfo In visualBulbs
                If bulb Is Nothing Then Continue For
                hash = MixVisualHash(hash, CInt(BulbVisualSignature(bulb)))
            Next
        End If
        Dim visualScores As ReelAndLED.ScoreCollection = OwningScores
        If visualScores IsNot Nothing Then
            hash = MixVisualHash(hash, visualScores.Count)
            For Each score As ReelAndLED.ScoreInfo In visualScores
                If score Is Nothing Then Continue For
                hash = MixVisualHash(hash, score.ID)
                hash = MixVisualHash(hash, score.ZOrder)
                hash = MixVisualHash(hash, score.LayerOrder)
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
                hash = MixVisualHash(hash, score.Reel3DStyle)
                hash = MixVisualHash(hash, score.Reel3DCurve)
                hash = MixVisualHash(hash, score.Reel3DGlassStyle)
                hash = MixVisualHash(hash, score.Reel3DShadow)
                hash = MixVisualHash(hash, If(score.Reel3DDividers, score.Reel3DDividerWidth, 0))
                If score.BehindGlass IsNot Nothing Then
                    hash = MixVisualHash(hash, (score.BehindGlass.ReelKey() & "|" & score.BehindGlass.OverlayKey()).GetHashCode())
                End If
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


    Private Function OrderedVisualItems() As Generic.IList(Of Object)
        If Backglass.currentData IsNot Nothing Then Backglass.currentData.EnsureUnifiedLayerDeck()
        Dim items As New Generic.List(Of Object)()
        Dim visualBulbs As Illumination.BulbCollection = OwningBulbs
        Dim visualScores As ReelAndLED.ScoreCollection = OwningScores
        If visualBulbs IsNot Nothing Then
            For Each bulb As Illumination.BulbInfo In visualBulbs
                items.Add(bulb)
            Next
        End If
        If visualScores IsNot Nothing Then
            For Each score As ReelAndLED.ScoreInfo In visualScores
                items.Add(score)
            Next
        End If
        items.Sort(Function(left As Object, right As Object)
                       Dim leftOrder As Integer = If(TypeOf left Is Illumination.BulbInfo,
                                                     DirectCast(left, Illumination.BulbInfo).LayerOrder,
                                                     DirectCast(left, ReelAndLED.ScoreInfo).LayerOrder)
                       Dim rightOrder As Integer = If(TypeOf right Is Illumination.BulbInfo,
                                                      DirectCast(right, Illumination.BulbInfo).LayerOrder,
                                                      DirectCast(right, ReelAndLED.ScoreInfo).LayerOrder)
                       Return rightOrder.CompareTo(leftOrder)
                   End Function)
        Return items
    End Function

    ' Return the complete editor footprint of a bulb.  A normal light/flasher is
    ' rendered beyond its blue selection box when GlowSpread is non-zero, so a
    ' drag repaint based only on Location/Size leaves the old outer glow behind.
    Friend Function BulbEditorBounds(ByVal bulb As Illumination.BulbInfo,
                                     ByVal displayScale As Single) As Rectangle
        If bulb IsNot Nothing AndAlso Not bulb.IsImageSnippit Then
            Return LightRotationEditorBounds(bulb, displayScale, bulb.LightRotationAngle)
        End If
        Return BulbEditorBoundsAtAngle(bulb, displayScale,
                                       If(bulb Is Nothing, 0.0F, bulb.LightRotationAngle))
    End Function

    Private Function BulbEditorBoundsAtAngle(ByVal bulb As Illumination.BulbInfo,
                                             ByVal displayScale As Single,
                                             ByVal rotationAngle As Single) As Rectangle
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
        nativeRect = Illumination.Create.RotatedLightBounds(nativeRect, rotationAngle)
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

    ' Include the complete temporary rotation footprint. During a light/flasher
    ' drag the saved angle is intentionally unchanged until mouse-up, so repaint
    ' bounds must use the pending angle or the old stem and handle remain onscreen.
    Friend Function LightRotationEditorBounds(ByVal bulb As Illumination.BulbInfo,
                                              ByVal displayScale As Single,
                                              ByVal angle As Single) As Rectangle
        If bulb Is Nothing OrElse displayScale <= 0.0F Then Return Rectangle.Empty

        Dim bounds As Rectangle = BulbEditorBoundsAtAngle(bulb, displayScale, angle)
        Dim selectionRect As New Rectangle(CInt(bulb.Location.X * displayScale),
                                           CInt(bulb.Location.Y * displayScale),
                                           Math.Max(1, CInt(bulb.Size.Width * displayScale)),
                                           Math.Max(1, CInt(bulb.Size.Height * displayScale)))
        Dim centerX As Single = selectionRect.X + selectionRect.Width / 2.0F
        Dim centerY As Single = selectionRect.Y + selectionRect.Height / 2.0F
        Dim handleRadius As Single = selectionRect.Height / 2.0F + 24.0F
        Dim radians As Double = angle * Math.PI / 180.0R
        Dim handleX As Single = centerX + CSng(Math.Sin(radians) * handleRadius)
        Dim handleY As Single = centerY - CSng(Math.Cos(radians) * handleRadius)
        Dim handleBounds As Rectangle = Rectangle.FromLTRB(
            CInt(Math.Floor(Math.Min(centerX, handleX))) - 10,
            CInt(Math.Floor(Math.Min(centerY, handleY))) - 10,
            CInt(Math.Ceiling(Math.Max(centerX, handleX))) + 11,
            CInt(Math.Ceiling(Math.Max(centerY, handleY))) + 11)
        Return Rectangle.Union(bounds, handleBounds)
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
                                Dim isText As Boolean = (Not isSnippet AndAlso Not isFlasher AndAlso Not String.IsNullOrEmpty(.Text))
                                Dim objectFrameColor As Color = If(isSnippet, Color.Red, If(isFlasher, Color.RoyalBlue, If(isBlinker, Color.DarkOrange, If(isText, Color.LimeGreen, Color.Gold))))
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
                                Dim typeText As String = If(isSnippet, "S", If(isFlasher, "F", If(isBlinker, "B", If(isText, "T", "L"))))
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
                                    Dim shape As Illumination.TextShapeInfo = .TextShape()
                                    ' TextRenderer ignores the rotation set for a turned frame, so
                                    ' turned (or shaped) text is drawn as a GDI+ outline instead.
                                    If shape.IsFlat AndAlso lightFrameState Is Nothing Then
                                        Dim horizontal As TextFormatFlags = If(.TextAlignment = Illumination.eTextAlignment.Left, TextFormatFlags.Left, If(.TextAlignment = Illumination.eTextAlignment.Right, TextFormatFlags.Right, TextFormatFlags.HorizontalCenter))
                                        TextRenderer.DrawText(pe.Graphics, .Text, font, rect, framecolor, TextFormatFlags.VerticalCenter Or horizontal Or TextFormatFlags.NoPrefix)
                                    Else
                                        ' Same bent/perspective outline the exported light image uses.
                                        Dim textState As Drawing2D.GraphicsState = pe.Graphics.Save()
                                        pe.Graphics.TranslateTransform(rect.X, rect.Y)
                                        pe.Graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
                                        Using shaped As Drawing2D.GraphicsPath = Illumination.Create.CreateShapedTextPath(pe.Graphics, .Text, font, .TextAlignment, New Rectangle(0, 0, rect.Width, rect.Height), shape)
                                            Using textBrush As New SolidBrush(framecolor)
                                                pe.Graphics.FillPath(textBrush, shaped)
                                            End Using
                                        End Using
                                        pe.Graphics.Restore(textState)
                                    End If
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
            ' Text shape handles, the same as a score window: corner dots for
            ' the height at each end, the center dot for depth, plus a bend dot.
            If selectedLight IsNot Nothing AndAlso Mouse.SelectedItems.Count = 1 AndAlso IsShapeableText(selectedLight) AndAlso
               Not Mouse.IsLightRotationActive Then
                Dim shapeHandles As PointF() = TextShapeHandlePoints(selectedLight, CSng(factor))
                pe.Graphics.DrawPolygon(Pens.DeepSkyBlue, New PointF() {shapeHandles(0), shapeHandles(1), shapeHandles(2), shapeHandles(3)})
                pe.Graphics.FillEllipse(Brushes.White, shapeHandles(0).X - 6, shapeHandles(0).Y - 6, 12, 12)
                pe.Graphics.DrawEllipse(Pens.LimeGreen, shapeHandles(0).X - 6, shapeHandles(0).Y - 6, 12, 12)
                pe.Graphics.DrawLine(Pens.LimeGreen, shapeHandles(0).X, shapeHandles(0).Y - 3, shapeHandles(0).X, shapeHandles(0).Y + 3)
                pe.Graphics.FillEllipse(Brushes.White, shapeHandles(1).X - 6, shapeHandles(1).Y - 6, 12, 12)
                pe.Graphics.DrawEllipse(Pens.DeepSkyBlue, shapeHandles(1).X - 6, shapeHandles(1).Y - 6, 12, 12)
                pe.Graphics.DrawLine(Pens.DeepSkyBlue, shapeHandles(1).X, shapeHandles(1).Y - 3, shapeHandles(1).X, shapeHandles(1).Y + 3)
                pe.Graphics.FillEllipse(Brushes.White, shapeHandles(4).X - 7, shapeHandles(4).Y - 7, 14, 14)
                pe.Graphics.DrawEllipse(Pens.DeepSkyBlue, shapeHandles(4).X - 7, shapeHandles(4).Y - 7, 14, 14)
                pe.Graphics.DrawLine(Pens.DeepSkyBlue, shapeHandles(4).X - 4, shapeHandles(4).Y, shapeHandles(4).X + 4, shapeHandles(4).Y)
                pe.Graphics.FillEllipse(Brushes.White, shapeHandles(5).X - 6, shapeHandles(5).Y - 6, 12, 12)
                pe.Graphics.DrawEllipse(Pens.Orange, shapeHandles(5).X - 6, shapeHandles(5).Y - 6, 12, 12)
                pe.Graphics.DrawArc(Pens.Orange, shapeHandles(5).X - 4, shapeHandles(5).Y - 2, 8, 6, 200, 140)
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
            If dragPreviewScores.Count > 0 AndAlso dragPreviewBase.Size = ClientSize Then
                graphics.DrawImageUnscaled(dragPreviewBase, 0, 0)
            Else
                graphics.DrawImage(dragPreviewBase, New Rectangle(Point.Empty, ClientSize))
            End If
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

                    Dim ordered As Generic.IList(Of Object) = OrderedVisualItems()
                    Dim belowRemaining As Integer = If(Backglass.currentData Is Nothing, 0, Backglass.currentData.CanvasLayerPositionFor(IsDMDPictureBox))
                    Dim canvasDrawn As Boolean = False
                    For Each item As Object In ordered
                        If Not canvasDrawn AndAlso belowRemaining <= 0 Then
                            DrawMainBackglassLayer(layerGraphics, New Rectangle(0, 0, nativeWidth, nativeHeight))
                            canvasDrawn = True
                        End If
                        If TypeOf item Is Illumination.BulbInfo Then
                            DrawUnifiedBulbOrSnippetNative(layerGraphics, nextComposite, DirectCast(item, Illumination.BulbInfo))
                        ElseIf TypeOf item Is ReelAndLED.ScoreInfo Then
                            DrawScoreLayerNative(layerGraphics, DirectCast(item, ReelAndLED.ScoreInfo))
                        End If
                        If Not canvasDrawn AndAlso belowRemaining > 0 Then belowRemaining -= 1
                    Next
                    If Not canvasDrawn Then DrawMainBackglassLayer(layerGraphics, New Rectangle(0, 0, nativeWidth, nativeHeight))
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

    ' Export artwork lights against the same lower snippet layers as the editor.
    ' Nothing means the original canvas-only path is sufficient.
    Friend Function CreateLightArtworkBackdrop(ByVal target As Illumination.BulbInfo,
                                                ByVal background As Image) As Bitmap
        If target Is Nothing OrElse background Is Nothing OrElse
           Not target.UsesArtworkPixelRenderer Then Return Nothing
        Dim targetBelowCanvas As Boolean = IsBulbBelowCanvas(target)
        Dim lower As New Generic.List(Of Illumination.BulbInfo)()
        For Each item As Object In OrderedVisualItems()
            If Object.ReferenceEquals(item, target) Then Exit For
            Dim snippet As Illumination.BulbInfo = TryCast(item, Illumination.BulbInfo)
            If snippet IsNot Nothing AndAlso snippet.IsImageSnippit AndAlso snippet.Image IsNot Nothing AndAlso
               Not snippet.SnippitInfo.PhysicsBall AndAlso LayerManager.IsVisible(snippet) AndAlso
               IsBulbBelowCanvas(snippet) = targetBelowCanvas Then lower.Add(snippet)
        Next
        If lower.Count = 0 Then Return Nothing
        Dim result As New Bitmap(background)
        Try
            Using graphics As Graphics = Graphics.FromImage(result)
                graphics.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
                graphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality
                For Each snippet As Illumination.BulbInfo In lower
                    If IsPictureAnimationReferenceFrame(snippet) Then DrawSnippetLayerNative(graphics, snippet)
                Next
            End Using
            Return result
        Catch
            result.Dispose()
            Throw
        End Try
    End Function

    ' Backdrops are identical when the same visible snippet layers sit below a
    ' light.  Lights.DrawImages uses this key to share one native canvas during
    ' a render pass instead of cloning the full backglass once per light.
    Friend Function LightArtworkBackdropKey(ByVal target As Illumination.BulbInfo) As String
        If target Is Nothing OrElse Not target.UsesArtworkPixelRenderer Then Return String.Empty

        Dim key As New Text.StringBuilder()
        Dim targetBelowCanvas As Boolean = IsBulbBelowCanvas(target)
        For Each item As Object In OrderedVisualItems()
            If Object.ReferenceEquals(item, target) Then Exit For
            Dim snippet As Illumination.BulbInfo = TryCast(item, Illumination.BulbInfo)
            If snippet IsNot Nothing AndAlso snippet.IsImageSnippit AndAlso snippet.Image IsNot Nothing AndAlso
               Not snippet.SnippitInfo.PhysicsBall AndAlso LayerManager.IsVisible(snippet) AndAlso
               IsBulbBelowCanvas(snippet) = targetBelowCanvas AndAlso IsPictureAnimationReferenceFrame(snippet) Then
                key.Append(snippet.ID).Append(":"c)
            End If
        Next
        Return key.ToString()
    End Function

    Private Function IsBulbBelowCanvas(ByVal bulb As Illumination.BulbInfo) As Boolean
        If bulb Is Nothing OrElse Backglass.currentData Is Nothing OrElse
           (bulb.ParentForm <> eParentForm.Backglass AndAlso bulb.ParentForm <> eParentForm.DMD) Then Return False

        Backglass.currentData.EnsureUnifiedLayerDeck()
        Dim visualCount As Integer = If(OwningBulbs Is Nothing, 0, OwningBulbs.Count) + If(OwningScores Is Nothing, 0, OwningScores.Count)
        Dim firstBelow As Integer = visualCount - Backglass.currentData.CanvasLayerPositionFor(bulb.ParentForm = eParentForm.DMD)
        Return bulb.LayerOrder >= Math.Max(0, firstBelow)
    End Function

    ' Group-aware version used by Light Settings.  The off image excludes every
    ' selected lamp and the on image forces every selected lamp through the same
    ' native compositor used by the designer canvas.
    Public Function CreateLayeredPreviewImageForBulbs(ByVal excludedBulbs As Generic.IEnumerable(Of Illumination.BulbInfo),
                                                       ByVal forceVisibleBulbs As Generic.IEnumerable(Of Illumination.BulbInfo),
                                                       Optional ByVal ignoreCurrentFilter As Boolean = False) As Bitmap
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

                Dim orderedItems As Object() = OrderedVisualItems().ToArray()
                Dim belowCount As Integer = If(Backglass.currentData Is Nothing, 0, Backglass.currentData.CanvasLayerPositionFor(IsDMDPictureBox))
                Dim belowItems As New Generic.List(Of Object)()
                Dim frontItems As New Generic.List(Of Object)()
                For Each item As Object In orderedItems
                    If belowCount > 0 Then
                        belowItems.Add(item)
                        belowCount -= 1
                    Else
                        frontItems.Add(item)
                    End If
                Next

                DrawPreviewDeckSegment(layerGraphics, result, belowItems, excluded, forced, ignoreCurrentFilter)
                DrawMainBackglassLayer(layerGraphics, New Rectangle(0, 0, nativeWidth, nativeHeight))
                DrawPreviewDeckSegment(layerGraphics, result, frontItems, excluded, forced, ignoreCurrentFilter)
            End Using
            Return result
        Catch
            result.Dispose()
            Return Nothing
        End Try
    End Function

    Private Sub DrawPreviewDeckSegment(ByVal graphics As Graphics,
                                       ByVal composite As Bitmap,
                                       ByVal items As Generic.IEnumerable(Of Object),
                                       ByVal excluded As Generic.HashSet(Of Illumination.BulbInfo),
                                       ByVal forced As Generic.HashSet(Of Illumination.BulbInfo),
                                       ByVal ignoreCurrentFilter As Boolean)
        ' Each side of the canvas uses the exact normal front-light two-pass
        ' renderer. The canvas is only the fixed card between the two segments.
        For Each item As Object In items
            Dim previewLight As Illumination.BulbInfo = TryCast(item, Illumination.BulbInfo)
            If previewLight Is Nothing OrElse excluded.Contains(previewLight) OrElse
               Not previewLight.DynamicFrontLighting OrElse Not previewLight.DynamicFrontShowLight OrElse
               previewLight.SeparateLightLayer OrElse
               Not PreviewBulbIsDrawable(previewLight, forced.Contains(previewLight), ignoreCurrentFilter) Then Continue For
            DrawBulbLayerOnCurrentComposite(graphics, composite, previewLight)
        Next

        For Each item As Object In items
            If TypeOf item Is Illumination.BulbInfo Then
                Dim itemBulb As Illumination.BulbInfo = DirectCast(item, Illumination.BulbInfo)
                If excluded.Contains(itemBulb) Then Continue For
                DrawUnifiedBulbOrSnippetNative(graphics, composite, itemBulb, forced.Contains(itemBulb), ignoreCurrentFilter)
            ElseIf TypeOf item Is ReelAndLED.ScoreInfo Then
                DrawScoreLayerNative(graphics, DirectCast(item, ReelAndLED.ScoreInfo))
            End If
        Next
    End Sub

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

    ' True for a single plain text box: the only object with text-shape handles.
    Friend Shared Function IsShapeableText(ByVal bulb As Illumination.BulbInfo) As Boolean
        Return bulb IsNot Nothing AndAlso Not bulb.IsImageSnippit AndAlso Not String.IsNullOrEmpty(bulb.Text)
    End Function

    ' Screen positions of a text box's shape handles: 0-3 the perspective
    ' outline (top-left, top-right, bottom-right, bottom-left), 4 the depth
    ' handle in the center, 5 the bend handle (bottom middle, moves with bend).
    ' Turned with the text box's rotation, like its light image.
    Friend Function TextShapeHandlePoints(ByVal bulb As Illumination.BulbInfo, ByVal scale As Single) As PointF()
        Dim box As New RectangleF(bulb.Location.X * scale, bulb.Location.Y * scale, bulb.Size.Width * scale, bulb.Size.Height * scale)
        Dim shape As Illumination.TextShapeInfo = bulb.TextShape()
        Dim corners As PointF() = shape.Corners(box)
        Dim centerX As Single = box.X + box.Width / 2.0F
        Dim centerY As Single = box.Y + box.Height / 2.0F
        Dim points As PointF() = {corners(0), corners(1), corners(2), corners(3),
                                  New PointF(centerX, centerY),
                                  New PointF(centerX, box.Bottom - shape.Bend * box.Height)}
        If Math.Abs(bulb.LightRotationAngle) > 0.001F Then
            Dim radians As Double = bulb.LightRotationAngle * Math.PI / 180.0
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
        ' A flat or rotated rectangle is one affine image draw in both the live
        ' drag preview and the completed canvas. Reserve the strip renderer for
        ' a true perspective trapezoid so releasing the mouse cannot change the
        ' reel's proportions.
        If Math.Abs(quad(0).X + quad(2).X - quad(1).X - quad(3).X) < 0.01F AndAlso
           Math.Abs(quad(0).Y + quad(2).Y - quad(1).Y - quad(3).Y) < 0.01F Then
            graphics.DrawImage(bitmap, New PointF() {quad(0), quad(1), quad(3)},
                               New RectangleF(0, 0, bitmap.Width, bitmap.Height), GraphicsUnit.Pixel)
            Return
        End If

        Dim strips As Integer = Math.Max(24, Math.Min(160, bitmap.Width))
        If dragPreviewActive AndAlso dragPreviewScores.Count > 0 Then
            strips = 24
        End If
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

    ' Realistic 3D reel style: the preview number (0) on its drum with 1 above
    ' and 9 below, lit like the exported reel images. Nothing = draw Classic.
    Private Function CreateRealisticReelDigit(ByVal score As ReelAndLED.ScoreInfo, ByVal size As Size) As Bitmap
        If score Is Nothing OrElse Not score.Reel3DEnabled OrElse
           (score.Reel3DStyle <> ReelRealistic.StyleRealistic AndAlso score.Reel3DStyle <> ReelBehindGlass.Style) Then Return Nothing
        Dim images As Image() = CreateReelNumberSet(score, Function(image, intensity, location) Lights.DrawIlluminatedReelImage(image, intensity, location), {8, 9, 0, 1, 2})
        If images Is Nothing Then Return Nothing
        Try
            Return RenderRealisticReelDigit(score, images, size, 0, 0.0)
        Finally
            DisposeReelNumberSet(images)
        End Try
    End Function

    ' The numbers 0 to 9 of the score's reel set (only those asked for), lit
    ' like the exported reel images. Nothing when the set has no separate
    ' numbers or no 0. Dispose with DisposeReelNumberSet.
    Friend Shared Function CreateReelNumberSet(ByVal score As ReelAndLED.ScoreInfo,
                                               ByVal illuminate As Func(Of Image, Integer, eReelIlluminationLocation, Image),
                                               Optional ByVal digits As Integer() = Nothing) As Image()
        If score Is Nothing OrElse illuminate Is Nothing Then Return Nothing
        If digits Is Nothing Then digits = {0, 1, 2, 3, 4, 5, 6, 7, 8, 9}
        Dim images(9) As Image
        For Each digit As Integer In digits
            Dim raw As Image = GetReelDigitImage(score.ReelType, digit)
            If raw Is Nothing Then Continue For
            images(digit) = New Bitmap(raw)
            If score.ReelIlluLocation <> eReelIlluminationLocation.Off AndAlso score.ReelIlluIntensity > 0 Then
                Dim illuminated As Image = illuminate(images(digit), score.ReelIlluIntensity, score.ReelIlluLocation)
                If illuminated IsNot Nothing AndAlso Not Object.ReferenceEquals(illuminated, images(digit)) Then
                    images(digit).Dispose()
                    images(digit) = illuminated
                End If
            End If
        Next
        If images(0) Is Nothing Then
            DisposeReelNumberSet(images)
            Return Nothing
        End If
        Return images
    End Function

    Friend Shared Sub DisposeReelNumberSet(ByVal images As Image())
        If images Is Nothing Then Return
        For Each image As Image In images
            If image IsNot Nothing Then image.Dispose()
        Next
    End Sub

    ' One Realistic reel showing digit, turned roll (-1 to 1) toward the next
    ' number, exactly as the Server and plugin draw it.
    Friend Shared Function RenderRealisticReelDigit(ByVal score As ReelAndLED.ScoreInfo, ByVal numberSet As Image(),
                                                    ByVal size As Size, ByVal digit As Integer, ByVal roll As Double) As Bitmap
        If numberSet Is Nothing OrElse digit < 0 OrElse digit > 9 OrElse numberSet(digit) Is Nothing Then Return Nothing
        Dim numbers(4) As Image
        For offset As Integer = -2 To 2
            numbers(offset + 2) = numberSet((digit + offset + 10) Mod 10)
        Next
        Dim glass As ReelBehindGlass.Settings = If(score.BehindGlass, ReelBehindGlass.Settings.Neutral())
        If score.Reel3DStyle = ReelBehindGlass.Style Then
            ' Behind glass: the Reel Glass Lab drawing, as the Server draws it,
            ' with the reel's Backlight brightness and Colour temperature too.
            Dim behind As Bitmap = ReelBehindGlass.RenderReel(numbers, size, BehindGlassNumberWidth(score, size), glass, roll, True,
                                                               Math.Max(0, Math.Min(400, score.Reel3DBrightness)) / 100.0)
            ReelAndLED.Reel3DEffect.GradeTemperature(behind, score.Reel3DTemperature)
            Return behind
        End If
        Dim realistic As Bitmap = ReelAndLED.Reel3DEffect.RenderRealisticReel(numbers, size,
                                                                            KeepDigitAspect(score), If(KeepDigitAspect(score), score.ReelDigitWidth, 0),
                                                                            score.Reel3DBrightness, score.Reel3DTemperature, score.Reel3DDepth,
                                                                            score.Reel3DCurve, score.Reel3DShadow, roll, NumberScale(glass))
        ReelBehindGlass.FinishReel(realistic, glass)
        Return realistic
    End Function

    ' Classic 3D reel: the original look, then the reel glass settings
    ' (brightness, warmth, softness), exactly as the Server draws it.
    Friend Shared Function RenderClassicReel(ByVal score As ReelAndLED.ScoreInfo, ByVal reelImage As Image) As Bitmap
        Dim enhanced As Bitmap = ReelAndLED.Reel3DEffect.RenderReel(reelImage, score.Reel3DBrightness, score.Reel3DTemperature, score.Reel3DDepth)
        ReelBehindGlass.FinishReel(enhanced, score.BehindGlass)
        Return enhanced
    End Function

    ' Number size for the Realistic drum (100 % = unchanged).
    Friend Shared Function NumberScale(ByVal glass As ReelBehindGlass.Settings) As Double
        If glass Is Nothing Then Return 1.0
        Return Math.Max(50, Math.Min(150, glass.NumberSize)) / 100.0
    End Function

    ' Behind glass: the number's width before Number size, as in the lab and
    ' the Server: the score's digit width, or 0.56 of the reel height.
    Friend Shared Function BehindGlassNumberWidth(ByVal score As ReelAndLED.ScoreInfo, ByVal size As Size) As Double
        If KeepDigitAspect(score) AndAlso score.ReelDigitWidth > 0 Then Return score.ReelDigitWidth
        Return CInt(size.Height * 0.56)
    End Function

    ' The window drawn over the reels, the same for every reel style: the
    ' original inset shadow and glass, then the reel glass window (shadow,
    ' set back and room reflection) when it is in use.
    Friend Shared Sub DrawScoreWindowOverlay(ByVal graphics As Graphics, ByVal window As Rectangle, ByVal score As ReelAndLED.ScoreInfo)
        ReelAndLED.Reel3DEffect.DrawWindowOverlay(graphics, window, score.Reel3DDepth, score.Reel3DGlass, score.Reel3DGlassStyle)
        If score.BehindGlass IsNot Nothing AndAlso Not score.BehindGlass.OverlayIsNeutral() Then
            ReelBehindGlass.DrawOverlay(graphics, window, score.BehindGlass)
        End If
    End Sub

    Private Function CreateScoreContentBitmap(ByVal score As ReelAndLED.ScoreInfo,
                                              ByRef glassOverlay As Bitmap) As Bitmap
        glassOverlay = Nothing
        If score Is Nothing OrElse score.Digits <= 0 Then Return Nothing
        Dim rect As New Rectangle(score.Location, score.Size)
        If rect.Width <= 1 OrElse rect.Height <= 1 Then Return Nothing

        Dim width As Single = ((rect.Width - score.Spacing / 2.0F * (score.Digits - 1)) / score.Digits)
        Do While width * score.Digits > rect.Width - 1 AndAlso width > 1
            width -= 1
        Loop
        Dim height As Integer = rect.Height - 1
        If score.PerfectScaleWidthFix Then height = CInt(My.Resources.LED_0.Height / My.Resources.LED_0.Width * width)
        Dim newSize As New Size(Math.Max(1, CInt(width)), Math.Max(1, height))
        Dim reelImage As Image = ResizeScoreReel(GetReelImage(score.ReelType, score.ReelColor, Backglass.currentData.UseDream7LEDs, Backglass.currentData.D7Thickness, Backglass.currentData.D7Shear, Backglass.currentData.D7Glow, newSize), newSize, score)
        If reelImage Is Nothing Then Return Nothing
        Dim scoreBitmap As Bitmap = Nothing
        Try
            If score.ReelIlluLocation <> eReelIlluminationLocation.Off AndAlso score.ReelIlluIntensity > 0 Then
                Dim illuminated As Image = Lights.DrawIlluminatedReelImage(reelImage, score.ReelIlluIntensity, score.ReelIlluLocation)
                If illuminated IsNot Nothing AndAlso Not Object.ReferenceEquals(illuminated, reelImage) Then
                    reelImage.Dispose()
                    reelImage = illuminated
                End If
            End If
            If score.Reel3DEnabled Then
                Dim enhanced As Bitmap = CreateRealisticReelDigit(score, newSize)
                If enhanced Is Nothing Then enhanced = RenderClassicReel(score, reelImage)
                If enhanced IsNot Nothing Then
                    reelImage.Dispose()
                    reelImage = enhanced
                End If
            End If
            scoreBitmap = New Bitmap(Math.Max(2, rect.Width), Math.Max(2, rect.Height), Imaging.PixelFormat.Format32bppArgb)
            Using sg As Graphics = Graphics.FromImage(scoreBitmap)
                sg.Clear(Color.Transparent)
                sg.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
                Dim x As Single = 1.0F
                For i As Integer = 1 To score.Digits
                    sg.DrawImage(reelImage, New PointF(x, 1.0F))
                    x += width + score.Spacing / 2.0F
                Next
            End Using
            If score.Reel3DEnabled Then
                glassOverlay = New Bitmap(scoreBitmap.Width, scoreBitmap.Height, Imaging.PixelFormat.Format32bppArgb)
                Using overlayGraphics As Graphics = Graphics.FromImage(glassOverlay)
                    overlayGraphics.Clear(Color.Transparent)
                    DrawScoreDividers(overlayGraphics, New Rectangle(0, 0, glassOverlay.Width, glassOverlay.Height), score, width)
                    DrawScoreWindowOverlay(overlayGraphics, New Rectangle(0, 0, glassOverlay.Width, glassOverlay.Height), score)
                End Using
            End If
            Return scoreBitmap
        Catch
            If scoreBitmap IsNot Nothing Then scoreBitmap.Dispose()
            If glassOverlay IsNot Nothing Then
                glassOverlay.Dispose()
                glassOverlay = Nothing
            End If
            Throw
        Finally
            reelImage.Dispose()
        End Try
    End Function

    ' Reel dividers on the score's window overlay, at the boundaries of the digits
    ' as this preview lays them out (x from 1, step width + Spacing / 2).
    Private Shared Sub DrawScoreDividers(ByVal graphics As Graphics, ByVal window As Rectangle,
                                         ByVal score As ReelAndLED.ScoreInfo, ByVal width As Single)
        If score Is Nothing OrElse Not score.Reel3DDividers OrElse score.Digits < 2 Then Return
        Dim lefts(score.Digits - 1) As Double
        Dim x As Single = 1.0F
        For i As Integer = 0 To score.Digits - 1
            lefts(i) = x
            x += width + score.Spacing / 2.0F
        Next
        ReelDivider.Draw(graphics, window, ReelDivider.Centers(lefts, width), score.Reel3DDividerWidth)
    End Sub

    Private Sub DrawScoreDragPreview(ByVal graphics As Graphics,
                                     ByVal preview As ScoreDragPreview,
                                     ByVal scale As Single)
        If graphics Is Nothing OrElse preview Is Nothing OrElse preview.Score Is Nothing OrElse
           preview.Content Is Nothing OrElse scale <= 0.0F Then Return
        Dim score As ReelAndLED.ScoreInfo = preview.Score
        If preview.RenderedSize <> score.Size Then
            preview.Dispose()
            preview.Content = CreateScoreContentBitmap(score, preview.GlassOverlay)
            preview.RenderedSize = score.Size
            If preview.Content Is Nothing Then Return
        End If
        If Not LayerManager.IsVisible(score) OrElse Not (ShowScoreFrames OrElse ShowScoring) OrElse
           score.DisplayState <> eScoreDisplayState.Visible OrElse score.Digits <= 0 Then Return
        Dim rect As New Rectangle(score.Location, score.Size)
        If rect.Width <= 1 OrElse rect.Height <= 1 Then Return

        Dim perspectivePoints As PointF() = ScorePerspectivePoints(score, scale)
        DrawScoreDragPreviewBitmap(graphics, preview.Content, perspectivePoints)
        If preview.GlassOverlay IsNot Nothing Then DrawScoreDragPreviewBitmap(graphics, preview.GlassOverlay, perspectivePoints)
    End Sub
    Private Sub DrawScoreDragPreviewBitmap(ByVal graphics As Graphics,
                                           ByVal bitmap As Bitmap,
                                           ByVal quad As PointF())
        If graphics Is Nothing OrElse bitmap Is Nothing OrElse quad Is Nothing OrElse quad.Length <> 4 Then Return

        ' A rectangle, rotation or uniform skew is one affine transform. Draw it
        ' once while dragging instead of passing the same transform through 160
        ' perspective strips. True trapezoidal perspective keeps the strip path.
        Dim expectedBottomRight As New PointF(quad(1).X + quad(3).X - quad(0).X,
                                              quad(1).Y + quad(3).Y - quad(0).Y)
        If Math.Abs(expectedBottomRight.X - quad(2).X) < 0.01F AndAlso
           Math.Abs(expectedBottomRight.Y - quad(2).Y) < 0.01F Then
            Dim oldMode As Drawing2D.InterpolationMode = graphics.InterpolationMode
            graphics.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
            Try
                Dim destination() As PointF = {quad(0), quad(1), quad(3)}
                graphics.DrawImage(bitmap, destination,
                                   New RectangleF(0.0F, 0.0F, bitmap.Width, bitmap.Height),
                                   GraphicsUnit.Pixel)
            Finally
                graphics.InterpolationMode = oldMode
            End Try
        Else
            DrawPerspectiveBitmap(graphics, bitmap, quad)
        End If
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
        Dim reelImage As Image = ResizeScoreReel(GetReelImage(score.ReelType, score.ReelColor, Backglass.currentData.UseDream7LEDs, Backglass.currentData.D7Thickness, Backglass.currentData.D7Shear, Backglass.currentData.D7Glow, newSize), newSize, score)
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
                Dim enhanced As Bitmap = CreateRealisticReelDigit(score, newSize)
                If enhanced Is Nothing Then enhanced = RenderClassicReel(score, reelImage)
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
                DrawPerspectiveBitmap(graphics, scoreBitmap, ScorePerspectivePoints(score, 1.0F))
                If score.Reel3DEnabled Then
                    Using glassOverlay As New Bitmap(scoreBitmap.Width, scoreBitmap.Height, Imaging.PixelFormat.Format32bppArgb)
                        Using overlayGraphics As Graphics = Graphics.FromImage(glassOverlay)
                            overlayGraphics.Clear(Color.Transparent)
                            DrawScoreDividers(overlayGraphics, New Rectangle(0, 0, glassOverlay.Width, glassOverlay.Height), score, width)
                            DrawScoreWindowOverlay(overlayGraphics, New Rectangle(0, 0, glassOverlay.Width, glassOverlay.Height), score)
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
                                                Optional ByVal forceVisible As Boolean = False,
                                                Optional ByVal ignoreCurrentFilter As Boolean = False)
        If bulb Is Nothing OrElse Not LayerManager.IsVisible(bulb) OrElse
           (Not ignoreCurrentFilter AndAlso Not BulbPassesCurrentFilter(bulb)) Then Return
        If Not IsPictureAnimationReferenceFrame(bulb) Then Return
        If bulb.IsImageSnippit Then
            DrawSnippetLayerNative(graphics, bulb)
        ElseIf (ShowIllumination OrElse forceVisible) AndAlso Not IsExternalIlluminationImageIlluminated Then
            If bulb.DynamicFrontLighting Then
                ' Separate light layers remain visible at their normal Z order.
                ' A non-separated visible base was already drawn below snippets.
                If bulb.SeparateLightLayer Then DrawBulbLayerOnCurrentComposite(graphics, composed, bulb)
                ' A settings-window preview forces the selected light on, and
                ' Accurate Light Preview represents the authored illuminated
                DrawDynamicFrontLightOnSnippets(graphics, composed, bulb, ignoreCurrentFilter)
            Else
                DrawBulbLayerOnCurrentComposite(graphics, composed, bulb)
            End If
        End If
    End Sub

    Private Function PreviewBulbIsDrawable(ByVal bulb As Illumination.BulbInfo,
                                           ByVal forceVisible As Boolean,
                                           ByVal ignoreCurrentFilter As Boolean) As Boolean
        Return bulb IsNot Nothing AndAlso LayerManager.IsVisible(bulb) AndAlso
               (ignoreCurrentFilter OrElse BulbPassesCurrentFilter(bulb)) AndAlso
               IsPictureAnimationReferenceFrame(bulb)
    End Function

    Private Function DynamicFrontTargetsSnippet(ByVal light As Illumination.BulbInfo,
                                                ByVal snippet As Illumination.BulbInfo) As Boolean
        If light Is Nothing OrElse snippet Is Nothing Then Return False
        If light.DynamicFrontTargetSelectionDefined Then
            Return light.DynamicFrontTargetSnippetIDs.Contains(snippet.ID)
        End If
        Return light.DynamicFrontTargetSnippetID <= 0 OrElse
               light.DynamicFrontTargetSnippetID = snippet.ID
    End Function

    Private Sub DrawDynamicFrontLightOnSnippets(ByVal graphics As Graphics,
                                                ByVal composed As Bitmap,
                                                ByVal light As Illumination.BulbInfo,
                                                ByVal ignoreCurrentFilter As Boolean)
        Dim visualBulbs As Illumination.BulbCollection = OwningBulbs
        If graphics Is Nothing OrElse composed Is Nothing OrElse light Is Nothing OrElse
           visualBulbs Is Nothing Then Return

        Dim lightRect As New Rectangle(light.Location, light.Size)
        Dim lightBounds As New Rectangle(light.LocationX, light.SizeX)
        If light.GlowSpread > 0 AndAlso lightRect.Width > 0 AndAlso lightRect.Height > 0 Then
            Dim scaleX As Double = If(lightRect.Width > 0, lightBounds.Width / CDbl(lightRect.Width), 1.0)
            Dim scaleY As Double = If(lightRect.Height > 0, lightBounds.Height / CDbl(lightRect.Height), 1.0)
            lightRect.Inflate(light.GlowSpread, light.GlowSpread)
            lightBounds.Inflate(CInt(Math.Round(light.GlowSpread * scaleX)),
                                CInt(Math.Round(light.GlowSpread * scaleY)))
        End If
        lightBounds = Illumination.Create.RotatedLightBounds(lightBounds, light.LightRotationAngle)
        lightBounds.Intersect(New Rectangle(Point.Empty, composed.Size))
        If lightBounds.Width <= 0 OrElse lightBounds.Height <= 0 Then Return

        Dim renderer As New Illumination.Create()
        Using lightField As Bitmap = renderer.CreateDynamicFrontLightField(
            lightRect, lightBounds,
            light.GlowSoftness, light.GlowFalloff, light.GlowIntensity,
            light.SelectionMaskData, light.SelectionFeather,
            light.GlobalMaskLayerExplicit AndAlso Not light.InFrontOfGlobalMask,
            light.LightDiffusion,
            light.MaskRadius, light.MaskSmooth, light.MaskFeather,
            light.MaskContrast, light.MaskShiftEdge, light.FlasherRadialSpikes,
            light.LightRotationAngle)
            If lightField Is Nothing Then Return

            ' Light the snippets from the bottom layer up (the canvas draws a higher
            ' LayerOrder first), and hide each lit copy wherever a snippet above it
            ' covers it, so a lit lower snippet never shows through the ones in front.
            Dim covers As New Generic.Dictionary(Of Illumination.BulbInfo, Byte())()
            Dim ordered As New Generic.List(Of Illumination.BulbInfo)()
            For Each bulb As Illumination.BulbInfo In visualBulbs
                If bulb IsNot Nothing Then ordered.Add(bulb)
            Next
            ordered = ordered.OrderByDescending(Function(bulb) bulb.LayerOrder).ToList()

            For Each snippet As Illumination.BulbInfo In ordered
                If snippet Is Nothing OrElse Not snippet.IsImageSnippit OrElse snippet.Image Is Nothing OrElse
                   snippet.ZOrder >= light.ZOrder OrElse
                   Not PreviewBulbIsDrawable(snippet, False, ignoreCurrentFilter) OrElse
                   Not DynamicFrontTargetsSnippet(light, snippet) Then Continue For

                Dim snippetBounds As New Rectangle(snippet.Location, snippet.Size)
                Dim overlap As Rectangle = Rectangle.Intersect(lightBounds, snippetBounds)
                If overlap.Width <= 0 OrElse overlap.Height <= 0 OrElse
                   snippetBounds.Width <= 0 OrElse snippetBounds.Height <= 0 Then Continue For

                Using snippetFrame As New Bitmap(snippetBounds.Width, snippetBounds.Height, PixelFormat.Format32bppArgb)
                    Using snippetGraphics As Graphics = Graphics.FromImage(snippetFrame)
                        snippetGraphics.Clear(Color.Transparent)
                        snippetGraphics.SmoothingMode = Drawing2D.SmoothingMode.HighQuality
                        snippetGraphics.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
                        snippetGraphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality
                        snippetGraphics.TranslateTransform(-snippetBounds.X, -snippetBounds.Y)
                        DrawSnippetLayerNative(snippetGraphics, snippet)
                    End Using

                    Dim sourceRect As New Rectangle(overlap.X - snippetBounds.X,
                                                    overlap.Y - snippetBounds.Y,
                                                    overlap.Width, overlap.Height)
                    Using sourceCrop As Bitmap = snippetFrame.Clone(sourceRect, PixelFormat.Format32bppArgb)
                        Using fieldCrop As New Bitmap(overlap.Width, overlap.Height, PixelFormat.Format32bppArgb)
                            Using fieldGraphics As Graphics = Graphics.FromImage(fieldCrop)
                                fieldGraphics.Clear(Color.Transparent)
                                fieldGraphics.DrawImage(lightField,
                                                        New Rectangle(Point.Empty, fieldCrop.Size),
                                                        New RectangleF((overlap.X - lightBounds.X) * lightField.Width / CSng(lightBounds.Width),
                                                                       (overlap.Y - lightBounds.Y) * lightField.Height / CSng(lightBounds.Height),
                                                                       overlap.Width * lightField.Width / CSng(lightBounds.Width),
                                                                       overlap.Height * lightField.Height / CSng(lightBounds.Height)),
                                                        GraphicsUnit.Pixel)
                            End Using

                            Using illuminated As Bitmap = Illumination.ArtworkFlasherRenderer.RenderFromAlphaField(
                                sourceCrop, fieldCrop, light.GlowIntensity, light.LightColor,
                                light.FlasherStyle, light.FlasherSaturation,
                                light.FlasherHighlightProtection, light.FlasherDarkAreaLift,
                                light.ArtworkContrast)
                                If illuminated IsNot Nothing Then
                                    Illumination.Create.ApplyTemperatureToRenderedLight(illuminated, light.LightTemperature)
                                    HideCoveredDynamicLight(illuminated, overlap, snippet, light, ordered, covers, ignoreCurrentFilter)
                                    graphics.DrawImageUnscaled(illuminated, overlap.Location)
                                End If
                            End Using
                        End Using
                    End Using
                End Using
            Next
        End Using
    End Sub

    ' Multiplies the lit copy's alpha by what is left uncovered by the image
    ' snippets drawn after this snippet and before the light (lower LayerOrder
    ' than the snippet, higher than the light). covers keeps each snippet's
    ' alpha for the rest of this light's pass.
    Private Sub HideCoveredDynamicLight(ByVal illuminated As Bitmap, ByVal overlap As Rectangle,
                                        ByVal snippet As Illumination.BulbInfo, ByVal light As Illumination.BulbInfo,
                                        ByVal ordered As Generic.List(Of Illumination.BulbInfo),
                                        ByVal covers As Generic.Dictionary(Of Illumination.BulbInfo, Byte()),
                                        ByVal ignoreCurrentFilter As Boolean)
        Dim pixels As Byte() = Nothing
        Dim stride As Integer = 0
        Dim data As Imaging.BitmapData = Nothing
        Try
            For Each above As Illumination.BulbInfo In ordered
                If above Is snippet OrElse Not above.IsImageSnippit OrElse above.Image Is Nothing OrElse
                   above.LayerOrder >= snippet.LayerOrder OrElse above.LayerOrder <= light.LayerOrder OrElse
                   Not PreviewBulbIsDrawable(above, False, ignoreCurrentFilter) Then Continue For
                Dim aboveBounds As New Rectangle(above.Location, above.Size)
                If aboveBounds.Width <= 0 OrElse aboveBounds.Height <= 0 Then Continue For
                Dim common As Rectangle = Rectangle.Intersect(overlap, aboveBounds)
                If common.Width <= 0 OrElse common.Height <= 0 Then Continue For

                Dim alpha As Byte() = Nothing
                If Not covers.TryGetValue(above, alpha) Then
                    alpha = ReadSnippetAlpha(above, aboveBounds)
                    covers(above) = alpha
                End If
                If alpha Is Nothing Then Continue For

                If pixels Is Nothing Then
                    data = illuminated.LockBits(New Rectangle(0, 0, illuminated.Width, illuminated.Height),
                                                Imaging.ImageLockMode.ReadWrite, Imaging.PixelFormat.Format32bppArgb)
                    stride = Math.Abs(data.Stride)
                    pixels = New Byte(stride * illuminated.Height - 1) {}
                    Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, pixels.Length)
                End If
                For y As Integer = common.Top To common.Bottom - 1
                    Dim coverRow As Integer = (y - aboveBounds.Y) * aboveBounds.Width - aboveBounds.X
                    Dim pixelRow As Integer = (y - overlap.Y) * stride
                    For x As Integer = common.Left To common.Right - 1
                        Dim covered As Integer = alpha(coverRow + x)
                        If covered = 0 Then Continue For
                        Dim p As Integer = pixelRow + (x - overlap.X) * 4 + 3
                        pixels(p) = CByte((pixels(p) * (255 - covered) + 127) \ 255)
                    Next
                Next
            Next
            If pixels IsNot Nothing Then Runtime.InteropServices.Marshal.Copy(pixels, 0, data.Scan0, pixels.Length)
        Finally
            If data IsNot Nothing Then illuminated.UnlockBits(data)
        End Try
    End Sub

    ' The snippet's alpha over its own box, drawn exactly as the canvas draws it.
    Private Function ReadSnippetAlpha(ByVal bulb As Illumination.BulbInfo, ByVal bounds As Rectangle) As Byte()
        Using frame As New Bitmap(bounds.Width, bounds.Height, Imaging.PixelFormat.Format32bppArgb)
            Using frameGraphics As Graphics = Graphics.FromImage(frame)
                frameGraphics.Clear(Color.Transparent)
                frameGraphics.SmoothingMode = Drawing2D.SmoothingMode.HighQuality
                frameGraphics.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
                frameGraphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality
                frameGraphics.TranslateTransform(-bounds.X, -bounds.Y)
                DrawSnippetLayerNative(frameGraphics, bulb)
            End Using
            Dim alpha(bounds.Width * bounds.Height - 1) As Byte
            Dim data As Imaging.BitmapData = frame.LockBits(New Rectangle(0, 0, bounds.Width, bounds.Height),
                                                            Imaging.ImageLockMode.ReadOnly, Imaging.PixelFormat.Format32bppArgb)
            Try
                Dim row(bounds.Width * 4 - 1) As Byte
                For y As Integer = 0 To bounds.Height - 1
                    Runtime.InteropServices.Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length)
                    For x As Integer = 0 To bounds.Width - 1
                        alpha(y * bounds.Width + x) = row(x * 4 + 3)
                    Next
                Next
            Finally
                frame.UnlockBits(data)
            End Try
            Return alpha
        End Using
    End Function

    Private Sub DrawSnippetLayerNative(ByVal graphics As Graphics,
                                       ByVal bulb As Illumination.BulbInfo)
        If bulb.Image Is Nothing Then Return
        Dim target As New Rectangle(bulb.Location, bulb.Size)
        If target.Width <= 0 OrElse target.Height <= 0 Then Return
        Dim layerOpacity As Integer = LayerManager.GetOpacity(bulb)
        Dim brightness As Integer = Math.Max(0, Math.Min(200, bulb.SnippitInfo.Brightness))
        Dim clippedSnippet As Bitmap = Nothing
        Dim snippetImage As Image = bulb.Image
        If Not String.IsNullOrEmpty(bulb.SelectionMaskData) Then
            Dim isInfinity As Boolean = Not String.IsNullOrEmpty(bulb.SnippitInfo.InfinityLightsGroup)
            clippedSnippet = Illumination.Lights.CreateSelectionMaskedSnippet(snippetImage, bulb.SelectionMaskData, target, False,
                                                                               If(isInfinity, bulb.MaskRadius, 0),
                                                                               If(isInfinity, bulb.MaskSmooth, 0),
                                                                               If(isInfinity, bulb.MaskFeather, 0),
                                                                               If(isInfinity, bulb.MaskShiftEdge, bulb.SnippitInfo.MaskShiftEdge),
                                                                               If(isInfinity, 0, bulb.SnippitInfo.MaskEdgeSoftness),
                                                                               If(isInfinity, 0, bulb.SnippitInfo.MaskOutwardDiffusion))
            If clippedSnippet IsNot Nothing Then snippetImage = clippedSnippet
        ElseIf bulb.SnippitInfo.MaskShiftEdge <> 0 OrElse bulb.SnippitInfo.MaskEdgeSoftness <> 0 OrElse
               bulb.SnippitInfo.MaskOutwardDiffusion <> 0 Then
            clippedSnippet = Illumination.Lights.CreateAlphaRefinedSnippet(snippetImage,
                                                                           0,
                                                                           0,
                                                                           0,
                                                                           bulb.SnippitInfo.MaskShiftEdge,
                                                                           bulb.SnippitInfo.MaskEdgeSoftness,
                                                                           bulb.SnippitInfo.MaskOutwardDiffusion)
            If clippedSnippet IsNot Nothing Then snippetImage = clippedSnippet
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
        Dim retainOverlay As Boolean = False
        Try
            If Not String.IsNullOrEmpty(bulb.Text) Then
                font = New Font(bulb.FontName, bulb.FontSize, bulb.FontStyle)
            End If
            Dim renderBackground As Bitmap = composed
            Dim signature As Long = BulbVisualSignature(bulb)
            Dim digest As String = BackdropDigest(renderBackground, rectX)
            Dim cached As CachedLightLayer = Nothing
            If lightLayerCache.TryGetValue(bulb.ID, cached) AndAlso cached.Signature = signature AndAlso
               cached.Bounds.Equals(rectX) AndAlso cached.BackdropDigest = digest Then
                graphics.DrawImageUnscaled(cached.Image, rectX.Location)
                Return
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
                                                  bulb.GlobalMaskLayerExplicit AndAlso Not bulb.InFrontOfGlobalMask, bulb.FlasherStyle, bulb.FlasherSaturation, bulb.FlasherHighlightProtection, bulb.FlasherDarkAreaLift, bulb.FlasherHotspotX, bulb.FlasherHotspotY, bulb.LightDiffusion, bulb.LightTemperature, bulb.UsesArtworkPixelRenderer, bulb.ArtworkContrast, bulb.MaskRadius, bulb.MaskSmartRadius, bulb.MaskSmooth, bulb.MaskFeather, bulb.MaskContrast, bulb.MaskShiftEdge, bulb.FlasherRadialSpikes, bulb.LightRotationAngle, bulb.SeparateLightLayer, bulb.TextShape())
            If overlay IsNot Nothing Then graphics.DrawImageUnscaled(overlay, rectX.Location)
            If TypeOf overlay Is Bitmap Then
                Dim size As Long = CLng(overlay.Width) * overlay.Height * 4L
                If size <= MaxLightLayerCacheBytes Then
                    If cached IsNot Nothing Then
                        lightLayerCacheBytes -= CLng(cached.Image.Width) * cached.Image.Height * 4L
                        cached.Image.Dispose()
                        lightLayerCache.Remove(bulb.ID)
                    End If
                    If lightLayerCacheBytes + size > MaxLightLayerCacheBytes Then ClearLightLayerCache()
                    lightLayerCache(bulb.ID) = New CachedLightLayer With {
                        .Signature = signature, .BackdropDigest = digest, .Bounds = rectX, .Image = DirectCast(overlay, Bitmap)}
                    lightLayerCacheBytes += size
                    retainOverlay = True
                End If
            End If
        Finally
            If overlay IsNot Nothing AndAlso Not retainOverlay Then overlay.Dispose()
            If font IsNot Nothing Then font.Dispose()
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

    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        If disposing Then
            If animationtimer IsNot Nothing Then
                animationtimer.Stop()
                RemoveHandler animationtimer.Tick, AddressOf AnimationTimer_Tick
                animationtimer.Dispose()
                animationtimer = Nothing
            End If
            currentAnimationSteps = Nothing
            animationOn.Clear()
            ClearUnifiedCompositeCache()
            ClearLightLayerCache()
            EndDragPreview(False)
            Illumination.Create.ClearRenderCaches()
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
                    End If
                    ' Otherwise the accurate preview is drawn by DrawUnifiedVisualLayers,
                    ' which owns its light cache.
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
                Dim visualScores As ReelAndLED.ScoreCollection = OwningScores
                If visualScores IsNot Nothing Then
                    For Each score As ReelAndLED.ScoreInfo In visualScores
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
                                Dim image As Image = ResizeScoreReel(GetReelImage(.ReelType, Color.FromArgb(0, 0, 0), Backglass.currentData.UseDream7LEDs, Backglass.currentData.D7Thickness, Backglass.currentData.D7Shear, Backglass.currentData.D7Glow, newsize), newsize, score)
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
                End If
            End Using
            ' that's it
            Return newimage
        End Get
    End Property
    Public ReadOnly Property IlluminatedImage() As Image
        Get
            ' Export the same full-quality layer stack shown by Accurate Light
            ' Preview.  Force every light on and ignore the editor's temporary
            ' ROM/name filter, matching the established illuminated-export scope.
            Return CreateLayeredPreviewImageForBulbs(Nothing, OwningBulbs, True)
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

        Dim ret As Image = ExportCanvasImage()
        For Each item As Images.ImageInfo In Backglass.currentData.Images
            If item.Type = Images.eImageInfoType.BackgroundImage AndAlso item.BackgroundImageType = Images.eBackgroundImageType.On Then
                ret = item.Image
                Exit For
            End If
        Next
        Return ret
    End Function

    Private Function ExportCanvasImage() As Image
        ' The shared renderer retains artwork in Me.Image and clears MyBase.Image.
        ' Preserve a selected legacy base image when present; otherwise export
        ' the retained artwork. DMD always uses its own retained canvas.
        Return If(IsDMDPictureBox, Me.Image, If(MyBase.Image, Me.Image))
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
    Private Sub AnimationTimer_Tick(Optional ByVal sender As Object = Nothing,
                                    Optional ByVal e As EventArgs = Nothing)
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
