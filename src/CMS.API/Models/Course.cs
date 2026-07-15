namespace CMS.API.Models;

/// <summary>
/// Response model for dbo.Course (課程).
/// <see cref="Pkid"/> IS the primary key (PK_Course over an int IDENTITY), so the routes carry an
/// :int constraint. The three <c>{Fk}Name</c> columns are JOIN-resolved display labels, not writable
/// (the write DTO carries only the <c>{Fk}Pkid</c> values).
/// </summary>
public class Course
{
    public int Pkid { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? OfficialTitle { get; set; }
    public string CourseId { get; set; } = string.Empty;
    public string ProdCourseId { get; set; } = string.Empty;
    public string FriendlyUrl { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public short PartnerPkid { get; set; }
    public short? CourseGroupPkid { get; set; }
    public byte PublishStatusPkid { get; set; }
    public DateOnly ScheduleOn { get; set; }
    public DateOnly ScheduleOff { get; set; }
    public short Hour { get; set; }
    public decimal ListPrice { get; set; }
    public decimal LearningCredit { get; set; }
    public string? Material { get; set; }
    public string? Objective { get; set; }
    public string? Target { get; set; }
    public string? Prerequisites { get; set; }
    public string? Outline { get; set; }
    public string? TowardCertOrExam { get; set; }
    public string? Note { get; set; }
    public string? OtherInfo { get; set; }
    public bool CanRepeat { get; set; }

    /// <summary>JOIN-resolved labels for display. Read-only; never in a write.</summary>
    public string PartnerName { get; set; } = string.Empty;
    public string? CourseGroupName { get; set; }
    public string PublishStatusName { get; set; } = string.Empty;

    /// <summary>N-N sets. Populated only by GetById (a separate query per junction).</summary>
    public List<int> CertificationPkids { get; set; } = [];
    public List<short> JobCategoryPkids { get; set; } = [];
}
