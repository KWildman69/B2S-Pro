Imports System
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Runtime.InteropServices

Namespace Illumination

    ' Dedicated artwork flasher renderer.
    '
    ' The old light renderer was designed to draw synthetic bulb/glow geometry.
    ' This class instead treats a flasher as masked artwork.  The rectangle is
    ' only a storage/cropping boundary and is never used as visible light alpha.
    Public NotInheritable Class ArtworkFlasherRenderer

        Private Sub New()
        End Sub

        Public Shared Function Render(ByVal source As Bitmap,
                                      ByVal absoluteRect As Rectangle,
                                      ByVal selectionMaskData As String,
                                      ByVal selectionFeather As Integer,
                                      ByVal intensity As Integer,
                                      ByVal lightColor As Color,
                                      ByVal flasherStyle As Integer,
                                      ByVal saturation As Integer,
                                      ByVal highlightProtection As Integer,
                                      ByVal darkAreaLift As Integer,
                                      ByVal hotspotX As Integer,
                                      ByVal hotspotY As Integer,
                                      ByVal edgeSoftness As Integer,
                                      ByVal glowSpread As Integer,
                                      ByVal glowStrength As Integer,
                                      Optional ByVal transmissionContrast As Integer = 140,
                                      Optional ByVal maskRadius As Integer = 0,
                                      Optional ByVal smartRadius As Boolean = False,
                                      Optional ByVal maskSmooth As Integer = 0,
                                      Optional ByVal maskFeather As Integer = 0,
                                      Optional ByVal maskContrast As Integer = 0,
                                      Optional ByVal maskShiftEdge As Integer = 0) As Bitmap
            If source Is Nothing OrElse String.IsNullOrEmpty(selectionMaskData) Then Return Nothing
            If source.Width <= 0 OrElse source.Height <= 0 Then Return Nothing

            Dim mask As Byte() = DecodeLocalMask(selectionMaskData, absoluteRect, source.Width, source.Height)
            If mask Is Nothing OrElse mask.Length <> source.Width * source.Height Then Return Nothing

            ApplyPhotoshopMaskRefinement(mask, source.Width, source.Height,
                                         maskRadius, smartRadius, maskSmooth,
                                         maskFeather, maskContrast, maskShiftEdge)
            NormalizeMask(mask, source.Width, source.Height, hotspotX, hotspotY, selectionFeather)
            ApplyNaturalEdgeSoftness(mask, source.Width, source.Height, edgeSoftness)
            If Not HasVisiblePixels(mask) Then Return Nothing

            ' Renderer 1C: one continuous alpha field only.  The selected mask is
            ' expanded and feathered once, then that same field controls both
            ' transmitted brightness and final alpha.  There is no separate core,
            ' center oval, transition ring, or brightness-dependent alpha pass.
            Dim lightField As Byte() = BuildSingleLightField(mask, source.Width, source.Height,
                                                             glowSpread, glowStrength, edgeSoftness)

            ' The expanded/softened light field may shape brightness inside the
            ' selection, but it must never escape the authored mask. Multiplying
            ' by the normalized mask at the final field stage preserves intentional
            ' feathered alpha while guaranteeing that transparent mask pixels stay
            ' completely unlit.
            For i As Integer = 0 To lightField.Length - 1
                lightField(i) = CByte((CInt(lightField(i)) * CInt(mask(i)) + 127) \ 255)
            Next

            Dim transmissionMap As Double() =
                BuildTransmissionMap(source, transmissionContrast)

            Return RenderPixels(source, lightField, transmissionMap,
                                intensity, lightColor, flasherStyle,
                                saturation, highlightProtection, darkAreaLift)
        End Function

        ' Illuminate the real artwork pixels with an existing light alpha field.
        ' This is used by Flasher Light objects that do not have a hand-drawn
        ' Quick Selection mask. The old synthetic light supplies shape/feather
        ' only; its RGB is discarded so the backglass artwork supplies every
        ' visible color and detail pixel.
        Public Shared Function RenderFromAlphaField(ByVal source As Bitmap,
                                                    ByVal lightTemplate As Bitmap,
                                                    ByVal intensity As Integer,
                                                    ByVal lightColor As Color,
                                                    ByVal flasherStyle As Integer,
                                                    ByVal saturation As Integer,
                                                    ByVal highlightProtection As Integer,
                                                    ByVal darkAreaLift As Integer,
                                                    Optional ByVal transmissionContrast As Integer = 140,
                                                    Optional ByVal maskRadius As Integer = 0,
                                                    Optional ByVal maskSmooth As Integer = 0,
                                                    Optional ByVal maskFeather As Integer = 0,
                                                    Optional ByVal maskContrast As Integer = 0,
                                                    Optional ByVal maskShiftEdge As Integer = 0,
                                                    Optional ByVal radialSpikes As Integer = 0) As Bitmap
            If source Is Nothing OrElse lightTemplate Is Nothing Then Return Nothing
            If source.Width <= 0 OrElse source.Height <= 0 Then Return Nothing
            If source.Size <> lightTemplate.Size Then Return Nothing

            Dim width As Integer = source.Width
            Dim height As Integer = source.Height
            Dim lightField(width * height - 1) As Byte
            Dim data As BitmapData = lightTemplate.LockBits(New Rectangle(0, 0, width, height),
                                                            ImageLockMode.ReadOnly,
                                                            PixelFormat.Format32bppArgb)
            Try
                Dim stride As Integer = Math.Abs(data.Stride)
                Dim pixels(stride * height - 1) As Byte
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length)
                For y As Integer = 0 To height - 1
                    Dim row As Integer = y * stride
                    Dim outputRow As Integer = y * width
                    For x As Integer = 0 To width - 1
                        lightField(outputRow + x) = pixels(row + x * 4 + 3)
                    Next
                Next
            Finally
                lightTemplate.UnlockBits(data)
            End Try

            ApplyBoxMaskRefinement(lightField, width, height,
                                   maskRadius, maskSmooth, maskFeather,
                                   maskContrast, maskShiftEdge)
            ApplyRadialSpikes(lightField, width, height, radialSpikes)
            If Not HasVisiblePixels(lightField) Then Return Nothing
            Dim transmissionMap As Double() = BuildTransmissionMap(source, transmissionContrast)
            Return RenderPixels(source, lightField, transmissionMap,
                                intensity, lightColor, flasherStyle,
                                saturation, highlightProtection, darkAreaLift)
        End Function

        ' Box-mode flashers use the glow template as their mask. Scale the
        ' Photoshop-style controls to the current box so a small flasher cannot
        ' be blurred completely away by a large numeric Feather value.
        Private Shared Sub ApplyBoxMaskRefinement(ByVal alpha As Byte(),
                                                  ByVal width As Integer,
                                                  ByVal height As Integer,
                                                  ByVal radius As Integer,
                                                  ByVal smooth As Integer,
                                                  ByVal feather As Integer,
                                                  ByVal contrast As Integer,
                                                  ByVal shiftEdge As Integer)
            If alpha Is Nothing OrElse width <= 0 OrElse height <= 0 Then Return
            Dim minDimension As Integer = Math.Max(1, Math.Min(width, height))

            radius = Math.Max(0, Math.Min(100, radius))
            If radius > 0 Then
                Dim radiusPixels As Integer = Math.Max(1, CInt(Math.Round(minDimension * radius / 1000.0)))
                BlurAlpha(alpha, width, height, radiusPixels)
            End If

            smooth = Math.Max(0, Math.Min(100, smooth))
            If smooth > 0 Then
                Dim smoothPixels As Integer = Math.Max(1, CInt(Math.Round(minDimension * smooth / 2500.0)))
                BlurAlpha(alpha, width, height, smoothPixels)
            End If

            shiftEdge = Math.Max(-100, Math.Min(100, shiftEdge))
            If shiftEdge <> 0 Then
                Dim shiftPixels As Integer = Math.Max(1, CInt(Math.Round(minDimension * Math.Abs(shiftEdge) / 500.0)))
                If shiftEdge > 0 Then
                    DilateAlpha(alpha, width, height, shiftPixels)
                Else
                    For i As Integer = 0 To alpha.Length - 1
                        alpha(i) = CByte(255 - alpha(i))
                    Next
                    DilateAlpha(alpha, width, height, shiftPixels)
                    For i As Integer = 0 To alpha.Length - 1
                        alpha(i) = CByte(255 - alpha(i))
                    Next
                End If
            End If

            feather = Math.Max(0, Math.Min(250, feather))
            If feather > 0 Then
                Dim featherPixels As Integer = Math.Max(1, CInt(Math.Round(minDimension * feather / 1250.0)))
                BlurAlpha(alpha, width, height, featherPixels)
            End If

            contrast = Math.Max(0, Math.Min(100, contrast))
            If contrast > 0 Then
                Dim factor As Double = 1.0 + contrast / 25.0
                For i As Integer = 0 To alpha.Length - 1
                    alpha(i) = CByte(ClampByte(CInt(Math.Round(((alpha(i) / 255.0 - 0.5) * factor + 0.5) * 255.0))))
                Next
            End If

            For i As Integer = 0 To alpha.Length - 1
                If alpha(i) < 3 Then alpha(i) = 0
            Next
        End Sub

        ' Add broad, tapered starburst splashes to the low-alpha outer portion
        ' of a box-mode flasher. The existing artwork remains the RGB source;
        ' this method changes only light alpha, so the effect cannot paint a
        ' white synthetic shape over the backglass. Zero is an exact no-op for
        ' legacy projects. As the authored value rises, each splash grows both
        ' longer and wider, then narrows toward its outer tip.
        Private Shared Sub ApplyRadialSpikes(ByVal alpha As Byte(),
                                             ByVal width As Integer,
                                             ByVal height As Integer,
                                             ByVal amount As Integer)
            If alpha Is Nothing OrElse width <= 2 OrElse height <= 2 Then Return
            amount = Math.Max(0, Math.Min(100, amount))
            If amount = 0 Then Return

            Dim centerX As Double = (width - 1) / 2.0
            Dim centerY As Double = (height - 1) / 2.0
            Dim radiusX As Double = Math.Max(1.0, centerX)
            Dim radiusY As Double = Math.Max(1.0, centerY)
            Dim strength As Double = amount / 100.0
            Dim rayCount As Integer = 8
            ' Anchor the broad base at the center glow. The slider moves the
            ' outer tip: raising it grows the splash away from the circle;
            ' lowering it retracts the tip back into the circle.
            Dim startRadius As Double = 0.35
            Dim endRadius As Double = startRadius + strength * (1.0 - startRadius)
            Dim maximumAlpha As Double = 40.0 + strength * 215.0
            Dim sectorHalfAngle As Double = Math.PI / rayCount
            Dim baseHalfWidth As Double = 0.55 + strength * 0.45

            For y As Integer = 0 To height - 1
                Dim dy As Double = (y - centerY) / radiusY
                For x As Integer = 0 To width - 1
                    Dim index As Integer = y * width + x
                    ' Preserve only the fully saturated center. The splash must
                    ' brighten the existing halo too; otherwise its broad base
                    ' is hidden and only a thin outer tip remains visible.
                    If alpha(index) >= 252 Then Continue For

                    Dim dx As Double = (x - centerX) / radiusX
                    Dim radialDistance As Double = Math.Sqrt(dx * dx + dy * dy)
                    If radialDistance < startRadius OrElse radialDistance > endRadius Then Continue For

                    Dim angle As Double = Math.Atan2(dy, dx)
                    Dim spoke As Double = Math.Abs(Math.Cos(angle * rayCount / 2.0))
                    Dim travel As Double = Math.Max(0.0, Math.Min(1.0,
                        (radialDistance - startRadius) / Math.Max(0.01, endRadius - startRadius)))
                    ' Low values retain restrained rays. At high values the
                    ' inner part opens into a wide splash. Its allowed angular
                    ' width then collapses toward the outside, producing the
                    ' broad-base/pointed-tip profile of a photographic flare.
                    Dim angleFromRay As Double =
                        Math.Acos(Math.Max(0.0, Math.Min(1.0, spoke))) / (rayCount / 2.0)
                    Dim normalizedAngle As Double = angleFromRay / sectorHalfAngle
                    ' Hold the wide base through the halo and taper primarily in
                    ' the outer section. Squaring travel delays the narrowing.
                    Dim allowedHalfWidth As Double = baseHalfWidth *
                        (1.0 - 0.94 * travel * travel)
                    If normalizedAngle >= allowedHalfWidth Then Continue For
                    spoke = 1.0 - normalizedAngle / Math.Max(0.01, allowedHalfWidth)
                    spoke = Math.Pow(spoke, 1.35)
                    Dim alternating As Double = 0.88 + 0.12 * Math.Cos(angle * rayCount / 2.0)
                    Dim rise As Double = Math.Min(1.0, travel / 0.12)
                    Dim taper As Double = rise * Math.Pow(Math.Max(0.0, 1.0 - travel), 0.42)
                    Dim rayAlpha As Integer = CInt(Math.Round(maximumAlpha * spoke * alternating * taper))

                    ' Screen-style alpha blending visibly integrates the broad
                    ' base with an existing glow without flattening its center.
                    Dim existingAlpha As Integer = alpha(index)
                    Dim combinedAlpha As Integer = existingAlpha +
                        CInt(Math.Round(rayAlpha * (255 - existingAlpha) / 255.0))
                    If combinedAlpha > existingAlpha Then
                        alpha(index) = CByte(Math.Min(255, combinedAlpha))
                    End If
                Next
            Next
        End Sub

        Public Shared Function GetSelectionBounds(ByVal selectionMaskData As String,
                                                   ByVal imageSize As Size,
                                                   ByVal searchBounds As Rectangle) As Rectangle
            If String.IsNullOrEmpty(selectionMaskData) OrElse imageSize.Width <= 0 OrElse imageSize.Height <= 0 Then
                Return Rectangle.Empty
            End If

            Try
                Dim rawBytes() As Byte = Convert.FromBase64String(selectionMaskData)
                Using stream As New MemoryStream(rawBytes)
                    Using temp As New Bitmap(stream)
                        Using fullMask As New Bitmap(temp)
                            Dim imageRect As New Rectangle(0, 0, imageSize.Width, imageSize.Height)
                            Dim search As Rectangle = Rectangle.Intersect(searchBounds, imageRect)
                            search.Intersect(New Rectangle(0, 0, fullMask.Width, fullMask.Height))
                            If search.Width <= 0 OrElse search.Height <= 0 Then Return Rectangle.Empty

                            Dim selectedCount As Long = 0
                            Dim totalCount As Long = CLng(search.Width) * search.Height

                            For y As Integer = search.Top To search.Bottom - 1
                                For x As Integer = search.Left To search.Right - 1
                                    If fullMask.GetPixel(x, y).A >= 96 Then selectedCount += 1
                                Next
                            Next

                            Dim inverted As Boolean = selectedCount > totalCount * 0.65

                            Dim minX As Integer = Integer.MaxValue
                            Dim minY As Integer = Integer.MaxValue
                            Dim maxX As Integer = Integer.MinValue
                            Dim maxY As Integer = Integer.MinValue

                            For y As Integer = search.Top To search.Bottom - 1
                                For x As Integer = search.Left To search.Right - 1
                                    Dim opaque As Boolean = fullMask.GetPixel(x, y).A >= 96
                                    Dim selected As Boolean = If(inverted, Not opaque, opaque)
                                    If selected Then
                                        If x < minX Then minX = x
                                        If y < minY Then minY = y
                                        If x > maxX Then maxX = x
                                        If y > maxY Then maxY = y
                                    End If
                                Next
                            Next

                            If minX = Integer.MaxValue Then Return Rectangle.Empty
                            Return Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1)
                        End Using
                    End Using
                End Using
            Catch
                Return Rectangle.Empty
            End Try
        End Function

        Private Shared Function DecodeLocalMask(ByVal maskData As String,
                                                ByVal absoluteRect As Rectangle,
                                                ByVal width As Integer,
                                                ByVal height As Integer) As Byte()
            Try
                Dim rawBytes() As Byte = Convert.FromBase64String(maskData)
                Using stream As New MemoryStream(rawBytes)
                    Using temp As New Bitmap(stream)
                        Using fullMask As New Bitmap(temp)
                            Dim crop As Rectangle = Rectangle.Intersect(absoluteRect, New Rectangle(0, 0, fullMask.Width, fullMask.Height))
                            If crop.Width <= 0 OrElse crop.Height <= 0 Then Return Nothing

                            Using local As New Bitmap(width, height, PixelFormat.Format32bppArgb)
                                Using graphics As Graphics = Graphics.FromImage(local)
                                    graphics.Clear(Color.Transparent)
                                    graphics.CompositingMode = Drawing2D.CompositingMode.SourceCopy
                                    graphics.InterpolationMode = Drawing2D.InterpolationMode.NearestNeighbor
                                    graphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.Half
                                    graphics.DrawImage(fullMask, New Rectangle(0, 0, width, height), crop, GraphicsUnit.Pixel)
                                End Using

                                Dim alpha(width * height - 1) As Byte
                                Dim data As BitmapData = local.LockBits(New Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb)
                                Try
                                    Dim stride As Integer = Math.Abs(data.Stride)
                                    Dim pixels(stride * height - 1) As Byte
                                    Marshal.Copy(data.Scan0, pixels, 0, pixels.Length)
                                    For y As Integer = 0 To height - 1
                                        Dim row As Integer = y * stride
                                        Dim outputRow As Integer = y * width
                                        For x As Integer = 0 To width - 1
                                            alpha(outputRow + x) = pixels(row + x * 4 + 3)
                                        Next
                                    Next
                                Finally
                                    local.UnlockBits(data)
                                End Try
                                Return alpha
                            End Using
                        End Using
                    End Using
                End Using
            Catch
                Return Nothing
            End Try
        End Function

        Private Shared Sub NormalizeMask(ByVal alpha As Byte(),
                                         ByVal width As Integer,
                                         ByVal height As Integer,
                                         ByVal hotspotX As Integer,
                                         ByVal hotspotY As Integer,
                                         ByVal feather As Integer)
            ' Mask alpha is authoritative. Quick Selection already stores the
            ' user's explicit selection, and its Invert Mask command physically
            ' inverts that alpha. Never guess polarity from the hotspot position:
            ' a hotspot outside a valid selection formerly inverted the complete
            ' crop, producing square light blocks and illuminating outside masks.

            ' Kill the faint alpha film responsible for visible rectangular haze.
            ' Values above the threshold are remapped to retain a smooth edge.
            Dim cutoff As Integer = If(feather <= 0, 28, Math.Max(12, 28 - Math.Min(16, feather \ 2)))
            For i As Integer = 0 To alpha.Length - 1
                Dim value As Integer = alpha(i)
                If value <= cutoff Then
                    alpha(i) = 0
                Else
                    alpha(i) = CByte(Math.Min(255, ((value - cutoff) * 255) \ (255 - cutoff)))
                End If
            Next

            If feather > 0 Then
                Dim radiusFeather As Integer = Math.Max(1, Math.Min(12, feather \ 3))
                BlurAlpha(alpha, width, height, radiusFeather)
                ' Blur can reintroduce a nearly invisible rectangle-wide film.
                For i As Integer = 0 To alpha.Length - 1
                    If alpha(i) < 10 Then alpha(i) = 0
                Next
            End If
        End Sub

        Private Shared Sub ApplyPhotoshopMaskRefinement(ByVal alpha As Byte(),
                                                        ByVal width As Integer,
                                                        ByVal height As Integer,
                                                        ByVal radius As Integer,
                                                        ByVal smartRadius As Boolean,
                                                        ByVal smooth As Integer,
                                                        ByVal feather As Integer,
                                                        ByVal contrast As Integer,
                                                        ByVal shiftEdge As Integer)
            If alpha Is Nothing OrElse width <= 0 OrElse height <= 0 Then Return

            radius = Math.Max(0, Math.Min(100, radius))
            If smartRadius Then radius = Math.Max(1, radius \ 2)
            If radius > 0 Then BlurAlpha(alpha, width, height, radius)

            smooth = Math.Max(0, Math.Min(100, smooth))
            If smooth > 0 Then
                Dim smoothRadius As Integer = Math.Max(1, CInt(Math.Round(smooth / 20.0)))
                BlurAlpha(alpha, width, height, smoothRadius)
                BlurAlpha(alpha, width, height, smoothRadius)
            End If

            shiftEdge = Math.Max(-100, Math.Min(100, shiftEdge))
            If shiftEdge <> 0 Then
                Dim shiftPixels As Integer = Math.Max(1, CInt(Math.Round(Math.Min(width, height) * Math.Abs(shiftEdge) / 500.0)))
                If shiftEdge > 0 Then
                    DilateAlpha(alpha, width, height, shiftPixels)
                Else
                    For i As Integer = 0 To alpha.Length - 1
                        alpha(i) = CByte(255 - alpha(i))
                    Next
                    DilateAlpha(alpha, width, height, shiftPixels)
                    For i As Integer = 0 To alpha.Length - 1
                        alpha(i) = CByte(255 - alpha(i))
                    Next
                End If
            End If

            feather = Math.Max(0, Math.Min(250, feather))
            If feather > 0 Then BlurAlpha(alpha, width, height, feather)

            contrast = Math.Max(0, Math.Min(100, contrast))
            If contrast > 0 Then
                Dim factor As Double = 1.0 + contrast / 25.0
                For i As Integer = 0 To alpha.Length - 1
                    alpha(i) = CByte(ClampByte(CInt(Math.Round(((alpha(i) / 255.0 - 0.5) * factor + 0.5) * 255.0))))
                Next
            End If
        End Sub

        Private Shared Sub ApplyNaturalEdgeSoftness(ByVal alpha As Byte(),
                                                          ByVal width As Integer,
                                                          ByVal height As Integer,
                                                          ByVal softness As Integer)
            If alpha Is Nothing OrElse width <= 1 OrElse height <= 1 Then Return
            softness = Math.Max(0, Math.Min(300, softness))
            If softness <= 0 Then Return

            ' A rectangle selection often touches the crop boundary. Fade only the
            ' boundary zone so that the crop box can never become a visible halo.
            Dim softnessScale As Double = softness / 100.0
            ' 100% preserves the previous maximum. 200–300% extend the fade much
            ' farther while keeping a smooth, natural falloff instead of a hard blur.
            Dim fadeFraction As Double = 0.015 + Math.Min(1.0, softnessScale) * 0.10
            If softnessScale > 1.0 Then fadeFraction += (softnessScale - 1.0) * 0.075
            Dim fadeWidth As Integer = Math.Max(2, CInt(Math.Round(Math.Min(width, height) * fadeFraction)))
            fadeWidth = Math.Min(fadeWidth, Math.Max(2, Math.Min(width, height) \ 3))
            For y As Integer = 0 To height - 1
                For x As Integer = 0 To width - 1
                    Dim distanceToEdge As Integer = Math.Min(Math.Min(x, width - 1 - x), Math.Min(y, height - 1 - y))
                    If distanceToEdge < fadeWidth Then
                        Dim t As Double = Math.Max(0.0, Math.Min(1.0, distanceToEdge / CDbl(fadeWidth)))
                        ' Smoothstep gives a natural feather without a straight band.
                        t = t * t * (3.0 - 2.0 * t)
                        Dim index As Integer = y * width + x
                        alpha(index) = CByte(Math.Max(0, Math.Min(255, CInt(alpha(index) * t))))
                    End If
                Next
            Next

            Dim blurRadius As Integer = Math.Max(1, CInt(Math.Round(Math.Min(200, softness) / 28.0)))
            If softness > 100 Then blurRadius += CInt(Math.Round((softness - 100) / 35.0))
            BlurAlpha(alpha, width, height, blurRadius)
            For i As Integer = 0 To alpha.Length - 1
                If alpha(i) < 12 Then alpha(i) = 0
            Next
        End Sub

        Private Shared Function HasVisiblePixels(ByVal alpha As Byte()) As Boolean
            For Each value As Byte In alpha
                If value >= 16 Then Return True
            Next
            Return False
        End Function

        Private Shared Function BuildSingleLightField(ByVal mask As Byte(),
                                                           ByVal width As Integer,
                                                           ByVal height As Integer,
                                                           ByVal spread As Integer,
                                                           ByVal diffusion As Integer,
                                                           ByVal edgeSoftness As Integer) As Byte()
            If mask Is Nothing OrElse mask.Length <> width * height Then Return Nothing
            If width <= 1 OrElse height <= 1 Then Return Nothing

            spread = Math.Max(0, spread)
            diffusion = Math.Max(0, Math.Min(300, diffusion))
            edgeSoftness = Math.Max(0, Math.Min(300, edgeSoftness))

            Dim field(mask.Length - 1) As Byte
            Array.Copy(mask, field, mask.Length)

            Dim minDimension As Integer = Math.Max(1, Math.Min(width, height))
            Dim diffusionScale As Double = diffusion / 100.0
            Dim softnessScale As Double = edgeSoftness / 100.0

            ' Expand only enough to create the outward transmission area.
            Dim expandPixels As Integer =
                CInt(Math.Round(Math.Min(18.0, spread * 0.025) +
                                minDimension * Math.Pow(diffusionScale, 1.08) * 0.010))
            expandPixels = Math.Max(0, Math.Min(Math.Max(2, minDimension \ 8), expandPixels))
            If expandPixels > 0 Then DilateAlpha(field, width, height, expandPixels)

            ' One broad Gaussian-like feather.  Multiple small box passes avoid
            ' visible bands while preserving a single monotonic falloff.
            Dim featherRadius As Integer =
                CInt(Math.Round(minDimension *
                                (0.012 +
                                 Math.Pow(diffusionScale, 1.12) * 0.115 +
                                 Math.Pow(softnessScale, 1.05) * 0.050) +
                                Math.Min(55.0, spread * 0.07)))
            featherRadius = Math.Max(2, Math.Min(Math.Max(8, minDimension \ 3), featherRadius))

            Dim passes As Integer = 5
            Dim passRadius As Integer = Math.Max(1, CInt(Math.Ceiling(featherRadius / CDbl(passes))))
            For pass As Integer = 1 To passes
                BlurAlpha(field, width, height, passRadius)
            Next

            ' Renderer 1D tuning pass: broaden the middle of the existing single
            ' field without adding another glow layer or changing its peak.  The
            ' normalized power curve keeps zero and the current maximum fixed,
            ' while extending the useful transition by roughly 15–20 percent.
            BroadenSingleField(field, 0.84)

            ' Remove only imperceptible haze.  Do not restore the solid mask core;
            ' doing so creates the flat inner oval seen in the previous build.
            For i As Integer = 0 To field.Length - 1
                If field(i) < 3 Then field(i) = 0
            Next

            Return field
        End Function


        Private Shared Sub BroadenSingleField(ByVal field As Byte(),
                                              ByVal curvePower As Double)
            If field Is Nothing OrElse field.Length = 0 Then Return

            Dim maximum As Integer = 0
            For Each value As Byte In field
                If value > maximum Then maximum = value
            Next
            If maximum <= 0 Then Return

            curvePower = Math.Max(0.65, Math.Min(1.0, curvePower))
            For i As Integer = 0 To field.Length - 1
                Dim normalized As Double = field(i) / CDbl(maximum)
                Dim broadened As Double = Math.Pow(normalized, curvePower)
                field(i) = CByte(Math.Max(0, Math.Min(255,
                    CInt(Math.Round(broadened * maximum)))))
            Next
        End Sub

        Private Shared Sub DilateAlpha(ByVal alpha As Byte(),
                                       ByVal width As Integer,
                                       ByVal height As Integer,
                                       ByVal radius As Integer)
            If alpha Is Nothing OrElse radius <= 0 Then Return

            Dim current(alpha.Length - 1) As Byte
            Array.Copy(alpha, current, alpha.Length)

            For pass As Integer = 1 To radius
                Dim nextPass(current.Length - 1) As Byte
                For y As Integer = 0 To height - 1
                    For x As Integer = 0 To width - 1
                        Dim maximum As Integer = 0
                        For yy As Integer = Math.Max(0, y - 1) To Math.Min(height - 1, y + 1)
                            Dim row As Integer = yy * width
                            For xx As Integer = Math.Max(0, x - 1) To Math.Min(width - 1, x + 1)
                                Dim value As Integer = current(row + xx)
                                If value > maximum Then maximum = value
                            Next
                        Next
                        nextPass(y * width + x) = CByte(maximum)
                    Next
                Next
                current = nextPass
            Next

            Array.Copy(current, alpha, alpha.Length)
        End Sub

        Private Shared Function BuildTransitionMask(ByVal mask As Byte(),
                                                          ByVal width As Integer,
                                                          ByVal height As Integer,
                                                          ByVal edgeSoftness As Integer,
                                                          ByVal glowStrength As Integer) As Byte()
            If mask Is Nothing Then Return Nothing

            Dim transition(mask.Length - 1) As Byte
            Array.Copy(mask, transition, mask.Length)

            Dim minDimension As Integer = Math.Max(1, Math.Min(width, height))
            Dim softnessScale As Double = Math.Max(0.0, Math.Min(3.0, edgeSoftness / 100.0))
            Dim diffusionScale As Double = Math.Max(0.0, Math.Min(3.0, glowStrength / 100.0))

            ' About 3–12% of the selected artwork size.  This gives the two
            ' lighting regions a real overlap band without visibly blurring RGB.
            Dim radius As Integer =
                CInt(Math.Round(minDimension *
                                (0.025 + softnessScale * 0.018 + diffusionScale * 0.012)))
            radius = Math.Max(3, Math.Min(Math.Max(6, minDimension \ 7), radius))

            ' Multiple smaller passes produce a smooth, seam-free transition.
            Dim passes As Integer = 3
            Dim passRadius As Integer = Math.Max(1, CInt(Math.Ceiling(radius / CDbl(passes))))
            For pass As Integer = 1 To passes
                BlurAlpha(transition, width, height, passRadius)
            Next

            Return transition
        End Function

        Private Shared Function SmoothStep(ByVal value As Double) As Double
            Dim t As Double = Math.Max(0.0, Math.Min(1.0, value))
            Return t * t * (3.0 - 2.0 * t)
        End Function

        Private Shared Function BuildTransmissionMap(ByVal source As Bitmap,
                                                            ByVal contrastPercent As Integer) As Double()
            Dim width As Integer = source.Width
            Dim height As Integer = source.Height
            Dim count As Integer = width * height
            Dim luminance(count - 1) As Double

            Dim data As BitmapData =
                source.LockBits(New Rectangle(0, 0, width, height),
                                ImageLockMode.ReadOnly,
                                PixelFormat.Format32bppArgb)
            Try
                Dim stride As Integer = Math.Abs(data.Stride)
                Dim pixels(stride * height - 1) As Byte
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length)

                For y As Integer = 0 To height - 1
                    For x As Integer = 0 To width - 1
                        Dim p As Integer = y * stride + x * 4
                        Dim b As Double = pixels(p)
                        Dim g As Double = pixels(p + 1)
                        Dim r As Double = pixels(p + 2)
                        luminance(y * width + x) =
                            Math.Max(0.0, Math.Min(1.0,
                                (r * 0.299 + g * 0.587 + b * 0.114) / 255.0))
                    Next
                Next
            Finally
                source.UnlockBits(data)
            End Try

            ' A small local average supplies the neighborhood reference used for
            ' local contrast. It preserves feather, line and paint detail instead
            ' of turning the outer field into a flat blurred halo.
            Dim localAverage() As Double = BlurDoubleMap(luminance, width, height, 4)

            Dim contrast As Double =
                Math.Max(0.0, Math.Min(3.0, contrastPercent / 100.0))
            Dim detailStrength As Double = 0.45 + contrast * 1.15
            Dim midpointStrength As Double = 0.72 + contrast * 0.28
            Dim result(count - 1) As Double

            For i As Integer = 0 To count - 1
                Dim lum As Double = luminance(i)
                Dim localDetail As Double = lum - localAverage(i)

                ' Preserve the original transmitted luminance, then restore and
                ' strengthen local detail around it. A gentle gamma keeps dark
                ' printed regions from becoming gray fog.
                Dim transmitted As Double =
                    Math.Pow(Math.Max(0.0, lum), 0.82) * midpointStrength +
                    localDetail * detailStrength

                result(i) = Math.Max(0.025, Math.Min(1.0, transmitted))
            Next

            Return result
        End Function

        Private Shared Function BlurDoubleMap(ByVal values As Double(),
                                              ByVal width As Integer,
                                              ByVal height As Integer,
                                              ByVal radius As Integer) As Double()
            If radius <= 0 Then Return CType(values.Clone(), Double())

            Dim horizontal(values.Length - 1) As Double
            Dim output(values.Length - 1) As Double

            For y As Integer = 0 To height - 1
                Dim row As Integer = y * width
                Dim sum As Double = 0.0
                Dim left As Integer = 0
                Dim right As Integer = Math.Min(width - 1, radius)

                For x As Integer = left To right
                    sum += values(row + x)
                Next

                For x As Integer = 0 To width - 1
                    horizontal(row + x) = sum / (right - left + 1)

                    Dim removeX As Integer = x - radius
                    Dim addX As Integer = x + radius + 1
                    If removeX >= 0 Then
                        sum -= values(row + removeX)
                        left += 1
                    End If
                    If addX < width Then
                        sum += values(row + addX)
                        right += 1
                    End If
                Next
            Next

            For x As Integer = 0 To width - 1
                Dim sum As Double = 0.0
                Dim top As Integer = 0
                Dim bottom As Integer = Math.Min(height - 1, radius)

                For y As Integer = top To bottom
                    sum += horizontal(y * width + x)
                Next

                For y As Integer = 0 To height - 1
                    output(y * width + x) = sum / (bottom - top + 1)

                    Dim removeY As Integer = y - radius
                    Dim addY As Integer = y + radius + 1
                    If removeY >= 0 Then
                        sum -= horizontal(removeY * width + x)
                        top += 1
                    End If
                    If addY < height Then
                        sum += horizontal(addY * width + x)
                        bottom += 1
                    End If
                Next
            Next

            Return output
        End Function

        Private Shared Function RenderPixels(ByVal source As Bitmap,
                                             ByVal lightField As Byte(),
                                             ByVal transmissionMap As Double(),
                                             ByVal intensity As Integer,
                                             ByVal lightColor As Color,
                                             ByVal flasherStyle As Integer,
                                             ByVal saturation As Integer,
                                             ByVal highlightProtection As Integer,
                                             ByVal darkAreaLift As Integer) As Bitmap
            Dim width As Integer = source.Width
            Dim height As Integer = source.Height
            Dim result As New Bitmap(width, height, PixelFormat.Format32bppArgb)
            Dim sourceData As BitmapData = source.LockBits(New Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb)
            Dim outputData As BitmapData = result.LockBits(New Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb)
            Try
                Dim sourceStride As Integer = Math.Abs(sourceData.Stride)
                Dim outputStride As Integer = Math.Abs(outputData.Stride)
                Dim sourcePixels(sourceStride * height - 1) As Byte
                Dim outputPixels(outputStride * height - 1) As Byte
                Marshal.Copy(sourceData.Scan0, sourcePixels, 0, sourcePixels.Length)

                ' Lamps are authored through an 0-800% UI range. Flashers may pass
                ' a renderer-only value up to 1600% so their maximum remains visibly
                ' more energetic than a steady lamp at its own 800% ceiling.
                Dim normalizedIntensity As Double = Math.Max(0.0, Math.Min(16.0, intensity / 100.0))
                Dim exposureStrength As Double =
                    normalizedIntensity * 1.18 +
                    Math.Pow(Math.Max(0.0, normalizedIntensity - 3.0), 1.28) * 0.58
                Dim whitePush As Double = Math.Max(0.0, Math.Min(0.82, (normalizedIntensity - 3.75) / 5.0))
                Dim protect As Double = Math.Max(0.0, Math.Min(1.0, highlightProtection / 100.0))
                Dim shadowLift As Double = Math.Max(0.0, Math.Min(1.0, darkAreaLift / 100.0))
                Dim sat As Double = Math.Max(0.0, Math.Min(2.0, saturation / 100.0))

                For y As Integer = 0 To height - 1
                    For x As Integer = 0 To width - 1
                        Dim index As Integer = y * width + x
                        Dim field As Double = If(lightField Is Nothing, 0.0, lightField(index) / 255.0)
                        If field <= 0.0 Then Continue For

                        Dim sp As Integer = y * sourceStride + x * 4
                        Dim dp As Integer = y * outputStride + x * 4
                        Dim b As Double = sourcePixels(sp)
                        Dim g As Double = sourcePixels(sp + 1)
                        Dim r As Double = sourcePixels(sp + 2)
                        Dim sourceAlpha As Double = sourcePixels(sp + 3) / 255.0
                        Dim transmission As Double = If(transmissionMap Is Nothing, 1.0, transmissionMap(index))
                        Dim luminance As Double = (r * 0.299 + g * 0.587 + b * 0.114) / 255.0

                        ' Printed black is the artwork's contrast mask, not a light
                        ' source. Keep true black completely unchanged and ease only
                        ' the anti-aliased near-black edge into the illuminated color.
                        ' Max-channel detection protects dark saturated paint (for
                        ' example deep red or blue) that luminance alone would mistake
                        ' for black.
                        Dim brightestChannel As Double = Math.Max(r, Math.Max(g, b))
                        Dim blackProtection As Double =
                            SmoothStep((brightestChannel - 18.0) / (48.0 - 18.0))
                        If blackProtection <= 0.0 Then Continue For

                        ' The same continuous field scales every lighting operation.
                        ' Brightness changes energy only; it cannot change the field shape.
                        Dim localEnergy As Double = exposureStrength * field

                        ' Renderer 1F final tuning pass: add a very small amount of punch
                        ' only to the brightest 16% of the existing single field.
                        ' SmoothStep keeps the adjustment continuous, so this does
                        ' not create a second hotspot, ring, or visible boundary.
                        Dim punchStart As Double = 0.84
                        Dim punchAmount As Double = SmoothStep((field - punchStart) / (1.0 - punchStart))
                        localEnergy *= 1.0 + punchAmount * 0.075 * (0.45 + transmission * 0.55)

                        Dim response As Double = 0.10 + 0.72 * Math.Sqrt(luminance) + shadowLift * (1.0 - luminance) * 0.18
                        Dim exposure As Double = 1.0 + localEnergy * response
                        exposure = 1.0 + (exposure - 1.0) * (1.0 - protect * luminance * 0.8)

                        Dim rr As Double = ScreenExposure(r, exposure)
                        Dim gg As Double = ScreenExposure(g, exposure)
                        Dim bb As Double = ScreenExposure(b, exposure)

                        Dim transmittedWhite As Double = whitePush * field * (0.35 + transmission * 0.65)
                        If transmittedWhite > 0.0 Then
                            rr += (255.0 - rr) * transmittedWhite
                            gg += (255.0 - gg) * transmittedWhite
                            bb += (255.0 - bb) * transmittedWhite
                        End If

                        Dim detailMix As Double = Math.Min(1.0, field * normalizedIntensity * 0.12)
                        If detailMix > 0.0 Then
                            Dim targetR As Double = lightColor.R + (255.0 - lightColor.R) * whitePush
                            Dim targetG As Double = lightColor.G + (255.0 - lightColor.G) * whitePush
                            Dim targetB As Double = lightColor.B + (255.0 - lightColor.B) * whitePush
                            Dim weighted As Double = detailMix * (0.20 + transmission * 0.80)
                            rr = 255.0 - ((255.0 - rr) * (255.0 - targetR * weighted) / 255.0)
                            gg = 255.0 - ((255.0 - gg) * (255.0 - targetG * weighted) / 255.0)
                            bb = 255.0 - ((255.0 - bb) * (255.0 - targetB * weighted) / 255.0)
                        End If

                        ' Values above the lamp's 800% ceiling exist only for a
                        ' flasher. Convert that additional energy into a brief,
                        ' white-hot photographic burst. At 1600% every fully selected
                        ' non-black pixel reaches true white; the 800-1600 slider range
                        ' is the user's continuous control over that blend. Mask edge
                        ' feathering remains intact, and black was rejected above.
                        Dim highEnergyBurst As Double =
                            SmoothStep((normalizedIntensity - 8.0) / 8.0) * field
                        If highEnergyBurst > 0.0 Then
                            Dim burstMix As Double = highEnergyBurst
                            rr += (255.0 - rr) * burstMix
                            gg += (255.0 - gg) * burstMix
                            bb += (255.0 - bb) * burstMix
                        End If

                        Dim litLum As Double = rr * 0.299 + gg * 0.587 + bb * 0.114
                        rr = litLum + (rr - litLum) * sat
                        gg = litLum + (gg - litLum) * sat
                        bb = litLum + (bb - litLum) * sat

                        Dim finalAlpha As Double = field * sourceAlpha * blackProtection
                        If flasherStyle = 2 Then
                            rr = lightColor.R
                            gg = lightColor.G
                            bb = lightColor.B
                        End If

                        outputPixels(dp) = CByte(ClampByte(CInt(bb)))
                        outputPixels(dp + 1) = CByte(ClampByte(CInt(gg)))
                        outputPixels(dp + 2) = CByte(ClampByte(CInt(rr)))
                        outputPixels(dp + 3) = CByte(ClampByte(CInt(finalAlpha * 255.0)))
                    Next
                Next

                Marshal.Copy(outputPixels, 0, outputData.Scan0, outputPixels.Length)
            Finally
                source.UnlockBits(sourceData)
                result.UnlockBits(outputData)
            End Try
            Return result
        End Function

        Private Shared Function ScreenExposure(ByVal channel As Double, ByVal exposure As Double) As Double
            Dim normalized As Double = Math.Max(0.0, Math.Min(1.0, channel / 255.0))
            Dim value As Double = 1.0 - Math.Pow(1.0 - normalized, Math.Max(1.0, exposure))
            Return value * 255.0
        End Function

        Private Shared Sub BlurAlpha(ByVal alpha As Byte(), ByVal width As Integer, ByVal height As Integer, ByVal radius As Integer)
            If alpha Is Nothing OrElse radius <= 0 Then Return
            Dim horizontal(alpha.Length - 1) As Byte
            Dim output(alpha.Length - 1) As Byte
            Dim window As Integer = radius * 2 + 1

            For y As Integer = 0 To height - 1
                Dim total As Integer = 0
                For x As Integer = -radius To radius
                    total += alpha(y * width + Math.Max(0, Math.Min(width - 1, x)))
                Next
                For x As Integer = 0 To width - 1
                    horizontal(y * width + x) = CByte(total \ window)
                    total += alpha(y * width + Math.Min(width - 1, x + radius + 1))
                    total -= alpha(y * width + Math.Max(0, x - radius))
                Next
            Next

            For x As Integer = 0 To width - 1
                Dim total As Integer = 0
                For y As Integer = -radius To radius
                    total += horizontal(Math.Max(0, Math.Min(height - 1, y)) * width + x)
                Next
                For y As Integer = 0 To height - 1
                    output(y * width + x) = CByte(total \ window)
                    total += horizontal(Math.Min(height - 1, y + radius + 1) * width + x)
                    total -= horizontal(Math.Max(0, y - radius) * width + x)
                Next
            Next
            Array.Copy(output, alpha, alpha.Length)
        End Sub

        Private Shared Function ClampByte(ByVal value As Integer) As Integer
            Return Math.Max(0, Math.Min(255, value))
        End Function

    End Class

End Namespace
