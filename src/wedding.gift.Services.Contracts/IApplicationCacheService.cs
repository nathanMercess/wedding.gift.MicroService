namespace wedding.gift.Services.Contracts;

public interface IApplicationCacheService
{
    string CurrentVersion { get; }
    void Invalidate();
}
