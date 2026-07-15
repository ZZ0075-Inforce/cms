namespace CMS.API.Models.Lookups;

/// <summary>
/// Slim Certification row for the Course N-N multi-select. Title is <c>nchar(100)</c> in the DB, so
/// the lookup query must <c>RTRIM</c> it or every label carries trailing spaces.
/// </summary>
public class CertificationLookup
{
    public int Pkid { get; set; }
    public string Title { get; set; } = string.Empty;
}
