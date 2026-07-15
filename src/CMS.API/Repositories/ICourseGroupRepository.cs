using CMS.API.Models;

namespace CMS.API.Repositories;

public interface ICourseGroupRepository
{
    Task<IReadOnlyList<CourseGroup>> GetAllAsync(CancellationToken ct = default);

    Task<IReadOnlyList<CourseGroup>> QueryAsync(CourseGroupQuery query, CancellationToken ct = default);

    Task<CourseGroup?> GetByIdAsync(short pkid, CancellationToken ct = default);

    /// <summary>Inserts the course group. Returns the new IDENTITY pkid.</summary>
    Task<short> InsertAsync(CourseGroupRequest request, CancellationToken ct = default);

    /// <summary>Updates the group name. Returns false when the pkid does not exist.</summary>
    Task<bool> UpdateAsync(CourseGroupRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes the course group. Returns false when the pkid does not exist.
    /// Throws SqlException 547 only when a PartnerCourseGroup row still references it — that FK does
    /// not cascade, so the caller must translate it into 409. Note that any Course rows in the group
    /// are cascade-deleted by FK_Course_CourseGroup and do NOT block the delete.
    /// </summary>
    Task<bool> DeleteAsync(short pkid, CancellationToken ct = default);
}
