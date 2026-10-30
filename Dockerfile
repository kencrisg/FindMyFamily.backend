# =============================================================================
# FindMyFamily API — imagen de producción
# =============================================================================
# Construir desde la carpeta FindMyFamily.Backend:
#   docker build -t findmyfamily-api .
# Probar en local:
#   docker run --rm -p 8080:8080 --env-file .env.docker findmyfamily-api
# =============================================================================

# --- Etapa de compilación -----------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Primero solo los archivos de proyecto: así la capa de restore se reutiliza
# mientras no cambien las dependencias, y cada build posterior es mucho más rápido.
COPY FindMyFamily.Api/FindMyFamily.Api.csproj               FindMyFamily.Api/
COPY FindMyFamily.Core/FindMyFamily.Core.csproj             FindMyFamily.Core/
COPY FindMyFamily.Infrastructure/FindMyFamily.Infrastructure.csproj FindMyFamily.Infrastructure/
RUN dotnet restore FindMyFamily.Api/FindMyFamily.Api.csproj

# Ahora el código. El proyecto de tests no se copia: no hace falta para publicar.
COPY FindMyFamily.Api/        FindMyFamily.Api/
COPY FindMyFamily.Core/       FindMyFamily.Core/
COPY FindMyFamily.Infrastructure/ FindMyFamily.Infrastructure/

RUN dotnet publish FindMyFamily.Api/FindMyFamily.Api.csproj \
    -c Release -o /app/publish --no-restore

# --- Etapa de ejecución -------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# libgssapi-krb5-2: Npgsql intenta cargarla al abrir la conexión y, si falta, escribe un
# "Error: libgssapi_krb5.so.2: cannot open shared object file" en el log. La conexión
# funciona igual (usamos autenticación por contraseña), pero ese mensaje hace perder tiempo
# buscando un fallo que no existe.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

# Usuario sin privilegios: si alguien logra ejecutar código, no es root del contenedor.
RUN useradd --uid 10001 --create-home --shell /usr/sbin/nologin appuser \
    && chown -R appuser:appuser /app
USER appuser

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# El puerto real lo decide el proveedor con la variable PORT (Render, Railway, Koyeb…).
# 8080 es solo el valor por defecto cuando no la define nadie.
ENV PORT=8080
EXPOSE 8080

# `sh -c` es necesario para que ${PORT} se expanda en tiempo de ejecución:
# en la forma exec de ENTRYPOINT no hay shell que sustituya variables.
ENTRYPOINT ["sh", "-c", "exec dotnet FindMyFamily.Api.dll --urls http://0.0.0.0:${PORT}"]
