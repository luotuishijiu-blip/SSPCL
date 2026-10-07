Public Class PageStarsectorSettingsLeft

    Public Event TabChanged(tag As Integer)

    Private Sub Item_Check(sender As Object, e As RouteEventArgs) Handles ItemLaunch.Check, ItemUI.Check, ItemSystem.Check
        RaiseEvent TabChanged(CInt(sender.Tag))
    End Sub

End Class
