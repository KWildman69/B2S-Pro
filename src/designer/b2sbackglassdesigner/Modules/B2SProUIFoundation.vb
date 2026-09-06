Imports System.Drawing
Imports System.Windows.Forms

''' <summary>
''' Shared visual specification for the B2S Pro interface rebuild.
''' Phase 1 introduces the architecture only; existing forms keep their current
''' appearance and behavior until they are migrated in later phases.
''' </summary>
Public Module B2SProUIFoundation

    Public Enum CommandGroup
        File
        Edit
        View
        Lighting
        Artwork
        PlayExport
        Neutral
    End Enum

    Public Enum SurfaceRole
        Application
        Header
        Toolbar
        Panel
        Card
        Input
        Popup
    End Enum

    Public NotInheritable Class Metrics
        Public Const CornerRadiusSmall As Integer = 6
        Public Const CornerRadiusMedium As Integer = 10
        Public Const CornerRadiusLarge As Integer = 14
        Public Const ControlHeight As Integer = 32
        Public Const ToolbarButtonWidth As Integer = 64
        Public Const ToolbarButtonHeight As Integer = 70
        Public Const ToolbarIconSize As Integer = 30
        Public Const PanelHeaderHeight As Integer = 38
        Public Const SpaceSmall As Integer = 4
        Public Const SpaceMedium As Integer = 8
        Public Const SpaceLarge As Integer = 12

        Private Sub New()
        End Sub
    End Class

    Public NotInheritable Class Palette
        Public Shared ReadOnly ApplicationBack As Color = Color.FromArgb(7, 9, 16)
        Public Shared ReadOnly HeaderBack As Color = Color.FromArgb(9, 12, 21)
        Public Shared ReadOnly ToolbarBack As Color = Color.FromArgb(10, 14, 24)
        Public Shared ReadOnly PanelBack As Color = Color.FromArgb(10, 14, 23)
        Public Shared ReadOnly CardBack As Color = Color.FromArgb(14, 18, 29)
        Public Shared ReadOnly InputBack As Color = Color.FromArgb(8, 11, 18)
        Public Shared ReadOnly Border As Color = Color.FromArgb(55, 68, 92)
        Public Shared ReadOnly TextPrimary As Color = Color.FromArgb(242, 246, 255)
        Public Shared ReadOnly TextSecondary As Color = Color.FromArgb(174, 185, 204)
        Public Shared ReadOnly Cyan As Color = Color.FromArgb(0, 205, 255)
        Public Shared ReadOnly Blue As Color = Color.FromArgb(40, 126, 255)
        Public Shared ReadOnly Purple As Color = Color.FromArgb(178, 60, 255)
        Public Shared ReadOnly Magenta As Color = Color.FromArgb(255, 36, 180)
        Public Shared ReadOnly Amber As Color = Color.FromArgb(255, 174, 28)
        Public Shared ReadOnly Green As Color = Color.FromArgb(45, 222, 76)
        Public Shared ReadOnly Red As Color = Color.FromArgb(255, 54, 70)

        Private Sub New()
        End Sub
    End Class

    Public Function AccentFor(group As CommandGroup) As Color
        Select Case group
            Case CommandGroup.File
                Return Palette.Cyan
            Case CommandGroup.Edit
                Return Palette.Magenta
            Case CommandGroup.View
                Return Palette.Blue
            Case CommandGroup.Lighting
                Return Palette.Amber
            Case CommandGroup.Artwork
                Return Palette.Purple
            Case CommandGroup.PlayExport
                Return Palette.Green
            Case Else
                Return Palette.Border
        End Select
    End Function

    Public Function CommandGroupForItem(item As ToolStripItem) As CommandGroup
        If item Is Nothing Then Return CommandGroup.Neutral

        Dim key As String = (item.Name & " " & item.Text).ToLowerInvariant()
        If key.Contains("new") OrElse key.Contains("open") OrElse key.Contains("save") Then Return CommandGroup.File
        If key.Contains("undo") OrElse key.Contains("redo") OrElse key.Contains("cut") OrElse
           key.Contains("copy") OrElse key.Contains("paste") OrElse key.Contains("delete") Then Return CommandGroup.Edit
        If key.Contains("zoom") OrElse key.Contains("grid") OrElse key.Contains("ruler") OrElse key.Contains("fit") Then Return CommandGroup.View
        If key.Contains("light") OrElse key.Contains("illu") OrElse key.Contains("bulb") OrElse key.Contains("flash") OrElse key.Contains("animation") Then Return CommandGroup.Lighting
        If key.Contains("image") OrElse key.Contains("snippet") OrElse key.Contains("snippit") OrElse key.Contains("reel") Then Return CommandGroup.Artwork
        If key.Contains("play") OrElse key.Contains("preview") OrElse key.Contains("export") OrElse key.Contains("create") Then Return CommandGroup.PlayExport
        Return CommandGroup.Neutral
    End Function

    Public Function AccentForForm(form As Form) As Color
        If form Is Nothing Then Return Palette.Cyan

        Dim key As String = (form.Name & " " & form.Text).ToLowerInvariant()
        If key.Contains("illumination") OrElse key.Contains("light") OrElse key.Contains("flash") Then Return Palette.Amber
        If key.Contains("layer") Then Return Palette.Purple
        If key.Contains("reel") OrElse key.Contains("led") Then Return Palette.Green
        If key.Contains("resource") OrElse key.Contains("image") Then Return Palette.Cyan
        If key.Contains("animation") Then Return Palette.Green
        If key.Contains("snippet") OrElse key.Contains("snippit") Then Return Palette.Magenta
        If key.Contains("history") OrElse key.Contains("undo") Then Return Palette.Blue
        Return Palette.Cyan
    End Function

End Module
