using System.ComponentModel.DataAnnotations;

namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class GuestSuggestionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class GuestConfirmationCreateDto
{
    [Required(ErrorMessage = "Informe ao menos uma pessoa.")]
    [MinLength(1, ErrorMessage = "Informe ao menos uma pessoa.")]
    [MaxLength(20, ErrorMessage = "Uma confirmação pode conter no máximo 20 pessoas.")]
    public List<GuestConfirmationGuestDto> Guests { get; set; } = [];
}

public sealed class GuestConfirmationGuestDto
{
    public Guid? GuestInvitationId { get; set; }

    [Required(ErrorMessage = "O nome do convidado é obrigatório.")]
    [MaxLength(120, ErrorMessage = "O nome do convidado deve ter no máximo 120 caracteres.")]
    public string Name { get; set; } = string.Empty;

    public bool IsSubmitter { get; set; }
}

public sealed class ConfirmedGuestResponseDto
{
    public Guid Id { get; set; }
    public Guid? GuestInvitationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public bool IsSubmitter { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class GuestConfirmationResponseDto
{
    public Guid Id { get; set; }
    public string SubmittedByName { get; set; } = string.Empty;
    public int PartySize { get; set; }
    public DateTime ConfirmedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public List<ConfirmedGuestResponseDto> Guests { get; set; } = [];
}

public sealed class GuestInvitationCreateDto
{
    [Required(ErrorMessage = "O nome do convite é obrigatório.")]
    [MaxLength(120, ErrorMessage = "O nome do convite deve ter no máximo 120 caracteres.")]
    public string Name { get; set; } = string.Empty;
}

public sealed class GuestInvitationUpdateDto
{
    [Required(ErrorMessage = "O nome do convite é obrigatório.")]
    [MaxLength(120, ErrorMessage = "O nome do convite deve ter no máximo 120 caracteres.")]
    public string Name { get; set; } = string.Empty;
}

public sealed class GuestInvitationActiveUpdateDto
{
    public bool IsActive { get; set; }
}

public sealed class GuestInvitationImportResponseDto
{
    public int CreatedCount { get; set; }
    public int SkippedCount { get; set; }
}

public sealed class GuestInvitationResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsConfirmed { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? SubmittedByName { get; set; }
    public int? PartySize { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class GuestInvitationQueryDto
{
    [MaxLength(120)]
    public string? Search { get; set; }

    [MaxLength(20)]
    public string? Status { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}

public sealed class GuestConfirmationQueryDto
{
    [MaxLength(120)]
    public string? Search { get; set; }

    [MaxLength(20)]
    public string? Source { get; set; }

    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}

public sealed class GuestSummaryDto
{
    public int InvitationCount { get; set; }
    public int PendingInvitationCount { get; set; }
    public int ConfirmationCount { get; set; }
    public int ConfirmedGuestCount { get; set; }
    public int FreeTextGuestCount { get; set; }
}
