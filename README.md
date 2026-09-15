# L2 Sudamérica - Price Bot

Este es un bot de Discord desarrollado en C# / .NET 8 que permite gestionar una lista de precios para un servidor de Lineage 2, utilizando exclusivamente **Donator Coins (DC)** y un sistema de almacenamiento JSON persistente preparado para Railway.

## 📋 Requisitos Previos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (Solo para desarrollo local)
- [Docker](https://www.docker.com/) (Para desarrollo o despliegue)
- Una cuenta de [Railway](https://railway.app/) para el despliegue de producción.
- Acceso al [Discord Developer Portal](https://discord.com/developers/applications).

---

## 🛠️ Cómo crear el bot en Discord Developer Portal

1. Ve a [Discord Developer Portal](https://discord.com/developers/applications).
2. Haz clic en **"New Application"** y ponle un nombre al bot.
3. Ve a la pestaña **"Bot"** en el menú izquierdo.
4. En **"Privileged Gateway Intents"**, NO necesitas habilitar ninguno, ya que este bot usa Slash Commands exclusively.
5. Sube un ícono si lo deseas.

### Qué permisos necesita (OAuth2)
1. Ve a la pestaña **"OAuth2" -> "URL Generator"**.
2. En **Scopes**, selecciona `bot` y `applications.commands`.
3. En **Bot Permissions**, selecciona `Send Messages`, `Embed Links`, `Use Slash Commands`.
4. Copia la URL generada en la parte inferior y ábrela en una pestaña nueva para invitar el bot a tu servidor (Guild).

### Cómo obtener el Bot Token
1. En la pestaña **"Bot"**, haz clic en **"Reset Token"**.
2. Copia el token. **NUNCA COMPARTAS ESTE TOKEN NI LO SUBAS A GITHUB.**

---

## ⚙️ Configuración

Necesitarás varios IDs para que el bot funcione.

### Cómo obtener IDs en Discord
Para obtener IDs, debes habilitar el **"Modo Desarrollador"** en Discord (Ajustes de Usuario -> Avanzado -> Modo Desarrollador).

- **Cómo configurar el Admin Role ID**: Haz clic derecho en el rol de administrador en tu servidor de Discord y selecciona "Copiar ID de Rol". Pégalo en la variable correspondiente.
- **Cómo configurar el Guild ID**: Haz clic derecho en el nombre de tu servidor (arriba a la izquierda) y selecciona "Copiar ID de Servidor". *(Esto hace que al iniciar, el bot registre los comandos de forma rápida en tu servidor de pruebas en lugar de esperar la caché global de Discord de hasta una hora).*

### Variables de entorno necesarias:
- `DISCORD_TOKEN`: El token secreto del bot.
- `DISCORD_GUILD_ID`: (Opcional, pero recomendado) ID de tu servidor para registrar los comandos rápidamente.
- `ADMIN_ROLE_ID`: El ID del rol que podrá agregar/actualizar precios.
- `DATA_PATH`: La ruta donde se guardará el JSON. En Railway usa `/app/data/items.json`.
- `TIMEZONE`: (Opcional) La zona horaria para las fechas, por defecto `America/Argentina/Buenos_Aires`.

---

## 💻 Desarrollo Local

Para correr el proyecto localmente sin Docker:

1. Renombra o edita `appsettings.json` o define las variables de entorno (`.env` opcional).
2. Abre la consola en el directorio raíz.
3. Ejecuta:
   ```bash
   dotnet build
   dotnet run --project L2PriceBot.App
   ```

---

## 🚀 Despliegue en Railway

El proyecto incluye un `Dockerfile` y `railway.json` optimizados.

### Pasos exactos:

1. Crea un proyecto en Railway (por ejemplo, desde el dashboard "New Project -> Deploy from GitHub repo" y selecciona tu repositorio).
2. Si prefieres no usar un repo, puedes usar la [Railway CLI](https://docs.railway.app/guides/cli):
   ```bash
   railway login
   railway link
   railway up
   ```
3. En el **Dashboard de Railway**, ve a las configuraciones de tu servicio (`L2PriceBot`).
4. Ve a la pestaña **"Variables"** y agrega:
   - `DISCORD_TOKEN` = TuTokenAqui
   - `DISCORD_GUILD_ID` = IDdeTuServidor
   - `ADMIN_ROLE_ID` = IDdeTuRolAdmin
   - `DATA_PATH` = `/app/data/items.json`
5. **MUY IMPORTANTE (PERSISTENCIA):** Ve a la pestaña **"Volumes"**.
   - Haz clic en **"Add Volume"**.
   - Conecta el volumen a la ruta **Mount Path**: `/app/data`
6. Railway reiniciará el contenedor y montará el disco. A partir de ahora los precios (JSON interno) no se perderán nunca a la hora del redeploy o si el contenedor se cae.
7. Ve a **"Deployments"** o haz clic en "Deploy" si pausaste la aplicación previamente para verificar logs en caso de que todo funcione.
8. En tu servidor de Discord verás al Bot en línea.

---

## 📜 Lista de Comandos

Los comandos son exclusivamente Slash Commands (escribiendo `/` en Discord).

### Para Usuarios Normales
- `/precios`: 🏠 **Cómo consultar precios completos.** Devuelve una lista categorizada con todos los precios usando Embeds de forma prolija.
- `/precio item:<nombre>`: 🔍 Consulta información precisa sobre el Item buscado, junto con la fecha de la última actualización.
- `/precio-buscar item:<texto>`: 🔎 Busca coincidencias parciales si no se conoce el nombre completo del ítem, mostrando un máximo de 20 coincidencias.
- `/precio-categorias`: 📋 Muestra un resumen de todas las categorías cargadas.
- `/ayuda`: ℹ️ Detalla de forma básica lo que puede hacer el bot.

### Para Administradores (requieren el Admin Role ID)
- `/precio-agregar nombre:<nombre> precio:<dc> categoria:<cat>`: ➕ **Cómo agregar precios.** Ingresátelo. Admite formatos como "1500", "1.500", "1.5k".
- `/precio-actualizar item:<nombre> precio:<dc>`: ✏️ **Cómo actualizar precios.** El nombre debe ser exacto (ignora mayús./minús.).
- `/precio-eliminar item:<nombre>`: 🗑️ **Cómo eliminar precios.** 

*(Ejemplo visual: `150k` o `150.000` se transformará inequívocamente como `150.000 DC` y rechazará precios negativos / Adenas)*

¡Disfruta el bot para Lineage 2 Sudamérica!
