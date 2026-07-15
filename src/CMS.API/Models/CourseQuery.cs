namespace CMS.API.Models;

/// <summary>
/// Search DTO for POST /api/courses/query. Every field is nullable — omitted/null means "no filter".
/// </summary>
public class CourseQuery
{
    /// <summary>LIKE across Title, OfficialTitle, CourseId, ProdCourseId, FriendlyUrl.</summary>
    public string? Keyword { get; set; }

    public short? PartnerPkid { get; set; }
    public short? CourseGroupPkid { get; set; }
    public byte? PublishStatusPkid { get; set; }
    public bool? CanRepeat { get; set; }

    public DateOnly? ScheduleOnFrom { get; set; }
    public DateOnly? ScheduleOnTo { get; set; }
    public DateOnly? ScheduleOffFrom { get; set; }
    public DateOnly? ScheduleOffTo { get; set; }
}
