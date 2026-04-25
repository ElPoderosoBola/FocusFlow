using FocusFlow.ViewModels;

namespace FocusFlow
{
    public partial class MainPage : ContentPage
    {
        private readonly MainViewModel _viewModel;

        public MainPage(MainViewModel viewModel)
        {
            InitializeComponent();

            // Enchufamos el Titiritero a la Pantalla
            _viewModel = viewModel;
            BindingContext = _viewModel;

            // Cargamos los datos de la base de datos SQLite al arrancar
            _ = _viewModel.InitializeAsync();
        }
    }
}