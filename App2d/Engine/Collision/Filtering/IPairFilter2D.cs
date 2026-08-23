namespace App2d.Engine.Collision.Filtering;

public interface IPairFilter2D<in T>
    where T : class
{
    bool ShouldTest(T first, T second);
}
