using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerUI;

var builder = WebApplication.CreateBuilder(args);

// ===============================
// Servicios
// ===============================
builder.Services.AddControllers();

builder.Services.AddHttpClient("proxy", client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    // Permite certificados no válidos (común en entornos de preproducción)
    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

builder.Services.AddSwaggerGen();

var app = builder.Build();

// ===============================
// Middleware
// ===============================
app.UseCors("AllowAll");
app.UseStaticFiles();
app.UseAuthorization();
app.MapControllers();

app.UseSwagger();

// ===============================
// Swagger UI Configurado para Proxy
// ===============================
app.UseSwaggerUI(c =>
{
    // Cambiamos las rutas para que apunten a los archivos FÍSICOS en wwwroot
    c.SwaggerEndpoint("/swagger/contrato.json", "API Contrato");
    c.SwaggerEndpoint("/swagger/factura.json", "API Factura");
    c.SwaggerEndpoint("/swagger/seguridad.json", "API Seguridad");

    c.RoutePrefix = "swagger";
    c.InjectJavascript("/swagger/swagger-proxy.js");
});

app.Run();