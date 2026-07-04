namespace Servika.Contracts.Admin;

/// <summary>A service category as the admin sees it — includes the active flag.</summary>
public sealed record AdminCategoryDto(
    Guid Id,
    string Slug,
    string Name,
    string Tint,
    string? IconKey,
    int SortOrder,
    bool IsPopular,
    bool IsActive);

/// <summary>Create a category (POST /admin/categories). Slug must be unique.</summary>
public sealed record CreateCategoryRequest(
    string Slug,
    string Name,
    string Tint,
    string? IconKey,
    int SortOrder,
    bool IsPopular);

/// <summary>Edit a category's display fields (PUT /admin/categories/{id}). Slug is immutable.</summary>
public sealed record UpdateCategoryRequest(
    string Name,
    string Tint,
    string? IconKey,
    int SortOrder,
    bool IsPopular);
