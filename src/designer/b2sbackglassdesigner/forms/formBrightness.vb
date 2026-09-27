Imports System

Public Class formBrightness

    Private sourceimage As Image = Nothing
    Private resetimage As Image = Nothing

    Public Shadows Function ShowDialog(ByVal owner As IWin32Window,
                                       ByRef image As Image,
                                       Optional ByVal originalBrightnessImage As Image = Nothing) As DialogResult
        ' show dialog
        sourceimage = image
        resetimage = If(originalBrightnessImage, image)
        NumericUpDownBrightness.Value = 0
        NumericUpDownGrillBrightness.Value = 0
        chkIgnoreGrill.Enabled = (Backglass.currentData.GrillHeight > 0 AndAlso Not Backglass.currentData.IsDMDImageShown)
        If chkIgnoreGrill.Enabled Then chkIgnoreGrill.Checked = True Else chkIgnoreGrill.Checked = False
        TrackBarGrillBrightness.Enabled = chkIgnoreGrill.Enabled
        NumericUpDownGrillBrightness.Enabled = chkIgnoreGrill.Enabled
        ChangeBrightness()
        ' now show the dialog
        Dim nRet As DialogResult = MyBase.ShowDialog(owner)
        If nRet = Windows.Forms.DialogResult.OK Then
            ' return new image
            image = CreateAdjustedImage(sourceimage.Size)
        End If
        Return nRet
    End Function

    Private Sub Ok_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnOk.Click
        MyBase.DialogResult = Windows.Forms.DialogResult.OK
        Me.Close()
    End Sub
    Private Sub Cancel_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnCancel.Click
        Me.Close()
    End Sub
    Private Sub ResetBrightness_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnResetBrightness.Click
        sourceimage = resetimage
        NumericUpDownBrightness.Value = 0
        NumericUpDownGrillBrightness.Value = 0
        ChangeBrightness()
    End Sub

    Private Sub formBrightness_ResizeBegin(sender As Object, e As System.EventArgs) Handles Me.ResizeBegin
        PictureBoxPreview.SizeMode = PictureBoxSizeMode.StretchImage
    End Sub
    Private Sub formBrightness_ResizeEnd(sender As Object, e As System.EventArgs) Handles Me.ResizeEnd
        ChangeBrightness()
        PictureBoxPreview.SizeMode = PictureBoxSizeMode.Normal
    End Sub

    Private Sub TrackBarBrightness_Scroll(sender As System.Object, e As System.EventArgs) Handles TrackBarBrightness.Scroll
        NumericUpDownBrightness.Value = TrackBarBrightness.Value
    End Sub
    Private Sub NumericUpDownBrightness_ValueChanged(sender As System.Object, e As System.EventArgs) Handles NumericUpDownBrightness.ValueChanged
        TrackBarBrightness.Value = NumericUpDownBrightness.Value
        ChangeBrightness()
    End Sub
    Private Sub TrackBarGrillBrightness_Scroll(sender As System.Object, e As System.EventArgs) Handles TrackBarGrillBrightness.Scroll
        NumericUpDownGrillBrightness.Value = TrackBarGrillBrightness.Value
    End Sub
    Private Sub NumericUpDownGrillBrightness_ValueChanged(sender As System.Object, e As System.EventArgs) Handles NumericUpDownGrillBrightness.ValueChanged
        TrackBarGrillBrightness.Value = NumericUpDownGrillBrightness.Value
        ChangeBrightness()
    End Sub

    Private Sub IgnoreGrill_CheckedChanged(sender As System.Object, e As System.EventArgs) Handles chkIgnoreGrill.CheckedChanged
        ChangeBrightness()
    End Sub

    Private Sub ChangeBrightness()
        If sourceimage Is Nothing OrElse PictureBoxPreview.Width <= 0 OrElse PictureBoxPreview.Height <= 0 Then Return
        Dim previous As Image = PictureBoxPreview.Image
        PictureBoxPreview.Image = CreateAdjustedImage(PictureBoxPreview.Size)
        If previous IsNot Nothing AndAlso Not Object.ReferenceEquals(previous, sourceimage) Then previous.Dispose()
    End Sub

    Private Function CreateAdjustedImage(ByVal targetSize As Size) As Bitmap
        Dim resized As Bitmap = If(targetSize = sourceimage.Size, sourceimage.Copy(), sourceimage.Resized(targetSize))
        If Not chkIgnoreGrill.Enabled Then
            resized.Filters.Brightness(CSng(NumericUpDownBrightness.Value / 100))
            Return resized
        End If

        Dim scaledGrillHeight As Integer = CInt(Math.Round(Backglass.currentData.GrillHeight * targetSize.Height / CDbl(sourceimage.Height)))
        scaledGrillHeight = Math.Max(1, Math.Min(targetSize.Height, scaledGrillHeight))
        Dim upperHeight As Integer = targetSize.Height - scaledGrillHeight
        Dim grillBrightness As Integer = CInt(NumericUpDownGrillBrightness.Value)
        If Not chkIgnoreGrill.Checked Then grillBrightness += CInt(NumericUpDownBrightness.Value)
        grillBrightness = Math.Max(-100, Math.Min(100, grillBrightness))

        Dim result As New Bitmap(targetSize.Width, targetSize.Height, Imaging.PixelFormat.Format32bppArgb)
        Using backglassimage As Bitmap = resized.PartFromImage(New Rectangle(0, 0, targetSize.Width, upperHeight)),
              grillimage As Bitmap = resized.PartFromImage(New Rectangle(0, upperHeight, targetSize.Width, scaledGrillHeight)),
              gr As Graphics = Graphics.FromImage(result)
            backglassimage.Filters.Brightness(CSng(NumericUpDownBrightness.Value / 100))
            grillimage.Filters.Brightness(CSng(grillBrightness / 100.0F))
            gr.PageUnit = GraphicsUnit.Pixel
            gr.DrawImageUnscaled(backglassimage, 0, 0)
            gr.DrawImageUnscaled(grillimage, 0, upperHeight)
        End Using
        resized.Dispose()
        Return result
    End Function

End Class
