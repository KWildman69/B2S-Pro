Imports System.Drawing

' The shared server simulation runs against these in-memory preview bodies.
' No COM server, registry, ROM connection or switch bridge is started.
Namespace PhysicsPreview
    Public Class B2SPictureBox
        Public RectangleF As RectangleF
        Public Left As Integer
        Public Top As Integer
        Public Width As Integer
        Public Height As Integer
        Public Visible As Boolean
        Public RotationAngle As Single
        Public RotationPivotX As Single
        Public RotationPivotY As Single
        Public RotationTipX As Single
        Public RotationTipY As Single
        Public PivotDownAngle As Single
        Public PivotAutomaticOscillation As Boolean
        Public PreservePhysicsArtworkAspect As Boolean
        Public RollEnabled As Boolean
        Public RollAngle As Single
        Public ReadOnly Property MotionPathCenter As PointF
            Get
                Return New PointF(RectangleF.X + RectangleF.Width / 2.0F, RectangleF.Y + RectangleF.Height / 2.0F)
            End Get
        End Property
        Public Sub SetMotionPathPosition(ByVal center As PointF, Optional ByVal advanceRoll As Boolean = False)
            If advanceRoll AndAlso RollEnabled Then
                Dim previous As PointF = MotionPathCenter
                Dim dx As Single = center.X - previous.X, dy As Single = center.Y - previous.Y
                Dim distance As Double = Math.Sqrt(dx * dx + dy * dy)
                Dim direction As Single = If(Math.Abs(dx) >= Math.Abs(dy), Math.Sign(dx), Math.Sign(dy))
                Dim radius As Single = Math.Max(1.0F, Math.Min(RectangleF.Width, RectangleF.Height) / 2.0F)
                If distance > 0.0001R AndAlso direction <> 0.0F Then
                    RollAngle = CSng((RollAngle + direction * distance / radius * 180.0R / Math.PI) Mod 360.0R)
                End If
            End If
            RectangleF = New RectangleF(center.X - RectangleF.Width / 2.0F, center.Y - RectangleF.Height / 2.0F,
                                        RectangleF.Width, RectangleF.Height)
        End Sub
    End Class

    Public Partial Class B2SData
        Implements IDisposable
        Private state As PhysicsBallState
        Private Shared activePreview As B2SData
        Public ReadOnly Body As New B2SPictureBox()
        Public Pivot As B2SPictureBox
        Public Event SwitchHit(ByVal switchID As Integer)

        Private Shared Function FindPivotPicture(ByVal name As String) As B2SPictureBox
            Return If(activePreview Is Nothing OrElse String.IsNullOrEmpty(name), Nothing, activePreview.Pivot)
        End Function
        Private Shared Sub PulsePhysicsSwitch(ByVal id As Integer)
            If activePreview IsNot Nothing Then activePreview.ReportSwitch(id)
        End Sub
        Private Sub ReportSwitch(ByVal id As Integer)
            RaiseEvent SwitchHit(id)
        End Sub

        Public Sub Configure(ByVal ballBounds As Rectangle, ByVal playBounds As RectangleF,
                             ByVal gravity As Single, ByVal strength As Single, ByVal bounce As Single,
                             ByVal paths As List(Of List(Of PointF)), ByVal obstacles As List(Of RectangleF),
                             ByVal zones As List(Of PhysicsSwitchZone), ByVal launcher As PhysicsLauncher,
                             ByVal segmentBounces As List(Of List(Of Single)), ByVal reset As Boolean, Optional ByVal obstacleBounces As List(Of Single) = Nothing)
            Body.Left = ballBounds.Left : Body.Top = ballBounds.Top
            Body.Width = ballBounds.Width : Body.Height = ballBounds.Height
            If reset OrElse state Is Nothing Then
                Body.RectangleF = ballBounds
                Body.RollAngle = 0.0F
            End If
            Dim replacement As New PhysicsBallState(Body, If(Pivot Is Nothing, "", "preview"), playBounds,
                                                    gravity, strength, bounce, paths, obstacles, zones, launcher, segmentBounces, obstacleBounces)
            If state IsNot Nothing Then
                If Not reset Then replacement.KeepPreviewMotion(state)
                state.Dispose()
            End If
            state = replacement
        End Sub
        Public ReadOnly Property CanFire As Boolean
            Get
                Return state IsNot Nothing AndAlso state.PreviewArmed
            End Get
        End Property
        Public Sub Advance(ByVal elapsed As Double)
            Dim previous As B2SData = activePreview
            Try
                activePreview = Me
                If state IsNot Nothing Then state.AdvancePreview(elapsed)
            Finally
                activePreview = previous
            End Try
        End Sub
        Public Sub Fire()
            Dim previous As B2SData = activePreview
            Try
                activePreview = Me
                If state IsNot Nothing Then state.Launch()
            Finally
                activePreview = previous
            End Try
        End Sub
        Public Sub Dispose() Implements IDisposable.Dispose
            If state IsNot Nothing Then state.Dispose() : state = Nothing
        End Sub
    End Class
End Namespace
