using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>
/// Write DTO for dbo.Partner. Pkid travels in the body on PUT (per the coding convention) and is
/// ignored on POST, where the DB generates it.
/// </summary>
public class PartnerRequest
{
    public short Pkid { get; set; }

    [Required(ErrorMessage = "廠商名稱為必填")]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 廠商代碼. Not a key — the DDL declares no UNIQUE constraint on it, and nothing FKs to it,
    /// so it stays editable (contrast AppRole.RoleId, which is the key and is immutable).
    /// </summary>
    [Required(ErrorMessage = "廠商代碼為必填")]
    [StringLength(10)]
    public string AppKey { get; set; } = string.Empty;

    [Required(ErrorMessage = "廠商選單顯示名稱為必填")]
    [StringLength(200)]
    public string NameOnPartnerMenu { get; set; } = string.Empty;

    [Required(ErrorMessage = "課程明細頁顯示名稱為必填")]
    [StringLength(50)]
    public string NameOnCourseDetailPage { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int DisplayOrder { get; set; }

    /// <summary>圖片檔名 — varchar(50) NULL in the DB, so optional here.</summary>
    [StringLength(50)]
    public string? ImageFilename { get; set; }
}
