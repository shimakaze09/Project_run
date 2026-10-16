namespace Run.Common
{
    /// <summary>
    /// GameObject tag strings used by more than one script, kept in one place so a rename shows
    /// up as a single compile error instead of several silent <c>CompareTag</c> mismatches.
    /// </summary>
    public static class Tags
    {
        public const string Player = "Player";
    }
}
