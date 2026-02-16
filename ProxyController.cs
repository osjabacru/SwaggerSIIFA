using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("proxy")]
public class ProxyController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public ProxyController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    [Route("{apiId}/{*path}")]
    public async Task ProxyRequest(string apiId, string path)
    {
        // 1. Obtener la URL base desde appsettings.json
        var baseUrl = _configuration.GetValue<string>($"SwaggerServers:{apiId}");

        if (string.IsNullOrEmpty(baseUrl))
        {
            Response.StatusCode = 404;
            await Response.WriteAsync($"Configuración para '{apiId}' no encontrada.");
            return;
        }

        var client = _httpClientFactory.CreateClient("proxy");

        // 2. Construir la URL destino de forma limpia
        var targetUrl = $"{baseUrl.TrimEnd('/')}/{path.TrimStart('/')}{Request.QueryString}";

        // 3. Crear el mensaje de solicitud con el método original (GET, POST, etc.)
        var requestMessage = new HttpRequestMessage(new HttpMethod(Request.Method), targetUrl);

        // 4. Copiar el Body si la petición lo requiere (POST, PUT, PATCH)
        if (Request.ContentLength > 0 || Request.Headers.ContainsKey("Transfer-Encoding"))
        {
            // Importante: No cerramos el stream del body para que se pueda copiar
            requestMessage.Content = new StreamContent(Request.Body);

            // Copiar el Content-Type específico
            if (!string.IsNullOrEmpty(Request.ContentType))
            {
                requestMessage.Content.Headers.TryAddWithoutValidation("Content-Type", Request.ContentType);
            }
        }

        // 5. Copiar los Headers (Authorization, Accept, etc.)
        foreach (var header in Request.Headers)
        {
            // Evitamos copiar headers que el HttpClient gestiona automáticamente o que causan conflicto
            if (!header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase) &&
                !header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
                {
                    requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }
            }
        }

        try
        {
            // 6. Enviar la petición al servidor de SISPRO
            using var responseMessage = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);

            // 7. Copiar la respuesta del servidor remoto a nuestra respuesta local
            Response.StatusCode = (int)responseMessage.StatusCode;

            // Copiar encabezados de respuesta
            foreach (var header in responseMessage.Headers)
            {
                Response.Headers[header.Key] = header.Value.ToArray();
            }
            foreach (var header in responseMessage.Content.Headers)
            {
                Response.Headers[header.Key] = header.Value.ToArray();
            }

            // Eliminar este header para evitar problemas de compresión/transmisión
            Response.Headers.Remove("transfer-encoding");

            // Copiar el contenido de la respuesta (el JSON de respuesta de SISPRO)
            await responseMessage.Content.CopyToAsync(Response.Body);
        }
        catch (Exception ex)
        {
            Response.StatusCode = 500;
            await Response.WriteAsync($"Error en el Proxy: {ex.Message}");
        }
    }
}