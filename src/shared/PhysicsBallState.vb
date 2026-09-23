Imports System
Imports System.Collections
Imports System.Collections.Generic
Imports System.Drawing

#If PHYSICS_PREVIEW Then
Namespace PhysicsPreview
#End If
Public Partial Class B2SData
    Public Class PhysicsSwitchZone
        Public Bounds As RectangleF
        Public SwitchID As Integer
        Public Angle As Single
    End Class
    Public Class PhysicsLauncher
        Public TriggerType As Integer
        Public TriggerID As Integer
        Public Origin As PointF
        Public Angle As Single
        Public Strength As Single
        Public RandomAngle As Single
        Public RandomStrength As Single
        Public CaptureRadius As Single
        Public FollowPivot As Boolean
    End Class

    Private Class PhysicsBallState
        Implements IDisposable

        Private Const PhysicsSubstepSeconds As Single = 0.001F
        Private Const ShallowSurfaceSlopeLimit As Single = 0.35F
        Private Const ShallowSurfaceRollMultiplier As Single = 2.0F
        Private Const BoundaryScatterDegrees As Single = 10.0F
        Private Const BoundaryScatterMinimumImpactSpeed As Single = 150.0F
        Private ReadOnly ball As B2SPictureBox
        Private ReadOnly flipperName As String
        Private ReadOnly bounds As RectangleF
        Private ReadOnly gravity As Single
        Private ReadOnly flipperStrength As Single
        Private ReadOnly boundaryBounce As Single
        Private ReadOnly boundaryPaths As New Generic.List(Of Generic.List(Of PointF))()
        Private ReadOnly boundarySegmentBounces As New Generic.List(Of Generic.List(Of Single))()
        Private ReadOnly obstacles As New Generic.List(Of RectangleF)()
        Private ReadOnly obstacleBounces As New Generic.List(Of Single)()
        Private ReadOnly switchZones As New Generic.List(Of PhysicsSwitchZone)()
        Private ReadOnly switchZoneInside As New Generic.List(Of Boolean)()
        Private ReadOnly launcher As PhysicsLauncher
        Private Shared ReadOnly launcherRandom As New Random()
        Private Shared boundaryScatterRandom As New Random()
        Private launcherArmed As Boolean = True
        Private launcherHolding As Boolean
        Private launcherExitedCapture As Boolean
        Private ReadOnly authoredBallWidth As Single
        Private ReadOnly authoredBallHeight As Single
        Private ReadOnly authoredBallCenter As PointF
        Private ReadOnly timer As New Windows.Forms.Timer() With {.Interval = 16}
        Private ReadOnly clock As New Diagnostics.Stopwatch()
        Private pendingPhysicsSeconds As Double
        Private Const MaxPhysicsStepsPerTick As Integer = 250
        Private velocity As PointF = PointF.Empty
        Private lastFlipperAngle As Single
        Private hasLastFlipperAngle As Boolean
        Public Sub New(ByVal pictureBox As B2SPictureBox, ByVal pivotName As String, ByVal playBounds As RectangleF,
                       ByVal gravityValue As Single, ByVal strengthValue As Single, ByVal bounceValue As Single,
                        ByVal returnBoundaries As Generic.List(Of Generic.List(Of PointF)),
                        ByVal returnObstacles As Generic.List(Of RectangleF),
                        ByVal returnSwitchZones As Generic.List(Of PhysicsSwitchZone),
                        ByVal returnLauncher As PhysicsLauncher,
                        ByVal returnBoundarySegmentBounces As Generic.List(Of Generic.List(Of Single)),
                        Optional ByVal returnObstacleBounces As Generic.List(Of Single) = Nothing)
            ball = pictureBox
            flipperName = pivotName
            bounds = playBounds
            gravity = gravityValue
            flipperStrength = strengthValue
            boundaryBounce = bounceValue
            authoredBallWidth = If(pictureBox.Width > 0, pictureBox.Width, pictureBox.RectangleF.Width)
            authoredBallHeight = If(pictureBox.Height > 0, pictureBox.Height, pictureBox.RectangleF.Height)
            ' RectangleF is initialized later by screen scaling. The control
            ' bounds already contain the ball's saved editor placement here.
            authoredBallCenter = New PointF(pictureBox.Left + pictureBox.Width / 2.0F,
                                            pictureBox.Top + pictureBox.Height / 2.0F)
            If returnBoundaries IsNot Nothing Then
                For pathIndex As Integer = 0 To returnBoundaries.Count - 1
                    Dim path As Generic.List(Of PointF) = returnBoundaries(pathIndex)
                    If path IsNot Nothing AndAlso path.Count >= 2 Then
                        boundaryPaths.Add(New Generic.List(Of PointF)(path))
                        Dim values As New Generic.List(Of Single)()
                        Dim saved As Generic.List(Of Single) = If(returnBoundarySegmentBounces IsNot Nothing AndAlso
                                                                  pathIndex < returnBoundarySegmentBounces.Count,
                                                                  returnBoundarySegmentBounces(pathIndex), Nothing)
                        For segmentIndex As Integer = 0 To path.Count - 2
                            Dim value As Single = If(saved IsNot Nothing AndAlso segmentIndex < saved.Count, saved(segmentIndex), -1.0F)
                            values.Add(Math.Max(-1.0F, Math.Min(3.0F, value)))
                        Next
                        boundarySegmentBounces.Add(values)
                    End If
                Next
            End If
            If returnObstacles IsNot Nothing Then
                For index As Integer = 0 To returnObstacles.Count - 1
                    Dim obstacle As RectangleF = returnObstacles(index)
                    If obstacle.Width > 0.0F AndAlso obstacle.Height > 0.0F Then
                        obstacles.Add(obstacle)
                        Dim value As Single = If(returnObstacleBounces IsNot Nothing AndAlso index < returnObstacleBounces.Count, returnObstacleBounces(index), -1.0F)
                        obstacleBounces.Add(If(Single.IsNaN(value) OrElse Single.IsInfinity(value) OrElse value < 0.0F, boundaryBounce, Math.Min(3.0F, value)))
                    End If
                Next
            End If
            If returnSwitchZones IsNot Nothing Then
                For Each zone As PhysicsSwitchZone In returnSwitchZones
                    If zone IsNot Nothing AndAlso zone.Bounds.Width > 0.0F AndAlso zone.Bounds.Height > 0.0F AndAlso zone.SwitchID > 0 Then
                        switchZones.Add(New PhysicsSwitchZone With {.Bounds = zone.Bounds, .SwitchID = zone.SwitchID, .Angle = NormalizeSwitchAngle(zone.Angle)})
                        switchZoneInside.Add(False)
                    End If
                Next
            End If
            If returnLauncher IsNot Nothing Then
                launcher = New PhysicsLauncher With {.TriggerType = returnLauncher.TriggerType, .TriggerID = returnLauncher.TriggerID,
                    .Origin = returnLauncher.Origin, .Angle = returnLauncher.Angle, .Strength = returnLauncher.Strength,
                    .RandomAngle = returnLauncher.RandomAngle, .RandomStrength = returnLauncher.RandomStrength,
                    .CaptureRadius = Math.Max(5.0F, returnLauncher.CaptureRadius), .FollowPivot = returnLauncher.FollowPivot}
                launcherHolding = True
            End If
            AddHandler timer.Tick, AddressOf Tick
        End Sub

        Public Sub Launch()
            If launcher Is Nothing OrElse Not launcherArmed Then Return
            Dim scaleX As Single = If(authoredBallWidth <= 0.0F, 1.0F, ball.RectangleF.Width / authoredBallWidth)
            Dim scaleY As Single = If(authoredBallHeight <= 0.0F, 1.0F, ball.RectangleF.Height / authoredBallHeight)
            Dim randomAngleOffset As Single = CSng((launcherRandom.NextDouble() * 2.0R - 1.0R) * launcher.RandomAngle)
            Dim randomStrengthFactor As Single = 1.0F + CSng((launcherRandom.NextDouble() * 2.0R - 1.0R) * launcher.RandomStrength / 100.0R)
            Dim origin As PointF = PointF.Empty, angle As Single = 0.0F
            GetLauncherPose(scaleX, scaleY, origin, angle)
            Dim radians As Double = (angle + randomAngleOffset) * Math.PI / 180.0R
            Dim launchStrength As Single = Math.Max(0.0F, launcher.Strength * randomStrengthFactor)
            ball.SetMotionPathPosition(origin)
            velocity = New PointF(CSng(Math.Cos(radians)) * launchStrength * scaleX, CSng(Math.Sin(radians)) * launchStrength * scaleY)
            launcherArmed = False
            launcherHolding = False
            launcherExitedCapture = False
            For index As Integer = 0 To switchZoneInside.Count - 1
                switchZoneInside(index) = False
            Next
        End Sub

        Public Sub Start()
            ball.Visible = True
            pendingPhysicsSeconds = 0.0R
            clock.Restart()
            timer.Start()
        End Sub

        Private Sub Tick(ByVal sender As Object, ByVal e As EventArgs)
            Dim elapsed As Double = clock.Elapsed.TotalSeconds
            clock.Restart()
            If ball.RectangleF.Width <= 0.0F OrElse ball.RectangleF.Height <= 0.0F Then Return
            AdvancePhysics(elapsed)
        End Sub

        Private Sub AdvancePhysics(ByVal elapsed As Double)
            If elapsed <= 0.0R OrElse Double.IsNaN(elapsed) OrElse Double.IsInfinity(elapsed) Then Return
            pendingPhysicsSeconds += elapsed
            Dim flipper As B2SPictureBox = FindPivotPicture(flipperName)
            Dim angularVelocity As Single = 0.0F
            If flipper IsNot Nothing Then
                If hasLastFlipperAngle Then angularVelocity = CSng((flipper.RotationAngle - lastFlipperAngle) * Math.PI / 180.0F / elapsed)
                lastFlipperAngle = flipper.RotationAngle
                hasLastFlipperAngle = True
            End If
            ' Use the same integration step on fast and slow PCs. Retain elapsed
            ' time rather than dropping everything beyond 40 ms. Cap work per
            ' callback so a delayed window cannot monopolize the message loop.
            Dim steps As Integer = CInt(Math.Min(MaxPhysicsStepsPerTick,
                                                Math.Floor((pendingPhysicsSeconds + 0.000000001R) / 0.001R)))
            For index As Integer = 1 To steps
                StepPhysics(PhysicsSubstepSeconds, flipper, angularVelocity)
            Next
            pendingPhysicsSeconds = Math.Max(0.0R, pendingPhysicsSeconds - steps * 0.001R)
        End Sub

