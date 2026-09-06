Imports System.Drawing
Imports System.Windows.Forms
Imports System.Runtime.InteropServices
Imports System.Drawing.Drawing2D

Public Module AppThemeManager
    Private Class ControlAppearance
        Public BackColor As Color
        Public ForeColor As Color
        Public UseVisualStyleBackColor As Boolean?
    End Class

    Private Class ToolStripAppearance
        Public BackColor As Color
        Public ForeColor As Color
        Public Renderer As ToolStripRenderer
    End Class

    Private Class RoundedChromeState
        Public Owner As Form
        Public Header As B2SWindowHeader
        Public NativeWindow As RoundedChromeNativeWindow
        Public Accent As Color
        Public OriginalClientSize As Size
        Public OriginalBorderStyle As FormBorderStyle
        Public OriginalPadding As Padding
        Public OriginalMinimumSize As Size
        Public OriginalMaximumSize As Size
        Public ContentOffset As Point
        Public CanResize As Boolean
        Public AdjustingControls As Boolean
        Public ContentControls As New HashSet(Of Control)()
        Public OriginalControlBounds As New Dictionary(Of Control, Rectangle)()
    End Class

    Private ReadOnly savedControls As New Dictionary(Of Control, ControlAppearance)()
    Private ReadOnly themedControls As New HashSet(Of Control)()
    Private ReadOnly savedToolStrips As New Dictionary(Of ToolStrip, ToolStripAppearance)()
    Private ReadOnly themedToolStrips As New HashSet(Of ToolStrip)()
    Private idleHooked As Boolean = False
    Private ReadOnly styledToolWindows As New HashSet(Of Form)()
    Private ReadOnly toolWindowAccents As New Dictionary(Of Form, Color)()
    Private ReadOnly roundedControls As New HashSet(Of Control)()
    Private ReadOnly roundedControlRadii As New Dictionary(Of Control, Integer)()
    Private ReadOnly ownerDrawTabs As New HashSet(Of TabControl)()
    Private ReadOnly styledDialogs As New HashSet(Of Form)()
    Private ReadOnly dialogAccents As New Dictionary(Of Form, Color)()
    Private ReadOnly focusStyledControls As New HashSet(Of Control)()
    Private ReadOnly polishedGroupBoxes As New HashSet(Of GroupBox)()
    Private ReadOnly neonButtons As New HashSet(Of ButtonBase)()
    Private ReadOnly proComboBoxes As New HashSet(Of ComboBox)()
    Private ReadOnly proTrackBars As New HashSet(Of TrackBar)()
    Private ReadOnly stateCheckBoxes As New HashSet(Of CheckBox)()
    Private ReadOnly stateRadioButtons As New HashSet(Of RadioButton)()
    Private ReadOnly proControlAccents As New Dictionary(Of Control, Color)()
    Private ReadOnly proActionButtons As New HashSet(Of ButtonBase)()
    Private ReadOnly roundedChromeStates As New Dictionary(Of Form, RoundedChromeState)()


    ' Phase 1 shared visual tokens. Keep the modern UI consistent everywhere.
    Private ReadOnly SurfaceDark As Color = Color.FromArgb(2, 4, 7)
    Private ReadOnly SurfaceMid As Color = Color.FromArgb(5, 7, 12)
    Private ReadOnly SurfaceRaised As Color = Color.FromArgb(10, 13, 20)
    Private ReadOnly FieldBack As Color = Color.FromArgb(3, 5, 9)
    Private ReadOnly TextPrimary As Color = Color.FromArgb(244, 246, 252)
    Private ReadOnly TextMuted As Color = Color.FromArgb(177, 183, 197)
    Private ReadOnly GoldAccent As Color = Color.FromArgb(255, 174, 24)
    Private ReadOnly PurpleAccent As Color = Color.FromArgb(224, 74, 255)
    Private ReadOnly RedAccent As Color = Color.FromArgb(255, 48, 75)
    Private ReadOnly GreenAccent As Color = Color.FromArgb(54, 229, 94)
    Private ReadOnly CyanAccent As Color = Color.FromArgb(41, 190, 255)

    ' Windows 10/11 DWM caption attributes. Unsupported systems simply ignore these calls.
    Private Const DWMWA_WINDOW_CORNER_PREFERENCE As Integer = 33
    Private Const DWMWA_BORDER_COLOR As Integer = 34
    Private Const DWMWA_CAPTION_COLOR As Integer = 35
    Private Const DWMWA_TEXT_COLOR As Integer = 36
    Private Const DWMWA_COLOR_DEFAULT As Integer = -1

    <DllImport("dwmapi.dll", PreserveSig:=True)>
    Private Function DwmSetWindowAttribute(hwnd As IntPtr, attribute As Integer, ByRef value As Integer, valueSize As Integer) As Integer
    End Function

    <DllImport("uxtheme.dll", CharSet:=CharSet.Unicode, PreserveSig:=True)>
    Private Function SetWindowTheme(hwnd As IntPtr, subAppName As String, subIdList As String) As Integer
    End Function

    <DllImport("user32.dll", PreserveSig:=True)>
    Private Function ReleaseCapture() As Boolean
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Auto, PreserveSig:=True)>
    Private Function SendMessage(hwnd As IntPtr, message As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
    End Function

    Private Const WM_NCLBUTTONDOWN As Integer = &HA1
    Private Const HTCAPTION As Integer = 2

    ' The enhanced designer now has one permanent visual identity.  Keep this
    ' property for existing callers that choose dark owner-draw colors.
    Public ReadOnly Property DarkMode As Boolean
        Get
            Return True
        End Get
    End Property

    Public Sub Initialize()
        If idleHooked Then Return
        AddHandler Application.Idle, AddressOf Application_Idle
        idleHooked = True
    End Sub

    Public Sub SetDarkMode(enabled As Boolean)
        ' Retain the existing entry point for source compatibility.  The enabled
        ' argument is intentionally ignored because Light Mode no longer exists.
        For Each openForm As Form In Application.OpenForms
            ApplyToForm(openForm)
        Next
    End Sub

    Public Sub ApplyToForm(target As Form)
        If target Is Nothing OrElse target.IsDisposed Then Return
        ' The startup splash owns its borderless neon presentation. Treating it
        ' as a normal dialog tries to inject rounded title-bar controls while the
        ' main form is being constructed, which aborts application startup.
        If TypeOf target Is formSplash Then Return
        ApplyDark(target)
        target.Invalidate(True)
    End Sub

    Private Sub Application_Idle(sender As Object, e As EventArgs)
        For Each openForm As Form In Application.OpenForms
            ApplyDark(openForm)
        Next
    End Sub

    Private Function SaveControl(control As Control) As Boolean
        If savedControls.ContainsKey(control) Then Return False
        Dim appearance As New ControlAppearance With {
            .BackColor = control.BackColor,
            .ForeColor = control.ForeColor
        }
        Dim button = TryCast(control, ButtonBase)
        If button IsNot Nothing Then appearance.UseVisualStyleBackColor = button.UseVisualStyleBackColor
        savedControls(control) = appearance
        Return True
    End Function

    Private Sub ApplyDark(control As Control)
        If TypeOf control Is formSplash Then Return
        SaveControl(control)
        Dim isNewControl As Boolean = themedControls.Add(control)

        ' Application.Idle checks for newly opened forms and dynamically added controls.
        ' Do not continuously rewrite colors or renderers on controls that are already themed;
        ' repeatedly resetting ToolStrip hosted controls causes visible flicker.
        If isNewControl Then
        If TypeOf control Is Form Then
            Dim themedForm As Form = DirectCast(control, Form)
            ApplyCaptionTheme(themedForm, True)
            themedForm.Font = New Font("Segoe UI", 9.0F, FontStyle.Regular)
            themedForm.BackColor = SurfaceDark
            themedForm.ForeColor = TextPrimary
        End If

        If TypeOf control Is PictureBox Then
            ' Artwork and previews must retain their exact colors.
        ElseIf TypeOf control Is TextBoxBase OrElse TypeOf control Is ComboBox OrElse TypeOf control Is ListBox OrElse TypeOf control Is ListView OrElse TypeOf control Is TreeView OrElse TypeOf control Is NumericUpDown Then
            control.BackColor = FieldBack
            control.ForeColor = TextPrimary
            control.Font = New Font("Segoe UI", 9.0F, FontStyle.Regular)
            Dim combo = TryCast(control, ComboBox)
            If combo IsNot Nothing Then combo.FlatStyle = FlatStyle.Flat
            Dim numeric = TryCast(control, NumericUpDown)
            If numeric IsNot Nothing Then numeric.BorderStyle = BorderStyle.FixedSingle
            ApplyModernBorder(control)
            ApplyFocusPolish(control)
        ElseIf TypeOf control Is ButtonBase AndAlso Not TypeOf control Is CheckBox AndAlso Not TypeOf control Is RadioButton Then
            control.ForeColor = Color.White
            Dim button = DirectCast(control, ButtonBase)
            button.UseVisualStyleBackColor = False
            button.FlatStyle = FlatStyle.Flat
            Dim buttonAccent As Color = AccentForControl(control)
            If Not IsColorSwatchButton(button) Then button.BackColor = Color.FromArgb(5, 7, 11)
            button.FlatAppearance.BorderColor = Color.FromArgb(205, buttonAccent)
            button.FlatAppearance.MouseOverBackColor = AccentHover(buttonAccent, 74)
            button.FlatAppearance.MouseDownBackColor = AccentHover(buttonAccent, 116)
            button.FlatAppearance.BorderSize = 1
            button.Font = New Font("Segoe UI Semibold", 9.0F, FontStyle.Regular)
            ApplyRoundedControl(button, 8)
            ApplyNeonButton(button)
            ApplyB2SProActionButton(button, buttonAccent)
        ElseIf TypeOf control Is Form OrElse TypeOf control Is Panel OrElse TypeOf control Is GroupBox OrElse TypeOf control Is TabPage OrElse TypeOf control Is SplitContainer OrElse TypeOf control Is FlowLayoutPanel OrElse TypeOf control Is TableLayoutPanel Then
            control.BackColor = SurfaceDark
            control.ForeColor = TextPrimary
            If TypeOf control Is GroupBox Then ApplyGroupBoxPolish(DirectCast(control, GroupBox))
        ElseIf TypeOf control Is Label OrElse TypeOf control Is CheckBox OrElse TypeOf control Is RadioButton Then
            If TypeOf control Is Label AndAlso IsAccentHeader(control) Then
                control.ForeColor = AccentForControl(control)
            Else
                control.ForeColor = TextPrimary
            End If
            If control.BackColor <> Color.Transparent Then control.BackColor = SurfaceDark
            Dim toggle = TryCast(control, ButtonBase)
            If toggle IsNot Nothing Then
                toggle.UseVisualStyleBackColor = False
                toggle.FlatStyle = FlatStyle.Flat
                toggle.FlatAppearance.BorderSize = 0
                toggle.FlatAppearance.MouseOverBackColor = Color.FromArgb(25, AccentForControl(control))
                toggle.FlatAppearance.CheckedBackColor = Color.FromArgb(58, AccentForControl(control))
            End If
            Dim checkBox = TryCast(control, CheckBox)
            If checkBox IsNot Nothing Then ApplyStateCheckBoxTheme(checkBox)
            Dim radioButton = TryCast(control, RadioButton)
            If radioButton IsNot Nothing Then ApplyStateRadioButtonTheme(radioButton)
        End If

        Dim strip = TryCast(control, ToolStrip)
        If strip IsNot Nothing Then ApplyDarkToolStrip(strip)
        If control.ContextMenuStrip IsNot Nothing Then ApplyDarkToolStrip(control.ContextMenuStrip)

        Dim grid = TryCast(control, DataGridView)
        If grid IsNot Nothing Then
            grid.BackgroundColor = SurfaceDark
            grid.GridColor = Color.FromArgb(56, 66, 94)
            grid.DefaultCellStyle.BackColor = Color.FromArgb(24, 27, 40)
            grid.DefaultCellStyle.ForeColor = Color.Gainsboro
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(62, 72, 112)
            grid.DefaultCellStyle.SelectionForeColor = Color.White
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(31, 36, 54)
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.WhiteSmoke
            grid.RowHeadersDefaultCellStyle.BackColor = Color.FromArgb(25, 29, 43)
            grid.EnableHeadersVisualStyles = False
            grid.BorderStyle = BorderStyle.None
        End If

        Dim tabs = TryCast(control, TabControl)
        If tabs IsNot Nothing Then ApplyModernTabs(tabs)

        Dim list = TryCast(control, ListView)
        If list IsNot Nothing Then
            list.BackColor = Color.FromArgb(17, 20, 31)
            list.ForeColor = TextPrimary
            list.BorderStyle = BorderStyle.FixedSingle
            list.FullRowSelect = True
            list.HideSelection = False
        End If
        End If

        For Each child As Control In control.Controls
            ApplyDark(child)
        Next

        Dim form = TryCast(control, Form)
        If form IsNot Nothing Then
            If form.Name.StartsWith("formTool", StringComparison.OrdinalIgnoreCase) Then
                If Not styledToolWindows.Contains(form) Then ApplyB2SProToolWindow(form)
            ElseIf Not form.Name.Equals("formDesigner", StringComparison.OrdinalIgnoreCase) Then
                If Not styledDialogs.Contains(form) Then ApplyB2SProDialog(form)
            End If
        End If
    End Sub

    Private Sub ApplyDarkToolStrip(strip As ToolStrip)
        If Not themedToolStrips.Add(strip) Then Return

        If Not savedToolStrips.ContainsKey(strip) Then
            savedToolStrips(strip) = New ToolStripAppearance With {
                .BackColor = strip.BackColor,
                .ForeColor = strip.ForeColor,
                .Renderer = strip.Renderer
            }
        End If
        strip.BackColor = Color.FromArgb(2, 3, 6)
        strip.ForeColor = TextPrimary
        strip.Padding = New Padding(5, 3, 5, 3)
        If TypeOf strip Is MenuStrip Then
            strip.Font = New Font("Segoe UI", 9.5F, FontStyle.Regular)
            strip.Padding = New Padding(8, 4, 8, 4)
        End If
        strip.Renderer = New DarkToolStripRenderer()
        strip.ShowItemToolTips = True
        If strip.Name.Equals("tsB2SDesigner", StringComparison.OrdinalIgnoreCase) Then
            strip.ImageScalingSize = New Size(30, 30)
            strip.AutoSize = False
            strip.Height = Math.Max(strip.Height, 66)
        End If
        If TypeOf strip Is ToolStripDropDown Then ApplyRoundedControl(strip, 12)
        PolishToolStripItems(strip.Items)
        ApplyToolStripItems(strip.Items, True)
    End Sub

    Private Sub ApplyToolStripItems(items As ToolStripItemCollection, dark As Boolean)
        For Each item As ToolStripItem In items
            If dark Then
                item.BackColor = Color.FromArgb(3, 5, 9)
                item.ForeColor = TextPrimary
                ' Preserve form-specific spacing. The main designer deliberately spreads
                ' its toolbar across the full header width after the theme is applied.
            End If
            Dim menuItem = TryCast(item, ToolStripMenuItem)
            If menuItem IsNot Nothing AndAlso menuItem.HasDropDownItems Then ApplyDarkToolStrip(menuItem.DropDown)
        Next
    End Sub

    ' Phase 4 final polish: predictable tooltips, keyboard focus glow, and
    ' cleaner section framing. These are presentation-only and do not alter events.
    Private Sub PolishToolStripItems(items As ToolStripItemCollection)
        For Each item As ToolStripItem In items
            item.AutoToolTip = True
            If String.IsNullOrWhiteSpace(item.ToolTipText) Then
                Dim fallback As String = If(item.Text, String.Empty).Replace("&", String.Empty).Trim()
                If String.IsNullOrWhiteSpace(fallback) Then fallback = FriendlyToolName(item.Name)
                item.ToolTipText = fallback
            End If

            Dim combo = TryCast(item, ToolStripComboBox)
            If combo IsNot Nothing Then
                combo.FlatStyle = FlatStyle.Flat
                combo.BackColor = FieldBack
                combo.ForeColor = TextPrimary
            End If

            Dim textBox = TryCast(item, ToolStripTextBox)
            If textBox IsNot Nothing Then
                textBox.BackColor = FieldBack
                textBox.ForeColor = TextPrimary
                textBox.BorderStyle = BorderStyle.FixedSingle
            End If

            Dim menuItem = TryCast(item, ToolStripMenuItem)
            If menuItem IsNot Nothing AndAlso menuItem.HasDropDownItems Then
                PolishToolStripItems(menuItem.DropDownItems)
            End If
        Next
    End Sub

    Private Function FriendlyToolName(value As String) As String
        If String.IsNullOrWhiteSpace(value) Then Return String.Empty
        Dim text As String = value
        For Each prefix As String In New String() {"tsb", "tsmi", "tsl", "tscmb", "toolStrip"}
            If text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then
                text = text.Substring(prefix.Length)
                Exit For
            End If
        Next
        If text.Length = 0 Then Return String.Empty
        Dim result As New System.Text.StringBuilder()
        For i As Integer = 0 To text.Length - 1
            Dim ch As Char = text(i)
            If i > 0 AndAlso Char.IsUpper(ch) AndAlso Not Char.IsUpper(text(i - 1)) Then result.Append(" "c)
            result.Append(ch)
        Next
        Return result.ToString().Trim()
    End Function

    Private Sub ApplyFocusPolish(control As Control)
        If control Is Nothing OrElse control.IsDisposed Then Return
        If focusStyledControls.Add(control) Then
            AddHandler control.Enter, AddressOf FocusControl_Enter
            AddHandler control.Leave, AddressOf FocusControl_Leave
            AddHandler control.Disposed, AddressOf FocusControl_Disposed
        End If
    End Sub

    Private Sub FocusControl_Enter(sender As Object, e As EventArgs)
        If Not DarkMode Then Return
        Dim control = TryCast(sender, Control)
        If control Is Nothing OrElse control.IsDisposed OrElse Not control.Enabled Then Return
        control.BackColor = Color.FromArgb(31, 37, 56)
        control.ForeColor = Color.White
        control.Invalidate()
        InvalidateToolWindowFrame(control)
    End Sub

    Private Sub FocusControl_Leave(sender As Object, e As EventArgs)
        If Not DarkMode Then Return
        Dim control = TryCast(sender, Control)
        If control Is Nothing OrElse control.IsDisposed Then Return
        control.BackColor = FieldBack
        control.ForeColor = TextPrimary
        control.Invalidate()
        InvalidateToolWindowFrame(control)
    End Sub

    Private Sub FocusControl_Disposed(sender As Object, e As EventArgs)
        Dim control = TryCast(sender, Control)
        If control IsNot Nothing Then focusStyledControls.Remove(control)
    End Sub

    Private Sub InvalidateToolWindowFrame(control As Control)
        Dim owner As Form = control.FindForm()
        If owner IsNot Nothing AndAlso (styledToolWindows.Contains(owner) OrElse styledDialogs.Contains(owner)) Then owner.Invalidate()
    End Sub

    Private Sub ApplyGroupBoxPolish(group As GroupBox)
        If group Is Nothing OrElse group.IsDisposed Then Return
        group.FlatStyle = FlatStyle.Flat
        If polishedGroupBoxes.Add(group) Then
            AddHandler group.Paint, AddressOf PolishedGroupBox_Paint
            AddHandler group.Disposed, AddressOf PolishedGroupBox_Disposed
        End If
    End Sub

    Private Sub PolishedGroupBox_Disposed(sender As Object, e As EventArgs)
        Dim group = TryCast(sender, GroupBox)
        If group IsNot Nothing Then polishedGroupBoxes.Remove(group)
    End Sub

    Private Sub PolishedGroupBox_Paint(sender As Object, e As PaintEventArgs)
        If Not DarkMode Then Return
        Dim group = TryCast(sender, GroupBox)
        If group Is Nothing OrElse group.ClientSize.Width < 6 OrElse group.ClientSize.Height < 12 Then Return

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
        Dim accent As Color = AccentForControl(group)
        Dim textSize As Size = TextRenderer.MeasureText(group.Text, group.Font)
        Dim borderTop As Integer = Math.Max(7, textSize.Height \ 2)
        Dim bounds As New Rectangle(1, borderTop, group.ClientSize.Width - 3, group.ClientSize.Height - borderTop - 2)

        Using path As GraphicsPath = CreateRoundedRectangle(bounds, 12)
            Using fill As New LinearGradientBrush(bounds,
                                                   Color.FromArgb(14, 18, 26),
                                                   Color.FromArgb(2, 4, 8),
                                                   LinearGradientMode.Vertical)
                e.Graphics.FillPath(fill, path)
            End Using
            Using glow As New Pen(Color.FromArgb(58, accent), 4.0F)
                e.Graphics.DrawPath(glow, path)
            End Using
            Using border As New Pen(Color.FromArgb(205, accent), 1.0F)
                e.Graphics.DrawPath(border, path)
            End Using
        End Using

        Dim textBounds As New Rectangle(12, 0, Math.Max(1, group.ClientSize.Width - 24), textSize.Height + 2)
        Using textBack As New SolidBrush(group.BackColor)
            e.Graphics.FillRectangle(textBack, textBounds)
        End Using
        TextRenderer.DrawText(e.Graphics, group.Text, group.Font, textBounds, accent, TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis)
    End Sub

    Private Sub ApplyModernBorder(control As Control)
        Dim textBox = TryCast(control, TextBoxBase)
        If textBox IsNot Nothing Then
            textBox.BorderStyle = BorderStyle.FixedSingle
            Return
        End If

        Dim listBox = TryCast(control, ListBox)
        If listBox IsNot Nothing Then listBox.BorderStyle = BorderStyle.FixedSingle
    End Sub

    Private Function IsColorSwatchButton(button As ButtonBase) As Boolean
        If button Is Nothing Then Return False
        Dim key As String = (If(button.Name, String.Empty) & " " & If(button.Text, String.Empty)).ToLowerInvariant()
        Return key.Contains("color") AndAlso String.IsNullOrWhiteSpace(button.Text) AndAlso button.Image Is Nothing
    End Function

    Private Sub ApplyNeonButton(button As ButtonBase)
        If button Is Nothing OrElse button.IsDisposed OrElse IsColorSwatchButton(button) Then Return
        If neonButtons.Add(button) Then
            AddHandler button.Paint, AddressOf NeonButton_Paint
            AddHandler button.Disposed, AddressOf NeonButton_Disposed
        End If
    End Sub

    Private Sub NeonButton_Disposed(sender As Object, e As EventArgs)
        Dim button = TryCast(sender, ButtonBase)
        If button IsNot Nothing Then neonButtons.Remove(button)
    End Sub

    Private Sub NeonButton_Paint(sender As Object, e As PaintEventArgs)
        If Not DarkMode Then Return
        Dim button = TryCast(sender, ButtonBase)
        If button Is Nothing OrElse button.ClientSize.Width < 5 OrElse button.ClientSize.Height < 5 Then Return

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
        Dim accent As Color = If(button.Enabled, AccentForControl(button), Color.FromArgb(92, 98, 112))
        Dim bounds As New Rectangle(1, 1, button.ClientSize.Width - 3, button.ClientSize.Height - 3)
        Using path As GraphicsPath = CreateRoundedRectangle(bounds, ButtonCornerRadius(button))
            Using glow As New Pen(Color.FromArgb(58, accent), 3.0F)
                e.Graphics.DrawPath(glow, path)
            End Using
            Using border As New Pen(Color.FromArgb(225, accent), 1.0F)
                e.Graphics.DrawPath(border, path)
            End Using
        End Using
    End Sub

    Private Sub ApplyRoundedControl(control As Control, radius As Integer)
        If control Is Nothing OrElse control.IsDisposed Then Return
        roundedControlRadii(control) = Math.Max(2, radius)
        If roundedControls.Add(control) Then
            AddHandler control.SizeChanged, AddressOf RoundedControl_SizeChanged
            AddHandler control.Disposed, AddressOf RoundedControl_Disposed
        End If
        SetRoundedRegion(control, roundedControlRadii(control))
    End Sub

    Private Sub RoundedControl_SizeChanged(sender As Object, e As EventArgs)
        Dim control = TryCast(sender, Control)
        If control Is Nothing Then Return
        Dim radius As Integer = 8
        roundedControlRadii.TryGetValue(control, radius)
        SetRoundedRegion(control, radius)
    End Sub

    Private Sub RoundedControl_Disposed(sender As Object, e As EventArgs)
        Dim control = TryCast(sender, Control)
        If control IsNot Nothing Then
            roundedControls.Remove(control)
            roundedControlRadii.Remove(control)
        End If
    End Sub

    Private Sub SetRoundedRegion(control As Control, radius As Integer)
        If control.ClientSize.Width < 2 OrElse control.ClientSize.Height < 2 Then Return
        Using path As GraphicsPath = CreateRoundedRectangle(New Rectangle(0, 0, control.ClientSize.Width - 1, control.ClientSize.Height - 1), radius)
            Dim oldRegion As Region = control.Region
            control.Region = New Region(path)
            If oldRegion IsNot Nothing Then oldRegion.Dispose()
        End Using
    End Sub

    Private Function ButtonCornerRadius(button As ButtonBase) As Integer
        If button Is Nothing Then Return 10
        Return Math.Max(8, Math.Min(16, (Math.Min(button.ClientSize.Width, button.ClientSize.Height) - 2) \ 2))
    End Function

    Private Sub ApplyModernTabs(tabs As TabControl)
        tabs.BackColor = SurfaceDark
        tabs.ForeColor = TextPrimary
        tabs.DrawMode = TabDrawMode.OwnerDrawFixed
        tabs.SizeMode = TabSizeMode.Normal
        tabs.Padding = New Point(16, 6)
        If ownerDrawTabs.Add(tabs) Then
            AddHandler tabs.DrawItem, AddressOf ModernTabs_DrawItem
            AddHandler tabs.Disposed, AddressOf ModernTabs_Disposed
        End If
    End Sub

    Private Sub ModernTabs_Disposed(sender As Object, e As EventArgs)
        Dim tabs = TryCast(sender, TabControl)
        If tabs IsNot Nothing Then ownerDrawTabs.Remove(tabs)
    End Sub

    Private Sub ModernTabs_DrawItem(sender As Object, e As DrawItemEventArgs)
        Dim tabs = TryCast(sender, TabControl)
        If tabs Is Nothing OrElse e.Index < 0 OrElse e.Index >= tabs.TabPages.Count Then Return

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
        Dim selected As Boolean = (e.Index = tabs.SelectedIndex)
        Dim bounds As Rectangle = Rectangle.Inflate(e.Bounds, -2, -2)
        Dim accent As Color = AccentForControl(tabs.TabPages(e.Index))
        Using path As GraphicsPath = CreateRoundedRectangle(bounds, 10)
            Dim top As Color = If(selected, Color.FromArgb(118, accent), Color.FromArgb(18, 21, 30))
            Dim bottom As Color = If(selected, Color.FromArgb(31, accent), Color.FromArgb(3, 5, 9))
            If selected Then
                Using glow As New Pen(Color.FromArgb(62, accent), 4.0F)
                    e.Graphics.DrawPath(glow, path)
                End Using
            End If
            Using brush As New LinearGradientBrush(bounds, top, bottom, LinearGradientMode.Vertical)
                e.Graphics.FillPath(brush, path)
            End Using
            Using pen As New Pen(Color.FromArgb(If(selected, 235, 88), accent), If(selected, 1.4F, 1.0F))
                e.Graphics.DrawPath(pen, path)
            End Using
        End Using

        Using tabFont As New Font("Segoe UI Semibold", 9.0F)
            TextRenderer.DrawText(e.Graphics, tabs.TabPages(e.Index).Text, tabFont, bounds, TextPrimary, TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis)
        End Using
    End Sub

    Private Function CreateRoundedRectangle(bounds As Rectangle, radius As Integer) As GraphicsPath
        Dim path As New GraphicsPath()
        Dim diameter As Integer = Math.Max(2, radius * 2)
        Dim arc As New Rectangle(bounds.Location, New Size(diameter, diameter))
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

    Private Function IsAccentHeader(control As Control) As Boolean
        Dim label = TryCast(control, Label)
        If label Is Nothing Then Return False
        If label.Font IsNot Nothing AndAlso label.Font.Bold Then Return True
        Dim text As String = If(label.Text, String.Empty).Trim().ToLowerInvariant()
        Return text = "layers" OrElse text = "properties" OrElse text = "illumination" OrElse
               text.Contains("reels") OrElse text.Contains("leds") OrElse text = "resources"
    End Function

    Private Function AccentForControl(control As Control) As Color
        Dim key As String = (If(control.Name, String.Empty) & " " & If(control.Text, String.Empty)).ToLowerInvariant()
        If key.Contains("play") OrElse key.Contains("preview") OrElse key.Contains("run") Then Return Color.FromArgb(55, 235, 92)
        If key.Contains("export") OrElse key.Contains("saveas") OrElse key.Contains("delete") OrElse key.Contains("remove") Then Return Color.FromArgb(255, 70, 96)
        If key.Contains("illumin") OrElse key.Contains("light") OrElse key.Contains("bulb") OrElse key.Contains("flasher") Then Return Color.FromArgb(255, 171, 38)
        If key.Contains("layer") OrElse key.Contains("history") OrElse key.Contains("undo") OrElse key.Contains("redo") Then Return Color.FromArgb(190, 82, 255)
        If key.Contains("reel") OrElse key.Contains("led") OrElse key.Contains("score") OrElse key.Contains("animation") Then Return Color.FromArgb(52, 225, 138)
        If key.Contains("image") OrElse key.Contains("resource") OrElse key.Contains("palette") OrElse key.Contains("zoom") OrElse key.Contains("grid") OrElse key.Contains("ruler") Then Return Color.FromArgb(38, 190, 255)
        If key.Contains("mask") OrElse key.Contains("selection") OrElse key.Contains("select") OrElse key.Contains("snipp") Then Return Color.FromArgb(224, 74, 255)
        If key.Contains("new") OrElse key.Contains("open") Then Return Color.FromArgb(54, 205, 255)
        If key.Contains("save") Then Return Color.FromArgb(129, 235, 72)
        Return Color.FromArgb(86, 145, 255)
    End Function

    Private Function AccentHover(accent As Color, strength As Integer) As Color
        ' Button BackColor must remain fully opaque in WinForms. Blend the accent
        ' into the dark surface instead of using alpha transparency.
        Dim amount As Integer = Math.Max(0, Math.Min(255, strength))
        Dim inverse As Integer = 255 - amount
        Return Color.FromArgb((30 * inverse + accent.R * amount) \ 255,
                              (34 * inverse + accent.G * amount) \ 255,
                              (50 * inverse + accent.B * amount) \ 255)
    End Function

    Private Class B2SWindowHeader
        Inherits Control

        Private ReadOnly ownerForm As Form
        Private ReadOnly accentColor As Color
        Private ReadOnly showMinimize As Boolean
        Private ReadOnly showMaximize As Boolean
        Private hoverButton As Integer = -1
        Private pressedButton As Integer = -1

        Public Sub New(owner As Form, accent As Color, minimizeBox As Boolean, maximizeBox As Boolean)
            ownerForm = owner
            accentColor = accent
            showMinimize = minimizeBox
            showMaximize = maximizeBox
            Name = "__b2sRoundedWindowHeader"
            TabStop = False
            BackColor = SurfaceDark
            ForeColor = TextPrimary
            Cursor = Cursors.Default
            SetStyle(ControlStyles.AllPaintingInWmPaint Or
                     ControlStyles.OptimizedDoubleBuffer Or
                     ControlStyles.ResizeRedraw Or
                     ControlStyles.UserPaint, True)
        End Sub

        Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
            e.Graphics.Clear(SurfaceDark)
        End Sub

        Protected Overrides Sub OnPaint(e As PaintEventArgs)
            MyBase.OnPaint(e)
            If Width < 30 OrElse Height < 20 Then Return

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
            Dim bounds As New Rectangle(0, 0, Width - 1, Height - 1)
            Using path As GraphicsPath = CreateRoundedRectangle(bounds, 7)
                Using fill As New LinearGradientBrush(bounds,
                                                       BlendColor(Color.FromArgb(16, 18, 25), accentColor, 0.48F),
                                                       BlendColor(Color.FromArgb(1, 2, 4), accentColor, 0.16F),
                                                       LinearGradientMode.Vertical)
                    e.Graphics.FillPath(fill, path)
                End Using
                Using border As New Pen(Color.FromArgb(225, accentColor), 1.0F)
                    e.Graphics.DrawPath(border, path)
                End Using
            End Using
            Using gloss As New Pen(Color.FromArgb(115, Color.White), 1.0F)
                e.Graphics.DrawLine(gloss, 8, 2, Math.Max(8, Width - 9), 2)
            End Using

            DrawWindowGlyph(e.Graphics, New Point(14, Height \ 2), ownerForm, accentColor)

            Dim buttonCount As Integer = 1 + If(showMaximize, 1, 0) + If(showMinimize, 1, 0)
            Dim titleRight As Integer = Width - 6 - (buttonCount * 26)
            Dim titleBounds As New Rectangle(28, 0, Math.Max(20, titleRight - 28), Height)
            Using titleFont As New Font("Arial Black", 9.25F, FontStyle.Regular)
                TextRenderer.DrawText(e.Graphics,
                                      If(ownerForm.Text, String.Empty).ToUpperInvariant(),
                                      titleFont,
                                      titleBounds,
                                      Color.White,
                                      TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix)
            End Using

            Dim buttonIndex As Integer = 0
            DrawCaptionButton(e.Graphics, buttonIndex, CaptionButtonBounds(buttonIndex), "close")
            buttonIndex += 1
            If showMaximize Then
                DrawCaptionButton(e.Graphics, buttonIndex, CaptionButtonBounds(buttonIndex), "maximize")
                buttonIndex += 1
            End If
            If showMinimize Then DrawCaptionButton(e.Graphics, buttonIndex, CaptionButtonBounds(buttonIndex), "minimize")
        End Sub

        Private Sub DrawCaptionButton(graphics As Graphics, index As Integer, bounds As Rectangle, kind As String)
            Dim isHovered As Boolean = (hoverButton = index)
            Dim isPressed As Boolean = (pressedButton = index)
            Dim buttonAccent As Color = If(kind = "close" AndAlso isHovered, RedAccent, accentColor)

            If isHovered OrElse isPressed Then
                Using path As GraphicsPath = CreateRoundedRectangle(bounds, 9)
                    Using fill As New SolidBrush(Color.FromArgb(If(isPressed, 104, 62), buttonAccent))
                        graphics.FillPath(fill, path)
                    End Using
                    Using glow As New Pen(Color.FromArgb(150, buttonAccent), 1.0F)
                        graphics.DrawPath(glow, path)
                    End Using
                End Using
            End If

            Dim centerX As Integer = bounds.Left + bounds.Width \ 2
            Dim centerY As Integer = bounds.Top + bounds.Height \ 2
            Using glyph As New Pen(If(isHovered, Color.White, Color.FromArgb(238, buttonAccent)), 1.35F)
                glyph.StartCap = LineCap.Round
                glyph.EndCap = LineCap.Round
                Select Case kind
                    Case "close"
                        graphics.DrawLine(glyph, centerX - 4, centerY - 4, centerX + 4, centerY + 4)
                        graphics.DrawLine(glyph, centerX + 4, centerY - 4, centerX - 4, centerY + 4)
                    Case "maximize"
                        If ownerForm.WindowState = FormWindowState.Maximized Then
                            graphics.DrawRectangle(glyph, centerX - 3, centerY - 5, 8, 8)
                            graphics.DrawRectangle(glyph, centerX - 6, centerY - 2, 8, 8)
                        Else
                            graphics.DrawRectangle(glyph, centerX - 5, centerY - 5, 10, 10)
                        End If
                    Case "minimize"
                        graphics.DrawLine(glyph, centerX - 5, centerY + 4, centerX + 5, centerY + 4)
                End Select
            End Using
        End Sub

        Private Function CaptionButtonBounds(index As Integer) As Rectangle
            Return New Rectangle(Width - 4 - ((index + 1) * 26), 4, 23, Math.Max(17, Height - 8))
        End Function

        Private Function HitCaptionButton(location As Point) As Integer
            Dim count As Integer = 1 + If(showMaximize, 1, 0) + If(showMinimize, 1, 0)
            For index As Integer = 0 To count - 1
                If CaptionButtonBounds(index).Contains(location) Then Return index
            Next
            Return -1
        End Function

        Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
            MyBase.OnMouseMove(e)
            Dim nextHover As Integer = HitCaptionButton(e.Location)
            If nextHover <> hoverButton Then
                hoverButton = nextHover
                Invalidate()
            End If
        End Sub

        Protected Overrides Sub OnMouseLeave(e As EventArgs)
            MyBase.OnMouseLeave(e)
            If hoverButton <> -1 Then
                hoverButton = -1
                Invalidate()
            End If
        End Sub

        Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
            MyBase.OnMouseDown(e)
            If e.Button <> MouseButtons.Left Then Return
            pressedButton = HitCaptionButton(e.Location)
            If pressedButton >= 0 Then
                Invalidate()
                Return
            End If

            ReleaseCapture()
            SendMessage(ownerForm.Handle, WM_NCLBUTTONDOWN, New IntPtr(HTCAPTION), IntPtr.Zero)
        End Sub

        Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
            MyBase.OnMouseUp(e)
            If e.Button <> MouseButtons.Left Then Return
            Dim releasedButton As Integer = HitCaptionButton(e.Location)
            Dim actionButton As Integer = pressedButton
            pressedButton = -1
            Invalidate()
            If actionButton < 0 OrElse actionButton <> releasedButton Then Return

            If actionButton = 0 Then
                ownerForm.Close()
                Return
            End If

            Dim nextIndex As Integer = 1
            If showMaximize Then
                If actionButton = nextIndex Then
                    ownerForm.WindowState = If(ownerForm.WindowState = FormWindowState.Maximized,
                                               FormWindowState.Normal,
                                               FormWindowState.Maximized)
                    Invalidate()
                    Return
                End If
                nextIndex += 1
            End If
            If showMinimize AndAlso actionButton = nextIndex Then ownerForm.WindowState = FormWindowState.Minimized
        End Sub

        Protected Overrides Sub OnMouseDoubleClick(e As MouseEventArgs)
            MyBase.OnMouseDoubleClick(e)
            If e.Button = MouseButtons.Left AndAlso showMaximize AndAlso HitCaptionButton(e.Location) < 0 Then
                ownerForm.WindowState = If(ownerForm.WindowState = FormWindowState.Maximized,
                                           FormWindowState.Normal,
                                           FormWindowState.Maximized)
                Invalidate()
            End If
        End Sub

        Private Shared Function BlendColor(baseColor As Color, overlay As Color, amount As Single) As Color
            amount = Math.Max(0.0F, Math.Min(1.0F, amount))
            Return Color.FromArgb(255,
                                  CInt(baseColor.R + (overlay.R - baseColor.R) * amount),
                                  CInt(baseColor.G + (overlay.G - baseColor.G) * amount),
                                  CInt(baseColor.B + (overlay.B - baseColor.B) * amount))
        End Function
    End Class

    Private Class RoundedChromeNativeWindow
        Inherits NativeWindow

        Private ReadOnly ownerForm As Form
        Private ReadOnly resizeEnabled As Boolean
        Private Const WM_NCHITTEST As Integer = &H84
        Private Const HTCLIENT As Integer = 1
        Private Const HTLEFT As Integer = 10
        Private Const HTRIGHT As Integer = 11
        Private Const HTTOP As Integer = 12
        Private Const HTTOPLEFT As Integer = 13
        Private Const HTTOPRIGHT As Integer = 14
        Private Const HTBOTTOM As Integer = 15
        Private Const HTBOTTOMLEFT As Integer = 16
        Private Const HTBOTTOMRIGHT As Integer = 17

        Public Sub New(owner As Form, canResize As Boolean)
            ownerForm = owner
            resizeEnabled = canResize
            AssignHandle(owner.Handle)
        End Sub

        Protected Overrides Sub WndProc(ByRef message As Message)
            MyBase.WndProc(message)
            If message.Msg <> WM_NCHITTEST OrElse Not resizeEnabled OrElse
               ownerForm.WindowState <> FormWindowState.Normal OrElse
               message.Result <> New IntPtr(HTCLIENT) Then Return

            Dim cursorPoint As Point = ownerForm.PointToClient(Control.MousePosition)
            Dim grip As Integer = Math.Max(6, CInt(Math.Round(7.0F * ownerForm.DeviceDpi / 96.0F)))
            Dim left As Boolean = cursorPoint.X <= grip
            Dim right As Boolean = cursorPoint.X >= ownerForm.ClientSize.Width - grip
            Dim top As Boolean = cursorPoint.Y <= grip
            Dim bottom As Boolean = cursorPoint.Y >= ownerForm.ClientSize.Height - grip

            If left AndAlso top Then
                message.Result = New IntPtr(HTTOPLEFT)
            ElseIf right AndAlso top Then
                message.Result = New IntPtr(HTTOPRIGHT)
            ElseIf left AndAlso bottom Then
                message.Result = New IntPtr(HTBOTTOMLEFT)
            ElseIf right AndAlso bottom Then
                message.Result = New IntPtr(HTBOTTOMRIGHT)
            ElseIf left Then
                message.Result = New IntPtr(HTLEFT)
            ElseIf right Then
                message.Result = New IntPtr(HTRIGHT)
            ElseIf top Then
                message.Result = New IntPtr(HTTOP)
            ElseIf bottom Then
                message.Result = New IntPtr(HTBOTTOM)
            End If
        End Sub

        Public Sub Detach()
            If Handle <> IntPtr.Zero Then ReleaseHandle()
        End Sub
    End Class

    Private Sub DrawWindowGlyph(graphics As Graphics, center As Point, owner As Form, accent As Color)
        Dim key As String = (If(owner.Name, String.Empty) & " " & If(owner.Text, String.Empty)).ToLowerInvariant()
        Using pen As New Pen(accent, 1.35F)
            pen.StartCap = LineCap.Round
            pen.EndCap = LineCap.Round
            If key.Contains("illum") OrElse key.Contains("light") Then
                graphics.DrawEllipse(pen, center.X - 5, center.Y - 5, 10, 10)
                For angle As Integer = 0 To 315 Step 45
                    Dim radians As Double = angle * Math.PI / 180.0R
                    graphics.DrawLine(pen,
                                      center.X + CInt(Math.Cos(radians) * 8),
                                      center.Y + CInt(Math.Sin(radians) * 8),
                                      center.X + CInt(Math.Cos(radians) * 11),
                                      center.Y + CInt(Math.Sin(radians) * 11))
                Next
            ElseIf key.Contains("layer") OrElse key.Contains("history") OrElse key.Contains("undo") Then
                For offset As Integer = -5 To 5 Step 5
                    graphics.DrawLine(pen, center.X - 8, center.Y + offset, center.X + 8, center.Y + offset)
                Next
            ElseIf key.Contains("reel") OrElse key.Contains("led") OrElse key.Contains("resource") OrElse key.Contains("image") Then
                graphics.DrawRectangle(pen, center.X - 8, center.Y - 7, 16, 14)
                graphics.DrawLines(pen, New Point() {
                                       New Point(center.X - 6, center.Y + 5),
                                       New Point(center.X - 1, center.Y),
                                       New Point(center.X + 3, center.Y + 3)})
                graphics.DrawEllipse(pen, center.X + 2, center.Y - 4, 3, 3)
            Else
                graphics.DrawEllipse(pen, center.X - 5, center.Y - 5, 10, 10)
                graphics.DrawLine(pen, center.X, center.Y - 9, center.X, center.Y + 9)
                graphics.DrawLine(pen, center.X - 9, center.Y, center.X + 9, center.Y)
            End If
        End Using
    End Sub

    Private Sub ApplyRoundedWindowChrome(target As Form, accent As Color)
        If target Is Nothing OrElse target.IsDisposed OrElse roundedChromeStates.ContainsKey(target) Then Return

        Dim oldClientSize As Size = target.ClientSize
        Dim oldOuterSize As Size = target.Size
        Dim nativeFrameWidth As Integer = Math.Max(6, oldOuterSize.Width - oldClientSize.Width)
        Dim nativeFrameHeight As Integer = Math.Max(30, oldOuterSize.Height - oldClientSize.Height)
        Dim frameInsetX As Integer = Math.Max(3, nativeFrameWidth \ 2)
        Dim frameInsetY As Integer = 3
        ' Compact studio-panel chrome: a shallow title tab instead of the former
        ' oversized floating-window caption.
        Dim captionHeight As Integer = 29
        Dim contentOffset As New Point(frameInsetX, frameInsetY + captionHeight)
        Dim oldPadding As Padding = target.Padding
        Dim oldMinimum As Size = target.MinimumSize
        Dim oldMaximum As Size = target.MaximumSize
        Dim originalBorder As FormBorderStyle = target.FormBorderStyle
        Dim canResize As Boolean = (originalBorder = FormBorderStyle.Sizable OrElse originalBorder = FormBorderStyle.SizableToolWindow)
        Dim showMinimize As Boolean = target.ControlBox AndAlso target.MinimizeBox
        Dim showMaximize As Boolean = target.ControlBox AndAlso target.MaximizeBox AndAlso canResize
        Dim originalControls As New List(Of KeyValuePair(Of Control, Rectangle))()
        For Each child As Control In target.Controls
            originalControls.Add(New KeyValuePair(Of Control, Rectangle)(child, child.Bounds))
        Next

        Dim state As New RoundedChromeState With {
            .Owner = target,
            .Accent = accent,
            .OriginalClientSize = oldClientSize,
            .OriginalBorderStyle = originalBorder,
            .OriginalPadding = oldPadding,
            .OriginalMinimumSize = oldMinimum,
            .OriginalMaximumSize = oldMaximum,
            .ContentOffset = contentOffset,
            .CanResize = canResize,
            .AdjustingControls = True
        }
        For Each item As KeyValuePair(Of Control, Rectangle) In originalControls
            state.ContentControls.Add(item.Key)
            state.OriginalControlBounds(item.Key) = item.Value
        Next
        roundedChromeStates(target) = state

        target.SuspendLayout()
        Try
            target.FormBorderStyle = FormBorderStyle.None
            ' Keep the original outer window dimensions. This preserves every
            ' saved window size and prevents tool windows from growing each time
            ' their geometry is restored on a later launch.
            target.ClientSize = oldOuterSize
            target.Padding = New Padding(oldPadding.Left + frameInsetX,
                                         oldPadding.Top + contentOffset.Y,
                                         oldPadding.Right + frameInsetX,
                                         oldPadding.Bottom + frameInsetY)

            For Each item As KeyValuePair(Of Control, Rectangle) In originalControls
                If item.Key.Dock = DockStyle.None Then
                    Dim translated As Rectangle = item.Value
                    translated.Offset(contentOffset)
                    item.Key.Bounds = translated
                End If
            Next

            Dim sizeDelta As New Size(target.Width - oldOuterSize.Width, target.Height - oldOuterSize.Height)
            If oldMinimum.Width > 0 OrElse oldMinimum.Height > 0 Then
                target.MinimumSize = New Size(Math.Max(0, oldMinimum.Width + sizeDelta.Width),
                                              Math.Max(0, oldMinimum.Height + sizeDelta.Height))
            End If
            If oldMaximum.Width > 0 OrElse oldMaximum.Height > 0 Then
                target.MaximumSize = New Size(If(oldMaximum.Width > 0, Math.Max(1, oldMaximum.Width + sizeDelta.Width), 0),
                                              If(oldMaximum.Height > 0, Math.Max(1, oldMaximum.Height + sizeDelta.Height), 0))
            End If

            Dim headerInset As Integer = frameInsetX + 2
            Dim header As New B2SWindowHeader(target, accent, showMinimize, showMaximize) With {
                .Location = New Point(headerInset, frameInsetY),
                .Size = New Size(Math.Max(1, target.ClientSize.Width - (headerInset * 2)), 28),
                .Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
            }
            state.Header = header
            target.Controls.Add(header)
            header.BringToFront()
            target.BackColor = SurfaceDark
            ApplyRoundedControl(target, 10)
            AddHandler target.ControlAdded, AddressOf RoundedChrome_ControlAdded
            AddHandler target.TextChanged, AddressOf RoundedChrome_TextChanged
            AddHandler target.Resize, AddressOf RoundedChrome_LayoutChanged
            AddHandler target.Shown, AddressOf RoundedChrome_LayoutChanged
            state.NativeWindow = New RoundedChromeNativeWindow(target, canResize)
        Finally
            state.AdjustingControls = False
            target.ResumeLayout(True)
        End Try
    End Sub

    Private Sub RoundedChrome_ControlAdded(sender As Object, e As ControlEventArgs)
        Dim target = TryCast(sender, Form)
        If target Is Nothing OrElse e.Control Is Nothing Then Return
        Dim state As RoundedChromeState = Nothing
        If Not roundedChromeStates.TryGetValue(target, state) OrElse state.AdjustingControls OrElse e.Control Is state.Header Then Return

        state.AdjustingControls = True
        Try
            state.ContentControls.Add(e.Control)
            state.OriginalControlBounds(e.Control) = e.Control.Bounds
            If e.Control.Dock = DockStyle.None Then e.Control.Location = Point.Add(e.Control.Location, CType(state.ContentOffset, Size))
            ApplyDark(e.Control)
            e.Control.BringToFront()
            If state.Header IsNot Nothing Then state.Header.BringToFront()
        Finally
            state.AdjustingControls = False
        End Try
    End Sub

    Private Sub RoundedChrome_LayoutChanged(sender As Object, e As EventArgs)
        Dim target = TryCast(sender, Form)
        If target Is Nothing Then Return
        Dim state As RoundedChromeState = Nothing
        If Not roundedChromeStates.TryGetValue(target, state) OrElse state.AdjustingControls Then Return

        ' A live resize invalidates only narrow exposed strips by default. The
        ' neon frame is painted relative to the whole client rectangle, so the
        ' previous edge would remain visible unless the entire surface is
        ' cleared and repainted at the new size.
        target.Invalidate(True)
        target.Update()

        ' A few legacy tool windows recalculate absolute control coordinates from
        ' the form's ClientSize after every resize. Detect that reset and restore
        ' the reserved title-band offset without changing the window's own layout code.
        Dim layoutWasReset As Boolean = False
        For Each child As Control In state.ContentControls
            If child IsNot Nothing AndAlso Not child.IsDisposed AndAlso child.Dock = DockStyle.None AndAlso child.Top >= 0 AndAlso child.Top < state.ContentOffset.Y Then
                layoutWasReset = True
                Exit For
            End If
        Next
        If Not layoutWasReset Then
            If state.Header IsNot Nothing Then state.Header.BringToFront()
            Return
        End If

        state.AdjustingControls = True
        Try
            For Each child As Control In state.ContentControls
                If child IsNot Nothing AndAlso Not child.IsDisposed AndAlso child.Dock = DockStyle.None Then
                    child.Location = New Point(child.Left + state.ContentOffset.X,
                                               child.Top + state.ContentOffset.Y)
                End If
            Next
            If state.Header IsNot Nothing Then state.Header.BringToFront()
        Finally
            state.AdjustingControls = False
        End Try
    End Sub

    Private Sub RoundedChrome_TextChanged(sender As Object, e As EventArgs)
        Dim target = TryCast(sender, Form)
        If target Is Nothing Then Return
        Dim state As RoundedChromeState = Nothing
        If roundedChromeStates.TryGetValue(target, state) AndAlso state.Header IsNot Nothing Then state.Header.Invalidate()
    End Sub

    Private Function RoundedChromeContentBounds(target As Form) As Rectangle
        Dim state As RoundedChromeState = Nothing
        If target IsNot Nothing AndAlso roundedChromeStates.TryGetValue(target, state) Then
            Return New Rectangle(state.ContentOffset, state.OriginalClientSize)
        End If
        Return New Rectangle(Point.Empty, target.ClientSize)
    End Function

    Private Sub ReleaseRoundedWindowChrome(target As Form)
        Dim state As RoundedChromeState = Nothing
        If target Is Nothing OrElse Not roundedChromeStates.TryGetValue(target, state) Then Return
        RemoveHandler target.ControlAdded, AddressOf RoundedChrome_ControlAdded
        RemoveHandler target.TextChanged, AddressOf RoundedChrome_TextChanged
        RemoveHandler target.Resize, AddressOf RoundedChrome_LayoutChanged
        RemoveHandler target.Shown, AddressOf RoundedChrome_LayoutChanged
        If state.NativeWindow IsNot Nothing Then state.NativeWindow.Detach()
        roundedChromeStates.Remove(target)

        If target.IsDisposed Then Return
        state.AdjustingControls = True
        target.SuspendLayout()
        Try
            If state.Header IsNot Nothing Then
                target.Controls.Remove(state.Header)
                state.Header.Dispose()
                state.Header = Nothing
            End If

            target.FormBorderStyle = state.OriginalBorderStyle
            target.Padding = state.OriginalPadding
            target.MinimumSize = state.OriginalMinimumSize
            target.MaximumSize = state.OriginalMaximumSize
            target.ClientSize = state.OriginalClientSize

            For Each item As KeyValuePair(Of Control, Rectangle) In state.OriginalControlBounds
                Dim child As Control = item.Key
                If child IsNot Nothing AndAlso Not child.IsDisposed AndAlso child.Dock = DockStyle.None Then
                    child.Bounds = item.Value
                End If
            Next
        Finally
            state.AdjustingControls = False
            target.ResumeLayout(True)
        End Try
    End Sub

    ' One application-wide binary control language: green is checked/on and
    ' red is unchecked/off.  This is attached by the shared theme traversal,
    ' so it covers designer controls, floating tools, dialogs, and controls
    ' that are created dynamically after a window is already open.
    Private Sub ApplyStateCheckBoxTheme(checkBox As CheckBox)
        If checkBox Is Nothing OrElse checkBox.IsDisposed Then Return

        checkBox.UseVisualStyleBackColor = False
        checkBox.Appearance = Appearance.Normal
        checkBox.FlatStyle = FlatStyle.Flat
        checkBox.FlatAppearance.BorderSize = 0

        If stateCheckBoxes.Add(checkBox) Then
            AddHandler checkBox.Paint, AddressOf StateCheckBox_Paint
            AddHandler checkBox.CheckedChanged, AddressOf StateCheckBox_VisualStateChanged
            AddHandler checkBox.CheckStateChanged, AddressOf StateCheckBox_VisualStateChanged
            AddHandler checkBox.EnabledChanged, AddressOf StateCheckBox_VisualStateChanged
            AddHandler checkBox.TextChanged, AddressOf StateCheckBox_VisualStateChanged
            AddHandler checkBox.FontChanged, AddressOf StateCheckBox_VisualStateChanged
            AddHandler checkBox.Disposed, AddressOf StateCheckBox_Disposed
        End If
        checkBox.Invalidate()
    End Sub

    Private Sub StateCheckBox_VisualStateChanged(sender As Object, e As EventArgs)
        Dim checkBox = TryCast(sender, CheckBox)
        If checkBox IsNot Nothing AndAlso Not checkBox.IsDisposed Then checkBox.Invalidate()
    End Sub

    Private Sub StateCheckBox_Disposed(sender As Object, e As EventArgs)
        Dim checkBox = TryCast(sender, CheckBox)
        If checkBox Is Nothing Then Return
        stateCheckBoxes.Remove(checkBox)
    End Sub

    Private Sub StateCheckBox_Paint(sender As Object, e As PaintEventArgs)
        PaintStateCheckBox(TryCast(sender, CheckBox), e)
    End Sub

    Public Sub PaintStateCheckBox(checkBox As CheckBox, e As PaintEventArgs)
        If checkBox Is Nothing OrElse e Is Nothing OrElse checkBox.ClientSize.Width <= 0 OrElse checkBox.ClientSize.Height <= 0 Then Return

        Dim background As Color = checkBox.BackColor
        If background.A < 255 Then
            background = If(checkBox.Parent IsNot Nothing AndAlso checkBox.Parent.BackColor.A = 255,
                            checkBox.Parent.BackColor,
                            SurfaceDark)
        End If
        e.Graphics.Clear(background)
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias

        Dim boxSize As Integer = Math.Max(9, Math.Min(11, checkBox.ClientSize.Height - 4))
        Dim boxTop As Integer = Math.Max(1, (checkBox.ClientSize.Height - boxSize) \ 2)
        Dim checkOnRight As Boolean = (checkBox.CheckAlign = ContentAlignment.TopRight OrElse
                                       checkBox.CheckAlign = ContentAlignment.MiddleRight OrElse
                                       checkBox.CheckAlign = ContentAlignment.BottomRight)
        Dim boxLeft As Integer = If(checkOnRight,
                                    Math.Max(1, checkBox.ClientSize.Width - boxSize - 1),
                                    1)
        Dim boxBounds As New Rectangle(boxLeft, boxTop, boxSize, boxSize)
        Dim stateColor As Color = If(checkBox.CheckState = CheckState.Checked, GreenAccent, RedAccent)

        Using glow As New Pen(Color.FromArgb(92, stateColor), 3.0F)
            e.Graphics.DrawRectangle(glow, boxBounds)
        End Using
        Using fill As New SolidBrush(stateColor)
            e.Graphics.FillRectangle(fill, boxBounds)
        End Using
        Using border As New Pen(Color.FromArgb(238, 245, 252), 1.0F)
            e.Graphics.DrawRectangle(border, boxBounds)
        End Using

        Dim gap As Integer = 4
        Dim textBounds As Rectangle
        If checkOnRight Then
            textBounds = New Rectangle(0, 0, Math.Max(1, boxBounds.Left - gap), checkBox.ClientSize.Height)
        Else
            textBounds = New Rectangle(boxBounds.Right + gap, 0,
                                       Math.Max(1, checkBox.ClientSize.Width - boxBounds.Right - gap),
                                       checkBox.ClientSize.Height)
        End If
        Dim textColor As Color = If(checkBox.Enabled, checkBox.ForeColor, TextMuted)
        Dim flags As TextFormatFlags = TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPadding
        flags = flags Or If(checkOnRight, TextFormatFlags.Right, TextFormatFlags.Left)
        If Not checkBox.UseMnemonic Then flags = flags Or TextFormatFlags.NoPrefix
        TextRenderer.DrawText(e.Graphics, checkBox.Text, checkBox.Font, textBounds, textColor, flags)

        If checkBox.Focused Then
            Dim focusBounds As Rectangle = textBounds
            focusBounds.Inflate(-1, -2)
            If focusBounds.Width > 2 AndAlso focusBounds.Height > 2 Then ControlPaint.DrawFocusRectangle(e.Graphics, focusBounds, textColor, background)
        End If
    End Sub

    Private Sub ApplyStateRadioButtonTheme(radioButton As RadioButton)
        If radioButton Is Nothing OrElse radioButton.IsDisposed Then Return

        radioButton.UseVisualStyleBackColor = False
        radioButton.Appearance = Appearance.Normal
        radioButton.FlatStyle = FlatStyle.Flat
        radioButton.FlatAppearance.BorderSize = 0

        If stateRadioButtons.Add(radioButton) Then
            AddHandler radioButton.Paint, AddressOf StateRadioButton_Paint
            AddHandler radioButton.CheckedChanged, AddressOf StateRadioButton_VisualStateChanged
            AddHandler radioButton.EnabledChanged, AddressOf StateRadioButton_VisualStateChanged
            AddHandler radioButton.TextChanged, AddressOf StateRadioButton_VisualStateChanged
            AddHandler radioButton.FontChanged, AddressOf StateRadioButton_VisualStateChanged
            AddHandler radioButton.Disposed, AddressOf StateRadioButton_Disposed
        End If
        radioButton.Invalidate()
    End Sub

    Private Sub StateRadioButton_VisualStateChanged(sender As Object, e As EventArgs)
        Dim radioButton = TryCast(sender, RadioButton)
        If radioButton IsNot Nothing AndAlso Not radioButton.IsDisposed Then radioButton.Invalidate()
    End Sub

    Private Sub StateRadioButton_Disposed(sender As Object, e As EventArgs)
        Dim radioButton = TryCast(sender, RadioButton)
        If radioButton Is Nothing Then Return
        stateRadioButtons.Remove(radioButton)
    End Sub

    Private Sub StateRadioButton_Paint(sender As Object, e As PaintEventArgs)
        PaintStateRadioButton(TryCast(sender, RadioButton), e)
    End Sub

    Public Sub PaintStateRadioButton(radioButton As RadioButton, e As PaintEventArgs)
        If radioButton Is Nothing OrElse e Is Nothing OrElse radioButton.ClientSize.Width <= 0 OrElse radioButton.ClientSize.Height <= 0 Then Return

        Dim background As Color = radioButton.BackColor
        If background.A < 255 Then
            background = If(radioButton.Parent IsNot Nothing AndAlso radioButton.Parent.BackColor.A = 255,
                            radioButton.Parent.BackColor,
                            SurfaceDark)
        End If
        e.Graphics.Clear(background)
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias

        Dim circleSize As Integer = Math.Max(9, Math.Min(11, radioButton.ClientSize.Height - 4))
        Dim circleTop As Integer = Math.Max(1, (radioButton.ClientSize.Height - circleSize) \ 2)
        Dim choiceOnRight As Boolean = (radioButton.CheckAlign = ContentAlignment.TopRight OrElse
                                        radioButton.CheckAlign = ContentAlignment.MiddleRight OrElse
                                        radioButton.CheckAlign = ContentAlignment.BottomRight)
        Dim circleLeft As Integer = If(choiceOnRight,
                                       Math.Max(1, radioButton.ClientSize.Width - circleSize - 1),
                                       1)
        Dim circleBounds As New Rectangle(circleLeft, circleTop, circleSize, circleSize)
        Dim stateColor As Color = If(radioButton.Checked, GreenAccent, RedAccent)

        Using glow As New Pen(Color.FromArgb(92, stateColor), 3.0F)
            e.Graphics.DrawEllipse(glow, circleBounds)
        End Using
        Using fill As New SolidBrush(stateColor)
            e.Graphics.FillEllipse(fill, circleBounds)
        End Using
        Using border As New Pen(Color.FromArgb(238, 245, 252), 1.0F)
            e.Graphics.DrawEllipse(border, circleBounds)
        End Using

        Dim gap As Integer = 4
        Dim textBounds As Rectangle
        If choiceOnRight Then
            textBounds = New Rectangle(0, 0, Math.Max(1, circleBounds.Left - gap), radioButton.ClientSize.Height)
        Else
            textBounds = New Rectangle(circleBounds.Right + gap, 0,
                                       Math.Max(1, radioButton.ClientSize.Width - circleBounds.Right - gap),
                                       radioButton.ClientSize.Height)
        End If
        Dim textColor As Color = If(radioButton.Enabled, radioButton.ForeColor, TextMuted)
        Dim flags As TextFormatFlags = TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPadding
        flags = flags Or If(choiceOnRight, TextFormatFlags.Right, TextFormatFlags.Left)
        If Not radioButton.UseMnemonic Then flags = flags Or TextFormatFlags.NoPrefix
        TextRenderer.DrawText(e.Graphics, radioButton.Text, radioButton.Font, textBounds, textColor, flags)

        If radioButton.Focused Then
            Dim focusBounds As Rectangle = textBounds
            focusBounds.Inflate(-1, -2)
            If focusBounds.Width > 2 AndAlso focusBounds.Height > 2 Then ControlPaint.DrawFocusRectangle(e.Graphics, focusBounds, textColor, background)
        End If
    End Sub

    ' Phase 3: give editor/settings dialogs the same polished B2S Pro identity
    ' without moving controls or changing any existing event handlers.
    Public Sub ApplyB2SProDialog(target As Form)
        If target Is Nothing OrElse target.IsDisposed OrElse Not DarkMode Then Return

        Dim accent As Color = DialogAccent(target)
        dialogAccents(target) = accent

        target.BackColor = SurfaceDark
        target.ForeColor = TextPrimary
        target.Font = New Font("Segoe UI", 9.0F, FontStyle.Regular)
        target.Padding = New Padding(5, 7, 5, 5)
        ApplyToolWindowCaptionTheme(target, accent)

        If styledDialogs.Add(target) Then
            AddHandler target.Paint, AddressOf B2SProDialog_Paint
            AddHandler target.FormClosed, AddressOf B2SProDialog_FormClosed
        End If

        StyleB2SProDialogTree(target, accent, 0)
        If target.TopLevel Then ApplyRoundedWindowChrome(target, accent)
        target.Invalidate(True)
    End Sub

    Private Function DialogAccent(target As Form) As Color
        Dim key As String = (If(target.Name, String.Empty) & " " & If(target.Text, String.Empty)).ToLowerInvariant()
        If key.Contains("light") OrElse key.Contains("brightness") OrElse key.Contains("illum") OrElse key.Contains("diffusion") Then Return Color.FromArgb(255, 151, 48)
        If key.Contains("snipp") OrElse key.Contains("mask") OrElse key.Contains("selection") OrElse key.Contains("globalmask") Then Return Color.FromArgb(183, 92, 255)
        If key.Contains("reel") OrElse key.Contains("led") OrElse key.Contains("color") OrElse key.Contains("palette") Then Return Color.FromArgb(48, 216, 138)
        If key.Contains("resize") OrElse key.Contains("settings") OrElse key.Contains("backup") OrElse key.Contains("about") Then Return Color.FromArgb(48, 173, 255)
        If key.Contains("animation") OrElse key.Contains("preview") OrElse key.Contains("vpm") Then Return Color.FromArgb(255, 92, 117)
        Return Color.FromArgb(74, 137, 255)
    End Function

    Private Sub StyleB2SProDialogTree(parent As Control, accent As Color, depth As Integer)
        For Each child As Control In parent.Controls
            If TypeOf child Is PictureBox Then
                ' Preserve artwork, previews, and color swatches exactly.
            ElseIf TypeOf child Is B2SLine Then
                child.BackColor = Color.FromArgb(2, 4, 8)
                child.ForeColor = accent
            ElseIf TypeOf child Is TextBoxBase OrElse TypeOf child Is ComboBox OrElse TypeOf child Is NumericUpDown OrElse TypeOf child Is DateTimePicker Then
                child.BackColor = FieldBack
                child.ForeColor = TextPrimary
                child.Font = New Font("Segoe UI", 9.0F, FontStyle.Regular)
                Dim combo = TryCast(child, ComboBox)
                If combo IsNot Nothing Then ApplyB2SProComboBox(combo, GoldAccent)
                ApplyModernBorder(child)
                ApplyRoundedControl(child, 6)
                ApplyFocusPolish(child)
                If Not proControlAccents.ContainsKey(child) Then AddHandler child.Disposed, AddressOf B2SProControl_Disposed
                proControlAccents(child) = GoldAccent
            ElseIf TypeOf child Is ButtonBase AndAlso Not TypeOf child Is CheckBox AndAlso Not TypeOf child Is RadioButton Then
                Dim button = DirectCast(child, ButtonBase)
                button.UseVisualStyleBackColor = False
                button.FlatStyle = FlatStyle.Flat
                Dim buttonAccent As Color = AccentForControl(button)
                If Not IsColorSwatchButton(button) Then button.BackColor = Color.FromArgb(5, 7, 11)
                button.ForeColor = Color.White
                button.FlatAppearance.BorderColor = Color.FromArgb(205, buttonAccent)
                button.FlatAppearance.BorderSize = 1
                button.FlatAppearance.MouseOverBackColor = AccentHover(buttonAccent, 74)
                button.FlatAppearance.MouseDownBackColor = AccentHover(buttonAccent, 116)
                button.Font = New Font("Segoe UI Semibold", 9.0F, FontStyle.Regular)
                ApplyRoundedControl(button, 8)
                ApplyNeonButton(button)
                ApplyB2SProActionButton(button, buttonAccent)
            ElseIf TypeOf child Is GroupBox Then
                child.BackColor = Color.FromArgb(3, 5, 9)
                child.ForeColor = accent
                child.Font = New Font("Segoe UI Semibold", 9.0F, FontStyle.Bold)
                ApplyGroupBoxPolish(DirectCast(child, GroupBox))
            ElseIf TypeOf child Is TabPage Then
                child.BackColor = Color.FromArgb(3, 5, 9)
                child.ForeColor = TextPrimary
            ElseIf TypeOf child Is Panel OrElse TypeOf child Is FlowLayoutPanel OrElse TypeOf child Is TableLayoutPanel OrElse TypeOf child Is SplitContainer Then
                child.BackColor = If(depth Mod 2 = 0, Color.FromArgb(2, 4, 8), Color.FromArgb(6, 8, 13))
                child.ForeColor = TextPrimary
            ElseIf TypeOf child Is Label Then
                child.BackColor = Color.Transparent
                child.ForeColor = If(child.Font IsNot Nothing AndAlso child.Font.Bold, accent, TextPrimary)
                child.Font = New Font("Segoe UI", child.Font.Size, child.Font.Style)
            ElseIf TypeOf child Is CheckBox OrElse TypeOf child Is RadioButton Then
                child.BackColor = Color.Transparent
                child.ForeColor = TextPrimary
                child.Font = New Font("Segoe UI", 8.75F, child.Font.Style)
                Dim toggle = DirectCast(child, ButtonBase)
                toggle.UseVisualStyleBackColor = False
                toggle.FlatStyle = FlatStyle.Flat
                toggle.FlatAppearance.BorderSize = 0
                toggle.FlatAppearance.MouseOverBackColor = Color.FromArgb(25, accent)
                toggle.FlatAppearance.CheckedBackColor = Color.FromArgb(58, accent)
            ElseIf TypeOf child Is TrackBar Then
                child.BackColor = Color.FromArgb(2, 4, 8)
                ApplyB2SProTrackBar(DirectCast(child, TrackBar), GoldAccent)
            ElseIf TypeOf child Is ListBox OrElse TypeOf child Is TreeView OrElse TypeOf child Is ListView Then
                child.BackColor = Color.FromArgb(3, 5, 9)
                child.ForeColor = TextPrimary
                ApplyRoundedControl(child, 10)
            End If

            If child.ContextMenuStrip IsNot Nothing Then ApplyDarkToolStrip(child.ContextMenuStrip)
            StyleB2SProDialogTree(child, accent, depth + 1)
        Next
    End Sub

    Private Sub B2SProDialog_Paint(sender As Object, e As PaintEventArgs)
        If Not DarkMode Then Return
        Dim target = TryCast(sender, Form)
        If target Is Nothing OrElse target.ClientSize.Width < 4 OrElse target.ClientSize.Height < 4 Then Return

        Dim accent As Color = Color.FromArgb(74, 137, 255)
        dialogAccents.TryGetValue(target, accent)
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias

        DrawGlassWindowFrame(e.Graphics, target, accent)
        DrawDialogControlFrames(e.Graphics, target, accent)
    End Sub

    Private Sub B2SProDialog_FormClosed(sender As Object, e As FormClosedEventArgs)
        Dim target = TryCast(sender, Form)
        If target Is Nothing Then Return
        styledDialogs.Remove(target)
        dialogAccents.Remove(target)
        ReleaseRoundedWindowChrome(target)
    End Sub

    Public Sub ApplyB2SProToolWindow(target As Form)
        If target Is Nothing OrElse target.IsDisposed OrElse Not DarkMode Then Return

        Dim accent As Color = AccentForControl(target)
        toolWindowAccents(target) = accent

        target.BackColor = Color.FromArgb(1, 3, 6)
        target.ForeColor = Color.FromArgb(242, 245, 252)
        target.Font = New Font("Segoe UI", 9.0F, FontStyle.Regular)
        target.Padding = New Padding(7, 9, 7, 7)
        ApplyToolWindowCaptionTheme(target, accent)

        If styledToolWindows.Add(target) Then
            AddHandler target.Paint, AddressOf B2SProToolWindow_Paint
            AddHandler target.FormClosed, AddressOf B2SProToolWindow_FormClosed
        End If

        StyleB2SProControlTree(target, accent, 0)
        ConfigureToolWindowSections(target)
        If target.TopLevel Then ApplyRoundedWindowChrome(target, accent)
        target.Invalidate(True)
    End Sub

    Private Sub ConfigureToolWindowSections(target As Form)
        Dim key As String = If(target.Name, String.Empty).ToLowerInvariant()
        If key.Contains("illumination") Then
            SetToolSectionTitle(target, "B2SLine2", "ROM / CONTROLLER")
            SetToolSectionTitle(target, "B2SLine5", "INTENSITY & COLOR")
            SetToolSectionTitle(target, "B2SLine3", "ILLUMINATED TEXT")
            SetToolSectionTitle(target, "B2SLine4", "POSITION & ACTIONS")
            SetToolSectionTitle(target, "B2SLine1", String.Empty)
            Dim unusedDivider As Control() = target.Controls.Find("B2SLine1", True)
            If unusedDivider.Length > 0 Then unusedDivider(0).Visible = False
        ElseIf key.Contains("reelsandleds") Then
            SetToolSectionTitle(target, "B2SLine3", "DISPLAY SETUP")
            SetToolSectionTitle(target, "B2SLine4", "B2S MAPPING")
            SetToolSectionTitle(target, "B2SLine5", "REEL / LED LIBRARY")
            SetToolSectionTitle(target, "B2SLine1", "POSITION & SIZE")
            SetToolSectionTitle(target, "B2SLine2", "ACTIONS")
        End If
    End Sub

    Private Sub SetToolSectionTitle(target As Form, controlName As String, title As String)
        Dim matches As Control() = target.Controls.Find(controlName, True)
        If matches.Length = 0 Then Return
        Dim divider As B2SLine = TryCast(matches(0), B2SLine)
        If divider Is Nothing Then Return
        divider.Text = title
        divider.Font = New Font("Segoe UI Semibold", 8.25F, FontStyle.Bold)
        divider.Invalidate()
    End Sub

    Private Sub StyleB2SProControlTree(parent As Control, accent As Color, depth As Integer)
        For Each child As Control In parent.Controls
            Dim childAccent As Color = ToolVisualAccent(parent.FindForm(), child, accent)
            If TypeOf child Is PictureBox Then
                ' Artwork thumbnails and previews must retain their original colors.
            ElseIf TypeOf child Is B2SLine Then
                child.BackColor = Color.FromArgb(2, 4, 8)
                child.ForeColor = childAccent
                proControlAccents(child) = childAccent
            ElseIf TypeOf child Is TextBoxBase OrElse TypeOf child Is ComboBox OrElse TypeOf child Is NumericUpDown Then
                child.BackColor = Color.FromArgb(4, 7, 12)
                child.ForeColor = Color.FromArgb(244, 246, 252)
                child.Font = New Font("Segoe UI", 9.0F, FontStyle.Regular)
                Dim combo = TryCast(child, ComboBox)
                If combo IsNot Nothing Then ApplyB2SProComboBox(combo, childAccent)
                ApplyModernBorder(child)
                ApplyRoundedControl(child, 6)
                ApplyFocusPolish(child)
                If Not proControlAccents.ContainsKey(child) Then AddHandler child.Disposed, AddressOf B2SProControl_Disposed
                proControlAccents(child) = childAccent
            ElseIf TypeOf child Is ListView Then
                Dim list = DirectCast(child, ListView)
                list.BackColor = Color.FromArgb(3, 5, 9)
                list.ForeColor = Color.FromArgb(238, 242, 250)
                list.BorderStyle = BorderStyle.FixedSingle
                list.Font = New Font("Segoe UI", 9.0F, FontStyle.Regular)
                list.FullRowSelect = True
                list.HideSelection = False
                ApplyRoundedControl(list, 10)
            ElseIf TypeOf child Is ListBox OrElse TypeOf child Is TreeView Then
                child.BackColor = Color.FromArgb(3, 5, 9)
                child.ForeColor = Color.FromArgb(238, 242, 250)
                ApplyRoundedControl(child, 10)
            ElseIf TypeOf child Is ButtonBase AndAlso Not TypeOf child Is CheckBox AndAlso Not TypeOf child Is RadioButton Then
                Dim button = DirectCast(child, ButtonBase)
                button.UseVisualStyleBackColor = False
                button.FlatStyle = FlatStyle.Flat
                Dim buttonAccent As Color = ToolVisualAccent(parent.FindForm(), button, AccentForControl(button))
                If Not IsColorSwatchButton(button) Then button.BackColor = Color.FromArgb(5, 7, 11)
                button.ForeColor = Color.White
                button.FlatAppearance.BorderColor = Color.FromArgb(205, buttonAccent)
                button.FlatAppearance.BorderSize = 1
                button.FlatAppearance.MouseOverBackColor = AccentHover(buttonAccent, 74)
                button.FlatAppearance.MouseDownBackColor = AccentHover(buttonAccent, 116)
                button.Font = New Font("Segoe UI Semibold", 8.75F, FontStyle.Regular)
                ApplyRoundedControl(button, 7)
                ApplyNeonButton(button)
                ApplyB2SProActionButton(button, buttonAccent)
                If button.Height >= 30 AndAlso Not IsColorSwatchButton(button) Then
                    button.BackColor = AccentHover(buttonAccent, 64)
                    button.FlatAppearance.BorderColor = Color.FromArgb(235, buttonAccent)
                End If
            ElseIf TypeOf child Is GroupBox Then
                child.BackColor = Color.FromArgb(3, 5, 9)
                child.ForeColor = accent
                child.Font = New Font("Segoe UI Semibold", 9.0F, FontStyle.Bold)
                ApplyGroupBoxPolish(DirectCast(child, GroupBox))
            ElseIf TypeOf child Is TabPage Then
                child.BackColor = Color.FromArgb(3, 5, 9)
                child.ForeColor = Color.FromArgb(238, 242, 250)
            ElseIf TypeOf child Is Panel OrElse TypeOf child Is FlowLayoutPanel OrElse TypeOf child Is TableLayoutPanel OrElse TypeOf child Is SplitContainer Then
                child.BackColor = If(depth Mod 2 = 0, Color.FromArgb(2, 4, 8), Color.FromArgb(6, 8, 13))
                child.ForeColor = Color.FromArgb(238, 242, 250)
            ElseIf TypeOf child Is Label Then
                child.BackColor = Color.Transparent
                child.ForeColor = If(child.Font IsNot Nothing AndAlso child.Font.Bold OrElse
                                     child.Name.Equals("lblSelectedIlluminationType", StringComparison.OrdinalIgnoreCase),
                                     childAccent,
                                     Color.FromArgb(226, 231, 242))
                child.Font = New Font("Segoe UI", child.Font.Size, child.Font.Style)
            ElseIf TypeOf child Is CheckBox OrElse TypeOf child Is RadioButton Then
                child.BackColor = Color.Transparent
                child.ForeColor = Color.FromArgb(226, 231, 242)
                child.Font = New Font("Segoe UI", 8.75F, child.Font.Style)
                Dim toggle = DirectCast(child, ButtonBase)
                toggle.UseVisualStyleBackColor = False
                toggle.FlatStyle = FlatStyle.Flat
                toggle.FlatAppearance.BorderSize = 0
                toggle.FlatAppearance.MouseOverBackColor = Color.FromArgb(25, childAccent)
                toggle.FlatAppearance.CheckedBackColor = Color.FromArgb(58, childAccent)
            ElseIf TypeOf child Is TrackBar Then
                child.BackColor = Color.FromArgb(2, 4, 8)
                ApplyB2SProTrackBar(DirectCast(child, TrackBar), childAccent)
            End If

            If child.ContextMenuStrip IsNot Nothing Then ApplyDarkToolStrip(child.ContextMenuStrip)
            StyleB2SProControlTree(child, accent, depth + 1)
        Next
    End Sub

    Private Function ToolVisualAccent(target As Form, control As Control, fallback As Color) As Color
        If target Is Nothing OrElse control Is Nothing Then Return fallback
        If Not target.Name.Equals("formToolIllumination", StringComparison.OrdinalIgnoreCase) Then Return fallback

        Select Case control.Name.ToLowerInvariant()
            Case "lblselectedilluminationtype"
                Return PurpleAccent
            Case "b2sline2", "lbllamppropertiesheader", "txtname", "txtid", "cmbinitstate", "cmbdualmode", "txtromid", "cmbromidtype", "chkrominverted", "txtb2sid", "cmbb2sidtype", "txtb2svalue"
                Return GoldAccent
            Case "b2sline5"
                Return PurpleAccent
            Case "trackbarintensity", "btnlightcolor", "cmbdodgecolor", "cmbillumode"
                Return GoldAccent
            Case "b2sline3", "txtilluminationtext", "rbalignleft", "rbaligncenter", "rbalignright", "btnfonts"
                Return GreenAccent
            Case "b2sline4", "txtlocationx", "txtlocationy", "txtsizewidth", "txtsizeheight"
                Return CyanAccent
            Case "btnsnippitsettings"
                Return RedAccent
            Case "btneditlightglow"
                Return GoldAccent
        End Select
        Return fallback
    End Function

    ' Stage 7: paint the existing tool-window controls with the glass-and-neon
    ' treatment from the approved concept. These helpers attach only visual
    ' painting; they do not replace controls or touch their event handlers.
    Private Sub ApplyB2SProComboBox(combo As ComboBox, accent As Color)
        combo.FlatStyle = FlatStyle.Flat
        combo.BackColor = Color.FromArgb(4, 7, 12)
        combo.ForeColor = TextPrimary
        combo.DrawMode = DrawMode.OwnerDrawFixed
        combo.ItemHeight = Math.Max(combo.ItemHeight, 19)
        proControlAccents(combo) = accent

        If proComboBoxes.Add(combo) Then
            AddHandler combo.DrawItem, AddressOf B2SProComboBox_DrawItem
            AddHandler combo.HandleCreated, AddressOf B2SProComboBox_HandleCreated
            AddHandler combo.Disposed, AddressOf B2SProControl_Disposed
        End If

        If combo.IsHandleCreated Then DisableNativeComboTheme(combo)
        combo.Invalidate()
    End Sub

    Private Sub B2SProComboBox_HandleCreated(sender As Object, e As EventArgs)
        DisableNativeComboTheme(TryCast(sender, ComboBox))
    End Sub

    Private Sub DisableNativeComboTheme(combo As ComboBox)
        If combo Is Nothing OrElse combo.IsDisposed OrElse Not combo.IsHandleCreated Then Return
        Try
            SetWindowTheme(combo.Handle, String.Empty, String.Empty)
        Catch ex As DllNotFoundException
        Catch ex As EntryPointNotFoundException
        End Try
    End Sub

    Private Sub B2SProComboBox_DrawItem(sender As Object, e As DrawItemEventArgs)
        Dim combo = TryCast(sender, ComboBox)
        If combo Is Nothing OrElse e.Bounds.Width <= 0 OrElse e.Bounds.Height <= 0 Then Return

        Dim accent As Color = AccentForControl(combo)
        proControlAccents.TryGetValue(combo, accent)
        Dim selected As Boolean = (e.State And DrawItemState.Selected) = DrawItemState.Selected
        Dim disabled As Boolean = (e.State And DrawItemState.Disabled) = DrawItemState.Disabled OrElse Not combo.Enabled
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias

        If selected AndAlso Not disabled Then
            Using fill As New LinearGradientBrush(e.Bounds,
                                                   Color.FromArgb(112, accent),
                                                   Color.FromArgb(22, accent),
                                                   LinearGradientMode.Vertical)
                e.Graphics.FillRectangle(fill, e.Bounds)
            End Using
            Using edge As New Pen(Color.FromArgb(225, accent), 1.0F)
                e.Graphics.DrawLine(edge, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1)
            End Using
        Else
            Using fill As New LinearGradientBrush(e.Bounds,
                                                   Color.FromArgb(14, 18, 27),
                                                   Color.FromArgb(3, 5, 9),
                                                   LinearGradientMode.Vertical)
                e.Graphics.FillRectangle(fill, e.Bounds)
            End Using
        End If

        Dim itemText As String = combo.Text
        If e.Index >= 0 AndAlso e.Index < combo.Items.Count Then itemText = combo.GetItemText(combo.Items(e.Index))
        Dim textColor As Color = If(disabled, Color.FromArgb(104, 111, 126), Color.FromArgb(245, 247, 252))
        Dim textBounds As Rectangle = Rectangle.Inflate(e.Bounds, -7, 0)
        TextRenderer.DrawText(e.Graphics,
                              itemText,
                              combo.Font,
                              textBounds,
                              textColor,
                              TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix)
        If (e.State And DrawItemState.Focus) = DrawItemState.Focus Then e.DrawFocusRectangle()
    End Sub

    Private Sub ApplyB2SProTrackBar(slider As TrackBar, accent As Color)
        proControlAccents(slider) = accent
        If proTrackBars.Add(slider) Then
            AddHandler slider.Paint, AddressOf B2SProTrackBar_Paint
            AddHandler slider.Disposed, AddressOf B2SProControl_Disposed
        End If
        slider.Invalidate()
    End Sub

    Private Sub B2SProTrackBar_Paint(sender As Object, e As PaintEventArgs)
        Dim slider = TryCast(sender, TrackBar)
        If slider Is Nothing OrElse slider.Orientation <> Orientation.Horizontal OrElse slider.ClientSize.Width < 24 Then Return

        Dim accent As Color = Color.FromArgb(255, 171, 38)
        proControlAccents.TryGetValue(slider, accent)
        Dim left As Integer = 10
        Dim right As Integer = slider.ClientSize.Width - 11
        Dim y As Integer = Math.Max(8, slider.ClientSize.Height \ 2)
        Dim range As Integer = Math.Max(1, slider.Maximum - slider.Minimum)
        Dim progress As Single = CSng(slider.Value - slider.Minimum) / CSng(range)
        Dim thumbX As Integer = left + CInt((right - left) * progress)

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
        Using glow As New Pen(Color.FromArgb(52, accent), 7.0F)
            glow.StartCap = LineCap.Round
            glow.EndCap = LineCap.Round
            e.Graphics.DrawLine(glow, left, y, thumbX, y)
        End Using
        Using emptyRail As New Pen(Color.FromArgb(83, 90, 106), 3.0F)
            emptyRail.StartCap = LineCap.Round
            emptyRail.EndCap = LineCap.Round
            e.Graphics.DrawLine(emptyRail, left, y, right, y)
        End Using
        Using activeRail As New Pen(Color.FromArgb(245, accent), 3.0F)
            activeRail.StartCap = LineCap.Round
            activeRail.EndCap = LineCap.Round
            e.Graphics.DrawLine(activeRail, left, y, thumbX, y)
        End Using
        Dim thumb As New Rectangle(thumbX - 6, y - 6, 12, 12)
        Using glowBrush As New SolidBrush(Color.FromArgb(74, accent))
            e.Graphics.FillEllipse(glowBrush, Rectangle.Inflate(thumb, 3, 3))
        End Using
        Using fill As New LinearGradientBrush(thumb, Color.White, accent, LinearGradientMode.Vertical)
            e.Graphics.FillEllipse(fill, thumb)
        End Using
        Using border As New Pen(Color.FromArgb(245, accent), 1.0F)
            e.Graphics.DrawEllipse(border, thumb)
        End Using
    End Sub

    Private Sub ApplyB2SProActionButton(button As ButtonBase, accent As Color)
        If button Is Nothing OrElse button.IsDisposed OrElse IsColorSwatchButton(button) Then Return
        proControlAccents(button) = accent
        ApplyRoundedControl(button, ButtonCornerRadius(button))
        If proActionButtons.Add(button) Then
            AddHandler button.Paint, AddressOf B2SProActionButton_Paint
            AddHandler button.Disposed, AddressOf B2SProControl_Disposed
        End If
    End Sub

    Private Sub B2SProActionButton_Paint(sender As Object, e As PaintEventArgs)
        Dim button = TryCast(sender, ButtonBase)
        If button Is Nothing OrElse IsColorSwatchButton(button) OrElse button.ClientSize.Width < 12 OrElse button.ClientSize.Height < 8 Then Return

        Dim accent As Color = AccentForControl(button)
        proControlAccents.TryGetValue(button, accent)
        If Not button.Enabled Then accent = Color.FromArgb(80, 87, 102)
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias

        Dim bounds As New Rectangle(1, 1, button.ClientSize.Width - 3, button.ClientSize.Height - 3)
        Dim fullWidthAction As Boolean = button.Image Is Nothing AndAlso
                                         button.Height >= 30 AndAlso
                                         button.Width >= 150 AndAlso
                                         Not String.IsNullOrWhiteSpace(button.Text)

        If button.Image IsNot Nothing Then
            Using imagePath As GraphicsPath = CreateRoundedRectangle(bounds, ButtonCornerRadius(button))
                Using imageGlow As New Pen(Color.FromArgb(72, accent), 4.0F)
                    e.Graphics.DrawPath(imageGlow, imagePath)
                End Using
                Using imageBorder As New Pen(Color.FromArgb(235, accent), 1.2F)
                    e.Graphics.DrawPath(imageBorder, imagePath)
                End Using
            End Using
            Return
        End If

        Dim isHovered As Boolean = False
        Try
            isHovered = button.RectangleToScreen(button.ClientRectangle).Contains(Control.MousePosition)
        Catch ex As InvalidOperationException
        End Try
        Dim isPressed As Boolean = isHovered AndAlso (Control.MouseButtons And MouseButtons.Left) = MouseButtons.Left
        Dim topStrength As Integer = If(isPressed, 80, If(isHovered, 142, If(fullWidthAction, 112, 62)))
        Dim bottomStrength As Integer = If(isPressed, 118, If(isHovered, 70, If(fullWidthAction, 38, 24)))
        Dim radius As Integer = ButtonCornerRadius(button)

        Using path As GraphicsPath = CreateRoundedRectangle(bounds, radius)
            Using glow As New Pen(Color.FromArgb(If(isHovered, 102, 68), accent), If(isHovered, 5.0F, 4.0F))
                e.Graphics.DrawPath(glow, path)
            End Using
            Using fill As New LinearGradientBrush(bounds,
                                                   AccentHover(accent, topStrength),
                                                   AccentHover(accent, bottomStrength),
                                                   LinearGradientMode.Vertical)
                e.Graphics.FillPath(fill, path)
            End Using
            Using border As New Pen(Color.FromArgb(245, accent), 1.25F)
                e.Graphics.DrawPath(border, path)
            End Using
        End Using

        Using topHighlight As New Pen(Color.FromArgb(95, Color.White), 1.0F)
            e.Graphics.DrawLine(topHighlight, bounds.Left + radius, bounds.Top + 2, bounds.Right - radius, bounds.Top + 2)
        End Using
        Using bottomGlow As New Pen(Color.FromArgb(225, accent), 2.0F)
            e.Graphics.DrawLine(bottomGlow, bounds.Left + radius, bounds.Bottom - 2, bounds.Right - radius, bounds.Bottom - 2)
        End Using

        Dim textBounds As Rectangle
        Dim textFlags As TextFormatFlags = TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis
        If fullWidthAction Then
            textBounds = New Rectangle(13, 1, Math.Max(1, button.ClientSize.Width - 52), button.ClientSize.Height - 2)
            textFlags = textFlags Or TextFormatFlags.Left
        Else
            textBounds = Rectangle.Inflate(button.ClientRectangle, -9, -2)
            textFlags = textFlags Or TextFormatFlags.HorizontalCenter
        End If
        TextRenderer.DrawText(e.Graphics, button.Text, button.Font, textBounds, Color.White, textFlags)

        If fullWidthAction Then
            Dim chevronX As Integer = button.ClientSize.Width - 23
            Dim chevronY As Integer = button.ClientSize.Height \ 2
            Using chevronGlow As New Pen(Color.FromArgb(72, accent), 4.0F)
                chevronGlow.StartCap = LineCap.Round
                chevronGlow.EndCap = LineCap.Round
                e.Graphics.DrawLines(chevronGlow, New Point() {
                                         New Point(chevronX - 4, chevronY - 5),
                                         New Point(chevronX, chevronY),
                                         New Point(chevronX - 4, chevronY + 5)})
            End Using
            Using chevron As New Pen(Color.White, 1.6F)
                chevron.StartCap = LineCap.Round
                chevron.EndCap = LineCap.Round
                e.Graphics.DrawLines(chevron, New Point() {
                                     New Point(chevronX - 4, chevronY - 5),
                                     New Point(chevronX, chevronY),
                                     New Point(chevronX - 4, chevronY + 5)})
            End Using
        End If

        If button.Focused Then
            ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(bounds, -4, -4), Color.White, Color.Transparent)
        End If
    End Sub

    Private Sub B2SProControl_Disposed(sender As Object, e As EventArgs)
        Dim control = TryCast(sender, Control)
        If control Is Nothing Then Return
        proControlAccents.Remove(control)
        Dim combo = TryCast(control, ComboBox)
        If combo IsNot Nothing Then proComboBoxes.Remove(combo)
        Dim slider = TryCast(control, TrackBar)
        If slider IsNot Nothing Then proTrackBars.Remove(slider)
        Dim button = TryCast(control, ButtonBase)
        If button IsNot Nothing Then proActionButtons.Remove(button)
    End Sub

    Private Sub B2SProToolWindow_Paint(sender As Object, e As PaintEventArgs)
        If Not DarkMode Then Return
        Dim target = TryCast(sender, Form)
        If target Is Nothing OrElse target.ClientSize.Width < 4 OrElse target.ClientSize.Height < 4 Then Return

        Dim accent As Color = Color.FromArgb(74, 137, 255)
        toolWindowAccents.TryGetValue(target, accent)

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
        DrawToolSectionCards(e.Graphics, target, accent)
        DrawToolControlFrames(e.Graphics, target, accent)
        DrawGlassWindowFrame(e.Graphics, target, accent)
    End Sub

    Private Sub DrawGlassWindowFrame(graphics As Graphics, target As Form, accent As Color)
        If target.ClientSize.Width < 8 OrElse target.ClientSize.Height < 8 Then Return
        Dim bounds As New Rectangle(1, 1, target.ClientSize.Width - 3, target.ClientSize.Height - 3)
        Using path As GraphicsPath = CreateRoundedRectangle(bounds, 8)
            ' One restrained panel edge replaces the former multi-line neon
            ' floating-window frame, while keeping resize hit-testing intact.
            Using border As New Pen(Color.FromArgb(150, accent), 1.0F)
                graphics.DrawPath(border, path)
            End Using
        End Using
    End Sub

    Private Sub DrawDialogControlFrames(graphics As Graphics, target As Form, accent As Color)
        Dim fields As New List(Of Control)()
        CollectToolFields(target, fields)
        DrawControlFrames(graphics, target, fields, accent)
    End Sub

    Private Sub DrawToolSectionCards(graphics As Graphics, target As Form, accent As Color)
        Dim key As String = If(target.Name, String.Empty).ToLowerInvariant()
        If Not key.Contains("illumination") AndAlso Not key.Contains("reelsandleds") Then Return

        Dim dividers As New List(Of B2SLine)()
        CollectToolDividers(target, dividers)
        dividers.Sort(Function(left As B2SLine, right As B2SLine)
                          Dim leftPoint As Point = target.PointToClient(left.Parent.PointToScreen(left.Location))
                          Dim rightPoint As Point = target.PointToClient(right.Parent.PointToScreen(right.Location))
                          Return leftPoint.Y.CompareTo(rightPoint.Y)
                      End Function)
        If dividers.Count = 0 Then Return

        Dim isIllumination As Boolean = key.Contains("illumination")
        Dim contentBounds As Rectangle = RoundedChromeContentBounds(target)
        ' Keep the first illumination card edge below the LAMP PROPERTIES
        ' caption.  The previous 45px offset placed the card's top border
        ' directly through that heading.
        Dim sectionTop As Integer = contentBounds.Top + If(isIllumination, 53, 12)
        Dim sectionAccent As Color = accent
        For Each divider As B2SLine In dividers
            If Not divider.Visible Then Continue For
            Dim dividerPoint As Point = target.PointToClient(divider.Parent.PointToScreen(divider.Location))
            Dim boundary As Integer = dividerPoint.Y + (divider.Height \ 2)
            DrawToolSectionCard(graphics, target, sectionTop, boundary - 5, sectionAccent)
            sectionTop = boundary + 5
            proControlAccents.TryGetValue(divider, sectionAccent)
        Next
        DrawToolSectionCard(graphics, target, sectionTop, contentBounds.Bottom - 9, sectionAccent)
    End Sub

    Private Sub CollectToolDividers(parent As Control, dividers As List(Of B2SLine))
        For Each child As Control In parent.Controls
            Dim divider As B2SLine = TryCast(child, B2SLine)
            If divider IsNot Nothing Then dividers.Add(divider)
            If child.HasChildren Then CollectToolDividers(child, dividers)
        Next
    End Sub

    Private Sub DrawToolSectionCard(graphics As Graphics,
                                    target As Form,
                                    top As Integer,
                                    bottom As Integer,
                                    accent As Color)
        If bottom - top < 12 Then Return
        Dim contentBounds As Rectangle = RoundedChromeContentBounds(target)
        Dim bounds As New Rectangle(contentBounds.Left + 10,
                                    top,
                                    Math.Max(1, contentBounds.Width - 21),
                                    bottom - top)
        Using path As GraphicsPath = CreateRoundedRectangle(bounds, 13)
            Using glow As New Pen(Color.FromArgb(45, accent), 5.0F)
                graphics.DrawPath(glow, path)
            End Using
            Using fill As New LinearGradientBrush(bounds,
                                                   Color.FromArgb(26, accent),
                                                   Color.FromArgb(2, 4, 8),
                                                   LinearGradientMode.Vertical)
                graphics.FillPath(fill, path)
            End Using
            Using border As New Pen(Color.FromArgb(185, accent), 1.0F)
                graphics.DrawPath(border, path)
            End Using
        End Using
    End Sub

    Private Sub DrawToolControlFrames(graphics As Graphics, target As Form, accent As Color)
        Dim fields As New List(Of Control)()
        CollectToolFields(target, fields)
        DrawControlFrames(graphics, target, fields, accent)
    End Sub

    Private Sub DrawControlFrames(graphics As Graphics, target As Form, fields As List(Of Control), accent As Color)
        For Each field As Control In fields
            If Not field.Visible OrElse field.ClientSize.Width < 3 OrElse field.ClientSize.Height < 3 Then Continue For
            Dim screenPoint As Point = field.Parent.PointToScreen(field.Location)
            Dim fieldLocation As Point = target.PointToClient(screenPoint)
            Dim bounds As New Rectangle(fieldLocation.X - 2, fieldLocation.Y - 2, field.Width + 3, field.Height + 3)
            Dim fieldAccent As Color = accent
            proControlAccents.TryGetValue(field, fieldAccent)
            Dim focused As Boolean = field.ContainsFocus

            Using path As GraphicsPath = CreateRoundedRectangle(bounds, 5)
                ' A soft lower shadow and a cool top highlight give native
                ' controls the same raised black-glass depth as the reference UI.
                Dim shadowBounds As Rectangle = bounds
                shadowBounds.Offset(0, 2)
                Using shadowPath As GraphicsPath = CreateRoundedRectangle(shadowBounds, 5),
                      shadow As New Pen(Color.FromArgb(155, 0, 0, 0), 3.0F)
                    graphics.DrawPath(shadow, shadowPath)
                End Using
                If focused Then
                    Using glow As New Pen(Color.FromArgb(92, fieldAccent), 4.0F)
                        graphics.DrawPath(glow, path)
                    End Using
                End If
                Using border As New Pen(Color.FromArgb(If(focused, 238, 105), fieldAccent), If(focused, 1.4F, 1.0F))
                    graphics.DrawPath(border, path)
                End Using
                Using highlight As New Pen(Color.FromArgb(118, 220, 228, 255), 1.0F)
                    graphics.DrawLine(highlight, bounds.Left + 6, bounds.Top + 1, bounds.Right - 6, bounds.Top + 1)
                End Using
            End Using
        Next
    End Sub

    Private Sub CollectToolFields(parent As Control, fields As List(Of Control))
        For Each child As Control In parent.Controls
            If TypeOf child Is TextBoxBase OrElse TypeOf child Is ComboBox OrElse TypeOf child Is NumericUpDown OrElse
               child.Name.Equals("lblSelectedIlluminationType", StringComparison.OrdinalIgnoreCase) Then fields.Add(child)
            If child.HasChildren Then CollectToolFields(child, fields)
        Next
    End Sub

    Private Sub ApplyToolWindowCaptionTheme(target As Form, accent As Color)
        If target Is Nothing OrElse target.IsDisposed OrElse Not target.IsHandleCreated Then Return

        Try
            Dim captionColor As Integer = DwmColorRef(Color.FromArgb(3, 5, 9))
            Dim textColor As Integer = DwmColorRef(Color.White)
            Dim borderColor As Integer = DwmColorRef(accent)
            Dim cornerPreference As Integer = 2
            DwmSetWindowAttribute(target.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, cornerPreference, Marshal.SizeOf(GetType(Integer)))
            DwmSetWindowAttribute(target.Handle, DWMWA_CAPTION_COLOR, captionColor, Marshal.SizeOf(GetType(Integer)))
            DwmSetWindowAttribute(target.Handle, DWMWA_TEXT_COLOR, textColor, Marshal.SizeOf(GetType(Integer)))
            DwmSetWindowAttribute(target.Handle, DWMWA_BORDER_COLOR, borderColor, Marshal.SizeOf(GetType(Integer)))
        Catch ex As DllNotFoundException
        Catch ex As EntryPointNotFoundException
        End Try
    End Sub

    Private Sub B2SProToolWindow_FormClosed(sender As Object, e As FormClosedEventArgs)
        Dim target = TryCast(sender, Form)
        If target Is Nothing Then Return
        styledToolWindows.Remove(target)
        toolWindowAccents.Remove(target)
        ReleaseRoundedWindowChrome(target)
    End Sub

    Private Sub RestoreControl(control As Control)
        Dim appearance As ControlAppearance = Nothing
        If savedControls.TryGetValue(control, appearance) Then
            control.BackColor = appearance.BackColor
            control.ForeColor = appearance.ForeColor
            Dim button = TryCast(control, ButtonBase)
            If button IsNot Nothing AndAlso appearance.UseVisualStyleBackColor.HasValue Then
                button.UseVisualStyleBackColor = appearance.UseVisualStyleBackColor.Value
                button.FlatStyle = FlatStyle.Standard
            End If
        End If

        themedControls.Remove(control)

        If focusStyledControls.Remove(control) Then
            RemoveHandler control.Enter, AddressOf FocusControl_Enter
            RemoveHandler control.Leave, AddressOf FocusControl_Leave
            RemoveHandler control.Disposed, AddressOf FocusControl_Disposed
        End If

        Dim restoreButton = TryCast(control, ButtonBase)
        If restoreButton IsNot Nothing AndAlso neonButtons.Remove(restoreButton) Then
            RemoveHandler restoreButton.Paint, AddressOf NeonButton_Paint
            RemoveHandler restoreButton.Disposed, AddressOf NeonButton_Disposed
        End If

        Dim restoreGroup = TryCast(control, GroupBox)
        If restoreGroup IsNot Nothing AndAlso polishedGroupBoxes.Remove(restoreGroup) Then
            RemoveHandler restoreGroup.Paint, AddressOf PolishedGroupBox_Paint
            RemoveHandler restoreGroup.Disposed, AddressOf PolishedGroupBox_Disposed
            restoreGroup.FlatStyle = FlatStyle.Standard
        End If

        If roundedControls.Remove(control) Then
            RemoveHandler control.SizeChanged, AddressOf RoundedControl_SizeChanged
            RemoveHandler control.Disposed, AddressOf RoundedControl_Disposed
            roundedControlRadii.Remove(control)
            Dim oldRegion As Region = control.Region
            control.Region = Nothing
            If oldRegion IsNot Nothing Then oldRegion.Dispose()
        End If

        Dim tabs = TryCast(control, TabControl)
        If tabs IsNot Nothing AndAlso ownerDrawTabs.Remove(tabs) Then
            RemoveHandler tabs.DrawItem, AddressOf ModernTabs_DrawItem
            RemoveHandler tabs.Disposed, AddressOf ModernTabs_Disposed
            tabs.DrawMode = TabDrawMode.Normal
        End If

        Dim form = TryCast(control, Form)
        If form IsNot Nothing Then
            If styledToolWindows.Remove(form) Then
                RemoveHandler form.Paint, AddressOf B2SProToolWindow_Paint
                RemoveHandler form.FormClosed, AddressOf B2SProToolWindow_FormClosed
                toolWindowAccents.Remove(form)
            End If
            If styledDialogs.Remove(form) Then
                RemoveHandler form.Paint, AddressOf B2SProDialog_Paint
                RemoveHandler form.FormClosed, AddressOf B2SProDialog_FormClosed
                dialogAccents.Remove(form)
            End If
            ReleaseRoundedWindowChrome(form)
            ApplyCaptionTheme(form, False)
        End If

        Dim strip = TryCast(control, ToolStrip)
        If strip IsNot Nothing Then
            themedToolStrips.Remove(strip)
            Dim stripAppearance As ToolStripAppearance = Nothing
            If savedToolStrips.TryGetValue(strip, stripAppearance) Then
                strip.BackColor = stripAppearance.BackColor
                strip.ForeColor = stripAppearance.ForeColor
                strip.Renderer = stripAppearance.Renderer
            End If
        End If

        For Each child As Control In control.Controls
            RestoreControl(child)
        Next
    End Sub


    Private Sub ApplyCaptionTheme(target As Form, dark As Boolean)
        If target Is Nothing OrElse target.IsDisposed OrElse Not target.IsHandleCreated Then Return

        Try
            Dim captionColor As Integer
            Dim textColor As Integer
            Dim borderColor As Integer

            ' Keep the application caption blue in BOTH Light and Dark modes.
            ' Previously Light mode restored the Windows accent color. On systems
            ' with a red accent this made every title bar red again.
            If dark Then
                Dim accent As Color = AccentForControl(target)
                captionColor = DwmColorRef(Color.FromArgb(34, 38, 56))
                borderColor = DwmColorRef(accent)
            Else
                captionColor = DwmColorRef(Color.FromArgb(45, 112, 190)) ' lighter blue
                borderColor = DwmColorRef(Color.FromArgb(70, 140, 220))
            End If
            textColor = DwmColorRef(Color.White)

            Dim cornerPreference As Integer = 2
            DwmSetWindowAttribute(target.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, cornerPreference, Marshal.SizeOf(GetType(Integer)))
            DwmSetWindowAttribute(target.Handle, DWMWA_CAPTION_COLOR, captionColor, Marshal.SizeOf(GetType(Integer)))
            DwmSetWindowAttribute(target.Handle, DWMWA_TEXT_COLOR, textColor, Marshal.SizeOf(GetType(Integer)))
            DwmSetWindowAttribute(target.Handle, DWMWA_BORDER_COLOR, borderColor, Marshal.SizeOf(GetType(Integer)))
        Catch ex As DllNotFoundException
            ' DWM is unavailable on very old Windows versions.
        Catch ex As EntryPointNotFoundException
            ' Caption-color attributes are unavailable; retain the normal Windows title bar.
        End Try
    End Sub

    Private Function DwmColorRef(value As Color) As Integer
        ' DWM expects a native Windows COLORREF (0x00BBGGRR).
        ' ColorTranslator.ToWin32 performs the correct packing and avoids manual
        ' RGB/BGR reversals. Mask off the alpha/sign bits for DWM.
        Return ColorTranslator.ToWin32(value) And &HFFFFFF
    End Function

    Private Class DarkToolStripRenderer
        Inherits ToolStripProfessionalRenderer

        Public Sub New()
            MyBase.New(New DarkColorTable())
            RoundedEdges = True
        End Sub

        Protected Overrides Sub OnRenderToolStripBackground(e As ToolStripRenderEventArgs)
            Dim isStatusBar As Boolean = TypeOf e.ToolStrip Is StatusStrip
            Dim topColor As Color = If(isStatusBar, Color.FromArgb(11, 15, 22), Color.FromArgb(10, 12, 17))
            Dim bottomColor As Color = If(isStatusBar, Color.FromArgb(1, 3, 6), Color.FromArgb(0, 1, 3))

            Using brush As New LinearGradientBrush(e.AffectedBounds, topColor, bottomColor, LinearGradientMode.Vertical)
                e.Graphics.FillRectangle(brush, e.AffectedBounds)
            End Using

            If isStatusBar Then
                ' Stronger B2S Pro dashboard treatment: a bright neon crown and
                ' subtle lower glow make the status area visually distinct from
                ' the canvas without changing its layout or behavior.
                Using glow As New Pen(Color.FromArgb(105, 61, 139, 255), 4.0F)
                    e.Graphics.DrawLine(glow, e.AffectedBounds.Left, e.AffectedBounds.Top + 1, e.AffectedBounds.Right, e.AffectedBounds.Top + 1)
                End Using
                Using line As New Pen(Color.FromArgb(235, 74, 205, 255), 1.5F)
                    e.Graphics.DrawLine(line, e.AffectedBounds.Left, e.AffectedBounds.Top, e.AffectedBounds.Right, e.AffectedBounds.Top)
                End Using
                Using lowerGlow As New Pen(Color.FromArgb(55, 255, 126, 72), 2.0F)
                    e.Graphics.DrawLine(lowerGlow, e.AffectedBounds.Left, e.AffectedBounds.Bottom - 2, e.AffectedBounds.Right, e.AffectedBounds.Bottom - 2)
                End Using
            End If

            Using pen As New Pen(Color.FromArgb(80, 92, 34, 148))
                e.Graphics.DrawLine(pen, e.AffectedBounds.Left, e.AffectedBounds.Bottom - 1, e.AffectedBounds.Right, e.AffectedBounds.Bottom - 1)
            End Using
        End Sub

        Protected Overrides Sub OnRenderButtonBackground(e As ToolStripItemRenderEventArgs)
            Dim button = TryCast(e.Item, ToolStripButton)
            If button Is Nothing Then
                MyBase.OnRenderButtonBackground(e)
                Return
            End If

            Dim bounds As New Rectangle(1, 1, Math.Max(1, button.Width - 3), Math.Max(1, button.Height - 3))
            Dim accent As Color = AccentForItem(button)
            Dim active As Boolean = button.Selected OrElse button.Pressed OrElse button.Checked
            Dim topColor As Color
            Dim bottomColor As Color
            Dim borderColor As Color

            If active Then
                topColor = Color.FromArgb(112, accent)
                bottomColor = Color.FromArgb(34, accent)
                borderColor = Color.FromArgb(230, accent)
            Else
                topColor = Color.FromArgb(17, 20, 27)
                bottomColor = Color.FromArgb(3, 5, 9)
                borderColor = Color.FromArgb(82, accent)
            End If

            Dim cornerRadius As Integer = Math.Max(10, Math.Min(16, bounds.Height \ 3))
            Using path As GraphicsPath = RoundedRectangle(bounds, cornerRadius)
                Using brush As New LinearGradientBrush(bounds, topColor, bottomColor, LinearGradientMode.Vertical)
                    e.Graphics.FillPath(brush, path)
                End Using
                Using pen As New Pen(borderColor, If(active, 1.4F, 1.0F))
                    e.Graphics.DrawPath(pen, path)
                End Using
            End Using

            Dim underlineY As Integer = bounds.Bottom - 2
            Using glow As New Pen(Color.FromArgb(If(active, 210, 95), accent), If(active, 2.2F, 1.2F))
                e.Graphics.DrawLine(glow, bounds.Left + 7, underlineY, bounds.Right - 7, underlineY)
            End Using
        End Sub

        Protected Overrides Sub OnRenderItemImage(e As ToolStripItemImageRenderEventArgs)
            If e.Image Is Nothing Then Return

            Dim accent As Color = AccentForItem(e.Item)
            Dim halo As Rectangle = Rectangle.Inflate(e.ImageRectangle, 6, 5)
            halo.Offset(0, 1)
            Using path As GraphicsPath = RoundedRectangle(halo, 8)
                Using brush As New SolidBrush(Color.FromArgb(42, accent))
                    e.Graphics.FillPath(brush, path)
                End Using
                Using pen As New Pen(Color.FromArgb(95, accent), 1.0F)
                    e.Graphics.DrawPath(pen, path)
                End Using
            End Using

            MyBase.OnRenderItemImage(e)
        End Sub

        Protected Overrides Sub OnRenderLabelBackground(e As ToolStripItemRenderEventArgs)
            Dim label = TryCast(e.Item, ToolStripStatusLabel)
            If label Is Nothing OrElse label.IsLink Then
                MyBase.OnRenderLabelBackground(e)
                Return
            End If

            Dim bounds As New Rectangle(2, 2, Math.Max(1, e.Item.Width - 5), Math.Max(1, e.Item.Height - 5))
            Dim accent As Color = AccentForItem(e.Item)
            Using path As GraphicsPath = RoundedRectangle(bounds, Math.Max(9, Math.Min(14, bounds.Height \ 2)))
                ' High-contrast segmented dashboard capsules.  The darker center
                ' keeps text readable while the colored edge identifies each block.
                Using brush As New LinearGradientBrush(bounds, Color.FromArgb(105, accent), Color.FromArgb(30, 12, 15, 25), LinearGradientMode.Vertical)
                    e.Graphics.FillPath(brush, path)
                End Using
                Using outerGlow As New Pen(Color.FromArgb(75, accent), 3.0F)
                    e.Graphics.DrawPath(outerGlow, path)
                End Using
                Using pen As New Pen(Color.FromArgb(235, accent), 1.3F)
                    e.Graphics.DrawPath(pen, path)
                End Using

                Dim accentBar As New Rectangle(bounds.Left + 7, bounds.Bottom - 4, Math.Max(8, bounds.Width - 14), 2)
                Using barBrush As New SolidBrush(Color.FromArgb(230, accent))
                    e.Graphics.FillRectangle(barBrush, accentBar)
                End Using
            End Using
        End Sub

        Protected Overrides Sub OnRenderMenuItemBackground(e As ToolStripItemRenderEventArgs)
            Dim bounds As New Rectangle(1, 1, Math.Max(1, e.Item.Width - 3), Math.Max(1, e.Item.Height - 3))
            Dim accent As Color = AccentForItem(e.Item)
            Dim active As Boolean = e.Item.Selected OrElse e.Item.Pressed
            Dim cornerRadius As Integer = Math.Max(8, Math.Min(12, bounds.Height \ 2))
            Using path As GraphicsPath = RoundedRectangle(bounds, cornerRadius)
                Using glow As New Pen(Color.FromArgb(If(active, 58, 18), accent), If(active, 4.0F, 2.0F))
                    e.Graphics.DrawPath(glow, path)
                End Using
                Using brush As New LinearGradientBrush(bounds,
                                                       If(active, Color.FromArgb(112, accent), Color.FromArgb(27, 31, 42)),
                                                       If(active, Color.FromArgb(28, accent), Color.FromArgb(3, 5, 9)),
                                                       LinearGradientMode.Vertical)
                    e.Graphics.FillPath(brush, path)
                End Using
                Using pen As New Pen(Color.FromArgb(If(active, 225, 58), accent), 1.0F)
                    e.Graphics.DrawPath(pen, path)
                End Using
                Using highlight As New Pen(Color.FromArgb(If(active, 112, 45), 235, 240, 255), 1.0F)
                    e.Graphics.DrawLine(highlight, bounds.Left + 7, bounds.Top + 1, bounds.Right - 7, bounds.Top + 1)
                End Using
            End Using
        End Sub

        Protected Overrides Sub OnRenderToolStripBorder(e As ToolStripRenderEventArgs)
            If TypeOf e.ToolStrip Is ToolStripDropDown Then
                Dim bounds As New Rectangle(1, 1, Math.Max(1, e.ToolStrip.Width - 3), Math.Max(1, e.ToolStrip.Height - 3))
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
                Using path As GraphicsPath = RoundedRectangle(bounds, 12)
                    Using glow As New Pen(Color.FromArgb(46, 255, 174, 24), 3.0F)
                        e.Graphics.DrawPath(glow, path)
                    End Using
                    Using border As New Pen(Color.FromArgb(220, 255, 174, 24), 1.0F)
                        e.Graphics.DrawPath(border, path)
                    End Using
                End Using
            Else
                MyBase.OnRenderToolStripBorder(e)
            End If
        End Sub

        Protected Overrides Sub OnRenderSeparator(e As ToolStripSeparatorRenderEventArgs)
            If e.Vertical Then
                Dim x As Integer = e.Item.Width \ 2
                Using line As New Pen(Color.FromArgb(115, 82, 151, 255), 1.0F)
                    e.Graphics.DrawLine(line, x, 6, x, Math.Max(6, e.Item.Height - 6))
                End Using
            Else
                Dim y As Integer = e.Item.Height \ 2
                Using line As New Pen(Color.FromArgb(105, 181, 69, 255), 1.0F)
                    e.Graphics.DrawLine(line, 28, y, Math.Max(28, e.Item.Width - 6), y)
                End Using
            End If
        End Sub

        Protected Overrides Sub OnRenderItemText(e As ToolStripItemTextRenderEventArgs)
            e.TextColor = If(e.Item.Enabled, Color.FromArgb(238, 242, 255), Color.FromArgb(118, 124, 142))
            MyBase.OnRenderItemText(e)
        End Sub

        Private Shared Function AccentForItem(item As ToolStripItem) As Color
            Dim key As String = (If(item.Name, String.Empty) & " " & If(item.Text, String.Empty) & " " & If(item.ToolTipText, String.Empty)).ToLowerInvariant()

            ' Keep the bottom dashboard visually consistent and easy to scan.
            ' These checks intentionally run before the generic command colors.
            If TypeOf item Is ToolStripStatusLabel Then
                If key.Contains("statusinfo") OrElse key.Contains("ready") OrElse key.Contains("recovered") Then Return Color.FromArgb(44, 214, 116)
                If key.Contains("fileinfo") OrElse key.Contains("image:") OrElse key.Contains("no image") Then Return Color.FromArgb(52, 174, 255)
                If key.Contains("filesize") OrElse key.Contains("size:") OrElse key.Contains("zoom:") Then Return Color.FromArgb(255, 173, 52)
                If key.Contains("marker") OrElse key.Contains("x:") OrElse key.Contains("y:") Then Return Color.FromArgb(190, 92, 255)
            End If

            If key.Contains("light") OrElse key.Contains("illum") OrElse key.Contains("bulb") Then Return Color.FromArgb(255, 153, 48)
            If key.Contains("reel") OrElse key.Contains("led") OrElse key.Contains("score") Then Return Color.FromArgb(50, 220, 145)
            If key.Contains("image") OrElse key.Contains("resource") OrElse key.Contains("open") Then Return Color.FromArgb(48, 180, 255)
            If key.Contains("save") OrElse key.Contains("export") Then Return Color.FromArgb(255, 116, 86)
            If key.Contains("undo") OrElse key.Contains("redo") OrElse key.Contains("history") Then Return Color.FromArgb(197, 105, 255)
            If key.Contains("zoom") OrElse key.Contains("view") Then Return Color.FromArgb(86, 151, 255)
            Return Color.FromArgb(76, 137, 255)
        End Function

        Private Shared Function RoundedRectangle(bounds As Rectangle, radius As Integer) As GraphicsPath
            Dim path As New GraphicsPath()
            Dim diameter As Integer = Math.Max(2, radius * 2)
            Dim arc As New Rectangle(bounds.Location, New Size(diameter, diameter))
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
    End Class

    Private Class DarkColorTable
        Inherits ProfessionalColorTable

        Public Overrides ReadOnly Property ToolStripDropDownBackground As Color
            Get
                Return Color.FromArgb(2, 4, 7)
            End Get
        End Property
        Public Overrides ReadOnly Property MenuItemSelected As Color
            Get
                Return Color.FromArgb(35, 12, 55)
            End Get
        End Property
        Public Overrides ReadOnly Property MenuItemBorder As Color
            Get
                Return Color.FromArgb(186, 65, 255)
            End Get
        End Property
        Public Overrides ReadOnly Property MenuItemSelectedGradientBegin As Color
            Get
                Return Color.FromArgb(83, 25, 122)
            End Get
        End Property
        Public Overrides ReadOnly Property MenuItemSelectedGradientEnd As Color
            Get
                Return Color.FromArgb(18, 7, 29)
            End Get
        End Property
        Public Overrides ReadOnly Property MenuItemPressedGradientBegin As Color
            Get
                Return Color.FromArgb(91, 27, 130)
            End Get
        End Property
        Public Overrides ReadOnly Property MenuItemPressedGradientEnd As Color
            Get
                Return Color.FromArgb(20, 7, 32)
            End Get
        End Property
        Public Overrides ReadOnly Property ImageMarginGradientBegin As Color
            Get
                Return Color.FromArgb(3, 5, 9)
            End Get
        End Property
        Public Overrides ReadOnly Property ImageMarginGradientMiddle As Color
            Get
                Return Color.FromArgb(7, 9, 14)
            End Get
        End Property
        Public Overrides ReadOnly Property ImageMarginGradientEnd As Color
            Get
                Return Color.FromArgb(3, 5, 9)
            End Get
        End Property
        Public Overrides ReadOnly Property SeparatorDark As Color
            Get
                Return Color.FromArgb(125, 53, 195)
            End Get
        End Property
        Public Overrides ReadOnly Property SeparatorLight As Color
            Get
                Return Color.FromArgb(125, 53, 195)
            End Get
        End Property
        Public Overrides ReadOnly Property ToolStripBorder As Color
            Get
                Return Color.FromArgb(119, 46, 190)
            End Get
        End Property
        Public Overrides ReadOnly Property StatusStripGradientBegin As Color
            Get
                Return Color.FromArgb(24, 28, 42)
            End Get
        End Property
        Public Overrides ReadOnly Property StatusStripGradientEnd As Color
            Get
                Return Color.FromArgb(10, 12, 20)
            End Get
        End Property
    End Class
End Module
