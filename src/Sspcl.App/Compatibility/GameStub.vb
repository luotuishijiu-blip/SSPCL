' 远行星号版：GameInstance / GameVersion 的最小占位类型。
' 原 Sspcl 的 Minecraft 版本实例已移除，这里仅保留让 Settings / 文本变量替换等基础设施能够编译的最小成员。

Public Class GameVersion
    Public Property VanillaName As String = ""
    Public Property HasForge As Boolean = False
    Public Property Forge As String = ""
    Public Property HasFabric As Boolean = False
    Public Property Fabric As String = ""
    Public Property HasNeoForge As Boolean = False
    Public Property NeoForge As String = ""
    Public Property HasLiteLoader As Boolean = False
End Class

Public Class GameInstance
    Public Property PathVersion As String = ""
    Public Property PathIndie As String = ""
    Public Property Name As String = ""
    Public Property Version As GameVersion = New GameVersion()
    Public ReadOnly Property IsLoaded As Boolean = True

    Public Sub New()
    End Sub
    Public Sub New(Name As String)
        Me.Name = Name
    End Sub
End Class

Public Class PageSetupSystem
    Public Shared ReadOnly Property IsLauncherNewest As Boolean = True
End Class

Module ModSspclStub
    Public Function FilterUserName(Text As String, ReplaceChar As Char) As String
        Return Text
    End Function
    Public Function FilterAccessToken(Text As String, ReplaceChar As Char) As String
        Return Text
    End Function
End Module
