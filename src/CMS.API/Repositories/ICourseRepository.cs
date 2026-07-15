using CMS.API.Models;

namespace CMS.API.Repositories;

public interface ICourseRepository
{
    Task<IReadOnlyList<Course>> GetAllAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Course>> QueryAsync(CourseQuery query, CancellationToken ct = default);

    /// <summary>A single course with both N-N pkid lists populated. Null when the pkid is unknown.</summary>
    Task<Course?> GetByIdAsync(int pkid, CancellationToken ct = default);

    /// <summary>Inserts the course and both N-N sets in one transaction. Returns the new pkid.</summary>
    Task<int> InsertAsync(CourseRequest request, CancellationToken ct = default);

    /// <summary>Updates the course and re-syncs both N-N sets. False when the pkid does not exist.</summary>
    Task<bool> UpdateAsync(CourseRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes the course. Returns false when the pkid does not exist.
    /// The two N-N junctions cascade, but CourseFAQ / CourseRelatedLink / HotCourse do NOT — a course
    /// still referenced by one of those throws SqlException 547, which the caller turns into 409.
    /// </summary>
    Task<bool> DeleteAsync(int pkid, CancellationToken ct = default);
}
