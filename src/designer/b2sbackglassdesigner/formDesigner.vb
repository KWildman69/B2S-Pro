Imports System
Imports System.IO
Imports System.Runtime.InteropServices

Public Class formDesigner

    Private zooms As String() = New String() {"500", "450", "400", "350", "300", "275", "250", "225", "200", "175", "150", "125", "110", "100", "90", "80", "75", "70", "67", "60", "50", "40", "33", "30", "25", "20", "10", "5"}

    Private WithEvents formToolReelsAndLEDs As formToolReelsAndLEDs = New formToolReelsAndLEDs()
    Private WithEvents formToolIllumination As formToolIllumination = New formToolIllumination()
    Private WithEvents formToolUndo As formToolUndo = New formToolUndo()
    Private WithEvents formToolResources As formToolResources = New formToolResources()
    Private WithEvents formToolLayers As formToolLayers = New formToolLayers()
    Private tsmiLayersEnhanced As ToolStripMenuItem
    Private tsmiLayerManagerView As ToolStripMenuItem
    ' B2S Pro integrated two-row header matching the approved visual target.
    Private b2sProHeader As TableLayoutPanel
    Private b2sProLogo As PictureBox
    Private b2sWindowMinimize As Button
    Private b2sWindowMaximize As Button
    Private b2sWindowClose As Button
    Private tsbAddSnippetEnhanced As ToolStripButton
    Private tsbMakeSnippetEnhanced As ToolStripButton
    Private tsbAddFlasherEnhanced As ToolStripButton
    Private tsbManageAnimationsEnhanced As ToolStripButton
    Private tsbChooseReelTypeEnhanced As ToolStripButton
    Private tsbBackglassBrightnessEnhanced As ToolStripButton
    Private tsbCreateDirectB2SEnhanced As ToolStripButton
    Private tsbBackglassPreviewEnhanced As ToolStripButton
    Private tsbIDTester As ToolStripButton
    Private mainNormalBounds As Rectangle = Rectangle.Empty
    Private previousMainWindowState As FormWindowState = FormWindowState.Normal
    Private correctingMainWindowBounds As Boolean

    Private Const WM_NCHITTEST As Integer = &H84
    Private Const WM_NCLBUTTONDOWN As Integer = &HA1
    Private Const HTCAPTION As Integer = 2
    Private Const HTCLIENT As Integer = 1
    Private Const HTLEFT As Integer = 10
    Private Const HTRIGHT As Integer = 11
    Private Const HTTOP As Integer = 12
    Private Const HTTOPLEFT As Integer = 13
    Private Const HTTOPRIGHT As Integer = 14
    Private Const HTBOTTOM As Integer = 15
    Private Const HTBOTTOMLEFT As Integer = 16
    Private Const HTBOTTOMRIGHT As Integer = 17
    Private Const WS_THICKFRAME As Integer = &H40000
    Private Const WS_MINIMIZEBOX As Integer = &H20000
    Private Const WS_MAXIMIZEBOX As Integer = &H10000
    Private Const WS_SYSMENU As Integer = &H80000

    Protected Overrides ReadOnly Property CreateParams As CreateParams
        Get
            Dim parameters As CreateParams = MyBase.CreateParams
            ' Preserve the custom borderless appearance while advertising the
            ' native resize/minimize/maximize capabilities Windows requires for
            ' Aero Snap and correct multi-monitor maximize behavior.
            parameters.Style = parameters.Style Or WS_THICKFRAME Or WS_MINIMIZEBOX Or WS_MAXIMIZEBOX Or WS_SYSMENU
            Return parameters
        End Get
    End Property

    <DllImport("user32.dll")>
    Private Shared Function ReleaseCapture() As Boolean
    End Function

    <DllImport("user32.dll")>
    Private Shared Function SendMessage(hWnd As IntPtr, msg As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
    End Function

    Private WithEvents formAddSnippit As formAddSnippit = New formAddSnippit()
    Private WithEvents formAnimations As formAnimations = New formAnimations()
    Private WithEvents formVPM As formVPM = New formVPM()

    Private save As Save = New Save()
    Private recent As Recent = New Recent()
    Private WithEvents coding As Coding = New Coding()

    Private WithEvents UndoEvents As Undo = New Undo()

    Private ignoreChanges As Boolean = False

    ' Enhanced 2.8.6: automatic recovery snapshots.
    Private WithEvents autoSaveTimer As New Windows.Forms.Timer()
    Private autoSaveInProgress As Boolean = False
    Private startupWorkspacePrepared As Boolean = False
    Private startupOpacity As Double = 1.0R
    Private startupPromptTimer As Timer
    Private startupFileOpened As Boolean = False
    Private objectSettingsDialogOpen As Boolean = False
    Private canvasZoomPreferenceReady As Boolean = False
    Private Const AutoSaveIntervalMinutes As Integer = 2
    Private Const AutoRecoverySuffix As String = "AutoRecovery"


#Region "constructor"

    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

        ' Keep the top-level window out of the desktop compositor until the saved
        ' bounds, theme, header, and native ToolStrip hosts have their final state.
        startupOpacity = Me.Opacity
        Me.Opacity = 0.0R

        ' Prevent native ToolStrip-hosted controls (combo boxes and text boxes)
        ' from painting their unfinished startup state directly onto the desktop.
        ' The main form remains visible so owned startup windows still behave normally.
        HideStartupToolbarHosts()

        ' Add any initialization after the InitializeComponent() call.
        InitializeEnhancedEditMenu()
        InitializeThemeMenu()
        InitializeB2SProVisuals()
        InitializeLayersPanel()
        InitializeAutoSave()
        ' Reparenting and toolbar styling can recreate the native hosted controls.
        ' Hide them again after all constructor-time header work is complete.
        HideStartupToolbarHosts()
        MyBase.SaveName = Me.Name

        ' Enhanced 3.0.13: attach the main form before its Load event so the
        ' monitor-aware layout restore always runs during normal startup.
        WindowStateManager.Attach(Me)

    End Sub

#End Region


    Private Sub HideStartupToolbarHosts()
        If tsB2SDesigner Is Nothing Then Return

        tsB2SDesigner.Visible = False
        For Each item As ToolStripItem In tsB2SDesigner.Items
            Dim host As ToolStripControlHost = TryCast(item, ToolStripControlHost)
            If host IsNot Nothing AndAlso host.Control IsNot Nothing Then
                host.Visible = False
                host.Control.Visible = False
            End If
        Next
    End Sub

    Private Sub RevealStartupToolbarHosts()
        If tsB2SDesigner Is Nothing Then Return

        For Each item As ToolStripItem In tsB2SDesigner.Items
            Dim host As ToolStripControlHost = TryCast(item, ToolStripControlHost)
            If host IsNot Nothing AndAlso host.Control IsNot Nothing Then
                host.Visible = True
                host.Control.Visible = True
            End If
        Next
        tsB2SDesigner.Visible = True
    End Sub

    Private Sub InitializeB2SProVisuals()
        Me.Text = "B2S Pro"
        Me.Font = New Font("Segoe UI", 9.0F, FontStyle.Regular)
        Me.FormBorderStyle = FormBorderStyle.None
        Me.Padding = New Padding(1)

        BuildB2SProHeader()

        ' Keep the existing image selector visible, but place it directly beside
        ' the lamp filter so it shares the compact lighting group instead of
        ' stretching a panel across the empty space beside Help.
        tsB2SDesigner.Items.Remove(tslImage)
        tsB2SDesigner.Items.Remove(tscmbImage)
        Dim imageSelectorIndex As Integer = tsB2SDesigner.Items.IndexOf(tscmbIDFilter) + 1
        tsB2SDesigner.Items.Insert(imageSelectorIndex, tslImage)
        tsB2SDesigner.Items.Insert(imageSelectorIndex + 1, tscmbImage)
        tslImage.Available = True
        tscmbImage.Available = True

        Dim stackedFieldIndex As Integer = tsB2SDesigner.Items.IndexOf(tslRomFilter)
        tsB2SDesigner.Items.Remove(tslRomFilter)
        tsB2SDesigner.Items.Remove(tscmbIDFilter)
        tsB2SDesigner.Items.Remove(tslImage)
        tsB2SDesigner.Items.Remove(tscmbImage)
        Dim lampFilterHost As ToolStripControlHost = CreateStackedToolbarField("Lamp filter", tscmbIDFilter, 108)
        Dim currentImageHost As ToolStripControlHost = CreateStackedToolbarField("Current image", tscmbImage, 108)
        tsB2SDesigner.Items.Insert(stackedFieldIndex, lampFilterHost)
        tsB2SDesigner.Items.Insert(stackedFieldIndex + 1, currentImageHost)

        ' Keep every command in its original location; only improve presentation.
        tsB2SDesigner.AutoSize = False
        tsB2SDesigner.Height = 76
        tsB2SDesigner.Padding = New Padding(10, 6, 10, 6)
        tsB2SDesigner.ImageScalingSize = New Size(26, 26)
        tsB2SDesigner.GripStyle = ToolStripGripStyle.Hidden
        tsB2SDesigner.LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow
        tsB2SDesigner.Stretch = True

        ' A flasher is an artwork-lighting object, not a normal lamp or generic
        ' snippet. Give it a dedicated creation path which opens the dedicated
        ' Flasher editor immediately.
        tsbAddFlasherEnhanced = New ToolStripButton() With {
            .Name = "tsbAddFlasherEnhanced",
            .ToolTipText = "Create a masked artwork flasher",
            .Enabled = tsmiAddNewBulbFrame.Enabled
        }
        AddHandler tsbAddFlasherEnhanced.Click, AddressOf AddNewFlasher_Click
        AddHandler tsmiAddNewBulbFrame.EnabledChanged,
            Sub() tsbAddFlasherEnhanced.Enabled = tsmiAddNewBulbFrame.Enabled
        Dim flasherInsertIndex As Integer = tsB2SDesigner.Items.IndexOf(tsbAddNewBulbFrame) + 1
        tsB2SDesigner.Items.Insert(flasherInsertIndex, tsbAddFlasherEnhanced)

        ' Add Snippet uses the same command path as the existing Illumination
        ' menu item, so both entry points open and update the identical dialog.
        tsbAddSnippetEnhanced = New ToolStripButton() With {
            .Name = "tsbAddSnippetEnhanced",
            .ToolTipText = "Add a new illumination snippet",
            .Enabled = tsmiAddANewIlluminationSnippit.Enabled
        }
        AddHandler tsbAddSnippetEnhanced.Click, AddressOf AddANewIlluminationSnippit_Click
        AddHandler tsmiAddANewIlluminationSnippit.EnabledChanged,
            Sub() tsbAddSnippetEnhanced.Enabled = tsmiAddANewIlluminationSnippit.Enabled
        Dim snippetInsertIndex As Integer = tsB2SDesigner.Items.IndexOf(tsbAddFlasherEnhanced) + 1
        tsB2SDesigner.Items.Insert(snippetInsertIndex, tsbAddSnippetEnhanced)

        ' Make Snippet captures pixels from the currently displayed Backglass or
        ' DMD image, then creates a normal movable snippet at the same location.
        tsbMakeSnippetEnhanced = New ToolStripButton() With {
            .Name = "tsbMakeSnippetEnhanced",
            .ToolTipText = "Mask pixels from the current image and make a movable snippet",
            .Enabled = tsmiAddANewIlluminationSnippit.Enabled
        }
        AddHandler tsbMakeSnippetEnhanced.Click, AddressOf MakeSnippetFromCurrentImage_Click
        AddHandler tsmiAddANewIlluminationSnippit.EnabledChanged,
            Sub() tsbMakeSnippetEnhanced.Enabled = tsmiAddANewIlluminationSnippit.Enabled
        tsB2SDesigner.Items.Insert(snippetInsertIndex + 1, tsbMakeSnippetEnhanced)

        ' Keep animation editing one click away on the main command toolbar.
        ' The button uses the existing menu command path so both entry points
        ' retain identical validation and window behavior.
        tsbManageAnimationsEnhanced = New ToolStripButton() With {
            .Name = "tsbManageAnimationsEnhanced",
            .ToolTipText = "Manage animations",
            .Enabled = tsmiManageAnimations.Enabled
        }
        AddHandler tsbManageAnimationsEnhanced.Click, AddressOf ManageAnimations_Click
        AddHandler tsmiManageAnimations.EnabledChanged,
            Sub() tsbManageAnimationsEnhanced.Enabled = tsmiManageAnimations.Enabled
        ' The animation command is intentionally isolated from the lighting
        ' controls so it reads as its own toolbar group.
        Dim animationInsertIndex As Integer = tsB2SDesigner.Items.IndexOf(ToolStripSeparator22) + 1
        tsB2SDesigner.Items.Insert(animationInsertIndex, tsbManageAnimationsEnhanced)
        tsB2SDesigner.Items.Insert(animationInsertIndex + 1,
                                   New ToolStripSeparator() With {.Name = "sepAnimationAfter"})

        ' Backglass brightness uses the existing Image > Brightness command,
        ' including its preview, undo and image replacement behavior. Keep it
        ' directly between Import Backglass Image and Show Illumination Frames.
        tsbBackglassBrightnessEnhanced = New ToolStripButton() With {
            .Name = "tsbBackglassBrightnessEnhanced",
            .ToolTipText = "Adjust backglass image brightness",
            .Enabled = tsmiBrightness.Enabled
        }
        AddHandler tsbBackglassBrightnessEnhanced.Click, AddressOf Brightness_Click
        AddHandler tsmiBrightness.EnabledChanged,
            Sub() tsbBackglassBrightnessEnhanced.Enabled = tsmiBrightness.Enabled
        Dim brightnessInsertIndex As Integer = tsB2SDesigner.Items.IndexOf(tsbImportBackgroundImage) + 1
        tsB2SDesigner.Items.Insert(brightnessInsertIndex, tsbBackglassBrightnessEnhanced)
        tsmiBrightness.Available = False

        ' Import Backglass Image, Brightness and Show Frames form one dedicated
        ' command group. Move the existing divider after Show Frames so Frames
        ' is no longer grouped with Add Reel and Scores.
        tsB2SDesigner.Items.Remove(ToolStripSeparator3)
        Dim imageFramesDividerIndex As Integer = tsB2SDesigner.Items.IndexOf(tsbShowScoreFrames) + 1
        tsB2SDesigner.Items.Insert(imageFramesDividerIndex, ToolStripSeparator3)

        tsbChooseReelTypeEnhanced = New ToolStripButton() With {
            .Name = "tsbChooseReelTypeEnhanced",
            .ToolTipText = "Choose reel or LED type",
            .Enabled = tsmiChooseReelType.Enabled
        }
        AddHandler tsbChooseReelTypeEnhanced.Click, AddressOf ChooseReelType_Click
        AddHandler tsmiChooseReelType.EnabledChanged,
            Sub() tsbChooseReelTypeEnhanced.Enabled = tsmiChooseReelType.Enabled
        Dim reelTypeInsertIndex As Integer = tsB2SDesigner.Items.IndexOf(tsbAddNewReelOrLEDFrame)
        tsB2SDesigner.Items.Insert(reelTypeInsertIndex, tsbChooseReelTypeEnhanced)

        ' Show Illumination Frames already has a dedicated button in the
        ' Import/Frames group. Remove the older duplicate LIGHTS button from
        ' the lighting group while retaining its menu command.
        tsbShowIlluFrames.Available = False

        ' Accurate Light Preview replaces the redundant standard preview button
        ' on the toolbar. The standard preview remains available from its menu.
        tsbShowIllumination.Available = False

        ' The toolbar slot formerly used for Help is now the Auto Save toggle.
        ' Help remains available from the regular Help menu.
        tsbHelp.Name = "tsbAutoSave"
        tsbHelp.CheckOnClick = True
        tsbHelp.Checked = True
        tsbHelp.ToolTipText = "Turn automatic recovery saves on or off"

        ' Two-step B2S Pro workflow, reusing the exact commands from the
        ' Backglass menu and keeping both actions in one dedicated group.
        tsbCreateDirectB2SEnhanced = New ToolStripButton() With {
            .Name = "tsbCreateDirectB2SBackglassFile",
            .Text = "STEP 1" & vbLf & "CREATE" & vbLf & "B2SPRO FILE",
            .ToolTipText = "Step 1: Create B2S Pro backglass file"
        }
        tsbBackglassPreviewEnhanced = New ToolStripButton() With {
            .Name = "tsbBackglassPreviewAndTest",
            .Text = "STEP 2" & vbLf & "BACKGLASS" & vbLf & "PREVIEW & TEST",
            .ToolTipText = "Step 2: Backglass Preview and Test"
        }
        AddHandler tsbCreateDirectB2SEnhanced.Click, AddressOf CreateDirectAccessBackglassCodeFile_Click
        AddHandler tsbBackglassPreviewEnhanced.Click, AddressOf BackglassPreviewAndTest_Click
        AddHandler tsmiCreateDirectAccessBackglassCodeFile.EnabledChanged,
            Sub() tsbCreateDirectB2SEnhanced.Enabled = tsmiCreateDirectAccessBackglassCodeFile.Enabled
        AddHandler tsmiBackglassPreviewAndTest.EnabledChanged,
            Sub() tsbBackglassPreviewEnhanced.Enabled = tsmiBackglassPreviewAndTest.Enabled
        Dim previewIndex As Integer = tsB2SDesigner.Items.IndexOf(tsbHelp)
        Dim previewDividerBefore As New ToolStripSeparator() With {.Name = "sepBackglassPreviewBefore"}
        Dim previewDividerAfter As New ToolStripSeparator() With {.Name = "sepBackglassPreviewAfter"}
        tsB2SDesigner.Items.Insert(previewIndex, previewDividerBefore)
        tsB2SDesigner.Items.Insert(previewIndex + 1, tsbCreateDirectB2SEnhanced)
        tsB2SDesigner.Items.Insert(previewIndex + 2, tsbBackglassPreviewEnhanced)

        ' Stage the embedded ID test backglass beside the matching VPX table.
        tsbIDTester = New ToolStripButton() With {
            .Name = "tsbIDTester",
            .Text = "ID" & vbLf & "TESTER",
            .ToolTipText = "Run the separate ID tester with a VPX table"
        }
        AddHandler tsbIDTester.Click, AddressOf IDTester_Click
        tsB2SDesigner.Items.Insert(previewIndex + 3, tsbIDTester)

        ' Keep the existing save/export progress control and its behavior, but
        ' place it where it remains visible beside the two-step output workflow.
        ssB2SDesigner.Items.Remove(tsProgress)
        tsProgress.ToolTipText = "Save and export progress"
        tsB2SDesigner.Items.Insert(previewIndex + 4, tsProgress)
        tsB2SDesigner.Items.Insert(previewIndex + 5, previewDividerAfter)

        ' Give every toolbar control breathing room.  This keeps the original
        ' controls and handlers intact while preventing the compressed look.
        For Each item As ToolStripItem In tsB2SDesigner.Items
            If TypeOf item Is ToolStripSeparator Then
                item.Margin = New Padding(8, 5, 8, 5)
            Else
                item.Margin = New Padding(4, 2, 4, 2)
                item.Padding = New Padding(2)
            End If

            If TypeOf item Is ToolStripComboBox Then
                Dim combo As ToolStripComboBox = DirectCast(item, ToolStripComboBox)
                combo.AutoSize = False
                combo.Width = Math.Max(combo.Width, 108)
            ElseIf TypeOf item Is ToolStripTextBox Then
                Dim textBox As ToolStripTextBox = DirectCast(item, ToolStripTextBox)
                textBox.AutoSize = False
                textBox.Width = Math.Max(textBox.Width, 138)
            End If
        Next

        ' Phase 2: apply the real grouped, glossy B2S Pro toolbar renderer while
        ' preserving every existing ToolStripItem and click handler.
        B2SProMainToolbar.Apply(tsB2SDesigner)

        ' Spread the existing toolbar controls across the full available header width.
        ' This only adjusts margins; every original item and click handler stays intact.
        AddHandler tsB2SDesigner.SizeChanged, AddressOf HeaderToolbar_SizeChanged
        AddHandler Me.Shown, AddressOf HeaderToolbar_FormShown

        msB2SDesigner.AutoSize = False
        msB2SDesigner.Height = 42
        ' The menu begins immediately beside the logo and uses the available
        ' header width instead of being squeezed into a narrow strip.
        msB2SDesigner.Padding = New Padding(12, 5, 8, 3)
        msB2SDesigner.Font = New Font("Segoe UI Semibold", 9.5F, FontStyle.Regular)
        msB2SDesigner.LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow
        msB2SDesigner.Stretch = True
        For Each menuItem As ToolStripItem In msB2SDesigner.Items
            menuItem.Margin = New Padding(6, 0, 6, 0)
            menuItem.Padding = New Padding(4, 0, 4, 0)
        Next
        B2SProMainToolbar.ApplyMenu(msB2SDesigner)

        ssB2SDesigner.AutoSize = False
        ssB2SDesigner.Height = 31
        ssB2SDesigner.Padding = New Padding(8, 3, 8, 3)
        ssB2SDesigner.SizingGrip = False
        ssB2SDesigner.Font = New Font("Segoe UI Semibold", 9.0F, FontStyle.Regular)

        tsLabelStatusInfo.ForeColor = Color.FromArgb(64, 226, 149)
        tsLabelFileInfo.ForeColor = Color.FromArgb(89, 184, 255)
        tsLabelFileSize.ForeColor = Color.FromArgb(255, 169, 65)
        tsLabelMarker.ForeColor = Color.FromArgb(206, 112, 255)
        B2SProMainToolbar.ApplyStatusBar(ssB2SDesigner)
    End Sub

    Private Function CreateStackedToolbarField(caption As String, source As ToolStripComboBox, fieldWidth As Integer) As ToolStripControlHost
        Dim field As New TableLayoutPanel With {
            .Name = "pnl" & caption.Replace(" ", String.Empty),
            .AutoSize = False,
            .Size = New Size(fieldWidth, 48),
            .RowCount = 2,
            .ColumnCount = 1,
            .Margin = Padding.Empty,
            .Padding = New Padding(2, 1, 2, 2),
            .BackColor = Color.FromArgb(2, 4, 8)
        }
        field.RowStyles.Add(New RowStyle(SizeType.Absolute, 15.0F))
        field.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        Dim heading As New Label With {
            .Text = caption,
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.BottomLeft,
            .Margin = Padding.Empty,
            .ForeColor = Color.FromArgb(205, 214, 232),
            .BackColor = Color.Transparent,
            .Font = New Font("Segoe UI Semibold", 6.5F, FontStyle.Regular)
        }
        Dim combo As ComboBox = source.ComboBox
        combo.Dock = DockStyle.Fill
        combo.Margin = Padding.Empty
        combo.FlatStyle = FlatStyle.Flat
        combo.BackColor = Color.FromArgb(3, 4, 7)
        combo.ForeColor = Color.White
        combo.Font = New Font("Segoe UI", 7.5F, FontStyle.Regular)
        field.Controls.Add(heading, 0, 0)
        field.Controls.Add(combo, 0, 1)
        combo.Visible = True
        combo.BringToFront()

        Return New ToolStripControlHost(field) With {
            .Name = "host" & caption.Replace(" ", String.Empty),
            .AutoSize = False,
            .Size = field.Size,
            .Margin = New Padding(3, 4, 3, 1),
            .Padding = Padding.Empty
        }
    End Function


    Private Sub HeaderToolbar_FormShown(sender As Object, e As EventArgs)
        ApplyHeaderToolbarSpacing()
    End Sub

    Private Sub HeaderToolbar_SizeChanged(sender As Object, e As EventArgs)
        ApplyHeaderToolbarSpacing()
    End Sub

    Private Sub ApplyHeaderToolbarSpacing()
        If tsB2SDesigner Is Nothing OrElse tsB2SDesigner.Items.Count = 0 Then Return
        If tsB2SDesigner.ClientSize.Width <= 0 Then Return
        tsB2SDesigner.PerformLayout()
        tsB2SDesigner.Invalidate()
    End Sub

    Private Sub BuildB2SProHeader()
        If b2sProHeader IsNot Nothing Then Return

        b2sProHeader = New TableLayoutPanel() With {
            .Name = "b2sProHeader",
            .Dock = DockStyle.Top,
            .Visible = False,
            .Height = 108,
            .RowCount = 2,
            .ColumnCount = 3,
            .Margin = Padding.Empty,
            .Padding = New Padding(0, 0, 0, 1),
            .BackColor = Color.FromArgb(13, 16, 27)
        }
        b2sProHeader.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 240.0F))
        b2sProHeader.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        b2sProHeader.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 126.0F))
        b2sProHeader.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        b2sProHeader.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        b2sProLogo = New PictureBox() With {
            .Name = "b2sProLogo",
            .Dock = DockStyle.Fill,
            .Margin = New Padding(4, 1, 4, 1),
            .BackColor = Color.FromArgb(13, 16, 27),
            .SizeMode = PictureBoxSizeMode.Zoom,
            .TabStop = False
        }
        b2sProLogo.Image = LoadB2SProLogoImage()
        AddHandler b2sProLogo.MouseDown, AddressOf MainHeader_MouseDown
        AddHandler b2sProLogo.DoubleClick, AddressOf MainHeader_DoubleClick
        AddHandler b2sProHeader.MouseDown, AddressOf MainHeader_MouseDown
        AddHandler b2sProHeader.DoubleClick, AddressOf MainHeader_DoubleClick

        Dim brandingPanel As New TableLayoutPanel With {
            .Name = "pnlB2SBranding",
            .Dock = DockStyle.Fill,
            .RowCount = 1,
            .ColumnCount = 1,
            .Margin = Padding.Empty,
            .Padding = New Padding(4, 2, 4, 2),
            .BackColor = Color.FromArgb(5, 7, 12)
        }
        AddHandler brandingPanel.MouseDown, AddressOf MainHeader_MouseDown
        AddHandler brandingPanel.DoubleClick, AddressOf MainHeader_DoubleClick
        brandingPanel.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        brandingPanel.Controls.Add(b2sProLogo, 0, 0)

        ' File commands remain available from the regular File menu. Do not
        ' duplicate them in the logo area or the main command ribbon.
        tsbNew.Available = False
        tsbOpen.Available = False
        tsbSave.Available = False
        sep0.Available = False

        Dim windowButtons As New FlowLayoutPanel With {
            .Name = "pnlMainWindowButtons",
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .Margin = Padding.Empty,
            .Padding = New Padding(0, 5, 5, 3),
            .BackColor = Color.FromArgb(1, 2, 4)
        }
        AddHandler windowButtons.MouseDown, AddressOf MainHeader_MouseDown
        AddHandler windowButtons.DoubleClick, AddressOf MainHeader_DoubleClick
        b2sWindowMinimize = CreateMainWindowButton("—", "Minimize")
        b2sWindowMaximize = CreateMainWindowButton("□", "Maximize / Restore")
        b2sWindowClose = CreateMainWindowButton("×", "Close")
        b2sWindowClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(185, 28, 45)
        AddHandler b2sWindowMinimize.Click, AddressOf MinimizeMainWindow
        AddHandler b2sWindowMaximize.Click, AddressOf ToggleMainWindowMaximize
        AddHandler b2sWindowClose.Click, Sub() Me.Close()
        windowButtons.Controls.AddRange(New Control() {b2sWindowMinimize, b2sWindowMaximize, b2sWindowClose})

        ' Reparent the existing controls so all commands and event handlers remain unchanged.
        msB2SDesigner.Dock = DockStyle.Fill
        msB2SDesigner.Margin = Padding.Empty
        msB2SDesigner.Location = Point.Empty
        tsB2SDesigner.Dock = DockStyle.Fill
        tsB2SDesigner.Margin = Padding.Empty
        tsB2SDesigner.Location = Point.Empty
        AddHandler msB2SDesigner.MouseDown, AddressOf MainMenuBlankArea_MouseDown
        AddHandler msB2SDesigner.DoubleClick, AddressOf MainMenuBlankArea_DoubleClick
        AddHandler tsB2SDesigner.MouseDown, AddressOf MainToolbarBlankArea_MouseDown

        b2sProHeader.Controls.Add(brandingPanel, 0, 0)
        b2sProHeader.SetRowSpan(brandingPanel, 2)
        b2sProHeader.Controls.Add(msB2SDesigner, 1, 0)
        b2sProHeader.Controls.Add(windowButtons, 2, 0)
        b2sProHeader.Controls.Add(tsB2SDesigner, 1, 1)
        b2sProHeader.SetColumnSpan(tsB2SDesigner, 2)

        Me.Controls.Add(b2sProHeader)
        b2sProHeader.BringToFront()
        ' Dock Fill must be laid out after the fixed header and status bar.
        ' Otherwise the canvas begins behind the header and its top is unreachable.
        B2STab.BringToFront()
        AddHandler b2sProHeader.Paint, AddressOf PaintCanvasBoundary
        Me.MainMenuStrip = msB2SDesigner
    End Sub

    Private Sub PaintCanvasBoundary(sender As Object, e As PaintEventArgs)
        Using divider As New Pen(Color.FromArgb(55, 110, 145))
            e.Graphics.DrawLine(divider, 0, b2sProHeader.ClientSize.Height - 1,
                                b2sProHeader.ClientSize.Width - 1, b2sProHeader.ClientSize.Height - 1)
        End Using
    End Sub

    Private Function CreateLogoFileButton(caption As String, icon As Image, handler As EventHandler) As Button
        Dim button As New Button With {
            .Name = "btnLogo" & caption.Replace(" ", String.Empty),
            .Text = caption,
            .Image = ResizeToolbarImage(icon, 14, 14),
            .ImageAlign = ContentAlignment.TopCenter,
            .TextAlign = ContentAlignment.BottomCenter,
            .TextImageRelation = TextImageRelation.ImageAboveText,
            .Size = New Size(44, 32),
            .Margin = New Padding(1, 0, 1, 0),
            .Padding = New Padding(1, 0, 1, 0),
            .FlatStyle = FlatStyle.Flat,
            .BackColor = Color.FromArgb(4, 7, 12),
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI Condensed", 6.25F, FontStyle.Regular),
            .TabStop = False,
            .UseVisualStyleBackColor = False
        }
        button.FlatAppearance.BorderColor = Color.FromArgb(140, 30, 149, 225)
        button.FlatAppearance.BorderSize = 1
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(62, 37, 53, 105)
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(105, 75, 35, 150)
        AddHandler button.Click, handler
        Return button
    End Function

    Private Function ResizeToolbarImage(source As Image, width As Integer, height As Integer) As Image
        If source Is Nothing Then Return Nothing
        Dim result As New Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb)
        Using graphics As Graphics = Graphics.FromImage(result)
            graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
            graphics.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
            graphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality
            graphics.DrawImage(source, New Rectangle(0, 0, width, height))
        End Using
        Return result
    End Function

    Private Function CreateMainWindowButton(caption As String, accessibleText As String) As Button
        Dim accent As Color
        If accessibleText.IndexOf("Close", StringComparison.OrdinalIgnoreCase) >= 0 Then
            accent = Color.FromArgb(255, 52, 72)
        ElseIf accessibleText.IndexOf("Maximize", StringComparison.OrdinalIgnoreCase) >= 0 Then
            accent = Color.FromArgb(255, 181, 38)
        Else
            accent = Color.FromArgb(38, 195, 255)
        End If
        Dim button As New MainWindowCommandButton(accent) With {
            .Text = caption,
            .AccessibleName = accessibleText,
            .Size = New Size(38, 29),
            .Margin = New Padding(2, 0, 2, 0),
            .FlatStyle = FlatStyle.Flat,
            .BackColor = Color.FromArgb(3, 5, 9),
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI Symbol", 10.0F, FontStyle.Bold),
            .TabStop = False,
            .UseVisualStyleBackColor = False
        }
        button.FlatAppearance.BorderSize = 0
        Return button
    End Function

    Private Class MainWindowCommandButton
        Inherits Button

        Private ReadOnly accent As Color
        Private hovered As Boolean
        Private pressed As Boolean

        Public Sub New(buttonAccent As Color)
            accent = buttonAccent
            SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        End Sub

        Protected Overrides Sub OnMouseEnter(e As EventArgs)
            hovered = True : Invalidate() : MyBase.OnMouseEnter(e)
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            hovered = False : pressed = False : Invalidate() : MyBase.OnMouseLeave(e)
        End Sub

        Protected Overrides Sub OnMouseDown(mevent As MouseEventArgs)
            pressed = True : Invalidate() : MyBase.OnMouseDown(mevent)
        End Sub

        Protected Overrides Sub OnMouseUp(mevent As MouseEventArgs)
            pressed = False : Invalidate() : MyBase.OnMouseUp(mevent)
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            e.Graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias
            e.Graphics.Clear(If(Parent Is Nothing, Color.FromArgb(1, 2, 4), Parent.BackColor))
            Dim bounds As New Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3))
            Using path As Drawing2D.GraphicsPath = RoundedButtonPath(bounds, 10)
                Dim topColor As Color = Color.FromArgb(If(hovered, 185, 112), accent)
                Dim bottomColor As Color = Color.FromArgb(If(pressed, 105, 24), accent)
                Using fill As New Drawing2D.LinearGradientBrush(bounds, topColor, bottomColor, Drawing2D.LinearGradientMode.Vertical)
                    e.Graphics.FillPath(fill, path)
                End Using
                If hovered Then
                    Using glow As New Pen(Color.FromArgb(105, accent), 4.0F)
                        e.Graphics.DrawPath(glow, path)
                    End Using
                End If
                Using border As New Pen(Color.FromArgb(240, accent), 1.2F)
                    e.Graphics.DrawPath(border, path)
                End Using
                Using gloss As New Pen(Color.FromArgb(150, Color.White), 1.0F)
                    e.Graphics.DrawLine(gloss, bounds.Left + 8, bounds.Top + 3, bounds.Right - 8, bounds.Top + 3)
                End Using
            End Using
            TextRenderer.DrawText(e.Graphics, Text, Font, bounds, Color.White,
                                  TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
        End Sub

        Private Shared Function RoundedButtonPath(bounds As Rectangle, radius As Integer) As Drawing2D.GraphicsPath
            Dim path As New Drawing2D.GraphicsPath()
            Dim diameter As Integer = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height))
            Dim arc As New Rectangle(bounds.X, bounds.Y, diameter, diameter)
            path.AddArc(arc, 180, 90)
            arc.X = bounds.Right - diameter : path.AddArc(arc, 270, 90)
            arc.Y = bounds.Bottom - diameter : path.AddArc(arc, 0, 90)
            arc.X = bounds.Left : path.AddArc(arc, 90, 90)
            path.CloseFigure()
            Return path
        End Function
    End Class

    Private Sub MainHeader_MouseDown(sender As Object, e As MouseEventArgs)
        If e.Button <> MouseButtons.Left OrElse Me.WindowState = FormWindowState.Minimized Then Return
        If Me.WindowState = FormWindowState.Maximized Then
            Dim cursorPoint As Point = Cursor.Position
            Dim restoreArea As Rectangle = VisibleMainNormalBounds(Screen.FromPoint(cursorPoint).WorkingArea)
            Dim restoredWidth As Integer = restoreArea.Width
            Dim relativeX As Double = Math.Max(0.15R, Math.Min(0.85R, CDbl(cursorPoint.X - Me.Bounds.Left) / Math.Max(1, Me.Bounds.Width)))
            Me.WindowState = FormWindowState.Normal
            Me.Bounds = restoreArea
            Me.Location = New Point(CInt(cursorPoint.X - restoredWidth * relativeX), Math.Max(Screen.FromPoint(cursorPoint).WorkingArea.Top, cursorPoint.Y - 14))
            If b2sWindowMaximize IsNot Nothing Then b2sWindowMaximize.Text = "□"
        End If
        ReleaseCapture()
        SendMessage(Me.Handle, WM_NCLBUTTONDOWN, CType(HTCAPTION, IntPtr), IntPtr.Zero)
    End Sub

    Private Sub MainMenuBlankArea_MouseDown(sender As Object, e As MouseEventArgs)
        If msB2SDesigner.GetItemAt(e.Location) Is Nothing Then MainHeader_MouseDown(sender, e)
    End Sub

    Private Sub MainMenuBlankArea_DoubleClick(sender As Object, e As EventArgs)
        Dim mousePoint As Point = msB2SDesigner.PointToClient(Cursor.Position)
        If msB2SDesigner.GetItemAt(mousePoint) Is Nothing Then MainHeader_DoubleClick(sender, e)
    End Sub

    Private Sub MainToolbarBlankArea_MouseDown(sender As Object, e As MouseEventArgs)
        If tsB2SDesigner.GetItemAt(e.Location) Is Nothing Then MainHeader_MouseDown(sender, e)
    End Sub

    Private Sub MainHeader_DoubleClick(sender As Object, e As EventArgs)
        ToggleMainWindowMaximize(sender, e)
    End Sub

    Private Sub ToggleMainWindowMaximize(sender As Object, e As EventArgs)
        If Me.WindowState = FormWindowState.Maximized Then
            Dim targetWorkArea As Rectangle = Screen.FromPoint(Cursor.Position).WorkingArea
            Me.WindowState = FormWindowState.Normal
            Me.Bounds = CenteredMainNormalBounds(targetWorkArea)
            If b2sWindowMaximize IsNot Nothing Then b2sWindowMaximize.Text = "□"
        Else
            RememberMainNormalBounds()
            ' Let Windows choose the working area belonging to this window.
            ' Supplying absolute multi-monitor coordinates here can maximize
            ' the borderless form on the wrong display.
            Me.MaximizedBounds = Rectangle.Empty
            Me.WindowState = FormWindowState.Maximized
            If b2sWindowMaximize IsNot Nothing Then b2sWindowMaximize.Text = "❐"
        End If
    End Sub

    Private Sub MinimizeMainWindow(sender As Object, e As EventArgs)
        RememberMainNormalBounds()
        Me.WindowState = FormWindowState.Minimized
    End Sub

    Private Sub RememberMainNormalBounds()
        If Me.WindowState <> FormWindowState.Normal OrElse Me.Width < 200 OrElse Me.Height < 150 Then Return
        mainNormalBounds = Me.Bounds
    End Sub

    Private Function VisibleMainNormalBounds(workArea As Rectangle) As Rectangle
        Dim candidate As Rectangle = mainNormalBounds
        If candidate.Width < 200 OrElse candidate.Height < 150 Then candidate = Me.RestoreBounds
        If candidate.Width < 200 OrElse candidate.Height < 150 Then candidate = New Rectangle(workArea.Left + 40, workArea.Top + 40, Math.Min(1280, workArea.Width - 80), Math.Min(800, workArea.Height - 80))

        candidate.Width = Math.Min(Math.Max(candidate.Width, Math.Max(640, Me.MinimumSize.Width)), workArea.Width)
        candidate.Height = Math.Min(Math.Max(candidate.Height, Math.Max(480, Me.MinimumSize.Height)), workArea.Height)
        candidate.X = Math.Max(workArea.Left, Math.Min(candidate.X, workArea.Right - candidate.Width))
        candidate.Y = Math.Max(workArea.Top, Math.Min(candidate.Y, workArea.Bottom - candidate.Height))
        mainNormalBounds = candidate
        Return candidate
    End Function

    Private Function CenteredMainNormalBounds(workArea As Rectangle) As Rectangle
        Dim candidate As Rectangle = VisibleMainNormalBounds(workArea)
        candidate.X = workArea.Left + Math.Max(0, (workArea.Width - candidate.Width) \ 2)
        candidate.Y = workArea.Top + Math.Max(0, (workArea.Height - candidate.Height) \ 2)
        mainNormalBounds = candidate
        Return candidate
    End Function

    Private Sub CorrectRestoredMainWindow()
        If correctingMainWindowBounds OrElse Me.IsDisposed OrElse Me.WindowState <> FormWindowState.Normal Then Return
        correctingMainWindowBounds = True
        Try
            Dim targetWorkArea As Rectangle = Screen.FromPoint(Cursor.Position).WorkingArea
            Me.Bounds = CenteredMainNormalBounds(targetWorkArea)
            Me.BringToFront()
            Me.Activate()
        Finally
            correctingMainWindowBounds = False
        End Try
    End Sub

    Protected Overrides Sub OnLocationChanged(e As EventArgs)
        MyBase.OnLocationChanged(e)
        RememberMainNormalBounds()
    End Sub

    Protected Overrides Sub OnSizeChanged(e As EventArgs)
        MyBase.OnSizeChanged(e)
        Dim restoredFromNonNormal As Boolean = (Me.WindowState = FormWindowState.Normal AndAlso previousMainWindowState <> FormWindowState.Normal)
        previousMainWindowState = Me.WindowState
        If restoredFromNonNormal AndAlso Me.IsHandleCreated Then
            Me.BeginInvoke(New MethodInvoker(AddressOf CorrectRestoredMainWindow))
            Return
        End If
        RememberMainNormalBounds()
    End Sub

    Protected Overrides Sub WndProc(ByRef m As Message)
        MyBase.WndProc(m)
        If m.Msg <> WM_NCHITTEST OrElse Me.WindowState <> FormWindowState.Normal OrElse CInt(m.Result) <> HTCLIENT Then Return

        Const grip As Integer = 9
        Dim point As Point = Me.PointToClient(Cursor.Position)
        Dim leftEdge As Boolean = point.X <= grip
        Dim rightEdge As Boolean = point.X >= Me.ClientSize.Width - grip
        Dim topEdge As Boolean = point.Y <= grip
        Dim bottomEdge As Boolean = point.Y >= Me.ClientSize.Height - grip

        If leftEdge AndAlso topEdge Then
            m.Result = CType(HTTOPLEFT, IntPtr)
        ElseIf rightEdge AndAlso topEdge Then
            m.Result = CType(HTTOPRIGHT, IntPtr)
        ElseIf leftEdge AndAlso bottomEdge Then
            m.Result = CType(HTBOTTOMLEFT, IntPtr)
        ElseIf rightEdge AndAlso bottomEdge Then
            m.Result = CType(HTBOTTOMRIGHT, IntPtr)
        ElseIf leftEdge Then
            m.Result = CType(HTLEFT, IntPtr)
        ElseIf rightEdge Then
            m.Result = CType(HTRIGHT, IntPtr)
        ElseIf topEdge Then
            m.Result = CType(HTTOP, IntPtr)
        ElseIf bottomEdge Then
            m.Result = CType(HTBOTTOM, IntPtr)
        End If
    End Sub

    Private Function LoadB2SProLogoImage() As Image
        ' Load the logo from the executable first. This prevents a missing-file black box.
        Try
            Dim assembly As Reflection.Assembly = Reflection.Assembly.GetExecutingAssembly()
            Using stream As Stream = assembly.GetManifestResourceStream("B2SProLogo.png")
                If stream IsNot Nothing Then
                    Using source As Image = Image.FromStream(stream)
                        Return New Bitmap(source)
                    End Using
                End If
            End Using
        Catch
            ' Fall through to disk locations. Branding must never stop startup.
        End Try

        Dim candidates As String() = {
            Path.Combine(Application.StartupPath, "Resources", "B2SProLogo.png"),
            Path.Combine(Application.StartupPath, "B2SProLogo.png"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "B2SProLogo.png")
        }

        For Each fileName As String In candidates
            If Not File.Exists(fileName) Then Continue For
            Try
                Using stream As New FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
                    Using source As Image = Image.FromStream(stream)
                        Return New Bitmap(source)
                    End Using
                End Using
            Catch
                ' Try the next location.
            End Try
        Next

        Return Nothing
    End Function

    Private Sub InitializeThemeMenu()
        AppThemeManager.Initialize()
        ' The approved black-glass interface is now the application's permanent
        ' appearance.  The legacy setting remains in the settings schema only so
        ' existing user configuration files stay compatible.
        AppThemeManager.SetDarkMode(True)
    End Sub

    Private Sub InitializeLayersPanel()
        ' Put the Z-order manager in both menus so it is easy to find.
        tsmiLayersEnhanced = New ToolStripMenuItem("Layers") With {
            .CheckOnClick = True,
            .Checked = True,
            .ShortcutKeys = Keys.Control Or Keys.L,
            .ShowShortcutKeys = True
        }
        AddHandler tsmiLayersEnhanced.Click, AddressOf LayersMenu_Click
        tsmiWindow.DropDownItems.Add(New ToolStripSeparator())
        tsmiWindow.DropDownItems.Add(tsmiLayersEnhanced)

    End Sub

    Private Sub LayerManagerView_Click(sender As Object, e As EventArgs)
        ShowLayersPanel()
        If formToolLayers IsNot Nothing Then
            formToolLayers.Activate()
            formToolLayers.BringToFront()
        End If
    End Sub

    Private Sub LayersMenu_Click(sender As Object, e As EventArgs)
        If tsmiLayersEnhanced.Checked Then
            ShowLayersPanel()
        ElseIf formToolLayers IsNot Nothing Then
            WindowStateManager.SaveNow(formToolLayers)
            formToolLayers.Hide()
        End If
    End Sub

    Private Sub ShowLayersPanel()
        If formToolLayers Is Nothing OrElse formToolLayers.IsDisposed Then formToolLayers = New formToolLayers() : WindowStateManager.Attach(formToolLayers)
        If Not formToolLayers.Visible Then
            Dim hasSavedLayout As Boolean = WindowStateManager.HasSavedState(formToolLayers) OrElse formToolLayers.HasSavedLayout
            formToolLayers.Show(Me)
            If Not hasSavedLayout Then
                formToolLayers.Location = New Point(Math.Max(Me.Left, Me.Right - formToolLayers.Width - 20), Me.Top + 100)
            End If
        End If
        formToolLayers.RefreshLayers()
        If tsmiLayersEnhanced IsNot Nothing Then tsmiLayersEnhanced.Checked = True
    End Sub

    Private Sub formToolLayers_FormClosed(sender As Object, e As FormClosedEventArgs) Handles formToolLayers.FormClosed
        If tsmiLayersEnhanced IsNot Nothing Then tsmiLayersEnhanced.Checked = False
    End Sub

    Private Sub InitializeAutoSave()
        autoSaveTimer.Interval = AutoSaveIntervalMinutes * 60 * 1000
        autoSaveTimer.Enabled = True
        tsbHelp.Checked = True
    End Sub

    Private Sub AutoSaveToolbar_Click(sender As Object, e As EventArgs) Handles tsbHelp.Click
        autoSaveTimer.Enabled = tsbHelp.Checked
        tsbHelp.ToolTipText = If(autoSaveTimer.Enabled, "Auto Save is ON", "Auto Save is OFF")
        tsB2SDesigner.Invalidate()
        ShowStatus(If(autoSaveTimer.Enabled, "Auto Save turned on", "Auto Save turned off"))
    End Sub

    Private Sub AutoSaveTimer_Tick(sender As Object, e As EventArgs) Handles autoSaveTimer.Tick
        CreateRecoverySnapshot()
    End Sub

    Private Sub CreateRecoverySnapshot()
        If autoSaveInProgress OrElse Backglass.currentData Is Nothing OrElse Not Backglass.currentData.IsDirty Then Return
        If String.IsNullOrWhiteSpace(Backglass.currentData.Name) Then Return

        ' Do not interrupt a drag or a modal settings edit with a full export.
        If Control.MouseButtons <> MouseButtons.None OrElse
           Application.OpenForms.Cast(Of Form)().Any(Function(openForm) openForm.Modal) Then Return
        If Backglass.currentData.RecoveryChangeVersion = Backglass.currentData.ChangeVersion AndAlso
           Not String.IsNullOrEmpty(Backglass.currentData.RecoverySourceFilePath) AndAlso
           IO.File.Exists(Backglass.currentData.RecoverySourceFilePath) Then Return

        autoSaveInProgress = True
        Dim data As Backglass.Data = Backglass.currentData
        Dim wasDirty As Boolean = data.IsDirty
        Try
            Dim recoveryFile As String = If(IsB2SProRecoveryPath(data.RecoverySourceFilePath),
                                            data.RecoverySourceFilePath,
                                            RecoveryFileFor(data))
            If Not IsB2SProRecoveryPath(recoveryFile) Then Throw New InvalidOperationException("The AutoRecovery destination is invalid.")
            Dim saveStartedUtc As DateTime = DateTime.UtcNow
            If coding.CreateB2SProFile(recoveryFile, isRecoverySnapshot:=True) AndAlso
               IO.File.Exists(recoveryFile) AndAlso
               IO.File.GetLastWriteTimeUtc(recoveryFile) >= saveStartedUtc.AddSeconds(-2) Then
                data.RecoverySourceFilePath = IO.Path.GetFullPath(recoveryFile)
                data.RecoveryChangeVersion = data.ChangeVersion
                ShowStatus("Auto-recovery copy saved at " & DateTime.Now.ToShortTimeString())
            Else
                ShowStatus("Auto-recovery was not saved; please save your backglass manually")
            End If
        Catch ex As Exception
            Debug.WriteLine("Auto-recovery failed: " & ex.Message)
            ShowStatus("Auto-recovery failed; please save your backglass manually")
        Finally
            If data.IsDirty <> wasDirty Then data.IsDirty = wasDirty
            autoSaveInProgress = False
        End Try
    End Sub

    Private Function B2SProFileFor(ByVal data As Backglass.Data) As String
        If data Is Nothing OrElse String.IsNullOrWhiteSpace(data.Name) Then Return String.Empty
        Dim fileName As String = If(String.IsNullOrWhiteSpace(data.VSName), data.Name, data.VSName) & B2SProFileExtension
        Return IO.Path.Combine(BackglassProjectsPath, data.Name, fileName)
    End Function

    Private Function RecoveryFileFor(ByVal data As Backglass.Data,
                                     Optional ByVal sourceFilename As String = "") As String
        If data Is Nothing OrElse String.IsNullOrWhiteSpace(data.Name) Then Return String.Empty
        Dim source As String = If(String.IsNullOrWhiteSpace(sourceFilename), data.SourceFilePath, sourceFilename)
        Dim identity As String = If(String.IsNullOrWhiteSpace(source),
                                    "new:" & data.ProjectGUID,
                                    "file:" & IO.Path.GetFullPath(source).ToUpperInvariant())
        Dim token As String
        Using sha = Security.Cryptography.SHA256.Create()
            token = BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(identity))).Replace("-", "").Substring(0, 12)
        End Using
        Dim projectName As String = If(String.IsNullOrWhiteSpace(data.LoadedName), data.Name, data.LoadedName)
        Dim fileBase As String = If(String.IsNullOrWhiteSpace(source),
                                    "Unsaved_" & If(String.IsNullOrWhiteSpace(data.VSName), projectName, data.VSName),
                                    IO.Path.GetFileNameWithoutExtension(source))
        For Each invalidCharacter As Char In IO.Path.GetInvalidFileNameChars()
            fileBase = fileBase.Replace(invalidCharacter, "_"c)
        Next
        Return IO.Path.Combine(BackglassProjectsPath,
                               projectName,
                               AutoRecoverySuffix,
                               fileBase & "_" & AutoRecoverySuffix & "_" & token & B2SProFileExtension)
    End Function

    Private Function IsB2SProRecoveryPath(ByVal filename As String) As Boolean
        If String.IsNullOrWhiteSpace(filename) Then Return False
        Try
            Dim fullPath As String = IO.Path.GetFullPath(filename)
            Dim projectsRoot As String = IO.Path.GetFullPath(BackglassProjectsPath).TrimEnd(IO.Path.DirectorySeparatorChar)
            Return fullPath.StartsWith(projectsRoot & IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) AndAlso
                   IO.Path.GetExtension(fullPath).Equals(B2SProFileExtension, StringComparison.OrdinalIgnoreCase) AndAlso
                   IO.Path.GetFileNameWithoutExtension(fullPath).IndexOf("_" & AutoRecoverySuffix & "_", StringComparison.OrdinalIgnoreCase) >= 0 AndAlso
                   IO.Path.GetFileName(IO.Path.GetDirectoryName(fullPath)).Equals(AutoRecoverySuffix, StringComparison.OrdinalIgnoreCase)
        Catch
            Return False
        End Try
    End Function

    Private Sub RemoveRecoveryCopy(ByVal data As Backglass.Data)
        If data Is Nothing OrElse Not IsB2SProRecoveryPath(data.RecoverySourceFilePath) Then Return
        Dim filename As String = data.RecoverySourceFilePath
        Try
            If IO.File.Exists(filename) Then IO.File.Delete(filename)
            Dim folder As String = IO.Path.GetDirectoryName(filename)
            If IO.Directory.Exists(folder) AndAlso IO.Directory.GetFileSystemEntries(folder).Length = 0 Then
                IO.Directory.Delete(folder)
            End If
            data.RecoverySourceFilePath = String.Empty
        Catch ex As Exception
            Debug.WriteLine("Could not remove auto-recovery copy: " & ex.Message)
            ShowStatus("Could not remove the auto-recovery copy: " & IO.Path.GetFileName(filename))
        End Try
    End Sub

    Private Sub OfferUnsavedRecoveryCopies()
        If Not IO.Directory.Exists(BackglassProjectsPath) Then Return
        For Each projectFolder As String In IO.Directory.GetDirectories(BackglassProjectsPath)
            Dim recoveryFolder As String = IO.Path.Combine(projectFolder, AutoRecoverySuffix)
            If Not IO.Directory.Exists(recoveryFolder) Then Continue For
            For Each recoveryFile As String In IO.Directory.GetFiles(recoveryFolder, "Unsaved_*_" & AutoRecoverySuffix & "_*.B2SPro")
                If Not IsB2SProRecoveryPath(recoveryFile) Then Continue For
                Try
                    Dim recoveryData As Backglass.Data = Nothing
                    If Not coding.ImportBackglassFile(recoveryData, recoveryFile, preserveEmbeddedVSName:=True) OrElse
                       recoveryData Is Nothing Then Continue For
                    recoveryData.RecoverySourceFilePath = recoveryFile
                    Dim answer As DialogResult = B2SMessageBox.Show(Me,
                        "An AutoRecovery copy was found for the unsaved backglass " & recoveryData.Name & "." & Environment.NewLine & Environment.NewLine &
                        "Yes opens the recovered work. No discards the recovery copy.",
                        AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                    If answer = DialogResult.Yes Then
                        LoadData(recoveryData)
                        If Object.ReferenceEquals(Backglass.currentData, recoveryData) Then recoveryData.IsDirty = True
                    Else
                        RemoveRecoveryCopy(recoveryData)
                    End If
                Catch ex As Exception
                    Debug.WriteLine("Could not load unsaved AutoRecovery copy: " & ex.Message)
                    ShowStatus("An AutoRecovery copy could not be read; it was kept for inspection")
                End Try
            Next
        Next
    End Sub

    Private Function SaveB2SPro(ByVal data As Backglass.Data,
                                Optional ByVal markClean As Boolean = True,
                                Optional ByVal outputFilename As String = "") As Boolean
        If data Is Nothing OrElse Not Object.ReferenceEquals(data, Backglass.currentData) Then Return False

        Dim target As String = If(String.IsNullOrWhiteSpace(outputFilename), B2SProFileFor(data), B2SProFileName(IO.Path.GetFullPath(outputFilename)))
        If String.IsNullOrWhiteSpace(target) Then Return False

        Dim saveStartedUtc As DateTime = DateTime.UtcNow
        TraceMotionPersistence("Saving B2S Pro file: " & target)
        If Not coding.CreateB2SProFile(target) Then Return False
        If Not IO.File.Exists(target) OrElse IO.File.GetLastWriteTimeUtc(target) < saveStartedUtc.AddSeconds(-2) Then
            TraceMotionPersistence("B2S Pro save could not be verified: " & target)
            Return False
        End If

        data.BackupName = String.Empty
        RemoveRecoveryCopy(data)
        data.SourceFilePath = IO.Path.GetFullPath(target)
        data.LoadedName = data.Name
        data.IsDirty = Not markClean
        recent.AddToRecentList(data, target)
        TraceMotionPersistence("B2S Pro save verified: " & target)
        Return True
    End Function

#Region "events"

    Private Sub formDesigner_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load
        ' allow drag and drop
        Me.AllowDrop = True
        ' start app title and status bar
        Me.Text = Headline
        'Me.KeyPreview = True
        ShowStatus()

        ' initialize some values
        For Each zoom As String In zooms
            tscmbZoomInPercent.Items.Add(zoom & "%")
        Next
        tscmbZoomInPercent.Items.Add(My.Resources.TXT_ZoomWindow)
        ' InitializeComponent assigns the legacy 100% resource text first.
        ' Do not allow that designer-time default to overwrite the persisted zoom.
        tscmbZoomInPercent.Text = RestoredCanvasZoom()
        canvasZoomPreferenceReady = True

        ' listbox to undo
        Undo.ListBox = formToolUndo.lbHistory

        ' This handler runs before WindowStateManager's Load handler. Complete the
        ' hidden header now so restoring/maximizing the saved window can only paint
        ' the finished toolbar, never its native hosted-control setup frames.
        PrepareRecoveryPromptWorkspace()
    End Sub

    Private Function RestoredCanvasZoom() As String
        Dim saved As String = "100%"
        Try
            If XmlSettings Is Nothing Then
                XmlSettings = New Xml.XmlDocument()
                If IO.File.Exists(SettingsFileName) Then
                    XmlSettings.Load(SettingsFileName)
                Else
                    XmlSettings.LoadXml(My.Resources.B2SBackglassDesigner_Settings_xml)
                End If
            End If
            Dim node As Xml.XmlNode = XmlSettings.SelectSingleNode("B2SBackglassDesignerSettings/FormSettings/formDesigner/CanvasZoom")
            If node IsNot Nothing AndAlso node.Attributes("Value") IsNot Nothing Then saved = node.Attributes("Value").Value
        Catch ex As Exception
            Debug.WriteLine("Could not read canvas zoom: " & ex.Message)
        End Try
        saved = saved.Trim()
        If saved.Equals(My.Resources.TXT_ZoomWindow, StringComparison.CurrentCultureIgnoreCase) OrElse
           saved.Equals("Window", StringComparison.CurrentCultureIgnoreCase) Then
            Return My.Resources.TXT_ZoomWindow
        End If

        Dim numericText As String = saved.Replace("%", String.Empty).Trim()
        Dim zoomValue As Integer
        If Integer.TryParse(numericText, zoomValue) Then
            zoomValue = Math.Max(5, Math.Min(500, zoomValue))
            Return zoomValue.ToString() & "%"
        End If
        Return "100%"
    End Function

    Private Sub SaveCanvasZoom(ByVal value As String)
        Try
            If XmlSettings Is Nothing Then RestoredCanvasZoom()
            Dim designerNode As Xml.XmlElement = TryCast(XmlSettings.SelectSingleNode("B2SBackglassDesignerSettings/FormSettings/formDesigner"), Xml.XmlElement)
            If designerNode Is Nothing Then Return
            Dim zoomNode As Xml.XmlElement = TryCast(designerNode.SelectSingleNode("CanvasZoom"), Xml.XmlElement)
            If zoomNode Is Nothing Then
                zoomNode = XmlSettings.CreateElement("CanvasZoom")
                designerNode.AppendChild(zoomNode)
            End If
            zoomNode.SetAttribute("Value", value)
            XmlSettings.Save(SettingsFileName)
        Catch ex As Exception
            Debug.WriteLine("Could not save canvas zoom: " & ex.Message)
        End Try
    End Sub

    Private Sub CanvasZoomTextChanged(sender As Object, e As EventArgs) Handles tscmbZoomInPercent.TextChanged
        If Not canvasZoomPreferenceReady Then Return
        Dim value As String = tscmbZoomInPercent.Text.Trim()
        If value.Equals(My.Resources.TXT_ZoomWindow, StringComparison.CurrentCultureIgnoreCase) OrElse
           value.Equals("Window", StringComparison.CurrentCultureIgnoreCase) Then
            SaveCanvasZoom(My.Resources.TXT_ZoomWindow)
            Return
        End If

        Dim zoomValue As Integer
        If Integer.TryParse(value.Replace("%", String.Empty).Trim(), zoomValue) AndAlso zoomValue >= 5 AndAlso zoomValue <= 500 Then
            SaveCanvasZoom(zoomValue.ToString() & "%")
        End If
    End Sub

    Private Sub formDesigner_Shown(sender As Object, e As System.EventArgs) Handles Me.Shown
        ' The header is finalized in OnLoad so no unfinished ToolStrip hosts can
        ' appear during the first top-level paint.
        Me.BringToFront()
        Me.Activate()
        Me.Refresh()
        Me.Update()

        ' The completed frame is now ready. Restore the normal design-time opacity
        ' before any startup warning, recovery prompt, or tool window is displayed.
        Me.Opacity = startupOpacity

        ' Showing an owned modal dialog from Shown blocks the first normal message-
        ' loop paint. The native ToolStrip hosts then remain in their temporary
        ' startup positions until the dialog closes. Give the completed main form
        ' one real displayed frame before checking for recovery files.
        Me.PerformLayout()
        If b2sProHeader IsNot Nothing Then b2sProHeader.PerformLayout()
        If tsB2SDesigner IsNot Nothing Then
            tsB2SDesigner.PerformLayout()
            tsB2SDesigner.Invalidate(True)
        End If
        Me.Invalidate(True)
        Me.Update()

        startupPromptTimer = New Timer() With {.Interval = 100}
        AddHandler startupPromptTimer.Tick, AddressOf FinishStartupAfterFirstPaint
        startupPromptTimer.Start()
    End Sub

    Private Sub FinishStartupAfterFirstPaint(sender As Object, e As EventArgs)
        If startupPromptTimer IsNot Nothing Then
            startupPromptTimer.Stop()
            RemoveHandler startupPromptTimer.Tick, AddressOf FinishStartupAfterFirstPaint
            startupPromptTimer.Dispose()
            startupPromptTimer = Nothing
        End If

        ' Check that the settings file can still be written while the completed
        ' designer remains visible behind any warning dialog.
        Try
            Dim XmlSettings As Xml.XmlDocument = New Xml.XmlDocument
            If IO.File.Exists(SettingsFileName) Then
                XmlSettings.Load(SettingsFileName)
                XmlSettings.Save(SettingsFileName)
            End If
        Catch ex As Exception
            B2SMessageBox.Show(Me, String.Format(My.Resources.MSG_StartupSaveError, ex.Message), AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
        End Try

        Dim startupFile As String = StartupFileArgument()

        ' Open the saved tool-window set only after startup is done.
        OpenStartupToolWindows()

        ' Windows file associations and Open With pass the selected file as a
        ' command-line argument. Route it through the same proven loaders used by
        ' File/Open, Recent Files, and drag-and-drop after the workspace is ready.
        If Not String.IsNullOrEmpty(startupFile) Then
            OpenStartupFile(startupFile)
        Else
            OfferUnsavedRecoveryCopies()
        End If

        ' Theme the newly opened windows and keep every owned startup window on
        ' the designer's current monitor.
        For Each openForm As Form In Application.OpenForms
            AppThemeManager.ApplyToForm(openForm)
        Next
        WindowStateManager.KeepOwnedFormsOnOwnerScreen(Me)

        Me.Focus()
        Me.BringToFront()
        B2STab.Focus()
        tscmbZoomInPercent.ComboBox.SelectionLength = 0
        StartUpdateCheck(False)
    End Sub

    Private Sub StartUpdateCheck(ByVal interactive As Boolean)
        Try
            Dim checkerPath As String = IO.Path.Combine(Application.StartupPath, "B2SUpdateChecker.exe")
            If Not IO.File.Exists(checkerPath) Then
                If interactive Then Throw New IO.FileNotFoundException("The update checker is missing. Please rerun B2S Pro setup to restore it.", checkerPath)
                Return
            End If

            Dim start As New ProcessStartInfo() With {
                .FileName = checkerPath,
                .Arguments = If(interactive, "--check-now designer", "--automatic designer"),
                .WorkingDirectory = Application.StartupPath,
                .UseShellExecute = False,
                .CreateNoWindow = True,
                .WindowStyle = ProcessWindowStyle.Hidden
            }
            Using checker As Process = Process.Start(start)
            End Using
        Catch ex As Exception
            ' Update availability must never interrupt Designer startup.
            If interactive Then B2SMessageBox.Show(Me, "The update check could not be started." & Environment.NewLine & Environment.NewLine & ex.Message,
                                                 AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Function StartupFileArgument() As String
        For Each argument As String In My.Application.CommandLineArgs
            If String.IsNullOrWhiteSpace(argument) Then Continue For
            Dim filename As String = argument.Trim().Trim(""""c)
            If Not IO.File.Exists(filename) Then Continue For

            Select Case IO.Path.GetExtension(filename).ToLowerInvariant()
                Case B2SProFileExtension.ToLowerInvariant(), LegacyDirectB2SFileExtension
                    Return IO.Path.GetFullPath(filename)
            End Select
        Next
        Return Nothing
    End Function

    Private Sub OpenStartupFile(ByVal filename As String)
        If startupFileOpened Then Return
        startupFileOpened = True

        Select Case IO.Path.GetExtension(filename).ToLowerInvariant()
            Case B2SProFileExtension.ToLowerInvariant(), LegacyDirectB2SFileExtension
                LoadBackglassFile(filename)
        End Select
    End Sub

    Private Sub OpenStartupToolWindows()
        For Each formName As String In MyBase.StartToolForms
            Select Case formName
                Case "formToolReelsAndLEDs"
                    CheckToolReelsAndLEDsForm()
                    ShowToolReelsAndLEDsForm()
                Case "formToolIllumination"
                    CheckToolIlluminationForm()
                    ShowToolIlluminationForm()
                Case "formToolUndo"
                    CheckToolUndoForm()
                    ShowToolUndoForm()
                Case "formToolResources"
                    CheckToolResourcesForm()
                    ShowToolResourcesForm()
            End Select
        Next
        ShowLayersPanel()
    End Sub

    Private Sub PrepareRecoveryPromptWorkspace()
        If startupWorkspacePrepared Then Return

        Me.SuspendLayout()
        If b2sProHeader IsNot Nothing Then b2sProHeader.SuspendLayout()
        If tsB2SDesigner IsNot Nothing Then tsB2SDesigner.SuspendLayout()
        Try
        ' Theme every window that has already been opened by the startup
        ' sequence, including Layers and the tool windows.
        For Each openForm As Form In Application.OpenForms
            AppThemeManager.ApplyToForm(openForm)
        Next

        ' Applying the theme also normalizes ToolStrip item margins. Restore
        ' the approved full-width B2S Pro toolbar spacing immediately after
        ' that theme pass so it remains spread out after a project is loaded.
        B2SProMainToolbar.Apply(tsB2SDesigner)
        B2SProMainToolbar.ApplyMenu(msB2SDesigner)
        B2SProMainToolbar.ApplyStatusBar(ssB2SDesigner)
        ApplyHeaderToolbarSpacing()

        ' Reveal the completed header only after autoscaling, theme application,
        ' and toolbar sizing are finished. This prevents the raw combo-box text
        ' and repeated/jittering toolbar paint seen during startup.
        If b2sProHeader IsNot Nothing Then b2sProHeader.Visible = True
        RevealStartupToolbarHosts()
        Me.PerformLayout()
        startupWorkspacePrepared = True
        Finally
            If tsB2SDesigner IsNot Nothing Then tsB2SDesigner.ResumeLayout(True)
            If b2sProHeader IsNot Nothing Then b2sProHeader.ResumeLayout(True)
            Me.ResumeLayout(True)
        End Try
    End Sub

    Private Sub formDesigner_FormClosing(ByVal sender As Object, ByVal e As System.Windows.Forms.FormClosingEventArgs) Handles Me.FormClosing
        ' Never serialize a full recovery project before asking whether the user
        ' wants to save. Large image-heavy projects can take many seconds and make
        ' the application appear hung before the confirmation dialog is shown.
        ' Pause the timer as well: a WinForms timer can tick inside the modal
        ' confirmation message loop and start the same expensive save there.
        Dim resumeAutoSave As Boolean = autoSaveTimer.Enabled
        autoSaveTimer.Stop()
        Do While B2STab.TabPages.Count > 0
            B2STab.SelectedIndex = B2STab.TabPages.Count - 1
            ' Do not force a synchronous canvas repaint just before the prompt.
            ' Rendering a large illuminated backglass here adds delay without
            ' changing any of the close/save decisions.
            If B2STab.RemoveBackglass(B2STab.SelectedIndex) = Windows.Forms.DialogResult.Cancel Then
                e.Cancel = True
                If resumeAutoSave Then autoSaveTimer.Start()
                Exit Do
            End If
        Loop
    End Sub

    Private Sub B2STab_ProjectSaveRequestedOnClose(ByVal data As Backglass.Data, ByRef saveSucceeded As Boolean) Handles B2STab.ProjectSaveRequestedOnClose
        saveSucceeded = False
        If data Is Nothing OrElse String.IsNullOrWhiteSpace(data.Name) Then Return
        Try
            saveSucceeded = SaveB2SPro(data)
            If Not saveSucceeded Then TraceMotionPersistence("Close B2S Pro save failed; keeping tab open")
        Catch ex As Exception
            TraceMotionPersistence("Close B2S Pro save failed; keeping tab open: " & ex.ToString())
        End Try
    End Sub

    Private Sub B2STab_ProjectClosed(ByVal data As Backglass.Data) Handles B2STab.ProjectClosed
        ' This also runs after the user explicitly chooses No. The original
        ' project was not saved, so its temporary recovery copy is discarded.
        RemoveRecoveryCopy(data)
    End Sub

    Private Sub formDesigner_DragEnter(sender As System.Object, e As System.Windows.Forms.DragEventArgs) Handles Me.DragEnter
        If e.Data.GetDataPresent(DataFormats.FileDrop) Then
            e.Effect = DragDropEffects.Copy
        Else
            e.Effect = DragDropEffects.None
        End If
    End Sub

    Private Sub formDesigner_DragDrop(sender As System.Object, e As System.Windows.Forms.DragEventArgs) Handles Me.DragDrop
        If e.Data.GetDataPresent(DataFormats.FileDrop) Then
            Dim file_paths As String() = CType(e.Data.GetData(DataFormats.FileDrop), String())
            For Each file_path As String In file_paths
                If IsSupportedBackglassFile(file_path) Then
                    LoadBackglassFile(file_path)
                ElseIf (My.Computer.FileSystem.GetFileInfo(file_path).Extension = ".png") Then
                    If Backglass.currentTabPage IsNot Nothing Then
                        Dim image As Image = Bitmap.FromFile(file_path).Copy(True)

                        If image IsNot Nothing Then
                            If Not Backglass.currentTabPage.ShowIlluFrames Then
                                tsmiShowIlluFrames.PerformClick()
                            End If
                            Dim name As String = IO.Path.GetFileNameWithoutExtension(file_path)
                            Dim box As B2SPictureBox = Backglass.currentTabPage.CurrentPictureBox
                            Dim location As Point = box.PointToClient(New Point(e.X, e.Y))
                            If location.X < 0 Or location.X > box.Width Or location.Y < 0 Or location.Y > box.Height Then
                                location = New Point(0, 0)
                            End If
                            Backglass.currentTabPage.Illumination_AddSnippit(name, image, New Point(location.X / box.Mouse.factor, location.Y / box.Mouse.factor))
                            Backglass.currentData.Images.Insert(Images.eImageInfoType.Title4IlluminationSnippits, New Images.ImageInfo(Images.eImageInfoType.IlluminationSnippits, name, image))
                            LoadToolResourcesForm()
                        End If
                    End If
                End If
            Next file_path
        End If
    End Sub

    Private Sub B2STab_LightsReportProgress(sender As Object, e As Illumination.Lights.LightsProgressEventArgs) Handles B2STab.LightsReportProgress
        ' Rendering is not a save. Only Coding_ReportProgress drives the
        ' save/export bar; normal editing must not flash a misleading save bar.
    End Sub

    Private Sub B2STab_SelectedIndexChanged(ByVal sender As Object, ByVal e As System.EventArgs) Handles B2STab.SelectedIndexChanged
        RefreshSettings()
        LockUnlockMenus()
        If formToolLayers IsNot Nothing AndAlso Not formToolLayers.IsDisposed Then formToolLayers.RefreshLayers()
    End Sub
    Private Sub B2STab_MyMouseDown(ByVal sender As Object, ByVal e As System.Windows.Forms.MouseEventArgs) Handles B2STab.MouseDown
        LoadToolReelsAndLEDsForm()

        ' Enhanced 2.3.2: clicking the open DMD canvas selects the actual DMD image.
        ' Existing score/light objects keep priority when one is hit.
        If Backglass.currentTabPage IsNot Nothing Then
            Dim selectDMDImage As Boolean = e.Button = MouseButtons.Left AndAlso
                                            Backglass.currentData IsNot Nothing AndAlso
                                            Backglass.currentData.IsDMDImageShown AndAlso
                                            Backglass.currentData.DMDImage IsNot Nothing AndAlso
                                            Backglass.currentTabPage.Mouse.SelectedItem Is Nothing
            Backglass.currentTabPage.IsDMDCanvasImageSelected = selectDMDImage
            If selectDMDImage Then
                tsmiCut.Enabled = True
                tsmiDelete.Enabled = True
            End If
        End If
    End Sub
    Private Sub B2STab_MyMouseMove(ByVal sender As Object, ByVal e As Mouse.MouseMoveEventArgs) Handles B2STab.MouseMove
        UpdateStatusBar4Mouse(Me, Backglass.currentTabPage, New Point(e.Location.X, e.Location.Y))
    End Sub
    Private Sub B2STab_CopyDMDCopyArea(sender As Object, e As System.EventArgs) Handles B2STab.CopyDMDCopyArea
        CopyDMDArea()
    End Sub
    Private Sub B2STab_RemoveDMDCopyArea(sender As Object, e As System.EventArgs) Handles B2STab.RemoveDMDCopyArea
        RemoveDMDCopyWindow()
    End Sub
    Private Sub B2STab_SelectedItemClicked(sender As Object, e As Mouse.MouseMoveEventArgs) Handles B2STab.SelectedItemClicked
        ' load tool window info
        If e.ItemType = Mouse.MouseMoveEventArgs.eItemType.Score Then
            LoadToolReelsAndLEDsForm()
        ElseIf e.ItemType = Mouse.MouseMoveEventArgs.eItemType.Bulb Then
            LoadToolIlluminationForm()
        End If
        ' maybe communicate with animation window
        If formAnimations IsNot Nothing AndAlso formAnimations.Visible Then
            If e.ItemType = Mouse.MouseMoveEventArgs.eItemType.Bulb AndAlso TypeOf sender Is Mouse Then
                With DirectCast(sender, Mouse)
                    formAnimations.BulbClicked(.SelectedBulb.Name, .SelectedBulb.ID)
                End With
            End If
        End If
        If formToolLayers IsNot Nothing AndAlso Not formToolLayers.IsDisposed Then
            formToolLayers.SynchronizeSelection(Backglass.currentTabPage.Mouse.SelectedItems)
        End If
        ' set focus to tab
        B2STab.Focus()
    End Sub
    Private lastDragToolUpdateTick As Integer = Environment.TickCount
    Private Const DragToolUpdateIntervalMs As Integer = 30

    Private Sub B2STab_SelectedItemMoving(ByVal sender As Object, ByVal e As Mouse.MouseMoveEventArgs) Handles B2STab.SelectedItemMoving
        ' Enhanced 3.0.3 Stage 2: editing text boxes on every raw mouse message
        ' is expensive. Update the tool window at roughly 33 FPS; the model itself
        ' still receives every movement and mouse-up commits the exact final value.
        Dim nowTick As Integer = Environment.TickCount
        If CUInt(nowTick - lastDragToolUpdateTick) < DragToolUpdateIntervalMs Then Return
        lastDragToolUpdateTick = nowTick
        If e.ItemType = Mouse.MouseMoveEventArgs.eItemType.Score Then
            If formToolReelsAndLEDs IsNot Nothing Then
                formToolReelsAndLEDs.ignoreChange = True
                formToolReelsAndLEDs.txtLocationX.Text = e.Location.X.ToString()
                formToolReelsAndLEDs.txtLocationY.Text = e.Location.Y.ToString()
                formToolReelsAndLEDs.txtSizeWidth.Text = e.Size.Width.ToString()
                formToolReelsAndLEDs.txtSizeHeight.Text = e.Size.Height.ToString()
                formToolReelsAndLEDs.ignoreChange = False
            End If
        ElseIf e.ItemType = Mouse.MouseMoveEventArgs.eItemType.Bulb Then
            If formToolIllumination IsNot Nothing Then
                formToolIllumination.ignoreChange = True
                formToolIllumination.txtLocationX.Text = e.Location.X.ToString()
                formToolIllumination.txtLocationY.Text = e.Location.Y.ToString()
                formToolIllumination.txtSizeWidth.Text = e.Size.Width.ToString()
                formToolIllumination.txtSizeHeight.Text = e.Size.Height.ToString()
                formToolIllumination.ignoreChange = False
            End If
        End If
        'B2STab.Focus()
    End Sub
    Private Sub B2STab_SelectedItemRemoved_Layers(sender As Object, e As EventArgs) Handles B2STab.SelectedItemRemoved
        If formToolLayers IsNot Nothing AndAlso Not formToolLayers.IsDisposed Then formToolLayers.RefreshLayers()
    End Sub

    Private Sub B2STab_SelectedBulbMoved(sender As Object, e As System.Windows.Forms.MouseEventArgs) Handles B2STab.SelectedBulbMoved
        If Backglass.currentTabPage IsNot Nothing AndAlso Backglass.currentTabPage.ShowIllumination Then
            Backglass.currentTabPage.RefreshEditorLighting()
        End If
    End Sub
    Private Sub B2STab_SelectedBulbEdited(sender As Object, e As System.EventArgs) Handles B2STab.SelectedBulbEdited
        If Backglass.currentTabPage IsNot Nothing AndAlso Backglass.currentTabPage.Mouse.SelectedBulb IsNot Nothing Then
            If formToolIllumination IsNot Nothing Then
                formToolIllumination.TrackBarIntensity.Value = Backglass.currentTabPage.Mouse.SelectedBulb.Intensity
                formToolIllumination.cmbDodgeColor.SelectedIndex = TranslateDodgeColor2Index(Backglass.currentTabPage.Mouse.SelectedBulb.DodgeColor)
                formToolIllumination.LoadGlowControls(Backglass.currentTabPage.Mouse.SelectedBulb)
            End If
        End If
    End Sub
    Private Sub B2STab_LightColorChanged(sedner As Object, e As Illumination.Lights.LightColorChangedEventArgs) Handles B2STab.LightColorChanged
        If Backglass.currentTabPage IsNot Nothing AndAlso Backglass.currentTabPage.Mouse.SelectedBulb IsNot Nothing Then
            If formToolIllumination IsNot Nothing Then
                formToolIllumination.btnLightColor.BackColor = Backglass.currentTabPage.Mouse.SelectedBulb.LightColor
            End If
        End If
    End Sub

    Private Sub ProgressReset_Tick(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles timerProgressReset.Tick
        timerProgressReset.Stop()
        tsProgress.Value = 0
        tsProgress.Visible = False
    End Sub

    Private Sub Coding_ReportProgress(sender As Object, e As Coding.CodingProgressEventArgs) Handles coding.ReportProgress
        ShowProgress(e.Progress)
    End Sub

#Region "tool form disposing (important!!!)"

    Private Sub ToolReelsAndLEDs_Disposed(ByVal sender As Object, ByVal e As System.EventArgs) Handles formToolReelsAndLEDs.Disposed
        formToolReelsAndLEDs = Nothing
    End Sub
    Private Sub ToolIllumination_Disposed(ByVal sender As Object, ByVal e As System.EventArgs) Handles formToolIllumination.Disposed
        formToolIllumination = Nothing
    End Sub
    Private Sub ToolUndo_Disposed(ByVal sender As Object, ByVal e As System.EventArgs) Handles formToolUndo.Disposed
        formToolUndo = Nothing
    End Sub
    Private Sub ToolResources_Disposed(ByVal sender As Object, ByVal e As System.EventArgs) Handles formToolResources.Disposed
        formToolResources = Nothing
    End Sub

    Private Sub AddSnippit_Disposed(ByVal sender As Object, ByVal e As System.EventArgs) Handles formAddSnippit.Disposed
        formAddSnippit = Nothing
    End Sub

    Private Sub Animations_Disposed(ByVal sender As Object, ByVal e As System.EventArgs) Handles formAnimations.Disposed
        formAnimations = Nothing
    End Sub

    Private Sub VPM_Disposed(ByVal sender As Object, ByVal e As System.EventArgs) Handles formVPM.Disposed
        formVPM = Nothing
    End Sub

#End Region

#Region "data events of the tool windows"

    Private Sub formToolReelsAndLEDs_DataChanged(ByVal sender As Object, ByVal e As formToolReelsAndLEDs.ScoreEventArgs) Handles formToolReelsAndLEDs.DataChanged
        If NoToolEvents Then Return
        If Backglass.currentTabPage IsNot Nothing Then
            Select Case e.TypeOfData
                Case B2SBackglassDesigner.formToolReelsAndLEDs.eScoreDataType.NumberOfPlayers
                    Backglass.currentTabPage.ReelsAndLEDs_SetNumberOfPlayers(CInt(e.Data))
                Case B2SBackglassDesigner.formToolReelsAndLEDs.eScoreDataType.B2SStartDigit
                    Backglass.currentTabPage.ReelsAndLEDs_SetB2SStartDigit(e.Data)
                Case B2SBackglassDesigner.formToolReelsAndLEDs.eScoreDataType.B2SScoreType
                    Backglass.currentTabPage.ReelsAndLEDs_SetB2SScoreType(e.Data)
                Case B2SBackglassDesigner.formToolReelsAndLEDs.eScoreDataType.B2SPlayerNo
                    Backglass.currentTabPage.ReelsAndLEDs_SetB2SPlayerNo(e.Data)
                Case B2SBackglassDesigner.formToolReelsAndLEDs.eScoreDataType.NumberOfDigits
                    Backglass.currentTabPage.ReelsAndLEDs_SetDigits(CInt(e.Data))
                Case B2SBackglassDesigner.formToolReelsAndLEDs.eScoreDataType.Spacing
                    Backglass.currentTabPage.ReelsAndLEDs_SetSpacing(CInt(e.Data))
                Case B2SBackglassDesigner.formToolReelsAndLEDs.eScoreDataType.Location
                    Backglass.currentTabPage.ReelsAndLEDs_SetLocation(e.Data)
                Case B2SBackglassDesigner.formToolReelsAndLEDs.eScoreDataType.Size
                    Backglass.currentTabPage.ReelsAndLEDs_SetSize(e.Data)
                Case B2SBackglassDesigner.formToolReelsAndLEDs.eScoreDataType.DisplayState
                    Backglass.currentTabPage.ReelsAndLEDs_SetState(e.Data)
                Case B2SBackglassDesigner.formToolReelsAndLEDs.eScoreDataType.ReelType
                    Backglass.currentTabPage.ReelsAndLEDs_SetReelType(e.Data)
                Case B2SBackglassDesigner.formToolReelsAndLEDs.eScoreDataType.UseDream7LEDs
                    Backglass.currentTabPage.ReelsAndLEDs_SetDream7LEDs(e.Data)
                Case B2SBackglassDesigner.formToolReelsAndLEDs.eScoreDataType.PerfectScaleWidthFix
                    Backglass.currentTabPage.ReelsAndLEDs_PerfectScaleWidthFix()
                Case B2SBackglassDesigner.formToolReelsAndLEDs.eScoreDataType.ChangeLEDColor
                    Backglass.currentTabPage.ReelsAndLEDs_ChangeLEDColor()
                Case B2SBackglassDesigner.formToolReelsAndLEDs.eScoreDataType.ReelIllumination
                    Backglass.currentTabPage.ReelsAndLEDs_ReelIllumination()
            End Select
        End If
    End Sub
    Private Sub formToolIllumination_DataChanged(ByVal sender As Object, ByVal e As formToolIllumination.IlluminationEventArgs) Handles formToolIllumination.DataChanged
        If NoToolEvents Then Return
        If Backglass.currentTabPage IsNot Nothing Then
            Select Case e.TypeOfData
                Case B2SBackglassDesigner.formToolIllumination.eIlluminationDataType.Name
                    Backglass.currentTabPage.Illumination_SetName(e.Data)
                Case B2SBackglassDesigner.formToolIllumination.eIlluminationDataType.ID
                    Backglass.currentTabPage.Illumination_SetID(e.Data)
                Case B2SBackglassDesigner.formToolIllumination.eIlluminationDataType.B2SID
                    Backglass.currentTabPage.Illumination_SetB2SID(e.Data)
                Case B2SBackglassDesigner.formToolIllumination.eIlluminationDataType.B2SIDType
                    Backglass.currentTabPage.Illumination_SetB2SIDType(e.Data)
                Case B2SBackglassDesigner.formToolIllumination.eIlluminationDataType.B2SValue
                    Backglass.currentTabPage.Illumination_SetB2SValue(e.Data)
                Case B2SBackglassDesigner.formToolIllumination.eIlluminationDataType.RomID
                    Backglass.currentTabPage.Illumination_SetRomID(e.Data)
                Case B2SBackglassDesigner.formToolIllumination.eIlluminationDataType.RomIDType
                    Backglass.currentTabPage.Illumination_SetRomIDType(e.Data)
                Case B2SBackglassDesigner.formToolIllumination.eIlluminationDataType.RomInverted
                    Backglass.currentTabPage.Illumination_SetRomInverted(e.Data)
                Case B2SBackglassDesigner.formToolIllumination.eIlluminationDataType.InitialState
                    Backglass.currentTabPage.Illumination_SetInitialState(e.Data)
                Case B2SBackglassDesigner.formToolIllumination.eIlluminationDataType.DualMode
                    Backglass.currentTabPage.Illumination_SetDualMode(e.Data)
                Case B2SBackglassDesigner.formToolIllumination.eIlluminationDataType.Intensity
                    Backglass.currentTabPage.Illumination_SetIntensity(e.Data)
                Case B2SBackglassDesigner.formToolIllumination.eIlluminationDataType.DodgeColor
                    Backglass.currentTabPage.Illumination_DodgeColor(e.Data)
                Case B2SBackglassDesigner.formToolIllumination.eIlluminationDataType.IlluMode
                    Backglass.currentTabPage.Illumination_IlluMode(e.Data)
                Case B2SBackglassDesigner.formToolIllumination.eIlluminationDataType.Illuminationtext
                    Backglass.currentTabPage.Illumination_SetText(e.Data)
                Case B2SBackglassDesigner.formToolIllumination.eIlluminationDataType.IlluminationtextAlignment
                    Backglass.currentTabPage.Illumination_SetTextAlignment(e.Data)
                Case B2SBackglassDesigner.formToolIllumination.eIlluminationDataType.IlluminationtextFont
                    Backglass.currentTabPage.Illumination_SetTextFont(e.Data)
                Case B2SBackglassDesigner.formToolIllumination.eIlluminationDataType.LightClassification
                    If formToolLayers IsNot Nothing AndAlso Not formToolLayers.IsDisposed Then formToolLayers.RefreshLayers()
                Case B2SBackglassDesigner.formToolIllumination.eIlluminationDataType.ChangeLightColor
                    Backglass.currentTabPage.Illumination_ChangeLightColor()
                Case B2SBackglassDesigner.formToolIllumination.eIlluminationDataType.Location
                    Backglass.currentTabPage.Illumination_SetLocation(e.Data)
                Case B2SBackglassDesigner.formToolIllumination.eIlluminationDataType.Size
                    Backglass.currentTabPage.Illumination_SetSize(e.Data)
                Case B2SBackglassDesigner.formToolIllumination.eIlluminationDataType.ZOrder
                    Backglass.currentTabPage.Illumination_SetZOrder(e.Data)
                Case B2SBackglassDesigner.formToolIllumination.eIlluminationDataType.SnippitInfo
                    Backglass.currentTabPage.Illumination_SetSnippitInfo(e.Data)
            End Select
            ' refresh rom filter
            RefreshIDFilter()
        End If
    End Sub
    Private Sub formToolResources_DataChanged(ByVal sender As Object, ByVal e As formToolResources.ImagesEventArgs) Handles formToolResources.DataChanged
        If NoToolEvents Then Return
        If Backglass.currentTabPage IsNot Nothing AndAlso Backglass.currentData IsNot Nothing Then
            Select Case e.TypeOfData
                Case B2SBackglassDesigner.formToolResources.eImagesDataType.BackgroundImageRemoved
                    Backglass.currentData.IsDirty = True

                Case B2SBackglassDesigner.formToolResources.eImagesDataType.IlluminationImageRemoved
                    Backglass.currentData.IsDirty = True
                Case B2SBackglassDesigner.formToolResources.eImagesDataType.DMDImageRemoved
                    Backglass.currentData.IsDirty = True
                    Dim currentDMDImageInfo As Images.ImageInfo = Backglass.currentData.Images.CurrentDMDImageInfo()
                    If currentDMDImageInfo Is Nothing Then
                        Backglass.currentTabPage.DMDImage = Nothing
                    Else
                        Backglass.currentTabPage.DMDImage(currentDMDImageInfo.Text) = currentDMDImageInfo.Image
                        Backglass.currentTabPage.Zoom(tscmbZoomInPercent.Text)
                        tscmbImage.SelectedIndex = 1
                    End If
                Case B2SBackglassDesigner.formToolResources.eImagesDataType.BackgroundImageSelectionChanged
                    Undo.AddEntry(New Undo.UndoEntry(Undo.Type.ImageChanged, Backglass.currentData.Image))
                    If B2SMessageBox.Show(My.Resources.MSG_BackgroundImageChange, AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1) = Windows.Forms.DialogResult.Yes Then
                        Backglass.currentBulbs.Resize(Backglass.currentData.Image.Size, e.Data.Image.Size)
                        Backglass.currentScores.Resize(Backglass.currentData.Image.Size, e.Data.Image.Size)
                        Dim factor As Single = Backglass.currentData.Image.Size.Height / e.Data.Image.Size.Height
                        Backglass.currentData.GrillHeight = Backglass.currentData.GrillHeight / factor
                        Backglass.currentData.SmallGrillHeight = Backglass.currentData.SmallGrillHeight / factor
                    End If
                    Backglass.currentTabPage.Image(e.Data.Text) = e.Data.Image
                    Backglass.currentTabPage.Zoom(tscmbZoomInPercent.Text)
                    tscmbImage.SelectedIndex = 0
                    UpdateStatusBar(Me, Backglass.currentTabPage)
                Case B2SBackglassDesigner.formToolResources.eImagesDataType.IlluminatedImageSelectionChanged

                    e.Data.Selected = True

                Case B2SBackglassDesigner.formToolResources.eImagesDataType.DMDImageSelectionChanged
                    Undo.AddEntry(New Undo.UndoEntry(Undo.Type.DMDImageChanged, Backglass.currentData.DMDImage))
                    Backglass.currentTabPage.DMDImage(e.Data.Text) = e.Data.Image
                    Backglass.currentTabPage.Zoom(tscmbZoomInPercent.Text)
                    tscmbImage.SelectedIndex = 1
                    UpdateStatusBar(Me, Backglass.currentTabPage)
                Case B2SBackglassDesigner.formToolResources.eImagesDataType.BackgroundImageTypeChanged
                    Backglass.currentData.IsDirty = True
                Case B2SBackglassDesigner.formToolResources.eImagesDataType.BackgroundImageRomIDChanged
                    Backglass.currentData.IsDirty = True
                Case B2SBackglassDesigner.formToolResources.eImagesDataType.BackgroundImageRomIDTypeChanged
                    Backglass.currentData.IsDirty = True
            End Select
        End If
    End Sub

    Private Sub formAddSnippit_SnippitAdded(sender As Object, e As formAddSnippit.AddSnippitEventArgs) Handles formAddSnippit.SnippitAdded
        Backglass.currentTabPage.Illumination_AddSnippit(e.Name, e.Image, e.Location)
        Backglass.currentData.Images.Insert(Images.eImageInfoType.Title4IlluminationSnippits, New Images.ImageInfo(Images.eImageInfoType.IlluminationSnippits, e.Name, e.Image))
        LoadToolResourcesForm()
    End Sub

    Private Sub formAnimations_ResetAnimationLights(sender As Object, e As System.EventArgs) Handles formAnimations.ResetAnimationLights
        Backglass.currentTabPage.ResetAnimationLights()
    End Sub
    Private Sub formAnimations_ShowAnimation(sender As Object, e As formAnimations.ShowAnimationEventArgs) Handles formAnimations.ShowAnimation
        Backglass.currentTabPage.ShowAnimation(e.Name)
    End Sub

#End Region

#End Region


#Region "menu stuff"

#Region "file menu"

    Private Sub B2STab_NewProjectRequested(sender As Object, e As EventArgs) Handles B2STab.NewProjectRequested
        ' Keep the empty-workspace shortcut identical to File > New, including
        ' all validation and setup performed by the existing menu command.
        tsmiNew.PerformClick()
    End Sub

    Private Sub New_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles tsbNew.Click, tsmiNew.Click
        OpenSettings(True)
    End Sub
    Private Sub Open_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles tsmiOpen.Click, tsbOpen.Click
        Using filedialog As OpenFileDialog = New OpenFileDialog
            With filedialog
                .Filter = "B2S Pro and directB2S backglass files (*.B2SPro;*.directb2s)|*.B2SPro;*.directb2s"
                .FileName = String.Empty
                .InitialDirectory = If(String.IsNullOrWhiteSpace(LatestImportDirectory), BackglassProjectsPath, LatestImportDirectory)
                If .ShowDialog(Me) = DialogResult.OK Then
                    LoadBackglassFile(.FileName)
                End If
            End With
        End Using
    End Sub
    Private Sub Close_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles tsmiClose.Click
        B2STab.RemoveBackglass(B2STab.SelectedIndex)
        ShowStatus()
        LockUnlockMenus()
    End Sub

    Private Sub Save_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles tsmiSave.Click, tsbSave.Click
        If Backglass.currentTabPage IsNot Nothing Then
            Cursor.Current = Cursors.WaitCursor
            SaveB2SPro(Backglass.currentData)
            Cursor.Current = Cursors.Default
        Else
            tsmiNew.PerformClick()
        End If
        LockUnlockMenus()
    End Sub
    Private Sub SaveAs_Click(sender As System.Object, e As System.EventArgs) Handles tsmiSaveAs.Click
        If Backglass.currentTabPage IsNot Nothing Then
            Using fileDialog As New SaveFileDialog()
                fileDialog.Title = "Save B2S Pro As"
                fileDialog.Filter = "B2S Pro backglass file (*.B2SPro)|*.B2SPro"
                fileDialog.DefaultExt = B2SProFileExtension.TrimStart("."c)
                fileDialog.AddExtension = True
                fileDialog.FileName = If(String.IsNullOrWhiteSpace(Backglass.currentData.VSName), Backglass.currentData.Name, Backglass.currentData.VSName) & B2SProFileExtension
                If Not String.IsNullOrWhiteSpace(Backglass.currentData.SourceFilePath) Then
                    fileDialog.InitialDirectory = IO.Path.GetDirectoryName(Backglass.currentData.SourceFilePath)
                End If
                If fileDialog.ShowDialog(Me) = DialogResult.OK Then
                    Cursor.Current = Cursors.WaitCursor
                    SaveB2SPro(Backglass.currentData, True, fileDialog.FileName)
                    Cursor.Current = Cursors.Default
                End If
            End Using
        End If
    End Sub
    Private Sub SaveAll_Click(sender As System.Object, e As System.EventArgs) Handles tsmiSaveAll.Click
        Dim originalIndex As Integer = B2STab.SelectedIndex
        For index As Integer = 0 To B2STab.TabPages.Count - 1
            B2STab.SelectedIndex = index
            If Backglass.currentData IsNot Nothing Then SaveB2SPro(Backglass.currentData)
        Next
        If originalIndex >= 0 AndAlso originalIndex < B2STab.TabPages.Count Then B2STab.SelectedIndex = originalIndex
    End Sub

    Private Sub Settings_Click(sender As System.Object, e As System.EventArgs) Handles tsmiSettings.Click
        If Backglass.currentTabPage IsNot Nothing Then
            OpenSettings(False)
        End If
    End Sub

    Private Sub ImportBackglassFile_Click(sender As System.Object, e As System.EventArgs) Handles tsmiImportBackglassFile.Click
        Using filedialog As OpenFileDialog = New OpenFileDialog
            With filedialog
                .Filter = "B2S Pro and directB2S backglass files (*.B2SPro;*.directb2s)|*.B2SPro;*.directb2s"
                .FileName = String.Empty
                .InitialDirectory = If(LatestImportDirectory.Length, LatestImportDirectory, BackglassProjectsPath)
                If .ShowDialog(Me) = DialogResult.OK Then
                    LoadBackglassFile(.FileName)
                End If
            End With
            ShowStatus()
        End Using
        LockUnlockMenus()
    End Sub

    Private Sub Exit_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles tsmiExit.Click
        Me.Close()
    End Sub

    ' open recent stuff
    Private Sub OpenRecent_DropDownOpening(ByVal sender As Object, ByVal e As System.EventArgs) Handles tsmiOpenRecent.DropDownOpening
        Dim recentDropDown As ToolStripDropDownMenu = TryCast(tsmiOpenRecent.DropDown, ToolStripDropDownMenu)
        If recentDropDown IsNot Nothing Then recentDropDown.ShowImageMargin = False
        ' Older builds added new/unsaved projects to this file menu. Remove
        ' entries that have no source file before rebuilding it.
        recent.RemoveUnsupportedBackglassEntries()
        recent.RemoveMissingEntries()
        If recent.IsDirty Then
            Do While True
                Dim found As Boolean = False
                For Each menuitem As ToolStripItem In tsmiOpenRecent.DropDownItems
                    If TypeOf menuitem Is ToolStripMenuItem Then
                        If menuitem.Name.StartsWith("recent") Then
                            found = True
                            tsmiOpenRecent.DropDownItems.Remove(menuitem)
                            menuitem.Image = Nothing
                            menuitem.Dispose()
                            Exit For
                        End If
                    End If
                Next
                If Not found Then Exit Do
            Loop
            Dim i As Integer = recent.recentEntries.Count
            For Each recentEntry As KeyValuePair(Of Integer, Recent.recentEntry) In recent.recentEntries
                With recentEntry.Value
                    Dim newTSMI As ToolStripMenuItem = New ToolStripMenuItem(If(i = 10, "1&0 ", "&" & i.ToString() & " ") & .Name, Nothing, AddressOf OpenRecent_ChildClick)
                    newTSMI.Name = "recent" & recentEntry.Key
                    newTSMI.Tag = recentEntry.Value
                    tsmiOpenRecent.DropDownItems.Insert(0, newTSMI)
                End With
                i -= 1
            Next
            recent.IsDirty = False
        End If
    End Sub
    Private Sub OpenRecent_ChildClick(ByVal sender As Object, ByVal e As EventArgs)
        Dim menuItem As ToolStripMenuItem = TryCast(sender, ToolStripMenuItem)
        Dim entry As Recent.recentEntry = If(menuItem IsNot Nothing, TryCast(menuItem.Tag, Recent.recentEntry), Nothing)
        If entry IsNot Nothing AndAlso IO.File.Exists(entry.FileName) Then
            TraceMotionPersistence("Recent clicked: " & entry.FileName)
            If IsSupportedBackglassFile(entry.FileName) Then
                LoadBackglassFile(entry.FileName)
            End If
        End If
    End Sub

    Private Sub ClearThisList_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles tsmiClearThisList.Click
        recent.RemoveAllFromRecentList()
    End Sub

#End Region

#Region "edit"

    Private Sub Undo_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles tsmiUndo.Click, tsbUndo.Click
        Undo.Undo()
        UpdateStatusBar(Me, Backglass.currentTabPage)
    End Sub
    Private Sub Redo_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles tsmiRedo.Click, tsbRedo.Click
        Undo.Redo()
        UpdateStatusBar(Me, Backglass.currentTabPage)
    End Sub

    Private copiedBulb As Illumination.BulbInfo = Nothing

    ' Enhanced Edition 2.3 arrange commands. Created in code so existing localized
    ' resource files remain untouched and older project files stay compatible.
    Private tsmiArrange As ToolStripMenuItem
    Private tsmiBringToFrontEnhanced As ToolStripMenuItem
    Private tsmiMoveForwardEnhanced As ToolStripMenuItem
    Private tsmiMoveBackwardEnhanced As ToolStripMenuItem
    Private tsmiSendToBackEnhanced As ToolStripMenuItem

    Private Sub InitializeEnhancedEditMenu()
        tsmiPaste.ShortcutKeys = Keys.Control Or Keys.V
        tsmiPaste.ShowShortcutKeys = True

        tsmiArrange = New ToolStripMenuItem("Arrange")
        tsmiBringToFrontEnhanced = New ToolStripMenuItem("Bring to Front", Nothing, AddressOf BringToFrontEnhanced_Click)
        tsmiMoveForwardEnhanced = New ToolStripMenuItem("Move Forward", Nothing, AddressOf MoveForwardEnhanced_Click)
        tsmiMoveBackwardEnhanced = New ToolStripMenuItem("Move Backward", Nothing, AddressOf MoveBackwardEnhanced_Click)
        tsmiSendToBackEnhanced = New ToolStripMenuItem("Send to Back", Nothing, AddressOf SendToBackEnhanced_Click)

        tsmiBringToFrontEnhanced.ShortcutKeys = Keys.Control Or Keys.Shift Or Keys.OemCloseBrackets
        tsmiMoveForwardEnhanced.ShortcutKeys = Keys.Control Or Keys.OemCloseBrackets
        tsmiMoveBackwardEnhanced.ShortcutKeys = Keys.Control Or Keys.OemOpenBrackets
        tsmiSendToBackEnhanced.ShortcutKeys = Keys.Control Or Keys.Shift Or Keys.OemOpenBrackets

        tsmiArrange.DropDownItems.AddRange(New ToolStripItem() {
            tsmiBringToFrontEnhanced, tsmiMoveForwardEnhanced,
            tsmiMoveBackwardEnhanced, tsmiSendToBackEnhanced})
        AddHandler tsmiEdit.DropDownOpening, AddressOf EnhancedEdit_DropDownOpening
    End Sub

    ' Copy every light-rendering and mask setting added after the original
    ' clipboard implementation. Keeping this in one place makes Cut, Copy and
    ' Paste produce the same light instead of silently reverting newer options.
    Private Shared Sub CopyExtendedLightProperties(ByVal source As Illumination.BulbInfo, ByVal target As Illumination.BulbInfo)
        If source Is Nothing OrElse target Is Nothing Then Return

        target.LightPurpose = source.LightPurpose
        target.ArtworkPixelLighting = source.ArtworkPixelLighting
        target.GlowSpread = source.GlowSpread
        target.GlowSoftness = source.GlowSoftness
        target.GlowIntensity = source.GlowIntensity
        target.GlowFalloff = source.GlowFalloff
        target.LightDiffusion = source.LightDiffusion
        target.LightTemperature = source.LightTemperature
        target.LightRotationAngle = source.LightRotationAngle
        target.GlowBlendMode = source.GlowBlendMode
        target.GlowPreviewQuality = source.GlowPreviewQuality
        target.FlasherStyle = source.FlasherStyle
        target.FlasherSaturation = source.FlasherSaturation
        target.FlasherHighlightProtection = source.FlasherHighlightProtection
        target.FlasherDarkAreaLift = source.FlasherDarkAreaLift
        target.FlasherHotspotX = source.FlasherHotspotX
        target.FlasherHotspotY = source.FlasherHotspotY
        target.FlasherPulseDuration = source.FlasherPulseDuration
        target.MaskRadius = source.MaskRadius
        target.MaskSmartRadius = source.MaskSmartRadius
        target.MaskSmooth = source.MaskSmooth
        target.MaskFeather = source.MaskFeather
        target.MaskContrast = source.MaskContrast
        target.MaskShiftEdge = source.MaskShiftEdge
        target.FlasherRadialSpikes = source.FlasherRadialSpikes
        target.ArtworkBrightness = source.ArtworkBrightness
        target.ArtworkContrast = source.ArtworkContrast
        target.ArtworkAdjustmentPasses = source.ArtworkAdjustmentPasses
        target.InFrontOfGlobalMask = source.InFrontOfGlobalMask
        target.GlobalMaskLayerExplicit = source.GlobalMaskLayerExplicit
        target.LightBehindCanvas = source.LightBehindCanvas
        target.SelectionMaskData = source.SelectionMaskData
        target.SelectionTolerance = source.SelectionTolerance
        target.SelectionFeather = source.SelectionFeather
        target.IsIlluminatedImageDirty = True
    End Sub

    Private Sub EnhancedEdit_DropDownOpening(sender As Object, e As EventArgs)
        Dim hasTab As Boolean = Backglass.currentTabPage IsNot Nothing
        Dim hasBulb As Boolean = hasTab AndAlso Backglass.currentTabPage.Mouse.SelectedBulb IsNot Nothing
        Dim hasItem As Boolean = hasTab AndAlso Backglass.currentTabPage.Mouse.SelectedItem IsNot Nothing
        Dim hasDmdCopyWindow As Boolean = hasTab AndAlso IsDMDCopyWindowActive() AndAlso Backglass.currentTabPage.Mouse.IsDMDCopyAreaSelected
        Dim hasDmdCanvasImage As Boolean = hasTab AndAlso Backglass.currentTabPage.IsDMDCanvasImageSelected AndAlso Backglass.currentData.DMDImage IsNot Nothing

        tsmiArrange.Enabled = hasBulb
        tsmiCut.Enabled = hasItem OrElse hasDmdCopyWindow OrElse hasDmdCanvasImage
        tsmiCopy.Enabled = hasBulb
        tsmiDelete.Enabled = hasItem OrElse hasDmdCopyWindow OrElse hasDmdCanvasImage
        tsmiPaste.Enabled = copiedBulb IsNot Nothing
    End Sub

    Private Function IsDMDCopyWindowActive() As Boolean
        If Backglass.currentTabPage Is Nothing Then Return False
        With Backglass.currentData.DMDCopyArea
            Return Backglass.currentTabPage.CopyDMDImageFromBackglass AndAlso
                   .Size.Width > 0 AndAlso .Size.Height > 0
        End With
    End Function

    Private Function RemoveDMDCopyWindow() As Boolean
        If Not IsDMDCopyWindowActive() Then Return False

        Backglass.currentTabPage.Mouse.IsDMDCopyAreaSelected = False
        Backglass.currentData.DMDCopyArea.Location = Point.Empty
        Backglass.currentData.DMDCopyArea.Size = Size.Empty
        Backglass.currentTabPage.CopyDMDImageFromBackglass = False
        tsmiCopyDMDImageFromBackglass.Checked = False
        Backglass.currentData.IsDirty = True
        ' Refresh already invalidates and paints the tab page; avoid queuing the same
        ' full repaint twice when the DMD copy window is removed.
        Backglass.currentTabPage.Refresh()
        UpdateStatusBar(Me, Backglass.currentTabPage)
        Return True
    End Function

    ' Enhanced 2.3.2: remove the actual DMD image currently displayed by Window > DMD image.
    Private Function RemoveSelectedDMDCanvasImage(Optional copyToClipboard As Boolean = False) As Boolean
        If Backglass.currentTabPage Is Nothing OrElse Backglass.currentData Is Nothing Then Return False
        If Not Backglass.currentData.IsDMDImageShown OrElse Not Backglass.currentTabPage.IsDMDCanvasImageSelected Then Return False
        If Backglass.currentData.DMDImage Is Nothing Then Return False

        Dim selectedInfo As Images.ImageInfo = Nothing
        For Each info As Images.ImageInfo In Backglass.currentData.Images
            If info.Type = Images.eImageInfoType.DMDImage Then
                If Object.ReferenceEquals(info.Image, Backglass.currentData.DMDImage) OrElse
                   info.Text.Equals(Backglass.currentData.DMDImageFileName, StringComparison.CurrentCultureIgnoreCase) Then
                    selectedInfo = info
                    Exit For
                End If
            End If
        Next
        If selectedInfo Is Nothing Then selectedInfo = Backglass.currentData.Images.CurrentDMDImageInfo()
        If selectedInfo Is Nothing Then Return False

        If copyToClipboard Then
            Try
                Clipboard.SetImage(New Bitmap(selectedInfo.Image))
            Catch
                ' Clipboard failure must not prevent Cut from removing the selected image.
            End Try
        End If

        Undo.AddEntry(New Undo.UndoEntry(Undo.Type.DMDImageChanged, Backglass.currentData.DMDImage))
        Backglass.currentData.Images.Remove(selectedInfo)
        Backglass.currentTabPage.IsDMDCanvasImageSelected = False

        Dim nextInfo As Images.ImageInfo = Backglass.currentData.Images.CurrentDMDImageInfo()
        If nextInfo Is Nothing Then
            Backglass.currentTabPage.DMDImage = Nothing
        Else
            Backglass.currentTabPage.DMDImage(nextInfo.Text) = nextInfo.Image
            Backglass.currentTabPage.Zoom(tscmbZoomInPercent.Text)
            tscmbImage.SelectedIndex = 1
        End If

        Backglass.currentData.IsDirty = True
        Backglass.currentData.IsSavedDMDImageDirty = True
        LoadToolResourcesForm()
        Backglass.currentTabPage.Invalidate()
        UpdateStatusBar(Me, Backglass.currentTabPage)
        Return True
    End Function

    Private Function CurrentBulbCollection() As Illumination.BulbCollection
        If Backglass.currentTabPage Is Nothing Then Return Nothing
        If Backglass.currentData.IsDMDImageShown Then
            Return Backglass.currentData.DMDBulbs
        End If
        Return Backglass.currentData.Bulbs
    End Function

    Private Sub ArrangeSelectedBulb(mode As Integer)
        If Backglass.currentTabPage Is Nothing OrElse Backglass.currentTabPage.Mouse.SelectedBulb Is Nothing Then Return
        Dim bulbs As Illumination.BulbCollection = CurrentBulbCollection()
        If bulbs Is Nothing OrElse bulbs.Count < 2 Then Return

        Dim selected As Illumination.BulbInfo = Backglass.currentTabPage.Mouse.SelectedBulb
        Dim ordered As New List(Of Illumination.BulbInfo)(bulbs)
        ordered.Sort(Function(a, b) a.ZOrder.CompareTo(b.ZOrder))
        Dim index As Integer = ordered.IndexOf(selected)
        If index < 0 Then Return

        Select Case mode
            Case 0 ' send to back
                ordered.RemoveAt(index)
                ordered.Insert(0, selected)
            Case 1 ' move backward
                If index = 0 Then Return
                ordered(index) = ordered(index - 1)
                ordered(index - 1) = selected
            Case 2 ' move forward
                If index = ordered.Count - 1 Then Return
                ordered(index) = ordered(index + 1)
                ordered(index + 1) = selected
            Case 3 ' bring to front
                ordered.RemoveAt(index)
                ordered.Add(selected)
        End Select

        For i As Integer = 0 To ordered.Count - 1
            ordered(i).ZOrder = i
        Next
        Backglass.currentData.IsDirty = True
        Backglass.currentTabPage.Invalidate()
        Backglass.currentTabPage.RefreshIllumination()
        UpdateStatusBar(Me, Backglass.currentTabPage)
    End Sub

    Private Sub BringToFrontEnhanced_Click(sender As Object, e As EventArgs)
        ArrangeSelectedBulb(3)
    End Sub

    Private Sub MoveForwardEnhanced_Click(sender As Object, e As EventArgs)
        ArrangeSelectedBulb(2)
    End Sub

    Private Sub MoveBackwardEnhanced_Click(sender As Object, e As EventArgs)
        ArrangeSelectedBulb(1)
    End Sub

    Private Sub SendToBackEnhanced_Click(sender As Object, e As EventArgs)
        ArrangeSelectedBulb(0)
    End Sub

    Private Sub Cut_Click(sender As Object, e As EventArgs) Handles tsmiCut.Click
        If Backglass.currentTabPage Is Nothing Then Return
        If RemoveSelectedDMDCanvasImage(True) Then Return
        If Backglass.currentTabPage.Mouse.IsDMDCopyAreaSelected AndAlso RemoveDMDCopyWindow() Then Return
        If Backglass.currentTabPage.Mouse.SelectedBulb IsNot Nothing Then
            Dim selectedBulb = Backglass.currentTabPage.Mouse.SelectedBulb

            ' Copy all the properties of the selected bulb (must happen BEFORE removal)
            copiedBulb = New Illumination.BulbInfo
            copiedBulb.B2SID = selectedBulb.B2SID
            copiedBulb.B2SIDType = selectedBulb.B2SIDType
            copiedBulb.B2SValue = selectedBulb.B2SValue
            copiedBulb.RomID = selectedBulb.RomID
            copiedBulb.RomIDType = selectedBulb.RomIDType
            copiedBulb.RomInverted = selectedBulb.RomInverted
            copiedBulb.Name = selectedBulb.Name
            copiedBulb.Text = selectedBulb.Text
            copiedBulb.TextAlignment = selectedBulb.TextAlignment
            copiedBulb.FontName = selectedBulb.FontName
            copiedBulb.FontSize = selectedBulb.FontSize
            copiedBulb.FontStyle = selectedBulb.FontStyle
            copiedBulb.Visible = selectedBulb.Visible
            copiedBulb.Location = selectedBulb.Location
            copiedBulb.Size = selectedBulb.Size
            copiedBulb.InitialState = selectedBulb.InitialState
            copiedBulb.DualMode = selectedBulb.DualMode
            copiedBulb.Intensity = selectedBulb.Intensity
            copiedBulb.LightColor = selectedBulb.LightColor
            copiedBulb.DodgeColor = selectedBulb.DodgeColor
            copiedBulb.IlluMode = selectedBulb.IlluMode
            copiedBulb.BlinkEnabled = selectedBulb.BlinkEnabled
            copiedBulb.BlinkInterval = selectedBulb.BlinkInterval
            copiedBulb.ZOrder = selectedBulb.ZOrder
            copiedBulb.IsImageSnippit = selectedBulb.IsImageSnippit
            copiedBulb.Image = selectedBulb.Image
            CopyExtendedLightProperties(selectedBulb, copiedBulb)

            ' Copy all SnippitInfo properties
            copiedBulb.SnippitInfo = New Illumination.SnippitInfo
            copiedBulb.SnippitInfo.Brightness = selectedBulb.SnippitInfo.Brightness
            copiedBulb.SnippitInfo.BehindCanvas = selectedBulb.SnippitInfo.BehindCanvas
            copiedBulb.SnippitInfo.SnippitType = selectedBulb.SnippitInfo.SnippitType
            copiedBulb.SnippitInfo.SnippitMechID = selectedBulb.SnippitInfo.SnippitMechID
            copiedBulb.SnippitInfo.SnippitRotatingSteps = selectedBulb.SnippitInfo.SnippitRotatingSteps
            copiedBulb.SnippitInfo.SnippitRotatingInterval = selectedBulb.SnippitInfo.SnippitRotatingInterval
            copiedBulb.SnippitInfo.SnippitRotatingDirection = selectedBulb.SnippitInfo.SnippitRotatingDirection
            copiedBulb.SnippitInfo.SnippitRotatingStopBehaviour = selectedBulb.SnippitInfo.SnippitRotatingStopBehaviour
            copiedBulb.SnippitInfo.AutomaticRotationEnabled = selectedBulb.SnippitInfo.AutomaticRotationEnabled
            copiedBulb.SnippitInfo.AutomaticRotationContinuous = selectedBulb.SnippitInfo.AutomaticRotationContinuous
            copiedBulb.SnippitInfo.AutomaticRotationTriggerID = selectedBulb.SnippitInfo.AutomaticRotationTriggerID
            copiedBulb.SnippitInfo.AutomaticRotationTriggerType = selectedBulb.SnippitInfo.AutomaticRotationTriggerType
            copiedBulb.SnippitInfo.AutomaticRotationSteps = selectedBulb.SnippitInfo.AutomaticRotationSteps
            copiedBulb.SnippitInfo.AutomaticRotationInterval = selectedBulb.SnippitInfo.AutomaticRotationInterval
            copiedBulb.SnippitInfo.AutomaticRotationDirection = selectedBulb.SnippitInfo.AutomaticRotationDirection
            copiedBulb.SnippitInfo.AutomaticRotationStopBehaviour = selectedBulb.SnippitInfo.AutomaticRotationStopBehaviour

            ' Remove selected bulb image from image collection
            If copiedBulb.IsImageSnippit Then
                Backglass.currentImages.RemoveByTypeAndName(Images.eImageInfoType.IlluminationSnippits, copiedBulb.Name)
            End If

            ' Remove the selected bulb from the collection (delete operation)
            Backglass.currentBulbs.Remove(selectedBulb)

            ' Refresh screen
            Backglass.currentTabPage.Invalidate()
            Backglass.currentTabPage.RefreshIllumination()
        End If
    End Sub

    Private Sub Copy_Click(sender As Object, e As EventArgs) Handles tsmiCopy.Click
        If Backglass.currentTabPage Is Nothing Then Return
        If Backglass.currentTabPage.Mouse.SelectedBulb IsNot Nothing Then
            Dim selectedBulb = Backglass.currentTabPage.Mouse.SelectedBulb

            ' Copy all the properties of the selected bulb
            copiedBulb = New Illumination.BulbInfo
            copiedBulb.B2SID = selectedBulb.B2SID
            copiedBulb.B2SIDType = selectedBulb.B2SIDType
            copiedBulb.B2SValue = selectedBulb.B2SValue
            copiedBulb.RomID = selectedBulb.RomID
            copiedBulb.RomIDType = selectedBulb.RomIDType
            copiedBulb.RomInverted = selectedBulb.RomInverted
            copiedBulb.Name = selectedBulb.Name
            copiedBulb.Text = selectedBulb.Text
            copiedBulb.TextAlignment = selectedBulb.TextAlignment
            copiedBulb.FontName = selectedBulb.FontName
            copiedBulb.FontSize = selectedBulb.FontSize
            copiedBulb.FontStyle = selectedBulb.FontStyle
            copiedBulb.Visible = selectedBulb.Visible
            copiedBulb.Location = selectedBulb.Location
            copiedBulb.Size = selectedBulb.Size
            copiedBulb.InitialState = selectedBulb.InitialState
            copiedBulb.DualMode = selectedBulb.DualMode
            copiedBulb.Intensity = selectedBulb.Intensity
            copiedBulb.LightColor = selectedBulb.LightColor
            copiedBulb.DodgeColor = selectedBulb.DodgeColor
            copiedBulb.IlluMode = selectedBulb.IlluMode
            copiedBulb.BlinkEnabled = selectedBulb.BlinkEnabled
            copiedBulb.BlinkInterval = selectedBulb.BlinkInterval
            copiedBulb.ZOrder = selectedBulb.ZOrder
            copiedBulb.IsImageSnippit = selectedBulb.IsImageSnippit
            copiedBulb.Image = selectedBulb.Image
            CopyExtendedLightProperties(selectedBulb, copiedBulb)

            ' Copy all SnippitInfo properties
            copiedBulb.SnippitInfo = New Illumination.SnippitInfo
            copiedBulb.SnippitInfo.Brightness = selectedBulb.SnippitInfo.Brightness
            copiedBulb.SnippitInfo.BehindCanvas = selectedBulb.SnippitInfo.BehindCanvas
            copiedBulb.SnippitInfo.SnippitType = selectedBulb.SnippitInfo.SnippitType
            copiedBulb.SnippitInfo.SnippitMechID = selectedBulb.SnippitInfo.SnippitMechID
            copiedBulb.SnippitInfo.SnippitRotatingSteps = selectedBulb.SnippitInfo.SnippitRotatingSteps
            copiedBulb.SnippitInfo.SnippitRotatingInterval = selectedBulb.SnippitInfo.SnippitRotatingInterval
            copiedBulb.SnippitInfo.SnippitRotatingDirection = selectedBulb.SnippitInfo.SnippitRotatingDirection
            copiedBulb.SnippitInfo.SnippitRotatingStopBehaviour = selectedBulb.SnippitInfo.SnippitRotatingStopBehaviour
            copiedBulb.SnippitInfo.AutomaticRotationEnabled = selectedBulb.SnippitInfo.AutomaticRotationEnabled
            copiedBulb.SnippitInfo.AutomaticRotationContinuous = selectedBulb.SnippitInfo.AutomaticRotationContinuous
            copiedBulb.SnippitInfo.AutomaticRotationTriggerID = selectedBulb.SnippitInfo.AutomaticRotationTriggerID
            copiedBulb.SnippitInfo.AutomaticRotationTriggerType = selectedBulb.SnippitInfo.AutomaticRotationTriggerType
            copiedBulb.SnippitInfo.AutomaticRotationSteps = selectedBulb.SnippitInfo.AutomaticRotationSteps
            copiedBulb.SnippitInfo.AutomaticRotationInterval = selectedBulb.SnippitInfo.AutomaticRotationInterval
            copiedBulb.SnippitInfo.AutomaticRotationDirection = selectedBulb.SnippitInfo.AutomaticRotationDirection
            copiedBulb.SnippitInfo.AutomaticRotationStopBehaviour = selectedBulb.SnippitInfo.AutomaticRotationStopBehaviour
        End If
    End Sub

    Private Sub Paste_Click(sender As Object, e As EventArgs) Handles tsmiPaste.Click
        If Backglass.currentTabPage Is Nothing Then Return
        If copiedBulb IsNot Nothing Then
            ' Offset location slightly
            copiedBulb.Location.X += 10
            copiedBulb.Location.Y += 10

            ' Create a new illumination bulb with all properties copied
            Dim newBulb As New Illumination.BulbInfo
            newBulb.B2SID = copiedBulb.B2SID
            newBulb.B2SIDType = copiedBulb.B2SIDType
            newBulb.B2SValue = copiedBulb.B2SValue
            newBulb.RomID = copiedBulb.RomID
            newBulb.RomIDType = copiedBulb.RomIDType
            newBulb.RomInverted = copiedBulb.RomInverted
            newBulb.Name = copiedBulb.Name
            newBulb.Text = copiedBulb.Text
            newBulb.TextAlignment = copiedBulb.TextAlignment
            newBulb.FontName = copiedBulb.FontName
            newBulb.FontSize = copiedBulb.FontSize
            newBulb.FontStyle = copiedBulb.FontStyle
            newBulb.Visible = copiedBulb.Visible
            newBulb.Location = copiedBulb.Location
            newBulb.Size = copiedBulb.Size
            newBulb.InitialState = copiedBulb.InitialState
            newBulb.DualMode = copiedBulb.DualMode
            newBulb.Intensity = copiedBulb.Intensity
            newBulb.LightColor = copiedBulb.LightColor
            newBulb.DodgeColor = copiedBulb.DodgeColor
            newBulb.IlluMode = copiedBulb.IlluMode
            newBulb.BlinkEnabled = copiedBulb.BlinkEnabled
            newBulb.BlinkInterval = copiedBulb.BlinkInterval
            newBulb.ZOrder = copiedBulb.ZOrder
            newBulb.IsImageSnippit = copiedBulb.IsImageSnippit
            newBulb.Image = copiedBulb.Image
            CopyExtendedLightProperties(copiedBulb, newBulb)

            ' Paste all SnippitInfo properties
            newBulb.SnippitInfo = New Illumination.SnippitInfo
            newBulb.SnippitInfo.Brightness = copiedBulb.SnippitInfo.Brightness
            newBulb.SnippitInfo.BehindCanvas = copiedBulb.SnippitInfo.BehindCanvas
            newBulb.SnippitInfo.SnippitType = copiedBulb.SnippitInfo.SnippitType
            newBulb.SnippitInfo.SnippitMechID = copiedBulb.SnippitInfo.SnippitMechID
            newBulb.SnippitInfo.SnippitRotatingSteps = copiedBulb.SnippitInfo.SnippitRotatingSteps
            newBulb.SnippitInfo.SnippitRotatingInterval = copiedBulb.SnippitInfo.SnippitRotatingInterval
            newBulb.SnippitInfo.SnippitRotatingDirection = copiedBulb.SnippitInfo.SnippitRotatingDirection
            newBulb.SnippitInfo.SnippitRotatingStopBehaviour = copiedBulb.SnippitInfo.SnippitRotatingStopBehaviour
            newBulb.SnippitInfo.AutomaticRotationEnabled = copiedBulb.SnippitInfo.AutomaticRotationEnabled
            newBulb.SnippitInfo.AutomaticRotationContinuous = copiedBulb.SnippitInfo.AutomaticRotationContinuous
            newBulb.SnippitInfo.AutomaticRotationTriggerID = copiedBulb.SnippitInfo.AutomaticRotationTriggerID
            newBulb.SnippitInfo.AutomaticRotationTriggerType = copiedBulb.SnippitInfo.AutomaticRotationTriggerType
            newBulb.SnippitInfo.AutomaticRotationSteps = copiedBulb.SnippitInfo.AutomaticRotationSteps
            newBulb.SnippitInfo.AutomaticRotationInterval = copiedBulb.SnippitInfo.AutomaticRotationInterval
            newBulb.SnippitInfo.AutomaticRotationDirection = copiedBulb.SnippitInfo.AutomaticRotationDirection
            newBulb.SnippitInfo.AutomaticRotationStopBehaviour = copiedBulb.SnippitInfo.AutomaticRotationStopBehaviour

            ' Add the new bulb image to the image collection
            If newBulb.IsImageSnippit Then
                Dim imageInfo As Images.ImageInfo = New Images.ImageInfo(Images.eImageInfoType.IlluminationSnippits)
                imageInfo.Text = newBulb.Name
                imageInfo.Image = newBulb.Image
                Backglass.currentImages.Insert(Images.eImageInfoType.Title4IlluminationSnippits, imageInfo)
            End If

            ' Add the new bulb to the collection
            If Backglass.currentTabPage.BackglassData.IsDMDImageShown Then
                newBulb.ParentForm = eParentForm.DMD
                Backglass.currentTabPage.BackglassData.DMDBulbs.Add(newBulb)
            Else
                newBulb.ParentForm = eParentForm.Backglass
                Backglass.currentTabPage.BackglassData.Bulbs.Add(newBulb)
            End If

            ' Select new bulb
            Backglass.currentTabPage.Mouse.SelectedBulb = newBulb

            ' Refresh screen
            Backglass.currentTabPage.BackglassData.IsDirty = True
            ' Enhanced 3.0.6 Stage 5: Stage 3 cache signatures notice the new
            ' bulb automatically. Avoid the old illumination off/on toggle,
            ' which caused multiple full render invalidations for every paste.
            Backglass.currentTabPage.Invalidate()
        End If
    End Sub

    Private Sub tsmiDelete_Click(sender As System.Object, e As System.EventArgs) Handles tsmiDelete.Click
        If Backglass.currentTabPage Is Nothing Then Return
        If RemoveSelectedDMDCanvasImage(False) Then Return
        If Backglass.currentTabPage.Mouse.IsDMDCopyAreaSelected AndAlso RemoveDMDCopyWindow() Then Return
        If Backglass.currentTabPage.Mouse.SelectedItem IsNot Nothing Then
            Backglass.currentTabPage.Mouse.KeyIsPressed(False, Keys.Delete)
        End If
    End Sub

#End Region

#Region "view"

    Private zoomAtWindow As Integer = 100

    Private Sub ZoomIn_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles tsbZoomIn.Click, tsmiZoomIn.Click
        If Backglass.currentTabPage IsNot Nothing Then
            If tscmbZoomInPercent.Text.Equals("Window", StringComparison.CurrentCultureIgnoreCase) Then
                tscmbZoomInPercent.Text = zoomAtWindow.ToString() & "%"
            End If
            If IsNumeric(tscmbZoomInPercent.Text.Replace("%", "")) Then
                Dim currentzoom As Integer = CInt(tscmbZoomInPercent.Text.Replace("%", ""))
                Dim lastzoom As Integer = CInt(zooms(0))
                For Each zoom As String In zooms
                    If CInt(zoom) <= currentzoom Then
                        'currentBackglass.Zoom(lastzoom)
                        tscmbZoomInPercent.Text = lastzoom.ToString() & "%"
                        Exit For
                    End If
                    lastzoom = CInt(zoom)
                Next
            End If
        End If
    End Sub
    Private Sub ZoomOut_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles tsbZoomOut.Click, tsmiZoomOut.Click
        If Backglass.currentTabPage IsNot Nothing Then
            If tscmbZoomInPercent.Text.Equals("Window", StringComparison.CurrentCultureIgnoreCase) Then
                tscmbZoomInPercent.Text = zoomAtWindow.ToString() & "%"
            End If
            If IsNumeric(tscmbZoomInPercent.Text.Replace("%", "")) Then
                Dim currentzoom As Integer = CInt(tscmbZoomInPercent.Text.Replace("%", ""))
                For Each zoom As String In zooms
                    If CInt(zoom) < currentzoom Then
                        'currentBackglass.Zoom(CInt(zoom))
                        tscmbZoomInPercent.Text = CInt(zoom).ToString() & "%"
                        Exit For
                    End If
                Next
            End If
        End If
    End Sub

    Private Sub ZoomToWindow_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles tsmiZoomToWindow.Click
        If Backglass.currentTabPage IsNot Nothing Then
            If tscmbZoomInPercent.SelectedIndex <> tscmbZoomInPercent.Items.Count - 1 Then
                tscmbZoomInPercent.SelectedIndex = tscmbZoomInPercent.Items.Count - 1
            Else
                tscmbZoomInPercent.Text = My.Resources.TXT_ZoomWindow
                ZoomInPercent_SelectedIndexChanged(Me, New EventArgs())
            End If
        End If
    End Sub

    Private Sub ZoomActualSize_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles tsmiZoomActualSize.Click
        If Backglass.currentTabPage IsNot Nothing Then
            tscmbZoomInPercent.Text = "100%"
        End If
    End Sub

    Private Sub ZoomInPercent_SelectedIndexChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles tscmbZoomInPercent.SelectedIndexChanged
        If Backglass.currentTabPage IsNot Nothing Then
            If IsNumeric(tscmbZoomInPercent.Text.Replace("%", "")) Then
                Backglass.currentTabPage.Zoom(CInt(tscmbZoomInPercent.Text.Replace("%", "")))
            ElseIf tscmbZoomInPercent.SelectedIndex = tscmbZoomInPercent.Items.Count - 1 Then
                Backglass.currentTabPage.Zoom("Window")
                zoomAtWindow = Backglass.currentData.Zoom
            End If
            Me.VerticalScroll.Value = 0
        End If
    End Sub
    Private Sub ZoomInPercent_KeyDown(ByVal sender As Object, ByVal e As System.Windows.Forms.KeyEventArgs) Handles tscmbZoomInPercent.KeyDown
        If e.KeyCode = Keys.Enter Then
            If IsNumeric(tscmbZoomInPercent.Text.Replace("%", "")) Then
                tscmbZoomInPercent.Text = CInt(tscmbZoomInPercent.Text.Replace("%", "")).ToString() & "%"
                If CInt(tscmbZoomInPercent.Text.Replace("%", "")) <= CInt(zooms(0)) Then
                    Backglass.currentTabPage.Zoom(CInt(tscmbZoomInPercent.Text.Replace("%", "")))
                End If
            End If
        End If
    End Sub

#End Region

#Region "image"

    Private Sub Image_DropDownOpening(ByVal sender As Object, ByVal e As System.EventArgs) Handles tsmiImage.DropDownOpening
        Dim isValid As Boolean = (Backglass.currentTabPage IsNot Nothing AndAlso Backglass.currentData.Image IsNot Nothing)
        tsmiReloadBackglassImage.Enabled = isValid
        tsmiImportDMDImage.Enabled = isValid AndAlso (Backglass.currentData.DMDType <> eDMDType.NoB2SDMD)
        tsmiImportIlluminationImage.Enabled = isValid
        tsmiGrillHeight.Enabled = isValid
        tsmiSetGrillHeight.Enabled = isValid
        tsmiSetMiniGrillHeight.Enabled = isValid
        tsmiDMDArea.Enabled = isValid
        tsmiCopyDMDImageFromBackglass.Enabled = isValid AndAlso (Backglass.currentData.DMDType <> eDMDType.NoB2SDMD)
        tsmiSetDefaultDMDLocation.Enabled = isValid AndAlso (Backglass.currentData.DMDType <> eDMDType.NoB2SDMD)
        tsmiResize.Enabled = isValid
        tsmiBrightness.Enabled = isValid
        If Backglass.currentTabPage IsNot Nothing Then
            tsmiSetGrillHeight.Checked = Backglass.currentTabPage.SetGrillHeight
            tsmiSetMiniGrillHeight.Checked = Backglass.currentTabPage.SetSmallGrillHeight
            tsmiCopyDMDImageFromBackglass.Checked = Backglass.currentTabPage.CopyDMDImageFromBackglass
            tsmiSetDefaultDMDLocation.Checked = Backglass.currentTabPage.SetDMDDefaultLocation
        End If
    End Sub

    Private Sub ImportBackgroundImage_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles tsmiImportBackglassImage.Click, tsbImportBackgroundImage.Click
        If Backglass.currentTabPage IsNot Nothing Then
            ImportBackgroundImage()
        Else
            tsmiNew.PerformClick()
        End If
        LockUnlockMenus()
    End Sub
    Private Sub ReloadBackgroundImage_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles tsmiReloadBackglassImage.Click
        If Backglass.currentTabPage IsNot Nothing Then
            If IO.File.Exists(Backglass.currentData.ImageFileName) Then
                Undo.AddEntry(New Undo.UndoEntry(Undo.Type.ImageReloaded, Backglass.currentData.Image))
                Dim oldimagesize As Size = Backglass.currentTabPage.Image.Size
                Dim image As Image = Bitmap.FromFile(Backglass.currentData.ImageFileName).Copy(True).Resized(Backglass.currentData.Image.Size)
                'Backglass.currentBulbs.Resize(oldimagesize, image.Size)
                'Backglass.currentScores.Resize(oldimagesize, image.Size)
                Backglass.currentTabPage.Image() = image
                Backglass.currentTabPage.Zoom(tscmbZoomInPercent.Text)
                Backglass.currentData.IsSavedImageDirty = True
                tscmbImage.SelectedIndex = 0
                UpdateStatusBar(Me, Backglass.currentTabPage)
            Else
                B2SMessageBox.Show(String.Format(My.Resources.MSG_CannotReloadBackPic, Backglass.currentData.ImageFileName), AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information)
            End If
        End If
    End Sub

    Private Sub ImportIlluminationImages_Click(sender As System.Object, e As System.EventArgs) Handles tsmiImportIlluminationImage.Click
        If Backglass.currentTabPage IsNot Nothing Then
            Using filedialog As OpenFileDialog = New OpenFileDialog
                With filedialog
                    .Filter = ImageFileExtensionFilter
                    .FileName = String.Empty
                    If .ShowDialog(Me) = DialogResult.OK Then
                        Try
                            Undo.AddEntry(New Undo.UndoEntry(Undo.Type.IlluminationImageImported, Backglass.currentData.Image))
                            Dim image As Image = Bitmap.FromFile(.FileName).Copy(True)
                            Backglass.currentData.Images.Insert(Images.eImageInfoType.Title4IlluminationImages, New Images.ImageInfo(Images.eImageInfoType.IlluminationImage, .FileName, image))
                        Catch
                            B2SMessageBox.Show(My.Resources.MSG_CannotLoadPic, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error)
                        Finally
                            LoadToolResourcesForm()
                            UpdateStatusBar(Me, Backglass.currentTabPage)
                        End Try
                    End If
                End With
            End Using
        End If
    End Sub
    Private Sub ImportDMDImage_Click(sender As System.Object, e As System.EventArgs) Handles tsmiImportDMDImage.Click
        If Backglass.currentTabPage IsNot Nothing Then
            Using filedialog As OpenFileDialog = New OpenFileDialog
                With filedialog
                    .Filter = ImageFileExtensionFilter
                    .FileName = String.Empty
                    If .ShowDialog(Me) = DialogResult.OK Then
                        Try
                            Undo.AddEntry(New Undo.UndoEntry(Undo.Type.DMDImageImported, Backglass.currentData.DMDImage))
                            Dim image As Image = Bitmap.FromFile(.FileName).Copy(True)
                            Backglass.currentTabPage.DMDImage(.FileName) = image
                            tscmbImage.SelectedIndex = 1
                            Backglass.currentData.Images.Insert(Images.eImageInfoType.Title4DMDImages, New Images.ImageInfo(Images.eImageInfoType.DMDImage, .FileName, image))
                            Backglass.currentData.IsSavedDMDImageDirty = True
                        Catch
                            B2SMessageBox.Show(My.Resources.MSG_CannotLoadPic, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error)
                        Finally
                            LoadToolResourcesForm()
                            UpdateStatusBar(Me, Backglass.currentTabPage)
                        End Try
                    End If
                End With
            End Using
        End If
    End Sub

    Private Sub CopyDMDImageFromBackglass_Click(sender As System.Object, e As System.EventArgs) Handles tsmiCopyDMDImageFromBackglass.Click
        If Backglass.currentTabPage IsNot Nothing Then
            If Backglass.currentData.DMDCopyArea.Location = Nothing OrElse Backglass.currentData.DMDCopyArea.Size = Nothing Then
                Dim size As Size = Backglass.currentData.Image.Size
                Backglass.currentData.DMDCopyArea.Location = New Point(CInt(size.Width / 2) - CInt(size.Width / 6), CInt(size.Height / 4) * 3)
                Backglass.currentData.DMDCopyArea.Size = New Size(CInt(size.Width / 3), CInt(size.Height / 6))
            End If
            Backglass.currentTabPage.CopyDMDImageFromBackglass = Not Backglass.currentTabPage.CopyDMDImageFromBackglass
            tsmiCopyDMDImageFromBackglass.Checked = Backglass.currentTabPage.CopyDMDImageFromBackglass
            Backglass.currentTabPage.SetGrillHeight = False
            Backglass.currentTabPage.SetSmallGrillHeight = False
            Backglass.currentTabPage.SetDMDDefaultLocation = False
        End If
    End Sub
    Private Sub SetDefaultDMDLocation_Click(sender As System.Object, e As System.EventArgs) Handles tsmiSetDefaultDMDLocation.Click
        If Backglass.currentTabPage IsNot Nothing Then
            If Backglass.currentData.DMDImage IsNot Nothing Then
                Backglass.currentTabPage.SetDMDDefaultLocation = Not Backglass.currentTabPage.SetDMDDefaultLocation
                tsmiSetDefaultDMDLocation.Checked = Backglass.currentTabPage.SetDMDDefaultLocation
            Else
                B2SMessageBox.Show(My.Resources.MSG_NoDMDFile4SettingLocation, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Exclamation)
            End If
            Backglass.currentTabPage.SetGrillHeight = False
            Backglass.currentTabPage.SetSmallGrillHeight = False
            Backglass.currentTabPage.CopyDMDImageFromBackglass = False
        End If
    End Sub

    Private Sub SetGrillHeight_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles tsmiSetGrillHeight.Click
        If Backglass.currentTabPage IsNot Nothing Then
            Backglass.currentTabPage.SetGrillHeight = Not Backglass.currentTabPage.SetGrillHeight
            tsmiSetGrillHeight.Checked = Backglass.currentTabPage.SetGrillHeight
            Backglass.currentTabPage.SetSmallGrillHeight = False
            Backglass.currentTabPage.CopyDMDImageFromBackglass = False
            Backglass.currentTabPage.SetDMDDefaultLocation = False
        End If
    End Sub
    Private Sub SetMiniGrillHeight_Click(sender As System.Object, e As System.EventArgs) Handles tsmiSetMiniGrillHeight.Click
        If Backglass.currentTabPage IsNot Nothing Then
            Backglass.currentTabPage.SetSmallGrillHeight = Not Backglass.currentTabPage.SetSmallGrillHeight
            tsmiSetMiniGrillHeight.Checked = Backglass.currentTabPage.SetSmallGrillHeight
            Backglass.currentTabPage.SetGrillHeight = False
            Backglass.currentTabPage.CopyDMDImageFromBackglass = False
            Backglass.currentTabPage.SetDMDDefaultLocation = False
        End If
    End Sub

    Private Sub Resize_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles tsmiResize.Click
        If Backglass.currentTabPage IsNot Nothing Then
            Dim image As Image = If(Backglass.currentData.IsDMDImageShown, Backglass.currentData.DMDImage, Backglass.currentData.Image)
            If image IsNot Nothing Then
                Dim newsize As Size = image.Size
                If formResize.ShowDialog(Me, newsize) = Windows.Forms.DialogResult.OK Then
                    Backglass.currentBulbs.Resize(image.Size, newsize)
                    Backglass.currentScores.Resize(image.Size, newsize)
                    Undo.AddEntry(New Undo.UndoEntry(Undo.Type.ImageResized, image, Backglass.currentData.IsDMDImageShown))
                    Dim newimage As Image = image.Resized(newsize)
                    If Backglass.currentData.IsDMDImageShown Then
                        Backglass.currentImages.Resize(Images.eImageInfoType.DMDImage, newsize)
                        Backglass.currentTabPage.DMDImage = newimage
                    Else
                        Backglass.currentImages.Resize(Images.eImageInfoType.BackgroundImage, newsize)
                        Backglass.currentTabPage.Image = newimage
                    End If
                    UpdateStatusBar(Me, Backglass.currentTabPage)
                End If
            End If
        End If
    End Sub

    Private Sub Brightness_Click(sender As System.Object, e As System.EventArgs) Handles tsmiBrightness.Click
        If Backglass.currentTabPage IsNot Nothing Then
            Dim image As Image = If(Backglass.currentData.IsDMDImageShown, Backglass.currentData.DMDImage, Backglass.currentData.Image)
            If image IsNot Nothing Then
                If formBrightness.ShowDialog(Me, image) = Windows.Forms.DialogResult.OK Then
                    Undo.AddEntry(New Undo.UndoEntry(Undo.Type.ImageBrightnessChanged, If(Backglass.currentData.IsDMDImageShown, Backglass.currentData.DMDImage, Backglass.currentData.Image), Backglass.currentData.IsDMDImageShown))
                    If Backglass.currentData.IsDMDImageShown Then
                        Backglass.currentTabPage.DMDImage = image
                    Else
                        Backglass.currentTabPage.Image = image
                    End If
                    Backglass.currentTabPage.Zoom(tscmbZoomInPercent.Text)
                End If
            End If
        End If
    End Sub

#End Region

#Region "scores, reels and leds"

    Private Sub ReelsLEDs_DropDownOpening(ByVal sender As Object, ByVal e As System.EventArgs) Handles tsmiReelsLEDs.DropDownOpening
        Dim isValid As Boolean = (Backglass.currentTabPage IsNot Nothing AndAlso Backglass.currentData.Image IsNot Nothing)
        tsmiChooseReelType.Enabled = isValid
        tsmiShowScoreFrames.Enabled = isValid
        tsmiAddNewReelOrLEDFrame.Enabled = isValid
        tsmiShowScoring.Enabled = isValid
    End Sub

    Private Sub ChooseReelType_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles tsmiChooseReelType.Click
        If Backglass.currentTabPage IsNot Nothing Then
            Dim reeltype As String = Backglass.currentData.ReelType
            Dim reelcolor As Color = Backglass.currentData.ReelColor
            Dim reelrollingdirection As eReelRollingDirection = Backglass.currentData.ReelRollingDirection
            Dim reelrollinginterval As Integer = Backglass.currentData.ReelRollingInterval
            Dim reelintermediatecount As Integer = Backglass.currentData.ReelIntermediateImageCount
            Dim usedream7leds As Boolean = Backglass.currentData.UseDream7LEDs
            Dim d7glow As Single = Backglass.currentData.D7Glow
            Dim d7thickness As Single = Backglass.currentData.D7Thickness
            Dim d7shear As Single = Backglass.currentData.D7Shear
            If formReelType.ShowDialog(Me, reeltype, reelcolor, reelrollingdirection, reelrollinginterval, reelintermediatecount, usedream7leds, d7glow, d7thickness, d7shear) = Windows.Forms.DialogResult.OK Then
                Backglass.currentData.ReelType = reeltype
                Backglass.currentData.ReelColor = reelcolor
                Backglass.currentData.ReelRollingDirection = reelrollingdirection
                Backglass.currentData.ReelRollingInterval = reelrollinginterval
                Backglass.currentData.ReelIntermediateImageCount = reelintermediatecount
                Backglass.currentData.UseDream7LEDs = usedream7leds
                Backglass.currentData.D7Glow = d7glow
                Backglass.currentData.D7Thickness = d7thickness
                Backglass.currentData.D7Shear = d7shear
                LoadToolReelsAndLEDsForm(True)
                Backglass.currentTabPage.Invalidate()
            End If
            If formToolReelsAndLEDs IsNot Nothing Then formToolReelsAndLEDs.ReloadReels()
        End If
    End Sub

    Private Sub ShowScoreFrames_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles tsmiShowScoreFrames.Click
        If Backglass.currentTabPage IsNot Nothing Then
            tsmiShowScoreFrames.Checked = Not tsmiShowScoreFrames.Checked
            tsbShowScoring.Checked = tsmiShowScoreFrames.Checked
            Backglass.currentTabPage.ShowScoreFrames = tsmiShowScoreFrames.Checked
            If tsmiShowScoreFrames.Checked Then
                CheckToolReelsAndLEDsForm()
                ShowToolReelsAndLEDsForm()
            End If
        End If
    End Sub

    Private Sub AddNewReelOrLEDFrame_Click(sender As System.Object, e As System.EventArgs) Handles tsmiAddNewReelOrLEDFrame.Click, tsbAddNewReelOrLEDFrame.Click
        If Backglass.currentTabPage IsNot Nothing Then
            ' maybe show frames
            If Not Backglass.currentTabPage.ShowScoreFrames Then
                tsmiShowScoreFrames.PerformClick()
            End If
            Backglass.currentTabPage.ShowScoreFrames = True
            ' add score frame
            Backglass.currentTabPage.ReelsAndLEDs_AddScore()
        End If
    End Sub

    Private Sub ShowScoring_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles tsmiShowScoring.Click
        If Backglass.currentTabPage IsNot Nothing Then
            tsmiShowScoring.Checked = Not tsmiShowScoring.Checked
            tsbShowScoring.Checked = tsmiShowScoring.Checked
            Backglass.currentTabPage.ShowScoring = tsmiShowScoring.Checked
            Backglass.currentTabPage.Invalidate()
        End If
    End Sub

    Private Sub ShowScoringToolbar_Click(ByVal sender As Object, ByVal e As EventArgs) Handles tsbShowScoring.Click
        ' This toolbar command displays the reel/LED frame outlines. Route it
        ' through the existing Show Score Frames workflow.
        ShowScoreFrames_Click(tsmiShowScoreFrames, e)
    End Sub

#End Region

#Region "illumination"

    Private Sub Illumination_DropDownOpening(ByVal sender As Object, ByVal e As System.EventArgs) Handles tsmiIllumination.DropDownOpening
        Dim isValid As Boolean = (Backglass.currentTabPage IsNot Nothing AndAlso Backglass.currentData.Image IsNot Nothing)
        tsmiShowIlluFrames.Enabled = isValid
        tsmiAddNewBulbFrame.Enabled = isValid
        tsmiAddANewIlluminationSnippit.Enabled = isValid
        tsmiShowIllumination.Enabled = isValid
        tsmiShowIlluminationWithAccurateIntensity.Enabled = isValid
        tsmiManageAnimations.Enabled = isValid
        tsmiTrimAllSnippits.Enabled = isValid
    End Sub

    Private Sub ShowIlluFrames_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles tsmiShowIlluFrames.Click, tsbShowIlluFrames.Click, tsbShowScoreFrames.Click
        If Backglass.currentTabPage IsNot Nothing Then
            tsmiShowIlluFrames.Checked = Not tsmiShowIlluFrames.Checked
            tsbShowIlluFrames.Checked = tsmiShowIlluFrames.Checked
            tsbShowScoreFrames.Checked = tsmiShowIlluFrames.Checked
            Backglass.currentTabPage.ShowIlluFrames = tsmiShowIlluFrames.Checked
            If tsmiShowIlluFrames.Checked Then
                If formToolIllumination Is Nothing Then formToolIllumination = New formToolIllumination()
                ShowToolIlluminationForm()
            End If
        End If
    End Sub

    Private Sub AddNewBulbFrame_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles tsmiAddNewBulbFrame.Click, tsbAddNewBulbFrame.Click
        If Backglass.currentTabPage IsNot Nothing Then
            ' maybe show frames
            If Not Backglass.currentTabPage.ShowIlluFrames Then
                tsmiShowIlluFrames.PerformClick()
            End If
            ' add bulb
            Backglass.currentTabPage.Illumination_AddBulb()
        End If
    End Sub
    Private Sub AddNewFlasher_Click(ByVal sender As Object, ByVal e As EventArgs)
        If Backglass.currentTabPage Is Nothing Then Return

        If Not Backglass.currentTabPage.ShowIlluFrames Then tsmiShowIlluFrames.PerformClick()
        Backglass.currentTabPage.Illumination_AddFlasher()
    End Sub
    Private Sub AddANewIlluminationSnippit_Click(sender As System.Object, e As System.EventArgs) Handles tsmiAddANewIlluminationSnippit.Click
        If Backglass.currentTabPage IsNot Nothing Then
            ' maybe show frames
            If Not Backglass.currentTabPage.ShowIlluFrames Then
                tsmiShowIlluFrames.PerformClick()
            End If
            ' add bulb
            If formAddSnippit Is Nothing Then formAddSnippit = New formAddSnippit()
            formAddSnippit.ShowDialog(Me)
        End If
    End Sub

    Private Sub MakeSnippetFromCurrentImage_Click(sender As Object, e As EventArgs)
        If Backglass.currentTabPage Is Nothing OrElse Backglass.currentData Is Nothing Then Return

        Dim currentPicture As B2SPictureBox = Backglass.currentTabPage.CurrentPictureBox
        If currentPicture Is Nothing OrElse currentPicture.Image Is Nothing Then
            MessageBox.Show(Me, "Load a Backglass or DMD image before making a snippet.",
                            "Make Snippet", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        If Not Backglass.currentTabPage.ShowIlluFrames Then tsmiShowIlluFrames.PerformClick()

        ' The temporary light gives Quick Selection a place to store its full-
        ' canvas alpha mask.  The finished snippet is deliberately created by
        ' the normal Add Snippet path so save/export and legacy behavior stay
        ' identical to every other illumination snippet.
        Dim selection As New Illumination.BulbInfo() With {
            .Name = "Make Snippet Selection",
            .Location = Point.Empty,
            .Size = currentPicture.Image.Size,
            .IsImageSnippit = True
        }

        Dim selectedBounds As Rectangle = Rectangle.Empty
        Dim snippetImage As Bitmap = Nothing
        Using selectionEditor As New formQuickSelection(selection, currentPicture.Image)
            If selectionEditor.ShowDialog(Me) <> DialogResult.OK Then Return

            selectedBounds = Rectangle.Intersect(selectionEditor.SelectedBounds,
                                                  New Rectangle(Point.Empty, currentPicture.Image.Size))
            If selectedBounds.Width <= 0 OrElse selectedBounds.Height <= 0 Then
                MessageBox.Show(Me, "Select part of the image before clicking OK.",
                                "Make Snippet", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Using croppedSource As New Bitmap(selectedBounds.Width, selectedBounds.Height,
                                              Drawing.Imaging.PixelFormat.Format32bppArgb)
                Using graphics As Graphics = Graphics.FromImage(croppedSource)
                    graphics.Clear(Color.Transparent)
                    graphics.CompositingMode = Drawing.Drawing2D.CompositingMode.SourceCopy
                    graphics.DrawImage(currentPicture.Image,
                                       New Rectangle(0, 0, croppedSource.Width, croppedSource.Height),
                                       selectedBounds, GraphicsUnit.Pixel)
                End Using
                snippetImage = Illumination.Lights.CreateSelectionMaskedSnippet(croppedSource,
                                                                                 selection.SelectionMaskData,
                                                                                 selectedBounds)
            End Using
        End Using

        If snippetImage Is Nothing Then
            MessageBox.Show(Me, "The selected pixels could not be made into a snippet.",
                            "Make Snippet", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        Dim snippetName As String = NextMadeSnippetName()
        Backglass.currentTabPage.Illumination_AddSnippit(snippetName, snippetImage, selectedBounds.Location)
        Backglass.currentData.Images.Insert(Images.eImageInfoType.Title4IlluminationSnippits,
                                            New Images.ImageInfo(Images.eImageInfoType.IlluminationSnippits,
                                                                 snippetName, snippetImage))
        LoadToolResourcesForm()
        If formToolLayers IsNot Nothing AndAlso Not formToolLayers.IsDisposed Then formToolLayers.RefreshLayers()
    End Sub

    Private Function NextMadeSnippetName() As String
        Const baseName As String = "Made Snippet"
        Dim suffix As Integer = 1
        Do
            ' Do not end generated names with a number: legacy Add Snippet treats
            ' trailing digits as an optional ROM ID.
            Dim candidate As String = If(suffix = 1, baseName, baseName & " (" & suffix.ToString() & ")")
            Dim exists As Boolean = False
            For Each bulb As Illumination.BulbInfo In Backglass.currentData.Bulbs
                If String.Equals(bulb.Name, candidate, StringComparison.OrdinalIgnoreCase) Then
                    exists = True
                    Exit For
                End If
            Next
            If Not exists Then
                For Each bulb As Illumination.BulbInfo In Backglass.currentData.DMDBulbs
                    If String.Equals(bulb.Name, candidate, StringComparison.OrdinalIgnoreCase) Then
                        exists = True
                        Exit For
                    End If
                Next
            End If
            If Not exists Then Return candidate
            suffix += 1
        Loop
    End Function

    Private Sub ShowIllumination_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles tsmiShowIllumination.Click, tsbShowIllumination.Click
        Cursor.Current = Cursors.WaitCursor
        If Backglass.currentTabPage IsNot Nothing Then
            tsmiShowIllumination.Checked = Not tsmiShowIllumination.Checked
            tsbShowIllumination.Checked = tsmiShowIllumination.Checked
            tsmiShowIlluminationWithAccurateIntensity.Enabled = Not tsmiShowIllumination.Checked
            tsbShowIlluminationWithAccurateIntensity.Enabled = Not tsmiShowIllumination.Checked
            Backglass.currentTabPage.ShowIllumination = tsmiShowIllumination.Checked
        End If
        Cursor.Current = Cursors.Default
    End Sub
    Private Sub ShowIlluminationWithAccurateIntensity_Click(sender As System.Object, e As System.EventArgs) Handles tsmiShowIlluminationWithAccurateIntensity.Click, tsbShowIlluminationWithAccurateIntensity.Click
        Cursor.Current = Cursors.WaitCursor
        If Backglass.currentTabPage IsNot Nothing Then
            tsmiShowIlluminationWithAccurateIntensity.Checked = Not tsmiShowIlluminationWithAccurateIntensity.Checked
            tsbShowIlluminationWithAccurateIntensity.Checked = tsmiShowIlluminationWithAccurateIntensity.Checked
            tsmiShowIllumination.Enabled = Not tsmiShowIlluminationWithAccurateIntensity.Checked
            tsbShowIllumination.Enabled = Not tsmiShowIlluminationWithAccurateIntensity.Checked
            Backglass.currentTabPage.ShowIntensityIllumination = tsmiShowIlluminationWithAccurateIntensity.Checked
        End If
        Cursor.Current = Cursors.Default
    End Sub

    Private Sub ManageAnimations_Click(sender As System.Object, e As System.EventArgs) Handles tsmiManageAnimations.Click
        If Backglass.currentData IsNot Nothing AndAlso Backglass.currentTabPage IsNot Nothing Then
            ' maybe show and filter frames
            If Not Backglass.currentTabPage.ShowIlluFrames Then
                tsmiShowIlluFrames.PerformClick()
            End If
            ' Opening the animation editor must not silently switch the canvas to
            ' the "with name" lamp filter. That filter hides ordinary unnamed
            ' lights and makes it appear that illumination frames were destroyed.
            ' Preserve the user's current filter and frame visibility instead.
            ' open animation dialog
            If formAnimations Is Nothing OrElse formAnimations.IsDisposed Then
                formAnimations = New formAnimations()
            End If
            If formAnimations.Visible Then
                If formAnimations.WindowState = FormWindowState.Minimized Then formAnimations.WindowState = FormWindowState.Normal
                formAnimations.BringToFront()
                formAnimations.Activate()
                Return
            End If
            formAnimations.Show(Me)
        End If
    End Sub

    Private Sub TrimAllSnippits_Click(sender As System.Object, e As System.EventArgs) Handles tsmiTrimAllSnippits.Click
        If Backglass.currentData IsNot Nothing AndAlso Backglass.currentTabPage IsNot Nothing Then
            Dim pictureAnimationFramesSkipped As Boolean = False
            For Each selected_bulb As Illumination.BulbInfo In Backglass.currentBulbs
                If selected_bulb IsNot Nothing AndAlso selected_bulb.Image IsNot Nothing Then
                    ' PA_ frames share one transparent canvas. Trimming them one by
                    ' one removes the offsets that make an actor travel between
                    ' frames and breaks the one-object move/resize contract.
                    If Not String.IsNullOrEmpty(selected_bulb.Name) AndAlso
                       selected_bulb.Name.StartsWith("PA_", StringComparison.OrdinalIgnoreCase) Then
                        pictureAnimationFramesSkipped = True
                        Continue For
                    End If

                    Dim trim_rect As Rectangle = TrimImage(selected_bulb.Image)

                    If trim_rect.X > 0 Or trim_rect.Y > 0 Or trim_rect.Width < selected_bulb.Image.Width Or trim_rect.Height < selected_bulb.Image.Height Then
                        Dim trimmed As New Bitmap(trim_rect.Width, trim_rect.Height, selected_bulb.Image.PixelFormat)
                        Using graphics As Graphics = Graphics.FromImage(trimmed)
                            graphics.DrawImage(selected_bulb.Image, New Rectangle(0, 0, trimmed.Width, trimmed.Height), trim_rect, System.Drawing.GraphicsUnit.Pixel)
                        End Using

                        For Each bulb As Illumination.BulbInfo In Backglass.currentBulbs
                            If bulb.Name = selected_bulb.Name Then
                                bulb.Image = DirectCast(trimmed, Image)
                                bulb.Size.Width = bulb.Image.Width
                                bulb.Size.Height = bulb.Image.Height
                                bulb.Location += trim_rect.Location
                            End If
                        Next

                        Backglass.currentImages.SetNewImage(Images.eImageInfoType.IlluminationSnippits, selected_bulb.Name, selected_bulb.Image)
                    End If
                End If
            Next

            RefreshImageInfoList()
            If pictureAnimationFramesSkipped Then
                B2SMessageBox.Show("Imported picture-animation frames were left unchanged. Their shared transparent canvas controls frame-to-frame movement and cannot be trimmed individually.",
                                   "Picture Animation Protected", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End If
        End If
    End Sub

    Private Sub IDFilter_SelectedIndexChanged(sender As Object, e As System.EventArgs) Handles tscmbIDFilter.SelectedIndexChanged
        If Backglass.currentTabPage IsNot Nothing AndAlso Not ignoreChanges Then
            Dim filter As String = If(tscmbIDFilter.Text.Contains(" "), tscmbIDFilter.Text.Substring(0, tscmbIDFilter.Text.IndexOf(" ")), tscmbIDFilter.Text)
            If tscmbIDFilter.SelectedIndex = 1 Then
                filter = "off"
            ElseIf tscmbIDFilter.SelectedIndex = 2 Then
                filter = "on"
            ElseIf tscmbIDFilter.SelectedIndex = 3 Then
                filter = "alwayson"
            ElseIf Backglass.currentData.DualBackglass AndAlso tscmbIDFilter.SelectedIndex = 4 Then
                filter = "authentic"
            ElseIf Backglass.currentData.DualBackglass AndAlso tscmbIDFilter.SelectedIndex = 5 Then
                filter = "fantasy"
            ElseIf tscmbIDFilter.SelectedIndex = tscmbIDFilter.Items.Count - 2 Then
                filter = "withoutid"
            ElseIf tscmbIDFilter.SelectedIndex = tscmbIDFilter.Items.Count - 1 Then
                filter = "withname"
            End If
            Backglass.currentTabPage.Illumination_SetRomFilter(filter)
        End If
    End Sub

#End Region

#Region "backglass"

    Private Sub Backglass_DropDownOpening(ByVal sender As Object, ByVal e As System.EventArgs) Handles tsmiBackglass.DropDownOpening
        Dim isValid As Boolean = (Backglass.currentTabPage IsNot Nothing AndAlso Backglass.currentData.Image IsNot Nothing)
        tsmiBackglassPreviewAndTest.Enabled = isValid
        tsmiCreateDarkBackglassImage.Enabled = isValid
        tsmiCreateIlluminatedBackglassImage.Enabled = isValid
        tsmiCreateDirectAccessBackglassCodeFile.Enabled = isValid AndAlso Backglass.currentData.DestType = eDestType.DirectB2S
        tsmiCreateMSBackglassCode.Enabled = isValid AndAlso Backglass.currentData.DestType = eDestType.VisualStudio2010
    End Sub

    Private Sub BackglassPreviewAndTest_Click(sender As System.Object, e As System.EventArgs) Handles tsmiBackglassPreviewAndTest.Click

        If Backglass.currentData IsNot Nothing AndAlso Backglass.currentTabPage IsNot Nothing Then
            If formVPM Is Nothing Then formVPM = New formVPM()
            formVPM.ShowDialog(Me)
            SaveSettings()
        End If

    End Sub

    Private Sub CreateDarkBackglassImage_Click(sender As System.Object, e As System.EventArgs) Handles tsmiCreateDarkBackglassImage.Click
        If Backglass.currentTabPage IsNot Nothing Then
            ShowProgress(0)
            Dim image As Image = Backglass.currentTabPage.DarkImage()
            ShowProgress(50)
            Dim filename As String = IO.Path.Combine(ProjectPath, Backglass.currentData.Name & " Dark" & IO.Path.GetExtension(Backglass.currentData.ImageFileName))
            IO.Directory.CreateDirectory(ProjectPath)
            ShowProgress(75)
            image.Save(filename)
            ShowProgress(100)
        End If
    End Sub
    Private Sub CreateIlluminatedBackglassImage_Click(sender As System.Object, e As System.EventArgs) Handles tsmiCreateIlluminatedBackglassImage.Click
        If Backglass.currentTabPage IsNot Nothing Then
            ShowProgress(0)
            Dim image As Image = Backglass.currentTabPage.IlluminatedImage()
            ShowProgress(50)
            Dim filename As String = IO.Path.Combine(ProjectPath, Backglass.currentData.Name & " Illuminated" & IO.Path.GetExtension(Backglass.currentData.ImageFileName))
            IO.Directory.CreateDirectory(ProjectPath)
            ShowProgress(75)
            image.Save(filename)
            ShowProgress(100)
        End If
    End Sub

    Private Sub CreateDirectAccessBackglassCodeFile_Click(sender As System.Object, e As System.EventArgs) Handles tsmiCreateDirectAccessBackglassCodeFile.Click

        If Backglass.currentTabPage IsNot Nothing Then
            SaveB2SPro(Backglass.currentData)
        End If

    End Sub
    Private Sub CreateMSBackglassCode_Click(sender As System.Object, e As System.EventArgs) Handles tsmiCreateMSBackglassCode.Click

        If Backglass.currentTabPage IsNot Nothing Then
            coding.CreateVisualStudioCode()
        End If

    End Sub

#End Region

#Region "window"

    Private Sub Window_DropDownOpening(ByVal sender As Object, ByVal e As System.EventArgs) Handles tsmiWindow.DropDownOpening
        tsmiBackglassImage.Checked = (Backglass.currentData IsNot Nothing AndAlso Not Backglass.currentData.IsDMDImageShown)
        tsmiDMDImage.Checked = (Backglass.currentData IsNot Nothing AndAlso Backglass.currentData.IsDMDImageShown)
        tsmiReelsAndLEDSettings.Checked = (formToolReelsAndLEDs IsNot Nothing AndAlso formToolReelsAndLEDs.Visible = True)
        tsmiIlluminationSettings.Checked = (formToolIllumination IsNot Nothing AndAlso formToolIllumination.Visible = True)
        tsmiHistory.Checked = (formToolUndo IsNot Nothing AndAlso formToolUndo.Visible = True)
        tsmiImages.Checked = (formToolResources IsNot Nothing AndAlso formToolResources.Visible = True)
        tsmiTranslucent.Checked = (DefaultOpacity <> 1)
    End Sub

    Private Sub BackglassImage_Click(sender As System.Object, e As System.EventArgs) Handles tsmiBackglassImage.Click
        If Backglass.currentTabPage IsNot Nothing Then
            tscmbImage.SelectedIndex = 0
            Backglass.currentTabPage.ShowBackglassImage()
            UpdateStatusBar(Me, Backglass.currentTabPage)
        End If
    End Sub
    Private Sub DMDImage_Click(sender As System.Object, e As System.EventArgs) Handles tsmiDMDImage.Click
        If Backglass.currentTabPage IsNot Nothing Then
            tscmbImage.SelectedIndex = 1
            Backglass.currentTabPage.ShowDMDImage()
            UpdateStatusBar(Me, Backglass.currentTabPage)
        End If
    End Sub
    Private Sub Image_SelectedIndexChanged(sender As Object, e As System.EventArgs) Handles tscmbImage.SelectedIndexChanged
        If tscmbImage.SelectedIndex = 0 Then
            If Backglass.currentTabPage IsNot Nothing Then Backglass.currentTabPage.ShowBackglassImage()
        ElseIf tscmbImage.SelectedIndex = 1 Then
            If Backglass.currentTabPage IsNot Nothing Then Backglass.currentTabPage.ShowDMDImage()
        End If
        UpdateStatusBar(Me, Backglass.currentTabPage)
        ' refresh rom filter
        RefreshIDFilter()
    End Sub

    Private Sub ReelsAndLEDSettings_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles tsmiReelsAndLEDSettings.Click
        CheckToolReelsAndLEDsForm()
        If Not formToolReelsAndLEDs.Visible Then
            formToolReelsAndLEDs.Show(Me)
        Else
            formToolReelsAndLEDs.Hide()
        End If
    End Sub
    Private Sub IlluminationSettings_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles tsmiIlluminationSettings.Click
        CheckToolIlluminationForm()
        If Not formToolIllumination.Visible Then
            formToolIllumination.Show(Me)
        Else
            formToolIllumination.Hide()
        End If
    End Sub
    Private Sub History_Click(sender As System.Object, e As System.EventArgs) Handles tsmiHistory.Click
        CheckToolUndoForm()
        If Not formToolUndo.Visible Then
            formToolUndo.Show(Me)
        Else
            formToolUndo.Hide()
        End If
    End Sub
    Private Sub Images_Click(sender As System.Object, e As System.EventArgs) Handles tsmiImages.Click
        CheckToolResourcesForm()
        If Not formToolResources.Visible Then
            formToolResources.Show(Me)
        Else
            formToolResources.Hide()
        End If
    End Sub

    Private Sub Translucent_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles tsmiTranslucent.Click
        If DefaultOpacity = 1 Then
            DefaultOpacity = 0.8
        Else
            DefaultOpacity = 1
        End If
        tsmiTranslucent.Checked = (DefaultOpacity <> 1)
        MyBase.SaveSettings()
        For Each form As Form In Me.OwnedForms
            If TypeOf form Is B2SBackglassDesigner.formBase Then
                form.Opacity = DefaultOpacity
            End If
        Next
    End Sub

#End Region

#Region "help"

    Private Sub CheckForUpdates_Click(sender As Object, e As EventArgs) Handles tsmiCheckForUpdates.Click
        StartUpdateCheck(True)
    End Sub

    Private Sub HelpTopics_Click(sender As System.Object, e As System.EventArgs) Handles tsmiHelpTopics.Click
        Try
            Help.ShowHelp(Me, EnsureB2SProHelpFile(), HelpNavigator.TableOfContents)
        Catch ex As Exception
            B2SMessageBox.Show(Me,
                               "B2S Pro Help could not be opened." & Environment.NewLine & Environment.NewLine & ex.Message,
                               AppTitle,
                               MessageBoxButtons.OK,
                               MessageBoxIcon.Error)
        End Try
    End Sub

    Private Function EnsureB2SProHelpFile() As String
        Const resourceName As String = "B2SProHelp.chm"
        Dim helpBytes As Byte()

        Using resourceStream As Stream = Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            If resourceStream Is Nothing Then
                Throw New InvalidOperationException("The embedded B2S Pro Help resource is missing.")
            End If

            Using buffer As New MemoryStream()
                resourceStream.CopyTo(buffer)
                helpBytes = buffer.ToArray()
            End Using
        End Using

        Dim contentHash As String
        Using sha256 = Security.Cryptography.SHA256.Create()
            contentHash = BitConverter.ToString(sha256.ComputeHash(helpBytes)).Replace("-", String.Empty).Substring(0, 12)
        End Using

        Dim helpFolder As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                                "B2S Pro",
                                                "Help")
        Directory.CreateDirectory(helpFolder)

        Dim helpFile As String = Path.Combine(helpFolder, "B2SProHelp-" & contentHash & ".chm")
        If Not File.Exists(helpFile) OrElse New FileInfo(helpFile).Length <> helpBytes.LongLength Then
            File.WriteAllBytes(helpFile, helpBytes)
        End If

        Return helpFile
    End Function

    Private Sub About_Click(sender As System.Object, e As System.EventArgs) Handles tsmiAbout.Click
        formAbout.ShowDialog(Me)
    End Sub

#End Region

#End Region


#Region "private methods"

    Private Sub LoadBackglassFile(ByVal filename As String)
        Dim backglassdata As Backglass.Data = Nothing
        Cursor.Current = Cursors.WaitCursor
        LatestImportDirectory = IO.Path.GetDirectoryName(filename)
        SaveSettings()
        Try
            coding.ImportBackglassFile(backglassdata, filename)
            If backglassdata IsNot Nothing Then
                Dim originalData As Backglass.Data = backglassdata
                Dim recoveryFile As String = RecoveryFileFor(originalData, filename)
                Dim recovered As Boolean = False
                If IsB2SProRecoveryPath(recoveryFile) AndAlso IO.File.Exists(recoveryFile) Then
                    Try
                        Dim recoveryData As Backglass.Data = Nothing
                        If coding.ImportBackglassFile(recoveryData, recoveryFile, preserveEmbeddedVSName:=True) AndAlso
                           recoveryData IsNot Nothing AndAlso
                           Not String.IsNullOrWhiteSpace(originalData.ProjectGUID) AndAlso
                           String.Equals(recoveryData.ProjectGUID, originalData.ProjectGUID, StringComparison.OrdinalIgnoreCase) Then
                            If IO.File.GetLastWriteTimeUtc(recoveryFile) > IO.File.GetLastWriteTimeUtc(filename) Then
                                Cursor.Current = Cursors.Default
                                Dim answer As DialogResult = B2SMessageBox.Show(Me,
                                    "A newer AutoRecovery copy was found for " & originalData.Name & "." & Environment.NewLine & Environment.NewLine &
                                    "Yes restores the unsaved changes. No discards the recovery copy and opens the original file.",
                                    AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                                Cursor.Current = Cursors.WaitCursor
                                If answer = DialogResult.Yes Then
                                    recoveryData.LoadedName = originalData.Name
                                    recoveryData.RecoverySourceFilePath = recoveryFile
                                    backglassdata = recoveryData
                                    recovered = True
                                Else
                                    originalData.RecoverySourceFilePath = recoveryFile
                                    RemoveRecoveryCopy(originalData)
                                End If
                            Else
                                originalData.RecoverySourceFilePath = recoveryFile
                                RemoveRecoveryCopy(originalData)
                            End If
                        End If
                    Catch ex As Exception
                        Debug.WriteLine("Could not load AutoRecovery copy; opening original: " & ex.Message)
                        ShowStatus("AutoRecovery copy could not be read; original backglass opened")
                    End Try
                End If
                LoadData(backglassdata, filename)
                If recovered AndAlso Object.ReferenceEquals(Backglass.currentData, backglassdata) Then
                    backglassdata.IsDirty = True
                End If
            End If
        Catch ex As Exception
            B2SMessageBox.Show(My.Resources.MSG_ImportError2, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
        Cursor.Current = Cursors.Default
        ShowStatus()
        LockUnlockMenus()
    End Sub

    Private Function OpenSettings(ByVal newtable As Boolean, Optional ByVal savetableas As Boolean = False) As Boolean
        Dim ret As Boolean = False
        Dim isANewTable As Boolean = (newtable AndAlso Not savetableas)
        Dim name As String = String.Empty
        Dim vsname As String = String.Empty
        Dim dualbackglass As Boolean = False
        Dim author As String = String.Empty
        Dim artwork As String = String.Empty
        Dim tabletype As eTableType = eTableType.NotDefined
        Dim addedmdefaults As Boolean = False
        Dim numberofplayers As Integer = 4
        Dim b2sdatacount As Integer = 5
        Dim dmdtype As eDMDType = eDMDType.NotDefined
        Dim commtype As eCommType = eCommType.NotDefined
        Dim desttype As eDestType = eDestType.NotDefined
        If Not isANewTable Then
            If Not savetableas Then
                name = Backglass.currentData.Name
                vsname = Backglass.currentData.VSName
                dualbackglass = Backglass.currentData.DualBackglass
                author = Backglass.currentData.Author
                artwork = Backglass.currentData.Artwork
            End If
            tabletype = Backglass.currentData.TableType
            addedmdefaults = Backglass.currentData.AddEMDefaults
            numberofplayers = Backglass.currentData.NumberOfPlayers
            b2sdatacount = Backglass.currentData.B2SDataCount
            dmdtype = Backglass.currentData.DMDType
            commtype = Backglass.currentData.CommType
            desttype = Backglass.currentData.DestType
        End If
        If formSettings.ShowDialog(Me, newtable, name, vsname, dualbackglass, author, artwork, tabletype, addedmdefaults, numberofplayers, b2sdatacount, dmdtype, commtype, desttype) = Windows.Forms.DialogResult.OK Then
            If Not String.IsNullOrEmpty(name) Then
                ret = True
                ' get data
                If isANewTable Then
                    B2STab.AddBackglass(New B2STabPage(name, vsname, dualbackglass, author, artwork, tabletype, addedmdefaults, numberofplayers, b2sdatacount, dmdtype, commtype, desttype))
                    B2STab.SelectedIndex = B2STab.TabPages.Count - 1
                Else
                    Backglass.currentData.Name = name
                    B2STab.SelectedTabPage.Text = name
                    B2STab.SelectedTabPage.Invalidate()
                    B2STab.Invalidate()
                    Backglass.currentData.VSName = vsname
                    Backglass.currentData.DualBackglass = dualbackglass
                    Backglass.currentData.Author = author
                    Backglass.currentData.Artwork = artwork
                    Backglass.currentData.TableType = tabletype
                    Backglass.currentData.AddEMDefaults = addedmdefaults
                    Backglass.currentData.NumberOfPlayers = numberofplayers
                    Backglass.currentData.B2SDataCount = b2sdatacount
                    Backglass.currentData.DMDType = dmdtype
                    Backglass.currentData.CommType = commtype
                    Backglass.currentData.DestType = desttype
                End If
            End If
        End If
        SaveSettings()
        ShowStatus()
        LockUnlockMenus()
        Return ret
    End Function

    Private Sub ImportBackgroundImage(Optional ByVal oldimagesize As Size = Nothing)
        Using filedialog As OpenFileDialog = New OpenFileDialog
            With filedialog
                .Filter = ImageFileExtensionFilter
                .FileName = String.Empty
                If .ShowDialog(Me) = DialogResult.OK Then
                    Try
                        Undo.AddEntry(New Undo.UndoEntry(Undo.Type.ImageImported, Backglass.currentData.Image))
                        Dim image As Image = Bitmap.FromFile(.FileName).Copy(True)
                        If oldimagesize <> Nothing Then
                            Backglass.currentBulbs.Resize(oldimagesize, image.Size)
                            Backglass.currentScores.Resize(oldimagesize, image.Size)
                            Dim factor As Single = oldimagesize.Height / image.Size.Height
                            Backglass.currentData.GrillHeight = Backglass.currentData.GrillHeight / factor
                            Backglass.currentData.SmallGrillHeight = Backglass.currentData.SmallGrillHeight / factor
                        ElseIf Backglass.currentData.Image IsNot Nothing Then
                            If B2SMessageBox.Show(My.Resources.MSG_BackgroundImageChange, AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1) = Windows.Forms.DialogResult.Yes Then
                                Backglass.currentBulbs.Resize(Backglass.currentData.Image.Size, image.Size)
                                Backglass.currentScores.Resize(Backglass.currentData.Image.Size, image.Size)
                                Dim factor As Single = Backglass.currentData.Image.Size.Height / image.Size.Height
                                Backglass.currentData.GrillHeight = Backglass.currentData.GrillHeight / factor
                                Backglass.currentData.SmallGrillHeight = Backglass.currentData.SmallGrillHeight / factor
                            End If
                        End If
                        Backglass.currentTabPage.Image(.FileName) = image
                        Backglass.currentData.Images.Insert(Images.eImageInfoType.Title4BackgroundImages, New Images.ImageInfo(Images.eImageInfoType.BackgroundImage, .FileName, image))
                        Backglass.currentTabPage.Zoom(tscmbZoomInPercent.Text)
                        Backglass.currentData.IsSavedImageDirty = True
                    Catch
                        B2SMessageBox.Show(My.Resources.MSG_CannotLoadPic, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error)
                    Finally
                        LoadToolResourcesForm()
                        UpdateStatusBar(Me, Backglass.currentTabPage)
                    End Try
                End If
            End With
        End Using
    End Sub

    Private Sub RefreshSettings()

        ' set undo backglass
        Undo.SelectedBackglass = Backglass.currentTabPage

        ' set some overall settings
        tsmiShowScoreFrames.Checked = If(Backglass.currentData IsNot Nothing, Backglass.currentData.ShowScoreFrames, False)
        tsbShowScoreFrames.Checked = If(Backglass.currentData IsNot Nothing, Backglass.currentData.ShowIlluFrames, False)
        tsmiShowScoring.Checked = If(Backglass.currentData IsNot Nothing, Backglass.currentData.ShowScoring, False)
        tsbShowScoring.Checked = If(Backglass.currentData IsNot Nothing, Backglass.currentData.ShowScoreFrames, False)
        tsmiShowIlluFrames.Checked = If(Backglass.currentData IsNot Nothing, Backglass.currentData.ShowIlluFrames, False)
        tsbShowIlluFrames.Checked = If(Backglass.currentData IsNot Nothing, Backglass.currentData.ShowIlluFrames, False)
        tsmiShowIllumination.Checked = If(Backglass.currentData IsNot Nothing, Backglass.currentData.ShowIllumination, False)
        tsbShowIllumination.Checked = If(Backglass.currentData IsNot Nothing, Backglass.currentData.ShowIllumination, False)
        tscmbImage.SelectedIndex = If(Backglass.currentData IsNot Nothing AndAlso Backglass.currentData.IsDMDImageShown, 1, 0)

        ' refresh reels tool window
        LoadToolReelsAndLEDsForm(True)

        ' refresh illumination tool window
        LoadToolIlluminationForm()

        ' refresh images tool window
        LoadToolResourcesForm()

        ' refresh rom filters
        RefreshIDFilter()

        UpdateStatusBar(Me, Backglass.currentTabPage)

    End Sub

    Private Sub RefreshIDFilter()
        ' refresh rom filter
        If Backglass.currentData IsNot Nothing Then
            ' store all filters
            Dim filters As String() = Nothing
            AddFilter(filters, "")
            AddFilter(filters, My.Resources.TXT_FilterOff)
            AddFilter(filters, My.Resources.TXT_FilterOn)
            AddFilter(filters, My.Resources.TXT_FilterAlwaysOn)
            If Backglass.currentData.DualBackglass Then
                AddFilter(filters, My.Resources.TXT_FilterAuthentic)
                AddFilter(filters, My.Resources.TXT_FilterFantasy)
            End If
            For Each item As KeyValuePair(Of String, Integer) In Backglass.currentUsedIDs
                AddFilter(filters, item.Key & " (" & item.Value.ToString() & ")")
            Next
            AddFilter(filters, My.Resources.TXT_FilterWithoutID)
            AddFilter(filters, My.Resources.TXT_FilterWithName)
            Dim i As Integer = 0
            Dim clearall As Boolean = (tscmbIDFilter.Items.Count <> filters.Length)
            If Not clearall Then
                For Each oldfilter As String In tscmbIDFilter.Items
                    If Not filters(i).Equals(oldfilter) Then
                        clearall = True
                        Exit For
                    End If
                    i += 1
                Next
            End If
            ' maybe refresh filter
            If clearall Then
                ignoreChanges = True
                Dim filter As String = tscmbIDFilter.Text
                If Not String.IsNullOrEmpty(filter) Then
                    filter = If(filter.Contains(" "), filter.Substring(0, filter.IndexOf(" ")), filter) & " "
                End If
                tscmbIDFilter.Items.Clear()
                For Each newfilter As String In filters
                    tscmbIDFilter.Items.Add(newfilter)
                    If Not String.IsNullOrEmpty(filter) Then
                        If (newfilter & " ").StartsWith(filter) Then filter = newfilter
                    End If
                Next
                If Not String.IsNullOrEmpty(filter) Then
                    tscmbIDFilter.Text = filter
                End If
                ignoreChanges = False
            End If
        End If
    End Sub
    Private Sub AddFilter(ByRef filters As String(), ByVal newfilter As String)
        If filters Is Nothing Then
            ReDim filters(0)
            filters(0) = newfilter
        Else
            ReDim Preserve filters(filters.Length)
            filters(filters.Length - 1) = newfilter
        End If
    End Sub

    Private Sub LockUnlockMenus()
        Dim isValid As Boolean = (Backglass.currentTabPage IsNot Nothing AndAlso Backglass.currentData.Image IsNot Nothing)
        ' Keep the enhanced toolbar button in sync when switching between an
        ' empty project and a project that already has a backglass image.  The
        ' old code only refreshed this command when the hidden Image menu was
        ' opened, leaving the visible brightness button disabled indefinitely.
        tsmiBrightness.Enabled = isValid
        tsbShowScoreFrames.Enabled = isValid
        tsbAddNewReelOrLEDFrame.Enabled = isValid
        tsbShowScoring.Enabled = isValid
        If tsbChooseReelTypeEnhanced IsNot Nothing Then tsbChooseReelTypeEnhanced.Enabled = isValid
        tsbShowIlluFrames.Enabled = isValid
        tsbAddNewBulbFrame.Enabled = isValid
        If tsbAddFlasherEnhanced IsNot Nothing Then tsbAddFlasherEnhanced.Enabled = isValid
        tsbShowIllumination.Enabled = isValid
        tsbShowIlluminationWithAccurateIntensity.Enabled = isValid
        If tsbManageAnimationsEnhanced IsNot Nothing Then tsbManageAnimationsEnhanced.Enabled = isValid
    End Sub

    Private Sub CheckToolReelsAndLEDsForm()
        If formToolReelsAndLEDs Is Nothing Then formToolReelsAndLEDs = New formToolReelsAndLEDs()
        LoadToolReelsAndLEDsForm()
    End Sub
    Private Sub ShowToolReelsAndLEDsForm()
        If Not formToolReelsAndLEDs.Visible Then formToolReelsAndLEDs.Show(Me)
    End Sub
    Private Sub LoadToolReelsAndLEDsForm(Optional ByVal refreshReelsAndLEDs As Boolean = False)
        NoToolEvents = True
        If formToolReelsAndLEDs IsNot Nothing AndAlso Backglass.currentTabPage IsNot Nothing Then
            formToolReelsAndLEDs.ignoreChange = True
            If Backglass.currentData IsNot Nothing Then
                formToolReelsAndLEDs.cmbNumberOfPlayers.Text = Backglass.currentData.NumberOfPlayers.ToString
                formToolReelsAndLEDs.chkDream7.Checked = Backglass.currentData.UseDream7LEDs
            Else
                formToolReelsAndLEDs.cmbNumberOfPlayers.Text = ""
                formToolReelsAndLEDs.chkDream7.Checked = False
            End If
            If Backglass.currentTabPage IsNot Nothing AndAlso Backglass.currentTabPage.SelectedScore IsNot Nothing Then
                With Backglass.currentTabPage.SelectedScore
                    formToolReelsAndLEDs.txtID.Text = .ID
                    formToolReelsAndLEDs.numericDigits.Text = .Digits
                    formToolReelsAndLEDs.numericSpacing.Text = .Spacing
                    formToolReelsAndLEDs.cmbInitState.SelectedIndex = .DisplayState
                    formToolReelsAndLEDs.txtB2SStartID.Text = If(.B2SStartDigit = 0, "", .B2SStartDigit.ToString())
                    formToolReelsAndLEDs.cmbB2SScoreType.SelectedIndex = .B2SScoreType
                    formToolReelsAndLEDs.cmbB2SPlayerNo.SelectedIndex = .B2SPlayerNo
                End With
            Else
                formToolReelsAndLEDs.txtID.Text = ""
                formToolReelsAndLEDs.numericDigits.Text = ""
                formToolReelsAndLEDs.numericSpacing.Text = ""
                formToolReelsAndLEDs.cmbInitState.SelectedIndex = 0
                formToolReelsAndLEDs.txtB2SStartID.Text = ""
                formToolReelsAndLEDs.cmbB2SScoreType.SelectedIndex = 0
                formToolReelsAndLEDs.cmbB2SPlayerNo.SelectedIndex = 0
            End If
            If Backglass.currentData IsNot Nothing Then
                formToolReelsAndLEDs.txtB2SStartID.Enabled = (Backglass.currentData.CommType = eCommType.B2S)
                formToolReelsAndLEDs.cmbB2SScoreType.Enabled = (Backglass.currentData.CommType = eCommType.B2S)
                formToolReelsAndLEDs.cmbB2SPlayerNo.Enabled = (Backglass.currentData.CommType = eCommType.B2S)
            End If
            If refreshReelsAndLEDs Then formToolReelsAndLEDs.ReloadReels()
            formToolReelsAndLEDs.btnPerfectScaleWidthFix.Enabled = (Backglass.currentTabPage.SelectedScore IsNot Nothing)
            formToolReelsAndLEDs.btnChangeLEDColor.Enabled = (Backglass.currentTabPage.SelectedScore IsNot Nothing AndAlso IsReelImageRendered(Backglass.currentTabPage.SelectedScore.ReelType))
            formToolReelsAndLEDs.btnReelIllumination.Enabled = (Backglass.currentTabPage.SelectedScore IsNot Nothing AndAlso Not IsReelImageRendered(Backglass.currentTabPage.SelectedScore.ReelType))
            formToolReelsAndLEDs.ignoreChange = False
        End If
        NoToolEvents = False
    End Sub

    Private Sub CheckToolIlluminationForm()
        If formToolIllumination Is Nothing Then formToolIllumination = New formToolIllumination()
        LoadToolReelsAndLEDsForm()
    End Sub
    Private Sub ShowToolIlluminationForm()
        If Not formToolIllumination.Visible Then formToolIllumination.Show(Me)
    End Sub

    Public Sub OpenSelectedObjectSettings()
        ' A modal dialog runs its own message loop. If more than one legacy
        ' canvas Mouse helper has subscribed to MouseDoubleClick, the duplicate
        ' queued callback can otherwise open a second properties dialog while
        ' the first one is already visible.
        If objectSettingsDialogOpen Then Return
        If Backglass.currentTabPage Is Nothing OrElse Backglass.currentTabPage.SelectedBulb Is Nothing Then Return

        Dim selected As Illumination.BulbInfo = Backglass.currentTabPage.SelectedBulb
        objectSettingsDialogOpen = True
        Try
            If selected.IsImageSnippit AndAlso selected.IlluMode <> Illumination.eIlluMode.Flasher Then
                OpenSelectedSnippitProperties(selected)
                Return
            End If

            ' Normal lights and artwork flashers use the full Light Settings dialog.
            Dim selectedLights = Backglass.currentTabPage.Mouse.SelectedItems.OfType(Of Illumination.BulbInfo)().Where(Function(item) Not item.IsImageSnippit OrElse item.IlluMode = Illumination.eIlluMode.Flasher)
            Using editor As New formLightDiffusionEditor(selected, selectedLights)
                editor.ShowDialog(Me)
            End Using

            Backglass.currentTabPage.RefreshIllumination()
            Backglass.currentTabPage.Invalidate()
            LoadToolIlluminationForm()
        Finally
            objectSettingsDialogOpen = False
        End Try
    End Sub

    Private Sub OpenSelectedSnippitProperties(ByVal selected As Illumination.BulbInfo)
        If selected Is Nothing OrElse Backglass.currentTabPage Is Nothing Then Return

        Dim snippetName As String = selected.Name
        Dim snippetType As eSnippitType = selected.SnippitInfo.SnippitType
        Dim snippetBrightness As Integer = selected.SnippitInfo.Brightness
        Dim snippetBehindCanvas As Boolean = selected.SnippitInfo.BehindCanvas
        Dim zorder As Integer = selected.ZOrder
        Dim mechID As Integer = selected.SnippitInfo.SnippitMechID
        Dim rotatingSteps As Integer = selected.SnippitInfo.SnippitRotatingSteps
        Dim rotatingInterval As Integer = selected.SnippitInfo.SnippitRotatingInterval
        Dim rotatingDirection As eSnippitRotationDirection = selected.SnippitInfo.SnippitRotatingDirection
        Dim rotationStopping As eSnippitRotationStopBehaviour = selected.SnippitInfo.SnippitRotatingStopBehaviour
        Dim automaticRotationEnabled As Boolean = selected.SnippitInfo.AutomaticRotationEnabled
        Dim automaticRotationContinuous As Boolean = selected.SnippitInfo.AutomaticRotationContinuous
        Dim automaticRotationTriggerID As Integer = selected.SnippitInfo.AutomaticRotationTriggerID
        Dim automaticRotationTriggerType As eRomIDType = selected.SnippitInfo.AutomaticRotationTriggerType
        Dim automaticRotationSteps As Integer = selected.SnippitInfo.AutomaticRotationSteps
        Dim automaticRotationInterval As Integer = selected.SnippitInfo.AutomaticRotationInterval
        Dim automaticRotationDirection As eSnippitRotationDirection = selected.SnippitInfo.AutomaticRotationDirection
        Dim automaticRotationStopBehaviour As eSnippitRotationStopBehaviour = selected.SnippitInfo.AutomaticRotationStopBehaviour

        Using propertiesDialog As New formSnippitSettings()
            If propertiesDialog.ShowDialog(Me, selected.ID, snippetName, snippetType, zorder, snippetBrightness, snippetBehindCanvas, mechID,
                                           rotatingSteps, rotatingInterval, rotatingDirection,
                                           rotationStopping, automaticRotationEnabled,
                                           automaticRotationContinuous, automaticRotationTriggerID,
                                           automaticRotationTriggerType,
                                           automaticRotationSteps, automaticRotationInterval,
                                           automaticRotationDirection,
                                           automaticRotationStopBehaviour) <> DialogResult.OK Then Return
        End Using

        Backglass.currentTabPage.Illumination_SetName(snippetName)
        Backglass.currentTabPage.Illumination_SetZOrder(zorder)
        Backglass.currentTabPage.Illumination_SetSnippitInfo(New Illumination.SnippitInfo With {
            .Brightness = snippetBrightness,
            .BehindCanvas = snippetBehindCanvas,
            .SnippitType = snippetType,
            .SnippitMechID = mechID,
            .SnippitRotatingSteps = rotatingSteps,
            .SnippitRotatingInterval = rotatingInterval,
            .SnippitRotatingDirection = rotatingDirection,
            .SnippitRotatingStopBehaviour = rotationStopping,
            .AutomaticRotationEnabled = automaticRotationEnabled,
            .AutomaticRotationContinuous = automaticRotationContinuous,
            .AutomaticRotationTriggerID = automaticRotationTriggerID,
            .AutomaticRotationTriggerType = automaticRotationTriggerType,
            .AutomaticRotationSteps = automaticRotationSteps,
            .AutomaticRotationInterval = automaticRotationInterval,
            .AutomaticRotationDirection = automaticRotationDirection,
            .AutomaticRotationStopBehaviour = automaticRotationStopBehaviour
        })

        Backglass.currentTabPage.RefreshIllumination()
        Backglass.currentTabPage.Invalidate()
        If formToolIllumination IsNot Nothing Then LoadToolIlluminationForm()
        If formToolLayers IsNot Nothing AndAlso Not formToolLayers.IsDisposed Then formToolLayers.RefreshLayers()
    End Sub

    Public Sub OpenSelectedIlluminationSettings(ByVal openSnippitDialog As Boolean)
        CheckToolIlluminationForm()
        LoadToolIlluminationForm()
        ShowToolIlluminationForm()

        If formToolIllumination.WindowState = FormWindowState.Minimized Then
            formToolIllumination.WindowState = FormWindowState.Normal
        End If
        formToolIllumination.BringToFront()
        formToolIllumination.Activate()

        If openSnippitDialog AndAlso formToolIllumination.btnSnippitSettings.Enabled Then
            formToolIllumination.BeginInvoke(New MethodInvoker(Sub() formToolIllumination.btnSnippitSettings.PerformClick()))
        End If
    End Sub
    Private Sub LoadToolIlluminationForm()
        NoToolEvents = True
        If formToolIllumination IsNot Nothing Then
            If Backglass.currentTabPage IsNot Nothing AndAlso Backglass.currentTabPage.SelectedBulb IsNot Nothing Then
                With Backglass.currentTabPage.SelectedBulb
                    formToolIllumination.txtName.Text = .Name
                    formToolIllumination.txtID.Text = .ID.ToString()
                    formToolIllumination.txtB2SID.Text = If(.B2SID = 0, "", .B2SID.ToString())
                    formToolIllumination.cmbB2SIDType.SelectedIndex = .B2SIDType
                    formToolIllumination.txtB2SValue.Text = If(.B2SValue = 0, "", .B2SValue.ToString())
                    formToolIllumination.txtRomID.Text = If(.RomID = 0, "", .RomID.ToString())
                    formToolIllumination.cmbROMIDType.SelectedIndex = .RomIDType
                    formToolIllumination.chkRomInverted.Checked = .RomInverted
                    formToolIllumination.cmbInitState.SelectedIndex = .InitialState
                    If Backglass.currentData.DualBackglass Then
                        formToolIllumination.cmbDualMode.SelectedIndex = .DualMode
                    Else
                        formToolIllumination.cmbDualMode.Text = String.Empty
                    End If
                    formToolIllumination.TrackBarIntensity.Maximum = If(.IlluMode = Illumination.eIlluMode.Flasher, 20, 5)
                    formToolIllumination.TrackBarIntensity.TickFrequency = If(.IlluMode = Illumination.eIlluMode.Flasher, 2, 1)
                    formToolIllumination.TrackBarIntensity.Value = Math.Max(formToolIllumination.TrackBarIntensity.Minimum, Math.Min(formToolIllumination.TrackBarIntensity.Maximum, .Intensity))
                    formToolIllumination.LoadGlowControls(Backglass.currentTabPage.SelectedBulb)
                    formToolIllumination.btnLightColor.BackColor = .LightColor
                    formToolIllumination.cmbDodgeColor.SelectedIndex = TranslateDodgeColor2Index(.DodgeColor)
                    ' The old lamp-to-flasher conversion option was removed from
                    ' this panel. Dedicated flashers retain their own setup dialog.
                    formToolIllumination.cmbIlluMode.SelectedIndex = 0
                    formToolIllumination.txtIlluminationText.Text = .Text
                    If .TextAlignment = Illumination.eTextAlignment.Left Then
                        formToolIllumination.rbAlignLeft.Checked = True
                    ElseIf .TextAlignment = Illumination.eTextAlignment.Right Then
                        formToolIllumination.rbAlignRight.Checked = True
                    Else
                        formToolIllumination.rbAlignCenter.Checked = True
                    End If
                    formToolIllumination.MyFont = If(String.IsNullOrEmpty(.FontName), Nothing, New Font(.FontName, .FontSize, .FontStyle))

                    formToolIllumination.ignoreChange = True
                    formToolIllumination.txtLocationX.Text = .Location.X.ToString()
                    formToolIllumination.txtLocationY.Text = .Location.Y.ToString()
                    formToolIllumination.txtSizeWidth.Text = .Size.Width.ToString()
                    formToolIllumination.txtSizeHeight.Text = .Size.Height.ToString()
                    formToolIllumination.ignoreChange = False

                    formToolIllumination.ZOrder = .ZOrder
                    formToolIllumination.IsSnippit = .IsImageSnippit
                    formToolIllumination.SnippitType = .SnippitInfo.SnippitType
                    formToolIllumination.SnippitBrightness = .SnippitInfo.Brightness
                    formToolIllumination.SnippitBehindCanvas = .SnippitInfo.BehindCanvas
                    formToolIllumination.SnippitMechID = .SnippitInfo.SnippitMechID
                    formToolIllumination.SnippitRotatingSteps = .SnippitInfo.SnippitRotatingSteps
                    formToolIllumination.SnippitRotatingInterval = .SnippitInfo.SnippitRotatingInterval
                    formToolIllumination.SnippitRotatingDirection = .SnippitInfo.SnippitRotatingDirection
                    formToolIllumination.SnippitRotatingStopBehaviour = .SnippitInfo.SnippitRotatingStopBehaviour
                    formToolIllumination.AutomaticRotationEnabled = .SnippitInfo.AutomaticRotationEnabled
                    formToolIllumination.AutomaticRotationContinuous = .SnippitInfo.AutomaticRotationContinuous
                    formToolIllumination.AutomaticRotationTriggerID = .SnippitInfo.AutomaticRotationTriggerID
                    formToolIllumination.AutomaticRotationTriggerType = .SnippitInfo.AutomaticRotationTriggerType
                    formToolIllumination.AutomaticRotationSteps = .SnippitInfo.AutomaticRotationSteps
                    formToolIllumination.AutomaticRotationInterval = .SnippitInfo.AutomaticRotationInterval
                    formToolIllumination.AutomaticRotationDirection = .SnippitInfo.AutomaticRotationDirection
                    formToolIllumination.AutomaticRotationStopBehaviour = .SnippitInfo.AutomaticRotationStopBehaviour
                End With
            Else
                formToolIllumination.txtName.Text = ""
                formToolIllumination.txtID.Text = ""
                formToolIllumination.txtB2SID.Text = ""
                formToolIllumination.cmbB2SIDType.SelectedIndex = 0
                formToolIllumination.txtB2SValue.Text = ""
                formToolIllumination.txtRomID.Text = ""
                formToolIllumination.cmbROMIDType.SelectedIndex = 0
                formToolIllumination.chkRomInverted.Checked = False
                formToolIllumination.cmbInitState.SelectedIndex = 0
                formToolIllumination.cmbDualMode.Text = String.Empty
                formToolIllumination.TrackBarIntensity.Value = 1
                formToolIllumination.LoadGlowControls(Nothing)
                formToolIllumination.btnLightColor.BackColor = DefaultLightColor
                formToolIllumination.cmbDodgeColor.SelectedIndex = 0
                formToolIllumination.cmbIlluMode.SelectedIndex = 0
                formToolIllumination.txtIlluminationText.Text = ""
                formToolIllumination.rbAlignCenter.Checked = True
                formToolIllumination.MyFont = Nothing
                formToolIllumination.ZOrder = 0
                formToolIllumination.IsSnippit = False

                formToolIllumination.ignoreChange = True
                formToolIllumination.txtLocationX.Text = ""
                formToolIllumination.txtLocationY.Text = ""
                formToolIllumination.txtSizeWidth.Text = ""
                formToolIllumination.txtSizeHeight.Text = ""
                formToolIllumination.ignoreChange = False
            End If
            ' set headlines and lock/unlock some fields
            If Backglass.currentData IsNot Nothing Then
                formToolIllumination.cmbDualMode.Enabled = (Backglass.currentData.DualBackglass AndAlso formToolIllumination.cmbInitState.SelectedIndex <> 2)
                formToolIllumination.lblB2SID.Visible = (Backglass.currentData.CommType = eCommType.B2S)
                formToolIllumination.txtB2SID.Visible = (Backglass.currentData.CommType = eCommType.B2S)
                formToolIllumination.lblB2SIDType.Visible = (Backglass.currentData.CommType = eCommType.B2S)
                formToolIllumination.cmbB2SIDType.Visible = (Backglass.currentData.CommType = eCommType.B2S)
                formToolIllumination.lblB2SValue.Visible = (Backglass.currentData.CommType = eCommType.B2S)
                formToolIllumination.txtB2SValue.Visible = (Backglass.currentData.CommType = eCommType.B2S)
                formToolIllumination.lblRomID.Visible = (Backglass.currentData.CommType = eCommType.Rom)
                formToolIllumination.txtRomID.Visible = (Backglass.currentData.CommType = eCommType.Rom)
                formToolIllumination.lblRomIDType.Visible = (Backglass.currentData.CommType = eCommType.Rom)
                formToolIllumination.cmbROMIDType.Visible = (Backglass.currentData.CommType = eCommType.Rom)
                formToolIllumination.chkRomInverted.Visible = (Backglass.currentData.CommType = eCommType.Rom)
            End If
        End If
        NoToolEvents = False
    End Sub

    Private Sub CheckToolUndoForm()
        If formToolUndo Is Nothing Then
            formToolUndo = New formToolUndo()
            Undo.ListBox = formToolUndo.lbHistory
            For Each undoentry As Undo.UndoEntry In Undo.UndoList
                formToolUndo.lbHistory.Items.Add(undoentry)
            Next
        End If
        LoadToolUndoForm()
    End Sub
    Private Sub ShowToolUndoForm()
        If Not formToolUndo.Visible Then formToolUndo.Show(Me)
    End Sub
    Private Sub LoadToolUndoForm()
        NoToolEvents = True
        If formToolUndo IsNot Nothing Then

        End If
        NoToolEvents = False
    End Sub

    Private Sub CheckToolResourcesForm()
        If formToolResources Is Nothing Then
            formToolResources = New formToolResources()
        End If
        LoadToolResourcesForm()
    End Sub
    Private Sub ShowToolResourcesForm()
        If Not formToolResources.Visible Then formToolResources.Show(Me)
    End Sub
    Private Sub LoadToolResourcesForm()
        NoToolEvents = True
        If formToolResources IsNot Nothing Then
            If Backglass.currentData IsNot Nothing Then
                formToolResources.ImageInfoList = Backglass.currentData.Images
            Else
                formToolResources.ImageInfoList = Nothing
                formToolResources.cmbImageType.SelectedIndex = 0
                formToolResources.txtRomID.Text = String.Empty
                formToolResources.cmbROMIDType.SelectedIndex = 0
            End If
        End If
        NoToolEvents = False
    End Sub

    Public Sub RefreshImageInfoList()
        If formToolResources IsNot Nothing Then
            If Backglass.currentData IsNot Nothing Then
                formToolResources.ImageInfoList = Backglass.currentData.Images
                Backglass.currentTabPage.Invalidate()
                Backglass.currentData.IsDirty = True
            End If
        End If
    End Sub

    Private Sub CopyDMDArea()
        If Backglass.currentData IsNot Nothing Then
            With Backglass.currentData
                ' get location of rectangle
                .DMDDefaultLocation = .DMDCopyArea.Location
                ' get image
                Dim image As Image = .Image.PartFromImage(New Rectangle(.DMDCopyArea.Location, .DMDCopyArea.Size))
                Backglass.currentTabPage.DMDImage() = image
                tscmbImage.SelectedIndex = 1
                Backglass.currentData.Images.Insert(Images.eImageInfoType.Title4DMDImages, New Images.ImageInfo(Images.eImageInfoType.DMDImage, "", image))
                Backglass.currentData.IsSavedDMDImageDirty = True
                LoadToolResourcesForm()
            End With
        End If
    End Sub

    Private Sub LoadData(ByVal _backglassdata As Backglass.Data, Optional ByVal sourceFileName As String = "")
        If _backglassdata IsNot Nothing Then
            Dim normalizedSourcePath As String = NormalizeSourcePath(sourceFileName)
            _backglassdata.SourceFilePath = normalizedSourcePath
            Dim loadData As Boolean = True
            Dim i As Integer = 0
            ' check whether the table is already loaded
            For Each backglass As B2STabPage In B2STab.TabPages
                Dim existingSourcePath As String = NormalizeSourcePath(backglass.BackglassData.SourceFilePath)
                Dim isSameDocument As Boolean
                If normalizedSourcePath.Length > 0 AndAlso existingSourcePath.Length > 0 Then
                    isSameDocument = String.Equals(existingSourcePath, normalizedSourcePath, StringComparison.OrdinalIgnoreCase)
                    If Not isSameDocument AndAlso
                       String.IsNullOrEmpty(backglass.BackglassData.BackupName) AndAlso
                       String.IsNullOrEmpty(_backglassdata.BackupName) AndAlso
                       Not String.IsNullOrWhiteSpace(backglass.BackglassData.ProjectGUID) AndAlso
                       String.Equals(backglass.BackglassData.ProjectGUID, _backglassdata.ProjectGUID, StringComparison.OrdinalIgnoreCase) AndAlso
                       String.Equals(backglass.BackglassData.Name, _backglassdata.Name, StringComparison.OrdinalIgnoreCase) Then
                         ' A legacy project and its exported backglass can have
                         ' different paths but still represent one logical document.
                        isSameDocument = True
                    End If
                Else
                    ' Unsaved/new projects have no source path, so preserve the
                    ' original name-and-backup duplicate behavior for those tabs.
                    isSameDocument = backglass.BackglassData.Name = _backglassdata.Name AndAlso
                                     backglass.BackglassData.BackupName = _backglassdata.BackupName
                End If
                If isSameDocument Then
                    loadData = False
                    B2STab.SelectedIndex = i
                    Exit For
                End If
                i += 1
            Next
            ' load data
            If loadData Then
                ' Capture the saved value before constructing the tab. A new
                ' Backglass.Data starts at 100%, and UpdateStatusBar runs during
                ' tab construction. Without this guard that temporary 100% value
                ' overwrites the user's saved zoom before it can be restored.
                Dim targetZoom As String = RestoredCanvasZoom()
                Dim preferenceWasReady As Boolean = canvasZoomPreferenceReady
                canvasZoomPreferenceReady = False
                Try
                    B2STab.AddBackglass(New B2STabPage(_backglassdata))
                    B2STab.SelectedIndex = B2STab.TabPages.Count - 1
                    UpdateSameNameTabLabels(_backglassdata.Name, _backglassdata.BackupName)
                    Backglass.currentData.IsDirty = False
                    B2STab.SelectedTabPage.Zoom(targetZoom)
                Finally
                    canvasZoomPreferenceReady = preferenceWasReady
                End Try
            End If
            If Not String.IsNullOrEmpty(sourceFileName) Then recent.AddToRecentList(_backglassdata, sourceFileName)
        End If
    End Sub

    Private Sub TraceMotionPersistence(ByVal message As String)
        Debug.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") & " | " & message)
    End Sub

    Private Sub IDTester_Click(sender As Object, e As EventArgs)
        If Backglass.currentData Is Nothing OrElse Backglass.currentTabPage Is Nothing Then
            MessageBox.Show(Me, "Open a backglass first so the ID tester can find its table.", "ID Tester", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim tableName As String = If(String.IsNullOrWhiteSpace(Backglass.currentData.VSName),
                                     Backglass.currentData.Name, Backglass.currentData.VSName)
        IDTester.Run(Me, tableName, DefaultVPTablesFolder)
    End Sub

    Private Function NormalizeSourcePath(ByVal sourceFileName As String) As String
        If String.IsNullOrWhiteSpace(sourceFileName) Then Return String.Empty
        Try
            Return IO.Path.GetFullPath(sourceFileName)
        Catch ex As Exception
            Return sourceFileName.Trim()
        End Try
    End Function

    Private Sub UpdateSameNameTabLabels(ByVal backglassName As String, ByVal backupName As String)
        Dim matchingTabs As New List(Of B2STabPage)()
        For Each tabpage As B2STabPage In B2STab.TabPages
            If tabpage.BackglassData.Name = backglassName AndAlso tabpage.BackglassData.BackupName = backupName Then
                matchingTabs.Add(tabpage)
            End If
        Next
        If matchingTabs.Count < 2 Then Return

        For Each tabpage As B2STabPage In matchingTabs
            Dim label As String = backglassName
            Dim sourcePath As String = tabpage.BackglassData.SourceFilePath
            If sourcePath.Length > 0 Then
                Dim parent As IO.DirectoryInfo = IO.Directory.GetParent(sourcePath)
                If parent IsNot Nothing Then label &= " [" & parent.Name & "]"
            End If
            If tabpage.BackglassData.IsBackup Then label &= " (Backup: '" & backupName & "')"
            tabpage.Text = label
        Next
        B2STab.Invalidate()
    End Sub

    Private Sub ShowStatus(Optional ByVal text As String = "")
        If Not String.IsNullOrEmpty(text) Then
            tsLabelStatusInfo.Text = text
        Else
            If String.IsNullOrEmpty(tsLabelFileInfo.Text) Then
                tsLabelStatusInfo.Text = My.Resources.STATUS_Start
            Else
                tsLabelStatusInfo.Text = My.Resources.STATUS_Default
            End If
        End If
    End Sub

    Private lastProgressPaintTick As Integer = Environment.TickCount
    Private Const ProgressPaintIntervalMs As Integer = 33

    Private Sub ShowProgress(ByVal value As Integer)
        tsProgress.Value = value
        If Not tsProgress.Visible OrElse (value = 0 AndAlso Not timerProgressReset.Enabled) Then
            tsProgress.Visible = True
        End If

        ' Progress can be reported many times inside image/save/export loops. A full
        ' synchronous ToolStrip Refresh for every value stalls the operation itself.
        ' Let Windows merge intermediate paints and force a paint at most ~30 FPS,
        ' while always showing the start and completed states immediately.
        Dim progressHost As ToolStrip = tsProgress.Owner
        If progressHost Is Nothing Then progressHost = tsB2SDesigner
        progressHost.Invalidate()
        Dim nowTick As Integer = Environment.TickCount
        If value = 0 OrElse value = 100 OrElse
           CUInt(nowTick - lastProgressPaintTick) >= ProgressPaintIntervalMs Then
            lastProgressPaintTick = nowTick
            progressHost.Update()
        End If

        If timerProgressReset.Enabled Then
            timerProgressReset.Stop()
        End If
        If value = 100 Then
            timerProgressReset.Start()
        End If
    End Sub

#End Region


End Class
