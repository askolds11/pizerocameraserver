using Microsoft.EntityFrameworkCore;
using MQTTnet;
using MudBlazor.Services;
using picamerasserver.Components;
using picamerasserver.Database;
using picamerasserver.Endpoints;
using picamerasserver.mqtt;
using picamerasserver.Options;
using picamerasserver.pizerocamera.manager;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

var logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();
builder.Logging.ClearProviders();
builder.Logging.AddSerilog(logger);

builder.Services.AddLocalization();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

builder.Services.Configure<DirectoriesOptions>(
    builder.Configuration.GetSection(DirectoriesOptions.Directories));
builder.Services.Configure<MqttOptions>(
    builder.Configuration.GetSection(MqttOptions.Mqtt));
builder.Services.Configure<ConnectionStringsOptions>(
    builder.Configuration.GetSection(ConnectionStringsOptions.ConnectionStrings));

var connectionStrings = builder.Configuration
    .GetSection(ConnectionStringsOptions.ConnectionStrings)
    .Get<ConnectionStringsOptions>();
if (connectionStrings == null)
{
    throw new Exception("ConnectionStrings not found");
}

builder.Services.AddDbContextFactory<PiDbContext>(opt =>
    opt.UseNpgsql(connectionStrings.Postgres));

var mqttFactory = new MqttClientFactory();
var mqttClient = mqttFactory.CreateMqttClient();
builder.Services.AddSingleton(mqttClient);
builder.Services.AddSingleton<MqttStuff>();
builder.Services.AddSingleton<PiZeroCameraManager>();

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
app.UseRequestLocalization("lv");
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .DisableAntiforgery();

// Force to instantly create
app.Services.GetRequiredService<MqttStuff>();
app.Services.GetRequiredService<PiZeroCameraManager>();

// Console.WriteLine("Test.");
// await Client_Subscribe_Samples.Subscribe_Topic();

app.AddDownloadUpdateEndpoint();
app.AddUploadImageEndpoint();
app.AddDownloadPicturesEndpoint();

app.Run();