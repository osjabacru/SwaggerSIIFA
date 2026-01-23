
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Portal de APIs SIFFA",
        Version = "v1",
        Description = "Documentación de las APIs SIFFA",
        Contact = new OpenApiContact
        {
            Name = "Equipo de Desarrollo SIFFA",
            Email = "soporte@siffa.com"
        }
    });
});

builder.Services.AddControllers();
builder.Services.AddHttpClient();
var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var path = ctx.File.PhysicalPath ?? "";

        // Desactivar caché SOLO para tu script personalizado
        if (path.EndsWith("swagger-request-interceptor.js", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            ctx.Context.Response.Headers["Pragma"] = "no-cache";
            ctx.Context.Response.Headers["Expires"] = "0";
            Console.WriteLine("🧹 Cache deshabilitado para swagger-request-interceptor.js");
        }
    }
});
//app.UseStaticFiles(); // sirve los .json desde wwwroot/swagger
app.UseAuthorization();
app.MapControllers();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    // c.SwaggerEndpoint("/swagger/contrato.json", "API Contrato v1");
    c.SwaggerEndpoint("/swagger/contratoV103.json", "API Contrato v103");
    
    c.SwaggerEndpoint("/swagger/factura.json", "API Factura v1");
    c.SwaggerEndpoint("/swagger/seguridad.json", "API Seguridad v1");

    c.RoutePrefix = string.Empty;

    c.ConfigObject.AdditionalItems["persistAuthorization"] = false;
    c.ConfigObject.AdditionalItems["persistConfig"] = false;

    c.InjectJavascript("/swagger/swagger-request-interceptor.js", "text/javascript");
    c.InjectJavascript("/swagger/swagger-reapply.js", "text/javascript");
});

app.Run();