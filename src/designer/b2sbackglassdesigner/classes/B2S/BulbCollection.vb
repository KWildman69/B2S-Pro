Namespace Illumination

    Public Class BulbCollection

        Inherits Generic.SortedList(Of Integer, Illumination.BulbInfo)

        ' Enhanced 3.0.6 Stage 5: the original Add method scanned every existing
        ' bulb to find the next collection key and object ID. Repeating that for
        ' hundreds of lights produced quadratic add time. These monotonic counters
        ' preserve the existing max-plus-one behavior without rescanning the list.
        Private nextKey As Integer = 1
        Private nextID As Integer = 1

        Public Shadows Sub Add(value As Illumination.BulbInfo)
            If value Is Nothing Then Throw New ArgumentNullException("value")

            ' Stay robust if code inserted a keyed item through the base collection.
            If Count > 0 Then
                Dim highestKey As Integer = Keys(Count - 1)
                If nextKey <= highestKey Then nextKey = highestKey + 1
            End If
            While ContainsKey(nextKey)
                nextKey += 1
            End While

            value.ID = nextID
            nextID += 1
            MyBase.Add(nextKey, value)
            nextKey += 1
        End Sub

        Public Shadows Sub Clear()
            MyBase.Clear()
            nextKey = 1
            nextID = 1
        End Sub

    End Class

End Namespace
