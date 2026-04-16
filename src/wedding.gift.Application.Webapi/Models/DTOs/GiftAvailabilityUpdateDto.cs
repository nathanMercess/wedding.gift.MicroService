using System.ComponentModel.DataAnnotations;

namespace wedding.gift.Application.Webapi.Models.DTOs;

public class GiftAvailabilityUpdateDto
{
    [Required(ErrorMessage = "O campo available é obrigatório.")]
    public bool Available { get; set; }
}
