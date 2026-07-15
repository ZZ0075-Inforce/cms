using CMS.API.Models.Lookups;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>Slim lists for FK / N-N dropdowns.</summary>
[ApiController]
[Route("api/lookups")]
[Produces("application/json")]
public class LookupsController(ILookupRepository repository) : ControllerBase
{
    [HttpGet("app-users")]
    [ProducesResponseType(typeof(IReadOnlyList<AppUserLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AppUserLookup>>> GetAppUsers(CancellationToken ct)
        => Ok(await repository.GetAppUsersAsync(ct));

    [HttpGet("app-roles")]
    [ProducesResponseType(typeof(IReadOnlyList<AppRoleLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AppRoleLookup>>> GetAppRoles(CancellationToken ct)
        => Ok(await repository.GetAppRolesAsync(ct));

    [HttpGet("partners")]
    [ProducesResponseType(typeof(IReadOnlyList<PartnerLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PartnerLookup>>> GetPartners(CancellationToken ct)
        => Ok(await repository.GetPartnersAsync(ct));

    [HttpGet("course-groups")]
    [ProducesResponseType(typeof(IReadOnlyList<CourseGroupLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CourseGroupLookup>>> GetCourseGroups(CancellationToken ct)
        => Ok(await repository.GetCourseGroupsAsync(ct));

    [HttpGet("publish-statuses")]
    [ProducesResponseType(typeof(IReadOnlyList<PublishStatusLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PublishStatusLookup>>> GetPublishStatuses(CancellationToken ct)
        => Ok(await repository.GetPublishStatusesAsync(ct));

    [HttpGet("certifications")]
    [ProducesResponseType(typeof(IReadOnlyList<CertificationLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CertificationLookup>>> GetCertifications(CancellationToken ct)
        => Ok(await repository.GetCertificationsAsync(ct));

    [HttpGet("job-categories")]
    [ProducesResponseType(typeof(IReadOnlyList<JobCategoryLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<JobCategoryLookup>>> GetJobCategories(CancellationToken ct)
        => Ok(await repository.GetJobCategoriesAsync(ct));

    [HttpGet("training-centers")]
    [ProducesResponseType(typeof(IReadOnlyList<TrainingCenterLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TrainingCenterLookup>>> GetTrainingCenters(CancellationToken ct)
        => Ok(await repository.GetTrainingCentersAsync(ct));

    /// <summary>
    /// Resolves a Promotion2 by its PromoCode for the FeaturedPromoItem edit form. The code is a URL
    /// path segment (nvarchar(30), no :int constraint) — the Angular service <c>encodeURIComponent</c>s
    /// it. Returns 404 when no promotion carries that code.
    /// </summary>
    [HttpGet("promotions/by-code/{code}")]
    [ProducesResponseType(typeof(PromotionLookup), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PromotionLookup>> GetPromotionByCode(string code, CancellationToken ct)
    {
        var promotion = await repository.GetPromotionByCodeAsync(code, ct);
        return promotion is null
            ? NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "找不到促銷代碼",
                Detail = $"找不到促銷代碼「{code}」對應的活動。"
            })
            : Ok(promotion);
    }
}
