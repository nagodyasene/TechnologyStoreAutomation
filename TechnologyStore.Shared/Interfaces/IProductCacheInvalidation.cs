namespace TechnologyStore.Shared.Interfaces;

/// <summary>
/// Optional interface implemented by cached repositories to allow callers to invalidate caches
/// after out-of-band writes (e.g., purchase order receipt updates stock directly).
/// </summary>
public interface IProductCacheInvalidation
{
    void InvalidateProductCaches();
}


