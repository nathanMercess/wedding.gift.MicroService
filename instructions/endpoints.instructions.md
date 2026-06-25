# Endpoints Instructions

- Apply these rules to any endpoint created or changed in `InfluencyMe.Platform.XXX.Application.Webapi`.
- Follow existing controller style in this repository before introducing new patterns.
- Keep endpoint methods thin: HTTP contract + delegation to service only, no validations or multiple service calls.

## Mandatory pre-checks
- Confirm there is an existing controller for the domain (reuse before creating a new one).
- Confirm the controller inherits from `ApiControllerBase`.
- Confirm endpoint behavior already exists in Services Contracts/Implementations (or create service contract first).
- Confirm request/response DTOs already exist in `InfluencyMe.Framework.DataTransferObject` (do not create local DTOs).
- Confirm service contracts return response DTOs/read models, never domain entities.
- Confirm route shape is consistent with existing endpoints in the same controller.
- Confirm endpoint authorization expectation (default is authenticated via base controller).

## Preferred Rules
- Use one controller per aggregate/domain entry point (example: `ConversationController`).
- Keep controllers `sealed`.
- Constructor injection should be expression-bodied when single assignment.
- Always use explicit parameter binding attributes (`[FromRoute]`, `[FromBody]`, `[FromQuery]`).
- Prefer route constraints for identifiers (`{id:guid}`).
- Keep naming explicit and business-readable.
- Keep endpoint methods without `Async` suffix (service methods keep `Async` suffix).
- Keep route segments consistent in casing/style already used by the controller.
- Return DTO/type directly when project relies on global exception/response middleware.
- Prefer expression-bodied endpoint methods (`=> await service.MethodAsync(...)`) when the endpoint is a single delegation.

## Hard Rules
- Never put business logic in controllers.
- Never access repositories/DbContext directly from controllers.
	- Only call service contracts (interfaces) injected via constructor.
- Never reference `Domain.Model` entities in controllers.
- Never call entity-to-DTO mapping extensions in controllers.
- Never return `ActionResult<T>` or `IActionResult` from controllers unless a framework-only edge case is explicitly required.
- Never return domain entities from service contracts consumed by Webapi controllers.
- Never add optional `CancellationToken` parameters (it must be mandatory).
- Never create endpoint-specific DTOs inside Webapi project.
- Never create dead code or unused endpoints.
- Never break base conventions:
  - `[ApiController]`
  - `[Authorize]`
  - `[Route("api/[controller]")]`

## Implementation Pattern (step by step)
1. Locate controller.
   - Reuse existing controller; create a new one only if domain boundary is clear.
2. Define or confirm contract.
   - Ensure method exists in `Services.Contracts` with `Async` suffix and `CancellationToken`.
3. Validate DTOs.
   - Reuse DTOs from framework package.
4. Add endpoint signature.
   - Choose correct verb attribute (`[HttpGet]`, `[HttpPost]`, `[HttpPut]`, `[HttpDelete]`).
   - Define route template with constraints when needed.
5. Bind inputs explicitly.
   - Route IDs via `[FromRoute]`, payload via `[FromBody]`, pagination/filter via `[FromQuery]`.
6. Delegate to service only.
   - Single call to injected service, no orchestration logic in controller.
   - If the service returns the response DTO directly, the controller should return that DTO directly.
7. Keep return contract stable.
   - Return DTO/type or `Task` according to existing controller style.
   - Use `Response.StatusCode` only for status changes that must stay without `ActionResult` (example: `201 Created`).
8. Validate consistency.
   - Method naming, route style, parameter order, and token usage aligned with existing endpoints.
9. Build and tests.
   - Build solution and run relevant tests (or full test suite when endpoint behavior changes).

## Expected Structure/Names (Examples)

### Controller shape
- `Controllers/Base/ApiControllerBase.cs`
- `Controllers/ConversationController.cs`

### Base controller pattern
- `ApiControllerBase : ControllerBase`
- Attributes in base:
  - `[ApiController]`
  - `[Authorize]`
  - `[Route("api/[controller]")]`

### Endpoint method pattern
- `public async Task<ConversationAndMessagesDto> GetMemberConversationByReferenceId(...) => await _conversationService.GetMemberConversationByReferenceIdAsync(...);`
- `public async Task StartConversationsForMembers(...) => await _conversationService.StartConversationsForMembersAsync(...);`
- `public async Task<GiftResponseDto> GetById([FromRoute] Guid id, CancellationToken cancellationToken) => await giftService.GetByIdAsync(id, cancellationToken);`

### Route pattern examples from current repository
- `GET    api/Conversation/{referenceId:guid}/Members/{memberId:guid}/GetConversation`
- `GET    api/Conversation/{referenceId:guid}/Users/{userId:guid}`
- `POST   api/Conversation`
- `POST   api/Conversation/{referenceId:guid}/SendTextMessage`
- `POST   api/Conversation/{referenceId:guid}/Members/{memberId:guid}/Messages/{messageId:guid}/MarkAsRead`
- `PUT    api/Conversation/{referenceId:guid}/Open`

## Anti-patterns (avoid)
- Fat controllers with validation/business rules/persistence logic.
- Controllers importing `Domain.Model` or calling `ToResponseDto()`.
- Hidden model binding (missing `[FromBody]` / `[FromQuery]` / `[FromRoute]` in complex signatures).
- Inconsistent route naming inside same controller.
- Mixing response styles (`ActionResult<T>` in one method, raw DTO in others).
- Missing route constraints for GUIDs.
- Calling implementation classes directly instead of contracts.
- Creating local DTOs in Webapi project.
