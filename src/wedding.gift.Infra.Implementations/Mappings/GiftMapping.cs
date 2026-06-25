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
        builder.Property(x => x.Description).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Price).HasColumnType("decimal(10,2)").IsRequired();
        builder.Property(x => x.Total).HasColumnType("decimal(10,2)").IsRequired();
        builder.Property(x => x.Image).HasMaxLength(500);
        builder.Property(x => x.Category).IsRequired().HasMaxLength(80);
        builder.Property(x => x.Available).HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.Ignore(x => x.RaisedAmount);
        builder.Ignore(x => x.RemainingAmount);
        builder.Ignore(x => x.FullyFunded);

        builder.Metadata.FindNavigation(nameof(Gift.Contributions))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasData(GetSeedGifts());
    }

    private static IEnumerable<Gift> GetSeedGifts()
    {
        DateTime createdAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        return
        [
            Gift.Seed(Guid.Parse("cbb2ebce-0130-4acc-aebc-054ca72cbfca"), "Jogo de Panelas Inox", "Conjunto com 5 panelas em inox para o dia a dia.", 899.90m, 899.90m, "https://images.example.com/panelas-inox.jpg", "Cozinha", true, createdAt),
            Gift.Seed(Guid.Parse("b8db2a9c-ee89-41f7-b50a-6d9fa59e34fc"), "Liquidificador 1200W", "Liquidificador potente com copo de vidro.", 459.90m, 459.90m, "https://images.example.com/liquidificador.jpg", "Eletrodomesticos", true, createdAt),
            Gift.Seed(Guid.Parse("bdb8970b-6645-4cc5-a2e7-e45ff77595f8"), "Jogo de Cama Queen", "Kit completo 400 fios para cama queen.", 329.99m, 329.99m, "https://images.example.com/jogo-cama.jpg", "Quarto", true, createdAt),
            Gift.Seed(Guid.Parse("df173a4e-d8f8-472f-ae72-7b64e3e8f076"), "Aparelho de Jantar 20 Pecas", "Conjunto de jantar em porcelana branca.", 519.00m, 519.00m, "https://images.example.com/aparelho-jantar.jpg", "Mesa", true, createdAt),
            Gift.Seed(Guid.Parse("42fdcc72-664b-4d65-95e2-e8f4f906f28b"), "Aspirador Robo", "Aspirador inteligente com base carregadora.", 1299.00m, 1299.00m, "https://images.example.com/aspirador-robo.jpg", "Casa", true, createdAt),
            Gift.Seed(Guid.Parse("0ac3abdd-0c2d-4234-b72b-b327d8563af7"), "Cafeteira Expresso", "Cafeteira automatica para capsulas e po.", 699.90m, 699.90m, "https://images.example.com/cafeteira.jpg", "Cozinha", true, createdAt)
        ];
    }
}
