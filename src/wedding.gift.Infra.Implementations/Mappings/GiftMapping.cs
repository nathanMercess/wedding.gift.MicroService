using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using wedding.gift.Domain.Model.Entities;

namespace wedding.gift.Infra.Implementations.Mappings;

public class GiftMapping : IEntityTypeConfiguration<Gift>
{
    public void Configure(EntityTypeBuilder<Gift> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title).IsRequired().HasMaxLength(120);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Price).HasColumnType("decimal(10,2)").IsRequired();
        builder.Property(x => x.ImageUrl).HasMaxLength(500);
        builder.Property(x => x.Category).IsRequired().HasMaxLength(80);
        builder.Property(x => x.Available).HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasData(GetSeedGifts());
    }

    private static IEnumerable<Gift> GetSeedGifts()
    {
        var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        return
        [
            new Gift
            {
                Id = Guid.Parse("cbb2ebce-0130-4acc-aebc-054ca72cbfca"),
                Title = "Jogo de Panelas Inox",
                Description = "Conjunto com 5 panelas em inox para o dia a dia.",
                Price = 899.90m,
                ImageUrl = "https://images.example.com/panelas-inox.jpg",
                Category = "Cozinha",
                Available = true,
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            },
            new Gift
            {
                Id = Guid.Parse("b8db2a9c-ee89-41f7-b50a-6d9fa59e34fc"),
                Title = "Liquidificador 1200W",
                Description = "Liquidificador potente com copo de vidro.",
                Price = 459.90m,
                ImageUrl = "https://images.example.com/liquidificador.jpg",
                Category = "Eletrodomésticos",
                Available = true,
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            },
            new Gift
            {
                Id = Guid.Parse("bdb8970b-6645-4cc5-a2e7-e45ff77595f8"),
                Title = "Jogo de Cama Queen",
                Description = "Kit completo 400 fios para cama queen.",
                Price = 329.99m,
                ImageUrl = "https://images.example.com/jogo-cama.jpg",
                Category = "Quarto",
                Available = true,
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            },
            new Gift
            {
                Id = Guid.Parse("df173a4e-d8f8-472f-ae72-7b64e3e8f076"),
                Title = "Aparelho de Jantar 20 Peças",
                Description = "Conjunto de jantar em porcelana branca.",
                Price = 519.00m,
                ImageUrl = "https://images.example.com/aparelho-jantar.jpg",
                Category = "Mesa",
                Available = true,
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            },
            new Gift
            {
                Id = Guid.Parse("42fdcc72-664b-4d65-95e2-e8f4f906f28b"),
                Title = "Aspirador Robô",
                Description = "Aspirador inteligente com base carregadora.",
                Price = 1299.00m,
                ImageUrl = "https://images.example.com/aspirador-robo.jpg",
                Category = "Casa",
                Available = true,
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            },
            new Gift
            {
                Id = Guid.Parse("0ac3abdd-0c2d-4234-b72b-b327d8563af7"),
                Title = "Cafeteira Expresso",
                Description = "Cafeteira automática para cápsulas e pó.",
                Price = 699.90m,
                ImageUrl = "https://images.example.com/cafeteira.jpg",
                Category = "Cozinha",
                Available = true,
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            }
        ];
    }
}
