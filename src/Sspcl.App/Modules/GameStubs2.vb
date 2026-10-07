' =============================================================
' 远行星号版：Minecraft 页面/类型/函数的空壳占位。
' 原 Sspcl 的 MC 功能已全部移除，这里只保留让残留引用能够编译的空壳，
' 所有方法均为空操作，不含任何 Minecraft 功能。
' =============================================================

#Region "MC 页面占位类"

Public Class MySkinStub
    Public Sub Start()
    End Sub
End Class

Public Class PageLaunchLeft : Inherits MyPageLeft
    Public Shared SkinLegacy As MySkinStub = New MySkinStub()
    Public Sub LaunchButtonClick()
    End Sub
    Public Sub RefreshPage(a As Boolean, b As Boolean)
    End Sub
    Public Sub RefreshButtonsUI()
    End Sub
    Public Sub PageChangeToLogin()
    End Sub
    Public Sub PageChangeToLaunching()
    End Sub
    Public Sub LaunchingRefresh()
    End Sub
    Public BtnLaunch As Object
    Public BtnVersion As Object
    Public BtnMore As Object
    Public LabVersion As Object
    Public AprilScaleTrans As Object
    Public AprilPosTrans As Object
End Class
Public Class PageLaunchRight : Inherits MyPageRight
    Public Sub ForceRefresh()
    End Sub
    Public LabLog As Object
End Class
Public Class PageDownloadLeft : Inherits MyPageLeft
    Public Property PageID As Integer = 0
    Public PanItem As Panel
    Public Function PageGet(subType As Object) As MyPageRight
        Return Nothing
    End Function
End Class
Public Class PageDownloadInstall : Inherits MyPageRight
    Public IsInSelectPage As Boolean
    Public Sub ExitSelectPage()
    End Sub
End Class
Public Class PageDownloadResourceDetail : Inherits MyPageRight
End Class
Public Class PageLoginAuth : Inherits MyPageRight
    Public ComboName As System.Windows.Controls.ComboBox
    Public TextPass As System.Windows.Controls.PasswordBox
End Class
Public Class PageLoginLegacy : Inherits MyPageRight
    Public ComboName As System.Windows.Controls.ComboBox
End Class
Public Class PageLoginMs : Inherits MyPageRight
    Public ComboAccounts As System.Windows.Controls.ComboBox
End Class
Public Class PageLoginNide : Inherits MyPageRight
    Public ComboName As System.Windows.Controls.ComboBox
    Public TextPass As System.Windows.Controls.PasswordBox
End Class
Public Class PageInstanceLeft : Inherits MyPageLeft
    Public Shared Property Instance As GameInstance
    Public Property PageID As Integer = 0
    Public Function PageGet(subType As Object) As MyPageRight
        Return Nothing
    End Function
    Public PanItem As Panel
    Public Shared Sub ReloadCurrentJava()
    End Sub
End Class
Public Class PageInstanceMod : Inherits MyPageRight
    Public Sub ReloadModList()
    End Sub
    Public Shared Function InstallMods(a As Object) As Boolean
        Return False
    End Function
End Class
Public Class PageInstanceSetup : Inherits MyPageRight
    Public Shared Sub OnVersionRamTypeChanged()
    End Sub
    Public Shared Sub OnVersionServerLoginChanged()
    End Sub
    Public Sub Reload()
    End Sub
End Class
Public Class PageLinkMain : Inherits MyPageRight
    Public Enum LinkStates
        Waiting = 0
    End Enum
    Public Shared LinkState As LinkStates
    Public Function TryExit(a As Boolean, b As Boolean) As Boolean
        Return True
    End Function
    Public Shared Function ValidateCodeFormat(a As Object) As Object
        Return Nothing
    End Function
    Public Shared Sub Join(Optional a As Object = Nothing)
    End Sub
End Class
Public Class PageOtherHelp : Inherits MyPageRight
    Public Shared Sub OnItemClick(Value As Object)
    End Sub
End Class
Public Class PageOtherLeft : Inherits MyPageLeft
    Public Property PageID As Integer = 0
    Public PanItem As Panel
    Public Function PageGet(subType As Object) As MyPageRight
        Return Nothing
    End Function
    Public Shared Sub RefreshHelp()
    End Sub
