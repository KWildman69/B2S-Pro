Namespace My
    Partial Friend Class MyApplication
        Protected Overrides Function OnInitialize(commandLineArgs As System.Collections.ObjectModel.ReadOnlyCollection(Of String)) As Boolean
            Me.MinimumSplashScreenDisplayTime = 3000
            Return MyBase.OnInitialize(commandLineArgs)
        End Function

        Protected Overrides Sub OnCreateSplashScreen()
            Me.SplashScreen = New Global.B2SBackglassDesigner.formSplash()
        End Sub
    End Class
End Namespace
