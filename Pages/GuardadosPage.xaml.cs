using BogoMaps.Services;
namespace BogoMaps.Pages;
public partial class GuardadosPage : ContentPage
{
    public GuardadosPage() => InitializeComponent();
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { ReportCards.Fill(ReportList, (await LocalRepository.Instance.ReadAsync()).Where(r => r.Saved)); }
        catch (Exception) { await DisplayAlert("Guardados", "No fue posible leer los datos locales.", "OK"); }
    }
}
