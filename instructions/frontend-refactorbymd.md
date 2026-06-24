# Ajustes para o front após refactor `refactorbymd`

## Resumo

Não houve mudança intencional de rotas públicas. O principal impacto para o front é padronizar o tratamento de erros da API.

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

`GET /api/Couple` continua retornando `200` com objeto vazio quando o casal ainda não foi configurado.

## Tratamento de erros HTTP

Para erros de regra de negócio e erros não tratados, a API retorna `application/problem+json`.

Exemplo:

```json
{
  "status": 404,
  "title": "Recurso não encontrado",
  "detail": "Presente '...' não encontrado.",
  "correlationId": "0HN..."
}
```

O front deve:

- Mostrar `detail` como mensagem principal quando existir.
- Usar `title` como fallback.
- Guardar ou exibir `correlationId` em telas/logs de suporte.
- Tratar `401` como sessão expirada ou token inválido.
- Tratar `403` como usuário sem permissão.

## Erros de validação

Quando o body/query falha validação de DTO, a API retorna `ValidationProblemDetails`.

Exemplo:

```json
{
  "status": 400,
  "title": "Erro de validação",
  "detail": "Verifique os campos enviados e tente novamente.",
  "errors": {
    "name": ["O campo Nome é obrigatório."]
  }
}
```

O front deve mapear `errors` por campo quando possível e usar `detail` como mensagem geral.

## Pagamentos

As rotas de pagamento continuam retornando `PaymentResponseDto`, inclusive nos erros `400` e `502`.

Exemplo:

```json
{
  "status": "error",
  "errorCode": "VALIDATION_ERROR",
  "message": "Mensagem do erro",
  "mpRequestId": "..."
}
```

O front deve manter tratamento separado para pagamento:

- Se HTTP `400` ou `502` vier com `status: "error"`, usar `message` como mensagem principal.
- Se houver `mpRequestId`, anexar em logs ou tela de suporte.
- Para Pix, continuar lendo `qrCode`, `qrCodeBase64` ou `pixQrCode`.
- Para cartão, considerar `contributionCreated` para atualizar a tela de confirmação quando vier preenchido.

## Checklist

- Centralizar parsing de erro `ProblemDetails`.
- Centralizar parsing de erro `ValidationProblemDetails`.
- Manter parser especial para `PaymentResponseDto`.
- Confirmar que chamadas admin continuam enviando Bearer token.
- Confirmar que upload de imagem usa `multipart/form-data` com campo `file`.
- Confirmar que telas públicas continuam sem token: gifts, couple, contributions públicas e pagamentos.
