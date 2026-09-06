'Imports System

Public Class formBase

    Inherits B2SThemedForm

    Private XML As Xml.XmlDocument = Nothing

    Private path As String = IO.Path.Combine(EXEDir, ProjectDir)
    ' Keep the established settings filename so the public B2S Pro rename does not
    ' make existing window preferences appear to vanish.
    Private filename As String = IO.Path.Combine(path, "B2S Designer.Settings.xml")

    Private IsLoadDone As Boolean = False

    Friend Enum eDefaultLocation
        NotDefined = 0
        NW = 1
        SW = 2
        SE = 3
        NE = 4
    End Enum
    Friend DefaultLocation As eDefaultLocation = eDefaultLocation.NotDefined
    Friend SaveName As String = String.Empty

    Private Sub formBase_Load(sender As Object, e As System.EventArgs) Handles Me.Load
        ' Every form derived from formBase now participates in the same robust,
        ' multi-monitor-aware layout persistence system.
        WindowStateManager.Attach(Me)
        LoadSettings()
        IsLoadDone = True
    End Sub

    Private Sub formBase_LocationChanged(sender As Object, e As System.EventArgs) Handles Me.LocationChanged
        SaveSettings()
    End Sub
    Private Sub formBase_SizeChanged(sender As Object, e As System.EventArgs) Handles Me.SizeChanged
        SaveSettings()
    End Sub

    Private Sub LoadSettings()
        ' Window geometry is restored by WindowStateManager. Keep the legacy
        ' settings file only for opacity and the original first-run placement.
        If IO.File.Exists(filename) Then
            Try
                XML = New Xml.XmlDocument
                XML.Load(filename)
                If XML IsNot Nothing AndAlso Not String.IsNullOrEmpty(SaveName) Then
                    Dim node As Xml.XmlElement = TryCast(XML.SelectSingleNode("B2SBackglassDesignerSettings/FormSettings/" & SaveName), Xml.XmlElement)
                    If node IsNot Nothing AndAlso node.SelectSingleNode("Opacity") IsNot Nothing Then
                        DefaultOpacity = CInt(node.SelectSingleNode("Opacity").Attributes("Value").InnerText) / 100
                    End If
                End If
            Catch
                XML = Nothing
            End Try
        ElseIf DefaultLocation <> eDefaultLocation.NotDefined AndAlso Me.Owner IsNot Nothing AndAlso Not WindowStateManager.HasSavedState(Me) Then
            Dim x As Integer
            Dim y As Integer
            If DefaultLocation = eDefaultLocation.NW OrElse DefaultLocation = eDefaultLocation.SW Then
                x = Me.Owner.Location.X + Me.Owner.Width - Me.Size.Width - 20
            Else
                x = Me.Owner.Location.X + 20
            End If
            If DefaultLocation = eDefaultLocation.SW OrElse DefaultLocation = eDefaultLocation.SE Then
                y = Me.Owner.Location.Y + Me.Owner.Height - Me.Size.Height - 20
            Else
                y = Me.Owner.Location.Y + 20
            End If
            Me.Location = New Point(x, y)
        End If
        If Not SaveName.Equals("formDesigner", StringComparison.CurrentCultureIgnoreCase) Then Me.Opacity = DefaultOpacity
    End Sub
    Public Sub SaveSettings()
        If Not IsLoadDone Then Return
        WindowStateManager.SaveNow(Me)
        If String.IsNullOrEmpty(SaveName) Then Return
        If CheckSaveDir() Then
            Try
                If XML Is Nothing Then XML = New Xml.XmlDocument
                Dim nodeHeader As Xml.XmlElement = XML.SelectSingleNode("B2SBackglassDesignerSettings")
                If nodeHeader Is Nothing Then
                    nodeHeader = XML.CreateElement("B2SBackglassDesignerSettings")
                    XML.AppendChild(nodeHeader)
                End If
                Dim nodeForms As Xml.XmlElement = nodeHeader.SelectSingleNode("FormSettings")
                If nodeForms Is Nothing Then
                    nodeForms = XML.CreateElement("FormSettings")
                    nodeHeader.AppendChild(nodeForms)
                End If
                Dim nodeForm As Xml.XmlElement = nodeForms.SelectSingleNode(SaveName)
                If nodeForm Is Nothing Then
                    nodeForm = XML.CreateElement(SaveName)
                    nodeForms.AppendChild(nodeForm)
                End If
                If DefaultOpacity <> 1 AndAlso SaveName.Equals("formDesigner", StringComparison.CurrentCultureIgnoreCase) Then
                    AddXMLAttribute(XML, nodeForm, "Opacity", "Value", CInt(DefaultOpacity * 100).ToString())
                End If
                XML.Save(filename)
            Catch
            End Try
        End If
    End Sub

    Private Function CheckSaveDir() As Boolean
        If Not IO.Directory.Exists(path) Then
            IO.Directory.CreateDirectory(path)
        End If
        Return (IO.Directory.Exists(path))
    End Function

    Private Sub AddXMLAttribute(ByRef XML As Xml.XmlDocument, ByRef nodeHeader As Xml.XmlElement, ByVal element As String, ByVal attribut As String, ByVal value As String)
        Dim node As Xml.XmlElement = nodeHeader.SelectSingleNode(element)
        If node Is Nothing Then
            node = XML.CreateElement(element)
        End If
        node.SetAttribute(attribut, value)
        nodeHeader.AppendChild(node)
    End Sub

End Class
