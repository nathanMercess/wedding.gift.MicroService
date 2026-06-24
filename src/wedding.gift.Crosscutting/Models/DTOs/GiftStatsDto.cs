namespace wedding.gift.Crosscutting.Models.DTOs;

public class GiftStatsDto
{
    public int Total { get; set; }
    public int Completed { get; set; }
    public int Contributors { get; set; }
    public decimal Raised { get; set; }
    public decimal Goal { get; set; }
}
