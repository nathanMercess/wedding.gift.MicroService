namespace wedding.gift.Crosscutting.Models.Configurations;

public class ApiOptions
{
    public const string SectionName = "Api";

    public string BaseUrl { get; set; } = "http://localhost:5000";
}
