Namespace ReelAndLED

    Public Class ScoreInfo

        Inherits InfoBase

        Public ReelType As String = String.Empty
        Public ReelColor As Color = Nothing
        Public Digits As Integer = 0
        Public Spacing As Integer = 0
        Public DisplayState As eScoreDisplayState = eScoreDisplayState.Visible

        ' Clockwise rotation in degrees around the center of the score window.
        Public RotationAngle As Single = 0.0F

        ' Horizontal depth turn. -1.0 turns left, 0 is straight on, +1.0 turns right.
        Public PerspectiveDepth As Single = 0.0F

        ' Independent vertical perspective at the left and right ends.
        ' 1.0 keeps the original height; smaller/larger values compress/stretch it.
        Public PerspectiveLeftScale As Single = 1.0F
        Public PerspectiveRightScale As Single = 1.0F

        ' Shared visual layer used by reels/LEDs, snippets and lights.
        ' Higher values are painted in front of lower values.
        Public ZOrder As Integer = 1000000
        Public BehindCanvas As Boolean = False
        Public B2SStartDigit As Integer = 0
        Public B2SScoreType As eB2SScoreType = eB2SScoreType.NotUsed
        Public B2SPlayerNo As eB2SPlayerNo = eB2SPlayerNo.NotUsed

        Public ReelIlluLocation As eReelIlluminationLocation = eReelIlluminationLocation.Off
        Public ReelIlluB2SID As Integer = 0
        Public ReelIlluB2SIDType As eB2SIDType = eB2SIDType.NotUsed
        Public ReelIlluB2SValue As Integer = 0
        Public ReelIlluIntensity As Integer = 1

        Public SingleReelSize As SizeF = Nothing
        Public IsSingleReelSizeDirty As Boolean = True
        Public SingleReelFactor As Double = 1

        Public PerfectScaleWidthFix As Boolean = False

        ' property for internal use

        Friend numbered As Boolean = False

    End Class

End Namespace
