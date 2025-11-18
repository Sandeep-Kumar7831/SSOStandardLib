using System.Diagnostics;

namespace ExternStandardApp.Pages;
public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;

    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Debug.WriteLine("[MainPage] OnAppearing - auto-reading tokens");
        await _viewModel.ReadTokensCommand.ExecuteAsync(null);
    }
}
