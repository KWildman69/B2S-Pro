Imports System
Imports System.Drawing
Imports System.Windows.Forms

''' <summary>
''' Simple, direct editor tool palette. All commands remain visible and use the
''' original editor icons, captions, tooltips, and event handlers. The palette
''' uses one readable column while docked and two columns while floating.
''' </summary>
Public Class formToolPalette
    Inherits B2SThemedForm

    Private Const DockedPaletteWidth As Integer = 250
    Private Const FloatingPaletteWidth As Integer = 360
    Private Const DockSnapDistance As Integer = 24
    Private Const SectionGap As Integer = 8

    Private ReadOnly contentPanel As New FlowLayoutPanel()
    Private ReadOnly toolTip As New ToolTip()
    Private ReadOnly selectButton As Button

    Private dockSide As DockStyle = DockStyle.None
    Private ownerMoveInProgress As Boolean
    Private editorControlsAttached As Boolean

    Public Event SelectionToolSelected(ByVal sender As Object, ByVal e As EventArgs)

    Public Sub New()
        Me.Name = "formToolPalette"
        Me.Text = "Editor Tools"
        Me.FormBorderStyle = FormBorderStyle.SizableToolWindow
        Me.ShowInTaskbar = False
        Me.StartPosition = FormStartPosition.Manual
        Me.MinimumSize = New Size(DockedPaletteWidth, 300)
        Me.Size = New Size(DockedPaletteWidth, 620)
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.AutoScaleMode = AutoScaleMode.Font

        contentPanel.Dock = DockStyle.Fill
        contentPanel.FlowDirection = FlowDirection.TopDown
        contentPanel.WrapContents = False
        contentPanel.AutoScroll = True
        contentPanel.Padding = New Padding(9, 9, 9, 12)
        contentPanel.Margin = Padding.Empty
        Me.Controls.Add(contentPanel)

        AddSectionHeader("TOOLS")
        selectButton = New Button() With {
            .Name = "btnSelectionTool",
            .Text = "↖   Select / Pointer",
            .Height = 38,
            .Margin = New Padding(1, 2, 1, SectionGap),
            .FlatStyle = FlatStyle.Flat,
            .TabStop = False,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Padding = New Padding(8, 0, 4, 0),
            .Font = New Font(SystemFonts.MessageBoxFont.FontFamily, 9.0F, FontStyle.Bold)
        }
        selectButton.FlatAppearance.BorderSize = 1
        contentPanel.Controls.Add(selectButton)
        toolTip.SetToolTip(selectButton, "Select and edit existing objects")
        AddHandler selectButton.Click, AddressOf SelectButton_Click
        SetSelectionActive()

        AddHandler Me.Load, AddressOf Palette_Load
        AddHandler Me.LocationChanged, AddressOf Palette_LocationChanged
        AddHandler Me.Resize, AddressOf Palette_Resize
        AddHandler Me.FormClosed, AddressOf Palette_FormClosed
        AddHandler Me.DoubleClick, AddressOf Palette_DoubleClick
    End Sub

    ''' <summary>
    ''' Moves the existing editor controls into this palette. The original
    ''' controls are reused so every existing command handler remains intact.
    ''' </summary>
    Public Sub AttachEditorControls(ByVal zoomItems As ToolStripItem(),
                                    ByVal reelsAndLedItems As ToolStripItem(),
                                    ByVal illuminationItems As ToolStripItem(),
                                    ByVal illuminationMenuItems As ToolStripMenuItem())
        If editorControlsAttached Then Return
        editorControlsAttached = True

        AddCommandSection("VIEW", zoomItems)
        AddCommandSection("REELS & LEDS", reelsAndLedItems)
        AddCommandSection("ILLUMINATION", illuminationItems, illuminationMenuItems)
        ApplyResponsiveLayout()
    End Sub

    Private Sub AddSectionHeader(ByVal title As String)
        Dim label As New Label() With {
            .Text = title,
            .Name = "paletteHeader" & title.Replace(" ", String.Empty).Replace("&", String.Empty),
            .AutoSize = False,
            .Height = 25,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Margin = New Padding(1, 2, 1, 2),
            .Padding = New Padding(4, 0, 0, 0),
            .Font = New Font(SystemFonts.MessageBoxFont.FontFamily, 8.0F, FontStyle.Bold)
        }
        contentPanel.Controls.Add(label)
    End Sub

    Private Sub AddCommandSection(ByVal title As String,
                                  ByVal items As ToolStripItem(),
                                  Optional ByVal linkedMenuItems As ToolStripMenuItem() = Nothing)
        AddSectionHeader(title)

        Dim strip As New ToolStrip() With {
            .Name = "palette" & title.Replace(" ", String.Empty).Replace("&", String.Empty),
            .GripStyle = ToolStripGripStyle.Hidden,
            .LayoutStyle = ToolStripLayoutStyle.Flow,
            .AutoSize = False,
            .CanOverflow = False,
            .Padding = New Padding(2),
            .Margin = New Padding(0, 0, 0, SectionGap),
            .Stretch = True,
            .ShowItemToolTips = True
        }

        For Each item As ToolStripItem In items
            If item Is Nothing Then Continue For
            If item.Owner IsNot Nothing Then item.Owner.Items.Remove(item)
            PrepareItemForPalette(item)
            strip.Items.Add(item)
        Next

        If linkedMenuItems IsNot Nothing Then
            For Each menuItem As ToolStripMenuItem In linkedMenuItems
                If menuItem Is Nothing Then Continue For
                strip.Items.Add(CreateLinkedMenuButton(menuItem))
            Next
        End If

        contentPanel.Controls.Add(strip)
    End Sub

    Private Function CreateLinkedMenuButton(ByVal menuItem As ToolStripMenuItem) As ToolStripButton
        Dim button As New ToolStripButton() With {
            .Name = "palette" & menuItem.Name,
            .Text = menuItem.Text.Replace("&", String.Empty).Replace("...", String.Empty),
            .Image = menuItem.Image,
            .ToolTipText = menuItem.ToolTipText,
            .Enabled = menuItem.Enabled,
            .Tag = menuItem
        }
        PrepareItemForPalette(button)
        AddHandler button.Click, AddressOf LinkedMenuButton_Click
        AddHandler menuItem.EnabledChanged, Sub(sender As Object, e As EventArgs) button.Enabled = menuItem.Enabled
        Return button
    End Function

    Private Sub LinkedMenuButton_Click(ByVal sender As Object, ByVal e As EventArgs)
        Dim button As ToolStripButton = TryCast(sender, ToolStripButton)
        If button Is Nothing Then Return
        Dim menuItem As ToolStripMenuItem = TryCast(button.Tag, ToolStripMenuItem)
        If menuItem IsNot Nothing AndAlso menuItem.Enabled Then menuItem.PerformClick()
    End Sub

    Private Sub PrepareItemForPalette(ByVal item As ToolStripItem)
        Dim button As ToolStripButton = TryCast(item, ToolStripButton)
        If button IsNot Nothing Then
            button.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
            button.TextImageRelation = TextImageRelation.ImageBeforeText
            button.TextAlign = ContentAlignment.MiddleLeft
            button.ImageAlign = ContentAlignment.MiddleLeft
            button.AutoSize = False
            button.Margin = New Padding(2)
            Return
        End If

        Dim label As ToolStripLabel = TryCast(item, ToolStripLabel)
        If label IsNot Nothing Then
            label.AutoSize = False
            label.TextAlign = ContentAlignment.MiddleLeft
            label.Margin = New Padding(3, 4, 3, 0)
            Return
        End If

        Dim combo As ToolStripComboBox = TryCast(item, ToolStripComboBox)
        If combo IsNot Nothing Then
            combo.AutoSize = False
            combo.Margin = New Padding(3, 1, 3, 5)
        End If
    End Sub

    Private Sub SelectButton_Click(ByVal sender As Object, ByVal e As EventArgs)
        SetSelectionActive()
        RaiseEvent SelectionToolSelected(Me, EventArgs.Empty)
    End Sub

    Private Sub SetSelectionActive()
        selectButton.BackColor = SystemColors.Highlight
        selectButton.ForeColor = SystemColors.HighlightText
        selectButton.FlatAppearance.BorderColor = SystemColors.Highlight
        selectButton.FlatAppearance.BorderSize = 2
    End Sub

    Private Sub Palette_Load(ByVal sender As Object, ByVal e As EventArgs)
        AppThemeManager.ApplyToForm(Me)
        AttachOwnerEvents()
        If Not WindowStateManager.HasSavedState(Me) Then DockTo(DockStyle.Left)
        ApplyResponsiveLayout()
    End Sub

    Private Sub ApplyResponsiveLayout()
        Dim scrollbarAllowance As Integer = If(contentPanel.VerticalScroll.Visible, SystemInformation.VerticalScrollBarWidth, 0)
        Dim availableWidth As Integer = Math.Max(180, Me.ClientSize.Width - contentPanel.Padding.Horizontal - scrollbarAllowance - 2)
        selectButton.Width = availableWidth

        For Each control As Control In contentPanel.Controls
            Dim label As Label = TryCast(control, Label)
            If label IsNot Nothing Then
                label.Width = availableWidth
                Continue For
            End If

            Dim strip As ToolStrip = TryCast(control, ToolStrip)
            If strip Is Nothing Then Continue For
            LayoutCommandStrip(strip, availableWidth)
        Next
    End Sub

    Private Sub LayoutCommandStrip(ByVal strip As ToolStrip, ByVal availableWidth As Integer)
        strip.Width = availableWidth

        Dim useTwoColumns As Boolean = (dockSide = DockStyle.None AndAlso availableWidth >= 310)
        Dim buttonWidth As Integer = If(useTwoColumns,
                                        Math.Max(138, CInt(Math.Floor((availableWidth - 14) / 2.0))),
                                        Math.Max(170, availableWidth - 8))
        Dim fullWidth As Integer = Math.Max(170, availableWidth - 8)
        Dim rows As Integer = 0
        Dim buttonsOnCurrentRow As Integer = 0

        For Each item As ToolStripItem In strip.Items
            Dim button As ToolStripButton = TryCast(item, ToolStripButton)
            If button IsNot Nothing Then
                button.Width = buttonWidth
                button.Height = 34
                If useTwoColumns Then
                    If buttonsOnCurrentRow = 0 Then rows += 1
                    buttonsOnCurrentRow = (buttonsOnCurrentRow + 1) Mod 2
                Else
                    rows += 1
                End If
                Continue For
            End If

            Dim label As ToolStripLabel = TryCast(item, ToolStripLabel)
            If label IsNot Nothing Then
                If useTwoColumns AndAlso buttonsOnCurrentRow <> 0 Then
                    buttonsOnCurrentRow = 0
                End If
                label.Width = fullWidth
                label.Height = 22
                rows += 1
                Continue For
            End If

            Dim combo As ToolStripComboBox = TryCast(item, ToolStripComboBox)
            If combo IsNot Nothing Then
                If useTwoColumns AndAlso buttonsOnCurrentRow <> 0 Then
                    buttonsOnCurrentRow = 0
                End If
                combo.Width = fullWidth
                rows += 1
                Continue For
            End If

            rows += 1
        Next

        strip.Height = Math.Max(38, rows * 38 + 7)
    End Sub

    Private Sub AttachOwnerEvents()
        If Me.Owner Is Nothing Then Return
        AddHandler Me.Owner.LocationChanged, AddressOf OwnerBoundsChanged
        AddHandler Me.Owner.SizeChanged, AddressOf OwnerBoundsChanged
        AddHandler Me.Owner.FormClosed, AddressOf OwnerClosed
    End Sub

    Private Sub DetachOwnerEvents()
        If Me.Owner Is Nothing Then Return
        RemoveHandler Me.Owner.LocationChanged, AddressOf OwnerBoundsChanged
        RemoveHandler Me.Owner.SizeChanged, AddressOf OwnerBoundsChanged
        RemoveHandler Me.Owner.FormClosed, AddressOf OwnerClosed
    End Sub

    Private Sub OwnerBoundsChanged(ByVal sender As Object, ByVal e As EventArgs)
        If dockSide = DockStyle.None OrElse Me.Owner Is Nothing OrElse Me.Owner.WindowState = FormWindowState.Minimized Then Return
        ownerMoveInProgress = True
        Try
            ApplyDockedBounds()
        Finally
            ownerMoveInProgress = False
        End Try
    End Sub

    Private Sub OwnerClosed(ByVal sender As Object, ByVal e As FormClosedEventArgs)
        Me.Close()
    End Sub

    Private Sub Palette_LocationChanged(ByVal sender As Object, ByVal e As EventArgs)
        If ownerMoveInProgress OrElse Me.Owner Is Nothing OrElse Me.WindowState <> FormWindowState.Normal Then Return

        Dim ownerBounds As Rectangle = Me.Owner.Bounds
        Dim nearLeft As Boolean = Math.Abs(Me.Right - ownerBounds.Left) <= DockSnapDistance
        Dim nearRight As Boolean = Math.Abs(Me.Left - ownerBounds.Right) <= DockSnapDistance
        Dim verticallyNear As Boolean = Me.Bottom >= ownerBounds.Top AndAlso Me.Top <= ownerBounds.Bottom

        If verticallyNear AndAlso nearLeft Then
            DockTo(DockStyle.Left)
        ElseIf verticallyNear AndAlso nearRight Then
            DockTo(DockStyle.Right)
        ElseIf dockSide <> DockStyle.None Then
            dockSide = DockStyle.None
            Me.MinimumSize = New Size(DockedPaletteWidth, 300)
            If Me.Width < FloatingPaletteWidth Then Me.Width = FloatingPaletteWidth
            ApplyResponsiveLayout()
        End If
    End Sub

    Private Sub Palette_Resize(ByVal sender As Object, ByVal e As EventArgs)
        If dockSide <> DockStyle.None AndAlso Not ownerMoveInProgress Then
            ApplyDockedBounds()
        End If
        ApplyResponsiveLayout()
    End Sub

    Private Sub Palette_DoubleClick(ByVal sender As Object, ByVal e As EventArgs)
        If dockSide = DockStyle.None Then
            DockTo(DockStyle.Left)
        ElseIf dockSide = DockStyle.Left Then
            DockTo(DockStyle.Right)
        Else
            dockSide = DockStyle.None
            Me.Width = FloatingPaletteWidth
            ApplyResponsiveLayout()
        End If
    End Sub

    Private Sub DockTo(ByVal side As DockStyle)
        If Me.Owner Is Nothing Then Return
        dockSide = side
        ApplyDockedBounds()
        ApplyResponsiveLayout()
    End Sub

    Private Sub ApplyDockedBounds()
        If Me.Owner Is Nothing OrElse dockSide = DockStyle.None Then Return

        Dim workArea As Rectangle = Screen.FromControl(Me.Owner).WorkingArea
        Dim ownerBounds As Rectangle = Me.Owner.Bounds
        Dim height As Integer = Math.Min(Math.Max(Me.Height, Me.MinimumSize.Height), workArea.Height)
        Dim y As Integer = Math.Max(workArea.Top, Math.Min(ownerBounds.Top, workArea.Bottom - height))
        Dim x As Integer

        If dockSide = DockStyle.Left Then
            x = Math.Max(workArea.Left, ownerBounds.Left - DockedPaletteWidth)
        Else
            x = Math.Min(workArea.Right - DockedPaletteWidth, ownerBounds.Right)
        End If

        ownerMoveInProgress = True
        Try
            Me.Bounds = New Rectangle(x, y, DockedPaletteWidth, height)
        Finally
            ownerMoveInProgress = False
        End Try
    End Sub

    Private Sub Palette_FormClosed(ByVal sender As Object, ByVal e As FormClosedEventArgs)
        DetachOwnerEvents()
    End Sub
End Class
