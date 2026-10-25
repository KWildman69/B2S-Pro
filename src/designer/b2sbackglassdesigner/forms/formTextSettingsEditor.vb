Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Windows.Forms

Public Class formTextSettingsEditor
    Inherits B2SThemedForm

    Private ReadOnly bulb As Illumination.BulbInfo
    Private ReadOnly selectedBulbs As New List(Of Illumination.BulbInfo)()
    Private ReadOnly additionalOriginals As New Dictionary(Of Illumination.BulbInfo, Illumination.BulbInfo)()
    Private ReadOnly originalText As String
    Private ReadOnly originalFontName As String
    Private ReadOnly originalFontSize As Single
    Private ReadOnly originalFontStyle As FontStyle
    Private ReadOnly originalAlignment As Illumination.eTextAlignment
    Private ReadOnly originalDodgeColor As Color
    Private ReadOnly originalGlowIntensity As Integer
    Private ReadOnly originalLightTemperature As Integer
    Private ReadOnly originalSelectionMask As String
    Private ReadOnly originalSelectionTolerance As Integer
    Private ReadOnly originalSelectionFeather As Integer

    Private ReadOnly textValue As TextBox
    Private ReadOnly fontButton As Button
    Private ReadOnly alignLeft As RadioButton
    Private ReadOnly alignCenter As RadioButton
    Private ReadOnly alignRight As RadioButton
    Private ReadOnly dodgeColor As ComboBox
    Private ReadOnly brightness As TrackBar
    Private ReadOnly temperature As TrackBar
    Private ReadOnly preview As PictureBox
    Private ReadOnly previewStatus As Label
    Private ReadOnly previewTimer As Timer
    Private previewSource As Bitmap
    Private previewZoomPercent As Integer = 0
    Private previewZoomLabel As Label
    Private previewPanOffset As Point = Point.Empty
    Private previewIsPanning As Boolean
    Private previewPanStart As Point
    Private previewPanStartOffset As Point
    Private initializing As Boolean = True

    Public Sub New(ByVal selected As Illumination.BulbInfo, Optional ByVal multiSelection As IEnumerable(Of Illumination.BulbInfo) = Nothing)
        If selected Is Nothing Then Throw New ArgumentNullException("selected")
        bulb = selected
        If multiSelection IsNot Nothing Then
            For Each item As Illumination.BulbInfo In multiSelection
                If item IsNot Nothing AndAlso Not item.IsImageSnippit AndAlso Not String.IsNullOrEmpty(item.Text) AndAlso Not selectedBulbs.Contains(item) Then selectedBulbs.Add(item)
            Next
        End If
        If Not selectedBulbs.Contains(bulb) Then selectedBulbs.Add(bulb)
        For Each item As Illumination.BulbInfo In selectedBulbs
            If Not Object.ReferenceEquals(item, bulb) Then
                Dim snapshot As New Illumination.BulbInfo()
                CopyTextAppearance(item, snapshot)
                additionalOriginals.Add(item, snapshot)
            End If
        Next
        originalText = bulb.Text
        originalFontName = bulb.FontName
        originalFontSize = bulb.FontSize
        originalFontStyle = bulb.FontStyle
        originalAlignment = bulb.TextAlignment
        originalDodgeColor = bulb.DodgeColor
        originalGlowIntensity = bulb.GlowIntensity
        originalLightTemperature = bulb.LightTemperature
        originalSelectionMask = bulb.SelectionMaskData
        originalSelectionTolerance = bulb.SelectionTolerance
        originalSelectionFeather = bulb.SelectionFeather

        WindowStateManager.Attach(Me)
        Text = "Text Box"
        Name = "formTextSettingsEditor"
        StartPosition = FormStartPosition.CenterParent
        FormBorderStyle = FormBorderStyle.Sizable
        ClientSize = New Size(1180, 735)
        MinimumSize = New Size(1000, 650)
        Font = New Font("Tahoma", 9.0F)
        BackColor = Color.FromArgb(18, 20, 22)
        ForeColor = Color.WhiteSmoke
        DoubleBuffered = True

        Dim accent As Color = Color.FromArgb(255, 153, 0)
        Dim panelColor As Color = Color.FromArgb(27, 29, 31)


        Dim description As New Label With {
            .Text = "Edit illuminated text and preview the saved DirectB2S result.",
            .Left = 26, .Top = 57, .Width = 440, .Height = 24, .ForeColor = Color.Gainsboro
        }

        Controls.Add(description)

        Dim settings As New GroupBox With {
            .Text = "  TEXT SETTINGS", .Left = 20, .Top = 92, .Width = 420, .Height = 545,
            .ForeColor = accent, .BackColor = panelColor, .Font = New Font("Tahoma", 9.0F, FontStyle.Bold),
            .Anchor = AnchorStyles.Top Or AnchorStyles.Bottom Or AnchorStyles.Left
        }
        Controls.Add(settings)

        settings.Controls.Add(New Label With {.Text = "Illuminated Text", .Left = 18, .Top = 34, .Width = 145, .ForeColor = Color.WhiteSmoke, .Font = New Font("Tahoma", 9.0F)})
        textValue = New TextBox With {
            .Left = 18, .Top = 56, .Width = 382, .Height = 70, .Multiline = True, .AcceptsReturn = True,
            .Text = bulb.Text, .BackColor = Color.FromArgb(38, 40, 43), .ForeColor = Color.WhiteSmoke
        }
        settings.Controls.Add(textValue)

        fontButton = New Button With {.Text = FontCaption(), .Left = 18, .Top = 139, .Width = 382, .Height = 31, .FlatStyle = FlatStyle.Flat}
        StyleDarkButton(fontButton)
        settings.Controls.Add(fontButton)

        settings.Controls.Add(New Label With {.Text = "Alignment", .Left = 18, .Top = 184, .Width = 90, .ForeColor = Color.WhiteSmoke, .Font = New Font("Tahoma", 9.0F)})
        alignLeft = New RadioButton With {.Text = "Left", .Left = 112, .Top = 180, .Width = 70, .Height = 25, .ForeColor = Color.WhiteSmoke}
        alignCenter = New RadioButton With {.Text = "Center", .Left = 190, .Top = 180, .Width = 78, .Height = 25, .ForeColor = Color.WhiteSmoke}
        alignRight = New RadioButton With {.Text = "Right", .Left = 276, .Top = 180, .Width = 72, .Height = 25, .ForeColor = Color.WhiteSmoke}
        Select Case bulb.TextAlignment
            Case Illumination.eTextAlignment.Left : alignLeft.Checked = True
            Case Illumination.eTextAlignment.Right : alignRight.Checked = True
            Case Else : alignCenter.Checked = True
        End Select
        settings.Controls.AddRange(New Control() {alignLeft, alignCenter, alignRight})

        settings.Controls.Add(New Label With {.Text = "Dodge Color", .Left = 18, .Top = 225, .Width = 145, .ForeColor = Color.WhiteSmoke, .Font = New Font("Tahoma", 9.0F)})
        dodgeColor = New ComboBox With {
            .Left = 160, .Top = 219, .Width = 240, .Height = 25,
            .DropDownStyle = ComboBoxStyle.DropDownList, .BackColor = Color.FromArgb(38, 40, 43), .ForeColor = Color.WhiteSmoke
        }
        dodgeColor.Items.AddRange(New Object() {"None (Off)", "Red", "Green", "Blue", "Yellow", "Magenta", "Purple", "White"})
        dodgeColor.SelectedIndex = Math.Max(0, Math.Min(7, TranslateDodgeColor2Index(bulb.DodgeColor)))
        settings.Controls.Add(dodgeColor)

        brightness = AddSlider(settings, "Brightness", 270, bulb.GlowIntensity, 0, 1600, "%")
        temperature = AddKelvinSlider(settings, "Light Temperature", 320, bulb.LightTemperature)
        settings.Controls.Add(New Label With {
            .Text = "Brightness range: 0% – 1600%   •   800% is the strong midpoint",
            .Left = 18, .Top = 368, .Width = 382, .Height = 34,
            .ForeColor = Color.Silver, .Font = New Font("Tahoma", 8.25F)
        })

        Dim quickSelection As New Button With {.Text = "Quick Selection...", .Left = 18, .Top = 420, .Width = 170, .Height = 31, .FlatStyle = FlatStyle.Flat}
        StyleDarkButton(quickSelection)
        settings.Controls.Add(quickSelection)
        Dim maskNote As New Label With {
            .Text = "Optional: limit the text light to a mask on the canvas.",
            .Left = 18, .Top = 462, .Width = 382, .Height = 44,
            .ForeColor = Color.Silver, .Font = New Font("Tahoma", 8.25F)
        }
        settings.Controls.Add(maskNote)

        Dim previewGroup As New GroupBox With {
            .Text = "  LIVE TEXT PREVIEW", .Left = 455, .Top = 92, .Width = 705, .Height = 545,
            .ForeColor = Color.FromArgb(42, 225, 105), .BackColor = panelColor,
            .Font = New Font("Tahoma", 9.0F, FontStyle.Bold),
            .Anchor = AnchorStyles.Top Or AnchorStyles.Bottom Or AnchorStyles.Left Or AnchorStyles.Right
        }
        Controls.Add(previewGroup)
        preview = New PictureBox With {
            .Left = 14, .Top = 29, .Width = 677, .Height = 415,
            .BackColor = Color.Black, .BorderStyle = BorderStyle.FixedSingle,
            .SizeMode = PictureBoxSizeMode.Normal, .TabStop = True,
            .Anchor = AnchorStyles.Top Or AnchorStyles.Left
        }
        Dim zoomOutButton As New Button With {.Text = "−", .Left = 190, .Top = 452, .Width = 42, .Height = 29, .FlatStyle = FlatStyle.Flat, .Anchor = AnchorStyles.Bottom Or AnchorStyles.Left}
        Dim zoomInButton As New Button With {.Text = "+", .Left = 237, .Top = 452, .Width = 42, .Height = 29, .FlatStyle = FlatStyle.Flat, .Anchor = AnchorStyles.Bottom Or AnchorStyles.Left}
        Dim fitButton As New Button With {.Text = "Fit", .Left = 284, .Top = 452, .Width = 58, .Height = 29, .FlatStyle = FlatStyle.Flat, .Anchor = AnchorStyles.Bottom Or AnchorStyles.Left}
        Dim actualButton As New Button With {.Text = "100%", .Left = 347, .Top = 452, .Width = 66, .Height = 29, .FlatStyle = FlatStyle.Flat, .Anchor = AnchorStyles.Bottom Or AnchorStyles.Left}
        StyleDarkButton(zoomOutButton)
        StyleDarkButton(zoomInButton)
        StyleDarkButton(fitButton)
        StyleDarkButton(actualButton)
        previewZoomLabel = New Label With {
            .Text = "Fit", .Left = 422, .Top = 457, .Width = 62, .Height = 22,
            .TextAlign = ContentAlignment.MiddleCenter, .ForeColor = accent,
            .Font = New Font("Tahoma", 8.25F, FontStyle.Bold),
            .Anchor = AnchorStyles.Bottom Or AnchorStyles.Left
        }
        previewStatus = New Label With {
            .Text = "Updating preview...", .Left = 14, .Top = 492, .Width = 677, .Height = 32,
            .ForeColor = Color.Gainsboro, .TextAlign = ContentAlignment.MiddleCenter,
            .Anchor = AnchorStyles.Bottom Or AnchorStyles.Left Or AnchorStyles.Right
        }
        previewGroup.Controls.Add(preview)
        previewGroup.Controls.AddRange(New Control() {zoomOutButton, zoomInButton, fitButton, actualButton, previewZoomLabel})
        previewGroup.Controls.Add(previewStatus)

        Dim saveButton As New Button With {
            .Text = "✓  Save & Close", .Left = 866, .Top = 661, .Width = 146, .Height = 42,
            .DialogResult = DialogResult.OK, .FlatStyle = FlatStyle.Flat,
            .BackColor = accent, .ForeColor = Color.Black,
            .Font = New Font("Tahoma", 10.0F, FontStyle.Bold),
            .Anchor = AnchorStyles.Bottom Or AnchorStyles.Right
        }
        Dim cancelButton As New Button With {
            .Text = "Cancel", .Left = 1022, .Top = 661, .Width = 138, .Height = 42,
            .DialogResult = DialogResult.Cancel, .FlatStyle = FlatStyle.Flat,
            .Anchor = AnchorStyles.Bottom Or AnchorStyles.Right
        }
        StyleDarkButton(cancelButton)
        Controls.Add(saveButton)
        Controls.Add(cancelButton)
        AcceptButton = saveButton
        CancelButton = cancelButton

        previewTimer = New Timer With {.Interval = 90}
        AddHandler previewTimer.Tick, AddressOf PreviewTimer_Tick
        AddHandler textValue.TextChanged, AddressOf TextControlChanged
        AddHandler fontButton.Click, AddressOf ChooseFont_Click
        AddHandler alignLeft.CheckedChanged, AddressOf AlignmentChanged
        AddHandler alignCenter.CheckedChanged, AddressOf AlignmentChanged
        AddHandler alignRight.CheckedChanged, AddressOf AlignmentChanged
        AddHandler dodgeColor.SelectedIndexChanged, AddressOf TextControlChanged
        AddHandler brightness.ValueChanged, AddressOf TextControlChanged
        AddHandler temperature.ValueChanged, AddressOf TextControlChanged
        AddHandler quickSelection.Click, AddressOf QuickSelection_Click
        AddHandler zoomOutButton.Click, AddressOf PreviewZoomOut_Click
        AddHandler zoomInButton.Click, AddressOf PreviewZoomIn_Click
        AddHandler fitButton.Click, AddressOf PreviewFit_Click
        AddHandler actualButton.Click, AddressOf PreviewActualSize_Click
        AddHandler preview.MouseWheel, AddressOf Preview_MouseWheel
        AddHandler preview.MouseDown, AddressOf Preview_MouseDown
        AddHandler preview.MouseMove, AddressOf Preview_MouseMove
        AddHandler preview.MouseUp, AddressOf Preview_MouseUp
        AddHandler preview.MouseLeave, AddressOf Preview_MouseLeave
        AddHandler preview.MouseEnter, AddressOf Preview_MouseEnter
        AddHandler preview.DoubleClick, AddressOf Preview_DoubleClick
        Dim resizePreview As EventHandler =
            Sub(sender As Object, e As EventArgs)
                preview.Width = Math.Max(300, previewGroup.ClientSize.Width - 28)
                preview.Height = Math.Max(180, previewGroup.ClientSize.Height - 130)
                RefreshRenderedPreview()
            End Sub
        AddHandler Me.Resize, resizePreview
        AddHandler Me.Shown, resizePreview
        AddHandler previewGroup.Resize, resizePreview

        AppThemeManager.ApplyB2SProDialog(Me)
        resizePreview(Me, EventArgs.Empty)
        initializing = False
        QueuePreview()
    End Sub

    Private Function AddSlider(ByVal parent As Control, ByVal caption As String, ByVal top As Integer,
                               ByVal initialValue As Integer, ByVal minimum As Integer,
                               ByVal maximum As Integer, ByVal suffix As String) As TrackBar
        parent.Controls.Add(New Label With {.Text = caption, .Left = 18, .Top = top + 8, .Width = 135, .ForeColor = Color.WhiteSmoke, .Font = New Font("Tahoma", 9.0F)})
        Dim number As New NumericUpDown With {.Left = 330, .Top = top + 4, .Width = 58, .BackColor = Color.FromArgb(38, 40, 43), .ForeColor = Color.WhiteSmoke}
        Dim slider As New TrackBar With {
            .Left = 150, .Top = top, .Width = 175, .Height = 38, .AutoSize = False,
            .Minimum = minimum, .Maximum = maximum, .TickFrequency = Math.Max(1, (maximum - minimum) \ 10),
            .Value = Math.Max(minimum, Math.Min(maximum, initialValue)), .BackColor = parent.BackColor
        }
        TrackBarNumericLink.Bind(slider, number)
        parent.Controls.Add(slider)
        parent.Controls.Add(number)
        parent.Controls.Add(New Label With {.Text = suffix, .Left = 391, .Top = top + 8, .Width = 22, .ForeColor = Color.WhiteSmoke, .Font = New Font("Tahoma", 9.0F)})
        Return slider
    End Function

    Private Function AddKelvinSlider(ByVal parent As Control, ByVal caption As String,
                                     ByVal top As Integer, ByVal initialValue As Integer) As TrackBar
        parent.Controls.Add(New Label With {.Text = caption, .Left = 18, .Top = top + 8, .Width = 135, .ForeColor = Color.WhiteSmoke, .Font = New Font("Tahoma", 9.0F)})
        Dim number As New NumericUpDown With {.Left = 324, .Top = top + 4, .Width = 66, .BackColor = Color.FromArgb(38, 40, 43), .ForeColor = Color.WhiteSmoke}
        Dim slider As New TrackBar With {
            .Left = 150, .Top = top, .Width = 169, .Height = 38, .AutoSize = False,
            .Minimum = 2000, .Maximum = 6500, .SmallChange = 100, .LargeChange = 500,
            .TickFrequency = 500, .Value = Math.Max(2000, Math.Min(6500, If(initialValue <= 0, 4000, initialValue))),
            .BackColor = parent.BackColor
        }
        TrackBarNumericLink.Bind(slider, number, 100)
        parent.Controls.Add(slider)
        parent.Controls.Add(number)
        parent.Controls.Add(New Label With {.Text = "K", .Left = 394, .Top = top + 8, .Width = 18, .ForeColor = Color.WhiteSmoke, .Font = New Font("Tahoma", 9.0F)})
        Return slider
    End Function

    Private Sub StyleDarkButton(ByVal button As Button)
        button.BackColor = Color.FromArgb(38, 40, 43)
        button.ForeColor = Color.WhiteSmoke
        button.FlatAppearance.BorderColor = Color.FromArgb(78, 80, 84)
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(52, 54, 57)
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(64, 66, 70)
    End Sub

    Private Function FontCaption() As String
        Dim name As String = If(String.IsNullOrWhiteSpace(bulb.FontName), "Arial", bulb.FontName)
        Return name & ", Size=" & Math.Max(1.0F, bulb.FontSize).ToString("0.##")
    End Function

    Private Sub ChooseFont_Click(ByVal sender As Object, ByVal e As EventArgs)
        Dim currentFont As Font = Nothing
        Try
            currentFont = New Font(If(String.IsNullOrWhiteSpace(bulb.FontName), "Arial", bulb.FontName),
                                   Math.Max(1.0F, bulb.FontSize), bulb.FontStyle, GraphicsUnit.Point)
            Using selectedFont As Font = formToolIllumination.ShowB2SFontDialog(currentFont, Me, bulb.Text)
                If selectedFont Is Nothing Then Return
                bulb.FontName = selectedFont.Name
                bulb.FontSize = selectedFont.SizeInPoints
                bulb.FontStyle = selectedFont.Style
                For Each item As Illumination.BulbInfo In selectedBulbs
                    item.FontName = bulb.FontName
                    item.FontSize = bulb.FontSize
                    item.FontStyle = bulb.FontStyle
                Next
                fontButton.Text = FontCaption()
                MarkChanged()
            End Using
        Finally
            If currentFont IsNot Nothing Then currentFont.Dispose()
        End Try
    End Sub

    Private Sub AlignmentChanged(ByVal sender As Object, ByVal e As EventArgs)
        If initializing OrElse Not DirectCast(sender, RadioButton).Checked Then Return
        If alignLeft.Checked Then
            bulb.TextAlignment = Illumination.eTextAlignment.Left
        ElseIf alignRight.Checked Then
            bulb.TextAlignment = Illumination.eTextAlignment.Right
        Else
            bulb.TextAlignment = Illumination.eTextAlignment.Center
        End If
        For Each item As Illumination.BulbInfo In selectedBulbs
            item.TextAlignment = bulb.TextAlignment
        Next
        MarkChanged()
    End Sub

    Private Sub TextControlChanged(ByVal sender As Object, ByVal e As EventArgs)
        If initializing Then Return
        For Each item As Illumination.BulbInfo In selectedBulbs
            If Object.ReferenceEquals(sender, textValue) Then item.Text = textValue.Text
            If Object.ReferenceEquals(sender, dodgeColor) Then item.DodgeColor = TranslateIndex2DodgeColor(dodgeColor.SelectedIndex)
            If Object.ReferenceEquals(sender, brightness) Then item.GlowIntensity = brightness.Value
            If Object.ReferenceEquals(sender, temperature) Then item.LightTemperature = temperature.Value
        Next
        MarkChanged()
    End Sub

    Private Sub QuickSelection_Click(ByVal sender As Object, ByVal e As EventArgs)
        If Backglass.currentTabPage Is Nothing OrElse Backglass.currentTabPage.BackglassData Is Nothing Then Return
        Dim data = Backglass.currentTabPage.BackglassData
        Dim background As Image = If(data.IsDMDImageShown, data.DMDImage, data.Image)
        If background Is Nothing Then Return
        Using selector As New formQuickSelection(bulb, background)
            If selector.ShowDialog(Me) = DialogResult.OK Then MarkChanged()
        End Using
    End Sub

    Private Sub MarkChanged()
        For Each item As Illumination.BulbInfo In selectedBulbs
            item.IsIlluminatedImageDirty = True
        Next
        If Backglass.currentData IsNot Nothing Then Backglass.currentData.IsDirty = True
        If Backglass.currentTabPage IsNot Nothing Then
            Backglass.currentTabPage.RefreshEditorLighting()
            Backglass.currentTabPage.Invalidate()
        End If
        QueuePreview()
    End Sub

    Private Sub QueuePreview()
        If previewTimer Is Nothing Then Return
        previewStatus.Text = "Updating preview..."
        previewTimer.Stop()
        previewTimer.Start()
    End Sub

    Private Sub PreviewTimer_Tick(ByVal sender As Object, ByVal e As EventArgs)
        previewTimer.Stop()
        RefreshPreview()
    End Sub

    Private Sub RefreshPreview()
        If Backglass.currentTabPage Is Nothing Then Return
        Dim canvas As B2SPictureBox = Backglass.currentTabPage.CurrentPictureBox
        If canvas Is Nothing OrElse canvas.Image Is Nothing Then
            SetPreviewSource(Nothing)
            previewStatus.Text = "No backglass image is available for preview."
            Return
        End If

        Dim excluded As New Generic.List(Of Illumination.BulbInfo)()
        Dim data = Backglass.currentTabPage.BackglassData
        Dim items As IEnumerable(Of Illumination.BulbInfo) = If(data.IsDMDImageShown, data.DMDBulbs, data.Bulbs)
        For Each item As Illumination.BulbInfo In items
            If Not selectedBulbs.Contains(item) AndAlso Not item.IsImageSnippit Then excluded.Add(item)
        Next

        Dim rendered As Bitmap = canvas.CreateLayeredPreviewImageForBulbs(excluded, selectedBulbs)
        SetPreviewSource(rendered)
        previewStatus.Text = "Live preview — selected text boxes are on; other lights are off."
    End Sub

    Private Sub SetPreviewSource(ByVal image As Bitmap)
        If previewSource IsNot Nothing AndAlso Not Object.ReferenceEquals(previewSource, image) Then previewSource.Dispose()
        previewSource = image
        RefreshRenderedPreview()
    End Sub

    Private Sub RefreshRenderedPreview()
        Dim rendered As Bitmap = RenderPreviewCanvas(previewSource, preview.ClientSize)
        Dim old As Image = preview.Image
        preview.Image = rendered
        If old IsNot Nothing AndAlso Not Object.ReferenceEquals(old, rendered) Then old.Dispose()
        If previewZoomLabel IsNot Nothing Then previewZoomLabel.Text = If(previewZoomPercent <= 0, "Fit", previewZoomPercent.ToString() & "%")
    End Sub

    Private Function RenderPreviewCanvas(ByVal source As Image, ByVal canvasSize As Size) As Bitmap
        If source Is Nothing OrElse canvasSize.Width <= 0 OrElse canvasSize.Height <= 0 Then Return Nothing
        Dim scale As Double = If(previewZoomPercent <= 0,
                                 Math.Min(canvasSize.Width / CDbl(source.Width), canvasSize.Height / CDbl(source.Height)),
                                 previewZoomPercent / 100.0)
        scale = Math.Max(0.05, Math.Min(8.0, scale))
        Dim drawWidth As Integer = Math.Max(1, CInt(Math.Round(source.Width * scale)))
        Dim drawHeight As Integer = Math.Max(1, CInt(Math.Round(source.Height * scale)))
        Dim output As New Bitmap(canvasSize.Width, canvasSize.Height, Imaging.PixelFormat.Format32bppArgb)
        Using graphics As Graphics = Graphics.FromImage(output)
            graphics.Clear(Color.FromArgb(11, 12, 13))
            graphics.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
            graphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality
            graphics.CompositingQuality = Drawing2D.CompositingQuality.HighQuality
            Dim left As Integer = (canvasSize.Width - drawWidth) \ 2 + previewPanOffset.X
            Dim top As Integer = (canvasSize.Height - drawHeight) \ 2 + previewPanOffset.Y
            graphics.DrawImage(source, New Rectangle(left, top, drawWidth, drawHeight))
        End Using
        Return output
    End Function

    Private Sub PreviewZoomOut_Click(ByVal sender As Object, ByVal e As EventArgs)
        If previewZoomPercent <= 0 Then previewZoomPercent = GetFitZoomPercent()
        previewZoomPercent = Math.Max(10, previewZoomPercent - 25)
        RefreshRenderedPreview()
    End Sub

    Private Sub PreviewZoomIn_Click(ByVal sender As Object, ByVal e As EventArgs)
        If previewZoomPercent <= 0 Then previewZoomPercent = GetFitZoomPercent()
        previewZoomPercent = Math.Min(800, previewZoomPercent + 25)
        RefreshRenderedPreview()
    End Sub

    Private Sub PreviewFit_Click(ByVal sender As Object, ByVal e As EventArgs)
        previewZoomPercent = 0
        previewPanOffset = Point.Empty
        RefreshRenderedPreview()
    End Sub

    Private Sub PreviewActualSize_Click(ByVal sender As Object, ByVal e As EventArgs)
        previewZoomPercent = 100
        previewPanOffset = Point.Empty
        RefreshRenderedPreview()
    End Sub

    Private Sub Preview_MouseWheel(ByVal sender As Object, ByVal e As MouseEventArgs)
        If previewSource Is Nothing Then Return
        ' Ctrl+wheel belongs to EditorWindowZoom. Do not also turn the live preview
        ' from responsive Fit mode into a fixed percentage during whole-window zoom.
        If (Control.ModifierKeys And Keys.Control) <> 0 Then Return
        Dim oldZoom As Integer = If(previewZoomPercent <= 0, GetFitZoomPercent(), previewZoomPercent)
        Dim newZoom As Integer = If(e.Delta > 0, Math.Min(800, oldZoom + 25), Math.Max(10, oldZoom - 25))
        If newZoom = oldZoom Then Return
        Dim center As New Point(preview.ClientSize.Width \ 2, preview.ClientSize.Height \ 2)
        Dim relativeX As Double = e.X - center.X - previewPanOffset.X
        Dim relativeY As Double = e.Y - center.Y - previewPanOffset.Y
        Dim ratio As Double = newZoom / CDbl(oldZoom)
        previewPanOffset = New Point(CInt(Math.Round(e.X - center.X - relativeX * ratio)),
                                     CInt(Math.Round(e.Y - center.Y - relativeY * ratio)))
        previewZoomPercent = newZoom
        RefreshRenderedPreview()
    End Sub

    Private Sub Preview_MouseDown(ByVal sender As Object, ByVal e As MouseEventArgs)
        If e.Button <> MouseButtons.Left OrElse previewSource Is Nothing Then Return
        previewIsPanning = True
        previewPanStart = e.Location
        previewPanStartOffset = previewPanOffset
        preview.Cursor = Cursors.Hand
        preview.Capture = True
    End Sub

    Private Sub Preview_MouseMove(ByVal sender As Object, ByVal e As MouseEventArgs)
        If Not previewIsPanning Then Return
        previewPanOffset = New Point(previewPanStartOffset.X + e.X - previewPanStart.X,
                                     previewPanStartOffset.Y + e.Y - previewPanStart.Y)
        RefreshRenderedPreview()
    End Sub

    Private Sub Preview_MouseUp(ByVal sender As Object, ByVal e As MouseEventArgs)
        If e.Button = MouseButtons.Left Then EndPreviewPan()
    End Sub

    Private Sub Preview_MouseLeave(ByVal sender As Object, ByVal e As EventArgs)
        If previewIsPanning AndAlso Control.MouseButtons = MouseButtons.None Then EndPreviewPan()
    End Sub

    Private Sub Preview_MouseEnter(ByVal sender As Object, ByVal e As EventArgs)
        preview.Focus()
    End Sub

    Private Sub Preview_DoubleClick(ByVal sender As Object, ByVal e As EventArgs)
        PreviewFit_Click(sender, e)
    End Sub

    Private Sub EndPreviewPan()
        previewIsPanning = False
        preview.Capture = False
        preview.Cursor = Cursors.Default
    End Sub

    Private Function GetFitZoomPercent() As Integer
        If previewSource Is Nothing OrElse preview.ClientSize.Width <= 0 OrElse preview.ClientSize.Height <= 0 Then Return 100
        Dim scale As Double = Math.Min(preview.ClientSize.Width / CDbl(previewSource.Width), preview.ClientSize.Height / CDbl(previewSource.Height))
        Return Math.Max(10, Math.Min(800, CInt(Math.Round(scale * 100.0))))
    End Function

    Protected Overrides Sub OnFormClosing(ByVal e As FormClosingEventArgs)
        If previewTimer IsNot Nothing Then previewTimer.Stop()
        If DialogResult = DialogResult.Cancel Then
            For Each entry As KeyValuePair(Of Illumination.BulbInfo, Illumination.BulbInfo) In additionalOriginals
                CopyTextAppearance(entry.Value, entry.Key)
                entry.Key.IsIlluminatedImageDirty = True
            Next
            bulb.Text = originalText
            bulb.FontName = originalFontName
            bulb.FontSize = originalFontSize
            bulb.FontStyle = originalFontStyle
            bulb.TextAlignment = originalAlignment
            bulb.DodgeColor = originalDodgeColor
            bulb.GlowIntensity = originalGlowIntensity
            bulb.LightTemperature = originalLightTemperature
            bulb.SelectionMaskData = originalSelectionMask
            bulb.SelectionTolerance = originalSelectionTolerance
            bulb.SelectionFeather = originalSelectionFeather
            bulb.IsIlluminatedImageDirty = True
        ElseIf DialogResult = DialogResult.OK Then
            Illumination.TextSettingsDefaults.Save(bulb)
        End If
        If Backglass.currentTabPage IsNot Nothing Then
            Backglass.currentTabPage.RefreshEditorLighting()
            Backglass.currentTabPage.Invalidate()
        End If
        If previewSource IsNot Nothing Then previewSource.Dispose() : previewSource = Nothing
        Dim oldPreview As Image = preview.Image
        preview.Image = Nothing
        If oldPreview IsNot Nothing Then oldPreview.Dispose()
        MyBase.OnFormClosing(e)
    End Sub

    Private Shared Sub CopyTextAppearance(ByVal source As Illumination.BulbInfo, ByVal target As Illumination.BulbInfo)
        target.Text = source.Text
        target.FontName = source.FontName
        target.FontSize = source.FontSize
        target.FontStyle = source.FontStyle
        target.TextAlignment = source.TextAlignment
        target.DodgeColor = source.DodgeColor
        target.GlowIntensity = source.GlowIntensity
        target.LightTemperature = source.LightTemperature
    End Sub
End Class
