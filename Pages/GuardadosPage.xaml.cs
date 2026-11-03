using BogoMaps.Services;
using BogoMaps.Models;
namespace BogoMaps.Pages;
public partial class GuardadosPage : BogoMaps.Controls.StitchPage
{
    private ReportCursor? next;
    private List<PedestrianReport> rows = [];
    private bool refreshing;
    public GuardadosPage() => InitializeComponent();
    protected override async void OnAppearing() { base.OnAppearing(); await RefreshAsync(false); }
    private async void OnMoreSavedClicked(object? sender, EventArgs e) => await RefreshAsync(true);
    private async Task RefreshAsync(bool more)
    {
        if (refreshing) return;
        refreshing = true; MoreSavedButton.IsEnabled = false;
        var startScope = AuthenticationService.Instance.Current?.UserId;
        if (!more) { rows = []; ReportList.Children.Clear(); next = null; MoreSavedButton.IsVisible = false; }
        SavedNotice.Text = "Actualizando guardados…";
        try
        {
            var page = await ReportData.Instance.FeedAsync(more ? next : null, favorites: true);
            rows = more ? rows.Concat(page.Reports).DistinctBy(r => r.Id).ToList() : page.Reports;
            next = page.Next;
            MoreSavedButton.IsVisible = next != null;
            SavedNotice.Text = ReportData.Instance.Notice;
            ReportCards.Fill(ReportList, rows.Where(r => r.Saved));
        }
        catch (Exception ex) { await DisplayAlert("Guardados", ex is CommunityException or FirebaseAuthException ? ex.Message : "No fue posible actualizar tus guardados.", "OK"); }
        finally
        {
            refreshing = false; MoreSavedButton.IsEnabled = true;
            if (AuthenticationService.Instance.Current?.UserId != startScope) { rows = []; ReportList.Children.Clear(); MoreSavedButton.IsVisible = false; }
        }
    }
}
