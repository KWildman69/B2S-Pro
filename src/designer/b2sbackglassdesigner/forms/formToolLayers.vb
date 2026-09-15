Imports System.Windows.Forms
Imports System.Drawing
Imports System.IO
Imports System.Xml

Imports System.Drawing.Drawing2D

Public Class formToolLayers
    Inherits B2SThemedForm

    Private ReadOnly layers As New ListView()
    Private hoveredLayerIndex As Integer = -1
    Private ReadOnly btnVisible As New Button()
    Private ReadOnly btnLock As New Button()
    Private ReadOnly btnRename As New Button()
    Private ReadOnly btnFront As New Button()
    Private ReadOnly btnBack As New Button()
    Private ReadOnly opacitySlider As New TrackBar()
    Private ReadOnly opacityNumber As New NumericUpDown()
    Private ReadOnly opacityLabel As New Label()
    Private ReadOnly layerNumber As New NumericUpDown()
    Private ReadOnly btnSetLayer As New Button()
    Private ReadOnly filterBox As New ComboBox()
    Private ReadOnly countLabel As New Label()
    Private ReadOnly chkShowLights As New CategoryToggleCheckBox()
    Private ReadOnly chkShowFlashers As New CategoryToggleCheckBox()
    Private ReadOnly chkShowSnippets As New CategoryToggleCheckBox()
    Private ReadOnly layerIcons As New ImageList()
    Private refreshing As Boolean
    Private draggedItems As New List(Of Object)()
    Private layoutWasChanged As Boolean
    Private restoringDedicatedLayout As Boolean
    Private Shared ReadOnly dedicatedLayoutFile As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "B2SBackglassDesigner", "layers-panel-layout.xml")

    Public ReadOnly Property HasSavedLayout As Boolean
        Get
            Return File.Exists(dedicatedLayoutFile)
        End Get
    End Property

    Public Sub New()
        ' New layout key prevents the former oversized saved bounds from being restored.
        Name = "formToolLayersCompactV3"
        WindowStateManager.Attach(Me)
        Text = "Layers"
        Width = 410
        Height = 465
        MinimumSize = New Size(400, 360)
        FormBorderStyle = FormBorderStyle.SizableToolWindow
        ShowInTaskbar = False
        BackColor = Color.FromArgb(191, 205, 219)
        ForeColor = SystemColors.ControlText
        Font = New Font("Tahoma", 8.25F, FontStyle.Regular, GraphicsUnit.Point)

        ConfigureIcons()

        AddHandler Me.Move, AddressOf LayersLayoutChanged
        AddHandler Me.Resize, AddressOf LayersLayoutChanged
        AddHandler Me.ResizeEnd, AddressOf SaveLayersLayoutNow
        AddHandler Me.VisibleChanged, AddressOf LayersVisibilityChanged
        AddHandler Me.FormClosing, AddressOf SaveLayersLayoutNow
        AddHandler Me.Load, AddressOf RestoreDedicatedLayout

        layers.Name = "lvLayers"
        layers.Dock = DockStyle.Fill
        layers.View = View.Details
        layers.FullRowSelect = True
        layers.HideSelection = False
        layers.MultiSelect = True
        layers.AllowDrop = True
        layers.BackColor = SystemColors.Window
        layers.ForeColor = SystemColors.WindowText
        layers.Font = Font
        layers.BorderStyle = BorderStyle.Fixed3D
        layers.SmallImageList = layerIcons
        layers.OwnerDraw = True
        layers.GridLines = False
        layers.Columns.Add("#", 34)
        layers.Columns.Add("Z", 32)
        layers.Columns.Add("Layer", 108)
        layers.Columns.Add("Type", 52)
        layers.Columns.Add("State", 54)
        layers.Columns.Add("Mask", 52)
        AddHandler layers.SelectedIndexChanged, AddressOf LayerSelected
        AddHandler layers.ItemDrag, AddressOf LayerItemDrag
        AddHandler layers.DragEnter, AddressOf LayerDragEnter
        AddHandler layers.DragOver, AddressOf LayerDragOver
        AddHandler layers.DragLeave, AddressOf LayerDragLeave
        AddHandler layers.DragDrop, AddressOf LayerDragDrop
        AddHandler layers.DoubleClick, AddressOf RenameLayer
        AddHandler layers.KeyDown, AddressOf LayersKeyDown
        AddHandler layers.DrawColumnHeader, AddressOf Layers_DrawColumnHeader
        AddHandler layers.DrawItem, AddressOf Layers_DrawItem
        AddHandler layers.DrawSubItem, AddressOf Layers_DrawSubItem
        AddHandler layers.MouseMove, AddressOf Layers_MouseMove
        AddHandler layers.MouseLeave, AddressOf Layers_MouseLeave
        AddHandler layers.ColumnWidthChanged, AddressOf LayersInternalLayoutChanged

        Dim helpLabel As New Label With {
            .Name = "lblLayersHelp",
            .Dock = DockStyle.Top,
            .Height = 23,
            .Padding = New Padding(6, 3, 6, 1),
            .Text = "Front → Back  •  Drag to reorder  •  Ctrl/Shift: multi-select",
            .BackColor = BackColor,
            .ForeColor = ForeColor,
            .Font = Font
        }

        Dim filterPanel As New TableLayoutPanel With {
            .Name = "pnlLayersFilter",
            .Dock = DockStyle.Top,
            .Height = 30,
            .ColumnCount = 3,
            .RowCount = 1,
            .BackColor = BackColor,
            .Padding = New Padding(5, 2, 5, 2)
        }
        filterPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        filterPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 92.0F))
        filterPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 38.0F))
        filterBox.Name = "cmbLayerFilter"
        filterBox.Dock = DockStyle.Fill
        filterBox.DropDownStyle = ComboBoxStyle.DropDownList
        filterBox.Font = Font
        filterBox.Items.AddRange(New Object() {"All", "Images", "Bulbs", "Flashers", "Snippets", "Scores", "Hidden", "Locked"})
        filterBox.SelectedIndex = 0
        countLabel.Dock = DockStyle.Fill
        countLabel.TextAlign = ContentAlignment.MiddleRight
        countLabel.Font = Font
        countLabel.ForeColor = ForeColor
        AddHandler filterBox.SelectedIndexChanged, AddressOf FilterChanged
        filterPanel.Controls.Add(filterBox, 1, 0)
        filterPanel.Controls.Add(countLabel, 2, 0)


        Dim categoryPanel As New FlowLayoutPanel With {
            .Name = "pnlLayerCategories",
            .Dock = DockStyle.Top,
            .Height = 25,
            .AutoSize = False,
            .BackColor = BackColor,
            .Padding = New Padding(6, 2, 3, 1),
            .WrapContents = False
        }
        SetupCategoryCheckBox(chkShowLights, "Lights", Color.Gold, LayerManager.IsLightCategoryVisible(), AddressOf CategoryVisibilityChanged)
        SetupCategoryCheckBox(chkShowFlashers, "Flashers", Color.RoyalBlue, LayerManager.IsFlasherCategoryVisible(), AddressOf CategoryVisibilityChanged)
        SetupCategoryCheckBox(chkShowSnippets, "Snippets", Color.Red, LayerManager.IsSnippetCategoryVisible(), AddressOf CategoryVisibilityChanged)
        categoryPanel.Controls.AddRange(New Control() {chkShowLights, chkShowFlashers, chkShowSnippets})

        Dim buttons As New TableLayoutPanel With {
            .Name = "pnlLayerCommands",
            .Dock = DockStyle.Top,
            .Height = 66,
            .AutoSize = False,
            .BackColor = BackColor,
            .Padding = New Padding(5, 3, 5, 3),
            .ColumnCount = 6,
            .RowCount = 2,
            .GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        }
        ' Two tiers keep every caption readable at the window's minimum width.
        ' The themed pill painter reserves horizontal padding, so five buttons
        ' must not be squeezed into one row.
        For column As Integer = 0 To 5
            buttons.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F / 6.0F))
        Next
        buttons.RowStyles.Add(New RowStyle(SizeType.Percent, 50.0F))
        buttons.RowStyles.Add(New RowStyle(SizeType.Percent, 50.0F))
        SetupButton(btnFront, "To Front", AddressOf BringFront)
        SetupButton(btnBack, "To Back", AddressOf SendBack)
        SetupButton(btnRename, "Rename", AddressOf RenameLayer)
        SetupButton(btnVisible, "Show / Hide", AddressOf ToggleVisible)
        SetupButton(btnLock, "Lock / Unlock", AddressOf ToggleLock)
        Dim commandButtons() As Button = {btnFront, btnBack, btnRename, btnVisible, btnLock}
        For Each commandButton As Button In commandButtons
            commandButton.Dock = DockStyle.Fill
            commandButton.Margin = New Padding(2, 1, 2, 1)
            commandButton.AutoEllipsis = False
        Next
        buttons.Controls.Add(btnFront, 0, 0)
        buttons.SetColumnSpan(btnFront, 2)
        buttons.Controls.Add(btnBack, 2, 0)
        buttons.SetColumnSpan(btnBack, 2)
        buttons.Controls.Add(btnRename, 4, 0)
        buttons.SetColumnSpan(btnRename, 2)
        buttons.Controls.Add(btnVisible, 0, 1)
        buttons.SetColumnSpan(btnVisible, 3)
        buttons.Controls.Add(btnLock, 3, 1)
        buttons.SetColumnSpan(btnLock, 3)

        Dim layerMenu As New ContextMenuStrip() With {.AutoSize = True, .ShowImageMargin = False}
        Dim orderMenu As New ToolStripMenuItem("Layer Order")
        orderMenu.DropDownItems.Add("Move to Front", Nothing, AddressOf BringFront)
        orderMenu.DropDownItems.Add("Move to Back", Nothing, AddressOf SendBack)
        Dim stateMenu As New ToolStripMenuItem("Visibility, Lock && Name")
        stateMenu.DropDownItems.Add("Show / Hide", Nothing, AddressOf ToggleVisible)
        stateMenu.DropDownItems.Add("Lock / Unlock", Nothing, AddressOf ToggleLock)
        stateMenu.DropDownItems.Add("Rename...", Nothing, AddressOf RenameLayer)
        Dim editMenu As New ToolStripMenuItem("Edit Selected Object")
        editMenu.DropDownItems.Add("Edit Snippet / Animation Mask...", Nothing, AddressOf EditSnippetMask)
        editMenu.DropDownItems.Add("Clear Snippet / Animation Mask", Nothing, AddressOf ClearSnippetMask)
        editMenu.DropDownItems.Add(New ToolStripSeparator())
        editMenu.DropDownItems.Add("Edit Entry Motion Path...", Nothing, AddressOf EditMotionPath)
        editMenu.DropDownItems.Add("Edit Exit Motion Path...", Nothing, AddressOf EditExitMotionPath)
        editMenu.DropDownItems.Add("Edit Pivot Animation...", Nothing, AddressOf EditPivotAnimation)
        editMenu.DropDownItems.Add("Edit Physics Boundaries...", Nothing, AddressOf EditPhysicsBoundaries)
        editMenu.DropDownItems.Add("Create Trough Animation...", Nothing, AddressOf CreateTroughAnimation)
        Dim maskMenu As New ToolStripMenuItem("Mask Placement")
        maskMenu.DropDownItems.Add("Behind Mask", Nothing, AddressOf SetBehindMask)
        maskMenu.DropDownItems.Add("In Front of Mask", Nothing, AddressOf SetInFrontMask)
        Dim canvasMenu As New ToolStripMenuItem("Canvas Placement")
        canvasMenu.DropDownItems.Add("Snippet Behind Canvas", Nothing, AddressOf SetSnippetBehindCanvas)
        canvasMenu.DropDownItems.Add("Snippet In Front of Canvas", Nothing, AddressOf SetSnippetInFrontOfCanvas)
        canvasMenu.DropDownItems.Add(New ToolStripSeparator())
        canvasMenu.DropDownItems.Add("Light Behind Canvas", Nothing, AddressOf SetLightBehindCanvas)
        canvasMenu.DropDownItems.Add("Light In Front of Canvas", Nothing, AddressOf SetLightInFrontOfCanvas)
        canvasMenu.DropDownItems.Add(New ToolStripSeparator())
        canvasMenu.DropDownItems.Add("Reel / LED Behind Canvas", Nothing, AddressOf SetScoreBehindCanvas)
        canvasMenu.DropDownItems.Add("Reel / LED In Front of Canvas", Nothing, AddressOf SetScoreInFrontOfCanvas)
        layerMenu.Items.AddRange(New ToolStripItem() {editMenu, orderMenu, stateMenu, maskMenu, canvasMenu, New ToolStripSeparator(),
                                                       New ToolStripMenuItem("Delete Selected Layer", Nothing, AddressOf DeleteLayer)})
        layers.ContextMenuStrip = layerMenu

        Dim layerPanel As New FlowLayoutPanel With {.Name = "pnlLayerPosition", .Dock = DockStyle.Bottom, .Height = 31, .BackColor = BackColor, .Padding = New Padding(5, 2, 5, 2), .WrapContents = False}
        Dim layerLabel As New Label With {.Text = "Z layer:", .AutoSize = True, .Margin = New Padding(0, 6, 4, 0), .ForeColor = ForeColor, .Font = Font}
        layerNumber.Minimum = Integer.MinValue
        layerNumber.Maximum = Integer.MaxValue
        layerNumber.Width = 72
        layerNumber.Enabled = False
        SetupButton(btnSetLayer, "Set Layer", AddressOf SetExactLayer)
        btnSetLayer.Enabled = False
        layerPanel.Controls.Add(layerLabel)
        layerPanel.Controls.Add(layerNumber)
        layerPanel.Controls.Add(btnSetLayer)

        Dim opacityPanel As New TableLayoutPanel With {.Name = "pnlLayerOpacity", .Dock = DockStyle.Bottom, .Height = 37, .BackColor = BackColor, .Padding = New Padding(5, 1, 5, 1), .ColumnCount = 3, .RowCount = 1}
        opacityPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 82.0F))
        opacityPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        opacityPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 62.0F))
        opacityLabel.Text = "Opacity (%)"
        opacityLabel.ForeColor = ForeColor
        opacityLabel.Font = Font
        opacityLabel.Dock = DockStyle.Fill
        opacityLabel.TextAlign = ContentAlignment.MiddleLeft
        opacitySlider.Dock = DockStyle.Fill
        opacitySlider.Minimum = 0
        opacitySlider.Maximum = 100
        opacitySlider.TickFrequency = 10
        opacitySlider.Value = 100
        TrackBarNumericLink.Bind(opacitySlider, opacityNumber)
        AddHandler opacitySlider.ValueChanged, AddressOf OpacityChanged
        opacityPanel.Controls.Add(opacityLabel, 0, 0)
        opacityPanel.Controls.Add(opacitySlider, 1, 0)
        opacityNumber.Dock = DockStyle.Fill
        opacityNumber.Margin = New Padding(2, 5, 0, 5)
        opacityPanel.Controls.Add(opacityNumber, 2, 0)

        Controls.Add(layers)
        Controls.Add(buttons)
        Controls.Add(categoryPanel)
        Controls.Add(filterPanel)
        Controls.Add(helpLabel)
        Controls.Add(layerPanel)
        Controls.Add(opacityPanel)
    End Sub

    Private Sub RestoreDedicatedLayout(ByVal sender As Object, ByVal e As EventArgs)
        If Not File.Exists(dedicatedLayoutFile) Then Return
        Try
            restoringDedicatedLayout = True
            refreshing = True
            Dim document As New XmlDocument()
            document.Load(dedicatedLayoutFile)
            Dim root As XmlElement = TryCast(document.DocumentElement, XmlElement)
            If root Is Nothing Then Return
            Dim savedBounds As New Rectangle(ReadLayoutInt(root, "x", Left), ReadLayoutInt(root, "y", Top), ReadLayoutInt(root, "width", Width), ReadLayoutInt(root, "height", Height))
            Dim work As Rectangle = Screen.FromRectangle(savedBounds).WorkingArea
            savedBounds.Width = Math.Max(MinimumSize.Width, Math.Min(work.Width, savedBounds.Width))
            savedBounds.Height = Math.Max(MinimumSize.Height, Math.Min(work.Height, savedBounds.Height))
            savedBounds.X = Math.Max(work.Left, Math.Min(savedBounds.X, work.Right - savedBounds.Width))
            savedBounds.Y = Math.Max(work.Top, Math.Min(savedBounds.Y, work.Bottom - savedBounds.Height))
            StartPosition = FormStartPosition.Manual
            Bounds = savedBounds
            filterBox.SelectedIndex = Math.Max(0, Math.Min(filterBox.Items.Count - 1, ReadLayoutInt(root, "filter", 0)))
            chkShowLights.Checked = ReadLayoutBool(root, "lights", True)
            chkShowFlashers.Checked = ReadLayoutBool(root, "flashers", True)
            chkShowSnippets.Checked = ReadLayoutBool(root, "snippets", True)
            LayerManager.SetLightCategoryVisible(chkShowLights.Checked)
            LayerManager.SetFlasherCategoryVisible(chkShowFlashers.Checked)
            LayerManager.SetSnippetCategoryVisible(chkShowSnippets.Checked)
            For index As Integer = 0 To layers.Columns.Count - 1
                layers.Columns(index).Width = Math.Max(20, ReadLayoutInt(root, "column" & index.ToString(), layers.Columns(index).Width))
            Next
        Catch
        Finally
            refreshing = False
            restoringDedicatedLayout = False
            layoutWasChanged = False
            If Backglass.currentTabPage IsNot Nothing Then RefreshLayers()
        End Try
    End Sub

    Private Sub SaveDedicatedLayout()
        If restoringDedicatedLayout OrElse Not IsHandleCreated Then Return
        Try
            Directory.CreateDirectory(Path.GetDirectoryName(dedicatedLayoutFile))
            Dim document As New XmlDocument()
            Dim root As XmlElement = document.CreateElement("layers")
            document.AppendChild(root)
            Dim savedBounds As Rectangle = If(WindowState = FormWindowState.Normal, Bounds, RestoreBounds)
            root.SetAttribute("x", savedBounds.X.ToString()) : root.SetAttribute("y", savedBounds.Y.ToString())
            root.SetAttribute("width", savedBounds.Width.ToString()) : root.SetAttribute("height", savedBounds.Height.ToString())
            root.SetAttribute("filter", filterBox.SelectedIndex.ToString())
            root.SetAttribute("lights", chkShowLights.Checked.ToString()) : root.SetAttribute("flashers", chkShowFlashers.Checked.ToString()) : root.SetAttribute("snippets", chkShowSnippets.Checked.ToString())
            For index As Integer = 0 To layers.Columns.Count - 1
                root.SetAttribute("column" & index.ToString(), layers.Columns(index).Width.ToString())
            Next
            document.Save(dedicatedLayoutFile)
        Catch
        End Try
    End Sub

    Private Shared Function ReadLayoutInt(ByVal root As XmlElement, ByVal name As String, ByVal fallback As Integer) As Integer
        Dim value As Integer
        If Integer.TryParse(root.GetAttribute(name), value) Then Return value
        Return fallback
    End Function

    Private Shared Function ReadLayoutBool(ByVal root As XmlElement, ByVal name As String, ByVal fallback As Boolean) As Boolean
        Dim value As Boolean
        If Boolean.TryParse(root.GetAttribute(name), value) Then Return value
        Return fallback
    End Function

    Private Sub LayersInternalLayoutChanged(ByVal sender As Object, ByVal e As EventArgs)
        If Not restoringDedicatedLayout Then layoutWasChanged = True
    End Sub

    Private Sub ConfigureIcons()
        layerIcons.ImageSize = New Size(22, 22)
        layerIcons.ColorDepth = ColorDepth.Depth32Bit
        layerIcons.Images.Add("Bulb", CreateTypeIcon("B", Color.Goldenrod))
        layerIcons.Images.Add("Lamp", layerIcons.Images("Bulb"))
        layerIcons.Images.Add("Blinker", CreateTypeIcon("B", Color.DarkOrange))
        layerIcons.Images.Add("Snippet", CreateTypeIcon("S", Color.Red))
        layerIcons.Images.Add("Score", CreateTypeIcon("#", Color.SeaGreen))
        layerIcons.Images.Add("Reel", layerIcons.Images("Score"))
        layerIcons.Images.Add("LED", CreateTypeIcon("7", Color.SeaGreen))
        layerIcons.Images.Add("Image", CreateTypeIcon("I", Color.MediumPurple))
        layerIcons.Images.Add("Canvas", CreateTypeIcon("C", Color.DimGray))
        layerIcons.Images.Add("Flasher", CreateTypeIcon("F", Color.RoyalBlue))
    End Sub

    Private Function CreateTypeIcon(letter As String, fillColor As Color) As Bitmap
        Dim image As New Bitmap(22, 22)
        Using graphics As Graphics = Graphics.FromImage(image)
            graphics.SmoothingMode = SmoothingMode.AntiAlias
            graphics.Clear(Color.Transparent)
            Dim bounds As New Rectangle(2, 2, 18, 18)
            Using glow As New Pen(Color.FromArgb(95, fillColor), 4.0F)
                graphics.DrawEllipse(glow, bounds)
            End Using
            Using brush As New LinearGradientBrush(bounds, Color.FromArgb(230, fillColor), Color.FromArgb(72, fillColor), LinearGradientMode.Vertical)
                graphics.FillEllipse(brush, bounds)
            End Using
            Using pen As New Pen(Color.FromArgb(235, fillColor), 1.0F)
                graphics.DrawEllipse(pen, bounds)
            End Using
            Using textBrush As New SolidBrush(Color.White), iconFont As New Font("Segoe UI", 9.0F, FontStyle.Bold, GraphicsUnit.Point)
                Dim textSize As SizeF = graphics.MeasureString(letter, iconFont)
                graphics.DrawString(letter, iconFont, textBrush, (22.0F - textSize.Width) / 2.0F, (22.0F - textSize.Height) / 2.0F - 1.0F)
            End Using
        End Using
        Return image
    End Function

    Private Sub SetupCategoryCheckBox(checkBox As CheckBox, caption As String, accentColor As Color, initialValue As Boolean, handler As EventHandler)
        checkBox.Name = "chkLayer" & caption
        checkBox.Text = caption
        checkBox.Checked = initialValue
        checkBox.AutoSize = True
        checkBox.ForeColor = accentColor
        checkBox.BackColor = BackColor
        checkBox.Font = New Font(Font, FontStyle.Bold)
        checkBox.Margin = New Padding(0, 0, 10, 0)
        AddHandler checkBox.CheckedChanged, handler
    End Sub

    Private Class CategoryToggleCheckBox
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

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            MyBase.OnPaint(e)
            AppThemeManager.PaintStateCheckBox(Me, e)
        End Sub
    End Class

    Private Sub CategoryVisibilityChanged(sender As Object, e As EventArgs)
        If refreshing Then Return
        layoutWasChanged = True
        LayerManager.SetLightCategoryVisible(chkShowLights.Checked)
        LayerManager.SetFlasherCategoryVisible(chkShowFlashers.Checked)
        LayerManager.SetSnippetCategoryVisible(chkShowSnippets.Checked)
        RefreshLayers()
        If Backglass.currentTabPage IsNot Nothing Then Backglass.currentTabPage.Invalidate()
    End Sub

    Private Sub SetupButton(button As Button, caption As String, handler As EventHandler)
        button.Name = "btnLayer" & caption.Replace("/", String.Empty).Replace(" ", String.Empty)
        button.Text = caption
        button.AutoSize = False
        button.Height = 26
        button.FlatStyle = FlatStyle.Flat
        button.BackColor = BackColor
        button.ForeColor = ForeColor
        button.Font = Font
        button.UseVisualStyleBackColor = False
        AddHandler button.Click, handler
    End Sub

    Private Sub Layers_DrawColumnHeader(sender As Object, e As DrawListViewColumnHeaderEventArgs)
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
        Using fill As New LinearGradientBrush(e.Bounds,
                                               Color.FromArgb(39, 17, 58),
                                               Color.FromArgb(5, 7, 12),
                                               LinearGradientMode.Vertical)
            e.Graphics.FillRectangle(fill, e.Bounds)
        End Using
        Using border As New Pen(Color.FromArgb(205, 190, 82, 255), 1.0F)
            e.Graphics.DrawLine(border, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1)
        End Using
        Dim textBounds As Rectangle = Rectangle.Inflate(e.Bounds, -7, 0)
        Using headerFont As New Font("Segoe UI Semibold", 8.0F, FontStyle.Bold)
            TextRenderer.DrawText(e.Graphics,
                                  e.Header.Text.ToUpperInvariant(),
                                  headerFont,
                                  textBounds,
                                  Color.FromArgb(232, 207, 255),
                                  TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis)
        End Using
    End Sub

    Private Sub Layers_MouseMove(sender As Object, e As MouseEventArgs)
        Dim hovered As ListViewItem = layers.GetItemAt(e.X, e.Y)
        Dim newIndex As Integer = If(hovered Is Nothing, -1, hovered.Index)
        If newIndex = hoveredLayerIndex Then
            If hovered IsNot Nothing Then layers.Invalidate(hovered.Bounds)
            Return
        End If

        Dim oldIndex As Integer = hoveredLayerIndex
        hoveredLayerIndex = newIndex
        If oldIndex >= 0 AndAlso oldIndex < layers.Items.Count Then layers.Invalidate(layers.Items(oldIndex).Bounds)
        If newIndex >= 0 Then layers.Invalidate(layers.Items(newIndex).Bounds)
    End Sub

    Private Sub Layers_MouseLeave(sender As Object, e As EventArgs)
        hoveredLayerIndex = -1
        layers.Invalidate()
    End Sub

    Private Sub Layers_DrawItem(sender As Object, e As DrawListViewItemEventArgs)
        If layers.View <> View.Details Then
            e.DrawDefault = True
            Return
        End If

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
        Dim typeName As String = If(e.Item.SubItems.Count > 3, e.Item.SubItems(3).Text, String.Empty)
        Dim rowBack As Color = LayerRowBackColor(e.Item, typeName)
        Dim cardBounds As Rectangle = Rectangle.Inflate(e.Bounds, -1, -1)
        If cardBounds.Width < 4 OrElse cardBounds.Height < 4 Then Return
        Using path As GraphicsPath = LayerRowPath(cardBounds, 6)
            Using shadow As New Pen(Color.FromArgb(175, 0, 0, 0), 3.0F)
                Dim shadowBounds As Rectangle = cardBounds
                shadowBounds.Offset(0, 1)
                Using shadowPath As GraphicsPath = LayerRowPath(shadowBounds, 6)
                    e.Graphics.DrawPath(shadow, shadowPath)
                End Using
            End Using
            Using fill As New LinearGradientBrush(cardBounds,
                                                   Color.FromArgb(Math.Min(255, rowBack.R + 14), Math.Min(255, rowBack.G + 14), Math.Min(255, rowBack.B + 18)),
                                                   rowBack,
                                                   LinearGradientMode.Vertical)
                e.Graphics.FillPath(fill, path)
            End Using
            Dim accent As Color = If(e.Item.Selected, Color.FromArgb(214, 93, 255), LayerCellColor(3, typeName, String.Empty))
            Using glow As New Pen(Color.FromArgb(If(e.Item.Selected, 150, 55), accent), If(e.Item.Selected, 2.0F, 1.0F))
                e.Graphics.DrawPath(glow, path)
            End Using
            Using highlight As New Pen(Color.FromArgb(80, 235, 240, 255), 1.0F)
                e.Graphics.DrawLine(highlight, cardBounds.Left + 7, cardBounds.Top + 1, cardBounds.Right - 7, cardBounds.Top + 1)
            End Using
        End Using
    End Sub

    Private Sub Layers_DrawSubItem(sender As Object, e As DrawListViewSubItemEventArgs)
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
        Dim selected As Boolean = e.Item.Selected
        Dim typeName As String = If(e.Item.SubItems.Count > 3, e.Item.SubItems(3).Text, String.Empty)
        Dim stateText As String = If(e.Item.SubItems.Count > 4, e.Item.SubItems(4).Text, String.Empty)
        Dim rowBack As Color = LayerRowBackColor(e.Item, typeName)

        Dim textColor As Color = If(selected, Color.White, LayerCellColor(e.ColumnIndex, typeName, stateText))
        Dim textBounds As Rectangle = Rectangle.Inflate(e.Bounds, -6, 0)
        If e.ColumnIndex = 0 AndAlso layers.SmallImageList IsNot Nothing AndAlso e.Item.ImageIndex >= 0 Then
            Dim icon As Image = layers.SmallImageList.Images(e.Item.ImageIndex)
            Dim imageY As Integer = e.Bounds.Top + Math.Max(0, (e.Bounds.Height - icon.Height) \ 2)
            e.Graphics.DrawImage(icon, e.Bounds.Left + 4, imageY, icon.Width, icon.Height)
            textBounds.X += icon.Width + 5
            textBounds.Width = Math.Max(1, textBounds.Width - icon.Width - 5)
        End If

        TextRenderer.DrawText(e.Graphics,
                              e.SubItem.Text,
                              layers.Font,
                              textBounds,
                              textColor,
                              TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis)
        If selected AndAlso e.ColumnIndex = layers.Columns.Count - 1 Then
            ControlPaint.DrawFocusRectangle(e.Graphics, e.Item.Bounds, Color.White, rowBack)
        End If
    End Sub

    Private Function LayerRowBackColor(item As ListViewItem, typeName As String) As Color
        If item.Selected Then Return Color.FromArgb(58, 17, 86)
        If typeName.Equals("Snippet", StringComparison.OrdinalIgnoreCase) Then Return Color.FromArgb(47, 6, 16)
        Return If(item.Index Mod 2 = 0, Color.FromArgb(7, 10, 17), Color.FromArgb(2, 4, 8))
    End Function

    Private Function LayerRowPath(bounds As Rectangle, radius As Integer) As GraphicsPath
        Dim path As New GraphicsPath()
        Dim diameter As Integer = Math.Max(2, Math.Min(Math.Min(bounds.Width, bounds.Height), radius * 2))
        Dim arc As New Rectangle(bounds.Left, bounds.Top, diameter, diameter)
        path.AddArc(arc, 180, 90)
        arc.X = bounds.Right - diameter
        path.AddArc(arc, 270, 90)
        arc.Y = bounds.Bottom - diameter
        path.AddArc(arc, 0, 90)
        arc.X = bounds.Left
        path.AddArc(arc, 90, 90)
        path.CloseFigure()
        Return path
    End Function

    Private Function LayerCellColor(columnIndex As Integer, typeName As String, stateText As String) As Color
        If columnIndex = 3 Then
            Select Case typeName.ToLowerInvariant()
                Case "lamp", "bulb"
                    Return Color.FromArgb(255, 190, 61)
                Case "blinker"
                    Return Color.FromArgb(255, 145, 35)
                Case "blinker"
                    Return Color.FromArgb(255, 145, 35)
                Case "flasher"
                    Return Color.FromArgb(77, 166, 255)
                Case "snippet"
                    Return Color.FromArgb(255, 92, 116)
                Case "score", "reel", "led"
                    Return Color.FromArgb(72, 226, 143)
                Case "image"
                    Return Color.FromArgb(205, 112, 255)
                Case "canvas"
                    Return Color.FromArgb(166, 174, 190)
            End Select
        ElseIf columnIndex = 4 AndAlso stateText.Length > 0 Then
            If stateText.IndexOf("Hidden", StringComparison.OrdinalIgnoreCase) >= 0 Then Return Color.FromArgb(255, 92, 116)
            If stateText.IndexOf("Locked", StringComparison.OrdinalIgnoreCase) >= 0 Then Return Color.FromArgb(255, 190, 61)
        End If
        Return Color.FromArgb(224, 230, 242)
    End Function

    Public Sub RefreshLayers()
        refreshing = True
        Dim selected As List(Of Object) = SelectedObjects()
        layers.BeginUpdate()
        layers.Items.Clear()
        If Backglass.currentTabPage IsNot Nothing Then
            AddVisualLayers()
        End If
        layers.EndUpdate()
        For Each item As Object In selected
            SelectObject(item, False)
        Next
        refreshing = False
        countLabel.Text = layers.Items.Count.ToString()
        UpdateButtonState()
    End Sub

    Private Sub AddVisualLayers()
        Dim visualItems As New List(Of Object)()
        If Backglass.currentBulbs IsNot Nothing Then
            For Each bulb As Illumination.BulbInfo In Backglass.currentBulbs
                visualItems.Add(bulb)
            Next
        End If
        If Backglass.currentScores IsNot Nothing Then
            For Each score As ReelAndLED.ScoreInfo In Backglass.currentScores
                visualItems.Add(score)
            Next
        End If

        visualItems.Sort(Function(left As Object, right As Object)
                             Dim result As Integer = GetVisualZ(right).CompareTo(GetVisualZ(left))
                             If result <> 0 Then Return result
                             Return GetStableVisualIndex(left).CompareTo(GetStableVisualIndex(right))
                         End Function)

        Dim stackPosition As Integer = 1
        Dim addedPictureAnimations As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim addedMotionGroups As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        For Each item As Object In visualItems
            If TypeOf item Is Illumination.BulbInfo Then
                Dim bulb As Illumination.BulbInfo = DirectCast(item, Illumination.BulbInfo)
                Dim typeName As String
                Dim pictureAnimationPrefix As String = GetPictureAnimationPrefix(bulb)
                Dim motionGroup As String = GetMotionGroup(bulb)
                If motionGroup.Length > 0 Then
                    If addedMotionGroups.Contains(motionGroup) Then Continue For
                    addedMotionGroups.Add(motionGroup)
                    typeName = "Motion Group"
                ElseIf pictureAnimationPrefix.Length > 0 Then
                    If addedPictureAnimations.Contains(pictureAnimationPrefix) Then Continue For
                    addedPictureAnimations.Add(pictureAnimationPrefix)
                    typeName = "Picture Animation"
                ElseIf bulb.IsImageSnippit Then
                    typeName = "Snippet"
                ElseIf bulb.IlluMode = Illumination.eIlluMode.Flasher OrElse bulb.LightPurpose = Illumination.eLightPurpose.Flasher Then
                    typeName = "Flasher"
                ElseIf bulb.BlinkEnabled Then
                    typeName = "Blinker"
                Else
                    typeName = "Lamp"
                End If
                Dim name As String
                If motionGroup.Length > 0 Then
                    name = motionGroup & " (" & MotionGroupMembers(bulb).Count.ToString() & " balls)"
                ElseIf pictureAnimationPrefix.Length > 0 Then
                    name = PictureAnimationDisplayName(pictureAnimationPrefix) & " (" & PictureAnimationMembers(bulb).Count.ToString() & " frames)"
                Else
                    name = If(String.IsNullOrWhiteSpace(bulb.Name), typeName & " " & bulb.ID, bulb.Name)
                End If
                Dim layerState As String = If(bulb.IsImageSnippit,
                                              If(bulb.SnippitInfo.BehindCanvas, "Behind Canvas", "Front"),
                                              If(bulb.LightBehindCanvas, "Behind Canvas", If(Not bulb.GlobalMaskLayerExplicit, "", If(bulb.InFrontOfGlobalMask, "Front", "Behind Mask"))))
                If MatchesFilter(name, typeName, bulb) Then AddLayerRow(bulb, stackPosition.ToString(), bulb.ZOrder.ToString(), name, typeName, layerState)
            ElseIf TypeOf item Is ReelAndLED.ScoreInfo Then
                Dim score As ReelAndLED.ScoreInfo = DirectCast(item, ReelAndLED.ScoreInfo)
                Dim displayType As String = If(IsReelImageRendered(score.ReelType) OrElse IsReelImageDream7(score.ReelType), "LED", "Reel")
                Dim name As String = displayType & " " & score.ID
                If MatchesFilter(name, "Score", score) Then AddLayerRow(score, stackPosition.ToString(), score.ZOrder.ToString(), name, displayType, If(score.BehindCanvas, "Behind Canvas", "Front"))
            End If
            stackPosition += 1
        Next

        If MatchesFilter("Canvas Background", "Canvas", Nothing) Then AddLayerRow(Nothing, "-", "-", "Canvas Background", "Canvas", "Fixed")
    End Sub

    Private Function GetPictureAnimationPrefix(ByVal bulb As Illumination.BulbInfo) As String
        If bulb Is Nothing OrElse String.IsNullOrEmpty(bulb.Name) OrElse
           Not bulb.Name.StartsWith("PA_", StringComparison.OrdinalIgnoreCase) Then Return String.Empty
        Dim separator As Integer = bulb.Name.LastIndexOf("_"c)
        If separator <= 2 OrElse separator >= bulb.Name.Length - 1 Then Return String.Empty
        Dim frameNumber As Integer
        If Not Integer.TryParse(bulb.Name.Substring(separator + 1), frameNumber) Then Return String.Empty
        Return bulb.Name.Substring(0, separator + 1)
    End Function

    Private Function PictureAnimationDisplayName(ByVal prefix As String) As String
        If prefix.Length <= 4 Then Return "Picture Animation"
        Return prefix.Substring(3, prefix.Length - 4).Replace("_"c, " "c)
    End Function

    Private Function GetMotionGroup(ByVal bulb As Illumination.BulbInfo) As String
        If bulb Is Nothing OrElse Not bulb.IsImageSnippit OrElse bulb.SnippitInfo Is Nothing Then Return String.Empty
        Return If(bulb.SnippitInfo.MotionPathSequenceOrder > 0, bulb.SnippitInfo.MotionPathSequenceGroup.Trim(), String.Empty)
    End Function

    Private Function MotionGroupMembers(ByVal representative As Illumination.BulbInfo) As List(Of Illumination.BulbInfo)
        Dim result As New List(Of Illumination.BulbInfo)()
        Dim groupName As String = GetMotionGroup(representative)
        If groupName.Length = 0 OrElse Backglass.currentBulbs Is Nothing Then Return result
        For Each bulb As Illumination.BulbInfo In Backglass.currentBulbs
            If String.Equals(GetMotionGroup(bulb), groupName, StringComparison.OrdinalIgnoreCase) Then result.Add(bulb)
        Next
        result.Sort(Function(left, right) left.SnippitInfo.MotionPathSequenceOrder.CompareTo(right.SnippitInfo.MotionPathSequenceOrder))
        Return result
    End Function

    Private Function PictureAnimationMembers(ByVal representative As Illumination.BulbInfo) As List(Of Illumination.BulbInfo)
        Dim result As New List(Of Illumination.BulbInfo)()
        Dim prefix As String = GetPictureAnimationPrefix(representative)
        If prefix.Length = 0 OrElse Backglass.currentBulbs Is Nothing Then Return result
        For Each bulb As Illumination.BulbInfo In Backglass.currentBulbs
            If bulb.IsImageSnippit AndAlso bulb.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then result.Add(bulb)
        Next
        Return result
    End Function

    Private Function SamePictureAnimation(ByVal left As Object, ByVal right As Object) As Boolean
        Dim leftPrefix As String = GetPictureAnimationPrefix(TryCast(left, Illumination.BulbInfo))
        Return leftPrefix.Length > 0 AndAlso
               String.Equals(leftPrefix, GetPictureAnimationPrefix(TryCast(right, Illumination.BulbInfo)), StringComparison.OrdinalIgnoreCase)
    End Function

    Private Function GetStableVisualIndex(item As Object) As Integer
        If TypeOf item Is Illumination.BulbInfo AndAlso Backglass.currentBulbs IsNot Nothing Then
            Return Backglass.currentBulbs.IndexOf(DirectCast(item, Illumination.BulbInfo))
        End If
        If TypeOf item Is ReelAndLED.ScoreInfo AndAlso Backglass.currentScores IsNot Nothing Then
            Return 1000000 + Backglass.currentScores.IndexOf(DirectCast(item, ReelAndLED.ScoreInfo))
        End If
        Return Integer.MaxValue
    End Function

    Private Function GetVisualZ(item As Object) As Integer
        If TypeOf item Is Illumination.BulbInfo Then Return DirectCast(item, Illumination.BulbInfo).ZOrder
        If TypeOf item Is ReelAndLED.ScoreInfo Then Return DirectCast(item, ReelAndLED.ScoreInfo).ZOrder
        Return Integer.MinValue
    End Function

    Private Sub SetVisualZ(item As Object, value As Integer)
        If TypeOf item Is Illumination.BulbInfo Then
            Dim bulb As Illumination.BulbInfo = DirectCast(item, Illumination.BulbInfo)
            bulb.ZOrder = value
            bulb.IsIlluminatedImageDirty = True
        ElseIf TypeOf item Is ReelAndLED.ScoreInfo Then
            DirectCast(item, ReelAndLED.ScoreInfo).ZOrder = value
        End If
    End Sub

    Private Function MatchesFilter(name As String, typeName As String, item As Object) As Boolean
        Select Case filterBox.Text
            Case "Images"
                Return typeName = "Image" OrElse typeName = "Canvas"
            Case "Bulbs"
                Return typeName = "Bulb" OrElse typeName = "Lamp" OrElse typeName = "Blinker"
            Case "Flashers"
                Return typeName = "Flasher"
            Case "Snippets"
                Return typeName = "Snippet" OrElse typeName = "Picture Animation" OrElse typeName = "Motion Group"
            Case "Scores"
                Return typeName = "Score"
            Case "Hidden"
                Return Not LayerManager.IsVisible(item)
            Case "Locked"
                Return LayerManager.IsLocked(item)
        End Select
        Return True
    End Function

    Private Sub AddLayerRow(item As Object, stackText As String, zText As String, layerName As String, typeName As String, maskText As String)
        Dim state As String = If(typeName = "Canvas", "Fixed", If(LayerManager.IsVisible(item), "", "Hidden"))
        If item IsNot Nothing AndAlso LayerManager.IsLocked(item) Then state = If(state.Length > 0, state & ", ", "") & "Locked"
        Dim row As New ListViewItem(stackText)
        row.ImageKey = If(typeName = "Picture Animation", "Snippet", typeName)
        row.SubItems.Add(zText)
        row.SubItems.Add(layerName)
        row.SubItems.Add(typeName)
        row.SubItems.Add(state)
        row.SubItems.Add(maskText)
        row.Tag = item
        If typeName = "Snippet" OrElse typeName = "Picture Animation" OrElse typeName = "Motion Group" Then
            row.BackColor = Color.Red
            row.ForeColor = Color.White
            For Each subItem As ListViewItem.ListViewSubItem In row.SubItems
                subItem.BackColor = Color.Red
                subItem.ForeColor = Color.White
            Next
        End If
        layers.Items.Add(row)
    End Sub

    Private Sub FilterChanged(sender As Object, e As EventArgs)
        If refreshing Then Return
        layoutWasChanged = True
        RefreshLayers()
    End Sub

    Private Function SelectedObject() As Object
        If layers.SelectedItems.Count = 0 Then Return Nothing
        Return layers.SelectedItems(0).Tag
    End Function

    Private Function SelectedObjects() As List(Of Object)
        Dim result As New List(Of Object)()
        For Each row As ListViewItem In layers.SelectedItems
            If row.Tag IsNot Nothing Then
                Dim members As List(Of Illumination.BulbInfo) = MotionGroupMembers(TryCast(row.Tag, Illumination.BulbInfo))
                If members.Count = 0 Then members = PictureAnimationMembers(TryCast(row.Tag, Illumination.BulbInfo))
                If members.Count > 0 Then
                    For Each member As Illumination.BulbInfo In members
                        result.Add(member)
                    Next
                Else
                    result.Add(row.Tag)
                End If
            End If
        Next
        Return result
    End Function

    Public Sub SelectObject(item As Object, Optional clearExisting As Boolean = True)
        If item Is Nothing OrElse layers Is Nothing OrElse layers.IsDisposed Then Return
        Dim wasRefreshing As Boolean = refreshing
        refreshing = True
        Try
            If clearExisting Then
                For index As Integer = layers.Items.Count - 1 To 0 Step -1
                    layers.Items(index).Selected = False
                Next
            End If
            For index As Integer = 0 To layers.Items.Count - 1
                Dim row As ListViewItem = layers.Items(index)
                If row IsNot Nothing AndAlso row.ListView Is layers AndAlso
                   (Object.ReferenceEquals(row.Tag, item) OrElse SamePictureAnimation(row.Tag, item)) Then
                    row.Selected = True
                    row.Focused = True
                    row.EnsureVisible()
                    Exit For
                End If
            Next
        Finally
            refreshing = wasRefreshing
        End Try
    End Sub

    Private Sub LayerSelected(sender As Object, e As EventArgs)
        If refreshing OrElse Backglass.currentTabPage Is Nothing Then Return
        Dim item As Object = SelectedObject()
        Dim canvasSelection As New List(Of InfoBase)()
        For Each selectedItem As Object In SelectedObjects()
            If TypeOf selectedItem Is InfoBase Then canvasSelection.Add(DirectCast(selectedItem, InfoBase))
        Next
        Backglass.currentTabPage.Mouse.SetSelection(canvasSelection, TryCast(item, InfoBase))
        refreshing = True
        Try
            opacitySlider.Value = LayerManager.GetOpacity(item)
            opacitySlider.Enabled = TypeOf item Is Illumination.BulbInfo AndAlso DirectCast(item, Illumination.BulbInfo).IsImageSnippit
        Finally
            refreshing = False
        End Try
        opacityLabel.Text = If(opacitySlider.Enabled, "Opacity (%)", "Opacity: snippets")
        If TypeOf item Is Illumination.BulbInfo OrElse TypeOf item Is ReelAndLED.ScoreInfo Then
            layerNumber.Value = Math.Max(layerNumber.Minimum, Math.Min(layerNumber.Maximum, CDec(GetVisualZ(item))))
        End If
        UpdateButtonState()
        Backglass.currentTabPage.Invalidate()
    End Sub

    Private Sub ToggleVisible(sender As Object, e As EventArgs)
        Dim selected As List(Of Object) = SelectedObjects()
        If selected.Count = 0 Then Return
        Dim makeVisible As Boolean = False
        For Each item As Object In selected
            If Not LayerManager.IsVisible(item) Then makeVisible = True
        Next
        For Each item As Object In selected
            LayerManager.SetVisible(item, makeVisible)
        Next
        RefreshAll(selected(0))
    End Sub

    Private Sub ToggleLock(sender As Object, e As EventArgs)
        Dim selected As List(Of Object) = SelectedObjects()
        If selected.Count = 0 Then Return
        Dim makeLocked As Boolean = False
        For Each item As Object In selected
            If Not LayerManager.IsLocked(item) Then makeLocked = True
        Next
        For Each item As Object In selected
            LayerManager.SetLocked(item, makeLocked)
        Next
        RefreshAll(selected(0))
    End Sub

    Private Sub RenameLayer(sender As Object, e As EventArgs)
        Dim item As Object = SelectedObject()
        If Not TypeOf item Is Illumination.BulbInfo Then Return
        Dim bulb As Illumination.BulbInfo = DirectCast(item, Illumination.BulbInfo)
        Dim typeName As String = If(bulb.IsImageSnippit, "Snippet", If(bulb.IlluMode = Illumination.eIlluMode.Flasher OrElse bulb.LightPurpose = Illumination.eLightPurpose.Flasher, "Flasher", If(bulb.BlinkEnabled, "Blinker", "Lamp")))
        Dim currentName As String = If(String.IsNullOrWhiteSpace(bulb.Name), typeName & " " & bulb.ID, bulb.Name)
        Dim newName As String = Microsoft.VisualBasic.Interaction.InputBox("Layer name:", "Rename Layer", currentName)
        If String.IsNullOrWhiteSpace(newName) OrElse newName = currentName Then Return
        bulb.Name = newName.Trim()
        If Backglass.currentTabPage IsNot Nothing AndAlso Backglass.currentTabPage.BackglassData IsNot Nothing Then Backglass.currentTabPage.BackglassData.IsDirty = True
        RefreshAll(bulb)
    End Sub

    Private Sub LayersKeyDown(sender As Object, e As KeyEventArgs)
        If e.KeyCode = Keys.F2 Then
            RenameLayer(sender, EventArgs.Empty)
            e.Handled = True
        ElseIf e.KeyCode = Keys.Delete Then
            DeleteLayer(sender, EventArgs.Empty)
            e.Handled = True
        End If
    End Sub

    Private Sub OpacityChanged(sender As Object, e As EventArgs)
        If refreshing Then Return
        Dim item As Object = SelectedObject()
        If item Is Nothing Then Return
        Dim members As List(Of Illumination.BulbInfo) = PictureAnimationMembers(TryCast(item, Illumination.BulbInfo))
        If members.Count > 0 Then
            For Each member As Illumination.BulbInfo In members
                LayerManager.SetOpacity(member, opacitySlider.Value)
            Next
        Else
            LayerManager.SetOpacity(item, opacitySlider.Value)
        End If
        opacityLabel.Text = "Opacity (%)"
        If Backglass.currentTabPage IsNot Nothing Then Backglass.currentTabPage.Invalidate()
    End Sub

    Private Sub BringFront(sender As Object, e As EventArgs)
        Reorder(True)
    End Sub

    Private Sub SendBack(sender As Object, e As EventArgs)
        Reorder(False)
    End Sub

    Private Sub Reorder(front As Boolean)
        Dim selected As List(Of Object) = GetSelectedMovableItemsInVisualOrder()
        If selected.Count = 0 Then Return
        Dim newZ As Integer = If(front, SafeIncrement(GetHighestVisualZ()), SafeDecrement(GetLowestVisualZ()))
        For Each item As Object In selected
            SetVisualZ(item, newZ)
        Next
        NormalizeBulbCollectionOrder()
        MarkDirty()
        RefreshAll(selected(0))
    End Sub

    Private Function GetSelectedMovableItemsInVisualOrder() As List(Of Object)
        Dim result As New List(Of Object)()
        For Each row As ListViewItem In layers.Items
            If row.Selected AndAlso (TypeOf row.Tag Is Illumination.BulbInfo OrElse TypeOf row.Tag Is ReelAndLED.ScoreInfo) Then result.Add(row.Tag)
        Next
        Return result
    End Function

    Private Function GetSelectedBulbsInVisualOrder() As List(Of Illumination.BulbInfo)
        Dim result As New List(Of Illumination.BulbInfo)()
        For Each item As Object In GetSelectedMovableItemsInVisualOrder()
            If TypeOf item Is Illumination.BulbInfo Then result.Add(DirectCast(item, Illumination.BulbInfo))
        Next
        Return result
    End Function

    Private Sub SetExactLayer(sender As Object, e As EventArgs)
        Dim selected As List(Of Object) = GetSelectedMovableItemsInVisualOrder()
        For Each item As Object In selected
            SetVisualZ(item, CInt(layerNumber.Value))
        Next
        If selected.Count > 0 Then
            NormalizeBulbCollectionOrder()
            MarkDirty()
            RefreshAll(selected(0))
        End If
    End Sub

    Private Sub NormalizeBulbCollectionOrder()
        If Backglass.currentBulbs Is Nothing OrElse Backglass.currentBulbs.Count < 2 Then Return
        Dim oldPositions As New Dictionary(Of Illumination.BulbInfo, Integer)()
        For index As Integer = 0 To Backglass.currentBulbs.Count - 1
            oldPositions(Backglass.currentBulbs(index)) = index
        Next
        Backglass.currentBulbs.Sort(Function(left As Illumination.BulbInfo, right As Illumination.BulbInfo)
                                        Dim result As Integer = right.ZOrder.CompareTo(left.ZOrder)
                                        If result <> 0 Then Return result
                                        Return oldPositions(left).CompareTo(oldPositions(right))
                                    End Function)
    End Sub

    Private Function GetHighestVisualZ() As Integer
        Dim value As Integer = 0
        If Backglass.currentBulbs IsNot Nothing Then
            For Each bulb As Illumination.BulbInfo In Backglass.currentBulbs
                value = Math.Max(value, bulb.ZOrder)
            Next
        End If
        If Backglass.currentScores IsNot Nothing Then
            For Each score As ReelAndLED.ScoreInfo In Backglass.currentScores
                value = Math.Max(value, score.ZOrder)
            Next
        End If
        Return value
    End Function

    Private Function GetLowestVisualZ() As Integer
        Dim value As Integer = 0
        If Backglass.currentBulbs IsNot Nothing Then
            For Each bulb As Illumination.BulbInfo In Backglass.currentBulbs
                value = Math.Min(value, bulb.ZOrder)
            Next
        End If
        If Backglass.currentScores IsNot Nothing Then
            For Each score As ReelAndLED.ScoreInfo In Backglass.currentScores
                value = Math.Min(value, score.ZOrder)
            Next
        End If
        Return value
    End Function

    Private Function SafeIncrement(value As Integer) As Integer
        Return If(value = Integer.MaxValue, value, value + 1)
    End Function

    Private Function SafeDecrement(value As Integer) As Integer
        Return If(value = Integer.MinValue, value, value - 1)
    End Function

    Private Sub LayerItemDrag(sender As Object, e As ItemDragEventArgs)
        draggedItems = GetSelectedMovableItemsInVisualOrder()
        Dim row As ListViewItem = TryCast(e.Item, ListViewItem)
        If row Is Nothing OrElse Not (TypeOf row.Tag Is Illumination.BulbInfo OrElse TypeOf row.Tag Is ReelAndLED.ScoreInfo) Then Return
        If draggedItems.Count = 0 Then draggedItems.Add(row.Tag)
        layers.DoDragDrop("B2S_LAYER_ROWS", DragDropEffects.Move)
    End Sub

    Private Sub LayerDragEnter(sender As Object, e As DragEventArgs)
        e.Effect = If(e.Data.GetDataPresent(DataFormats.Text) AndAlso CStr(e.Data.GetData(DataFormats.Text)) = "B2S_LAYER_ROWS", DragDropEffects.Move, DragDropEffects.None)
    End Sub

    Private Sub LayerDragOver(sender As Object, e As DragEventArgs)
        If draggedItems.Count = 0 Then
            layers.InsertionMark.Index = -1
            e.Effect = DragDropEffects.None
            Return
        End If
        Dim clientPoint As Point = layers.PointToClient(New Point(e.X, e.Y))
        Dim target As ListViewItem = layers.GetItemAt(clientPoint.X, clientPoint.Y)
        If target Is Nothing OrElse Not (TypeOf target.Tag Is Illumination.BulbInfo OrElse TypeOf target.Tag Is ReelAndLED.ScoreInfo) Then
            layers.InsertionMark.Index = -1
            e.Effect = DragDropEffects.None
            Return
        End If
        layers.InsertionMark.Index = target.Index
        layers.InsertionMark.AppearsAfterItem = clientPoint.Y > target.Bounds.Top + target.Bounds.Height \ 2
        e.Effect = DragDropEffects.Move
    End Sub

    Private Sub LayerDragLeave(sender As Object, e As EventArgs)
        layers.InsertionMark.Index = -1
    End Sub

    Private Sub LayerDragDrop(sender As Object, e As DragEventArgs)
        If draggedItems.Count = 0 Then Return
        Dim clientPoint As Point = layers.PointToClient(New Point(e.X, e.Y))
        Dim targetRow As ListViewItem = layers.GetItemAt(clientPoint.X, clientPoint.Y)
        If targetRow Is Nothing OrElse Not (TypeOf targetRow.Tag Is Illumination.BulbInfo OrElse TypeOf targetRow.Tag Is ReelAndLED.ScoreInfo) Then Return
        Dim target As Object = targetRow.Tag
        If draggedItems.Contains(target) Then Return
        Dim placeAfter As Boolean = clientPoint.Y > targetRow.Bounds.Top + targetRow.Bounds.Height \ 2
        layers.InsertionMark.Index = -1
        Dim newZ As Integer = If(placeAfter, SafeDecrement(GetVisualZ(target)), SafeIncrement(GetVisualZ(target)))
        For Each item As Object In draggedItems
            SetVisualZ(item, newZ)
        Next
        NormalizeBulbCollectionOrder()
        MarkDirty()
        Dim first As Object = draggedItems(0)
        draggedItems.Clear()
        RefreshAll(first)
    End Sub

    Private Sub UpdateButtonState()
        Dim hasSelection As Boolean = layers.SelectedItems.Count > 0
        Dim hasMovable As Boolean = GetSelectedMovableItemsInVisualOrder().Count > 0
        btnFront.Enabled = hasMovable
        btnBack.Enabled = hasMovable
        btnVisible.Enabled = hasSelection
        btnLock.Enabled = hasSelection
        btnRename.Enabled = layers.SelectedItems.Count = 1 AndAlso TypeOf SelectedObject() Is Illumination.BulbInfo AndAlso
                            GetPictureAnimationPrefix(DirectCast(SelectedObject(), Illumination.BulbInfo)).Length = 0
        layerNumber.Enabled = hasMovable
        btnSetLayer.Enabled = hasMovable
    End Sub

    Private Sub SetBehindMask(sender As Object, e As EventArgs)
        SetMaskLayer(False)
    End Sub

    Private Sub SetInFrontMask(sender As Object, e As EventArgs)
        SetMaskLayer(True)
    End Sub

    Public Sub SelectObjects(ByVal items As System.Collections.Generic.IEnumerable(Of InfoBase))
        If items Is Nothing Then Return
        Dim wasRefreshing As Boolean = refreshing
        refreshing = True
        Try
            For Each row As ListViewItem In layers.Items
                row.Selected = False
            Next
            For Each item As InfoBase In items
                SelectObject(item, False)
            Next
        Finally
            refreshing = wasRefreshing
        End Try
        UpdateButtonState()
    End Sub

    Public Sub SynchronizeSelection(ByVal items As System.Collections.Generic.IEnumerable(Of InfoBase))
        If items Is Nothing Then Return
        Dim snapshot As New List(Of InfoBase)()
        For Each item As InfoBase In items
            If item IsNot Nothing Then snapshot.Add(item)
        Next

        ' Selection changes are frequent and do not change layer membership.
        ' Rebuild only when a newly-created object is not represented yet.
        Dim missingRow As Boolean = False
        For Each item As InfoBase In snapshot
            Dim found As Boolean = False
            For Each row As ListViewItem In layers.Items
                If Object.ReferenceEquals(row.Tag, item) OrElse SamePictureAnimation(row.Tag, item) Then
                    found = True
                    Exit For
                End If
            Next
            If Not found Then
                missingRow = True
                Exit For
            End If
        Next
        If missingRow Then RefreshLayers()
        SelectObjects(snapshot)
    End Sub

    Private Sub LayersLayoutChanged(ByVal sender As Object, ByVal e As EventArgs)
        If Me.Visible AndAlso Me.WindowState = FormWindowState.Normal Then layoutWasChanged = True
    End Sub

    Private Sub SaveLayersLayoutNow(ByVal sender As Object, ByVal e As EventArgs)
        If Not Me.IsHandleCreated OrElse Me.WindowState = FormWindowState.Minimized Then Return
        WindowStateManager.SaveNow(Me)
        SaveDedicatedLayout()
        layoutWasChanged = False
    End Sub

    Private Sub LayersVisibilityChanged(ByVal sender As Object, ByVal e As EventArgs)
        If Not Me.Visible AndAlso layoutWasChanged Then SaveLayersLayoutNow(sender, e)
    End Sub

    Private Sub SetSnippetBehindCanvas(sender As Object, e As EventArgs)
        SetSnippetCanvasLayer(True)
    End Sub

    Private Sub EditSnippetMask(sender As Object, e As EventArgs)
        Dim snippets As New List(Of Illumination.BulbInfo)()
        For Each item As Object In SelectedObjects()
            Dim snippet As Illumination.BulbInfo = TryCast(item, Illumination.BulbInfo)
            If snippet IsNot Nothing AndAlso snippet.IsImageSnippit Then snippets.Add(snippet)
        Next
        If snippets.Count = 0 OrElse Backglass.currentTabPage Is Nothing OrElse
           Backglass.currentTabPage.CurrentPictureBox Is Nothing OrElse
           Backglass.currentTabPage.CurrentPictureBox.Image Is Nothing Then Return

        Dim representative As Illumination.BulbInfo = snippets(0)
        Using editor As New formQuickSelection(representative, Backglass.currentTabPage.CurrentPictureBox.Image)
            If editor.ShowDialog(Me) <> DialogResult.OK Then Return
        End Using

        For Each snippet As Illumination.BulbInfo In snippets
            snippet.SelectionMaskData = representative.SelectionMaskData
            snippet.IsIlluminatedImageDirty = True
        Next
        MarkDirty()
        RefreshAll(representative)
    End Sub

    Private Sub EditPivotAnimation(sender As Object, e As EventArgs)
        Dim snippet As Illumination.BulbInfo = TryCast(SelectedObject(), Illumination.BulbInfo)
        If snippet Is Nothing OrElse Not snippet.IsImageSnippit OrElse snippet.Image Is Nothing Then
            MessageBox.Show(Me, "Select one image snippet in the Layers panel first.", "Pivot Animation", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        If Backglass.currentTabPage Is Nothing OrElse Backglass.currentTabPage.CurrentPictureBox Is Nothing OrElse
           Backglass.currentTabPage.CurrentPictureBox.Image Is Nothing Then Return
        Using editor As New formPivotAnimation(snippet, Backglass.currentTabPage.CurrentPictureBox.Image)
            If editor.ShowDialog(Me) <> DialogResult.OK Then Return
        End Using
        snippet.IsIlluminatedImageDirty = True
        MarkDirty()
        RefreshAll(snippet)
    End Sub

    Private Sub EditPhysicsBoundaries(sender As Object, e As EventArgs)
        Dim ball As Illumination.BulbInfo = TryCast(SelectedObject(), Illumination.BulbInfo)
        If ball Is Nothing OrElse Not ball.IsImageSnippit OrElse ball.Image Is Nothing Then
            MessageBox.Show(Me, "Select the ball image snippet in the Layers panel first.", "Physics Boundary Editor",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        If Backglass.currentTabPage Is Nothing OrElse Backglass.currentTabPage.CurrentPictureBox Is Nothing OrElse
           Backglass.currentTabPage.CurrentPictureBox.Image Is Nothing OrElse Backglass.currentBulbs Is Nothing Then Return

        Using editor As New formPhysicsEditor(ball, Backglass.currentTabPage.CurrentPictureBox.Image, Backglass.currentBulbs)
            If editor.ShowDialog(Me) <> DialogResult.OK Then Return
            ball.SnippitInfo.PhysicsBall = editor.ResultEnabled
            ball.SnippitInfo.MotionPathRollEnabled = editor.ResultRollEnabled
            ball.SnippitInfo.PhysicsFlipperName = editor.ResultFlipperName
            ball.SnippitInfo.PhysicsBounds = editor.ResultBounds
            ball.SnippitInfo.PhysicsGravity = editor.ResultGravity
            ball.SnippitInfo.PhysicsFlipperStrength = editor.ResultFlipperStrength
            ball.SnippitInfo.PhysicsBoundaryBounce = editor.ResultBoundaryBounce
            ball.SnippitInfo.PhysicsFloorPoints.Clear()
            ball.SnippitInfo.PhysicsFloorPoints.AddRange(editor.ResultPoints)
            ball.SnippitInfo.PhysicsBoundaryPaths.Clear()
            For Each path As List(Of PointF) In editor.ResultBoundaryPaths
                ball.SnippitInfo.PhysicsBoundaryPaths.Add(New List(Of PointF)(path))
            Next
            ball.SnippitInfo.PhysicsBoundaryNames.Clear()
            ball.SnippitInfo.PhysicsBoundaryNames.AddRange(editor.ResultBoundaryNames)
            ball.SnippitInfo.PhysicsBoundaryLocks.Clear()
            ball.SnippitInfo.PhysicsBoundaryLocks.AddRange(editor.ResultBoundaryLocks)
            ball.SnippitInfo.PhysicsBoundarySegmentBounces.Clear()
            For Each values As List(Of Single) In editor.ResultBoundarySegmentBounces
                ball.SnippitInfo.PhysicsBoundarySegmentBounces.Add(New List(Of Single)(values))
            Next
            ball.SnippitInfo.PhysicsObstacles.Clear()
            ball.SnippitInfo.PhysicsObstacles.AddRange(editor.ResultObstacles)
            ball.SnippitInfo.PhysicsSwitchZones.Clear()
            ball.SnippitInfo.PhysicsSwitchZones.AddRange(editor.ResultSwitchZones)
            ball.SnippitInfo.PhysicsSwitchIDs.Clear()
            ball.SnippitInfo.PhysicsSwitchIDs.AddRange(editor.ResultSwitchIDs)
            ball.SnippitInfo.PhysicsSwitchAngles.Clear()
            ball.SnippitInfo.PhysicsSwitchAngles.AddRange(editor.ResultSwitchAngles)
            ball.SnippitInfo.PhysicsLauncherEnabled = editor.ResultLauncherEnabled
            ball.SnippitInfo.PhysicsLauncherFollowPivot = editor.ResultLauncherFollowPivot
            ball.SnippitInfo.PhysicsLauncherTriggerType = editor.ResultLauncherTriggerType
            ball.SnippitInfo.PhysicsLauncherTriggerID = editor.ResultLauncherTriggerID
            ball.SnippitInfo.PhysicsLauncherX = editor.ResultLauncherX
            ball.SnippitInfo.PhysicsLauncherY = editor.ResultLauncherY
            ball.SnippitInfo.PhysicsLauncherAngle = editor.ResultLauncherAngle
            ball.SnippitInfo.PhysicsLauncherStrength = editor.ResultLauncherStrength
            ball.SnippitInfo.PhysicsLauncherRandomAngle = editor.ResultLauncherRandomAngle
            ball.SnippitInfo.PhysicsLauncherRandomStrength = editor.ResultLauncherRandomStrength
            ball.SnippitInfo.PhysicsLauncherCaptureRadius = editor.ResultLauncherCaptureRadius
            If editor.ResultLauncherEnabled AndAlso editor.ResultLauncherFollowPivot Then
                ball.Location = New Point(CInt(Math.Round(editor.ResultLauncherX - ball.Size.Width / 2.0F)),
                                          CInt(Math.Round(editor.ResultLauncherY - ball.Size.Height / 2.0F)))
            End If
        End Using
        MarkDirty()
        RefreshAll(ball)
    End Sub

    Private Sub EditMotionPath(sender As Object, e As EventArgs)
        Dim snippet As Illumination.BulbInfo = TryCast(SelectedObject(), Illumination.BulbInfo)
        If snippet Is Nothing OrElse Not snippet.IsImageSnippit OrElse snippet.Image Is Nothing Then
            MessageBox.Show(Me,
                            "Select one image snippet in the Layers panel first.",
                            "Motion Path Test",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information)
            Return
        End If
        If Backglass.currentTabPage Is Nothing OrElse Backglass.currentTabPage.CurrentPictureBox Is Nothing OrElse
           Backglass.currentTabPage.CurrentPictureBox.Image Is Nothing Then Return

        Dim groupMembers As List(Of Illumination.BulbInfo) = MotionGroupMembers(snippet)
        Dim pathSource As Illumination.BulbInfo = If(groupMembers.Count > 1, groupMembers(0), snippet)
        Using editor As New formMotionPathTest(pathSource, Backglass.currentTabPage.CurrentPictureBox.Image)
            If editor.ShowDialog(Me) = DialogResult.OK Then
                If groupMembers.Count > 1 AndAlso Not String.IsNullOrWhiteSpace(snippet.SnippitInfo.MotionPathSequenceGroup) Then
                    For Each member As Illumination.BulbInfo In groupMembers
                        Dim slot As PointF = MotionPathEndpoint(member)
                        member.SnippitInfo.MotionPathPoints.Clear()
                        If member Is pathSource Then
                            member.SnippitInfo.MotionPathPoints.AddRange(editor.ResultPoints)
                        Else
                            member.SnippitInfo.MotionPathPoints.AddRange(BuildEntryPathToSlot(editor.ResultPoints, slot))
                        End If
                        member.SnippitInfo.MotionPathDuration = editor.ResultDuration
                        member.SnippitInfo.MotionPathLoop = editor.ResultLoop
                        member.SnippitInfo.MotionPathSolenoidID = editor.ResultSolenoidID
                        member.SnippitInfo.MotionPathLampID = editor.ResultLampID
                        member.SnippitInfo.MotionPathB2SID = editor.ResultB2SID
                        member.SnippitInfo.MotionPathStopB2SID = editor.ResultStopB2SID
                        member.SnippitInfo.MotionPathResumeB2SID = editor.ResultResumeB2SID
                        member.SnippitInfo.MotionPathQueueTriggers = editor.ResultQueueTriggers
                        member.SnippitInfo.MotionPathRollEnabled = editor.ResultRollEnabled
                        member.SnippitInfo.MotionPathRespawnEnabled = editor.ResultRespawnEnabled
                        member.SnippitInfo.MotionPathRespawnPoint = editor.ResultRespawnStart
                        member.SnippitInfo.MotionPathRespawnDuration = editor.ResultRespawnDuration
                    Next
                    MarkDirty()
                    Return
                End If
                snippet.SnippitInfo.MotionPathPoints.Clear()
                snippet.SnippitInfo.MotionPathPoints.AddRange(editor.ResultPoints)
                snippet.SnippitInfo.MotionPathDuration = editor.ResultDuration
                snippet.SnippitInfo.MotionPathLoop = editor.ResultLoop
                snippet.SnippitInfo.MotionPathSolenoidID = editor.ResultSolenoidID
                snippet.SnippitInfo.MotionPathLampID = editor.ResultLampID
                snippet.SnippitInfo.MotionPathB2SID = editor.ResultB2SID
                snippet.SnippitInfo.MotionPathStopB2SID = editor.ResultStopB2SID
                snippet.SnippitInfo.MotionPathResumeB2SID = editor.ResultResumeB2SID
                snippet.SnippitInfo.MotionPathQueueTriggers = editor.ResultQueueTriggers
                snippet.SnippitInfo.MotionPathRollEnabled = editor.ResultRollEnabled
                snippet.SnippitInfo.MotionPathSequenceGroup = editor.ResultSequenceGroup
                snippet.SnippitInfo.MotionPathSequenceOrder = editor.ResultSequenceOrder
                snippet.SnippitInfo.MotionPathRespawnEnabled = editor.ResultRespawnEnabled
                snippet.SnippitInfo.MotionPathRespawnPoint = editor.ResultRespawnStart
                snippet.SnippitInfo.MotionPathRespawnDuration = editor.ResultRespawnDuration
                MarkDirty()
            End If
        End Using
    End Sub

    Private Sub EditExitMotionPath(sender As Object, e As EventArgs)
        Dim snippet As Illumination.BulbInfo = TryCast(SelectedObject(), Illumination.BulbInfo)
        If snippet Is Nothing OrElse Not snippet.IsImageSnippit OrElse snippet.Image Is Nothing Then
            MessageBox.Show(Me, "Select one image snippet in the Layers panel first.", "Exit Motion Path", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        If Backglass.currentTabPage Is Nothing OrElse Backglass.currentTabPage.CurrentPictureBox Is Nothing OrElse Backglass.currentTabPage.CurrentPictureBox.Image Is Nothing Then Return

        Dim groupMembers As List(Of Illumination.BulbInfo) = MotionGroupMembers(snippet)
        Dim pathSource As Illumination.BulbInfo = If(groupMembers.Count > 1, groupMembers(0), snippet)
        Using editor As New formMotionPathTest(pathSource, Backglass.currentTabPage.CurrentPictureBox.Image, True)
            If editor.ShowDialog(Me) = DialogResult.OK Then
                If groupMembers.Count > 1 AndAlso Not String.IsNullOrWhiteSpace(snippet.SnippitInfo.MotionPathSequenceGroup) Then
                    For Each member As Illumination.BulbInfo In groupMembers
                        Dim exitPath As New List(Of PointF)(editor.ResultPoints)
                        If member IsNot pathSource Then exitPath(0) = MotionPathEndpoint(member)
                        member.SnippitInfo.MotionPathExitPoints.Clear()
                        member.SnippitInfo.MotionPathExitPoints.AddRange(exitPath)
                        member.SnippitInfo.MotionPathExitDuration = editor.ResultDuration
                        member.SnippitInfo.MotionPathRemoveSolenoidID = editor.ResultSolenoidID
                        member.SnippitInfo.MotionPathRemoveLampID = editor.ResultLampID
                        member.SnippitInfo.MotionPathRemoveB2SID = editor.ResultB2SID
                        member.SnippitInfo.MotionPathRollEnabled = editor.ResultRollEnabled
                    Next
                    MarkDirty()
                    Return
                End If
                snippet.SnippitInfo.MotionPathExitPoints.Clear()
                snippet.SnippitInfo.MotionPathExitPoints.AddRange(editor.ResultPoints)
                snippet.SnippitInfo.MotionPathExitDuration = editor.ResultDuration
                snippet.SnippitInfo.MotionPathRemoveSolenoidID = editor.ResultSolenoidID
                snippet.SnippitInfo.MotionPathRemoveLampID = editor.ResultLampID
                snippet.SnippitInfo.MotionPathRemoveB2SID = editor.ResultB2SID
                snippet.SnippitInfo.MotionPathRollEnabled = editor.ResultRollEnabled
                MarkDirty()
            End If
        End Using
    End Sub

    Private Function MotionPathEndpoint(snippet As Illumination.BulbInfo) As PointF
        Dim points As List(Of PointF) = snippet.SnippitInfo.MotionPathPoints
        If points IsNot Nothing AndAlso points.Count > 0 Then Return points(points.Count - 1)
        Return New PointF(snippet.Location.X + snippet.Size.Width / 2.0F,
                          snippet.Location.Y + snippet.Size.Height / 2.0F)
    End Function

    Private Sub CreateTroughAnimation(sender As Object, e As EventArgs)
        Dim template As Illumination.BulbInfo = TryCast(SelectedObject(), Illumination.BulbInfo)
        If template Is Nothing OrElse Not template.IsImageSnippit OrElse template.Image Is Nothing Then
            MessageBox.Show(Me, "Select one ball image snippet in the Layers panel first.", "Create Trough Animation", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        If Backglass.currentTabPage Is Nothing OrElse Backglass.currentTabPage.CurrentPictureBox Is Nothing OrElse Backglass.currentTabPage.CurrentPictureBox.Image Is Nothing Then Return

        Dim existingMembers As List(Of Illumination.BulbInfo) = MotionGroupMembers(template)
        Using wizard As New formTroughWizard(template, Backglass.currentTabPage.CurrentPictureBox.Image, existingMembers)
            If wizard.ShowDialog(Me) <> DialogResult.OK Then Return
            Dim unsafeMessage As String = Nothing
            If Not ValidateTroughRoutes(wizard, template, unsafeMessage) Then
                MessageBox.Show(Me, unsafeMessage, "Create Trough Animation", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If
            Dim created As New List(Of Illumination.BulbInfo)()
            Dim registryNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            If Not String.IsNullOrWhiteSpace(template.Name) Then registryNames.Add(template.Name)
            For Each existing As Illumination.BulbInfo In existingMembers
                If existing IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(existing.Name) Then registryNames.Add(existing.Name)
            Next
            For Each existingName As String In registryNames
                Backglass.currentImages.RemoveByTypeAndName(Images.eImageInfoType.IlluminationSnippits, existingName)
            Next
            ' Regenerating an existing motion group must replace its members,
            ' not append another set. Keep the selected representative as the
            ' shared template and remove the other members through the same
            ' in-memory collections and image registry used by normal editing.
            For Each existing As Illumination.BulbInfo In existingMembers
                If existing Is template Then Continue For
                If existing.ParentForm = eParentForm.DMD Then
                    Backglass.currentTabPage.BackglassData.DMDBulbs.Remove(existing)
                Else
                    Backglass.currentTabPage.BackglassData.Bulbs.Remove(existing)
                End If
            Next
            Dim nextID As Integer = 1
            If Backglass.currentBulbs IsNot Nothing AndAlso Backglass.currentBulbs.Count > 0 Then nextID = Backglass.currentBulbs.Max(Function(item) item.ID) + 1
            For index As Integer = 0 To wizard.BallCount - 1
                Dim t As Single = If(wizard.BallCount <= 1, 0.0F, index / CSng(wizard.BallCount - 1))
                Dim slot As New PointF(wizard.FirstCenter.X + (wizard.LastCenter.X - wizard.FirstCenter.X) * t,
                                       wizard.FirstCenter.Y + (wizard.LastCenter.Y - wizard.FirstCenter.Y) * t)
                Dim ball As Illumination.BulbInfo = If(index = 0, template, CloneTroughSnippet(template, nextID + index - 1))
                ball.Name = wizard.GroupName & " ball " & (index + 1).ToString()
                ball.Image = wizard.BallImage(index)
                ball.Size = wizard.BallSize
                ball.Visible = False
                ball.Location = New Point(CInt(Math.Round(wizard.EntryPath(0).X - ball.Size.Width / 2.0F)), CInt(Math.Round(wizard.EntryPath(0).Y - ball.Size.Height / 2.0F)))
                ApplyTroughSettings(ball, wizard, slot, index + 1)
                If index > 0 Then
                    If ball.ParentForm = eParentForm.DMD Then Backglass.currentTabPage.BackglassData.DMDBulbs.Add(ball) Else Backglass.currentTabPage.BackglassData.Bulbs.Add(ball)
                End If
                Dim imageInfo As New Images.ImageInfo(Images.eImageInfoType.IlluminationSnippits) With {.Text = ball.Name, .Image = ball.Image}
                Backglass.currentImages.Insert(Images.eImageInfoType.Title4IlluminationSnippits, imageInfo)
                created.Add(ball)
            Next
            MarkDirty()
            RefreshAll(created(0), created)
        End Using
    End Sub

    Private Function ValidateTroughRoutes(wizard As formTroughWizard, template As Illumination.BulbInfo, ByRef message As String) As Boolean
        Dim slots As New List(Of PointF)()
        For index As Integer = 0 To wizard.BallCount - 1
            Dim t As Single = If(wizard.BallCount <= 1, 0.0F, index / CSng(wizard.BallCount - 1))
            slots.Add(New PointF(wizard.FirstCenter.X + (wizard.LastCenter.X - wizard.FirstCenter.X) * t,
                                 wizard.FirstCenter.Y + (wizard.LastCenter.Y - wizard.FirstCenter.Y) * t))
        Next

        ' Use the visible ball radius, with a small allowance for transparent image
        ' padding, to reject entry routes that would visibly pass through an
        ' already occupied slot. This runs before an existing group is replaced.
        Dim clearance As Double = Math.Max(4.0, Math.Max(template.Size.Width, template.Size.Height) * 0.45)
        For movingIndex As Integer = 1 To slots.Count - 1
            Dim route As List(Of PointF) = BuildEntryPathToSlot(wizard.EntryPath, slots(movingIndex))
            For occupiedIndex As Integer = 0 To movingIndex - 1
                For pointIndex As Integer = 0 To route.Count - 2
                    If DistanceFromSegment(slots(occupiedIndex), route(pointIndex), route(pointIndex + 1)) < clearance Then
                        message = "Ball " & (movingIndex + 1).ToString() & " would pass through occupied slot " & (occupiedIndex + 1).ToString() & "." & Environment.NewLine & Environment.NewLine &
                                  "Move the FIRST/LAST slot handles away from the entry route, then create the trough again."
                        Return False
                    End If
                Next
            Next
        Next
        Return True
    End Function

    Private Function DistanceFromSegment(point As PointF, segmentStart As PointF, segmentEnd As PointF) As Double
        Dim dx As Double = segmentEnd.X - segmentStart.X
        Dim dy As Double = segmentEnd.Y - segmentStart.Y
        Dim lengthSquared As Double = dx * dx + dy * dy
        If lengthSquared <= 0.0001 Then Return Math.Sqrt(DistanceSquared(point, segmentStart))
        Dim t As Double = ((point.X - segmentStart.X) * dx + (point.Y - segmentStart.Y) * dy) / lengthSquared
        t = Math.Max(0.0, Math.Min(1.0, t))
        Dim projection As New PointF(CSng(segmentStart.X + dx * t), CSng(segmentStart.Y + dy * t))
        Return Math.Sqrt(DistanceSquared(point, projection))
    End Function

    Private Function CloneTroughSnippet(source As Illumination.BulbInfo, id As Integer) As Illumination.BulbInfo
        Dim clone As New Illumination.BulbInfo With {
            .ID = id, .B2SID = source.B2SID, .B2SIDType = source.B2SIDType, .B2SValue = source.B2SValue,
            .RomID = source.RomID, .RomIDType = source.RomIDType, .RomInverted = source.RomInverted,
            .Size = source.Size, .ParentForm = source.ParentForm, .InitialState = source.InitialState,
            .DualMode = source.DualMode, .Intensity = source.Intensity, .LightColor = source.LightColor,
            .DodgeColor = source.DodgeColor, .IlluMode = source.IlluMode, .ZOrder = source.ZOrder,
            .IsImageSnippit = True, .Image = source.Image, .SelectionMaskData = source.SelectionMaskData,
            .SelectionTolerance = source.SelectionTolerance, .SelectionFeather = source.SelectionFeather,
            .InFrontOfGlobalMask = source.InFrontOfGlobalMask, .GlobalMaskLayerExplicit = source.GlobalMaskLayerExplicit
        }
        clone.SnippitInfo.Brightness = source.SnippitInfo.Brightness
        clone.SnippitInfo.BehindCanvas = source.SnippitInfo.BehindCanvas
        clone.SnippitInfo.SnippitType = eSnippitType.StandardImage
        Return clone
    End Function

    Private Sub ApplyTroughSettings(ball As Illumination.BulbInfo, wizard As formTroughWizard, slot As PointF, order As Integer)
        Dim entry As List(Of PointF) = BuildEntryPathToSlot(wizard.EntryPath, slot)
        Dim exitPath As List(Of PointF) = wizard.ExitPath
        exitPath(0) = slot
        With ball.SnippitInfo
            .MotionPathPoints.Clear() : .MotionPathPoints.AddRange(entry)
            .MotionPathExitPoints.Clear() : .MotionPathExitPoints.AddRange(exitPath)
            .MotionPathDuration = wizard.EntryTime : .MotionPathExitDuration = wizard.ExitTime
            .MotionPathSolenoidID = wizard.EntrySolenoidID : .MotionPathLampID = wizard.EntryLampID : .MotionPathB2SID = wizard.EntryB2SID
            .MotionPathStopB2SID = wizard.StopB2SID : .MotionPathResumeB2SID = wizard.ResumeB2SID
            .MotionPathRemoveSolenoidID = wizard.RemoveSolenoidID : .MotionPathRemoveLampID = wizard.RemoveLampID : .MotionPathRemoveB2SID = wizard.RemoveB2SID
            .MotionPathQueueTriggers = True : .MotionPathLoop = False
            .MotionPathRollEnabled = wizard.RollEnabled
            .MotionPathSequenceGroup = wizard.GroupName : .MotionPathSequenceOrder = order
            .MotionPathRespawnEnabled = wizard.RespawnEnabled
            .MotionPathRespawnPoint = wizard.RespawnStart
            .MotionPathRespawnDuration = wizard.RespawnTime
        End With
    End Sub

    Private Function BuildEntryPathToSlot(sourcePath As List(Of PointF), slot As PointF) As List(Of PointF)
        If sourcePath Is Nothing OrElse sourcePath.Count < 2 Then Return New List(Of PointF)()
        Dim bestSegment As Integer = sourcePath.Count - 2
        Dim bestT As Single = 1.0F
        Dim bestDistance As Double = Double.MaxValue
        Dim bestProjection As PointF = sourcePath(sourcePath.Count - 1)
        For index As Integer = 0 To sourcePath.Count - 2
            Dim a As PointF = sourcePath(index)
            Dim b As PointF = sourcePath(index + 1)
            Dim dx As Double = b.X - a.X
            Dim dy As Double = b.Y - a.Y
            Dim lengthSquared As Double = dx * dx + dy * dy
            Dim t As Double = If(lengthSquared <= 0.0001, 0.0, ((slot.X - a.X) * dx + (slot.Y - a.Y) * dy) / lengthSquared)
            t = Math.Max(0.0, Math.Min(1.0, t))
            Dim projection As New PointF(CSng(a.X + dx * t), CSng(a.Y + dy * t))
            Dim px As Double = slot.X - projection.X
            Dim py As Double = slot.Y - projection.Y
            Dim distance As Double = px * px + py * py
            ' On an exact tie prefer the later point along the authored route.
            If distance < bestDistance - 0.001 OrElse (Math.Abs(distance - bestDistance) <= 0.001 AndAlso index >= bestSegment) Then
                bestDistance = distance
                bestSegment = index
                bestT = CSng(t)
                bestProjection = projection
            End If
        Next

        Dim result As New List(Of PointF)()
        For index As Integer = 0 To bestSegment
            result.Add(sourcePath(index))
        Next
        If bestT > 0.001F AndAlso DistanceSquared(result(result.Count - 1), bestProjection) > 0.01 Then result.Add(bestProjection)
        If DistanceSquared(result(result.Count - 1), slot) > 0.01 Then result.Add(slot) Else result(result.Count - 1) = slot
        If result.Count = 1 Then result.Insert(0, sourcePath(0))
        Return result
    End Function

    Private Function DistanceSquared(left As PointF, right As PointF) As Double
        Dim dx As Double = left.X - right.X
        Dim dy As Double = left.Y - right.Y
        Return dx * dx + dy * dy
    End Function

    Private Sub ClearSnippetMask(sender As Object, e As EventArgs)
        Dim changed As Boolean = False
        Dim firstChanged As Illumination.BulbInfo = Nothing
        For Each item As Object In SelectedObjects()
            Dim snippet As Illumination.BulbInfo = TryCast(item, Illumination.BulbInfo)
            If snippet IsNot Nothing AndAlso snippet.IsImageSnippit AndAlso Not String.IsNullOrEmpty(snippet.SelectionMaskData) Then
                snippet.SelectionMaskData = String.Empty
                snippet.IsIlluminatedImageDirty = True
                If firstChanged Is Nothing Then firstChanged = snippet
                changed = True
            End If
        Next
        If changed Then
            MarkDirty()
            RefreshAll(firstChanged)
        End If
    End Sub

    Private Sub SetSnippetInFrontOfCanvas(sender As Object, e As EventArgs)
        SetSnippetCanvasLayer(False)
    End Sub

    Private Sub SetSnippetCanvasLayer(behindCanvas As Boolean)
        Dim changed As Boolean = False
        For Each item As Object In SelectedObjects()
            If TypeOf item Is Illumination.BulbInfo Then
                Dim bulb As Illumination.BulbInfo = DirectCast(item, Illumination.BulbInfo)
                If bulb.IsImageSnippit AndAlso bulb.SnippitInfo.BehindCanvas <> behindCanvas Then
                    bulb.SnippitInfo.BehindCanvas = behindCanvas
                    bulb.IsIlluminatedImageDirty = True
                    changed = True
                End If
            End If
        Next
        If changed Then
            MarkDirty()
            RefreshAll()
        End If
    End Sub

    Private Sub SetLightBehindCanvas(sender As Object, e As EventArgs)
        SetLightCanvasLayer(True)
    End Sub

    Private Sub SetLightInFrontOfCanvas(sender As Object, e As EventArgs)
        SetLightCanvasLayer(False)
    End Sub

    Private Sub SetLightCanvasLayer(behindCanvas As Boolean)
        Dim changed As Boolean = False
        For Each item As Object In SelectedObjects()
            If TypeOf item Is Illumination.BulbInfo Then
                Dim bulb As Illumination.BulbInfo = DirectCast(item, Illumination.BulbInfo)
                If Not bulb.IsImageSnippit AndAlso bulb.LightBehindCanvas <> behindCanvas Then
                    bulb.LightBehindCanvas = behindCanvas
                    bulb.IsIlluminatedImageDirty = True
                    changed = True
                End If
            End If
        Next
        If changed Then
            MarkDirty()
            RefreshAll()
        End If
    End Sub

    Private Sub SetScoreBehindCanvas(sender As Object, e As EventArgs)
        SetScoreCanvasLayer(True)
    End Sub

    Private Sub SetScoreInFrontOfCanvas(sender As Object, e As EventArgs)
        SetScoreCanvasLayer(False)
    End Sub

    Private Sub SetScoreCanvasLayer(behindCanvas As Boolean)
        Dim changed As Boolean = False
        For Each item As Object In SelectedObjects()
            If TypeOf item Is ReelAndLED.ScoreInfo Then
                Dim score As ReelAndLED.ScoreInfo = DirectCast(item, ReelAndLED.ScoreInfo)
                If score.BehindCanvas <> behindCanvas Then
                    score.BehindCanvas = behindCanvas
                    changed = True
                End If
            End If
        Next
        If changed Then
            MarkDirty()
            RefreshAll()
        End If
    End Sub

    Private Sub SetMaskLayer(inFront As Boolean)
        Dim changed As Boolean = False
        For Each item As Object In SelectedObjects()
            If TypeOf item Is Illumination.BulbInfo Then
                Dim bulb As Illumination.BulbInfo = DirectCast(item, Illumination.BulbInfo)
                If Not bulb.GlobalMaskLayerExplicit OrElse bulb.InFrontOfGlobalMask <> inFront Then
                    bulb.InFrontOfGlobalMask = inFront
                    bulb.GlobalMaskLayerExplicit = True
                    bulb.IsIlluminatedImageDirty = True
                    changed = True
                End If
            End If
        Next
        If changed Then
            MarkDirty()
            RefreshAll()
        End If
    End Sub

    Private Sub DeleteLayer(sender As Object, e As EventArgs)
        Dim selected As List(Of Object) = SelectedObjects()
        If selected.Count = 0 Then Return
        Dim deletionSnapshot As New List(Of InfoBase)()
        For Each item As Object In selected
            Dim visualItem As InfoBase = TryCast(item, InfoBase)
            If visualItem IsNot Nothing Then deletionSnapshot.Add(visualItem)
        Next
        If deletionSnapshot.Count = 0 Then Return
        Backglass.currentTabPage.Mouse.DeleteItems(deletionSnapshot)
    End Sub

    Private Sub MarkDirty()
        If Backglass.currentTabPage IsNot Nothing AndAlso Backglass.currentTabPage.BackglassData IsNot Nothing Then Backglass.currentTabPage.BackglassData.IsDirty = True
    End Sub

    Private Sub RefreshAll(Optional selected As Object = Nothing, Optional selectedItems As IEnumerable(Of Illumination.BulbInfo) = Nothing)
        RefreshLayers()
        If selectedItems IsNot Nothing Then
            For Each item As Illumination.BulbInfo In selectedItems
                SelectObject(item, False)
            Next
        ElseIf selected IsNot Nothing Then
            SelectObject(selected)
        End If
        layers.Focus()
        If Backglass.currentTabPage IsNot Nothing Then
            Backglass.currentTabPage.Invalidate()
            Backglass.currentTabPage.RefreshIllumination()
        End If
    End Sub
End Class
