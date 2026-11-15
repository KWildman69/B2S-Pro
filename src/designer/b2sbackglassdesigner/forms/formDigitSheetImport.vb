Imports System.Drawing.Imaging
Imports System.IO
Imports System.Windows.Forms

Public Class formDigitSheetImport
    Inherits Form

    Private sheet As Bitmap
    Private digits As Image()
    Private creditImages As Image()
    Private creditMode As Boolean
    Private ReadOnly creditMaximum As New NumericUpDown With {.Minimum = 1, .Maximum = 99, .Value = 25, .Width = 60}
    Private recognized As DigitRecognition.Glyph()
    Private pdfFile As String
    Private pdfPage As Integer = 1
    Private recognitionVersion As Integer
    Private ReadOnly recognizeButton As New Button With {.Text = "Recognize digits", .MinimumSize = New Size(145, 30)}
    Private ReadOnly digitPadding As New NumericUpDown With {.Minimum = 0, .Maximum = 200, .Value = 12, .Width = 60}
    Private ReadOnly confirmed As New CheckBox With {.Text = "I checked the ten digit previews", .AutoSize = True}
    Private ReadOnly sheetPreview As New PictureBox With {.Dock = DockStyle.Fill, .SizeMode = PictureBoxSizeMode.Zoom, .BackColor = Color.FromArgb(32, 32, 32)}
    Private ReadOnly digitPreview As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .AutoScroll = True}
    Private ReadOnly controlsRow As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .AutoScroll = True}
    Private ReadOnly columns As New NumericUpDown With {.Minimum = 1, .Maximum = 10, .Value = 10, .Width = 48}
    Private ReadOnly rows As New NumericUpDown With {.Minimum = 1, .Maximum = 10, .Value = 1, .Width = 48}
    Private ReadOnly leftMargin As New NumericUpDown With {.Maximum = 100000, .Width = 62}
    Private ReadOnly topMargin As New NumericUpDown With {.Maximum = 100000, .Width = 62}
    Private ReadOnly rightMargin As New NumericUpDown With {.Maximum = 100000, .Width = 62}
    Private ReadOnly bottomMargin As New NumericUpDown With {.Maximum = 100000, .Width = 62}
    Private ReadOnly gapX As New NumericUpDown With {.Maximum = 100000, .Width = 62}
    Private ReadOnly gapY As New NumericUpDown With {.Maximum = 100000, .Width = 62}
    Private ReadOnly order As New TextBox With {.Text = "0123456789", .Width = 112, .MaxLength = 10}
    Private ReadOnly nameBox As New TextBox With {.Text = "Custom digit sheet", .Width = 200, .MaxLength = 80}
    Private ReadOnly status As New Label With {.Dock = DockStyle.Fill, .Text = "Choose one sheet. Set its grid and crop borders, then check all ten digits before saving.", .AutoSize = False}
    Private ReadOnly saveButton As New Button With {.Text = "Save image set", .AutoSize = True, .MinimumSize = New Size(150, 30), .Enabled = False}

    Public Sub New()
        Text = "Import digit sheet — preview 0–9"
        ClientSize = New Size(950, 720)
        MinimumSize = New Size(850, 650)
        StartPosition = FormStartPosition.CenterParent
        Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 5, .Padding = New Padding(12)}
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 120))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 55))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 45))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 44))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 40))
        Dim choose As New Button With {.Text = "Choose digit sheet…", .AutoSize = True}
        AddHandler choose.Click, AddressOf ChooseSheet
        controlsRow.Controls.Add(choose)
        AddEditor("Set name", nameBox)
        AddEditor("Padding (pixels)", digitPadding)
        controlsRow.Controls.Add(recognizeButton)
        controlsRow.Controls.Add(confirmed)
        Dim hint As New Label With {.Text = "Recognize and extract each 0–9 from the sheet. Repeated columns are supported. Original artwork is retained; review OCR guesses before saving.", .AutoSize = True, .Margin = New Padding(4, 8, 4, 4)}
        controlsRow.Controls.Add(hint)
        layout.Controls.Add(controlsRow, 0, 0)
        layout.Controls.Add(sheetPreview, 0, 1)
        layout.Controls.Add(digitPreview, 0, 2)
        layout.Controls.Add(status, 0, 3)
        Dim buttons As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.RightToLeft}
        Dim cancel As New Button With {.Text = "Cancel", .AutoSize = True, .DialogResult = DialogResult.Cancel}
        buttons.Controls.Add(cancel)
        buttons.Controls.Add(saveButton)
        layout.Controls.Add(buttons, 0, 4)
        Controls.Add(layout)
        CancelButton = cancel
        AddHandler saveButton.Click, Sub()
                                         If digits IsNot Nothing Then DialogResult = DialogResult.OK
                                     End Sub
        For Each editor In New NumericUpDown() {columns, rows, leftMargin, topMargin, rightMargin, bottomMargin, gapX, gapY}
            AddHandler editor.ValueChanged, AddressOf RefreshPreview
        Next
        AddHandler order.TextChanged, AddressOf RefreshPreview
        AddHandler nameBox.TextChanged, AddressOf RefreshPreview
        AddHandler digitPadding.ValueChanged, AddressOf RefreshPreview
        AddHandler confirmed.CheckedChanged, AddressOf RefreshPreview
        AddHandler recognizeButton.Click, Sub() StartRecognition()
        AddHandler sheetPreview.Paint, AddressOf PaintCropGrid
    End Sub

    Private Sub AddEditor(caption As String, editor As Control)
        Dim panel As New FlowLayoutPanel With {.AutoSize = True, .WrapContents = False, .Margin = New Padding(4, 4, 8, 4)}
        panel.Controls.Add(New Label With {.Text = caption, .AutoSize = True, .Margin = New Padding(0, 5, 3, 0)})
        panel.Controls.Add(editor)
        controlsRow.Controls.Add(panel)
    End Sub

    Public Sub ConfigureCreditRange()
        creditMode = True
        confirmed.Text = "I checked the generated credit values"
        AddEditor("Highest credit value", creditMaximum)
        controlsRow.Controls.SetChildIndex(creditMaximum.Parent, 2)
        controlsRow.Controls.Add(New Label With {.AutoSize = True, .Text = "Generate 1 through your chosen maximum; zero is included for reset."})
        AddHandler creditMaximum.ValueChanged, Sub()
                                                   confirmed.Checked = False
                                                   RefreshPreview(Me, EventArgs.Empty)
                                               End Sub
    End Sub

    Public Shared Function CreateCreditImages(sourceDigits As Image(), maximum As Integer) As Image()
        If maximum < 1 OrElse maximum > 99 Then Throw New ArgumentException("Choose a highest credit value from 1 to 99. Credit reels use at most two digits.")
        If sourceDigits Is Nothing OrElse sourceDigits.Length <> 10 OrElse sourceDigits.Any(Function(image) image Is Nothing) Then Throw New ArgumentException("All ten digits 0–9 are required.")
        Dim size As Size = sourceDigits(0).Size
        If sourceDigits.Any(Function(image) image.Size <> size) Then Throw New ArgumentException("The digit frames must have the same size.")
        Dim places As Integer = 2
        Dim bytes = (CLng(maximum) + 1) * size.Width * places * size.Height * 4L
        If bytes > 256L * 1024 * 1024 Then Throw New ArgumentException("This range would need more than 256 MB of reel images. Use a lower-resolution digit sheet for this range.")
        Dim result(maximum) As Image
        Try
            For value As Integer = 0 To maximum
                Dim image As New Bitmap(size.Width * places, size.Height, PixelFormat.Format32bppArgb)
                result(value) = image
                Using graphics As Graphics = Graphics.FromImage(image)
                    graphics.Clear(Color.White)
                    Dim text = value.ToString(Globalization.CultureInfo.InvariantCulture)
                    Dim x As Integer = (image.Width - text.Length * size.Width) \ 2
                    For Each digit As Char In text
                        graphics.DrawImage(sourceDigits(AscW(digit) - AscW("0"c)), New Rectangle(x, 0, size.Width, size.Height), 0, 0, size.Width, size.Height, GraphicsUnit.Pixel)
                        x += size.Width
                    Next
                End Using
            Next
            Return result
        Catch
            For Each image In result
                If image IsNot Nothing Then image.Dispose()
            Next
            Throw
        End Try
    End Function

    Private Sub ChooseSheet(sender As Object, e As EventArgs)
        Using dialog As New OpenFileDialog With {.Filter = "Digit sheets (PDF or image)|*.pdf;*.png;*.bmp;*.jpg;*.jpeg;*.gif|PDF|*.pdf|Images|*.png;*.bmp;*.jpg;*.jpeg;*.gif", .Title = "Choose one sheet containing digits 0 through 9"}
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
            Try
                If Path.GetExtension(dialog.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase) Then
                    Dim selectedPage As Integer = 1
                    Using rendered = ChoosePdfPage(Me, dialog.FileName, selectedPage)
                        If rendered IsNot Nothing Then
                            pdfFile = dialog.FileName
                            pdfPage = selectedPage
                            SetSheet(New Bitmap(rendered))
                        End If
                    End Using
                Else
                    LoadSheet(dialog.FileName)
                End If
            Catch ex As Exception
                MessageBox.Show(Me, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End Using
    End Sub

    Public Sub LoadSheet(path As String)
        pdfFile = Nothing
        Dim nextSheet As Bitmap
        Using source As Image = Image.FromFile(path)
            nextSheet = New Bitmap(source)
        End Using
        SetSheet(nextSheet)
    End Sub

    Private Sub SetSheet(nextSheet As Bitmap)
        sheetPreview.Image = Nothing
        If sheet IsNot Nothing Then sheet.Dispose()
        sheet = nextSheet
        sheetPreview.Image = sheet
        recognized = Nothing
        confirmed.Checked = False
        StartRecognition()
    End Sub

    Private Async Sub StartRecognition()
        If sheet Is Nothing Then Return
        recognitionVersion += 1
        Dim version = recognitionVersion
        Dim snapshot As New Bitmap(sheet)
        Dim sourcePdf = pdfFile
        Dim sourcePage = pdfPage
        recognized = Nothing
        ClearDigits()
        confirmed.Checked = False
        saveButton.Enabled = False
        recognizeButton.Enabled = False
        status.Text = "Recognizing the actual digits and their positions… Nothing has been saved."
        Try
            Dim result = Await Threading.Tasks.Task.Run(Function()
                                                           Using snapshot
                                                               Return DigitRecognition.Recognize(snapshot, sourcePdf, sourcePage)
                                                           End Using
                                                       End Function)
            If IsDisposed OrElse version <> recognitionVersion Then Return
            recognized = result
            RefreshPreview(Me, EventArgs.Empty)
        Catch ex As Exception
            If Not IsDisposed AndAlso version = recognitionVersion Then status.Text = ex.Message
        Finally
            If Not IsDisposed AndAlso version = recognitionVersion Then recognizeButton.Enabled = True
        End Try
    End Sub

    Private Sub ClearDigits()
        If creditImages IsNot Nothing Then
            For Each image In creditImages
                image.Dispose()
            Next
            creditImages = Nothing
        End If
        For Each panel As Control In digitPreview.Controls
            For Each picture In panel.Controls.OfType(Of PictureBox)()
                picture.Image = Nothing
            Next
        Next
        While digitPreview.Controls.Count > 0
            Dim child = digitPreview.Controls(0)
            digitPreview.Controls.Remove(child)
            child.Dispose()
        End While
        If digits IsNot Nothing Then
            For Each image In digits
                image.Dispose()
            Next
            digits = Nothing
        End If
    End Sub

    Private Sub RefreshPreview(sender As Object, e As EventArgs)
        ClearDigits()
        saveButton.Enabled = False
        If sheet Is Nothing OrElse recognized Is Nothing Then Return
        Try
            ValidateImageSetName(nameBox.Text)
            digits = DigitRecognition.Extract(sheet, recognized, CInt(digitPadding.Value))
            If creditMode Then creditImages = CreateCreditImages(digits, CInt(creditMaximum.Value))
            Dim previewImages = If(creditMode, creditImages, digits)
            For i As Integer = 0 To previewImages.Length - 1
                Dim panel As New Panel With {.Size = New Size(155, 170), .Margin = New Padding(5)}
                panel.Controls.Add(New PictureBox With {.Image = previewImages(i), .SizeMode = PictureBoxSizeMode.Zoom, .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(55, 55, 55)})
                Dim uncertain = If(creditMode, recognized.Any(Function(glyph) glyph.review OrElse glyph.confidence < 70), recognized(i).review OrElse recognized(i).confidence < 70)
                panel.Controls.Add(New Label With {.Text = If(creditMode, "Credit ", "Digit ") & i.ToString() & If(uncertain, " — check OCR guess", ""), .Dock = DockStyle.Bottom, .Height = 22, .ForeColor = If(uncertain, Color.Orange, ForeColor), .TextAlign = ContentAlignment.MiddleCenter})
                digitPreview.Controls.Add(panel)
            Next
            status.Text = String.Format("Identified 0–9 and extracted the original artwork, each {0} × {1} pixels. Check every preview and tick the confirmation to save. Cancel saves nothing.", digits(0).Width, digits(0).Height)
            If creditMode Then status.Text = "Generated credit values 1–" & creditMaximum.Value.ToString() & ", plus zero for reset, from the recognized digits. Check the complete set before saving. Cancel saves nothing."
            saveButton.Enabled = confirmed.Checked
        Catch ex As Exception
            status.Text = ex.Message
        End Try
        sheetPreview.Invalidate()
    End Sub

    Private Sub PaintCropGrid(sender As Object, e As PaintEventArgs)
        If sheet Is Nothing OrElse digits Is Nothing Then Return
        Dim scale As Double = Math.Min(sheetPreview.ClientSize.Width / CDbl(sheet.Width), sheetPreview.ClientSize.Height / CDbl(sheet.Height))
        Dim offsetX = (sheetPreview.ClientSize.Width - sheet.Width * scale) / 2
        Dim offsetY = (sheetPreview.ClientSize.Height - sheet.Height * scale) / 2
        Using pen As New Pen(Color.Cyan, 2), brush As New SolidBrush(Color.Cyan)
            Dim bounds = recognized.Select(Function(g) g.Bounds).ToArray()
            For i As Integer = 0 To 9
                Dim r As New RectangleF(CSng(offsetX + bounds(i).X * scale), CSng(offsetY + bounds(i).Y * scale), CSng(bounds(i).Width * scale), CSng(bounds(i).Height * scale))
                e.Graphics.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height)
                e.Graphics.DrawString(i.ToString(), Font, brush, r.Location)
            Next
        End Using
    End Sub

    Public Function TakeImages() As Image()
        If DialogResult <> DialogResult.OK OrElse digits Is Nothing Then Throw New InvalidOperationException("Preview and save the set first.")
        Dim result = If(creditMode, creditImages, digits)
        If creditMode Then
            creditImages = Nothing
        Else
            digits = Nothing
        End If
        Return result
    End Function

    Public ReadOnly Property SetName As String
        Get
            Return nameBox.Text.Trim()
        End Get
    End Property

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            ClearDigits()
            sheetPreview.Image = Nothing
            If sheet IsNot Nothing Then sheet.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class
