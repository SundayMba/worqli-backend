using Servika.Application.Abstractions.Persistence;
using Servika.Contracts.Catalogue;

namespace Servika.Application.Catalogue;

/// <summary>Lists the active service categories in display order.</summary>
public sealed class GetCategoriesHandler
{
    private readonly ICatalogueRepository _catalogue;

    public GetCategoriesHandler(ICatalogueRepository catalogue)
    {
        _catalogue = catalogue;
    }

    public async Task<IReadOnlyList<CategoryDto>> HandleAsync(CancellationToken ct)
    {
        var categories = await _catalogue.GetCategoriesAsync(ct);
        return categories.Select(c => c.ToDto()).ToList();
    }
}
