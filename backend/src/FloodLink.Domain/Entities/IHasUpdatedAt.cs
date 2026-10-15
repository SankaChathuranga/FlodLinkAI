namespace FloodLink.Domain.Entities;

/// <summary>
/// An entity whose <see cref="UpdatedAt"/> is stamped by <c>AppDbContext.SaveChanges</c> whenever
/// the row is modified, so callers never set it by hand.
/// </summary>
public interface IHasUpdatedAt
{
    /// <summary>UTC time of the last change to the row.</summary>
    DateTime UpdatedAt { get; set; }
}
