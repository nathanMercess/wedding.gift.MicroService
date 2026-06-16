namespace wedding.gift.Crosscutting.Models.Configurations;

public class GcsOptions
{
    public const string SectionName = "Gcs";

    public string BucketName { get; set; } = string.Empty;
}
