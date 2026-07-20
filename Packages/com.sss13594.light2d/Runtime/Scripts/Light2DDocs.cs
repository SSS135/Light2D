namespace Light2D
{
    /// <summary>
    /// Single source of the components' <c>[HelpURL]</c> target. Centralized so a future docs move is a
    /// one-line change. Internal by design: exporting it would break the ContractTests public-surface
    /// assertion, and an internal compile-time const is still valid as an attribute argument in this
    /// assembly.
    /// </summary>
    internal static class Light2DDocs
    {
        // Canonical help target; matches package.json's documentationUrl (the live GitHub repository).
        internal const string HelpUrl = "https://github.com/SSS135/Light2D/blob/master/README.md";
    }
}
