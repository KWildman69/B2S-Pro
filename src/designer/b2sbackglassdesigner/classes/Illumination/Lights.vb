Imports System
Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.Runtime.InteropServices

Namespace Illumination

    Public Class Lights

        Public Event ReportProgress(ByVal sender As Object, ByVal e As LightsProgressEventArgs)
        Public Class LightsProgressEventArgs
            Inherits System.EventArgs

            Public Progress As Integer = 0

            Public Sub New(ByVal _progress As Integer)
                Progress = _progress
            End Sub
        End Class
        Public Class LightColorChangedEventArgs
            Inherits System.EventArgs

            Public Color As Color = Nothing

            Public Sub New(ByVal _color As Color)
                Color = _color
            End Sub
        End Class

        Private lightcreation As Illumination.Create = New Illumination.Create()

        Private parent As B2SPictureBox = Nothing
        Private isDMD As Boolean = False

        Private ReadOnly Property bulbs() As Illumination.BulbCollection
            Get
                Return If(Backglass.currentData IsNot Nothing, If(isDMD, Backglass.currentData.DMDBulbs, Backglass.currentData.Bulbs), Nothing)
            End Get
        End Property

        Public Event ImageIsRendered(ByVal sender As Object, ByVal e As ImageEventArgs)
        Public Class ImageEventArgs
            Inherits EventArgs

            Public Image As Image = Nothing

            Public Sub New(ByVal _image As Image)
                Image = _image
            End Sub
        End Class

        Public Property factor() As Double = 1

        Private newimage As Image = Nothing
        Public ReadOnly Property Image() As Image
            Get
                Return newimage
            End Get
        End Property

        Public Sub PrepareImage(currentimage As Image)
            Static bulbcount As Integer = 0
            If bulbs.Count > 0 AndAlso bulbcount <> bulbs.Count Then
                bulbcount = bulbs.Count
                newimage = New Bitmap(currentimage.Width, currentimage.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb)
                lightcreation.MergeLayers(currentimage, newimage)
                ClearImage()
            End If
        End Sub
        Private Function GetBulbsBackToFront() As Generic.List(Of Illumination.BulbInfo)
            Dim result As New Generic.List(Of Illumination.BulbInfo)()
            If bulbs Is Nothing Then Return result
            For Each bulb As Illumination.BulbInfo In bulbs
                result.Add(bulb)
            Next
            result.Sort(Function(left As Illumination.BulbInfo, right As Illumination.BulbInfo)
                            Dim compare As Integer = left.ZOrder.CompareTo(right.ZOrder)
                            If compare <> 0 Then Return compare

                            ' Objects may intentionally share a Z layer. The collection order
                            ' is their tie-break order: lower collection indexes are visually
                            ' farther forward, so draw higher indexes first (back to front).
                            Dim leftIndex As Integer = bulbs.IndexOf(left)
                            Dim rightIndex As Integer = bulbs.IndexOf(right)
                            Return rightIndex.CompareTo(leftIndex)
                        End Function)
            Return result
        End Function

        Public Sub DrawImage(currentimage As Image, _
                             Optional ByVal rominfofilter As String = "")
            If bulbs.Count > 0 Then
                newimage = New Bitmap(currentimage.Width, currentimage.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb)
                Using gr As Graphics = Graphics.FromImage(newimage)
                    gr.Clear(Color.Transparent)
                    gr.SmoothingMode = SmoothingMode.HighQuality
                    gr.TextRenderingHint = Drawing.Text.TextRenderingHint.AntiAliasGridFit
                    For Each bulb As Illumination.BulbInfo In GetBulbsBackToFront()
                        With bulb
                            Dim getin As Boolean = (String.IsNullOrEmpty(rominfofilter) OrElse .RomInfo2String.Equals(rominfofilter))
                            getin = getin AndAlso Not .IsImageSnippit
                            If getin Then
                                If Not String.IsNullOrEmpty(.Text) Then
                                    Dim font As Font = New Font(.FontName, .FontSize, .FontStyle)
                                    lightcreation.DrawLight(gr, New Rectangle(.Location, .Size), .Text, font, .TextAlignment, .IlluMode, .GlowSoftness, .GlowFalloff, .GlowIntensity)
                                    font.Dispose()
                                Else
                                    lightcreation.DrawLight(gr, New Rectangle(.Location, .Size), , , , .IlluMode)
                                End If
                            End If
                        End With
                    Next
                End Using
                lightcreation.MergeLayers(currentimage, newimage)
                Using gr As Graphics = Graphics.FromImage(newimage)
                    For Each bulb As Illumination.BulbInfo In GetBulbsBackToFront()
                        With bulb
                            Dim getin As Boolean = (String.IsNullOrEmpty(rominfofilter) OrElse .RomInfo2String.Equals(rominfofilter))
                            getin = (getin AndAlso .IsImageSnippit AndAlso .Image IsNot Nothing)
                            If getin Then
                                Dim brightness As Single = CSng(Math.Max(0, Math.Min(200, .SnippitInfo.Brightness)) / 100.0R)
                                If brightness = 1.0F Then
                                    gr.DrawImage(.Image, New Rectangle(.Location, .Size))
                                Else
                                    Using attributes As New ImageAttributes()
                                        Dim matrix As New ColorMatrix()
                                        matrix.Matrix00 = brightness
                                        matrix.Matrix11 = brightness
                                        matrix.Matrix22 = brightness
                                        attributes.SetColorMatrix(matrix)
                                        gr.DrawImage(.Image, New Rectangle(.Location, .Size), 0, 0, .Image.Width, .Image.Height, GraphicsUnit.Pixel, attributes)
                                    End Using
                                End If
                            End If
                        End With
                    Next
                End Using
            Else
                newimage = Nothing
            End If
        End Sub
        Public Sub ClearImage()
            If newimage IsNot Nothing Then
                newimage.Dispose()
                newimage = Nothing
            End If
        End Sub

        Private newimages As Generic.SortedList(Of Integer, ImageInfo) = New Generic.SortedList(Of Integer, ImageInfo)
        Public Class ImageInfo
            Public BulbID As Integer = -1
            Public Name As String = String.Empty
            Public Image As Image = Nothing
            Public OffImage As Image = Nothing
            Public Rectangle As Rectangle = Nothing
            Public Disposable As Boolean = True

            Public Sub New(ByVal _bulbid As Integer, ByVal _name As String, ByVal _image As Image, ByVal _offimage As Image, ByVal _rectangle As Rectangle, ByVal _disposable As Boolean)
                BulbID = _bulbid
                Name = _name
                Image = _image
                OffImage = _offimage
                Rectangle = _rectangle
                Disposable = _disposable
            End Sub
        End Class
        Public ReadOnly Property Images() As Generic.SortedList(Of Integer, ImageInfo)
            Get
                Return newimages
            End Get
        End Property
        Public ReadOnly Property OrderedImages() As Generic.List(Of Generic.KeyValuePair(Of Integer, ImageInfo))
            Get
                Dim result As New Generic.List(Of Generic.KeyValuePair(Of Integer, ImageInfo))()
                For Each pair As Generic.KeyValuePair(Of Integer, ImageInfo) In newimages
                    result.Add(pair)
                Next
                result.Sort(Function(left As Generic.KeyValuePair(Of Integer, ImageInfo), right As Generic.KeyValuePair(Of Integer, ImageInfo))
                                Dim leftZ As Integer = GetBulbZOrder(left.Value.BulbID)
                                Dim rightZ As Integer = GetBulbZOrder(right.Value.BulbID)
                                Dim compare As Integer = leftZ.CompareTo(rightZ)
                                If compare <> 0 Then Return compare
                                Return left.Key.CompareTo(right.Key)
                            End Function)
                Return result
            End Get
        End Property

        Private Function GetBulbZOrder(ByVal bulbID As Integer) As Integer
            If bulbs Is Nothing Then Return 0
            For Each bulb As Illumination.BulbInfo In bulbs
                If bulb.ID = bulbID Then Return bulb.ZOrder
            Next
            Return 0
        End Function
        Private Sub DisposeImageInfo(ByVal info As ImageInfo)
            If info Is Nothing OrElse Not info.Disposable Then Return
            If info.Image IsNot Nothing Then
                info.Image.Dispose()
                info.Image = Nothing
            End If
            If info.OffImage IsNot Nothing Then
                info.OffImage.Dispose()
                info.OffImage = Nothing
            End If
        End Sub

        Private Sub RemoveCachedImage(ByVal id As Integer)
            If Not newimages.ContainsKey(id) Then Return
            Dim info As ImageInfo = newimages(id)
            newimages.Remove(id)
            DisposeImageInfo(info)
        End Sub

        Private Function GetImageByID(ByVal id As Integer) As Image
            Dim ret As Image = Nothing
            For Each imageinfo As KeyValuePair(Of Integer, ImageInfo) In newimages
                If imageinfo.Value.BulbID = id Then
                    ret = imageinfo.Value.Image
                    Exit For
                End If
            Next
            Return ret
        End Function

        Public Shared Function CreateBrightnessAdjustedSnippet(ByVal source As Image, ByVal brightness As Integer) As Image
            If source Is Nothing Then Return Nothing
            brightness = Math.Max(0, Math.Min(200, brightness))
            If brightness = 100 Then Return source

            Dim adjusted As New Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb)
            Using graphics As Graphics = Graphics.FromImage(adjusted), attributes As New ImageAttributes()
                graphics.Clear(Color.Transparent)
                graphics.CompositingMode = CompositingMode.SourceCopy
                Dim scale As Single = CSng(brightness / 100.0R)
                Dim matrix As New ColorMatrix()
                matrix.Matrix00 = scale
                matrix.Matrix11 = scale
                matrix.Matrix22 = scale
                matrix.Matrix33 = 1.0F
                attributes.SetColorMatrix(matrix)
                graphics.DrawImage(source, New Rectangle(0, 0, adjusted.Width, adjusted.Height), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes)
            End Using
            Return adjusted
        End Function

        ' A DirectB2S picture box is always displayed above the form background.
        ' To represent a snippet physically behind transparent backglass artwork,
        ' keep only the snippet pixels that fall beneath transparent/partially
        ' transparent canvas pixels. The exported object stays local-sized and
        ' never duplicates the full background, avoiding scaling seams.
        Public Shared Function CreateCanvasClippedSnippet(ByVal source As Image,
                                                          ByVal canvas As Image,
                                                          ByVal absoluteRectangle As Rectangle) As Bitmap
            If source Is Nothing OrElse canvas Is Nothing OrElse absoluteRectangle.Width <= 0 OrElse absoluteRectangle.Height <= 0 Then Return Nothing

            Dim rendered As New Bitmap(absoluteRectangle.Width, absoluteRectangle.Height, PixelFormat.Format32bppArgb)
            Dim canvasBitmap As Bitmap = TryCast(canvas, Bitmap)
            Dim convertedCanvas As Bitmap = Nothing
            Try
                Using graphics As Graphics = Graphics.FromImage(rendered)
                    graphics.Clear(Color.Transparent)
                    graphics.CompositingMode = CompositingMode.SourceCopy
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality
                    graphics.DrawImage(source, New Rectangle(0, 0, rendered.Width, rendered.Height), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel)
                End Using
                If canvasBitmap Is Nothing OrElse canvasBitmap.PixelFormat <> PixelFormat.Format32bppArgb Then
                    convertedCanvas = New Bitmap(canvas.Width, canvas.Height, PixelFormat.Format32bppArgb)
                    Using graphics As Graphics = Graphics.FromImage(convertedCanvas)
                        graphics.Clear(Color.Transparent)
                        graphics.CompositingMode = CompositingMode.SourceCopy
                        graphics.DrawImage(canvas, New Rectangle(0, 0, canvas.Width, canvas.Height), 0, 0, canvas.Width, canvas.Height, GraphicsUnit.Pixel)
                    End Using
                    canvasBitmap = convertedCanvas
                End If

                Dim bounds As New Rectangle(0, 0, rendered.Width, rendered.Height)
                Dim renderedData As BitmapData = rendered.LockBits(bounds, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb)
                Dim canvasBounds As New Rectangle(0, 0, canvasBitmap.Width, canvasBitmap.Height)
                Dim canvasData As BitmapData = canvasBitmap.LockBits(canvasBounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb)
                Try
                    Dim renderedStride As Integer = Math.Abs(renderedData.Stride)
                    Dim canvasStride As Integer = Math.Abs(canvasData.Stride)
                    Dim renderedRaw(renderedStride * rendered.Height - 1) As Byte
                    Dim canvasRaw(canvasStride * canvasBitmap.Height - 1) As Byte
                    Marshal.Copy(renderedData.Scan0, renderedRaw, 0, renderedRaw.Length)
                    Marshal.Copy(canvasData.Scan0, canvasRaw, 0, canvasRaw.Length)
                    For y As Integer = 0 To rendered.Height - 1
                        Dim renderedRow As Integer = If(renderedData.Stride >= 0, y, rendered.Height - 1 - y) * renderedStride
                        Dim canvasY As Integer = absoluteRectangle.Y + y
                        For x As Integer = 0 To rendered.Width - 1
                            Dim renderedPixel As Integer = renderedRow + x * 4
                            Dim canvasX As Integer = absoluteRectangle.X + x
                            Dim transmission As Integer = 0
                            If canvasX >= 0 AndAlso canvasY >= 0 AndAlso canvasX < canvasBitmap.Width AndAlso canvasY < canvasBitmap.Height Then
                                Dim canvasRow As Integer = If(canvasData.Stride >= 0, canvasY, canvasBitmap.Height - 1 - canvasY) * canvasStride
                                Dim canvasPixel As Integer = canvasRow + canvasX * 4
                                transmission = 255 - CInt(canvasRaw(canvasPixel + 3))
                            End If
                            renderedRaw(renderedPixel + 3) = CByte((CInt(renderedRaw(renderedPixel + 3)) * transmission + 127) \ 255)
                        Next
                    Next
                    Marshal.Copy(renderedRaw, 0, renderedData.Scan0, renderedRaw.Length)
                Finally
                    canvasBitmap.UnlockBits(canvasData)
                    rendered.UnlockBits(renderedData)
                End Try
                Return rendered
            Catch
                rendered.Dispose()
                Return Nothing
            Finally
                If convertedCanvas IsNot Nothing Then convertedCanvas.Dispose()
            End Try
        End Function

        ' Selection masks are stored in full-canvas coordinates.  Snippets keep
        ' their normal local image and runtime rectangle; only their alpha is
        ' clipped to the portion of the shared mask beneath that rectangle.
        ' An empty mask stays on the original snippet path.
        Public Shared Function CreateSelectionMaskedSnippet(ByVal source As Image,
                                                             ByVal selectionMaskData As String,
                                                             ByVal absoluteRectangle As Rectangle) As Bitmap
            If source Is Nothing OrElse String.IsNullOrEmpty(selectionMaskData) OrElse
               absoluteRectangle.Width <= 0 OrElse absoluteRectangle.Height <= 0 Then Return Nothing

            Dim rendered As New Bitmap(absoluteRectangle.Width, absoluteRectangle.Height, PixelFormat.Format32bppArgb)
            Dim maskBitmap As Bitmap = Nothing
            Try
                Using maskStream As New IO.MemoryStream(Convert.FromBase64String(selectionMaskData))
                    Using decodedMask As New Bitmap(maskStream)
                        maskBitmap = New Bitmap(decodedMask.Width, decodedMask.Height, PixelFormat.Format32bppArgb)
                        Using maskGraphics As Graphics = Graphics.FromImage(maskBitmap)
                            maskGraphics.Clear(Color.Transparent)
                            maskGraphics.CompositingMode = CompositingMode.SourceCopy
                            maskGraphics.DrawImageUnscaled(decodedMask, 0, 0)
                        End Using
                    End Using
                End Using

                Using graphics As Graphics = Graphics.FromImage(rendered)
                    graphics.Clear(Color.Transparent)
                    graphics.CompositingMode = CompositingMode.SourceCopy
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality
                    graphics.DrawImage(source, New Rectangle(0, 0, rendered.Width, rendered.Height), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel)
                End Using

                Dim bounds As New Rectangle(0, 0, rendered.Width, rendered.Height)
                Dim renderedData As BitmapData = rendered.LockBits(bounds, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb)
                Dim maskBounds As New Rectangle(0, 0, maskBitmap.Width, maskBitmap.Height)
                Dim maskData As BitmapData = maskBitmap.LockBits(maskBounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb)
                Try
                    Dim renderedStride As Integer = Math.Abs(renderedData.Stride)
                    Dim maskStride As Integer = Math.Abs(maskData.Stride)
                    Dim renderedRaw(renderedStride * rendered.Height - 1) As Byte
                    Dim maskRaw(maskStride * maskBitmap.Height - 1) As Byte
                    Marshal.Copy(renderedData.Scan0, renderedRaw, 0, renderedRaw.Length)
                    Marshal.Copy(maskData.Scan0, maskRaw, 0, maskRaw.Length)
                    For y As Integer = 0 To rendered.Height - 1
                        Dim renderedRow As Integer = If(renderedData.Stride >= 0, y, rendered.Height - 1 - y) * renderedStride
                        Dim maskY As Integer = absoluteRectangle.Y + y
                        For x As Integer = 0 To rendered.Width - 1
                            Dim renderedPixel As Integer = renderedRow + x * 4
                            Dim maskX As Integer = absoluteRectangle.X + x
                            Dim maskAlpha As Integer = 0
                            If maskX >= 0 AndAlso maskY >= 0 AndAlso maskX < maskBitmap.Width AndAlso maskY < maskBitmap.Height Then
                                Dim maskRow As Integer = If(maskData.Stride >= 0, maskY, maskBitmap.Height - 1 - maskY) * maskStride
                                maskAlpha = maskRaw(maskRow + maskX * 4 + 3)
                            End If
                            renderedRaw(renderedPixel + 3) = CByte((CInt(renderedRaw(renderedPixel + 3)) * maskAlpha + 127) \ 255)
                        Next
                    Next
                    Marshal.Copy(renderedRaw, 0, renderedData.Scan0, renderedRaw.Length)
                Finally
                    maskBitmap.UnlockBits(maskData)
                    rendered.UnlockBits(renderedData)
                End Try
                Return rendered
            Catch
                rendered.Dispose()
                Return Nothing
            Finally
                If maskBitmap IsNot Nothing Then maskBitmap.Dispose()
            End Try
        End Function

        Public Sub DrawImages(ByVal currentimage As Image,
                              Optional ByVal currentoffimage As Image = Nothing,
                              Optional ByVal onlyrenderalwaysonlights As Boolean = False,
                              Optional ByVal onlyrenderonlights As Boolean = False,
                              Optional ByVal rominfofilter As String = "")
            Cursor.Current = Cursors.WaitCursor
            RaiseEvent ReportProgress(Me, New LightsProgressEventArgs(0))
            Dim progress As Integer = 0

            ' 3.0.10: export safety. A project can occasionally retain an empty
            ' light entry after intensive add/delete/paste editing. Older drawing
            ' code dereferenced that entry and stopped DirectB2S processing with a
            ' NullReferenceException. Ignore incomplete entries; valid lights and
            ' snippets continue to render normally.
            If currentimage Is Nothing Then
                RaiseEvent ReportProgress(Me, New LightsProgressEventArgs(100))
                Cursor.Current = Cursors.Default
                Return
            End If

            'ClearImages()
            If bulbs.Count > 0 Then
                ' 2.8.8: release cached renderings for bulbs that were deleted.
                Dim liveIDs As New Generic.HashSet(Of Integer)()
                For Each liveBulb As Illumination.BulbInfo In bulbs
                    If liveBulb IsNot Nothing Then liveIDs.Add(liveBulb.ID)
                Next
                For i As Integer = newimages.Count - 1 To 0 Step -1
                    Dim cachedID As Integer = newimages.Keys(i)
                    If Not liveIDs.Contains(cachedID) Then RemoveCachedImage(cachedID)
                Next

                progress += 1
                RaiseEvent ReportProgress(Me, New LightsProgressEventArgs(progress / bulbs.Count))
                For Each bulb As Illumination.BulbInfo In bulbs
                    If bulb Is Nothing Then Continue For
                    With bulb
                        If Not newimages.ContainsKey(.ID) OrElse .IsIlluminatedImageDirty Then
                            If .IsIlluminatedImageDirty Then
                                RemoveCachedImage(.ID)
                            End If
                            Dim getin As Boolean = ((String.IsNullOrEmpty(rominfofilter) OrElse rominfofilter.Equals(.B2SInfo2String) OrElse rominfofilter.Equals(.RomInfo2String)) OrElse
                                                    (rominfofilter.Equals("withoutid") AndAlso ((Backglass.currentData.CommType = eCommType.B2S AndAlso String.IsNullOrEmpty(.B2SInfo2String)) OrElse (Backglass.currentData.CommType = eCommType.Rom AndAlso String.IsNullOrEmpty(.RomInfo2String)))) OrElse
                                                    (rominfofilter.Equals("withname") AndAlso Not String.IsNullOrEmpty(.Name)) OrElse
                                                    (rominfofilter.Equals("off") AndAlso .InitialState = 0) OrElse
                                                    (rominfofilter.Equals("on") AndAlso .InitialState = 1) OrElse
                                                    (rominfofilter.Equals("alwayson") AndAlso .InitialState = 2) OrElse
                                                    (rominfofilter.Equals("authentic") AndAlso .DualMode <> eDualMode.Fantasy) OrElse
                                                    (rominfofilter.Equals("fantasy") AndAlso .DualMode <> eDualMode.Authentic))
                            getin = (getin AndAlso ((Not onlyrenderalwaysonlights AndAlso Not onlyrenderonlights) OrElse
                                                    (onlyrenderalwaysonlights AndAlso .InitialState = 2 AndAlso Not (.BlinkEnabled AndAlso .IlluMode <> eIlluMode.Flasher AndAlso Not .IsImageSnippit)) OrElse
                                                    (onlyrenderonlights AndAlso .InitialState >= 1)))
                            If getin Then
                                Dim imageinfo As ImageInfo = Nothing
                                Dim illuimage As Image = GetImageByID(.ID)
                                If illuimage IsNot Nothing AndAlso Not .IsImageSnippit AndAlso .IsIlluminatedImageDirty Then
                                    illuimage.Dispose()
                                    illuimage = Nothing
                                End If
                                If Not (.IsImageSnippit AndAlso .Image IsNot Nothing) Then
                                    Dim rect As Rectangle = New Rectangle(.Location, .Size)
                                    Dim rectX As Rectangle = New Rectangle(.LocationX, .SizeX)
                                    If .GlowSpread > 0 AndAlso rect.Width > 0 AndAlso rect.Height > 0 Then
                                        Dim scaleX As Double = If(rect.Width > 0, rectX.Width / CDbl(rect.Width), 1.0)
                                        Dim scaleY As Double = If(rect.Height > 0, rectX.Height / CDbl(rect.Height), 1.0)
                                        rect.Inflate(.GlowSpread, .GlowSpread)
                                        rectX.Inflate(CInt(Math.Round(.GlowSpread * scaleX)), CInt(Math.Round(.GlowSpread * scaleY)))
                                    End If
                                    rectX = Illumination.Create.RotatedLightBounds(rectX, .LightRotationAngle)
                                    rectX.Intersect(New Rectangle(0, 0, currentimage.Width, currentimage.Height))
                                    If rectX.Width > 0 AndAlso rectX.Height > 0 Then
                                        If Not String.IsNullOrEmpty(.Text) Then
                                            Dim font As Font = New Font(.FontName, .FontSize, .FontStyle)
                                            Dim lowerintensity As Integer = Math.Max(.Intensity - 2, 1)
                                            If .DodgeColor <> Nothing Then lowerintensity = 3
                                            'Dim lowerintensity As Integer = 1
                                            If illuimage Is Nothing Then illuimage = lightcreation.CreateOverlayImage(currentimage, rect, rectX, .Intensity, .LightColor, .DodgeColor, .Text, font, .TextAlignment, .IlluMode, .GlowSoftness, .GlowFalloff, .GlowIntensity, .SelectionMaskData, .SelectionFeather, .GlobalMaskLayerExplicit AndAlso Not .InFrontOfGlobalMask, .FlasherStyle, .FlasherSaturation, .FlasherHighlightProtection, .FlasherDarkAreaLift, .FlasherHotspotX, .FlasherHotspotY, .LightDiffusion, .LightTemperature, .LightPurpose = eLightPurpose.Flasher, .ArtworkContrast, .MaskRadius, .MaskSmartRadius, .MaskSmooth, .MaskFeather, .MaskContrast, .MaskShiftEdge, .FlasherRadialSpikes, .LightRotationAngle)
                                            Dim offimage As Image = If(currentoffimage IsNot Nothing, lightcreation.CreateOverlayImage(currentoffimage, rect, rectX, lowerintensity, .LightColor, Nothing, .Text, font, .TextAlignment, .IlluMode, .GlowSoftness, .GlowFalloff, .GlowIntensity, .SelectionMaskData, .SelectionFeather, .GlobalMaskLayerExplicit AndAlso Not .InFrontOfGlobalMask, .FlasherStyle, .FlasherSaturation, .FlasherHighlightProtection, .FlasherDarkAreaLift, .FlasherHotspotX, .FlasherHotspotY, .LightDiffusion, .LightTemperature, .LightPurpose = eLightPurpose.Flasher, .ArtworkContrast, .MaskRadius, .MaskSmartRadius, .MaskSmooth, .MaskFeather, .MaskContrast, .MaskShiftEdge, .FlasherRadialSpikes, .LightRotationAngle), Nothing)
                                            If .LightBehindCanvas Then
                                                illuimage = ClipLightBehindCanvas(illuimage, currentimage, rectX)
                                                offimage = ClipLightBehindCanvas(offimage, If(currentoffimage, currentimage), rectX)
                                            End If
                                            imageinfo = New ImageInfo(.ID, .Name, illuimage,
                                                                             offimage,
                                                                             rectX,
                                                                             True)
                                            font.Dispose()
                                        Else
                                            Dim lowerintensity As Integer = Math.Max(.Intensity - 2, 1)
                                            If .DodgeColor <> Nothing Then lowerintensity = 3
                                            'Dim lowerintensity As Integer = 1
                                            If illuimage Is Nothing Then illuimage = lightcreation.CreateOverlayImage(currentimage, rect, rectX, .Intensity, .LightColor, .DodgeColor, "", Nothing, eTextAlignment.Center, .IlluMode, .GlowSoftness, .GlowFalloff, .GlowIntensity, .SelectionMaskData, .SelectionFeather, .GlobalMaskLayerExplicit AndAlso Not .InFrontOfGlobalMask, .FlasherStyle, .FlasherSaturation, .FlasherHighlightProtection, .FlasherDarkAreaLift, .FlasherHotspotX, .FlasherHotspotY, .LightDiffusion, .LightTemperature, .LightPurpose = eLightPurpose.Flasher, .ArtworkContrast, .MaskRadius, .MaskSmartRadius, .MaskSmooth, .MaskFeather, .MaskContrast, .MaskShiftEdge, .FlasherRadialSpikes, .LightRotationAngle)
                                            Dim offimage As Image = If(currentoffimage IsNot Nothing, lightcreation.CreateOverlayImage(currentoffimage, rect, rectX, lowerintensity, .LightColor, Nothing, "", Nothing, eTextAlignment.Center, .IlluMode, .GlowSoftness, .GlowFalloff, .GlowIntensity, .SelectionMaskData, .SelectionFeather, .GlobalMaskLayerExplicit AndAlso Not .InFrontOfGlobalMask, .FlasherStyle, .FlasherSaturation, .FlasherHighlightProtection, .FlasherDarkAreaLift, .FlasherHotspotX, .FlasherHotspotY, .LightDiffusion, .LightTemperature, .LightPurpose = eLightPurpose.Flasher, .ArtworkContrast, .MaskRadius, .MaskSmartRadius, .MaskSmooth, .MaskFeather, .MaskContrast, .MaskShiftEdge, .FlasherRadialSpikes, .LightRotationAngle), Nothing)
                                            If .LightBehindCanvas Then
                                                illuimage = ClipLightBehindCanvas(illuimage, currentimage, rectX)
                                                offimage = ClipLightBehindCanvas(offimage, If(currentoffimage, currentimage), rectX)
                                            End If
                                            imageinfo = New ImageInfo(.ID, .Name, illuimage,
                                                                             offimage,
                                                                             rectX,
                                                                             True)
                                        End If
                                    End If
                                Else
                                    Dim rect As Rectangle = New Rectangle(.Location, .Size)
                                    Dim adjustedSnippet As Image = CreateBrightnessAdjustedSnippet(.Image, .SnippitInfo.Brightness)
                                    If Not String.IsNullOrEmpty(.SelectionMaskData) Then
                                        Dim maskedSnippet As Image = CreateSelectionMaskedSnippet(adjustedSnippet, .SelectionMaskData, rect)
                                        If maskedSnippet IsNot Nothing Then
                                            If Not Object.ReferenceEquals(adjustedSnippet, .Image) Then adjustedSnippet.Dispose()
                                            adjustedSnippet = maskedSnippet
                                        End If
                                    End If
                                    If .SnippitInfo.BehindCanvas Then
                                        Dim clippedSnippet As Image = CreateCanvasClippedSnippet(adjustedSnippet, currentimage, rect)
                                        If clippedSnippet IsNot Nothing Then
                                            If Not Object.ReferenceEquals(adjustedSnippet, .Image) Then adjustedSnippet.Dispose()
                                            adjustedSnippet = clippedSnippet
                                        End If
                                    End If
                                    If illuimage Is Nothing Then illuimage = adjustedSnippet
                                    imageinfo = New ImageInfo(.ID, .Name, illuimage,
                                                                     Nothing,
                                                                     rect,
                                                                     Not Object.ReferenceEquals(illuimage, .Image))
                                End If
                                ' maybe add newimages entry
                                If imageinfo IsNot Nothing AndAlso imageinfo.Image IsNot Nothing Then
                                    newimages.Add(.ID, imageinfo)
                                    .IsIlluminatedImageDirty = False
                                ElseIf imageinfo IsNot Nothing Then
                                    DisposeImageInfo(imageinfo)
                                End If
                            End If
                        End If
                    End With
                Next
            End If
            RaiseEvent ReportProgress(Me, New LightsProgressEventArgs(100))
            Cursor.Current = Cursors.Default
        End Sub

        Private Shared Function ClipLightBehindCanvas(ByVal lightImage As Image,
                                                       ByVal canvas As Image,
                                                       ByVal absoluteRectangle As Rectangle) As Image
            If lightImage Is Nothing OrElse canvas Is Nothing Then Return lightImage
            Dim clipped As Bitmap = CreateCanvasClippedSnippet(lightImage, canvas, absoluteRectangle)
            If clipped Is Nothing Then Return lightImage
            lightImage.Dispose()
            Return clipped
        End Function

        Public Sub ClearImages()
            For i As Integer = newimages.Count - 1 To 0 Step -1
                DisposeImageInfo(newimages(newimages.Keys(i)))
            Next
            newimages.Clear()
        End Sub

        Public Function DrawIlluminatedReelImage(ByVal reelimage As Image,
                                                 ByVal reelintensity As Integer,
                                                 ByVal reelillulocation As eReelIlluminationLocation) As Image
            If reelillulocation = eReelIlluminationLocation.Off OrElse reelintensity <= 0 Then
                Return reelimage
            End If

            ' Rear illumination is transmission through the reel artwork, not a
            ' white lamp painted over it. Apply a bounded exposure curve to the
            ' existing RGB values so black digits and outlines remain black while
            ' translucent midtones brighten progressively through the 0-100 range.
            Dim result As New Bitmap(reelimage.Width, reelimage.Height, PixelFormat.Format32bppArgb)
            Using graphics As Graphics = Graphics.FromImage(result)
                graphics.Clear(Color.Transparent)
                graphics.CompositingMode = CompositingMode.SourceCopy
                graphics.DrawImage(reelimage, New Rectangle(0, 0, result.Width, result.Height))
            End Using

            Dim bounds As New Rectangle(0, 0, result.Width, result.Height)
            Dim data As BitmapData = result.LockBits(bounds, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb)
            Try
                Dim stride As Integer = Math.Abs(data.Stride)
                Dim raw(stride * result.Height - 1) As Byte
                Marshal.Copy(data.Scan0, raw, 0, raw.Length)
                Dim baseGain As Double = Math.Min(100, Math.Max(0, reelintensity)) * 0.14

                For y As Integer = 0 To result.Height - 1
                    Dim vertical As Double = If(result.Height <= 1, 0.5, y / CDbl(result.Height - 1))
                    Dim topLight As Double = Math.Max(0.0, 1.0 - vertical / 0.72)
                    Dim bottomLight As Double = Math.Max(0.0, 1.0 - (1.0 - vertical) / 0.72)
                    Dim influence As Double
                    Select Case reelillulocation
                        Case eReelIlluminationLocation.Above
                            influence = topLight
                        Case eReelIlluminationLocation.Below
                            influence = bottomLight
                        Case eReelIlluminationLocation.AboveAndBelow
                            influence = Math.Max(topLight, bottomLight)
                        Case Else
                            influence = 0.0
                    End Select
                    ' Smooth the transition so the source reads as a lamp behind
                    ' the reel instead of a hard horizontal brightness band.
                    influence = influence * influence * (3.0 - 2.0 * influence)
                    Dim gain As Double = baseGain * influence
                    If gain <= 0.0 Then Continue For

                    Dim row As Integer = If(data.Stride >= 0, y, result.Height - 1 - y) * stride
                    For x As Integer = 0 To result.Width - 1
                        Dim pixel As Integer = row + x * 4
                        Dim luminance As Double = (CInt(raw(pixel + 2)) * 299.0 + CInt(raw(pixel + 1)) * 587.0 + CInt(raw(pixel)) * 114.0) / 1000.0
                        ' Rear light passes through the light/translucent reel stock.
                        ' Black and very dark ink stays completely unchanged; the
                        ' transition to full illumination is softened to preserve
                        ' antialiased digit and outline edges.
                        If luminance <= 64.0 Then Continue For
                        Dim lightPixel As Double = Math.Min(1.0, (luminance - 64.0) / 96.0)
                        lightPixel = lightPixel * lightPixel * (3.0 - 2.0 * lightPixel)
                        Dim pixelGain As Double = gain * lightPixel
                        For channel As Integer = 0 To 2
                            Dim source As Double = raw(pixel + channel) / 255.0
                            ' Bounded exposure: preserves zero/black and retains
                            ' separation between neighboring tones at high settings.
                            Dim transmitted As Double = source * (1.0 + pixelGain) / (1.0 + pixelGain * source)
                            raw(pixel + channel) = CByte(Math.Max(0, Math.Min(255, CInt(Math.Round(transmitted * 255.0)))))
                        Next
                    Next
                Next
                Marshal.Copy(raw, 0, data.Scan0, raw.Length)
            Finally
                result.UnlockBits(data)
            End Try
            Return result
        End Function

        Public Sub New(ByRef _parent As B2SPictureBox, ByVal _IsDMD As Boolean)
            parent = _parent
            isDMD = _IsDMD
        End Sub

    End Class

End Namespace
