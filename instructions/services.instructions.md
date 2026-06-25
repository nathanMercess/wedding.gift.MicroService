# Services Instructions

- Apply these rules to any service created or changed in `InfluencyMe.Platform.XXX.Services.Implementations` and its contract in `InfluencyMe.Platform.XXX.Services.Contracts`.
- Follow existing service style in this repository before introducing new patterns.
- Keep service methods focused on application/business orchestration.

## Mandatory pre-checks
- Confirm there is an existing service contract/interface for the behavior (reuse before creating a new service).
- Confirm implementation class is registered in dependency injection extension.
- Confirm required DTOs already exist in `InfluencyMe.Framework.DataTransferObject` (do not create local DTOs).
- Confirm required repository contracts already exist in `Infra.Contracts`.
- Confirm method naming between contract and implementation is consistent (`Async` suffix in services).
- Confirm exception strategy uses handled/domain exceptions already used in the project.
- Confirm service contracts consumed by Webapi return DTOs/read models, not domain entities.

## Preferred Rules
- Keep service classes `internal sealed`.
- Keep constructor injection explicit and only with required dependencies.
- Validate contracts at method start using guard clauses.
- Use repository query methods and domain behaviors instead of duplicating rules.
- Use `AsNoTracking()` for read-only queries.
- Map domain entities to response DTOs inside service implementations before returning to Webapi.
- Keep service methods small and split orchestration into private methods when needed.
- Prefer batch operations to reduce round-trips when processing collections.

## Hard Rules
- Never place HTTP concerns in services (`[FromBody]`, headers, route concerns, `HttpContext` branching).
- Never expose domain entities from service contracts used by controllers.
- Never access DbContext directly from service when repository abstraction already exists.
- Never skip contract validation for required ids/inputs.
- Never create dead code or unused private methods.
- Never break transaction consistency when operation spans multiple writes.
- Always use private readonly fields for dependencies injected via constructor.
	- Example: `private readonly IConversationRepository _conversationRepository;`
	- Example constructor: `public ConversationService(IConversationRepository conversationRepository) => _conversationRepository = conversationRepository;`
- Services should never have state beyond their method scope (no instance fields except dependencies).
- Always prefer batch operations to reduce round-trips.
	- Database or api calls should prefer using methods that accept collections of ids or entities to process multiple items in one call instead of looping and calling single-item methods multiple times.

## Implementation Pattern (step by step)
1. Define or update service contract.
   - Add method in `Services.Contracts/Services/I...Service.cs` with `Async` suffix and `CancellationToken`.
2. Implement method in service class.
   - Keep signature identical to contract.
3. Add guard clauses first.
   - Validate ids, required request object, and required fields.
4. Load required aggregate/data.
   - Use repository methods with tracking mode according to operation intent.
5. Enforce business rule.
   - Apply domain methods and fail with handled exception when rule is violated.
6. Persist changes.
   - Use repository `SaveChangesAsync` for simple writes.
   - Use unit of work transaction for multi-step writes that must be atomic.
7. Map and return result.
   - Use existing mapping extensions in service implementation.
   - Return response DTO/read model to Webapi callers.
8. Register service in dependency injection.
   - Add `AddScoped<IContract, Implementation>()` in `DependencyInjectionExtensions`.
9. Validate with build and tests.
   - Build solution and run relevant tests (or full test suite when behavior changes).

## Expected Structure/Names (Examples)

### Contract location and naming
- `src/InfluencyMe.Platform.Chat.Services.Contracts/Services/IConversationService.cs`
- Interface name pattern: `I{Name}Service`
- Method pattern: `Task<TResult> BusinessActionAsync(..., CancellationToken cancellationToken);`

### Implementation location and naming
- `src/InfluencyMe.Platform.Chat.Services.Implementations/Services/ConversationService.cs`
- Class declaration pattern: `internal sealed class ConversationService : IConversationService`

### Dependency injection registration
- `src/InfluencyMe.Platform.Chat.Services.Implementations/Extensions/DependencyInjectionExtensions.cs`
- Pattern: `services.AddScoped<IConversationService, ConversationService>();`
	- Look for a AddServices or similar method in the extension class for the service category.

### Method style examples from current repository
- Input validation:
  - `if (referenceId.IsInvalid() || request is null) throw new HandledException(ExceptionConstants.INVALID_CONTRACT);`
- Not found handling:
  - `return member?.ToMemberDto() ?? throw new HandledException(ExceptionConstants.NOT_FOUND);`
- Transactional orchestration:
  - Begin transaction, write message, replicate dependent rows, commit, rollback on failure.
- Read model query:
  - `AsNoTracking().FirstOrDefaultAsync(cancellationToken)` for read-only paths.

## Anti-patterns (avoid)
- Fat service methods mixing validation, persistence, mapping, and side effects without separation.
- Repeating query/persistence logic already available in repositories.
- Missing transaction boundary for operations with multiple dependent writes.
- Throwing generic exceptions where handled exception constants are expected.
- Ignoring cancellation token in async EF/repository calls.
- Creating ad-hoc DTOs in service projects.
- Large nested conditionals instead of guard clauses and early returns.
