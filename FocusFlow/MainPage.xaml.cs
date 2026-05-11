using FocusFlow.ViewModels;

namespace FocusFlow
{
    public partial class MainPage : ContentPage
    {
        private readonly MainViewModel _viewModel;

        public MainPage(MainViewModel viewModel)
        {
            InitializeComponent();

            // Linkeamos el viewmodel a la view
            _viewModel = viewModel;
            BindingContext = _viewModel;

            // Cargamos los datos de la base de datos SQLite al arrancar
            _ = _viewModel.InitializeAsync();
        }
    }
}