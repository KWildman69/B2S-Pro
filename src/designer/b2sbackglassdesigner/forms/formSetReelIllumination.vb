Imports System

Public Class formSetReelIllumination

    Private IsDirty As Boolean = False
    Private ignoreChanges As Boolean = False

    Private ReadOnly chkReel3D As New CheckBox()
    Private ReadOnly trackReelBrightness As New TrackBar()
    Private ReadOnly trackReelTemperature As New TrackBar()
    Private ReadOnly trackReelDepth As New TrackBar()
    Private ReadOnly trackReelGlass As New TrackBar()
    Private ReadOnly cmbReelStyle As New ComboBox()
    Private ReadOnly trackReelCurve As New TrackBar()
    Private ReadOnly trackReelShadow As New TrackBar()
    Private ReadOnly numReelCurve As New NumericUpDown()
    Private ReadOnly numReelShadow As New NumericUpDown()
    Private ReadOnly numLegacyIntensity As New NumericUpDown()
    Private ReadOnly numReelBrightness As New NumericUpDown()
    Private ReadOnly numReelTemperature As New NumericUpDown()
    Private ReadOnly numReelDepth As New NumericUpDown()
    Private ReadOnly numReelGlass As New NumericUpDown()
    Private ReadOnly cmbGlassStyle As New ComboBox()
    Private ReadOnly chkReelDividers As New CheckBox()
    Private ReadOnly trackReelDividerWidth As New TrackBar()
    Private ReadOnly numReelDividerWidth As New NumericUpDown()
    ' Reel roll time edits the backglass-wide ReelRollingInterval (the same value as
    ' Reel Type > Rolling interval), which the Server, plugin and Test spin already use.
    Private ReadOnly trackReelRollTime As New TrackBar()
    Private ReadOnly numReelRollTime As New NumericUpDown()
    Private Const RollTimeMinimum As Integer = 20
    Private Const RollTimeMaximum As Integer = 60
    Private Const RuntimeDefaultRollingInterval As Integer = 101
    Private ReadOnly btnTestSpin As New Button()
    Private testSpinWindow As ReelTestSpinWindow = Nothing

    ' The score shown in the Test spin window (its settings follow this
    ' window live through LivePreviewChanged).
    Public Property TestScore As ReelAndLED.ScoreInfo = Nothing
    ' Lights the reel artwork exactly like the Designer preview.
    Public Property TestIlluminate As Func(Of Image, Integer, eReelIlluminationLocation, Image) = Nothing

    Public Event LivePreviewChanged(ByVal location As eReelIlluminationLocation,
                                    ByVal intensity As Integer,
                                    ByVal reel3DEnabled As Boolean,
                                    ByVal reel3DBrightness As Integer,
                                    ByVal reel3DTemperature As Integer,
                                    ByVal reel3DDepth As Integer,
                                    ByVal reel3DGlass As Integer,
                                    ByVal reel3DStyle As Integer,
                                    ByVal reel3DCurve As Integer,
                                    ByVal reel3DShadow As Integer,
                                    ByVal reel3DGlassStyle As Integer,
                                    ByVal reel3DDividers As Boolean,
                                    ByVal reel3DDividerWidth As Integer)

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
                                       ByRef reel3Dglass As Integer,
                                       ByRef reel3Dstyle As Integer,
                                       ByRef reel3Dcurve As Integer,
                                       ByRef reel3Dshadow As Integer,
                                       ByRef reel3DglassStyle As Integer,
                                       ByRef reel3Ddividers As Boolean,
                                       ByRef reel3DdividerWidth As Integer) As DialogResult
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
        cmbReelStyle.SelectedIndex = Math.Max(0, Math.Min(1, reel3Dstyle))
        trackReelCurve.Value = Math.Max(trackReelCurve.Minimum, Math.Min(trackReelCurve.Maximum, reel3Dcurve))
        trackReelShadow.Value = Math.Max(trackReelShadow.Minimum, Math.Min(trackReelShadow.Maximum, reel3Dshadow))
        cmbGlassStyle.SelectedIndex = Math.Max(0, Math.Min(ReelGlass.MaximumStyle, reel3DglassStyle))
        chkReelDividers.Checked = reel3Ddividers
        trackReelDividerWidth.Value = Math.Max(trackReelDividerWidth.Minimum, Math.Min(trackReelDividerWidth.Maximum, reel3DdividerWidth))
        LoadRollTime()
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
        reel3Dstyle = Math.Max(0, cmbReelStyle.SelectedIndex)
        reel3Dcurve = trackReelCurve.Value
        reel3Dshadow = trackReelShadow.Value
        reel3DglassStyle = Math.Max(0, cmbGlassStyle.SelectedIndex)
        reel3Ddividers = chkReelDividers.Checked
        reel3DdividerWidth = trackReelDividerWidth.Value
        Return nRet
    End Function

    ' Shows the backglass rolling interval as the runtimes use it (below 10 means the
    ' 101 ms default). Opening the window never changes the saved value.
    Private Sub LoadRollTime()
        Dim saved As Integer = 0
        If Backglass.currentData IsNot Nothing Then saved = Backglass.currentData.ReelRollingInterval
        Dim effective As Integer = If(saved < 10, RuntimeDefaultRollingInterval, saved)
        Dim minimum As Integer = Math.Min(RollTimeMinimum, effective)
        Dim maximum As Integer = Math.Max(RollTimeMaximum, effective)
        numReelRollTime.Minimum = Math.Min(numReelRollTime.Minimum, minimum)
        numReelRollTime.Maximum = Math.Max(numReelRollTime.Maximum, maximum)
        trackReelRollTime.Minimum = Math.Min(trackReelRollTime.Minimum, minimum)
        trackReelRollTime.Maximum = Math.Max(trackReelRollTime.Maximum, maximum)
        trackReelRollTime.Value = effective
        trackReelRollTime.Minimum = minimum
        trackReelRollTime.Maximum = maximum
        numReelRollTime.Minimum = minimum
        numReelRollTime.Maximum = maximum
        numReelRollTime.Value = effective
        trackReelRollTime.Enabled = Backglass.currentData IsNot Nothing
    End Sub

    Private Sub RollTimeChanged(ByVal sender As Object, ByVal e As EventArgs)
        If ignoreChanges OrElse Backglass.currentData Is Nothing Then Return
        Backglass.currentData.ReelRollingInterval = trackReelRollTime.Value
        IsDirty = True
        If testSpinWindow IsNot Nothing AndAlso Not testSpinWindow.IsDisposed Then testSpinWindow.SettingsChanged()
    End Sub

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
                                      trackReelGlass.Value,
                                      Math.Max(0, cmbReelStyle.SelectedIndex),
                                      trackReelCurve.Value,
                                      trackReelShadow.Value,
                                      Math.Max(0, cmbGlassStyle.SelectedIndex),
                                      chkReelDividers.Checked,
                                      trackReelDividerWidth.Value)
        If testSpinWindow IsNot Nothing AndAlso Not testSpinWindow.IsDisposed Then testSpinWindow.SettingsChanged()
    End Sub

    Private Sub BuildReel3DControls()
        Text = "Reel Lighting & 3D"
        ClientSize = New Size(704, 561)
        MinimumSize = New Size(720, 599)
        grpGeneral.Location = New Point(10, 7)
        grpGeneral.Size = New Size(227, 286)
        TrackBarIntensity.SetBounds(93, 143, 67, 45)
        numLegacyIntensity.SetBounds(160, 149, 55, 24)
        numLegacyIntensity.Name = "numLegacyIntensity"
        TrackBarNumericLink.Bind(TrackBarIntensity, numLegacyIntensity)
        grpGeneral.Controls.Add(numLegacyIntensity)
        numLegacyIntensity.BringToFront()
        btnOk.Location = New Point(508, 524)
        btnCancel.Location = New Point(603, 524)
        btnCancel.Text = "Close"

        ' Test spin: shows the selected score rolling, as VPX will.
        btnTestSpin.Text = "Test spin..."
        btnTestSpin.Location = New Point(256, 524)
        btnTestSpin.Size = New Size(Math.Max(110, btnOk.Width + 20), btnOk.Height)
        btnTestSpin.Anchor = AnchorStyles.Bottom Or AnchorStyles.Left
        AddHandler btnTestSpin.Click, AddressOf TestSpin_Click
        Controls.Add(btnTestSpin)

        Dim group As New GroupBox() With {
            .Text = "3D BACKLIT REELS",
            .Location = New Point(246, 7),
            .Size = New Size(448, 507),
            .Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right Or AnchorStyles.Bottom
        }
        Dim layout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .Padding = New Padding(10, 8, 10, 8),
            .ColumnCount = 3,
            .RowCount = 12
        }
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 145))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 76))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 31))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 31))
        For i As Integer = 1 To 4
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 43))
        Next
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 31))
        For i As Integer = 1 To 3
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
        ConfigureReelSlider(trackReelCurve, 0, ReelRealistic.MaximumCurve, 100, 10, 10, 20)
        ConfigureReelSlider(trackReelShadow, 0, 200, 100, 10, 10, 20)
        ConfigureReelSlider(trackReelRollTime, RollTimeMinimum, RollTimeMaximum, RollTimeMinimum, 5, 1, 5)
        ConfigureReelSlider(trackReelDividerWidth, ReelDivider.MinimumWidth, ReelDivider.MaximumWidth, ReelDivider.DefaultWidth, 5, 1, 5)

        Dim styleLabel As New Label() With {
            .Text = "Reel style",
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft
        }
        cmbReelStyle.DropDownStyle = ComboBoxStyle.DropDownList
        cmbReelStyle.Items.AddRange(New Object() {"Classic", "Realistic (curved drum)"})
        cmbReelStyle.SelectedIndex = 0
        cmbReelStyle.Dock = DockStyle.Fill
        cmbReelStyle.Margin = New Padding(3, 4, 0, 3)
        layout.Controls.Add(styleLabel, 0, 1)
        layout.Controls.Add(cmbReelStyle, 1, 1)

        AddReelSliderRow(layout, 2, "Backlight brightness (%)", trackReelBrightness, numReelBrightness)
        AddReelSliderRow(layout, 3, "Color temperature (K)", trackReelTemperature, numReelTemperature, 100)
        AddReelSliderRow(layout, 4, "3D depth (%)", trackReelDepth, numReelDepth)
        AddReelSliderRow(layout, 5, "Glass reflection (%)", trackReelGlass, numReelGlass)

        Dim glassLabel As New Label() With {
            .Text = "Glass look",
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft
        }
        cmbGlassStyle.DropDownStyle = ComboBoxStyle.DropDownList
        cmbGlassStyle.Items.AddRange(New Object() {"Reflection lines (original)", "Soft wide reflection", "Soft diagonal streaks"})
        cmbGlassStyle.SelectedIndex = 0
        cmbGlassStyle.Dock = DockStyle.Fill
        cmbGlassStyle.Margin = New Padding(3, 4, 0, 3)
        layout.Controls.Add(glassLabel, 0, 6)
        layout.Controls.Add(cmbGlassStyle, 1, 6)

        AddReelSliderRow(layout, 7, "Drum curve (%)", trackReelCurve, numReelCurve)
        AddReelSliderRow(layout, 8, "Window shadow (%)", trackReelShadow, numReelShadow)
        AddReelSliderRow(layout, 9, "Reel roll time (ms)", trackReelRollTime, numReelRollTime)
        Dim rollTimeTip As New ToolTip()
        rollTimeTip.SetToolTip(trackReelRollTime, "Higher is slower. One value for every reel on this backglass (Reel Type > Rolling interval).")

        ' Reel dividers: in the free space under Illumination settings, so the
        ' window keeps its height. The slider sets the black line's width in
        ' design pixels (the backglass image's pixels).
        Dim dividerGroup As New GroupBox() With {
            .Text = "REEL DIVIDERS",
            .Location = New Point(10, 301),
            .Size = New Size(227, 118)
        }
        chkReelDividers.Text = "Line between the numbers"
        chkReelDividers.AutoSize = True
        chkReelDividers.Location = New Point(12, 24)
        Dim dividerWidthLabel As New Label() With {
            .Text = "Width (px)",
            .AutoSize = True,
            .Location = New Point(12, 56)
        }
        trackReelDividerWidth.Dock = DockStyle.None
        trackReelDividerWidth.AutoSize = False
        trackReelDividerWidth.SetBounds(6, 74, 150, 38)
        numReelDividerWidth.SetBounds(160, 78, 55, 24)
        TrackBarNumericLink.Bind(trackReelDividerWidth, numReelDividerWidth)
        dividerGroup.Controls.Add(chkReelDividers)
        dividerGroup.Controls.Add(dividerWidthLabel)
        dividerGroup.Controls.Add(trackReelDividerWidth)
        dividerGroup.Controls.Add(numReelDividerWidth)
        Controls.Add(dividerGroup)
        dividerGroup.BringToFront()
        rollTimeTip.SetToolTip(chkReelDividers, "A black line centred between the numbers, so each number looks like its own reel.")

        Dim neutral As New Label() With {
            .Text = "4000 K is neutral. Higher roll time = slower reels (all reels on this backglass).",
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .AutoEllipsis = True
        }
        layout.Controls.Add(neutral, 0, 10)
        layout.SetColumnSpan(neutral, 3)
        Dim note As New Label() With {
            .Text = "Changes preview live on the selected score display. Black printed digits stay dark.",
            .Dock = DockStyle.Fill,
            .AutoEllipsis = True
        }
        layout.Controls.Add(note, 0, 11)
        layout.SetColumnSpan(note, 3)

        AddHandler chkReel3D.CheckedChanged, AddressOf Reel3DValueChanged
        AddHandler trackReelBrightness.ValueChanged, AddressOf Reel3DValueChanged
        AddHandler trackReelTemperature.ValueChanged, AddressOf Reel3DValueChanged
        AddHandler trackReelDepth.ValueChanged, AddressOf Reel3DValueChanged
        AddHandler trackReelGlass.ValueChanged, AddressOf Reel3DValueChanged
        AddHandler cmbReelStyle.SelectedIndexChanged, AddressOf Reel3DValueChanged
        AddHandler trackReelCurve.ValueChanged, AddressOf Reel3DValueChanged
        AddHandler trackReelShadow.ValueChanged, AddressOf Reel3DValueChanged
        AddHandler cmbGlassStyle.SelectedIndexChanged, AddressOf Reel3DValueChanged
        AddHandler trackReelRollTime.ValueChanged, AddressOf RollTimeChanged
        AddHandler chkReelDividers.CheckedChanged, AddressOf Reel3DValueChanged
        AddHandler trackReelDividerWidth.ValueChanged, AddressOf Reel3DValueChanged

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
        cmbGlassStyle.Enabled = enabled
        cmbReelStyle.Enabled = enabled
        ' Drum curve and window shadow belong to the Realistic style only.
        Dim realistic As Boolean = enabled AndAlso cmbReelStyle.SelectedIndex = 1
        trackReelCurve.Enabled = realistic
        trackReelShadow.Enabled = realistic
        ' Dividers belong to the 3D window (both styles).
        chkReelDividers.Enabled = enabled
        trackReelDividerWidth.Enabled = enabled AndAlso chkReelDividers.Checked
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

    Private Sub TestSpin_Click(ByVal sender As Object, ByVal e As EventArgs)
        If TestScore Is Nothing OrElse TestIlluminate Is Nothing Then Return
        If testSpinWindow Is Nothing OrElse testSpinWindow.IsDisposed Then
            testSpinWindow = New ReelTestSpinWindow(TestScore, TestIlluminate)
            testSpinWindow.Show(Me)
        Else
            testSpinWindow.Activate()
        End If
        testSpinWindow.SpinToNewScore()
    End Sub

    Protected Overrides Sub OnFormClosed(ByVal e As FormClosedEventArgs)
        If testSpinWindow IsNot Nothing AndAlso Not testSpinWindow.IsDisposed Then testSpinWindow.Close()
        testSpinWindow = Nothing
        MyBase.OnFormClosed(e)
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