End Class
Public Class PageOtherTest : Inherits MyPageRight
    Public Shared Sub Jrrp()
    End Sub
    Public Shared Sub MemoryOptimize(a As Boolean)
    End Sub
    Public Shared Sub MemoryOptimizeInternal(a As Boolean)
    End Sub
    Public Shared Sub RubbishClear()
    End Sub
    Public Shared Sub StartCustomDownload(url As String, name As String, Optional extra As String = Nothing)
    End Sub
    Public Shared Function GetRandomHint() As String
        Return ""
    End Function
    Public Shared Function GetRandomCave() As String
        Return ""
    End Function
End Class
Public Class PageSelectLeft : Inherits MyPageLeft
End Class
Public Class PageSelectRight : Inherits MyPageRight
    Public Shared ShowHidden As Boolean
    Public Shared Function GameInstanceListContent(i As GameInstance) As UIElement
        Return Nothing
    End Function
End Class
Public Class PageSetupLaunch : Inherits MyPageRight
    Public Shared Sub UpdateSkinType()
    End Sub
    Public Shared Sub UpdateRamType()
    End Sub
End Class
Public Class PageSetupLeft : Inherits MyPageLeft
    Public Property PageID As Integer = 0
    Public PanItem As Panel
    Public Function PageGet(subType As Object) As MyPageRight
        Return Nothing
    End Function
End Class
Public Class PageSetupUI : Inherits MyPageRight
    Public Shared ReadOnly Property IsLauncherNewest As Boolean = True
    Public Shared HiddenForceShow As Boolean = False
    Public CheckLogoLeft As FrameworkElement
    Public PanLogoText As FrameworkElement
    Public PanLogoChange As FrameworkElement
    Public CardLogo As MyCard
    Public Shared Sub HiddenRefresh()
    End Sub
    Public Shared Sub BackgroundRefresh(a As Boolean, b As Boolean)
    End Sub
    Public Shared Sub OnMainPageTypeChanged()
    End Sub
End Class
Public Class PageSpeedLeft : Inherits MyPageLeft
    Public Sub TaskRemove(loader As Object)
    End Sub
    Public Sub TaskRefresh(loader As Object)
    End Sub
End Class
Public Class PageSpeedRight : Inherits MyPageRight
End Class
Public Class MyMsgLogin : Inherits Grid
    Public Sub New(x As Object)
    End Sub
End Class

#End Region

#Region "MC 类型占位"

Public Enum LoginType
    None = 0
    Legacy = 1
    Ms = 2
    Nide = 3
    Auth = 4
End Enum
Public Class CrashAnalyzer
    Public Sub Import(a As Object)
    End Sub
    Public Function Prepare() As Boolean
        Return False
    End Function
    Public Sub Analyze()
    End Sub
    Public Sub Output(a As Boolean, b As List(Of String))
    End Sub
End Class
Public Class LoginInput
    Public Property Type As LoginType
End Class
Public Class LoginOutput
    Public Property Name As String = ""
    Public Property Uuid As String = ""
End Class
Public Class GameFolder
    Public Property Location As String = ""
    Public Shared Widening Operator CType(Value As GameFolder) As String
        Return If(Value Is Nothing, "", Value.Location)
    End Operator
End Class
Public Class GameWatcher
    Public Sub Kill()
    End Sub
End Class
Public Class ResourceProject
    Public Property TranslatedName As String = ""
End Class

#End Region

#Region "MC 函数占位"

Module ModDevelop
    Public Sub Start()
    End Sub
End Module

Module ModGameStubs
    Public Sub GameLoaderNoop(l As LoaderTask(Of Integer, Integer))
    End Sub
    Public Sub GameLoginNoop(l As LoaderTask(Of LoginInput, LoginOutput))
    End Sub
    Public Function HasRunningMinecraft() As Boolean
        Return False
    End Function
    Public Sub JavaInit()
    End Sub
    Public Sub MusicControlNext()
    End Sub
    Public Sub MusicControlPause()
    End Sub
    Public Sub MusicRefreshPlay(a As Boolean, b As Boolean)
    End Sub
    Public Sub GameLaunchCancel()
    End Sub
    Public Function GameLaunchJavaSelected() As Object
        Return Nothing
    End Function
End Module

#End Region
