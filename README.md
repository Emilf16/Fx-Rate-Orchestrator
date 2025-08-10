# ExchangeApiBR — Fx Rate Orchestrator (ASP.NET Core .NET 9)

Proyecto de referencia que implementa un API Gateway para cotizaciones de tipo de cambio. El Gateway consulta en paralelo 3 proveedores dummy (formatos heterogéneos JSON/XML), aplica tolerancia a fallos y selecciona la mejor cotización (mayor monto convertido).

## Resumen ejecutivo

- .NET 9, ASP.NET Core, arquitectura limpia (Domain / Application / Presentation).
- Concurrencia y timeout por solicitud con cancelación cooperativa.
- Tolerante a fallos parciales: sigue respondiendo si uno o más proveedores fallan.
- Logging estructurado y pruebas unitarias.
- Docker Compose para orquestar Gateway + 3 providers.
- Swagger en el Gateway para exploración de la API.

## Estructura y arquitectura

```
ExchangeApiBR.sln
src/
  Domain/                      ← Modelos del dominio puros (sin dependencias externas)
    Models/
      ExchangeRequest.cs       ← Solicitud de cambio (moneda origen/destino, monto)
      ExchangeQuote.cs         ← Cotización (proveedor, tasa, total convertido)
      BestQuoteResult.cs       ← Resultado de mejor cotización
  Application/                 ← Casos de uso / lógica de aplicación
    Providers/
      IExchangeRateProvider.cs ← Contrato que deben implementar los proveedores
    Services/
      ExchangeAggregatorService.cs ← Orquesta llamadas concurrentes y selecciona la mejor oferta
  Gateway/                     ← Capa de presentación (API pública)
    Controllers/ExchangeController.cs ← Endpoint POST /api/exchange/best-quote
    Providers/Api{1,2,3}Provider.cs  ← Clientes HTTP a providers (parsean y devuelven valores crudos)
    Program.cs                 ← DI, HttpClient, Swagger, logging
  Provider.Api1/..2/..3/       ← Servicios dummy; devuelven respuestas aleatorias

docker-compose.yml             ← Orquestación de contenedores
```

Principios aplicados:

- Separación de responsabilidades y dependencias unidireccionales (Domain → Application → Presentation).
- Interfaz `IExchangeRateProvider` para facilitar la extensibilidad (Open/Closed, Dependency Inversion).
- Agregador con única responsabilidad: coordina proveedores, aplica timeout y selecciona la mejor oferta.

## Flujo de alto nivel

1. El Gateway recibe una solicitud y valida el DTO.
2. `ExchangeAggregatorService` lanza solicitudes a todos los proveedores en paralelo (con timeout global de 3s y cancelación enlazada).
3. Se descartan respuestas inválidas/errores y se conserva lo válido.
4. Se selecciona la mejor cotización por mayor `ConvertedAmount`.
5. El Gateway redondea a 2 decimales y responde.

## Contrato de la API (Gateway)

- Endpoint: `POST /api/exchange/best-quote`
- Request body:

```
{
  "sourceCurrency": "USD",      // string(3), requerido
  "targetCurrency": "BRL",      // string(3), requerido
  "amount": 1000                  // decimal > 0
}
```

- Response 200 OK (ejemplo):

```
{
  "provider": "API2",
  "rate": 6.32,                   // número; redondeado a 2 decimales
  "total": 6317                   // número; redondeado a 2 decimales
}
```

Notas: el Gateway redondea usando `MidpointRounding.AwayFromZero`. JSON no preserva ceros a la derecha (p.ej. 6317.00 → 6317).

- Errores relevantes:
  - 400 BadRequest: validación de entrada (DTO inválido)
  - 503 Service Unavailable: ningún proveedor válido respondió a tiempo
  - 504 Gateway Timeout: timeout global alcanzado
  - 500 Internal Server Error: error inesperado

## Resiliencia y rendimiento

- Concurrencia por solicitud: peticiones a proveedores en paralelo.
- Timeout global con `CancellationToken` enlazado y cancelación cooperativa.
- Tolerancia a fallos: errores individuales de proveedor no derriban la respuesta del Gateway.
- Selección determinística de la mejor oferta (máximo `ConvertedAmount`).

## Logging y observabilidad

- Logging estructurado en Gateway y clientes de proveedores (niveles Warning/Error para fallos remotos).
- Fácil de extender a OpenTelemetry (traces/metrics) si se requiere en entornos reales.

## Pruebas

- xUnit + Moq + FluentAssertions.
- Cobertura principal:
  - Selección de la mejor cotización.
  - Manejo de timeouts/errores de proveedores.
  - Validación de entrada en el Controller.

## Puesta en marcha

Requisitos: .NET 9 SDK, Docker Desktop.

- Local (sin contenedores):

  - `dotnet build`
  - `dotnet test`

- Docker Compose:
  - `docker compose build`
  - `docker compose up -d`

Gateway: http://localhost:8080
Swagger UI: http://localhost:8080/swagger

Prueba rápida (curl):

```
curl -s -X POST http://localhost:8080/api/exchange/best-quote \
  -H 'Content-Type: application/json' \
  -d '{"sourceCurrency":"USD","targetCurrency":"BRL","amount":1000}'
```

### Simular fallo de un proveedor

- Apagar un proveedor y mantener los otros activos:

```
docker compose stop provider3
```

- Probar el endpoint (el Gateway seguirá respondiendo con las ofertas válidas restantes).

## Configuración

- `src/Gateway/appsettings.json`:
  - `Providers:Api{1..3}:BaseUrl`: URLs base de proveedores.
- Cada Provider API expone `/rate` y retorna valores aleatorios para facilitar pruebas.

## Extensibilidad (agregar proveedor)

1. Implementar `IExchangeRateProvider` (cliente HTTP propio, parseo del formato remoto).
2. Registrar su `HttpClient` y la implementación en `Program.cs` del Gateway.
3. (Opcional) Añadir un nuevo servicio a docker-compose si es un microservicio separado.

## Decisiones clave y trade-offs

- El redondeo (2 decimales, `AwayFromZero`) está centralizado en el Gateway para normalizar salidas; los proveedores pueden devolver valores crudos.
- Se priorizó simplicidad y claridad del flujo sobre reintentos sofisticados; es trivial añadir Polly para políticas de reintento/backoff.
- Selección por mayor total convertido es congruente con "mejor cotización" orientada a maximizar cantidad recibida.

## Seguridad (notas)

- No expone secretos; configuración simple vía `appsettings`/variables de entorno.
- Para producción: habilitar HTTPS, rate limiting, validación de orígenes (CORS), autenticación/autorización y telemetría.
