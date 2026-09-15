Namespace My
    Partial Friend Class MyApplication
        Protected Overrides Function OnInitialize(commandLineArgs As System.Collections.ObjectModel.ReadOnlyCollection(Of String)) As Boolean
            Me.MinimumSplashScreenDisplayTime = If(IDTesterRestore.IsMonitorCommand(commandLineArgs), 0, 3000)
            Return MyBase.OnInitialize(commandLineArgs)
        End Function

        Protected Overrides Sub OnCreateSplashScreen()
            Dim arguments As String() = Environment.GetCommandLineArgs()
            If arguments.Length = 3 AndAlso String.Equals(arguments(1), "--id-tester-monitor", StringComparison.Ordinal) Then Return
            Me.SplashScreen = New Global.B2SBackglassDesigner.formSplash()
        End Sub

        Private Sub MyApplication_Startup(sender As Object, e As Microsoft.VisualBasic.ApplicationServices.StartupEventArgs) Handles Me.Startup
            If IDTesterRestore.IsMonitorCommand(e.CommandLine) Then
                e.Cancel = True
                IDTesterRestore.MonitorFromChild(e.CommandLine(1))
                Return
            End If
            Try
                IDTesterRestore.RecoverPendingTests()
            Catch ex As Exception
                MessageBox.Show("An earlier ID test still needs its backglass restored: " & ex.Message,
                                "ID Tester restore", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End Try
        End Sub
    End Class
End Namespace
