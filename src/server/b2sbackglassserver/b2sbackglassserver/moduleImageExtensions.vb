#Disable Warning BC42016, BC42017, BC42018, BC42019, BC42032
Imports System
Imports System.Drawing

Module moduleImageExtensions

    ' Full-canvas artwork is sampled once at load time. Keep the existing
    ' snippet/animation resizing paths separate from this presentation filter.
    <System.Runtime.CompilerServices.Extension()> _
    Public Function ResizedCanvas(image As Image, size As Size) As Image
        If image Is Nothing OrElse size.Width <= 0 OrElse size.Height <= 0 Then Return Nothing
        Dim ret As New Bitmap(size.Width, size.Height)
        If image.Size = size Then
            Dim native As Bitmap = DirectCast(image.Clone(), Bitmap)
            native.SetResolution(ret.HorizontalResolution, ret.VerticalResolution)
            ret.Dispose()
            Return native
        End If
        Using gr As Graphics = Graphics.FromImage(ret)
            gr.PageUnit = GraphicsUnit.Pixel
            gr.CompositingMode = Drawing2D.CompositingMode.SourceCopy
            gr.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
            Using attributes As New Imaging.ImageAttributes()
                ' Sample the edge pixels rather than transparent pixels
                ' outside the image, avoiding a faded canvas border.
                attributes.SetWrapMode(Drawing2D.WrapMode.TileFlipXY)
                gr.DrawImage(image, New Rectangle(Point.Empty, size),
                             0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes)
            End Using
        End Using
        Return ret
    End Function

    <System.Runtime.CompilerServices.Extension()> _
    Public Function Resized(image As Image, size As Size, Optional ByVal disposeOriginal As Boolean = False) As Image
        If image Is Nothing Then Return Nothing
        If size.Width <= 0 OrElse size.Height <= 0 Then Return Nothing
        Dim ret As Bitmap = New Bitmap(size.Width, size.Height)
        Using gr As Graphics = Graphics.FromImage(ret)
            gr.PageUnit = GraphicsUnit.Pixel
            'gr.InterpolationMode = Drawing2D.InterpolationMode.High
            gr.SmoothingMode = Drawing2D.SmoothingMode.HighQuality
            gr.DrawImage(image, New Rectangle(0, 0, ret.Width, ret.Height))
        End Using
        If disposeOriginal Then
            image.Dispose()
            image = Nothing
        End If
        Return ret
    End Function

    <System.Runtime.CompilerServices.Extension()> _
    Public Function Rotated(image As Image, angle As Integer) As Image
        If image Is Nothing Then Return Nothing
        Dim ret As Bitmap = New Bitmap(image.Width, image.Height)
        Dim matrix As New System.Drawing.Drawing2D.Matrix
        Using gr As Graphics = Graphics.FromImage(ret)
            gr.PageUnit = GraphicsUnit.Pixel
            'gr.InterpolationMode = Drawing2D.InterpolationMode.High
            gr.SmoothingMode = Drawing2D.SmoothingMode.HighQuality
            matrix.RotateAt(angle * -1, New Point(CInt(image.Width / 2), CInt(image.Height / 2)))
            gr.Transform = matrix
            gr.DrawImage(image, New Rectangle(0, 0, image.Width, image.Height))
        End Using
        Return ret
    End Function

    <System.Runtime.CompilerServices.Extension()> _
    Public Function ResizedF(image As Image, sizeF As SizeF, Optional ByVal disposeOriginal As Boolean = False) As Image
        If image Is Nothing Then Return Nothing
        If sizeF.Width <= 0 OrElse sizeF.Height <= 0 Then Return Nothing
        Dim largesize As Size = New Size(CInt(sizeF.Width) + 1, CInt(sizeF.Height) + 1)
        Dim ret As Bitmap = New Bitmap(largesize.Width, largesize.Height)
        Using gr As Graphics = Graphics.FromImage(ret)
            gr.PageUnit = GraphicsUnit.Pixel
            'gr.InterpolationMode = Drawing2D.InterpolationMode.High
            gr.SmoothingMode = Drawing2D.SmoothingMode.HighQuality
            gr.DrawImage(image, New Rectangle(0, 0, sizeF.Width, sizeF.Height))
        End Using
        If disposeOriginal Then
            image.Dispose()
            image = Nothing
        End If
        Return ret
    End Function

End Module
