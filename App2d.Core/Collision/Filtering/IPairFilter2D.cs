namespace App2d.Core.Collision.Filtering;

/// <summary>Early eligibility for an unordered pair. Implementations must give the same answer in either argument order.</summary>
public interface IPairFilter2D<in T>
    where T : class
{
    bool ShouldTest(T first, T second);
}
