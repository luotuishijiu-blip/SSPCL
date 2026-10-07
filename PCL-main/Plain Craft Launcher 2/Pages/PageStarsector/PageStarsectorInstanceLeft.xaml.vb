Public Class PageStarsectorInstanceLeft

    Public Event TabChanged(tag As Integer)

    Private Sub Item_Check(sender As Object, e As RouteEventArgs) Handles ItemMemory.Check, ItemOverview.Check, ItemMod.Check, ItemExport.Check, ItemSettings.Check
        Logger.Info("TabCheck tag=" & sender.Tag)
        RaiseEvent TabChanged(CInt(sender.Tag))
    End Sub

End Class
