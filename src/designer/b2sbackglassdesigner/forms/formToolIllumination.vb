Imports System

Public Class formToolIllumination

    Public ignoreChange As Boolean = False

    Private btnGlow As Button
    Private lblSelectedType As Label
    Private lblLampPropertiesHeader As Label
    Private illuminationToolTip As ToolTip
    Private ReadOnly numericIntensity As New NumericUpDown()

    Public Enum eIlluminationDataType
        NotDefined = 0
        Name = 1
        ID = 2
        B2SID = 3
        B2SIDType = 4
        B2SValue = 5
        RomID = 11
        RomIDType = 12
        RomInverted = 13
        InitialState = 21
        DualMode = 22
        Intensity = 23
        DodgeColor = 24
        IlluMode = 25
        Illuminationtext = 31
        IlluminationtextFont = 32
        IlluminationtextAlignment = 33
        LightClassification = 34
        ChangeLightColor = 36
        ChangeDodgeColor = 37
        Location = 41
        Size = 42
        ZOrder = 51
        SnippitInfo = 52
    End Enum

    Public Event DataChanged(ByVal sender As Object, ByVal e As IlluminationEventArgs)
    Public Class IlluminationEventArgs
        Inherits EventArgs

        Public TypeOfData As eIlluminationDataType = eIlluminationDataType.NotDefined
        Public Data As Object = Nothing

        Public Sub New(ByVal _typeofdata As eIlluminationDataType)
            TypeOfData = _typeofdata
        End Sub
        Public Sub New(ByVal _typeofdata As eIlluminationDataType, ByVal _data As Object)
            TypeOfData = _typeofdata
            Data = _data
        End Sub
    End Class

    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call.
        MyBase.DefaultLocation = eDefaultLocation.SE
        MyBase.SaveName = Me.Name

        lblSelectedType = New Label With {
            .Name = "lblSelectedIlluminationType",
            .Text = "Selected: None",
            .Left = 12,
            .Top = 10,
            .Height = 30,
            .Width = Math.Max(240, Me.ClientSize.Width - 24),
            .Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Font = New Font("Segoe UI Semibold", 9.5F, FontStyle.Regular),
            .Padding = New Padding(9, 0, 4, 0),
            .BorderStyle = BorderStyle.None
        }
        Me.Controls.Add(lblSelectedType)
        lblSelectedType.BringToFront()

        lblLampPropertiesHeader = New Label With {
            .Name = "lblLampPropertiesHeader",
            .Text = "LAMP PROPERTIES",
            .AutoSize = False,
            .Left = 20,
            .Top = 48,
            .Height = 18,
            .Width = Math.Max(220, Me.ClientSize.Width - 40),
            .Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Font = New Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
            .BackColor = Color.Transparent
        }
        Me.Controls.Add(lblLampPropertiesHeader)
        lblLampPropertiesHeader.BringToFront()

        illuminationToolTip = New ToolTip()

        ' Compact studio-panel proportions used by every rebuilt tool window.
        Me.ClientSize = ScaleLogicalSize(New Size(270, 650))
        Me.MinimumSize = ScaleLogicalSize(New Size(250, 560))
        Me.SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)

        btnGlow = New Button With {.Name = "btnEditLightGlow", .Text = "Light Settings...", .FlatStyle = FlatStyle.Flat, .Height = 25}
        AddHandler btnGlow.Click, AddressOf EditLightGlow_Click
        AddHandler Me.Resize, AddressOf ReflowIlluminationLayout
        AddHandler Me.Shown, AddressOf ReflowIlluminationLayout
        AddHandler Me.DpiChanged, AddressOf ReflowIlluminationLayout
        Me.Controls.Add(btnGlow)

        numericIntensity.Name = "numericIntensity"
        TrackBarNumericLink.Bind(TrackBarIntensity, numericIntensity)
        Me.Controls.Add(numericIntensity)

        ReflowIlluminationLayout(Nothing, EventArgs.Empty)
        btnGlow.BringToFront()
        numericIntensity.BringToFront()

    End Sub


    Private Sub ReflowIlluminationLayout(ByVal sender As Object, ByVal e As EventArgs)
        If btnGlow Is Nothing Then Return

        Dim margin As Integer = ScaleLogical(8)
        Dim labelLeft As Integer = ScaleLogical(14)
        Dim labelWidth As Integer = ScaleLogical(82)
        Dim contentLeft As Integer = ScaleLogical(102)
        Dim contentWidth As Integer = Math.Max(ScaleLogical(112), Me.ClientSize.Width - contentLeft - margin)
        Dim fullWidth As Integer = Math.Max(ScaleLogical(250), Me.ClientSize.Width - (margin * 2))
        Dim halfWidth As Integer = Math.Max(ScaleLogical(50), (contentWidth - ScaleLogical(8)) \ 2)

        lblSelectedType.SetBounds(margin, ScaleLogical(6), fullWidth, ScaleLogical(25))
        lblLampPropertiesHeader.SetBounds(labelLeft, ScaleLogical(36), Math.Max(ScaleLogical(180), Me.ClientSize.Width - ScaleLogical(28)), ScaleLogical(16))

        ' Lamp properties.
        lblName.SetBounds(labelLeft, ScaleLogical(54), labelWidth, ScaleLogical(21))
        txtName.SetBounds(contentLeft, ScaleLogical(53), contentWidth, ScaleLogical(22))
        lblID.SetBounds(labelLeft, ScaleLogical(80), labelWidth, ScaleLogical(21))
        txtID.SetBounds(contentLeft, ScaleLogical(79), contentWidth, ScaleLogical(22))
        lblInitState.SetBounds(labelLeft, ScaleLogical(106), labelWidth, ScaleLogical(21))
        cmbInitState.SetBounds(contentLeft, ScaleLogical(105), contentWidth, ScaleLogical(22))
        lblDualMode.SetBounds(labelLeft, ScaleLogical(132), labelWidth, ScaleLogical(21))
        cmbDualMode.SetBounds(contentLeft, ScaleLogical(131), contentWidth, ScaleLogical(22))
        B2SLine2.SetBounds(margin, ScaleLogical(158), fullWidth, ScaleLogical(16))

        ' ROM and B2S controller rows retain their original alternate-control overlap.
        lblRomID.SetBounds(labelLeft, ScaleLogical(178), labelWidth, ScaleLogical(21))
        txtRomID.SetBounds(contentLeft, ScaleLogical(177), contentWidth, ScaleLogical(22))
        lblB2SIDType.SetBounds(labelLeft, ScaleLogical(178), labelWidth, ScaleLogical(21))
        cmbB2SIDType.SetBounds(contentLeft, ScaleLogical(177), contentWidth, ScaleLogical(22))
        lblRomIDType.SetBounds(labelLeft, ScaleLogical(204), labelWidth, ScaleLogical(21))
        cmbROMIDType.SetBounds(contentLeft, ScaleLogical(203), contentWidth, ScaleLogical(22))
        lblB2SID.SetBounds(labelLeft, ScaleLogical(204), labelWidth, ScaleLogical(21))
        txtB2SID.SetBounds(contentLeft, ScaleLogical(203), contentWidth, ScaleLogical(22))
        chkRomInverted.SetBounds(contentLeft, ScaleLogical(228), contentWidth, ScaleLogical(21))
        lblB2SValue.SetBounds(labelLeft, ScaleLogical(230), labelWidth, ScaleLogical(21))
        txtB2SValue.SetBounds(contentLeft, ScaleLogical(229), contentWidth, ScaleLogical(22))
        B2SLine5.SetBounds(margin, ScaleLogical(254), fullWidth, ScaleLogical(16))

        ' Intensity and color.
        lblIntensity.SetBounds(labelLeft, ScaleLogical(274), labelWidth, ScaleLogical(21))
        TrackBarIntensity.SetBounds(contentLeft, ScaleLogical(268), Math.Max(ScaleLogical(42), contentWidth - ScaleLogical(92)), ScaleLogical(31))
        numericIntensity.SetBounds(contentLeft + contentWidth - ScaleLogical(86), ScaleLogical(272), ScaleLogical(54), ScaleLogical(23))
        btnLightColor.SetBounds(contentLeft + contentWidth - ScaleLogical(28), ScaleLogical(272), ScaleLogical(28), ScaleLogical(23))
        lblDodgeColor.SetBounds(labelLeft, ScaleLogical(300), labelWidth, ScaleLogical(21))
        cmbDodgeColor.SetBounds(contentLeft, ScaleLogical(299), contentWidth, ScaleLogical(22))
        B2SLine3.SetBounds(margin, ScaleLogical(324), fullWidth, ScaleLogical(16))

        ' Illuminated text and font controls.
        lblIlluminationText.SetBounds(labelLeft, ScaleLogical(344), labelWidth, ScaleLogical(21))
        txtIlluminationText.SetBounds(contentLeft, ScaleLogical(343), contentWidth, ScaleLogical(48))
        txtIlluminationText.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right

        rbAlignLeft.SetBounds(labelLeft, ScaleLogical(394), ScaleLogical(28), ScaleLogical(22))
        rbAlignCenter.SetBounds(labelLeft + ScaleLogical(32), ScaleLogical(394), ScaleLogical(28), ScaleLogical(22))
        rbAlignRight.SetBounds(labelLeft + ScaleLogical(64), ScaleLogical(394), ScaleLogical(28), ScaleLogical(22))
        btnFonts.SetBounds(margin, ScaleLogical(420), fullWidth, ScaleLogical(28))
        btnFonts.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right

        B2SLine1.Visible = False
        B2SLine4.SetBounds(margin, ScaleLogical(454), fullWidth, ScaleLogical(16))
        B2SLine4.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right

        ' Location and size rows.
        lblLocation.SetBounds(labelLeft, ScaleLogical(474), labelWidth, ScaleLogical(21))
        txtLocationX.SetBounds(contentLeft, ScaleLogical(473), halfWidth, ScaleLogical(22))
        txtLocationY.SetBounds(contentLeft + halfWidth + ScaleLogical(6), ScaleLogical(473), contentWidth - halfWidth - ScaleLogical(6), ScaleLogical(22))
        lblSize.SetBounds(labelLeft, ScaleLogical(500), labelWidth, ScaleLogical(21))
        txtSizeWidth.SetBounds(contentLeft, ScaleLogical(499), halfWidth, ScaleLogical(22))
        txtSizeHeight.SetBounds(contentLeft + halfWidth + ScaleLogical(6), ScaleLogical(499), contentWidth - halfWidth - ScaleLogical(6), ScaleLogical(22))

        ' Advanced Feather and Contrast controls are intentionally available
        ' only through the Light Settings dialog.
        btnSnippitSettings.SetBounds(margin, ScaleLogical(528), fullWidth, ScaleLogical(29))
        btnSnippitSettings.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        btnGlow.SetBounds(margin, ScaleLogical(563), fullWidth, ScaleLogical(29))
        btnGlow.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        btnGlow.BringToFront()
    End Sub

    Private Function ScaleLogical(ByVal value As Integer) As Integer
        ' This form already uses WinForms font autoscaling. Scaling these layout
        ' values by DeviceDpi a second time makes the tool window and every row
        ' grow far beyond their intended size on a high-DPI display.
        Return value
    End Function

    Private Function ScaleLogicalSize(ByVal value As Size) As Size
        Return value
    End Function


    Public Sub LoadGlowControls(ByVal selected As Illumination.BulbInfo)
        ' Artwork flashers become image-backed objects after Layer via Copy.
        ' They must remain editable even when IsImageSnippit is True.
        Dim hasEditableSelection As Boolean = selected IsNot Nothing AndAlso
                                              Not selected.IsImageSnippit AndAlso
                                              selected.IlluMode <> Illumination.eIlluMode.Flasher
        btnGlow.Enabled = hasEditableSelection
        btnGlow.Visible = (selected Is Nothing OrElse selected.IlluMode <> Illumination.eIlluMode.Flasher)
        btnLightColor.Enabled = hasEditableSelection
        If selected Is Nothing Then
            lblSelectedType.Text = "Selected: None"
            lblIntensity.Text = "Intensity:"
            btnGlow.Text = "Light Settings..."
            illuminationToolTip.SetToolTip(btnLightColor, "Select a light first.")
            Return
        End If

        Dim isArtworkFlasher As Boolean = selected.IlluMode = Illumination.eIlluMode.Flasher
        If isArtworkFlasher Then
            lblSelectedType.Text = "Selected: Flasher"
            lblIntensity.Text = "Flash Intensity:"
            illuminationToolTip.SetToolTip(btnLightColor, "Flash Color")
        Else
            lblSelectedType.Text = "Selected: Light"
            lblIntensity.Text = "Light Intensity:"
            btnGlow.Text = "Light Settings..."
            illuminationToolTip.SetToolTip(btnLightColor, "Light Color")
        End If
    End Sub

    Private Sub EditLightGlow_Click(ByVal sender As Object, ByVal e As EventArgs)
        If Backglass.currentTabPage Is Nothing OrElse Backglass.currentTabPage.Mouse.SelectedBulb Is Nothing Then
            B2SMessageBox.Show(Me, "Select a light first.", "Light Settings", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        Dim selected As Illumination.BulbInfo = Backglass.currentTabPage.Mouse.SelectedBulb
        If selected.IlluMode = Illumination.eIlluMode.Flasher Then Return
        If selected.IsImageSnippit AndAlso selected.IlluMode <> Illumination.eIlluMode.Flasher Then
            B2SMessageBox.Show(Me, "Light glow applies to normal lights, not image snippets.", "Light Settings", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        Dim selectedLights = Backglass.currentTabPage.Mouse.SelectedItems.OfType(Of Illumination.BulbInfo)().Where(Function(item) Not item.IsImageSnippit AndAlso item.IlluMode <> Illumination.eIlluMode.Flasher)
        Using editor As New formLightDiffusionEditor(selected, selectedLights)
            AddHandler editor.PreviewChanged, AddressOf LightGlowPreviewChanged
            editor.ShowDialog(Me)
            RemoveHandler editor.PreviewChanged, AddressOf LightGlowPreviewChanged
        End Using
    End Sub

    Private Sub LightGlowPreviewChanged(ByVal sender As Object, ByVal e As EventArgs)
        If Backglass.currentTabPage Is Nothing Then Return
        Backglass.currentTabPage.RefreshEditorLighting()
        Backglass.currentTabPage.Invalidate()
        LoadGlowControls(Backglass.currentTabPage.Mouse.SelectedBulb)
        RaiseEvent DataChanged(Me, New IlluminationEventArgs(eIlluminationDataType.LightClassification))
    End Sub

    Private _MyFont As Font
    Public Property MyFont() As Font
        Get
            Return _MyFont
        End Get
        Set(ByVal value As Font)
            If _MyFont IsNot Nothing Then _MyFont.Dispose()
            _MyFont = value
            If value IsNot Nothing Then
                btnFonts.Text = value.Name & ", Size=" & value.Size
            Else
                btnFonts.Text = My.Resources.TXT_ChooseFont
            End If
        End Set
    End Property

    Private _IsSnippit As Boolean
    Public Property IsSnippit() As Boolean
        Get
            Return _IsSnippit
        End Get
        Set(ByVal value As Boolean)
            _IsSnippit = value
            btnSnippitSettings.Enabled = value
        End Set
    End Property
    Public Property ZOrder() As Integer = 0
    Public Property SnippitType() As eSnippitType = eSnippitType.StandardImage
    Public Property SnippitBrightness() As Integer = 100
    Public Property SnippitBehindCanvas() As Boolean = False
    Public Property SnippitMechID() As Integer = 0
    Public Property SnippitRotatingSteps() As Integer = 0
    Public Property SnippitRotatingInterval() As Integer = 0
    Public Property SnippitRotatingDirection() As eSnippitRotationDirection = eSnippitRotationDirection.Clockwise
    Public Property SnippitRotatingStopBehaviour() As eSnippitRotationStopBehaviour = eSnippitRotationStopBehaviour.SpinOff
    Public Property AutomaticRotationEnabled() As Boolean = False
    Public Property AutomaticRotationContinuous() As Boolean = True
    Public Property AutomaticRotationTriggerID() As Integer = 0
    Public Property AutomaticRotationTriggerType() As eRomIDType = eRomIDType.Lamp
    Public Property AutomaticRotationSteps() As Integer = 24
    Public Property AutomaticRotationInterval() As Integer = 50
    Public Property AutomaticRotationDirection() As eSnippitRotationDirection = eSnippitRotationDirection.Clockwise
    Public Property AutomaticRotationStopBehaviour() As eSnippitRotationStopBehaviour = eSnippitRotationStopBehaviour.RunAnimationTillEnd

    Private Sub formToolIllumination_Load(sender As Object, e As System.EventArgs) Handles Me.Load
        cmbDodgeColor.SelectionLength = 0
        cmbInitState.SelectionLength = 0
        cmbROMIDType.SelectionLength = 0
        cmbB2SIDType.SelectionLength = 0
    End Sub

    Private Sub Name_TextChanged(sender As System.Object, e As System.EventArgs) Handles txtName.TextChanged
        RaiseEvent DataChanged(Me, New IlluminationEventArgs(eIlluminationDataType.Name, txtName.Text))
    End Sub
    Private Sub ID_TextChanged(sender As System.Object, e As System.EventArgs) Handles txtID.TextChanged
        If Not String.IsNullOrEmpty(txtID.Text) AndAlso Not IsNumeric(txtID.Text) Then txtID.Text = "0"
        RaiseEvent DataChanged(Me, New IlluminationEventArgs(eIlluminationDataType.ID, txtID.Text))
    End Sub
    Private Sub InitState_SelectedIndexChanged(sender As System.Object, e As System.EventArgs) Handles cmbInitState.SelectedIndexChanged
        RaiseEvent DataChanged(Me, New IlluminationEventArgs(eIlluminationDataType.InitialState, cmbInitState.SelectedIndex))
    End Sub
    Private Sub DualMode_SelectedIndexChanged(sender As System.Object, e As System.EventArgs) Handles cmbDualMode.SelectedIndexChanged
        RaiseEvent DataChanged(Me, New IlluminationEventArgs(eIlluminationDataType.DualMode, cmbDualMode.SelectedIndex))
    End Sub

    Private Sub B2SID_TextChanged(sender As System.Object, e As System.EventArgs) Handles txtB2SID.TextChanged
        If Not String.IsNullOrEmpty(txtB2SID.Text) Then
            If (Not IsNumeric(txtB2SID.Text) OrElse txtB2SID.Text = "0") Then txtB2SID.Text = ""
            If Not ignoreChange AndAlso Not String.IsNullOrEmpty(txtB2SID.Text) Then cmbB2SIDType.SelectedIndex = 0
        End If
        RaiseEvent DataChanged(Me, New IlluminationEventArgs(eIlluminationDataType.B2SID, txtB2SID.Text))
    End Sub
    Private Sub B2SIDType_SelectedIndexChanged(sender As Object, e As System.EventArgs) Handles cmbB2SIDType.SelectedIndexChanged
        RaiseEvent DataChanged(Me, New IlluminationEventArgs(eIlluminationDataType.B2SIDType, cmbB2SIDType.SelectedIndex))
        ignoreChange = True
        Select Case cmbB2SIDType.SelectedIndex
            Case 1 : txtB2SID.Text = 25
            Case 2 : txtB2SID.Text = 26
            Case 3 : txtB2SID.Text = 27
            Case 4 : txtB2SID.Text = 28
            Case 5 : txtB2SID.Text = 30
            Case 6 : txtB2SID.Text = 31
            Case 7 : txtB2SID.Text = 32
            Case 8 : txtB2SID.Text = 33
            Case 9 : txtB2SID.Text = 34
            Case 10 : txtB2SID.Text = 35
            Case 11 : txtB2SID.Text = 36
        End Select
        ignoreChange = False
    End Sub
    Private Sub B2SValue_TextChanged(sender As Object, e As System.EventArgs) Handles txtB2SValue.TextChanged
        If Not String.IsNullOrEmpty(txtB2SValue.Text) Then
            If (Not IsNumeric(txtB2SValue.Text) OrElse txtB2SValue.Text = "0") Then txtB2SValue.Text = ""
        End If
        RaiseEvent DataChanged(Me, New IlluminationEventArgs(eIlluminationDataType.B2SValue, txtB2SValue.Text))
    End Sub
    Private Sub RomID_TextChanged(sender As System.Object, e As System.EventArgs) Handles txtRomID.TextChanged
        If Not String.IsNullOrEmpty(txtRomID.Text) Then
            If (Not IsNumeric(txtRomID.Text) OrElse txtRomID.Text = "0") Then txtRomID.Text = ""
            If Not String.IsNullOrEmpty(txtRomID.Text) AndAlso cmbROMIDType.SelectedIndex = 0 Then cmbROMIDType.SelectedIndex = 1
        End If
        RaiseEvent DataChanged(Me, New IlluminationEventArgs(eIlluminationDataType.RomID, txtRomID.Text))
    End Sub
    Private Sub RomIDType_SelectedIndexChanged(sender As System.Object, e As System.EventArgs) Handles cmbROMIDType.SelectedIndexChanged
        RaiseEvent DataChanged(Me, New IlluminationEventArgs(eIlluminationDataType.RomIDType, cmbROMIDType.SelectedIndex))
    End Sub
    Private Sub RomInverted_CheckedChanged(sender As System.Object, e As System.EventArgs) Handles chkRomInverted.CheckedChanged
        RaiseEvent DataChanged(Me, New IlluminationEventArgs(eIlluminationDataType.RomInverted, chkRomInverted.Checked))
    End Sub

    Private Sub Intensity_Scroll(sender As System.Object, e As System.EventArgs) Handles TrackBarIntensity.ValueChanged
        If ignoreChange Then Return
        RaiseEvent DataChanged(Me, New IlluminationEventArgs(eIlluminationDataType.Intensity, TrackBarIntensity.Value))
    End Sub
    Private Sub LightColor_Click(sender As System.Object, e As System.EventArgs) Handles btnLightColor.Click
        RaiseEvent DataChanged(Me, New IlluminationEventArgs(eIlluminationDataType.ChangeLightColor))
    End Sub
    Private Sub DodgeColor_SelectedIndexChanged(sender As System.Object, e As System.EventArgs) Handles cmbDodgeColor.SelectedIndexChanged
        RaiseEvent DataChanged(Me, New IlluminationEventArgs(eIlluminationDataType.DodgeColor, TranslateIndex2DodgeColor(cmbDodgeColor.SelectedIndex)))
    End Sub
    Private Sub IlluMode_SelectedIndexChanged(sender As System.Object, e As System.EventArgs) Handles cmbIlluMode.SelectedIndexChanged
        RaiseEvent DataChanged(Me, New IlluminationEventArgs(eIlluminationDataType.IlluMode, cmbIlluMode.SelectedIndex))
        If Backglass.currentTabPage IsNot Nothing AndAlso Backglass.currentTabPage.Mouse.SelectedBulb IsNot Nothing Then
            LoadGlowControls(Backglass.currentTabPage.Mouse.SelectedBulb)
        End If
    End Sub

    Private Sub IlluminationText_TextChanged(sender As Object, e As System.EventArgs) Handles txtIlluminationText.TextChanged
        RaiseEvent DataChanged(Me, New IlluminationEventArgs(eIlluminationDataType.Illuminationtext, txtIlluminationText.Text))
    End Sub
    Private Sub AlignLeft_CheckedChanged(sender As System.Object, e As System.EventArgs) Handles rbAlignLeft.CheckedChanged
        RaiseEvent DataChanged(Me, New IlluminationEventArgs(eIlluminationDataType.IlluminationtextAlignment, Illumination.eTextAlignment.Left))
    End Sub
    Private Sub AlignCenter_CheckedChanged(sender As System.Object, e As System.EventArgs) Handles rbAlignCenter.CheckedChanged
        RaiseEvent DataChanged(Me, New IlluminationEventArgs(eIlluminationDataType.IlluminationtextAlignment, Illumination.eTextAlignment.Center))
    End Sub
    Private Sub AlignRight_CheckedChanged(sender As System.Object, e As System.EventArgs) Handles rbAlignRight.CheckedChanged
        RaiseEvent DataChanged(Me, New IlluminationEventArgs(eIlluminationDataType.IlluminationtextAlignment, Illumination.eTextAlignment.Right))
    End Sub
    Private Sub Fonts_Click(sender As System.Object, e As System.EventArgs) Handles btnFonts.Click
        Dim selectedFont As Font = ShowB2SFontDialog(_MyFont)
        If selectedFont Is Nothing Then Return
        MyFont = selectedFont
        RaiseEvent DataChanged(Me, New IlluminationEventArgs(eIlluminationDataType.IlluminationtextFont, selectedFont))
    End Sub

    Private Function ShowB2SFontDialog(ByVal currentFont As Font) As Font
        Dim initialFont As Font = If(currentFont, New Font("Arial", 10.0F, FontStyle.Regular, GraphicsUnit.Point))
        Using dialog As New Form()
            dialog.Text = "Illuminated Text Font"
            dialog.StartPosition = FormStartPosition.CenterParent
            dialog.FormBorderStyle = FormBorderStyle.FixedDialog
            dialog.ClientSize = New Size(490, 350)
            dialog.MinimizeBox = False
            dialog.MaximizeBox = False
            dialog.ShowInTaskbar = False

            Dim fontLabel As New Label With {.Text = "FONT", .Left = 16, .Top = 14, .Width = 270, .Height = 18}
            Dim fontList As New ListBox With {.Left = 16, .Top = 34, .Width = 280, .Height = 230, .Sorted = True, .IntegralHeight = False}
            For Each family As FontFamily In FontFamily.Families
                fontList.Items.Add(family.Name)
            Next

            Dim styleLabel As New Label With {.Text = "FONT STYLE", .Left = 312, .Top = 14, .Width = 160, .Height = 18}
            Dim styleBox As New ComboBox With {.Left = 312, .Top = 34, .Width = 160, .DropDownStyle = ComboBoxStyle.DropDownList}
            styleBox.Items.AddRange(New Object() {"Regular", "Bold", "Italic", "Bold Italic"})

            Dim sizeLabel As New Label With {.Text = "SIZE", .Left = 312, .Top = 76, .Width = 160, .Height = 18}
            Dim sizeBox As New ComboBox With {.Left = 312, .Top = 96, .Width = 160, .DropDownStyle = ComboBoxStyle.DropDown, .MaxDropDownItems = 12}
            sizeBox.Items.AddRange(New Object() {"6", "8", "9", "10", "11", "12", "14", "16", "18", "20", "22", "24", "26", "28", "30", "32", "36", "40", "48", "60", "72", "96"})
            sizeBox.Text = initialFont.SizeInPoints.ToString("0.##")
            Dim underlineBox As New CheckBox With {.Text = "Underline", .Left = 312, .Top = 132, .Width = 82, .Checked = initialFont.Underline}
            Dim strikeoutBox As New CheckBox With {.Text = "Strikeout", .Left = 398, .Top = 132, .Width = 74, .Checked = initialFont.Strikeout}
            Dim sampleLabel As New Label With {.Text = "SAMPLE", .Left = 312, .Top = 166, .Width = 160, .Height = 18}
            Dim sample As New Label With {.Text = If(String.IsNullOrEmpty(txtIlluminationText.Text), "Illuminated Text", txtIlluminationText.Text), .Left = 312, .Top = 186, .Width = 160, .Height = 78, .BorderStyle = BorderStyle.FixedSingle, .TextAlign = ContentAlignment.MiddleCenter, .AutoEllipsis = True}

            Dim okButton As New Button With {.Text = "OK", .Left = 292, .Top = 292, .Width = 86, .Height = 32, .DialogResult = DialogResult.OK, .FlatStyle = FlatStyle.Flat}
            Dim cancelButton As New Button With {.Text = "Cancel", .Left = 386, .Top = 292, .Width = 86, .Height = 32, .DialogResult = DialogResult.Cancel, .FlatStyle = FlatStyle.Flat}
            dialog.Controls.AddRange(New Control() {fontLabel, fontList, styleLabel, styleBox, sizeLabel, sizeBox, underlineBox, strikeoutBox, sampleLabel, sample, okButton, cancelButton})
            dialog.AcceptButton = okButton
            dialog.CancelButton = cancelButton

            Dim initialIndex As Integer = fontList.FindStringExact(initialFont.Name)
            If initialIndex < 0 Then initialIndex = fontList.FindStringExact(initialFont.FontFamily.Name)
            If initialIndex >= 0 Then fontList.SelectedIndex = initialIndex
            Dim baseStyle As FontStyle = initialFont.Style And (FontStyle.Bold Or FontStyle.Italic)
            styleBox.SelectedIndex = If(baseStyle = (FontStyle.Bold Or FontStyle.Italic), 3, If(baseStyle = FontStyle.Bold, 1, If(baseStyle = FontStyle.Italic, 2, 0)))

            Dim samplePreviewFont As Font = Nothing
            Dim updateSample As Action =
                Sub()
                    If fontList.SelectedItem Is Nothing OrElse styleBox.SelectedIndex < 0 Then Return
                    Dim selectedSize As Single
                    If Not Single.TryParse(sizeBox.Text, selectedSize) OrElse selectedSize < 1.0F OrElse selectedSize > 500.0F Then
                        okButton.Enabled = False
                        Return
                    End If
                    Dim style As FontStyle = If(styleBox.SelectedIndex = 1, FontStyle.Bold, If(styleBox.SelectedIndex = 2, FontStyle.Italic, If(styleBox.SelectedIndex = 3, FontStyle.Bold Or FontStyle.Italic, FontStyle.Regular)))
                    If underlineBox.Checked Then style = style Or FontStyle.Underline
                    If strikeoutBox.Checked Then style = style Or FontStyle.Strikeout
                    Try
                        Dim previewFont As New Font(CStr(fontList.SelectedItem), selectedSize, style, GraphicsUnit.Point)
                        If samplePreviewFont IsNot Nothing Then samplePreviewFont.Dispose()
                        samplePreviewFont = previewFont
                        sample.Font = samplePreviewFont
                        okButton.Enabled = True
                    Catch
                        okButton.Enabled = False
                    End Try
                End Sub
            AddHandler fontList.SelectedIndexChanged, Sub() updateSample()
            AddHandler styleBox.SelectedIndexChanged, Sub() updateSample()
            AddHandler sizeBox.SelectedIndexChanged, Sub() updateSample()
            AddHandler sizeBox.TextChanged, Sub() updateSample()
            AddHandler underlineBox.CheckedChanged, Sub() updateSample()
            AddHandler strikeoutBox.CheckedChanged, Sub() updateSample()
            updateSample()

            AppThemeManager.ApplyB2SProDialog(dialog)
            Dim fontDialogResult As DialogResult = dialog.ShowDialog(Me)
            If samplePreviewFont IsNot Nothing Then samplePreviewFont.Dispose()
            If fontDialogResult <> DialogResult.OK OrElse fontList.SelectedItem Is Nothing Then Return Nothing

            Dim finalSize As Single
            If Not Single.TryParse(sizeBox.Text, finalSize) Then Return Nothing
            Dim selectedStyle As FontStyle = If(styleBox.SelectedIndex = 1, FontStyle.Bold, If(styleBox.SelectedIndex = 2, FontStyle.Italic, If(styleBox.SelectedIndex = 3, FontStyle.Bold Or FontStyle.Italic, FontStyle.Regular)))
            If underlineBox.Checked Then selectedStyle = selectedStyle Or FontStyle.Underline
            If strikeoutBox.Checked Then selectedStyle = selectedStyle Or FontStyle.Strikeout
            Return New Font(CStr(fontList.SelectedItem), finalSize, selectedStyle, GraphicsUnit.Point)
        End Using
    End Function

    Private Sub LocationX_TextChanged(sender As System.Object, e As System.EventArgs) Handles txtLocationX.TextChanged
        If ignoreChange Then Return
        If Not IsNumeric(txtLocationX.Text) Then txtLocationX.Text = 0
        If Not IsNumeric(txtLocationY.Text) Then Return
        RaiseEvent DataChanged(Me, New IlluminationEventArgs(eIlluminationDataType.Location, New Point(CInt(txtLocationX.Text), CInt(txtLocationY.Text))))
    End Sub
    Private Sub LocationY_TextChanged(sender As System.Object, e As System.EventArgs) Handles txtLocationY.TextChanged
        If ignoreChange Then Return
        If Not IsNumeric(txtLocationY.Text) Then txtLocationY.Text = 0
        If Not IsNumeric(txtLocationX.Text) Then Return
        RaiseEvent DataChanged(Me, New IlluminationEventArgs(eIlluminationDataType.Location, New Point(CInt(txtLocationX.Text), CInt(txtLocationY.Text))))
    End Sub
    Private Sub SizeWidth_TextChanged(sender As System.Object, e As System.EventArgs) Handles txtSizeWidth.TextChanged
        If ignoreChange Then Return
        If Not IsNumeric(txtSizeWidth.Text) Then txtSizeWidth.Text = 0
        If Not IsNumeric(txtSizeHeight.Text) Then Return
        RaiseEvent DataChanged(Me, New IlluminationEventArgs(eIlluminationDataType.Size, New Size(CInt(txtSizeWidth.Text), CInt(txtSizeHeight.Text))))
    End Sub
    Private Sub SizeHeight_TextChanged(sender As System.Object, e As System.EventArgs) Handles txtSizeHeight.TextChanged
        If ignoreChange Then Return
        If Not IsNumeric(txtSizeHeight.Text) Then txtSizeHeight.Text = 0
        If Not IsNumeric(txtSizeWidth.Text) Then Return
        RaiseEvent DataChanged(Me, New IlluminationEventArgs(eIlluminationDataType.Size, New Size(CInt(txtSizeWidth.Text), CInt(txtSizeHeight.Text))))
    End Sub
    Private Sub Size_Validated(sender As Object, e As System.EventArgs) Handles txtSizeWidth.Validated, txtSizeHeight.Validated
        If Not IsNumeric(txtSizeWidth.Text) Then txtSizeWidth.Text = "10"
        If Not IsNumeric(txtSizeHeight.Text) Then txtSizeHeight.Text = "10"
        If CInt(txtSizeWidth.Text) < 10 Then txtSizeWidth.Text = "10"
        If CInt(txtSizeHeight.Text) < 10 Then txtSizeHeight.Text = "10"
        RaiseEvent DataChanged(Me, New IlluminationEventArgs(eIlluminationDataType.Size, New Size(CInt(txtSizeWidth.Text), CInt(txtSizeHeight.Text))))
    End Sub

    Private Sub SnippitSettings_Click(sender As System.Object, e As System.EventArgs) Handles btnSnippitSettings.Click
        If IsSnippit Then
            Dim formSnippit As formSnippitSettings = New formSnippitSettings()
            If formSnippit.ShowDialog(Me, CInt(txtID.Text), txtName.Text, SnippitType, ZOrder, SnippitBrightness, SnippitBehindCanvas, SnippitMechID, SnippitRotatingSteps, SnippitRotatingInterval, SnippitRotatingDirection, SnippitRotatingStopBehaviour, AutomaticRotationEnabled, AutomaticRotationContinuous, AutomaticRotationTriggerID, AutomaticRotationTriggerType, AutomaticRotationSteps, AutomaticRotationInterval, AutomaticRotationDirection, AutomaticRotationStopBehaviour) = Windows.Forms.DialogResult.OK Then
                Dim snippitinfo As Illumination.SnippitInfo = New Illumination.SnippitInfo()
                snippitinfo.Brightness = SnippitBrightness
                snippitinfo.BehindCanvas = SnippitBehindCanvas
                snippitinfo.SnippitType = SnippitType
                snippitinfo.SnippitMechID = SnippitMechID
                snippitinfo.SnippitRotatingSteps = SnippitRotatingSteps
                snippitinfo.SnippitRotatingInterval = SnippitRotatingInterval
                snippitinfo.SnippitRotatingDirection = SnippitRotatingDirection
                snippitinfo.SnippitRotatingStopBehaviour = SnippitRotatingStopBehaviour
                snippitinfo.AutomaticRotationEnabled = AutomaticRotationEnabled
                snippitinfo.AutomaticRotationContinuous = AutomaticRotationContinuous
                snippitinfo.AutomaticRotationTriggerID = AutomaticRotationTriggerID
                snippitinfo.AutomaticRotationTriggerType = AutomaticRotationTriggerType
                snippitinfo.AutomaticRotationSteps = AutomaticRotationSteps
                snippitinfo.AutomaticRotationInterval = AutomaticRotationInterval
                snippitinfo.AutomaticRotationDirection = AutomaticRotationDirection
                snippitinfo.AutomaticRotationStopBehaviour = AutomaticRotationStopBehaviour
                RaiseEvent DataChanged(Me, New IlluminationEventArgs(eIlluminationDataType.ZOrder, ZOrder))
                RaiseEvent DataChanged(Me, New IlluminationEventArgs(eIlluminationDataType.SnippitInfo, snippitinfo))
            End If
        End If
    End Sub

End Class
