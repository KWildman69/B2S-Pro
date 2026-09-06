Imports System.Drawing
Imports System.Windows.Forms

''' <summary>
''' First working shell for Global Mask Studio.
''' This build establishes the permanent three-pane workspace and input framework.
''' Mask generation and Quick Selection integration will be added in later builds.
''' </summary>
Public Class formGlobalMaskStudio
    Inherits B2SThemedForm

    Private Enum StudioTool
        [Select]
        Brush
        Lasso
        Rectangle
        Eraser
        Pan
        Zoom
    End Enum

    Private ReadOnly sourceImage As Image
    Private currentTool As StudioTool = StudioTool.Select
    Private currentZoom As Integer = 100

    Private toolStripMain As ToolStrip
    Private statusStripMain As StatusStrip
    Private splitOuter As SplitContainer
    Private splitWorkspace As SplitContainer
    Private panelTools As Panel
    Private flowTools As FlowLayoutPanel
    Private panelArtworkHost As Panel
    Private panelArtworkViewport As Panel
    Private panelCanvas As Panel
    Private picBackglass As PictureBox
    Private panelPreviewHost As Panel
    Private picMaskPreview As PictureBox
    Private lblArtworkHeader As Label
    Private lblPreviewHeader As Label
    Private lblTool As ToolStripStatusLabel
    Private lblZoom As ToolStripStatusLabel
    Private lblCursor As ToolStripStatusLabel
    Private lblStatus As ToolStripStatusLabel
    Private toolButtons As New Dictionary(Of StudioTool, Button)()

    Public Sub New(ByVal artwork As Image)
        sourceImage = If(artwork Is Nothing, Nothing, DirectCast(artwork.Clone(), Image))
        InitializeStudio()
        WindowStateManager.Attach(Me)
        AppThemeManager.ApplyToForm(Me)
    End Sub

    Private Sub InitializeStudio()
        SuspendLayout()

        Name = "formGlobalMaskStudio"
        Text = "Global Mask Studio"
        StartPosition = FormStartPosition.CenterParent
        ClientSize = New Size(1480, 860)
        MinimumSize = New Size(1050, 650)
        KeyPreview = True
        BackColor = Color.FromArgb(37, 37, 38)
        ForeColor = Color.Gainsboro

        BuildToolbar()
        BuildStatusBar()
        BuildWorkspace()

        Controls.Add(splitOuter)
        Controls.Add(statusStripMain)
        Controls.Add(toolStripMain)

        If sourceImage IsNot Nothing Then
            picBackglass.Image = sourceImage
            BuildBlankMask(sourceImage.Width, sourceImage.Height)
            FitArtworkToViewport()
            lblStatus.Text = "Backglass loaded - Studio shell ready"
        Else
            lblStatus.Text = "No backglass image is currently loaded"
        End If

        AddHandler Shown, AddressOf Studio_Shown
        AddHandler Resize, AddressOf Studio_Resize
        AddHandler FormClosed, AddressOf Studio_FormClosed
        AddHandler KeyDown, AddressOf Studio_KeyDown

        ResumeLayout(True)
    End Sub

    Private Sub BuildToolbar()
        toolStripMain = New ToolStrip() With {
            .Name = "toolStripGlobalMaskStudio",
            .Dock = DockStyle.Top,
            .GripStyle = ToolStripGripStyle.Hidden,
            .Padding = New Padding(8, 4, 8, 4),
            .AutoSize = True,
            .ImageScalingSize = New Size(20, 20)
        }

        Dim rear As New ToolStripButton("Open Rear Image") With {.Enabled = False, .ToolTipText = "Rear-image assistance will be added in a later build."}
        Dim overlay As New ToolStripButton("Overlay") With {.Enabled = False, .ToolTipText = "Overlay comparison will be added in a later build."}
        Dim generate As New ToolStripButton("Generate Mask") With {.Enabled = False, .ToolTipText = "Automatic mask generation will be added after the editor tools are connected."}
        Dim finish As New ToolStripButton("Finish") With {.Alignment = ToolStripItemAlignment.Right, .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)}
        Dim cancel As New ToolStripButton("Cancel") With {.Alignment = ToolStripItemAlignment.Right}
        Dim reset As New ToolStripButton("Reset View")

        AddHandler reset.Click, Sub(sender As Object, e As EventArgs)
                                    FitArtworkToViewport()
                                    lblStatus.Text = "View reset"
                                End Sub
        AddHandler finish.Click, Sub(sender As Object, e As EventArgs)
                                     DialogResult = DialogResult.OK
                                     Close()
                                 End Sub
        AddHandler cancel.Click, Sub(sender As Object, e As EventArgs)
                                     DialogResult = DialogResult.Cancel
                                     Close()
                                 End Sub

        toolStripMain.Items.Add(rear)
        toolStripMain.Items.Add(overlay)
        toolStripMain.Items.Add(generate)
        toolStripMain.Items.Add(New ToolStripSeparator())
        toolStripMain.Items.Add(reset)
        toolStripMain.Items.Add(cancel)
        toolStripMain.Items.Add(finish)
    End Sub

    Private Sub BuildStatusBar()
        statusStripMain = New StatusStrip() With {.Name = "statusStripGlobalMaskStudio", .Dock = DockStyle.Bottom}
        lblTool = New ToolStripStatusLabel("Tool: Select")
        lblZoom = New ToolStripStatusLabel("Zoom: 100%")
        lblCursor = New ToolStripStatusLabel("X: 0  Y: 0")
        lblStatus = New ToolStripStatusLabel("Ready") With {.Spring = True, .TextAlign = ContentAlignment.MiddleLeft}
        statusStripMain.Items.Add(lblTool)
        statusStripMain.Items.Add(New ToolStripStatusLabel(" | "))
        statusStripMain.Items.Add(lblZoom)
        statusStripMain.Items.Add(New ToolStripStatusLabel(" | "))
        statusStripMain.Items.Add(lblCursor)
        statusStripMain.Items.Add(New ToolStripStatusLabel(" | "))
        statusStripMain.Items.Add(lblStatus)
    End Sub

    Private Sub BuildWorkspace()
        splitOuter = New SplitContainer() With {
            .Name = "splitGlobalMaskOuter",
            .Dock = DockStyle.Fill,
            .Orientation = Orientation.Vertical,
                        .FixedPanel = FixedPanel.Panel1,
            .Panel1MinSize = 180,
            .Panel2MinSize = 650,
            .SplitterWidth = 5
        }

        splitWorkspace = New SplitContainer() With {
            .Name = "splitGlobalMaskWorkspace",
            .Dock = DockStyle.Fill,
            .Orientation = Orientation.Vertical,
                        .Panel1MinSize = 480,
            .Panel2MinSize = 260,
            .SplitterWidth = 5
        }

        panelTools = New Panel() With {.Dock = DockStyle.Fill, .Padding = New Padding(10)}
        Dim toolsHeader As New Label() With {
            .Text = "TOOLS",
            .Dock = DockStyle.Top,
            .Height = 35,
            .Font = New Font("Segoe UI Semibold", 11.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft
        }
        flowTools = New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.TopDown,
            .WrapContents = False,
            .AutoScroll = True,
            .Padding = New Padding(0, 8, 0, 0)
        }
        panelTools.Controls.Add(flowTools)
        panelTools.Controls.Add(toolsHeader)

        AddToolButton(StudioTool.Select, "Select", "S")
        AddToolButton(StudioTool.Brush, "Brush", "B")
        AddToolButton(StudioTool.Lasso, "Lasso", "L")
        AddToolButton(StudioTool.Rectangle, "Rectangle", "R")
        AddToolButton(StudioTool.Eraser, "Eraser", "E")
        AddToolButton(StudioTool.Pan, "Pan", "H")
        AddToolButton(StudioTool.Zoom, "Zoom", "Z")
        SetCurrentTool(StudioTool.Select)

        panelArtworkHost = New Panel() With {.Dock = DockStyle.Fill, .Padding = New Padding(8)}
        lblArtworkHeader = CreateHeader("FRONT BACKGLASS")
        panelArtworkViewport = New Panel() With {
            .Dock = DockStyle.Fill,
            .AutoScroll = True,
            .BackColor = Color.FromArgb(24, 24, 24),
            .BorderStyle = BorderStyle.FixedSingle
        }
        panelCanvas = New Panel() With {.BackColor = Color.Black, .Location = New Point(12, 12)}
        picBackglass = New PictureBox() With {
            .Dock = DockStyle.Fill,
            .BackColor = Color.Black,
            .SizeMode = PictureBoxSizeMode.StretchImage,
            .TabStop = False
        }
        panelCanvas.Controls.Add(picBackglass)
        panelArtworkViewport.Controls.Add(panelCanvas)
        panelArtworkHost.Controls.Add(panelArtworkViewport)
        panelArtworkHost.Controls.Add(lblArtworkHeader)

        panelPreviewHost = New Panel() With {.Dock = DockStyle.Fill, .Padding = New Padding(8)}
        lblPreviewHeader = CreateHeader("GLOBAL MASK PREVIEW")
        picMaskPreview = New PictureBox() With {
            .Dock = DockStyle.Fill,
            .BackColor = Color.Black,
            .BorderStyle = BorderStyle.FixedSingle,
            .SizeMode = PictureBoxSizeMode.Zoom,
            .TabStop = False
        }
        panelPreviewHost.Controls.Add(picMaskPreview)
        panelPreviewHost.Controls.Add(lblPreviewHeader)

        splitOuter.Panel1.Controls.Add(panelTools)
        splitOuter.Panel2.Controls.Add(splitWorkspace)
        splitWorkspace.Panel1.Controls.Add(panelArtworkHost)
        splitWorkspace.Panel2.Controls.Add(panelPreviewHost)

        AddHandler picBackglass.MouseMove, AddressOf Artwork_MouseMove
        AddHandler picBackglass.MouseWheel, AddressOf Artwork_MouseWheel
        AddHandler picBackglass.MouseEnter, Sub(sender As Object, e As EventArgs) picBackglass.Focus()
    End Sub

    Private Function CreateHeader(ByVal text As String) As Label
        Return New Label() With {
            .Text = text,
            .Dock = DockStyle.Top,
            .Height = 38,
            .Padding = New Padding(8, 0, 0, 0),
            .Font = New Font("Segoe UI Semibold", 10.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft
        }
    End Function

    Private Sub AddToolButton(ByVal mode As StudioTool, ByVal caption As String, ByVal shortcut As String)
        Dim button As New Button() With {
            .Text = caption & "     " & shortcut,
            .Width = 165,
            .Height = 43,
            .Margin = New Padding(2, 3, 2, 3),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Padding = New Padding(14, 0, 0, 0),
            .FlatStyle = FlatStyle.Flat,
            .Tag = mode,
            .Cursor = Cursors.Hand
        }
        button.FlatAppearance.BorderSize = 1
        AddHandler button.Click, AddressOf ToolButton_Click
        toolButtons(mode) = button
        flowTools.Controls.Add(button)
    End Sub

    Private Sub ToolButton_Click(ByVal sender As Object, ByVal e As EventArgs)
        Dim button As Button = TryCast(sender, Button)
        If button Is Nothing Then Return
        SetCurrentTool(DirectCast(button.Tag, StudioTool))
    End Sub

    Private Sub SetCurrentTool(ByVal mode As StudioTool)
        currentTool = mode
        For Each pair As KeyValuePair(Of StudioTool, Button) In toolButtons
            pair.Value.FlatAppearance.BorderSize = If(pair.Key = mode, 2, 1)
            pair.Value.Font = New Font(pair.Value.Font, If(pair.Key = mode, FontStyle.Bold, FontStyle.Regular))
        Next
        lblTool.Text = "Tool: " & mode.ToString()
        lblStatus.Text = mode.ToString() & " tool selected"
        Select Case mode
            Case StudioTool.Pan
                picBackglass.Cursor = Cursors.Hand
            Case StudioTool.Zoom
                picBackglass.Cursor = Cursors.Cross
            Case Else
                picBackglass.Cursor = Cursors.Default
        End Select
    End Sub

    Private Sub BuildBlankMask(ByVal width As Integer, ByVal height As Integer)
        If width <= 0 OrElse height <= 0 Then Return
        Dim blank As New Bitmap(width, height)
        Using g As Graphics = Graphics.FromImage(blank)
            g.Clear(Color.Black)
        End Using
        Dim oldImage As Image = picMaskPreview.Image
        picMaskPreview.Image = blank
        If oldImage IsNot Nothing Then oldImage.Dispose()
    End Sub

    Private Sub FitArtworkToViewport()
        If sourceImage Is Nothing OrElse panelArtworkViewport.ClientSize.Width <= 0 OrElse panelArtworkViewport.ClientSize.Height <= 0 Then Return
        Dim availableWidth As Integer = Math.Max(50, panelArtworkViewport.ClientSize.Width - 28)
        Dim availableHeight As Integer = Math.Max(50, panelArtworkViewport.ClientSize.Height - 28)
        Dim scale As Double = Math.Min(availableWidth / CDbl(sourceImage.Width), availableHeight / CDbl(sourceImage.Height))
        currentZoom = Math.Max(5, Math.Min(500, CInt(Math.Floor(scale * 100.0))))
        ApplyZoom()
    End Sub

    Private Sub ApplyZoom()
        If sourceImage Is Nothing Then Return
        panelCanvas.Size = New Size(Math.Max(1, CInt(sourceImage.Width * currentZoom / 100.0)),
                                    Math.Max(1, CInt(sourceImage.Height * currentZoom / 100.0)))
        lblZoom.Text = "Zoom: " & currentZoom.ToString() & "%"
        panelArtworkViewport.AutoScrollMinSize = New Size(panelCanvas.Width + 24, panelCanvas.Height + 24)
    End Sub

    Private Sub Artwork_MouseWheel(ByVal sender As Object, ByVal e As MouseEventArgs)
        If (ModifierKeys And Keys.Control) <> Keys.Control AndAlso currentTool <> StudioTool.Zoom Then Return
        currentZoom = Math.Max(5, Math.Min(500, currentZoom + If(e.Delta > 0, 10, -10)))
        ApplyZoom()
        lblStatus.Text = "Zoom changed"
    End Sub

    Private Sub Artwork_MouseMove(ByVal sender As Object, ByVal e As MouseEventArgs)
        If sourceImage Is Nothing OrElse picBackglass.ClientSize.Width <= 0 OrElse picBackglass.ClientSize.Height <= 0 Then Return
        Dim imageX As Integer = Math.Max(0, Math.Min(sourceImage.Width - 1, CInt(e.X * sourceImage.Width / CDbl(picBackglass.ClientSize.Width))))
        Dim imageY As Integer = Math.Max(0, Math.Min(sourceImage.Height - 1, CInt(e.Y * sourceImage.Height / CDbl(picBackglass.ClientSize.Height))))
        lblCursor.Text = "X: " & imageX.ToString() & "  Y: " & imageY.ToString()
    End Sub

    Private Sub Studio_KeyDown(ByVal sender As Object, ByVal e As KeyEventArgs)
        Select Case e.KeyCode
            Case Keys.S : SetCurrentTool(StudioTool.Select)
            Case Keys.B : SetCurrentTool(StudioTool.Brush)
            Case Keys.L : SetCurrentTool(StudioTool.Lasso)
            Case Keys.R : SetCurrentTool(StudioTool.Rectangle)
            Case Keys.E : SetCurrentTool(StudioTool.Eraser)
            Case Keys.H : SetCurrentTool(StudioTool.Pan)
            Case Keys.Z : SetCurrentTool(StudioTool.Zoom)
            Case Keys.Escape : Close()
            Case Else : Return
        End Select
        e.Handled = True
    End Sub

    Private Sub Studio_Shown(ByVal sender As Object, ByVal e As EventArgs)
        If splitOuter IsNot Nothing Then
            Dim d As Integer = Math.Max(splitOuter.Panel1MinSize, Math.Min(205, splitOuter.Width - splitOuter.Panel2MinSize - splitOuter.SplitterWidth))
            If d > 0 Then splitOuter.SplitterDistance = d
        End If
        If splitWorkspace IsNot Nothing Then
            Dim d As Integer = Math.Max(splitWorkspace.Panel1MinSize, Math.Min(splitWorkspace.Width - 320, splitWorkspace.Width - splitWorkspace.Panel2MinSize - splitWorkspace.SplitterWidth))
            If d > 0 Then splitWorkspace.SplitterDistance = d
        End If
        If Not WindowStateManager.HasSavedState(Me) Then FitArtworkToViewport()
        AppThemeManager.ApplyToForm(Me)
    End Sub

    Private Sub Studio_Resize(ByVal sender As Object, ByVal e As EventArgs)
        If WindowState = FormWindowState.Minimized Then Return
        If sourceImage IsNot Nothing AndAlso currentZoom <= 0 Then FitArtworkToViewport()
    End Sub

    Private Sub Studio_FormClosed(ByVal sender As Object, ByVal e As FormClosedEventArgs)
        If picMaskPreview IsNot Nothing AndAlso picMaskPreview.Image IsNot Nothing Then
            picMaskPreview.Image.Dispose()
            picMaskPreview.Image = Nothing
        End If
        If picBackglass IsNot Nothing Then picBackglass.Image = Nothing
        If sourceImage IsNot Nothing Then sourceImage.Dispose()
    End Sub
End Class