#If PHYSICS_PREVIEW Then
        Public Sub AdvancePreview(ByVal elapsed As Double)
            AdvancePhysics(elapsed)
        End Sub

        Public ReadOnly Property PreviewArmed As Boolean
            Get
                Return launcher Is Nothing OrElse launcherArmed
            End Get
        End Property

        Public Sub KeepPreviewMotion(ByVal previous As PhysicsBallState)
            velocity = previous.velocity
            pendingPhysicsSeconds = previous.pendingPhysicsSeconds
            If (launcher Is Nothing) = (previous.launcher Is Nothing) Then
                launcherArmed = previous.launcherArmed
                launcherHolding = previous.launcherHolding
                launcherExitedCapture = previous.launcherExitedCapture
            End If
            lastFlipperAngle = previous.lastFlipperAngle
            hasLastFlipperAngle = previous.hasLastFlipperAngle
            For index As Integer = 0 To Math.Min(switchZoneInside.Count, previous.switchZoneInside.Count) - 1
                If switchZones(index).SwitchID = previous.switchZones(index).SwitchID AndAlso
                   switchZones(index).Bounds = previous.switchZones(index).Bounds AndAlso
                   switchZones(index).Angle = previous.switchZones(index).Angle Then
                    switchZoneInside(index) = previous.switchZoneInside(index)
                End If
            Next
        End Sub
