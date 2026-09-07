Imports System

Public Class formSetReelIllumination

    Private IsDirty As Boolean = False
    Private ignoreChanges As Boolean = False

    Private ReadOnly chkReel3D As New CheckBox()
    Private ReadOnly trackReelBrightness As New TrackBar()
    Private ReadOnly trackReelTemperature As New TrackBar()
    Private ReadOnly trackReelDepth As New TrackBar()
    Private ReadOnly trackReelGlass As New TrackBar()
    Private ReadOnly numLegacyIntensity As New NumericUpDown()
    Private ReadOnly numReelBrightness As New NumericUpDown()
    Private ReadOnly numReelTemperature As New NumericUpDown()
    Private ReadOnly numReelDepth As New NumericUpDown()
    Private ReadOnly numReelGlass As New NumericUpDown()

    Public Event LivePreviewChanged(ByVal location As eReelIlluminationLocation,
                                    ByVal intensity As Integer,
                                    ByVal reel3DEnabled As Boolean,
                                    ByVal reel3DBrightness As Integer,
                                    ByVal reel3DTemperature As Integer,
                                    ByVal reel3DDepth As Integer,
                                    ByVal reel3DGlass As Integer)

    Public Sub New()
        InitializeComponent()
        BuildReel3DControls()
    End Sub

    Public Shadows Function ShowDialog(ByVal owner As IWin32Window,
                                       ByRef reelillulocation As eReelIlluminationLocation,
                                       ByRef reelillub2sid As Integer,
                                       ByRef reelillub2sidtype As eB2SIDType,
                                       ByRef reelillub2svalue As Integer,
                                       ByRef reelilluintensity As Integer,
                                       ByRef reel3Denabled As Boolean,
                                       ByRef reel3Dbrightness As Integer,
                                       ByRef reel3Dtemperature As Integer,
                                       ByRef reel3Ddepth As Integer,
                                       ByRef reel3Dglass As Integer) As DialogResult
        ignoreChanges = True
        cmbIlluLocation.SelectedIndex = reelillulocation
        cmbB2SIDType.SelectedIndex = reelillub2sidtype
        If reelillub2sid > 0 Then txtB2SID.Text = reelillub2sid
        If reelillub2svalue > 0 Then txtB2SValue.Text = reelillub2svalue
        TrackBarIntensity.Value = Math.Max(TrackBarIntensity.Minimum, Math.Min(TrackBarIntensity.Maximum, reelilluintensity))
        chkReel3D.Checked = reel3Denabled
        trackReelBrightness.Value = Math.Max(trackReelBrightness.Minimum, Math.Min(trackReelBrightness.Maximum, reel3Dbrightness))
        trackReelTemperature.Value = Math.Max(trackReelTemperature.Minimum, Math.Min(trackReelTemperature.Maximum, reel3Dtemperature))
        trackReelDepth.Value = Math.Max(trackReelDepth.Minimum, Math.Min(trackReelDepth.Maximum, reel3Ddepth))
        trackReelGlass.Value = Math.Max(trackReelGlass.Minimum, Math.Min(trackReelGlass.Maximum, reel3Dglass))
        UpdateIntensityLabel()
        UpdateReel3DEnabledState()
        ignoreChanges = False
        ' now show the dialog
        Dim nRet As DialogResult = MyBase.ShowDialog(owner)
        ' Every control is an auto-save control. Returning the current values
        ' for OK, Close/Escape and the title-bar X prevents a reopened window
        ' from falling back to the values it had before the last edit.
        reelillulocation = CType(Math.Max(0, cmbIlluLocation.SelectedIndex), eReelIlluminationLocation)
        reelillub2sidtype = CType(Math.Max(0, cmbB2SIDType.SelectedIndex), eB2SIDType)
        reelillub2sid = ParseNonNegativeInteger(txtB2SID.Text)
        reelillub2svalue = ParseNonNegativeInteger(txtB2SValue.Text)
        reelilluintensity = TrackBarIntensity.Value
        reel3Denabled = chkReel3D.Checked
        reel3Dbrightness = trackReelBrightness.Value
        reel3Dtemperature = trackReelTemperature.Value
        reel3Ddepth = trackReelDepth.Value
        reel3Dglass = trackReelGlass.Value
        Return nRet
    End Function

    Private Shared Function ParseNonNegativeInteger(ByVal value As String) As Integer
        Dim parsed As Integer
        If Integer.TryParse(value, parsed) AndAlso parsed > 0 Then Return parsed
        Return 0
    End Function

    Private Sub IntensityChanged(sender As Object, e As EventArgs) Handles TrackBarIntensity.ValueChanged
        UpdateIntensityLabel()
        If ignoreChanges Then Return
        IsDirty = True
        RaiseLivePreview()
    End Sub

    Private Sub IlluminationLocationChanged(sender As Object, e As EventArgs) Handles cmbIlluLocation.SelectedIndexChanged
        If ignoreChanges OrElse cmbIlluLocation.SelectedIndex < 0 Then Return
        IsDirty = True
        RaiseLivePreview()
    End Sub

    Private Sub UpdateIntensityLabel()
        If lblIntensity IsNot Nothing Then lblIntensity.Text = "Intensity"
    End Sub

    Private Sub RaiseLivePreview()
        RaiseEvent LivePreviewChanged(CType(Math.Max(0, cmbIlluLocation.SelectedIndex), eReelIlluminationLocation),
                                      TrackBarIntensity.Value,
                                      chkReel3D.Checked,
                                      trackReelBrightness.Value,
                                      trackReelTemperature.Value,
                                      trackReelDepth.Value,
                                      trackReelGlass.Value)
    End Sub

    Private Sub BuildReel3DControls()
        Text = "Reel Lighting & 3D"
        ClientSize = New Size(704, 340)
        MinimumSize = New Size(720, 378)
        grpGeneral.Location = New Point(10, 7)
        grpGeneral.Size = New Size(227, 286)
        TrackBarIntensity.SetBounds(93, 143, 67, 45)
        numLegacyIntensity.SetBounds(160, 149, 55, 24)
        numLegacyIntensity.Name = "numLegacyIntensity"
        TrackBarNumericLink.Bind(TrackBarIntensity, numLegacyIntensity)
        grpGeneral.Controls.Add(numLegacyIntensity)
        numLegacyIntensity.BringToFront()
        btnOk.Location = New Point(508, 303)
        btnCancel.Location = New Point(603, 303)
        btnCancel.Text = "Close"

        Dim group As New GroupBox() With {
            .Text = "3D BACKLIT REELS",
            .Location = New Point(246, 7),
            .Size = New Size(448, 286),
            .Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right Or AnchorStyles.Bottom
        }
        Dim layout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .Padding = New Padding(10, 8, 10, 8),
            .ColumnCount = 3,
            .RowCount = 7
        }
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 145))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 76))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 31))
        For i As Integer = 1 To 4
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 43))
        Next
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 25))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        chkReel3D.Text = "Enable realistic 3D backlighting"
        chkReel3D.AutoSize = True
        chkReel3D.Font = New Font(Font, FontStyle.Bold)
        layout.Controls.Add(chkReel3D, 0, 0)
        layout.SetColumnSpan(chkReel3D, 3)

        ConfigureReelSlider(trackReelBrightness, 0, 400, 100, 25, 5, 25)
        ConfigureReelSlider(trackReelTemperature, 2000, 6500, 4000, 500, 100, 500)
        ConfigureReelSlider(trackReelDepth, 0, 200, 100, 10, 10, 20)
        ConfigureReelSlider(trackReelGlass, 0, 200, 55, 10, 10, 20)
        AddReelSliderRow(layout, 1, "Backlight brightness (%)", trackReelBrightness, numReelBrightness)
        AddReelSliderRow(layout, 2, "Color temperature (K)", trackReelTemperature, numReelTemperature, 100)
        AddReelSliderRow(layout, 3, "3D depth (%)", trackReelDepth, numReelDepth)
        AddReelSliderRow(layout, 4, "Glass reflection (%)", trackReelGlass, numReelGlass)

        Dim neutral As New Label() With {
            .Text = "4000 K is neutral",
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft
        }
        layout.Controls.Add(neutral, 0, 5)
        layout.SetColumnSpan(neutral, 3)
        Dim note As New Label() With {
            .Text = "Changes preview live on the selected score display. Black printed digits stay dark.",
            .Dock = DockStyle.Fill,
            .AutoEllipsis = True
        }
        layout.Controls.Add(note, 0, 6)
        layout.SetColumnSpan(note, 3)

        AddHandler chkReel3D.CheckedChanged, AddressOf Reel3DValueChanged
        AddHandler trackReelBrightness.ValueChanged, AddressOf Reel3DValueChanged
        AddHandler trackReelTemperature.ValueChanged, AddressOf Reel3DValueChanged
        AddHandler trackReelDepth.ValueChanged, AddressOf Reel3DValueChanged
        AddHandler trackReelGlass.ValueChanged, AddressOf Reel3DValueChanged

        group.Controls.Add(layout)
        Controls.Add(group)
        group.BringToFront()
    End Sub

    Private Shared Sub ConfigureReelSlider(ByVal slider As TrackBar,
                                           ByVal minimum As Integer,
                                           ByVal maximum As Integer,
                                           ByVal value As Integer,
                                           ByVal tickFrequency As Integer,
                                           ByVal smallChange As Integer,
                                           ByVal largeChange As Integer)
        slider.Minimum = minimum
        slider.Maximum = maximum
        slider.Value = value
        slider.TickFrequency = tickFrequency
        slider.SmallChange = smallChange
        slider.LargeChange = largeChange
        slider.Dock = DockStyle.Fill
        slider.AutoSize = False
        slider.Height = 38
    End Sub

    Private Shared Sub AddReelSliderRow(ByVal layout As TableLayoutPanel,
                                        ByVal row As Integer,
                                        ByVal caption As String,
                                        ByVal slider As TrackBar,
                                        ByVal number As NumericUpDown,
                                        Optional ByVal increment As Integer = 1)
        Dim captionLabel As New Label() With {
            .Text = caption,
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft
        }
        number.Dock = DockStyle.Fill
        number.Margin = New Padding(4, 7, 0, 8)
        TrackBarNumericLink.Bind(slider, number, increment)
        layout.Controls.Add(captionLabel, 0, row)
        layout.Controls.Add(slider, 1, row)
        layout.Controls.Add(number, 2, row)
    End Sub

    Private Sub Reel3DValueChanged(ByVal sender As Object, ByVal e As EventArgs)
        UpdateReel3DEnabledState()
        If ignoreChanges Then Return
        IsDirty = True
        RaiseLivePreview()
    End Sub

    Private Sub UpdateReel3DEnabledState()
        Dim enabled As Boolean = chkReel3D.Checked
        trackReelBrightness.Enabled = enabled
        trackReelTemperature.Enabled = enabled
        trackReelDepth.Enabled = enabled
        trackReelGlass.Enabled = enabled
    End Sub

    Private Sub formSetLEDColor_Shown(sender As Object, e As System.EventArgs) Handles Me.Shown
        IsDirty = False
    End Sub

    Private Sub txtB2SID_TextChanged(sender As System.Object, e As System.EventArgs) Handles txtB2SID.TextChanged
        If ignoreChanges Then Return
        If Not String.IsNullOrEmpty(txtB2SID.Text) Then cmbB2SIDType.SelectedIndex = 0
    End Sub
    Private Sub B2SIDType_SelectedIndexChanged(sender As Object, e As System.EventArgs) Handles cmbB2SIDType.SelectedIndexChanged
        If ignoreChanges Then Return
        ignoreChanges = True
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
        ignoreChanges = False
    End Sub

    Private Sub Ok_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnOk.Click
        MyBase.DialogResult = Windows.Forms.DialogResult.OK
        Me.Close()
    End Sub
    Private Sub Cancel_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnCancel.Click
        ' This editor saves continuously, so Close never rolls the controls
        ' back or asks the user to save the same settings a second time.
        MyBase.DialogResult = Windows.Forms.DialogResult.OK
        Me.Close()
    End Sub

End Class
