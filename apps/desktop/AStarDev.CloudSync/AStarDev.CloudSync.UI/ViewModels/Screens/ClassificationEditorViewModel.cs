using System.Collections.ObjectModel;
using AStarDev.CloudSync.IPC;
using AStarDev.CloudSync.UI.Localisation;
using AStarDev.FunctionalParadigm;
using CommunityToolkit.Mvvm.Input;

namespace AStarDev.CloudSync.UI.ViewModels.Screens;

/// <summary>
/// The forms of the Classifications screen: add, rename and remove a category, and add, edit and remove a term. Every change is a command to the daemon, which words a refusal for the user; the form shows it and keeps what was typed.
/// <see cref="Changed"/> is raised after the daemon accepted a change, so the screen can read the terms again.
/// </summary>
internal sealed class ClassificationEditorViewModel : ViewModelBase
{
    private readonly ILocalisationService localisation;
    private readonly Func<IIpcClient> createClient;
    private CategoryRowViewModel? selectedCategory;
    private TermRowViewModel? selectedTerm;
    private string newCategoryName = string.Empty;
    private string renameText = string.Empty;
    private string newTermText = string.Empty;
    private CategoryRowViewModel? newTermCategory;
    private string editText = string.Empty;
    private bool editIgnored;
    private CategoryRowViewModel? editCategory;
    private string? errorMessage;

    /// <summary>Creates the forms with no categories until the screen reads them.</summary>
    /// <param name="localisation">Supplies every text.</param>
    /// <param name="createClient">Creates a fresh client per change; one client is one connection.</param>
    public ClassificationEditorViewModel(ILocalisationService localisation, Func<IIpcClient> createClient)
    {
        this.localisation = localisation;
        this.createClient = createClient;
        AddCategoryCommand = new AsyncRelayCommand(AddCategoryAsync);
        RenameCategoryCommand = new AsyncRelayCommand(RenameCategoryAsync);
        DeleteCategoryCommand = new AsyncRelayCommand(DeleteCategoryAsync);
        AddTermCommand = new AsyncRelayCommand(AddTermAsync);
        SaveTermCommand = new AsyncRelayCommand(SaveTermAsync);
        DeleteTermCommand = new AsyncRelayCommand(DeleteTermAsync);
    }

    /// <summary>Raised after the daemon accepted a change.</summary>
    public event Action? Changed;

    /// <summary>Every category, for the pickers and the category forms.</summary>
    public ObservableCollection<CategoryRowViewModel> Categories { get; } = [];

    /// <summary>The category the rename and remove buttons act on. Choosing one copies its name into <see cref="RenameText"/>.</summary>
    public CategoryRowViewModel? SelectedCategory
    {
        get => selectedCategory;
        set
        {
            if (!SetProperty(ref selectedCategory, value)) return;

            RenameText = value?.Name ?? string.Empty;
            OnPropertyChanged(nameof(HasSelectedCategory));
        }
    }

    /// <summary>True when a category is chosen in the category forms.</summary>
    public bool HasSelectedCategory => selectedCategory is not null;

    /// <summary>The name for the category to add.</summary>
    public string NewCategoryName
    {
        get => newCategoryName;
        set => SetProperty(ref newCategoryName, value);
    }

    /// <summary>The new name for <see cref="SelectedCategory"/>.</summary>
    public string RenameText
    {
        get => renameText;
        set => SetProperty(ref renameText, value);
    }

    /// <summary>The text of the term to add.</summary>
    public string NewTermText
    {
        get => newTermText;
        set => SetProperty(ref newTermText, value);
    }

    /// <summary>The category the term to add goes into.</summary>
    public CategoryRowViewModel? NewTermCategory
    {
        get => newTermCategory;
        set => SetProperty(ref newTermCategory, value);
    }

    /// <summary>The term the edit form acts on; <see langword="null"/> until one is chosen in the terms list.</summary>
    public TermRowViewModel? SelectedTerm => selectedTerm;

    /// <summary>True when a term is chosen for editing.</summary>
    public bool HasSelectedTerm => selectedTerm is not null;

    /// <summary>The new text of the edited term.</summary>
    public string EditText
    {
        get => editText;
        set => SetProperty(ref editText, value);
    }

    /// <summary>The category the edited term moves to.</summary>
    public CategoryRowViewModel? EditCategory
    {
        get => editCategory;
        set => SetProperty(ref editCategory, value);
    }

    /// <summary>Whether the edited term is ignored, so it classifies nothing.</summary>
    public bool EditIgnored
    {
        get => editIgnored;
        set => SetProperty(ref editIgnored, value);
    }

    /// <summary>Why the last change failed, worded for the user. <see langword="null"/> after one that worked.</summary>
    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    /// <summary>Adds the category named in <see cref="NewCategoryName"/>.</summary>
    public IAsyncRelayCommand AddCategoryCommand { get; }

    /// <summary>Gives <see cref="SelectedCategory"/> the name in <see cref="RenameText"/>.</summary>
    public IAsyncRelayCommand RenameCategoryCommand { get; }

    /// <summary>Removes <see cref="SelectedCategory"/>, which the daemon refuses while it holds terms.</summary>
    public IAsyncRelayCommand DeleteCategoryCommand { get; }

