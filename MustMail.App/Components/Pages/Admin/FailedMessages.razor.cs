namespace MustMail.App.Components.Pages.Admin;

public class FailedMessagesBase : ComponentBase
{
    // Page variables
    protected MudDataGrid<Message> Grid = null!;

    // Class variables
    private string _searchString = null!;

    // Component parameters and dependency injection
    [Inject] public IDbContextFactory<DatabaseContext> DbFactory { get; set; } = null!;

    // Load failed messages - used by mud data grid to load the data server side using pagination and supporting search
    protected async Task<GridData<Message>> ServerReload(GridState<Message> state, CancellationToken token)
    {
        await using DatabaseContext dbContext = await DbFactory.CreateDbContextAsync(token);
        var query = dbContext.Message.Where(m => m.DeliveryFailed);

        if (!string.IsNullOrWhiteSpace(_searchString))
            query = query.Where(m => m.SenderEmail.Contains(_searchString)
                                  || m.Subject.Contains(_searchString));

        int total = await query.CountAsync(cancellationToken: token);

        List<Message> items = await query
            .OrderByDescending(m => m.Timestamp)
            .Skip(state.Page * state.PageSize)
            .Take(state.PageSize)
            .ToListAsync(cancellationToken: token);

        return new GridData<Message> { Items = items, TotalItems = total };
    }

    // On Search - On search string changed reload the server data with the search string
    protected Task OnSearch(string text)
    {
        _searchString = text;
        return Grid.ReloadServerData();
    }
}
