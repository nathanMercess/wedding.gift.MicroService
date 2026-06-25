# Ajustes para o front após refactor `refactorbymd`

## Resumo

As rotas continuam as mesmas, mas o contrato de resposta mudou. A API agora responde JSON no envelope padrão:

```json
{
  "success": true,
  "data": {},
  "error": null,
  "correlationId": "0HN..."
}
```

Em erro:

```json
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
```

O front deve tratar mensagens a partir de `error.code`. A API não envia mais mensagem amigável em `detail`, `title` ou `message` para erro de regra de negócio.

## Rotas

Manter as rotas atuais com prefixo `/api`:

- `POST /api/Auth/login`
- `POST /api/Auth/register`
- `GET /api/Auth/confirm-email`
- `GET /api/Gifts`
- `GET /api/Gifts/stats`
- `GET /api/Gifts/{id}`
- `POST /api/Gifts/{id}/contribute`
- `GET /api/Gifts/{giftId}/contributions`
- `GET /api/Couple`
- `POST /api/Payment/card`
- `POST /api/Payment/pix`
- `GET /api/Payment/status/{mpOrderId}`
- `GET /api/Contributions`
- `GET /api/Contributions/{id}`
- `POST /api/Contributions`
- `GET /api/admin/dashboard`
- `GET /api/admin/gifts`
- `POST /api/admin/gifts`
- `PUT /api/admin/gifts/{id}`
- `PATCH /api/admin/gifts/{id}/availability`
- `DELETE /api/admin/gifts/{id}`
- `POST /api/admin/gifts/enrich`
- `PUT /api/admin/couple`
- `POST /api/admin/uploads/image`

O webhook do Mercado Pago continua aceitando `/api/webhook/mercadopago` e `/webhook/mercadopago`. O front não precisa consumir essa rota.

## Sucesso

Ler sempre `response.data` para o payload real.

Exemplos:

- Login: `data.accessToken`, `data.expiresAtUtc`, `data.userName`, `data.email`, `data.role`.
- Lista paginada de gifts: `data.items`, `data.totalCount`, `data.page`, `data.pageSize`.
- `GET /api/Couple`: `data` pode vir com objeto vazio quando o casal ainda não foi configurado.
- Confirmação de e-mail, delete de gift e webhook retornam `success: true` com `data: null`.

## Erros

O front deve centralizar um parser:

```ts
type ApiResponse<T> = {
  success: boolean;
  data: T | null;
  error: {
    code: string;
    fields?: Record<string, string[]> | null;
    details?: unknown;
  } | null;
  correlationId: string;
};
```

Regras:

- Se `success === true`, usar `data`.
- Se `success === false`, usar `error.code` para buscar mensagem local no front.
- Guardar `correlationId` em logs/telas de suporte.
- Tratar HTTP `401` com `error.code = "UNAUTHORIZED"` como sessão expirada/token inválido.
- Tratar HTTP `403` com `error.code = "FORBIDDEN"` como usuário sem permissão.

## Validação

Quando body/query falha validação de DTO:

```json
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
```

O front deve mapear `error.fields` por campo e traduzir `FIELD_INVALID` localmente.

## Códigos

Criar tabela local de mensagens para estes códigos gerais:

- `BAD_REQUEST`
- `UNAUTHORIZED`
- `FORBIDDEN`
- `NOT_FOUND`
- `HTTP_ERROR`
- `VALIDATION_ERROR`
- `FIELD_INVALID`
- `UNHANDLED_ERROR`
- `REQUIRED_FIELDS`
- `INVALID_CREDENTIALS`
- `USER_INACTIVE`
- `EMAIL_NOT_CONFIRMED`
- `EMAIL_ALREADY_EXISTS`
- `USER_NOT_FOUND`
- `INVALID_CONFIRMATION_TOKEN`
- `INVALID_JWT_CONFIGURATION`
- `INVALID_CONTRIBUTION_STATUS`
- `CONTRIBUTION_NOT_FOUND`
- `GIFT_NOT_FOUND`
- `GIFT_UNAVAILABLE`
- `INVALID_GIFT_PAGE`
- `INVALID_GIFT_PAGE_SIZE`
- `INVALID_DASHBOARD_DAYS`
- `INVALID_DASHBOARD_RECENT_ITEMS`
- `INVALID_IMAGE_FILE`
- `IMAGE_FILE_TOO_LARGE`
- `INVALID_IMAGE_CONTENT_TYPE`
- `INVALID_PRODUCT_URL`
- `PRODUCT_URL_UNREACHABLE`
- `INVALID_BOOTSTRAP_ADMIN_ROLE`
- `UNAUTHORIZED_WEBHOOK`

Pagamentos também usam estes códigos:

- `PAYMENT_DECLINED`
- `INSUFFICIENT_AMOUNT`
- `DUPLICATE_ORDER`
- `PIX_EXPIRED`
- `PIX_REJECTED`
- `INVALID_CARD_TOKEN`
- `PROVIDER_ERROR`
- `VALIDATION_ERROR`

## Pagamentos

Sucesso de pagamento vem no envelope, com `PaymentResponseDto` dentro de `data`:

```json
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
```

Erro de pagamento vem no envelope de erro:

```json
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
```

O front deve usar `error.code` também para pagamento. Para Pix aprovado/pendente, continuar lendo `data.qrCode`, `data.qrCodeBase64` ou `data.pixQrCode`.

## Checklist

- Trocar parser de `ProblemDetails`/`ValidationProblemDetails` pelo parser de `ApiResponse<T>`.
- Criar dicionário local `error.code -> mensagem`.
- Ler payload sempre de `data`.
- Atualizar telas de pagamento para tratar erro por `error.code`.
- Confirmar que chamadas admin continuam enviando Bearer token.
- Confirmar que upload de imagem usa `multipart/form-data` com campo `file`.
- Confirmar que telas públicas continuam sem token: gifts, couple, contribuições públicas e pagamentos.

## Prompt para o front

Use este prompt no projeto frontend:

```text
Atualize a integração com a wedding-gift-api para o contrato novo.

Todas as respostas JSON agora vêm no envelope:
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

Regras:
- Não ler mais payload direto da raiz da resposta; sempre usar response.data.
- Não tratar ProblemDetails, ValidationProblemDetails, title, detail ou message como fonte principal de erro.
- Centralizar um parser ApiResponse<T>.
- Se success=true, retornar data para as telas.
- Se success=false, traduzir error.code em mensagens locais do front.
- Para validação, mapear error.fields por campo e traduzir FIELD_INVALID.
- Guardar correlationId para suporte/logs.
- Tratar 401/UNAUTHORIZED como sessão expirada ou token inválido.
- Tratar 403/FORBIDDEN como usuário sem permissão.
- Pagamentos também usam o mesmo envelope; em sucesso, PaymentResponseDto vem em data; em erro, usar error.code.
- Para Pix, continuar lendo data.qrCode, data.qrCodeBase64 ou data.pixQrCode.
- Para admin, manter Bearer token.
- Para upload, manter multipart/form-data com campo file.

Crie/atualize tipos TypeScript:
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

Atualize todos os services/clients HTTP para desembrulhar ApiResponse<T> e ajuste telas para consumir data.
```
