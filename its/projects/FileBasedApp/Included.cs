public static class IncludedHelper // Raises S3903
{
    // FIXME raises S1134
    public static string Message() => "Hello from an included file!"; // raises S3400
}
