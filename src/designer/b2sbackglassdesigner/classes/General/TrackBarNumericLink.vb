Imports System
Imports System.Windows.Forms

''' <summary>
''' Keeps a manual integer editor synchronized with an existing TrackBar.
''' The TrackBar remains the single value source, so its established preview,
''' model-update, and save handlers continue to run unchanged.
''' </summary>
Friend NotInheritable Class TrackBarNumericLink

    Private Sub New()
    End Sub

    Public Shared Sub Bind(ByVal slider As TrackBar,
                           ByVal number As NumericUpDown,
                           Optional ByVal increment As Integer = 1)
        If slider Is Nothing OrElse number Is Nothing Then Return

        number.DecimalPlaces = 0
        number.Minimum = slider.Minimum
        number.Maximum = slider.Maximum
        number.Increment = Math.Max(1, increment)
        number.ThousandsSeparator = slider.Maximum >= 1000
        number.TextAlign = HorizontalAlignment.Right
        number.Value = slider.Value
        number.Enabled = slider.Enabled

        Dim synchronizing As Boolean = False
        AddHandler slider.ValueChanged,
            Sub(sender As Object, e As EventArgs)
                If synchronizing Then Return
                synchronizing = True
                Try
                    If number.Minimum <> slider.Minimum Then number.Minimum = slider.Minimum
                    If number.Maximum <> slider.Maximum Then number.Maximum = slider.Maximum
                    If number.Value <> slider.Value Then number.Value = slider.Value
                Finally
                    synchronizing = False
                End Try
            End Sub
        AddHandler number.ValueChanged,
            Sub(sender As Object, e As EventArgs)
                If synchronizing Then Return
                synchronizing = True
                Try
                    Dim value As Integer = Math.Max(slider.Minimum, Math.Min(slider.Maximum, Decimal.ToInt32(number.Value)))
                    If slider.Value <> value Then slider.Value = value
                Finally
                    synchronizing = False
                End Try
            End Sub
        AddHandler slider.EnabledChanged,
            Sub(sender As Object, e As EventArgs)
                number.Enabled = slider.Enabled
            End Sub
    End Sub

End Class
