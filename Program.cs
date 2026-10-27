using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.FileProviders;
using SkyNet;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 110L * 1024 * 1024);
builder.Services.Configure<IISServerOptions>(o => o.MaxRequestBodySize = 110L * 1024 * 1024);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 110L * 1024 * 1024);

var app = builder.Build();
PropertyManagement.Core.Local.Accessor = app.Services.GetRequiredService<IHttpContextAccessor>();
PropertyManagement.Core.Init.OnStart().GetAwaiter().GetResult();
app.Use(async (ctx, next) =>
{
    if (HttpMethods.IsPost(ctx.Request.Method) && ctx.Request.Path.Equals("/stripe-webhook", StringComparison.OrdinalIgnoreCase))
    {
        await PropertyManagement.Core.Pay.Webhook(ctx);
        return;
    }
    await next();
});
string videoRoot = Path.Combine(PropertyManagement.Core.Store.Root, "images");
if (Directory.Exists(videoRoot))
{
    app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/images") && (ctx.Request.Path.Value ?? string.Empty).EndsWith(".mp4", StringComparison.OrdinalIgnoreCase), branch => branch.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(videoRoot), RequestPath = "/images" }));
}
app.UseMiddleware<IHandler>();

app.UseStaticHttpCurrent();

app.Run();