' Test spin: the selected score display rolling to a new score, drawn with the
' same reel code, layout and step time as the Server and plugin, so it shows
' what VPX will show (WYSIWYG). It follows the Reel Lighting & 3D settings live.
Friend Class ReelTestSpinWindow
    Inherits Form

    Private ReadOnly score As ReelAndLED.ScoreInfo
    Private ReadOnly illuminate As Func(Of Image, Integer, eReelIlluminationLocation, Image)
    Private ReadOnly canvas As New SpinCanvas()
    Private ReadOnly btnSpin As New Button()
    Private ReadOnly btnZero As New Button()
    Private ReadOnly chkSlow As New CheckBox()
    Private ReadOnly lblSpeed As New Label()
    Private ReadOnly frameTimer As New System.Windows.Forms.Timer()
    Private ReadOnly clock As New Diagnostics.Stopwatch()
    Private ReadOnly rng As New Random()
    Private lastMs As Double = 0.0
    Private values As Integer() = New Integer() {}
    Private targets As Integer() = New Integer() {}
    Private stepAcc As Double() = New Double() {}
    Private rolls As Double() = New Double() {}
    Private numberSet As Image() = Nothing
    Private numberSetDirty As Boolean = True
    Private frame As Bitmap = Nothing
    Private frameDirty As Boolean = True

    Private NotInheritable Class SpinCanvas
        Inherits Panel
        Public Sub New()
            SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
            BackColor = Color.Black
        End Sub
    End Class

    Public Sub New(ByVal score As ReelAndLED.ScoreInfo, ByVal illuminate As Func(Of Image, Integer, eReelIlluminationLocation, Image))
        Me.score = score
        Me.illuminate = illuminate
        SuspendLayout()
        AutoScaleDimensions = New SizeF(96.0F, 96.0F)
        AutoScaleMode = AutoScaleMode.Dpi
        Text = "Test spin"
        ShowInTaskbar = False
        MinimizeBox = False
        StartPosition = FormStartPosition.CenterParent
        ClientSize = New Size(680, 240)
        MinimumSize = New Size(380, 200)

        Dim bar As New FlowLayoutPanel() With {
            .Dock = DockStyle.Bottom,
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .Padding = New Padding(6),
            .WrapContents = True
        }
        btnSpin.Text = "Spin to a new score"
        btnSpin.AutoSize = True
        btnZero.Text = "Reset to 0"
        btnZero.AutoSize = True
        chkSlow.Text = "Slow motion (10x slower)"
        chkSlow.AutoSize = True
        chkSlow.Margin = New Padding(12, 7, 3, 3)
        lblSpeed.AutoSize = True
        lblSpeed.Margin = New Padding(12, 8, 3, 3)
        bar.Controls.Add(btnSpin)
        bar.Controls.Add(btnZero)
        bar.Controls.Add(chkSlow)
        bar.Controls.Add(lblSpeed)
        canvas.Dock = DockStyle.Fill
        Controls.Add(canvas)
        Controls.Add(bar)
        ResumeLayout(False)
        PerformLayout()

        ResizeDigits()
        frameTimer.Interval = 15
        AddHandler frameTimer.Tick, AddressOf FrameTick
        AddHandler canvas.Paint, AddressOf CanvasPaint
        AddHandler btnSpin.Click, Sub() SpinToNewScore()
        AddHandler btnZero.Click, Sub() SpinTo(New Integer(values.Length - 1) {})
        AddHandler chkSlow.CheckedChanged, Sub() UpdateSpeedLabel()
        UpdateSpeedLabel()
    End Sub

    ' The Reel Lighting & 3D settings changed: draw with the new values.
    Public Sub SettingsChanged()
        UpdateSpeedLabel()
        numberSetDirty = True
        frameDirty = True
        canvas.Invalidate()
    End Sub

    Public Sub SpinToNewScore()
        ResizeDigits()
        Dim newTargets(values.Length - 1) As Integer
        Dim changed As Boolean = False
        Do Until changed
            For i As Integer = 0 To newTargets.Length - 1
                newTargets(i) = rng.Next(0, 10)
                If newTargets(i) <> values(i) Then changed = True
            Next
        Loop
        SpinTo(newTargets)
    End Sub

    Private Sub SpinTo(ByVal newTargets As Integer())
        ResizeDigits()
        For i As Integer = 0 To Math.Min(newTargets.Length, targets.Length) - 1
            targets(i) = newTargets(i)
        Next
        If Not frameTimer.Enabled Then
            clock.Restart()
            lastMs = 0.0
            frameTimer.Start()
        End If
    End Sub

    Private Sub ResizeDigits()
        Dim digits As Integer = Math.Max(1, score.Digits)
        If values.Length = digits Then Return
        values = New Integer(digits - 1) {}
        targets = New Integer(digits - 1) {}
        stepAcc = New Double(digits - 1) {}
        rolls = New Double(digits - 1) {}
        frameDirty = True
    End Sub

    ' One reel step: the same time the Server and plugin use.
    Private Function StepMilliseconds() As Double
        Return ReelRealistic.StepMilliseconds(Backglass.currentData.ReelRollingInterval,
                                              Backglass.currentData.ReelIntermediateImageCount) * If(chkSlow.Checked, 10.0, 1.0)
    End Function

    Private Sub UpdateSpeedLabel()
        Dim stepMs As Double = ReelRealistic.StepMilliseconds(Backglass.currentData.ReelRollingInterval,
                                                              Backglass.currentData.ReelIntermediateImageCount)
        lblSpeed.Text = If(chkSlow.Checked,
                           String.Format("One step: {0:0} ms (VPX: {1:0} ms)", stepMs * 10.0, stepMs),
                           String.Format("One step: {0:0} ms, the same as VPX", stepMs))
    End Sub

    Private Sub FrameTick(ByVal sender As Object, ByVal e As EventArgs)
        ResizeDigits()
        Dim now As Double = clock.Elapsed.TotalMilliseconds
        Dim elapsed As Double = Math.Min(1000.0, Math.Max(0.0, now - lastMs))
        lastMs = now
        Dim stepMs As Double = StepMilliseconds()
        Dim anyRolling As Boolean = False
        ' Right to left: while a reel still has to roll past 9, the reels to
        ' its left wait (score carry), as B2SReelDisplay and the plugin do.
        Dim hold As Boolean = False
        For i As Integer = values.Length - 1 To 0 Step -1
            If values(i) <> targets(i) AndAlso Not hold Then
                stepAcc(i) += elapsed
                Do While stepAcc(i) >= stepMs AndAlso values(i) <> targets(i)
                    stepAcc(i) -= stepMs
                    values(i) = (values(i) + 1) Mod 10
                Loop
                If values(i) = targets(i) Then stepAcc(i) = 0.0
            End If
            rolls(i) = If(values(i) <> targets(i), stepAcc(i) / stepMs, 0.0)
            If values(i) <> targets(i) Then anyRolling = True
            If values(i) > targets(i) Then hold = True
        Next
        frameDirty = True
        canvas.Invalidate()
        If Not anyRolling Then
            frameTimer.Stop()
            clock.Stop()
        End If
    End Sub

    Private Sub CanvasPaint(ByVal sender As Object, ByVal e As PaintEventArgs)
        e.Graphics.Clear(Color.Black)
        EnsureFrame()
        If frame Is Nothing Then Return
        Dim area As Rectangle = canvas.ClientRectangle
        area.Inflate(-8, -8)
        If area.Width <= 0 OrElse area.Height <= 0 Then Return
        Dim scale As Double = Math.Min(area.Width / CDbl(frame.Width), area.Height / CDbl(frame.Height))
        Dim w As Single = CSng(frame.Width * scale)
        Dim h As Single = CSng(frame.Height * scale)
        e.Graphics.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
        e.Graphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality
        e.Graphics.DrawImage(frame, New RectangleF(area.X + (area.Width - w) / 2.0F, area.Y + (area.Height - h) / 2.0F, w, h))
    End Sub

    ' The score at its design size, laid out like the Server's reel boxes:
    ' one box per digit, each covered by its piece of one display-wide glass.
    Private Sub EnsureFrame()
        If Not frameDirty AndAlso frame IsNot Nothing Then Return
        If numberSetDirty Then
            B2SPictureBox.DisposeReelNumberSet(numberSet)
            numberSet = B2SPictureBox.CreateReelNumberSet(score, illuminate)
            numberSetDirty = False
        End If
        Dim frameWidth As Integer = Math.Max(2, score.Size.Width)
        Dim frameHeight As Integer = Math.Max(2, score.Size.Height)
        Dim digits As Integer = values.Length
        Dim designWidth As Integer = Math.Max(1, CInt((frameWidth - (digits - 1) * score.Spacing / 2) / digits))
        If frame IsNot Nothing Then frame.Dispose()
        frame = New Bitmap(frameWidth, frameHeight, Imaging.PixelFormat.Format32bppArgb)
        Dim overlay As Bitmap = Nothing
        Try
            If score.Reel3DEnabled Then
                overlay = New Bitmap(frameWidth, frameHeight, Imaging.PixelFormat.Format32bppArgb)
                Using og As Graphics = Graphics.FromImage(overlay)
                    og.Clear(Color.Transparent)
                    If score.Reel3DDividers AndAlso digits > 1 Then
                        ' The Server's digit boxes: x = i * (width + Spacing / 2).
                        Dim lefts(digits - 1) As Double
                        For i As Integer = 0 To digits - 1
                            lefts(i) = CInt(i * (designWidth + score.Spacing / 2))
                        Next
                        ReelDivider.Draw(og, New Rectangle(0, 0, frameWidth, frameHeight),
                                         ReelDivider.Centers(lefts, designWidth), score.Reel3DDividerWidth)
                    End If
                    ReelAndLED.Reel3DEffect.DrawWindowOverlay(og, New Rectangle(0, 0, frameWidth, frameHeight),
                                                             score.Reel3DDepth, score.Reel3DGlass, score.Reel3DGlassStyle)
                End Using
            End If
            Using g As Graphics = Graphics.FromImage(frame)
                g.Clear(Color.Black)
                g.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
                g.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality
                For i As Integer = 0 To digits - 1
                    Dim x As Integer = CInt(i * (designWidth + score.Spacing / 2))
                    Dim target As New Rectangle(x, 0, designWidth, frameHeight)
                    Using reel As Image = RenderDigit(values(i), rolls(i), New Size(designWidth, frameHeight))
                        If reel IsNot Nothing Then
                            g.DrawImage(reel, target, New Rectangle(0, 0, reel.Width, reel.Height), GraphicsUnit.Pixel)
                        End If
                    End Using
                    If overlay IsNot Nothing AndAlso x < frameWidth Then
                        g.DrawImage(overlay, target, New Rectangle(x, 0, Math.Min(designWidth, frameWidth - x), frameHeight), GraphicsUnit.Pixel)
                    End If
                    ' The strip to the next digit gets its part of the window overlay
                    ' too, as the Server's backglass draws it (ReelGapOverlay).
                    If overlay IsNot Nothing AndAlso i < digits - 1 Then
                        Dim gapX As Integer = x + designWidth
                        Dim nextX As Integer = Math.Min(frameWidth, CInt((i + 1) * (designWidth + score.Spacing / 2)))
                        If nextX > gapX Then
                            Dim gap As New Rectangle(gapX, 0, nextX - gapX, frameHeight)
                            g.DrawImage(overlay, gap, gap, GraphicsUnit.Pixel)
                        End If
                    End If
                Next
            End Using
        Finally
            If overlay IsNot Nothing Then overlay.Dispose()
        End Try
        frameDirty = False
    End Sub

    Private Function RenderDigit(ByVal digit As Integer, ByVal roll As Double, ByVal size As Size) As Image
        If score.Reel3DEnabled AndAlso score.Reel3DStyle = ReelRealistic.StyleRealistic AndAlso numberSet IsNot Nothing Then
            Return B2SPictureBox.RenderRealisticReelDigit(score, numberSet, size, digit, roll)
        End If
        ' Classic: the number jumps at each step, sized and lit like the Designer preview.
        Dim source As Image = GetReelDigitImage(score.ReelType, digit)
        If source Is Nothing Then
            source = GetReelImage(score.ReelType, score.ReelColor, Backglass.currentData.UseDream7LEDs, Backglass.currentData.D7Thickness,
                                  Backglass.currentData.D7Shear, Backglass.currentData.D7Glow, size)
        End If
        If source Is Nothing Then Return Nothing
        Dim reelImage As Image = ResizeScoreReel(source, size, score)
        If reelImage Is Nothing Then Return Nothing
        If score.ReelIlluLocation <> eReelIlluminationLocation.Off AndAlso score.ReelIlluIntensity > 0 Then
            Dim illuminated As Image = illuminate(reelImage, score.ReelIlluIntensity, score.ReelIlluLocation)
            If illuminated IsNot Nothing AndAlso Not Object.ReferenceEquals(illuminated, reelImage) Then
                reelImage.Dispose()
                reelImage = illuminated
            End If
        End If
        If score.Reel3DEnabled Then
            Dim enhanced As Bitmap = ReelAndLED.Reel3DEffect.RenderReel(reelImage, score.Reel3DBrightness, score.Reel3DTemperature, score.Reel3DDepth)
            If enhanced IsNot Nothing Then
                reelImage.Dispose()
                reelImage = enhanced
            End If
        End If
        Return reelImage
    End Function

    Protected Overrides Sub OnFormClosed(ByVal e As FormClosedEventArgs)
        frameTimer.Stop()
        frameTimer.Dispose()
        B2SPictureBox.DisposeReelNumberSet(numberSet)
        numberSet = Nothing
        If frame IsNot Nothing Then frame.Dispose()
        frame = Nothing
        MyBase.OnFormClosed(e)
    End Sub

End Class
