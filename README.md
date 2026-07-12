# Wedding Gift API

API .NET 10 para lista de presentes de casamento, contribuições, pagamentos via Mercado Pago, personalização do site e administração.

## Requisitos

- .NET SDK 10
- SQL Server
- Credenciais do Mercado Pago
- Bucket Google Cloud Storage
- SMTP opcional para confirmações e notificações

## Configuração local

Os arquivos `appsettings` versionados não contêm segredos. Configure os valores por variáveis de ambiente, User Secrets ou Secret Manager.

```powershell
$env:ConnectionStrings__DefaultConnection="Server=..."
$env:Jwt__SigningKey="uma-chave-aleatoria-com-pelo-menos-32-caracteres"
$env:MercadoPago__AccessToken="..."
$env:MercadoPago__WebhookSecret="..."
$env:Gcs__BucketName="weddinggift-uploads"
dotnet run --project src/wedding.gift.Application.Webapi
```

Valores anteriormente versionados devem ser considerados comprometidos e rotacionados nos respectivos provedores.

## Build e testes

```powershell
dotnet restore
dotnet build wedding.gift.MicroService.slnx -c Release -warnaserror
dotnet test wedding.gift.MicroService.slnx -c Release --collect:"XPlat Code Coverage" --settings coverlet.runsettings
dotnet list wedding.gift.MicroService.slnx package --vulnerable --include-transitive
```

## Banco de dados

As migrations pendentes são aplicadas no startup. Para criar uma migration:

```powershell
dotnet ef migrations add NomeDaMigration --project src/wedding.gift.Infra.Implementations --startup-project src/wedding.gift.Application.Webapi --context AppDbContext
```

## Rotas principais

- `GET /api/gifts`: vitrine pública paginada
- `POST /api/payment/card` e `POST /api/payment/pix`: pagamentos
- `POST /api/payment/order-lookup/request`: solicita link de consulta sem revelar se o pedido existe
- `GET /api/payment/order-lookup/{token}`: consulta segura, curta e de uso único
- `POST /webhook/mercadopago`: webhook assinado
- `POST /api/auth/login`, `/refresh` e `/logout`: autenticação
- `GET /health/live` e `GET /health/ready`: saúde da aplicação
- `/api/admin/*`: operações administrativas protegidas por role
- `GET /api/admin/contributions` e `/export.csv`: central de contribuições e exportação
- `GET /api/admin/payments`: consulta operacional segura dos pagamentos
- `GET /api/admin/overview?days=30`: resumo financeiro isolado por casal

Swagger fica disponível fora do ambiente de produção.

## Regras financeiras

- A disponibilidade é derivada das contribuições pagas e reservas ativas.
- O cliente não define manualmente a disponibilidade.
- Pagamentos usam `OrderId` idempotente e rejeitam reuso com dados diferentes.
- Pix usa expiração explícita e estados oficiais do Mercado Pago.
- Reembolsos e chargebacks deixam de contar como valor arrecadado.
- Presentes com histórico financeiro não podem ser excluídos fisicamente.
- Confirmações ao convidado usam outbox persistente, retentativas e link de consulta com token armazenado somente como hash.
- Categorias aceitas: `Cozinha`, `Eletrodomésticos`, `Quarto`, `Mesa`, `Casa`; `null` representa sem categoria.

## Deploy

O workflow do GitHub Actions executa build, testes com cobertura, auditoria de pacotes, verificação de migrations, build da imagem e deploy no Cloud Run. Produção deve usar proteção de ambiente e segredos do Google Secret Manager.
