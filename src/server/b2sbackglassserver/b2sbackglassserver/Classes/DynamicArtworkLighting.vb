Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.Runtime.InteropServices

' Runtime half of the Designer's established artwork-pixel renderer. The
' exported alpha field already contains the authored glow and mask geometry;
' this class applies that field to the current moving snippet frame.
Friend NotInheritable Class DynamicArtworkLighting
    Friend NotInheritable Class Workspace
        Friend Field() As Byte = Nothing
        Friend FieldPixels() As Byte = Nothing
        Friend Luminance() As Double = Nothing
        Friend BlurHorizontal() As Double = Nothing
        Friend BlurOutput() As Double = Nothing
        Friend Transmission() As Double = Nothing
        Friend SourcePixels() As Byte = Nothing
        Friend OutputPixels() As Byte = Nothing
        Friend TemperaturePixels() As Byte = Nothing

        Friend Sub EnsureCapacity(ByVal pixelCount As Integer, ByVal byteCount As Integer)
            EnsureByteCapacity(Field, pixelCount)
            EnsureByteCapacity(FieldPixels, byteCount)
            EnsureDoubleCapacity(Luminance, pixelCount)
            EnsureDoubleCapacity(BlurHorizontal, pixelCount)
            EnsureDoubleCapacity(BlurOutput, pixelCount)
            EnsureDoubleCapacity(Transmission, pixelCount)
            EnsureByteCapacity(SourcePixels, byteCount)
            EnsureByteCapacity(OutputPixels, byteCount)
            EnsureByteCapacity(TemperaturePixels, byteCount)
        End Sub

        Private Shared Sub EnsureByteCapacity(ByRef buffer() As Byte, ByVal length As Integer)
            If buffer Is Nothing OrElse buffer.Length < length Then ReDim buffer(length - 1)
        End Sub

        Private Shared Sub EnsureDoubleCapacity(ByRef buffer() As Double, ByVal length As Integer)
            If buffer Is Nothing OrElse buffer.Length < length Then ReDim buffer(length - 1)
        End Sub
    End Class

    Private Sub New()
    End Sub

    Public Shared Function Render(ByVal source As Bitmap,
                                  ByVal lightFieldImage As Bitmap,
                                  ByVal intensity As Integer,
                                  ByVal lightColor As Color,
                                  ByVal flasherStyle As Integer,
                                  ByVal saturation As Integer,
                                  ByVal highlightProtection As Integer,
                                  ByVal darkAreaLift As Integer,
                                  ByVal transmissionContrast As Integer,
                                  ByVal lightTemperature As Integer) As Bitmap
        If source Is Nothing OrElse lightFieldImage Is Nothing OrElse source.Size <> lightFieldImage.Size Then Return Nothing
        Dim width As Integer = source.Width
        Dim height As Integer = source.Height
        Dim field(width * height - 1) As Byte
        Dim fieldData As BitmapData = lightFieldImage.LockBits(New Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb)
        Try
            Dim stride As Integer = Math.Abs(fieldData.Stride)
            Dim raw(stride * height - 1) As Byte
            Marshal.Copy(fieldData.Scan0, raw, 0, raw.Length)
            For y As Integer = 0 To height - 1
                For x As Integer = 0 To width - 1
                    field(y * width + x) = raw(y * stride + x * 4 + 3)
                Next
            Next
        Finally
            lightFieldImage.UnlockBits(fieldData)
        End Try

        Dim rendered As Bitmap = RenderPixels(source, field, BuildTransmissionMap(source, transmissionContrast),
                                               intensity, lightColor, flasherStyle, saturation,
                                               highlightProtection, darkAreaLift)
        ApplyTemperature(rendered, lightTemperature)
        Return rendered
    End Function

    ' Native one-image rotation can repaint many dynamic lights every display
    ' refresh. Reuse its large pixel and blur buffers so those repaints do not
    ' force full garbage collections. The established Render entry point above
    ' remains unchanged for every non-native moving and static snippet.
    Friend Shared Function RenderWithWorkspace(ByVal source As Bitmap,
                                               ByVal lightFieldImage As Bitmap,
                                               ByVal intensity As Integer,
                                               ByVal lightColor As Color,
                                               ByVal flasherStyle As Integer,
                                               ByVal saturation As Integer,
                                               ByVal highlightProtection As Integer,
                                               ByVal darkAreaLift As Integer,
                                               ByVal transmissionContrast As Integer,
                                               ByVal lightTemperature As Integer,
                                               ByVal workspace As Workspace) As Bitmap
        If source Is Nothing OrElse lightFieldImage Is Nothing OrElse workspace Is Nothing OrElse
           source.Size <> lightFieldImage.Size Then Return Nothing

        Dim width As Integer = source.Width
        Dim height As Integer = source.Height
        Dim pixelCount As Integer = width * height
        Dim fieldData As BitmapData = lightFieldImage.LockBits(New Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb)
        Try
            Dim stride As Integer = Math.Abs(fieldData.Stride)
            Dim byteCount As Integer = stride * height
            workspace.EnsureCapacity(pixelCount, byteCount)
            Marshal.Copy(fieldData.Scan0, workspace.FieldPixels, 0, byteCount)
            For y As Integer = 0 To height - 1
                For x As Integer = 0 To width - 1
                    workspace.Field(y * width + x) = workspace.FieldPixels(y * stride + x * 4 + 3)
                Next
            Next
        Finally
            lightFieldImage.UnlockBits(fieldData)
        End Try

        Dim rendered As Bitmap = RenderPixelsWithWorkspace(
            source, workspace.Field, BuildTransmissionMapWithWorkspace(source, transmissionContrast, workspace),
            intensity, lightColor, flasherStyle, saturation, highlightProtection, darkAreaLift, workspace)
        ApplyTemperatureWithWorkspace(rendered, lightTemperature, workspace)
        Return rendered
    End Function

    Private Shared Function BuildTransmissionMapWithWorkspace(ByVal source As Bitmap,
                                                              ByVal contrastPercent As Integer,
                                                              ByVal workspace As Workspace) As Double()
        Dim width As Integer = source.Width
        Dim height As Integer = source.Height
        Dim pixelCount As Integer = width * height
        Dim data As BitmapData = source.LockBits(New Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb)
        Try
            Dim stride As Integer = Math.Abs(data.Stride)
            Dim byteCount As Integer = stride * height
            workspace.EnsureCapacity(pixelCount, byteCount)
            Marshal.Copy(data.Scan0, workspace.SourcePixels, 0, byteCount)
            For y As Integer = 0 To height - 1
                For x As Integer = 0 To width - 1
                    Dim p As Integer = y * stride + x * 4
                    workspace.Luminance(y * width + x) = Math.Max(0.0, Math.Min(1.0,
                        (workspace.SourcePixels(p + 2) * 0.299 + workspace.SourcePixels(p + 1) * 0.587 + workspace.SourcePixels(p) * 0.114) / 255.0))
                Next
            Next
        Finally
            source.UnlockBits(data)
        End Try

        BlurDoubleMapWithWorkspace(workspace.Luminance, width, height, 4, workspace)
        Dim contrast As Double = Math.Max(0.0, Math.Min(3.0, contrastPercent / 100.0))
        Dim detailStrength As Double = 0.45 + contrast * 1.15
        Dim midpointStrength As Double = 0.72 + contrast * 0.28
        For i As Integer = 0 To pixelCount - 1
            Dim transmitted As Double = Math.Pow(Math.Max(0.0, workspace.Luminance(i)), 0.82) * midpointStrength +
                                        (workspace.Luminance(i) - workspace.BlurOutput(i)) * detailStrength
            workspace.Transmission(i) = Math.Max(0.025, Math.Min(1.0, transmitted))
        Next
        Return workspace.Transmission
    End Function

    Private Shared Sub BlurDoubleMapWithWorkspace(ByVal values As Double(), ByVal width As Integer,
                                                  ByVal height As Integer, ByVal radius As Integer,
                                                  ByVal workspace As Workspace)
        For y As Integer = 0 To height - 1
            Dim row As Integer = y * width
            Dim sum As Double = 0.0
            Dim left As Integer = 0
            Dim right As Integer = Math.Min(width - 1, radius)
            For x As Integer = left To right
                sum += values(row + x)
            Next
            For x As Integer = 0 To width - 1
                workspace.BlurHorizontal(row + x) = sum / (right - left + 1)
                Dim removeX As Integer = x - radius
                Dim addX As Integer = x + radius + 1
                If removeX >= 0 Then sum -= values(row + removeX) : left += 1
                If addX < width Then sum += values(row + addX) : right += 1
            Next
        Next
        For x As Integer = 0 To width - 1
            Dim sum As Double = 0.0
            Dim top As Integer = 0
            Dim bottom As Integer = Math.Min(height - 1, radius)
            For y As Integer = top To bottom
                sum += workspace.BlurHorizontal(y * width + x)
            Next
            For y As Integer = 0 To height - 1
                workspace.BlurOutput(y * width + x) = sum / (bottom - top + 1)
                Dim removeY As Integer = y - radius
                Dim addY As Integer = y + radius + 1
                If removeY >= 0 Then sum -= workspace.BlurHorizontal(removeY * width + x) : top += 1
                If addY < height Then sum += workspace.BlurHorizontal(addY * width + x) : bottom += 1
            Next
        Next
    End Sub

    Private Shared Function RenderPixelsWithWorkspace(ByVal source As Bitmap, ByVal lightField As Byte(),
                                                      ByVal transmissionMap As Double(), ByVal intensity As Integer,
                                                      ByVal lightColor As Color, ByVal flasherStyle As Integer,
                                                      ByVal saturation As Integer, ByVal highlightProtection As Integer,
                                                      ByVal darkAreaLift As Integer, ByVal workspace As Workspace) As Bitmap
        Dim width As Integer = source.Width
        Dim height As Integer = source.Height
        Dim result As New Bitmap(width, height, PixelFormat.Format32bppArgb)
        Dim sourceData As BitmapData = source.LockBits(New Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb)
        Dim outputData As BitmapData = result.LockBits(New Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb)
        Try
            Dim sourceStride As Integer = Math.Abs(sourceData.Stride)
            Dim outputStride As Integer = Math.Abs(outputData.Stride)
            Dim sourceByteCount As Integer = sourceStride * height
            Dim outputByteCount As Integer = outputStride * height
            workspace.EnsureCapacity(width * height, Math.Max(sourceByteCount, outputByteCount))
            Marshal.Copy(sourceData.Scan0, workspace.SourcePixels, 0, sourceByteCount)
            Array.Clear(workspace.OutputPixels, 0, outputByteCount)
            Dim normalizedIntensity As Double = Math.Max(0.0, Math.Min(16.0, intensity / 100.0))
            Dim exposureStrength As Double = normalizedIntensity * 1.18 + Math.Pow(Math.Max(0.0, normalizedIntensity - 3.0), 1.28) * 0.58
            Dim whitePush As Double = Math.Max(0.0, Math.Min(0.82, (normalizedIntensity - 3.75) / 5.0))
            Dim protect As Double = Math.Max(0.0, Math.Min(1.0, highlightProtection / 100.0))
            Dim shadowLift As Double = Math.Max(0.0, Math.Min(1.0, darkAreaLift / 100.0))
            Dim sat As Double = Math.Max(0.0, Math.Min(2.0, saturation / 100.0))
            For y As Integer = 0 To height - 1
                For x As Integer = 0 To width - 1
                    Dim index As Integer = y * width + x
                    Dim field As Double = lightField(index) / 255.0
                    If field <= 0.0 Then Continue For
                    Dim sp As Integer = y * sourceStride + x * 4
                    Dim dp As Integer = y * outputStride + x * 4
                    Dim b As Double = workspace.SourcePixels(sp)
                    Dim g As Double = workspace.SourcePixels(sp + 1)
                    Dim r As Double = workspace.SourcePixels(sp + 2)
                    Dim sourceAlpha As Double = workspace.SourcePixels(sp + 3) / 255.0
                    Dim surfaceDetail As Double = transmissionMap(index)
                    Dim luminance As Double = (r * 0.299 + g * 0.587 + b * 0.114) / 255.0
                    Dim blackProtection As Double = SmoothStep((Math.Max(r, Math.Max(g, b)) - 18.0) / 30.0)
                    If blackProtection <= 0.0 Then Continue For
                    Dim response As Double = 0.72 + shadowLift * (1.0 - luminance) * 0.28
                    Dim detailResponse As Double = 0.8 + surfaceDetail * 0.4
                    Dim localEnergy As Double = exposureStrength * field * response * detailResponse
                    localEnergy *= 1.0 - protect * luminance * 0.8
                    Dim gain As Double = 1.0 + localEnergy * 0.38
                    Dim rr As Double = r * (1.0 + (gain - 1.0) * lightColor.R / 255.0)
                    Dim gg As Double = g * (1.0 + (gain - 1.0) * lightColor.G / 255.0)
                    Dim bb As Double = b * (1.0 + (gain - 1.0) * lightColor.B / 255.0)
                    Dim burstMix As Double = SmoothStep((normalizedIntensity - 8.0) / 8.0) * field *
                                             SmoothStep((luminance - 0.55) / 0.45)
                    rr += (255.0 - rr) * burstMix
                    gg += (255.0 - gg) * burstMix
                    bb += (255.0 - bb) * burstMix
                    Dim litLum As Double = rr * 0.299 + gg * 0.587 + bb * 0.114
                    rr = litLum + (rr - litLum) * sat
                    gg = litLum + (gg - litLum) * sat
                    bb = litLum + (bb - litLum) * sat
                    If flasherStyle = 2 Then rr = lightColor.R : gg = lightColor.G : bb = lightColor.B
                    workspace.OutputPixels(dp) = CByte(ClampByte(CInt(bb)))
                    workspace.OutputPixels(dp + 1) = CByte(ClampByte(CInt(gg)))
                    workspace.OutputPixels(dp + 2) = CByte(ClampByte(CInt(rr)))
                    workspace.OutputPixels(dp + 3) = CByte(ClampByte(CInt(field * sourceAlpha * blackProtection * 255.0)))
                Next
            Next
            Marshal.Copy(workspace.OutputPixels, 0, outputData.Scan0, outputByteCount)
        Finally
            source.UnlockBits(sourceData)
            result.UnlockBits(outputData)
        End Try
        Return result
    End Function

    Private Shared Sub ApplyTemperatureWithWorkspace(ByVal rendered As Bitmap, ByVal kelvin As Integer,
                                                     ByVal workspace As Workspace)
        If rendered Is Nothing Then Return
        kelvin = Math.Max(2000, Math.Min(6500, kelvin))
        If kelvin = 4000 Then Return
        Dim rf, gf, bf As Double
        If kelvin <= 2700 Then
            Dim t As Double = (kelvin - 2000.0) / 700.0 : rf = Lerp(1.22, 1.14, t) : gf = Lerp(0.92, 0.96, t) : bf = Lerp(0.58, 0.72, t)
        ElseIf kelvin <= 3000 Then
            Dim t As Double = (kelvin - 2700.0) / 300.0 : rf = Lerp(1.14, 1.1, t) : gf = Lerp(0.96, 0.98, t) : bf = Lerp(0.72, 0.8, t)
        ElseIf kelvin <= 4000 Then
            Dim t As Double = (kelvin - 3000.0) / 1000.0 : rf = Lerp(1.1, 1.0, t) : gf = Lerp(0.98, 1.0, t) : bf = Lerp(0.8, 1.0, t)
        ElseIf kelvin <= 5000 Then
            Dim t As Double = (kelvin - 4000.0) / 1000.0 : rf = Lerp(1.0, 0.96, t) : gf = Lerp(1.0, 1.01, t) : bf = Lerp(1.0, 1.1, t)
        Else
            Dim t As Double = (kelvin - 5000.0) / 1500.0 : rf = Lerp(0.96, 0.9, t) : gf = Lerp(1.01, 1.0, t) : bf = Lerp(1.1, 1.22, t)
        End If
        Dim data As BitmapData = rendered.LockBits(New Rectangle(0, 0, rendered.Width, rendered.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb)
        Try
            Dim stride As Integer = Math.Abs(data.Stride)
            Dim byteCount As Integer = stride * rendered.Height
            workspace.EnsureCapacity(rendered.Width * rendered.Height, byteCount)
            Marshal.Copy(data.Scan0, workspace.TemperaturePixels, 0, byteCount)
            For y As Integer = 0 To rendered.Height - 1
                For x As Integer = 0 To rendered.Width - 1
                    Dim p As Integer = y * stride + x * 4
                    If workspace.TemperaturePixels(p + 3) = 0 Then Continue For
                    Dim ob As Double = workspace.TemperaturePixels(p)
                    Dim og As Double = workspace.TemperaturePixels(p + 1)
                    Dim orr As Double = workspace.TemperaturePixels(p + 2)
                    Dim oldLuma As Double = orr * 0.2126 + og * 0.7152 + ob * 0.0722
                    Dim nr As Double = orr * rf, ng As Double = og * gf, nb As Double = ob * bf
                    Dim newLuma As Double = nr * 0.2126 + ng * 0.7152 + nb * 0.0722
                    If newLuma > 0.001 Then
                        Dim scale As Double = Math.Max(0.82, Math.Min(1.18, oldLuma / newLuma))
                        nr *= scale : ng *= scale : nb *= scale
                    End If
                    Dim strength As Double = 0.55 + 0.3 * (oldLuma / 255.0)
                    workspace.TemperaturePixels(p) = CByte(ClampByte(CInt(Math.Round(ob + (nb - ob) * strength))))
                    workspace.TemperaturePixels(p + 1) = CByte(ClampByte(CInt(Math.Round(og + (ng - og) * strength))))
                    workspace.TemperaturePixels(p + 2) = CByte(ClampByte(CInt(Math.Round(orr + (nr - orr) * strength))))
                Next
            Next
            Marshal.Copy(workspace.TemperaturePixels, 0, data.Scan0, byteCount)
        Finally
            rendered.UnlockBits(data)
        End Try
    End Sub

    Private Shared Function BuildTransmissionMap(ByVal source As Bitmap, ByVal contrastPercent As Integer) As Double()
        Dim width As Integer = source.Width
        Dim height As Integer = source.Height
        Dim luminance(width * height - 1) As Double
        Dim data As BitmapData = source.LockBits(New Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb)
        Try
            Dim stride As Integer = Math.Abs(data.Stride)
            Dim raw(stride * height - 1) As Byte
            Marshal.Copy(data.Scan0, raw, 0, raw.Length)
            For y As Integer = 0 To height - 1
                For x As Integer = 0 To width - 1
                    Dim p As Integer = y * stride + x * 4
                    luminance(y * width + x) = Math.Max(0.0, Math.Min(1.0,
                        (raw(p + 2) * 0.299 + raw(p + 1) * 0.587 + raw(p) * 0.114) / 255.0))
                Next
            Next
        Finally
            source.UnlockBits(data)
        End Try

        Dim localAverage() As Double = BlurDoubleMap(luminance, width, height, 4)
        Dim contrast As Double = Math.Max(0.0, Math.Min(3.0, contrastPercent / 100.0))
        Dim detailStrength As Double = 0.45 + contrast * 1.15
        Dim midpointStrength As Double = 0.72 + contrast * 0.28
        Dim result(luminance.Length - 1) As Double
        For i As Integer = 0 To luminance.Length - 1
            Dim transmitted As Double = Math.Pow(Math.Max(0.0, luminance(i)), 0.82) * midpointStrength +
                                        (luminance(i) - localAverage(i)) * detailStrength
            result(i) = Math.Max(0.025, Math.Min(1.0, transmitted))
        Next
        Return result
    End Function

    Private Shared Function BlurDoubleMap(ByVal values As Double(), ByVal width As Integer, ByVal height As Integer, ByVal radius As Integer) As Double()
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
                If removeX >= 0 Then sum -= values(row + removeX) : left += 1
                If addX < width Then sum += values(row + addX) : right += 1
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
                If removeY >= 0 Then sum -= horizontal(removeY * width + x) : top += 1
                If addY < height Then sum += horizontal(addY * width + x) : bottom += 1
            Next
        Next
        Return output
    End Function

    Private Shared Function RenderPixels(ByVal source As Bitmap, ByVal lightField As Byte(), ByVal transmissionMap As Double(),
                                         ByVal intensity As Integer, ByVal lightColor As Color, ByVal flasherStyle As Integer,
                                         ByVal saturation As Integer, ByVal highlightProtection As Integer,
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
            Dim normalizedIntensity As Double = Math.Max(0.0, Math.Min(16.0, intensity / 100.0))
            Dim exposureStrength As Double = normalizedIntensity * 1.18 + Math.Pow(Math.Max(0.0, normalizedIntensity - 3.0), 1.28) * 0.58
            Dim whitePush As Double = Math.Max(0.0, Math.Min(0.82, (normalizedIntensity - 3.75) / 5.0))
            Dim protect As Double = Math.Max(0.0, Math.Min(1.0, highlightProtection / 100.0))
            Dim shadowLift As Double = Math.Max(0.0, Math.Min(1.0, darkAreaLift / 100.0))
            Dim sat As Double = Math.Max(0.0, Math.Min(2.0, saturation / 100.0))
            For y As Integer = 0 To height - 1
                For x As Integer = 0 To width - 1
                    Dim index As Integer = y * width + x
                    Dim field As Double = lightField(index) / 255.0
                    If field <= 0.0 Then Continue For
                    Dim sp As Integer = y * sourceStride + x * 4
                    Dim dp As Integer = y * outputStride + x * 4
                    Dim b As Double = sourcePixels(sp)
                    Dim g As Double = sourcePixels(sp + 1)
                    Dim r As Double = sourcePixels(sp + 2)
                    Dim sourceAlpha As Double = sourcePixels(sp + 3) / 255.0
                    Dim surfaceDetail As Double = transmissionMap(index)
                    Dim luminance As Double = (r * 0.299 + g * 0.587 + b * 0.114) / 255.0
                    Dim blackProtection As Double = SmoothStep((Math.Max(r, Math.Max(g, b)) - 18.0) / 30.0)
                    If blackProtection <= 0.0 Then Continue For
                    ' Dynamic front lighting is reflected by the moving artwork's
                    ' surface. Scale the existing pigment by the incident light;
                    ' do not drive every nonzero channel toward white as a
                    ' transmitted/backlight calculation does.
                    Dim response As Double = 0.72 + shadowLift * (1.0 - luminance) * 0.28
                    Dim detailResponse As Double = 0.8 + surfaceDetail * 0.4
                    Dim localEnergy As Double = exposureStrength * field * response * detailResponse
                    localEnergy *= 1.0 - protect * luminance * 0.8
                    Dim gain As Double = 1.0 + localEnergy * 0.38
                    Dim rr As Double = r * (1.0 + (gain - 1.0) * lightColor.R / 255.0)
                    Dim gg As Double = g * (1.0 + (gain - 1.0) * lightColor.G / 255.0)
                    Dim bb As Double = b * (1.0 + (gain - 1.0) * lightColor.B / 255.0)

                    ' Above 800%, add a highlight-only white burst. Restrict it
                    ' to already bright painted detail so dark pigment never turns
                    ' into the translucent white wash produced by the old model.
                    Dim burstMix As Double = SmoothStep((normalizedIntensity - 8.0) / 8.0) * field *
                                             SmoothStep((luminance - 0.55) / 0.45)
                    rr += (255.0 - rr) * burstMix
                    gg += (255.0 - gg) * burstMix
                    bb += (255.0 - bb) * burstMix
                    Dim litLum As Double = rr * 0.299 + gg * 0.587 + bb * 0.114
                    rr = litLum + (rr - litLum) * sat
                    gg = litLum + (gg - litLum) * sat
                    bb = litLum + (bb - litLum) * sat
                    If flasherStyle = 2 Then rr = lightColor.R : gg = lightColor.G : bb = lightColor.B
                    outputPixels(dp) = CByte(ClampByte(CInt(bb)))
                    outputPixels(dp + 1) = CByte(ClampByte(CInt(gg)))
                    outputPixels(dp + 2) = CByte(ClampByte(CInt(rr)))
                    outputPixels(dp + 3) = CByte(ClampByte(CInt(field * sourceAlpha * blackProtection * 255.0)))
                Next
            Next
            Marshal.Copy(outputPixels, 0, outputData.Scan0, outputPixels.Length)
        Finally
            source.UnlockBits(sourceData)
            result.UnlockBits(outputData)
        End Try
        Return result
    End Function

    Private Shared Sub ApplyTemperature(ByVal rendered As Bitmap, ByVal kelvin As Integer)
        If rendered Is Nothing Then Return
        kelvin = Math.Max(2000, Math.Min(6500, kelvin))
        If kelvin = 4000 Then Return
        Dim rf, gf, bf As Double
        If kelvin <= 2700 Then
            Dim t As Double = (kelvin - 2000.0) / 700.0 : rf = Lerp(1.22, 1.14, t) : gf = Lerp(0.92, 0.96, t) : bf = Lerp(0.58, 0.72, t)
        ElseIf kelvin <= 3000 Then
            Dim t As Double = (kelvin - 2700.0) / 300.0 : rf = Lerp(1.14, 1.1, t) : gf = Lerp(0.96, 0.98, t) : bf = Lerp(0.72, 0.8, t)
        ElseIf kelvin <= 4000 Then
            Dim t As Double = (kelvin - 3000.0) / 1000.0 : rf = Lerp(1.1, 1.0, t) : gf = Lerp(0.98, 1.0, t) : bf = Lerp(0.8, 1.0, t)
        ElseIf kelvin <= 5000 Then
            Dim t As Double = (kelvin - 4000.0) / 1000.0 : rf = Lerp(1.0, 0.96, t) : gf = Lerp(1.0, 1.01, t) : bf = Lerp(1.0, 1.1, t)
        Else
            Dim t As Double = (kelvin - 5000.0) / 1500.0 : rf = Lerp(0.96, 0.9, t) : gf = Lerp(1.01, 1.0, t) : bf = Lerp(1.1, 1.22, t)
        End If
        Dim data As BitmapData = rendered.LockBits(New Rectangle(0, 0, rendered.Width, rendered.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb)
        Try
            Dim stride As Integer = Math.Abs(data.Stride)
            Dim raw(stride * rendered.Height - 1) As Byte
            Marshal.Copy(data.Scan0, raw, 0, raw.Length)
            For y As Integer = 0 To rendered.Height - 1
                For x As Integer = 0 To rendered.Width - 1
                    Dim p As Integer = y * stride + x * 4
                    If raw(p + 3) = 0 Then Continue For
                    Dim ob As Double = raw(p), og As Double = raw(p + 1), orr As Double = raw(p + 2)
                    Dim oldLuma As Double = orr * 0.2126 + og * 0.7152 + ob * 0.0722
                    Dim nr As Double = orr * rf, ng As Double = og * gf, nb As Double = ob * bf
                    Dim newLuma As Double = nr * 0.2126 + ng * 0.7152 + nb * 0.0722
                    If newLuma > 0.001 Then
                        Dim scale As Double = Math.Max(0.82, Math.Min(1.18, oldLuma / newLuma))
                        nr *= scale : ng *= scale : nb *= scale
                    End If
                    Dim strength As Double = 0.55 + 0.3 * (oldLuma / 255.0)
                    raw(p) = CByte(ClampByte(CInt(Math.Round(ob + (nb - ob) * strength))))
                    raw(p + 1) = CByte(ClampByte(CInt(Math.Round(og + (ng - og) * strength))))
                    raw(p + 2) = CByte(ClampByte(CInt(Math.Round(orr + (nr - orr) * strength))))
                Next
            Next
            Marshal.Copy(raw, 0, data.Scan0, raw.Length)
        Finally
            rendered.UnlockBits(data)
        End Try
    End Sub

    Private Shared Function SmoothStep(ByVal value As Double) As Double
        Dim t As Double = Math.Max(0.0, Math.Min(1.0, value))
        Return t * t * (3.0 - 2.0 * t)
    End Function

    Private Shared Function ScreenExposure(ByVal channel As Double, ByVal exposure As Double) As Double
        Dim normalized As Double = Math.Max(0.0, Math.Min(1.0, channel / 255.0))
        Return (1.0 - Math.Pow(1.0 - normalized, Math.Max(1.0, exposure))) * 255.0
    End Function

    Private Shared Function Lerp(ByVal a As Double, ByVal b As Double, ByVal amount As Double) As Double
        amount = Math.Max(0.0, Math.Min(1.0, amount))
        Return a + (b - a) * amount
    End Function

    Private Shared Function ClampByte(ByVal value As Integer) As Integer
        Return Math.Max(0, Math.Min(255, value))
    End Function
End Class
