Imports System

Public Class formSnippitSettings

    Private WithEvents grpAppearance As GroupBox
    Private WithEvents trkBrightness As TrackBar
    Private WithEvents numericBrightness As NumericUpDown
    Private WithEvents chkBehindCanvas As CheckBox
    Private WithEvents grpAutomaticRotation As GroupBox
    Private WithEvents chkAutomaticRotation As CheckBox
    Private WithEvents radAutomaticRotationContinuous As RadioButton
    Private WithEvents radAutomaticRotationTriggered As RadioButton
    Private WithEvents numericAutomaticRotationTriggerID As NumericUpDown
    Private pnlAutomaticRotationTriggerType As Panel
    Private WithEvents radAutomaticRotationSW As RadioButton
    Private WithEvents radAutomaticRotationB2S As RadioButton
    Private WithEvents radAutomaticRotationLamp As RadioButton
    Private WithEvents radAutomaticRotationSolenoid As RadioButton
    Private WithEvents cmbAutomaticRotationDirection As ComboBox
    Private WithEvents numericAutomaticRotationSteps As NumericUpDown
    Private WithEvents numericAutomaticRotationInterval As NumericUpDown
    Private lblAutomaticRotationTriggerID As Label
    Private lblAutomaticRotationDirection As Label
    Private lblAutomaticRotationSteps As Label
    Private lblAutomaticRotationInterval As Label
    Private lblAutomaticRotationMilliseconds As Label

    Private Shared activeDialog As formSnippitSettings = Nothing
    Private ReadOnly diagnosticID As String = Guid.NewGuid().ToString("N").Substring(0, 8)
    Public ignoreChange As Boolean = False

    Private IsDirty As Boolean = False
    Private bulbID As Integer = -1
    Private revealCompletedLayout As Boolean = True
    Private ReadOnly previewBulbs As New Generic.List(Of Illumination.BulbInfo)()
    Private ReadOnly originalBrightness As New Generic.Dictionary(Of Illumination.BulbInfo, Integer)()
    Private ReadOnly originalBehindCanvas As New Generic.Dictionary(Of Illumination.BulbInfo, Boolean)()
    Private snippetLayoutRoot As TableLayoutPanel
    Private snippetLeftPanel As Panel
    Private snippetLeftContent As Panel
    Private snippetRightLayout As TableLayoutPanel
    Private snippetActionPanel As FlowLayoutPanel
    Private snippetPreviewGroup As GroupBox
    Private snippetPreviewBox As EditorPreviewPictureBox
    Private snippetPreviewStatus As Label
    Private snippetPreviewZoomLabel As Label
    Private snippetPreviewZoomOutButton As Button
    Private snippetPreviewZoomInButton As Button
    Private snippetPreviewFitButton As Button
    Private snippetPreviewActualButton As Button
    Private snippetPreviewSource As Bitmap
    Private snippetPreviewTimer As Timer
    Private snippetPreviewPending As Boolean
    Private snippetPreviewZoomPercent As Integer
    Private snippetPreviewPanOffset As Point = Point.Empty
    Private snippetPreviewIsPanning As Boolean
    Private snippetPreviewPanStart As Point
    Private snippetPreviewPanStartOffset As Point

    Public Sub New()
        InitializeComponent()
        BuildAppearanceSection()
        BuildAutomaticRotationSection()
        TraceWindow("constructor after InitializeComponent")

        ' Build the complete resizable layout before B2SThemedForm applies the
        ' shared B2S window chrome during the first ShowDialog call. Applying
        ' the chrome before changing the legacy dialog to Sizable would leave
        ' both the native Windows frame and the B2S frame active.
        BuildSnippetManagedLayout()
        TraceWindow("constructor after theme")

        ' Keep the already completed frame out of the compositor until its
        ' ShowDialog layout pass has also finished.
        Me.Opacity = 0.0R
        AddHandler Me.LocationChanged, Sub() TraceWindow("LocationChanged")
        AddHandler Me.SizeChanged, Sub() TraceWindow("SizeChanged")
        AddHandler Me.VisibleChanged, Sub() TraceWindow("VisibleChanged")
        AddHandler Me.FormClosed, AddressOf SnippetSettings_FormClosed
    End Sub

    Private Sub TraceWindow(ByVal action As String)
        Debug.WriteLine(String.Format("{0:HH:mm:ss.fff} id={1} {2} visible={3} opacity={4:0.00} bounds={5},{6},{7},{8}",
                                      DateTime.Now, diagnosticID, action, Me.Visible, Me.Opacity,
                                      Me.Left, Me.Top, Me.Width, Me.Height))
    End Sub

    Public Shadows Function ShowDialog(ByVal owner As IWin32Window,
                                       ByVal id As Integer,
                                       ByRef name As String,
                                       ByRef snippittype As eSnippitType,
                                       ByRef zorder As Integer,
                                       ByRef brightness As Integer,
                                       ByRef behindCanvas As Boolean,
                                       ByRef mechid As Integer,
                                       ByRef rotatingsteps As Integer,
                                       ByRef rotatinginterval As Integer,
                                       ByRef rotatingdirection As eSnippitRotationDirection,
                                       ByRef rotationstopping As eSnippitRotationStopBehaviour,
                                       ByRef automaticRotationEnabled As Boolean,
                                       ByRef automaticRotationContinuous As Boolean,
                                       ByRef automaticRotationTriggerID As Integer,
                                       ByRef automaticRotationTriggerType As eRomIDType,
                                       ByRef automaticRotationSteps As Integer,
                                       ByRef automaticRotationInterval As Integer,
                                       ByRef automaticRotationDirection As eSnippitRotationDirection,
                                       ByRef automaticRotationStopBehaviour As eSnippitRotationStopBehaviour,
                                       Optional ByVal multiSelection As IEnumerable(Of Illumination.BulbInfo) = Nothing) As DialogResult

        ' Several legacy routes can reach snippet properties during the same
        ' double-click message sequence. Enforce the single-window rule at the
        ' dialog itself so no caller can create a second visible instance.
        If activeDialog IsNot Nothing AndAlso Not activeDialog.IsDisposed Then
            TraceWindow("ShowDialog refused; active=" & activeDialog.diagnosticID)
            If activeDialog.WindowState = FormWindowState.Minimized Then activeDialog.WindowState = FormWindowState.Normal
            activeDialog.BringToFront()
            activeDialog.Activate()
            Return DialogResult.Cancel
        End If

        activeDialog = Me
        TraceWindow("ShowDialog accepted")

        ignoreChange = True
        ' set starting values
        bulbID = id
        previewBulbs.Clear()
        originalBrightness.Clear()
        originalBehindCanvas.Clear()
        If multiSelection IsNot Nothing Then
            For Each candidate As Illumination.BulbInfo In multiSelection
                If candidate IsNot Nothing AndAlso Not previewBulbs.Contains(candidate) Then previewBulbs.Add(candidate)
            Next
        End If
        If Backglass.currentTabPage IsNot Nothing AndAlso Backglass.currentTabPage.Mouse IsNot Nothing Then
            Dim selected As Illumination.BulbInfo = Backglass.currentTabPage.Mouse.SelectedBulb
            If selected IsNot Nothing AndAlso selected.ID = id AndAlso Not previewBulbs.Contains(selected) Then previewBulbs.Insert(0, selected)
        End If
        For Each candidate As Illumination.BulbInfo In previewBulbs
            originalBrightness(candidate) = Math.Max(0, Math.Min(200, candidate.SnippitInfo.Brightness))
            originalBehindCanvas(candidate) = candidate.SnippitInfo.BehindCanvas
        Next
        Dim primaryPreview As Illumination.BulbInfo = previewBulbs.FirstOrDefault(Function(candidate) candidate.ID = id)
        brightness = Math.Max(0, Math.Min(200, If(primaryPreview IsNot Nothing, primaryPreview.SnippitInfo.Brightness, brightness)))
        behindCanvas = If(primaryPreview IsNot Nothing, primaryPreview.SnippitInfo.BehindCanvas, behindCanvas)

        Dim legacySelfRotation As Boolean = (snippittype = eSnippitType.SelfRotatingImage)
        Dim legacyMechanicalRotation As Boolean = (snippittype = eSnippitType.MechRotatingImage)

        ' Legacy rotation and one-image rotation are separate formats. A legacy
        ' snippet never enters the native mode merely because its properties
        ' dialog was opened. To create a one-image spinner, the author must
        ' deliberately choose Standard Image and then enable that mode.
        If legacySelfRotation OrElse legacyMechanicalRotation Then automaticRotationEnabled = False

        txtName.Text = name
        ConfigureSnippetTypeChoices(legacySelfRotation)
        SelectSnippetType(snippittype)
        If zorder < 0 Then zorder = 0
        numericZOrder.Maximum = Decimal.MaxValue
        numericZOrder.Value = Math.Max(0, zorder)
        numericZOrder.ReadOnly = True
        numericZOrder.TabStop = False
        numericZOrder.Enabled = False
        lblZOrder.Text = "Z layer (managed in Layers):"
        numericZOrder.AccessibleDescription = "Use the Layers window to change stacking order."
        brightness = Math.Max(0, Math.Min(200, brightness))
        trkBrightness.Value = brightness
        numericBrightness.Value = brightness
        chkBehindCanvas.Checked = behindCanvas
        If mechid < numericMechID.Minimum OrElse mechid > numericMechID.Maximum Then mechid = numericMechID.Minimum
        numericMechID.Value = mechid
        If rotatingsteps < numericRotatingSteps.Minimum Then rotatingsteps = numericRotatingSteps.Minimum
        If rotatingsteps > numericRotatingSteps.Maximum Then rotatingsteps = numericRotatingSteps.Maximum
        numericRotatingSteps.Value = rotatingsteps
        If rotatinginterval < numericRotatingInterval.Minimum Then rotatinginterval = numericRotatingInterval.Minimum
        If rotatinginterval > numericRotatingInterval.Maximum Then rotatinginterval = numericRotatingInterval.Maximum
        numericRotatingInterval.Value = rotatinginterval
        If rotatingdirection < eSnippitRotationDirection.Clockwise OrElse rotatingdirection > eSnippitRotationDirection.AntiClockwise Then rotatingdirection = eSnippitRotationDirection.Clockwise
        cmbRotatingDirection.SelectedIndex = rotatingdirection
        If rotationstopping < eSnippitRotationStopBehaviour.SpinOff OrElse rotationstopping > eSnippitRotationStopBehaviour.RunAnimationToFirstStep Then rotationstopping = eSnippitRotationStopBehaviour.SpinOff
        cmbRotationStopBehaviour.SelectedIndex = rotationstopping
        chkAutomaticRotation.Checked = automaticRotationEnabled
        radAutomaticRotationContinuous.Checked = automaticRotationContinuous OrElse automaticRotationTriggerID <= 0
        radAutomaticRotationTriggered.Checked = Not radAutomaticRotationContinuous.Checked
        numericAutomaticRotationTriggerID.Value = Math.Max(numericAutomaticRotationTriggerID.Minimum, Math.Min(numericAutomaticRotationTriggerID.Maximum, Math.Max(1, automaticRotationTriggerID)))
        Dim displayedTriggerType As eRomIDType = automaticRotationTriggerType
        If displayedTriggerType <> eRomIDType.Lamp AndAlso displayedTriggerType <> eRomIDType.Solenoid AndAlso displayedTriggerType <> eRomIDType.B2SCommand AndAlso displayedTriggerType <> eRomIDType.Switch Then displayedTriggerType = eRomIDType.Lamp
        radAutomaticRotationLamp.Checked = (displayedTriggerType = eRomIDType.Lamp)
        radAutomaticRotationSolenoid.Checked = (displayedTriggerType = eRomIDType.Solenoid)
        radAutomaticRotationB2S.Checked = (displayedTriggerType = eRomIDType.B2SCommand)
        radAutomaticRotationSW.Checked = (displayedTriggerType = eRomIDType.Switch)
        numericAutomaticRotationSteps.Value = Math.Max(numericAutomaticRotationSteps.Minimum, Math.Min(numericAutomaticRotationSteps.Maximum, automaticRotationSteps))
        numericAutomaticRotationInterval.Value = Math.Max(numericAutomaticRotationInterval.Minimum, Math.Min(numericAutomaticRotationInterval.Maximum, automaticRotationInterval))
        Dim displayedDirection As eSnippitRotationDirection = automaticRotationDirection
        If displayedDirection < eSnippitRotationDirection.Clockwise OrElse displayedDirection > eSnippitRotationDirection.AntiClockwise Then displayedDirection = eSnippitRotationDirection.Clockwise
        cmbAutomaticRotationDirection.SelectedIndex = displayedDirection
        Dim displayedStop As eSnippitRotationStopBehaviour = If(legacySelfRotation OrElse legacyMechanicalRotation, rotationstopping, automaticRotationStopBehaviour)
        If displayedStop < eSnippitRotationStopBehaviour.SpinOff OrElse displayedStop > eSnippitRotationStopBehaviour.RunAnimationToFirstStep Then displayedStop = eSnippitRotationStopBehaviour.StopImmediatelly
        cmbRotationStopBehaviour.SelectedIndex = displayedStop
        ignoreChange = False
        EnableDisable()
        QueueSnippetPreview()
        
        ' open dialog
        Dim ret As DialogResult
        Try
            TraceWindow("before MyBase.ShowDialog")
            ret = MyBase.ShowDialog(owner)
        Finally
            TraceWindow("after MyBase.ShowDialog")
            If Object.ReferenceEquals(activeDialog, Me) Then activeDialog = Nothing
        End Try
        If ret = Windows.Forms.DialogResult.OK Then
            ' return some values
            name = txtName.Text
            snippittype = SelectedSnippetType()
            zorder = CInt(numericZOrder.Value)
            brightness = CInt(numericBrightness.Value)
            behindCanvas = chkBehindCanvas.Checked
            mechid = numericMechID.Value
            rotatingsteps = numericRotatingSteps.Value
            rotatinginterval = numericRotatingInterval.Value
            rotatingdirection = cmbRotatingDirection.SelectedIndex
            rotationstopping = cmbRotationStopBehaviour.SelectedIndex
            automaticRotationEnabled = (SelectedSnippetType() = eSnippitType.StandardImage AndAlso chkAutomaticRotation.Checked)
            automaticRotationContinuous = radAutomaticRotationContinuous.Checked
            automaticRotationTriggerID = If(radAutomaticRotationTriggered.Checked, CInt(numericAutomaticRotationTriggerID.Value), 0)
            automaticRotationTriggerType = If(radAutomaticRotationSW.Checked, eRomIDType.Switch, If(radAutomaticRotationB2S.Checked, eRomIDType.B2SCommand, If(radAutomaticRotationSolenoid.Checked, eRomIDType.Solenoid, eRomIDType.Lamp)))
            automaticRotationSteps = CInt(numericAutomaticRotationSteps.Value)
            automaticRotationInterval = CInt(numericAutomaticRotationInterval.Value)
            automaticRotationDirection = CType(cmbAutomaticRotationDirection.SelectedIndex, eSnippitRotationDirection)
            automaticRotationStopBehaviour = CType(cmbRotationStopBehaviour.SelectedIndex, eSnippitRotationStopBehaviour)
        End If

        Return ret

    End Function

    Private Sub Ok_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnOk.Click
        If String.IsNullOrEmpty(txtName.Text) OrElse String.IsNullOrEmpty(cmbType.Text) Then
            B2SMessageBox.Show(My.Resources.MSG_EnterSnippitName, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
            txtName.Focus()
            Return
        End If
        MyBase.DialogResult = Windows.Forms.DialogResult.OK
        Me.Close()
    End Sub
    Private Sub Cancel_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnCancel.Click
        If IsDirty Then
            Dim ret As DialogResult = B2SMessageBox.Show(My.Resources.MSG_IsDirty, AppTitle, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question)
            If ret = Windows.Forms.DialogResult.Yes Then
                btnOk.PerformClick()
            ElseIf ret = Windows.Forms.DialogResult.No Then
                Me.Close()
            End If
        Else
            Me.Close()
        End If
    End Sub

    Private currentType As eSnippitType = eSnippitType.StandardImage
    Private ReadOnly snippetTypeChoices As New List(Of eSnippitType)()

    Private Sub ConfigureSnippetTypeChoices(ByVal includeLegacySelfRotation As Boolean)
        Dim standardText As Object = cmbType.Items(CInt(eSnippitType.StandardImage))
        Dim selfRotatingText As Object = cmbType.Items(CInt(eSnippitType.SelfRotatingImage))
        Dim mechanicalText As Object = cmbType.Items(CInt(eSnippitType.MechRotatingImage))

        cmbType.Items.Clear()
        snippetTypeChoices.Clear()
        cmbType.Items.Add(standardText)
        snippetTypeChoices.Add(eSnippitType.StandardImage)
        If includeLegacySelfRotation Then
            cmbType.Items.Add(selfRotatingText)
            snippetTypeChoices.Add(eSnippitType.SelfRotatingImage)
        End If
        cmbType.Items.Add(mechanicalText)
        snippetTypeChoices.Add(eSnippitType.MechRotatingImage)
    End Sub

    Private Function SelectedSnippetType() As eSnippitType
        If cmbType.SelectedIndex < 0 OrElse cmbType.SelectedIndex >= snippetTypeChoices.Count Then Return eSnippitType.StandardImage
        Return snippetTypeChoices(cmbType.SelectedIndex)
    End Function

    Private Sub SelectSnippetType(ByVal value As eSnippitType)
        Dim index As Integer = snippetTypeChoices.IndexOf(value)
        cmbType.SelectedIndex = If(index >= 0, index, 0)
    End Sub

    Private Sub Type_Enter(sender As Object, e As System.EventArgs) Handles cmbType.Enter
        currentType = SelectedSnippetType()
    End Sub
    Private Sub Type_SelectedIndexChanged(sender As System.Object, e As System.EventArgs) Handles cmbType.SelectedIndexChanged
        If ignoreChange Then Return
        If SelectedSnippetType() = eSnippitType.SelfRotatingImage Then
            If IsThereAlreadyOneSelfRotatingSnippit(bulbID) Then
                B2SMessageBox.Show(My.Resources.MSG_ThereIsAlreadyOneSelfRotatingSnippit, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information)
                If currentType <> eSnippitType.SelfRotatingImage Then
                    SelectSnippetType(currentType)
                Else
                    SelectSnippetType(eSnippitType.StandardImage)
                End If
            End If
        End If
        If SelectedSnippetType() <> eSnippitType.StandardImage Then chkAutomaticRotation.Checked = False
        If Not ignoreChange Then IsDirty = True
        EnableDisable()
    End Sub

    Private Sub formSnippitSettings_Load(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles MyBase.Load
        IsDirty = False
    End Sub
    Private Sub formSnippitSettings_Shown(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Shown
        TraceWindow("Shown entered")
        IsDirty = False
        Me.PerformLayout()
        Me.Refresh()

        If revealCompletedLayout Then
            revealCompletedLayout = False
            Me.BeginInvoke(New MethodInvoker(
                Sub()
                    If Me.IsDisposed Then Return
                    Me.PerformLayout()
                    Me.Opacity = 1.0R
                    TraceWindow("revealed")
                    Me.Activate()
                    txtName.Focus()
                End Sub))
        Else
            Me.Opacity = 1.0R
            txtName.Focus()
        End If
        QueueSnippetPreview()
    End Sub

    Private Sub EnableDisable()
        Dim selectedType As eSnippitType = SelectedSnippetType()
        Dim standardImage As Boolean = (selectedType = eSnippitType.StandardImage)
        Dim legacySelfRotation As Boolean = (selectedType = eSnippitType.SelfRotatingImage)
        Dim mechanicalRotation As Boolean = (selectedType = eSnippitType.MechRotatingImage)
        Dim legacyRotation As Boolean = legacySelfRotation OrElse mechanicalRotation
        numericMechID.Visible = mechanicalRotation
        lblMechID.Visible = mechanicalRotation
        lblRotationDirection.Visible = legacyRotation
        cmbRotatingDirection.Visible = legacyRotation
        lblRotatingSteps.Visible = legacyRotation
        numericRotatingSteps.Visible = legacyRotation
        lblRotatingInterval.Visible = legacyRotation
        numericRotatingInterval.Visible = legacyRotation
        Label2.Visible = legacyRotation
        numericRotatingSteps.Enabled = legacyRotation
        numericRotatingInterval.Enabled = legacySelfRotation
        cmbRotatingDirection.Enabled = legacyRotation
        Dim automaticRotationAvailable As Boolean = standardImage
        chkAutomaticRotation.Enabled = automaticRotationAvailable
        chkAutomaticRotation.Visible = standardImage
        chkAutomaticRotation.Text = "Enable one-image rotation"
        Dim automaticRotationActive As Boolean = automaticRotationAvailable AndAlso chkAutomaticRotation.Checked
        numericMechID.Enabled = mechanicalRotation
        radAutomaticRotationContinuous.Visible = standardImage
        radAutomaticRotationTriggered.Visible = standardImage
        lblAutomaticRotationTriggerID.Visible = standardImage
        numericAutomaticRotationTriggerID.Visible = standardImage
        pnlAutomaticRotationTriggerType.Visible = standardImage
        lblAutomaticRotationDirection.Visible = standardImage
        cmbAutomaticRotationDirection.Visible = standardImage
        lblAutomaticRotationSteps.Visible = standardImage
        numericAutomaticRotationSteps.Visible = standardImage
        lblAutomaticRotationInterval.Visible = standardImage
        numericAutomaticRotationInterval.Visible = standardImage
        lblAutomaticRotationMilliseconds.Visible = standardImage
        radAutomaticRotationContinuous.Enabled = automaticRotationActive
        radAutomaticRotationTriggered.Enabled = automaticRotationActive
        numericAutomaticRotationTriggerID.Enabled = automaticRotationActive AndAlso radAutomaticRotationTriggered.Checked
        radAutomaticRotationLamp.Enabled = numericAutomaticRotationTriggerID.Enabled
        radAutomaticRotationSolenoid.Enabled = numericAutomaticRotationTriggerID.Enabled
        radAutomaticRotationB2S.Enabled = numericAutomaticRotationTriggerID.Enabled
        radAutomaticRotationSW.Enabled = numericAutomaticRotationTriggerID.Enabled
        cmbAutomaticRotationDirection.Enabled = automaticRotationActive
        numericAutomaticRotationSteps.Enabled = automaticRotationActive
        numericAutomaticRotationInterval.Enabled = automaticRotationActive
        cmbRotationStopBehaviour.Enabled = legacyRotation OrElse (automaticRotationActive AndAlso radAutomaticRotationTriggered.Checked)
        lblAutomaticRotationInterval.Enabled = automaticRotationActive
        lblAutomaticRotationMilliseconds.Enabled = automaticRotationActive
    End Sub

    Private Sub BuildAppearanceSection()
        grpAppearance = New GroupBox With {
            .Name = "grpAppearance",
            .Text = "Image appearance",
            .Location = New Point(groupGeneral.Left, groupGeneral.Top),
            .Size = New Size(grpRotating.Width, 88),
            .TabStop = False
        }
        Dim lblBrightness As New Label With {
            .Text = "Brightness:",
            .AutoSize = False,
            .TextAlign = ContentAlignment.MiddleRight,
            .Location = New Point(18, 24),
            .Size = New Size(108, 21)
        }
        trkBrightness = New TrackBar With {
            .Name = "trkBrightness",
            .Minimum = 0,
            .Maximum = 200,
            .Value = 100,
            .TickFrequency = 25,
            .SmallChange = 5,
            .LargeChange = 10,
            .AutoSize = False,
            .Location = New Point(132, 21),
            .Size = New Size(184, 30)
        }
        numericBrightness = New NumericUpDown With {
            .Name = "numericBrightness",
            .Minimum = 0D,
            .Maximum = 200D,
            .Value = 100D,
            .Increment = 5D,
            .Location = New Point(320, 24),
            .Size = New Size(55, 21)
        }
        Dim lblPercent As New Label With {.Text = "%", .AutoSize = True, .Location = New Point(378, 27)}
        chkBehindCanvas = New AutomaticRotationStatusCheckBox With {
            .Name = "chkBehindCanvas",
            .Text = "Place snippet behind the backglass canvas",
            .AutoSize = True,
            .Location = New Point(132, 57)
        }
        grpAppearance.Controls.AddRange(New Control() {lblBrightness, trkBrightness, numericBrightness, lblPercent, chkBehindCanvas})
        Controls.Add(grpAppearance)
    End Sub

    Private Sub BrightnessTrackChanged(sender As Object, e As EventArgs) Handles trkBrightness.ValueChanged
        If numericBrightness Is Nothing Then Return
        If CInt(numericBrightness.Value) <> trkBrightness.Value Then numericBrightness.Value = trkBrightness.Value
        If Not ignoreChange Then
            IsDirty = True
            ApplyBrightnessPreview(trkBrightness.Value)
        End If
    End Sub

    Private Sub BrightnessNumberChanged(sender As Object, e As EventArgs) Handles numericBrightness.ValueChanged
        If trkBrightness Is Nothing Then Return
        Dim value As Integer = CInt(numericBrightness.Value)
        If trkBrightness.Value <> value Then trkBrightness.Value = value
        If Not ignoreChange Then
            IsDirty = True
            ApplyBrightnessPreview(value)
        End If
    End Sub

    Private Sub ApplyBrightnessPreview(ByVal value As Integer)
        ' Appearance changes remain local to this dialog until Save & Close.
        ' RefreshSnippetPreviewSource temporarily applies the pending values
        ' only while composing the embedded preview, then restores the model.
        QueueSnippetPreview()
    End Sub

    Private Sub BehindCanvasChanged(sender As Object, e As EventArgs) Handles chkBehindCanvas.CheckedChanged
        If ignoreChange Then Return
        IsDirty = True
        ApplyBehindCanvasPreview(chkBehindCanvas.Checked)
    End Sub

    Private Sub ApplyBehindCanvasPreview(ByVal value As Boolean)
        QueueSnippetPreview()
    End Sub

    Private Sub BuildAutomaticRotationSection()
        grpAutomaticRotation = New GroupBox With {
            .Name = "grpAutomaticRotation",
            .Text = "Automatic image rotation",
            .Location = New Point(grpAppearance.Left, grpAppearance.Bottom + 6),
            .Size = New Size(grpRotating.Width, 310),
            .TabStop = False
        }

        ' Keep snippet identity in the supported authoring panel. Z-layer stays
        ' hidden because stacking is owned by the Layers menu.
        grpAutomaticRotation.Controls.Add(lblName)
        grpAutomaticRotation.Controls.Add(txtName)
        grpAutomaticRotation.Controls.Add(lblType)
        grpAutomaticRotation.Controls.Add(cmbType)
        grpAutomaticRotation.Controls.Add(btnChangeImage)
        grpAutomaticRotation.Controls.Add(btnTrimImage)
        lblName.Location = New Point(18, 23)
        lblName.Size = New Size(108, 21)
        lblName.TextAlign = ContentAlignment.MiddleRight
        txtName.Location = New Point(132, 23)
        txtName.Size = New Size(252, 21)
        lblType.Location = New Point(18, 51)
        lblType.Size = New Size(108, 21)
        lblType.TextAlign = ContentAlignment.MiddleRight
        cmbType.Location = New Point(132, 51)
        cmbType.Size = New Size(252, 21)
        btnChangeImage.Location = New Point(132, 79)
        btnChangeImage.Size = New Size(122, 25)
        btnTrimImage.Location = New Point(262, 79)
        btnTrimImage.Size = New Size(122, 25)

        chkAutomaticRotation = New AutomaticRotationStatusCheckBox With {
            .Name = "chkAutomaticRotation",
            .Text = "Enable image rotation",
            .AutoSize = True,
            .Location = New Point(18, 112)
        }
        radAutomaticRotationContinuous = New RadioButton With {
            .Name = "radAutomaticRotationContinuous",
            .Text = "Continuous",
            .AutoSize = True,
            .Checked = True,
            .Location = New Point(52, 138)
        }
        radAutomaticRotationTriggered = New RadioButton With {
            .Name = "radAutomaticRotationTriggered",
            .Text = "Triggered by B2S / ROM command",
            .AutoSize = True,
            .Location = New Point(176, 138)
        }
        lblAutomaticRotationTriggerID = New Label With {.Text = "Command ID:", .AutoSize = False, .TextAlign = ContentAlignment.MiddleRight, .Location = New Point(18, 165), .Size = New Size(135, 21)}
        numericAutomaticRotationTriggerID = New NumericUpDown With {.Name = "numericAutomaticRotationTriggerID", .Minimum = 1D, .Maximum = 9999D, .Value = 1D, .Location = New Point(160, 165), .Size = New Size(68, 21)}
        ' Keep the source choices in their own panel.  Radio buttons sharing the
        ' group box with Continuous/Command would otherwise uncheck that mode.
        pnlAutomaticRotationTriggerType = New Panel With {.Name = "pnlAutomaticRotationTriggerType", .Location = New Point(236, 163), .Size = New Size(215, 25)}
        radAutomaticRotationLamp = New RadioButton With {.Name = "radAutomaticRotationLamp", .Text = "Lamp", .AutoSize = True, .Checked = True, .Location = New Point(0, 3)}
        radAutomaticRotationSolenoid = New RadioButton With {.Name = "radAutomaticRotationSolenoid", .Text = "Solenoid", .AutoSize = True, .Location = New Point(78, 3)}
        radAutomaticRotationSolenoid.Left = 53
        radAutomaticRotationB2S = New RadioButton With {.Name = "radAutomaticRotationB2S", .Text = "B2S", .AutoSize = True, .Location = New Point(124, 3)}
        radAutomaticRotationSW = New RadioButton With {.Name = "radAutomaticRotationSW", .Text = "SW", .AutoSize = True, .Location = New Point(174, 3)}
        pnlAutomaticRotationTriggerType.Width = 230
        pnlAutomaticRotationTriggerType.Controls.AddRange(New Control() {radAutomaticRotationLamp, radAutomaticRotationSolenoid, radAutomaticRotationB2S, radAutomaticRotationSW})

        grpAutomaticRotation.Controls.Add(lblMechID)
        grpAutomaticRotation.Controls.Add(numericMechID)
        lblMechID.Location = New Point(18, 165)
        lblMechID.Size = New Size(135, 21)
        lblMechID.TextAlign = ContentAlignment.MiddleRight
        numericMechID.Location = New Point(160, 165)
        numericMechID.Size = New Size(68, 21)

        ' The rebuilt dialog hides grpRotating, so move the real legacy controls
        ' into the supported panel and show them only for legacy snippet types.
        grpAutomaticRotation.Controls.Add(lblRotationDirection)
        grpAutomaticRotation.Controls.Add(cmbRotatingDirection)
        grpAutomaticRotation.Controls.Add(lblRotatingSteps)
        grpAutomaticRotation.Controls.Add(numericRotatingSteps)
        grpAutomaticRotation.Controls.Add(lblRotatingInterval)
        grpAutomaticRotation.Controls.Add(numericRotatingInterval)
        grpAutomaticRotation.Controls.Add(Label2)
        lblRotationDirection.Location = New Point(18, 193)
        lblRotationDirection.Size = New Size(135, 21)
        lblRotationDirection.TextAlign = ContentAlignment.MiddleRight
        cmbRotatingDirection.Location = New Point(160, 193)
        cmbRotatingDirection.Size = New Size(147, 21)
        lblRotatingSteps.Location = New Point(18, 221)
        lblRotatingSteps.Size = New Size(135, 21)
        lblRotatingSteps.TextAlign = ContentAlignment.MiddleRight
        numericRotatingSteps.Location = New Point(160, 221)
        numericRotatingSteps.Size = New Size(58, 21)
        lblRotatingInterval.Location = New Point(18, 249)
        lblRotatingInterval.Size = New Size(135, 21)
        lblRotatingInterval.TextAlign = ContentAlignment.MiddleRight
        numericRotatingInterval.Location = New Point(160, 249)
        numericRotatingInterval.Size = New Size(53, 21)
        Label2.Location = New Point(219, 253)

        lblAutomaticRotationDirection = New Label With {.Text = "Rotation direction:", .AutoSize = False, .TextAlign = ContentAlignment.MiddleRight, .Location = New Point(18, 193), .Size = New Size(135, 21)}
        cmbAutomaticRotationDirection = New ComboBox With {.Name = "cmbAutomaticRotationDirection", .DropDownStyle = ComboBoxStyle.DropDownList, .Location = New Point(160, 193), .Size = New Size(147, 21)}
        cmbAutomaticRotationDirection.Items.AddRange(New Object() {"Clockwise", "Counterclockwise"})

        lblAutomaticRotationSteps = New Label With {.Text = "Steps per rotation:", .AutoSize = False, .TextAlign = ContentAlignment.MiddleRight, .Location = New Point(18, 221), .Size = New Size(135, 21)}
        numericAutomaticRotationSteps = New NumericUpDown With {.Name = "numericAutomaticRotationSteps", .Minimum = 2D, .Maximum = 360D, .Value = 24D, .Location = New Point(160, 221), .Size = New Size(58, 21)}

        lblAutomaticRotationInterval = New Label With {.Text = "Frame interval:", .AutoSize = False, .TextAlign = ContentAlignment.MiddleRight, .Location = New Point(18, 249), .Size = New Size(135, 21)}
        numericAutomaticRotationInterval = New NumericUpDown With {.Name = "numericAutomaticRotationInterval", .Minimum = 10D, .Maximum = 500D, .Value = 50D, .Increment = 10D, .Location = New Point(160, 249), .Size = New Size(53, 21)}
        lblAutomaticRotationMilliseconds = New Label With {.Text = "(in ms)", .AutoSize = True, .Location = New Point(219, 253)}

        grpAutomaticRotation.Controls.Add(lblRotationStopping)
        grpAutomaticRotation.Controls.Add(cmbRotationStopBehaviour)
        lblRotationStopping.Location = New Point(18, 277)
        lblRotationStopping.Size = New Size(135, 21)
        lblRotationStopping.TextAlign = ContentAlignment.MiddleRight
        cmbRotationStopBehaviour.Location = New Point(160, 277)
        cmbRotationStopBehaviour.Size = New Size(224, 21)
        If cmbRotationStopBehaviour.SelectedIndex < 0 AndAlso cmbRotationStopBehaviour.Items.Count > 1 Then
            cmbRotationStopBehaviour.SelectedIndex = eSnippitRotationStopBehaviour.StopImmediatelly
        End If

        grpAutomaticRotation.Controls.AddRange(New Control() {chkAutomaticRotation, radAutomaticRotationContinuous, radAutomaticRotationTriggered, lblAutomaticRotationTriggerID, numericAutomaticRotationTriggerID, pnlAutomaticRotationTriggerType, lblAutomaticRotationDirection, cmbAutomaticRotationDirection, lblAutomaticRotationSteps, numericAutomaticRotationSteps, lblAutomaticRotationInterval, numericAutomaticRotationInterval, lblAutomaticRotationMilliseconds})
        ' Four source choices need a complete row below the command ID.
        pnlAutomaticRotationTriggerType.Location = New Point(18, 191)
        pnlAutomaticRotationTriggerType.Size = New Size(grpAutomaticRotation.Width - 36, 25)
        For Each control As Control In grpAutomaticRotation.Controls
            If control IsNot pnlAutomaticRotationTriggerType AndAlso control.Top >= 193 Then control.Top += 30
        Next
        grpAutomaticRotation.Height += 30
        Controls.Add(grpAutomaticRotation)

        groupGeneral.Visible = False
        grpRotating.Visible = False

        Dim newButtonY As Integer = grpAutomaticRotation.Bottom + 8
        ClientSize = New Size(ClientSize.Width, newButtonY + btnOk.Height + 14)
        btnOk.Top = newButtonY
        btnCancel.Top = newButtonY
    End Sub

    Private Sub BuildSnippetManagedLayout()
        Dim accent As Color = Color.FromArgb(255, 153, 0)
        Dim background As Color = Color.FromArgb(18, 20, 22)
        Dim panelColor As Color = Color.FromArgb(27, 29, 31)

        Text = "Snippet"
        ClientSize = New Size(1260, 780)
        MinimumSize = New Size(1080, 700)
        MaximumSize = Size.Empty
        FormBorderStyle = FormBorderStyle.Sizable
        MaximizeBox = True
        MinimizeBox = False
        DoubleBuffered = True
        Font = New Font("Tahoma", 9.0F)
        BackColor = background
        ForeColor = Color.WhiteSmoke

        snippetLayoutRoot = New TableLayoutPanel With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 1,
            .Padding = New Padding(15, 18, 15, 10),
            .Margin = Padding.Empty,
            .BackColor = background
        }
        snippetLayoutRoot.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 475.0F))
        snippetLayoutRoot.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        snippetLayoutRoot.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        snippetLeftPanel = New Panel With {
            .Dock = DockStyle.Fill,
            .Margin = Padding.Empty,
            .AutoScroll = True,
            .BackColor = background
        }
        snippetLeftContent = New Panel With {
            .Location = Point.Empty,
            .Size = New Size(460, 560),
            .Anchor = AnchorStyles.Top Or AnchorStyles.Left,
            .BackColor = background
        }
        snippetRightLayout = New TableLayoutPanel With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .Margin = New Padding(5, 0, 0, 0),
            .Padding = Padding.Empty,
            .BackColor = background
        }
        snippetRightLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        snippetRightLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        snippetRightLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 48.0F))
        snippetActionPanel = New FlowLayoutPanel With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.RightToLeft,
            .WrapContents = False,
            .Margin = Padding.Empty,
            .Padding = New Padding(0, 5, 0, 5),
            .BackColor = background
        }

        Dim title As New Label With {
            .Text = "▣  Snippet",
            .Left = 9, .Top = 0, .Width = 430, .Height = 42,
            .Font = New Font("Tahoma", 20.0F, FontStyle.Bold),
            .ForeColor = accent
        }
        Dim description As New Label With {
            .Text = "Adjust the selected snippet settings and see the complete backglass update live.",
            .Left = 11, .Top = 43, .Width = 440, .Height = 38,
            .ForeColor = Color.Gainsboro
        }
        grpAppearance.SetBounds(10, 88, 440, grpAppearance.Height)
        grpAppearance.ForeColor = accent
        grpAppearance.BackColor = panelColor
        grpAutomaticRotation.SetBounds(10, grpAppearance.Bottom + 8, 440, grpAutomaticRotation.Height)
        grpAutomaticRotation.ForeColor = accent
        grpAutomaticRotation.BackColor = panelColor

        snippetLeftContent.Controls.Add(title)
        snippetLeftContent.Controls.Add(description)
        snippetLeftContent.Controls.Add(grpAppearance)
        snippetLeftContent.Controls.Add(grpAutomaticRotation)
        snippetLeftContent.Size = New Size(460, grpAutomaticRotation.Bottom + 20)
        snippetLeftPanel.Controls.Add(snippetLeftContent)
        snippetLeftPanel.AutoScrollMinSize = snippetLeftContent.Size

        BuildSnippetPreviewUi(accent, panelColor)

        btnOk.Text = "✓  Save & Close"
        btnOk.Size = New Size(146, 38)
        btnOk.FlatStyle = FlatStyle.Flat
        btnOk.BackColor = accent
        btnOk.ForeColor = Color.Black
        btnOk.Font = New Font("Tahoma", 10.0F, FontStyle.Bold)
        btnOk.Margin = Padding.Empty
        btnCancel.Text = "Cancel"
        btnCancel.Size = New Size(126, 38)
        btnCancel.FlatStyle = FlatStyle.Flat
        btnCancel.Margin = New Padding(10, 0, 0, 0)
        StyleSnippetButton(btnCancel)

        Controls.Add(snippetLayoutRoot)
        snippetLayoutRoot.Controls.Add(snippetLeftPanel, 0, 0)
        snippetLayoutRoot.Controls.Add(snippetRightLayout, 1, 0)
        snippetRightLayout.Controls.Add(snippetPreviewGroup, 0, 0)
        snippetRightLayout.Controls.Add(snippetActionPanel, 0, 1)
        snippetActionPanel.Controls.Add(btnCancel)
        snippetActionPanel.Controls.Add(btnOk)
        snippetLayoutRoot.BringToFront()

        AddHandler Me.Resize, AddressOf SnippetSettings_Resize
        AddHandler snippetPreviewGroup.Resize, AddressOf SnippetPreviewGroup_Resize
        LayoutSnippetPreview()
    End Sub

    Private Sub BuildSnippetPreviewUi(ByVal accent As Color, ByVal panelColor As Color)
        snippetPreviewGroup = New GroupBox With {
            .Text = "  LIVE SNIPPET PREVIEW",
            .Size = New Size(700, 690),
            .Dock = DockStyle.Fill,
            .Margin = Padding.Empty,
            .ForeColor = accent,
            .BackColor = panelColor,
            .Font = New Font("Tahoma", 9.0F, FontStyle.Bold)
        }
        snippetPreviewGroup.Controls.Add(New Label With {
            .Text = "Live Snippet Result",
            .Left = 14, .Top = 29, .Width = 665, .Height = 24,
            .TextAlign = ContentAlignment.MiddleCenter,
            .ForeColor = Color.WhiteSmoke,
            .Font = New Font("Tahoma", 9.0F),
            .Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        })
        snippetPreviewBox = New EditorPreviewPictureBox With {
            .Left = 14, .Top = 57, .Width = 665, .Height = 500,
            .BackColor = Color.FromArgb(11, 12, 13),
            .BorderStyle = BorderStyle.FixedSingle,
            .SizeMode = PictureBoxSizeMode.Normal,
            .TabStop = True
        }
        snippetPreviewZoomOutButton = New Button With {.Text = "−", .Width = 42, .Height = 29, .FlatStyle = FlatStyle.Flat}
        snippetPreviewZoomInButton = New Button With {.Text = "+", .Width = 42, .Height = 29, .FlatStyle = FlatStyle.Flat}
        snippetPreviewFitButton = New Button With {.Text = "Fit", .Width = 58, .Height = 29, .FlatStyle = FlatStyle.Flat}
        snippetPreviewActualButton = New Button With {.Text = "100%", .Width = 66, .Height = 29, .FlatStyle = FlatStyle.Flat}
        For Each button As Button In New Button() {snippetPreviewZoomOutButton, snippetPreviewZoomInButton, snippetPreviewFitButton, snippetPreviewActualButton}
            StyleSnippetButton(button)
            snippetPreviewGroup.Controls.Add(button)
        Next
        snippetPreviewZoomLabel = New Label With {
            .Text = "Fit", .Width = 62, .Height = 22,
            .TextAlign = ContentAlignment.MiddleCenter,
            .ForeColor = accent,
            .Font = New Font("Tahoma", 8.25F, FontStyle.Bold)
        }
        snippetPreviewStatus = New Label With {
            .Text = "Preparing preview...", .Width = 240, .Height = 36,
            .TextAlign = ContentAlignment.MiddleCenter,
            .ForeColor = Color.Silver,
            .Font = New Font("Tahoma", 8.25F)
        }
        snippetPreviewGroup.Controls.Add(snippetPreviewBox)
        snippetPreviewGroup.Controls.Add(snippetPreviewZoomLabel)
        snippetPreviewGroup.Controls.Add(snippetPreviewStatus)

        AddHandler snippetPreviewZoomOutButton.Click, AddressOf SnippetPreviewZoomOut_Click
        AddHandler snippetPreviewZoomInButton.Click, AddressOf SnippetPreviewZoomIn_Click
        AddHandler snippetPreviewFitButton.Click, AddressOf SnippetPreviewFit_Click
        AddHandler snippetPreviewActualButton.Click, AddressOf SnippetPreviewActual_Click
        AddHandler snippetPreviewBox.MouseWheel, AddressOf SnippetPreview_MouseWheel
        AddHandler snippetPreviewBox.MouseDown, AddressOf SnippetPreview_MouseDown
        AddHandler snippetPreviewBox.MouseMove, AddressOf SnippetPreview_MouseMove
        AddHandler snippetPreviewBox.MouseUp, AddressOf SnippetPreview_MouseUp
        AddHandler snippetPreviewBox.MouseLeave, AddressOf SnippetPreview_MouseLeave
        AddHandler snippetPreviewBox.MouseEnter, AddressOf SnippetPreview_MouseEnter
        AddHandler snippetPreviewBox.DoubleClick, AddressOf SnippetPreview_DoubleClick

        snippetPreviewTimer = New Timer With {.Interval = 75}
        AddHandler snippetPreviewTimer.Tick, AddressOf SnippetPreviewTimer_Tick
    End Sub

    Private Sub StyleSnippetButton(ByVal button As Button)
        button.BackColor = Color.FromArgb(38, 40, 43)
        button.ForeColor = Color.WhiteSmoke
        button.FlatAppearance.BorderColor = Color.FromArgb(78, 80, 84)
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(52, 54, 57)
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(64, 66, 70)
    End Sub

    Private Sub SnippetSettings_Resize(ByVal sender As Object, ByVal e As EventArgs)
        LayoutSnippetPreview()
        RefreshSnippetPreviewImage()
    End Sub

    Private Sub SnippetPreviewGroup_Resize(ByVal sender As Object, ByVal e As EventArgs)
        LayoutSnippetPreview()
        RefreshSnippetPreviewImage()
    End Sub

    Private Sub LayoutSnippetPreview()
        If snippetPreviewGroup Is Nothing OrElse snippetPreviewBox Is Nothing Then Return
        Dim controlsTop As Integer = Math.Max(245, snippetPreviewGroup.ClientSize.Height - 48)
        snippetPreviewBox.SetBounds(14, 57,
                                    Math.Max(300, snippetPreviewGroup.ClientSize.Width - 28),
                                    Math.Max(180, controlsTop - 65))
        snippetPreviewZoomOutButton.SetBounds(14, controlsTop, 42, 29)
        snippetPreviewZoomInButton.SetBounds(61, controlsTop, 42, 29)
        snippetPreviewFitButton.SetBounds(108, controlsTop, 58, 29)
        snippetPreviewActualButton.SetBounds(171, controlsTop, 66, 29)
        snippetPreviewZoomLabel.SetBounds(246, controlsTop + 4, 62, 22)
        snippetPreviewStatus.SetBounds(Math.Max(316, snippetPreviewGroup.ClientSize.Width - 250), controlsTop - 3, 240, 36)
    End Sub

    Private Sub QueueSnippetPreview()
        If snippetPreviewTimer Is Nothing Then Return
        snippetPreviewPending = True
        If Not snippetPreviewTimer.Enabled Then snippetPreviewTimer.Start()
    End Sub

    Private Sub SnippetPreviewTimer_Tick(ByVal sender As Object, ByVal e As EventArgs)
        snippetPreviewTimer.Stop()
        If Not snippetPreviewPending Then Return
        snippetPreviewPending = False
        RefreshSnippetPreviewSource()
        If snippetPreviewPending Then snippetPreviewTimer.Start()
    End Sub

    Private Sub RefreshSnippetPreviewSource()
        If snippetPreviewBox Is Nothing Then Return
        Dim canvas As B2SPictureBox = If(Backglass.currentTabPage IsNot Nothing, Backglass.currentTabPage.CurrentPictureBox, Nothing)
        If canvas Is Nothing Then
            snippetPreviewStatus.Text = "No backglass image is available for preview."
            SetSnippetPreviewImage(Nothing)
            Return
        End If

        Dim rendered As Bitmap = Nothing
        Dim pendingBrightness As Integer = Math.Max(0, Math.Min(200, CInt(numericBrightness.Value)))
        Dim pendingBehindCanvas As Boolean = chkBehindCanvas.Checked
        Try
            ' Compose the pending appearance values in the embedded preview
            ' without changing the live Designer canvas or committing project
            ' data. Every model value is restored before this method returns.
            For Each previewBulb As Illumination.BulbInfo In previewBulbs
                previewBulb.SnippitInfo.Brightness = pendingBrightness
                previewBulb.SnippitInfo.BehindCanvas = pendingBehindCanvas
                previewBulb.IsIlluminatedImageDirty = True
            Next
            rendered = canvas.CreateLayeredPreviewImage()
        Finally
            For Each previewBulb As Illumination.BulbInfo In previewBulbs
                If originalBrightness.ContainsKey(previewBulb) Then previewBulb.SnippitInfo.Brightness = originalBrightness(previewBulb)
                If originalBehindCanvas.ContainsKey(previewBulb) Then previewBulb.SnippitInfo.BehindCanvas = originalBehindCanvas(previewBulb)
                previewBulb.IsIlluminatedImageDirty = True
            Next
        End Try
        If rendered Is Nothing Then
            snippetPreviewStatus.Text = "The snippet preview could not be rendered."
            Return
        End If
        If snippetPreviewSource IsNot Nothing Then snippetPreviewSource.Dispose()
        snippetPreviewSource = rendered
        snippetPreviewStatus.Text = If(previewBulbs.Count > 1,
                                       "Live preview — " & previewBulbs.Count.ToString() & " selected snippets",
                                       "Live preview — selected snippet")
        RefreshSnippetPreviewImage()
    End Sub

    Private Sub RefreshSnippetPreviewImage()
        If snippetPreviewBox Is Nothing Then Return
        SetSnippetPreviewImage(RenderSnippetPreviewCanvas(snippetPreviewSource, snippetPreviewBox.ClientSize))
        If snippetPreviewZoomLabel IsNot Nothing Then
            snippetPreviewZoomLabel.Text = If(snippetPreviewZoomPercent <= 0, "Fit", snippetPreviewZoomPercent.ToString() & "%")
        End If
    End Sub

    Private Function RenderSnippetPreviewCanvas(ByVal source As Image, ByVal canvasSize As Size) As Bitmap
        If source Is Nothing OrElse canvasSize.Width <= 0 OrElse canvasSize.Height <= 0 Then Return Nothing
        Dim scale As Double = If(snippetPreviewZoomPercent <= 0,
                                 Math.Min(canvasSize.Width / CDbl(source.Width), canvasSize.Height / CDbl(source.Height)),
                                 snippetPreviewZoomPercent / 100.0R)
        scale = Math.Max(0.05R, Math.Min(8.0R, scale))
        Dim drawWidth As Integer = Math.Max(1, CInt(Math.Round(source.Width * scale)))
        Dim drawHeight As Integer = Math.Max(1, CInt(Math.Round(source.Height * scale)))
        Dim result As New Bitmap(canvasSize.Width, canvasSize.Height, Imaging.PixelFormat.Format32bppArgb)
        Using graphics As Graphics = Graphics.FromImage(result)
            graphics.Clear(Color.FromArgb(11, 12, 13))
            graphics.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
            graphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality
            graphics.CompositingQuality = Drawing2D.CompositingQuality.HighQuality
            Dim left As Integer = (canvasSize.Width - drawWidth) \ 2 + snippetPreviewPanOffset.X
            Dim top As Integer = (canvasSize.Height - drawHeight) \ 2 + snippetPreviewPanOffset.Y
            graphics.DrawImage(source, New Rectangle(left, top, drawWidth, drawHeight))
        End Using
        Return result
    End Function

    Private Sub SetSnippetPreviewImage(ByVal image As Image)
        If snippetPreviewBox Is Nothing Then
            If image IsNot Nothing Then image.Dispose()
            Return
        End If
        Dim oldImage As Image = snippetPreviewBox.Image
        snippetPreviewBox.Image = image
        If oldImage IsNot Nothing Then oldImage.Dispose()
    End Sub

    Private Sub SnippetPreviewZoomOut_Click(ByVal sender As Object, ByVal e As EventArgs)
        If snippetPreviewZoomPercent <= 0 Then snippetPreviewZoomPercent = GetSnippetFitZoomPercent()
        snippetPreviewZoomPercent = Math.Max(10, snippetPreviewZoomPercent - 25)
        RefreshSnippetPreviewImage()
    End Sub

    Private Sub SnippetPreviewZoomIn_Click(ByVal sender As Object, ByVal e As EventArgs)
        If snippetPreviewZoomPercent <= 0 Then snippetPreviewZoomPercent = GetSnippetFitZoomPercent()
        snippetPreviewZoomPercent = Math.Min(800, snippetPreviewZoomPercent + 25)
        RefreshSnippetPreviewImage()
    End Sub

    Private Sub SnippetPreviewFit_Click(ByVal sender As Object, ByVal e As EventArgs)
        snippetPreviewZoomPercent = 0
        snippetPreviewPanOffset = Point.Empty
        RefreshSnippetPreviewImage()
    End Sub

    Private Sub SnippetPreviewActual_Click(ByVal sender As Object, ByVal e As EventArgs)
        snippetPreviewZoomPercent = 100
        snippetPreviewPanOffset = Point.Empty
        RefreshSnippetPreviewImage()
    End Sub

    Private Sub SnippetPreview_MouseWheel(ByVal sender As Object, ByVal e As MouseEventArgs)
        If snippetPreviewBox Is Nothing Then Return
        Dim oldZoom As Integer = snippetPreviewZoomPercent
        If oldZoom <= 0 Then oldZoom = GetSnippetFitZoomPercent()
        Dim newZoom As Integer = If(e.Delta > 0, Math.Min(800, oldZoom + 25), If(e.Delta < 0, Math.Max(10, oldZoom - 25), oldZoom))
        If newZoom = oldZoom Then Return
        Dim canvasCenter As New Point(snippetPreviewBox.ClientSize.Width \ 2, snippetPreviewBox.ClientSize.Height \ 2)
        Dim relativeX As Double = e.X - canvasCenter.X - snippetPreviewPanOffset.X
        Dim relativeY As Double = e.Y - canvasCenter.Y - snippetPreviewPanOffset.Y
        Dim ratio As Double = newZoom / CDbl(oldZoom)
        snippetPreviewPanOffset = New Point(
            CInt(Math.Round(e.X - canvasCenter.X - relativeX * ratio)),
            CInt(Math.Round(e.Y - canvasCenter.Y - relativeY * ratio)))
        snippetPreviewZoomPercent = newZoom
        RefreshSnippetPreviewImage()
    End Sub

    Private Sub SnippetPreview_MouseDown(ByVal sender As Object, ByVal e As MouseEventArgs)
        If e.Button <> MouseButtons.Left OrElse snippetPreviewBox Is Nothing Then Return
        snippetPreviewIsPanning = True
        snippetPreviewPanStart = e.Location
        snippetPreviewPanStartOffset = snippetPreviewPanOffset
        snippetPreviewBox.Cursor = Cursors.Hand
        snippetPreviewBox.Capture = True
    End Sub

    Private Sub SnippetPreview_MouseMove(ByVal sender As Object, ByVal e As MouseEventArgs)
        If Not snippetPreviewIsPanning Then Return
        snippetPreviewPanOffset = New Point(snippetPreviewPanStartOffset.X + e.X - snippetPreviewPanStart.X,
                                            snippetPreviewPanStartOffset.Y + e.Y - snippetPreviewPanStart.Y)
        RefreshSnippetPreviewImage()
    End Sub

    Private Sub SnippetPreview_MouseUp(ByVal sender As Object, ByVal e As MouseEventArgs)
        If e.Button = MouseButtons.Left Then EndSnippetPreviewPan()
    End Sub

    Private Sub SnippetPreview_MouseLeave(ByVal sender As Object, ByVal e As EventArgs)
        If snippetPreviewIsPanning AndAlso Control.MouseButtons = MouseButtons.None Then EndSnippetPreviewPan()
    End Sub

    Private Sub SnippetPreview_MouseEnter(ByVal sender As Object, ByVal e As EventArgs)
        If snippetPreviewBox IsNot Nothing Then snippetPreviewBox.Focus()
    End Sub

    Private Sub SnippetPreview_DoubleClick(ByVal sender As Object, ByVal e As EventArgs)
        SnippetPreviewFit_Click(sender, e)
    End Sub

    Private Sub EndSnippetPreviewPan()
        snippetPreviewIsPanning = False
        If snippetPreviewBox IsNot Nothing Then
            snippetPreviewBox.Capture = False
            snippetPreviewBox.Cursor = Cursors.Default
        End If
    End Sub

    Private Function GetSnippetFitZoomPercent() As Integer
        If snippetPreviewSource Is Nothing OrElse snippetPreviewBox Is Nothing Then Return 100
        Dim scale As Double = Math.Min(snippetPreviewBox.ClientSize.Width / CDbl(snippetPreviewSource.Width),
                                       snippetPreviewBox.ClientSize.Height / CDbl(snippetPreviewSource.Height))
        Return Math.Max(10, Math.Min(800, CInt(Math.Round(scale * 100.0R))))
    End Function

    Private Sub SnippetSettings_FormClosed(ByVal sender As Object, ByVal e As FormClosedEventArgs)
        If snippetPreviewTimer IsNot Nothing Then snippetPreviewTimer.Stop()
        If snippetPreviewSource IsNot Nothing Then snippetPreviewSource.Dispose() : snippetPreviewSource = Nothing
        SetSnippetPreviewImage(Nothing)
    End Sub

    Private Sub AutomaticRotationChanged(sender As Object, e As EventArgs) Handles chkAutomaticRotation.CheckedChanged, radAutomaticRotationContinuous.CheckedChanged, radAutomaticRotationTriggered.CheckedChanged, numericAutomaticRotationTriggerID.ValueChanged, radAutomaticRotationLamp.CheckedChanged, radAutomaticRotationSolenoid.CheckedChanged, radAutomaticRotationB2S.CheckedChanged, radAutomaticRotationSW.CheckedChanged, cmbAutomaticRotationDirection.SelectedIndexChanged, numericAutomaticRotationSteps.ValueChanged, numericAutomaticRotationInterval.ValueChanged, cmbRotationStopBehaviour.SelectedIndexChanged, numericMechID.ValueChanged
        ' numericMechID is created by InitializeComponent before the rebuilt
        ' rotation panel exists, so its initial Minimum/Value events are ignored.
        If grpAutomaticRotation Is Nothing OrElse chkAutomaticRotation Is Nothing Then Return
        If ignoreChange Then Return
        ' Selecting any trigger-specific control is itself an explicit request
        ' for triggered mode. Do not let the project remain silently continuous
        ' after the author chooses Lamp, Solenoid, or edits the command ID.
        If Object.ReferenceEquals(sender, numericAutomaticRotationTriggerID) OrElse
           (Object.ReferenceEquals(sender, radAutomaticRotationLamp) AndAlso radAutomaticRotationLamp.Checked) OrElse
           (Object.ReferenceEquals(sender, radAutomaticRotationSolenoid) AndAlso radAutomaticRotationSolenoid.Checked) OrElse
           (Object.ReferenceEquals(sender, radAutomaticRotationB2S) AndAlso radAutomaticRotationB2S.Checked) OrElse
           (Object.ReferenceEquals(sender, radAutomaticRotationSW) AndAlso radAutomaticRotationSW.Checked) Then
            radAutomaticRotationTriggered.Checked = True
        End If
        IsDirty = True
        EnableDisable()
    End Sub

    Private Class AutomaticRotationStatusCheckBox
        Inherits CheckBox

        Public Sub New()
            SetStyle(ControlStyles.UserPaint Or
                     ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.ResizeRedraw, True)
        End Sub

        Protected Overrides Sub OnCheckedChanged(e As EventArgs)
            MyBase.OnCheckedChanged(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnEnabledChanged(e As EventArgs)
            MyBase.OnEnabledChanged(e)
            Invalidate()
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            MyBase.OnPaint(e)
            AppThemeManager.PaintStateCheckBox(Me, e)
        End Sub
    End Class

    Private Sub btnChangeImage_Click(sender As Object, e As EventArgs) Handles btnChangeImage.Click
        Dim bulb = Backglass.currentTabPage.Mouse.SelectedBulb
        If bulb IsNot Nothing Then
            Using fileDialog As OpenFileDialog = New OpenFileDialog
                With fileDialog
                    .Filter = ImageFileExtensionFilter
                    .FileName = String.Empty
                    .InitialDirectory = BackglassProjectsPath
                    If .ShowDialog(Me) = DialogResult.OK Then
                        Backglass.currentImages.RemoveByTypeAndName(Images.eImageInfoType.IlluminationSnippits, bulb.Name)

                        bulb.Image = Bitmap.FromFile(.FileName).Copy(True)
                        bulb.Name = IO.Path.GetFileNameWithoutExtension(.FileName)
                        bulb.Size.Width = bulb.Image.Width
                        bulb.Size.Height = bulb.Image.Height

                        Dim imageInfo As Images.ImageInfo = New Images.ImageInfo(Images.eImageInfoType.IlluminationSnippits)
                        imageInfo.Text = bulb.Name
                        imageInfo.Image = bulb.Image
                        Backglass.currentImages.Insert(Images.eImageInfoType.Title4IlluminationSnippits, imageInfo)

                        B2SBackglassDesigner.formDesigner.RefreshImageInfoList()
                        QueueSnippetPreview()
                    End If
                End With
            End Using
        End If
    End Sub

    Private Sub btnTrimImage_Click(sender As Object, e As EventArgs) Handles btnTrimImage.Click
        Dim selected_bulb = Backglass.currentTabPage.Mouse.SelectedBulb
        If selected_bulb IsNot Nothing AndAlso selected_bulb.Image IsNot Nothing Then
            If Not String.IsNullOrEmpty(selected_bulb.Name) AndAlso
               selected_bulb.Name.StartsWith("PA_", StringComparison.OrdinalIgnoreCase) Then
                B2SMessageBox.Show("This frame belongs to an imported picture animation. Its shared transparent canvas preserves frame-to-frame movement and cannot be trimmed individually.",
                                   "Picture Animation Protected", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim trim_rect As Rectangle = TrimImage(selected_bulb.Image)

            If trim_rect.X > 0 Or trim_rect.Y > 0 Or trim_rect.Width < selected_bulb.Image.Width Or trim_rect.Height < selected_bulb.Image.Height Then
                Dim trimmed As New Bitmap(trim_rect.Width, trim_rect.Height, selected_bulb.Image.PixelFormat)
                Graphics.FromImage(trimmed).DrawImage(selected_bulb.Image, New Rectangle(0, 0, trimmed.Width, trimmed.Height), trim_rect, System.Drawing.GraphicsUnit.Pixel)

                For Each bulb As Illumination.BulbInfo In Backglass.currentBulbs
                    If bulb.Name = selected_bulb.Name Then
                        bulb.Image = DirectCast(trimmed, Image)
                        bulb.Size.Width = bulb.Image.Width
                        bulb.Size.Height = bulb.Image.Height
                        bulb.Location += trim_rect.Location
                    End If
                Next

                Backglass.currentImages.SetNewImage(Images.eImageInfoType.IlluminationSnippits, selected_bulb.Name, selected_bulb.Image)
                B2SBackglassDesigner.formDesigner.RefreshImageInfoList()
                QueueSnippetPreview()
            End If
        End If
    End Sub

End Class
