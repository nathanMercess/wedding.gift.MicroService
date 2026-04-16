using System.ComponentModel.DataAnnotations;

namespace wedding.gift.Crosscutting.Models.DTOs;

public class GiftAvailabilityUpdateDto
{
    [Required(ErrorMessage = "O campo available é obrigatório.")]
    public bool Available { get; set; }
}
