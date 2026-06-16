# Padrões de código — .NET Backend

Estes padrões são obrigatórios para qualquer arquivo novo/editado neste projeto.

## Referências base
- **Controller**: [src/wedding.gift.Application.Webapi/Controllers/AdminGiftsController.cs](src/wedding.gift.Application.Webapi/Controllers/AdminGiftsController.cs)
- **Service contrato**: [src/wedding.gift.Services.Contracts/IGiftService.cs](src/wedding.gift.Services.Contracts/IGiftService.cs)
- **Service implementação**: [src/wedding.gift.Services.Implementations/GiftService.cs](src/wedding.gift.Services.Implementations/GiftService.cs)
- **Repository**: [src/wedding.gift.Infra.Implementations/Repositories/PaymentRepository.cs](src/wedding.gift.Infra.Implementations/Repositories/PaymentRepository.cs)

---

## Arquitetura em camadas

```
Application.Webapi   → Controllers (thin), configuração de DI/middleware
Services.Contracts   → Interfaces (IXxxService)
Services.Implementations → Lógica de negócio, exceções, extensões de mapeamento
Infra.Contracts      → Interfaces de repositório (IXxxRepository)
Infra.Implementations → EF Core DbContext, mappings, repositórios, migrations
Crosscutting         → DTOs, constantes, configurações (Options), modelos compartilhados
Domain.Model         → Entidades
```

Regra de dependência: cada camada só referencia as camadas abaixo dela — `Webapi` não importa diretamente `Infra.Implementations`.

---

## Controllers

1. **Thin controllers** — sem lógica de negócio. Só validam entrada, delegam ao service e retornam o resultado.
2. **Primary constructor** (C# 12) com injeção direta dos serviços:
   ```csharp
   public class GiftsController(IGiftService giftService) : ApiControllerBase { }
   ```
3. **Herdar de `ApiControllerBase`** (adiciona `[ApiController]`, `[Authorize]`, `[Route("[controller]")]`). Para rotas públicas, sobrescrever com `[AllowAnonymous]` no método ou classe.
4. **`[ProducesResponseType]`** declarado em todos os endpoints.
5. **Deixar as exceções subirem** — o middleware global (`UseExceptionHandler`) captura `AppException` e mapeia para `ProblemDetails`. Não usar try/catch nos controllers, a menos que haja lógica de fallback real.

---

## Exceções e erros

Usar sempre as subclasses de `AppException` em `Services.Implementations/Exceptions`:

| Classe | HTTP | Quando usar |
|---|---|---|
| `BadRequestException` | 400 | Dados inválidos, regras de negócio violadas |
| `NotFoundException` | 404 | Entidade não encontrada |
| `ConflictException` | 409 | Estado conflitante (ex: presente indisponível) |

```csharp
var entity = await dbContext.Gifts.FindAsync(id)
    ?? throw new NotFoundException($"Presente '{id}' não encontrado.");
```

Nunca retornar `null` de um service quando a entidade era esperada — sempre lançar `NotFoundException`.

---

## Services

1. **Contrato em `Services.Contracts`** — interface pura, sem dependências de infra.
2. **Implementação em `Services.Implementations`** — injetar `AppDbContext` diretamente (não usar repositórios dentro de services, a menos que seja o `IPaymentRepository` ou outro com lógica SQL específica).
3. **Primary constructor** para injeção:
   ```csharp
   public class GiftService(AppDbContext dbContext) : IGiftService { }
   ```
4. **Registrar no `DependencyInjectionExtensions.AddServices()`** — nunca registrar services diretamente no `Program.cs` (exceção: `StorageClient` singleton, que é infraestrutura).
5. **Mapeamento entity ↔ DTO** via métodos de extensão estáticos em `EntityDtoMappings.cs` (`ToEntity()`, `ToResponseDto()`, `ApplyUpdate()`).

---

## Repositórios

- Interface em `Infra.Contracts`, implementação em `Infra.Implementations/Repositories`.
- Usar EF Core diretamente (`AppDbContext`). Repositórios só existem quando a query é complexa ou reutilizada.
- `AsNoTracking()` em todas as queries de leitura.
- Cancelation token em todos os métodos assíncronos.

---

## Migrations e banco

- **`MigrateAsync()` no startup** (`Program.cs`) — aplica migrations pendentes automaticamente a cada deploy:
  ```csharp
  using (var scope = app.Services.CreateScope())
  {
      var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      await db.Database.MigrateAsync();
  }
  ```
- Gerar migrations com:
  ```bash
  dotnet ef migrations add NomeDaMigration --project src/wedding.gift.Infra.Implementations --startup-project src/wedding.gift.Application.Webapi --context AppDbContext
  ```

---

## Configuração (Options pattern)

Toda configuração externa usa o Options pattern em `Crosscutting/Models/Configurations`:

```csharp
public class GcsOptions
{
    public const string SectionName = "Gcs";
    public string BucketName { get; set; } = string.Empty;
}
```

Registrar em `Program.cs`:
```csharp
builder.Services.Configure<GcsOptions>(builder.Configuration.GetSection(GcsOptions.SectionName));
```

Consumir via `IOptions<T>` injetado no construtor do service.

---

## Rotas

- O `RouteConvention("api")` prefixa automaticamente todas as rotas com `/api/`.
- Usar `[Route("admin/recurso")]` para rotas protegidas e `[Route("recurso")]` para públicas.
- Guids em rotas: `{id:guid}`.

---

## Regras gerais

- Nomes em **inglês** (código, classes, métodos, propriedades). Mensagens de erro para o usuário em **português**.
- Sem comentários no código, salvo casos onde o "porquê" é genuinamente não óbvio.
- `CancellationToken cancellationToken` em todos os métodos assíncronos públicos.
- DTOs de resposta nunca expõem campos de infraestrutura (ex: `PasswordHash`, `PasswordSalt`).
- `Available` de `Gift` é sempre recalculado pelo servidor a partir da soma de contribuições pagas — nunca aceitar o valor do cliente como definitivo.
