using wedding.gift.Crosscutting.Constants;

namespace wedding.gift.Domain.Model.Entities;

public sealed class Gift
{
    private readonly List<Contribution> _contributions = [];

    private Gift()
    {
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public decimal Total { get; private set; }
    public string Image { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public bool Available { get; private set; } = true;
    public bool AllowPartialContribution { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public ICollection<Contribution> Contributions => _contributions;
    public decimal RaisedAmount => _contributions.Where(x => x.Status == ContributionStatus.Paid).Sum(x => x.Amount);
    public decimal RemainingAmount => Math.Max(Total - RaisedAmount, 0);
    public bool FullyFunded => Total > 0 && RaisedAmount >= Total;

    public static Gift Create(
        string name,
        string description,
        decimal price,
        decimal total,
        string image,
        string category,
        bool available,
        bool allowPartialContribution)
    {
        Gift gift = new()
        {
            Id = Guid.NewGuid()
        };

        gift.Update(name, description, price, total, image, category, available, allowPartialContribution);
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
        bool available,
        DateTime createdAt)
    {
        Gift gift = new()
        {
            Id = id,
            Name = name.Trim(),
            Description = description.Trim(),
            Price = price,
            Total = total > 0 ? total : price,
            Image = image.Trim(),
            Category = category.Trim(),
            Available = available,
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
        string category,
        bool available,
        bool allowPartialContribution)
    {
        Name = name.Trim();
        Description = description.Trim();
        Price = price;
        Total = total > 0 ? total : price;
        Image = image.Trim();
        Category = category?.Trim() ?? string.Empty;
        Available = available;
        AllowPartialContribution = allowPartialContribution;
        Touch();
    }

    public void SetAvailability(bool available)
    {
        Available = available;
        Touch();
    }

    public bool CanReceiveContribution(decimal amount)
        => Available && (AllowPartialContribution || amount >= RemainingAmount) && amount <= RemainingAmount;

    private void Touch()
        => UpdatedAt = DateTime.UtcNow;
}
