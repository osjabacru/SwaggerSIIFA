(() => {
    console.log("%c[Swagger Redirect] Script cargado correctamente ✅", "color: lime; font-weight:bold;");

    // Función principal de inicio
    const init = () => {
        waitForSwagger(() => {
            attachInterceptor();
            watchForDefinitionChanges();
        });
    };

    // Esperar a que Swagger UI esté completamente inicializado
    function waitForSwagger(callback) {
        const check = setInterval(() => {
            if (window.ui && typeof window.ui.getSystem === "function") {
                clearInterval(check);
                console.log("%c[Swagger Redirect] Swagger listo, aplicando interceptor...", "color: cyan;");
                callback();
            }
        }, 800);
    }

    // Detectar cambios en la definición seleccionada
    function watchForDefinitionChanges() {
        const select = document.querySelector("select#select");
        if (!select) {
            console.warn("[Swagger Redirect] ⚠️ select#select no encontrado, reintentando...");
            setTimeout(watchForDefinitionChanges, 1500);
            return;
        }

        select.addEventListener("change", () => {
            const newContext = getCurrentContext();
            console.log(`%c[Swagger Redirect] Cambio de definición detectado → ${newContext}`, "color: orange; font-weight:bold;");

            // Esperar a que Swagger reconstruya el UI completamente
            const waitNewUI = setInterval(() => {
                if (window.ui && typeof window.ui.getSystem === "function") {
                    clearInterval(waitNewUI);
                    console.log("%c[Swagger Redirect] Nueva instancia de Swagger detectada. Reinyectando interceptor...", "color: cyan;");
                    attachInterceptor();
                }
            }, 1000);
        });
    }

    // Determinar el contexto actual (seguridad, contrato, factura)
    function getCurrentContext() {
        const url = new URL(window.location.href);
        const param = url.searchParams.get("urls.primaryName")?.toLowerCase() || "";

        if (param.includes("contrato")) return "contrato";
        if (param.includes("factura")) return "factura";
        if (param.includes("seguridad")) return "seguridad";

        const select = document.querySelector("select#select");
        const txt = select?.options[select.selectedIndex]?.textContent?.toLowerCase() || "";
        if (txt.includes("contrato")) return "contrato";
        if (txt.includes("factura")) return "factura";
        if (txt.includes("seguridad")) return "seguridad";

        return "seguridad";
    }

    // Inyectar interceptor de peticiones
    function attachInterceptor() {
        try {
            const system = window.ui.getSystem();
            if (!system) return console.warn("[Swagger Redirect] ❌ system no disponible todavía");

            const config = system.getConfigs?.();
            if (!config) return console.warn("[Swagger Redirect] ⚠️ Config no disponible aún");

            const context = getCurrentContext();
            const TARGET_BASE = `${window.location.origin}/proxy/${context}`.replace(/\/$/, "");

            console.log(`%c[Swagger Redirect] Contexto actual: ${context}`, "color: violet; font-weight:bold;");

            const prevRequest = config.requestInterceptor;
            const prevResponse = config.responseInterceptor;

            //config.requestInterceptor = (req) => {
            //    try {
            //        if (!req || !req.url) return req;
            //        if (req.url.includes("/swagger/") || req.url.endsWith(".json")) return req;

            //        // Normalizar URL
            //        if (req.url.startsWith("/")) {
            //            req.url = `${TARGET_BASE}${req.url}`;
            //        } else if (/^https?:\/\//i.test(req.url)) {
            //            try {
            //                const parsed = new URL(req.url);
            //                req.url = `${TARGET_BASE}${parsed.pathname}${parsed.search}`;
            //            } catch {
            //                req.url = `${TARGET_BASE}${req.url.replace(/^https?:\/\/[^/]+/i, "")}`;
            //            }
            //        }

            //        // Agregar token y encabezados
            //        const token = ui.getItem("jwt_token");
            //        if (token) req.headers["Authorization"] = `Bearer ${token}`;
            //        req.headers["X-Swagger-Context"] = context;

            //        req.url = req.url.replace(/([^:]\/)\/+/g, "$1");

            //        console.log(`[Swagger Redirect] → ${req.url} (contexto: ${context})`);
            //    } catch (err) {
            //        console.error("[Swagger Redirect] error al normalizar URL:", err);
            //    }

            //    return prevRequest ? prevRequest(req) : req;
            //};

            config.requestInterceptor = (req) => {
                try {
                    if (!req || !req.url) return req;
                    if (req.url.includes("/swagger/") || req.url.endsWith(".json")) return req;

                    // Normalizar URL
                    if (req.url.startsWith("/")) {
                        req.url = `${TARGET_BASE}${req.url}`;
                    } else if (/^https?:\/\//i.test(req.url)) {
                        try {
                            const parsed = new URL(req.url);
                            req.url = `${TARGET_BASE}${parsed.pathname}${parsed.search}`;
                        } catch {
                            req.url = `${TARGET_BASE}${req.url.replace(/^https?:\/\/[^/]+/i, "")}`;
                        }
                    }

                    // 🚨 Ajuste solicitado: tomar el token desde el propio header asignado por Swagger
                    const swaggerAuth = req.headers["Authorization"]; // <-- viene del botón Authorize

                    if (swaggerAuth) {
                        req.headers["Authorization"] = swaggerAuth;
                        console.log("[TOKEN QUE ENVÍA SWAGGER] →", swaggerAuth);
                    }

                    req.headers["X-Swagger-Context"] = context;

                    req.url = req.url.replace(/([^:]\/)\/+/g, "$1");

                    console.log(`[Swagger Redirect] → ${req.url} (contexto: ${context})`);
                } catch (err) {
                    console.error("[Swagger Redirect] error al normalizar URL:", err);
                }

                return prevRequest ? prevRequest(req) : req;
            };

            config.responseInterceptor = (res) => {
                try {
                    const isLogin = /\/api\/auth\/login$/i.test(res.url || "") && context === "seguridad";
                    if (isLogin && res.status === 200) {
                        let token = null;
                        if (typeof res.data === "string" && res.data.startsWith("eyJ")) {
                            token = res.data.trim();
                        } else if (res.data && typeof res.data === "object") {
                            token = res.data.token || res.data.accessToken || null;
                        }
                        if (token) {
                            localStorage.setItem("jwt_token", token);
                            console.log("%c[Swagger Redirect] 🔑 Token JWT guardado correctamente", "color: lime;");
                        }
                    }
                } catch (err) {
                    console.error("[Swagger Redirect] error en responseInterceptor:", err);
                }

                return prevResponse ? prevResponse(res) : res;
            };

            console.log(`%c[Swagger Redirect] ✅ Interceptor activo para: ${context}`, "color: yellow; font-weight:bold;");
        } catch (e) {
            console.error("[Swagger Redirect] ⚠️ Error al aplicar interceptor:", e);
        }
    }

    // Iniciar
    init();
})();