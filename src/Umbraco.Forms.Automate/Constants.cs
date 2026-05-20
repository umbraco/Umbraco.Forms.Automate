namespace Umbraco.Forms.Automate;

/// <summary>
/// Constants for the Forms Automate package.
/// </summary>
public static class Constants
{
    /// <summary>
    /// Umbraco backoffice section aliases referenced by Forms Automate step types.
    /// </summary>
    public static class Sections
    {
        /// <summary>
        /// The Forms section — required for any step type that exposes form definitions,
        /// records, or PII captured by form submissions (IP address, member key, field
        /// values).
        /// </summary>
        public const string Forms = "forms";
    }
}
