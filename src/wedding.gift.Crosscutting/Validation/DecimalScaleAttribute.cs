using System.ComponentModel.DataAnnotations;

namespace wedding.gift.Crosscutting.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class DecimalScaleAttribute(int maximumDecimalPlaces) : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is null)
            return true;

        if (value is not decimal decimalValue)
            return false;

        int scale = (decimal.GetBits(decimalValue)[3] >> 16) & 0x7F;
        return scale <= maximumDecimalPlaces;
    }
}
