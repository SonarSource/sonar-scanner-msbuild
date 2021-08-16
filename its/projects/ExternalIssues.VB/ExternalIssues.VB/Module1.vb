Module Module1

    ' SonarLint violations
    '   S3385 (exit sub)                              : on in QP, off in local ruleset
    '   S2358 (replace Not... Is with IsNot)          : off in both QP and ruleset
    '   S2372 (remove exception from property getter) : off in QP, on in local ruleset
    '   S112  (don't throw Exception in user code)    : on in both, error in local ruleset
    '   
    '  External issues: (only reported when SQ >= v7.4)
    '   CC0021 : use NameOf(a)
    '   CC0062 : consider naming intefaces starting with 'I'

    Private Foo As Integer
    Sub Main()

        Exit Sub ' violates S3385
    End Sub

    Private Sub Test()
        Dim a = Not "a" Is Nothing  ' violates CC0021 and S2358
    End Sub


    Public ReadOnly Property Property1 As String
        Get
            Throw New Exception("violates S2372 and S112")
        End Get
    End Property

    Public Interface MyInterface ' CC0062 consider naming interfaces starting with 'I'

    End Interface

End Module

