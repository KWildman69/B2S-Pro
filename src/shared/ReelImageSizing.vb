Imports System.Drawing

Public NotInheritable Class ReelImageSizing
    Public Shared Function FittedContentSize(source As Image, target As Size) As Size
        If source Is Nothing OrElse target.Width <= 0 OrElse target.Height <= 0 Then Return Size.Empty
        Dim scale As Double = Math.Min(target.Width / CDbl(source.Width), target.Height / CDbl(source.Height))
        Return New Size(Math.Max(1, CInt(Math.Round(source.Width * scale))),
                        Math.Max(1, CInt(Math.Round(source.Height * scale))))
    End Function

    Public Shared Function Fit(source As Image, target As Size) As Bitmap
        If source Is Nothing OrElse target.Width <= 0 OrElse target.Height <= 0 Then Return Nothing
        Dim result As New Bitmap(target.Width, target.Height, Imaging.PixelFormat.Format32bppArgb)
        Dim contentSize As Size = FittedContentSize(source, target)
        Using g As Graphics = Graphics.FromImage(result)
            Dim background As Color = Color.Transparent
            If TypeOf source Is Bitmap Then background = DirectCast(source, Bitmap).GetPixel(0, 0)
            g.Clear(background)
            g.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
            g.DrawImage(source, New RectangleF((target.Width - contentSize.Width) / 2.0F,
                                                (target.Height - contentSize.Height) / 2.0F,
                                                contentSize.Width,
                                                contentSize.Height),
                              New RectangleF(0, 0, source.Width, source.Height), GraphicsUnit.Pixel)
        End Using
        Return result
    End Function

    Public Shared Function StretchToWidth(source As Image, target As Size, contentWidth As Integer) As Bitmap
        If source Is Nothing OrElse target.Width <= 0 OrElse target.Height <= 0 Then Return Nothing
        Dim result As New Bitmap(target.Width, target.Height, Imaging.PixelFormat.Format32bppArgb)
        Dim width As Integer = Math.Max(1, Math.Min(target.Width, contentWidth))
        Using g As Graphics = Graphics.FromImage(result)
            Dim background As Color = Color.Transparent
            If TypeOf source Is Bitmap Then background = DirectCast(source, Bitmap).GetPixel(0, 0)
            g.Clear(background)
            g.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
            g.DrawImage(source, New RectangleF((target.Width - width) / 2.0F, 0.0F, width, target.Height),
                              New RectangleF(0, 0, source.Width, source.Height), GraphicsUnit.Pixel)
        End Using
        Return result
    End Function
End Class
