using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using MQTTnet;
using MudBlazor;
using MudBlazor.Services;
using picamerasserver.Components;
using picamerasserver.Database;
using picamerasserver.Endpoints;
using picamerasserver.mqtt;
using picamerasserver.Options;
using picamerasserver.pizerocamera;
using picamerasserver.pizerocamera.manager;
using picamerasserver.pizerocamera.TakePicture;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

var logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();
builder.Logging.ClearProviders();
builder.Logging.AddSerilog(logger);

builder.Services.AddLocalization();

// Add services to the container.
builder.Services.AddRazorComponents(options => options.DetailedErrors = true)
    .AddInteractiveServerComponents();

builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomLeft;
    config.SnackbarConfiguration.HideTransitionDuration = 500;
    config.SnackbarConfiguration.ShowTransitionDuration = 500;
});

builder.Services.Configure<DirectoriesOptions>(
    builder.Configuration.GetSection(DirectoriesOptions.Directories));
builder.Services.Configure<MqttOptions>(
    builder.Configuration.GetSection(MqttOptions.Mqtt));
builder.Services.Configure<ConnectionStringsOptions>(
    builder.Configuration.GetSection(ConnectionStringsOptions.ConnectionStrings));
builder.Services.Configure<ServerOptions>(
    builder.Configuration.GetSection(ServerOptions.Server));
builder.Services.Configure<SmbOptions>(
    builder.Configuration.GetSection(SmbOptions.Smb));

var connectionStrings = builder.Configuration
    .GetSection(ConnectionStringsOptions.ConnectionStrings)
    .Get<ConnectionStringsOptions>();
if (connectionStrings == null)
{
    throw new Exception("ConnectionStrings not found");
}

builder.Services.AddPooledDbContextFactory<PiDbContext>(opt =>
    opt.UseNpgsql(connectionStrings.Postgres));

var mqttFactory = new MqttClientFactory();
var mqttClient = mqttFactory.CreateMqttClient();
builder.Services.AddSingleton(mqttClient);
builder.Services.AddSingleton<MqttStuff>();
builder.Services.AddSingleton<ChangeListener>();
builder.Services.AddSingleton<PiZeroCameraManager>();
builder.Services.AddSingleton<ISendPictureManager>(sp => sp.GetRequiredService<PiZeroCameraManager>());
builder.Services.AddSingleton<ITakePictureManager, TakePicture>();
builder.Services.AddSingleton<ISendPictureSetManager>(sp => sp.GetRequiredService<PiZeroCameraManager>());
builder.Services.AddSingleton<UploadToServer>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// app.UseHttpsRedirection();

// app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .DisableAntiforgery();

app.UseRequestLocalization(options =>
{
    var cinfo = CultureInfo.GetCultures(CultureTypes.AllCultures & ~CultureTypes.NeutralCultures);
    var supportedCultures = cinfo.Select(t => t.Name).Distinct().ToArray();
    options.AddSupportedCultures(supportedCultures)
        .AddSupportedUICultures(supportedCultures)
        .DefaultRequestCulture = new RequestCulture(
        culture: "lv-LV",
        uiCulture: "en");
});

// Force to instantly create
app.Services.GetRequiredService<MqttStuff>();
app.Services.GetRequiredService<ChangeListener>();
app.Services.GetRequiredService<PiZeroCameraManager>();

// Console.WriteLine("Test.");
// await Client_Subscribe_Samples.Subscribe_Topic();

app.AddDownloadUpdateEndpoint();
app.AddUploadImageEndpoint();
app.AddDownloadPicturesEndpoint();
app.AddDownloadPictureSetEndpoint();

app.Run();