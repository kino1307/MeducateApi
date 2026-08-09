namespace Meducate.Application;

internal static class TopicConstants
{
    internal const int MinSourceLength = 100;
    internal const string TopicTypeOther = "Other";
    internal const string TopicTypeNonMedical = "Non-Medical";

    // Topic types that map to an ICD-11-codeable clinical entity. Drug/Procedure/etc.
    // use other coding systems (or none) and are never looked up.
    internal static readonly HashSet<string> Icd11CodeableTypes =
        ["Disease", "Disorder", "Syndrome", "Symptom", "Mental Health"];
}
