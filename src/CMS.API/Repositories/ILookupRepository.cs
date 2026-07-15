using CMS.API.Models.Lookups;

namespace CMS.API.Repositories;

public interface ILookupRepository
{
    Task<IReadOnlyList<AppUserLookup>> GetAppUsersAsync(CancellationToken ct = default);

    Task<IReadOnlyList<AppRoleLookup>> GetAppRolesAsync(CancellationToken ct = default);

    Task<IReadOnlyList<PartnerLookup>> GetPartnersAsync(CancellationToken ct = default);

    Task<IReadOnlyList<CourseGroupLookup>> GetCourseGroupsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<PublishStatusLookup>> GetPublishStatusesAsync(CancellationToken ct = default);

    Task<IReadOnlyList<CertificationLookup>> GetCertificationsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<JobCategoryLookup>> GetJobCategoriesAsync(CancellationToken ct = default);

    Task<IReadOnlyList<TrainingCenterLookup>> GetTrainingCentersAsync(CancellationToken ct = default);

    /// <summary>
    /// Resolves a single Promotion2 by its unique PromoCode, for the FeaturedPromoItem edit form.
    /// Returns null when no promotion carries that code.
    /// </summary>
    Task<PromotionLookup?> GetPromotionByCodeAsync(string promoCode, CancellationToken ct = default);
}
