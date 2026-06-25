namespace wedding.gift.Crosscutting.Models.DTOs.Auth;

public sealed class RegisterResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Message { get; set; } = "Cadastro realizado com sucesso. Verifique seu e-mail para confirmar a conta.";
}
