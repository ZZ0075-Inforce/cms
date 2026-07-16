using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>
/// Write DTO for dbo.PublishStatus. Unlike most tables, <see cref="Pkid"/> is a client-supplied key
/// (tinyint, NOT an IDENTITY): it is written on INSERT and is the record identity on PUT, where it is
/// immutable (never in the UPDATE SET list — Course FKs to it and the FK has no ON UPDATE CASCADE).
/// </summary>
public class PublishStatusRequest
{
    /// <summary>主代碼 — supplied on create; the immutable key on update. tinyint range is 0–255.</summary>
    [Range(0, 255, ErrorMessage = "主代碼須介於 0 到 255")]
    public byte Pkid { get; set; }

    [Required(ErrorMessage = "狀態說明為必填")]
    [StringLength(50)]
    public string Description { get; set; } = string.Empty;

    public bool IsDraft { get; set; }
    public bool IsPublished { get; set; }
    public bool IsDiscontinued { get; set; }
}
