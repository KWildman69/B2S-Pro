Imports System.Drawing.Drawing2D
Imports System.Globalization
Imports System.Linq
Imports System.Windows.Forms

Public Class formPhysicsEditor
    Inherits B2SThemedForm

    Private ReadOnly sourceBall As Illumination.BulbInfo
    Private ReadOnly canvas As New PhysicsCanvas()
    Private ReadOnly saveButton As New Button()
    Private ReadOnly closeButton As New Button()
    Private ReadOnly clearButton As New Button()
    Private ReadOnly newBoundaryButton As New Button()
    Private ReadOnly deleteBoundaryButton As New Button()
    Private ReadOnly renameBoundaryButton As New Button()
    Private ReadOnly spliceBoundaryButton As New Button()
    Private ReadOnly lockBoundaryButton As New Button()
    Private ReadOnly addObstacleButton As New Button()
    Private ReadOnly deleteObstacleButton As New Button()
    Private ReadOnly addSwitchButton As New Button()
    Private ReadOnly deleteSwitchButton As New Button()
    Private ReadOnly switchIDBox As New NumericUpDown()
    Private ReadOnly launcherEnabledCheck As New CheckBox()
    Private ReadOnly launcherFollowPivotCheck As New CheckBox()
    Private ReadOnly launcherTypeBox As New ComboBox()
    Private ReadOnly launcherIDBox As New NumericUpDown()
    Private ReadOnly launcherXBox As New NumericUpDown()
    Private ReadOnly launcherYBox As New NumericUpDown()
    Private ReadOnly launcherAngleBox As New NumericUpDown()
    Private ReadOnly launcherStrengthBox As New NumericUpDown()
    Private ReadOnly launcherRandomAngleBox As New NumericUpDown()
    Private ReadOnly launcherRandomStrengthBox As New NumericUpDown()
    Private ReadOnly launcherCaptureRadiusBox As New NumericUpDown()
    Private ReadOnly boundaryBox As New ComboBox()
    Private ReadOnly enabledCheck As New CheckBox()
    Private ReadOnly rollBallCheckBox As New CheckBox()
    Private ReadOnly flipperBox As New ComboBox()
    Private ReadOnly gravityBox As New NumericUpDown()
    Private ReadOnly strengthBox As New NumericUpDown()
    Private ReadOnly bounceBox As New NumericUpDown()
    Private ReadOnly segmentUseDefaultCheck As New CheckBox()
    Private ReadOnly segmentBounceBox As New NumericUpDown()
    Private updatingSegmentBounce As Boolean

    Public Sub New(ByVal ball As Illumination.BulbInfo,
                   ByVal backglassImage As Image,
                   ByVal snippets As IEnumerable(Of Illumination.BulbInfo))
        sourceBall = ball
        Text = "Physics Boundary Editor — " & If(String.IsNullOrWhiteSpace(ball.Name), "Ball", ball.Name)
        StartPosition = FormStartPosition.CenterParent
        Width = 1120
        Height = 790
        MinimumSize = New Size(980, 600)
        BackColor = Color.FromArgb(9, 12, 20)

        Dim sidebar As New Panel With {.Dock = DockStyle.Right, .Width = 430, .Padding = New Padding(8),
                                      .BackColor = Color.FromArgb(22, 25, 38)}
        Dim tabs As New TabControl With {.Dock = DockStyle.Fill}
        Dim ballPage As TabPage = CreateEditorPage("Ball")
        Dim boundaryPage As TabPage = CreateEditorPage("Boundaries")
        Dim launcherPage As TabPage = CreateEditorPage("Launcher")
        tabs.TabPages.AddRange(New TabPage() {ballPage, boundaryPage, launcherPage})
        ConfigureButton(saveButton, "Save Boundaries", AddressOf SaveBoundaries)
        ConfigureButton(closeButton, "Cancel", AddressOf CancelEditor)
        ConfigureButton(clearButton, "Clear Points", AddressOf ClearBoundaries)
        ConfigureButton(newBoundaryButton, "New Boundary", AddressOf NewBoundary)
        ConfigureButton(deleteBoundaryButton, "Delete Boundary", AddressOf DeleteBoundary)
        ConfigureButton(renameBoundaryButton, "Rename", AddressOf RenameBoundary)
        ConfigureButton(spliceBoundaryButton, "Splice Into Line", AddressOf SpliceBoundary)
        ConfigureButton(lockBoundaryButton, "Lock Boundary", AddressOf ToggleBoundaryLock)
        ConfigureButton(addObstacleButton, "Add Circular Bumper", AddressOf AddObstacle)
        ConfigureButton(deleteObstacleButton, "Delete Bumper", AddressOf DeleteObstacle)
        ConfigureButton(addSwitchButton, "Add Switch Zone", AddressOf AddSwitchZone)
        ConfigureButton(deleteSwitchButton, "Delete Switch Zone", AddressOf DeleteSwitchZone)
        switchIDBox.Minimum = 1D
        switchIDBox.Maximum = 255D
        switchIDBox.Value = 1D
        switchIDBox.Width = 58
        switchIDBox.Enabled = False
        switchIDBox.Margin = New Padding(0, 3, 8, 0)
        AddHandler switchIDBox.ValueChanged, AddressOf SwitchIDChanged
        Dim switchIDLabel As Label = ToolbarLabel("Switch ID:")

        launcherEnabledCheck.Text = "Enable launcher"
        launcherEnabledCheck.Checked = ball.SnippitInfo.PhysicsLauncherEnabled
        launcherEnabledCheck.ForeColor = Color.White
        launcherEnabledCheck.AutoSize = True
        launcherEnabledCheck.Margin = New Padding(8, 8, 5, 0)
        launcherFollowPivotCheck.Text = "Attach launcher to selected pivot snippet"
        launcherFollowPivotCheck.Checked = ball.SnippitInfo.PhysicsLauncherFollowPivot
        launcherFollowPivotCheck.ForeColor = Color.White
        launcherFollowPivotCheck.AutoSize = True
        launcherFollowPivotCheck.Margin = New Padding(8, 8, 5, 0)
        launcherTypeBox.DropDownStyle = ComboBoxStyle.DropDownList
        launcherTypeBox.Items.AddRange(New Object() {"Solenoid", "B2S ID"})
        launcherTypeBox.SelectedIndex = If(ball.SnippitInfo.PhysicsLauncherTriggerType = 3, 1, 0)
        launcherTypeBox.Width = 82
        launcherTypeBox.Margin = New Padding(0, 3, 5, 0)
        ConfigureLauncherBox(launcherIDBox, 0D, 255D, ball.SnippitInfo.PhysicsLauncherTriggerID, 0)
        Dim defaultX As Single = If(ball.SnippitInfo.PhysicsLauncherEnabled, ball.SnippitInfo.PhysicsLauncherX, CSng(ball.Location.X + ball.Size.Width / 2.0F))
        Dim defaultY As Single = If(ball.SnippitInfo.PhysicsLauncherEnabled, ball.SnippitInfo.PhysicsLauncherY, CSng(ball.Location.Y + ball.Size.Height / 2.0F))
        ConfigureLauncherBox(launcherXBox, -100000D, 100000D, defaultX, 1)
        ConfigureLauncherBox(launcherYBox, -100000D, 100000D, defaultY, 1)
        ConfigureLauncherBox(launcherAngleBox, -360D, 360D, ball.SnippitInfo.PhysicsLauncherAngle, 1)
        ConfigureLauncherBox(launcherStrengthBox, 0D, 10000D, ball.SnippitInfo.PhysicsLauncherStrength, 0)
        ConfigureLauncherBox(launcherRandomAngleBox, 0D, 180D, ball.SnippitInfo.PhysicsLauncherRandomAngle, 1)
        ConfigureLauncherBox(launcherRandomStrengthBox, 0D, 100D, ball.SnippitInfo.PhysicsLauncherRandomStrength, 1)
        ConfigureLauncherBox(launcherCaptureRadiusBox, 5D, 500D, ball.SnippitInfo.PhysicsLauncherCaptureRadius, 1)
        AddHandler launcherEnabledCheck.CheckedChanged, AddressOf LauncherPreviewChanged
        AddHandler launcherFollowPivotCheck.CheckedChanged, AddressOf LauncherPreviewChanged
        AddHandler launcherXBox.ValueChanged, AddressOf LauncherPreviewChanged
        AddHandler launcherYBox.ValueChanged, AddressOf LauncherPreviewChanged
        AddHandler launcherAngleBox.ValueChanged, AddressOf LauncherPreviewChanged
        AddHandler launcherStrengthBox.ValueChanged, AddressOf LauncherPreviewChanged
        AddHandler launcherCaptureRadiusBox.ValueChanged, AddressOf LauncherPreviewChanged

        enabledCheck.Text = "Enable ball physics"
        enabledCheck.Checked = ball.SnippitInfo.PhysicsBall
        enabledCheck.ForeColor = Color.White
        enabledCheck.AutoSize = True
        enabledCheck.Margin = New Padding(8, 8, 5, 0)

        rollBallCheckBox.Text = "Roll ball while moving"
        rollBallCheckBox.Checked = ball.SnippitInfo.MotionPathRollEnabled
        rollBallCheckBox.ForeColor = Color.White
        rollBallCheckBox.AutoSize = True
        rollBallCheckBox.Margin = New Padding(8, 8, 5, 0)

        Dim flipperLabel As Label = ToolbarLabel("Pivot snippet:")
        flipperBox.DropDownStyle = ComboBoxStyle.DropDownList
        flipperBox.Width = 145
        flipperBox.Margin = New Padding(0, 3, 5, 0)
        flipperBox.Items.Add("(none)")
        For Each snippet As Illumination.BulbInfo In snippets
            If snippet IsNot ball AndAlso snippet.IsImageSnippit AndAlso snippet.SnippitInfo.PivotAnimationEnabled AndAlso
               Not String.IsNullOrWhiteSpace(snippet.Name) Then flipperBox.Items.Add(snippet.Name)
        Next
        Dim selectedFlipper As Integer = flipperBox.FindStringExact(ball.SnippitInfo.PhysicsFlipperName)
        flipperBox.SelectedIndex = If(selectedFlipper >= 0, selectedFlipper, 0)

        Dim gravityLabel As Label = ToolbarLabel("Gravity:")
        gravityBox.Minimum = 0D
        gravityBox.Maximum = 10000D
        gravityBox.DecimalPlaces = 0
        gravityBox.Increment = 25D
        gravityBox.Value = CDec(Math.Max(0.0F, Math.Min(10000.0F, ball.SnippitInfo.PhysicsGravity)))
        gravityBox.Width = 72
        gravityBox.Margin = New Padding(0, 3, 8, 0)

        Dim strengthLabel As Label = ToolbarLabel("Contact strength:")
        strengthBox.Minimum = 0D
        strengthBox.Maximum = 5D
        strengthBox.DecimalPlaces = 2
        strengthBox.Increment = 0.1D
        strengthBox.Value = CDec(Math.Max(0.0F, Math.Min(5.0F, ball.SnippitInfo.PhysicsFlipperStrength)))
        strengthBox.Width = 62
        strengthBox.Margin = New Padding(0, 3, 8, 0)

        Dim bounceLabel As Label = ToolbarLabel("Boundary bounce:")
        bounceBox.Minimum = 0D
        bounceBox.Maximum = 1D
        bounceBox.DecimalPlaces = 2
        bounceBox.Increment = 0.05D
        bounceBox.Value = CDec(Math.Max(0.0F, Math.Min(1.0F, ball.SnippitInfo.PhysicsBoundaryBounce)))
        bounceBox.Width = 62
        bounceBox.Margin = New Padding(0, 3, 8, 0)

        Dim segmentBounceLabel As Label = ToolbarLabel("Selected segment bounce (0.00–3.00):")
        segmentUseDefaultCheck.Text = "Use normal boundary bounce"
        segmentUseDefaultCheck.Checked = True
        segmentUseDefaultCheck.ForeColor = Color.White
        segmentUseDefaultCheck.AutoSize = True
        segmentUseDefaultCheck.Enabled = False
        segmentBounceBox.Minimum = 0D
        segmentBounceBox.Maximum = 3D
        segmentBounceBox.DecimalPlaces = 2
        segmentBounceBox.Increment = 0.05D
        segmentBounceBox.Value = bounceBox.Value
        segmentBounceBox.Enabled = False
        AddHandler segmentUseDefaultCheck.CheckedChanged, AddressOf SegmentBounceChanged
        AddHandler segmentBounceBox.ValueChanged, AddressOf SegmentBounceChanged

        Dim boundaryLabel As Label = ToolbarLabel("Boundary:")
        boundaryBox.DropDownStyle = ComboBoxStyle.DropDownList
        boundaryBox.Width = 130
        boundaryBox.Margin = New Padding(0, 3, 3, 0)
        AddHandler boundaryBox.SelectedIndexChanged, AddressOf ActiveBoundaryChanged

        AddPageControls(ballPage, New Control() {enabledCheck, rollBallCheckBox, flipperLabel, flipperBox, gravityLabel, gravityBox,
                                                 strengthLabel, strengthBox, bounceLabel, bounceBox})
        AddPageControls(boundaryPage, New Control() {boundaryLabel, boundaryBox, segmentBounceLabel, segmentUseDefaultCheck,
                                                     segmentBounceBox, lockBoundaryButton, newBoundaryButton,
                                                     renameBoundaryButton, spliceBoundaryButton, deleteBoundaryButton,
                                                     clearButton,
                                                     SidebarHeader("CIRCULAR BUMPERS"), addObstacleButton, deleteObstacleButton,
                                                    SidebarHeader("SWITCH ZONES"), addSwitchButton, deleteSwitchButton,
                                                    switchIDLabel, switchIDBox})
        AddPageControls(launcherPage, New Control() {launcherEnabledCheck, launcherFollowPivotCheck, ToolbarLabel("Trigger type:"), launcherTypeBox,
                                                     ToolbarLabel("Trigger ID:"), launcherIDBox,
                                                     ToolbarLabel("Launch angle:"), launcherAngleBox, ToolbarLabel("Strength:"), launcherStrengthBox,
                                                     ToolbarLabel("Random angle:"), launcherRandomAngleBox,
                                                     ToolbarLabel("Random strength %:"), launcherRandomStrengthBox,
                                                     ToolbarLabel("Capture radius:"), launcherCaptureRadiusBox})

        Dim actions As New TableLayoutPanel With {.Dock = DockStyle.Bottom, .Height = 46, .ColumnCount = 2,
                                                  .Padding = New Padding(0, 6, 0, 0)}
        actions.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        actions.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        saveButton.Dock = DockStyle.Fill : closeButton.Dock = DockStyle.Fill
        saveButton.Margin = New Padding(0, 0, 4, 0) : closeButton.Margin = New Padding(4, 0, 0, 0)
        actions.Controls.Add(saveButton, 0, 0) : actions.Controls.Add(closeButton, 1, 0)
        sidebar.Controls.Add(tabs) : sidebar.Controls.Add(actions)

        Dim help As New Label With {
            .Dock = DockStyle.Bottom, .Height = 30, .TextAlign = ContentAlignment.MiddleCenter,
            .ForeColor = Color.White, .BackColor = Color.FromArgb(22, 25, 38),
            .Text = "Click a line between two points to select its bounce; click empty space to add points. Separate boundaries never connect."
        }

        canvas.Dock = DockStyle.Fill
        canvas.BackglassImage = New Bitmap(backglassImage)
        canvas.AuthoredSize = backglassImage.Size
        For Each snippet As Illumination.BulbInfo In snippets.OrderBy(Function(item) item.ZOrder)
            If snippet.IsImageSnippit AndAlso snippet.Image IsNot Nothing Then
                canvas.Scene.Add(New SceneItem(snippet.Name, snippet.Image,
                                               New RectangleF(snippet.Location.X, snippet.Location.Y,
                                                              Math.Max(1, snippet.Size.Width), Math.Max(1, snippet.Size.Height)),
                                               Object.ReferenceEquals(snippet, ball)))
            End If
        Next
        If ball.SnippitInfo.PhysicsBoundaryPaths.Count > 0 Then
            For Each path As List(Of PointF) In ball.SnippitInfo.PhysicsBoundaryPaths
                canvas.Paths.Add(New List(Of PointF)(path))
            Next
        Else
            canvas.Paths.Add(New List(Of PointF)(ball.SnippitInfo.PhysicsFloorPoints))
        End If
        For index As Integer = 0 To canvas.Paths.Count - 1
            Dim savedName As String = If(index < ball.SnippitInfo.PhysicsBoundaryNames.Count,
                                         ball.SnippitInfo.PhysicsBoundaryNames(index).Trim(), String.Empty)
            canvas.BoundaryNames.Add(If(savedName.Length > 0, savedName, "Boundary " & (index + 1).ToString()))
            canvas.BoundaryLocks.Add(index < ball.SnippitInfo.PhysicsBoundaryLocks.Count AndAlso
                                     ball.SnippitInfo.PhysicsBoundaryLocks(index))
            Dim savedSegmentBounces As New List(Of Single)()
            If index < ball.SnippitInfo.PhysicsBoundarySegmentBounces.Count Then
                savedSegmentBounces.AddRange(ball.SnippitInfo.PhysicsBoundarySegmentBounces(index))
            End If
            canvas.BoundarySegmentBounces.Add(savedSegmentBounces)
        Next
        canvas.Obstacles.AddRange(ball.SnippitInfo.PhysicsObstacles)
        canvas.SwitchZones.AddRange(ball.SnippitInfo.PhysicsSwitchZones)
        canvas.SwitchIDs.AddRange(ball.SnippitInfo.PhysicsSwitchIDs)
        canvas.SwitchAngles.AddRange(ball.SnippitInfo.PhysicsSwitchAngles)
        While canvas.SwitchIDs.Count < canvas.SwitchZones.Count
            canvas.SwitchIDs.Add(1)
        End While
        While canvas.SwitchAngles.Count < canvas.SwitchZones.Count
            canvas.SwitchAngles.Add(0.0F)
        End While
        While canvas.SwitchAngles.Count > canvas.SwitchZones.Count
            canvas.SwitchAngles.RemoveAt(canvas.SwitchAngles.Count - 1)
        End While
        RefreshBoundaryList(0)
        AddHandler canvas.SelectionChanged, AddressOf CanvasSelectionChanged
        AddHandler canvas.BoundaryStructureChanged, AddressOf CanvasBoundaryStructureChanged
        AddHandler canvas.LauncherOriginChanged, AddressOf LauncherOriginDragged
        AddHandler canvas.LauncherAngleChanged, AddressOf LauncherAngleDragged

        Controls.Add(canvas)
        Controls.Add(help)
        Controls.Add(sidebar)
        CanvasSelectionChanged(Nothing, EventArgs.Empty)
        LauncherPreviewChanged(Nothing, EventArgs.Empty)
    End Sub

    Private Shared Function CreateEditorPage(ByVal caption As String) As TabPage
        Dim page As New TabPage(caption) With {.BackColor = Color.FromArgb(22, 25, 38), .Padding = New Padding(8)}
        Dim list As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.TopDown,
                                             .WrapContents = False, .AutoScroll = True,
                                             .BackColor = Color.FromArgb(22, 25, 38)}
        page.Controls.Add(list)
        Return page
    End Function

    Private Shared Sub AddPageControls(ByVal page As TabPage, ByVal controls As IEnumerable(Of Control))
        Dim list As FlowLayoutPanel = DirectCast(page.Controls(0), FlowLayoutPanel)
        For Each control As Control In controls
            control.Margin = New Padding(3, 3, 3, 3)
            If TypeOf control Is Label Then
                control.AutoSize = False : control.Width = 370 : control.Height = If(DirectCast(control, Label).Font.Bold, 26, 20)
                DirectCast(control, Label).TextAlign = ContentAlignment.BottomLeft
            ElseIf TypeOf control Is CheckBox Then
                control.AutoSize = False : control.Width = 370 : control.Height = 25
            Else
                control.Width = 370
                If TypeOf control Is Button Then control.AutoSize = False : control.Height = 31
            End If
            list.Controls.Add(control)
        Next
    End Sub

    Private Shared Function SidebarHeader(ByVal caption As String) As Label
        Return New Label With {.Text = caption, .ForeColor = Color.FromArgb(105, 220, 255),
                               .Font = New Font(SystemFonts.MessageBoxFont, FontStyle.Bold),
                               .AutoSize = False, .Width = 370, .Height = 26,
                               .TextAlign = ContentAlignment.BottomLeft, .Margin = New Padding(3, 10, 3, 2)}
    End Function

    Public ReadOnly Property ResultPoints As List(Of PointF)
        Get
            Dim paths As List(Of List(Of PointF)) = ResultBoundaryPaths
            Return If(paths.Count > 0, New List(Of PointF)(paths(0)), New List(Of PointF)())
        End Get
    End Property

    Public ReadOnly Property ResultBoundaryPaths As List(Of List(Of PointF))
        Get
            Dim result As New List(Of List(Of PointF))()
            For Each path As List(Of PointF) In canvas.Paths
                If path.Count >= 2 Then result.Add(New List(Of PointF)(path))
            Next
            Return result
        End Get
    End Property

    Public ReadOnly Property ResultBoundaryNames As List(Of String)
        Get
            Dim result As New List(Of String)()
            For index As Integer = 0 To canvas.Paths.Count - 1
                If canvas.Paths(index).Count >= 2 Then result.Add(canvas.BoundaryNames(index))
            Next
            Return result
        End Get
    End Property

    Public ReadOnly Property ResultBoundaryLocks As List(Of Boolean)
        Get
            Dim result As New List(Of Boolean)()
            For index As Integer = 0 To canvas.Paths.Count - 1
                If canvas.Paths(index).Count >= 2 Then
                    result.Add(index < canvas.BoundaryLocks.Count AndAlso canvas.BoundaryLocks(index))
                End If
            Next
            Return result
        End Get
    End Property

    Public ReadOnly Property ResultBoundarySegmentBounces As List(Of List(Of Single))
        Get
            Dim result As New List(Of List(Of Single))()
            For index As Integer = 0 To canvas.Paths.Count - 1
                If canvas.Paths(index).Count >= 2 Then
                    Dim values As New List(Of Single)()
                    If index < canvas.BoundarySegmentBounces.Count Then values.AddRange(canvas.BoundarySegmentBounces(index))
                    While values.Count < canvas.Paths(index).Count - 1
                        values.Add(-1.0F)
                    End While
                    While values.Count > canvas.Paths(index).Count - 1
                        values.RemoveAt(values.Count - 1)
                    End While
                    result.Add(values)
                End If
            Next
            Return result
        End Get
    End Property

    Public ReadOnly Property ResultObstacles As List(Of RectangleF)
        Get
            Return canvas.Obstacles.Select(Function(item) New RectangleF(item.X, item.Y, item.Width, item.Height)).ToList()
        End Get
    End Property

    Public ReadOnly Property ResultSwitchZones As List(Of RectangleF)
        Get
            Return canvas.SwitchZones.Select(Function(item) New RectangleF(item.X, item.Y, item.Width, item.Height)).ToList()
        End Get
    End Property

    Public ReadOnly Property ResultSwitchIDs As List(Of Integer)
        Get
            Return New List(Of Integer)(canvas.SwitchIDs)
        End Get
    End Property

    Public ReadOnly Property ResultEnabled As Boolean
        Get
            Return enabledCheck.Checked
        End Get
    End Property

    Public ReadOnly Property ResultRollEnabled As Boolean
        Get
            Return rollBallCheckBox.Checked
        End Get
    End Property

    Public ReadOnly Property ResultFlipperName As String
        Get
            Return If(flipperBox.SelectedIndex <= 0, String.Empty, CStr(flipperBox.SelectedItem))
        End Get
    End Property

    Public ReadOnly Property ResultGravity As Single
        Get
            Return CSng(gravityBox.Value)
        End Get
    End Property

    Public ReadOnly Property ResultFlipperStrength As Single
        Get
            Return CSng(strengthBox.Value)
        End Get
    End Property

    Public ReadOnly Property ResultBoundaryBounce As Single
        Get
            Return CSng(bounceBox.Value)
        End Get
    End Property

    Public ReadOnly Property ResultBounds As String
        Get
            Return "0,0," & canvas.AuthoredSize.Width.ToString(CultureInfo.InvariantCulture) & "," &
                   canvas.AuthoredSize.Height.ToString(CultureInfo.InvariantCulture)
        End Get
    End Property

    Public ReadOnly Property ResultLauncherEnabled As Boolean
        Get
            Return launcherEnabledCheck.Checked
        End Get
    End Property

    Public ReadOnly Property ResultSwitchAngles As List(Of Single)
        Get
            Return New List(Of Single)(canvas.SwitchAngles)
        End Get
    End Property
    Public ReadOnly Property ResultLauncherFollowPivot As Boolean
        Get
            Return launcherFollowPivotCheck.Checked
        End Get
    End Property
    Public ReadOnly Property ResultLauncherTriggerType As Integer
        Get
            Return If(launcherTypeBox.SelectedIndex = 1, 3, 1)
        End Get
    End Property
    Public ReadOnly Property ResultLauncherTriggerID As Integer
        Get
            Return CInt(launcherIDBox.Value)
        End Get
    End Property
    Public ReadOnly Property ResultLauncherX As Single
        Get
            Return CSng(launcherXBox.Value)
        End Get
    End Property
    Public ReadOnly Property ResultLauncherY As Single
        Get
            Return CSng(launcherYBox.Value)
        End Get
    End Property
    Public ReadOnly Property ResultLauncherAngle As Single
        Get
            Return CSng(launcherAngleBox.Value)
        End Get
    End Property
    Public ReadOnly Property ResultLauncherStrength As Single
        Get
            Return CSng(launcherStrengthBox.Value)
        End Get
    End Property
    Public ReadOnly Property ResultLauncherRandomAngle As Single
        Get
            Return CSng(launcherRandomAngleBox.Value)
        End Get
    End Property
    Public ReadOnly Property ResultLauncherRandomStrength As Single
        Get
            Return CSng(launcherRandomStrengthBox.Value)
        End Get
    End Property
    Public ReadOnly Property ResultLauncherCaptureRadius As Single
        Get
            Return CSng(launcherCaptureRadiusBox.Value)
        End Get
    End Property

    Private Sub ConfigureButton(ByVal button As Button, ByVal caption As String, ByVal handler As EventHandler)
        button.Text = caption
        button.AutoSize = True
        button.Height = 29
        button.Margin = New Padding(3, 1, 3, 1)
        AddHandler button.Click, handler
    End Sub

    Private Function ToolbarLabel(ByVal text As String) As Label
        Return New Label With {.Text = text, .ForeColor = Color.White, .AutoSize = True,
                               .Margin = New Padding(8, 8, 3, 0)}
    End Function

    Private Sub ConfigureLauncherBox(ByVal box As NumericUpDown, ByVal minimum As Decimal, ByVal maximum As Decimal, ByVal value As Single, ByVal decimals As Integer)
        box.Minimum = minimum
        box.Maximum = maximum
        box.DecimalPlaces = decimals
        box.Increment = If(decimals = 0, 1D, 0.5D)
        box.Value = Math.Max(minimum, Math.Min(maximum, CDec(value)))
        box.Width = 68
        box.Margin = New Padding(0, 3, 5, 0)
    End Sub

    Private Sub LauncherPreviewChanged(ByVal sender As Object, ByVal e As EventArgs)
        canvas.LauncherEnabled = launcherEnabledCheck.Checked
        canvas.LauncherOrigin = New PointF(CSng(launcherXBox.Value), CSng(launcherYBox.Value))
        canvas.LauncherAngle = CSng(launcherAngleBox.Value)
        canvas.LauncherStrength = CSng(launcherStrengthBox.Value)
        canvas.LauncherCaptureRadius = CSng(launcherCaptureRadiusBox.Value)
        canvas.SetBallPreviewLocation(launcherEnabledCheck.Checked AndAlso launcherFollowPivotCheck.Checked,
                                      canvas.LauncherOrigin)
        strengthBox.Enabled = Not (launcherEnabledCheck.Checked AndAlso launcherFollowPivotCheck.Checked)
        canvas.Invalidate()
    End Sub

    Private Sub LauncherAngleDragged(ByVal sender As Object, ByVal e As EventArgs)
        launcherAngleBox.Value = Math.Max(launcherAngleBox.Minimum,
                                          Math.Min(launcherAngleBox.Maximum, CDec(Math.Round(canvas.LauncherAngle, 1))))
    End Sub

    Private Sub LauncherOriginDragged(ByVal sender As Object, ByVal e As EventArgs)
        ' Updating X raises LauncherPreviewChanged, which writes the backing
        ' controls back into the canvas. Capture both mouse coordinates first
        ' so that event cannot replace the newly dragged Y value with the old one.
        Dim draggedOrigin As PointF = canvas.LauncherOrigin
        launcherXBox.Value = Math.Max(launcherXBox.Minimum, Math.Min(launcherXBox.Maximum, CDec(Math.Round(draggedOrigin.X, 1))))
        launcherYBox.Value = Math.Max(launcherYBox.Minimum, Math.Min(launcherYBox.Maximum, CDec(Math.Round(draggedOrigin.Y, 1))))
    End Sub

    Private Sub CanvasSelectionChanged(ByVal sender As Object, ByVal e As EventArgs)
        Dim boundaryEditable As Boolean = Not canvas.IsActiveBoundaryLocked
        Dim switchID As Nullable(Of Integer) = canvas.SelectedSwitchID
        switchIDBox.Enabled = switchID.HasValue
        deleteSwitchButton.Enabled = switchID.HasValue
        If switchID.HasValue Then switchIDBox.Value = Math.Max(switchIDBox.Minimum, Math.Min(switchIDBox.Maximum, switchID.Value))

        updatingSegmentBounce = True
        Dim hasSegment As Boolean = canvas.SelectedSegmentIndex >= 0
        Dim segmentOverride As Single = canvas.SelectedSegmentBounceOverride
        segmentUseDefaultCheck.Enabled = hasSegment AndAlso boundaryEditable
        segmentUseDefaultCheck.Checked = Not hasSegment OrElse segmentOverride < 0.0F
        If hasSegment Then
            Dim displayedBounce As Single = If(segmentOverride >= 0.0F, segmentOverride, CSng(bounceBox.Value))
            segmentBounceBox.Value = Math.Max(segmentBounceBox.Minimum, Math.Min(segmentBounceBox.Maximum, CDec(displayedBounce)))
        End If
        segmentBounceBox.Enabled = hasSegment AndAlso boundaryEditable AndAlso Not segmentUseDefaultCheck.Checked
        updatingSegmentBounce = False
    End Sub

    Private Sub SegmentBounceChanged(ByVal sender As Object, ByVal e As EventArgs)
        If updatingSegmentBounce OrElse canvas.SelectedSegmentIndex < 0 Then Return
        Dim useDefault As Boolean = segmentUseDefaultCheck.Checked
        segmentBounceBox.Enabled = Not useDefault AndAlso Not canvas.IsActiveBoundaryLocked
        canvas.SetSelectedSegmentBounce(If(useDefault, -1.0F, CSng(segmentBounceBox.Value)))
    End Sub

    Private Sub ClearBoundaries(ByVal sender As Object, ByVal e As EventArgs)
        canvas.ClearPoints()
    End Sub

    Private Sub NewBoundary(ByVal sender As Object, ByVal e As EventArgs)
        canvas.Paths.Add(New List(Of PointF)())
        canvas.BoundaryNames.Add("Boundary " & canvas.Paths.Count.ToString())
        canvas.BoundaryLocks.Add(False)
        canvas.BoundarySegmentBounces.Add(New List(Of Single)())
        RefreshBoundaryList(canvas.Paths.Count - 1)
    End Sub

    Private Sub DeleteBoundary(ByVal sender As Object, ByVal e As EventArgs)
        If canvas.IsActiveBoundaryLocked Then Return
        If canvas.Paths.Count <= 1 Then
            canvas.ClearPoints()
            Return
        End If
        Dim index As Integer = canvas.ActivePathIndex
        canvas.Paths.RemoveAt(index)
        canvas.BoundaryNames.RemoveAt(index)
        If index < canvas.BoundaryLocks.Count Then canvas.BoundaryLocks.RemoveAt(index)
        If index < canvas.BoundarySegmentBounces.Count Then canvas.BoundarySegmentBounces.RemoveAt(index)
        RefreshBoundaryList(Math.Min(index, canvas.Paths.Count - 1))
    End Sub

    Private Sub RenameBoundary(ByVal sender As Object, ByVal e As EventArgs)
        If canvas.IsActiveBoundaryLocked Then Return
        Dim index As Integer = canvas.ActivePathIndex
        If index < 0 OrElse index >= canvas.BoundaryNames.Count Then Return
        Dim value As String = Microsoft.VisualBasic.Interaction.InputBox("Boundary name:", "Rename Boundary", canvas.BoundaryNames(index)).Replace("|", " ").Trim()
        If value.Length = 0 Then Return
        canvas.BoundaryNames(index) = value
        RefreshBoundaryList(index)
    End Sub

    Private Sub SpliceBoundary(ByVal sender As Object, ByVal e As EventArgs)
        If canvas.IsActiveBoundaryLocked Then Return
        If canvas.SpliceActiveBoundary() Then
            RefreshBoundaryList(canvas.ActivePathIndex)
        Else
            MessageBox.Show(Me, "This boundary needs two intersections with the same older boundary before it can be spliced.",
                            "Splice Into Line", MessageBoxButtons.OK, MessageBoxIcon.Information)
        End If
    End Sub

    Private Sub AddObstacle(ByVal sender As Object, ByVal e As EventArgs)
        canvas.AddObstacle()
    End Sub

    Private Sub DeleteObstacle(ByVal sender As Object, ByVal e As EventArgs)
        canvas.DeleteSelectedObstacle()
    End Sub

    Private Sub AddSwitchZone(ByVal sender As Object, ByVal e As EventArgs)
        canvas.AddSwitchZone(CInt(switchIDBox.Value))
    End Sub

    Private Sub DeleteSwitchZone(ByVal sender As Object, ByVal e As EventArgs)
        canvas.DeleteSelectedSwitchZone()
    End Sub

    Private Sub SwitchIDChanged(ByVal sender As Object, ByVal e As EventArgs)
        If switchIDBox.Enabled Then canvas.SetSelectedSwitchID(CInt(switchIDBox.Value))
    End Sub

    Private Sub ActiveBoundaryChanged(ByVal sender As Object, ByVal e As EventArgs)
        If boundaryBox.SelectedIndex >= 0 Then
            canvas.SetActivePath(boundaryBox.SelectedIndex)
            RefreshBoundaryEditState()
        End If
    End Sub

    Private Sub ToggleBoundaryLock(ByVal sender As Object, ByVal e As EventArgs)
        Dim index As Integer = canvas.ActivePathIndex
        If index < 0 OrElse index >= canvas.Paths.Count Then Return
        canvas.SetBoundaryLocked(index, Not canvas.IsBoundaryLocked(index))
        RefreshBoundaryList(index)
        CanvasSelectionChanged(Nothing, EventArgs.Empty)
    End Sub

    Private Sub CanvasBoundaryStructureChanged(ByVal sender As Object, ByVal e As EventArgs)
        RefreshBoundaryList(canvas.ActivePathIndex)
    End Sub

    Private Sub RefreshBoundaryList(ByVal selectedIndex As Integer)
        boundaryBox.BeginUpdate()
        boundaryBox.Items.Clear()
        For index As Integer = 0 To canvas.BoundaryNames.Count - 1
            Dim prefix As String = If(canvas.IsBoundaryLocked(index), "[Locked] ", String.Empty)
            boundaryBox.Items.Add(prefix & canvas.BoundaryNames(index))
        Next
        boundaryBox.EndUpdate()
        If boundaryBox.Items.Count > 0 Then boundaryBox.SelectedIndex = Math.Max(0, Math.Min(selectedIndex, boundaryBox.Items.Count - 1))
        RefreshBoundaryEditState()
        canvas.Invalidate()
    End Sub

    Private Sub RefreshBoundaryEditState()
        Dim hasBoundary As Boolean = canvas.Paths.Count > 0 AndAlso canvas.ActivePathIndex >= 0
        Dim locked As Boolean = hasBoundary AndAlso canvas.IsActiveBoundaryLocked
        lockBoundaryButton.Enabled = hasBoundary
        lockBoundaryButton.Text = If(locked, "Unlock Boundary", "Lock Boundary")
        deleteBoundaryButton.Enabled = hasBoundary AndAlso Not locked
        renameBoundaryButton.Enabled = hasBoundary AndAlso Not locked
        spliceBoundaryButton.Enabled = hasBoundary AndAlso Not locked
        clearButton.Enabled = hasBoundary AndAlso Not locked
        CanvasSelectionChanged(Nothing, EventArgs.Empty)
    End Sub

    Private Sub SaveBoundaries(ByVal sender As Object, ByVal e As EventArgs)
        If launcherEnabledCheck.Checked AndAlso launcherFollowPivotCheck.Checked AndAlso flipperBox.SelectedIndex <= 0 Then
            MessageBox.Show(Me, "Select a pivot-enabled snippet on the Ball tab before attaching the launcher.", "Physics Boundary Editor",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        If enabledCheck.Checked AndAlso Not canvas.Paths.Any(Function(path) path IsNot Nothing AndAlso path.Count >= 2) Then
            MessageBox.Show(Me, "Enabled ball physics needs at least two boundary points.", "Physics Boundary Editor",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        DialogResult = DialogResult.OK
        Close()
    End Sub

    Private Sub CancelEditor(ByVal sender As Object, ByVal e As EventArgs)
        DialogResult = DialogResult.Cancel
        Close()
    End Sub

    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        If disposing AndAlso canvas.BackglassImage IsNot Nothing Then canvas.BackglassImage.Dispose()
        MyBase.Dispose(disposing)
    End Sub

    Private Class SceneItem
        Public ReadOnly Name As String
        Public ReadOnly Image As Image
        Public Bounds As RectangleF
        Public ReadOnly InitialBounds As RectangleF
        Public ReadOnly IsBall As Boolean

        Public Sub New(ByVal name As String, ByVal image As Image, ByVal bounds As RectangleF, ByVal isBall As Boolean)
            Me.Name = name
            Me.Image = image
            Me.Bounds = bounds
            Me.InitialBounds = bounds
            Me.IsBall = isBall
        End Sub
    End Class

    Private Class PhysicsCanvas
        Inherits Control

        Public BackglassImage As Image
        Public AuthoredSize As Size
        Public ReadOnly Scene As New List(Of SceneItem)()
        Public ReadOnly Paths As New List(Of List(Of PointF))()
        Public ReadOnly BoundaryNames As New List(Of String)()
        Public ReadOnly BoundaryLocks As New List(Of Boolean)()
        Public ReadOnly BoundarySegmentBounces As New List(Of List(Of Single))()
        Public ReadOnly Obstacles As New List(Of RectangleF)()
        Public ReadOnly SwitchZones As New List(Of RectangleF)()
        Public ReadOnly SwitchIDs As New List(Of Integer)()
        Public ReadOnly SwitchAngles As New List(Of Single)()
        Public LauncherEnabled As Boolean
        Public LauncherOrigin As PointF
        Public LauncherAngle As Single
        Public LauncherStrength As Single
        Public LauncherCaptureRadius As Single
        Public Property ActivePathIndex As Integer = 0
        Private selectedIndex As Integer = -1
        Private selectedSegment As Integer = -1
        Private dragging As Boolean
        Private selectedObstacle As Integer = -1
        Private resizingObstacle As Boolean
        Private selectedSwitch As Integer = -1
        Private resizingSwitch As Boolean
        Private rotatingSwitch As Boolean
        Private switchRotationDragOffset As Single
        Private draggingLauncherAngle As Boolean
        Private draggingLauncherOrigin As Boolean
        Private launcherOriginDragOffset As PointF

        Public Event SelectionChanged As EventHandler
        Public Event BoundaryStructureChanged As EventHandler
        Public Event LauncherOriginChanged As EventHandler
        Public Event LauncherAngleChanged As EventHandler

        Public Sub SetBallPreviewLocation(ByVal attached As Boolean, ByVal point As PointF)
            For Each item As SceneItem In Scene
                If Not item.IsBall Then Continue For
                Dim original As RectangleF = item.InitialBounds
                item.Bounds = If(attached,
                                 New RectangleF(point.X - original.Width / 2.0F, point.Y - original.Height / 2.0F,
                                                original.Width, original.Height),
                                 original)
            Next
        End Sub

        Public Sub New()
            DoubleBuffered = True
            SetStyle(ControlStyles.ResizeRedraw Or ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer, True)
            BackColor = Color.Black
            TabStop = True
        End Sub

        Public ReadOnly Property SelectedPoint As Nullable(Of PointF)
            Get
                Dim points As List(Of PointF) = ActivePoints()
                If selectedIndex < 0 OrElse selectedIndex >= points.Count Then Return Nothing
                Return points(selectedIndex)
            End Get
        End Property

        Public ReadOnly Property SelectedSegmentIndex As Integer
            Get
                Return selectedSegment
            End Get
        End Property

        Public ReadOnly Property SelectedSegmentBounceOverride As Single
            Get
                EnsureBoundaryMetadata()
                If ActivePathIndex < 0 OrElse ActivePathIndex >= BoundarySegmentBounces.Count OrElse
                   selectedSegment < 0 OrElse selectedSegment >= BoundarySegmentBounces(ActivePathIndex).Count Then Return -1.0F
                Return BoundarySegmentBounces(ActivePathIndex)(selectedSegment)
            End Get
        End Property

        Public Sub SetSelectedSegmentBounce(ByVal value As Single)
            EnsureBoundaryMetadata()
            If ActivePathIndex < 0 OrElse ActivePathIndex >= BoundarySegmentBounces.Count OrElse
               selectedSegment < 0 OrElse selectedSegment >= BoundarySegmentBounces(ActivePathIndex).Count Then Return
            BoundarySegmentBounces(ActivePathIndex)(selectedSegment) = Math.Max(-1.0F, Math.Min(3.0F, value))
            Invalidate()
        End Sub

        Public ReadOnly Property IsActiveBoundaryLocked As Boolean
            Get
                Return IsBoundaryLocked(ActivePathIndex)
            End Get
        End Property

        Public Function IsBoundaryLocked(ByVal index As Integer) As Boolean
            Return index >= 0 AndAlso index < BoundaryLocks.Count AndAlso BoundaryLocks(index)
        End Function

        Public Sub SetBoundaryLocked(ByVal index As Integer, ByVal locked As Boolean)
            EnsureBoundaryMetadata()
            If index < 0 OrElse index >= BoundaryLocks.Count Then Return
            BoundaryLocks(index) = locked
            selectedIndex = -1
            selectedSegment = -1
            dragging = False
            Invalidate()
            RaiseEvent SelectionChanged(Me, EventArgs.Empty)
        End Sub

        Public ReadOnly Property SelectedSwitchID As Nullable(Of Integer)
            Get
                If selectedSwitch < 0 OrElse selectedSwitch >= SwitchIDs.Count Then Return Nothing
                Return SwitchIDs(selectedSwitch)
            End Get
        End Property

        Public Sub SetActivePath(ByVal index As Integer)
            EnsureBoundaryMetadata()
            If Paths.Count = 0 Then ActivePathIndex = -1 Else ActivePathIndex = Math.Max(0, Math.Min(index, Paths.Count - 1))
            selectedIndex = -1
            selectedSegment = -1
            dragging = False
            selectedObstacle = -1
            selectedSwitch = -1
            Invalidate()
            RaiseEvent SelectionChanged(Me, EventArgs.Empty)
        End Sub

        Public Sub AddObstacle()
            Dim diameter As Single = Math.Max(30.0F, Math.Min(AuthoredSize.Width, AuthoredSize.Height) * 0.08F)
            Obstacles.Add(New RectangleF((AuthoredSize.Width - diameter) / 2.0F, (AuthoredSize.Height - diameter) / 2.0F, diameter, diameter))
            selectedObstacle = Obstacles.Count - 1
            selectedIndex = -1
            selectedSegment = -1
            Invalidate()
            RaiseEvent SelectionChanged(Me, EventArgs.Empty)
        End Sub

        Public Sub DeleteSelectedObstacle()
            If selectedObstacle < 0 OrElse selectedObstacle >= Obstacles.Count Then Return
            Obstacles.RemoveAt(selectedObstacle)
            selectedObstacle = -1
            Invalidate()
            RaiseEvent SelectionChanged(Me, EventArgs.Empty)
        End Sub

        Public Sub AddSwitchZone(ByVal switchID As Integer)
            Dim width As Single = Math.Max(60.0F, AuthoredSize.Width * 0.12F)
            Dim height As Single = Math.Max(40.0F, AuthoredSize.Height * 0.1F)
            SwitchZones.Add(New RectangleF((AuthoredSize.Width - width) / 2.0F, (AuthoredSize.Height - height) / 2.0F, width, height))
            SwitchIDs.Add(Math.Max(1, Math.Min(255, switchID)))
            SwitchAngles.Add(0.0F)
            selectedSwitch = SwitchZones.Count - 1
            selectedObstacle = -1
            selectedIndex = -1
            selectedSegment = -1
            Invalidate()
            RaiseEvent SelectionChanged(Me, EventArgs.Empty)
        End Sub

        Public Sub DeleteSelectedSwitchZone()
            If selectedSwitch < 0 OrElse selectedSwitch >= SwitchZones.Count Then Return
            SwitchZones.RemoveAt(selectedSwitch)
            SwitchIDs.RemoveAt(selectedSwitch)
            If selectedSwitch < SwitchAngles.Count Then SwitchAngles.RemoveAt(selectedSwitch)
            selectedSwitch = -1
            Invalidate()
            RaiseEvent SelectionChanged(Me, EventArgs.Empty)
        End Sub

        Public Sub SetSelectedSwitchID(ByVal switchID As Integer)
            If selectedSwitch < 0 OrElse selectedSwitch >= SwitchIDs.Count Then Return
            SwitchIDs(selectedSwitch) = Math.Max(1, Math.Min(255, switchID))
            Invalidate()
        End Sub

        Public Function SpliceActiveBoundary() As Boolean
            If IsActiveBoundaryLocked Then Return False
            EnsureBoundaryMetadata()
            Dim merged As Boolean = MergeActivePathAtIntersections()
            If merged Then RaiseEvent BoundaryStructureChanged(Me, EventArgs.Empty)
            Return merged
        End Function

        Public Sub DeleteSelectedPoint()
            If IsActiveBoundaryLocked Then Return
            Dim points As List(Of PointF) = ActivePoints()
            If selectedIndex < 0 OrElse selectedIndex >= points.Count Then Return
            Dim values As List(Of Single) = ActiveSegmentBounces()
            If points.Count >= 2 Then
                If selectedIndex = 0 Then
                    If values.Count > 0 Then values.RemoveAt(0)
                ElseIf selectedIndex = points.Count - 1 Then
                    If values.Count > 0 Then values.RemoveAt(values.Count - 1)
                Else
                    Dim leftValue As Single = If(selectedIndex - 1 < values.Count, values(selectedIndex - 1), -1.0F)
                    Dim rightValue As Single = If(selectedIndex < values.Count, values(selectedIndex), -1.0F)
                    If selectedIndex < values.Count Then values.RemoveAt(selectedIndex)
                    If selectedIndex - 1 < values.Count Then values.RemoveAt(selectedIndex - 1)
                    values.Insert(selectedIndex - 1, If(Math.Abs(leftValue - rightValue) < 0.0001F, leftValue, -1.0F))
                End If
            End If
            points.RemoveAt(selectedIndex)
            selectedIndex = Math.Min(selectedIndex, points.Count - 1)
            selectedSegment = -1
            Invalidate()
            RaiseEvent SelectionChanged(Me, EventArgs.Empty)
        End Sub

        Public Sub ClearPoints()
            If IsActiveBoundaryLocked Then Return
            ActivePoints().Clear()
            ActiveSegmentBounces().Clear()
            selectedIndex = -1
            selectedSegment = -1
            Invalidate()
            RaiseEvent SelectionChanged(Me, EventArgs.Empty)
        End Sub

        Protected Overrides Sub OnPaint(ByVal e As PaintEventArgs)
            MyBase.OnPaint(e)
            If BackglassImage Is Nothing OrElse AuthoredSize.Width <= 0 OrElse AuthoredSize.Height <= 0 Then Return
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic
            Dim view As RectangleF = ImageView()
            e.Graphics.DrawImage(BackglassImage, view)
            Dim scale As Single = view.Width / AuthoredSize.Width
            Dim state As GraphicsState = e.Graphics.Save()
            e.Graphics.TranslateTransform(view.X, view.Y)
            e.Graphics.ScaleTransform(scale, scale)

            For Each item As SceneItem In Scene
                e.Graphics.DrawImage(item.Image, item.Bounds)
                If item.IsBall Then
                    Using ballPen As New Pen(Color.Lime, 2.0F / scale)
                        e.Graphics.DrawRectangle(ballPen, item.Bounds.X, item.Bounds.Y, item.Bounds.Width, item.Bounds.Height)
                    End Using
                End If
            Next

            For pathIndex As Integer = 0 To Paths.Count - 1
                Dim path As List(Of PointF) = Paths(pathIndex)
                If path.Count > 1 Then
                    For segmentIndex As Integer = 0 To path.Count - 2
                        Dim boundaryColor As Color
                        Dim hasOverride As Boolean = pathIndex < BoundarySegmentBounces.Count AndAlso
                                                     segmentIndex < BoundarySegmentBounces(pathIndex).Count AndAlso
                                                     BoundarySegmentBounces(pathIndex)(segmentIndex) >= 0.0F
                        If pathIndex = ActivePathIndex AndAlso segmentIndex = selectedSegment Then
                            boundaryColor = Color.Yellow
                        ElseIf hasOverride Then
                            boundaryColor = Color.Lime
                        ElseIf IsBoundaryLocked(pathIndex) Then
                            boundaryColor = If(pathIndex = ActivePathIndex, Color.Gold, Color.FromArgb(210, 150, 150, 150))
                        Else
                            boundaryColor = If(pathIndex = ActivePathIndex, Color.FromArgb(255, 0, 230, 255), Color.FromArgb(210, 255, 80, 190))
                        End If
                        Using shadow As New Pen(Color.Black, 7.0F / scale),
                              boundary As New Pen(boundaryColor, 3.0F / scale)
                            shadow.StartCap = LineCap.Round : shadow.EndCap = LineCap.Round
                            boundary.StartCap = LineCap.Round : boundary.EndCap = LineCap.Round
                            e.Graphics.DrawLine(shadow, path(segmentIndex), path(segmentIndex + 1))
                            e.Graphics.DrawLine(boundary, path(segmentIndex), path(segmentIndex + 1))
                        End Using
                    Next
                End If
            Next
            For obstacleIndex As Integer = 0 To Obstacles.Count - 1
                Dim obstacle As RectangleF = Obstacles(obstacleIndex)
                Using fill As New SolidBrush(Color.FromArgb(55, 255, 170, 0)),
                      outline As New Pen(If(obstacleIndex = selectedObstacle, Color.Yellow, Color.Orange), 3.0F / scale)
                    e.Graphics.FillEllipse(fill, obstacle)
                    e.Graphics.DrawEllipse(outline, obstacle)
                End Using
                If obstacleIndex = selectedObstacle Then
                    Dim handleRadius As Single = 7.0F / scale
                    Dim handle As New RectangleF(obstacle.Right - handleRadius, obstacle.Top + obstacle.Height / 2.0F - handleRadius,
                                                 handleRadius * 2.0F, handleRadius * 2.0F)
                    e.Graphics.FillEllipse(Brushes.Yellow, handle)
                    e.Graphics.DrawEllipse(Pens.Black, handle)
                End If
            Next
            For switchIndex As Integer = 0 To SwitchZones.Count - 1
                Dim zone As RectangleF = SwitchZones(switchIndex)
                Dim angle As Single = SwitchAngle(switchIndex)
                Dim center As New PointF(zone.Left + zone.Width / 2.0F, zone.Top + zone.Height / 2.0F)
                Dim switchState As GraphicsState = e.Graphics.Save()
                e.Graphics.TranslateTransform(center.X, center.Y)
                e.Graphics.RotateTransform(angle)
                e.Graphics.TranslateTransform(-center.X, -center.Y)
                Using fill As New SolidBrush(Color.FromArgb(45, 0, 255, 90)),
                      outline As New Pen(If(switchIndex = selectedSwitch, Color.Yellow, Color.Lime), 3.0F / scale),
                      labelFont As New Font(Font.FontFamily, Math.Max(8.0F, 12.0F / scale), FontStyle.Bold)
                    e.Graphics.FillRectangle(fill, zone)
                    e.Graphics.DrawRectangle(outline, zone.X, zone.Y, zone.Width, zone.Height)
                    e.Graphics.DrawString("SW " & SwitchIDs(switchIndex).ToString(), labelFont, Brushes.White, zone.X + 3.0F / scale, zone.Y + 3.0F / scale)
                End Using
                e.Graphics.Restore(switchState)
                If switchIndex = selectedSwitch Then
                    Dim handleSize As Single = 12.0F / scale
                    Dim resizePoint As PointF = RotateAround(New PointF(zone.Right, zone.Bottom), center, angle)
                    Dim handle As New RectangleF(resizePoint.X - handleSize / 2.0F, resizePoint.Y - handleSize / 2.0F, handleSize, handleSize)
                    e.Graphics.FillRectangle(Brushes.Yellow, handle)
                    e.Graphics.DrawRectangle(Pens.Black, Rectangle.Round(handle))
                    Dim topCenter As PointF = RotateAround(New PointF(center.X, zone.Top), center, angle)
                    Dim rotationPoint As PointF = SwitchRotationHandlePoint(zone, angle, scale)
                    Using handleLine As New Pen(Color.DeepSkyBlue, 2.0F / scale)
                        e.Graphics.DrawLine(handleLine, topCenter, rotationPoint)
                    End Using
                    Dim rotationRadius As Single = 7.0F / scale
                    e.Graphics.FillEllipse(Brushes.DeepSkyBlue, rotationPoint.X - rotationRadius, rotationPoint.Y - rotationRadius,
                                           rotationRadius * 2.0F, rotationRadius * 2.0F)
                    Using handleOutline As New Pen(Color.White, 1.5F / scale)
                        e.Graphics.DrawEllipse(handleOutline, rotationPoint.X - rotationRadius, rotationPoint.Y - rotationRadius,
                                               rotationRadius * 2.0F, rotationRadius * 2.0F)
                    End Using
                End If
            Next
            If LauncherEnabled Then
                Dim radians As Double = LauncherAngle * Math.PI / 180.0R
                Dim directionX As Single = CSng(Math.Cos(radians)), directionY As Single = CSng(Math.Sin(radians))
                Dim tip As PointF = LauncherArrowTip(scale)
                Dim headBase As New PointF(tip.X - directionX * 9.0F / scale, tip.Y - directionY * 9.0F / scale)
                Using launchPen As New Pen(Color.DeepSkyBlue, 2.0F / scale),
                      headBrush As New SolidBrush(Color.DeepSkyBlue)
                    e.Graphics.DrawLine(launchPen, LauncherOrigin, headBase)
                    e.Graphics.FillPolygon(headBrush, New PointF() {
                        tip,
                        New PointF(headBase.X - directionY * 4.0F / scale, headBase.Y + directionX * 4.0F / scale),
                        New PointF(headBase.X + directionY * 4.0F / scale, headBase.Y - directionX * 4.0F / scale)})
                End Using
                Dim launchRadius As Single = 4.0F / scale
                e.Graphics.FillEllipse(Brushes.DeepSkyBlue, LauncherOrigin.X - launchRadius, LauncherOrigin.Y - launchRadius, launchRadius * 2.0F, launchRadius * 2.0F)
                e.Graphics.DrawString("LAUNCH", Font, Brushes.White, LauncherOrigin.X + 10.0F / scale, LauncherOrigin.Y + 4.0F / scale)
                Using capturePen As New Pen(Color.FromArgb(210, 80, 255, 160), 2.0F / scale)
                    capturePen.DashStyle = DashStyle.Dash
                    e.Graphics.DrawEllipse(capturePen, LauncherOrigin.X - LauncherCaptureRadius, LauncherOrigin.Y - LauncherCaptureRadius, LauncherCaptureRadius * 2.0F, LauncherCaptureRadius * 2.0F)
                End Using
            End If
            Dim points As List(Of PointF) = ActivePoints()
            For index As Integer = 0 To points.Count - 1
                Dim radius As Single = 8.0F / scale
                Dim point As PointF = points(index)
                Dim marker As New RectangleF(point.X - radius, point.Y - radius, radius * 2.0F, radius * 2.0F)
                Dim markerColor As Color = If(IsActiveBoundaryLocked, Color.Goldenrod,
                                              If(index = selectedIndex, Color.Yellow, Color.OrangeRed))
                Using fill As New SolidBrush(markerColor), outline As New Pen(Color.White, 2.0F / scale)
                    e.Graphics.FillEllipse(fill, marker)
                    e.Graphics.DrawEllipse(outline, marker)
                End Using
            Next
            e.Graphics.Restore(state)
        End Sub

        Protected Overrides Sub OnMouseDown(ByVal e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            Focus()
            Dim authored As PointF = ClientToAuthored(e.Location)
            If e.Button = MouseButtons.Left AndAlso LauncherEnabled AndAlso HitLauncherOrigin(authored) Then
                draggingLauncherOrigin = True
                launcherOriginDragOffset = New PointF(LauncherOrigin.X - authored.X, LauncherOrigin.Y - authored.Y)
                Capture = True
                Cursor = Cursors.SizeAll
                Return
            End If
            If e.Button = MouseButtons.Left AndAlso LauncherEnabled AndAlso HitLauncherArrow(authored) Then
                draggingLauncherAngle = True
                Capture = True
                Cursor = Cursors.Hand
                Return
            End If
            If e.Button = MouseButtons.Left AndAlso selectedSwitch >= 0 AndAlso selectedSwitch < SwitchZones.Count Then
                Dim selectedZone As RectangleF = SwitchZones(selectedSwitch)
                Dim selectedAngle As Single = SwitchAngle(selectedSwitch)
                If HitSwitchRotationHandle(authored, selectedZone, selectedAngle) Then
                    Dim center As New PointF(selectedZone.Left + selectedZone.Width / 2.0F,
                                             selectedZone.Top + selectedZone.Height / 2.0F)
                    rotatingSwitch = True
                    resizingSwitch = False
                    dragging = True
                    switchRotationDragOffset = NormalizeSwitchAngle(selectedAngle - PointerSwitchAngle(authored, center))
                    Capture = True
                    Cursor = Cursors.Hand
                    Return
                End If
                If HitSwitchHandle(authored, selectedZone, selectedAngle) Then
                    rotatingSwitch = False
                    resizingSwitch = True
                    dragging = True
                    Capture = True
                    Cursor = Cursors.SizeNWSE
                    Return
                End If
            End If
            Dim switchHit As Integer = HitSwitchZone(authored)
            If e.Button = MouseButtons.Left AndAlso switchHit >= 0 Then
                selectedSwitch = switchHit
                selectedObstacle = -1
                selectedIndex = -1
                selectedSegment = -1
                rotatingSwitch = False
                resizingSwitch = HitSwitchHandle(authored, SwitchZones(switchHit), SwitchAngle(switchHit))
                dragging = True
                Capture = True
                Invalidate()
                RaiseEvent SelectionChanged(Me, EventArgs.Empty)
                Return
            End If
            Dim obstacleHit As Integer = HitObstacle(authored)
            If e.Button = MouseButtons.Left AndAlso obstacleHit >= 0 Then
                selectedObstacle = obstacleHit
                selectedSwitch = -1
                selectedIndex = -1
                selectedSegment = -1
                resizingObstacle = HitObstacleHandle(authored, Obstacles(obstacleHit))
                dragging = True
                Invalidate()
                RaiseEvent SelectionChanged(Me, EventArgs.Empty)
                Return
            End If
            If IsActiveBoundaryLocked Then
                selectedIndex = -1
                selectedSegment = -1
                dragging = False
                Invalidate()
                RaiseEvent SelectionChanged(Me, EventArgs.Empty)
                Return
            End If
            Dim hit As Integer = HitPoint(authored)
            If e.Button = MouseButtons.Right Then
                If hit >= 0 Then
                    selectedIndex = hit
                    DeleteSelectedPoint()
                End If
                Return
            End If
            If e.Button <> MouseButtons.Left Then Return
            If hit >= 0 Then
                selectedIndex = hit
                selectedSegment = -1
                selectedObstacle = -1
                selectedSwitch = -1
                dragging = True
            Else
                Dim segmentHit As Integer = HitSegment(authored)
                If segmentHit >= 0 Then
                    selectedIndex = -1
                    selectedSegment = segmentHit
                    selectedObstacle = -1
                    selectedSwitch = -1
                    dragging = False
                ElseIf IsInsideImage(e.Location) Then
                    Dim points As List(Of PointF) = ActivePoints()
                    Dim values As List(Of Single) = ActiveSegmentBounces()
                    If points.Count > 0 Then values.Add(-1.0F)
                    points.Add(ClampPoint(authored))
                    selectedIndex = points.Count - 1
                    selectedSegment = -1
                    selectedObstacle = -1
                    selectedSwitch = -1
                    dragging = True
                End If
            End If
            Invalidate()
            RaiseEvent SelectionChanged(Me, EventArgs.Empty)
        End Sub

        Protected Overrides Sub OnMouseMove(ByVal e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            If draggingLauncherOrigin Then
                Dim point As PointF = ClientToAuthored(e.Location)
                LauncherOrigin = ClampPoint(New PointF(point.X + launcherOriginDragOffset.X,
                                                        point.Y + launcherOriginDragOffset.Y))
                RaiseEvent LauncherOriginChanged(Me, EventArgs.Empty)
                Invalidate()
                Return
            End If
            If draggingLauncherAngle Then
                Dim point As PointF = ClientToAuthored(e.Location)
                If Math.Abs(point.X - LauncherOrigin.X) + Math.Abs(point.Y - LauncherOrigin.Y) > 0.001F Then
                    LauncherAngle = CSng(Math.Atan2(point.Y - LauncherOrigin.Y, point.X - LauncherOrigin.X) * 180.0R / Math.PI)
                    RaiseEvent LauncherAngleChanged(Me, EventArgs.Empty)
                    Invalidate()
                End If
                Return
            End If
            Dim points As List(Of PointF) = ActivePoints()
            If dragging AndAlso selectedSwitch >= 0 AndAlso selectedSwitch < SwitchZones.Count Then
                Dim zone As RectangleF = SwitchZones(selectedSwitch)
                Dim authored As PointF = ClientToAuthored(e.Location)
                Dim angle As Single = SwitchAngle(selectedSwitch)
                Dim center As New PointF(zone.Left + zone.Width / 2.0F, zone.Top + zone.Height / 2.0F)
                If rotatingSwitch Then
                    SwitchAngles(selectedSwitch) = NormalizeSwitchAngle(PointerSwitchAngle(authored, center) + switchRotationDragOffset)
                ElseIf resizingSwitch Then
                    authored = ClampPoint(authored)
                    Dim radians As Double = angle * Math.PI / 180.0R
                    Dim axisX As New PointF(CSng(Math.Cos(radians)), CSng(Math.Sin(radians)))
                    Dim axisY As New PointF(-axisX.Y, axisX.X)
                    Dim fixedCorner As PointF = RotateAround(New PointF(zone.Left, zone.Top), center, angle)
                    Dim deltaX As Single = authored.X - fixedCorner.X
                    Dim deltaY As Single = authored.Y - fixedCorner.Y
                    Dim width As Single = Math.Max(12.0F, deltaX * axisX.X + deltaY * axisX.Y)
                    Dim height As Single = Math.Max(12.0F, deltaX * axisY.X + deltaY * axisY.Y)
                    Dim resizedCenter As New PointF(fixedCorner.X + axisX.X * width / 2.0F + axisY.X * height / 2.0F,
                                                    fixedCorner.Y + axisX.Y * width / 2.0F + axisY.Y * height / 2.0F)
                    SwitchZones(selectedSwitch) = New RectangleF(resizedCenter.X - width / 2.0F, resizedCenter.Y - height / 2.0F,
                                                                  width, height)
                Else
                    Dim radians As Double = angle * Math.PI / 180.0R
                    Dim halfWidth As Single = zone.Width / 2.0F, halfHeight As Single = zone.Height / 2.0F
                    Dim extentX As Single = CSng(Math.Abs(Math.Cos(radians)) * halfWidth + Math.Abs(Math.Sin(radians)) * halfHeight)
                    Dim extentY As Single = CSng(Math.Abs(Math.Sin(radians)) * halfWidth + Math.Abs(Math.Cos(radians)) * halfHeight)
                    Dim centerX As Single = Math.Max(extentX, Math.Min(AuthoredSize.Width - extentX, authored.X))
                    Dim centerY As Single = Math.Max(extentY, Math.Min(AuthoredSize.Height - extentY, authored.Y))
                    SwitchZones(selectedSwitch) = New RectangleF(centerX - halfWidth, centerY - halfHeight, zone.Width, zone.Height)
                End If
                Invalidate()
                Return
            End If
            If dragging AndAlso selectedObstacle >= 0 AndAlso selectedObstacle < Obstacles.Count Then
                Dim obstacle As RectangleF = Obstacles(selectedObstacle)
                Dim authored As PointF = ClampPoint(ClientToAuthored(e.Location))
                If resizingObstacle Then
                    Dim centerX As Single = obstacle.X + obstacle.Width / 2.0F
                    Dim centerY As Single = obstacle.Y + obstacle.Height / 2.0F
                    Dim radius As Single = Math.Max(8.0F, CSng(Math.Sqrt((authored.X - centerX) ^ 2 + (authored.Y - centerY) ^ 2)))
                    Obstacles(selectedObstacle) = New RectangleF(centerX - radius, centerY - radius, radius * 2.0F, radius * 2.0F)
                Else
                    Obstacles(selectedObstacle) = New RectangleF(authored.X - obstacle.Width / 2.0F, authored.Y - obstacle.Height / 2.0F,
                                                                 obstacle.Width, obstacle.Height)
                End If
                Invalidate()
                Return
            End If
            If IsActiveBoundaryLocked Then Return
            If Not dragging OrElse selectedIndex < 0 OrElse selectedIndex >= points.Count Then Return
            points(selectedIndex) = ClampPoint(ClientToAuthored(e.Location))
            Invalidate()
            RaiseEvent SelectionChanged(Me, EventArgs.Empty)
        End Sub

        Protected Overrides Sub OnMouseUp(ByVal e As MouseEventArgs)
            If draggingLauncherAngle OrElse draggingLauncherOrigin Then
                draggingLauncherAngle = False
                draggingLauncherOrigin = False
                Capture = False
                Cursor = Cursors.Default
            End If
            dragging = False
            resizingObstacle = False
            resizingSwitch = False
            rotatingSwitch = False
            Capture = False
            Cursor = Cursors.Default
            MyBase.OnMouseUp(e)
        End Sub

        Private Function HitObstacle(ByVal point As PointF) As Integer
            For index As Integer = Obstacles.Count - 1 To 0 Step -1
                Dim obstacle As RectangleF = Obstacles(index)
                Dim radius As Single = obstacle.Width / 2.0F
                Dim dx As Single = point.X - (obstacle.X + radius)
                Dim dy As Single = point.Y - (obstacle.Y + radius)
                If dx * dx + dy * dy <= (radius + 10.0F) * (radius + 10.0F) Then Return index
            Next
            Return -1
        End Function

        Private Function HitSwitchZone(ByVal point As PointF) As Integer
            For index As Integer = SwitchZones.Count - 1 To 0 Step -1
                If PointInRotatedSwitch(point, SwitchZones(index), SwitchAngle(index)) Then Return index
            Next
            Return -1
        End Function

        Private Function HitSwitchHandle(ByVal point As PointF, ByVal zone As RectangleF, ByVal angle As Single) As Boolean
            Dim center As New PointF(zone.Left + zone.Width / 2.0F, zone.Top + zone.Height / 2.0F)
            Dim handle As PointF = RotateAround(New PointF(zone.Right, zone.Bottom), center, angle)
            Dim scale As Single = Math.Max(0.01F, ImageView().Width / AuthoredSize.Width)
            Dim tolerance As Single = 15.0F / scale
            Return Math.Abs(point.X - handle.X) <= tolerance AndAlso Math.Abs(point.Y - handle.Y) <= tolerance
        End Function

        Private Function HitSwitchRotationHandle(ByVal point As PointF, ByVal zone As RectangleF, ByVal angle As Single) As Boolean
            Dim scale As Single = Math.Max(0.01F, ImageView().Width / AuthoredSize.Width)
            Dim handle As PointF = SwitchRotationHandlePoint(zone, angle, scale)
            Dim radius As Single = 12.0F / scale
            Dim dx As Single = point.X - handle.X, dy As Single = point.Y - handle.Y
            Return dx * dx + dy * dy <= radius * radius
        End Function

        Private Function SwitchRotationHandlePoint(ByVal zone As RectangleF, ByVal angle As Single, ByVal scale As Single) As PointF
            Dim center As New PointF(zone.Left + zone.Width / 2.0F, zone.Top + zone.Height / 2.0F)
            Return RotateAround(New PointF(center.X, zone.Top - 28.0F / Math.Max(0.01F, scale)), center, angle)
        End Function

        Private Function SwitchAngle(ByVal index As Integer) As Single
            Return If(index >= 0 AndAlso index < SwitchAngles.Count, SwitchAngles(index), 0.0F)
        End Function

        Private Shared Function PointInRotatedSwitch(ByVal point As PointF, ByVal zone As RectangleF, ByVal angle As Single) As Boolean
            If Math.Abs(angle) < 0.001F Then Return zone.Contains(point.X, point.Y)
            Dim center As New PointF(zone.Left + zone.Width / 2.0F, zone.Top + zone.Height / 2.0F)
            Dim local As PointF = RotateAround(point, center, -angle)
            Return zone.Contains(local.X, local.Y)
        End Function

        Private Shared Function RotateAround(ByVal point As PointF, ByVal center As PointF, ByVal angle As Single) As PointF
            If Math.Abs(angle) < 0.001F Then Return point
            Dim radians As Double = angle * Math.PI / 180.0R
            Dim cosine As Double = Math.Cos(radians), sine As Double = Math.Sin(radians)
            Dim x As Double = point.X - center.X, y As Double = point.Y - center.Y
            Return New PointF(CSng(center.X + x * cosine - y * sine),
                              CSng(center.Y + x * sine + y * cosine))
        End Function

        Private Shared Function PointerSwitchAngle(ByVal point As PointF, ByVal center As PointF) As Single
            Return CSng(Math.Atan2(point.Y - center.Y, point.X - center.X) * 180.0R / Math.PI + 90.0R)
        End Function

        Private Shared Function NormalizeSwitchAngle(ByVal angle As Single) As Single
            Dim normalized As Single = angle Mod 360.0F
            If normalized > 180.0F Then normalized -= 360.0F
            If normalized <= -180.0F Then normalized += 360.0F
            Return normalized
        End Function

        Private Function HitObstacleHandle(ByVal point As PointF, ByVal obstacle As RectangleF) As Boolean
            Dim dx As Single = point.X - obstacle.Right
            Dim dy As Single = point.Y - (obstacle.Top + obstacle.Height / 2.0F)
            Return dx * dx + dy * dy <= 225.0F
        End Function

        Protected Overrides Sub OnKeyDown(ByVal e As KeyEventArgs)
            If e.KeyCode = Keys.Delete Then
                DeleteSelectedPoint()
                e.Handled = True
            End If
            MyBase.OnKeyDown(e)
        End Sub

        Private Function ImageView() As RectangleF
            Dim scale As Single = Math.Min(ClientSize.Width / CSng(AuthoredSize.Width), ClientSize.Height / CSng(AuthoredSize.Height))
            Dim width As Single = AuthoredSize.Width * scale
            Dim height As Single = AuthoredSize.Height * scale
            Return New RectangleF((ClientSize.Width - width) / 2.0F, (ClientSize.Height - height) / 2.0F, width, height)
        End Function

        Private Function LauncherArrowTip(ByVal scale As Single) As PointF
            Dim radians As Double = LauncherAngle * Math.PI / 180.0R
            Dim length As Single = 40.0F / Math.Max(0.01F, scale)
            Return New PointF(LauncherOrigin.X + CSng(Math.Cos(radians)) * length,
                              LauncherOrigin.Y + CSng(Math.Sin(radians)) * length)
        End Function

        Private Function HitLauncherArrow(ByVal point As PointF) As Boolean
            If AuthoredSize.Width <= 0 Then Return False
            Dim scale As Single = ImageView().Width / AuthoredSize.Width
            Dim tip As PointF = LauncherArrowTip(scale)
            Dim dx As Single = tip.X - LauncherOrigin.X, dy As Single = tip.Y - LauncherOrigin.Y
            Dim lengthSquared As Single = dx * dx + dy * dy
            If lengthSquared <= 0.001F Then Return False
            Dim fraction As Single = Math.Max(0.0F, Math.Min(1.0F,
                ((point.X - LauncherOrigin.X) * dx + (point.Y - LauncherOrigin.Y) * dy) / lengthSquared))
            Dim differenceX As Single = point.X - (LauncherOrigin.X + fraction * dx)
            Dim differenceY As Single = point.Y - (LauncherOrigin.Y + fraction * dy)
            Dim tolerance As Single = 10.0F / Math.Max(0.01F, scale)
            Return differenceX * differenceX + differenceY * differenceY <= tolerance * tolerance
        End Function

        Private Function HitLauncherOrigin(ByVal point As PointF) As Boolean
            If AuthoredSize.Width <= 0 Then Return False
            Dim scale As Single = ImageView().Width / AuthoredSize.Width
            Dim radius As Single = 8.0F / Math.Max(0.01F, scale)
            Dim dx As Single = point.X - LauncherOrigin.X, dy As Single = point.Y - LauncherOrigin.Y
            Return dx * dx + dy * dy <= radius * radius
        End Function

        Private Function ClientToAuthored(ByVal point As Point) As PointF
            Dim view As RectangleF = ImageView()
            Dim scale As Single = view.Width / AuthoredSize.Width
            Return New PointF((point.X - view.X) / scale, (point.Y - view.Y) / scale)
        End Function

        Private Function ClampPoint(ByVal point As PointF) As PointF
            Return New PointF(Math.Max(0.0F, Math.Min(AuthoredSize.Width, point.X)),
                              Math.Max(0.0F, Math.Min(AuthoredSize.Height, point.Y)))
        End Function

        Private Function IsInsideImage(ByVal point As Point) As Boolean
            Return ImageView().Contains(point)
        End Function

        Private Function HitPoint(ByVal point As PointF) As Integer
            Dim view As RectangleF = ImageView()
            Dim scale As Single = view.Width / AuthoredSize.Width
            Dim tolerance As Single = 13.0F / Math.Max(0.01F, scale)
            Dim points As List(Of PointF) = ActivePoints()
            For index As Integer = points.Count - 1 To 0 Step -1
                Dim dx As Single = points(index).X - point.X
                Dim dy As Single = points(index).Y - point.Y
                If dx * dx + dy * dy <= tolerance * tolerance Then Return index
            Next
            Return -1
        End Function

        Private Function HitSegment(ByVal point As PointF) As Integer
            Dim view As RectangleF = ImageView()
            Dim scale As Single = view.Width / AuthoredSize.Width
            Dim tolerance As Single = 8.0F / Math.Max(0.01F, scale)
            Dim toleranceSquared As Single = tolerance * tolerance
            Dim points As List(Of PointF) = ActivePoints()
            For index As Integer = points.Count - 2 To 0 Step -1
                Dim segmentX As Single = points(index + 1).X - points(index).X
                Dim segmentY As Single = points(index + 1).Y - points(index).Y
                Dim lengthSquared As Single = segmentX * segmentX + segmentY * segmentY
                If lengthSquared <= 0.0001F Then Continue For
                Dim fraction As Single = ((point.X - points(index).X) * segmentX + (point.Y - points(index).Y) * segmentY) / lengthSquared
                fraction = Math.Max(0.0F, Math.Min(1.0F, fraction))
                Dim contactX As Single = points(index).X + segmentX * fraction
                Dim contactY As Single = points(index).Y + segmentY * fraction
                Dim dx As Single = point.X - contactX
                Dim dy As Single = point.Y - contactY
                If dx * dx + dy * dy <= toleranceSquared Then Return index
            Next
            Return -1
        End Function

        Private Class PathIntersection
            Public Point As PointF
            Public SourcePosition As Single
            Public TargetPosition As Single
        End Class

        Private Function MergeActivePathAtIntersections() As Boolean
            Dim sourceIndex As Integer = ActivePathIndex
            If sourceIndex <= 0 OrElse sourceIndex >= Paths.Count OrElse Paths(sourceIndex).Count < 2 Then Return False
            Dim source As List(Of PointF) = Paths(sourceIndex)
            Dim sourceBounces As New List(Of Single)(BoundarySegmentBounces(sourceIndex))
            For targetIndex As Integer = sourceIndex - 1 To 0 Step -1
                If IsBoundaryLocked(targetIndex) Then Continue For
                Dim target As List(Of PointF) = Paths(targetIndex)
                Dim targetBounces As New List(Of Single)(BoundarySegmentBounces(targetIndex))
                If target.Count < 2 Then Continue For
                Dim hits As List(Of PathIntersection) = FindIntersections(source, target)
                If hits.Count < 2 Then Continue For

                hits.Sort(Function(left, right) left.SourcePosition.CompareTo(right.SourcePosition))
                Dim first As PathIntersection = hits(0)
                Dim last As PathIntersection = hits(hits.Count - 1)
                If Math.Abs(first.SourcePosition - last.SourcePosition) < 0.001F OrElse
                   Math.Abs(first.TargetPosition - last.TargetPosition) < 0.001F Then Continue For

                Dim leftHit As PathIntersection = If(first.TargetPosition <= last.TargetPosition, first, last)
                Dim rightHit As PathIntersection = If(first.TargetPosition <= last.TargetPosition, last, first)
                Dim merged As New List(Of PointF)()
                For vertex As Integer = 0 To target.Count - 1
                    If vertex >= leftHit.TargetPosition Then Exit For
                    AddUnique(merged, target(vertex))
                Next
                AddUnique(merged, leftHit.Point)
                For Each point As PointF In SlicePath(source, leftHit.SourcePosition, rightHit.SourcePosition, leftHit.Point, rightHit.Point)
                    AddUnique(merged, point)
                Next
                AddUnique(merged, rightHit.Point)
                For vertex As Integer = 0 To target.Count - 1
                    If vertex > rightHit.TargetPosition Then AddUnique(merged, target(vertex))
                Next

                Paths(targetIndex) = merged
                BoundarySegmentBounces(targetIndex) = BuildMergedSegmentBounces(merged, target, targetBounces, source, sourceBounces)
                Paths.RemoveAt(sourceIndex)
                BoundaryNames.RemoveAt(sourceIndex)
                If sourceIndex < BoundaryLocks.Count Then BoundaryLocks.RemoveAt(sourceIndex)
                If sourceIndex < BoundarySegmentBounces.Count Then BoundarySegmentBounces.RemoveAt(sourceIndex)
                ActivePathIndex = targetIndex
                selectedIndex = -1
                Invalidate()
                RaiseEvent SelectionChanged(Me, EventArgs.Empty)
                Return True
            Next
            Return False
        End Function

        Private Function BuildMergedSegmentBounces(ByVal merged As List(Of PointF),
                                                    ByVal target As List(Of PointF), ByVal targetBounces As List(Of Single),
                                                    ByVal source As List(Of PointF), ByVal sourceBounces As List(Of Single)) As List(Of Single)
            Dim result As New List(Of Single)()
            For index As Integer = 0 To merged.Count - 2
                Dim midpoint As New PointF((merged(index).X + merged(index + 1).X) / 2.0F,
                                            (merged(index).Y + merged(index + 1).Y) / 2.0F)
                Dim value As Single = -1.0F
                If Not TryGetBounceAtPoint(midpoint, source, sourceBounces, value) Then
                    TryGetBounceAtPoint(midpoint, target, targetBounces, value)
                End If
                result.Add(value)
            Next
            Return result
        End Function

        Private Function TryGetBounceAtPoint(ByVal point As PointF, ByVal path As List(Of PointF),
                                             ByVal bounces As List(Of Single), ByRef value As Single) As Boolean
            For index As Integer = 0 To path.Count - 2
                Dim segmentX As Single = path(index + 1).X - path(index).X
                Dim segmentY As Single = path(index + 1).Y - path(index).Y
                Dim lengthSquared As Single = segmentX * segmentX + segmentY * segmentY
                If lengthSquared <= 0.0001F Then Continue For
                Dim fraction As Single = ((point.X - path(index).X) * segmentX + (point.Y - path(index).Y) * segmentY) / lengthSquared
                If fraction < -0.0001F OrElse fraction > 1.0001F Then Continue For
                Dim contactX As Single = path(index).X + segmentX * fraction
                Dim contactY As Single = path(index).Y + segmentY * fraction
                Dim dx As Single = point.X - contactX
                Dim dy As Single = point.Y - contactY
                If dx * dx + dy * dy <= 0.25F Then
                    value = If(index < bounces.Count, bounces(index), -1.0F)
                    Return True
                End If
            Next
            Return False
        End Function

        Private Function FindIntersections(ByVal source As List(Of PointF), ByVal target As List(Of PointF)) As List(Of PathIntersection)
            Dim result As New List(Of PathIntersection)()
            For sourceSegment As Integer = 0 To source.Count - 2
                For targetSegment As Integer = 0 To target.Count - 2
                    Dim sourceFraction As Single
                    Dim targetFraction As Single
                    Dim point As PointF
                    If SegmentIntersection(source(sourceSegment), source(sourceSegment + 1),
                                           target(targetSegment), target(targetSegment + 1),
                                           sourceFraction, targetFraction, point) Then
                        Dim duplicate As Boolean = result.Any(Function(hit) DistanceSquared(hit.Point, point) < 0.25F)
                        If Not duplicate Then
                            result.Add(New PathIntersection With {
                                .Point = point,
                                .SourcePosition = sourceSegment + sourceFraction,
                                .TargetPosition = targetSegment + targetFraction
                            })
                        End If
                    End If
                Next
            Next
            Return result
        End Function

        Private Function SegmentIntersection(ByVal a As PointF, ByVal b As PointF, ByVal c As PointF, ByVal d As PointF,
                                             ByRef aFraction As Single, ByRef cFraction As Single, ByRef point As PointF) As Boolean
            Dim rx As Double = b.X - a.X
            Dim ry As Double = b.Y - a.Y
            Dim sx As Double = d.X - c.X
            Dim sy As Double = d.Y - c.Y
            Dim denominator As Double = rx * sy - ry * sx
            If Math.Abs(denominator) < 0.000001 Then Return False
            Dim qx As Double = c.X - a.X
            Dim qy As Double = c.Y - a.Y
            Dim t As Double = (qx * sy - qy * sx) / denominator
            Dim u As Double = (qx * ry - qy * rx) / denominator
            If t < -0.0001 OrElse t > 1.0001 OrElse u < -0.0001 OrElse u > 1.0001 Then Return False
            aFraction = CSng(Math.Max(0.0, Math.Min(1.0, t)))
            cFraction = CSng(Math.Max(0.0, Math.Min(1.0, u)))
            point = New PointF(CSng(a.X + rx * aFraction), CSng(a.Y + ry * aFraction))
            Return True
        End Function

        Private Function SlicePath(ByVal source As List(Of PointF), ByVal startPosition As Single, ByVal endPosition As Single,
                                   ByVal startPoint As PointF, ByVal endPoint As PointF) As List(Of PointF)
            Dim result As New List(Of PointF)()
            AddUnique(result, startPoint)
            If startPosition <= endPosition Then
                For vertex As Integer = 1 To source.Count - 2
                    If vertex > startPosition AndAlso vertex < endPosition Then AddUnique(result, source(vertex))
                Next
            Else
                For vertex As Integer = source.Count - 2 To 1 Step -1
                    If vertex < startPosition AndAlso vertex > endPosition Then AddUnique(result, source(vertex))
                Next
            End If
            AddUnique(result, endPoint)
            Return result
        End Function

        Private Sub AddUnique(ByVal points As List(Of PointF), ByVal point As PointF)
            If points.Count = 0 OrElse DistanceSquared(points(points.Count - 1), point) >= 0.0001F Then points.Add(point)
        End Sub

        Private Function DistanceSquared(ByVal left As PointF, ByVal right As PointF) As Single
            Dim dx As Single = left.X - right.X
            Dim dy As Single = left.Y - right.Y
            Return dx * dx + dy * dy
        End Function

        Private Function ActivePoints() As List(Of PointF)
            If Paths.Count = 0 Then
                Paths.Add(New List(Of PointF)())
                BoundaryNames.Add("Boundary 1")
                BoundaryLocks.Add(False)
                BoundarySegmentBounces.Add(New List(Of Single)())
                ActivePathIndex = 0
            End If
            EnsureBoundaryMetadata()
            ActivePathIndex = Math.Max(0, Math.Min(ActivePathIndex, Paths.Count - 1))
            Return Paths(ActivePathIndex)
        End Function

        Private Function ActiveSegmentBounces() As List(Of Single)
            ActivePoints()
            EnsureBoundaryMetadata()
            Return BoundarySegmentBounces(ActivePathIndex)
        End Function

        Private Sub EnsureBoundaryMetadata()
            While BoundaryNames.Count < Paths.Count
                BoundaryNames.Add("Boundary " & (BoundaryNames.Count + 1).ToString())
            End While
            While BoundaryLocks.Count < Paths.Count
                BoundaryLocks.Add(False)
            End While
            While BoundaryLocks.Count > Paths.Count
                BoundaryLocks.RemoveAt(BoundaryLocks.Count - 1)
            End While
            While BoundarySegmentBounces.Count < Paths.Count
                BoundarySegmentBounces.Add(New List(Of Single)())
            End While
            While BoundarySegmentBounces.Count > Paths.Count
                BoundarySegmentBounces.RemoveAt(BoundarySegmentBounces.Count - 1)
            End While
            For index As Integer = 0 To Paths.Count - 1
                Dim expectedSegments As Integer = Math.Max(0, Paths(index).Count - 1)
                While BoundarySegmentBounces(index).Count < expectedSegments
                    BoundarySegmentBounces(index).Add(-1.0F)
                End While
                While BoundarySegmentBounces(index).Count > expectedSegments
                    BoundarySegmentBounces(index).RemoveAt(BoundarySegmentBounces(index).Count - 1)
                End While
            Next
        End Sub
    End Class
End Class
