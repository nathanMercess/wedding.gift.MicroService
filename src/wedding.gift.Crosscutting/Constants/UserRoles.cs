namespace wedding.gift.Crosscutting.Constants;

public static class UserRoles
{
    public const string Admin = "Admin";
    public const string SuperAdmin = "SuperAdmin";
    public const string AdminOrSuperAdmin = Admin + "," + SuperAdmin;
    public const string Member = "Member";
    public const string AdminMemberOrSuperAdmin = Admin + "," + Member + "," + SuperAdmin;
}
