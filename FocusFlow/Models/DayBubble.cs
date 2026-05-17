using CommunityToolkit.Mvvm.ComponentModel;

namespace FocusFlow.Models;


public partial class DayBubble : ObservableObject
{
    public string Name { get; set; } 
    public string NumberValue { get; set; }

    [ObservableProperty]
    private bool isSelected;
}