using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VocalEye.Desktop.ApiClient;
using VocalEye.Desktop.App.ViewModels;
using VocalEye.Desktop.App.Views;
using VocalEye.Desktop.Core.Interfaces;
using VocalEye.Desktop.Hosting;
namespace VocalEye.Desktop.App;
public partial class App : Application
{
 private IHost? _host;
 protected override async void OnStartup(StartupEventArgs e)
 {
  base.OnStartup(e);
  try {
   _host = Host.CreateDefaultBuilder().ConfigureAppConfiguration((_,cfg)=>cfg.AddJsonFile("appsettings.json", optional:true, reloadOnChange:true))
   .ConfigureServices((ctx,services)=>{
    services.Configure<BackendApiOptions>(ctx.Configuration.GetSection(BackendApiOptions.SectionName));
    services.Configure<BackendProcessOptions>(ctx.Configuration.GetSection(BackendProcessOptions.SectionName));
    services.AddHttpClient<IBackendConnection,BackendConnection>();
    services.AddSingleton<BackendProcessManager>(); services.AddSingleton<MainViewModel>(); services.AddSingleton<MainWindow>();
   }).Build();
   await _host.StartAsync(); var window=_host.Services.GetRequiredService<MainWindow>(); MainWindow=window; window.Show();
  } catch(Exception ex) { MessageBox.Show($"VocalEye Desktop could not start.\n\n{ex.Message}","Startup error",MessageBoxButton.OK,MessageBoxImage.Error); Shutdown(-1); }
 }
 protected override async void OnExit(ExitEventArgs e) { if(_host is not null) { try { await _host.Services.GetRequiredService<BackendProcessManager>().StopAsync(); await _host.StopAsync(); } finally { _host.Dispose(); } } base.OnExit(e); }
}
