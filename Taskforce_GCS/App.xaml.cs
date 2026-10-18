using System;
using System.Windows;

namespace Taskforce_GCS;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            MessageBox.Show($"Error no controlado:\n{args.ExceptionObject}", "Error en Taskforce GCS", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        DispatcherUnhandledException += (s, args) =>
        {
            MessageBox.Show($"Error en hilo de interfaz:\n{args.Exception.Message}", "Error en Taskforce GCS", MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true; // Evitar que la aplicación se cierre bruscamente
        };
    }
}