    /// <summary>Adds the term in <see cref="NewTermText"/> to <see cref="NewTermCategory"/>.</summary>
    public IAsyncRelayCommand AddTermCommand { get; }

    /// <summary>Saves the edit form to <see cref="SelectedTerm"/>.</summary>
    public IAsyncRelayCommand SaveTermCommand { get; }

    /// <summary>Removes <see cref="SelectedTerm"/> and its links.</summary>
    public IAsyncRelayCommand DeleteTermCommand { get; }

    /// <summary>Label of the add category button.</summary>
    public string AddCategoryText => localisation.Get("Classifications.Editor.AddCategory");

    /// <summary>Label of the rename category button.</summary>
    public string RenameCategoryText => localisation.Get("Classifications.Editor.RenameCategory");

    /// <summary>Label of the remove category button.</summary>
    public string DeleteCategoryText => localisation.Get("Classifications.Editor.DeleteCategory");

    /// <summary>Label of the add term button.</summary>
    public string AddTermText => localisation.Get("Classifications.Editor.AddTerm");

    /// <summary>Label of the save term button.</summary>
    public string SaveTermText => localisation.Get("Classifications.Editor.SaveTerm");

    /// <summary>Label of the remove term button.</summary>
    public string DeleteTermText => localisation.Get("Classifications.Editor.DeleteTerm");

    /// <summary>Label of the ignored check box.</summary>
    public string IgnoredText => localisation.Get("Classifications.Editor.Ignored");

    /// <summary>Hint inside the new category box.</summary>
    public string NewCategoryHint => localisation.Get("Classifications.Editor.NewCategoryHint");

    /// <summary>Hint inside the new term box.</summary>
    public string NewTermHint => localisation.Get("Classifications.Editor.NewTermHint");

    /// <summary>Replaces the categories with those the daemon sent, keeping the choices that still exist.</summary>
    /// <param name="categories">Every category.</param>
    public void SetCategories(IEnumerable<ClassificationCategoryView> categories)
    {
        var selected = selectedCategory?.Id;
        var newCategory = newTermCategory?.Id;
        var edit = editCategory?.Id;
        Categories.Clear();
        foreach (var category in categories) Categories.Add(new CategoryRowViewModel(category.Id, category.Name));

        SelectedCategory = Categories.FirstOrDefault(category => category.Id == selected);
        NewTermCategory = Categories.FirstOrDefault(category => category.Id == newCategory) ?? Categories.FirstOrDefault();
        EditCategory = Categories.FirstOrDefault(category => category.Id == edit);
    }

    /// <summary>Starts the edit form from a term, or clears it when <paramref name="term"/> is <see langword="null"/>.</summary>
    /// <param name="term">The term chosen in the terms list.</param>
    public void ShowTerm(TermRowViewModel? term)
    {
        selectedTerm = term;
        EditText = term?.Text ?? string.Empty;
        EditIgnored = term?.Ignored ?? false;
        EditCategory = Categories.FirstOrDefault(category => category.Id == term?.CategoryId);
        OnPropertyChanged(nameof(SelectedTerm));
        OnPropertyChanged(nameof(HasSelectedTerm));
    }

    private Task AddCategoryAsync(CancellationToken cancellationToken)
        => RunAsync(new CreateCategory(NewCategoryName), () => NewCategoryName = string.Empty, cancellationToken);

    private Task RenameCategoryAsync(CancellationToken cancellationToken)
        => selectedCategory is { } category ? RunAsync(new RenameCategory(category.Id, RenameText), () => { }, cancellationToken) : Task.CompletedTask;

    private Task DeleteCategoryAsync(CancellationToken cancellationToken)
        => selectedCategory is { } category ? RunAsync(new DeleteCategory(category.Id), () => SelectedCategory = null, cancellationToken) : Task.CompletedTask;

    private Task AddTermAsync(CancellationToken cancellationToken)
        => newTermCategory is { } category ? RunAsync(new CreateTerm(NewTermText, category.Id), () => NewTermText = string.Empty, cancellationToken) : Task.CompletedTask;

    private Task SaveTermAsync(CancellationToken cancellationToken)
        => selectedTerm is { } term && editCategory is { } category ? RunAsync(new UpdateTerm(term.Id, EditText, category.Id, EditIgnored), () => { }, cancellationToken) : Task.CompletedTask;

    private Task DeleteTermAsync(CancellationToken cancellationToken)
        => selectedTerm is { } term ? RunAsync(new DeleteTerm(term.Id), () => ShowTerm(null), cancellationToken) : Task.CompletedTask;

    private async Task RunAsync(ICommand command, Action onAccepted, CancellationToken cancellationToken)
    {
        switch (await ClassificationRequests.SendAsync(createClient, command, localisation.Get("Classifications.Editor.Refused"), cancellationToken))
        {
            case Ok<Unit>:
                ErrorMessage = null;
                onAccepted();
                Changed?.Invoke();
                break;
            case Fail<ConnectFailure> fail:
                ErrorMessage = fail.Error.Message;
                break;
        }
    }
}
