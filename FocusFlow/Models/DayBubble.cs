using CommunityToolkit.Mvvm.ComponentModel;

namespace FocusFlow.Models;

// ¡Nuestra burbuja de luz!
public partial class DayBubble : ObservableObject
{
    public string Name { get; set; } // La letra que se ve: "L", "M", "X"...
    public string NumberValue { get; set; } // Lo que entiende el código por debajo: "1", "2"...

    [ObservableProperty]
    private bool isSelected; // ¿Está iluminada?
}