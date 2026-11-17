Imports System.Collections.Generic
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.Runtime.CompilerServices
Imports System.Runtime.InteropServices

' B2S Pro "Realistic" 3D reel style, shared by the Designer and the Server so both
' draw exactly the same pixels (the plugin has a line-for-line C++ port).
' The number is drawn on a turning drum: it curves away at the top and bottom,
' slices of the next and previous numbers show above and below, the window's
' top lip casts a shadow and a lamp lights the paper from behind. Every size is
' a share of the reel box, so the look is the same at any size (WYSIWYG).
' Colour temperature and the glass are applied by the caller (Reel3DEffect).
Public NotInheritable Class ReelRealistic

    Private Sub New()
    End Sub

    Public Const StyleClassic As Integer = 0
    Public Const StyleRealistic As Integer = 1
    Public Const MaximumCurve As Integer = 300

    ' Paper between two numbers on the strip, as a share of a number's height.
    Private Const NumberGap As Double = 0.3
    ' One number per 36 degrees of drum.
    Private Const Pitch As Double = 2.0 * Math.PI / 10.0

    ' Time one reel step takes, the same as one B2SReelBox / plugin step:
    ' (intermediates + 3) ticks of RollingInterval / (intermediates + 2).
    Public Shared Function StepMilliseconds(ByVal rollingInterval As Integer, ByVal intermediates As Integer) As Double
        If rollingInterval < 10 Then rollingInterval = 101
        Dim k As Integer = Math.Max(0, intermediates)
        Return rollingInterval * (k + 3.0) / (k + 2.0)
    End Function

    Private NotInheritable Class Pixels
        Public Width As Integer
        Public Height As Integer
        Public Data As Byte() ' BGRA, top-down, no padding
    End Class

    ' Converted artwork, kept as long as the image itself is alive.
    Private Shared ReadOnly pixelCache As New ConditionalWeakTable(Of Image, Pixels)()

    Private Shared Function PixelsOf(ByVal image As Image) As Pixels
        If image Is Nothing Then Return Nothing
        Dim cached As Pixels = Nothing
        SyncLock pixelCache
            If pixelCache.TryGetValue(image, cached) Then Return cached
        End SyncLock
        If image.Width <= 0 OrElse image.Height <= 0 Then Return Nothing
        Dim result As New Pixels With {.Width = image.Width, .Height = image.Height}
        Using copy As New Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb)
            Using g As Graphics = Graphics.FromImage(copy)
                g.Clear(Color.Transparent)
                g.CompositingMode = Drawing2D.CompositingMode.SourceCopy
                g.DrawImage(image, New Rectangle(0, 0, image.Width, image.Height))
            End Using
            Dim data As BitmapData = copy.LockBits(New Rectangle(0, 0, copy.Width, copy.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb)
            Try
                result.Data = New Byte(copy.Width * copy.Height * 4 - 1) {}
                For y As Integer = 0 To copy.Height - 1
                    Dim row As Integer = If(data.Stride >= 0, y, copy.Height - 1 - y)
                    Marshal.Copy(IntPtr.Add(data.Scan0, row * Math.Abs(data.Stride)), result.Data, y * copy.Width * 4, copy.Width * 4)
                Next
            Finally
                copy.UnlockBits(data)
            End Try
        End Using
        SyncLock pixelCache
            Dim existing As Pixels = Nothing
            If pixelCache.TryGetValue(image, existing) Then Return existing
            pixelCache.Add(image, result)
        End SyncLock
        Return result
    End Function

    ' Where every sample of the window looks on the strip, and how much light
    ' it gets. Depends only on the size and the sliders, not on the number or
    ' the roll, so a rolling reel only samples the artwork for each frame.
    Private NotInheritable Class Layout
        Public U As Double()     ' strip position in numbers (down = +), roll 0
        Public Light As Double()
        Public HalfRange As Double
        Public CentreSlope As Double
    End Class

    Private Shared ReadOnly layoutCache As New Dictionary(Of String, Layout)()

    Private Shared Function LayoutOf(ByVal w As Integer, ByVal h As Integer,
                                     ByVal b As Double, ByVal d As Double, ByVal c As Double, ByVal s As Double) As Layout
        Dim key As String = w.ToString() & "x" & h.ToString() & "|" & b.ToString("R") & "|" & d.ToString("R") & "|" & c.ToString("R") & "|" & s.ToString("R")
        SyncLock layoutCache
            Dim cached As Layout = Nothing
            If layoutCache.TryGetValue(key, cached) Then Return cached
        End SyncLock

        ' Half of the strip seen through the window, in numbers: 0.5 (one flat
        ' number) at no curve, a whole number each way at 200, 1.25 at 300.
        Dim result As New Layout()
        result.HalfRange = Math.Min(1.25, 0.5 + 0.25 * c)
        Dim alpha As Double = result.HalfRange * Pitch
        result.CentreSlope = 1.0 + (Math.Sin(alpha) / alpha - 1.0) * c
        result.U = New Double(w * h * 4 - 1) {}
        result.Light = New Double(w * h * 4 - 1) {}
        Dim n As Integer = 0
        For y As Integer = 0 To h - 1
            For x As Integer = 0 To w - 1
                ' 2 x 2 samples per pixel
                For sy As Integer = 0 To 1
                    For sx As Integer = 0 To 1
                        Dim px As Double = x + 0.25 + 0.5 * sx
                        Dim py As Double = y + 0.25 + 0.5 * sy
                        Dim t As Double = 2.0 * py / h - 1.0
                        Dim cyl As Double = Math.Asin(Math.Max(-1.0, Math.Min(1.0, t * Math.Sin(alpha)))) / alpha
                        Dim f As Double = Math.Max(-1.5, Math.Min(1.5, t + (cyl - t) * c))
                        result.U(n) = f * result.HalfRange
                        ' Lighting
                        Dim xn As Double = px / w
                        Dim yn As Double = py / h
                        Dim turn As Double = Math.Cos(Math.Min(1.0, Math.Abs(f)) * 78.0 * Math.PI / 180.0)
                        Dim shade As Double = Math.Max(0.15, 1.0 - d * 0.7 * (1.0 - Math.Pow(turn, 1.6)))
                        Dim side As Double = 1.0 - d * 0.28 * (1.0 - Smooth(0.0, 0.16, xn) * Smooth(0.0, 0.16, 1.0 - xn))
                        Dim lip As Double = Math.Max(0.0, 1.0 - s * 0.78 * (1.0 - Smooth(0.0, 0.26, yn))) *
                                            Math.Max(0.0, 1.0 - s * 0.45 * (1.0 - Smooth(0.0, 0.1, 1.0 - yn)))
                        Dim lamp As Double = Math.Exp(-(((xn - 0.5) / 0.55) ^ 2 + ((yn - 0.3) / 0.45) ^ 2))
                        result.Light(n) = shade * side * lip * (0.28 + b * (0.5 + 0.42 * lamp)) * 1.05
                        n += 1
                    Next
                Next
            Next
        Next
        SyncLock layoutCache
            If layoutCache.Count >= 16 Then layoutCache.Clear()
            layoutCache(key) = result
        End SyncLock
        Return result
    End Function

    ' numbers: the strip around the shown number, index 0 to 4 = offsets -2 to
    ' +2 (+1 = the next number, which sits above; Nothing = plain paper).
    ' roll: -1 to 1, how far the strip has turned toward the next (+) or the
    ' previous (-) number; 0 = the shown number sits in the window.
    ' digitWidth: 0 = keep the number's own proportions (or stretch to the box
    ' when keepAspect is False), > 0 = that width in box pixels.
    Public Shared Function Render(ByVal numbers As Image(),
                                  ByVal target As Size,
                                  ByVal keepAspect As Boolean,
                                  ByVal digitWidth As Integer,
                                  ByVal brightness As Integer,
                                  ByVal depth As Integer,
                                  ByVal curve As Integer,
                                  ByVal shadow As Integer,
                                  ByVal roll As Double) As Bitmap
        If numbers Is Nothing OrElse numbers.Length < 5 OrElse numbers(2) Is Nothing OrElse target.Width <= 0 OrElse target.Height <= 0 Then Return Nothing
        Dim strip(4) As Pixels
        For i As Integer = 0 To 4
            strip(i) = PixelsOf(numbers(i))
        Next
        If strip(2) Is Nothing Then Return Nothing
        Dim w As Integer = target.Width
        Dim h As Integer = target.Height
        Dim output As Byte() = RenderPixels(strip, w, h, keepAspect, digitWidth,
                                            Math.Max(0, Math.Min(400, brightness)) / 100.0,
                                            Math.Max(0, Math.Min(200, depth)) / 100.0,
                                            Math.Max(0, Math.Min(MaximumCurve, curve)) / 100.0,
                                            Math.Max(0, Math.Min(200, shadow)) / 100.0,
                                            Math.Max(-1.0, Math.Min(1.0, roll)))
        Dim result As New Bitmap(w, h, PixelFormat.Format32bppArgb)
        Dim data As BitmapData = result.LockBits(New Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb)
        Try
            For y As Integer = 0 To h - 1
                Dim row As Integer = If(data.Stride >= 0, y, h - 1 - y)
                Marshal.Copy(output, y * w * 4, IntPtr.Add(data.Scan0, row * Math.Abs(data.Stride)), w * 4)
            Next
        Finally
            result.UnlockBits(data)
        End Try
        Return result
    End Function

    Private Shared Function RenderPixels(ByVal strip As Pixels(),
                                         ByVal w As Integer, ByVal h As Integer,
                                         ByVal keepAspect As Boolean, ByVal digitWidth As Integer,
                                         ByVal b As Double, ByVal d As Double, ByVal c As Double, ByVal s As Double,
                                         ByVal roll As Double) As Byte()
        Dim cur As Pixels = strip(2)
        ' Paper colour: the average of the shown number's left column.
        Dim paperB As Double = 0, paperG As Double = 0, paperR As Double = 0, paperWeight As Double = 0
        For y As Integer = 0 To cur.Height - 1
            Dim i As Integer = (y * cur.Width) * 4
            Dim a As Double = cur.Data(i + 3) / 255.0
            paperB += cur.Data(i) * a : paperG += cur.Data(i + 1) * a : paperR += cur.Data(i + 2) * a
            paperWeight += a
        Next
        If paperWeight > 0 Then
            paperB /= paperWeight : paperG /= paperWeight : paperR /= paperWeight
        Else
            paperB = 255 : paperG = 255 : paperR = 255
        End If

        Dim lay As Layout = LayoutOf(w, h, b, d, c, s)
        Dim cellHeight As Double = h / (2.0 * lay.HalfRange * lay.CentreSlope)
        Dim numberHeight As Double = cellHeight / (1.0 + NumberGap)
        Dim glyphWidth As Double
        If keepAspect Then
            glyphWidth = If(digitWidth > 0, Math.Min(w, digitWidth), cur.Width * numberHeight / cur.Height)
        Else
            glyphWidth = w
        End If

        Dim output As Byte() = New Byte(w * h * 4 - 1) {}
        Dim sum(2) As Double
        Dim n As Integer = 0
        For y As Integer = 0 To h - 1
            For x As Integer = 0 To w - 1
                sum(0) = 0 : sum(1) = 0 : sum(2) = 0
                For sy As Integer = 0 To 1
                    For sx As Integer = 0 To 1
                        Dim px As Double = x + 0.25 + 0.5 * sx
                        ' Turning toward the next number moves the strip down.
                        Dim u As Double = lay.U(n) - roll
                        Dim k As Integer = CInt(Math.Floor(u + 0.5))
                        Dim v As Double = u - k
                        Dim img As Pixels = If(k >= -2 AndAlso k <= 2, strip(2 - k), Nothing)
                        Dim rB As Double = paperB, rG As Double = paperG, rR As Double = paperR
                        If img IsNot Nothing Then
                            Dim iy As Double = ((v + 0.5) * (1.0 + NumberGap) - NumberGap / 2.0) * img.Height
                            Dim ix As Double = (px - w / 2.0) * img.Width / glyphWidth + img.Width / 2.0
                            If iy >= 0 AndAlso iy < img.Height AndAlso ix >= 0 AndAlso ix < img.Width Then
                                SampleOverPaper(img, ix - 0.5, iy - 0.5, paperB, paperG, paperR, rB, rG, rR)
                            End If
                        End If
                        Dim light As Double = lay.Light(n)
                        sum(0) += rB * light : sum(1) += rG * light : sum(2) += rR * light
                        n += 1
                    Next
                Next
                Dim o As Integer = (y * w + x) * 4
                output(o) = ClampByte(sum(0) / 4.0)
                output(o + 1) = ClampByte(sum(1) / 4.0)
                output(o + 2) = ClampByte(sum(2) / 4.0)
                output(o + 3) = 255
            Next
        Next
        Return output
    End Function

    ' Bilinear sample (edge clamped) composited over the paper colour.
    Private Shared Sub SampleOverPaper(ByVal img As Pixels, ByVal fx As Double, ByVal fy As Double,
                                       ByVal paperB As Double, ByVal paperG As Double, ByVal paperR As Double,
                                       ByRef rB As Double, ByRef rG As Double, ByRef rR As Double)
        Dim x0 As Integer = CInt(Math.Floor(fx))
        Dim y0 As Integer = CInt(Math.Floor(fy))
        Dim tx As Double = fx - x0
        Dim ty As Double = fy - y0
        rB = 0 : rG = 0 : rR = 0
        For j As Integer = 0 To 1
            For i As Integer = 0 To 1
                Dim weight As Double = If(i = 0, 1.0 - tx, tx) * If(j = 0, 1.0 - ty, ty)
                Dim sx As Integer = Math.Max(0, Math.Min(img.Width - 1, x0 + i))
                Dim sy As Integer = Math.Max(0, Math.Min(img.Height - 1, y0 + j))
                Dim p As Integer = (sy * img.Width + sx) * 4
                Dim a As Double = img.Data(p + 3) / 255.0
                rB += weight * (img.Data(p) * a + paperB * (1.0 - a))
                rG += weight * (img.Data(p + 1) * a + paperG * (1.0 - a))
                rR += weight * (img.Data(p + 2) * a + paperR * (1.0 - a))
            Next
        Next
    End Sub

    Friend Shared Function Smooth(ByVal e0 As Double, ByVal e1 As Double, ByVal x As Double) As Double
        Dim t As Double = Math.Max(0.0, Math.Min(1.0, (x - e0) / (e1 - e0)))
        Return t * t * (3.0 - 2.0 * t)
    End Function

    Private Shared Function ClampByte(ByVal value As Double) As Byte
        Return CByte(Math.Max(0, Math.Min(255, CInt(Math.Round(value)))))
    End Function

End Class

' B2S Pro soft glass for 3D reels, shared by the Designer and the Server (the
' plugin has a line-for-line C++ port). One sheet of glass over the whole score
' window reflects the room: broad, soft-edged light with no hard lines, and the
' glass takes away a little light. Every position is a share of the window
' height and width, so it looks the same at any size (WYSIWYG).
Public NotInheritable Class ReelGlass

    Private Sub New()
    End Sub

    Public Const StyleLines As Integer = 0      ' the original reflection (lines and band)
    Public Const StyleSoftWide As Integer = 1   ' one wide soft reflection
    Public Const StyleSoftStreaks As Integer = 2 ' soft diagonal streaks
    Public Const MaximumStyle As Integer = 2

    Private Shared ReadOnly cache As New Dictionary(Of String, Bitmap)()

    ' Draws the soft reflection over window. glassReflection 0 to 200 (%).
    Public Shared Sub DrawReflection(ByVal graphics As Graphics, ByVal window As Rectangle,
                                     ByVal style As Integer, ByVal glassReflection As Integer)
        If graphics Is Nothing OrElse window.Width <= 0 OrElse window.Height <= 0 Then Return
        If style <> StyleSoftWide AndAlso style <> StyleSoftStreaks Then Return
        glassReflection = Math.Max(0, Math.Min(200, glassReflection))
        If glassReflection <= 0 Then Return
        Dim key As String = window.Width.ToString() & "x" & window.Height.ToString() & "|" & style.ToString() & "|" & glassReflection.ToString()
        Dim overlay As Bitmap = Nothing
        SyncLock cache
            If Not cache.TryGetValue(key, overlay) Then
                If cache.Count >= 16 Then
                    For Each old As Bitmap In cache.Values
                        old.Dispose()
                    Next
                    cache.Clear()
                End If
                overlay = CreateReflection(window.Width, window.Height, style, glassReflection)
                cache(key) = overlay
            End If
            Dim oldMode As Drawing2D.CompositingMode = graphics.CompositingMode
            graphics.CompositingMode = Drawing2D.CompositingMode.SourceOver
            graphics.DrawImage(overlay, New Rectangle(window.X, window.Y, window.Width, window.Height),
                               0, 0, overlay.Width, overlay.Height, GraphicsUnit.Pixel)
            graphics.CompositingMode = oldMode
        End SyncLock
    End Sub

    Private Shared Function CreateReflection(ByVal w As Integer, ByVal h As Integer,
                                             ByVal style As Integer, ByVal glassReflection As Integer) As Bitmap
        Dim amount As Double = glassReflection / 100.0
        Dim absorb As Double = 0.04 * amount
        Dim raw As Byte() = New Byte(w * h * 4 - 1) {}
        For y As Integer = 0 To h - 1
            For x As Integer = 0 To w - 1
                Dim r As Double = Reflection(x, y, w, h, style) * amount
                r = Math.Max(0.0, Math.Min(1.0, r))
                Dim a As Double = 1.0 - (1.0 - absorb) * (1.0 - r)
                Dim o As Integer = (y * w + x) * 4
                If a > 0 Then
                    ' Cool daylight colour, over the light the glass absorbs.
                    raw(o) = ClampByte(255.0 * 1.0 * r / a)
                    raw(o + 1) = ClampByte(255.0 * 0.97 * r / a)
                    raw(o + 2) = ClampByte(255.0 * 0.93 * r / a)
                    raw(o + 3) = ClampByte(255.0 * a)
                End If
            Next
        Next
        Dim result As New Bitmap(w, h, PixelFormat.Format32bppArgb)
        Dim data As BitmapData = result.LockBits(New Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb)
        Try
            For y As Integer = 0 To h - 1
                Dim row As Integer = If(data.Stride >= 0, y, h - 1 - y)
                Marshal.Copy(raw, y * w * 4, IntPtr.Add(data.Scan0, row * Math.Abs(data.Stride)), w * 4)
            Next
        Finally
            result.UnlockBits(data)
        End Try
        Return result
    End Function

    ' Reflected light at 100 %, before the strength slider.
    Private Shared Function Reflection(ByVal x As Integer, ByVal y As Integer, ByVal w As Integer, ByVal h As Integer, ByVal style As Integer) As Double
        Dim xn As Double = (x + 0.5) / h
        Dim yn As Double = (y + 0.5) / h
        ' Glass reflects a little more toward its top edge.
        If style = StyleSoftWide Then
            Dim dgl As Double = xn * 0.42 + yn - 0.2
            Dim sheen As Double = Math.Exp(-(dgl / 0.55) ^ 2) * 0.16
            Dim core As Double = Math.Exp(-((dgl - 0.05) / 0.16) ^ 2) * 0.2
            Dim second As Double = Math.Exp(-((dgl - 0.62) / 0.07) ^ 2) * 0.08
            Dim wave As Double = 0.75 + 0.25 * Math.Cos((x + 0.5) / w * Math.PI * 1.3 + 0.6)
            Dim fresnel As Double = 0.025 * (1.0 - ReelRealistic.Smooth(0.0, 0.6, yn))
            Return (sheen + core + second) * wave + fresnel
        Else
            Dim dgl As Double = xn * 0.9 - yn
            Dim period As Double = w / CDbl(h) * 0.9
            Dim p1 As Double = Math.Exp(-((dgl - period * 0.3) / 0.22) ^ 2) * 0.22
            Dim p2 As Double = Math.Exp(-((dgl - period * 0.3 - 0.48) / 0.08) ^ 2) * 0.12
            Dim p3 As Double = Math.Exp(-((dgl - period * 0.72) / 0.35) ^ 2) * 0.1
            Dim fresnel As Double = 0.03 * (1.0 - ReelRealistic.Smooth(0.0, 0.6, yn))
            Return p1 + p2 + p3 + fresnel
        End If
    End Function

    Private Shared Function ClampByte(ByVal value As Double) As Byte
        Return CByte(Math.Max(0, Math.Min(255, CInt(Math.Round(value)))))
    End Function

End Class

' B2S Pro reel dividers (Designer, Server; ported to the plugin's ProReelDividers):
' a solid black line, full window height, centred on each boundary between two
' digits, so every number reads as its own wheel. It is drawn into the
' display-wide window overlay before the inset shadow and glass, which pass over it.
Public NotInheritable Class ReelDivider

    Public Const DefaultWidth As Integer = 8
    Public Const MinimumWidth As Integer = 1
    Public Const MaximumWidth As Integer = 60

    Private Sub New()
    End Sub

    ' The centre of each boundary: halfway between the end of digit i and the
    ' start of digit i + 1, from a renderer's own digit layout.
    Public Shared Function Centers(ByVal digitLefts As IList(Of Double), ByVal digitWidth As Double) As Double()
        If digitLefts Is Nothing OrElse digitLefts.Count < 2 Then Return New Double() {}
        Dim result(digitLefts.Count - 2) As Double
        For i As Integer = 0 To digitLefts.Count - 2
            result(i) = (digitLefts(i) + digitWidth + digitLefts(i + 1)) / 2.0
        Next
        Return result
    End Function

    ' Whole pixels only, so every renderer fills exactly the same columns:
    ' x0 = floor(centre - width / 2 + 0.5), columns x0 .. x0 + width - 1.
    Public Shared Sub Draw(ByVal graphics As Graphics, ByVal window As Rectangle, ByVal centers As IList(Of Double), ByVal width As Integer)
        If graphics Is Nothing OrElse centers Is Nothing OrElse width <= 0 OrElse window.Width <= 0 OrElse window.Height <= 0 Then Return
        width = Math.Max(MinimumWidth, Math.Min(MaximumWidth, width))
        Dim state As Drawing2D.GraphicsState = graphics.Save()
        Try
            graphics.SmoothingMode = Drawing2D.SmoothingMode.None
            graphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.None
            graphics.CompositingMode = Drawing2D.CompositingMode.SourceCopy
            Using brush As New SolidBrush(Color.Black)
                For Each center As Double In centers
                    Dim x0 As Integer = CInt(Math.Floor(center - width / 2.0 + 0.5))
                    Dim line As Rectangle = Rectangle.Intersect(New Rectangle(window.X + x0, window.Y, width, window.Height), window)
                    If line.Width > 0 AndAlso line.Height > 0 Then graphics.FillRectangle(brush, line)
                Next
            End Using
        Finally
            graphics.Restore(state)
        End Try
    End Sub

End Class
