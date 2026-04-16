using System.ComponentModel.DataAnnotations;

namespace wedding.gift.Crosscutting.Models.DTOs;

public class ContributionCreateDto
{
    [Required(ErrorMessage = "O GiftId é obrigatório.")]
    public Guid GiftId { get; set; }

    [Required(ErrorMessage = "O nome do contribuinte é obrigatório.")]
    [MaxLength(120, ErrorMessage = "O nome do contribuinte deve ter no máximo 120 caracteres.")]
    public string ContributorName { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "A mensagem deve ter no máximo 500 caracteres.")]
    public string Message { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue, ErrorMessage = "O valor da contribuição deve ser maior que zero.")]
    public decimal Amount { get; set; }

    [MaxLength(50, ErrorMessage = "O método de pagamento deve ter no máximo 50 caracteres.")]
    public string PaymentMethod { get; set; } = string.Empty;

    public DateTime PaidAt { get; set; }

    [Required(ErrorMessage = "O status é obrigatório.")]
    [RegularExpression("^(Pending|Paid|Cancelled)$", ErrorMessage = "Status inválido. Valores permitidos: Pending, Paid, Cancelled.")]
    public string Status { get; set; } = "Pending";
}
