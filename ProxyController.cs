using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text;

[ApiController]
[Route("proxy/{context}/{**path}")]
public class ProxyController : ControllerBase
{
    private readonly HttpClient _httpClient;

    public ProxyController(IHttpClientFactory factory)
    {
        _httpClient = factory.CreateClient();
    }

    [HttpGet]
    [HttpPost]
    [HttpPut]
    [HttpDelete]
    public async Task<IActionResult> Forward(string context, string path)
    {
        try
        {
            // 1️⃣ Determinar base según contexto
            string baseUrl = context.ToLower() switch
            {
                "contrato" => "https://siifa.sispropreprod.gov.co/siifacon",
                "factura" => "https://siifa.sispropreprod.gov.co/siifafa",
                "seguridad" => "https://siifa.sispropreprod.gov.co/siifaseg",
                _ => throw new Exception($"Contexto desconocido: {context}")
            };

            // 2️⃣ Obtener el path real de la solicitud (no el parámetro)
            var fullPath = Request.Path.Value ?? "";
            var original = fullPath;

            Console.WriteLine($"➡️ Request.Path: {fullPath}");
            Console.WriteLine($"➡️ Param 'path': {path ?? "(null)"}");

            baseUrl = path.ToLower() switch
            {
                var p when p.Contains("siifacon") => "https://siifa.sispropreprod.gov.co/siifacon",
                var p when p.Contains("siifafa") => "https://siifa.sispropreprod.gov.co/siifafa",
                var p when p.Contains("siifaseg") => "https://siifa.sispropreprod.gov.co/siifaseg",
                var p when p.Contains("contrato") => "https://siifa.sispropreprod.gov.co/siifacon",
                _ => throw new Exception($"Contexto desconocido: {path}")
            };

            // 3️⃣ Limpiar completamente cualquier prefijo duplicado
            // Quitar "/proxy/{context}" inicial y todo lo que haya antes del endpoint real
            // Ejemplo: "/proxy/seguridad/proxy/seguridad/siifaseg/api/Auth/login"
            // → "api/Auth/login"
            string pattern = @$"proxy/{context}/";
            int idx = fullPath.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                // Cortar desde después de la última ocurrencia de proxy/{context}/
                int lastIdx = fullPath.LastIndexOf(pattern, StringComparison.OrdinalIgnoreCase);
                fullPath = fullPath.Substring(lastIdx + pattern.Length);
            }

            // Quitar otros prefijos comunes (siifaseg/, siifacon/, siifafa/, proxy/)
            string[] prefixes = { "siifaseg/", "siifacon/", "siifafa/", "proxy/" };
            foreach (var pfx in prefixes)
            {
                if (fullPath.StartsWith(pfx, StringComparison.OrdinalIgnoreCase))
                {
                    fullPath = fullPath.Substring(pfx.Length);
                    break;
                }
            }

            if (fullPath.Contains("/siifacon/", StringComparison.OrdinalIgnoreCase))
                fullPath = fullPath.Substring(fullPath.IndexOf("/siifacon/", StringComparison.OrdinalIgnoreCase) + "/siifacon/".Length);

            else if (fullPath.Contains("/siifafa/", StringComparison.OrdinalIgnoreCase))
                fullPath = fullPath.Substring(fullPath.IndexOf("/siifafa/", StringComparison.OrdinalIgnoreCase) + "/siifafa/".Length);

            else if (fullPath.Contains("/siifaseg/", StringComparison.OrdinalIgnoreCase))
                fullPath = fullPath.Substring(fullPath.IndexOf("/siifaseg/", StringComparison.OrdinalIgnoreCase) + "/siifaseg/".Length);

            // Limpiar barras redundantes
            fullPath = fullPath.TrimStart('/').Replace("//", "/");

            // 4️⃣ Armar URL destino
            var queryString = Request.QueryString.HasValue ? Request.QueryString.Value : "";
            var targetUrl = $"{baseUrl}/{fullPath}{queryString}"
                .Replace(":/", "://")
                .Replace("//", "/");

            Console.WriteLine($"🔹 [{context}] Limpieza: '{original}' → '{fullPath}'");
            Console.WriteLine($"🔹 [{context}] Reenviando solicitud a: {targetUrl}");

            // 5️⃣ Crear solicitud HTTP hacia el destino
            var request = new HttpRequestMessage(new HttpMethod(Request.Method), targetUrl);

            // Copiar el cuerpo si aplica
            if (Request.ContentLength > 0)
            {
                using var reader = new StreamReader(Request.Body, Encoding.UTF8);
                var body = await reader.ReadToEndAsync();
                request.Content = new StringContent(body, Encoding.UTF8, Request.ContentType ?? "application/json");
            }

            // Copiar headers excepto Host
            foreach (var header in Request.Headers)
            {
                if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()) && request.Content != null)
                    request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }

            request.Headers.UserAgent.ParseAdd("SwaggerProxy/1.0");

            // 6️⃣ Enviar la solicitud al backend
            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            Response.StatusCode = (int)response.StatusCode;
            Response.Headers["X-Proxied-To"] = targetUrl;

            Console.WriteLine($"✅ [{context}] Respuesta {(int)response.StatusCode}");
            return Content(responseContent, response.Content.Headers.ContentType?.ToString() ?? "application/json");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error en proxy ({context}/{path}): {ex.Message}");
            return StatusCode(500, $"Proxy error: {ex.Message}");
        }
    }
}