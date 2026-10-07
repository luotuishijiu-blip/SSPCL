Public Class PageStarsectorOtherLeft

    Public Event TabChanged(tag As Integer)

    Private Sub Item_Check(sender As Object, e As RouteEventArgs) Handles ItemHelp.Check, ItemAbout.Check, ItemTest.Check, ItemFeedback.Check, ItemVote.Check
        RaiseEvent TabChanged(CInt(sender.Tag))
    End Sub

End Class
