Imports System

Public Module moduleImageExtensions

    <System.Runtime.CompilerServices.Extension()> _
    Public Function Resized(image As Image, size As Size) As Image
        If image Is Nothing Then Return Nothing
        If size.Width <= 0 OrElse size.Height <= 0 Then Return Nothing
        Dim ret As Bitmap = New Bitmap(size.Width, size.Height)
        Using gr As Graphics = Graphics.FromImage(ret)
            gr.PageUnit = GraphicsUnit.Pixel
            'gr.InterpolationMode = Drawing2D.InterpolationMode.High
            gr.SmoothingMode = Drawing2D.SmoothingMode.HighQuality
            gr.DrawImage(image, New Rectangle(0, 0, ret.Width, ret.Height))
        End Using
        Return ret
    End Function

    <System.Runtime.CompilerServices.Extension()> _
    Public Function PartFromImage(image As Image, area As Rectangle) As Image
        If image Is Nothing Then Return Nothing
        Dim imageBackground As Bitmap = CType(image, Bitmap)
        Dim imagePart As Image = New Bitmap(area.Width, area.Height)
        imagePart = imageBackground.Clone(area, Imaging.PixelFormat.Format32bppArgb)
        Return imagePart
    End Function

    <System.Runtime.CompilerServices.Extension()> _
    Public Function Clone(image As Image, width As Integer, height As Integer) As Image
        If image Is Nothing Then Return Nothing
        Dim ret As Image = New Bitmap(width, height)
        Using gr As Graphics = Graphics.FromImage(ret)
            gr.PageUnit = GraphicsUnit.Pixel
            'gr.InterpolationMode = Drawing2D.InterpolationMode.High
            gr.SmoothingMode = Drawing2D.SmoothingMode.HighQuality
            gr.DrawImage(image, New Rectangle(0, 0, ret.Width, ret.Height))
        End Using
        Return ret
    End Function

    <System.Runtime.CompilerServices.Extension()> _
    Public Function Copy(image As Image, Optional ByVal disposeOriginal As Boolean = False) As Image
        If image Is Nothing Then Return Nothing
        Dim ret As Image = New Bitmap(image)
        If disposeOriginal Then image.Dispose()
        Return ret
    End Function

    <System.Runtime.CompilerServices.Extension()> _
    Public Function Filters(ByVal b As Bitmap) As ImageFilter
        Return New ImageFilter(b)
    End Function

    Public Class ImageFilter
        Dim b As Bitmap
        Public Sub New(ByVal b As Bitmap)
            Me.b = b
        End Sub

        Public Shared Function SupportsLockBits(ByVal b As Bitmap) As Boolean
            Return Bitmap.GetPixelFormatSize(b.PixelFormat) = 32 AndAlso b.PixelFormat <> Imaging.PixelFormat.Indexed
        End Function

#Region "Bitmap Data"

        Private Class BitmapData
            Public Bitmap As Bitmap
            Public ByteData As Byte()
            Public BitmapData As Imaging.BitmapData

            Private Sub New()
                ' nothing to do
            End Sub

            Public Shared Function LockBits(ByVal b As Bitmap) As BitmapData
                If SupportsLockBits(b) = False Then
                    Throw New Exception("Your bitmap must be 32bit non-indexed in order to apply filters")
                End If
                LockBits = New BitmapData

                LockBits.Bitmap = b

                Dim bmpRect As New Rectangle(0, 0, b.Width, b.Height)
                ReDim LockBits.ByteData(b.Width * b.Height * 4 - 1)

                LockBits.BitmapData = b.LockBits(bmpRect, Imaging.ImageLockMode.ReadWrite, b.PixelFormat)
                System.Runtime.InteropServices.Marshal.Copy(LockBits.BitmapData.Scan0, LockBits.ByteData, 0, LockBits.ByteData.Length)
            End Function

            Public Sub UnlockBits()
                System.Runtime.InteropServices.Marshal.Copy(Me.ByteData, 0, Me.BitmapData.Scan0, Me.ByteData.Length)
                Me.Bitmap.UnlockBits(Me.BitmapData)
            End Sub

            Public Function YFromOffset(ByVal Offset As Integer) As Integer
                Return CInt(Int(Offset / (Bitmap.Width * 4)))
            End Function

            Public Function StartOfLineFromOffset(ByVal Offset As Integer) As Integer
                Return Offset - (Offset Mod (Bitmap.Width * 4))
            End Function

        End Class

#End Region

        Public Sub Brightness(Optional ByVal amount As Single = 0)
            If amount = 0 Then Return
            Dim bData = BitmapData.LockBits(b)
            If amount > 0 Then
                For ii = LBound(bData.ByteData) To UBound(bData.ByteData) Step 4
                    bData.ByteData(ii) = CByte(bData.ByteData(ii) + (amount * (255 - bData.ByteData(ii)))) 'blue
                    bData.ByteData(ii + 1) = CByte(bData.ByteData(ii + 1) + (amount * (255 - bData.ByteData(ii + 1)))) 'green
                    bData.ByteData(ii + 2) = CByte(bData.ByteData(ii + 2) + (amount * (255 - bData.ByteData(ii + 2)))) 'red
                Next
            Else
                For ii = LBound(bData.ByteData) To UBound(bData.ByteData) Step 4
                    bData.ByteData(ii) = CByte(bData.ByteData(ii) - (Math.Abs(amount) * bData.ByteData(ii))) 'blue
                    bData.ByteData(ii + 1) = CByte(bData.ByteData(ii + 1) - (Math.Abs(amount) * bData.ByteData(ii + 1))) 'green
                    bData.ByteData(ii + 2) = CByte(bData.ByteData(ii + 2) - (Math.Abs(amount) * bData.ByteData(ii + 2))) 'red
                Next
            End If
            bData.UnlockBits()
        End Sub

    End Class

End Module
