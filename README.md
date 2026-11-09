# Sistema PQR

API REST para la gestión de peticiones, quejas y reclamos de CENTRO DE ENSEÑANZA AUTOMOVILISTICA ELYON YIRETH S.A.S.

## Requisitos

- .NET 8 SDK
- PostgreSQL (local) o una base administrada, por ejemplo Neon
- Docker para crear la imagen de despliegue (opcional)

## Ejecución

Desde la carpeta raíz:

```powershell
dotnet run --project PqrSystem.Api
```

La API crea la tabla `Tickets` al iniciar. En local usa `ConnectionStrings:DefaultConnection` de `PqrSystem.Api/appsettings.json`. En producción, configura `DATABASE_URL` con la URL PostgreSQL del proveedor; cuando se proporciona, tiene prioridad sobre la cadena local. La URL PostgreSQL (`postgresql://usuario:contraseña@host/base?sslmode=require`) se convierte a una cadena Npgsql y TLS queda requerido por defecto.

Para compilar la imagen:

```powershell
docker build -t sistema-pqr .
```

El contenedor escucha en el puerto `10000`. Configura `DATABASE_URL`, `AdminSettings__Password` y, si usas respaldo, `GoogleFormSettings__FormUrl` en el servicio de despliegue. El esquema se crea al iniciar la API; los datos que estuvieran en una base SQLite local no se copian automáticamente a PostgreSQL y requieren una migración/exportación aparte.

El acceso administrativo requiere la cabecera `X-Admin-Password`. La contraseña debe configurarse como secreto mediante `AdminSettings__Password`; la aplicación no incluye una contraseña predeterminada. Sirve la aplicación exclusivamente sobre HTTPS. La contraseña ingresada en la interfaz se conserva solo en memoria mientras la página está abierta.

## Endpoints

- `POST /api/tickets`: crea un ticket. JSON: `{"documentoEstudiante":"DOC-001","nombreEstudiante":"ESTUDIANTE DE PRUEBA","categoria":"Servicio","descripcion":"Descripción de la solicitud"}`.
- `GET /api/tickets/estudiante/{documento}`: consulta todos los tickets asociados al documento, con su estado y respuesta administrativa.
- `GET /api/tickets/pendientes`: lista los tickets pendientes. Requiere `X-Admin-Password`.
- `PUT /api/tickets/{id}/respuesta`: resuelve un ticket. Requiere `X-Admin-Password`. JSON: `{"respuesta":"Respuesta para la solicitud"}`. La respuesta incluye el documento del estudiante para su notificación.

Las categorías válidas son `Infraestructura`, `Vehiculos`, `Servicio`, `Academico`, `Sugerencia`, `Felicitación` y `Otro`. El estado inicial es `Pendiente`; tras responder queda `Resuelto`.

El respaldo opcional envía cada ticket nuevo al Google Form configurado en `GoogleFormSettings__FormUrl`, mediante un formulario URL-encoded de 13 campos. La resolución administrativa permanece en PostgreSQL; Google Forms no permite actualizar una respuesta enviada previamente. Si el formulario no está configurado o falla la solicitud, el ticket se conserva en PostgreSQL y el error queda registrado.

## Pruebas

```powershell
dotnet test
```

Las pruebas unitarias usan un repositorio en memoria y no requieren una instancia de PostgreSQL.
