Imports System.Runtime.InteropServices
Imports System.Windows.Forms

' The mouse wheel never changes a value in any window. Over a number box, a
' slider or a closed drop-down list the wheel scrolls the window or panel
' around it instead, so scanning a window with the wheel cannot change a
' setting by accident. Values change only by typing, clicking or dragging.
' Ctrl+wheel window zoom (EditorWindowZoom), canvas and preview wheels and
' open drop-down lists are not affected.
Public NotInheritable Class WheelValueGuard
    Implements IMessageFilter

    Private Const WM_MOUSEWHEEL As Integer = &H20A
    Private Const WM_MOUSEHWHEEL As Integer = &H20E
    Private Shared installed As Boolean

    <DllImport("user32.dll")>
    Private Shared Function SendMessage(hWnd As IntPtr, msg As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
    End Function

    Public Shared Sub Install()
        If installed Then Return
        Application.AddMessageFilter(New WheelValueGuard())
        installed = True
    End Sub

    Public Function PreFilterMessage(ByRef message As Message) As Boolean Implements IMessageFilter.PreFilterMessage
        If message.Msg <> WM_MOUSEWHEEL AndAlso message.Msg <> WM_MOUSEHWHEEL Then Return False
        Dim target As Control = ValueControl(Control.FromChildHandle(message.HWnd))
        If target Is Nothing Then Return False
        ' Ctrl+wheel in a zoomable window belongs to EditorWindowZoom.
        If (Control.ModifierKeys And Keys.Control) <> 0 AndAlso EditorWindowZoom.Zooms(target.FindForm()) Then Return False
        Dim scroller As ScrollableControl = ScrollParent(target)
        If scroller IsNot Nothing AndAlso scroller.IsHandleCreated Then SendMessage(scroller.Handle, message.Msg, message.WParam, message.LParam)
        Return True
    End Function

    Private Shared Function ValueControl(control As Control) As Control
        While control IsNot Nothing AndAlso Not TypeOf control Is Form
            If TypeOf control Is UpDownBase OrElse TypeOf control Is TrackBar Then Return control
            Dim combo As ComboBox = TryCast(control, ComboBox)
            If combo IsNot Nothing Then Return If(combo.DroppedDown, Nothing, combo)
            control = control.Parent
        End While
        Return Nothing
    End Function

    Private Shared Function ScrollParent(control As Control) As ScrollableControl
        Dim parent As Control = control.Parent
        While parent IsNot Nothing
            Dim scrollable As ScrollableControl = TryCast(parent, ScrollableControl)
            If scrollable IsNot Nothing AndAlso scrollable.AutoScroll AndAlso
               (scrollable.VerticalScroll.Visible OrElse scrollable.HorizontalScroll.Visible) Then Return scrollable
            parent = parent.Parent
        End While
        Return Nothing
    End Function
End Class
