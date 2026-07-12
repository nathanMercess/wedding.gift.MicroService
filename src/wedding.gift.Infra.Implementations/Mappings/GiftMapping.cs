using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using wedding.gift.Domain.Model.Entities;

namespace wedding.gift.Infra.Implementations.Mappings;

public sealed class GiftMapping : IEntityTypeConfiguration<Gift>
{
    public void Configure(EntityTypeBuilder<Gift> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(120);
        builder.Property(x => x.CoupleId).IsRequired();
        builder.Property(x => x.Description).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Price).HasColumnType("decimal(10,2)").IsRequired();
        builder.Property(x => x.Total).HasColumnType("decimal(10,2)").IsRequired();
        builder.Property(x => x.Image).IsRequired(false).HasMaxLength(500);
        builder.Property(x => x.Category).IsRequired(false).HasMaxLength(80);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.Ignore(x => x.RaisedAmount);
        builder.Ignore(x => x.RemainingAmount);
        builder.Ignore(x => x.FullyFunded);

        builder.Metadata.FindNavigation(nameof(Gift.Contributions))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => x.CoupleId);

        builder.HasData(GetSeedGifts());
    }

    private static IEnumerable<Gift> GetSeedGifts()
    {
        DateTime createdAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        return
        [
            Gift.Seed(Guid.Parse("cbb2ebce-0130-4acc-aebc-054ca72cbfca"), "Jogo de Panelas Inox", "Conjunto com 5 panelas em inox para o dia a dia.", 899.90m, 899.90m, "https://images.example.com/panelas-inox.jpg", "Cozinha", createdAt),
            Gift.Seed(Guid.Parse("b8db2a9c-ee89-41f7-b50a-6d9fa59e34fc"), "Liquidificador 1200W", "Liquidificador potente com copo de vidro.", 459.90m, 459.90m, "https://images.example.com/liquidificador.jpg", "Eletrodomésticos", createdAt),
            Gift.Seed(Guid.Parse("bdb8970b-6645-4cc5-a2e7-e45ff77595f8"), "Jogo de Cama Queen", "Kit completo 400 fios para cama queen.", 329.99m, 329.99m, "https://images.example.com/jogo-cama.jpg", "Quarto", createdAt),
            Gift.Seed(Guid.Parse("df173a4e-d8f8-472f-ae72-7b64e3e8f076"), "Aparelho de Jantar 20 Peças", "Conjunto de jantar em porcelana branca.", 519.00m, 519.00m, "https://images.example.com/aparelho-jantar.jpg", "Mesa", createdAt),
            Gift.Seed(Guid.Parse("42fdcc72-664b-4d65-95e2-e8f4f906f28b"), "Aspirador Robô", "Aspirador inteligente com base carregadora.", 1299.00m, 1299.00m, "https://images.example.com/aspirador-robo.jpg", "Casa", createdAt),
            Gift.Seed(Guid.Parse("0ac3abdd-0c2d-4234-b72b-b327d8563af7"), "Cafeteira Expresso", "Cafeteira automática para cápsulas e pó.", 699.90m, 699.90m, "https://images.example.com/cafeteira.jpg", "Cozinha", createdAt)
        ];
    }
}
