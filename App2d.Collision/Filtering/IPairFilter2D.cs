namespace App2d.Collision.Filtering;

public interface IPairFilter2D<in T>
    where T : class
{
    bool ShouldTest(T first, T second);
}
