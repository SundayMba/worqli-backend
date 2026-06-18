namespace Servika.Domain.Users;

/// <summary>
/// The kind of account a user holds. Drives role-based authorization (RBAC)
/// across the whole platform — see security/RBAC_AND_SECURITY.md. A user has
/// exactly one role.
/// </summary>
public enum Role
{
    Customer = 0,
    Artisan = 1,
    Admin = 2,
    SuperAdmin = 3,
}
