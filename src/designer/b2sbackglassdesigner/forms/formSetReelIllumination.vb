Imports System

Public Class formSetReelIllumination

    Private IsDirty As Boolean = False
    Private ignoreChanges As Boolean = False

    Public Event LivePreviewChanged(ByVal location As eReelIlluminationLocation, ByVal intensity As Integer)

    Public Shadows Function ShowDialog(ByVal owner As IWin32Window,
                                       ByRef reelillulocation As eReelIlluminationLocation,
                                       ByRef reelillub2sid As Integer,
                                       ByRef reelillub2sidtype As eB2SIDType,
                                       ByRef reelillub2svalue As Integer,
                                       ByRef reelilluintensity As Integer) As DialogResult
        ignoreChanges = True
        cmbIlluLocation.SelectedIndex = reelillulocation
        cmbB2SIDType.SelectedIndex = reelillub2sidtype
        If reelillub2sid > 0 Then txtB2SID.Text = reelillub2sid
        If reelillub2svalue > 0 Then txtB2SValue.Text = reelillub2svalue
        TrackBarIntensity.Value = Math.Max(TrackBarIntensity.Minimum, Math.Min(TrackBarIntensity.Maximum, reelilluintensity))
        UpdateIntensityLabel()
        ignoreChanges = False
        ' now show the dialog
        Dim nRet As DialogResult = MyBase.ShowDialog(owner)
        If nRet = Windows.Forms.DialogResult.OK Then
            reelillulocation = cmbIlluLocation.SelectedIndex
            reelillub2sidtype = cmbB2SIDType.SelectedIndex
            reelillub2sid = If(Not String.IsNullOrEmpty(txtB2SID.Text), CInt(txtB2SID.Text), 0)
            reelillub2svalue = If(Not String.IsNullOrEmpty(txtB2SValue.Text), CInt(txtB2SValue.Text), 0)
            reelilluintensity = TrackBarIntensity.Value
        Else
            RaiseEvent LivePreviewChanged(reelillulocation, reelilluintensity)
        End If
        Return nRet
    End Function

    Private Sub IntensityChanged(sender As Object, e As EventArgs) Handles TrackBarIntensity.ValueChanged
        UpdateIntensityLabel()
        If ignoreChanges Then Return
        IsDirty = True
        RaiseLivePreview()
    End Sub

    Private Sub IlluminationLocationChanged(sender As Object, e As EventArgs) Handles cmbIlluLocation.SelectedIndexChanged
        If ignoreChanges OrElse cmbIlluLocation.SelectedIndex < 0 Then Return
        IsDirty = True
        RaiseLivePreview()
    End Sub

    Private Sub UpdateIntensityLabel()
        If lblIntensity IsNot Nothing AndAlso TrackBarIntensity IsNot Nothing Then lblIntensity.Text = "Intensity: " & TrackBarIntensity.Value.ToString()
    End Sub

    Private Sub RaiseLivePreview()
        RaiseEvent LivePreviewChanged(CType(Math.Max(0, cmbIlluLocation.SelectedIndex), eReelIlluminationLocation), TrackBarIntensity.Value)
    End Sub

    Private Sub formSetLEDColor_Shown(sender As Object, e As System.EventArgs) Handles Me.Shown
        IsDirty = False
    End Sub

    Private Sub txtB2SID_TextChanged(sender As System.Object, e As System.EventArgs) Handles txtB2SID.TextChanged
        If ignoreChanges Then Return
        If Not String.IsNullOrEmpty(txtB2SID.Text) Then cmbB2SIDType.SelectedIndex = 0
    End Sub
    Private Sub B2SIDType_SelectedIndexChanged(sender As Object, e As System.EventArgs) Handles cmbB2SIDType.SelectedIndexChanged
        If ignoreChanges Then Return
        ignoreChanges = True
        Select Case cmbB2SIDType.SelectedIndex
            Case 1 : txtB2SID.Text = 25
            Case 2 : txtB2SID.Text = 26
            Case 3 : txtB2SID.Text = 27
            Case 4 : txtB2SID.Text = 28
            Case 5 : txtB2SID.Text = 30
            Case 6 : txtB2SID.Text = 31
            Case 7 : txtB2SID.Text = 32
            Case 8 : txtB2SID.Text = 33
            Case 9 : txtB2SID.Text = 34
            Case 10 : txtB2SID.Text = 35
            Case 11 : txtB2SID.Text = 36
        End Select
        ignoreChanges = False
    End Sub

    Private Sub Ok_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnOk.Click
        MyBase.DialogResult = Windows.Forms.DialogResult.OK
        Me.Close()
    End Sub
    Private Sub Cancel_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnCancel.Click
        If IsDirty Then
            Dim ret As DialogResult = B2SMessageBox.Show(My.Resources.MSG_IsDirty, AppTitle, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question)
            If ret = Windows.Forms.DialogResult.Yes Then
                btnOk.PerformClick()
            ElseIf ret = Windows.Forms.DialogResult.No Then
                Me.Close()
            End If
        Else
            Me.Close()
        End If
    End Sub

End Class
