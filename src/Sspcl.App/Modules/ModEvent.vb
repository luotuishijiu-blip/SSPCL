#Region "附加属性"

''' <summary>
''' 用于在 XAML 中初始化列表对象。
''' </summary>
<Markup.ContentProperty("Events")>
Public Class CustomEventCollection
    Implements IEnumerable(Of CustomEvent)
    Dim _Events As New List(Of CustomEvent)
    Public ReadOnly Property Events As List(Of CustomEvent)
        Get
            Return _Events
        End Get
    End Property
    Public Function GetEnumerator() As IEnumerator(Of CustomEvent) Implements IEnumerable(Of CustomEvent).GetEnumerator
        Return DirectCast(Events, IEnumerable(Of CustomEvent)).GetEnumerator()
    End Function
    Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
        Return DirectCast(Events, IEnumerable).GetEnumerator()
    End Function
End Class

''' <summary>
''' 提供自定义事件的附加属性。
''' </summary>
Public Class CustomEventService

    'Events
    Public Shared ReadOnly EventsProperty As DependencyProperty =
            DependencyProperty.RegisterAttached("Events", GetType(CustomEventCollection), GetType(CustomEventService), New PropertyMetadata(Nothing))
    <AttachedPropertyBrowsableForType(GetType(DependencyObject))>
    Public Shared Sub SetEvents(d As DependencyObject, value As CustomEventCollection)
        d.SetValue(EventsProperty, value)
    End Sub
    <AttachedPropertyBrowsableForType(GetType(DependencyObject))>
    Public Shared Function GetEvents(d As DependencyObject) As CustomEventCollection
        If d.GetValue(EventsProperty) Is Nothing Then d.SetValue(EventsProperty, New CustomEventCollection)
        Return d.GetValue(EventsProperty)
    End Function

    'EventType
    Public Shared ReadOnly EventTypeProperty As DependencyProperty =
            DependencyProperty.RegisterAttached("EventType", GetType(CustomEvent.EventType), GetType(CustomEventService), New PropertyMetadata(Nothing))
    <AttachedPropertyBrowsableForType(GetType(DependencyObject))>
    Public Shared Sub SetEventType(d As DependencyObject, value As CustomEvent.EventType)
        d.SetValue(EventTypeProperty, value)
    End Sub
    <AttachedPropertyBrowsableForType(GetType(DependencyObject))>
    Public Shared Function GetEventType(d As DependencyObject) As CustomEvent.EventType
        Return d.GetValue(EventTypeProperty)
    End Function

    'EventData
    Public Shared ReadOnly EventDataProperty As DependencyProperty =
            DependencyProperty.RegisterAttached("EventData", GetType(String), GetType(CustomEventService), New PropertyMetadata(Nothing))
    <AttachedPropertyBrowsableForType(GetType(DependencyObject))>
    Public Shared Sub SetEventData(d As DependencyObject, value As String)
        d.SetValue(EventDataProperty, value)
    End Sub
    <AttachedPropertyBrowsableForType(GetType(DependencyObject))>
    Public Shared Function GetEventData(d As DependencyObject) As String
        Return d.GetValue(EventDataProperty)
    End Function

End Class

Partial Public Module ModMain

    ''' <summary>
    ''' 触发该控件上的自定义事件。
    ''' </summary>
    <Runtime.CompilerServices.Extension>
    Public Sub RaiseCustomEvent(Control As DependencyObject)
        Dim Events = CustomEventService.GetEvents(Control).ToList
        Dim EventType = CustomEventService.GetEventType(Control)
        If EventType <> CustomEvent.EventType.None Then Events.Add(New CustomEvent(EventType, CustomEventService.GetEventData(Control)))
        If Not Events.Any Then Return
        RunInNewThread(
        Sub()
            For Each e In Events
                e.Raise()
            Next
        End Sub, "执行自定义事件 " & GetUuid())
    End Sub

End Module

#End Region

''' <summary>
''' 自定义事件。
''' </summary>
Public Class CustomEvent
    Inherits DependencyObject

    Public Property Type As EventType
        Get
            Dim Value As EventType = EventType.None
            RunInUiWait(Sub() Value = GetValue(TypeProperty))
            Return Value
        End Get
        Set(value As EventType)
            SetValue(TypeProperty, value)
        End Set
    End Property
    Public Shared ReadOnly TypeProperty As DependencyProperty =
        DependencyProperty.Register("Type", GetType(EventType), GetType(CustomEvent), New PropertyMetadata(EventType.None))

    Public Property Data As String
        Get
            Dim Value As String = Nothing
            RunInUiWait(Sub() Value = GetValue(DataProperty))
            Return Value
        End Get
        Set(value As String)
            SetValue(DataProperty, value)
        End Set
    End Property
    Public Shared ReadOnly DataProperty As DependencyProperty =
        DependencyProperty.Register("Data", GetType(String), GetType(CustomEvent), New PropertyMetadata(Nothing))

    Public Sub New()
    End Sub
    Public Sub New(Type As EventType, Data As String)
        Me.Type = Type
        Me.Data = Data
    End Sub

    Public Sub Raise()
        Raise(Type, Data)
    End Sub

    Public Enum EventType
        None = 0
        打开网页
        打开文件
        打开帮助
        执行命令
        启动游戏
        复制文本
        刷新主页
        刷新页面
        刷新帮助
        今日人品
        内存优化
        清理垃圾
        弹出窗口
        弹出提示
        切换页面
        导入整合包
        安装整合包
        下载文件
        修改设置
        写入设置
        修改变量
        写入变量
        加入房间
        检查更新
    End Enum

    ''' <summary>
    ''' 触发一个自定义事件。远行星号版只保留通用事件。
    ''' </summary>
    Public Shared Sub Raise(Type As EventType, Arg As String)
        If Type = EventType.None Then Return
        Logger.Info($"执行自定义事件：{Type}, {Arg}")
        If Arg Is Nothing Then Arg = ""
        Try
            Select Case Type
                Case EventType.弹出窗口
                    Dim Args As String() = Arg.Split("|")
                    If Args.Length >= 2 Then
                        MyMsgBox(Args(1).Replace("\n", vbCrLf), Args(0).Replace("\n", vbCrLf), "确定")
                    End If
                Case EventType.弹出提示
                    Hint(Arg.Replace("\n", vbCrLf))
                Case Else
                    ' 远行星号版已移除其余事件
            End Select
        Catch ex As Exception
            Logger.Error(ex, $"事件执行失败（{Type}, {Arg}）")
        End Try
    End Sub

    ''' <summary>
    ''' 返回 {绝对 Url, WorkingDir}。远行星号版仅作路径透传。
    ''' </summary>
    Public Shared Function GetAbsoluteUrls(RelativeUrl As String, Type As EventType) As String()
        If RelativeUrl Is Nothing Then RelativeUrl = ""
        Return {RelativeUrl, Paths.Base & "Sspcl"}
    End Function

End Class
