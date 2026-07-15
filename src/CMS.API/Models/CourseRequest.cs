using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>
/// Write DTO for dbo.Course. Pkid travels in the body on PUT (per the coding convention) and is
/// ignored on POST, where the DB generates it. Carries only the FK <c>{Fk}Pkid</c> values and the
/// two N-N pkid lists — never the JOIN-resolved <c>{Fk}Name</c> labels.
/// </summary>
public class CourseRequest
{
    public int Pkid { get; set; }

    [Required(ErrorMessage = "課程名稱為必填")]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(300)]
    public string? OfficialTitle { get; set; }

    [Required(ErrorMessage = "簡介代碼為必填")]
    [StringLength(50)]
    public string CourseId { get; set; } = string.Empty;

    [Required(ErrorMessage = "科目代碼為必填")]
    [StringLength(50)]
    public string ProdCourseId { get; set; } = string.Empty;

    [Required(ErrorMessage = "友善網址為必填")]
    [StringLength(100)]
    public string FriendlyUrl { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int DisplayOrder { get; set; }

    [Required]
    public short PartnerPkid { get; set; }

    /// <summary>Nullable FK — no course group is allowed.</summary>
    public short? CourseGroupPkid { get; set; }

    [Required]
    public byte PublishStatusPkid { get; set; }

    [Required]
    public DateOnly ScheduleOn { get; set; }

    [Required]
    public DateOnly ScheduleOff { get; set; }

    [Range(0, short.MaxValue)]
    public short Hour { get; set; }

    [Range(0.0, 999999999.0)]   // decimal(9,0)
    public decimal ListPrice { get; set; }

    [Range(0.0, 99999999.9)]    // decimal(9,1)
    public decimal LearningCredit { get; set; }

    [StringLength(500)]
    public string? Material { get; set; }

    [StringLength(4000)]
    public string? Objective { get; set; }

    [StringLength(500)]
    public string? Target { get; set; }

    [StringLength(4000)]
    public string? Prerequisites { get; set; }

    /// <summary>nvarchar(max) — no length cap.</summary>
    public string? Outline { get; set; }

    /// <summary>nvarchar(max) — no length cap.</summary>
    public string? TowardCertOrExam { get; set; }

    [StringLength(4000)]
    public string? Note { get; set; }

    [StringLength(4000)]
    public string? OtherInfo { get; set; }

    public bool CanRepeat { get; set; }

    public List<int> CertificationPkids { get; set; } = [];
    public List<short> JobCategoryPkids { get; set; } = [];
}
