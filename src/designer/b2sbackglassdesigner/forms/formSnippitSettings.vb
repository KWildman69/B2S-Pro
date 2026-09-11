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
    Private previewBulb As Illumination.BulbInfo = Nothing
    Private originalBrightness As Integer = 100
    Private originalBehindCanvas As Boolean = False

    Public Sub New()
        InitializeComponent()
        BuildAppearanceSection()
        BuildAutomaticRotationSection()
        TraceWindow("constructor after InitializeComponent")

        ' Build the complete B2S Pro frame while this dialog is still only a
        ' managed object. Waiting for SetVisibleCore/ShowDialog lets Windows
        ' expose the legacy 431x310 frame before the custom chrome expands it.
        AppThemeManager.Initialize()
        AppThemeManager.ApplyToForm(Me)
        TraceWindow("constructor after theme")

        ' Keep the already completed frame out of the compositor until its
        ' ShowDialog layout pass has also finished.
        Me.Opacity = 0.0R
        AddHandler Me.LocationChanged, Sub() TraceWindow("LocationChanged")
        AddHandler Me.SizeChanged, Sub() TraceWindow("SizeChanged")
        AddHandler Me.VisibleChanged, Sub() TraceWindow("VisibleChanged")
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
                                       ByRef automaticRotationStopBehaviour As eSnippitRotationStopBehaviour) As DialogResult

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
        previewBulb = Nothing
        If Backglass.currentTabPage IsNot Nothing AndAlso Backglass.currentTabPage.Mouse IsNot Nothing Then
            Dim selected As Illumination.BulbInfo = Backglass.currentTabPage.Mouse.SelectedBulb
            If selected IsNot Nothing AndAlso selected.ID = id Then previewBulb = selected
        End If
        originalBrightness = Math.Max(0, Math.Min(200, If(previewBulb IsNot Nothing, previewBulb.SnippitInfo.Brightness, brightness)))
        originalBehindCanvas = If(previewBulb IsNot Nothing, previewBulb.SnippitInfo.BehindCanvas, behindCanvas)
        brightness = originalBrightness

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
        chkBehindCanvas.Checked = originalBehindCanvas
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
        If displayedTriggerType <> eRomIDType.Lamp AndAlso displayedTriggerType <> eRomIDType.Solenoid Then displayedTriggerType = eRomIDType.Lamp
        radAutomaticRotationLamp.Checked = (displayedTriggerType = eRomIDType.Lamp)
        radAutomaticRotationSolenoid.Checked = (displayedTriggerType = eRomIDType.Solenoid)
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
            automaticRotationTriggerType = If(radAutomaticRotationSolenoid.Checked, eRomIDType.Solenoid, eRomIDType.Lamp)
            automaticRotationSteps = CInt(numericAutomaticRotationSteps.Value)
            automaticRotationInterval = CInt(numericAutomaticRotationInterval.Value)
            automaticRotationDirection = CType(cmbAutomaticRotationDirection.SelectedIndex, eSnippitRotationDirection)
            automaticRotationStopBehaviour = CType(cmbRotationStopBehaviour.SelectedIndex, eSnippitRotationStopBehaviour)
            ApplyBrightnessPreview(CInt(numericBrightness.Value))
        Else
            ApplyBrightnessPreview(originalBrightness)
            ApplyBehindCanvasPreview(originalBehindCanvas)
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
        If previewBulb Is Nothing Then Return
        value = Math.Max(0, Math.Min(200, value))
        If previewBulb.SnippitInfo.Brightness <> value Then
            previewBulb.SnippitInfo.Brightness = value
            previewBulb.IsIlluminatedImageDirty = True
        End If
        If Backglass.currentTabPage IsNot Nothing Then Backglass.currentTabPage.Refresh()
    End Sub

    Private Sub BehindCanvasChanged(sender As Object, e As EventArgs) Handles chkBehindCanvas.CheckedChanged
        If ignoreChange Then Return
        IsDirty = True
        ApplyBehindCanvasPreview(chkBehindCanvas.Checked)
    End Sub

    Private Sub ApplyBehindCanvasPreview(ByVal value As Boolean)
        If previewBulb Is Nothing Then Return
        If previewBulb.SnippitInfo.BehindCanvas <> value Then
            previewBulb.SnippitInfo.BehindCanvas = value
            previewBulb.IsIlluminatedImageDirty = True
        End If
        If Backglass.currentTabPage IsNot Nothing Then Backglass.currentTabPage.Refresh()
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
        pnlAutomaticRotationTriggerType.Controls.AddRange(New Control() {radAutomaticRotationLamp, radAutomaticRotationSolenoid})

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
        Controls.Add(grpAutomaticRotation)

        groupGeneral.Visible = False
        grpRotating.Visible = False

        Dim newButtonY As Integer = grpAutomaticRotation.Bottom + 8
        ClientSize = New Size(ClientSize.Width, newButtonY + btnOk.Height + 14)
        btnOk.Top = newButtonY
        btnCancel.Top = newButtonY
    End Sub

    Private Sub AutomaticRotationChanged(sender As Object, e As EventArgs) Handles chkAutomaticRotation.CheckedChanged, radAutomaticRotationContinuous.CheckedChanged, radAutomaticRotationTriggered.CheckedChanged, numericAutomaticRotationTriggerID.ValueChanged, radAutomaticRotationLamp.CheckedChanged, radAutomaticRotationSolenoid.CheckedChanged, cmbAutomaticRotationDirection.SelectedIndexChanged, numericAutomaticRotationSteps.ValueChanged, numericAutomaticRotationInterval.ValueChanged, cmbRotationStopBehaviour.SelectedIndexChanged, numericMechID.ValueChanged
        ' numericMechID is created by InitializeComponent before the rebuilt
        ' rotation panel exists, so its initial Minimum/Value events are ignored.
        If grpAutomaticRotation Is Nothing OrElse chkAutomaticRotation Is Nothing Then Return
        If ignoreChange Then Return
        ' Selecting any trigger-specific control is itself an explicit request
        ' for triggered mode. Do not let the project remain silently continuous
        ' after the author chooses Lamp, Solenoid, or edits the command ID.
        If Object.ReferenceEquals(sender, numericAutomaticRotationTriggerID) OrElse
           (Object.ReferenceEquals(sender, radAutomaticRotationLamp) AndAlso radAutomaticRotationLamp.Checked) OrElse
           (Object.ReferenceEquals(sender, radAutomaticRotationSolenoid) AndAlso radAutomaticRotationSolenoid.Checked) Then
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
            End If
        End If
    End Sub

End Class
