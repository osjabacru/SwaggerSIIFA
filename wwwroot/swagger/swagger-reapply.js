setTimeout(() => {
    console.log("[Swagger Redirect] Forzando reinyección de interceptor...");
    const evt = new Event("change");
    const select = document.querySelector("select#select");
    if (select) select.dispatchEvent(evt);
}, 2000);