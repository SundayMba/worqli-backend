using Servika.Application.Abstractions.Persistence;
using Servika.Application.Common;
using Servika.Contracts.Admin;
using Servika.Domain.Catalogue;

namespace Servika.Application.Admin;

/// <summary>Maps a category to the admin DTO (includes the active flag).</summary>
internal static class AdminCategoryMapping
{
    public static AdminCategoryDto ToAdminDto(this ServiceCategory c) =>
        new(c.Id, c.Slug, c.Name, c.Tint, c.IconKey, c.SortOrder, c.IsPopular, c.IsActive);
}

/// <summary>Every category (incl. inactive), in display order — the admin catalogue.</summary>
public sealed class ListAllCategoriesHandler
{
    private readonly ICatalogueRepository _catalogue;

    public ListAllCategoriesHandler(ICatalogueRepository catalogue)
    {
        _catalogue = catalogue;
    }

    public async Task<IReadOnlyList<AdminCategoryDto>> HandleAsync(CancellationToken ct)
    {
        var categories = await _catalogue.GetAllCategoriesAsync(ct);
        return categories.Select(c => c.ToAdminDto()).ToList();
    }
}

/// <summary>Creates a new service category (unique slug).</summary>
public sealed class CreateCategoryHandler
{
    private readonly ICatalogueRepository _catalogue;

    public CreateCategoryHandler(ICatalogueRepository catalogue)
    {
        _catalogue = catalogue;
    }

    public async Task<AdminCategoryDto> HandleAsync(CreateCategoryRequest request, CancellationToken ct)
    {
        var slug = (request.Slug ?? string.Empty).Trim().ToLowerInvariant();
        if (await _catalogue.CategorySlugExistsAsync(slug, ct))
            throw new ConflictException($"A category with slug '{slug}' already exists.");

        var category = ServiceCategory.Create(
            Guid.NewGuid(), slug, request.Name, request.Tint,
            request.SortOrder, request.IsPopular, request.IconKey);

        _catalogue.AddCategory(category);
        await _catalogue.SaveChangesAsync(ct);
        return category.ToAdminDto();
    }
}

/// <summary>Edits a category's display fields (slug is immutable).</summary>
public sealed class UpdateCategoryHandler
{
    private readonly ICatalogueRepository _catalogue;

    public UpdateCategoryHandler(ICatalogueRepository catalogue)
    {
        _catalogue = catalogue;
    }

    public async Task<AdminCategoryDto> HandleAsync(
        Guid id, UpdateCategoryRequest request, CancellationToken ct)
    {
        var category = await _catalogue.FindCategoryByIdForUpdateAsync(id, ct)
            ?? throw new NotFoundException($"Category '{id}' was not found.");

        category.Update(request.Name, request.Tint, request.SortOrder, request.IsPopular, request.IconKey);
        await _catalogue.SaveChangesAsync(ct);
        return category.ToAdminDto();
    }
}

/// <summary>Enables/disables a category (soft show/hide in the marketplace).</summary>
public sealed class SetCategoryActiveHandler
{
    private readonly ICatalogueRepository _catalogue;

    public SetCategoryActiveHandler(ICatalogueRepository catalogue)
    {
        _catalogue = catalogue;
    }

    public async Task<AdminCategoryDto> HandleAsync(Guid id, bool isActive, CancellationToken ct)
    {
        var category = await _catalogue.FindCategoryByIdForUpdateAsync(id, ct)
            ?? throw new NotFoundException($"Category '{id}' was not found.");

        category.SetActive(isActive);
        await _catalogue.SaveChangesAsync(ct);
        return category.ToAdminDto();
    }
}
