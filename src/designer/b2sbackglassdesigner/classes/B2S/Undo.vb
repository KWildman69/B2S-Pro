Public Class Undo

    Public Shared Property SelectedBackglass() As B2STabPage = Nothing
    Public Shared Property ListBox() As ListBox = Nothing

    Public Enum Type
        Undefined = 0
        BulbAdded = 1
        ScoreAdded = 2
        BulbOrScoreMoved = 3
        BulbRemoved = 4
        ScoreRemoved = 5
        ImageImported = 51
        ImageReloaded = 52
        ImageChanged = 53
        ImageResized = 54
        ImageBrightnessChanged = 55
        ImageRemoved = 61
        IlluminationImageImported = 71
        IlluminationImageChanged = 73
        DMDImageImported = 81
        DMDImageChanged = 83
    End Enum
    Public Shared UndoList As UndoCollection = New UndoCollection
    Public Shared RedoList As UndoCollection = New UndoCollection
    Public Class UndoCollection

        Inherits Generic.List(Of UndoEntry)

    End Class
    Public Class UndoEntry

        Public Type As Type = Type.Undefined
        Public Item As Object = Nothing
        Public Data1 As Object = Nothing
        Public Data2 As Object = Nothing
        Public Data3 As Object = Nothing
        Public Data4 As Object = Nothing
        Public Owner As B2STabPage = Nothing
        Public RedoItem As Object = Nothing

        Public Sub New(ByVal _type As Type, ByVal _item As Object)
            Type = _type
            Item = _item
        End Sub
        Public Sub New(ByVal _type As Type, ByVal _item As Object, ByVal _data1 As Object)
            Type = _type
            Item = _item
            Data1 = _data1
        End Sub
        Public Sub New(ByVal _type As Type, ByVal _item As Object, ByVal _data1 As Object, ByVal _data2 As Object, ByVal _data3 As Object, ByVal _data4 As Object)
            Type = _type
            Item = _item
            Data1 = _data1
            Data2 = _data2
            Data3 = _data3
            Data4 = _data4
        End Sub

        Public ReadOnly Property Image() As Image
            Get
                Select Case Type
                    Case B2SBackglassDesigner.Undo.Type.BulbAdded
                        Return My.Resources.marker_newbulb
                    Case B2SBackglassDesigner.Undo.Type.ScoreAdded
                        Return My.Resources.marker_reel2
                    Case B2SBackglassDesigner.Undo.Type.BulbOrScoreMoved
                        Return My.Resources.marker_bulb
                    Case B2SBackglassDesigner.Undo.Type.BulbRemoved
                        Return My.Resources.delete_red
                    Case B2SBackglassDesigner.Undo.Type.ScoreRemoved
                        Return My.Resources.delete_red
                    Case B2SBackglassDesigner.Undo.Type.ImageImported
                        Return My.Resources.chooseback3
                    Case B2SBackglassDesigner.Undo.Type.ImageReloaded
                        Return My.Resources.chooseback2
                    Case B2SBackglassDesigner.Undo.Type.ImageChanged
                        Return My.Resources.chooseback1
                    Case B2SBackglassDesigner.Undo.Type.ImageResized
                        Return My.Resources.resize
                    Case B2SBackglassDesigner.Undo.Type.ImageRemoved

                    Case B2SBackglassDesigner.Undo.Type.ImageBrightnessChanged
                        Return My.Resources.brightness
                    Case B2SBackglassDesigner.Undo.Type.IlluminationImageImported

                    Case B2SBackglassDesigner.Undo.Type.IlluminationImageChanged

                    Case B2SBackglassDesigner.Undo.Type.DMDImageImported

                    Case B2SBackglassDesigner.Undo.Type.DMDImageChanged

                End Select
                Return My.Resources.designer
            End Get
        End Property

        Public Overrides Function ToString() As String
            Select Case Type
                Case B2SBackglassDesigner.Undo.Type.BulbAdded
                    Return My.Resources.UNDO_BulbAdded
                Case B2SBackglassDesigner.Undo.Type.ScoreAdded
                    Return My.Resources.UNDO_ScoreAdded
                Case B2SBackglassDesigner.Undo.Type.BulbOrScoreMoved
                    Return My.Resources.UNDO_BulbOrScoreMoved
                Case B2SBackglassDesigner.Undo.Type.BulbRemoved
                    Return My.Resources.UNDO_BulbRemoved
                Case B2SBackglassDesigner.Undo.Type.ScoreRemoved
                    Return My.Resources.UNDO_ScoreRemoved
                Case B2SBackglassDesigner.Undo.Type.ImageImported
                    Return My.Resources.UNDO_ImageImported
                Case B2SBackglassDesigner.Undo.Type.ImageReloaded
                    Return My.Resources.UNDO_ImageReloaded
                Case B2SBackglassDesigner.Undo.Type.ImageChanged
                    Return My.Resources.UNDO_ImageChanged
                Case B2SBackglassDesigner.Undo.Type.ImageResized
                    Return My.Resources.UNDO_ImageResized
                Case B2SBackglassDesigner.Undo.Type.ImageRemoved

                Case B2SBackglassDesigner.Undo.Type.ImageBrightnessChanged
                    Return My.Resources.UNDO_ImageBrightnessChanged
                Case B2SBackglassDesigner.Undo.Type.IlluminationImageImported

                Case B2SBackglassDesigner.Undo.Type.IlluminationImageChanged

                Case B2SBackglassDesigner.Undo.Type.DMDImageImported

                Case B2SBackglassDesigner.Undo.Type.DMDImageChanged

            End Select
            Return Type.ToString()
        End Function

    End Class

    Public Shared Sub AddEntry(item As UndoEntry)
        If item Is Nothing Then Return
        If item.Owner Is Nothing Then item.Owner = SelectedBackglass
        UndoList.Add(item)
        ' A new edit starts a new history branch. Redo entries from the old
        ' branch must not be replayed over the new document state.
        RedoList.Clear()
        ' add to undo listbox
        If ListBox IsNot Nothing AndAlso Not ListBox.IsDisposed Then
            ListBox.Items.Add(item)
            ListBox.SelectedItem = item
        End If
    End Sub
    Public Shared Sub Clear()
        UndoList.Clear()
        RedoList.Clear()
        If ListBox IsNot Nothing AndAlso Not ListBox.IsDisposed Then
            ListBox.Items.Clear()
            ListBox.SelectedItem = Nothing
        End If
    End Sub

    Public Shared Sub Undo()
        If UndoList.Count = 0 Then Return

        Dim index As Integer = UndoList.Count - 1
        Dim current As UndoEntry = UndoList(index)
        Dim target As B2STabPage = If(current.Owner, SelectedBackglass)
        If target Is Nothing OrElse Not ApplyEntry(current, target, True) Then Return

        UndoList.RemoveAt(index)
        RedoList.Add(current)
        If ListBox IsNot Nothing AndAlso Not ListBox.IsDisposed Then
            If index < ListBox.Items.Count Then ListBox.Items.RemoveAt(index)
            ListBox.SelectedIndex = Math.Min(index - 1, ListBox.Items.Count - 1)
        End If
        target.Invalidate()
    End Sub

    Public Shared Sub Redo()
        If RedoList.Count = 0 Then Return

        Dim redoIndex As Integer = RedoList.Count - 1
        Dim current As UndoEntry = RedoList(redoIndex)
        Dim target As B2STabPage = If(current.Owner, SelectedBackglass)
        If target Is Nothing OrElse Not ApplyEntry(current, target, False) Then Return

        RedoList.RemoveAt(redoIndex)
        UndoList.Add(current)
        If ListBox IsNot Nothing AndAlso Not ListBox.IsDisposed Then
            ListBox.Items.Add(current)
            ListBox.SelectedItem = current
        End If
        target.Invalidate()
    End Sub

    Private Shared Function ApplyEntry(entry As UndoEntry, target As B2STabPage, undoing As Boolean) As Boolean
        If entry Is Nothing OrElse target Is Nothing OrElse target.BackglassData Is Nothing Then Return False

        Select Case entry.Type
            Case Type.BulbAdded
                Dim bulb = TryCast(entry.Item, Illumination.BulbInfo)
                If bulb Is Nothing Then Return False
                Dim bulbs = BulbsFor(target, bulb)
                If undoing Then
                    bulbs.Remove(bulb)
                    If Object.ReferenceEquals(target.Mouse.SelectedBulb, bulb) Then target.Mouse.SelectedBulb = Nothing
                Else
                    If Not bulbs.Contains(bulb) Then bulbs.Add(bulb)
                    target.Mouse.SelectedBulb = bulb
                End If

            Case Type.ScoreAdded
                Dim score = TryCast(entry.Item, ReelAndLED.ScoreInfo)
                If score Is Nothing Then Return False
                Dim scores = ScoresFor(target, score)
                If undoing Then
                    scores.Remove(score)
                    If Object.ReferenceEquals(target.Mouse.SelectedScore, score) Then target.Mouse.SelectedScore = Nothing
                Else
                    If Not scores.Contains(score) Then scores.Add(score.ID, score)
                    target.Mouse.SelectedScore = score
                End If

            Case Type.BulbOrScoreMoved
                Dim item = TryCast(entry.Item, InfoBase)
                If item Is Nothing Then Return False
                item.Location = DirectCast(If(undoing, entry.Data1, entry.Data3), Point)
                item.Size = DirectCast(If(undoing, entry.Data2, entry.Data4), Size)

            Case Type.BulbRemoved
                Dim bulb = TryCast(entry.Item, Illumination.BulbInfo)
                If bulb Is Nothing Then Return False
                Dim bulbs = BulbsFor(target, bulb)
                If undoing Then
                    If Not bulbs.Contains(bulb) Then bulbs.Add(bulb)
                    target.Mouse.SelectedBulb = bulb
                Else
                    bulbs.Remove(bulb)
                    If Object.ReferenceEquals(target.Mouse.SelectedBulb, bulb) Then target.Mouse.SelectedBulb = Nothing
                End If

            Case Type.ScoreRemoved
                Dim score = TryCast(entry.Item, ReelAndLED.ScoreInfo)
                If score Is Nothing Then Return False
                Dim scores = ScoresFor(target, score)
                If undoing Then
                    If Not scores.Contains(score) Then scores.Add(score.ID, score)
                    target.Mouse.SelectedScore = score
                Else
                    scores.Remove(score)
                    If Object.ReferenceEquals(target.Mouse.SelectedScore, score) Then target.Mouse.SelectedScore = Nothing
                End If

            Case Type.ImageImported, Type.ImageReloaded, Type.ImageChanged
                If undoing Then entry.RedoItem = target.Image
                target.Image = DirectCast(If(undoing, entry.Item, entry.RedoItem), Image)

            Case Type.ImageResized, Type.ImageBrightnessChanged
                Dim useDMD As Boolean = (entry.Data1 IsNot Nothing AndAlso CBool(entry.Data1))
                If useDMD Then
                    If undoing Then entry.RedoItem = target.DMDImage
                    target.DMDImage = DirectCast(If(undoing, entry.Item, entry.RedoItem), Image)
                Else
                    If undoing Then entry.RedoItem = target.Image
                    target.Image = DirectCast(If(undoing, entry.Item, entry.RedoItem), Image)
                End If

            Case Type.DMDImageImported, Type.DMDImageChanged
                If undoing Then entry.RedoItem = target.DMDImage
                target.DMDImage = DirectCast(If(undoing, entry.Item, entry.RedoItem), Image)

            Case Type.ImageRemoved, Type.IlluminationImageImported, Type.IlluminationImageChanged
                ' These legacy entry types never stored enough information to
                ' reconstruct their image-list operation. Preserve their prior
                ' no-op behavior without corrupting the two history stacks.

            Case Else
                Return False
        End Select

        target.BackglassData.IsDirty = True
        Return True
    End Function

    Private Shared Function BulbsFor(target As B2STabPage, bulb As Illumination.BulbInfo) As Illumination.BulbCollection
        Return If(bulb.ParentForm = eParentForm.DMD, target.BackglassData.DMDBulbs, target.BackglassData.Bulbs)
    End Function

    Private Shared Function ScoresFor(target As B2STabPage, score As ReelAndLED.ScoreInfo) As ReelAndLED.ScoreCollection
        Return If(score.ParentForm = eParentForm.DMD, target.BackglassData.DMDScores, target.BackglassData.Scores)
    End Function

End Class
