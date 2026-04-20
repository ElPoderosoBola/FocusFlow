using FocusFlow.ViewModels;

namespace FocusFlow
{
    public partial class MainPage : ContentPage
    {
        int count = 0;
        private readonly MainViewModel _viewModel;

        public MainPage(MainViewModel viewModel)
        {
            InitializeComponent();

            // Asigna el ViewModel a la vista para el enlace de datos (Binding).
            _viewModel = viewModel;
            BindingContext = _viewModel;

            // Inicializa el ViewModel cuando la página ya está preparada.
            _ = _viewModel.InitializeAsync();
        }

        private void OnCounterClicked(object? sender, EventArgs e)
        {
            count++;

            if (count == 1)
                CounterBtn.Text = $"Clicked {count} time";
            else
                CounterBtn.Text = $"Clicked {count} times";

            SemanticScreenReader.Announce(CounterBtn.Text);
        }
    }
}
