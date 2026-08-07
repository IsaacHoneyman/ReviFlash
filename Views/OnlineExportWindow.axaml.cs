using Avalonia;
using Avalonia.Controls;
using ReviFlash.ViewModels;
using System.ComponentModel;

namespace ReviFlash.Views;

public partial class OnlineExportWindow : Window
{
    public OnlineExportWindow()
    {
        InitializeComponent();
        
        var vm = new OnlineExportViewModel();
        DataContext = vm;

        vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OnlineExportViewModel.IsAuthenticated) && sender is OnlineExportViewModel vm)
        {
            double oldWidth = vm.IsAuthenticated ? 450 : 1100;
            double newWidth = vm.IsAuthenticated ? 1100 : 450;
            
            double oldHeight = vm.IsAuthenticated ? 550 : 720;
            double newHeight = vm.IsAuthenticated ? 720 : 550;

            double widthDiff = newWidth - oldWidth;
            double heightDiff = newHeight - oldHeight;

            double scaling = RenderScaling;
            
            int shiftX = (int)((widthDiff / 2) * scaling);
            int shiftY = (int)((heightDiff / 2) * scaling);

            Position = new PixelPoint(Position.X - shiftX, Position.Y - shiftY);
        }
    }
}