#End If

        Private Sub StepPhysics(ByVal elapsed As Single, ByVal flipper As B2SPictureBox, ByVal angularVelocity As Single)
            Dim center As PointF = ball.MotionPathCenter
            Dim scaleX As Single = If(authoredBallWidth <= 0.0F, 1.0F, ball.RectangleF.Width / authoredBallWidth)
            Dim scaleY As Single = If(authoredBallHeight <= 0.0F, 1.0F, ball.RectangleF.Height / authoredBallHeight)
            If launcherHolding AndAlso launcher IsNot Nothing Then
                Dim origin As PointF = PointF.Empty, angle As Single = 0.0F
                GetLauncherPose(scaleX, scaleY, origin, angle)
                ball.SetMotionPathPosition(origin)
                velocity = PointF.Empty
                Return
            End If
            Dim runtimeBounds As RectangleF = RectangleF.FromLTRB(bounds.Left * scaleX, bounds.Top * scaleY, bounds.Right * scaleX, bounds.Bottom * scaleY)
            Dim previousCenter As PointF = center
            velocity.Y += gravity * scaleY * elapsed
            center.X += velocity.X * elapsed
            center.Y += velocity.Y * elapsed
            Dim radius As Single = Math.Max(2.0F, Math.Min(ball.RectangleF.Width, ball.RectangleF.Height) * 0.43F)
            If center.X - radius < runtimeBounds.Left Then center.X = runtimeBounds.Left + radius : velocity.X = Math.Abs(velocity.X) * 0.55F
            If center.X + radius > runtimeBounds.Right Then center.X = runtimeBounds.Right - radius : velocity.X = -Math.Abs(velocity.X) * 0.55F
            If center.Y - radius < runtimeBounds.Top Then center.Y = runtimeBounds.Top + radius : velocity.Y = Math.Abs(velocity.Y) * 0.55F
            If center.Y + radius > runtimeBounds.Bottom Then center.Y = runtimeBounds.Bottom - radius : velocity.Y = -Math.Abs(velocity.Y) * 0.55F
            ' An attached launcher uses a generic rotating snippet, not a flipper
            ' collision surface. Legacy physics balls keep their original contact.
            If flipper IsNot Nothing AndAlso (launcher Is Nothing OrElse Not launcher.FollowPivot) Then
                ResolveFlipperCollision(center, radius, velocity, flipper, angularVelocity)
            End If
            For pathIndex As Integer = 0 To boundaryPaths.Count - 1
                Dim path As Generic.List(Of PointF) = boundaryPaths(pathIndex)
                For segmentIndex As Integer = 0 To path.Count - 2
                    Dim fromPoint As New PointF(path(segmentIndex).X * scaleX, path(segmentIndex).Y * scaleY)
                    Dim toPoint As New PointF(path(segmentIndex + 1).X * scaleX, path(segmentIndex + 1).Y * scaleY)
                    Dim segmentBounce As Single = boundaryBounce
                    If pathIndex < boundarySegmentBounces.Count AndAlso segmentIndex < boundarySegmentBounces(pathIndex).Count AndAlso
                       boundarySegmentBounces(pathIndex)(segmentIndex) >= 0.0F Then
                        segmentBounce = boundarySegmentBounces(pathIndex)(segmentIndex)
                    End If
                    Dim beforeCollisionCenter As PointF = center
                    Dim beforeCollisionVelocity As PointF = velocity
                    ResolveStaticFloorCollision(center, previousCenter, radius, velocity, fromPoint, toPoint, segmentBounce)
                    Dim collisionResolved As Boolean =
                        Math.Abs(center.X - beforeCollisionCenter.X) > 0.0001F OrElse
                        Math.Abs(center.Y - beforeCollisionCenter.Y) > 0.0001F OrElse
                        Math.Abs(velocity.X - beforeCollisionVelocity.X) > 0.0001F OrElse
                        Math.Abs(velocity.Y - beforeCollisionVelocity.Y) > 0.0001F
                    If collisionResolved Then
                        ApplyShallowSurfaceRollingBoost(velocity, fromPoint, toPoint, elapsed, gravity * scaleY)
                    End If
                Next
            Next
            For index As Integer = 0 To obstacles.Count - 1
                ResolveCircularObstacleCollision(center, radius, velocity, obstacles(index), scaleX, scaleY, obstacleBounces(index))
            Next
            CheckLauncherCapture(center, scaleX, scaleY)
            CheckSwitchZones(previousCenter, center, scaleX, scaleY)
            ball.SetMotionPathPosition(center, True)
        End Sub

        Private Shared Sub ApplyShallowSurfaceRollingBoost(ByRef ballVelocity As PointF,
                                                            ByVal fromPoint As PointF,
                                                            ByVal toPoint As PointF,
                                                            ByVal elapsed As Single,
                                                            ByVal gravityAcceleration As Single)
            Dim segmentX As Single = toPoint.X - fromPoint.X
            Dim segmentY As Single = toPoint.Y - fromPoint.Y
            Dim length As Single = CSng(Math.Sqrt(segmentX * segmentX + segmentY * segmentY))
            If length <= 0.01F Then Return

            Dim tangentX As Single = segmentX / length
            Dim tangentY As Single = segmentY / length
            Dim absoluteSlope As Single = Math.Abs(tangentY)
            If absoluteSlope <= 0.001F OrElse absoluteSlope > ShallowSurfaceSlopeLimit Then Return

            Dim extraProjectedGravity As Single =
                gravityAcceleration * tangentY * (ShallowSurfaceRollMultiplier - 1.0F)
            ballVelocity.X += tangentX * extraProjectedGravity * elapsed
            ballVelocity.Y += tangentY * extraProjectedGravity * elapsed
        End Sub

        Private Sub CheckLauncherCapture(ByRef center As PointF, ByVal scaleX As Single, ByVal scaleY As Single)
            If launcher Is Nothing OrElse launcherArmed Then Return
            Dim origin As PointF = PointF.Empty, angle As Single = 0.0F
            GetLauncherPose(scaleX, scaleY, origin, angle)
            Dim radius As Single = launcher.CaptureRadius * (scaleX + scaleY) / 2.0F
            Dim dx As Single = center.X - origin.X
            Dim dy As Single = center.Y - origin.Y
            Dim inside As Boolean = dx * dx + dy * dy <= radius * radius
            If Not launcherExitedCapture Then
                launcherExitedCapture = Not inside
            ElseIf inside Then
                center = origin
                velocity = PointF.Empty
                launcherArmed = True
                launcherHolding = True
            End If
        End Sub

        Private Sub GetLauncherPose(ByVal scaleX As Single, ByVal scaleY As Single,
                                    ByRef origin As PointF, ByRef angle As Single)
            origin = New PointF(launcher.Origin.X * scaleX, launcher.Origin.Y * scaleY)
            angle = launcher.Angle
            If Not launcher.FollowPivot Then Return
            Dim pivot As B2SPictureBox = FindPivotPicture(flipperName)
            If pivot Is Nothing Then Return
            If pivot.PivotAutomaticOscillation Then
                ' The main editor ball placement is the firing point for an attached
                ' automatic pivot. Keep the saved launch coordinates for other modes.
                origin = New PointF(authoredBallCenter.X * scaleX, authoredBallCenter.Y * scaleY)
            End If
            Dim hinge As New PointF(pivot.RectangleF.Left + pivot.RectangleF.Width * pivot.RotationPivotX,
                                    pivot.RectangleF.Top + pivot.RectangleF.Height * pivot.RotationPivotY)
            If pivot.PreservePhysicsArtworkAspect AndAlso pivot.Width > 0 AndAlso pivot.Height > 0 Then
                ' Keep the held ball at the point it occupied on the authored
                ' launcher. The background and physics boundaries retain their
                ' independent X/Y screen scaling; only this rigid pair uses one
                ' visual scale around the hinge.
                Dim authoredOrigin As PointF = If(pivot.PivotAutomaticOscillation, authoredBallCenter, launcher.Origin)
                Dim authoredHinge As New PointF(pivot.Left + pivot.Width * pivot.RotationPivotX,
                                                pivot.Top + pivot.Height * pivot.RotationPivotY)
                Dim visualScale As Single = Math.Min(pivot.RectangleF.Width / pivot.Width,
                                                    pivot.RectangleF.Height / pivot.Height)
                origin = New PointF(hinge.X + (authoredOrigin.X - authoredHinge.X) * visualScale,
                                    hinge.Y + (authoredOrigin.Y - authoredHinge.Y) * visualScale)
            End If
            Dim restAngle As Single = If(pivot.PivotAutomaticOscillation, 0.0F, pivot.PivotDownAngle)
            Dim delta As Single = pivot.RotationAngle - restAngle
            Dim radians As Double = delta * Math.PI / 180.0R
            Dim dx As Single = origin.X - hinge.X, dy As Single = origin.Y - hinge.Y
            If pivot.PivotAutomaticOscillation AndAlso pivot.Width > 0 AndAlso pivot.Height > 0 Then
                ' Match the automatic pivot artwork: rotate the editor point in
                ' authored coordinates, then apply the backglass X/Y scales.
                Dim authoredHinge As New PointF(pivot.Left + pivot.Width * pivot.RotationPivotX,
                                                pivot.Top + pivot.Height * pivot.RotationPivotY)
                Dim authoredX As Single = authoredBallCenter.X - authoredHinge.X
                Dim authoredY As Single = authoredBallCenter.Y - authoredHinge.Y
                Dim pivotScaleX As Single = pivot.RectangleF.Width / pivot.Width
                Dim pivotScaleY As Single = pivot.RectangleF.Height / pivot.Height
                origin = New PointF(hinge.X + CSng((authoredX * Math.Cos(radians) - authoredY * Math.Sin(radians)) * pivotScaleX),
                                    hinge.Y + CSng((authoredX * Math.Sin(radians) + authoredY * Math.Cos(radians)) * pivotScaleY))
            Else
                origin = New PointF(hinge.X + CSng(dx * Math.Cos(radians) - dy * Math.Sin(radians)),
                                    hinge.Y + CSng(dx * Math.Sin(radians) + dy * Math.Cos(radians)))
            End If
            angle += delta
        End Sub

        Private Sub CheckSwitchZones(ByVal previousCenter As PointF, ByVal center As PointF,
                                     ByVal scaleX As Single, ByVal scaleY As Single)
            If Math.Abs(scaleX) < 0.000001F OrElse Math.Abs(scaleY) < 0.000001F Then Return
            Dim authoredPrevious As New PointF(previousCenter.X / scaleX, previousCenter.Y / scaleY)
            Dim authoredCurrent As New PointF(center.X / scaleX, center.Y / scaleY)
            For index As Integer = 0 To switchZones.Count - 1
                Dim zone As PhysicsSwitchZone = switchZones(index)
                Dim inside As Boolean = PointInSwitchZone(authoredCurrent, zone)
                Dim crossed As Boolean = Not inside AndAlso SegmentIntersectsSwitchZone(authoredPrevious, authoredCurrent, zone)
                If Not switchZoneInside(index) AndAlso (inside OrElse crossed) Then PulsePhysicsSwitch(zone.SwitchID)
                switchZoneInside(index) = inside
            Next
        End Sub

        Private Shared Function PointInSwitchZone(ByVal point As PointF, ByVal zone As PhysicsSwitchZone) As Boolean
            Dim local As PointF = SwitchZoneLocalPoint(point, zone)
            Return zone.Bounds.Contains(local.X, local.Y)
        End Function

        Private Shared Function SegmentIntersectsSwitchZone(ByVal fromPoint As PointF, ByVal toPoint As PointF,
                                                             ByVal zone As PhysicsSwitchZone) As Boolean
            Dim localFrom As PointF = SwitchZoneLocalPoint(fromPoint, zone)
            Dim localTo As PointF = SwitchZoneLocalPoint(toPoint, zone)
            If zone.Bounds.Contains(localFrom.X, localFrom.Y) OrElse zone.Bounds.Contains(localTo.X, localTo.Y) Then Return True
            Dim deltaX As Double = localTo.X - localFrom.X, deltaY As Double = localTo.Y - localFrom.Y
            Dim first As Double = 0.0R, last As Double = 1.0R
            Return ClipSwitchSegment(-deltaX, localFrom.X - zone.Bounds.Left, first, last) AndAlso
                   ClipSwitchSegment(deltaX, zone.Bounds.Right - localFrom.X, first, last) AndAlso
                   ClipSwitchSegment(-deltaY, localFrom.Y - zone.Bounds.Top, first, last) AndAlso
                   ClipSwitchSegment(deltaY, zone.Bounds.Bottom - localFrom.Y, first, last)
        End Function

        Private Shared Function ClipSwitchSegment(ByVal direction As Double, ByVal distance As Double,
                                                   ByRef first As Double, ByRef last As Double) As Boolean
            If Math.Abs(direction) < 0.0000001R Then Return distance >= 0.0R
            Dim ratio As Double = distance / direction
            If direction < 0.0R Then
                If ratio > last Then Return False
                If ratio > first Then first = ratio
            Else
                If ratio < first Then Return False
                If ratio < last Then last = ratio
            End If
            Return True
        End Function

        Private Shared Function SwitchZoneLocalPoint(ByVal point As PointF, ByVal zone As PhysicsSwitchZone) As PointF
            Dim angle As Single = NormalizeSwitchAngle(zone.Angle)
            If Math.Abs(angle) < 0.001F Then Return point
            Dim centerX As Single = zone.Bounds.Left + zone.Bounds.Width / 2.0F
            Dim centerY As Single = zone.Bounds.Top + zone.Bounds.Height / 2.0F
            Dim radians As Double = -angle * Math.PI / 180.0R
            Dim cosine As Double = Math.Cos(radians), sine As Double = Math.Sin(radians)
            Dim x As Double = point.X - centerX, y As Double = point.Y - centerY
            Return New PointF(CSng(centerX + x * cosine - y * sine),
                              CSng(centerY + x * sine + y * cosine))
        End Function

        Private Shared Function NormalizeSwitchAngle(ByVal angle As Single) As Single
            Dim normalized As Single = angle Mod 360.0F
            If normalized > 180.0F Then normalized -= 360.0F
            If normalized <= -180.0F Then normalized += 360.0F
            Return normalized
        End Function

        Private Sub ResolveCircularObstacleCollision(ByRef center As PointF, ByVal ballRadius As Single,
                                                      ByRef ballVelocity As PointF, ByVal obstacle As RectangleF,
                                                      ByVal scaleX As Single, ByVal scaleY As Single, ByVal bounce As Single)
            Dim obstacleCenterX As Single = (obstacle.X + obstacle.Width / 2.0F) * scaleX
            Dim obstacleCenterY As Single = (obstacle.Y + obstacle.Height / 2.0F) * scaleY
            Dim obstacleRadius As Single = Math.Max(1.0F, (obstacle.Width * scaleX + obstacle.Height * scaleY) / 4.0F)
            Dim dx As Single = center.X - obstacleCenterX
            Dim dy As Single = center.Y - obstacleCenterY
            Dim minimumDistance As Single = ballRadius + obstacleRadius
            Dim distanceSquared As Single = dx * dx + dy * dy
            If distanceSquared >= minimumDistance * minimumDistance Then Return
            Dim distance As Single = CSng(Math.Sqrt(distanceSquared))
            Dim normalX As Single
            Dim normalY As Single
            If distance < 0.001F Then
                normalX = 0.0F
                normalY = -1.0F
                distance = 0.0F
            Else
                normalX = dx / distance
                normalY = dy / distance
            End If
            center.X += normalX * (minimumDistance - distance)
            center.Y += normalY * (minimumDistance - distance)
            Dim normalSpeed As Single = ballVelocity.X * normalX + ballVelocity.Y * normalY
            If normalSpeed < 0.0F Then
                ballVelocity.X -= (1.0F + bounce) * normalSpeed * normalX
                ballVelocity.Y -= (1.0F + bounce) * normalSpeed * normalY
            End If
        End Sub

        Private Sub ResolveStaticFloorCollision(ByRef center As PointF, ByVal previousCenter As PointF,
                                                 ByVal radius As Single, ByRef ballVelocity As PointF,
                                                 ByVal fromPoint As PointF, ByVal toPoint As PointF,
                                                 ByVal bounce As Single)
            Dim segmentX As Single = toPoint.X - fromPoint.X
            Dim segmentY As Single = toPoint.Y - fromPoint.Y
            Dim lengthSquared As Single = segmentX * segmentX + segmentY * segmentY
            If lengthSquared <= 0.01F Then Return
            Dim rawFraction As Single =
                ((center.X - fromPoint.X) * segmentX + (center.Y - fromPoint.Y) * segmentY) / lengthSquared
            Dim fraction As Single = Math.Max(0.0F, Math.Min(1.0F, rawFraction))
            Dim contactX As Single = fromPoint.X + segmentX * fraction
            Dim contactY As Single = fromPoint.Y + segmentY * fraction
            Dim offsetX As Single = center.X - contactX
            Dim offsetY As Single = center.Y - contactY
            Dim distanceSquared As Single = offsetX * offsetX + offsetY * offsetY
            If distanceSquared >= radius * radius Then
                ' A fast ball can cross the entire boundary between timer steps and
                ' finish beyond it without overlapping. Sweep the ball's full path
                ' against the segment capsule before treating this as no collision.
                Dim sweptCenter As PointF
                Dim sweptNormal As PointF
                If Not TryGetSweptStaticFloorCollision(previousCenter, center, radius,
                                                       fromPoint, toPoint,
                                                       sweptCenter, sweptNormal) Then Return

                center.X = sweptCenter.X + sweptNormal.X * 0.01F
                center.Y = sweptCenter.Y + sweptNormal.Y * 0.01F
                Dim sweptNormalSpeed As Single =
                    ballVelocity.X * sweptNormal.X + ballVelocity.Y * sweptNormal.Y
                If sweptNormalSpeed < 0.0F Then
                    ReflectBoundaryVelocity(ballVelocity, sweptNormal.X, sweptNormal.Y, bounce, sweptNormalSpeed)
                End If
                Return
            End If

            Dim distance As Single = CSng(Math.Sqrt(distanceSquared))
            Dim normalX As Single
            Dim normalY As Single
            If rawFraction < 0.0F OrElse rawFraction > 1.0F Then
                Dim previousOffsetX As Single = previousCenter.X - contactX
                Dim previousOffsetY As Single = previousCenter.Y - contactY
                Dim previousDistance As Single = CSng(Math.Sqrt(previousOffsetX * previousOffsetX + previousOffsetY * previousOffsetY))
                ' Resolve against the ball's current radial direction so a slow
                ' contact can roll around a segment endpoint instead of being
                ' anchored to its previous position until enough speed builds.
                If distance >= 0.001F Then
                    normalX = offsetX / distance
                    normalY = offsetY / distance
                ElseIf previousDistance >= 0.001F Then
                    normalX = previousOffsetX / previousDistance
                    normalY = previousOffsetY / previousDistance
                Else
                    Return
                End If
            Else
                Dim segmentLength As Single = CSng(Math.Sqrt(lengthSquared))
                normalX = segmentY / segmentLength
                normalY = -segmentX / segmentLength
                Dim previousSide As Single =
                    (previousCenter.X - contactX) * normalX + (previousCenter.Y - contactY) * normalY
                If Math.Abs(previousSide) < 0.001F Then
                    previousSide = offsetX * normalX + offsetY * normalY
                End If
                If Math.Abs(previousSide) < 0.001F Then
                    If ballVelocity.X * normalX + ballVelocity.Y * normalY > 0.0F Then
                        normalX = -normalX
                        normalY = -normalY
                    End If
                ElseIf previousSide < 0.0F Then
                    normalX = -normalX
                    normalY = -normalY
                End If
            End If
            center.X = contactX + normalX * radius
            center.Y = contactY + normalY * radius
            Dim normalSpeed As Single = ballVelocity.X * normalX + ballVelocity.Y * normalY
            If normalSpeed < 0.0F Then
                ReflectBoundaryVelocity(ballVelocity, normalX, normalY, bounce, normalSpeed)
            End If
        End Sub

        Private Shared Sub ReflectBoundaryVelocity(ByRef ballVelocity As PointF,
                                                    ByVal normalX As Single,
                                                    ByVal normalY As Single,
                                                    ByVal bounce As Single,
                                                    ByVal incomingNormalSpeed As Single)
            ballVelocity.X -= (1.0F + bounce) * incomingNormalSpeed * normalX
            ballVelocity.Y -= (1.0F + bounce) * incomingNormalSpeed * normalY
            If -incomingNormalSpeed < BoundaryScatterMinimumImpactSpeed Then Return

            Dim scatterDegrees As Single
            SyncLock boundaryScatterRandom
                scatterDegrees = CSng((boundaryScatterRandom.NextDouble() * 2.0R - 1.0R) * BoundaryScatterDegrees)
            End SyncLock
            RotateBoundaryVelocity(ballVelocity, normalX, normalY, scatterDegrees)
        End Sub

        Private Shared Sub RotateBoundaryVelocity(ByRef ballVelocity As PointF,
                                                   ByVal normalX As Single,
                                                   ByVal normalY As Single,
                                                   ByVal degrees As Single)
            Dim radians As Double = degrees * Math.PI / 180.0R
            Dim cosine As Single = CSng(Math.Cos(radians))
            Dim sine As Single = CSng(Math.Sin(radians))
            Dim rotatedX As Single = ballVelocity.X * cosine - ballVelocity.Y * sine
            Dim rotatedY As Single = ballVelocity.X * sine + ballVelocity.Y * cosine
            If rotatedX * normalX + rotatedY * normalY <= 0.0F Then Return
            ballVelocity.X = rotatedX
            ballVelocity.Y = rotatedY
        End Sub

        Private Function TryGetSweptStaticFloorCollision(ByVal previousCenter As PointF,
                                                         ByVal currentCenter As PointF,
                                                         ByVal radius As Single,
                                                         ByVal fromPoint As PointF,
                                                         ByVal toPoint As PointF,
                                                         ByRef impactCenter As PointF,
                                                         ByRef impactNormal As PointF) As Boolean
            Dim motionX As Single = currentCenter.X - previousCenter.X
            Dim motionY As Single = currentCenter.Y - previousCenter.Y
            Dim motionLengthSquared As Single = motionX * motionX + motionY * motionY
            If motionLengthSquared <= 0.000001F Then Return False

            Dim segmentX As Single = toPoint.X - fromPoint.X
            Dim segmentY As Single = toPoint.Y - fromPoint.Y
            Dim segmentLengthSquared As Single = segmentX * segmentX + segmentY * segmentY
            If segmentLengthSquared <= 0.01F Then Return False

            Dim segmentLength As Single = CSng(Math.Sqrt(segmentLengthSquared))
            Dim tangentX As Single = segmentX / segmentLength
            Dim tangentY As Single = segmentY / segmentLength
            Dim baseNormalX As Single = tangentY
            Dim baseNormalY As Single = -tangentX
            Dim previousSignedDistance As Single =
                (previousCenter.X - fromPoint.X) * baseNormalX +
                (previousCenter.Y - fromPoint.Y) * baseNormalY
            Dim currentSignedDistance As Single =
                (currentCenter.X - fromPoint.X) * baseNormalX +
                (currentCenter.Y - fromPoint.Y) * baseNormalY
            Dim signedDistanceChange As Single = currentSignedDistance - previousSignedDistance
            Dim bestTime As Single = Single.MaxValue
            Dim found As Boolean = False

            ' A collision with a neighboring corner segment can push the ball
            ' inside this segment after it was already processed for the step.
            ' If the next movement crosses from that embedded position to the
            ' opposite side, restore it to the side it occupied before crossing.
            Dim previousProjection As Single =
                (previousCenter.X - fromPoint.X) * tangentX +
                (previousCenter.Y - fromPoint.Y) * tangentY
            Dim currentProjection As Single =
                (currentCenter.X - fromPoint.X) * tangentX +
                (currentCenter.Y - fromPoint.Y) * tangentY
            If previousProjection >= 0.0F AndAlso previousProjection <= segmentLength AndAlso
               currentProjection >= 0.0F AndAlso currentProjection <= segmentLength AndAlso
               Math.Abs(previousSignedDistance) < radius AndAlso
               previousSignedDistance * currentSignedDistance <= 0.0F AndAlso
               Math.Abs(currentSignedDistance) >= radius Then
                Dim retainedSide As Single
                If Math.Abs(previousSignedDistance) >= 0.000001F Then
                    retainedSide = If(previousSignedDistance > 0.0F, 1.0F, -1.0F)
                Else
                    retainedSide = If(signedDistanceChange < 0.0F, 1.0F, -1.0F)
                End If
                impactCenter = New PointF(fromPoint.X + tangentX * previousProjection + baseNormalX * retainedSide * radius,
                                          fromPoint.Y + tangentY * previousProjection + baseNormalY * retainedSide * radius)
                impactNormal = New PointF(baseNormalX * retainedSide, baseNormalY * retainedSide)
                Return True
            End If

            ' Test the two parallel sides of the segment's radius-expanded capsule.
            For sideIndex As Integer = 0 To 1
                Dim sideSign As Single = If(sideIndex = 0, 1.0F, -1.0F)
                If sideSign * signedDistanceChange < -0.000001F Then
                    Dim hitTime As Single =
                        (sideSign * radius - previousSignedDistance) / signedDistanceChange
                    If hitTime >= 0.0F AndAlso hitTime <= 1.0F AndAlso hitTime < bestTime Then
                        Dim hitX As Single = previousCenter.X + motionX * hitTime
                        Dim hitY As Single = previousCenter.Y + motionY * hitTime
                        Dim segmentProjection As Single =
                            (hitX - fromPoint.X) * tangentX + (hitY - fromPoint.Y) * tangentY
                        If segmentProjection >= 0.0F AndAlso segmentProjection <= segmentLength Then
                            bestTime = hitTime
                            impactCenter = New PointF(hitX, hitY)
                            impactNormal = New PointF(baseNormalX * sideSign, baseNormalY * sideSign)
                            found = True
                        End If
                    End If
                End If
            Next

            ' Test the round caps so high-speed movement cannot skip a boundary end.
            For endpointIndex As Integer = 0 To 1
                Dim endpoint As PointF = If(endpointIndex = 0, fromPoint, toPoint)
                Dim relativeX As Single = previousCenter.X - endpoint.X
                Dim relativeY As Single = previousCenter.Y - endpoint.Y
                Dim circleConstant As Single =
                    relativeX * relativeX + relativeY * relativeY - radius * radius
                If circleConstant >= 0.0F Then
                    Dim circleLinear As Single = 2.0F * (relativeX * motionX + relativeY * motionY)
                    Dim discriminant As Double =
                        CDbl(circleLinear) * CDbl(circleLinear) -
                        4.0R * CDbl(motionLengthSquared) * CDbl(circleConstant)
                    If discriminant >= 0.0R Then
                        Dim hitTime As Single =
                            CSng((-CDbl(circleLinear) - Math.Sqrt(discriminant)) /
                                 (2.0R * CDbl(motionLengthSquared)))
                        If hitTime >= 0.0F AndAlso hitTime <= 1.0F AndAlso hitTime < bestTime Then
                            Dim hitX As Single = previousCenter.X + motionX * hitTime
                            Dim hitY As Single = previousCenter.Y + motionY * hitTime
                            Dim normalX As Single = hitX - endpoint.X
                            Dim normalY As Single = hitY - endpoint.Y
                            Dim normalLength As Single = CSng(Math.Sqrt(normalX * normalX + normalY * normalY))
                            If normalLength >= 0.001F Then
                                bestTime = hitTime
                                impactCenter = New PointF(hitX, hitY)
                                impactNormal = New PointF(normalX / normalLength, normalY / normalLength)
                                found = True
                            End If
                        End If
                    End If
                End If
            Next

            Return found
        End Function

        Private Sub ResolveFlipperCollision(ByRef center As PointF, ByVal ballRadius As Single, ByRef ballVelocity As PointF,
                                            ByVal flipper As B2SPictureBox, ByVal angularVelocity As Single)
            Dim hinge As New PointF(flipper.RectangleF.Left + flipper.RectangleF.Width * flipper.RotationPivotX,
                                    flipper.RectangleF.Top + flipper.RectangleF.Height * flipper.RotationPivotY)
            Dim baseTipX As Single = flipper.RectangleF.Left + flipper.RectangleF.Width * flipper.RotationTipX
            Dim baseTipY As Single = flipper.RectangleF.Top + flipper.RectangleF.Height * flipper.RotationTipY
            Dim radians As Double = flipper.RotationAngle * Math.PI / 180.0R
            Dim sourceX As Double = baseTipX - hinge.X
            Dim sourceY As Double = baseTipY - hinge.Y
            Dim tip As New PointF(CSng(hinge.X + sourceX * Math.Cos(radians) - sourceY * Math.Sin(radians)),
                                  CSng(hinge.Y + sourceX * Math.Sin(radians) + sourceY * Math.Cos(radians)))
            Dim segmentX As Single = tip.X - hinge.X
            Dim segmentY As Single = tip.Y - hinge.Y
            Dim lengthSquared As Single = segmentX * segmentX + segmentY * segmentY
            If lengthSquared <= 0.01F Then Return
            Dim rawFraction As Single = ((center.X - hinge.X) * segmentX + (center.Y - hinge.Y) * segmentY) / lengthSquared
            Dim fraction As Single = Math.Max(0.0F, Math.Min(1.0F, rawFraction))
            Dim contact As New PointF(hinge.X + segmentX * fraction, hinge.Y + segmentY * fraction)
            Dim segmentLength As Single = CSng(Math.Sqrt(lengthSquared))
            Dim normalX As Single = segmentY / segmentLength
            Dim normalY As Single = -segmentX / segmentLength
            If normalY > 0.0F Then normalX = -normalX : normalY = -normalY
            Dim flipperRadius As Single = Math.Max(3.0F, Math.Min(flipper.RectangleF.Width, flipper.RectangleF.Height) * 0.07F)
            Dim minimumDistance As Single = ballRadius + flipperRadius
            If rawFraction < 0.0F OrElse rawFraction > 1.0F Then
                Dim endX As Single = center.X - contact.X
                Dim endY As Single = center.Y - contact.Y
                Dim endDistance As Single = CSng(Math.Sqrt(endX * endX + endY * endY))
                If endDistance >= minimumDistance OrElse endDistance < 0.001F Then Return
                normalX = endX / endDistance : normalY = endY / endDistance
                If normalY > 0.0F Then Return
                center.X += normalX * (minimumDistance - endDistance)
                center.Y += normalY * (minimumDistance - endDistance)
            Else
                Dim signedDistance As Single = (center.X - contact.X) * normalX + (center.Y - contact.Y) * normalY
                If signedDistance >= minimumDistance Then Return
                center.X += normalX * (minimumDistance - signedDistance)
                center.Y += normalY * (minimumDistance - signedDistance)
            End If
            Dim armX As Single = contact.X - hinge.X
            Dim armY As Single = contact.Y - hinge.Y
            Dim surfaceX As Single = -angularVelocity * armY * flipperStrength
            Dim surfaceY As Single = angularVelocity * armX * flipperStrength
            Dim relativeX As Single = ballVelocity.X - surfaceX
            Dim relativeY As Single = ballVelocity.Y - surfaceY
            Dim towardSurface As Single = relativeX * normalX + relativeY * normalY
            If towardSurface < 0.0F Then
                Const restitution As Single = 0.58F
                relativeX -= (1.0F + restitution) * towardSurface * normalX
                relativeY -= (1.0F + restitution) * towardSurface * normalY
                ballVelocity.X = surfaceX + relativeX
                ballVelocity.Y = surfaceY + relativeY
            End If
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            timer.Stop()
            RemoveHandler timer.Tick, AddressOf Tick
            timer.Dispose()
            clock.Stop()
        End Sub
    End Class

End Class
#If PHYSICS_PREVIEW Then
End Namespace
#End If
