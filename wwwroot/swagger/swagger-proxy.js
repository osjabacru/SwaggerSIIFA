(function () {
    function activateProxy() {
        if (!window.ui) {
            setTimeout(activateProxy, 200);
            return;
        }

        const ui = window.ui;

        ui.getConfigs().requestInterceptor = (req) => {
            // 1. REGLA DE EXCEPCIÓN: Si la petición es para descargar el JSON de definición,
            // NO hacer nada. Dejar que cargue desde /swagger/contrato.json, etc.
            if (req.url.endsWith(".json")) {
                return req;
            }

            const PROXY_BASE = window.location.origin + "/proxy";

            // 2. Si ya tiene el proxy en la URL, no volver a procesar
            if (req.url.startsWith(PROXY_BASE)) return req;

            // 3. Obtener el archivo cargado actualmente
            const state = ui.getState().toJS();
            const currentDefinition = state.spec.url || "";

            let apiId = "";
            if (currentDefinition.includes("contrato.json")) apiId = "siifacon";
            else if (currentDefinition.includes("factura.json")) apiId = "siifafa";
            else if (currentDefinition.includes("seguridad.json")) apiId = "siifaseg";

            if (apiId) {
                const urlObj = new URL(req.url, window.location.origin);
                let path = urlObj.pathname;

                // 4. LÓGICA DE DEDUPLICACIÓN (Como lo pediste)
                // Si el path ya trae el contexto (ej: /siifaseg), lo limpiamos
                const searchString = `/${apiId}`;
                if (path.startsWith(searchString)) {
                    path = path.substring(searchString.length);
                }

                // Aseguramos que el path empiece con /
                if (!path.startsWith("/")) path = "/" + path;

                // 5. Construcción de la URL hacia tu Proxy

                const newUrl = `${PROXY_BASE}/${apiId}${path}${urlObj.search}`;

                console.log(`[Proxy ${apiId}] Redirigiendo método: ${newUrl}`);
                req.url = newUrl;
            }

            return req;
        };

        console.log("Interceptor corregido: Definiciones JSON permitidas.");
    }

    activateProxy();
})();