'=== Phase 2 Unified Artwork Pipeline ===
Imports System
Imports System.Drawing.Imaging
Imports System.Runtime.InteropServices
Imports System.Drawing.Drawing2D
Imports System.IO

Namespace Illumination

    Public Class Create

        Private Const C_OVERLAYS As Integer = 2
        Private Const C_BULBRADIUS As Single = 0.15

        Private doDodge As Boolean = True

        ' 2.7.8: cache the decoded project mask while editing. Export still forces
        ' each bulb image to regenerate, but repeated preview redraws no longer
        ' decode the same Base64 PNG for every light.
        Private Shared cachedGlobalMaskData As String = Nothing
        Private Shared cachedGlobalMask As Bitmap = Nothing
        Private Shared cachedGlobalBinaryKey As String = Nothing
        Private Shared cachedGlobalBinaryWidth As Integer = 0
        Private Shared cachedGlobalBinaryHeight As Integer = 0
        Private Shared cachedGlobalBinary As Byte() = Nothing
        Private Shared ReadOnly globalMaskSync As New Object()

        ' 2.8.8: bounded render caches. Moving a light no longer rebuilds the
        ' same glow geometry, decoded selection mask, and feathered mask on
        ' every mouse-move preview. Cached entries are immutable and cloned
        ' before drawing, so the existing rendering result remains unchanged.
        Private Const C_MAX_GLOW_CACHE As Integer = 48
        Private Const C_MAX_SELECTION_CACHE As Integer = 24
        Private Shared ReadOnly renderCacheSync As New Object()
        Private Shared ReadOnly glowTemplateCache As New Generic.Dictionary(Of String, Bitmap)()
        Private Shared ReadOnly glowTemplateOrder As New Generic.Queue(Of String)()
        Private Shared ReadOnly selectionAlphaCache As New Generic.Dictionary(Of String, Byte())()
        Private Shared ReadOnly selectionAlphaOrder As New Generic.Queue(Of String)()

        Public Shared Sub ClearRenderCaches()
            SyncLock renderCacheSync
                For Each cached As Bitmap In glowTemplateCache.Values
                    cached.Dispose()
                Next
                glowTemplateCache.Clear()
                glowTemplateOrder.Clear()
                selectionAlphaCache.Clear()
                selectionAlphaOrder.Clear()
            End SyncLock
            InvalidateGlobalMaskCache()
        End Sub

        Public Shared Sub InvalidateGlobalMaskCache()
            SyncLock globalMaskSync
                If cachedGlobalMask IsNot Nothing Then cachedGlobalMask.Dispose()
                cachedGlobalMask = Nothing
                cachedGlobalMaskData = Nothing
                cachedGlobalBinaryKey = Nothing
                cachedGlobalBinaryWidth = 0
                cachedGlobalBinaryHeight = 0
                cachedGlobalBinary = Nothing
            End SyncLock
        End Sub

        Private Shared Function GetCachedGlobalMask(ByVal maskData As String) As Bitmap
            SyncLock globalMaskSync
                If cachedGlobalMask Is Nothing OrElse Not String.Equals(cachedGlobalMaskData, maskData, StringComparison.Ordinal) Then
                    If cachedGlobalMask IsNot Nothing Then cachedGlobalMask.Dispose()
                    Dim bytes() As Byte = Convert.FromBase64String(maskData)
                    Using ms As New MemoryStream(bytes)
                        Using temp As New Bitmap(ms)
                            cachedGlobalMask = New Bitmap(temp)
                        End Using
                    End Using
                    cachedGlobalMaskData = maskData
                End If
                Return cachedGlobalMask
            End SyncLock
        End Function

        Private Shared Function GetGlowTemplate(ByVal width As Integer,
                                                      ByVal height As Integer,
                                                      ByVal text As String,
                                                      ByVal font As Font,
                                                      ByVal textalignment As Illumination.eTextAlignment,
                                                      ByVal illumode As Illumination.eIlluMode,
                                                      ByVal glowSoftness As Integer,
                                                      ByVal glowFalloff As Integer,
                                                      ByVal glowIntensity As Integer,
                                                      Optional ByVal lightDiffusion As Integer = 0) As Bitmap
            Dim fontKey As String = If(font Is Nothing, "", font.Name & "|" & font.Size.ToString(Globalization.CultureInfo.InvariantCulture) & "|" & CInt(font.Style).ToString())
            Dim key As String = width.ToString() & "x" & height.ToString() & "|" & text & "|" & fontKey & "|" & CInt(textalignment).ToString() & "|" & CInt(illumode).ToString() & "|" & glowSoftness.ToString() & "|" & glowFalloff.ToString() & "|" & glowIntensity.ToString() & "|" & lightDiffusion.ToString()

            SyncLock renderCacheSync
                Dim cached As Bitmap = Nothing
                If glowTemplateCache.TryGetValue(key, cached) Then Return DirectCast(cached.Clone(), Bitmap)
            End SyncLock

            Dim created As New Bitmap(width, height, PixelFormat.Format32bppArgb)
            Using gr As Graphics = Graphics.FromImage(created)
                gr.Clear(Color.Transparent)
                gr.SmoothingMode = SmoothingMode.HighQuality
                gr.TextRenderingHint = Drawing.Text.TextRenderingHint.AntiAliasGridFit
                Dim renderer As New Create()
                If Not String.IsNullOrEmpty(text) AndAlso font IsNot Nothing Then
                    renderer.DrawLight(gr, New Rectangle(0, 0, width, height), text, font, textalignment, illumode, glowSoftness, glowFalloff, glowIntensity, lightDiffusion)
                Else
                    renderer.DrawLight(gr, New Rectangle(0, 0, width, height), , , , illumode, glowSoftness, glowFalloff, glowIntensity, lightDiffusion)
                End If
            End Using

            SyncLock renderCacheSync
                If width * CLng(height) <= 4000000 AndAlso Not glowTemplateCache.ContainsKey(key) Then
                    While glowTemplateOrder.Count >= C_MAX_GLOW_CACHE
                        Dim oldest As String = glowTemplateOrder.Dequeue()
                        Dim oldBitmap As Bitmap = Nothing
                        If glowTemplateCache.TryGetValue(oldest, oldBitmap) Then
                            glowTemplateCache.Remove(oldest)
                            oldBitmap.Dispose()
                        End If
                    End While
                    glowTemplateCache.Add(key, DirectCast(created.Clone(), Bitmap))
                    glowTemplateOrder.Enqueue(key)
                End If
            End SyncLock
            Return created
        End Function

        Private Shared Function GetSelectionAlpha(ByVal maskData As String, ByVal rectX As Rectangle, ByVal width As Integer, ByVal height As Integer, ByVal feather As Integer) As Byte()
            Dim maskIdentity As String = maskData.Length.ToString() & ":" & maskData.GetHashCode().ToString()
            Dim key As String = maskIdentity & "|" & rectX.X.ToString() & "," & rectX.Y.ToString() & "," & rectX.Width.ToString() & "," & rectX.Height.ToString() & "|" & width.ToString() & "x" & height.ToString() & "|" & feather.ToString()
            SyncLock renderCacheSync
                Dim cached As Byte() = Nothing
                If selectionAlphaCache.TryGetValue(key, cached) Then Return cached
            End SyncLock

            If width <= 0 OrElse height <= 0 Then Return New Byte() {}
            Dim alpha(width * height - 1) As Byte
            Dim bytes() As Byte = Convert.FromBase64String(maskData)
            Using ms As New MemoryStream(bytes)
                Using fullMaskTemp As New Bitmap(ms)
                    Using fullMask As New Bitmap(fullMaskTemp)
                        Dim cropRect As Rectangle = Rectangle.Intersect(rectX, New Rectangle(0, 0, fullMask.Width, fullMask.Height))
                        If cropRect.Width > 0 AndAlso cropRect.Height > 0 Then
                            Using localMask As New Bitmap(width, height, PixelFormat.Format32bppArgb)
                                Using g As Graphics = Graphics.FromImage(localMask)
                                    g.Clear(Color.Transparent)
                                    g.DrawImage(fullMask, New Rectangle(0, 0, width, height), cropRect, GraphicsUnit.Pixel)
                                End Using
                                If feather > 0 Then
                                    Dim renderer As New Create()
                                    renderer.BlurMask(localMask, Math.Max(1, feather \ 2))
                                End If
                                Dim data As BitmapData = localMask.LockBits(New Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb)
                                Try
                                    Dim stride As Integer = Math.Abs(data.Stride)
                                    Dim raw(stride * height - 1) As Byte
                                    Marshal.Copy(data.Scan0, raw, 0, raw.Length)
                                    For y As Integer = 0 To height - 1
                                        Dim row As Integer = y * stride
                                        Dim outRow As Integer = y * width
                                        For x As Integer = 0 To width - 1
                                            alpha(outRow + x) = raw(row + x * 4 + 3)
                                        Next
                                    Next
                                Finally
                                    localMask.UnlockBits(data)
                                End Try
                            End Using
                        End If
                    End Using
                End Using
            End Using

            SyncLock renderCacheSync
                If Not selectionAlphaCache.ContainsKey(key) Then
                    While selectionAlphaOrder.Count >= C_MAX_SELECTION_CACHE
                        Dim oldest As String = selectionAlphaOrder.Dequeue()
                        selectionAlphaCache.Remove(oldest)
                    End While
                    selectionAlphaCache.Add(key, alpha)
                    selectionAlphaOrder.Enqueue(key)
                End If
            End SyncLock
            Return alpha
        End Function

        Private Class BitmapToByteConverter

            Private bitmap As Bitmap
            Private data As BitmapData

            Public bytes As Byte()

            Public ReadOnly Property Stride As Integer
                Get
                    If data Is Nothing Then Return 0
                    Return data.Stride
                End Get
            End Property

            Public Sub New(ByVal bitmap As Bitmap)
                Me.bitmap = bitmap
            End Sub

            Public Sub BitmapToBytes()
                Dim bounds As Rectangle = New Rectangle(0, 0, bitmap.Width, bitmap.Height)
                data = bitmap.LockBits(bounds, Imaging.ImageLockMode.ReadWrite, Imaging.PixelFormat.Format32bppArgb)
                Dim size As Integer = Math.Abs(data.Stride) * data.Height
                ReDim bytes(size - 1)
                Marshal.Copy(data.Scan0, bytes, 0, size)
            End Sub

            Public Sub BytesToBitmap()
                Dim size As Integer = Math.Abs(data.Stride) * data.Height
                Marshal.Copy(bytes, 0, data.Scan0, size)
                bitmap.UnlockBits(data)
                bitmap = Nothing
                bytes = Nothing
                data = Nothing
            End Sub

        End Class

        Public Sub MergeLayers(ByVal layer1 As Bitmap,
                               ByVal layer2 As Bitmap,
                               Optional ByVal overlays As Double = 0,
                               Optional ByVal lightcolor As Color = Nothing,
                               Optional ByVal dodgecolor As Color = Nothing)
            If layer1 Is Nothing OrElse layer2 Is Nothing Then Return
            If layer1.Width <= 0 OrElse layer1.Height <= 0 OrElse layer2.Width <= 0 OrElse layer2.Height <= 0 Then Return
            If lightcolor = Nothing Then lightcolor = DefaultLightColor

            Dim layer1bytes As BitmapToByteConverter = New BitmapToByteConverter(layer1)
            Dim layer2bytes As BitmapToByteConverter = New BitmapToByteConverter(layer2)
            layer1bytes.BitmapToBytes()
            layer2bytes.BitmapToBytes()

            ' 2.8.7: edge-safe merge. A light touching or crossing an artwork edge can
            ' produce bitmaps that differ by a pixel after clipping. Process only the
            ' shared rectangle and use each bitmap's own stride so no byte-array index
            ' can run past the end of either layer.
            Dim mergeWidth As Integer = Math.Min(layer1.Width, layer2.Width)
            Dim mergeHeight As Integer = Math.Min(layer1.Height, layer2.Height)
            Dim stride1 As Integer = Math.Abs(layer1bytes.Stride)
            Dim stride2 As Integer = Math.Abs(layer2bytes.Stride)

            For y As Integer = 0 To mergeHeight - 1
                Dim row1 As Integer = y * stride1
                Dim row2 As Integer = y * stride2
                For x As Integer = 0 To mergeWidth - 1
                    Dim pixel1 As Integer = row1 + x * 4
                    Dim pixel2 As Integer = row2 + x * 4

                    ' BGRA byte order (reverse RGB).
                    Dim rgb(2) As Byte
                    rgb(0) = layer1bytes.bytes(pixel1)
                    rgb(1) = layer1bytes.bytes(pixel1 + 1)
                    rgb(2) = layer1bytes.bytes(pixel1 + 2)
                    ' Preserve fractional brightness. The former Integer argument
                    ' rounded the 0-400% slider into a few whole overlay passes, so
                    ' long stretches of the control produced exactly the same image.
                    Dim overlayStrength As Double = Math.Max(0.0, If(overlays > 0, overlays, CDbl(C_OVERLAYS)))
                    Dim wholePasses As Integer = CInt(Math.Floor(overlayStrength))
                    Dim fractionalPass As Double = overlayStrength - wholePasses
                    For i As Integer = 1 To wholePasses
                        rgb(0) = ColorOverlay(rgb(0), lightcolor.B)
                        rgb(1) = ColorOverlay(rgb(1), lightcolor.G)
                        rgb(2) = ColorOverlay(rgb(2), lightcolor.R)
                    Next
                    If fractionalPass > 0.0 Then
                        Dim nextBlue As Byte = ColorOverlay(rgb(0), lightcolor.B)
                        Dim nextGreen As Byte = ColorOverlay(rgb(1), lightcolor.G)
                        Dim nextRed As Byte = ColorOverlay(rgb(2), lightcolor.R)
                        rgb(0) = CByte(Math.Max(0, Math.Min(255, CInt(Math.Round(CInt(rgb(0)) + (CInt(nextBlue) - CInt(rgb(0))) * fractionalPass)))))
                        rgb(1) = CByte(Math.Max(0, Math.Min(255, CInt(Math.Round(CInt(rgb(1)) + (CInt(nextGreen) - CInt(rgb(1))) * fractionalPass)))))
                        rgb(2) = CByte(Math.Max(0, Math.Min(255, CInt(Math.Round(CInt(rgb(2)) + (CInt(nextRed) - CInt(rgb(2))) * fractionalPass)))))
                    End If
                    If doDodge AndAlso dodgecolor <> Nothing Then
                        rgb(0) = ColorDodge(rgb(0), dodgecolor.B)
                        rgb(1) = ColorDodge(rgb(1), dodgecolor.G)
                        rgb(2) = ColorDodge(rgb(2), dodgecolor.R)
                    End If
                    layer2bytes.bytes(pixel2) = rgb(0)
                    layer2bytes.bytes(pixel2 + 1) = rgb(1)
                    layer2bytes.bytes(pixel2 + 2) = rgb(2)
                Next
            Next

            layer1bytes.BytesToBitmap()
            layer2bytes.BytesToBitmap()
        End Sub

        Private Function ColorDodge(ByVal a As Byte, ByVal b As Byte) As Byte
            If b = 255 Then Return b
            Dim c As Integer = (CInt(a) << 8) / (255 - CInt(b))
            Return CByte(If((c > 255), 255, c))
        End Function
        Private Function ColorOverlay(ByVal a As Byte, ByVal b As Byte) As Byte
            Dim c As Integer = If(CInt(a) < 128, (2 * CInt(b) * CInt(a) / 255), (255 - 2 * (255 - CInt(b)) * (255 - CInt(a)) / 255))
            Return CByte(If((c > 255), 255, c))
        End Function

        Public Sub DrawLight(ByVal gr As Graphics,
                             ByVal rectlight As Rectangle,
                             Optional ByVal text As String = "",
                             Optional ByVal font As Font = Nothing,
                             Optional ByVal textalignment As Illumination.eTextAlignment = eTextAlignment.Center,
                             Optional ByVal illumode As Illumination.eIlluMode = eIlluMode.Standard,
                             Optional ByVal glowSoftness As Integer = 60,
                             Optional ByVal glowFalloff As Integer = 60,
                             Optional ByVal glowIntensity As Integer = 100,
                             Optional ByVal lightDiffusion As Integer = 0)

            Dim pathlight As GraphicsPath = New GraphicsPath()
            Dim pathlight2 As GraphicsPath = Nothing
            Select Case illumode
                Case eIlluMode.Flasher
                    ' star bulb: flasher
                    With rectlight
                        Dim f As Integer = 5
                        pathlight.AddPolygon(New Point() {New Point(.X, .Y), New Point(.X + .Width / f, .Y + .Height / 2), New Point(.X, .Y + .Height)})
                        pathlight.AddPolygon(New Point() {New Point(.X, .Y + .Height), New Point(.X + .Width / 2, .Y + .Height / f * (f - 1)), New Point(.X + .Width, .Y + .Height)})
                        pathlight.AddPolygon(New Point() {New Point(.X + .Width, .Y + .Height), New Point(.X + .Width / f * (f - 1), .Y + .Height / 2), New Point(.X + .Width, .Y)})
                        pathlight.AddPolygon(New Point() {New Point(.X + .Width, .Y), New Point(.X + .Width / 2, .Y + .Height / f), New Point(.X, .Y)})
                        pathlight.CloseFigure()
                        pathlight2 = New GraphicsPath()
                        pathlight2.AddEllipse(rectlight)
                    End With
                Case Else
                    ' standard bulb: ellipse
                    pathlight.AddEllipse(rectlight)
            End Select

            ' color bulb
            Dim grad As PathGradientBrush = New PathGradientBrush(pathlight)
            grad.WrapMode = WrapMode.Clamp
            grad.SurroundColors = New Color() {Color.FromArgb(0, 0, 0, 0)}
            ' color not important, because due to a ms round bug we can use only alpha of the color
            Dim centerAlpha As Integer = CInt(Math.Max(0, Math.Min(255, 255.0 * glowIntensity / 100.0)))
            grad.CenterColor = Color.FromArgb(If(String.IsNullOrEmpty(text), centerAlpha, Math.Min(30, centerAlpha)), 255, 255, 255)
            grad.CenterPoint = New PointF(rectlight.X + rectlight.Width / 2 - 5, rectlight.Y + rectlight.Height / 2)
            ' Edge feather now has a real 0-200 range. 0 keeps a tight center and
            ' 200 produces the broadest transition. Contrast remains a separate
            ' 0-100 control instead of cancelling the feather setting.
            Dim featherFactor As Double = Math.Max(0.0, Math.Min(1.0, glowSoftness / 200.0))
            Dim contrastFactor As Double = Math.Max(0.0, Math.Min(1.0, glowFalloff / 100.0))
            Dim baseFocus As Double = 0.88 - (0.82 * featherFactor)
            baseFocus += (contrastFactor - 0.5) * 0.18

            ' Light Diffuser is neutral at zero and progressively spreads the
            ' center energy outward through the full 0-300 range.
            Dim diffusionFactor As Double = Math.Max(0.0, Math.Min(1.0, lightDiffusion / 300.0))
            Dim focus As Single = CSng(Math.Max(0.01, Math.Min(0.95, baseFocus * (1.0 - 0.88 * diffusionFactor))))
            grad.FocusScales = New PointF(focus, focus)
            gr.FillRectangle(grad, rectlight)
            grad.Dispose()
            pathlight.Dispose()
            If pathlight2 IsNot Nothing Then
                grad = New PathGradientBrush(pathlight2)
                grad.WrapMode = WrapMode.Clamp
                grad.SurroundColors = New Color() {Color.FromArgb(0, 0, 0, 0)}
                ' color not important, because due to a ms round bug we can use only alpha of the color
                grad.CenterColor = Color.FromArgb(If(String.IsNullOrEmpty(text), centerAlpha, Math.Min(30, centerAlpha)), 255, 255, 255)
                grad.CenterPoint = New PointF(rectlight.X + rectlight.Width / 2 - 5, rectlight.Y + rectlight.Height / 2)
                grad.FocusScales = New PointF(focus, focus)
                gr.FillRectangle(grad, rectlight)
                grad.Dispose()
                pathlight2.Dispose()
            End If

            ' maybe render text
            If Not String.IsNullOrEmpty(text) AndAlso font IsNot Nothing Then
                Dim size As Size = TextRenderer.MeasureText(gr, text, font, New Size(0, 0), TextFormatFlags.NoPrefix)
                Dim loc As Point = New Point(rectlight.X + CInt((rectlight.Width - size.Width) / 2) + 2, rectlight.Y + CInt((rectlight.Height - size.Height) / 2))
                Dim left As Point = New Point(0, 0)
                Dim right As Point = New Point(rectlight.Width - 1, rectlight.Height - 1)
                Dim blend As ColorBlend = New ColorBlend
                blend.Colors = New Color() {Color.Transparent, Color.White, Color.Transparent}
                blend.Positions = New Single() {0.0F, 0.5F, 1.0F}
                If text.Contains(vbCrLf) Then
                    Using brush As LinearGradientBrush = New LinearGradientBrush(left, right, Color.White, Color.White)
                        brush.InterpolationColors = blend
                        'Dim brush As Brush = New SolidBrush(Color.FromArgb(255, 255, 255, 255))
                        Dim lines As String() = text.Split(vbCrLf)
                        Dim y As Integer = rectlight.Y + CInt((rectlight.Height - size.Height) / 2)
                        For Each line As String In lines
                            line = line.Trim.Replace(Chr(13), "").Replace(Chr(10), "")
                            Dim linesize As Size = TextRenderer.MeasureText(gr, line, font)
                            Dim x As Integer = rectlight.X + CInt((rectlight.Width - linesize.Width) / 2) + 2
                            If textalignment = eTextAlignment.Left Then
                                x = 2
                            ElseIf textalignment = eTextAlignment.Right Then
                                x = rectlight.X + rectlight.Width - linesize.Width + 2
                            End If
                            gr.DrawString(line, font, brush, New Point(x, y))
                            'gr.DrawString(line, font, brush, New Rectangle(New Point(x, y), New Size(linesize.Width, linesize.Height)), Drawing.StringFormat.GenericTypographic)
                            y += CInt(size.Height / lines.Count)
                        Next
                        'brush.Dispose()
                    End Using
                Else
                    Using brush As LinearGradientBrush = New LinearGradientBrush(left, right, Color.White, Color.White)
                        brush.InterpolationColors = blend
                        Dim x As Integer = loc.X
                        If textalignment = eTextAlignment.Left Then
                            x = 2
                        ElseIf textalignment = eTextAlignment.Right Then
                            x = rectlight.X + rectlight.Width - size.Width + 2
                        End If
                        gr.DrawString(text, font, brush, New Point(x, loc.Y))
                        'gr.DrawString(text, font, brush, New Rectangle(New Point(x, loc.Y), size), Drawing.StringFormat.GenericTypographic)
                    End Using
                End If
            End If

        End Sub

        ' 3.1.6: artwork-based flasher rendering.
        ' A flasher with a Quick Selection mask now brightens the actual artwork
        ' inside that mask. The mask defines the visible shape; no synthetic star,
        ' oval, or rectangular light shape is drawn over the artwork.
        Private Function CreateArtworkFlasherOverlay(ByVal source As Bitmap,
                                                      ByVal rectX As Rectangle,
                                                      ByVal selectionMaskData As String,
                                                      ByVal selectionFeather As Integer,
                                                      ByVal glowSoftness As Integer,
                                                      ByVal glowFalloff As Integer,
                                                      ByVal glowIntensity As Integer,
                                                      ByVal lightcolor As Color,
                                                      ByVal flasherStyle As Integer,
                                                      ByVal saturation As Integer,
                                                      ByVal highlightProtection As Integer,
                                                      ByVal darkAreaLift As Integer,
                                                      ByVal hotspotX As Integer,
                                                      ByVal hotspotY As Integer) As Bitmap
            If source Is Nothing OrElse String.IsNullOrEmpty(selectionMaskData) Then Return Nothing
            Dim width As Integer = source.Width, height As Integer = source.Height
            If width <= 0 OrElse height <= 0 Then Return Nothing
            Dim coreAlpha As Byte() = GetSelectionAlpha(selectionMaskData, rectX, width, height, selectionFeather)
            If coreAlpha Is Nothing OrElse coreAlpha.Length <> width * height Then Return Nothing

            ' 3.1.7: The bloom seed is made from the bright details in the selected
            ' artwork, not from a radial hotspot and not from the complete mask.
            ' This removes the visible circular halo while retaining a subtle,
            ' artwork-shaped light spill around highlights.
            Dim bloomAlpha(width * height - 1) As Byte
            Dim result As New Bitmap(width, height, PixelFormat.Format32bppArgb)
            Dim srcData As BitmapData = source.LockBits(New Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb)
            Dim dstData As BitmapData = result.LockBits(New Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb)
            Try
                Dim ss As Integer = Math.Abs(srcData.Stride), ds As Integer = Math.Abs(dstData.Stride)
                Dim srcBytes(ss * height - 1) As Byte, dstBytes(ds * height - 1) As Byte
                Marshal.Copy(srcData.Scan0, srcBytes, 0, srcBytes.Length)

                ' Build an irregular luminance-driven bloom map. Dark outlines and
                ' transparent areas produce almost no bloom; bright printed details
                ' produce the strongest bloom.
                For y As Integer = 0 To height - 1
                    For x As Integer = 0 To width - 1
                        Dim si As Integer = y * ss + x * 4, mi As Integer = y * width + x
                        Dim core As Double = coreAlpha(mi) / 255.0
                        If core <= 0 Then Continue For
                        Dim b As Double = srcBytes(si), g As Double = srcBytes(si + 1), r As Double = srcBytes(si + 2)
                        Dim sa As Double = srcBytes(si + 3) / 255.0
                        Dim lum As Double = (r * 0.299 + g * 0.587 + b * 0.114) / 255.0
                        Dim highlight As Double = Math.Max(0.0, (lum - 0.28) / 0.72)
                        highlight = highlight * highlight
                        bloomAlpha(mi) = CByte(ClampByte(CInt(Math.Round(255.0 * core * sa * highlight))))
                    Next
                Next

                Dim spreadRadius As Integer = Math.Max(0, Math.Min(25, CInt(Math.Round(glowSoftness / 6.0))))
                If spreadRadius > 0 Then BlurAlphaBytes(bloomAlpha, width, height, spreadRadius)

                Dim strength As Double = Math.Max(0.0, Math.Min(4.0, glowIntensity / 100.0))
                Dim protect As Double = Math.Max(0.0, Math.Min(1.0, highlightProtection / 100.0))
                Dim darkLift As Double = Math.Max(0.0, Math.Min(1.0, darkAreaLift / 100.0))
                Dim sat As Double = Math.Max(0.0, Math.Min(2.0, saturation / 100.0))
                Dim hx As Double = Math.Max(0.0, Math.Min(width - 1.0, (hotspotX / 100.0) * (width - 1)))
                Dim hy As Double = Math.Max(0.0, Math.Min(height - 1.0, (hotspotY / 100.0) * (height - 1)))
                Dim maxDist As Double = Math.Max(1.0, Math.Sqrt(width * width + height * height) * 0.9)
                Dim glowAmount As Double = Math.Max(0.0, Math.Min(0.25, glowFalloff / 100.0 * 0.25))

                For y As Integer = 0 To height - 1
                    For x As Integer = 0 To width - 1
                        Dim si As Integer = y * ss + x * 4, di As Integer = y * ds + x * 4, mi As Integer = y * width + x
                        Dim core As Double = coreAlpha(mi) / 255.0
                        Dim blur As Double = bloomAlpha(mi) / 255.0
                        If core <= 0 AndAlso blur <= 0 Then Continue For
                        Dim b As Double = srcBytes(si), g As Double = srcBytes(si + 1), r As Double = srcBytes(si + 2)
                        Dim sa As Double = srcBytes(si + 3) / 255.0
                        Dim lum As Double = (r * 0.299 + g * 0.587 + b * 0.114) / 255.0

                        ' The hotspot now only gives a very small directional bias to
                        ' existing bright details. It never contributes alpha, tint,
                        ' or an independently visible radial glow.
                        Dim dx As Double = x - hx, dy As Double = y - hy
                        Dim hotspotBias As Double = Math.Max(0.0, 1.0 - Math.Sqrt(dx * dx + dy * dy) / maxDist)
                        hotspotBias = hotspotBias * hotspotBias * lum * lum * 0.10

                        Dim response As Double = 0.10 + 0.78 * Math.Sqrt(lum) + hotspotBias
                        Dim shadowResponse As Double = darkLift * (1.0 - lum) * 0.22
                        Dim exposure As Double = 1.0 + strength * (response + shadowResponse)
                        exposure = 1.0 + (exposure - 1.0) * (1.0 - protect * lum * 0.78)
                        Dim rr As Double = ExposureChannel(CInt(r), exposure)
                        Dim gg As Double = ExposureChannel(CInt(g), exposure)
                        Dim bb As Double = ExposureChannel(CInt(b), exposure)
                        Dim outLum As Double = rr * 0.299 + gg * 0.587 + bb * 0.114
                        rr = outLum + (rr - outLum) * sat
                        gg = outLum + (gg - outLum) * sat
                        bb = outLum + (bb - outLum) * sat

                        Dim coreA As Double = core
                        Dim bloomA As Double = blur * glowAmount
                        Dim finalA As Double
                        Select Case flasherStyle
                            Case 1
                                finalA = coreA
                            Case 2
                                finalA = bloomA
                                Dim glowWhite As Double = 0.62 + 0.38 * lum
                                rr = lightcolor.R * glowWhite
                                gg = lightcolor.G * glowWhite
                                bb = lightcolor.B * glowWhite
                            Case Else
                                finalA = Math.Max(coreA, bloomA)
                        End Select
                        finalA *= sa
                        dstBytes(di) = CByte(ClampByte(CInt(bb)))
                        dstBytes(di + 1) = CByte(ClampByte(CInt(gg)))
                        dstBytes(di + 2) = CByte(ClampByte(CInt(rr)))
                        dstBytes(di + 3) = CByte(ClampByte(CInt(finalA * 255)))
                    Next
                Next
                Marshal.Copy(dstBytes, 0, dstData.Scan0, dstBytes.Length)
            Finally
                source.UnlockBits(srcData)
                result.UnlockBits(dstData)
            End Try
            Return result
        End Function

        Private Shared Function ExposureChannel(ByVal value As Integer, ByVal exposure As Double) As Integer
            Dim normalized As Double = Math.Max(0.0, Math.Min(1.0, value / 255.0))
            ' 1-(1-x)^e keeps exposure=1 unchanged and approaches white smoothly,
            ' avoiding the hard clipping that made earlier flashers look fake.
            Dim brightened As Double = 1.0 - Math.Pow(1.0 - normalized, Math.Max(1.0, exposure))
            Return ClampByte(CInt(Math.Round(brightened * 255.0)))
        End Function

        Private Shared Function ClampByte(ByVal value As Integer) As Integer
            Return Math.Max(0, Math.Min(255, value))
        End Function

        Private Shared Sub BlurAlphaBytes(ByVal alpha As Byte(), ByVal width As Integer, ByVal height As Integer, ByVal radius As Integer)
            If alpha Is Nothing OrElse width <= 0 OrElse height <= 0 OrElse radius <= 0 Then Return
            radius = Math.Min(25, radius)
            Dim horizontal(alpha.Length - 1) As Integer
            Dim output(alpha.Length - 1) As Byte

            For y As Integer = 0 To height - 1
                Dim sum As Integer = 0
                For x As Integer = -radius To radius
                    Dim sampleX As Integer = Math.Max(0, Math.Min(width - 1, x))
                    sum += alpha(y * width + sampleX)
                Next
                For x As Integer = 0 To width - 1
                    horizontal(y * width + x) = sum \ (radius * 2 + 1)
                    Dim removeX As Integer = Math.Max(0, x - radius)
                    Dim addX As Integer = Math.Min(width - 1, x + radius + 1)
                    sum += alpha(y * width + addX) - alpha(y * width + removeX)
                Next
            Next

            For x As Integer = 0 To width - 1
                Dim sum As Integer = 0
                For y As Integer = -radius To radius
                    Dim sampleY As Integer = Math.Max(0, Math.Min(height - 1, y))
                    sum += horizontal(sampleY * width + x)
                Next
                For y As Integer = 0 To height - 1
                    output(y * width + x) = CByte(Math.Max(0, Math.Min(255, sum \ (radius * 2 + 1))))
                    Dim removeY As Integer = Math.Max(0, y - radius)
                    Dim addY As Integer = Math.Min(height - 1, y + radius + 1)
                    sum += horizontal(addY * width + x) - horizontal(removeY * width + x)
                Next
            Next

            Array.Copy(output, alpha, alpha.Length)
        End Sub

        Private Shared Sub ApplyTemperatureToRenderedLight(ByVal rendered As Bitmap, ByVal kelvin As Integer)
            If rendered Is Nothing Then Return
            kelvin = Math.Max(2000, Math.Min(6500, kelvin))
            If kelvin = 4000 Then Return

            ' Apply a photographic white-balance grade after the proven light
            ' renderer has finished. 4000K remains the exact neutral/reference
            ' point. The end points are deliberately restrained so the control
            ' looks like a real bulb change instead of a heavy colour filter.
            Dim redFactor As Double
            Dim greenFactor As Double
            Dim blueFactor As Double

            If kelvin <= 2700 Then
                Dim t As Double = (kelvin - 2000.0) / 700.0
                redFactor = LerpDouble(1.22, 1.14, t)
                greenFactor = LerpDouble(0.92, 0.96, t)
                blueFactor = LerpDouble(0.58, 0.72, t)
            ElseIf kelvin <= 3000 Then
                Dim t As Double = (kelvin - 2700.0) / 300.0
                redFactor = LerpDouble(1.14, 1.1, t)
                greenFactor = LerpDouble(0.96, 0.98, t)
                blueFactor = LerpDouble(0.72, 0.8, t)
            ElseIf kelvin <= 4000 Then
                Dim t As Double = (kelvin - 3000.0) / 1000.0
                redFactor = LerpDouble(1.1, 1.0, t)
                greenFactor = LerpDouble(0.98, 1.0, t)
                blueFactor = LerpDouble(0.8, 1.0, t)
            ElseIf kelvin <= 5000 Then
                Dim t As Double = (kelvin - 4000.0) / 1000.0
                redFactor = LerpDouble(1.0, 0.96, t)
                greenFactor = LerpDouble(1.0, 1.01, t)
                blueFactor = LerpDouble(1.0, 1.1, t)
            Else
                Dim t As Double = (kelvin - 5000.0) / 1500.0
                redFactor = LerpDouble(0.96, 0.9, t)
                greenFactor = LerpDouble(1.01, 1.0, t)
                blueFactor = LerpDouble(1.1, 1.22, t)
            End If

            Dim data As BitmapData = rendered.LockBits(New Rectangle(0, 0, rendered.Width, rendered.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb)
            Try
                Dim stride As Integer = Math.Abs(data.Stride)
                Dim raw(stride * rendered.Height - 1) As Byte
                Marshal.Copy(data.Scan0, raw, 0, raw.Length)

                For y As Integer = 0 To rendered.Height - 1
                    Dim row As Integer = y * stride
                    For x As Integer = 0 To rendered.Width - 1
                        Dim pixel As Integer = row + x * 4
                        If raw(pixel + 3) <> 0 Then
                            Dim originalB As Double = raw(pixel)
                            Dim originalG As Double = raw(pixel + 1)
                            Dim originalR As Double = raw(pixel + 2)

                            Dim oldLuma As Double = originalR * 0.2126 + originalG * 0.7152 + originalB * 0.0722
                            Dim gradedR As Double = originalR * redFactor
                            Dim gradedG As Double = originalG * greenFactor
                            Dim gradedB As Double = originalB * blueFactor
                            Dim newLuma As Double = gradedR * 0.2126 + gradedG * 0.7152 + gradedB * 0.0722

                            ' Preserve perceived brightness so temperature does not
                            ' secretly act like another brightness control.
                            If newLuma > 0.001 Then
                                Dim lumaScale As Double = oldLuma / newLuma
                                lumaScale = Math.Max(0.82, Math.Min(1.18, lumaScale))
                                gradedR *= lumaScale
                                gradedG *= lumaScale
                                gradedB *= lumaScale
                            End If

                            ' Brighter illuminated pixels receive the full bulb cast;
                            ' very dark pixels get less tint, preventing muddy shadows.
                            Dim strength As Double = 0.55 + 0.3 * (oldLuma / 255.0)
                            Dim adjustedR As Integer = CInt(Math.Round(originalR + (gradedR - originalR) * strength))
                            Dim adjustedG As Integer = CInt(Math.Round(originalG + (gradedG - originalG) * strength))
                            Dim adjustedB As Integer = CInt(Math.Round(originalB + (gradedB - originalB) * strength))

                            raw(pixel) = CByte(Math.Max(0, Math.Min(255, adjustedB)))
                            raw(pixel + 1) = CByte(Math.Max(0, Math.Min(255, adjustedG)))
                            raw(pixel + 2) = CByte(Math.Max(0, Math.Min(255, adjustedR)))
                        End If
                    Next
                Next

                Marshal.Copy(raw, 0, data.Scan0, raw.Length)
            Finally
                rendered.UnlockBits(data)
            End Try
        End Sub

        Private Shared Function LerpDouble(ByVal startValue As Double, ByVal endValue As Double, ByVal amount As Double) As Double
            amount = Math.Max(0.0, Math.Min(1.0, amount))
            Return startValue + (endValue - startValue) * amount
        End Function

        Private Shared Function GetArtworkExposure(ByVal baseIntensity As Integer,
                                                   ByVal isFlasher As Boolean) As Integer
            ' The authored value is honest and visible in the UI: lamps are limited
            ' to 800%, while flashers may use the high-energy range through 1600%.
            ' No hidden multiplier makes two slider values collapse to one output.
            Dim maximum As Integer = If(isFlasher, 1600, 800)
            Return Math.Max(0, Math.Min(maximum, baseIntensity))
        End Function

        Public Function CreateOverlayImage(ByVal imageBackground As Bitmap,
                                           ByVal rect As Rectangle,
                                           ByVal rectX As Rectangle,
                                           ByVal intensity As Integer,
                                           ByVal lightcolor As Color,
                                           ByVal dodgecolor As Color,
                                           ByVal text As String,
                                           ByVal font As Font,
                                           ByVal textalignment As Illumination.eTextAlignment,
                                           ByVal illumode As Illumination.eIlluMode,
                                           Optional ByVal glowSoftness As Integer = 60,
                                           Optional ByVal glowFalloff As Integer = 60,
                                           Optional ByVal glowIntensity As Integer = 100,
                                           Optional ByVal selectionMaskData As String = "",
                                           Optional ByVal selectionFeather As Integer = 0,
                                           Optional ByVal applyGlobalMask As Boolean = True,
                                           Optional ByVal flasherStyle As Integer = 0,
                                           Optional ByVal flasherSaturation As Integer = 105,
                                           Optional ByVal flasherHighlightProtection As Integer = 70,
                                           Optional ByVal flasherDarkAreaLift As Integer = 20,
                                           Optional ByVal flasherHotspotX As Integer = 50,
                                           Optional ByVal flasherHotspotY As Integer = 50,
                                           Optional ByVal lightDiffusion As Integer = 0,
                                           Optional ByVal lightTemperature As Integer = 4000,
                                           Optional ByVal artworkPixelLighting As Boolean = False,
                                           Optional ByVal transmissionContrast As Integer = 140,
                                           Optional ByVal maskRadius As Integer = 0,
                                           Optional ByVal maskSmartRadius As Boolean = False,
                                           Optional ByVal maskSmooth As Integer = 0,
                                           Optional ByVal maskFeather As Integer = 0,
                                           Optional ByVal maskContrast As Integer = 0,
                                           Optional ByVal maskShiftEdge As Integer = 0,
                                           Optional ByVal flasherRadialSpikes As Integer = 0,
                                           Optional ByVal lightRotationAngle As Single = 0.0F,
                                           Optional ByVal transmitTransparentCanvas As Boolean = False) As Image

            ' maybe create the scaled and illuminated image part
            If imageBackground IsNot Nothing Then
                ' Normal lamps—including text lamps and Quick Selection lamps—must
                ' keep the established synthetic lamp/text renderer.  Only an actual
                ' flasher uses the artwork-pixel renderer.  Routing ordinary lamps
                ' through the flasher renderer discards sharp text RGB and replaces a
                ' masked lamp with illuminated artwork, which can disappear completely
                ' over transparent canvas areas.
                Dim isFlasherLighting As Boolean = artworkPixelLighting OrElse
                                                       illumode = Illumination.eIlluMode.Flasher
                Dim useArtworkPixels As Boolean = isFlasherLighting
                Dim artworkExposure As Integer =
                    GetArtworkExposure(glowIntensity, isFlasherLighting)
                Dim currentimage As Bitmap = imageBackground.Clone(rectX, Imaging.PixelFormat.Format32bppArgb)
                ' 2.8.8: reuse the expensive anti-aliased glow/text template while
                ' dragging. The returned bitmap is a clone and remains safe to clip
                ' and merge into the current background crop.
                Dim image As Bitmap =
                    GetGlowTemplate(rect.Width, rect.Height, text, font, textalignment,
                                    illumode, glowSoftness, glowFalloff,
                                    glowIntensity, lightDiffusion)

                If Math.Abs(lightRotationAngle) >= 0.001F Then
                    Dim rotatedImage As Bitmap = PositionRotatedLightTemplate(image, rect, rectX, lightRotationAngle)
                    image.Dispose()
                    image = rotatedImage
                ElseIf Not rect.Equals(rectX) Then
                    ' 2.8.7: always return a light bitmap with exactly the same size
                    ' as the clipped background rectangle. Drawing with an offset is
                    ' safer than calculating a crop and also handles negative X/Y,
                    ' right/bottom clipping, glow spread, and rounding at image edges.
                    Dim clippedImage As New Bitmap(rectX.Width, rectX.Height, PixelFormat.Format32bppArgb)
                    Using clippedGraphics As Graphics = Graphics.FromImage(clippedImage)
                        clippedGraphics.Clear(Color.Transparent)
                        clippedGraphics.DrawImageUnscaled(image, rect.X - rectX.X, rect.Y - rectX.Y)
                    End Using
                    image.Dispose()
                    image = clippedImage
                End If

                If useArtworkPixels AndAlso (isFlasherLighting OrElse String.IsNullOrEmpty(selectionMaskData)) Then
                    Dim pixelLit As Bitmap = ArtworkFlasherRenderer.RenderFromAlphaField(
                        currentimage, image, artworkExposure, lightcolor,
                        flasherStyle, flasherSaturation, flasherHighlightProtection,
                        flasherDarkAreaLift, transmissionContrast,
                        maskRadius, maskSmooth, maskFeather, maskContrast, maskShiftEdge,
                        flasherRadialSpikes, transmitTransparentCanvas)
                    If pixelLit IsNot Nothing Then
                        image.Dispose()
                        image = pixelLit
                        ApplyTemperatureToRenderedLight(image, lightTemperature)
                        If Not String.IsNullOrEmpty(selectionMaskData) Then
                            ApplySelectionMask(image, currentimage, rectX, selectionMaskData, selectionFeather)
                        End If
                        If applyGlobalMask Then ApplyGlobalIlluminationMask(image, rectX)
                        currentimage.Dispose()
                        Return image
                    End If
                End If

                ' Keep the original light intensity behavior through 100%, then
                ' scale the merge energy with Glow Intensity up to the 400% UI limit.
                ' This was accidentally lost when the temperature renderer was tuned.
                Dim effectiveIntensity As Double = Math.Max(0.0, intensity * (Math.Max(0, glowIntensity) / 100.0))
                MergeLayers(currentimage, image, effectiveIntensity, lightcolor, dodgecolor)
                ' A rear snippet can be visible through transparent canvas pixels.
                ' Those pixels commonly store RGB 0,0,0 even though their alpha is
                ' zero. The legacy artwork merge therefore produced a black glow
                ' patch above the snippet. Preserve normal artwork illumination on
                ' opaque pixels, but turn the transparent portion into a colored,
                ' alpha-only light overlay so moving/rotating layers remain visible.
                ApplyTransparentCanvasTransmission(image, currentimage, If(lightcolor = Nothing, DefaultLightColor, lightcolor), glowIntensity)
                ApplyTemperatureToRenderedLight(image, lightTemperature)
                If Not String.IsNullOrEmpty(selectionMaskData) Then
                    ApplySelectionMask(image, currentimage, rectX, selectionMaskData, selectionFeather)
                End If
                If applyGlobalMask Then ApplyGlobalIlluminationMask(image, rectX)
                currentimage.Dispose()
                Return image
            Else
                Return Nothing
            End If

        End Function

        Private Sub ApplyTransparentCanvasTransmission(ByVal illuminated As Bitmap,
                                                        ByVal background As Bitmap,
                                                        ByVal lightColor As Color,
                                                        ByVal glowIntensity As Integer)
            If illuminated Is Nothing OrElse background Is Nothing Then Return
            Dim width As Integer = Math.Min(illuminated.Width, background.Width)
            Dim height As Integer = Math.Min(illuminated.Height, background.Height)
            If width <= 0 OrElse height <= 0 Then Return

            Dim lightBytes As New BitmapToByteConverter(illuminated)
            Dim backgroundBytes As New BitmapToByteConverter(background)
            lightBytes.BitmapToBytes()
            backgroundBytes.BitmapToBytes()
            Try
                Dim lightStride As Integer = Math.Abs(lightBytes.Stride)
                Dim backgroundStride As Integer = Math.Abs(backgroundBytes.Stride)
                For y As Integer = 0 To height - 1
                    Dim lightRow As Integer = y * lightStride
                    Dim backgroundRow As Integer = y * backgroundStride
                    For x As Integer = 0 To width - 1
                        Dim lightPixel As Integer = lightRow + x * 4
                        If lightBytes.bytes(lightPixel + 3) = 0 Then Continue For
                        Dim backgroundPixel As Integer = backgroundRow + x * 4
                        Dim backgroundAlpha As Integer = CInt(backgroundBytes.bytes(backgroundPixel + 3))
                        If backgroundAlpha >= 255 Then Continue For

                        Dim transmission As Integer = 255 - backgroundAlpha
                        lightBytes.bytes(lightPixel) = CByte((CInt(lightBytes.bytes(lightPixel)) * backgroundAlpha + CInt(lightColor.B) * transmission + 127) \ 255)
                        lightBytes.bytes(lightPixel + 1) = CByte((CInt(lightBytes.bytes(lightPixel + 1)) * backgroundAlpha + CInt(lightColor.G) * transmission + 127) \ 255)
                        lightBytes.bytes(lightPixel + 2) = CByte((CInt(lightBytes.bytes(lightPixel + 2)) * backgroundAlpha + CInt(lightColor.R) * transmission + 127) \ 255)
                        ' Transparent canvas has no artwork RGB to brighten, so its
                        ' visible strength must come from alpha over the live snippet.
                        ' Map the complete 0-400% range to a texture-preserving curve:
                        ' 100% retains the established transmitted level, while every
                        ' value through 400% continues to produce a visible increase.
                        Dim clampedIntensity As Double = Math.Max(0.0, Math.Min(800.0, CDbl(glowIntensity)))
                        Dim currentCenterAlpha As Double = Math.Min(255.0, 255.0 * clampedIntensity / 100.0)
                        Dim desiredCenterAlpha As Double = If(clampedIntensity <= 0.0, 0.0, 204.0 * Math.Sqrt(clampedIntensity / 400.0))
                        Dim transmittedScale As Double = If(currentCenterAlpha <= 0.0, 0.0, desiredCenterAlpha / currentCenterAlpha)
                        Dim canvasOpacity As Double = backgroundAlpha / 255.0
                        Dim alphaScale As Double = transmittedScale + (1.0 - transmittedScale) * canvasOpacity
                        Dim adjustedAlpha As Integer = CInt(Math.Round(CInt(lightBytes.bytes(lightPixel + 3)) * alphaScale))
                        lightBytes.bytes(lightPixel + 3) = CByte(Math.Max(0, Math.Min(255, adjustedAlpha)))
                    Next
                Next
            Finally
                backgroundBytes.BytesToBitmap()
                lightBytes.BytesToBitmap()
            End Try
        End Sub

        Public Shared Function RotatedLightBounds(ByVal rectangle As Rectangle,
                                                  ByVal angle As Single) As Rectangle
            If rectangle.Width <= 0 OrElse rectangle.Height <= 0 OrElse Math.Abs(angle) < 0.001F Then Return rectangle

            Dim radians As Double = angle * Math.PI / 180.0R
            Dim cosine As Double = Math.Abs(Math.Cos(radians))
            Dim sine As Double = Math.Abs(Math.Sin(radians))
            Dim width As Double = rectangle.Width * cosine + rectangle.Height * sine
            Dim height As Double = rectangle.Width * sine + rectangle.Height * cosine
            Dim centerX As Double = rectangle.Left + rectangle.Width / 2.0R
            Dim centerY As Double = rectangle.Top + rectangle.Height / 2.0R
            Return Rectangle.FromLTRB(CInt(Math.Floor(centerX - width / 2.0R)),
                                      CInt(Math.Floor(centerY - height / 2.0R)),
                                      CInt(Math.Ceiling(centerX + width / 2.0R)),
                                      CInt(Math.Ceiling(centerY + height / 2.0R)))
        End Function

        Private Shared Function PositionRotatedLightTemplate(ByVal source As Bitmap,
                                                              ByVal sourceRectangle As Rectangle,
                                                              ByVal outputRectangle As Rectangle,
                                                              ByVal angle As Single) As Bitmap
            If source Is Nothing OrElse outputRectangle.Width <= 0 OrElse outputRectangle.Height <= 0 Then Return Nothing

            Dim positioned As New Bitmap(outputRectangle.Width, outputRectangle.Height, PixelFormat.Format32bppArgb)
            Using graphics As Graphics = Graphics.FromImage(positioned)
                graphics.Clear(Color.Transparent)
                graphics.CompositingMode = CompositingMode.SourceCopy
                graphics.CompositingQuality = CompositingQuality.HighQuality
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality
                graphics.SmoothingMode = SmoothingMode.HighQuality

                Dim centerX As Single = CSng(sourceRectangle.Left + sourceRectangle.Width / 2.0R - outputRectangle.Left)
                Dim centerY As Single = CSng(sourceRectangle.Top + sourceRectangle.Height / 2.0R - outputRectangle.Top)
                graphics.TranslateTransform(centerX, centerY)
                graphics.RotateTransform(angle)
                graphics.DrawImage(source,
                                   New RectangleF(-sourceRectangle.Width / 2.0F, -sourceRectangle.Height / 2.0F,
                                                  sourceRectangle.Width, sourceRectangle.Height),
                                   New RectangleF(0.0F, 0.0F, source.Width, source.Height),
                                   GraphicsUnit.Pixel)
            End Using
            Return positioned
        End Function


        ' Applies the project-wide binary light mask after the per-light mask.
        ' Enhanced 2.7.3: sample the full mask with exact absolute backglass
        ' coordinates. Do not crop and stretch a mask section to the light size,
        ' because that causes partial/half-backglass alignment failures.
        Private Sub ApplyGlobalIlluminationMask(ByVal illuminated As Bitmap, ByVal rectX As Rectangle)
            Try
                If Backglass.currentData Is Nothing OrElse Not Backglass.currentData.GlobalIlluminationMaskEnabled Then Return
                Dim maskData As String = Backglass.currentData.GlobalIlluminationMaskData
                If String.IsNullOrEmpty(maskData) Then Return

                Dim fullMask As Bitmap = GetCachedGlobalMask(maskData)
                If fullMask Is Nothing Then Return
                Dim threshold As Integer = Math.Max(0, Math.Min(255, Backglass.currentData.GlobalIlluminationMaskThreshold))
                Dim inverted As Boolean = Backglass.currentData.GlobalIlluminationMaskInverted
                Dim binaryKey As String = maskData.Length.ToString() & ":" & maskData.GetHashCode().ToString() & "|" & threshold.ToString() & "|" & inverted.ToString()
                Dim binary As Byte() = Nothing

                SyncLock globalMaskSync
                    If cachedGlobalBinary Is Nothing OrElse Not String.Equals(cachedGlobalBinaryKey, binaryKey, StringComparison.Ordinal) Then
                        Dim data As BitmapData = fullMask.LockBits(New Rectangle(0, 0, fullMask.Width, fullMask.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb)
                        Try
                            Dim stride As Integer = Math.Abs(data.Stride)
                            Dim raw(stride * fullMask.Height - 1) As Byte
                            Marshal.Copy(data.Scan0, raw, 0, raw.Length)
                            Dim nextBinary(fullMask.Width * fullMask.Height - 1) As Byte
                            For y As Integer = 0 To fullMask.Height - 1
                                Dim row As Integer = y * stride
                                Dim outRow As Integer = y * fullMask.Width
                                For x As Integer = 0 To fullMask.Width - 1
                                    Dim pixel As Integer = row + x * 4
                                    Dim luminance As Integer = (CInt(raw(pixel + 2)) * 299 + CInt(raw(pixel + 1)) * 587 + CInt(raw(pixel)) * 114) \ 1000
                                    Dim passes As Boolean = (raw(pixel + 3) >= 128 AndAlso luminance >= threshold)
                                    If inverted Then passes = Not passes
                                    nextBinary(outRow + x) = If(passes, CByte(255), CByte(0))
                                Next
                            Next
                            cachedGlobalBinary = nextBinary
                            cachedGlobalBinaryKey = binaryKey
                            cachedGlobalBinaryWidth = fullMask.Width
                            cachedGlobalBinaryHeight = fullMask.Height
                        Finally
                            fullMask.UnlockBits(data)
                        End Try
                    End If
                    binary = cachedGlobalBinary
                End SyncLock

                Dim illuminatedData As BitmapData = illuminated.LockBits(New Rectangle(0, 0, illuminated.Width, illuminated.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb)
                Try
                    Dim stride As Integer = Math.Abs(illuminatedData.Stride)
                    Dim raw(stride * illuminated.Height - 1) As Byte
                    Marshal.Copy(illuminatedData.Scan0, raw, 0, raw.Length)
                    For y As Integer = 0 To illuminated.Height - 1
                        Dim maskY As Integer = rectX.Y + y
                        Dim row As Integer = y * stride
                        For x As Integer = 0 To illuminated.Width - 1
                            Dim maskX As Integer = rectX.X + x
                            Dim passes As Boolean = (maskX >= 0 AndAlso maskY >= 0 AndAlso maskX < cachedGlobalBinaryWidth AndAlso maskY < cachedGlobalBinaryHeight AndAlso binary(maskY * cachedGlobalBinaryWidth + maskX) <> 0)
                            If Not passes Then
                                Dim pixel As Integer = row + x * 4
                                raw(pixel) = 0
                                raw(pixel + 1) = 0
                                raw(pixel + 2) = 0
                                raw(pixel + 3) = 0
                            End If
                        Next
                    Next
                    Marshal.Copy(raw, 0, illuminatedData.Scan0, raw.Length)
                Finally
                    illuminated.UnlockBits(illuminatedData)
                End Try
            Catch
                ' A corrupt optional global mask must not stop normal light rendering.
            End Try
        End Sub

        Private Sub ApplySelectionMask(ByVal illuminated As Bitmap, ByVal original As Bitmap, ByVal rectX As Rectangle, ByVal maskData As String, ByVal feather As Integer)
            Try
                Dim width As Integer = Math.Min(illuminated.Width, original.Width)
                Dim height As Integer = Math.Min(illuminated.Height, original.Height)
                If width <= 0 OrElse height <= 0 Then Return
                Dim alpha As Byte() = GetSelectionAlpha(maskData, rectX, illuminated.Width, illuminated.Height, feather)

                Dim data As BitmapData = illuminated.LockBits(New Rectangle(0, 0, illuminated.Width, illuminated.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb)
                Try
                    Dim stride As Integer = Math.Abs(data.Stride)
                    Dim raw(stride * illuminated.Height - 1) As Byte
                    Marshal.Copy(data.Scan0, raw, 0, raw.Length)
                    For y As Integer = 0 To height - 1
                        Dim row As Integer = y * stride
                        Dim alphaRow As Integer = y * illuminated.Width
                        For x As Integer = 0 To width - 1
                            Dim pixel As Integer = row + x * 4
                            Dim maskAlpha As Integer = alpha(alphaRow + x)
                            If maskAlpha <= 0 Then
                                raw(pixel) = 0
                                raw(pixel + 1) = 0
                                raw(pixel + 2) = 0
                                raw(pixel + 3) = 0
                            Else
                                raw(pixel + 3) = CByte((CInt(raw(pixel + 3)) * maskAlpha) \ 255)
                            End If
                        Next
                    Next
                    Marshal.Copy(raw, 0, data.Scan0, raw.Length)
                Finally
                    illuminated.UnlockBits(data)
                End Try
            Catch
                ' A corrupt optional per-light mask must not stop normal rendering.
            End Try
        End Sub

        Private Sub BlurMask(ByVal bmp As Bitmap, ByVal radius As Integer)
            radius = Math.Min(25, Math.Max(1, radius))
            Dim w As Integer = bmp.Width
            Dim h As Integer = bmp.Height
            Dim source(w * h - 1) As Integer
            Dim target(w * h - 1) As Integer
            For y As Integer = 0 To h - 1
                For x As Integer = 0 To w - 1
                    source(y * w + x) = bmp.GetPixel(x, y).A
                Next
            Next
            For y As Integer = 0 To h - 1
                For x As Integer = 0 To w - 1
                    Dim total As Integer = 0
                    Dim count As Integer = 0
                    For xx As Integer = Math.Max(0, x - radius) To Math.Min(w - 1, x + radius)
                        total += source(y * w + xx) : count += 1
                    Next
                    target(y * w + x) = total \ count
                Next
            Next
            source = CType(target.Clone(), Integer())
            For y As Integer = 0 To h - 1
                For x As Integer = 0 To w - 1
                    Dim total As Integer = 0
                    Dim count As Integer = 0
                    For yy As Integer = Math.Max(0, y - radius) To Math.Min(h - 1, y + radius)
                        total += source(yy * w + x) : count += 1
                    Next
                    target(y * w + x) = total \ count
                Next
            Next
            For y As Integer = 0 To h - 1
                For x As Integer = 0 To w - 1
                    Dim a As Integer = target(y * w + x)
                    bmp.SetPixel(x, y, Color.FromArgb(a, 255, 255, 255))
                Next
            Next
        End Sub

        Public Function CreateImage(ByVal image As Bitmap,
                                    ByVal rect As Rectangle,
                                    ByVal rectX As Rectangle,
                                    ByVal intensity As Integer,
                                    ByVal lightcolor As Color,
                                    ByVal dodgecolor As Color,
                                    ByVal text As String,
                                    ByVal font As Font,
                                    ByVal textalignment As Illumination.eTextAlignment,
                                    ByVal illumode As Illumination.eIlluMode) As Image

            If image Is Nothing Then Return Nothing

            Dim ret As Image = image.Clone()
            Dim overlay = CreateOverlayImage(image, rect, rectX, intensity, lightcolor, dodgecolor, text, font, textalignment, illumode)
            Using gr As Graphics = Graphics.FromImage(ret)
                gr.DrawImage(overlay, rectX.X, rectX.Y)
            End Using
            overlay.Dispose()
            overlay = Nothing
            Return ret

        End Function

    End Class

End Namespace
