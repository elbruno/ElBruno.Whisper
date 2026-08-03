using ElBruno.Whisper;
using ElBruno.Whisper.BlazorComponents.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddWhisper();
builder.Services.AddWhisperBlazorComponents();

var app = builder.Build();

app.MapDefaultEndpoints();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<BlazorWhisper.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
