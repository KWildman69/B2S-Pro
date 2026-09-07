Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.Runtime.InteropServices

Namespace ReelAndLED

    ''' <summary>
    ''' B2S Pro's opt-in mechanical reel treatment. This is intentionally
    ''' independent of the legacy reel illumination path.
    ''' </summary>
    Public NotInheritable Class Reel3DEffect

        Private Sub New()
        End Sub

        Public Shared Function RenderReel(ByVal source As Image,
                                          ByVal brightness As Integer,
                                          ByVal temperature As Integer,
                                          ByVal depth As Integer) As Bitmap
            If source Is Nothing Then Return Nothing

            brightness = Math.Max(0, Math.Min(400, brightness))
            temperature = Math.Max(2000, Math.Min(6500, temperature))
            depth = Math.Max(0, Math.Min(200, depth))

            Dim input As New Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb)
            Using graphics As Graphics = Graphics.FromImage(input)
                graphics.Clear(Color.Transparent)
                graphics.CompositingMode = CompositingMode.SourceCopy
                graphics.DrawImage(source, New Rectangle(0, 0, input.Width, input.Height))
            End Using

            Dim result As New Bitmap(input.Width, input.Height, PixelFormat.Format32bppArgb)
            Dim bounds As New Rectangle(0, 0, input.Width, input.Height)
            Dim inputData As BitmapData = input.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb)
            Dim outputData As BitmapData = result.LockBits(bounds, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb)
            Try
                Dim inputStride As Integer = Math.Abs(inputData.Stride)
                Dim outputStride As Integer = Math.Abs(outputData.Stride)
                Dim inputRaw(inputStride * input.Height - 1) As Byte
                Dim outputRaw(outputStride * result.Height - 1) As Byte
                Marshal.Copy(inputData.Scan0, inputRaw, 0, inputRaw.Length)

                Dim depthAmount As Double = depth / 100.0
                Dim lightAmount As Double = brightness / 100.0
                For y As Integer = 0 To input.Height - 1
                    Dim normalY As Double = If(input.Height <= 1, 0.0, 2.0 * y / (input.Height - 1.0) - 1.0)
                    Dim roundness As Double = Math.Sqrt(Math.Max(0.0, 1.0 - normalY * normalY))
                    Dim drumShade As Double = Math.Max(0.18, 1.0 - depthAmount * 0.5 * (1.0 - roundness))
                    Dim inputRow As Integer = If(inputData.Stride >= 0, y, input.Height - 1 - y) * inputStride
                    Dim outputRow As Integer = If(outputData.Stride >= 0, y, result.Height - 1 - y) * outputStride

                    For x As Integer = 0 To input.Width - 1
                        Dim inputPixel As Integer = inputRow + x * 4
                        Dim outputPixel As Integer = outputRow + x * 4
                        Dim blue As Integer = inputRaw(inputPixel)
                        Dim green As Integer = inputRaw(inputPixel + 1)
                        Dim red As Integer = inputRaw(inputPixel + 2)
                        Dim alpha As Integer = inputRaw(inputPixel + 3)
                        Dim luminance As Double = (0.2126 * red + 0.7152 * green + 0.0722 * blue) / 255.0
                        Dim material As Double = Math.Pow(Math.Max(0.0, Math.Min(1.0, luminance)), 1.35)
                        Dim normalX As Double = If(input.Width <= 1, 0.0, 2.0 * x / (input.Width - 1.0) - 1.0)
                        Dim bulbFalloff As Double = 0.68 + 0.32 * Math.Max(0.0, 1.0 - Math.Sqrt(normalX * normalX * 0.38 + normalY * normalY * 0.72))
                        Dim addedLight As Double = material * bulbFalloff * lightAmount * 0.54

                        outputRaw(outputPixel) = ClampByte(blue * drumShade * 0.67 + 255.0 * addedLight)
                        outputRaw(outputPixel + 1) = ClampByte(green * drumShade * 0.67 + 255.0 * addedLight)
                        outputRaw(outputPixel + 2) = ClampByte(red * drumShade * 0.67 + 255.0 * addedLight)
                        outputRaw(outputPixel + 3) = CByte(alpha)
                    Next
                Next

                Marshal.Copy(outputRaw, 0, outputData.Scan0, outputRaw.Length)
            Finally
                input.UnlockBits(inputData)
                result.UnlockBits(outputData)
                input.Dispose()
            End Try

            Using graphics As Graphics = Graphics.FromImage(result)
                Dim depthAmount As Double = depth / 100.0
                Dim sideWidth As Integer = Math.Min(Math.Max(1, result.Width \ 3), 12 + CInt(Math.Round(8.0 * depthAmount)))
                For i As Integer = 0 To sideWidth - 1
                    Dim alpha As Integer = ClampAlpha(105.0 * depthAmount * (1.0 - i / CDbl(sideWidth)))
                    Using pen As New Pen(Color.FromArgb(alpha, 0, 0, 0))
                        graphics.DrawLine(pen, i, 0, i, result.Height - 1)
                        graphics.DrawLine(pen, result.Width - 1 - i, 0, result.Width - 1 - i, result.Height - 1)
                    End Using
                Next
            End Using

            ApplyTemperatureGrade(result, temperature)
            Return result
        End Function

        Public Shared Sub DrawWindowOverlay(ByVal graphics As Graphics,
                                            ByVal window As Rectangle,
                                            ByVal depth As Integer,
                                            ByVal glassReflection As Integer)
            If graphics Is Nothing OrElse window.Width <= 2 OrElse window.Height <= 2 Then Return
            depth = Math.Max(0, Math.Min(200, depth))
            glassReflection = Math.Max(0, Math.Min(200, glassReflection))

            Dim depthAmount As Double = depth / 100.0
            Dim insetWidth As Integer = 18 + CInt(Math.Round(7.0 * depthAmount))
            insetWidth = Math.Min(insetWidth, Math.Max(1, Math.Min(window.Width, window.Height) \ 2 - 1))
            For i As Integer = 0 To insetWidth - 1
                Dim alpha As Integer = ClampAlpha(150.0 * depthAmount * (1.0 - i / CDbl(insetWidth)))
                Using pen As New Pen(Color.FromArgb(alpha, 0, 0, 0))
                    graphics.DrawRectangle(pen,
                                           window.X + i,
                                           window.Y + i,
                                           Math.Max(1, window.Width - 1 - i * 2),
                                           Math.Max(1, window.Height - 1 - i * 2))
                End Using
            Next

            Dim reflectionAlpha As Integer = ClampAlpha(150.0 * glassReflection / 100.0)
            If reflectionAlpha <= 0 Then Return

            Dim shineRect As New Rectangle(window.X + 16,
                                           window.Y + 8,
                                           Math.Max(2, window.Width - 32),
                                           Math.Max(16, window.Height \ 3))
            Using shine As New LinearGradientBrush(shineRect,
                                                   Color.FromArgb(reflectionAlpha, 235, 248, 255),
                                                   Color.FromArgb(0, 235, 248, 255),
                                                   90.0F)
                graphics.FillRectangle(shine, shineRect)
            End Using
            Using line As New Pen(Color.FromArgb(reflectionAlpha, 255, 255, 255), 2.5F)
                graphics.DrawLine(line, window.Left + 35, window.Top + 19, window.Right - 85, window.Top + 7)
            End Using
            Using streak As New Pen(Color.FromArgb(reflectionAlpha \ 2, 210, 238, 255), 5.0F)
                graphics.DrawLine(streak,
                                  window.Left + window.Width \ 3,
                                  window.Top + 10,
                                  window.Left + window.Width \ 2,
                                  window.Top + window.Height \ 3)
            End Using
        End Sub

        Private Shared Sub ApplyTemperatureGrade(ByVal rendered As Bitmap, ByVal kelvin As Integer)
            kelvin = Math.Max(2000, Math.Min(6500, kelvin))
            If kelvin = 4000 Then Return

            Dim redFactor As Double
            Dim greenFactor As Double
            Dim blueFactor As Double
            If kelvin <= 2700 Then
                Dim amount As Double = (kelvin - 2000.0) / 700.0
                redFactor = Lerp(1.22, 1.14, amount)
                greenFactor = Lerp(0.92, 0.96, amount)
                blueFactor = Lerp(0.58, 0.72, amount)
            ElseIf kelvin <= 3000 Then
                Dim amount As Double = (kelvin - 2700.0) / 300.0
                redFactor = Lerp(1.14, 1.1, amount)
                greenFactor = Lerp(0.96, 0.98, amount)
                blueFactor = Lerp(0.72, 0.8, amount)
            ElseIf kelvin <= 4000 Then
                Dim amount As Double = (kelvin - 3000.0) / 1000.0
                redFactor = Lerp(1.1, 1.0, amount)
                greenFactor = Lerp(0.98, 1.0, amount)
                blueFactor = Lerp(0.8, 1.0, amount)
            ElseIf kelvin <= 5000 Then
                Dim amount As Double = (kelvin - 4000.0) / 1000.0
                redFactor = Lerp(1.0, 0.96, amount)
                greenFactor = Lerp(1.0, 1.01, amount)
                blueFactor = Lerp(1.0, 1.1, amount)
            Else
                Dim amount As Double = (kelvin - 5000.0) / 1500.0
                redFactor = Lerp(0.96, 0.9, amount)
                greenFactor = Lerp(1.01, 1.0, amount)
                blueFactor = Lerp(1.1, 1.22, amount)
            End If

            Dim bounds As New Rectangle(0, 0, rendered.Width, rendered.Height)
            Dim data As BitmapData = rendered.LockBits(bounds, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb)
            Try
                Dim stride As Integer = Math.Abs(data.Stride)
                Dim raw(stride * rendered.Height - 1) As Byte
                Marshal.Copy(data.Scan0, raw, 0, raw.Length)
                For y As Integer = 0 To rendered.Height - 1
                    Dim row As Integer = If(data.Stride >= 0, y, rendered.Height - 1 - y) * stride
                    For x As Integer = 0 To rendered.Width - 1
                        Dim pixel As Integer = row + x * 4
                        If raw(pixel + 3) = 0 Then Continue For
                        Dim originalBlue As Double = raw(pixel)
                        Dim originalGreen As Double = raw(pixel + 1)
                        Dim originalRed As Double = raw(pixel + 2)
                        Dim oldLuma As Double = originalRed * 0.2126 + originalGreen * 0.7152 + originalBlue * 0.0722
                        Dim gradedRed As Double = originalRed * redFactor
                        Dim gradedGreen As Double = originalGreen * greenFactor
                        Dim gradedBlue As Double = originalBlue * blueFactor
                        Dim newLuma As Double = gradedRed * 0.2126 + gradedGreen * 0.7152 + gradedBlue * 0.0722
                        If newLuma > 0.001 Then
                            Dim lumaScale As Double = Math.Max(0.82, Math.Min(1.18, oldLuma / newLuma))
                            gradedRed *= lumaScale
                            gradedGreen *= lumaScale
                            gradedBlue *= lumaScale
                        End If
                        Dim strength As Double = 0.55 + 0.3 * (oldLuma / 255.0)
                        raw(pixel) = ClampByte(originalBlue + (gradedBlue - originalBlue) * strength)
                        raw(pixel + 1) = ClampByte(originalGreen + (gradedGreen - originalGreen) * strength)
                        raw(pixel + 2) = ClampByte(originalRed + (gradedRed - originalRed) * strength)
                    Next
                Next
                Marshal.Copy(raw, 0, data.Scan0, raw.Length)
            Finally
                rendered.UnlockBits(data)
            End Try
        End Sub

        Private Shared Function Lerp(ByVal startValue As Double, ByVal endValue As Double, ByVal amount As Double) As Double
            amount = Math.Max(0.0, Math.Min(1.0, amount))
            Return startValue + (endValue - startValue) * amount
        End Function

        Private Shared Function ClampByte(ByVal value As Double) As Byte
            Return CByte(Math.Max(0, Math.Min(255, CInt(Math.Round(value)))))
        End Function

        Private Shared Function ClampAlpha(ByVal value As Double) As Integer
            Return Math.Max(0, Math.Min(255, CInt(Math.Round(value))))
        End Function

    End Class

End Namespace
