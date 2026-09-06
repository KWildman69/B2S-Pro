Imports System.Runtime.CompilerServices

Public Module LayerManager
    Private ReadOnly hiddenItems As New HashSet(Of Object)(New ReferenceComparer())
    Private ReadOnly lockedItems As New HashSet(Of Object)(New ReferenceComparer())
    Private ReadOnly opacityItems As New Dictionary(Of Object, Integer)(New ReferenceComparer())
    Private showLights As Boolean = True
    Private showFlashers As Boolean = True
    Private showSnippets As Boolean = True

    Public Function IsVisible(item As Object) As Boolean
        If item Is Nothing OrElse hiddenItems.Contains(item) Then Return False
        If TypeOf item Is Illumination.BulbInfo Then
            Dim bulb As Illumination.BulbInfo = DirectCast(item, Illumination.BulbInfo)
            If bulb.IsImageSnippit Then Return showSnippets
            If bulb.IlluMode = Illumination.eIlluMode.Flasher OrElse bulb.LightPurpose = Illumination.eLightPurpose.Flasher Then Return showFlashers
            Return showLights
        End If
        Return True
    End Function

    Public Function IsLightCategoryVisible() As Boolean
        Return showLights
    End Function

    Public Function IsFlasherCategoryVisible() As Boolean
        Return showFlashers
    End Function

    Public Function IsSnippetCategoryVisible() As Boolean
        Return showSnippets
    End Function

    Public Sub SetLightCategoryVisible(visible As Boolean)
        showLights = visible
    End Sub

    Public Sub SetFlasherCategoryVisible(visible As Boolean)
        showFlashers = visible
    End Sub

    Public Sub SetSnippetCategoryVisible(visible As Boolean)
        showSnippets = visible
    End Sub

    Public Sub SetVisible(item As Object, visible As Boolean)
        If item Is Nothing Then Return
        If visible Then hiddenItems.Remove(item) Else hiddenItems.Add(item)
    End Sub

    Public Function IsLocked(item As Object) As Boolean
        Return item IsNot Nothing AndAlso lockedItems.Contains(item)
    End Function

    Public Sub SetLocked(item As Object, locked As Boolean)
        If item Is Nothing Then Return
        If locked Then lockedItems.Add(item) Else lockedItems.Remove(item)
    End Sub

    Public Function GetOpacity(item As Object) As Integer
        If item Is Nothing OrElse Not opacityItems.ContainsKey(item) Then Return 100
        Return opacityItems(item)
    End Function

    Public Sub SetOpacity(item As Object, value As Integer)
        If item Is Nothing Then Return
        opacityItems(item) = Math.Max(0, Math.Min(100, value))
    End Sub

    Public Sub Forget(item As Object)
        hiddenItems.Remove(item)
        lockedItems.Remove(item)
        opacityItems.Remove(item)
    End Sub

    Private Class ReferenceComparer
        Implements IEqualityComparer(Of Object)
        Public Overloads Function Equals(x As Object, y As Object) As Boolean Implements IEqualityComparer(Of Object).Equals
            Return Object.ReferenceEquals(x, y)
        End Function
        Public Shadows Function GetHashCode(obj As Object) As Integer Implements IEqualityComparer(Of Object).GetHashCode
            Return RuntimeHelpers.GetHashCode(obj)
        End Function
    End Class
End Module
