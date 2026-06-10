using MartCart.Domain.Entities;

namespace MartCart.Domain.Contracts;

public readonly record struct DateRange(DateTimeOffset From, DateTimeOffset To);

public interface IMartRepository
{
    Task<Mart> UpsertAsync(Mart mart, CancellationToken ct = default);
    Task<Mart?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Mart>> ListAsync(CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface ICartRepository
{
    Task<Cart> CreateAsync(Guid martId, decimal threshold, decimal discountAmount, CancellationToken ct = default);
    Task<Cart?> GetAsync(Guid cartId, CancellationToken ct = default);
    Task<Cart?> GetActiveAsync(CancellationToken ct = default);
    Task AddItemAsync(Guid cartId, CartItem item, CancellationToken ct = default);
    Task UpdateItemAsync(CartItem item, CancellationToken ct = default);
    Task<bool> RemoveItemAsync(Guid cartId, Guid itemId, CancellationToken ct = default);
    Task CloseAsync(Guid cartId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid cartId, CancellationToken ct = default);
    Task<IReadOnlyList<Cart>> ListAsync(DateRange range, CancellationToken ct = default);
}

public interface IAuthGate
{
    Task<bool> IsPinSetAsync(CancellationToken ct = default);
    Task SetPinAsync(string pin, CancellationToken ct = default);
    Task<bool> UnlockAsync(string pin, CancellationToken ct = default);
    Task ChangePinAsync(string oldPin, string newPin, CancellationToken ct = default);
}
