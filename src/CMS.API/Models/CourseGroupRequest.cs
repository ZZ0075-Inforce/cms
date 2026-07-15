using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>
/// Write DTO for dbo.CourseGroup. Pkid travels in the body on PUT (per the coding convention) and is
/// ignored on POST, where the DB generates it.
/// </summary>
public class CourseGroupRequest
{
    public short Pkid { get; set; }

    /// <summary>群組名稱 — the only real column. nvarchar(100) NOT NULL, no UNIQUE constraint.</summary>
    [Required(ErrorMessage = "群組名稱為必填")]
    [StringLength(100)]
    public string Description { get; set; } = string.Empty;
}
