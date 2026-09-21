'=== Phase 2 Sprint 1 ===
Imports System
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.Windows.Forms

Public Class formLightDiffusionEditor
    Inherits B2SThemedForm

    Private ReadOnly bulb As Illumination.BulbInfo
    Private ReadOnly selectedBulbs As New List(Of Illumination.BulbInfo)()
    Private ReadOnly additionalOriginals As New Dictionary(Of Illumination.BulbInfo, LightSettingsSnapshot)()
    Private ReadOnly spread As NumericUpDown
    Private ReadOnly softness As TrackBar
    Private ReadOnly intensity As TrackBar
    Private ReadOnly lightDiffusion As TrackBar
    Private ReadOnly lightTemperature As TrackBar
    Private ReadOnly falloff As TrackBar
    Private ReadOnly transmissionContrast As TrackBar
    Private ReadOnly saveTheseSettings As CheckBox
    Private flasherStyle As ComboBox
    Private flasherSaturation As TrackBar
    Private flasherHighlightProtection As TrackBar
    Private flasherDarkAreaLift As TrackBar
    Private flasherHotspotX As NumericUpDown
    Private flasherHotspotY As NumericUpDown
    Private maskRadius As TrackBar
    Private maskSmartRadius As CheckBox
    Private maskSmooth As TrackBar
    Private maskFeather As TrackBar
    Private maskContrast As TrackBar
    Private maskShiftEdge As TrackBar
    Private flasherRadialSpikes As TrackBar
    Private adjustmentPasses As NumericUpDown
    Private flasherMaskStatus As Label
    Private flasherPreviewGroup As GroupBox
    Private flasherMaskGroup As GroupBox
    Private flasherOkButton As Button
    Private flasherCloseButton As Button
    Private flasherTitleLabel As Label
    Private flasherDescriptionLabel As Label
    Private flasherSettingsGroup As GroupBox
    Private flasherPreviewModeGroup As GroupBox
    Private flasherQuickSelectButton As Button
    Private flasherCopyButton As Button
    Private flasherPasteButton As Button
    Private flasherResetButton As Button
    Private flasherLayoutRoot As TableLayoutPanel
    Private flasherLeftPanel As Panel
    Private flasherRightLayout As TableLayoutPanel
    Private flasherActionPanel As FlowLayoutPanel

    Private ReadOnly originalSpread As Integer
    Private ReadOnly originalSoftness As Integer
    Private ReadOnly originalIntensity As Integer
    Private ReadOnly originalFalloff As Integer
    Private ReadOnly originalLightDiffusion As Integer
    Private ReadOnly originalLightTemperature As Integer
    Private ReadOnly originalLightPurpose As Illumination.eLightPurpose
    Private ReadOnly originalArtworkPixelLighting As Boolean
    Private ReadOnly originalBlendMode As Integer
    Private ReadOnly originalPreviewQuality As Integer
    Private ReadOnly originalInFrontOfMask As Boolean
    Private ReadOnly originalGlobalMaskLayerExplicit As Boolean
    Private ReadOnly originalLightColor As Color
    Private ReadOnly originalSelectionMask As String
    Private ReadOnly originalSelectionTolerance As Integer
    Private ReadOnly originalSelectionFeather As Integer
    Private ReadOnly originalFlasherStyle As Integer
    Private ReadOnly originalFlasherSaturation As Integer
    Private ReadOnly originalFlasherHighlightProtection As Integer
    Private ReadOnly originalFlasherDarkAreaLift As Integer
    Private ReadOnly originalFlasherHotspotX As Integer
    Private ReadOnly originalFlasherHotspotY As Integer
    Private ReadOnly originalFlasherPulseDuration As Integer
    Private ReadOnly originalIsImageSnippit As Boolean
    Private ReadOnly originalImage As Image
    Private ReadOnly originalMaskRadius As Integer
    Private ReadOnly originalMaskSmartRadius As Boolean
    Private ReadOnly originalMaskSmooth As Integer
    Private ReadOnly originalMaskFeather As Integer
    Private ReadOnly originalMaskContrast As Integer
    Private ReadOnly originalMaskShiftEdge As Integer
    Private ReadOnly originalFlasherRadialSpikes As Integer
    Private ReadOnly originalArtworkBrightness As Integer
    Private ReadOnly originalArtworkContrast As Integer
    Private ReadOnly originalArtworkAdjustmentPasses As Integer
    Private generateFlasherButton As Button
    Private livePreviewOriginal As PictureBox
    Private livePreviewFlashed As PictureBox
    Private livePreviewStatus As Label
    Private livePreviewTimer As Timer
    Private livePreviewPending As Boolean
    Private canvasPreviewPending As Boolean
    Private previewMode As ComboBox
    Private blinkTimer As Timer
    Private blinkShowingFlashed As Boolean = True
    Private blinkPreviewStartedAt As Long = Diagnostics.Stopwatch.GetTimestamp()
    Private blinkPreviewSignature As String = Nothing
    Private flasherPulseEnabled As CheckBox
    Private flasherPulseDuration As NumericUpDown
    Private flasherPulsePreviewEndsAt As Long = 0
    Private flasherPulsePreviewNextAt As Long = 0
    Private flasherPulsePreviewRepeating As Boolean = False
    Private testPulseButton As Button
    Private Const FlasherTestPulseDurationMilliseconds As Integer = 75
    Private previewIndependentBlinkSource As Bitmap
    Private blinkerEnabled As CheckBox
    Private blinkerInterval As NumericUpDown
    Private isUpdatingLivePreview As Boolean = False
    Private isInitializing As Boolean = True
    Private flashColorButton As Button
    Private previewOriginalSource As Bitmap
    Private previewFlashedSource As Bitmap
    Private previewZoomPercent As Integer = 0 ' 0 = fit to window
    Private previewZoomLabel As Label
    Private previewPanOffset As Point = Point.Empty
    Private previewIsPanning As Boolean = False
    Private previewPanStart As Point
    Private previewPanStartOffset As Point
    Private lightPurposeFlasher As CheckBox
    Private currentProfileIsFlasher As Boolean
    Private ReadOnly originalBlinkEnabled As Boolean
    Private ReadOnly originalBlinkInterval As Integer
    Private ReadOnly isDedicatedFlasher As Boolean

    ' Light and flasher slider profiles are now stored independently and persistently.

    Public Event PreviewChanged(ByVal sender As Object, ByVal e As EventArgs)

    Public Sub New(ByVal selectedBulb As Illumination.BulbInfo, Optional ByVal multiSelection As IEnumerable(Of Illumination.BulbInfo) = Nothing)
        bulb = selectedBulb
        isDedicatedFlasher = (bulb.IlluMode = Illumination.eIlluMode.Flasher OrElse
                              bulb.LightPurpose = Illumination.eLightPurpose.Flasher)
        If multiSelection IsNot Nothing Then
            For Each selected As Illumination.BulbInfo In multiSelection
                If selected IsNot Nothing AndAlso Not selectedBulbs.Contains(selected) Then selectedBulbs.Add(selected)
            Next
        End If
        If Not selectedBulbs.Contains(bulb) Then selectedBulbs.Add(bulb)
        For Each selected As Illumination.BulbInfo In selectedBulbs
            If Not Object.ReferenceEquals(selected, bulb) Then additionalOriginals(selected) = New LightSettingsSnapshot(selected)
        Next
        originalSpread = bulb.GlowSpread
        originalSoftness = bulb.GlowSoftness
        originalIntensity = bulb.GlowIntensity
        originalFalloff = bulb.GlowFalloff
        originalLightDiffusion = bulb.LightDiffusion
        originalLightTemperature = bulb.LightTemperature
        originalLightPurpose = bulb.LightPurpose
        originalArtworkPixelLighting = bulb.ArtworkPixelLighting
        originalBlendMode = bulb.GlowBlendMode
        originalPreviewQuality = bulb.GlowPreviewQuality
        originalInFrontOfMask = bulb.InFrontOfGlobalMask
        originalGlobalMaskLayerExplicit = bulb.GlobalMaskLayerExplicit
        originalLightColor = bulb.LightColor
        originalSelectionMask = bulb.SelectionMaskData
        originalSelectionTolerance = bulb.SelectionTolerance
        originalSelectionFeather = bulb.SelectionFeather
        originalFlasherStyle = bulb.FlasherStyle
        originalFlasherSaturation = bulb.FlasherSaturation
        originalFlasherHighlightProtection = bulb.FlasherHighlightProtection
        originalFlasherDarkAreaLift = bulb.FlasherDarkAreaLift
        originalFlasherHotspotX = bulb.FlasherHotspotX
        originalFlasherHotspotY = bulb.FlasherHotspotY
        originalFlasherPulseDuration = bulb.FlasherPulseDuration
        originalIsImageSnippit = bulb.IsImageSnippit
        originalImage = If(bulb.Image IsNot Nothing, New Bitmap(bulb.Image), Nothing)
        originalMaskRadius = bulb.MaskRadius
        originalMaskSmartRadius = bulb.MaskSmartRadius
        originalMaskSmooth = bulb.MaskSmooth
        originalMaskFeather = bulb.MaskFeather
        originalMaskContrast = bulb.MaskContrast
        originalMaskShiftEdge = bulb.MaskShiftEdge
        originalFlasherRadialSpikes = bulb.FlasherRadialSpikes
        originalArtworkBrightness = bulb.ArtworkBrightness
        originalArtworkContrast = bulb.ArtworkContrast
        originalArtworkAdjustmentPasses = bulb.ArtworkAdjustmentPasses
        originalBlinkEnabled = bulb.BlinkEnabled
        originalBlinkInterval = bulb.BlinkInterval

        ' Opening the new Light editor previews the same artwork-pixel pipeline
        ' as the Flasher editor. Cancel restores legacy lights unchanged; saving
        ' makes the upgrade explicit for only the selected regular light(s).
        For Each selected As Illumination.BulbInfo In selectedBulbs
            If selected IsNot Nothing AndAlso Not selected.IsImageSnippit AndAlso
               selected.IlluMode <> Illumination.eIlluMode.Flasher AndAlso
               selected.LightPurpose <> Illumination.eLightPurpose.Flasher Then
                selected.ArtworkPixelLighting = True
                selected.IsIlluminatedImageDirty = True
            End If
        Next

        ' Opening Light Settings must reflect the values already stored on the
        ' selected light.  Do not recall the last-used profile here: doing so
        ' changed existing projects before the user touched a control.
        currentProfileIsFlasher = Illumination.LightGlowDefaults.IsFlasherProfile(bulb)

        WindowStateManager.Attach(Me)
        StartPosition = FormStartPosition.CenterParent
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        Font = New Font("Tahoma", 9.0F)

        ' The application routes image snippets to their own settings dialog.
        ' This editor therefore has one shared current layout for lights and flashers.
            Dim initialSpread As Integer = bulb.GlowSpread
            Dim initialSoftness As Integer = bulb.GlowSoftness
            Dim initialDiffusion As Integer = bulb.GlowFalloff
            Dim initialIntensity As Integer = bulb.GlowIntensity
            Dim initialTransmissionContrast As Integer = If(bulb.ArtworkContrast <= 0, 140, bulb.ArtworkContrast)
            Dim initialFlashColor As Color = bulb.LightColor

            Text = If(isDedicatedFlasher, "Flasher", "Light")
            Name = If(isDedicatedFlasher, "formFlasherEditor", "formLightEditor")
            ' Keep the complete dialog inside a typical 1080p working area.  The
            ' previous action row started below the client area and was clipped.
            ClientSize = New Size(1380, 800)
            MinimumSize = New Size(1260, 780)
            MaximumSize = Size.Empty
            FormBorderStyle = FormBorderStyle.Sizable
            MaximizeBox = True
            DoubleBuffered = True
            BackColor = Color.FromArgb(18, 20, 22)
            ForeColor = Color.WhiteSmoke

            Dim accent As Color = Color.FromArgb(255, 153, 0)
            Dim panelColor As Color = Color.FromArgb(27, 29, 31)

            flasherTitleLabel = New Label With {
                .Text = If(isDedicatedFlasher, "⚡  Flasher", "☀  Light"),
                .Left = 24, .Top = 18, .Width = 430, .Height = 42,
                .Font = New Font("Tahoma", 20.0F, FontStyle.Bold),
                .ForeColor = accent
            }
            Controls.Add(flasherTitleLabel)
            flasherDescriptionLabel = New Label With {
                .Text = "The placed " & If(isDedicatedFlasher, "flasher", "light") & " box defines the light area. Every slider updates the preview live.",
                .Left = 26, .Top = 58, .Width = 530, .Height = 26,
                .ForeColor = Color.Gainsboro
            }
            Controls.Add(flasherDescriptionLabel)

            flasherSettingsGroup = New GroupBox With {
                .Text = If(isDedicatedFlasher, "  FLASHER SETTINGS", "  LIGHT SETTINGS"),
                .Left = 20, .Top = 94, .Width = 555, .Height = 505,
                .ForeColor = accent, .BackColor = panelColor,
                .Font = New Font("Tahoma", 9.0F, FontStyle.Bold)
            }
            Controls.Add(flasherSettingsGroup)

            flasherSettingsGroup.Controls.Add(New Label With {.Text = "Glow Radius", .Left = 18, .Top = 38, .Width = 145, .ForeColor = Color.WhiteSmoke, .Font = New Font("Tahoma", 9.0F)})
            spread = New NumericUpDown With {
                .Left = 330, .Top = 31, .Width = 92, .Minimum = 0, .Maximum = 1000,
                .Value = Math.Max(0, Math.Min(1000, initialSpread)),
                .BackColor = Color.FromArgb(38, 40, 43), .ForeColor = Color.WhiteSmoke
            }
            flasherSettingsGroup.Controls.Add(spread)

            softness = AddArtworkSlider(flasherSettingsGroup, "Edge Softness", 57, initialSoftness, 0, 300, "%")
            falloff = AddArtworkSlider(flasherSettingsGroup, "Outward Diffusion", 97, initialDiffusion, 0, 300, "%")
            transmissionContrast = AddArtworkSlider(flasherSettingsGroup, "Transmission Contrast", 137,
                                                     initialTransmissionContrast, 0, 300, "%")
            intensity = AddArtworkSlider(flasherSettingsGroup, "Brightness", 177, initialIntensity, 0, 1600, "%")
            lightTemperature = AddArtworkKelvinSlider(flasherSettingsGroup, "Light Temperature", 217, bulb.LightTemperature)

            flasherSettingsGroup.Controls.Add(New Label With {
                .Text = "Brightness range: 0% – 1600%   •   800% is the strong midpoint", .Left = 18, .Top = 260, .Width = 430,
                .ForeColor = Color.Silver, .Font = New Font("Tahoma", 8.25F)
            })

            flasherPreviewModeGroup = New GroupBox With {
                .Text = If(isDedicatedFlasher, "  PREVIEW MODE", "  BLINKER RUNTIME"),
                .Left = 20, .Top = 610, .Width = 555, .Height = If(isDedicatedFlasher, 76, 92),
                .ForeColor = accent, .BackColor = panelColor,
                .Font = New Font("Tahoma", 9.0F, FontStyle.Bold)
            }
            Controls.Add(flasherPreviewModeGroup)

            If isDedicatedFlasher Then
                Dim flashedButton As New Button With {.Text = "☀  Live Flash", .Left = 18, .Top = 27, .Width = 126, .Height = 36, .FlatStyle = FlatStyle.Flat}
                testPulseButton = New Button With {.Text = "⚡  Test Pulse", .Left = 164, .Top = 27, .Width = 126, .Height = 36, .FlatStyle = FlatStyle.Flat, .ForeColor = accent}
                StyleDarkButton(flashedButton)
                StyleDarkButton(testPulseButton)
                AddHandler flashedButton.Click, Sub(sender As Object, e As EventArgs)
                                                     StopRepeatingFlasherPulse(False)
                                                     previewMode.SelectedIndex = 0
                                                     RefreshPreviewImages()
                                                 End Sub
                AddHandler testPulseButton.Click, AddressOf TestFlasherPulsePreview
                flasherPreviewModeGroup.Controls.AddRange(New Control() {flashedButton, testPulseButton})
            Else
                blinkerEnabled = New CheckBox With {
                    .Name = "chkBlinkerLight", .Text = "Enable blinker", .Left = 18, .Top = 20,
                    .Width = 125, .Height = 24, .Checked = bulb.BlinkEnabled,
                    .ForeColor = Color.WhiteSmoke, .Font = New Font("Tahoma", 9.0F)
                }
                Dim intervalLabel As New Label With {
                    .Text = "Interval:", .Left = 150, .Top = 23, .Width = 58, .Height = 22,
                    .ForeColor = Color.WhiteSmoke, .Font = New Font("Tahoma", 9.0F)
                }
                blinkerInterval = New NumericUpDown With {
                    .Name = "numericBlinkerInterval", .Left = 208, .Top = 19, .Width = 90,
                    .Minimum = 1D, .Maximum = 60000D, .Increment = 1D,
                    .ThousandsSeparator = True, .Value = Math.Max(1D, Math.Min(60000D, bulb.BlinkInterval)),
                    .Enabled = bulb.BlinkEnabled,
                    .BackColor = Color.FromArgb(38, 40, 43), .ForeColor = Color.WhiteSmoke,
                    .Font = New Font("Tahoma", 9.0F)
                }
                Dim millisecondsLabel As New Label With {
                    .Text = "ms", .Left = 304, .Top = 23, .Width = 30, .Height = 22,
                    .Enabled = bulb.BlinkEnabled, .ForeColor = Color.WhiteSmoke,
                    .Font = New Font("Tahoma", 9.0F)
                }
                intervalLabel.Enabled = bulb.BlinkEnabled
                Dim flashedButton As New Button With {.Text = "Live Light", .Left = 18, .Top = 51, .Width = 150, .Height = 28, .FlatStyle = FlatStyle.Flat}
                Dim blinkButton As New Button With {.Text = "Blink Preview", .Left = 178, .Top = 51, .Width = 150, .Height = 28, .FlatStyle = FlatStyle.Flat}
                StyleDarkButton(flashedButton)
                StyleDarkButton(blinkButton)
                AddHandler flashedButton.Click, Sub(sender As Object, e As EventArgs) previewMode.SelectedIndex = 0
                AddHandler blinkButton.Click, Sub(sender As Object, e As EventArgs) previewMode.SelectedIndex = 1
                AddHandler blinkerEnabled.CheckedChanged, Sub(sender As Object, e As EventArgs)
                                                               blinkerInterval.Enabled = blinkerEnabled.Checked
                                                               intervalLabel.Enabled = blinkerEnabled.Checked
                                                               millisecondsLabel.Enabled = blinkerEnabled.Checked
                                                               BlinkerControlChanged(sender, e)
                                                           End Sub
                AddHandler blinkerInterval.ValueChanged, AddressOf BlinkerControlChanged
                flasherPreviewModeGroup.Controls.AddRange(New Control() {blinkerEnabled, intervalLabel, blinkerInterval,
                                                                         millisecondsLabel, flashedButton, blinkButton})
            End If

            If isDedicatedFlasher Then
                flasherResetButton = New Button With {.Text = "Reset", .Width = 82, .Height = 38, .FlatStyle = FlatStyle.Flat}
                StyleDarkButton(flasherResetButton)
                AddHandler flasherResetButton.Click, AddressOf Reset_Click
                Controls.Add(flasherResetButton)
            End If

            saveTheseSettings = New CheckBox With {
                .Text = If(isDedicatedFlasher, "Flasher settings save automatically", "Light settings save automatically"),
                .Checked = True, .Enabled = False, .Visible = False,
                .ForeColor = Color.Gainsboro
            }
            Controls.Add(saveTheseSettings)

            BuildArtworkPreviewUi()
            BuildFlasherMaskRefinementUi()

            flasherOkButton = New Button With {
                .Text = "✓  Save & Close", .Left = 1082, .Top = 752, .Width = 146, .Height = 38,
                .DialogResult = DialogResult.OK, .FlatStyle = FlatStyle.Flat,
                .BackColor = accent, .ForeColor = Color.Black,
                .Font = New Font("Tahoma", 10.0F, FontStyle.Bold)
            }
            flasherCloseButton = New Button With {
                .Text = "Close", .Left = 1238, .Top = 752, .Width = 126, .Height = 38,
                .DialogResult = DialogResult.OK, .FlatStyle = FlatStyle.Flat
            }
            StyleDarkButton(flasherCloseButton)
            Controls.Add(flasherOkButton)
            Controls.Add(flasherCloseButton)
            AcceptButton = flasherOkButton
            Me.CancelButton = flasherCloseButton
            BuildFlasherManagedLayout()
            AddHandler Me.Resize, AddressOf FlasherEditor_Resize
            AddHandler Me.Shown, AddressOf FlasherEditor_Resize
            AddHandler flasherPreviewGroup.Resize, AddressOf FlasherSection_Resize
            LayoutFlasherEditor()

            AddHandler spread.ValueChanged, AddressOf PreviewControlChanged
            AddHandler softness.ValueChanged, AddressOf PreviewControlChanged
            AddHandler falloff.ValueChanged, AddressOf PreviewControlChanged
            AddHandler transmissionContrast.ValueChanged, AddressOf PreviewControlChanged
            AddHandler intensity.ValueChanged, AddressOf PreviewControlChanged
            AddHandler lightTemperature.ValueChanged, AddressOf PreviewControlChanged
            isInitializing = False
            QueueLivePreview()
    End Sub

    Private Sub BuildArtworkPreviewUi()
        Dim accent As Color = Color.FromArgb(255, 153, 0)
        Dim panelColor As Color = Color.FromArgb(27, 29, 31)
        ' Light and Flasher intentionally share the exact same managed layout.
        ' Only their user-facing labels and their trigger identity differ.
        Dim previewGroupHeight As Integer = 396
        Dim previewImageHeight As Integer = 250
        Dim previewControlsTop As Integer = 320

        Dim previewGroup As New GroupBox With {
            .Text = If(isDedicatedFlasher, "  LIVE FLASHER PREVIEW", "  LIVE LIGHT PREVIEW"),
            .Left = 495, .Top = 18, .Width = 695, .Height = previewGroupHeight,
            .ForeColor = accent, .BackColor = panelColor,
            .Font = New Font("Tahoma", 9.0F, FontStyle.Bold),
            .Anchor = AnchorStyles.Top Or AnchorStyles.Left
        }

        flasherPreviewGroup = previewGroup

        previewGroup.Controls.Add(New Label With {
            .Text = If(isDedicatedFlasher, "Live Flash Result", "Live Light Result"), .Left = 14, .Top = 29, .Width = 665, .Height = 24,
            .TextAlign = ContentAlignment.MiddleCenter, .ForeColor = Color.WhiteSmoke,
            .Font = New Font("Tahoma", 9.0F),
            .Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        })

        livePreviewFlashed = New EditorPreviewPictureBox With {
            .Left = 14, .Top = 57, .Width = 665, .Height = previewImageHeight,
            .BackColor = Color.FromArgb(11, 12, 13), .BorderStyle = BorderStyle.FixedSingle,
            .SizeMode = PictureBoxSizeMode.Normal,
            .Anchor = AnchorStyles.Top Or AnchorStyles.Left
        }

        ' Kept as an internal mode selector for the existing Live Flash/Blink buttons.
        previewMode = New ComboBox With {
            .Left = 14, .Top = previewControlsTop + 2, .Width = 165,
            .DropDownStyle = ComboBoxStyle.DropDownList,
            .BackColor = Color.FromArgb(38, 40, 43), .ForeColor = Color.WhiteSmoke,
            .Visible = False, .Anchor = AnchorStyles.Bottom Or AnchorStyles.Left
        }
        previewMode.Items.AddRange(New Object() {If(isDedicatedFlasher, "Live Flash", "Live Light"), "Blink"})
        previewMode.SelectedIndex = 0
        AddHandler previewMode.SelectedIndexChanged, AddressOf PreviewModeChanged

        Dim zoomOutButton As New Button With {.Text = "−", .Left = 190, .Top = previewControlsTop, .Width = 42, .Height = 29, .FlatStyle = FlatStyle.Flat, .Anchor = AnchorStyles.Bottom Or AnchorStyles.Left}
        Dim zoomInButton As New Button With {.Text = "+", .Left = 237, .Top = previewControlsTop, .Width = 42, .Height = 29, .FlatStyle = FlatStyle.Flat, .Anchor = AnchorStyles.Bottom Or AnchorStyles.Left}
        Dim fitButton As New Button With {.Text = "Fit", .Left = 284, .Top = previewControlsTop, .Width = 58, .Height = 29, .FlatStyle = FlatStyle.Flat, .Anchor = AnchorStyles.Bottom Or AnchorStyles.Left}
        Dim actualButton As New Button With {.Text = "100%", .Left = 347, .Top = previewControlsTop, .Width = 66, .Height = 29, .FlatStyle = FlatStyle.Flat, .Anchor = AnchorStyles.Bottom Or AnchorStyles.Left}
        StyleDarkButton(zoomOutButton)
        StyleDarkButton(zoomInButton)
        StyleDarkButton(fitButton)
        StyleDarkButton(actualButton)
        AddHandler zoomOutButton.Click, AddressOf PreviewZoomOut_Click
        AddHandler zoomInButton.Click, AddressOf PreviewZoomIn_Click
        AddHandler fitButton.Click, AddressOf PreviewFit_Click
        AddHandler actualButton.Click, AddressOf PreviewActualSize_Click
        previewZoomLabel = New Label With {
            .Text = "Fit", .Left = 422, .Top = previewControlsTop + 5, .Width = 62, .Height = 22,
            .TextAlign = ContentAlignment.MiddleCenter, .ForeColor = accent,
            .Font = New Font("Tahoma", 8.25F, FontStyle.Bold),
            .Anchor = AnchorStyles.Bottom Or AnchorStyles.Left
        }

        livePreviewStatus = New Label With {
            .Text = "Preparing preview...", .Left = 490, .Top = previewControlsTop - 2, .Width = 199, .Height = 36,
            .TextAlign = ContentAlignment.MiddleCenter, .ForeColor = Color.Silver,
            .Font = New Font("Tahoma", 8.25F),
            .Anchor = AnchorStyles.Bottom Or AnchorStyles.Right
        }

        previewGroup.Controls.Add(livePreviewFlashed)
        previewGroup.Controls.Add(previewMode)
        previewGroup.Controls.Add(zoomOutButton)
        previewGroup.Controls.Add(zoomInButton)
        previewGroup.Controls.Add(fitButton)
        previewGroup.Controls.Add(actualButton)
        previewGroup.Controls.Add(previewZoomLabel)
        previewGroup.Controls.Add(livePreviewStatus)
        Controls.Add(previewGroup)

        AddHandler livePreviewFlashed.MouseWheel, AddressOf Preview_MouseWheel
        AddHandler livePreviewFlashed.MouseDown, AddressOf Preview_MouseDown
        AddHandler livePreviewFlashed.MouseMove, AddressOf Preview_MouseMove
        AddHandler livePreviewFlashed.MouseUp, AddressOf Preview_MouseUp
        AddHandler livePreviewFlashed.MouseLeave, AddressOf Preview_MouseLeave
        AddHandler livePreviewFlashed.MouseEnter, AddressOf Preview_MouseEnter
        AddHandler livePreviewFlashed.DoubleClick, AddressOf Preview_DoubleClick

        livePreviewTimer = New Timer With {.Interval = 75}
        AddHandler livePreviewTimer.Tick, AddressOf LivePreviewTimer_Tick
        ' A short scheduler tick lets each selected light keep its own blink
        ' interval. The expensive preview is rebuilt only when a phase changes.
        blinkTimer = New Timer With {.Interval = 25}
        AddHandler blinkTimer.Tick, AddressOf BlinkTimer_Tick
    End Sub

    Private Sub BuildFlasherMaskRefinementUi()
        Dim accent As Color = Color.FromArgb(255, 153, 0)
        flasherSettingsGroup.Controls.Add(New Label With {
            .Name = "flasherMaskHeading", .Text = "MASK REFINEMENT",
            .Left = 18, .Top = 285, .Width = 250, .Height = 20,
            .ForeColor = accent, .Font = New Font("Tahoma", 9.0F, FontStyle.Bold)
        })

        maskRadius = AddMaskSlider(flasherSettingsGroup, "maskRadiusSlider", "Radius", 18, 309, bulb.MaskRadius, 0, 100, " px")
        maskSmooth = AddMaskSlider(flasherSettingsGroup, "maskSmoothSlider", "Smooth", 280, 309, bulb.MaskSmooth, 0, 100, "%")
        maskFeather = AddMaskSlider(flasherSettingsGroup, "maskFeatherSlider", "Feather", 18, 353, bulb.MaskFeather, 0, 250, " px")
        maskContrast = AddMaskSlider(flasherSettingsGroup, "maskContrastSlider", "Contrast", 280, 353, bulb.MaskContrast, 0, 100, "%")
        maskShiftEdge = AddMaskSlider(flasherSettingsGroup, "maskShiftEdgeSlider", "Shift Edge", 18, 397, bulb.MaskShiftEdge, -100, 100, "%")
        flasherRadialSpikes = AddMaskSlider(flasherSettingsGroup, "flasherRadialSpikesSlider", "Radial Spikes", 280, 397, bulb.FlasherRadialSpikes, 0, 100, "%")
        flasherSettingsGroup.Controls.Add(New Label With {
            .Name = "flasherMaskHelp",
            .Text = "The " & If(isDedicatedFlasher, "flasher", "light") & " box is the mask. Positive Shift Edge expands it; negative contracts it.",
            .Left = 18, .Top = 441, .Width = 515, .Height = 20,
            .ForeColor = Color.Silver, .Font = New Font("Tahoma", 8.25F),
            .Anchor = AnchorStyles.Bottom Or AnchorStyles.Left Or AnchorStyles.Right
        })

        flasherQuickSelectButton = New Button With {
            .Name = "flasherQuickSelection", .Text = "Quick Selection...",
            .Left = 18, .Top = 465, .Width = 150, .Height = 27, .FlatStyle = FlatStyle.Flat
        }
        AddHandler flasherQuickSelectButton.Click, AddressOf QuickSelection_Click
        flasherSettingsGroup.Controls.Add(flasherQuickSelectButton)

        AddHandler maskRadius.ValueChanged, AddressOf MaskRefinementControlChanged
        AddHandler maskSmooth.ValueChanged, AddressOf MaskRefinementControlChanged
        AddHandler maskFeather.ValueChanged, AddressOf MaskRefinementControlChanged
        AddHandler maskContrast.ValueChanged, AddressOf MaskRefinementControlChanged
        AddHandler maskShiftEdge.ValueChanged, AddressOf MaskRefinementControlChanged
        AddHandler flasherRadialSpikes.ValueChanged, AddressOf MaskRefinementControlChanged
        UpdateFlasherMaskAvailability()
    End Sub

    Private Sub FlasherEditor_Resize(ByVal sender As Object, ByVal e As EventArgs)
        LayoutFlasherEditor()
        RefreshPreviewImages()
    End Sub

    Private Sub FlasherSection_Resize(ByVal sender As Object, ByVal e As EventArgs)
        LayoutFlasherEditor()
    End Sub

    Private Sub BuildFlasherManagedLayout()
        flasherLayoutRoot = New TableLayoutPanel With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 1,
            .Padding = New Padding(15, 18, 15, 10),
            .Margin = Padding.Empty,
            .BackColor = BackColor
        }
        flasherLayoutRoot.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 575.0F))
        flasherLayoutRoot.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        flasherLayoutRoot.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        flasherLeftPanel = New Panel With {
            .Dock = DockStyle.Fill,
            .Margin = Padding.Empty,
            .BackColor = BackColor
        }
        flasherRightLayout = New TableLayoutPanel With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .Margin = New Padding(5, 0, 0, 0),
            .Padding = Padding.Empty,
            .BackColor = BackColor
        }
        flasherRightLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        flasherRightLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        flasherRightLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 48.0F))

        flasherActionPanel = New FlowLayoutPanel With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.RightToLeft,
            .WrapContents = False,
            .Margin = Padding.Empty,
            .Padding = New Padding(0, 5, 0, 5),
            .BackColor = BackColor
        }

        Controls.Add(flasherLayoutRoot)
        flasherLayoutRoot.Controls.Add(flasherLeftPanel, 0, 0)
        flasherLayoutRoot.Controls.Add(flasherRightLayout, 1, 0)

        For Each control As Control In New Control() {
            flasherTitleLabel, flasherDescriptionLabel, flasherSettingsGroup,
            flasherPreviewModeGroup, saveTheseSettings
        }
            If control IsNot Nothing Then flasherLeftPanel.Controls.Add(control)
        Next

        flasherPreviewGroup.Dock = DockStyle.Fill
        flasherPreviewGroup.Margin = Padding.Empty
        flasherRightLayout.Controls.Add(flasherPreviewGroup, 0, 0)
        flasherRightLayout.Controls.Add(flasherActionPanel, 0, 1)

        flasherCloseButton.Margin = New Padding(10, 0, 0, 0)
        flasherOkButton.Margin = Padding.Empty
        flasherActionPanel.Controls.Add(flasherCloseButton)
        flasherActionPanel.Controls.Add(flasherOkButton)
        If flasherResetButton IsNot Nothing Then
            flasherResetButton.Margin = New Padding(18, 0, 0, 0)
            flasherActionPanel.Controls.Add(flasherResetButton)
        End If

        flasherLayoutRoot.BringToFront()
    End Sub

    Private Sub LayoutFlasherEditor()
        If flasherPreviewGroup Is Nothing Then Return

        If livePreviewFlashed IsNot Nothing Then
            livePreviewFlashed.Width = Math.Max(300, flasherPreviewGroup.ClientSize.Width - 28)
            ' Reserve a clear strip below the image for zoom and status controls.
            livePreviewFlashed.Height = Math.Max(180, flasherPreviewGroup.ClientSize.Height - 165)
        End If
        If livePreviewStatus IsNot Nothing Then
            livePreviewStatus.Left = Math.Max(490, flasherPreviewGroup.ClientSize.Width - livePreviewStatus.Width - 8)
        End If
        LayoutFlasherSettingsMaskControls()
    End Sub

    Private Sub LayoutFlasherSettingsMaskControls()
        If flasherSettingsGroup Is Nothing Then Return
        Const columnWidth As Integer = 260
        LayoutFlasherMaskSlider("maskRadiusSlider", 18, 309, columnWidth)
        LayoutFlasherMaskSlider("maskSmoothSlider", 280, 309, columnWidth)
        LayoutFlasherMaskSlider("maskFeatherSlider", 18, 353, columnWidth)
        LayoutFlasherMaskSlider("maskContrastSlider", 280, 353, columnWidth)
        LayoutFlasherMaskSlider("maskShiftEdgeSlider", 18, 397, columnWidth)
        LayoutFlasherMaskSlider("flasherRadialSpikesSlider", 280, 397, columnWidth)
    End Sub

    Private Sub LayoutFlasherMaskSlider(ByVal controlName As String,
                                        ByVal left As Integer,
                                        ByVal top As Integer,
                                        ByVal columnWidth As Integer)
        Dim slider As Control = FindFlasherMaskControl(controlName)
        Dim caption As Control = FindFlasherMaskControl(controlName & "Caption")
        Dim valueEditor As Control = FindFlasherMaskControl(controlName & "Value")
        If slider Is Nothing OrElse caption Is Nothing OrElse valueEditor Is Nothing Then Return

        Const captionWidth As Integer = 82
        Const valueWidth As Integer = 50
        Const innerGap As Integer = 5
        caption.SetBounds(left, top + 8, captionWidth, 22)
        valueEditor.SetBounds(left + columnWidth - valueWidth, top + 4, valueWidth, 24)
        slider.SetBounds(left + captionWidth,
                         top,
                         Math.Max(120, columnWidth - captionWidth - valueWidth - innerGap),
                         38)
    End Sub

    Private Function FindFlasherMaskControl(ByVal controlName As String) As Control
        If flasherSettingsGroup Is Nothing Then Return Nothing
        Dim matches As Control() = flasherSettingsGroup.Controls.Find(controlName, True)
        Return If(matches.Length > 0, matches(0), Nothing)
    End Function

    Private Sub UpdateFlasherMaskAvailability()
        For Each slider As TrackBar In New TrackBar() {maskRadius, maskSmooth, maskFeather, maskContrast, maskShiftEdge, flasherRadialSpikes}
            If slider IsNot Nothing Then slider.Enabled = True
        Next
    End Sub

    Private Sub QueueLivePreview()
        If livePreviewTimer Is Nothing Then Return
        livePreviewPending = True
        If Not livePreviewTimer.Enabled Then livePreviewTimer.Start()
    End Sub

    Private Sub LivePreviewTimer_Tick(ByVal sender As Object, ByVal e As EventArgs)
        livePreviewTimer.Stop()
        If Not livePreviewPending Then Return
        livePreviewPending = False
        UpdateLiveArtworkPreview()
        FlushCanvasPreview()
        If livePreviewPending Then livePreviewTimer.Start()
    End Sub

    Private Sub QueueCanvasPreview()
        canvasPreviewPending = True
        QueueLivePreview()
    End Sub

    Private Sub FlushCanvasPreview()
        If Not canvasPreviewPending Then Return
        canvasPreviewPending = False
        RaiseEvent PreviewChanged(Me, EventArgs.Empty)
    End Sub

    Private Sub UpdateLiveArtworkPreview()
        If isUpdatingLivePreview OrElse livePreviewFlashed Is Nothing Then Return
        isUpdatingLivePreview = True
        Try
            If bulb.IlluMode <> Illumination.eIlluMode.Flasher Then
                UpdateLiveLightPreview()
                Return
            End If
            Dim bg As Image = GetArtworkBackground()
            If bg Is Nothing Then
                SetPreviewSources(Nothing, Nothing)
                livePreviewStatus.Text = "No backglass image is available for preview."
                Return
            End If

            ' Always preview the complete backglass. Quick Selection still limits
            ' the illuminated pixels, but it must never change the preview framing.
            Dim canvas As B2SPictureBox = If(Backglass.currentTabPage IsNot Nothing, Backglass.currentTabPage.CurrentPictureBox, Nothing)
            ' Every flasher is excluded from the off image, so slider
            ' changes cannot alter that image while this modal editor is open.
            ' Keep it for the life of the dialog instead of rerendering every
            ' other flasher effect on every slider tick. The on image forces
            ' only the active flasher and keeps every other flasher excluded.
            Dim original As Bitmap = Nothing
            Dim flashed As Bitmap = Nothing
            If canvas IsNot Nothing Then
                Dim otherFlashers As Generic.List(Of Illumination.BulbInfo) = PreviewFlashersExceptActive()
                Dim allFlashers As New Generic.List(Of Illumination.BulbInfo)(otherFlashers)
                allFlashers.Add(bulb)
                If previewOriginalSource Is Nothing Then
                    original = canvas.CreateLayeredPreviewImageForBulbs(allFlashers, Nothing)
                End If
                flashed = canvas.CreateLayeredPreviewImageForBulbs(otherFlashers, New Illumination.BulbInfo() {bulb})
            End If
            If original Is Nothing AndAlso previewOriginalSource Is Nothing Then original = New Bitmap(bg)
            If flashed Is Nothing Then
                Dim offSource As Bitmap = If(original, previewOriginalSource)
                flashed = New Bitmap(offSource)
                RenderFallbackFlasherPreview(offSource, flashed)
            End If
            If original IsNot Nothing Then
                SetPreviewSources(original, flashed)
            Else
                SetFlashedPreviewSource(flashed)
            End If
            livePreviewStatus.Text = If(String.IsNullOrEmpty(bulb.SelectionMaskData),
                                        "Live preview — the placed flasher box defines the mask.",
                                        "Live preview — Quick Selection masks the flasher in the full backglass view.")
        Catch ex As Exception
            livePreviewStatus.Text = "Preview error: " & ex.Message
        Finally
            isUpdatingLivePreview = False
        End Try
    End Sub

    Private Function PreviewFlashersExceptActive() As Generic.List(Of Illumination.BulbInfo)
        Dim result As New Generic.List(Of Illumination.BulbInfo)()
        If Backglass.currentBulbs Is Nothing Then Return result

        For Each candidate As Illumination.BulbInfo In Backglass.currentBulbs
            If candidate Is Nothing OrElse Object.ReferenceEquals(candidate, bulb) Then Continue For
            If candidate.IlluMode = Illumination.eIlluMode.Flasher OrElse
               candidate.LightPurpose = Illumination.eLightPurpose.Flasher Then
                result.Add(candidate)
            End If
        Next
        Return result
    End Function

    Private Function PreviewLightsExceptSelected() As Generic.List(Of Illumination.BulbInfo)
        Dim result As New Generic.List(Of Illumination.BulbInfo)()
        If Backglass.currentBulbs Is Nothing Then Return result

        Dim selectedSet As New Generic.HashSet(Of Illumination.BulbInfo)(selectedBulbs)
        For Each candidate As Illumination.BulbInfo In Backglass.currentBulbs
            If candidate Is Nothing OrElse candidate.IsImageSnippit OrElse selectedSet.Contains(candidate) Then Continue For
            result.Add(candidate)
        Next
        Return result
    End Function

    Private Sub UpdateLiveLightPreview()
        ' Render both states through the designer's real layer compositor. This
        ' keeps snippets, scores, opacity, visibility and Z order in the preview.
        Dim original As Bitmap = Nothing
        Dim flashed As Bitmap = Nothing
        Dim canvas As B2SPictureBox = If(Backglass.currentTabPage IsNot Nothing, Backglass.currentTabPage.CurrentPictureBox, Nothing)
        If canvas IsNot Nothing Then
            Dim otherLights As Generic.List(Of Illumination.BulbInfo) = PreviewLightsExceptSelected()
            Dim allLights As New Generic.List(Of Illumination.BulbInfo)(otherLights)
            allLights.AddRange(selectedBulbs)
            If previewOriginalSource Is Nothing Then
                original = canvas.CreateLayeredPreviewImageForBulbs(allLights, Nothing)
            End If
            flashed = canvas.CreateLayeredPreviewImageForBulbs(otherLights, selectedBulbs)
        End If

        Dim bg As Image = GetArtworkBackground()
        If bg Is Nothing Then
            If original IsNot Nothing Then original.Dispose()
            If flashed IsNot Nothing Then flashed.Dispose()
            SetPreviewSources(Nothing, Nothing)
            livePreviewStatus.Text = "No backglass image is available for preview."
            Return
        End If

        If original Is Nothing AndAlso previewOriginalSource Is Nothing Then original = New Bitmap(bg)
        If flashed Is Nothing Then
            Dim offSource As Bitmap = If(original, previewOriginalSource)
            flashed = New Bitmap(offSource)
        Else
            If original IsNot Nothing Then
                SetPreviewSources(original, flashed)
            Else
                SetFlashedPreviewSource(flashed)
            End If
            livePreviewStatus.Text = "Live preview — only the selected light is on; every other light and flasher is off."
            Return
        End If
        Dim rect As New Rectangle(bulb.Location, bulb.Size)
        Dim rectX As New Rectangle(bulb.LocationX, bulb.SizeX)

        If rect.Width <= 0 OrElse rect.Height <= 0 OrElse rectX.Width <= 0 OrElse rectX.Height <= 0 Then
            SetPreviewSources(original, Nothing)
            livePreviewStatus.Text = "The selected light has no visible area."
            Return
        End If

        If bulb.GlowSpread > 0 Then
            Dim scaleX As Double = rectX.Width / CDbl(Math.Max(1, rect.Width))
            Dim scaleY As Double = rectX.Height / CDbl(Math.Max(1, rect.Height))
            rect.Inflate(bulb.GlowSpread, bulb.GlowSpread)
            rectX.Inflate(CInt(Math.Round(bulb.GlowSpread * scaleX)),
                          CInt(Math.Round(bulb.GlowSpread * scaleY)))
        End If
        rectX = Illumination.Create.RotatedLightBounds(rectX, bulb.LightRotationAngle)
        rectX.Intersect(New Rectangle(0, 0, original.Width, original.Height))

        If rectX.Width > 0 AndAlso rectX.Height > 0 Then
            Dim creator As New Illumination.Create()
            Dim overlay As Image = creator.CreateOverlayImage(
                original, rect, rectX, bulb.Intensity, bulb.LightColor, bulb.DodgeColor,
                bulb.Text, Nothing, bulb.TextAlignment, Illumination.eIlluMode.Standard,
                bulb.GlowSoftness, bulb.GlowFalloff, bulb.GlowIntensity,
                bulb.SelectionMaskData, bulb.SelectionFeather, bulb.GlobalMaskLayerExplicit AndAlso Not bulb.InFrontOfGlobalMask,
                bulb.FlasherStyle, bulb.FlasherSaturation, bulb.FlasherHighlightProtection,
                bulb.FlasherDarkAreaLift, bulb.FlasherHotspotX, bulb.FlasherHotspotY,
                bulb.LightDiffusion, bulb.LightTemperature,
                bulb.UsesArtworkPixelRenderer,
                bulb.ArtworkContrast, bulb.MaskRadius, bulb.MaskSmartRadius,
                bulb.MaskSmooth, bulb.MaskFeather, bulb.MaskContrast, bulb.MaskShiftEdge,
                bulb.FlasherRadialSpikes, bulb.LightRotationAngle)
            If overlay IsNot Nothing Then
                Using g As Graphics = Graphics.FromImage(flashed)
                    g.DrawImage(overlay, rectX.X, rectX.Y, rectX.Width, rectX.Height)
                End Using
                overlay.Dispose()
            End If
        End If

        If original IsNot Nothing Then
            SetPreviewSources(original, flashed)
        Else
            SetFlashedPreviewSource(flashed)
        End If
        livePreviewStatus.Text = "Live preview — only the selected light is on; every other light and flasher is off."
    End Sub

    Private Sub PreviewModeChanged(ByVal sender As Object, ByVal e As EventArgs)
        If previewMode Is Nothing OrElse livePreviewFlashed Is Nothing Then Return
        flasherPulsePreviewRepeating = False
        flasherPulsePreviewEndsAt = 0
        flasherPulsePreviewNextAt = 0
        SetTestPulseButtonState(False)
        If blinkTimer IsNot Nothing Then blinkTimer.Stop()
        blinkShowingFlashed = True
        blinkPreviewStartedAt = Diagnostics.Stopwatch.GetTimestamp()
        blinkPreviewSignature = Nothing
        If previewMode.SelectedIndex = 1 AndAlso blinkTimer IsNot Nothing Then
            blinkTimer.Start()
        End If
        RefreshPreviewImages()
    End Sub

    Private Sub RenderFallbackFlasherPreview(ByVal original As Bitmap, ByVal flashed As Bitmap)
        Dim rect As New Rectangle(bulb.Location, bulb.Size)
        Dim rectX As New Rectangle(bulb.LocationX, bulb.SizeX)
        If rect.Width <= 0 OrElse rect.Height <= 0 OrElse rectX.Width <= 0 OrElse rectX.Height <= 0 Then Return
        rectX = Illumination.Create.RotatedLightBounds(rectX, bulb.LightRotationAngle)
        rectX.Intersect(New Rectangle(0, 0, original.Width, original.Height))
        If rectX.Width <= 0 OrElse rectX.Height <= 0 Then Return

        Dim creator As New Illumination.Create()
        Dim overlay As Image = creator.CreateOverlayImage(
            original, rect, rectX, bulb.Intensity, bulb.LightColor, bulb.DodgeColor,
            bulb.Text, Nothing, bulb.TextAlignment, Illumination.eIlluMode.Flasher,
            bulb.GlowSoftness, bulb.GlowFalloff, bulb.GlowIntensity,
            bulb.SelectionMaskData, bulb.SelectionFeather, bulb.GlobalMaskLayerExplicit AndAlso Not bulb.InFrontOfGlobalMask,
            bulb.FlasherStyle, bulb.FlasherSaturation, bulb.FlasherHighlightProtection,
            bulb.FlasherDarkAreaLift, bulb.FlasherHotspotX, bulb.FlasherHotspotY,
            bulb.LightDiffusion, bulb.LightTemperature,
            True, bulb.ArtworkContrast, bulb.MaskRadius, bulb.MaskSmartRadius,
            bulb.MaskSmooth, bulb.MaskFeather, bulb.MaskContrast, bulb.MaskShiftEdge,
            bulb.FlasherRadialSpikes, bulb.LightRotationAngle)
        If overlay Is Nothing Then Return
        Using g As Graphics = Graphics.FromImage(flashed)
            g.DrawImage(overlay, rectX.X, rectX.Y, rectX.Width, rectX.Height)
        End Using
        overlay.Dispose()
    End Sub

    Private Sub BlinkerControlChanged(ByVal sender As Object, ByVal e As EventArgs)
        If isInitializing Then Return
        For Each selected As Illumination.BulbInfo In selectedBulbs
            If Object.ReferenceEquals(sender, blinkerEnabled) Then selected.BlinkEnabled = blinkerEnabled.Checked
            If Object.ReferenceEquals(sender, blinkerInterval) Then selected.BlinkInterval = Math.Max(1, Math.Min(60000, CInt(blinkerInterval.Value)))
            selected.IsIlluminatedImageDirty = True
        Next
        blinkPreviewSignature = Nothing
        QueueLivePreview()
        QueueCanvasPreview()
    End Sub

    Private Sub FlasherPulseControlChanged(ByVal sender As Object, ByVal e As EventArgs)
        If flasherPulseEnabled Is Nothing OrElse flasherPulseDuration Is Nothing Then Return
        UpdateFlasherPulseAvailability()
        If isInitializing Then Return

        Dim duration As Integer = If(flasherPulseEnabled.Checked,
                                     Math.Max(50, Math.Min(5000, CInt(flasherPulseDuration.Value))),
                                     0)
        For Each selected As Illumination.BulbInfo In selectedBulbs
            If selected IsNot Nothing AndAlso (selected.IlluMode = Illumination.eIlluMode.Flasher OrElse selected.LightPurpose = Illumination.eLightPurpose.Flasher) Then
                selected.FlasherPulseDuration = duration
            End If
        Next
        If Backglass.currentData IsNot Nothing Then Backglass.currentData.IsDirty = True
    End Sub

    Private Sub TestFlasherPulsePreview(ByVal sender As Object, ByVal e As EventArgs)
        If bulb.IlluMode = Illumination.eIlluMode.Flasher Then
            If flasherPulsePreviewRepeating Then
                StopRepeatingFlasherPulse(True)
            Else
                StartRepeatingFlasherPulse()
            End If
            Return
        End If

        If previewMode IsNot Nothing Then previewMode.SelectedIndex = 0
        Dim duration As Integer = If(flasherPulseDuration IsNot Nothing,
                                     Math.Max(50, Math.Min(5000, CInt(flasherPulseDuration.Value))),
                                     250)
        flasherPulsePreviewEndsAt = Diagnostics.Stopwatch.GetTimestamp() +
                                  CLng(duration * Diagnostics.Stopwatch.Frequency / 1000.0R)
        If blinkTimer IsNot Nothing Then blinkTimer.Start()
        If livePreviewStatus IsNot Nothing Then livePreviewStatus.Text = "One-shot pulse preview — " & duration.ToString() & " ms"
        RefreshPreviewImages()
    End Sub

    Private Sub StartRepeatingFlasherPulse()
        If previewMode IsNot Nothing AndAlso previewMode.SelectedIndex <> 0 Then previewMode.SelectedIndex = 0
        Dim now As Long = Diagnostics.Stopwatch.GetTimestamp()
        flasherPulsePreviewRepeating = True
        flasherPulsePreviewEndsAt = now + CLng(FlasherTestPulseDurationMilliseconds * Diagnostics.Stopwatch.Frequency / 1000.0R)
        flasherPulsePreviewNextAt = now + Diagnostics.Stopwatch.Frequency
        SetTestPulseButtonState(True)
        If blinkTimer IsNot Nothing Then blinkTimer.Start()
        If livePreviewStatus IsNot Nothing Then livePreviewStatus.Text = "Repeating pulse preview — every 1 second"
        RefreshPreviewImages()
    End Sub

    Private Sub StopRepeatingFlasherPulse(ByVal refreshPreview As Boolean)
        flasherPulsePreviewRepeating = False
        flasherPulsePreviewEndsAt = 0
        flasherPulsePreviewNextAt = 0
        SetTestPulseButtonState(False)
        If blinkTimer IsNot Nothing Then blinkTimer.Stop()
        If livePreviewStatus IsNot Nothing Then livePreviewStatus.Text = "Pulse preview stopped — Live Flash"
        If refreshPreview Then RefreshPreviewImages()
    End Sub

    Private Sub SetTestPulseButtonState(ByVal running As Boolean)
        If testPulseButton Is Nothing Then Return
        If running Then
            testPulseButton.Text = "■  Stop Pulse"
            testPulseButton.BackColor = Color.FromArgb(155, 38, 52)
            testPulseButton.ForeColor = Color.White
            testPulseButton.FlatAppearance.BorderColor = Color.FromArgb(255, 96, 112)
        Else
            testPulseButton.Text = "⚡  Test Pulse"
            StyleDarkButton(testPulseButton)
        End If
    End Sub

    Private Sub BlinkTimer_Tick(ByVal sender As Object, ByVal e As EventArgs)
        If flasherPulsePreviewRepeating Then
            Dim now As Long = Diagnostics.Stopwatch.GetTimestamp()
            Dim previewChanged As Boolean = False
            If flasherPulsePreviewEndsAt > 0 AndAlso now >= flasherPulsePreviewEndsAt Then
                flasherPulsePreviewEndsAt = 0
                previewChanged = True
            End If
            If now >= flasherPulsePreviewNextAt Then
                flasherPulsePreviewEndsAt = now + CLng(FlasherTestPulseDurationMilliseconds * Diagnostics.Stopwatch.Frequency / 1000.0R)
                flasherPulsePreviewNextAt = now + Diagnostics.Stopwatch.Frequency
                previewChanged = True
            End If
            If previewChanged Then RefreshPreviewImages()
            Return
        End If
        If flasherPulsePreviewEndsAt > 0 Then
            RefreshPreviewImages()
            If flasherPulsePreviewEndsAt = 0 AndAlso blinkTimer IsNot Nothing Then blinkTimer.Stop()
            Return
        End If
        If previewMode Is Nothing OrElse previewMode.SelectedIndex <> 1 Then
            If blinkTimer IsNot Nothing Then blinkTimer.Stop()
            Return
        End If
        RefreshPreviewImages()
    End Sub

    Private Function GetArtworkBackground() As Image
        If Backglass.currentTabPage Is Nothing OrElse Backglass.currentTabPage.BackglassData Is Nothing Then Return Nothing
        Return If(Backglass.currentTabPage.BackglassData.IsDMDImageShown,
                  Backglass.currentTabPage.BackglassData.DMDImage,
                  Backglass.currentTabPage.BackglassData.Image)
    End Function

    Private Sub SetPreviewSources(ByVal original As Image, ByVal flashed As Image)
        If previewIndependentBlinkSource IsNot Nothing Then previewIndependentBlinkSource.Dispose() : previewIndependentBlinkSource = Nothing
        blinkPreviewSignature = Nothing
        If previewOriginalSource IsNot Nothing Then previewOriginalSource.Dispose()
        If previewFlashedSource IsNot Nothing Then previewFlashedSource.Dispose()
        previewOriginalSource = If(original IsNot Nothing, New Bitmap(original), Nothing)
        previewFlashedSource = If(flashed IsNot Nothing, New Bitmap(flashed), Nothing)
        If original IsNot Nothing Then original.Dispose()
        If flashed IsNot Nothing Then flashed.Dispose()
        RefreshPreviewImages()
    End Sub

    Private Sub SetFlashedPreviewSource(ByVal flashed As Bitmap)
        If previewIndependentBlinkSource IsNot Nothing Then previewIndependentBlinkSource.Dispose() : previewIndependentBlinkSource = Nothing
        blinkPreviewSignature = Nothing
        If previewFlashedSource IsNot Nothing Then previewFlashedSource.Dispose()
        previewFlashedSource = flashed
        RefreshPreviewImages()
    End Sub

    Private Sub RefreshPreviewImages()
        If livePreviewFlashed Is Nothing Then Return

        Dim previewSource As Bitmap = previewFlashedSource
        If flasherPulsePreviewRepeating Then
            previewSource = If(flasherPulsePreviewEndsAt > Diagnostics.Stopwatch.GetTimestamp(),
                               previewFlashedSource,
                               previewOriginalSource)
        ElseIf flasherPulsePreviewEndsAt > 0 Then
            If Diagnostics.Stopwatch.GetTimestamp() >= flasherPulsePreviewEndsAt Then
                flasherPulsePreviewEndsAt = 0
                previewSource = previewOriginalSource
                If livePreviewStatus IsNot Nothing Then livePreviewStatus.Text = "One-shot pulse preview complete."
            End If
        ElseIf previewMode IsNot Nothing AndAlso previewMode.SelectedIndex = 1 Then
            RefreshIndependentBlinkPreview()
            previewSource = If(previewIndependentBlinkSource, previewOriginalSource)
        End If
        SetPreviewImage(livePreviewFlashed, RenderPreviewCanvas(previewSource, livePreviewFlashed.ClientSize))
        If previewZoomLabel IsNot Nothing Then previewZoomLabel.Text = If(previewZoomPercent <= 0, "Fit", previewZoomPercent.ToString() & "%")
    End Sub

    Private Sub RefreshIndependentBlinkPreview()
        Dim canvas As B2SPictureBox = If(Backglass.currentTabPage IsNot Nothing, Backglass.currentTabPage.CurrentPictureBox, Nothing)
        If canvas Is Nothing OrElse selectedBulbs.Count = 0 Then Return

        Dim elapsedMilliseconds As Double = (Diagnostics.Stopwatch.GetTimestamp() - blinkPreviewStartedAt) * 1000.0R / Diagnostics.Stopwatch.Frequency
        Dim switchedOff As New List(Of Illumination.BulbInfo)()
        Dim switchedOn As New List(Of Illumination.BulbInfo)()
        Dim signature As New Text.StringBuilder(selectedBulbs.Count * 12)

        For Each selected As Illumination.BulbInfo In selectedBulbs
            If selected Is Nothing Then Continue For
            Dim interval As Integer = Math.Max(1, Math.Min(60000, selected.BlinkInterval))
            Dim phaseOn As Boolean = (CLng(Math.Floor(elapsedMilliseconds / interval)) Mod 2L) = 0L
            signature.Append(selected.ID).Append(If(phaseOn, "+", "-")).Append(";")
            If phaseOn Then
                switchedOn.Add(selected)
            Else
                switchedOff.Add(selected)
            End If
        Next

        Dim nextSignature As String = signature.ToString()
        If nextSignature = blinkPreviewSignature AndAlso previewIndependentBlinkSource IsNot Nothing Then Return

        Dim excluded As New List(Of Illumination.BulbInfo)(PreviewLightsExceptSelected())
        excluded.AddRange(switchedOff)
        Dim rendered As Bitmap = canvas.CreateLayeredPreviewImageForBulbs(excluded, switchedOn)
        If rendered Is Nothing Then Return
        If previewIndependentBlinkSource IsNot Nothing Then previewIndependentBlinkSource.Dispose()
        previewIndependentBlinkSource = rendered
        blinkPreviewSignature = nextSignature
        If livePreviewStatus IsNot Nothing Then livePreviewStatus.Text = "Blink preview — only the selected light is blinking; every other light and flasher is off."
    End Sub

    Private Function RenderPreviewCanvas(ByVal source As Image, ByVal canvasSize As Size) As Bitmap
        If source Is Nothing OrElse canvasSize.Width <= 0 OrElse canvasSize.Height <= 0 Then Return Nothing
        Dim scale As Double
        If previewZoomPercent <= 0 Then
            scale = Math.Min(canvasSize.Width / CDbl(source.Width), canvasSize.Height / CDbl(source.Height))
        Else
            scale = previewZoomPercent / 100.0
        End If
        scale = Math.Max(0.05, Math.Min(8.0, scale))
        Dim drawWidth As Integer = Math.Max(1, CInt(Math.Round(source.Width * scale)))
        Dim drawHeight As Integer = Math.Max(1, CInt(Math.Round(source.Height * scale)))
        Dim canvas As New Bitmap(canvasSize.Width, canvasSize.Height, Imaging.PixelFormat.Format32bppArgb)
        Using g As Graphics = Graphics.FromImage(canvas)
            g.Clear(Color.FromArgb(11, 12, 13))
            g.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
            g.PixelOffsetMode = Drawing2D.PixelOffsetMode.HighQuality
            g.CompositingQuality = Drawing2D.CompositingQuality.HighQuality
            Dim left As Integer = (canvasSize.Width - drawWidth) \ 2 + previewPanOffset.X
            Dim top As Integer = (canvasSize.Height - drawHeight) \ 2 + previewPanOffset.Y
            g.DrawImage(source, New Rectangle(left, top, drawWidth, drawHeight))
        End Using
        Return canvas
    End Function

    Private Sub SetPreviewImage(ByVal picture As PictureBox, ByVal image As Image)
        If picture Is Nothing Then
            If image IsNot Nothing Then image.Dispose()
            Return
        End If
        Dim oldImage As Image = picture.Image
        picture.Image = image
        If oldImage IsNot Nothing Then oldImage.Dispose()
    End Sub

    Private Sub PreviewZoomOut_Click(ByVal sender As Object, ByVal e As EventArgs)
        If previewZoomPercent <= 0 Then previewZoomPercent = GetFitZoomPercent()
        previewZoomPercent = Math.Max(10, previewZoomPercent - 25)
        RefreshPreviewImages()
    End Sub

    Private Sub PreviewZoomIn_Click(ByVal sender As Object, ByVal e As EventArgs)
        If previewZoomPercent <= 0 Then previewZoomPercent = GetFitZoomPercent()
        previewZoomPercent = Math.Min(800, previewZoomPercent + 25)
        RefreshPreviewImages()
    End Sub

    Private Sub PreviewFit_Click(ByVal sender As Object, ByVal e As EventArgs)
        previewZoomPercent = 0
        previewPanOffset = Point.Empty
        RefreshPreviewImages()
    End Sub

    Private Sub PreviewActualSize_Click(ByVal sender As Object, ByVal e As EventArgs)
        previewZoomPercent = 100
        previewPanOffset = Point.Empty
        RefreshPreviewImages()
    End Sub

    Private Sub Preview_MouseWheel(ByVal sender As Object, ByVal e As MouseEventArgs)
        If livePreviewFlashed Is Nothing Then Return

        Dim oldZoom As Integer = previewZoomPercent
        If oldZoom <= 0 Then oldZoom = GetFitZoomPercent()

        Dim newZoom As Integer = oldZoom
        If e.Delta > 0 Then
            newZoom = Math.Min(800, oldZoom + 25)
        ElseIf e.Delta < 0 Then
            newZoom = Math.Max(10, oldZoom - 25)
        End If
        If newZoom = oldZoom Then Return

        ' Keep the artwork point beneath the mouse in the same location while zooming.
        Dim canvasCenter As New Point(livePreviewFlashed.ClientSize.Width \ 2,
                                     livePreviewFlashed.ClientSize.Height \ 2)
        Dim relativeX As Double = e.X - canvasCenter.X - previewPanOffset.X
        Dim relativeY As Double = e.Y - canvasCenter.Y - previewPanOffset.Y
        Dim ratio As Double = newZoom / CDbl(oldZoom)
        previewPanOffset = New Point(
            CInt(Math.Round(e.X - canvasCenter.X - relativeX * ratio)),
            CInt(Math.Round(e.Y - canvasCenter.Y - relativeY * ratio)))

        previewZoomPercent = newZoom
        RefreshPreviewImages()
    End Sub

    Private Sub Preview_MouseDown(ByVal sender As Object, ByVal e As MouseEventArgs)
        If e.Button <> MouseButtons.Left OrElse livePreviewFlashed Is Nothing Then Return

        previewIsPanning = True
        previewPanStart = e.Location
        previewPanStartOffset = previewPanOffset
        livePreviewFlashed.Cursor = Cursors.Hand
        livePreviewFlashed.Capture = True
    End Sub

    Private Sub Preview_MouseMove(ByVal sender As Object, ByVal e As MouseEventArgs)
        If Not previewIsPanning Then Return

        previewPanOffset = New Point(previewPanStartOffset.X + e.X - previewPanStart.X,
                                     previewPanStartOffset.Y + e.Y - previewPanStart.Y)
        RefreshPreviewImages()
    End Sub

    Private Sub Preview_MouseUp(ByVal sender As Object, ByVal e As MouseEventArgs)
        If e.Button = MouseButtons.Left Then EndPreviewPan()
    End Sub

    Private Sub Preview_MouseLeave(ByVal sender As Object, ByVal e As EventArgs)
        If previewIsPanning AndAlso Control.MouseButtons = MouseButtons.None Then EndPreviewPan()
    End Sub

    Private Sub Preview_MouseEnter(ByVal sender As Object, ByVal e As EventArgs)
        If livePreviewFlashed IsNot Nothing Then livePreviewFlashed.Focus()
    End Sub

    Private Sub Preview_DoubleClick(ByVal sender As Object, ByVal e As EventArgs)
        PreviewFit_Click(sender, e)
    End Sub

    Private Sub EndPreviewPan()
        previewIsPanning = False
        If livePreviewFlashed IsNot Nothing Then
            livePreviewFlashed.Capture = False
            livePreviewFlashed.Cursor = Cursors.Default
        End If
    End Sub

    Private Function GetFitZoomPercent() As Integer
        Dim source As Image = If(previewOriginalSource IsNot Nothing, DirectCast(previewOriginalSource, Image), DirectCast(previewFlashedSource, Image))
        If source Is Nothing OrElse livePreviewFlashed Is Nothing Then Return 100
        Dim scale As Double = Math.Min(livePreviewFlashed.ClientSize.Width / CDbl(source.Width), livePreviewFlashed.ClientSize.Height / CDbl(source.Height))
        Return Math.Max(10, Math.Min(800, CInt(Math.Round(scale * 100.0))))
    End Function

    Private Function AddArtworkSlider(ByVal parent As Control,
                                      ByVal caption As String,
                                      ByVal top As Integer,
                                      ByVal initialValue As Integer,
                                      ByVal minimum As Integer,
                                      ByVal maximum As Integer,
                                      ByVal suffix As String) As TrackBar
        parent.Controls.Add(New Label With {
            .Text = caption, .Left = 18, .Top = top + 8, .Width = 145,
            .ForeColor = Color.WhiteSmoke, .Font = New Font("Tahoma", 9.0F)
        })
        Dim number As New NumericUpDown With {
            .Left = 365, .Top = top + 4, .Width = 72,
            .BackColor = Color.FromArgb(38, 40, 43), .ForeColor = Color.WhiteSmoke
        }
        Dim slider As New TrackBar With {
            .Left = 160, .Top = top, .Width = 200, .Height = 38, .AutoSize = False,
            .Minimum = minimum, .Maximum = maximum,
            .TickFrequency = Math.Max(1, (maximum - minimum) \ 10),
            .Value = Math.Max(minimum, Math.Min(maximum, initialValue)),
            .BackColor = parent.BackColor
        }
        TrackBarNumericLink.Bind(slider, number)
        parent.Controls.Add(slider)
        parent.Controls.Add(number)
        parent.Controls.Add(New Label With {.Text = suffix, .Left = 440, .Top = top + 8, .Width = 32,
                                            .ForeColor = Color.WhiteSmoke, .Font = New Font("Tahoma", 9.0F)})
        Return slider
    End Function

    Private Sub StyleDarkButton(ByVal button As Button)
        button.BackColor = Color.FromArgb(38, 40, 43)
        button.ForeColor = Color.WhiteSmoke
        button.FlatAppearance.BorderColor = Color.FromArgb(78, 80, 84)
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(52, 54, 57)
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(64, 66, 70)
    End Sub

    Private Sub FlashColor_Click(ByVal sender As Object, ByVal e As EventArgs)
        Using dialog As New ColorDialog With {.Color = bulb.LightColor, .FullOpen = True}
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
            For Each selected As Illumination.BulbInfo In selectedBulbs
                selected.LightColor = dialog.Color
                selected.IsIlluminatedImageDirty = True
            Next
            flashColorButton.BackColor = dialog.Color
            flashColorButton.ForeColor = If(dialog.Color.GetBrightness() < 0.45F, Color.White, Color.Black)
            If Backglass.currentData IsNot Nothing Then Backglass.currentData.IsDirty = True
            QueueLivePreview()
        QueueCanvasPreview()
        End Using
    End Sub

    Private Function AddMaskSlider(ByVal parent As Control,
                                   ByVal controlName As String,
                                   ByVal caption As String,
                                   ByVal left As Integer,
                                   ByVal top As Integer,
                                   ByVal value As Integer,
                                   ByVal minimum As Integer,
                                   ByVal maximum As Integer,
                                   ByVal suffix As String) As TrackBar
        parent.Controls.Add(New Label With {
            .Name = controlName & "Caption", .Text = caption,
            .Left = left, .Top = top + 8, .Width = 82, .Height = 22,
            .ForeColor = Color.WhiteSmoke, .Font = New Font("Tahoma", 8.5F)
        })
        Dim number As New NumericUpDown With {
            .Name = controlName & "Value",
            .Left = left + 205, .Top = top + 4, .Width = 55, .Height = 24,
            .BackColor = Color.FromArgb(38, 40, 43), .ForeColor = Color.WhiteSmoke
        }
        Dim slider As New TrackBar With {
            .Name = controlName, .Left = left + 82, .Top = top, .Width = 200, .Height = 38, .AutoSize = False,
            .Minimum = minimum, .Maximum = maximum,
            .TickFrequency = Math.Max(1, (maximum - minimum) \ 10),
            .SmallChange = 1, .LargeChange = Math.Max(1, (maximum - minimum) \ 20),
            .Value = Math.Max(minimum, Math.Min(maximum, value)), .BackColor = parent.BackColor
        }
        TrackBarNumericLink.Bind(slider, number)
        parent.Controls.Add(slider)
        parent.Controls.Add(number)
        Return slider
    End Function

    Private Function AddArtworkKelvinSlider(ByVal parent As Control,
                                             ByVal caption As String,
                                             ByVal top As Integer,
                                             ByVal initialValue As Integer) As TrackBar
        parent.Controls.Add(New Label With {
            .Text = caption, .Left = 18, .Top = top + 8, .Width = 145,
            .ForeColor = Color.WhiteSmoke, .Font = New Font("Tahoma", 9.0F)
        })
        Dim number As New NumericUpDown With {
            .Left = 365, .Top = top + 4, .Width = 78,
            .BackColor = Color.FromArgb(38, 40, 43), .ForeColor = Color.WhiteSmoke
        }
        Dim initialKelvin As Integer = Math.Max(2000, Math.Min(6500, initialValue))
        Dim slider As New TrackBar With {
            .Left = 160, .Top = top, .Width = 200, .Height = 38, .AutoSize = False,
            .Minimum = 2000, .Maximum = 6500, .SmallChange = 100, .LargeChange = 500,
            .TickFrequency = 500, .Value = initialKelvin, .BackColor = parent.BackColor
        }
        TrackBarNumericLink.Bind(slider, number, 100)
        parent.Controls.Add(slider)
        parent.Controls.Add(number)
        parent.Controls.Add(New Label With {.Text = "K", .Left = 446, .Top = top + 8, .Width = 22,
                                            .ForeColor = Color.WhiteSmoke, .Font = New Font("Tahoma", 9.0F)})
        Return slider
    End Function

    Private Function AddMaskNumber(ByVal parent As Control, ByVal caption As String,
                                   ByVal left As Integer, ByVal top As Integer,
                                   ByVal value As Integer, ByVal minimum As Integer,
                                   ByVal maximum As Integer) As NumericUpDown
        parent.Controls.Add(New Label With {.Text = caption, .Left = left, .Top = top + 4, .Width = 100})
        Dim number As New NumericUpDown With {.Left = left + 105, .Top = top, .Width = 120,
                                              .Minimum = minimum, .Maximum = maximum,
                                              .Value = Math.Max(minimum, Math.Min(maximum, value))}
        parent.Controls.Add(number)
        Return number
    End Function

    Private Function AddGroupSlider(ByVal group As GroupBox, ByVal caption As String, ByVal top As Integer, ByVal initialValue As Integer, ByVal minimum As Integer, ByVal maximum As Integer) As TrackBar
        group.Controls.Add(New Label With {.Text = caption, .Left = 12, .Top = top + 7, .Width = 115})
        Dim number As New NumericUpDown With {.Left = 454, .Top = top + 3, .Width = 68}
        Dim slider As New TrackBar With {.Left = 127, .Top = top, .Width = 323, .Minimum = minimum, .Maximum = maximum, .TickFrequency = Math.Max(1, (maximum - minimum) \ 10), .Value = Math.Max(minimum, Math.Min(maximum, initialValue))}
        TrackBarNumericLink.Bind(slider, number)
        group.Controls.Add(slider) : group.Controls.Add(number)
        Return slider
    End Function

    Private Sub UpdateLightPurposeButtonAppearance()
        If lightPurposeFlasher Is Nothing Then Return

        If lightPurposeFlasher.Checked Then
            lightPurposeFlasher.Text = "FLASHER LIGHT — ACTIVE  •  CLICK TO CHANGE TO LAMP"
            lightPurposeFlasher.BackColor = Color.FromArgb(184, 92, 0)
            lightPurposeFlasher.ForeColor = Color.White
            lightPurposeFlasher.FlatAppearance.BorderColor = Color.FromArgb(255, 181, 71)
        Else
            lightPurposeFlasher.Text = "LAMP LIGHT — ACTIVE  •  CLICK TO CHANGE TO FLASHER"
            lightPurposeFlasher.BackColor = Color.FromArgb(48, 82, 122)
            lightPurposeFlasher.ForeColor = Color.White
            lightPurposeFlasher.FlatAppearance.BorderColor = Color.FromArgb(115, 170, 225)
        End If
        UpdateFlasherPulseAvailability()
    End Sub

    Private Sub UpdateFlasherPulseAvailability()
        If flasherPulseEnabled Is Nothing OrElse flasherPulseDuration Is Nothing Then Return
        Dim isFlasher As Boolean = bulb.IlluMode = Illumination.eIlluMode.Flasher OrElse
                                   bulb.LightPurpose = Illumination.eLightPurpose.Flasher
        flasherPulseEnabled.Enabled = isFlasher
        flasherPulseDuration.Enabled = isFlasher AndAlso flasherPulseEnabled.Checked
    End Sub

    Private Sub LightPurposeChanged(ByVal sender As Object, ByVal e As EventArgs)
        UpdateLightPurposeButtonAppearance()
        If isInitializing Then Return

        Dim requestedFlasherProfile As Boolean = lightPurposeFlasher.Checked
        If requestedFlasherProfile = currentProfileIsFlasher Then Return

        ' Save the slider positions under the profile we are LEAVING.
        ' bulb.LightPurpose still contains the old type at this point.
        Illumination.LightGlowDefaults.Save(CInt(spread.Value), softness.Value, falloff.Value,
                                              intensity.Value, bulb,
                                              If(lightDiffusion IsNot Nothing, lightDiffusion.Value, 0),
                                              If(lightTemperature IsNot Nothing, lightTemperature.Value, 4000))

        ' Change the object's type, then recall the completely separate profile.
        bulb.LightPurpose = If(requestedFlasherProfile,
                               Illumination.eLightPurpose.Flasher,
                               Illumination.eLightPurpose.Lamp)
        SetIntensityRange(requestedFlasherProfile)
        currentProfileIsFlasher = requestedFlasherProfile
        Illumination.LightGlowDefaults.ApplyTo(bulb)
        LoadCurrentProfileIntoSliders()
        ApplyToBulb()
        QueueLivePreview()
        QueueCanvasPreview()

        If Backglass.currentData IsNot Nothing Then Backglass.currentData.IsDirty = True
        RefreshOpenLayersPanel()
    End Sub

    Private Sub SetIntensityRange(ByVal isFlasher As Boolean)
        If intensity Is Nothing Then Return
        Dim newMaximum As Decimal = If(isFlasher, 1600D, 800D)
        If intensity.Maximum = newMaximum Then Return
        intensity.Maximum = newMaximum
        If intensity.Value > intensity.Maximum Then intensity.Value = intensity.Maximum
    End Sub

    Private Sub LoadCurrentProfileIntoSliders()
        isInitializing = True
        Try
            spread.Value = Math.Max(spread.Minimum, Math.Min(spread.Maximum, bulb.GlowSpread))
            softness.Value = Math.Max(softness.Minimum, Math.Min(softness.Maximum, bulb.GlowSoftness))
            falloff.Value = Math.Max(falloff.Minimum, Math.Min(falloff.Maximum, bulb.GlowFalloff))
            intensity.Value = Math.Max(intensity.Minimum, Math.Min(intensity.Maximum, bulb.GlowIntensity))
            If lightDiffusion IsNot Nothing Then
                lightDiffusion.Value = Math.Max(lightDiffusion.Minimum, Math.Min(lightDiffusion.Maximum, bulb.LightDiffusion))
            End If
            If lightTemperature IsNot Nothing Then
                lightTemperature.Value = Math.Max(lightTemperature.Minimum, Math.Min(lightTemperature.Maximum, bulb.LightTemperature))
            End If
        Finally
            isInitializing = False
        End Try
    End Sub

    Private Sub RefreshOpenLayersPanel()
        For Each openForm As Form In Application.OpenForms
            If TypeOf openForm Is formToolLayers Then
                DirectCast(openForm, formToolLayers).RefreshLayers()
                Exit For
            End If
        Next
    End Sub

    Private Sub PreviewControlChanged(ByVal sender As Object, ByVal e As EventArgs)
        If isInitializing Then Return
        For Each selected As Illumination.BulbInfo In selectedBulbs
            If Object.ReferenceEquals(sender, spread) Then selected.GlowSpread = CInt(spread.Value)
            If Object.ReferenceEquals(sender, softness) Then selected.GlowSoftness = softness.Value
            If Object.ReferenceEquals(sender, falloff) Then selected.GlowFalloff = falloff.Value
            If Object.ReferenceEquals(sender, intensity) Then
                selected.GlowIntensity = intensity.Value
                If maskRadius IsNot Nothing Then selected.ArtworkBrightness = Math.Max(-100, Math.Min(300, intensity.Value - 100))
            End If
            If lightDiffusion IsNot Nothing AndAlso Object.ReferenceEquals(sender, lightDiffusion) Then selected.LightDiffusion = lightDiffusion.Value
            If lightTemperature IsNot Nothing AndAlso Object.ReferenceEquals(sender, lightTemperature) Then selected.LightTemperature = lightTemperature.Value
            If transmissionContrast IsNot Nothing AndAlso Object.ReferenceEquals(sender, transmissionContrast) Then selected.ArtworkContrast = transmissionContrast.Value
            selected.IsIlluminatedImageDirty = True
        Next
        If Backglass.currentData IsNot Nothing Then Backglass.currentData.IsDirty = True
        If livePreviewStatus IsNot Nothing Then livePreviewStatus.Text = "Updating live preview..."
        QueueLivePreview()
        QueueCanvasPreview()
    End Sub

    Protected Overrides Sub OnFormClosing(ByVal e As FormClosingEventArgs)
        Dim automaticFlasherCommit As Boolean = bulb.IlluMode = Illumination.eIlluMode.Flasher
        If Me.DialogResult = DialogResult.Cancel AndAlso Not automaticFlasherCommit Then
            bulb.GlowSpread = originalSpread
            bulb.GlowSoftness = originalSoftness
            bulb.GlowIntensity = originalIntensity
            bulb.GlowFalloff = originalFalloff
            bulb.LightDiffusion = originalLightDiffusion
            bulb.LightTemperature = originalLightTemperature
            bulb.LightPurpose = originalLightPurpose
            bulb.ArtworkPixelLighting = originalArtworkPixelLighting
            bulb.GlowBlendMode = originalBlendMode
            bulb.GlowPreviewQuality = originalPreviewQuality
            bulb.InFrontOfGlobalMask = originalInFrontOfMask
            bulb.GlobalMaskLayerExplicit = originalGlobalMaskLayerExplicit
            bulb.LightColor = originalLightColor
            bulb.SelectionMaskData = originalSelectionMask
            bulb.SelectionTolerance = originalSelectionTolerance
            bulb.SelectionFeather = originalSelectionFeather
            bulb.FlasherStyle = originalFlasherStyle
            bulb.FlasherSaturation = originalFlasherSaturation
            bulb.FlasherHighlightProtection = originalFlasherHighlightProtection
            bulb.FlasherDarkAreaLift = originalFlasherDarkAreaLift
            bulb.FlasherHotspotX = originalFlasherHotspotX
            bulb.FlasherHotspotY = originalFlasherHotspotY
            bulb.FlasherPulseDuration = originalFlasherPulseDuration
            bulb.MaskRadius = originalMaskRadius
            bulb.MaskSmartRadius = originalMaskSmartRadius
            bulb.MaskSmooth = originalMaskSmooth
            bulb.MaskFeather = originalMaskFeather
            bulb.MaskContrast = originalMaskContrast
            bulb.MaskShiftEdge = originalMaskShiftEdge
            bulb.FlasherRadialSpikes = originalFlasherRadialSpikes
            bulb.ArtworkBrightness = originalArtworkBrightness
            bulb.ArtworkContrast = originalArtworkContrast
            bulb.ArtworkAdjustmentPasses = originalArtworkAdjustmentPasses
            bulb.BlinkEnabled = originalBlinkEnabled
            bulb.BlinkInterval = originalBlinkInterval
            If bulb.Image IsNot Nothing AndAlso Not Object.ReferenceEquals(bulb.Image, originalImage) Then bulb.Image.Dispose()
            bulb.Image = If(originalImage IsNot Nothing, New Bitmap(originalImage), Nothing)
            bulb.IsImageSnippit = originalIsImageSnippit
            bulb.IsIlluminatedImageDirty = True
            For Each original As KeyValuePair(Of Illumination.BulbInfo, LightSettingsSnapshot) In additionalOriginals
                original.Value.Restore(original.Key)
            Next
        QueueCanvasPreview()
        ElseIf Me.DialogResult = DialogResult.OK OrElse automaticFlasherCommit Then
            ' Save automatically into the independent Light or Flasher profile.
            Illumination.LightGlowDefaults.Save(CInt(spread.Value), softness.Value, falloff.Value,
                                                  intensity.Value, bulb,
                                                  If(lightDiffusion IsNot Nothing, lightDiffusion.Value, 0),
                                                  If(lightTemperature IsNot Nothing, lightTemperature.Value, 4000))
            If Backglass.currentData IsNot Nothing Then Backglass.currentData.IsDirty = True
        End If
        FlushCanvasPreview()
        RefreshOpenLayersPanel()
        If livePreviewTimer IsNot Nothing Then
            livePreviewTimer.Stop()
            livePreviewTimer.Dispose()
            livePreviewTimer = Nothing
        End If
        If blinkTimer IsNot Nothing Then
            blinkTimer.Stop()
            blinkTimer.Dispose()
            blinkTimer = Nothing
        End If
        SetPreviewImage(livePreviewFlashed, Nothing)
        If previewOriginalSource IsNot Nothing Then previewOriginalSource.Dispose() : previewOriginalSource = Nothing
        If previewFlashedSource IsNot Nothing Then previewFlashedSource.Dispose() : previewFlashedSource = Nothing
        If previewIndependentBlinkSource IsNot Nothing Then previewIndependentBlinkSource.Dispose() : previewIndependentBlinkSource = Nothing
        If originalImage IsNot Nothing Then originalImage.Dispose()
        MyBase.OnFormClosing(e)
    End Sub


    Private Sub QuickSelection_Click(ByVal sender As Object, ByVal e As EventArgs)
        If Backglass.currentTabPage Is Nothing OrElse Backglass.currentTabPage.BackglassData Is Nothing Then Return
        Dim bg As Image = If(Backglass.currentTabPage.BackglassData.IsDMDImageShown, Backglass.currentTabPage.BackglassData.DMDImage, Backglass.currentTabPage.BackglassData.Image)
        If bg Is Nothing Then Return

        ' Control changes are committed individually as they occur. Quick
        ' Selection must not batch-copy unrelated slider values.
        Dim inFrontBeforeQuickSelection As Boolean = bulb.InFrontOfGlobalMask

        Using editor As New formQuickSelection(bulb, bg)
            If editor.ShowDialog(Me) = DialogResult.OK Then
                bulb.InFrontOfGlobalMask = inFrontBeforeQuickSelection

                ' Quick Selection stores one full-backglass mask. Apply that
                ' completed mask to every selected Light or Flasher; each object
                ' crops the shared mask through its own bounds while rendering.
                Dim sharedSelectionMask As String = bulb.SelectionMaskData
                For Each selected As Illumination.BulbInfo In selectedBulbs
                    If selected Is Nothing Then Continue For
                    selected.SelectionMaskData = sharedSelectionMask
                    selected.IsIlluminatedImageDirty = True
                Next
                If Backglass.currentData IsNot Nothing Then Backglass.currentData.IsDirty = True
                UpdateFlasherMaskAvailability()
                QueueLivePreview()
        QueueCanvasPreview()
            Else
                bulb.InFrontOfGlobalMask = inFrontBeforeQuickSelection
            End If
        End Using
    End Sub

    Private Function FlasherMaskStatusText() As String
        If String.IsNullOrEmpty(bulb.SelectionMaskData) Then
            Return "No flasher area selected — use 1. Select Flasher Area before building."
        End If
        Return "Flasher area selected — refine the edge below, then preview or build it."
    End Function

    Private Sub MaskRefinementControlChanged(ByVal sender As Object, ByVal e As EventArgs)
        If isInitializing OrElse maskRadius Is Nothing Then Return
        For Each selected As Illumination.BulbInfo In selectedBulbs
            ApplyMaskRefinementToBulb(selected)
        Next
        If Backglass.currentData IsNot Nothing Then Backglass.currentData.IsDirty = True
        If livePreviewStatus IsNot Nothing Then livePreviewStatus.Text = "Updating live preview..."
        QueueLivePreview()
        QueueCanvasPreview()
    End Sub

    Private Sub ApplyMaskRefinementToBulb(ByVal target As Illumination.BulbInfo)
        If target Is Nothing OrElse maskRadius Is Nothing Then Return
        target.MaskRadius = CInt(maskRadius.Value)
        target.MaskSmartRadius = If(maskSmartRadius IsNot Nothing, maskSmartRadius.Checked, False)
        target.MaskSmooth = CInt(maskSmooth.Value)
        target.MaskFeather = CInt(maskFeather.Value)
        target.MaskContrast = CInt(maskContrast.Value)
        target.MaskShiftEdge = CInt(maskShiftEdge.Value)
        target.FlasherRadialSpikes = If(flasherRadialSpikes IsNot Nothing, flasherRadialSpikes.Value, 0)
        target.IsIlluminatedImageDirty = True
    End Sub

    Private Function GetArtworkAssetBounds(ByVal bg As Image) As Rectangle
        Dim imageRect As New Rectangle(0, 0, bg.Width, bg.Height)
        Dim currentBounds As New Rectangle(bulb.LocationX, bulb.SizeX)
        currentBounds.Intersect(imageRect)
        If currentBounds.Width <= 0 OrElse currentBounds.Height <= 0 Then Return Rectangle.Empty

        Dim selectionBounds As Rectangle =
            Illumination.ArtworkFlasherRenderer.GetSelectionBounds(
                bulb.SelectionMaskData, bg.Size, currentBounds)

        If selectionBounds.Width <= 0 OrElse selectionBounds.Height <= 0 Then
            selectionBounds = currentBounds
        End If

        Dim minDimension As Integer = Math.Max(1, Math.Min(selectionBounds.Width, selectionBounds.Height))

        ' IMPORTANT: keep the asset canvas a fixed size while sliders move.
        ' The previous implementation changed the bitmap dimensions whenever
        ' Edge Softness or Outward Diffusion changed. Because the preview is in
        ' Fit mode, that only looked like zooming in and out and hid the actual
        ' pixel/alpha changes.
        '
        ' Reserve enough transparent room for the maximum 300% settings, then
        ' let the renderer change only the alpha pixels inside this fixed canvas.
        Dim padding As Integer =
            CInt(Math.Round(minDimension * 0.90 +
                            Math.Min(120.0, Math.Max(0, bulb.GlowSpread) * 0.15)))

        ' Keep the canvas fixed while allowing the 200–300% diffusion range
        ' to extend substantially farther before it reaches the asset edge.
        padding = Math.Max(28, Math.Min(Math.Max(48, CInt(minDimension * 1.35)), padding))

        Dim padded As Rectangle = selectionBounds
        padded.Inflate(padding, padding)
        padded.Intersect(imageRect)
        Return padded
    End Function

    Private Function RenderArtworkAsset(ByVal bg As Image,
                                        ByVal assetBounds As Rectangle) As Bitmap
        If assetBounds.Width <= 0 OrElse assetBounds.Height <= 0 Then Return Nothing

        Using sourceCrop As New Bitmap(assetBounds.Width, assetBounds.Height, Imaging.PixelFormat.Format32bppArgb)
            Using g As Graphics = Graphics.FromImage(sourceCrop)
                g.Clear(Color.Transparent)
                g.DrawImage(bg,
                            New Rectangle(0, 0, assetBounds.Width, assetBounds.Height),
                            assetBounds,
                            GraphicsUnit.Pixel)
            End Using

            Return Illumination.ArtworkFlasherRenderer.Render(
                sourceCrop, assetBounds, bulb.SelectionMaskData,
                bulb.SelectionFeather, bulb.GlowIntensity, bulb.LightColor, bulb.FlasherStyle,
                bulb.FlasherSaturation, bulb.FlasherHighlightProtection, bulb.FlasherDarkAreaLift,
                bulb.FlasherHotspotX, bulb.FlasherHotspotY, bulb.GlowSoftness,
                bulb.GlowSpread, bulb.GlowFalloff, bulb.ArtworkContrast,
                bulb.MaskRadius, bulb.MaskSmartRadius, bulb.MaskSmooth,
                bulb.MaskFeather, bulb.MaskContrast, bulb.MaskShiftEdge)
        End Using
    End Function

    Private Sub GenerateFlasherImage_Click(ByVal sender As Object, ByVal e As EventArgs)
        GenerateFlasherImage(True)
    End Sub

    Private Sub GenerateFlasherImage(ByVal showMessages As Boolean)
        If bulb.IlluMode <> Illumination.eIlluMode.Flasher Then Return
        If String.IsNullOrEmpty(bulb.SelectionMaskData) Then
            If showMessages Then B2SMessageBox.Show(Me, "Use Quick Selection first to select the area for this flasher.", "Flasher Image", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        If Backglass.currentTabPage Is Nothing OrElse Backglass.currentTabPage.BackglassData Is Nothing Then Return
        Dim bg As Image = If(Backglass.currentTabPage.BackglassData.IsDMDImageShown, Backglass.currentTabPage.BackglassData.DMDImage, Backglass.currentTabPage.BackglassData.Image)
        If bg Is Nothing Then Return

        Cursor = Cursors.WaitCursor
        Try
            Dim bounds As Rectangle = GetArtworkAssetBounds(bg)
            If bounds.Width <= 0 OrElse bounds.Height <= 0 Then Return

            Dim generated As Bitmap = RenderArtworkAsset(bg, bounds)
            If generated Is Nothing Then
                If showMessages Then B2SMessageBox.Show(Me, "The flasher image could not be generated. Check the selection and flasher box.", "Flasher Image", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            If bulb.Image IsNot Nothing Then bulb.Image.Dispose()
            bulb.Image = generated

            bulb.Location = bounds.Location
            bulb.Size = bounds.Size

            bulb.IsImageSnippit = True
            bulb.SnippitInfo.SnippitType = eSnippitType.StandardImage
            bulb.IsIlluminatedImageDirty = True
            If Backglass.currentData IsNot Nothing Then Backglass.currentData.IsDirty = True
            QueueLivePreview()
        QueueCanvasPreview()
            If showMessages Then B2SMessageBox.Show(Me, "The padded Photoshop-style flasher asset was created. The image remains sharp while the alpha feather now has room to spread outside the original selection.", "Flasher Image", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            If showMessages Then B2SMessageBox.Show(Me, "The flasher image could not be generated." & Environment.NewLine & ex.Message, "Flasher Image", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            Cursor = Cursors.Default
        End Try
    End Sub

    Private Sub CopyProperties_Click(ByVal sender As Object, ByVal e As EventArgs)
        LightPropertyClipboard.CopyFrom(bulb)
        For Each c As Control In Controls
            If TypeOf c Is Button AndAlso c.Text = "Paste" Then c.Enabled = True
        Next
    End Sub

    Private Sub PasteProperties_Click(ByVal sender As Object, ByVal e As EventArgs)
        LightPropertyClipboard.PasteTo(bulb)
        LoadBulbValues()
        UpdateFlasherMaskAvailability()
        If Backglass.currentData IsNot Nothing Then Backglass.currentData.IsDirty = True
        QueueLivePreview()
        QueueCanvasPreview()
    End Sub

    Private Sub LoadBulbValues()
        isInitializing = True
        Try
            SetIntensityRange(bulb.IlluMode = Illumination.eIlluMode.Flasher OrElse
                              bulb.UsesArtworkPixelRenderer)
            spread.Value = Math.Max(spread.Minimum, Math.Min(spread.Maximum, bulb.GlowSpread))
            softness.Value = Math.Max(softness.Minimum, Math.Min(softness.Maximum, bulb.GlowSoftness))
            intensity.Value = Math.Max(intensity.Minimum, Math.Min(intensity.Maximum, bulb.GlowIntensity))
            falloff.Value = Math.Max(falloff.Minimum, Math.Min(falloff.Maximum, bulb.GlowFalloff))
            If lightDiffusion IsNot Nothing Then lightDiffusion.Value = Math.Max(lightDiffusion.Minimum, Math.Min(lightDiffusion.Maximum, bulb.LightDiffusion))
            If lightTemperature IsNot Nothing Then lightTemperature.Value = Math.Max(lightTemperature.Minimum, Math.Min(lightTemperature.Maximum, If(bulb.LightTemperature <= 0, 4000, bulb.LightTemperature)))
            If maskRadius IsNot Nothing Then
                maskRadius.Value = Math.Max(maskRadius.Minimum, Math.Min(maskRadius.Maximum, bulb.MaskRadius))
                maskSmooth.Value = Math.Max(maskSmooth.Minimum, Math.Min(maskSmooth.Maximum, bulb.MaskSmooth))
                maskFeather.Value = Math.Max(maskFeather.Minimum, Math.Min(maskFeather.Maximum, bulb.MaskFeather))
                maskContrast.Value = Math.Max(maskContrast.Minimum, Math.Min(maskContrast.Maximum, bulb.MaskContrast))
                maskShiftEdge.Value = Math.Max(maskShiftEdge.Minimum, Math.Min(maskShiftEdge.Maximum, bulb.MaskShiftEdge))
                If flasherRadialSpikes IsNot Nothing Then
                    flasherRadialSpikes.Value = Math.Max(flasherRadialSpikes.Minimum, Math.Min(flasherRadialSpikes.Maximum, bulb.FlasherRadialSpikes))
                End If
                If adjustmentPasses IsNot Nothing Then
                    adjustmentPasses.Value = Math.Max(adjustmentPasses.Minimum, Math.Min(adjustmentPasses.Maximum, bulb.ArtworkAdjustmentPasses))
                End If
                If maskSmartRadius IsNot Nothing Then maskSmartRadius.Checked = bulb.MaskSmartRadius
            End If
            If flasherPulseEnabled IsNot Nothing AndAlso flasherPulseDuration IsNot Nothing Then
                flasherPulseEnabled.Checked = bulb.FlasherPulseDuration > 0
                flasherPulseDuration.Value = Math.Max(flasherPulseDuration.Minimum,
                                                      Math.Min(flasherPulseDuration.Maximum,
                                                               If(bulb.FlasherPulseDuration > 0, bulb.FlasherPulseDuration, 250)))
                UpdateFlasherPulseAvailability()
            End If
        Finally
            isInitializing = False
        End Try
    End Sub

    Private Sub Reset_Click(ByVal sender As Object, ByVal e As EventArgs)
        spread.Value = 0
        softness.Value = 60
        falloff.Value = 60
        intensity.Value = If(bulb.UsesArtworkPixelRenderer, 200, 100)
        If lightDiffusion IsNot Nothing Then lightDiffusion.Value = 0
        If lightTemperature IsNot Nothing Then lightTemperature.Value = 4000
        If flasherPulseEnabled IsNot Nothing Then flasherPulseEnabled.Checked = False
        If flasherPulseDuration IsNot Nothing Then flasherPulseDuration.Value = 250 : flasherPulseDuration.Enabled = False
        If maskRadius IsNot Nothing Then
            maskRadius.Value = 18 : maskSmooth.Value = 50 : maskFeather.Value = 52
            maskContrast.Value = 0 : maskShiftEdge.Value = 10
            If flasherRadialSpikes IsNot Nothing Then flasherRadialSpikes.Value = 0
            If adjustmentPasses IsNot Nothing Then adjustmentPasses.Value = 1
            If maskSmartRadius IsNot Nothing Then maskSmartRadius.Checked = False
        End If
    End Sub

    Public Sub ApplyToBulb()
        For Each selected As Illumination.BulbInfo In selectedBulbs
            ApplyControlsToBulb(selected)
        Next
    End Sub

    Private Sub ApplyControlsToBulb(ByVal target As Illumination.BulbInfo)
        If Not target.IsImageSnippit AndAlso
           target.IlluMode <> Illumination.eIlluMode.Flasher AndAlso
           target.LightPurpose <> Illumination.eLightPurpose.Flasher Then
            target.ArtworkPixelLighting = True
        End If
        target.GlowSpread = CInt(spread.Value)
        target.GlowSoftness = softness.Value
        target.GlowFalloff = falloff.Value
        target.GlowIntensity = intensity.Value
        If lightDiffusion IsNot Nothing Then target.LightDiffusion = lightDiffusion.Value
        If lightTemperature IsNot Nothing Then target.LightTemperature = lightTemperature.Value
        If lightPurposeFlasher IsNot Nothing Then
            target.LightPurpose = If(lightPurposeFlasher.Checked,
                                   Illumination.eLightPurpose.Flasher,
                                   Illumination.eLightPurpose.Lamp)
        End If
        If transmissionContrast IsNot Nothing Then target.ArtworkContrast = transmissionContrast.Value
        If blinkerEnabled IsNot Nothing Then target.BlinkEnabled = blinkerEnabled.Checked
        If blinkerInterval IsNot Nothing Then target.BlinkInterval = Math.Max(1, Math.Min(60000, CInt(blinkerInterval.Value)))
        If flasherPulseEnabled IsNot Nothing AndAlso flasherPulseDuration IsNot Nothing Then
            target.FlasherPulseDuration = If(flasherPulseEnabled.Checked,
                                             Math.Max(50, Math.Min(5000, CInt(flasherPulseDuration.Value))),
                                             0)
        End If
        If maskRadius IsNot Nothing Then
            ApplyMaskRefinementToBulb(target)
            target.ArtworkBrightness = Math.Max(-100, Math.Min(300, intensity.Value - 100))
            If transmissionContrast IsNot Nothing Then target.ArtworkContrast = transmissionContrast.Value
            target.FlasherStyle = 1
        End If
        target.IsIlluminatedImageDirty = True
    End Sub

    Private NotInheritable Class LightSettingsSnapshot
        Private ReadOnly spread, softness, falloff, glowIntensity, diffusion, temperature, artworkBrightness, artworkContrast, adjustmentPasses, maskRadius, maskSmooth, maskFeather, maskContrast, maskShiftEdge, flasherRadialSpikes, blinkInterval, flasherPulseDuration As Integer
        Private ReadOnly purpose As Illumination.eLightPurpose
        Private ReadOnly inFront, globalMaskLayerExplicit, blinkEnabled, smartRadius, artworkPixelLighting As Boolean
        Private ReadOnly lightColor As Color

        Public Sub New(ByVal source As Illumination.BulbInfo)
            spread = source.GlowSpread : softness = source.GlowSoftness : falloff = source.GlowFalloff : glowIntensity = source.GlowIntensity
            diffusion = source.LightDiffusion : temperature = source.LightTemperature : purpose = source.LightPurpose
            artworkPixelLighting = source.ArtworkPixelLighting
            artworkBrightness = source.ArtworkBrightness : artworkContrast = source.ArtworkContrast : adjustmentPasses = source.ArtworkAdjustmentPasses
            maskRadius = source.MaskRadius : smartRadius = source.MaskSmartRadius : maskSmooth = source.MaskSmooth : maskFeather = source.MaskFeather
            maskContrast = source.MaskContrast : maskShiftEdge = source.MaskShiftEdge : inFront = source.InFrontOfGlobalMask : globalMaskLayerExplicit = source.GlobalMaskLayerExplicit
            flasherRadialSpikes = source.FlasherRadialSpikes
            blinkEnabled = source.BlinkEnabled : blinkInterval = source.BlinkInterval : lightColor = source.LightColor
            flasherPulseDuration = source.FlasherPulseDuration
        End Sub

        Public Sub Restore(ByVal target As Illumination.BulbInfo)
            target.GlowSpread = spread : target.GlowSoftness = softness : target.GlowFalloff = falloff : target.GlowIntensity = glowIntensity
            target.LightDiffusion = diffusion : target.LightTemperature = temperature : target.LightPurpose = purpose
            target.ArtworkPixelLighting = artworkPixelLighting
            target.ArtworkBrightness = artworkBrightness : target.ArtworkContrast = artworkContrast : target.ArtworkAdjustmentPasses = adjustmentPasses
            target.MaskRadius = maskRadius : target.MaskSmartRadius = smartRadius : target.MaskSmooth = maskSmooth : target.MaskFeather = maskFeather
            target.MaskContrast = maskContrast : target.MaskShiftEdge = maskShiftEdge : target.InFrontOfGlobalMask = inFront : target.GlobalMaskLayerExplicit = globalMaskLayerExplicit
            target.FlasherRadialSpikes = flasherRadialSpikes
            target.BlinkEnabled = blinkEnabled : target.BlinkInterval = blinkInterval : target.LightColor = lightColor
            target.FlasherPulseDuration = flasherPulseDuration
            target.IsIlluminatedImageDirty = True
        End Sub
    End Class

End Class
