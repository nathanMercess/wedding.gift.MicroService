# Prompt unico para o front - refactor `refactorbymd`

Use este prompt no projeto frontend:

```text
Atualize a integracao com a wedding-gift-api para o contrato novo do backend.

Contexto:
- As rotas continuam as mesmas com prefixo /api.
- O webhook do Mercado Pago aceita /api/webhook/mercadopago e /webhook/mercadopago, mas o front nao precisa consumir essa rota.
- O backend agora retorna DTOs/read models pelas services; nenhuma tela deve depender de formato de entidade de dominio.
- A API nao retorna mais ProblemDetails ou ValidationProblemDetails como contrato principal.
- A API nao envia mensagem amigavel de erro como detail, title ou message para regra de negocio.
- O front deve traduzir erros localmente a partir de error.code.

Rotas consumidas pelo front:
- POST /api/Auth/login
- POST /api/Auth/register
- GET /api/Auth/confirm-email
- GET /api/Gifts
- GET /api/Gifts/stats
- GET /api/Gifts/{id}
- POST /api/Gifts/{id}/contribute
- GET /api/Gifts/{giftId}/contributions
- GET /api/Couple
- POST /api/Payment/card
- POST /api/Payment/pix
- GET /api/Payment/status/{mpOrderId}
- GET /api/Contributions
- GET /api/Contributions/{id}
- POST /api/Contributions
- GET /api/admin/dashboard
- GET /api/admin/gifts
- POST /api/admin/gifts
- PUT /api/admin/gifts/{id}
- PATCH /api/admin/gifts/{id}/availability
- DELETE /api/admin/gifts/{id}
- POST /api/admin/gifts/enrich
- PUT /api/admin/couple
- POST /api/admin/uploads/image

Todas as respostas JSON agora vem neste envelope:
{
  "success": boolean,
  "data": T | null,
  "error": {
    "code": string,
    "fields": Record<string, string[]> | null,
    "details": unknown | null
  } | null,
  "correlationId": string
}

Crie/atualize estes tipos TypeScript:
type ApiResponse<T> = {
  success: boolean;
  data: T | null;
  error: ApiError | null;
  correlationId: string;
};

type ApiError = {
  code: string;
  fields?: Record<string, string[]> | null;
  details?: unknown | null;
};

Regras obrigatorias:
- Centralizar um parser ApiResponse<T> para todos os clients/services HTTP.
- Nunca ler payload direto da raiz da resposta; sempre usar response.data.
- Se success=true, retornar data para as telas.
- Se success=false, traduzir error.code em mensagens locais do front.
- Guardar correlationId em logs/telas de suporte.
- Tratar HTTP 401 ou error.code UNAUTHORIZED como sessao expirada/token invalido.
- Tratar HTTP 403 ou error.code FORBIDDEN como usuario sem permissao.
- Nao tratar ProblemDetails, ValidationProblemDetails, title, detail ou message como fonte principal de erro.
- Remover parsers antigos especificos de ProblemDetails/ValidationProblemDetails.

Exemplo de sucesso:
{
  "success": true,
  "data": {},
  "error": null,
  "correlationId": "0HN..."
}

Exemplo de erro:
{
  "success": false,
  "data": null,
  "error": {
    "code": "GIFT_NOT_FOUND",
    "fields": null,
    "details": null
  },
  "correlationId": "0HN..."
}

Validacao:
- Quando body/query falha validacao, a resposta vem com error.code = VALIDATION_ERROR.
- Campos invalidos vem em error.fields.
- Cada campo recebe codigos como FIELD_INVALID.
- Mapear error.fields por campo e traduzir FIELD_INVALID localmente.

Exemplo de validacao:
{
  "success": false,
  "data": null,
  "error": {
    "code": "VALIDATION_ERROR",
    "fields": {
      "name": ["FIELD_INVALID"]
    },
    "details": null
  },
  "correlationId": "0HN..."
}

Codigos gerais para criar no dicionario local de mensagens:
- BAD_REQUEST
- UNAUTHORIZED
- FORBIDDEN
- NOT_FOUND
- HTTP_ERROR
- VALIDATION_ERROR
- FIELD_INVALID
- UNHANDLED_ERROR
- REQUIRED_FIELDS
- INVALID_CREDENTIALS
- USER_INACTIVE
- EMAIL_NOT_CONFIRMED
- EMAIL_ALREADY_EXISTS
- USER_NOT_FOUND
- INVALID_CONFIRMATION_TOKEN
- INVALID_JWT_CONFIGURATION
- INVALID_CONTRIBUTION_STATUS
- CONTRIBUTION_NOT_FOUND
- GIFT_NOT_FOUND
- GIFT_UNAVAILABLE
- INVALID_GIFT_PAGE
- INVALID_GIFT_PAGE_SIZE
- INVALID_DASHBOARD_DAYS
- INVALID_DASHBOARD_RECENT_ITEMS
- INVALID_IMAGE_FILE
- IMAGE_FILE_TOO_LARGE
- INVALID_IMAGE_CONTENT_TYPE
- INVALID_PRODUCT_URL
- PRODUCT_URL_UNREACHABLE
- INVALID_BOOTSTRAP_ADMIN_ROLE
- UNAUTHORIZED_WEBHOOK

Codigos de pagamento:
- PAYMENT_DECLINED
- INSUFFICIENT_AMOUNT
- DUPLICATE_ORDER
- PIX_EXPIRED
- PIX_REJECTED
- INVALID_CARD_TOKEN
- PROVIDER_ERROR
- VALIDATION_ERROR

Pagamentos:
- Pagamento tambem usa o mesmo envelope.
- Em sucesso, PaymentResponseDto vem em data.
- Em erro, usar error.code como fonte unica de traducao.
- Para Pix, continuar lendo data.qrCode, data.qrCodeBase64 ou data.pixQrCode.
- Para cartao, considerar data.contributionCreated quando vier preenchido.

Exemplo de sucesso de pagamento:
{
  "success": true,
  "data": {
    "status": "approved",
    "errorCode": null,
    "qrCode": "",
    "qrCodeBase64": null,
    "pixQrCode": ""
  },
  "error": null,
  "correlationId": "0HN..."
}

Exemplo de erro de pagamento:
{
  "success": false,
  "data": null,
  "error": {
    "code": "PAYMENT_DECLINED",
    "fields": null,
    "details": null
  },
  "correlationId": "0HN..."
}

Payloads de sucesso esperados:
- Login: data.accessToken, data.expiresAtUtc, data.userName, data.email, data.role.
- Lista paginada de gifts: data.items, data.totalCount, data.page, data.pageSize.
- GET /api/Couple: data pode vir como objeto vazio quando o casal ainda nao foi configurado.
- Confirmacao de e-mail, delete de gift e webhook retornam success=true com data=null.

Checklist final:
- Atualizar todos os services/clients HTTP para desembrulhar ApiResponse<T>.
- Ajustar telas para consumir data.
- Criar dicionario local error.code -> mensagem.
- Atualizar telas de pagamento para tratar erro por error.code.
- Manter Bearer token nas chamadas admin.
- Manter upload de imagem como multipart/form-data com campo file.
- Manter telas publicas sem token: gifts, couple, contribuicoes publicas e pagamentos.
```
