using BeybladeMeta.Core.Data;
using BeybladeMeta.Core.Ingestion;
using BeybladeMeta.Core.Parsing;
using BeybladeMeta.Web;
using BeybladeMeta.Web.Components;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Factory registration also registers MetaDbContext itself as a scoped service (used by IngestionService)
builder.Services.AddDbContextFactory<MetaDbContext>(o =>
    o.UseSqlite($"Data Source={Path.Combine(builder.Environment.ContentRootPath, "beyblade-meta.db")}"));
builder.Services.AddSingleton(PartsVocabulary.CreateDefault());
builder.Services.AddSingleton<PostParser>();
builder.Services.AddScoped<IngestionService>();
builder.Services.AddHttpClient<ThreadClient>(http => ThreadClient.ConfigureClient(
        http,
        builder.Configuration["ThreadFetch:UserAgent"],
        builder.Configuration["ThreadFetch:Cookie"]))
    // UseCookies must be off or the handler's cookie container silently drops the manual Cookie header
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false });
builder.Services.AddSingleton(builder.Configuration.GetSection("ThreadIndex").Get<ThreadIndexOptions>() ?? new ThreadIndexOptions());
builder.Services.AddHostedService<ThreadIndexWorker>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<MetaDbContext>().Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
