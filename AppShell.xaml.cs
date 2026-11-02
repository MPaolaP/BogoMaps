namespace BogoMaps
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute("detalle", typeof(Pages.DetallePuntoPage));
        }
    }
}
