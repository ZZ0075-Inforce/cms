namespace CMS.API.Models;

/// <summary>
/// Search DTO for POST /api/featured-promo-items/query. Every field is nullable — omitted/null means
/// "no filter". The board drives two of them: the active TrainingCenter tab
/// (<see cref="TrainingCenterPkid"/>) and the selected Monday–Sunday week
/// (<see cref="ScheduleOnFrom"/> / <see cref="ScheduleOnTo"/>).
/// </summary>
public class FeaturedPromoItemQuery
{
    public short? TrainingCenterPkid { get; set; }

    public DateOnly? ScheduleOnFrom { get; set; }
    public DateOnly? ScheduleOnTo { get; set; }
}
