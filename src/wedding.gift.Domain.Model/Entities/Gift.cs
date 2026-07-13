using wedding.gift.Crosscutting.Constants;

namespace wedding.gift.Domain.Model.Entities;

public sealed class Gift
{
    private readonly List<Contribution> _contributions = [];

    private Gift()
    {
    }

    public Guid Id { get; private set; }
    public Guid CoupleId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public decimal Total { get; private set; }
    public string Image { get; private set; } = string.Empty;
    public string? Category { get; private set; }
    public bool AllowPartialContribution { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public ICollection<Contribution> Contributions => _contributions;
    public decimal RaisedAmount => _contributions.Where(x => x.Status == ContributionStatus.Paid).Sum(x => x.NetAmount);
    public decimal RemainingAmount => Math.Max(Total - RaisedAmount, 0);
    public bool FullyFunded => Total > 0 && RaisedAmount >= Total;

    public static Gift Create(
        string name,
        string description,
        decimal price,
        decimal total,
        string image,
        string category,
        bool allowPartialContribution,
        Guid? coupleId = null)
    {
        Gift gift = new()
        {
            Id = Guid.NewGuid(),
            CoupleId = coupleId ?? Couple.SingletonId
        };

        gift.Update(name, description, price, total, image, category, allowPartialContribution);
        gift.CreatedAt = gift.UpdatedAt;

        return gift;
    }

    public static Gift Seed(
        Guid id,
        string name,
        string description,
        decimal price,
        decimal total,
        string image,
        string category,
        DateTime createdAt,
        Guid? coupleId = null)
    {
        Gift gift = new()
        {
            Id = id,
            CoupleId = coupleId ?? Couple.SingletonId,
            Name = name.Trim(),
            Description = description.Trim(),
            Price = price,
            Total = total > 0 ? total : price,
            Image = image.Trim(),
            Category = string.IsNullOrWhiteSpace(category) ? string.Empty : category.Trim(),
            AllowPartialContribution = true,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };

        return gift;
    }

    public void Update(
        string name,
        string description,
        decimal price,
        decimal total,
        string image,
        string? category,
        bool allowPartialContribution)
    {
        Name = name.Trim();
        Description = description.Trim();
        Price = price;
        Total = total > 0 ? total : price;
        Image = image.Trim();
        Category = string.IsNullOrWhiteSpace(category) ? string.Empty : category.Trim();
        AllowPartialContribution = allowPartialContribution;
        Touch();
    }

    public void UpdateCategory(string? category)
    {
        Category = string.IsNullOrWhiteSpace(category) ? string.Empty : category.Trim();
        Touch();
    }

    public bool CanReceiveContribution(decimal amount)
        => (AllowPartialContribution || amount >= RemainingAmount) && amount <= RemainingAmount;

    private void Touch()
        => UpdatedAt = DateTime.UtcNow;
}
