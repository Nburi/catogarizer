using Catogarizer.Core.Models;

namespace Catogarizer.App.ViewModels;

public sealed class CategoryViewModel
{
    public Category Model { get; }

    public Guid Id => Model.Id;
    public string Name => Model.Name;

    public CategoryViewModel(Category model)
    {
        Model = model;
    }
}
