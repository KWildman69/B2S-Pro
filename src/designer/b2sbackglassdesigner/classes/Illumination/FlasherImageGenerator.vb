Imports System
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.IO

Namespace Illumination

    ''' <summary>
    ''' Creates the same kind of asset produced by Photoshop's
    ''' Select and Mask -> Layer via Copy -> Brightness/Contrast workflow.
    ''' The returned bitmap is a transparent copy of the real backglass pixels.
    ''' </summary>
    Public NotInheritable Class FlasherImageGenerator

        Private Sub New()
        End Sub

        Public Shared Function Generate(ByVal fullArtwork As Image,
                                        ByVal bulb As BulbInfo) As Bitmap
            If fullArtwork Is Nothing OrElse bulb Is Nothing Then Return Nothing
            If String.IsNullOrEmpty(bulb.SelectionMaskData) Then Return Nothing

            If fullArtwork.Width <= 0 OrElse fullArtwork.Height <= 0 Then Return Nothing
            If bulb.SizeX.Width <= 0 OrElse bulb.SizeX.Height <= 0 Then Return Nothing

            Dim sourceRect As New Rectangle(bulb.LocationX, bulb.SizeX)
            sourceRect.Intersect(New Rectangle(0, 0, fullArtwork.Width, fullArtwork.Height))
            If sourceRect.Width <= 0 OrElse sourceRect.Height <= 0 Then Return Nothing

            Using artwork As New Bitmap(fullArtwork)
                Using fullMask As Bitmap = DecodeMask(bulb.SelectionMaskData)
                    If fullMask Is Nothing Then Return Nothing

                    Dim width As Integer = sourceRect.Width
                    Dim height As Integer = sourceRect.Height
                    Dim alpha(width * height - 1) As Byte

                    For y As Integer = 0 To height - 1
                        For x As Integer = 0 To width - 1
                            Dim mx As Integer = sourceRect.X + x
                            Dim my As Integer = sourceRect.Y + y
                            If mx >= 0 AndAlso my >= 0 AndAlso mx < fullMask.Width AndAlso my < fullMask.Height Then
                                Dim c As Color = fullMask.GetPixel(mx, my)
                                Dim gray As Integer = CInt((CLng(c.R) + CLng(c.G) + CLng(c.B)) / 3L)
                                Dim maskedAlpha As Integer = CInt((CLng(gray) * CLng(c.A)) / 255L)
                                alpha(y * width + x) = CByte(Math.Max(0, Math.Min(255, maskedAlpha)))
                            End If
                        Next
                    Next

                    RefineMask(alpha, width, height, bulb)

                    Dim result As New Bitmap(width, height, PixelFormat.Format32bppArgb)
                    Dim passes As Integer = Math.Max(1, Math.Min(12, bulb.ArtworkAdjustmentPasses))

                    For y As Integer = 0 To height - 1
                        For x As Integer = 0 To width - 1
                            Dim a As Integer = alpha(y * width + x)
                            If a <= 0 Then
                                result.SetPixel(x, y, Color.Transparent)
                            Else
                                Dim src As Color = artwork.GetPixel(sourceRect.X + x, sourceRect.Y + y)
                                Dim adjusted As Color = src
                                For pass As Integer = 1 To passes
                                    adjusted = ApplyBrightnessContrast(adjusted, bulb.ArtworkBrightness, bulb.ArtworkContrast)
                                Next
                                result.SetPixel(x, y, Color.FromArgb(a, adjusted.R, adjusted.G, adjusted.B))
                            End If
                        Next
                    Next

                    Return result
                End Using
            End Using
        End Function

        Private Shared Sub RefineMask(ByVal alpha() As Byte,
                                      ByVal width As Integer,
                                      ByVal height As Integer,
                                      ByVal bulb As BulbInfo)
            ' Radius/Smooth soften small jagged selection defects before feathering.
            Dim radius As Integer = Math.Max(0, Math.Min(100, bulb.MaskRadius))
            If bulb.MaskSmartRadius Then radius = Math.Max(1, radius \ 2)
            If radius > 0 Then BoxBlurAlpha(alpha, width, height, radius)

            Dim smooth As Integer = Math.Max(0, Math.Min(100, bulb.MaskSmooth))
            If smooth > 0 Then
                Dim smoothRadius As Integer = Math.Max(1, CInt(Math.Round(smooth / 20.0)))
                BoxBlurAlpha(alpha, width, height, smoothRadius)
                BoxBlurAlpha(alpha, width, height, smoothRadius)
            End If

            ' Shift Edge expands or contracts the alpha silhouette before feather.
            Dim shift As Integer = Math.Max(-100, Math.Min(100, bulb.MaskShiftEdge))
            If shift <> 0 Then ShiftMaskEdge(alpha, width, height, shift)

            Dim feather As Integer = Math.Max(0, Math.Min(250, bulb.MaskFeather))
            If feather > 0 Then BoxBlurAlpha(alpha, width, height, feather)

            ' Contrast tightens a feathered mask around the 50% point.
            Dim contrast As Integer = Math.Max(0, Math.Min(100, bulb.MaskContrast))
            If contrast > 0 Then
                Dim factor As Double = 1.0 + contrast / 25.0
                For i As Integer = 0 To alpha.Length - 1
                    alpha(i) = ClampByte(((alpha(i) / 255.0 - 0.5) * factor + 0.5) * 255.0)
                Next
            End If
        End Sub

        Private Shared Sub ShiftMaskEdge(ByVal alpha() As Byte,
                                         ByVal width As Integer,
                                         ByVal height As Integer,
                                         ByVal shiftPercent As Integer)
            Dim pixels As Integer = Math.Max(1, CInt(Math.Round(Math.Min(width, height) * Math.Abs(shiftPercent) / 500.0)))
            Dim source(alpha.Length - 1) As Byte
            Array.Copy(alpha, source, alpha.Length)

            For pass As Integer = 1 To pixels
                Dim output(alpha.Length - 1) As Byte
                For y As Integer = 0 To height - 1
                    For x As Integer = 0 To width - 1
                        Dim value As Integer = If(shiftPercent > 0, 0, 255)
                        For oy As Integer = -1 To 1
                            For ox As Integer = -1 To 1
                                Dim sx As Integer = Math.Max(0, Math.Min(width - 1, x + ox))
                                Dim sy As Integer = Math.Max(0, Math.Min(height - 1, y + oy))
                                Dim sample As Integer = source(sy * width + sx)
                                If shiftPercent > 0 Then
                                    value = Math.Max(value, sample)
                                Else
                                    value = Math.Min(value, sample)
                                End If
                            Next
                        Next
                        output(y * width + x) = CByte(value)
                    Next
                Next
                source = output
            Next
            Array.Copy(source, alpha, alpha.Length)
        End Sub

        Private Shared Function ApplyBrightnessContrast(ByVal source As Color,
                                                        ByVal brightness As Integer,
                                                        ByVal contrast As Integer) As Color
            brightness = Math.Max(-150, Math.Min(150, brightness))
            contrast = Math.Max(-100, Math.Min(100, contrast))

            Dim brightnessOffset As Double = brightness * 255.0 / 150.0
            Dim contrastFactor As Double
            If contrast >= 0 Then
                contrastFactor = 1.0 + contrast / 50.0
            Else
                contrastFactor = 1.0 + contrast / 100.0
            End If

            Dim r As Double = ((source.R - 127.5) * contrastFactor + 127.5) + brightnessOffset
            Dim g As Double = ((source.G - 127.5) * contrastFactor + 127.5) + brightnessOffset
            Dim b As Double = ((source.B - 127.5) * contrastFactor + 127.5) + brightnessOffset
            Return Color.FromArgb(source.A, ClampByte(r), ClampByte(g), ClampByte(b))
        End Function

        Private Shared Function DecodeMask(ByVal data As String) As Bitmap
            Try
                Dim bytes() As Byte = Convert.FromBase64String(data)
                Using ms As New MemoryStream(bytes)
                    Using tmp As New Bitmap(ms)
                        Return New Bitmap(tmp)
                    End Using
                End Using
            Catch
                Return Nothing
            End Try
        End Function

        Private Shared Sub BoxBlurAlpha(ByVal alpha() As Byte,
                                        ByVal width As Integer,
                                        ByVal height As Integer,
                                        ByVal radius As Integer)
            If radius <= 0 OrElse width <= 0 OrElse height <= 0 Then Return
            radius = Math.Min(radius, Math.Max(width, height))
            Dim horizontal(alpha.Length - 1) As Integer
            Dim output(alpha.Length - 1) As Byte
            Dim diameter As Integer = Math.Max(1, radius * 2 + 1)

            For y As Integer = 0 To height - 1
                Dim sum As Integer = 0
                For ox As Integer = -radius To radius
                    Dim sx As Integer = Math.Max(0, Math.Min(width - 1, ox))
                    sum += alpha(y * width + sx)
                Next
                For x As Integer = 0 To width - 1
                    horizontal(y * width + x) = sum \ diameter
                    Dim removeX As Integer = Math.Max(0, x - radius)
                    Dim addX As Integer = Math.Min(width - 1, x + radius + 1)
                    sum += alpha(y * width + addX) - alpha(y * width + removeX)
                Next
            Next

            For x As Integer = 0 To width - 1
                Dim sum As Integer = 0
                For oy As Integer = -radius To radius
                    Dim sy As Integer = Math.Max(0, Math.Min(height - 1, oy))
                    sum += horizontal(sy * width + x)
                Next
                For y As Integer = 0 To height - 1
                    output(y * width + x) = ClampByte(sum \ diameter)
                    Dim removeY As Integer = Math.Max(0, y - radius)
                    Dim addY As Integer = Math.Min(height - 1, y + radius + 1)
                    sum += horizontal(addY * width + x) - horizontal(removeY * width + x)
                Next
            Next
            Array.Copy(output, alpha, alpha.Length)
        End Sub

        Private Shared Function ClampByte(ByVal value As Double) As Byte
            Return CByte(Math.Max(0, Math.Min(255, CInt(Math.Round(value)))))
        End Function

    End Class
End Namespace
