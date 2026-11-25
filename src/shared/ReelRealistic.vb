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
                                  ByVal roll As Double,
                                  Optional ByVal numberScale As Double = 1.0) As Bitmap
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
                                            Math.Max(-1.0, Math.Min(1.0, roll)), numberScale)
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
                                         ByVal roll As Double, ByVal numberScale As Double) As Byte()
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
        ' Number size (shared reel setting): 1 = unchanged.
        If numberScale > 0 AndAlso numberScale <> 1.0 Then glyphWidth = Math.Max(1.0, glyphWidth * numberScale)

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

' B2S Pro "Behind glass" reel style (Reel3DStyle = 2), shared by the Designer
' and the Server. It is the drawing of the Reel Glass Lab the user approved
' (B2S-Local-Work\Reel-Glass-Research-20261124\Reel-Glass-Lab.ps1): an evenly
' lit drum, the window's shadow and set back, softness behind the glass and a
' soft room reflection on the glass, with the lab's own sliders. Every size is
' a share of the reel box, so the look is the same at any size (WYSIWYG).
Public NotInheritable Class ReelBehindGlass

    Private Sub New()
    End Sub

    Public Const Style As Integer = 2

    ' The lab's sliders. The defaults are the settings the user chose in the
    ' lab on 2026-11-24.
    Public NotInheritable Class Settings
        Public Reflection As Integer = 100   ' Glass reflection, 0 to 300
        Public Angle As Integer = 50         ' Reflection angle, -50 to 50
        Public Shadow As Integer = 118       ' Shadow under the window edge, 0 to 200
        Public SetBack As Integer = 0        ' Reels set back (dark gap), 0 to 200
        Public Softness As Integer = 3       ' Softness behind the glass, 0 to 200
        Public Brightness As Integer = 100   ' Reel brightness, 40 to 160
        Public Warmth As Integer = 100       ' Paper warmth (cream), 0 to 200
        Public Curve As Integer = 60         ' Drum curve, 0 to 200
        Public NumberSize As Integer = 87    ' Number size, 50 to 150

        Public Function Clone() As Settings
            Return DirectCast(MemberwiseClone(), Settings)
        End Function

        ' The settings that change nothing: used for Classic and Realistic reels
        ' that have no saved values, so they look exactly as before.
        Public Shared Function Neutral() As Settings
            Return New Settings With {.Reflection = 0, .Shadow = 0, .SetBack = 0, .Softness = 0,
                                      .Brightness = 100, .Warmth = 0, .NumberSize = 100}
        End Function

        Public Function IsNeutral() As Boolean
            Dim n As Settings = Neutral()
            Return Reflection = n.Reflection AndAlso Angle = n.Angle AndAlso Shadow = n.Shadow AndAlso SetBack = n.SetBack AndAlso
                   Softness = n.Softness AndAlso Brightness = n.Brightness AndAlso Warmth = n.Warmth AndAlso
                   Curve = n.Curve AndAlso NumberSize = n.NumberSize
        End Function

        ' True when FinishReel would change nothing.
        Public Function FinishIsNeutral() As Boolean
            Return Softness = 0 AndAlso Brightness = 100 AndAlso Warmth = 0
        End Function

        ' True when DrawOverlay would draw nothing.
        Public Function OverlayIsNeutral() As Boolean
            Return Reflection = 0 AndAlso Shadow = 0 AndAlso SetBack = 0
        End Function

        Public Sub Clamp()
            Reflection = Math.Max(0, Math.Min(300, Reflection))
            Angle = Math.Max(-50, Math.Min(50, Angle))
            Shadow = Math.Max(0, Math.Min(200, Shadow))
            SetBack = Math.Max(0, Math.Min(200, SetBack))
            Softness = Math.Max(0, Math.Min(200, Softness))
            Brightness = Math.Max(40, Math.Min(160, Brightness))
            Warmth = Math.Max(0, Math.Min(200, Warmth))
            Curve = Math.Max(0, Math.Min(200, Curve))
            NumberSize = Math.Max(50, Math.Min(150, NumberSize))
        End Sub

        Public Function ReelKey() As String
            Return Softness.ToString() & "|" & Brightness.ToString() & "|" & Warmth.ToString() & "|" & Curve.ToString() & "|" & NumberSize.ToString()
        End Function

        Public Function OverlayKey() As String
            Return Reflection.ToString() & "|" & Angle.ToString() & "|" & Shadow.ToString() & "|" & SetBack.ToString()
        End Function

        ' Score attributes (only written for the Behind glass style).
        Public Sub WriteTo(ByVal node As System.Xml.XmlElement)
            Clamp()
            node.SetAttribute("ReelGlassReflection", Reflection.ToString())
            node.SetAttribute("ReelGlassAngle", Angle.ToString())
            node.SetAttribute("ReelGlassShadow", Shadow.ToString())
            node.SetAttribute("ReelGlassSetBack", SetBack.ToString())
            node.SetAttribute("ReelGlassSoftness", Softness.ToString())
            node.SetAttribute("ReelGlassBrightness", Brightness.ToString())
            node.SetAttribute("ReelGlassWarmth", Warmth.ToString())
            node.SetAttribute("ReelGlassCurve", Curve.ToString())
            node.SetAttribute("ReelGlassNumberSize", NumberSize.ToString())
        End Sub

        Public Shared Function ReadFrom(ByVal node As System.Xml.XmlNode) As Settings
            Return ReadFrom(node, New Settings())
        End Function

        ' fallback: the values used for attributes the file does not have.
        Public Shared Function ReadFrom(ByVal node As System.Xml.XmlNode, ByVal fallback As Settings) As Settings
            Dim result As Settings = If(fallback Is Nothing, New Settings(), fallback.Clone())
            If node IsNot Nothing Then
                result.Reflection = ReadInt(node, "ReelGlassReflection", result.Reflection)
                result.Angle = ReadInt(node, "ReelGlassAngle", result.Angle)
                result.Shadow = ReadInt(node, "ReelGlassShadow", result.Shadow)
                result.SetBack = ReadInt(node, "ReelGlassSetBack", result.SetBack)
                result.Softness = ReadInt(node, "ReelGlassSoftness", result.Softness)
                result.Brightness = ReadInt(node, "ReelGlassBrightness", result.Brightness)
                result.Warmth = ReadInt(node, "ReelGlassWarmth", result.Warmth)
                result.Curve = ReadInt(node, "ReelGlassCurve", result.Curve)
                result.NumberSize = ReadInt(node, "ReelGlassNumberSize", result.NumberSize)
            End If
            result.Clamp()
            Return result
        End Function

        Private Shared Function ReadInt(ByVal node As System.Xml.XmlNode, ByVal name As String, ByVal fallback As Integer) As Integer
            Dim attribute As System.Xml.XmlAttribute = If(node.Attributes Is Nothing, Nothing, node.Attributes(name))
            Dim value As Integer
            If attribute IsNot Nothing AndAlso Integer.TryParse(attribute.Value, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, value) Then Return value
            Return fallback
        End Function
    End Class

    Private NotInheritable Class Pixels
        Public Width As Integer
        Public Height As Integer
        Public Data As Byte() ' BGRA, top-down, no padding
    End Class

    Private Shared ReadOnly pixelCache As New ConditionalWeakTable(Of Image, Pixels)()

    ' The artwork as 32 bpp ARGB (as the lab reads it).
    Private Shared Function PixelsOf(ByVal image As Image) As Pixels
        If image Is Nothing OrElse image.Width <= 0 OrElse image.Height <= 0 Then Return Nothing
        Dim cached As Pixels = Nothing
        SyncLock pixelCache
            If pixelCache.TryGetValue(image, cached) Then Return cached
        End SyncLock
        Dim result As New Pixels With {.Width = image.Width, .Height = image.Height}
        result.Data = New Byte(image.Width * image.Height * 4 - 1) {}
        Dim bitmap As Bitmap = TryCast(image, Bitmap)
        Dim owned As Bitmap = Nothing
        If bitmap Is Nothing Then
            owned = New Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb)
            Using g As Graphics = Graphics.FromImage(owned)
                g.Clear(Color.Transparent)
                g.CompositingMode = Drawing2D.CompositingMode.SourceCopy
                g.DrawImage(image, New Rectangle(0, 0, image.Width, image.Height))
            End Using
            bitmap = owned
        End If
        Try
            SyncLock image
                Dim data As BitmapData = bitmap.LockBits(New Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb)
                Try
                    For y As Integer = 0 To bitmap.Height - 1
                        Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), result.Data, y * bitmap.Width * 4, bitmap.Width * 4)
                    Next
                Finally
                    bitmap.UnlockBits(data)
                End Try
            End SyncLock
        Finally
            If owned IsNot Nothing Then owned.Dispose()
        End Try
        SyncLock pixelCache
            Dim existing As Pixels = Nothing
            If pixelCache.TryGetValue(image, existing) Then Return existing
            pixelCache.Add(image, result)
        End SyncLock
        Return result
    End Function

    ' One reel (the lab's GlassLab.Reel, with the drum turned by roll).
    ' numbers: offsets -2 to +2 around the shown number (+1 = next, above).
    ' glyphWidth: the number's width in box pixels before Number size.
    ' backlightOn = False: a triggered reel whose light is off (dimmed).
    Public Shared Function RenderReel(ByVal numbers As Image(), ByVal target As Size, ByVal glyphWidth As Double,
                                      ByVal settings As Settings, ByVal roll As Double, ByVal backlightOn As Boolean,
                                      Optional ByVal brightnessScale As Double = 1.0) As Bitmap
        If numbers Is Nothing OrElse numbers.Length < 5 OrElse numbers(2) Is Nothing OrElse settings Is Nothing OrElse target.Width <= 0 OrElse target.Height <= 0 Then Return Nothing
        Dim s As Settings = settings.Clone()
        s.Clamp()
        Dim strip(4) As Pixels
        For i As Integer = 0 To 4
            strip(i) = PixelsOf(numbers(i))
        Next
        Dim cur As Pixels = strip(2)
        If cur Is Nothing Then Return Nothing
        Dim w As Integer = target.Width
        Dim h As Integer = target.Height
        Dim glyphW As Integer = Math.Max(1, CInt(glyphWidth * s.NumberSize / 100.0))
        Dim bright As Double = s.Brightness / 100.0 * If(backlightOn, 1.0, 0.28) * Math.Max(0.0, brightnessScale)
        Dim warm As Double = s.Warmth / 100.0
        Dim curve As Double = s.Curve / 100.0
        roll = Math.Max(-1.0, Math.Min(1.0, roll))
        Dim pb As Double = cur.Data(0), pg As Double = cur.Data(1), pr As Double = cur.Data(2)
        Dim half As Double = 0.48 + 0.32 * curve
        Dim alpha As Double = Math.Min(1.45, 0.35 + 0.45 * curve)
        Const gap As Double = 0.3
        Dim o As Byte() = New Byte(w * h * 4 - 1) {}
        For y As Integer = 0 To h - 1
            For x As Integer = 0 To w - 1
                Dim sB As Double = 0, sG As Double = 0, sR As Double = 0
                For sy As Integer = 0 To 1
                    For sx As Integer = 0 To 1
                        Dim py As Double = y + 0.25 + 0.5 * sy
                        Dim pxx As Double = x + 0.25 + 0.5 * sx
                        Dim t As Double = 2.0 * py / h - 1
                        Dim ang As Double = Math.Asin(Math.Max(-1, Math.Min(1, t * Math.Sin(alpha))))
                        Dim f As Double = ang / alpha
                        Dim u As Double = f * half - roll
                        Dim k As Integer = CInt(Math.Floor(u + 0.5))
                        Dim v As Double = u - k
                        Dim light As Double = 1.0 - (0.75 * Math.Min(1, curve)) * (1 - Math.Pow(Math.Cos(Math.Min(1, Math.Abs(f)) * 1.25), 0.9))
                        Dim rB As Double = pb, rG As Double = pg, rR As Double = pr
                        If k >= -2 AndAlso k <= 2 AndAlso strip(2 - k) IsNot Nothing Then
                            Dim img As Pixels = strip(2 - k)
                            Dim iw As Integer = img.Width, ih As Integer = img.Height
                            Dim iy As Double = ((v + 0.5) * (1 + gap) - gap / 2) * ih
                            Dim ix As Double = (pxx - w / 2.0) * iw / CDbl(glyphW) + iw / 2.0
                            If iy >= 0 AndAlso iy < ih - 1 AndAlso ix >= 0 AndAlso ix < iw - 1 Then
                                Dim x0 As Integer = CInt(Math.Floor(ix)), y0 As Integer = CInt(Math.Floor(iy))
                                Dim tx As Double = ix - x0, ty As Double = iy - y0
                                rB = 0 : rG = 0 : rR = 0
                                For j As Integer = 0 To 1
                                    For i As Integer = 0 To 1
                                        Dim wgt As Double = If(i = 0, 1 - tx, tx) * If(j = 0, 1 - ty, ty)
                                        Dim p As Integer = ((y0 + j) * iw + x0 + i) * 4
                                        Dim a As Double = img.Data(p + 3) / 255.0
                                        rB += wgt * (img.Data(p) * a + pb * (1 - a))
                                        rG += wgt * (img.Data(p + 1) * a + pg * (1 - a))
                                        rR += wgt * (img.Data(p + 2) * a + pr * (1 - a))
                                    Next
                                Next
                            End If
                        End If
                        sB += rB * light : sG += rG * light : sR += rR * light
                    Next
                Next
                Dim q As Integer = (y * w + x) * 4
                o(q) = ToByte(sB / 4 * bright * (1 - 0.3 * warm))
                o(q + 1) = ToByte(sG / 4 * bright * (1 - 0.08 * warm))
                o(q + 2) = ToByte(sR / 4 * bright)
                o(q + 3) = 255
            Next
        Next
        If s.Softness > 0 Then Soften(o, w, h, s.Softness / 100.0)
        Dim result As New Bitmap(w, h, PixelFormat.Format32bppArgb)
        Dim bd As BitmapData = result.LockBits(New Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb)
        Try
            For y As Integer = 0 To h - 1
                Marshal.Copy(o, y * w * 4, IntPtr.Add(bd.Scan0, y * bd.Stride), w * 4)
            Next
        Finally
            result.UnlockBits(bd)
        End Try
        Return result
    End Function

    ' The lab's Reel brightness, Paper warmth and Softness (the same math as
    ' RenderReel) on a reel drawn by another style (Classic or Realistic), in place.
    Public Shared Sub FinishReel(ByVal reel As Bitmap, ByVal settings As Settings)
        If reel Is Nothing OrElse settings Is Nothing OrElse reel.Width <= 0 OrElse reel.Height <= 0 Then Return
        Dim s As Settings = settings.Clone()
        s.Clamp()
        If s.FinishIsNeutral() Then Return
        Dim w As Integer = reel.Width
        Dim h As Integer = reel.Height
        Dim bright As Double = s.Brightness / 100.0
        Dim warm As Double = s.Warmth / 100.0
        Dim o As Byte() = New Byte(w * h * 4 - 1) {}
        Dim bd As BitmapData = reel.LockBits(New Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb)
        Try
            For y As Integer = 0 To h - 1
                Marshal.Copy(IntPtr.Add(bd.Scan0, y * bd.Stride), o, y * w * 4, w * 4)
            Next
            For q As Integer = 0 To o.Length - 4 Step 4
                o(q) = ToByte(o(q) * bright * (1 - 0.3 * warm))
                o(q + 1) = ToByte(o(q + 1) * bright * (1 - 0.08 * warm))
                o(q + 2) = ToByte(o(q + 2) * bright)
            Next
            If s.Softness > 0 Then Soften(o, w, h, s.Softness / 100.0)
            For y As Integer = 0 To h - 1
                Marshal.Copy(o, y * w * 4, IntPtr.Add(bd.Scan0, y * bd.Stride), w * 4)
            Next
        Finally
            reel.UnlockBits(bd)
        End Try
    End Sub

    ' The lab's softness: a 3 x 3 blur mixed in and lifted blacks.
    Private Shared Sub Soften(ByVal a As Byte(), ByVal w As Integer, ByVal h As Integer, ByVal amount As Double)
        Dim src As Byte() = DirectCast(a.Clone(), Byte())
        Dim mix As Double = Math.Min(1, amount)
        For y As Integer = 0 To h - 1
            For x As Integer = 0 To w - 1
                Dim q As Integer = (y * w + x) * 4
                For c As Integer = 0 To 2
                    Dim sum As Integer = 0
                    For j As Integer = -1 To 1
                        Dim yy As Integer = Math.Max(0, Math.Min(h - 1, y + j))
                        For i As Integer = -1 To 1
                            Dim xx As Integer = Math.Max(0, Math.Min(w - 1, x + i))
                            sum += src((yy * w + xx) * 4 + c)
                        Next
                    Next
                    Dim blur As Double = sum / 9.0
                    Dim value As Double = src(q + c) * (1 - mix) + blur * mix
                    a(q + c) = ToByte(value * (1 - 0.07 * amount) + 12 * amount)
                Next
            Next
        Next
    End Sub

    Private Shared ReadOnly overlayCache As New Dictionary(Of String, Bitmap)()

    ' The window over the reels (the lab's Depth and Glass): the shadow under
    ' the window edge, the set back and the soft room reflection. Drawn over
    ' the reels and dividers of the whole score window.
    Public Shared Sub DrawOverlay(ByVal graphics As Graphics, ByVal window As Rectangle, ByVal settings As Settings)
        If graphics Is Nothing OrElse settings Is Nothing OrElse window.Width <= 2 OrElse window.Height <= 2 Then Return
        Dim s As Settings = settings.Clone()
        s.Clamp()
        Dim key As String = window.Width.ToString() & "x" & window.Height.ToString() & "|" & s.OverlayKey()
        SyncLock overlayCache
            Dim overlay As Bitmap = Nothing
            If Not overlayCache.TryGetValue(key, overlay) Then
                If overlayCache.Count >= 16 Then
                    For Each old As Bitmap In overlayCache.Values
                        old.Dispose()
                    Next
                    overlayCache.Clear()
                End If
                overlay = CreateOverlay(window.Width, window.Height, s)
                overlayCache(key) = overlay
            End If
            Dim oldMode As Drawing2D.CompositingMode = graphics.CompositingMode
            graphics.CompositingMode = Drawing2D.CompositingMode.SourceOver
            graphics.DrawImage(overlay, New Rectangle(window.X, window.Y, window.Width, window.Height),
                               0, 0, overlay.Width, overlay.Height, GraphicsUnit.Pixel)
            graphics.CompositingMode = oldMode
        End SyncLock
    End Sub

    Private Shared Function Sm(ByVal e0 As Double, ByVal e1 As Double, ByVal x As Double) As Double
        Dim t As Double = Math.Max(0, Math.Min(1, (x - e0) / (e1 - e0)))
        Return t * t * (3 - 2 * t)
    End Function

    Private Shared Function CreateOverlay(ByVal w As Integer, ByVal h As Integer, ByVal s As Settings) As Bitmap
        Dim shadow As Double = s.Shadow / 100.0
        Dim setBack As Double = s.SetBack / 100.0
        Dim amount As Double = s.Reflection / 100.0
        Dim slope As Double = s.Angle / 100.0
        Dim gapPx As Integer = CInt(Math.Round(h * 0.035 * setBack))
        ' The window's edge line (set back only): the lab's pen, inside the window.
        Dim edge As Integer = If(setBack > 0, Math.Max(1, CInt(Math.Round(Math.Max(2.0, h / 40.0) / 2.0))), 0)
        Dim H0 As Double = h, Wd As Double = w
        Dim raw As Byte() = New Byte(w * h * 4 - 1) {}
        For y As Integer = 0 To h - 1
            For x As Integer = 0 To w - 1
                Dim yn As Double = (y + 0.5) / h, xn As Double = (x + 0.5) / w
                ' Depth: the share of light left (1 = untouched).
                Dim m As Double
                If x < edge OrElse x >= w - edge OrElse y < edge OrElse y >= h - edge Then
                    m = 0.0
                ElseIf y < gapPx OrElse y >= h - gapPx OrElse x < gapPx OrElse x >= w - gapPx Then
                    m = 0.1
                Else
                    m = (1 - Math.Min(0.95, 0.62 * shadow) * (1 - Sm(0, 0.2, yn))) *
                        (1 - Math.Min(0.9, 0.3 * shadow) * (1 - Sm(0, 0.09, 1 - yn))) *
                        (1 - 0.25 * shadow * (1 - Sm(0, 0.04, xn))) *
                        (1 - 0.25 * shadow * (1 - Sm(0, 0.04, 1 - xn)))
                    m = Math.Max(0, m)
                End If
                ' Glass: light the glass adds, per colour (B, G, R).
                Dim gxn As Double = x / H0, gyn As Double = y / H0
                Dim d As Double = gyn + gxn * slope
                Dim band As Double = Math.Exp(-Math.Pow((d - 0.35) / 0.22, 2)) * 0.2 * (0.7 + 0.3 * Math.Cos(x / Wd * 3.0))
                Dim thin As Double = Math.Exp(-Math.Pow((d - 0.85) / 0.05, 2)) * 0.08
                Dim b1 As Double = Math.Exp(-(Math.Pow((x - Wd * 0.8) / (H0 * 0.9), 2) + Math.Pow((y - H0 * 0.25) / (H0 * 0.45), 2))) * 0.18
                Const haze As Double = 0.03
                Dim lB As Double = Math.Min(1, ((band + thin + haze) + b1 * 0.55) * amount)
                Dim lG As Double = Math.Min(1, ((band + thin + haze) * 0.98 + b1 * 0.75) * amount)
                Dim lR As Double = Math.Min(1, ((band + thin + haze) * 0.95 + b1) * amount)
                Dim ag As Double = Math.Max(lB, Math.Max(lG, lR))
                ' One ARGB pixel: the dark first, the reflected light over it.
                Dim aTotal As Double = 1 - m * (1 - ag)
                Dim q As Integer = (y * w + x) * 4
                If aTotal > 0 Then
                    raw(q) = ToByte(255 * lB / aTotal)
                    raw(q + 1) = ToByte(255 * lG / aTotal)
                    raw(q + 2) = ToByte(255 * lR / aTotal)
                    raw(q + 3) = ToByte(255 * aTotal)
                End If
            Next
        Next
        Dim result As New Bitmap(w, h, PixelFormat.Format32bppArgb)
        Dim bd As BitmapData = result.LockBits(New Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb)
        Try
            For y As Integer = 0 To h - 1
                Marshal.Copy(raw, y * w * 4, IntPtr.Add(bd.Scan0, y * bd.Stride), w * 4)
            Next
        Finally
            result.UnlockBits(bd)
        End Try
        Return result
    End Function

    Private Shared Function ToByte(ByVal value As Double) As Byte
        Return CByte(Math.Max(0, Math.Min(255, Math.Round(value))))
    End Function

End Class
