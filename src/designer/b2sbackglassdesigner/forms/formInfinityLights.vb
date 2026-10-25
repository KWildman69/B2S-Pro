Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.Runtime.InteropServices

Friend Enum InfinityAnimationEffect
    Clockwise
    Counterclockwise
    Inward
    Outward
    Bounce
    Pulse
    Strobe
End Enum

Friend Class InfinityEffectSetting
    Public Effect As InfinityAnimationEffect
    Public Cycles As Integer
    Public Speed As Integer
End Class

Public Class formInfinityLights
    Inherits B2SThemedForm

    Private ReadOnly numBulbSize As New NumericUpDown()
    Private ReadOnly numSpacing As New NumericUpDown()
    Private ReadOnly numDepth As New NumericUpDown()
    Private ReadOnly numInset As New NumericUpDown()
    Private ReadOnly numBrightness As New NumericUpDown()
    Private ReadOnly numKelvin As New NumericUpDown()
    Private ReadOnly numRowsLit As New NumericUpDown()
    Private ReadOnly numColorStrength As New NumericUpDown()
    Private ReadOnly numGlowRadius As New NumericUpDown()
    Private ReadOnly numEdgeSoftness As New NumericUpDown()
    Private ReadOnly numOutwardDiffusion As New NumericUpDown()
    Private ReadOnly numMaskRadius As New NumericUpDown()
    Private ReadOnly numMaskFeather As New NumericUpDown()
    Private ReadOnly numMaskShiftEdge As New NumericUpDown()
    Private ReadOnly numMaskSmooth As New NumericUpDown()
    Private ReadOnly btnColor As New Button()
    Private ReadOnly btnQuickSelection As New Button()
    Private ReadOnly cmbLinePattern As New ComboBox()
    Private ReadOnly chkShowAllBulbs As New CheckBox()
    Private ReadOnly btnTestAnimation As New Button()
    Private ReadOnly animationTimer As New Timer()
    Private ReadOnly playlist As New DataGridView()
    Private ReadOnly preview As New PictureBox()
    Private ReadOnly btnPreviewZoomOut As New Button()
    Private ReadOnly btnPreviewZoomIn As New Button()
    Private ReadOnly previewZoomLabel As New Label()
    Private infinityPreviewGroup As GroupBox
    Private ReadOnly outerBounds As Rectangle
    Private ReadOnly previewBackground As Bitmap
    Private ReadOnly quickSelectionArtwork As Bitmap
    Private ReadOnly infinitySettings As Illumination.BulbInfo
    Private previewSource As Bitmap
    Private previewStaticLayer As Bitmap
    Private previewStaticLayerKey As String = String.Empty
    Private previewZoomPercent As Integer
    Private previewPanOffset As Point
    Private previewIsPanning As Boolean
    Private previewPanStart As Point
    Private previewPanStartOffset As Point
    Private testEffectIndex As Integer
    Private testCycle As Integer
    Private testPhase As Integer
    Private committingTypedNumber As Boolean

    Public Sub New(ByVal bounds As Rectangle,
                   Optional ByVal background As Image = Nothing,
                   Optional ByVal savedSettings As String = "",
                   Optional ByVal sourceSettings As Illumination.BulbInfo = Nothing,
                   Optional ByVal selectionArtwork As Image = Nothing)
        outerBounds = bounds
        infinitySettings = New Illumination.BulbInfo() With {
            .Location = bounds.Location,
            .Size = bounds.Size,
            .ParentForm = If(sourceSettings Is Nothing, eParentForm.Backglass, sourceSettings.ParentForm)
        }
        If sourceSettings IsNot Nothing Then CopyEditableSettings(sourceSettings, infinitySettings)
        If background IsNot Nothing AndAlso background.Width > 0 AndAlso background.Height > 0 Then
            previewBackground = New Bitmap(background.Width, background.Height, PixelFormat.Format32bppArgb)
            Using graphics As Graphics = Graphics.FromImage(previewBackground)
                graphics.DrawImage(background, New Rectangle(Point.Empty, previewBackground.Size))
            End Using
        End If
        If selectionArtwork IsNot Nothing AndAlso selectionArtwork.Width > 0 AndAlso selectionArtwork.Height > 0 Then
            quickSelectionArtwork = New Bitmap(selectionArtwork)
        ElseIf previewBackground IsNot Nothing Then
            quickSelectionArtwork = New Bitmap(previewBackground)
        End If
        Text = "Generate Infinity Lights"
        Name = "formInfinityLights"
        StartPosition = FormStartPosition.CenterParent
        FormBorderStyle = FormBorderStyle.Sizable
        MaximizeBox = True
        MinimizeBox = False
        DoubleBuffered = True
        ClientSize = New Size(1120, 650)
        MinimumSize = New Size(1136, 689)
        BackColor = Color.FromArgb(8, 11, 17)
        ForeColor = Color.White
        Font = New Font("Segoe UI", 9.0F)

        Dim settings As New TableLayoutPanel() With {
            .Location = New Point(0, 0), .Size = New Size(382, 780),
            .ColumnCount = 2, .RowCount = 20, .BackColor = Color.FromArgb(14, 18, 27),
            .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .Dock = DockStyle.Top
        }
        settings.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 35))
        settings.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 65))
        Controls.Add(settings)

        ConfigureNumber(numBulbSize, 8, 160, Math.Max(18, Math.Min(80, bounds.Height \ 10)), 1)
        ConfigureNumber(numSpacing, 4, 1000, Math.Max(8, Math.Min(70, bounds.Width \ 24)), 1)
        ConfigureNumber(numDepth, 1, 12, 5, 1)
        ConfigureNumber(numInset, 2, 160, Math.Max(8, Math.Min(45, bounds.Height \ 18)), 1)
        ConfigureNumber(numBrightness, 0, 1600, 800, 10)
        ConfigureNumber(numKelvin, 2000, 6500, 3000, 100)
        ConfigureNumber(numRowsLit, 1, 24, 2, 1)
        ConfigureNumber(numColorStrength, 0, 100, 60, 1)
        ' Existing v1-v5 Infinity files did not apply these controls. Zero is
        ' therefore the compatibility default; v6 settings are loaded below.
        ConfigureNumber(numGlowRadius, 0, 1000, 0, 1)
        ConfigureNumber(numEdgeSoftness, 0, 300, 0, 1)
        ConfigureNumber(numOutwardDiffusion, 0, 300, 0, 1)
        ConfigureNumber(numMaskRadius, 0, 100, 0, 1)
        ConfigureNumber(numMaskFeather, 0, 250, 0, 1)
        ConfigureNumber(numMaskShiftEdge, -100, 100, 0, 1)
        ConfigureNumber(numMaskSmooth, 0, 100, 0, 1)
        AddSliderSetting(settings, 0, "Bulb diameter", numBulbSize, "px")
        AddSliderSetting(settings, 1, "Bulb spacing", numSpacing, "px")
        AddSliderSetting(settings, 2, "Mirror depth", numDepth, "rings")
        AddSliderSetting(settings, 3, "Depth inset", numInset, "px")
        AddSliderSetting(settings, 4, "Brightness", numBrightness, "%")
        AddSliderSetting(settings, 5, "Bulb temperature", numKelvin, "K")
        AddSliderSetting(settings, 6, "Lines / rings lit", numRowsLit, "")
        cmbLinePattern.DropDownStyle = ComboBoxStyle.DropDownList
        cmbLinePattern.Items.AddRange(New Object() {"Evenly spaced", "Adjacent"})
        cmbLinePattern.SelectedIndex = 0
        AddSetting(settings, 7, "Rotating line pattern", cmbLinePattern, String.Empty)
        cmbLinePattern.Width = 210
        btnColor.Text = "White"
        btnColor.BackColor = Color.White
        btnColor.ForeColor = Color.Black
        AddSetting(settings, 8, "Bulb color", btnColor, String.Empty)
        AddHandler btnColor.Click, AddressOf ChooseBulbColor
        AddSliderSetting(settings, 9, "Color strength", numColorStrength, "%")
        chkShowAllBulbs.Text = "Show all bulbs (including unlit)"
        chkShowAllBulbs.Checked = True
        chkShowAllBulbs.AutoSize = True
        chkShowAllBulbs.ForeColor = Color.White
        settings.RowStyles.Add(New RowStyle(SizeType.Absolute, 40))
        settings.Controls.Add(chkShowAllBulbs, 0, 10)
        settings.SetColumnSpan(chkShowAllBulbs, 2)
        AddHandler chkShowAllBulbs.CheckedChanged, AddressOf PreviewChanged

        AddSliderSetting(settings, 11, "Glow Radius", numGlowRadius, "px")
        AddSliderSetting(settings, 12, "Edge Softness", numEdgeSoftness, "%")
        AddSliderSetting(settings, 13, "Outward Diffusion", numOutwardDiffusion, "%")
        AddSliderSetting(settings, 14, "Radius", numMaskRadius, "px")
        AddSliderSetting(settings, 15, "Feather", numMaskFeather, "px")
        AddSliderSetting(settings, 16, "Shift Edge", numMaskShiftEdge, "%")
        AddSliderSetting(settings, 17, "Smooth", numMaskSmooth, "%")

        btnQuickSelection.Text = "Quick Selection..."
        btnQuickSelection.Dock = DockStyle.Fill
        settings.RowStyles.Add(New RowStyle(SizeType.Absolute, 42))
        settings.Controls.Add(btnQuickSelection, 0, 18)
        settings.SetColumnSpan(btnQuickSelection, 2)
        AddHandler btnQuickSelection.Click, AddressOf QuickSelection_Click

        Dim note As New Label() With {
            .Text = "The selected light box is the outer boundary. It will be replaced by generated bulb images and one normal looping B2S animation.",
            .Dock = DockStyle.Fill, .ForeColor = Color.Gainsboro, .AutoSize = False
        }
        settings.RowStyles.Add(New RowStyle(SizeType.Absolute, 64))
        settings.Controls.Add(note, 0, 19)
        settings.SetColumnSpan(note, 2)

        preview.Location = New Point(428, 14)
        preview.Size = New Size(678, 320)
        preview.BackColor = Color.Black
        preview.SizeMode = PictureBoxSizeMode.Normal
        preview.BorderStyle = BorderStyle.FixedSingle
        preview.TabStop = True
        preview.Anchor = AnchorStyles.Top Or AnchorStyles.Bottom Or AnchorStyles.Left Or AnchorStyles.Right
        Controls.Add(preview)

        AddHandler preview.MouseWheel, AddressOf Preview_MouseWheel
        AddHandler preview.MouseDown, AddressOf Preview_MouseDown
        AddHandler preview.MouseMove, AddressOf Preview_MouseMove
        AddHandler preview.MouseUp, AddressOf Preview_MouseUp
        AddHandler preview.MouseLeave, AddressOf Preview_MouseLeave
        AddHandler preview.MouseEnter, AddressOf Preview_MouseEnter
        AddHandler preview.DoubleClick, AddressOf Preview_DoubleClick

        playlist.Location = New Point(428, 388)
        playlist.Size = New Size(678, 188)
        playlist.AllowUserToAddRows = False
        playlist.AllowUserToDeleteRows = False
        playlist.AllowUserToResizeRows = False
        playlist.RowHeadersVisible = False
        playlist.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        playlist.MultiSelect = False
        playlist.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        playlist.BackgroundColor = Color.FromArgb(14, 18, 27)
        playlist.ForeColor = Color.White
        playlist.DefaultCellStyle.BackColor = Color.FromArgb(23, 29, 42)
        playlist.DefaultCellStyle.ForeColor = Color.White
        playlist.DefaultCellStyle.SelectionBackColor = Color.FromArgb(32, 82, 122)
        playlist.Anchor = AnchorStyles.Left Or AnchorStyles.Right Or AnchorStyles.Bottom
        Dim effectColumn As New DataGridViewComboBoxColumn() With {.Name = "Effect", .HeaderText = "Preprogrammed effect", .FlatStyle = FlatStyle.Flat}
        effectColumn.Items.AddRange("Clockwise", "Counterclockwise", "Inward", "Outward", "Bounce", "Pulse", "Strobe")
        playlist.Columns.Add(effectColumn)
        playlist.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Cycles", .HeaderText = "Cycles", .FillWeight = 35})
        playlist.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Speed", .HeaderText = "Speed (ms)", .FillWeight = 45})
        Controls.Add(playlist)
        AddPlaylistRow(InfinityAnimationEffect.Clockwise, 2, 70)

        Dim addEffect As New Button() With {.Text = "Add Effect", .Location = New Point(428, 584), .Size = New Size(105, 30)}
        Dim removeEffect As New Button() With {.Text = "Remove", .Location = New Point(539, 584), .Size = New Size(90, 30)}
        Dim moveUp As New Button() With {.Text = "Move Up", .Location = New Point(635, 584), .Size = New Size(90, 30)}
        Dim moveDown As New Button() With {.Text = "Move Down", .Location = New Point(731, 584), .Size = New Size(96, 30)}
        For Each button As Button In New Button() {addEffect, removeEffect, moveUp, moveDown}
            button.Anchor = AnchorStyles.Left Or AnchorStyles.Bottom
        Next
        AddHandler addEffect.Click, Sub() AddPlaylistRow(InfinityAnimationEffect.Clockwise, 1, 70)
        AddHandler removeEffect.Click, AddressOf RemoveSelectedEffect
        AddHandler moveUp.Click, Sub() MoveSelectedEffect(-1)
        AddHandler moveDown.Click, Sub() MoveSelectedEffect(1)
        Controls.AddRange(New Control() {addEffect, removeEffect, moveUp, moveDown})

        btnTestAnimation.Text = "Test Animation"
        btnTestAnimation.Location = New Point(14, 520)
        btnTestAnimation.Size = New Size(400, 38)
        btnTestAnimation.Anchor = AnchorStyles.Left Or AnchorStyles.Bottom
        Controls.Add(btnTestAnimation)
        AddHandler btnTestAnimation.Click, AddressOf ToggleAnimationTest
        AddHandler animationTimer.Tick, AddressOf AnimationTimerTick

        Dim createButton As New Button() With {.Text = "Create", .DialogResult = DialogResult.OK, .Location = New Point(918, 596), .Size = New Size(90, 34)}
        Dim cancelButton As New Button() With {.Text = "Cancel", .DialogResult = DialogResult.Cancel, .Location = New Point(1016, 596), .Size = New Size(90, 34)}
        createButton.Anchor = AnchorStyles.Right Or AnchorStyles.Bottom
        cancelButton.Anchor = AnchorStyles.Right Or AnchorStyles.Bottom
        Controls.Add(createButton)
        Controls.Add(cancelButton)

        ' Match the proven Light/Flasher editor layout: keep the settings column
        ' fixed and give all remaining resize space to the preview side.
        Dim layoutRoot As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 1,
            .Padding = New Padding(14),
            .Margin = Padding.Empty,
            .BackColor = BackColor
        }
        layoutRoot.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 400.0F))
        layoutRoot.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        layoutRoot.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        Dim leftPanel As New Panel() With {.Dock = DockStyle.Fill, .Margin = Padding.Empty, .BackColor = BackColor}
        Dim settingsScroll As New Panel() With {.Dock = DockStyle.Fill, .AutoScroll = True, .BackColor = settings.BackColor}
        settingsScroll.Controls.Add(settings)
        btnTestAnimation.Dock = DockStyle.Bottom
        btnTestAnimation.Location = Point.Empty
        leftPanel.Controls.Add(settingsScroll)
        leftPanel.Controls.Add(btnTestAnimation)

        Dim rightLayout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .Margin = New Padding(14, 0, 0, 0),
            .Padding = Padding.Empty,
            .BackColor = BackColor
        }
        rightLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        rightLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        rightLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 188.0F))
        rightLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 48.0F))

        infinityPreviewGroup = New GroupBox() With {
            .Text = "  LIVE INFINITY PREVIEW",
            .Dock = DockStyle.Fill,
            .Margin = Padding.Empty,
            .ForeColor = Color.FromArgb(42, 225, 105),
            .BackColor = BackColor
        }
        preview.Dock = DockStyle.None
        preview.Anchor = AnchorStyles.Top Or AnchorStyles.Left
        btnPreviewZoomOut.Text = "−"
        btnPreviewZoomOut.Size = New Size(42, 29)
        btnPreviewZoomOut.FlatStyle = FlatStyle.Flat
        btnPreviewZoomOut.Anchor = AnchorStyles.Bottom Or AnchorStyles.Left
        btnPreviewZoomIn.Text = "+"
        btnPreviewZoomIn.Size = New Size(42, 29)
        btnPreviewZoomIn.FlatStyle = FlatStyle.Flat
        btnPreviewZoomIn.Anchor = AnchorStyles.Bottom Or AnchorStyles.Left
        For Each button As Button In New Button() {btnPreviewZoomOut, btnPreviewZoomIn}
            button.BackColor = Color.FromArgb(38, 40, 43)
            button.ForeColor = Color.WhiteSmoke
            button.FlatAppearance.BorderColor = Color.FromArgb(78, 80, 84)
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(52, 54, 57)
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(64, 66, 70)
        Next
        AddHandler btnPreviewZoomOut.Click, AddressOf PreviewZoomOut_Click
        AddHandler btnPreviewZoomIn.Click, AddressOf PreviewZoomIn_Click
        previewZoomLabel.Text = "Fit"
        previewZoomLabel.Size = New Size(62, 22)
        previewZoomLabel.TextAlign = ContentAlignment.MiddleCenter
        previewZoomLabel.ForeColor = Color.FromArgb(42, 225, 105)
        previewZoomLabel.Font = New Font("Tahoma", 8.25F, FontStyle.Bold)
        previewZoomLabel.Anchor = AnchorStyles.Bottom Or AnchorStyles.Left
        infinityPreviewGroup.Controls.AddRange(New Control() {preview, btnPreviewZoomOut, btnPreviewZoomIn, previewZoomLabel})

        playlist.Dock = DockStyle.Fill
        playlist.Margin = New Padding(0, 8, 0, 0)

        Dim actionLayout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 1,
            .Margin = Padding.Empty,
            .Padding = New Padding(0, 7, 0, 0),
            .BackColor = BackColor
        }
        actionLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        actionLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 210.0F))

        Dim effectActions As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .Margin = Padding.Empty,
            .BackColor = BackColor
        }
        effectActions.Controls.AddRange(New Control() {addEffect, removeEffect, moveUp, moveDown})

        Dim dialogActions As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.RightToLeft,
            .WrapContents = False,
            .Margin = Padding.Empty,
            .BackColor = BackColor
        }
        cancelButton.Margin = Padding.Empty
        createButton.Margin = New Padding(0, 0, 8, 0)
        dialogActions.Controls.Add(cancelButton)
        dialogActions.Controls.Add(createButton)
        actionLayout.Controls.Add(effectActions, 0, 0)
        actionLayout.Controls.Add(dialogActions, 1, 0)

        rightLayout.Controls.Add(infinityPreviewGroup, 0, 0)
        rightLayout.Controls.Add(playlist, 0, 1)
        rightLayout.Controls.Add(actionLayout, 0, 2)
        layoutRoot.Controls.Add(leftPanel, 0, 0)
        layoutRoot.Controls.Add(rightLayout, 1, 0)
        Controls.Add(layoutRoot)
        layoutRoot.BringToFront()

        AddHandler Me.Resize, AddressOf InfinityEditor_Resize
        AddHandler Me.Shown, AddressOf InfinityEditor_Resize
        AddHandler infinityPreviewGroup.Resize, AddressOf InfinityPreviewSection_Resize
        LayoutInfinityEditor()

        AcceptButton = createButton
        Me.CancelButton = cancelButton
        AddHandler createButton.Click, AddressOf ValidatePlaylist
        AddHandler numDepth.ValueChanged, AddressOf DepthChanged
        AddHandler numRowsLit.ValueChanged, AddressOf PreviewChanged
        AddHandler cmbLinePattern.SelectedIndexChanged, AddressOf PreviewChanged

        For Each control As NumericUpDown In New NumericUpDown() {numBulbSize, numSpacing, numDepth, numInset, numBrightness, numKelvin, numRowsLit, numColorStrength,
                                                                  numGlowRadius, numEdgeSoftness, numOutwardDiffusion, numMaskRadius, numMaskFeather, numMaskShiftEdge, numMaskSmooth}
            AddHandler control.ValueChanged, AddressOf PreviewChanged
            AddHandler control.TextChanged, AddressOf NumericTextChanged
            AddHandler control.KeyDown, AddressOf NumericKeyDown
        Next
        ApplySavedSettings(savedSettings)
        RenderPreview()
    End Sub

    Private Sub InfinityEditor_Resize(ByVal sender As Object, ByVal e As EventArgs)
        LayoutInfinityEditor()
        RefreshRenderedPreview()
    End Sub

    Private Sub InfinityPreviewSection_Resize(ByVal sender As Object, ByVal e As EventArgs)
        LayoutInfinityEditor()
    End Sub

    Private Sub LayoutInfinityEditor()
        If infinityPreviewGroup Is Nothing Then Return

        preview.Left = 14
        preview.Top = 28
        preview.Width = Math.Max(300, infinityPreviewGroup.ClientSize.Width - 28)
        preview.Height = Math.Max(180, infinityPreviewGroup.ClientSize.Height - 78)

        Dim controlsTop As Integer = infinityPreviewGroup.ClientSize.Height - 39
        btnPreviewZoomOut.Location = New Point(14, controlsTop)
        btnPreviewZoomIn.Location = New Point(61, controlsTop)
        previewZoomLabel.Location = New Point(108, controlsTop + 4)
    End Sub

    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        If disposing Then
            If preview.Image IsNot Nothing Then preview.Image.Dispose()
            If previewSource IsNot Nothing Then previewSource.Dispose()
            If previewStaticLayer IsNot Nothing Then previewStaticLayer.Dispose()
            If previewBackground IsNot Nothing Then previewBackground.Dispose()
            If quickSelectionArtwork IsNot Nothing Then quickSelectionArtwork.Dispose()
            animationTimer.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub

    Public ReadOnly Property BulbSize As Integer
        Get
            Return CInt(numBulbSize.Value)
        End Get
    End Property
    Public ReadOnly Property Spacing As Integer
        Get
            Return CInt(numSpacing.Value)
        End Get
    End Property
    Public ReadOnly Property Depth As Integer
        Get
            Return CInt(numDepth.Value)
        End Get
    End Property
    Public ReadOnly Property DepthInset As Integer
        Get
            Return CInt(numInset.Value)
        End Get
    End Property
    Public ReadOnly Property Kelvin As Integer
        Get
            Return CInt(numKelvin.Value)
        End Get
    End Property
    Public ReadOnly Property Brightness As Integer
        Get
            Return CInt(numBrightness.Value)
        End Get
    End Property
    Public ReadOnly Property RowsLit As Integer
        Get
            Return CInt(numRowsLit.Value)
        End Get
    End Property
    Public ReadOnly Property BulbColor As Color
        Get
            Return btnColor.BackColor
        End Get
    End Property
    Public ReadOnly Property ColorStrength As Integer
        Get
            Return CInt(numColorStrength.Value)
        End Get
    End Property
    Public ReadOnly Property ShowAllBulbs As Boolean
        Get
            Return chkShowAllBulbs.Checked
        End Get
    End Property
    Public ReadOnly Property GlowRadius As Integer
        Get
            Return CInt(numGlowRadius.Value)
        End Get
    End Property
    Public ReadOnly Property EdgeSoftness As Integer
        Get
            Return CInt(numEdgeSoftness.Value)
        End Get
    End Property
    Public ReadOnly Property OutwardDiffusion As Integer
        Get
            Return CInt(numOutwardDiffusion.Value)
        End Get
    End Property
    Public ReadOnly Property MaskRadius As Integer
        Get
            Return CInt(numMaskRadius.Value)
        End Get
    End Property
    Public ReadOnly Property MaskFeather As Integer
        Get
            Return CInt(numMaskFeather.Value)
        End Get
    End Property
    Public ReadOnly Property MaskShiftEdge As Integer
        Get
            Return CInt(numMaskShiftEdge.Value)
        End Get
    End Property
    Public ReadOnly Property MaskSmooth As Integer
        Get
            Return CInt(numMaskSmooth.Value)
        End Get
    End Property
    Friend ReadOnly Property SettingsBulb As Illumination.BulbInfo
        Get
            ApplyControlsToSettingsBulb()
            Return infinitySettings
        End Get
    End Property
    Public ReadOnly Property EvenlySpacedLines As Boolean
        Get
            Return cmbLinePattern.SelectedIndex <> 1
        End Get
    End Property

    Private Shared Sub CopyEditableSettings(ByVal source As Illumination.BulbInfo,
                                            ByVal target As Illumination.BulbInfo)
        If source Is Nothing OrElse target Is Nothing Then Return
        target.SelectionMaskData = source.SelectionMaskData
        target.SelectionTolerance = source.SelectionTolerance
        target.SelectionFeather = source.SelectionFeather
        target.MaskRadius = source.MaskRadius
        target.MaskSmooth = source.MaskSmooth
        target.MaskFeather = source.MaskFeather
        target.MaskShiftEdge = source.MaskShiftEdge
        target.InFrontOfGlobalMask = source.InFrontOfGlobalMask
        target.GlobalMaskLayerExplicit = source.GlobalMaskLayerExplicit
        target.LightBehindCanvas = source.LightBehindCanvas
        target.SnippitInfo.BehindCanvas = source.SnippitInfo.BehindCanvas
        target.GlowSpread = source.GlowSpread
        target.GlowSoftness = source.GlowSoftness
        target.GlowFalloff = source.GlowFalloff
    End Sub

    Private Sub ApplyControlsToSettingsBulb()
        infinitySettings.GlowSpread = GlowRadius
        infinitySettings.GlowSoftness = EdgeSoftness
        infinitySettings.GlowFalloff = OutwardDiffusion
        infinitySettings.MaskRadius = MaskRadius
        infinitySettings.MaskFeather = MaskFeather
        infinitySettings.MaskShiftEdge = MaskShiftEdge
        infinitySettings.MaskSmooth = MaskSmooth
    End Sub

    Private Sub QuickSelection_Click(ByVal sender As Object, ByVal e As EventArgs)
        If quickSelectionArtwork Is Nothing Then Return
        ApplyControlsToSettingsBulb()
        Using editor As New formQuickSelection(infinitySettings, quickSelectionArtwork)
            If editor.ShowDialog(Me) = DialogResult.OK Then RenderPreview()
        End Using
    End Sub

    Private Sub ChooseBulbColor(sender As Object, e As EventArgs)
        Using picker As New ColorDialog() With {.Color = btnColor.BackColor, .FullOpen = True}
            If picker.ShowDialog(Me) = DialogResult.OK Then btnColor.BackColor = picker.Color : btnColor.Text = picker.Color.Name : RenderPreview()
        End Using
    End Sub
    Friend ReadOnly Property Effects As List(Of InfinityEffectSetting)
        Get
            playlist.EndEdit()
            Dim result As New List(Of InfinityEffectSetting)()
            For Each row As DataGridViewRow In playlist.Rows
                Dim effectName As String = Convert.ToString(row.Cells("Effect").Value)
                Dim effect As InfinityAnimationEffect
                If Not [Enum].TryParse(effectName, effect) Then Continue For
                Dim cycles As Integer
                Dim speed As Integer
                If Not Integer.TryParse(Convert.ToString(row.Cells("Cycles").Value), cycles) Then cycles = 1
                If Not Integer.TryParse(Convert.ToString(row.Cells("Speed").Value), speed) Then speed = 70
                result.Add(New InfinityEffectSetting() With {.Effect = effect, .Cycles = Math.Max(1, Math.Min(100, cycles)), .Speed = Math.Max(10, Math.Min(2000, speed))})
            Next
            Return result
        End Get
    End Property

    Friend Function SerializeSettings() As String
        Dim effectText As String = String.Join(";", Effects.Select(Function(item) String.Join(",", CInt(item.Effect), item.Cycles, item.Speed)).ToArray())
        Return String.Join("|", "v6", BulbSize, Spacing, Depth, DepthInset, Kelvin, RowsLit, BulbColor.ToArgb(), If(EvenlySpacedLines, 0, 1), Brightness, ColorStrength, If(ShowAllBulbs, 1, 0),
                           GlowRadius, EdgeSoftness, OutwardDiffusion, MaskRadius, MaskFeather, MaskShiftEdge, MaskSmooth, effectText)
    End Function

    Private Sub ApplySavedSettings(ByVal value As String)
        If String.IsNullOrWhiteSpace(value) Then Return
        Dim parts() As String = value.Split("|"c)
        If parts.Length < 9 OrElse (parts(0) <> "v1" AndAlso parts(0) <> "v2" AndAlso parts(0) <> "v3" AndAlso parts(0) <> "v4" AndAlso parts(0) <> "v5" AndAlso parts(0) <> "v6") Then Return
        Dim parsed As Integer
        If Integer.TryParse(parts(1), parsed) Then numBulbSize.Value = Math.Max(numBulbSize.Minimum, Math.Min(numBulbSize.Maximum, parsed))
        If Integer.TryParse(parts(2), parsed) Then numSpacing.Value = Math.Max(numSpacing.Minimum, Math.Min(numSpacing.Maximum, parsed))
        If Integer.TryParse(parts(3), parsed) Then numDepth.Value = Math.Max(numDepth.Minimum, Math.Min(numDepth.Maximum, parsed))
        numRowsLit.Maximum = numDepth.Value
        If Integer.TryParse(parts(4), parsed) Then numInset.Value = Math.Max(numInset.Minimum, Math.Min(numInset.Maximum, parsed))
        If Integer.TryParse(parts(5), parsed) Then numKelvin.Value = Math.Max(numKelvin.Minimum, Math.Min(numKelvin.Maximum, parsed))
        If Integer.TryParse(parts(6), parsed) Then numRowsLit.Value = Math.Max(numRowsLit.Minimum, Math.Min(numRowsLit.Maximum, parsed))
        If Integer.TryParse(parts(7), parsed) Then
            btnColor.BackColor = Color.FromArgb(parsed)
            btnColor.ForeColor = If(btnColor.BackColor.GetBrightness() < 0.45F, Color.White, Color.Black)
            btnColor.Text = btnColor.BackColor.Name
        End If
        Dim effectPart As Integer = 8
        If (parts(0) = "v2" OrElse parts(0) = "v3" OrElse parts(0) = "v4" OrElse parts(0) = "v5" OrElse parts(0) = "v6") AndAlso parts.Length >= 10 Then
            If Integer.TryParse(parts(8), parsed) Then cmbLinePattern.SelectedIndex = If(parsed = 1, 1, 0)
            effectPart = 9
        End If
        If parts(0) = "v3" AndAlso parts.Length >= 11 Then
            If Integer.TryParse(parts(9), parsed) Then numBrightness.Value = Math.Max(numBrightness.Minimum, Math.Min(numBrightness.Maximum, parsed))
            effectPart = 10
        End If
        If parts(0) = "v4" AndAlso parts.Length >= 12 Then
            If Integer.TryParse(parts(9), parsed) Then numBrightness.Value = Math.Max(numBrightness.Minimum, Math.Min(numBrightness.Maximum, parsed))
            If Integer.TryParse(parts(10), parsed) Then numColorStrength.Value = Math.Max(numColorStrength.Minimum, Math.Min(numColorStrength.Maximum, parsed))
            effectPart = 11
        End If
        If parts(0) = "v5" AndAlso parts.Length >= 13 Then
            If Integer.TryParse(parts(9), parsed) Then numBrightness.Value = Math.Max(numBrightness.Minimum, Math.Min(numBrightness.Maximum, parsed))
            If Integer.TryParse(parts(10), parsed) Then numColorStrength.Value = Math.Max(numColorStrength.Minimum, Math.Min(numColorStrength.Maximum, parsed))
            If Integer.TryParse(parts(11), parsed) Then chkShowAllBulbs.Checked = parsed <> 0
            effectPart = 12
        End If
        If parts(0) = "v6" AndAlso parts.Length >= 20 Then
            If Integer.TryParse(parts(9), parsed) Then numBrightness.Value = Math.Max(numBrightness.Minimum, Math.Min(numBrightness.Maximum, parsed))
            If Integer.TryParse(parts(10), parsed) Then numColorStrength.Value = Math.Max(numColorStrength.Minimum, Math.Min(numColorStrength.Maximum, parsed))
            If Integer.TryParse(parts(11), parsed) Then chkShowAllBulbs.Checked = parsed <> 0
            If Integer.TryParse(parts(12), parsed) Then numGlowRadius.Value = Math.Max(numGlowRadius.Minimum, Math.Min(numGlowRadius.Maximum, parsed))
            If Integer.TryParse(parts(13), parsed) Then numEdgeSoftness.Value = Math.Max(numEdgeSoftness.Minimum, Math.Min(numEdgeSoftness.Maximum, parsed))
            If Integer.TryParse(parts(14), parsed) Then numOutwardDiffusion.Value = Math.Max(numOutwardDiffusion.Minimum, Math.Min(numOutwardDiffusion.Maximum, parsed))
            If Integer.TryParse(parts(15), parsed) Then numMaskRadius.Value = Math.Max(numMaskRadius.Minimum, Math.Min(numMaskRadius.Maximum, parsed))
            If Integer.TryParse(parts(16), parsed) Then numMaskFeather.Value = Math.Max(numMaskFeather.Minimum, Math.Min(numMaskFeather.Maximum, parsed))
            If Integer.TryParse(parts(17), parsed) Then numMaskShiftEdge.Value = Math.Max(numMaskShiftEdge.Minimum, Math.Min(numMaskShiftEdge.Maximum, parsed))
            If Integer.TryParse(parts(18), parsed) Then numMaskSmooth.Value = Math.Max(numMaskSmooth.Minimum, Math.Min(numMaskSmooth.Maximum, parsed))
            effectPart = 19
        End If
        playlist.Rows.Clear()
        For Each token As String In parts(effectPart).Split(";"c)
            Dim values() As String = token.Split(","c)
            If values.Length <> 3 Then Continue For
            Dim effectValue, cycles, speed As Integer
            If Integer.TryParse(values(0), effectValue) AndAlso [Enum].IsDefined(GetType(InfinityAnimationEffect), effectValue) AndAlso
               Integer.TryParse(values(1), cycles) AndAlso Integer.TryParse(values(2), speed) Then
                AddPlaylistRow(CType(effectValue, InfinityAnimationEffect), Math.Max(1, Math.Min(100, cycles)), Math.Max(10, Math.Min(2000, speed)))
            End If
        Next
        If playlist.Rows.Count = 0 Then AddPlaylistRow(InfinityAnimationEffect.Clockwise, 2, 70)
    End Sub

    Private Sub AddPlaylistRow(ByVal effect As InfinityAnimationEffect, ByVal cycles As Integer, ByVal speed As Integer)
        Dim index As Integer = playlist.Rows.Add(effect.ToString(), cycles, speed)
        playlist.ClearSelection()
        playlist.Rows(index).Selected = True
    End Sub

    Private Sub RemoveSelectedEffect(ByVal sender As Object, ByVal e As EventArgs)
        If playlist.SelectedRows.Count = 0 Then Return
        playlist.Rows.RemoveAt(playlist.SelectedRows(0).Index)
    End Sub

    Private Sub MoveSelectedEffect(ByVal direction As Integer)
        If playlist.SelectedRows.Count = 0 Then Return
        Dim sourceIndex As Integer = playlist.SelectedRows(0).Index
        Dim targetIndex As Integer = sourceIndex + direction
        If targetIndex < 0 OrElse targetIndex >= playlist.Rows.Count Then Return
        Dim values As Object() = playlist.Rows(sourceIndex).Cells.Cast(Of DataGridViewCell)().Select(Function(cell) cell.Value).ToArray()
        playlist.Rows.RemoveAt(sourceIndex)
        playlist.Rows.Insert(targetIndex, values)
        playlist.ClearSelection()
        playlist.Rows(targetIndex).Selected = True
    End Sub

    Private Sub DepthChanged(ByVal sender As Object, ByVal e As EventArgs)
        RenderPreview()
    End Sub

    Private Sub ValidatePlaylist(ByVal sender As Object, ByVal e As EventArgs)
        If Effects.Count > 0 Then Return
        DialogResult = DialogResult.None
        MessageBox.Show(Me, "Add at least one animation effect.", "Generate Infinity Lights", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    Private Shared Sub ConfigureNumber(ByVal control As NumericUpDown, ByVal minimum As Integer, ByVal maximum As Integer, ByVal value As Integer, ByVal increment As Integer)
        control.Minimum = minimum
        control.Maximum = maximum
        control.Value = Math.Max(minimum, Math.Min(maximum, value))
        control.Increment = increment
        control.Dock = DockStyle.Fill
        control.BackColor = Color.FromArgb(23, 29, 42)
        control.ForeColor = Color.White
    End Sub

    Private Shared Sub AddSetting(ByVal panel As TableLayoutPanel, ByVal row As Integer, ByVal caption As String, ByVal control As Control, ByVal suffix As String)
        panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 40))
        panel.Controls.Add(New Label() With {.Text = caption, .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, row)
        Dim host As New FlowLayoutPanel() With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.LeftToRight, .WrapContents = False}
        control.Width = 70
        host.Controls.Add(control)
        host.Controls.Add(New Label() With {.Text = suffix, .AutoSize = True, .Margin = New Padding(4, 8, 0, 0)})
        panel.Controls.Add(host, 1, row)
    End Sub

    Private Shared Sub AddSliderSetting(ByVal panel As TableLayoutPanel, ByVal row As Integer, ByVal caption As String,
                                        ByVal number As NumericUpDown, ByVal suffix As String)
        panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 40))
        panel.Controls.Add(New Label() With {.Text = caption, .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, row)
        Dim host As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 3, .RowCount = 1, .Margin = Padding.Empty}
        host.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        host.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 72.0F))
        host.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 34.0F))
        Dim slider As New TrackBar() With {
            .Dock = DockStyle.Fill, .AutoSize = False, .Height = 32,
            .Minimum = Decimal.ToInt32(number.Minimum), .Maximum = Decimal.ToInt32(number.Maximum),
            .Value = Decimal.ToInt32(number.Value),
            .TickFrequency = Math.Max(1, (Decimal.ToInt32(number.Maximum) - Decimal.ToInt32(number.Minimum)) \ 10),
            .SmallChange = Math.Max(1, Decimal.ToInt32(number.Increment)),
            .LargeChange = Math.Max(1, (Decimal.ToInt32(number.Maximum) - Decimal.ToInt32(number.Minimum)) \ 20),
            .BackColor = panel.BackColor
        }
        number.Dock = DockStyle.Fill
        TrackBarNumericLink.Bind(slider, number, Decimal.ToInt32(number.Increment))
        host.Controls.Add(slider, 0, 0)
        host.Controls.Add(number, 1, 0)
        host.Controls.Add(New Label() With {.Text = suffix, .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 2, 0)
        panel.Controls.Add(host, 1, row)
    End Sub

    Private Sub PreviewChanged(ByVal sender As Object, ByVal e As EventArgs)
        If animationTimer.Enabled Then RenderAnimationTestFrame(False) Else RenderPreview()
    End Sub

    Private Sub NumericTextChanged(ByVal sender As Object, ByVal e As EventArgs)
        If committingTypedNumber Then Return
        Dim control As NumericUpDown = TryCast(sender, NumericUpDown)
        If control Is Nothing Then Return
        Dim typedValue As Decimal
        If Not Decimal.TryParse(control.Text, typedValue) Then Return
        If typedValue < control.Minimum OrElse typedValue > control.Maximum OrElse control.Value = typedValue Then Return
        committingTypedNumber = True
        Try
            control.Value = typedValue
        Finally
            committingTypedNumber = False
        End Try
    End Sub

    Private Sub NumericKeyDown(ByVal sender As Object, ByVal e As KeyEventArgs)
        If e.KeyCode <> Keys.Enter Then Return
        NumericTextChanged(sender, EventArgs.Empty)
        e.SuppressKeyPress = True
        e.Handled = True
    End Sub

    Private Sub ToggleAnimationTest(ByVal sender As Object, ByVal e As EventArgs)
        If animationTimer.Enabled Then
            animationTimer.Stop()
            btnTestAnimation.Text = "Test Animation"
            RenderPreview()
            Return
        End If
        If Effects.Count = 0 Then Return
        testEffectIndex = 0
        testCycle = 0
        testPhase = 0
        btnTestAnimation.Text = "Stop Test"
        RenderAnimationTestFrame(True)
        animationTimer.Start()
    End Sub

    Private Sub AnimationTimerTick(ByVal sender As Object, ByVal e As EventArgs)
        RenderAnimationTestFrame(True)
    End Sub

    Private Sub RenderAnimationTestFrame(ByVal advance As Boolean)
        Dim settings As List(Of InfinityEffectSetting) = Effects
        If settings.Count = 0 Then Return
        If testEffectIndex >= settings.Count Then testEffectIndex = 0
        Dim current As InfinityEffectSetting = settings(testEffectIndex)
        Dim bulbCount As Integer = InfinityLightGenerator.PerimeterBulbCount(New Rectangle(Point.Empty, outerBounds.Size), Spacing)
        Dim phaseCount As Integer = InfinityLightGenerator.EffectFrameCount(current.Effect, Depth, bulbCount)
        animationTimer.Interval = current.Speed
        Dim renderSize As Size = PreviewRenderSize()
        Dim snippetBounds As Rectangle = PreviewSnippetBounds(renderSize)
        Dim scale As Double = PreviewSnippetScale(snippetBounds.Size)
        Dim frame As Bitmap = InfinityLightGenerator.CreateAnimatedFrameImage(snippetBounds.Size, Depth, ScalePreviewValue(DepthInset, scale),
                                                                              bulbCount, ScalePreviewValue(BulbSize, scale), Kelvin,
                                                                              BulbColor, current.Effect, testPhase, phaseCount, RowsLit,
                                                                              EvenlySpacedLines, Brightness, ColorStrength, False,
                                                                              ScalePreviewOptionalValue(GlowRadius, scale), EdgeSoftness, OutwardDiffusion)
        Dim snippet As Bitmap = CreatePreviewSnippet(frame, snippetBounds.Size, scale, bulbCount)
        Dim canvas As New Bitmap(renderSize.Width, renderSize.Height, PixelFormat.Format32bppArgb)
        Using graphics As Graphics = Graphics.FromImage(canvas)
            graphics.Clear(Color.FromArgb(5, 6, 9))
            If previewBackground IsNot Nothing Then graphics.DrawImage(previewBackground, New Rectangle(0, 0, canvas.Width, canvas.Height))
            graphics.DrawImageUnscaled(snippet, snippetBounds.Location)
        End Using
        frame.Dispose()
        snippet.Dispose()
        SetPreviewSource(canvas)
        If Not advance Then Return
        testPhase += 1
        If testPhase >= phaseCount Then
            testPhase = 0
            testCycle += 1
            If testCycle >= current.Cycles Then
                testCycle = 0
                testEffectIndex = (testEffectIndex + 1) Mod settings.Count
            End If
        End If
    End Sub

    Private Sub RenderPreview()
        Dim renderSize As Size = PreviewRenderSize()
        Dim snippetBounds As Rectangle = PreviewSnippetBounds(renderSize)
        Dim scale As Double = PreviewSnippetScale(snippetBounds.Size)
        Dim bulbCount As Integer = InfinityLightGenerator.PerimeterBulbCount(New Rectangle(Point.Empty, outerBounds.Size), Spacing)
        Dim canvas As New Bitmap(renderSize.Width, renderSize.Height, PixelFormat.Format32bppArgb)
        Using graphics As Graphics = Graphics.FromImage(canvas)
            graphics.Clear(Color.FromArgb(5, 6, 9))
            If previewBackground IsNot Nothing Then graphics.DrawImage(previewBackground, New Rectangle(0, 0, canvas.Width, canvas.Height))
            If Effects.Count > 0 Then
                Dim firstEffect As InfinityEffectSetting = Effects(0)
                Dim phaseCount As Integer = InfinityLightGenerator.EffectFrameCount(firstEffect.Effect, Depth, bulbCount)
                Using frame As Bitmap = InfinityLightGenerator.CreateAnimatedFrameImage(snippetBounds.Size, Depth, ScalePreviewValue(DepthInset, scale),
                                                                                         bulbCount, ScalePreviewValue(BulbSize, scale), Kelvin,
                                                                                         BulbColor, firstEffect.Effect, 0, phaseCount, RowsLit,
                                                                                         EvenlySpacedLines, Brightness, ColorStrength, False,
                                                                                         ScalePreviewOptionalValue(GlowRadius, scale), EdgeSoftness, OutwardDiffusion)
                    Using snippet As Bitmap = CreatePreviewSnippet(frame, snippetBounds.Size, scale, bulbCount)
                        graphics.DrawImageUnscaled(snippet, snippetBounds.Location)
                    End Using
                End Using
            End If
        End Using
        SetPreviewSource(canvas)
    End Sub

    Private Function PreviewRenderSize() As Size
        Dim sourceWidth As Integer = If(previewBackground Is Nothing, Math.Max(1, outerBounds.Width), previewBackground.Width)
        Dim sourceHeight As Integer = If(previewBackground Is Nothing, Math.Max(1, outerBounds.Height), previewBackground.Height)
        Dim availableWidth As Integer = Math.Max(320, preview.ClientSize.Width)
        Dim availableHeight As Integer = Math.Max(240, preview.ClientSize.Height)
        Dim scale As Double = Math.Min(1.0R, Math.Min(availableWidth / CDbl(sourceWidth), availableHeight / CDbl(sourceHeight)))
        Return New Size(Math.Max(1, CInt(Math.Round(sourceWidth * scale))),
                        Math.Max(1, CInt(Math.Round(sourceHeight * scale))))
    End Function

    Private Function PreviewSnippetBounds(ByVal renderSize As Size) As Rectangle
        If previewBackground Is Nothing Then Return New Rectangle(Point.Empty, renderSize)

        Dim scaleX As Double = renderSize.Width / CDbl(Math.Max(1, previewBackground.Width))
        Dim scaleY As Double = renderSize.Height / CDbl(Math.Max(1, previewBackground.Height))
        Return New Rectangle(CInt(Math.Round(outerBounds.X * scaleX)),
                             CInt(Math.Round(outerBounds.Y * scaleY)),
                             Math.Max(1, CInt(Math.Round(outerBounds.Width * scaleX))),
                             Math.Max(1, CInt(Math.Round(outerBounds.Height * scaleY))))
    End Function

    Private Function PreviewSnippetScale(ByVal snippetSize As Size) As Double
        Return Math.Min(snippetSize.Width / CDbl(Math.Max(1, outerBounds.Width)),
                        snippetSize.Height / CDbl(Math.Max(1, outerBounds.Height)))
    End Function

    Private Shared Function ScalePreviewValue(ByVal value As Integer, ByVal scale As Double) As Integer
        Return Math.Max(1, CInt(Math.Round(value * scale)))
    End Function

    Private Shared Function ScalePreviewOptionalValue(ByVal value As Integer, ByVal scale As Double) As Integer
        If value <= 0 Then Return 0
        Return Math.Max(1, CInt(Math.Round(value * scale)))
    End Function

    Private Function CreatePreviewSnippet(ByVal activeFrame As Bitmap,
                                          ByVal snippetSize As Size,
                                          ByVal scale As Double,
                                          ByVal bulbCount As Integer) As Bitmap
        Dim combined As New Bitmap(snippetSize.Width, snippetSize.Height, PixelFormat.Format32bppArgb)
        Using graphics As Graphics = Graphics.FromImage(combined)
            graphics.Clear(Color.Transparent)
            If ShowAllBulbs Then graphics.DrawImageUnscaled(GetPreviewStaticLayer(snippetSize, scale, bulbCount), Point.Empty)
            If activeFrame IsNot Nothing Then graphics.DrawImageUnscaled(activeFrame, Point.Empty)
        End Using
        If String.IsNullOrEmpty(infinitySettings.SelectionMaskData) Then Return combined

        Dim masked As Bitmap = Illumination.Lights.CreateSelectionMaskedSnippet(combined,
                                                                                infinitySettings.SelectionMaskData,
                                                                                outerBounds,
                                                                                True,
                                                                                MaskRadius,
                                                                                MaskSmooth,
                                                                                MaskFeather,
                                                                                MaskShiftEdge)
        If masked Is Nothing Then Return combined
        combined.Dispose()
        Return masked
    End Function

    Private Function GetPreviewStaticLayer(ByVal renderSize As Size,
                                           ByVal scale As Double,
                                           ByVal bulbCount As Integer) As Bitmap
        Dim scaledInset As Integer = ScalePreviewValue(DepthInset, scale)
        Dim scaledBulbSize As Integer = ScalePreviewValue(BulbSize, scale)
        Dim key As String = String.Join("|", renderSize.Width, renderSize.Height, Depth, scaledInset, bulbCount, scaledBulbSize)
        If previewStaticLayer Is Nothing OrElse Not String.Equals(previewStaticLayerKey, key, StringComparison.Ordinal) Then
            If previewStaticLayer IsNot Nothing Then previewStaticLayer.Dispose()
            previewStaticLayer = InfinityLightGenerator.CreateStaticBulbLayer(renderSize, Depth, scaledInset, bulbCount, scaledBulbSize)
            previewStaticLayerKey = key
        End If
        Return previewStaticLayer
    End Function

    Private Sub SetPreviewSource(ByVal image As Bitmap)
        If previewSource IsNot Nothing AndAlso Not Object.ReferenceEquals(previewSource, image) Then previewSource.Dispose()
        previewSource = image
        RefreshRenderedPreview()
    End Sub

    Private Sub RefreshRenderedPreview()
        Dim rendered As Bitmap = RenderPreviewCanvas(previewSource, preview.ClientSize)
        Dim oldImage As Image = preview.Image
        preview.Image = rendered
        If oldImage IsNot Nothing AndAlso Not Object.ReferenceEquals(oldImage, rendered) Then oldImage.Dispose()
        previewZoomLabel.Text = If(previewZoomPercent <= 0, "Fit", previewZoomPercent.ToString() & "%")
    End Sub

    Private Function RenderPreviewCanvas(ByVal source As Image, ByVal canvasSize As Size) As Bitmap
        If source Is Nothing OrElse canvasSize.Width <= 0 OrElse canvasSize.Height <= 0 Then Return Nothing
        Dim scale As Double = If(previewZoomPercent <= 0,
                                 Math.Min(canvasSize.Width / CDbl(source.Width), canvasSize.Height / CDbl(source.Height)),
                                 previewZoomPercent / 100.0R)
        scale = Math.Max(0.05R, Math.Min(8.0R, scale))
        Dim drawWidth As Integer = Math.Max(1, CInt(Math.Round(source.Width * scale)))
        Dim drawHeight As Integer = Math.Max(1, CInt(Math.Round(source.Height * scale)))
        Dim output As New Bitmap(canvasSize.Width, canvasSize.Height, PixelFormat.Format32bppArgb)
        Using graphics As Graphics = Graphics.FromImage(output)
            graphics.Clear(Color.FromArgb(5, 6, 9))
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality
            graphics.CompositingQuality = CompositingQuality.HighQuality
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
        If previewSource Is Nothing OrElse (Control.ModifierKeys And Keys.Control) <> 0 Then Return
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

    Private Sub Preview_Resize(ByVal sender As Object, ByVal e As EventArgs)
        RefreshRenderedPreview()
    End Sub

    Private Sub EndPreviewPan()
        previewIsPanning = False
        preview.Capture = False
        preview.Cursor = Cursors.Default
    End Sub

    Private Function GetFitZoomPercent() As Integer
        If previewSource Is Nothing OrElse preview.ClientSize.Width <= 0 OrElse preview.ClientSize.Height <= 0 Then Return 100
        Dim scale As Double = Math.Min(preview.ClientSize.Width / CDbl(previewSource.Width), preview.ClientSize.Height / CDbl(previewSource.Height))
        Return Math.Max(10, Math.Min(800, CInt(Math.Round(scale * 100.0R))))
    End Function
End Class

Friend NotInheritable Class InfinityLightGenerator
    Private Shared ReadOnly bulbSource As Bitmap = LoadBulbSource()
    Private Const MaxBulbCacheEntries As Integer = 128
    Private Shared ReadOnly bulbCache As New Dictionary(Of String, Bitmap)(StringComparer.Ordinal)
    Private Shared ReadOnly bulbCacheOrder As New Queue(Of String)()
    Private Shared ReadOnly bulbCacheSync As New Object()

    Private Sub New()
    End Sub

    Private Shared Function LoadBulbSource() As Bitmap
        Using stream As IO.Stream = GetType(InfinityLightGenerator).Assembly.GetManifestResourceStream("B2SPro.InfinityBulb.png")
            If stream Is Nothing Then Throw New InvalidOperationException("The embedded infinity bulb artwork is missing.")
            Using embeddedImage As New Bitmap(stream)
                Return New Bitmap(embeddedImage)
            End Using
        End Using
    End Function

    Friend Shared Function PerimeterBulbCount(ByVal bounds As Rectangle, ByVal spacing As Integer) As Integer
        spacing = Math.Max(4, spacing)
        Return Math.Max(8, CInt(Math.Round((bounds.Width * 2.0R + bounds.Height * 2.0R) / spacing)))
    End Function

    Friend Shared Function PerimeterPoints(ByVal bounds As Rectangle, ByVal bulbCount As Integer) As List(Of PointF)
        Dim result As New List(Of PointF)()
        If bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return result
        bulbCount = Math.Max(4, bulbCount)
        Dim perimeter As Double = bounds.Width * 2.0R + bounds.Height * 2.0R
        For index As Integer = 0 To bulbCount - 1
            Dim distance As Double = perimeter * index / bulbCount
            If distance < bounds.Width Then
                result.Add(New PointF(CSng(bounds.Left + distance), bounds.Top))
            ElseIf distance < bounds.Width + bounds.Height Then
                result.Add(New PointF(bounds.Right, CSng(bounds.Top + distance - bounds.Width)))
            ElseIf distance < bounds.Width * 2.0R + bounds.Height Then
                result.Add(New PointF(CSng(bounds.Right - (distance - bounds.Width - bounds.Height)), bounds.Bottom))
            Else
                result.Add(New PointF(bounds.Left, CSng(bounds.Bottom - (distance - bounds.Width * 2.0R - bounds.Height))))
            End If
        Next
        Return result
    End Function

    Friend Shared Function CreateBulbRowImage(ByVal canvasSize As Size,
                                               ByVal ringBounds As Rectangle,
                                               ByVal bulbCount As Integer,
                                               ByVal diameter As Integer,
                                               ByVal kelvin As Integer,
                                               ByVal depthOpacity As Single) As Bitmap
        Dim row As New Bitmap(canvasSize.Width, canvasSize.Height, PixelFormat.Format32bppArgb)
        Using graphics As Graphics = Graphics.FromImage(row),
              bulb As Bitmap = CreateBulbImage(diameter, kelvin, Color.White, depthOpacity, 800)
            graphics.Clear(Color.Transparent)
            graphics.SmoothingMode = SmoothingMode.AntiAlias
            For Each point As PointF In PerimeterPoints(ringBounds, bulbCount)
                graphics.DrawImage(bulb, point.X - diameter / 2.0F, point.Y - diameter / 2.0F, diameter, diameter)
            Next
        End Using
        Return row
    End Function

    Friend Shared Function CreateStaticBulbLayer(ByVal canvasSize As Size,
                                                  ByVal depth As Integer,
                                                  ByVal depthInset As Integer,
                                                  ByVal bulbCount As Integer,
                                                  ByVal bulbSize As Integer) As Bitmap
        Dim layer As New Bitmap(canvasSize.Width, canvasSize.Height, PixelFormat.Format32bppArgb)
        Using graphics As Graphics = Graphics.FromImage(layer)
            graphics.Clear(Color.Transparent)
            graphics.SmoothingMode = SmoothingMode.AntiAlias
            graphics.CompositingQuality = CompositingQuality.HighQuality
            For ring As Integer = depth - 1 To 0 Step -1
                Dim inset As Integer = ring * depthInset
                Dim ringBounds As New Rectangle(inset, inset, canvasSize.Width - inset * 2, canvasSize.Height - inset * 2)
                If ringBounds.Width <= bulbSize OrElse ringBounds.Height <= bulbSize Then Continue For
                Dim diameter As Integer = Math.Max(5, CInt(Math.Round(bulbSize * Math.Pow(0.88, ring))))
                Dim depthOpacity As Single = CSng(Math.Pow(0.78, ring)) * 0.42F
                Using bulb As Bitmap = CreateBulbImage(diameter, 4200, Color.White, depthOpacity, 420, 0)
                    For Each point As PointF In PerimeterPoints(ringBounds, bulbCount)
                        graphics.DrawImageUnscaled(bulb, CInt(Math.Round(point.X - diameter / 2.0F)), CInt(Math.Round(point.Y - diameter / 2.0F)))
                    Next
                End Using
            Next
        End Using
        Return layer
    End Function

    Friend Shared Function EffectFrameCount(ByVal effect As InfinityAnimationEffect,
                                            ByVal depth As Integer,
                                            Optional ByVal bulbCount As Integer = 24) As Integer
        Select Case effect
            Case InfinityAnimationEffect.Inward, InfinityAnimationEffect.Outward
                Return Math.Max(1, depth)
            Case InfinityAnimationEffect.Bounce
                Return Math.Max(2, depth * 2 - 2)
            Case InfinityAnimationEffect.Pulse
                Return 12
            Case InfinityAnimationEffect.Strobe
                Return 2
            Case Else
                ' Clockwise and counterclockwise phases select a physical bulb
                ' position. More phases than positions only duplicate identical
                ' full-size bitmaps and hold the same position for extra ticks.
                Return Math.Max(1, Math.Min(24, bulbCount))
        End Select
    End Function

    Friend Shared Function VisualDigest(ByVal bitmap As Bitmap) As String
        If bitmap Is Nothing Then Return String.Empty
        Dim bounds As New Rectangle(Point.Empty, bitmap.Size)
        Dim data As BitmapData = bitmap.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb)
        Try
            Dim row(bitmap.Width * 4 - 1) As Byte
            Using hash As Security.Cryptography.SHA256 = Security.Cryptography.SHA256.Create()
                For y As Integer = 0 To bitmap.Height - 1
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length)
                    hash.TransformBlock(row, 0, row.Length, row, 0)
                Next
                hash.TransformFinalBlock(New Byte() {}, 0, 0)
                Return Convert.ToBase64String(hash.Hash)
            End Using
        Finally
            bitmap.UnlockBits(data)
        End Try
    End Function

    Friend Shared Function GreatestCommonDivisor(ByVal left As Integer, ByVal right As Integer) As Integer
        left = Math.Abs(left)
        right = Math.Abs(right)
        While right <> 0
            Dim remainder As Integer = left Mod right
            left = right
            right = remainder
        End While
        Return Math.Max(10, left)
    End Function

    Friend Shared Function CreateAnimatedFrameImage(ByVal canvasSize As Size,
                                                     ByVal depth As Integer,
                                                     ByVal depthInset As Integer,
                                                     ByVal bulbCount As Integer,
                                                     ByVal bulbSize As Integer,
                                                     ByVal kelvin As Integer, ByVal tint As Color,
                                                     ByVal effect As InfinityAnimationEffect,
                                                     ByVal phase As Integer,
                                                     ByVal phaseCount As Integer,
                                                     ByVal rowsLit As Integer,
                                                     Optional ByVal evenlySpacedLines As Boolean = True,
                                                     Optional ByVal brightness As Integer = 800,
                                                     Optional ByVal colorStrength As Integer = 60,
                                                      Optional ByVal showAllBulbs As Boolean = True,
                                                      Optional ByVal glowRadius As Integer = 0,
                                                      Optional ByVal edgeSoftness As Integer = 0,
                                                      Optional ByVal outwardDiffusion As Integer = 0) As Bitmap
        Dim frame As New Bitmap(canvasSize.Width, canvasSize.Height, PixelFormat.Format32bppArgb)
        Using graphics As Graphics = Graphics.FromImage(frame)
            graphics.Clear(Color.Transparent)
            graphics.SmoothingMode = SmoothingMode.AntiAlias
            Dim activeIndex As Integer = 0
            If effect = InfinityAnimationEffect.Clockwise OrElse effect = InfinityAnimationEffect.Counterclockwise Then
                Dim travelPhase As Integer = If(effect = InfinityAnimationEffect.Clockwise, phase, phaseCount - 1 - phase)
                activeIndex = CInt(Math.Floor(CDbl(travelPhase) * bulbCount / Math.Max(1, phaseCount))) Mod bulbCount
            End If
            Dim activeRing As Integer = 0
            Select Case effect
                Case InfinityAnimationEffect.Inward
                    activeRing = phase Mod Math.Max(1, depth)
                Case InfinityAnimationEffect.Outward
                    activeRing = depth - 1 - (phase Mod Math.Max(1, depth))
                Case InfinityAnimationEffect.Bounce
                    Dim bounceLength As Integer = Math.Max(1, depth * 2 - 2)
                    Dim bouncePhase As Integer = phase Mod bounceLength
                    activeRing = If(bouncePhase < depth, bouncePhase, bounceLength - bouncePhase)
            End Select
            rowsLit = Math.Max(1, rowsLit)
            Dim activeRingCount As Integer = rowsLit
            If effect = InfinityAnimationEffect.Inward OrElse
               effect = InfinityAnimationEffect.Outward OrElse
               effect = InfinityAnimationEffect.Bounce Then
                ' Ring-travel effects need at least one unlit ring or every
                ' generated phase is visually identical when rowsLit >= depth.
                activeRingCount = Math.Min(rowsLit, Math.Max(1, depth - 1))
            End If
            For ring As Integer = 0 To depth - 1
                Dim inset As Integer = ring * depthInset
                Dim ringBounds As New Rectangle(inset, inset, canvasSize.Width - inset * 2, canvasSize.Height - inset * 2)
                If ringBounds.Width <= bulbSize OrElse ringBounds.Height <= bulbSize Then Exit For
                Dim diameter As Integer = Math.Max(5, CInt(Math.Round(bulbSize * Math.Pow(0.88, ring))))
                Dim depthOpacity As Single = CSng(Math.Pow(0.78, ring))
                Dim points As List(Of PointF) = PerimeterPoints(ringBounds, bulbCount)
                Dim pulseLevel As Single = 1.0F
                If effect = InfinityAnimationEffect.Pulse Then
                    pulseLevel = 0.2F + 0.8F * CSng((Math.Sin((CDbl(phase) / Math.Max(1, phaseCount)) * Math.PI * 2.0R - Math.PI / 2.0R) + 1.0R) / 2.0R)
                End If
                Using dimBulb As Bitmap = CreateBulbImage(diameter, 6500, Color.White, depthOpacity * 0.48F),
                      brightBulb As Bitmap = CreateBulbImage(diameter, kelvin, tint, depthOpacity * pulseLevel, brightness, colorStrength)
                    If showAllBulbs Then
                        For Each point As PointF In points
                            graphics.DrawImage(dimBulb, point.X - diameter / 2.0F, point.Y - diameter / 2.0F, diameter, diameter)
                        Next
                    End If
                    Dim rowIsActive As Boolean
                    If effect = InfinityAnimationEffect.Clockwise OrElse effect = InfinityAnimationEffect.Counterclockwise Then
                        rowIsActive = True
                    ElseIf effect = InfinityAnimationEffect.Pulse Then
                        rowIsActive = ring < rowsLit
                    ElseIf effect = InfinityAnimationEffect.Strobe Then
                        rowIsActive = (phase Mod 2) = 0
                    Else
                        Dim relativeRing As Integer = (ring - activeRing + depth) Mod depth
                        rowIsActive = relativeRing < activeRingCount
                    End If
                    If rowIsActive Then
                        If effect = InfinityAnimationEffect.Clockwise OrElse effect = InfinityAnimationEffect.Counterclockwise Then
                            For line As Integer = 0 To rowsLit - 1
                                Dim offset As Integer = If(evenlySpacedLines,
                                                           CInt(Math.Round(CDbl(line) * points.Count / rowsLit)),
                                                           line)
                                Dim index As Integer = (activeIndex + offset) Mod points.Count
                                Dim point As PointF = points(index)
                                graphics.DrawImage(brightBulb, point.X - diameter / 2.0F, point.Y - diameter / 2.0F, diameter, diameter)
                            Next
                        Else
                            For Each point As PointF In points
                                graphics.DrawImage(brightBulb, point.X - diameter / 2.0F, point.Y - diameter / 2.0F, diameter, diameter)
                            Next
                        End If
                    End If
                End Using
            Next
        End Using
        If glowRadius > 0 OrElse edgeSoftness > 0 OrElse outwardDiffusion > 0 Then
            Dim enhanced As Bitmap = Illumination.ArtworkFlasherRenderer.ApplyTransparentGlow(frame, glowRadius, edgeSoftness, outwardDiffusion)
            frame.Dispose()
            Return enhanced
        End If
        Return frame
    End Function

    Friend Shared Function CreateBulbImage(ByVal diameter As Integer, ByVal kelvin As Integer, ByVal tint As Color, ByVal depthOpacity As Single,
                                           Optional ByVal brightness As Integer = 800,
                                           Optional ByVal colorStrength As Integer = 60) As Bitmap
        diameter = Math.Max(5, diameter)
        depthOpacity = Math.Max(0.0F, Math.Min(1.0F, depthOpacity))
        brightness = Math.Max(0, Math.Min(1600, brightness))
        colorStrength = Math.Max(0, Math.Min(100, colorStrength))
        Dim opacityKey As Integer = CInt(Math.Round(depthOpacity * 255.0F))
        Dim cacheKey As String = String.Join("|", diameter, kelvin, tint.ToArgb(), opacityKey, brightness, colorStrength)
        SyncLock bulbCacheSync
            Dim cached As Bitmap = Nothing
            If bulbCache.TryGetValue(cacheKey, cached) Then Return New Bitmap(cached)
        End SyncLock

        Dim image As Bitmap = CreateBulbSourceImage(diameter)
        Dim calculatedKelvinColor As Color = KelvinColor(kelvin)
        ApplyBulbLighting(image, calculatedKelvinColor, tint, depthOpacity, brightness, colorStrength)
        SyncLock bulbCacheSync
            If Not bulbCache.ContainsKey(cacheKey) Then
                bulbCache.Add(cacheKey, New Bitmap(image))
                bulbCacheOrder.Enqueue(cacheKey)
                While bulbCacheOrder.Count > MaxBulbCacheEntries
                    Dim expiredKey As String = bulbCacheOrder.Dequeue()
                    Dim expired As Bitmap = Nothing
                    If bulbCache.TryGetValue(expiredKey, expired) Then
                        bulbCache.Remove(expiredKey)
                        expired.Dispose()
                    End If
                End While
            End If
        End SyncLock
        Return image
    End Function

    Private Shared Function CreateBulbSourceImage(ByVal diameter As Integer) As Bitmap
        Dim image As New Bitmap(diameter, diameter, PixelFormat.Format32bppArgb)
        Using graphics As Graphics = Graphics.FromImage(image)
            graphics.Clear(Color.Transparent)
            graphics.SmoothingMode = SmoothingMode.AntiAlias
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality
            graphics.CompositingQuality = CompositingQuality.HighQuality
            DrawBulbHalo(graphics, diameter)
            If diameter >= 32 Then
                Dim margin As Integer = Math.Max(1, CInt(Math.Round(diameter * 0.06R)))
                graphics.DrawImage(bulbSource,
                                   New Rectangle(margin, margin, diameter - margin * 2, diameter - margin * 2),
                                   0, 0, bulbSource.Width, bulbSource.Height,
                                   GraphicsUnit.Pixel)
            Else
                DrawSmallBulb(graphics, diameter)
            End If
        End Using
        Return image
    End Function

    Private Shared Sub DrawBulbHalo(ByVal graphics As Graphics, ByVal diameter As Integer)
        Using path As New GraphicsPath()
            path.AddEllipse(0, 0, diameter - 1, diameter - 1)
            Using halo As New PathGradientBrush(path)
                halo.CenterPoint = New PointF(diameter * 0.5F, diameter * 0.5F)
                halo.CenterColor = Color.FromArgb(150, 255, 245, 214)
                halo.SurroundColors = New Color() {Color.Transparent}
                halo.FocusScales = New PointF(0.32F, 0.32F)
                graphics.FillPath(halo, path)
            End Using
        End Using
    End Sub

    Private Shared Sub DrawSmallBulb(ByVal graphics As Graphics, ByVal diameter As Integer)
        Dim bodyMargin As Single = Math.Max(1.0F, diameter * 0.17F)
        Dim body As New RectangleF(bodyMargin, bodyMargin, diameter - bodyMargin * 2.0F, diameter - bodyMargin * 2.0F)
        Using path As New GraphicsPath()
            path.AddEllipse(body)
            Using glass As New PathGradientBrush(path)
                glass.CenterPoint = New PointF(diameter * 0.47F, diameter * 0.48F)
                glass.CenterColor = Color.FromArgb(245, 255, 250, 225)
                glass.SurroundColors = New Color() {Color.FromArgb(105, 154, 119, 70)}
                glass.FocusScales = New PointF(0.23F, 0.23F)
                graphics.FillPath(glass, path)
            End Using
        End Using
        Using rim As New Pen(Color.FromArgb(185, 255, 246, 216), Math.Max(1.0F, diameter * 0.055F))
            graphics.DrawEllipse(rim, body)
        End Using
        Dim highlightSize As Single = Math.Max(1.0F, diameter * 0.18F)
        Using highlight As New SolidBrush(Color.FromArgb(225, Color.White))
            graphics.FillEllipse(highlight, diameter * 0.30F, diameter * 0.25F, highlightSize, highlightSize)
        End Using
        If diameter >= 14 Then
            Using filament As New Pen(Color.FromArgb(230, 255, 187, 64), Math.Max(1.0F, diameter * 0.055F))
                filament.StartCap = LineCap.Round
                filament.EndCap = LineCap.Round
                graphics.DrawLine(filament, diameter * 0.34F, diameter * 0.56F, diameter * 0.66F, diameter * 0.56F)
            End Using
        End If
    End Sub

    Private Shared Sub ApplyBulbLighting(ByVal image As Bitmap, ByVal temperature As Color, ByVal tint As Color,
                                         ByVal depthOpacity As Single, ByVal brightness As Integer,
                                         ByVal colorStrength As Integer)
        Dim bounds As New Rectangle(0, 0, image.Width, image.Height)
        Dim data As BitmapData = image.LockBits(bounds, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb)
        Try
            Dim stride As Integer = Math.Abs(data.Stride)
            Dim pixels(stride * image.Height - 1) As Byte
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length)
            Dim lowEnergy As Double = Math.Min(1.0R, brightness / 800.0R)
            Dim whiteBlend As Double = Math.Max(0.0R, (brightness - 800) / 800.0R) * 0.88R
            Dim temperatureR As Double = temperature.R / 255.0R
            Dim temperatureG As Double = temperature.G / 255.0R
            Dim temperatureB As Double = temperature.B / 255.0R
            Dim highestTint As Integer = Math.Max(tint.R, Math.Max(tint.G, tint.B))
            Dim lowestTint As Integer = Math.Min(tint.R, Math.Min(tint.G, tint.B))
            Dim tintSaturation As Double = If(highestTint = 0, 0.0R, (highestTint - lowestTint) / CDbl(highestTint))
            Dim tintR As Double = tint.R / 255.0R
            Dim tintG As Double = tint.G / 255.0R
            Dim tintB As Double = tint.B / 255.0R
            For y As Integer = 0 To image.Height - 1
                Dim row As Integer = y * stride
                For x As Integer = 0 To image.Width - 1
                    Dim offset As Integer = row + x * 4
                    Dim alpha As Integer = pixels(offset + 3)
                    If alpha = 0 Then Continue For
                    Dim blue As Double = pixels(offset)
                    Dim green As Double = pixels(offset + 1)
                    Dim red As Double = pixels(offset + 2)
                    Dim luminance As Double = red * 0.299R + green * 0.587R + blue * 0.114R
                    Const temperatureBlend As Double = 0.12R
                    red = red * (1.0R - temperatureBlend) + luminance * temperatureR * temperatureBlend
                    green = green * (1.0R - temperatureBlend) + luminance * temperatureG * temperatureBlend
                    blue = blue * (1.0R - temperatureBlend) + luminance * temperatureB * temperatureBlend
                    Dim requestedColorStrength As Double = Math.Max(0.0R, Math.Min(1.0R, colorStrength / 100.0R))
                    Dim tintStrength As Double = tintSaturation * requestedColorStrength * 0.92R * (1.0R - Math.Min(1.0R, luminance / 255.0R) * 0.42R)
                    Dim peak As Double = Math.Max(red, Math.Max(green, blue))
                    red = (red * (1.0R - tintStrength) + peak * tintR * tintStrength) * lowEnergy
                    green = (green * (1.0R - tintStrength) + peak * tintG * tintStrength) * lowEnergy
                    blue = (blue * (1.0R - tintStrength) + peak * tintB * tintStrength) * lowEnergy
                    If whiteBlend > 0.0R Then
                        red += (255.0R - red) * whiteBlend
                        green += (255.0R - green) * whiteBlend
                        blue += (255.0R - blue) * whiteBlend
                    End If
                    pixels(offset) = CByte(ClampByte(blue))
                    pixels(offset + 1) = CByte(ClampByte(green))
                    pixels(offset + 2) = CByte(ClampByte(red))
                    pixels(offset + 3) = CByte(ClampByte(alpha * depthOpacity))
                Next
            Next
            Marshal.Copy(pixels, 0, data.Scan0, pixels.Length)
        Finally
            image.UnlockBits(data)
        End Try
    End Sub

    Friend Shared Function CreateToolbarIcon() As Bitmap
        Dim icon As New Bitmap(32, 32, PixelFormat.Format32bppArgb)
        Using graphics As Graphics = Graphics.FromImage(icon),
              bulb As Bitmap = CreateBulbImage(22, 3600, Color.White, 1.0F, 800)
            graphics.Clear(Color.Transparent)
            graphics.SmoothingMode = SmoothingMode.AntiAlias
            Using outerRing As New Pen(Color.FromArgb(220, 0, 210, 255), 1.8F),
                  innerRing As New Pen(Color.FromArgb(170, 125, 80, 255), 1.2F)
                graphics.DrawEllipse(outerRing, 1.5F, 1.5F, 28.0F, 28.0F)
                graphics.DrawEllipse(innerRing, 5.0F, 5.0F, 21.0F, 21.0F)
            End Using
            graphics.DrawImage(bulb, 5, 5, 22, 22)
        End Using
        Return icon
    End Function

    Private Shared Function KelvinColor(ByVal kelvin As Integer) As Color
        kelvin = Math.Max(2000, Math.Min(6500, kelvin))
        Dim t As Double = kelvin / 100.0R
        Dim red As Double
        Dim green As Double
        Dim blue As Double
        If t <= 66 Then
            red = 255
            green = 99.4708025861 * Math.Log(t) - 161.1195681661
            blue = If(t <= 19, 0, 138.5177312231 * Math.Log(t - 10) - 305.0447927307)
        Else
            red = 329.698727446 * Math.Pow(t - 60, -0.1332047592)
            green = 288.1221695283 * Math.Pow(t - 60, -0.0755148492)
            blue = 255
        End If
        Return Color.FromArgb(ClampByte(red), ClampByte(green), ClampByte(blue))
    End Function

    Private Shared Function ClampByte(ByVal value As Double) As Integer
        Return CInt(Math.Max(0, Math.Min(255, Math.Round(value))))
    End Function
End Class
